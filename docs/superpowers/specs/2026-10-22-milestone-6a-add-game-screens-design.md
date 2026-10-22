# Milestone 6a — Add a game from the app: design

October 22, 2026 · Complements [docs/architecture.md](../../architecture.md) ("V2 — Add a game", milestone 6 of the roadmap) and the [milestone 2 spec](2026-10-19-milestone-2-cli-path-design.md).

## Goal

Add a game from the app: choose a folder or a `.zip`, see its summary before anything is copied, run the add steps with progress, and read the audit verdict with the functions that are not implemented. The add logic of milestone 2 is reused; Core gains a separate preparation step so the summary comes before the copy.

Milestone 6 is split, with the user's agreement: **6a** (this spec) adds a game from the app; **6b** adds Reconvert (Core, CLI, app).

**Out of scope:** Reconvert (6b); moving the game instead of copying it (deferred to milestone 7, with the library); launching from the app (milestone 7); a games list in the app.

## Facts checked on 2026-10-22

- `GameImporter.AddAsync(input, toIntel, …)` resolves, validates and adds in one call; it returns `GameAdded`, `GameRejected` or `GameAlreadyAdded`, and deletes the staging folder of a zip unless the copy did not complete.
- Step names: "Copy the game", "Copy the AnyPS5 libraries", "Unwrap SELF files", "Convert with the relinker", "Audit the imports", "Record the game".
- Targeted messages already exist: `UnwrapSelfStep` says the game "must come already unwrapped from the dump tool"; `RelinkStep` adds, with the Intel option, that "the game may use an AMD-only instruction that cannot be converted for Intel CPUs". They are reused as they are.
- `audit.json` (import_audit.py `--json`) has an `imports` array; each entry has `nid`, `name` (may be null), `library` (may be null), `module` (on some entries), and `class` (`implemented`, `stub`, `absent`, `module`). DOOM for PS5 v1.1.1 with commit 3fd1cc3 has 4 stub entries: `sceSystemServiceLoadExec` (libSceSystemService.prx), `fscanf_nid_postfix` (libSceLibcInternal.prx), `__tls_get_addr_nid_postfix` (libkernel.prx, and libc.prx as a module).
- DOOM audits orange with both the validated release (361 implemented, 3 stubs, 0 absent) and the build (360, 4, 0), so the roadmap's "green audit" criterion cannot be met; it becomes "the same verdict as the CLI".

## Core

**Preparation.** `GameImporter.PrepareAsync(string input, CancellationToken)` returns a `PreparedGame`:

- it resolves the input (a folder, or a `.zip` extracted into staging) and validates it, as `AddAsync` does today;
- it throws `InvalidGameSourceException` for a path that is neither a folder nor a `.zip`, and `InvalidGameSourceException` with "Cannot read {path}: {reason}" when a zip cannot be read or extracted (the staging folder is deleted first);
- extraction and the size computation run off the calling thread's synchronization context (`ConfigureAwait(false)`), so the app can await it from the UI.

`PreparedGame` (in `Core.Games`), `IDisposable`:

- `Source` (`GameSource`), `Validation` (`GameValidation`: metadata, module folders, size, issues);
- `IconPath`: the full path of `sce_sys/icon0.png` when it exists, else null;
- `Binaries`: the ELF or SELF state of `eboot.bin` and of every file of the module folders (from `BinaryKinds`), as (relative path, kind) pairs;
- `AlreadyAdded` and `GameDirectory`: whether `<root>\games\<titleId>\.ps5kitsune\game.json` exists, and that folder (null without metadata);
- `IsBlocked`: `Validation.IsBlocked`.
- `Dispose()` deletes the staging folder unless the copy step finished, with the existing rule and logging of `AddAsync`'s `finally`. Disposing twice does nothing.

**Add.** `GameImporter.AddAsync(PreparedGame game, bool toIntel, IProgress<StepProgress>?, Action<StepEvent>?, CancellationToken)` returns `GameAdded`:

- it throws `AnyPS5NotInstalledException` when AnyPS5 is not installed, and `InvalidOperationException` for a blocked or already added game;
- it runs the existing six steps; the staging folder is handled by `Dispose`, not by `AddAsync`.
- `GameImporter.StepNames` (static, the six names in order) lets a screen list the steps before the add starts.

The old `AddAsync(string input, …)` becomes prepare, then the checks that return `GameRejected` or `GameAlreadyAdded`, then the new `AddAsync`, then dispose; its results and the CLI's output do not change.

**Unresolved functions.** `AuditSummary` gains `Unresolved`: a list of `UnresolvedImport(string Name, string Where, AuditClass Class)` for every `imports` entry whose class is `stub` or `absent`, in file order, where `Name` is `name` or else `nid`, `Where` is `library` or else `module` or else "", and `AuditClass` is `Stub` or `Absent`. Entries with the same name and place appear once. A file without `imports` gives an empty list. `game.json` does not store the list; the result screen reads it from the summary returned by the add.

## App

```text
Home → Choose a game → Summary → Adding → Result
```

Pages travel through `IPages`: `ChooseGame()`, `GameSummary(PreparedGame)`, `AddGame(PreparedGame, bool toIntel)`, `AddResult(GameAdded)`.

**Home.** A new "Add a game" button, enabled when the install check finished and the status is Installed; otherwise disabled with "Install AnyPS5 first to add games." under it.

**Choose a game.**

- Title "Add a game"; text "Choose the game's folder or its .zip, in the DOOM for PS5 format, or drop it here."
- Buttons "Choose a folder…" and "Choose a .zip…" (the platform pickers; the zip picker filters `*.zip`), and a drop zone accepting one folder or one `.zip` file. The view's drop handler passes the path to the same view-model method as the buttons.
- While preparing: "Reading the game…" with an indeterminate bar; the buttons are disabled.
- An `InvalidGameSourceException` stays on the page and shows its message.
- Back returns to Home.

**Summary.**

- The icon (64×64, or a neutral placeholder), the name, the title ID, the size (`{n} MB`, one decimal), and "{n} module folders" / "No module folder".
- Binaries: "All files are unwrapped (ELF)." or "{n} files are SELF and will be unwrapped." (with `eboot.bin` named when it is one of them).
- Issues: blocking ones in red, warnings in orange, each on its own line.
- Already added: "{titleId} is already in your games ({folder})." with an "Open its folder" button; Add is disabled.
- Checkbox "Convert for an Intel CPU", ticked when the CPU is Intel (`ICpuInfo`).
- Buttons Back (disposes the prepared game and returns to Choose) and "Add the game" (enabled when not blocked and not already added), which opens Adding.
- When AnyPS5 is no longer installed at that point, Adding catches `AnyPS5NotInstalledException` and shows "AnyPS5 is not installed. Install it from Home first." as its failure line, with no Retry. The exception's own message, used by the CLI, does not change.

**Adding.**

- The six step rows, the log, Cancel, Retry, "Open log", and the failure line with "Show details" when the exception carries details, as on Install.
- Closing the window during an add asks for confirmation ("Adding a game is in progress. Quitting cancels it; adding it again resumes where it stopped."). A later add of the same game resumes: steps already done are skipped.
- On success it opens Result and disposes the prepared game.

**Result.**

- Verdict line in its colour: "Green: the game can load.", "Orange: the game loads but will stop if it calls an unimplemented function.", "Red: not playable yet; wait for an AnyPS5 update."
- Counts: "{implemented} implemented, {stub} stubs, {absent} absent".
- Expander "Show the unresolved functions ({n})", hidden when there are none: one line per function, "{name} — {where} ({stub|absent})".
- Validation warnings from `GameAdded.Warnings`, if any.
- Buttons "Open the game folder", "Add another game" (to Choose), "Back to home".

**Shared step list.** Install and Adding show the same step list. The rows, log, progress, Cancel, Retry and failure details move from `InstallViewModel` into a `StepRunViewModel` that both pages use; Install's behaviour and texts do not change, and its existing tests keep passing.

## Errors

- Prepare: see Core; the Choose page shows the message.
- AnyPS5 not installed when adding: the Adding page shows the failure as above.
- A failed step: "{Step} failed: {message}", with Retry; the existing targeted messages for an encrypted SELF and for an Intel conversion failure are shown as they are.
- The app closing with a prepared game not yet added: the prepared game is disposed on shutdown, which deletes its staging folder.

## Tests

No test uses the network, the compiler or Python (the process runner is faked, as today).

- Core:
  - `PrepareAsync` with a folder and with a zip: metadata, icon path, size, module folders, binaries, issues;
  - an already added game; a path that is neither; an unreadable zip;
  - `Dispose` deletes the staging folder before the copy and keeps it after the copy;
  - `AddAsync(PreparedGame, …)` refuses a blocked game, an already added game, and runs only when AnyPS5 is installed;
  - the old `AddAsync(string, …)` results are unchanged (existing tests);
  - `AuditSummary.Unresolved` from a fixture shaped like DOOM's `audit.json`, with deduplication, and an empty list without `imports`;
  - `StepNames` matches the steps run.
- App:
  - Home: "Add a game" enabled only when installed and not checking;
  - Choose: a folder or a zip opens Summary; an invalid path shows its message; the busy state;
  - Summary: binaries and issues texts, already added, the Intel default from the CPU, Add disabled when blocked, Back disposes;
  - Adding: step rows, cancel, retry, failure details, success opens Result;
  - Result: the three verdicts' texts, the counts, the unresolved list and its hidden state, the buttons' targets;
  - Install: existing tests pass after the `StepRunViewModel` extraction;
  - Page factory: the new pages resolve from the real container.

## Criterion

Run by the user on the AMD laptop after the tests pass:

1. On a root with AnyPS5 installed, drop `C:\PS5Games\PPSA99666.zip` on Choose. Summary shows the DOOM icon, the name, PPSA99666, the size and module folders, and Intel unticked. Add runs every step; Result shows the same verdict and counts as the CLI (orange, the 4 stubs listed). `ps5kitsune run PPSA99666` starts the game.
2. Adding the same zip again: Summary says it is already in your games, with "Open its folder".
3. Close the window during "Convert with the relinker" and confirm. Reopen and add the zip again: the steps already done are skipped and Result appears.
4. Choose a folder that is not a game (for example `C:\PS5Games`): Summary shows the blocking issues in red and Add is disabled.

## Docs

- `docs/architecture.md`:
  - roadmap: milestone 6 split into 6a (this spec, linked) and 6b (Reconvert); criterion "the same verdict as the CLI" instead of "green audit";
  - V2 screens as built: options on the Summary, copy only (Move deferred to milestone 7), no "Add to library" button (the game is registered when `game.json` is written), the unresolved functions list.
- `README.md`: adding a game from the app.
