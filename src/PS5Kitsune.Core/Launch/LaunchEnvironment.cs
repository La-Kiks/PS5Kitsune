using System.Globalization;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Launch;

/// <summary>The environment variables a game's settings become, added to the game process only. See docs/architecture.md, "Per-game settings".</summary>
public static class LaunchEnvironment
{
    public const string Gpu = "ANYPS5_GPU";

    public const string DriverFiles = "VK_DRIVER_FILES";

    public const string Language = "ANYPS5_LANGUAGE";

    public const string PresentMode = "ANYPS5_PRESENT_MODE";

    public const string DisableDrivers = "VK_LOADER_DRIVERS_DISABLE";

    public const string Overlay = "ANYPS5_OVERLAY";

    public const string OverlayPosition = "ANYPS5_OVERLAY_POS";

    public const string PlayGoUnlistedChunks = "APS5_PLAYGO_UNLISTED_CHUNKS";

    public const string SystemFonts = "ANYPS5_SYSTEM_FONTS";

    public const string OutputResolution = "APS5_OUTPUT_RESOLUTION";

    /// <summary>The name AnyPS5 matches against Lavapipe's device.</summary>
    public const string LavapipeGpu = "llvmpipe";

    public const string DozenPattern = "*dzn*";

    /// <summary>The <c>&lt;width&gt;x&lt;height&gt;</c> AnyPS5 reads for a resolution choice; null for null or an unknown choice.</summary>
    public static string? ResolutionValue(string? resolution) => resolution switch
    {
        "1080p" => "1920x1080",
        "2160p" => "3840x2160",
        _ => null,
    };

    /// <summary>Every variable <see cref="For"/> can set: an extra variable may not use one.</summary>
    public static IReadOnlySet<string> ReservedNames { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        Gpu, DriverFiles, Language, PresentMode, DisableDrivers, Overlay, OverlayPosition, PlayGoUnlistedChunks, SystemFonts, OutputResolution,
    };

    /// <summary>A value equal to AnyPS5's default is left out, so a game with default settings and no fonts gets nothing.</summary>
    /// <param name="lavapipeManifest">Mesa's Lavapipe manifest; required when <see cref="GameSettings.Lavapipe"/> is on.</param>
    /// <param name="fontsDirectory">The installed Noto fonts; null when they are not installed.</param>
    /// <exception cref="ArgumentException">Lavapipe is on and <paramref name="lavapipeManifest"/> is null.</exception>
    public static IReadOnlyDictionary<string, string> For(GameSettings settings, string? lavapipeManifest, string? fontsDirectory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        if (settings.Lavapipe)
        {
            variables[DriverFiles] = lavapipeManifest ?? throw new ArgumentException("Lavapipe is on: its manifest is required.", nameof(lavapipeManifest));
            variables[Gpu] = LavapipeGpu;
        }
        else
        {
            if (settings.Gpu is { } gpu)
            {
                variables[Gpu] = gpu;
            }

            if (settings.DisableDozen)
            {
                variables[DisableDrivers] = DozenPattern;
            }
        }

        if (settings.Language != GameLanguages.Default)
        {
            variables[Language] = settings.Language.ToString(CultureInfo.InvariantCulture);
        }

        if (settings.PresentMode != GameSettingsValidator.DefaultPresentMode)
        {
            variables[PresentMode] = settings.PresentMode;
        }

        if (settings.Overlay)
        {
            variables[Overlay] = "1";
            if (settings.OverlayPosition != GameSettingsValidator.DefaultOverlayPosition)
            {
                variables[OverlayPosition] = settings.OverlayPosition;
            }
        }

        if (settings.PlayGoUnlistedChunks is { } chunks)
        {
            variables[PlayGoUnlistedChunks] = chunks;
        }

        if (ResolutionValue(settings.Resolution) is { } size)
        {
            variables[OutputResolution] = size;
        }

        if (fontsDirectory is not null)
        {
            variables[SystemFonts] = fontsDirectory;
        }

        // Last, and never over a setting's variable: a reserved name is refused on the page and dropped on read.
        foreach (var (name, value) in settings.ExtraEnvironment)
        {
            variables.TryAdd(name, value);
        }

        return variables;
    }
}
