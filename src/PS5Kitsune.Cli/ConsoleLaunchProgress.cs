using PS5Kitsune.Core.Launch;

namespace PS5Kitsune.Cli;

/// <summary>Prints which download a launch waits for, then its progress every 10 %.</summary>
internal sealed class ConsoleLaunchProgress : IProgress<LaunchProgress>
{
    private LaunchDownload? _current;
    private ConsoleDownloadProgress _progress = new();

    public void Report(LaunchProgress value)
    {
        if (value.Download != _current)
        {
            _current = value.Download;
            _progress = new ConsoleDownloadProgress();
            Console.WriteLine(value.Download == LaunchDownload.Lavapipe ? "Downloading Lavapipe…" : "Downloading fonts…");
        }

        _progress.Report(value.Progress);
    }
}
