using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.App.ViewModels;

/// <summary>What was read from the game, before anything is copied, and the Intel option. Owns the prepared game until the add starts.</summary>
/// <param name="conversionText">For an added game, whether it was converted with the AnyPS5 in use (<see cref="ConversionLine"/>).</param>
public sealed partial class GameSummaryViewModel(PreparedGame game, bool cpuIsIntel, IPlatform platform, Navigator navigator, IPages pages, string? conversionText = null)
    : ObservableObject, IPreparedGameOwner, IBusyPage
{
    private bool _handedOver;

    /// <summary>Back or Reconvert is releasing the prepared game; the sidebar stays locked meanwhile.</summary>
    [ObservableProperty]
    public partial bool IsLeaving { get; private set; }

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

    /// <summary>For an already added game, how its conversion compares with the AnyPS5 in use; null otherwise.</summary>
    public string? ConversionText { get; } = conversionText;

    public bool CanReconvert => game.AlreadyAdded && !game.IsDisposed;

    public static string ConversionLine(GameRecord record, AnyPS5InstallRecord? install)
    {
        ArgumentNullException.ThrowIfNull(record);
        var converted = ConversionLabels.Of(record.ConvertedWith);
        return install is null ? $"Converted with {converted}."
            : GameLibrary.NeedsReconversion(record, install) ? $"Converted with {converted}; the AnyPS5 in use is {ConversionLabels.Of(install)}."
            : "Converted with the AnyPS5 in use.";
    }

    public string? ResumeText => game.Resumes
        ? "A previous add of this game stopped; adding it resumes where it stopped. The Intel option only applies if the conversion has not run yet."
        : null;

    [ObservableProperty]
    public partial bool ToIntel { get; set; } = cpuIsIntel;

    [ObservableProperty]
    public partial string? OpenError { get; private set; }

    public bool CanAdd => !game.IsBlocked && !game.AlreadyAdded && !game.IsDisposed;

    private bool CanGoBack => !game.IsDisposed;

    public Task ReleaseAsync() => _handedOver ? Task.CompletedTask : game.DisposeAsync().AsTask();

    /// <summary>The Adding page owns the game from here.</summary>
    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void Add()
    {
        if (game.IsDisposed)
        {
            return;
        }

        _handedOver = true;
        navigator.GoTo(pages.AddGame(game, ToIntel));
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private async Task BackAsync()
    {
        // Disposal starts as soon as DisposeAsync is called, so both commands are blocked for the whole (possibly slow) cleanup.
        IsLeaving = true;
        try
        {
            var disposing = game.DisposeAsync();
            AddCommand.NotifyCanExecuteChanged();
            BackCommand.NotifyCanExecuteChanged();
            ReconvertCommand.NotifyCanExecuteChanged();
            await disposing.ConfigureAwait(true);
            navigator.GoBack();
        }
        finally
        {
            IsLeaving = false;
        }
    }

    /// <summary>The prepared game (and a zip's staging folder) is released first: Reconvert works on the added game's folder.</summary>
    [RelayCommand(CanExecute = nameof(CanReconvert))]
    private async Task ReconvertAsync()
    {
        IsLeaving = true;
        try
        {
            var disposing = game.DisposeAsync();
            AddCommand.NotifyCanExecuteChanged();
            BackCommand.NotifyCanExecuteChanged();
            ReconvertCommand.NotifyCanExecuteChanged();
            await disposing.ConfigureAwait(true);
            navigator.GoTo(pages.Reconvert(TitleId, Name));
        }
        finally
        {
            IsLeaving = false;
        }
    }

    [RelayCommand]
    private void OpenFolder() => OpenError = game.GameDirectory is { } folder ? PathOpener.TryOpen(platform, folder) : null;
}
