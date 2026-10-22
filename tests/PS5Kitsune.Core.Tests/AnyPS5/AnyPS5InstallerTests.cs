using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.Tests.AnyPS5;

public sealed class AnyPS5InstallerTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly AnyPS5TestSetup _setup;

    public AnyPS5InstallerTests()
    {
        _setup = new AnyPS5TestSetup(_temp.Path);
    }

    private string Release => _temp.Combine("anyps5", "installs", "release");

    [Fact]
    public async Task Install_lays_out_installs_release()
    {
        Assert.False(_setup.Installer.IsInstalled());

        await _setup.InstallAsync();

        Assert.Equal(AnyPS5TestSetup.RelinkerContent, File.ReadAllBytes(Path.Combine(Release, "relinker.exe")));
        Assert.True(File.Exists(Path.Combine(Release, "libs", "libSceAudioOut.prx")));
        Assert.True(File.Exists(Path.Combine(Release, "libs", "anyps5.dll")));
        Assert.False(File.Exists(Path.Combine(Release, "libs", "README.md")));
        Assert.True(File.Exists(Path.Combine(Release, "scripts", "tools", "import_audit.py")));
        Assert.True(File.Exists(Path.Combine(Release, "scripts", "tools", "nid_names.py")));
        Assert.True(File.Exists(Path.Combine(Release, "scripts", "core", "libs", "prx", "libc", "stub.cpp")));
        Assert.False(Directory.Exists(Path.Combine(Release, "scripts", "docs")));
        Assert.Equal(AnyPS5TestSetup.UnwrapScriptContent, File.ReadAllBytes(Path.Combine(Release, "scripts", "tools", "unwrap_self.py")));
        Assert.True(File.Exists(_temp.Combine("tools", "python", "python.exe")));
        Assert.True(_setup.Installer.IsInstalled());
    }

    [Fact]
    public async Task Install_writes_the_install_record()
    {
        await _setup.InstallAsync();

        var record = JsonFiles.Read<AnyPS5InstallRecord>(Path.Combine(Release, "install.json"));

        Assert.Equal(
            new AnyPS5InstallRecord(2, "test-profile", AnyPS5Mode.Release, "v0.1.2", null, AnyPS5TestSetup.UnwrapScriptUrl, _setup.Time.GetLocalNow()),
            record);
    }

    [Fact]
    public async Task Release_assets_are_downloaded_from_the_source_tag()
    {
        await _setup.InstallAsync();

        Assert.Contains(_setup.Downloader.Requests, request =>
            request.Source == new Uri("https://github.com/boykopovar/AnyPS5/releases/download/v0.1.2/relinker-v0.1.2.exe"));
        Assert.Contains(_setup.Downloader.Requests, request =>
            request.Source == new Uri("https://github.com/boykopovar/AnyPS5/releases/download/v0.1.2/prx-windows-v0.1.2.zip"));
    }

    [Fact]
    public async Task A_second_install_downloads_nothing()
    {
        await _setup.InstallAsync();
        var requests = _setup.Downloader.Requests.Count;

        await _setup.InstallAsync();

        Assert.Equal(requests, _setup.Downloader.Requests.Count);
    }

    [Fact]
    public async Task A_modified_relinker_is_downloaded_again()
    {
        await _setup.InstallAsync();
        File.WriteAllText(_setup.Paths.Relinker, "tampered");
        Assert.False(_setup.Installer.IsInstalled());
        var before = _setup.Downloader.Requests.Count;

        await _setup.InstallAsync();

        var request = Assert.Single(_setup.Downloader.Requests.Skip(before));
        Assert.EndsWith("relinker-v0.1.2.exe", request.Source.AbsolutePath, StringComparison.Ordinal);
        Assert.True(_setup.Installer.IsInstalled());
    }

    [Fact]
    public void An_empty_root_is_not_installed()
    {
        var state = _setup.Installer.GetState(AnyPS5Mode.Release);

        Assert.Equal(AnyPS5InstallStatus.NotInstalled, state.Status);
        Assert.Null(state.Record);
    }

    [Fact]
    public async Task A_complete_install_is_installed_with_its_record()
    {
        await _setup.InstallAsync();

        var state = _setup.Installer.GetState(AnyPS5Mode.Release);

        Assert.Equal(AnyPS5InstallStatus.Installed, state.Status);
        Assert.Equal("v0.1.2", state.Record?.Version);
        Assert.Equal("test-profile", state.Record?.Profile);
    }

    [Fact]
    public async Task A_missing_file_makes_the_install_incomplete()
    {
        await _setup.InstallAsync();
        File.Delete(_setup.Paths.Relinker);

        Assert.Equal(AnyPS5InstallStatus.Incomplete, _setup.Installer.GetState(AnyPS5Mode.Release).Status);
    }

    [Fact]
    public async Task A_record_for_another_tag_makes_the_install_incomplete()
    {
        await _setup.InstallAsync();
        var record = JsonFiles.Read<AnyPS5InstallRecord>(_setup.Paths.RecordFile)!;
        await JsonFiles.WriteAsync(_setup.Paths.RecordFile, record with { Version = "v0.1.1" }, TestContext.Current.CancellationToken);

        var state = _setup.Installer.GetState(AnyPS5Mode.Release);

        Assert.Equal(AnyPS5InstallStatus.Incomplete, state.Status);
        Assert.Equal("v0.1.1", state.Record?.Version);
    }

    [Fact]
    public async Task An_unreadable_record_is_ignored()
    {
        await _setup.InstallAsync();
        await File.WriteAllTextAsync(_setup.Paths.RecordFile, "{ not json", TestContext.Current.CancellationToken);

        var state = _setup.Installer.GetState(AnyPS5Mode.Release);

        Assert.Equal(AnyPS5InstallStatus.Incomplete, state.Status);
        Assert.Null(state.Record);
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task A_schema_1_record_still_counts_as_installed()
    {
        await _setup.InstallAsync();
        await File.WriteAllTextAsync(_setup.Paths.RecordFile, $$"""
            {
              "schemaVersion": 1, "profile": "test-profile", "mode": "release", "version": "v0.1.2",
              "unwrapScript": "{{AnyPS5TestSetup.UnwrapScriptUrl}}", "installedAt": "2026-10-20T12:00:00+00:00"
            }
            """, TestContext.Current.CancellationToken);

        var state = _setup.Installer.GetState(AnyPS5Mode.Release);

        Assert.Equal(AnyPS5InstallStatus.Installed, state.Status);
        Assert.Equal(AnyPS5Mode.Release, state.Record?.Mode);
        Assert.Null(state.Record?.Commit);
    }

    [Fact]
    public async Task A_successful_install_becomes_the_active_one()
    {
        Assert.Null(_setup.Installer.ActiveMode);

        await _setup.InstallAsync();

        Assert.Equal(AnyPS5Mode.Release, _setup.Installer.ActiveMode);
        Assert.Equal("release", File.ReadAllText(_setup.Paths.ActiveModeFile));
        Assert.Equal("v0.1.2", _setup.Installer.ReadRecord()?.Version);
        Assert.Equal("v0.1.2", _setup.Installer.ReadRecord(AnyPS5Mode.Release)?.Version);
        Assert.Null(_setup.Installer.ReadRecord(AnyPS5Mode.Build));
    }

    [Fact]
    public void No_step_clears_the_other_mode()
    {
        Assert.Equal("Install Python", _setup.Installer.CreateSteps(AnyPS5Mode.Release)[0].Name);
        Assert.Equal("Install Python", _setup.Installer.CreateSteps(AnyPS5Mode.Build)[0].Name);
    }

    [Fact]
    public async Task The_current_mode_is_the_requested_then_the_active_then_release()
    {
        Assert.Equal(AnyPS5Mode.Release, _setup.Installer.CurrentMode);
        _setup.Paths.WriteActiveMode(AnyPS5Mode.Build);
        Assert.Equal(AnyPS5Mode.Build, _setup.Installer.CurrentMode);

        Directory.CreateDirectory(Path.GetDirectoryName(_setup.Paths.RequestedModeFile)!);
        await File.WriteAllTextAsync(_setup.Paths.RequestedModeFile, "release", TestContext.Current.CancellationToken);

        Assert.Equal(AnyPS5Mode.Release, _setup.Installer.CurrentMode);
    }

    [Fact]
    public async Task Use_switches_without_running_a_step()
    {
        await _setup.InstallAsync();
        var record = _setup.Installer.ReadRecord()!;
        await JsonFiles.WriteAsync(_setup.Paths.For(AnyPS5Mode.Build).RecordFile, record with { Mode = AnyPS5Mode.Build, Version = "48d61f5" }, TestContext.Current.CancellationToken);
        var requests = _setup.Downloader.Requests.Count;
        var calls = _setup.Runner.Calls.Count;

        _setup.Installer.Use(AnyPS5Mode.Build);

        Assert.Equal(AnyPS5Mode.Build, _setup.Installer.ActiveMode);
        Assert.Equal("48d61f5", _setup.Installer.ReadRecord()?.Version);
        Assert.Equal(_setup.Paths.For(AnyPS5Mode.Build).RootDirectory, _setup.Paths.RootDirectory);
        Assert.Equal(requests, _setup.Downloader.Requests.Count);
        Assert.Equal(calls, _setup.Runner.Calls.Count);
    }

    [Fact]
    public async Task Use_refuses_a_mode_whose_install_did_not_finish()
    {
        await _setup.InstallAsync();
        var record = _setup.Installer.ReadRecord()!;
        await JsonFiles.WriteAsync(_setup.Paths.For(AnyPS5Mode.Build).RecordFile, record with { Mode = AnyPS5Mode.Build, Version = "48d61f5" }, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(_setup.Paths.RequestedModeFile, "build", TestContext.Current.CancellationToken);

        var exception = Assert.Throws<InvalidOperationException>(() => _setup.Installer.Use(AnyPS5Mode.Build));

        Assert.Equal("The build install did not finish; run install --mode build to resume it.", exception.Message);
        Assert.Equal(AnyPS5Mode.Release, _setup.Installer.ActiveMode);
    }

    [Fact]
    public async Task Use_refuses_an_install_that_never_finished()
    {
        await _setup.InstallAsync();

        var exception = Assert.Throws<InvalidOperationException>(() => _setup.Installer.Use(AnyPS5Mode.Build));

        Assert.Equal("The build install is not finished; run install --mode build first.", exception.Message);
        Assert.Equal(AnyPS5Mode.Release, _setup.Installer.ActiveMode);
    }

    [Fact]
    public async Task The_state_of_a_mode_ignores_the_active_install()
    {
        await _setup.InstallAsync();
        _setup.Paths.WriteActiveMode(AnyPS5Mode.Build);

        var state = _setup.Installer.GetState(AnyPS5Mode.Release);

        Assert.Equal(AnyPS5InstallStatus.Installed, state.Status);
        Assert.Equal("v0.1.2", state.Record?.Version);
        Assert.False(_setup.Installer.IsInstalled());
    }

    [Fact]
    public async Task An_unfinished_install_of_the_other_mode_keeps_the_active_one_usable()
    {
        await _setup.InstallAsync();
        Directory.CreateDirectory(Path.GetDirectoryName(_setup.Paths.RequestedModeFile)!);
        await File.WriteAllTextAsync(_setup.Paths.RequestedModeFile, "build", TestContext.Current.CancellationToken);

        Assert.Equal(AnyPS5Mode.Build, _setup.Installer.CurrentMode);
        Assert.True(_setup.Installer.IsInstalled());
    }

    [Fact]
    public async Task A_requested_mode_file_decides_the_current_mode()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_setup.Paths.RequestedModeFile)!);
        await File.WriteAllTextAsync(_setup.Paths.RequestedModeFile, "build", TestContext.Current.CancellationToken);

        Assert.Equal(_temp.Combine("anyps5", "state", "requested-mode"), _setup.Paths.RequestedModeFile);
        Assert.Equal(AnyPS5Mode.Build, _setup.Installer.RequestedMode);
        Assert.Equal(AnyPS5Mode.Build, _setup.Installer.CurrentMode);
    }

    [Fact]
    public async Task An_unknown_requested_mode_is_ignored()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_setup.Paths.RequestedModeFile)!);
        await File.WriteAllTextAsync(_setup.Paths.RequestedModeFile, "nightly", TestContext.Current.CancellationToken);

        Assert.Null(_setup.Installer.RequestedMode);
        Assert.Equal(AnyPS5Mode.Release, _setup.Installer.CurrentMode);
    }

    [Fact]
    public async Task A_mode_without_a_source_is_incomplete_instead_of_throwing()
    {
        var releaseOnly = new AnyPS5Installer([_setup.ReleaseSource], _setup.PythonTool, _setup.Paths, _setup.StepRunner, _setup.Time);
        await _setup.InstallAsync();
        var record = _setup.Installer.ReadRecord()!;
        await JsonFiles.WriteAsync(_setup.Paths.For(AnyPS5Mode.Build).RecordFile, record with { Mode = AnyPS5Mode.Build }, TestContext.Current.CancellationToken);
        _setup.Paths.WriteActiveMode(AnyPS5Mode.Build);

        var state = releaseOnly.GetState(releaseOnly.CurrentMode);

        Assert.Equal(AnyPS5Mode.Build, releaseOnly.CurrentMode);
        Assert.Equal(AnyPS5InstallStatus.Incomplete, state.Status);
        Assert.Equal(AnyPS5Mode.Build, state.Record?.Mode);
    }

    [Fact]
    public async Task A_requested_mode_without_a_source_is_incomplete_instead_of_throwing()
    {
        var releaseOnly = new AnyPS5Installer([_setup.ReleaseSource], _setup.PythonTool, _setup.Paths, _setup.StepRunner, _setup.Time);
        Directory.CreateDirectory(Path.GetDirectoryName(_setup.Paths.RequestedModeFile)!);
        await File.WriteAllTextAsync(_setup.Paths.RequestedModeFile, "build", TestContext.Current.CancellationToken);

        Assert.Equal(AnyPS5InstallStatus.Incomplete, releaseOnly.GetState(AnyPS5Mode.Build).Status);
        Assert.Throws<InvalidOperationException>(() => releaseOnly.CreateSteps(AnyPS5Mode.Build));
    }

    [Fact]
    public void A_mode_without_a_source_is_refused()
    {
        var releaseOnly = new AnyPS5Installer([_setup.ReleaseSource], _setup.PythonTool, _setup.Paths, _setup.StepRunner, _setup.Time);

        var exception = Assert.Throws<InvalidOperationException>(() => releaseOnly.CreateSteps(AnyPS5Mode.Build));

        Assert.Contains("build", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Says_which_modes_the_profile_supports()
    {
        var releaseOnly = new AnyPS5Installer([_setup.ReleaseSource], _setup.PythonTool, _setup.Paths, _setup.StepRunner, _setup.Time);

        Assert.True(releaseOnly.Supports(AnyPS5Mode.Release));
        Assert.False(releaseOnly.Supports(AnyPS5Mode.Build));
    }
}
