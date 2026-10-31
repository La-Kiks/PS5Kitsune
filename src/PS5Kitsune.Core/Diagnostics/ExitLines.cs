using System.Globalization;

namespace PS5Kitsune.Core.Diagnostics;

/// <summary>The line PS5Kitsune appends to a launch's log once the game has exited. See the 7c spec, "Exit line".</summary>
public static class ExitLines
{
    public const string Prefix = "[ps5kitsune] ";

    private const string ExitedStart = "exited with code ";

    private const string StoppedStart = "stopped by the user (exit code ";

    private const string UnknownLine = "exited (exit code unknown)";

    /// <summary>The exit line of a game that ran on this OS.</summary>
    public static string Exited(int exitCode) => Exited(exitCode, OperatingSystem.IsLinux());

    // The decimal code comes first on both OSes, so Parse reads every line ever written.
    internal static string Exited(int exitCode, bool linux) =>
        linux
            ? $"{Prefix}{ExitedStart}{ExitCodes.Describe((uint)exitCode, linux: true)}"
            : string.Create(CultureInfo.InvariantCulture, $"{Prefix}{ExitedStart}{(uint)exitCode} ({ExitCodes.Describe((uint)exitCode, linux: false)})");

    /// <summary>The exit line of a game whose exit code cannot be read: a game found again on Linux, which is not PS5Kitsune's child.</summary>
    public static string Unknown() => Prefix + UnknownLine;

    public static string Stopped(int exitCode) =>
        string.Create(CultureInfo.InvariantCulture, $"{Prefix}{StoppedStart}{(uint)exitCode})");

    /// <summary>What an exit line says; null for any other line.</summary>
    internal static ExitRecord? Parse(string line)
    {
        if (!line.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = line.AsSpan(Prefix.Length);
        if (rest.SequenceEqual(UnknownLine))
        {
            return new ExitRecord(null, Stopped: false);
        }

        if (TryCode(rest, ExitedStart, out var code))
        {
            return new ExitRecord(code, Stopped: false);
        }

        return TryCode(rest, StoppedStart, out code) ? new ExitRecord(code, Stopped: true) : null;
    }

    private static bool TryCode(ReadOnlySpan<char> rest, string start, out uint code)
    {
        code = 0;
        if (!rest.StartsWith(start, StringComparison.Ordinal))
        {
            return false;
        }

        var digits = rest[start.Length..];
        var end = digits.IndexOfAnyExceptInRange('0', '9');
        return uint.TryParse(end < 0 ? digits : digits[..end], NumberStyles.None, CultureInfo.InvariantCulture, out code);
    }
}

/// <param name="Stopped">True when the user ended the game with Stop.</param>
internal readonly record struct ExitRecord(uint? Code, bool Stopped);
