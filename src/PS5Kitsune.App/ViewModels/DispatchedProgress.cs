using PS5Kitsune.App.Navigation;

namespace PS5Kitsune.App.ViewModels;

/// <summary>Reports progress on the UI thread; the downloads report from the thread pool.</summary>
internal sealed class DispatchedProgress<T>(IUiDispatcher ui, Action<T> report) : IProgress<T>
{
    public void Report(T value) => ui.Post(() => report(value));
}
