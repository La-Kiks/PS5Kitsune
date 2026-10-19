using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Downloads;

/// <summary>
/// Downloads into "&lt;destination&gt;.part", resumes it with a Range request when it exists,
/// and only moves it to the destination once its SHA-256 matches.
/// </summary>
public sealed partial class HttpDownloader(HttpClient httpClient, ILogger<HttpDownloader> logger) : IDownloader
{
    private const int BufferSize = 81920;

    public async Task DownloadAsync(DownloadRequest request, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var expected = request.ExpectedSha256.ToLowerInvariant();

        if (File.Exists(request.Destination) && await ComputeSha256Async(request.Destination, cancellationToken).ConfigureAwait(false) == expected)
        {
            LogAlreadyPresent(request.Destination);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(request.Destination))!);
        var partPath = request.Destination + ".part";
        var existingBytes = File.Exists(partPath) ? new FileInfo(partPath).Length : 0;

        using var message = new HttpRequestMessage(HttpMethod.Get, request.Source);
        if (existingBytes > 0)
        {
            message.Headers.Range = new RangeHeaderValue(existingBytes, null);
        }

        using var response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (existingBytes > 0 && response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            // The part file is already complete (or longer than the resource): verify it as is.
            LogResumeRejected(request.Source);
        }
        else
        {
            response.EnsureSuccessStatusCode();
            var resuming = existingBytes > 0 && response.StatusCode == HttpStatusCode.PartialContent;
            if (!resuming)
            {
                existingBytes = 0;
            }
            else
            {
                LogResuming(request.Source, existingBytes);
            }

            var total = response.Content.Headers.ContentLength is { } length ? existingBytes + length : (long?)null;
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var target = new FileStream(partPath, resuming ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
            await CopyWithProgressAsync(source, target, existingBytes, total, progress, cancellationToken).ConfigureAwait(false);
        }

        var actual = await ComputeSha256Async(partPath, cancellationToken).ConfigureAwait(false);
        if (actual != expected)
        {
            File.Delete(partPath);
            throw new ChecksumMismatchException(request.Source, expected, actual);
        }

        File.Move(partPath, request.Destination, overwrite: true);
        LogDownloaded(request.Source, request.Destination);
    }

    internal static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }

    private static async Task CopyWithProgressAsync(Stream source, Stream target, long received, long? total, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        var buffer = new byte[BufferSize];
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            received += read;
            progress?.Report(new DownloadProgress(received, total));
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Destination} already present with the expected hash")]
    private partial void LogAlreadyPresent(string destination);

    [LoggerMessage(Level = LogLevel.Information, Message = "Resuming {Source} from byte {Offset}")]
    private partial void LogResuming(Uri source, long offset);

    [LoggerMessage(Level = LogLevel.Information, Message = "Server rejected the resume range for {Source}; verifying the partial file")]
    private partial void LogResumeRejected(Uri source);

    [LoggerMessage(Level = LogLevel.Information, Message = "Downloaded {Source} to {Destination}")]
    private partial void LogDownloaded(Uri source, string destination);
}
