using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Launch;

/// <summary>Mesa's Lavapipe (CPU rendering), downloaded on the first launch that needs it.</summary>
public interface ILavapipe
{
    /// <summary>The Vulkan driver manifest passed in VK_DRIVER_FILES.</summary>
    string Manifest { get; }

    bool IsInstalled();

    Task InstallAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default);
}
