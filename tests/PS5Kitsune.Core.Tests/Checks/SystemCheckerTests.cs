using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Checks.Vulkan;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.Checks;

public sealed class SystemCheckerTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FakePlatform _platform = new();
    private readonly FakeCpuInfo _cpu = new();
    private readonly FakeProbeRunner _probe = new();

    public void Dispose() => _temp.Dispose();

    private SystemFactsCollector CreateCollector() =>
        new(_platform, _cpu, _probe, new DataLayout(_temp.Path), ProfileLoader.LoadEmbedded());

    [Fact]
    public async Task Collects_every_fact()
    {
        _platform.CommitLimitBytes = 16UL << 30;
        _platform.VulkanDriverManifests = ["dzn_icd.json"];

        var facts = await CreateCollector().CollectAsync(TestContext.Current.CancellationToken);

        Assert.Equal(16UL << 30, facts.CommitLimitBytes);
        Assert.Equal(["dzn_icd.json"], facts.VulkanDriverManifests);
        Assert.Equal(Path.GetFullPath(_temp.Path), facts.DataRoot);
        Assert.True(facts.FreeDiskBytes > 0);
        Assert.Equal(13824, facts.Limits.CommitLimitMiB);
        Assert.Same(_probe.Outcome, facts.Vulkan);
        Assert.Equal("AuthenticAMD", facts.Cpu.Vendor);
    }

    [Fact]
    public async Task Runs_the_checks_in_order_and_reports_the_worst_status()
    {
        _platform.CommitLimitBytes = 8UL << 30;
        var checker = new SystemChecker(CreateCollector(), SystemChecks.All, NullLogger<SystemChecker>.Instance);

        var report = await checker.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            ["os-arch", "cpu-extensions", "cpu-vendor", "vulkan-driver", "vulkan-features", "commit-limit", "dozen", "nvidia-driver", "disk-space", "install-path"],
            report.Results.Select(result => result.Id));
        Assert.Equal(CheckStatus.Warning, report.Overall);
        Assert.False(report.HasBlocking);
        Assert.Same(_probe.Outcome, report.Vulkan);
    }

    [Theory]
    [InlineData(CheckStatus.Ok, "This PC can run AnyPS5.")]
    [InlineData(CheckStatus.Info, "This PC can run AnyPS5.")]
    [InlineData(CheckStatus.Warning, "This PC can run AnyPS5, with warnings.")]
    [InlineData(CheckStatus.Blocking, "Blocking problems: fix them before installing.")]
    public void Verdict_follows_the_worst_status(CheckStatus worst, string verdict)
    {
        var report = new SystemCheckReport([new CheckResult("a", "A", CheckStatus.Ok, "s"), new CheckResult("b", "B", worst, "s")], TestFacts.Probe());

        Assert.Equal(worst, report.Overall);
        Assert.Equal(verdict, report.Verdict);
        Assert.Equal(worst == CheckStatus.Blocking, report.HasBlocking);
    }

    [Fact]
    public void Core_registration_resolves_the_checker()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IPlatform>(_platform);
        services.AddPS5KitsuneCore(new DataLayout(_temp.Path), ProfileLoader.LoadEmbedded());
        using var provider = services.BuildServiceProvider();

        Assert.IsType<SystemChecker>(provider.GetRequiredService<ISystemChecker>());
        Assert.Equal(10, provider.GetServices<ISystemCheck>().Count());
    }

    private sealed class FakeCpuInfo : ICpuInfo
    {
        public CpuFacts Read() => new("AuthenticAMD", []);
    }

    private sealed class FakeProbeRunner : IVulkanProbeRunner
    {
        public VulkanProbeOutcome Outcome { get; } = TestFacts.Probe(TestFacts.Radeon);

        public Task<VulkanProbeOutcome> RunAsync(CancellationToken cancellationToken = default) => Task.FromResult(Outcome);
    }
}
