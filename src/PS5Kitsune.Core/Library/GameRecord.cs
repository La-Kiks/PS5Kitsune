using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Core.Library;

/// <summary>Contents of &lt;game&gt;/.ps5kitsune/game.json. See docs/architecture.md, "Game record".</summary>
public sealed record GameRecord
{
    public const int CurrentSchemaVersion = 1;

    public required int SchemaVersion { get; init; }

    public required string TitleId { get; init; }

    public required string Name { get; init; }

    /// <summary>Converted executable, relative to the game folder.</summary>
    public required string Executable { get; init; }

    /// <summary>icon0.png relative to the game folder, '/' separators; null when the game has none.</summary>
    public string? Icon { get; init; }

    public required ConversionInfo ConvertedWith { get; init; }

    public required AuditRecord Audit { get; init; }

    public GameSettings Settings { get; init; } = new();

    public required DateTimeOffset AddedAt { get; init; }

    public DateTimeOffset? LastPlayedAt { get; init; }

    /// <summary>When Reconvert last rewrote the conversion; null for a game never reconverted. Optional, so schema 1 still reads.</summary>
    public DateTimeOffset? ReconvertedAt { get; init; }
}

/// <param name="Mode">The install mode that converted the game: "release" or "build".</param>
/// <param name="Version">The release tag, or the first 7 characters of the commit for a build.</param>
/// <param name="Commit">The commit built; null for a release.</param>
/// <param name="ToIntel">Whether the relinker ran with its Intel option; Reconvert reuses it.</param>
public sealed record ConversionInfo(string Profile, string Mode, string Version, string? Commit, bool ToIntel);

public sealed record AuditRecord(int Implemented, int Stub, int Absent, AuditVerdict Verdict);

/// <summary>Per-game settings, passed to the game as environment variables at launch. See docs/architecture.md, "Per-game settings".</summary>
public sealed record GameSettings
{
    /// <summary>Part of a GPU name (ANYPS5_GPU); null lets AnyPS5 choose.</summary>
    public string? Gpu { get; init; }

    /// <summary>CPU rendering with Mesa's Lavapipe; overrides <see cref="Gpu"/> and <see cref="DisableDozen"/>.</summary>
    public bool Lavapipe { get; init; }

    /// <summary>Console language number, 0 to 30 (<see cref="GameLanguages"/>).</summary>
    public int Language { get; init; } = GameLanguages.Default;

    /// <summary>One of <see cref="GameSettingsValidator.PresentModes"/>.</summary>
    public string PresentMode { get; init; } = GameSettingsValidator.DefaultPresentMode;

    /// <summary>Hides Mesa's Dozen driver from the Vulkan loader.</summary>
    public bool DisableDozen { get; init; }

    /// <summary>Shows the performance overlay from the first frame.</summary>
    public bool Overlay { get; init; }

    /// <summary>One of <see cref="GameSettingsValidator.OverlayPositions"/>.</summary>
    public string OverlayPosition { get; init; } = GameSettingsValidator.DefaultOverlayPosition;

    /// <summary>"installed" or "absent" for dumps without PlayGo files; null keeps AnyPS5's error.</summary>
    public string? PlayGoUnlistedChunks { get; init; }
}
