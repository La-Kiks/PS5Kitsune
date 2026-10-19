using System.IO.Compression;
using System.Text.Json;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Tools;

/// <summary>
/// A tool distributed as a zip archive. Installing downloads and verifies the archive, extracts it
/// beside the final folder, then swaps it in, so an interrupted install never leaves a half-extracted tool.
/// A marker file records the archive hash: <see cref="IsInstalled"/> compares it with the profile.
/// </summary>
public abstract class ArchiveTool(string id, ToolDefinition definition, DataLayout layout, IDownloader downloader) : IPortableTool
{
    internal const string MarkerFileName = ".ps5kitsune-tool.json";

    public string Id { get; } = id;

    protected ToolDefinition Definition { get; } = definition;

    protected string InstallDirectory => layout.ToolDirectory(Id);

    /// <summary>Path of a file that must exist once the tool is extracted.</summary>
    protected abstract string RequiredFile { get; }

    public bool IsInstalled()
    {
        var marker = Path.Combine(InstallDirectory, MarkerFileName);
        if (!File.Exists(marker) || !File.Exists(RequiredFile))
        {
            return false;
        }

        try
        {
            var recorded = JsonSerializer.Deserialize<ToolMarker>(File.ReadAllText(marker));
            return recorded?.Sha256 == Definition.Sha256;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public async Task InstallAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (IsInstalled())
        {
            return;
        }

        var archive = Path.Combine(layout.Downloads, Path.GetFileName(Definition.Url.AbsolutePath));
        await downloader.DownloadAsync(new DownloadRequest(Definition.Url, archive, Definition.Sha256), progress, cancellationToken).ConfigureAwait(false);

        var staging = InstallDirectory + ".staging";
        if (Directory.Exists(staging))
        {
            Directory.Delete(staging, recursive: true);
        }

        // ExtractToDirectory rejects entries that would land outside the target folder.
        await ZipFile.ExtractToDirectoryAsync(archive, staging, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(
            Path.Combine(staging, MarkerFileName),
            JsonSerializer.Serialize(new ToolMarker(Id, Definition.Version, Definition.Sha256)),
            cancellationToken).ConfigureAwait(false);

        if (Directory.Exists(InstallDirectory))
        {
            Directory.Delete(InstallDirectory, recursive: true);
        }

        Directory.Move(staging, InstallDirectory);
    }

    public abstract Task<string> GetVersionAsync(CancellationToken cancellationToken = default);

    private sealed record ToolMarker(string Id, string Version, string Sha256);
}
