using System.Diagnostics;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Platform.Linux;

/// <summary>Linux: XDG folders, no executable suffix for tools, .elf games, no commit limit. See the 8a spec, "IPlatform on Linux".</summary>
public sealed class LinuxPlatform : IPlatform
{
    private readonly Func<string, string?> _variable;
    private readonly string _home;

    public LinuxPlatform(DistroPackages packages)
        : this(packages, Environment.GetEnvironmentVariable, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
    {
    }

    internal LinuxPlatform(DistroPackages packages, Func<string, string?> variable, string home)
    {
        Packages = packages;
        _variable = variable;
        _home = home;
    }

    /// <summary>The distribution's install commands, shared with the Linux checks and services.</summary>
    public DistroPackages Packages { get; }

    public string Name => "Linux";

    public string Os => ProfileOs.Linux;

    public string DefaultDataRoot => Path.Combine(XdgFolder("XDG_DATA_HOME", ".local/share"), "ps5kitsune");

    public string SettingsDirectory => Path.Combine(XdgFolder("XDG_CONFIG_HOME", ".config"), "ps5kitsune");

    public string ExecutableSuffix => "";

    public string GameExecutableSuffix => ".elf";

    public string VulkanLoaderName => "libvulkan.so.1";

    public string VulkanInstallAction =>
        $"Install Vulkan: {Packages.CommandFor(LinuxPackage.VulkanLoader, LinuxPackage.MesaVulkanDrivers)}. With an NVIDIA GPU, install NVIDIA's driver instead of Mesa.";

    public ulong? GetCommitLimitBytes() => null;

    public IReadOnlyList<string> GetVulkanDriverManifests() => VulkanManifests.Find(VulkanManifests.Directories, "*.json");

    /// <summary>Linux checks give commands to run, never a fix to open.</summary>
    public void OpenFix(CheckFix fix) => throw new ArgumentOutOfRangeException(nameof(fix), fix, "Linux has no fix to open.");

    public void OpenPath(string path)
    {
        using (Process.Start(new ProcessStartInfo("xdg-open") { ArgumentList = { path } }))
        {
        }
    }

    // XDG Base Directory: a variable that is unset, empty or relative is ignored.
    private string XdgFolder(string variable, string fallback) =>
        _variable(variable) is { Length: > 0 } value && Path.IsPathRooted(value) ? value : Path.Combine(_home, fallback);
}
