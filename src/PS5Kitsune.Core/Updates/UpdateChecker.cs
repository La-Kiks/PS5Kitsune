using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PS5Kitsune.Core.Updates;

/// <param name="Version">The newer release's version.</param>
/// <param name="ReleasePage">Its page on GitHub, built from the version.</param>
public sealed record UpdateInfo(AppVersion Version, Uri ReleasePage);

/// <summary>
/// Asks GitHub for PS5Kitsune's latest release. Only the app calls it, once per start; see the update notice spec.
/// A failure is logged and gives null: it never throws, except when the caller cancels.
/// </summary>
public sealed partial class UpdateChecker
{
    /// <summary>The app only ever looks for its own releases.</summary>
    public const string Repository = "La-Kiks/PS5Kitsune";

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _httpClient;
    private readonly ILogger<UpdateChecker> _logger;
    private readonly TimeSpan _timeout;

    public UpdateChecker(HttpClient httpClient, ILogger<UpdateChecker> logger)
        : this(httpClient, logger, DefaultTimeout)
    {
    }

    internal UpdateChecker(HttpClient httpClient, ILogger<UpdateChecker> logger, TimeSpan timeout)
    {
        _httpClient = httpClient;
        _logger = logger;
        _timeout = timeout;
    }

    /// <summary>The latest published release; GitHub leaves out drafts and pre-releases.</summary>
    public static Uri LatestReleaseUrl { get; } = new("https://api.github.com/repos/" + Repository + "/releases/latest");

    /// <returns>The latest release when it is newer than <paramref name="current"/>; null otherwise, and for a pre-release build without asking.</returns>
    public async Task<UpdateInfo?> CheckAsync(AppVersion current, CancellationToken cancellationToken = default)
    {
        if (current.IsPreRelease)
        {
            return null;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        byte[] body;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await _httpClient.SendAsync(request, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                LogErrorAnswer((int)response.StatusCode);
                return null;
            }

            // Bytes, not text: a proxy's unknown charset= would make ReadAsStringAsync throw.
            body = await response.Content.ReadAsByteArrayAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogTimedOut(_timeout.TotalSeconds);
            return null;
        }
        catch (HttpRequestException exception)
        {
            LogUnreachable(exception.Message);
            return null;
        }

        var tag = ReadTag(body);
        if (tag is null)
        {
            LogNoTag();
            return null;
        }

        if (!tag.StartsWith('v') || AppVersion.Parse(tag) is not { IsPreRelease: false } latest)
        {
            LogNotAVersion(tag);
            return null;
        }

        return latest > current ? new UpdateInfo(latest, new Uri("https://github.com/" + Repository + "/releases/tag/v" + latest)) : null;
    }

    /// <returns>The <c>tag_name</c> string; null when the body is not JSON, not an object, or has no string <c>tag_name</c>.</returns>
    private static string? ReadTag(byte[] body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("tag_name", out var tag) && tag.ValueKind == JsonValueKind.String
                ? tag.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Update check: GitHub is unreachable ({Reason}).")]
    private partial void LogUnreachable(string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Update check: GitHub did not answer within {Seconds} seconds.")]
    private partial void LogTimedOut(double seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Update check: GitHub answered {Status}.")]
    private partial void LogErrorAnswer(int status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Update check: the answer has no release tag.")]
    private partial void LogNoTag();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Update check: the latest release's tag {Tag} is not a vX.Y.Z version.")]
    private partial void LogNotAVersion(string tag);
}
