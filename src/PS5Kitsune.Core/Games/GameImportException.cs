namespace PS5Kitsune.Core.Games;

/// <summary>
/// An add step failed for a reason the user can act on. <see cref="Summary"/> says what happened in one line;
/// <see cref="Details"/> holds the tool's output, shown on demand. The message joins them, as the CLI prints it.
/// </summary>
public sealed class GameImportException(string summary, string? details = null)
    : Exception(details is null ? summary : summary + Environment.NewLine + details)
{
    public string Summary { get; } = summary;

    public string? Details { get; } = details;
}
