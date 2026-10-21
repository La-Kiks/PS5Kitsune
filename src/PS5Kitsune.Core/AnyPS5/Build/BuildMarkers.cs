using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.AnyPS5.Build;

/// <summary>
/// Step markers: a file holding the commit a step completed for. Written after success, so a step a tool
/// left half done is never trusted; a marker for another commit means the step runs again.
/// </summary>
public sealed class BuildMarkers(BuildPaths paths, AnyPS5Profile profile)
{
    /// <exception cref="Profiles.InvalidProfileException">The profile has no build section.</exception>
    public string Commit => profile.RequireBuild().Commit;

    public bool IsCurrent(string name)
    {
        var marker = paths.Marker(name);
        return File.Exists(marker) && File.ReadAllText(marker).Trim() == Commit;
    }

    public void Clear(string name)
    {
        var marker = paths.Marker(name);
        if (File.Exists(marker))
        {
            File.Delete(marker);
        }
    }

    public async Task WriteAsync(string name, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(paths.State);
        var marker = paths.Marker(name);
        await File.WriteAllTextAsync(marker + ".tmp", Commit, cancellationToken).ConfigureAwait(false);
        File.Move(marker + ".tmp", marker, overwrite: true);
    }
}
