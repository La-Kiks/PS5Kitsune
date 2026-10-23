using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Games.Steps;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class AuditStepTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FakeProcessRunner _runner = new();
    private readonly GameLayout _game;
    private readonly AnyPS5Paths _paths;
    private readonly PythonEmbeddableTool _python;
    private readonly GameImportContext _context;

    public AuditStepTests()
    {
        var platform = new FakePlatform();
        var layout = new DataLayout(_temp.Combine("root"));
        _game = new GameLayout(_temp.Combine("games", TestGames.TitleId), TestGames.TitleId, platform);
        _paths = new AnyPS5Paths(layout, platform);
        _python = new PythonEmbeddableTool(new ToolDefinition("3.14.8", new Uri("https://example.test/p.zip"), new string('a', 64)), layout, platform, new FakeDownloader(), _runner);
        _context = new GameImportContext(new GameSource(_temp.Combine("unused"), null), new GameMetadata(TestGames.TitleId, "DOOM"), ["sce_module"], _game, toIntel: false);

        TestFiles.Write(Path.Combine(_game.Root, "PPSA99666.registry.json"), "[]");
        TestFiles.Write(Path.Combine(_game.Root, "PPSA99666.libc.registry.json"), "[]");
        TestFiles.Write(Path.Combine(_game.Root, "PPSA99666.needed.json"), "[]");
        TestFiles.Write(Path.Combine(_game.App0, "sce_module", "libc.prx"), TestFiles.Elf());
        TestFiles.Write(Path.Combine(_game.App0, "sce_module", "libc.prx.guest.prx"), "MZ fake pe"u8.ToArray());
    }

    private static string StagedModules => Path.Combine(".ps5kitsune", "audit-modules", "sce_module");

    private AuditStep CreateStep() => new(_context, _paths, _python, _runner);

    private void RespondWith(int exitCode, string auditJson) => _runner.Respond = spec =>
    {
        TestFiles.Write(Path.Combine(spec.WorkingDirectory!, "audit.json"), auditJson);
        return new FakeProcessResult(exitCode);
    };

    [Fact]
    public async Task Passes_every_registry_and_the_module_folders()
    {
        RespondWith(0, AuditFiles.Json(262, 0, 0));

        await CreateStep().ExecuteAsync(null, TestContext.Current.CancellationToken);

        var call = Assert.Single(_runner.Calls);
        Assert.Equal(_game.Root, call.WorkingDirectory);
        Assert.Equal(
            [_paths.ImportAudit, "PPSA99666.libc.registry.json", "PPSA99666.registry.json", "--libs", "libs", "--modules", StagedModules, "--json", "audit.json"],
            call.Arguments.Skip(2));
        Assert.Equal(AuditVerdict.Green, _context.Audit!.Verdict);
        Assert.Equal(262, _context.Audit.Implemented);
    }

    [Fact]
    public async Task Gives_the_audit_only_the_elf_modules()
    {
        List<string> seen = [];
        _runner.Respond = spec =>
        {
            var staged = Path.Combine(spec.WorkingDirectory!, StagedModules);
            seen.AddRange(Directory.GetFiles(staged, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(staged, path)));
            TestFiles.Write(Path.Combine(spec.WorkingDirectory!, "audit.json"), AuditFiles.Json(1, 0, 0));
            return new FakeProcessResult(0);
        };

        await CreateStep().ExecuteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(["libc.prx"], seen);
        Assert.True(File.Exists(Path.Combine(_game.App0, "sce_module", "libc.prx.guest.prx")));
    }

    [Fact]
    public async Task A_stale_staged_file_is_gone_when_the_audit_runs()
    {
        TestFiles.Write(Path.Combine(_game.AuditModules, "sce_module", "stale.prx"), TestFiles.Elf());
        var stale = true;
        _runner.Respond = spec =>
        {
            stale = File.Exists(Path.Combine(spec.WorkingDirectory!, StagedModules, "stale.prx"));
            TestFiles.Write(Path.Combine(spec.WorkingDirectory!, "audit.json"), AuditFiles.Json(1, 0, 0));
            return new FakeProcessResult(0);
        };

        await CreateStep().ExecuteAsync(null, TestContext.Current.CancellationToken);

        Assert.False(stale);
    }

    [Fact]
    public async Task Exit_code_1_still_reads_the_audit()
    {
        RespondWith(1, AuditFiles.Json(250, 0, 3));

        await CreateStep().ExecuteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(AuditVerdict.Red, _context.Audit!.Verdict);
        Assert.Equal(3, _context.Audit.Absent);
    }

    [Fact]
    public async Task Exit_code_2_fails_with_the_script_errors()
    {
        _runner.Respond = _ => new FakeProcessResult(2, StandardError: ["FAIL: PPSA99666.registry.json: bad registry"]);

        var exception = await Assert.ThrowsAsync<GameImportException>(() => CreateStep().ExecuteAsync(null, TestContext.Current.CancellationToken));

        Assert.Contains("bad registry", exception.Message, StringComparison.Ordinal);
        var failure = Assert.IsType<GameImportException>(exception);
        Assert.DoesNotContain(Environment.NewLine, failure.Summary, StringComparison.Ordinal);
        Assert.Equal(failure.Summary + Environment.NewLine + failure.Details, failure.Message);
    }

    [Fact]
    public async Task Without_a_registry_the_audit_does_not_run()
    {
        File.Delete(Path.Combine(_game.Root, "PPSA99666.registry.json"));
        File.Delete(Path.Combine(_game.Root, "PPSA99666.libc.registry.json"));

        await Assert.ThrowsAsync<GameImportException>(() => CreateStep().ExecuteAsync(null, TestContext.Current.CancellationToken));

        Assert.Empty(_runner.Calls);
    }

    [Fact]
    public void The_audit_always_runs_again()
    {
        Assert.False(CreateStep().IsAlreadyDone());
    }

    public void Dispose() => _temp.Dispose();
}
