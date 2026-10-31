using PS5Kitsune.Core.Launch;

namespace PS5Kitsune.Core.Library;

/// <summary>The values AnyPS5 accepts for each setting. AnyPS5 stops on an invalid value, so a hand-edited game.json is normalized on read.</summary>
public static class GameSettingsValidator
{
    public const string DefaultPresentMode = "fifo";

    public const string DefaultOverlayPosition = "tl";

    public static IReadOnlyList<string> PresentModes { get; } = ["fifo", "fifo_latest_ready", "mailbox", "immediate"];

    public static IReadOnlyList<string> OverlayPositions { get; } = ["tl", "tr", "bl", "br"];

    public static IReadOnlyList<string> PlayGoValues { get; } = ["installed", "absent"];

    /// <summary>The video-out modes AnyPS5 v0.1.4 has; a future mode is one more entry here and in <see cref="Launch.LaunchEnvironment.ResolutionValue"/>.</summary>
    public static IReadOnlyList<string> Resolutions { get; } = ["1080p", "2160p"];

    /// <summary>Replaces each value AnyPS5 would refuse with its default; a blank GPU name becomes null.</summary>
    /// <returns>The settings to use, and the JSON names of the fields replaced, in declaration order.</returns>
    public static (GameSettings Settings, IReadOnlyList<string> Replaced) Normalize(GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var replaced = new List<string>();
        var normalized = settings;
        if (settings.Gpu is { } gpu && string.IsNullOrWhiteSpace(gpu))
        {
            normalized = normalized with { Gpu = null };
            replaced.Add("gpu");
        }

        if (!GameLanguages.All.Any(language => language.Number == settings.Language))
        {
            normalized = normalized with { Language = GameLanguages.Default };
            replaced.Add("language");
        }

        if (!PresentModes.Contains(settings.PresentMode))
        {
            normalized = normalized with { PresentMode = DefaultPresentMode };
            replaced.Add("presentMode");
        }

        if (!OverlayPositions.Contains(settings.OverlayPosition))
        {
            normalized = normalized with { OverlayPosition = DefaultOverlayPosition };
            replaced.Add("overlayPosition");
        }

        if (settings.PlayGoUnlistedChunks is { } chunks && !PlayGoValues.Contains(chunks))
        {
            normalized = normalized with { PlayGoUnlistedChunks = null };
            replaced.Add("playGoUnlistedChunks");
        }

        if (settings.Resolution is { } resolution && !Resolutions.Contains(resolution))
        {
            normalized = normalized with { Resolution = null };
            replaced.Add("resolution");
        }

        var kept = settings.ExtraEnvironment
            .Where(variable => variable.Value is not null && variable.Value.AsSpan().IndexOfAny('\r', '\n', '\0') < 0 && ExtraVariables.Problem(variable.Key) is null)
            .ToDictionary(StringComparer.Ordinal);
        if (kept.Count != settings.ExtraEnvironment.Count)
        {
            normalized = normalized with { ExtraEnvironment = kept };
            replaced.Add("extraEnvironment");
        }

        return (normalized, replaced);
    }
}
