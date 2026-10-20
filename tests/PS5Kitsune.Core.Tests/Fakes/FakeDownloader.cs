using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Tests.Fakes;

/// <summary>Writes registered content instead of downloading, and records every request.</summary>
internal sealed class FakeDownloader : IDownloader
{
    private readonly Dictionary<Uri, byte[]> _content = new();

    public List<DownloadRequest> Requests { get; } = [];

    public void Serve(Uri url, byte[] content) => _content[url] = content;

    public Task DownloadAsync(DownloadRequest request, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        if (!_content.TryGetValue(request.Source, out var content))
        {
            throw new HttpRequestException($"404: {request.Source}");
        }

        TestFiles.Write(request.Destination, content);
        return Task.CompletedTask;
    }
}
