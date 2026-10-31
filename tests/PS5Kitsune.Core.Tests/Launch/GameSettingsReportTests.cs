using PS5Kitsune.Core.Launch;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Tests.Launch;

public sealed class GameSettingsReportTests
{
    private const string Manifest = @"C:\PS5Kitsune\tools\mesa\lvp_icd.x86_64.json";

    private const string Fonts = @"C:\PS5Kitsune\fonts";

    [Fact]
    public void Defaults_with_fonts_installed()
    {
        var lines = GameSettingsReport.Lines(new GameSettings(), Manifest, lavapipeInstalled: false, "launch the game again to download it", Fonts, fontsInstalled: true);

        Assert.Equal(
            [
                "GPU: automatic (default)",
                "CPU rendering (Lavapipe): off (default)",
                "Game language: 1, English (United States) (default)",
                "Vertical sync: fifo (default)",
                "Disable Dozen: off (default)",
                "Performance overlay: off (default)",
                "Overlay position: tl (default)",
                "PlayGo unlisted chunks: unset (default)",
                "Resolution: match the display (default)",
                "Environment at launch:",
                @"  ANYPS5_SYSTEM_FONTS=C:\PS5Kitsune\fonts",
            ],
            lines);
    }

    [Fact]
    public void Defaults_without_fonts_in_the_profile_pass_nothing()
    {
        var lines = GameSettingsReport.Lines(new GameSettings(), null, lavapipeInstalled: false, null, null, fontsInstalled: false);

        Assert.Equal(["Environment at launch:", "  (none)"], lines.Skip(9));
    }

    [Fact]
    public void A_full_set_before_the_first_launch()
    {
        var settings = new GameSettings
        {
            Gpu = "Radeon", Lavapipe = true, Language = 2, PresentMode = "mailbox", DisableDozen = true, Overlay = true, OverlayPosition = "br", PlayGoUnlistedChunks = "absent",
        };

        var lines = GameSettingsReport.Lines(settings, Manifest, lavapipeInstalled: false, "launch the game again to download it", Fonts, fontsInstalled: false);

        Assert.Equal(
            [
                "GPU: Radeon",
                "CPU rendering (Lavapipe): on",
                "Game language: 2, French",
                "Vertical sync: mailbox",
                "Disable Dozen: on",
                "Performance overlay: on",
                "Overlay position: br",
                "PlayGo unlisted chunks: absent",
                "Resolution: match the display (default)",
                "Environment at launch:",
                "  ANYPS5_GPU=llvmpipe",
                "  ANYPS5_LANGUAGE=2",
                "  ANYPS5_OVERLAY=1",
                "  ANYPS5_OVERLAY_POS=br",
                "  ANYPS5_PRESENT_MODE=mailbox",
                @"  ANYPS5_SYSTEM_FONTS=C:\PS5Kitsune\fonts (downloaded on first launch)",
                "  APS5_PLAYGO_UNLISTED_CHUNKS=absent",
                @"  VK_DRIVER_FILES=C:\PS5Kitsune\tools\mesa\lvp_icd.x86_64.json (not installed yet: launch the game again to download it)",
            ],
            lines);
    }

    [Fact]
    public void A_missing_lavapipe_says_what_to_do()
    {
        var lines = GameSettingsReport.Lines(new GameSettings { Lavapipe = true }, "/usr/share/vulkan/icd.d/lvp_icd.x86_64.json", lavapipeInstalled: false, "install it with: sudo apt install mesa-vulkan-drivers", null, false);

        Assert.Contains("  VK_DRIVER_FILES=/usr/share/vulkan/icd.d/lvp_icd.x86_64.json (not installed yet: install it with: sudo apt install mesa-vulkan-drivers)", lines);
    }

    [Fact]
    public void Lavapipe_without_a_mesa_download_says_so()
    {
        var lines = GameSettingsReport.Lines(new GameSettings { Lavapipe = true }, null, lavapipeInstalled: false, null, null, fontsInstalled: false);

        Assert.Equal(["Environment at launch:", "  (none)", "  Lavapipe is not available: this profile has no Mesa download."], lines.Skip(9));
    }

    [Theory]
    [InlineData("1080p", "Resolution: 1080p")]
    [InlineData("2160p", "Resolution: 4K")]
    public void The_resolution_line_names_the_choice(string resolution, string line) =>
        Assert.Contains(line, GameSettingsReport.Lines(new GameSettings { Resolution = resolution }, null, lavapipeInstalled: false, null, null, fontsInstalled: false));
}
