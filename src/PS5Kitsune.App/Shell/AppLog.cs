using Microsoft.Extensions.Logging;

namespace PS5Kitsune.App.Shell;

internal static partial class AppLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "{Problem}")]
    public static partial void SettingsIgnored(this ILogger logger, string problem);

    [LoggerMessage(Level = LogLevel.Error, Message = "Confirming the window close failed; the window stays open.")]
    public static partial void CloseConfirmationFailed(this ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reading the chosen game failed.")]
    public static partial void PrepareGameFailed(this ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "A page failed after its steps finished.")]
    public static partial void StepRunHookFailed(this ILogger logger, Exception exception);
}
