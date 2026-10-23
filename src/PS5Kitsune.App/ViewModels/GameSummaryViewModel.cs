using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.App.ViewModels;

/// <summary>What was read from the game, before anything is copied, and the Intel option. Owns the prepared game until the add starts.</summary>
public sealed partial class GameSummaryViewModel(PreparedGame game, bool cpuIsIntel, IPlatform platform, Navigator navigator, IPages pages)
    : ObservableObject, IPreparedGameOwner
{
    private bool _handedOver;

    public string Name => game.Metadata?.Name ?? "Unknown game";

    public string TitleId => game.Metadata?.TitleId ?? "";

    /// <summary>Full path of icon0.png; the view loads it into memory.</summary>
    public string? IconPath => game.IconPath;

    public string SizeText => string.Create(CultureInfo.InvariantCulture, $"{game.Validation.SizeBytes / 1048576.0:0.0} MB");

    public string ModulesText => game.Validation.ModuleDirectories.Count switch
    {
        0 => "No module folder",
        1 => "1 module folder",
        var count => string.Create(CultureInfo.InvariantCulture, $"{count} module folders"),
    };

    public string BinariesText
    {
        get
        {
            var self = game.Binaries.Where(binary => binary.Kind == BinaryKind.Self).ToList();
            if (self.Count == 0)
            {
                return "No SELF file.";
            }

            var start = self.Count == 1 ? "1 file is" : string.Create(CultureInfo.InvariantCulture, $"{self.Count} files are");
            var eboot = self.Any(binary => binary.RelativePath == GameSourceResolver.ExecutableName) && self.Count > 1 ? ", eboot.bin among them" : "";
            return $"{start} SELF and will be unwrapped{eboot}.";
        }
    }

    public IReadOnlyList<string> BlockingIssues { get; } =
        [.. game.Validation.Issues.Where(issue => issue.Severity == IssueSeverity.Blocking).Select(issue => issue.Message)];

    public IReadOnlyList<string> Warnings { get; } = [.. game.Validation.Warnings.Select(issue => issue.Message)];

    public string? AlreadyAddedText => game.AlreadyAdded ? $"{TitleId} is already in your games ({game.GameDirectory})." : null;

    public string? ResumeText => game.Resumes
        ? "A previous add of this game stopped; adding it resumes where it stopped. The Intel option only applies if the conversion has not run yet."
        : null;

    [ObservableProperty]
    public partial bool ToIntel { get; set; } = cpuIsIntel;

    [ObservableProperty]
    public partial string? OpenError { get; private set; }

    public bool CanAdd => !game.IsBlocked && !game.AlreadyAdded;

    public Task ReleaseAsync() => _handedOver ? Task.CompletedTask : game.DisposeAsync().AsTask();

    /// <summary>The Adding page owns the game from here.</summary>
    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void Add()
    {
        _handedOver = true;
        navigator.GoTo(pages.AddGame(game, ToIntel));
    }

    [RelayCommand]
    private async Task BackAsync()
    {
        await game.DisposeAsync().ConfigureAwait(true);
        navigator.GoBack();
    }

    [RelayCommand]
    private void OpenFolder() => OpenError = game.GameDirectory is { } folder ? PathOpener.TryOpen(platform, folder) : null;
}
