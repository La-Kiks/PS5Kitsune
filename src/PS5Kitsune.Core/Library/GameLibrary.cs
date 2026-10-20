using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Core.Library;

public sealed class GameNotFoundException(string message) : Exception(message);

/// <summary>Games added under &lt;root&gt;/games, looked up by title ID. library.json comes with milestone 7.</summary>
public sealed class GameLibrary(DataLayout layout, IPlatform platform, TimeProvider time)
{
    /// <exception cref="GameNotFoundException">No game.json for this title ID.</exception>
    /// <exception cref="InvalidGameRecordException">The game.json is malformed.</exception>
    public (GameLayout Game, GameRecord Record) Find(string titleId)
    {
        if (GameValidator.IsValidTitleId(titleId))
        {
            var game = new GameLayout(layout.GameDirectory(titleId), titleId, platform);
            if (GameRecordStore.Read(game.RecordFile) is { } record)
            {
                return (game, record);
            }
        }

        throw new GameNotFoundException($"No game {titleId} in {layout.Games}.");
    }

    public Task MarkPlayedAsync(GameLayout game, GameRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(record);
        return GameRecordStore.WriteAsync(game.RecordFile, record with { LastPlayedAt = time.GetLocalNow() }, cancellationToken);
    }
}
