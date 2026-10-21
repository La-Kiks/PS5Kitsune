# Milestone 5a — Build mode, Core and CLI: design

October 21, 2026 · Complements [docs/architecture.md](../../architecture.md) (milestone 5 of the roadmap, "V1c — Build mode").

## Goal

Build AnyPS5 from source on a clean Windows PC with `ps5kitsune install --mode build`: portable MinGit and WinLibs, the profile's commit, configure, compile, `libs`, then the same `anyps5\current` a release install produces. An interrupted build resumes without starting over, and a new commit in the profile updates incrementally.

Milestone 5 is split in two:

- **5a (this spec):** Core and CLI. It carries the three roadmap criteria.
- **5b:** the UI. The Build card on Mode becomes active, the Tools screen appears, and the Install page shows the build progress. It gets its own spec.

**Out of scope:** the UI (5b), profiles downloaded from the PS5Kitsune repository and the "new validated version" notice (open decision in the architecture), ccache, a "clean build" option, choosing the number of jobs on the command line, the WinLibs 7z archive, and deleting `source\` or `build\` when going back to release mode.

## Facts checked on 2026-10-21

- WinLibs `15.2.0posix-14.0.0-ucrt-r7` ships CMake 4.3.1, Ninja 1.13.2 and GNU Make (release notes). No fallback to the official CMake and Ninja zips is needed. AnyPS5's release workflow puts `C:\winlibs\mingw64\bin` on `PATH`, ahead of the runner's own tools.
- Asset `winlibs-x86_64-posix-seh-gcc-15.2.0-mingw-w64ucrt-14.0.0-r7.zip`: 267,593,703 bytes, `sha256:cb2fbad6162540cdf5e1facdce08d4dac359e8cf64f7f696a99274291763b815`.
- MinGit `v2.56.0.windows.2`, asset `MinGit-2.56.0.2-64-bit.zip`: 39,806,486 bytes, `sha256:da35e72aa21c005a5a0d298cfbae110bc1609a815730ea0dde84b01a1b3cd3be`.
- At commit `48d61f53b46f1d0726889e4dd39c5ef2b3eb30cd`, `tools/unwrap_self.py` is in the repository. In build mode every script comes from the clone.
- The repository has 16 submodules (SDL2, Vulkan-Headers, SPIRV-Headers, SPIRV-Tools, glslang, VulkanMemoryAllocator, LibAtrac9, ffmpeg-core, freetype, stb, tracy, libjpeg-turbo, libusb-cmake, mbedtls, zstd, ZArchive).
- The release workflow configures with `-G Ninja -DCMAKE_BUILD_TYPE=Release -DBUILD_TESTING=ON -DCMAKE_C_COMPILER=gcc -DCMAKE_CXX_COMPILER=g++` and ccache, then builds `all` and `libs` separately. It sets `SSL_CERT_FILE` to Git for Windows' `ucrt64` bundle, else the `mingw64` one.

## Profile

`schemaVersion` stays at 1: no profile has been published outside the app yet.

- `alternateSource` is replaced by a `build` section:

  ```json
  "build": {
    "repository": "https://github.com/boykopovar/AnyPS5.git",
    "commit": "3fd1cc393b8512c2e989860053f30dee5a8cd1aa"
  }
  ```

  Validation: `repository` uses HTTPS, and `commit` is 40 lowercase hex characters.
- `tools` gains `mingit` and `winlibs` (version, URL, SHA-256), with the values above.
- `checks` gains `buildMinFreeDiskGiB`. It started at 15 and was set to 10 from the criterion run (see "Criterion result"). The PC check uses the threshold of the requested mode.

## Active profile and override

`ProfileLoader.LoadActive(DataLayout)` returns `ActiveProfile(Profile, Origin, Problem)`, where `Origin` is `Embedded` or `Override`.

- `<root>\profiles\override.json` present and valid: it replaces the embedded profile.
- Absent: the embedded profile, with no problem.
- Present but unreadable or invalid: the embedded profile, with a problem text. The CLI logs it as a warning, the same way it handles `settings.json`.

This simplifies the architecture's "the most recent valid one wins": with no downloaded profile there is nothing to compare, so a valid override simply wins. The comparison comes back with downloaded profiles.

## Portable tools

`MinGitTool` and `WinLibsTool` derive from `ArchiveTool`, like `PythonEmbeddableTool`, and are registered as `IPortableTool`. `ps5kitsune tools list` and `tools install mingit|winlibs` work without changing the command.

| Tool | Folder | Required files (done-check, with the archive hash) | `GetVersionAsync` |
| --- | --- | --- | --- |
| `mingit` | `tools\mingit` | `cmd\git.exe` | `git --version` → `2.56.0.windows.2` |
| `winlibs` | `tools\winlibs` | `mingw64\bin\g++.exe`, `cmake.exe`, `ninja.exe` | `gcc -dumpfullversion` → `15.2.0` |

- `MinGitTool.CaBundle` returns `mingw64\etc\ssl\certs\ca-bundle.crt`, else `ucrt64\etc\ssl\certs\ca-bundle.crt`. If neither exists, it fails with a message naming both paths. The real location is confirmed at the first extraction.
- `BuildEnvironment` builds the child-process environment for the build: `tools\winlibs\mingw64\bin` then `tools\mingit\cmd` **prepended** to the child's `PATH`, plus `SSL_CERT_FILE` set to the CA bundle. The app's own environment is never changed.

## Layout

```text
<root>\anyps5\
├── source\        git clone (fetched by SHA, depth 1)
├── build\         CMake build folder, outside the clone
├── state\         one marker per build step, holding the commit
└── current\       relinker.exe, libs\, scripts\, install.json — the same for both modes
```

A marker is written **after** its step succeeds, never before. A missing marker, or one holding another commit, means the step is not done.

## Build steps

`IAnyPS5Source` gives each mode its steps: `ReleaseSource` (today's download steps) and `BuildSource`. `AnyPS5Installer` builds the list as Python + `source.CreateSteps()` + `WriteInstallRecordStep`.

| # | Step | Execution | Done when |
| --- | --- | --- | --- |
| 1 | Install MinGit | `MinGitTool.InstallAsync` | tool installed |
| 2 | Install WinLibs | `WinLibsTool.InstallAsync` | tool installed |
| 3 | Get the AnyPS5 source | In `source\`: `git init` and `git config core.longpaths true` if `.git` is missing; `git remote add origin <repository>`, or `set-url` if the remote exists; `git fetch --depth 1 origin <commit>`; `git checkout --detach --force <commit>` | `state\checkout.done` = commit |
| 4 | Update the submodules | `git submodule sync --recursive`, then `git submodule update --init --recursive --depth 1 --force` | `state\submodules.done` = commit |
| 5 | Configure the build | `cmake -S source -B build -G Ninja -DCMAKE_BUILD_TYPE=Release -DBUILD_TESTING=OFF -DCMAKE_C_COMPILER=<winlibs>\gcc.exe -DCMAKE_CXX_COMPILER=<winlibs>\g++.exe` | `state\configure.done` = commit |
| 6 | Compile AnyPS5 | `cmake --build build --parallel <N>` | `state\compile.done` = commit |
| 7 | Build the libraries | `cmake --build build --target libs --parallel <N>` | `state\libs.done` = commit |
| 8 | Copy the build | See below | every target file exists with the size and SHA-256 of its source |

Every git and CMake command runs with the `BuildEnvironment`.

**Step 8, copy into `current`:**

- `build\core\relinker\relinker.exe` → `current\relinker.exe`.
- `build\core\libs\libs\*.prx` plus `libgcc_s_seh-1.dll`, `libstdc++-6.dll` and `libwinpthread-1.dll` from `tools\winlibs\mingw64\bin` → `current\libs\`. The folder is mirrored, so any other file is removed.
- `source\tools\` → `current\scripts\tools\` and `source\core\libs\prx\` → `current\scripts\core\libs\prx\`, mirrored the same way.

**Parallel jobs:** `N = max(1, min(logical processors, floor(physical RAM in GiB / 2)))`. GCC can use about 2 GiB per job on large C++ files, and a laptop with 16 GiB would swap at `--parallel` with no limit.

**Resume:**

- An interruption during step 6 leaves no `compile.done`. The next run skips steps 1–5, and Ninja recompiles only what is missing.
- A new commit makes every marker stale. Steps 3–8 run again, and CMake and Ninja reuse `build\` incrementally.

## Switching modes

Before running the steps, the installer reads `install.json`. If it records the other mode, it deletes `current\`, so the release's `libTracyClient.dll` cannot linger next to a build, and the other way round. `source\`, `build\` and `state\` are kept, so going back to build mode is incremental.

## `install.json`, schema 2

```json
{
  "schemaVersion": 2,
  "profile": "doom-validated-2026-10",
  "mode": "build",
  "version": "3fd1cc3",
  "commit": "3fd1cc393b8512c2e989860053f30dee5a8cd1aa",
  "unwrapScript": null,
  "installedAt": "2026-10-22T09:00:00+02:00"
}
```

- `version`: the tag in release mode, the first 7 characters of the commit in build mode.
- `commit`: null in release mode.
- `unwrapScript`: the profile's URL in release mode, null in build mode, where the script comes from the clone.
- The reader accepts schemas 1 and 2. The installer writes 2.

## Installer API

- `AnyPS5Mode { Release, Build }`.
- `IAnyPS5Installer.CreateSteps(AnyPS5Mode)`, `GetState(AnyPS5Mode)`, `InstallAsync(AnyPS5Mode, progress, onEvent, cancellationToken)` and `ReadRecord()`.
- `GetState(mode)` reports `Incomplete` when `install.json` records another mode, the same way it already does for another profile or tag.
- The app (milestone 4) passes the record's mode, or `Release` when there is no record. Its screens do not change in 5a.

## CLI

`ps5kitsune install [--mode release|build]`.

Mode resolution: the `--mode` option, else the mode of an unfinished install, else the mode in `install.json`, else `release`. After an override changes `build.commit`, a plain `ps5kitsune install` therefore updates the build.

The command starts with three lines, before the PC check:

```text
Data root: C:\PS5Kitsune (saved setting)
Profile: doom-validated-2026-10 (embedded)
Mode: build (current install)
```

The data root source is one of `--root`, `saved setting` or `default`. The mode source is one of `--mode`, `unfinished install`, `current install` or `default`. This also settles the milestone 4 follow-up "the CLI should print the data root it used".

## Tool output

- Every line from git and CMake goes to the log file at Debug, stdout and stderr alike (see refinement 15).
- `StepProgress` gains an optional `Fraction(int Done, int Total)`. `NinjaProgress.TryParse` reads it from lines such as `[123/2400] Building CXX object …`.
- The console shows the fraction as a single progress line that overwrites itself, like downloads. Other lines reach the console only when they come from stderr and contain `error:`, `fatal:` or `CMake Error`. The log keeps everything.

## Errors

A non-zero exit code becomes a `StepFailedException` carrying the last 20 lines of output (`OutputTail`) and the log path. Each step adds a targeted first line:

| Step | Message |
| --- | --- |
| Get the source, submodules | "Cannot download from GitHub: check the connection, then run install again." |
| Configure, with `SSL`, `certificate` or `error 60` in the output | "CMake could not download a dependency over HTTPS. CA bundle used: <path>." |
| Configure, otherwise | "CMake could not configure the build." |
| Compile, libs | "The build failed. The log holds the full compiler output." |
| Any, when the drive has less than 1 GiB free | "Not enough disk space on <drive>: <free> left." |

**Cancellation** (Ctrl+C): the running process and its children are killed, the step emits `Cancelled` and no marker is written. The plan checks that CliWrap kills the whole tree (Ninja starts `g++` and `cc1plus`). If it does not, `IProcessRunner` on Windows adds a Job Object.

## Tests

No test uses the real network or the compiler unless it is marked as an integration test.

- Profile: the `build` section's validation, the new tools, `buildMinFreeDiskGiB`. `LoadActive` with no override, a valid one, an invalid one and an unreadable one.
- Tools: MinGit and WinLibs done-checks on fake extracted folders. The CA bundle order (`mingw64`, then `ucrt64`, then an error). `BuildEnvironment` prepends both folders to `PATH` and sets `SSL_CERT_FILE`.
- Build steps, with a fake `IProcessRunner` that records commands:
  - the exact arguments of each step;
  - a marker written only after exit code 0;
  - a marker holding another commit means not done;
  - the targeted error messages;
  - cancellation writes no marker.
- `NinjaProgress.TryParse`: a valid line, a malformed line, `[0/0]`, a line without brackets.
- Copy step: mirroring removes a stray file, and the done-check fails when a source file changes.
- Installer: the step list per mode, `current\` emptied when the mode changes, `GetState(mode)`, `install.json` schema 1 still read, schema 2 written.
- Parallel jobs: the formula for 1 core, for little RAM and for many cores.
- CLI: mode resolution (option, record, default) and the three opening lines.
- One Windows integration test, `[Trait("Category", "Network")]` and excluded from the default run and from CI. It runs steps 3 and 4 against the real repository and checks `git submodule status`.

## Criterion

Run by the user on their PC after the tests pass, on an empty root:

1. `ps5kitsune install --mode build` finishes with no intervention. Note the total time and the size of `source\` plus `build\`. `buildMinFreeDiskGiB` is set from that size.
2. On a second empty root, press Ctrl+C during "Compile AnyPS5", then run `ps5kitsune install` again. Steps 1–5 say "already done", and Ninja's counter resumes where it stopped, not from 0.
3. Copy the profile to `<root>\profiles\override.json` with a newer commit, then run `ps5kitsune install` with no `--mode`. The CLI prints `Profile: … (override)` and `Mode: build (current install)`, and the build takes much less time than in point 1.
4. Bonus: `ps5kitsune add` of DOOM with the built `current` gives a green audit.

## Docs

- `docs/architecture.md`:
  - build mode (layout, markers, fetch by SHA, `BUILD_TESTING=OFF`, a corrected note on what the CI does);
  - the override's resolution order;
  - `install.json` schema 2;
  - the 5a / 5b split in the roadmap.
- `README.md`: `--mode` and `override.json`.

## Refinements from the implementation plan

1. MinGit 2.56 keeps its CA bundle under `ucrt64`: it is tried first, `mingw64` second.
2. The process runner kills the whole tree itself on cancellation (no Job Object needed). A kill that fails is logged, never thrown.
3. Error-like lines reach the console from both outputs: Ninja prints compiler errors on the standard output. The tail quoted in errors holds both outputs, Ninja counters excluded.
4. The console prints the build fraction every 10 %, like downloads, instead of overwriting one line.
5. `GetSourceStep` sets `remote.origin.url` with `git config`, so a rerun after any interruption needs no special case; configure passes `-DCMAKE_MAKE_PROGRAM` and forward-slash paths.
6. The copy step's done-check ignores extra files in `current` (Python's `__pycache__`); execution still mirrors `libs` and `scripts`.
7. The app's Install page installs in release mode (the only card on Mode until 5b); Home and Done read the current mode from `install.json`. The app also loads `override.json` per root, without showing an ignored one until 5b.
8. The network integration test is `[Fact(Explicit = true)]`, so neither the default run nor CI runs it.
9. `BuildMarkers.Clear` ignores a missing marker file, so a first run, where `anyps5\state` does not exist yet, works.
10. The `install` command's log arguments are computed into locals first (analyzer CA1873).
11. An interrupted install resumes in its mode: `InstallAsync` writes `anyps5\state\requested-mode` before the first step and deletes it after the last one succeeds. `RequestedMode` comes before the record in `CurrentMode`, and `InstallModes.Resolve` uses it with the source `unfinished install`.
12. `GetState` returns Incomplete for a mode the active profile does not support, so Home never throws on a build record; `CreateSteps` and `InstallAsync` still throw, and `install` reports the unavailable mode before the PC check.
13. Until 5b, Home shows a build install ("built from source", or "The AnyPS5 build is incomplete") with its action disabled, and points to `ps5kitsune install`, so the app never replaces a build with a release.
14. Clearing `current` on a mode switch is the first step, "Remove the other mode's files", listed only when `install.json` records the other mode; it deletes `install.json` last, so a locked file fails as a step and a retry clears again. The copy step also requires the relinker, a `.prx`, `import_audit.py` and `unwrap_self.py` before copying.
15. Tool stderr is logged at Debug like stdout: at Information it reached the console for every tool. Error-like lines still reach the console once, through the step's progress messages.
16. The criterion run showed that libusb-cmake's `gen-describe.ps1` needs a non-WSL `bash.exe` and picked the WSL alias in `%LOCALAPPDATA%\Microsoft\WindowsApps`, because MinGit ships no `bash.exe`. MinGit's install now copies `usr\bin\sh.exe` (GNU bash) to `bash.exe` (a missing copy means reinstall), and the child PATH drops the WindowsApps entry and ends with `tools\mingit\usr\bin`.
17. The CLI gives a cancelled command 30 seconds to stop (`InvocationConfiguration.ProcessTerminationTimeout`). The criterion run showed that System.CommandLine's default of 2 seconds ended the process before CMake and Ninja had stopped, so the cancellation was never logged and the log file was cut short.

## Criterion result

Run by the user on 2026-10-21 on the AMD laptop (8 threads, so 7 parallel jobs), against commit `48d61f5`, then `3fd1cc3` for the bonus:

1. **Clean build: passed after one fix.** The first run failed at "Build the libraries" on libusb-cmake's `bash.exe` lookup (refinement 16). After the fix, a plain `install` resumed at that step (`Mode: build (unfinished install)`) and finished. A second clean root then built with no intervention.
   - Durations: tools 24 s, source and submodules 75 s, configure 99 s (it downloads FFmpeg), compile 122 s, libraries 395 s, copy 2 s. That is about 12 minutes in all.
   - Sizes: `tools` 1.1 GB, `build` 0.9 GB, `source` 0.5 GB, `current` 0.3 GB, `downloads` 0.3 GB, so a root of 3.2 GB. `buildMinFreeDiskGiB` is set to 10 from this.
2. **Ctrl+C and resume: passed.**
   - A Ctrl+C during "Install MinGit" resumed without downloading the zip again.
   - A Ctrl+C during "Compile AnyPS5" left no `ninja` or `cc1plus` running. The next `install` said `build (unfinished install)`, skipped steps 1 to 6, and Ninja had 226 of 364 units left.
   - That run showed the missing cancellation log (refinement 17). After the fix, a Ctrl+C during "Build the libraries" printed `cancelled after 218.5 s` and `Cancelled.`, and the resume had 505 of 860 units left.
3. **Incremental update: passed.**
   - An override pointing at `663de1a` printed `Profile: doom-validated-2026-10 (override)` and `Mode: build (current install)`.
   - Submodules took 18 s instead of 70 s and configure 11 s instead of 97 s. Compile took 78 s (364 units), and the libraries took 435 s.
   - The total was about 9 minutes against 12. The libraries rebuild almost fully because the new commit changes code they share, so the update is shorter but not much shorter.
   - Removing the override and running `install` again went back to `48d61f5` incrementally.
4. **Bonus, DOOM with the built `current`: passed once the build commit was changed.**
   - With `48d61f5`, `add` gave orange (360 implemented, 4 stubs, 0 absent), but DOOM aborted at startup: libkernel threw "Thread destructor callback is already registered".
   - Release v0.1.3 from AnyPS5's CI (commit `3fd1cc3`, 2026-10-20) ran DOOM with the same audit. Our build of `3fd1cc3` also ran it to a clean exit (code 0). So the crash belongs to commit `48d61f5`, not to build mode.
   - Removing the 233 `.prx` that `tools/package_release.py` does not ship at `48d61f5` did not change the crash, so the copy step is not the cause.
   - The profile's `build.commit` is now `3fd1cc3`, the commit validated with DOOM. The extra stub against v0.1.2 is `sceSystemServiceLoadExec`, a stub in AnyPS5's source at both commits.

Lesson: a build commit is validated by running DOOM on it, not by the audit alone. `48d61f5` came from the architecture study and was never run.

Seen during the run, for 5b: a sub-build that runs its own Ninja interleaves its `[n/m]` counter with the main one in the console progress.
