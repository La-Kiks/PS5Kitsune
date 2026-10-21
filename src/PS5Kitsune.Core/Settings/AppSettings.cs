namespace PS5Kitsune.Core.Settings;

/// <summary>Contents of settings.json, shared by the app and the CLI.</summary>
/// <param name="SchemaVersion">Version of the file format.</param>
/// <param name="DataRoot">The data root chosen in the app; null for the platform's default.</param>
public sealed record AppSettings(int SchemaVersion, string? DataRoot)
{
    public const int CurrentSchemaVersion = 1;

    public static AppSettings Default { get; } = new(CurrentSchemaVersion, null);
}

/// <param name="Settings">The settings to use.</param>
/// <param name="Problem">Why the file was ignored, to log once logging exists; null when it was read or absent.</param>
public sealed record AppSettingsLoad(AppSettings Settings, string? Problem);
