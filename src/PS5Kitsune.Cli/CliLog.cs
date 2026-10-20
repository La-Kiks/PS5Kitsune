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

    [LoggerMessage(Level = LogLevel.Information, Message = "AnyPS5 {Version} is installed in {Directory}.")]
    public static partial void AnyPS5Ready(this ILogger logger, string version, string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "CPU: {Vendor}. Intel conversion (--to-intel): {ToIntel}.")]
    public static partial void CpuDetected(this ILogger logger, string vendor, string toIntel);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Message}")]
    public static partial void BlockingIssue(this ILogger logger, string message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Message}")]
    public static partial void WarningIssue(this ILogger logger, string message);

    [LoggerMessage(Level = LogLevel.Error, Message = "{TitleId} is already added in {Directory}. Delete that folder to add the game again.")]
    public static partial void AlreadyAdded(this ILogger logger, string titleId, string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Name} ({TitleId}) is added in {Directory}.")]
    public static partial void GameAdded(this ILogger logger, string name, string titleId, string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "Audit: {Implemented} implemented, {Stub} stubs, {Absent} absent.")]
    public static partial void AuditCounts(this ILogger logger, int implemented, int stub, int absent);

    [LoggerMessage(Level = LogLevel.Information, Message = "Verdict: green. The game can load.")]
    public static partial void VerdictGreen(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Verdict: orange. The game loads but will stop if it calls an unimplemented function.")]
    public static partial void VerdictOrange(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Verdict: red. Not playable yet: wait for an AnyPS5 update. The game stays added and will need a reconversion.")]
    public static partial void VerdictRed(this ILogger logger);
}
