using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Checks.Vulkan;
using PS5Kitsune.Core.Games.Steps;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.Games;

/// <summary>
/// Adds a game in two stages: prepare (resolve the folder or zip and validate it), then add (run the add steps).
/// Without game.json, a new add resumes at the first step not done. See docs/architecture.md, "V2 — Add a game".
/// </summary>
public sealed partial class GameImporter(
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
    IVulkanProbeRunner probe,
    TimeProvider time,
    ILogger<GameImporter> logger) : IGameImporter
{
    public static readonly IReadOnlyList<string> Names =
        [CopyGameStep.StepName, CopyLibsStep.StepName, UnwrapSelfStep.StepName, RelinkStep.StepName, AuditStep.StepName, WriteGameRecordStep.StepName];

    public IReadOnlyList<string> StepNames => Names;

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

        await using var game = await PrepareAsync(input, cancellationToken).ConfigureAwait(false);
        if (game.IsBlocked || game.Metadata is not { } metadata)
        {
            return new GameRejected(game.Validation.Issues);
        }

        if (game.AlreadyAdded)
        {
            return new GameAlreadyAdded(metadata.TitleId, game.GameDirectory!);
        }

        return await AddAsync(game, toIntel, progress, onEvent, cancellationToken).ConfigureAwait(false);
    }

    public Task<PreparedGame> PrepareAsync(string input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Task.Run(() => PrepareCoreAsync(input, cancellationToken), cancellationToken);
    }

    public async Task<GameAdded> AddAsync(
        PreparedGame game, bool toIntel, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        ObjectDisposedException.ThrowIf(game.IsDisposed, game);
        if (!installer.IsInstalled())
        {
            throw new AnyPS5NotInstalledException();
        }

        if (game.IsBlocked || game.Metadata is not { } metadata)
        {
            throw new InvalidOperationException("The game has blocking issues; it cannot be added.");
        }

        var layoutGame = new GameLayout(layout.GameDirectory(metadata.TitleId), metadata.TitleId, platform);
        if (File.Exists(layoutGame.RecordFile))
        {
            throw new InvalidOperationException($"{metadata.TitleId} is already added.");
        }

        var install = installer.ReadRecord() ?? throw new AnyPS5NotInstalledException();
        var context = new GameImportContext(game.Source, metadata, game.Validation.ModuleDirectories, layoutGame, toIntel);
        var lavapipe = DefaultLavapipe(await probe.RunAsync(cancellationToken).ConfigureAwait(false));
        if (lavapipe)
        {
            var titleId = metadata.TitleId;
            LogLavapipeByDefault(titleId);
        }

        IStep[] steps =
        [
            new CopyGameStep(context),
            new CopyLibsStep(paths, layoutGame),
            new UnwrapSelfStep(context, paths, python, processRunner),
            new RelinkStep(context, paths, profile, processRunner),
            new AuditStep(context, paths, python, processRunner),
            new WriteGameRecordStep(context, install, time, lavapipe),
        ];
        await stepRunner.RunAsync(steps, progress, onEvent, cancellationToken).ConfigureAwait(false);

        var record = GameRecordStore.Read(layoutGame.RecordFile) ?? throw new InvalidOperationException($"{layoutGame.RecordFile} was not written.");
        return new GameAdded(record, layoutGame.Root, game.Validation.Warnings, context.Audit ?? AuditSummary.Read(layoutGame.AuditFile));
    }

    private async Task<PreparedGame> PrepareCoreAsync(string input, CancellationToken cancellationToken)
    {
        var path = Path.GetFullPath(input);
        var staging = File.Exists(path) && string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase)
            ? resolver.StagingFor(path)
            : null;
        try
        {
            GameSource source;
            try
            {
                source = await resolver.ResolveAsync(input, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (staging is not null && exception is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                throw new InvalidGameSourceException($"Cannot read {input}: {exception.Message}", exception);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var validation = validator.Validate(source.Root, layout.Games);
            var icon = Path.Combine(source.Root, "sce_sys", "icon0.png");
            var binaries = new List<GameBinary> { new(GameSourceResolver.ExecutableName, BinaryKinds.Detect(Path.Combine(source.Root, GameSourceResolver.ExecutableName))) };
            foreach (var directory in validation.ModuleDirectories)
            {
                foreach (var file in Directory.EnumerateFiles(Path.Combine(source.Root, directory), "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
                {
                    binaries.Add(new GameBinary(Path.GetRelativePath(source.Root, file).Replace('\\', '/'), BinaryKinds.Detect(file)));
                }
            }

            string? gameDirectory = null;
            var alreadyAdded = false;
            var resumes = false;
            if (validation.Metadata is { } metadata)
            {
                var game = new GameLayout(layout.GameDirectory(metadata.TitleId), metadata.TitleId, platform);
                gameDirectory = game.Root;
                alreadyAdded = File.Exists(game.RecordFile);
                resumes = !alreadyAdded && Directory.Exists(game.Root);
            }

            return new PreparedGame(source, validation, File.Exists(icon) ? icon : null, binaries, gameDirectory, alreadyAdded, resumes, LogStagingNotDeleted);
        }
        catch (Exception) when (staging is not null)
        {
            PreparedGame.DeleteStaging(staging, LogStagingNotDeleted);
            throw;
        }
    }

    /// <summary>
    /// Lavapipe starts on when Vulkan works but no GPU has shaderInt64 and bufferDeviceAddress (none at all counts); a failed probe
    /// or a missing loader leaves it off, since Lavapipe needs the loader too.
    /// </summary>
    internal static bool DefaultLavapipe(VulkanProbeOutcome outcome) =>
        outcome.Status != VulkanProbeStatus.Failed
        && outcome.Report is { LoaderFound: true } report
        && !report.Gpus.Any(gpu => gpu.SupportsAnyPS5);

    [LoggerMessage(Level = LogLevel.Information, Message = "No GPU supports AnyPS5: CPU rendering (Lavapipe) is on for {TitleId}.")]
    private partial void LogLavapipeByDefault(string titleId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not delete the staging folder {Staging}; it can be deleted by hand.")]
    private partial void LogStagingNotDeleted(Exception exception, string staging);
}
