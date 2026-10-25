using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core.Diagnostics;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Cli;

internal static class DiagnoseCommand
{
    public static Command Create(Option<DirectoryInfo?> rootOption)
    {
        var titleIdArgument = new Argument<string>("titleId") { Description = "Title ID of an added game, for example PPSA99666." };
        var reportOption = new Option<bool>("--report") { Description = "Print the text to paste in a new Game bring-up thread instead." };
        var command = new Command("diagnose", "Look for a known AnyPS5 error in a game's last log and say how to fix it.") { titleIdArgument, reportOption };
        command.SetAction((parseResult, cancellationToken) => CommandHost.RunAsync(parseResult.GetValue(rootOption), "diagnose", async (services, logger) =>
        {
            // An unknown title ID throws GameNotFoundException: CommandHost prints it and exits with 1.
            var (game, record) = services.GetRequiredService<GameLibrary>().Find(parseResult.GetRequiredValue(titleIdArgument));

            // Log names are yyyyMMdd-HHmmss.log: the last in ordinal order is the newest.
            var log = Directory.Exists(game.Logs) ? Directory.EnumerateFiles(game.Logs, "*.log").Order(StringComparer.Ordinal).LastOrDefault() : null;
            if (log is null)
            {
                var name = record.Name;
                logger.NoLogYet(name);
                return 1;
            }

            var diagnosis = await services.GetRequiredService<CrashDiagnoser>().DiagnoseAsync(log, null, cancellationToken);
            if (parseResult.GetValue(reportOption))
            {
                // Printed as is, without a log prefix, so it pastes cleanly.
                Console.Write(await services.GetRequiredService<CrashReporter>().BuildAsync(record.TitleId, log, diagnosis, cancellationToken));
                return 0;
            }

            if (diagnosis is null)
            {
                logger.NoKnownError(log);
                return 0;
            }

            foreach (var line in DiagnosisTexts.Lines(diagnosis, record.Name, record.TitleId))
            {
                logger.DiagnosisLine(line);
            }

            return 0;
        }));
        return command;
    }
}
