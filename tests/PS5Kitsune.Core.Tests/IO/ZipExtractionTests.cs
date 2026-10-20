using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Core.Tests.IO;

public sealed class ZipExtractionTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    private string Target => _temp.Combine("target");

    private string WriteZip(params (string Name, byte[] Content)[] entries)
    {
        var path = _temp.Combine("archive.zip");
        File.WriteAllBytes(path, TestFiles.ZipBytes(entries));
        return path;
    }

    [Fact]
    public async Task Extracts_only_the_entries_under_a_prefix()
    {
        var zip = WriteZip(("libs/a.prx", "a"u8.ToArray()), ("libs/sub/b.dll", "b"u8.ToArray()), ("README.md", "r"u8.ToArray()));

        await ZipExtraction.ExtractAsync(zip, Target, "abc", ZipExtraction.UnderPrefix("libs/"), TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(Target, "a.prx")));
        Assert.True(File.Exists(Path.Combine(Target, "sub", "b.dll")));
        Assert.False(File.Exists(Path.Combine(Target, "README.md")));
        Assert.True(ZipExtraction.IsExtracted(Target, "abc"));
        Assert.False(ZipExtraction.IsExtracted(Target, "def"));
        Assert.False(Directory.Exists(Target + ".staging"));
    }

    [Fact]
    public async Task Strips_the_top_folder_and_keeps_the_included_trees()
    {
        var zip = WriteZip(
            ("AnyPS5-0.1.2/tools/import_audit.py", "t"u8.ToArray()),
            ("AnyPS5-0.1.2/core/libs/prx/libc/stub.cpp", "c"u8.ToArray()),
            ("AnyPS5-0.1.2/core/other.txt", "o"u8.ToArray()),
            ("AnyPS5-0.1.2/README.md", "r"u8.ToArray()));

        await ZipExtraction.ExtractAsync(zip, Target, "abc", ZipExtraction.StripTopFolder(["tools/", "core/libs/prx/"]), TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(Target, "tools", "import_audit.py")));
        Assert.True(File.Exists(Path.Combine(Target, "core", "libs", "prx", "libc", "stub.cpp")));
        Assert.False(File.Exists(Path.Combine(Target, "core", "other.txt")));
        Assert.False(File.Exists(Path.Combine(Target, "README.md")));
    }

    [Fact]
    public async Task Replaces_a_previous_extraction()
    {
        TestFiles.Write(Path.Combine(Target, "old.txt"), "old");
        var zip = WriteZip(("new.txt", "n"u8.ToArray()));

        await ZipExtraction.ExtractAsync(zip, Target, "abc", ZipExtraction.Everything, TestContext.Current.CancellationToken);

        Assert.False(File.Exists(Path.Combine(Target, "old.txt")));
        Assert.True(File.Exists(Path.Combine(Target, "new.txt")));
    }

    [Fact]
    public async Task Without_a_hash_no_marker_is_written()
    {
        var zip = WriteZip(("eboot.bin", "e"u8.ToArray()));

        await ZipExtraction.ExtractAsync(zip, Target, archiveSha256: null, ZipExtraction.Everything, TestContext.Current.CancellationToken);

        Assert.Equal(["eboot.bin"], Directory.GetFiles(Target).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Rejects_an_entry_that_escapes_the_target()
    {
        var zip = WriteZip(("../evil.txt", "e"u8.ToArray()));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            ZipExtraction.ExtractAsync(zip, Target, "abc", ZipExtraction.Everything, TestContext.Current.CancellationToken));

        Assert.False(File.Exists(_temp.Combine("evil.txt")));
    }

    public void Dispose() => _temp.Dispose();
}
