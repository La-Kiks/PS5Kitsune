using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Games.Steps;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class WriteGameRecordStepTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
    private readonly GameImportContext _context;

    public WriteGameRecordStepTests()
    {
        var game = new GameLayout(_temp.Combine("games", TestGames.TitleId), TestGames.TitleId, new FakePlatform());
        _context = new GameImportContext(new GameSource(_temp.Combine("unused"), null), new GameMetadata(TestGames.TitleId, "DOOM"), ["sce_module"], game, toIntel: true);
        TestFiles.Write(Path.Combine(game.App0, "sce_sys", "icon0.png"), "png");
    }

    private static readonly AnyPS5InstallRecord Release = new(2, "doom-validated-2026-10", AnyPS5Mode.Release, "v0.1.2", null, new Uri("https://example.test/u.py"), default);

    private static readonly AnyPS5InstallRecord Build = new(2, "doom-validated-2026-10", AnyPS5Mode.Build, "3fd1cc3", "3fd1cc393b8512c2e989860053f30dee5a8cd1aa", null, default);

    private WriteGameRecordStep CreateStep(AnyPS5InstallRecord? install = null) => new(_context, install ?? Release, _time, lavapipe: false);

    [Fact]
    public async Task Records_the_game_with_default_settings()
    {
        _context.Audit = new AuditSummary(262, 0, 0, new Dictionary<string, int>());
        var step = CreateStep();
        Assert.False(step.IsAlreadyDone());

        await step.ExecuteAsync(null, TestContext.Current.CancellationToken);

        var record = GameRecordStore.Read(_context.Game.RecordFile)!;
        Assert.Equal("PPSA99666", record.TitleId);
        Assert.Equal("DOOM", record.Name);
        Assert.Equal("PPSA99666.exe", record.Executable);
        Assert.Equal("app0/sce_sys/icon0.png", record.Icon);
        Assert.Equal(new ConversionInfo("doom-validated-2026-10", "release", "v0.1.2", null, true), record.ConvertedWith);
        Assert.Equal(new AuditRecord(262, 0, 0, AuditVerdict.Green), record.Audit);
        Assert.Equal(new GameSettings(), record.Settings);
        Assert.Equal(_time.GetLocalNow(), record.AddedAt);
        Assert.Null(record.LastPlayedAt);
        Assert.True(step.IsAlreadyDone());
    }

    [Fact]
    public async Task A_game_converted_with_a_build_records_the_build()
    {
        _context.Audit = new AuditSummary(262, 0, 0, new Dictionary<string, int>());

        await CreateStep(Build).ExecuteAsync(null, TestContext.Current.CancellationToken);

        var record = GameRecordStore.Read(_context.Game.RecordFile)!;
        Assert.Equal(new ConversionInfo("doom-validated-2026-10", "build", "3fd1cc3", "3fd1cc393b8512c2e989860053f30dee5a8cd1aa", true), record.ConvertedWith);
    }

    [Fact]
    public async Task The_audit_must_run_first()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateStep().ExecuteAsync(null, TestContext.Current.CancellationToken));
    }

    public void Dispose() => _temp.Dispose();
}
