using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.Tests.Tools;

public sealed class PythonEmbeddableToolTests : IDisposable
{
    private static readonly ToolDefinition Definition = new("3.14.8", new Uri("https://example.test/python-embed.zip"), new string('a', 64));

    private readonly TempDirectory _temp = new();
    private readonly FakeDownloader _downloader = new();
    private readonly FakeProcessRunner _processRunner = new();

    public PythonEmbeddableToolTests()
    {
        _downloader.Serve(Definition.Url, TestFiles.ZipBytes(("python.exe", "fake"u8.ToArray()), ("python314.zip", [])));
    }

    private PythonEmbeddableTool CreateTool() =>
        new(Definition, new DataLayout(_temp.Path), new FakePlatform(), _downloader, _processRunner);

    [Fact]
    public async Task Install_extracts_the_archive_into_tools_python()
    {
        var tool = CreateTool();
        Assert.False(tool.IsInstalled());

        await tool.InstallAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(tool.IsInstalled());
        Assert.True(File.Exists(_temp.Combine("tools", "python", "python.exe")));
        Assert.False(Directory.Exists(_temp.Combine("tools", "python.staging")));
        Assert.Equal(Definition.Sha256, _downloader.Requests.Single().ExpectedSha256);
    }

    [Fact]
    public async Task Install_does_nothing_when_already_installed()
    {
        var tool = CreateTool();
        await tool.InstallAsync(cancellationToken: TestContext.Current.CancellationToken);

        await tool.InstallAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(_downloader.Requests);
    }

    [Fact]
    public async Task A_tool_installed_from_another_archive_is_not_installed()
    {
        await CreateTool().InstallAsync(cancellationToken: TestContext.Current.CancellationToken);

        var newer = new PythonEmbeddableTool(
            Definition with { Sha256 = new string('b', 64) },
            new DataLayout(_temp.Path), new FakePlatform(), _downloader, _processRunner);

        Assert.False(newer.IsInstalled());
    }

    [Fact]
    public async Task A_deleted_executable_means_not_installed()
    {
        var tool = CreateTool();
        await tool.InstallAsync(cancellationToken: TestContext.Current.CancellationToken);

        File.Delete(tool.Executable);

        Assert.False(tool.IsInstalled());
    }

    [Fact]
    public async Task GetVersion_parses_the_python_banner()
    {
        _processRunner.Respond = _ => new FakeProcessResult(0, ["Python 3.14.8"]);

        var version = await CreateTool().GetVersionAsync(TestContext.Current.CancellationToken);

        Assert.Equal("3.14.8", version);
        Assert.Equal(["--version"], _processRunner.LastSpec!.Arguments);
    }

    [Fact]
    public async Task GetVersion_fails_on_a_non_zero_exit_code()
    {
        _processRunner.Respond = _ => new FakeProcessResult(9009);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateTool().GetVersionAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Scripts_run_through_a_bootstrap_that_keeps_their_folder_importable()
    {
        var tool = CreateTool();
        var script = _temp.Combine("scripts", "tools", "import_audit.py");

        var spec = tool.CreateScriptSpec(script, ["a.registry.json", "--libs", "libs"]);

        Assert.Equal(tool.Executable, spec.FileName);
        Assert.Equal("-c", spec.Arguments[0]);
        Assert.Contains("sys.path.insert(0", spec.Arguments[1], StringComparison.Ordinal);
        Assert.Contains("runpy.run_path", spec.Arguments[1], StringComparison.Ordinal);
        Assert.Equal([script, "a.registry.json", "--libs", "libs"], spec.Arguments.Skip(2));
        Assert.Equal("import_audit.py", FakeProcessRunner.ScriptName(spec));
    }

    public void Dispose() => _temp.Dispose();
}
