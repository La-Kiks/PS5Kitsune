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
    public AnyPS5Mode? RequestedMode
    {
        get
        {
            try
            {
                var name = File.ReadAllText(paths.RequestedModeFile).Trim();
                return Enum.GetValues<AnyPS5Mode>().Where(mode => InstallModes.Name(mode) == name).Cast<AnyPS5Mode?>().FirstOrDefault();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
    }

    public AnyPS5Mode CurrentMode => RequestedMode ?? ReadRecord()?.Mode ?? AnyPS5Mode.Release;

    public bool Supports(AnyPS5Mode mode) => Source(mode) is not null;

    /// <summary>The mode's steps, after a "Remove the other mode's files" step when install.json records the other mode.</summary>
    public IReadOnlyList<IStep> CreateSteps(AnyPS5Mode mode)
    {
        var source = Source(mode)
            ?? throw new InvalidOperationException(mode == AnyPS5Mode.Build
                ? "Mode build is not available: the active profile has no build section or no build tools."
                : "Mode release is not available.");
        IStep[] clear = ReadRecord() is { } record && record.Mode != mode ? [new ClearOtherModeStep(mode, paths, ReadRecord)] : [];
        return [.. clear, new InstallToolStep("Install Python", python), .. source.CreateSteps(), new WriteInstallRecordStep(source, paths, time)];
    }

    /// <summary>True when every step of the current mode is done.</summary>
    public bool IsInstalled() => GetState(CurrentMode).Status == AnyPS5InstallStatus.Installed;

    /// <summary>A mode the profile does not support is Incomplete, so a screen can show a build install whatever the profile.</summary>
    public AnyPS5InstallState GetState(AnyPS5Mode mode)
    {
        var record = ReadRecord();
        if (Source(mode) is null)
        {
            return new AnyPS5InstallState(AnyPS5InstallStatus.Incomplete, record);
        }

        var steps = CreateSteps(mode);
        var done = steps.Count(IsDone);
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

    /// <summary>
    /// Writes the requested mode first and deletes it once every step succeeded, so an install that fails or is
    /// cancelled before install.json is written resumes in the same mode.
    /// </summary>
    public async Task InstallAsync(AnyPS5Mode mode, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default)
    {
        var steps = CreateSteps(mode);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.RequestedModeFile)!);
        await File.WriteAllTextAsync(paths.RequestedModeFile, InstallModes.Name(mode), cancellationToken).ConfigureAwait(false);
        await stepRunner.RunAsync(steps, progress, onEvent, cancellationToken).ConfigureAwait(false);
        File.Delete(paths.RequestedModeFile);
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

    private IAnyPS5Source? Source(AnyPS5Mode mode) => sources.FirstOrDefault(source => source.Mode == mode);
}
