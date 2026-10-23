using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.App.ViewModels;

/// <summary>The audit verdict of the game just added, with what AnyPS5 does not implement yet.</summary>
public sealed partial class AddResultViewModel(GameAdded added, IPlatform platform, Navigator navigator, IPages pages) : ObservableObject
{
    public string Title => $"{added.Record.Name} is added";

    public AuditVerdict Verdict => added.Audit.Verdict;

    public bool IsGreen => Verdict == AuditVerdict.Green;

    public bool IsOrange => Verdict == AuditVerdict.Orange;

    public bool IsRed => Verdict == AuditVerdict.Red;

    public string VerdictText => Verdict switch
    {
        AuditVerdict.Green => "Green: the game can load.",
        AuditVerdict.Orange => "Orange: the game loads but will stop if it calls an unimplemented function.",
        _ => "Red: not playable yet; wait for an AnyPS5 update.",
    };

    public string CountsText => string.Create(
        CultureInfo.InvariantCulture, $"{added.Audit.Implemented} implemented, {added.Audit.Stub} stubs, {added.Audit.Absent} absent");

    public string? MissingLibrariesText => added.Audit.MissingLibraries.Count == 0
        ? null
        : "Missing libraries: " + string.Join(", ", added.Audit.MissingLibraries.Keys);

    public bool HasUnresolved => added.Audit.Unresolved.Count > 0;

    public string UnresolvedHeader => string.Create(CultureInfo.InvariantCulture, $"Show the unresolved functions ({added.Audit.Unresolved.Count})");

    public IReadOnlyList<string> UnresolvedLines { get; } = [.. added.Audit.Unresolved.Select(Line)];

    public IReadOnlyList<string> Warnings { get; } = [.. added.Warnings.Select(issue => issue.Message)];

    [ObservableProperty]
    public partial string? OpenError { get; private set; }

    private static string Line(UnresolvedImport import)
    {
        var kind = import.Class == AuditClass.Stub ? "stub" : "absent";
        return import.Where.Length == 0 ? $"{import.Name} ({kind})" : $"{import.Name} — {import.Where} ({kind})";
    }

    [RelayCommand]
    private void OpenFolder() => OpenError = PathOpener.TryOpen(platform, added.GameDirectory);

    [RelayCommand]
    private void AddAnother()
    {
        navigator.GoHome(pages.Home());
        navigator.GoTo(pages.ChooseGame());
    }

    [RelayCommand]
    private void BackHome() => navigator.GoHome(pages.Home());
}
