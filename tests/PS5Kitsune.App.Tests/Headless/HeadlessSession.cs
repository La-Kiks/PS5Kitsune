using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using PS5Kitsune.App.Shell;

namespace PS5Kitsune.App.Tests.Headless;

/// <summary>The app's styles without its composition root, which reads the real settings and data root.</summary>
public sealed class HeadlessTestApp : Application
{
    public override void Initialize() =>
        Styles.Add(new StyleInclude(new Uri("avares://PS5Kitsune.App.Tests/")) { Source = new Uri("avares://PS5Kitsune/Styles/AppTheme.axaml") });

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<HeadlessTestApp>()
            .UseSkia()
            .UseAppFonts()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

/// <summary>
/// One Avalonia UI thread for the whole test run. It is never disposed: HeadlessUnitTestSession.Dispose hangs, and the session ends with
/// the test process.
/// </summary>
public sealed class HeadlessSession
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(HeadlessTestApp));

    public Task RunAsync(Action action) => _session.Dispatch(action, TestContext.Current.CancellationToken);

    public Task<T> RunAsync<T>(Func<T> action) => _session.Dispatch(action, TestContext.Current.CancellationToken);

    /// <summary>Runs async UI work on the UI thread; its continuations stay there.</summary>
    public Task<T> RunUiAsync<T>(Func<Task<T>> action) => _session.Dispatch(action, TestContext.Current.CancellationToken);
}

[CollectionDefinition("Headless")]
public sealed class HeadlessCollectionDefinition : ICollectionFixture<HeadlessSession>;
