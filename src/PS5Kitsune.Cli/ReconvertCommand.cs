using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Cli;

internal static class ReconvertCommand
{
    public static Command Create(Option<DirectoryInfo?> rootOption)
    {
        var titleIdArgument = new Argument<string>("titleId") { Description = "Title ID of an added game, for example PPSA99666." };
        var command = new Command(
            "reconvert",
            "Convert an added game again with the AnyPS5 in use: new libraries, executable and audit. Saves and settings are kept; a failure puts the previous conversion back.")
        {
            titleIdArgument,
        };
        command.SetAction((parseResult, cancellationToken) => CommandHost.RunAsync(parseResult.GetValue(rootOption), "reconvert", async (services, logger) =>
        {
            var installer = services.GetRequiredService<AnyPS5Installer>();
            installer.MigrateLegacyInstall();
            var titleId = parseResult.GetRequiredValue(titleIdArgument);
            if (installer.IsInstalled() && installer.ReadRecord() is { } install)
            {
                var (_, current) = services.GetRequiredService<GameLibrary>().Find(titleId);
                if (!GameLibrary.NeedsReconversion(current, install))
                {
                    var conversion = ConversionLabels.Of(install);
                    logger.AlreadyConverted(conversion);
                }
            }

            GameReconverted result;
            try
            {
                result = await services.GetRequiredService<GameReconverter>().ReconvertAsync(titleId, new ConsoleStepProgress(), cancellationToken: cancellationToken);
            }
            catch (StepFailedException exception)
            {
                var message = exception.Message;
                logger.Failed(exception, message);
                logger.PreviousRestored();
                return 1;
            }
            catch (OperationCanceledException)
            {
                logger.PreviousRestored();
                throw;
            }
            catch (ReconvertRestoreException exception)
            {
                var message = exception.Message;
                logger.Failed(exception, message);
                logger.PreviousNotRestored(exception.RestoreFailure);
                return 1;
            }

            var record = result.Record;
            var name = record.Name;
            var reconvertedTitleId = record.TitleId;
            var directory = result.GameDirectory;
            logger.GameReconverted(name, reconvertedTitleId, directory);
            VerdictReport.Print(logger, record.Audit);
            var before = result.PreviousAudit;
            var implemented = before.Implemented;
            var stub = before.Stub;
            var absent = before.Absent;
            logger.AuditBefore(implemented, stub, absent);
            return 0;
        }));
        return command;
    }
}
