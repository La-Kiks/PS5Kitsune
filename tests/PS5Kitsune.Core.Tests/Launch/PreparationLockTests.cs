using PS5Kitsune.Core.Launch;

namespace PS5Kitsune.Core.Tests.Launch;

public sealed class PreparationLockTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // Two instances on the same root stand for two processes: the lock is the file, not the object.
    private PreparationLock Create() => new(new DataLayout(_temp.Path)) { RetryDelay = TimeSpan.FromMilliseconds(20) };

    [Fact]
    public async Task A_second_holder_waits_until_the_first_releases_and_says_so_once()
    {
        var first = await Create().AcquireAsync(null, Token);
        var waited = 0;

        var second = Create().AcquireAsync(() => waited++, Token);
        await Task.Delay(200, Token);

        Assert.False(second.IsCompleted);
        first.Dispose();
        using var held = await second.WaitAsync(TimeSpan.FromSeconds(10), Token);
        Assert.Equal(1, waited);
        Assert.True(File.Exists(_temp.Combine("tools", PreparationLock.FileName)));
    }

    [Fact]
    public async Task Waiting_can_be_cancelled()
    {
        using var first = await Create().AcquireAsync(null, Token);
        using var cancellation = new CancellationTokenSource();

        var second = Create().AcquireAsync(null, cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
    }

    public void Dispose() => _temp.Dispose();
}
