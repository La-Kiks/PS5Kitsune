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
                more.Flyout?.Hide();
                return isOpen;
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(opened);
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
