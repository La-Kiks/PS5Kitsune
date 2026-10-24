namespace PS5Kitsune.Core;

/// <summary>Folders under the data root. See docs/architecture.md, "Root layout".</summary>
public sealed class DataLayout(string root)
{
    public string Root { get; } = Path.GetFullPath(root);

    public string Tools => Path.Combine(Root, "tools");

    public string Downloads => Path.Combine(Root, "downloads");

    public string Logs => Path.Combine(Root, "logs");

    public string Profiles => Path.Combine(Root, "profiles");

    /// <summary>The AnyPS5 install and, in build mode, its clone, build folder and step markers.</summary>
    public string AnyPS5 => Path.Combine(Root, "anyps5");

    /// <summary>One folder per install mode (release, build), each with relinker, libs, scripts and install.json.</summary>
    public string AnyPS5Installs => Path.Combine(AnyPS5, "installs");

    /// <summary>The single install folder of milestones 4 to 5b; moved into <see cref="AnyPS5Installs"/> by the migration.</summary>
    public string AnyPS5Current => Path.Combine(AnyPS5, "current");

    public string Games => Path.Combine(Root, "games");

    /// <summary>The Noto fonts, shared by every game through ANYPS5_SYSTEM_FONTS.</summary>
    public string Fonts => Path.Combine(Root, "fonts");

    /// <summary>Zips of games extracted before they are copied.</summary>
    public string Staging => Path.Combine(Downloads, "staging");

    public string GameDirectory(string titleId) => Path.Combine(Games, titleId);

    public string ToolDirectory(string toolId) => Path.Combine(Tools, toolId);
}
