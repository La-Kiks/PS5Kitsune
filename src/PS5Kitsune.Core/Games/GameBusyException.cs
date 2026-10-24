namespace PS5Kitsune.Core.Games;

/// <summary>Another PS5Kitsune action (a Reconvert, a launch, a removal) holds the game's lock; nothing was changed.</summary>
public sealed class GameBusyException(string titleId) : Exception($"Another PS5Kitsune action is using {titleId}. Try again when it finishes.")
{
    public string TitleId { get; } = titleId;
}
