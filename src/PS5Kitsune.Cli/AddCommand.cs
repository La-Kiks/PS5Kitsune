using System.CommandLine;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Cli;

internal static class AddCommand
{
    public static Command Create(Option<DirectoryInfo?> rootOption)
    {
        var pathArgument = new Argument<string>("path") { Description = "Game folder or .zip, in the DOOM for PS5 format." };
        var command = new Command("add", "Validate a game, copy it into the games folder, convert it and audit its imports.") { pathArgument };
        command.SetAction((parseResult, cancellationToken) => CommandHost.RunAsync(parseResult.GetValue(rootOption), "add", async (services, logger) =>
        {
            var vendor = CpuVendor.Detect();
            var toIntel = CpuVendor.IsIntel(vendor);
            var toIntelText = toIntel ? "yes" : "no";
            logger.CpuDetected(vendor, toIntelText);

            var importer = services.GetRequiredService<GameImporter>();
            var result = await importer.AddAsync(parseResult.GetRequiredValue(pathArgument), toIntel, new ConsoleStepProgress(), cancellationToken: cancellationToken);
            switch (result)
            {
                case GameRejected rejected:
                    Report(logger, rejected.Issues);
                    return 1;
                case GameAlreadyAdded already:
                    var alreadyTitleId = already.TitleId;
                    var alreadyDirectory = already.GameDirectory;
                    logger.AlreadyAdded(alreadyTitleId, alreadyDirectory);
                    return 1;
                case GameAdded added:
                    Report(logger, added.Warnings);
                    var record = added.Record;
                    var name = record.Name;
                    var titleId = record.TitleId;
                    var directory = added.GameDirectory;
                    logger.GameAdded(name, titleId, directory);
                    var audit = record.Audit;
                    var implemented = audit.Implemented;
                    var stub = audit.Stub;
                    var absent = audit.Absent;
                    logger.AuditCounts(implemented, stub, absent);
                    switch (audit.Verdict)
                    {
                        case AuditVerdict.Green:
                            logger.VerdictGreen();
                            break;
                        case AuditVerdict.Orange:
                            logger.VerdictOrange();
                            break;
                        default:
                            logger.VerdictRed();
                            break;
                    }

                    return 0;
                default:
                    throw new UnreachableException();
            }
        }));
        return command;
    }

    private static void Report(ILogger logger, IReadOnlyList<ValidationIssue> issues)
    {
        foreach (var issue in issues)
        {
            var message = issue.Message;
            if (issue.Severity == IssueSeverity.Blocking)
            {
                logger.BlockingIssue(message);
            }
            else
            {
                logger.WarningIssue(message);
            }
        }
    }
}
