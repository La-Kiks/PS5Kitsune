using System.CommandLine;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Cli;

internal static class ListCommand
{
    public static Command Create(Option<DirectoryInfo?> rootOption)
    {
        var command = new Command("list", "List the added games: verdict, whether they need a reconvert or are running, and when they were last played.");
        command.SetAction((parseResult, _) => CommandHost.RunAsync(parseResult.GetValue(rootOption), "list", (services, logger) =>
        {
            var installer = services.GetRequiredService<AnyPS5Installer>();
            installer.MigrateLegacyInstall();
            var install = installer.ReadRecord();
            var games = services.GetRequiredService<GamePlayer>().List();
            if (games.Count == 0)
            {
                logger.NoGames();
            }

            foreach (var game in games)
            {
                var line = Line(game, install);
                logger.GameListed(line);
            }

            return Task.FromResult(0);
        }));
        return command;
    }

    private static string Line(LibraryGame game, AnyPS5InstallRecord? install)
    {
        var record = game.Record;
        var verdict = record.Audit.Verdict switch
        {
            AuditVerdict.Green => "green",
            AuditVerdict.Orange => "orange",
            _ => "red",
        };
        List<string> fields = [record.TitleId, record.Name, verdict];
        if (install is not null && GameLibrary.NeedsReconversion(record, install))
        {
            fields.Add("reconvert");
        }

        if (game.Running is { } running)
        {
            fields.Add(string.Create(CultureInfo.InvariantCulture, $"running (PID {running.ProcessId})"));
        }

        fields.Add(record.LastPlayedAt is { } played
            ? string.Create(CultureInfo.InvariantCulture, $"played {played.LocalDateTime:yyyy-MM-dd HH:mm}")
            : "never played");
        return string.Join("  ", fields);
    }
}
