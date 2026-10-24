using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
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
    private readonly GamePlayer _player;

    public GamePlayerTests()
    {
        _setup = new AnyPS5TestSetup(_temp.Combine("root"));
        _library = new GameLibrary(_setup.Layout, _setup.Platform, _setup.Time, NullLogger<GameLibrary>.Instance);
        _running = new RunningGames(_launcher, NullLogger<RunningGames>.Instance);
        _player = new GamePlayer(_library, _setup.Installer, _launcher, _running);
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

    public void Dispose() => _temp.Dispose();
}
