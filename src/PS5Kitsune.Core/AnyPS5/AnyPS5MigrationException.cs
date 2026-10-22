namespace PS5Kitsune.Core.AnyPS5;

/// <summary>anyps5\current could not be moved into anyps5\installs; nothing was changed.</summary>
public sealed class AnyPS5MigrationException(Exception inner)
    : Exception($"Cannot move anyps5\\current: {inner.Message.TrimEnd('.')}. Close any game using it and retry.", inner);
