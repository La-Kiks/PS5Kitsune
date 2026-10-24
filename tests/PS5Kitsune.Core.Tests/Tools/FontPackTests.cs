using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.Tests.Tools;

public sealed class FontPackTests : IDisposable
{
    private static readonly byte[] Regular = "regular"u8.ToArray();
    private static readonly byte[] Bold = "bold!"u8.ToArray();

    private static readonly FontFile RegularFont = new("NotoSans-Regular.ttf", new Uri("https://example.test/NotoSans-Regular.ttf"), TestFiles.Sha256(Regular), Regular.Length);
    private static readonly FontFile BoldFont = new("NotoSans-Bold.ttf", new Uri("https://example.test/NotoSans-Bold.ttf"), TestFiles.Sha256(Bold), Bold.Length);

    private readonly TempDirectory _temp = new();
    private readonly FakeDownloader _downloader = new();

    private FontPack CreatePack(params FontFile[] fonts) => new(
        new AnyPS5Profile { SchemaVersion = 1, Id = "test", Source = new ProfileSource("release", Tag: "v1"), Fonts = fonts },
        new DataLayout(_temp.Path),
        _downloader);

    [Fact]
    public async Task Install_downloads_every_file_into_fonts_then_writes_the_marker()
    {
        _downloader.Serve(RegularFont.Url, Regular);
        _downloader.Serve(BoldFont.Url, Bold);
        var pack = CreatePack(RegularFont, BoldFont);
        var reports = new List<DownloadProgress>();

        await pack.InstallAsync(new SyncProgress(reports.Add), TestContext.Current.CancellationToken);

        Assert.True(pack.IsInstalled());
        Assert.Equal(_temp.Combine("fonts"), pack.Folder);
        Assert.True(File.Exists(_temp.Combine("fonts", "NotoSans-Regular.ttf")));
        Assert.True(File.Exists(_temp.Combine("fonts", ".complete")));
        Assert.Equal(new DownloadProgress(12, 12), reports[^1]);
        Assert.All(reports, report => Assert.Equal(12, report.TotalBytes));
    }

    [Fact]
    public async Task A_failed_file_leaves_no_marker()
    {
        _downloader.Serve(RegularFont.Url, Regular);
        var pack = CreatePack(RegularFont, BoldFont);

        await Assert.ThrowsAsync<HttpRequestException>(() => pack.InstallAsync(cancellationToken: TestContext.Current.CancellationToken));

        Assert.False(pack.IsInstalled());
        Assert.False(File.Exists(_temp.Combine("fonts", ".complete")));
        Assert.True(File.Exists(_temp.Combine("fonts", "NotoSans-Regular.ttf")));
    }

    [Fact]
    public async Task Another_list_of_fonts_is_not_installed_yet()
    {
        _downloader.Serve(RegularFont.Url, Regular);
        await CreatePack(RegularFont).InstallAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(CreatePack(RegularFont, BoldFont).IsInstalled());
    }

    [Fact]
    public async Task An_installed_pack_downloads_nothing()
    {
        _downloader.Serve(RegularFont.Url, Regular);
        var pack = CreatePack(RegularFont);
        await pack.InstallAsync(cancellationToken: TestContext.Current.CancellationToken);

        await pack.InstallAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(_downloader.Requests);
    }

    public void Dispose() => _temp.Dispose();

    private sealed class SyncProgress(Action<DownloadProgress> report) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => report(value);
    }
}
