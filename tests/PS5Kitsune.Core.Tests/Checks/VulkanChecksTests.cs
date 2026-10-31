using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Checks.Vulkan;

namespace PS5Kitsune.Core.Tests.Checks;

public sealed class VulkanChecksTests
{
    private static SystemFacts With(VulkanProbeOutcome probe) => TestFacts.Healthy() with { Vulkan = probe };

    [Fact]
    public void A_gpu_makes_the_driver_check_ok()
    {
        var result = new VulkanDriverCheck().Evaluate(TestFacts.Healthy());

        Assert.Equal(CheckStatus.Ok, result.Status);
        Assert.Contains("AMD Radeon(TM) Graphics", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void A_failed_probe_is_blocking_with_its_reason()
    {
        var result = new VulkanDriverCheck().Evaluate(With(VulkanProbeOutcome.Failed("The probe exited with code -1.")));

        Assert.Equal(CheckStatus.Blocking, result.Status);
        Assert.Contains("exited with code -1", result.Summary, StringComparison.Ordinal);
        Assert.NotNull(result.Action);
    }

    [Fact]
    public void A_missing_loader_is_blocking()
    {
        var probe = new VulkanProbeOutcome(VulkanProbeStatus.Succeeded, new VulkanProbeReport(false, "vulkan-1.dll not found", []), null);

        Assert.Equal(CheckStatus.Blocking, new VulkanDriverCheck().Evaluate(With(probe)).Status);
    }

    [Fact]
    public void No_gpu_is_blocking()
    {
        Assert.Equal(CheckStatus.Blocking, new VulkanDriverCheck().Evaluate(With(TestFacts.Probe())).Status);
    }

    [Fact]
    public void Iris_xe_without_shaderInt64_is_a_warning_mentioning_lavapipe()
    {
        var facts = With(new VulkanProbeOutcome(VulkanProbeStatus.Succeeded, VulkanProbeReport.FromJson(VulkanProbeTests.IrisXeJson), null));

        var result = new VulkanFeaturesCheck().Evaluate(facts);

        Assert.Equal(CheckStatus.Warning, result.Status);
        Assert.Contains("Iris(R) Xe", result.Summary, StringComparison.Ordinal);
        Assert.Contains("shaderInt64", result.Summary, StringComparison.Ordinal);
        Assert.Contains("Lavapipe", result.Action, StringComparison.Ordinal);
    }

    [Fact]
    public void Several_gpus_without_the_features_say_these_gpus()
    {
        var result = new VulkanFeaturesCheck().Evaluate(With(TestFacts.Probe(TestFacts.IrisXe, TestFacts.IrisXe with { Name = "Other" })));

        Assert.Contains("Games cannot render on these GPUs.", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Intel_igpu_plus_nvidia_names_the_usable_gpu()
    {
        var result = new VulkanFeaturesCheck().Evaluate(With(TestFacts.Probe(TestFacts.IrisXe, TestFacts.GeForce)));

        Assert.Equal(CheckStatus.Ok, result.Status);
        Assert.StartsWith("NVIDIA GeForce RTX 3050 Laptop GPU", result.Summary, StringComparison.Ordinal);
        Assert.Contains("Iris(R) Xe Graphics lacks shaderInt64", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void A_gpu_with_both_features_is_ok()
    {
        Assert.Equal(CheckStatus.Ok, new VulkanFeaturesCheck().Evaluate(TestFacts.Healthy()).Status);
    }

    [Fact]
    public void Features_and_nvidia_are_skipped_without_a_working_driver()
    {
        var facts = With(VulkanProbeOutcome.Failed("crash"));

        Assert.Equal(CheckStatus.Info, new VulkanFeaturesCheck().Evaluate(facts).Status);
        Assert.Equal(CheckStatus.Info, new NvidiaDriverCheck().Evaluate(facts).Status);
    }

    [Fact]
    public void Nvidia_610_47_is_a_warning()
    {
        var bad = TestFacts.GeForce with { DriverInfo = "610.47" };

        var result = new NvidiaDriverCheck().Evaluate(With(TestFacts.Probe(bad)));

        Assert.Equal(CheckStatus.Warning, result.Status);
        Assert.Equal("NVIDIA GeForce RTX 3050 Laptop GPU uses driver 610.47, known to crash AnyPS5 games.", result.Summary);
    }

    [Fact]
    public void Another_nvidia_driver_is_ok()
    {
        Assert.Equal(CheckStatus.Ok, new NvidiaDriverCheck().Evaluate(With(TestFacts.Probe(TestFacts.GeForce))).Status);
    }

    [Fact]
    public void No_nvidia_gpu_is_ok()
    {
        Assert.Equal(CheckStatus.Ok, new NvidiaDriverCheck().Evaluate(TestFacts.Healthy()).Status);
    }

    [Fact]
    public void Dozen_is_not_applicable_on_linux()
    {
        var result = new DozenCheck().Evaluate(TestFacts.Healthy() with { Os = ProfileOs.Linux });

        Assert.Equal(CheckStatus.Info, result.Status);
        Assert.Equal("Not applicable on this system.", result.Summary);
    }

    [Fact]
    public void No_dozen_is_ok()
    {
        Assert.Equal(CheckStatus.Ok, new DozenCheck().Evaluate(TestFacts.Healthy()).Status);
    }

    [Fact]
    public void A_dozen_manifest_is_info()
    {
        var facts = TestFacts.Healthy() with { VulkanDriverManifests = [@"C:\Mesa\dzn_icd.x86_64.json"] };

        Assert.Equal(CheckStatus.Info, new DozenCheck().Evaluate(facts).Status);
    }

    [Fact]
    public void A_folder_named_dzn_does_not_make_a_manifest_a_dozen_one()
    {
        var facts = TestFacts.Healthy() with { VulkanDriverManifests = [Path.Combine("C:", "Users", "dzn", "Drivers", "amd_icd64.json")] };

        Assert.Equal(CheckStatus.Ok, new DozenCheck().Evaluate(facts).Status);
    }

    [Fact]
    public void A_dozen_gpu_is_info()
    {
        var dozen = TestFacts.Radeon with { Name = "Microsoft Direct3D12 (AMD Radeon)", DriverId = "MesaDozen" };

        Assert.Equal(CheckStatus.Info, new DozenCheck().Evaluate(With(TestFacts.Probe(dozen))).Status);
    }

    [Fact]
    public void A_retry_without_dozen_explains_the_two_probes()
    {
        var probe = TestFacts.Probe(TestFacts.Radeon) with { Status = VulkanProbeStatus.SucceededWithoutDozen };

        var result = new DozenCheck().Evaluate(With(probe));

        Assert.Equal(CheckStatus.Info, result.Status);
        Assert.Equal("The first Vulkan probe failed and the second, with Dozen disabled, succeeded; the launcher will disable Dozen when it starts a game.", result.Summary);
    }

    [Fact]
    public void Dozen_is_still_checked_when_the_probe_failed()
    {
        var facts = With(VulkanProbeOutcome.Failed("crash")) with { VulkanDriverManifests = [@"C:\Mesa\dzn_icd.x86_64.json"] };

        Assert.Equal(CheckStatus.Info, new DozenCheck().Evaluate(facts).Status);
    }

    [Fact]
    public void A_missing_loader_names_the_platform_loader_and_its_fix()
    {
        var facts = TestFacts.Healthy() with
        {
            Vulkan = new VulkanProbeOutcome(VulkanProbeStatus.Succeeded, new VulkanProbeReport(false, "libvulkan.so.1 not found", []), null),
            VulkanLoaderName = "libvulkan.so.1",
            VulkanInstallAction = "Install Vulkan: sudo apt install libvulkan1 mesa-vulkan-drivers.",
        };

        var result = new VulkanDriverCheck().Evaluate(facts);

        Assert.Equal(CheckStatus.Blocking, result.Status);
        Assert.Equal("No Vulkan loader was found (libvulkan.so.1).", result.Summary);
        Assert.Equal("Install Vulkan: sudo apt install libvulkan1 mesa-vulkan-drivers.", result.Action);
    }
}
