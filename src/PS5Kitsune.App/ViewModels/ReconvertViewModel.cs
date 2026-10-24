using Microsoft.Extensions.Logging;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.App.ViewModels;

/// <summary>Reconverts an added game when shown. Core puts the previous conversion back after a failure or a cancel.</summary>
public sealed partial class ReconvertViewModel : StepRunViewModel
{
    private const string Kept = "The previous conversion was kept; the game runs as before.";
    private const string NotRestored = "The previous conversion could not be restored yet; it will be restored on the next start or Reconvert.";

    private readonly IGameReconverter _reconverter;
    private readonly string _titleId;
    private readonly Navigator _navigator;
    private readonly IPages _pages;
    private GameReconverted? _reconverted;

    public ReconvertViewModel(
        IGameReconverter reconverter, string titleId, string name, string logFile, IPlatform platform, IUiDispatcher ui, Navigator navigator, IPages pages,
        Action<bool>? busyChanged = null, ILogger? logger = null)
        : base(logFile, platform, ui, busyChanged, logger)
    {
        _reconverter = reconverter;
        _titleId = titleId;
        _navigator = navigator;
        _pages = pages;
        Title = $"Reconverting {name}";
        foreach (var stepName in StepNames())
        {
            Steps.Add(new InstallStepViewModel(stepName));
        }
    }

    public string Title { get; }

    public override string? QuitMessage { get; } = "A Reconvert is in progress. Quitting cancels it and puts the previous conversion back.";

    /// <summary>Copying libraries, converting and auditing report nothing measurable.</summary>
    protected override bool IndeterminateOnStart => true;

    protected override IReadOnlyList<string> StepNames() => _reconverter.StepNames;

    protected override async Task RunCoreAsync(IProgress<StepProgress> progress, Action<StepEvent> onEvent, CancellationToken cancellationToken) =>
        _reconverted = await _reconverter.ReconvertAsync(_titleId, progress, onEvent, cancellationToken).ConfigureAwait(true);

    protected override (string Summary, string? Details) SplitFailure(Exception exception)
    {
        var (failure, outcome) = exception is ReconvertRestoreException { InnerException: { } inner } ? (inner, NotRestored) : (exception, Kept);
        var (summary, details) = Describe(failure);
        return (summary + Environment.NewLine + outcome, details);
    }

    protected override bool CanRetryAfter(Exception exception) => exception is not (AnyPS5NotInstalledException or GameNotFoundException);

    protected override Task OnSucceededAsync()
    {
        _navigator.GoTo(_pages.ReconvertResult(_reconverted!));
        return Task.CompletedTask;
    }

    protected override Task OnCancelledAsync()
    {
        _navigator.GoHome(_pages.Library());
        return Task.CompletedTask;
    }

    protected override Task LeaveAsync()
    {
        _navigator.GoHome(_pages.Library());
        return Task.CompletedTask;
    }

    private static (string Summary, string? Details) Describe(Exception exception)
    {
        if (exception is AnyPS5NotInstalledException)
        {
            return ("AnyPS5 is not installed. Install it from Home first.", null);
        }

        if (exception is StepFailedException { InnerException: GameImportException failure } failed)
        {
            return ($"{failed.StepName} failed: {failure.Summary}", string.IsNullOrEmpty(failure.Details) ? null : failure.Details);
        }

        if (exception is StepFailedException other)
        {
            return ($"{other.StepName} failed: {other.InnerException?.Message ?? other.Message}", null);
        }

        return ($"Reconverting the game failed: {exception.Message}", null);
    }
}
