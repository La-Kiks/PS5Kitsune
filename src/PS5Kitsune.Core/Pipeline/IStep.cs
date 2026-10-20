namespace PS5Kitsune.Core.Pipeline;

/// <summary>
/// A long operation with progress, cancellation and resume. See docs/architecture.md, "Resuming steps".
/// </summary>
public interface IStep
{
    /// <summary>Short imperative name shown in logs and in the UI, for example "Download the relinker".</summary>
    string Name { get; }

    /// <summary>
    /// True when what the step produces is already there (a file with the right hash, a copied folder).
    /// Checked just before the step would run, so an earlier step can change the answer.
    /// </summary>
    bool IsAlreadyDone();

    Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken);
}
