using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.App.ViewModels;

/// <summary>A running game: its log followed live, Stop, then its exit code. Leaving the page never stops the game.</summary>
public sealed partial class GameViewModel : ObservableObject, INavigationAware
{
    /// <summary>Log lines kept on screen; the file keeps them all.</summary>
    public const int KeptLines = 500;

    private readonly GamePlayer _player;
    private readonly IPlatform _platform;
    private readonly IUiDispatcher _ui;
    private readonly Navigator _navigator;
    private readonly IPages _pages;
    private PlayingGame _playing;
    private CancellationTokenSource? _following;
    private bool _started;

    public GameViewModel(PlayingGame playing, bool firstLaunch, GamePlayer player, IPlatform platform, IUiDispatcher ui, Navigator navigator, IPages pages)
    {
        _playing = playing;
        _player = player;
        _platform = platform;
        _ui = ui;
        _navigator = navigator;
        _pages = pages;
        Title = playing.Record.Name;
        FirstLaunchHint = firstLaunch ? "The first launch may stutter while shaders are prepared." : null;
        ShowRunning();
    }

    public string Title { get; }

    public ObservableCollection<string> Log { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExited))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand), nameof(LaunchAgainCommand))]
    public partial bool IsRunning { get; private set; }

    public bool HasExited => !IsRunning;

    /// <summary>"Running (PID n)", then "Exited with code n".</summary>
    [ObservableProperty]
    public partial string StatusText { get; private set; } = "";

    [ObservableProperty]
    public partial string? FirstLaunchHint { get; private set; }

    /// <summary>Why the last action failed; null otherwise.</summary>
    [ObservableProperty]
    public partial string? Message { get; private set; }

    /// <summary>Completes when the page stops following the current process: it exited, or the page was left.</summary>
    public Task Following { get; private set; } = Task.CompletedTask;

    public void OnNavigatedTo()
    {
        if (!_started)
        {
            StartFollowing();
        }
    }

    private void ShowRunning()
    {
        IsRunning = true;
        StatusText = string.Create(CultureInfo.InvariantCulture, $"Running (PID {_playing.Process.ProcessId})");
    }

    private void StartFollowing()
    {
        _started = true;
        Following = FollowAsync(_playing);
    }

    private async Task FollowAsync(PlayingGame playing)
    {
        using var cancellation = new CancellationTokenSource();
        _following = cancellation;
        var cancellationToken = cancellation.Token;
        var exit = playing.Process.WaitForExitAsync(cancellationToken);
        try
        {
            try
            {
                await Task.Run(
                    () => LogTail.FollowAsync(playing.Process.LogFile, line => _ui.Post(() => AddLine(line)), exit, cancellationToken),
                    cancellationToken).ConfigureAwait(true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Message = $"Cannot follow the log: {exception.Message}";
            }

            var exitCode = await exit.ConfigureAwait(true);
            _player.Forget(playing);
            IsRunning = false;
            StatusText = string.Create(CultureInfo.InvariantCulture, $"Exited with code {exitCode}");
        }
        catch (OperationCanceledException)
        {
            // The page was left: the game keeps running and stays recorded.
        }
        finally
        {
            _following = null;
        }
    }

    private void AddLine(string line)
    {
        Log.Add(line);
        while (Log.Count > KeptLines)
        {
            Log.RemoveAt(0);
        }
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private async Task StopAsync()
    {
        Message = null;
        try
        {
            await _playing.Process.StopAsync().ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Message = $"Could not stop {Title}: {exception.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(HasExited))]
    private async Task LaunchAgainAsync()
    {
        Message = null;
        var titleId = _playing.Record.TitleId;
        PlayingGame playing;
        try
        {
            playing = await Task.Run(() => _player.LaunchAsync(titleId, CancellationToken.None)).ConfigureAwait(true);
        }
        catch (Exception exception) when (LaunchErrors.Describe(exception, Title) is { } message)
        {
            Message = message;
            return;
        }

        _playing.Process.Dispose();
        _playing = playing;
        Log.Clear();
        FirstLaunchHint = null;
        ShowRunning();
        StartFollowing();
    }

    [RelayCommand]
    private async Task BackToLibraryAsync()
    {
        if (_following is { } following)
        {
            await following.CancelAsync().ConfigureAwait(true);
        }

        await Following.ConfigureAwait(true);
        _playing.Process.Dispose();
        _navigator.GoHome(_pages.Library());
    }

    [RelayCommand]
    private void OpenFolder() => Message = PathOpener.TryOpen(_platform, _playing.Game.Root);
}
