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

    private const int MoveAttempts = 5;

    private static readonly TimeSpan MoveRetryDelay = TimeSpan.FromMilliseconds(50);

    /// <summary>Writes through a temporary file, so a crash never leaves half a record.</summary>
    public static Task WriteAsync<T>(string path, T value, CancellationToken cancellationToken) =>
        WriteAsync(path, value, MoveAttempts, MoveRetryDelay, cancellationToken);

    /// <summary>
    /// The final move is retried: while another handle holds the destination open (an antivirus scan, a reader), Windows refuses to replace it
    /// with access denied, or a sharing or lock violation.
    /// </summary>
    internal static async Task WriteAsync<T>(string path, T value, int attempts, TimeSpan delay, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(value, Options), cancellationToken).ConfigureAwait(false);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(temporary, path, overwrite: true);
                return;
            }
            catch (Exception exception) when (attempt < attempts && IsTransient(exception))
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool IsTransient(Exception exception) =>
        exception is UnauthorizedAccessException
        || (exception is IOException && exception.HResult is unchecked((int)0x80070020) or unchecked((int)0x80070021));
}
