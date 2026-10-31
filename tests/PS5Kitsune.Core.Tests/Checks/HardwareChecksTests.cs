using System.Runtime.InteropServices;
using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.Core.Tests.Checks;

public sealed class HardwareChecksTests
{
    [Fact]
    public void X64_is_ok()
    {
        var result = new OsArchitectureCheck().Evaluate(TestFacts.Healthy());

        Assert.Equal(CheckStatus.Ok, result.Status);
        Assert.Equal("os-arch", result.Id);
    }

    [Fact]
    public void Arm64_is_blocking()
    {
        var result = new OsArchitectureCheck().Evaluate(TestFacts.Healthy() with { OSArchitecture = Architecture.Arm64 });

        Assert.Equal(CheckStatus.Blocking, result.Status);
        Assert.Contains("Arm64", result.Summary, StringComparison.Ordinal);
        Assert.NotNull(result.Action);
    }

    [Fact]
    public void All_extensions_present_is_ok()
    {
        Assert.Equal(CheckStatus.Ok, new CpuExtensionsCheck().Evaluate(TestFacts.Healthy()).Status);
    }

    [Fact]
    public void A_missing_extension_is_blocking_and_named()
    {
        var facts = TestFacts.Healthy() with { Cpu = new CpuFacts("GenuineIntel", ["F16C", "MOVBE"]) };

        var result = new CpuExtensionsCheck().Evaluate(facts);

        Assert.Equal(CheckStatus.Blocking, result.Status);
        Assert.Contains("F16C, MOVBE", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Intel_is_info_and_mentions_to_intel()
    {
        var result = new CpuVendorCheck().Evaluate(TestFacts.Healthy() with { Cpu = new CpuFacts("GenuineIntel", []) });

        Assert.Equal(CheckStatus.Info, result.Status);
        Assert.Contains("--to-intel", result.Summary, StringComparison.Ordinal);
        Assert.Contains("AMD", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Amd_is_info()
    {
        var result = new CpuVendorCheck().Evaluate(TestFacts.Healthy());

        Assert.Equal(CheckStatus.Info, result.Status);
        Assert.Contains("AMD", result.Summary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(13824, CheckStatus.Ok)]
    [InlineData(13823, CheckStatus.Warning)]
    public void Commit_limit_threshold(long mib, CheckStatus expected)
    {
        var facts = TestFacts.Healthy() with { CommitLimitBytes = (ulong)mib << 20 };

        var result = new CommitLimitCheck().Evaluate(facts);

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public void A_low_commit_limit_offers_the_performance_settings()
    {
        var facts = TestFacts.Healthy() with { CommitLimitBytes = 8UL << 30 };

        var result = new CommitLimitCheck().Evaluate(facts);

        Assert.Equal(CheckFix.OpenPerformanceSettings, result.Fix);
        Assert.Contains("8,192", result.Summary, StringComparison.Ordinal);
        Assert.Contains("13,824", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_a_commit_limit_the_check_is_not_applicable()
    {
        var result = new CommitLimitCheck().Evaluate(TestFacts.Healthy() with { CommitLimitBytes = null });

        Assert.Equal(CheckStatus.Info, result.Status);
        Assert.Equal("Not applicable on this system.", result.Summary);
    }
}
