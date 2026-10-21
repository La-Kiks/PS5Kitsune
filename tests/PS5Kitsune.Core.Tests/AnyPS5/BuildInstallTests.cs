using PS5Kitsune.Core.AnyPS5;

namespace PS5Kitsune.Core.Tests.AnyPS5;

public sealed class BuildInstallTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly AnyPS5TestSetup _setup;

    public BuildInstallTests()
    {
        _setup = new AnyPS5TestSetup(_temp.Path);
        _setup.PlayTheBuild();
    }

    [Fact]
    public void Build_mode_lists_its_steps_in_order()
    {
        Assert.Equal(
            [
                "Install Python", "Install MinGit", "Install WinLibs", "Get the AnyPS5 source", "Update the submodules",
                "Configure the build", "Compile AnyPS5", "Build the libraries", "Copy the build", "Record the install",
            ],
            _setup.Installer.CreateSteps(AnyPS5Mode.Build).Select(step => step.Name));
    }

    [Fact]
    public async Task A_build_install_fills_current_and_records_the_commit()
    {
        await _setup.InstallAsync(AnyPS5Mode.Build);

        Assert.Equal("built relinker", File.ReadAllText(_setup.Paths.Relinker));
        Assert.True(File.Exists(Path.Combine(_setup.Paths.Libs, "libSceAudioOut.prx")));
        Assert.True(File.Exists(_setup.Paths.UnwrapSelf));
        Assert.Equal(
            new AnyPS5InstallRecord(2, "test-profile", AnyPS5Mode.Build, "48d61f5", AnyPS5TestSetup.Commit, null, _setup.Time.GetLocalNow()),
            _setup.Installer.ReadRecord());
        Assert.Equal(AnyPS5InstallStatus.Installed, _setup.Installer.GetState(AnyPS5Mode.Build).Status);
        Assert.Equal(AnyPS5Mode.Build, _setup.Installer.CurrentMode);
    }

    [Fact]
    public async Task A_second_build_install_runs_nothing()
    {
        await _setup.InstallAsync(AnyPS5Mode.Build);
        var calls = _setup.Runner.Calls.Count;

        await _setup.InstallAsync(AnyPS5Mode.Build);

        Assert.Equal(calls, _setup.Runner.Calls.Count);
    }

    [Fact]
    public async Task A_new_commit_rebuilds_without_reinstalling_the_tools()
    {
        await _setup.InstallAsync(AnyPS5Mode.Build);
        var downloads = _setup.Downloader.Requests.Count;

        var updated = new AnyPS5TestSetup(_temp.Path, new string('c', 40));
        updated.PlayTheBuild("relinker of the new commit");
        await updated.InstallAsync(AnyPS5Mode.Build);

        Assert.Empty(updated.Downloader.Requests);
        Assert.Contains(updated.Runner.Calls, call => call.Arguments is ["fetch", "--depth", "1", "origin", var sha] && sha == new string('c', 40));
        Assert.Equal("relinker of the new commit", File.ReadAllText(updated.Paths.Relinker));
        Assert.Equal(new string('c', 40), updated.Installer.ReadRecord()?.Commit);
        Assert.True(downloads > 0);
    }

    [Fact]
    public async Task Going_back_to_release_replaces_current_and_keeps_the_build_folders()
    {
        await _setup.InstallAsync(AnyPS5Mode.Build);

        await _setup.InstallAsync(AnyPS5Mode.Release);

        Assert.Equal(AnyPS5TestSetup.RelinkerContent, File.ReadAllBytes(_setup.Paths.Relinker));
        Assert.False(File.Exists(Path.Combine(_setup.Paths.Libs, "libstdc++-6.dll")));
        Assert.True(File.Exists(_setup.BuildPaths.BuiltRelinker));
        Assert.Equal(AnyPS5Mode.Release, _setup.Installer.CurrentMode);
    }

    public void Dispose() => _temp.Dispose();
}
