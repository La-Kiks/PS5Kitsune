namespace PS5Kitsune.Core.Abstractions;

/// <param name="Executable">Program to start.</param>
/// <param name="WorkingDirectory">The game folder: the game resolves its files and creates data\ there.</param>
/// <param name="LogDirectory">Folder of the launch logs; only the newest are kept.</param>
public sealed record GameLaunchRequest(string Executable, string WorkingDirectory, string LogDirectory)
{
    public IReadOnlyList<string> Arguments { get; init; } = [];
}

/// <summary>A started game. Disposing releases the launcher's handle; it never stops the game.</summary>
public interface ILaunchedGame : IDisposable
{
    int ProcessId { get; }

    /// <summary>File receiving the game's stdout and stderr.</summary>
    string LogFile { get; }

    /// <summary>Completes with the exit code when the game exits.</summary>
    Task<int> WaitForExitAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Starts a game detached from the launcher, its output written straight to a log file.
/// See docs/architecture.md, "Launching".
/// </summary>
public interface IGameLauncher
{
    ILaunchedGame Launch(GameLaunchRequest request);
}
