using PS5Kitsune.Core.AnyPS5;

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
}
