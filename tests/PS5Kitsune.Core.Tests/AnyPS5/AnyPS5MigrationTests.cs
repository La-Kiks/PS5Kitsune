using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Core.Tests.AnyPS5;

public sealed class AnyPS5MigrationTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly AnyPS5TestSetup _setup;

    public AnyPS5MigrationTests()
    {
        _setup = new AnyPS5TestSetup(_temp.Path);
    }

    private string Current => _temp.Combine("anyps5", "current");

    private async Task WriteCurrentAsync(AnyPS5Mode? recordMode)
    {
        TestFiles.Write(Path.Combine(Current, "relinker.exe"), "relinker");
        if (recordMode is { } mode)
        {
            var record = new AnyPS5InstallRecord(2, "test-profile", mode, "v0.1.2", null, null, _setup.Time.GetLocalNow());
            await JsonFiles.WriteAsync(Path.Combine(Current, "install.json"), record, TestContext.Current.CancellationToken);
        }
    }

    [Theory]
    [InlineData(AnyPS5Mode.Release)]
    [InlineData(AnyPS5Mode.Build)]
    public async Task Current_moves_to_the_folder_of_its_mode_and_becomes_active(AnyPS5Mode mode)
    {
        await WriteCurrentAsync(mode);

        _setup.Installer.MigrateLegacyInstall();

        Assert.False(Directory.Exists(Current));
        Assert.True(File.Exists(_setup.Paths.For(mode).Relinker));
        Assert.Equal(mode, _setup.Installer.ActiveMode);
        Assert.Equal(mode, _setup.Installer.ReadRecord()?.Mode);
    }

    [Fact]
    public async Task Without_a_record_the_requested_mode_decides()
    {
        await WriteCurrentAsync(null);
        TestFiles.Write(_setup.Paths.RequestedModeFile, "build");

        _setup.Installer.MigrateLegacyInstall();

        Assert.True(File.Exists(_setup.Paths.For(AnyPS5Mode.Build).Relinker));
        Assert.Equal(AnyPS5Mode.Build, _setup.Installer.ActiveMode);
    }

    [Fact]
    public async Task Without_a_record_nor_a_requested_mode_it_is_a_release()
    {
        await WriteCurrentAsync(null);

        _setup.Installer.MigrateLegacyInstall();

        Assert.True(File.Exists(_setup.Paths.For(AnyPS5Mode.Release).Relinker));
        Assert.Equal(AnyPS5Mode.Release, _setup.Installer.ActiveMode);
    }

    [Fact]
    public void Nothing_happens_without_current()
    {
        _setup.Installer.MigrateLegacyInstall();

        Assert.False(Directory.Exists(_temp.Combine("anyps5", "installs")));
        Assert.Null(_setup.Installer.ActiveMode);
    }

    [Fact]
    public async Task Nothing_moves_when_the_target_exists()
    {
        await WriteCurrentAsync(AnyPS5Mode.Release);
        TestFiles.Write(Path.Combine(_setup.Paths.For(AnyPS5Mode.Release).RootDirectory, "kept.txt"), "already there");

        _setup.Installer.MigrateLegacyInstall();

        Assert.True(Directory.Exists(Current));
        Assert.True(File.Exists(Path.Combine(_setup.Paths.For(AnyPS5Mode.Release).RootDirectory, "kept.txt")));
        Assert.Null(_setup.Installer.ActiveMode);
    }

    [Fact]
    public async Task An_existing_active_file_is_kept()
    {
        await WriteCurrentAsync(AnyPS5Mode.Build);
        _setup.Paths.WriteActiveMode(AnyPS5Mode.Release);

        _setup.Installer.MigrateLegacyInstall();

        Assert.True(File.Exists(_setup.Paths.For(AnyPS5Mode.Build).Relinker));
        Assert.Equal(AnyPS5Mode.Release, _setup.Installer.ActiveMode);
    }

    [Fact]
    public async Task A_locked_file_stops_the_move_and_leaves_current_in_place()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Only Windows refuses to move a folder holding an open file.");
        }

        await WriteCurrentAsync(AnyPS5Mode.Release);
        using (new FileStream(Path.Combine(Current, "relinker.exe"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var exception = Assert.Throws<AnyPS5MigrationException>(() => _setup.Installer.MigrateLegacyInstall());

            Assert.StartsWith(@"Cannot move anyps5\current: ", exception.Message, StringComparison.Ordinal);
            Assert.EndsWith(". Close any game using it and retry.", exception.Message, StringComparison.Ordinal);
        }

        Assert.True(File.Exists(Path.Combine(Current, "relinker.exe")));
        Assert.Null(_setup.Installer.ActiveMode);
    }

    public void Dispose() => _temp.Dispose();
}
