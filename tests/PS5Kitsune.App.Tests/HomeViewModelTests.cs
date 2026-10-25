using System.Globalization;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Sessions;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Settings;
using PS5Kitsune.Core.Tests;

namespace PS5Kitsune.App.Tests;

public sealed class HomeViewModelTests : IDisposable
{
    private static readonly AnyPS5InstallRecord Record = new(1, "test-profile", AnyPS5Mode.Release, "v0.1.2", null, new Uri("https://example.test/u.py"), new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));

    private static readonly AnyPS5InstallRecord BuildRecord = new(2, "test-profile", AnyPS5Mode.Build, "48d61f5", new string('a', 40), null, new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));

    private readonly TempDirectory _temp = new();
    private readonly FakeInstaller _installer = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly Navigator _navigator = new();
    private readonly SessionHost _sessions;
    private readonly AppSettingsStore _settings;

    public HomeViewModelTests()
    {
        _sessions = new SessionHost(root => TestSessions.Create(root, _installer), _temp.Combine("root"));
        _settings = new AppSettingsStore(_temp.Combine("settings"));
    }

    private HomeViewModel Create() => new(_sessions, _settings, _dialogs, _navigator, new FakePages(), new TestPlatform());

    private async Task<HomeViewModel> CreateAsync()
    {
        var viewModel = Create();
        await viewModel.RefreshAsync();
        return viewModel;
    }

    [Fact]
    public async Task Shows_the_platform()
    {
        var viewModel = await CreateAsync();

        Assert.Equal("Test", viewModel.Platform);
        Assert.Equal("test-profile (validated)", viewModel.ProfileText);
    }

    [Fact]
    public async Task Nothing_installed_offers_to_install()
    {
        var viewModel = await CreateAsync();

        Assert.Equal(AnyPS5InstallStatus.NotInstalled, viewModel.Status);
        Assert.Equal("AnyPS5 is not installed", viewModel.StateTitle);
        Assert.Equal("Install AnyPS5", viewModel.ActionText);
        Assert.Equal("test-profile (validated)", viewModel.ProfileText);
        Assert.Equal(_temp.Combine("root"), viewModel.DataRoot);
    }

    [Fact]
    public async Task An_install_offers_to_verify_it()
    {
        _installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, Record);

        var viewModel = await CreateAsync();

        Assert.Equal("AnyPS5 v0.1.2 is installed", viewModel.StateTitle);
        Assert.Contains("test-profile", viewModel.StateDetail, StringComparison.Ordinal);
        Assert.Contains(Record.InstalledAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), viewModel.StateDetail, StringComparison.Ordinal);
        Assert.Equal("Verify / repair", viewModel.ActionText);
    }

    [Fact]
    public async Task An_incomplete_install_offers_to_resume()
    {
        _installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Incomplete, null);

        var viewModel = await CreateAsync();

        Assert.Equal("The AnyPS5 install is incomplete", viewModel.StateTitle);
        Assert.Equal("Resume the install", viewModel.ActionText);
    }

    [Fact]
    public async Task A_release_install_can_be_repaired_from_the_app()
    {
        _installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, Record);

        Assert.True((await CreateAsync()).StartCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_build_install_is_shown_and_can_be_repaired()
    {
        _installer.ActiveMode = AnyPS5Mode.Build;
        _installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, BuildRecord);

        var viewModel = await CreateAsync();

        Assert.Equal("AnyPS5 48d61f5 is installed (built from source)", viewModel.StateTitle);
        Assert.Contains(BuildRecord.InstalledAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), viewModel.StateDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("command line", viewModel.StateDetail, StringComparison.Ordinal);
        Assert.Equal("Verify / repair", viewModel.ActionText);
        Assert.True(viewModel.StartCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(AnyPS5InstallStatus.Incomplete)]
    [InlineData(AnyPS5InstallStatus.NotInstalled)]
    public async Task An_unfinished_build_can_be_resumed(AnyPS5InstallStatus status)
    {
        _installer.RequestedMode = AnyPS5Mode.Build;
        _installer.State = new AnyPS5InstallState(status, null);

        var viewModel = await CreateAsync();

        Assert.Equal("The AnyPS5 build is incomplete", viewModel.StateTitle);
        Assert.Equal("Resume it to finish: the steps already done are skipped.", viewModel.StateDetail);
        Assert.Equal("Resume the install", viewModel.ActionText);
        Assert.True(viewModel.StartCommand.CanExecute(null));
    }

    [Fact]
    public async Task The_check_migrates_the_old_install_first()
    {
        await CreateAsync();

        Assert.Equal(1, _installer.Migrations);
    }

    [Fact]
    public async Task A_failed_migration_is_shown_and_retried_on_the_next_refresh()
    {
        _installer.MigrateFailure = new AnyPS5MigrationException(new IOException("The file is in use."));

        var viewModel = await CreateAsync();

        Assert.Equal("Cannot read the AnyPS5 install", viewModel.StateTitle);
        Assert.Equal(@"Cannot move anyps5\current: The file is in use. Close any game using it and retry.", viewModel.StateDetail);
        Assert.False(viewModel.IsChecking);

        _installer.MigrateFailure = null;
        await viewModel.RefreshAsync();

        Assert.Equal(2, _installer.Migrations);
        Assert.Equal("AnyPS5 is not installed", viewModel.StateTitle);
    }

    [Fact]
    public async Task The_other_finished_install_is_listed()
    {
        _installer.ActiveMode = AnyPS5Mode.Release;
        _installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, Record);
        _installer.Records[AnyPS5Mode.Build] = BuildRecord;

        var viewModel = await CreateAsync();

        Assert.Equal("AnyPS5 v0.1.2 is installed", viewModel.StateTitle);
        Assert.Equal("Also installed: build 48d61f5", viewModel.OtherInstallText);
    }

    [Fact]
    public async Task No_other_install_lists_nothing()
    {
        _installer.ActiveMode = AnyPS5Mode.Release;
        _installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, Record);

        var viewModel = await CreateAsync();

        Assert.Null(viewModel.OtherInstallText);
    }

    [Fact]
    public async Task An_interrupted_switch_keeps_the_active_install_and_offers_to_resume()
    {
        _installer.ActiveMode = AnyPS5Mode.Release;
        _installer.RequestedMode = AnyPS5Mode.Build;
        _installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, Record);
        _installer.Records[AnyPS5Mode.Build] = BuildRecord;

        var viewModel = await CreateAsync();

        Assert.Equal("AnyPS5 v0.1.2 is installed", viewModel.StateTitle);
        Assert.Equal("The build install did not finish.", viewModel.StateDetail);
        Assert.Equal("Resume the install", viewModel.ActionText);
        Assert.Null(viewModel.OtherInstallText);
        Assert.Equal(AnyPS5Mode.Release, _installer.CheckedModes.Last());
    }

    [Fact]
    public async Task Start_goes_to_the_mode_page()
    {
        (await CreateAsync()).StartCommand.Execute(null);

        Assert.Equal("mode", _navigator.CurrentPage);
    }

    [Fact]
    public async Task Entering_the_page_refreshes_the_state()
    {
        var viewModel = await CreateAsync();
        _installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, Record);

        var done = new TaskCompletionSource();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(HomeViewModel.IsChecking) && !viewModel.IsChecking)
            {
                done.TrySetResult();
            }
        };

        viewModel.OnNavigatedTo();
        await done.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(AnyPS5InstallStatus.Installed, viewModel.Status);
    }

    [Fact]
    public async Task The_page_shows_it_is_checking_until_the_state_is_read()
    {
        using var gate = new ManualResetEventSlim();
        _installer.GetStateGate = gate;
        _installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, Record);
        var viewModel = Create();

        var refresh = viewModel.RefreshAsync();
        Assert.True(await _installer.GetStateEntered.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        Assert.Equal("Checking the AnyPS5 install…", viewModel.StateTitle);
        Assert.True(viewModel.IsChecking);
        Assert.False(viewModel.StartCommand.CanExecute(null));

        gate.Set();
        await refresh;

        Assert.Equal("AnyPS5 v0.1.2 is installed", viewModel.StateTitle);
        Assert.False(viewModel.IsChecking);
        Assert.True(viewModel.StartCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_superseded_check_is_discarded()
    {
        using var gateA = new ManualResetEventSlim();
        using var gateB = new ManualResetEventSlim();
        var viewModel = Create();

        _installer.GetStateGate = gateA;
        _installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.NotInstalled, null);
        var refreshA = viewModel.RefreshAsync();
        Assert.True(await _installer.GetStateEntered.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        _installer.GetStateGate = gateB;
        _installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, Record);
        var refreshB = viewModel.RefreshAsync();
        Assert.True(await _installer.GetStateEntered.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        // B finishes first, then A's older result arrives and must not overwrite it.
        gateB.Set();
        await refreshB;
        gateA.Set();
        await refreshA;

        Assert.Equal(AnyPS5InstallStatus.Installed, viewModel.Status);
        Assert.Equal("AnyPS5 v0.1.2 is installed", viewModel.StateTitle);
        Assert.Equal("Verify / repair", viewModel.ActionText);
        Assert.False(viewModel.IsChecking);
    }

    [Fact]
    public async Task A_failing_check_says_why_and_never_throws()
    {
        _installer.GetStateFailure = new IOException("disk gone");
        var viewModel = Create();

        await viewModel.RefreshAsync();

        Assert.Equal("Cannot read the AnyPS5 install", viewModel.StateTitle);
        Assert.Contains("disk gone", viewModel.StateDetail, StringComparison.Ordinal);
        Assert.False(viewModel.IsChecking);
    }

    [Fact]
    public async Task Changing_the_root_saves_it_and_replaces_the_session()
    {
        var viewModel = await CreateAsync();
        var first = _sessions.Current;
        _dialogs.Folder = _temp.Combine("other");

        await viewModel.ChangeRootCommand.ExecuteAsync(null);

        Assert.Equal(_temp.Combine("other"), viewModel.DataRoot);
        Assert.Equal(_temp.Combine("other"), _sessions.Current.Layout.Root);
        Assert.True(first.IsDisposed);
        Assert.Equal(_temp.Combine("other"), _settings.Load().Settings.DataRoot);
        Assert.Null(viewModel.RootError);
    }

    [Fact]
    public async Task Changing_the_root_keeps_the_theme()
    {
        await _settings.SaveAsync(new AppSettings(AppSettings.CurrentSchemaVersion, null, AppTheme.Dark), TestContext.Current.CancellationToken);
        var viewModel = await CreateAsync();
        _dialogs.Folder = _temp.Combine("other");

        await viewModel.ChangeRootCommand.ExecuteAsync(null);

        Assert.Equal(new AppSettings(AppSettings.CurrentSchemaVersion, _temp.Combine("other"), AppTheme.Dark), _settings.Load().Settings);
    }

    [Fact]
    public async Task Cancelling_the_picker_changes_nothing()
    {
        var viewModel = await CreateAsync();
        var first = _sessions.Current;

        await viewModel.ChangeRootCommand.ExecuteAsync(null);

        Assert.Same(first, _sessions.Current);
        Assert.False(File.Exists(_settings.FilePath));
    }

    [Fact]
    public async Task An_unusable_folder_keeps_the_root_and_says_why()
    {
        var viewModel = await CreateAsync();
        var file = _temp.Combine("a-file");
        await File.WriteAllTextAsync(file, "x", TestContext.Current.CancellationToken);
        _dialogs.Folder = Path.Combine(file, "root");

        await viewModel.ChangeRootCommand.ExecuteAsync(null);

        Assert.Equal(_temp.Combine("root"), viewModel.DataRoot);
        Assert.StartsWith("Cannot use ", viewModel.RootError, StringComparison.Ordinal);
        Assert.False(File.Exists(_settings.FilePath));
    }

    [Fact]
    public async Task A_failed_save_says_why_and_keeps_the_new_root()
    {
        await File.WriteAllTextAsync(_temp.Combine("settings"), "x", TestContext.Current.CancellationToken);
        var viewModel = await CreateAsync();
        _dialogs.Folder = _temp.Combine("other");

        await viewModel.ChangeRootCommand.ExecuteAsync(null);

        Assert.StartsWith("Cannot save the setting", viewModel.RootError, StringComparison.Ordinal);
        Assert.Equal(_temp.Combine("other"), viewModel.DataRoot);
        Assert.Equal(_temp.Combine("other"), _sessions.Current.Layout.Root);
    }

    [Fact]
    public async Task A_refused_change_keeps_the_root_and_the_setting()
    {
        var viewModel = await CreateAsync();
        var first = _sessions.Current;
        _sessions.IsBusy = true;
        _dialogs.Folder = _temp.Combine("other");

        await viewModel.ChangeRootCommand.ExecuteAsync(null);

        Assert.StartsWith("Cannot use ", viewModel.RootError, StringComparison.Ordinal);
        Assert.Equal(_temp.Combine("root"), viewModel.DataRoot);
        Assert.Same(first, _sessions.Current);
        Assert.False(File.Exists(_settings.FilePath));
    }

    [Fact]
    public async Task Adding_a_game_needs_an_install()
    {
        var viewModel = await CreateAsync();

        Assert.False(viewModel.AddGameCommand.CanExecute(null));
        Assert.Equal("Install AnyPS5 first to add games.", viewModel.AddGameHint);
    }

    [Fact]
    public async Task With_an_install_a_game_can_be_added()
    {
        _installer.ActiveMode = AnyPS5Mode.Release;
        _installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, Record);
        var viewModel = await CreateAsync();

        Assert.True(viewModel.AddGameCommand.CanExecute(null));
        Assert.Null(viewModel.AddGameHint);
        viewModel.AddGameCommand.Execute(null);
        Assert.Equal("choose", _navigator.CurrentPage);
    }

    public void Dispose()
    {
        _sessions.Dispose();
        _temp.Dispose();
    }

    private Task WriteGameAsync(string titleId, string name, ConversionInfo convertedWith) => GameRecordStore.WriteAsync(
        Path.Combine(_temp.Combine("root"), "games", titleId, ".ps5kitsune", "game.json"),
        new GameRecord
        {
            SchemaVersion = GameRecord.CurrentSchemaVersion,
            TitleId = titleId,
            Name = name,
            Executable = titleId + ".exe",
            ConvertedWith = convertedWith,
            Audit = new AuditRecord(360, 4, 0, AuditVerdict.Orange),
            AddedAt = DateTimeOffset.UnixEpoch,
        },
        TestContext.Current.CancellationToken);

    [Fact]
    public async Task The_library_button_opens_the_library_once_a_game_is_added()
    {
        _installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, Record);
        await WriteGameAsync("PPSA99666", "DOOM", new ConversionInfo("test-profile", "release", "v0.1.2", null, false));

        var viewModel = await CreateAsync();

        Assert.True(viewModel.HasGames);
        Assert.True(viewModel.LibraryCommand.CanExecute(null));
        viewModel.LibraryCommand.Execute(null);
        Assert.Equal("library", _navigator.CurrentPage);
    }

    [Fact]
    public async Task Without_games_the_library_button_is_disabled()
    {
        _installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, Record);

        var viewModel = await CreateAsync();

        Assert.False(viewModel.HasGames);
        Assert.False(viewModel.LibraryCommand.CanExecute(null));
    }
}
