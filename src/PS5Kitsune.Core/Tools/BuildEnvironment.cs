namespace PS5Kitsune.Core.Tools;

/// <summary>
/// The tools build mode runs, and the environment of their processes. The child's PATH is WinLibs, MinGit's cmd, the inherited
/// entries without the WindowsApps folder (its WSL alias is a bash.exe that fails, and libusb-cmake's gen-describe.ps1 would pick it),
/// then MinGit's usr\bin last for its bash.exe copy (it also holds find and sort, which must not shadow Windows' own).
/// SSL_CERT_FILE points at MinGit's CA bundle so CMake can download FFmpeg (otherwise curl error 60).
/// The app's own environment is never changed.
/// </summary>
public sealed class BuildEnvironment(MinGitTool mingit, WinLibsTool winlibs)
{
    public string Git => mingit.Executable;

    public string CMake => winlibs.CMake;

    public string Ninja => winlibs.Ninja;

    public string Gcc => winlibs.Gcc;

    public string Gxx => winlibs.Gxx;

    public string CaBundle => mingit.CaBundle;

    /// <summary>Read when a command starts: the CA bundle only exists once MinGit is installed.</summary>
    /// <exception cref="InvalidOperationException">MinGit has no CA bundle.</exception>
    public IReadOnlyDictionary<string, string?> Variables() => Variables(
        Environment.GetEnvironmentVariable("PATH") ?? "",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps"));

    internal IReadOnlyDictionary<string, string?> Variables(string inheritedPath, string windowsAppsDirectory)
    {
        var windowsApps = Trimmed(windowsAppsDirectory);
        var inherited = inheritedPath
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(entry => !string.Equals(Trimmed(entry), windowsApps, StringComparison.OrdinalIgnoreCase));
        string[] path = [winlibs.Bin, mingit.CommandDirectory, .. inherited, mingit.ShellDirectory];
        return new Dictionary<string, string?>
        {
            ["PATH"] = string.Join(Path.PathSeparator, path),
            ["SSL_CERT_FILE"] = CaBundle,
        };
    }

    private static string Trimmed(string directory) => directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
