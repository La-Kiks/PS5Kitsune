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

    /// <summary>PC check thresholds; a profile without a "checks" section gets the defaults.</summary>
    public CheckThresholds Checks { get; init; } = new();

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
/// <param name="PublishedAt">When the release was published, shown on the Mode screen; null when unknown.</param>
public sealed record ProfileSource(string Kind, string? Tag = null, string? Sha = null, DateTimeOffset? PublishedAt = null)
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

/// <summary>PC check thresholds that depend on the AnyPS5 version.</summary>
public sealed record CheckThresholds
{
    /// <summary>Memory a game can commit; RAM + page file must reach it.</summary>
    public long CommitLimitMiB { get; init; } = 13824;

    /// <summary>Free space wanted on the data root's drive for AnyPS5 and a game.</summary>
    public long MinFreeDiskGiB { get; init; } = 5;

    /// <summary>NVIDIA driver versions, as Vulkan's driverInfo reports them, known to crash AnyPS5 games.</summary>
    public IReadOnlyList<string> NvidiaBadDrivers { get; init; } = [];
}
