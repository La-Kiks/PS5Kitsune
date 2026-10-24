using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Launch;

/// <summary>
/// Downloads what a launch needs before the game lock is taken (it can take minutes): Mesa when the game uses Lavapipe, and the fonts.
/// Mesa failing stops the launch; the fonts failing only warns. See the 7b spec, "Mesa and fonts".
/// </summary>
/// <param name="lavapipe">Null when the profile has no Mesa download.</param>
/// <param name="fonts">Null when the profile has no fonts.</param>
public sealed partial class LaunchPreparation(ILavapipe? lavapipe, IFontPack? fonts, ILogger<LaunchPreparation> logger)
{
    /// <exception cref="LavapipeUnavailableException">The settings use Lavapipe and Mesa cannot be installed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<LaunchResources> PrepareAsync(GameSettings settings, IProgress<LaunchProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string? manifest = null;
        if (settings.Lavapipe)
        {
            if (lavapipe is null)
            {
                throw new LavapipeUnavailableException("Could not download Lavapipe: this profile has no Mesa download.");
            }

            if (!lavapipe.IsInstalled())
            {
                try
                {
                    await lavapipe.InstallAsync(Relay(progress, LaunchDownload.Lavapipe), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (IsDownloadFailure(exception, cancellationToken))
                {
                    throw new LavapipeUnavailableException($"Could not download Lavapipe: {exception.Message}", exception);
                }
            }

            manifest = lavapipe.Manifest;
        }

        if (fonts is null)
        {
            return new LaunchResources(manifest, null, null);
        }

        if (!fonts.IsInstalled())
        {
            try
            {
                await fonts.InstallAsync(Relay(progress, LaunchDownload.Fonts), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsDownloadFailure(exception, cancellationToken))
            {
                var reason = exception.Message;
                LogFontsFailed(exception, reason);
                return new LaunchResources(manifest, null, $"Fonts could not be downloaded ({reason}); games that use system fonts may show no text or stop.");
            }
        }

        return new LaunchResources(manifest, fonts.Folder, null);
    }

    /// <summary>Mesa's manifest when it is installed; null otherwise. For settings that turned Lavapipe on after the preparation.</summary>
    public string? InstalledLavapipe() => lavapipe is not null && lavapipe.IsInstalled() ? lavapipe.Manifest : null;

    // An HttpClient timeout surfaces as a cancellation the caller did not ask for: it is a failure.
    private static bool IsDownloadFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException or IOException or UnauthorizedAccessException or ChecksumMismatchException or InvalidDataException
        || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);

    private static Relayed? Relay(IProgress<LaunchProgress>? progress, LaunchDownload download) => progress is null ? null : new Relayed(progress, download);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The fonts could not be downloaded: {Reason}. The game starts without them.")]
    private partial void LogFontsFailed(Exception exception, string reason);

    private sealed class Relayed(IProgress<LaunchProgress> target, LaunchDownload download) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => target.Report(new LaunchProgress(download, value));
    }
}
