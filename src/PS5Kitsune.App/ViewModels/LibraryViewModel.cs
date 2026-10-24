using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.App.ViewModels;

/// <summary>The added games as tiles: launch in one click, the running game, and each game's actions. See the 7a spec, "Library page".</summary>
public sealed partial class LibraryViewModel(
    GamePlayer player, GameCleaner cleaner, RunningGames running, IAnyPS5Installer installer, IPlatform platform, IDialogService dialogs,
    Navigator navigator, IPages pages)
    : ObservableObject, INavigationAware
{
    private IReadOnlyList<GameTileViewModel> _all = [];
    private int _generation;

    /// <summary>The tiles matching <see cref="Search"/>, last played first.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<GameTileViewModel> Games { get; private set; } = [];

    [ObservableProperty]
    public partial string Search { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoading { get; private set; }

    /// <summary>No game is added (not: none matches the search).</summary>
    public bool IsEmpty => !IsLoading && _all.Count == 0;

    /// <summary>Why the last action failed; null otherwise.</summary>
    [ObservableProperty]
    public partial string? Message { get; private set; }

    /// <summary>The app opens on the Library once AnyPS5 is installed and a game is added; reads no game.json.</summary>
    public static bool OpensFirst(IAnyPS5Installer installer, GameLibrary library)
    {
        ArgumentNullException.ThrowIfNull(installer);
        ArgumentNullException.ThrowIfNull(library);
        return installer.ReadRecord() is not null && library.HasGames();
    }

    public void OnNavigatedTo() => _ = RefreshAsync();

    /// <summary>Reads the games on a worker thread; a result that arrives after a newer refresh started is dropped.</summary>
    public async Task RefreshAsync()
    {
        var generation = ++_generation;
        IsLoading = true;
        IReadOnlyList<GameTileViewModel> tiles;
        try
        {
            tiles = await Task.Run(Read).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (generation == _generation)
            {
                Message = $"Cannot read the library: {exception.Message}";
                IsLoading = false;
            }

            return;
        }

        if (generation != _generation)
        {
            return;
        }

        _all = tiles;
        ApplySearch();
        IsLoading = false;
        OnPropertyChanged(nameof(IsEmpty));
    }

    partial void OnSearchChanged(string value) => ApplySearch();

    private IReadOnlyList<GameTileViewModel> Read()
    {
        var install = installer.ReadRecord();
        return
        [
            .. player.List()
                .Select(game => new GameTileViewModel(game, install is not null && GameLibrary.NeedsReconversion(game.Record, install), this))
                .OrderByDescending(tile => tile.LastPlayedAt.HasValue)
                .ThenByDescending(tile => tile.LastPlayedAt)
                .ThenBy(tile => tile.Name, StringComparer.CurrentCultureIgnoreCase),
        ];
    }

    private void ApplySearch() =>
        Games = Search.Trim() is { Length: > 0 } text
            ? [.. _all.Where(tile => tile.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase))]
            : _all;

    /// <summary>A click on the tile: launches the game, or shows it when it runs.</summary>
    [RelayCommand]
    private Task OpenAsync(GameTileViewModel tile) => tile.IsRunning ? ShowAsync(tile) : LaunchAsync(tile);

    [RelayCommand]
    private async Task LaunchAsync(GameTileViewModel tile)
    {
        Message = null;
        if (tile.IsRunning)
        {
            await ShowAsync(tile).ConfigureAwait(true);
            return;
        }

        // One game at a time.
        if (_all.FirstOrDefault(other => other.IsRunning && other.TitleId != tile.TitleId) is { } other)
        {
            if (await dialogs.ConfirmAsync("A game is running", $"{other.Name} is running. Show it?", "Show it").ConfigureAwait(true))
            {
                await ShowAsync(other).ConfigureAwait(true);
            }

            return;
        }

        // The Game page downloads what is missing and launches; a game started elsewhere is reported there.
        navigator.GoTo(pages.Launch(tile.Entry.Game, tile.Name, firstLaunch: tile.LastPlayedAt is null));
    }

    private async Task ShowAsync(GameTileViewModel tile)
    {
        var process = await Task.Run(() => running.Attach(tile.Entry.Game)).ConfigureAwait(true);
        if (process is null)
        {
            Message = $"{tile.Name} is no longer running.";
            await RefreshAsync().ConfigureAwait(true);
            return;
        }

        navigator.GoTo(pages.Game(new PlayingGame(tile.Entry.Game, tile.Entry.Record, process), firstLaunch: false));
    }

    [RelayCommand]
    private void OpenFolder(GameTileViewModel tile) => Message = PathOpener.TryOpen(platform, tile.Entry.Game.Root);

    [RelayCommand]
    private void Settings(GameTileViewModel tile) => navigator.GoTo(pages.GameSettings(tile.TitleId));

    private static bool CanViewLastLog(GameTileViewModel? tile) => tile?.HasLog == true;

    // Log names sort by date, so the greatest is the newest.
    [RelayCommand(CanExecute = nameof(CanViewLastLog))]
    private void ViewLastLog(GameTileViewModel tile) =>
        Message = PathOpener.TryRun(() => platform.OpenPath(Directory.GetFiles(tile.Entry.Game.Logs, "*.log").Max(StringComparer.Ordinal)!));

    [RelayCommand]
    private void Reconvert(GameTileViewModel tile)
    {
        Message = null;
        if (tile.IsRunning)
        {
            Message = $"Close {tile.Name} first.";
            return;
        }

        navigator.GoTo(pages.Reconvert(tile.TitleId, tile.Name));
    }

    [RelayCommand]
    private async Task ClearShaderCacheAsync(GameTileViewModel tile)
    {
        Message = null;
        long size;
        try
        {
            size = await Task.Run(() => GameCleaner.ShaderCacheSize(tile.Entry.Game)).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Message = $"Could not read the shader cache of {tile.Name}: {exception.Message}";
            return;
        }

        if (size == 0)
        {
            Message = $"{tile.Name} has no shader cache.";
            return;
        }

        var question = $"Delete the shader cache of {tile.Name} ({FormatSize(size)})? The next launch will stutter while shaders are prepared again.";
        if (!await dialogs.ConfirmAsync("Clear the shader cache?", question, "Delete").ConfigureAwait(true))
        {
            return;
        }

        try
        {
            await Task.Run(() => cleaner.ClearShaderCache(tile.TitleId)).ConfigureAwait(true);
        }
        catch (GameRunningException)
        {
            Message = $"Close {tile.Name} first.";
        }
        catch (GameBusyException)
        {
            Message = $"Another PS5Kitsune action is using {tile.Name}. Try again when it finishes.";
        }
        catch (GameNotFoundException)
        {
            // Removed elsewhere already: the refresh drops it.
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (InvalidGameRecordException exception)
        {
            Message = $"Could not clear the shader cache of {tile.Name}: {exception.Message}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Message = $"The shader cache of {tile.Name} may be partly cleared: {exception.Message}";
        }
    }

    [RelayCommand]
    private async Task RemoveAsync(GameTileViewModel tile)
    {
        Message = null;
        bool hasSaves;
        try
        {
            hasSaves = await Task.Run(() => GameCleaner.HasSaves(tile.Entry.Game)).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Message = $"Could not remove {tile.Name} completely: {exception.Message}";
            await RefreshAsync().ConfigureAwait(true);
            return;
        }

        var question = hasSaves ? $"Delete {tile.Name} and its folder? Its saves in data\\ are deleted too." : $"Delete {tile.Name} and its folder?";
        if (!await dialogs.ConfirmAsync($"Remove {tile.Name}?", question, "Delete").ConfigureAwait(true))
        {
            return;
        }

        try
        {
            await Task.Run(() => cleaner.Remove(tile.TitleId)).ConfigureAwait(true);
        }
        catch (GameRunningException)
        {
            Message = $"Close {tile.Name} first.";
        }
        catch (GameBusyException)
        {
            Message = $"Another PS5Kitsune action is using {tile.Name}. Try again when it finishes.";
        }
        catch (GameNotFoundException)
        {
            // Removed elsewhere already: the refresh drops it.
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidGameRecordException)
        {
            Message = $"Could not remove {tile.Name} completely: {exception.Message}";
        }

        await RefreshAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private void AddGame() => navigator.GoTo(pages.ChooseGame());

    [RelayCommand]
    private void Home() => navigator.GoHome(pages.Home());

    private static string FormatSize(long bytes) => bytes >= 1L << 30
        ? string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)(1L << 30):0.0} GB")
        : string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)(1L << 20):0.0} MB");
}
