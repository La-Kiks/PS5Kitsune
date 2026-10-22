using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.AnyPS5;

public sealed class AnyPS5PathsTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly AnyPS5Paths _paths;

    public AnyPS5PathsTests()
    {
        _paths = new AnyPS5Paths(new DataLayout(_temp.Path), new FakePlatform());
    }

    [Fact]
    public void Each_mode_has_its_own_folder()
    {
        var build = _paths.For(AnyPS5Mode.Build);

        Assert.Equal(_temp.Combine("anyps5", "installs", "build"), build.RootDirectory);
        Assert.Equal(_temp.Combine("anyps5", "installs", "build", "relinker.exe"), build.Relinker);
        Assert.Equal(_temp.Combine("anyps5", "installs", "build", "libs"), build.Libs);
        Assert.Equal(_temp.Combine("anyps5", "installs", "build", "scripts"), build.Scripts);
        Assert.Equal(_temp.Combine("anyps5", "installs", "build", "scripts", "tools", "import_audit.py"), build.ImportAudit);
        Assert.Equal(_temp.Combine("anyps5", "installs", "build", "scripts", "tools", "unwrap_self.py"), build.UnwrapSelf);
        Assert.Equal(_temp.Combine("anyps5", "installs", "build", "install.json"), build.RecordFile);
        Assert.Equal(_temp.Combine("anyps5", "installs", "release"), _paths.For(AnyPS5Mode.Release).RootDirectory);
    }

    [Fact]
    public void The_active_view_follows_the_active_file()
    {
        Assert.Null(_paths.ActiveMode);
        Assert.Equal(_temp.Combine("anyps5", "installs", "release"), _paths.RootDirectory);

        _paths.WriteActiveMode(AnyPS5Mode.Build);

        Assert.Equal(_temp.Combine("anyps5", "state", "active"), _paths.ActiveModeFile);
        Assert.Equal("build", File.ReadAllText(_paths.ActiveModeFile));
        Assert.Equal(AnyPS5Mode.Build, _paths.ActiveMode);
        Assert.Equal(_paths.For(AnyPS5Mode.Build).Relinker, _paths.Relinker);
        Assert.Equal(_paths.For(AnyPS5Mode.Build).RecordFile, _paths.RecordFile);
    }

    [Theory]
    [InlineData("nightly")]
    [InlineData("")]
    public void An_unknown_active_file_means_no_active_install(string content)
    {
        TestFiles.Write(_paths.ActiveModeFile, content);

        Assert.Null(_paths.ActiveMode);
    }

    [Fact]
    public void The_legacy_folder_is_anyps5_current() =>
        Assert.Equal(_temp.Combine("anyps5", "current"), _paths.LegacyCurrent);

    public void Dispose() => _temp.Dispose();
}
