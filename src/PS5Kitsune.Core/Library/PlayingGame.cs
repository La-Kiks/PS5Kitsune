using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Core.Library;

/// <summary>A game launched or found running. Disposing <see cref="Process"/> releases the handle, never the game.</summary>
public sealed record PlayingGame(GameLayout Game, GameRecord Record, ILaunchedGame Process);
