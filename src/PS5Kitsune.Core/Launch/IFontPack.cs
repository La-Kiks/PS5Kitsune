using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Launch;

/// <summary>The Noto font substitutes, downloaded once for every game.</summary>
public interface IFontPack
{
    /// <summary>The folder passed in ANYPS5_SYSTEM_FONTS.</summary>
    string Folder { get; }

    bool IsInstalled();

    Task InstallAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default);
}
