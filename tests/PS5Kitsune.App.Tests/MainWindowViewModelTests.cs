using CommunityToolkit.Mvvm.ComponentModel;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Sessions;
using PS5Kitsune.App.Shell;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Settings;
using PS5Kitsune.Core.Tests;

namespace PS5Kitsune.App.Tests;

public sealed partial class MainWindowViewModelTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FakeInstaller _installer = new("Install Python");
    private readonly FakeDialogs _dialogs = new();
    private readonly Navigator _navigator = new();
    private readonly SessionHost _sessions;
    private readonly ThemeService _theme;
    private readonly List<AppTheme> _applied = [];
    private readonly MainWindowViewModel _viewModel;

    public MainWindowViewModelTests()
    {
        _sessions = new SessionHost(root => TestSessions.Create(root, _installer), _temp.Combine("a"));
        _theme = new ThemeService(new AppSettingsStore(_temp.Combine("settings")), _applied.Add);
        _viewModel = new MainWindowViewModel(_navigator, _dialogs, new FakePages(), _theme);
    }

    [Fact]
    public void The_sidebar_is_locked_while_the_page_runs_work()
    {
        var work = new FakeWork { IsRunning = true };
        _navigator.GoTo(work);

        Assert.True(_viewModel.IsBusy);
        Assert.False(_viewModel.GoLibraryCommand.CanExecute(null));
        Assert.Equal("Finish or cancel the current step first.", _viewModel.NavTip);

        work.IsRunning = false;

        Assert.False(_viewModel.IsBusy);
        Assert.True(_viewModel.GoLibraryCommand.CanExecute(null));
        Assert.Null(_viewModel.NavTip);
    }

    [Fact]
    public async Task Going_to_the_library_leaves_every_page_then_clears_the_history()
    {
        var game = new FakeLeavable();
        var settings = new FakeLeavable();
        _navigator.GoTo("library");
        _navigator.GoTo(game);
        _navigator.GoTo(settings);

        await _viewModel.GoLibraryCommand.ExecuteAsync(null);

        Assert.True(settings.Left);
        Assert.True(game.Left);
        Assert.Equal("library", _navigator.CurrentPage);
        Assert.Equal(["library"], _navigator.Pages);
    }

    [Fact]
    public async Task A_page_that_refuses_to_leave_keeps_the_user_there()
    {
        var settings = new FakeLeavable { Allows = false };
        _navigator.GoTo(settings);

        await _viewModel.GoHomeCommand.ExecuteAsync(null);

        Assert.Same(settings, _navigator.CurrentPage);
    }

    [Fact]
    public async Task Leaving_a_page_that_owns_a_game_releases_it()
    {
        var owner = new FakeOwner();
        _navigator.GoTo(owner);

        await _viewModel.GoHomeCommand.ExecuteAsync(null);

        Assert.True(owner.Released);
        Assert.Equal("home", _navigator.CurrentPage);
    }

    [Fact]
    public async Task A_page_that_fails_to_leave_does_not_block_navigation()
    {
        _navigator.GoTo(new FakeLeavable { Throws = true });

        await _viewModel.GoHomeCommand.ExecuteAsync(null);

        Assert.Equal("home", _navigator.CurrentPage);
    }

    [Fact]
    public void The_current_section_follows_the_page()
    {
        var home = new HomeViewModel(_sessions, new AppSettingsStore(_temp.Combine("settings")), _dialogs, _navigator, new FakePages(), new TestPlatform());
        _navigator.GoTo(home);
        Assert.Equal(NavSection.Home, _viewModel.CurrentSection);
        Assert.True(_viewModel.IsHomeCurrent);
        Assert.Equal("current", _viewModel.HomeItemStatus);

        _navigator.GoTo("mode");
        Assert.Equal(NavSection.None, _viewModel.CurrentSection);
        Assert.Null(_viewModel.HomeItemStatus);
    }

    [Fact]
    public async Task Clicking_Home_on_the_Home_page_does_nothing()
    {
        var below = new FakeLeavable();
        var home = new HomeViewModel(_sessions, new AppSettingsStore(_temp.Combine("settings")), _dialogs, _navigator, new FakePages(), new TestPlatform());
        _navigator.GoTo(below);
        _navigator.GoTo(home);

        await _viewModel.GoHomeCommand.ExecuteAsync(null);

        Assert.False(below.Left);
        Assert.Same(home, _navigator.CurrentPage);
        Assert.Equal(2, _navigator.Pages.Count());
    }

    [Fact]
    public async Task Clicking_Library_on_the_Library_page_does_nothing()
    {
        using var library = new TestLibrary();
        var below = new FakeLeavable();
        var page = new LibraryViewModel(library.Player, library.Cleaner, library.Running, library.Installer, library.Platform, library.Dialogs, _navigator, new FakePages());
        _navigator.GoTo(below);
        _navigator.GoTo(page);

        await _viewModel.GoLibraryCommand.ExecuteAsync(null);

        Assert.False(below.Left);
        Assert.Same(page, _navigator.CurrentPage);
        Assert.Equal(2, _navigator.Pages.Count());
        await page.Refreshing;
    }

    [Fact]
    public async Task Choosing_a_theme_applies_and_saves_it()
    {
        Assert.True(_viewModel.IsSystemTheme);

        _viewModel.IsDarkTheme = true;
        await _viewModel.ThemeSaving;

        Assert.Equal([AppTheme.Dark], _applied);
        Assert.True(_viewModel.IsDarkTheme);
        Assert.False(_viewModel.IsSystemTheme);
        Assert.Equal(AppTheme.Dark, new AppSettingsStore(_temp.Combine("settings")).Load().Settings.Theme);
    }

    private sealed class FakeLeavable : ILeavable
    {
        public bool Allows { get; init; } = true;

        public bool Throws { get; init; }

        public bool Left { get; private set; }

        public Task<bool> LeaveAsync()
        {
            Left = true;
            return Throws ? Task.FromException<bool>(new IOException("disk gone")) : Task.FromResult(Allows);
        }
    }

    [Fact]
    public async Task Closing_without_an_install_asks_nothing()
    {
        Assert.True(await _viewModel.ConfirmCloseAsync());
        Assert.Empty(_dialogs.Confirmations);
    }

    [Fact]
    public async Task Closing_during_an_install_asks_and_can_be_declined()
    {
        var install = StartRunningInstall();

        Assert.True(_viewModel.NeedsCloseHandling);
        Assert.False(await _viewModel.ConfirmCloseAsync());
        Assert.Single(_dialogs.Confirmations);
        Assert.True(install.IsRunning);

        await install.CancelAndWaitAsync();
    }

    [Fact]
    public async Task Confirming_cancels_the_install_before_closing()
    {
        var install = StartRunningInstall();
        _dialogs.Confirm = true;

        Assert.True(await _viewModel.ConfirmCloseAsync());
        Assert.False(install.IsRunning);
    }

    [Fact]
    public async Task Closing_on_a_page_that_owns_a_game_releases_it_without_asking()
    {
        var owner = new FakeOwner();
        _navigator.GoTo(owner);

        Assert.True(_viewModel.NeedsCloseHandling);
        Assert.True(await _viewModel.ConfirmCloseAsync());

        Assert.True(owner.Released);
        Assert.Empty(_dialogs.Confirmations);
    }

    [Fact]
    public async Task Running_work_without_a_quit_message_is_cancelled_without_asking()
    {
        var work = new FakeWork { IsRunning = true };
        _navigator.GoTo(work);

        Assert.True(await _viewModel.ConfirmCloseAsync());

        Assert.True(work.Cancelled);
        Assert.Empty(_dialogs.Confirmations);
    }

    private sealed class FakeOwner : IPreparedGameOwner
    {
        public bool Released { get; private set; }

        public Task ReleaseAsync()
        {
            Released = true;
            return Task.CompletedTask;
        }
    }

    private sealed partial class FakeWork : ObservableObject, IRunningWork
    {
        [ObservableProperty]
        public partial bool IsRunning { get; set; }

        public string? QuitMessage => null;

        public bool Cancelled { get; private set; }

        public Task CancelAndWaitAsync()
        {
            Cancelled = true;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Closing_during_an_add_cancels_it_and_releases_the_game()
    {
        var importer = new FakeGameImporter();
        var game = TestGameData.Prepared();
        importer.Add = async (_, _, _, _, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return TestGameData.Added();
        };
        var adding = new AddGameViewModel(importer, game, false, "app.log", new TestPlatform(), new InlineDispatcher(), _navigator, new FakePages());
        _navigator.GoTo(adding);
        _dialogs.Confirm = true;

        Assert.True(_viewModel.NeedsCloseHandling);
        Assert.True(await _viewModel.ConfirmCloseAsync());

        Assert.Equal("Adding a game is in progress. Quitting cancels it; adding it again resumes where it stopped.", Assert.Single(_dialogs.Confirmations));
        Assert.True(game.IsDisposed);
    }

    private InstallViewModel StartRunningInstall()
    {
        _installer.Install = (_, _, cancellationToken) => Task.Delay(Timeout.Infinite, cancellationToken);
        var install = new InstallViewModel(_installer, AnyPS5Mode.Release, "app.log", new TestPlatform(), new InlineDispatcher(), _navigator, new FakePages());
        _navigator.GoTo(install);
        return install;
    }

    public void Dispose()
    {
        _sessions.Dispose();
        _temp.Dispose();
    }
}
