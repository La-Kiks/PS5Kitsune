# Milestone 5b — Build mode UI: design

October 22, 2026 · Complements [docs/architecture.md](../../architecture.md) (milestone 5 of the roadmap) and the [milestone 5a spec](2026-10-21-milestone-5a-build-mode-design.md).

## Goal

Build AnyPS5 from source from the app: the Build card on Mode becomes selectable, a Tools screen shows what build mode will download, and the Install page shows the build's progress. Home stops sending build installs to the command line.

**Out of scope:** a "clean build" button, choosing the number of jobs, deleting `source\` or `build\`, and the 5a ledger's follow-up on markers keyed on the commit only.

## Facts checked on 2026-10-22

- Download sizes (`Content-Length`): Python 3.14.8 embeddable 12,601,679 bytes; MinGit 2.56.0.2 39,806,486 bytes; WinLibs r7 267,593,703 bytes. Total 320,001,868 bytes, shown as 305.2 MB (the app divides by 1,048,576, as for downloads).
- A full build root measured 3.2 GB in the 5a criterion run.
- `BuildCommandException.Message` holds the targeted message, the exit code and the 20-line output tail in one string.
- The app runs the PC check before Mode and always checks with `AnyPS5Mode.Release`; `PageFactory.Install()` hard-codes `AnyPS5Mode.Release`.

## Flow

```text
Home → Mode → PC check (chosen mode) → Tools (build only) → Install → Done
```

Mode now comes before the PC check, so the check runs for the chosen mode and matches `ps5kitsune install --mode <mode>`. Back works at every step.

The mode travels from page to page as a parameter, never in the session, so a data root change cannot leave a stale choice:

- `IPages.SystemCheck(AnyPS5Mode mode)`, `IPages.Tools()`, `IPages.Install(AnyPS5Mode mode)`.
- `PageFactory` no longer hard-codes `Release`.

## Home

- Every action ("Install AnyPS5", "Resume the install", "Verify / repair") opens Mode.
- `CanStart` and the "use the command line" text are removed. A build install keeps "(built from source)" in its title; an incomplete build reads "The AnyPS5 build is incomplete" with the usual resume detail.

## Mode

Two selectable cards. Clicking a card selects it: accent border and a "Selected" line, as the Release card shows today. The button reads "Continue" and opens `SystemCheck(selected)`.

- **Release {tag}:** unchanged text.
- **Build from source:**
  - "Commit {first 7 characters}"
  - the profile status ("Validated with DOOM for PS5" or "Not validated yet")
  - "About 15 minutes the first time, faster for updates."
  - "Needs about 10 GiB of free disk space." (the profile's `buildMinFreeDiskGiB`)
- **Preselection:** `installer.CurrentMode`, which is the mode of an unfinished install, else the mode in `install.json`, else Release. This is the CLI's resolution without `--mode`.
- **Profile without a `build` section:** the Build card is disabled and reads "This profile does not define a build." If `CurrentMode` is Build in that case, Release is preselected.

## PC check

`SystemCheckViewModel` takes the mode and calls `checker.RunAsync(mode)`. "Continue" opens `Tools()` in build mode and `Install(Release)` in release mode. Nothing else changes.

## Tools (build only)

- **Rows:** Python, MinGit, WinLibs. Each shows a display name, the profile version and a badge: "Installed" (green) or "Missing" (grey) with its download size.
- **Totals:**
  - "To download: 305.2 MB", the sum of the missing tools' sizes. When a missing tool has no size, its row reads "size unknown" and the total reads "To download: at least …". With nothing missing: "All tools are present."
  - "Disk space for the build: about 3 GB."
- **Button:** "Download what's missing and build", or "Build" when nothing is missing. It opens `Install(Build)`, whose first steps download the tools. Back returns to the check.
- **Data:** Core's `BuildToolsSummary` (in `Core.Tools`) returns, for the tools build mode installs, a `PortableToolInfo(Id, DisplayName, Version, SizeBytes?, IsInstalled)`. `IsInstalled` comes from `IPortableTool.IsInstalled()`, the same check the install steps use. The page is read once per visit, on navigation.

### Profile: tool size

Each `tools` entry gains an optional `size` in bytes. `schemaVersion` stays at 1. Validation: when present, `size` is greater than 0. The embedded profile gets the three sizes above. No request is made to learn a size: the screen works offline, and the downloader already checks the real length.

## Install

The rows, ticks, Cancel, Retry and "Open log" work as today, with the steps from `installer.CreateSteps(mode)`.

**Progress area** (the current download bar, generalized):

| State | Bar | Text |
| --- | --- | --- |
| Download in progress | determinate, or indeterminate without a length | "12.3 / 267.6 MB" (today's text) |
| Fraction reported | determinate | "123 / 364 units" |
| Build step running, no download or fraction yet | indeterminate | the step's name |
| No step running, or the step finished, was skipped or failed | hidden | — |

The indeterminate state only covers build steps (Get the source through Copy the build), so a release install looks as it does today.

**Interleaved counters.** `FractionTracker`, in `Core.Pipeline` next to `NinjaProgress`, filters fractions within one step. It keeps the counter with the largest `Total` seen and ignores a fraction with a smaller `Total` (a sub-build's own Ninja). A larger `Total` takes over, and `Reset()` starts each step afresh. The app and the CLI's 10 % printing (`ConsoleStepProgress`) both use it.

**Log view.** It holds step events and `StepProgress.Message` lines, which Core already limits to error-like lines (`error:`, `fatal:`, `CMake Error`). The full output stays in the log file.

**Failure details.** `BuildCommandException` gains `Summary` (the targeted message) and `Details` (the exit code line and the output tail). `Message` stays `Summary + newline + Details`, so the CLI output is unchanged. The page shows "{Step} failed: {Summary}" and a collapsed "Show details" expander with `Details` in a monospace, selectable text box. For any other exception, the failure line uses the inner exception's message as today and the expander is hidden.

**Closing the window.** The confirm dialog stays as it is. Its text, "Quitting cancels it; it will resume where it stopped", already holds for builds.

## Done

Unchanged, plus one line for a build record: "Built from source, commit {first 7 characters}."

## Tests

No test uses the network or the compiler.

- Mode:
  - preselection: unfinished install, then record, then Release;
  - Build disabled without a `build` section, Release preselected then;
  - selecting a card;
  - Continue opens the check with the selected mode.
- PC check:
  - `RunAsync` receives the mode;
  - Continue opens Tools in build mode and Install in release mode.
- Tools:
  - rows from the profile and `IsInstalled`;
  - the missing total, "size unknown" with "at least", "All tools are present";
  - the button text;
  - the target page.
- `BuildToolsSummary`: the three tools, in order, with sizes and installed state.
- Install:
  - the build step list for `Install(Build)`;
  - a fraction gives the determinate bar and its text;
  - a running build step with no counter is indeterminate;
  - a release step with no download hides the bar;
  - a build failure fills Summary and Details;
  - another failure hides the details.
- `FractionTracker`:
  - the largest total wins;
  - a smaller total is ignored;
  - a larger total takes over;
  - `Reset` clears.
- Profile: `size` is optional, and 0 or a negative size is rejected.
- `BuildCommandException`: `Summary`, `Details`, and `Message` unchanged.
- Home: a build install's action is enabled and opens Mode.
- Done: the build line appears only for a build record.
- Page factory: the new pages resolve from the real container.

## Criterion

Run by the user on the AMD laptop after the tests pass:

1. On an empty root: Mode → Build → check (the disk row uses 10 GiB) → Tools (all three missing, 305.2 MB) → Install finishes. The compile and libraries bars advance with no jump backwards. Done shows the commit line.
2. On a second empty root, close the window during "Compile AnyPS5" and confirm. Reopen: Home says the build is incomplete, Mode preselects Build, and Tools says "All tools are present". Install skips the finished steps, and the compile counter resumes where Ninja stopped.
3. Home on the finished build → "Verify / repair" → Build preselected → every step is skipped, and Done appears within seconds.
4. From Home, choose Release on Mode: "Remove the other mode's files" runs, and Home then shows the release install.

## Docs

- `docs/architecture.md`:
  - the screen order (Mode before the PC check);
  - the Tools screen as built;
  - the removal of "repairs a build only from the command line";
  - 5b in the roadmap, with a link to this spec.
- `README.md`: build mode from the app.

## Refinements from the implementation plan

1. `FractionTracker` lives in `Core.Pipeline` but is created per command inside `BuildCommands.RunAsync`, so it needs no `Reset()` and the console's 10 % printing gets the fix without change.
2. Mode asks the installer, through `IAnyPS5Installer.Supports(mode)`, whether build is available: build also needs the MinGit and WinLibs tools, not only the `build` section.
3. In build mode every running step shows the indeterminate bar until a download or a counter replaces it, tool steps included. Release mode shows the bar for downloads only, as before.
4. The Tools total reads "To download: size unknown" when every missing tool lacks a size.
5. Constant UI texts on view models are get-only properties with an initializer, because the analyzers (CA1822) reject expression-bodied constants on instance members the views bind to.

## Decision after the final review

The final review found that switching modes in the app empties `anyps5\current` before the new mode is installed, so a failed or cancelled build after a release leaves no working AnyPS5. The CLI does the same, but only on an explicit `--mode`. The user chose to keep this behaviour in 5b. A later milestone will keep the release and build installs side by side (`anyps5\release`, `anyps5\build`, plus the active one), so a switch never removes a working install. Until then, switching modes in the app replaces the installed mode from its first step.

## Changes from the criterion run

1. Home froze for several seconds on "Back to home" after a build: its refresh ran `GetState`, whose "Copy the build" done-check hashes every copied file on both sides (about 2 x 334 MB), on the UI thread. Home now checks the install on a worker thread and shows "Checking the AnyPS5 install…" meanwhile; a result that arrives after a newer refresh is dropped.
2. Mode shows "Installed: {version}" on the installed mode's card, and its button reads "Verify / repair" for the installed mode, "Resume the install" for an unfinished one, and "Continue" otherwise.
