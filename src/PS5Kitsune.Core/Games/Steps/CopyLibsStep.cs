using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.Games.Steps;

/// <summary>
/// Copies the install's libs/ beside the game. The folder is fully controlled by the app,
/// so no Sony .prx can slip in (error 193).
/// </summary>
public sealed class CopyLibsStep(AnyPS5Paths paths, GameLayout game) : IStep
{
    public string Name => "Copy the AnyPS5 libraries";

    public bool IsAlreadyDone() => LibFiles().All(IsCopied);

    public Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken) =>
        Task.Run(() => FileTree.Copy(paths.Libs, game.Libs, relative => relative == ZipExtraction.MarkerFileName || IsCopied(relative), cancellationToken), cancellationToken);

    private IEnumerable<string> LibFiles() =>
        FileTree.RelativeFiles(paths.Libs).Where(relative => relative != ZipExtraction.MarkerFileName);

    private bool IsCopied(string relative) =>
        FileTree.SameSize(Path.Combine(paths.Libs, relative), Path.Combine(game.Libs, relative));
}
