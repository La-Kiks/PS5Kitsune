using System.Buffers.Binary;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace PS5Kitsune.Core.Checks;

/// <param name="Vendor">CPUID vendor string, for example "AuthenticAMD"; "unknown" off x86.</param>
/// <param name="MissingExtensions">Required extensions this CPU lacks, in <see cref="X86CpuInfo.RequiredExtensions"/> order.</param>
public sealed record CpuFacts(string Vendor, IReadOnlyList<string> MissingExtensions)
{
    /// <summary>True for Intel CPUs, which need the relinker's --to-intel option.</summary>
    public bool IsIntel => Vendor == "GenuineIntel";
}

public interface ICpuInfo
{
    CpuFacts Read();
}

/// <summary>Reads the CPU through .NET intrinsics and CPUID.</summary>
public sealed class X86CpuInfo : ICpuInfo
{
    /// <summary>Extensions converted games use; a game stops at startup when one is missing.</summary>
    public static IReadOnlyList<string> RequiredExtensions { get; } = ["AVX2", "FMA", "F16C", "BMI1", "BMI2", "LZCNT", "MOVBE"];

    public CpuFacts Read() => new(ReadVendor(), MissingExtensions());

    private static string ReadVendor()
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

    private static List<string> MissingExtensions()
    {
        if (!X86Base.IsSupported)
        {
            return [.. RequiredExtensions];
        }

        // .NET has no IsSupported for F16C and MOVBE: CPUID leaf 1, ECX bits 29 and 22.
        var (_, _, ecx, _) = X86Base.CpuId(1, 0);
        var present = new Dictionary<string, bool>
        {
            ["AVX2"] = Avx2.IsSupported,
            ["FMA"] = Fma.IsSupported,
            ["F16C"] = (ecx & (1 << 29)) != 0,
            ["BMI1"] = Bmi1.IsSupported,
            ["BMI2"] = Bmi2.IsSupported,
            ["LZCNT"] = Lzcnt.IsSupported,
            ["MOVBE"] = (ecx & (1 << 22)) != 0,
        };
        return [.. RequiredExtensions.Where(extension => !present[extension])];
    }
}
