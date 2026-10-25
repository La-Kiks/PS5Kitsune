using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Checks.Vulkan;
using PS5Kitsune.Core.Launch;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Diagnostics;

/// <param name="GpuFailure">Why no GPU was detected; null when the probe ran.</param>
/// <param name="RamBytes">Null when unknown.</param>
/// <param name="CommitLimitBytes">Null where the OS has no commit limit.</param>
public sealed record CrashReportFacts(
    string AppVersion, string Os, CpuFacts Cpu, IReadOnlyList<VulkanGpu> Gpus, string? GpuFailure, long? RamBytes, ulong? CommitLimitBytes);

/// <summary>
/// The text Report copies for AnyPS5's Game bring-up Discussions: the PC, the game, the exit and the end of the log.
/// Nothing is sent. Its fields name no full path but the log's. See the 7c spec, "Fixes".
/// </summary>
public sealed class CrashReporter(GameLibrary library, AnyPS5Profile profile, ICpuInfo cpu, IVulkanProbeRunner vulkan, IPlatform platform)
{
    public const int LogLines = 30;

    // Lavapipe's manifest named without its folder, so the report holds no path.
    private const string ManifestName = "lvp_icd.x86_64.json";

    /// <param name="diagnosis">Null when nothing matched (diagnose --report).</param>
    /// <exception cref="GameNotFoundException">No game with this title ID.</exception>
    /// <exception cref="InvalidGameRecordException">The game.json is malformed.</exception>
    public async Task<string> BuildAsync(string titleId, string logPath, Diagnosis? diagnosis, CancellationToken cancellationToken)
    {
        var (_, record) = library.Find(titleId);
        VulkanProbeOutcome outcome;
        try
        {
            outcome = await vulkan.RunAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Like the Settings page: a probe that throws is a failed detection, so the report still gets written.
            outcome = VulkanProbeOutcome.Failed(exception.Message);
        }

        var facts = new CrashReportFacts(
            typeof(CrashReporter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown",
            RuntimeInformation.OSDescription,
            cpu.Read(),
            outcome.Report?.Gpus ?? [],
            outcome.FailureReason,
            GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            platform.GetCommitLimitBytes());
        var lastLines = await LastLinesAsync(logPath, cancellationToken).ConfigureAwait(false);
        var loggedCode = diagnosis is null ? await LoggedExitCodeAsync(logPath, cancellationToken).ConfigureAwait(false) : null;
        return Format(facts, profile.Id, record, diagnosis, logPath, lastLines, loggedCode);
    }

    internal static string Format(CrashReportFacts facts, string profileId, GameRecord record, Diagnosis? diagnosis, string logPath, IReadOnlyList<string> lastLines, uint? loggedExitCode = null)
    {
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"PS5Kitsune {facts.AppVersion} on {facts.Os}");
        text.AppendLine(CultureInfo.InvariantCulture, $"CPU: {facts.Cpu.Vendor}{(facts.Cpu.MissingExtensions.Count == 0 ? "" : $", missing {string.Join(", ", facts.Cpu.MissingExtensions)}")}");
        if (facts.Gpus.Count == 0)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"GPU: none detected{(facts.GpuFailure is { } reason ? $" ({reason})" : "")}");
        }

        foreach (var gpu in facts.Gpus)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"GPU: {gpu.Name}, driver {gpu.DriverInfo ?? "unknown"}{(gpu.SupportsAnyPS5 ? "" : " (not supported)")}");
        }

        text.AppendLine(CultureInfo.InvariantCulture, $"Memory: {GiB(facts.RamBytes)} RAM, commit limit {GiB((long?)facts.CommitLimitBytes)}");
        text.AppendLine(CultureInfo.InvariantCulture, $"AnyPS5: {record.ConvertedWith.Mode} {record.ConvertedWith.Version}, profile {profileId}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Game: {record.TitleId} {record.Name}, audit {record.Audit.Implemented} implemented, {record.Audit.Stub} stubs, {record.Audit.Absent} absent");
        var variables = LaunchEnvironment.For(record.Settings, record.Settings.Lavapipe ? ManifestName : null, null)
            .OrderBy(variable => variable.Key, StringComparer.Ordinal)
            .Select(variable => $"{variable.Key}={variable.Value}")
            .ToList();
        text.AppendLine(CultureInfo.InvariantCulture, $"Settings: {(variables.Count == 0 ? "defaults" : string.Join(", ", variables))}");
        var exitCode = diagnosis?.ExitCode ?? loggedExitCode;
        text.AppendLine(CultureInfo.InvariantCulture, $"Exit code: {(exitCode is { } code ? string.Create(CultureInfo.InvariantCulture, $"0x{code:X8}") : "unknown")}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Diagnosis: {diagnosis?.Id ?? "none"}");
        if (diagnosis?.Line is { } line)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"Matched line {diagnosis.LineNumber}: {line}");
        }

        text.AppendLine(CultureInfo.InvariantCulture, $"Log: {logPath}");
        text.AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"Last {LogLines} log lines:");
        text.AppendLine("```text");
        foreach (var logLine in lastLines)
        {
            text.AppendLine(logLine);
        }

        text.AppendLine("```");
        return text.ToString();
    }

    /// <summary>The code of the last exit line anywhere in the log; null when it has none or cannot be read.</summary>
    internal static async Task<uint?> LoggedExitCodeAsync(string logPath, CancellationToken cancellationToken)
    {
        try
        {
            uint? code = null;
            using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                if (ExitLines.Parse(line) is { } exit)
                {
                    code = exit.Code;
                }
            }

            return code;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The last <see cref="LogLines"/> lines; one line saying why when the log cannot be read.</summary>
    internal static async Task<IReadOnlyList<string>> LastLinesAsync(string logPath, CancellationToken cancellationToken)
    {
        try
        {
            var kept = new Queue<string>(LogLines);
            using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                if (kept.Count == LogLines)
                {
                    kept.Dequeue();
                }

                kept.Enqueue(line);
            }

            return [.. kept];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [$"(the log could not be read: {exception.Message})"];
        }
    }

    private static string GiB(long? bytes) =>
        bytes is { } value ? string.Create(CultureInfo.InvariantCulture, $"{value / (double)(1L << 30):0.0} GiB") : "unknown";
}
