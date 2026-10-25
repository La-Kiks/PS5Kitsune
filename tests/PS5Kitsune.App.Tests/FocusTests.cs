using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Threading;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Shell;
using PS5Kitsune.App.Views;
using PS5Kitsune.App.Tests.Headless;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Settings;

namespace PS5Kitsune.App.Tests;

/// <summary>Builds the gallery once for every FocusTests case.</summary>
public sealed class GalleryFixture : IAsyncLifetime
{
    private PageGallery? _gallery;

    internal IReadOnlyDictionary<string, object> Pages { get; private set; } = new Dictionary<string, object>();

    internal string SettingsFolder => _gallery!.SettingsFolder;

    public async ValueTask InitializeAsync()
    {
        _gallery = new PageGallery();
        Pages = (await _gallery.BuildAsync()).ToDictionary(entry => entry.Name, entry => entry.Page);
    }

    public ValueTask DisposeAsync()
    {
        _gallery?.Dispose();
        return ValueTask.CompletedTask;
    }
}

[Collection("Headless")]
public sealed class FocusTests(HeadlessSession session, GalleryFixture gallery) : IClassFixture<GalleryFixture>
{
    public static TheoryData<string, string> Expected { get; } = new()
    {
        { "Home", "Verify / repair" },
        // Continue is disabled with a blocking check, so the focus falls back to "Check again" (rank 2).
        { "SystemCheck", "Check again" },
        { "SystemCheck-Ok", "Continue" },
        { "Mode", "Continue" },
        { "Tools", "Download what's missing and build" },
        { "Install-Running", "Cancel" },
        { "Install-Failed", "Retry" },
        { "Done", "Back to home" },
        { "ChooseGame", "Choose a folder…" },
        { "GameSummary", "Add the game" },
        { "AddGame", "Cancel" },
        { "AddResult", "Go to library" },
        { "Reconvert", "Cancel" },
        { "Library", "Search by name" },
        { "Library-Empty", "Add a game" },
        { "Game-Preparing", "← Library" },
        { "Game-Running-NoLog", "Stop" },
        { "Game-Running-Log", "Stop" },
        { "Game-Diagnosis", "Launch again" },
        { "GameSettings", "GPU" },
    };

    [Theory]
    [MemberData(nameof(Expected))]
    public async Task The_first_focus_is_the_primary_control(string page, string expected)
    {
        var actual = await session.RunAsync(() =>
        {
            var navigator = new Navigator();
            var theme = new ThemeService(new AppSettingsStore(gallery.SettingsFolder), _ => { });
            var window = new MainWindow { DataContext = new MainWindowViewModel(navigator, new FakeDialogs(), new FakePages(), theme) };
            try
            {
                window.Show();
                navigator.GoTo(gallery.Pages[page]);
                Dispatcher.UIThread.RunJobs();
                Dispatcher.UIThread.RunJobs();
                return Describe(window.FocusManager!.GetFocusedElement());
            }
            finally
            {
                navigator.GoTo("end");
                window.Close();
            }
        });

        Assert.True(expected == actual.Text, $"{page}: expected focus on \"{expected}\", got {actual.Type} \"{actual.Text}\".");
    }

    private static (string Type, string? Text) Describe(object? element) => element switch
    {
        Button { Content: string content } => ("Button", content),
        TextBox box => ("TextBox", AutomationProperties.GetName(box)),
        ComboBox combo => ("ComboBox", (AutomationProperties.GetLabeledBy(combo) as TextBlock)?.Text),
        null => ("null", null),
        _ => (element.GetType().Name, null),
    };
}
