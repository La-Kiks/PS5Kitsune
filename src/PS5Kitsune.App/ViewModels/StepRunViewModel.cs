using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.App.ViewModels;

/// <summary>
/// A page that runs steps when shown: step list, a progress bar for downloads and counters, a log built from the step events.
/// Events and progress arrive on worker threads and are posted to the UI thread in order. Install and Adding inherit it.
/// </summary>
public abstract partial class StepRunViewModel : ObservableObject, INavigationAware, IRunningWork
{
    private readonly string _logFile;
    private readonly IPlatform _platform;
    private readonly Action<bool>? _busyChanged;
    private CancellationTokenSource? _cancellation;
    private Task _running = Task.CompletedTask;
    private bool _started;

    /// <param name="busyChanged">Told true when the run starts and false when it stops, so the data root cannot change meanwhile.</param>
    protected StepRunViewModel(string logFile, IPlatform platform, IUiDispatcher ui, Action<bool>? busyChanged)
    {
        _logFile = logFile;
        _platform = platform;
        Ui = ui;
        _busyChanged = busyChanged;
    }

    public ObservableCollection<InstallStepViewModel> Steps { get; } = [];

    public ObservableCollection<string> Log { get; } = [];

    [ObservableProperty]
    public partial bool IsRunning { get; private set; }

    [ObservableProperty]
    public partial bool HasFailed { get; private set; }

    [ObservableProperty]
    public partial string? Failure { get; private set; }

    /// <summary>The tool output of a failed step; null otherwise.</summary>
    [ObservableProperty]
    public partial string? FailureDetails { get; private set; }

    /// <summary>False when retrying cannot help (for example AnyPS5 is not installed).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    public partial bool CanRetry { get; private set; } = true;

    /// <summary>A download, a counter, or a running step with neither yet.</summary>
    [ObservableProperty]
    public partial bool ShowsProgress { get; private set; }

    [ObservableProperty]
    public partial double ProgressPercent { get; private set; }

    /// <summary>True when a download has no length, or a step reports no counter yet.</summary>
    [ObservableProperty]
    public partial bool ProgressIndeterminate { get; private set; }

    [ObservableProperty]
    public partial string ProgressText { get; private set; } = "";

    [ObservableProperty]
    public partial string? OpenError { get; private set; }

    public abstract string? QuitMessage { get; }

    protected IUiDispatcher Ui { get; }

    /// <summary>Whether a step shows an indeterminate bar as soon as it starts.</summary>
    protected abstract bool IndeterminateOnStart { get; }

    protected abstract IReadOnlyList<string> StepNames();

    protected abstract Task RunCoreAsync(IProgress<StepProgress> progress, Action<StepEvent> onEvent, CancellationToken cancellationToken);

    protected abstract (string Summary, string? Details) SplitFailure(Exception exception);

    protected virtual bool CanRetryAfter(Exception exception) => true;

    /// <summary>Runs on the UI thread after a successful run.</summary>
    protected abstract Task OnSucceededAsync();

    /// <summary>Runs on the UI thread after a cancelled run.</summary>
    protected abstract Task OnCancelledAsync();

    /// <summary>"Back to home".</summary>
    protected abstract Task LeaveAsync();

    public void OnNavigatedTo()
    {
        if (!_started)
        {
            _ = RunAsync();
        }
    }

    /// <summary>Runs the steps; completed steps are skipped, so this also resumes after a failure.</summary>
    public Task RunAsync()
    {
        _started = true;
        _running = RunLoopAsync();
        return _running;
    }

    /// <summary>For closing the window: cancels the run and waits until it and its clean-up have finished.</summary>
    public async Task CancelAndWaitAsync()
    {
        _cancellation?.Cancel();
        await _running.ConfigureAwait(true);
    }

    private async Task RunLoopAsync()
    {
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        IsRunning = true;
        HasFailed = false;
        Failure = null;
        FailureDetails = null;
        CanRetry = true;
        RebuildSteps();

        _busyChanged?.Invoke(true);
        bool? succeeded = null;
        try
        {
            await RunCoreAsync(new UiProgress(this), stepEvent => Ui.Post(() => Apply(stepEvent)), cancellation.Token).ConfigureAwait(true);
            succeeded = true;
        }
        catch (OperationCanceledException)
        {
            succeeded = false;
        }
        catch (Exception exception)
        {
            var (summary, details) = SplitFailure(exception);
            var canRetry = CanRetryAfter(exception);
            Ui.Post(() => Fail(summary, details, canRetry));
        }
        finally
        {
            _cancellation = null;
            _busyChanged?.Invoke(false);
            Ui.Post(() =>
            {
                IsRunning = false;
                ShowsProgress = false;
            });
        }

        if (succeeded == true)
        {
            await OnSucceededAsync().ConfigureAwait(true);
        }
        else if (succeeded == false)
        {
            await OnCancelledAsync().ConfigureAwait(true);
        }
    }

    /// <summary>The list can be shorter on a retry (the other mode's files are gone), so the rows follow the current steps.</summary>
    private void RebuildSteps()
    {
        Steps.Clear();
        foreach (var name in StepNames())
        {
            Steps.Add(new InstallStepViewModel(name));
        }
    }

    private void Fail(string message, string? details, bool canRetry)
    {
        HasFailed = true;
        Failure = message;
        FailureDetails = details;
        CanRetry = canRetry;
    }

    private void Apply(StepEvent stepEvent)
    {
        Steps[stepEvent.Number - 1].State = stepEvent.Kind switch
        {
            StepEventKind.Started => InstallStepState.Running,
            StepEventKind.Skipped => InstallStepState.Skipped,
            StepEventKind.Completed => InstallStepState.Done,
            StepEventKind.Failed => InstallStepState.Failed,
            _ => InstallStepState.Cancelled,
        };
        Log.Add(stepEvent.Kind switch
        {
            StepEventKind.Started => $"{stepEvent.StepName}…",
            StepEventKind.Skipped => $"{stepEvent.StepName}: already done",
            StepEventKind.Completed => string.Create(CultureInfo.InvariantCulture, $"{stepEvent.StepName}: done in {stepEvent.Duration?.TotalSeconds ?? 0:0.0} s"),
            StepEventKind.Failed => $"{stepEvent.StepName}: failed: {stepEvent.Error?.Message}",
            _ => $"{stepEvent.StepName}: cancelled",
        });
        if (stepEvent.Kind == StepEventKind.Started && IndeterminateOnStart)
        {
            // Git, CMake and extraction report nothing measurable for a while: show that the step is working.
            ShowsProgress = true;
            ProgressIndeterminate = true;
            ProgressPercent = 0;
            ProgressText = stepEvent.StepName;
        }
        else if (stepEvent.Kind != StepEventKind.Started)
        {
            ShowsProgress = false;
        }
    }

    private void Apply(StepProgress progress)
    {
        if (progress.Message is { } message)
        {
            Log.Add(message);
        }

        if (progress.Download is { } download)
        {
            var total = download.TotalBytes ?? 0;
            ShowsProgress = true;
            ProgressIndeterminate = total <= 0;
            ProgressPercent = total > 0 ? download.ReceivedBytes * 100.0 / total : 0;
            ProgressText = total > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{download.ReceivedBytes / 1048576.0:0.0} / {total / 1048576.0:0.0} MB")
                : string.Create(CultureInfo.InvariantCulture, $"{download.ReceivedBytes / 1048576.0:0.0} MB");
        }

        if (progress.Fraction is { } fraction)
        {
            ShowsProgress = true;
            ProgressIndeterminate = false;
            ProgressPercent = fraction.Done * 100.0 / fraction.Total;
            ProgressText = string.Create(CultureInfo.InvariantCulture, $"{fraction.Done} / {fraction.Total} units");
        }
    }

    [RelayCommand]
    private void Cancel() => _cancellation?.Cancel();

    [RelayCommand(CanExecute = nameof(CanRetry))]
    private Task RetryAsync() => RunAsync();

    [RelayCommand]
    private Task BackHomeAsync() => LeaveAsync();

    [RelayCommand]
    private void OpenLog() => OpenError = PathOpener.TryOpen(_platform, _logFile);

    private sealed class UiProgress(StepRunViewModel owner) : IProgress<StepProgress>
    {
        public void Report(StepProgress value) => owner.Ui.Post(() => owner.Apply(value));
    }
}
