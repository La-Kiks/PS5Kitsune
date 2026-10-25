using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tests.Games;

namespace PS5Kitsune.Core.Tests.Library;

public sealed class GameLibraryTests : IDisposable
{
    private static readonly AnyPS5InstallRecord Release = new(2, "doom-validated-2026-10", AnyPS5Mode.Release, "v0.1.2", null, null, DateTimeOffset.UnixEpoch);

    private readonly TempDirectory _temp = new();
    private readonly DataLayout _layout;
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 20, 0, 0, TimeSpan.Zero));
    private readonly GameLibrary _library;

    public GameLibraryTests()
    {
        _layout = new DataLayout(_temp.Path);
        _library = new GameLibrary(_layout, new FakePlatform(), _time, NullLogger<GameLibrary>.Instance);
    }

    private Task WriteAsync(string titleId, string name) => GameRecordStore.WriteAsync(
        Path.Combine(_layout.GameDirectory(titleId), ".ps5kitsune", "game.json"), TestRecords.Sample() with { TitleId = titleId, Name = name }, TestContext.Current.CancellationToken);

    private Task WriteSampleAsync() => WriteAsync("PPSA99666", "DOOM");

    [Fact]
    public async Task Finds_a_recorded_game()
    {
        await WriteSampleAsync();

        var (game, record) = _library.Find("PPSA99666");

        Assert.Equal("DOOM", record.Name);
        Assert.Equal(_layout.GameDirectory("PPSA99666"), game.Root);
    }

    [Fact]
    public void An_unknown_title_id_names_the_games_folder()
    {
        var exception = Assert.Throws<GameNotFoundException>(() => _library.Find("PPSA00001"));

        Assert.Contains("PPSA00001", exception.Message, StringComparison.Ordinal);
        Assert.Contains(_layout.Games, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_title_id_with_path_characters_is_not_looked_up()
    {
        await WriteSampleAsync();

        Assert.Throws<GameNotFoundException>(() => _library.Find(Path.Combine("..", "games", "PPSA99666")));
    }

    [Fact]
    public async Task Marking_a_game_played_records_the_time()
    {
        await WriteSampleAsync();
        var game = _library.Layout("PPSA99666");

        var written = await _library.MarkPlayedAsync(game, TestContext.Current.CancellationToken);

        Assert.Equal(_time.GetLocalNow(), written.LastPlayedAt);
        Assert.Equal(_time.GetLocalNow(), _library.Find("PPSA99666").Record.LastPlayedAt);
    }

    [Fact]
    public async Task Marking_a_game_played_keeps_what_was_written_since_it_was_found()
    {
        await WriteSampleAsync();
        var (game, record) = _library.Find("PPSA99666");
        await GameRecordStore.WriteAsync(game.RecordFile, record with { Settings = new GameSettings { Language = 5 } }, TestContext.Current.CancellationToken);

        await _library.MarkPlayedAsync(game, TestContext.Current.CancellationToken);

        Assert.Equal(5, _library.Find("PPSA99666").Record.Settings.Language);
    }

    [Fact]
    public async Task Reading_a_held_game_restores_an_interrupted_reconvert()
    {
        var game = new GameLayout(_layout.GameDirectory("PPSA99666"), "PPSA99666", new FakePlatform());
        ConvertedGames.Create(game);
        await WriteSampleAsync();
        var before = ConvertedGames.Snapshot(game.Root);
        ConversionBackup.SetAside(game);
        TestFiles.Write(game.Executable, "half-built exe");

        GameRecord record;
        using (GameLock.TryAcquireNow(game))
        {
            record = _library.ReadHeld(game);
        }

        Assert.Equal("DOOM", record.Name);
        Assert.Equal(before, ConvertedGames.Snapshot(game.Root));
    }

    [Fact]
    public async Task Has_games_once_a_game_json_exists()
    {
        Assert.False(_library.HasGames());
        Directory.CreateDirectory(_layout.GameDirectory("PPSA00004"));
        Assert.False(_library.HasGames());

        await WriteSampleAsync();

        Assert.True(_library.HasGames());
    }

    [Fact]
    public async Task Finding_a_game_restores_an_interrupted_reconvert()
    {
        var game = new GameLayout(_layout.GameDirectory("PPSA99666"), "PPSA99666", new FakePlatform());
        ConvertedGames.Create(game);
        await WriteSampleAsync();
        var before = ConvertedGames.Snapshot(game.Root);
        ConversionBackup.SetAside(game);
        TestFiles.Write(game.Executable, "half-built exe");

        _library.Find("PPSA99666");

        Assert.Equal(before, ConvertedGames.Snapshot(game.Root));
    }

    [Fact]
    public async Task Finding_a_game_with_nothing_set_aside_opens_no_lock()
    {
        await WriteSampleAsync();
        var game = _library.Find("PPSA99666").Game;

        Assert.False(File.Exists(game.Lock));
    }

    [Fact]
    public async Task A_restore_by_a_reader_leaves_no_lock_behind()
    {
        var game = new GameLayout(_layout.GameDirectory("PPSA99666"), "PPSA99666", new FakePlatform());
        ConvertedGames.Create(game);
        await WriteSampleAsync();
        ConversionBackup.SetAside(game);

        _library.Find("PPSA99666");

        Assert.False(Directory.Exists(game.Previous));
        Assert.False(File.Exists(game.Lock));
        Assert.False(GameLock.IsHeld(game));
    }

    [Fact]
    public async Task Finding_a_game_does_not_restore_while_it_is_reconverted()
    {
        var game = new GameLayout(_layout.GameDirectory("PPSA99666"), "PPSA99666", new FakePlatform());
        ConvertedGames.Create(game);
        await WriteSampleAsync();
        var before = ConvertedGames.Snapshot(game.Root);
        ConversionBackup.SetAside(game);
        TestFiles.Write(game.Executable, "half-built exe");

        using (GameLock.TryAcquireNow(game))
        {
            Assert.Equal("DOOM", _library.Find("PPSA99666").Record.Name);
            Assert.True(Directory.Exists(game.Previous));
        }

        _library.Find("PPSA99666");

        Assert.False(Directory.Exists(game.Previous));
        Assert.Equal(before, ConvertedGames.Snapshot(game.Root));
    }

    [Fact]
    public async Task Listing_the_games_does_not_restore_while_one_is_reconverted()
    {
        var game = new GameLayout(_layout.GameDirectory("PPSA99666"), "PPSA99666", new FakePlatform());
        ConvertedGames.Create(game);
        await WriteSampleAsync();
        var before = ConvertedGames.Snapshot(game.Root);
        ConversionBackup.SetAside(game);
        TestFiles.Write(game.Executable, "half-built exe");

        using (GameLock.TryAcquireNow(game))
        {
            Assert.Equal(["DOOM"], _library.All().Select(entry => entry.Record.Name));
            Assert.True(Directory.Exists(game.Previous));
        }

        _library.All();

        Assert.False(Directory.Exists(game.Previous));
        Assert.Equal(before, ConvertedGames.Snapshot(game.Root));
    }

    [Fact]
    public async Task Lists_the_games_by_name_and_skips_a_malformed_record()
    {
        await WriteAsync("PPSA00002", "Zeta");
        await WriteAsync("PPSA00001", "Alpha");
        TestFiles.Write(Path.Combine(_layout.GameDirectory("PPSA00003"), ".ps5kitsune", "game.json"), "{ \"titleId\": ");
        Directory.CreateDirectory(_layout.GameDirectory("PPSA00004"));

        var games = _library.All();

        Assert.Equal(["Alpha", "Zeta"], games.Select(entry => entry.Record.Name));
        Assert.Equal(_layout.GameDirectory("PPSA00001"), games[0].Game.Root);
        Assert.False(Directory.Exists(Path.Combine(_layout.GameDirectory("PPSA00004"), ".ps5kitsune")));
    }

    [Fact]
    public void No_games_folder_lists_nothing()
    {
        Assert.Empty(_library.All());
    }

    [Fact]
    public void A_game_converted_with_the_install_in_use_needs_nothing()
    {
        Assert.False(GameLibrary.NeedsReconversion(TestRecords.Sample(), Release));
    }

    [Theory]
    [InlineData("other-profile", AnyPS5Mode.Release, "v0.1.2", null)]
    [InlineData("doom-validated-2026-10", AnyPS5Mode.Build, "v0.1.2", null)]
    [InlineData("doom-validated-2026-10", AnyPS5Mode.Release, "v0.1.3", null)]
    [InlineData("doom-validated-2026-10", AnyPS5Mode.Release, "v0.1.2", "3fd1cc3")]
    public void Any_difference_in_the_install_needs_reconversion(string profile, AnyPS5Mode mode, string version, string? commit)
    {
        var install = Release with { Profile = profile, Mode = mode, Version = version, Commit = commit };

        Assert.True(GameLibrary.NeedsReconversion(TestRecords.Sample(), install));
    }

    [Fact]
    public void Labels_name_the_mode_and_the_version()
    {
        Assert.Equal("release v0.1.2", ConversionLabels.Of(TestRecords.Sample().ConvertedWith));
        Assert.Equal("build 3fd1cc3", ConversionLabels.Of(Release with { Mode = AnyPS5Mode.Build, Version = "3fd1cc3" }));
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task An_invalid_setting_reads_as_its_default_and_the_file_is_kept()
    {
        var path = Path.Combine(_layout.GameDirectory("PPSA99666"), ".ps5kitsune", "game.json");
        await GameRecordStore.WriteAsync(path, TestRecords.Sample() with { Settings = new GameSettings { Language = 99, PresentMode = "vsync" } }, TestContext.Current.CancellationToken);

        var (_, record) = _library.Find("PPSA99666");

        Assert.Equal(new GameSettings(), record.Settings);
        Assert.Equal(new GameSettings(), Assert.Single(_library.All()).Record.Settings);
        Assert.Equal(99, GameRecordStore.Read(path)!.Settings.Language);
    }

    [Fact]
    public async Task A_record_without_the_new_settings_reads_with_their_defaults()
    {
        var path = Path.Combine(_layout.GameDirectory("PPSA99666"), ".ps5kitsune", "game.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, """
            {
              "schemaVersion": 1, "titleId": "PPSA99666", "name": "DOOM", "executable": "PPSA99666.exe",
              "convertedWith": { "profile": "doom-validated-2026-10", "mode": "release", "version": "v0.1.2", "commit": null, "toIntel": false },
              "audit": { "implemented": 262, "stub": 0, "absent": 0, "verdict": "green" },
              "settings": { "gpu": null, "lavapipe": true, "language": 2, "presentMode": "mailbox" },
              "addedAt": "2026-10-18T15:00:00+02:00", "lastPlayedAt": null
            }
            """, TestContext.Current.CancellationToken);

        var (_, record) = _library.Find("PPSA99666");

        Assert.Equal(new GameSettings { Lavapipe = true, Language = 2, PresentMode = "mailbox" }, record.Settings);
    }
}
