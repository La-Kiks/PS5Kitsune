using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Cli;

/// <summary>Prints step details and download progress, synchronously so lines stay in order.</summary>
internal sealed class ConsoleStepProgress : IProgress<StepProgress>
{
    private readonly ConsoleDownloadProgress _download = new();

    public void Report(StepProgress value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Message is not null)
        {
            Console.WriteLine($"  {value.Message}");
        }

        if (value.Download is { } download)
        {
            _download.Report(download);
        }
    }
}
