using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core.AnyPS5;
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

            // A running game, a busy game or a missing executable throws: CommandHost prints the message and exits with 1.
            var playing = await player.LaunchAsync(parseResult.GetRequiredValue(titleIdArgument), cancellationToken);
            var record = playing.Record;
            using var launched = playing.Process;
            if (installer.ReadRecord() is { } install && GameLibrary.NeedsReconversion(record, install))
            {
                var convertedWith = ConversionLabels.Of(record.ConvertedWith);
                var inUse = ConversionLabels.Of(install);
                var titleId = record.TitleId;
                logger.NeedsReconversion(convertedWith, inUse, titleId);
            }

            logger.GameStarted(record.Name, launched.ProcessId, launched.LogFile);
            var exit = launched.WaitForExitAsync(cancellationToken);
            try
            {
                await LogTail.FollowAsync(launched.LogFile, Console.WriteLine, exit, cancellationToken);
                var exitCode = await exit;
                player.Forget(playing);
                logger.GameExited(record.Name, exitCode);
                return exitCode;
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
