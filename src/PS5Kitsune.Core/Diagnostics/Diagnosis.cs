namespace PS5Kitsune.Core.Diagnostics;

/// <summary>The known error found in a launch's log.</summary>
/// <param name="Id">One of <see cref="DiagnosisIds"/>.</param>
/// <param name="Captures">The named groups of the matched regexes, then the rule's fixed captures: "module", "variable".</param>
/// <param name="Line">The first matched line, trimmed, at most 300 characters; null when only the exit code matched.</param>
/// <param name="LineNumber">1-based; null when <paramref name="Line"/> is.</param>
/// <param name="ExitCode">Null when nobody saw the exit.</param>
public sealed record Diagnosis(string Id, IReadOnlyDictionary<string, string> Captures, string? Line, int? LineNumber, uint? ExitCode, string LogPath)
{
    public string? Capture(string name) => Captures.TryGetValue(name, out var value) ? value : null;
}
