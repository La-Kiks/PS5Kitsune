using Avalonia.Styling;
using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Settings;

namespace PS5Kitsune.App.Shell;

/// <summary>Applies the app's theme and saves the user's choice in settings.json.</summary>
/// <param name="apply">Sets the application's theme variant; tests record it instead.</param>
public sealed class ThemeService(AppSettingsStore store, Action<AppTheme> apply, Func<ILogger>? loggerSource = null)
{
    public AppTheme Current { get; private set; }

    public void Apply(AppTheme theme)
    {
        Current = theme;
        apply(theme);
    }

    /// <summary>Applies at once, then saves; a failed save is logged and the theme stays for this run.</summary>
    public async Task ChangeAsync(AppTheme theme)
    {
        Apply(theme);
        try
        {
            await store.SaveAsync(store.Load().Settings with { Theme = theme }).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            loggerSource?.Invoke().ThemeSaveFailed(exception);
        }
    }

    public static ThemeVariant VariantOf(AppTheme theme) => theme switch
    {
        AppTheme.Light => ThemeVariant.Light,
        AppTheme.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };
}
