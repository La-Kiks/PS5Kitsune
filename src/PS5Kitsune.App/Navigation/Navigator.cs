using CommunityToolkit.Mvvm.ComponentModel;

namespace PS5Kitsune.App.Navigation;

/// <summary>The page shown in the window, with a back stack.</summary>
public sealed partial class Navigator : ObservableObject
{
    private readonly Stack<object> _history = new();

    [ObservableProperty]
    public partial object? CurrentPage { get; private set; }

    /// <summary>The current page, then the pages Back returns to, newest first.</summary>
    public IEnumerable<object> Pages => CurrentPage is null ? _history : _history.Prepend(CurrentPage);

    public void GoTo(object page)
    {
        if (CurrentPage is not null)
        {
            _history.Push(CurrentPage);
        }

        Show(page);
    }

    public void GoBack()
    {
        if (_history.TryPop(out var page))
        {
            Show(page);
        }
    }

    public void GoHome(object home)
    {
        _history.Clear();
        Show(home);
    }

    private void Show(object page)
    {
        CurrentPage = page;
        (page as INavigationAware)?.OnNavigatedTo();
    }
}
