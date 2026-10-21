using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Pipeline;

/// <param name="Message">A line of detail, for example the file being unwrapped.</param>
/// <param name="Download">Progress of the file the step is downloading.</param>
/// <param name="Fraction">Progress counted in work units, for example compiled files.</param>
public sealed record StepProgress(string? Message = null, DownloadProgress? Download = null, StepFraction? Fraction = null)
{
    /// <summary>Forwards download progress to <paramref name="progress"/> synchronously, so console lines keep their order.</summary>
    public static IProgress<DownloadProgress>? ForDownload(IProgress<StepProgress>? progress) =>
        progress is null ? null : new DownloadForwarder(progress);

    private sealed class DownloadForwarder(IProgress<StepProgress> progress) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => progress.Report(new StepProgress(Download: value));
    }
}
