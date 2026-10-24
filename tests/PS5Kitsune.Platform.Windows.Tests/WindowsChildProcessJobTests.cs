using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging.Abstractions;

namespace PS5Kitsune.Platform.Windows.Tests;

[SupportedOSPlatform("windows")]
public sealed class WindowsChildProcessJobTests
{
    private static Process StartPing() => Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "PING.EXE"), "-n 30 127.0.0.1")
    {
        CreateNoWindow = true,
        UseShellExecute = false,
        RedirectStandardOutput = true,
    })!;

    [Fact]
    public async Task A_process_in_the_job_ends_when_the_job_is_closed()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        var cancellationToken = TestContext.Current.CancellationToken;
        using var ping = StartPing();
        var job = new WindowsChildProcessJob(NullLogger<WindowsChildProcessJob>.Instance);

        job.Assign(ping.Id);
        job.Dispose();

        await ping.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        Assert.True(ping.HasExited);
    }

    [Fact]
    public async Task Assigning_a_process_that_exited_does_not_throw()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        var cancellationToken = TestContext.Current.CancellationToken;
        using var ping = StartPing();
        var processId = ping.Id;
        ping.Kill();
        await ping.WaitForExitAsync(cancellationToken);
        using var job = new WindowsChildProcessJob(NullLogger<WindowsChildProcessJob>.Instance);

        job.Assign(processId);
    }
}
