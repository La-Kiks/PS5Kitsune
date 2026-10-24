using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Core.Library;

/// <summary>Removes a game or clears its shader cache: under the game lock, and never while the game runs.</summary>
public sealed partial class GameCleaner(GameLibrary library, RunningGames running, ILogger<GameCleaner> logger)
{
    /// <summary>Bytes in shader_cache\; 0 when there is none.</summary>
    public static long ShaderCacheSize(GameLayout game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return Directory.Exists(game.ShaderCache)
            ? new DirectoryInfo(game.ShaderCache).EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length)
            : 0;
    }

    /// <summary>True when data\ holds at least one file.</summary>
    public static bool HasSaves(GameLayout game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return Directory.Exists(game.Data) && Directory.EnumerateFiles(game.Data, "*", SearchOption.AllDirectories).Any();
    }

    /// <exception cref="GameNotFoundException">No game with this title ID.</exception>
    /// <exception cref="GameBusyException">Another action holds the game's lock.</exception>
    /// <exception cref="GameRunningException">The game runs.</exception>
    /// <exception cref="IOException">A file is in use: the cache may be partly cleared, and the game rebuilds it.</exception>
    /// <exception cref="UnauthorizedAccessException">A file could not be deleted: the cache may be partly cleared.</exception>
    public void ClearShaderCache(string titleId)
    {
        var game = library.Layout(titleId);
        using var held = GameLock.TryAcquire(game) ?? throw new GameBusyException(titleId);
        RefuseRunning(game, library.ReadHeld(game));
        if (Directory.Exists(game.ShaderCache))
        {
            Directory.Delete(game.ShaderCache, recursive: true);
        }
    }

    /// <summary>
    /// Deletes the game folder, saves included. game.json goes last: a delete stopped by a file in use leaves the game listed, and a
    /// retry finishes it.
    /// </summary>
    /// <exception cref="GameNotFoundException">No game with this title ID.</exception>
    /// <exception cref="GameBusyException">Another action holds the game's lock.</exception>
    /// <exception cref="GameRunningException">The game runs.</exception>
    /// <exception cref="IOException">A file is in use; the game stays listed.</exception>
    /// <exception cref="UnauthorizedAccessException">A file could not be deleted; the game stays listed.</exception>
    public void Remove(string titleId)
    {
        var game = library.Layout(titleId);
        using (GameLock.TryAcquire(game) ?? throw new GameBusyException(titleId))
        {
            RefuseRunning(game, library.ReadHeld(game));
            foreach (var directory in Directory.GetDirectories(game.Root))
            {
                if (!SamePath(directory, game.Meta))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }

            foreach (var file in Directory.GetFiles(game.Root))
            {
                File.Delete(file);
            }

            foreach (var directory in Directory.GetDirectories(game.Meta))
            {
                Directory.Delete(directory, recursive: true);
            }

            foreach (var file in Directory.GetFiles(game.Meta))
            {
                if (!SamePath(file, game.RecordFile) && !SamePath(file, game.Lock))
                {
                    File.Delete(file);
                }
            }

            File.Delete(game.RecordFile);
        }

        // The lock file went with its handle; without game.json the folder is no longer a game, so a failure here is only logged.
        try
        {
            Directory.Delete(game.Root, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var root = game.Root;
            LogFolderLeft(exception, root);
        }
    }

    private void RefuseRunning(GameLayout game, GameRecord record)
    {
        if (running.Check(game) is { } current)
        {
            throw new GameRunningException(record.Name, current);
        }
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Removed the game, but {Root} could not be deleted; it is no longer listed.")]
    private partial void LogFolderLeft(Exception exception, string root);
}
