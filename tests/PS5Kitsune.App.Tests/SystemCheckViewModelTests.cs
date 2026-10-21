using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
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
    private readonly Navigator _navigator = new();

    private SystemCheckViewModel Create(Func<CancellationToken, Task<SystemCheckReport>> answer) =>
        new(new FakeChecker(answer), _platform, _navigator, new FakePages());

    [Fact]
    public async Task Shows_the_results_and_the_verdict()
    {
        var viewModel = Create(_ => Task.FromResult(Report()));

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
        var viewModel = Create(_ => Task.FromResult(report));
        Assert.Null(viewModel.BannerStatus);

        await viewModel.CheckCommand.ExecuteAsync(null);

        Assert.Equal(worst, viewModel.Overall);
        Assert.Equal(banner, viewModel.BannerStatus);
    }

    [Fact]
    public async Task Is_checking_until_the_checker_answers()
    {
        var answer = new TaskCompletionSource<SystemCheckReport>();
        var viewModel = Create(_ => answer.Task);

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
        var viewModel = Create(_ => Task.FromResult(Report()));

        await viewModel.CheckCommand.ExecuteAsync(null);
        await viewModel.CheckCommand.ExecuteAsync(null);

        Assert.Equal(2, viewModel.Checks.Count);
        Assert.Single(viewModel.Gpus);
    }

    [Fact]
    public async Task A_checker_failure_shows_its_message()
    {
        var viewModel = Create(_ => throw new IOException("disk gone"));

        await viewModel.CheckCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsChecking);
        Assert.Equal(CheckStatus.Blocking, viewModel.Overall);
        Assert.Contains("disk gone", viewModel.Verdict, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_fix_button_asks_the_platform()
    {
        var viewModel = Create(_ => Task.FromResult(Report()));
        await viewModel.CheckCommand.ExecuteAsync(null);

        var withFix = viewModel.Checks[1];
        withFix.OpenFixCommand.Execute(null);

        Assert.False(viewModel.Checks[0].HasFix);
        Assert.True(withFix.HasFix);
        Assert.True(withFix.HasAction);
        Assert.Equal([CheckFix.OpenPerformanceSettings], _platform.OpenedFixes);
    }

    [Theory]
    [InlineData(CheckStatus.Ok, true)]
    [InlineData(CheckStatus.Info, true)]
    [InlineData(CheckStatus.Warning, true)]
    [InlineData(CheckStatus.Blocking, false)]
    public async Task Continue_is_blocked_only_by_a_blocking_result(CheckStatus worst, bool canContinue)
    {
        var report = new SystemCheckReport([new CheckResult("a", "A", worst, "s")], Report().Vulkan);
        var viewModel = Create(_ => Task.FromResult(report));

        await viewModel.CheckCommand.ExecuteAsync(null);

        Assert.Equal(canContinue, viewModel.CanContinue);
        Assert.Equal(canContinue, viewModel.ContinueCommand.CanExecute(null));
    }

    [Fact]
    public void Continue_is_disabled_before_the_check()
    {
        Assert.False(Create(_ => Task.FromResult(Report())).CanContinue);
    }

    [Fact]
    public async Task Continue_goes_to_the_mode_page()
    {
        var viewModel = Create(_ => Task.FromResult(Report()));
        await viewModel.CheckCommand.ExecuteAsync(null);

        viewModel.ContinueCommand.Execute(null);

        Assert.Equal("mode", _navigator.CurrentPage);
    }

    [Fact]
    public async Task Entering_the_page_checks_once()
    {
        var runs = 0;
        var viewModel = Create(_ =>
        {
            runs++;
            return Task.FromResult(Report());
        });

        viewModel.OnNavigatedTo();
        await Task.Yield();
        viewModel.OnNavigatedTo();

        Assert.Equal(1, runs);
    }

    [Fact]
    public async Task Cancelling_says_so_and_allows_checking_again()
    {
        var viewModel = Create(async cancellationToken =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return Report();
        });
        var running = viewModel.CheckCommand.ExecuteAsync(null);

        viewModel.CheckCommand.Cancel();
        await running;

        Assert.False(viewModel.IsChecking);
        Assert.Null(viewModel.Overall);
        Assert.Equal("Check cancelled.", viewModel.Verdict);
        Assert.False(viewModel.CanContinue);
    }

    [Fact]
    public void Back_goes_to_the_previous_page()
    {
        _navigator.GoTo("home");
        var viewModel = Create(_ => Task.FromResult(Report()));
        _navigator.GoTo(viewModel);

        viewModel.BackCommand.Execute(null);

        Assert.Equal("home", _navigator.CurrentPage);
    }

    [Fact]
    public async Task A_fix_that_cannot_open_shows_the_error()
    {
        var viewModel = Create(_ => Task.FromResult(Report()));
        await viewModel.CheckCommand.ExecuteAsync(null);
        _platform.OpenFailure = new System.ComponentModel.Win32Exception("not found");

        viewModel.Checks[1].OpenFixCommand.Execute(null);

        Assert.Contains("not found", viewModel.Checks[1].FixError, StringComparison.Ordinal);
    }

    private sealed class FakeChecker(Func<CancellationToken, Task<SystemCheckReport>> answer) : ISystemChecker
    {
        public Task<SystemCheckReport> RunAsync(AnyPS5Mode mode, CancellationToken cancellationToken = default) => answer(cancellationToken);
    }
}
