namespace PS5Kitsune.Core.AnyPS5;

/// <summary>Contents of anyps5/current/install.json.</summary>
/// <param name="Mode">"release" until build mode exists (milestone 5).</param>
/// <param name="Version">Tag of the release.</param>
/// <param name="UnwrapScript">URL of unwrap_self.py; it holds the pinned commit.</param>
public sealed record AnyPS5InstallRecord(int SchemaVersion, string Profile, string Mode, string Version, Uri UnwrapScript, DateTimeOffset InstalledAt)
{
    public const int CurrentSchemaVersion = 1;
}
