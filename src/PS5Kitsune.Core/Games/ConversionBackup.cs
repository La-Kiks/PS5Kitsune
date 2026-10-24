namespace PS5Kitsune.Core.Games;

/// <summary>
/// Sets a game's conversion outputs aside during a Reconvert and moves them back when it does not finish. One rule per folder:
/// .ps5kitsune/previous.partial means the move was interrupted (nothing new was built); .ps5kitsune/previous means the new
/// conversion did not finish. See docs/superpowers/specs/2026-10-23-milestone-6b-reconvert-design.md, "Recovery".
/// </summary>
public static class ConversionBackup
{
    /// <summary>The conversion outputs now in the game folder, relative to it: files, and libs as one folder.</summary>
    public static IReadOnlyList<string> Outputs(GameLayout game)
    {
        ArgumentNullException.ThrowIfNull(game);
        var outputs = new List<string>();
        foreach (var file in new[] { game.Executable, game.RelinkedMarker, game.AuditFile })
        {
            if (File.Exists(file))
            {
                outputs.Add(Path.GetRelativePath(game.Root, file));
            }
        }

        if (Directory.Exists(game.Root))
        {
            outputs.AddRange(Directory.EnumerateFiles(game.Root, "*.registry.json").Order(StringComparer.Ordinal).Select(file => Path.GetFileName(file)));
            outputs.AddRange(Directory.EnumerateFiles(game.Root, "*.needed.json").Order(StringComparer.Ordinal).Select(file => Path.GetFileName(file)));
        }

        if (Directory.Exists(game.Libs))
        {
            outputs.Add(Path.GetRelativePath(game.Root, game.Libs));
        }

        if (Directory.Exists(game.App0))
        {
            outputs.AddRange(Directory.EnumerateFiles(game.App0, "*.guest.prx", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(game.Root, file))
                .Order(StringComparer.Ordinal));
        }

        return outputs;
    }

    /// <summary>Moves the outputs into previous.partial, then renames it to previous in one rename.</summary>
    public static void SetAside(GameLayout game)
    {
        ArgumentNullException.ThrowIfNull(game);
        Directory.CreateDirectory(game.PreviousPartial);
        foreach (var relative in Outputs(game))
        {
            Move(Path.Combine(game.Root, relative), Path.Combine(game.PreviousPartial, relative));
        }

        Directory.Move(game.PreviousPartial, game.Previous);
    }

    /// <summary>Puts an unfinished Reconvert's set-aside conversion back.</summary>
    /// <returns>False when there was nothing to restore.</returns>
    public static bool Restore(GameLayout game)
    {
        ArgumentNullException.ThrowIfNull(game);
        TryDelete(game.PreviousDiscarded);
        if (Directory.Exists(game.Previous))
        {
            foreach (var relative in Outputs(game))
            {
                Delete(Path.Combine(game.Root, relative));
            }

            MoveBack(game.Previous, game);
            return true;
        }

        if (Directory.Exists(game.PreviousPartial))
        {
            MoveBack(game.PreviousPartial, game);
            return true;
        }

        return false;
    }

    /// <summary>Drops the set-aside conversion once the new one is complete: a rename first, so it can never be restored.</summary>
    public static void Discard(GameLayout game)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (Directory.Exists(game.Previous))
        {
            Directory.Move(game.Previous, game.PreviousDiscarded);
        }

        TryDelete(game.PreviousDiscarded);
    }

    private static void MoveBack(string folder, GameLayout game)
    {
        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).ToList())
        {
            Move(file, Path.Combine(game.Root, Path.GetRelativePath(folder, file)));
        }

        Directory.Delete(folder, recursive: true);
    }

    private static void Move(string source, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (Directory.Exists(source))
        {
            Directory.Move(source, target);
        }
        else
        {
            File.Move(source, target, overwrite: true);
        }
    }

    private static void Delete(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
        else
        {
            File.Delete(path);
        }
    }

    // A leftover discarded folder only takes space: a failed delete is retried on the next restore.
    private static void TryDelete(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
