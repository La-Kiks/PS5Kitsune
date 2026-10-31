using System.Text.Json;
using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Core.Library;

/// <summary>
/// The game PS5Kitsune launched from each game folder, recorded in .ps5kitsune\running.json and checked against the live process: closing
/// PS5Kitsune never stops a game, and the next start finds it again. A record whose process exited, or whose PID now belongs to another
/// process, is deleted, and so is a malformed one.
/// </summary>
public sealed partial class RunningGames(IGameLauncher launcher, ILogger<RunningGames> logger)
{
    /// <summary>The game's process, opened again; null when it is not running. Dispose the result.</summary>
    public ILaunchedGame? Attach(GameLayout game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return Find(game).Process;
    }

    /// <summary>The game's record when its process is running; the handle opened to check it is closed again.</summary>
    public RunningGame? Check(GameLayout game)
    {
        ArgumentNullException.ThrowIfNull(game);
        var (record, process) = Find(game);
        using (process)
        {
            return process is null ? null : record;
        }
    }

    /// <summary>Records a game just launched. Not cancellable: the game already runs.</summary>
    public Task RecordAsync(GameLayout game, ILaunchedGame process, string executable)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(process);
        var record = new RunningGame(RunningGame.CurrentSchemaVersion, process.ProcessId, process.StartedAt, Path.GetFullPath(executable), process.LogFile)
        {
            BootId = process.BootId,
            StartTicks = process.StartTicks,
        };
        LogRecorded(record.ProcessId, game.TitleId);
        return JsonFiles.WriteAsync(game.RunningRecord, record, CancellationToken.None);
    }

    /// <summary>Deletes the record once its game exited; a record of a later launch is kept.</summary>
    public void Forget(GameLayout game, ILaunchedGame process)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(process);
        if (TryRead(game) is { } record && record.ProcessId == process.ProcessId && record.StartedAt == process.StartedAt)
        {
            LogForgotten(record.ProcessId, game.TitleId);
            TryDelete(game.RunningRecord);
        }
    }

    private (RunningGame? Record, ILaunchedGame? Process) Find(GameLayout game)
    {
        RunningGame? record;
        try
        {
            record = Read(game);
        }
        catch (JsonException exception)
        {
            var path = game.RunningRecord;
            LogMalformed(exception, path);
            TryDelete(path);
            return (null, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Being replaced by a launch right now: unknown, so not deleted.
            var path = game.RunningRecord;
            LogUnreadable(exception, path);
            return (null, null);
        }

        if (record is null)
        {
            return (null, null);
        }

        var process = launcher.Attach(record);
        if (process is null && TryRead(game) == record)
        {
            // Unchanged since it was read: a launch that just replaced it is not undone.
            TryDelete(game.RunningRecord);
        }

        return (record, process);
    }

    /// <exception cref="JsonException">Malformed, another schema version, or a field missing.</exception>
    private static RunningGame? Read(GameLayout game)
    {
        var record = JsonFiles.Read<RunningGame>(game.RunningRecord);
        if (record is null)
        {
            return null;
        }

        if (record.SchemaVersion is not (1 or RunningGame.CurrentSchemaVersion))
        {
            throw new JsonException($"Schema version {record.SchemaVersion}; this app reads versions 1 and {RunningGame.CurrentSchemaVersion}.");
        }

        return record.ProcessId > 0 && !string.IsNullOrEmpty(record.Executable) && !string.IsNullOrEmpty(record.LogFile)
            ? record
            : throw new JsonException("A field is missing.");
    }

    private static RunningGame? TryRead(GameLayout game)
    {
        try
        {
            return Read(game);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Found stale again on the next check.
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Recorded process {ProcessId} as the running game {TitleId}.")]
    private partial void LogRecorded(int processId, string titleId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Forgot process {ProcessId} of the game {TitleId}: it exited.")]
    private partial void LogForgotten(int processId, string titleId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Deleted {Path}: it is not a valid running game record.")]
    private partial void LogMalformed(Exception exception, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read {Path}; the game is treated as not running.")]
    private partial void LogUnreadable(Exception exception, string path);
}
