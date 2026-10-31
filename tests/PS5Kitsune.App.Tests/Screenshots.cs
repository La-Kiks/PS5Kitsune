using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Shell;
using PS5Kitsune.App.Views;
using PS5Kitsune.App.Tests.Headless;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Settings;

namespace PS5Kitsune.App.Tests;

[Collection("Headless")]
public sealed class Screenshots(HeadlessSession session)
{
    [Fact]
    public async Task Render_every_page()
    {
        var folder = Environment.GetEnvironmentVariable("PS5K_SCREENSHOTS");
        if (string.IsNullOrEmpty(folder))
        {
            Assert.Skip("Set PS5K_SCREENSHOTS to a folder to render every page.");
        }

        Directory.CreateDirectory(folder);
        using var gallery = new PageGallery();
        try
        {
            foreach (var (name, page) in await gallery.BuildAsync())
            {
                foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                {
                    await session.RunAsync(() =>
                    {
                        Application.Current!.RequestedThemeVariant = variant;
                        var navigator = new Navigator();
                        var window = new MainWindow
                        {
                            DataContext = new MainWindowViewModel(navigator, new FakeDialogs(), new FakePages(), new ThemeService(new AppSettingsStore(gallery.SettingsFolder), _ => { })),
                            Width = 860,
                            // The Advanced section is at the bottom of the Settings page.
                            Height = name.EndsWith("-Advanced", StringComparison.Ordinal) ? 1100 : 680,
                        };
                        try
                        {
                            window.Show();
                            navigator.GoTo(page);
                            Dispatcher.UIThread.RunJobs();
                            Dispatcher.UIThread.RunJobs();

                            // Fluent animates the Expander chevron when a page appears; a frame captured mid-flight shows it tilted.
                            Thread.Sleep(500);
                            Dispatcher.UIThread.RunJobs();
                            window.CaptureRenderedFrame()!.Save(Path.Combine(folder, $"{name}-{variant.Key}.png"), new PngBitmapEncoderOptions());
                        }
                        finally
                        {
                            navigator.GoTo("end");
                            window.Close();
                        }
                    });
                }
            }

            foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                await session.RunAsync(() =>
                {
                    Application.Current!.RequestedThemeVariant = variant;
                    var notice = new UpdateNoticeViewModel(_ => { })
                    {
                        Update = new PS5Kitsune.Core.Updates.UpdateInfo(new PS5Kitsune.Core.Updates.AppVersion(0, 2, 0, false), new Uri("https://github.com/La-Kiks/PS5Kitsune/releases/tag/v0.2.0")),
                    };
                    var window = new MainWindow
                    {
                        DataContext = new MainWindowViewModel(new Navigator(), new FakeDialogs(), new FakePages(), new ThemeService(new AppSettingsStore(gallery.SettingsFolder), _ => { }), updateNotice: notice),
                        Width = 860,
                        Height = 680,
                    };
                    try
                    {
                        window.Show();
                        Dispatcher.UIThread.RunJobs();
                        window.CaptureRenderedFrame()!.Save(Path.Combine(folder, $"update-notice-{variant.Key}.png"), new PngBitmapEncoderOptions());
                    }
                    finally
                    {
                        window.Close();
                    }
                });
            }
        }
        finally
        {
            await session.RunAsync(() => Application.Current!.RequestedThemeVariant = ThemeVariant.Default);
        }
    }
}
