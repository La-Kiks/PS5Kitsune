using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Tools;

/// <summary>The official MinGit zip: git for build mode, and the CA bundle CMake needs to download over HTTPS.</summary>
public sealed class MinGitTool(
    ToolDefinition definition,
    DataLayout layout,
    IPlatform platform,
    IDownloader downloader,
    IProcessRunner processRunner)
    : ArchiveTool(ToolId, definition, layout, downloader)
{
    public const string ToolId = "mingit";

    private const string VersionPrefix = "git version ";

    public string CommandDirectory => Path.Combine(InstallDirectory, "cmd");

    public string Executable => Path.Combine(CommandDirectory, "git" + platform.ExecutableSuffix);

    /// <summary>The CA bundle, under ucrt64 (MinGit 2.56) or mingw64 (older MinGit), as AnyPS5's CI looks for it.</summary>
    /// <exception cref="InvalidOperationException">Neither file exists.</exception>
    public string CaBundle
    {
        get
        {
            string[] candidates =
            [
                Path.Combine(InstallDirectory, "ucrt64", "etc", "ssl", "certs", "ca-bundle.crt"),
                Path.Combine(InstallDirectory, "mingw64", "etc", "ssl", "certs", "ca-bundle.crt"),
            ];
            return candidates.FirstOrDefault(File.Exists)
                ?? throw new InvalidOperationException($"MinGit has no CA bundle: neither {candidates[0]} nor {candidates[1]} exists.");
        }
    }

    protected override IEnumerable<string> RequiredFiles => [Executable];

    public override async Task<string> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var lines = new List<string>();
        var result = await processRunner.RunAsync(new ProcessSpec(Executable, ["--version"]), line => lines.Add(line.Text), cancellationToken).ConfigureAwait(false);

        // "git version 2.56.0.windows.2"
        var output = string.Join(' ', lines).Trim();
        if (result.ExitCode != 0 || !output.StartsWith(VersionPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{Executable} --version exited with code {result.ExitCode}: {output}");
        }

        return output[VersionPrefix.Length..];
    }
}
