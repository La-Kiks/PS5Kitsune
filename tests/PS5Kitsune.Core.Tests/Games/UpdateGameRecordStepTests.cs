using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Games.Steps;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tests.Library;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class UpdateGameRecordStepTests : IDisposable
{
    private static readonly AnyPS5InstallRecord Build = new(2, "doom-validated-2026-10", AnyPS5Mode.Build, "3fd1cc3", new string('3', 40), null, DateTimeOffset.UnixEpoch);

    private readonly TempDirectory _temp = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 23, 18, 0, 0, TimeSpan.Zero));
    private readonly GameLayout _game;
    private readonly GameImportContext _context;

    public UpdateGameRecordStepTests()
    {
        _game = new GameLayout(_temp.Combine("games", TestGames.TitleId), TestGames.TitleId, new FakePlatform());
        _context = new GameImportContext(new GameSource(_game.App0, null), new GameMetadata(TestGames.TitleId, "DOOM"), ["sce_module"], _game, toIntel: true)
        {
            Audit = new AuditSummary(360, 4, 0, new Dictionary<string, int>()),
        };
    }

    [Fact]
    public async Task Discards_the_old_conversion_and_rewrites_the_conversion_fields_only()
    {
        ConvertedGames.Create(_game);
        ConversionBackup.SetAside(_game);
        var previous = TestRecords.Sample() with
        {
            ConvertedWith = new ConversionInfo("doom-validated-2026-10", "release", "v0.1.2", null, true),
            Settings = new GameSettings { Lavapipe = true, Language = 2 },
            LastPlayedAt = new DateTimeOffset(2026, 10, 22, 21, 0, 0, TimeSpan.Zero),
        };
        var step = new UpdateGameRecordStep(_context, previous, Build, _time);
        Assert.Equal("Record the game", step.Name);
        Assert.False(step.IsAlreadyDone());

        await step.ExecuteAsync(null, TestContext.Current.CancellationToken);

        Assert.False(Directory.Exists(_game.Previous));
        Assert.False(Directory.Exists(_game.PreviousDiscarded));
        var record = GameRecordStore.Read(_game.RecordFile)!;
        Assert.Equal(new ConversionInfo("doom-validated-2026-10", "build", "3fd1cc3", new string('3', 40), true), record.ConvertedWith);
        Assert.Equal(new AuditRecord(360, 4, 0, AuditVerdict.Orange), record.Audit);
        Assert.Equal(_time.GetLocalNow(), record.ReconvertedAt);
        Assert.Equal(previous with { ConvertedWith = record.ConvertedWith, Audit = record.Audit, ReconvertedAt = record.ReconvertedAt }, record);
    }

    [Fact]
    public async Task A_cancelled_step_keeps_the_old_conversion_and_the_record()
    {
        ConvertedGames.Create(_game);
        ConversionBackup.SetAside(_game);
        var record = TestRecords.Sample();
        await GameRecordStore.WriteAsync(_game.RecordFile, record, TestContext.Current.CancellationToken);
        var recordBefore = File.ReadAllText(_game.RecordFile);
        var step = new UpdateGameRecordStep(_context, record, Build, _time);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => step.ExecuteAsync(null, cancelled.Token));

        Assert.True(Directory.Exists(_game.Previous));
        Assert.False(Directory.Exists(_game.PreviousDiscarded));
        Assert.Equal(recordBefore, File.ReadAllText(_game.RecordFile));
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task Keeps_the_last_played_time_and_settings_written_during_the_reconvert()
    {
        ConvertedGames.Create(_game);
        ConversionBackup.SetAside(_game);
        var previous = TestRecords.Sample();
        var playedDuringTheRun = new DateTimeOffset(2026, 10, 23, 17, 30, 0, TimeSpan.Zero);
        await GameRecordStore.WriteAsync(
            _game.RecordFile, previous with { LastPlayedAt = playedDuringTheRun, Settings = new GameSettings { Language = 3 } }, TestContext.Current.CancellationToken);

        await new UpdateGameRecordStep(_context, previous, Build, _time).ExecuteAsync(null, TestContext.Current.CancellationToken);

        var record = GameRecordStore.Read(_game.RecordFile)!;
        Assert.Equal(playedDuringTheRun, record.LastPlayedAt);
        Assert.Equal(3, record.Settings.Language);
        Assert.Equal(_time.GetLocalNow(), record.ReconvertedAt);
    }
}
