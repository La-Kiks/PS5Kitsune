using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Cli;

internal static class InstallCommand
{
    public static Command Create(Option<DirectoryInfo?> rootOption)
    {
        var modeOption = new Option<AnyPS5Mode?>("--mode")
        {
            Description = "release: download the release. build: compile the profile's commit. Defaults to the mode of the current install, else release.",
        };
        var command = new Command("install", "Install AnyPS5 from the active profile: Python, relinker, libraries and scripts.") { modeOption };
        command.SetAction((parseResult, cancellationToken) => CommandHost.RunAsync(parseResult.GetValue(rootOption), "install", async (services, logger) =>
        {
            var installer = services.GetRequiredService<AnyPS5Installer>();
            var profile = services.GetRequiredService<AnyPS5Profile>();
            var context = services.GetRequiredService<CommandContext>();
            var choice = InstallModes.Resolve(parseResult.GetValue(modeOption), installer.RequestedMode, installer.ReadRecord());
            var root = services.GetRequiredService<DataLayout>().Root;
            var origin = context.ProfileOrigin == ProfileOrigin.Override ? "override" : "embedded";
            var modeName = InstallModes.Name(choice.Mode);
            logger.DataRootUsed(root, context.DataRootSource);
            logger.ProfileUsed(profile.Id, origin);
            logger.ModeUsed(modeName, choice.Source);
            try
            {
                installer.CreateSteps(choice.Mode);
            }
            catch (InvalidOperationException exception)
            {
                logger.ModeUnavailable(exception.Message);
                return 1;
            }

            var report = await services.GetRequiredService<ISystemChecker>().RunAsync(choice.Mode, cancellationToken);
            CheckReportPrinter.Print(logger, report, onlyProblems: true);
            if (report.HasBlocking)
            {
                logger.InstallBlocked();
                return CheckReportPrinter.BlockingExitCode;
            }

            await installer.InstallAsync(choice.Mode, new ConsoleStepProgress(), cancellationToken: cancellationToken);
            var version = installer.ReadRecord()?.Version ?? profile.Source.Version;
            var directory = services.GetRequiredService<AnyPS5Paths>().RootDirectory;
            logger.AnyPS5Ready(version, directory);
            return 0;
        }));
        return command;
    }
}
