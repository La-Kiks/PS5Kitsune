using System.Text.Json;
using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Core.Library;

public sealed class InvalidGameRecordException(string message, Exception? innerException = null) : Exception(message, innerException);

public static class GameRecordStore
{
    /// <returns>Null when the file does not exist.</returns>
    /// <exception cref="InvalidGameRecordException">The file is malformed or has another schema version.</exception>
    public static GameRecord? Read(string path)
    {
        GameRecord? record;
        try
        {
            record = JsonFiles.Read<GameRecord>(path);
        }
        catch (JsonException exception)
        {
            throw new InvalidGameRecordException($"{path} is not a valid game record: {exception.Message}", exception);
        }

        if (record is not null && record.SchemaVersion != GameRecord.CurrentSchemaVersion)
        {
            throw new InvalidGameRecordException($"{path} has schema version {record.SchemaVersion}; this app reads version {GameRecord.CurrentSchemaVersion}.");
        }

        return record;
    }

    public static Task WriteAsync(string path, GameRecord record, CancellationToken cancellationToken) =>
        JsonFiles.WriteAsync(path, record, cancellationToken);
}
