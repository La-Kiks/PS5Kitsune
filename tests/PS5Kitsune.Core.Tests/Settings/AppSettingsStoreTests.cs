using PS5Kitsune.Core.Settings;

namespace PS5Kitsune.Core.Tests.Settings;

public sealed class AppSettingsStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    private AppSettingsStore Store => new(_temp.Combine("settings"));

    [Fact]
    public void A_missing_file_gives_the_defaults()
    {
        var load = Store.Load();

        Assert.Equal(AppSettings.Default, load.Settings);
        Assert.Null(load.Settings.DataRoot);
        Assert.Null(load.Problem);
    }

    [Fact]
    public async Task Saved_settings_load_back()
    {
        // A fully qualified path on every OS: Load() ignores a relative root, and D:\ is relative on Linux.
        var settings = new AppSettings(AppSettings.CurrentSchemaVersion, _temp.Combine("data"));

        await Store.SaveAsync(settings, TestContext.Current.CancellationToken);

        Assert.Equal(new AppSettingsLoad(settings, null), Store.Load());
        Assert.True(File.Exists(_temp.Combine("settings", "settings.json")));
    }

    [Fact]
    public async Task A_corrupt_file_gives_the_defaults_and_a_problem()
    {
        Directory.CreateDirectory(_temp.Combine("settings"));
        await File.WriteAllTextAsync(_temp.Combine("settings", "settings.json"), "{ nope", TestContext.Current.CancellationToken);

        var load = Store.Load();

        Assert.Equal(AppSettings.Default, load.Settings);
        Assert.Contains("settings.json", load.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_schema_version_gives_the_defaults_and_a_problem()
    {
        Directory.CreateDirectory(_temp.Combine("settings"));
        await File.WriteAllTextAsync(_temp.Combine("settings", "settings.json"), """{ "schemaVersion": 7, "dataRoot": "D:\\X" }""", TestContext.Current.CancellationToken);

        var load = Store.Load();

        Assert.Equal(AppSettings.Default, load.Settings);
        Assert.Contains("schema version 7", load.Problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("PS5")]
    public async Task An_empty_or_relative_data_root_gives_the_defaults_and_a_problem(string dataRoot)
    {
        Directory.CreateDirectory(_temp.Combine("settings"));
        var json = $$"""{ "schemaVersion": {{AppSettings.CurrentSchemaVersion}}, "dataRoot": "{{dataRoot}}" }""";
        await File.WriteAllTextAsync(_temp.Combine("settings", "settings.json"), json, TestContext.Current.CancellationToken);

        var load = Store.Load();

        Assert.Equal(AppSettings.Default, load.Settings);
        Assert.Contains("invalid dataRoot", load.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_file_without_a_theme_reads_system()
    {
        Directory.CreateDirectory(_temp.Combine("settings"));
        await File.WriteAllTextAsync(_temp.Combine("settings", "settings.json"), """{ "schemaVersion": 1, "dataRoot": null }""", TestContext.Current.CancellationToken);

        var load = Store.Load();

        Assert.Null(load.Problem);
        Assert.Equal(AppTheme.System, load.Settings.Theme);
    }

    [Fact]
    public async Task The_theme_round_trips_as_a_camel_case_string()
    {
        await Store.SaveAsync(new AppSettings(AppSettings.CurrentSchemaVersion, null, AppTheme.Dark), TestContext.Current.CancellationToken);

        Assert.Contains("\"theme\": \"dark\"", await File.ReadAllTextAsync(_temp.Combine("settings", "settings.json"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal(AppTheme.Dark, Store.Load().Settings.Theme);
    }

    [Fact]
    public async Task An_unknown_theme_gives_the_defaults_and_a_problem()
    {
        Directory.CreateDirectory(_temp.Combine("settings"));
        await File.WriteAllTextAsync(_temp.Combine("settings", "settings.json"), """{ "schemaVersion": 1, "theme": "purple" }""", TestContext.Current.CancellationToken);

        var load = Store.Load();

        Assert.Equal(AppSettings.Default, load.Settings);
        Assert.NotNull(load.Problem);
    }

    public void Dispose() => _temp.Dispose();
}
