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
    public GameValidation Validate(string gameRoot, string destination)
    {
        var issues = new List<ValidationIssue>();
        if (!File.Exists(Path.Combine(gameRoot, GameSourceResolver.ExecutableName)))
        {
            issues.Add(Blocking($"{GameSourceResolver.ExecutableName} is missing."));
        }

        var metadata = ReadMetadata(gameRoot, issues);

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

    private static GameMetadata? ReadMetadata(string gameRoot, List<ValidationIssue> issues)
    {
        var path = Path.Combine(gameRoot, "sce_sys", "param.json");
        if (!File.Exists(path))
        {
            issues.Add(Blocking("sce_sys/param.json is missing."));
            return null;
        }

        try
        {
            return ParamJson.Read(path);
        }
        catch (InvalidDataException exception)
        {
            issues.Add(Blocking(exception.Message));
            return null;
        }
    }

    private static ValidationIssue Blocking(string message) => new(IssueSeverity.Blocking, message);

    private static long AvailableBytesOnDrive(string path) =>
        new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!).AvailableFreeSpace;
}
