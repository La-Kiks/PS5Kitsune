using System.IO.Pipes;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Tests;

namespace PS5Kitsune.Platform.Windows.Tests;

[SupportedOSPlatform("windows")]
public sealed class WindowsGameLauncherTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly WindowsGameLauncher _launcher = new(NullLogger<WindowsGameLauncher>.Instance) { StopGrace = TimeSpan.FromMilliseconds(200) };

    private static string Ping => Path.Combine(Environment.SystemDirectory, "PING.EXE");

    /// <summary>A game with no window that lives about 30 s.</summary>
    private ILaunchedGame StartPing() =>
        _launcher.Launch(new GameLaunchRequest(Ping, _temp.Path, Logs) { Arguments = ["-n", "30", "127.0.0.1"] });

    private static RunningGame RecordOf(ILaunchedGame game, string executable) =>
        new(RunningGame.CurrentSchemaVersion, game.ProcessId, game.StartedAt, executable, game.LogFile);

    private string Logs => _temp.Combine("logs");

    private async Task<(int ExitCode, string Log, string LogFile)> RunCmdAsync(params string[] arguments)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var game = _launcher.Launch(new GameLaunchRequest(Path.Combine(Environment.SystemDirectory, "cmd.exe"), _temp.Path, Logs) { Arguments = arguments });
        var exitCode = await game.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        return (exitCode, await File.ReadAllTextAsync(game.LogFile, cancellationToken), game.LogFile);
    }

    [Fact]
    public async Task Writes_the_output_to_a_log_file()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");

        var (exitCode, log, logFile) = await RunCmdAsync("/c", "echo", "hello");

        Assert.Equal(0, exitCode);
        Assert.Contains("hello", log, StringComparison.Ordinal);
        Assert.Equal(Logs, Path.GetDirectoryName(logFile));
    }

    [Fact]
    public async Task Returns_the_exit_code()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");

        var (exitCode, _, _) = await RunCmdAsync("/c", "exit", "7");

        Assert.Equal(7, exitCode);
    }

    [Fact]
    public async Task Runs_in_the_working_directory()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");

        var (_, log, _) = await RunCmdAsync("/c", "cd");

        Assert.Contains(_temp.Path, log, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Keeps_the_ten_newest_logs()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        for (var second = 0; second < 12; second++)
        {
            File.WriteAllText(Path.Combine(Directory.CreateDirectory(Logs).FullName, $"20000101-0000{second:00}.log"), "old");
        }

        await RunCmdAsync("/c", "exit", "0");

        var logs = Directory.GetFiles(Logs, "*.log");
        Assert.Equal(10, logs.Length);
        Assert.DoesNotContain(Path.Combine(Logs, "20000101-000000.log"), logs);
        Assert.Contains(Path.Combine(Logs, "20000101-000011.log"), logs);
    }

    [Fact]
    public async Task Passes_no_other_inheritable_handle_to_the_game()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        var cancellationToken = TestContext.Current.CancellationToken;

        // The client end is the write end, and the only one made inheritable.
        using var pipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        using var game = _launcher.Launch(new GameLaunchRequest(Path.Combine(Environment.SystemDirectory, "cmd.exe"), _temp.Path, Logs)
        {
            Arguments = ["/c", "ping", "-n", "11", "127.0.0.1", ">nul"],
        });
        pipe.DisposeLocalCopyOfClientHandle();

        // The game lives about 10 s. If it held the write end, the read would only end when it exits.
        var read = Task.Run(() => pipe.Read(new byte[1]), cancellationToken);
        var completed = await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken));

        Assert.Same(read, completed);
        Assert.Equal(0, await read);
        await game.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
    }

    [Fact]
    public async Task The_start_time_is_the_process_creation_time()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        using var game = StartPing();
        var after = DateTimeOffset.UtcNow.AddSeconds(1);
        await game.StopAsync(TestContext.Current.CancellationToken);

        Assert.InRange(game.StartedAt, before, after);
    }

    [Fact]
    public async Task Attaches_to_a_running_game_stops_it_and_reads_its_exit_code()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        var cancellationToken = TestContext.Current.CancellationToken;
        using var launched = StartPing();

        using var attached = _launcher.Attach(RecordOf(launched, Ping));

        Assert.NotNull(attached);
        Assert.Equal(launched.ProcessId, attached.ProcessId);
        Assert.Equal(launched.StartedAt, attached.StartedAt);
        Assert.Equal(launched.LogFile, attached.LogFile);
        await attached.StopAsync(cancellationToken);
        Assert.Equal(1, await attached.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(10), cancellationToken));
        Assert.Null(_launcher.Attach(RecordOf(launched, Ping)));
    }

    [Fact]
    public async Task Does_not_attach_when_the_start_time_differs()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        using var launched = StartPing();

        var attached = _launcher.Attach(RecordOf(launched, Ping) with { StartedAt = launched.StartedAt.AddSeconds(1) });
        await launched.StopAsync(TestContext.Current.CancellationToken);

        Assert.Null(attached);
    }

    [Fact]
    public async Task Does_not_attach_to_another_executable()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        using var launched = StartPing();

        var attached = _launcher.Attach(RecordOf(launched, Path.Combine(Environment.SystemDirectory, "cmd.exe")));
        await launched.StopAsync(TestContext.Current.CancellationToken);

        Assert.Null(attached);
    }

    [Fact]
    public async Task Does_not_attach_to_an_exited_game()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        var cancellationToken = TestContext.Current.CancellationToken;
        var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        using var launched = _launcher.Launch(new GameLaunchRequest(cmd, _temp.Path, Logs) { Arguments = ["/c", "exit", "0"] });
        await launched.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        Assert.Null(_launcher.Attach(RecordOf(launched, cmd)));
    }

    [Fact]
    public async Task Stop_ends_a_game_without_a_window()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        var cancellationToken = TestContext.Current.CancellationToken;
        using var game = StartPing();

        await game.StopAsync(cancellationToken);

        Assert.Equal(1, await game.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(10), cancellationToken));
    }

    [Fact]
    public async Task Cancelling_a_stop_leaves_the_game_running()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        var cancellationToken = TestContext.Current.CancellationToken;
        var patient = new WindowsGameLauncher(NullLogger<WindowsGameLauncher>.Instance) { StopGrace = TimeSpan.FromSeconds(10) };
        using var launched = patient.Launch(new GameLaunchRequest(Ping, _temp.Path, Logs) { Arguments = ["-n", "30", "127.0.0.1"] });
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => launched.StopAsync(cancellation.Token));

        using var attached = _launcher.Attach(RecordOf(launched, Ping));
        Assert.NotNull(attached);
        await attached.StopAsync(cancellationToken);
    }

    public void Dispose() => _temp.Dispose();
}
