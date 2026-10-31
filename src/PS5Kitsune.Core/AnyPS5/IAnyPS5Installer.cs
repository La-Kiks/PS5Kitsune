using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.AnyPS5;

/// <summary>Installs AnyPS5 into anyps5\installs\&lt;mode&gt; and chooses the active install. The app's screens use it through this interface.</summary>
public interface IAnyPS5Installer
{
    /// <summary><see cref="RequestedMode"/>, else <see cref="ActiveMode"/>, else <see cref="AnyPS5Mode.Release"/>.</summary>
    AnyPS5Mode CurrentMode { get; }

    /// <summary>
    /// The mode of an install that started and did not finish (anyps5\state\requested-mode); null when there is none,
    /// or when the file is unreadable or holds an unknown value. Never throws.
    /// </summary>
    AnyPS5Mode? RequestedMode { get; }

    /// <summary>True when the active profile can install <paramref name="mode"/>; build needs a build section and the build tools.</summary>
    bool Supports(AnyPS5Mode mode);

    /// <summary>Why build mode cannot be installed here, for the Mode page's build card.</summary>
    string BuildUnavailableReason { get; }

    /// <summary>The install in use (anyps5\state\active); null when there is none, or the file is unreadable or unknown. Never throws.</summary>
    AnyPS5Mode? ActiveMode { get; }

    /// <summary>The active install's install.json; null with no active install, or when missing or unreadable.</summary>
    AnyPS5InstallRecord? ReadRecord();

    /// <summary>anyps5\installs\&lt;mode&gt;\install.json as found; null when missing or unreadable.</summary>
    AnyPS5InstallRecord? ReadRecord(AnyPS5Mode mode);

    /// <summary>Makes <paramref name="mode"/>'s finished install the active one, with no step and no done-check.</summary>
    /// <exception cref="InvalidOperationException"><paramref name="mode"/> has no install.json (its install never finished), or an install of it started and did not finish (<see cref="RequestedMode"/> is <paramref name="mode"/>).</exception>
    void Use(AnyPS5Mode mode);

    /// <summary>The install steps of <paramref name="mode"/>, in order; their names are shown before the install starts.</summary>
    /// <exception cref="InvalidOperationException">The active profile does not support <paramref name="mode"/>.</exception>
    IReadOnlyList<IStep> CreateSteps(AnyPS5Mode mode);

    /// <summary>
    /// Where the install of <paramref name="mode"/> stands in its own folder, whatever is active, from the steps' done-checks
    /// and its install.json; Incomplete when the active profile does not support <paramref name="mode"/>.
    /// </summary>
    AnyPS5InstallState GetState(AnyPS5Mode mode);

    /// <summary>
    /// Runs the steps of <see cref="CreateSteps"/> into anyps5\installs\&lt;mode&gt;, then makes <paramref name="mode"/> the
    /// active install. A failure or a cancel leaves the active install as it was. Remembers <paramref name="mode"/> as
    /// <see cref="RequestedMode"/> until every step succeeded.
    /// </summary>
    /// <exception cref="InvalidOperationException">The active profile does not support <paramref name="mode"/>.</exception>
    /// <exception cref="StepFailedException">A step failed.</exception>
    /// <exception cref="OperationCanceledException">The install was cancelled; it can be resumed.</exception>
    Task InstallAsync(AnyPS5Mode mode, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default);

    /// <summary>Moves anyps5\current, from before milestone 5c, into anyps5\installs and makes it active; does nothing otherwise.</summary>
    /// <exception cref="AnyPS5MigrationException">The move failed; nothing was changed.</exception>
    void MigrateLegacyInstall();
}
