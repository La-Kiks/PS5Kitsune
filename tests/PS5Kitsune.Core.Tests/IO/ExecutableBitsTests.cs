using System.Runtime.Versioning;
using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Core.Tests.IO;

public sealed class ExecutableBitsTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    [SupportedOSPlatform("linux")]
    public void Ensure_adds_the_execute_bits_and_keeps_the_others()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        var file = _temp.Combine("PPSA99666.elf");
        File.WriteAllText(file, "elf");
        File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);

        Assert.False(ExecutableBits.Has(file));
        ExecutableBits.Ensure(file);

        Assert.True(ExecutableBits.Has(file));
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute,
            File.GetUnixFileMode(file));
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    public void EnsureGame_covers_every_file_with_the_game_extension_beside_it()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        var game = _temp.Combine("PPSA99666.elf");
        var picker = _temp.Combine("picker.picker.bin.elf");
        var registry = _temp.Combine("PPSA99666.registry.json");
        foreach (var file in new[] { game, picker, registry })
        {
            File.WriteAllText(file, "x");
            File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        ExecutableBits.EnsureGame(game);

        Assert.True(ExecutableBits.Has(game));
        Assert.True(ExecutableBits.Has(picker));
        Assert.False(ExecutableBits.Has(registry));
    }

    [Fact]
    public void On_windows_everything_counts_as_executable()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        var file = _temp.Combine("game.exe");
        File.WriteAllText(file, "x");

        ExecutableBits.EnsureGame(file);

        Assert.True(ExecutableBits.Has(file));
    }

    public void Dispose() => _temp.Dispose();
}
