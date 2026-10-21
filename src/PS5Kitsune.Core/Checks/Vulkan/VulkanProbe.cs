using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace PS5Kitsune.Core.Checks.Vulkan;

/// <summary>
/// Lists the GPUs and the features AnyPS5 needs. Runs only in the probe child process
/// (<c>--probe-vulkan</c>): some drivers crash the process during enumeration.
/// </summary>
public static unsafe class VulkanProbe
{
    private const uint ApiVersion11 = (1u << 22) | (1u << 12);
    private const uint ApiVersion12 = (1u << 22) | (2u << 12);

    public static VulkanProbeReport Run()
    {
        Vk vk;
        try
        {
            vk = Vk.GetApi();
        }
        catch (Exception exception)
        {
            // Silk.NET reports a missing loader with different exception types depending on the OS.
            return new VulkanProbeReport(LoaderFound: false, exception.Message, []);
        }

        using (vk)
        {
            return Enumerate(vk);
        }
    }

    private static VulkanProbeReport Enumerate(Vk vk)
    {
        uint loaderVersion;
        try
        {
            if (vk.EnumerateInstanceVersion(&loaderVersion) != Result.Success)
            {
                loaderVersion = 0;
            }
        }
        catch (Exception)
        {
            // vkEnumerateInstanceVersion does not exist in a Vulkan 1.0 loader.
            loaderVersion = 0;
        }

        if (loaderVersion < ApiVersion11)
        {
            return new VulkanProbeReport(true, "The Vulkan loader is older than 1.1.", []);
        }

        var instanceVersion = loaderVersion >= ApiVersion12 ? ApiVersion12 : ApiVersion11;
        var applicationName = (byte*)Marshal.StringToHGlobalAnsi("PS5Kitsune");
        try
        {
            var applicationInfo = new ApplicationInfo
            {
                SType = StructureType.ApplicationInfo,
                PApplicationName = applicationName,
                ApiVersion = instanceVersion,
            };
            var createInfo = new InstanceCreateInfo
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &applicationInfo,
            };
            Instance instance;
            var result = vk.CreateInstance(&createInfo, null, &instance);
            if (result != Result.Success)
            {
                return new VulkanProbeReport(true, $"vkCreateInstance returned {result}.", []);
            }

            try
            {
                return ListGpus(vk, instance, instanceVersion);
            }
            finally
            {
                vk.DestroyInstance(instance, null);
            }
        }
        finally
        {
            Marshal.FreeHGlobal((nint)applicationName);
        }
    }

    private static VulkanProbeReport ListGpus(Vk vk, Instance instance, uint instanceVersion)
    {
        uint count = 0;
        var result = vk.EnumeratePhysicalDevices(instance, &count, null);
        if (result != Result.Success)
        {
            return new VulkanProbeReport(true, $"vkEnumeratePhysicalDevices returned {result}.", []);
        }

        var devices = new PhysicalDevice[count];
        fixed (PhysicalDevice* pointer = devices)
        {
            result = vk.EnumeratePhysicalDevices(instance, &count, pointer);
        }

        if (result is not (Result.Success or Result.Incomplete))
        {
            return new VulkanProbeReport(true, $"vkEnumeratePhysicalDevices returned {result}.", []);
        }

        return new VulkanProbeReport(true, null, [.. devices.Take((int)count).Select(device => Describe(vk, device, instanceVersion))]);
    }

    private static VulkanGpu Describe(Vk vk, PhysicalDevice device, uint instanceVersion)
    {
        PhysicalDeviceProperties properties;
        vk.GetPhysicalDeviceProperties(device, &properties);

        // The 1.2 structs may only be chained when both the instance and the device speak Vulkan 1.2.
        var has12 = instanceVersion >= ApiVersion12 && properties.ApiVersion >= ApiVersion12;

        var driver = new PhysicalDeviceDriverProperties { SType = StructureType.PhysicalDeviceDriverProperties };
        var properties2 = new PhysicalDeviceProperties2
        {
            SType = StructureType.PhysicalDeviceProperties2,
            PNext = has12 ? &driver : null,
        };
        vk.GetPhysicalDeviceProperties2(device, &properties2);

        var features12 = new PhysicalDeviceVulkan12Features { SType = StructureType.PhysicalDeviceVulkan12Features };
        var features2 = new PhysicalDeviceFeatures2
        {
            SType = StructureType.PhysicalDeviceFeatures2,
            PNext = has12 ? &features12 : null,
        };
        vk.GetPhysicalDeviceFeatures2(device, &features2);

        return new VulkanGpu(
            Text(properties.DeviceName),
            properties.VendorID,
            DeviceType(properties.DeviceType),
            FormatVersion(properties.ApiVersion),
            has12 ? DriverName(driver.DriverID) : null,
            has12 ? Text(driver.DriverName) : null,
            has12 ? Text(driver.DriverInfo) : null,
            features2.Features.ShaderInt64,
            has12 && features12.BufferDeviceAddress);
    }

    // Silk.NET names the values "DriverIDAmdProprietary"; the report uses the Vulkan spec names ("AmdProprietary").
    private static string DriverName(DriverId id) => id.ToString().Replace("DriverID", "", StringComparison.Ordinal);

    private static string Text(byte* nullTerminated) => Marshal.PtrToStringUTF8((nint)nullTerminated) ?? "";

    private static string DeviceType(PhysicalDeviceType type) => type switch
    {
        PhysicalDeviceType.IntegratedGpu => "integrated",
        PhysicalDeviceType.DiscreteGpu => "discrete",
        PhysicalDeviceType.VirtualGpu => "virtual",
        PhysicalDeviceType.Cpu => "cpu",
        _ => "other",
    };

    private static string FormatVersion(uint version) => $"{(version >> 22) & 0x7F}.{(version >> 12) & 0x3FF}.{version & 0xFFF}";
}
