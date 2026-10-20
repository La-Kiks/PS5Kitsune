namespace PS5Kitsune.Core.Games;

/// <summary>What the add steps share: the source, the validated metadata, the game folder and the results.</summary>
public sealed class GameImportContext(GameSource source, GameMetadata metadata, IReadOnlyList<string> moduleDirectories, GameLayout game, bool toIntel)
{
    public GameSource Source { get; } = source;

    public GameMetadata Metadata { get; } = metadata;

    /// <summary>Module folders of the game (sce_module, sce_modules, prx), relative to app0.</summary>
    public IReadOnlyList<string> ModuleDirectories { get; } = moduleDirectories;

    public GameLayout Game { get; } = game;

    /// <summary>Whether the relinker gets its Intel option.</summary>
    public bool ToIntel { get; } = toIntel;

    /// <summary>Set by the audit step; the game record is written from it.</summary>
    public AuditSummary? Audit { get; set; }
}
