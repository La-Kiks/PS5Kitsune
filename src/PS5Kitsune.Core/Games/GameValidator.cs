using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Core.Games;

/// <summary>
/// Checks a game root before anything is copied, and reports every issue at once.
/// See docs/architecture.md, "V2 — Add a game", step 2.
/// </summary>
/// <param name="availableBytes">Free space for a destination path; replaced in tests.</param>
public sealed class GameValidator(Func<string, long> availableBytes)
{
    public GameValidator()
        : this(AvailableBytesOnDrive)
    {
    }

    public static IReadOnlyList<string> ModuleDirectoryNames { get; } = ["sce_module", "sce_modules", "prx"];

    /// <summary>A title ID becomes a folder name: ASCII letters and digits only.</summary>
    public static bool IsValidTitleId(string titleId) =>
        !string.IsNullOrEmpty(titleId) && titleId.All(char.IsAsciiLetterOrDigit);

    /// <param name="destination">Folder the game will be copied under; only its drive's free space matters.</param>
    /// <param name="ps4">What the active install offers PS4 games; null is <see cref="Ps4Support.Unknown"/>.</param>
    public GameValidation Validate(string gameRoot, string destination, Ps4Support? ps4 = null)
    {
        var issues = new List<ValidationIssue>();
        if (!File.Exists(Path.Combine(gameRoot, GameSourceResolver.ExecutableName)))
        {
            issues.Add(Blocking($"{GameSourceResolver.ExecutableName} is missing."));
        }

        var metadata = ReadMetadata(gameRoot, issues);
        if (metadata is { IsPs4: true })
        {
            issues.Add(Ps4Issue(ps4 ?? Ps4Support.Unknown));
        }

        List<string> modules = [.. ModuleDirectoryNames.Where(name => Directory.Exists(Path.Combine(gameRoot, name)))];
        if (modules.Contains("sce_module") && modules.Contains("sce_modules"))
        {
            issues.Add(Blocking("sce_module and sce_modules both exist: the relinker refuses this layout. Keep only one of them."));
        }

        if (modules.Count == 0)
        {
            issues.Add(new ValidationIssue(IssueSeverity.Warning, "No module folder (sce_module, sce_modules or prx): libraries kept beside eboot.bin are not converted yet."));
        }

        var size = FileTree.Size(gameRoot);
        var available = availableBytes(destination);
        if (available < size)
        {
            issues.Add(Blocking($"Not enough free space for {destination}: {size / 1024 / 1024} MiB needed, {available / 1024 / 1024} MiB available."));
        }

        return new GameValidation(metadata, modules, size, issues);
    }

    // param.json for a PS5 game; param.sfo, which PS4 games ship instead, when it is absent.
    private static GameMetadata? ReadMetadata(string gameRoot, List<ValidationIssue> issues)
    {
        var json = Path.Combine(gameRoot, "sce_sys", "param.json");
        var sfo = Path.Combine(gameRoot, "sce_sys", "param.sfo");
        try
        {
            if (File.Exists(json))
            {
                return ParamJson.Read(json);
            }

            if (File.Exists(sfo))
            {
                return ParamSfo.Read(sfo);
            }
        }
        catch (InvalidDataException exception)
        {
            issues.Add(Blocking(exception.Message));
            return null;
        }

        issues.Add(Blocking("sce_sys/param.json is missing (PS5) and so is sce_sys/param.sfo (PS4)."));
        return null;
    }

    private static ValidationIssue Ps4Issue(Ps4Support ps4) => ps4.DriverPresent
        ? new ValidationIssue(IssueSeverity.Warning, "PS4 game: PS4 support in AnyPS5 is experimental. Expect missing imports in the audit.")
        : Blocking($"PS4 game: {ps4.AnyPS5Name} cannot draw PS4 graphics yet (no libSceGnmDriver). PS4 support is in progress upstream.");

    private static ValidationIssue Blocking(string message) => new(IssueSeverity.Blocking, message);

    private static long AvailableBytesOnDrive(string path) =>
        new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!).AvailableFreeSpace;
}
