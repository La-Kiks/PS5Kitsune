using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;
using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Tests;

namespace PS5Kitsune.Platform.Windows.Tests;

[SupportedOSPlatform("windows")]
public sealed partial class WindowsGameLauncherTests : IDisposable
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

    private async Task<(int ExitCode, string Log, string LogFile)> RunCmdAsync(IReadOnlyDictionary<string, string>? environment, params string[] arguments)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var game = _launcher.Launch(new GameLaunchRequest(Path.Combine(Environment.SystemDirectory, "cmd.exe"), _temp.Path, Logs)
        {
            Arguments = arguments,
            Environment = environment ?? new Dictionary<string, string>(),
        });
        var exitCode = await game.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        return (exitCode, await File.ReadAllTextAsync(game.LogFile, cancellationToken), game.LogFile);
    }

    private Task<(int ExitCode, string Log, string LogFile)> RunCmdAsync(params string[] arguments) => RunCmdAsync(null, arguments);

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

    [Fact]
    public async Task Passes_the_settings_to_the_game_only_on_top_of_the_inherited_environment()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");

        var (_, log, _) = await RunCmdAsync(
            new Dictionary<string, string> { ["PS5KITSUNE_TEST_SETTING"] = "from-settings" },
            "/c", "echo", "%PS5KITSUNE_TEST_SETTING%;%SystemRoot%");

        Assert.Contains("from-settings;", log, StringComparison.Ordinal);
        Assert.Contains(Environment.GetEnvironmentVariable("SystemRoot")!, log, StringComparison.OrdinalIgnoreCase);
        Assert.Null(Environment.GetEnvironmentVariable("PS5KITSUNE_TEST_SETTING"));
    }

    [Fact]
    public async Task A_game_that_exited_with_code_259_is_not_found_running()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        using var game = _launcher.Launch(new GameLaunchRequest(cmd, _temp.Path, Logs) { Arguments = ["/c", "exit", "259"] });

        // The handle held here keeps the PID from being reused while Attach looks at it.
        var exitCode = await game.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.Equal(259, exitCode);
        Assert.Null(_launcher.Attach(RecordOf(game, cmd)));
    }

    [Fact]
    public async Task The_game_breaks_away_from_a_job_that_allows_it()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        using var self = System.Diagnostics.Process.GetCurrentProcess();
        if (IsProcessInJob(self.SafeHandle, 0, out var alreadyInJob) && alreadyInJob)
        {
            // Breaking away from nested jobs also depends on the outer job.
            Assert.Skip("The test process already runs in a job.");
        }

        using var job = CreateJobObject(0, null);
        var limits = new ExtendedLimitInformation { BasicLimitInformation = new BasicLimitInformation { LimitFlags = JobObjectLimitBreakawayOk } };
        Assert.True(SetInformationJobObject(job, JobObjectExtendedLimitInformation, ref limits, (uint)Marshal.SizeOf<ExtendedLimitInformation>()));
        // The test process stays in this job: it has no limit but allowing breakaway, and closing it ends nothing.
        Assert.True(AssignProcessToJobObject(job, self.SafeHandle));

        using var game = StartPing();
        using var process = System.Diagnostics.Process.GetProcessById(game.ProcessId);

        Assert.True(IsProcessInJob(process.SafeHandle, job.DangerousGetHandle(), out var inJob));
        Assert.False(inJob);
        await game.StopAsync(TestContext.Current.CancellationToken);
    }

    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JobObjectLimitBreakawayOk = 0x00000800;

    [LibraryImport("kernel32.dll", EntryPoint = "CreateJobObjectW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateJobObject(nint jobAttributes, string? name);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetInformationJobObject(SafeFileHandle job, int infoClass, ref ExtendedLimitInformation info, uint length);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AssignProcessToJobObject(SafeFileHandle job, SafeProcessHandle process);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsProcessInJob(SafeProcessHandle process, nint job, [MarshalAs(UnmanagedType.Bool)] out bool result);

#pragma warning disable CS0649 // Native layouts: Windows reads fields the C# code never touches.
    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimitInformation
    {
        public BasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }
#pragma warning restore CS0649

    public void Dispose() => _temp.Dispose();
}
