using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Platform.Linux.Tests;

public sealed class LinuxPlatformTests
{
    private static readonly DistroPackages Ubuntu = new(OsRelease.Parse("ID=ubuntu\nID_LIKE=debian\n"));

    private static LinuxPlatform With(params (string Name, string Value)[] variables) =>
        new(Ubuntu, name => variables.FirstOrDefault(variable => variable.Name == name).Value, "/home/ka");

    [Fact]
    public void Without_xdg_variables_the_folders_are_under_home()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        var platform = With();

        Assert.Equal("/home/ka/.local/share/ps5kitsune", platform.DefaultDataRoot);
        Assert.Equal("/home/ka/.config/ps5kitsune", platform.SettingsDirectory);
    }

    [Fact]
    public void Absolute_xdg_variables_are_used()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        var platform = With(("XDG_DATA_HOME", "/data"), ("XDG_CONFIG_HOME", "/cfg"));

        Assert.Equal("/data/ps5kitsune", platform.DefaultDataRoot);
        Assert.Equal("/cfg/ps5kitsune", platform.SettingsDirectory);
    }

    [Fact]
    public void Empty_or_relative_xdg_variables_are_ignored()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        var platform = With(("XDG_DATA_HOME", ""), ("XDG_CONFIG_HOME", "relative/cfg"));

        Assert.Equal("/home/ka/.local/share/ps5kitsune", platform.DefaultDataRoot);
        Assert.Equal("/home/ka/.config/ps5kitsune", platform.SettingsDirectory);
    }

    [Fact]
    public void Describes_linux()
    {
        var platform = With();

        Assert.Equal(ProfileOs.Linux, platform.Os);
        Assert.Equal("", platform.ExecutableSuffix);
        Assert.Equal(".elf", platform.GameExecutableSuffix);
        Assert.Null(platform.GetCommitLimitBytes());
        Assert.Equal("libvulkan.so.1", platform.VulkanLoaderName);
        Assert.Equal(
            "Install Vulkan: sudo apt install libvulkan1 mesa-vulkan-drivers. With an NVIDIA GPU, install NVIDIA's driver instead of Mesa.",
            platform.VulkanInstallAction);
    }
}
