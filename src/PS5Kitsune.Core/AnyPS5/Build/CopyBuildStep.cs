using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.AnyPS5.Build;

/// <summary>
/// Copies the build into anyps5\current so it matches a release install: relinker, libs (.prx + the 3 WinLibs DLLs)
/// and the scripts of the same commit. libs and scripts are mirrored on execution; the done-check ignores extra files,
/// such as the __pycache__ Python writes beside the scripts.
/// </summary>
public sealed class CopyBuildStep(BuildPaths build, AnyPS5InstallPaths current, WinLibsTool winlibs) : IStep
{
    public string Name => "Copy the build";

    public bool IsAlreadyDone() =>
        Missing() is null
        && Plan().All(copy => File.Exists(copy.Source) && FileTree.SameSize(copy.Source, copy.Target) && FileHash.Sha256(copy.Source) == FileHash.Sha256(copy.Target));

    public Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        if (Missing() is { } missing)
        {
            throw missing;
        }

        var plan = Plan();
        foreach (var (source, _) in plan.Where(copy => !File.Exists(copy.Source)))
        {
            throw new FileNotFoundException($"The build did not produce {source}.", source);
        }

        foreach (var (source, target) in plan)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: true);
        }

        var planned = plan.Select(copy => Path.GetFullPath(copy.Target)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var root in (string[])[current.Libs, current.Scripts])
        {
            foreach (var stray in FileTree.RelativeFiles(root).Select(relative => Path.GetFullPath(Path.Combine(root, relative))).Where(path => !planned.Contains(path)).ToList())
            {
                File.Delete(stray);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>What a release has and the build did not produce, checked before copying anything; null when complete.</summary>
    private FileNotFoundException? Missing()
    {
        string[] required = [build.BuiltRelinker, Path.Combine(build.Source, "tools", "import_audit.py"), Path.Combine(build.Source, "tools", "unwrap_self.py")];
        if (required.FirstOrDefault(path => !File.Exists(path)) is { } absent)
        {
            return new FileNotFoundException($"The build did not produce {absent}.", absent);
        }

        return Directory.Exists(build.BuiltLibs) && Directory.EnumerateFiles(build.BuiltLibs, "*.prx").Any()
            ? null
            : new FileNotFoundException($"The build produced no .prx library in {build.BuiltLibs}.", build.BuiltLibs);
    }

    private List<(string Source, string Target)> Plan()
    {
        var plan = new List<(string Source, string Target)> { (build.BuiltRelinker, current.Relinker) };
        if (Directory.Exists(build.BuiltLibs))
        {
            plan.AddRange(Directory.EnumerateFiles(build.BuiltLibs, "*.prx").Select(prx => (prx, Path.Combine(current.Libs, Path.GetFileName(prx)))));
        }

        plan.AddRange(winlibs.RuntimeDlls.Select(dll => (dll, Path.Combine(current.Libs, Path.GetFileName(dll)))));
        foreach (var folder in (string[])["tools", Path.Combine("core", "libs", "prx")])
        {
            var sourceRoot = Path.Combine(build.Source, folder);
            plan.AddRange(FileTree.RelativeFiles(sourceRoot).Select(relative => (Path.Combine(sourceRoot, relative), Path.Combine(current.Scripts, folder, relative))));
        }

        return plan;
    }
}
