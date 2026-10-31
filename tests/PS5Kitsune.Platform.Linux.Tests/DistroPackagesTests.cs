namespace PS5Kitsune.Platform.Linux.Tests;

public sealed class DistroPackagesTests : IDisposable
{
    private readonly PS5Kitsune.Core.Tests.TempDirectory _temp = new();

    private static string CommandFor(string osRelease, params LinuxPackage[] packages) => new DistroPackages(OsRelease.Parse(osRelease)).CommandFor(packages);

    [Fact]
    public void Ubuntu_uses_apt()
    {
        var ubuntu = "PRETTY_NAME=\"Ubuntu 24.04.5 LTS\"\nNAME=\"Ubuntu\"\nID=ubuntu\nID_LIKE=debian\n";

        Assert.Equal("sudo apt install python3", CommandFor(ubuntu, LinuxPackage.Python));
        Assert.Equal("sudo apt install libvulkan1 mesa-vulkan-drivers", CommandFor(ubuntu, LinuxPackage.VulkanLoader, LinuxPackage.MesaVulkanDrivers));
    }

    [Fact]
    public void Debian_uses_apt() => Assert.Equal("sudo apt install python3", CommandFor("ID=debian\n", LinuxPackage.Python));

    [Fact]
    public void Linux_mint_is_found_through_its_quoted_id_like() =>
        Assert.Equal("sudo apt install mesa-vulkan-drivers", CommandFor("ID=linuxmint\nID_LIKE=\"ubuntu debian\"\n", LinuxPackage.MesaVulkanDrivers));

    [Fact]
    public void Fedora_uses_dnf() =>
        Assert.Equal("sudo dnf install python3 vulkan-loader mesa-vulkan-drivers", CommandFor("ID=fedora\n", LinuxPackage.Python, LinuxPackage.VulkanLoader, LinuxPackage.MesaVulkanDrivers));

    [Fact]
    public void Arch_uses_pacman() =>
        Assert.Equal("sudo pacman -S python vulkan-icd-loader vulkan-swrast", CommandFor("ID='arch'\n", LinuxPackage.Python, LinuxPackage.VulkanLoader, LinuxPackage.MesaVulkanDrivers));

    [Fact]
    public void An_unknown_distribution_gets_the_debian_names() =>
        Assert.Equal("Install: python3, libvulkan1", CommandFor("ID=gentoo\n", LinuxPackage.Python, LinuxPackage.VulkanLoader));

    [Fact]
    public void Comments_and_blank_lines_are_skipped()
    {
        var release = OsRelease.Parse("# a comment\n\nID=fedora\r\nID_LIKE=\n");

        Assert.Equal("fedora", release.Id);
        Assert.Empty(release.IdLike);
    }

    [Fact]
    public void A_missing_file_is_an_unknown_distribution()
    {
        var release = OsRelease.Read(_temp.Combine("os-release"));

        Assert.Equal("", release.Id);
        Assert.Equal("Install: python3", new DistroPackages(release).CommandFor(LinuxPackage.Python));
    }

    public void Dispose() => _temp.Dispose();
}
