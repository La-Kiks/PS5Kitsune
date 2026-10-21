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

Early development — milestone 4 of 8 (install screens). The app installs AnyPS5 (release mode) from its screens; adding games still goes through the command line. The design and roadmap are in [docs/architecture.md](docs/architecture.md).

The command-line tool installs AnyPS5, adds a game and runs it:

```powershell
ps5kitsune check                     # can this PC run AnyPS5? CPU, Vulkan, memory, disk, data folder
ps5kitsune install                   # checks, then Python, relinker, libraries and scripts of AnyPS5 v0.1.2
ps5kitsune install --mode build      # same result, but AnyPS5 is built from source (see below)
ps5kitsune add <game folder or zip>  # validate, copy, unwrap, convert and audit
ps5kitsune run <title ID>            # launch from the game folder, log in .ps5kitsune\logs
```

Every command takes `--root <data root>`; without it, the CLI uses the data folder chosen in the app, else `C:\PS5Kitsune`. `check` and `install` exit with code 2 when a check is blocking.

### Build mode

`ps5kitsune install --mode release|build` picks how AnyPS5 is installed. Release mode downloads the published release. Build mode downloads MinGit and WinLibs, fetches the profile's commit and compiles it. It needs more disk space (15 GiB checked for now) and a long first run. An interrupted build resumes where it stopped, and a new commit builds incrementally.

Without `--mode`, the CLI resumes an unfinished install in the mode it was started in, else uses the mode of the current install, else `release`. The command starts with three lines that say what it will use:

```text
Data root: C:\PS5Kitsune (saved setting)
Profile: doom-validated-2026-10 (embedded)
Mode: build (current install)
```

The data root comes from `--root`, `saved setting` or `default`. The mode comes from `--mode`, `unfinished install`, `current install` or `default`. If the active profile cannot build (no `build` section), the command stops right after these lines and suggests `--mode release`.

Switching modes starts with a "Remove the other mode's files" step that empties `anyps5\current`; the clone and the build folder are kept, so going back to build mode is incremental. The console shows the steps, the build progress and the compiler's error lines; the full git and CMake output is in the log under `<root>\logs`.

To build another commit, copy the profile to `<root>\profiles\override.json`, change `build.commit`, then run `ps5kitsune install`. A valid override replaces the embedded profile (the second line then says `override`). An invalid one is ignored, with a warning in the log. The app's Install page installs in release mode until the build UI arrives. The app's Home page shows a build install, but repairs or updates it only from the command line (`ps5kitsune install`) until then.

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
