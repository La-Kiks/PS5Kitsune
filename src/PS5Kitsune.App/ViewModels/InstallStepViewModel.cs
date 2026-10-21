using CommunityToolkit.Mvvm.ComponentModel;

namespace PS5Kitsune.App.ViewModels;

public enum InstallStepState
{
    Pending,
    Running,
    Skipped,
    Done,
    Failed,
    Cancelled,
}

/// <summary>One line of the install page's step list.</summary>
public sealed partial class InstallStepViewModel(string name) : ObservableObject
{
    public string Name => name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText))]
    public partial InstallStepState State { get; set; }

    public string StateText => State switch
    {
        InstallStepState.Running => "running…",
        InstallStepState.Skipped => "already done",
        InstallStepState.Done => "done",
        InstallStepState.Failed => "failed",
        InstallStepState.Cancelled => "cancelled",
        _ => "",
    };
}
