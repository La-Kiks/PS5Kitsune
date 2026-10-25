using Avalonia.Styling;
using PS5Kitsune.App.Shell;
using PS5Kitsune.Core.Settings;
using PS5Kitsune.Core.Tests;

namespace PS5Kitsune.App.Tests;

public sealed class ThemeServiceTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly List<AppTheme> _applied = [];

    [Fact]
    public async Task Changing_applies_then_saves_and_keeps_the_root()
    {
        var store = new AppSettingsStore(_temp.Combine("settings"));
        await store.SaveAsync(new AppSettings(AppSettings.CurrentSchemaVersion, _temp.Combine("root")), TestContext.Current.CancellationToken);
        var service = new ThemeService(store, _applied.Add);

        await service.ChangeAsync(AppTheme.Light);

        Assert.Equal([AppTheme.Light], _applied);
        Assert.Equal(AppTheme.Light, service.Current);
        Assert.Equal(new AppSettings(AppSettings.CurrentSchemaVersion, _temp.Combine("root"), AppTheme.Light), store.Load().Settings);
    }

    [Fact]
    public async Task A_failed_save_still_applies_the_theme()
    {
        // A file where the settings folder should be: the save cannot create it.
        await File.WriteAllTextAsync(_temp.Combine("settings"), "x", TestContext.Current.CancellationToken);
        var logger = new ListLogger();
        var service = new ThemeService(new AppSettingsStore(_temp.Combine("settings")), _applied.Add, () => logger);

        await service.ChangeAsync(AppTheme.Dark);

        Assert.Equal([AppTheme.Dark], _applied);
        Assert.Equal(AppTheme.Dark, service.Current);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Warning, entry.Level);
        Assert.Equal("Could not save the theme.", entry.Message);
    }

    [Fact]
    public async Task Changes_started_back_to_back_are_saved_one_at_a_time_and_the_last_one_wins()
    {
        var store = new AppSettingsStore(_temp.Combine("settings"));
        await store.SaveAsync(new AppSettings(AppSettings.CurrentSchemaVersion, _temp.Combine("root")), TestContext.Current.CancellationToken);
        var logger = new ListLogger();
        var service = new ThemeService(store, _applied.Add, () => logger);

        var first = service.ChangeAsync(AppTheme.Light);
        var second = service.ChangeAsync(AppTheme.Dark);
        await Task.WhenAll(first, second);

        Assert.Empty(logger.Entries);
        Assert.Equal(AppTheme.Dark, store.Load().Settings.Theme);
    }

    [Theory]
    [InlineData(AppTheme.System, "Default")]
    [InlineData(AppTheme.Light, "Light")]
    [InlineData(AppTheme.Dark, "Dark")]
    public void Each_theme_maps_to_its_variant(AppTheme theme, string variant) =>
        Assert.Equal(variant, ThemeService.VariantOf(theme).Key.ToString());

    public void Dispose() => _temp.Dispose();
}
