using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Core.Tests.AnyPS5;

public sealed class AnyPS5InstallerTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly AnyPS5TestSetup _setup;

    public AnyPS5InstallerTests()
    {
        _setup = new AnyPS5TestSetup(_temp.Path);
    }

    private string Current => _temp.Combine("anyps5", "current");

    [Fact]
    public async Task Install_lays_out_anyps5_current()
    {
        Assert.False(_setup.Installer.IsInstalled());

        await _setup.InstallAsync();

        Assert.Equal(AnyPS5TestSetup.RelinkerContent, File.ReadAllBytes(Path.Combine(Current, "relinker.exe")));
        Assert.True(File.Exists(Path.Combine(Current, "libs", "libSceAudioOut.prx")));
        Assert.True(File.Exists(Path.Combine(Current, "libs", "anyps5.dll")));
        Assert.False(File.Exists(Path.Combine(Current, "libs", "README.md")));
        Assert.True(File.Exists(Path.Combine(Current, "scripts", "tools", "import_audit.py")));
        Assert.True(File.Exists(Path.Combine(Current, "scripts", "tools", "nid_names.py")));
        Assert.True(File.Exists(Path.Combine(Current, "scripts", "core", "libs", "prx", "libc", "stub.cpp")));
        Assert.False(Directory.Exists(Path.Combine(Current, "scripts", "docs")));
        Assert.Equal(AnyPS5TestSetup.UnwrapScriptContent, File.ReadAllBytes(Path.Combine(Current, "scripts", "tools", "unwrap_self.py")));
        Assert.True(File.Exists(_temp.Combine("tools", "python", "python.exe")));
        Assert.True(_setup.Installer.IsInstalled());
    }

    [Fact]
    public async Task Install_writes_the_install_record()
    {
        await _setup.InstallAsync();

        var record = JsonFiles.Read<AnyPS5InstallRecord>(Path.Combine(Current, "install.json"));

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
    public async Task The_current_mode_comes_from_the_record()
    {
        Assert.Equal(AnyPS5Mode.Release, _setup.Installer.CurrentMode);
        await _setup.InstallAsync();
        var record = _setup.Installer.ReadRecord()!;
        await JsonFiles.WriteAsync(_setup.Paths.RecordFile, record with { Mode = AnyPS5Mode.Build }, TestContext.Current.CancellationToken);

        Assert.Equal(AnyPS5Mode.Build, _setup.Installer.CurrentMode);
        Assert.Equal(AnyPS5InstallStatus.Incomplete, _setup.Installer.GetState(AnyPS5Mode.Release).Status);
    }

    [Fact]
    public async Task Installing_another_mode_empties_current_first()
    {
        await _setup.InstallAsync();
        var record = _setup.Installer.ReadRecord()!;
        await JsonFiles.WriteAsync(_setup.Paths.RecordFile, record with { Mode = AnyPS5Mode.Build }, TestContext.Current.CancellationToken);
        TestFiles.Write(Path.Combine(Current, "libs", "libTracyClient.dll"), "left by the other mode");

        await _setup.InstallAsync();

        Assert.False(File.Exists(Path.Combine(Current, "libs", "libTracyClient.dll")));
        Assert.Equal(AnyPS5InstallStatus.Installed, _setup.Installer.GetState(AnyPS5Mode.Release).Status);
    }

    [Fact]
    public void A_mode_without_a_source_is_refused()
    {
        var releaseOnly = new AnyPS5Installer([_setup.ReleaseSource], _setup.PythonTool, _setup.Paths, _setup.StepRunner, _setup.Time);

        var exception = Assert.Throws<InvalidOperationException>(() => releaseOnly.CreateSteps(AnyPS5Mode.Build));

        Assert.Contains("build", exception.Message, StringComparison.Ordinal);
    }
}
