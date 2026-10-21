namespace PS5Kitsune.Core.Checks;

/// <summary>Ordered from best to worst: the overall status of a report is the highest one.</summary>
public enum CheckStatus
{
    Ok,
    Info,
    Warning,
    Blocking,
}

/// <summary>A fix the host can offer as a button; the platform knows how to open it.</summary>
public enum CheckFix
{
    /// <summary>Windows "Performance Options", where the page file is set.</summary>
    OpenPerformanceSettings,
}

/// <param name="Id">Stable identifier of the check.</param>
/// <param name="Title">Short name of the check.</param>
/// <param name="Status">Severity of the finding.</param>
/// <param name="Summary">What was found, in one or two sentences.</param>
/// <param name="Action">What the user should do; null when nothing is needed.</param>
/// <param name="Fix">A button the host can offer; null when there is none.</param>
public sealed record CheckResult(string Id, string Title, CheckStatus Status, string Summary, string? Action = null, CheckFix? Fix = null);

/// <summary>One PC check: a pure function of the facts collected once for all checks.</summary>
public interface ISystemCheck
{
    string Id { get; }

    string Title { get; }

    CheckResult Evaluate(SystemFacts facts);
}

public abstract class SystemCheck : ISystemCheck
{
    public abstract string Id { get; }

    public abstract string Title { get; }

    public abstract CheckResult Evaluate(SystemFacts facts);

    protected CheckResult Result(CheckStatus status, string summary, string? action = null, CheckFix? fix = null) =>
        new(Id, Title, status, summary, action, fix);
}
