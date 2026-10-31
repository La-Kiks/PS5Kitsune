using PS5Kitsune.Core.Launch;

namespace PS5Kitsune.Platform.Linux.Tests;

public sealed class SystemLavapipeTests : IDisposable
{
    private readonly PS5Kitsune.Core.Tests.TempDirectory _temp = new();

    private static readonly DistroPackages Fedora = new(OsRelease.Parse("ID=fedora\n"));

    [Fact]
    public void Finds_the_lavapipe_manifest()
    {
        var icd = _temp.Combine("icd.d");
        Directory.CreateDirectory(icd);
        File.WriteAllText(Path.Combine(icd, "radeon_icd.json"), "{}");
        File.WriteAllText(Path.Combine(icd, "lvp_icd.json"), "{}");
        var lavapipe = new SystemLavapipe(Fedora) { ManifestDirectories = [_temp.Combine("missing"), icd] };

        Assert.True(lavapipe.IsInstalled());
        Assert.Equal(Path.Combine(icd, "lvp_icd.json"), lavapipe.Manifest);
    }

    [Fact]
    public async Task Without_a_manifest_install_names_the_mesa_command()
    {
        var lavapipe = new SystemLavapipe(Fedora) { ManifestDirectories = [_temp.Path] };

        Assert.False(lavapipe.IsInstalled());
        Assert.Equal("", lavapipe.Manifest);
        var exception = await Assert.ThrowsAsync<LavapipeUnavailableException>(() => lavapipe.InstallAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("Lavapipe is not installed: sudo dnf install mesa-vulkan-drivers", exception.Message);
    }

    public void Dispose() => _temp.Dispose();
}
