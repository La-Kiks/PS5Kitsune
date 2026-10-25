using PS5Kitsune.App.ViewModels;

namespace PS5Kitsune.App.Views;

public sealed partial class InstallView : PageView
{
    public InstallView() => InitializeComponent();

    /// <summary>Keeps the newest log line in view.</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is InstallViewModel viewModel)
        {
            viewModel.Log.CollectionChanged += (_, _) => LogScroller.ScrollToEnd();
        }
    }
}
