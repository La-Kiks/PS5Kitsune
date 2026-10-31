using System.Globalization;

namespace PS5Kitsune.Core.Diagnostics;

/// <summary>An exit code as people read it on the OS that produced it. See the 8b spec, "Linux exit codes".</summary>
public static class ExitCodes
{
    // The usual Linux signals by number; others are shown by number only.
    private static readonly string?[] SignalNames =
    [
        null, "SIGHUP", "SIGINT", "SIGQUIT", "SIGILL", "SIGTRAP", "SIGABRT", "SIGBUS", "SIGFPE", "SIGKILL", "SIGUSR1",
        "SIGSEGV", "SIGUSR2", "SIGPIPE", "SIGALRM", "SIGTERM",
    ];

    /// <summary>The code of a process that ran on this OS: hexadecimal on Windows (0xC0000005), decimal on Linux with the signal (139 (signal 11, SIGSEGV)).</summary>
    public static string Describe(uint code) => Describe(code, OperatingSystem.IsLinux());

    internal static string Describe(uint code, bool linux)
    {
        if (!linux)
        {
            return string.Create(CultureInfo.InvariantCulture, $"0x{code:X8}");
        }

        // The Linux launcher returns 128 + the signal that ended a process; real-time signals go up to 64.
        var signal = code - 128;
        if (code <= 128 || signal > 64)
        {
            return code.ToString(CultureInfo.InvariantCulture);
        }

        return signal < SignalNames.Length && SignalNames[signal] is { } name
            ? string.Create(CultureInfo.InvariantCulture, $"{code} (signal {signal}, {name})")
            : string.Create(CultureInfo.InvariantCulture, $"{code} (signal {signal})");
    }
}
