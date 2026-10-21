namespace PS5Kitsune.Core.Tools;

/// <summary>
/// The tools build mode runs, and the environment of their processes: WinLibs then MinGit first on the child's PATH,
/// and SSL_CERT_FILE on MinGit's CA bundle so CMake can download FFmpeg (otherwise curl error 60).
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
    public IReadOnlyDictionary<string, string?> Variables()
    {
        string[] path = [winlibs.Bin, mingit.CommandDirectory, .. (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)];
        return new Dictionary<string, string?>
        {
            ["PATH"] = string.Join(Path.PathSeparator, path),
            ["SSL_CERT_FILE"] = CaBundle,
        };
    }
}
