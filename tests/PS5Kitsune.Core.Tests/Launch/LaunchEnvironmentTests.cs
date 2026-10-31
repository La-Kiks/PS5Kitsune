using PS5Kitsune.Core.Launch;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Tests.Launch;

public sealed class LaunchEnvironmentTests
{
    private const string Manifest = @"C:\PS5Kitsune\tools\mesa\lvp_icd.x86_64.json";

    private const string Fonts = @"C:\PS5Kitsune\fonts";

    [Fact]
    public void Default_settings_without_fonts_pass_nothing() =>
        Assert.Empty(LaunchEnvironment.For(new GameSettings(), null, null));

    [Theory]
    [MemberData(nameof(OneSetting))]
    public void Each_setting_becomes_its_variable(GameSettings settings, string name, string value) =>
        Assert.Equal(new Dictionary<string, string> { [name] = value }, LaunchEnvironment.For(settings, null, null));

    public static TheoryData<GameSettings, string, string> OneSetting => new()
    {
        { new GameSettings { Gpu = "Radeon" }, "ANYPS5_GPU", "Radeon" },
        { new GameSettings { DisableDozen = true }, "VK_LOADER_DRIVERS_DISABLE", "*dzn*" },
        { new GameSettings { Language = 21 }, "ANYPS5_LANGUAGE", "21" },
        { new GameSettings { Language = 0 }, "ANYPS5_LANGUAGE", "0" },
        { new GameSettings { PresentMode = "mailbox" }, "ANYPS5_PRESENT_MODE", "mailbox" },
        { new GameSettings { Overlay = true }, "ANYPS5_OVERLAY", "1" },
        { new GameSettings { PlayGoUnlistedChunks = "installed" }, "APS5_PLAYGO_UNLISTED_CHUNKS", "installed" },
        { new GameSettings { Resolution = "1080p" }, "APS5_OUTPUT_RESOLUTION", "1920x1080" },
        { new GameSettings { Resolution = "2160p" }, "APS5_OUTPUT_RESOLUTION", "3840x2160" },
    };

    [Fact]
    public void The_overlay_position_is_passed_only_with_the_overlay_and_when_not_top_left()
    {
        Assert.Empty(LaunchEnvironment.For(new GameSettings { OverlayPosition = "br" }, null, null));
        Assert.Equal(
            new Dictionary<string, string> { ["ANYPS5_OVERLAY"] = "1", ["ANYPS5_OVERLAY_POS"] = "br" },
            LaunchEnvironment.For(new GameSettings { Overlay = true, OverlayPosition = "br" }, null, null));
    }

    [Fact]
    public void Lavapipe_overrides_the_gpu_and_dozen()
    {
        var environment = LaunchEnvironment.For(new GameSettings { Lavapipe = true, Gpu = "Radeon", DisableDozen = true }, Manifest, null);

        Assert.Equal(new Dictionary<string, string> { ["VK_DRIVER_FILES"] = Manifest, ["ANYPS5_GPU"] = "llvmpipe" }, environment);
    }

    [Fact]
    public void Fonts_are_passed_when_installed() =>
        Assert.Equal(new Dictionary<string, string> { ["ANYPS5_SYSTEM_FONTS"] = Fonts }, LaunchEnvironment.For(new GameSettings(), null, Fonts));

    [Fact]
    public void Lavapipe_without_a_manifest_is_a_programming_error() =>
        Assert.Throws<ArgumentException>(() => LaunchEnvironment.For(new GameSettings { Lavapipe = true }, null, null));
}
