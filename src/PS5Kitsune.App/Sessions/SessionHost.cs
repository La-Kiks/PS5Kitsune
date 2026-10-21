namespace PS5Kitsune.App.Sessions;

/// <summary>Holds the session of the current data root and replaces it when the user picks another root.</summary>
public sealed class SessionHost : IDisposable
{
    private readonly Func<string, RootSession> _create;

    public SessionHost(Func<string, RootSession> create, string root)
    {
        _create = create;
        Current = create(root);
    }

    public RootSession Current { get; private set; }

    /// <summary>Set while an install runs: its services must not be disposed under it.</summary>
    public bool IsBusy { get; set; }

    public event EventHandler? SessionChanged;

    /// <exception cref="InvalidOperationException">An install is running.</exception>
    public void ChangeRoot(string root)
    {
        if (IsBusy)
        {
            throw new InvalidOperationException("The data root cannot change while an install runs.");
        }

        var previous = Current;
        Current = _create(root);
        previous.Dispose();
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => Current.Dispose();
}
