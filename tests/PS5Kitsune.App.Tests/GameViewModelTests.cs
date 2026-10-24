using System.ComponentModel;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Games;
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
        Assert.True(page.HasExited);
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

    public void Dispose() => _library.Dispose();
}
