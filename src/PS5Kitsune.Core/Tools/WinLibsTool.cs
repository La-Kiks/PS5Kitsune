using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Tools;

/// <summary>The WinLibs GCC zip, the only toolchain AnyPS5 supports on Windows. It ships CMake and Ninja too.</summary>
public sealed class WinLibsTool(
    ToolDefinition definition,
    DataLayout layout,
    IPlatform platform,
    IDownloader downloader,
    IProcessRunner processRunner)
    : ArchiveTool(ToolId, definition, layout, downloader)
{
    public const string ToolId = "winlibs";

    public string Bin => Path.Combine(InstallDirectory, "mingw64", "bin");

    public string Gcc => Tool("gcc");

    public string Gxx => Tool("g++");

    public string CMake => Tool("cmake");

    public string Ninja => Tool("ninja");

    /// <summary>The GCC runtime DLLs the relinker and the libraries load; they go into anyps5\installs\build\libs.</summary>
    public IReadOnlyList<string> RuntimeDlls =>
    [
        Path.Combine(Bin, "libgcc_s_seh-1.dll"),
        Path.Combine(Bin, "libstdc++-6.dll"),
        Path.Combine(Bin, "libwinpthread-1.dll"),
    ];

    protected override IEnumerable<string> RequiredFiles => [Gxx, CMake, Ninja];

    public override async Task<string> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var lines = new List<string>();
        var result = await processRunner.RunAsync(new ProcessSpec(Gcc, ["-dumpfullversion"]), line => lines.Add(line.Text), cancellationToken).ConfigureAwait(false);

        // "15.2.0"
        var output = lines.FirstOrDefault()?.Trim() ?? "";
        if (result.ExitCode != 0 || output.Length == 0)
        {
            throw new InvalidOperationException($"{Gcc} -dumpfullversion exited with code {result.ExitCode}: {output}");
        }

        return output;
    }

    private string Tool(string name) => Path.Combine(Bin, name + platform.ExecutableSuffix);
}
