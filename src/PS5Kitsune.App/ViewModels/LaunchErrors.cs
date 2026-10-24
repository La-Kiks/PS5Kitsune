using System.ComponentModel;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.App.ViewModels;

/// <summary>The line the Library and the Game page show when a launch is refused or fails.</summary>
internal static class LaunchErrors
{
    /// <returns>Null for an exception a launch is not expected to throw: the caller lets it through.</returns>
    public static string? Describe(Exception exception, string name) => exception switch
    {
        GameRunningException => $"{name} is already running.",
        GameBusyException => $"Another PS5Kitsune action is using {name}. Try again when it finishes.",
        GameExecutableMissingException => $"The executable is missing: Reconvert {name}.",
        AnyPS5NotInstalledException => "AnyPS5 is not installed. Install it from Home first.",
        GameNotFoundException or InvalidGameRecordException or IOException or UnauthorizedAccessException or Win32Exception or PlatformNotSupportedException =>
            $"Could not launch {name}: {exception.Message}",
        _ => null,
    };
}
