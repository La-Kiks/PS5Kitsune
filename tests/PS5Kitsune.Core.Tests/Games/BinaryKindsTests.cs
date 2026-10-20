using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class BinaryKindsTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Theory]
    [InlineData(new byte[] { 0x7F, 0x45, 0x4C, 0x46, 0x02 }, BinaryKind.Elf)]
    [InlineData(new byte[] { 0x4F, 0x15, 0x3D, 0x1D, 0x00 }, BinaryKind.Self)]
    [InlineData(new byte[] { 0x54, 0x14, 0xF5, 0xEE, 0x00 }, BinaryKind.Self)]
    [InlineData(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, BinaryKind.Other)]
    [InlineData(new byte[] { 0x7F, 0x45 }, BinaryKind.Other)]
    public void Detects_the_kind_from_the_first_four_bytes(byte[] content, BinaryKind expected)
    {
        var path = _temp.Combine("file");
        TestFiles.Write(path, content);

        Assert.Equal(expected, BinaryKinds.Detect(path));
    }

    [Fact]
    public void A_missing_file_is_missing()
    {
        Assert.Equal(BinaryKind.Missing, BinaryKinds.Detect(_temp.Combine("missing")));
    }

    public void Dispose() => _temp.Dispose();
}
