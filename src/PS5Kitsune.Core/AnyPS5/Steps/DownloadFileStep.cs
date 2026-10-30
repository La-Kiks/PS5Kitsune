using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.AnyPS5.Steps;

/// <summary>
/// Downloads one file to its final place. Done when it is there with the expected hash and, for a program (<paramref name="executable"/>),
/// with its execute bit (Linux).
/// </summary>
public sealed class DownloadFileStep(string name, Uri url, string destination, string sha256, IDownloader downloader, bool executable = false) : IStep
{
    public string Name => name;

    public bool IsAlreadyDone() => FileHash.Matches(destination, sha256) && (!executable || ExecutableBits.Has(destination));

    public async Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        if (!FileHash.Matches(destination, sha256))
        {
            await downloader.DownloadAsync(new DownloadRequest(url, destination, sha256), StepProgress.ForDownload(progress), cancellationToken).ConfigureAwait(false);
        }

        if (executable)
        {
            ExecutableBits.Ensure(destination);
        }
    }
}
