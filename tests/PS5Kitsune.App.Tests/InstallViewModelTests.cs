using System.ComponentModel;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.AnyPS5.Build;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.App.Tests;

public sealed class InstallViewModelTests
{
    private readonly FakeInstaller _installer = new("Install Python", "Download the relinker");
    private readonly TestPlatform _platform = new();
    private readonly Navigator _navigator = new();
    private readonly List<bool> _busy = [];

    private InstallViewModel Create() =>
        new(_installer, AnyPS5Mode.Release, "C:/root/logs/app.log", _platform, new InlineDispatcher(), _navigator, new FakePages(), _busy.Add);

    private InstallViewModel CreateBuild() =>
        new(_installer, AnyPS5Mode.Build, "C:/root/logs/app.log", _platform, new InlineDispatcher(), _navigator, new FakePages(), _busy.Add);

    [Fact]
    public async Task Installs_in_the_mode_it_was_created_with()
    {
        var viewModel = Create();

        await viewModel.RunAsync();

        Assert.Equal(AnyPS5Mode.Release, _installer.InstalledMode);
    }

    [Fact]
    public void Lists_the_steps_before_starting()
    {
        var viewModel = Create();

        Assert.Equal(["Install Python", "Download the relinker"], viewModel.Steps.Select(step => step.Name));
        Assert.All(viewModel.Steps, step => Assert.Equal(InstallStepState.Pending, step.State));
    }

    [Fact]
    public async Task A_successful_install_goes_to_Done()
    {
        _installer.Install = (_, onEvent, _) =>
        {
            onEvent!(new StepEvent(StepEventKind.Skipped, "Install Python", 1, 2));
            onEvent(new StepEvent(StepEventKind.Started, "Download the relinker", 2, 2));
            onEvent(new StepEvent(StepEventKind.Completed, "Download the relinker", 2, 2, TimeSpan.FromSeconds(1.5)));
            return Task.CompletedTask;
        };
        var viewModel = Create();

        await viewModel.RunAsync();

        Assert.Equal("done", _navigator.CurrentPage);
        Assert.Equal([InstallStepState.Skipped, InstallStepState.Done], viewModel.Steps.Select(step => step.State));
        Assert.Equal(["Install Python: already done", "Download the relinker…", "Download the relinker: done in 1.5 s"], viewModel.Log);
        Assert.False(viewModel.IsRunning);
        Assert.Equal([true, false], _busy);
    }

    [Fact]
    public async Task A_failure_shows_the_step_and_Retry_resumes()
    {
        _installer.Install = (_, onEvent, _) =>
        {
            var cause = new IOException("disk full");
            onEvent!(new StepEvent(StepEventKind.Started, "Download the relinker", 2, 2));
            onEvent(new StepEvent(StepEventKind.Failed, "Download the relinker", 2, 2, TimeSpan.Zero, cause));
            throw new StepFailedException("Download the relinker", cause);
        };
        var viewModel = Create();

        await viewModel.RunAsync();

        Assert.True(viewModel.HasFailed);
        Assert.Equal("Download the relinker failed: disk full", viewModel.Failure);
        Assert.Equal(InstallStepState.Failed, viewModel.Steps[1].State);
        Assert.Null(_navigator.CurrentPage);

        _installer.Install = (_, _, _) => Task.CompletedTask;
        await viewModel.RetryCommand.ExecuteAsync(null);

        Assert.False(viewModel.HasFailed);
        Assert.Equal("done", _navigator.CurrentPage);
        Assert.Equal(2, _installer.Runs);
    }

    [Fact]
    public async Task Retry_rebuilds_the_rows_when_the_step_list_got_shorter()
    {
        _installer.StepNames = ["Remove the other mode's files", "Install Python", "Download the relinker"];
        _installer.Install = (_, _, _) => throw new StepFailedException("Download the relinker", new IOException("disk full"));
        var viewModel = Create();

        await viewModel.RunAsync();

        Assert.Equal(3, viewModel.Steps.Count);

        _installer.StepNames = ["Install Python", "Download the relinker"];
        _installer.Install = (_, onEvent, _) =>
        {
            onEvent!(new StepEvent(StepEventKind.Skipped, "Install Python", 1, 2));
            onEvent(new StepEvent(StepEventKind.Completed, "Download the relinker", 2, 2, TimeSpan.Zero));
            return Task.CompletedTask;
        };
        await viewModel.RetryCommand.ExecuteAsync(null);

        Assert.Equal(["Install Python", "Download the relinker"], viewModel.Steps.Select(step => step.Name));
        Assert.Equal([InstallStepState.Skipped, InstallStepState.Done], viewModel.Steps.Select(step => step.State));
    }

    [Fact]
    public async Task Cancel_goes_back_home()
    {
        _installer.Install = async (_, onEvent, cancellationToken) =>
        {
            onEvent!(new StepEvent(StepEventKind.Started, "Install Python", 1, 2));
            await Task.Delay(Timeout.Infinite, cancellationToken);
        };
        var viewModel = Create();

        var running = viewModel.RunAsync();
        Assert.True(viewModel.IsRunning);
        viewModel.CancelCommand.Execute(null);
        await running;

        Assert.Equal("home", _navigator.CurrentPage);
        Assert.False(viewModel.IsRunning);
    }

    [Fact]
    public async Task CancelAndWaitAsync_stops_a_running_install()
    {
        _installer.Install = (_, _, cancellationToken) => Task.Delay(Timeout.Infinite, cancellationToken);
        var viewModel = Create();
        _ = viewModel.RunAsync();

        await viewModel.CancelAndWaitAsync();

        Assert.False(viewModel.IsRunning);
    }

    [Fact]
    public async Task Download_progress_drives_the_bar_and_messages_go_to_the_log()
    {
        IProgress<StepProgress>? reported = null;
        var release = new TaskCompletionSource();
        _installer.Install = (progress, _, _) =>
        {
            reported = progress;
            return release.Task;
        };
        var viewModel = Create();
        var running = viewModel.RunAsync();

        reported!.Report(new StepProgress(Download: new(50, 200)));
        Assert.True(viewModel.ShowsProgress);
        Assert.False(viewModel.ProgressIndeterminate);
        Assert.Equal(25, viewModel.ProgressPercent);
        Assert.Equal("0.0 / 0.0 MB", viewModel.ProgressText);

        reported.Report(new StepProgress(Download: new(50, null)));
        Assert.True(viewModel.ProgressIndeterminate);

        reported.Report(new StepProgress("Extracting libs"));
        Assert.Contains("Extracting libs", viewModel.Log);

        release.SetResult();
        await running;
        Assert.False(viewModel.ShowsProgress);
    }

    [Fact]
    public async Task A_build_counter_drives_the_bar()
    {
        IProgress<StepProgress>? reported = null;
        Action<StepEvent>? events = null;
        var release = new TaskCompletionSource();
        _installer.Install = (progress, onEvent, _) =>
        {
            reported = progress;
            events = onEvent;
            return release.Task;
        };
        var viewModel = CreateBuild();
        var running = viewModel.RunAsync();

        events!(new StepEvent(StepEventKind.Started, "Compile AnyPS5", 2, 2));
        Assert.True(viewModel.ShowsProgress);
        Assert.True(viewModel.ProgressIndeterminate);
        Assert.Equal("Compile AnyPS5", viewModel.ProgressText);

        reported!.Report(new StepProgress(Fraction: new StepFraction(91, 364)));
        Assert.False(viewModel.ProgressIndeterminate);
        Assert.Equal(25, viewModel.ProgressPercent);
        Assert.Equal("91 / 364 units", viewModel.ProgressText);

        events(new StepEvent(StepEventKind.Completed, "Compile AnyPS5", 2, 2, TimeSpan.FromSeconds(1)));
        Assert.False(viewModel.ShowsProgress);

        release.SetResult();
        await running;
    }

    [Fact]
    public async Task A_release_step_without_a_download_shows_no_bar()
    {
        Action<StepEvent>? events = null;
        var release = new TaskCompletionSource();
        _installer.Install = (_, onEvent, _) =>
        {
            events = onEvent;
            return release.Task;
        };
        var viewModel = Create();
        var running = viewModel.RunAsync();

        events!(new StepEvent(StepEventKind.Started, "Download the relinker", 2, 2));

        Assert.False(viewModel.ShowsProgress);
        release.SetResult();
        await running;
    }

    [Fact]
    public async Task A_build_failure_shows_its_summary_and_details()
    {
        _installer.Install = (_, _, _) => throw new StepFailedException(
            "Compile AnyPS5",
            new BuildCommandException("The build failed. The log holds the full compiler output.", "cmake exited with code 1.\nmain.cpp:1: error: boom"));
        var viewModel = CreateBuild();

        await viewModel.RunAsync();

        Assert.Equal("Compile AnyPS5 failed: The build failed. The log holds the full compiler output.", viewModel.Failure);
        Assert.Equal("cmake exited with code 1.\nmain.cpp:1: error: boom", viewModel.FailureDetails);
    }

    [Fact]
    public async Task Another_failure_has_no_details()
    {
        _installer.Install = (_, _, _) => throw new StepFailedException("Download the relinker", new IOException("disk full"));
        var viewModel = Create();

        await viewModel.RunAsync();

        Assert.Equal("Download the relinker failed: disk full", viewModel.Failure);
        Assert.Null(viewModel.FailureDetails);
    }

    [Fact]
    public async Task Retry_clears_the_details()
    {
        _installer.Install = (_, _, _) => throw new StepFailedException("Compile AnyPS5", new BuildCommandException("The build failed.", "details"));
        var viewModel = CreateBuild();
        await viewModel.RunAsync();

        _installer.Install = (_, _, _) => Task.CompletedTask;
        await viewModel.RetryCommand.ExecuteAsync(null);

        Assert.Null(viewModel.FailureDetails);
    }

    [Fact]
    public void Entering_the_page_starts_the_install_once()
    {
        var viewModel = Create();

        viewModel.OnNavigatedTo();
        viewModel.OnNavigatedTo();

        Assert.Equal(1, _installer.Runs);
    }

    [Fact]
    public void Open_log_opens_the_session_log_or_shows_why_not()
    {
        var viewModel = Create();

        viewModel.OpenLogCommand.Execute(null);
        Assert.Equal(["C:/root/logs/app.log"], _platform.OpenedPaths);

        _platform.OpenFailure = new Win32Exception("no app");
        viewModel.OpenLogCommand.Execute(null);
        Assert.Contains("no app", viewModel.OpenError, StringComparison.Ordinal);
    }
}
