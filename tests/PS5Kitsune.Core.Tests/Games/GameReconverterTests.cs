using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Tests.AnyPS5;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class GameReconverterTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly AnyPS5TestSetup _setup;
    private readonly GameImporter _importer;
    private readonly GameLibrary _library;
    private readonly GameReconverter _reconverter;
    private readonly CancellationTokenSource _cancellation = new();
    private int _relinkerExitCode;
    private int _auditExitCode;
    private bool _cancelDuringRelink;
    private string _relinkerOutput = "add";
    private bool _holdPreviousExecutable;
    private FileStream? _heldFile;

    public GameReconverterTests()
    {
        _setup = new AnyPS5TestSetup(_temp.Combine("root"));
        _setup.Runner.Respond = Respond;
        _importer = new GameImporter(
            _setup.Layout, _setup.Platform, _setup.Profile, _setup.Paths, _setup.Installer,
            new GameSourceResolver(_setup.Layout), new GameValidator(_ => long.MaxValue),
            _setup.PythonTool, _setup.Runner, _setup.StepRunner, _setup.Time, NullLogger<GameImporter>.Instance);
        _library = new GameLibrary(_setup.Layout, _setup.Platform, _setup.Time, NullLogger<GameLibrary>.Instance);
        _reconverter = new GameReconverter(
            _library, _setup.Paths, _setup.Profile, _setup.Installer, _setup.PythonTool, _setup.Runner, _setup.StepRunner, _setup.Time,
            NullLogger<GameReconverter>.Instance);
    }

    private GameLayout Game => new(_setup.Layout.GameDirectory(TestGames.TitleId), TestGames.TitleId, _setup.Platform);

    // Plays unwrap_self.py, the relinker and import_audit.py. The relinker's outputs carry _relinkerOutput, so a test sees which run wrote them.
    private FakeProcessResult Respond(ProcessSpec spec)
    {
        switch (FakeProcessRunner.ScriptName(spec))
        {
            case "unwrap_self.py":
                TestFiles.Write(spec.Arguments[^1], TestFiles.Elf());
                return new FakeProcessResult(0);
            case "import_audit.py":
                if (_auditExitCode != 0)
                {
                    return new FakeProcessResult(_auditExitCode, StandardError: ["audit: unreadable"]);
                }

                TestFiles.Write(Path.Combine(spec.WorkingDirectory!, "audit.json"), AuditFiles.Json(361, 3, 0));
                return new FakeProcessResult(0);
        }

        var root = spec.WorkingDirectory!;
        TestFiles.Write(Path.Combine(root, "PPSA99666.exe"), _relinkerOutput + " exe");
        TestFiles.Write(Path.Combine(root, "PPSA99666.registry.json"), _relinkerOutput + " registry");
        TestFiles.Write(Path.Combine(root, "app0", "sce_module", "libc.guest.prx"), _relinkerOutput + " pe");
        if (_holdPreviousExecutable)
        {
            // An exclusive handle inside previous makes the restore's rename of the folder fail.
            _heldFile = new FileStream(Path.Combine(Game.Previous, "PPSA99666.exe"), FileMode.Open, FileAccess.Read, FileShare.None);
        }

        if (_cancelDuringRelink)
        {
            _cancellation.Cancel();
        }

        return _relinkerExitCode != 0 ? new FakeProcessResult(_relinkerExitCode, StandardError: ["relinker: failure"]) : new FakeProcessResult(0);
    }

    /// <summary>Installs the release, adds DOOM, then records it as converted by the build (360/4/0) and lets the game create its files.</summary>
    private async Task AddDoomConvertedWithTheBuildAsync(bool toIntel = false)
    {
        await _setup.InstallAsync();
        var zip = _temp.Combine("PPSA99666.zip");
        File.WriteAllBytes(zip, TestGames.Zip("PPSA99666/"));
        Assert.IsType<GameAdded>(await _importer.AddAsync(zip, toIntel: false, cancellationToken: TestContext.Current.CancellationToken));

        var (game, record) = _library.Find(TestGames.TitleId);
        await GameRecordStore.WriteAsync(game.RecordFile, record with
        {
            ConvertedWith = new ConversionInfo("test-profile", "build", "3fd1cc3", new string('3', 40), toIntel),
            Audit = new AuditRecord(360, 4, 0, AuditVerdict.Orange),
        }, TestContext.Current.CancellationToken);
        foreach (var relative in new[] { "data/save.dat", "download0/dlc.bin", "_trophy/trophy.dat", "anyps5-input.ini", "anyps5-fonts/font.ttf", "shader_cache/v1/cache.bin" })
        {
            TestFiles.Write(Path.Combine(game.Root, relative), "kept " + relative);
        }

        TestFiles.Write(Path.Combine(game.Libs, "libOnlyInOldVersion.prx"), "old only");
        _relinkerOutput = "reconvert";
    }

    private Task<GameReconverted> ReconvertAsync() =>
        _reconverter.ReconvertAsync(TestGames.TitleId, cancellationToken: _cancellation.Token);

    [Fact]
    public void Lists_the_six_steps()
    {
        Assert.Equal(
            ["Set the old conversion aside", "Copy the AnyPS5 libraries", "Unwrap SELF files", "Convert with the relinker", "Audit the imports", "Record the game"],
            _reconverter.StepNames);
    }

    [Fact]
    public async Task Reconverting_rewrites_the_conversion_and_keeps_everything_else()
    {
        await AddDoomConvertedWithTheBuildAsync();
        var before = ConvertedGames.Snapshot(Game.Root);
        var (_, previous) = _library.Find(TestGames.TitleId);

        var result = await ReconvertAsync();

        var after = ConvertedGames.Snapshot(Game.Root);
        foreach (var (relative, content) in before.Where(entry => !ConvertedGames.IsConversionOutput(entry.Key)))
        {
            Assert.Equal(content, after[relative]);
        }

        Assert.Equal("reconvert exe", File.ReadAllText(Game.Executable));
        Assert.Equal("reconvert pe", File.ReadAllText(Path.Combine(Game.App0, "sce_module", "libc.guest.prx")));
        Assert.False(File.Exists(Path.Combine(Game.Libs, "libOnlyInOldVersion.prx")));
        Assert.True(File.Exists(Path.Combine(Game.Libs, "libSceAudioOut.prx")));
        Assert.False(Directory.Exists(Game.Previous));

        Assert.Equal(new AuditRecord(360, 4, 0, AuditVerdict.Orange), result.PreviousAudit);
        Assert.Equal(361, result.Audit.Implemented);
        Assert.Equal(Game.Root, result.GameDirectory);
        var record = GameRecordStore.Read(Game.RecordFile)!;
        Assert.Equal(record, result.Record);
        Assert.Equal(new ConversionInfo("test-profile", "release", "v0.1.2", null, false), record.ConvertedWith);
        Assert.Equal(new AuditRecord(361, 3, 0, AuditVerdict.Orange), record.Audit);
        Assert.Equal(_setup.Time.GetLocalNow(), record.ReconvertedAt);
        Assert.Equal(previous.AddedAt, record.AddedAt);
        Assert.Equal(previous.Settings, record.Settings);
        Assert.False(GameLibrary.NeedsReconversion(record, _setup.Installer.ReadRecord()!));
    }

    [Fact]
    public async Task The_intel_option_of_the_game_is_reused()
    {
        await AddDoomConvertedWithTheBuildAsync(toIntel: true);

        await ReconvertAsync();

        var relink = _setup.Runner.Calls.Last(spec => spec.FileName == _setup.Paths.Relinker);
        Assert.Contains("--to-intel", relink.Arguments);
        Assert.True(GameRecordStore.Read(Game.RecordFile)!.ConvertedWith.ToIntel);
    }

    [Fact]
    public async Task A_failed_relink_restores_the_previous_conversion()
    {
        await AddDoomConvertedWithTheBuildAsync();
        var before = ConvertedGames.Snapshot(Game.Root);
        _relinkerExitCode = 2;

        var failure = await Assert.ThrowsAsync<StepFailedException>(ReconvertAsync);

        Assert.Equal("Convert with the relinker", failure.StepName);
        Assert.Equal(before, ConvertedGames.Snapshot(Game.Root));
    }

    [Fact]
    public async Task A_failed_audit_restores_the_previous_conversion()
    {
        await AddDoomConvertedWithTheBuildAsync();
        var before = ConvertedGames.Snapshot(Game.Root);
        _auditExitCode = 2;

        var failure = await Assert.ThrowsAsync<StepFailedException>(ReconvertAsync);

        Assert.Equal("Audit the imports", failure.StepName);
        Assert.Equal(before, ConvertedGames.Snapshot(Game.Root));
    }

    [Fact]
    public async Task A_cancelled_reconvert_restores_the_previous_conversion()
    {
        await AddDoomConvertedWithTheBuildAsync();
        var before = ConvertedGames.Snapshot(Game.Root);
        _cancelDuringRelink = true;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(ReconvertAsync);

        Assert.Equal(before, ConvertedGames.Snapshot(Game.Root));
    }

    [Fact]
    public async Task A_failed_restore_is_reported_and_retried_later()
    {
        await AddDoomConvertedWithTheBuildAsync();
        var before = ConvertedGames.Snapshot(Game.Root);
        _relinkerExitCode = 2;
        _holdPreviousExecutable = true;

        ReconvertRestoreException failure;
        try
        {
            failure = await Assert.ThrowsAsync<ReconvertRestoreException>(ReconvertAsync);
        }
        finally
        {
            _heldFile?.Dispose();
            _heldFile = null;
        }

        var run = Assert.IsType<StepFailedException>(failure.InnerException);
        Assert.Equal("Convert with the relinker", run.StepName);
        Assert.NotNull(failure.RestoreFailure);
        Assert.True(Directory.Exists(Game.Previous));

        Assert.True(ConversionBackup.Restore(Game));
        Assert.Equal(before, ConvertedGames.Snapshot(Game.Root));
    }

    [Fact]
    public async Task A_running_game_stops_the_reconvert_with_nothing_changed()
    {
        await AddDoomConvertedWithTheBuildAsync();
        var before = ConvertedGames.Snapshot(Game.Root);

        StepFailedException failure;
        using (new FileStream(Game.Executable, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            failure = await Assert.ThrowsAsync<StepFailedException>(ReconvertAsync);
        }

        Assert.Equal("Set the old conversion aside", failure.StepName);
        Assert.Equal("Close the game if it is running.", Assert.IsType<GameImportException>(failure.InnerException).Details);
        Assert.Equal(before, ConvertedGames.Snapshot(Game.Root));
    }

    [Fact]
    public async Task An_interrupted_reconvert_is_restored_before_running()
    {
        await AddDoomConvertedWithTheBuildAsync();
        ConversionBackup.SetAside(Game);
        TestFiles.Write(Game.Executable, "half-built exe");

        await ReconvertAsync();

        Assert.Equal("reconvert exe", File.ReadAllText(Game.Executable));
        Assert.False(File.Exists(Path.Combine(Game.Libs, "libOnlyInOldVersion.prx")));
        Assert.False(Directory.Exists(Game.Previous));
    }

    [Fact]
    public async Task A_game_already_reconverted_elsewhere_is_refused_with_nothing_changed()
    {
        await AddDoomConvertedWithTheBuildAsync();
        var before = ConvertedGames.Snapshot(Game.Root);

        ReconvertInProgressException refused;
        using (ReconvertLock.TryAcquire(Game))
        {
            refused = await Assert.ThrowsAsync<ReconvertInProgressException>(ReconvertAsync);
        }

        Assert.Equal("A Reconvert of PPSA99666 is already in progress.", refused.Message);
        Assert.Equal(before, ConvertedGames.Snapshot(Game.Root));
    }

    [Fact]
    public async Task A_reader_restoring_the_game_makes_the_reconvert_refuse_with_nothing_changed()
    {
        await AddDoomConvertedWithTheBuildAsync();
        ConversionBackup.SetAside(Game);
        TestFiles.Write(Game.Executable, "half-built exe");
        var before = ConvertedGames.Snapshot(Game.Root);

        // The reader's hold on the lock while it restores, simulated.
        using (ReconvertLock.TryAcquire(Game))
        {
            await Assert.ThrowsAsync<ReconvertInProgressException>(ReconvertAsync);
        }

        Assert.Equal(before, ConvertedGames.Snapshot(Game.Root));
    }

    [Fact]
    public async Task A_failed_restore_of_an_interrupted_reconvert_is_reported_as_not_restored()
    {
        await AddDoomConvertedWithTheBuildAsync();
        ConversionBackup.SetAside(Game);
        TestFiles.Write(Game.Executable, "half-built exe");
        var callsBefore = _setup.Runner.Calls.Count;

        ReconvertRestoreException failure;
        using (new FileStream(Path.Combine(Game.Previous, "PPSA99666.exe"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            failure = await Assert.ThrowsAsync<ReconvertRestoreException>(ReconvertAsync);
        }

        Assert.IsAssignableFrom<IOException>(failure.RestoreFailure);
        Assert.Same(failure.RestoreFailure, failure.InnerException);
        Assert.Equal(callsBefore, _setup.Runner.Calls.Count);
        Assert.True(Directory.Exists(Game.Previous));
        Assert.False(ReconvertLock.IsHeld(Game));
    }

    [Fact]
    public async Task Reconverting_requires_the_install()
    {
        await Assert.ThrowsAsync<AnyPS5NotInstalledException>(ReconvertAsync);
    }

    [Fact]
    public async Task An_unknown_game_is_reported()
    {
        await _setup.InstallAsync();

        await Assert.ThrowsAsync<GameNotFoundException>(ReconvertAsync);
        Assert.False(Directory.Exists(Game.Root));
    }

    public void Dispose()
    {
        _cancellation.Dispose();
        _temp.Dispose();
    }
}
