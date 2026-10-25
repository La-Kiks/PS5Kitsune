using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Checks.Vulkan;
using PS5Kitsune.Core.Diagnostics;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Tests.Library;

namespace PS5Kitsune.Core.Tests.Diagnostics;

public sealed class CrashReporterTests : IDisposable
{
    private const string LogPath = "C:\\PS5K\\games\\PPSA99666\\.ps5kitsune\\logs\\20261025-101500.log";

    private static readonly CrashReportFacts Facts = new(
        "0.7.0",
        "Microsoft Windows 10.0.26300",
        new CpuFacts("AuthenticAMD", []),
        [new VulkanGpu("AMD Radeon(TM) Graphics", 0x1002, "integrated", "1.3.280", "AmdProprietary", "AMD proprietary driver", "25.10.1", true, true)],
        null,
        16L << 30,
        32UL << 30);

    private readonly TempDirectory _temp = new();

    private static Diagnosis Unexplained => new(DiagnosisIds.Unexplained, new Dictionary<string, string>(), null, null, 0xC0000005, LogPath);

    private static string Format(Diagnosis? diagnosis, GameRecord? record = null) =>
        CrashReporter.Format(Facts, "doom-validated-2026-10", record ?? TestRecords.Sample(), diagnosis, LogPath, ["line a", "line b"]);

    [Fact]
    public void The_report_has_the_pc_the_game_the_exit_and_the_log()
    {
        var report = Format(Unexplained, TestRecords.Sample() with { Settings = new GameSettings { Language = 5 } });

        Assert.Contains("PS5Kitsune 0.7.0 on Microsoft Windows 10.0.26300", report, StringComparison.Ordinal);
        Assert.Contains("CPU: AuthenticAMD", report, StringComparison.Ordinal);
        Assert.Contains("GPU: AMD Radeon(TM) Graphics, driver 25.10.1", report, StringComparison.Ordinal);
        Assert.Contains("Memory: 16.0 GiB RAM, commit limit 32.0 GiB", report, StringComparison.Ordinal);
        Assert.Contains("AnyPS5: release v0.1.2, profile doom-validated-2026-10", report, StringComparison.Ordinal);
        Assert.Contains("Game: PPSA99666 DOOM, audit 262 implemented, 3 stubs, 0 absent", report, StringComparison.Ordinal);
        Assert.Contains("Settings: ANYPS5_LANGUAGE=5", report, StringComparison.Ordinal);
        Assert.Contains("Exit code: 0xC0000005", report, StringComparison.Ordinal);
        Assert.Contains("Diagnosis: unexplained", report, StringComparison.Ordinal);
        Assert.Contains("Log: " + LogPath, report, StringComparison.Ordinal);
        Assert.EndsWith("```text" + Environment.NewLine + "line a" + Environment.NewLine + "line b" + Environment.NewLine + "```" + Environment.NewLine, report, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_the_log_line_names_a_full_path()
    {
        var header = Format(Unexplained, TestRecords.Sample() with { Settings = new GameSettings { Lavapipe = true } }).Split("Last 30 log lines:")[0];

        Assert.DoesNotContain(":\\", header.Replace("Log: " + LogPath, "", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public void Without_a_diagnosis_the_report_says_none_and_default_settings_say_defaults()
    {
        var report = Format(null);

        Assert.Contains("Diagnosis: none", report, StringComparison.Ordinal);
        Assert.Contains("Exit code: unknown", report, StringComparison.Ordinal);
        Assert.Contains("Settings: defaults", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_last_30_lines_of_the_log_are_quoted()
    {
        var log = _temp.Combine("game.log");
        await File.WriteAllLinesAsync(log, Enumerable.Range(1, 40).Select(index => $"line {index}"), TestContext.Current.CancellationToken);

        var lines = await CrashReporter.LastLinesAsync(log, TestContext.Current.CancellationToken);

        Assert.Equal(Enumerable.Range(11, 30).Select(index => $"line {index}"), lines);
    }

    public void Dispose() => _temp.Dispose();
}
