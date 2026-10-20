using System.Text.Json;
using System.Text.Json.Serialization;

namespace PS5Kitsune.Core.IO;

/// <summary>The app's JSON records (install.json, game.json): camelCase, indented, enums as camelCase strings.</summary>
internal static class JsonFiles
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <returns>Null when the file does not exist.</returns>
    /// <exception cref="JsonException">The file is not valid JSON for <typeparamref name="T"/>.</exception>
    public static T? Read<T>(string path)
        where T : class =>
        File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) : null;

    /// <summary>Writes through a temporary file, so a crash never leaves half a record.</summary>
    public static async Task WriteAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(value, Options), cancellationToken).ConfigureAwait(false);
        File.Move(temporary, path, overwrite: true);
    }
}
