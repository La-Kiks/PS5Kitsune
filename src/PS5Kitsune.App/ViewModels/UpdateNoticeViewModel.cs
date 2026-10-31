using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.Core.Updates;

namespace PS5Kitsune.App.ViewModels;

/// <summary>The sidebar's update notice: hidden until the check finds a newer release, then a button that opens its page.</summary>
/// <param name="open">Opens a web page; the app passes <c>IPlatform.OpenPath</c>.</param>
public sealed partial class UpdateNoticeViewModel(Action<string> open) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdate), nameof(Text), nameof(ButtonName))]
    [NotifyCanExecuteChangedFor(nameof(OpenCommand))]
    public partial UpdateInfo? Update { get; set; }

    /// <summary>Why the browser could not be opened, with the address to open by hand; null otherwise.</summary>
    [ObservableProperty]
    public partial string? OpenError { get; set; }

    public bool HasUpdate => Update is not null;

    public string? Text => Update is { } update ? "PS5Kitsune " + update.Version : null;

    public string? ButtonName => Update is { } update ? "Download PS5Kitsune " + update.Version : null;

    /// <summary>Checks once; does nothing when the running version is unknown. Never throws: the checker logs its failures.</summary>
    public async Task CheckAsync(UpdateChecker checker, AppVersion? current)
    {
        if (current is not { } version)
        {
            return;
        }

        Update = await checker.CheckAsync(version).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(HasUpdate))]
    private void Open()
    {
        if (Update is { } update)
        {
            var address = update.ReleasePage.ToString();
            var problem = PathOpener.TryRun(() => open(address));
            OpenError = problem is null ? null : $"{problem} Open {address} in your browser.";
        }
    }
}
