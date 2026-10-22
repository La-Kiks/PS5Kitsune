using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.AnyPS5.Build;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.App.ViewModels;

/// <summary>
/// Runs the install when the page is shown: step list, a progress bar for downloads and build counters, a log built from the installer's events.
/// Events and progress arrive on worker threads and are posted to the UI thread in order.
/// </summary>
public sealed partial class InstallViewModel : ObservableObject, INavigationAware
{
    private readonly IAnyPS5Installer _installer;
    private readonly AnyPS5Mode _mode;
    private readonly string _logFile;
    private readonly IPlatform _platform;
    private readonly IUiDispatcher _ui;
    private readonly Navigator _navigator;
    private readonly IPages _pages;
    private readonly Action<bool>? _busyChanged;
    private CancellationTokenSource? _cancellation;
    private Task _running = Task.CompletedTask;
    private bool _started;

    /// <param name="busyChanged">Told true when the install starts and false when it stops, so the data root cannot change meanwhile.</param>
    public InstallViewModel(IAnyPS5Installer installer, AnyPS5Mode mode, string logFile, IPlatform platform, IUiDispatcher ui, Navigator navigator, IPages pages, Action<bool>? busyChanged = null)
    {
        _installer = installer;
        _mode = mode;
        _logFile = logFile;
        _platform = platform;
        _ui = ui;
        _navigator = navigator;
        _pages = pages;
        _busyChanged = busyChanged;
        Steps = [];
        RebuildSteps();
    }

    public ObservableCollection<InstallStepViewModel> Steps { get; }

    public ObservableCollection<string> Log { get; } = [];

    [ObservableProperty]
    public partial bool IsRunning { get; private set; }

    [ObservableProperty]
    public partial bool HasFailed { get; private set; }

    [ObservableProperty]
    public partial string? Failure { get; private set; }

    /// <summary>A download, a build counter, or a running build step with neither yet.</summary>
    [ObservableProperty]
    public partial bool ShowsProgress { get; private set; }

    [ObservableProperty]
    public partial double ProgressPercent { get; private set; }

    /// <summary>True when a download has no length, or a build step reports no counter yet.</summary>
    [ObservableProperty]
    public partial bool ProgressIndeterminate { get; private set; }

    [ObservableProperty]
    public partial string ProgressText { get; private set; } = "";

    /// <summary>The exit code and last output lines of a failed build command; null otherwise.</summary>
    [ObservableProperty]
    public partial string? FailureDetails { get; private set; }

    [ObservableProperty]
    public partial string? OpenError { get; private set; }

    public void OnNavigatedTo()
    {
        if (!_started)
        {
            _ = RunAsync();
        }
    }

    /// <summary>Runs the install; completed steps are skipped, so this also resumes after a failure.</summary>
    public Task RunAsync()
    {
        _started = true;
        _running = RunCoreAsync();
        return _running;
    }

    /// <summary>For closing the window: cancels the install and waits until the running step has stopped.</summary>
    public async Task CancelAndWaitAsync()
    {
        _cancellation?.Cancel();
        await _running.ConfigureAwait(true);
    }

    private async Task RunCoreAsync()
    {
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        IsRunning = true;
        HasFailed = false;
        Failure = null;
        FailureDetails = null;
        RebuildSteps();

        _busyChanged?.Invoke(true);
        try
        {
            await _installer.InstallAsync(_mode, new UiProgress(this), stepEvent => _ui.Post(() => Apply(stepEvent)), cancellation.Token).ConfigureAwait(true);
            _ui.Post(() => _navigator.GoTo(_pages.Done()));
        }
        catch (OperationCanceledException)
        {
            _ui.Post(() => _navigator.GoHome(_pages.Home()));
        }
        catch (StepFailedException exception)
        {
            var build = exception.InnerException as BuildCommandException;
            var message = $"{exception.StepName} failed: {build?.Summary ?? exception.InnerException?.Message ?? exception.Message}";
            var details = string.IsNullOrEmpty(build?.Details) ? null : build.Details;
            _ui.Post(() => Fail(message, details));
        }
        catch (Exception exception)
        {
            _ui.Post(() => Fail($"The install failed: {exception.Message}", null));
        }
        finally
        {
            _cancellation = null;
            _busyChanged?.Invoke(false);
            _ui.Post(() =>
            {
                IsRunning = false;
                ShowsProgress = false;
            });
        }
    }

    /// <summary>The list can be shorter on a retry (the other mode's files are gone), so the rows follow the installer's current steps.</summary>
    private void RebuildSteps()
    {
        Steps.Clear();
        foreach (var step in _installer.CreateSteps(_mode))
        {
            Steps.Add(new InstallStepViewModel(step.Name));
        }
    }

    private void Fail(string message, string? details)
    {
        HasFailed = true;
        Failure = message;
        FailureDetails = details;
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
        if (stepEvent.Kind == StepEventKind.Started && _mode == AnyPS5Mode.Build)
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

    [RelayCommand]
    private Task RetryAsync() => RunAsync();

    [RelayCommand]
    private void BackHome() => _navigator.GoHome(_pages.Home());

    [RelayCommand]
    private void OpenLog() => OpenError = PathOpener.TryOpen(_platform, _logFile);

    private sealed class UiProgress(InstallViewModel owner) : IProgress<StepProgress>
    {
        public void Report(StepProgress value) => owner._ui.Post(() => owner.Apply(value));
    }
}
