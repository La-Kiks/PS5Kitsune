using System.CommandLine;
using PS5Kitsune.Cli;

var rootOption = new Option<DirectoryInfo?>("--root")
{
    Description = "Data root (tools, AnyPS5, games). Defaults to C:\\PS5Kitsune on Windows.",
    Recursive = true,
};

var rootCommand = new RootCommand("PS5Kitsune: installer and launcher for AnyPS5.")
{
    InstallCommand.Create(rootOption),
    AddCommand.Create(rootOption),
    ToolsCommand.Create(rootOption),
};
rootCommand.Options.Add(rootOption);

return await rootCommand.Parse(args).InvokeAsync();
