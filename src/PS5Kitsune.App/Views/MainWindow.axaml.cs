using Avalonia.Controls;
using PS5Kitsune.App.ViewModels;

namespace PS5Kitsune.App.Views;

public sealed partial class MainWindow : Window
{
    private bool _closeConfirmed;

    public MainWindow() => InitializeComponent();

    /// <summary>During an install, closing waits for the user's answer and for the install to stop.</summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!_closeConfirmed && DataContext is MainWindowViewModel { IsInstallRunning: true } viewModel)
        {
            e.Cancel = true;
            _ = ConfirmAndCloseAsync(viewModel);
        }

        base.OnClosing(e);
    }

    private async Task ConfirmAndCloseAsync(MainWindowViewModel viewModel)
    {
        if (await viewModel.ConfirmCloseAsync().ConfigureAwait(true))
        {
            _closeConfirmed = true;
            Close();
        }
    }
}
