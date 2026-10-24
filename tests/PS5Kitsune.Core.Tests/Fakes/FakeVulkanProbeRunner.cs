using PS5Kitsune.Core.Checks.Vulkan;

namespace PS5Kitsune.Core.Tests.Fakes;

/// <summary>Returns a set outcome; by default one AMD GPU that supports AnyPS5.</summary>
internal sealed class FakeVulkanProbeRunner : IVulkanProbeRunner
{
    public VulkanProbeOutcome Outcome { get; set; } = new(
        VulkanProbeStatus.Succeeded,
        new VulkanProbeReport(true, null, [new VulkanGpu("AMD Radeon(TM) Graphics", 0x1002, "integrated", "1.3.280", "AmdProprietary", "AMD proprietary driver", "25.10.1", true, true)]),
        null);

    public int Runs { get; private set; }

    public Task<VulkanProbeOutcome> RunAsync(CancellationToken cancellationToken = default)
    {
        Runs++;
        return Task.FromResult(Outcome);
    }
}
