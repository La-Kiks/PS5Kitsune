using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.App.Tests;

public sealed class ReconvertViewModelTests
{
    private const string Kept = "The previous conversion was kept; the game runs as before.";

    private readonly FakeGameReconverter _reconverter = new();
    private readonly Navigator _navigator = new();
    private readonly List<bool> _busy = [];

    // Not navigated to: showing the page would start the run, and the tests start it themselves.
    private ReconvertViewModel Create() =>
        new(_reconverter, "PPSA99666", "DOOM", "app.log", new TestPlatform(), new InlineDispatcher(), _navigator, new FakePages(), _busy.Add);

    [Fact]
    public void Lists_the_steps_and_names_the_game()
    {
        var viewModel = Create();

        Assert.Equal("Reconverting DOOM", viewModel.Title);
        Assert.Equal(_reconverter.StepNames, viewModel.Steps.Select(step => step.Name));
        Assert.Equal("A Reconvert is in progress. Quitting cancels it and puts the previous conversion back.", viewModel.QuitMessage);
    }

    [Fact]
    public async Task A_successful_reconvert_opens_the_result()
    {
        var viewModel = Create();

        await viewModel.RunAsync();

        Assert.Equal(["PPSA99666"], _reconverter.Reconverted);
        Assert.Equal("reconverted:PPSA99666", _navigator.CurrentPage);
        Assert.Equal([true, false], _busy);
    }

    [Fact]
    public async Task A_failed_step_says_the_previous_conversion_was_kept_and_can_be_retried()
    {
        _reconverter.Reconvert = (_, _, _, _) =>
            throw new StepFailedException("Convert with the relinker", new GameImportException("The relinker failed with exit code 2.", "error: bad instruction"));
        var viewModel = Create();

        await viewModel.RunAsync();

        Assert.Equal("Convert with the relinker failed: The relinker failed with exit code 2." + Environment.NewLine + Kept, viewModel.Failure);
        Assert.Equal("error: bad instruction", viewModel.FailureDetails);
        Assert.True(viewModel.CanRetry);

        _reconverter.Reconvert = (_, _, _, _) => Task.FromResult(TestGameData.Reconverted());
        await viewModel.RetryCommand.ExecuteAsync(null);
        Assert.Equal("reconverted:PPSA99666", _navigator.CurrentPage);
    }

    [Fact]
    public async Task A_failed_restore_says_when_it_will_be_restored()
    {
        _reconverter.Reconvert = (_, _, _, _) => throw new ReconvertRestoreException(
            new StepFailedException("Audit the imports", new GameImportException("import_audit.py could not read its input (exit code 2).")),
            new IOException("in use"));
        var viewModel = Create();

        await viewModel.RunAsync();

        Assert.Equal(
            "Audit the imports failed: import_audit.py could not read its input (exit code 2)." + Environment.NewLine
                + "The previous conversion could not be restored yet; it will be restored on the next start or Reconvert.",
            viewModel.Failure);
    }

    [Fact]
    public async Task Without_an_install_it_cannot_be_retried()
    {
        _reconverter.Reconvert = (_, _, _, _) => throw new AnyPS5NotInstalledException();
        var viewModel = Create();

        await viewModel.RunAsync();

        Assert.Equal("AnyPS5 is not installed. Install it from Home first." + Environment.NewLine + Kept, viewModel.Failure);
        Assert.False(viewModel.CanRetry);
    }

    [Fact]
    public async Task Cancelling_goes_to_the_library()
    {
        _reconverter.Reconvert = (_, _, _, _) => Task.FromCanceled<GameReconverted>(new CancellationToken(canceled: true));
        var viewModel = Create();

        await viewModel.RunAsync();

        Assert.Equal("library", _navigator.CurrentPage);
    }

    [Fact]
    public async Task Closing_the_window_cancels_and_waits_for_the_restore()
    {
        var restored = false;
        _reconverter.Reconvert = async (_, _, _, token) =>
        {
            try
            {
                await Task.Delay(Timeout.Infinite, token);
            }
            finally
            {
                restored = true;
            }

            return TestGameData.Reconverted();
        };
        var viewModel = Create();
        var running = viewModel.RunAsync();

        Assert.True(viewModel.IsRunning);
        await viewModel.CancelAndWaitAsync();

        Assert.True(restored);
        await running;
    }
}
