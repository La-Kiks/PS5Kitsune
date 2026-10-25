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

    /// <summary>What build mode compiles; null when the profile only supports release mode.</summary>
    public ProfileBuild? Build { get; init; }

    public IReadOnlyDictionary<string, ProfileAsset> Assets { get; init; } = new Dictionary<string, ProfileAsset>();

    public IReadOnlyDictionary<string, ToolDefinition> Tools { get; init; } = new Dictionary<string, ToolDefinition>();

    /// <summary>The Noto font substitutes passed to games through ANYPS5_SYSTEM_FONTS; empty when the profile has none.</summary>
    public IReadOnlyList<FontFile> Fonts { get; init; } = [];

    public RelinkerOptions Relinker { get; init; } = new();

    /// <summary>PC check thresholds; a profile without a "checks" section gets the defaults.</summary>
    public CheckThresholds Checks { get; init; } = new();

    /// <summary>Known AnyPS5 errors, searched in a game's log after it exits; a profile without the section has no rules.</summary>
    public ProfileDiagnostics Diagnostics { get; init; } = new();

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

    /// <exception cref="InvalidProfileException">The profile has no build section.</exception>
    public ProfileBuild RequireBuild() =>
        Build ?? throw new InvalidProfileException($"Profile {Id} has no build section: build mode is not available.");
}

/// <param name="Kind">"release" (uses <paramref name="Tag"/>) or "commit" (uses <paramref name="Sha"/>).</param>
/// <param name="PublishedAt">When the release was published, shown on the Mode screen; null when unknown.</param>
public sealed record ProfileSource(string Kind, string? Tag = null, string? Sha = null, DateTimeOffset? PublishedAt = null)
{
    /// <summary>The tag of a release, the SHA of a commit.</summary>
    public string Version => Tag ?? Sha ?? "unknown";
}

/// <param name="Repository">Git URL of AnyPS5, over HTTPS.</param>
/// <param name="Commit">Full lowercase SHA of the commit to build.</param>
public sealed record ProfileBuild(Uri Repository, string Commit);

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

/// <param name="Size">Download size of the archive in bytes, shown on the Tools screen; null when the profile does not give it.</param>
public sealed record ToolDefinition(string Version, Uri Url, string Sha256, long? Size = null);

/// <param name="Name">File name in &lt;root&gt;/fonts, as AnyPS5 looks it up (for example NotoSans-Regular.ttf).</param>
/// <param name="Size">Bytes, for the download progress.</param>
public sealed record FontFile(string Name, Uri Url, string Sha256, long Size);

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

    /// <summary>Free space wanted for build mode: the clone, the build folder, the tools and a game.</summary>
    public long BuildMinFreeDiskGiB { get; init; } = 10;

    /// <summary>NVIDIA driver versions, as Vulkan's driverInfo reports them, known to crash AnyPS5 games.</summary>
    public IReadOnlyList<string> NvidiaBadDrivers { get; init; } = [];
}

/// <summary>The crash diagnosis rules. See docs/architecture.md, "Crash diagnosis".</summary>
public sealed record ProfileDiagnostics
{
    /// <summary>The page Report opens after copying its text; null when the profile names none.</summary>
    public Uri? ReportUrl { get; init; }

    /// <summary>Tried in order: the first rule whose conditions are met wins.</summary>
    public IReadOnlyList<DiagnosticRule> Rules { get; init; } = [];
}

/// <summary>A known error: .NET regexes searched in every line of the log, and exit codes.</summary>
public sealed record DiagnosticRule
{
    public const string MatchAll = "all";

    public const string MatchAny = "any";

    /// <summary>Names the explanation and the fix in the app; several rules may share one.</summary>
    public required string Id { get; init; }

    public IReadOnlyList<string> Log { get; init; } = [];

    /// <summary>Hex strings such as "0xC000001D", compared with the exit code as a 32-bit unsigned value.</summary>
    public IReadOnlyList<string> ExitCodes { get; init; } = [];

    /// <summary>
    /// "all": every regex matches somewhere in the log and, when exit codes are given, the exit code is one of them.
    /// "any": one regex matches, or the exit code is one of them.
    /// </summary>
    public string Match { get; init; } = MatchAll;

    /// <summary>Values for the explanation that no named group of the regexes supplies, for example the variable of a message that names none.</summary>
    public IReadOnlyDictionary<string, string> Captures { get; init; } = new Dictionary<string, string>();
}
