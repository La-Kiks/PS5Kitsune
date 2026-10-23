using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Games.Steps;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class UnwrapSelfStepTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FakeProcessRunner _runner = new();
    private readonly GameLayout _game;
    private readonly AnyPS5Paths _paths;
    private readonly PythonEmbeddableTool _python;

    public UnwrapSelfStepTests()
    {
        var platform = new FakePlatform();
        var layout = new DataLayout(_temp.Combine("root"));
        _game = new GameLayout(_temp.Combine("games", TestGames.TitleId), TestGames.TitleId, platform);
        _paths = new AnyPS5Paths(layout, platform);
        _python = new PythonEmbeddableTool(new ToolDefinition("3.14.8", new Uri("https://example.test/p.zip"), new string('a', 64)), layout, platform, new FakeDownloader(), _runner);

        TestFiles.Write(App0("eboot.bin"), TestFiles.Self());
        TestFiles.Write(App0("sce_module", "libc.prx"), TestFiles.Self());
        TestFiles.Write(App0("sce_module", "libfoo.prx"), TestFiles.Elf());
        TestFiles.Write(App0("sce_module", "notes.txt"), "not a binary");
        TestFiles.Write(App0("wads", "doom1.wad"), TestFiles.Self());

        // Plays unwrap_self.py: writes an ELF to the output path (last argument).
        _runner.Respond = spec =>
        {
            TestFiles.Write(spec.Arguments[^1], TestFiles.Elf());
            return new FakeProcessResult(0);
        };
    }

    private string App0(params string[] parts) => Path.Combine([_game.App0, .. parts]);

    private string Original(params string[] parts) => Path.Combine([_game.Originals, .. parts]);

    private UnwrapSelfStep CreateStep() => new(
        new GameImportContext(new GameSource(_temp.Combine("unused"), null), new GameMetadata(TestGames.TitleId, "DOOM"), ["sce_module"], _game, toIntel: false),
        _paths, _python, _runner);

    [Fact]
    public async Task Unwraps_the_self_files_after_backing_them_up()
    {
        var step = CreateStep();
        Assert.False(step.IsAlreadyDone());

        await step.ExecuteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(2, _runner.Calls.Count);
        Assert.Equal(TestFiles.Self(), File.ReadAllBytes(Original("eboot.bin")));
        Assert.Equal(TestFiles.Self(), File.ReadAllBytes(Original("sce_module", "libc.prx")));
        Assert.Equal(TestFiles.Elf(), File.ReadAllBytes(App0("eboot.bin")));
        Assert.Equal(TestFiles.Elf(), File.ReadAllBytes(App0("sce_module", "libc.prx")));
        Assert.False(File.Exists(Original("sce_module", "libfoo.prx")));
        Assert.False(File.Exists(Original("sce_module", "notes.txt")));
        Assert.False(File.Exists(Original("wads", "doom1.wad")));
        Assert.True(step.IsAlreadyDone());
    }

    [Fact]
    public async Task Runs_unwrap_self_py_from_the_backup_to_a_partial_file_then_moves_it_into_place()
    {
        await CreateStep().ExecuteAsync(null, TestContext.Current.CancellationToken);

        var call = _runner.Calls[0];
        Assert.Equal(_python.Executable, call.FileName);
        Assert.Equal([_paths.UnwrapSelf, Original("eboot.bin"), App0("eboot.bin") + ".partial"], call.Arguments.Skip(2));
        Assert.Equal(TestFiles.Elf(), File.ReadAllBytes(App0("eboot.bin")));
        Assert.Empty(Directory.EnumerateFiles(_game.App0, "*.partial", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task A_refused_container_says_the_game_must_come_already_unwrapped()
    {
        _runner.Respond = _ => new FakeProcessResult(2, StandardError: ["FAIL: eboot.bin: every load segment looks encrypted"]);

        var exception = await Assert.ThrowsAsync<GameImportException>(() => CreateStep().ExecuteAsync(null, TestContext.Current.CancellationToken));

        Assert.Contains("already unwrapped from the dump tool", exception.Message, StringComparison.Ordinal);
        Assert.Contains("every load segment looks encrypted", exception.Message, StringComparison.Ordinal);
        var failure = Assert.IsType<GameImportException>(exception);
        Assert.DoesNotContain(Environment.NewLine, failure.Summary, StringComparison.Ordinal);
        Assert.Equal(failure.Summary + Environment.NewLine + failure.Details, failure.Message);
        Assert.Contains("must come already unwrapped from the dump tool", failure.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resumes_from_the_backup_when_the_unwrapped_file_is_missing()
    {
        Directory.CreateDirectory(_game.Originals);
        File.Move(App0("eboot.bin"), Original("eboot.bin"));
        var step = CreateStep();
        Assert.False(step.IsAlreadyDone());

        await step.ExecuteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(TestFiles.Elf(), File.ReadAllBytes(App0("eboot.bin")));
        Assert.Equal(TestFiles.Self(), File.ReadAllBytes(Original("eboot.bin")));
    }

    [Fact]
    public async Task A_partial_output_never_replaces_the_backup()
    {
        Directory.CreateDirectory(_game.Originals);
        File.Move(App0("eboot.bin"), Original("eboot.bin"));
        TestFiles.Write(App0("eboot.bin"), "partial");

        await CreateStep().ExecuteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(TestFiles.Self(), File.ReadAllBytes(Original("eboot.bin")));
        Assert.Equal(TestFiles.Elf(), File.ReadAllBytes(App0("eboot.bin")));
        Assert.Equal(Original("eboot.bin"), _runner.Calls[0].Arguments[^2]);
    }

    [Fact]
    public async Task A_failed_run_leaves_no_partial_and_no_elf_so_a_rerun_unwraps_again()
    {
        _runner.Respond = spec =>
        {
            TestFiles.Write(spec.Arguments[^1], TestFiles.Elf()[..4]);
            return new FakeProcessResult(1);
        };
        var step = CreateStep();

        await Assert.ThrowsAsync<GameImportException>(() => step.ExecuteAsync(null, TestContext.Current.CancellationToken));

        Assert.Empty(Directory.EnumerateFiles(_game.App0, "*.partial", SearchOption.AllDirectories));
        Assert.False(File.Exists(App0("eboot.bin")));
        Assert.False(step.IsAlreadyDone());

        _runner.Respond = spec =>
        {
            TestFiles.Write(spec.Arguments[^1], TestFiles.Elf());
            return new FakeProcessResult(0);
        };
        await step.ExecuteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(TestFiles.Elf(), File.ReadAllBytes(App0("eboot.bin")));
        Assert.True(step.IsAlreadyDone());
    }

    [Fact]
    public async Task A_leftover_partial_is_deleted_before_the_run()
    {
        TestFiles.Write(App0("eboot.bin") + ".partial", "stale");
        var seen = new List<bool>();
        _runner.Respond = spec =>
        {
            seen.Add(File.Exists(spec.Arguments[^1]));
            TestFiles.Write(spec.Arguments[^1], TestFiles.Elf());
            return new FakeProcessResult(0);
        };

        await CreateStep().ExecuteAsync(null, TestContext.Current.CancellationToken);

        Assert.All(seen, existed => Assert.False(existed));
    }

    public void Dispose() => _temp.Dispose();
}
