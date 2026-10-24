namespace PS5Kitsune.Core.Games;

/// <summary>
/// A Reconvert failed or was cancelled, and putting the previous conversion back failed too. .ps5kitsune/previous is kept,
/// so the next Reconvert or the next read of the game restores it.
/// </summary>
/// <param name="failure">Why the Reconvert stopped (a <c>StepFailedException</c> or an <see cref="OperationCanceledException"/>); the inner exception.</param>
/// <param name="restoreFailure">Why the restore failed.</param>
public sealed class ReconvertRestoreException(Exception failure, Exception restoreFailure) : Exception(failure?.Message, failure)
{
    public Exception RestoreFailure { get; } = restoreFailure;
}
