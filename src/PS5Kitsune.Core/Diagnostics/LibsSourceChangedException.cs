namespace PS5Kitsune.Core.Diagnostics;

/// <summary>The install that converted a game is missing or has another version, so its libs may not match the conversion.</summary>
public sealed class LibsSourceChangedException(string mode, string name)
    : Exception($"The {mode} install has changed since {name} was converted. Reconvert it instead.");
