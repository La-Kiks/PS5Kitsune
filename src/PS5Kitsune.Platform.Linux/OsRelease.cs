namespace PS5Kitsune.Platform.Linux;

/// <summary>The distribution, from /etc/os-release: its ID and the IDs it is like (ID_LIKE).</summary>
public sealed record OsRelease(string Id, IReadOnlyList<string> IdLike)
{
    public const string DefaultPath = "/etc/os-release";

    public static OsRelease Unknown { get; } = new("", []);

    /// <summary>Reads KEY=value lines; values may be quoted with " or '. Comments and other lines are skipped.</summary>
    public static OsRelease Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var id = "";
        var idLike = "";
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            var equals = line.IndexOf('=', StringComparison.Ordinal);
            if (line.StartsWith('#') || equals <= 0)
            {
                continue;
            }

            var value = Unquote(line[(equals + 1)..]);
            switch (line[..equals])
            {
                case "ID":
                    id = value;
                    break;
                case "ID_LIKE":
                    idLike = value;
                    break;
            }
        }

        return new OsRelease(id, idLike.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>The file's distribution; <see cref="Unknown"/> when it is missing or unreadable.</summary>
    public static OsRelease Read(string path = DefaultPath)
    {
        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Unknown;
        }
    }

    private static string Unquote(string value) =>
        value.Length >= 2 && (value[0] is '"' or '\'') && value[^1] == value[0] ? value[1..^1] : value;
}
