namespace PS5Kitsune.Core.Library;

public sealed record GameLanguage(int Number, string Name);

/// <summary>The console languages a game can be given through ANYPS5_LANGUAGE, as AnyPS5's USAGE.md lists them.</summary>
public static class GameLanguages
{
    /// <summary>English (United States), AnyPS5's default.</summary>
    public const int Default = 1;

    public static IReadOnlyList<GameLanguage> All { get; } =
    [
        new(0, "Japanese"),
        new(1, "English (United States)"),
        new(2, "French"),
        new(3, "Spanish"),
        new(4, "German"),
        new(5, "Italian"),
        new(6, "Dutch"),
        new(7, "Portuguese (Portugal)"),
        new(8, "Russian"),
        new(9, "Korean"),
        new(10, "Chinese (Traditional)"),
        new(11, "Chinese (Simplified)"),
        new(12, "Finnish"),
        new(13, "Swedish"),
        new(14, "Danish"),
        new(15, "Norwegian"),
        new(16, "Polish"),
        new(17, "Portuguese (Brazil)"),
        new(18, "English (United Kingdom)"),
        new(19, "Turkish"),
        new(20, "Spanish (Latin America)"),
        new(21, "Arabic"),
        new(22, "French (Canada)"),
        new(23, "Czech"),
        new(24, "Hungarian"),
        new(25, "Greek"),
        new(26, "Romanian"),
        new(27, "Thai"),
        new(28, "Vietnamese"),
        new(29, "Indonesian"),
        new(30, "Ukrainian"),
    ];

    /// <exception cref="ArgumentOutOfRangeException">Not a console language number.</exception>
    public static string Name(int number) =>
        All.FirstOrDefault(language => language.Number == number)?.Name ?? throw new ArgumentOutOfRangeException(nameof(number), number, "Not a console language number.");
}
