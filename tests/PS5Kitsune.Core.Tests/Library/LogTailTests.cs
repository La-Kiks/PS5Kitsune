using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Tests.Library;

public sealed class LogTailTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public async Task Follows_the_lines_written_until_the_process_exits()
    {
        var path = _temp.Combine("game.log");
        await using var writer = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
        var lines = new List<string>();
        var firstLine = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource();

        var follow = LogTail.FollowAsync(path, line => { lines.Add(line); firstLine.TrySetResult(); }, exited.Task, TestContext.Current.CancellationToken);
        await writer.WriteAsync("one\n");
        await firstLine.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await writer.WriteAsync("two\r\npar");
        await writer.WriteAsync("tial");
        exited.SetResult();
        await follow.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(["one", "two", "partial"], lines);
    }

    [Fact]
    public async Task Cancelling_stops_following()
    {
        var path = _temp.Combine("game.log");
        TestFiles.Write(path, "");
        using var cancellation = new CancellationTokenSource();

        var follow = LogTail.FollowAsync(path, _ => { }, new TaskCompletionSource().Task, cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => follow);
    }

    public void Dispose() => _temp.Dispose();
}
