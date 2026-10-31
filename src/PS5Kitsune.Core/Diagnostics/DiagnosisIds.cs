namespace PS5Kitsune.Core.Diagnostics;

/// <summary>The rule ids the app explains. See the 7c spec, "Game page".</summary>
public static class DiagnosisIds
{
    public const string NvidiaShaderCompiler = "nvidia-shader-compiler";

    public const string LibsNotDll = "libs-not-dll";

    public const string LibsMissingDll = "libs-missing-dll";

    public const string CommitLimit = "commit-limit";

    public const string GpuFeatures = "gpu-features";

    public const string CpuTooOld = "cpu-too-old";

    public const string ParamJson = "param-json";

    public const string InvalidSetting = "invalid-setting";

    public const string NotImplemented = "not-implemented";

    /// <summary>No Vulkan loader or driver could be loaded (Linux).</summary>
    public const string VulkanMissing = "vulkan-missing";

    /// <summary>The dynamic loader could not find a system library (Linux).</summary>
    public const string SharedLibraryMissing = "shared-library-missing";

    /// <summary>GPU stores to imported memory failed (Linux).</summary>
    public const string BdaAccess = "bda-access";

    /// <summary>/dev/udmabuf could not be opened (Linux).</summary>
    public const string UdmabufAccess = "udmabuf-access";

    /// <summary>A non-zero exit that no rule explains; never a profile rule.</summary>
    public const string Unexplained = "unexplained";

    /// <summary>The ids a profile rule may use.</summary>
    public static readonly IReadOnlySet<string> Known = new HashSet<string>(StringComparer.Ordinal)
    {
        NvidiaShaderCompiler, LibsNotDll, LibsMissingDll, CommitLimit, GpuFeatures, CpuTooOld, ParamJson, InvalidSetting, NotImplemented,
        VulkanMissing, SharedLibraryMissing, BdaAccess, UdmabufAccess,
    };
}
