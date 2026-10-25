using Avalonia.Styling;
using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Settings;

namespace PS5Kitsune.App.Shell;

/// <summary>Applies the app's theme and saves the user's choice in settings.json.</summary>
/// <param name="apply">Sets the application's theme variant; tests record it instead.</param>
public sealed class ThemeService(AppSettingsStore store, Action<AppTheme> apply, Func<ILogger>? loggerSource = null)
{
    private Task _lastSave = Task.CompletedTask;

    public AppTheme Current { get; private set; }

    public void Apply(AppTheme theme)
    {
        Current = theme;
        apply(theme);
    }

    /// <summary>
    /// Applies at once, then saves; a failed save is logged and the theme stays for this run. Saves run one at a time, each writing the
    /// latest choice, because overlapping writes would fight over the same temporary file.
    /// </summary>
    public Task ChangeAsync(AppTheme theme)
    {
        Apply(theme);
        return _lastSave = SaveAfterAsync(_lastSave);
    }

    private async Task SaveAfterAsync(Task previous)
    {
        // WhenAny never throws, so one failed turn cannot block the next.
        await Task.WhenAny(previous).ConfigureAwait(true);
        try
        {
            await store.SaveAsync(store.Load().Settings with { Theme = Current }).ConfigureAwait(true);
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
