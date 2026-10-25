using PS5Kitsune.App.Controls;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.App.Tests;

public sealed class AddResultViewModelTests
{
    private readonly Navigator _navigator = new();
    private readonly TestPlatform _platform = new();

    private AddResultViewModel Create(GameAdded added) => new(added, _platform, _navigator, new FakePages());

    private static AuditSummary Doom() => new(360, 4, 0, new Dictionary<string, int>())
    {
        Unresolved =
        [
            new UnresolvedImport("__tls_get_addr_nid_postfix", "libc.prx", AuditClass.Stub),
            new UnresolvedImport("fscanf_nid_postfix", "libSceLibcInternal.prx", AuditClass.Stub),
            new UnresolvedImport("sceSystemServiceLoadExec", "libSceSystemService.prx", AuditClass.Stub),
            new UnresolvedImport("__tls_get_addr_nid_postfix", "libkernel.prx", AuditClass.Stub),
        ],
    };

    [Fact]
    public void An_orange_audit_lists_its_stubs()
    {
        var viewModel = Create(TestGameData.Added(Doom()));

        Assert.Equal(AuditVerdict.Orange, viewModel.Verdict);
        Assert.Equal("Orange: the game loads but will stop if it calls an unimplemented function.", viewModel.VerdictText);
        Assert.Equal("360 implemented, 4 stubs, 0 absent", viewModel.CountsText);
        Assert.Null(viewModel.MissingLibrariesText);
        Assert.True(viewModel.HasUnresolved);
        Assert.Equal("Show the unresolved functions (4)", viewModel.UnresolvedHeader);
        Assert.Equal("sceSystemServiceLoadExec — libSceSystemService.prx (stub)", viewModel.UnresolvedLines[2]);
    }

    [Fact]
    public void The_verdict_has_a_chip_kind_and_label()
    {
        var orange = Create(TestGameData.Added(Doom()));
        var green = Create(TestGameData.Added(new AuditSummary(262, 0, 0, new Dictionary<string, int>())));

        Assert.Equal(StatusKind.Green, green.VerdictKind);
        Assert.Equal("Green", green.VerdictLabel);
        Assert.Equal(StatusKind.Orange, orange.VerdictKind);
        Assert.Equal("Orange", orange.VerdictLabel);
    }

    [Fact]
    public void A_green_audit_hides_the_list()
    {
        var viewModel = Create(TestGameData.Added(new AuditSummary(262, 0, 0, new Dictionary<string, int>())));

        Assert.Equal("Green: the game can load.", viewModel.VerdictText);
        Assert.False(viewModel.HasUnresolved);
    }

    [Fact]
    public void A_red_audit_names_missing_libraries_and_absent_functions()
    {
        var audit = new AuditSummary(250, 0, 1, new Dictionary<string, int> { ["libSceNpTrophy.prx"] = 3 })
        {
            Unresolved = [new UnresolvedImport("AbCdEfGhIjk", "", AuditClass.Absent)],
        };

        var viewModel = Create(TestGameData.Added(audit));

        Assert.Equal("Red: not playable yet; wait for an AnyPS5 update.", viewModel.VerdictText);
        Assert.Equal("Missing libraries: libSceNpTrophy.prx", viewModel.MissingLibrariesText);
        Assert.Equal(["AbCdEfGhIjk (absent)"], viewModel.UnresolvedLines);
    }

    [Fact]
    public void Shows_the_validation_warnings() =>
        Assert.Equal(
            ["Low disk space."],
            Create(TestGameData.Added(warnings: [new ValidationIssue(IssueSeverity.Warning, "Low disk space.")])).Warnings);

    [Fact]
    public void A_reconverted_game_shows_the_counts_before()
    {
        var viewModel = new AddResultViewModel(TestGameData.Reconverted(), new TestPlatform(), new Navigator(), new FakePages());

        Assert.Equal("DOOM reconverted", viewModel.Title);
        Assert.Equal("361 implemented, 3 stubs, 0 absent", viewModel.CountsText);
        Assert.Equal("Before: 360 implemented, 4 stubs, 0 absent", viewModel.BeforeText);
    }

    [Fact]
    public void An_added_game_has_no_before_line()
    {
        Assert.Null(new AddResultViewModel(TestGameData.Added(), new TestPlatform(), new Navigator(), new FakePages()).BeforeText);
    }

    [Fact]
    public void The_buttons_open_the_folder_add_another_or_go_to_the_library()
    {
        var viewModel = Create(TestGameData.Added());

        viewModel.OpenFolderCommand.Execute(null);
        Assert.Equal([@"C:\root\games\PPSA99666"], _platform.OpenedPaths);

        viewModel.AddAnotherCommand.Execute(null);
        Assert.Equal("choose", _navigator.CurrentPage);
        _navigator.GoBack();
        Assert.Equal("library", _navigator.CurrentPage);

        viewModel.BackToLibraryCommand.Execute(null);
        Assert.Equal("library", _navigator.CurrentPage);
    }
}
