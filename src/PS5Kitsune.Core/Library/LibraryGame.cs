using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Core.Library;

/// <summary>An added game as the library shows it.</summary>
/// <param name="Running">Its running.json, checked against the live process; null when it is not running.</param>
public sealed record LibraryGame(GameLayout Game, GameRecord Record, RunningGame? Running);
