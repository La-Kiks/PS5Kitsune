using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Games.Steps;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class RelinkStepTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FakeProcessRunner _runner = new();
    private readonly GameLayout _game;
    private readonly AnyPS5Paths _paths;

    public RelinkStepTests()
    {
        var platform = new FakePlatform();
        _game = new GameLayout(_temp.Combine("games", TestGames.TitleId), TestGames.TitleId, platform);
        _paths = new AnyPS5Paths(new DataLayout(_temp.Combine("root")), platform);

        // Plays the relinker: writes the executable and its registry in the working directory.
        _runner.Respond = spec =>
        {
            TestFiles.Write(Path.Combine(spec.WorkingDirectory!, "PPSA99666.exe"), "exe");
            TestFiles.Write(Path.Combine(spec.WorkingDirectory!, "PPSA99666.registry.json"), "[]");
            return new FakeProcessResult(0);
        };
    }

    private RelinkStep CreateStep(bool toIntel) => new(
        new GameImportContext(new GameSource(_temp.Combine("unused"), null), new GameMetadata(TestGames.TitleId, "DOOM"), ["sce_module"], _game, toIntel),
        _paths, ProfileLoader.LoadEmbedded(), _runner);

    [Fact]
    public async Task Runs_the_relinker_from_the_game_folder()
    {
        var step = CreateStep(toIntel: false);
        Assert.False(step.IsAlreadyDone());

        await step.ExecuteAsync(null, TestContext.Current.CancellationToken);

        var call = Assert.Single(_runner.Calls);
        Assert.Equal(_paths.Relinker, call.FileName);
        Assert.Equal(_game.Root, call.WorkingDirectory);
        Assert.Equal(["--windows", "--registry", Path.Combine("app0", "eboot.bin"), "PPSA99666.exe"], call.Arguments);
        Assert.True(step.IsAlreadyDone());
    }

    [Fact]
    public async Task Adds_the_intel_option_on_an_intel_cpu()
    {
        await CreateStep(toIntel: true).ExecuteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(["--windows", "--registry", "--to-intel", Path.Combine("app0", "eboot.bin"), "PPSA99666.exe"], _runner.Calls[0].Arguments);
    }

    [Fact]
    public async Task A_failure_quotes_the_relinker_errors_and_names_the_intel_option()
    {
        _runner.Respond = _ => new FakeProcessResult(2, StandardError: ["error: unsupported instruction vpdpbusd"]);

        var exception = await Assert.ThrowsAsync<GameImportException>(() => CreateStep(toIntel: true).ExecuteAsync(null, TestContext.Current.CancellationToken));

        Assert.Contains("exit code 2", exception.Message, StringComparison.Ordinal);
        Assert.Contains("--to-intel", exception.Message, StringComparison.Ordinal);
        Assert.Contains("unsupported instruction vpdpbusd", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_run_that_wrote_its_outputs_is_not_done()
    {
        _runner.Respond = spec =>
        {
            TestFiles.Write(Path.Combine(spec.WorkingDirectory!, "PPSA99666.exe"), "exe");
            TestFiles.Write(Path.Combine(spec.WorkingDirectory!, "PPSA99666.registry.json"), "[]");
            return new FakeProcessResult(2);
        };
        var step = CreateStep(toIntel: false);

        await Assert.ThrowsAsync<GameImportException>(() => step.ExecuteAsync(null, TestContext.Current.CancellationToken));

        Assert.True(File.Exists(_game.Executable));
        Assert.False(step.IsAlreadyDone());
    }

    [Fact]
    public async Task Stale_relinker_outputs_are_removed_before_a_run()
    {
        TestFiles.Write(Path.Combine(_game.Root, "PPSA99666.old.guest.prx.registry.json"), "[]");
        TestFiles.Write(Path.Combine(_game.Root, "PPSA99666.old.needed.json"), "[]");
        TestFiles.Write(Path.Combine(_game.App0, "sce_module", "libold.guest.prx"), "pe");
        TestFiles.Write(Path.Combine(_game.App0, "sce_module", "libold.prx"), "elf");
        _runner.Respond = _ => new FakeProcessResult(0);

        await CreateStep(toIntel: false).ExecuteAsync(null, TestContext.Current.CancellationToken);

        Assert.False(File.Exists(Path.Combine(_game.Root, "PPSA99666.old.guest.prx.registry.json")));
        Assert.False(File.Exists(Path.Combine(_game.Root, "PPSA99666.old.needed.json")));
        Assert.False(File.Exists(Path.Combine(_game.App0, "sce_module", "libold.guest.prx")));
        Assert.True(File.Exists(Path.Combine(_game.App0, "sce_module", "libold.prx")));
    }

    public void Dispose() => _temp.Dispose();
}
