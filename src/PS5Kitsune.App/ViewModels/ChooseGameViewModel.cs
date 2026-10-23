using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.App.ViewModels;

/// <summary>The first page of adding a game: a folder or zip, from the pickers or dropped on the page, read before anything is copied.</summary>
/// <param name="busyChanged">Told true while the game is read, so the data root cannot change meanwhile.</param>
public sealed partial class ChooseGameViewModel(IGameImporter importer, IDialogService dialogs, Navigator navigator, IPages pages, Action<bool>? busyChanged = null)
    : ObservableObject, IRunningWork
{
    private CancellationTokenSource? _cancellation;
    private Task _running = Task.CompletedTask;

    public string Title { get; } = "Add a game";

    public string Instructions { get; } = "Choose the game's folder or its .zip, in the DOOM for PS5 format, or drop it here.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning), nameof(CanChoose))]
    [NotifyCanExecuteChangedFor(nameof(ChooseFolderCommand), nameof(ChooseZipCommand), nameof(BackCommand))]
    public partial bool IsPreparing { get; private set; }

    /// <summary>Why the chosen path is not a game; null otherwise.</summary>
    [ObservableProperty]
    public partial string? Error { get; private set; }

    public bool IsRunning => IsPreparing;

    /// <summary>Reading a game is only cancelled, never confirmed.</summary>
    public string? QuitMessage => null;

    public bool CanChoose => !IsPreparing;

    /// <summary>Reads the game at <paramref name="path"/> (the pickers and the drop zone call it), then opens its summary.</summary>
    public Task OpenAsync(string path)
    {
        if (IsPreparing)
        {
            return Task.CompletedTask;
        }

        _running = PrepareAsync(path);
        return _running;
    }

    public async Task CancelAndWaitAsync()
    {
        _cancellation?.Cancel();
        await _running.ConfigureAwait(true);
    }

    private async Task PrepareAsync(string path)
    {
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        IsPreparing = true;
        Error = null;
        busyChanged?.Invoke(true);
        PreparedGame? game = null;
        try
        {
            game = await importer.PrepareAsync(path, cancellation.Token).ConfigureAwait(true);
            if (cancellation.IsCancellationRequested || !ReferenceEquals(navigator.CurrentPage, this))
            {
                // A late result: still running until its staging folder is deleted, so a window close waits for it.
                await game.DisposeAsync().ConfigureAwait(true);
                game = null;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Error = exception.Message;
        }
        finally
        {
            _cancellation = null;
            IsPreparing = false;
            busyChanged?.Invoke(false);
        }

        if (game is not null)
        {
            navigator.GoTo(pages.GameSummary(game));
        }
    }

    [RelayCommand(CanExecute = nameof(CanChoose))]
    private async Task ChooseFolderAsync()
    {
        if (await dialogs.PickFolderAsync("Choose the game's folder", null).ConfigureAwait(true) is { } folder)
        {
            await OpenAsync(folder).ConfigureAwait(true);
        }
    }

    [RelayCommand(CanExecute = nameof(CanChoose))]
    private async Task ChooseZipAsync()
    {
        if (await dialogs.PickFileAsync("Choose the game's .zip", "zip").ConfigureAwait(true) is { } zip)
        {
            await OpenAsync(zip).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private void Cancel() => _cancellation?.Cancel();

    [RelayCommand(CanExecute = nameof(CanChoose))]
    private void Back() => navigator.GoHome(pages.Home());
}
