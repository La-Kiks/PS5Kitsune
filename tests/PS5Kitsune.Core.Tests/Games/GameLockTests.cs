using System.Diagnostics;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class GameLockTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly GameLayout _game;

    public GameLockTests()
    {
        _game = new GameLayout(_temp.Combine("games", TestGames.TitleId), TestGames.TitleId, new FakePlatform());
        ConvertedGames.Create(_game);
    }

    [Fact]
    public void A_held_lock_cannot_be_acquired_again()
    {
        using var held = GameLock.TryAcquireNow(_game);

        Assert.NotNull(held);
        Assert.Null(GameLock.TryAcquireNow(_game));
        Assert.True(GameLock.IsHeld(_game));
    }

    [Fact]
    public void A_released_lock_is_not_held_and_its_file_is_gone()
    {
        GameLock.TryAcquire(_game)!.Dispose();

        Assert.False(GameLock.IsHeld(_game));
        Assert.False(File.Exists(_game.Lock));
        Assert.EndsWith(Path.Combine(".ps5kitsune", "game.lock"), _game.Lock, StringComparison.Ordinal);
    }

    [Fact]
    public void A_leftover_lock_file_is_not_held()
    {
        TestFiles.Write(_game.Lock, "");

        Assert.False(GameLock.IsHeld(_game));
    }

    [Fact]
    public void The_lock_is_not_a_conversion_output()
    {
        using var held = GameLock.TryAcquire(_game);

        Assert.DoesNotContain(ConversionBackup.Outputs(_game), path => path.Contains("game.lock", StringComparison.Ordinal));
    }

    [Fact]
    public void A_lock_released_within_the_retry_window_is_acquired()
    {
        var held = GameLock.TryAcquireNow(_game)!;
        // A dedicated thread: the thread pool can starve under parallel test load and delay the release.
        var release = new Thread(() =>
        {
            Thread.Sleep(100);
            held.Dispose();
        });
        release.Start();

        using var acquired = GameLock.TryAcquire(_game, attempts: 100, delay: TimeSpan.FromMilliseconds(20));
        release.Join();

        Assert.NotNull(acquired);
    }

    [Fact]
    public void A_lock_held_through_the_retry_window_is_not_acquired()
    {
        using var held = GameLock.TryAcquireNow(_game);
        var stopwatch = Stopwatch.StartNew();

        Assert.Null(GameLock.TryAcquire(_game));
        Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(150), $"Gave up after {stopwatch.Elapsed}.");
    }

    public void Dispose() => _temp.Dispose();
}
