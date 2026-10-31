using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Launch;

/// <summary>Mesa's Lavapipe (CPU rendering): downloaded on Windows, the system's Mesa on Linux.</summary>
public interface ILavapipe
{
    /// <summary>The Vulkan driver manifest passed in VK_DRIVER_FILES.</summary>
    string Manifest { get; }

    /// <summary>How the platform gets Lavapipe, as a full sentence.</summary>
    string HowToGet { get; }

    /// <summary>What to do now when Lavapipe is missing, as a lower-case phrase that follows "Lavapipe is not installed: ".</summary>
    string WhenMissing { get; }

    bool IsInstalled();

    Task InstallAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default);
}
