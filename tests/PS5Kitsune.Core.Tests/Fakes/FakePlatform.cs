using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.Core.Tests.Fakes;

internal sealed class FakePlatform : IPlatform
{
    public string Name => "Test";

    public string DefaultDataRoot => "unused";

    public string SettingsDirectory => "unused";

    public string ExecutableSuffix => ".exe";

    public string Os { get; set; } = "windows";

    public string GameExecutableSuffix { get; set; } = ".exe";

    public string VulkanLoaderName { get; set; } = "vulkan-1.dll";

    public string VulkanInstallAction { get; set; } = "Install or update the graphics driver from your GPU maker's website (AMD, Intel or NVIDIA).";

    public ulong? CommitLimitBytes { get; set; } = 32UL << 30;

    public IReadOnlyList<string> VulkanDriverManifests { get; set; } = [];

    public List<CheckFix> OpenedFixes { get; } = [];

    public ulong? GetCommitLimitBytes() => CommitLimitBytes;

    public IReadOnlyList<string> GetVulkanDriverManifests() => VulkanDriverManifests;

    public void OpenFix(CheckFix fix) => OpenedFixes.Add(fix);

    public List<string> OpenedPaths { get; } = [];

    public void OpenPath(string path) => OpenedPaths.Add(path);
}
