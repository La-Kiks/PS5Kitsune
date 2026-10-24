using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Cli;

internal static class RemoveCommand
{
    public static Command Create(Option<DirectoryInfo?> rootOption)
    {
        var titleIdArgument = new Argument<string>("titleId") { Description = "Title ID of an added game, for example PPSA99666." };
        var yesOption = new Option<bool>("--yes") { Description = "Delete without asking." };
        var command = new Command("remove", "Delete an added game and its folder, saves included. Refused while the game runs.") { titleIdArgument, yesOption };
        command.SetAction((parseResult, _) => CommandHost.RunAsync(parseResult.GetValue(rootOption), "remove", (services, logger) =>
        {
            var titleId = parseResult.GetRequiredValue(titleIdArgument);
            var (game, record) = services.GetRequiredService<GameLibrary>().Find(titleId);
            var name = record.Name;
            if (!parseResult.GetValue(yesOption) && !Confirm(name, GameCleaner.HasSaves(game)))
            {
                logger.NothingRemoved();
                return Task.FromResult(1);
            }

            // A running or busy game throws: CommandHost prints the message and exits with 1.
            services.GetRequiredService<GameCleaner>().Remove(titleId);
            var directory = game.Root;
            logger.GameRemoved(name, titleId, directory);
            return Task.FromResult(0);
        }));
        return command;
    }

    /// <summary>Anything but y or yes, including no input (redirected stdin), keeps the game.</summary>
    private static bool Confirm(string name, bool hasSaves)
    {
        var saves = hasSaves ? " Its saves in data\\ are deleted too." : "";
        Console.Write($"Delete {name} and its folder?{saves} [y/N] ");
        var answer = Console.ReadLine()?.Trim();
        return string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase) || string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase);
    }
}
