using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Logging;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Sessions;
using PS5Kitsune.App.Shell;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.App.Views;
using PS5Kitsune.Core;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Settings;
using PS5Kitsune.Core.Updates;
using PS5Kitsune.Platform;

namespace PS5Kitsune.App;

public sealed partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>Composition root: root-independent objects here, everything tied to a data root in its <see cref="RootSession"/>.</summary>
    public override void OnFrameworkInitializationCompleted()
    {
        var platform = PlatformServices.Detect();
        var settings = new AppSettingsStore(platform.SettingsDirectory);
        var loaded = settings.Load();
        var sessions = new SessionHost(
            root => RootSession.Create(root, platform, ProfileLoader.LoadActive(new DataLayout(root), platform.Os).Profile),
            DataRoots.Resolve(null, loaded.Settings, platform.DefaultDataRoot));
        var theme = new ThemeService(settings, choice => RequestedThemeVariant = ThemeService.VariantOf(choice), () => sessions.Current.Get<ILogger<App>>());
        theme.Apply(loaded.Settings.Theme);
        if (loaded.Problem is { } problem)
        {
            sessions.Current.Get<ILogger<App>>().SettingsIgnored(problem);
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow { LoggerSource = () => sessions.Current.Get<ILogger<MainWindow>>() };
            var navigator = new Navigator();
            var dialogs = new AvaloniaDialogService(() => window);
            var pages = new PageFactory(sessions, navigator, dialogs, settings, new AvaloniaUiDispatcher(), platform);
            var updateNotice = new UpdateNoticeViewModel(platform.OpenPath);
            window.DataContext = new MainWindowViewModel(navigator, dialogs, pages, theme, () => sessions.Current.Get<ILogger<MainWindowViewModel>>(), updateNotice);
            if (loaded.Settings.CheckForUpdates)
            {
                // In the background once the window is up: startup never waits for GitHub, and a failure only logs a line.
                window.Opened += async (_, _) => await updateNotice.CheckAsync(sessions.Current.Get<UpdateChecker>(), AppVersion.Current).ConfigureAwait(true);
            }
            navigator.GoHome(pages.Start());
            desktop.MainWindow = window;
            desktop.Exit += (_, _) => sessions.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
