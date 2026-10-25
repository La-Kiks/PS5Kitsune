using PS5Kitsune.App.Controls;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.App.Tests;

public sealed class ToolsViewModelTests
{
    private readonly Navigator _navigator = new();
    private readonly FakeTool _python = new("python", true);
    private readonly FakeTool _mingit = new("mingit", false);
    private readonly FakeTool _winlibs = new("winlibs", false);

    private ToolsViewModel Create(long? winLibsSize = 267593703)
    {
        var profile = TestSessions.Profile with
        {
            Tools = new Dictionary<string, ToolDefinition>
            {
                ["python"] = Definition("3.14.8", 12601679),
                ["mingit"] = Definition("2.56.0.2", 39806486),
                ["winlibs"] = Definition("15.2.0-r7", winLibsSize),
            },
        };
        var viewModel = new ToolsViewModel(new BuildToolsSummary(profile, [_python, _mingit, _winlibs]), _navigator, new FakePages());
        viewModel.OnNavigatedTo();
        return viewModel;
    }

    private static ToolDefinition Definition(string version, long? size) =>
        new(version, new Uri("https://example.test/t.zip"), new string('a', 64), size);

    [Fact]
    public void Shows_each_tool_with_its_state_and_size()
    {
        var viewModel = Create();

        Assert.Equal(["Python", "MinGit", "WinLibs"], viewModel.Tools.Select(tool => tool.Name));
        Assert.Equal(["3.14.8", "2.56.0.2", "15.2.0-r7"], viewModel.Tools.Select(tool => tool.Version));
        Assert.Equal(["Installed", "Missing", "Missing"], viewModel.Tools.Select(tool => tool.StatusText));
        Assert.Equal(["", "38.0 MB", "255.2 MB"], viewModel.Tools.Select(tool => tool.SizeText));
    }

    [Fact]
    public void An_installed_tool_is_green_and_a_missing_one_neutral()
    {
        var viewModel = Create();

        Assert.Equal([StatusKind.Green, StatusKind.Neutral, StatusKind.Neutral], viewModel.Tools.Select(tool => tool.Kind));
    }

    [Fact]
    public void Totals_the_missing_tools()
    {
        var viewModel = Create();

        Assert.Equal("To download: 293.2 MB", viewModel.DownloadText);
        Assert.Equal("Disk space for the build: about 3 GB.", viewModel.DiskText);
        Assert.Equal("Download what's missing and build", viewModel.ContinueText);
    }

    [Fact]
    public void A_missing_tool_without_a_size_makes_the_total_a_minimum()
    {
        var viewModel = Create(winLibsSize: null);

        Assert.Equal("size unknown", viewModel.Tools[2].SizeText);
        Assert.Equal("To download: at least 38.0 MB", viewModel.DownloadText);
    }

    [Fact]
    public void Only_tools_without_a_size_missing_gives_an_unknown_total()
    {
        _mingit.Installed = true;
        var viewModel = Create(winLibsSize: null);

        Assert.Equal("To download: size unknown", viewModel.DownloadText);
    }

    [Fact]
    public void Nothing_missing_says_so_and_offers_to_build()
    {
        _mingit.Installed = true;
        _winlibs.Installed = true;

        var viewModel = Create();

        Assert.Equal("All tools are present.", viewModel.DownloadText);
        Assert.Equal("Build", viewModel.ContinueText);
    }

    [Fact]
    public void Entering_the_page_reads_the_tools_again()
    {
        var viewModel = Create();
        _mingit.Installed = true;
        _winlibs.Installed = true;

        viewModel.OnNavigatedTo();

        Assert.Equal("All tools are present.", viewModel.DownloadText);
        Assert.Equal(3, viewModel.Tools.Count);
    }

    [Fact]
    public void Continue_installs_in_build_mode()
    {
        Create().ContinueCommand.Execute(null);

        Assert.Equal("install:Build", _navigator.CurrentPage);
    }

    [Fact]
    public void Back_returns_to_the_previous_page()
    {
        _navigator.GoTo("check:Build");
        var viewModel = Create();
        _navigator.GoTo(viewModel);

        viewModel.BackCommand.Execute(null);

        Assert.Equal("check:Build", _navigator.CurrentPage);
    }
}
