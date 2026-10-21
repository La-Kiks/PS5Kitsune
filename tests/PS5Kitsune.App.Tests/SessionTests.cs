using Microsoft.Extensions.Logging;
using PS5Kitsune.App.Sessions;
using PS5Kitsune.Core.Tests;

namespace PS5Kitsune.App.Tests;

public sealed partial class SessionTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void Changing_the_root_replaces_and_disposes_the_session()
    {
        using var host = new SessionHost(root => TestSessions.Create(root, new FakeInstaller()), _temp.Combine("a"));
        var first = host.Current;
        var changes = 0;
        host.SessionChanged += (_, _) => changes++;

        host.ChangeRoot(_temp.Combine("b"));

        Assert.NotSame(first, host.Current);
        Assert.Equal(_temp.Combine("b"), host.Current.Layout.Root);
        Assert.True(first.IsDisposed);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void The_root_cannot_change_while_busy()
    {
        using var host = new SessionHost(root => TestSessions.Create(root, new FakeInstaller()), _temp.Combine("a"));
        host.IsBusy = true;

        Assert.Throws<InvalidOperationException>(() => host.ChangeRoot(_temp.Combine("b")));
        Assert.Equal(_temp.Combine("a"), host.Current.Layout.Root);
    }

    [Fact]
    public void Each_session_logs_under_its_own_root()
    {
        var profile = Core.Profiles.ProfileLoader.LoadEmbedded();
        string firstLog;
        using (var first = RootSession.Create(_temp.Combine("a"), new TestPlatform(), profile))
        {
            Write(first.Get<ILogger<SessionTests>>(), "first session");
            firstLog = first.LogFile;
        }

        // The disposed session released its file: nothing else can hold it open.
        using (new FileStream(firstLog, FileMode.Open, FileAccess.Read, FileShare.None))
        {
        }

        using var second = RootSession.Create(_temp.Combine("b"), new TestPlatform(), profile);
        Write(second.Get<ILogger<SessionTests>>(), "second session");

        Assert.StartsWith(_temp.Combine("a", "logs"), firstLog, StringComparison.Ordinal);
        Assert.StartsWith(_temp.Combine("b", "logs"), second.LogFile, StringComparison.Ordinal);
        Assert.Contains("first session", File.ReadAllText(firstLog), StringComparison.Ordinal);
        Assert.Contains("second session", ReadLog(second.LogFile), StringComparison.Ordinal);
    }

    /// <summary>Reads a log the live session still holds open for writing.</summary>
    private static string ReadLog(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Text}")]
    private static partial void Write(ILogger logger, string text);

    public void Dispose() => _temp.Dispose();
}
