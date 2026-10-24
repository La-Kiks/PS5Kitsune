using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Library;

/// <summary>The game is running: PS5Kitsune launched it and its process is alive. Nothing was changed.</summary>
public sealed class GameRunningException(string name, RunningGame running)
    : Exception($"{name} is already running (PID {running.ProcessId}). Log: {running.LogFile}.")
{
    public string Name { get; } = name;

    public RunningGame Running { get; } = running;
}
