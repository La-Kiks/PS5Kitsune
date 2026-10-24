using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.Games.Steps;

/// <summary>
/// Reconvert's first step: moves the conversion outputs into .ps5kitsune/previous, so a failure can put them back.
/// Windows lets a running executable be renamed, so the executable is first opened exclusively: a running game stops the step.
/// </summary>
public sealed class SetAsideStep(GameLayout game) : IStep
{
    public const string StepName = "Set the old conversion aside";

    public string Name => StepName;

    public bool IsAlreadyDone() => false;

    public Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        try
        {
            if (File.Exists(game.Executable))
            {
                using (new FileStream(game.Executable, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                }
            }

            ConversionBackup.SetAside(game);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new GameImportException($"Cannot set the old conversion aside: {exception.Message}", "Close the game if it is running.");
        }

        return Task.CompletedTask;
    }
}
