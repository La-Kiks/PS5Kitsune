using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Processes;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Games.Steps;

/// <summary>
/// Runs the relinker from the game folder: relinker --windows --registry [--to-intel] app0/eboot.bin &lt;titleId&gt;.exe.
/// Done when the executable and its registry exist.
/// </summary>
public sealed class RelinkStep(GameImportContext context, AnyPS5Paths paths, AnyPS5Profile profile, IProcessRunner processRunner) : IStep
{
    public string Name => "Convert with the relinker";

    public bool IsAlreadyDone() => File.Exists(context.Game.Executable) && File.Exists(context.Game.Registry);

    public async Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        List<string> arguments = [.. profile.Relinker.WindowsArgs];
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
            throw new GameImportException($"The relinker failed with exit code {result.ExitCode}.{intelNote}{Environment.NewLine}{errors}");
        }
    }
}
