using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Diagnostics;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Cli;

internal static class RunCommand
{
    public static Command Create(Option<DirectoryInfo?> rootOption)
    {
        var titleIdArgument = new Argument<string>("titleId") { Description = "Title ID of an added game, for example PPSA99666." };
        var command = new Command("run", "Launch an added game from its folder and follow its log until it exits.") { titleIdArgument };
        command.SetAction((parseResult, cancellationToken) => CommandHost.RunAsync(parseResult.GetValue(rootOption), "run", async (services, logger) =>
        {
            var installer = services.GetRequiredService<AnyPS5Installer>();
            installer.MigrateLegacyInstall();
            var player = services.GetRequiredService<GamePlayer>();

            // Ctrl+C during a download cancels the launch; LavapipeUnavailableException is printed by CommandHost (exit code 1).
            // A running game, a busy game or a missing executable throws too: CommandHost prints the message and exits with 1.
            var playing = await player.LaunchAsync(parseResult.GetRequiredValue(titleIdArgument), new ConsoleLaunchProgress(), cancellationToken);
            var record = playing.Record;
            using var launched = playing.Process;
            if (installer.ReadRecord() is { } install && GameLibrary.NeedsReconversion(record, install))
            {
                var convertedWith = ConversionLabels.Of(record.ConvertedWith);
                var inUse = ConversionLabels.Of(install);
                var titleId = record.TitleId;
                logger.NeedsReconversion(convertedWith, inUse, titleId);
            }

            if (playing.FontsWarning is { } warning)
            {
                logger.FontsMissing(warning);
            }

            logger.GameStarted(record.Name, launched.ProcessId, launched.LogFile);
            var exit = launched.WaitForExitAsync(cancellationToken);
            try
            {
                await LogTail.FollowAsync(launched.LogFile, Console.WriteLine, exit, cancellationToken);
                var exitCode = await exit;
                player.Forget(playing);
                if (exitCode is { } code)
                {
                    logger.GameExited(record.Name, ExitCodes.Describe((uint)code));
                }
                else
                {
                    logger.GameExitedUnknown(record.Name);
                }

                // The exit is seen: it goes to the log, then the log is searched. Ctrl+C below saw no exit and writes nothing.
                var diagnosis = await services.GetRequiredService<CrashDiagnoser>().FinishAsync(launched.LogFile, exitCode, stopped: false, CancellationToken.None);
                if (diagnosis is not null)
                {
                    foreach (var line in DiagnosisTexts.Lines(diagnosis, record.Settings, record.Name, record.TitleId))
                    {
                        logger.DiagnosisLine(line);
                    }
                }

                return exitCode ?? 0;
            }
            catch (OperationCanceledException)
            {
                // Ctrl+C stops following the log, not the game; running.json stays, so the app finds the game.
                // Let the wait unregister before the handle is released.
                await exit.ContinueWith(static _ => { }, TaskScheduler.Default);
                logger.StoppedFollowing(record.Name, launched.ProcessId, launched.LogFile);
                return 130;
            }
        }));
        return command;
    }
}
