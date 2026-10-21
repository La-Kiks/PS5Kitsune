using System.Text.Json;
using PS5Kitsune.Core.AnyPS5.Steps;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.AnyPS5;

/// <summary>Installs AnyPS5 into anyps5/current: Python, then the steps of the mode's source, then install.json.</summary>
public sealed class AnyPS5Installer(
    IEnumerable<IAnyPS5Source> sources,
    PythonEmbeddableTool python,
    AnyPS5Paths paths,
    StepRunner stepRunner,
    TimeProvider time) : IAnyPS5Installer
{
    public AnyPS5Mode CurrentMode => ReadRecord()?.Mode ?? AnyPS5Mode.Release;

    public IReadOnlyList<IStep> CreateSteps(AnyPS5Mode mode)
    {
        var source = Source(mode);
        return [new InstallToolStep("Install Python", python), .. source.CreateSteps(), new WriteInstallRecordStep(source, paths, time)];
    }

    /// <summary>True when every step of the current mode is done.</summary>
    public bool IsInstalled() => GetState(CurrentMode).Status == AnyPS5InstallStatus.Installed;

    public AnyPS5InstallState GetState(AnyPS5Mode mode)
    {
        var steps = CreateSteps(mode);
        var done = steps.Count(IsDone);
        var record = ReadRecord();
        var status = done == steps.Count ? AnyPS5InstallStatus.Installed
            : done == 0 && record is null ? AnyPS5InstallStatus.NotInstalled
            : AnyPS5InstallStatus.Incomplete;
        return new AnyPS5InstallState(status, record);
    }

    public AnyPS5InstallRecord? ReadRecord()
    {
        try
        {
            return JsonFiles.Read<AnyPS5InstallRecord>(paths.RecordFile);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public Task InstallAsync(AnyPS5Mode mode, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default)
    {
        var steps = CreateSteps(mode);

        // The other mode's files must not linger: the release zip has libTracyClient.dll, a build does not.
        if (ReadRecord() is { } record && record.Mode != mode && Directory.Exists(paths.RootDirectory))
        {
            Directory.Delete(paths.RootDirectory, recursive: true);
        }

        return stepRunner.RunAsync(steps, progress, onEvent, cancellationToken);
    }

    /// <summary>A done-check that throws (a locked or unreadable file) counts as not done; the install reports the error.</summary>
    private static bool IsDone(IStep step)
    {
        try
        {
            return step.IsAlreadyDone();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private IAnyPS5Source Source(AnyPS5Mode mode) =>
        sources.FirstOrDefault(source => source.Mode == mode)
        ?? throw new InvalidOperationException(mode == AnyPS5Mode.Build
            ? "Mode build is not available: the active profile has no build section or no build tools."
            : "Mode release is not available.");
}
