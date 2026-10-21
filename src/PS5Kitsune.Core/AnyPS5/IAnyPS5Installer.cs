using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.AnyPS5;

/// <summary>Installs AnyPS5 into anyps5/current. The app's screens use it through this interface.</summary>
public interface IAnyPS5Installer
{
    /// <summary>The install steps, in order; their names are shown before the install starts.</summary>
    IReadOnlyList<IStep> CreateSteps();

    /// <summary>Where the install stands, from the steps' done-checks and install.json.</summary>
    AnyPS5InstallState GetState();

    /// <exception cref="StepFailedException">A step failed.</exception>
    /// <exception cref="OperationCanceledException">The install was cancelled; it can be resumed.</exception>
    Task InstallAsync(IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default);
}
