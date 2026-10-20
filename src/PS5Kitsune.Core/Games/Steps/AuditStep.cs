using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Processes;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.Games.Steps;

/// <summary>
/// Runs import_audit.py on every registry the relinker wrote, reads audit.json and stores the summary in the context.
/// </summary>
public sealed class AuditStep(GameImportContext context, AnyPS5Paths paths, PythonEmbeddableTool python, IProcessRunner processRunner) : IStep
{
    public string Name => "Audit the imports";

    // Always runs again: it is quick, and its result feeds the game record.
    public bool IsAlreadyDone() => false;

    public async Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        var game = context.Game;
        List<string> registries = [.. Directory.GetFiles(game.Root, "*.registry.json").Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)];
        if (registries.Count == 0)
        {
            throw new GameImportException($"No *.registry.json in {game.Root}: the relinker did not write its registry.");
        }

        List<string> arguments = [.. registries, "--libs", "libs"];
        // Relinker v0.1.2 writes PE <module>.guest.prx files beside the original modules, and import_audit.py
        // reads every file of a --modules folder as an ELF. So the audit gets a copy of the ELF modules only.
        if (Directory.Exists(game.AuditModules))
        {
            Directory.Delete(game.AuditModules, recursive: true);
        }

        foreach (var directory in context.ModuleDirectories)
        {
            var source = Path.Combine(game.App0, directory);
            var staged = Path.Combine(game.AuditModules, directory);
            Directory.CreateDirectory(staged);
            foreach (var relative in FileTree.RelativeFiles(source).Where(relative => BinaryKinds.Detect(Path.Combine(source, relative)) == BinaryKind.Elf))
            {
                var target = Path.Combine(staged, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(Path.Combine(source, relative), target, overwrite: true);
            }

            arguments.Add("--modules");
            arguments.Add(Path.Combine(Path.GetRelativePath(game.Root, game.AuditModules), directory));
        }

        arguments.Add("--json");
        arguments.Add(Path.GetFileName(game.AuditFile));
        File.Delete(game.AuditFile);

        var errors = new OutputTail();
        var spec = python.CreateScriptSpec(paths.ImportAudit, arguments) with { WorkingDirectory = game.Root };
        var result = await processRunner.RunAsync(spec, errors.Collect, cancellationToken).ConfigureAwait(false);

        // 0: nothing blocks; 1: absent imports or missing libraries (the verdict shows it); 2: unreadable input.
        if (result.ExitCode is not (0 or 1))
        {
            throw new GameImportException($"import_audit.py could not read its input (exit code {result.ExitCode}).{Environment.NewLine}{errors}");
        }

        context.Audit = AuditSummary.Read(game.AuditFile);
    }
}
