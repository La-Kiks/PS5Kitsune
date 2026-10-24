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

    /// <summary>When Reconvert last rewrote the conversion; null for a game never reconverted. Optional, so schema 1 still reads.</summary>
    public DateTimeOffset? ReconvertedAt { get; init; }

    public DateTimeOffset? LastPlayedAt { get; init; }
}

/// <param name="Mode">The install mode that converted the game: "release" or "build".</param>
/// <param name="Version">The release tag, or the first 7 characters of the commit for a build.</param>
/// <param name="Commit">The commit built; null for a release.</param>
/// <param name="ToIntel">Whether the relinker ran with its Intel option; Reconvert reuses it.</param>
public sealed record ConversionInfo(string Profile, string Mode, string Version, string? Commit, bool ToIntel);

public sealed record AuditRecord(int Implemented, int Stub, int Absent, AuditVerdict Verdict);

/// <summary>Per-game settings (milestone 7 edits them); defaults until then.</summary>
public sealed record GameSettings
{
    public string? Gpu { get; init; }

    public bool Lavapipe { get; init; }

    public int Language { get; init; } = 1;

    public string PresentMode { get; init; } = "fifo";
}
