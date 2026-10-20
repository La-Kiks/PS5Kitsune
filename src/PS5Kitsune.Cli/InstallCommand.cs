using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Cli;

internal static class InstallCommand
{
    public static Command Create(Option<DirectoryInfo?> rootOption)
    {
        var command = new Command("install", "Install the AnyPS5 release of the active profile: Python, relinker, libraries and scripts.");
        command.SetAction((parseResult, cancellationToken) => CommandHost.RunAsync(parseResult.GetValue(rootOption), "install", async (services, logger) =>
        {
            await services.GetRequiredService<AnyPS5Installer>().InstallAsync(new ConsoleStepProgress(), cancellationToken: cancellationToken);
            var version = services.GetRequiredService<AnyPS5Profile>().Source.Version;
            var directory = services.GetRequiredService<AnyPS5Paths>().RootDirectory;
            logger.AnyPS5Ready(version, directory);
            return 0;
        }));
        return command;
    }
}
