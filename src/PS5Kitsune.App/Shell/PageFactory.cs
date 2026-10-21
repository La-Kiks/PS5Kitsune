using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Sessions;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Settings;

namespace PS5Kitsune.App.Shell;

/// <summary>Builds each page from the services of the current data root.</summary>
public sealed class PageFactory(SessionHost sessions, Navigator navigator, IDialogService dialogs, AppSettingsStore settings, IUiDispatcher ui, IPlatform platform)
    : IPages
{
    private RootSession Session => sessions.Current;

    public object Home() => new HomeViewModel(sessions, settings, dialogs, navigator, this);

    public object SystemCheck() => new SystemCheckViewModel(Session.Get<ISystemChecker>(), platform, navigator, this);

    public object Mode() => new ModeViewModel(Session.Get<AnyPS5Profile>(), navigator, this);

    public object Install() =>
        new InstallViewModel(Session.Get<IAnyPS5Installer>(), Session.LogFile, platform, ui, navigator, this, busy => sessions.IsBusy = busy);

    public object Done() =>
        new DoneViewModel(Session.Get<IAnyPS5Installer>().GetState().Record, Session.Get<AnyPS5Paths>(), Session.LogFile, platform, navigator, this);
}
