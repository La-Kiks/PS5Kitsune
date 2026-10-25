using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Shell;
using PS5Kitsune.App.Tests.Headless;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.App.Views;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Settings;
using PS5Kitsune.Core.Tests;

namespace PS5Kitsune.App.Tests;

[Collection("Headless")]
public sealed class TabOrderTests(HeadlessSession session) : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public async Task Tab_goes_through_the_sidebar_then_the_page_then_its_footer()
    {
        var stops = await session.RunAsync(() =>
        {
            var navigator = new Navigator();
            var theme = new ThemeService(new AppSettingsStore(_temp.Combine("settings")), _ => { });
            var window = new MainWindow { DataContext = new MainWindowViewModel(navigator, new FakeDialogs(), new FakePages(), theme) };
            try
            {
                window.Show();
                navigator.GoTo(new ModeViewModel(TestSessions.Profile, new FakeInstaller { CurrentMode = AnyPS5Mode.Release }, new Navigator(), new FakePages()));
                Dispatcher.UIThread.RunJobs();
                Dispatcher.UIThread.RunJobs();

                window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Home")).Focus(NavigationMethod.Tab);
                Dispatcher.UIThread.RunJobs();
                var seen = new List<string> { Describe(window.FocusManager!.GetFocusedElement()) };
                for (var step = 0; step < 8; step++)
                {
                    window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
                    seen.Add(Describe(window.FocusManager!.GetFocusedElement()));
                }

                return seen;
            }
            finally
            {
                window.Close();
            }
        });

        // The theme switch may take one Tab stop or three; either way it sits between the sidebar buttons and the page.
        var order = stops.Select(stop => stop.StartsWith("radio:Theme", StringComparison.Ordinal) ? "theme" : stop).Distinct().Take(6);
        Assert.Equal(["Home", "Library", "theme", "radio:Mode:", "Back", "Continue"], order);
    }

    public void Dispose() => _temp.Dispose();

    private static string Describe(object? element) => element switch
    {
        RadioButton radio => $"radio:{radio.GroupName}:{Avalonia.Automation.AutomationProperties.GetName(radio)}",
        Button { Content: string content } => content,
        null => "null",
        _ => element.GetType().Name,
    };
}
