using System.ComponentModel;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Platform.Linux.Tests;

[SupportedOSPlatform("linux")]
public sealed class LinuxGameLauncherTests : IDisposable
{
    private readonly PS5Kitsune.Core.Tests.TempDirectory _temp = new();

    private readonly LinuxGameLauncher _launcher = new(NullLogger<LinuxGameLauncher>.Instance)
    {
        StopGrace = TimeSpan.FromMilliseconds(300),
        AttachPoll = TimeSpan.FromMilliseconds(50),
    };

    private string Logs => _temp.Combine("logs");

    private ILaunchedGame Shell(string script, IReadOnlyDictionary<string, string>? environment = null) =>
        _launcher.Launch(new GameLaunchRequest("/bin/sh", _temp.Path, Logs)
        {
            Arguments = ["-c", script],
            Environment = environment ?? new Dictionary<string, string>(),
        });

    /// <summary>/usr/bin/sleep itself (not through sh), so /proc/&lt;pid&gt;/exe is /usr/bin/sleep from the start.</summary>
    private ILaunchedGame Sleep(int seconds) =>
        _launcher.Launch(new GameLaunchRequest("/usr/bin/sleep", _temp.Path, Logs) { Arguments = [seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)] });

    private static async Task<int> ExitOf(ILaunchedGame game) =>
        await game.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

    private static RunningGame RecordOf(ILaunchedGame game, string executable) =>
        new(RunningGame.CurrentSchemaVersion, game.ProcessId, game.StartedAt, executable, game.LogFile);

    private static async Task LogContains(ILaunchedGame game, string text)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!(await File.ReadAllTextAsync(game.LogFile, TestContext.Current.CancellationToken)).Contains(text, StringComparison.Ordinal))
        {
            Assert.True(DateTime.UtcNow < deadline, $"The log never showed \"{text}\".");
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Writes_stdout_and_stderr_to_the_log_and_returns_the_exit_code()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        using var game = Shell("echo out; echo err >&2; exit 3");

        Assert.Equal(3, await ExitOf(game));
        var log = await File.ReadAllTextAsync(game.LogFile, TestContext.Current.CancellationToken);
        Assert.Contains("out", log, StringComparison.Ordinal);
        Assert.Contains("err", log, StringComparison.Ordinal);
        Assert.Equal(Logs, Path.GetDirectoryName(game.LogFile));
    }

    [Fact]
    public async Task Runs_in_the_working_directory_with_the_added_variables()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        using var game = Shell("pwd; echo \"v=$PS5K_TEST\"; echo \"path=$PATH\"", new Dictionary<string, string> { ["PS5K_TEST"] = "on" });

        Assert.Equal(0, await ExitOf(game));
        var log = await File.ReadAllTextAsync(game.LogFile, TestContext.Current.CancellationToken);
        Assert.Contains(_temp.Path, log, StringComparison.Ordinal);
        Assert.Contains("v=on", log, StringComparison.Ordinal);
        Assert.DoesNotContain("path=\n", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_game_leads_its_own_session()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        using var game = Shell("sleep 30");
        try
        {
            Assert.Equal(game.ProcessId, ProcStat.Read(game.ProcessId)!.Value.Session);
        }
        finally
        {
            await game.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task A_signal_gives_128_plus_its_number()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        using var game = Shell("kill -9 $$");

        Assert.Equal(137, await ExitOf(game));
    }

    [Fact]
    public async Task Stop_ends_a_game_with_sigterm()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        using var game = Sleep(30);

        await game.StopAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(128 + 15, await ExitOf(game));
    }

    [Fact]
    public async Task Stop_kills_a_game_that_ignores_sigterm()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        using var game = Shell("trap '' TERM; echo trapped; sleep 30");
        // Stop right after the launch would reach sh before its trap: wait for the line printed after it.
        await LogContains(game, "trapped");

        await game.StopAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(128 + 9, await ExitOf(game));
    }

    [Fact]
    public async Task Attach_finds_a_running_game_and_follows_it_until_it_exits()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        using var game = Sleep(1);

        using var attached = _launcher.Attach(RecordOf(game, "/usr/bin/sleep"));

        Assert.NotNull(attached);
        Assert.Equal(game.ProcessId, attached.ProcessId);
        Assert.Equal(-1, await ExitOf(attached));
    }

    [Fact]
    public async Task Attach_refuses_another_start_time_or_executable()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        using var game = Sleep(30);
        try
        {
            Assert.Null(_launcher.Attach(RecordOf(game, "/usr/bin/sleep") with { StartedAt = game.StartedAt.AddSeconds(-60) }));
            Assert.Null(_launcher.Attach(RecordOf(game, "/usr/bin/true")));
        }
        finally
        {
            await game.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Attach_follows_symbolic_links_in_the_recorded_executable()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        // Like a games folder reached through a link (~/Games -> /mnt/data/Games): /proc/<pid>/exe shows the resolved path.
        var linkedBin = _temp.Combine("bin");
        Directory.CreateSymbolicLink(linkedBin, "/usr/bin");
        using var game = Sleep(30);
        try
        {
            using var attached = _launcher.Attach(RecordOf(game, linkedBin + "/sleep"));

            Assert.NotNull(attached);
        }
        finally
        {
            await game.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Attach_returns_null_once_the_game_has_exited()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        // The record would match if the process still ran, so only its exit can make Attach refuse it.
        using var game = Sleep(0);
        await ExitOf(game);

        Assert.Null(_launcher.Attach(RecordOf(game, "/usr/bin/sleep")));
    }

    [Fact]
    public async Task The_game_does_not_inherit_ignored_sigpipe()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        // .NET ignores SIGPIPE, and an ignored signal stays ignored across exec unless posix_spawn resets it.
        using var game = Shell("grep SigIgn /proc/$$/status");

        Assert.Equal(0, await ExitOf(game));
        var line = (await File.ReadAllTextAsync(game.LogFile, TestContext.Current.CancellationToken)).Trim();
        var ignored = ulong.Parse(line["SigIgn:".Length..].Trim(), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(0UL, ignored & 0x1000); // SIGPIPE is 13: bit 12.
    }

    [Fact]
    public void A_missing_executable_throws()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");

        Assert.Throws<Win32Exception>(() => _launcher.Launch(new GameLaunchRequest(_temp.Combine("missing.elf"), _temp.Path, Logs)));
    }

    [Fact]
    public async Task Keeps_the_newest_logs()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        Directory.CreateDirectory(Logs);
        for (var day = 10; day < 25; day++)
        {
            await File.WriteAllTextAsync(Path.Combine(Logs, $"202601{day}-120000.log"), "", TestContext.Current.CancellationToken);
        }

        using var game = Shell("exit 0");
        await ExitOf(game);

        Assert.Equal(10, Directory.GetFiles(Logs, "*.log").Length);
    }

    public void Dispose() => _temp.Dispose();
}
