using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Shell;
using PS5Kitsune.Core.Settings;

namespace PS5Kitsune.App.ViewModels;

public enum NavSection
{
    None,
    Home,
    Library,
}

/// <summary>The window shell: the sidebar (Home, Library, the theme switch) and the page the navigator shows.</summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly IPages _pages;
    private readonly ThemeService _theme;
    private readonly Func<ILogger>? _loggerSource;
    private INotifyPropertyChanged? _watched;

    public MainWindowViewModel(Navigator navigator, IDialogService dialogs, IPages pages, ThemeService theme, Func<ILogger>? loggerSource = null)
    {
        Navigator = navigator;
        _dialogs = dialogs;
        _pages = pages;
        _theme = theme;
        _loggerSource = loggerSource;
        navigator.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Navigator.CurrentPage))
            {
                OnPageChanged();
            }
        };
        OnPageChanged();
    }

    public Navigator Navigator { get; }

    /// <summary>The page runs work the sidebar must not interrupt: an install, an add, a reconvert, reading a game, a launch's downloads, or a page going back on its own.</summary>
    public bool IsBusy => Navigator.CurrentPage is IRunningWork { IsRunning: true } or GameViewModel { IsPreparing: true } or IBusyPage { IsLeaving: true };

    public bool CanNavigate => !IsBusy;

    public string? NavTip => IsBusy ? "Finish or cancel the current step first." : null;

    public NavSection CurrentSection => Navigator.CurrentPage switch
    {
        HomeViewModel => NavSection.Home,
        LibraryViewModel => NavSection.Library,
        _ => NavSection.None,
    };

    public bool IsHomeCurrent => CurrentSection == NavSection.Home;

    public bool IsLibraryCurrent => CurrentSection == NavSection.Library;

    public string? HomeItemStatus => IsHomeCurrent ? "current" : null;

    public string? LibraryItemStatus => IsLibraryCurrent ? "current" : null;

    /// <summary>Completes when the last theme choice is saved; for tests.</summary>
    public Task ThemeSaving { get; private set; } = Task.CompletedTask;

    public bool IsSystemTheme
    {
        get => _theme.Current == AppTheme.System;
        set => ChooseTheme(value, AppTheme.System);
    }

    public bool IsLightTheme
    {
        get => _theme.Current == AppTheme.Light;
        set => ChooseTheme(value, AppTheme.Light);
    }

    public bool IsDarkTheme
    {
        get => _theme.Current == AppTheme.Dark;
        set => ChooseTheme(value, AppTheme.Dark);
    }

    /// <summary>The current page runs work or owns a prepared game: closing must go through <see cref="ConfirmCloseAsync"/>.</summary>
    public bool NeedsCloseHandling => Navigator.CurrentPage is IRunningWork { IsRunning: true } or IPreparedGameOwner;

    /// <summary>
    /// Asks before closing when the running work has a quit message; then cancels it and waits, then releases a prepared game.
    /// </summary>
    /// <returns>True when the window may close.</returns>
    public async Task<bool> ConfirmCloseAsync()
    {
        if (Navigator.CurrentPage is IRunningWork { IsRunning: true } work)
        {
            if (work.QuitMessage is { } message && !await _dialogs.ConfirmAsync("Quit PS5Kitsune?", message, "Quit").ConfigureAwait(true))
            {
                return false;
            }

            await work.CancelAndWaitAsync().ConfigureAwait(true);
        }

        if (Navigator.CurrentPage is IPreparedGameOwner owner)
        {
            await owner.ReleaseAsync().ConfigureAwait(true);
        }

        return true;
    }

    [RelayCommand(CanExecute = nameof(CanNavigate))]
    private Task GoHomeAsync() => CurrentSection == NavSection.Home ? Task.CompletedTask : LeaveAndShowAsync(_pages.Home);

    [RelayCommand(CanExecute = nameof(CanNavigate))]
    private Task GoLibraryAsync() => CurrentSection == NavSection.Library ? Task.CompletedTask : LeaveAndShowAsync(_pages.Library);

    /// <summary>Leaves every page in the stack, current first, as their Back would; a page may keep the user there.</summary>
    private async Task LeaveAndShowAsync(Func<object> target)
    {
        foreach (var page in Navigator.Pages.ToList())
        {
            if (page is not ILeavable leavable)
            {
                continue;
            }

            try
            {
                if (!await leavable.LeaveAsync().ConfigureAwait(true))
                {
                    return;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _loggerSource?.Invoke().LeaveFailed(exception, page.GetType().Name);
            }
        }

        if (Navigator.CurrentPage is IPreparedGameOwner owner)
        {
            try
            {
                await owner.ReleaseAsync().ConfigureAwait(true);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _loggerSource?.Invoke().LeaveFailed(exception, owner.GetType().Name);
            }
        }

        Navigator.GoHome(target());
    }

    private void ChooseTheme(bool chosen, AppTheme theme)
    {
        if (!chosen || _theme.Current == theme)
        {
            return;
        }

        // ChangeAsync applies synchronously before its first await, so Current is already the new theme here.
        ThemeSaving = _theme.ChangeAsync(theme);
        OnPropertyChanged(nameof(IsSystemTheme));
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(IsDarkTheme));
    }

    private void OnPageChanged()
    {
        if (_watched is not null)
        {
            _watched.PropertyChanged -= OnPagePropertyChanged;
        }

        _watched = Navigator.CurrentPage as INotifyPropertyChanged;
        if (_watched is not null)
        {
            _watched.PropertyChanged += OnPagePropertyChanged;
        }

        OnPropertyChanged(nameof(CurrentSection));
        OnPropertyChanged(nameof(IsHomeCurrent));
        OnPropertyChanged(nameof(IsLibraryCurrent));
        OnPropertyChanged(nameof(HomeItemStatus));
        OnPropertyChanged(nameof(LibraryItemStatus));
        OnBusyChanged();
    }

    private void OnPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IRunningWork.IsRunning) or nameof(GameViewModel.IsPreparing) or nameof(IBusyPage.IsLeaving))
        {
            OnBusyChanged();
        }
    }

    private void OnBusyChanged()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanNavigate));
        OnPropertyChanged(nameof(NavTip));
        GoHomeCommand.NotifyCanExecuteChanged();
        GoLibraryCommand.NotifyCanExecuteChanged();
    }
}
