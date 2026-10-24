using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Sessions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Settings;

namespace PS5Kitsune.App.ViewModels;

/// <summary>The first page: where AnyPS5 stands on the current data root, and the choice of that root.</summary>
public sealed partial class HomeViewModel(SessionHost sessions, AppSettingsStore settings, IDialogService dialogs, Navigator navigator, IPages pages)
    : ObservableObject, INavigationAware
{
    private int _generation;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddGameCommand))]
    [NotifyPropertyChangedFor(nameof(AddGameHint))]
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

    /// <summary>"Also installed: {mode} {version}" when the install not in use is finished too; null otherwise.</summary>
    [ObservableProperty]
    public partial string? OtherInstallText { get; private set; }

    /// <summary>True while the install is being checked: the done-checks can hash hundreds of MB.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(AddGameCommand), nameof(LibraryCommand))]
    [NotifyPropertyChangedFor(nameof(AddGameHint))]
    public partial bool IsChecking { get; private set; }

    /// <summary>True once a game is added: enables "Library".</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LibraryCommand))]
    public partial bool HasGames { get; private set; }

    public bool CanOpenLibrary => !IsChecking && HasGames;

    public bool CanStart => !IsChecking;

    public bool CanAddGame => !IsChecking && Status == AnyPS5InstallStatus.Installed;

    /// <summary>Why "Add a game" is disabled; null when it is enabled.</summary>
    public string? AddGameHint => CanAddGame ? null : "Install AnyPS5 first to add games.";

    public void OnNavigatedTo() => _ = RefreshAsync();

    /// <summary>Reads the install state of the current root on a worker thread; a result that arrives after a newer refresh started is dropped.</summary>
    public async Task RefreshAsync()
    {
        var generation = ++_generation;
        var session = sessions.Current;
        var profile = session.Get<AnyPS5Profile>();
        var installer = session.Get<IAnyPS5Installer>();
        var library = session.Get<GameLibrary>();
        DataRoot = session.Layout.Root;
        ProfileText = $"{profile.Id} ({(profile.Validated ? "validated" : "not validated yet")})";
        IsChecking = true;
        (StateTitle, StateDetail, ActionText) = ("Checking the AnyPS5 install…", "", "");
        OtherInstallText = null;

        HomeCheck check;
        try
        {
            check = await Task.Run(() => Check(installer, library)).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            if (generation == _generation)
            {
                (StateTitle, StateDetail, ActionText) = ("Cannot read the AnyPS5 install", exception.Message, "Install AnyPS5");
                HasGames = false;
                IsChecking = false;
            }

            return;
        }

        if (generation != _generation)
        {
            return;
        }

        Apply(check.State, check.Mode);
        ApplyBothInstalls(check);
        HasGames = check.HasGames;
        IsChecking = false;
    }

    /// <param name="Mode">The install described: the active one, else an unfinished one, else release.</param>
    /// <param name="Other">The record of the other mode's install; null when it never finished or no install is active.</param>
    /// <param name="HasGames">At least one game is added.</param>
    private sealed record HomeCheck(AnyPS5Mode Mode, AnyPS5InstallState State, AnyPS5Mode? Active, AnyPS5Mode? Requested, AnyPS5InstallRecord? Other,
        bool HasGames);

    /// <summary>Runs on a worker thread: the migration moves a folder, and the done-checks can hash hundreds of MB.</summary>
    private static HomeCheck Check(IAnyPS5Installer installer, GameLibrary library)
    {
        installer.MigrateLegacyInstall();
        var active = installer.ActiveMode;
        var requested = installer.RequestedMode;
        var mode = active ?? requested ?? AnyPS5Mode.Release;
        var other = active is { } activeMode ? installer.ReadRecord(OtherMode(activeMode)) : null;
        var state = installer.GetState(mode);
        return new HomeCheck(mode, state, active, requested, other, library.HasGames());
    }

    private static AnyPS5Mode OtherMode(AnyPS5Mode mode) => mode == AnyPS5Mode.Release ? AnyPS5Mode.Build : AnyPS5Mode.Release;

    /// <summary>An unfinished install of the other mode is offered for resuming; a finished one is listed.</summary>
    private void ApplyBothInstalls(HomeCheck check)
    {
        if (check.Active is not { } active)
        {
            return;
        }

        if (check.Requested is { } requested && requested != active)
        {
            StateDetail = $"The {InstallModes.Name(requested)} install did not finish.";
            ActionText = "Resume the install";
            return;
        }

        if (check.Other is { } other)
        {
            OtherInstallText = $"Also installed: {InstallModes.Name(other.Mode)} {other.Version}";
        }
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

    [RelayCommand(CanExecute = nameof(CanAddGame))]
    private void AddGame() => navigator.GoTo(pages.ChooseGame());

    [RelayCommand(CanExecute = nameof(CanOpenLibrary))]
    private void Library() => navigator.GoHome(pages.Library());

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
