using System.Runtime.InteropServices;
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Checks.Vulkan;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Tests.Checks;

internal static class TestFacts
{
    public static VulkanGpu Radeon { get; } = new("AMD Radeon(TM) Graphics", 0x1002, "integrated", "1.3.280", "AmdProprietary", "AMD proprietary driver", "25.10.1", true, true);

    public static VulkanGpu IrisXe { get; } = new("Intel(R) Iris(R) Xe Graphics", 0x8086, "integrated", "1.3.289", "IntelProprietaryWindows", "Intel Corporation", "101.5762", false, true);

    public static VulkanGpu GeForce { get; } = new("NVIDIA GeForce RTX 3050 Laptop GPU", VulkanGpu.NvidiaVendorId, "discrete", "1.4.303", "NvidiaProprietary", "NVIDIA", "581.29", true, true);

    public static VulkanProbeOutcome Probe(params VulkanGpu[] gpus) => new(VulkanProbeStatus.Succeeded, new VulkanProbeReport(true, null, gpus), null);

    /// <summary>A PC on which every check is OK (cpu-vendor is always Info).</summary>
    public static SystemFacts Healthy() => new()
    {
        OSArchitecture = Architecture.X64,
        Cpu = new CpuFacts("AuthenticAMD", []),
        Vulkan = Probe(Radeon),
        CommitLimitBytes = 32UL << 30,
        DataRoot = @"C:\PS5Kitsune",
        FreeDiskBytes = 100L << 30,
        Limits = new CheckThresholds { NvidiaBadDrivers = ["610.47"] },
    };
}
