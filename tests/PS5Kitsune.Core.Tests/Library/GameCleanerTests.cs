using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tests.Games;

namespace PS5Kitsune.Core.Tests.Library;

public sealed class GameCleanerTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly DataLayout _layout;
    private readonly FakeGameLauncher _launcher = new();
    private readonly GameLibrary _library;
    private readonly RunningGames _running;
    private readonly GameCleaner _cleaner;
    private readonly GameLayout _game;

    public GameCleanerTests()
    {
        _layout = new DataLayout(_temp.Path);
        _library = new GameLibrary(_layout, new FakePlatform(), TimeProvider.System, NullLogger<GameLibrary>.Instance);
        _running = new RunningGames(_launcher, NullLogger<RunningGames>.Instance);
        _cleaner = new GameCleaner(_library, _running, NullLogger<GameCleaner>.Instance);
        _game = new GameLayout(_layout.GameDirectory(TestGames.TitleId), TestGames.TitleId, new FakePlatform());
    }

    private async Task AddDoomAsync()
    {
        ConvertedGames.Create(_game);
        await GameRecordStore.WriteAsync(_game.RecordFile, TestRecords.Sample(), TestContext.Current.CancellationToken);
    }

    private Task StartDoomAsync() =>
        _running.RecordAsync(_game, _launcher.Launch(new GameLaunchRequest(_game.Executable, _game.Root, _game.Logs)), _game.Executable);

    [Fact]
    public async Task The_shader_cache_size_counts_every_file()
    {
        await AddDoomAsync();
        Assert.Equal("kept shader_cache/v1/cache.bin".Length, GameCleaner.ShaderCacheSize(_game));
        TestFiles.Write(Path.Combine(_game.ShaderCache, "v2", "deep", "more.bin"), "12345");

        Assert.Equal("kept shader_cache/v1/cache.bin".Length + 5, GameCleaner.ShaderCacheSize(_game));
    }

    [Fact]
    public void No_shader_cache_has_size_zero()
    {
        Assert.Equal(0, GameCleaner.ShaderCacheSize(_game));
    }

    [Fact]
    public async Task Clearing_the_shader_cache_keeps_everything_else()
    {
        await AddDoomAsync();
        var before = ConvertedGames.Snapshot(_game.Root);

        _cleaner.ClearShaderCache(TestGames.TitleId);

        Assert.False(Directory.Exists(_game.ShaderCache));
        before.Remove("shader_cache/v1/cache.bin");
        Assert.Equal(before, ConvertedGames.Snapshot(_game.Root));
    }

    [Fact]
    public async Task Saves_are_detected()
    {
        await AddDoomAsync();
        Assert.True(GameCleaner.HasSaves(_game));

        Directory.Delete(_game.Data, recursive: true);
        Directory.CreateDirectory(_game.Data);

        Assert.False(GameCleaner.HasSaves(_game));
    }

    [Fact]
    public async Task Removing_deletes_the_game_folder()
    {
        await AddDoomAsync();

        _cleaner.Remove(TestGames.TitleId);

        Assert.False(Directory.Exists(_game.Root));
        Assert.Empty(_library.All());
    }

    [Fact]
    public async Task A_running_game_is_neither_removed_nor_cleared()
    {
        await AddDoomAsync();
        await StartDoomAsync();
        var before = ConvertedGames.Snapshot(_game.Root);

        Assert.Equal("DOOM", Assert.Throws<GameRunningException>(() => _cleaner.Remove(TestGames.TitleId)).Name);
        Assert.Throws<GameRunningException>(() => _cleaner.ClearShaderCache(TestGames.TitleId));

        Assert.Equal(before, ConvertedGames.Snapshot(_game.Root));
    }

    [Fact]
    public async Task A_busy_game_is_neither_removed_nor_cleared()
    {
        await AddDoomAsync();

        using (GameLock.TryAcquireNow(_game))
        {
            Assert.Throws<GameBusyException>(() => _cleaner.Remove(TestGames.TitleId));
            Assert.Throws<GameBusyException>(() => _cleaner.ClearShaderCache(TestGames.TitleId));
        }

        Assert.True(File.Exists(_game.RecordFile));
        Assert.True(Directory.Exists(_game.ShaderCache));
    }

    [Fact]
    public async Task A_removal_stopped_by_a_file_in_use_keeps_the_game_listed_and_can_be_retried()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Only Windows refuses to delete an open file.");
        }

        await AddDoomAsync();

        using (new FileStream(Path.Combine(_game.App0, "eboot.bin"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.ThrowsAny<IOException>(() => _cleaner.Remove(TestGames.TitleId));
            Assert.Equal("DOOM", Assert.Single(_library.All()).Record.Name);
        }

        _cleaner.Remove(TestGames.TitleId);

        Assert.False(Directory.Exists(_game.Root));
    }

    [Fact]
    public async Task A_game_running_unrecorded_fails_the_removal_before_the_saves_are_touched()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Only Windows refuses to delete an open file.");
        }

        await AddDoomAsync();
        TestFiles.Write(Path.Combine(_game.Data, "save.dat"), "save");

        // An open image stands for a game started outside PS5Kitsune.
        using (new FileStream(_game.Executable, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.ThrowsAny<IOException>(() => _cleaner.Remove(TestGames.TitleId));
        }

        Assert.True(File.Exists(Path.Combine(_game.Data, "save.dat")));
        Assert.True(File.Exists(_game.RecordFile));
    }

    public void Dispose() => _temp.Dispose();
}
