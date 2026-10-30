namespace PS5Kitsune.Core.Profiles;

/// <summary>The values of a profile's "os" field; <see cref="Abstractions.IPlatform.Os"/> gives the running OS's.</summary>
public static class ProfileOs
{
    public const string Windows = "windows";

    public const string Linux = "linux";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal) { Windows, Linux };
}
