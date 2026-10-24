using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.Games.Steps;

/// <summary>Writes game.json last: its presence means the game is added. It names the install that converted the game.</summary>
public sealed class WriteGameRecordStep(GameImportContext context, AnyPS5InstallRecord install, TimeProvider time) : IStep
{
    private const string IconPath = "app0/sce_sys/icon0.png";

    public const string StepName = "Record the game";

    public string Name => StepName;

    public bool IsAlreadyDone() => File.Exists(context.Game.RecordFile);

    public Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        var audit = context.Audit ?? throw new InvalidOperationException("The audit must run before the game is recorded.");
        var record = new GameRecord
        {
            SchemaVersion = GameRecord.CurrentSchemaVersion,
            TitleId = context.Metadata.TitleId,
            Name = context.Metadata.Name,
            Executable = context.Game.ExecutableName,
            Icon = File.Exists(Path.Combine(context.Game.Root, IconPath)) ? IconPath : null,
            ConvertedWith = new ConversionInfo(install.Profile, InstallModes.Name(install.Mode), install.Version, install.Commit, context.ToIntel),
            Audit = new AuditRecord(audit.Implemented, audit.Stub, audit.Absent, audit.Verdict),
            AddedAt = time.GetLocalNow(),
        };
        return GameRecordStore.WriteAsync(context.Game.RecordFile, record, cancellationToken);
    }
}
