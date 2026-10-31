using PS5Kitsune.Core.Diagnostics;

namespace PS5Kitsune.Core.Tests.Diagnostics;

public sealed class ExitCodesTests
{
    [Theory]
    [InlineData(0u, "0x00000000")]
    [InlineData(0xC0000005u, "0xC0000005")]
    public void Windows_codes_are_hexadecimal(uint code, string text) => Assert.Equal(text, ExitCodes.Describe(code, linux: false));

    [Theory]
    [InlineData(0u, "0")]
    [InlineData(1u, "1")]
    [InlineData(139u, "139 (signal 11, SIGSEGV)")]
    [InlineData(137u, "137 (signal 9, SIGKILL)")]
    [InlineData(143u, "143 (signal 15, SIGTERM)")]
    [InlineData(162u, "162 (signal 34)")]
    [InlineData(200u, "200")]
    [InlineData(4294967295u, "4294967295")]
    public void Linux_codes_are_decimal_with_the_signal(uint code, string text) => Assert.Equal(text, ExitCodes.Describe(code, linux: true));
}
