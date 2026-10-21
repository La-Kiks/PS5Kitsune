using System.Text.Json;
using System.Text.Json.Serialization;

namespace PS5Kitsune.Core.Checks.Vulkan;

/// <summary>What the probe child process found, as it writes it to its standard output.</summary>
/// <param name="LoaderFound">False when the Vulkan loader (vulkan-1.dll, libvulkan.so.1) cannot be loaded.</param>
/// <param name="Error">Why enumeration stopped (a VkResult name or a message); null when it completed.</param>
public sealed record VulkanProbeReport(bool LoaderFound, string? Error, IReadOnlyList<VulkanGpu> Gpus)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <exception cref="JsonException">The text is not a probe report.</exception>
    public static VulkanProbeReport FromJson(string json) =>
        JsonSerializer.Deserialize<VulkanProbeReport>(json, Options) ?? throw new JsonException("The probe wrote an empty report.");
}

/// <param name="DeviceType">"integrated", "discrete", "virtual", "cpu" or "other".</param>
/// <param name="ApiVersion">Vulkan version the driver supports, for example "1.3.280".</param>
/// <param name="DriverId">VkDriverId name, for example "NvidiaProprietary"; null below Vulkan 1.2.</param>
/// <param name="DriverInfo">Driver version text ("610.47" on NVIDIA); null below Vulkan 1.2.</param>
public sealed record VulkanGpu(
    string Name,
    uint VendorId,
    string DeviceType,
    string ApiVersion,
    string? DriverId,
    string? DriverName,
    string? DriverInfo,
    bool ShaderInt64,
    bool BufferDeviceAddress)
{
    public const uint NvidiaVendorId = 0x10DE;

    [JsonIgnore]
    public bool IsNvidia => VendorId == NvidiaVendorId;

    /// <summary>Dozen: Mesa's Vulkan driver over Direct3D 12.</summary>
    [JsonIgnore]
    public bool IsDozen => DriverId == "MesaDozen";

    /// <summary>AnyPS5 needs both features on the GPU it renders with.</summary>
    [JsonIgnore]
    public bool SupportsAnyPS5 => ShaderInt64 && BufferDeviceAddress;

    public string Describe() =>
        $"{Name} ({DeviceType}), driver {DriverInfo ?? "unknown"}, shaderInt64: {YesNo(ShaderInt64)}, bufferDeviceAddress: {YesNo(BufferDeviceAddress)}";

    private static string YesNo(bool value) => value ? "yes" : "no";
}
