using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Launch;

namespace PS5Kitsune.Platform.Linux;

/// <summary>Lavapipe from the distribution's Mesa: nothing to download, its manifest (lvp_icd*.json) is found in the Vulkan driver folders.</summary>
public sealed class SystemLavapipe(DistroPackages packages) : ILavapipe
{
    internal IReadOnlyList<string> ManifestDirectories { get; init; } = VulkanManifests.Directories;

    /// <summary>The manifest's path; empty when Mesa's Lavapipe is not installed.</summary>
    public string Manifest => Find() ?? "";

    /// <inheritdoc />
    public string HowToGet => $"It comes from the system's Mesa; if it is missing, install it with: {packages.CommandFor(LinuxPackage.MesaVulkanDrivers)}";

    /// <inheritdoc />
    public string WhenMissing => $"install it with: {packages.CommandFor(LinuxPackage.MesaVulkanDrivers)}";

    public bool IsInstalled() => Find() is not null;

    public Task InstallAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default) =>
        IsInstalled()
            ? Task.CompletedTask
            : Task.FromException(new LavapipeUnavailableException($"Lavapipe is not installed: {WhenMissing}"));

    private string? Find() => VulkanManifests.Find(ManifestDirectories, "lvp_icd*.json") is [var first, ..] ? first : null;
}
