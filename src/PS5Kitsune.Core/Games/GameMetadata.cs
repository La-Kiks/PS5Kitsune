namespace PS5Kitsune.Core.Games;

/// <param name="TitleId">From param.json; also the name of the game folder.</param>
/// <param name="Name">Display name, resolved from localizedParameters.</param>
public sealed record GameMetadata(string TitleId, string Name);
