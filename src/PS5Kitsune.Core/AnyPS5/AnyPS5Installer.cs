using System.Text.Json;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5.Steps;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.AnyPS5;

/// <summary>
/// Installs the release of the active profile into anyps5/current. A single mode exists until build mode
/// (milestone 5), so there is no IAnyPS5Source yet.
/// </summary>
public sealed class AnyPS5Installer(
    AnyPS5Profile profile,
    AnyPS5Paths paths,
    DataLayout layout,
    PythonEmbeddableTool python,
    IDownloader downloader,
    StepRunner stepRunner,
    TimeProvider time) : IAnyPS5Installer
{
    public IReadOnlyList<IStep> CreateSteps()
    {
        var relinker = profile.Asset(ProfileAssetKeys.Relinker);
        var libs = profile.Asset(ProfileAssetKeys.Libs);
        var scripts = profile.Asset(ProfileAssetKeys.Scripts);
        var unwrapScript = profile.Asset(ProfileAssetKeys.UnwrapScript);
        return
        [
            new InstallPythonStep(python),
            new DownloadFileStep("Download the relinker", profile.AssetUrl(ProfileAssetKeys.Relinker), paths.Relinker, relinker.Sha256, downloader),
            new DownloadArchiveStep(
                "Download the libraries", profile.AssetUrl(ProfileAssetKeys.Libs), ArchivePath(ProfileAssetKeys.Libs, libs),
                libs.Sha256, paths.Libs, ZipExtraction.UnderPrefix("libs/"), downloader),
            new DownloadArchiveStep(
                "Download the scripts", profile.AssetUrl(ProfileAssetKeys.Scripts), ArchivePath(ProfileAssetKeys.Scripts, scripts),
                scripts.Sha256, paths.Scripts, ZipExtraction.StripTopFolder(scripts.Include ?? []), downloader),
            new DownloadFileStep("Download unwrap_self.py", profile.AssetUrl(ProfileAssetKeys.UnwrapScript), paths.UnwrapSelf, unwrapScript.Sha256, downloader),
            new WriteInstallRecordStep(profile, paths, time),
        ];
    }

    /// <summary>True when every install step is done: files present with the profile's hashes and install.json written.</summary>
    public bool IsInstalled() => CreateSteps().All(step => step.IsAlreadyDone());

    public AnyPS5InstallState GetState()
    {
        var steps = CreateSteps();
        var done = steps.Count(IsDone);
        var record = ReadRecord();
        var status = done == steps.Count ? AnyPS5InstallStatus.Installed
            : done == 0 && record is null ? AnyPS5InstallStatus.NotInstalled
            : AnyPS5InstallStatus.Incomplete;
        return new AnyPS5InstallState(status, record);
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

    private AnyPS5InstallRecord? ReadRecord()
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

    public Task InstallAsync(IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default) =>
        stepRunner.RunAsync(CreateSteps(), progress, onEvent, cancellationToken);

    private string ArchivePath(string key, ProfileAsset asset) =>
        Path.Combine(layout.Downloads, $"anyps5-{key}-{asset.Sha256[..12]}.zip");
}
