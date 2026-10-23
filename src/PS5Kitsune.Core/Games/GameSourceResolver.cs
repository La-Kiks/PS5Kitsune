using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Core.Games;

/// <summary>
/// Turns what the user gave (a folder or a .zip) into the folder holding eboot.bin: the input itself,
/// its app0, or its single subfolder (or that subfolder's app0). A zip is extracted to downloads/staging first.
/// </summary>
public sealed class GameSourceResolver(DataLayout layout)
{
    public const string ExecutableName = "eboot.bin";

    /// <summary>Where a zip is extracted: downloads\staging\&lt;zip name without extension&gt;.</summary>
    public string StagingFor(string zipPath) => Path.Combine(layout.Staging, Path.GetFileNameWithoutExtension(zipPath));

    /// <exception cref="InvalidGameSourceException">The input does not exist or holds no eboot.bin where expected.</exception>
    public async Task<GameSource> ResolveAsync(string input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var path = Path.GetFullPath(input);
        if (File.Exists(path) && string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            var staging = StagingFor(path);
            await ZipExtraction.ExtractAsync(path, staging, archiveSha256: null, ZipExtraction.Everything, cancellationToken).ConfigureAwait(false);
            return new GameSource(FindRoot(staging, input), staging);
        }

        if (Directory.Exists(path))
        {
            return new GameSource(FindRoot(path, input), null);
        }

        throw new InvalidGameSourceException($"{input} is neither a folder nor a .zip file.");
    }

    private static string FindRoot(string folder, string input)
    {
        List<string> candidates = [folder, Path.Combine(folder, "app0")];
        if (Directory.GetDirectories(folder) is [var single])
        {
            candidates.Add(single);
            candidates.Add(Path.Combine(single, "app0"));
        }

        return candidates.FirstOrDefault(candidate => File.Exists(Path.Combine(candidate, ExecutableName)))
            ?? throw new InvalidGameSourceException($"No {ExecutableName} found in {input}, in its app0 folder or in its single subfolder.");
    }
}
