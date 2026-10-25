using PS5Kitsune.App.Navigation;

namespace PS5Kitsune.App.Tests;

public sealed class NavigatorTests
{
    private readonly Navigator _navigator = new();

    [Fact]
    public void GoTo_shows_the_page_and_tells_it()
    {
        var page = new AwarePage();

        _navigator.GoTo(page);

        Assert.Same(page, _navigator.CurrentPage);
        Assert.Equal(1, page.Visits);
    }

    [Fact]
    public void GoBack_returns_to_the_previous_page_and_tells_it_again()
    {
        var first = new AwarePage();
        _navigator.GoTo(first);
        _navigator.GoTo("second");

        _navigator.GoBack();

        Assert.Same(first, _navigator.CurrentPage);
        Assert.Equal(2, first.Visits);
    }

    [Fact]
    public void GoBack_with_no_history_stays()
    {
        _navigator.GoTo("only");

        _navigator.GoBack();

        Assert.Equal("only", _navigator.CurrentPage);
    }

    [Fact]
    public void GoHome_clears_the_history()
    {
        _navigator.GoTo("a");
        _navigator.GoTo("b");

        _navigator.GoHome("home");
        _navigator.GoBack();

        Assert.Equal("home", _navigator.CurrentPage);
    }

    [Fact]
    public void CurrentPage_raises_PropertyChanged()
    {
        var changed = new List<string?>();
        _navigator.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _navigator.GoTo("a");

        Assert.Contains(nameof(Navigator.CurrentPage), changed);
    }

    [Fact]
    public void Pages_lists_the_current_page_then_the_history_newest_first()
    {
        var navigator = new Navigator();
        navigator.GoTo("a");
        navigator.GoTo("b");
        navigator.GoTo("c");

        Assert.Equal(["c", "b", "a"], navigator.Pages);
    }

    private sealed class AwarePage : INavigationAware
    {
        public int Visits { get; private set; }

        public void OnNavigatedTo() => Visits++;
    }
}
