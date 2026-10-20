namespace PS5Kitsune.Core.Pipeline;

public enum StepEventKind
{
    Started,
    Skipped,
    Completed,
    Failed,
}

/// <param name="Number">Position of the step, from 1.</param>
/// <param name="Count">Number of steps in the run.</param>
/// <param name="Duration">Set for Completed and Failed.</param>
/// <param name="Error">Set for Failed.</param>
public sealed record StepEvent(StepEventKind Kind, string StepName, int Number, int Count, TimeSpan? Duration = null, Exception? Error = null);
