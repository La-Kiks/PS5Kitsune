using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.Library;

public sealed class GameLibraryTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly DataLayout _layout;
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 20, 0, 0, TimeSpan.Zero));
    private readonly GameLibrary _library;

    public GameLibraryTests()
    {
        _layout = new DataLayout(_temp.Path);
        _library = new GameLibrary(_layout, new FakePlatform(), _time);
    }

    private Task WriteSampleAsync() => GameRecordStore.WriteAsync(
        Path.Combine(_layout.GameDirectory("PPSA99666"), ".ps5kitsune", "game.json"), TestRecords.Sample(), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Finds_a_recorded_game()
    {
        await WriteSampleAsync();

        var (game, record) = _library.Find("PPSA99666");

        Assert.Equal("DOOM", record.Name);
        Assert.Equal(_layout.GameDirectory("PPSA99666"), game.Root);
    }

    [Fact]
    public void An_unknown_title_id_names_the_games_folder()
    {
        var exception = Assert.Throws<GameNotFoundException>(() => _library.Find("PPSA00001"));

        Assert.Contains("PPSA00001", exception.Message, StringComparison.Ordinal);
        Assert.Contains(_layout.Games, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_title_id_with_path_characters_is_not_looked_up()
    {
        await WriteSampleAsync();

        Assert.Throws<GameNotFoundException>(() => _library.Find(Path.Combine("..", "games", "PPSA99666")));
    }

    [Fact]
    public async Task Marking_a_game_played_records_the_time()
    {
        await WriteSampleAsync();
        var (game, record) = _library.Find("PPSA99666");

        await _library.MarkPlayedAsync(game, record, TestContext.Current.CancellationToken);

        Assert.Equal(_time.GetLocalNow(), _library.Find("PPSA99666").Record.LastPlayedAt);
    }

    public void Dispose() => _temp.Dispose();
}
