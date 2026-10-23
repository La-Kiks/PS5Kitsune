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

    /// <summary>The current page runs work or owns a prepared game: closing must go through <see cref="ConfirmCloseAsync"/>.</summary>
    public bool NeedsCloseHandling => Navigator.CurrentPage is IRunningWork { IsRunning: true } or IPreparedGameOwner;

    /// <summary>
    /// Asks before closing when the running work has a quit message; then cancels it and waits, then releases a prepared game.
    /// </summary>
    /// <returns>True when the window may close.</returns>
    public async Task<bool> ConfirmCloseAsync()
    {
        if (Navigator.CurrentPage is IRunningWork { IsRunning: true } work)
        {
            if (work.QuitMessage is { } message && !await _dialogs.ConfirmAsync("Quit PS5Kitsune?", message, "Quit").ConfigureAwait(true))
            {
                return false;
            }

            await work.CancelAndWaitAsync().ConfigureAwait(true);
        }

        if (Navigator.CurrentPage is IPreparedGameOwner owner)
        {
            await owner.ReleaseAsync().ConfigureAwait(true);
        }

        return true;
    }
}
