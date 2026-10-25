using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Threading;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Shell;
using PS5Kitsune.App.Tests.Headless;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.App.Views;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Checks.Vulkan;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Settings;
using PS5Kitsune.Core.Tests;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.App.Tests;

/// <summary>Pages that load in the background get their first focus once the content is there, not at Loaded.</summary>
[Collection("Headless")]
public sealed class FocusSettleTests(HeadlessSession session) : IDisposable
{
    private const string Doom = "PPSA99666";

    private readonly TempDirectory _temp = new();
    private readonly TestLibrary _library = new();

    [Fact]
    public async Task SystemCheck_focuses_Continue_once_the_check_is_done()
    {
        var pending = new TaskCompletionSource<SystemCheckReport>();
        var (before, after) = await session.RunUiAsync(async () =>
        {
            var navigator = new Navigator();
            var window = NewWindow(navigator);
            var viewModel = new SystemCheckViewModel(new PendingChecker(pending.Task), AnyPS5Mode.Release, new TestPlatform(), navigator, new FakePages());
            try
            {
                window.Show();
                navigator.GoTo(viewModel);
                Flush();
                var first = FocusTests.Describe(window.FocusManager!.GetFocusedElement());

                pending.SetResult(AllOk());
                await viewModel.CheckCommand.ExecutionTask!;
                Flush();
                return (first, FocusTests.Describe(window.FocusManager!.GetFocusedElement()));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.NotEqual("Continue", before.Text);
        Assert.Equal("Continue", after.Text);
    }

    [Fact]
    public async Task Focus_the_user_moved_to_the_sidebar_while_loading_is_left_alone()
    {
        var pending = new TaskCompletionSource<SystemCheckReport>();
        var (home, after) = await session.RunUiAsync(async () =>
        {
            var navigator = new Navigator();
            var window = NewWindow(navigator);
            var viewModel = new SystemCheckViewModel(new PendingChecker(pending.Task), AnyPS5Mode.Release, new TestPlatform(), navigator, new FakePages());
            try
            {
                window.Show();
                navigator.GoTo(viewModel);
                Flush();
                var button = window.GetVisualDescendants().OfType<Button>().Single(candidate => Equals(candidate.Content, "Home"));
                button.Focus(NavigationMethod.Tab);
                Flush();

                pending.SetResult(AllOk());
                await viewModel.CheckCommand.ExecutionTask!;
                Flush();
                return (button, window.FocusManager!.GetFocusedElement());
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Same(home, after);
    }

    [Fact]
    public async Task An_empty_library_focuses_Add_a_game_once_the_read_is_done()
    {
        using var gate = new ManualResetEventSlim();
        _library.Installer.ReadRecordGate = gate;
        var viewModel = new LibraryViewModel(_library.Player, _library.Cleaner, _library.Running, _library.Installer, _library.Platform, _library.Dialogs, new Navigator(), new FakePages());
        var (before, after) = await session.RunUiAsync(async () =>
        {
            var navigator = new Navigator();
            var window = NewWindow(navigator);
            try
            {
                window.Show();
                navigator.GoTo(viewModel);
                Flush();
                var first = FocusTests.Describe(window.FocusManager!.GetFocusedElement());

                gate.Set();
                await viewModel.RefreshAsync();
                Flush();
                return (first, FocusTests.Describe(window.FocusManager!.GetFocusedElement()));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.NotEqual("Add a game", before.Text);
        Assert.Equal("Add a game", after.Text);
    }

    [Fact]
    public async Task GameSettings_focuses_the_GPU_once_the_settings_are_loaded()
    {
        await _library.AddAsync(Doom, "DOOM");
        var viewModel = new GameSettingsViewModel(Doom, _library.Player, _library.Library, _library.Running, new FakeVulkanProbeRunner(), _library.Dialogs, new Navigator());
        var (before, after) = await session.RunUiAsync(async () =>
        {
            // Shown without OnNavigatedTo, so the page is still loading.
            var window = new Window { Content = new GameSettingsView { DataContext = viewModel } };
            try
            {
                window.Show();
                Flush();
                var first = FocusTests.Describe(window.FocusManager!.GetFocusedElement());

                viewModel.OnNavigatedTo();
                await viewModel.Loading;
                Flush();
                return (first, FocusTests.Describe(window.FocusManager!.GetFocusedElement()));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.NotEqual("GPU", before.Text);
        Assert.Equal("GPU", after.Text);
    }

    [Fact]
    public async Task Install_moves_focus_from_Cancel_to_Retry_when_the_run_fails()
    {
        IProgress<StepProgress>? reported = null;
        var release = new TaskCompletionSource();
        var installer = new FakeInstaller("Download the relinker")
        {
            Install = (progress, _, _) =>
            {
                reported = progress;
                return release.Task;
            },
        };
        var viewModel = new InstallViewModel(installer, AnyPS5Mode.Release, "C:/root/logs/app.log", new TestPlatform(), new InlineDispatcher(), new Navigator(), new FakePages(), _ => { });
        var (during, after) = await session.RunUiAsync(async () =>
        {
            var window = new Window { Content = new InstallView { DataContext = viewModel } };
            try
            {
                window.Show();
                Flush();
                var running = viewModel.RunAsync();
                Flush();
                reported!.Report(new StepProgress(Download: new(50, 200)));
                Flush();
                reported.Report(new StepProgress(Download: new(100, 200)));
                Flush();
                var cancel = FocusTests.Describe(window.FocusManager!.GetFocusedElement());

                release.SetException(new StepFailedException("Download the relinker", new IOException("disk full")));
                await running;
                Flush();
                return (cancel, FocusTests.Describe(window.FocusManager!.GetFocusedElement()));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal("Cancel", during.Text);
        Assert.Equal("Retry", after.Text);
    }

    [Fact]
    public async Task A_game_page_moves_focus_from_Library_to_Stop_once_the_game_runs()
    {
        await _library.AddAsync(Doom, "DOOM");
        await _library.Player.SaveSettingsAsync(Doom, new GameSettings { Lavapipe = true }, TestContext.Current.CancellationToken);
        _library.Lavapipe.Reports = [34L << 20];
        _library.Lavapipe.Gate = new TaskCompletionSource();
        var page = new GameViewModel(_library.Game(Doom), "DOOM", true, _library.Player, _library.Platform, new InlineDispatcher(), new Navigator(), new FakePages());
        var (during, after) = await session.RunUiAsync(async () =>
        {
            var window = new Window { Content = new GameView { DataContext = page } };
            try
            {
                window.Show();
                page.OnNavigatedTo();
                Flush();
                var first = FocusTests.Describe(window.FocusManager!.GetFocusedElement());

                _library.Lavapipe.Gate.SetResult();
                await page.Launching;
                Flush();
                return (first, FocusTests.Describe(window.FocusManager!.GetFocusedElement()));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Contains("Library", during.Text, StringComparison.Ordinal);
        Assert.Equal("Stop", after.Text);
    }

    public void Dispose()
    {
        _library.Dispose();
        _temp.Dispose();
    }

    private static void Flush()
    {
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
    }

    private MainWindow NewWindow(Navigator navigator) => new()
    {
        DataContext = new MainWindowViewModel(navigator, new FakeDialogs(), new FakePages(), new ThemeService(new AppSettingsStore(_temp.Combine("settings")), _ => { })),
    };

    private static SystemCheckReport AllOk() => new(
        [new CheckResult("os-arch", "64-bit x86 system", CheckStatus.Ok, "64-bit x86 system.")],
        new VulkanProbeOutcome(VulkanProbeStatus.Succeeded, new VulkanProbeReport(true, null, []), null));

    private sealed class PendingChecker(Task<SystemCheckReport> report) : ISystemChecker
    {
        public Task<SystemCheckReport> RunAsync(AnyPS5Mode mode, CancellationToken cancellationToken = default) => report;
    }
}
