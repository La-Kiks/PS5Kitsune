namespace PS5Kitsune.Core.Settings;

public static class DataRoots
{
    /// <summary>The command line's root, else the root saved by the app, else the platform's default.</summary>
    public static string Resolve(string? cliRoot, AppSettings settings, string defaultRoot)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return cliRoot ?? settings.DataRoot ?? defaultRoot;
    }

    /// <summary>Creates the folder if needed and checks a file can be written in it.</summary>
    /// <returns>Null when the folder can be used, else a message for the user.</returns>
    public static string? Prepare(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            Directory.CreateDirectory(full);
            var probe = Path.Combine(full, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return $"Cannot use {path}: {exception.Message}";
        }
    }
}
