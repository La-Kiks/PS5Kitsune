using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Core.Tests.Fakes;

internal sealed class FakeFreeSpace : IFreeSpace
{
    public long? Bytes { get; set; } = 100L << 30;

    public long? FreeBytes(string path) => Bytes;
}
