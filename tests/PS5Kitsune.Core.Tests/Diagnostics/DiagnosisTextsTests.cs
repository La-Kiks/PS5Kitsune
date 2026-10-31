using PS5Kitsune.Core.Diagnostics;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Tests.Diagnostics;

public sealed class DiagnosisTextsTests
{
    private static Diagnosis Of(string id, string? capture = null, string? value = null, uint? exitCode = 1, string? line = "the line") =>
        new(id, capture is null ? new Dictionary<string, string>() : new Dictionary<string, string> { [capture] = value! }, line, line is null ? null : 2, exitCode, "C:\\G\\logs\\1.log");

    [Theory]
    [InlineData(DiagnosisIds.VulkanMissing, "Vulkan could not be loaded", "AnyPS5 could not load the Vulkan loader or a Vulkan driver. Run ps5kitsune check: it shows the command that installs them.")]
    [InlineData(DiagnosisIds.BdaAccess, "The GPU could not reach some memory", "GPU stores failed (BDA access failed), usually because /dev/udmabuf could not be used. Run ps5kitsune check: it tells whether this kernel provides /dev/udmabuf and whether you can open it.")]
    [InlineData(DiagnosisIds.UdmabufAccess, "No access to /dev/udmabuf", "AnyPS5 could not open /dev/udmabuf, so it copied shared memory instead of importing it. Give your user access: sudo usermod -aG kvm $USER, then log out and in again.")]
    public void The_linux_diagnoses_explain_without_a_button(string id, string title, string explanation)
    {
        var text = DiagnosisTexts.For(Of(id), new GameSettings());

        Assert.Equal(title, text.Title);
        Assert.Equal(explanation, text.Explanation);
        Assert.Equal(DiagnosisAction.None, text.Action);
        Assert.Null(text.ActionText);
    }

    [Fact]
    public void A_missing_shared_library_is_named()
    {
        var text = DiagnosisTexts.For(Of(DiagnosisIds.SharedLibraryMissing, "library", "libpulse.so.0"), new GameSettings());

        Assert.Equal("A system library is missing", text.Title);
        Assert.Equal("libpulse.so.0 is not installed. Install the package that provides it with your distribution's package manager.", text.Explanation);
        Assert.Equal(DiagnosisAction.None, text.Action);
    }

    [Fact]
    public void A_missing_dll_names_the_module_file_and_offers_repair_libs()
    {
        var text = DiagnosisTexts.For(Of(DiagnosisIds.LibsMissingDll, "module", "C:\\G\\libs\\libSceAudioOut.prx"), new GameSettings());

        Assert.Equal("A library could not load", text.Title);
        Assert.Equal("libSceAudioOut.prx needs a DLL that is missing from the game's libs folder or from Windows.", text.Explanation);
        Assert.Equal(DiagnosisAction.RepairLibs, text.Action);
        Assert.Equal("Repair libs", text.ActionText);
    }

    [Fact]
    public void A_refused_setting_variable_offers_to_reset_its_setting()
    {
        var text = DiagnosisTexts.For(Of(DiagnosisIds.InvalidSetting, "variable", "ANYPS5_LANGUAGE"), new GameSettings());

        Assert.Equal("ANYPS5_LANGUAGE has a value AnyPS5 refuses.", text.Explanation);
        Assert.Equal(DiagnosisAction.ResetSetting, text.Action);
        Assert.Equal("Reset game language", text.ActionText);
    }

    [Fact]
    public void A_variable_no_setting_sets_has_no_action()
    {
        var text = DiagnosisTexts.For(Of(DiagnosisIds.InvalidSetting, "variable", "ANYPS5_SYSTEM_FONTS"), new GameSettings());

        Assert.Equal(DiagnosisAction.None, text.Action);
        Assert.Null(text.ActionText);
    }

    [Fact]
    public void With_cpu_rendering_on_a_missing_llvmpipe_device_offers_to_turn_it_off()
    {
        var text = DiagnosisTexts.For(Of(DiagnosisIds.InvalidSetting, "variable", "anyps5_gpu"), new GameSettings { Lavapipe = true });

        Assert.Equal("A setting has a value AnyPS5 refuses", text.Title);
        Assert.Equal("CPU rendering sets ANYPS5_GPU=llvmpipe, and Vulkan found no llvmpipe device.", text.Explanation);
        Assert.Equal(DiagnosisAction.TurnOffLavapipe, text.Action);
        Assert.Equal("Turn off CPU rendering", text.ActionText);
    }

    [Fact]
    public void Without_cpu_rendering_a_refused_gpu_keeps_the_reset_action()
    {
        var text = DiagnosisTexts.For(Of(DiagnosisIds.InvalidSetting, "variable", "ANYPS5_GPU"), new GameSettings { Gpu = "nvidia" });

        Assert.Equal("ANYPS5_GPU has a value AnyPS5 refuses.", text.Explanation);
        Assert.Equal(DiagnosisAction.ResetSetting, text.Action);
        Assert.Equal("Reset GPU", text.ActionText);
    }

    [Fact]
    public void Missing_gpu_features_with_cpu_rendering_already_on_offer_report()
    {
        var on = DiagnosisTexts.For(Of(DiagnosisIds.GpuFeatures), new GameSettings { Lavapipe = true });
        var off = DiagnosisTexts.For(Of(DiagnosisIds.GpuFeatures), new GameSettings());

        Assert.Equal("No GPU here supports AnyPS5", on.Title);
        Assert.Equal("The graphics driver lacks buffer device address or shaderInt64, even with CPU rendering on.", on.Explanation);
        Assert.Equal(DiagnosisAction.Report, on.Action);
        Assert.Equal("Report", on.ActionText);
        Assert.Equal(DiagnosisAction.TurnOnLavapipe, off.Action);
        Assert.Equal("Turn on CPU rendering", off.ActionText);
    }

    [Fact]
    public void An_unexplained_exit_shows_the_code_in_hex_and_offers_report()
    {
        var text = DiagnosisTexts.For(Of(DiagnosisIds.Unexplained, exitCode: 0xC0000005, line: null), new GameSettings());

        Assert.Equal("AnyPS5 stopped with an error it does not explain yet (exit code 0xC0000005).", text.Explanation);
        Assert.Equal(DiagnosisAction.Report, text.Action);
    }

    [Fact]
    public void The_cli_lines_give_the_explanation_the_line_and_the_fix()
    {
        var lines = DiagnosisTexts.Lines(Of(DiagnosisIds.LibsMissingDll, "module", "C:\\G\\libs\\libSceAudioOut.prx"), new GameSettings(), "DOOM", "PPSA99666");

        Assert.Equal(
            [
                "A library could not load: libSceAudioOut.prx needs a DLL that is missing from the game's libs folder or from Windows.",
                "Line 2: the line",
                "Fix: launch DOOM from the app; when it stops, choose Repair libs.",
            ],
            lines);
    }

    [Fact]
    public void The_report_fix_names_the_diagnose_command_and_a_cpu_error_has_no_fix()
    {
        Assert.Equal(
            "Fix: run ps5kitsune diagnose PPSA99666 --report and paste the text in a new Game bring-up thread.",
            DiagnosisTexts.Lines(Of(DiagnosisIds.NotImplemented), new GameSettings(), "DOOM", "PPSA99666")[^1]);
        Assert.Equal(2, DiagnosisTexts.Lines(Of(DiagnosisIds.CpuTooOld), new GameSettings(), "DOOM", "PPSA99666").Count);
    }

    [Fact]
    public void Resetting_a_setting_restores_only_its_default()
    {
        var reset = SettingVariables.Find("anyps5_language")!.Reset(new GameSettings { Language = 5, Overlay = true });

        Assert.Equal(new GameSettings { Overlay = true }, reset);
        Assert.Null(SettingVariables.Find("ANYPS5_SYSTEM_FONTS"));
    }
}
