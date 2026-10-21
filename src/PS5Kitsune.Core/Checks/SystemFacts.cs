using System.Runtime.InteropServices;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Checks.Vulkan;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Checks;

/// <summary>Everything the checks read, collected once by <see cref="SystemFactsCollector"/>.</summary>
public sealed record SystemFacts
{
    public required Architecture OSArchitecture { get; init; }

    public required CpuFacts Cpu { get; init; }

    public required VulkanProbeOutcome Vulkan { get; init; }

    /// <summary>RAM + page file, in bytes; null where the OS has no commit limit.</summary>
    public ulong? CommitLimitBytes { get; init; }

    public IReadOnlyList<string> VulkanDriverManifests { get; init; } = [];

    /// <summary>Full path of the data root.</summary>
    public required string DataRoot { get; init; }

    /// <summary>Free space on the data root's drive; null when it could not be read.</summary>
    public long? FreeDiskBytes { get; init; }

    public required CheckThresholds Limits { get; init; }

    /// <summary>The install mode the check is for: build mode needs more disk space.</summary>
    public AnyPS5Mode Mode { get; init; } = AnyPS5Mode.Release;
}
