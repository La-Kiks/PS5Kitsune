using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Core.Library;

public sealed class GameNotFoundException(string message) : Exception(message);

/// <summary>
/// Games added under &lt;root&gt;/games, looked up by title ID.
/// Reading a game first restores a Reconvert that did not finish, so a half-built conversion is never used; a game whose lock is held (a Reconvert or another action is running) is left alone.
/// </summary>
public sealed partial class GameLibrary(DataLayout layout, IPlatform platform, TimeProvider time, ILogger<GameLibrary> logger)
{
    /// <exception cref="GameNotFoundException">No game.json for this title ID.</exception>
    /// <exception cref="InvalidGameRecordException">The game.json is malformed.</exception>
    /// <exception cref="IOException">Restoring an interrupted Reconvert failed; the next read retries it.</exception>
    /// <exception cref="UnauthorizedAccessException">Restoring an interrupted Reconvert was denied; the next read retries it.</exception>
    public (GameLayout Game, GameRecord Record) Find(string titleId)
    {
        var game = Layout(titleId);
        RestoreInterrupted(game);
        return (game, Normalized(GameRecordStore.Read(game.RecordFile) ?? throw NotFound(titleId)));
    }

    /// <summary>The folder of an added game, without reading or restoring it.</summary>
    /// <exception cref="GameNotFoundException">The title ID is not valid, or the game has no game.json.</exception>
    public GameLayout Layout(string titleId)
    {
        if (GameValidator.IsValidTitleId(titleId))
        {
            var game = new GameLayout(layout.GameDirectory(titleId), titleId, platform);
            if (File.Exists(game.RecordFile))
            {
                return game;
            }
        }

        throw NotFound(titleId);
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
                    games.Add((game, Normalized(record)));
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

    /// <summary>Re-reads game.json and writes it with lastPlayedAt set, so a change made since the game was found is kept.</summary>
    /// <returns>The record written.</returns>
    public async Task<GameRecord> MarkPlayedAsync(GameLayout game, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(game);
        var played = (GameRecordStore.Read(game.RecordFile) ?? throw NotFound(game.TitleId)) with { LastPlayedAt = time.GetLocalNow() };
        await GameRecordStore.WriteAsync(game.RecordFile, played, cancellationToken).ConfigureAwait(false);
        return played;
    }

    /// <summary>Reads a game whose lock the caller holds: restores an interrupted Reconvert first (Find would skip it, the lock being held).</summary>
    /// <exception cref="GameNotFoundException">No game.json.</exception>
    /// <exception cref="InvalidGameRecordException">The game.json is malformed.</exception>
    /// <exception cref="IOException">The restore failed; the next read retries it.</exception>
    /// <exception cref="UnauthorizedAccessException">The restore was denied; the next read retries it.</exception>
    public GameRecord ReadHeld(GameLayout game)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (ConversionBackup.Restore(game))
        {
            var root = game.Root;
            LogRestored(root);
        }

        return Normalized(GameRecordStore.Read(game.RecordFile) ?? throw NotFound(game.TitleId));
    }

    /// <summary>True when at least one game folder has a game.json; reads nothing and restores nothing.</summary>
    public bool HasGames() =>
        Directory.Exists(layout.Games)
        && Directory.EnumerateDirectories(layout.Games).Any(directory =>
            Path.GetFileName(directory) is var titleId
            && GameValidator.IsValidTitleId(titleId)
            && File.Exists(new GameLayout(directory, titleId, platform).RecordFile));

    // game.json may say "settings": null; a value AnyPS5 would refuse is replaced in memory only, so the file shows what was written.
    private GameRecord Normalized(GameRecord record)
    {
        var (settings, replaced) = GameSettingsValidator.Normalize(record.Settings ?? new GameSettings());
        foreach (var field in replaced)
        {
            var titleId = record.TitleId;
            LogSettingReplaced(titleId, field);
        }

        return record with { Settings = settings };
    }

    private GameNotFoundException NotFound(string titleId) => new($"No game {titleId} in {layout.Games}.");

    // An action in progress holds the lock (a Reconvert's set-aside conversion is not interrupted), so the game is left alone. The reader holds the
    // lock while it restores, so a Reconvert cannot start in between; with nothing set aside, no lock file is opened.
    private void RestoreInterrupted(GameLayout game)
    {
        if (!Directory.Exists(game.Previous) && !Directory.Exists(game.PreviousPartial) && !Directory.Exists(game.PreviousDiscarded))
        {
            return;
        }

        using var held = GameLock.TryAcquireNow(game);
        if (held is not null && ConversionBackup.Restore(game))
        {
            var root = game.Root;
            LogRestored(root);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Restored the previous conversion of {Game}: a Reconvert did not finish.")]
    private partial void LogRestored(string game);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipped the game in {Directory}: it cannot be read.")]
    private partial void LogGameSkipped(Exception exception, string directory);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{TitleId}: the setting {Field} in game.json is not valid; its default is used.")]
    private partial void LogSettingReplaced(string titleId, string field);
}
