# Milestone 6b — Reconvert: design

October 23, 2026 · Complements [docs/architecture.md](../../architecture.md) ("V2 — Add a game", milestone 6 of the roadmap) and the [milestone 6a spec](2026-10-22-milestone-6a-add-game-screens-design.md).

## Goal

Reconvert an added game after an AnyPS5 update: new libs, new executable, new audit, without copying `app0` again and without touching what the game created in its folder. A failed or cancelled Reconvert leaves the game exactly as it was. Available from the CLI (`ps5kitsune reconvert <titleId>`) and from the app (Home's list of games to reconvert, and the Summary page of an already added game).

**Criterion (roadmap):** Reconvert keeps `data\` and `_trophy\`.

**Out of scope:** the library screen and its "Needs reconversion" badge (milestone 7; Home's list stands in until then); reconverting several games in one action; locking against a concurrent `add` or `reconvert` of the same game from another process; detecting a running game by PID (milestone 7).

## Facts checked on 2026-10-23

- `ps5kitsune use release|build` switches between two finished installs. DOOM for PS5 audits 360 implemented, 4 stubs, 0 absent with the build (commit 3fd1cc3) and 361, 3, 0 with release v0.1.2: switching installs and reconverting changes the counts, which makes the criterion observable.
- `CopyLibsStep` skips a file whose copy has the same size and never deletes: run on an existing `libs\`, it would leave libraries of the previous version in place.
- `RelinkStep.DeletePreviousOutputs` deletes the relinked marker, the executable, `*.registry.json`, `*.needed.json` in the game folder and `app0\**\*.guest.prx`. The relinker writes the `<module>.guest.prx` files beside the original modules in `app0`, not beside the executable, so a side folder cannot hold every new output without its own `app0`.
- `UnwrapSelfStep` is done when no file of `eboot.bin` and the module folders is a SELF; after an add, all are ELF, so it is skipped.
- `AuditStep` always runs, rebuilds `.ps5kitsune\audit-modules` and deletes `audit.json` before running import_audit.py.
- `GameRecord` (schema 1) holds `ConvertedWith(Profile, Mode, Version, Commit, ToIntel)`, `Audit`, `Settings`, `AddedAt` and `LastPlayedAt`. `AnyPS5InstallRecord` holds `Profile`, `Mode`, `Version` and `Commit`.
- `JsonFiles.WriteAsync` writes a `.tmp` file then moves it over the target: `game.json` is replaced atomically.
- `GameLibrary.Find(titleId)` reads one `game.json`; nothing lists the games yet.
- Step names are written in Core (`GameImporter.Names`) and again in the app.

## Core

### GameReconverter

New class in `Core/Games`, behind `IGameReconverter`, registered in Core's services:

```csharp
Task<GameReconverted> ReconvertAsync(string titleId, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default);
IReadOnlyList<string> StepNames { get; }
```

- Refuses with `AnyPS5NotInstalledException` when no install is in use, `GameNotFoundException` when the title ID has no `game.json`, and `InvalidGameRecordException` for a malformed one, as `run` does.
- Restores an interrupted Reconvert first (see Recovery).
- Reuses `ConvertedWith.ToIntel` from `game.json`; the CPU is not detected again.
- The module folders come from the game's `app0` (`sce_module`, `sce_modules`, `prx`, as the validator finds them), so the existing steps get the same `GameImportContext` shape as in an add.
- Returns `GameReconverted(GameRecord Record, string GameDirectory, AuditSummary Audit, AuditRecord PreviousAudit)`.
- On a step failure or a cancellation, it restores the previous conversion, then rethrows (`StepFailedException` or `OperationCanceledException`). If the restore itself fails, it logs the error and throws `ReconvertRestoreException`, which wraps the original exception and keeps it as the inner exception.

### Steps

| # | Name | Class |
| --- | --- | --- |
| 1 | Set the old conversion aside | `SetAsideStep` (new) |
| 2 | Copy the AnyPS5 libraries | `CopyLibsStep` |
| 3 | Unwrap SELF files | `UnwrapSelfStep` |
| 4 | Convert with the relinker | `RelinkStep` |
| 5 | Audit the imports | `AuditStep` |
| 6 | Record the game | `UpdateGameRecordStep` (new) |

**Set the old conversion aside.** Moves the conversion outputs into `.ps5kitsune\previous.partial\`, keeping their path relative to the game folder, then renames `previous.partial` to `previous` in one rename. The conversion outputs are:

- the executable, the relinked marker, `audit.json`;
- `*.registry.json` and `*.needed.json` in the game folder;
- `libs\`;
- `app0\**\*.guest.prx`.

It is never already done. The moves are synchronous (same volume, so each is a rename). A move that fails (`IOException`, `UnauthorizedAccessException`) throws `GameImportException("Cannot set the old conversion aside: {reason}", "Close the game if it is running.")`; the failure path then restores what was moved, so nothing has changed.

**Steps 2 to 5** are the add's steps, unchanged. `libs\` is gone after step 1, so step 2 copies every library of the install in use.

**Record the game.** Deletes `.ps5kitsune\previous\`, then rewrites `game.json` from the existing record with:

- `ConvertedWith` = the install in use (`Profile`, mode name, `Version`, `Commit`) and the kept `ToIntel`;
- `Audit` = the new counts and verdict;
- `ReconvertedAt` = now (new optional field, `DateTimeOffset?`, null for a game never reconverted; the schema stays at version 1 and older records still read).

`SchemaVersion`, `TitleId`, `Name`, `Executable`, `Icon`, `Settings`, `AddedAt` and `LastPlayedAt` are kept. If the process dies between the deletion and the write, the game has consistent new outputs and an old record: it shows as needing reconversion again, which is harmless.

### Recovery

One rule per folder state, applied by `ConversionBackup.Restore(GameLayout)`:

- **`.ps5kitsune\previous.partial\` exists:** the move was interrupted before anything new was built. Its items are moved back (overwriting nothing new, since nothing was built), then the folder is deleted.
- **`.ps5kitsune\previous\` exists:** the run did not finish. The conversion outputs now in place (the list above) are deleted, every item of `previous\` is moved back, then the folder is deleted.

Restore runs at the start of `ReconvertAsync`, after a failed or cancelled run, in `GameLibrary.Find` (so `run` never launches a half-built game), and in `GameLibrary.All` (Home's scan). A restore that fails leaves the folder in place, so the rule applies again next time.

### Never touched

`data\`, `download0\`, `_trophy\`, `anyps5-input.ini`, `anyps5-fonts\`, `shader_cache\`, every file of `app0` other than `*.guest.prx`, `.ps5kitsune\originals\` and `.ps5kitsune\logs\`.

### Out of date

`GameLibrary` gains:

- `All()`: every folder of `<root>\games` whose `game.json` reads, as `(GameLayout, GameRecord)`, ordered by name. A malformed `game.json` is skipped and logged at Warning.
- `static bool NeedsReconversion(GameRecord record, AnyPS5InstallRecord install)`: true when `Profile`, mode name, `Version` or `Commit` differs between `record.ConvertedWith` and `install`.

## CLI

- **`ps5kitsune reconvert <titleId>`:** the step lines, then the verdict as `add` prints it, plus `Before: 360 implemented, 4 stubs, 0 absent`. When the game was already converted with the install in use, a line first says `Already converted with release v0.1.2; reconverting anyway.`
  - On failure or Ctrl+C: `The previous conversion was restored.` If the restore failed: `The previous conversion could not be restored yet; it will be restored on the next start or reconvert.`
  - Exit codes: 0 success, 1 failure, 130 cancelled.
- **`ps5kitsune run <titleId>`:** when the game needs reconversion, a warning first: `Converted with build 3fd1cc3; the AnyPS5 in use is release v0.1.2. Run ps5kitsune reconvert PPSA99666.` The game still launches.

## App

- **Home:** a "Games to reconvert" section, shown only when at least one game needs reconversion. Each row: name, title ID, `Converted with build 3fd1cc3 · in use: release v0.1.2`, and a Reconvert button. The list is built when Home loads (`RefreshAsync`), so it follows an install, a mode switch or a change of data root. The buttons are disabled while AnyPS5 is not installed or while Home is checking.
- **Summary (already added game):** a Reconvert button beside "Open its folder", and a line: `Converted with the AnyPS5 in use.` or `Converted with build 3fd1cc3; the AnyPS5 in use is release v0.1.2.` Always enabled. Clicking it disposes the prepared game (deleting a zip's staging folder) before navigating, as Back does.
- **Reconverting:** new page, `ReconvertViewModel : StepRunViewModel`, same view shape as Adding: the six steps, progress, log, Cancel and Retry. Title: `Reconverting DOOM`.
  - Failure: the error's summary and details as in Adding, then `The previous conversion was kept; the game runs as before.` (or the restore-failed sentence above).
  - Cancel: the run stops, the restore runs, the page returns to Home.
  - Closing the window during the run goes through `IRunningWork`: confirm, cancel, wait for the restore, close.
- **Result:** `AddResultViewModel` and `AddResultView` are reused with the title `DOOM reconverted` and a `Before: 360 implemented, 4 stubs, 0 absent` line under the counts; no new view.

## Errors

| Case | Behaviour |
| --- | --- |
| The executable is locked (game running) | Step 1 fails with "Cannot set the old conversion aside: …" and "Close the game if it is running."; nothing changed. |
| The relinker or the audit fails | The previous conversion is restored; the step's summary and details are shown, then the "kept" sentence. |
| Cancel or Ctrl+C | The previous conversion is restored. |
| The restore fails | Logged at Error; `previous\` stays; the "restored on the next start or reconvert" sentence. |
| Malformed `game.json` | `reconvert` and `run` report it as today; Home skips the game and logs a Warning. |
| AnyPS5 not installed | `reconvert` reports it as `run` does; Home's buttons are disabled. |
| Same install | Allowed; the CLI says so first. |

## 6a follow-ups folded in

Taken from the PR #7 description because 6b touches the same code:

- **Step names in one place:** the app reads the step names from Core (`IGameImporter.StepNames`, `IGameReconverter.StepNames`) instead of repeating them.
- **`IsDisposed` guard:** `GameImporter.AddAsync(PreparedGame, …)` throws `ObjectDisposedException` for a disposed game.
- **Dropped hook exceptions:** `StepRunViewModel`'s `_ = RunAsync()` no longer drops exceptions thrown by the success and cancel hooks; they are logged at Error and shown as the page's error.

The other follow-ups (staging sweep at startup, `PathToBitmapConverter`, a shared step-run view, Home's hint while checking, the spin-wait in `PreparedGame.DisposeAsync`, Retry after a concurrent add, closing during a run navigating Home, `DragOver`) stay for milestones 7 and 7b.

## Tests

- **Core** (temporary game folder, fake process runner):
  - After a Reconvert, every "never touched" file is byte-identical; a library present only in the old `libs\` is gone; `game.json` keeps `Settings`, `AddedAt` and `LastPlayedAt` and has the new `ConvertedWith`, `Audit` and `ReconvertedAt`; the relinker gets the Intel option when `ToIntel` is true.
  - A relinker failure, an audit exit code 2 and a cancellation each leave exactly the previous files (names and contents) and the previous `game.json`.
  - Recovery: `previous.partial\` with some items moved; `previous\` with partial new outputs; both restore the previous state.
  - A locked executable: the run fails at step 1 with the "Close the game" details and nothing changes.
  - `NeedsReconversion` for each field (Profile, Mode, Version, Commit) and for identical installs; `All()` skips a malformed record and orders by name.
  - `ReconvertedAt` absent in an older `game.json` reads as null.
- **CLI:** `reconvert` output, the "Before" line, the "already converted" line, exit codes 0, 1 and 130; the `run` warning.
- **App view models:** Home's list (hidden when empty, rows, disabled while not installed or checking); Summary's Reconvert line and button, which disposes the prepared game; the Reconverting page (failure text with the "kept" sentence, Cancel returns Home after the restore, close handling); the Result title and "Before" line; a hook exception in `StepRunViewModel` shown and logged.

## Criterion

By hand, on the AMD laptop, on a fresh data root:

1. With the build install, add DOOM: orange, 360 implemented, 4 stubs, 0 absent.
2. `ps5kitsune run PPSA99666` once so the game creates `data\` and `_trophy\`; record a hash of every file in `data\`, `_trophy\`, `shader_cache\` and of `anyps5-input.ini` (if present).
3. `ps5kitsune use release`: Home lists DOOM under "Games to reconvert".
4. Reconvert from Home: orange, 361, 3, 0, with `Before: 360 implemented, 4 stubs, 0 absent`; the hashes of step 2 are unchanged; `.ps5kitsune\previous\` is gone.
5. `ps5kitsune run PPSA99666`: DOOM starts, with no reconversion warning.
6. Switch back to the build, start a Reconvert from the app and Cancel it during "Convert with the relinker": the game is back to its release conversion (`game.json` says release v0.1.2), and it runs.
7. `ps5kitsune reconvert PPSA99666` on the build: 360, 4, 0, with `Before: 361 implemented, 3 stubs, 0 absent`.

## Docs

- `docs/architecture.md`: the Reconvert paragraph describes the set-aside and restore, the `ReconvertedAt` field joins the game record, the roadmap marks 6b, and "Screens" mentions Home's list and the Reconverting page.
- `README.md`: the status line, and one sentence on `ps5kitsune reconvert` and the app's Reconvert.
