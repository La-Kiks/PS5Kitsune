using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class GameValidatorTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public GameValidatorTests()
    {
        TestGames.Create(Game);
    }

    private string Game => _temp.Combine("game");

    private GameValidation Validate(long available = long.MaxValue) =>
        new GameValidator(_ => available).Validate(Game, _temp.Combine("games"));

    private void WriteParam(string json) => TestFiles.Write(Path.Combine(Game, "sce_sys", "param.json"), json);

    [Fact]
    public void A_doom_like_game_is_valid()
    {
        var validation = Validate();

        Assert.False(validation.IsBlocked);
        Assert.Empty(validation.Issues);
        Assert.Equal(new GameMetadata("PPSA99666", "DOOM"), validation.Metadata);
        Assert.Equal(["sce_module"], validation.ModuleDirectories);
        Assert.Equal(new DirectoryInfo(Game).EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length), validation.SizeBytes);
    }

    [Theory]
    [InlineData("""{ "titleId": "PPSA1", "localizedParameters": { "defaultLanguage": "fr-FR", "en-US": { "titleName": "English" }, "fr-FR": { "titleName": "Francais" } } }""", "Francais")]
    [InlineData("""{ "titleId": "PPSA1", "defaultLanguage": "fr-FR", "localizedParameters": { "en-US": { "titleName": "English" }, "fr-FR": { "titleName": "Francais" } } }""", "Francais")]
    [InlineData("""{ "titleId": "PPSA1", "localizedParameters": { "ja-JP": { "titleName": "Japanese" }, "en-US": { "titleName": "English" } } }""", "English")]
    [InlineData("""{ "titleId": "PPSA1", "localizedParameters": { "defaultLanguage": "de-DE", "ja-JP": { "titleName": "Japanese" } } }""", "Japanese")]
    [InlineData("""{ "titleId": "PPSA1", "localizedParameters": { "defaultLanguage": "en-US" } }""", "PPSA1")]
    public void The_name_comes_from_the_default_language_then_en_US_then_the_first_language(string json, string expected)
    {
        WriteParam(json);

        Assert.Equal(expected, Validate().Metadata!.Name);
    }

    [Theory]
    [InlineData("{ not json", "not valid JSON")]
    [InlineData("""{ "localizedParameters": { "en-US": { "titleName": "X" } } }""", "no titleId")]
    [InlineData("""{ "titleId": "PPSA1" }""", "no localizedParameters")]
    [InlineData("""{ "titleId": "PPSA1", "localizedParameters": {} }""", "no localizedParameters")]
    [InlineData("""{ "titleId": "../evil", "localizedParameters": { "en-US": { "titleName": "X" } } }""", "letters and digits")]
    public void An_invalid_param_json_is_blocking(string json, string expected)
    {
        WriteParam(json);

        var validation = Validate();

        Assert.True(validation.IsBlocked);
        Assert.Contains(validation.Issues, issue => issue.Severity == IssueSeverity.Blocking && issue.Message.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void A_missing_param_json_is_blocking()
    {
        File.Delete(Path.Combine(Game, "sce_sys", "param.json"));

        Assert.Contains(Validate().Issues, issue => issue.Severity == IssueSeverity.Blocking && issue.Message.Contains("param.json is missing", StringComparison.Ordinal));
    }

    [Fact]
    public void Sce_module_and_sce_modules_together_are_blocking()
    {
        Directory.CreateDirectory(Path.Combine(Game, "sce_modules"));

        Assert.Contains(Validate().Issues, issue => issue.Severity == IssueSeverity.Blocking && issue.Message.Contains("sce_module and sce_modules", StringComparison.Ordinal));
    }

    [Fact]
    public void Prx_beside_sce_module_is_accepted()
    {
        TestFiles.Write(Path.Combine(Game, "prx", "extra.prx"), TestFiles.Elf());

        var validation = Validate();

        Assert.False(validation.IsBlocked);
        Assert.Equal(["sce_module", "prx"], validation.ModuleDirectories);
    }

    [Fact]
    public void No_module_folder_is_a_warning()
    {
        Directory.Delete(Path.Combine(Game, "sce_module"), recursive: true);

        var validation = Validate();

        Assert.False(validation.IsBlocked);
        Assert.Equal(IssueSeverity.Warning, Assert.Single(validation.Warnings).Severity);
        Assert.Empty(validation.ModuleDirectories);
    }

    [Fact]
    public void Not_enough_free_space_is_blocking()
    {
        Assert.Contains(Validate(available: 10).Issues, issue => issue.Severity == IssueSeverity.Blocking && issue.Message.Contains("free space", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_blocking_issue_is_reported_at_once()
    {
        File.Delete(Path.Combine(Game, "eboot.bin"));
        File.Delete(Path.Combine(Game, "sce_sys", "param.json"));

        var validation = Validate(available: 10);

        Assert.Equal(3, validation.Issues.Count(issue => issue.Severity == IssueSeverity.Blocking));
        Assert.Null(validation.Metadata);
    }

    public void Dispose() => _temp.Dispose();
}
