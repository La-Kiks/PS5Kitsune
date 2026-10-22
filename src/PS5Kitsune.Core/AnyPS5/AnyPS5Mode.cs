namespace PS5Kitsune.Core.AnyPS5;

/// <summary>How AnyPS5 gets into anyps5\installs\&lt;mode&gt;. See docs/architecture.md, "Getting AnyPS5".</summary>
public enum AnyPS5Mode
{
    /// <summary>Download the release of the profile's tag.</summary>
    Release,

    /// <summary>Compile the profile's commit with MinGit and WinLibs.</summary>
    Build,
}
