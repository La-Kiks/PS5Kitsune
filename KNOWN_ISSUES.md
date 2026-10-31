# Known issues

Problems you may hit with PS5Kitsune, and how to get around them. To report a new one, open an [issue](https://github.com/La-Kiks/PS5Kitsune/issues) with the text `ps5kitsune diagnose <title ID> --report` prints (Report on the Game page in the app).

## Adding a game fails with "Ambiguous guest dependency name"

Some dumps ship backups next to the real files, such as `eboot.bin.esbak` or `sce_module/libc.prx.esbak` (seen with Demon's Souls, PPSA01342). The relinker matches module names loosely, so a backup and its original both answer to the same dependency name, and conversion fails with `Ambiguous guest dependency name: libc.prx`.

**Workaround:** move every `.esbak` file (and any other copy named like `eboot.bin.*` or `*.prx.*`) out of the game folder, then add the game again.

**Planned fix:** the validation step finds these files across the whole game folder, leaves them out of the `app0` copy, keeps them under `.ps5kitsune\`, and lists them on the summary screen.

## Build mode on Windows: DOOM crashes at the quit prompt

With AnyPS5 built from `7fcab08` (v0.1.4) on Windows, DOOM plays, but choosing Quit ends it with `0xC0000005` before the confirm screen shows: a string function in `ucrtbase.dll` reads a bad address while DOOM writes the quit message. The release download of the same tag shows the confirm screen and exits with 0, on Windows and on Linux. It is an AnyPS5 issue (the locally built libraries), seen on 2026-10-31.

**Workaround:** use release mode (`ps5kitsune install --mode release`, then `ps5kitsune use release`, or the mode choice in the app), then reconvert the game.

**Status:** to be reported upstream with `ps5kitsune diagnose PPSA99666 --report`. This entry goes once a build-mode commit quits cleanly.

## On Linux, a failed game may look like a normal quit

In WSL, AnyPS5 exited with code 0 when DOOM failed (`I_AllocLow: cannot allocate`). PS5Kitsune treats exit code 0 as a normal quit, so the Game page shows no diagnosis. Not yet checked on a real Linux install.

**Workaround:** if a game closes on its own, open its last log from the game's menu and look at its last lines. `ps5kitsune diagnose` does not help here: it reads the same exit code from the log. `diagnose <title ID> --report` still prints the text for a bug report.

**Planned fix:** check on a real Linux install whether exit code 0 still comes with a failure; if it does, diagnose from the log even after exit code 0.

## Build mode is Windows-only

On Linux, only release mode is supported: `install --mode build` and `use build` refuse. Release mode installs the same AnyPS5 version. See [TODO.md](TODO.md) for what build mode on Linux needs.
