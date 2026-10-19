using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Downloads;

namespace PS5Kitsune.Core.Tests.Downloads;

public sealed class HttpDownloaderTests : IDisposable
{
    private static readonly Uri Source = new("https://example.test/tool.zip");
    private static readonly byte[] Content = Enumerable.Range(0, 200_000).Select(i => (byte)(i % 251)).ToArray();
    private static readonly string ContentSha256 = Convert.ToHexStringLower(SHA256.HashData(Content));

    private readonly TempDirectory _temp = new();
    private readonly FakeHandler _handler = new(Content);

    private string Destination => _temp.Combine("downloads", "tool.zip");

    private HttpDownloader CreateDownloader() => new(new HttpClient(_handler), NullLogger<HttpDownloader>.Instance);

    [Fact]
    public async Task Downloads_and_verifies_the_file()
    {
        await CreateDownloader().DownloadAsync(new DownloadRequest(Source, Destination, ContentSha256), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(Content, await File.ReadAllBytesAsync(Destination, TestContext.Current.CancellationToken));
        Assert.False(File.Exists(Destination + ".part"));
    }

    [Fact]
    public async Task Rejects_a_file_whose_hash_does_not_match_and_keeps_nothing()
    {
        var wrongHash = new string('0', 64);

        var exception = await Assert.ThrowsAsync<ChecksumMismatchException>(() =>
            CreateDownloader().DownloadAsync(new DownloadRequest(Source, Destination, wrongHash), cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(ContentSha256, exception.Actual);
        Assert.False(File.Exists(Destination));
        Assert.False(File.Exists(Destination + ".part"));
    }

    [Fact]
    public async Task Resumes_an_interrupted_download_with_a_range_request()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Destination)!);
        await File.WriteAllBytesAsync(Destination + ".part", Content[..50_000], TestContext.Current.CancellationToken);

        await CreateDownloader().DownloadAsync(new DownloadRequest(Source, Destination, ContentSha256), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(50_000, _handler.LastRangeStart);
        Assert.Equal(Content, await File.ReadAllBytesAsync(Destination, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Restarts_from_zero_when_the_server_ignores_the_range()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Destination)!);
        await File.WriteAllBytesAsync(Destination + ".part", Content[..50_000], TestContext.Current.CancellationToken);
        _handler.SupportsRanges = false;

        await CreateDownloader().DownloadAsync(new DownloadRequest(Source, Destination, ContentSha256), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(Content, await File.ReadAllBytesAsync(Destination, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Skips_the_download_when_the_destination_already_matches()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Destination)!);
        await File.WriteAllBytesAsync(Destination, Content, TestContext.Current.CancellationToken);

        await CreateDownloader().DownloadAsync(new DownloadRequest(Source, Destination, ContentSha256), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, _handler.RequestCount);
    }

    [Fact]
    public async Task Reports_progress_up_to_the_total_size()
    {
        var reports = new List<DownloadProgress>();

        await CreateDownloader().DownloadAsync(
            new DownloadRequest(Source, Destination, ContentSha256),
            new SynchronousProgress(reports.Add),
            TestContext.Current.CancellationToken);

        Assert.Equal(new DownloadProgress(Content.Length, Content.Length), reports[^1]);
    }

    public void Dispose()
    {
        _handler.Dispose();
        _temp.Dispose();
    }

    private sealed class SynchronousProgress(Action<DownloadProgress> report) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => report(value);
    }

    private sealed class FakeHandler(byte[] content) : HttpMessageHandler
    {
        public bool SupportsRanges { get; set; } = true;

        public int RequestCount { get; private set; }

        public long? LastRangeStart { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRangeStart = request.Headers.Range?.Ranges.Single().From;
            if (SupportsRanges && LastRangeStart is { } start)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PartialContent)
                {
                    Content = new ByteArrayContent(content[(int)start..]),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });
        }
    }
}
