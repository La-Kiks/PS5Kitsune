namespace PS5Kitsune.Core.AnyPS5;

/// <summary>Contents of anyps5/current/install.json. Schema 1 (release only, no commit) is still read.</summary>
/// <param name="Version">The release tag, or the first 7 characters of the commit in build mode.</param>
/// <param name="Commit">The commit built; null in release mode.</param>
/// <param name="UnwrapScript">URL of unwrap_self.py in release mode, which holds its pinned commit; null in build mode, where it comes from the clone.</param>
public sealed record AnyPS5InstallRecord(
    int SchemaVersion,
    string Profile,
    AnyPS5Mode Mode,
    string Version,
    string? Commit,
    Uri? UnwrapScript,
    DateTimeOffset InstalledAt)
{
    public const int CurrentSchemaVersion = 2;
}
