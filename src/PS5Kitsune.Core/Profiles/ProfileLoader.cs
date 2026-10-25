using System.Globalization;
using System.Text.Json;

namespace PS5Kitsune.Core.Profiles;

public sealed class InvalidProfileException(string message, Exception? innerException = null) : Exception(message, innerException);

public static class ProfileLoader
{
    private const string EmbeddedResourceName = "PS5Kitsune.Core.Profiles.default-profile.json";

    public const string OverrideFileName = "override.json";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>The profile shipped inside the app.</summary>
    public static AnyPS5Profile LoadEmbedded()
    {
        using var stream = typeof(ProfileLoader).Assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidProfileException($"Embedded resource {EmbeddedResourceName} is missing.");
        return Load(stream);
    }

    /// <summary>
    /// The override in the data root's profiles folder when it is valid, else the embedded profile.
    /// An override that cannot be read or validated is ignored, and <see cref="ActiveProfile.Problem"/> says why.
    /// </summary>
    public static ActiveProfile LoadActive(DataLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var path = Path.Combine(layout.Profiles, OverrideFileName);
        if (!File.Exists(path))
        {
            return new ActiveProfile(LoadEmbedded(), ProfileOrigin.Embedded, null);
        }

        try
        {
            using var stream = File.OpenRead(path);
            return new ActiveProfile(Load(stream), ProfileOrigin.Override, null);
        }
        catch (Exception exception) when (exception is InvalidProfileException or IOException or UnauthorizedAccessException)
        {
            return new ActiveProfile(LoadEmbedded(), ProfileOrigin.Embedded, $"Ignored {path}: {exception.Message} Using the embedded profile.");
        }
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
            if (tool.Size is <= 0)
            {
                throw new InvalidProfileException($"Profile {profile.Id}: tool {name} size must be positive, got {tool.Size.Value.ToString(CultureInfo.InvariantCulture)}.");
            }
        }

        var fontNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var font in profile.Fonts)
        {
            if (string.IsNullOrEmpty(font.Name) || font.Name is "." or ".." || font.Name.IndexOfAny(['/', '\\', ':']) >= 0 || Path.GetFileName(font.Name) != font.Name || font.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || !fontNames.Add(font.Name))
            {
                throw new InvalidProfileException($"Profile {profile.Id}: font \"{font.Name}\" needs a unique file name without a folder.");
            }

            RequireSha256(profile.Id, $"font {font.Name}", font.Sha256);
            RequireHttps(profile.Id, $"font {font.Name}", font.Url);
            if (font.Size <= 0)
            {
                throw new InvalidProfileException($"Profile {profile.Id}: font {font.Name} size must be positive.");
            }
        }

        if (profile.Build is { } build)
        {
            RequireHttps(profile.Id, "build repository", build.Repository);
            if (build.Commit.Length != 40 || !build.Commit.All(char.IsAsciiHexDigitLower))
            {
                throw new InvalidProfileException($"Profile {profile.Id}: build.commit needs a full lowercase hex SHA, got \"{build.Commit}\".");
            }
        }

        if (profile.Checks.CommitLimitMiB <= 0 || profile.Checks.MinFreeDiskGiB < 0 || profile.Checks.BuildMinFreeDiskGiB < 0)
        {
            throw new InvalidProfileException($"Profile {profile.Id}: checks.commitLimitMiB must be positive, checks.minFreeDiskGiB and checks.buildMinFreeDiskGiB not negative.");
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
