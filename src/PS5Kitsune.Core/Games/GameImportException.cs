namespace PS5Kitsune.Core.Games;

/// <summary>An add step failed for a reason the user can act on; the message says what happened.</summary>
public sealed class GameImportException(string message) : Exception(message);
