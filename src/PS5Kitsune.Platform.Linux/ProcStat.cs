using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PS5Kitsune.Platform.Linux;

/// <summary>The fields of /proc/&lt;pid&gt;/stat the launcher reads. See proc(5).</summary>
/// <param name="State">Field 3: R, S, D, Z (zombie), T…</param>
/// <param name="Session">Field 6: the session ID.</param>
/// <param name="StartTicks">Field 22: when the process started, in clock ticks since boot.</param>
internal readonly partial record struct ProcStat(char State, int Session, ulong StartTicks)
{
    private const int ClockTicksName = 2; // _SC_CLK_TCK

    /// <summary>Null when the line is not a stat line.</summary>
    public static ProcStat? Parse(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        // The name (field 2) is in parentheses and may contain spaces and parentheses: the fields start after the last ')'.
        var close = line.LastIndexOf(')');
        if (close < 0)
        {
            return null;
        }

        var fields = line[(close + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // fields[0] is field 3.
        return fields.Length > 19
            && fields[0].Length == 1
            && int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var session)
            && ulong.TryParse(fields[19], NumberStyles.None, CultureInfo.InvariantCulture, out var startTicks)
            ? new ProcStat(fields[0][0], session, startTicks)
            : null;
    }

    /// <summary>The process's stat; null when it does not exist or cannot be read.</summary>
    public static ProcStat? Read(int pid)
    {
        try
        {
            return Parse(File.ReadAllText($"/proc/{pid.ToString(CultureInfo.InvariantCulture)}/stat"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The "btime" line of /proc/stat: the boot time in seconds since the epoch.</summary>
    /// <exception cref="InvalidDataException">No btime line.</exception>
    public static long BootTime(string procStatText)
    {
        ArgumentNullException.ThrowIfNull(procStatText);
        foreach (var line in procStatText.Split('\n'))
        {
            if (line.StartsWith("btime ", StringComparison.Ordinal)
                && long.TryParse(line.AsSpan(6).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
            {
                return seconds;
            }
        }

        throw new InvalidDataException("/proc/stat has no btime line.");
    }

    public static DateTimeOffset StartTime(ulong startTicks, long bootTimeSeconds, long ticksPerSecond) =>
        DateTimeOffset.FromUnixTimeSeconds(bootTimeSeconds).AddTicks((long)(startTicks * (ulong)TimeSpan.TicksPerSecond / (ulong)ticksPerSecond));

    /// <summary>When <paramref name="pid"/> started, in UTC; null when it does not exist. Stable across reads, so it can be compared with running.json.</summary>
    [SupportedOSPlatform("linux")]
    public static DateTimeOffset? StartTimeOf(int pid) =>
        Read(pid) is { } stat ? StartTime(stat.StartTicks, BootTime(File.ReadAllText("/proc/stat")), SysConf(ClockTicksName)) : null;

    [LibraryImport("libc", EntryPoint = "sysconf")]
    private static partial long SysConf(int name);
}
