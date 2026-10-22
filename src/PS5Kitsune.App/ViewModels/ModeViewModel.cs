using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.App.ViewModels;

/// <summary>Release or build, chosen before the PC check because the check depends on the mode.</summary>
public sealed partial class ModeViewModel : ObservableObject
{
    private readonly AnyPS5Profile _profile;
    private readonly Navigator _navigator;
    private readonly IPages _pages;
    private readonly AnyPS5InstallRecord? _record;
    private readonly AnyPS5Mode? _requestedMode;

    /// <summary>Preselects the mode the CLI would use without --mode: an unfinished install's, else the installed one's, else release.</summary>
    public ModeViewModel(AnyPS5Profile profile, IAnyPS5Installer installer, Navigator navigator, IPages pages)
    {
        _profile = profile;
        _navigator = navigator;
        _pages = pages;
        _record = installer.ReadRecord();
        _requestedMode = installer.RequestedMode;
        CanBuild = installer.Supports(AnyPS5Mode.Build);
        SelectedMode = installer.CurrentMode == AnyPS5Mode.Build && CanBuild ? AnyPS5Mode.Build : AnyPS5Mode.Release;
    }

    public string ReleaseTitle => $"Release {_profile.Source.Version}";

    public string PublishedText => _profile.Source.PublishedAt is { } publishedAt
        ? "Published " + publishedAt.UtcDateTime.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture)
        : "Publication date unknown";

    public string ProfileStatusText => _profile.Validated ? "Validated with DOOM for PS5" : "Not validated yet";

    /// <summary>False when the active profile has no build section or no build tools.</summary>
    public bool CanBuild { get; }

    public string BuildCommitText => _profile.Build is { } build ? $"Commit {build.Commit[..7]}" : "";

    public string BuildDurationText { get; } = "About 15 minutes the first time, faster for updates.";

    public string BuildDiskText => string.Create(CultureInfo.InvariantCulture, $"Needs about {_profile.Checks.BuildMinFreeDiskGiB} GiB of free disk space.");

    public string BuildUnavailableText { get; } = "This profile does not define a build.";

    /// <summary>"Installed: {version}" on the release card when the install is a release; null otherwise.</summary>
    public string? ReleaseInstalledText => InstalledText(AnyPS5Mode.Release);

    /// <summary>"Installed: {version}" on the build card when the install is a build; null otherwise.</summary>
    public string? BuildInstalledText => InstalledText(AnyPS5Mode.Build);

    /// <summary>Names what Continue does for the selected mode.</summary>
    public string ContinueText => _requestedMode == SelectedMode ? "Resume the install"
        : _record?.Mode == SelectedMode ? "Verify / repair"
        : "Continue";

    private string? InstalledText(AnyPS5Mode mode) => _record is not null && _record.Mode == mode ? $"Installed: {_record.Version}" : null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContinueText))]
    [NotifyPropertyChangedFor(nameof(IsReleaseSelected))]
    [NotifyPropertyChangedFor(nameof(IsBuildSelected))]
    public partial AnyPS5Mode SelectedMode { get; private set; }

    public bool IsReleaseSelected => SelectedMode == AnyPS5Mode.Release;

    public bool IsBuildSelected => SelectedMode == AnyPS5Mode.Build;

    [RelayCommand]
    private void SelectRelease() => SelectedMode = AnyPS5Mode.Release;

    [RelayCommand(CanExecute = nameof(CanBuild))]
    private void SelectBuild() => SelectedMode = AnyPS5Mode.Build;

    [RelayCommand]
    private void Back() => _navigator.GoBack();

    [RelayCommand]
    private void Continue() => _navigator.GoTo(_pages.SystemCheck(SelectedMode));
}
