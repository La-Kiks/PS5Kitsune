using System.Text.Json;
using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Core.AnyPS5;

/// <summary>
/// Moves the single install folder of milestones 4 to 5b, anyps5\current, into anyps5\installs\&lt;mode&gt; and makes it
/// active. Idempotent: nothing happens without current or when the target exists.
/// </summary>
public static class AnyPS5Migration
{
    /// <exception cref="AnyPS5MigrationException">The move failed, for example on a file held by a running game.</exception>
    public static void Run(AnyPS5Paths paths)
    {
        var current = paths.LegacyCurrent;
        if (!Directory.Exists(current))
        {
            return;
        }

        var mode = RecordMode(current) ?? paths.RequestedMode ?? AnyPS5Mode.Release;
        var target = paths.For(mode).RootDirectory;
        if (Directory.Exists(target))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            Directory.Move(current, target);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new AnyPS5MigrationException(exception);
        }

        if (!File.Exists(paths.ActiveModeFile))
        {
            paths.WriteActiveMode(mode);
        }
    }

    private static AnyPS5Mode? RecordMode(string current)
    {
        try
        {
            return JsonFiles.Read<AnyPS5InstallRecord>(Path.Combine(current, "install.json"))?.Mode;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
