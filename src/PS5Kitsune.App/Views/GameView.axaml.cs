using System.Collections.Specialized;
using Avalonia.Controls;
using PS5Kitsune.App.ViewModels;

namespace PS5Kitsune.App.Views;

public sealed partial class GameView : PageView
{
    private GameViewModel? _viewModel;

    public GameView() => InitializeComponent();

    /// <summary>Keeps the newest log line in view, and drops the subscription when the page changes.</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.Log.CollectionChanged -= OnLogChanged;
        }

        _viewModel = DataContext as GameViewModel;
        if (_viewModel is not null)
        {
            _viewModel.Log.CollectionChanged += OnLogChanged;
        }
    }

    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e) => LogScroller.ScrollToEnd();
}
