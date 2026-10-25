using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.App.ViewModels;

/// <summary>One check on the PC check screen.</summary>
public sealed partial class CheckRowViewModel(CheckResult result, IPlatform platform) : ObservableObject
{
    public string Title => result.Title;

    public string Summary => result.Summary;

    public string? Action => result.Action;

    public bool HasAction => result.Action is not null;

    public CheckStatus Status => result.Status;

    public string StatusText => Label(result.Status);

    /// <summary>The word on a status chip.</summary>
    public static string Label(CheckStatus status) => status switch
    {
        CheckStatus.Ok => "OK",
        CheckStatus.Info => "Info",
        CheckStatus.Warning => "Warning",
        _ => "Blocking",
    };

    public bool HasFix => result.Fix is not null;

    /// <summary>Why the fix could not be opened; null when it opened.</summary>
    [ObservableProperty]
    public partial string? FixError { get; private set; }

    [RelayCommand]
    private void OpenFix()
    {
        if (result.Fix is { } fix)
        {
            FixError = PathOpener.TryRun(() => platform.OpenFix(fix));
        }
    }
}
