using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.AnyPS5;

/// <summary>Files of the AnyPS5 install. See docs/architecture.md, "Root layout".</summary>
public sealed class AnyPS5Paths(DataLayout layout, IPlatform platform)
{
    public string RootDirectory => layout.AnyPS5Current;

    public string Relinker => Path.Combine(RootDirectory, "relinker" + platform.ExecutableSuffix);

    /// <summary>The .prx files and DLLs copied beside each game.</summary>
    public string Libs => Path.Combine(RootDirectory, "libs");

    /// <summary>tools/ and core/libs/prx/ of the source tree, layout kept so import_audit.py finds ../core/libs/prx.</summary>
    public string Scripts => Path.Combine(RootDirectory, "scripts");

    public string ImportAudit => Path.Combine(Scripts, "tools", "import_audit.py");

    public string UnwrapSelf => Path.Combine(Scripts, "tools", "unwrap_self.py");

    public string RecordFile => Path.Combine(RootDirectory, "install.json");
}
