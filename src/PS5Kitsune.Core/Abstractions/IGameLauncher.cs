namespace PS5Kitsune.Core.Abstractions;

/// <param name="Executable">Program to start.</param>
/// <param name="WorkingDirectory">The game folder: the game resolves its files and creates data\ there.</param>
/// <param name="LogDirectory">Folder of the launch logs; only the newest are kept.</param>
public sealed record GameLaunchRequest(string Executable, string WorkingDirectory, string LogDirectory)
{
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>Variables added to the inherited environment, for the game process only.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();
}

/// <summary>A launched game, as recorded in .ps5kitsune\running.json. See docs/architecture.md, "Launching".</summary>
/// <param name="StartedAt">The process creation time read from the OS, in UTC: with the PID, it tells the game from a later process reusing the PID.</param>
/// <param name="Executable">The full path of the game's executable.</param>
public sealed record RunningGame(int SchemaVersion, int ProcessId, DateTimeOffset StartedAt, string Executable, string LogFile)
{
    public const int CurrentSchemaVersion = 2;

    /// <summary>Linux: /proc/sys/kernel/random/boot_id when the game started; null on Windows and in schema 1.</summary>
    public string? BootId { get; init; }

    /// <summary>Linux: the game's start time in clock ticks since boot (/proc/&lt;pid&gt;/stat field 22); null on Windows and in schema 1.</summary>
    public ulong? StartTicks { get; init; }
}

/// <summary>A started game. Disposing releases the launcher's handle; it never stops the game.</summary>
public interface ILaunchedGame : IDisposable
{
    int ProcessId { get; }

    /// <summary>File receiving the game's stdout and stderr.</summary>
    string LogFile { get; }

    /// <summary>The process creation time read from the OS, in UTC.</summary>
    DateTimeOffset StartedAt { get; }

    /// <summary>Linux: the boot the game started in; null elsewhere or when unreadable. Recorded with <see cref="StartTicks"/> so a clock change cannot hide the game.</summary>
    string? BootId => null;

    /// <summary>Linux: the game's start in clock ticks since boot; null elsewhere or when unreadable.</summary>
    ulong? StartTicks => null;

    /// <summary>Completes with the exit code when the game exits; null when the code cannot be read (a game found again on Linux).</summary>
    Task<int?> WaitForExitAsync(CancellationToken cancellationToken = default);

    /// <summary>Asks the game's windows to close, waits a few seconds, then ends the process. Completes once it has exited.</summary>
    /// <exception cref="System.ComponentModel.Win32Exception">The process could not be ended.</exception>
    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Starts a game detached from the launcher, its output written straight to a log file.
/// See docs/architecture.md, "Launching".
/// </summary>
public interface IGameLauncher
{
    ILaunchedGame Launch(GameLaunchRequest request);

    /// <summary>Opens the recorded game's process again, for example after PS5Kitsune was closed and reopened.</summary>
    /// <returns>Null when the process exited, cannot be opened, or its PID now belongs to another process (start time or executable differ).</returns>
    ILaunchedGame? Attach(RunningGame record);
}
