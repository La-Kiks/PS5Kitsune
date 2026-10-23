using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.App.Tests;

public sealed class ChooseGameViewModelTests
{
    private readonly FakeGameImporter _importer = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly Navigator _navigator = new();
    private readonly List<bool> _busy = [];

    private ChooseGameViewModel Create()
    {
        var viewModel = new ChooseGameViewModel(_importer, _dialogs, _navigator, new FakePages(), _busy.Add);
        _navigator.GoTo(viewModel);
        return viewModel;
    }

    [Fact]
    public async Task A_chosen_folder_opens_the_summary()
    {
        _dialogs.Folder = @"C:\games\doom";
        var viewModel = Create();

        await viewModel.ChooseFolderCommand.ExecuteAsync(null);

        Assert.Equal([@"C:\games\doom"], _importer.Prepared);
        Assert.Equal("summary:PPSA99666", _navigator.CurrentPage);
        Assert.Equal([true, false], _busy);
    }

    [Fact]
    public async Task A_chosen_zip_opens_the_summary()
    {
        _dialogs.File = @"C:\PS5Games\PPSA99666.zip";
        var viewModel = Create();

        await viewModel.ChooseZipCommand.ExecuteAsync(null);

        Assert.Equal([@"C:\PS5Games\PPSA99666.zip"], _importer.Prepared);
        Assert.Equal("summary:PPSA99666", _navigator.CurrentPage);
    }

    [Fact]
    public async Task A_cancelled_picker_does_nothing()
    {
        var viewModel = Create();

        await viewModel.ChooseFolderCommand.ExecuteAsync(null);

        Assert.Empty(_importer.Prepared);
        Assert.Same(viewModel, _navigator.CurrentPage);
    }

    [Fact]
    public async Task An_invalid_path_shows_its_message()
    {
        _importer.Prepare = (_, _) => throw new InvalidGameSourceException(@"No eboot.bin found in C:\PS5Games, in its app0 folder or in its single subfolder.");
        var viewModel = Create();

        await viewModel.OpenAsync(@"C:\PS5Games");

        Assert.Equal(@"No eboot.bin found in C:\PS5Games, in its app0 folder or in its single subfolder.", viewModel.Error);
        Assert.Same(viewModel, _navigator.CurrentPage);
        Assert.False(viewModel.IsPreparing);
    }

    [Fact]
    public async Task Preparing_shows_busy_and_can_be_cancelled()
    {
        var release = new TaskCompletionSource();
        _importer.Prepare = async (_, token) =>
        {
            await release.Task.WaitAsync(token);
            return TestGameData.Prepared();
        };
        var viewModel = Create();

        var opening = viewModel.OpenAsync(@"C:\games\doom");

        Assert.True(viewModel.IsPreparing);
        Assert.True(viewModel.IsRunning);
        Assert.Null(viewModel.QuitMessage);
        Assert.False(viewModel.ChooseFolderCommand.CanExecute(null));
        viewModel.CancelCommand.Execute(null);
        await opening;

        Assert.False(viewModel.IsPreparing);
        Assert.Null(viewModel.Error);
        Assert.Same(viewModel, _navigator.CurrentPage);
    }

    [Fact]
    public async Task A_result_arriving_after_cancel_is_disposed()
    {
        var game = TestGameData.Prepared();
        var release = new TaskCompletionSource();
        _importer.Prepare = async (_, _) =>
        {
            await release.Task;
            return game;   // ignores the token, as an extraction finishing its last file would
        };
        var viewModel = Create();

        var opening = viewModel.OpenAsync(@"C:\games\doom");
        viewModel.CancelCommand.Execute(null);
        release.SetResult();
        await opening;

        Assert.True(game.IsDisposed);
        Assert.Same(viewModel, _navigator.CurrentPage);
    }

    [Fact]
    public async Task Any_other_failure_shows_its_message_instead_of_escaping()
    {
        _importer.Prepare = (_, _) => throw new ArgumentException("The path is not valid.");
        var viewModel = Create();

        await viewModel.OpenAsync(@"C:\games\doom");

        Assert.Equal("The path is not valid.", viewModel.Error);
        Assert.False(viewModel.IsPreparing);
        Assert.Same(viewModel, _navigator.CurrentPage);
    }

    [Fact]
    public async Task The_page_stays_busy_until_a_late_result_is_disposed()
    {
        var release = new TaskCompletionSource();
        var game = TestGameData.Prepared();
        _importer.Prepare = async (_, _) =>
        {
            await release.Task;
            return game;
        };
        var viewModel = Create();

        var opening = viewModel.OpenAsync(@"C:\games\doom");
        viewModel.CancelCommand.Execute(null);
        release.SetResult();
        await opening;

        Assert.True(game.IsDisposed);
        Assert.False(viewModel.IsPreparing);
        Assert.Equal([true, false], _busy);
    }

    [Fact]
    public void Back_returns_home()
    {
        var viewModel = Create();

        viewModel.BackCommand.Execute(null);

        Assert.Equal("home", _navigator.CurrentPage);
    }
}
