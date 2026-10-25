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

    /// <summary>
    /// Applies at once, then saves; a failed save is logged and the theme stays for this run. The store runs its updates one at a time
    /// in the order they were started, so the last choice wins.
    /// </summary>
    public async Task ChangeAsync(AppTheme theme)
    {
        Apply(theme);
        try
        {
            if (!await store.UpdateAsync(settings => settings with { Theme = theme }).ConfigureAwait(true))
            {
                // The file was ignored at load: saving would replace it with defaults, so the theme only holds for this run.
                loggerSource?.Invoke().ThemeNotSaved(store.Load().Problem ?? "the settings file could not be used.");
            }
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
