# TODO

Items to investigate at a later date.

## Keep backup files out of `app0` when adding a game

Some dumps ship backups next to the real files, such as `eboot.bin.esbak` or `sce_module/libc.prx.esbak` (seen with Demon's Souls, PPSA01342). The relinker matches module names loosely, so a backup and its original both answer to the same dependency name, and conversion fails with `Ambiguous guest dependency name: libc.prx`. The validation step should find files named like a module or `eboot.bin` plus an extra extension (`*.prx.*`, `eboot.bin.*`, …) across the whole game folder, leave them out of the `app0` copy, keep them under `.ps5kitsune\`, and list them on the summary screen. Moving every `.esbak` out of `app0` by hand was enough to convert the game.

## Build mode on Linux

Milestone 8a supports release mode only on Linux. Build mode needs the system GCC and the build dependency packages listed in AnyPS5's BUILD.md, an install command per distribution, and a child-process job: Linux has no job object, so git and CMake started by a PS5Kitsune that is killed outright keep running (`IChildProcessJob` is a no-op there).

## Exit code of a reattached game on Linux

A game found again from `running.json` is not PS5Kitsune's child on Linux, so its exit code cannot be read: `WaitForExitAsync` returns -1. The app (milestone 8b) should show "exited" without a code, and skip the diagnosis by exit code, for such a game.

## A clock-proof identity for a running game on Linux

On Linux the launcher recognises a running game by its executable and its start time, computed from the boot time in `/proc/stat`. That boot time moves when the wall clock is stepped, so the start times are compared with a 5-second tolerance. A big clock jump (WSL resyncing after the host slept) still loses the game. A robust fix records an identity that ignores the clock in `running.json`: `/proc/sys/kernel/random/boot_id` plus the process's start ticks (`/proc/<pid>/stat` field 22). Worth doing with the app (8b), which follows games for longer.

## Lavapipe text on Linux

On Linux `settings` prints `VK_DRIVER_FILES= (downloaded on first launch)` and `GamePlayer` says "Launch again to download it.", but there Lavapipe comes from the system Mesa and is never downloaded. Give `ILavapipe` a "how to get it" text (the install command on Linux, the download on Windows) and use it in both places.

## Exit codes on Linux

Exit codes are shown Windows-style on Linux: 139 (SIGSEGV) shows as `0x0000008B` and -1 as `0xFFFFFFFF`. On Linux, show the decimal code, or "signal N" for a process killed by a signal.

## The Dozen check on Linux

The Dozen check prints "Not installed" on Linux, where Dozen does not exist. Show it as not applicable there, like the commit limit.

## Stop the whole process group on Linux

Stop sends SIGTERM to the game's process group and SIGKILL after the grace period, but only while the leader lives. Children that ignore SIGTERM survive a leader that exits on it. Stop could SIGKILL the whole group after the leader exits.

## Required OS in profiles and system facts

`ProfileLoader.LoadEmbedded` and `LoadActive` default `os` to Windows, and `SystemFacts` defaults to the Windows texts. A caller that forgets them gets Windows behaviour on Linux silently; make them required.

## "Exit 0 = normal quit" on Linux

In WSL AnyPS5 exited with code 0 when DOOM failed (`I_AllocLow: cannot allocate`). `CrashDiagnoser` treats exit 0 as a normal quit; check on real Linux whether that still holds, or diagnose from the log even after exit 0.
