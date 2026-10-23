namespace PS5Kitsune.Core.Games;

/// <param name="RelativePath">Relative to the game root, '/' separators.</param>
public sealed record GameBinary(string RelativePath, BinaryKind Kind);

/// <summary>
/// A game read and validated, not yet added: what the summary shows. It owns the staging folder of an extracted zip,
/// deleted by <see cref="DisposeAsync"/>; a kept staging folder is never reused, since a zip is extracted again on every add.
/// </summary>
/// <param name="gameDirectory">&lt;root&gt;\games\&lt;titleId&gt;; null without metadata.</param>
/// <param name="alreadyAdded">The game folder holds a game.json.</param>
/// <param name="resumes">The game folder exists without game.json: a previous add stopped.</param>
/// <param name="onCleanupFailed">Told the exception and the folder when a staging folder cannot be deleted.</param>
public sealed class PreparedGame(
    GameSource source,
    GameValidation validation,
    string? iconPath,
    IReadOnlyList<GameBinary> binaries,
    string? gameDirectory,
    bool alreadyAdded,
    bool resumes,
    Action<Exception, string>? onCleanupFailed = null) : IAsyncDisposable
{
    private int _disposed;

    public GameSource Source { get; } = source;

    public GameValidation Validation { get; } = validation;

    public GameMetadata? Metadata => Validation.Metadata;

    public bool IsBlocked => Validation.IsBlocked;

    /// <summary>Full path of sce_sys/icon0.png; null when the game has none.</summary>
    public string? IconPath { get; } = iconPath;

    /// <summary>eboot.bin, then every file of the module folders.</summary>
    public IReadOnlyList<GameBinary> Binaries { get; } = binaries;

    public string? GameDirectory { get; } = gameDirectory;

    public bool AlreadyAdded { get; } = alreadyAdded;

    public bool Resumes { get; } = resumes;

    public bool IsDisposed => Volatile.Read(ref _disposed) == 1;

    /// <summary>Deletes the staging folder on the thread pool; does nothing the second time or without one.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1 || Source.StagingDirectory is not { } staging)
        {
            return;
        }

        await Task.Run(() => DeleteStaging(staging, onCleanupFailed)).ConfigureAwait(false);
    }

    /// <summary>Deletes a staging folder and its half-extracted sibling (&lt;staging&gt;.staging); a failure is reported, not thrown.</summary>
    internal static void DeleteStaging(string staging, Action<Exception, string>? onFailed)
    {
        foreach (var folder in (string[])[staging, staging + ".staging"])
        {
            try
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, recursive: true);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                onFailed?.Invoke(exception, folder);
            }
        }
    }
}
