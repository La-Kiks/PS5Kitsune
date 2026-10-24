namespace PS5Kitsune.Core.Games;

/// <summary>
/// Held for a whole Reconvert: .ps5kitsune/reconvert.lock opened with no sharing. Reading the game elsewhere does not restore it while
/// the lock is held, and a second Reconvert is refused. The file is deleted on close; a leftover file with no open handle is not held.
/// </summary>
public static class ReconvertLock
{
    /// <summary>Takes the lock; dispose the result to release it.</summary>
    /// <returns>Null when the lock is held elsewhere.</returns>
    public static IDisposable? TryAcquire(GameLayout game)
    {
        ArgumentNullException.ThrowIfNull(game);
        Directory.CreateDirectory(game.Meta);
        try
        {
            return new FileStream(game.ReconvertLock, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>True while a Reconvert of the game is running, in this process or another.</summary>
    public static bool IsHeld(GameLayout game)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (!File.Exists(game.ReconvertLock))
        {
            return false;
        }

        using var probe = TryAcquire(game);
        return probe is null;
    }
}
