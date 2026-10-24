using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Launch;

public enum LaunchDownload
{
    Lavapipe,
    Fonts,
}

/// <summary>A download a launch waits for.</summary>
public readonly record struct LaunchProgress(LaunchDownload Download, DownloadProgress Progress);

/// <param name="LavapipeManifest">Mesa's manifest when the settings use Lavapipe; null otherwise.</param>
/// <param name="FontsDirectory">The installed fonts; null when the profile has none or their download failed.</param>
/// <param name="FontsWarning">Why the fonts are missing after a failed download; null otherwise.</param>
public sealed record LaunchResources(string? LavapipeManifest, string? FontsDirectory, string? FontsWarning);
