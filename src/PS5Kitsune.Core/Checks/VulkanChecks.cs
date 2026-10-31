using PS5Kitsune.Core.Checks.Vulkan;
using PS5Kitsune.Core.Profiles;

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
    public override string Id => "vulkan-driver";

    public override string Title => "Vulkan driver";

    public override CheckResult Evaluate(SystemFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return facts.Vulkan.Report switch
        {
            null => Result(CheckStatus.Blocking, $"Vulkan enumeration failed. {facts.Vulkan.FailureReason}", facts.VulkanInstallAction),
            { LoaderFound: false } => Result(CheckStatus.Blocking, $"No Vulkan loader was found ({facts.VulkanLoaderName}).", facts.VulkanInstallAction),
            { Gpus.Count: 0 } report => Result(CheckStatus.Blocking, report.Error is null ? "Vulkan found no GPU." : $"Vulkan found no GPU: {report.Error}", facts.VulkanInstallAction),
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
                $"{string.Join("; ", lacking)}. {(gpus.Count == 1 ? "Games cannot render on this GPU." : "Games cannot render on these GPUs.")}",
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
        if (facts.Os == ProfileOs.Linux)
        {
            // Dozen is Mesa's Vulkan over Direct3D 12: Windows only.
            return Result(CheckStatus.Info, "Not applicable on this system.");
        }

        if (facts.Vulkan.Status == VulkanProbeStatus.SucceededWithoutDozen)
        {
            return Result(CheckStatus.Info, "The first Vulkan probe failed and the second, with Dozen disabled, succeeded; the launcher will disable Dozen when it starts a game.");
        }

        var installed = facts.VulkanDriverManifests.Any(manifest => Path.GetFileName(manifest).Contains("dzn", StringComparison.OrdinalIgnoreCase))
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
            : Result(CheckStatus.Warning, $"{bad.Name} uses driver {bad.DriverInfo}, known to crash AnyPS5 games.", "Install another NVIDIA driver version.");
    }
}
