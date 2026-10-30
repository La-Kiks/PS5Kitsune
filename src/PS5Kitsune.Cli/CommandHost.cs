using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PS5Kitsune.Core;
using PS5Kitsune.Core.Logging;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Settings;
using PS5Kitsune.Platform.Windows;

namespace PS5Kitsune.Cli;

internal static class CommandHost
{
    /// <summary>
    /// Builds the services for one command on the root from --root, else the app's settings, else the default; logs to &lt;root&gt;/logs/&lt;runName&gt;-&lt;date&gt;.log, and maps
    /// cancellation to exit code 130 and any other exception to 1.
    /// </summary>
    public static async Task<int> RunAsync(DirectoryInfo? rootOverride, string runName, Func<IServiceProvider, ILogger, Task<int>> action)
    {
        var platform = PlatformServices.Detect();
        var settings = new AppSettingsStore(platform.SettingsDirectory).Load();
        var layout = new DataLayout(DataRoots.Resolve(rootOverride?.FullName, settings.Settings, platform.DefaultDataRoot));
        var services = new ServiceCollection();
        var logFile = services.AddPS5KitsuneLogging(layout.Logs, runName, console: true);
        var active = ProfileLoader.LoadActive(layout, platform.Os);
        services.AddPlatform(platform).AddPS5KitsuneCore(layout, active.Profile);
        services.AddSingleton(new CommandContext(DataRoots.Describe(rootOverride?.FullName, settings.Settings), active.Origin));

        await using var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("ps5kitsune");
        if (settings.Problem is { } problem)
        {
            logger.SettingsIgnored(problem);
        }

        if (active.Problem is { } profileProblem)
        {
            logger.ProfileIgnored(profileProblem);
        }

        try
        {
            return await action(provider, logger);
        }
        catch (OperationCanceledException)
        {
            logger.Cancelled();
            return 130;
        }
        catch (Exception exception)
        {
            logger.Failed(exception, exception.Message);
            logger.FullLog(logFile);
            return 1;
        }
    }
}
