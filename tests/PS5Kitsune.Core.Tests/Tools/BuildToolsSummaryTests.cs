using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.Tests.Tools;

public sealed class BuildToolsSummaryTests
{
    private static ToolDefinition Definition(string version, long? size) =>
        new(version, new Uri("https://example.test/t.zip"), new string('a', 64), size);

    private static AnyPS5Profile Profile(params (string Id, ToolDefinition Definition)[] tools) => new()
    {
        SchemaVersion = AnyPS5Profile.CurrentSchemaVersion,
        Id = "test",
        Source = new ProfileSource("release", Tag: "v1"),
        Tools = tools.ToDictionary(tool => tool.Id, tool => tool.Definition),
    };

    [Fact]
    public void Lists_the_build_tools_in_install_order_with_their_profile_data()
    {
        var profile = Profile(
            ("winlibs", Definition("15.2.0-r7", 300)),
            ("python", Definition("3.14.8", 10)),
            ("mingit", Definition("2.56.0.2", null)));
        var summary = new BuildToolsSummary(profile, [new FakeTool("winlibs", false), new FakeTool("python", true), new FakeTool("mingit", false)]);

        var tools = summary.Read();

        Assert.Equal(
            [
                new PortableToolInfo("python", "Python", "3.14.8", 10, true),
                new PortableToolInfo("mingit", "MinGit", "2.56.0.2", null, false),
                new PortableToolInfo("winlibs", "WinLibs", "15.2.0-r7", 300, false),
            ],
            tools);
    }

    [Fact]
    public void Leaves_out_a_tool_the_profile_or_the_container_lacks()
    {
        var profile = Profile(("python", Definition("3.14.8", 10)), ("mingit", Definition("2.56.0.2", 20)));
        var summary = new BuildToolsSummary(profile, [new FakeTool("python", true), new FakeTool("winlibs", false)]);

        Assert.Equal(["python"], summary.Read().Select(tool => tool.Id));
    }

    [Fact]
    public void Reads_the_installed_state_each_time()
    {
        var python = new FakeTool("python", false);
        var summary = new BuildToolsSummary(Profile(("python", Definition("3.14.8", 10))), [python]);
        Assert.False(summary.Read()[0].IsInstalled);

        python.Installed = true;

        Assert.True(summary.Read()[0].IsInstalled);
    }

    private sealed class FakeTool(string id, bool installed) : IPortableTool
    {
        public string Id => id;

        public bool Installed { get; set; } = installed;

        public bool IsInstalled() => Installed;

        public Task InstallAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<string> GetVersionAsync(CancellationToken cancellationToken = default) => Task.FromResult("1");
    }
}
