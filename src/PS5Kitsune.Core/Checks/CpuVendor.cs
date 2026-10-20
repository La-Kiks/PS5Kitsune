using System.Buffers.Binary;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace PS5Kitsune.Core.Checks;

/// <summary>CPU vendor from CPUID leaf 0. The full PC checks come with milestone 3.</summary>
public static class CpuVendor
{
    public static string Detect()
    {
        if (!X86Base.IsSupported)
        {
            return "unknown";
        }

        // The vendor string is EBX, EDX, ECX in that order, 4 ASCII bytes each.
        var (_, ebx, ecx, edx) = X86Base.CpuId(0, 0);
        Span<byte> vendor = stackalloc byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(vendor, ebx);
        BinaryPrimitives.WriteInt32LittleEndian(vendor[4..], edx);
        BinaryPrimitives.WriteInt32LittleEndian(vendor[8..], ecx);
        return Encoding.ASCII.GetString(vendor);
    }

    /// <summary>True for Intel CPUs, which need the relinker's --to-intel option.</summary>
    public static bool IsIntel(string vendor) => vendor == "GenuineIntel";
}
