using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.App.Navigation;

/// <summary>
/// Creates page view models for the current data root. Pages are objects: the window picks a view by type.
/// The chosen mode travels from page to page as an argument, never in the session.
/// </summary>
public interface IPages
{
    object Home();

    object Mode();

    object SystemCheck(AnyPS5Mode mode);

    /// <summary>The build tools screen; only build mode goes through it.</summary>
    object Tools();

    object Install(AnyPS5Mode mode);

    object Done();

    object ChooseGame();

    /// <summary>Takes ownership of <paramref name="game"/> until it is added or the user goes back.</summary>
    object GameSummary(PreparedGame game);

    object AddGame(PreparedGame game, bool toIntel);

    object AddResult(GameAdded added);

    object Reconvert(string titleId, string name);

    object ReconvertResult(GameReconverted reconverted);

    /// <summary>The added games; the first page once AnyPS5 is installed and a game is added.</summary>
    object Library();

    /// <summary>A running game. Takes ownership of <paramref name="playing"/>'s process handle.</summary>
    /// <param name="firstLaunch">The game was never played before: shaders are prepared, so it may stutter.</param>
    object Game(PlayingGame playing, bool firstLaunch);

    /// <summary>A game to launch: the page downloads what it needs, launches it, then follows it.</summary>
    /// <param name="firstLaunch">The game was never played before: shaders are prepared, so it may stutter.</param>
    object Launch(GameLayout game, string name, bool firstLaunch);
}
