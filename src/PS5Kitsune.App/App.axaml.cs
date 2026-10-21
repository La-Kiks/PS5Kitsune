using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Logging;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Sessions;
using PS5Kitsune.App.Shell;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.App.Views;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Settings;
using PS5Kitsune.Platform.Windows;

namespace PS5Kitsune.App;

public sealed partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>Composition root: root-independent objects here, everything tied to a data root in its <see cref="RootSession"/>.</summary>
    public override void OnFrameworkInitializationCompleted()
    {
        var platform = PlatformServices.Detect();
        var profile = ProfileLoader.LoadEmbedded();
        var settings = new AppSettingsStore(platform.SettingsDirectory);
        var loaded = settings.Load();
        var sessions = new SessionHost(
            root => RootSession.Create(root, platform, profile),
            DataRoots.Resolve(null, loaded.Settings, platform.DefaultDataRoot));
        if (loaded.Problem is { } problem)
        {
            sessions.Current.Get<ILogger<App>>().SettingsIgnored(problem);
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            var navigator = new Navigator();
            var dialogs = new AvaloniaDialogService(() => window);
            var pages = new PageFactory(sessions, navigator, dialogs, settings, new AvaloniaUiDispatcher(), platform);
            window.DataContext = new MainWindowViewModel(sessions, navigator, platform, dialogs);
            navigator.GoHome(pages.Home());
            desktop.MainWindow = window;
            desktop.Exit += (_, _) => sessions.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
