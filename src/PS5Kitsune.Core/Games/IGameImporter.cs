using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.Games;

/// <summary>Adds a game in two stages, so a screen can show the summary before anything is copied.</summary>
public interface IGameImporter
{
    /// <summary>The add steps' names, in order, to list before the add starts.</summary>
    IReadOnlyList<string> StepNames { get; }

    /// <summary>Resolves the folder or zip and validates it, on the thread pool; a zip is extracted into staging.</summary>
    /// <exception cref="InvalidGameSourceException">Not a folder or zip, no eboot.bin, or an unreadable zip; no staging is left.</exception>
    /// <exception cref="OperationCanceledException">Cancelled; no staging is left.</exception>
    Task<PreparedGame> PrepareAsync(string input, CancellationToken cancellationToken);

    /// <summary>Runs the add steps; an add that stopped resumes. Does not dispose <paramref name="game"/>.</summary>
    /// <exception cref="AnyPS5NotInstalledException">AnyPS5 is not installed.</exception>
    /// <exception cref="InvalidOperationException">The game is blocked or already added.</exception>
    /// <exception cref="ObjectDisposedException"><paramref name="game"/> was disposed (its staging folder may be gone).</exception>
    /// <exception cref="StepFailedException">A step failed.</exception>
    Task<GameAdded> AddAsync(PreparedGame game, bool toIntel, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default);
}
