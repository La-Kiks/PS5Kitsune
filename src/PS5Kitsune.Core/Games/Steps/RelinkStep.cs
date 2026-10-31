using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Processes;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Games.Steps;

/// <summary>
/// Runs the relinker from the game folder: relinker &lt;profile args&gt; [--to-intel] app0/eboot.bin &lt;game executable&gt;.
/// Done when the marker written after a successful run exists, with the executable and its registry.
/// </summary>
public sealed class RelinkStep(GameImportContext context, AnyPS5Paths paths, AnyPS5Profile profile, IProcessRunner processRunner) : IStep
{
    public const string StepName = "Convert with the relinker";

    public string Name => StepName;

    public bool IsAlreadyDone() => File.Exists(context.Game.RelinkedMarker) && File.Exists(context.Game.Executable) && File.Exists(context.Game.Registry);

    public async Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        DeletePreviousOutputs();
        List<string> arguments = [.. profile.Relinker.Args];
        if (context.ToIntel && profile.Relinker.IntelArg is { } intelArg)
        {
            arguments.Add(intelArg);
        }

        arguments.Add(Path.Combine("app0", GameSourceResolver.ExecutableName));
        arguments.Add(context.Game.ExecutableName);

        var errors = new OutputTail();
        var spec = new ProcessSpec(paths.Relinker, arguments) { WorkingDirectory = context.Game.Root };
        var result = await processRunner.RunAsync(spec, errors.Collect, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            var intelNote = context.ToIntel
                ? $" It ran with {profile.Relinker.IntelArg}: the game may use an AMD-only instruction that cannot be converted for Intel CPUs."
                : "";
            throw new GameImportException($"The relinker failed with exit code {result.ExitCode}.{intelNote}", errors.ToString());
        }

        Directory.CreateDirectory(context.Game.Meta);
        await File.WriteAllTextAsync(context.Game.RelinkedMarker, "", cancellationToken).ConfigureAwait(false);
    }

    // A rerun must never mix old and new outputs: the relinker's own files go, the game's modules stay.
    private void DeletePreviousOutputs()
    {
        var game = context.Game;
        if (!Directory.Exists(game.Root))
        {
            return;
        }

        if (Directory.Exists(game.Meta))
        {
            File.Delete(game.RelinkedMarker);
        }

        File.Delete(game.Executable);
        foreach (var file in Directory.EnumerateFiles(game.Root, "*.registry.json").Concat(Directory.EnumerateFiles(game.Root, "*.needed.json")))
        {
            File.Delete(file);
        }

        if (Directory.Exists(game.App0))
        {
            foreach (var file in Directory.EnumerateFiles(game.App0, "*.guest.prx", SearchOption.AllDirectories))
            {
                File.Delete(file);
            }
        }
    }
}
