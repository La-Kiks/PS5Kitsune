using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class ReconvertLockTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly GameLayout _game;

    public ReconvertLockTests()
    {
        _game = new GameLayout(_temp.Combine("games", TestGames.TitleId), TestGames.TitleId, new FakePlatform());
        ConvertedGames.Create(_game);
    }

    [Fact]
    public void A_held_lock_cannot_be_acquired_again()
    {
        using var held = ReconvertLock.TryAcquire(_game);

        Assert.NotNull(held);
        Assert.Null(ReconvertLock.TryAcquire(_game));
        Assert.True(ReconvertLock.IsHeld(_game));
    }

    [Fact]
    public void A_released_lock_is_not_held_and_its_file_is_gone()
    {
        ReconvertLock.TryAcquire(_game)!.Dispose();

        Assert.False(ReconvertLock.IsHeld(_game));
        Assert.False(File.Exists(_game.ReconvertLock));
    }

    [Fact]
    public void A_leftover_lock_file_is_not_held()
    {
        TestFiles.Write(_game.ReconvertLock, "");

        Assert.False(ReconvertLock.IsHeld(_game));
    }

    [Fact]
    public void The_lock_is_not_a_conversion_output()
    {
        using var held = ReconvertLock.TryAcquire(_game);

        Assert.DoesNotContain(ConversionBackup.Outputs(_game), path => path.Contains("reconvert.lock", StringComparison.Ordinal));
    }

    public void Dispose() => _temp.Dispose();
}
