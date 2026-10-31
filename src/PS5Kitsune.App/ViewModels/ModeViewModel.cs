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
    private readonly IAnyPS5Installer _installer;
    private readonly AnyPS5InstallRecord? _releaseRecord;
    private readonly AnyPS5InstallRecord? _buildRecord;
    private readonly AnyPS5Mode? _activeMode;
    private readonly AnyPS5Mode? _requestedMode;

    /// <summary>Preselects the mode the CLI would use without --mode: an unfinished install's, else the installed one's, else release.</summary>
    public ModeViewModel(AnyPS5Profile profile, IAnyPS5Installer installer, Navigator navigator, IPages pages)
    {
        _profile = profile;
        _navigator = navigator;
        _pages = pages;
        _installer = installer;
        BuildUnavailableText = installer.BuildUnavailableReason;
        _releaseRecord = installer.ReadRecord(AnyPS5Mode.Release);
        _buildRecord = installer.ReadRecord(AnyPS5Mode.Build);
        _activeMode = installer.ActiveMode;
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

    public string BuildUnavailableText { get; }

    /// <summary>"Installed: {version}" on the release card when the release install is finished; null otherwise.</summary>
    public string? ReleaseInstalledText => _releaseRecord is { } record ? $"Installed: {record.Version}" : null;

    /// <summary>"Installed: {version}" on the build card when the build install is finished; null otherwise.</summary>
    public string? BuildInstalledText => _buildRecord is { } record ? $"Installed: {record.Version}" : null;

    public bool IsReleaseInUse => _activeMode == AnyPS5Mode.Release;

    public bool IsBuildInUse => _activeMode == AnyPS5Mode.Build;

    /// <summary>Names what Continue does for the selected mode.</summary>
    public string ContinueText => _requestedMode == SelectedMode ? "Resume the install"
        : _activeMode == SelectedMode ? "Verify / repair"
        : SwitchesOnly ? "Use this install"
        : "Continue";

    /// <summary>The selected install is finished and not in use, and no install of it is pending: Continue only switches.</summary>
    private bool SwitchesOnly => _requestedMode != SelectedMode && _activeMode != SelectedMode
        && (SelectedMode == AnyPS5Mode.Release ? _releaseRecord : _buildRecord) is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContinueText))]
    [NotifyPropertyChangedFor(nameof(IsReleaseSelected))]
    [NotifyPropertyChangedFor(nameof(IsBuildSelected))]
    public partial AnyPS5Mode SelectedMode { get; private set; }

    /// <summary>Bound two-way to the release card: checking it selects Release; unchecking is ignored (the other card's check selects).</summary>
    public bool IsReleaseSelected
    {
        get => SelectedMode == AnyPS5Mode.Release;
        set
        {
            if (value)
            {
                SelectedMode = AnyPS5Mode.Release;
            }
        }
    }

    public bool IsBuildSelected
    {
        get => SelectedMode == AnyPS5Mode.Build;
        set
        {
            if (value && CanBuild)
            {
                SelectedMode = AnyPS5Mode.Build;
            }
        }
    }

    /// <summary>Why the last "Use this install" failed; null otherwise, and cleared when the selected card changes.</summary>
    [ObservableProperty]
    public partial string? ErrorText { get; private set; }

    partial void OnSelectedModeChanged(AnyPS5Mode value) => ErrorText = null;

    [RelayCommand]
    private void SelectRelease() => SelectedMode = AnyPS5Mode.Release;

    [RelayCommand(CanExecute = nameof(CanBuild))]
    private void SelectBuild() => SelectedMode = AnyPS5Mode.Build;

    [RelayCommand]
    private void Back() => _navigator.GoBack();

    [RelayCommand]
    private void Continue()
    {
        if (SwitchesOnly)
        {
            try
            {
                _installer.Use(SelectedMode);
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                ErrorText = $"Cannot switch to this install: {exception.Message}";
                return;
            }

            _navigator.GoHome(_pages.Home());
            return;
        }

        _navigator.GoTo(_pages.SystemCheck(SelectedMode));
    }
}
