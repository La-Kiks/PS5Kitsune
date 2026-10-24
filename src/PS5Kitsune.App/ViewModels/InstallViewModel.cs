using Microsoft.Extensions.Logging;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.AnyPS5.Build;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.App.ViewModels;

/// <summary>Runs the AnyPS5 install when the page is shown; see <see cref="StepRunViewModel"/>.</summary>
public sealed partial class InstallViewModel : StepRunViewModel
{
    private readonly IAnyPS5Installer _installer;
    private readonly AnyPS5Mode _mode;
    private readonly Navigator _navigator;
    private readonly IPages _pages;

    /// <param name="busyChanged">Told true when the install starts and false when it stops, so the data root cannot change meanwhile.</param>
    public InstallViewModel(IAnyPS5Installer installer, AnyPS5Mode mode, string logFile, IPlatform platform, IUiDispatcher ui, Navigator navigator, IPages pages, Action<bool>? busyChanged = null, ILogger? logger = null)
        : base(logFile, platform, ui, busyChanged, logger)
    {
        _installer = installer;
        _mode = mode;
        _navigator = navigator;
        _pages = pages;
        foreach (var name in StepNames())
        {
            Steps.Add(new InstallStepViewModel(name));
        }
    }

    public override string? QuitMessage { get; } = "An install is running. Quitting cancels it; it will resume where it stopped.";

    /// <summary>Git, CMake and extraction report nothing measurable for a while; release steps are quick downloads.</summary>
    protected override bool IndeterminateOnStart => _mode == AnyPS5Mode.Build;

    /// <summary>The installer's current steps, read again on every run.</summary>
    protected override IReadOnlyList<string> StepNames() => [.. _installer.CreateSteps(_mode).Select(step => step.Name)];

    protected override Task RunCoreAsync(IProgress<StepProgress> progress, Action<StepEvent> onEvent, CancellationToken cancellationToken) =>
        _installer.InstallAsync(_mode, progress, onEvent, cancellationToken);

    protected override (string Summary, string? Details) SplitFailure(Exception exception)
    {
        if (exception is StepFailedException failed)
        {
            var build = failed.InnerException as BuildCommandException;
            return ($"{failed.StepName} failed: {build?.Summary ?? failed.InnerException?.Message ?? failed.Message}",
                string.IsNullOrEmpty(build?.Details) ? null : build.Details);
        }

        return ($"The install failed: {exception.Message}", null);
    }

    protected override Task OnSucceededAsync()
    {
        Ui.Post(() => _navigator.GoTo(_pages.Done()));
        return Task.CompletedTask;
    }

    protected override Task OnCancelledAsync()
    {
        Ui.Post(() => _navigator.GoHome(_pages.Home()));
        return Task.CompletedTask;
    }

    protected override Task LeaveAsync()
    {
        _navigator.GoHome(_pages.Home());
        return Task.CompletedTask;
    }
}
