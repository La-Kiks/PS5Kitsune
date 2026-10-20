using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.Games.Steps;

/// <summary>Copies the game root into app0 as it is. Done when every source file is there with the same size.</summary>
public sealed class CopyGameStep(GameImportContext context) : IStep
{
    public string Name => "Copy the game";

    public bool IsAlreadyDone() => FileTree.RelativeFiles(context.Source.Root).All(IsCopied);

    public Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken) =>
        Task.Run(() => FileTree.Copy(context.Source.Root, context.Game.App0, IsCopied, cancellationToken), cancellationToken);

    // An unwrapped file has a new size in app0; its copy as supplied is in originals.
    private bool IsCopied(string relative)
    {
        var source = Path.Combine(context.Source.Root, relative);
        return FileTree.SameSize(source, Path.Combine(context.Game.App0, relative))
            || FileTree.SameSize(source, Path.Combine(context.Game.Originals, relative));
    }
}
