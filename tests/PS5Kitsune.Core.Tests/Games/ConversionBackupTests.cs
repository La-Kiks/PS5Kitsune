using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Games.Steps;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class ConversionBackupTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly GameLayout _game;

    public ConversionBackupTests()
    {
        _game = new GameLayout(_temp.Combine("games", TestGames.TitleId), TestGames.TitleId, new FakePlatform());
        ConvertedGames.Create(_game);
    }

    private void WriteNewOutputs()
    {
        TestFiles.Write(_game.Executable, "new exe");
        TestFiles.Write(_game.Registry, "new registry");
        TestFiles.Write(Path.Combine(_game.Libs, "libSceAudioOut.prx"), "new prx");
        TestFiles.Write(Path.Combine(_game.Libs, "libOnlyInNewVersion.prx"), "new only");
        TestFiles.Write(Path.Combine(_game.App0, "sce_module", "libc.guest.prx"), "new pe");
    }

    [Fact]
    public void Lists_every_conversion_output()
    {
        var outputs = ConversionBackup.Outputs(_game).Select(path => path.Replace('\\', '/')).ToList();

        Assert.Equal(
            ["PPSA99666.exe", ".ps5kitsune/relinked", "audit.json", "PPSA99666.libc.registry.json", "PPSA99666.registry.json", "PPSA99666.needed.json", "libs", "app0/sce_module/libc.guest.prx"],
            outputs);
    }

    [Fact]
    public void Setting_aside_moves_the_outputs_and_nothing_else()
    {
        var before = ConvertedGames.Snapshot(_game.Root);

        ConversionBackup.SetAside(_game);

        Assert.True(Directory.Exists(_game.Previous));
        Assert.False(Directory.Exists(_game.PreviousPartial));
        Assert.Empty(ConversionBackup.Outputs(_game));
        var after = ConvertedGames.Snapshot(_game.Root);
        foreach (var relative in ConvertedGames.Untouched)
        {
            Assert.Equal(before[relative], after[relative]);
        }

        Assert.Equal(before["PPSA99666.exe"], after[".ps5kitsune/previous/PPSA99666.exe"]);
        Assert.Equal(before["libs/libOnlyInOldVersion.prx"], after[".ps5kitsune/previous/libs/libOnlyInOldVersion.prx"]);
        Assert.Equal(before["app0/sce_module/libc.guest.prx"], after[".ps5kitsune/previous/app0/sce_module/libc.guest.prx"]);
    }

    [Fact]
    public void Restoring_after_new_outputs_puts_the_old_conversion_back_exactly()
    {
        var before = ConvertedGames.Snapshot(_game.Root);
        ConversionBackup.SetAside(_game);
        WriteNewOutputs();

        Assert.True(ConversionBackup.Restore(_game));

        Assert.Equal(before, ConvertedGames.Snapshot(_game.Root));
        Assert.False(Directory.Exists(_game.Previous));
    }

    [Fact]
    public void An_interrupted_set_aside_is_moved_back()
    {
        var before = ConvertedGames.Snapshot(_game.Root);
        // As if the process died after moving two outputs: they sit in previous.partial, the rest is in place.
        TestFiles.Write(Path.Combine(_game.PreviousPartial, "PPSA99666.exe"), "old exe");
        File.Delete(_game.Executable);
        Directory.Move(_game.Libs, Path.Combine(_game.PreviousPartial, "libs"));

        Assert.True(ConversionBackup.Restore(_game));

        Assert.Equal(before, ConvertedGames.Snapshot(_game.Root));
        Assert.False(Directory.Exists(_game.PreviousPartial));
    }

    [Fact]
    public void Nothing_to_restore_changes_nothing()
    {
        var before = ConvertedGames.Snapshot(_game.Root);

        Assert.False(ConversionBackup.Restore(_game));

        Assert.Equal(before, ConvertedGames.Snapshot(_game.Root));
    }

    [Fact]
    public void Discarding_deletes_the_set_aside_conversion()
    {
        ConversionBackup.SetAside(_game);
        WriteNewOutputs();
        var withNew = ConvertedGames.Snapshot(_game.Root).Where(entry => !entry.Key.StartsWith(".ps5kitsune/previous/", StringComparison.Ordinal)).ToList();

        ConversionBackup.Discard(_game);

        Assert.False(Directory.Exists(_game.Previous));
        Assert.False(Directory.Exists(_game.PreviousDiscarded));
        Assert.Equal(withNew, ConvertedGames.Snapshot(_game.Root).ToList());
        Assert.False(ConversionBackup.Restore(_game));
    }

    [Fact]
    public void A_leftover_discarded_folder_is_deleted_by_the_next_restore()
    {
        TestFiles.Write(Path.Combine(_game.PreviousDiscarded, "PPSA99666.exe"), "older exe");

        ConversionBackup.Restore(_game);

        Assert.False(Directory.Exists(_game.PreviousDiscarded));
        Assert.Equal("old exe", File.ReadAllText(_game.Executable));
    }

    [Fact]
    public async Task The_step_sets_the_outputs_aside_and_is_never_done()
    {
        var step = new SetAsideStep(_game);
        Assert.Equal("Set the old conversion aside", step.Name);
        Assert.False(step.IsAlreadyDone());

        await step.ExecuteAsync(null, TestContext.Current.CancellationToken);

        Assert.True(Directory.Exists(_game.Previous));
        Assert.False(step.IsAlreadyDone());
    }

    [Fact]
    public async Task A_running_game_stops_the_step_before_anything_moves()
    {
        var before = ConvertedGames.Snapshot(_game.Root);
        var step = new SetAsideStep(_game);

        GameImportException failure;
        using (new FileStream(_game.Executable, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            failure = await Assert.ThrowsAsync<GameImportException>(() => step.ExecuteAsync(null, TestContext.Current.CancellationToken));
        }

        Assert.StartsWith("Cannot set the old conversion aside: ", failure.Summary, StringComparison.Ordinal);
        Assert.Equal("Close the game if it is running.", failure.Details);
        Assert.Equal(before, ConvertedGames.Snapshot(_game.Root));
        Assert.False(Directory.Exists(_game.PreviousPartial));
    }

    public void Dispose() => _temp.Dispose();
}
