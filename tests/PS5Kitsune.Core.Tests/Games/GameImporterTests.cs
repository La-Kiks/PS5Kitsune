using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Tests.AnyPS5;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class GameImporterTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly AnyPS5TestSetup _setup;
    private readonly GameImporter _importer;
    private int _relinkerExitCode;

    public GameImporterTests()
    {
        _setup = new AnyPS5TestSetup(_temp.Combine("root"));
        _setup.Runner.Respond = Respond;
        _importer = new GameImporter(
            _setup.Layout, _setup.Platform, _setup.Profile, _setup.Paths, _setup.Installer,
            new GameSourceResolver(_setup.Layout), new GameValidator(_ => long.MaxValue),
            _setup.PythonTool, _setup.Runner, _setup.StepRunner, _setup.Time, NullLogger<GameImporter>.Instance);
    }

    private string GameDirectory => _setup.Layout.GameDirectory(TestGames.TitleId);

    private string Staging => Path.Combine(_setup.Layout.Staging, TestGames.TitleId);

    private Task<AddGameResult> AddAsync(string input) =>
        _importer.AddAsync(input, toIntel: false, cancellationToken: TestContext.Current.CancellationToken);

    private string WriteZip()
    {
        var zip = _temp.Combine("PPSA99666.zip");
        File.WriteAllBytes(zip, TestGames.Zip("PPSA99666/"));
        return zip;
    }

    // Plays unwrap_self.py, the relinker and import_audit.py.
    private FakeProcessResult Respond(ProcessSpec spec)
    {
        switch (FakeProcessRunner.ScriptName(spec))
        {
            case "unwrap_self.py":
                TestFiles.Write(spec.Arguments[^1], TestFiles.Elf());
                return new FakeProcessResult(0);
            case "import_audit.py":
                TestFiles.Write(Path.Combine(spec.WorkingDirectory!, "audit.json"), AuditFiles.Json(262, 0, 0));
                return new FakeProcessResult(0);
        }

        if (spec.FileName != _setup.Paths.Relinker)
        {
            throw new InvalidOperationException($"Unexpected command {spec.FileName}");
        }

        // A failing relinker still leaves its first outputs behind.
        TestFiles.Write(Path.Combine(spec.WorkingDirectory!, "PPSA99666.exe"), "exe");
        TestFiles.Write(Path.Combine(spec.WorkingDirectory!, "PPSA99666.registry.json"), "[]");
        return _relinkerExitCode != 0
            ? new FakeProcessResult(_relinkerExitCode, StandardError: ["relinker: failure"])
            : new FakeProcessResult(0);
    }

    [Fact]
    public async Task Adding_requires_the_anyps5_install()
    {
        await Assert.ThrowsAsync<AnyPS5NotInstalledException>(() => AddAsync(WriteZip()));
    }

    [Fact]
    public async Task Adds_a_zip_end_to_end()
    {
        await _setup.InstallAsync();

        var added = Assert.IsType<GameAdded>(await AddAsync(WriteZip()));

        Assert.Equal("DOOM", added.Record.Name);
        Assert.Equal(262, added.Audit.Implemented);
        Assert.Empty(added.Audit.Unresolved);
        Assert.Equal("release", added.Record.ConvertedWith.Mode);
        Assert.Equal("v0.1.2", added.Record.ConvertedWith.Version);
        Assert.Equal(AuditVerdict.Green, added.Record.Audit.Verdict);
        Assert.Equal(GameDirectory, added.GameDirectory);
        Assert.Equal(TestFiles.Elf(), File.ReadAllBytes(Path.Combine(GameDirectory, "app0", "eboot.bin")));
        Assert.Equal(TestFiles.Self(), File.ReadAllBytes(Path.Combine(GameDirectory, ".ps5kitsune", "originals", "eboot.bin")));
        Assert.True(File.Exists(Path.Combine(GameDirectory, "libs", "libSceAudioOut.prx")));
        Assert.True(File.Exists(Path.Combine(GameDirectory, ".ps5kitsune", "game.json")));
        Assert.False(Directory.Exists(Staging));
    }

    [Fact]
    public async Task A_game_already_added_is_refused()
    {
        await _setup.InstallAsync();
        await AddAsync(WriteZip());

        var already = Assert.IsType<GameAlreadyAdded>(await AddAsync(WriteZip()));

        Assert.Equal(TestGames.TitleId, already.TitleId);
        Assert.Equal(GameDirectory, already.GameDirectory);
        Assert.False(Directory.Exists(Staging));
    }

    [Fact]
    public async Task An_invalid_game_is_rejected_before_anything_is_copied()
    {
        await _setup.InstallAsync();
        TestFiles.Write(_temp.Combine("broken", "eboot.bin"), TestFiles.Self());

        var rejected = Assert.IsType<GameRejected>(await AddAsync(_temp.Combine("broken")));

        Assert.Contains(rejected.Issues, issue => issue.Severity == IssueSeverity.Blocking && issue.Message.Contains("param.json", StringComparison.Ordinal));
        Assert.False(Directory.Exists(_setup.Layout.Games));
    }

    [Fact]
    public async Task A_failed_add_resumes_without_copying_or_unwrapping_again()
    {
        await _setup.InstallAsync();
        _relinkerExitCode = 2;

        var failure = await Assert.ThrowsAsync<StepFailedException>(() => AddAsync(WriteZip()));

        Assert.Equal("Convert with the relinker", failure.StepName);
        Assert.False(File.Exists(Path.Combine(GameDirectory, ".ps5kitsune", "game.json")));
        Assert.False(Directory.Exists(Staging));

        _relinkerExitCode = 0;
        var unwrapsBefore = _setup.Runner.Calls.Count(spec => FakeProcessRunner.ScriptName(spec) == "unwrap_self.py");

        Assert.IsType<GameAdded>(await AddAsync(WriteZip()));

        Assert.Equal(unwrapsBefore, _setup.Runner.Calls.Count(spec => FakeProcessRunner.ScriptName(spec) == "unwrap_self.py"));
    }

    [Fact]
    public async Task A_relinker_that_failed_after_writing_its_outputs_runs_again()
    {
        await _setup.InstallAsync();
        _relinkerExitCode = 2;
        await Assert.ThrowsAsync<StepFailedException>(() => AddAsync(WriteZip()));
        var relinksBefore = _setup.Runner.Calls.Count(spec => spec.FileName == _setup.Paths.Relinker);

        _relinkerExitCode = 0;
        Assert.IsType<GameAdded>(await AddAsync(WriteZip()));

        Assert.Equal(relinksBefore + 1, _setup.Runner.Calls.Count(spec => spec.FileName == _setup.Paths.Relinker));
    }

    [Fact]
    public async Task A_prepared_game_is_added_and_returns_its_audit()
    {
        await _setup.InstallAsync();
        await using var game = await _importer.PrepareAsync(WriteZip(), TestContext.Current.CancellationToken);

        var added = await _importer.AddAsync(game, toIntel: false, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(TestGames.TitleId, added.Record.TitleId);
        Assert.Equal(262, added.Audit.Implemented);
    }

    [Fact]
    public async Task A_blocked_or_added_game_is_refused()
    {
        await _setup.InstallAsync();
        var folder = _temp.Combine("blocked");
        TestGames.Create(folder);
        File.Delete(Path.Combine(folder, "sce_sys", "param.json"));
        await using var blocked = await _importer.PrepareAsync(folder, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _importer.AddAsync(blocked, false, cancellationToken: TestContext.Current.CancellationToken));

        await AddAsync(WriteZip());
        await using var again = await _importer.PrepareAsync(WriteZip(), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _importer.AddAsync(again, false, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Adding_a_prepared_game_requires_the_install()
    {
        await using var game = await _importer.PrepareAsync(WriteZip(), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<AnyPS5NotInstalledException>(() => _importer.AddAsync(game, false, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_install_is_checked_before_the_input()
    {
        await Assert.ThrowsAsync<AnyPS5NotInstalledException>(() => AddAsync(_temp.Combine("nothing.txt")));
    }

    public void Dispose() => _temp.Dispose();
    [Fact]
    public async Task A_disposed_game_is_refused()
    {
        await _setup.InstallAsync();
        var game = await _importer.PrepareAsync(WriteZip(), TestContext.Current.CancellationToken);
        await game.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => _importer.AddAsync(game, false, cancellationToken: TestContext.Current.CancellationToken));
    }
}
