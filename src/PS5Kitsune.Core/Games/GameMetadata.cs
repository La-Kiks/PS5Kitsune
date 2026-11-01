namespace PS5Kitsune.Core.Games;

/// <param name="TitleId">From param.json or param.sfo; also the name of the game folder.</param>
/// <param name="Name">Display name, resolved from localizedParameters or param.sfo's TITLE.</param>
/// <param name="IsPs4">A PS4 game: read from param.sfo, or a CUSA title ID.</param>
public sealed record GameMetadata(string TitleId, string Name, bool IsPs4 = false);
