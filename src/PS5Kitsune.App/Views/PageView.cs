using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PS5Kitsune.App.Controls;

namespace PS5Kitsune.App.Views;

/// <summary>Every page's view: once loaded, focus goes to its <see cref="InitialFocus"/> candidate, so the keyboard starts on the page.</summary>
public class PageView : UserControl
{
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // After the first layout, so bindings have set IsVisible and IsEnabled.
        Dispatcher.UIThread.Post(() => InitialFocus.Apply(this), DispatcherPriority.Loaded);
    }
}
