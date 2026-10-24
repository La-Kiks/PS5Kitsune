using Microsoft.Extensions.Logging;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.App.Tests;

public sealed class StepRunViewModelTests
{
    [Fact]
    public async Task A_failing_success_hook_is_logged_and_shown()
    {
        var logger = new ListLogger();
        var viewModel = new HookFailingRun(logger);

        await viewModel.RunAsync();

        Assert.True(viewModel.HasFailed);
        Assert.Equal("Something went wrong after the steps: boom", viewModel.Failure);
        Assert.False(viewModel.CanRetry);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    private sealed class HookFailingRun(ILogger logger) : StepRunViewModel("app.log", new TestPlatform(), new InlineDispatcher(), null, logger)
    {
        public override string? QuitMessage => null;

        protected override bool IndeterminateOnStart => false;

        protected override IReadOnlyList<string> StepNames() => ["Only step"];

        protected override Task RunCoreAsync(IProgress<StepProgress> progress, Action<StepEvent> onEvent, CancellationToken cancellationToken) => Task.CompletedTask;

        protected override (string Summary, string? Details) SplitFailure(Exception exception) => (exception.Message, null);

        protected override Task OnSucceededAsync() => throw new InvalidOperationException("boom");

        protected override Task OnCancelledAsync() => Task.CompletedTask;

        protected override Task LeaveAsync() => Task.CompletedTask;
    }
}
