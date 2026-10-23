namespace PS5Kitsune.Core.Games;

/// <param name="Root">Folder holding eboot.bin.</param>
/// <param name="StagingDirectory">Folder a zip was extracted to; null when the input was a folder.</param>
public sealed record GameSource(string Root, string? StagingDirectory);

public sealed class InvalidGameSourceException(string message, Exception? innerException = null) : Exception(message, innerException);
