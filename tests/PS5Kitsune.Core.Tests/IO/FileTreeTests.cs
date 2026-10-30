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

    [Fact]
    public void Symbolic_links_are_copied_as_real_files()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        var real = _temp.Combine("real");
        var source = _temp.Combine("source");
        var target = _temp.Combine("target");
        TestFiles.Write(Path.Combine(real, "a.txt"), "aaa");
        TestFiles.Write(Path.Combine(real, "sub", "b.txt"), "bb");
        Directory.CreateDirectory(source);
        File.CreateSymbolicLink(Path.Combine(source, "a.txt"), Path.Combine(real, "a.txt"));
        Directory.CreateSymbolicLink(Path.Combine(source, "sub"), Path.Combine(real, "sub"));

        FileTree.Copy(source, target, _ => false, TestContext.Current.CancellationToken);

        Assert.Equal("aaa", File.ReadAllText(Path.Combine(target, "a.txt")));
        Assert.Null(new FileInfo(Path.Combine(target, "a.txt")).LinkTarget);
        Assert.Equal("bb", File.ReadAllText(Path.Combine(target, "sub", "b.txt")));
        Assert.Null(new DirectoryInfo(Path.Combine(target, "sub")).LinkTarget);
    }

    public void Dispose() => _temp.Dispose();
}
