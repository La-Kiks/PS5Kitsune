using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Tests.Library;

public sealed class GameSettingsValidatorTests
{
    [Fact]
    public void Defaults_are_valid()
    {
        var (settings, replaced) = GameSettingsValidator.Normalize(new GameSettings());

        Assert.Equal(new GameSettings(), settings);
        Assert.Empty(replaced);
    }

    [Fact]
    public void Valid_values_are_kept()
    {
        var chosen = new GameSettings
        {
            Gpu = "Radeon", Lavapipe = true, Language = 30, PresentMode = "immediate", DisableDozen = true, Overlay = true, OverlayPosition = "br", PlayGoUnlistedChunks = "absent",
        };

        var (settings, replaced) = GameSettingsValidator.Normalize(chosen);

        Assert.Equal(chosen, settings);
        Assert.Empty(replaced);
    }

    [Fact]
    public void Each_invalid_value_becomes_its_default()
    {
        var invalid = new GameSettings { Gpu = "  ", Language = 31, PresentMode = "vsync", OverlayPosition = "middle", PlayGoUnlistedChunks = "maybe" };

        var (settings, replaced) = GameSettingsValidator.Normalize(invalid);

        Assert.Equal(new GameSettings(), settings);
        Assert.Equal(["gpu", "language", "presentMode", "overlayPosition", "playGoUnlistedChunks"], replaced);
    }

    [Fact]
    public void A_negative_language_and_a_null_mode_become_defaults()
    {
        var (settings, replaced) = GameSettingsValidator.Normalize(new GameSettings { Language = -1, PresentMode = null!, OverlayPosition = null! });

        Assert.Equal(new GameSettings(), settings);
        Assert.Equal(["language", "presentMode", "overlayPosition"], replaced);
    }

    [Fact]
    public void Languages_cover_0_to_30_by_name()
    {
        Assert.Equal(Enumerable.Range(0, 31), GameLanguages.All.Select(language => language.Number));
        Assert.Equal("English (United States)", GameLanguages.Name(1));
        Assert.Equal("Arabic", GameLanguages.Name(21));
        Assert.Equal("Ukrainian", GameLanguages.Name(30));
    }
}
