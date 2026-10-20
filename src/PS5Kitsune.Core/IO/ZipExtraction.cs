using System.IO.Compression;

namespace PS5Kitsune.Core.IO;

/// <summary>
/// Extracts part of a zip through a sibling staging folder swapped in at the end, so an interrupted
/// extraction never leaves a half-filled target. A marker file can record the archive hash.
/// </summary>
public static class ZipExtraction
{
    internal const string MarkerFileName = ".ps5kitsune-extracted";

    /// <summary>Keeps every entry at its own path.</summary>
    public static Func<string, string?> Everything { get; } = static name => name;

    /// <summary>Keeps the entries under <paramref name="prefix"/> (for example "libs/"), relative to it.</summary>
    public static Func<string, string?> UnderPrefix(string prefix) =>
        name => name.StartsWith(prefix, StringComparison.Ordinal) && name.Length > prefix.Length ? name[prefix.Length..] : null;

    /// <summary>
    /// Drops the archive's top folder (GitHub source archives have one) and keeps the entries under one of
    /// <paramref name="includes"/>, tree layout kept. No includes keeps everything.
    /// </summary>
    public static Func<string, string?> StripTopFolder(IReadOnlyList<string> includes) => name =>
    {
        var slash = name.IndexOf('/', StringComparison.Ordinal);
        if (slash < 0)
        {
            return null;
        }

        var inner = name[(slash + 1)..];
        return includes.Count == 0 || includes.Any(include => inner.StartsWith(include, StringComparison.Ordinal)) ? inner : null;
    };

    /// <param name="archiveSha256">Recorded in a marker file inside the target; null writes no marker.</param>
    /// <param name="map">Entry name ('/' separators) to a path relative to the target, or null to skip the entry.</param>
    /// <exception cref="InvalidDataException">An entry would land outside the target.</exception>
    public static async Task ExtractAsync(string archive, string target, string? archiveSha256, Func<string, string?> map, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(map);
        var staging = Path.GetFullPath(target) + ".staging";
        if (Directory.Exists(staging))
        {
            Directory.Delete(staging, recursive: true);
        }

        Directory.CreateDirectory(staging);
        var stagingRoot = staging + Path.DirectorySeparatorChar;
        using (var zip = ZipFile.OpenRead(archive))
        {
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.EndsWith('/') || map(entry.FullName) is not { } relative)
                {
                    continue;
                }

                var destination = Path.GetFullPath(Path.Combine(staging, relative));
                if (!destination.StartsWith(stagingRoot, StringComparison.Ordinal))
                {
                    throw new InvalidDataException($"{archive}: entry {entry.FullName} would be extracted outside {target}.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await using var source = entry.Open();
                await using var output = File.Create(destination);
                await source.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }
        }

        if (archiveSha256 is not null)
        {
            await File.WriteAllTextAsync(Path.Combine(staging, MarkerFileName), archiveSha256, cancellationToken).ConfigureAwait(false);
        }

        if (Directory.Exists(target))
        {
            Directory.Delete(target, recursive: true);
        }

        Directory.Move(staging, target);
    }

    /// <summary>True when <paramref name="target"/> was extracted from the archive with this hash.</summary>
    public static bool IsExtracted(string target, string archiveSha256)
    {
        var marker = Path.Combine(target, MarkerFileName);
        return File.Exists(marker) && File.ReadAllText(marker).Trim() == archiveSha256;
    }
}
