using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Launch;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.App.ViewModels;

/// <summary>
/// A game's page: launches it (downloading Lavapipe and the fonts first when needed), follows its log live, Stop, then its exit code and, after an error AnyPS5 is known to print, its diagnosis.
/// Leaving the page never stops the game; leaving during a download cancels the launch.
/// </summary>
public sealed partial class GameViewModel : ObservableObject, INavigationAware, ILeavable
{
    /// <summary>Log lines kept on screen; the file keeps them all.</summary>
    public const int KeptLines = 500;

    private readonly GameLayout _game;
    private readonly GamePlayer _player;
    private readonly IPlatform _platform;
    private readonly IUiDispatcher _ui;
    private readonly Navigator _navigator;
    private readonly IPages _pages;
    private readonly ILogger<GameViewModel> _logger;
    private PlayingGame? _playing;
    private CancellationTokenSource? _following;
    private CancellationTokenSource? _launching;
    private bool _started;
    private readonly DiagnosisServices? _diagnosis;
    private bool _stopped;
    private int _launches;
    private bool _hasLogAnnounced;

    /// <summary>A game found running: the page follows it.</summary>
    public GameViewModel(PlayingGame playing, bool firstLaunch, GamePlayer player, IPlatform platform, IUiDispatcher ui, Navigator navigator, IPages pages, ILogger<GameViewModel>? logger = null, DiagnosisServices? diagnosis = null)
        : this(playing.Game, playing.Record.Name, firstLaunch, player, platform, ui, navigator, pages, logger, diagnosis)
    {
        _playing = playing;
        IsPreparing = false;
        ShowRunning(playing);
    }

    /// <summary>A game to launch: the page launches it when shown.</summary>
    public GameViewModel(GameLayout game, string name, bool firstLaunch, GamePlayer player, IPlatform platform, IUiDispatcher ui, Navigator navigator, IPages pages, ILogger<GameViewModel>? logger = null, DiagnosisServices? diagnosis = null)
    {
        _logger = logger ?? NullLogger<GameViewModel>.Instance;
        _diagnosis = diagnosis;
        _game = game;
        _player = player;
        _platform = platform;
        _ui = ui;
        _navigator = navigator;
        _pages = pages;
        Title = name;
        FirstLaunchHint = firstLaunch ? "The first launch may stutter while shaders are prepared." : null;
        IsPreparing = true;
        Log.CollectionChanged += (_, _) => AnnounceHasLog();
    }

    public string Title { get; }

    public ObservableCollection<string> Log { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLaunchAgain))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand), nameof(LaunchAgainCommand))]
    public partial bool IsRunning { get; private set; }

    /// <summary>Downloading or starting: Cancel stops the launch.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLaunchAgain))]
    [NotifyCanExecuteChangedFor(nameof(CancelLaunchCommand), nameof(LaunchAgainCommand))]
    public partial bool IsPreparing { get; private set; }

    /// <summary>The download's progress from 0 to 1; 0 when unknown.</summary>
    [ObservableProperty]
    public partial double DownloadValue { get; private set; }

    /// <summary>True unless a download with a known size is running.</summary>
    [ObservableProperty]
    public partial bool IsDownloadIndeterminate { get; private set; } = true;

    /// <summary>The log has at least one line; until then the page says where the game's output will appear.</summary>
    public bool HasLog => Log.Count > 0;

    // Announced only when it flips, so a log line does not make the view re-evaluate the whole page.
    private void AnnounceHasLog()
    {
        if (HasLog == _hasLogAnnounced)
        {
            return;
        }

        _hasLogAnnounced = HasLog;
        OnPropertyChanged(nameof(HasLog));
    }

    public bool CanLaunchAgain => !IsRunning && !IsPreparing;

    /// <summary>"Starting…", a download, "Running (PID n)", "Exited with code n", "Launch cancelled" or "Not running".</summary>
    [ObservableProperty]
    public partial string StatusText { get; private set; } = "Starting…";

    [ObservableProperty]
    public partial string? FirstLaunchHint { get; private set; }

    /// <summary>The fonts warning of the current launch; null otherwise.</summary>
    [ObservableProperty]
    public partial string? FontsWarning { get; private set; }

    /// <summary>Why the last action failed; null otherwise.</summary>
    [ObservableProperty]
    public partial string? Message { get; private set; }

    /// <summary>The known error of the last exit this page watched; null otherwise, and once Launch again starts.</summary>
    [ObservableProperty]
    public partial DiagnosisViewModel? Diagnosis { get; private set; }

    /// <summary>Completes when the current launch has started the game, failed or been cancelled.</summary>
    public Task Launching { get; private set; } = Task.CompletedTask;

    /// <summary>Completes when the page stops following the current process: it exited, or the page was left.</summary>
    public Task Following { get; private set; } = Task.CompletedTask;

    public void OnNavigatedTo()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        if (_playing is { } playing)
        {
            Following = FollowAsync(playing);
        }
        else
        {
            Launching = LaunchAsync();
        }
    }

    private void ShowRunning(PlayingGame playing)
    {
        IsRunning = true;
        StatusText = string.Create(CultureInfo.InvariantCulture, $"Running (PID {playing.Process.ProcessId})");
    }

    // Launches, then starts following the game without waiting for it to exit.
    private async Task LaunchAsync()
    {
        _launches++;
        _stopped = false;
        Diagnosis = null;
        var previousStatus = StatusText;
        Message = null;
        FontsWarning = null;
        IsPreparing = true;
        StatusText = "Starting…";
        using var cancellation = new CancellationTokenSource();
        _launching = cancellation;
        PlayingGame playing;
        try
        {
            try
            {
                var progress = new DispatchedProgress<LaunchProgress>(_ui, ShowDownload);
                playing = await Task.Run(() => _player.LaunchAsync(_game.TitleId, progress, cancellation.Token), CancellationToken.None).ConfigureAwait(true);
            }
            catch (GameRunningException)
            {
                // Started elsewhere (the CLI, say) since the Library refreshed: follow it as if this page had launched it.
                var attached = await Task.Run(() => _player.Attach(_game.TitleId)).ConfigureAwait(true);
                if (attached is null)
                {
                    Message = $"{Title} is no longer running.";
                    StatusText = _playing is null ? "Not running" : previousStatus;
                    return;
                }

                playing = attached;
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Launch cancelled";
            return;
        }
        catch (Exception exception)
        {
            var mapped = LaunchErrors.Describe(exception, Title);
            Message = mapped ?? $"Could not launch {Title}: {exception.Message}";
            // A refused relaunch keeps the exit code on screen.
            StatusText = _playing is null ? "Not running" : previousStatus;
            if (mapped is null)
            {
                var titleId = _game.TitleId;
                LogLaunchFailed(exception, titleId);
            }

            return;
        }
        finally
        {
            // Only now, so Launch again cannot start while an attach is still running.
            _launching = null;
            IsPreparing = false;
            DownloadValue = 0;
            IsDownloadIndeterminate = true;
        }

        _playing?.Process.Dispose();
        _playing = playing;
        Log.Clear();
        FontsWarning = playing.FontsWarning;
        ShowRunning(playing);
        Following = FollowAsync(playing);
    }

    private void ShowDownload(LaunchProgress value)
    {
        // A report can arrive after the launch moved on.
        if (!IsPreparing)
        {
            return;
        }

        if (value.Download == LaunchDownload.Waiting)
        {
            StatusText = "Waiting for another launch to finish its downloads…";
            DownloadValue = 0;
            IsDownloadIndeterminate = true;
            return;
        }

        var what = value.Download == LaunchDownload.Lavapipe ? "Lavapipe" : "fonts";
        var received = value.Progress.ReceivedBytes >> 20;
        IsDownloadIndeterminate = value.Progress.TotalBytes is not > 0;
        DownloadValue = value.Progress.TotalBytes is > 0 and var known ? (double)value.Progress.ReceivedBytes / known : 0;
        StatusText = value.Progress.TotalBytes is { } total
            ? string.Create(CultureInfo.InvariantCulture, $"Downloading {what}… {received} MB / {total >> 20} MB")
            : string.Create(CultureInfo.InvariantCulture, $"Downloading {what}… {received} MB");
    }

    private async Task FollowAsync(PlayingGame playing)
    {
        using var cancellation = new CancellationTokenSource();
        _following = cancellation;
        var cancellationToken = cancellation.Token;
        var exit = playing.Process.WaitForExitAsync(cancellationToken);
        try
        {
            try
            {
                await Task.Run(
                    () => LogTail.FollowAsync(playing.Process.LogFile, line => _ui.Post(() => AddLine(line)), exit, cancellationToken),
                    cancellationToken).ConfigureAwait(true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Message = $"Cannot follow the log: {exception.Message}";
            }

            var exitCode = await exit.ConfigureAwait(true);
            _player.Forget(playing);
            IsRunning = false;
            StatusText = string.Create(CultureInfo.InvariantCulture, $"Exited with code {exitCode}");
            if (_diagnosis is { } services)
            {
                var launch = _launches;
                var stopped = _stopped;
                var logFile = playing.Process.LogFile;
                try
                {
                    var found = await Task.Run(() => services.Diagnoser.FinishAsync(logFile, exitCode, stopped, CancellationToken.None), CancellationToken.None)
                        .ConfigureAwait(true);

                    // A launch started meanwhile owns the page.
                    if (found is not null && launch == _launches)
                    {
                        Diagnosis = new DiagnosisViewModel(found, _game, Title, playing.Record.Settings, _player, _platform, services);
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // A failed diagnosis must not surface from Back.
                    var titleId = _game.TitleId;
                    LogDiagnosisFailed(exception, titleId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The page was left: the game keeps running and stays recorded.
        }
        finally
        {
            // Only this follow's own token: a newer launch may have replaced it meanwhile.
            if (_following == cancellation)
            {
                _following = null;
            }
        }
    }

    private void AddLine(string line)
    {
        Log.Add(line);
        while (Log.Count > KeptLines)
        {
            Log.RemoveAt(0);
        }
    }

    [RelayCommand(CanExecute = nameof(IsPreparing))]
    private void CancelLaunch() => _launching?.Cancel();

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private async Task StopAsync()
    {
        if (_playing is not { } playing)
        {
            return;
        }

        Message = null;
        try
        {
            _stopped = true;
            await playing.Process.StopAsync().ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _stopped = false;
            Message = $"Could not stop {Title}: {exception.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanLaunchAgain))]
    private Task LaunchAgainAsync()
    {
        FirstLaunchHint = null;
        Launching = LaunchAsync();
        return Launching;
    }

    /// <summary>Stops any launch and stops following the game; the game itself keeps running.</summary>
    public async Task<bool> LeaveAsync()
    {
        if (_launching is { } launching)
        {
            await launching.CancelAsync().ConfigureAwait(true);
        }

        // Leaving must not depend on how the launch ended: an unexpected error is not rethrown here.
        await Launching.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
        if (_following is { } following)
        {
            await following.CancelAsync().ConfigureAwait(true);
        }

        await Following.ConfigureAwait(true);
        _playing?.Process.Dispose();
        return true;
    }

    [RelayCommand]
    private async Task BackToLibraryAsync()
    {
        await LeaveAsync().ConfigureAwait(true);
        _navigator.GoHome(_pages.Library());
    }

    [RelayCommand]
    private void OpenFolder() => Message = PathOpener.TryOpen(_platform, _game.Root);

    [RelayCommand]
    private void OpenSettings() => _navigator.GoTo(_pages.GameSettings(_game.TitleId));

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not launch {TitleId}.")]
    private partial void LogLaunchFailed(Exception exception, string titleId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not diagnose the exit of {TitleId}.")]
    private partial void LogDiagnosisFailed(Exception exception, string titleId);
}
