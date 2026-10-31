# TODO

Items to investigate at a later date.

## Keep backup files out of `app0` when adding a game

Some dumps ship backups next to the real files, such as `eboot.bin.esbak` or `sce_module/libc.prx.esbak` (seen with Demon's Souls, PPSA01342). The relinker matches module names loosely, so a backup and its original both answer to the same dependency name, and conversion fails with `Ambiguous guest dependency name: libc.prx`. The validation step should find files named like a module or `eboot.bin` plus an extra extension (`*.prx.*`, `eboot.bin.*`, …) across the whole game folder, leave them out of the `app0` copy, keep them under `.ps5kitsune\`, and list them on the summary screen. Moving every `.esbak` out of `app0` by hand was enough to convert the game.

## Build mode on Linux

Milestone 8a supports release mode only on Linux. Build mode needs the system GCC and the build dependency packages listed in AnyPS5's BUILD.md, an install command per distribution, and a child-process job: Linux has no job object, so git and CMake started by a PS5Kitsune that is killed outright keep running (`IChildProcessJob` is a no-op there).

## Required OS in profiles and system facts

`ProfileLoader.LoadEmbedded` and `LoadActive` default `os` to Windows, and `SystemFacts` defaults to the Windows texts. A caller that forgets them gets Windows behaviour on Linux silently; make them required.

## "Exit 0 = normal quit" on Linux

In WSL AnyPS5 exited with code 0 when DOOM failed (`I_AllocLow: cannot allocate`). `CrashDiagnoser` treats exit 0 as a normal quit; check on real Linux whether that still holds, or diagnose from the log even after exit 0.

## Known issue: DOOM crashes at the quit prompt in build mode on Windows

With AnyPS5 built from `7fcab08` (v0.1.4) on Windows, DOOM plays, but choosing Quit ends it with `0xC0000005` before the confirm screen shows: a string function in `ucrtbase.dll` reads a bad address while DOOM writes the quit message. The release download of the same tag shows the confirm screen and exits with 0, on Windows and on Linux. It is an AnyPS5 issue (the locally built libraries), seen on 2026-10-31 on the AMD laptop. Report it upstream with `ps5kitsune diagnose PPSA99666 --report`, and drop this entry once a build-mode commit quits cleanly.
