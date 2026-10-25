using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.App.ViewModels;

/// <summary>The PC check screen for the chosen mode: runs every check and shows the results with a verdict. A blocking result stops the install here.</summary>
public sealed partial class SystemCheckViewModel(ISystemChecker checker, AnyPS5Mode mode, IPlatform platform, Navigator navigator, IPages pages)
    : ObservableObject, INavigationAware
{
    public ObservableCollection<CheckRowViewModel> Checks { get; } = [];

    /// <summary>One line per GPU the Vulkan probe found.</summary>
    public ObservableCollection<string> Gpus { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanContinue))]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    public partial bool IsChecking { get; private set; }

    [ObservableProperty]
    public partial string Verdict { get; private set; } = "";

    /// <summary>Null while checking, before the first check and after a cancelled one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BannerStatus))]
    [NotifyPropertyChangedFor(nameof(BannerLabel))]
    [NotifyPropertyChangedFor(nameof(CanContinue))]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    public partial CheckStatus? Overall { get; private set; }

    /// <summary>Colors the verdict banner: <see cref="Overall"/> with Info shown as Ok, because cpu-vendor is always Info.</summary>
    public CheckStatus? BannerStatus => Overall == CheckStatus.Info ? CheckStatus.Ok : Overall;

    /// <summary>The banner chip's word: "Checking" while there is no status.</summary>
    public string BannerLabel => BannerStatus is { } status ? CheckRowViewModel.Label(status) : "Checking";

    /// <summary>Warnings do not block: the banner already shows them.</summary>
    public bool CanContinue => !IsChecking && Overall is { } overall && overall != CheckStatus.Blocking;

    /// <summary>Checks the first time the page is shown, and again after a cancelled check; not when coming back from a later page.</summary>
    public void OnNavigatedTo()
    {
        if (!IsChecking && Overall is null)
        {
            CheckCommand.Execute(null);
        }
    }

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
            var report = await checker.RunAsync(mode, cancellationToken).ConfigureAwait(true);
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
        catch (OperationCanceledException)
        {
            Verdict = "Check cancelled.";
        }
        catch (Exception exception)
        {
            Overall = CheckStatus.Blocking;
            Verdict = $"Could not check this PC: {exception.Message}";
        }
        finally
        {
            IsChecking = false;
        }
    }

    /// <summary>Build mode shows its tools first; release goes straight to the install.</summary>
    [RelayCommand(CanExecute = nameof(CanContinue))]
    private void Continue() => navigator.GoTo(mode == AnyPS5Mode.Build ? pages.Tools() : pages.Install(mode));

    [RelayCommand]
    private void Back()
    {
        CheckCommand.Cancel();
        navigator.GoBack();
    }
}
