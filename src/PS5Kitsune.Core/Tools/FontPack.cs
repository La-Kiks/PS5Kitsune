using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Launch;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Tools;

/// <summary>
/// The profile's Noto fonts in &lt;root&gt;/fonts. The marker, written last, lists the files and hashes it was installed from,
/// so a profile with other fonts downloads again; files already there with the right hash are not downloaded twice.
/// </summary>
public sealed class FontPack(AnyPS5Profile profile, DataLayout layout, IDownloader downloader) : IFontPack
{
    internal const string MarkerFileName = ".complete";

    public string Folder => layout.Fonts;

    private string Marker => Path.Combine(Folder, MarkerFileName);

    public bool IsInstalled() =>
        File.Exists(Marker)
        && File.ReadAllText(Marker) == Fingerprint()
        && profile.Fonts.All(font => File.Exists(Path.Combine(Folder, font.Name)));

    /// <summary>Reports the bytes received over the whole list.</summary>
    public async Task InstallAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (IsInstalled())
        {
            return;
        }

        Directory.CreateDirectory(Folder);
        File.Delete(Marker);
        var total = profile.Fonts.Sum(font => font.Size);
        long done = 0;
        foreach (var font in profile.Fonts)
        {
            var before = done;
            var fileProgress = progress is null ? null : new Offset(progress, before, total);
            await downloader.DownloadAsync(new DownloadRequest(font.Url, Path.Combine(Folder, font.Name), font.Sha256), fileProgress, cancellationToken).ConfigureAwait(false);
            done += font.Size;
            progress?.Report(new DownloadProgress(done, total));
        }

        await File.WriteAllTextAsync(Marker, Fingerprint(), cancellationToken).ConfigureAwait(false);
    }

    private string Fingerprint() => string.Join('\n', profile.Fonts.Select(font => $"{font.Name} {font.Sha256}"));

    // One file's progress, as a part of the whole list.
    private sealed class Offset(IProgress<DownloadProgress> target, long before, long total) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => target.Report(new DownloadProgress(before + value.ReceivedBytes, total));
    }
}
