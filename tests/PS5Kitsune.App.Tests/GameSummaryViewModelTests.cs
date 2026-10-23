using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.App.Tests;

public sealed class GameSummaryViewModelTests
{
    private static readonly string[] AllFolders = ["sce_module", "prx"];

    private readonly Navigator _navigator = new();
    private readonly TestPlatform _platform = new();

    private GameSummaryViewModel Create(PreparedGame game, bool intel = false)
    {
        _navigator.GoTo("choose");
        var viewModel = new GameSummaryViewModel(game, intel, _platform, _navigator, new FakePages());
        _navigator.GoTo(viewModel);
        return viewModel;
    }

    [Fact]
    public void Describes_the_game()
    {
        var viewModel = Create(TestGameData.Prepared());

        Assert.Equal("DOOM", viewModel.Name);
        Assert.Equal("PPSA99666", viewModel.TitleId);
        Assert.Equal("2.3 MB", viewModel.SizeText);
        Assert.Equal("1 module folder", viewModel.ModulesText);
        Assert.Equal("2 files are SELF and will be unwrapped, eboot.bin among them.", viewModel.BinariesText);
        Assert.Empty(viewModel.BlockingIssues);
        Assert.Null(viewModel.AlreadyAddedText);
        Assert.Null(viewModel.ResumeText);
        Assert.False(viewModel.ToIntel);
        Assert.True(viewModel.AddCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(0, "No module folder")]
    [InlineData(2, "2 module folders")]
    public void Counts_module_folders(int count, string expected)
    {
        var folders = AllFolders.Take(count).ToList();

        Assert.Equal(expected, Create(TestGameData.Prepared(moduleDirectories: folders)).ModulesText);
    }

    [Fact]
    public void Names_the_binaries_state()
    {
        Assert.Equal("No SELF file.", Create(TestGameData.Prepared(binaries: [new GameBinary("eboot.bin", BinaryKind.Elf)])).BinariesText);
        Assert.Equal(
            "1 file is SELF and will be unwrapped.",
            Create(TestGameData.Prepared(binaries: [new GameBinary("eboot.bin", BinaryKind.Elf), new GameBinary("sce_module/libc.prx", BinaryKind.Self)])).BinariesText);
    }

    [Fact]
    public void Blocking_issues_disable_adding_and_warnings_do_not()
    {
        var viewModel = Create(TestGameData.Prepared(issues:
        [
            new ValidationIssue(IssueSeverity.Blocking, "sce_sys/param.json is missing."),
            new ValidationIssue(IssueSeverity.Warning, "Low disk space."),
        ]));

        Assert.Equal(["sce_sys/param.json is missing."], viewModel.BlockingIssues);
        Assert.Equal(["Low disk space."], viewModel.Warnings);
        Assert.False(viewModel.AddCommand.CanExecute(null));
    }

    [Fact]
    public void An_added_game_offers_its_folder_instead()
    {
        var viewModel = Create(TestGameData.Prepared(alreadyAdded: true));

        Assert.Equal(@"PPSA99666 is already in your games (C:\root\games\PPSA99666).", viewModel.AlreadyAddedText);
        Assert.False(viewModel.AddCommand.CanExecute(null));
        viewModel.OpenFolderCommand.Execute(null);
        Assert.Equal([@"C:\root\games\PPSA99666"], _platform.OpenedPaths);
    }

    [Fact]
    public void A_stopped_add_says_it_resumes() =>
        Assert.Equal(
            "A previous add of this game stopped; adding it resumes where it stopped. The Intel option only applies if the conversion has not run yet.",
            Create(TestGameData.Prepared(resumes: true)).ResumeText);

    [Fact]
    public void The_intel_option_follows_the_cpu_and_goes_to_the_add()
    {
        var game = TestGameData.Prepared();
        var viewModel = Create(game, intel: true);

        Assert.True(viewModel.ToIntel);
        viewModel.AddCommand.Execute(null);

        Assert.Equal("adding:PPSA99666:True", _navigator.CurrentPage);
        Assert.False(game.IsDisposed);
    }

    [Fact]
    public async Task Back_disposes_the_game_and_returns_to_choose()
    {
        var game = TestGameData.Prepared();
        var viewModel = Create(game);

        await viewModel.BackCommand.ExecuteAsync(null);

        Assert.True(game.IsDisposed);
        Assert.Equal("choose", _navigator.CurrentPage);
    }

    [Fact]
    public async Task Releasing_after_the_add_started_leaves_the_game_to_the_add_page()
    {
        var game = TestGameData.Prepared();
        var viewModel = Create(game);
        viewModel.AddCommand.Execute(null);

        await viewModel.ReleaseAsync();

        Assert.False(game.IsDisposed);
    }
}
