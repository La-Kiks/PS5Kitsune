using PS5Kitsune.Core.AnyPS5;

namespace PS5Kitsune.Core.Tests.AnyPS5;

public sealed class InstallModesTests
{
    private static readonly AnyPS5InstallRecord BuildRecord =
        new(2, "p", AnyPS5Mode.Build, "48d61f5", new string('a', 40), null, DateTimeOffset.UnixEpoch);

    private static readonly AnyPS5InstallRecord ReleaseRecord =
        new(2, "p", AnyPS5Mode.Release, "v0.1.2", null, new Uri("https://example.test/u.py"), DateTimeOffset.UnixEpoch);

    [Fact]
    public void The_option_wins()
    {
        Assert.Equal(new InstallModeChoice(AnyPS5Mode.Release, "--mode"), InstallModes.Resolve(AnyPS5Mode.Release, AnyPS5Mode.Build, BuildRecord));
    }

    [Fact]
    public void Without_the_option_an_unfinished_install_decides()
    {
        Assert.Equal(new InstallModeChoice(AnyPS5Mode.Build, "unfinished install"), InstallModes.Resolve(null, AnyPS5Mode.Build, ReleaseRecord));
    }

    [Fact]
    public void Without_the_option_the_current_install_decides()
    {
        Assert.Equal(new InstallModeChoice(AnyPS5Mode.Build, "current install"), InstallModes.Resolve(null, null, BuildRecord));
    }

    [Fact]
    public void Without_option_nor_install_release_is_the_default()
    {
        Assert.Equal(new InstallModeChoice(AnyPS5Mode.Release, "default"), InstallModes.Resolve(null, null, null));
    }

    [Theory]
    [InlineData(AnyPS5Mode.Release, "release")]
    [InlineData(AnyPS5Mode.Build, "build")]
    public void Modes_have_command_line_names(AnyPS5Mode mode, string name)
    {
        Assert.Equal(name, InstallModes.Name(mode));
    }
}
