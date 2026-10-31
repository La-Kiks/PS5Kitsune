namespace PS5Kitsune.Platform.Linux;

/// <summary>What PS5Kitsune may need from the distribution.</summary>
public enum LinuxPackage
{
    Python,
    VulkanLoader,

    /// <summary>Mesa's Vulkan drivers: Lavapipe, and RADV for AMD GPUs.</summary>
    MesaVulkanDrivers,
}

/// <summary>The command that installs system packages on this distribution, shown for the user to run. See the 8a spec, "Other Linux services".</summary>
public sealed class DistroPackages(OsRelease release)
{
    private static readonly Family Debian = new("sudo apt install", "python3", "libvulkan1", "mesa-vulkan-drivers");
    private static readonly Family Fedora = new("sudo dnf install", "python3", "vulkan-loader", "mesa-vulkan-drivers");
    private static readonly Family Arch = new("sudo pacman -S", "python", "vulkan-icd-loader", "vulkan-swrast");

    /// <summary>One command for all <paramref name="packages"/>; for an unknown distribution, "Install: " and the Debian names.</summary>
    public string CommandFor(params LinuxPackage[] packages)
    {
        ArgumentNullException.ThrowIfNull(packages);
        var family = FamilyOf(release);
        var names = packages.Select(package => (family ?? Debian).Name(package));
        return family is null ? $"Install: {string.Join(", ", names)}" : $"{family.Command} {string.Join(' ', names)}";
    }

    private static Family? FamilyOf(OsRelease release) =>
        release.IdLike.Prepend(release.Id).Select(id => id switch
        {
            "debian" or "ubuntu" => Debian,
            "fedora" => Fedora,
            "arch" => Arch,
            _ => null,
        }).FirstOrDefault(family => family is not null);

    private sealed record Family(string Command, string Python, string VulkanLoader, string Mesa)
    {
        public string Name(LinuxPackage package) => package switch
        {
            LinuxPackage.Python => Python,
            LinuxPackage.VulkanLoader => VulkanLoader,
            LinuxPackage.MesaVulkanDrivers => Mesa,
            _ => throw new ArgumentOutOfRangeException(nameof(package), package, null),
        };
    }
}
