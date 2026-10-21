using System.Text.Json;

namespace PS5Kitsune.Core.Profiles;

public sealed class InvalidProfileException(string message, Exception? innerException = null) : Exception(message, innerException);

public static class ProfileLoader
{
    private const string EmbeddedResourceName = "PS5Kitsune.Core.Profiles.default-profile.json";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>The profile shipped inside the app.</summary>
    public static AnyPS5Profile LoadEmbedded()
    {
        using var stream = typeof(ProfileLoader).Assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidProfileException($"Embedded resource {EmbeddedResourceName} is missing.");
        return Load(stream);
    }

    /// <exception cref="InvalidProfileException">The JSON is malformed or fails validation.</exception>
    public static AnyPS5Profile Load(Stream json)
    {
        AnyPS5Profile? profile;
        try
        {
            profile = JsonSerializer.Deserialize<AnyPS5Profile>(json, Options);
        }
        catch (JsonException exception)
        {
            throw new InvalidProfileException($"Profile is not valid JSON: {exception.Message}", exception);
        }

        if (profile is null)
        {
            throw new InvalidProfileException("Profile is empty.");
        }

        Validate(profile);
        return profile;
    }

    private static void Validate(AnyPS5Profile profile)
    {
        if (profile.SchemaVersion != AnyPS5Profile.CurrentSchemaVersion)
        {
            throw new InvalidProfileException($"Profile {profile.Id} has schema version {profile.SchemaVersion}; this app reads version {AnyPS5Profile.CurrentSchemaVersion}.");
        }

        foreach (var (name, asset) in profile.Assets)
        {
            RequireSha256(profile.Id, $"asset {name}", asset.Sha256);
            if (asset.Url is not null)
            {
                RequireHttps(profile.Id, $"asset {name}", asset.Url);
            }
            else if (string.IsNullOrEmpty(asset.Name) || profile.Source.Tag is null)
            {
                throw new InvalidProfileException($"Profile {profile.Id}: asset {name} needs a url, or a name and a release source tag.");
            }
        }

        foreach (var (name, tool) in profile.Tools)
        {
            RequireSha256(profile.Id, $"tool {name}", tool.Sha256);
            RequireHttps(profile.Id, $"tool {name}", tool.Url);
        }

        if (profile.Checks.CommitLimitMiB <= 0 || profile.Checks.MinFreeDiskGiB < 0)
        {
            throw new InvalidProfileException($"Profile {profile.Id}: checks.commitLimitMiB must be positive and checks.minFreeDiskGiB not negative.");
        }
    }

    private static void RequireSha256(string profileId, string what, string value)
    {
        if (value.Length != 64 || !value.All(char.IsAsciiHexDigitLower))
        {
            throw new InvalidProfileException($"Profile {profileId}: {what} needs a lowercase hex SHA-256, got \"{value}\".");
        }
    }

    private static void RequireHttps(string profileId, string what, Uri url)
    {
        if (url.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidProfileException($"Profile {profileId}: {what} must be downloaded over HTTPS.");
        }
    }
}
