using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Cli;

/// <summary>Prints step details, download progress and build progress, synchronously so lines stay in order.</summary>
internal sealed class ConsoleStepProgress : IProgress<StepProgress>
{
    private readonly ConsoleDownloadProgress _download = new();
    private int _fractionTotal = -1;
    private long _fractionTenth = -1;

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

        if (value.Fraction is { } fraction)
        {
            ReportFraction(fraction);
        }
    }

    private void ReportFraction(StepFraction fraction)
    {
        if (fraction.Total != _fractionTotal)
        {
            _fractionTotal = fraction.Total;
            _fractionTenth = -1;
        }

        var tenth = (long)fraction.Done * 10 / fraction.Total;
        if (tenth != _fractionTenth)
        {
            _fractionTenth = tenth;
            Console.WriteLine($"  {tenth * 10,3} %  ({fraction.Done} / {fraction.Total})");
        }
    }
}
