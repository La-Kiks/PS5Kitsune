using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.AnyPS5.Build;

namespace PS5Kitsune.Core.Tests.AnyPS5.Build;

public sealed class CopyBuildStepTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly BuildTestSetup _setup;
    private readonly AnyPS5Paths _current;

    public CopyBuildStepTests()
    {
        _setup = new BuildTestSetup(_temp.Path);
        _current = new AnyPS5Paths(_setup.Layout, _setup.Platform);
        TestFiles.Write(_setup.Paths.BuiltRelinker, "relinker");
        TestFiles.Write(Path.Combine(_setup.Paths.BuiltLibs, "libSceAudioOut.prx"), "prx");
        TestFiles.Write(Path.Combine(_setup.Paths.BuiltLibs, "CMakeFiles", "ignored.txt"), "not a prx");
        TestFiles.Write(Path.Combine(_setup.Paths.Source, "tools", "import_audit.py"), "audit");
        TestFiles.Write(Path.Combine(_setup.Paths.Source, "tools", "unwrap_self.py"), "unwrap");
        TestFiles.Write(Path.Combine(_setup.Paths.Source, "core", "libs", "prx", "libc", "stub.cpp"), "cpp");
        TestFiles.Write(Path.Combine(_setup.Paths.Source, "docs", "BUILD.md"), "not copied");
    }

    private CopyBuildStep Step => new(_setup.Paths, _current, _setup.WinLibs);

    [Fact]
    public async Task Copies_the_relinker_libraries_DLLs_and_scripts()
    {
        Assert.False(Step.IsAlreadyDone());

        await Step.ExecuteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal("relinker", File.ReadAllText(_current.Relinker));
        Assert.True(File.Exists(Path.Combine(_current.Libs, "libSceAudioOut.prx")));
        Assert.True(File.Exists(Path.Combine(_current.Libs, "libstdc++-6.dll")));
        Assert.False(File.Exists(Path.Combine(_current.Libs, "ignored.txt")));
        Assert.True(File.Exists(_current.ImportAudit));
        Assert.True(File.Exists(_current.UnwrapSelf));
        Assert.True(File.Exists(Path.Combine(_current.Scripts, "core", "libs", "prx", "libc", "stub.cpp")));
        Assert.False(Directory.Exists(Path.Combine(_current.Scripts, "docs")));
        Assert.True(Step.IsAlreadyDone());
    }

    [Fact]
    public async Task Stray_files_are_removed_and_extra_files_do_not_undo_the_step()
    {
        TestFiles.Write(Path.Combine(_current.Libs, "libTracyClient.dll"), "from a release");
        await Step.ExecuteAsync(null, TestContext.Current.CancellationToken);
        Assert.False(File.Exists(Path.Combine(_current.Libs, "libTracyClient.dll")));

        TestFiles.Write(Path.Combine(_current.Scripts, "tools", "__pycache__", "x.pyc"), "cache");

        Assert.True(Step.IsAlreadyDone());
    }

    [Fact]
    public async Task A_rebuilt_relinker_is_copied_again()
    {
        await Step.ExecuteAsync(null, TestContext.Current.CancellationToken);

        TestFiles.Write(_setup.Paths.BuiltRelinker, "relinker v2");

        Assert.False(Step.IsAlreadyDone());
    }

    [Fact]
    public async Task A_missing_relinker_is_reported()
    {
        File.Delete(_setup.Paths.BuiltRelinker);

        var exception = await Assert.ThrowsAsync<FileNotFoundException>(() => Step.ExecuteAsync(null, TestContext.Current.CancellationToken));

        Assert.Contains("relinker", exception.Message, StringComparison.Ordinal);
    }

    public void Dispose() => _temp.Dispose();
}
