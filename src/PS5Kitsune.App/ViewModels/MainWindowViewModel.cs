using CommunityToolkit.Mvvm.ComponentModel;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Sessions;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.App.ViewModels;

/// <summary>The window shell: a header with the current data root and profile, and the page the navigator shows.</summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly SessionHost _sessions;
    private readonly IPlatform _platform;
    private readonly IDialogService _dialogs;

    public MainWindowViewModel(SessionHost sessions, Navigator navigator, IPlatform platform, IDialogService dialogs)
    {
        _sessions = sessions;
        _platform = platform;
        _dialogs = dialogs;
        Navigator = navigator;
        sessions.SessionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(DataRoot));
            OnPropertyChanged(nameof(Profile));
        };
    }

    public Navigator Navigator { get; }

    public string Platform => _platform.Name;

    public string DataRoot => _sessions.Current.Layout.Root;

    public string Profile
    {
        get
        {
            var profile = _sessions.Current.Get<AnyPS5Profile>();
            return $"{profile.Id} ({(profile.Validated ? "validated" : "not validated yet")})";
        }
    }

    public bool IsInstallRunning => Navigator.CurrentPage is InstallViewModel { IsRunning: true };

    /// <summary>Asks before closing during an install; on yes, cancels it and waits for the running step to stop.</summary>
    /// <returns>True when the window may close.</returns>
    public async Task<bool> ConfirmCloseAsync()
    {
        if (Navigator.CurrentPage is not InstallViewModel { IsRunning: true } install)
        {
            return true;
        }

        var quit = await _dialogs.ConfirmAsync(
            "Quit PS5Kitsune?",
            "An install is running. Quitting cancels it; it will resume where it stopped.",
            "Quit").ConfigureAwait(true);
        if (!quit)
        {
            return false;
        }

        await install.CancelAndWaitAsync().ConfigureAwait(true);
        return true;
    }
}
