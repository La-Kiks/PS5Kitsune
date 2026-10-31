using System.Globalization;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Launch;

namespace PS5Kitsune.Core.Diagnostics;

/// <summary>What the Game page offers for a diagnosis. See the 7c spec, "Game page".</summary>
public enum DiagnosisAction
{
    None,
    RepairLibs,
    OpenPageFileSettings,
    TurnOnLavapipe,
    TurnOffLavapipe,
    OpenNvidiaDrivers,
    OpenFolder,
    ResetSetting,
    Report,
}

/// <param name="ActionText">The button's text; null with <see cref="DiagnosisAction.None"/>.</param>
public sealed record DiagnosisText(string Title, string Explanation, DiagnosisAction Action, string? ActionText);

/// <summary>Each diagnosis id's title, explanation and action, shared by the app and the CLI.</summary>
public static class DiagnosisTexts
{
    public const string NvidiaDriversUrl = "https://www.nvidia.com/en-us/drivers/";

    private const string SettingTitle = "A setting has a value AnyPS5 refuses";

    public static DiagnosisText For(Diagnosis diagnosis, GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(diagnosis);
        ArgumentNullException.ThrowIfNull(settings);
        var module = FileName(diagnosis.Capture("module")) ?? "A library";
        return diagnosis.Id switch
        {
            DiagnosisIds.LibsMissingDll => new(
                "A library could not load", $"{module} needs a DLL that is missing from the game's libs folder or from Windows.", DiagnosisAction.RepairLibs, "Repair libs"),
            DiagnosisIds.LibsNotDll => new(
                "A file in libs is not a Windows DLL",
                $"{module} is not a Windows library. Repair libs copies the folder again from the AnyPS5 install.",
                DiagnosisAction.RepairLibs,
                "Repair libs"),
            DiagnosisIds.CommitLimit => new(
                "Windows ran out of memory to commit",
                "The page file plus RAM is too small for this game. Enlarge the page file, or close other programs such as the browser.",
                DiagnosisAction.OpenPageFileSettings,
                "Open page file settings"),
            DiagnosisIds.GpuFeatures => GpuFeatures(settings),
            DiagnosisIds.NvidiaShaderCompiler => new(
                "Known NVIDIA driver crash",
                "NVIDIA's shader compiler (nvgpucomp64.dll) crashed. Driver 610.47 is known to do this.",
                DiagnosisAction.OpenNvidiaDrivers,
                "Open NVIDIA drivers page"),
            DiagnosisIds.CpuTooOld => new("This CPU is not supported", "The CPU lacks instructions AnyPS5 needs.", DiagnosisAction.None, null),
            DiagnosisIds.ParamJson => new(
                "The game's metadata is missing",
                @"sce_sys\param.json is missing or damaged. Remove the game and add it again from its original folder.",
                DiagnosisAction.OpenFolder,
                "Open folder"),
            DiagnosisIds.InvalidSetting => Setting(diagnosis.Capture("variable"), settings),
            DiagnosisIds.NotImplemented => new(
                "AnyPS5 does not handle this game yet", "The game uses a feature AnyPS5 has not implemented.", DiagnosisAction.Report, "Report"),
            DiagnosisIds.VulkanMissing => new(
                "Vulkan could not be loaded",
                "AnyPS5 could not load the Vulkan loader or a Vulkan driver. Run ps5kitsune check: it shows the command that installs them.",
                DiagnosisAction.None,
                null),
            DiagnosisIds.SharedLibraryMissing => new(
                "A system library is missing",
                $"{diagnosis.Capture("library") ?? "A library"} is not installed. Install the package that provides it with your distribution's package manager.",
                DiagnosisAction.None,
                null),
            DiagnosisIds.BdaAccess => new(
                "The GPU could not reach some memory",
                "GPU stores failed (BDA access failed), usually because /dev/udmabuf could not be used. Run ps5kitsune check: it tells whether this kernel provides /dev/udmabuf and whether you can open it.",
                DiagnosisAction.None,
                null),
            DiagnosisIds.UdmabufAccess => new(
                "No access to /dev/udmabuf",
                "AnyPS5 could not open /dev/udmabuf, so it copied shared memory instead of importing it. Give your user access: sudo usermod -aG kvm $USER, then log out and in again.",
                DiagnosisAction.None,
                null),
            _ => new(
                "The game stopped with an error",
                $"AnyPS5 stopped with an error it does not explain yet (exit code {Code(diagnosis.ExitCode)}).",
                DiagnosisAction.Report,
                "Report"),
        };
    }

    /// <summary>What `run` and `diagnose` print: the title and explanation, the matched line, then the fix in words when there is one.</summary>
    public static IReadOnlyList<string> Lines(Diagnosis diagnosis, GameSettings settings, string name, string titleId)
    {
        var text = For(diagnosis, settings);
        var lines = new List<string> { $"{text.Title}: {text.Explanation}" };
        if (diagnosis.Line is { } line)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"Line {diagnosis.LineNumber}: {line}"));
        }

        var fix = text.Action switch
        {
            DiagnosisAction.RepairLibs or DiagnosisAction.TurnOnLavapipe or DiagnosisAction.TurnOffLavapipe or DiagnosisAction.ResetSetting or DiagnosisAction.OpenPageFileSettings =>
                $"Fix: launch {name} from the app; when it stops, choose {text.ActionText}.",
            DiagnosisAction.OpenNvidiaDrivers => $"Fix: update the NVIDIA driver from {NvidiaDriversUrl}",
            DiagnosisAction.OpenFolder => "Fix: remove the game and add it again from its original folder.",
            DiagnosisAction.Report => $"Fix: run ps5kitsune diagnose {titleId} --report and paste the text in a new Game bring-up thread.",
            _ => null,
        };
        if (fix is not null)
        {
            lines.Add(fix);
        }

        return lines;
    }

    private static DiagnosisText GpuFeatures(GameSettings settings) => settings.Lavapipe
        ? new(
            "No GPU here supports AnyPS5",
            "The graphics driver lacks buffer device address or shaderInt64, even with CPU rendering on.",
            DiagnosisAction.Report,
            "Report")
        : new(
            "No GPU here supports AnyPS5",
            "The graphics driver lacks buffer device address or shaderInt64. CPU rendering works on any PC, more slowly.",
            DiagnosisAction.TurnOnLavapipe,
            "Turn on CPU rendering");

    private static DiagnosisText Setting(string? variable, GameSettings settings)
    {
        // CPU rendering sets ANYPS5_GPU itself: resetting the GPU setting would change nothing.
        if (settings.Lavapipe && string.Equals(variable, LaunchEnvironment.Gpu, StringComparison.OrdinalIgnoreCase))
        {
            return new(
                SettingTitle,
                "CPU rendering sets ANYPS5_GPU=llvmpipe, and Vulkan found no llvmpipe device.",
                DiagnosisAction.TurnOffLavapipe,
                "Turn off CPU rendering");
        }

        var explanation = $"{variable ?? "A setting"} has a value AnyPS5 refuses.";
        return SettingVariables.Find(variable) is { } setting
            ? new(SettingTitle, explanation, DiagnosisAction.ResetSetting, $"Reset {setting.Label}")
            : new(SettingTitle, explanation, DiagnosisAction.None, null);
    }

    // The log names Windows paths; Path.GetFileName would not split them on Linux, where the tests also run.
    private static string? FileName(string? path) => path?[(path.LastIndexOfAny(['\\', '/']) + 1)..];

    private static string Code(uint? code) => code is { } value ? ExitCodes.Describe(value) : "unknown";
}
