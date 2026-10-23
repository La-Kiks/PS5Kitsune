using Avalonia.Controls;
using PS5Kitsune.App.ViewModels;

namespace PS5Kitsune.App.Views;

public sealed partial class MainWindow : Window
{
    private bool _closeConfirmed;
    private bool _closePending;

    public MainWindow() => InitializeComponent();

    /// <summary>While a page runs work or holds a prepared game, closing waits for the user's answer and for the clean-up.</summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!_closeConfirmed && DataContext is MainWindowViewModel { NeedsCloseHandling: true } viewModel)
        {
            e.Cancel = true;
            if (!_closePending)
            {
                _closePending = true;
                _ = ConfirmAndCloseAsync(viewModel);
            }
        }

        base.OnClosing(e);
    }

    private async Task ConfirmAndCloseAsync(MainWindowViewModel viewModel)
    {
        try
        {
            if (await viewModel.ConfirmCloseAsync().ConfigureAwait(true))
            {
                _closeConfirmed = true;
                Close();
                return;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The window stays open; the work keeps running.
        }

        _closePending = false;
    }
}
