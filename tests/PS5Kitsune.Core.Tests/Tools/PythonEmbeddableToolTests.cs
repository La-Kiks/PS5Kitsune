using System.IO.Compression;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.Tests.Tools;

public sealed class PythonEmbeddableToolTests : IDisposable
{
    private static readonly ToolDefinition Definition = new("3.14.8", new Uri("https://example.test/python-embed.zip"), new string('a', 64));

    private readonly TempDirectory _temp = new();
    private readonly FakeDownloader _downloader = new();
    private readonly FakeProcessRunner _processRunner = new();

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
        _processRunner.Output = "Python 3.14.8";

        var version = await CreateTool().GetVersionAsync(TestContext.Current.CancellationToken);

        Assert.Equal("3.14.8", version);
        Assert.Equal(["--version"], _processRunner.LastSpec!.Arguments);
    }

    [Fact]
    public async Task GetVersion_fails_on_a_non_zero_exit_code()
    {
        _processRunner.ExitCode = 9009;

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateTool().GetVersionAsync(TestContext.Current.CancellationToken));
    }

    public void Dispose() => _temp.Dispose();

    private sealed class FakePlatform : IPlatform
    {
        public string Name => "Test";

        public string DefaultDataRoot => "unused";

        public string SettingsDirectory => "unused";

        public string ExecutableSuffix => ".exe";
    }

    /// <summary>Writes a small zip that looks like the embeddable Python archive.</summary>
    private sealed class FakeDownloader : IDownloader
    {
        public List<DownloadRequest> Requests { get; } = [];

        public Task DownloadAsync(DownloadRequest request, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            Directory.CreateDirectory(Path.GetDirectoryName(request.Destination)!);
            using var archive = ZipFile.Open(request.Destination, ZipArchiveMode.Create);
            using (var writer = new StreamWriter(archive.CreateEntry("python.exe").Open()))
            {
                writer.Write("fake");
            }

            archive.CreateEntry("python314.zip");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public string Output { get; set; } = "";

        public int ExitCode { get; set; }

        public ProcessSpec? LastSpec { get; private set; }

        public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<OutputLine>? onOutput = null, CancellationToken cancellationToken = default)
        {
            LastSpec = spec;
            onOutput?.Invoke(new OutputLine(OutputSource.StandardOutput, Output));
            return Task.FromResult(new ProcessResult(ExitCode, TimeSpan.Zero));
        }
    }
}
