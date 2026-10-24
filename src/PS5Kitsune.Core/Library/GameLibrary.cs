using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Core.Library;

public sealed class GameNotFoundException(string message) : Exception(message);

/// <summary>
/// Games added under &lt;root&gt;/games, looked up by title ID. library.json comes with milestone 7.
/// Reading a game first restores a Reconvert that did not finish, so a half-built conversion is never used.
/// </summary>
public sealed partial class GameLibrary(DataLayout layout, IPlatform platform, TimeProvider time, ILogger<GameLibrary> logger)
{
    /// <exception cref="GameNotFoundException">No game.json for this title ID.</exception>
    /// <exception cref="InvalidGameRecordException">The game.json is malformed.</exception>
    public (GameLayout Game, GameRecord Record) Find(string titleId)
    {
        if (GameValidator.IsValidTitleId(titleId))
        {
            var game = new GameLayout(layout.GameDirectory(titleId), titleId, platform);
            RestoreInterrupted(game);
            if (GameRecordStore.Read(game.RecordFile) is { } record)
            {
                return (game, record);
            }
        }

        throw new GameNotFoundException($"No game {titleId} in {layout.Games}.");
    }

    /// <summary>Every game whose game.json reads, ordered by name; a game that cannot be read is skipped and logged.</summary>
    public IReadOnlyList<(GameLayout Game, GameRecord Record)> All()
    {
        if (!Directory.Exists(layout.Games))
        {
            return [];
        }

        var games = new List<(GameLayout Game, GameRecord Record)>();
        foreach (var directory in Directory.EnumerateDirectories(layout.Games))
        {
            var titleId = Path.GetFileName(directory);
            if (!GameValidator.IsValidTitleId(titleId))
            {
                continue;
            }

            var game = new GameLayout(directory, titleId, platform);
            try
            {
                RestoreInterrupted(game);
                if (GameRecordStore.Read(game.RecordFile) is { } record)
                {
                    games.Add((game, record));
                }
            }
            catch (Exception exception) when (exception is InvalidGameRecordException or IOException or UnauthorizedAccessException)
            {
                LogGameSkipped(exception, directory);
            }
        }

        return [.. games.OrderBy(entry => entry.Record.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>True when the game was converted with another profile, mode, version or commit than <paramref name="install"/>.</summary>
    public static bool NeedsReconversion(GameRecord record, AnyPS5InstallRecord install)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(install);
        var converted = record.ConvertedWith;
        return converted.Profile != install.Profile
            || converted.Mode != InstallModes.Name(install.Mode)
            || converted.Version != install.Version
            || converted.Commit != install.Commit;
    }

    public Task MarkPlayedAsync(GameLayout game, GameRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(record);
        return GameRecordStore.WriteAsync(game.RecordFile, record with { LastPlayedAt = time.GetLocalNow() }, cancellationToken);
    }

    private void RestoreInterrupted(GameLayout game)
    {
        if (ConversionBackup.Restore(game))
        {
            var root = game.Root;
            LogRestored(root);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Restored the previous conversion of {Game}: a Reconvert did not finish.")]
    private partial void LogRestored(string game);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipped the game in {Directory}: it cannot be read.")]
    private partial void LogGameSkipped(Exception exception, string directory);
}
