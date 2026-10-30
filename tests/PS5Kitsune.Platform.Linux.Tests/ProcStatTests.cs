namespace PS5Kitsune.Platform.Linux.Tests;

public sealed class ProcStatTests
{
    // Field 3 is the state, 6 the session, 22 the start time in clock ticks since boot.
    private const string Sleep = "4259 (sleep) S 4167 4259 4259 0 -1 4194304 101 0 0 0 0 0 0 0 20 0 1 0 123456 2367488 224 18446744073709551615 1 1 0 0 0 0 0 0 0 0 0 0 17 3 0 0 0 0 0";

    [Fact]
    public void Reads_the_state_the_session_and_the_start_ticks()
    {
        var stat = ProcStat.Parse(Sleep);

        Assert.NotNull(stat);
        Assert.Equal('S', stat.Value.State);
        Assert.Equal(4259, stat.Value.Session);
        Assert.Equal(123456UL, stat.Value.StartTicks);
    }

    [Fact]
    public void A_name_with_spaces_and_parentheses_is_skipped_whole()
    {
        var stat = ProcStat.Parse(Sleep.Replace("(sleep)", "(my (game) x)", StringComparison.Ordinal));

        Assert.Equal('S', stat!.Value.State);
        Assert.Equal(123456UL, stat.Value.StartTicks);
    }

    [Fact]
    public void A_truncated_line_is_not_a_stat() => Assert.Null(ProcStat.Parse("4259 (sleep) S 4167"));

    [Fact]
    public void The_start_time_is_boot_time_plus_ticks()
    {
        var bootTime = ProcStat.BootTime("cpu  1 2 3\nbtime 1761800000\nprocesses 12\n");

        Assert.Equal(1761800000, bootTime);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1761800000).AddMilliseconds(1234560), ProcStat.StartTime(123456, bootTime, 100));
    }

    [Fact]
    public void Start_times_up_to_five_seconds_apart_are_the_same_start()
    {
        var recorded = DateTimeOffset.FromUnixTimeSeconds(1761800000);

        Assert.True(ProcStat.SameStart(recorded.AddSeconds(4), recorded));
        Assert.True(ProcStat.SameStart(recorded.AddSeconds(-4), recorded));
        Assert.False(ProcStat.SameStart(recorded.AddSeconds(6), recorded));
        Assert.False(ProcStat.SameStart(recorded.AddSeconds(-6), recorded));
    }
}
