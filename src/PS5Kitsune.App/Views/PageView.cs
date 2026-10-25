using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PS5Kitsune.App.Controls;

namespace PS5Kitsune.App.Views;

/// <summary>
/// Every page's view: once loaded, focus goes to its <see cref="InitialFocus"/> candidate, so the keyboard starts on the page.
/// A page that loads in the background has no candidate yet (or a worse one), so the choice is repeated whenever its view model changes,
/// until the user moves focus themselves, focus leaves the page after we placed it, or the view is unloaded.
/// </summary>
public class PageView : UserControl
{
    private INotifyPropertyChanged? _source;
    private bool _watching;
    private bool _placed;
    private bool _causing;
    private bool _queued;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _watching = true;
        _placed = false;
        AddHandler(GotFocusEvent, OnGotFocus);
        Watch(DataContext as INotifyPropertyChanged);
        Queue();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        _watching = false;
        RemoveHandler(GotFocusEvent, OnGotFocus);
        Watch(null);
        base.OnUnloaded(e);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_watching)
        {
            Watch(DataContext as INotifyPropertyChanged);
            Queue();
        }
    }

    private void Watch(INotifyPropertyChanged? source)
    {
        if (_source is not null)
        {
            _source.PropertyChanged -= OnSourceChanged;
        }

        _source = source;
        if (_source is not null)
        {
            _source.PropertyChanged += OnSourceChanged;
        }
    }

    private void OnSourceChanged(object? sender, PropertyChangedEventArgs e) => Queue();

    // A focus we did not cause: the user chose, so the page stops choosing for them.
    private void OnGotFocus(object? sender, RoutedEventArgs e)
    {
        if (!_causing)
        {
            _watching = false;
        }
    }

    // After the layout and the bindings, so IsVisible and IsEnabled are up to date; one pass for a burst of changes.
    private void Queue()
    {
        if (_queued)
        {
            return;
        }

        _queued = true;
        Dispatcher.UIThread.Post(Settle, DispatcherPriority.Loaded);
    }

    private void Settle()
    {
        _queued = false;
        if (!_watching)
        {
            return;
        }

        // Once placed, focus that moved out of the page is the user's: leave it there.
        if (_placed && TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Visual focused && !this.IsVisualAncestorOf(focused))
        {
            _watching = false;
            return;
        }

        _causing = true;
        try
        {
            _placed |= InitialFocus.Apply(this);
        }
        finally
        {
            _causing = false;
        }
    }
}
