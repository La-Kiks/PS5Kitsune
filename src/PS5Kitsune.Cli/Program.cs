using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PS5Kitsune.Cli;
using PS5Kitsune.Core;
using PS5Kitsune.Core.Logging;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tools;
using PS5Kitsune.Platform.Windows;

var rootOption = new Option<DirectoryInfo?>("--root")
{
    Description = "Data root (tools, AnyPS5, games). Defaults to C:\\PS5Kitsune on Windows.",
    Recursive = true,
};

var toolArgument = new Argument<string>("tool") { Description = "Tool to install, as shown by 'tools list'." };

var listCommand = new Command("list", "List the portable tools of the active profile and whether they are installed.");
listCommand.SetAction(parseResult => Run(parseResult.GetValue(rootOption), "tools-list", (services, _) =>
{
    foreach (var tool in services.GetServices<IPortableTool>())
    {
        Console.WriteLine($"{tool.Id,-10} {(tool.IsInstalled() ? "installed" : "missing")}");
    }

    return Task.FromResult(0);
}));

var installCommand = new Command("install", "Download, verify and extract a portable tool, then check that it runs.") { toolArgument };
installCommand.SetAction((parseResult, cancellationToken) => Run(parseResult.GetValue(rootOption), "tools-install", async (services, logger) =>
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

var toolsCommand = new Command("tools", "Manage the portable tools (Python, Git, WinLibs…).") { listCommand, installCommand };
var rootCommand = new RootCommand("PS5Kitsune: installer and launcher for AnyPS5.") { toolsCommand };
rootCommand.Options.Add(rootOption);

return await rootCommand.Parse(args).InvokeAsync();

static async Task<int> Run(DirectoryInfo? rootOverride, string runName, Func<IServiceProvider, ILogger, Task<int>> action)
{
    var platform = PlatformServices.Detect();
    var layout = new DataLayout(rootOverride?.FullName ?? platform.DefaultDataRoot);
    var services = new ServiceCollection();
    var logFile = services.AddPS5KitsuneLogging(layout.Logs, runName, console: true);
    services.AddPlatform(platform).AddPS5KitsuneCore(layout, ProfileLoader.LoadEmbedded());

    await using var provider = services.BuildServiceProvider();
    var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("ps5kitsune");
    try
    {
        return await action(provider, logger);
    }
    catch (OperationCanceledException)
    {
        logger.Cancelled();
        return 130;
    }
    catch (Exception exception)
    {
        logger.Failed(exception, exception.Message);
        logger.FullLog(logFile);
        return 1;
    }
}
