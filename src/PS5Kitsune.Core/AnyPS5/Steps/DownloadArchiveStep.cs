using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.AnyPS5.Steps;

/// <summary>
/// Downloads a zip into downloads/ and extracts the entries <paramref name="map"/> keeps into <paramref name="target"/>.
/// Done when the target's marker holds the archive hash.
/// </summary>
public sealed class DownloadArchiveStep(
    string name, Uri url, string archive, string sha256, string target, Func<string, string?> map, IDownloader downloader) : IStep
{
    public string Name => name;

    public bool IsAlreadyDone() => ZipExtraction.IsExtracted(target, sha256);

    public async Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        await downloader.DownloadAsync(new DownloadRequest(url, archive, sha256), StepProgress.ForDownload(progress), cancellationToken).ConfigureAwait(false);
        await ZipExtraction.ExtractAsync(archive, target, sha256, map, cancellationToken).ConfigureAwait(false);
    }
}
