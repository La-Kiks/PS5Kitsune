using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Tests.Headless;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.App.Views;

namespace PS5Kitsune.App.Tests;

[Collection("Headless")]
public sealed class LibraryViewTests(HeadlessSession session) : IDisposable
{
    private readonly TestLibrary _library = new();
    private readonly Navigator _navigator = new();

    [Fact]
    public async Task Arrows_move_between_tiles_and_Enter_opens_the_game()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        await _library.AddAsync("PPSA00002", "Bravo");
        var page = await CreateAsync();

        await session.RunAsync(() =>
        {
            var (window, list) = Show(page);
            try
            {
                list.Focus(NavigationMethod.Tab);
                list.SelectedIndex = 0;
                Dispatcher.UIThread.RunJobs();

                window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
                Assert.Equal(1, list.SelectedIndex);

                window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
            }
            finally
            {
                window.Close();
            }
        });

        // Bravo is not running, so Open launches it: the fake pages return "launch:{titleId}".
        Assert.Equal("launch:PPSA00002", _navigator.CurrentPage);
    }

    [Fact]
    public async Task Shift_F10_opens_the_actions_of_the_selected_tile()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        var page = await CreateAsync();

        var opened = await session.RunAsync(() =>
        {
            var (window, list) = Show(page);
            try
            {
                list.Focus(NavigationMethod.Tab);
                list.SelectedIndex = 0;
                Dispatcher.UIThread.RunJobs();

                window.KeyPressQwerty(PhysicalKey.F10, RawInputModifiers.Shift);
                Dispatcher.UIThread.RunJobs();
                var more = list.GetVisualDescendants().OfType<Button>().Single();
                var isOpen = more.Flyout?.IsOpen ?? false;
                var onMenuItem = more.Flyout is MenuFlyout { Items: [MenuItem first, ..] } && TopLevel.GetTopLevel(first)?.FocusManager?.GetFocusedElement() is MenuItem;
                more.Flyout?.Hide();
                return (isOpen, onMenuItem);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(opened.isOpen);
        Assert.True(opened.onMenuItem, "keyboard focus is inside the menu");
    }

    [Fact]
    public async Task The_grid_is_a_single_tab_stop_and_Shift_Tab_leaves_it()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        await _library.AddAsync("PPSA00002", "Bravo");
        var page = await CreateAsync();

        var steps = await session.RunAsync(() =>
        {
            var (window, list, after) = ShowWithButton(page);
            try
            {
                var search = window.GetVisualDescendants().OfType<TextBox>().Single();
                search.Focus(NavigationMethod.Tab);
                Dispatcher.UIThread.RunJobs();

                // The header's "Add a game" button sits between the search box and the grid.
                window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
                window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
                var intoGrid = InGrid(window, list);
                window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
                var afterGrid = InGrid(window, list);
                var onAfter = ReferenceEquals(window.FocusManager!.GetFocusedElement(), after);

                window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Shift);
                var backInGrid = InGrid(window, list);
                window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Shift);
                var outOfGrid = !InGrid(window, list) && window.FocusManager!.GetFocusedElement() is Button;
                window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Shift);
                var onSearch = ReferenceEquals(window.FocusManager!.GetFocusedElement(), search);
                return (intoGrid, afterGrid, onAfter, backInGrid, outOfGrid, onSearch);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(steps.intoGrid, "Tab from the search box reaches a tile");
        Assert.False(steps.afterGrid, "the next Tab leaves the grid");
        Assert.True(steps.onAfter, "the next Tab reaches the button after the grid");
        Assert.True(steps.backInGrid, "Shift+Tab comes back to a tile");
        Assert.True(steps.outOfGrid, "Shift+Tab from the tile goes to the header button, not back into the grid");
        Assert.True(steps.onSearch, "and one more Shift+Tab reaches the search box");
    }

    [Fact]
    public async Task With_no_games_Tab_from_the_empty_state_button_does_not_land_on_the_list()
    {
        var page = await CreateAsync();

        var result = await session.RunAsync(() =>
        {
            var (window, list, _) = ShowWithButton(page);
            try
            {
                var add = window.GetVisualDescendants().OfType<Button>().First(button => button.IsVisible && Equals(button.Content, "Add a game")
                    && button.GetVisualAncestors().OfType<StackPanel>().Any());
                add.Focus(NavigationMethod.Tab);
                Dispatcher.UIThread.RunJobs();

                window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
                var focused = window.FocusManager!.GetFocusedElement();
                return (focused is not null, InGrid(window, list));
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(result.Item1, "focus lands somewhere");
        Assert.False(result.Item2, "focus is not on the list");
    }

    [Fact]
    public async Task The_Menu_key_opens_the_actions_of_the_selected_tile()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        var page = await CreateAsync();

        var opened = await session.RunAsync(() =>
        {
            var (window, list) = Show(page);
            try
            {
                list.Focus(NavigationMethod.Tab);
                list.SelectedIndex = 0;
                Dispatcher.UIThread.RunJobs();

                window.KeyPressQwerty(PhysicalKey.ContextMenu, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                var more = list.GetVisualDescendants().OfType<Button>().Single();
                var isOpen = more.Flyout?.IsOpen ?? false;
                var onMenuItem = more.Flyout is MenuFlyout { Items: [MenuItem first, ..] } && TopLevel.GetTopLevel(first)?.FocusManager?.GetFocusedElement() is MenuItem;
                more.Flyout?.Hide();
                return (isOpen, onMenuItem);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(opened.isOpen);
        Assert.True(opened.onMenuItem, "keyboard focus is inside the menu");
    }

    private static bool InGrid(Window window, ListBox list) =>
        window.FocusManager!.GetFocusedElement() is Visual focused && focused.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not null
        && focused.GetVisualAncestors().Contains(list);

    private static (Window Window, ListBox List, Button After) ShowWithButton(LibraryViewModel page)
    {
        var after = new Button { Content = "After" };
        var panel = new DockPanel();
        DockPanel.SetDock(after, Dock.Bottom);
        panel.Children.Add(after);
        panel.Children.Add(new LibraryView { DataContext = page });
        var window = new Window { Content = panel, Width = 860, Height = 680 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, window.GetVisualDescendants().OfType<ListBox>().Single(), after);
    }

    private async Task<LibraryViewModel> CreateAsync()
    {
        var page = new LibraryViewModel(
            _library.Player, _library.Cleaner, _library.Running, _library.Installer, _library.Platform, new FakeDialogs(), _navigator, new FakePages());
        await page.RefreshAsync();
        return page;
    }

    private static (Window Window, ListBox List) Show(LibraryViewModel page)
    {
        var window = new Window { Content = new LibraryView { DataContext = page }, Width = 860, Height = 680 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, window.GetVisualDescendants().OfType<ListBox>().Single());
    }

    public void Dispose() => _library.Dispose();
}
