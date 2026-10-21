using PS5Kitsune.Core.Checks.Vulkan;

namespace PS5Kitsune.Core.Tests.Checks;

public sealed class VulkanProbeTests
{
    internal const string IrisXeJson = """
        { "loaderFound": true, "error": null, "gpus": [
          { "name": "Intel(R) Iris(R) Xe Graphics", "vendorId": 32902, "deviceType": "integrated", "apiVersion": "1.3.289",
            "driverId": "IntelProprietaryWindows", "driverName": "Intel Corporation", "driverInfo": "101.5762",
            "shaderInt64": false, "bufferDeviceAddress": true } ] }
        """;

    [Fact]
    public void Reads_the_probe_json()
    {
        var report = VulkanProbeReport.FromJson(IrisXeJson);

        Assert.True(report.LoaderFound);
        Assert.Null(report.Error);
        var gpu = Assert.Single(report.Gpus);
        Assert.Equal("Intel(R) Iris(R) Xe Graphics", gpu.Name);
        Assert.Equal(0x8086u, gpu.VendorId);
        Assert.False(gpu.ShaderInt64);
        Assert.True(gpu.BufferDeviceAddress);
        Assert.False(gpu.SupportsAnyPS5);
        Assert.False(gpu.IsNvidia);
    }

    [Fact]
    public void Writes_camel_case_json_without_computed_properties()
    {
        var gpu = new VulkanGpu("GPU", VulkanGpu.NvidiaVendorId, "discrete", "1.3.0", "NvidiaProprietary", "NVIDIA", "610.47", true, true);

        var json = new VulkanProbeReport(true, null, [gpu]).ToJson();

        Assert.Contains("\"loaderFound\":true", json, StringComparison.Ordinal);
        Assert.Contains("\"driverInfo\":\"610.47\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("isNvidia", json, StringComparison.Ordinal);
        Assert.DoesNotContain("supportsAnyPS5", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_output_that_is_not_json()
    {
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => VulkanProbeReport.FromJson("Segmentation fault"));
    }

    [Fact]
    public void Describes_a_gpu_in_one_line()
    {
        var gpu = new VulkanGpu("Radeon", 0x1002, "integrated", "1.3.280", "AmdProprietary", "AMD", "25.10", true, false);

        Assert.Equal("Radeon (integrated), driver 25.10, shaderInt64: yes, bufferDeviceAddress: no", gpu.Describe());
    }

    [Fact]
    public void Dozen_is_recognized_by_its_driver_id()
    {
        var gpu = new VulkanGpu("Microsoft Direct3D12", 0x1414, "integrated", "1.2.0", "MesaDozen", "Dozen", "Mesa 24", true, true);

        Assert.True(gpu.IsDozen);
    }

    // Runs the real probe in this process: on a machine whose driver crashes enumeration this test crashes too,
    // which is acceptable for a test run (the app itself never probes in-process).
    [Fact]
    public void Real_probe_output_round_trips_through_json()
    {
        var report = VulkanProbe.Run();

        var parsed = VulkanProbeReport.FromJson(report.ToJson());

        Assert.Equal(report.LoaderFound, parsed.LoaderFound);
        Assert.Equal(report.Error, parsed.Error);
        Assert.Equal(report.Gpus.Count, parsed.Gpus.Count);
        Assert.All(parsed.Gpus, gpu => Assert.False(string.IsNullOrEmpty(gpu.Name)));
    }
}
