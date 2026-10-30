# Milestone 8a — Linux CLI (release mode): design

October 30, 2026 · Complements [docs/architecture.md](../../architecture.md) ("Linux compatibility", "Data and configuration") and the [milestone 7c spec](2026-10-25-milestone-7c-crash-diagnosis-design.md).

## Goal

The command-line tool runs on Linux in release mode: `check`, `install`, `add` (and reconverting), `list`, `run`, `settings`, `diagnose` and `use release` take DOOM for PS5 from a folder to a running game on Ubuntu. Core keeps one pipeline for both OSes. What differs comes from a profile file per OS and from a new `PS5Kitsune.Platform.Linux` project.

Milestone 8 is split in two: **8a** (this spec) is the Linux CLI in release mode; **8b** is the Avalonia app on Linux and the `linux-x64` packaging.

**Criterion:** the CLI path in WSL Ubuntu, then DOOM played on a real Ubuntu install. See [Criterion](#criterion).

**Out of scope:**

- **8b:** the app on Linux, `linux-x64` packages, a desktop shortcut. The relinker already writes a `.desktop` file when `icon0.png` exists.
- **Build mode on Linux** (system GCC, the build dependency packages). `install --mode build` and `use build` refuse on Linux. Recorded in `TODO.md`.
- **Running anything as root**, `pkexec` included. A missing package shows the command to copy.
- **A display setting (`SDL_VIDEODRIVER`).** SDL chooses Wayland, then X11, on its own (see [Facts](#facts-checked-on-2026-10-30)); the game inherits the environment, so `SDL_VIDEODRIVER=x11 ps5kitsune run …` works without one. The architecture keeps the row, marked "if a game needs it".
- **Distributions other than** the Debian, Fedora and Arch families for package commands. Others get the package names only.

## Facts checked on 2026-10-30

From the AnyPS5 v0.1.3 release (the release the embedded profile pins), inspected in WSL Ubuntu 24.04:

- **Release assets:** `relinker-v0.1.3` (Linux, SHA-256 `bd2a959885830e14f091477edf297fc4c3f45b83a29171c47f5719d721fedb45`) next to `relinker-v0.1.3.exe`, and `prx-linux-v0.1.3.zip` (SHA-256 `017bdb0e931fa0cdb17f0cea805a824180b88205f5c50f4473550c63a4e811f3`, 352 entries under `libs/`) next to the Windows ones. Both hashes match the digests GitHub publishes.
- **The Linux relinker is statically linked** (`ldd`: "not a dynamic executable"): it needs nothing installed.
- **The 352 Linux libraries** (`libs/*.prx`) link only against `libc`, `libm`, `libstdc++` and `libgcc_s`. They load at run time `libvulkan.so.1`, Wayland (`libwayland-client`, `-cursor`, `-egl`, `libdecor-0`, `libxkbcommon`), X11 (`libX11`, `libX11-xcb`, `libXext`, `libXrandr`), `libpulse`, `libasound` and `libdbus-1`. A desktop distribution has all of them except, possibly, the Vulkan loader and a driver. SDL2 is linked into `libSceAgcDriver.prx` and the audio libraries; it reads `SDL_VIDEODRIVER` and defaults to `wayland,x11`. When Vulkan cannot be loaded, the game logs a line starting with `Vulkan loader: `.
- **Python:** the scripts need Python 3 and only the standard library (`USAGE.md`); Ubuntu ships `python3`.
- **`/dev/udmabuf`** (`USAGE.md`): used when the Vulkan driver imports dma-buf memory, which is every driver but NVIDIA's proprietary one. The user needs read-write access, usually through the `kvm` group (`root:kvm`, `0660`). Without it the game prints `[gpu] open /dev/udmabuf: <reason>; shared direct memory is copied instead of imported, …` and can later fail with `BDA access failed: address=0x…`.
- **Output** (`USAGE.md`): without `--windows` the relinker writes a Linux ELF; `app.elf` is the documented name. It writes the executable and each `Executable file:` it lists with execute permission; a copy that lost the file mode needs `chmod +x`. On Linux, `app0/` must not contain symbolic links.
- **WSL2** (Ubuntu 24.04, kernel 5.15.167.4): Vulkan sees only `llvmpipe` (with `shaderInt64` and `bufferDeviceAddress`); the kernel is built without `CONFIG_UDMABUF` and has no `/dev/kvm`. Good for everything but playing a game.
- **`posix_spawn` with `POSIX_SPAWN_SETSID`** (tested in WSL with a throwaway program): the child gets its own session, the file actions send stdout and stderr to the log, and `waitpid` returns its exit code even while .NET starts and reaps its own `Process` children (.NET only reaps the PIDs it started).

## Structure

The rule from the architecture holds: Core has no OS code, and the CLI and the app pick the platform at startup.

- **`PS5Kitsune.Platform.Linux`** (new, `net10.0`, Linux-only APIs marked `[SupportedOSPlatform("linux")]`) and **`PS5Kitsune.Platform.Linux.Tests`**, whose tests skip on Windows the way the Windows tests skip on Linux.
- **Choosing the platform:** `PlatformServices.Detect()` and `AddPlatform` move out of `Platform.Windows` into a small shared entry point that references both Platform projects and registers the one for the running OS. The CLI (`CommandHost`) and the app (`App.axaml.cs`) call it. Any other OS still gets `PlatformNotSupportedException`.

### `IPlatform` on Linux

| Member | Linux |
| --- | --- |
| `Name` | `Linux` |
| `DefaultDataRoot` | `$XDG_DATA_HOME/ps5kitsune`, else `~/.local/share/ps5kitsune` |
| `SettingsDirectory` | `$XDG_CONFIG_HOME/ps5kitsune`, else `~/.config/ps5kitsune` |
| `ExecutableSuffix` | empty (the relinker, tools) |
| `GameExecutableSuffix` (new) | `.elf`; `.exe` on Windows |
| `GetCommitLimitBytes()` | `null` |
| `GetVulkanDriverManifests()` | the `*.json` files of `/usr/share/vulkan/icd.d` and `/etc/vulkan/icd.d` |
| `OpenFix` | nothing to open: Linux fixes are commands, printed by the check |
| `OpenPath` | `xdg-open` |

The data root keeps its "no spaces" rule on both OSes.

### Other Linux services

- **`DistroPackages`**: reads `/etc/os-release` (`ID`, then each `ID_LIKE` word) and maps a need (Python, Vulkan loader, Mesa Vulkan drivers) to a command:

  | Family | Python | Vulkan loader | Mesa (Lavapipe and RADV) |
  | --- | --- | --- | --- |
  | Debian, Ubuntu | `sudo apt install python3` | `libvulkan1` | `mesa-vulkan-drivers` |
  | Fedora | `sudo dnf install python3` | `vulkan-loader` | `mesa-vulkan-drivers` |
  | Arch | `sudo pacman -S python` | `vulkan-icd-loader` | `vulkan-swrast` |

  Several needs combine into one command. An unknown distribution, or an unreadable `os-release`, gives "Install: <package names>" with the Debian names.
- **`SystemPython`** (`IPythonRuntime`): `python3` found on `PATH`. Nothing to download.
- **`SystemLavapipe`** (`ILavapipe`): the first `lvp_icd*.json` in the manifest folders above. None: `LavapipeUnavailableException` with Mesa's install command.
- **`LinuxGameLauncher`** (`IGameLauncher`): see [Run](#run).
- **`IChildProcessJob`**: does nothing on Linux. In release mode the tools are the relinker and Python, both short, and cancelling already ends them. A PS5Kitsune killed outright leaves them to finish. To revisit together with build mode on Linux, where git and CMake run for minutes.

### Core changes

- **`IPythonRuntime`** (`Executable`, plus making sure it exists): Windows registers `PythonEmbeddableTool`, Linux `SystemPython`. `AuditStep`, `UnwrapSelfStep`, `AnyPS5Installer`, `GameImporter` and `GameReconverter` depend on it instead of `PythonEmbeddableTool`.
- **`ILavapipe`** already exists: Windows keeps `MesaTool`, Linux gets `SystemLavapipe`.
- **Mode guard:** on Linux, `InstallModes` offers release only; asking for build gives "Build mode is not available on Linux yet; use release mode."

## Profile (schema 2)

- **One embedded file per OS:** `default-profile.windows.json` (today's profile, moved) and `default-profile.linux.json`. Only the running OS's file is loaded, as the embedded profile and as the base an override replaces.
- **`"os"`**, new and required: `"windows"` or `"linux"`. `CurrentSchemaVersion` becomes 2.
- **`relinker.windowsArgs` becomes `relinker.args`:** `["--windows", "--registry"]` on Windows, `["--registry"]` on Linux. `intelArg` is unchanged.
- **An `override.json`** for the other OS, or still on schema 1, is ignored with the existing message ("Ignored <path>: … Using the embedded profile."), which names the reason (wrong OS, or schema 1).
- **The Linux profile:**
  - `id`, `source`, `build`, the `scripts` and `unwrapScript` assets, `fonts` and `checks`: the same as Windows.
  - `assets.relinker`: `relinker-v0.1.3`. `assets.libs`: `prx-linux-v0.1.3.zip` (the zip, because zip extraction exists and libraries need no execute bit).
  - `tools`: none.
  - `validated`: `false` until the real-Ubuntu run of the criterion, then `true` in the same PR.
  - `diagnostics`: the rules whose texts are the same on both OSes (GPU features, `param.json`, settings errors, unimplemented features, memory), plus Linux rules:

    | Id | Matches | Explanation and fix |
    | --- | --- | --- |
    | `udmabuf-access` | `open /dev/udmabuf: ` | No access to `/dev/udmabuf`: `sudo usermod -aG kvm $USER`, then log out and in. |
    | `bda-access` | `BDA access failed` | GPU stores failed, usually after the udmabuf message: same fix; if `/dev/udmabuf` is absent, the kernel lacks it. |
    | `shared-library-missing` | `error while loading shared libraries: (?<library>\S+)` | A system library is missing: names it. |
    | `vulkan-missing` | `Vulkan loader: ` | Vulkan could not be loaded: the distribution's command for the loader and Mesa. |

    The Windows-only rules (DLL errors 126 and 193, the NVIDIA shader compiler, the commit limit) stay in the Windows profile only.
- **A test** checks that the shared parts of both profiles stay identical: `id`, `source`, `build`, the `scripts` and `unwrapScript` assets, `fonts` and `checks`.

## Install (release mode)

- The steps do not change: download, SHA-256, extract the libs and the scripts, write `install.json`.
- After downloading the relinker, Core gives it the execute bit with `File.SetUnixFileMode` (a cross-platform .NET API, skipped on Windows).
- **Python:** on Linux the step checks that `python3` exists instead of downloading. Missing: the install stops with the distribution's command.

## Add a game and Reconvert

- **`RelinkStep`** passes `profile.Relinker.Args` (plus `intelArg` when needed) and names the output `<titleId>` + `GameExecutableSuffix`: `PPSA99666.elf` on Linux.
- **Audit and unwrap** run through `IPythonRuntime`. The rest is unchanged: validation, copy into `app0`, the ELF-only copy given to the audit.
- **Symbolic links:** the copy into `app0` writes real files: a symbolic link in the game folder, to a file or to a folder, is copied as the content it points to. A Linux test covers both.
- **Reconvert** keeps its lock and its move-aside folder. The Windows check that opens the exe with `FileShare.None` to see a running game does not exist on Linux, where a running file can be renamed. There, a running game is found through `running.json` and the `Attach` check.

## Run

- **Execute bits:** before each launch, `run` makes sure the game executable and every `Executable file:` the relinker listed (kept from the relink output) have the user execute bit, adding it if needed.
- **Starting** (`LinuxGameLauncher.Launch`): `posix_spawn` through P/Invoke with `POSIX_SPAWN_SETSID`, so the game has its own session and outlives the CLI and the terminal. File actions open the log file on stdout and stderr and `/dev/null` on stdin. The environment is the inherited one plus `GameLaunchRequest.Environment` (`ANYPS5_SYSTEM_FONTS`, `VK_DRIVER_FILES` for Lavapipe, per-game settings), as on Windows. Old logs are trimmed as on Windows.
- **Start time and executable:** `StartedAt` comes from `/proc/<pid>/stat` (start time in clock ticks since boot, plus the boot time from `/proc/stat`'s `btime`). The executable is `/proc/<pid>/exe`.
- **Waiting:** a game started here is waited for with `waitpid` on a dedicated thread. A game found again with `Attach` is not our child: a `/proc/<pid>` check every 500 ms (gone, a zombie, or another start time means it exited; its exit code is unknown, reported as -1 as on Windows when it cannot be read).
- **`Attach`:** null when `/proc/<pid>` is gone, the process is a zombie, or its start time or executable differ from `running.json`.
- **`Stop`:** `SIGTERM` to the game's process group, wait the grace period (5 seconds, as on Windows), then `SIGKILL`.

## Checks on Linux

| Check | Linux behaviour |
| --- | --- |
| CPU extensions | Unchanged (.NET intrinsics). |
| Vulkan | Unchanged probe in a child process (Silk.NET loads `libvulkan.so.1`). When the loader is missing: "Vulkan is not installed" with the distribution's command; blocking. |
| Commit limit | "Not applicable on Linux"; not shown as a problem. |
| Disk space, data folder | Unchanged. |
| Python (new, Linux) | `python3` on `PATH`. Missing: blocking, with the command. |
| `/dev/udmabuf` (new, Linux) | Read-write access: OK. No access: **Warning**, with `sudo usermod -aG kvm $USER` then logging out and in. Device absent: **Warning**, "This kernel does not provide /dev/udmabuf; games copy shared memory instead of importing it, and some may fail." Skipped when every GPU uses NVIDIA's proprietary driver. Never blocking. |
| Lavapipe | Shown as available when `SystemLavapipe` finds a manifest; otherwise its fix is Mesa's install command. Forcing Lavapipe on with no manifest fails the launch with `LavapipeUnavailableException`. |

## CLI

- No new command or option. Text that names Windows-only things (the commit limit, DLLs, `.exe`) comes from the profile or the platform, never hard-coded in a command.
- `install --mode build` and `use build` exit with code 1 on Linux with the mode-guard message.

## Errors

| Situation | Result |
| --- | --- |
| `python3` missing during `install` or `add` | Stops with "Python 3 is required: <command>". |
| No Lavapipe manifest with Lavapipe forced on | `run` stops with Mesa's install command (`LavapipeUnavailableException`). |
| Build mode asked on Linux | Exit 1, "Build mode is not available on Linux yet; use release mode." |
| `override.json` for the other OS or on schema 1 | Ignored; the embedded profile is used and the log says why. |
| `posix_spawn` fails | `run` stops with the error text (for example `Permission denied` when the execute bit cannot be set). |
| Game exits with a known log message | `diagnose` and the exit line use the Linux profile's rules. |

## Tests

- **`Platform.Linux.Tests`** (run on Ubuntu, in CI and in WSL; skipped on Windows):
  - `/etc/os-release` parsing and package commands, from text fixtures (Ubuntu, Debian, Fedora, Arch, Linux Mint through `ID_LIKE`, unknown, missing file).
  - `/proc/<pid>/stat` parsing (a process name with spaces and parentheses) and the start-time conversion, from fixtures.
  - XDG folders with and without the variables.
  - Lavapipe manifest search in a temporary folder.
  - The launcher with real processes: launch `sh` writing to stdout and stderr, then the log content and the exit code; `Attach` to a running `sleep`; a mismatched record gives null; `Stop` ends a process on `SIGTERM`, and one that ignores `SIGTERM` after the grace period; the process keeps running after its parent's process group goes away.
- **Core tests:**
  - Both embedded profiles load and validate; the shared parts are identical.
  - An override for the other OS, or on schema 1, is ignored with its reason.
  - `RelinkStep` arguments and output name for each OS (fake `IPlatform`).
  - The relinker download gets the execute bit (Linux only).
  - The mode guard refuses build on Linux.
- **Windows unchanged:** the 940 existing tests stay green on Windows.
- **CI:** the Ubuntu job also runs `dotnet publish src/PS5Kitsune.Cli -r linux-x64 --self-contained`, so a Windows dependency in what Linux ships fails the build.

## Criterion

By hand:

1. **WSL Ubuntu 24.04:** `ps5kitsune check` shows llvmpipe, the udmabuf warning (device absent) and the commit limit as not applicable. `install`, then `add` DOOM (`PPSA99666.zip`): the same audit verdict as on Windows with v0.1.3, orange 360/4/0. `list`, `settings` and `diagnose PPSA99666` work.
2. **Real Ubuntu on the AMD laptop** (live USB with persistence, or dual boot): `check`, `install`, `add`, `run` DOOM, and it is playable. The Linux profile then gets `validated: true`.

The PR is opened after step 1 and merged after step 2.

## Docs

- **`docs/architecture.md`, "Linux compatibility":**
  - The `SDL_VIDEODRIVER` row says "if a game needs it; SDL picks Wayland, then X11".
  - Python: the system `python3`. Lavapipe: the system Mesa manifest. udmabuf: a warning, skipped for NVIDIA's proprietary driver.
  - Note the profile per OS (schema 2) and `GameExecutableSuffix`.
  - The roadmap gets 8a (this spec) and 8b (the app, packaging); build mode on Linux is listed as later.
- **README:** a Linux section (release mode only, the packages to install, the kvm group); the Roadmap line for 8 shows the split.
- **`TODO.md`:** build mode on Linux, and a real child-process job on Linux.
