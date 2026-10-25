using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Shell;
using PS5Kitsune.App.Tests.Headless;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.App.Views;
using PS5Kitsune.Core.Settings;
using PS5Kitsune.Core.Tests;

namespace PS5Kitsune.App.Tests;

[Collection("Headless")]
public sealed class ShellTests(HeadlessSession session) : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public Task The_sidebar_buttons_are_disabled_while_work_runs() => session.RunAsync(() =>
    {
        var navigator = new Navigator();
        var viewModel = new MainWindowViewModel(navigator, new FakeDialogs(), new FakePages(), new ThemeService(new AppSettingsStore(_temp.Combine("s")), _ => { }));
        var window = new MainWindow { DataContext = viewModel };
        try
        {
            window.Show();
            navigator.GoTo(new BusyPage());
            Dispatcher.UIThread.RunJobs();

            var library = window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Library"));

            Assert.False(library.IsEffectivelyEnabled);
        }
        finally
        {
            window.Close();
        }
    });

    private sealed partial class BusyPage : ObservableObject, IRunningWork
    {
        public bool IsRunning => true;

        public string? QuitMessage => null;

        public Task CancelAndWaitAsync() => Task.CompletedTask;
    }

    public void Dispose() => _temp.Dispose();
}
