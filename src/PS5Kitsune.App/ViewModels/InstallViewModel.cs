using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.App.ViewModels;

/// <summary>
/// Runs the install when the page is shown: step list, download bar, a log built from the installer's events.
/// Events and progress arrive on worker threads and are posted to the UI thread in order.
/// </summary>
public sealed partial class InstallViewModel : ObservableObject, INavigationAware
{
    private readonly IAnyPS5Installer _installer;
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
    public InstallViewModel(IAnyPS5Installer installer, string logFile, IPlatform platform, IUiDispatcher ui, Navigator navigator, IPages pages, Action<bool>? busyChanged = null)
    {
        _installer = installer;
        _logFile = logFile;
        _platform = platform;
        _ui = ui;
        _navigator = navigator;
        _pages = pages;
        _busyChanged = busyChanged;
        Steps = [.. installer.CreateSteps().Select(step => new InstallStepViewModel(step.Name))];
    }

    public ObservableCollection<InstallStepViewModel> Steps { get; }

    public ObservableCollection<string> Log { get; } = [];

    [ObservableProperty]
    public partial bool IsRunning { get; private set; }

    [ObservableProperty]
    public partial bool HasFailed { get; private set; }

    [ObservableProperty]
    public partial string? Failure { get; private set; }

    [ObservableProperty]
    public partial bool IsDownloading { get; private set; }

    [ObservableProperty]
    public partial double DownloadPercent { get; private set; }

    /// <summary>True when the server sent no length.</summary>
    [ObservableProperty]
    public partial bool DownloadIndeterminate { get; private set; }

    [ObservableProperty]
    public partial string DownloadText { get; private set; } = "";

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
        foreach (var step in Steps)
        {
            step.State = InstallStepState.Pending;
        }

        _busyChanged?.Invoke(true);
        try
        {
            await _installer.InstallAsync(new UiProgress(this), stepEvent => _ui.Post(() => Apply(stepEvent)), cancellation.Token).ConfigureAwait(true);
            _ui.Post(() => _navigator.GoTo(_pages.Done()));
        }
        catch (OperationCanceledException)
        {
            _ui.Post(() => _navigator.GoHome(_pages.Home()));
        }
        catch (StepFailedException exception)
        {
            var message = $"{exception.StepName} failed: {exception.InnerException?.Message ?? exception.Message}";
            _ui.Post(() => Fail(message));
        }
        catch (Exception exception)
        {
            _ui.Post(() => Fail($"The install failed: {exception.Message}"));
        }
        finally
        {
            _cancellation = null;
            _busyChanged?.Invoke(false);
            _ui.Post(() =>
            {
                IsRunning = false;
                IsDownloading = false;
            });
        }
    }

    private void Fail(string message)
    {
        HasFailed = true;
        Failure = message;
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
        if (stepEvent.Kind != StepEventKind.Started)
        {
            IsDownloading = false;
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
            IsDownloading = true;
            DownloadIndeterminate = total <= 0;
            DownloadPercent = total > 0 ? download.ReceivedBytes * 100.0 / total : 0;
            DownloadText = total > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{download.ReceivedBytes / 1048576.0:0.0} / {total / 1048576.0:0.0} MB")
                : string.Create(CultureInfo.InvariantCulture, $"{download.ReceivedBytes / 1048576.0:0.0} MB");
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
