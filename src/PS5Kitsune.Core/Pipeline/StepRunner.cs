using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace PS5Kitsune.Core.Pipeline;

/// <summary>
/// Runs steps in order, skips those already done, and stops at the first failure.
/// Logs every step at Information level (the CLI console shows them); <c>onEvent</c> is for the UI.
/// </summary>
public sealed partial class StepRunner(ILogger<StepRunner> logger)
{
    /// <exception cref="StepFailedException">A step failed; the exception names it and wraps the cause.</exception>
    /// <exception cref="OperationCanceledException">The run was cancelled; it can be resumed.</exception>
    public async Task RunAsync(IReadOnlyList<IStep> steps, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(steps);
        for (var index = 0; index < steps.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = steps[index];
            var number = index + 1;
            if (step.IsAlreadyDone())
            {
                LogSkipped(number, steps.Count, step.Name);
                onEvent?.Invoke(new StepEvent(StepEventKind.Skipped, step.Name, number, steps.Count));
                continue;
            }

            LogStarted(number, steps.Count, step.Name);
            onEvent?.Invoke(new StepEvent(StepEventKind.Started, step.Name, number, steps.Count));
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await step.ExecuteAsync(progress, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                LogFailed(exception, number, steps.Count, step.Name, stopwatch.Elapsed.TotalSeconds);
                onEvent?.Invoke(new StepEvent(StepEventKind.Failed, step.Name, number, steps.Count, stopwatch.Elapsed, exception));
                throw new StepFailedException(step.Name, exception);
            }

            LogCompleted(number, steps.Count, step.Name, stopwatch.Elapsed.TotalSeconds);
            onEvent?.Invoke(new StepEvent(StepEventKind.Completed, step.Name, number, steps.Count, stopwatch.Elapsed));
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "[{Number}/{Count}] {Step}")]
    private partial void LogStarted(int number, int count, string step);

    [LoggerMessage(Level = LogLevel.Information, Message = "[{Number}/{Count}] {Step}: already done")]
    private partial void LogSkipped(int number, int count, string step);

    [LoggerMessage(Level = LogLevel.Information, Message = "[{Number}/{Count}] {Step}: done in {Seconds:0.0} s")]
    private partial void LogCompleted(int number, int count, string step, double seconds);

    [LoggerMessage(Level = LogLevel.Error, Message = "[{Number}/{Count}] {Step}: failed after {Seconds:0.0} s")]
    private partial void LogFailed(Exception exception, int number, int count, string step, double seconds);
}
