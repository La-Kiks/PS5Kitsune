namespace PS5Kitsune.Core;

/// <summary>Folders under the data root. See docs/architecture.md, "Root layout".</summary>
public sealed class DataLayout(string root)
{
    public string Root { get; } = Path.GetFullPath(root);

    public string Tools => Path.Combine(Root, "tools");

    public string Downloads => Path.Combine(Root, "downloads");

    public string Logs => Path.Combine(Root, "logs");

    public string Profiles => Path.Combine(Root, "profiles");

    /// <summary>The AnyPS5 install used to convert games: relinker, libs, scripts.</summary>
    public string AnyPS5Current => Path.Combine(Root, "anyps5", "current");

    public string Games => Path.Combine(Root, "games");

    /// <summary>Zips of games extracted before they are copied.</summary>
    public string Staging => Path.Combine(Downloads, "staging");

    public string GameDirectory(string titleId) => Path.Combine(Games, titleId);

    public string ToolDirectory(string toolId) => Path.Combine(Tools, toolId);
}
