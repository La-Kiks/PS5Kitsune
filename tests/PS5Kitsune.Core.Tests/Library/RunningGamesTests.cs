using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tests.Games;

namespace PS5Kitsune.Core.Tests.Library;

public sealed class RunningGamesTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FakeGameLauncher _launcher = new();
    private readonly RunningGames _running;
    private readonly GameLayout _game;

    public RunningGamesTests()
    {
        _running = new RunningGames(_launcher, NullLogger<RunningGames>.Instance);
        _game = new GameLayout(_temp.Combine("games", TestGames.TitleId), TestGames.TitleId, new FakePlatform());
        Directory.CreateDirectory(_game.Meta);
    }

    private async Task<FakeLaunchedGame> LaunchAsync()
    {
        var process = (FakeLaunchedGame)_launcher.Launch(new GameLaunchRequest(_game.Executable, _game.Root, _game.Logs));
        await _running.RecordAsync(_game, process, _game.Executable);
        return process;
    }

    [Fact]
    public async Task A_recorded_game_is_found_while_it_runs()
    {
        var process = await LaunchAsync();

        Assert.Same(process, _running.Attach(_game));
        Assert.Equal(
            new RunningGame(RunningGame.CurrentSchemaVersion, process.ProcessId, process.StartedAt, _game.Executable, process.LogFile),
            _running.Check(_game));
    }

    [Fact]
    public async Task The_start_time_survives_the_record_to_the_tick()
    {
        _launcher.Now = new DateTimeOffset(2026, 10, 24, 18, 3, 11, TimeSpan.Zero).AddTicks(4_170_123);
        var process = await LaunchAsync();

        var found = _running.Check(_game);

        Assert.NotNull(found);
        Assert.Equal(process.StartedAt, found.StartedAt);
        Assert.Equal(_launcher.Now, found.StartedAt);
    }

    [Fact]
    public void No_record_is_not_running()
    {
        Assert.Null(_running.Check(_game));
        Assert.Null(_running.Attach(_game));
    }

    [Fact]
    public async Task A_record_whose_process_exited_is_deleted()
    {
        var process = await LaunchAsync();
        process.Exit(0);

        Assert.Null(_running.Check(_game));
        Assert.False(File.Exists(_game.RunningRecord));
    }

    [Theory]
    [InlineData("{ \"processId\": ")]
    [InlineData("{ \"schemaVersion\": 2, \"processId\": 1, \"startedAt\": \"2026-10-24T18:03:11Z\", \"executable\": \"x\", \"logFile\": \"y\" }")]
    [InlineData("{ \"schemaVersion\": 1, \"processId\": 1, \"startedAt\": \"2026-10-24T18:03:11Z\" }")]
    public void A_malformed_record_is_deleted(string json)
    {
        TestFiles.Write(_game.RunningRecord, json);

        Assert.Null(_running.Check(_game));
        Assert.False(File.Exists(_game.RunningRecord));
    }

    [Fact]
    public async Task Forgetting_deletes_the_record_of_that_process_only()
    {
        var first = await LaunchAsync();
        first.Exit(0);
        var second = await LaunchAsync();

        _running.Forget(_game, first);
        Assert.True(File.Exists(_game.RunningRecord));

        _running.Forget(_game, second);
        Assert.False(File.Exists(_game.RunningRecord));
    }

    public void Dispose() => _temp.Dispose();
}
