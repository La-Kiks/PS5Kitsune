namespace PS5Kitsune.Core.Games;

/// <summary>Another Reconvert of the same game holds its lock.</summary>
public sealed class ReconvertInProgressException(string titleId) : Exception($"A Reconvert of {titleId} is already in progress.")
{
    public string TitleId { get; } = titleId;
}
