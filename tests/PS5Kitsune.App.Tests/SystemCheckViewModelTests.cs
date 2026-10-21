using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Checks.Vulkan;

namespace PS5Kitsune.App.Tests;

public sealed class SystemCheckViewModelTests
{
    private static readonly VulkanGpu Radeon = new("AMD Radeon(TM) Graphics", 0x1002, "integrated", "1.3.280", "AmdProprietary", "AMD", "25.10.1", true, true);

    private static SystemCheckReport Report() => new(
        [
            new CheckResult("os-arch", "64-bit x86 system", CheckStatus.Ok, "64-bit x86 system."),
            new CheckResult("commit-limit", "Memory commit limit", CheckStatus.Warning, "Too low.", "Enlarge the page file.", CheckFix.OpenPerformanceSettings),
        ],
        new VulkanProbeOutcome(VulkanProbeStatus.Succeeded, new VulkanProbeReport(true, null, [Radeon]), null));

    private readonly TestPlatform _platform = new();

    [Fact]
    public async Task Shows_the_results_and_the_verdict()
    {
        var viewModel = new SystemCheckViewModel(new FakeChecker(() => Task.FromResult(Report())), _platform);

        await viewModel.CheckCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsChecking);
        Assert.Equal(CheckStatus.Warning, viewModel.Overall);
        Assert.Equal("This PC can run AnyPS5, with warnings.", viewModel.Verdict);
        Assert.Equal(["OK", "Warning"], viewModel.Checks.Select(row => row.StatusText));
        Assert.Equal([Radeon.Describe()], viewModel.Gpus);
    }

    [Theory]
    [InlineData(CheckStatus.Ok, CheckStatus.Ok)]
    [InlineData(CheckStatus.Info, CheckStatus.Ok)]
    [InlineData(CheckStatus.Warning, CheckStatus.Warning)]
    [InlineData(CheckStatus.Blocking, CheckStatus.Blocking)]
    public async Task The_banner_shows_Info_as_Ok(CheckStatus worst, CheckStatus banner)
    {
        var report = new SystemCheckReport([new CheckResult("a", "A", worst, "s")], Report().Vulkan);
        var viewModel = new SystemCheckViewModel(new FakeChecker(() => Task.FromResult(report)), _platform);
        Assert.Null(viewModel.BannerStatus);

        await viewModel.CheckCommand.ExecuteAsync(null);

        Assert.Equal(worst, viewModel.Overall);
        Assert.Equal(banner, viewModel.BannerStatus);
    }

    [Fact]
    public async Task Is_checking_until_the_checker_answers()
    {
        var answer = new TaskCompletionSource<SystemCheckReport>();
        var viewModel = new SystemCheckViewModel(new FakeChecker(() => answer.Task), _platform);

        var running = viewModel.CheckCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsChecking);
        Assert.Equal("Checking this PC…", viewModel.Verdict);
        Assert.Null(viewModel.Overall);
        answer.SetResult(Report());
        await running;
        Assert.False(viewModel.IsChecking);
    }

    [Fact]
    public async Task Checking_again_replaces_the_results()
    {
        var viewModel = new SystemCheckViewModel(new FakeChecker(() => Task.FromResult(Report())), _platform);

        await viewModel.CheckCommand.ExecuteAsync(null);
        await viewModel.CheckCommand.ExecuteAsync(null);

        Assert.Equal(2, viewModel.Checks.Count);
        Assert.Single(viewModel.Gpus);
    }

    [Fact]
    public async Task A_checker_failure_shows_its_message()
    {
        var viewModel = new SystemCheckViewModel(new FakeChecker(() => throw new IOException("disk gone")), _platform);

        await viewModel.CheckCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsChecking);
        Assert.Equal(CheckStatus.Blocking, viewModel.Overall);
        Assert.Contains("disk gone", viewModel.Verdict, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_fix_button_asks_the_platform()
    {
        var viewModel = new SystemCheckViewModel(new FakeChecker(() => Task.FromResult(Report())), _platform);
        await viewModel.CheckCommand.ExecuteAsync(null);

        var withFix = viewModel.Checks[1];
        withFix.OpenFixCommand.Execute(null);

        Assert.False(viewModel.Checks[0].HasFix);
        Assert.True(withFix.HasFix);
        Assert.True(withFix.HasAction);
        Assert.Equal([CheckFix.OpenPerformanceSettings], _platform.OpenedFixes);
    }

    private sealed class FakeChecker(Func<Task<SystemCheckReport>> answer) : ISystemChecker
    {
        public Task<SystemCheckReport> RunAsync(CancellationToken cancellationToken = default) => answer();
    }
}
