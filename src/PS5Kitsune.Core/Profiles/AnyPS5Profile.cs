namespace PS5Kitsune.Core.Profiles;

/// <summary>
/// An AnyPS5 version and everything that depends on it: assets, tools, relinker options.
/// See docs/architecture.md, "AnyPS5 profiles".
/// </summary>
public sealed record AnyPS5Profile
{
    public const int CurrentSchemaVersion = 1;

    public const string RepositoryUrl = "https://github.com/boykopovar/AnyPS5";

    public required int SchemaVersion { get; init; }

    public required string Id { get; init; }

    /// <summary>True once the profile has been run end to end with DOOM for PS5.</summary>
    public bool Validated { get; init; }

    public required ProfileSource Source { get; init; }

    public ProfileSource? AlternateSource { get; init; }

    public IReadOnlyDictionary<string, ProfileAsset> Assets { get; init; } = new Dictionary<string, ProfileAsset>();

    public IReadOnlyDictionary<string, ToolDefinition> Tools { get; init; } = new Dictionary<string, ToolDefinition>();

    public RelinkerOptions Relinker { get; init; } = new();

    /// <exception cref="InvalidProfileException">The profile has no asset named <paramref name="key"/>.</exception>
    public ProfileAsset Asset(string key) =>
        Assets.TryGetValue(key, out var asset) ? asset : throw new InvalidProfileException($"Profile {Id} has no asset \"{key}\".");

    /// <summary>The asset's own URL, else the asset of that name in the release named by the source tag.</summary>
    /// <exception cref="InvalidProfileException">The profile has no asset named <paramref name="key"/>.</exception>
    public Uri AssetUrl(string key)
    {
        var asset = Asset(key);
        return asset.Url ?? new Uri($"{RepositoryUrl}/releases/download/{Source.Tag}/{asset.Name}");
    }
}

/// <param name="Kind">"release" (uses <paramref name="Tag"/>) or "commit" (uses <paramref name="Sha"/>).</param>
public sealed record ProfileSource(string Kind, string? Tag = null, string? Sha = null)
{
    /// <summary>The tag of a release, the SHA of a commit.</summary>
    public string Version => Tag ?? Sha ?? "unknown";
}

/// <param name="Name">File name of a release asset of the source tag. Unused when <paramref name="Url"/> is set.</param>
/// <param name="Url">Direct URL, for files that are not release assets (source archive, pinned script).</param>
/// <param name="Include">For an archive: path prefixes to extract, relative to its top folder. Null or empty extracts everything.</param>
public sealed record ProfileAsset(string? Name, string Sha256, Uri? Url = null, IReadOnlyList<string>? Include = null);

/// <summary>Keys of the assets a profile must declare.</summary>
public static class ProfileAssetKeys
{
    public const string Relinker = "relinker";

    public const string Libs = "libs";

    public const string Scripts = "scripts";

    public const string UnwrapScript = "unwrapScript";
}

public sealed record ToolDefinition(string Version, Uri Url, string Sha256);

public sealed record RelinkerOptions
{
    public IReadOnlyList<string> WindowsArgs { get; init; } = [];

    public string? IntelArg { get; init; }
}
