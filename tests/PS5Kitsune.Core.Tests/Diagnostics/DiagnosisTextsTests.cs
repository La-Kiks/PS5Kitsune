using PS5Kitsune.Core.Diagnostics;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Tests.Diagnostics;

public sealed class DiagnosisTextsTests
{
    private static Diagnosis Of(string id, string? capture = null, string? value = null, uint? exitCode = 1, string? line = "the line") =>
        new(id, capture is null ? new Dictionary<string, string>() : new Dictionary<string, string> { [capture] = value! }, line, line is null ? null : 2, exitCode, "C:\\G\\logs\\1.log");

    [Fact]
    public void A_missing_dll_names_the_module_file_and_offers_repair_libs()
    {
        var text = DiagnosisTexts.For(Of(DiagnosisIds.LibsMissingDll, "module", "C:\\G\\libs\\libSceAudioOut.prx"));

        Assert.Equal("A library could not load", text.Title);
        Assert.Equal("libSceAudioOut.prx needs a DLL that is missing from the game's libs folder.", text.Explanation);
        Assert.Equal(DiagnosisAction.RepairLibs, text.Action);
        Assert.Equal("Repair libs", text.ActionText);
    }

    [Fact]
    public void A_refused_setting_variable_offers_to_reset_its_setting()
    {
        var text = DiagnosisTexts.For(Of(DiagnosisIds.InvalidSetting, "variable", "ANYPS5_LANGUAGE"));

        Assert.Equal("ANYPS5_LANGUAGE has a value AnyPS5 refuses.", text.Explanation);
        Assert.Equal(DiagnosisAction.ResetSetting, text.Action);
        Assert.Equal("Reset game language", text.ActionText);
    }

    [Fact]
    public void A_variable_no_setting_sets_has_no_action()
    {
        var text = DiagnosisTexts.For(Of(DiagnosisIds.InvalidSetting, "variable", "ANYPS5_SYSTEM_FONTS"));

        Assert.Equal(DiagnosisAction.None, text.Action);
        Assert.Null(text.ActionText);
    }

    [Fact]
    public void An_unexplained_exit_shows_the_code_in_hex_and_offers_report()
    {
        var text = DiagnosisTexts.For(Of(DiagnosisIds.Unexplained, exitCode: 0xC0000005, line: null));

        Assert.Equal("AnyPS5 stopped with an error it does not explain yet (exit code 0xC0000005).", text.Explanation);
        Assert.Equal(DiagnosisAction.Report, text.Action);
    }

    [Fact]
    public void The_cli_lines_give_the_explanation_the_line_and_the_fix()
    {
        var lines = DiagnosisTexts.Lines(Of(DiagnosisIds.LibsMissingDll, "module", "C:\\G\\libs\\libSceAudioOut.prx"), "DOOM", "PPSA99666");

        Assert.Equal(
            [
                "A library could not load: libSceAudioOut.prx needs a DLL that is missing from the game's libs folder.",
                "Line 2: the line",
                "Fix: in the app, open DOOM and choose Repair libs.",
            ],
            lines);
    }

    [Fact]
    public void The_report_fix_names_the_diagnose_command_and_a_cpu_error_has_no_fix()
    {
        Assert.Equal(
            "Fix: run ps5kitsune diagnose PPSA99666 --report and paste the text in a new Game bring-up thread.",
            DiagnosisTexts.Lines(Of(DiagnosisIds.NotImplemented), "DOOM", "PPSA99666")[^1]);
        Assert.Equal(2, DiagnosisTexts.Lines(Of(DiagnosisIds.CpuTooOld), "DOOM", "PPSA99666").Count);
    }

    [Fact]
    public void Resetting_a_setting_restores_only_its_default()
    {
        var reset = SettingVariables.Find("anyps5_language")!.Reset(new GameSettings { Language = 5, Overlay = true });

        Assert.Equal(new GameSettings { Overlay = true }, reset);
        Assert.Null(SettingVariables.Find("ANYPS5_SYSTEM_FONTS"));
    }
}
