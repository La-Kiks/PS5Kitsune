using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Core.Tests.IO;

public sealed class FileTreeTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void Copies_the_files_not_yet_copied()
    {
        var source = _temp.Combine("source");
        var target = _temp.Combine("target");
        TestFiles.Write(Path.Combine(source, "a.txt"), "aaa");
        TestFiles.Write(Path.Combine(source, "sub", "b.txt"), "bb");
        TestFiles.Write(Path.Combine(target, "a.txt"), "old");

        FileTree.Copy(source, target, relative => relative == "a.txt", TestContext.Current.CancellationToken);

        Assert.Equal("old", File.ReadAllText(Path.Combine(target, "a.txt")));
        Assert.Equal("bb", File.ReadAllText(Path.Combine(target, "sub", "b.txt")));
        Assert.Equal(5, FileTree.Size(source));
        Assert.Equal(["a.txt", Path.Combine("sub", "b.txt")], FileTree.RelativeFiles(source).Order(StringComparer.Ordinal));
        Assert.True(FileTree.SameSize(Path.Combine(source, "a.txt"), Path.Combine(target, "a.txt")));
        Assert.False(FileTree.SameSize(Path.Combine(source, "a.txt"), Path.Combine(target, "missing.txt")));
        Assert.Empty(FileTree.RelativeFiles(_temp.Combine("missing")));
    }

    public void Dispose() => _temp.Dispose();
}
