using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Cli;

/// <summary>Prints download progress every 10 %, synchronously so lines stay in order.</summary>
internal sealed class ConsoleDownloadProgress : IProgress<DownloadProgress>
{
    private long _lastPrintedTenth = -1;

    public void Report(DownloadProgress value)
    {
        if (value.TotalBytes is not > 0)
        {
            return;
        }

        var tenth = value.ReceivedBytes * 10 / value.TotalBytes.Value;
        if (tenth != _lastPrintedTenth)
        {
            _lastPrintedTenth = tenth;
            Console.WriteLine($"  {tenth * 10,3} %  ({value.ReceivedBytes / 1024 / 1024} / {value.TotalBytes.Value / 1024 / 1024} MiB)");
        }
    }
}
