using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.App.ViewModels;

/// <summary>
/// Build mode's tools: what is installed, what is missing and its download size. The install's first steps
/// download what is missing, so this page only hands off to Install.
/// </summary>
public sealed partial class ToolsViewModel(BuildToolsSummary summary, Navigator navigator, IPages pages)
    : ObservableObject, INavigationAware
{
    public ObservableCollection<ToolRowViewModel> Tools { get; } = [];

    /// <summary>The 5a criterion run measured a full build root at 3.2 GB.</summary>
    public string DiskText { get; } = "Disk space for the build: about 3 GB.";

    [ObservableProperty]
    public partial string DownloadText { get; private set; } = "";

    [ObservableProperty]
    public partial string ContinueText { get; private set; } = "";

    /// <summary>Reads the tools on every visit: coming back after a cancelled install may find more of them.</summary>
    public void OnNavigatedTo()
    {
        Tools.Clear();
        foreach (var tool in summary.Read())
        {
            Tools.Add(new ToolRowViewModel(tool));
        }

        var missing = Tools.Where(tool => !tool.IsInstalled).Select(tool => tool.Info).ToList();
        var known = missing.Sum(tool => tool.SizeBytes ?? 0);
        var anyUnknown = missing.Any(tool => tool.SizeBytes is null);
        DownloadText = missing.Count == 0 ? "All tools are present."
            : known == 0 ? "To download: size unknown"
            : anyUnknown ? $"To download: at least {ToolRowViewModel.FormatMegabytes(known)}"
            : $"To download: {ToolRowViewModel.FormatMegabytes(known)}";
        ContinueText = missing.Count == 0 ? "Build" : "Download what's missing and build";
    }

    [RelayCommand]
    private void Back() => navigator.GoBack();

    [RelayCommand]
    private void Continue() => navigator.GoTo(pages.Install(AnyPS5Mode.Build));
}
