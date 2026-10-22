using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.AnyPS5;

/// <summary>Installs AnyPS5 into anyps5/current, in release or build mode. The app's screens use it through this interface.</summary>
public interface IAnyPS5Installer
{
    /// <summary><see cref="RequestedMode"/>, else the mode of install.json, else <see cref="AnyPS5Mode.Release"/>.</summary>
    AnyPS5Mode CurrentMode { get; }

    /// <summary>
    /// The mode of an install that started and did not finish (anyps5\state\requested-mode); null when there is none,
    /// or when the file is unreadable or holds an unknown value. Never throws.
    /// </summary>
    AnyPS5Mode? RequestedMode { get; }

    /// <summary>True when the active profile can install <paramref name="mode"/>; build needs a build section and the build tools.</summary>
    bool Supports(AnyPS5Mode mode);

    /// <summary>install.json as found; null when missing or unreadable.</summary>
    AnyPS5InstallRecord? ReadRecord();

    /// <summary>The install steps of <paramref name="mode"/>, in order; their names are shown before the install starts.</summary>
    /// <exception cref="InvalidOperationException">The active profile does not support <paramref name="mode"/>.</exception>
    IReadOnlyList<IStep> CreateSteps(AnyPS5Mode mode);

    /// <summary>
    /// Where the install of <paramref name="mode"/> stands, from the steps' done-checks and install.json;
    /// Incomplete when the active profile does not support <paramref name="mode"/>.
    /// </summary>
    AnyPS5InstallState GetState(AnyPS5Mode mode);

    /// <summary>
    /// Runs the steps of <see cref="CreateSteps"/>, the first of which empties anyps5/current when install.json records
    /// the other mode. Remembers <paramref name="mode"/> as <see cref="RequestedMode"/> until every step succeeded.
    /// </summary>
    /// <exception cref="InvalidOperationException">The active profile does not support <paramref name="mode"/>.</exception>
    /// <exception cref="StepFailedException">A step failed.</exception>
    /// <exception cref="OperationCanceledException">The install was cancelled; it can be resumed.</exception>
    Task InstallAsync(AnyPS5Mode mode, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default);
}
