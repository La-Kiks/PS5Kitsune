# Milestone 8b — The app on Linux and the AppImage: design

October 31, 2026 · Complements [docs/architecture.md](../../architecture.md) ("Linux compatibility", "Launching") and the [milestone 8a spec](2026-10-30-milestone-8a-linux-cli-design.md).

## Goal

The Avalonia app runs on Linux and ships as one AppImage that also holds the command-line tool. Screens and texts that assume Windows say the right thing on Linux, and the Linux launcher follow-ups left by 8a are done: an unknown exit code, Linux-style exit codes, a running game recognised without trusting the clock, and Stop ending the whole process group.

**Criterion:** a rehearsal in WSL (WSLg), then the CI AppImage used on a real Ubuntu install to install AnyPS5, add DOOM and play it. See [Criterion](#criterion).

**Out of scope:**

- **A GitHub Release.** CI builds the AppImage and the Windows single `.exe` as run artifacts only; publishing a first release, and choosing version numbers, is a later decision.
- **Desktop integration from the app.** No menu entry written by PS5Kitsune and no per-game shortcuts. The AppImage carries its `.desktop` file and icon, which AppImage managers (Gear Lever, AppImageLauncher) use.
- **Flatpak, `.deb`, `.rpm`.**
- **Build mode on Linux** (still in `TODO.md`).
- **From `TODO.md`, kept there:** "Required OS in profiles and system facts" and "Exit 0 = normal quit on Linux".

## Packaging

### `packaging/linux/build-appimage.sh <version>`

A POSIX shell script, run on Linux (CI, WSL). It writes everything under `artifacts/appimage/` (git-ignored):

1. **Publish** `src/PS5Kitsune.App` and `src/PS5Kitsune.Cli` with `-c Release -r linux-x64 --self-contained -p:Version=<version>` (not single-file) into `AppDir/usr/bin`. Central package management keeps shared dependencies at one version, so the two outputs merge; the script publishes each to its own folder first and fails if a file present in both differs, then copies both into `usr/bin`.
2. **`AppDir/AppRun`**, a `sh` script:
   - no argument: `exec "$HERE/usr/bin/PS5Kitsune"`;
   - any argument: `exec "$HERE/usr/bin/ps5kitsune" "$@"` (so `--help` and `--version` reach the CLI).
3. **Desktop entry and icon:** `AppDir/ps5kitsune.desktop` (`Name=PS5Kitsune`, `Exec=PS5Kitsune`, `Icon=ps5kitsune`, `Categories=Game;Utility;`, `StartupWMClass` = the app's window class, `Terminal=false`) and `AppDir/ps5kitsune.png`, a 256×256 PNG of `kitsune.ico` stored in `packaging/linux/`.
4. **`appimagetool`:** downloaded from a pinned release URL to `artifacts/appimage/tools/`, its SHA-256 checked against a hash in the script, then run on `AppDir` to write `PS5Kitsune-<version>-x86_64.AppImage`. The release used embeds the static type-2 runtime, so the AppImage runs on Ubuntu 24.04+ without `libfuse2`.

The AppImage is read-only; settings and data stay where 8a put them (`~/.config/ps5kitsune`, `~/.local/share/ps5kitsune`).

**Licenses:** `appimagetool` is a build tool and not shipped. The runtime it embeds in the AppImage is MIT (GPLv2-compatible). The license check of shipped NuGet packages is unchanged: the App adds no package that the check does not already cover.

### CI

- **Ubuntu job:** the "Publish the Linux CLI" step is replaced by `packaging/linux/build-appimage.sh 0.0.0-ci.<run number>+<short sha>`, then:
  - `./PS5Kitsune-*.AppImage --appimage-extract-and-run --version` must print that version (the runner has no FUSE; this goes through `AppRun` to the CLI);
  - the extracted `usr/bin` must hold both `PS5Kitsune` and `ps5kitsune`;
  - the AppImage is uploaded as an artifact.
- **Windows job:** `dotnet publish src/PS5Kitsune.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=<same version>`, uploaded as an artifact.

### README

A Linux section: download the AppImage artifact from a CI run, `chmod +x`, run it for the app or with arguments for the CLI (`./PS5Kitsune-….AppImage check`); `--appimage-extract-and-run` when FUSE is unavailable; adding it to the app menu with Gear Lever or AppImageLauncher.

## The app on Linux

The App project keeps no OS check: every difference below comes from `IPlatform`, `ILavapipe` or the profile.

- **Build card on Mode.** A new `IPlatform.BuildUnavailableReason` (`string?`): null on Windows, "Build mode is not available on Linux yet." on Linux. When it is set, the build card shows it instead of "This profile does not define a build." The CLI's refusal of `install --mode build` and `use build` uses the same text. `CanBuild` is already false on Linux (the Linux profile has no build section), so the Tools screen stays unreachable.
- **Lavapipe wording.** `ILavapipe` gains `string HowToGet`:
  - Windows: "Mesa (~70 MB) is downloaded on the first launch."
  - Linux: "Uses the system's Mesa (Lavapipe). If it is missing, install it with: `<command>`", the command from 8a's per-distribution packages.

  It replaces the fixed texts in the Settings page help, `GamePlayer`'s "Launch again to download it.", the `settings` command's `VK_DRIVER_FILES= (downloaded on first launch)`, and the diagnosis message after Turn on CPU rendering.
- **Dozen.** On Linux, the Dozen check shows "Not applicable on this system." (in `check` and on the Check page), like the commit limit, and the Settings page hides "Disable Dozen" through the existing `ShowDisableDozen`.
- **Diagnosis fixes.** Page file settings and NVIDIA drivers are only offered by Windows profile rules: no change. Open folder and View last log use `xdg-open`, already in `LinuxPlatform.OpenPath`.
- **Pickers, drag and drop, window.** Avalonia's own: the folder and zip pickers (desktop portal) and drag and drop (X11 and Wayland) are checked in the criterion run, with no code planned. The `.desktop` file's `StartupWMClass` matches the window class so the dock shows the PS5Kitsune icon.

## The launcher on Linux

### Unknown exit code

- `ILaunchedGame.WaitForExitAsync` returns `Task<int?>`; null means the code cannot be known. Only a game reattached on Linux returns null; Windows and a Linux child always return a code.
- `ExitLines.Unknown()` writes `[ps5kitsune] exited (exit code unknown)`. `ExitLines.Parse` reads it as an exit record whose code is null (`ExitRecord.Code` becomes `uint?`).
- The diagnosis skips exit-code rules when the code is null and still applies its log rules. A null code is not "exit 0 = normal quit": the panel shows a diagnosis only when a log rule matches.
- The Game page and `run` say "DOOM exited." without a code.

### Linux exit codes

- The Linux launcher keeps returning the exit status, or 128 + the signal that ended the game.
- `IPlatform.DescribeExitCode(int code)`:
  - Windows: `0xC0000005` (the existing hex form);
  - Linux: `1`, or `139 (signal 11, SIGSEGV)` for a code above 128 that matches a known signal.
- The exit line keeps its decimal code first, so `Parse` and older logs keep working: `exited with code {code} ({DescribeExitCode})` on Windows as today, `exited with code 139 (signal 11, SIGSEGV)` on Linux, and `exited with code 1` when the description is the number itself.
- Used by the CLI's `run` output, the Game page, `DiagnosisTexts` and `CrashReporter` instead of their own `0x{code:X8}`.

### A clock-proof identity

- `RunningGame` moves to schema 2 with two optional fields, `BootId` (`string?`) and `StartTicks` (`ulong?`). The Linux launcher fills them from `/proc/sys/kernel/random/boot_id` and field 22 of `/proc/<pid>/stat`; Windows leaves them null.
- The reader accepts schemas 1 and 2; the writer writes 2.
- Linux `Attach`: when the record has both fields, the process matches only if the current `boot_id` and its start ticks are equal (and the executable matches, as today); the 5-second start-time tolerance is not used. A record without them is checked as in 8a.

### Stop ends the whole process group

- For a game started by PS5Kitsune, the waiting thread uses `waitid(P_PID, pid, WEXITED | WNOWAIT)`: it sees the exit without reaping, so the zombie leader keeps the PID and the process group ID from being reused.
- If a Stop was asked, the thread then sends `SIGKILL` to the group (a no-op when it is empty), and only then reaps the leader with `waitpid`. A game that exits on its own leaves its group alone.
- Stop itself is unchanged: `SIGTERM` to the group, then `SIGKILL` after the grace period while the leader lives.
- A reattached game is not our child: Stop keeps signalling its group only while the leader lives.

## Errors

- The AppImage started where FUSE is missing: the runtime's own message; the README gives `--appimage-extract-and-run`.
- `boot_id` or `/proc/<pid>/stat` unreadable when recording a launch: the fields stay null and `Attach` falls back to the 8a check; the launch goes on.
- `waitid` failing: the thread falls back to `waitpid` as in 8a (no group kill after exit) and logs why.

## Tests

- **Core:** `ExitLines` (unknown line; parsing old lines, new lines and the unknown line); diagnosis with a null code (exit-code rules skipped, log rules applied); `RunningGame` schema 1 and 2 round-trips.
- **Platforms:** `DescribeExitCode` on Windows and Linux (0, 1, 139, 137, a code above 128 that is no signal); `BuildUnavailableReason`; `HowToGet` on both; the Linux Dozen check.
- **App:** the Mode build card's text from the platform; the Settings Lavapipe help from `ILavapipe`; the Game page's "exited." line for a null code.
- **Linux launcher, with real processes** (WSL and ubuntu-latest): Stop kills a child that ignores `SIGTERM` after the leader exits on it; a natural exit leaves such a child running (then the test kills it); `Attach` with `boot_id` and ticks matching, with another `boot_id`, with other ticks; a reattached game's exit reported as null.
- **Packaging:** the CI checks in [CI](#ci). Opening the window is left to the criterion.

## Criterion

1. **WSL rehearsal (Claude).** Build the AppImage with the script in WSL Ubuntu 24.04 and start the app through WSLg (`--appimage-extract-and-run` if FUSE is missing): Home, Mode (the build card shows the Linux text), Check, Install (release v0.1.4), add DOOM (green 357/0/0). Through the AppImage, `check` and `list` run the CLI. Games do not run in WSL.
2. **Real Ubuntu 26.04 (the user, AMD laptop).** Download the CI AppImage, `chmod +x`, open it: install release v0.1.4; add DOOM once from the picker and once by drag and drop (removing it in between); play it from the Library; close the app while DOOM runs, reopen it, see the game found again, and Stop it; both themes and the keyboard alone; one CLI command through the AppImage.

## Docs

- `docs/architecture.md`: 8b's spec link and criterion in the roadmap; a packaging row for Linux (AppImage holding the app and the CLI).
- README: the Linux app section (see [README](#readme)) and the status line.
- `TODO.md`: drop "Exit code of a reattached game on Linux", "A clock-proof identity…", "Exit codes on Linux", "Stop the whole process group on Linux", "Lavapipe text on Linux" and "The Dozen check on Linux".
