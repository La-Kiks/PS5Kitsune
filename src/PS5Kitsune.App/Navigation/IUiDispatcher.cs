namespace PS5Kitsune.App.Navigation;

/// <summary>Runs an action on the UI thread, in order. Tests run it inline.</summary>
public interface IUiDispatcher
{
    void Post(Action action);
}
