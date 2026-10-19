using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Tools;

/// <summary>A tool downloaded as an archive into the data root, never installed system-wide.</summary>
public interface IPortableTool
{
    /// <summary>Identifier used on the command line and as the folder name under tools/.</summary>
    string Id { get; }

    /// <summary>True when the tool is extracted and matches the profile's archive hash.</summary>
    bool IsInstalled();

    Task InstallAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Runs the tool and returns the version it reports.</summary>
    Task<string> GetVersionAsync(CancellationToken cancellationToken = default);
}
