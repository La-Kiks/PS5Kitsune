using System.Collections;
using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Platform.Linux;

/// <summary>
/// Starts the game with posix_spawn in a session of its own, its stdout and stderr opened straight on the log file:
/// no pipe slows a verbose game down, and closing the terminal or the launcher does not end it. See the 8a spec, "Run".
/// </summary>
[SupportedOSPlatform("linux")]
public sealed partial class LinuxGameLauncher(ILogger<LinuxGameLauncher> logger) : IGameLauncher
{
    private const int KeptLogs = 10;

    /// <summary>How long Stop waits after SIGTERM before SIGKILL.</summary>
    internal TimeSpan StopGrace { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>How often an attached game, which is not our child, is checked for its exit.</summary>
    internal TimeSpan AttachPoll { get; init; } = TimeSpan.FromMilliseconds(500);

    public ILaunchedGame Launch(GameLaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Directory.CreateDirectory(request.LogDirectory);
        DeleteOldLogs(request.LogDirectory);
        var logFile = Path.Combine(request.LogDirectory, string.Create(CultureInfo.InvariantCulture, $"{DateTime.Now:yyyyMMdd-HHmmss}.log"));
        var pid = PosixSpawn.Start(request.Executable, request.Arguments, request.WorkingDirectory, logFile, EnvironmentOf(request.Environment));

        // The child cannot be reaped before the waiting thread starts, so its stat is readable (at worst as a zombie).
        var game = new SpawnedGame(pid, logFile, ProcStat.StartTimeOf(pid) ?? DateTimeOffset.UtcNow, StopGrace);
        var executable = request.Executable;
        LogLaunched(executable, pid, logFile);
        return game;
    }

    public ILaunchedGame? Attach(RunningGame record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!IsRecorded(record))
        {
            return null;
        }

        var executable = record.Executable;
        var pid = record.ProcessId;
        LogAttached(executable, pid);
        return new AttachedGame(record, StopGrace, AttachPoll);
    }

    /// <summary>The recorded process still runs and is the recorded game: same start time, same executable.</summary>
    internal static bool IsRecorded(RunningGame record)
    {
        try
        {
            return ProcStat.Read(record.ProcessId) is { State: not 'Z' }
                && ProcStat.StartTimeOf(record.ProcessId) == record.StartedAt
                && new FileInfo($"/proc/{record.ProcessId.ToString(CultureInfo.InvariantCulture)}/exe").LinkTarget is { } target
                && string.Equals(target, Path.GetFullPath(record.Executable), StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return false;
        }
    }

    private static List<string> EnvironmentOf(IReadOnlyDictionary<string, string> overrides)
    {
        var variables = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            variables[(string)entry.Key] = (string?)entry.Value ?? "";
        }

        foreach (var (name, value) in overrides)
        {
            variables[name] = value;
        }

        return [.. variables.Select(pair => $"{pair.Key}={pair.Value}")];
    }

    // Makes room for the new log by keeping the newest KeptLogs - 1. Names sort by date.
    private static void DeleteOldLogs(string directory)
    {
        foreach (var old in Directory.GetFiles(directory, "*.log").OrderDescending(StringComparer.Ordinal).Skip(KeptLogs - 1))
        {
            File.Delete(old);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Started {Executable} (PID {ProcessId}), output in {LogFile}")]
    private partial void LogLaunched(string executable, int processId, string logFile);

    [LoggerMessage(Level = LogLevel.Information, Message = "Found {Executable} running again (PID {ProcessId})")]
    private partial void LogAttached(string executable, int processId);

    /// <summary>Stop for both kinds: SIGTERM to the group, the grace period, then SIGKILL; completes once the game has exited.</summary>
    private static async Task StopAsync(ILaunchedGame game, TimeSpan grace, CancellationToken cancellationToken)
    {
        var exit = game.WaitForExitAsync(cancellationToken);
        if (PosixSpawn.SignalGroup(game.ProcessId, PosixSpawn.SigTerm))
        {
            var finished = await Task.WhenAny(exit, Task.Delay(grace, cancellationToken)).ConfigureAwait(false) == exit;

            // A cancelled delay also completes WhenAny: cancelling only abandons the wait, it never ends the game.
            cancellationToken.ThrowIfCancellationRequested();
            if (!finished)
            {
                PosixSpawn.SignalGroup(game.ProcessId, PosixSpawn.SigKill);
            }
        }

        await exit.ConfigureAwait(false);
    }

    /// <summary>A game this launcher started: a thread waits for it from the start, so its exit code is kept and no zombie stays.</summary>
    private sealed class SpawnedGame : ILaunchedGame
    {
        private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TimeSpan _stopGrace;

        public SpawnedGame(int processId, string logFile, DateTimeOffset startedAt, TimeSpan stopGrace)
        {
            ProcessId = processId;
            LogFile = logFile;
            StartedAt = startedAt;
            _stopGrace = stopGrace;
            new Thread(() => _exited.TrySetResult(PosixSpawn.WaitForExit(processId))) { IsBackground = true, Name = "Game exit" }.Start();
        }

        public int ProcessId { get; }

        public string LogFile { get; }

        public DateTimeOffset StartedAt { get; }

        public Task<int> WaitForExitAsync(CancellationToken cancellationToken = default) => _exited.Task.WaitAsync(cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken = default) => LinuxGameLauncher.StopAsync(this, _stopGrace, cancellationToken);

        // The waiting thread keeps running: it reaps the game whenever it exits. Nothing else to release.
        public void Dispose()
        {
        }
    }

    /// <summary>A game found again from running.json: not our child, so its exit is seen by polling /proc and its exit code is unknown (-1).</summary>
    private sealed class AttachedGame(RunningGame record, TimeSpan stopGrace, TimeSpan poll) : ILaunchedGame
    {
        public int ProcessId => record.ProcessId;

        public string LogFile => record.LogFile;

        public DateTimeOffset StartedAt => record.StartedAt;

        public async Task<int> WaitForExitAsync(CancellationToken cancellationToken = default)
        {
            while (IsRunning())
            {
                await Task.Delay(poll, cancellationToken).ConfigureAwait(false);
            }

            return -1;
        }

        public Task StopAsync(CancellationToken cancellationToken = default) => LinuxGameLauncher.StopAsync(this, stopGrace, cancellationToken);

        public void Dispose()
        {
        }

        // Gone, a zombie, or a later process reusing the PID: the game has exited.
        private bool IsRunning()
        {
            try
            {
                return ProcStat.Read(record.ProcessId) is { State: not 'Z' } && ProcStat.StartTimeOf(record.ProcessId) == record.StartedAt;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                return false;
            }
        }
    }
}
