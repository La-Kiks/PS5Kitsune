using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;

namespace PS5Kitsune.App.ViewModels;

/// <summary>The end of the install. Adding a game arrives with milestone 6, so this page only leads home.</summary>
public sealed partial class DoneViewModel(AnyPS5InstallRecord? record, AnyPS5Paths paths, string logFile, IPlatform platform, Navigator navigator, IPages pages)
    : ObservableObject
{
    public string Title => record is null ? "AnyPS5 is installed" : $"AnyPS5 {record.Version} is installed";

    public string Relinker => paths.Relinker;

    public string Libs => paths.Libs;

    [ObservableProperty]
    public partial string? OpenError { get; private set; }

    [RelayCommand]
    private void OpenFolder() => OpenError = PathOpener.TryOpen(platform, paths.RootDirectory);

    [RelayCommand]
    private void OpenLog() => OpenError = PathOpener.TryOpen(platform, logFile);

    [RelayCommand]
    private void BackHome() => navigator.GoHome(pages.Home());
}
