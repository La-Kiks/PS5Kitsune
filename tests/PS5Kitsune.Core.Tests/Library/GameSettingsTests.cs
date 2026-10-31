using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Tests.Library;

public sealed class GameSettingsTests
{
    [Fact]
    public void Extra_variables_compare_by_content()
    {
        var first = new GameSettings { ExtraEnvironment = new Dictionary<string, string> { ["APS5_A"] = "1", ["APS5_B"] = "2" } };
        var second = new GameSettings { ExtraEnvironment = new Dictionary<string, string> { ["APS5_B"] = "2", ["APS5_A"] = "1" } };

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, second with { ExtraEnvironment = new Dictionary<string, string> { ["APS5_A"] = "1", ["APS5_B"] = "3" } });
        Assert.NotEqual(first, new GameSettings());
    }

    [Fact]
    public void Every_other_field_still_takes_part_in_equality()
    {
        var defaults = new GameSettings();
        GameSettings[] changed =
        [
            defaults with { Gpu = "Radeon" },
            defaults with { Lavapipe = true },
            defaults with { Language = 2 },
            defaults with { PresentMode = "mailbox" },
            defaults with { DisableDozen = true },
            defaults with { Overlay = true },
            defaults with { OverlayPosition = "br" },
            defaults with { PlayGoUnlistedChunks = "installed" },
            defaults with { Resolution = "1080p" },
        ];

        Assert.All(changed, settings => Assert.NotEqual(defaults, settings));

        // A new field must be added to Equals, GetHashCode and the list above.
        Assert.Equal(10, typeof(GameSettings).GetProperties().Length);
    }

    [Fact]
    public void Extra_variables_are_kept_sorted_and_null_reads_as_none()
    {
        var settings = new GameSettings { ExtraEnvironment = new Dictionary<string, string> { ["APS5_B"] = "2", ["ANYPS5_A"] = "1" } };

        Assert.Equal(["ANYPS5_A", "APS5_B"], settings.ExtraEnvironment.Keys);
        Assert.Empty(new GameSettings { ExtraEnvironment = null! }.ExtraEnvironment);
    }
}
