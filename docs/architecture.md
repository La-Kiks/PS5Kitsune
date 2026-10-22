# PS5Kitsune — architecture plan

Revision 2 (after review) · October 18, 2026

## Goal and scope

A single desktop application, in C# / .NET and Avalonia, that automates the manual AnyPS5 procedure in three successive versions. Each version adds screens without rewriting the previous ones.

| Version | What the user gets | Replaces in the manual procedure |
| --- | --- | --- |
| V1 — Installer | A checked PC and a ready AnyPS5 (relinker + libs + scripts), downloaded or built | Prerequisites + step 1 |
| V2 — Add a game | A converted and audited game folder, ready to launch | Steps 2 to 5 |
| V3 — Launcher | A library of installed games, one-click launch, error diagnosis | Step 6 + common errors |

**Out of scope:** dumping games, any decryption and any retrieval of Sony files. The application only processes folders supplied by the user; the reference test game is [DOOM for PS5](https://github.com/lowbit/ps5-doom).

**Name:** `PS5Kitsune`. It is deliberately distinct from "AnyPS5", so as not to be confused with the official project, whose README already warns about sites reusing its name. The app presents itself as a third-party installer for AnyPS5, with a link to the official repository.

**License and repository:** GPL version 2 only (`GPL-2.0-only`), like AnyPS5. The code is published in a public GitHub repository, with the license text (`LICENSE`) at the root and the SPDX identifier in every .NET project (`<PackageLicenseExpression>GPL-2.0-only</PackageLicenseExpression>`). This allows reusing AnyPS5 code in the app later, for example by porting a Python script to C#, without compatibility issues. Dependencies shipped with the app must be GPLv2-compatible: MIT, BSD or LGPL are fine, **Apache 2.0 is not** (incompatible with GPLv2 according to the FSF). This rules out Serilog (Apache 2.0) in favor of NLog (BSD-3). xUnit is also Apache 2.0, but it is only used by tests and never shipped. CI checks the license of every shipped NuGet package (milestone 1).

**Language:** code, documentation, commit messages and UI are in English.

## Guiding principles

Four decisions made during the review, which shape everything else:

1. **Everything is portable, nothing is installed system-wide.** Git, Python and WinLibs (with CMake/Ninja) are downloaded as archives into the app's root, with pinned versions and verified hashes. No `winget`, no elevation, nothing added to the system `PATH`. Uninstalling = deleting a folder.
2. **A pinned AnyPS5 profile by default.** The app installs an AnyPS5 version tested with DOOM (release tag or commit), not the head of `main`. "Latest" is an explicit choice for advanced users.
3. **Everything that depends on AnyPS5 lives in the profile, not in code:** commands, options, paths, error patterns, hashes. Defaults are embedded and can be overridden by a local file or by a profile downloaded from the app's repository.
4. **End to end first, screens second.** The full chain (install → convert DOOM → audit → launch) runs from the CLI before any UI is polished.

## Technical choices

The stack is .NET LTS + Avalonia with MVVM, published as a self-contained executable for `win-x64`, then `linux-x64`. Everything OS-specific goes through an interface, which keeps Linux possible without a rewrite.

| Component | Choice | Why |
| --- | --- | --- |
| Runtime | .NET 10 (LTS) | Long-term support, `System.Runtime.Intrinsics` to test CPU extensions |
| UI | Avalonia (latest stable) + Fluent theme | Same UI code on Windows and Linux |
| MVVM | CommunityToolkit.Mvvm | Source generators for `ObservableProperty` and `RelayCommand`, little boilerplate |
| Dependency injection | Microsoft.Extensions.DependencyInjection | Pick the Windows or Linux implementation at startup |
| Process execution | CliWrap for tools (git, cmake, python, relinker); native `Process` with inherited file handles for the game | Tools are followed line by line; the game must not depend on the launcher staying alive (see V3) |
| Logging | NLog via `Microsoft.Extensions.Logging` (file + view in the UI) | One log per operation, attachable to a report; BSD-3 license, GPLv2-compatible (Serilog, under Apache 2.0, is not) |
| Downloads | `HttpClient` + GitHub REST API | Releases, WinLibs, MinGit, Python, Mesa; resume, progress, SHA-256 check |
| Archives | `System.IO.Compression` (zip) + SharpCompress (7z) | Mesa for Windows ships as `.7z` (to be confirmed at the Lavapipe milestone); Linux `tar.gz` via `System.Formats.Tar` |
| Tests | xUnit + fake test folders | Validate detection and folder validation without a real game |
| Publishing | `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true` | A single `.exe` (~80–100 MB), no .NET to install; Avalonia's native libraries are included |

**Administrator rights:** never required. The page file is never changed automatically: the app explains and opens the right settings panel.

**Python:** the repository's scripts (`import_audit.py`, `unwrap_self.py`) are called as-is rather than rewritten in C#. They only use the standard library ("needs Python 3 and nothing else", per `USAGE.md`), so the official **embeddable Python** (zip) is enough. It is needed **in both modes**, release and build. The build itself does not need Python (only its tests do).

**Signing:** the app executable is unsigned at first; SmartScreen will warn on first launch. To be documented, and reconsidered (certificate) if the app is widely distributed.

## Solution architecture

The logic lives in `Core`, with no reference to Avalonia or Windows. The UI only displays steps and their progress, and each OS provides its own implementation of `Core`'s interfaces.

Dependencies go one way only: the UI and the CLI only call Core, and only the Platform projects talk to Windows or Linux. The Linux project is planned, but not before V3.

```text
PS5Kitsune/
├── src/
│   ├── PS5Kitsune.Core/                business logic, no UI or OS dependency
│   │   ├── Checks/                     ISystemCheck: CPU, Vulkan, memory, disk…
│   │   ├── Tools/                      IPortableTool: MinGit, Python, WinLibs, CMake/Ninja, Mesa
│   │   ├── Profiles/                   AnyPS5Profile: pinned version, commands, patterns, hashes
│   │   ├── AnyPS5/                     IAnyPS5Source: ReleaseSource, SourceBuildSource
│   │   ├── Games/                      folder validation, conversion, audit (V2)
│   │   ├── Library/                    library and launching (V3)
│   │   ├── Pipeline/                   IStep, StepRunner, progress, resume
│   │   └── Abstractions/               IPlatform, IProcessRunner, IDownloader, IGameLauncher
│   ├── PS5Kitsune.Platform.Windows/    commit limit, Windows paths, Job Objects, P/Invoke
│   ├── PS5Kitsune.Platform.Linux/      (later) packages, XDG paths, udmabuf
│   ├── PS5Kitsune.App/                 Avalonia: Views, ViewModels, navigation
│   └── PS5Kitsune.Cli/                 same logic on the command line; isolated Vulkan probe
└── tests/
    └── PS5Kitsune.Core.Tests/          fake game folders, fake IProcessRunner
```

| Abstraction | Role | Example implementation |
| --- | --- | --- |
| `IPlatform` | OS-specific facts: default folders, executable suffix, relinker options, toolchain | `WindowsPlatform` returns `--windows`, `.exe`, WinLibs |
| `ISystemCheck` | One PC check, returns OK, Warning or Blocking, with a corrective action | `ShaderInt64Check` |
| `IPortableTool` | Detect, download, verify (SHA-256) and extract a tool into `tools\` | `MinGitTool`, `PythonEmbeddableTool`, `WinLibsTool` |
| `AnyPS5Profile` | Targeted AnyPS5 version and everything that depends on it (see "AnyPS5 profiles") | `doom-validated-2026-10` |
| `IAnyPS5Source` | Produce an AnyPS5 install: relinker path, libs folder, scripts, version | `ReleaseSource`, `SourceBuildSource` |
| `IStep` + `StepRunner` | A long step with progress, cancellation, logging and resume | `CloneStep`, `ConfigureStep`, `BuildLibsStep` |
| `IProcessRunner` | Run git, cmake, python, the relinker, reading output line by line | CliWrap wrapper, fakeable in tests |
| `IGameLauncher` | Launch a game detached from the launcher, output written straight to a file | `WindowsGameLauncher` (inherited handles) |

### Resuming steps

Each `IStep` exposes `IsAlreadyDone()`, based on **what the step produced** (the file exists, its hash is right, the clone's commit matches the profile) rather than on a state journal. If the user deletes a folder by hand, the step is simply redone. The `StepRunner` skips completed steps and resumes at the first one that is not. A done-check never trusts a file an external tool may leave half-written: tool outputs are moved into place or confirmed by a marker written after success. The copy steps still compare sizes after an in-place copy; copying to a temporary file and moving it is a follow-up. A done-check that throws counts as that step's failure; a cancellation emits a `Cancelled` event for the running step.

### Isolated Vulkan probe

Vulkan enumeration can crash the process (Dozen driver, some D3D12 drivers, see `USAGE.md`). So it **never runs in the UI process**: the app relaunches itself as `PS5Kitsune.exe --probe-vulkan`, which queries Vulkan via Silk.NET and writes JSON (GPUs, `shaderInt64`, `bufferDeviceAddress`, driver version). A probe crash becomes a result ("Vulkan enumeration failed") and triggers a second attempt with `VK_LOADER_DRIVERS_DISABLE='*dzn*'`. `vulkaninfo` is not used: it ships with the Vulkan SDK and is missing on most PCs. Both executables handle `--probe-vulkan` before anything else starts (`PS5Kitsune.exe` and `ps5kitsune.exe`); the parent waits 15 seconds at most per attempt. The thresholds (commit limit, disk space, NVIDIA drivers) live in the profile's `checks` section.

The CLI costs little once `Core` exists. It hosts the probe, drives the end-to-end path of milestone 2, and will be the first Linux deliverable.

## AnyPS5 profiles

A profile is a JSON file describing an AnyPS5 version **validated with DOOM** and everything that depends on it. It replaces constants scattered through the code.

```json
{
  "schemaVersion": 1,
  "id": "doom-validated-2026-10",
  "source": { "kind": "release", "tag": "v0.1.2" },
  "build": {
    "repository": "https://github.com/boykopovar/AnyPS5.git",
    "commit": "3fd1cc393b8512c2e989860053f30dee5a8cd1aa"
  },
  "assets": {
    "relinker": { "name": "relinker-{tag}.exe", "sha256": "88cc311d…" },
    "libs":     { "name": "prx-windows-{tag}.zip", "sha256": "b26e507e…" }
  },
  "tools": {
    "winlibs": { "url": "…/winlibs-x86_64-posix-seh-gcc-15.2.0-mingw-w64ucrt-14.0.0-r7.zip", "sha256": "…" },
    "mingit":  { "url": "…", "sha256": "…" },
    "python":  { "url": "…/python-3.14.8-embed-amd64.zip", "sha256": "…" }
  },
  "relinker": { "windowsArgs": ["--windows", "--registry"], "intelArg": "--to-intel" },
  "errorPatterns": "patterns-v1.json"
}
```

- **`build` section:** `repository` uses HTTPS and `commit` is 40 lowercase hex characters. Build mode installs that commit.
- **Resolution order:** the profile embedded in the app, or a valid local override `<root>\profiles\override.json`, which replaces it for both the CLI and the app. An override that cannot be read or is invalid is ignored, with a warning in the log. The profile downloaded from the PS5Kitsune repository (checked on demand) is still to come; the most recent valid one will then win, and the user will see which one is active.
- **"Latest" mode:** a synthetic profile that follows the latest release or `main`, with no pinned hash, flagged as untested in the UI and in game records.
- **Updating the default profile:** the maintainer validates a new tag or commit with DOOM and publishes the profile; users see "new validated version available" and affected games are marked "needs reconversion".

## V1 — AnyPS5 installer

V1 checks the PC, downloads the required portable tools, then produces a complete AnyPS5 install: relinker, `libs` folder with the 3 DLLs, and the repository's Python scripts. It offers two modes, release or build; the active profile says which one is recommended.

### PC checks

Each check returns OK, Warning or Blocking, with an explanation and an action. Thresholds come from the repository documentation ([USAGE.md](https://github.com/boykopovar/AnyPS5/blob/main/docs/user/USAGE.md), [BUILD.md](https://github.com/boykopovar/AnyPS5/blob/main/docs/dev/BUILD.md)).

| Check | How | On failure |
| --- | --- | --- |
| 64-bit Windows on x86-64 | `RuntimeInformation.OSArchitecture == X64` | Blocking (ARM not supported) |
| CPU extensions: AVX2, FMA, F16C, BMI1, BMI2, LZCNT, MOVBE | `Avx2.IsSupported`, `Fma`, `Bmi1`, `Bmi2`, `Lzcnt`; F16C and MOVBE via `X86Base.CpuId` (leaf 1, ECX) | Blocking: a converted game stops at startup naming the missing extension |
| CPU vendor | Vendor string via `X86Base.CpuId` (leaf 0) | Info: Intel enables `--to-intel` in V2, with the warning that games generating AMD instructions at runtime require an AMD CPU |
| Vulkan driver and GPUs | Isolated probe (see above) | Blocking if no driver |
| `shaderInt64` and `bufferDeviceAddress` per GPU | Same probe, feature by feature | Warning: offer Lavapipe (Mesa download in V1, activation in V3) |
| Commit limit (RAM + page file) | `GlobalMemoryStatusEx`: a game can commit up to 13,824 MiB | Warning + button opening `SystemPropertiesPerformance.exe`; no automatic change |
| Dozen driver (D3D12 over Vulkan) present | Vulkan driver manifests in the registry, and the probe's `driverId` (`MesaDozen`) | Info: can crash enumeration, can be disabled at launch in V3 |
| NVIDIA driver known to crash | Driver version read by the probe | Warning: 610.47 crashes in `nvgpucomp64.dll` (see `USAGE.md`); version list in the profile |
| Free disk space | `DriveInfo` on the install folder | Warning below the profile's `checks.minFreeDiskGiB` (5 GiB) in release mode, or `checks.buildMinFreeDiskGiB` (10 GiB) in build mode: a built root measured 3.2 GB (tools 1.1 GB, build 0.9 GB, source 0.5 GB, install 0.3 GB, downloads 0.3 GB), plus room for a game and for the build folder to grow |
| Install path | No spaces, ASCII only | Blocking: WinLibs and CMake fail otherwise |

### Portable tools

All tools go into `<root>\tools\`, downloaded from their official source, verified by SHA-256 (values in the profile), then extracted. They are passed to child processes through environment variables (the child process `PATH` only).

| Tool | Mode | Source | Notes |
| --- | --- | --- | --- |
| Embeddable Python 3.14 | Both | Official python.org zip (`python-3.14.x-embed-amd64.zip`); the scripts need 3.9 or newer | Enough for the scripts (standard library only); no pip, no venv. No more Microsoft Store alias to work around |
| MinGit 2.56.0.2 | Build | Official Git for Windows zip (`MinGit-2.56.0.2-64-bit.zip`) | Provides `git` and the CA bundle for `SSL_CERT_FILE`. The bundle is under `ucrt64`, with `mingw64` as the fallback |
| WinLibs GCC 15.2.0 r7 | Build | `winlibs-x86_64-posix-seh-gcc-15.2.0-mingw-w64ucrt-14.0.0-r7.zip` | Only supported Windows toolchain (BUILD.md). Includes CMake 4.3.1 and Ninja 1.13.2 (checked in the r7 release notes), so no other zip is needed |
| Mesa (Lavapipe) | On demand | `pal1000/mesa-dist-win` release | 7z archive, extracted with SharpCompress |

### Getting AnyPS5

**Release mode**

1. Read the release targeted by the profile via `GET /repos/boykopovar/AnyPS5/releases/tags/<tag>` (or `latest` in "latest" mode).
2. Download `relinker-<tag>.exe` and `prx-windows-<tag>.zip`. Verify each file against the `digest` field (`sha256:…`) that the GitHub API provides for every release asset, in addition to the profile's hash when present. The zip contains a `libs/` folder with the `.prx` files and the 3 DLLs; it may also contain `libTracyClient.dll`.
3. Download the source archive of the same tag and extract `tools\` and `core\libs\prx\` into `anyps5\installs\release\scripts\` **keeping the tree layout**. `import_audit.py` looks for throwing stubs in `../core/libs/prx` relative to itself by default: with this layout, no `--source` option is needed.
4. Show the release date and the profile status (validated or not).

**Build mode**

Layout under `anyps5\`: `source\` (git clone), `build\` (CMake build folder, outside the clone), `state\` (one marker per step, holding the commit) and `installs\build\` (the finished install, same layout as a release). A marker is written after its step succeeds, never before. A missing marker, or one holding another commit, means the step is not done.

| # | Step | Execution | Done when |
| --- | --- | --- | --- |
| 1 | Install MinGit | `MinGitTool.InstallAsync` | tool installed |
| 2 | Install WinLibs | `WinLibsTool.InstallAsync` | tool installed |
| 3 | Get the AnyPS5 source | In `source\`: `git init`, `git config core.longpaths true`, `git config remote.origin.url <repository>`, `git fetch --depth 1 origin <sha>`, `git checkout --detach --force <sha>` | `state\checkout.done` = commit |
| 4 | Update the submodules | `git submodule sync --recursive`, then `git submodule update --init --recursive --depth 1 --force` | `state\submodules.done` = commit |
| 5 | Configure the build | `cmake -S source -B build -G Ninja -DCMAKE_BUILD_TYPE=Release -DBUILD_TESTING=OFF -DCMAKE_MAKE_PROGRAM=<ninja> -DCMAKE_C_COMPILER=<gcc> -DCMAKE_CXX_COMPILER=<g++>` | `state\configure.done` = commit |
| 6 | Compile AnyPS5 | `cmake --build build --parallel N` | `state\compile.done` = commit |
| 7 | Build the libraries | `cmake --build build --target libs --parallel N` | `state\libs.done` = commit |
| 8 | Copy the build | See below | the build produced the relinker, at least one `.prx`, `import_audit.py` and `unwrap_self.py`, and every target file exists with the size and SHA-256 of its source |

- The commit is fetched by SHA at depth 1, so the clone stays small. The `remote.origin.url` setting is rewritten on every run, so a rerun after any interruption needs no special case.
- `BUILD_TESTING` is `OFF`, so the `libs` target is not part of `all` and is built separately (step 7).
- The compiler and `ninja` paths are full WinLibs paths with forward slashes, and `-DCMAKE_MAKE_PROGRAM` names Ninja, so CMake does not depend on `PATH` to find them.
- Every git and CMake command runs with the build environment: the child's `PATH` is `tools\winlibs\mingw64\bin`, `tools\mingit\cmd`, the inherited entries without `%LOCALAPPDATA%\Microsoft\WindowsApps` (the WSL and Store app aliases), then `tools\mingit\usr\bin` last (it also holds `find` and `sort`, which must not shadow Windows' own). The reason: libusb-cmake's `gen-describe.ps1` needs a non-WSL `bash.exe`, MinGit ships only `usr\bin\sh.exe` (GNU bash), so the install copies it to `bash.exe` and the WSL alias is kept off the `PATH`. `SSL_CERT_FILE` is set to MinGit's CA bundle (`ucrt64\etc\ssl\certs\ca-bundle.crt`, else `mingw64\...`). This avoids error 60 when CMake downloads FFmpeg. The app's own environment is never changed.
- Parallel jobs: `N = max(1, min(logical processors, physical RAM in GiB / 2))`. GCC can use about 2 GiB per job on large C++ files.
- Copy step: `build\core\relinker\relinker.exe` goes to `installs\build\relinker.exe`; `build\core\libs\libs\*.prx` plus `libgcc_s_seh-1.dll`, `libstdc++-6.dll` and `libwinpthread-1.dll` from WinLibs go to `installs\build\libs\`; `source\tools\` and `source\core\libs\prx\` go to `installs\build\scripts\`. These folders are mirrored. The result is identical in layout to a release install. The step first checks that the build produced the relinker, at least one `.prx`, `source\tools\import_audit.py` and `source\tools\unwrap_self.py`, and fails before copying anything when one is missing.
- Side by side: the release and the build each keep their own folder under `installs\`, so installing one never touches the other and switching back runs no step. `state\active` holds `release` or `build` and names the install games are converted with. An install becomes active only once every step succeeded; a failed or cancelled one never changes it. `source\`, `build\` and `state\` are kept, so going back to build mode is incremental.
- Mode of a plain `install`: `--mode`, else the mode of an unfinished install, else the mode of `install.json`, else release. The installer writes the requested mode to `state\requested-mode` before the first step and deletes it once every step succeeded, so an install cancelled or failed before `install.json` is written resumes in the same mode (`Mode: build (unfinished install)`). A mode the active profile does not support is reported right after the opening lines, before the PC check.
- Tool output: every git and CMake line, stdout and stderr, goes to the log file at Debug. The console shows the build's progress every 10 % and, once, each line that looks like an error (`error:`, `fatal:`, `CMake Error`, `FAILED:`). A sub-build that runs its own Ninja prints a smaller counter between the main one's lines: each command follows the counter with the largest total, so the progress never jumps back.
- `ps5kitsune use release|build` switches to an install that is already finished, with no step; it refuses an unfinished one, and an install whose update did not finish (exit code 1). It logs "AnyPS5 {mode} {version} is now in use ({directory})."
- The app shows a build install on Home ("built from source") and repairs, resumes or updates it like a release: Home opens Mode, which preselects build.
- Resume: an interruption during step 6 leaves no `compile.done`. The next run skips steps 1 to 5 and Ninja recompiles only what is missing.
- Update: a new commit in the profile makes every marker stale. Steps 3 to 8 run again, and CMake and Ninja reuse `build\` incrementally.
- AnyPS5's CI configures with `BUILD_TESTING=ON` and ccache, and puts WinLibs first on `PATH`. PS5Kitsune uses neither `BUILD_TESTING=ON` nor ccache. WinLibs r7 ships CMake 4.3.1 and Ninja 1.13.2, so the fallback to the official zips is not needed.

Each step is an `IStep`: on failure, the user resumes from the failed step without redoing the 20-minute build.

### Screens

1. **Home**: current state (nothing installed, or installed version + commit, active profile, new validated version available). Its action opens Mode. When the other mode is also installed it lists it ("Also installed: {mode} {version}"), and it reports an interrupted install ("The {mode} install did not finish.").
2. **Mode**: release or build, with date, commit, profile status, estimated duration and disk space of each option. It preselects the mode of an unfinished install, else the installed mode, else release. Build is disabled when the active profile has no build. The active install shows "In use"; a finished one that is not active has a "Use this install" button.
3. **PC check**: list of checks for the chosen mode (build mode needs `buildMinFreeDiskGiB` free), with status badge and action button.
4. **Tools** (build mode only): Python, MinGit and WinLibs, each installed or missing with its download size (the profile's `size`), the total to download and the disk space a build takes. Its button opens Install, whose first steps download what is missing.
5. **Install**: steps ticked as they complete, a progress bar (download bytes, Ninja's `[n/m]` counter, or indeterminate while a build step reports nothing), scrolling log, Cancel button. A failed build command shows its targeted message, with the exit code and last output lines under "Show details".
6. **Done**: relinker and libs paths, the commit for a build, "Open log" button, continue to V2.

Mode comes before the PC check because the check depends on the mode. The mode is passed from page to page, never stored in the session. Milestone 4 implemented Home, PC check, Mode, Install and Done for release mode; milestone 5b added build mode and the Tools screen.

## V2 — Add a game

V2 takes a game folder supplied by the user, in the DOOM for PS5 format, and turns it into a converted, audited folder registered in the library. It follows the manual procedure's sequence, adding checks and a backup of originals.

### Flow

1. **Pick the source folder.** If it already contains an `app0` subfolder, that one is used.
2. **Validate the structure** (nothing is copied before this step):
   - `eboot.bin` present;
   - `sce_sys/param.json` present and matching what AnyPS5 reads: a non-empty `titleId` and a non-empty `localizedParameters` object. The displayed name is the `titleName` of the `defaultLanguage` entry (inside `localizedParameters` or at the top level), else `en-US`, else the first language. Read with `System.Text.Json`;
   - module folders: `sce_module`, `sce_modules` or `prx`. `prx` can coexist with either of the other two, but `sce_module` and `sce_modules` together are a relinker error. None of the three → offer `--module-dir .` for homebrews that keep their libraries beside the executable;
   - total game size compared with free space at the destination.
3. **Show the summary**: `sce_sys/icon0.png` icon, name, title ID, size, module count, ELF or SELF state of each file.
4. **Create the game folder** in the library, `<library>\<titleId>\`, and **copy** the game into `app0` as-is. This is the default. "Move" is only offered when the source is on the same drive, after an explicit confirmation reminding that the source will no longer be at its original location.
5. **Copy `libs`** from the V1 AnyPS5 install (`.prx` + 3 DLLs). The folder is fully controlled by the app, so no Sony `.prx` can slip in (error 193).
6. **Check ELF or SELF** on `eboot.bin` and on every file of the module folders: the first 4 bytes are `7F 45 4C 46` for an ELF. An unwrapped SELF module is **skipped with a warning** by the relinker (not a failure), hence detecting it beforehand.
7. **Unwrap if needed** with `python tools\unwrap_self.py <input> <output>`, the repository's official script, replacing the video's `unself.py`. It refuses containers whose segments all look encrypted: the app then states that the game must come already unwrapped from the dump tool. Originals are first backed up to `<game>\.ps5kitsune\originals\`, **outside `app0`**: the relinker treats files whose name extends another module's (`libX.prx.bak`…) as backups and may skip them.
8. **Convert** from the game folder: `relinker --windows [--to-intel] --registry app0\eboot.bin <titleId>.exe`. `--to-intel` is added automatically based on the CPU detected in V1 and **recorded in `game.json`**, so that Reconvert reuses it. Exit codes: 0 success, 1 invalid arguments, 2 failure with the error on stderr; with `--to-intel`, an unconvertible AMD instruction fails the conversion (code 2), with a dedicated message. `Guest module:` and `Executable file:` lines are kept in the log.
9. **Audit**: `python scripts\tools\import_audit.py <*.registry.json> --libs libs --modules app0\sce_module --json audit.json`, passing **all** registries produced (`<titleId>.registry.json` and `<titleId>.<module>.registry.json`). The `*.needed.json` companions must stay beside them. The app reads the JSON rather than parsing text. Exit codes: 0 nothing blocks, 1 absent imports or missing libraries, 2 unreadable input.
10. **Register the game** in the library (see Data and configuration), with the profile and AnyPS5 version used and the audit summary.

> The executable name does not matter to AnyPS5: output files follow the output name (`<output>.registry.json`), and runtime files (`anyps5-input.ini`, `anyps5-fonts/`, `shader_cache/`) are looked up beside the exe, whatever its name. `<titleId>.exe` is therefore kept: it makes processes identifiable in Task Manager.

### Verdict after the audit

| Audit result | Verdict | Message |
| --- | --- | --- |
| `absent` = 0 and `stub` = 0 | Green | The game can load |
| `absent` = 0 and `stub` > 0 | Orange | The game loads but will stop if it calls an unimplemented function |
| `absent` > 0 or missing library | Red | Not playable yet: wait for an AnyPS5 update; the game stays in the library, marked "needs reconversion" |

### Screens

1. **Folder choice**: drag and drop or Browse button.
2. **Detected summary**: icon, name, ID, size, blocking issues in red.
3. **Options**: copy (default) or move, Intel conversion (pre-checked based on the CPU).
4. **Progress**: steps 4 to 9 with log.
5. **Result**: audit verdict, collapsible list of absent functions, "Add to library" button.

A **Reconvert** action reruns steps 5 to 9 on an existing game after an AnyPS5 update: new libs, new exe, new audit, without copying `app0` again. It **never touches** data the game created in its folder: `data\` (saves), `download0\`, `_trophy\`, `anyps5-input.ini`. `shader_cache\` is kept (AnyPS5 stores shaders per recompiler version and purges old ones itself).

## V3 — Launcher

V3 shows the games registered by V2 and launches them correctly, that is, from their folder, with a log and the game's settings translated into environment variables. After a crash, it translates known errors into a clear message with a fix button.

### Library

- Tile grid: `icon0.png`, name, title ID, audit verdict badge (green, orange, red).
- **Needs reconversion** badge when the installed profile is newer than the one that converted the game.
- Search by name, sort by last played.
- Per-game menu: Launch, Settings, Open folder, View last log, Reconvert, Clear shader cache (shows its size, which can reach several GB), Remove (from the library only, or also from disk after confirmation, flagging saves present in `data\`).

### Launching

- Working directory = game folder. The game looks up its files relative to it and creates `data\`, `download0\` and `_trophy\` there; this is the easiest mistake to make by hand.
- Stdout and stderr are **written directly to** `<game>\.ps5kitsune\logs\<date-time>.log`: the launcher opens the file and the game inherits its handle. Nothing goes through pipes: the game is neither slowed by very verbose output nor crashed if the launcher closes. The UI follows the log by reading the file (*tail*). The last 10 logs are kept.
- No console window (`CreateNoWindow`), Stop button during play, exit code shown at the end.
- Closing the launcher during a game **does not kill the game** (no kill-on-close Job Object); on reopening, the launcher finds the running process by its recorded PID.
- First-launch message: it may stutter while shaders are prepared; the second run is smooth (shader cache and pipeline journal).

### Per-game settings

| UI setting | Translated to | Values |
| --- | --- | --- |
| GPU | `ANYPS5_GPU` (part of the name, case-insensitive) | List of GPU names detected by the probe |
| CPU rendering (Lavapipe) | `VK_DRIVER_FILES` = Mesa's `lvp_icd.x86_64.json` manifest + `ANYPS5_GPU=llvmpipe` | Offered by default if `shaderInt64` is missing |
| Game language | `ANYPS5_LANGUAGE` | Default 1 (English, US); list of the 31 languages from the docs (0 to 30) |
| Vertical sync | `ANYPS5_PRESENT_MODE` | `fifo` (default), `fifo_latest_ready`, `mailbox`, `immediate` |
| Disable Dozen | `VK_LOADER_DRIVERS_DISABLE='*dzn*'` | Only if another Vulkan driver exists |
| Performance overlay | `ANYPS5_OVERLAY=1` (+ `ANYPS5_OVERLAY_POS`) | Otherwise F10 in game |
| Advanced: PlayGo chunks | `APS5_PLAYGO_UNLISTED_CHUNKS` | `installed`, `absent` or empty |
| Keyboard and mouse bindings | `anyps5-input.ini` file beside the game exe | Simple action → key editor (`Action = KEY:Name` format, actions listed in `INPUT_MAPPING.md`) |

These variables only apply to the game process, never to the system. An invalid setting stops the game with an error naming the variable: the UI therefore only offers valid values.

**Fonts (global option):** AnyPS5 looks for `anyps5-fonts\` beside each game's exe, or in the folder named by `ANYPS5_SYSTEM_FONTS`. The app downloads the Noto fonts (open license) once into `<root>\fonts\` and passes `ANYPS5_SYSTEM_FONTS` at every launch: no per-game copy.

### Crash diagnosis

The launcher searches the log and error output for known patterns, then shows an explanation and an action. Patterns come from the profile.

| Pattern in the log | Explanation | Suggested action |
| --- | --- | --- |
| Error 193 | A file that is not a Windows DLL is in `libs` | Repair `libs` (copy again from the install) |
| Error 126 | One of the 3 DLLs is missing | Repair `libs` |
| `replace guest placeholder with resident direct memory`, `commit direct memory pages`, `keep unmapped direct memory`, `create direct memory backing` | Insufficient commit limit | Open page file settings, suggest closing the browser |
| `buffer device address and shaderInt64 are required` | Incompatible graphics driver | Enable Lavapipe for this game (downloads Mesa if missing) |
| `0xc0000005` with `nvgpucomp64.dll` | Known NVIDIA shader compiler bug (driver 610.47) | Update the NVIDIA driver |
| Missing CPU extensions at startup | CPU too old | None; clear message |
| `param.json` error | Missing or invalid metadata | Revalidate the folder (V2 step 2) |
| `ANYPS5_*` variable named in the error | Invalid setting | Reset that setting |
| Unimplemented function name | AnyPS5 does not handle this function yet | **Report** button: prepares a text with the PC config, game ID, profile and log path, then opens the repository's [Game bring-up](https://github.com/boykopovar/AnyPS5/discussions/categories/game-bring-up) Discussions category (one thread per game, as the README asks); nothing is sent automatically |

## Linux compatibility

Linux is a good second OS: AnyPS5 is at home there. The relinker produces a Linux ELF by default and releases already publish `prx-linux-<version>.tar.gz`. The work is mostly about installing prerequisites and a few different checks.

### What changes

| Topic | Windows | Linux |
| --- | --- | --- |
| Tool installation | Portable archives in `tools\` | Distribution packages (`apt`, `dnf`), read from `/etc/os-release`; commands shown for copying or run via `pkexec` |
| Compiler | WinLibs GCC 15.2.0 r7, mandatory | System GCC and G++ |
| Build dependencies | None extra | X11, Xext, XRandR, Wayland, xkbcommon, EGL, libdecor, PipeWire, PulseAudio, ALSA headers (list in `BUILD.md`); on Fedora `glibc-static` and `libstdc++-static` |
| FFmpeg SSL error | Worked around with `SSL_CERT_FILE` | Not applicable |
| Libs | `.prx` + 3 DLLs | `.prx` only |
| Conversion | `--windows`, `.exe` output | No option, `.elf` output; restore the execute bit after a copy, on the exe and every listed `Executable file:` |
| Python | Portable embeddable Python | System `python3` |
| Lavapipe | Downloaded Mesa | System Mesa package; manifest in `/usr/share/vulkan/icd.d/` |
| Memory | Commit limit check | No equivalent check; instead, read-write access to `/dev/udmabuf` |
| `/dev/udmabuf` | Not applicable | If denied: suggest `sudo usermod -aG kvm $USER` then logging in again |
| Display | Not applicable | `SDL_VIDEODRIVER` setting (Wayland or X11) in game settings |
| Desktop shortcut | `.lnk` (optional) | The relinker already writes a `.desktop` file and an icon when `icon0.png` exists |

### Rules from V1 onwards

- No hard-coded paths with `\`: always `Path.Combine` and folders provided by `IPlatform`.
- No direct call to a tool or Windows API outside `Platform.Windows`. Windows code (P/Invoke, registry) stays in that project.
- CPU and Vulkan checks live in `Core`: .NET intrinsics and Silk.NET work on both OSes.
- CI builds `Core` and the CLI for `win-x64` **and** `linux-x64` from the start: a Windows dependency slipping into `Core` breaks the build immediately. The `Platform.Linux` project is only created at the Linux milestone.

## Data and configuration

Everything is in readable JSON files, no database. Each game carries its own record in its folder: if the library index is lost, a **Rescan** button rebuilds it.

### Locations

| Content | Windows | Linux |
| --- | --- | --- |
| App settings | `%LOCALAPPDATA%\PS5Kitsune\settings.json` | `~/.config/ps5kitsune/settings.json` |
| Data root (tools, AnyPS5, games) | `C:\PS5Kitsune\` by default, configurable, no spaces | `~/.local/share/ps5kitsune/` |

The default root is not under `%LOCALAPPDATA%`, because the Windows user name may contain a space, which breaks WinLibs and CMake.

### Root layout

```text
C:\PS5Kitsune\
├── tools\
│   ├── python\                  embeddable Python (both modes)
│   ├── mingit\                  portable Git (build mode)
│   ├── winlibs\                 GCC 15.2.0 r7 + CMake + Ninja (build mode)
│   └── mesa\                    Lavapipe, downloaded on demand
├── profiles\
│   ├── downloaded.json          last fetched profile
│   └── override.json            local override (optional)
├── fonts\                       Noto fonts, passed via ANYPS5_SYSTEM_FONTS
├── anyps5\
│   ├── source\                  git clone, fetched by SHA (build mode)
│   ├── build\                   CMake build folder (build mode)
│   ├── state\                   one marker per build step, holding the commit (build mode); active (release or build); requested-mode while an install is unfinished
│   └── installs\
│       ├── release\             the release install
│       └── build\               the build install, copied from build\
│           ├── relinker.exe
│           ├── libs\            .prx + 3 DLLs, ready to copy
│           ├── scripts\         tools\ and core\libs\prx of the same commit, tree layout kept
│           └── install.json     profile, mode, version, commit, date
├── games\
│   └── PPSA00000\               one folder per game (title ID)
│       ├── app0\
│       ├── libs\
│       ├── PPSA00000.exe
│       ├── *.registry.json + *.needed.json
│       ├── audit.json
│       ├── anyps5-input.ini     created by the binding editor
│       ├── shader_cache\        created by the game
│       ├── data\  download0\  _trophy\   created by the game, never touched by the app
│       └── .ps5kitsune\
│           ├── game.json        game record + settings
│           ├── originals\       files before unwrapping
│           └── logs\            last 10 launches
├── library.json                 library index
└── logs\                        installer logs
```

Both installs stay side by side: `state\active` names the one games are converted with, and an install becomes active only once every step succeeded. `ps5kitsune use release|build` and Mode's "Use this install" switch with no step. A root from before 5c has its `anyps5\current` moved into `installs\<mode>` the first time the CLI or the app opens it.

The data root itself is chosen on the app's home screen and saved in `%LOCALAPPDATA%\PS5Kitsune\settings.json` (`{ "schemaVersion": 1, "dataRoot": "…" }`), outside the root. The CLI reads the same file: `--root`, then the saved root, then `C:\PS5Kitsune`. When the user changes the root on Home, the app replaces the session first and saves the setting second: a refused change leaves `settings.json` untouched, and a failed save keeps the new root for this run and shows "Cannot save the setting".

### Install record (`install.json`)

`install.json` sits in each install folder (`anyps5\installs\release`, `anyps5\installs\build`) and records what is installed there.

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

- `mode`: `release` or `build`.
- `version`: the tag in release mode, the first 7 characters of the commit in build mode.
- `commit`: null in release mode.
- `unwrapScript`: the profile's URL in release mode, null in build mode, where the script comes from the clone.
- The reader accepts schemas 1 and 2, and counts a schema 1 record as installed. The installer writes schema 2.

### Game record (`game.json`)

```json
{
  "schemaVersion": 1,
  "titleId": "PPSA00000",
  "name": "DOOM",
  "executable": "PPSA00000.exe",
  "icon": "app0/sce_sys/icon0.png",
  "convertedWith": { "profile": "doom-validated-2026-10", "mode": "release", "version": "v0.1.2", "commit": null, "toIntel": true },
  "audit": { "implemented": 262, "stub": 0, "absent": 0, "verdict": "green" },
  "settings": { "gpu": null, "lavapipe": false, "language": 1, "presentMode": "fifo" },
  "addedAt": "2026-10-18T15:00:00+02:00",
  "lastPlayedAt": null
}
```

`convertedWith.mode` is the install mode that converted the game, `release` or `build`; `commit` is set for a build.

`PPSA00000` is an example; the real ID comes from the game's `param.json`. Every JSON file of the app has a `schemaVersion` field, to migrate records cleanly when the format changes.

## Risks and open points

The main risk is how fast AnyPS5 evolves: the repository merges several pull requests a day, so paths, options and messages can change under the installer. The answer is the pinned, validated profile (see "AnyPS5 profiles").

### Risks

| Risk | Effect | Mitigation |
| --- | --- | --- |
| AnyPS5 options, paths or messages change | Broken steps or diagnostics | Pinned profile by default; commands and patterns in the profile, updated without recompiling the app |
| User picks the "latest" profile | Untested behavior | Flagged in the UI and in `game.json`; one-click return to the validated profile |
| Antivirus quarantines generated executables | Conversion or launch fails with no clear reason | Detect the missing file, explain the exclusion to add. Bitdefender Advanced Threat Defense is a known case (see `BUILD.md`) |
| SmartScreen on the unsigned app | Distrust on first launch | Document; consider signing for wide distribution |
| Only one free test game (DOOM) | Edge cases not covered | Fake folders in unit tests; user feedback later; the community [blocker map](https://alxcbtw.github.io/anyps5-roadmap/) as a reference |
| Development from a non-Windows environment | UI and P/Invoke code untested | Develop and test on a Windows machine; GitHub Actions CI on `windows-latest` |

### Verified in the AnyPS5 repository (commit `48d61f5`, 2026-10-18)

| Point | Finding |
| --- | --- |
| Latest release | `v0.1.2` of 2026-10-14; Windows libs weigh ~82 MB |
| Release asset names | `relinker-<tag>.exe`, `prx-windows-<tag>.zip` (content under `libs/`), plus `USAGE.md` and `INPUT_MAPPING.md` |
| Release asset hashes | `digest` field (`sha256:…`) present in the GitHub API |
| Relinker path after a full build | `build\core\relinker\relinker.exe` (`tools/package_release.py`) |
| `unwrap_self.py` | `unwrap_self.py <input.self> <output.elf> [--verbose]`; refuses encrypted containers |
| `import_audit.py --json` | Exists; exit codes 0 / 1 / 2; commit stamp `null` without git, no error |
| Python for the build | Only for tests (`find_package(Python3 QUIET)`): unnecessary with `BUILD_TESTING=OFF` |
| Generated exe name | No effect on AnyPS5; runtime files are looked up beside the exe |
| `param.json` | `titleName` is in `localizedParameters.<language>`, not at the top level |
| `anyps5-input.ini` | Beside the game exe, or the path given by `ANYPS5_INPUT_CONFIG` |
| Fonts | `anyps5-fonts\` beside the exe, or `ANYPS5_SYSTEM_FONTS` |
| Still to confirm | Mesa 7z format (at the Lavapipe milestone) |

### Legal framework

- The app does no dumping and no decryption. It only processes folders supplied by the user, and `unwrap_self.py` itself refuses encrypted containers.
- AnyPS5 is licensed **GPL v2 only**. By downloading the relinker and libs from official sources instead of bundling them, the installer redistributes nothing.
- PS5Kitsune is itself GPL v2 and its code is public: anyone redistributing the app, modified or not, must provide its source under the same license.
- **Option now possible:** publish builds of the profile's pinned commit from PS5Kitsune's public CI, sparing users the build. This is redistribution of GPLv2 binaries: each build must come with the corresponding source (link to the exact commit, submodules included, and build scripts), with the license text. Since the CI is public, the build script itself is published. To be decided after milestone 2 (see Open decisions).
- Repeat the project's disclaimer in the app: users are responsible for the provenance of the files they convert.

### Open decisions

- [x] App name: **PS5Kitsune**
- [x] License: **GPL v2 only**, public GitHub repository
- [x] Project language: **English**
- [ ] Publish AnyPS5 builds from PS5Kitsune's CI, or let users choose between the official release and a local build
- [x] Default mode of the first profile: **release `v0.1.2`**, which runs DOOM for PS5 (validated 2026-10-20 with milestone 2's CLI; audit orange: 361 implemented, 3 stubs, 0 absent)
- [x] Build mode disk space threshold: **`buildMinFreeDiskGiB` = 10 GiB**, from the build measured on 2026-10-21 (root of 3.2 GB)
- [ ] Check for new profiles at startup, or only on demand

Decided at review: portable tools without winget, pinned profile by default, copy (not move) the game by default, isolated Vulkan probe, game launched detached from the launcher.

## Roadmap

The order follows "end to end first, screens second": each milestone ends with a criterion verifiable on a Windows machine, with DOOM for PS5 as the reference game.

1. **Foundation.** Public `PS5Kitsune` GitHub repository with `LICENSE` (GPL v2) and a README pointing to the official AnyPS5 repository and repeating its disclaimer; solution, `Core` / `Platform.Windows` / `App` / `Cli` / test projects, dependency injection, `IProcessRunner`, `IDownloader` with SHA-256, logging, profile format, `win-x64` + `linux-x64` CI (Core and CLI) with a license check of shipped NuGet packages.
   - Criterion: the CLI downloads embeddable Python, verifies its hash and logs `python --version`.
2. **End-to-end CLI path.** Profile release mode, DOOM folder preparation, ELF/SELF check and `unwrap_self.py`, relinker, JSON audit, `game.json`, launch with the log written to a file. No screens.
   - Criterion: `ps5kitsune install`, then `ps5kitsune add <DOOM folder>`, then `ps5kitsune run PPSA…` runs DOOM. This milestone validates the first profile and tells whether release `v0.1.2` is enough or a build is needed.
3. **V1a — PC checks.** Isolated Vulkan probe, CPU, commit limit, disk, path; check screen.
   - Criterion: `ps5kitsune check` and the check screen agree on the AMD reference laptop, with no blocking result; on an Intel + NVIDIA PC the probe sees both GPUs and names the usable one. A GPU without `shaderInt64` (the Iris Xe case) is covered by tests from probe fixtures.
4. **V1b — Install screens, release mode.** Home, mode choice, progress, profile status.
   - Criterion: install from the UI on an empty root, identical to the CLI result.
5. **V1c — Build mode**, in two parts.
   - **5a — Build mode, Core and CLI.** MinGit, WinLibs, fetch at the profile's commit, configure with `SSL_CERT_FILE`, build, `libs`, resume via `IsAlreadyDone()`, `install --mode`, `override.json`. Spec: [milestone 5a](superpowers/specs/2026-10-21-milestone-5a-build-mode-design.md).
     - Criterion: `ps5kitsune install --mode build` finishes with no intervention on a clean PC; an interruption mid-build resumes without redoing everything; then an incremental update to another commit.
   - **5b — Build mode UI.** The Build card on Mode, the Tools screen, and the build progress on Install. Spec: [milestone 5b](superpowers/specs/2026-10-22-milestone-5b-build-ui-design.md).
   - **5c — Side-by-side installs.** Release and build each get their own folder (`anyps5\installs\release`, `anyps5\installs\build`; the build folder name was taken by CMake) plus the choice of the active one, so switching modes never removes a working install and switching back is instant. `AnyPS5Paths`, the done-checks, `install.json`, Home and Mode become aware of both installs, and a converted game records which install it was audited against. Spec: [milestone 5c](superpowers/specs/2026-10-22-milestone-5c-side-by-side-installs-design.md).
     - Criterion: with a release installed, a build cancelled or failed mid-compile leaves the release active and usable; switching between two finished installs takes seconds and runs no step.
6. **V2 — Add-a-game screens.** On top of milestone 2's logic: summary, options, progress, verdict, Reconvert.
   - Criterion: DOOM added from the UI, green audit; Reconvert keeps `data\` and `_trophy\`.
7. **V3 — Launcher.** Library, detached launch with log, per-game settings, Lavapipe (Mesa 7z), fonts, error diagnosis.
   - Criterion: DOOM launched in one click with Lavapipe on the Iris Xe; the launcher closed and reopened finds the running game; a deliberately provoked error 126 is recognized and repaired.
   - **7b — Visual design.** Once every main screen exists (install, add a game, library and launch): a coherent theme in light and dark, typography and spacing, shared card components (Mode, Tools), the library's look with game icons, and accessibility (selected states exposed to screen readers, keyboard navigation).
     - Criterion: every screen follows the same design in both themes, and each one can be used with the keyboard alone.
8. **Linux.** `Platform.Linux`: per-distribution packages, `/dev/udmabuf`, XDG paths; CLI first, then UI.
   - Criterion: DOOM converted and run on native Linux or in a VM with a GPU.

From milestone 2 the tool is usable from the command line; milestone 4 gives a first graphical installer with no C++ build on the user's side.
