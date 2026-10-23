namespace PS5Kitsune.App.Navigation;

/// <summary>A page doing work that closing the window must stop first.</summary>
public interface IRunningWork
{
    bool IsRunning { get; }

    /// <summary>Asked before quitting while running; null cancels without asking.</summary>
    string? QuitMessage { get; }

    /// <summary>Cancels the work and waits until it has stopped, including its clean-up.</summary>
    Task CancelAndWaitAsync();
}
