using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
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
            if (!installer.IsInstalled())
            {
                throw new AnyPS5NotInstalledException();
            }

            var library = services.GetRequiredService<GameLibrary>();
            var (game, record) = library.Find(parseResult.GetRequiredValue(titleIdArgument));
            if (GameLock.IsHeld(game))
            {
                var reconvertedTitleId = record.TitleId;
                logger.ReconvertInProgress(reconvertedTitleId);
                return 1;
            }

            if (installer.ReadRecord() is { } install && GameLibrary.NeedsReconversion(record, install))
            {
                var convertedWith = ConversionLabels.Of(record.ConvertedWith);
                var inUse = ConversionLabels.Of(install);
                var titleId = record.TitleId;
                logger.NeedsReconversion(convertedWith, inUse, titleId);
            }
            var executable = Path.Combine(game.Root, record.Executable);
            if (!File.Exists(executable))
            {
                logger.ExecutableMissing(executable);
                return 1;
            }

            await library.MarkPlayedAsync(game, cancellationToken);
            using var launched = services.GetRequiredService<IGameLauncher>().Launch(new GameLaunchRequest(executable, game.Root, game.Logs));
            logger.GameStarted(record.Name, launched.ProcessId, launched.LogFile);

            var exit = launched.WaitForExitAsync(cancellationToken);
            try
            {
                await LogTail.FollowAsync(launched.LogFile, Console.WriteLine, exit, cancellationToken);
                var exitCode = await exit;
                logger.GameExited(record.Name, exitCode);
                return exitCode;
            }
            catch (OperationCanceledException)
            {
                // Ctrl+C stops following the log, not the game. Let the wait unregister before the handle is released.
                await exit.ContinueWith(static _ => { }, TaskScheduler.Default);
                logger.StoppedFollowing(record.Name, launched.ProcessId, launched.LogFile);
                return 130;
            }
        }));
        return command;
    }
}
