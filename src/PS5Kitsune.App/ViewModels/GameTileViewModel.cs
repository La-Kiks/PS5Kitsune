using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.App.ViewModels;

/// <summary>
/// A game in the Library, built on a worker thread (it looks at the logs folder). Its menu binds to the Library's commands through
/// <see cref="Owner"/>: a flyout is outside the tile's visual tree, so a $parent binding cannot reach the page.
/// </summary>
public sealed class GameTileViewModel(LibraryGame entry, bool needsReconvert, LibraryViewModel owner)
{
    public LibraryGame Entry { get; } = entry;

    public LibraryViewModel Owner { get; } = owner;

    public string TitleId => Entry.Record.TitleId;

    public string Name => Entry.Record.Name;

    /// <summary>icon0.png's full path; null when the game has none.</summary>
    public string? IconPath => Entry.Record.Icon is { } icon ? Path.Combine(Entry.Game.Root, icon) : null;

    public bool IsGreen => Entry.Record.Audit.Verdict == AuditVerdict.Green;

    public bool IsOrange => Entry.Record.Audit.Verdict == AuditVerdict.Orange;

    public bool IsRed => Entry.Record.Audit.Verdict == AuditVerdict.Red;

    public bool NeedsReconvert { get; } = needsReconvert;

    public bool IsRunning => Entry.Running is not null;

    public DateTimeOffset? LastPlayedAt => Entry.Record.LastPlayedAt;

    public bool HasLog { get; } = Directory.Exists(entry.Game.Logs) && Directory.EnumerateFiles(entry.Game.Logs, "*.log").Any();
}
