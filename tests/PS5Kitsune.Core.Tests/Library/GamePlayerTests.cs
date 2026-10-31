using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Launch;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Tests.AnyPS5;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tests.Games;

namespace PS5Kitsune.Core.Tests.Library;

public sealed class GamePlayerTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly AnyPS5TestSetup _setup;
    private readonly FakeGameLauncher _launcher = new();
    private readonly GameLibrary _library;
    private readonly RunningGames _running;
    private readonly FakeLavapipe _lavapipe = new();
    private readonly FakeFontPack _fonts = new() { Installed = true };
    private readonly GamePlayer _player;

    public GamePlayerTests()
    {
        _setup = new AnyPS5TestSetup(_temp.Combine("root"));
        _library = new GameLibrary(_setup.Layout, _setup.Platform, _setup.Time, NullLogger<GameLibrary>.Instance);
        _running = new RunningGames(_launcher, NullLogger<RunningGames>.Instance);
        _player = new GamePlayer(
            _library, _setup.Installer, _launcher, _running,
            new LaunchPreparation(_lavapipe, _fonts, NullLogger<LaunchPreparation>.Instance), NullLogger<GamePlayer>.Instance);
    }

    private GameLayout Game => new(_setup.Layout.GameDirectory(TestGames.TitleId), TestGames.TitleId, _setup.Platform);

    private async Task AddDoomAsync()
    {
        await _setup.InstallAsync();
        ConvertedGames.Create(Game);
        await GameRecordStore.WriteAsync(Game.RecordFile, TestRecords.Sample(), TestContext.Current.CancellationToken);
    }

    private Task<PlayingGame> LaunchAsync() => _player.LaunchAsync(TestGames.TitleId, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Launching_starts_the_game_from_its_folder_records_it_and_marks_it_played()
    {
        await AddDoomAsync();

        var playing = await LaunchAsync();

        var request = Assert.Single(_launcher.Launches);
        Assert.Equal(Game.Executable, request.Executable);
        Assert.Equal(Game.Root, request.WorkingDirectory);
        Assert.Equal(Game.Logs, request.LogDirectory);
        Assert.Equal(_setup.Time.GetLocalNow(), playing.Record.LastPlayedAt);
        Assert.Equal(_setup.Time.GetLocalNow(), _library.Find(TestGames.TitleId).Record.LastPlayedAt);
        Assert.Equal(playing.Process.ProcessId, _running.Check(Game)!.ProcessId);
        Assert.False(GameLock.IsHeld(Game));
    }

    [Fact]
    public async Task Attaching_to_a_running_game_returns_the_same_process_and_to_a_stopped_one_returns_null()
    {
        await AddDoomAsync();
        Assert.Null(_player.Attach(TestGames.TitleId));

        var playing = await LaunchAsync();

        using var attached = _player.Attach(TestGames.TitleId)!.Process;
        Assert.Equal(playing.Process.ProcessId, attached.ProcessId);
    }

    [Fact]
    public async Task Listing_shows_which_games_run()
    {
        await AddDoomAsync();
        Assert.Null(Assert.Single(_player.List()).Running);

        var playing = await LaunchAsync();

        Assert.Equal(playing.Process.ProcessId, Assert.Single(_player.List()).Running!.ProcessId);
    }

    [Fact]
    public async Task Launching_requires_the_install()
    {
        ConvertedGames.Create(Game);
        await GameRecordStore.WriteAsync(Game.RecordFile, TestRecords.Sample(), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<AnyPS5NotInstalledException>(LaunchAsync);
        Assert.Empty(_launcher.Launches);
    }

    [Fact]
    public async Task A_running_game_is_not_launched_again()
    {
        await AddDoomAsync();
        var playing = await LaunchAsync();

        var refused = await Assert.ThrowsAsync<GameRunningException>(LaunchAsync);

        Assert.Equal("DOOM", refused.Name);
        Assert.Equal(playing.Process.ProcessId, refused.Running.ProcessId);
        Assert.Equal($"DOOM is already running (PID {playing.Process.ProcessId}). Log: {playing.Process.LogFile}.", refused.Message);
        Assert.Single(_launcher.Launches);
    }

    [Fact]
    public async Task A_game_whose_process_exited_is_launched_again()
    {
        await AddDoomAsync();
        ((FakeLaunchedGame)(await LaunchAsync()).Process).Exit(0);

        await LaunchAsync();

        Assert.Equal(2, _launcher.Launches.Count);
    }

    [Fact]
    public async Task A_busy_game_is_refused()
    {
        await AddDoomAsync();

        using (GameLock.TryAcquireNow(Game))
        {
            await Assert.ThrowsAsync<GameBusyException>(LaunchAsync);
        }

        Assert.Empty(_launcher.Launches);
    }

    [Fact]
    public async Task A_missing_executable_is_refused()
    {
        await AddDoomAsync();
        File.Delete(Game.Executable);

        var refused = await Assert.ThrowsAsync<GameExecutableMissingException>(LaunchAsync);

        Assert.Equal(Game.Executable, refused.Executable);
        Assert.Empty(_launcher.Launches);
        Assert.False(File.Exists(Game.RunningRecord));
    }

    [Fact]
    public async Task Launching_restores_an_interrupted_reconvert_first()
    {
        await AddDoomAsync();
        var before = ConvertedGames.Snapshot(Game.Root);
        ConversionBackup.SetAside(Game);
        TestFiles.Write(Game.Executable, "half-built exe");

        await LaunchAsync();

        Assert.False(Directory.Exists(Game.Previous));
        Assert.Equal(before["PPSA99666.exe"], ConvertedGames.Snapshot(Game.Root)["PPSA99666.exe"]);
    }

    [Fact]
    public async Task Forgetting_a_game_that_exited_deletes_its_record()
    {
        await AddDoomAsync();
        var playing = await LaunchAsync();
        ((FakeLaunchedGame)playing.Process).Exit(0);

        _player.Forget(playing);

        Assert.False(File.Exists(Game.RunningRecord));
    }

    private Task SetSettingsAsync(GameSettings settings) =>
        GameRecordStore.WriteAsync(Game.RecordFile, GameRecordStore.Read(Game.RecordFile)! with { Settings = settings }, TestContext.Current.CancellationToken);

    [Fact]
    public async Task The_settings_and_the_fonts_become_the_game_environment()
    {
        await AddDoomAsync();
        await SetSettingsAsync(new GameSettings { Language = 2 });

        await LaunchAsync();

        Assert.Equal(
            new Dictionary<string, string> { ["ANYPS5_LANGUAGE"] = "2", ["ANYPS5_SYSTEM_FONTS"] = _fonts.Folder },
            Assert.Single(_launcher.Launches).Environment);
    }

    [Fact]
    public async Task Lavapipe_is_downloaded_then_passed()
    {
        await AddDoomAsync();
        await SetSettingsAsync(new GameSettings { Lavapipe = true });

        await LaunchAsync();

        Assert.Equal(1, _lavapipe.Installs);
        var environment = Assert.Single(_launcher.Launches).Environment;
        Assert.Equal(_lavapipe.Manifest, environment["VK_DRIVER_FILES"]);
        Assert.Equal("llvmpipe", environment["ANYPS5_GPU"]);
    }

    [Fact]
    public async Task Lavapipe_turned_on_after_the_preparation_is_refused_when_mesa_is_missing()
    {
        await AddDoomAsync();
        _fonts.Installed = false;
        _fonts.Gate = new TaskCompletionSource();
        var launching = LaunchAsync();
        await SetSettingsAsync(new GameSettings { Lavapipe = true });
        _fonts.Gate.SetResult();

        var exception = await Assert.ThrowsAsync<LavapipeUnavailableException>(() => launching);

        Assert.Equal("Lavapipe is not installed: launch the test again", exception.Message);
        Assert.Empty(_launcher.Launches);
        Assert.False(GameLock.IsHeld(Game));
    }

    [Fact]
    public async Task Without_a_mesa_download_the_refusal_says_the_profile_has_none()
    {
        await AddDoomAsync();
        var player = new GamePlayer(
            _library, _setup.Installer, _launcher, _running,
            new LaunchPreparation(null, _fonts, NullLogger<LaunchPreparation>.Instance), NullLogger<GamePlayer>.Instance);
        _fonts.Installed = false;
        _fonts.Gate = new TaskCompletionSource();
        var launching = player.LaunchAsync(TestGames.TitleId, null, TestContext.Current.CancellationToken);
        await player.SaveSettingsAsync(TestGames.TitleId, new GameSettings { Lavapipe = true }, TestContext.Current.CancellationToken);
        _fonts.Gate.SetResult();

        var exception = await Assert.ThrowsAsync<LavapipeUnavailableException>(() => launching);

        Assert.Null(player.LavapipeHowToGet);
        Assert.Equal("Lavapipe is not installed: this profile has no Mesa download", exception.Message);
    }

    [Fact]
    public async Task A_cancel_requested_as_the_preparation_finishes_stops_the_launch()
    {
        await AddDoomAsync();
        _fonts.Installed = false;
        using var cancellation = new CancellationTokenSource();
        _fonts.OnInstalled = cancellation.Cancel;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _player.LaunchAsync(TestGames.TitleId, cancellation.Token));

        Assert.Equal(1, _fonts.Installs);
        Assert.Empty(_launcher.Launches);
        Assert.False(GameLock.IsHeld(Game));
    }

    [Fact]
    public async Task A_failed_launch_is_not_counted_as_played()
    {
        await AddDoomAsync();
        _launcher.LaunchFailure = new System.ComponentModel.Win32Exception(2);

        await Assert.ThrowsAsync<System.ComponentModel.Win32Exception>(LaunchAsync);

        Assert.Null(_library.Find(TestGames.TitleId).Record.LastPlayedAt);
        Assert.Null(_running.Check(Game));
    }

    [Fact]
    public async Task A_font_warning_reaches_the_caller()
    {
        await AddDoomAsync();
        _fonts.Installed = false;
        _fonts.Failure = new HttpRequestException("offline");

        var playing = await LaunchAsync();

        Assert.Equal("Fonts could not be downloaded (offline); games that use system fonts may show no text or stop.", playing.FontsWarning);
        Assert.False(Assert.Single(_launcher.Launches).Environment.ContainsKey("ANYPS5_SYSTEM_FONTS"));
    }

    [Fact]
    public async Task A_running_game_is_refused_before_any_download()
    {
        await AddDoomAsync();
        await LaunchAsync();
        _fonts.Installed = false;

        await Assert.ThrowsAsync<GameRunningException>(LaunchAsync);

        Assert.Equal(0, _fonts.Installs);
    }

    [Fact]
    public async Task Saving_settings_keeps_the_rest_of_the_record()
    {
        await AddDoomAsync();
        var played = DateTimeOffset.UnixEpoch;
        await GameRecordStore.WriteAsync(Game.RecordFile, GameRecordStore.Read(Game.RecordFile)! with { LastPlayedAt = played }, TestContext.Current.CancellationToken);

        var saved = await _player.SaveSettingsAsync(TestGames.TitleId, new GameSettings { Overlay = true, Gpu = " " }, TestContext.Current.CancellationToken);

        Assert.Equal(new GameSettings { Overlay = true }, saved.Settings);
        var record = GameRecordStore.Read(Game.RecordFile)!;
        Assert.Equal(new GameSettings { Overlay = true }, record.Settings);
        Assert.Equal(played, record.LastPlayedAt);
        Assert.Equal("DOOM", record.Name);
    }

    [Fact]
    public async Task Saving_settings_is_refused_while_the_game_runs_or_is_busy()
    {
        await AddDoomAsync();
        using (GameLock.TryAcquireNow(Game))
        {
            await Assert.ThrowsAsync<GameBusyException>(() => _player.SaveSettingsAsync(TestGames.TitleId, new GameSettings(), TestContext.Current.CancellationToken));
        }

        await LaunchAsync();
        await Assert.ThrowsAsync<GameRunningException>(() => _player.SaveSettingsAsync(TestGames.TitleId, new GameSettings(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Updating_settings_changes_what_game_json_holds_now()
    {
        await AddDoomAsync();
        await _player.SaveSettingsAsync(TestGames.TitleId, new GameSettings { Language = 5, Overlay = true }, TestContext.Current.CancellationToken);

        var saved = await _player.UpdateSettingsAsync(TestGames.TitleId, settings => settings with { Language = 1 }, TestContext.Current.CancellationToken);

        Assert.Equal(new GameSettings { Overlay = true }, saved.Settings);
        Assert.Equal(new GameSettings { Overlay = true }, _library.Find(TestGames.TitleId).Record.Settings);
    }

    [Fact]
    public async Task Updating_settings_refuses_a_running_game()
    {
        await AddDoomAsync();
        await LaunchAsync();

        await Assert.ThrowsAsync<GameRunningException>(
            () => _player.UpdateSettingsAsync(TestGames.TitleId, settings => settings with { Lavapipe = true }, TestContext.Current.CancellationToken));
    }

    public void Dispose() => _temp.Dispose();
}
