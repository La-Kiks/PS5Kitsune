using System.Globalization;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Sessions;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Settings;
using PS5Kitsune.Core.Tests;

namespace PS5Kitsune.App.Tests;

public sealed class HomeViewModelTests : IDisposable
{
    private static readonly AnyPS5InstallRecord Record = new(1, "test-profile", "release", "v0.1.2", new Uri("https://example.test/u.py"), new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));

    private readonly TempDirectory _temp = new();
    private readonly FakeInstaller _installer = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly Navigator _navigator = new();
    private readonly SessionHost _sessions;
    private readonly AppSettingsStore _settings;

    public HomeViewModelTests()
    {
        _sessions = new SessionHost(root => TestSessions.Create(root, _installer), _temp.Combine("root"));
        _settings = new AppSettingsStore(_temp.Combine("settings"));
    }

    private HomeViewModel Create()
    {
        var viewModel = new HomeViewModel(_sessions, _settings, _dialogs, _navigator, new FakePages());
        viewModel.Refresh();
        return viewModel;
    }

    [Fact]
    public void Nothing_installed_offers_to_install()
    {
        var viewModel = Create();

        Assert.Equal(AnyPS5InstallStatus.NotInstalled, viewModel.Status);
        Assert.Equal("AnyPS5 is not installed", viewModel.StateTitle);
        Assert.Equal("Install AnyPS5", viewModel.ActionText);
        Assert.Equal("test-profile (validated)", viewModel.ProfileText);
        Assert.Equal(_temp.Combine("root"), viewModel.DataRoot);
    }

    [Fact]
    public void An_install_offers_to_verify_it()
    {
        _installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, Record);

        var viewModel = Create();

        Assert.Equal("AnyPS5 v0.1.2 is installed", viewModel.StateTitle);
        Assert.Contains("test-profile", viewModel.StateDetail, StringComparison.Ordinal);
        Assert.Contains(Record.InstalledAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), viewModel.StateDetail, StringComparison.Ordinal);
        Assert.Equal("Verify / repair", viewModel.ActionText);
    }

    [Fact]
    public void An_incomplete_install_offers_to_resume()
    {
        _installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Incomplete, null);

        var viewModel = Create();

        Assert.Equal("The AnyPS5 install is incomplete", viewModel.StateTitle);
        Assert.Equal("Resume the install", viewModel.ActionText);
    }

    [Fact]
    public void Start_goes_to_the_pc_check()
    {
        Create().StartCommand.Execute(null);

        Assert.Equal("check", _navigator.CurrentPage);
    }

    [Fact]
    public void Entering_the_page_refreshes_the_state()
    {
        var viewModel = Create();
        _installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, Record);

        viewModel.OnNavigatedTo();

        Assert.Equal(AnyPS5InstallStatus.Installed, viewModel.Status);
    }

    [Fact]
    public async Task Changing_the_root_saves_it_and_replaces_the_session()
    {
        var viewModel = Create();
        var first = _sessions.Current;
        _dialogs.Folder = _temp.Combine("other");

        await viewModel.ChangeRootCommand.ExecuteAsync(null);

        Assert.Equal(_temp.Combine("other"), viewModel.DataRoot);
        Assert.Equal(_temp.Combine("other"), _sessions.Current.Layout.Root);
        Assert.True(first.IsDisposed);
        Assert.Equal(_temp.Combine("other"), _settings.Load().Settings.DataRoot);
        Assert.Null(viewModel.RootError);
    }

    [Fact]
    public async Task Cancelling_the_picker_changes_nothing()
    {
        var viewModel = Create();
        var first = _sessions.Current;

        await viewModel.ChangeRootCommand.ExecuteAsync(null);

        Assert.Same(first, _sessions.Current);
        Assert.False(File.Exists(_settings.FilePath));
    }

    [Fact]
    public async Task An_unusable_folder_keeps_the_root_and_says_why()
    {
        var viewModel = Create();
        var file = _temp.Combine("a-file");
        await File.WriteAllTextAsync(file, "x", TestContext.Current.CancellationToken);
        _dialogs.Folder = Path.Combine(file, "root");

        await viewModel.ChangeRootCommand.ExecuteAsync(null);

        Assert.Equal(_temp.Combine("root"), viewModel.DataRoot);
        Assert.StartsWith("Cannot use ", viewModel.RootError, StringComparison.Ordinal);
        Assert.False(File.Exists(_settings.FilePath));
    }

    [Fact]
    public async Task A_failed_save_says_why_and_keeps_the_new_root()
    {
        await File.WriteAllTextAsync(_temp.Combine("settings"), "x", TestContext.Current.CancellationToken);
        var viewModel = Create();
        _dialogs.Folder = _temp.Combine("other");

        await viewModel.ChangeRootCommand.ExecuteAsync(null);

        Assert.StartsWith("Cannot save the setting", viewModel.RootError, StringComparison.Ordinal);
        Assert.Equal(_temp.Combine("other"), viewModel.DataRoot);
        Assert.Equal(_temp.Combine("other"), _sessions.Current.Layout.Root);
    }

    [Fact]
    public async Task A_refused_change_keeps_the_root_and_the_setting()
    {
        var viewModel = Create();
        var first = _sessions.Current;
        _sessions.IsBusy = true;
        _dialogs.Folder = _temp.Combine("other");

        await viewModel.ChangeRootCommand.ExecuteAsync(null);

        Assert.StartsWith("Cannot use ", viewModel.RootError, StringComparison.Ordinal);
        Assert.Equal(_temp.Combine("root"), viewModel.DataRoot);
        Assert.Same(first, _sessions.Current);
        Assert.False(File.Exists(_settings.FilePath));
    }

    public void Dispose()
    {
        _sessions.Dispose();
        _temp.Dispose();
    }
}
