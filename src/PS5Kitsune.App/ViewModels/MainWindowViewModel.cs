using CommunityToolkit.Mvvm.ComponentModel;
using PS5Kitsune.Core;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.App.ViewModels;

/// <summary>The window shell: a header with what the app resolved at startup, and the current page.</summary>
public sealed partial class MainWindowViewModel(IPlatform platform, DataLayout layout, AnyPS5Profile profile, SystemCheckViewModel systemCheck) : ObservableObject
{
    public string Platform => platform.Name;

    public string DataRoot => layout.Root;

    public string Profile => $"{profile.Id} ({(profile.Validated ? "validated" : "not validated yet")})";

    public SystemCheckViewModel SystemCheck => systemCheck;

    /// <summary>The page shown. Milestone 4 adds the other pages and the navigation between them.</summary>
    public object CurrentPage => systemCheck;
}
