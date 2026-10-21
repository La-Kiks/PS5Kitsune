using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.AnyPS5;

/// <summary>Installs AnyPS5 into anyps5/current, in release or build mode. The app's screens use it through this interface.</summary>
public interface IAnyPS5Installer
{
    /// <summary>The mode of install.json, else <see cref="AnyPS5Mode.Release"/>.</summary>
    AnyPS5Mode CurrentMode { get; }

    /// <summary>install.json as found; null when missing or unreadable.</summary>
    AnyPS5InstallRecord? ReadRecord();

    /// <summary>The install steps of <paramref name="mode"/>, in order; their names are shown before the install starts.</summary>
    /// <exception cref="InvalidOperationException">The active profile does not support <paramref name="mode"/>.</exception>
    IReadOnlyList<IStep> CreateSteps(AnyPS5Mode mode);

    /// <summary>Where the install of <paramref name="mode"/> stands, from the steps' done-checks and install.json.</summary>
    AnyPS5InstallState GetState(AnyPS5Mode mode);

    /// <summary>Empties anyps5/current first when install.json records the other mode.</summary>
    /// <exception cref="StepFailedException">A step failed.</exception>
    /// <exception cref="OperationCanceledException">The install was cancelled; it can be resumed.</exception>
    Task InstallAsync(AnyPS5Mode mode, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default);
}
