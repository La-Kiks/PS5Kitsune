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

    private GameValidation ValidatePs4(Ps4Support? ps4 = null) =>
        new GameValidator(_ => long.MaxValue).Validate(Game, _temp.Combine("games"), ps4);

    private void MakePs4()
    {
        File.Delete(Path.Combine(Game, "sce_sys", "param.json"));
        File.WriteAllBytes(Path.Combine(Game, "sce_sys", "param.sfo"), TestSfo.Bloodborne);
    }

    [Fact]
    public void A_ps4_game_without_the_driver_is_blocked_with_the_version()
    {
        MakePs4();

        var validation = ValidatePs4(new Ps4Support(DriverPresent: false, "AnyPS5 v0.1.4"));

        Assert.True(validation.IsBlocked);
        Assert.Equal(new GameMetadata("CUSA03173", "Bloodborne®", IsPs4: true), validation.Metadata);
        var issue = Assert.Single(validation.Issues);
        Assert.Equal(new ValidationIssue(IssueSeverity.Blocking, "PS4 game: AnyPS5 v0.1.4 cannot draw PS4 graphics yet (no libSceGnmDriver). PS4 support is in progress upstream."), issue);
    }

    [Fact]
    public void Without_an_active_install_the_message_names_the_installed_anyps5()
    {
        MakePs4();

        Assert.Equal(
            "PS4 game: the installed AnyPS5 cannot draw PS4 graphics yet (no libSceGnmDriver). PS4 support is in progress upstream.",
            Assert.Single(ValidatePs4().Issues).Message);
    }

    [Fact]
    public void A_ps4_game_with_the_driver_gets_a_warning_only()
    {
        MakePs4();

        var validation = ValidatePs4(new Ps4Support(DriverPresent: true, "AnyPS5 v0.2.0"));

        Assert.False(validation.IsBlocked);
        Assert.Equal(
            new ValidationIssue(IssueSeverity.Warning, "PS4 game: PS4 support in AnyPS5 is experimental. Expect missing imports in the audit."),
            Assert.Single(validation.Issues));
    }

    [Fact]
    public void A_cusa_title_id_in_param_json_counts_as_ps4()
    {
        WriteParam("""{ "titleId": "CUSA01664", "localizedParameters": { "en-US": { "titleName": "LIMBO" } } }""");

        var validation = ValidatePs4(new Ps4Support(DriverPresent: false, "AnyPS5 v0.1.4"));

        Assert.True(validation.Metadata!.IsPs4);
        Assert.Contains(validation.Issues, issue => issue.Message.StartsWith("PS4 game: AnyPS5 v0.1.4 cannot draw", StringComparison.Ordinal));
    }

    [Fact]
    public void A_damaged_param_sfo_is_blocking_with_its_reason()
    {
        File.Delete(Path.Combine(Game, "sce_sys", "param.json"));
        File.WriteAllBytes(Path.Combine(Game, "sce_sys", "param.sfo"), [1, 2, 3]);

        var validation = ValidatePs4();

        Assert.Null(validation.Metadata);
        Assert.Equal(new ValidationIssue(IssueSeverity.Blocking, "sce_sys/param.sfo is not a valid PSF file."), Assert.Single(validation.Issues));
    }

    [Fact]
    public void Neither_metadata_file_names_both()
    {
        File.Delete(Path.Combine(Game, "sce_sys", "param.json"));

        Assert.Contains(
            new ValidationIssue(IssueSeverity.Blocking, "sce_sys/param.json is missing (PS5) and so is sce_sys/param.sfo (PS4)."),
            Validate().Issues);
    }

    [Fact]
    public void A_ps5_game_gets_no_ps4_issue_whatever_the_driver()
    {
        Assert.Empty(ValidatePs4(new Ps4Support(DriverPresent: false, "AnyPS5 v0.1.4")).Issues);
        Assert.Empty(ValidatePs4(new Ps4Support(DriverPresent: true, "AnyPS5 v0.2.0")).Issues);
    }

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
    public void Unity_module_folders_are_found()
    {
        TestFiles.Write(Path.Combine(Game, "Media", "Modules", "Il2cppUserAssemblies.prx"), TestFiles.Elf());
        TestFiles.Write(Path.Combine(Game, "Media", "Plugins", "PSN.prx"), TestFiles.Elf());
        Directory.CreateDirectory(Path.Combine(Game, "Media", "StreamingAssets"));

        var validation = Validate();

        Assert.False(validation.IsBlocked);
        Assert.Empty(validation.Issues);
        Assert.Equal(["sce_module", "Media/Modules", "Media/Plugins"], validation.ModuleDirectories);
    }

    [Fact]
    public void A_unity_module_folder_alone_is_not_a_missing_module_folder()
    {
        Directory.Delete(Path.Combine(Game, "sce_module"), recursive: true);
        TestFiles.Write(Path.Combine(Game, "Media", "Modules", "Il2cppUserAssemblies.prx"), TestFiles.Elf());

        var validation = Validate();

        Assert.Empty(validation.Issues);
        Assert.Equal(["Media/Modules"], validation.ModuleDirectories);
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
