using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.Platform.Linux;

/// <summary>
/// AnyPS5 imports shared memory through /dev/udmabuf with every Vulkan driver but NVIDIA's proprietary one. Without it the memory is copied,
/// and some games then fail; so a warning, never blocking. See the AnyPS5 USAGE.md and the 8a spec, "Checks on Linux".
/// </summary>
public sealed class UdmabufCheck : SystemCheck
{
    private const string NvidiaProprietary = "NvidiaProprietary";

    private const string Copied = "games copy shared memory instead of importing it, and some may fail.";

    internal string DevicePath { get; init; } = "/dev/udmabuf";

    internal Func<string, bool> CanReadWrite { get; init; } = OpensForReadWrite;

    public override string Id => "udmabuf";

    public override string Title => "/dev/udmabuf";

    public override CheckResult Evaluate(SystemFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.Vulkan.Report?.Gpus is { Count: > 0 } gpus && gpus.All(gpu => gpu.DriverId == NvidiaProprietary))
        {
            return Result(CheckStatus.Info, "Not needed: every GPU uses NVIDIA's proprietary driver.");
        }

        if (!File.Exists(DevicePath))
        {
            return Result(CheckStatus.Warning, $"This kernel does not provide /dev/udmabuf; {Copied}");
        }

        return CanReadWrite(DevicePath)
            ? Result(CheckStatus.Ok, "Read-write access.")
            : Result(CheckStatus.Warning, $"No read-write access to /dev/udmabuf; {Copied}", "sudo usermod -aG kvm $USER, then log out and in again.");
    }

    private static bool OpensForReadWrite(string path)
    {
        try
        {
            using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.ReadWrite);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
