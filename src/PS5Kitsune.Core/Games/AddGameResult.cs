using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Games;

public abstract record AddGameResult;

/// <param name="Warnings">Non-blocking validation issues, to show the user.</param>
public sealed record GameAdded(GameRecord Record, string GameDirectory, IReadOnlyList<ValidationIssue> Warnings) : AddGameResult;

/// <summary>Validation found blocking issues; nothing was copied.</summary>
public sealed record GameRejected(IReadOnlyList<ValidationIssue> Issues) : AddGameResult;

/// <summary>game.json already exists; delete the game folder to add the game again.</summary>
public sealed record GameAlreadyAdded(string TitleId, string GameDirectory) : AddGameResult;
