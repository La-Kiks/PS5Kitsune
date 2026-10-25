using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Diagnostics;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.App.Tests;

public sealed class DiagnosisViewModelTests : IDisposable
{
    private static readonly Uri ReportUrl = new("https://github.com/boykopovar/AnyPS5/discussions/categories/game-bring-up");

    private readonly TestLibrary _library = new();

    private static Diagnosis Of(string id, string? variable = null) => new(
        id,
        variable is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["variable"] = variable },
        "the line",
        4,
        1,
        "missing.log");

    private async Task<DiagnosisViewModel> OpenAsync(Diagnosis diagnosis)
    {
        var game = await _library.AddAsync("PPSA99666", "DOOM");
        return new DiagnosisViewModel(diagnosis, game, "DOOM", _library.Player, _library.Platform, _library.Diagnosis(ReportUrl));
    }

    private GameSettings Saved() => _library.Library.Find("PPSA99666").Record.Settings;

    [Fact]
    public async Task Shows_the_explanation_the_line_and_the_action()
    {
        var panel = await OpenAsync(Of(DiagnosisIds.GpuFeatures));

        Assert.Equal("No GPU here supports AnyPS5", panel.Title);
        Assert.Equal("The graphics driver lacks buffer device address or shaderInt64. CPU rendering works on any PC, more slowly.", panel.Explanation);
        Assert.Equal("Line 4: the line", panel.LineText);
        Assert.Equal("Turn on CPU rendering", panel.ActionText);
        Assert.True(panel.FixCommand.CanExecute(null));
    }

    [Fact]
    public async Task Turn_on_cpu_rendering_saves_lavapipe()
    {
        var panel = await OpenAsync(Of(DiagnosisIds.GpuFeatures));

        await panel.FixCommand.ExecuteAsync(null);

        Assert.True(Saved().Lavapipe);
        Assert.Equal("CPU rendering is on. Launch again to download Mesa and start the game.", panel.Result);
    }

    [Fact]
    public async Task Reset_puts_back_only_the_refused_setting()
    {
        var panel = await OpenAsync(Of(DiagnosisIds.InvalidSetting, "ANYPS5_LANGUAGE"));
        await _library.Player.SaveSettingsAsync("PPSA99666", new GameSettings { Language = 5, Overlay = true }, TestContext.Current.CancellationToken);

        await panel.FixCommand.ExecuteAsync(null);

        Assert.Equal(new GameSettings { Overlay = true }, Saved());
        Assert.Equal("Game language reset. Launch again to check.", panel.Result);
    }

    [Fact]
    public async Task Repair_libs_copies_them_from_the_install()
    {
        var source = _library.Paths.For(AnyPS5Mode.Release).Libs;
        Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "libwinpthread-1.dll"), "dll", TestContext.Current.CancellationToken);
        var panel = await OpenAsync(Of(DiagnosisIds.LibsMissingDll));

        await panel.FixCommand.ExecuteAsync(null);

        Assert.True(File.Exists(Path.Combine(_library.Game("PPSA99666").Libs, "libwinpthread-1.dll")));
        Assert.Equal("Libs repaired. Launch again to check.", panel.Result);
        Assert.False(panel.IsFixing);
    }

    [Fact]
    public async Task Repair_libs_is_refused_while_the_game_runs()
    {
        var panel = await OpenAsync(Of(DiagnosisIds.LibsMissingDll));
        await _library.Player.LaunchAsync("PPSA99666", TestContext.Current.CancellationToken);

        await panel.FixCommand.ExecuteAsync(null);

        Assert.Equal("DOOM is running. Stop it first.", panel.Result);
    }

    [Fact]
    public async Task Repair_libs_after_an_update_says_to_reconvert()
    {
        var panel = await OpenAsync(Of(DiagnosisIds.LibsNotDll));
        _library.Installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, TestLibrary.Release with { Version = "v0.1.3" });

        await panel.FixCommand.ExecuteAsync(null);

        Assert.Equal("The release install has changed since DOOM was converted. Reconvert it instead.", panel.Result);
    }

    [Fact]
    public async Task Report_copies_the_text_then_opens_the_bring_up_page()
    {
        var panel = await OpenAsync(Of(DiagnosisIds.Unexplained));

        await panel.FixCommand.ExecuteAsync(null);

        Assert.Contains("Diagnosis: unexplained", Assert.Single(_library.Dialogs.Copied), StringComparison.Ordinal);
        Assert.Equal([ReportUrl.AbsoluteUri], _library.Platform.OpenedPaths);
        Assert.Equal("Report copied. Paste it in a new thread for PPSA99666.", panel.Result);
    }

    [Fact]
    public async Task A_failed_copy_points_to_the_cli()
    {
        var panel = await OpenAsync(Of(DiagnosisIds.NotImplemented));
        _library.Dialogs.CopyFailure = new InvalidOperationException("No clipboard");

        await panel.FixCommand.ExecuteAsync(null);

        Assert.Equal("Could not copy the report: No clipboard. Use ps5kitsune diagnose PPSA99666 --report.", panel.Result);
        Assert.Empty(_library.Platform.OpenedPaths);
    }

    [Fact]
    public async Task Open_page_file_settings_opens_them_or_says_why_not()
    {
        var panel = await OpenAsync(Of(DiagnosisIds.CommitLimit));

        await panel.FixCommand.ExecuteAsync(null);
        Assert.Equal([CheckFix.OpenPerformanceSettings], _library.Platform.OpenedFixes);
        Assert.Null(panel.Result);

        _library.Platform.OpenFailure = new InvalidOperationException("blocked");
        await panel.FixCommand.ExecuteAsync(null);
        Assert.Equal("Could not open the page file settings: blocked", panel.Result);
    }

    [Fact]
    public async Task An_unsupported_cpu_has_no_action()
    {
        var panel = await OpenAsync(Of(DiagnosisIds.CpuTooOld));

        Assert.False(panel.HasAction);
        Assert.False(panel.FixCommand.CanExecute(null));
    }

    public void Dispose() => _library.Dispose();
}
