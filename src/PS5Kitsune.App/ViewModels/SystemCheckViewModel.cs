using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.App.ViewModels;

/// <summary>The PC check screen: runs every check and shows the results with a verdict.</summary>
public sealed partial class SystemCheckViewModel(ISystemChecker checker, IPlatform platform) : ObservableObject
{
    public ObservableCollection<CheckRowViewModel> Checks { get; } = [];

    /// <summary>One line per GPU the Vulkan probe found.</summary>
    public ObservableCollection<string> Gpus { get; } = [];

    [ObservableProperty]
    public partial bool IsChecking { get; private set; }

    [ObservableProperty]
    public partial string Verdict { get; private set; } = "";

    /// <summary>Null while checking.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BannerStatus))]
    public partial CheckStatus? Overall { get; private set; }

    /// <summary>Colors the verdict banner: <see cref="Overall"/> with Info shown as Ok, because cpu-vendor is always Info.</summary>
    public CheckStatus? BannerStatus => Overall == CheckStatus.Info ? CheckStatus.Ok : Overall;

    [RelayCommand]
    private async Task CheckAsync(CancellationToken cancellationToken)
    {
        IsChecking = true;
        Verdict = "Checking this PC…";
        Overall = null;
        Checks.Clear();
        Gpus.Clear();
        try
        {
            var report = await checker.RunAsync(cancellationToken).ConfigureAwait(true);
            foreach (var result in report.Results)
            {
                Checks.Add(new CheckRowViewModel(result, platform));
            }

            foreach (var gpu in report.Vulkan.Report?.Gpus ?? [])
            {
                Gpus.Add(gpu.Describe());
            }

            Overall = report.Overall;
            Verdict = report.Verdict;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Overall = CheckStatus.Blocking;
            Verdict = $"Could not check this PC: {exception.Message}";
        }
        finally
        {
            IsChecking = false;
        }
    }
}
