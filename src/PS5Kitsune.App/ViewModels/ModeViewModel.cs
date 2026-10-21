using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.App.ViewModels;

/// <summary>Release or build. Build mode arrives with milestone 5, so only Release can be chosen.</summary>
public sealed partial class ModeViewModel(AnyPS5Profile profile, Navigator navigator, IPages pages)
{
    public string ReleaseTitle => $"Release {profile.Source.Version}";

    public string PublishedText => profile.Source.PublishedAt is { } publishedAt
        ? "Published " + publishedAt.UtcDateTime.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture)
        : "Publication date unknown";

    public string ProfileStatusText => profile.Validated ? "Validated with DOOM for PS5" : "Not validated yet";

    [RelayCommand]
    private void Back() => navigator.GoBack();

    [RelayCommand]
    private void Install() => navigator.GoTo(pages.Install());
}
