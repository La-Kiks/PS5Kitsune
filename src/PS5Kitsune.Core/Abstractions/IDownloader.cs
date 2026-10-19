namespace PS5Kitsune.Core.Abstractions;

/// <param name="Source">URL to download.</param>
/// <param name="Destination">Final file path. A ".part" file beside it holds an interrupted download.</param>
/// <param name="ExpectedSha256">Lowercase hex SHA-256 the file must match.</param>
public sealed record DownloadRequest(Uri Source, string Destination, string ExpectedSha256);

/// <param name="TotalBytes">Null when the server does not send a length.</param>
public readonly record struct DownloadProgress(long ReceivedBytes, long? TotalBytes);

/// <summary>Downloads a file, resuming an interrupted download, and verifies its SHA-256.</summary>
public interface IDownloader
{
    /// <summary>
    /// Downloads <see cref="DownloadRequest.Source"/> to <see cref="DownloadRequest.Destination"/>.
    /// Returns immediately when the destination already exists with the expected hash.
    /// </summary>
    /// <exception cref="ChecksumMismatchException">The downloaded file does not match; nothing is kept.</exception>
    Task DownloadAsync(DownloadRequest request, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default);
}

public sealed class ChecksumMismatchException(Uri source, string expected, string actual)
    : Exception($"SHA-256 mismatch for {source}: expected {expected}, got {actual}.")
{
    public string Expected { get; } = expected;

    public string Actual { get; } = actual;
}
