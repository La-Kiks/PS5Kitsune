using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Library;

/// <summary>Stands in where the OS has no launcher yet (Linux, milestone 8): nothing can start, so nothing is running.</summary>
public sealed class UnsupportedGameLauncher : IGameLauncher
{
    public ILaunchedGame Launch(GameLaunchRequest request) => throw new PlatformNotSupportedException("Launching games is not supported on this OS yet.");

    public ILaunchedGame? Attach(RunningGame record) => null;
}
