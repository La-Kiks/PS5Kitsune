using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Checks.Vulkan;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Checks;

/// <summary>Gathers every fact once, so each check stays a pure function and the probe runs once.</summary>
public sealed class SystemFactsCollector(IPlatform platform, ICpuInfo cpu, IVulkanProbeRunner vulkan, DataLayout layout, AnyPS5Profile profile)
{
    public async Task<SystemFacts> CollectAsync(CancellationToken cancellationToken = default) => new()
    {
        OSArchitecture = RuntimeInformation.OSArchitecture,
        Cpu = cpu.Read(),
        Vulkan = await vulkan.RunAsync(cancellationToken).ConfigureAwait(false),
        CommitLimitBytes = platform.GetCommitLimitBytes(),
        VulkanDriverManifests = platform.GetVulkanDriverManifests(),
        DataRoot = layout.Root,
        FreeDiskBytes = FreeSpace(layout.Root),
        Limits = profile.Checks,
    };

    // The data root may not exist yet: its drive is what matters.
    private static long? FreeSpace(string root)
    {
        try
        {
            return new DriveInfo(Path.GetPathRoot(root) ?? root).AvailableFreeSpace;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}

public sealed record SystemCheckReport(IReadOnlyList<CheckResult> Results, VulkanProbeOutcome Vulkan)
{
    public CheckStatus Overall => Results.Count == 0 ? CheckStatus.Ok : Results.Max(result => result.Status);

    public bool HasBlocking => Overall == CheckStatus.Blocking;

    public string Verdict => Overall switch
    {
        CheckStatus.Blocking => "Blocking problems: fix them before installing.",
        CheckStatus.Warning => "This PC can run AnyPS5, with warnings.",
        _ => "This PC can run AnyPS5.",
    };
}

public interface ISystemChecker
{
    Task<SystemCheckReport> RunAsync(CancellationToken cancellationToken = default);
}

public sealed partial class SystemChecker(SystemFactsCollector collector, IEnumerable<ISystemCheck> checks, ILogger<SystemChecker> logger) : ISystemChecker
{
    public async Task<SystemCheckReport> RunAsync(CancellationToken cancellationToken = default)
    {
        var facts = await collector.CollectAsync(cancellationToken).ConfigureAwait(false);
        var results = checks.Select(check => check.Evaluate(facts)).ToList();
        foreach (var result in results)
        {
            LogResult(result.Id, result.Status, result.Summary);
        }

        return new SystemCheckReport(results, facts.Vulkan);
    }

    // Debug: the hosts print the results themselves; the log file keeps them.
    [LoggerMessage(Level = LogLevel.Debug, Message = "Check {Id}: {Status}. {Summary}")]
    private partial void LogResult(string id, CheckStatus status, string summary);
}

/// <summary>Every check, in the order the hosts show them (see the spec's checks table).</summary>
public static class SystemChecks
{
    public static IReadOnlyList<ISystemCheck> All { get; } =
    [
        new OsArchitectureCheck(),
        new CpuExtensionsCheck(),
        new CpuVendorCheck(),
        new VulkanDriverCheck(),
        new VulkanFeaturesCheck(),
        new CommitLimitCheck(),
        new DozenCheck(),
        new NvidiaDriverCheck(),
        new DiskSpaceCheck(),
        new InstallPathCheck(),
    ];
}
