using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class GameSourceResolverTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly DataLayout _layout;
    private readonly GameSourceResolver _resolver;

    public GameSourceResolverTests()
    {
        _layout = new DataLayout(_temp.Combine("root"));
        _resolver = new GameSourceResolver(_layout);
    }

    private Task<GameSource> ResolveAsync(string input) => _resolver.ResolveAsync(input, TestContext.Current.CancellationToken);

    [Fact]
    public async Task A_folder_holding_eboot_bin_is_the_game_root()
    {
        TestGames.Create(_temp.Combine("doom"));

        var source = await ResolveAsync(_temp.Combine("doom"));

        Assert.Equal(_temp.Combine("doom"), source.Root);
        Assert.Null(source.StagingDirectory);
    }

    [Fact]
    public async Task An_app0_subfolder_is_the_game_root()
    {
        TestGames.Create(_temp.Combine("dump", "app0"));

        var source = await ResolveAsync(_temp.Combine("dump"));

        Assert.Equal(_temp.Combine("dump", "app0"), source.Root);
    }

    [Fact]
    public async Task A_single_subfolder_is_the_game_root()
    {
        TestGames.Create(_temp.Combine("download", "PPSA99666"));

        var source = await ResolveAsync(_temp.Combine("download"));

        Assert.Equal(_temp.Combine("download", "PPSA99666"), source.Root);
    }

    [Fact]
    public async Task A_zip_with_a_top_folder_is_extracted_to_staging()
    {
        var zip = _temp.Combine("PPSA99666.zip");
        File.WriteAllBytes(zip, TestGames.Zip("PPSA99666/"));

        var source = await ResolveAsync(zip);

        var staging = Path.Combine(_layout.Staging, "PPSA99666");
        Assert.Equal(staging, source.StagingDirectory);
        Assert.Equal(Path.Combine(staging, "PPSA99666"), source.Root);
        Assert.True(File.Exists(Path.Combine(source.Root, "eboot.bin")));
    }

    [Fact]
    public async Task A_flat_zip_is_its_own_game_root()
    {
        var zip = _temp.Combine("doom.zip");
        File.WriteAllBytes(zip, TestGames.Zip(""));

        var source = await ResolveAsync(zip);

        Assert.Equal(source.StagingDirectory, source.Root);
        Assert.Equal(5, Directory.GetFiles(source.Root, "*", SearchOption.AllDirectories).Length);
    }

    [Fact]
    public async Task A_folder_without_eboot_bin_is_rejected()
    {
        TestFiles.Write(_temp.Combine("empty", "readme.txt"), "nothing here");
        TestFiles.Write(_temp.Combine("empty", "other.txt"), "nothing here");

        var exception = await Assert.ThrowsAsync<InvalidGameSourceException>(() => ResolveAsync(_temp.Combine("empty")));

        Assert.Contains("eboot.bin", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_path_is_rejected()
    {
        await Assert.ThrowsAsync<InvalidGameSourceException>(() => ResolveAsync(_temp.Combine("missing")));
    }

    public void Dispose() => _temp.Dispose();
}
