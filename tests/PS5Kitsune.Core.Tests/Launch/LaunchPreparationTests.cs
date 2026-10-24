using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Launch;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.Launch;

public sealed class LaunchPreparationTests
{
    private readonly FakeLavapipe _lavapipe = new();
    private readonly FakeFontPack _fonts = new();

    private LaunchPreparation Create(bool withLavapipe = true, bool withFonts = true) =>
        new(withLavapipe ? _lavapipe : null, withFonts ? _fonts : null, NullLogger<LaunchPreparation>.Instance);

    private static readonly GameSettings WithLavapipe = new() { Lavapipe = true };

    [Fact]
    public async Task Lavapipe_and_fonts_missing_are_installed_lavapipe_first_with_progress()
    {
        _lavapipe.Reports = [1L << 20];
        _fonts.Reports = [2L << 20];
        var reports = new List<LaunchProgress>();

        var resources = await Create().PrepareAsync(WithLavapipe, new SyncProgress(reports.Add), TestContext.Current.CancellationToken);

        Assert.Equal(new LaunchResources(_lavapipe.Manifest, _fonts.Folder, null), resources);
        Assert.Equal(
            [new LaunchProgress(LaunchDownload.Lavapipe, new DownloadProgress(1L << 20, FakeLavapipe.Total)), new LaunchProgress(LaunchDownload.Fonts, new DownloadProgress(2L << 20, FakeFontPack.Total))],
            reports);
    }

    [Fact]
    public async Task Nothing_to_download_installs_nothing()
    {
        _lavapipe.Installed = true;
        _fonts.Installed = true;

        var resources = await Create().PrepareAsync(WithLavapipe, null, TestContext.Current.CancellationToken);

        Assert.Equal(new LaunchResources(_lavapipe.Manifest, _fonts.Folder, null), resources);
        Assert.Equal(0, _lavapipe.Installs);
        Assert.Equal(0, _fonts.Installs);
    }

    [Fact]
    public async Task Without_lavapipe_in_the_settings_mesa_is_left_alone()
    {
        _fonts.Installed = true;

        var resources = await Create().PrepareAsync(new GameSettings(), null, TestContext.Current.CancellationToken);

        Assert.Null(resources.LavapipeManifest);
        Assert.Equal(0, _lavapipe.Installs);
    }

    [Fact]
    public async Task A_failed_mesa_download_stops_the_launch()
    {
        _lavapipe.Failure = new HttpRequestException("404");

        var exception = await Assert.ThrowsAsync<LavapipeUnavailableException>(() => Create().PrepareAsync(WithLavapipe, null, TestContext.Current.CancellationToken));

        Assert.Equal("Could not download Lavapipe: 404", exception.Message);
        Assert.Equal(0, _fonts.Installs);
    }

    [Fact]
    public async Task A_profile_without_mesa_cannot_use_lavapipe()
    {
        var exception = await Assert.ThrowsAsync<LavapipeUnavailableException>(
            () => Create(withLavapipe: false).PrepareAsync(WithLavapipe, null, TestContext.Current.CancellationToken));

        Assert.Equal("Could not download Lavapipe: this profile has no Mesa download.", exception.Message);
    }

    [Fact]
    public async Task A_failed_font_download_only_warns()
    {
        _fonts.Failure = new IOException("disk full");

        var resources = await Create().PrepareAsync(new GameSettings(), null, TestContext.Current.CancellationToken);

        Assert.Null(resources.FontsDirectory);
        Assert.Equal("Fonts could not be downloaded (disk full); games that use system fonts may show no text or stop.", resources.FontsWarning);
    }

    [Fact]
    public async Task A_timeout_is_a_failure_not_a_cancellation()
    {
        _fonts.Failure = new TaskCanceledException("timed out");

        var resources = await Create().PrepareAsync(new GameSettings(), null, TestContext.Current.CancellationToken);

        Assert.NotNull(resources.FontsWarning);
    }

    [Fact]
    public async Task Cancelling_stops_the_download()
    {
        _lavapipe.Gate = new TaskCompletionSource();
        using var cancellation = new CancellationTokenSource();
        var preparing = Create().PrepareAsync(WithLavapipe, null, cancellation.Token);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preparing);
        Assert.False(_lavapipe.Installed);
    }

    [Fact]
    public async Task No_fonts_in_the_profile_means_no_folder_and_no_warning()
    {
        var resources = await Create(withFonts: false).PrepareAsync(new GameSettings(), null, TestContext.Current.CancellationToken);

        Assert.Equal(new LaunchResources(null, null, null), resources);
    }

    [Fact]
    public void The_installed_manifest_is_known_only_once_installed()
    {
        Assert.Null(Create().InstalledLavapipe());
        _lavapipe.Installed = true;
        Assert.Equal(_lavapipe.Manifest, Create().InstalledLavapipe());
        Assert.Null(Create(withLavapipe: false).InstalledLavapipe());
    }

    private sealed class SyncProgress(Action<LaunchProgress> report) : IProgress<LaunchProgress>
    {
        public void Report(LaunchProgress value) => report(value);
    }
}
