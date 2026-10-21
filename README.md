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

Early development — milestone 3 of 8 (PC checks). The app shows a PC check screen; installing and adding games still go through the command line. The design and roadmap are in [docs/architecture.md](docs/architecture.md).

The command-line tool installs AnyPS5, adds a game and runs it:

```powershell
ps5kitsune check                     # can this PC run AnyPS5? CPU, Vulkan, memory, disk, data folder
ps5kitsune install                   # checks, then Python, relinker, libraries and scripts of AnyPS5 v0.1.2
ps5kitsune add <game folder or zip>  # validate, copy, unwrap, convert and audit
ps5kitsune run <title ID>            # launch from the game folder, log in .ps5kitsune\logs
```

Every command takes `--root <data root>`; the default is `C:\PS5Kitsune`. `check` and `install` exit with code 2 when a check is blocking.

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
