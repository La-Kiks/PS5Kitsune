using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Checks.Vulkan;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.App.ViewModels;

/// <param name="Name">The value saved in ANYPS5_GPU; null for Automatic.</param>
public sealed record GpuChoice(string? Name, string Label);

/// <summary>A setting's value with its label; <paramref name="Value"/> is null for "unset".</summary>
public sealed record SettingChoice(string? Value, string Label);

/// <summary>A game's settings, saved to game.json and applied at its next launch. See the 7b spec, "Settings page".</summary>
public sealed partial class GameSettingsViewModel(
    string titleId, GamePlayer player, GameLibrary library, RunningGames running, IVulkanProbeRunner probe, IDialogService dialogs, Navigator navigator)
    : ObservableObject, INavigationAware, ILeavable
{
    private static readonly GpuChoice Automatic = new(null, "Automatic");

    private static readonly SettingChoice[] PresentModeChoices =
        [new("fifo", "On"), new("fifo_latest_ready", "Latest frame"), new("mailbox", "Mailbox"), new("immediate", "Off, may tear")];

    private static readonly SettingChoice[] OverlayPositionChoices =
        [new("tl", "Top left"), new("tr", "Top right"), new("bl", "Bottom left"), new("br", "Bottom right")];

    private static readonly SettingChoice[] PlayGo =
        [new(null, "Default"), new("installed", "Installed (High on Life needs this)"), new("absent", "Absent (Sifu uses this)")];

    private GameSettings _saved = new();
    private string _name = "";
    private bool _loaded;
    private bool _readFailed;
    private IReadOnlyList<VulkanGpu> _detected = [];
    private bool _detectionDone;
    private CancellationTokenSource? _watch;

    [ObservableProperty]
    public partial string Title { get; private set; } = "Settings";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit), nameof(CanChooseGpu), nameof(CanChooseOverlayPosition))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(ResetCommand))]
    public partial bool IsLoading { get; private set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit), nameof(CanChooseGpu), nameof(CanChooseOverlayPosition))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(ResetCommand))]
    public partial bool IsRunning { get; private set; }

    public bool CanEdit => !IsLoading && !IsRunning && !_readFailed;

    public bool CanChooseGpu => CanEdit && !Lavapipe;

    public bool CanChooseOverlayPosition => CanEdit && Overlay;

    public string RunningNotice { get; } = "Running: changes can't be saved until it stops";

    public string LavapipeHelp { get; } = "Slow but works on any PC. Mesa (~70 MB) is downloaded on the first launch.";

    public string DozenHelp { get; } = "Dozen can crash while GPUs are listed. Lavapipe ignores this.";

    public string OverlayHelp { get; } = "F10 toggles it in game.";

    public string PlayGoHelp { get; } = "Only matters for dumps without PlayGo files.";

    public ObservableCollection<GpuChoice> Gpus { get; } = [Automatic];

    [ObservableProperty]
    public partial GpuChoice? SelectedGpu { get; set; } = Automatic;

    /// <summary>"Detecting GPUs…", why the detection failed, or null.</summary>
    [ObservableProperty]
    public partial string? GpuStatus { get; private set; } = "Detecting GPUs…";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChooseGpu))]
    public partial bool Lavapipe { get; set; }

    public IReadOnlyList<GameLanguage> Languages { get; } = GameLanguages.All;

    [ObservableProperty]
    public partial GameLanguage SelectedLanguage { get; set; } = GameLanguages.All[GameLanguages.Default];

    public IReadOnlyList<SettingChoice> PresentModes { get; } = PresentModeChoices;

    [ObservableProperty]
    public partial SettingChoice SelectedPresentMode { get; set; } = PresentModeChoices[0];

    [ObservableProperty]
    public partial bool ShowDisableDozen { get; private set; }

    [ObservableProperty]
    public partial bool DisableDozen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChooseOverlayPosition))]
    public partial bool Overlay { get; set; }

    public IReadOnlyList<SettingChoice> OverlayPositions { get; } = OverlayPositionChoices;

    [ObservableProperty]
    public partial SettingChoice SelectedOverlayPosition { get; set; } = OverlayPositionChoices[0];

    public IReadOnlyList<SettingChoice> PlayGoChoices { get; } = PlayGo;

    [ObservableProperty]
    public partial SettingChoice SelectedPlayGo { get; set; } = PlayGo[0];

    /// <summary>"Saved"; null otherwise.</summary>
    [ObservableProperty]
    public partial string? Notice { get; private set; }

    /// <summary>Why the page cannot read or save; null otherwise.</summary>
    [ObservableProperty]
    public partial string? Message { get; private set; }

    /// <summary>Completes once the settings are read and the GPUs detected.</summary>
    public Task Loading { get; private set; } = Task.CompletedTask;

    /// <summary>Completes when the page stops watching the running game: it exited, or the page was left.</summary>
    public Task Watching { get; private set; } = Task.CompletedTask;

    public void OnNavigatedTo()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        Loading = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var (record, isRunning) = await Task.Run(() =>
            {
                var (game, found) = library.Find(titleId);
                return (found, running.Check(game) is not null);
            }).ConfigureAwait(true);
            _name = record.Name;
            Title = $"{record.Name} settings";
            IsRunning = isRunning;
            if (isRunning)
            {
                StartWatching();
            }

            _saved = record.Settings;
            Apply(record.Settings);
        }
        catch (Exception exception) when (exception is GameNotFoundException or InvalidGameRecordException or IOException or UnauthorizedAccessException)
        {
            _readFailed = true;
            GpuStatus = null;
            Message = $"Cannot read the settings of {titleId}: {exception.Message}";
            IsLoading = false;
            return;
        }

        IsLoading = false;
        VulkanProbeOutcome outcome;
        try
        {
            outcome = await Task.Run(() => probe.RunAsync()).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            outcome = VulkanProbeOutcome.Failed(exception.Message);
        }

        _detectionDone = outcome.Report is not null;
        if (outcome.Report is { } report)
        {
            _detected = report.Gpus;
            GpuStatus = null;
            ShowDisableDozen = outcome.Status == VulkanProbeStatus.SucceededWithoutDozen
                || (report.Gpus.Any(gpu => gpu.IsDozen) && report.Gpus.Any(gpu => !gpu.IsDozen));
        }
        else
        {
            GpuStatus = $"Could not detect GPUs: {outcome.FailureReason}";
        }

        RebuildGpus(SelectedGpu?.Name);
    }

    // Automatic, each detected GPU, then the chosen one when it was not detected. Names compare exactly: that is what is saved.
    private void RebuildGpus(string? selected)
    {
        Gpus.Clear();
        Gpus.Add(Automatic);
        foreach (var gpu in _detected)
        {
            Gpus.Add(new GpuChoice(gpu.Name, gpu.SupportsAnyPS5 ? gpu.Name : $"{gpu.Name} (not supported)"));
        }

        if (selected is not null && !Gpus.Any(choice => choice.Name == selected))
        {
            Gpus.Add(new GpuChoice(selected, _detectionDone ? $"{selected} (not found)" : selected));
        }

        SelectedGpu = Gpus.First(choice => choice.Name == selected);
    }

    private void Apply(GameSettings settings)
    {
        RebuildGpus(settings.Gpu);
        Lavapipe = settings.Lavapipe;
        SelectedLanguage = Languages.First(language => language.Number == settings.Language);
        SelectedPresentMode = PresentModes.First(choice => choice.Value == settings.PresentMode);
        DisableDozen = settings.DisableDozen;
        Overlay = settings.Overlay;
        SelectedOverlayPosition = OverlayPositions.First(choice => choice.Value == settings.OverlayPosition);
        SelectedPlayGo = PlayGoChoices.First(choice => choice.Value == settings.PlayGoUnlistedChunks);
    }

    private const string RunningMessageEnd = " is running. Stop it to change its settings.";

    // Settings only apply at launch, so a running game makes the page read-only until it exits.
    private void StartWatching()
    {
        if (_watch is not null)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _watch = cancellation;
        Watching = WatchAsync(cancellation.Token);
    }

    private async Task WatchAsync(CancellationToken cancellationToken)
    {
        try
        {
            var playing = await Task.Run(() => player.Attach(titleId), cancellationToken).ConfigureAwait(true);
            if (playing is not null)
            {
                using (playing.Process)
                {
                    await playing.Process.WaitForExitAsync(cancellationToken).ConfigureAwait(true);
                }
            }

            IsRunning = false;
            if (Message?.EndsWith(RunningMessageEnd, StringComparison.Ordinal) == true)
            {
                Message = null;
            }
        }
        catch (OperationCanceledException)
        {
            // The page was left.
        }
        catch (Exception exception) when (exception is GameNotFoundException or InvalidGameRecordException or IOException or UnauthorizedAccessException)
        {
            // The game can no longer be read: the page stays read-only, as when it opened.
        }
        finally
        {
            _watch?.Dispose();
            _watch = null;
        }
    }

    // A hidden Disable Dozen keeps its saved value because nothing writes DisableDozen while it is hidden;
    // the `with` on _saved keeps nothing itself, as every property is overwritten.
    private GameSettings Current() => _saved with
    {
        Gpu = SelectedGpu?.Name,
        Lavapipe = Lavapipe,
        Language = SelectedLanguage.Number,
        PresentMode = SelectedPresentMode.Value!,
        DisableDozen = DisableDozen,
        Overlay = Overlay,
        OverlayPosition = SelectedOverlayPosition.Value!,
        PlayGoUnlistedChunks = SelectedPlayGo.Value,
    };

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task SaveAsync()
    {
        Message = null;
        Notice = null;
        var settings = Current();
        try
        {
            var saved = await Task.Run(() => player.SaveSettingsAsync(titleId, settings, CancellationToken.None)).ConfigureAwait(true);
            _saved = saved.Settings;
            Notice = "Saved";
        }
        catch (GameRunningException)
        {
            IsRunning = true;
            Message = $"{_name}{RunningMessageEnd}";
            StartWatching();
        }
        catch (GameBusyException)
        {
            Message = $"{_name} is busy with another action. Try again.";
        }
        catch (Exception exception) when (exception is GameNotFoundException or InvalidGameRecordException or IOException or UnauthorizedAccessException)
        {
            Message = $"Could not save the settings of {_name}: {exception.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Reset()
    {
        Notice = null;
        // A hidden Disable Dozen is not the user's to reset: keep its saved value.
        Apply(new GameSettings { DisableDozen = !ShowDisableDozen && DisableDozen });
    }

    /// <summary>Asks before discarding unsaved changes, then stops watching the game.</summary>
    public async Task<bool> LeaveAsync()
    {
        if (CanEdit && Current() != _saved
            && !await dialogs.ConfirmAsync("Discard changes?", $"Your changes to the settings of {_name} are not saved.", "Discard").ConfigureAwait(true))
        {
            return false;
        }

        if (_watch is { } watch)
        {
            await watch.CancelAsync().ConfigureAwait(true);
        }

        return true;
    }

    [RelayCommand]
    private async Task BackAsync()
    {
        if (await LeaveAsync().ConfigureAwait(true))
        {
            navigator.GoBack();
        }
    }
}
