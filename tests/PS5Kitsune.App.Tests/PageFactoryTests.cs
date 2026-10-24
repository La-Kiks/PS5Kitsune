using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Sessions;
using PS5Kitsune.App.Shell;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Settings;
using PS5Kitsune.Core.Tests;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.App.Tests;

public sealed class PageFactoryTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void Builds_every_page_from_a_real_session()
    {
        var platform = new TestPlatform();
        var profile = ProfileLoader.LoadEmbedded();
        using var sessions = new SessionHost(root => RootSession.Create(root, platform, profile), _temp.Combine("root"));
        var pages = new PageFactory(sessions, new Navigator(), new FakeDialogs(), new AppSettingsStore(_temp.Combine("settings")), new InlineDispatcher(), platform);

        Assert.IsType<HomeViewModel>(pages.Home());
        Assert.IsType<ModeViewModel>(pages.Mode());
        Assert.IsType<SystemCheckViewModel>(pages.SystemCheck(AnyPS5Mode.Build));
        var tools = Assert.IsType<ToolsViewModel>(pages.Tools());
        tools.OnNavigatedTo();
        Assert.Equal(3, tools.Tools.Count);
        Assert.Equal(6, Assert.IsType<InstallViewModel>(pages.Install(AnyPS5Mode.Release)).Steps.Count);
        Assert.Equal(10, Assert.IsType<InstallViewModel>(pages.Install(AnyPS5Mode.Build)).Steps.Count);
        Assert.IsType<DoneViewModel>(pages.Done());
        Assert.IsType<ChooseGameViewModel>(pages.ChooseGame());
        Assert.IsType<GameSummaryViewModel>(pages.GameSummary(TestGameData.Prepared()));
        Assert.IsType<AddGameViewModel>(pages.AddGame(TestGameData.Prepared(), false));
        Assert.IsType<AddResultViewModel>(pages.AddResult(TestGameData.Added()));
        Assert.Equal(6, Assert.IsType<ReconvertViewModel>(pages.Reconvert("PPSA99666", "DOOM")).Steps.Count);
        Assert.IsType<AddResultViewModel>(pages.ReconvertResult(TestGameData.Reconverted()));
        Assert.IsType<LibraryViewModel>(pages.Library());
        Assert.IsType<HomeViewModel>(pages.Start());
        var game = new GameLayout(_temp.Combine("root", "games", "PPSA99666"), "PPSA99666", platform);
        var process = new FakeLaunchedGame(4812, _temp.Combine("game.log"), DateTimeOffset.UnixEpoch);
        Assert.IsType<GameViewModel>(pages.Game(new PlayingGame(game, TestGameData.Added().Record, process), firstLaunch: false));
    }

    public void Dispose() => _temp.Dispose();
}
