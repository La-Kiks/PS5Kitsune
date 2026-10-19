using Microsoft.Extensions.Logging;

namespace PS5Kitsune.Cli;

internal static partial class CliLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Unknown tool \"{Tool}\". Run 'ps5kitsune tools list' to see the available tools.")]
    public static partial void UnknownTool(this ILogger logger, string tool);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Tool} {Version} is ready.")]
    public static partial void ToolReady(this ILogger logger, string tool, string version);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cancelled.")]
    public static partial void Cancelled(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Message}")]
    public static partial void Failed(this ILogger logger, Exception exception, string message);

    [LoggerMessage(Level = LogLevel.Information, Message = "Full log: {LogFile}")]
    public static partial void FullLog(this ILogger logger, string logFile);
}
