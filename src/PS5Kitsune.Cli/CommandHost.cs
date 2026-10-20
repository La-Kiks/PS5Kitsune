using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PS5Kitsune.Core;
using PS5Kitsune.Core.Logging;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Platform.Windows;

namespace PS5Kitsune.Cli;

internal static class CommandHost
{
    /// <summary>
    /// Builds the services for one command, logs to &lt;root&gt;/logs/&lt;runName&gt;-&lt;date&gt;.log, and maps
    /// cancellation to exit code 130 and any other exception to 1.
    /// </summary>
    public static async Task<int> RunAsync(DirectoryInfo? rootOverride, string runName, Func<IServiceProvider, ILogger, Task<int>> action)
    {
        var platform = PlatformServices.Detect();
        var layout = new DataLayout(rootOverride?.FullName ?? platform.DefaultDataRoot);
        var services = new ServiceCollection();
        var logFile = services.AddPS5KitsuneLogging(layout.Logs, runName, console: true);
        services.AddPlatform(platform).AddPS5KitsuneCore(layout, ProfileLoader.LoadEmbedded());

        await using var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("ps5kitsune");
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
