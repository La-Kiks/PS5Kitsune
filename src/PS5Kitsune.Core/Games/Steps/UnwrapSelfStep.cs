using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Processes;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.Games.Steps;

/// <summary>
/// Unwraps the SELF containers among eboot.bin and the module files: the relinker skips an unwrapped SELF
/// module with only a warning. Each original goes to .ps5kitsune/originals first, outside app0, because the
/// relinker may skip files whose name extends another module's.
/// </summary>
public sealed class UnwrapSelfStep(GameImportContext context, AnyPS5Paths paths, PythonEmbeddableTool python, IProcessRunner processRunner) : IStep
{
    public string Name => "Unwrap SELF files";

    public bool IsAlreadyDone() => !FilesToUnwrap().Any();

    public async Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        foreach (var relative in FilesToUnwrap().ToList())
        {
            var original = Path.Combine(context.Game.App0, relative);
            var backup = Path.Combine(context.Game.Originals, relative);
            // Only a SELF is backed up. A partial output from an interrupted run must never replace the backup.
            if (BinaryKinds.Detect(original) == BinaryKind.Self)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                File.Move(original, backup, overwrite: true);
            }

            progress?.Report(new StepProgress($"Unwrapping {relative}"));
            var errors = new OutputTail();
            // The script writes its output in one go: a kill mid-write must not leave a truncated ELF at the real path.
            var partial = original + ".partial";
            File.Delete(partial);
            var spec = python.CreateScriptSpec(paths.UnwrapSelf, [backup, partial]);
            var result = await processRunner.RunAsync(spec, errors.Collect, cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                File.Delete(partial);
                throw new GameImportException(
                    $"unwrap_self.py could not unwrap {relative} (exit code {result.ExitCode}): the game must come already unwrapped from the dump tool.",
                    errors.ToString());
            }

            File.Move(partial, original, overwrite: true);
        }
    }

    private IEnumerable<string> FilesToUnwrap() => Candidates().Where(NeedsUnwrap);

    // eboot.bin, then the files of the module folders, as paths relative to app0. Backups count too,
    // so a file moved to originals by an interrupted run is still found.
    private IEnumerable<string> Candidates()
    {
        yield return GameSourceResolver.ExecutableName;
        foreach (var directory in context.ModuleDirectories)
        {
            var files = FileTree.RelativeFiles(Path.Combine(context.Game.App0, directory))
                .Concat(FileTree.RelativeFiles(Path.Combine(context.Game.Originals, directory)))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal);
            foreach (var file in files)
            {
                yield return Path.Combine(directory, file);
            }
        }
    }

    // A SELF in app0, or a backup whose unwrapped copy is missing or incomplete.
    private bool NeedsUnwrap(string relative)
    {
        var current = BinaryKinds.Detect(Path.Combine(context.Game.App0, relative));
        return current == BinaryKind.Self
            || (current != BinaryKind.Elf && File.Exists(Path.Combine(context.Game.Originals, relative)));
    }
}
