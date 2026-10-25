using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Diagnostics;

/// <param name="Label">As in "Reset game language": lower case, except names such as GPU.</param>
/// <param name="Reset">The settings with this one back to its default.</param>
public sealed record SettingVariable(string Variable, string Label, Func<GameSettings, GameSettings> Reset);

/// <summary>The game setting behind each environment variable AnyPS5 may refuse. See the 7c spec, "Fixes".</summary>
public static class SettingVariables
{
    private static readonly GameSettings Defaults = new();

    private static readonly SettingVariable[] All =
    [
        new("ANYPS5_GPU", "GPU", settings => settings with { Gpu = Defaults.Gpu }),
        new("ANYPS5_LANGUAGE", "game language", settings => settings with { Language = Defaults.Language }),
        new("ANYPS5_PRESENT_MODE", "vertical sync", settings => settings with { PresentMode = Defaults.PresentMode }),
        new("ANYPS5_OVERLAY", "performance overlay", settings => settings with { Overlay = Defaults.Overlay }),
        new("ANYPS5_OVERLAY_POS", "overlay position", settings => settings with { OverlayPosition = Defaults.OverlayPosition }),
        new("APS5_PLAYGO_UNLISTED_CHUNKS", "PlayGo unlisted chunks", settings => settings with { PlayGoUnlistedChunks = Defaults.PlayGoUnlistedChunks }),
    ];

    /// <summary>The setting behind <paramref name="variable"/>, compared ignoring case; null for a variable no game setting sets.</summary>
    public static SettingVariable? Find(string? variable) =>
        All.FirstOrDefault(item => string.Equals(item.Variable, variable, StringComparison.OrdinalIgnoreCase));
}
