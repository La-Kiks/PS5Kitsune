using System.CommandLine;
using PS5Kitsune.Cli;
using PS5Kitsune.Core.Checks.Vulkan;

// The Vulkan probe child (see VulkanProbeRunner): nothing else is loaded, so a crashing driver only takes this process down.
if (args is [VulkanProbeRunner.ProbeArgument])
{
    Console.Out.Write(VulkanProbe.Run().ToJson());
    return 0;
}

var rootOption = new Option<DirectoryInfo?>("--root")
{
    Description = "Data root (tools, AnyPS5, games). Defaults to the folder chosen in the app, else C:\\PS5Kitsune on Windows.",
    Recursive = true,
};

var rootCommand = new RootCommand("PS5Kitsune: installer and launcher for AnyPS5.")
{
    CheckCommand.Create(rootOption),
    InstallCommand.Create(rootOption),
    AddCommand.Create(rootOption),
    RunCommand.Create(rootOption),
    ToolsCommand.Create(rootOption),
};
rootCommand.Options.Add(rootOption);

// Ctrl+C cancels the command, which then waits for git, CMake and Ninja to stop and logs the cancellation.
// The default 2-second timeout cut that short and ended the process with the log unflushed.
var invocation = new InvocationConfiguration { ProcessTerminationTimeout = TimeSpan.FromSeconds(30) };
return await rootCommand.Parse(args).InvokeAsync(invocation);
