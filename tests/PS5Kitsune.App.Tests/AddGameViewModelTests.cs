using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.App.Tests;

public sealed class AddGameViewModelTests
{
    private readonly FakeGameImporter _importer = new();
    private readonly Navigator _navigator = new();
    private readonly PreparedGame _game = TestGameData.Prepared();
    private readonly List<bool> _busy = [];

    // Not navigated to: showing the page would start the run, and the tests start it themselves.
    private AddGameViewModel Create(bool toIntel = false) =>
        new(_importer, _game, toIntel, "app.log", new TestPlatform(), new InlineDispatcher(), _navigator, new FakePages(), _busy.Add);

    [Fact]
    public void Lists_the_six_steps_before_starting()
    {
        var viewModel = new AddGameViewModel(_importer, _game, false, "app.log", new TestPlatform(), new InlineDispatcher(), _navigator, new FakePages());

        Assert.Equal(_importer.StepNames, viewModel.Steps.Select(step => step.Name));
        Assert.Equal("Adding a game is in progress. Quitting cancels it; adding it again resumes where it stopped.", viewModel.QuitMessage);
    }

    [Fact]
    public async Task A_successful_add_disposes_the_game_and_opens_the_result()
    {
        var viewModel = Create(toIntel: true);

        await viewModel.RunAsync();

        Assert.Equal([(_game, true)], _importer.Adds);
        Assert.True(_game.IsDisposed);
        Assert.Equal("result:PPSA99666", _navigator.CurrentPage);
        Assert.Equal([true, false], _busy);
    }

    [Fact]
    public async Task A_running_step_shows_an_indeterminate_bar()
    {
        var release = new TaskCompletionSource<GameAdded>();
        _importer.Add = (_, _, _, onEvent, _) =>
        {
            onEvent!(new StepEvent(StepEventKind.Started, "Copy the game", 1, 6));
            return release.Task;
        };
        var viewModel = Create();

        var running = viewModel.RunAsync();

        Assert.True(viewModel.ShowsProgress);
        Assert.True(viewModel.ProgressIndeterminate);
        release.SetResult(TestGameData.Added());
        await running;
    }

    [Fact]
    public async Task Cancelling_disposes_the_game_and_goes_home()
    {
        _importer.Add = (_, _, _, _, token) => Task.FromCanceled<GameAdded>(new CancellationToken(canceled: true));
        var viewModel = Create();

        await viewModel.RunAsync();

        Assert.True(_game.IsDisposed);
        Assert.Equal("home", _navigator.CurrentPage);
    }

    [Fact]
    public async Task A_failed_step_shows_its_summary_and_details_and_can_be_retried()
    {
        var failure = new GameImportException("The relinker failed with exit code 2.", "error: bad instruction");
        _importer.Add = (_, _, _, _, _) => throw new StepFailedException("Convert with the relinker", failure);
        var viewModel = Create();

        await viewModel.RunAsync();

        Assert.Equal("Convert with the relinker failed: The relinker failed with exit code 2.", viewModel.Failure);
        Assert.Equal("error: bad instruction", viewModel.FailureDetails);
        Assert.True(viewModel.CanRetry);
        Assert.False(_game.IsDisposed);

        _importer.Add = (_, _, _, _, _) => Task.FromResult(TestGameData.Added());
        await viewModel.RetryCommand.ExecuteAsync(null);
        Assert.Equal("result:PPSA99666", _navigator.CurrentPage);
    }

    [Fact]
    public async Task Without_an_install_the_add_cannot_be_retried()
    {
        _importer.Add = (_, _, _, _, _) => throw new AnyPS5NotInstalledException();
        var viewModel = Create();

        await viewModel.RunAsync();

        Assert.Equal("AnyPS5 is not installed. Install it from Home first.", viewModel.Failure);
        Assert.False(viewModel.CanRetry);
        Assert.False(viewModel.RetryCommand.CanExecute(null));
    }

    [Fact]
    public async Task Back_to_home_after_a_failure_disposes_the_game()
    {
        _importer.Add = (_, _, _, _, _) => throw new AnyPS5NotInstalledException();
        var viewModel = Create();
        await viewModel.RunAsync();

        await viewModel.BackHomeCommand.ExecuteAsync(null);

        Assert.True(_game.IsDisposed);
        Assert.Equal("home", _navigator.CurrentPage);
    }

    [Fact]
    public async Task Releasing_disposes_the_game()
    {
        var viewModel = Create();

        await viewModel.ReleaseAsync();

        Assert.True(_game.IsDisposed);
    }
}
