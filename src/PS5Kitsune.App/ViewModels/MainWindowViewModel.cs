using CommunityToolkit.Mvvm.ComponentModel;
using PS5Kitsune.Core;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.App.ViewModels;

/// <summary>Milestone 1 placeholder: shows what the app resolved at startup.</summary>
public sealed partial class MainWindowViewModel(IPlatform platform, DataLayout layout, AnyPS5Profile profile) : ObservableObject
{
    public string Platform => platform.Name;

    public string DataRoot => layout.Root;

    public string Profile => $"{profile.Id} ({(profile.Validated ? "validated" : "not validated yet")})";
}
