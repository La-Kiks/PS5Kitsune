using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core.Launch;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Cli;

internal static class SettingsCommand
{
    public static Command Create(Option<DirectoryInfo?> rootOption)
    {
        var titleIdArgument = new Argument<string>("titleId") { Description = "Title ID of an added game, for example PPSA99666." };
        var command = new Command("settings", "Show a game's settings and the environment variables they become at launch. Edit them in the app.") { titleIdArgument };
        command.SetAction((parseResult, _) => CommandHost.RunAsync(parseResult.GetValue(rootOption), "settings", (services, logger) =>
        {
            // An unknown title ID throws GameNotFoundException: CommandHost prints it and exits with 1.
            var (_, record) = services.GetRequiredService<GameLibrary>().Find(parseResult.GetRequiredValue(titleIdArgument));
            var lavapipe = services.GetService<ILavapipe>();
            var fonts = services.GetService<IFontPack>();
            var lines = GameSettingsReport.Lines(record.Settings, lavapipe?.Manifest, lavapipe?.IsInstalled() == true, lavapipe?.WhenMissing, fonts?.Folder, fonts?.IsInstalled() == true);
            foreach (var line in lines)
            {
                logger.SettingsLine(line);
            }

            return Task.FromResult(0);
        }));
        return command;
    }
}
