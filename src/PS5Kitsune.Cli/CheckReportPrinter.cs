using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.Cli;

internal static class CheckReportPrinter
{
    /// <summary>Exit code of check and install when a check is blocking.</summary>
    public const int BlockingExitCode = 2;

    /// <param name="onlyProblems">True to print Warning and Blocking results only (install).</param>
    public static void Print(ILogger logger, SystemCheckReport report, bool onlyProblems)
    {
        foreach (var result in report.Results)
        {
            if (onlyProblems && result.Status < CheckStatus.Warning)
            {
                continue;
            }

            var title = result.Title;
            var summary = result.Summary;
            switch (result.Status)
            {
                case CheckStatus.Blocking:
                    logger.CheckBlocking(title, summary);
                    break;
                case CheckStatus.Warning:
                    logger.CheckWarning(title, summary);
                    break;
                default:
                    var badge = result.Status == CheckStatus.Ok ? "OK" : "INFO";
                    logger.CheckPassed(badge, title, summary);
                    break;
            }

            if (result.Action is { } action)
            {
                var text = ActionText(result, action);
                logger.CheckAction(text);
            }
        }
    }

    private static string ActionText(CheckResult result, string action) =>
        result.Fix == CheckFix.OpenPerformanceSettings ? $"{action} Run SystemPropertiesPerformance.exe." : action;

    public static void PrintGpus(ILogger logger, SystemCheckReport report)
    {
        foreach (var gpu in report.Vulkan.Report?.Gpus ?? [])
        {
            var description = gpu.Describe();
            logger.GpuFound(description);
        }
    }
}
