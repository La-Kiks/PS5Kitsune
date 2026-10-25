namespace PS5Kitsune.Core.Launch;

/// <summary>
/// &lt;root&gt;\tools\.prepare.lock, held while a launch downloads Mesa or the fonts, so two launches (the app and the CLI, or two games)
/// never install them at once. The OS releases it when its handle closes, even after a crash. See the 7c spec, "PR #10 follow-ups".
/// </summary>
public sealed class PreparationLock(DataLayout layout)
{
    public const string FileName = ".prepare.lock";

    public string FilePath => Path.Combine(layout.Tools, FileName);

    /// <summary>How long to wait before trying again.</summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <param name="waiting">Called once, when another launch holds the lock.</param>
    /// <returns>The held lock: dispose it to release.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled while waiting.</exception>
    public async Task<IDisposable> AcquireAsync(Action? waiting, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(layout.Tools);
        var reported = false;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(FilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException exception) when (IsHeldElsewhere(exception))
            {
                if (!reported)
                {
                    reported = true;
                    waiting?.Invoke();
                }

                await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    // Windows reports a sharing or lock violation. Elsewhere .NET emulates FileShare.None with an advisory lock and throws a plain IOException.
    private static bool IsHeldElsewhere(IOException exception) =>
        exception.HResult is unchecked((int)0x80070020) or unchecked((int)0x80070021)
        || (!OperatingSystem.IsWindows() && exception is not (FileNotFoundException or DirectoryNotFoundException));
}
