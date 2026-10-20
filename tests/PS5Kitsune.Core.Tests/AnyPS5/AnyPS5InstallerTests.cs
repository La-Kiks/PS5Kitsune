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
            new AnyPS5InstallRecord(1, "test-profile", "release", "v0.1.2", AnyPS5TestSetup.UnwrapScriptUrl, _setup.Time.GetLocalNow()),
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

    public void Dispose() => _temp.Dispose();
}
