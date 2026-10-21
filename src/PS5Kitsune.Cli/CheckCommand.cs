using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.Cli;

internal static class CheckCommand
{
    public static Command Create(Option<DirectoryInfo?> rootOption)
    {
        var command = new Command("check", "Check that this PC can run AnyPS5: CPU, Vulkan driver and GPU features, memory, disk and data folder.");
        command.SetAction((parseResult, cancellationToken) => CommandHost.RunAsync(parseResult.GetValue(rootOption), "check", async (services, logger) =>
        {
            var report = await services.GetRequiredService<ISystemChecker>().RunAsync(cancellationToken);
            CheckReportPrinter.Print(logger, report, onlyProblems: false);
            CheckReportPrinter.PrintGpus(logger, report);
            logger.CheckVerdict(report.Verdict);
            return report.HasBlocking ? CheckReportPrinter.BlockingExitCode : 0;
        }));
        return command;
    }
}
