using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.Games.Steps;

/// <summary>
/// Reconvert's last step: discards the set-aside conversion, then rewrites game.json with the install in use and the new audit.
/// A crash between the two leaves the new conversion with the old record: the game only shows as needing another Reconvert.
/// </summary>
public sealed class UpdateGameRecordStep(GameImportContext context, GameRecord previous, AnyPS5InstallRecord install, TimeProvider time) : IStep
{
    public const string StepName = WriteGameRecordStep.StepName;

    public string Name => StepName;

    public bool IsAlreadyDone() => false;

    public async Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        var audit = context.Audit ?? throw new InvalidOperationException("The audit must run before the game is recorded.");
        cancellationToken.ThrowIfCancellationRequested();
        ConversionBackup.Discard(context.Game);
        var record = previous with
        {
            ConvertedWith = new ConversionInfo(install.Profile, InstallModes.Name(install.Mode), install.Version, install.Commit, previous.ConvertedWith.ToIntel),
            Audit = new AuditRecord(audit.Implemented, audit.Stub, audit.Absent, audit.Verdict),
            ReconvertedAt = time.GetLocalNow(),
        };
        // Not cancellable: once the old conversion is discarded, a cancel could no longer restore it.
        await GameRecordStore.WriteAsync(context.Game.RecordFile, record, CancellationToken.None).ConfigureAwait(false);
    }
}
