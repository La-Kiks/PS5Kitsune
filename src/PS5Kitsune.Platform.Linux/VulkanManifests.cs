namespace PS5Kitsune.Platform.Linux;

/// <summary>Where Linux distributions install Vulkan driver manifests (ICD JSON files).</summary>
public static class VulkanManifests
{
    public static IReadOnlyList<string> Directories { get; } = ["/usr/share/vulkan/icd.d", "/etc/vulkan/icd.d"];

    /// <summary>The files matching <paramref name="pattern"/> in the folders that exist, sorted by path.</summary>
    public static IReadOnlyList<string> Find(IEnumerable<string> directories, string pattern) =>
        [.. directories.Where(Directory.Exists).SelectMany(directory => Directory.EnumerateFiles(directory, pattern)).Order(StringComparer.Ordinal)];
}
