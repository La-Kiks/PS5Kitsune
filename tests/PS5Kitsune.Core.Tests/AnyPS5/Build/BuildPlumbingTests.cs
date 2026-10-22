using PS5Kitsune.Core.AnyPS5.Build;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.AnyPS5.Build;

public sealed class BuildPlumbingTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly BuildTestSetup _setup;

    public BuildPlumbingTests()
    {
        _setup = new BuildTestSetup(_temp.Path);
    }

    [Fact]
    public void Build_folders_sit_beside_current()
    {
        Assert.Equal(_temp.Combine("anyps5", "source"), _setup.Paths.Source);
        Assert.Equal(_temp.Combine("anyps5", "build"), _setup.Paths.Build);
        Assert.Equal(_temp.Combine("anyps5", "state", "compile.done"), _setup.Paths.Marker("compile"));
        Assert.Equal(_temp.Combine("anyps5", "build", "core", "relinker", "relinker.exe"), _setup.Paths.BuiltRelinker);
    }

    [Fact]
    public async Task A_marker_is_current_only_for_the_profile_commit()
    {
        Assert.False(_setup.Markers.IsCurrent("compile"));

        await _setup.Markers.WriteAsync("compile", TestContext.Current.CancellationToken);
        Assert.True(_setup.Markers.IsCurrent("compile"));
        Assert.Equal(BuildTestSetup.Commit, File.ReadAllText(_setup.Paths.Marker("compile")));

        var newer = new BuildTestSetup(_temp.Path, new string('a', 40));
        Assert.False(newer.Markers.IsCurrent("compile"));

        _setup.Markers.Clear("compile");
        Assert.False(_setup.Markers.IsCurrent("compile"));
    }

    [Theory]
    [InlineData(16, 32, 16)]
    [InlineData(16, 16, 8)]
    [InlineData(4, 64, 4)]
    [InlineData(8, 1, 1)]
    [InlineData(1, 0, 1)]
    public void Parallel_jobs_follow_cores_and_memory(int cores, int memoryGiB, int expected)
    {
        Assert.Equal(expected, ParallelJobs.Compute(cores, (long)memoryGiB << 30));
    }

    [Fact]
    public async Task Commands_run_with_the_build_environment()
    {
        await _setup.Commands.RunAsync(_setup.Commands.Git("status"), _ => "unused", null, TestContext.Current.CancellationToken);

        var spec = _setup.Runner.LastSpec!;
        Assert.Equal(_setup.MinGit.Executable, spec.FileName);
        Assert.Equal(_setup.Paths.Source, spec.WorkingDirectory);
        Assert.Equal(_setup.Environment.CaBundle, spec.Environment["SSL_CERT_FILE"]);
        Assert.Equal(_setup.Paths.Root, _setup.Commands.CMake("--version").WorkingDirectory);
    }

    [Fact]
    public async Task A_failure_gives_the_message_the_exit_code_and_the_last_lines()
    {
        _setup.Runner.Respond = _ => new FakeProcessResult(1, ["FAILED: core/main.cpp.obj", "main.cpp:1: error: boom"], ["warning: noise"]);

        var exception = await Assert.ThrowsAsync<BuildCommandException>(() =>
            _setup.Commands.RunAsync(_setup.Commands.CMake("--build", "x"), _ => "The build failed.", null, TestContext.Current.CancellationToken));

        Assert.StartsWith("The build failed.", exception.Message, StringComparison.Ordinal);
        Assert.Contains("exited with code 1", exception.Message, StringComparison.Ordinal);
        Assert.Contains("main.cpp:1: error: boom", exception.Message, StringComparison.Ordinal);
        Assert.Equal("The build failed.", exception.Summary);
        Assert.StartsWith("cmake exited with code 1.", exception.Details, StringComparison.Ordinal);
        Assert.Contains("main.cpp:1: error: boom", exception.Details, StringComparison.Ordinal);
        Assert.Equal(exception.Summary + Environment.NewLine + exception.Details, exception.Message);
    }

    [Fact]
    public async Task A_sub_build_counter_does_not_move_the_main_one()
    {
        _setup.Runner.Respond = _ => new FakeProcessResult(0, ["[1/10] a", "[1/3] sub", "[2/3] sub", "[2/10] b", "[1/20] c"]);
        var reports = new List<StepProgress>();

        await _setup.Commands.RunAsync(_setup.Commands.CMake("--build", "x"), _ => "", new SynchronousProgress(reports.Add), TestContext.Current.CancellationToken);

        Assert.Equal(
            [new StepFraction(1, 10), new StepFraction(2, 10), new StepFraction(1, 20)],
            reports.Where(report => report.Fraction is not null).Select(report => report.Fraction!.Value));
    }

    [Fact]
    public void A_command_failure_without_details_is_its_summary()
    {
        var exception = new BuildCommandException("no");

        Assert.Equal("no", exception.Message);
        Assert.Equal("no", exception.Summary);
        Assert.Equal("", exception.Details);
    }

    [Fact]
    public async Task A_certificate_error_is_flagged()
    {
        _setup.Runner.Respond = _ => new FakeProcessResult(1, StandardError: ["  status_code: 60", "  status_string: \"SSL peer certificate or SSH remote key was not OK\""]);
        CommandFailure? seen = null;

        await Assert.ThrowsAsync<BuildCommandException>(() => _setup.Commands.RunAsync(
            _setup.Commands.CMake("-S", "x"), failure => { seen = failure; return "configure"; }, null, TestContext.Current.CancellationToken));

        Assert.True(seen?.SawCertificateError);
    }

    [Fact]
    public async Task Low_disk_space_replaces_the_message()
    {
        _setup.FreeSpace.Bytes = 200L << 20;
        _setup.Runner.Respond = _ => new FakeProcessResult(1);

        var exception = await Assert.ThrowsAsync<BuildCommandException>(() =>
            _setup.Commands.RunAsync(_setup.Commands.CMake("--build", "x"), _ => "The build failed.", null, TestContext.Current.CancellationToken));

        Assert.StartsWith("Not enough disk space on ", exception.Message, StringComparison.Ordinal);
        Assert.Contains("0.2 GiB left", exception.Message, StringComparison.Ordinal);
        Assert.StartsWith("Not enough disk space on ", exception.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ninja_counters_and_error_lines_are_reported()
    {
        _setup.Runner.Respond = _ => new FakeProcessResult(0, ["[1/4] Building CXX a.obj", "[2/4] Building CXX b.obj", "a.cpp:3: error: x", "-- ordinary line"]);
        var reports = new List<StepProgress>();

        await _setup.Commands.RunAsync(_setup.Commands.CMake("--build", "x"), _ => "", new SynchronousProgress(reports.Add), TestContext.Current.CancellationToken);

        Assert.Equal([new StepFraction(1, 4), new StepFraction(2, 4)], reports.Where(report => report.Fraction is not null).Select(report => report.Fraction!.Value));
        Assert.Equal(["a.cpp:3: error: x"], reports.Where(report => report.Message is not null).Select(report => report.Message!));
    }

    [Fact]
    public async Task A_step_writes_its_marker_only_after_success()
    {
        var failing = new ProbeStep(_setup.Markers, () => throw new BuildCommandException("no"));
        await Assert.ThrowsAsync<BuildCommandException>(() => failing.ExecuteAsync(null, TestContext.Current.CancellationToken));
        Assert.False(failing.IsAlreadyDone());

        var working = new ProbeStep(_setup.Markers, () => Task.CompletedTask);
        await working.ExecuteAsync(null, TestContext.Current.CancellationToken);
        Assert.True(working.IsAlreadyDone());
    }

    [Fact]
    public async Task A_step_clears_its_marker_before_running()
    {
        await _setup.Markers.WriteAsync("probe", TestContext.Current.CancellationToken);
        var step = new ProbeStep(_setup.Markers, () => throw new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => step.ExecuteAsync(null, TestContext.Current.CancellationToken));

        Assert.False(step.IsAlreadyDone());
    }

    public void Dispose() => _temp.Dispose();

    private sealed class ProbeStep(BuildMarkers markers, Func<Task> run) : BuildCommandStep(markers, "probe")
    {
        public override string Name => "Probe";

        protected override Task RunCommandsAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken) => run();
    }

    private sealed class SynchronousProgress(Action<StepProgress> report) : IProgress<StepProgress>
    {
        public void Report(StepProgress value) => report(value);
    }
}
