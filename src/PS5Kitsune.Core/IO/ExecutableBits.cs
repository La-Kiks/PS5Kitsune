namespace PS5Kitsune.Core.IO;

/// <summary>
/// The execute bits a Linux program needs and a copy may lose (a browser download, a copy tool that drops modes).
/// Windows has no such bits: there every file counts as executable and nothing changes.
/// </summary>
public static class ExecutableBits
{
    private const UnixFileMode AllExecute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    /// <summary>True when the user may execute <paramref name="path"/>; always true on Windows.</summary>
    public static bool Has(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        return (File.GetUnixFileMode(path) & UnixFileMode.UserExecute) != 0;
    }

    /// <summary>Adds the user, group and other execute bits to <paramref name="path"/>, keeping its other bits.</summary>
    public static void Ensure(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var mode = File.GetUnixFileMode(path);
        if ((mode & UnixFileMode.UserExecute) == 0)
        {
            File.SetUnixFileMode(path, mode | AllExecute);
        }
    }

    /// <summary>
    /// Ensures the execute bits of a converted game and of every file beside it with the same extension: the relinker names the other
    /// executables it converts after the main one's extension (see USAGE.md, --executable).
    /// </summary>
    public static void EnsureGame(string executable)
    {
        ArgumentNullException.ThrowIfNull(executable);
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Ensure(executable);
        var extension = Path.GetExtension(executable);
        if (extension.Length > 0 && Path.GetDirectoryName(executable) is { } folder)
        {
            foreach (var sibling in Directory.EnumerateFiles(folder, "*" + extension))
            {
                Ensure(sibling);
            }
        }
    }
}
