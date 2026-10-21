using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.App.Views;
using PS5Kitsune.Core;
using PS5Kitsune.Core.Logging;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Platform.Windows;

namespace PS5Kitsune.App;

public sealed partial class App : Application
{
    private ServiceProvider? _services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var platform = PlatformServices.Detect();
        var layout = new DataLayout(platform.DefaultDataRoot);
        var services = new ServiceCollection();
        services.AddPS5KitsuneLogging(layout.Logs, "app", console: false);
        services.AddPlatform(platform).AddPS5KitsuneCore(layout, ProfileLoader.LoadEmbedded());
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<SystemCheckViewModel>();
        _services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = _services.GetRequiredService<MainWindowViewModel>();
            var window = new MainWindow { DataContext = viewModel };
            window.Opened += (_, _) => viewModel.SystemCheck.CheckCommand.Execute(null);
            desktop.MainWindow = window;
            desktop.Exit += (_, _) => _services.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
