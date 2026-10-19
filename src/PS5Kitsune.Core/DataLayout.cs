namespace PS5Kitsune.Core;

/// <summary>Folders under the data root. See docs/architecture.md, "Root layout".</summary>
public sealed class DataLayout(string root)
{
    public string Root { get; } = Path.GetFullPath(root);

    public string Tools => Path.Combine(Root, "tools");

    public string Downloads => Path.Combine(Root, "downloads");

    public string Logs => Path.Combine(Root, "logs");

    public string Profiles => Path.Combine(Root, "profiles");

    public string ToolDirectory(string toolId) => Path.Combine(Tools, toolId);
}
