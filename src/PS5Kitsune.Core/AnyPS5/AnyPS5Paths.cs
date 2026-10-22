using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.AnyPS5;

/// <summary>
/// The AnyPS5 installs and the state files that choose between them. The file properties point at the active install,
/// read from anyps5\state\active on each access, so game conversion always uses the install in use. See
/// docs/architecture.md, "Root layout".
/// </summary>
public sealed class AnyPS5Paths(DataLayout layout, IPlatform platform)
{
    public string RootDirectory => Active.RootDirectory;

    public string Relinker => Active.Relinker;

    /// <inheritdoc cref="AnyPS5InstallPaths.Libs"/>
    public string Libs => Active.Libs;

    /// <inheritdoc cref="AnyPS5InstallPaths.Scripts"/>
    public string Scripts => Active.Scripts;

    public string ImportAudit => Active.ImportAudit;

    public string UnwrapSelf => Active.UnwrapSelf;

    public string RecordFile => Active.RecordFile;

    public string RequestedModeFile => Path.Combine(layout.AnyPS5, "state", "requested-mode");

    public string ActiveModeFile => Path.Combine(layout.AnyPS5, "state", "active");

    /// <summary>anyps5\current, the single install folder before milestone 5c.</summary>
    public string LegacyCurrent => layout.AnyPS5Current;

    /// <summary>The mode in anyps5\state\active; null when the file is missing, unreadable or holds an unknown value.</summary>
    public AnyPS5Mode? ActiveMode => ReadMode(ActiveModeFile);

    /// <summary>The mode in anyps5\state\requested-mode; null when the file is missing, unreadable or holds an unknown value.</summary>
    public AnyPS5Mode? RequestedMode => ReadMode(RequestedModeFile);

    public AnyPS5InstallPaths For(AnyPS5Mode mode) => new(Path.Combine(layout.AnyPS5Installs, InstallModes.Name(mode)), platform);

    public void WriteActiveMode(AnyPS5Mode mode)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ActiveModeFile)!);
        File.WriteAllText(ActiveModeFile, InstallModes.Name(mode));
    }

    /// <summary>With no active install, the release folder, where nothing is installed: callers check IsInstalled first.</summary>
    private AnyPS5InstallPaths Active => For(ActiveMode ?? AnyPS5Mode.Release);

    private static AnyPS5Mode? ReadMode(string file)
    {
        try
        {
            return InstallModes.Parse(File.ReadAllText(file).Trim());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
