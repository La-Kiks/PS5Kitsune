using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Launch;

namespace PS5Kitsune.Core.Library;

/// <summary>Lists, launches and configures the added games: the one path for `run` and the app. See the 7a and 7b specs, "GamePlayer".</summary>
public sealed partial class GamePlayer(
    GameLibrary library, IAnyPS5Installer installer, IGameLauncher launcher, RunningGames running, LaunchPreparation preparation, ILogger<GamePlayer> logger)
{
    /// <summary>Every readable game, by name, with whether it runs.</summary>
    public IReadOnlyList<LibraryGame> List() =>
        [.. library.All().Select(entry => new LibraryGame(entry.Game, entry.Record, running.Check(entry.Game)))];

    /// <inheritdoc cref="LaunchAsync(string, IProgress{LaunchProgress}?, CancellationToken)"/>
    public Task<PlayingGame> LaunchAsync(string titleId, CancellationToken cancellationToken) => LaunchAsync(titleId, null, cancellationToken);

    /// <summary>
    /// Downloads what the settings need (outside the lock: it can take minutes), then launches the game from its folder with its settings
    /// as environment variables, its output in a new log, marks it played and records it in running.json. The game lock is held from the
    /// checks until the record is written, so a Reconvert or a removal cannot start in between.
    /// </summary>
    /// <exception cref="AnyPS5NotInstalledException">No AnyPS5 install is in use.</exception>
    /// <exception cref="GameNotFoundException">No game with this title ID.</exception>
    /// <exception cref="InvalidGameRecordException">The game.json is malformed.</exception>
    /// <exception cref="GameBusyException">Another action holds the game's lock.</exception>
    /// <exception cref="GameRunningException">The game is already running.</exception>
    /// <exception cref="GameExecutableMissingException">The converted executable is missing.</exception>
    /// <exception cref="LavapipeUnavailableException">The game uses Lavapipe and Mesa cannot be installed.</exception>
    public async Task<PlayingGame> LaunchAsync(string titleId, IProgress<LaunchProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(titleId);
        if (installer.ReadRecord() is null)
        {
            throw new AnyPS5NotInstalledException();
        }

        var (game, before) = library.Find(titleId);
        if (running.Check(game) is { } early)
        {
            throw new GameRunningException(before.Name, early);
        }

        var resources = await preparation.PrepareAsync(before.Settings, progress, cancellationToken).ConfigureAwait(false);

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

        // The settings may have turned Lavapipe on since the preparation.
        var manifest = !record.Settings.Lavapipe
            ? null
            : resources.LavapipeManifest ?? preparation.InstalledLavapipe() ?? throw new LavapipeUnavailableException("Lavapipe is not installed. Launch again to download it.");
        var environment = LaunchEnvironment.For(record.Settings, manifest, resources.FontsDirectory);
        var process = launcher.Launch(new GameLaunchRequest(executable, game.Root, game.Logs) { Environment = environment });
        GameRecord played;
        try
        {
            // The game has started: recording it is not cancelled.
            played = await MarkPlayedAsync(game, record).ConfigureAwait(false);
            await running.RecordAsync(game, process, executable).ConfigureAwait(false);
        }
        catch
        {
            process.Dispose();
            throw;
        }

        return new PlayingGame(game, played, process) { FontsWarning = resources.FontsWarning };
    }

    /// <summary>Replaces the game's settings (normalized), keeping the rest of game.json as it is now.</summary>
    /// <returns>The record written.</returns>
    /// <exception cref="GameNotFoundException">No game with this title ID.</exception>
    /// <exception cref="InvalidGameRecordException">The game.json is malformed.</exception>
    /// <exception cref="GameBusyException">Another action holds the game's lock.</exception>
    /// <exception cref="GameRunningException">The game is running: settings only apply at launch.</exception>
    public async Task<GameRecord> SaveSettingsAsync(string titleId, GameSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(titleId);
        ArgumentNullException.ThrowIfNull(settings);
        var game = library.Layout(titleId);
        using var held = GameLock.TryAcquire(game) ?? throw new GameBusyException(titleId);
        var record = library.ReadHeld(game);
        if (running.Check(game) is { } current)
        {
            throw new GameRunningException(record.Name, current);
        }

        var saved = record with { Settings = GameSettingsValidator.Normalize(settings).Settings };
        await GameRecordStore.WriteAsync(game.RecordFile, saved, cancellationToken).ConfigureAwait(false);
        return saved;
    }

    /// <summary>Deletes the game's running.json once its exit was seen.</summary>
    public void Forget(PlayingGame playing)
    {
        ArgumentNullException.ThrowIfNull(playing);
        running.Forget(playing.Game, playing.Process);
    }

    // A record that cannot be written does not stop a game that already runs.
    private async Task<GameRecord> MarkPlayedAsync(GameLayout game, GameRecord record)
    {
        try
        {
            return await library.MarkPlayedAsync(game, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidGameRecordException or GameNotFoundException)
        {
            var titleId = record.TitleId;
            LogNotMarkedPlayed(exception, titleId);
            return record;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{TitleId} started, but its last played date could not be written.")]
    private partial void LogNotMarkedPlayed(Exception exception, string titleId);
}
