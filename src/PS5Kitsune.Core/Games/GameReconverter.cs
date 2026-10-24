using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games.Steps;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.Games;

/// <summary>
/// Reconvert: sets the conversion outputs aside, runs the add's libs, unwrap, relink and audit steps on the game folder, then
/// rewrites game.json. app0 is not copied again, and nothing the game created is touched. See docs/architecture.md, "V2 — Add a game".
/// </summary>
public sealed partial class GameReconverter(
    GameLibrary library,
    AnyPS5Paths paths,
    AnyPS5Profile profile,
    AnyPS5Installer installer,
    PythonEmbeddableTool python,
    IProcessRunner processRunner,
    StepRunner stepRunner,
    TimeProvider time,
    ILogger<GameReconverter> logger) : IGameReconverter
{
    public static readonly IReadOnlyList<string> Names =
        [SetAsideStep.StepName, CopyLibsStep.StepName, UnwrapSelfStep.StepName, RelinkStep.StepName, AuditStep.StepName, UpdateGameRecordStep.StepName];

    public IReadOnlyList<string> StepNames => Names;

    public async Task<GameReconverted> ReconvertAsync(
        string titleId, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(titleId);
        if (!installer.IsInstalled())
        {
            throw new AnyPS5NotInstalledException();
        }

        var install = installer.ReadRecord() ?? throw new AnyPS5NotInstalledException();
        var layout = library.Layout(titleId);
        using var held = ReconvertLock.TryAcquire(layout) ?? throw new ReconvertInProgressException(titleId);
        // Find skips the restore while the lock is held, ours included: restore a Reconvert that did not finish here, so this run
        // starts from the last complete conversion.
        try
        {
            ConversionBackup.Restore(layout);
        }
        catch (Exception restoreFailure) when (restoreFailure is IOException or UnauthorizedAccessException)
        {
            LogRestoreFailed(restoreFailure, layout.Root);
            throw new ReconvertRestoreException(restoreFailure, restoreFailure);
        }

        var (game, record) = library.Find(titleId);
        List<string> modules = [.. GameValidator.ModuleDirectoryNames.Where(name => Directory.Exists(Path.Combine(game.App0, name)))];
        var context = new GameImportContext(
            new GameSource(game.App0, null), new GameMetadata(record.TitleId, record.Name), modules, game, record.ConvertedWith.ToIntel);
        IStep[] steps =
        [
            new SetAsideStep(game),
            new CopyLibsStep(paths, game),
            new UnwrapSelfStep(context, paths, python, processRunner),
            new RelinkStep(context, paths, profile, processRunner),
            new AuditStep(context, paths, python, processRunner),
            new UpdateGameRecordStep(context, record, install, time),
        ];

        try
        {
            await stepRunner.RunAsync(steps, progress, onEvent, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is StepFailedException or OperationCanceledException)
        {
            try
            {
                ConversionBackup.Restore(game);
            }
            catch (Exception restoreFailure) when (restoreFailure is IOException or UnauthorizedAccessException)
            {
                LogRestoreFailed(restoreFailure, game.Root);
                throw new ReconvertRestoreException(exception, restoreFailure);
            }

            throw;
        }

        var updated = GameRecordStore.Read(game.RecordFile) ?? throw new InvalidOperationException($"{game.RecordFile} was not written.");
        return new GameReconverted(updated, game.Root, context.Audit ?? AuditSummary.Read(game.AuditFile), record.Audit);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not restore the previous conversion of {Game}; it will be restored on the next start or Reconvert.")]
    private partial void LogRestoreFailed(Exception exception, string game);
}
