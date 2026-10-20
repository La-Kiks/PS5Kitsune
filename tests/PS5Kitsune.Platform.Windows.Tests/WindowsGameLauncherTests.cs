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
    private readonly WindowsGameLauncher _launcher = new(NullLogger<WindowsGameLauncher>.Instance);

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

    public void Dispose() => _temp.Dispose();
}
