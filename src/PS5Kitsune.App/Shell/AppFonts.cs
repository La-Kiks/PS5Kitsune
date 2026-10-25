using Avalonia;
using Avalonia.Media;

namespace PS5Kitsune.App.Shell;

public static class AppFonts
{
    /// <summary>Embeds Inter and makes it the default family, so windows, flyouts and tooltips all use it on every OS.</summary>
    public static AppBuilder UseAppFonts(this AppBuilder builder) =>
        builder.WithInterFont().With(new FontManagerOptions { DefaultFamilyName = "fonts:Inter#Inter" });
}
