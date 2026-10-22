using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core;
using PS5Kitsune.Core.AnyPS5;

namespace PS5Kitsune.App.Tests;

public sealed class DoneViewModelTests
{
    private static readonly AnyPS5InstallRecord Record = new(1, "test-profile", AnyPS5Mode.Release, "v0.1.2", null, new Uri("https://example.test/u.py"), DateTimeOffset.UnixEpoch);

    private readonly TestPlatform _platform = new();
    private readonly Navigator _navigator = new();
    private readonly AnyPS5Paths _paths;

    public DoneViewModelTests()
    {
        _paths = new AnyPS5Paths(new DataLayout(Path.Combine(Path.GetTempPath(), "root")), _platform);
    }

    private DoneViewModel Create(AnyPS5InstallRecord? record = null) =>
        new(record ?? Record, _paths, "app.log", _platform, _navigator, new FakePages());

    [Fact]
    public void Shows_the_version_and_the_paths()
    {
        var viewModel = Create();

        Assert.Equal("AnyPS5 v0.1.2 is installed", viewModel.Title);
        Assert.Equal(_paths.Relinker, viewModel.Relinker);
        Assert.Equal(_paths.Libs, viewModel.Libs);
    }

    [Fact]
    public void Opens_the_folder_and_the_log()
    {
        var viewModel = Create();

        viewModel.OpenFolderCommand.Execute(null);
        viewModel.OpenLogCommand.Execute(null);

        Assert.Equal([_paths.RootDirectory, "app.log"], _platform.OpenedPaths);
    }

    [Fact]
    public void Back_to_home_clears_the_history()
    {
        _navigator.GoTo("install");
        var viewModel = Create();

        viewModel.BackHomeCommand.Execute(null);
        _navigator.GoBack();

        Assert.Equal("home", _navigator.CurrentPage);
    }

    [Fact]
    public void A_build_names_its_commit()
    {
        var build = new AnyPS5InstallRecord(2, "test-profile", AnyPS5Mode.Build, "3fd1cc3", "3fd1cc393b8512c2e989860053f30dee5a8cd1aa", null, DateTimeOffset.UnixEpoch);

        var viewModel = Create(build);

        Assert.Equal("AnyPS5 3fd1cc3 is installed", viewModel.Title);
        Assert.Equal("Built from source, commit 3fd1cc3.", viewModel.BuildText);
    }

    [Fact]
    public void A_release_has_no_build_line()
    {
        Assert.Null(Create().BuildText);
    }
}
