using PS5Kitsune.Core.Checks.Vulkan;

namespace PS5Kitsune.Core.Checks;

internal static class VulkanFacts
{
    /// <summary>The GPUs when the driver check passes; null otherwise, so later checks report "not checked".</summary>
    public static IReadOnlyList<VulkanGpu>? WorkingGpus(this SystemFacts facts) =>
        facts.Vulkan.Report is { LoaderFound: true, Gpus.Count: > 0 } report ? report.Gpus : null;

    public static string Names(IEnumerable<VulkanGpu> gpus) => string.Join(", ", gpus.Select(gpu => gpu.Name));
}

public sealed class VulkanDriverCheck : SystemCheck
{
    private const string UpdateDriver = "Install or update the graphics driver from your GPU maker's website (AMD, Intel or NVIDIA).";

    public override string Id => "vulkan-driver";

    public override string Title => "Vulkan driver";

    public override CheckResult Evaluate(SystemFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return facts.Vulkan.Report switch
        {
            null => Result(CheckStatus.Blocking, $"Vulkan enumeration failed. {facts.Vulkan.FailureReason}", UpdateDriver),
            { LoaderFound: false } => Result(CheckStatus.Blocking, "No Vulkan loader was found (vulkan-1.dll).", UpdateDriver),
            { Gpus.Count: 0 } report => Result(CheckStatus.Blocking, report.Error is null ? "Vulkan found no GPU." : $"Vulkan found no GPU: {report.Error}", UpdateDriver),
            var report => Result(CheckStatus.Ok, $"{report.Gpus.Count} GPU(s): {VulkanFacts.Names(report.Gpus)}."),
        };
    }
}

public sealed class VulkanFeaturesCheck : SystemCheck
{
    public override string Id => "vulkan-features";

    public override string Title => "GPU features (shaderInt64, bufferDeviceAddress)";

    public override CheckResult Evaluate(SystemFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.WorkingGpus() is not { } gpus)
        {
            return Result(CheckStatus.Info, "Not checked: no working Vulkan driver.");
        }

        var lacking = gpus.Where(gpu => !gpu.SupportsAnyPS5).Select(gpu => $"{gpu.Name} lacks {Missing(gpu)}").ToList();
        var usable = gpus.FirstOrDefault(gpu => gpu.SupportsAnyPS5);
        if (usable is null)
        {
            return Result(
                CheckStatus.Warning,
                $"{string.Join("; ", lacking)}. Games cannot render on this GPU.",
                "Games can still run with Lavapipe, a slower software renderer that the launcher will offer.");
        }

        return lacking.Count == 0
            ? Result(CheckStatus.Ok, gpus.Count == 1 ? $"{usable.Name} has both features." : "Every GPU has both features.")
            : Result(CheckStatus.Ok, $"{usable.Name} has both features and will run games; {string.Join("; ", lacking)}.");
    }

    private static string Missing(VulkanGpu gpu) => (gpu.ShaderInt64, gpu.BufferDeviceAddress) switch
    {
        (false, false) => "shaderInt64 and bufferDeviceAddress",
        (false, true) => "shaderInt64",
        _ => "bufferDeviceAddress",
    };
}

public sealed class DozenCheck : SystemCheck
{
    public override string Id => "dozen";

    public override string Title => "Dozen driver (Vulkan over Direct3D 12)";

    public override CheckResult Evaluate(SystemFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.Vulkan.Status == VulkanProbeStatus.SucceededWithoutDozen)
        {
            return Result(CheckStatus.Info, "Dozen made Vulkan enumeration crash; it was disabled for the check and the launcher will disable it when it starts a game.");
        }

        var installed = facts.VulkanDriverManifests.Any(manifest => manifest.Contains("dzn", StringComparison.OrdinalIgnoreCase))
            || (facts.Vulkan.Report?.Gpus.Any(gpu => gpu.IsDozen) ?? false);
        return installed
            ? Result(CheckStatus.Info, "Dozen is installed. It can crash Vulkan enumeration; the launcher can disable it when it starts a game.")
            : Result(CheckStatus.Ok, "Not installed.");
    }
}

public sealed class NvidiaDriverCheck : SystemCheck
{
    public override string Id => "nvidia-driver";

    public override string Title => "NVIDIA driver";

    public override CheckResult Evaluate(SystemFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.WorkingGpus() is not { } gpus)
        {
            return Result(CheckStatus.Info, "Not checked: no working Vulkan driver.");
        }

        var nvidia = gpus.Where(gpu => gpu.IsNvidia).ToList();
        if (nvidia.Count == 0)
        {
            return Result(CheckStatus.Ok, "No NVIDIA GPU.");
        }

        var bad = nvidia.FirstOrDefault(gpu => gpu.DriverInfo is { } version && facts.Limits.NvidiaBadDrivers.Contains(version));
        return bad is null
            ? Result(CheckStatus.Ok, $"Driver {string.Join(", ", nvidia.Select(gpu => gpu.DriverInfo ?? "unknown"))}.")
            : Result(CheckStatus.Warning, $"{bad.Name} uses driver {bad.DriverInfo}, known to crash AnyPS5 games (610.47 crashes in nvgpucomp64.dll).", "Install another NVIDIA driver version.");
    }
}
