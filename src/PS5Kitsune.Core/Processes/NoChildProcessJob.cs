using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Processes;

/// <summary>Where the OS offers no job object yet (Linux, milestone 8): tools are not tied to PS5Kitsune.</summary>
public sealed class NoChildProcessJob : IChildProcessJob
{
    public void Assign(int processId)
    {
    }
}
