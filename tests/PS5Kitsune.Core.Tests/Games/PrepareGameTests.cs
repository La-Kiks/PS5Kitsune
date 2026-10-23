using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Tests.AnyPS5;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class PrepareGameTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly AnyPS5TestSetup _setup;
    private readonly GameImporter _importer;

    public PrepareGameTests()
    {
        _setup = new AnyPS5TestSetup(_temp.Combine("root"));
        _importer = new GameImporter(
            _setup.Layout, _setup.Platform, _setup.Profile, _setup.Paths, _setup.Installer,
            new GameSourceResolver(_setup.Layout), new GameValidator(_ => long.MaxValue),
            _setup.PythonTool, _setup.Runner, _setup.StepRunner, _setup.Time, NullLogger<GameImporter>.Instance);
    }

    private string Staging => Path.Combine(_setup.Layout.Staging, TestGames.TitleId);

    private Task<PreparedGame> PrepareAsync(string input) => _importer.PrepareAsync(input, TestContext.Current.CancellationToken);

    private string WriteZip(byte[] content)
    {
        var zip = _temp.Combine("PPSA99666.zip");
        File.WriteAllBytes(zip, content);
        return zip;
    }

    [Fact]
    public async Task A_folder_is_described_without_copying_anything()
    {
        var folder = _temp.Combine("doom");
        TestGames.Create(folder);

        await using var game = await PrepareAsync(folder);

        Assert.Equal(TestGames.TitleId, game.Metadata?.TitleId);
        Assert.Equal("DOOM", game.Metadata?.Name);
        Assert.Equal(Path.Combine(folder, "sce_sys", "icon0.png"), game.IconPath);
        Assert.Equal(["sce_module"], game.Validation.ModuleDirectories);
        Assert.True(game.Validation.SizeBytes > 0);
        Assert.Equal([new GameBinary("eboot.bin", BinaryKind.Self), new GameBinary("sce_module/libc.prx", BinaryKind.Self)], game.Binaries);
        Assert.False(game.IsBlocked);
        Assert.False(game.AlreadyAdded);
        Assert.False(game.Resumes);
        Assert.Equal(_setup.Layout.GameDirectory(TestGames.TitleId), game.GameDirectory);
        Assert.Null(game.Source.StagingDirectory);
        Assert.False(Directory.Exists(_setup.Layout.Games));
    }

    [Fact]
    public async Task A_zip_is_extracted_to_staging_and_disposing_deletes_it()
    {
        var game = await PrepareAsync(WriteZip(TestGames.Zip("PPSA99666/")));

        Assert.Equal(Staging, game.Source.StagingDirectory);
        Assert.True(Directory.Exists(Staging));
        Assert.Equal("DOOM", game.Metadata?.Name);

        await game.DisposeAsync();
        await game.DisposeAsync();

        Assert.True(game.IsDisposed);
        Assert.False(Directory.Exists(Staging));
        Assert.False(Directory.Exists(Staging + ".staging"));
    }

    [Fact]
    public async Task A_second_dispose_waits_for_the_deletion_already_running()
    {
        var game = await PrepareAsync(WriteZip(TestGames.Zip("PPSA99666/")));
        for (var index = 0; index < 3000; index++)
        {
            TestFiles.Write(Path.Combine(Staging, "bulk", $"{index % 30}", $"file{index}.bin"), "x");
        }

        var first = game.DisposeAsync();
        var second = game.DisposeAsync();
        await second;

        Assert.True(game.IsDisposed);
        Assert.False(Directory.Exists(Staging));
        await first;
    }

    [Fact]
    public async Task An_added_game_and_a_stopped_add_are_recognized()
    {
        var folder = _temp.Combine("doom");
        TestGames.Create(folder);
        var gameDirectory = _setup.Layout.GameDirectory(TestGames.TitleId);
        Directory.CreateDirectory(Path.Combine(gameDirectory, "app0"));

        await using (var stopped = await PrepareAsync(folder))
        {
            Assert.True(stopped.Resumes);
            Assert.False(stopped.AlreadyAdded);
        }

        TestFiles.Write(Path.Combine(gameDirectory, ".ps5kitsune", "game.json"), "{}");
        await using var added = await PrepareAsync(folder);

        Assert.True(added.AlreadyAdded);
        Assert.False(added.Resumes);
    }

    [Fact]
    public async Task A_path_that_is_neither_is_refused()
    {
        var exception = await Assert.ThrowsAsync<InvalidGameSourceException>(() => PrepareAsync(_temp.Combine("nothing.txt")));

        Assert.Contains("is neither a folder nor a .zip file", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_folder_without_eboot_is_refused()
    {
        Directory.CreateDirectory(_temp.Combine("empty"));

        var exception = await Assert.ThrowsAsync<InvalidGameSourceException>(() => PrepareAsync(_temp.Combine("empty")));

        Assert.StartsWith("No eboot.bin found in", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_zip_without_eboot_leaves_no_staging()
    {
        var zip = WriteZip(TestFiles.ZipBytes(("PPSA99666/readme.txt", "no game"u8.ToArray())));

        await Assert.ThrowsAsync<InvalidGameSourceException>(() => PrepareAsync(zip));

        Assert.False(Directory.Exists(Staging));
        Assert.False(Directory.Exists(Staging + ".staging"));
    }

    [Fact]
    public async Task An_unreadable_zip_is_refused_and_leaves_no_staging()
    {
        var zip = WriteZip("not a zip"u8.ToArray());

        var exception = await Assert.ThrowsAsync<InvalidGameSourceException>(() => PrepareAsync(zip));

        Assert.StartsWith($"Cannot read {zip}: ", exception.Message, StringComparison.Ordinal);
        Assert.IsType<InvalidDataException>(exception.InnerException);
        Assert.False(Directory.Exists(Staging));
        Assert.False(Directory.Exists(Staging + ".staging"));
    }

    [Fact]
    public async Task A_cancelled_preparation_leaves_no_staging()
    {
        var zip = WriteZip(TestGames.Zip("PPSA99666/"));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _importer.PrepareAsync(zip, cancellation.Token));

        Assert.False(Directory.Exists(Staging));
        Assert.False(Directory.Exists(Staging + ".staging"));
    }

    [Fact]
    public void Step_names_are_the_six_add_steps() =>
        Assert.Equal(
            ["Copy the game", "Copy the AnyPS5 libraries", "Unwrap SELF files", "Convert with the relinker", "Audit the imports", "Record the game"],
            ((IGameImporter)_importer).StepNames);

    public void Dispose() => _temp.Dispose();
}
