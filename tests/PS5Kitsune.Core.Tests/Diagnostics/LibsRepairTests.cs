using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Diagnostics;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Tests.AnyPS5;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tests.Games;
using PS5Kitsune.Core.Tests.Library;

namespace PS5Kitsune.Core.Tests.Diagnostics;

public sealed class LibsRepairTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly AnyPS5TestSetup _setup;
    private readonly FakeGameLauncher _launcher = new();
    private readonly GameLibrary _library;
    private readonly RunningGames _running;
    private readonly LibsRepair _repair;

    public LibsRepairTests()
    {
        _setup = new AnyPS5TestSetup(_temp.Combine("root"));
        _library = new GameLibrary(_setup.Layout, _setup.Platform, _setup.Time, NullLogger<GameLibrary>.Instance);
        _running = new RunningGames(_launcher, NullLogger<RunningGames>.Instance);
        _repair = new LibsRepair(_library, _setup.Installer, _setup.Paths, _running);
    }

    private GameLayout Game => new(_setup.Layout.GameDirectory(TestGames.TitleId), TestGames.TitleId, _setup.Platform);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // DOOM converted by the release install, with a libs folder that lost a DLL and gained a stray PS5 module.
    private async Task AddDoomAsync(string? version = null, string mode = "release")
    {
        await _setup.InstallAsync();
        ConvertedGames.Create(Game);
        var install = _setup.Installer.ReadRecord(AnyPS5Mode.Release)!;
        await GameRecordStore.WriteAsync(
            Game.RecordFile,
            TestRecords.Sample() with { ConvertedWith = new ConversionInfo(install.Profile, mode, version ?? install.Version, null, false) },
            Token);
        TestFiles.Write(Path.Combine(Game.Libs, "libSceAudioOut.prx"), "prx");
        TestFiles.Write(Path.Combine(Game.Libs, "libSceNpManager.sprx"), "sony module");
    }

    [Fact]
    public async Task Repair_copies_the_libs_again_from_the_install_that_converted_the_game()
    {
        await AddDoomAsync();

        await _repair.RepairAsync(TestGames.TitleId, Token);

        var source = _setup.Paths.For(AnyPS5Mode.Release).Libs;
        Assert.Equal(
            FileTree.RelativeFiles(source).Where(relative => relative != ZipExtraction.MarkerFileName).Order(StringComparer.Ordinal),
            FileTree.RelativeFiles(Game.Libs).Order(StringComparer.Ordinal));
        Assert.False(GameLock.IsHeld(Game));
    }

    [Fact]
    public async Task Repair_refuses_a_running_game()
    {
        await AddDoomAsync();
        var process = _launcher.Launch(new GameLaunchRequest(Game.Executable, Game.Root, Game.Logs));
        await _running.RecordAsync(Game, process, Game.Executable);

        await Assert.ThrowsAsync<GameRunningException>(() => _repair.RepairAsync(TestGames.TitleId, Token));
        Assert.True(File.Exists(Path.Combine(Game.Libs, "libSceNpManager.sprx")));
    }

    [Fact]
    public async Task Repair_refuses_a_busy_game()
    {
        await AddDoomAsync();
        using var held = GameLock.TryAcquire(Game);

        await Assert.ThrowsAsync<GameBusyException>(() => _repair.RepairAsync(TestGames.TitleId, Token));
    }

    [Fact]
    public async Task Repair_refuses_when_the_install_has_another_version()
    {
        await AddDoomAsync(version: "v0.0.9");

        var exception = await Assert.ThrowsAsync<LibsSourceChangedException>(() => _repair.RepairAsync(TestGames.TitleId, Token));

        Assert.Equal("The release install has changed since DOOM was converted. Reconvert it instead.", exception.Message);
        Assert.True(File.Exists(Path.Combine(Game.Libs, "libSceNpManager.sprx")));
    }

    [Fact]
    public async Task Repair_refuses_when_the_install_is_missing()
    {
        await AddDoomAsync(mode: "build");

        await Assert.ThrowsAsync<LibsSourceChangedException>(() => _repair.RepairAsync(TestGames.TitleId, Token));
    }

    public void Dispose() => _temp.Dispose();
}
