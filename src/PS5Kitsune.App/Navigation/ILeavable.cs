namespace PS5Kitsune.App.Navigation;

/// <summary>A page with clean-up to run before it is left, by its own Back or by the sidebar.</summary>
public interface ILeavable
{
    /// <summary>Runs the clean-up without navigating.</summary>
    /// <returns>False when the user chose to stay (for example, declined to discard changes).</returns>
    Task<bool> LeaveAsync();
}
