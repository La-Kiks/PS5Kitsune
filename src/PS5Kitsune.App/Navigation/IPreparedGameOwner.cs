namespace PS5Kitsune.App.Navigation;

/// <summary>A page holding a prepared game, whose staging folder must be deleted when the window closes.</summary>
public interface IPreparedGameOwner
{
    Task ReleaseAsync();
}
