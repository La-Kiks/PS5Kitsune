using Avalonia.Threading;
using PS5Kitsune.App.Navigation;

namespace PS5Kitsune.App.Shell;

public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public void Post(Action action) => Dispatcher.UIThread.Post(action);
}
