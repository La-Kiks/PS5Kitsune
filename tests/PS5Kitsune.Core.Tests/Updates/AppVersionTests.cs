using PS5Kitsune.Core.Updates;

namespace PS5Kitsune.Core.Tests.Updates;

public sealed class AppVersionTests
{
    [Theory]
    [InlineData("0.1.0", 0, 1, 0, false)]
    [InlineData("v0.2.0", 0, 2, 0, false)]
    [InlineData("0.1.0+1a00b0e32ea0d0d6d220a81908e313fc9894cf7e", 0, 1, 0, false)]
    [InlineData("0.0.0-ci.44", 0, 0, 0, true)]
    [InlineData("1.2.3-rc.1+abc", 1, 2, 3, true)]
    [InlineData("10.20.30", 10, 20, 30, false)]
    public void Parses_a_version(string text, int major, int minor, int patch, bool preRelease)
    {
        Assert.Equal(new AppVersion(major, minor, patch, preRelease), AppVersion.Parse(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("v")]
    [InlineData("0.1")]
    [InlineData("0.1.0.0")]
    [InlineData("a.b.c")]
    [InlineData("0.1.0-")]
    [InlineData(" 0.1.0")]
    [InlineData("0.1.0 ")]
    [InlineData("-1.0.0")]
    [InlineData("+abc")]
    public void Rejects_what_is_not_a_version(string text)
    {
        Assert.Null(AppVersion.Parse(text));
    }

    [Fact]
    public void Orders_by_major_minor_and_patch_as_numbers()
    {
        string[] ordered = ["0.1.0", "0.1.1", "0.2.0", "0.10.0", "1.0.0"];
        var versions = ordered.Select(text => AppVersion.Parse(text)!.Value).ToList();

        for (var i = 1; i < versions.Count; i++)
        {
            Assert.True(versions[i - 1] < versions[i], $"{ordered[i - 1]} < {ordered[i]}");
            Assert.True(versions[i] > versions[i - 1], $"{ordered[i]} > {ordered[i - 1]}");
        }
    }

    [Fact]
    public void The_build_part_does_not_count()
    {
        Assert.Equal(AppVersion.Parse("0.1.0"), AppVersion.Parse("0.1.0+other"));
        Assert.Equal(0, AppVersion.Parse("0.1.0")!.Value.CompareTo(AppVersion.Parse("0.1.0+other")!.Value));
    }

    [Fact]
    public void Prints_major_minor_and_patch()
    {
        Assert.Equal("1.2.3", AppVersion.Parse("v1.2.3-rc.1+abc")!.Value.ToString());
    }
}
