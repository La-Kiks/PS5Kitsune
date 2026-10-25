using System.Text.Json;
using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Core.Settings;

/// <summary>Reads and writes settings.json in the platform's settings folder, outside the data root.</summary>
public sealed class AppSettingsStore(string settingsDirectory)
{
    private readonly object _gate = new();
    private Task _lastUpdate = Task.CompletedTask;

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

            if (settings.SchemaVersion != AppSettings.CurrentSchemaVersion)
            {
                return new AppSettingsLoad(AppSettings.Default, $"{FilePath} has schema version {settings.SchemaVersion}; using the default settings.");
            }

            // A relative root would resolve against each process's own current directory.
            return settings.DataRoot is not null && (string.IsNullOrWhiteSpace(settings.DataRoot) || !Path.IsPathFullyQualified(settings.DataRoot))
                ? new AppSettingsLoad(AppSettings.Default, $"{FilePath} has an invalid dataRoot \"{settings.DataRoot}\"; using the default settings.")
                : new AppSettingsLoad(settings, null);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppSettingsLoad(AppSettings.Default, $"{FilePath} cannot be read ({exception.Message}); using the default settings.");
        }
    }

    /// <summary>Writes through a temporary file, so a crash never leaves half a file.</summary>
    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
        JsonFiles.WriteAsync(FilePath, settings, cancellationToken);

    /// <summary>
    /// Loads the current settings, applies <paramref name="change"/> and saves. Updates on this store run one at a time in the order they were
    /// started, so two writers (the theme and the data root) cannot fight over the temporary file or save a stale copy of each other's field.
    /// A file that <see cref="Load"/> could not use (corrupt, another schema version, an invalid root) is not overwritten: the change then only
    /// applies to this run, and the result is false. A missing file is not a problem, so the first save creates it.
    /// </summary>
    /// <returns>True when the settings were saved.</returns>
    public Task<bool> UpdateAsync(Func<AppSettings, AppSettings> change, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var update = UpdateAfterAsync(_lastUpdate, change, cancellationToken);
            _lastUpdate = update;
            return update;
        }
    }

    private async Task<bool> UpdateAfterAsync(Task previous, Func<AppSettings, AppSettings> change, CancellationToken cancellationToken)
    {
        // WhenAny never throws, so one failed update cannot block the next.
        await Task.WhenAny(previous).ConfigureAwait(false);
        var load = Load();
        if (load.Problem is not null)
        {
            return false;
        }

        await SaveAsync(change(load.Settings), cancellationToken).ConfigureAwait(false);
        return true;
    }
}
