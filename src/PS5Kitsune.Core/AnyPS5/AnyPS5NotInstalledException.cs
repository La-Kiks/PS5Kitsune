namespace PS5Kitsune.Core.AnyPS5;

public sealed class AnyPS5NotInstalledException()
    : Exception("AnyPS5 is not installed. Run 'ps5kitsune install' first.");
