namespace PS5Kitsune.Core.Games;

public enum IssueSeverity
{
    Warning,
    Blocking,
}

public sealed record ValidationIssue(IssueSeverity Severity, string Message);

/// <param name="Metadata">Null when neither param.json nor param.sfo can be read.</param>
/// <param name="ModuleDirectories">Module folders present in the game root, among <see cref="GameValidator.ModuleDirectoryNames"/>.</param>
public sealed record GameValidation(GameMetadata? Metadata, IReadOnlyList<string> ModuleDirectories, long SizeBytes, IReadOnlyList<ValidationIssue> Issues)
{
    public bool IsBlocked => Issues.Any(issue => issue.Severity == IssueSeverity.Blocking);

    public IReadOnlyList<ValidationIssue> Warnings => [.. Issues.Where(issue => issue.Severity == IssueSeverity.Warning)];
}
