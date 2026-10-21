using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Sessions;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Tests;

namespace PS5Kitsune.App.Tests;

public sealed class MainWindowViewModelTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FakeInstaller _installer = new("Install Python");
    private readonly FakeDialogs _dialogs = new();
    private readonly Navigator _navigator = new();
    private readonly SessionHost _sessions;
    private readonly MainWindowViewModel _viewModel;

    public MainWindowViewModelTests()
    {
        _sessions = new SessionHost(root => TestSessions.Create(root, _installer), _temp.Combine("a"));
        _viewModel = new MainWindowViewModel(_sessions, _navigator, new TestPlatform(), _dialogs);
    }

    [Fact]
    public void The_header_follows_the_data_root()
    {
        var changed = new List<string?>();
        _viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        Assert.Equal(_temp.Combine("a"), _viewModel.DataRoot);
        Assert.Equal("test-profile (validated)", _viewModel.Profile);

        _sessions.ChangeRoot(_temp.Combine("b"));

        Assert.Equal(_temp.Combine("b"), _viewModel.DataRoot);
        Assert.Contains(nameof(MainWindowViewModel.DataRoot), changed);
    }

    [Fact]
    public async Task Closing_without_an_install_asks_nothing()
    {
        Assert.True(await _viewModel.ConfirmCloseAsync());
        Assert.Empty(_dialogs.Confirmations);
    }

    [Fact]
    public async Task Closing_during_an_install_asks_and_can_be_declined()
    {
        var install = StartRunningInstall();

        Assert.True(_viewModel.IsInstallRunning);
        Assert.False(await _viewModel.ConfirmCloseAsync());
        Assert.Single(_dialogs.Confirmations);
        Assert.True(install.IsRunning);

        await install.CancelAndWaitAsync();
    }

    [Fact]
    public async Task Confirming_cancels_the_install_before_closing()
    {
        var install = StartRunningInstall();
        _dialogs.Confirm = true;

        Assert.True(await _viewModel.ConfirmCloseAsync());
        Assert.False(install.IsRunning);
    }

    private InstallViewModel StartRunningInstall()
    {
        _installer.Install = (_, _, cancellationToken) => Task.Delay(Timeout.Infinite, cancellationToken);
        var install = new InstallViewModel(_installer, AnyPS5Mode.Release, "app.log", new TestPlatform(), new InlineDispatcher(), _navigator, new FakePages());
        _navigator.GoTo(install);
        return install;
    }

    public void Dispose()
    {
        _sessions.Dispose();
        _temp.Dispose();
    }
}
