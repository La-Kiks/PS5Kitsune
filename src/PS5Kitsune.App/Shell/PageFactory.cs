using Microsoft.Extensions.Logging;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Sessions;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Settings;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.App.Shell;

/// <summary>Builds each page from the services of the current data root.</summary>
public sealed class PageFactory(SessionHost sessions, Navigator navigator, IDialogService dialogs, AppSettingsStore settings, IUiDispatcher ui, IPlatform platform)
    : IPages
{
    private RootSession Session => sessions.Current;

    public object Home() => new HomeViewModel(sessions, settings, dialogs, navigator, this);

    public object Mode() => new ModeViewModel(Session.Get<AnyPS5Profile>(), Session.Get<IAnyPS5Installer>(), navigator, this);

    public object SystemCheck(AnyPS5Mode mode) => new SystemCheckViewModel(Session.Get<ISystemChecker>(), mode, platform, navigator, this);

    public object Tools() => new ToolsViewModel(Session.Get<BuildToolsSummary>(), navigator, this);

    public object Install(AnyPS5Mode mode) =>
        new InstallViewModel(Session.Get<IAnyPS5Installer>(), mode, Session.LogFile, platform, ui, navigator, this, busy => sessions.IsBusy = busy, Session.Get<ILogger<StepRunViewModel>>());

    public object Done() =>
        new DoneViewModel(Session.Get<IAnyPS5Installer>().ReadRecord(), Session.Get<AnyPS5Paths>(), Session.LogFile, platform, navigator, this);

    public object ChooseGame() => new ChooseGameViewModel(Session.Get<IGameImporter>(), dialogs, navigator, this, Session.Get<ILogger<ChooseGameViewModel>>(), busy => sessions.IsBusy = busy);

    public object GameSummary(PreparedGame game) =>
        new GameSummaryViewModel(game, Session.Get<ICpuInfo>().Read().IsIntel, platform, navigator, this, ConversionText(game));

    /// <summary>How an already added game's conversion compares with the install in use; null for a new game or an unreadable record.</summary>
    private string? ConversionText(PreparedGame game)
    {
        if (!game.AlreadyAdded || game.Metadata is not { } metadata)
        {
            return null;
        }

        try
        {
            var (_, record) = Session.Get<GameLibrary>().Find(metadata.TitleId);
            return GameSummaryViewModel.ConversionLine(record, Session.Get<IAnyPS5Installer>().ReadRecord());
        }
        catch (Exception exception) when (exception is GameNotFoundException or InvalidGameRecordException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public object AddGame(PreparedGame game, bool toIntel) =>
        new AddGameViewModel(Session.Get<IGameImporter>(), game, toIntel, Session.LogFile, platform, ui, navigator, this, busy => sessions.IsBusy = busy, Session.Get<ILogger<StepRunViewModel>>());

    public object AddResult(GameAdded added) => new AddResultViewModel(added, platform, navigator, this);

    public object Reconvert(string titleId, string name) =>
        new ReconvertViewModel(Session.Get<IGameReconverter>(), titleId, name, Session.LogFile, platform, ui, navigator, this, busy => sessions.IsBusy = busy, Session.Get<ILogger<StepRunViewModel>>());

    public object ReconvertResult(GameReconverted reconverted) => new AddResultViewModel(reconverted, platform, navigator, this);
}
