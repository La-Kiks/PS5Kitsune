# TODO

Items to investigate at a later date.

## Keep backup files out of `app0` when adding a game

Some dumps ship backups next to the real files, such as `eboot.bin.esbak` or `sce_module/libc.prx.esbak` (seen with Demon's Souls, PPSA01342). The relinker matches module names loosely, so a backup and its original both answer to the same dependency name, and conversion fails with `Ambiguous guest dependency name: libc.prx`. The validation step should find files named like a module or `eboot.bin` plus an extra extension (`*.prx.*`, `eboot.bin.*`, …) across the whole game folder, leave them out of the `app0` copy, keep them under `.ps5kitsune\`, and list them on the summary screen. Moving every `.esbak` out of `app0` by hand was enough to convert the game.
