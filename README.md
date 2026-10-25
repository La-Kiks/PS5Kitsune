<p align="center">
  <img src="assets/branding/kitsune-icone-encre.svg" alt="PS5Kitsune logo" width="128">
</p>

# PS5Kitsune

A desktop installer and launcher for [AnyPS5](https://github.com/boykopovar/AnyPS5), the project that relinks PS5 executables into native Windows and Linux programs.

PS5Kitsune automates the manual AnyPS5 procedure:

1. **Install** — check the PC (CPU extensions, Vulkan, `shaderInt64`, commit limit), then download or build AnyPS5 with portable tools. Nothing is installed system-wide and no administrator rights are needed.
2. **Add a game** — validate a game folder you supply, convert it with the relinker, and audit its imports.
3. **Launch** — a library of converted games, launched from the right folder with per-game settings and readable error diagnosis.

> **PS5Kitsune is not affiliated with AnyPS5.** It is a third-party installer that downloads AnyPS5 from its [official repository](https://github.com/boykopovar/AnyPS5) only. Report AnyPS5 issues there, not here.

## Status

Early development — milestone 6 of 8 (add a game; 6a, add a game from the app, done; 6b, reconvert, in progress). The app installs AnyPS5 from its screens, as a release or built from source, and both installs can stay side by side; adding a game is also possible from the app. The design and roadmap are in [docs/architecture.md](docs/architecture.md).

The command-line tool installs AnyPS5, adds a game and runs it:

```powershell
ps5kitsune check                     # can this PC run AnyPS5? CPU, Vulkan, memory, disk, data folder
ps5kitsune install                   # checks, then Python, relinker, libraries and scripts of AnyPS5 v0.1.2
ps5kitsune install --mode build      # same result, but AnyPS5 is built from source (see below)
ps5kitsune use release|build         # switch between the release and build installs, both kept side by side
ps5kitsune add <game folder or zip>  # validate, copy, unwrap, convert and audit
ps5kitsune reconvert <title ID>      # convert a game again after an AnyPS5 update
ps5kitsune run <title ID>            # launch from the game folder, log in .ps5kitsune\logs
ps5kitsune list                      # the added games, their verdict, and whether they run
ps5kitsune remove <title ID>         # delete a game and its folder (asks first; --yes skips)
ps5kitsune settings <title ID>       # show a game's settings and the environment variables they become at launch (edit them in the app)
ps5kitsune diagnose <title ID>       # look for a known AnyPS5 error in the game's last log and say how to fix it (--report: text for a bug report)
```

In the app, the Library lists the added games and opens first once a game is added: one click launches a game and follows its log, and closing the app leaves the game running (reopening finds it again). Each game's menu opens its folder or its last log, reconverts it, clears its shader cache or removes it. Home's "Add a game" takes a folder or a .zip (picked or dropped), shows its summary, adds it and shows the audit verdict. After an AnyPS5 update, `ps5kitsune reconvert <titleId>` (or Reconvert in the app) converts a game again without copying it, keeping its saves; a failure puts the previous conversion back. After a game stops with an error AnyPS5 is known to print, the Game page explains it and offers its fix (Repair libs, Turn on CPU rendering, Reset a setting, …), or Report for an error nobody explains yet.

Every command takes `--root <data root>`; without it, the CLI uses the data folder chosen in the app, else `C:\PS5Kitsune`. `check` and `install` exit with code 2 when a check is blocking.

### Build mode

`ps5kitsune install --mode release|build` picks how AnyPS5 is installed. Release mode downloads the published release. Build mode downloads MinGit and WinLibs, fetches the profile's commit and compiles it. It needs more disk space (10 GiB checked) and a first run of about 12 minutes on an 8-thread laptop. An interrupted build resumes where it stopped, and a new commit builds incrementally.

Without `--mode`, the CLI resumes an unfinished install in the mode it was started in, else uses the mode of the current install, else `release`. The command starts with three lines that say what it will use:

```text
Data root: C:\PS5Kitsune (saved setting)
Profile: doom-validated-2026-10 (embedded)
Mode: build (current install)
```

The data root comes from `--root`, `saved setting` or `default`. The mode comes from `--mode`, `unfinished install`, `current install` or `default`. If the active profile cannot build (no `build` section), the command stops right after these lines and suggests `--mode release`.

The release and the build each live in their own folder (`anyps5\installs\release`, `anyps5\installs\build`), so installing one never removes the other. A successful install becomes the one in use; a failed or cancelled one never changes it. The clone and the build folder are kept, so going back to build mode is incremental. The console shows the steps, the build progress and the compiler's error lines; the full git and CMake output is in the log under `<root>\logs`.

To build another commit, copy the profile to `<root>\profiles\override.json`, change `build.commit`, then run `ps5kitsune install`. A valid override replaces the embedded profile (the second line then says `override`). An invalid one is ignored, with a warning in the log. In the app, choose "Build from source" on the Mode screen: the PC check then uses the build threshold, and a Tools screen shows what will be downloaded before the build starts.

## What PS5Kitsune does not do

PS5Kitsune does not dump games, decrypt anything, or download any Sony files. It only processes folders that you supply. You are responsible for the provenance of the files you convert and for using them in accordance with applicable laws and their license terms.

## Building

Requirements: Windows 10 or 11 (x64) and the .NET 10 SDK.

```powershell
dotnet build PS5Kitsune.slnx -c Release
dotnet test --solution PS5Kitsune.slnx -c Release
```

| Project | Role |
| --- | --- |
| `src/PS5Kitsune.Core` | Business logic, no UI or OS dependency |
| `src/PS5Kitsune.Platform.Windows` | Windows-specific implementations |
| `src/PS5Kitsune.Cli` | `ps5kitsune` command-line tool |
| `src/PS5Kitsune.App` | Avalonia desktop app |
| `tests/PS5Kitsune.Core.Tests` | Unit tests |
| `tests/PS5Kitsune.App.Tests` | View model tests |
| `tests/PS5Kitsune.Platform.Windows.Tests` | Windows tests (skipped on Linux) |

## License

PS5Kitsune is licensed under the [GNU General Public License version 2 only](LICENSE) (`GPL-2.0-only`), the same license as AnyPS5. Every dependency shipped with the app must be GPLv2-compatible; CI checks this on each push.
