using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Diagnostics;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.App.ViewModels;

/// <summary>The Game page's panel after an exit with a known error: the explanation, the matched line and its fix. See the 7c spec, "Game page".</summary>
public sealed partial class DiagnosisViewModel : ObservableObject
{
    private readonly Diagnosis _diagnosis;
    private readonly GameLayout _game;
    private readonly string _name;
    private readonly GamePlayer _player;
    private readonly IPlatform _platform;
    private readonly DiagnosisServices _services;
    private readonly DiagnosisText _text;

    public DiagnosisViewModel(Diagnosis diagnosis, GameLayout game, string name, GameSettings settings, GamePlayer player, IPlatform platform, DiagnosisServices services)
    {
        _diagnosis = diagnosis;
        _game = game;
        _name = name;
        _player = player;
        _platform = platform;
        _services = services;
        _text = DiagnosisTexts.For(diagnosis, settings);
        LineText = diagnosis.Line is { } line ? string.Create(CultureInfo.InvariantCulture, $"Line {diagnosis.LineNumber}: {line}") : null;
    }

    public string Title => _text.Title;

    public string Explanation => _text.Explanation;

    /// <summary>"Line n: …"; null when only the exit code matched.</summary>
    public string? LineText { get; }

    public string? ActionText => _text.ActionText;

    public bool HasAction => _text.Action != DiagnosisAction.None;

    /// <summary>What the fix did, or why it failed; null before it ran, and after a fix that only opens something.</summary>
    [ObservableProperty]
    public partial string? Result { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FixCommand))]
    public partial bool IsFixing { get; private set; }

    private bool CanFix => HasAction && !IsFixing;

    [RelayCommand(CanExecute = nameof(CanFix))]
    private async Task FixAsync()
    {
        IsFixing = true;
        Result = null;
        try
        {
            Result = await RunFixAsync().ConfigureAwait(true);
        }
        finally
        {
            IsFixing = false;
        }
    }

    private async Task<string?> RunFixAsync()
    {
        switch (_text.Action)
        {
            case DiagnosisAction.RepairLibs:
                return await RepairLibsAsync().ConfigureAwait(true);
            case DiagnosisAction.TurnOnLavapipe:
                return await ChangeSettingsAsync(
                    settings => settings with { Lavapipe = true },
                    _player.LavapipeHowToGet is { } how ? $"CPU rendering is on. {how} Launch again to start the game." : "CPU rendering is on. Launch again to start the game.").ConfigureAwait(true);
            case DiagnosisAction.TurnOffLavapipe:
                return await ChangeSettingsAsync(settings => settings with { Lavapipe = false }, "CPU rendering is off. Launch again to check.").ConfigureAwait(true);
            case DiagnosisAction.ResetSetting when SettingVariables.Find(_diagnosis.Capture("variable")) is { } setting:
                var label = char.ToUpperInvariant(setting.Label[0]) + setting.Label[1..];
                return await ChangeSettingsAsync(setting.Reset, $"{label} reset. Launch again to check.").ConfigureAwait(true);
            case DiagnosisAction.OpenPageFileSettings:
                return Open("the page file settings", () => _platform.OpenFix(CheckFix.OpenPerformanceSettings));
            case DiagnosisAction.OpenNvidiaDrivers:
                return Open("the NVIDIA drivers page", () => _platform.OpenPath(DiagnosisTexts.NvidiaDriversUrl));
            case DiagnosisAction.OpenFolder:
                return PathOpener.TryOpen(_platform, _game.Root);
            case DiagnosisAction.Report:
                return await ReportAsync().ConfigureAwait(true);
            default:
                return null;
        }
    }

    private async Task<string> RepairLibsAsync()
    {
        try
        {
            await Task.Run(() => _services.Libs.RepairAsync(_game.TitleId, CancellationToken.None)).ConfigureAwait(true);
            return "Libs repaired. Launch again to check.";
        }
        catch (LibsSourceChangedException exception)
        {
            return exception.Message;
        }
        catch (Exception exception) when (exception is GameRunningException or GameBusyException)
        {
            return Refused(exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or GameNotFoundException or InvalidGameRecordException)
        {
            return $"Could not repair libs: {exception.Message}";
        }
    }

    private async Task<string> ChangeSettingsAsync(Func<GameSettings, GameSettings> change, string done)
    {
        try
        {
            await Task.Run(() => _player.UpdateSettingsAsync(_game.TitleId, change, CancellationToken.None)).ConfigureAwait(true);
            return done;
        }
        catch (Exception exception) when (exception is GameRunningException or GameBusyException)
        {
            return Refused(exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or GameNotFoundException or InvalidGameRecordException)
        {
            return $"Could not save the settings of {_name}: {exception.Message}";
        }
    }

    private async Task<string> ReportAsync()
    {
        var titleId = _game.TitleId;
        try
        {
            var text = await Task.Run(() => _services.Reporter.BuildAsync(titleId, _diagnosis.LogPath, _diagnosis, CancellationToken.None)).ConfigureAwait(true);
            await _services.Dialogs.CopyTextAsync(text).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return $"Could not copy the report: {exception.Message}. Use ps5kitsune diagnose {titleId} --report.";
        }

        if (_services.ReportUrl is { } url && Open("the Game bring-up page", () => _platform.OpenPath(url.AbsoluteUri)) is { } failure)
        {
            return failure;
        }

        return $"Report copied. Paste it in a new thread for {titleId}.";
    }

    private string Refused(Exception exception) => exception is GameRunningException
        ? $"{_name} is running. Stop it first."
        : $"{_name} is busy with another action. Try again.";

    private static string? Open(string what, Action open)
    {
        try
        {
            open();
            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return $"Could not open {what}: {exception.Message}";
        }
    }
}
