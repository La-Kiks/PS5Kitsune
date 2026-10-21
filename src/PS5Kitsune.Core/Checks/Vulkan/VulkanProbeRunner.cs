using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Processes;

namespace PS5Kitsune.Core.Checks.Vulkan;

public enum VulkanProbeStatus
{
    Succeeded,

    /// <summary>The first attempt failed; the second, with Dozen disabled, succeeded.</summary>
    SucceededWithoutDozen,

    Failed,
}

/// <param name="Report">What the probe found; null when <paramref name="Status"/> is Failed.</param>
/// <param name="FailureReason">Why both attempts failed; null otherwise.</param>
public sealed record VulkanProbeOutcome(VulkanProbeStatus Status, VulkanProbeReport? Report, string? FailureReason)
{
    public static VulkanProbeOutcome Failed(string reason) => new(VulkanProbeStatus.Failed, null, reason);
}

public interface IVulkanProbeRunner
{
    /// <summary>Probes Vulkan in a child process. A crash or a hang becomes a Failed outcome, not an exception.</summary>
    Task<VulkanProbeOutcome> RunAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Relaunches the running executable with <see cref="ProbeArgument"/>, so a driver that crashes
/// during enumeration takes down the child, not the app. See docs/architecture.md, "Isolated Vulkan probe".
/// </summary>
public sealed partial class VulkanProbeRunner : IVulkanProbeRunner
{
    public const string ProbeArgument = "--probe-vulkan";

    public const string DisableDriversVariable = "VK_LOADER_DRIVERS_DISABLE";

    public const string DozenPattern = "*dzn*";

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    private readonly IProcessRunner _runner;
    private readonly ILogger<VulkanProbeRunner> _logger;
    private readonly string _executable;
    private readonly TimeSpan _timeout;

    public VulkanProbeRunner(IProcessRunner runner, ILogger<VulkanProbeRunner> logger)
        : this(runner, logger, Environment.ProcessPath ?? throw new InvalidOperationException("The path of the running executable is unknown."), DefaultTimeout)
    {
    }

    internal VulkanProbeRunner(IProcessRunner runner, ILogger<VulkanProbeRunner> logger, string executable, TimeSpan timeout)
    {
        _runner = runner;
        _logger = logger;
        _executable = executable;
        _timeout = timeout;
    }

    public async Task<VulkanProbeOutcome> RunAsync(CancellationToken cancellationToken = default)
    {
        var (report, failure) = await AttemptAsync(withoutDozen: false, cancellationToken).ConfigureAwait(false);
        if (report is not null)
        {
            return new VulkanProbeOutcome(VulkanProbeStatus.Succeeded, report, null);
        }

        LogRetrying(failure);
        var (retryReport, retryFailure) = await AttemptAsync(withoutDozen: true, cancellationToken).ConfigureAwait(false);
        if (retryReport is not null)
        {
            return new VulkanProbeOutcome(VulkanProbeStatus.SucceededWithoutDozen, retryReport, null);
        }

        LogFailed(retryFailure);
        return VulkanProbeOutcome.Failed($"{failure} With Dozen disabled: {retryFailure}");
    }

    private async Task<(VulkanProbeReport? Report, string Failure)> AttemptAsync(bool withoutDozen, CancellationToken cancellationToken)
    {
        var spec = new ProcessSpec(_executable, [ProbeArgument])
        {
            Environment = withoutDozen
                ? new Dictionary<string, string?> { [DisableDriversVariable] = DozenPattern }
                : new Dictionary<string, string?>(),
        };
        var output = new StringBuilder();
        var errors = new OutputTail(5);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        ProcessResult result;
        try
        {
            result = await _runner.RunAsync(spec, line =>
            {
                if (line.Source == OutputSource.StandardOutput)
                {
                    output.AppendLine(line.Text);
                }

                errors.Collect(line);
            }, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (null, string.Create(CultureInfo.InvariantCulture, $"The probe did not answer within {_timeout.TotalSeconds:0.##} seconds."));
        }

        if (result.ExitCode != 0)
        {
            var stderr = errors.ToString();
            var detail = stderr.Length == 0 ? "" : $" ({stderr.ReplaceLineEndings(" ")})";
            return (null, string.Create(CultureInfo.InvariantCulture, $"The probe exited with code {result.ExitCode} (0x{result.ExitCode:X8}){detail}."));
        }

        try
        {
            return (VulkanProbeReport.FromJson(output.ToString()), "");
        }
        catch (JsonException exception)
        {
            return (null, $"The probe output is not valid JSON: {exception.Message}");
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Vulkan probe failed: {Failure} Retrying with Dozen disabled.")]
    private partial void LogRetrying(string failure);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Vulkan probe failed again with Dozen disabled: {Failure}")]
    private partial void LogFailed(string failure);
}
