# Milestone 5c — Side-by-side installs: design

October 22, 2026 · Complements [docs/architecture.md](../../architecture.md) (milestone 5 of the roadmap), the [milestone 5a spec](2026-10-21-milestone-5a-build-mode-design.md) and the [milestone 5b spec](2026-10-22-milestone-5b-build-ui-design.md).

## Goal

The release and build installs each live in their own folder, and a small file names the active one. A switch never removes a working install: a build that fails or is cancelled leaves the release in use, and switching between two finished installs takes about a second and runs no step.

**Out of scope:** removing an install (both stay until the user deletes the folders), checks before a switch, and the 5a ledger's follow-up on markers keyed on the commit only.

## Facts checked on 2026-10-22

- `anyps5\build\` is already the CMake build folder (`BuildPaths.Build`), so the installs cannot live in `anyps5\release` and `anyps5\build` as the roadmap entry said.
- `AnyPS5Paths.RootDirectory` is `anyps5\current`. Its users: the release steps, `CopyBuildStep`, `WriteInstallRecordStep`, `ClearOtherModeStep`, the game steps (relink, copy libs, unwrap, audit), `GameImporter`, Done and the CLI's `install`.
- `AnyPS5Installer.IsInstalled()` checks `CurrentMode`, which is the requested mode first: during an unfinished build, `run` and `add` would refuse even with a working release.
- `ConversionInfo` records the profile's source (`profile.Source.Kind`, `Version`, `Sha`), not the install that converted the game.

## Layout

```text
anyps5\
├── source\  build\              unchanged (build mode)
├── state\
│   ├── active                   "release" or "build" (new)
│   ├── requested-mode           unchanged
│   └── *.done                   build markers, unchanged
└── installs\
    ├── release\                 relinker.exe, libs\, scripts\, install.json
    └── build\                   the same files, copied from build\
```

`DataLayout` gains `AnyPS5Installs` (`anyps5\installs`). `AnyPS5Current` stays for the migration only.

## Paths

- `AnyPS5Paths.For(AnyPS5Mode mode)` returns the paths of `installs\<mode>`: `RootDirectory`, `Relinker`, `Libs`, `Scripts`, `ImportAudit`, `UnwrapSelf`, `RecordFile`. The install steps of a mode write there.
- The existing properties of `AnyPS5Paths` (`RootDirectory`, `Relinker`, …) point at the active install, read from `state\active` on each access. With no active install they point at `installs\release`, where nothing is installed, and the callers already refuse through `IsInstalled()`. Game conversion and Done keep their code.
- `RequestedModeFile` stays; `ActiveModeFile` is `anyps5\state\active`.

## Installer

- `ActiveMode` (`AnyPS5Mode?`): the mode in `state\active`; null when the file is missing, unreadable or holds an unknown value. Never throws, like `RequestedMode`.
- `CurrentMode` = `RequestedMode`, else `ActiveMode`, else Release.
- `ReadRecord(AnyPS5Mode mode)`: `installs\<mode>\install.json`, null when missing or unreadable. `ReadRecord()` returns the active install's record, null with no active install.
- `CreateSteps(mode)`: Install Python, the source's steps writing into `installs\<mode>`, then "Record the install". `ClearOtherModeStep` is deleted.
- `GetState(mode)` describes only `installs\<mode>`, whatever is active. Its `Record` is `ReadRecord(mode)`.
- `IsInstalled()` is true when there is an active install and `GetState(ActiveMode)` is Installed. An unfinished install of the other mode no longer blocks `run` or `add`.
- `InstallAsync(mode)` writes `requested-mode`, runs the steps, then writes `active` = mode, then deletes `requested-mode`. A failure or a cancel never touches `active`.
- `Use(AnyPS5Mode mode)` (new) writes `active` = mode with no step and no done-check. It throws `InvalidOperationException` ("The {mode} install is not finished; run install --mode {mode} first.") when `ReadRecord(mode)` is null. It leaves `requested-mode` alone.
- `InstallModes.Resolve` takes the active install's record, so a plain `install` resumes an unfinished install, else repairs or updates the active one, else installs a release. Its source text "current install" stays.

## Migration of an existing root

`AnyPS5Migration.Run()` (Core, in `Core.AnyPS5`), idempotent:

- If `anyps5\current` exists and `installs\<mode>` does not, it moves `current` there (`Directory.Move`, same disk) and writes `active` = mode, unless `active` already exists.
- The mode is the one in `current\install.json`, else `requested-mode`, else Release.
- Nothing happens when `current` is absent or the target exists.
- An `IOException` or `UnauthorizedAccessException` (a file in `current` held by a running game) leaves everything in place and is rethrown as `AnyPS5MigrationException` with the message "Cannot move anyps5\current: {reason}. Close any game using it and retry."

Callers:

- The CLI runs it in `CommandHost` before any command that reads the install (`install`, `use`, `add`, `run`). On failure it prints the message and exits with code 1.
- The app runs it in Home's refresh, on the worker thread, before `GetState`. On failure Home shows "Cannot read the AnyPS5 install" with the message, as for a `GetState` failure, and retries on the next refresh.

## Game record

`WriteGameRecordStep` fills `ConversionInfo` from the active install's record: profile, mode (`"release"` or `"build"`), version and commit. The `mode` field now names the install mode; it never held anything but `"release"` before, since the embedded profile is a release. `GameRecord.CurrentSchemaVersion` stays at 1.

## App

**Home**

- The title describes the active install: "AnyPS5 v0.1.2 is installed", "AnyPS5 3fd1cc3 is installed (built from source)", or the existing incomplete and update texts for it.
- When the other mode's install is finished too: "Also installed: build 3fd1cc3" (or "release v0.1.2").
- When an install of the other mode did not finish (`RequestedMode` differs from `ActiveMode`): the title still shows the active install, the detail reads "The build install did not finish." (or "release"), and the action is "Resume the install".
- Otherwise the action is "Resume the install" for an unfinished install, "Verify / repair" (or "Update") for the active install, and "Install AnyPS5" with nothing installed. Every action opens Mode, as today.

**Mode**

- Each card reads "Installed: {version}" when `ReadRecord(mode)` is not null, and "In use" on the active install's card.
- The button for the selected card:
  - "Resume the install" when it is `RequestedMode`;
  - else "Verify / repair" when it is `ActiveMode`;
  - else "Use this install" when its install is finished (`ReadRecord(mode)` not null): it calls `Use(mode)` and opens Home, with no PC check, Tools screen or step;
  - else "Continue", the 5b flow.
- When the profile changed since that install, "Use this install" still switches; Home then offers the update.

**Install, Done:** unchanged. Done shows the active install, the one just installed.

## CLI

- `ps5kitsune use release|build` (new): runs `Use(mode)` and logs "AnyPS5 {mode} {version} is now in use ({directory})". On `InvalidOperationException` it prints the message and exits with code 1.
- `install`: unchanged options; its last line names the install now in use and its directory under `installs\`.
- `add`, `run`: use the active install, with no change.

## Errors

- `state\active` unreadable or unknown: no active install. Home offers "Install AnyPS5"; an install writes the file again.
- The active install's folder deleted by hand: `GetState` reports NotInstalled or Incomplete as usual, and "Verify / repair" reinstalls it.
- Migration failure: see "Migration".
- `Use` on an unfinished install: `InvalidOperationException`. Mode never offers it; the CLI prints the message.

## Tests

No test uses the network or the compiler.

- Installer:
  - each mode's steps write into `installs\<mode>`, and there is no clear step;
  - `active` is written after success only, and stays unchanged after a failed or cancelled install (the release stays active);
  - `CurrentMode`: requested, then active, then Release;
  - `GetState(mode)` does not depend on the active install;
  - `IsInstalled()` uses the active install, even with an unfinished install of the other mode;
  - `Use` switches, and refuses when the mode has no record;
  - `ActiveMode` with a missing, unreadable or unknown file is null.
- Migration:
  - `current` with a release record moves to `installs\release` and becomes active; the same with a build record;
  - with no record, the requested mode, else Release;
  - nothing happens when `current` is absent or the target exists;
  - an existing `active` is kept;
  - a move failure throws `AnyPS5MigrationException` and leaves `current` in place.
- Paths: `For(mode)` and the active view.
- Game record: `ConversionInfo` from the active install's record.
- `InstallModes.Resolve` with the active install's record.
- Home: the "Also installed" line, the interrupted-switch detail and action, and the migration error.
- Mode: "In use", the four button texts, and "Use this install" calling `Use` and opening Home.
- CLI: `use` succeeds and logs; `use` on an unfinished install exits with code 1; a migration failure exits with code 1.

## Criterion

Run by the user on the AMD laptop after the tests pass:

1. Migration: open the app on the 5b test root that holds a build. `current\` moves to `installs\build`, Home shows the build, and "Verify / repair" skips every step.
2. On a root with a release, choose Build and cancel during "Compile AnyPS5". Home still shows the release in use and says the build did not finish; `ps5kitsune add` and `run` work with the release. Resume the build to the end: the build becomes active.
3. With both installs finished, "Use this install" on Mode switches in both directions, then `ps5kitsune use release`. Each switch takes about a second and runs no step.
4. Add DOOM with the build active: its `game.json` records mode `build` and the commit.

## Docs

- `docs/architecture.md`:
  - the root layout (`installs\`, `state\active`);
  - the active install, `Use` and the migration;
  - the removal of "Remove the other mode's files";
  - the `convertedWith.mode` values;
  - 5c in the roadmap, with the corrected folder names and a link to this spec.
- `README.md`: the `use` command.

## Changes after the final review

1. `Use(mode)` also refuses a mode whose install did not finish (`RequestedMode == mode`), with "The {mode} install did not finish; run install --mode {mode} to resume it.", so `ps5kitsune use` cannot make an install active while its update is pending. Mode already offered "Resume the install" in that case.
2. Mode's "Use this install" shows "Cannot switch to this install: {reason}" and stays on the page when the switch fails, instead of letting the exception escape.
