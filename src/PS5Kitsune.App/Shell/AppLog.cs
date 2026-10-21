using Microsoft.Extensions.Logging;

namespace PS5Kitsune.App.Shell;

internal static partial class AppLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "{Problem}")]
    public static partial void SettingsIgnored(this ILogger logger, string problem);
}
