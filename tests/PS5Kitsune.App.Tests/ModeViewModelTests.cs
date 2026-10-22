using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.App.Tests;

public sealed class ModeViewModelTests
{
    private static readonly AnyPS5Profile BuildProfile = TestSessions.Profile with
    {
        Build = new ProfileBuild(new Uri("https://github.com/boykopovar/AnyPS5.git"), "3fd1cc393b8512c2e989860053f30dee5a8cd1aa"),
        Checks = new CheckThresholds { BuildMinFreeDiskGiB = 10 },
    };

    private readonly Navigator _navigator = new();
    private readonly FakeInstaller _installer = new();

    private ModeViewModel Create(AnyPS5Profile? profile = null) => new(profile ?? BuildProfile, _installer, _navigator, new FakePages());

    [Fact]
    public void Describes_the_release()
    {
        var viewModel = Create();

        Assert.Equal("Release v0.1.2", viewModel.ReleaseTitle);
        Assert.Equal("Published October 14, 2026", viewModel.PublishedText);
        Assert.Equal("Validated with DOOM for PS5", viewModel.ProfileStatusText);
    }

    [Fact]
    public void Says_when_the_date_or_validation_is_missing()
    {
        var viewModel = Create(BuildProfile with { Validated = false, Source = new ProfileSource("release", Tag: "v9") });

        Assert.Equal("Publication date unknown", viewModel.PublishedText);
        Assert.Equal("Not validated yet", viewModel.ProfileStatusText);
    }

    [Fact]
    public void Describes_the_build()
    {
        var viewModel = Create();

        Assert.True(viewModel.CanBuild);
        Assert.Equal("Commit 3fd1cc3", viewModel.BuildCommitText);
        Assert.Equal("About 15 minutes the first time, faster for updates.", viewModel.BuildDurationText);
        Assert.Equal("Needs about 10 GiB of free disk space.", viewModel.BuildDiskText);
    }

    [Theory]
    [InlineData(AnyPS5Mode.Release)]
    [InlineData(AnyPS5Mode.Build)]
    public void Preselects_the_installer_s_current_mode(AnyPS5Mode current)
    {
        _installer.CurrentMode = current;

        var viewModel = Create();

        Assert.Equal(current, viewModel.SelectedMode);
        Assert.Equal(current == AnyPS5Mode.Release, viewModel.IsReleaseSelected);
        Assert.Equal(current == AnyPS5Mode.Build, viewModel.IsBuildSelected);
    }

    [Fact]
    public void Without_a_build_the_card_is_disabled_and_release_is_preselected()
    {
        _installer.CurrentMode = AnyPS5Mode.Build;
        _installer.BuildSupported = false;

        var viewModel = Create(TestSessions.Profile);

        Assert.False(viewModel.CanBuild);
        Assert.False(viewModel.SelectBuildCommand.CanExecute(null));
        Assert.Equal(AnyPS5Mode.Release, viewModel.SelectedMode);
        Assert.Equal("", viewModel.BuildCommitText);
    }

    [Fact]
    public void Selecting_a_card_changes_the_mode()
    {
        var viewModel = Create();

        viewModel.SelectBuildCommand.Execute(null);
        Assert.True(viewModel.IsBuildSelected);

        viewModel.SelectReleaseCommand.Execute(null);
        Assert.True(viewModel.IsReleaseSelected);
    }

    [Theory]
    [InlineData(false, "check:Release")]
    [InlineData(true, "check:Build")]
    public void Continue_checks_the_pc_for_the_selected_mode(bool build, string next)
    {
        var viewModel = Create();
        if (build)
        {
            viewModel.SelectBuildCommand.Execute(null);
        }

        viewModel.ContinueCommand.Execute(null);

        Assert.Equal(next, _navigator.CurrentPage);
    }

    [Fact]
    public void Back_returns_home()
    {
        _navigator.GoTo("home");
        var viewModel = Create();
        _navigator.GoTo(viewModel);

        viewModel.BackCommand.Execute(null);

        Assert.Equal("home", _navigator.CurrentPage);
    }
}
