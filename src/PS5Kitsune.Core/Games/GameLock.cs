namespace PS5Kitsune.Core.Games;

/// <summary>
/// .ps5kitsune/game.lock opened with no sharing and deleted on close. Held by every action that changes the game folder or needs it unchanged:
/// a Reconvert (whole run), a reader restoring an interrupted Reconvert, a launch (until running.json is written), Remove and Clear shader cache.
/// The OS releases it when its process dies; a leftover file with no open handle is not held.
/// </summary>
public static class GameLock
{
    private const int Attempts = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(50);

    /// <summary>Takes the lock, trying again for about 250 ms: a short hold elsewhere (a reader's restore, a launch) must not refuse an action.</summary>
    /// <returns>Null when the lock stayed held; dispose the result to release it.</returns>
    public static IDisposable? TryAcquire(GameLayout game) => TryAcquire(game, Attempts, RetryDelay);

    internal static IDisposable? TryAcquire(GameLayout game, int attempts, TimeSpan delay)
    {
        for (var attempt = 1; ; attempt++)
        {
            if (TryAcquireNow(game) is { } held)
            {
                return held;
            }

            if (attempt >= attempts)
            {
                return null;
            }

            Thread.Sleep(delay);
        }
    }

    /// <summary>One attempt, for readers: they skip the restore rather than wait.</summary>
    /// <returns>Null when the lock is held elsewhere; dispose the result to release it.</returns>
    public static IDisposable? TryAcquireNow(GameLayout game)
    {
        ArgumentNullException.ThrowIfNull(game);
        Directory.CreateDirectory(game.Meta);
        try
        {
            return new FileStream(game.Lock, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
        }
        // A handle being closed with delete-on-close can make the open fail with access denied.
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>True while an action holds the lock, in this process or another.</summary>
    public static bool IsHeld(GameLayout game)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (!File.Exists(game.Lock))
        {
            return false;
        }

        using var probe = TryAcquireNow(game);
        return probe is null;
    }
}
