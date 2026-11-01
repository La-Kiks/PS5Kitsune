using System.Text.Json;

namespace PS5Kitsune.Core.Games;

/// <summary>Reads what AnyPS5 needs from sce_sys/param.json. See docs/architecture.md, "V2 — Add a game".</summary>
internal static class ParamJson
{
    /// <exception cref="InvalidDataException">The file is not what AnyPS5 reads; the message says why.</exception>
    public static GameMetadata Read(string path)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"sce_sys/param.json is not valid JSON: {exception.Message}", exception);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("sce_sys/param.json must hold a JSON object.");
            }

            var titleId = GetString(root, "titleId");
            if (string.IsNullOrWhiteSpace(titleId))
            {
                throw new InvalidDataException("sce_sys/param.json has no titleId.");
            }

            if (!GameValidator.IsValidTitleId(titleId))
            {
                throw new InvalidDataException($"Title ID \"{titleId}\" must contain only letters and digits.");
            }

            if (!root.TryGetProperty("localizedParameters", out var localized)
                || localized.ValueKind != JsonValueKind.Object
                || !localized.EnumerateObject().Any())
            {
                throw new InvalidDataException("sce_sys/param.json has no localizedParameters.");
            }

            return new GameMetadata(titleId, ResolveName(root, localized) ?? titleId, ParamSfo.IsPs4TitleId(titleId));
        }
    }

    // titleName of the default language (declared in localizedParameters or at the top level), else en-US, else the first language.
    private static string? ResolveName(JsonElement root, JsonElement localized)
    {
        var defaultLanguage = GetString(localized, "defaultLanguage") ?? GetString(root, "defaultLanguage");
        foreach (var language in new[] { defaultLanguage, "en-US" })
        {
            if (language is not null && localized.TryGetProperty(language, out var entry) && TitleName(entry) is { } name)
            {
                return name;
            }
        }

        return localized.EnumerateObject().Select(property => TitleName(property.Value)).FirstOrDefault(name => name is not null);
    }

    private static string? TitleName(JsonElement entry) =>
        entry.ValueKind == JsonValueKind.Object && GetString(entry, "titleName") is { } name && !string.IsNullOrWhiteSpace(name) ? name : null;

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
