using System.ComponentModel;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Launch;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.App.Tests;

public sealed class GameViewModelTests : IDisposable
{
    private readonly TestLibrary _library = new();
    private readonly Navigator _navigator = new();

    private async Task<(GameViewModel Page, FakeLaunchedGame Process)> LaunchAsync(bool firstLaunch = true)
    {
        await _library.AddAsync("PPSA99666", "DOOM");
        var playing = await _library.Player.LaunchAsync("PPSA99666", TestContext.Current.CancellationToken);
        var page = new GameViewModel(playing, firstLaunch, _library.Player, _library.Platform, new InlineDispatcher(), _navigator, new FakePages());
        return (page, (FakeLaunchedGame)playing.Process);
    }

    private static Task FinishedAsync(GameViewModel page) => page.Following.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

    [Fact]
    public async Task A_running_game_shows_its_pid_and_the_first_launch_hint()
    {
        var (page, _) = await LaunchAsync();

        Assert.Equal("DOOM", page.Title);
        Assert.Equal("Running (PID 4812)", page.StatusText);
        Assert.True(page.IsRunning);
        Assert.Equal("The first launch may stutter while shaders are prepared.", page.FirstLaunchHint);
        Assert.True(page.StopCommand.CanExecute(null));
        Assert.False(page.LaunchAgainCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_later_launch_shows_no_hint()
    {
        var (page, _) = await LaunchAsync(firstLaunch: false);

        Assert.Null(page.FirstLaunchHint);
    }

    [Fact]
    public async Task Follows_the_log_then_shows_the_exit_code_and_forgets_the_game()
    {
        var (page, process) = await LaunchAsync();
        await File.AppendAllTextAsync(process.LogFile, "line 1\nline 2\n", TestContext.Current.CancellationToken);
        process.Exit(3);

        page.OnNavigatedTo();
        await FinishedAsync(page);

        Assert.Equal(["line 1", "line 2"], page.Log);
        Assert.Equal("Exited with code 3", page.StatusText);
        Assert.False(page.IsRunning);
        Assert.True(page.CanLaunchAgain);
        Assert.False(File.Exists(_library.Game("PPSA99666").RunningRecord));
        Assert.True(page.LaunchAgainCommand.CanExecute(null));
    }

    [Fact]
    public async Task Keeps_the_last_500_lines()
    {
        var (page, process) = await LaunchAsync();
        await File.WriteAllLinesAsync(process.LogFile, Enumerable.Range(0, 600).Select(index => $"line {index}"), TestContext.Current.CancellationToken);
        process.Exit(0);

        page.OnNavigatedTo();
        await FinishedAsync(page);

        Assert.Equal(GameViewModel.KeptLines, page.Log.Count);
        Assert.Equal("line 100", page.Log[0]);
        Assert.Equal("line 599", page.Log[^1]);
    }

    [Fact]
    public async Task Stop_ends_the_game()
    {
        var (page, _) = await LaunchAsync();
        page.OnNavigatedTo();

        await page.StopCommand.ExecuteAsync(null);
        await FinishedAsync(page);

        Assert.Equal("Exited with code 1", page.StatusText);
    }

    [Fact]
    public async Task A_failed_stop_is_shown_and_the_game_stays_running()
    {
        var (page, process) = await LaunchAsync();
        process.StopFailure = new Win32Exception(5, "Access is denied");
        page.OnNavigatedTo();

        await page.StopCommand.ExecuteAsync(null);

        Assert.Equal("Could not stop DOOM: Access is denied", page.Message);
        Assert.True(page.IsRunning);
        Assert.True(File.Exists(_library.Game("PPSA99666").RunningRecord));
    }

    [Fact]
    public async Task Leaving_goes_to_the_library_and_keeps_the_game_running_and_recorded()
    {
        var (page, process) = await LaunchAsync();
        page.OnNavigatedTo();

        await page.BackToLibraryCommand.ExecuteAsync(null);

        Assert.Equal("library", _navigator.CurrentPage);
        Assert.False(process.HasExited);
        Assert.True(File.Exists(_library.Game("PPSA99666").RunningRecord));
    }

    [Fact]
    public async Task Launch_again_starts_a_new_process_and_follows_it()
    {
        var (page, process) = await LaunchAsync();
        await File.AppendAllTextAsync(process.LogFile, "old\n", TestContext.Current.CancellationToken);
        process.Exit(0);
        page.OnNavigatedTo();
        await FinishedAsync(page);

        await page.LaunchAgainCommand.ExecuteAsync(null);
        Assert.Null(page.Message);

        Assert.Equal(2, _library.Launcher.Launches.Count);
        Assert.Equal("Running (PID 4813)", page.StatusText);
        Assert.Empty(page.Log);
        Assert.Null(page.FirstLaunchHint);
    }

    [Fact]
    public async Task A_refused_launch_again_is_shown()
    {
        var (page, process) = await LaunchAsync();
        process.Exit(0);
        page.OnNavigatedTo();
        await FinishedAsync(page);

        using (GameLock.TryAcquireNow(_library.Game("PPSA99666")))
        {
            await page.LaunchAgainCommand.ExecuteAsync(null);
        }

        Assert.Equal("Another PS5Kitsune action is using DOOM. Try again when it finishes.", page.Message);
        Assert.Equal("Exited with code 0", page.StatusText);
    }

    [Fact]
    public async Task Open_folder_opens_the_game_folder()
    {
        var (page, _) = await LaunchAsync();

        page.OpenFolderCommand.Execute(null);

        Assert.Equal([_library.Game("PPSA99666").Root], _library.Platform.OpenedPaths);
    }

    [Fact]
    public async Task Settings_opens_the_game_settings()
    {
        var (page, _) = await LaunchAsync();

        page.OpenSettingsCommand.Execute(null);

        Assert.Equal("settings:PPSA99666", _navigator.CurrentPage);
    }

    private async Task<GameViewModel> OpenLaunchPageAsync(bool firstLaunch = true)
    {
        await _library.AddAsync("PPSA99666", "DOOM");
        return new GameViewModel(_library.Game("PPSA99666"), "DOOM", firstLaunch, _library.Player, _library.Platform, new InlineDispatcher(), _navigator, new FakePages());
    }

    // The launch runs on the thread pool; its progress reaches the page through the (inline) dispatcher.
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out.");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    private async Task SetSettingsAsync(GameSettings settings) =>
        await _library.Player.SaveSettingsAsync("PPSA99666", settings, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Opening_the_page_launches_the_game_and_follows_it()
    {
        var page = await OpenLaunchPageAsync();
        Assert.Equal("Starting…", page.StatusText);

        page.OnNavigatedTo();
        await page.Launching;

        Assert.Single(_library.Launcher.Launches);
        Assert.Equal("Running (PID 4812)", page.StatusText);
        Assert.True(page.IsRunning);
        Assert.False(page.IsPreparing);
        Assert.Equal("The first launch may stutter while shaders are prepared.", page.FirstLaunchHint);
    }

    [Fact]
    public async Task A_font_download_shows_its_progress_and_can_be_cancelled()
    {
        _library.Fonts.Installed = false;
        _library.Fonts.Reports = [34L << 20];
        _library.Fonts.Gate = new TaskCompletionSource();
        var page = await OpenLaunchPageAsync();

        page.OnNavigatedTo();
        await WaitUntilAsync(() => page.StatusText.StartsWith("Downloading", StringComparison.Ordinal));

        Assert.Equal("Downloading fonts… 34 MB / 86 MB", page.StatusText);
        Assert.True(page.IsPreparing);
        Assert.False(page.CanLaunchAgain);
        page.CancelLaunchCommand.Execute(null);
        await page.Launching;
        Assert.Equal("Launch cancelled", page.StatusText);
        Assert.Empty(_library.Launcher.Launches);
        Assert.True(page.CanLaunchAgain);
    }

    [Fact]
    public async Task Lavapipe_is_downloaded_first_then_the_game_starts()
    {
        var page = await OpenLaunchPageAsync();
        await SetSettingsAsync(new GameSettings { Lavapipe = true });
        _library.Lavapipe.Reports = [34L << 20];
        _library.Lavapipe.Gate = new TaskCompletionSource();

        page.OnNavigatedTo();
        await WaitUntilAsync(() => page.StatusText.StartsWith("Downloading", StringComparison.Ordinal));
        Assert.Equal("Downloading Lavapipe… 34 MB / 70 MB", page.StatusText);
        _library.Lavapipe.Gate.SetResult();
        await page.Launching;

        Assert.True(page.IsRunning);
        Assert.Equal(_library.Lavapipe.Manifest, Assert.Single(_library.Launcher.Launches).Environment["VK_DRIVER_FILES"]);
    }

    [Fact]
    public async Task A_font_warning_is_shown_and_the_game_still_starts()
    {
        _library.Fonts.Installed = false;
        _library.Fonts.Failure = new HttpRequestException("offline");
        var page = await OpenLaunchPageAsync();

        page.OnNavigatedTo();
        await page.Launching;

        Assert.Equal("Fonts could not be downloaded (offline); games that use system fonts may show no text or stop.", page.FontsWarning);
        Assert.True(page.IsRunning);
    }

    [Fact]
    public async Task A_failed_lavapipe_download_offers_to_launch_again()
    {
        var page = await OpenLaunchPageAsync();
        await SetSettingsAsync(new GameSettings { Lavapipe = true });
        _library.Lavapipe.Failure = new HttpRequestException("404");

        page.OnNavigatedTo();
        await page.Launching;

        Assert.Equal("Could not download Lavapipe: 404", page.Message);
        Assert.Equal("Not running", page.StatusText);
        Assert.True(page.CanLaunchAgain);
    }

    [Fact]
    public async Task A_busy_game_says_why()
    {
        var page = await OpenLaunchPageAsync();

        using (GameLock.TryAcquireNow(_library.Game("PPSA99666")))
        {
            page.OnNavigatedTo();
            await page.Launching;
        }

        Assert.Equal("Another PS5Kitsune action is using DOOM. Try again when it finishes.", page.Message);
        Assert.Equal("Not running", page.StatusText);
    }

    [Fact]
    public async Task A_missing_executable_suggests_a_reconvert()
    {
        var page = await OpenLaunchPageAsync();
        File.Delete(_library.Game("PPSA99666").Executable);

        page.OnNavigatedTo();
        await page.Launching;

        Assert.Equal("The executable is missing: Reconvert DOOM.", page.Message);
    }

    [Fact]
    public async Task Leaving_during_a_download_cancels_the_launch()
    {
        _library.Fonts.Installed = false;
        _library.Fonts.Gate = new TaskCompletionSource();
        var page = await OpenLaunchPageAsync();
        page.OnNavigatedTo();
        await WaitUntilAsync(() => page.IsPreparing && _library.Fonts.Installs == 1);

        await page.BackToLibraryCommand.ExecuteAsync(null);

        Assert.Equal("library", _navigator.CurrentPage);
        Assert.Empty(_library.Launcher.Launches);
    }

    [Fact]
    public async Task A_game_already_running_is_followed_instead_of_refused()
    {
        var page = await OpenLaunchPageAsync();
        var started = await _library.Player.LaunchAsync("PPSA99666", TestContext.Current.CancellationToken);

        page.OnNavigatedTo();
        await page.Launching;

        Assert.True(page.IsRunning);
        Assert.False(page.IsPreparing);
        Assert.Equal("Running (PID 4812)", page.StatusText);
        Assert.Null(page.Message);
        Assert.Single(_library.Launcher.Launches);
        ((FakeLaunchedGame)started.Process).Exit(0);
        await FinishedAsync(page);
        Assert.Equal("Exited with code 0", page.StatusText);
    }

    [Fact]
    public async Task Leaving_still_works_after_an_unexpected_launch_error()
    {
        _library.Launcher.LaunchFailure = new InvalidOperationException("boom");
        var page = await OpenLaunchPageAsync();

        page.OnNavigatedTo();
        await Assert.ThrowsAsync<InvalidOperationException>(() => page.Launching);
        await page.BackToLibraryCommand.ExecuteAsync(null);

        Assert.Equal("library", _navigator.CurrentPage);
    }

    [Fact]
    public async Task A_download_without_a_total_shows_only_the_received_size()
    {
        _library.Fonts.Installed = false;
        _library.Fonts.Reports = [34L << 20];
        _library.Fonts.ReportedTotal = null;
        _library.Fonts.Gate = new TaskCompletionSource();
        var page = await OpenLaunchPageAsync();

        page.OnNavigatedTo();
        await WaitUntilAsync(() => page.StatusText.StartsWith("Downloading", StringComparison.Ordinal));

        Assert.Equal("Downloading fonts… 34 MB", page.StatusText);
        page.CancelLaunchCommand.Execute(null);
        await page.Launching;
    }

    public void Dispose() => _library.Dispose();
}
