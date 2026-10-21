using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Tools;

/// <summary>
/// The official embeddable Python zip. It runs AnyPS5's scripts (import_audit.py, unwrap_self.py),
/// which only need the standard library.
/// </summary>
public sealed class PythonEmbeddableTool(
    ToolDefinition definition,
    DataLayout layout,
    IPlatform platform,
    IDownloader downloader,
    IProcessRunner processRunner)
    : ArchiveTool(ToolId, definition, layout, downloader)
{
    public const string ToolId = "python";

    // The ._pth file of the embeddable distribution keeps the script's folder off sys.path and makes
    // Python ignore PYTHONPATH, so a script importing its siblings (import_audit.py imports nid_names
    // and progress) fails when run directly. This bootstrap puts the folder back first, then runs the
    // script as __main__; its SystemExit code becomes the process exit code.
    private const string ScriptBootstrap =
        "import os,runpy,sys;sys.argv=sys.argv[1:];sys.path.insert(0,os.path.dirname(os.path.abspath(sys.argv[0])));runpy.run_path(sys.argv[0],run_name='__main__')";

    public string Executable => Path.Combine(InstallDirectory, "python" + platform.ExecutableSuffix);

    protected override IEnumerable<string> RequiredFiles => [Executable];

    /// <summary>A command running <paramref name="script"/> with <paramref name="arguments"/>; the script's exit code is the process exit code.</summary>
    public ProcessSpec CreateScriptSpec(string script, IEnumerable<string> arguments) =>
        new(Executable, ["-c", ScriptBootstrap, script, .. arguments]);

    public override async Task<string> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var lines = new List<string>();
        var result = await processRunner.RunAsync(
            new ProcessSpec(Executable, ["--version"]),
            line => lines.Add(line.Text),
            cancellationToken).ConfigureAwait(false);

        // "Python 3.14.8"
        var output = string.Join(' ', lines).Trim();
        if (result.ExitCode != 0 || !output.StartsWith("Python ", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{Executable} --version exited with code {result.ExitCode}: {output}");
        }

        return output["Python ".Length..];
    }
}
