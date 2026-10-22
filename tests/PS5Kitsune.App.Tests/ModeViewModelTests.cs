using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.App.Tests;

public sealed class ModeViewModelTests
{
    private readonly Navigator _navigator = new();

    [Fact]
    public void Describes_the_release()
    {
        var viewModel = new ModeViewModel(TestSessions.Profile, _navigator, new FakePages());

        Assert.Equal("Release v0.1.2", viewModel.ReleaseTitle);
        Assert.Equal("Published October 14, 2026", viewModel.PublishedText);
        Assert.Equal("Validated with DOOM for PS5", viewModel.ProfileStatusText);
    }

    [Fact]
    public void Says_when_the_date_or_validation_is_missing()
    {
        var profile = TestSessions.Profile with { Validated = false, Source = new ProfileSource("release", Tag: "v9") };

        var viewModel = new ModeViewModel(profile, _navigator, new FakePages());

        Assert.Equal("Publication date unknown", viewModel.PublishedText);
        Assert.Equal("Not validated yet", viewModel.ProfileStatusText);
    }

    [Fact]
    public void Install_goes_to_the_install_page()
    {
        var viewModel = new ModeViewModel(TestSessions.Profile, _navigator, new FakePages());

        viewModel.InstallCommand.Execute(null);

        Assert.Equal("install:Release", _navigator.CurrentPage);
    }

    [Fact]
    public void Back_returns_to_the_pc_check()
    {
        _navigator.GoTo("check:Release");
        var viewModel = new ModeViewModel(TestSessions.Profile, _navigator, new FakePages());
        _navigator.GoTo(viewModel);

        viewModel.BackCommand.Execute(null);

        Assert.Equal("check:Release", _navigator.CurrentPage);
    }
}
