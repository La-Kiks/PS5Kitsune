using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Tests.Checks;

public sealed class StorageChecksTests
{
    [Theory]
    [InlineData(@"C:\PS5Kitsune", CheckStatus.Ok)]
    [InlineData(@"D:\Games\PS5", CheckStatus.Ok)]
    [InlineData(@"C:\PS5 Kitsune", CheckStatus.Blocking)]
    [InlineData(@"C:\Jeux\Café", CheckStatus.Blocking)]
    public void Install_path_must_be_ascii_without_spaces(string root, CheckStatus expected)
    {
        var result = new InstallPathCheck().Evaluate(TestFacts.Healthy() with { DataRoot = root });

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public void Build_mode_needs_the_build_threshold()
    {
        var facts = TestFacts.Healthy() with
        {
            Mode = AnyPS5Mode.Build,
            FreeDiskBytes = 10L << 30,
            Limits = new CheckThresholds { MinFreeDiskGiB = 5, BuildMinFreeDiskGiB = 15 },
        };

        var result = new DiskSpaceCheck().Evaluate(facts);

        Assert.Equal(CheckStatus.Warning, result.Status);
        Assert.Contains("15 GiB", result.Summary, StringComparison.Ordinal);
        Assert.Contains("built from source", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void A_blocked_path_names_the_path_and_suggests_one()
    {
        var result = new InstallPathCheck().Evaluate(TestFacts.Healthy() with { DataRoot = @"C:\PS5 Kitsune" });

        Assert.Contains(@"C:\PS5 Kitsune", result.Summary, StringComparison.Ordinal);
        Assert.Contains(@"C:\PS5Kitsune", result.Action, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(5L << 30, CheckStatus.Ok)]
    [InlineData((5L << 30) - 1, CheckStatus.Warning)]
    public void Disk_space_threshold(long free, CheckStatus expected)
    {
        var result = new DiskSpaceCheck().Evaluate(TestFacts.Healthy() with { FreeDiskBytes = free });

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public void Low_disk_space_shows_the_free_space()
    {
        var result = new DiskSpaceCheck().Evaluate(TestFacts.Healthy() with { FreeDiskBytes = 3L << 30 });

        Assert.Contains("3.0 GiB", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Free_space_just_under_the_threshold_never_shows_as_the_threshold()
    {
        var result = new DiskSpaceCheck().Evaluate(TestFacts.Healthy() with { FreeDiskBytes = (5L << 30) - 1 });

        Assert.Contains("4.9 GiB", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_disk_space_is_info()
    {
        var result = new DiskSpaceCheck().Evaluate(TestFacts.Healthy() with { FreeDiskBytes = null });

        Assert.Equal(CheckStatus.Info, result.Status);
    }

    [Fact]
    public void A_path_with_a_space_suggests_the_platform_default()
    {
        var result = new InstallPathCheck().Evaluate(TestFacts.Healthy() with { DataRoot = "/home/a b/ps5kitsune", DefaultDataRoot = "/home/ab/.local/share/ps5kitsune" });

        Assert.Equal(CheckStatus.Blocking, result.Status);
        Assert.Equal("Choose a data folder without spaces or accents, for example /home/ab/.local/share/ps5kitsune (command line: --root).", result.Action);
    }
}
