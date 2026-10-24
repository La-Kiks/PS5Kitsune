using System.Globalization;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Launch;

/// <summary>What `ps5kitsune settings` prints: each setting, then the variables a launch passes, sorted by name.</summary>
public static class GameSettingsReport
{
    private const string FirstLaunch = " (downloaded on first launch)";

    /// <param name="lavapipeManifest">Where Mesa's manifest is or will be; null when the profile has no Mesa download.</param>
    /// <param name="fontsDirectory">Where the fonts are or will be; null when the profile has no fonts.</param>
    public static IReadOnlyList<string> Lines(GameSettings settings, string? lavapipeManifest, bool lavapipeInstalled, string? fontsDirectory, bool fontsInstalled)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var defaults = new GameSettings();
        List<string> lines =
        [
            Line("GPU", settings.Gpu ?? "automatic", settings.Gpu == defaults.Gpu),
            Line("CPU rendering (Lavapipe)", OnOff(settings.Lavapipe), settings.Lavapipe == defaults.Lavapipe),
            Line(
                "Game language",
                string.Create(CultureInfo.InvariantCulture, $"{settings.Language}, {GameLanguages.Name(settings.Language)}"),
                settings.Language == defaults.Language),
            Line("Vertical sync", settings.PresentMode, settings.PresentMode == defaults.PresentMode),
            Line("Disable Dozen", OnOff(settings.DisableDozen), settings.DisableDozen == defaults.DisableDozen),
            Line("Performance overlay", OnOff(settings.Overlay), settings.Overlay == defaults.Overlay),
            Line("Overlay position", settings.OverlayPosition, settings.OverlayPosition == defaults.OverlayPosition),
            Line("PlayGo unlisted chunks", settings.PlayGoUnlistedChunks ?? "unset", settings.PlayGoUnlistedChunks == defaults.PlayGoUnlistedChunks),
            "Environment at launch:",
        ];

        var lavapipeMissing = settings.Lavapipe && lavapipeManifest is null;
        var effective = lavapipeMissing ? settings with { Lavapipe = false, Gpu = null, DisableDozen = false } : settings;
        var variables = LaunchEnvironment.For(effective, lavapipeManifest, fontsDirectory);
        if (variables.Count == 0)
        {
            lines.Add("  (none)");
        }

        foreach (var (name, value) in variables.OrderBy(variable => variable.Key, StringComparer.Ordinal))
        {
            var pending = (name == LaunchEnvironment.DriverFiles && !lavapipeInstalled) || (name == LaunchEnvironment.SystemFonts && !fontsInstalled);
            lines.Add($"  {name}={value}{(pending ? FirstLaunch : "")}");
        }

        if (lavapipeMissing)
        {
            lines.Add("  Lavapipe is not available: this profile has no Mesa download.");
        }

        return lines;
    }

    private static string Line(string label, string value, bool isDefault) => $"{label}: {value}{(isDefault ? " (default)" : "")}";

    private static string OnOff(bool value) => value ? "on" : "off";
}
