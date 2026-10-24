# Milestone 7a — Library and launch: design

October 24, 2026 · Complements [docs/architecture.md](../../architecture.md) ("V3 — Launcher", milestone 7 of the roadmap) and the [milestone 6b spec](2026-10-23-milestone-6b-reconvert-design.md).

## Goal

Show the added games in the app and launch them in one click, with the log followed live, a Stop button and the exit code. Closing the app never stops a game, and reopening it finds the game still running. The CLI gets the same view of the library (`list`, `remove`) and `run` shares the same rules. The PR #8 follow-ups that belong to launching are folded in.

**Criterion:** on the AMD laptop with DOOM, see [Criterion](#criterion).

**Out of scope:**

- **7b:** per-game settings, Lavapipe (Mesa download and activation), fonts.
- **7c:** crash diagnosis and its repair actions.
- **7d:** visual design, which was called 7b before this split. It also covers the UI-thread start of the step-run pages and a shared step-run view.
- **Later:** Move when adding a game (6a deferred it to milestone 7; it stays a follow-up), and a `library.json` index (the library stays the folders under `<root>\games`). Running several games at once, and detecting a game started outside PS5Kitsune, are not planned.

## Split of milestone 7

| Part | Content |
| --- | --- |
| 7a | Library page, launch from the app, game page with live log and Stop, reattach after reopening, Open folder, View last log, Reconvert, Clear shader cache, Remove; CLI `list` and `remove`; PR #8 follow-ups |
| 7b | Per-game settings, Lavapipe (Mesa 7z), fonts |
| 7c | Crash diagnosis and repair |
| 7d | Visual design (every screen exists by then) |

The roadmap's milestone 7 criterion splits accordingly: Lavapipe on the Iris Xe goes to 7b, error 126 recognized and repaired goes to 7c, and "the launcher closed and reopened finds the running game" is 7a's.

## Facts checked on 2026-10-24

- The app has no launch yet. Only `ps5kitsune run` launches: it checks the install and `ReconvertLock.IsHeld`, calls `GameLibrary.MarkPlayedAsync`, launches through `IGameLauncher`, then follows the log with `LogTail.FollowAsync`. Ctrl+C stops following, not the game.
- `WindowsGameLauncher` starts the game with CreateProcess, `CREATE_NO_WINDOW`, and stdout and stderr set to an inherited handle to the log file (a handle list restricts inheritance to it). There is no job object. It keeps the last 10 logs, named `yyyyMMdd-HHmmss.log`.
- `ILaunchedGame` exposes `ProcessId`, `LogFile` and `WaitForExitAsync`; it holds the process handle, so the exit code is read after exit.
- `ReconvertLock` is `.ps5kitsune\reconvert.lock` opened with no sharing and delete-on-close. `TryAcquire` returns null when it is held, and `IsHeld` probes it.
- `GameLibrary.All()` lists every readable game ordered by name. `NeedsReconversion(record, install)` compares the conversion with the install in use.
- `GameRecord` has `Icon` (relative path to `icon0.png`, or null), `Audit.Verdict`, `AddedAt` and `LastPlayedAt`. `MarkPlayedAsync` writes the record it was given, so a write between its read and its write is lost. `UpdateGameRecordStep` writes the record read when the Reconvert started.
- `CliWrapProcessRunner` starts tools through CliWrap and can read the child's PID once `ExecuteAsync` has started; on cancel it kills the whole tree.
- Home lists "Games to reconvert" (6b) and is the app's first page. `ConfirmWindow` exists for yes/no confirmations. `IPlatform.OpenPath` opens a folder or a file.

## Core

### The game lock

`ReconvertLock` becomes `GameLock`, file `.ps5kitsune\game.lock`, same mechanism (no sharing, delete-on-close). It is held by every action that changes the game folder or depends on it not changing: Reconvert (whole run), a reader restoring an interrupted Reconvert, a launch (from its checks until `running.json` is written), Remove and Clear shader cache.

- `TryAcquire` retries 5 times over about 250 ms before returning null, so a reader's short restore or a launch no longer refuses another action (6b follow-up "false refusal").
- `ReconvertInProgressException` becomes `GameBusyException`: "Another PS5Kitsune action is using {titleId}. Try again when it finishes."
- A leftover `reconvert.lock` from 6b is ignored: it is not held once its process is gone.

### Running games

A running game is recorded in `.ps5kitsune\running.json`:

```json
{ "schemaVersion": 1, "processId": 4812, "startedAt": "2026-10-24T18:03:11.4170000Z", "executable": "C:\\PS5Kitsune\\games\\PPSA01234\\PPSA01234.exe", "logFile": "C:\\PS5Kitsune\\games\\PPSA01234\\.ps5kitsune\\logs\\20261024-200311.log" }
```

`startedAt` is the process creation time read from the OS, not the time of the call, so it identifies the process even after its PID is reused.

`IGameLauncher` changes:

```csharp
ILaunchedGame Launch(GameLaunchRequest request);
/// <returns>Null when the process exited, or its PID now belongs to another process (start time or executable differ).</returns>
ILaunchedGame? Attach(RunningGame record);
```

`ILaunchedGame` gains `DateTimeOffset StartedAt` and `Task StopAsync(CancellationToken)`: it asks the game's windows to close, waits up to 5 seconds, then terminates the process. `WaitForExitAsync` returns the exit code for an attached game too. Windows opens an attached process with `PROCESS_QUERY_LIMITED_INFORMATION | SYNCHRONIZE | PROCESS_TERMINATE` and compares `GetProcessTimes` and `QueryFullProcessImageName` with the record.

### GamePlayer

New class in `Core/Library`, the single entry point for `run` and the app:

- `Running(GameLayout game) → ILaunchedGame?` reads `running.json` and attaches. A record that does not attach, or does not parse, is stale: it is deleted (a malformed one with a warning) and null is returned.
- `LaunchAsync(string titleId, CancellationToken) → ILaunchedGame`:
  1. Refuses when no install is in use (`AnyPS5NotInstalledException`).
  2. Takes the game lock, or throws `GameBusyException`. Holding it through the launch closes 6b's gap between `run`'s lock check and the launch: a Reconvert cannot start in between.
  3. Throws `GameRunningException(processId, logFile)` when `Running` finds the game.
  4. Throws `GameExecutableMissingException` when the executable is missing.
  5. Re-reads `game.json` and writes it with `lastPlayedAt` set (`MarkPlayedAsync` takes the layout and re-reads, instead of taking a record).
  6. Launches, writes `running.json`, releases the lock.
- `ForgetAsync(GameLayout game, ILaunchedGame launched)` deletes `running.json` once the exit is observed, only if it still names that process. Whoever observes the exit calls it (`run`, the game page); a record nobody cleaned is found stale later.

`GameLibrary.All()` returns `LibraryGame(GameLayout Game, GameRecord Record, RunningGame? Running)`: `Running` is the record once checked against the live process (the handle opened for the check is closed), so the Library and `list` badge a game without keeping handles. The game page calls `GamePlayer.Running` to attach.

### Rules that use the running state

Each takes the game lock first, then refuses a running game with `GameRunningException`:

- **Reconvert.** `SetAsideStep`'s exe check stays as the second guard, for a game started outside PS5Kitsune.
- **Remove.** `GameRemover.RemoveAsync(string titleId)` deletes the game folder. `game.json` is deleted last, so a partial delete (a file in use) leaves a listed game that a retry finishes. `GameLibrary.HasSaves(game)` is true when `data\` holds at least one file; the UI and the CLI use it to warn.
- **Clear shader cache.** `ShaderCache.Size(game)` (bytes of `shader_cache\`, 0 when missing) and `ShaderCache.ClearAsync(game)`. A failure says the cache may be partly cleared; the game rebuilds it.

### PR #8 follow-ups folded in

- **Child processes die with PS5Kitsune.** New platform service `IChildProcessJob` with `Assign(int processId)`. Windows creates one job object per PS5Kitsune process with `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`; `CliWrapProcessRunner` assigns each tool (relinker, Python, CMake, Ninja, Git) as soon as its PID is known, and children it starts afterwards inherit the job. A grandchild started in the microseconds before the assignment escapes; that is accepted. The game is never assigned. Linux does nothing (milestone 8).
- **`lastPlayedAt` lost update.** "Record the game" re-reads `game.json` just before writing and keeps that read's `lastPlayedAt` and `settings`.
- **Gap in `run`.** Closed by `GamePlayer` holding the lock through the launch.
- **False refusal.** Closed by the lock's retry.
- **Linux delete-on-close** stays a known gap until milestone 8.

## App

### Start page

The app opens on **Library** when AnyPS5 is installed and at least one game is added, otherwise on **Home**. Home gains a "Library" button (enabled once a game is added) and loses "Games to reconvert": the Library's badge replaces it.

### Library page

- Header: "Library", a name search box, "Add a game" and "Home".
- A wrapping grid of tiles. A tile shows `icon0.png` (a placeholder when the game has none), the name, the title ID and badges: the verdict (green, orange, red), "Needs reconvert", "Running".
- Order: last played first; never played games after, by name.
- Clicking a tile launches the game; on a running game it opens its game page.
- A "…" menu per tile: Launch, Open folder, View last log (disabled when `logs\` is empty), Reconvert, Clear shader cache, Remove.
- The list is read again each time the page is shown, off the UI thread.
- Errors from an action appear in a line above the grid: "DOOM is already running.", "Another PS5Kitsune action is using DOOM. Try again when it finishes.", "The executable is missing: Reconvert DOOM.", or the launch failure's message.
- An empty library (all games removed) shows "No games yet" and "Add a game".

### Game page

One game at a time. Launching a game while another runs asks "{Other} is running. Show it?" and opens that game's page instead.

- Running: name, "Running (PID n)", Stop, the log followed live (`LogTail`, last 500 lines kept on screen), Open folder, "← Library". The first launch of a game adds "The first launch may stutter while shaders are prepared."
- Exited: "Exited with code N", "Launch again" and "← Library". The page calls `ForgetAsync`.
- Stop failed: "Could not stop DOOM: {message}". The game stays running and recorded.
- Leaving the page leaves the game running. On the next app start, the running game's tile shows "Running" and its page reattaches: the log restarts from the beginning of the file.

### Confirmations

Through `ConfirmWindow`:

- Clear shader cache: "Delete the shader cache of DOOM (3.2 GB)? The next launch will stutter while shaders are prepared again."
- Remove: "Delete DOOM and its folder?", plus "Its saves in data\ are deleted too." when it has saves.

### Reconvert

The menu's Reconvert opens 6b's Reconverting page; its "Done" and the result page go back to Library. A running game shows "Close DOOM first." on the Library page without leaving it.

## CLI

- **`run <titleId>`** goes through `GamePlayer`: it records the game in `running.json`, forgets it on exit, and on an already running game prints "DOOM is already running (PID n). Log: {path}." and exits with 1. Ctrl+C stops following, leaves the record, and prints the PID and log as today.
- **`list`** prints one line per game: title ID, name, verdict, then "reconvert" when it needs a Reconvert, "running (PID n)" when running, and the last played date or "never played". With no game: "No games. Add one with: ps5kitsune add <folder>".
- **`remove <titleId> [--yes]`** asks "Delete DOOM and its folder? [y/N]" (adding "Its saves in data\ are deleted too." when it has saves); `--yes` skips the question. It refuses a running game or a held lock with exit code 1.

## Errors

| Case | App | CLI |
| --- | --- | --- |
| Lock held | Library line: another action is using the game | exit 1, same sentence |
| Game already running | Opens its page (Library) | exit 1, PID and log |
| Executable missing | Library line, suggests Reconvert | exit 1 |
| CreateProcess failed | Library line with the Win32 message | exit 1 |
| Stop failed | Game page line; game stays recorded | — |
| Malformed `running.json` | Deleted, warning in the app log | same |
| Remove partly failed | Library line naming the file in use; the game stays listed | exit 1, file named |
| Clear shader cache failed | Library line: may be partly cleared | — |

## Tests

- **Core (xUnit):**
  - `GamePlayer` with a fake `IGameLauncher`: refuses while the lock is held or the game runs; re-reads the record before writing `lastPlayedAt`; writes `running.json`, and `ForgetAsync` deletes it only when it names that process; a record that does not attach or does not parse is deleted.
  - `GameLock`: the retry succeeds when the holder releases within the window and returns null when it does not.
  - `GameRemover`: refuses a running game and a held lock; deletes `game.json` last (a file held open leaves the game listed, Windows only).
  - `ShaderCache`: size of a nested folder, 0 when missing, clear.
  - `UpdateGameRecordStep` keeps a `lastPlayedAt` written during the Reconvert.
  - `GameLibrary.All()` reports the running state, and `HasSaves`.
- **Windows platform tests:**
  - `Attach` to a real short-lived process (a waiting `cmd.exe`), its exit code after exit, and null on a start-time mismatch.
  - `StopAsync` ends a process with no window.
  - A process assigned to `IChildProcessJob` dies when the job is closed.
- **App (headless):**
  - `LibraryViewModel`: badges, search, order, start page choice, the error line.
  - `GameViewModel` with a fake launcher: running, exited, stop failed, reattach.
  - Home: the Library button, and no "Games to reconvert".
- **CLI:** no test project; `list` and `remove` are checked by hand during the criterion run.

## Criterion

On the AMD laptop, with DOOM added:

1. The app opens on Library; DOOM's tile shows its icon and a green badge.
2. One click launches DOOM; its page follows the log live.
3. With DOOM running, close the app and reopen it: DOOM's tile shows "Running", its page reattaches, Stop ends DOOM and shows its exit code.
4. `ps5kitsune list` shows DOOM with its last played date.
5. Clear shader cache shows the cache size and empties `shader_cache\`.
6. Killing PS5Kitsune from Task Manager during a Reconvert's relinker also ends the relinker; reopening restores the previous conversion.
7. Remove deletes DOOM's folder and its tile (the source folder is untouched, since add copies it); adding DOOM again brings it back.

## Docs

- `architecture.md`: the roadmap splits milestone 7 into 7a–7d (visual design becomes 7d); "V3 — Launcher" describes `running.json`, the game lock and the job object for tools; the root layout lists `running.json` and `game.lock` and drops `library.json` (not planned).
- README: `list` and `remove`.
