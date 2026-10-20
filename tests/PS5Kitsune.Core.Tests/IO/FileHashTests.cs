using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Core.Tests.IO;

public sealed class FileHashTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public async Task Hashes_a_file_in_lowercase_hex()
    {
        var path = _temp.Combine("file.bin");
        TestFiles.Write(path, "abc");

        // SHA-256 of "abc".
        const string expected = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
        Assert.Equal(expected, FileHash.Sha256(path));
        Assert.Equal(expected, await FileHash.Sha256Async(path, TestContext.Current.CancellationToken));
        Assert.True(FileHash.Matches(path, expected));
        Assert.False(FileHash.Matches(path, new string('0', 64)));
        Assert.False(FileHash.Matches(_temp.Combine("missing.bin"), expected));
    }

    public void Dispose() => _temp.Dispose();
}
