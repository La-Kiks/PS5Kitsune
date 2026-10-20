using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.Tests.Pipeline;

public sealed class StepRunnerTests
{
    private readonly StepRunner _runner = new(NullLogger<StepRunner>.Instance);
    private readonly List<StepEvent> _events = [];
    private readonly List<string> _executed = [];

    [Fact]
    public async Task Runs_steps_in_order_and_skips_those_already_done()
    {
        var b = new FakeStep("B", _executed) { Done = true };

        await _runner.RunAsync([new FakeStep("A", _executed), b, new FakeStep("C", _executed)], onEvent: _events.Add, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["A", "C"], _executed);
        Assert.Equal(
            [(StepEventKind.Started, "A"), (StepEventKind.Completed, "A"), (StepEventKind.Skipped, "B"), (StepEventKind.Started, "C"), (StepEventKind.Completed, "C")],
            _events.Select(e => (e.Kind, e.StepName)));
        Assert.All(_events, e => Assert.Equal(3, e.Count));
        Assert.Equal(2, _events.Single(e => e.Kind == StepEventKind.Skipped).Number);
    }

    [Fact]
    public async Task Checks_whether_a_step_is_done_just_before_running_it()
    {
        var b = new FakeStep("B", _executed);
        var a = new FakeStep("A", _executed) { OnExecute = () => b.Done = true };

        await _runner.RunAsync([a, b], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["A"], _executed);
    }

    [Fact]
    public async Task Stops_at_the_first_failure_and_names_the_step()
    {
        var steps = new IStep[]
        {
            new FakeStep("A", _executed),
            new FakeStep("B", _executed) { Failure = new InvalidOperationException("disk full") },
            new FakeStep("C", _executed),
        };

        var exception = await Assert.ThrowsAsync<StepFailedException>(() =>
            _runner.RunAsync(steps, onEvent: _events.Add, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("B", exception.StepName);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains("disk full", exception.Message, StringComparison.Ordinal);
        Assert.Equal(["A", "B"], _executed);
        Assert.Equal((StepEventKind.Failed, "B"), (_events[^1].Kind, _events[^1].StepName));
    }

    [Fact]
    public async Task Cancellation_is_not_wrapped()
    {
        using var cancellation = new CancellationTokenSource();
        var a = new FakeStep("A", _executed) { OnExecute = cancellation.Cancel, Failure = new OperationCanceledException(cancellation.Token) };

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _runner.RunAsync([a, new FakeStep("B", _executed)], cancellationToken: cancellation.Token));

        Assert.Equal(["A"], _executed);
    }

    [Fact]
    public void Download_progress_is_forwarded_synchronously()
    {
        var received = new List<StepProgress>();
        var forwarded = StepProgress.ForDownload(new SyncProgress(received.Add))!;

        forwarded.Report(new(10, 100));

        Assert.Equal(new StepProgress(Download: new(10, 100)), Assert.Single(received));
        Assert.Null(StepProgress.ForDownload(null));
    }

    private sealed class SyncProgress(Action<StepProgress> report) : IProgress<StepProgress>
    {
        public void Report(StepProgress value) => report(value);
    }

    private sealed class FakeStep(string name, List<string> executed) : IStep
    {
        public string Name => name;

        public bool Done { get; set; }

        public Exception? Failure { get; init; }

        public Action? OnExecute { get; init; }

        public bool IsAlreadyDone() => Done;

        public Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)
        {
            executed.Add(Name);
            OnExecute?.Invoke();
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }
}
