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
    public void An_unknown_resolution_reads_as_following_the_display()
    {
        var (settings, replaced) = GameSettingsValidator.Normalize(new GameSettings { Resolution = "1440p" });

        Assert.Null(settings.Resolution);
        Assert.Equal(["resolution"], replaced);
    }

    [Theory]
    [InlineData("1080p")]
    [InlineData("2160p")]
    public void A_known_resolution_is_kept(string resolution)
    {
        var (settings, replaced) = GameSettingsValidator.Normalize(new GameSettings { Resolution = resolution });

        Assert.Equal(resolution, settings.Resolution);
        Assert.Empty(replaced);
    }

    [Fact]
    public void Languages_cover_0_to_30_by_name()
    {
        Assert.Equal(Enumerable.Range(0, 31), GameLanguages.All.Select(language => language.Number));
        Assert.Equal("English (United States)", GameLanguages.Name(1));
        Assert.Equal("Arabic", GameLanguages.Name(21));
        Assert.Equal("Ukrainian", GameLanguages.Name(30));
    }

    [Fact]
    public void Extra_variables_with_a_bad_or_reserved_name_or_no_value_are_dropped()
    {
        var (settings, replaced) = GameSettingsValidator.Normalize(new GameSettings
        {
            ExtraEnvironment = new Dictionary<string, string> { ["APS5_HOST_IMPORT_MIB"] = "6144", ["ANYPS5_GPU"] = "x", ["vk_x"] = "1", ["APS5_NULL"] = null! },
        });

        Assert.Equal(new Dictionary<string, string> { ["APS5_HOST_IMPORT_MIB"] = "6144" }, settings.ExtraEnvironment);
        Assert.Equal(["extraEnvironment"], replaced);
    }

    [Theory]
    [InlineData("a\nb")]
    [InlineData("a\rb")]
    [InlineData("a\0b")]
    public void Extra_variables_whose_value_has_a_line_break_or_nul_are_dropped(string value)
    {
        var (settings, replaced) = GameSettingsValidator.Normalize(new GameSettings
        {
            ExtraEnvironment = new Dictionary<string, string> { ["APS5_HOST_IMPORT_MIB"] = "6144", ["APS5_BAD"] = value },
        });

        Assert.Equal(new Dictionary<string, string> { ["APS5_HOST_IMPORT_MIB"] = "6144" }, settings.ExtraEnvironment);
        Assert.Equal(["extraEnvironment"], replaced);
    }

    [Fact]
    public void Valid_extra_variables_are_kept()
    {
        var chosen = new GameSettings { ExtraEnvironment = new Dictionary<string, string> { ["APS5_HOST_IMPORT_MIB"] = "6144" } };

        var (settings, replaced) = GameSettingsValidator.Normalize(chosen);

        Assert.Equal(chosen, settings);
        Assert.Empty(replaced);
    }
}
