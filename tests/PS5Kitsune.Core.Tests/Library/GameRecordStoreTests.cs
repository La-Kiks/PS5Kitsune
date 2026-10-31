using System.Text.Json.Nodes;
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
    public async Task The_resolution_round_trips()
    {
        var record = TestRecords.Sample() with { Settings = new GameSettings { Resolution = "2160p" } };

        await GameRecordStore.WriteAsync(RecordFile, record, TestContext.Current.CancellationToken);

        Assert.Equal("2160p", GameRecordStore.Read(RecordFile)!.Settings.Resolution);
        Assert.Contains("\"resolution\": \"2160p\"", File.ReadAllText(RecordFile), StringComparison.Ordinal);
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

    [Fact]
    public void A_record_without_reconverted_at_reads_as_never_reconverted()
    {
        TestFiles.Write(RecordFile, """
            {
              "schemaVersion": 1, "titleId": "PPSA99666", "name": "DOOM", "executable": "PPSA99666.exe",
              "convertedWith": { "profile": "p", "mode": "release", "version": "v0.1.2", "commit": null, "toIntel": false },
              "audit": { "implemented": 361, "stub": 3, "absent": 0, "verdict": "orange" },
              "addedAt": "2026-10-23T10:00:00+02:00"
            }
            """);

        Assert.Null(GameRecordStore.Read(RecordFile)!.ReconvertedAt);
    }

    [Fact]
    public async Task Extra_variables_round_trip_sorted_by_name()
    {
        var record = TestRecords.Sample() with { Settings = new GameSettings { ExtraEnvironment = new Dictionary<string, string> { ["APS5_B"] = "2", ["APS5_A"] = "1" } } };

        await GameRecordStore.WriteAsync(RecordFile, record, TestContext.Current.CancellationToken);

        Assert.Equal(record, GameRecordStore.Read(RecordFile));
        var json = File.ReadAllText(RecordFile);
        Assert.Contains("\"extraEnvironment\": {", json, StringComparison.Ordinal);
        Assert.True(json.IndexOf("\"APS5_A\": \"1\"", StringComparison.Ordinal) < json.IndexOf("\"APS5_B\": \"2\"", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_record_without_the_new_settings_reads_with_their_defaults(bool asNull)
    {
        await GameRecordStore.WriteAsync(RecordFile, TestRecords.Sample(), TestContext.Current.CancellationToken);
        var node = JsonNode.Parse(File.ReadAllText(RecordFile))!;
        var settingsNode = node["settings"]!.AsObject();
        settingsNode.Remove("resolution");
        settingsNode.Remove("extraEnvironment");
        if (asNull)
        {
            settingsNode["extraEnvironment"] = null;
        }

        File.WriteAllText(RecordFile, node.ToJsonString());

        var settings = GameRecordStore.Read(RecordFile)!.Settings;
        Assert.Null(settings.Resolution);
        Assert.Empty(settings.ExtraEnvironment);
    }

    public void Dispose() => _temp.Dispose();
}
