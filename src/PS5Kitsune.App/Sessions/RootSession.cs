using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Logging;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Platform.Windows;

namespace PS5Kitsune.App.Sessions;

/// <summary>The services for one data root: logging under its logs folder, and Core. Replaced when the root changes.</summary>
public sealed class RootSession(DataLayout layout, ServiceProvider services, string logFile) : IDisposable
{
    public DataLayout Layout { get; } = layout;

    /// <summary>This session's log file, for "Open log".</summary>
    public string LogFile { get; } = logFile;

    public bool IsDisposed { get; private set; }

    public static RootSession Create(string root, IPlatform platform, AnyPS5Profile profile)
    {
        var layout = new DataLayout(root);
        var services = new ServiceCollection();
        var logFile = services.AddPS5KitsuneLogging(layout.Logs, "app", console: false);
        services.AddPlatform(platform).AddPS5KitsuneCore(layout, profile);
        return new RootSession(layout, services.BuildServiceProvider(), logFile);
    }

    public T Get<T>()
        where T : notnull => services.GetRequiredService<T>();

    public void Dispose()
    {
        IsDisposed = true;
        services.Dispose();
    }
}
