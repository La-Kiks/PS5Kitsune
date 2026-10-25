using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using PS5Kitsune.App.Tests.Headless;

namespace PS5Kitsune.App.Tests;

[Collection("Headless")]
public sealed class ThemeTests(HeadlessSession session)
{
    public static TheoryData<string> Variants => ["Light", "Dark"];

    // Text and its background; the WCAG AA minimum for body text is 4.5:1.
    private static readonly (string Text, string Background)[] TextPairs =
    [
        ("Text.Primary", "Surface.Page"), ("Text.Muted", "Surface.Page"), ("Text.Title", "Surface.Page"),
        ("Text.Primary", "Surface.Raised"), ("Text.Muted", "Surface.Raised"), ("Text.Title", "Surface.Raised"),
        ("Text.Primary", "Surface.Sidebar"), ("Text.Muted", "Surface.Sidebar"),
        ("Error", "Surface.Page"), ("Warning", "Surface.Page"),
        ("OnAccent", "Accent"), ("OnAccent", "Accent.Hover"),
        ("OnAccent", "Status.Green"), ("OnAccent", "Status.Orange"), ("OnAccent", "Status.Red"),
        ("OnAccent", "Status.Reconvert"), ("OnAccent", "Status.Running"),
        ("Text.Primary", "Border.Strong"),
    ];

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task Every_text_pair_reaches_AA(string variant)
    {
        var failures = await session.RunAsync(() =>
        {
            var theme = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
            return TextPairs
                .Select(pair => (pair, ratio: Contrast(Color(pair.Text, theme), Color(pair.Background, theme))))
                .Where(result => result.ratio < 4.5)
                .Select(result => $"{result.pair.Text} on {result.pair.Background}: {result.ratio:0.00}")
                .ToList();
        });

        Assert.Empty(failures);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task The_accent_is_visible_as_a_focus_ring_on_the_page(string variant)
    {
        var ratio = await session.RunAsync(() =>
        {
            var theme = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
            return Contrast(Color("Accent", theme), Color("Surface.Page", theme));
        });

        // WCAG non-text contrast.
        Assert.True(ratio >= 3, $"{ratio:0.00}");
    }

    [Fact]
    public async Task Inter_is_the_default_font()
    {
        var name = await session.RunAsync(() => FontManager.Current.DefaultFontFamily.Name);

        Assert.Equal("Inter", name);
    }

    private static Color Color(string key, ThemeVariant theme) =>
        Application.Current!.TryGetResource(key, theme, out var value) && value is ISolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException($"No brush {key} in {theme}.");

    private static double Contrast(Color a, Color b)
    {
        var (first, second) = (Luminance(a), Luminance(b));
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }

    private static double Luminance(Color color) => 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);

    private static double Channel(byte value)
    {
        var c = value / 255.0;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
