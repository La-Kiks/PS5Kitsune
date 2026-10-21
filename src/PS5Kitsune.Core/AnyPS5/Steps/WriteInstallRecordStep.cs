using System.Text.Json;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.AnyPS5.Steps;

/// <summary>Writes install.json last. Done when it describes the source's install, whatever its schema version.</summary>
public sealed class WriteInstallRecordStep(IAnyPS5Source source, AnyPS5Paths paths, TimeProvider time) : IStep
{
    public string Name => "Record the install";

    public bool IsAlreadyDone()
    {
        try
        {
            var expected = source.CreateRecord(default);
            return JsonFiles.Read<AnyPS5InstallRecord>(paths.RecordFile) is { } record
                && record with { InstalledAt = default, SchemaVersion = expected.SchemaVersion } == expected;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken) =>
        JsonFiles.WriteAsync(paths.RecordFile, source.CreateRecord(time.GetLocalNow()), cancellationToken);
}
