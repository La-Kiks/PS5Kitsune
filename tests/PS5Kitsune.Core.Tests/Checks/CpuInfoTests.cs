using System.Runtime.Intrinsics.X86;
using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.Core.Tests.Checks;

public sealed class CpuInfoTests
{
    [Fact]
    public void Reads_a_twelve_character_vendor_on_x86()
    {
        Assert.SkipUnless(X86Base.IsSupported, "Not an x86 CPU.");

        var vendor = new X86CpuInfo().Read().Vendor;

        Assert.Equal(12, vendor.Length);
        Assert.All(vendor, character => Assert.True(char.IsAsciiLetterOrDigit(character) || character == ' '));
    }

    // The development laptop and the CI runners all have these extensions; this checks the detection itself.
    [Fact]
    public void A_modern_x86_cpu_misses_no_extension()
    {
        Assert.SkipUnless(X86Base.IsSupported, "Not an x86 CPU.");

        Assert.Empty(new X86CpuInfo().Read().MissingExtensions);
    }

    [Theory]
    [InlineData("GenuineIntel", true)]
    [InlineData("AuthenticAMD", false)]
    [InlineData("unknown", false)]
    public void Only_GenuineIntel_is_Intel(string vendor, bool expected)
    {
        Assert.Equal(expected, new CpuFacts(vendor, []).IsIntel);
    }

    [Fact]
    public void Lists_the_seven_required_extensions()
    {
        Assert.Equal(["AVX2", "FMA", "F16C", "BMI1", "BMI2", "LZCNT", "MOVBE"], X86CpuInfo.RequiredExtensions);
    }
}
