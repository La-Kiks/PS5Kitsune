using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Core.Tests.IO;

public sealed class JsonFilesTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    private sealed record Sample(int Value);

    [Fact]
    public async Task A_destination_held_open_for_a_moment_is_replaced_once_released()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Only Windows refuses to replace an open file.");
        }

        var path = _temp.Combine("game.json");
        await File.WriteAllTextAsync(path, "{}", TestContext.Current.CancellationToken);
        var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        // A dedicated thread, not the pool: the retry delays must not starve the release.
        var release = new Thread(() =>
        {
            Thread.Sleep(100);
            held.Dispose();
        });
        release.Start();

        await JsonFiles.WriteAsync(path, new Sample(7), attempts: 100, delay: TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken);
        release.Join();

        Assert.Equal(7, JsonFiles.Read<Sample>(path)!.Value);
    }

    [Fact]
    public async Task A_destination_held_past_the_retries_throws()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Only Windows refuses to replace an open file.");
        }

        var path = _temp.Combine("game.json");
        await File.WriteAllTextAsync(path, "{}", TestContext.Current.CancellationToken);
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => JsonFiles.WriteAsync(path, new Sample(7), attempts: 2, delay: TimeSpan.FromMilliseconds(10), TestContext.Current.CancellationToken));
    }

    public void Dispose() => _temp.Dispose();
}
