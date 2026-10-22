using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core.AnyPS5;

namespace PS5Kitsune.Cli;

internal static class UseCommand
{
    public static Command Create(Option<DirectoryInfo?> rootOption)
    {
        var modeArgument = new Argument<AnyPS5Mode>("mode") { Description = "release or build: the finished install to use." };
        var command = new Command("use", "Switch to the release or build install, both already installed. Runs no step.") { modeArgument };
        command.SetAction((parseResult, cancellationToken) => CommandHost.RunAsync(parseResult.GetValue(rootOption), "use", (services, logger) =>
        {
            var installer = services.GetRequiredService<AnyPS5Installer>();
            installer.MigrateLegacyInstall();
            var mode = parseResult.GetValue(modeArgument);
            try
            {
                installer.Use(mode);
            }
            catch (InvalidOperationException exception)
            {
                logger.UseRefused(exception.Message);
                return Task.FromResult(1);
            }

            var paths = services.GetRequiredService<AnyPS5Paths>();
            var modeName = InstallModes.Name(mode);
            var version = installer.ReadRecord(mode)!.Version;
            var directory = paths.For(mode).RootDirectory;
            logger.InstallInUse(modeName, version, directory);
            return Task.FromResult(0);
        }));
        return command;
    }
}
