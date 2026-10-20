using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Cli;

internal static class ToolsCommand
{
    public static Command Create(Option<DirectoryInfo?> rootOption)
    {
        var toolArgument = new Argument<string>("tool") { Description = "Tool to install, as shown by 'tools list'." };

        var listCommand = new Command("list", "List the portable tools of the active profile and whether they are installed.");
        listCommand.SetAction(parseResult => CommandHost.RunAsync(parseResult.GetValue(rootOption), "tools-list", (services, _) =>
        {
            foreach (var tool in services.GetServices<IPortableTool>())
            {
                Console.WriteLine($"{tool.Id,-10} {(tool.IsInstalled() ? "installed" : "missing")}");
            }

            return Task.FromResult(0);
        }));

        var installCommand = new Command("install", "Download, verify and extract a portable tool, then check that it runs.") { toolArgument };
        installCommand.SetAction((parseResult, cancellationToken) => CommandHost.RunAsync(parseResult.GetValue(rootOption), "tools-install", async (services, logger) =>
        {
            var id = parseResult.GetRequiredValue(toolArgument);
            var tool = services.GetServices<IPortableTool>().FirstOrDefault(candidate => candidate.Id == id);
            if (tool is null)
            {
                logger.UnknownTool(id);
                return 1;
            }

            await tool.InstallAsync(new ConsoleDownloadProgress(), cancellationToken);
            var version = await tool.GetVersionAsync(cancellationToken);
            logger.ToolReady(tool.Id, version);
            return 0;
        }));

        return new Command("tools", "Manage the portable tools (Python, Git, WinLibs…).") { listCommand, installCommand };
    }
}
