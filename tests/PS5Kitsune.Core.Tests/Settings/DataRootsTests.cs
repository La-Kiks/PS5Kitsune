using PS5Kitsune.Core.Settings;

namespace PS5Kitsune.Core.Tests.Settings;

public sealed class DataRootsTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void The_command_line_root_wins()
    {
        Assert.Equal("cli", DataRoots.Resolve("cli", new AppSettings(1, "saved"), "default"));
    }

    [Fact]
    public void The_saved_root_beats_the_default()
    {
        Assert.Equal("saved", DataRoots.Resolve(null, new AppSettings(1, "saved"), "default"));
    }

    [Fact]
    public void The_default_root_is_the_fallback()
    {
        Assert.Equal("default", DataRoots.Resolve(null, AppSettings.Default, "default"));
    }

    [Fact]
    public void Prepare_creates_a_writable_folder()
    {
        var root = _temp.Combine("new", "root");

        Assert.Null(DataRoots.Prepare(root));
        Assert.True(Directory.Exists(root));
        Assert.Empty(Directory.EnumerateFileSystemEntries(root));
    }

    [Fact]
    public void Prepare_reports_a_folder_that_cannot_be_created()
    {
        var file = _temp.Combine("a-file");
        File.WriteAllText(file, "x");

        var problem = DataRoots.Prepare(Path.Combine(file, "root"));

        Assert.NotNull(problem);
        Assert.StartsWith("Cannot use ", problem, StringComparison.Ordinal);
    }

    public void Dispose() => _temp.Dispose();
}
