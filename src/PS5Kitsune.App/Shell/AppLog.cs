using Microsoft.Extensions.Logging;

namespace PS5Kitsune.App.Shell;

internal static partial class AppLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "{Problem}")]
    public static partial void SettingsIgnored(this ILogger logger, string problem);

    [LoggerMessage(Level = LogLevel.Error, Message = "Confirming the window close failed; the window stays open.")]
    public static partial void CloseConfirmationFailed(this ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not save the theme.")]
    public static partial void ThemeSaveFailed(this ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Did not save the theme: {Problem}")]
    public static partial void ThemeNotSaved(this ILogger logger, string problem);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not leave {Page}.")]
    public static partial void LeaveFailed(this ILogger logger, Exception exception, string page);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reading the chosen game failed.")]
    public static partial void PrepareGameFailed(this ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "A page failed after its steps finished.")]
    public static partial void StepRunHookFailed(this ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The update check failed unexpectedly.")]
    public static partial void UpdateCheckFailed(this ILogger logger, Exception exception);
}
