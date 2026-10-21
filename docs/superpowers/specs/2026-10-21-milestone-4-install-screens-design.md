# Milestone 4 — Install screens, release mode: design

October 21, 2026 · Complements [docs/architecture.md](../../architecture.md) (milestone 4 of the roadmap, "V1b — Install screens, release mode").

## Goal

Install AnyPS5 from the app, with no command line: home, PC check, mode choice, install progress, done. The result is the same install the CLI produces.

**Out of scope:** build mode and the Tools screen (milestone 5), downloading new profiles from the repository and the "new validated version available" notice (open decision in the architecture), Lavapipe and `ANYPS5_GPU` (milestone 7), adding a game (milestone 6).

## Flow

```text
Home ──Install / Resume / Verify──► PC check ──Continue──► Mode ──Install──► Install ──► Done
  ▲                                 (disabled on Blocking)  (Release only)      │  failure: Retry
  └──────────────── Back (from every page except during the install) ◄─────────┘  cancel: back to Home
```

The app opens on Home (today it opens on the PC check).

## Screens

UI text is in English.

**Home**

- The data root, with a "Change…" button (folder picker). See "Data root".
- The active profile and whether it is validated.
- The AnyPS5 state, from `AnyPS5Installer.GetState()`:

| State | Shown | Button |
| --- | --- | --- |
| `NotInstalled` (no `install.json`, no step done) | "AnyPS5 is not installed" | "Install AnyPS5" |
| `Installed` (every step done) | Version, profile and install date from `install.json` | "Verify / repair" |
| `Incomplete` (some steps done, or `install.json` for another profile or tag) | "The AnyPS5 install is incomplete" | "Resume the install" |

Every button leads to the PC check, then Mode, then Install. "Verify / repair" runs the same pipeline: completed steps are skipped, so it is almost instant when nothing is wrong.

**PC check**

The milestone 3 screen, plus "Back" and "Continue". "Continue" is disabled while checking and when the report has a Blocking result; warnings leave it enabled (the banner already shows them). The checks start when the page is entered, through the navigation, not from `App.axaml.cs`. Cancelling a check no longer leaves "Checking this PC…" shown: the verdict reads "Check cancelled" and "Check again" stays available.

**Mode**

Two cards:

- **Release** (selected): the profile's tag, its publication date (`source.publishedAt`), whether the profile is validated, and "A few minutes, no compilation".
- **Build** (disabled): "Available in a later version". Milestone 5 enables it.

"Back" and "Install".

**Install**

- The list of steps, each pending, running, skipped, done, failed or cancelled.
- A progress bar for the file being downloaded (bytes received out of total; indeterminate when the size is unknown).
- A scrolling log built from the installer's own output: one line per `StepEvent` (started, already done, done in N s, failed with the cause's message, cancelled) and one per `StepProgress.Message`. Download progress goes to the bar, not to the log. The full log stays in the NLog file.
- "Cancel" and "Open log" (opens the session's log file, `<root>\logs\app-<yyyyMMdd-HHmmss>.log`; `LoggingSetup` exposes the path it chose).
- On failure: the failed step and the cause's message, "Retry" (runs the install again; completed steps are skipped), "Back to home", "Open log".
- On cancel: back to Home, which recomputes the state (`Incomplete`).
- On success: Done.

**Done**

The installed version, the relinker path and the libs folder, "Open folder" (`anyps5\current`), "Open log", "Back to home". No "continue" link yet: adding a game comes with milestone 6.

**Closing the window during an install**

`Window.Closing` is cancelled and a confirmation asks: "An install is running. Quitting cancels it; it will resume where it stopped." On confirmation the install's token is cancelled, the app waits for the running step to stop (a cancelled download leaves only its `.partial` file), then closes. Closing at any other time needs no confirmation.

## Data root

Today the app always uses `platform.DefaultDataRoot` (`C:\PS5Kitsune`) and only the CLI takes `--root`.

- **Settings file:** `<IPlatform.SettingsDirectory>\settings.json`, outside the data root (`%LOCALAPPDATA%\PS5Kitsune` on Windows). Contents: `{ "schemaVersion": 1, "dataRoot": "D:\\PS5Kitsune" }`. A missing `dataRoot` means the default root. A file that cannot be read or parsed falls back to the default root, with a warning in the log; it is overwritten on the next change.
- **App:** "Change…" opens a folder picker. The chosen folder is created if needed; if it cannot be created or written, Home shows the error and keeps the current root. Otherwise the setting is saved, the session is replaced (see "Sessions") and Home recomputes the state. The `install-path` and `disk-space` checks judge the new root on the next PC check.
- **CLI:** `CommandHost` resolves the root as `--root`, then the setting, then the default. The app and the CLI therefore work on the same root without `--root`.

## Components

### Core

- **`AnyPS5InstallState`**: `record AnyPS5InstallState(AnyPS5InstallStatus Status, AnyPS5InstallRecord? Record)`, with `enum AnyPS5InstallStatus { NotInstalled, Installed, Incomplete }`. `AnyPS5Installer.GetState()` reads `install.json` (null when missing or unreadable) and the steps' done-checks: all done gives `Installed`; none done and no record gives `NotInstalled`; anything else gives `Incomplete`. A record for another profile or tag makes `WriteInstallRecordStep` not done, so it is `Incomplete`. `IsInstalled()` stays and becomes `GetState().Status == Installed`.
- **`StepRunner`**, two follow-ups from milestone 2:
  - `IsAlreadyDone()` runs inside the `try`: an exception there is logged and reported as `Failed`, then thrown as `StepFailedException`.
  - New `StepEventKind.Cancelled`: when the token is cancelled during a step, the runner emits `Cancelled` for that step before rethrowing `OperationCanceledException`. A cancellation between steps emits nothing (no step was running).
- **Profile:** `source.publishedAt` (optional `DateTimeOffset`), set to `2026-10-14T08:56:52Z` for `v0.1.2` in `doom-validated-2026-10`. The Mode card shows "Publication date unknown" when it is missing. No GitHub API call.
- **`IPlatform`** gains:
  - `string SettingsDirectory`: `%LOCALAPPDATA%\PS5Kitsune` on Windows.
  - `void OpenPath(string path)`: opens a folder or a file with the shell (`explorer.exe` for folders, `UseShellExecute` for files).
- **`AppSettings`** (`record AppSettings(int SchemaVersion, string? DataRoot)`) and **`AppSettingsStore`** (`Load()`, `Save(AppSettings)`), in Core so the CLI reads the same file. `Save` writes a temporary file and moves it into place.

### App

- **`Navigator`**: holds `CurrentPage` (observable) and exposes `GoTo(object page)` and `GoHome()`. Entering a page calls its `OnNavigatedTo()` when it implements `INavigationAware`; that is where the PC check starts and where Home recomputes the state. `MainWindowViewModel` shows the header (platform, data root, profile) and binds to `Navigator.CurrentPage`; page views are picked by `DataTemplate`s, as today.
- **`RootSession`**: for one data root, builds a `ServiceProvider` with `AddPS5KitsuneLogging(<root>\logs, "app")` and `AddPS5KitsuneCore(layout, profile)`, plus the platform. It is disposed when replaced, which flushes its log. **`SessionHost`** holds the current session and replaces it when the root changes; it refuses to while an install runs (the "Change…" button is on Home, so this cannot happen through the UI, but the guard is there).
- **App container** (root-independent): `IPlatform`, the profile, `AppSettingsStore`, `SessionHost`, `Navigator`, `IDialogService`, `MainWindowViewModel`.
- **Page view models**, created from the current session through a small factory: `HomeViewModel`, `SystemCheckViewModel` (existing, plus Back and Continue), `ModeViewModel`, `InstallViewModel`, `DoneViewModel`.
- **`InstallViewModel`**: runs `AnyPS5Installer.InstallAsync` with `onEvent` and `progress`, and posts each update to the UI thread. Exposes `IsRunning` for the window's close guard and `CancelAndWaitAsync()`.
- **`IDialogService`**: `PickFolderAsync(string? start)` (Avalonia `StorageProvider`) and `ConfirmAsync(string title, string message)` (a small modal window). Faked in tests.
- `OpenPath` and `OpenFix` failures (for example a `Win32Exception`) are caught in the view model and shown on the page instead of crashing the app (milestone 3 follow-up).

### CLI

`CommandHost` reads `AppSettingsStore` to resolve the root. Nothing else changes.

## Errors

| Situation | Behavior |
| --- | --- |
| A step fails | Install page shows the step and the cause's message; Retry, Back to home, Open log |
| The user cancels | `Cancelled` event, back to Home, state `Incomplete` |
| The window closes during the install | Confirmation, then cancel, wait, close |
| The chosen root cannot be created or written | Error on Home, current root kept |
| `settings.json` unreadable | Default root, warning in the log |
| Opening a folder, a file or a fix fails | Message on the page |
| An exception escapes a done-check | Reported as that step's failure |

## Tests

**Core.Tests**

- `GetState()`: empty root (`NotInstalled`); every step's output present with the profile's hashes and `install.json` written (`Installed`); one file missing (`Incomplete`); `install.json` for another tag (`Incomplete`). Uses small fake assets and a test profile with their hashes.
- `StepRunner`: a done-check that throws gives `Failed` and `StepFailedException`; cancelling during a step gives `Cancelled` and `OperationCanceledException`; cancelling before the first step emits nothing.
- `AppSettingsStore`: missing file, corrupt file, round trip.
- `ProfileLoader`: reads `publishedAt`; the embedded profile has it.
- Root resolution: `--root` beats the setting, the setting beats the default.

**App.Tests**

- `Navigator`: `GoTo` changes `CurrentPage` and calls `OnNavigatedTo`.
- `HomeViewModel`: the text and button for each state; "Change…" with a fake dialog saves the setting and replaces the session; an unwritable folder keeps the root and shows the error.
- `SystemCheckViewModel`: "Continue" disabled with a Blocking result, enabled with only warnings; cancel shows "Check cancelled".
- `InstallViewModel` with fake steps: success goes to Done; failure shows the step and Retry resumes; cancel goes back to Home; `IsRunning` and `CancelAndWaitAsync()`.
- `SessionHost`: replacing the session disposes the old one.

**Platform.Windows.Tests:** `SettingsDirectory` ends with `PS5Kitsune` and is under `LocalApplicationData`.

## Criterion

On the AMD laptop:

1. In the app, choose an empty root, then Install AnyPS5 → PC check → Mode (Release) → Install → Done.
2. `ps5kitsune install` with no `--root`: every step is "already done". The done-checks compare the profile's hashes, so the CLI sees the same install.
3. `ps5kitsune install --root <another empty root>`, then compare both trees and `install.json` (all fields but `installedAt`).
4. Cancel during a download, check that Home shows "incomplete", resume, and check that finished steps are skipped. Close the window during an install and confirm.

## Refinements from the implementation plan

1. `IAnyPS5Installer` (Core) is extracted from `AnyPS5Installer` (`CreateSteps`, `GetState`, `InstallAsync`) so the app's view models can be tested with a fake. `IsInstalled()` keeps its implementation, which gives the same answer.
2. No app-level container: `App.axaml.cs` is a plain composition root. Only `RootSession` builds a `ServiceProvider`.
3. `IPlatform.SettingsDirectory` already existed, unused, under Roaming `ApplicationData`; it moves to `LocalApplicationData`.
4. "Back" returns to the previous page through the navigator's back stack. The PC check runs by itself only the first time its page is entered, or after a cancelled check.
5. Root helpers live in a static `DataRoots` class in Core: `Resolve` and `Prepare`.
6. `AppSettingsStore.Load()` returns the settings and an optional problem text, which the host logs once logging exists.
