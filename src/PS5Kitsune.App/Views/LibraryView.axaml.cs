using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using PS5Kitsune.App.ViewModels;

namespace PS5Kitsune.App.Views;

public sealed partial class LibraryView : PageView
{
    public LibraryView()
    {
        InitializeComponent();
        Games.AddHandler(KeyDownEvent, OnGamesKeyDown, RoutingStrategies.Tunnel);
        Games.AddHandler(TappedEvent, OnGamesTapped);
        Games.GotFocus += OnGamesGotFocus;
    }

    /// <summary>Enter opens the game; the Menu key or Shift+F10 opens its actions, as "…" does with the mouse.</summary>
    private void OnGamesKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not LibraryViewModel page || CurrentTile() is not { } tile)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            page.OpenCommand.Execute(tile);
            e.Handled = true;
        }
        else if (e.Key == Key.Apps || (e.Key == Key.F10 && e.KeyModifiers == KeyModifiers.Shift))
        {
            OpenActions(tile);
            e.Handled = true;
        }
    }

    /// <summary>One click opens the game, as before the grid became a list; a click on "…" only opens its menu.</summary>
    private void OnGamesTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is LibraryViewModel page && e.Source is Visual source
            && source.FindAncestorOfType<Button>(includeSelf: true) is null
            && source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is { DataContext: GameTileViewModel tile })
        {
            page.OpenCommand.Execute(tile);
        }
    }

    /// <summary>Focusing the list itself (a script, a screen reader) hands the focus to the selected tile, else the first one.</summary>
    private void OnGamesGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (ReferenceEquals(e.Source, Games) && (Games.ContainerFromIndex(Math.Max(Games.SelectedIndex, 0)) is { } container))
        {
            container.Focus(e.NavigationMethod);
        }
    }

    /// <summary>The focused tile, else the selected one.</summary>
    private GameTileViewModel? CurrentTile() =>
        (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext as GameTileViewModel
        ?? Games.SelectedItem as GameTileViewModel;

    private void OpenActions(GameTileViewModel tile)
    {
        if (Games.ContainerFromItem(tile) is Control container && container.FindDescendantOfType<Button>() is { Flyout: { } flyout } more)
        {
            flyout.ShowAt(more);
        }
    }
}
