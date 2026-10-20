using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Tests.Library;

internal static class TestRecords
{
    public static GameRecord Sample() => new()
    {
        SchemaVersion = GameRecord.CurrentSchemaVersion,
        TitleId = "PPSA99666",
        Name = "DOOM",
        Executable = "PPSA99666.exe",
        Icon = "app0/sce_sys/icon0.png",
        ConvertedWith = new ConversionInfo("doom-validated-2026-10", "release", "v0.1.2", null, false),
        Audit = new AuditRecord(262, 3, 0, AuditVerdict.Orange),
        AddedAt = new DateTimeOffset(2026, 10, 20, 15, 0, 0, TimeSpan.FromHours(2)),
    };
}
