using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.Tests.Tools;

public sealed class BuildToolsTests : IDisposable
{
    private static readonly byte[] MinGitArchive = TestFiles.ZipBytes(
        ("cmd/git.exe", "git"u8.ToArray()),
        ("ucrt64/etc/ssl/certs/ca-bundle.crt", "certs"u8.ToArray()));

    private static readonly byte[] WinLibsArchive = TestFiles.ZipBytes(
        ("mingw64/bin/gcc.exe", "gcc"u8.ToArray()),
        ("mingw64/bin/g++.exe", "g++"u8.ToArray()),
        ("mingw64/bin/cmake.exe", "cmake"u8.ToArray()),
        ("mingw64/bin/ninja.exe", "ninja"u8.ToArray()));

    private static readonly ToolDefinition MinGitDefinition = new("2.56.0.2", new Uri("https://example.test/mingit.zip"), TestFiles.Sha256(MinGitArchive));

    private static readonly ToolDefinition WinLibsDefinition = new("15.2.0-r7", new Uri("https://example.test/winlibs.zip"), TestFiles.Sha256(WinLibsArchive));

    private readonly TempDirectory _temp = new();
    private readonly FakeDownloader _downloader = new();
    private readonly FakeProcessRunner _runner = new();

    public BuildToolsTests()
    {
        _downloader.Serve(MinGitDefinition.Url, MinGitArchive);
        _downloader.Serve(WinLibsDefinition.Url, WinLibsArchive);
    }

    private DataLayout Layout => new(_temp.Path);

    private MinGitTool MinGit() => new(MinGitDefinition, Layout, new FakePlatform(), _downloader, _runner);

    private WinLibsTool WinLibs() => new(WinLibsDefinition, Layout, new FakePlatform(), _downloader, _runner);

    [Fact]
    public async Task MinGit_installs_into_tools_mingit()
    {
        var tool = MinGit();

        await tool.InstallAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(tool.IsInstalled());
        Assert.Equal(_temp.Combine("tools", "mingit", "cmd", "git.exe"), tool.Executable);
        Assert.Equal(_temp.Combine("tools", "mingit", "ucrt64", "etc", "ssl", "certs", "ca-bundle.crt"), tool.CaBundle);
    }

    [Fact]
    public async Task WinLibs_is_not_installed_without_ninja()
    {
        var tool = WinLibs();
        await tool.InstallAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(tool.IsInstalled());

        File.Delete(tool.Ninja);

        Assert.False(tool.IsInstalled());
    }

    [Fact]
    public void The_CA_bundle_falls_back_to_mingw64()
    {
        TestFiles.Write(_temp.Combine("tools", "mingit", "mingw64", "etc", "ssl", "certs", "ca-bundle.crt"), "certs");

        Assert.Equal(_temp.Combine("tools", "mingit", "mingw64", "etc", "ssl", "certs", "ca-bundle.crt"), MinGit().CaBundle);
    }

    [Fact]
    public void A_missing_CA_bundle_names_both_places()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => MinGit().CaBundle);

        Assert.Contains("ucrt64", exception.Message, StringComparison.Ordinal);
        Assert.Contains("mingw64", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Versions_are_read_from_the_tools()
    {
        _runner.Respond = spec => Path.GetFileName(spec.FileName) == "git.exe"
            ? new FakeProcessResult(0, ["git version 2.56.0.windows.2"])
            : new FakeProcessResult(0, ["15.2.0"]);

        Assert.Equal("2.56.0.windows.2", await MinGit().GetVersionAsync(TestContext.Current.CancellationToken));
        Assert.Equal("15.2.0", await WinLibs().GetVersionAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["-dumpfullversion"], _runner.LastSpec!.Arguments);
    }

    [Fact]
    public async Task The_build_environment_puts_the_tools_first_and_sets_the_CA_bundle()
    {
        var mingit = MinGit();
        var winlibs = WinLibs();
        await mingit.InstallAsync(cancellationToken: TestContext.Current.CancellationToken);

        var variables = new BuildEnvironment(mingit, winlibs).Variables();

        var path = variables["PATH"]!.Split(Path.PathSeparator);
        Assert.Equal(winlibs.Bin, path[0]);
        Assert.Equal(mingit.CommandDirectory, path[1]);
        Assert.Equal(mingit.CaBundle, variables["SSL_CERT_FILE"]);
    }

    public void Dispose() => _temp.Dispose();
}
