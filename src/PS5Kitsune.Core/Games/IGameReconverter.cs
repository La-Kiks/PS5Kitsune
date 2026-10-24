using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.Games;

/// <summary>Converts an added game again with the AnyPS5 in use, keeping what the game created. See the 6b spec.</summary>
public interface IGameReconverter
{
    /// <summary>The Reconvert steps' names, in order, to list before it starts.</summary>
    IReadOnlyList<string> StepNames { get; }

    /// <summary>Sets the conversion aside, rebuilds it and rewrites game.json; a failure or a cancel puts the previous conversion back.
    /// The game's <see cref="GameLock"/> is held for the whole run.
    /// If writing game.json fails after the old conversion was discarded, the game keeps the new conversion and its old record, and shows as needing a Reconvert.</summary>
    /// <exception cref="AnyPS5NotInstalledException">AnyPS5 is not installed.</exception>
    /// <exception cref="Library.GameNotFoundException">No game with this title ID.</exception>
    /// <exception cref="Library.InvalidGameRecordException">The game.json is malformed.</exception>
    /// <exception cref="StepFailedException">A step failed; the previous conversion is back (except a failed game.json write after the discard; see above).</exception>
    /// <exception cref="OperationCanceledException">Cancelled; the previous conversion is back.</exception>
    /// <exception cref="GameBusyException">Another action holds the game's lock; nothing was changed.</exception>
    /// <exception cref="ReconvertRestoreException">A step failed or the run was cancelled, and the restore failed; or restoring an earlier
    /// Reconvert that did not finish failed before this one started (then the inner exception is the restore failure).</exception>
    Task<GameReconverted> ReconvertAsync(string titleId, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default);
}
