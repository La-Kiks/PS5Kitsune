namespace PS5Kitsune.Core.Abstractions;

/// <summary>
/// Ties the tools PS5Kitsune starts (git, CMake, Ninja, Python, the relinker) to its lifetime: when PS5Kitsune ends, even killed from the
/// Task Manager, they end too. Games are never assigned: they outlive PS5Kitsune.
/// </summary>
public interface IChildProcessJob
{
    /// <summary>Adds a started tool; the processes it starts afterwards follow it. Never throws: a tool that already exited is skipped.</summary>
    void Assign(int processId);
}
