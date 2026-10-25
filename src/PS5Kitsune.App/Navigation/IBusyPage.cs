using System.ComponentModel;

namespace PS5Kitsune.App.Navigation;

/// <summary>A page that goes back on its own after an await: until it has, the sidebar must not leave it a second time.</summary>
public interface IBusyPage : INotifyPropertyChanged
{
    /// <summary>The page's own Back is in progress. Raises <see cref="INotifyPropertyChanged.PropertyChanged"/> when it changes.</summary>
    bool IsLeaving { get; }
}
