namespace PS5Kitsune.Core.Profiles;

/// <summary>
/// An AnyPS5 version and everything that depends on it: assets, tools, relinker options.
/// See docs/architecture.md, "AnyPS5 profiles".
/// </summary>
public sealed record AnyPS5Profile
{
    public const int CurrentSchemaVersion = 1;

    public required int SchemaVersion { get; init; }

    public required string Id { get; init; }

    /// <summary>True once the profile has been run end to end with DOOM for PS5.</summary>
    public bool Validated { get; init; }

    public required ProfileSource Source { get; init; }

    public ProfileSource? AlternateSource { get; init; }

    public IReadOnlyDictionary<string, ProfileAsset> Assets { get; init; } = new Dictionary<string, ProfileAsset>();

    public IReadOnlyDictionary<string, ToolDefinition> Tools { get; init; } = new Dictionary<string, ToolDefinition>();

    public RelinkerOptions Relinker { get; init; } = new();
}

/// <param name="Kind">"release" (uses <paramref name="Tag"/>) or "commit" (uses <paramref name="Sha"/>).</param>
public sealed record ProfileSource(string Kind, string? Tag = null, string? Sha = null);

public sealed record ProfileAsset(string Name, string Sha256);

public sealed record ToolDefinition(string Version, Uri Url, string Sha256);

public sealed record RelinkerOptions
{
    public IReadOnlyList<string> WindowsArgs { get; init; } = [];

    public string? IntelArg { get; init; }
}
