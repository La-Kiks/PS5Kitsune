using PS5Kitsune.Core.Launch;

namespace PS5Kitsune.Cli;

/// <summary>Prints which download a launch waits for (or that it waits for another launch), then its progress every 10 %.</summary>
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
            Console.WriteLine(value.Download switch
            {
                LaunchDownload.Lavapipe => "Downloading Lavapipe…",
                LaunchDownload.Fonts => "Downloading fonts…",
                _ => "Waiting for another launch to finish its downloads…",
            });
        }

        if (value.Download != LaunchDownload.Waiting)
        {
            _progress.Report(value.Progress);
        }
    }
}
