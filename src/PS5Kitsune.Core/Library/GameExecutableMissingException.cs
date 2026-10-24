namespace PS5Kitsune.Core.Library;

/// <summary>The converted executable is gone, usually deleted by an antivirus; a Reconvert writes it again.</summary>
public sealed class GameExecutableMissingException(string executable)
    : Exception($"{executable} is missing; an antivirus may have deleted it. Add an exclusion for the games folder, then reconvert the game.")
{
    public string Executable { get; } = executable;
}
