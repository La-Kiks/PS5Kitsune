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

    public string Executable => Path.Combine(InstallDirectory, "python" + platform.ExecutableSuffix);

    protected override string RequiredFile => Executable;

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
