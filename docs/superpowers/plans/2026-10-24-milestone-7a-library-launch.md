# Milestone 7a — Library and launch Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show the added games in a Library page and launch them in one click, with the log followed live, Stop and the exit code; closing the app never stops a game and reopening it finds the game again. The CLI gets `list` and `remove`, and `run` shares the same rules.

**Architecture:** `ReconvertLock` becomes `GameLock` (`.ps5kitsune\game.lock`), held by every action that changes the game folder and retried briefly. `IGameLauncher` gains `Attach` and `ILaunchedGame` gains `StartedAt` and `StopAsync`; `RunningGames` records a launched game in `.ps5kitsune\running.json` and checks it against the live process. `GamePlayer` (launch, list) and `GameCleaner` (remove, clear shader cache) are the entry points for the CLI and the app; `GameReconverter` refuses a running game. A Windows job object ties the tools PS5Kitsune starts to its lifetime. The app gets a Game page and a Library page (the start page once a game is added).

**Tech Stack:** .NET 10, C#, Avalonia 12 (compiled bindings), CommunityToolkit.Mvvm, xUnit v3 on Microsoft.Testing.Platform, P/Invoke through `LibraryImport`.

**Spec:** `docs/superpowers/specs/2026-10-24-milestone-7a-library-launch-design.md` (revision 5408f1b).

## Global Constraints

- Code, docs, commit messages and UI text in English.
- `TreatWarningsAsErrors`, `AnalysisLevel latest-recommended`, `EnforceCodeStyleInBuild`: 0 warnings. `[LoggerMessage]` for logging (CA1848); InvariantCulture when formatting numbers and dates (CA1305); no `ToLowerInvariant` for display (CA1308: use a switch); constant texts on view models are get-only properties with an initializer (CA1822); compute log arguments into locals before the call (CA1873).
- No new NuGet dependency.
- Files: `.ps5kitsune\game.lock` (the game lock), `.ps5kitsune\running.json` (the running game), `shader_cache\`, `data\` (saves). `running.json` holds `schemaVersion` (1), `processId`, `startedAt` (process creation time), `executable`, `logFile`.
- The game lock is retried 5 times, 50 ms apart, by actions (launch, Reconvert, Remove, Clear shader cache); readers (`Find`, `All`) try once.
- The game process is never assigned to the job object; tools started through `IProcessRunner` always are.
- Core texts, verbatim:
  - `GameBusyException`: "Another PS5Kitsune action is using {titleId}. Try again when it finishes."
  - `GameRunningException`: "{name} is already running (PID {processId}). Log: {logFile}."
  - `GameExecutableMissingException`: "{executable} is missing; an antivirus may have deleted it. Add an exclusion for the games folder, then reconvert the game."
- CLI texts, verbatim:
  - `list`: one line per game, fields joined by two spaces: title ID, name, `green`/`orange`/`red`, then `reconvert` when it needs one, `running (PID {n})` when running, and `played yyyy-MM-dd HH:mm` (local time) or `never played`. No game: "No games. Add one with: ps5kitsune add <folder>".
  - `remove`: prompt "Delete {name} and its folder? [y/N] ", with " Its saves in data\ are deleted too." before " [y/N] " when it has saves; refusal "Nothing was removed." (exit 1); success "{Name} ({TitleId}) is removed: {Directory} is deleted."
  - Exit codes: 0 success, 1 failure or refusal, 130 cancelled.
- App texts, verbatim:
  - Library: title "Library", search watermark "Search by name", buttons "Add a game", "Home", empty state "No games yet"; menu "Launch", "Open folder", "View last log", "Reconvert", "Clear shader cache", "Remove"; badges "Green", "Orange", "Red", "Needs reconvert", "Running".
  - Library and Game page errors: "{name} is already running.", "Another PS5Kitsune action is using {name}. Try again when it finishes.", "The executable is missing: Reconvert {name}.", "AnyPS5 is not installed. Install it from Home first.", "Could not launch {name}: {message}", "Close {name} first.", "{name} is no longer running.", "{name} has no shader cache.", "The shader cache of {name} may be partly cleared: {message}", "Could not remove {name} completely: {message}", "Cannot read the library: {message}".
  - Switch dialog: title "A game is running", message "{other} is running. Show it?", button "Show it".
  - Clear shader cache dialog: title "Clear the shader cache?", message "Delete the shader cache of {name} ({size})? The next launch will stutter while shaders are prepared again.", button "Delete". Size: "{x:0.0} GB" from 1 GiB, else "{x:0.0} MB".
  - Remove dialog: title "Remove {name}?", message "Delete {name} and its folder?" plus " Its saves in data\ are deleted too." when it has saves, button "Delete".
  - Game page: "Running (PID {n})", "Exited with code {n}", "The first launch may stutter while shaders are prepared.", "Could not stop {name}: {message}", "Cannot follow the log: {message}"; buttons "← Library", "Open folder", "Stop", "Launch again".
  - Home: button "Library". Result page: button "Go to library" (was "Back to home").
- Colours: red `#C42B1C`, orange `#9D5D00`, green `#0F7B0F`, blue `#005FB8` (Running badge).
- Tests never use the network, the compiler or Python. Windows-only tests start with `Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.")` (Platform tests) or `if (!OperatingSystem.IsWindows()) { Assert.Skip("..."); }` (Core tests).
- Build: `dotnet build PS5Kitsune.slnx -c Release`. Tests: `dotnet test --solution PS5Kitsune.slnx -c Release`; one class: `dotnet test --project <test project> -c Release -- --filter-class "<full class name>"` (a filtered run exits with code 8 when it selects nothing: check the summary). Run commands through the Bash tool. The test output is in French ("échec" = failed, "réussi" = passed, "ignoré" = skipped).
- Close any running `PS5Kitsune.exe` before building the App (it locks `bin\Release`).
- Line endings: keep each edited file's existing endings (most C# files are CRLF; never mix LF lines into a CRLF file); new files may be LF.
- Commit locally after each task, author La-Kiks, message ending with `Co-Authored-By: <your model name> <noreply@anthropic.com>`. Do not push. Never add or run an `install.cmd`.

## Refinements of the spec

1. **`RunningGames`** (Core/Library) owns `running.json`: `Attach`, `Check`, `RecordAsync`, `Forget`. `GamePlayer`, `GameReconverter` and `GameCleaner` use it. `GameLibrary.All()` keeps its shape; `GamePlayer.List()` returns `LibraryGame(GameLayout Game, GameRecord Record, RunningGame? Running)`.
2. **`GameRemover` and `ShaderCache` become one `GameCleaner`** (Core/Library): `Remove(titleId)`, `ClearShaderCache(titleId)`, and the statics `ShaderCacheSize(GameLayout)` and `HasSaves(GameLayout)` (not on `GameLibrary`). They are synchronous; the app calls them through `Task.Run`.
3. **`GameLock.TryAcquire` retries; `GameLock.TryAcquireNow` tries once.** Readers use `TryAcquireNow`, so a running Reconvert does not slow every read of the library by 250 ms.
4. **`GameLibrary.ReadHeld(GameLayout)`** reads a game whose lock the caller holds: it restores an interrupted Reconvert, then reads `game.json`. `Find` would skip the restore there (the lock is the caller's own). `GameReconverter` uses it instead of `Find`.
5. **`GameLibrary.MarkPlayedAsync(GameLayout, CancellationToken)`** re-reads `game.json` and returns the record it wrote.
6. **Core registers fallbacks with `TryAdd`:** `UnsupportedGameLauncher` (`Launch` throws `PlatformNotSupportedException`, `Attach` returns null) and `NoChildProcessJob`. `AddPlatform` runs before `AddPS5KitsuneCore`, so Windows keeps its implementations and every service resolves on Linux CI.
7. **The CLI lets `GameRunningException`, `GameBusyException` and `GameExecutableMissingException` reach `CommandHost`**, which prints the message and exits with 1; their messages are the CLI texts. `run` requires an install in use (`ReadRecord()` not null) rather than running the done-checks.
8. **`IPages.Game(PlayingGame playing, bool firstLaunch)`**; `PlayingGame(GameLayout Game, GameRecord Record, ILaunchedGame Process)`. The Game page owns the process handle and disposes it when left. "Launch again" relaunches from the page.
9. **Task order:** the Game page (Task 6) comes before the Library (Task 7); until Task 7, `PageFactory.Library()` returns Home.
10. **The Result page's "Back to home" becomes "Go to library"**, and "Add another game" returns over the Library; the Reconverting page's cancel and "Back to home" go to the Library.
11. **`remove` without confirmation** prints "Nothing was removed." and exits with 1.
12. **`WindowsGameLauncher.StopGrace`** (internal, 5 s by default) lets tests stop a game without waiting 5 s.
13. **`GameView` unsubscribes from the log's `CollectionChanged`** when its data context changes (the 6b view follow-up, for the new view only).
14. **A game with no shader cache** shows "{name} has no shader cache." and no dialog.

## File map

| File | Change |
| --- | --- |
| `src/PS5Kitsune.Core/Games/ReconvertLock.cs` → `GameLock.cs` | rename, retry, `TryAcquireNow` |
| `src/PS5Kitsune.Core/Games/ReconvertInProgressException.cs` → `GameBusyException.cs` | rename, new text |
| `src/PS5Kitsune.Core/Games/GameLayout.cs` | `Lock`, `RunningRecord`, `ShaderCache`, `Data` |
| `src/PS5Kitsune.Core/Library/GameLibrary.cs` | `TryAcquireNow`, `ReadHeld`, `HasGames`, `MarkPlayedAsync` re-reads |
| `src/PS5Kitsune.Core/Games/Steps/UpdateGameRecordStep.cs` | re-reads `game.json` |
| `src/PS5Kitsune.Core/Games/GameReconverter.cs`, `IGameReconverter.cs` | `GameLock`, `ReadHeld`, refuses a running game |
| `src/PS5Kitsune.Core/Abstractions/IGameLauncher.cs` | `RunningGame`, `Attach`, `StartedAt`, `StopAsync` |
| `src/PS5Kitsune.Core/Abstractions/IChildProcessJob.cs` | new |
| `src/PS5Kitsune.Core/Library/RunningGames.cs`, `GamePlayer.cs`, `GameCleaner.cs`, `LibraryGame.cs`, `PlayingGame.cs`, `GameRunningException.cs`, `GameExecutableMissingException.cs`, `UnsupportedGameLauncher.cs` | new |
| `src/PS5Kitsune.Core/Processes/CliWrapProcessRunner.cs`, `NoChildProcessJob.cs` | job assignment, new fallback |
| `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs` | new services and fallbacks |
| `src/PS5Kitsune.Platform.Windows/WindowsGameLauncher.cs`, `WindowsChildProcessJob.cs`, `PlatformServices.cs` | attach, stop, job |
| `src/PS5Kitsune.Cli/RunCommand.cs`, `ListCommand.cs`, `RemoveCommand.cs`, `CliLog.cs`, `Program.cs` | `GamePlayer`, new commands |
| `src/PS5Kitsune.App/ViewModels/GameViewModel.cs`, `LaunchErrors.cs`, `Views/GameView.axaml(.cs)` | new (Task 6) |
| `src/PS5Kitsune.App/ViewModels/LibraryViewModel.cs`, `GameTileViewModel.cs`, `Views/LibraryView.axaml(.cs)` | new (Task 7) |
| `src/PS5Kitsune.App/Navigation/IPages.cs`, `Shell/PageFactory.cs`, `Views/MainWindow.axaml`, `App.axaml.cs` | pages, start page |
| `src/PS5Kitsune.App/ViewModels/HomeViewModel.cs`, `Views/HomeView.axaml`, `GameToReconvert.cs` (deleted) | Library button |
| `src/PS5Kitsune.App/ViewModels/ReconvertViewModel.cs`, `AddResultViewModel.cs`, `Views/AddResultView.axaml` | back to the Library |
| `tests/PS5Kitsune.Core.Tests/Fakes/FakeGameLauncher.cs` | new, linked into the App tests |
| `tests/PS5Kitsune.App.Tests/Fakes.cs`, `PS5Kitsune.App.Tests.csproj` | `TestLibrary`, `FakePages`, link |
| `docs/architecture.md`, `README.md` | Task 8 |

---

### Task 1: Core — the game lock and fresh records

**Files:**
- Rename: `src/PS5Kitsune.Core/Games/ReconvertLock.cs` → `src/PS5Kitsune.Core/Games/GameLock.cs`
- Rename: `src/PS5Kitsune.Core/Games/ReconvertInProgressException.cs` → `src/PS5Kitsune.Core/Games/GameBusyException.cs`
- Modify: `src/PS5Kitsune.Core/Games/GameLayout.cs`, `src/PS5Kitsune.Core/Library/GameLibrary.cs`, `src/PS5Kitsune.Core/Games/Steps/UpdateGameRecordStep.cs`, `src/PS5Kitsune.Core/Games/GameReconverter.cs`, `src/PS5Kitsune.Core/Games/IGameReconverter.cs`, `src/PS5Kitsune.Cli/RunCommand.cs`
- Rename: `tests/PS5Kitsune.Core.Tests/Games/ReconvertLockTests.cs` → `tests/PS5Kitsune.Core.Tests/Games/GameLockTests.cs`
- Modify: `tests/PS5Kitsune.Core.Tests/Library/GameLibraryTests.cs`, `tests/PS5Kitsune.Core.Tests/Games/UpdateGameRecordStepTests.cs`, `tests/PS5Kitsune.Core.Tests/Games/GameReconverterTests.cs`

**Interfaces:**
- Consumes: `ConversionBackup.Restore(GameLayout) → bool`, `GameRecordStore.Read/WriteAsync`, `GameValidator.IsValidTitleId`.
- Produces:
  - `static class GameLock { IDisposable? TryAcquire(GameLayout game); IDisposable? TryAcquireNow(GameLayout game); bool IsHeld(GameLayout game); }`
  - `sealed class GameBusyException(string titleId) : Exception` with `string TitleId`.
  - `GameLayout.Lock` (`.ps5kitsune\game.lock`), `GameLayout.RunningRecord` (`.ps5kitsune\running.json`), `GameLayout.ShaderCache` (`shader_cache`), `GameLayout.Data` (`data`). `GameLayout.ReconvertLock` is removed.
  - `GameRecord GameLibrary.ReadHeld(GameLayout game)`; `bool GameLibrary.HasGames()`; `Task<GameRecord> GameLibrary.MarkPlayedAsync(GameLayout game, CancellationToken cancellationToken)`.

- [ ] **Step 1: Rename the files with git**

```bash
git mv src/PS5Kitsune.Core/Games/ReconvertLock.cs src/PS5Kitsune.Core/Games/GameLock.cs
git mv src/PS5Kitsune.Core/Games/ReconvertInProgressException.cs src/PS5Kitsune.Core/Games/GameBusyException.cs
git mv tests/PS5Kitsune.Core.Tests/Games/ReconvertLockTests.cs tests/PS5Kitsune.Core.Tests/Games/GameLockTests.cs
```

- [ ] **Step 2: Write the failing tests**

Replace `tests/PS5Kitsune.Core.Tests/Games/GameLockTests.cs` with:

```csharp
using System.Diagnostics;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class GameLockTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly GameLayout _game;

    public GameLockTests()
    {
        _game = new GameLayout(_temp.Combine("games", TestGames.TitleId), TestGames.TitleId, new FakePlatform());
        ConvertedGames.Create(_game);
    }

    [Fact]
    public void A_held_lock_cannot_be_acquired_again()
    {
        using var held = GameLock.TryAcquireNow(_game);

        Assert.NotNull(held);
        Assert.Null(GameLock.TryAcquireNow(_game));
        Assert.True(GameLock.IsHeld(_game));
    }

    [Fact]
    public void A_released_lock_is_not_held_and_its_file_is_gone()
    {
        GameLock.TryAcquire(_game)!.Dispose();

        Assert.False(GameLock.IsHeld(_game));
        Assert.False(File.Exists(_game.Lock));
        Assert.EndsWith(Path.Combine(".ps5kitsune", "game.lock"), _game.Lock, StringComparison.Ordinal);
    }

    [Fact]
    public void A_leftover_lock_file_is_not_held()
    {
        TestFiles.Write(_game.Lock, "");

        Assert.False(GameLock.IsHeld(_game));
    }

    [Fact]
    public void The_lock_is_not_a_conversion_output()
    {
        using var held = GameLock.TryAcquire(_game);

        Assert.DoesNotContain(ConversionBackup.Outputs(_game), path => path.Contains("game.lock", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_lock_released_within_the_retry_window_is_acquired()
    {
        var held = GameLock.TryAcquireNow(_game)!;
        var release = Task.Run(async () =>
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
            held.Dispose();
        }, TestContext.Current.CancellationToken);

        using var acquired = GameLock.TryAcquire(_game);
        await release;

        Assert.NotNull(acquired);
    }

    [Fact]
    public void A_lock_held_through_the_retry_window_is_not_acquired()
    {
        using var held = GameLock.TryAcquireNow(_game);
        var stopwatch = Stopwatch.StartNew();

        Assert.Null(GameLock.TryAcquire(_game));
        Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(150), $"Gave up after {stopwatch.Elapsed}.");
    }

    public void Dispose() => _temp.Dispose();
}
```

In `tests/PS5Kitsune.Core.Tests/Library/GameLibraryTests.cs`:
- replace every `ReconvertLock.TryAcquire(` with `GameLock.TryAcquireNow(`, every `ReconvertLock.IsHeld(` with `GameLock.IsHeld(`, and every `game.ReconvertLock` with `game.Lock`;
- replace the test `Marking_a_game_played_records_the_time` with:

```csharp
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
```

In `tests/PS5Kitsune.Core.Tests/Games/UpdateGameRecordStepTests.cs`, add inside the class:

```csharp
    [Fact]
    public async Task Keeps_the_last_played_time_and_settings_written_during_the_reconvert()
    {
        ConvertedGames.Create(_game);
        ConversionBackup.SetAside(_game);
        var previous = TestRecords.Sample();
        var playedDuringTheRun = new DateTimeOffset(2026, 10, 23, 17, 30, 0, TimeSpan.Zero);
        await GameRecordStore.WriteAsync(
            _game.RecordFile, previous with { LastPlayedAt = playedDuringTheRun, Settings = new GameSettings { Language = 3 } }, TestContext.Current.CancellationToken);

        await new UpdateGameRecordStep(_context, previous, Build, _time).ExecuteAsync(null, TestContext.Current.CancellationToken);

        var record = GameRecordStore.Read(_game.RecordFile)!;
        Assert.Equal(playedDuringTheRun, record.LastPlayedAt);
        Assert.Equal(3, record.Settings.Language);
        Assert.Equal(_time.GetLocalNow(), record.ReconvertedAt);
    }
```

In `tests/PS5Kitsune.Core.Tests/Games/GameReconverterTests.cs`: replace every `ReconvertInProgressException` with `GameBusyException`, every `ReconvertLock.TryAcquire(` with `GameLock.TryAcquireNow(`, every `ReconvertLock.IsHeld(` with `GameLock.IsHeld(`, and the expected message `"A Reconvert of PPSA99666 is already in progress."` with `"Another PS5Kitsune action is using PPSA99666. Try again when it finishes."`.

- [ ] **Step 3: Run the Core tests to verify they fail to compile**

Run: `dotnet build tests/PS5Kitsune.Core.Tests -c Release`
Expected: FAIL, errors such as `CS0103: The name 'GameLock' does not exist` and `'GameLayout' does not contain a definition for 'Lock'`.

- [ ] **Step 4: Implement the lock and the exception**

Replace `src/PS5Kitsune.Core/Games/GameLock.cs` with:

```csharp
namespace PS5Kitsune.Core.Games;

/// <summary>
/// .ps5kitsune/game.lock opened with no sharing and deleted on close. Held by every action that changes the game folder or needs it unchanged:
/// a Reconvert (whole run), a reader restoring an interrupted Reconvert, a launch (until running.json is written), Remove and Clear shader cache.
/// The OS releases it when its process dies; a leftover file with no open handle is not held.
/// </summary>
public static class GameLock
{
    private const int Attempts = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(50);

    /// <summary>Takes the lock, trying again for about 250 ms: a short hold elsewhere (a reader's restore, a launch) must not refuse an action.</summary>
    /// <returns>Null when the lock stayed held; dispose the result to release it.</returns>
    public static IDisposable? TryAcquire(GameLayout game)
    {
        for (var attempt = 1; ; attempt++)
        {
            if (TryAcquireNow(game) is { } held)
            {
                return held;
            }

            if (attempt == Attempts)
            {
                return null;
            }

            Thread.Sleep(RetryDelay);
        }
    }

    /// <summary>One attempt, for readers: they skip the restore rather than wait.</summary>
    /// <returns>Null when the lock is held elsewhere; dispose the result to release it.</returns>
    public static IDisposable? TryAcquireNow(GameLayout game)
    {
        ArgumentNullException.ThrowIfNull(game);
        Directory.CreateDirectory(game.Meta);
        try
        {
            return new FileStream(game.Lock, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
        }
        // A handle being closed with delete-on-close can make the open fail with access denied.
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>True while an action holds the lock, in this process or another.</summary>
    public static bool IsHeld(GameLayout game)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (!File.Exists(game.Lock))
        {
            return false;
        }

        using var probe = TryAcquireNow(game);
        return probe is null;
    }
}
```

Replace `src/PS5Kitsune.Core/Games/GameBusyException.cs` with:

```csharp
namespace PS5Kitsune.Core.Games;

/// <summary>Another PS5Kitsune action (a Reconvert, a launch, a removal) holds the game's lock; nothing was changed.</summary>
public sealed class GameBusyException(string titleId) : Exception($"Another PS5Kitsune action is using {titleId}. Try again when it finishes.")
{
    public string TitleId { get; } = titleId;
}
```

In `src/PS5Kitsune.Core/Games/GameLayout.cs`, replace the last property (`ReconvertLock`) with:

```csharp
    /// <summary>Held by any action that changes the game folder; not a conversion output. See <see cref="GameLock"/>.</summary>
    public string Lock => Path.Combine(Meta, "game.lock");

    /// <summary>The game launched by PS5Kitsune that may still run. See <see cref="Library.RunningGames"/>.</summary>
    public string RunningRecord => Path.Combine(Meta, "running.json");

    /// <summary>Created by the game; can reach several GB.</summary>
    public string ShaderCache => Path.Combine(Root, "shader_cache");

    /// <summary>The game's saves, created by the game.</summary>
    public string Data => Path.Combine(Root, "data");
```

(`Library.RunningGames` arrives in Task 3; until then write the `see cref` as plain text: `/// <summary>The game launched by PS5Kitsune that may still run (see RunningGames).</summary>`, and Task 3 turns it into the `see cref`.)

- [ ] **Step 5: Update the library, the step and the reconverter**

In `src/PS5Kitsune.Core/Library/GameLibrary.cs`:

Replace the class summary's last sentence with "a game whose lock is held (a Reconvert or another action is running) is left alone."

Replace `MarkPlayedAsync` with:

```csharp
    /// <summary>Re-reads game.json and writes it with lastPlayedAt set, so a change made since the game was found is kept.</summary>
    /// <returns>The record written.</returns>
    public async Task<GameRecord> MarkPlayedAsync(GameLayout game, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(game);
        var played = (GameRecordStore.Read(game.RecordFile) ?? throw NotFound(game.TitleId)) with { LastPlayedAt = time.GetLocalNow() };
        await GameRecordStore.WriteAsync(game.RecordFile, played, cancellationToken).ConfigureAwait(false);
        return played;
    }

    /// <summary>Reads a game whose lock the caller holds: restores an interrupted Reconvert first (Find would skip it, the lock being held).</summary>
    /// <exception cref="GameNotFoundException">No game.json.</exception>
    /// <exception cref="InvalidGameRecordException">The game.json is malformed.</exception>
    /// <exception cref="IOException">The restore failed; the next read retries it.</exception>
    /// <exception cref="UnauthorizedAccessException">The restore was denied; the next read retries it.</exception>
    public GameRecord ReadHeld(GameLayout game)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (ConversionBackup.Restore(game))
        {
            var root = game.Root;
            LogRestored(root);
        }

        return GameRecordStore.Read(game.RecordFile) ?? throw NotFound(game.TitleId);
    }

    /// <summary>True when at least one game folder has a game.json; reads nothing and restores nothing.</summary>
    public bool HasGames() =>
        Directory.Exists(layout.Games)
        && Directory.EnumerateDirectories(layout.Games).Any(directory =>
            Path.GetFileName(directory) is var titleId
            && GameValidator.IsValidTitleId(titleId)
            && File.Exists(new GameLayout(directory, titleId, platform).RecordFile));
```

In `RestoreInterrupted`, replace `ReconvertLock.TryAcquire(game)` with `GameLock.TryAcquireNow(game)`, and its comment's first sentence with "An action in progress holds the lock (a Reconvert's set-aside conversion is not interrupted), so the game is left alone."

In `src/PS5Kitsune.Core/Games/Steps/UpdateGameRecordStep.cs`, replace `ExecuteAsync` with:

```csharp
    public async Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        var audit = context.Audit ?? throw new InvalidOperationException("The audit must run before the game is recorded.");
        cancellationToken.ThrowIfCancellationRequested();
        // Read before the discard, so a malformed record fails while the old conversion can still be restored.
        // A launch during the run may have written lastPlayedAt, and the settings may have changed.
        var current = GameRecordStore.Read(context.Game.RecordFile) ?? previous;
        ConversionBackup.Discard(context.Game);
        var record = previous with
        {
            ConvertedWith = new ConversionInfo(install.Profile, InstallModes.Name(install.Mode), install.Version, install.Commit, previous.ConvertedWith.ToIntel),
            Audit = new AuditRecord(audit.Implemented, audit.Stub, audit.Absent, audit.Verdict),
            ReconvertedAt = time.GetLocalNow(),
            LastPlayedAt = current.LastPlayedAt,
            Settings = current.Settings,
        };
        // Not cancellable: once the old conversion is discarded, a cancel could no longer restore it.
        await GameRecordStore.WriteAsync(context.Game.RecordFile, record, CancellationToken.None).ConfigureAwait(false);
    }
```

In `src/PS5Kitsune.Core/Games/GameReconverter.cs`:
- replace `using var held = ReconvertLock.TryAcquire(layout) ?? throw new ReconvertInProgressException(titleId);` with `using var held = GameLock.TryAcquire(layout) ?? throw new GameBusyException(titleId);`;
- replace the comment above the restore with `// Find skips the restore while the lock is held, ours included: restore a Reconvert that did not finish here, so this run starts from the last complete conversion.` (unchanged wording, kept);
- replace `var (game, record) = library.Find(titleId);` with:

```csharp
        var game = layout;
        var record = library.ReadHeld(game);
```

In `src/PS5Kitsune.Core/Games/IGameReconverter.cs`: replace `The game's <see cref="ReconvertLock"/> is held for the whole run.` with `The game's <see cref="GameLock"/> is held for the whole run.`, and the `ReconvertInProgressException` line with `/// <exception cref="GameBusyException">Another action holds the game's lock; nothing was changed.</exception>`.

In `src/PS5Kitsune.Cli/RunCommand.cs` (Task 5 rewrites it; this keeps it compiling):
- replace `if (ReconvertLock.IsHeld(game))` with `if (GameLock.IsHeld(game))`;
- replace `await library.MarkPlayedAsync(game, record, cancellationToken);` with `await library.MarkPlayedAsync(game, cancellationToken);`.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet build PS5Kitsune.slnx -c Release` — Expected: 0 warnings, 0 errors.
Run: `dotnet test --project tests/PS5Kitsune.Core.Tests -c Release`
Expected: 0 failed.

- [ ] **Step 7: Commit**

```bash
git add -A src/PS5Kitsune.Core src/PS5Kitsune.Cli/RunCommand.cs tests/PS5Kitsune.Core.Tests
git commit -m "Turn the reconvert lock into a game lock and keep fresh records

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 2: Platform — attach to a running game, stop it

**Files:**
- Modify: `src/PS5Kitsune.Core/Abstractions/IGameLauncher.cs`, `src/PS5Kitsune.Platform.Windows/WindowsGameLauncher.cs`
- Modify: `tests/PS5Kitsune.Platform.Windows.Tests/WindowsGameLauncherTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces:
  - `sealed record RunningGame(int SchemaVersion, int ProcessId, DateTimeOffset StartedAt, string Executable, string LogFile)` with `const int CurrentSchemaVersion = 1` (namespace `PS5Kitsune.Core.Abstractions`).
  - `ILaunchedGame.StartedAt` (`DateTimeOffset`, the process creation time, UTC); `Task ILaunchedGame.StopAsync(CancellationToken cancellationToken = default)`.
  - `ILaunchedGame? IGameLauncher.Attach(RunningGame record)`.
  - `WindowsGameLauncher.StopGrace` (`internal TimeSpan`, init, default 5 s).

- [ ] **Step 1: Write the failing tests**

In `tests/PS5Kitsune.Platform.Windows.Tests/WindowsGameLauncherTests.cs`, replace the `_launcher` field with:

```csharp
    private readonly WindowsGameLauncher _launcher = new(NullLogger<WindowsGameLauncher>.Instance) { StopGrace = TimeSpan.FromMilliseconds(200) };

    private static string Ping => Path.Combine(Environment.SystemDirectory, "PING.EXE");

    /// <summary>A game with no window that lives about 30 s.</summary>
    private ILaunchedGame StartPing() =>
        _launcher.Launch(new GameLaunchRequest(Ping, _temp.Path, Logs) { Arguments = ["-n", "30", "127.0.0.1"] });

    private static RunningGame RecordOf(ILaunchedGame game, string executable) =>
        new(RunningGame.CurrentSchemaVersion, game.ProcessId, game.StartedAt, executable, game.LogFile);
```

and add these tests inside the class:

```csharp
    [Fact]
    public async Task The_start_time_is_the_process_creation_time()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        using var game = StartPing();
        var after = DateTimeOffset.UtcNow.AddSeconds(1);
        await game.StopAsync(TestContext.Current.CancellationToken);

        Assert.InRange(game.StartedAt, before, after);
    }

    [Fact]
    public async Task Attaches_to_a_running_game_stops_it_and_reads_its_exit_code()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        var cancellationToken = TestContext.Current.CancellationToken;
        using var launched = StartPing();

        using var attached = _launcher.Attach(RecordOf(launched, Ping));

        Assert.NotNull(attached);
        Assert.Equal(launched.ProcessId, attached.ProcessId);
        Assert.Equal(launched.StartedAt, attached.StartedAt);
        Assert.Equal(launched.LogFile, attached.LogFile);
        await attached.StopAsync(cancellationToken);
        Assert.Equal(1, await attached.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(10), cancellationToken));
        Assert.Null(_launcher.Attach(RecordOf(launched, Ping)));
    }

    [Fact]
    public async Task Does_not_attach_when_the_start_time_differs()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        using var launched = StartPing();

        var attached = _launcher.Attach(RecordOf(launched, Ping) with { StartedAt = launched.StartedAt.AddSeconds(1) });
        await launched.StopAsync(TestContext.Current.CancellationToken);

        Assert.Null(attached);
    }

    [Fact]
    public async Task Does_not_attach_to_another_executable()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        using var launched = StartPing();

        var attached = _launcher.Attach(RecordOf(launched, Path.Combine(Environment.SystemDirectory, "cmd.exe")));
        await launched.StopAsync(TestContext.Current.CancellationToken);

        Assert.Null(attached);
    }

    [Fact]
    public async Task Does_not_attach_to_an_exited_game()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        var cancellationToken = TestContext.Current.CancellationToken;
        var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        using var launched = _launcher.Launch(new GameLaunchRequest(cmd, _temp.Path, Logs) { Arguments = ["/c", "exit", "0"] });
        await launched.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        Assert.Null(_launcher.Attach(RecordOf(launched, cmd)));
    }

    [Fact]
    public async Task Stop_ends_a_game_without_a_window()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        var cancellationToken = TestContext.Current.CancellationToken;
        using var game = StartPing();

        await game.StopAsync(cancellationToken);

        Assert.Equal(1, await game.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(10), cancellationToken));
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build tests/PS5Kitsune.Platform.Windows.Tests -c Release`
Expected: FAIL, `CS0246: The type or namespace name 'RunningGame' could not be found` and `'WindowsGameLauncher' does not contain a definition for 'StopGrace'`.

- [ ] **Step 3: Extend the launcher interface**

In `src/PS5Kitsune.Core/Abstractions/IGameLauncher.cs`, add after `GameLaunchRequest`:

```csharp
/// <summary>A launched game, as recorded in .ps5kitsune\running.json. See docs/architecture.md, "Launching".</summary>
/// <param name="StartedAt">The process creation time read from the OS, in UTC: with the PID, it tells the game from a later process reusing the PID.</param>
/// <param name="Executable">The full path of the game's executable.</param>
public sealed record RunningGame(int SchemaVersion, int ProcessId, DateTimeOffset StartedAt, string Executable, string LogFile)
{
    public const int CurrentSchemaVersion = 1;
}
```

In `ILaunchedGame`, add after `LogFile`:

```csharp
    /// <summary>The process creation time read from the OS, in UTC.</summary>
    DateTimeOffset StartedAt { get; }
```

and after `WaitForExitAsync`:

```csharp
    /// <summary>Asks the game's windows to close, waits a few seconds, then ends the process. Completes once it has exited.</summary>
    /// <exception cref="System.ComponentModel.Win32Exception">The process could not be ended.</exception>
    Task StopAsync(CancellationToken cancellationToken = default);
```

In `IGameLauncher`, add after `Launch`:

```csharp
    /// <summary>Opens the recorded game's process again, for example after PS5Kitsune was closed and reopened.</summary>
    /// <returns>Null when the process exited, cannot be opened, or its PID now belongs to another process (start time or executable differ).</returns>
    ILaunchedGame? Attach(RunningGame record);
```

- [ ] **Step 4: Implement attach and stop on Windows**

In `src/PS5Kitsune.Platform.Windows/WindowsGameLauncher.cs`:

Add constants after `ProcThreadAttributeHandleList`:

```csharp
    private const uint ProcessTerminate = 0x0001;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint Synchronize = 0x00100000;
    private const uint StillActive = 259;
```

Add after the constants:

```csharp
    /// <summary>How long Stop waits for the game to close its windows before ending it.</summary>
    internal TimeSpan StopGrace { get; init; } = TimeSpan.FromSeconds(5);
```

In `Launch`, replace the last two lines (`LogLaunched` and the `return`) with:

```csharp
        var handle = new SafeProcessHandle(process.Process, ownsHandle: true);
        var startedAt = CreationTime(handle);
        LogLaunched(request.Executable, process.ProcessId, logFile);
        return new LaunchedGame(handle, process.ProcessId, logFile, startedAt, StopGrace);
    }

    public ILaunchedGame? Attach(RunningGame record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var process = OpenProcess(ProcessQueryLimitedInformation | Synchronize | ProcessTerminate, inheritHandle: false, record.ProcessId);
        // Not opened: the process is gone, or belongs to someone else (another user, an elevated process): not our game either way.
        if (process.IsInvalid
            || !GetExitCodeProcess(process, out var exitCode) || exitCode != StillActive
            || CreationTime(process) != record.StartedAt
            || ImagePath(process) is not { } path
            || !string.Equals(Path.GetFullPath(path), Path.GetFullPath(record.Executable), StringComparison.OrdinalIgnoreCase))
        {
            process.Dispose();
            return null;
        }

        LogAttached(record.Executable, record.ProcessId);
        return new LaunchedGame(process, record.ProcessId, record.LogFile, record.StartedAt, StopGrace);
    }

    private static DateTimeOffset CreationTime(SafeProcessHandle process) =>
        GetProcessTimes(process, out var creation, out _, out _, out _)
            ? DateTimeOffset.FromFileTime(creation).ToUniversalTime()
            : throw new Win32Exception(Marshal.GetLastPInvokeError());

    private static string? ImagePath(SafeProcessHandle process)
    {
        var buffer = new char[32768];
        var size = buffer.Length;
        return QueryFullProcessImageName(process, 0, ref buffer[0], ref size) ? new string(buffer, 0, size) : null;
    }
```

(The `CloseHandle(process.Thread);` line stays before the new lines.)

Add after `LogLaunched`:

```csharp
    [LoggerMessage(Level = LogLevel.Information, Message = "Found {Executable} still running (PID {ProcessId})")]
    private partial void LogAttached(string executable, int processId);
```

Add these imports after `GetExitCodeProcess`:

```csharp
    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetProcessTimes(SafeProcessHandle process, out long creationTime, out long exitTime, out long kernelTime, out long userTime);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, ref char name, ref int size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool TerminateProcess(SafeProcessHandle process, uint exitCode);
```

Replace the `LaunchedGame` class's declaration and first members with:

```csharp
    private sealed class LaunchedGame(SafeProcessHandle process, int processId, string logFile, DateTimeOffset startedAt, TimeSpan stopGrace) : ILaunchedGame
    {
        public int ProcessId { get; } = processId;

        public string LogFile { get; } = logFile;

        public DateTimeOffset StartedAt { get; } = startedAt;

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            CloseWindows();
            var exit = WaitForExitAsync(cancellationToken);
            if (await Task.WhenAny(exit, Task.Delay(stopGrace, cancellationToken)).ConfigureAwait(false) != exit
                && !TerminateProcess(process, 1))
            {
                var error = Marshal.GetLastPInvokeError();
                // Access denied once the process has already exited: nothing left to stop.
                if (!exit.IsCompleted)
                {
                    throw new Win32Exception(error);
                }
            }

            await exit.ConfigureAwait(false);
        }

        // The handle held here keeps the PID from being reused, so the process found by ID is the game.
        private void CloseWindows()
        {
            try
            {
                using var running = System.Diagnostics.Process.GetProcessById(ProcessId);
                running.CloseMainWindow();
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                // Already exited.
            }
        }
```

(`WaitForExitAsync` and `Dispose` stay as they are.)

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet build PS5Kitsune.slnx -c Release` — Expected: 0 warnings, 0 errors.
Run: `dotnet test --project tests/PS5Kitsune.Platform.Windows.Tests -c Release`
Expected: 0 failed (the new tests run on Windows).

- [ ] **Step 6: Commit**

```bash
git add src/PS5Kitsune.Core/Abstractions/IGameLauncher.cs src/PS5Kitsune.Platform.Windows/WindowsGameLauncher.cs tests/PS5Kitsune.Platform.Windows.Tests/WindowsGameLauncherTests.cs
git commit -m "Attach to a running game and stop it

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 3: Core — running games, GamePlayer and GameCleaner

**Files:**
- Create: `src/PS5Kitsune.Core/Library/RunningGames.cs`, `GamePlayer.cs`, `GameCleaner.cs`, `LibraryGame.cs`, `PlayingGame.cs`, `GameRunningException.cs`, `GameExecutableMissingException.cs`, `UnsupportedGameLauncher.cs`
- Modify: `src/PS5Kitsune.Core/Games/GameReconverter.cs`, `src/PS5Kitsune.Core/Games/IGameReconverter.cs`, `src/PS5Kitsune.Core/Games/GameLayout.cs`, `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`
- Create: `tests/PS5Kitsune.Core.Tests/Fakes/FakeGameLauncher.cs`, `tests/PS5Kitsune.Core.Tests/Library/RunningGamesTests.cs`, `GamePlayerTests.cs`, `GameCleanerTests.cs`
- Modify: `tests/PS5Kitsune.Core.Tests/Games/GameReconverterTests.cs`

**Interfaces:**
- Consumes: Task 1 (`GameLock`, `GameBusyException`, `GameLayout.RunningRecord/ShaderCache/Data/Lock`, `GameLibrary.ReadHeld/MarkPlayedAsync`); Task 2 (`RunningGame`, `ILaunchedGame.StartedAt/StopAsync`, `IGameLauncher.Attach`).
- Produces:
  - `RunningGames(IGameLauncher launcher, ILogger<RunningGames> logger)`: `ILaunchedGame? Attach(GameLayout game)`, `RunningGame? Check(GameLayout game)`, `Task RecordAsync(GameLayout game, ILaunchedGame process, string executable)`, `void Forget(GameLayout game, ILaunchedGame process)`.
  - `GamePlayer(GameLibrary library, IAnyPS5Installer installer, IGameLauncher launcher, RunningGames running)`: `IReadOnlyList<LibraryGame> List()`, `Task<PlayingGame> LaunchAsync(string titleId, CancellationToken cancellationToken)`, `void Forget(PlayingGame playing)`.
  - `GameCleaner(GameLibrary library, RunningGames running, ILogger<GameCleaner> logger)`: `void Remove(string titleId)`, `void ClearShaderCache(string titleId)`, `static long ShaderCacheSize(GameLayout game)`, `static bool HasSaves(GameLayout game)`.
  - `sealed record LibraryGame(GameLayout Game, GameRecord Record, RunningGame? Running)`; `sealed record PlayingGame(GameLayout Game, GameRecord Record, ILaunchedGame Process)`.
  - `GameRunningException(string name, RunningGame running)` with `Name`, `Running`; `GameExecutableMissingException(string executable)` with `Executable`.
  - `GameReconverter` gains a `RunningGames running` constructor parameter, right after `GameLibrary library`, and throws `GameRunningException`.
  - Test fake `FakeGameLauncher` / `FakeLaunchedGame` (namespace `PS5Kitsune.Core.Tests.Fakes`), shared with the App tests in Task 6.

- [ ] **Step 1: Write the fake launcher**

Create `tests/PS5Kitsune.Core.Tests/Fakes/FakeGameLauncher.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Tests.Fakes;

/// <summary>Launches fake games that run until a test ends them; also linked into the App tests.</summary>
internal sealed class FakeGameLauncher : IGameLauncher
{
    private int _nextProcessId = 4812;

    public List<GameLaunchRequest> Launches { get; } = [];

    /// <summary>Every game launched, by PID; an exited one stays listed.</summary>
    public Dictionary<int, FakeLaunchedGame> Games { get; } = [];

    /// <summary>Thrown by Launch when set.</summary>
    public Exception? LaunchFailure { get; set; }

    public DateTimeOffset Now { get; set; } = new(2026, 10, 24, 18, 3, 11, TimeSpan.Zero);

    public ILaunchedGame Launch(GameLaunchRequest request)
    {
        if (LaunchFailure is not null)
        {
            throw LaunchFailure;
        }

        Launches.Add(request);
        Directory.CreateDirectory(request.LogDirectory);
        var logFile = Path.Combine(request.LogDirectory, $"{Launches.Count:000}.log");
        File.WriteAllText(logFile, "");
        var game = new FakeLaunchedGame(_nextProcessId++, logFile, Now);
        Games[game.ProcessId] = game;
        return game;
    }

    public ILaunchedGame? Attach(RunningGame record) =>
        Games.TryGetValue(record.ProcessId, out var game) && game.StartedAt == record.StartedAt && !game.HasExited ? game : null;
}

internal sealed class FakeLaunchedGame(int processId, string logFile, DateTimeOffset startedAt) : ILaunchedGame
{
    private readonly TaskCompletionSource<int> _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int ProcessId { get; } = processId;

    public string LogFile { get; } = logFile;

    public DateTimeOffset StartedAt { get; } = startedAt;

    public bool HasExited => _exit.Task.IsCompleted;

    /// <summary>Thrown by StopAsync when set; the game then keeps running.</summary>
    public Exception? StopFailure { get; set; }

    public void Exit(int exitCode) => _exit.TrySetResult(exitCode);

    public Task<int> WaitForExitAsync(CancellationToken cancellationToken = default) => _exit.Task.WaitAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (StopFailure is not null)
        {
            throw StopFailure;
        }

        Exit(1);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }
}
```

- [ ] **Step 2: Write the failing tests**

Create `tests/PS5Kitsune.Core.Tests/Library/RunningGamesTests.cs`:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tests.Games;

namespace PS5Kitsune.Core.Tests.Library;

public sealed class RunningGamesTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FakeGameLauncher _launcher = new();
    private readonly RunningGames _running;
    private readonly GameLayout _game;

    public RunningGamesTests()
    {
        _running = new RunningGames(_launcher, NullLogger<RunningGames>.Instance);
        _game = new GameLayout(_temp.Combine("games", TestGames.TitleId), TestGames.TitleId, new FakePlatform());
        Directory.CreateDirectory(_game.Meta);
    }

    private async Task<FakeLaunchedGame> LaunchAsync()
    {
        var process = (FakeLaunchedGame)_launcher.Launch(new GameLaunchRequest(_game.Executable, _game.Root, _game.Logs));
        await _running.RecordAsync(_game, process, _game.Executable);
        return process;
    }

    [Fact]
    public async Task A_recorded_game_is_found_while_it_runs()
    {
        var process = await LaunchAsync();

        Assert.Same(process, _running.Attach(_game));
        Assert.Equal(
            new RunningGame(RunningGame.CurrentSchemaVersion, process.ProcessId, process.StartedAt, _game.Executable, process.LogFile),
            _running.Check(_game));
    }

    [Fact]
    public void No_record_is_not_running()
    {
        Assert.Null(_running.Check(_game));
        Assert.Null(_running.Attach(_game));
    }

    [Fact]
    public async Task A_record_whose_process_exited_is_deleted()
    {
        var process = await LaunchAsync();
        process.Exit(0);

        Assert.Null(_running.Check(_game));
        Assert.False(File.Exists(_game.RunningRecord));
    }

    [Theory]
    [InlineData("{ \"processId\": ")]
    [InlineData("{ \"schemaVersion\": 2, \"processId\": 1, \"startedAt\": \"2026-10-24T18:03:11Z\", \"executable\": \"x\", \"logFile\": \"y\" }")]
    [InlineData("{ \"schemaVersion\": 1, \"processId\": 1, \"startedAt\": \"2026-10-24T18:03:11Z\" }")]
    public void A_malformed_record_is_deleted(string json)
    {
        TestFiles.Write(_game.RunningRecord, json);

        Assert.Null(_running.Check(_game));
        Assert.False(File.Exists(_game.RunningRecord));
    }

    [Fact]
    public async Task Forgetting_deletes_the_record_of_that_process_only()
    {
        var first = await LaunchAsync();
        first.Exit(0);
        var second = await LaunchAsync();

        _running.Forget(_game, first);
        Assert.True(File.Exists(_game.RunningRecord));

        _running.Forget(_game, second);
        Assert.False(File.Exists(_game.RunningRecord));
    }

    public void Dispose() => _temp.Dispose();
}
```

Create `tests/PS5Kitsune.Core.Tests/Library/GamePlayerTests.cs`:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Tests.AnyPS5;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tests.Games;

namespace PS5Kitsune.Core.Tests.Library;

public sealed class GamePlayerTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly AnyPS5TestSetup _setup;
    private readonly FakeGameLauncher _launcher = new();
    private readonly GameLibrary _library;
    private readonly RunningGames _running;
    private readonly GamePlayer _player;

    public GamePlayerTests()
    {
        _setup = new AnyPS5TestSetup(_temp.Combine("root"));
        _library = new GameLibrary(_setup.Layout, _setup.Platform, _setup.Time, NullLogger<GameLibrary>.Instance);
        _running = new RunningGames(_launcher, NullLogger<RunningGames>.Instance);
        _player = new GamePlayer(_library, _setup.Installer, _launcher, _running);
    }

    private GameLayout Game => new(_setup.Layout.GameDirectory(TestGames.TitleId), TestGames.TitleId, _setup.Platform);

    private async Task AddDoomAsync()
    {
        await _setup.InstallAsync();
        ConvertedGames.Create(Game);
        await GameRecordStore.WriteAsync(Game.RecordFile, TestRecords.Sample(), TestContext.Current.CancellationToken);
    }

    private Task<PlayingGame> LaunchAsync() => _player.LaunchAsync(TestGames.TitleId, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Launching_starts_the_game_from_its_folder_records_it_and_marks_it_played()
    {
        await AddDoomAsync();

        var playing = await LaunchAsync();

        var request = Assert.Single(_launcher.Launches);
        Assert.Equal(Game.Executable, request.Executable);
        Assert.Equal(Game.Root, request.WorkingDirectory);
        Assert.Equal(Game.Logs, request.LogDirectory);
        Assert.Equal(_setup.Time.GetLocalNow(), playing.Record.LastPlayedAt);
        Assert.Equal(_setup.Time.GetLocalNow(), _library.Find(TestGames.TitleId).Record.LastPlayedAt);
        Assert.Equal(playing.Process.ProcessId, _running.Check(Game)!.ProcessId);
        Assert.False(GameLock.IsHeld(Game));
    }

    [Fact]
    public async Task Listing_shows_which_games_run()
    {
        await AddDoomAsync();
        Assert.Null(Assert.Single(_player.List()).Running);

        var playing = await LaunchAsync();

        Assert.Equal(playing.Process.ProcessId, Assert.Single(_player.List()).Running!.ProcessId);
    }

    [Fact]
    public async Task Launching_requires_the_install()
    {
        ConvertedGames.Create(Game);
        await GameRecordStore.WriteAsync(Game.RecordFile, TestRecords.Sample(), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<AnyPS5NotInstalledException>(LaunchAsync);
        Assert.Empty(_launcher.Launches);
    }

    [Fact]
    public async Task A_running_game_is_not_launched_again()
    {
        await AddDoomAsync();
        var playing = await LaunchAsync();

        var refused = await Assert.ThrowsAsync<GameRunningException>(LaunchAsync);

        Assert.Equal("DOOM", refused.Name);
        Assert.Equal(playing.Process.ProcessId, refused.Running.ProcessId);
        Assert.Equal($"DOOM is already running (PID {playing.Process.ProcessId}). Log: {playing.Process.LogFile}.", refused.Message);
        Assert.Single(_launcher.Launches);
    }

    [Fact]
    public async Task A_game_whose_process_exited_is_launched_again()
    {
        await AddDoomAsync();
        ((FakeLaunchedGame)(await LaunchAsync()).Process).Exit(0);

        await LaunchAsync();

        Assert.Equal(2, _launcher.Launches.Count);
    }

    [Fact]
    public async Task A_busy_game_is_refused()
    {
        await AddDoomAsync();

        using (GameLock.TryAcquireNow(Game))
        {
            await Assert.ThrowsAsync<GameBusyException>(LaunchAsync);
        }

        Assert.Empty(_launcher.Launches);
    }

    [Fact]
    public async Task A_missing_executable_is_refused()
    {
        await AddDoomAsync();
        File.Delete(Game.Executable);

        var refused = await Assert.ThrowsAsync<GameExecutableMissingException>(LaunchAsync);

        Assert.Equal(Game.Executable, refused.Executable);
        Assert.Empty(_launcher.Launches);
        Assert.False(File.Exists(Game.RunningRecord));
    }

    [Fact]
    public async Task Launching_restores_an_interrupted_reconvert_first()
    {
        await AddDoomAsync();
        var before = ConvertedGames.Snapshot(Game.Root);
        ConversionBackup.SetAside(Game);
        TestFiles.Write(Game.Executable, "half-built exe");

        await LaunchAsync();

        Assert.False(Directory.Exists(Game.Previous));
        Assert.Equal(before["PPSA99666.exe"], ConvertedGames.Snapshot(Game.Root)["PPSA99666.exe"]);
    }

    [Fact]
    public async Task Forgetting_a_game_that_exited_deletes_its_record()
    {
        await AddDoomAsync();
        var playing = await LaunchAsync();
        ((FakeLaunchedGame)playing.Process).Exit(0);

        _player.Forget(playing);

        Assert.False(File.Exists(Game.RunningRecord));
    }

    public void Dispose() => _temp.Dispose();
}
```

Create `tests/PS5Kitsune.Core.Tests/Library/GameCleanerTests.cs`:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tests.Games;

namespace PS5Kitsune.Core.Tests.Library;

public sealed class GameCleanerTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly DataLayout _layout;
    private readonly FakeGameLauncher _launcher = new();
    private readonly GameLibrary _library;
    private readonly RunningGames _running;
    private readonly GameCleaner _cleaner;
    private readonly GameLayout _game;

    public GameCleanerTests()
    {
        _layout = new DataLayout(_temp.Path);
        _library = new GameLibrary(_layout, new FakePlatform(), TimeProvider.System, NullLogger<GameLibrary>.Instance);
        _running = new RunningGames(_launcher, NullLogger<RunningGames>.Instance);
        _cleaner = new GameCleaner(_library, _running, NullLogger<GameCleaner>.Instance);
        _game = new GameLayout(_layout.GameDirectory(TestGames.TitleId), TestGames.TitleId, new FakePlatform());
    }

    private async Task AddDoomAsync()
    {
        ConvertedGames.Create(_game);
        await GameRecordStore.WriteAsync(_game.RecordFile, TestRecords.Sample(), TestContext.Current.CancellationToken);
    }

    private Task StartDoomAsync() =>
        _running.RecordAsync(_game, _launcher.Launch(new GameLaunchRequest(_game.Executable, _game.Root, _game.Logs)), _game.Executable);

    [Fact]
    public async Task The_shader_cache_size_counts_every_file()
    {
        await AddDoomAsync();
        Assert.Equal("kept shader_cache/v1/cache.bin".Length, GameCleaner.ShaderCacheSize(_game));
        TestFiles.Write(Path.Combine(_game.ShaderCache, "v2", "deep", "more.bin"), "12345");

        Assert.Equal("kept shader_cache/v1/cache.bin".Length + 5, GameCleaner.ShaderCacheSize(_game));
    }

    [Fact]
    public void No_shader_cache_has_size_zero()
    {
        Assert.Equal(0, GameCleaner.ShaderCacheSize(_game));
    }

    [Fact]
    public async Task Clearing_the_shader_cache_keeps_everything_else()
    {
        await AddDoomAsync();
        var before = ConvertedGames.Snapshot(_game.Root);

        _cleaner.ClearShaderCache(TestGames.TitleId);

        Assert.False(Directory.Exists(_game.ShaderCache));
        before.Remove("shader_cache/v1/cache.bin");
        Assert.Equal(before, ConvertedGames.Snapshot(_game.Root));
    }

    [Fact]
    public async Task Saves_are_detected()
    {
        await AddDoomAsync();
        Assert.True(GameCleaner.HasSaves(_game));

        Directory.Delete(_game.Data, recursive: true);
        Directory.CreateDirectory(_game.Data);

        Assert.False(GameCleaner.HasSaves(_game));
    }

    [Fact]
    public async Task Removing_deletes_the_game_folder()
    {
        await AddDoomAsync();

        _cleaner.Remove(TestGames.TitleId);

        Assert.False(Directory.Exists(_game.Root));
        Assert.Empty(_library.All());
    }

    [Fact]
    public async Task A_running_game_is_neither_removed_nor_cleared()
    {
        await AddDoomAsync();
        await StartDoomAsync();
        var before = ConvertedGames.Snapshot(_game.Root);

        Assert.Equal("DOOM", Assert.Throws<GameRunningException>(() => _cleaner.Remove(TestGames.TitleId)).Name);
        Assert.Throws<GameRunningException>(() => _cleaner.ClearShaderCache(TestGames.TitleId));

        Assert.Equal(before, ConvertedGames.Snapshot(_game.Root));
    }

    [Fact]
    public async Task A_busy_game_is_neither_removed_nor_cleared()
    {
        await AddDoomAsync();

        using (GameLock.TryAcquireNow(_game))
        {
            Assert.Throws<GameBusyException>(() => _cleaner.Remove(TestGames.TitleId));
            Assert.Throws<GameBusyException>(() => _cleaner.ClearShaderCache(TestGames.TitleId));
        }

        Assert.True(File.Exists(_game.RecordFile));
        Assert.True(Directory.Exists(_game.ShaderCache));
    }

    [Fact]
    public async Task A_removal_stopped_by_a_file_in_use_keeps_the_game_listed_and_can_be_retried()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Only Windows refuses to delete an open file.");
        }

        await AddDoomAsync();

        using (new FileStream(Path.Combine(_game.App0, "eboot.bin"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.ThrowsAny<IOException>(() => _cleaner.Remove(TestGames.TitleId));
            Assert.Equal("DOOM", Assert.Single(_library.All()).Record.Name);
        }

        _cleaner.Remove(TestGames.TitleId);

        Assert.False(Directory.Exists(_game.Root));
    }

    public void Dispose() => _temp.Dispose();
}
```

In `tests/PS5Kitsune.Core.Tests/Games/GameReconverterTests.cs`:
- add the field `private readonly FakeGameLauncher _launcher = new();` and the field `private readonly RunningGames _running;`;
- in the constructor, before `_reconverter = …`, add `_running = new RunningGames(_launcher, NullLogger<RunningGames>.Instance);` and pass `_running` as the reconverter's second argument: `new GameReconverter(_library, _running, _setup.Paths, …)`;
- add the test:

```csharp
    [Fact]
    public async Task A_running_game_is_refused_with_nothing_changed()
    {
        await AddDoomConvertedWithTheBuildAsync();
        var process = _launcher.Launch(new GameLaunchRequest(Game.Executable, Game.Root, Game.Logs));
        await _running.RecordAsync(Game, process, Game.Executable);
        var before = ConvertedGames.Snapshot(Game.Root);
        var callsBefore = _setup.Runner.Calls.Count;

        var refused = await Assert.ThrowsAsync<GameRunningException>(ReconvertAsync);

        Assert.Equal("DOOM", refused.Name);
        Assert.Equal(before, ConvertedGames.Snapshot(Game.Root));
        Assert.Equal(callsBefore, _setup.Runner.Calls.Count);
        Assert.False(GameLock.IsHeld(Game));
    }
```

- [ ] **Step 3: Run the tests to verify they fail to compile**

Run: `dotnet build tests/PS5Kitsune.Core.Tests -c Release`
Expected: FAIL, `CS0246: The type or namespace name 'RunningGames' could not be found` (and `GamePlayer`, `GameCleaner`).

- [ ] **Step 4: Write the records and exceptions**

Create `src/PS5Kitsune.Core/Library/LibraryGame.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Core.Library;

/// <summary>An added game as the library shows it.</summary>
/// <param name="Running">Its running.json, checked against the live process; null when it is not running.</param>
public sealed record LibraryGame(GameLayout Game, GameRecord Record, RunningGame? Running);
```

Create `src/PS5Kitsune.Core/Library/PlayingGame.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Core.Library;

/// <summary>A game launched or found running. Disposing <see cref="Process"/> releases the handle, never the game.</summary>
public sealed record PlayingGame(GameLayout Game, GameRecord Record, ILaunchedGame Process);
```

Create `src/PS5Kitsune.Core/Library/GameRunningException.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Library;

/// <summary>The game is running: PS5Kitsune launched it and its process is alive. Nothing was changed.</summary>
public sealed class GameRunningException(string name, RunningGame running)
    : Exception($"{name} is already running (PID {running.ProcessId}). Log: {running.LogFile}.")
{
    public string Name { get; } = name;

    public RunningGame Running { get; } = running;
}
```

Create `src/PS5Kitsune.Core/Library/GameExecutableMissingException.cs`:

```csharp
namespace PS5Kitsune.Core.Library;

/// <summary>The converted executable is gone, usually deleted by an antivirus; a Reconvert writes it again.</summary>
public sealed class GameExecutableMissingException(string executable)
    : Exception($"{executable} is missing; an antivirus may have deleted it. Add an exclusion for the games folder, then reconvert the game.")
{
    public string Executable { get; } = executable;
}
```

Create `src/PS5Kitsune.Core/Library/UnsupportedGameLauncher.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Library;

/// <summary>Stands in where the OS has no launcher yet (Linux, milestone 8): nothing can start, so nothing is running.</summary>
public sealed class UnsupportedGameLauncher : IGameLauncher
{
    public ILaunchedGame Launch(GameLaunchRequest request) => throw new PlatformNotSupportedException("Launching games is not supported on this OS yet.");

    public ILaunchedGame? Attach(RunningGame record) => null;
}
```

- [ ] **Step 5: Write RunningGames**

Create `src/PS5Kitsune.Core/Library/RunningGames.cs`:

```csharp
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Core.Library;

/// <summary>
/// The game PS5Kitsune launched from each game folder, recorded in .ps5kitsune\running.json and checked against the live process: closing
/// PS5Kitsune never stops a game, and the next start finds it again. A record whose process exited, or whose PID now belongs to another
/// process, is deleted, and so is a malformed one.
/// </summary>
public sealed partial class RunningGames(IGameLauncher launcher, ILogger<RunningGames> logger)
{
    /// <summary>The game's process, opened again; null when it is not running. Dispose the result.</summary>
    public ILaunchedGame? Attach(GameLayout game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return Find(game).Process;
    }

    /// <summary>The game's record when its process is running; the handle opened to check it is closed again.</summary>
    public RunningGame? Check(GameLayout game)
    {
        ArgumentNullException.ThrowIfNull(game);
        var (record, process) = Find(game);
        using (process)
        {
            return process is null ? null : record;
        }
    }

    /// <summary>Records a game just launched. Not cancellable: the game already runs.</summary>
    public Task RecordAsync(GameLayout game, ILaunchedGame process, string executable)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(process);
        var record = new RunningGame(RunningGame.CurrentSchemaVersion, process.ProcessId, process.StartedAt, Path.GetFullPath(executable), process.LogFile);
        return JsonFiles.WriteAsync(game.RunningRecord, record, CancellationToken.None);
    }

    /// <summary>Deletes the record once its game exited; a record of a later launch is kept.</summary>
    public void Forget(GameLayout game, ILaunchedGame process)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(process);
        if (TryRead(game) is { } record && record.ProcessId == process.ProcessId && record.StartedAt == process.StartedAt)
        {
            TryDelete(game.RunningRecord);
        }
    }

    private (RunningGame? Record, ILaunchedGame? Process) Find(GameLayout game)
    {
        RunningGame? record;
        try
        {
            record = Read(game);
        }
        catch (JsonException exception)
        {
            var path = game.RunningRecord;
            LogMalformed(exception, path);
            TryDelete(path);
            return (null, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Being replaced by a launch right now: unknown, so not deleted.
            var path = game.RunningRecord;
            LogUnreadable(exception, path);
            return (null, null);
        }

        if (record is null)
        {
            return (null, null);
        }

        var process = launcher.Attach(record);
        if (process is null && TryRead(game) == record)
        {
            // Unchanged since it was read: a launch that just replaced it is not undone.
            TryDelete(game.RunningRecord);
        }

        return (record, process);
    }

    /// <exception cref="JsonException">Malformed, another schema version, or a field missing.</exception>
    private static RunningGame? Read(GameLayout game)
    {
        var record = JsonFiles.Read<RunningGame>(game.RunningRecord);
        if (record is null)
        {
            return null;
        }

        if (record.SchemaVersion != RunningGame.CurrentSchemaVersion)
        {
            throw new JsonException($"Schema version {record.SchemaVersion}; this app reads version {RunningGame.CurrentSchemaVersion}.");
        }

        return record.ProcessId > 0 && !string.IsNullOrEmpty(record.Executable) && !string.IsNullOrEmpty(record.LogFile)
            ? record
            : throw new JsonException("A field is missing.");
    }

    private static RunningGame? TryRead(GameLayout game)
    {
        try
        {
            return Read(game);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Found stale again on the next check.
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Deleted {Path}: it is not a valid running game record.")]
    private partial void LogMalformed(Exception exception, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read {Path}; the game is treated as not running.")]
    private partial void LogUnreadable(Exception exception, string path);
}
```

Note: `RunningGame` is a positional record, so JSON missing `executable` deserializes to null despite the non-nullable type; the `IsNullOrEmpty` check is what rejects it. If the analyzer flags `string.IsNullOrEmpty` on a non-nullable string as always false, keep it anyway (it is not flagged by the current rule set; if it is, compare with `is null or ""`).

- [ ] **Step 6: Write GamePlayer and GameCleaner**

Create `src/PS5Kitsune.Core/Library/GamePlayer.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Core.Library;

/// <summary>Lists and launches the added games: the one path for `run` and the app. See the 7a spec, "GamePlayer".</summary>
public sealed class GamePlayer(GameLibrary library, IAnyPS5Installer installer, IGameLauncher launcher, RunningGames running)
{
    /// <summary>Every readable game, by name, with whether it runs.</summary>
    public IReadOnlyList<LibraryGame> List() =>
        [.. library.All().Select(entry => new LibraryGame(entry.Game, entry.Record, running.Check(entry.Game)))];

    /// <summary>
    /// Launches the game from its folder, its output in a new log, and records it in running.json. The game lock is held from the checks
    /// until the record is written, so a Reconvert or a removal cannot start in between.
    /// </summary>
    /// <exception cref="AnyPS5NotInstalledException">No AnyPS5 install is in use.</exception>
    /// <exception cref="GameNotFoundException">No game with this title ID.</exception>
    /// <exception cref="InvalidGameRecordException">The game.json is malformed.</exception>
    /// <exception cref="GameBusyException">Another action holds the game's lock.</exception>
    /// <exception cref="GameRunningException">The game is already running.</exception>
    /// <exception cref="GameExecutableMissingException">The converted executable is missing.</exception>
    public async Task<PlayingGame> LaunchAsync(string titleId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(titleId);
        if (installer.ReadRecord() is null)
        {
            throw new AnyPS5NotInstalledException();
        }

        var game = library.Layout(titleId);
        using var held = GameLock.TryAcquire(game) ?? throw new GameBusyException(titleId);
        var record = library.ReadHeld(game);
        if (running.Check(game) is { } current)
        {
            throw new GameRunningException(record.Name, current);
        }

        var executable = Path.Combine(game.Root, record.Executable);
        if (!File.Exists(executable))
        {
            throw new GameExecutableMissingException(executable);
        }

        var played = await library.MarkPlayedAsync(game, cancellationToken).ConfigureAwait(false);
        var process = launcher.Launch(new GameLaunchRequest(executable, game.Root, game.Logs));
        try
        {
            await running.RecordAsync(game, process, executable).ConfigureAwait(false);
        }
        catch
        {
            process.Dispose();
            throw;
        }

        return new PlayingGame(game, played, process);
    }

    /// <summary>Deletes the game's running.json once its exit was seen.</summary>
    public void Forget(PlayingGame playing)
    {
        ArgumentNullException.ThrowIfNull(playing);
        running.Forget(playing.Game, playing.Process);
    }
}
```

Create `src/PS5Kitsune.Core/Library/GameCleaner.cs`:

```csharp
using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Core.Library;

/// <summary>Removes a game or clears its shader cache: under the game lock, and never while the game runs.</summary>
public sealed partial class GameCleaner(GameLibrary library, RunningGames running, ILogger<GameCleaner> logger)
{
    /// <summary>Bytes in shader_cache\; 0 when there is none.</summary>
    public static long ShaderCacheSize(GameLayout game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return Directory.Exists(game.ShaderCache)
            ? new DirectoryInfo(game.ShaderCache).EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length)
            : 0;
    }

    /// <summary>True when data\ holds at least one file.</summary>
    public static bool HasSaves(GameLayout game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return Directory.Exists(game.Data) && Directory.EnumerateFiles(game.Data, "*", SearchOption.AllDirectories).Any();
    }

    /// <exception cref="GameNotFoundException">No game with this title ID.</exception>
    /// <exception cref="GameBusyException">Another action holds the game's lock.</exception>
    /// <exception cref="GameRunningException">The game runs.</exception>
    /// <exception cref="IOException">A file is in use: the cache may be partly cleared, and the game rebuilds it.</exception>
    /// <exception cref="UnauthorizedAccessException">A file could not be deleted: the cache may be partly cleared.</exception>
    public void ClearShaderCache(string titleId)
    {
        var game = library.Layout(titleId);
        using var held = GameLock.TryAcquire(game) ?? throw new GameBusyException(titleId);
        RefuseRunning(game, library.ReadHeld(game));
        if (Directory.Exists(game.ShaderCache))
        {
            Directory.Delete(game.ShaderCache, recursive: true);
        }
    }

    /// <summary>
    /// Deletes the game folder, saves included. game.json goes last: a delete stopped by a file in use leaves the game listed, and a
    /// retry finishes it.
    /// </summary>
    /// <exception cref="GameNotFoundException">No game with this title ID.</exception>
    /// <exception cref="GameBusyException">Another action holds the game's lock.</exception>
    /// <exception cref="GameRunningException">The game runs.</exception>
    /// <exception cref="IOException">A file is in use; the game stays listed.</exception>
    /// <exception cref="UnauthorizedAccessException">A file could not be deleted; the game stays listed.</exception>
    public void Remove(string titleId)
    {
        var game = library.Layout(titleId);
        using (GameLock.TryAcquire(game) ?? throw new GameBusyException(titleId))
        {
            RefuseRunning(game, library.ReadHeld(game));
            foreach (var directory in Directory.GetDirectories(game.Root))
            {
                if (!SamePath(directory, game.Meta))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }

            foreach (var file in Directory.GetFiles(game.Root))
            {
                File.Delete(file);
            }

            foreach (var directory in Directory.GetDirectories(game.Meta))
            {
                Directory.Delete(directory, recursive: true);
            }

            foreach (var file in Directory.GetFiles(game.Meta))
            {
                if (!SamePath(file, game.RecordFile) && !SamePath(file, game.Lock))
                {
                    File.Delete(file);
                }
            }

            File.Delete(game.RecordFile);
        }

        // The lock file went with its handle; without game.json the folder is no longer a game, so a failure here is only logged.
        try
        {
            Directory.Delete(game.Root, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var root = game.Root;
            LogFolderLeft(exception, root);
        }
    }

    private void RefuseRunning(GameLayout game, GameRecord record)
    {
        if (running.Check(game) is { } current)
        {
            throw new GameRunningException(record.Name, current);
        }
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Removed the game, but {Root} could not be deleted; it is no longer listed.")]
    private partial void LogFolderLeft(Exception exception, string root);
}
```

- [ ] **Step 7: Refuse a running game in Reconvert, register the services**

In `src/PS5Kitsune.Core/Games/GameReconverter.cs`:
- add the constructor parameter `RunningGames running,` right after `GameLibrary library,`;
- after `var record = library.ReadHeld(game);`, add:

```csharp
        if (running.Check(game) is { } current)
        {
            throw new GameRunningException(record.Name, current);
        }
```

In `src/PS5Kitsune.Core/Games/IGameReconverter.cs`, add after the `GameBusyException` line:

```csharp
    /// <exception cref="Library.GameRunningException">The game runs; nothing was changed.</exception>
```

In `src/PS5Kitsune.Core/Games/GameLayout.cs`, change the `RunningRecord` summary to `/// <summary>The game launched by PS5Kitsune that may still run. See <see cref="Library.RunningGames"/>.</summary>`.

In `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`:
- add `using Microsoft.Extensions.DependencyInjection.Extensions;`;
- after `services.AddSingleton<GameLibrary>();` add:

```csharp
        // The platform registers its launcher first; elsewhere nothing can run, so nothing is running.
        services.TryAddSingleton<IGameLauncher, UnsupportedGameLauncher>();
        services.AddSingleton<RunningGames>();
        services.AddSingleton<GamePlayer>();
        services.AddSingleton<GameCleaner>();
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet build PS5Kitsune.slnx -c Release` — Expected: 0 warnings, 0 errors.
Run: `dotnet test --project tests/PS5Kitsune.Core.Tests -c Release`
Expected: 0 failed.

- [ ] **Step 9: Commit**

```bash
git add src/PS5Kitsune.Core tests/PS5Kitsune.Core.Tests
git commit -m "Record running games, launch through GamePlayer, remove and clear through GameCleaner

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 4: Platform — tools die with PS5Kitsune

**Files:**
- Create: `src/PS5Kitsune.Core/Abstractions/IChildProcessJob.cs`, `src/PS5Kitsune.Core/Processes/NoChildProcessJob.cs`, `src/PS5Kitsune.Platform.Windows/WindowsChildProcessJob.cs`
- Modify: `src/PS5Kitsune.Core/Processes/CliWrapProcessRunner.cs`, `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`, `src/PS5Kitsune.Platform.Windows/PlatformServices.cs`
- Create: `tests/PS5Kitsune.Platform.Windows.Tests/WindowsChildProcessJobTests.cs`
- Modify: `tests/PS5Kitsune.Core.Tests/Processes/CliWrapProcessRunnerTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `interface IChildProcessJob { void Assign(int processId); }`; `CliWrapProcessRunner(ILogger<CliWrapProcessRunner> logger, IChildProcessJob? job = null)`; `WindowsChildProcessJob(ILogger<WindowsChildProcessJob> logger) : IChildProcessJob, IDisposable`.

- [ ] **Step 1: Write the failing tests**

In `tests/PS5Kitsune.Core.Tests/Processes/CliWrapProcessRunnerTests.cs`, add inside the class:

```csharp
    private sealed class RecordingJob : IChildProcessJob
    {
        public List<int> Assigned { get; } = [];

        public void Assign(int processId) => Assigned.Add(processId);
    }

    [Fact]
    public async Task Assigns_each_tool_to_the_job()
    {
        var job = new RecordingJob();
        var runner = new CliWrapProcessRunner(NullLogger<CliWrapProcessRunner>.Instance, job);

        await runner.RunAsync(new ProcessSpec("dotnet", ["--version"]), cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(Assert.Single(job.Assigned) > 0);
    }
```

Create `tests/PS5Kitsune.Platform.Windows.Tests/WindowsChildProcessJobTests.cs`:

```csharp
using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging.Abstractions;

namespace PS5Kitsune.Platform.Windows.Tests;

[SupportedOSPlatform("windows")]
public sealed class WindowsChildProcessJobTests
{
    private static Process StartPing() => Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "PING.EXE"), "-n 30 127.0.0.1")
    {
        CreateNoWindow = true,
        UseShellExecute = false,
        RedirectStandardOutput = true,
    })!;

    [Fact]
    public async Task A_process_in_the_job_ends_when_the_job_is_closed()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        var cancellationToken = TestContext.Current.CancellationToken;
        using var ping = StartPing();
        var job = new WindowsChildProcessJob(NullLogger<WindowsChildProcessJob>.Instance);

        job.Assign(ping.Id);
        job.Dispose();

        await ping.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        Assert.True(ping.HasExited);
    }

    [Fact]
    public async Task Assigning_a_process_that_exited_does_not_throw()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        var cancellationToken = TestContext.Current.CancellationToken;
        using var ping = StartPing();
        var processId = ping.Id;
        ping.Kill();
        await ping.WaitForExitAsync(cancellationToken);
        using var job = new WindowsChildProcessJob(NullLogger<WindowsChildProcessJob>.Instance);

        job.Assign(processId);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: FAIL, `CS0246: The type or namespace name 'IChildProcessJob' could not be found` and `'WindowsChildProcessJob' could not be found`.

- [ ] **Step 3: Write the abstraction, the fallback and the runner change**

Create `src/PS5Kitsune.Core/Abstractions/IChildProcessJob.cs`:

```csharp
namespace PS5Kitsune.Core.Abstractions;

/// <summary>
/// Ties the tools PS5Kitsune starts (git, CMake, Ninja, Python, the relinker) to its lifetime: when PS5Kitsune ends, even killed from the
/// Task Manager, they end too. Games are never assigned: they outlive PS5Kitsune.
/// </summary>
public interface IChildProcessJob
{
    /// <summary>Adds a started tool; the processes it starts afterwards follow it. Never throws: a tool that already exited is skipped.</summary>
    void Assign(int processId);
}
```

Create `src/PS5Kitsune.Core/Processes/NoChildProcessJob.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Processes;

/// <summary>Where the OS offers no job object yet (Linux, milestone 8): tools are not tied to PS5Kitsune.</summary>
public sealed class NoChildProcessJob : IChildProcessJob
{
    public void Assign(int processId)
    {
    }
}
```

In `src/PS5Kitsune.Core/Processes/CliWrapProcessRunner.cs`:
- change the class declaration to `public sealed partial class CliWrapProcessRunner(ILogger<CliWrapProcessRunner> logger, IChildProcessJob? job = null) : IProcessRunner`;
- after `var task = command.ExecuteAsync(CancellationToken.None);` add:

```csharp
        // Before the tool starts its own children, so they join the job too (a child started in the first microseconds escapes).
        job?.Assign(task.ProcessId);
```

In `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`, before `services.AddSingleton<IProcessRunner, CliWrapProcessRunner>();` add:

```csharp
        services.TryAddSingleton<IChildProcessJob, NoChildProcessJob>();
```

- [ ] **Step 4: Write the Windows job**

Create `src/PS5Kitsune.Platform.Windows/WindowsChildProcessJob.cs`:

```csharp
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Platform.Windows;

/// <summary>
/// One job object per PS5Kitsune process, with kill-on-close: Windows closes the handle when PS5Kitsune ends, however it ends, and
/// ends every tool in the job. PS5Kitsune itself is not in the job, so the games it launches are not either.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsChildProcessJob : IChildProcessJob, IDisposable
{
    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JobObjectLimitKillOnJobClose = 0x00002000;
    private const uint ProcessTerminate = 0x0001;
    private const uint ProcessSetQuota = 0x0100;

    private readonly JobHandle _job;
    private readonly ILogger<WindowsChildProcessJob> _logger;

    public WindowsChildProcessJob(ILogger<WindowsChildProcessJob> logger)
    {
        _logger = logger;
        _job = CreateJobObject(0, null);
        if (_job.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        var limits = new ExtendedLimitInformation { BasicLimitInformation = new BasicLimitInformation { LimitFlags = JobObjectLimitKillOnJobClose } };
        if (!SetInformationJobObject(_job, JobObjectExtendedLimitInformation, ref limits, (uint)Marshal.SizeOf<ExtendedLimitInformation>()))
        {
            var error = Marshal.GetLastPInvokeError();
            _job.Dispose();
            throw new Win32Exception(error);
        }
    }

    public void Assign(int processId)
    {
        using var process = OpenProcess(ProcessSetQuota | ProcessTerminate, inheritHandle: false, processId);
        if (process.IsInvalid || !AssignProcessToJobObject(_job, process))
        {
            var error = Marshal.GetLastPInvokeError();
            LogAssignFailed(processId, error);
        }
    }

    public void Dispose() => _job.Dispose();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not tie process {ProcessId} to PS5Kitsune (error {Error}); it may have exited already.")]
    private partial void LogAssignFailed(int processId, int error);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateJobObjectW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial JobHandle CreateJobObject(nint jobAttributes, string? name);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetInformationJobObject(JobHandle job, int infoClass, ref ExtendedLimitInformation info, uint length);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AssignProcessToJobObject(JobHandle job, SafeProcessHandle process);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);

    private sealed class JobHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public JobHandle()
            : base(ownsHandle: true)
        {
        }

        protected override bool ReleaseHandle() => CloseHandle(handle);
    }

#pragma warning disable CS0649 // Native layouts: Windows reads fields the C# code never touches.
    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimitInformation
    {
        public BasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }
#pragma warning restore CS0649
}
```

In `src/PS5Kitsune.Platform.Windows/PlatformServices.cs`, after `services.AddSingleton<IGameLauncher, WindowsGameLauncher>();` add:

```csharp
            services.AddSingleton<IChildProcessJob, WindowsChildProcessJob>();
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet build PS5Kitsune.slnx -c Release` — Expected: 0 warnings, 0 errors.
Run: `dotnet test --project tests/PS5Kitsune.Platform.Windows.Tests -c Release` and `dotnet test --project tests/PS5Kitsune.Core.Tests -c Release`
Expected: 0 failed in both.

- [ ] **Step 6: Commit**

```bash
git add src/PS5Kitsune.Core src/PS5Kitsune.Platform.Windows tests/PS5Kitsune.Core.Tests/Processes tests/PS5Kitsune.Platform.Windows.Tests
git commit -m "End the tools PS5Kitsune started when it ends

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 5: CLI — run through GamePlayer, list, remove

**Files:**
- Modify: `src/PS5Kitsune.Cli/RunCommand.cs`, `src/PS5Kitsune.Cli/CliLog.cs`, `src/PS5Kitsune.Cli/Program.cs`
- Create: `src/PS5Kitsune.Cli/ListCommand.cs`, `src/PS5Kitsune.Cli/RemoveCommand.cs`

**Interfaces:**
- Consumes: `GamePlayer.LaunchAsync/List/Forget`, `PlayingGame`, `LibraryGame`, `GameCleaner.Remove`, `GameCleaner.HasSaves` (Task 3); `GameLibrary.Find`, `GameLibrary.NeedsReconversion`, `ConversionLabels`, `LogTail.FollowAsync`.
- Produces: `ps5kitsune list`; `ps5kitsune remove <titleId> [--yes]`; `run` records the game and forgets it on exit. There is no CLI test project: these are checked by hand in the criterion run.

- [ ] **Step 1: Rewrite run**

Replace `src/PS5Kitsune.Cli/RunCommand.cs` with:

```csharp
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Cli;

internal static class RunCommand
{
    public static Command Create(Option<DirectoryInfo?> rootOption)
    {
        var titleIdArgument = new Argument<string>("titleId") { Description = "Title ID of an added game, for example PPSA99666." };
        var command = new Command("run", "Launch an added game from its folder and follow its log until it exits.") { titleIdArgument };
        command.SetAction((parseResult, cancellationToken) => CommandHost.RunAsync(parseResult.GetValue(rootOption), "run", async (services, logger) =>
        {
            var installer = services.GetRequiredService<AnyPS5Installer>();
            installer.MigrateLegacyInstall();
            var player = services.GetRequiredService<GamePlayer>();

            // A running game, a busy game or a missing executable throws: CommandHost prints the message and exits with 1.
            var playing = await player.LaunchAsync(parseResult.GetRequiredValue(titleIdArgument), cancellationToken);
            var record = playing.Record;
            using var launched = playing.Process;
            if (installer.ReadRecord() is { } install && GameLibrary.NeedsReconversion(record, install))
            {
                var convertedWith = ConversionLabels.Of(record.ConvertedWith);
                var inUse = ConversionLabels.Of(install);
                var titleId = record.TitleId;
                logger.NeedsReconversion(convertedWith, inUse, titleId);
            }

            logger.GameStarted(record.Name, launched.ProcessId, launched.LogFile);
            var exit = launched.WaitForExitAsync(cancellationToken);
            try
            {
                await LogTail.FollowAsync(launched.LogFile, Console.WriteLine, exit, cancellationToken);
                var exitCode = await exit;
                player.Forget(playing);
                logger.GameExited(record.Name, exitCode);
                return exitCode;
            }
            catch (OperationCanceledException)
            {
                // Ctrl+C stops following the log, not the game; running.json stays, so the app finds the game.
                // Let the wait unregister before the handle is released.
                await exit.ContinueWith(static _ => { }, TaskScheduler.Default);
                logger.StoppedFollowing(record.Name, launched.ProcessId, launched.LogFile);
                return 130;
            }
        }));
        return command;
    }
}
```

- [ ] **Step 2: Add the texts**

In `src/PS5Kitsune.Cli/CliLog.cs`, delete `ExecutableMissing` and `ReconvertInProgress` (their texts are now the exceptions' messages), and add before the closing brace:

```csharp
    [LoggerMessage(Level = LogLevel.Information, Message = "{Line}")]
    public static partial void GameListed(this ILogger logger, string line);

    [LoggerMessage(Level = LogLevel.Information, Message = "No games. Add one with: ps5kitsune add <folder>")]
    public static partial void NoGames(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Nothing was removed.")]
    public static partial void NothingRemoved(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Name} ({TitleId}) is removed: {Directory} is deleted.")]
    public static partial void GameRemoved(this ILogger logger, string name, string titleId, string directory);
```

- [ ] **Step 3: Write list and remove**

Create `src/PS5Kitsune.Cli/ListCommand.cs`:

```csharp
using System.CommandLine;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Cli;

internal static class ListCommand
{
    public static Command Create(Option<DirectoryInfo?> rootOption)
    {
        var command = new Command("list", "List the added games: verdict, whether they need a reconvert or are running, and when they were last played.");
        command.SetAction((parseResult, _) => CommandHost.RunAsync(parseResult.GetValue(rootOption), "list", (services, logger) =>
        {
            var installer = services.GetRequiredService<AnyPS5Installer>();
            installer.MigrateLegacyInstall();
            var install = installer.ReadRecord();
            var games = services.GetRequiredService<GamePlayer>().List();
            if (games.Count == 0)
            {
                logger.NoGames();
            }

            foreach (var game in games)
            {
                var line = Line(game, install);
                logger.GameListed(line);
            }

            return Task.FromResult(0);
        }));
        return command;
    }

    private static string Line(LibraryGame game, AnyPS5InstallRecord? install)
    {
        var record = game.Record;
        var verdict = record.Audit.Verdict switch
        {
            AuditVerdict.Green => "green",
            AuditVerdict.Orange => "orange",
            _ => "red",
        };
        List<string> fields = [record.TitleId, record.Name, verdict];
        if (install is not null && GameLibrary.NeedsReconversion(record, install))
        {
            fields.Add("reconvert");
        }

        if (game.Running is { } running)
        {
            fields.Add(string.Create(CultureInfo.InvariantCulture, $"running (PID {running.ProcessId})"));
        }

        fields.Add(record.LastPlayedAt is { } played
            ? string.Create(CultureInfo.InvariantCulture, $"played {played.LocalDateTime:yyyy-MM-dd HH:mm}")
            : "never played");
        return string.Join("  ", fields);
    }
}
```

Create `src/PS5Kitsune.Cli/RemoveCommand.cs`:

```csharp
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Cli;

internal static class RemoveCommand
{
    public static Command Create(Option<DirectoryInfo?> rootOption)
    {
        var titleIdArgument = new Argument<string>("titleId") { Description = "Title ID of an added game, for example PPSA99666." };
        var yesOption = new Option<bool>("--yes") { Description = "Delete without asking." };
        var command = new Command("remove", "Delete an added game and its folder, saves included. Refused while the game runs.") { titleIdArgument, yesOption };
        command.SetAction((parseResult, _) => CommandHost.RunAsync(parseResult.GetValue(rootOption), "remove", (services, logger) =>
        {
            var titleId = parseResult.GetRequiredValue(titleIdArgument);
            var (game, record) = services.GetRequiredService<GameLibrary>().Find(titleId);
            var name = record.Name;
            if (!parseResult.GetValue(yesOption) && !Confirm(name, GameCleaner.HasSaves(game)))
            {
                logger.NothingRemoved();
                return Task.FromResult(1);
            }

            // A running or busy game throws: CommandHost prints the message and exits with 1.
            services.GetRequiredService<GameCleaner>().Remove(titleId);
            var directory = game.Root;
            logger.GameRemoved(name, titleId, directory);
            return Task.FromResult(0);
        }));
        return command;
    }

    /// <summary>Anything but y or yes, including no input (redirected stdin), keeps the game.</summary>
    private static bool Confirm(string name, bool hasSaves)
    {
        var saves = hasSaves ? " Its saves in data\\ are deleted too." : "";
        Console.Write($"Delete {name} and its folder?{saves} [y/N] ");
        var answer = Console.ReadLine()?.Trim();
        return string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase) || string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase);
    }
}
```

In `src/PS5Kitsune.Cli/Program.cs`, add `ListCommand.Create(rootOption),` and `RemoveCommand.Create(rootOption),` after `RunCommand.Create(rootOption),`.

- [ ] **Step 4: Build, test, and try the commands**

Run: `dotnet build PS5Kitsune.slnx -c Release` — Expected: 0 warnings, 0 errors.
Run: `dotnet test --solution PS5Kitsune.slnx -c Release` — Expected: 0 failed.
Run, with an empty folder in the scratchpad as `<root>`: `dotnet run --project src/PS5Kitsune.Cli -c Release -- list --root <root>`
Expected: "No games. Add one with: ps5kitsune add <folder>", exit code 0.
Run: `dotnet run --project src/PS5Kitsune.Cli -c Release -- remove PPSA99666 --yes --root <root>`
Expected: an error naming PPSA99666 (no such game), exit code 1.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Cli
git commit -m "Run through GamePlayer, add list and remove to the CLI

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 6: App — the Game page

**Files:**
- Create: `src/PS5Kitsune.App/ViewModels/GameViewModel.cs`, `src/PS5Kitsune.App/ViewModels/LaunchErrors.cs`, `src/PS5Kitsune.App/Views/GameView.axaml`, `src/PS5Kitsune.App/Views/GameView.axaml.cs`
- Modify: `src/PS5Kitsune.App/Navigation/IPages.cs`, `src/PS5Kitsune.App/Shell/PageFactory.cs`, `src/PS5Kitsune.App/Views/MainWindow.axaml`
- Modify: `tests/PS5Kitsune.App.Tests/PS5Kitsune.App.Tests.csproj`, `tests/PS5Kitsune.App.Tests/Fakes.cs`, `tests/PS5Kitsune.App.Tests/PageFactoryTests.cs`
- Create: `tests/PS5Kitsune.App.Tests/GameViewModelTests.cs`

**Interfaces:**
- Consumes: `GamePlayer`, `PlayingGame`, `RunningGames`, `GameCleaner`, the exceptions, `FakeGameLauncher`, `FakeLaunchedGame` (Task 3); `GameLock` (Task 1); `LogTail.FollowAsync`; `PathOpener`.
- Produces:
  - `IPages.Library()` (until Task 7, `PageFactory.Library()` returns `Home()`); `IPages.Game(PlayingGame playing, bool firstLaunch)`.
  - `GameViewModel(PlayingGame playing, bool firstLaunch, GamePlayer player, IPlatform platform, IUiDispatcher ui, Navigator navigator, IPages pages) : ObservableObject, INavigationAware` with `Title`, `StatusText`, `IsRunning`, `HasExited`, `FirstLaunchHint`, `Message`, `Log`, `Following`, `StopCommand`, `LaunchAgainCommand`, `BackToLibraryCommand`, `OpenFolderCommand`, and `const int KeptLines = 500`.
  - `internal static class LaunchErrors { string? Describe(Exception exception, string name); }`.
  - The App test helper `TestLibrary`; `FakePages.Library()` returns `"library"`, and `FakePages.Game(...)` returns `"game:{titleId}"`.

- [ ] **Step 1: Share the fake launcher and add the test helpers**

In `tests/PS5Kitsune.App.Tests/PS5Kitsune.App.Tests.csproj`, add to the `ItemGroup` that links `TempDirectory.cs`:

```xml
    <Compile Include="..\PS5Kitsune.Core.Tests\Fakes\FakeGameLauncher.cs" Link="FakeGameLauncher.cs" />
```

In `tests/PS5Kitsune.App.Tests/Fakes.cs`:
- add `using PS5Kitsune.Core.Tests;` and `using PS5Kitsune.Core.Tests.Fakes;`;
- add to `FakePages`:

```csharp
    public object Library() => "library";

    public object Game(PlayingGame playing, bool firstLaunch) => $"game:{playing.Record.TitleId}";
```

- add at the end of the file:

```csharp
/// <summary>A real library on a temp root with a fake launcher; games are written as an add leaves them (game.json and the executable).</summary>
internal sealed class TestLibrary : IDisposable
{
    public static readonly AnyPS5InstallRecord Release =
        new(1, "test-profile", AnyPS5Mode.Release, "v0.1.2", null, null, new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));

    private readonly TempDirectory _temp = new();

    public TestLibrary()
    {
        Layout = new DataLayout(_temp.Combine("root"));
        Library = new GameLibrary(Layout, Platform, TimeProvider.System, NullLogger<GameLibrary>.Instance);
        Running = new RunningGames(Launcher, NullLogger<RunningGames>.Instance);
        Installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, Release);
        Player = new GamePlayer(Library, Installer, Launcher, Running);
        Cleaner = new GameCleaner(Library, Running, NullLogger<GameCleaner>.Instance);
    }

    public DataLayout Layout { get; }

    public TestPlatform Platform { get; } = new();

    public FakeGameLauncher Launcher { get; } = new();

    public FakeInstaller Installer { get; } = new();

    public GameLibrary Library { get; }

    public RunningGames Running { get; }

    public GamePlayer Player { get; }

    public GameCleaner Cleaner { get; }

    public GameLayout Game(string titleId) => new(Layout.GameDirectory(titleId), titleId, Platform);

    public async Task<GameLayout> AddAsync(string titleId, string name, ConversionInfo? convertedWith = null, DateTimeOffset? lastPlayedAt = null)
    {
        var game = Game(titleId);
        Directory.CreateDirectory(game.Root);
        await File.WriteAllTextAsync(game.Executable, "exe");
        await GameRecordStore.WriteAsync(game.RecordFile, new GameRecord
        {
            SchemaVersion = GameRecord.CurrentSchemaVersion,
            TitleId = titleId,
            Name = name,
            Executable = titleId + ".exe",
            ConvertedWith = convertedWith ?? new ConversionInfo("test-profile", "release", "v0.1.2", null, false),
            Audit = new AuditRecord(361, 3, 0, AuditVerdict.Orange),
            AddedAt = DateTimeOffset.UnixEpoch,
            LastPlayedAt = lastPlayedAt,
        }, CancellationToken.None);
        return game;
    }

    public void Dispose()
    {
        // Ends every fake game, so no test leaves a wait running.
        foreach (var game in Launcher.Games.Values)
        {
            game.Exit(0);
        }

        _temp.Dispose();
    }
}
```

- [ ] **Step 2: Write the failing tests**

Create `tests/PS5Kitsune.App.Tests/GameViewModelTests.cs`:

```csharp
using System.ComponentModel;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.App.Tests;

public sealed class GameViewModelTests : IDisposable
{
    private readonly TestLibrary _library = new();
    private readonly Navigator _navigator = new();

    private async Task<(GameViewModel Page, FakeLaunchedGame Process)> LaunchAsync(bool firstLaunch = true)
    {
        await _library.AddAsync("PPSA99666", "DOOM");
        var playing = await _library.Player.LaunchAsync("PPSA99666", TestContext.Current.CancellationToken);
        var page = new GameViewModel(playing, firstLaunch, _library.Player, _library.Platform, new InlineDispatcher(), _navigator, new FakePages());
        return (page, (FakeLaunchedGame)playing.Process);
    }

    private static Task FinishedAsync(GameViewModel page) => page.Following.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

    [Fact]
    public async Task A_running_game_shows_its_pid_and_the_first_launch_hint()
    {
        var (page, _) = await LaunchAsync();

        Assert.Equal("DOOM", page.Title);
        Assert.Equal("Running (PID 4812)", page.StatusText);
        Assert.True(page.IsRunning);
        Assert.Equal("The first launch may stutter while shaders are prepared.", page.FirstLaunchHint);
        Assert.True(page.StopCommand.CanExecute(null));
        Assert.False(page.LaunchAgainCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_later_launch_shows_no_hint()
    {
        var (page, _) = await LaunchAsync(firstLaunch: false);

        Assert.Null(page.FirstLaunchHint);
    }

    [Fact]
    public async Task Follows_the_log_then_shows_the_exit_code_and_forgets_the_game()
    {
        var (page, process) = await LaunchAsync();
        await File.AppendAllTextAsync(process.LogFile, "line 1\nline 2\n", TestContext.Current.CancellationToken);
        process.Exit(3);

        page.OnNavigatedTo();
        await FinishedAsync(page);

        Assert.Equal(["line 1", "line 2"], page.Log);
        Assert.Equal("Exited with code 3", page.StatusText);
        Assert.False(page.IsRunning);
        Assert.True(page.HasExited);
        Assert.False(File.Exists(_library.Game("PPSA99666").RunningRecord));
        Assert.True(page.LaunchAgainCommand.CanExecute(null));
    }

    [Fact]
    public async Task Keeps_the_last_500_lines()
    {
        var (page, process) = await LaunchAsync();
        await File.WriteAllLinesAsync(process.LogFile, Enumerable.Range(0, 600).Select(index => $"line {index}"), TestContext.Current.CancellationToken);
        process.Exit(0);

        page.OnNavigatedTo();
        await FinishedAsync(page);

        Assert.Equal(GameViewModel.KeptLines, page.Log.Count);
        Assert.Equal("line 100", page.Log[0]);
        Assert.Equal("line 599", page.Log[^1]);
    }

    [Fact]
    public async Task Stop_ends_the_game()
    {
        var (page, _) = await LaunchAsync();
        page.OnNavigatedTo();

        await page.StopCommand.ExecuteAsync(null);
        await FinishedAsync(page);

        Assert.Equal("Exited with code 1", page.StatusText);
    }

    [Fact]
    public async Task A_failed_stop_is_shown_and_the_game_stays_running()
    {
        var (page, process) = await LaunchAsync();
        process.StopFailure = new Win32Exception(5, "Access is denied");
        page.OnNavigatedTo();

        await page.StopCommand.ExecuteAsync(null);

        Assert.Equal("Could not stop DOOM: Access is denied", page.Message);
        Assert.True(page.IsRunning);
        Assert.True(File.Exists(_library.Game("PPSA99666").RunningRecord));
    }

    [Fact]
    public async Task Leaving_goes_to_the_library_and_keeps_the_game_running_and_recorded()
    {
        var (page, process) = await LaunchAsync();
        page.OnNavigatedTo();

        await page.BackToLibraryCommand.ExecuteAsync(null);

        Assert.Equal("library", _navigator.CurrentPage);
        Assert.False(process.HasExited);
        Assert.True(File.Exists(_library.Game("PPSA99666").RunningRecord));
    }

    [Fact]
    public async Task Launch_again_starts_a_new_process_and_follows_it()
    {
        var (page, process) = await LaunchAsync();
        await File.AppendAllTextAsync(process.LogFile, "old\n", TestContext.Current.CancellationToken);
        process.Exit(0);
        page.OnNavigatedTo();
        await FinishedAsync(page);

        await page.LaunchAgainCommand.ExecuteAsync(null);

        Assert.Equal(2, _library.Launcher.Launches.Count);
        Assert.Equal("Running (PID 4813)", page.StatusText);
        Assert.Empty(page.Log);
        Assert.Null(page.FirstLaunchHint);
    }

    [Fact]
    public async Task A_refused_launch_again_is_shown()
    {
        var (page, process) = await LaunchAsync();
        process.Exit(0);
        page.OnNavigatedTo();
        await FinishedAsync(page);

        using (GameLock.TryAcquireNow(_library.Game("PPSA99666")))
        {
            await page.LaunchAgainCommand.ExecuteAsync(null);
        }

        Assert.Equal("Another PS5Kitsune action is using DOOM. Try again when it finishes.", page.Message);
        Assert.Equal("Exited with code 0", page.StatusText);
    }

    [Fact]
    public async Task Open_folder_opens_the_game_folder()
    {
        var (page, _) = await LaunchAsync();

        page.OpenFolderCommand.Execute(null);

        Assert.Equal([_library.Game("PPSA99666").Root], _library.Platform.OpenedPaths);
    }

    public void Dispose() => _library.Dispose();
}
```

In `tests/PS5Kitsune.App.Tests/PageFactoryTests.cs`, add `using PS5Kitsune.Core.Library;` and `using PS5Kitsune.Core.Tests.Fakes;`, and at the end of `Builds_every_page_from_a_real_session`:

```csharp
        var game = new GameLayout(_temp.Combine("root", "games", "PPSA99666"), "PPSA99666", platform);
        var process = new FakeLaunchedGame(4812, _temp.Combine("game.log"), DateTimeOffset.UnixEpoch);
        Assert.IsType<GameViewModel>(pages.Game(new PlayingGame(game, TestGameData.Added().Record, process), firstLaunch: false));
```

- [ ] **Step 3: Run the tests to verify they fail to compile**

Run: `dotnet build tests/PS5Kitsune.App.Tests -c Release`
Expected: FAIL, `CS0246: The type or namespace name 'GameViewModel' could not be found`.

- [ ] **Step 4: Add the pages to the navigation**

In `src/PS5Kitsune.App/Navigation/IPages.cs`, add `using PS5Kitsune.Core.Library;` and, after `ReconvertResult`:

```csharp
    /// <summary>The added games; the first page once AnyPS5 is installed and a game is added.</summary>
    object Library();

    /// <summary>A running game. Takes ownership of <paramref name="playing"/>'s process handle.</summary>
    /// <param name="firstLaunch">The game was never played before: shaders are prepared, so it may stutter.</param>
    object Game(PlayingGame playing, bool firstLaunch);
```

In `src/PS5Kitsune.App/Shell/PageFactory.cs`, add after `ReconvertResult`:

```csharp
    // Task 7 replaces this with the Library page.
    public object Library() => Home();

    public object Game(PlayingGame playing, bool firstLaunch) =>
        new GameViewModel(playing, firstLaunch, Session.Get<GamePlayer>(), platform, ui, navigator, this);
```

- [ ] **Step 5: Write the launch errors and the view model**

Create `src/PS5Kitsune.App/ViewModels/LaunchErrors.cs`:

```csharp
using System.ComponentModel;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.App.ViewModels;

/// <summary>The line the Library and the Game page show when a launch is refused or fails.</summary>
internal static class LaunchErrors
{
    /// <returns>Null for an exception a launch is not expected to throw: the caller lets it through.</returns>
    public static string? Describe(Exception exception, string name) => exception switch
    {
        GameRunningException => $"{name} is already running.",
        GameBusyException => $"Another PS5Kitsune action is using {name}. Try again when it finishes.",
        GameExecutableMissingException => $"The executable is missing: Reconvert {name}.",
        AnyPS5NotInstalledException => "AnyPS5 is not installed. Install it from Home first.",
        GameNotFoundException or InvalidGameRecordException or IOException or UnauthorizedAccessException or Win32Exception or PlatformNotSupportedException =>
            $"Could not launch {name}: {exception.Message}",
        _ => null,
    };
}
```

Create `src/PS5Kitsune.App/ViewModels/GameViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.App.ViewModels;

/// <summary>A running game: its log followed live, Stop, then its exit code. Leaving the page never stops the game.</summary>
public sealed partial class GameViewModel : ObservableObject, INavigationAware
{
    /// <summary>Log lines kept on screen; the file keeps them all.</summary>
    public const int KeptLines = 500;

    private readonly GamePlayer _player;
    private readonly IPlatform _platform;
    private readonly IUiDispatcher _ui;
    private readonly Navigator _navigator;
    private readonly IPages _pages;
    private PlayingGame _playing;
    private CancellationTokenSource? _following;

    public GameViewModel(PlayingGame playing, bool firstLaunch, GamePlayer player, IPlatform platform, IUiDispatcher ui, Navigator navigator, IPages pages)
    {
        _playing = playing;
        _player = player;
        _platform = platform;
        _ui = ui;
        _navigator = navigator;
        _pages = pages;
        Title = playing.Record.Name;
        FirstLaunchHint = firstLaunch ? "The first launch may stutter while shaders are prepared." : null;
        ShowRunning();
    }

    public string Title { get; }

    public ObservableCollection<string> Log { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExited))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand), nameof(LaunchAgainCommand))]
    public partial bool IsRunning { get; private set; }

    public bool HasExited => !IsRunning;

    /// <summary>"Running (PID n)", then "Exited with code n".</summary>
    [ObservableProperty]
    public partial string StatusText { get; private set; } = "";

    [ObservableProperty]
    public partial string? FirstLaunchHint { get; private set; }

    /// <summary>Why the last action failed; null otherwise.</summary>
    [ObservableProperty]
    public partial string? Message { get; private set; }

    /// <summary>Completes when the page stops following the current process: it exited, or the page was left.</summary>
    public Task Following { get; private set; } = Task.CompletedTask;

    public void OnNavigatedTo()
    {
        if (_following is null)
        {
            StartFollowing();
        }
    }

    private void ShowRunning()
    {
        IsRunning = true;
        StatusText = string.Create(CultureInfo.InvariantCulture, $"Running (PID {_playing.Process.ProcessId})");
    }

    private void StartFollowing()
    {
        _following = new CancellationTokenSource();
        Following = FollowAsync(_playing, _following.Token);
    }

    private async Task FollowAsync(PlayingGame playing, CancellationToken cancellationToken)
    {
        var exit = playing.Process.WaitForExitAsync(cancellationToken);
        try
        {
            try
            {
                await Task.Run(
                    () => LogTail.FollowAsync(playing.Process.LogFile, line => _ui.Post(() => AddLine(line)), exit, cancellationToken),
                    cancellationToken).ConfigureAwait(true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Message = $"Cannot follow the log: {exception.Message}";
            }

            var exitCode = await exit.ConfigureAwait(true);
            _player.Forget(playing);
            IsRunning = false;
            StatusText = string.Create(CultureInfo.InvariantCulture, $"Exited with code {exitCode}");
        }
        catch (OperationCanceledException)
        {
            // The page was left: the game keeps running and stays recorded.
        }
    }

    private void AddLine(string line)
    {
        Log.Add(line);
        while (Log.Count > KeptLines)
        {
            Log.RemoveAt(0);
        }
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private async Task StopAsync()
    {
        Message = null;
        try
        {
            await _playing.Process.StopAsync().ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Message = $"Could not stop {Title}: {exception.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(HasExited))]
    private async Task LaunchAgainAsync()
    {
        Message = null;
        var titleId = _playing.Record.TitleId;
        PlayingGame playing;
        try
        {
            playing = await Task.Run(() => _player.LaunchAsync(titleId, CancellationToken.None)).ConfigureAwait(true);
        }
        catch (Exception exception) when (LaunchErrors.Describe(exception, Title) is { } message)
        {
            Message = message;
            return;
        }

        _playing.Process.Dispose();
        _playing = playing;
        Log.Clear();
        FirstLaunchHint = null;
        ShowRunning();
        StartFollowing();
    }

    [RelayCommand]
    private async Task BackToLibraryAsync()
    {
        if (_following is { } following)
        {
            await following.CancelAsync().ConfigureAwait(true);
        }

        await Following.ConfigureAwait(true);
        _playing.Process.Dispose();
        _navigator.GoHome(_pages.Library());
    }

    [RelayCommand]
    private void OpenFolder() => Message = PathOpener.TryOpen(_platform, _playing.Game.Root);
}
```

- [ ] **Step 6: Write the view**

Create `src/PS5Kitsune.App/Views/GameView.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:PS5Kitsune.App.ViewModels"
             xmlns:sys="using:System"
             x:Class="PS5Kitsune.App.Views.GameView"
             x:DataType="vm:GameViewModel">
  <DockPanel>
    <StackPanel DockPanel.Dock="Top" Spacing="6">
      <TextBlock Text="{Binding Title}" FontSize="20" FontWeight="SemiBold" />
      <TextBlock Text="{Binding StatusText}" />
      <TextBlock Text="{Binding FirstLaunchHint}" IsVisible="{Binding FirstLaunchHint, Converter={x:Static ObjectConverters.IsNotNull}}"
                 Opacity="0.85" TextWrapping="Wrap" />
      <TextBlock Text="{Binding Message}" IsVisible="{Binding Message, Converter={x:Static ObjectConverters.IsNotNull}}"
                 Foreground="#C42B1C" TextWrapping="Wrap" />
    </StackPanel>

    <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right" Margin="0,12,0,0">
      <Button Content="← Library" Command="{Binding BackToLibraryCommand}" />
      <Button Content="Open folder" Command="{Binding OpenFolderCommand}" />
      <Button Content="Stop" Command="{Binding StopCommand}" IsVisible="{Binding IsRunning}" />
      <Button Content="Launch again" Classes="accent" Command="{Binding LaunchAgainCommand}" IsVisible="{Binding HasExited}" />
    </StackPanel>

    <Border Margin="0,12,0,0" Padding="8" CornerRadius="4" Background="#14808080">
      <ScrollViewer x:Name="LogScroller">
        <ItemsControl ItemsSource="{Binding Log}">
          <ItemsControl.ItemTemplate>
            <DataTemplate x:DataType="sys:String">
              <TextBlock Text="{Binding}" FontFamily="Cascadia Mono,Consolas,monospace" FontSize="12" TextWrapping="Wrap" />
            </DataTemplate>
          </ItemsControl.ItemTemplate>
        </ItemsControl>
      </ScrollViewer>
    </Border>
  </DockPanel>
</UserControl>
```

Create `src/PS5Kitsune.App/Views/GameView.axaml.cs`:

```csharp
using System.Collections.Specialized;
using Avalonia.Controls;
using PS5Kitsune.App.ViewModels;

namespace PS5Kitsune.App.Views;

public sealed partial class GameView : UserControl
{
    private GameViewModel? _viewModel;

    public GameView() => InitializeComponent();

    /// <summary>Keeps the newest log line in view, and drops the subscription when the page changes.</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.Log.CollectionChanged -= OnLogChanged;
        }

        _viewModel = DataContext as GameViewModel;
        if (_viewModel is not null)
        {
            _viewModel.Log.CollectionChanged += OnLogChanged;
        }
    }

    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e) => LogScroller.ScrollToEnd();
}
```

In `src/PS5Kitsune.App/Views/MainWindow.axaml`, add after the `AddResultViewModel` template:

```xml
    <DataTemplate DataType="vm:GameViewModel">
      <views:GameView />
    </DataTemplate>
```

- [ ] **Step 7: Run the tests to verify they pass**

Close any running `PS5Kitsune.exe` first.
Run: `dotnet build PS5Kitsune.slnx -c Release` — Expected: 0 warnings, 0 errors.
Run: `dotnet test --project tests/PS5Kitsune.App.Tests -c Release` — Expected: 0 failed.

- [ ] **Step 8: Commit**

```bash
git add src/PS5Kitsune.App tests/PS5Kitsune.App.Tests
git commit -m "Add the Game page: live log, Stop, exit code, launch again

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 7: App — the Library page and the start page

**Files:**
- Create: `src/PS5Kitsune.App/ViewModels/LibraryViewModel.cs`, `src/PS5Kitsune.App/ViewModels/GameTileViewModel.cs`, `src/PS5Kitsune.App/Views/LibraryView.axaml`, `src/PS5Kitsune.App/Views/LibraryView.axaml.cs`
- Modify: `src/PS5Kitsune.App/Shell/PageFactory.cs`, `src/PS5Kitsune.App/App.axaml.cs`, `src/PS5Kitsune.App/Views/MainWindow.axaml`
- Modify: `src/PS5Kitsune.App/ViewModels/HomeViewModel.cs`, `src/PS5Kitsune.App/Views/HomeView.axaml`; delete `src/PS5Kitsune.App/ViewModels/GameToReconvert.cs`
- Modify: `src/PS5Kitsune.App/ViewModels/ReconvertViewModel.cs`, `src/PS5Kitsune.App/Views/ReconvertView.axaml`, `src/PS5Kitsune.App/ViewModels/AddResultViewModel.cs`, `src/PS5Kitsune.App/Views/AddResultView.axaml`
- Create: `tests/PS5Kitsune.App.Tests/LibraryViewModelTests.cs`
- Modify: `tests/PS5Kitsune.App.Tests/HomeViewModelTests.cs`, `ReconvertViewModelTests.cs`, `AddResultViewModelTests.cs`, `PageFactoryTests.cs`

**Interfaces:**
- Consumes: `GamePlayer.List/LaunchAsync`, `RunningGames.Attach`, `GameCleaner.Remove/ClearShaderCache/ShaderCacheSize/HasSaves`, `LibraryGame`, `PlayingGame` (Task 3); `GameLibrary.HasGames` (Task 1), `GameLibrary.NeedsReconversion`; `IPages.Library/Game`, `LaunchErrors.Describe`, `TestLibrary` (Task 6).
- Produces:
  - `LibraryViewModel(GamePlayer player, GameCleaner cleaner, RunningGames running, IAnyPS5Installer installer, IPlatform platform, IDialogService dialogs, Navigator navigator, IPages pages) : ObservableObject, INavigationAware`, with:
    - `Games`, `Search`, `IsLoading`, `IsEmpty`, `Message`, `RefreshAsync()`;
    - the commands `OpenCommand`, `LaunchCommand`, `OpenFolderCommand`, `ViewLastLogCommand`, `ReconvertCommand`, `ClearShaderCacheCommand` and `RemoveCommand`, each taking a `GameTileViewModel`, plus `AddGameCommand` and `HomeCommand`;
    - `static bool OpensFirst(IAnyPS5Installer installer, GameLibrary library)`.
  - `GameTileViewModel(LibraryGame entry, bool needsReconvert, LibraryViewModel owner)` with `Entry`, `Owner`, `TitleId`, `Name`, `IconPath`, `IsGreen`, `IsOrange`, `IsRed`, `NeedsReconvert`, `IsRunning`, `LastPlayedAt`, `HasLog`.
  - `PageFactory.Library()` builds the Library page, and `PageFactory.Start()` picks the first page.
  - Home: adds `HasGames` and `LibraryCommand`, and removes `GamesToReconvert`, `HasGamesToReconvert`, `ReconvertCommand` and `GameToReconvert`.
  - `AddResultViewModel.BackToLibraryCommand` replaces `BackHomeCommand`.

- [ ] **Step 1: Write the failing tests**

Create `tests/PS5Kitsune.App.Tests/LibraryViewModelTests.cs`:

```csharp
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.App.Tests;

public sealed class LibraryViewModelTests : IDisposable
{
    private readonly TestLibrary _library = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly Navigator _navigator = new();

    private async Task<LibraryViewModel> CreateAsync()
    {
        var page = new LibraryViewModel(
            _library.Player, _library.Cleaner, _library.Running, _library.Installer, _library.Platform, _dialogs, _navigator, new FakePages());
        await page.RefreshAsync();
        return page;
    }

    private static GameTileViewModel Tile(LibraryViewModel page, string name) => page.Games.Single(tile => tile.Name == name);

    private Task StartAsync(string titleId) => _library.Player.LaunchAsync(titleId, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Lists_the_games_last_played_first_with_their_badges()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        await _library.AddAsync("PPSA00002", "Bravo", lastPlayedAt: new DateTimeOffset(2026, 10, 20, 20, 0, 0, TimeSpan.Zero));
        await _library.AddAsync("PPSA00003", "Charlie", lastPlayedAt: new DateTimeOffset(2026, 10, 22, 20, 0, 0, TimeSpan.Zero));
        await _library.AddAsync("PPSA00004", "Delta", new ConversionInfo("test-profile", "build", "3fd1cc3", new string('3', 40), false));

        var page = await CreateAsync();

        Assert.Equal(["Charlie", "Bravo", "Alpha", "Delta"], page.Games.Select(tile => tile.Name));
        Assert.True(Tile(page, "Delta").NeedsReconvert);
        Assert.False(Tile(page, "Alpha").NeedsReconvert);
        Assert.True(Tile(page, "Alpha").IsOrange);
        Assert.Equal("PPSA00001", Tile(page, "Alpha").TitleId);
        Assert.False(page.IsEmpty);
    }

    [Fact]
    public async Task The_search_filters_by_name()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        await _library.AddAsync("PPSA00003", "Charlie");
        var page = await CreateAsync();

        page.Search = "CHAR";

        Assert.Equal(["Charlie"], page.Games.Select(tile => tile.Name));
        page.Search = "";
        Assert.Equal(2, page.Games.Count);
    }

    [Fact]
    public async Task An_empty_library_says_so()
    {
        var page = await CreateAsync();

        Assert.True(page.IsEmpty);
        Assert.Empty(page.Games);
    }

    [Fact]
    public async Task A_running_game_is_badged()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        await StartAsync("PPSA00001");

        var page = await CreateAsync();

        Assert.True(Tile(page, "Alpha").IsRunning);
    }

    [Fact]
    public async Task Clicking_a_game_launches_it_and_opens_its_page()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        var page = await CreateAsync();

        await page.OpenCommand.ExecuteAsync(Tile(page, "Alpha"));

        Assert.Single(_library.Launcher.Launches);
        Assert.Equal("game:PPSA00001", _navigator.CurrentPage);
    }

    [Fact]
    public async Task Clicking_a_running_game_opens_its_page_without_launching_it_again()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        await StartAsync("PPSA00001");
        var page = await CreateAsync();

        await page.OpenCommand.ExecuteAsync(Tile(page, "Alpha"));

        Assert.Single(_library.Launcher.Launches);
        Assert.Equal("game:PPSA00001", _navigator.CurrentPage);
    }

    [Fact]
    public async Task A_game_that_stopped_since_the_refresh_says_so()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        await StartAsync("PPSA00001");
        var page = await CreateAsync();
        _library.Launcher.Games[4812].Exit(0);

        await page.OpenCommand.ExecuteAsync(Tile(page, "Alpha"));

        Assert.Equal("Alpha is no longer running.", page.Message);
        Assert.False(Tile(page, "Alpha").IsRunning);
    }

    [Fact]
    public async Task Launching_while_another_game_runs_offers_to_show_it()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        await _library.AddAsync("PPSA00002", "Bravo");
        await StartAsync("PPSA00002");
        var page = await CreateAsync();
        _dialogs.Confirm = true;

        await page.LaunchCommand.ExecuteAsync(Tile(page, "Alpha"));

        Assert.Equal(["Bravo is running. Show it?"], _dialogs.Confirmations);
        Assert.Single(_library.Launcher.Launches);
        Assert.Equal("game:PPSA00002", _navigator.CurrentPage);
    }

    [Fact]
    public async Task A_busy_game_says_why()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        var page = await CreateAsync();

        using (GameLock.TryAcquireNow(_library.Game("PPSA00001")))
        {
            await page.LaunchCommand.ExecuteAsync(Tile(page, "Alpha"));
        }

        Assert.Equal("Another PS5Kitsune action is using Alpha. Try again when it finishes.", page.Message);
        Assert.Null(_navigator.CurrentPage);
    }

    [Fact]
    public async Task A_missing_executable_suggests_a_reconvert()
    {
        var game = await _library.AddAsync("PPSA00001", "Alpha");
        File.Delete(game.Executable);
        var page = await CreateAsync();

        await page.LaunchCommand.ExecuteAsync(Tile(page, "Alpha"));

        Assert.Equal("The executable is missing: Reconvert Alpha.", page.Message);
    }

    [Fact]
    public async Task Reconvert_opens_the_reconverting_page_unless_the_game_runs()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        await _library.AddAsync("PPSA00002", "Bravo");
        await StartAsync("PPSA00002");
        var page = await CreateAsync();

        page.ReconvertCommand.Execute(Tile(page, "Bravo"));
        Assert.Equal("Close Bravo first.", page.Message);
        Assert.Null(_navigator.CurrentPage);

        page.ReconvertCommand.Execute(Tile(page, "Alpha"));
        Assert.Equal("reconvert:PPSA00001", _navigator.CurrentPage);
    }

    [Fact]
    public async Task Clearing_the_shader_cache_confirms_with_its_size()
    {
        var game = await _library.AddAsync("PPSA00001", "Alpha");
        Directory.CreateDirectory(Path.Combine(game.ShaderCache, "v1"));
        await File.WriteAllBytesAsync(Path.Combine(game.ShaderCache, "v1", "cache.bin"), new byte[1_572_864], TestContext.Current.CancellationToken);
        var page = await CreateAsync();
        _dialogs.Confirm = true;

        await page.ClearShaderCacheCommand.ExecuteAsync(Tile(page, "Alpha"));

        Assert.Equal(["Delete the shader cache of Alpha (1.5 MB)? The next launch will stutter while shaders are prepared again."], _dialogs.Confirmations);
        Assert.False(Directory.Exists(game.ShaderCache));
        Assert.Null(page.Message);
    }

    [Fact]
    public async Task No_shader_cache_asks_nothing()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        var page = await CreateAsync();

        await page.ClearShaderCacheCommand.ExecuteAsync(Tile(page, "Alpha"));

        Assert.Empty(_dialogs.Confirmations);
        Assert.Equal("Alpha has no shader cache.", page.Message);
    }

    [Fact]
    public async Task Removing_warns_about_saves_and_deletes_the_game()
    {
        var game = await _library.AddAsync("PPSA00001", "Alpha");
        Directory.CreateDirectory(game.Data);
        await File.WriteAllTextAsync(Path.Combine(game.Data, "save.dat"), "save", TestContext.Current.CancellationToken);
        var page = await CreateAsync();
        _dialogs.Confirm = true;

        await page.RemoveCommand.ExecuteAsync(Tile(page, "Alpha"));

        Assert.Equal([@"Delete Alpha and its folder? Its saves in data\ are deleted too."], _dialogs.Confirmations);
        Assert.False(Directory.Exists(game.Root));
        Assert.True(page.IsEmpty);
    }

    [Fact]
    public async Task A_cancelled_removal_keeps_the_game()
    {
        var game = await _library.AddAsync("PPSA00001", "Alpha");
        var page = await CreateAsync();

        await page.RemoveCommand.ExecuteAsync(Tile(page, "Alpha"));

        Assert.Equal(["Delete Alpha and its folder?"], _dialogs.Confirmations);
        Assert.True(File.Exists(game.RecordFile));
    }

    [Fact]
    public async Task View_last_log_opens_the_newest_log()
    {
        var game = await _library.AddAsync("PPSA00001", "Alpha");
        await _library.AddAsync("PPSA00002", "Bravo");
        Directory.CreateDirectory(game.Logs);
        await File.WriteAllTextAsync(Path.Combine(game.Logs, "20261020-200000.log"), "", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(game.Logs, "20261022-200000.log"), "", TestContext.Current.CancellationToken);
        var page = await CreateAsync();

        Assert.False(page.ViewLastLogCommand.CanExecute(Tile(page, "Bravo")));
        page.ViewLastLogCommand.Execute(Tile(page, "Alpha"));

        Assert.Equal([Path.Combine(game.Logs, "20261022-200000.log")], _library.Platform.OpenedPaths);
    }

    [Fact]
    public async Task Home_and_add_a_game_navigate()
    {
        var page = await CreateAsync();

        page.AddGameCommand.Execute(null);
        Assert.Equal("choose", _navigator.CurrentPage);

        page.HomeCommand.Execute(null);
        Assert.Equal("home", _navigator.CurrentPage);
    }

    [Fact]
    public async Task The_library_opens_first_once_installed_with_a_game()
    {
        Assert.False(LibraryViewModel.OpensFirst(_library.Installer, _library.Library));

        await _library.AddAsync("PPSA00001", "Alpha");
        Assert.True(LibraryViewModel.OpensFirst(_library.Installer, _library.Library));

        _library.Installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.NotInstalled, null);
        Assert.False(LibraryViewModel.OpensFirst(_library.Installer, _library.Library));
    }

    public void Dispose() => _library.Dispose();
}
```

In `tests/PS5Kitsune.App.Tests/HomeViewModelTests.cs`, delete the tests `Lists_the_games_converted_with_another_install`, `No_game_to_reconvert_hides_the_section` and `Without_an_install_no_game_is_listed_and_reconvert_is_disabled`. Then add these two tests, which reuse the existing `WriteGameAsync` helper:

```csharp
    [Fact]
    public async Task The_library_button_opens_the_library_once_a_game_is_added()
    {
        _installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, Record);
        await WriteGameAsync("PPSA99666", "DOOM", new ConversionInfo("test-profile", "release", "v0.1.2", null, false));

        var viewModel = await CreateAsync();

        Assert.True(viewModel.HasGames);
        Assert.True(viewModel.LibraryCommand.CanExecute(null));
        viewModel.LibraryCommand.Execute(null);
        Assert.Equal("library", _navigator.CurrentPage);
    }

    [Fact]
    public async Task Without_games_the_library_button_is_disabled()
    {
        _installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, Record);

        var viewModel = await CreateAsync();

        Assert.False(viewModel.HasGames);
        Assert.False(viewModel.LibraryCommand.CanExecute(null));
    }
```

In `tests/PS5Kitsune.App.Tests/ReconvertViewModelTests.cs`, rename `Cancelling_goes_home` to `Cancelling_goes_to_the_library` and expect `"library"` instead of `"home"`.

In `tests/PS5Kitsune.App.Tests/AddResultViewModelTests.cs`:
- rename `The_buttons_open_the_folder_add_another_or_go_home` to `The_buttons_open_the_folder_add_another_or_go_to_the_library`;
- replace `BackHomeCommand` with `BackToLibraryCommand`;
- expect `"library"` wherever the test expects `"home"`.

In `tests/PS5Kitsune.App.Tests/PageFactoryTests.cs`, add after the `ReconvertResult` assertion:

```csharp
        Assert.IsType<LibraryViewModel>(pages.Library());
        Assert.IsType<HomeViewModel>(pages.Start());
```

- [ ] **Step 2: Run the tests to verify they fail to compile**

Run: `dotnet build tests/PS5Kitsune.App.Tests -c Release`
Expected: FAIL, with `CS0246: The type or namespace name 'LibraryViewModel' could not be found` and `'HomeViewModel' does not contain a definition for 'LibraryCommand'`.

- [ ] **Step 3: Write the tile and the Library view model**

Create `src/PS5Kitsune.App/ViewModels/GameTileViewModel.cs`:

```csharp
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.App.ViewModels;

/// <summary>
/// A game in the Library, built on a worker thread (it looks at the logs folder). Its menu binds to the Library's commands through
/// <see cref="Owner"/>: a flyout is outside the tile's visual tree, so a $parent binding cannot reach the page.
/// </summary>
public sealed class GameTileViewModel(LibraryGame entry, bool needsReconvert, LibraryViewModel owner)
{
    public LibraryGame Entry { get; } = entry;

    public LibraryViewModel Owner { get; } = owner;

    public string TitleId => Entry.Record.TitleId;

    public string Name => Entry.Record.Name;

    /// <summary>icon0.png's full path; null when the game has none.</summary>
    public string? IconPath => Entry.Record.Icon is { } icon ? Path.Combine(Entry.Game.Root, icon) : null;

    public bool IsGreen => Entry.Record.Audit.Verdict == AuditVerdict.Green;

    public bool IsOrange => Entry.Record.Audit.Verdict == AuditVerdict.Orange;

    public bool IsRed => Entry.Record.Audit.Verdict == AuditVerdict.Red;

    public bool NeedsReconvert { get; } = needsReconvert;

    public bool IsRunning => Entry.Running is not null;

    public DateTimeOffset? LastPlayedAt => Entry.Record.LastPlayedAt;

    public bool HasLog { get; } = Directory.Exists(entry.Game.Logs) && Directory.EnumerateFiles(entry.Game.Logs, "*.log").Any();
}
```

Create `src/PS5Kitsune.App/ViewModels/LibraryViewModel.cs`:

```csharp
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.App.ViewModels;

/// <summary>The added games as tiles: launch in one click, the running game, and each game's actions. See the 7a spec, "Library page".</summary>
public sealed partial class LibraryViewModel(
    GamePlayer player, GameCleaner cleaner, RunningGames running, IAnyPS5Installer installer, IPlatform platform, IDialogService dialogs,
    Navigator navigator, IPages pages)
    : ObservableObject, INavigationAware
{
    private IReadOnlyList<GameTileViewModel> _all = [];
    private int _generation;

    /// <summary>The tiles matching <see cref="Search"/>, last played first.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<GameTileViewModel> Games { get; private set; } = [];

    [ObservableProperty]
    public partial string Search { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoading { get; private set; }

    /// <summary>No game is added (not: none matches the search).</summary>
    public bool IsEmpty => !IsLoading && _all.Count == 0;

    /// <summary>Why the last action failed; null otherwise.</summary>
    [ObservableProperty]
    public partial string? Message { get; private set; }

    /// <summary>The app opens on the Library once AnyPS5 is installed and a game is added; reads no game.json.</summary>
    public static bool OpensFirst(IAnyPS5Installer installer, GameLibrary library)
    {
        ArgumentNullException.ThrowIfNull(installer);
        ArgumentNullException.ThrowIfNull(library);
        return installer.ReadRecord() is not null && library.HasGames();
    }

    public void OnNavigatedTo() => _ = RefreshAsync();

    /// <summary>Reads the games on a worker thread; a result that arrives after a newer refresh started is dropped.</summary>
    public async Task RefreshAsync()
    {
        var generation = ++_generation;
        IsLoading = true;
        IReadOnlyList<GameTileViewModel> tiles;
        try
        {
            tiles = await Task.Run(Read).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (generation == _generation)
            {
                Message = $"Cannot read the library: {exception.Message}";
                IsLoading = false;
            }

            return;
        }

        if (generation != _generation)
        {
            return;
        }

        _all = tiles;
        ApplySearch();
        IsLoading = false;
        OnPropertyChanged(nameof(IsEmpty));
    }

    partial void OnSearchChanged(string value) => ApplySearch();

    private IReadOnlyList<GameTileViewModel> Read()
    {
        var install = installer.ReadRecord();
        return
        [
            .. player.List()
                .Select(game => new GameTileViewModel(game, install is not null && GameLibrary.NeedsReconversion(game.Record, install), this))
                .OrderByDescending(tile => tile.LastPlayedAt.HasValue)
                .ThenByDescending(tile => tile.LastPlayedAt)
                .ThenBy(tile => tile.Name, StringComparer.CurrentCultureIgnoreCase),
        ];
    }

    private void ApplySearch() =>
        Games = Search.Trim() is { Length: > 0 } text
            ? [.. _all.Where(tile => tile.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase))]
            : _all;

    /// <summary>A click on the tile: launches the game, or shows it when it runs.</summary>
    [RelayCommand]
    private Task OpenAsync(GameTileViewModel tile) => tile.IsRunning ? ShowAsync(tile) : LaunchAsync(tile);

    [RelayCommand]
    private async Task LaunchAsync(GameTileViewModel tile)
    {
        Message = null;
        if (tile.IsRunning)
        {
            await ShowAsync(tile).ConfigureAwait(true);
            return;
        }

        // One game at a time.
        if (_all.FirstOrDefault(other => other.IsRunning && other.TitleId != tile.TitleId) is { } other)
        {
            if (await dialogs.ConfirmAsync("A game is running", $"{other.Name} is running. Show it?", "Show it").ConfigureAwait(true))
            {
                await ShowAsync(other).ConfigureAwait(true);
            }

            return;
        }

        PlayingGame playing;
        try
        {
            playing = await Task.Run(() => player.LaunchAsync(tile.TitleId, CancellationToken.None)).ConfigureAwait(true);
        }
        catch (GameRunningException)
        {
            // Started elsewhere (the CLI) since the last refresh.
            await ShowAsync(tile).ConfigureAwait(true);
            return;
        }
        catch (Exception exception) when (LaunchErrors.Describe(exception, tile.Name) is { } message)
        {
            Message = message;
            return;
        }

        navigator.GoTo(pages.Game(playing, firstLaunch: tile.LastPlayedAt is null));
    }

    private async Task ShowAsync(GameTileViewModel tile)
    {
        var process = await Task.Run(() => running.Attach(tile.Entry.Game)).ConfigureAwait(true);
        if (process is null)
        {
            Message = $"{tile.Name} is no longer running.";
            await RefreshAsync().ConfigureAwait(true);
            return;
        }

        navigator.GoTo(pages.Game(new PlayingGame(tile.Entry.Game, tile.Entry.Record, process), firstLaunch: false));
    }

    [RelayCommand]
    private void OpenFolder(GameTileViewModel tile) => Message = PathOpener.TryOpen(platform, tile.Entry.Game.Root);

    private static bool CanViewLastLog(GameTileViewModel? tile) => tile?.HasLog == true;

    // Log names sort by date, so the greatest is the newest.
    [RelayCommand(CanExecute = nameof(CanViewLastLog))]
    private void ViewLastLog(GameTileViewModel tile) =>
        Message = PathOpener.TryRun(() => platform.OpenPath(Directory.GetFiles(tile.Entry.Game.Logs, "*.log").Max(StringComparer.Ordinal)!));

    [RelayCommand]
    private void Reconvert(GameTileViewModel tile)
    {
        Message = null;
        if (tile.IsRunning)
        {
            Message = $"Close {tile.Name} first.";
            return;
        }

        navigator.GoTo(pages.Reconvert(tile.TitleId, tile.Name));
    }

    [RelayCommand]
    private async Task ClearShaderCacheAsync(GameTileViewModel tile)
    {
        Message = null;
        var size = await Task.Run(() => GameCleaner.ShaderCacheSize(tile.Entry.Game)).ConfigureAwait(true);
        if (size == 0)
        {
            Message = $"{tile.Name} has no shader cache.";
            return;
        }

        var question = $"Delete the shader cache of {tile.Name} ({FormatSize(size)})? The next launch will stutter while shaders are prepared again.";
        if (!await dialogs.ConfirmAsync("Clear the shader cache?", question, "Delete").ConfigureAwait(true))
        {
            return;
        }

        try
        {
            await Task.Run(() => cleaner.ClearShaderCache(tile.TitleId)).ConfigureAwait(true);
        }
        catch (GameRunningException)
        {
            Message = $"Close {tile.Name} first.";
        }
        catch (GameBusyException)
        {
            Message = $"Another PS5Kitsune action is using {tile.Name}. Try again when it finishes.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Message = $"The shader cache of {tile.Name} may be partly cleared: {exception.Message}";
        }
    }

    [RelayCommand]
    private async Task RemoveAsync(GameTileViewModel tile)
    {
        Message = null;
        var hasSaves = await Task.Run(() => GameCleaner.HasSaves(tile.Entry.Game)).ConfigureAwait(true);
        var question = hasSaves ? $"Delete {tile.Name} and its folder? Its saves in data\\ are deleted too." : $"Delete {tile.Name} and its folder?";
        if (!await dialogs.ConfirmAsync($"Remove {tile.Name}?", question, "Delete").ConfigureAwait(true))
        {
            return;
        }

        try
        {
            await Task.Run(() => cleaner.Remove(tile.TitleId)).ConfigureAwait(true);
        }
        catch (GameRunningException)
        {
            Message = $"Close {tile.Name} first.";
        }
        catch (GameBusyException)
        {
            Message = $"Another PS5Kitsune action is using {tile.Name}. Try again when it finishes.";
        }
        catch (GameNotFoundException)
        {
            // Removed elsewhere already: the refresh drops it.
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Message = $"Could not remove {tile.Name} completely: {exception.Message}";
        }

        await RefreshAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private void AddGame() => navigator.GoTo(pages.ChooseGame());

    [RelayCommand]
    private void Home() => navigator.GoHome(pages.Home());

    private static string FormatSize(long bytes) => bytes >= 1L << 30
        ? string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)(1L << 30):0.0} GB")
        : string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)(1L << 20):0.0} MB");
}
```

Notes for the implementer:
- `Max(StringComparer.Ordinal)` is the `Enumerable.Max<TSource>(IEnumerable<TSource>, IComparer<TSource>?)` overload.
- If CommunityToolkit's generator refuses a static `CanExecute` method, make `CanViewLastLog` an instance method and suppress CA1822 on it with `[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "CanExecute target of a generated command.")]`.

- [ ] **Step 4: Write the Library view**

Create `src/PS5Kitsune.App/Views/LibraryView.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:PS5Kitsune.App.ViewModels"
             xmlns:views="using:PS5Kitsune.App.Views"
             x:Class="PS5Kitsune.App.Views.LibraryView"
             x:DataType="vm:LibraryViewModel">
  <DockPanel>
    <Grid DockPanel.Dock="Top" ColumnDefinitions="Auto,*,Auto,Auto" ColumnSpacing="8" Margin="0,0,0,12">
      <TextBlock Text="Library" FontSize="20" FontWeight="SemiBold" VerticalAlignment="Center" />
      <TextBox Grid.Column="1" Text="{Binding Search}" Watermark="Search by name" MaxWidth="320" HorizontalAlignment="Left" />
      <Button Grid.Column="2" Content="Add a game" Command="{Binding AddGameCommand}" />
      <Button Grid.Column="3" Content="Home" Command="{Binding HomeCommand}" />
    </Grid>

    <TextBlock DockPanel.Dock="Top" Text="{Binding Message}" IsVisible="{Binding Message, Converter={x:Static ObjectConverters.IsNotNull}}"
               Foreground="#C42B1C" TextWrapping="Wrap" Margin="0,0,0,8" />

    <StackPanel DockPanel.Dock="Top" Spacing="8" IsVisible="{Binding IsEmpty}" HorizontalAlignment="Left">
      <TextBlock Text="No games yet" FontWeight="SemiBold" />
      <Button Content="Add a game" Classes="accent" Command="{Binding AddGameCommand}" />
    </StackPanel>

    <ScrollViewer>
      <ItemsControl ItemsSource="{Binding Games}">
        <ItemsControl.ItemsPanel>
          <ItemsPanelTemplate>
            <WrapPanel />
          </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
        <ItemsControl.ItemTemplate>
          <DataTemplate x:DataType="vm:GameTileViewModel">
            <Border Width="180" Margin="0,0,12,12" Padding="10" CornerRadius="6" BorderThickness="1" BorderBrush="#40808080">
              <StackPanel Spacing="6">
                <Button Padding="0" Background="Transparent" HorizontalAlignment="Stretch"
                        Command="{Binding Owner.OpenCommand}" CommandParameter="{Binding}" ToolTip.Tip="Launch">
                  <Panel Height="158">
                    <Border Background="#20808080" CornerRadius="4" />
                    <TextBlock Text="{Binding TitleId}" Opacity="0.6" HorizontalAlignment="Center" VerticalAlignment="Center"
                               IsVisible="{Binding IconPath, Converter={x:Static ObjectConverters.IsNull}}" />
                    <Image Source="{Binding IconPath, Converter={x:Static views:PathToBitmapConverter.Instance}}" Stretch="Uniform" />
                  </Panel>
                </Button>
                <Grid ColumnDefinitions="*,Auto">
                  <StackPanel>
                    <TextBlock Text="{Binding Name}" FontWeight="SemiBold" TextTrimming="CharacterEllipsis" />
                    <TextBlock Text="{Binding TitleId}" Opacity="0.7" FontSize="12" />
                  </StackPanel>
                  <Button Grid.Column="1" Content="…" Padding="8,2" VerticalAlignment="Top">
                    <Button.Flyout>
                      <MenuFlyout>
                        <MenuItem Header="Launch" Command="{Binding Owner.LaunchCommand}" CommandParameter="{Binding}" />
                        <MenuItem Header="Open folder" Command="{Binding Owner.OpenFolderCommand}" CommandParameter="{Binding}" />
                        <MenuItem Header="View last log" Command="{Binding Owner.ViewLastLogCommand}" CommandParameter="{Binding}" />
                        <MenuItem Header="Reconvert" Command="{Binding Owner.ReconvertCommand}" CommandParameter="{Binding}" />
                        <MenuItem Header="Clear shader cache" Command="{Binding Owner.ClearShaderCacheCommand}" CommandParameter="{Binding}" />
                        <MenuItem Header="Remove" Command="{Binding Owner.RemoveCommand}" CommandParameter="{Binding}" />
                      </MenuFlyout>
                    </Button.Flyout>
                  </Button>
                </Grid>
                <WrapPanel>
                  <Border Background="#0F7B0F" CornerRadius="3" Padding="6,1" Margin="0,0,4,4" IsVisible="{Binding IsGreen}">
                    <TextBlock Text="Green" Foreground="White" FontSize="11" />
                  </Border>
                  <Border Background="#9D5D00" CornerRadius="3" Padding="6,1" Margin="0,0,4,4" IsVisible="{Binding IsOrange}">
                    <TextBlock Text="Orange" Foreground="White" FontSize="11" />
                  </Border>
                  <Border Background="#C42B1C" CornerRadius="3" Padding="6,1" Margin="0,0,4,4" IsVisible="{Binding IsRed}">
                    <TextBlock Text="Red" Foreground="White" FontSize="11" />
                  </Border>
                  <Border Background="#9D5D00" CornerRadius="3" Padding="6,1" Margin="0,0,4,4" IsVisible="{Binding NeedsReconvert}">
                    <TextBlock Text="Needs reconvert" Foreground="White" FontSize="11" />
                  </Border>
                  <Border Background="#005FB8" CornerRadius="3" Padding="6,1" Margin="0,0,4,4" IsVisible="{Binding IsRunning}">
                    <TextBlock Text="Running" Foreground="White" FontSize="11" />
                  </Border>
                </WrapPanel>
              </StackPanel>
            </Border>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
    </ScrollViewer>
  </DockPanel>
</UserControl>
```

Create `src/PS5Kitsune.App/Views/LibraryView.axaml.cs`:

```csharp
using Avalonia.Controls;

namespace PS5Kitsune.App.Views;

public sealed partial class LibraryView : UserControl
{
    public LibraryView() => InitializeComponent();
}
```

In `src/PS5Kitsune.App/Views/MainWindow.axaml`, add before the `GameViewModel` template:

```xml
    <DataTemplate DataType="vm:LibraryViewModel">
      <views:LibraryView />
    </DataTemplate>
```

- [ ] **Step 5: Make the Library the start page; rewire Home, Reconvert and Result**

In `src/PS5Kitsune.App/Shell/PageFactory.cs`, replace the temporary `Library()` and its comment with:

```csharp
    public object Library() =>
        new LibraryViewModel(
            Session.Get<GamePlayer>(), Session.Get<GameCleaner>(), Session.Get<RunningGames>(), Session.Get<IAnyPS5Installer>(), platform, dialogs, navigator, this);

    /// <summary>The first page: the Library once AnyPS5 is installed and a game is added, else Home.</summary>
    public object Start() => LibraryViewModel.OpensFirst(Session.Get<IAnyPS5Installer>(), Session.Get<GameLibrary>()) ? Library() : Home();
```

In `src/PS5Kitsune.App/App.axaml.cs`, replace `navigator.GoHome(pages.Home());` with `navigator.GoHome(pages.Start());`.

In `src/PS5Kitsune.App/ViewModels/HomeViewModel.cs`:
- on `Status`, change the `NotifyCanExecuteChangedFor` attribute to `[NotifyCanExecuteChangedFor(nameof(AddGameCommand))]`;
- on `IsChecking`, change it to `[NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(AddGameCommand), nameof(LibraryCommand))]`;
- replace the `GamesToReconvert` property (with its comment and attributes) and `HasGamesToReconvert` with:

```csharp
    /// <summary>True once a game is added: enables "Library".</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LibraryCommand))]
    public partial bool HasGames { get; private set; }

    public bool CanOpenLibrary => !IsChecking && HasGames;
```

- in `RefreshAsync`, replace `GamesToReconvert = [];` with `HasGames = false;`, and `GamesToReconvert = check.ToReconvert;` with `HasGames = check.HasGames;`;
- in the `HomeCheck` record, replace the `ToReconvert` parameter with `bool HasGames`, and its doc line with `/// <param name="HasGames">At least one game is added.</param>`;
- in `Check`, replace the `toReconvert` computation and the `return` with `return new HomeCheck(mode, state, active, requested, other, library.HasGames());`, and change the method's summary to "Runs on a worker thread: the migration moves a folder, and the done-checks can hash hundreds of MB.";
- replace the `Reconvert` command with:

```csharp
    [RelayCommand(CanExecute = nameof(CanOpenLibrary))]
    private void Library() => navigator.GoHome(pages.Library());
```

- remove any `using` the build reports as unused.

Delete `src/PS5Kitsune.App/ViewModels/GameToReconvert.cs` with `git rm`.

In `src/PS5Kitsune.App/Views/HomeView.axaml`:
- delete the `StackPanel` holding "Games to reconvert";
- add this line after the "Add a game" button:

```xml
        <Button Content="Library" Command="{Binding LibraryCommand}" />
```

In `src/PS5Kitsune.App/ViewModels/ReconvertViewModel.cs`, in `OnCancelledAsync` and in `LeaveAsync`, replace `_navigator.GoHome(_pages.Home());` with `_navigator.GoHome(_pages.Library());`. In `src/PS5Kitsune.App/Views/ReconvertView.axaml`, change `Content="Back to home"` to `Content="Go to library"`.

In `src/PS5Kitsune.App/ViewModels/AddResultViewModel.cs`:
- in `AddAnother`, replace `_navigator.GoHome(_pages.Home());` with `_navigator.GoHome(_pages.Library());`;
- replace the `BackHome` command with:

```csharp
    [RelayCommand]
    private void BackToLibrary() => _navigator.GoHome(_pages.Library());
```

In `src/PS5Kitsune.App/Views/AddResultView.axaml`, replace `<Button Content="Back to home" Classes="accent" Command="{Binding BackHomeCommand}" />` with `<Button Content="Go to library" Classes="accent" Command="{Binding BackToLibraryCommand}" />`.

- [ ] **Step 6: Run the tests to verify they pass**

Close any running `PS5Kitsune.exe` first.
Run: `dotnet build PS5Kitsune.slnx -c Release` — Expected: 0 warnings, 0 errors.
Run: `dotnet test --solution PS5Kitsune.slnx -c Release` — Expected: 0 failed.

- [ ] **Step 7: Look at the Library in the running app**

Run: `dotnet run --project src/PS5Kitsune.App -c Release` on a root with AnyPS5 installed and DOOM added.

Expected:
- the app opens on Library;
- DOOM's tile shows its icon and its verdict badge;
- the "…" menu opens and its items work;
- "View last log" is enabled only when a log exists.

If the menu items stay disabled or do nothing, the flyout did not inherit the tile's data context. Stop and report it, with what you saw; do not guess a fix.

- [ ] **Step 8: Commit**

```bash
git add -A src/PS5Kitsune.App tests/PS5Kitsune.App.Tests
git commit -m "Add the Library page and open the app on it

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 8: Docs

**Files:**
- Modify: `docs/architecture.md`, `README.md`

**Interfaces:**
- Consumes: everything above.
- Produces: docs only.

- [ ] **Step 1: Update the architecture doc**

Make these changes in `docs/architecture.md`:

1. In the 6b Reconvert paragraph (it starts "Before rebuilding, the conversion outputs"):
   - replace "`.ps5kitsune\reconvert.lock`" with "`.ps5kitsune\game.lock`";
   - replace "from Home's "Games to reconvert" and from the summary of an already added game" with "from the Library's menu and from the summary of an already added game".
2. In "V2 — Add a game":
   - in step 4, replace "(Move is deferred to milestone 7.)" with "(Move is deferred; it is not part of milestone 7.)";
   - in the Summary screen line, replace "Copy only; Move comes with the library (milestone 7)." with "Copy only; Move is a later follow-up."
3. In "V3 — Launcher", "Library", replace the bullets with:

```markdown
- The app opens on the Library once AnyPS5 is installed and a game is added; Home has a "Library" button.
- Tile grid: `icon0.png`, name, title ID, audit verdict badge (green, orange, red), "Needs reconvert" when the game was converted with another AnyPS5 than the one in use, "Running" while it runs.
- Search by name, sorted by last played (never played last, by name).
- A click launches the game, or shows it when it runs. Per-game menu: Launch, Open folder, View last log, Reconvert, Clear shader cache (shows its size, which can reach several GB), Remove (deletes the game folder after a confirmation that flags saves present in `data\`). Settings comes with 7b.
- The CLI has the same view: `ps5kitsune list` and `ps5kitsune remove <titleId> [--yes]`.
```

4. In "Launching", replace the bullet "Closing the launcher during a game **does not kill the game** (no kill-on-close Job Object); on reopening, the launcher finds the running process by its recorded PID." with:

```markdown
- Closing the launcher during a game **does not kill the game**: the game is never in a kill-on-close Job Object. A launch records the game in `.ps5kitsune\running.json` (PID, process creation time, executable, log file); on reopening, the launcher opens the process again and checks its start time and executable, so a PID reused by another process is never taken for the game. A record whose process is gone is deleted. One game runs at a time from the app.
- The tools PS5Kitsune runs (git, CMake, Ninja, Python, the relinker) are in a kill-on-close Job Object: killing PS5Kitsune, even from the Task Manager, ends them.
- Every action that changes a game folder (Reconvert, a launch until it is recorded, Remove, Clear shader cache, the restore of an interrupted Reconvert) holds `.ps5kitsune\game.lock`, retried for about 250 ms. A running game refuses Reconvert, Remove and Clear shader cache.
```

5. In the root layout tree:
   - delete the line `├── library.json                 library index`;
   - under `.ps5kitsune\`, add these lines after `game.json`, keeping the tree's characters:

```
│           ├── running.json     the game launched by PS5Kitsune, while it may run
│           ├── game.lock        held while an action changes the folder
```

6. In the roadmap, replace item 7 (its criterion, and the 7b bullet with its criterion) with:

```markdown
7. **V3 — Launcher**, in four parts.
   - **7a — Library and launch.** Library page (the start page once a game is added), launch in one click with the log followed live, Stop and the exit code, the running game found again after reopening (`running.json`), Open folder, View last log, Reconvert, Clear shader cache, Remove; CLI `list` and `remove`; the game lock and the tools' Job Object. Spec: [milestone 7a](superpowers/specs/2026-10-24-milestone-7a-library-launch-design.md).
     - Criterion: DOOM launched in one click from the Library; the launcher closed and reopened finds the running game and stops it.
   - **7b — Per-game settings**, Lavapipe (Mesa 7z) and fonts.
     - Criterion: DOOM launched in one click with Lavapipe on the Iris Xe.
   - **7c — Crash diagnosis.**
     - Criterion: a deliberately provoked error 126 is recognized and repaired.
   - **7d — Visual design.** Once every main screen exists (install, add a game, library and launch): a coherent theme in light and dark, typography and spacing, shared card components (Mode, Tools), the library's look with game icons, and accessibility (selected states exposed to screen readers, keyboard navigation).
     - Criterion: every screen follows the same design in both themes, and each one can be used with the keyboard alone.
```

7. Run `grep -n "7b" docs/architecture.md`. Change any other "7b" that means visual design to "7d".

- [ ] **Step 2: Update the README**

In `README.md`, add these lines to the command block, after the `ps5kitsune run <title ID>` line:

```
ps5kitsune list                      # the added games, their verdict, and whether they run
ps5kitsune remove <title ID>         # delete a game and its folder (asks first; --yes skips)
```

Then replace the paragraph that starts "In the app, Home's "Add a game"" with:

```markdown
In the app, the Library lists the added games and opens first once a game is added: one click launches a game and follows its log, and closing the app leaves the game running (reopening finds it again). Each game's menu opens its folder or its last log, reconverts it, clears its shader cache or removes it. Home's "Add a game" takes a folder or a .zip (picked or dropped), shows its summary, adds it and shows the audit verdict. After an AnyPS5 update, `ps5kitsune reconvert <titleId>` (or Reconvert in the app) converts a game again without copying it, keeping its saves; a failure puts the previous conversion back.
```

- [ ] **Step 3: Check and commit**

Run: `grep -n "reconvert.lock\|library.json\|Games to reconvert" docs/architecture.md README.md`
Expected: no output.

```bash
git add docs/architecture.md README.md
git commit -m "Document the library, launching and the 7a-7d split

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

## Criterion (run by the user, after Task 8)

On the AMD laptop, with DOOM added (spec, "Criterion"):

1. The app opens on Library; DOOM's tile shows its icon and its verdict badge.
2. One click launches DOOM; its page follows the log live.
3. With DOOM running, close the app and reopen it: DOOM's tile shows "Running", its page reattaches, and Stop ends DOOM and shows its exit code.
4. `ps5kitsune list` shows DOOM with its last played date.
5. Clear shader cache shows the cache size and empties `shader_cache\`.
6. Killing PS5Kitsune from the Task Manager during a Reconvert's relinker also ends the relinker; reopening restores the previous conversion.
7. Remove deletes DOOM's folder and its tile; adding DOOM again brings it back.
