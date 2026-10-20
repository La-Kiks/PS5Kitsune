using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Games.Steps;

/// <summary>Writes game.json last: its presence means the game is added.</summary>
public sealed class WriteGameRecordStep(GameImportContext context, AnyPS5Profile profile, TimeProvider time) : IStep
{
    private const string IconPath = "app0/sce_sys/icon0.png";

    public string Name => "Record the game";

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
            ConvertedWith = new ConversionInfo(
                profile.Id,
                profile.Source.Kind,
                profile.Source.Version,
                profile.Source.Kind == "commit" ? profile.Source.Sha : null,
                context.ToIntel),
            Audit = new AuditRecord(audit.Implemented, audit.Stub, audit.Absent, audit.Verdict),
            AddedAt = time.GetLocalNow(),
        };
        return GameRecordStore.WriteAsync(context.Game.RecordFile, record, cancellationToken);
    }
}
