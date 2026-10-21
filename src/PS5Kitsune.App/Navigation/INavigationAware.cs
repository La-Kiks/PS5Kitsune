namespace PS5Kitsune.App.Navigation;

/// <summary>A page that wants to know when it is shown, including when the user comes back to it.</summary>
public interface INavigationAware
{
    void OnNavigatedTo();
}
