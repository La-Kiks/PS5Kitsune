using System.Text.Json;
using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Core.Settings;

/// <summary>Reads and writes settings.json in the platform's settings folder, outside the data root.</summary>
public sealed class AppSettingsStore(string settingsDirectory)
{
    public string FilePath { get; } = Path.Combine(settingsDirectory, "settings.json");

    /// <summary>Never throws: a file that cannot be used gives the defaults and says why.</summary>
    public AppSettingsLoad Load()
    {
        try
        {
            var settings = JsonFiles.Read<AppSettings>(FilePath);
            if (settings is null)
            {
                return new AppSettingsLoad(AppSettings.Default, null);
            }

            return settings.SchemaVersion == AppSettings.CurrentSchemaVersion
                ? new AppSettingsLoad(settings, null)
                : new AppSettingsLoad(AppSettings.Default, $"{FilePath} has schema version {settings.SchemaVersion}; using the default settings.");
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppSettingsLoad(AppSettings.Default, $"{FilePath} cannot be read ({exception.Message}); using the default settings.");
        }
    }

    /// <summary>Writes through a temporary file, so a crash never leaves half a file.</summary>
    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
        JsonFiles.WriteAsync(FilePath, settings, cancellationToken);
}
