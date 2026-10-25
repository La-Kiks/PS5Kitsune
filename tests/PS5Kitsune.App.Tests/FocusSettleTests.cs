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
