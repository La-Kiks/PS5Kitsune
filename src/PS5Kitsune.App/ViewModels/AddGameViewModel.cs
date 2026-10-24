using Microsoft.Extensions.Logging;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.App.ViewModels;

/// <summary>Runs the add steps when shown; owns the prepared game and disposes it when the add stops or the user leaves.</summary>
public sealed partial class AddGameViewModel : StepRunViewModel, IPreparedGameOwner
{
    private readonly IGameImporter _importer;
    private readonly PreparedGame _game;
    private readonly bool _toIntel;
    private readonly Navigator _navigator;
    private readonly IPages _pages;
    private GameAdded? _added;

    public AddGameViewModel(
        IGameImporter importer, PreparedGame game, bool toIntel, string logFile, IPlatform platform, IUiDispatcher ui, Navigator navigator, IPages pages, Action<bool>? busyChanged = null, ILogger? logger = null)
        : base(logFile, platform, ui, busyChanged, logger)
    {
        _importer = importer;
        _game = game;
        _toIntel = toIntel;
        _navigator = navigator;
        _pages = pages;
        foreach (var name in StepNames())
        {
            Steps.Add(new InstallStepViewModel(name));
        }
    }

    public string Title => $"Adding {_game.Metadata?.Name ?? "the game"}";

    public override string? QuitMessage { get; } = "Adding a game is in progress. Quitting cancels it; adding it again resumes where it stopped.";

    /// <summary>Copying, unwrapping and converting report nothing measurable.</summary>
    protected override bool IndeterminateOnStart => true;

    public Task ReleaseAsync() => _game.DisposeAsync().AsTask();

    protected override IReadOnlyList<string> StepNames() => _importer.StepNames;

    protected override async Task RunCoreAsync(IProgress<StepProgress> progress, Action<StepEvent> onEvent, CancellationToken cancellationToken) =>
        _added = await _importer.AddAsync(_game, _toIntel, progress, onEvent, cancellationToken).ConfigureAwait(true);

    protected override (string Summary, string? Details) SplitFailure(Exception exception) => exception switch
    {
        AnyPS5NotInstalledException => ("AnyPS5 is not installed. Install it from Home first.", null),
        StepFailedException { InnerException: GameImportException failure } failed =>
            ($"{failed.StepName} failed: {failure.Summary}", string.IsNullOrEmpty(failure.Details) ? null : failure.Details),
        StepFailedException failed => ($"{failed.StepName} failed: {failed.InnerException?.Message ?? failed.Message}", null),
        _ => ($"Adding the game failed: {exception.Message}", null),
    };

    protected override bool CanRetryAfter(Exception exception) => exception is not AnyPS5NotInstalledException;

    protected override async Task OnSucceededAsync()
    {
        await _game.DisposeAsync().ConfigureAwait(true);
        _navigator.GoTo(_pages.AddResult(_added!));
    }

    protected override async Task OnCancelledAsync()
    {
        await _game.DisposeAsync().ConfigureAwait(true);
        _navigator.GoHome(_pages.Home());
    }

    protected override async Task LeaveAsync()
    {
        await _game.DisposeAsync().ConfigureAwait(true);
        _navigator.GoHome(_pages.Home());
    }
}
