using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Diagnostics;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Tests.Diagnostics;

public sealed class CrashDiagnoserTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly CrashDiagnoser _diagnoser = new(ProfileLoader.LoadEmbedded(), NullLogger<CrashDiagnoser>.Instance);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string Log(string text)
    {
        var path = _temp.Combine("logs", "20261025-101500.log");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    /// <summary>Log text built from AnyPS5 v0.1.2's messages, the rule id it must give, and its capture ("module" or "variable") when it has one.</summary>
    public static TheoryData<string, string, string?> Samples => new()
    {
        { "FATAL: unhandled exception 0xc0000005 on thread 4321 'Render'\n  rip 0x00007ffb1a2b3c4d nvgpucomp64.dll+0x1a2b3c\n", DiagnosisIds.NvidiaShaderCompiler, null },
        { "Loading PRX: C:\\G\\libs\\libSceAudioOut.prx -> FAILED\nFailed to load module: C:\\G\\libs\\libSceAudioOut.prx\nGetLastError: 193 - %1 is not a valid Win32 application.\n", DiagnosisIds.LibsNotDll, "C:\\G\\libs\\libSceAudioOut.prx" },
        { "Loading PRX: C:\\G\\libs\\libSceAudioOut.prx -> FAILED\nFailed to load module: C:\\G\\libs\\libSceAudioOut.prx\nGetLastError: 126 - The specified module could not be found.\n", DiagnosisIds.LibsMissingDll, "C:\\G\\libs\\libSceAudioOut.prx" },
        { "what():  replace guest placeholder with resident direct memory: The paging file is too small for this operation to complete.\n", DiagnosisIds.CommitLimit, null },
        { "what():  commit direct memory pages\n", DiagnosisIds.CommitLimit, null },
        { "Vulkan: buffer device address and shaderInt64 are required\n", DiagnosisIds.GpuFeatures, null },
        { "\nFATAL: unhandled exception 0xc000001d on thread 1 'main'\n", DiagnosisIds.CpuTooOld, null },
        { "what():  param.json not found\n", DiagnosisIds.ParamJson, null },
        { "what():  unterminated string in param.json\n", DiagnosisIds.ParamJson, null },
        { "what():  sceSystemServiceParamGetInt: ANYPS5_LANGUAGE must be a console language number from 0 to 30\n", DiagnosisIds.InvalidSetting, "ANYPS5_LANGUAGE" },
        { "libSceFont: ANYPS5_SYSTEM_FONTS is not a directory: C:\\fonts\n", DiagnosisIds.InvalidSetting, "ANYPS5_SYSTEM_FONTS" },
        { "what():  Vulkan: no usable device whose name contains ANYPS5_GPU=\"GeForce\"; devices: AMD Radeon(TM) Graphics\n", DiagnosisIds.InvalidSetting, "ANYPS5_GPU" },
        { "what():  Vulkan presentation: the surface does not support the requested present mode\n", DiagnosisIds.InvalidSetting, "ANYPS5_PRESENT_MODE" },
        { "Ngs2: reverb at 96000 Hz is not implemented\n", DiagnosisIds.NotImplemented, null },
    };

    [Theory]
    [MemberData(nameof(Samples))]
    public async Task Each_known_error_gives_its_rule(string log, string id, string? capture)
    {
        var diagnosis = await _diagnoser.DiagnoseAsync(Log("[debug:0] launcher: start\n" + log), 1, Token);

        Assert.NotNull(diagnosis);
        Assert.Equal(id, diagnosis.Id);
        Assert.Equal(1u, diagnosis.ExitCode);
        Assert.NotNull(diagnosis.Line);
        if (capture is not null)
        {
            Assert.Contains(capture, diagnosis.Captures.Values);
        }
    }

    [Fact]
    public async Task The_first_rule_in_profile_order_wins()
    {
        var log = Log("Ngs2: reverb at 96000 Hz is not implemented\nFATAL: unhandled exception 0xc0000005 on thread 1 'Render'\n  rip 0x00007ffb1a2b3c4d nvgpucomp64.dll+0x10\n");

        var diagnosis = await _diagnoser.DiagnoseAsync(log, 0xC0000005, Token);

        Assert.Equal(DiagnosisIds.NvidiaShaderCompiler, diagnosis?.Id);
        Assert.Equal(2, diagnosis?.LineNumber);
    }

    [Fact]
    public async Task An_all_rule_needs_every_pattern()
    {
        var diagnosis = await _diagnoser.DiagnoseAsync(Log("Failed to load module: C:\\G\\libs\\a.prx\n"), 1, Token);

        Assert.Equal(DiagnosisIds.Unexplained, diagnosis?.Id);
    }

    [Fact]
    public async Task An_exit_code_alone_can_match_an_any_rule()
    {
        var diagnosis = await _diagnoser.DiagnoseAsync(Log("nothing known\n"), 0xC000001D, Token);

        Assert.Equal(DiagnosisIds.CpuTooOld, diagnosis?.Id);
        Assert.Null(diagnosis?.Line);
        Assert.Null(diagnosis?.LineNumber);
    }

    [Theory]
    [InlineData(1u, DiagnosisIds.Unexplained)]
    [InlineData(0u, null)]
    [InlineData(null, null)]
    public async Task With_no_match_only_a_failed_exit_is_unexplained(uint? exitCode, string? id)
    {
        var diagnosis = await _diagnoser.DiagnoseAsync(Log("all good\n"), exitCode, Token);

        Assert.Equal(id, diagnosis?.Id);
    }

    [Fact]
    public async Task A_clean_exit_is_not_diagnosed_even_when_the_log_has_a_known_message()
    {
        var text = "Vulkan: buffer device address and shaderInt64 are required\n";

        Assert.Null(await _diagnoser.DiagnoseAsync(Log(text), 0, Token));
        Assert.Null(await _diagnoser.DiagnoseAsync(Log(text + ExitLines.Exited(0) + "\n"), null, Token));
    }

    [Fact]
    public async Task The_exit_line_gives_the_exit_code_when_none_is_passed()
    {
        var log = Log("all good\n" + ExitLines.Exited(unchecked((int)0xC000001D)) + "\n");

        var diagnosis = await _diagnoser.DiagnoseAsync(log, null, Token);

        Assert.Equal(DiagnosisIds.CpuTooOld, diagnosis?.Id);
        Assert.Equal(0xC000001Du, diagnosis?.ExitCode);
    }

    [Fact]
    public async Task A_game_stopped_by_the_user_is_not_diagnosed()
    {
        var log = Log("Vulkan: buffer device address and shaderInt64 are required\n" + ExitLines.Stopped(1) + "\n");

        Assert.Null(await _diagnoser.DiagnoseAsync(log, null, Token));
    }

    [Fact]
    public async Task A_stop_recorded_before_a_later_exit_line_still_wins()
    {
        var log = Log("Vulkan: buffer device address and shaderInt64 are required\n" + ExitLines.Stopped(1) + "\n" + ExitLines.Exited(1) + "\n");

        Assert.Null(await _diagnoser.DiagnoseAsync(log, null, Token));
    }

    [Fact]
    public async Task Finishing_appends_the_exit_line_on_its_own_line_then_diagnoses()
    {
        var log = Log("Vulkan: buffer device address and shaderInt64 are required\nlast line without newline");

        var diagnosis = await _diagnoser.FinishAsync(log, 3, stopped: false, Token);

        Assert.Equal(DiagnosisIds.GpuFeatures, diagnosis?.Id);
        var lines = await File.ReadAllLinesAsync(log, Token);
        Assert.Equal(["last line without newline", "[ps5kitsune] exited with code 3 (0x00000003)"], lines[^2..]);
    }

    [Fact]
    public async Task Finishing_a_stopped_game_writes_the_stopped_line_and_diagnoses_nothing()
    {
        var log = Log("Vulkan: buffer device address and shaderInt64 are required\n");

        var diagnosis = await _diagnoser.FinishAsync(log, unchecked((int)0xC0000005), stopped: true, Token);

        Assert.Null(diagnosis);
        Assert.Equal("[ps5kitsune] stopped by the user (exit code 3221225477)", (await File.ReadAllLinesAsync(log, Token))[^1]);
    }

    [Fact]
    public void Exit_lines_print_the_code_unsigned_and_in_hex()
    {
        Assert.Equal("[ps5kitsune] exited with code 3221225477 (0xC0000005)", ExitLines.Exited(unchecked((int)0xC0000005)));
    }

    [Fact]
    public async Task A_missing_log_gives_no_diagnosis()
    {
        Assert.Null(await _diagnoser.DiagnoseAsync(_temp.Combine("missing.log"), 1, Token));
    }

    [Fact]
    public async Task The_matched_line_is_trimmed_to_300_characters()
    {
        var diagnosis = await _diagnoser.DiagnoseAsync(Log("  this is not implemented " + new string('x', 400) + "\n"), 1, Token);

        Assert.Equal(300, diagnosis?.Line?.Length);
        Assert.StartsWith("this is not implemented", diagnosis?.Line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_rule_with_an_id_this_app_does_not_know_is_skipped()
    {
        var profile = ProfileLoader.Load(new MemoryStream(Encoding.UTF8.GetBytes("""
            {
              "schemaVersion": 1, "id": "future", "source": { "kind": "release", "tag": "v1" },
              "diagnostics": { "rules": [ { "id": "future-thing", "log": [ "boom" ] }, { "id": "gpu-features", "log": [ "boom" ] } ] }
            }
            """)));
        var diagnoser = new CrashDiagnoser(profile, NullLogger<CrashDiagnoser>.Instance);

        Assert.Equal(DiagnosisIds.GpuFeatures, (await diagnoser.DiagnoseAsync(Log("boom\n"), 1, Token))?.Id);
    }

    public void Dispose() => _temp.Dispose();
}
