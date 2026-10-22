# Milestone 6a — Add a game from the app: design

October 22, 2026 · Complements [docs/architecture.md](../../architecture.md) ("V2 — Add a game", milestone 6 of the roadmap) and the [milestone 2 spec](2026-10-19-milestone-2-cli-path-design.md).

## Goal

Add a game from the app: choose a folder or a `.zip`, see its summary before anything is copied, run the add steps with progress, and read the audit verdict with the functions that are not implemented. The add logic of milestone 2 is reused; Core gains a separate preparation step so the summary comes before the copy.

Milestone 6 is split, with the user's agreement: **6a** (this spec) adds a game from the app; **6b** adds Reconvert (Core, CLI, app).

**Out of scope:** Reconvert (6b); moving the game instead of copying it (deferred to milestone 7, with the library); launching from the app (milestone 7); a games list in the app.

## Facts checked on 2026-10-22

- `GameImporter.AddAsync(input, toIntel, …)` checks the AnyPS5 install first, then resolves, validates and adds in one call; it returns `GameAdded`, `GameRejected` or `GameAlreadyAdded`. Its `finally` deletes the staging folder of a zip when the copy never started or finished, and keeps it when the copy started and did not complete (`GameImporter.cs`).
- `GameSourceResolver` throws `InvalidGameSourceException` ("No eboot.bin found in …") for a folder without `eboot.bin`, before validation; the validator's own "eboot.bin is missing" issue is therefore not reached through the resolver.
- Step names: "Copy the game", "Copy the AnyPS5 libraries", "Unwrap SELF files", "Convert with the relinker", "Audit the imports", "Record the game".
- `UnwrapSelfStep` and `RelinkStep` throw `GameImportException` with a targeted sentence ("the game must come already unwrapped from the dump tool"; with the Intel option, "the game may use an AMD-only instruction that cannot be converted for Intel CPUs") followed by a newline and the process's output tail, all in one message.
- `GameAdded` holds the `GameRecord` only; `GameRecord.Audit` keeps the counts, not the import list.
- `audit.json` (import_audit.py `--json`) has an `imports` array; each entry has `nid`, `name` (may be null), `library` (may be null), `module` (set on some entries where `library` is null), and `class` (`implemented`, `stub`, `absent`, `module`). DOOM for PS5 v1.1.1 with commit 3fd1cc3 has 4 stub entries, so 4 lines on the result screen: `sceSystemServiceLoadExec` (libSceSystemService.prx), `fscanf_nid_postfix` (libSceLibcInternal.prx), `__tls_get_addr_nid_postfix` (libkernel.prx) and `__tls_get_addr_nid_postfix` (libc.prx, a module).
- `GameSourceResolver` extracts a zip again on every call, and `ZipExtraction` extracts into `<staging>.staging` then renames it, deleting any previous target first; it does not clean up on an exception. A staging folder kept after a failed add is therefore never reused.
- `AuditStep` also builds its failure message as a sentence, a newline and the script's output tail.
- The app's `IDialogService` has `PickFolderAsync` and `ConfirmAsync` only; no view handles drag and drop. `ICpuInfo` is registered in Core's services, so a session can resolve it.
- The close confirmation is tied to `InstallViewModel` (`MainWindowViewModel.IsInstallRunning` and its cancel, and `MainWindow.axaml.cs`).
- DOOM audits orange with both the validated release (361 implemented, 3 stubs, 0 absent) and the build (360, 4, 0), so the roadmap's "green audit" criterion cannot be met; it becomes "the same verdict as the CLI".

## Core

**Importer interface.** `IGameImporter` (in `Core.Games`), implemented by `GameImporter` and registered as a singleton, so the app tests use a fake as they do with `IAnyPS5Installer`:

- `Task<PreparedGame> PrepareAsync(string input, CancellationToken)`;
- `Task<GameAdded> AddAsync(PreparedGame game, bool toIntel, IProgress<StepProgress>?, Action<StepEvent>?, CancellationToken)`;
- `IReadOnlyList<string> StepNames` (the six names, in order).

**Preparation.** `PrepareAsync`:

- runs its whole body on the thread pool (`Task.Run`): resolving a folder, extracting a zip, computing the size and reading the binaries are synchronous today;
- resolves the input (a folder, or a `.zip` extracted into staging) and validates it, as `AddAsync` does today;
- throws `InvalidGameSourceException` for a path that is neither a folder nor a `.zip`, or a folder or zip without `eboot.bin` (the resolver's message), and `InvalidGameSourceException` with "Cannot read {path}: {reason}" when a zip cannot be read or extracted;
- on any exception, including a cancellation and "No eboot.bin", deletes both `<staging>` and `<staging>.staging` before rethrowing;
- honours the cancellation token during the extraction (the existing per-entry copy already does) and once before validation; validation, the size and the binaries are not cancellable, and no token is added to `GameValidator`.

**Prepared game.** `PreparedGame` (in `Core.Games`), `IAsyncDisposable`, with a public constructor so tests and fakes can build one:

- `Source` (`GameSource`), `Validation` (`GameValidation`: metadata, module folders, size, issues), `IsBlocked`;
- `IconPath`: the full path of `sce_sys/icon0.png` when it exists, else null;
- `Binaries`: the ELF, SELF or other state of `eboot.bin` and of every file of the module folders (from `BinaryKinds`), as (relative path, kind) pairs;
- `GameDirectory`: `<root>\games\<titleId>` (null without metadata); `AlreadyAdded`: its `game.json` exists; `Resumes`: the folder exists without `game.json` (a previous add stopped);
- `DisposeAsync()` deletes the staging folder (`<staging>` and `<staging>.staging`) on the thread pool, so the UI never blocks on a large tree. Since a kept staging folder is never reused (see Facts), it always deletes it, which replaces today's keep-on-interrupted-copy rule; the add's resume works from the copy in the game folder. Without a staging folder it does nothing; disposing twice does nothing. A deletion failure is logged and ignored, as today.

**Add.** `AddAsync(PreparedGame, …)`:

- throws `AnyPS5NotInstalledException` when AnyPS5 is not installed, and `InvalidOperationException` for a blocked or already added game;
- runs the existing six steps; the staging folder is handled by `DisposeAsync`, not by `AddAsync`;
- returns `GameAdded` with a new `Audit` member (`AuditSummary`, from the audit step's context), so the result screen can list the unresolved functions.

The old `AddAsync(string input, toIntel, …)` keeps its order: the install check first, then prepare, then the checks that return `GameRejected` or `GameAlreadyAdded`, then the new `AddAsync`, then dispose. Its results and the CLI's output do not change; the only difference is that an interrupted copy no longer leaves the extracted zip behind.

**Failure details.** `GameImportException` gains `Summary` and `Details`, like `BuildCommandException`, with a `GameImportException(string summary, string? details = null)` constructor: `Message` stays `Summary + newline + Details` when there are details, else `Summary` (the CLI's output does not change). `UnwrapSelfStep`, `RelinkStep` and `AuditStep` put their sentence in `Summary` and the exit code line and output tail in `Details`.

**Unresolved functions.** `AuditSummary` gains `Unresolved`: a list of `UnresolvedImport(string Name, string Where, AuditClass Class)` for every `imports` entry whose class is `stub` or `absent`, in file order, where `Name` is `name` or else `nid`, `Where` is `library` or else `module` or else "", and `AuditClass` is `Stub` or `Absent`. Entries with the same name and place appear once. A file without `imports` gives an empty list. `game.json` does not store the list.

## App

```text
Home → Choose a game → Summary → Adding → Result
```

Pages travel through `IPages`: `ChooseGame()`, `GameSummary(PreparedGame)`, `AddGame(PreparedGame, bool toIntel)`, `AddResult(GameAdded)`.

**Who owns the prepared game.** The page that holds it disposes it (`await DisposeAsync()`, on the UI thread's continuation, never in a posted callback) when leaving the flow: Summary on Back; Adding in its run's `finally` on success or cancel, and on "Back to home" after a failure. Leaving to Home always goes through `Navigator.GoHome`, which clears the back stack, so no old page keeps a disposed game reachable. A preparation that completes after Choose's Cancel, or after the page was left, disposes its result at once.

**Running work and the close confirmation.** `MainWindowViewModel` and `MainWindow` stop referring to `InstallViewModel` and use two interfaces:

- `IRunningWork { bool IsRunning; string? QuitMessage; Task CancelAndWaitAsync(); }`, implemented by Install, Choose (while preparing) and Adding. A non-null `QuitMessage` asks for confirmation first (Install's message stays as it is; Adding's is below); null (Choose) cancels and waits without asking.
- `IPreparedGameOwner { Task ReleaseAsync(); }`, implemented by Summary and Adding, which dispose their prepared game.

On window close, the main window cancels and waits for the current page's running work (after confirmation when asked), then awaits `ReleaseAsync()` when the page owns a prepared game, then closes. Choose and Adding set `sessions.IsBusy` while running, as Install does, so the data root cannot change meanwhile.

**Dialogs.** `IDialogService` gains `Task<string?> PickFileAsync(string title, string extension)` (the zip picker, filtering `*.zip`); `FakeDialogs` supports it.

**Home.** A new "Add a game" button, enabled when the install check finished and the status is Installed; otherwise disabled with "Install AnyPS5 first to add games." under it.

**Choose a game.**

- Title "Add a game"; text "Choose the game's folder or its .zip, in the DOOM for PS5 format, or drop it here."
- Buttons "Choose a folder…" and "Choose a .zip…", and a drop zone accepting one folder or one `.zip` file. The view's drop handler (`DragDrop` in `ChooseGameView`) passes the path to the same view-model method as the buttons; the drop itself is checked by criterion 1 only.
- While preparing: "Reading the game…" with an indeterminate bar and a Cancel button; the other buttons are disabled.
- An `InvalidGameSourceException` stays on the page and shows its message.
- Back returns to Home.

**Summary.**

- The icon (64×64, or a neutral placeholder): the view loads the file into a bitmap in memory, so deleting the staging folder later cannot break it, and view-model tests need no image.
- The name, the title ID, the size (`{n} MB`, bytes / 1,048,576 with one decimal, as the app's downloads), and "{n} module folders" / "No module folder".
- Binaries: "No SELF file." or "{n} files are SELF and will be unwrapped." (with `eboot.bin` named when it is one of them).
- Issues: blocking ones in red, warnings in orange, each on its own line.
- Already added: "{titleId} is already in your games ({folder})." with an "Open its folder" button; "Add the game" is disabled.
- Resumes: "A previous add of this game stopped; adding it resumes where it stopped. The Intel option only applies if the conversion has not run yet."
- Checkbox "Convert for an Intel CPU", ticked when the CPU is Intel (`ICpuInfo`).
- Buttons Back (disposes the prepared game and returns to Choose) and "Add the game" (enabled when not blocked and not already added), which opens Adding.

**Adding.**

- The six step rows, the log, Cancel, Retry, "Open log", and the failure line "{Step} failed: {Summary}" with "Show details" holding `Details` when the exception is a `GameImportException` with details, as on Install. Every step shows an indeterminate bar while it runs, until it reports progress.
- Cancel: waits for the run to stop, disposes the prepared game, then goes Home (as Install does).
- After a failure: Retry reruns from the failed step; "Back to home" disposes the prepared game and goes Home.
- AnyPS5 not installed: the page catches `AnyPS5NotInstalledException` and shows "AnyPS5 is not installed. Install it from Home first." with "Back to home" and no Retry. The exception's own message, used by the CLI, does not change.
- Closing the window during an add asks for confirmation ("Adding a game is in progress. Quitting cancels it; adding it again resumes where it stopped.").
- On success: disposes the prepared game and opens Result.

**Result.**

- Verdict line in its colour: "Green: the game can load.", "Orange: the game loads but will stop if it calls an unimplemented function.", "Red: not playable yet; wait for an AnyPS5 update."
- Counts: "{implemented} implemented, {stub} stubs, {absent} absent".
- Missing libraries, when the audit reports any: "Missing libraries: {name}, {name}" (a red verdict can come from them alone).
- Expander "Show the unresolved functions ({n})", hidden when there are none: one line per function, "{name} — {where} ({stub|absent})".
- Validation warnings from `GameAdded.Warnings`, if any.
- Buttons "Open the game folder", "Add another game" (`GoHome(Home)` then `GoTo(ChooseGame)`), "Back to home".

**Shared step list.** Install and Adding show the same step list. The rows, log, progress, Cancel, Retry, failure details and `CancelAndWaitAsync` move from `InstallViewModel` into an abstract base class `StepRunViewModel` that both pages inherit, so the members the views and tests use (`Steps`, `Log`, `Failure`, `ShowsProgress`, …) stay where they are. The page-specific parts are abstract or virtual members:

- `IReadOnlyList<string> StepNames()` (Install: from `CreateSteps(mode)` on every run, as today; Adding: `IGameImporter.StepNames`);
- `Task RunCoreAsync(IProgress<StepProgress>, Action<StepEvent>, CancellationToken)`;
- `(string Summary, string? Details) SplitFailure(Exception)` (Install: `BuildCommandException`, and its "The install failed: …" prefix; Adding: `GameImportException`, and the "not installed" case);
- `bool CanRetry(Exception)` (false for `AnyPS5NotInstalledException`);
- `OnSucceeded()` and `OnCancelled()` (Install: Done and Home, as today; Adding: Result and Home, after disposing);
- `bool IndeterminateOnStart` (Install: build mode only, as today; Adding: always).

Install's behaviour and texts do not change, and its existing tests keep passing unchanged.

## Errors

- Prepare: see Core; the Choose page shows the message. A cancelled preparation returns to the Choose page's idle state.
- AnyPS5 not installed when adding: see Adding.
- A failed step: "{Step} failed: {Summary}", details under "Show details", Retry.
- The window closing while preparing or adding: confirmation for an add, then cancel, wait, dispose.

## Tests

No test uses the network, the compiler or Python (the process runner is faked, as today).

- Core:
  - `PrepareAsync` with a folder and with a zip: metadata, icon path, size, module folders, binaries, issues, `AlreadyAdded`, `Resumes`;
  - a path that is neither; a folder without `eboot.bin`; a zip without `eboot.bin`, an unreadable zip and a cancelled preparation (each leaves neither `<staging>` nor `<staging>.staging`);
  - `DisposeAsync` deletes the staging folder, does nothing without one, and can be called twice;
  - `AddAsync(PreparedGame, …)` refuses a blocked game and an already added game, requires the install, and returns the audit summary;
  - the old `AddAsync(string, …)` results and order are unchanged (existing tests, plus one for "not installed" before resolving);
  - `GameImportException`: `Summary`, `Details`, and `Message` unchanged for the unwrap, relink and audit failures;
  - `AuditSummary.Unresolved` from a fixture shaped like DOOM's `audit.json`, with deduplication, and an empty list without `imports`;
  - `StepNames` matches the steps run.
- App (with a fake `IGameImporter`):
  - Home: "Add a game" enabled only when installed and not checking;
  - Choose: a folder or a zip opens Summary; an invalid path shows its message; busy state and Cancel; a result arriving after Cancel is disposed;
  - Summary: binaries, issues, already added and resume texts, the Intel default from the CPU, Add disabled when blocked or already added, Back disposes;
  - Adding: step rows, indeterminate start, cancel (disposes, goes Home), retry, failure details, "not installed" without Retry, success disposes and opens Result;
  - Result: the three verdicts' texts, the counts, missing libraries, the unresolved list and its hidden state, the buttons' targets;
  - Main window (existing `MainWindowViewModelTests` updated to the new interfaces): confirmation and cancel through `IRunningWork` for Install and Adding, cancel without confirmation for Choose, `ReleaseAsync` on close for Summary and for Adding after a failure;
  - Install: existing tests pass after the `StepRunViewModel` extraction;
  - Page factory: the new pages resolve from the real container.

## Criterion

Run by the user on the AMD laptop after the tests pass:

1. On a root with AnyPS5 installed, drop `C:\PS5Games\PPSA99666.zip` on Choose. Summary shows the DOOM icon, the name, PPSA99666, the size and module folders, and Intel unticked. Add runs every step; Result shows the same verdict and counts as the CLI (orange, the 4 stubs listed). `ps5kitsune run PPSA99666` starts the game.
2. Adding the same zip again: Summary says it is already in your games, with "Open its folder".
3. On another root with AnyPS5 installed, close the window during "Convert with the relinker" and confirm. Reopen and add the zip again: Summary says a previous add stopped; the steps already done are skipped and Result appears.
4. Choose `C:\PS5Games` (no `eboot.bin`): Choose shows "No eboot.bin found in …". Then choose a copy of the extracted game with `sce_sys\param.json` deleted: Summary shows the blocking issue in red and "Add the game" is disabled.

## Docs

- `docs/architecture.md`:
  - roadmap: milestone 6 split into 6a (this spec, linked) and 6b (Reconvert); criterion "the same verdict as the CLI" instead of "green audit";
  - V2 screens as built: options on the Summary, copy only (Move deferred to milestone 7), no "Add to library" button (the game is registered when `game.json` is written), the unresolved functions and missing libraries on Result.
- `README.md`: adding a game from the app.

## Review before planning

An independent review of the first draft (2026-10-22) found: criterion 4 unreachable (the resolver rejects a folder without `eboot.bin` first), the staging rule stated backwards, no audit list in `GameAdded`, preparation not actually off the UI thread, no file picker or drop support, no importer interface for app tests, the close confirmation tied to Install, gaps in the prepared game's lifetime, failure details mixed into the message, and the old `AddAsync` order. The second version addressed each of them.

A second review (2026-10-22) confirmed those fixes and found: the shared step list could not keep Install's tests as a separate object (now an abstract base class), an unneeded copy-state signal for the staging rule (now always deleted, since staging is never reused), the `.staging` folder and the "no eboot.bin" zip left behind, the dispose blocking the UI thread and racing the window close (now `DisposeAsync`, awaited, and `IPreparedGameOwner`), no close handling for a page holding a game without running work, a cancel-versus-completion race on Choose, and `AuditStep`'s message. This version addresses them. It also noted that 6a is at the upper bound of one milestone; drag and drop and "Open its folder" stay in, as chosen.
