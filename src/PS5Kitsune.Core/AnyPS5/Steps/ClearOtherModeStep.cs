using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.AnyPS5.Steps;

/// <summary>
/// Empties anyps5\current when install.json records the other mode: the release zip has libTracyClient.dll, a build
/// does not. install.json goes last, so an interrupted clear still knows there is something to clear.
/// </summary>
public sealed class ClearOtherModeStep(AnyPS5Mode mode, AnyPS5Paths paths, Func<AnyPS5InstallRecord?> readRecord) : IStep
{
    public string Name => "Remove the other mode's files";

    public bool IsAlreadyDone() => readRecord() is not { } record || record.Mode == mode;

    public Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(paths.RootDirectory))
        {
            return Task.CompletedTask;
        }

        var recordFile = Path.GetFullPath(paths.RecordFile);
        foreach (var entry in Directory.EnumerateFileSystemEntries(paths.RootDirectory).ToList())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(Path.GetFullPath(entry), recordFile, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Directory.Exists(entry))
            {
                Directory.Delete(entry, recursive: true);
            }
            else
            {
                File.Delete(entry);
            }
        }

        File.Delete(paths.RecordFile);
        return Task.CompletedTask;
    }
}
