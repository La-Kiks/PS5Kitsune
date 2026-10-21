using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets;
using NLogLevel = NLog.LogLevel;

namespace PS5Kitsune.Core.Logging;

public static class LoggingSetup
{
    /// <summary>
    /// Logs everything (debug and up, including tool output) to one file per run under
    /// <paramref name="logDirectory"/>, and optionally information and up to the console.
    /// </summary>
    /// <returns>The path of this run's log file.</returns>
    public static string AddPS5KitsuneLogging(this IServiceCollection services, string logDirectory, string runName, bool console)
    {
        var logFile = Path.Combine(logDirectory, $"{runName}-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        var configuration = new LoggingConfiguration();
        configuration.AddRule(NLogLevel.Debug, NLogLevel.Fatal, new FileTarget("file")
        {
            FileName = logFile,
            Layout = "${longdate} ${level:uppercase=true:padding=-5} ${logger:shortName=true} ${message}${onexception:${newline}${exception:format=tostring}}",
        });
        if (console)
        {
            configuration.AddRule(NLogLevel.Info, NLogLevel.Fatal, new ConsoleTarget("console") { Layout = "${message}" });
        }

        // One factory per call, owned by the container: disposing the provider closes this run's files
        // without touching another run's configuration.
        var factory = new NLog.LogFactory { Configuration = configuration };
        services.AddSingleton(_ => factory);
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddNLog(provider => provider.GetRequiredService<NLog.LogFactory>());
        });
        return logFile;
    }
}
