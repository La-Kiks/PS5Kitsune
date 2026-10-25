using PS5Kitsune.App.Controls;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.App.Tests;

public sealed class LibraryViewModelTests : IDisposable
{
    private readonly TestLibrary _library = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly Navigator _navigator = new();

    private async Task<LibraryViewModel> CreateAsync()
    {
        var page = new LibraryViewModel(
            _library.Player, _library.Cleaner, _library.Running, _library.Installer, _library.Platform, _dialogs, _navigator, new FakePages());
        await page.RefreshAsync();
        return page;
    }

    private static GameTileViewModel Tile(LibraryViewModel page, string name) => page.Games.Single(tile => tile.Name == name);

    private Task<PlayingGame> StartAsync(string titleId) => _library.Player.LaunchAsync(titleId, TestContext.Current.CancellationToken);

    [Fact]
    public async Task A_tile_shows_one_status_running_first_then_reconvert_then_compatibility()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        await _library.AddAsync("PPSA00004", "Delta", new ConversionInfo("test-profile", "build", "3fd1cc3", new string('3', 40), false));
        await StartAsync("PPSA00001");

        var page = await CreateAsync();

        Assert.Equal(StatusKind.Running, Tile(page, "Alpha").Status);
        Assert.Equal("Alpha, PPSA00001, Running", Tile(page, "Alpha").AutomationName);
        Assert.Equal(StatusKind.Reconvert, Tile(page, "Delta").Status);
        Assert.Equal("Needs reconvert", Tile(page, "Delta").StatusText);
        Assert.Equal("Actions for Delta", Tile(page, "Delta").ActionsName);
    }

    [Fact]
    public async Task Lists_the_games_last_played_first_with_their_badges()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        await _library.AddAsync("PPSA00002", "Bravo", lastPlayedAt: new DateTimeOffset(2026, 10, 20, 20, 0, 0, TimeSpan.Zero));
        await _library.AddAsync("PPSA00003", "Charlie", lastPlayedAt: new DateTimeOffset(2026, 10, 22, 20, 0, 0, TimeSpan.Zero));
        await _library.AddAsync("PPSA00004", "Delta", new ConversionInfo("test-profile", "build", "3fd1cc3", new string('3', 40), false));

        var page = await CreateAsync();

        Assert.Equal(["Charlie", "Bravo", "Alpha", "Delta"], page.Games.Select(tile => tile.Name));
        Assert.True(Tile(page, "Delta").NeedsReconvert);
        Assert.False(Tile(page, "Alpha").NeedsReconvert);
        Assert.True(Tile(page, "Alpha").IsOrange);
        Assert.Equal("PPSA00001", Tile(page, "Alpha").TitleId);
        Assert.False(page.IsEmpty);
    }

    [Fact]
    public async Task The_search_filters_by_name()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        await _library.AddAsync("PPSA00003", "Charlie");
        var page = await CreateAsync();

        page.Search = "CHAR";

        Assert.Equal(["Charlie"], page.Games.Select(tile => tile.Name));
        page.Search = "";
        Assert.Equal(2, page.Games.Count);
    }

    [Fact]
    public async Task An_empty_library_says_so()
    {
        var page = await CreateAsync();

        Assert.True(page.IsEmpty);
        Assert.Empty(page.Games);
    }

    [Fact]
    public async Task A_running_game_is_badged()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        await StartAsync("PPSA00001");

        var page = await CreateAsync();

        Assert.True(Tile(page, "Alpha").IsRunning);
    }

    [Fact]
    public async Task Clicking_a_game_opens_its_page_which_launches_it()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        var page = await CreateAsync();

        await page.OpenCommand.ExecuteAsync(Tile(page, "Alpha"));

        Assert.Empty(_library.Launcher.Launches);
        Assert.Equal("launch:PPSA00001", _navigator.CurrentPage);
    }

    [Fact]
    public async Task Clicking_a_game_started_after_the_last_refresh_still_opens_its_launch_page()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        var page = await CreateAsync();
        using var started = (await StartAsync("PPSA00001")).Process;

        await page.OpenCommand.ExecuteAsync(Tile(page, "Alpha"));

        Assert.Equal("launch:PPSA00001", _navigator.CurrentPage);
    }

    [Fact]
    public async Task Clicking_a_running_game_opens_its_page_without_launching_it_again()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        await StartAsync("PPSA00001");
        var page = await CreateAsync();

        await page.OpenCommand.ExecuteAsync(Tile(page, "Alpha"));

        Assert.Single(_library.Launcher.Launches);
        Assert.Equal("game:PPSA00001", _navigator.CurrentPage);
    }

    [Fact]
    public async Task A_game_that_stopped_since_the_refresh_says_so()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        await StartAsync("PPSA00001");
        var page = await CreateAsync();
        _library.Launcher.Games[4812].Exit(0);

        await page.OpenCommand.ExecuteAsync(Tile(page, "Alpha"));

        Assert.Equal("Alpha is no longer running.", page.Message);
        Assert.False(Tile(page, "Alpha").IsRunning);
    }

    [Fact]
    public async Task Launching_while_another_game_runs_offers_to_show_it()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        await _library.AddAsync("PPSA00002", "Bravo");
        await StartAsync("PPSA00002");
        var page = await CreateAsync();
        _dialogs.Confirm = true;

        await page.LaunchCommand.ExecuteAsync(Tile(page, "Alpha"));

        Assert.Equal(["Bravo is running. Show it?"], _dialogs.Confirmations);
        Assert.Single(_library.Launcher.Launches);
        Assert.Equal("game:PPSA00002", _navigator.CurrentPage);
    }

    [Fact]
    public async Task Reconvert_opens_the_reconverting_page_unless_the_game_runs()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        await _library.AddAsync("PPSA00002", "Bravo");
        await StartAsync("PPSA00002");
        var page = await CreateAsync();

        page.ReconvertCommand.Execute(Tile(page, "Bravo"));
        Assert.Equal("Close Bravo first.", page.Message);
        Assert.Null(_navigator.CurrentPage);

        page.ReconvertCommand.Execute(Tile(page, "Alpha"));
        Assert.Equal("reconvert:PPSA00001", _navigator.CurrentPage);
    }

    [Fact]
    public async Task Clearing_the_shader_cache_confirms_with_its_size()
    {
        var game = await _library.AddAsync("PPSA00001", "Alpha");
        Directory.CreateDirectory(Path.Combine(game.ShaderCache, "v1"));
        await File.WriteAllBytesAsync(Path.Combine(game.ShaderCache, "v1", "cache.bin"), new byte[1_572_864], TestContext.Current.CancellationToken);
        var page = await CreateAsync();
        _dialogs.Confirm = true;

        await page.ClearShaderCacheCommand.ExecuteAsync(Tile(page, "Alpha"));

        Assert.Equal(["Delete the shader cache of Alpha (1.5 MB)? The next launch will stutter while shaders are prepared again."], _dialogs.Confirmations);
        Assert.False(Directory.Exists(game.ShaderCache));
        Assert.Null(page.Message);
    }

    [Fact]
    public async Task Clearing_the_shader_cache_of_a_game_removed_meanwhile_refreshes_the_list()
    {
        var game = await _library.AddAsync("PPSA00001", "Alpha");
        await _library.AddAsync("PPSA00002", "Bravo");
        Directory.CreateDirectory(Path.Combine(game.ShaderCache, "v1"));
        await File.WriteAllBytesAsync(Path.Combine(game.ShaderCache, "v1", "cache.bin"), new byte[1024], TestContext.Current.CancellationToken);
        var page = await CreateAsync();
        File.Delete(game.RecordFile);
        _dialogs.Confirm = true;

        await page.ClearShaderCacheCommand.ExecuteAsync(Tile(page, "Alpha"));

        Assert.Equal(["Bravo"], page.Games.Select(tile => tile.Name));
    }

    [Fact]
    public async Task Removing_a_game_whose_record_became_malformed_says_so()
    {
        var game = await _library.AddAsync("PPSA00001", "Alpha");
        var page = await CreateAsync();
        await File.WriteAllTextAsync(game.RecordFile, "{", TestContext.Current.CancellationToken);
        _dialogs.Confirm = true;

        await page.RemoveCommand.ExecuteAsync(Tile(page, "Alpha"));

        Assert.StartsWith("Could not remove Alpha completely: ", page.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_shader_cache_asks_nothing()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        var page = await CreateAsync();

        await page.ClearShaderCacheCommand.ExecuteAsync(Tile(page, "Alpha"));

        Assert.Empty(_dialogs.Confirmations);
        Assert.Equal("Alpha has no shader cache.", page.Message);
    }

    [Fact]
    public async Task Removing_warns_about_saves_and_deletes_the_game()
    {
        var game = await _library.AddAsync("PPSA00001", "Alpha");
        Directory.CreateDirectory(game.Data);
        await File.WriteAllTextAsync(Path.Combine(game.Data, "save.dat"), "save", TestContext.Current.CancellationToken);
        var page = await CreateAsync();
        _dialogs.Confirm = true;

        await page.RemoveCommand.ExecuteAsync(Tile(page, "Alpha"));

        Assert.Equal([@"Delete Alpha and its folder? Its saves in data\ are deleted too."], _dialogs.Confirmations);
        Assert.False(Directory.Exists(game.Root));
        Assert.True(page.IsEmpty);
    }

    [Fact]
    public async Task A_cancelled_removal_keeps_the_game()
    {
        var game = await _library.AddAsync("PPSA00001", "Alpha");
        var page = await CreateAsync();

        await page.RemoveCommand.ExecuteAsync(Tile(page, "Alpha"));

        Assert.Equal(["Delete Alpha and its folder?"], _dialogs.Confirmations);
        Assert.True(File.Exists(game.RecordFile));
    }

    [Fact]
    public async Task View_last_log_opens_the_newest_log()
    {
        var game = await _library.AddAsync("PPSA00001", "Alpha");
        await _library.AddAsync("PPSA00002", "Bravo");
        Directory.CreateDirectory(game.Logs);
        await File.WriteAllTextAsync(Path.Combine(game.Logs, "20261020-200000.log"), "", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(game.Logs, "20261022-200000.log"), "", TestContext.Current.CancellationToken);
        var page = await CreateAsync();

        Assert.False(page.ViewLastLogCommand.CanExecute(Tile(page, "Bravo")));
        page.ViewLastLogCommand.Execute(Tile(page, "Alpha"));

        Assert.Equal([Path.Combine(game.Logs, "20261022-200000.log")], _library.Platform.OpenedPaths);
    }

    [Fact]
    public async Task Add_a_game_navigates()
    {
        var page = await CreateAsync();

        page.AddGameCommand.Execute(null);
        Assert.Equal("choose", _navigator.CurrentPage);
    }

    [Fact]
    public async Task Adding_a_game_needs_an_install()
    {
        _library.Installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.NotInstalled, null);

        var page = await CreateAsync();

        Assert.False(page.CanAddGame);
        Assert.False(page.AddGameCommand.CanExecute(null));
        Assert.Equal("Install AnyPS5 from Home first, then add games.", page.EmptyText);
    }

    [Fact]
    public async Task The_library_opens_first_once_installed_with_a_game()
    {
        Assert.False(LibraryViewModel.OpensFirst(_library.Installer, _library.Library));

        await _library.AddAsync("PPSA00001", "Alpha");
        Assert.True(LibraryViewModel.OpensFirst(_library.Installer, _library.Library));

        _library.Installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.NotInstalled, null);
        Assert.False(LibraryViewModel.OpensFirst(_library.Installer, _library.Library));
    }

    [Fact]
    public async Task Settings_opens_the_game_settings()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        var page = await CreateAsync();

        page.SettingsCommand.Execute(Tile(page, "Alpha"));

        Assert.Equal("settings:PPSA00001", _navigator.CurrentPage);
    }

    public void Dispose() => _library.Dispose();
}
