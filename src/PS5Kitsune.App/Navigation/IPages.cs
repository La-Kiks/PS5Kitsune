namespace PS5Kitsune.App.Navigation;

/// <summary>Creates page view models for the current data root. Pages are objects: the window picks a view by type.</summary>
public interface IPages
{
    object Home();

    object SystemCheck();

    object Mode();

    object Install();

    object Done();
}
