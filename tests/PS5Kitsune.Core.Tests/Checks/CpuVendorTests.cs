using System.Runtime.Intrinsics.X86;
using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.Core.Tests.Checks;

public sealed class CpuVendorTests
{
    [Fact]
    public void Reads_a_twelve_character_vendor_on_x86()
    {
        Assert.SkipUnless(X86Base.IsSupported, "Not an x86 CPU.");

        var vendor = CpuVendor.Detect();

        Assert.Equal(12, vendor.Length);
        Assert.All(vendor, character => Assert.True(char.IsAsciiLetterOrDigit(character) || character == ' '));
    }

    [Theory]
    [InlineData("GenuineIntel", true)]
    [InlineData("AuthenticAMD", false)]
    [InlineData("unknown", false)]
    public void Only_GenuineIntel_is_Intel(string vendor, bool expected)
    {
        Assert.Equal(expected, CpuVendor.IsIntel(vendor));
    }
}
