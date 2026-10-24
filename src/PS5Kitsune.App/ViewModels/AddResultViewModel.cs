using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.App.ViewModels;

/// <summary>The audit verdict of the game just added or reconverted, with what AnyPS5 does not implement yet.</summary>
public sealed partial class AddResultViewModel : ObservableObject
{
    private readonly AuditSummary _audit;
    private readonly string _gameDirectory;
    private readonly IPlatform _platform;
    private readonly Navigator _navigator;
    private readonly IPages _pages;

    public AddResultViewModel(GameAdded added, IPlatform platform, Navigator navigator, IPages pages)
        : this($"{added.Record.Name} is added", added.GameDirectory, added.Audit, added.Warnings, null, platform, navigator, pages)
    {
    }

    public AddResultViewModel(GameReconverted reconverted, IPlatform platform, Navigator navigator, IPages pages)
        : this($"{reconverted.Record.Name} reconverted", reconverted.GameDirectory, reconverted.Audit, [], Before(reconverted.PreviousAudit), platform, navigator, pages)
    {
    }

    private AddResultViewModel(
        string title, string gameDirectory, AuditSummary audit, IReadOnlyList<ValidationIssue> warnings, string? beforeText, IPlatform platform, Navigator navigator, IPages pages)
    {
        Title = title;
        _gameDirectory = gameDirectory;
        _audit = audit;
        BeforeText = beforeText;
        _platform = platform;
        _navigator = navigator;
        _pages = pages;
        UnresolvedLines = [.. audit.Unresolved.Select(Line)];
        Warnings = [.. warnings.Select(issue => issue.Message)];
    }

    public string Title { get; }

    public AuditVerdict Verdict => _audit.Verdict;

    public bool IsGreen => Verdict == AuditVerdict.Green;

    public bool IsOrange => Verdict == AuditVerdict.Orange;

    public bool IsRed => Verdict == AuditVerdict.Red;

    public string VerdictText => Verdict switch
    {
        AuditVerdict.Green => "Green: the game can load.",
        AuditVerdict.Orange => "Orange: the game loads but will stop if it calls an unimplemented function.",
        _ => "Red: not playable yet; wait for an AnyPS5 update.",
    };

    public string CountsText => string.Create(CultureInfo.InvariantCulture, $"{_audit.Implemented} implemented, {_audit.Stub} stubs, {_audit.Absent} absent");

    /// <summary>The counts before a Reconvert; null after an add.</summary>
    public string? BeforeText { get; }

    public string? MissingLibrariesText => _audit.MissingLibraries.Count == 0
        ? null
        : "Missing libraries: " + string.Join(", ", _audit.MissingLibraries.Keys);

    public bool HasUnresolved => _audit.Unresolved.Count > 0;

    public string UnresolvedHeader => string.Create(CultureInfo.InvariantCulture, $"Show the unresolved functions ({_audit.Unresolved.Count})");

    public IReadOnlyList<string> UnresolvedLines { get; }

    public IReadOnlyList<string> Warnings { get; }

    [ObservableProperty]
    public partial string? OpenError { get; private set; }

    private static string Before(AuditRecord audit) =>
        string.Create(CultureInfo.InvariantCulture, $"Before: {audit.Implemented} implemented, {audit.Stub} stubs, {audit.Absent} absent");

    private static string Line(UnresolvedImport import)
    {
        var kind = import.Class == AuditClass.Stub ? "stub" : "absent";
        return import.Where.Length == 0 ? $"{import.Name} ({kind})" : $"{import.Name} — {import.Where} ({kind})";
    }

    [RelayCommand]
    private void OpenFolder() => OpenError = PathOpener.TryOpen(_platform, _gameDirectory);

    [RelayCommand]
    private void AddAnother()
    {
        _navigator.GoHome(_pages.Library());
        _navigator.GoTo(_pages.ChooseGame());
    }

    [RelayCommand]
    private void BackToLibrary() => _navigator.GoHome(_pages.Library());
}
