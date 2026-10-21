namespace PS5Kitsune.Core.AnyPS5;

public enum AnyPS5InstallStatus
{
    /// <summary>No step done and no install.json.</summary>
    NotInstalled,

    /// <summary>Every step done: the files have the profile's hashes and install.json describes this profile.</summary>
    Installed,

    /// <summary>Anything in between, including an install.json for another profile or tag.</summary>
    Incomplete,
}

/// <param name="Record">install.json as found, even when it describes another install; null when missing or unreadable.</param>
public sealed record AnyPS5InstallState(AnyPS5InstallStatus Status, AnyPS5InstallRecord? Record);
