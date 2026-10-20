using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Tests.Library;

public sealed class GameRecordStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    private string RecordFile => _temp.Combine(".ps5kitsune", "game.json");

    [Fact]
    public async Task A_record_round_trips_through_game_json()
    {
        var record = TestRecords.Sample();

        await GameRecordStore.WriteAsync(RecordFile, record, TestContext.Current.CancellationToken);

        Assert.Equal(record, GameRecordStore.Read(RecordFile));
        var json = File.ReadAllText(RecordFile);
        Assert.Contains("\"schemaVersion\": 1", json, StringComparison.Ordinal);
        Assert.Contains("\"verdict\": \"orange\"", json, StringComparison.Ordinal);
        Assert.Contains("\"presentMode\": \"fifo\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_record_reads_as_null()
    {
        Assert.Null(GameRecordStore.Read(RecordFile));
    }

    [Fact]
    public async Task Another_schema_version_is_refused()
    {
        await GameRecordStore.WriteAsync(RecordFile, TestRecords.Sample() with { SchemaVersion = 2 }, TestContext.Current.CancellationToken);

        var exception = Assert.Throws<InvalidGameRecordException>(() => GameRecordStore.Read(RecordFile));

        Assert.Contains("schema version 2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_malformed_record_is_refused()
    {
        TestFiles.Write(RecordFile, "{ \"titleId\": ");

        Assert.Throws<InvalidGameRecordException>(() => GameRecordStore.Read(RecordFile));
    }

    public void Dispose() => _temp.Dispose();
}
