namespace PS5Kitsune.Core.Tools;

/// <summary>The system has no Python 3. The message names the command that installs it and is shown as is.</summary>
public sealed class PythonMissingException(string message) : Exception(message);
