using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Core.Library;

/// <summary>Lists and launches the added games: the one path for `run` and the app. See the 7a spec, "GamePlayer".</summary>
public sealed class GamePlayer(GameLibrary library, IAnyPS5Installer installer, IGameLauncher launcher, RunningGames running)
{
    /// <summary>Every readable game, by name, with whether it runs.</summary>
    public IReadOnlyList<LibraryGame> List() =>
        [.. library.All().Select(entry => new LibraryGame(entry.Game, entry.Record, running.Check(entry.Game)))];

    /// <summary>
    /// Launches the game from its folder, its output in a new log, and records it in running.json. The game lock is held from the checks
    /// until the record is written, so a Reconvert or a removal cannot start in between.
    /// </summary>
    /// <exception cref="AnyPS5NotInstalledException">No AnyPS5 install is in use.</exception>
    /// <exception cref="GameNotFoundException">No game with this title ID.</exception>
    /// <exception cref="InvalidGameRecordException">The game.json is malformed.</exception>
    /// <exception cref="GameBusyException">Another action holds the game's lock.</exception>
    /// <exception cref="GameRunningException">The game is already running.</exception>
    /// <exception cref="GameExecutableMissingException">The converted executable is missing.</exception>
    public async Task<PlayingGame> LaunchAsync(string titleId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(titleId);
        if (installer.ReadRecord() is null)
        {
            throw new AnyPS5NotInstalledException();
        }

        var game = library.Layout(titleId);
        using var held = GameLock.TryAcquire(game) ?? throw new GameBusyException(titleId);
        var record = library.ReadHeld(game);
        if (running.Check(game) is { } current)
        {
            throw new GameRunningException(record.Name, current);
        }

        var executable = Path.Combine(game.Root, record.Executable);
        if (!File.Exists(executable))
        {
            throw new GameExecutableMissingException(executable);
        }

        var played = await library.MarkPlayedAsync(game, cancellationToken).ConfigureAwait(false);
        var process = launcher.Launch(new GameLaunchRequest(executable, game.Root, game.Logs));
        try
        {
            await running.RecordAsync(game, process, executable).ConfigureAwait(false);
        }
        catch
        {
            process.Dispose();
            throw;
        }

        return new PlayingGame(game, played, process);
    }

    /// <summary>Deletes the game's running.json once its exit was seen.</summary>
    public void Forget(PlayingGame playing)
    {
        ArgumentNullException.ThrowIfNull(playing);
        running.Forget(playing.Game, playing.Process);
    }
}
