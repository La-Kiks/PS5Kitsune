using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.App.ViewModels;

/// <summary>Opening a folder, a file or a settings dialog can fail (missing file, no associated app); the page shows why instead of crashing.</summary>
internal static class PathOpener
{
    /// <returns>Null on success, else the message to show.</returns>
    public static string? TryRun(Action open)
    {
        try
        {
            open();
            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return $"Could not open it: {exception.Message}";
        }
    }

    public static string? TryOpen(IPlatform platform, string path) => TryRun(() => platform.OpenPath(path));
}
