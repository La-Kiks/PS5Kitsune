using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Games.Steps;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class CopyStepsTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly GameLayout _game;
    private readonly GameImportContext _context;

    public CopyStepsTests()
    {
        var source = _temp.Combine("source");
        TestGames.Create(source);
        _game = new GameLayout(_temp.Combine("games", TestGames.TitleId), TestGames.TitleId, new FakePlatform());
        _context = new GameImportContext(new GameSource(source, null), new GameMetadata(TestGames.TitleId, "DOOM"), ["sce_module"], _game, toIntel: false);
    }

    [Fact]
    public async Task CopyGame_copies_the_game_into_app0()
    {
        var step = new CopyGameStep(_context);
        Assert.False(step.IsAlreadyDone());

        await step.ExecuteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(TestFiles.Self(), File.ReadAllBytes(Path.Combine(_game.App0, "eboot.bin")));
        Assert.True(File.Exists(Path.Combine(_game.App0, "wads", "doom1.wad")));
        Assert.True(step.IsAlreadyDone());
    }

    [Fact]
    public async Task CopyGame_counts_a_file_backed_up_before_unwrapping_as_copied()
    {
        var step = new CopyGameStep(_context);
        await step.ExecuteAsync(null, TestContext.Current.CancellationToken);

        Directory.CreateDirectory(_game.Originals);
        File.Move(Path.Combine(_game.App0, "eboot.bin"), Path.Combine(_game.Originals, "eboot.bin"));
        TestFiles.Write(Path.Combine(_game.App0, "eboot.bin"), TestFiles.Elf());

        Assert.True(step.IsAlreadyDone());
    }

    [Fact]
    public async Task CopyLibs_copies_the_install_libs_without_the_extraction_marker()
    {
        var paths = new AnyPS5Paths(new DataLayout(_temp.Combine("root")), new FakePlatform());
        TestFiles.Write(Path.Combine(paths.Libs, "libSceAudioOut.prx"), "prx");
        TestFiles.Write(Path.Combine(paths.Libs, ".ps5kitsune-extracted"), "hash");
        var step = new CopyLibsStep(paths, _game);
        Assert.False(step.IsAlreadyDone());

        await step.ExecuteAsync(null, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(_game.Libs, "libSceAudioOut.prx")));
        Assert.False(File.Exists(Path.Combine(_game.Libs, ".ps5kitsune-extracted")));
        Assert.True(step.IsAlreadyDone());
    }

    [Fact]
    public void The_game_layout_follows_the_architecture()
    {
        Assert.Equal(Path.Combine(_game.Root, "PPSA99666.exe"), _game.Executable);
        Assert.Equal(Path.Combine(_game.Root, "PPSA99666.registry.json"), _game.Registry);
        Assert.Equal(Path.Combine(_game.Root, ".ps5kitsune", "game.json"), _game.RecordFile);
        Assert.Equal(Path.Combine(_game.Root, ".ps5kitsune", "originals"), _game.Originals);
        Assert.Equal(Path.Combine(_game.Root, ".ps5kitsune", "logs"), _game.Logs);
    }

    public void Dispose() => _temp.Dispose();
}
