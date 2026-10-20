using System.Text.Json;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.AnyPS5.Steps;

/// <summary>Writes install.json last. Done when it describes this profile's install.</summary>
public sealed class WriteInstallRecordStep(AnyPS5Profile profile, AnyPS5Paths paths, TimeProvider time) : IStep
{
    public string Name => "Record the install";

    public bool IsAlreadyDone()
    {
        try
        {
            return JsonFiles.Read<AnyPS5InstallRecord>(paths.RecordFile) is { } record
                && record with { InstalledAt = default } == Expected(default);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken) =>
        JsonFiles.WriteAsync(paths.RecordFile, Expected(time.GetLocalNow()), cancellationToken);

    private AnyPS5InstallRecord Expected(DateTimeOffset installedAt) => new(
        AnyPS5InstallRecord.CurrentSchemaVersion,
        profile.Id,
        profile.Source.Kind,
        profile.Source.Version,
        profile.AssetUrl(ProfileAssetKeys.UnwrapScript),
        installedAt);
}
