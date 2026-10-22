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

    private static AnyPS5InstallState Installed(AnyPS5Mode mode, string version) =>
        new(AnyPS5InstallStatus.Installed, new AnyPS5InstallRecord(2, "test-profile", mode, version, null, null, DateTimeOffset.UnixEpoch));

    [Fact]
    public void A_release_install_is_named_on_its_card_and_repaired_from_the_button()
    {
        _installer.ActiveMode = AnyPS5Mode.Release;
        _installer.State = Installed(AnyPS5Mode.Release, "v0.1.2");

        var viewModel = Create();

        Assert.Equal("Installed: v0.1.2", viewModel.ReleaseInstalledText);
        Assert.Null(viewModel.BuildInstalledText);
        Assert.Equal("Verify / repair", viewModel.ContinueText);

        viewModel.SelectBuildCommand.Execute(null);

        Assert.Equal("Continue", viewModel.ContinueText);
    }

    [Fact]
    public void A_build_install_is_named_on_its_card_and_repaired_from_the_button()
    {
        _installer.ActiveMode = AnyPS5Mode.Build;
        _installer.CurrentMode = AnyPS5Mode.Build;
        _installer.State = Installed(AnyPS5Mode.Build, "3fd1cc3");

        var viewModel = Create();

        Assert.Equal("Installed: 3fd1cc3", viewModel.BuildInstalledText);
        Assert.Null(viewModel.ReleaseInstalledText);
        Assert.Equal("Verify / repair", viewModel.ContinueText);
    }

    [Fact]
    public void An_unfinished_install_is_resumed()
    {
        _installer.CurrentMode = AnyPS5Mode.Build;
        _installer.RequestedMode = AnyPS5Mode.Build;

        var viewModel = Create();

        Assert.Equal("Resume the install", viewModel.ContinueText);
    }

    private static readonly AnyPS5InstallRecord ReleaseRecord = new(2, "test-profile", AnyPS5Mode.Release, "v0.1.2", null, new Uri("https://example.test/u.py"), default);

    private static readonly AnyPS5InstallRecord BuildRecord = new(2, "test-profile", AnyPS5Mode.Build, "3fd1cc3", "3fd1cc393b8512c2e989860053f30dee5a8cd1aa", null, default);

    [Fact]
    public void Both_installs_show_their_version_and_the_active_one_is_in_use()
    {
        _installer.Records[AnyPS5Mode.Release] = ReleaseRecord;
        _installer.Records[AnyPS5Mode.Build] = BuildRecord;
        _installer.ActiveMode = AnyPS5Mode.Release;

        var viewModel = Create();

        Assert.Equal("Installed: v0.1.2", viewModel.ReleaseInstalledText);
        Assert.Equal("Installed: 3fd1cc3", viewModel.BuildInstalledText);
        Assert.True(viewModel.IsReleaseInUse);
        Assert.False(viewModel.IsBuildInUse);
    }

    [Fact]
    public void The_button_names_what_happens_to_the_selected_install()
    {
        _installer.Records[AnyPS5Mode.Release] = ReleaseRecord;
        _installer.Records[AnyPS5Mode.Build] = BuildRecord;
        _installer.ActiveMode = AnyPS5Mode.Release;
        _installer.CurrentMode = AnyPS5Mode.Release;
        var viewModel = Create();

        Assert.Equal("Verify / repair", viewModel.ContinueText);
        viewModel.SelectBuildCommand.Execute(null);
        Assert.Equal("Use this install", viewModel.ContinueText);
    }

    [Fact]
    public void An_unfinished_install_is_resumed_before_anything_else()
    {
        _installer.Records[AnyPS5Mode.Build] = BuildRecord;
        _installer.ActiveMode = AnyPS5Mode.Build;
        _installer.RequestedMode = AnyPS5Mode.Build;
        _installer.CurrentMode = AnyPS5Mode.Build;

        Assert.Equal("Resume the install", Create().ContinueText);
    }

    [Fact]
    public void A_mode_never_installed_continues_to_the_check()
    {
        _installer.Records[AnyPS5Mode.Release] = ReleaseRecord;
        _installer.ActiveMode = AnyPS5Mode.Release;
        var viewModel = Create();

        viewModel.SelectBuildCommand.Execute(null);
        Assert.Equal("Continue", viewModel.ContinueText);
        viewModel.ContinueCommand.Execute(null);

        Assert.Equal("check:Build", _navigator.CurrentPage);
        Assert.Empty(_installer.Used);
    }

    [Fact]
    public void Use_this_install_switches_and_goes_home()
    {
        _installer.Records[AnyPS5Mode.Release] = ReleaseRecord;
        _installer.Records[AnyPS5Mode.Build] = BuildRecord;
        _installer.ActiveMode = AnyPS5Mode.Release;
        var viewModel = Create();

        viewModel.SelectBuildCommand.Execute(null);
        viewModel.ContinueCommand.Execute(null);

        Assert.Equal([AnyPS5Mode.Build], _installer.Used);
        Assert.Equal("home", _navigator.CurrentPage);
    }

    [Fact]
    public void A_failed_switch_stays_on_the_page_and_says_why()
    {
        _installer.Records[AnyPS5Mode.Release] = ReleaseRecord;
        _installer.Records[AnyPS5Mode.Build] = BuildRecord;
        _installer.ActiveMode = AnyPS5Mode.Release;
        _installer.UseFailure = new InvalidOperationException("gone");
        var viewModel = Create();
        viewModel.SelectBuildCommand.Execute(null);

        viewModel.ContinueCommand.Execute(null);

        Assert.NotEqual("home", _navigator.CurrentPage);
        Assert.Equal("Cannot switch to this install: gone", viewModel.ErrorText);

        viewModel.SelectReleaseCommand.Execute(null);

        Assert.Null(viewModel.ErrorText);
    }

    [Fact]
    public void Without_an_install_nothing_is_named_and_the_button_continues()
    {
        var viewModel = Create();

        Assert.Null(viewModel.ReleaseInstalledText);
        Assert.Null(viewModel.BuildInstalledText);
        Assert.Equal("Continue", viewModel.ContinueText);
    }
}
