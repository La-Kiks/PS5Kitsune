using System.Text.Json;
using PS5Kitsune.Core.AnyPS5.Steps;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.AnyPS5;

/// <summary>Installs AnyPS5 into anyps5\installs\&lt;mode&gt;: Python, then the steps of the mode's source, then install.json; then makes it active.</summary>
public sealed class AnyPS5Installer(
    IEnumerable<IAnyPS5Source> sources,
    IPythonRuntime python,
    AnyPS5Paths paths,
    StepRunner stepRunner,
    TimeProvider time,
    AnyPS5Profile? profile = null) : IAnyPS5Installer
{
    public AnyPS5Mode? RequestedMode => paths.RequestedMode;

    public AnyPS5Mode? ActiveMode => paths.ActiveMode;

    public AnyPS5Mode CurrentMode => RequestedMode ?? ActiveMode ?? AnyPS5Mode.Release;

    public bool Supports(AnyPS5Mode mode) => Source(mode) is not null;

    public IReadOnlyList<IStep> CreateSteps(AnyPS5Mode mode)
    {
        var source = Source(mode) ?? throw new InvalidOperationException(UnavailableMessage(mode));
        return [new PreparePythonStep(python), .. source.CreateSteps(), new WriteInstallRecordStep(source, paths.For(mode), time)];
    }

    internal string UnavailableMessage(AnyPS5Mode mode) => mode switch
    {
        AnyPS5Mode.Build when profile?.Os == ProfileOs.Linux => "Build mode is not available on Linux yet; use release mode.",
        AnyPS5Mode.Build => "Mode build is not available: the active profile has no build section or no build tools.",
        _ => "Mode release is not available.",
    };

    /// <summary>True when there is an active install and every one of its steps is done.</summary>
    public bool IsInstalled() => ActiveMode is { } active && GetState(active).Status == AnyPS5InstallStatus.Installed;

    /// <summary>A mode the profile does not support is Incomplete, so a screen can show a build install whatever the profile.</summary>
    public AnyPS5InstallState GetState(AnyPS5Mode mode)
    {
        var record = ReadRecord(mode);
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

    public AnyPS5InstallRecord? ReadRecord() => ActiveMode is { } active ? ReadRecord(active) : null;

    public AnyPS5InstallRecord? ReadRecord(AnyPS5Mode mode)
    {
        try
        {
            return JsonFiles.Read<AnyPS5InstallRecord>(paths.For(mode).RecordFile);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void MigrateLegacyInstall() => AnyPS5Migration.Run(paths);

    public void Use(AnyPS5Mode mode)
    {
        if (!Supports(mode))
        {
            throw new InvalidOperationException(UnavailableMessage(mode));
        }

        var name = InstallModes.Name(mode);
        if (RequestedMode == mode)
        {
            throw new InvalidOperationException($"The {name} install did not finish; run install --mode {name} to resume it.");
        }

        if (ReadRecord(mode) is null)
        {
            throw new InvalidOperationException($"The {name} install is not finished; run install --mode {name} first.");
        }

        paths.WriteActiveMode(mode);
    }

    /// <summary>
    /// Writes the requested mode first and deletes it once every step succeeded and the mode is active, so an install
    /// that fails or is cancelled resumes in the same mode and leaves the active install untouched.
    /// </summary>
    public async Task InstallAsync(AnyPS5Mode mode, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default)
    {
        var steps = CreateSteps(mode);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.RequestedModeFile)!);
        await File.WriteAllTextAsync(paths.RequestedModeFile, InstallModes.Name(mode), cancellationToken).ConfigureAwait(false);
        await stepRunner.RunAsync(steps, progress, onEvent, cancellationToken).ConfigureAwait(false);
        paths.WriteActiveMode(mode);
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
