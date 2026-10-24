using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Games;

/// <param name="Audit">The new audit's counts and unresolved functions.</param>
/// <param name="PreviousAudit">The counts game.json held before, to show what changed.</param>
public sealed record GameReconverted(GameRecord Record, string GameDirectory, AuditSummary Audit, AuditRecord PreviousAudit);
