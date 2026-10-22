using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Sessions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Settings;

namespace PS5Kitsune.App.ViewModels;

/// <summary>The first page: where AnyPS5 stands on the current data root, and the choice of that root.</summary>
public sealed partial class HomeViewModel(SessionHost sessions, AppSettingsStore settings, IDialogService dialogs, Navigator navigator, IPages pages)
    : ObservableObject, INavigationAware
{
    private int _generation;

    [ObservableProperty]
    public partial AnyPS5InstallStatus Status { get; private set; }

    [ObservableProperty]
    public partial string StateTitle { get; private set; } = "";

    [ObservableProperty]
    public partial string StateDetail { get; private set; } = "";

    [ObservableProperty]
    public partial string ActionText { get; private set; } = "";

    [ObservableProperty]
    public partial string DataRoot { get; private set; } = "";

    [ObservableProperty]
    public partial string ProfileText { get; private set; } = "";

    /// <summary>Why the chosen folder cannot be used; null otherwise.</summary>
    [ObservableProperty]
    public partial string? RootError { get; private set; }

    /// <summary>True while the install is being checked: the done-checks can hash hundreds of MB.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    public partial bool IsChecking { get; private set; }

    public bool CanStart => !IsChecking;

    public void OnNavigatedTo() => _ = RefreshAsync();

    /// <summary>Reads the install state of the current root on a worker thread; a result that arrives after a newer refresh started is dropped.</summary>
    public async Task RefreshAsync()
    {
        var generation = ++_generation;
        var session = sessions.Current;
        var profile = session.Get<AnyPS5Profile>();
        var installer = session.Get<IAnyPS5Installer>();
        DataRoot = session.Layout.Root;
        ProfileText = $"{profile.Id} ({(profile.Validated ? "validated" : "not validated yet")})";
        var mode = installer.CurrentMode;
        IsChecking = true;
        (StateTitle, StateDetail, ActionText) = ("Checking the AnyPS5 install…", "", "");

        AnyPS5InstallState state;
        try
        {
            state = await Task.Run(() => installer.GetState(mode)).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            if (generation == _generation)
            {
                (StateTitle, StateDetail, ActionText) = ("Cannot read the AnyPS5 install", exception.Message, "Install AnyPS5");
                IsChecking = false;
            }

            return;
        }

        if (generation != _generation)
        {
            return;
        }

        Apply(state, mode);
        IsChecking = false;
    }

    private void Apply(AnyPS5InstallState state, AnyPS5Mode mode)
    {
        Status = state.Status;
        (StateTitle, StateDetail, ActionText) = state.Status switch
        {
            AnyPS5InstallStatus.Installed => (
                state.Record is null ? "AnyPS5 is installed" : $"AnyPS5 {state.Record.Version} is installed",
                state.Record is { } record
                    ? string.Create(CultureInfo.InvariantCulture, $"Profile {record.Profile}, installed {record.InstalledAt.LocalDateTime:yyyy-MM-dd HH:mm}.")
                    : "",
                "Verify / repair"),
            AnyPS5InstallStatus.Incomplete => (
                "The AnyPS5 install is incomplete",
                "Resume it to finish: the steps already done are skipped.",
                "Resume the install"),
            _ => (
                "AnyPS5 is not installed",
                "Install it to convert and run games. It takes a few minutes and needs no compilation.",
                "Install AnyPS5"),
        };

        if (mode == AnyPS5Mode.Build)
        {
            StateTitle = state.Status == AnyPS5InstallStatus.Installed
                ? (state.Record is null ? "AnyPS5 is installed (built from source)" : $"AnyPS5 {state.Record.Version} is installed (built from source)")
                : "The AnyPS5 build is incomplete";
            if (state.Status != AnyPS5InstallStatus.Installed)
            {
                StateDetail = "Resume it to finish: the steps already done are skipped.";
                ActionText = "Resume the install";
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start() => navigator.GoTo(pages.Mode());

    [RelayCommand]
    private async Task ChangeRootAsync()
    {
        var chosen = await dialogs.PickFolderAsync("Choose the PS5Kitsune data folder", DataRoot).ConfigureAwait(true);
        if (chosen is null)
        {
            return;
        }

        string root;
        try
        {
            root = Path.GetFullPath(chosen);
            if (DataRoots.Prepare(root) is { } problem)
            {
                RootError = problem;
                return;
            }

            sessions.ChangeRoot(root);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            RootError = $"Cannot use {chosen}: {exception.Message}";
            return;
        }

        // The session is replaced first: a refused change must not leave a saved setting for a root that was not applied.
        RootError = null;
        try
        {
            await settings.SaveAsync(new AppSettings(AppSettings.CurrentSchemaVersion, root)).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            RootError = $"Cannot save the setting: {exception.Message}";
        }

        await RefreshAsync().ConfigureAwait(true);
    }
}
