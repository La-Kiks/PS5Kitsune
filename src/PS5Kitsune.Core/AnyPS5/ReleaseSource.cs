using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5.Steps;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.AnyPS5;

/// <summary>The release of the profile's tag: relinker, libraries, scripts and unwrap_self.py, each checked against the profile's hash.</summary>
public sealed class ReleaseSource(AnyPS5Profile profile, AnyPS5Paths paths, DataLayout layout, IDownloader downloader) : IAnyPS5Source
{
    public AnyPS5Mode Mode => AnyPS5Mode.Release;

    public IReadOnlyList<IStep> CreateSteps()
    {
        var target = paths.For(AnyPS5Mode.Release);
        var relinker = profile.Asset(ProfileAssetKeys.Relinker);
        var libs = profile.Asset(ProfileAssetKeys.Libs);
        var scripts = profile.Asset(ProfileAssetKeys.Scripts);
        var unwrapScript = profile.Asset(ProfileAssetKeys.UnwrapScript);
        return
        [
            new DownloadFileStep("Download the relinker", profile.AssetUrl(ProfileAssetKeys.Relinker), target.Relinker, relinker.Sha256, downloader, executable: true),
            new DownloadArchiveStep(
                "Download the libraries", profile.AssetUrl(ProfileAssetKeys.Libs), ArchivePath(ProfileAssetKeys.Libs, libs),
                libs.Sha256, target.Libs, ZipExtraction.UnderPrefix("libs/"), downloader),
            new DownloadArchiveStep(
                "Download the scripts", profile.AssetUrl(ProfileAssetKeys.Scripts), ArchivePath(ProfileAssetKeys.Scripts, scripts),
                scripts.Sha256, target.Scripts, ZipExtraction.StripTopFolder(scripts.Include ?? []), downloader),
            new DownloadFileStep("Download unwrap_self.py", profile.AssetUrl(ProfileAssetKeys.UnwrapScript), target.UnwrapSelf, unwrapScript.Sha256, downloader),
        ];
    }

    public AnyPS5InstallRecord CreateRecord(DateTimeOffset installedAt) => new(
        AnyPS5InstallRecord.CurrentSchemaVersion,
        profile.Id,
        AnyPS5Mode.Release,
        profile.Source.Version,
        Commit: null,
        profile.AssetUrl(ProfileAssetKeys.UnwrapScript),
        installedAt);

    private string ArchivePath(string key, ProfileAsset asset) =>
        Path.Combine(layout.Downloads, $"anyps5-{key}-{asset.Sha256[..12]}.zip");
}
