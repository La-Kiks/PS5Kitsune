using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Launch;
using PS5Kitsune.Core.Profiles;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace PS5Kitsune.Core.Tools;

/// <summary>
/// Mesa for Windows (pal1000/mesa-dist-win), for Lavapipe only: its manifest and driver are extracted side by side, the manifest naming
/// the driver as .\vulkan_lvp.dll. See docs/architecture.md, "Portable tools".
/// </summary>
public sealed class MesaTool(ToolDefinition definition, DataLayout layout, IDownloader downloader)
    : ArchiveTool(ToolId, definition, layout, downloader), ILavapipe
{
    public const string ToolId = "mesa";

    private const string ManifestName = "lvp_icd.x86_64.json";

    private const string DriverName = "vulkan_lvp.dll";

    private static readonly string[] Kept = [$"x64/{ManifestName}", $"x64/{DriverName}"];

    public string Manifest => Path.Combine(InstallDirectory, ManifestName);

    protected override IEnumerable<string> RequiredFiles => [Manifest, Path.Combine(InstallDirectory, DriverName)];

    /// <summary>Lavapipe is a driver, not a program to run: the version is the profile's.</summary>
    public override Task<string> GetVersionAsync(CancellationToken cancellationToken = default) => Task.FromResult(Definition.Version);

    // The x64 folder alone is 381 MB, so only Lavapipe is kept (56 MB). The reader walks the solid archive once; opening entries one by one
    // decompresses it again for each (14 s against 113 s, measured on 26.2.4).
    protected override Task ExtractAsync(string archive, string directory, CancellationToken cancellationToken) =>
        Task.Run(() => Extract(archive, directory, cancellationToken), cancellationToken);

    private static void Extract(string archive, string directory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        var found = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            using var sevenZip = SevenZipArchive.OpenArchive(archive, new ReaderOptions());
            using var reader = sevenZip.ExtractAllEntries();
            while (found.Count < Kept.Length && reader.MoveToNextEntry())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var key = reader.Entry.Key?.Replace('\\', '/');
                if (key is null || reader.Entry.IsDirectory || !Kept.Contains(key))
                {
                    continue;
                }

                using var output = File.Create(Path.Combine(directory, Path.GetFileName(key)));
                reader.WriteEntryTo(output);
                found.Add(key);
            }
        }
        catch (SharpCompressException exception)
        {
            throw new InvalidDataException($"{archive} is not a readable Mesa archive: {exception.Message}", exception);
        }

        if (Kept.FirstOrDefault(name => !found.Contains(name)) is { } missing)
        {
            throw new InvalidDataException($"{archive} does not contain {missing}.");
        }
    }
}
