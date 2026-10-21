using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Checks.Vulkan;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.Checks;

public sealed class VulkanProbeRunnerTests
{
    private const int AccessViolation = unchecked((int)0xC0000005);

    private readonly FakeProcessRunner _runner = new();

    private VulkanProbeRunner CreateRunner(IProcessRunner? runner = null, TimeSpan? timeout = null) =>
        new(runner ?? _runner, NullLogger<VulkanProbeRunner>.Instance, "probe.exe", timeout ?? TimeSpan.FromSeconds(15));

    private static FakeProcessResult Json() => new(0, [VulkanProbeTests.IrisXeJson]);

    [Fact]
    public async Task Returns_the_report_of_a_successful_probe()
    {
        _runner.Respond = _ => Json();

        var outcome = await CreateRunner().RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(VulkanProbeStatus.Succeeded, outcome.Status);
        Assert.Single(outcome.Report!.Gpus);
        var call = Assert.Single(_runner.Calls);
        Assert.Equal("probe.exe", call.FileName);
        Assert.Equal(["--probe-vulkan"], call.Arguments);
        Assert.Empty(call.Environment);
    }

    [Fact]
    public async Task Retries_without_Dozen_after_a_crash()
    {
        _runner.Respond = spec => spec.Environment.ContainsKey(VulkanProbeRunner.DisableDriversVariable) ? Json() : new FakeProcessResult(AccessViolation);

        var outcome = await CreateRunner().RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(VulkanProbeStatus.SucceededWithoutDozen, outcome.Status);
        Assert.NotNull(outcome.Report);
        Assert.Equal(2, _runner.Calls.Count);
        Assert.Equal("*dzn*", _runner.Calls[1].Environment[VulkanProbeRunner.DisableDriversVariable]);
    }

    [Fact]
    public async Task Fails_when_both_attempts_crash()
    {
        _runner.Respond = _ => new FakeProcessResult(AccessViolation, StandardError: ["driver exploded"]);

        var outcome = await CreateRunner().RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(VulkanProbeStatus.Failed, outcome.Status);
        Assert.Null(outcome.Report);
        Assert.Contains("0xC0000005", outcome.FailureReason, StringComparison.Ordinal);
        Assert.Contains("driver exploded", outcome.FailureReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fails_when_the_output_is_not_json()
    {
        _runner.Respond = _ => new FakeProcessResult(0, ["not json"]);

        var outcome = await CreateRunner().RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(VulkanProbeStatus.Failed, outcome.Status);
        Assert.Contains("not valid JSON", outcome.FailureReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_probe_that_hangs_times_out_twice()
    {
        var hanging = new HangingProcessRunner();

        var outcome = await CreateRunner(hanging, TimeSpan.FromMilliseconds(50)).RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(VulkanProbeStatus.Failed, outcome.Status);
        Assert.Contains("did not answer", outcome.FailureReason, StringComparison.Ordinal);
        Assert.Equal(2, hanging.Calls);
    }

    [Fact]
    public async Task User_cancellation_is_not_a_timeout()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateRunner(new HangingProcessRunner()).RunAsync(cancellation.Token));
    }

    private sealed class HangingProcessRunner : IProcessRunner
    {
        public int Calls { get; private set; }

        public async Task<ProcessResult> RunAsync(ProcessSpec spec, Action<OutputLine>? onOutput = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Unreachable.");
        }
    }
}
