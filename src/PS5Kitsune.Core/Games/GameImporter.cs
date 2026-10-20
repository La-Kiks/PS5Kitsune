using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games.Steps;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.Games;

/// <summary>
/// Adds a game: resolve the folder or zip, validate it, then run the add steps. Without game.json,
/// a new add resumes at the first step not done. See docs/architecture.md, "V2 — Add a game".
/// </summary>
public sealed class GameImporter(
    DataLayout layout,
    IPlatform platform,
    AnyPS5Profile profile,
    AnyPS5Paths paths,
    AnyPS5Installer installer,
    GameSourceResolver resolver,
    GameValidator validator,
    PythonEmbeddableTool python,
    IProcessRunner processRunner,
    StepRunner stepRunner,
    TimeProvider time)
{
    /// <param name="toIntel">Convert for an Intel CPU (relinker Intel option).</param>
    /// <exception cref="AnyPS5NotInstalledException">Run install first.</exception>
    /// <exception cref="InvalidGameSourceException">The input is not a game folder or zip.</exception>
    /// <exception cref="StepFailedException">A step failed; running add again resumes.</exception>
    public async Task<AddGameResult> AddAsync(
        string input, bool toIntel, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default)
    {
        if (!installer.IsInstalled())
        {
            throw new AnyPS5NotInstalledException();
        }

        var source = await resolver.ResolveAsync(input, cancellationToken).ConfigureAwait(false);
        CopyGameStep? copy = null;
        try
        {
            var validation = validator.Validate(source.Root, layout.Games);
            if (validation.IsBlocked || validation.Metadata is not { } metadata)
            {
                return new GameRejected(validation.Issues);
            }

            var game = new GameLayout(layout.GameDirectory(metadata.TitleId), metadata.TitleId, platform);
            if (File.Exists(game.RecordFile))
            {
                return new GameAlreadyAdded(metadata.TitleId, game.Root);
            }

            var context = new GameImportContext(source, metadata, validation.ModuleDirectories, game, toIntel);
            copy = new CopyGameStep(context);
            IStep[] steps =
            [
                copy,
                new CopyLibsStep(paths, game),
                new UnwrapSelfStep(context, paths, python, processRunner),
                new RelinkStep(context, paths, profile, processRunner),
                new AuditStep(context, paths, python, processRunner),
                new WriteGameRecordStep(context, profile, time),
            ];
            await stepRunner.RunAsync(steps, progress, onEvent, cancellationToken).ConfigureAwait(false);

            var record = GameRecordStore.Read(game.RecordFile) ?? throw new InvalidOperationException($"{game.RecordFile} was not written.");
            return new GameAdded(record, game.Root, validation.Warnings);
        }
        finally
        {
            // An extracted zip is only needed until the game is copied; keep it when the copy did not complete.
            if (source.StagingDirectory is { } staging && Directory.Exists(staging) && (copy is null || copy.IsAlreadyDone()))
            {
                Directory.Delete(staging, recursive: true);
            }
        }
    }
}
