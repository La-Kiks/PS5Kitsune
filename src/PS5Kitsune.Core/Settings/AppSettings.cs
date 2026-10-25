namespace PS5Kitsune.Core.Settings;

/// <summary>Contents of settings.json, shared by the app and the CLI.</summary>
/// <param name="SchemaVersion">Version of the file format.</param>
/// <param name="DataRoot">The data root chosen in the app; null for the platform's default.</param>
/// <param name="Theme">The app's theme; absent in older files, which means <see cref="AppTheme.System"/>.</param>
public sealed record AppSettings(int SchemaVersion, string? DataRoot, AppTheme Theme = AppTheme.System)
{
    public const int CurrentSchemaVersion = 1;

    public static AppSettings Default { get; } = new(CurrentSchemaVersion, null);
}

/// <summary>The app's light or dark look; System follows the operating system.</summary>
public enum AppTheme
{
    System,
    Light,
    Dark,
}

/// <param name="Settings">The settings to use.</param>
/// <param name="Problem">Why the file was ignored, to log once logging exists; null when it was read or absent.</param>
/// <param name="OtherSchema">The problem is a file with a newer schema version: it may be a newer build's, so it is never replaced.</param>
public sealed record AppSettingsLoad(AppSettings Settings, string? Problem, bool OtherSchema = false);
