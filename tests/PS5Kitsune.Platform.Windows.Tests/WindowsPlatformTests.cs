using System.Runtime.Versioning;

namespace PS5Kitsune.Platform.Windows.Tests;

[SupportedOSPlatform("windows")]
public sealed class WindowsPlatformTests
{
    private readonly WindowsPlatform _platform = new();

    [Fact]
    public void Settings_live_in_local_app_data()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");

        Assert.Equal(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PS5Kitsune"),
            _platform.SettingsDirectory);
    }

    [Fact]
    public void Commit_limit_is_at_least_the_physical_memory()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");

        var commitLimit = _platform.GetCommitLimitBytes();

        Assert.NotNull(commitLimit);
        Assert.True(commitLimit >= (ulong)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 2, $"Commit limit {commitLimit} looks too small.");
    }

    [Fact]
    public void Driver_manifests_can_be_listed()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");

        var manifests = _platform.GetVulkanDriverManifests();

        Assert.All(manifests, manifest => Assert.False(string.IsNullOrWhiteSpace(manifest)));
    }
}
