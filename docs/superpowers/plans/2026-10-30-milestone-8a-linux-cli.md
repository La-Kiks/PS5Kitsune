# Milestone 8a — Linux CLI (release mode) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `ps5kitsune` runs on Linux in release mode (`check`, `install`, `add`, `list`, `run`, `settings`, `diagnose`, `use release`) and takes DOOM for PS5 from a folder to a running game on Ubuntu, while Windows keeps working unchanged.

**Architecture:** Core keeps one pipeline for both OSes. The profile becomes one embedded file per OS (schema 2, `os` field, `relinker.args`). What differs at run time comes from `IPlatform` (new members) and from a new `PS5Kitsune.Platform.Linux` project (XDG folders, `/etc/os-release` package commands, system `python3`, system Lavapipe, a `posix_spawn` game launcher, the Python and udmabuf checks). A new `PS5Kitsune.Platform` project picks the Windows or Linux platform at startup for the CLI and the app.

**Tech Stack:** .NET 10, C#, `LibraryImport` P/Invoke into `libc` (posix_spawn, waitpid, kill, sysconf), xUnit v3 on Microsoft.Testing.Platform, WSL2 Ubuntu 24.04 for the Linux runs. No new NuGet package.

**Spec:** `docs/superpowers/specs/2026-10-30-milestone-8a-linux-cli-design.md` (revision of this plan's first commit: see Task 0).

## Global Constraints

- Code, docs, commit messages and CLI text in English.
- `TreatWarningsAsErrors`, `AnalysisLevel latest-recommended`, `EnforceCodeStyleInBuild`: 0 warnings. `[LoggerMessage]` for logging (CA1848); `string.Create(CultureInfo.InvariantCulture, $"…")` when formatting numbers (CA1305); compute log arguments into locals before the call (CA1873); Linux-only APIs in a class marked `[SupportedOSPlatform("linux")]` or behind `OperatingSystem.IsLinux()`/`!OperatingSystem.IsWindows()` (CA1416); P/Invoke with `[LibraryImport]`, never `[DllImport]` (SYSLIB1054).
- No new NuGet package. Core keeps no OS API call: the only OS tests allowed in Core are `OperatingSystem.IsWindows()` guards around `File.GetUnixFileMode`/`File.SetUnixFileMode` (precedent: `PreparationLock`).
- Profile schema 2, verbatim: `"schemaVersion": 2`, `"os": "windows" | "linux"`, `"relinker": { "args": [...], "intelArg": "--to-intel" }`. Windows args `["--windows", "--registry"]`; Linux args `["--registry"]`.
- Linux release assets, verbatim: relinker `relinker-v0.1.3`, SHA-256 `bd2a959885830e14f091477edf297fc4c3f45b83a29171c47f5719d721fedb45`; libs `prx-linux-v0.1.3.zip`, SHA-256 `017bdb0e931fa0cdb17f0cea805a824180b88205f5c50f4473550c63a4e811f3` (352 entries under `libs/`).
- Linux folders: settings `$XDG_CONFIG_HOME/ps5kitsune`, else `~/.config/ps5kitsune`; data `$XDG_DATA_HOME/ps5kitsune`, else `~/.local/share/ps5kitsune` (a variable that is unset, empty or not an absolute path is ignored, per the XDG spec).
- Executable names: tools keep `IPlatform.ExecutableSuffix` (`.exe` / empty); a converted game uses `IPlatform.GameExecutableSuffix` (`.exe` / `.elf`).
- Package commands, verbatim (Task 4): Debian family `sudo apt install <packages>` with `python3`, `libvulkan1`, `mesa-vulkan-drivers`; Fedora `sudo dnf install <packages>` with `python3`, `vulkan-loader`, `mesa-vulkan-drivers`; Arch `sudo pacman -S <packages>` with `python`, `vulkan-icd-loader`, `vulkan-swrast`; unknown `Install: <Debian package names, comma separated>`.
- Texts, verbatim:
  - Build mode on Linux: "Build mode is not available on Linux yet; use release mode." (Task 3)
  - Missing Python: "Python 3 is required: {command}" (Task 4)
  - Missing Lavapipe: "Lavapipe is not installed: {command}" (Task 4)
  - Vulkan fix on Linux: "Install Vulkan: {command}. With an NVIDIA GPU, install NVIDIA's driver instead of Mesa." (Task 4)
  - Commit limit where the OS has none: "Not applicable on this system." (Task 2)
  - Checks (Task 7): `python` "Python 3" — OK "{path}." / Blocking "python3 was not found." action "{command}"; `udmabuf` "/dev/udmabuf" — OK "Read-write access." / Warning "No read-write access to /dev/udmabuf; games copy shared memory instead of importing it, and some may fail." action "sudo usermod -aG kvm $USER, then log out and in again." / Warning "This kernel does not provide /dev/udmabuf; games copy shared memory instead of importing it, and some may fail." no action / Info "Not needed: every GPU uses NVIDIA's proprietary driver."
  - Diagnosis ids and texts (Task 8): see Task 8's table.
- Linux-only tests start with `Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");`. CA1416 does not treat that call as a guard: a test method that calls a Linux-only API (`File.GetUnixFileMode`/`SetUnixFileMode`, `LinuxGameLauncher`, `ProcStat.StartTimeOf`) carries `[SupportedOSPlatform("linux")]` (`using System.Runtime.Versioning;`), and so do the classes `LinuxGameLauncherTests` and `LinuxServicesTests` — as the Windows test classes carry `[SupportedOSPlatform("windows")]`. The attribute does not skip anything at run time. Windows-only tests keep their existing skip. Tests that build Linux paths in strings must not depend on Windows `Path` parsing them.
- **Build and test on both OSes** after every task:
  - Windows (Bash tool, repo root): `dotnet build PS5Kitsune.slnx -c Release` then `dotnet test --solution PS5Kitsune.slnx -c Release --no-build`. The test output is in French ("échec" = failed, "réussi" = passed, "ignoré" = skipped).
  - Linux (WSL; copies the working tree, uncommitted changes included, into `~/src/PS5Kitsune` and builds there):
    `wsl -d Ubuntu-24.04 -e bash -c '~/ps5k-sync.sh && . ~/ps5k-env.sh && dotnet build PS5Kitsune.slnx -c Release 2>&1 | tail -3 && dotnet test --solution PS5Kitsune.slnx -c Release --no-build 2>&1 | tail -12' 2>&1 | tr -d '\0'`
  - One class: `dotnet test --project <test project> -c Release -- --filter-class "<full class name>"` (in WSL, after `~/ps5k-sync.sh && . ~/ps5k-env.sh &&`). A filtered run exits with code 8 when it selects nothing: check the summary.
  - Always `wsl -d Ubuntu-24.04 -e …` (never `wsl -- …`, which re-expands `$VARS` on the Windows side).
- Close any running `PS5Kitsune.exe` before building the App on Windows (it locks `bin\Release`).
- Line endings: keep each edited file's existing endings (most C# files are CRLF; never mix LF lines into a CRLF file); new files may be LF. `sed -i` on a CRLF file keeps the `\r`.
- Commit locally after each task, author La-Kiks, message ending with `Co-Authored-By: <your model name> <noreply@anthropic.com>`. Do not push. Never add or run an `install.cmd`.

## Refinements of the spec

1. **SDL** (corrected in the spec on 2026-10-30): SDL2 is linked into `libSceAgcDriver.prx` and reads `SDL_VIDEODRIVER` (default `wayland,x11`). No display setting in 8a; the architecture keeps the row as "if a game needs it".
2. **A reattached game's exit code** cannot be read on Linux (it is not our child). `WaitForExitAsync` of an attached Linux game returns `-1`; the CLI never attaches (it only launches), so this only matters to the app, in 8b. Recorded in `TODO.md` (Task 9).
3. **Execute bits:** the relinker writes extra executables (`--executable`) beside the main one with the main one's extension, and PS5Kitsune never passes `--executable`. So before a launch, `ExecutableBits.EnsureGame` gives the execute bit to the game executable and to every file in the game folder with the same extension (Task 3). No parsing of the relinker's `Executable file:` lines.
4. **Build mode guard:** the Linux profile keeps the shared `build` section, and Core already registers `BuildSource` only when the `mingit` and `winlibs` tools exist, which the Linux profile lacks. The guard is therefore only a message: `AnyPS5Installer` gives "Build mode is not available on Linux yet; use release mode." when the profile's `os` is `linux`, from `CreateSteps` and from `Use` (Task 3).
5. **Python step name:** `IPythonRuntime.StepName` is "Install Python" for the embeddable zip and "Find Python 3" for the system one, so the Linux install does not claim to install anything.
6. **Reconvert** needs no change: `GameReconverter` checks `running.json` (with `Attach`) before `SetAsideStep`; on Linux the exclusive open in `SetAsideStep` is an advisory lock that never fails for a running ELF, which is harmless.
7. **Platform checks** are plain `SystemCheck`s the platform registers as `ISystemCheck`; `SystemChecker` runs Core's (`SystemChecks.All`) first, then them. They may read the system in `Evaluate` (a file test, a `PATH` search): both are instant, unlike the Vulkan probe that `SystemFacts` collects once.
8. **`libc`** in `[LibraryImport("libc")]` resolves to glibc's `libc.so.6` without the `libc6-dev` package (checked in WSL with a throwaway program).

## File map

| File | Task | Responsibility |
| --- | --- | --- |
| `src/PS5Kitsune.Core/Profiles/default-profile.windows.json` (renamed from `default-profile.json`) | 1 | The Windows profile, schema 2 |
| `src/PS5Kitsune.Core/Profiles/default-profile.linux.json` (new) | 1, 8 | The Linux profile |
| `src/PS5Kitsune.Core/Profiles/ProfileOs.cs` (new) | 1 | `windows` / `linux` constants |
| `src/PS5Kitsune.Core/Profiles/AnyPS5Profile.cs`, `ProfileLoader.cs`, `PS5Kitsune.Core.csproj` | 1 | `Os`, `Relinker.Args`, per-OS loading |
| `src/PS5Kitsune.Core/Games/Steps/RelinkStep.cs` | 1 | Uses `Relinker.Args` |
| `src/PS5Kitsune.Core/Abstractions/IPlatform.cs`, `Checks/*` , `Games/GameLayout.cs` | 2 | New platform members and the texts that use them |
| `src/PS5Kitsune.Core/Tools/IPythonRuntime.cs`, `PythonScripts.cs`, `PythonMissingException.cs`, `AnyPS5/Steps/PreparePythonStep.cs` (new) | 3 | Python behind an interface |
| `src/PS5Kitsune.Core/IO/ExecutableBits.cs` (new) | 3 | Execute bits on Linux |
| `src/PS5Kitsune.Platform.Linux/*` (new project) | 4, 6, 7 | Linux platform |
| `tests/PS5Kitsune.Platform.Linux.Tests/*` (new project) | 4, 6, 7 | Linux tests |
| `src/PS5Kitsune.Platform/*` (new project) | 5 | Picks the platform; registers its services |
| `src/PS5Kitsune.Core/Checks/SystemChecker.cs`, `src/PS5Kitsune.Platform.Linux/PythonCheck.cs`, `UdmabufCheck.cs` | 7 | Core's checks first, then the Linux ones |
| `src/PS5Kitsune.Core/Diagnostics/DiagnosisIds.cs`, `DiagnosisTexts.cs` | 8 | Linux diagnoses |
| `.github/workflows/ci.yml`, `docs/architecture.md`, `README.md`, `TODO.md` | 9 | CI publish, docs |

---

### Task 0: Commit the corrected spec

The spec was corrected after its first commit (SDL, confirmed facts, hashes). Commit it with this plan.

- [x] **Step 1: Commit** (done: 6dc1b44)

```bash
git add docs/superpowers/specs/2026-10-30-milestone-8a-linux-cli-design.md docs/superpowers/plans/2026-10-30-milestone-8a-linux-cli.md
git commit -m "Correct the 8a spec (SDL, confirmed facts) and add its plan"
```

(With the attribution line of the Global Constraints.)

---

### Task 1: Core — one profile per OS (schema 2)

**Files:**
- Rename: `src/PS5Kitsune.Core/Profiles/default-profile.json` → `src/PS5Kitsune.Core/Profiles/default-profile.windows.json`
- Create: `src/PS5Kitsune.Core/Profiles/default-profile.linux.json` (generated, Step 6)
- Create: `src/PS5Kitsune.Core/Profiles/ProfileOs.cs`
- Modify: `src/PS5Kitsune.Core/Profiles/AnyPS5Profile.cs`, `src/PS5Kitsune.Core/Profiles/ProfileLoader.cs`, `src/PS5Kitsune.Core/PS5Kitsune.Core.csproj`, `src/PS5Kitsune.Core/Games/Steps/RelinkStep.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Profiles/ProfileLoaderTests.cs`, `tests/PS5Kitsune.Core.Tests/Profiles/ActiveProfileTests.cs`, `tests/PS5Kitsune.Core.Tests/AnyPS5/AnyPS5TestSetup.cs`

**Interfaces:**
- Produces: `ProfileOs.Windows` (`"windows"`), `ProfileOs.Linux` (`"linux"`), `ProfileOs.All`; `AnyPS5Profile.Os` (string, `""` by default); `RelinkerOptions.Args` (replaces `WindowsArgs`); `ProfileLoader.LoadEmbedded(string os = ProfileOs.Windows)`; `ProfileLoader.LoadActive(DataLayout layout, string os = ProfileOs.Windows)`.

- [ ] **Step 1: Move the profile tests to schema 2**

Every profile JSON in the two profile test files uses the same opening. Run:

```bash
sed -i 's/"schemaVersion": 1, "id"/"schemaVersion": 2, "os": "windows", "id"/' tests/PS5Kitsune.Core.Tests/Profiles/ProfileLoaderTests.cs tests/PS5Kitsune.Core.Tests/Profiles/ActiveProfileTests.cs
grep -c '"schemaVersion": 1' tests/PS5Kitsune.Core.Tests/Profiles/ProfileLoaderTests.cs tests/PS5Kitsune.Core.Tests/Profiles/ActiveProfileTests.cs
```

Expected: both counts `0`.

In `ProfileLoaderTests.cs`, `Rejects_an_unknown_schema_version` now uses the current version; change it to 3:

```csharp
    [Fact]
    public void Rejects_an_unknown_schema_version()
    {
        var exception = Assert.Throws<InvalidProfileException>(() => Load("""
            { "schemaVersion": 3, "os": "windows", "id": "future", "source": { "kind": "release", "tag": "v9" } }
            """));

        Assert.Contains("schema version 3", exception.Message, StringComparison.Ordinal);
    }
```

In `Embedded_profile_loads_with_a_pinned_python`, replace `Assert.Contains("--windows", profile.Relinker.WindowsArgs);` with:

```csharp
        Assert.Equal(ProfileOs.Windows, profile.Os);
        Assert.Equal(["--windows", "--registry"], profile.Relinker.Args);
```

In `tests/PS5Kitsune.Core.Tests/AnyPS5/AnyPS5TestSetup.cs`, replace `Relinker = new RelinkerOptions { WindowsArgs = ["--windows", "--registry"], IntelArg = "--to-intel" },` with:

```csharp
            Os = ProfileOs.Windows,
            Relinker = new RelinkerOptions { Args = ["--windows", "--registry"], IntelArg = "--to-intel" },
```

- [ ] **Step 2: Write the failing tests for the new behaviour**

Append to `ProfileLoaderTests` (inside the class, before its private helpers):

```csharp
    [Fact]
    public void The_linux_profile_downloads_the_linux_release_and_no_tools()
    {
        var profile = ProfileLoader.LoadEmbedded(ProfileOs.Linux);

        Assert.Equal(ProfileOs.Linux, profile.Os);
        Assert.False(profile.Validated);
        Assert.Equal("relinker-v0.1.3", profile.Asset(ProfileAssetKeys.Relinker).Name);
        Assert.Equal("bd2a959885830e14f091477edf297fc4c3f45b83a29171c47f5719d721fedb45", profile.Asset(ProfileAssetKeys.Relinker).Sha256);
        Assert.Equal("prx-linux-v0.1.3.zip", profile.Asset(ProfileAssetKeys.Libs).Name);
        Assert.Equal("017bdb0e931fa0cdb17f0cea805a824180b88205f5c50f4473550c63a4e811f3", profile.Asset(ProfileAssetKeys.Libs).Sha256);
        Assert.Empty(profile.Tools);
        Assert.Equal(["--registry"], profile.Relinker.Args);
        Assert.Equal("--to-intel", profile.Relinker.IntelArg);
    }

    [Fact]
    public void Both_profiles_share_the_release_the_scripts_the_fonts_and_the_thresholds()
    {
        var windows = ProfileLoader.LoadEmbedded(ProfileOs.Windows);
        var linux = ProfileLoader.LoadEmbedded(ProfileOs.Linux);

        Assert.Equal(windows.Id, linux.Id);
        Assert.Equal(Json(windows.Source), Json(linux.Source));
        Assert.Equal(Json(windows.Build), Json(linux.Build));
        Assert.Equal(Json(windows.Asset(ProfileAssetKeys.Scripts)), Json(linux.Asset(ProfileAssetKeys.Scripts)));
        Assert.Equal(Json(windows.Asset(ProfileAssetKeys.UnwrapScript)), Json(linux.Asset(ProfileAssetKeys.UnwrapScript)));
        Assert.Equal(Json(windows.Fonts), Json(linux.Fonts));
        Assert.Equal(Json(windows.Checks), Json(linux.Checks));
        Assert.Equal(windows.Diagnostics.ReportUrl, linux.Diagnostics.ReportUrl);
    }

    [Fact]
    public void Rejects_an_unknown_os()
    {
        var exception = Assert.Throws<InvalidProfileException>(() => Load("""
            { "schemaVersion": 2, "os": "macos", "id": "mac", "source": { "kind": "release", "tag": "v9" } }
            """));

        Assert.Contains("\"macos\"", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_a_profile_without_an_os()
    {
        Assert.Throws<InvalidProfileException>(() => Load("""
            { "schemaVersion": 2, "id": "none", "source": { "kind": "release", "tag": "v9" } }
            """));
    }

    private static string Json<T>(T value) => System.Text.Json.JsonSerializer.Serialize(value);
```

Append to `ActiveProfileTests`:

```csharp
    [Fact]
    public void Each_os_gets_its_own_embedded_profile()
    {
        Assert.Equal(ProfileOs.Windows, ProfileLoader.LoadActive(Layout, ProfileOs.Windows).Profile.Os);
        Assert.Equal(ProfileOs.Linux, ProfileLoader.LoadActive(Layout, ProfileOs.Linux).Profile.Os);
    }

    [Fact]
    public void An_override_for_the_other_os_is_ignored_with_a_problem()
    {
        TestFiles.Write(OverridePath, """{ "schemaVersion": 2, "os": "windows", "id": "windows-override", "source": { "kind": "release", "tag": "v9" } }""");

        var active = ProfileLoader.LoadActive(Layout, ProfileOs.Linux);

        Assert.Equal(ProfileOrigin.Embedded, active.Origin);
        Assert.Equal(ProfileOs.Linux, active.Profile.Os);
        Assert.Contains("is for windows, not linux", active.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void A_schema_1_override_is_ignored_with_a_problem()
    {
        TestFiles.Write(OverridePath, """{ "schemaVersion": 1, "id": "old", "source": { "kind": "release", "tag": "v9" } }""");

        var active = ProfileLoader.LoadActive(Layout);

        Assert.Equal(ProfileOrigin.Embedded, active.Origin);
        Assert.Contains("schema version 1", active.Problem, StringComparison.Ordinal);
    }
```

- [ ] **Step 3: Run the profile tests to see them fail to build**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: errors — `ProfileOs` does not exist, `RelinkerOptions` has no `Args`, `LoadEmbedded` takes no argument.

- [ ] **Step 4: Add `ProfileOs` and the profile fields**

Create `src/PS5Kitsune.Core/Profiles/ProfileOs.cs`:

```csharp
namespace PS5Kitsune.Core.Profiles;

/// <summary>The values of a profile's "os" field; <see cref="Abstractions.IPlatform.Os"/> gives the running OS's.</summary>
public static class ProfileOs
{
    public const string Windows = "windows";

    public const string Linux = "linux";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal) { Windows, Linux };
}
```

In `src/PS5Kitsune.Core/Profiles/AnyPS5Profile.cs`:
- `public const int CurrentSchemaVersion = 1;` becomes `public const int CurrentSchemaVersion = 2;`
- After the `Id` property, add:

```csharp
    /// <summary>The OS this profile is for: <see cref="ProfileOs.Windows"/> or <see cref="ProfileOs.Linux"/>. Validated, not required by the JSON reader,
    /// so a schema 1 file is refused for its schema version rather than for a missing field.</summary>
    public string Os { get; init; } = "";
```

- In `RelinkerOptions`, replace the `WindowsArgs` property (and its doc comment) with:

```csharp
    /// <summary>Arguments before the input and output paths: --windows --registry on Windows, --registry on Linux.</summary>
    public IReadOnlyList<string> Args { get; init; } = [];
```

- [ ] **Step 5: Load the profile of an OS**

In `src/PS5Kitsune.Core/Profiles/ProfileLoader.cs`, replace the `EmbeddedResourceName` constant, `LoadEmbedded` and `LoadActive` with:

```csharp
    private const string EmbeddedResourcePrefix = "PS5Kitsune.Core.Profiles.default-profile.";

    public const string OverrideFileName = "override.json";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>The profile shipped inside the app for <paramref name="os"/>, a <see cref="ProfileOs"/> value.</summary>
    public static AnyPS5Profile LoadEmbedded(string os = ProfileOs.Windows)
    {
        if (!ProfileOs.All.Contains(os))
        {
            throw new ArgumentOutOfRangeException(nameof(os), os, "Unknown profile OS.");
        }

        var name = $"{EmbeddedResourcePrefix}{os}.json";
        using var stream = typeof(ProfileLoader).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidProfileException($"Embedded resource {name} is missing.");
        return Load(stream);
    }

    /// <summary>
    /// The override in the data root's profiles folder when it is valid and for <paramref name="os"/>, else the embedded profile of <paramref name="os"/>.
    /// An override that cannot be read, fails validation or is for another OS is ignored, and <see cref="ActiveProfile.Problem"/> says why.
    /// </summary>
    public static ActiveProfile LoadActive(DataLayout layout, string os = ProfileOs.Windows)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var path = Path.Combine(layout.Profiles, OverrideFileName);
        if (!File.Exists(path))
        {
            return new ActiveProfile(LoadEmbedded(os), ProfileOrigin.Embedded, null);
        }

        try
        {
            AnyPS5Profile profile;
            using (var stream = File.OpenRead(path))
            {
                profile = Load(stream);
            }

            if (profile.Os != os)
            {
                throw new InvalidProfileException($"Profile {profile.Id} is for {profile.Os}, not {os}.");
            }

            return new ActiveProfile(profile, ProfileOrigin.Override, null);
        }
        catch (Exception exception) when (exception is InvalidProfileException or IOException or UnauthorizedAccessException)
        {
            return new ActiveProfile(LoadEmbedded(os), ProfileOrigin.Embedded, $"Ignored {path}: {exception.Message} Using the embedded profile.");
        }
    }
```

In `Validate`, right after the schema version check, add:

```csharp
        if (!ProfileOs.All.Contains(profile.Os))
        {
            throw new InvalidProfileException($"Profile {profile.Id} has os \"{profile.Os}\"; expected \"{ProfileOs.Windows}\" or \"{ProfileOs.Linux}\".");
        }
```

In `src/PS5Kitsune.Core/PS5Kitsune.Core.csproj`, replace the `default-profile.json` `EmbeddedResource` line with:

```xml
    <EmbeddedResource Include="Profiles\default-profile.windows.json" LogicalName="PS5Kitsune.Core.Profiles.default-profile.windows.json" />
    <EmbeddedResource Include="Profiles\default-profile.linux.json" LogicalName="PS5Kitsune.Core.Profiles.default-profile.linux.json" />
```

In `src/PS5Kitsune.Core/Games/Steps/RelinkStep.cs`:
- the class doc's first line becomes `/// Runs the relinker from the game folder: relinker &lt;profile args&gt; [--to-intel] app0/eboot.bin &lt;game executable&gt;.`
- `List<string> arguments = [.. profile.Relinker.WindowsArgs];` becomes `List<string> arguments = [.. profile.Relinker.Args];`

- [ ] **Step 6: Rename the Windows profile and generate the Linux one**

```bash
git mv src/PS5Kitsune.Core/Profiles/default-profile.json src/PS5Kitsune.Core/Profiles/default-profile.windows.json
```

Edit `default-profile.windows.json` by hand (keep its line endings):
- `"schemaVersion": 1,` becomes `"schemaVersion": 2,` and a new line `  "os": "windows",` follows it.
- `"windowsArgs": [ "--windows", "--registry" ],` becomes `"args": [ "--windows", "--registry" ],`.

Write this generator to the scratchpad (`C:\Users\kilia\AppData\Local\Temp\claude\C--Users-kilia-source-repos-PS5Kitsune\<session>\scratchpad\make_linux_profile.py`; any scratch folder outside the repo works) and run it from WSL:

```python
# Builds default-profile.linux.json from the Windows profile: same release, scripts, fonts, checks and report URL;
# Linux relinker and libs; no portable tools; Linux relinker args; only the diagnosis rules whose log texts exist on Linux.
import copy, json, sys

repo = sys.argv[1]
source = f"{repo}/src/PS5Kitsune.Core/Profiles/default-profile.windows.json"
target = f"{repo}/src/PS5Kitsune.Core/Profiles/default-profile.linux.json"
windows = json.load(open(source, encoding="utf-8"))
linux = copy.deepcopy(windows)
linux["os"] = "linux"
linux["validated"] = False
linux["assets"]["relinker"] = {"name": "relinker-v0.1.3", "sha256": "bd2a959885830e14f091477edf297fc4c3f45b83a29171c47f5719d721fedb45"}
linux["assets"]["libs"] = {"name": "prx-linux-v0.1.3.zip", "sha256": "017bdb0e931fa0cdb17f0cea805a824180b88205f5c50f4473550c63a4e811f3"}
del linux["tools"]
linux["relinker"] = {"args": ["--registry"], "intelArg": windows["relinker"]["intelArg"]}
shared = {"gpu-features", "param-json", "invalid-setting", "not-implemented"}
linux["diagnostics"]["rules"] = [rule for rule in windows["diagnostics"]["rules"] if rule["id"] in shared]
with open(target, "w", encoding="utf-8", newline="\n") as output:
    json.dump(linux, output, indent=2, ensure_ascii=False)
    output.write("\n")
```

```bash
wsl -d Ubuntu-24.04 -e python3 -I "/mnt/c/Users/kilia/AppData/Local/Temp/claude/C--Users-kilia-source-repos-PS5Kitsune/<session>/scratchpad/make_linux_profile.py" /mnt/c/Users/kilia/source/repos/PS5Kitsune
```

Check the result: `"os": "linux"`, `"validated": false`, no `"tools"` key, `"relinker": { "args": [ "--registry" ], ... }`, and 7 diagnosis rules (`gpu-features`, `param-json`, four `invalid-setting`, `not-implemented`).

- [ ] **Step 7: Build and run every test on Windows and Linux**

Run the two commands of the Global Constraints.
Expected: 0 warnings; every test passes on both (the new tests included). If an existing test asserts on `WindowsArgs` or the old resource name, update it to `Args` / `LoadEmbedded()`.

- [ ] **Step 8: Commit**

```bash
git add -A src/PS5Kitsune.Core tests/PS5Kitsune.Core.Tests
git commit -m "Give each OS its own embedded profile (schema 2: os, relinker.args)"
```

---

### Task 2: Core — the platform facts a non-Windows OS changes

**Files:**
- Modify: `src/PS5Kitsune.Core/Abstractions/IPlatform.cs`, `src/PS5Kitsune.Platform.Windows/WindowsPlatform.cs`, `src/PS5Kitsune.Core/Games/GameLayout.cs`, `src/PS5Kitsune.Core/Checks/SystemFacts.cs`, `src/PS5Kitsune.Core/Checks/SystemChecker.cs` (the collector), `src/PS5Kitsune.Core/Checks/VulkanChecks.cs`, `src/PS5Kitsune.Core/Checks/HardwareChecks.cs`, `src/PS5Kitsune.Core/Checks/StorageChecks.cs`, `src/PS5Kitsune.Cli/CommandHost.cs`, `src/PS5Kitsune.App/App.axaml.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Fakes/FakePlatform.cs`, `tests/PS5Kitsune.App.Tests/Fakes.cs` (`TestPlatform`), `tests/PS5Kitsune.Core.Tests/Checks/` (the check tests), `tests/PS5Kitsune.Core.Tests/Games/` (a layout test)

**Interfaces:**
- Consumes: `ProfileOs` (Task 1).
- Produces: `IPlatform.Os` (a `ProfileOs` value), `IPlatform.GameExecutableSuffix`, `IPlatform.VulkanLoaderName`, `IPlatform.VulkanInstallAction`; `SystemFacts.VulkanLoaderName`, `SystemFacts.VulkanInstallAction`, `SystemFacts.DefaultDataRoot` (with the Windows values as defaults); `GameLayout.ExecutableName` uses `GameExecutableSuffix`.

- [ ] **Step 1: Write the failing tests**

In `tests/PS5Kitsune.Core.Tests/Fakes/FakePlatform.cs`, add settable members (the defaults are the Windows values, so existing tests keep their expectations):

```csharp
    public string Os { get; set; } = "windows";

    public string GameExecutableSuffix { get; set; } = ".exe";

    public string VulkanLoaderName { get; set; } = "vulkan-1.dll";

    public string VulkanInstallAction { get; set; } = "Install or update the graphics driver from your GPU maker's website (AMD, Intel or NVIDIA).";
```

In `tests/PS5Kitsune.App.Tests/Fakes.cs`, `TestPlatform` gets the same four members as get-only properties with these values (`=> "windows"`, `=> ".exe"`, `=> "vulkan-1.dll"`, `=> "Install or update the graphics driver from your GPU maker's website (AMD, Intel or NVIDIA)."`).

The check tests build their facts with `TestFacts.Healthy()` (`tests/PS5Kitsune.Core.Tests/Checks/TestFacts.cs`). In `VulkanChecksTests.cs`, add:

```csharp
    [Fact]
    public void A_missing_loader_names_the_platform_loader_and_its_fix()
    {
        var facts = TestFacts.Healthy() with
        {
            Vulkan = new VulkanProbeOutcome(VulkanProbeStatus.Succeeded, new VulkanProbeReport(false, "libvulkan.so.1 not found", []), null),
            VulkanLoaderName = "libvulkan.so.1",
            VulkanInstallAction = "Install Vulkan: sudo apt install libvulkan1 mesa-vulkan-drivers.",
        };

        var result = new VulkanDriverCheck().Evaluate(facts);

        Assert.Equal(CheckStatus.Blocking, result.Status);
        Assert.Equal("No Vulkan loader was found (libvulkan.so.1).", result.Summary);
        Assert.Equal("Install Vulkan: sudo apt install libvulkan1 mesa-vulkan-drivers.", result.Action);
    }
```

In `HardwareChecksTests.cs`, add:

```csharp
    [Fact]
    public void Without_a_commit_limit_the_check_is_not_applicable()
    {
        var result = new CommitLimitCheck().Evaluate(TestFacts.Healthy() with { CommitLimitBytes = null });

        Assert.Equal(CheckStatus.Info, result.Status);
        Assert.Equal("Not applicable on this system.", result.Summary);
    }
```

In `StorageChecksTests.cs`, add:

```csharp
    [Fact]
    public void A_path_with_a_space_suggests_the_platform_default()
    {
        var result = new InstallPathCheck().Evaluate(TestFacts.Healthy() with { DataRoot = "/home/a b/ps5kitsune", DefaultDataRoot = "/home/ab/.local/share/ps5kitsune" });

        Assert.Equal(CheckStatus.Blocking, result.Status);
        Assert.Equal("Choose a data folder without spaces or accents, for example /home/ab/.local/share/ps5kitsune (command line: --root).", result.Action);
    }
```

Find a `GameLayout` test file (`grep -rln "new GameLayout(" tests/PS5Kitsune.Core.Tests | head -1`) and add:

```csharp
    [Fact]
    public void The_game_executable_takes_the_game_suffix()
    {
        var layout = new GameLayout(Path.GetTempPath(), "PPSA99666", new FakePlatform { GameExecutableSuffix = ".elf" });

        Assert.Equal("PPSA99666.elf", layout.ExecutableName);
    }
```

- [ ] **Step 2: Run to see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: errors — `SystemFacts` has no `VulkanLoaderName`, `IPlatform` lacks the new members (the fakes declare members the interface does not have: fine), `FakePlatform` has no `GameExecutableSuffix` use.

- [ ] **Step 3: Add the platform members**

In `src/PS5Kitsune.Core/Abstractions/IPlatform.cs`, add after `ExecutableSuffix`:

```csharp
    /// <summary>The <see cref="Profiles.ProfileOs"/> value of this OS: picks the embedded profile and checks an override's "os".</summary>
    string Os { get; }

    /// <summary>Suffix of a converted game: ".exe" on Windows, ".elf" on Linux.</summary>
    string GameExecutableSuffix { get; }

    /// <summary>The Vulkan loader's file name, for the Vulkan check: "vulkan-1.dll", "libvulkan.so.1".</summary>
    string VulkanLoaderName { get; }

    /// <summary>What to do when Vulkan is missing or broken, shown by the Vulkan check.</summary>
    string VulkanInstallAction { get; }
```

In `src/PS5Kitsune.Platform.Windows/WindowsPlatform.cs`, add after `ExecutableSuffix`:

```csharp
    public string Os => ProfileOs.Windows;

    public string GameExecutableSuffix => ".exe";

    public string VulkanLoaderName => "vulkan-1.dll";

    public string VulkanInstallAction => "Install or update the graphics driver from your GPU maker's website (AMD, Intel or NVIDIA).";
```

(and `using PS5Kitsune.Core.Profiles;`).

In `src/PS5Kitsune.Core/Games/GameLayout.cs`: `public string ExecutableName => TitleId + platform.ExecutableSuffix;` becomes `public string ExecutableName => TitleId + platform.GameExecutableSuffix;`.

- [ ] **Step 4: Carry them into the facts and the checks**

In `src/PS5Kitsune.Core/Checks/SystemFacts.cs`, add:

```csharp
    /// <summary>See <see cref="IPlatform.VulkanLoaderName"/>.</summary>
    public string VulkanLoaderName { get; init; } = "vulkan-1.dll";

    /// <summary>See <see cref="IPlatform.VulkanInstallAction"/>.</summary>
    public string VulkanInstallAction { get; init; } = "Install or update the graphics driver from your GPU maker's website (AMD, Intel or NVIDIA).";

    /// <summary>The platform's default data root, suggested when the data root is unusable.</summary>
    public string DefaultDataRoot { get; init; } = @"C:\PS5Kitsune";
```

(with `using PS5Kitsune.Core.Abstractions;` for the cref).

In `SystemFactsCollector.CollectAsync` (`src/PS5Kitsune.Core/Checks/SystemChecker.cs`), add to the initializer:

```csharp
        VulkanLoaderName = platform.VulkanLoaderName,
        VulkanInstallAction = platform.VulkanInstallAction,
        DefaultDataRoot = platform.DefaultDataRoot,
```

In `VulkanDriverCheck` (`VulkanChecks.cs`), remove the `UpdateDriver` constant and use the facts:

```csharp
        return facts.Vulkan.Report switch
        {
            null => Result(CheckStatus.Blocking, $"Vulkan enumeration failed. {facts.Vulkan.FailureReason}", facts.VulkanInstallAction),
            { LoaderFound: false } => Result(CheckStatus.Blocking, $"No Vulkan loader was found ({facts.VulkanLoaderName}).", facts.VulkanInstallAction),
            { Gpus.Count: 0 } report => Result(CheckStatus.Blocking, report.Error is null ? "Vulkan found no GPU." : $"Vulkan found no GPU: {report.Error}", facts.VulkanInstallAction),
            var report => Result(CheckStatus.Ok, $"{report.Gpus.Count} GPU(s): {VulkanFacts.Names(report.Gpus)}."),
        };
```

In `CommitLimitCheck` (`HardwareChecks.cs`): `"Not checked on this system."` becomes `"Not applicable on this system."`.

In `InstallPathCheck` (`StorageChecks.cs`):

```csharp
            : Result(
                CheckStatus.Blocking,
                $"{root} contains spaces or non-ASCII characters; AnyPS5's tools fail on such paths.",
                $"Choose a data folder without spaces or accents, for example {facts.DefaultDataRoot} (command line: --root).");
```

- [ ] **Step 5: Load the running OS's profile in the hosts**

`src/PS5Kitsune.Cli/CommandHost.cs`: `var active = ProfileLoader.LoadActive(layout);` becomes `var active = ProfileLoader.LoadActive(layout, platform.Os);`.

`src/PS5Kitsune.App/App.axaml.cs`: `ProfileLoader.LoadActive(new DataLayout(root)).Profile` becomes `ProfileLoader.LoadActive(new DataLayout(root), platform.Os).Profile`.

- [ ] **Step 6: Build and run every test on Windows and Linux**

Expected: 0 warnings, all pass. Existing tests that asserted the old texts ("Not checked on this system.", "WinLibs and CMake fail", `C:\PS5Kitsune` in the action) are updated to the new texts — these are the only text changes allowed in this task.

- [ ] **Step 7: Commit**

```bash
git add -A src tests
git commit -m "Take the game suffix and the Vulkan and data-root texts from the platform"
```

---

### Task 3: Core — Python behind an interface, execute bits, the build-mode message

**Files:**
- Create: `src/PS5Kitsune.Core/Tools/IPythonRuntime.cs`, `src/PS5Kitsune.Core/Tools/PythonScripts.cs`, `src/PS5Kitsune.Core/Tools/PythonMissingException.cs`, `src/PS5Kitsune.Core/AnyPS5/Steps/PreparePythonStep.cs`, `src/PS5Kitsune.Core/IO/ExecutableBits.cs`
- Modify: `src/PS5Kitsune.Core/Tools/PythonEmbeddableTool.cs`, `src/PS5Kitsune.Core/AnyPS5/AnyPS5Installer.cs`, `src/PS5Kitsune.Core/Games/GameImporter.cs`, `src/PS5Kitsune.Core/Games/GameReconverter.cs`, `src/PS5Kitsune.Core/Games/Steps/AuditStep.cs`, `src/PS5Kitsune.Core/Games/Steps/UnwrapSelfStep.cs`, `src/PS5Kitsune.Core/AnyPS5/Steps/DownloadFileStep.cs`, `src/PS5Kitsune.Core/AnyPS5/ReleaseSource.cs`, `src/PS5Kitsune.Core/Library/GamePlayer.cs`, `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`
- Test: `tests/PS5Kitsune.Core.Tests/IO/ExecutableBitsTests.cs` (new), `tests/PS5Kitsune.Core.Tests/IO/FileTreeTests.cs`, `tests/PS5Kitsune.Core.Tests/AnyPS5/AnyPS5InstallerTests.cs`, `tests/PS5Kitsune.Core.Tests/AnyPS5/ReleaseSourceTests.cs` (or the file testing `ReleaseSource`)

**Interfaces:**
- Consumes: `ProfileOs`, `AnyPS5Profile.Os` (Task 1).
- Produces:
  - `IPythonRuntime { string StepName { get; } bool IsInstalled(); Task InstallAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default); ProcessSpec CreateScriptSpec(string script, IEnumerable<string> arguments); }`
  - `PythonScripts.CreateSpec(string executable, string script, IEnumerable<string> arguments)` (the runpy bootstrap, shared).
  - `PythonMissingException(string message)`.
  - `ExecutableBits.Has(string path)`, `ExecutableBits.Ensure(string path)`, `ExecutableBits.EnsureGame(string executable)` (no-ops on Windows).
  - `DownloadFileStep(string name, Uri url, string destination, string sha256, IDownloader downloader, bool executable = false)`.
  - `AnyPS5Installer(…, TimeProvider time, AnyPS5Profile? profile = null)`; `AnyPS5Installer.UnavailableMessage(AnyPS5Mode mode)` (internal).

- [ ] **Step 1: Write the failing tests**

Create `tests/PS5Kitsune.Core.Tests/IO/ExecutableBitsTests.cs`:

```csharp
using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Core.Tests.IO;

public sealed class ExecutableBitsTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void Ensure_adds_the_execute_bits_and_keeps_the_others()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        var file = _temp.Combine("PPSA99666.elf");
        File.WriteAllText(file, "elf");
        File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);

        Assert.False(ExecutableBits.Has(file));
        ExecutableBits.Ensure(file);

        Assert.True(ExecutableBits.Has(file));
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute,
            File.GetUnixFileMode(file));
    }

    [Fact]
    public void EnsureGame_covers_every_file_with_the_game_extension_beside_it()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        var game = _temp.Combine("PPSA99666.elf");
        var picker = _temp.Combine("picker.picker.bin.elf");
        var registry = _temp.Combine("PPSA99666.registry.json");
        foreach (var file in new[] { game, picker, registry })
        {
            File.WriteAllText(file, "x");
            File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        ExecutableBits.EnsureGame(game);

        Assert.True(ExecutableBits.Has(game));
        Assert.True(ExecutableBits.Has(picker));
        Assert.False(ExecutableBits.Has(registry));
    }

    [Fact]
    public void On_windows_everything_counts_as_executable()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        var file = _temp.Combine("game.exe");
        File.WriteAllText(file, "x");

        ExecutableBits.EnsureGame(file);

        Assert.True(ExecutableBits.Has(file));
    }

    public void Dispose() => _temp.Dispose();
}
```

In `tests/PS5Kitsune.Core.Tests/IO/FileTreeTests.cs`, add the spec's symbolic-link test. It passes at once: .NET follows a linked folder while enumerating and `File.Copy` writes the target's content (checked in WSL). It guards that `app0` never receives a link, which AnyPS5 forbids on Linux:

```csharp
    [Fact]
    public void Symbolic_links_are_copied_as_real_files()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        var real = _temp.Combine("real");
        var source = _temp.Combine("source");
        var target = _temp.Combine("target");
        TestFiles.Write(Path.Combine(real, "a.txt"), "aaa");
        TestFiles.Write(Path.Combine(real, "sub", "b.txt"), "bb");
        Directory.CreateDirectory(source);
        File.CreateSymbolicLink(Path.Combine(source, "a.txt"), Path.Combine(real, "a.txt"));
        Directory.CreateSymbolicLink(Path.Combine(source, "sub"), Path.Combine(real, "sub"));

        FileTree.Copy(source, target, _ => false, TestContext.Current.CancellationToken);

        Assert.Equal("aaa", File.ReadAllText(Path.Combine(target, "a.txt")));
        Assert.Null(new FileInfo(Path.Combine(target, "a.txt")).LinkTarget);
        Assert.Equal("bb", File.ReadAllText(Path.Combine(target, "sub", "b.txt")));
        Assert.Null(new DirectoryInfo(Path.Combine(target, "sub")).LinkTarget);
    }
```

In `AnyPS5InstallerTests.cs`, add (next to the existing `releaseOnly` tests, reusing `_setup`):

```csharp
    [Fact]
    public void On_linux_build_mode_is_refused_with_its_own_message()
    {
        var linuxProfile = _setup.Profile with { Os = ProfileOs.Linux };
        var releaseOnly = new AnyPS5Installer([_setup.ReleaseSource], _setup.PythonTool, _setup.Paths, _setup.StepRunner, _setup.Time, linuxProfile);

        var create = Assert.Throws<InvalidOperationException>(() => releaseOnly.CreateSteps(AnyPS5Mode.Build));
        var use = Assert.Throws<InvalidOperationException>(() => releaseOnly.Use(AnyPS5Mode.Build));

        Assert.Equal("Build mode is not available on Linux yet; use release mode.", create.Message);
        Assert.Equal("Build mode is not available on Linux yet; use release mode.", use.Message);
    }
```

In the test file of `ReleaseSource` (`grep -rln "ReleaseSource" tests/PS5Kitsune.Core.Tests`), add a Linux-only test that installs the release through the existing setup and checks the relinker's execute bit:

```csharp
    [Fact]
    public async Task On_linux_the_downloaded_relinker_is_executable()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");

        await _setup.Installer.InstallAsync(AnyPS5Mode.Release, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(ExecutableBits.Has(_setup.Paths.For(AnyPS5Mode.Release).Relinker));
    }
```

(If that file has no `_setup` field, put this test in `AnyPS5InstallerTests.cs`, which has one. Add `using PS5Kitsune.Core.IO;` and `using PS5Kitsune.Core.Profiles;` where needed.)

- [ ] **Step 2: Run to see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release` — expected errors: `ExecutableBits` missing, no 6-argument `AnyPS5Installer` constructor.

- [ ] **Step 3: Add `ExecutableBits`**

Create `src/PS5Kitsune.Core/IO/ExecutableBits.cs`:

```csharp
namespace PS5Kitsune.Core.IO;

/// <summary>
/// The execute bits a Linux program needs and a copy may lose (a browser download, a copy tool that drops modes).
/// Windows has no such bits: there every file counts as executable and nothing changes.
/// </summary>
public static class ExecutableBits
{
    private const UnixFileMode AllExecute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    /// <summary>True when the user may execute <paramref name="path"/>; always true on Windows.</summary>
    public static bool Has(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        return (File.GetUnixFileMode(path) & UnixFileMode.UserExecute) != 0;
    }

    /// <summary>Adds the user, group and other execute bits to <paramref name="path"/>, keeping its other bits.</summary>
    public static void Ensure(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var mode = File.GetUnixFileMode(path);
        if ((mode & UnixFileMode.UserExecute) == 0)
        {
            File.SetUnixFileMode(path, mode | AllExecute);
        }
    }

    /// <summary>
    /// Ensures the execute bits of a converted game and of every file beside it with the same extension: the relinker names the other
    /// executables it converts after the main one's extension (see USAGE.md, --executable).
    /// </summary>
    public static void EnsureGame(string executable)
    {
        ArgumentNullException.ThrowIfNull(executable);
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Ensure(executable);
        var extension = Path.GetExtension(executable);
        if (extension.Length > 0 && Path.GetDirectoryName(executable) is { } folder)
        {
            foreach (var sibling in Directory.EnumerateFiles(folder, "*" + extension))
            {
                Ensure(sibling);
            }
        }
    }
}
```

- [ ] **Step 4: The relinker download keeps its execute bit, and a launch ensures the game's**

`src/PS5Kitsune.Core/AnyPS5/Steps/DownloadFileStep.cs` becomes:

```csharp
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.AnyPS5.Steps;

/// <summary>
/// Downloads one file to its final place. Done when it is there with the expected hash and, for a program (<paramref name="executable"/>),
/// with its execute bit (Linux).
/// </summary>
public sealed class DownloadFileStep(string name, Uri url, string destination, string sha256, IDownloader downloader, bool executable = false) : IStep
{
    public string Name => name;

    public bool IsAlreadyDone() => FileHash.Matches(destination, sha256) && (!executable || ExecutableBits.Has(destination));

    public async Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        if (!FileHash.Matches(destination, sha256))
        {
            await downloader.DownloadAsync(new DownloadRequest(url, destination, sha256), StepProgress.ForDownload(progress), cancellationToken).ConfigureAwait(false);
        }

        if (executable)
        {
            ExecutableBits.Ensure(destination);
        }
    }
}
```

In `ReleaseSource.CreateSteps`, the relinker step gets `executable: true`:

```csharp
            new DownloadFileStep("Download the relinker", profile.AssetUrl(ProfileAssetKeys.Relinker), target.Relinker, relinker.Sha256, downloader, executable: true),
```

In `GamePlayer.LaunchAsync`, right after the `GameExecutableMissingException` check and before computing `manifest`, add:

```csharp
        // A copy may have dropped the execute bits (Linux); the relinker set them.
        ExecutableBits.EnsureGame(executable);
```

(with `using PS5Kitsune.Core.IO;`).

- [ ] **Step 5: Put Python behind `IPythonRuntime`**

Create `src/PS5Kitsune.Core/Tools/IPythonRuntime.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Tools;

/// <summary>The Python that runs AnyPS5's scripts: the embeddable zip on Windows, the system python3 on Linux.</summary>
public interface IPythonRuntime
{
    /// <summary>The install step's name: "Install Python", or "Find Python 3" where the system provides it.</summary>
    string StepName { get; }

    /// <summary>True when Python can run the scripts now.</summary>
    bool IsInstalled();

    /// <summary>Downloads Python; where the system provides it, throws <see cref="PythonMissingException"/> naming the command that installs it.</summary>
    Task InstallAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>A command running <paramref name="script"/> with <paramref name="arguments"/>; the script's exit code is the process exit code.</summary>
    ProcessSpec CreateScriptSpec(string script, IEnumerable<string> arguments);
}
```

Create `src/PS5Kitsune.Core/Tools/PythonScripts.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Tools;

/// <summary>How every Python runs a script of AnyPS5.</summary>
public static class PythonScripts
{
    // The ._pth file of the embeddable distribution keeps the script's folder off sys.path and makes
    // Python ignore PYTHONPATH, so a script importing its siblings (import_audit.py imports nid_names
    // and progress) fails when run directly. This bootstrap puts the folder back first, then runs the
    // script as __main__; its SystemExit code becomes the process exit code. A system Python needs no
    // bootstrap, and runs it the same.
    private const string ScriptBootstrap =
        "import os,runpy,sys;sys.argv=sys.argv[1:];sys.path.insert(0,os.path.dirname(os.path.abspath(sys.argv[0])));runpy.run_path(sys.argv[0],run_name='__main__')";

    public static ProcessSpec CreateSpec(string executable, string script, IEnumerable<string> arguments) =>
        new(executable, ["-c", ScriptBootstrap, script, .. arguments]);
}
```

Create `src/PS5Kitsune.Core/Tools/PythonMissingException.cs`:

```csharp
namespace PS5Kitsune.Core.Tools;

/// <summary>The system has no Python 3. The message names the command that installs it and is shown as is.</summary>
public sealed class PythonMissingException(string message) : Exception(message);
```

In `PythonEmbeddableTool.cs`: remove the `ScriptBootstrap` constant and its comment; declare `: ArchiveTool(ToolId, definition, layout, downloader), IPythonRuntime`; add `public string StepName => "Install Python";`; `CreateScriptSpec` becomes:

```csharp
    public ProcessSpec CreateScriptSpec(string script, IEnumerable<string> arguments) => PythonScripts.CreateSpec(Executable, script, arguments);
```

Create `src/PS5Kitsune.Core/AnyPS5/Steps/PreparePythonStep.cs`:

```csharp
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.AnyPS5.Steps;

/// <summary>Makes sure Python can run AnyPS5's scripts: downloads the embeddable zip, or finds the system python3.</summary>
public sealed class PreparePythonStep(IPythonRuntime python) : IStep
{
    public string Name => python.StepName;

    public bool IsAlreadyDone() => python.IsInstalled();

    public Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken) =>
        python.InstallAsync(StepProgress.ForDownload(progress), cancellationToken);
}
```

Replace the type `PythonEmbeddableTool` with `IPythonRuntime` in the constructor parameters of `AnyPS5Installer`, `GameImporter`, `GameReconverter`, `AuditStep` and `UnwrapSelfStep` (their bodies only call `CreateScriptSpec` or pass the object on).

- [ ] **Step 6: The installer's steps and build-mode message**

`AnyPS5Installer`'s primary constructor becomes:

```csharp
public sealed class AnyPS5Installer(
    IEnumerable<IAnyPS5Source> sources,
    IPythonRuntime python,
    AnyPS5Paths paths,
    StepRunner stepRunner,
    TimeProvider time,
    AnyPS5Profile? profile = null) : IAnyPS5Installer
```

(`using PS5Kitsune.Core.Profiles;`). Dependency injection fills `profile` from the registered `AnyPS5Profile`.

`CreateSteps` becomes:

```csharp
    public IReadOnlyList<IStep> CreateSteps(AnyPS5Mode mode)
    {
        var source = Source(mode) ?? throw new InvalidOperationException(UnavailableMessage(mode));
        return [new PreparePythonStep(python), .. source.CreateSteps(), new WriteInstallRecordStep(source, paths.For(mode), time)];
    }

    internal string UnavailableMessage(AnyPS5Mode mode) => mode switch
    {
        AnyPS5Mode.Build when profile?.Os == ProfileOs.Linux => "Build mode is not available on Linux yet; use release mode.",
        AnyPS5Mode.Build => "Mode build is not available: the active profile has no build section or no build tools.",
        _ => "Mode release is not available.",
    };
```

At the start of `Use`, before the `RequestedMode` check:

```csharp
        if (!Supports(mode))
        {
            throw new InvalidOperationException(UnavailableMessage(mode));
        }
```

- [ ] **Step 7: Register Python as `IPythonRuntime`**

In `ServiceCollectionExtensions.AddPS5KitsuneCore`, right after `AddTool<PythonEmbeddableTool>(…)`:

```csharp
        // The platform registers the system Python first where it has one (Linux); TryAdd keeps it.
        if (profile.Tools.ContainsKey(PythonEmbeddableTool.ToolId))
        {
            services.TryAddSingleton<IPythonRuntime>(provider => provider.GetRequiredService<PythonEmbeddableTool>());
        }
```

- [ ] **Step 8: Build and run every test on Windows and Linux**

Expected: 0 warnings, all pass (the new tests run on their OS and skip on the other). The existing tests keep passing `PythonEmbeddableTool` where an `IPythonRuntime` is expected; the Windows install's first step is still named "Install Python".

- [ ] **Step 9: Commit**

```bash
git add -A src tests
git commit -m "Run scripts through IPythonRuntime, keep execute bits on Linux, refuse build mode on Linux"
```

---

### Task 4: Platform.Linux — folders, package commands, system Python and Lavapipe

**Files:**
- Create: `src/PS5Kitsune.Platform.Linux/PS5Kitsune.Platform.Linux.csproj`, `LinuxPlatform.cs`, `OsRelease.cs`, `DistroPackages.cs`, `PathSearch.cs`, `VulkanManifests.cs`, `SystemPython.cs`, `SystemLavapipe.cs`
- Create: `tests/PS5Kitsune.Platform.Linux.Tests/PS5Kitsune.Platform.Linux.Tests.csproj`, `LinuxPlatformTests.cs`, `DistroPackagesTests.cs`, `SystemPythonTests.cs`, `SystemLavapipeTests.cs`
- Modify: `PS5Kitsune.slnx`

**Interfaces:**
- Consumes: `IPlatform` members (Task 2), `IPythonRuntime`, `PythonScripts`, `PythonMissingException`, `ExecutableBits` (Task 3), `ILavapipe`, `LavapipeUnavailableException` (existing, `PS5Kitsune.Core.Launch`), `ProfileOs` (Task 1).
- Produces (namespace `PS5Kitsune.Platform.Linux`):
  - `OsRelease(string Id, IReadOnlyList<string> IdLike)` with `Parse(string text)`, `Read(string path = "/etc/os-release")`, `Unknown`.
  - `enum LinuxPackage { Python, VulkanLoader, MesaVulkanDrivers }`; `DistroPackages(OsRelease release)` with `string CommandFor(params LinuxPackage[] packages)`.
  - `LinuxPlatform(DistroPackages packages)` : `IPlatform`, with `DistroPackages Packages`.
  - `SystemPython(DistroPackages packages)` : `IPythonRuntime`, with `string? Executable` and `internal string? SearchPath { init; }`.
  - `SystemLavapipe(DistroPackages packages)` : `ILavapipe`, with `internal IReadOnlyList<string> ManifestDirectories { init; }`.
  - `VulkanManifests.Directories` (`/usr/share/vulkan/icd.d`, `/etc/vulkan/icd.d`).

- [ ] **Step 1: Create the projects**

`src/PS5Kitsune.Platform.Linux/PS5Kitsune.Platform.Linux.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!-- Targets plain net10.0 (not a Linux-only TFM) so the Windows build still compiles it;
       Linux-only APIs are guarded by [SupportedOSPlatform("linux")]. -->

  <ItemGroup>
    <ProjectReference Include="..\PS5Kitsune.Core\PS5Kitsune.Core.csproj" />
  </ItemGroup>

  <PropertyGroup>
    <!-- Required by the LibraryImport source generator (P/Invoke for the game launcher). -->
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="PS5Kitsune.Platform.Linux.Tests" />
  </ItemGroup>

</Project>
```

`tests/PS5Kitsune.Platform.Linux.Tests/PS5Kitsune.Platform.Linux.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
    <IsPublishable>false</IsPublishable>
    <!-- Test code: allow underscores in test names. -->
    <NoWarn>$(NoWarn);CA1707</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit.v3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\PS5Kitsune.Platform.Linux\PS5Kitsune.Platform.Linux.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Compile Include="..\PS5Kitsune.Core.Tests\TempDirectory.cs" Link="TempDirectory.cs" />
    <Compile Include="..\PS5Kitsune.Core.Tests\Checks\TestFacts.cs" Link="TestFacts.cs" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

</Project>
```

In `PS5Kitsune.slnx`, add `<Project Path="src/PS5Kitsune.Platform.Linux/PS5Kitsune.Platform.Linux.csproj" />` after the Windows platform line, and `<Project Path="tests/PS5Kitsune.Platform.Linux.Tests/PS5Kitsune.Platform.Linux.Tests.csproj" />` after the Windows tests line.

- [ ] **Step 2: Write the failing tests**

`tests/PS5Kitsune.Platform.Linux.Tests/DistroPackagesTests.cs` (pure text: runs on both OSes):

```csharp
namespace PS5Kitsune.Platform.Linux.Tests;

public sealed class DistroPackagesTests : IDisposable
{
    private readonly PS5Kitsune.Core.Tests.TempDirectory _temp = new();

    private static string CommandFor(string osRelease, params LinuxPackage[] packages) => new DistroPackages(OsRelease.Parse(osRelease)).CommandFor(packages);

    [Fact]
    public void Ubuntu_uses_apt()
    {
        var ubuntu = "PRETTY_NAME=\"Ubuntu 24.04.5 LTS\"\nNAME=\"Ubuntu\"\nID=ubuntu\nID_LIKE=debian\n";

        Assert.Equal("sudo apt install python3", CommandFor(ubuntu, LinuxPackage.Python));
        Assert.Equal("sudo apt install libvulkan1 mesa-vulkan-drivers", CommandFor(ubuntu, LinuxPackage.VulkanLoader, LinuxPackage.MesaVulkanDrivers));
    }

    [Fact]
    public void Debian_uses_apt() => Assert.Equal("sudo apt install python3", CommandFor("ID=debian\n", LinuxPackage.Python));

    [Fact]
    public void Linux_mint_is_found_through_its_quoted_id_like() =>
        Assert.Equal("sudo apt install mesa-vulkan-drivers", CommandFor("ID=linuxmint\nID_LIKE=\"ubuntu debian\"\n", LinuxPackage.MesaVulkanDrivers));

    [Fact]
    public void Fedora_uses_dnf() =>
        Assert.Equal("sudo dnf install python3 vulkan-loader mesa-vulkan-drivers", CommandFor("ID=fedora\n", LinuxPackage.Python, LinuxPackage.VulkanLoader, LinuxPackage.MesaVulkanDrivers));

    [Fact]
    public void Arch_uses_pacman() =>
        Assert.Equal("sudo pacman -S python vulkan-icd-loader vulkan-swrast", CommandFor("ID='arch'\n", LinuxPackage.Python, LinuxPackage.VulkanLoader, LinuxPackage.MesaVulkanDrivers));

    [Fact]
    public void An_unknown_distribution_gets_the_debian_names() =>
        Assert.Equal("Install: python3, libvulkan1", CommandFor("ID=gentoo\n", LinuxPackage.Python, LinuxPackage.VulkanLoader));

    [Fact]
    public void Comments_and_blank_lines_are_skipped()
    {
        var release = OsRelease.Parse("# a comment\n\nID=fedora\r\nID_LIKE=\n");

        Assert.Equal("fedora", release.Id);
        Assert.Empty(release.IdLike);
    }

    [Fact]
    public void A_missing_file_is_an_unknown_distribution()
    {
        var release = OsRelease.Read(_temp.Combine("os-release"));

        Assert.Equal("", release.Id);
        Assert.Equal("Install: python3", new DistroPackages(release).CommandFor(LinuxPackage.Python));
    }

    public void Dispose() => _temp.Dispose();
}
```

(If `TempDirectory`'s namespace differs from `PS5Kitsune.Core.Tests`, use the one its file declares.)

`tests/PS5Kitsune.Platform.Linux.Tests/LinuxPlatformTests.cs`:

```csharp
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Platform.Linux.Tests;

public sealed class LinuxPlatformTests
{
    private static readonly DistroPackages Ubuntu = new(OsRelease.Parse("ID=ubuntu\nID_LIKE=debian\n"));

    private static LinuxPlatform With(params (string Name, string Value)[] variables) =>
        new(Ubuntu, name => variables.FirstOrDefault(variable => variable.Name == name).Value, "/home/ka");

    [Fact]
    public void Without_xdg_variables_the_folders_are_under_home()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        var platform = With();

        Assert.Equal("/home/ka/.local/share/ps5kitsune", platform.DefaultDataRoot);
        Assert.Equal("/home/ka/.config/ps5kitsune", platform.SettingsDirectory);
    }

    [Fact]
    public void Absolute_xdg_variables_are_used()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        var platform = With(("XDG_DATA_HOME", "/data"), ("XDG_CONFIG_HOME", "/cfg"));

        Assert.Equal("/data/ps5kitsune", platform.DefaultDataRoot);
        Assert.Equal("/cfg/ps5kitsune", platform.SettingsDirectory);
    }

    [Fact]
    public void Empty_or_relative_xdg_variables_are_ignored()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        var platform = With(("XDG_DATA_HOME", ""), ("XDG_CONFIG_HOME", "relative/cfg"));

        Assert.Equal("/home/ka/.local/share/ps5kitsune", platform.DefaultDataRoot);
        Assert.Equal("/home/ka/.config/ps5kitsune", platform.SettingsDirectory);
    }

    [Fact]
    public void Describes_linux()
    {
        var platform = With();

        Assert.Equal(ProfileOs.Linux, platform.Os);
        Assert.Equal("", platform.ExecutableSuffix);
        Assert.Equal(".elf", platform.GameExecutableSuffix);
        Assert.Null(platform.GetCommitLimitBytes());
        Assert.Equal("libvulkan.so.1", platform.VulkanLoaderName);
        Assert.Equal(
            "Install Vulkan: sudo apt install libvulkan1 mesa-vulkan-drivers. With an NVIDIA GPU, install NVIDIA's driver instead of Mesa.",
            platform.VulkanInstallAction);
    }
}
```

`tests/PS5Kitsune.Platform.Linux.Tests/SystemPythonTests.cs`:

```csharp
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Platform.Linux.Tests;

public sealed class SystemPythonTests : IDisposable
{
    private readonly PS5Kitsune.Core.Tests.TempDirectory _temp = new();

    private static readonly DistroPackages Ubuntu = new(OsRelease.Parse("ID=ubuntu\n"));

    [Fact]
    public void Finds_an_executable_python3_on_the_path()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        var bin = _temp.Combine("bin");
        Directory.CreateDirectory(bin);
        var python = Path.Combine(bin, "python3");
        File.WriteAllText(python, "#!/bin/sh\n");
        File.SetUnixFileMode(python, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var runtime = new SystemPython(Ubuntu) { SearchPath = $"relative:{_temp.Combine("empty")}:{bin}" };

        Assert.True(runtime.IsInstalled());
        Assert.Equal(python, runtime.Executable);
        Assert.Equal(python, runtime.CreateScriptSpec("/s/import_audit.py", ["--json", "a.json"]).FileName);
        Assert.Equal("Find Python 3", runtime.StepName);
    }

    [Fact]
    public void A_python3_without_its_execute_bit_does_not_count()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        var python = _temp.Combine("python3");
        File.WriteAllText(python, "");
        File.SetUnixFileMode(python, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        Assert.False(new SystemPython(Ubuntu) { SearchPath = _temp.Path }.IsInstalled());
    }

    [Fact]
    public async Task Without_python3_install_names_the_command()
    {
        var runtime = new SystemPython(Ubuntu) { SearchPath = "" };

        var exception = await Assert.ThrowsAsync<PythonMissingException>(() => runtime.InstallAsync(cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("Python 3 is required: sudo apt install python3", exception.Message);
    }

    public void Dispose() => _temp.Dispose();
}
```

`tests/PS5Kitsune.Platform.Linux.Tests/SystemLavapipeTests.cs`:

```csharp
using PS5Kitsune.Core.Launch;

namespace PS5Kitsune.Platform.Linux.Tests;

public sealed class SystemLavapipeTests : IDisposable
{
    private readonly PS5Kitsune.Core.Tests.TempDirectory _temp = new();

    private static readonly DistroPackages Fedora = new(OsRelease.Parse("ID=fedora\n"));

    [Fact]
    public void Finds_the_lavapipe_manifest()
    {
        var icd = _temp.Combine("icd.d");
        Directory.CreateDirectory(icd);
        File.WriteAllText(Path.Combine(icd, "radeon_icd.json"), "{}");
        File.WriteAllText(Path.Combine(icd, "lvp_icd.json"), "{}");
        var lavapipe = new SystemLavapipe(Fedora) { ManifestDirectories = [_temp.Combine("missing"), icd] };

        Assert.True(lavapipe.IsInstalled());
        Assert.Equal(Path.Combine(icd, "lvp_icd.json"), lavapipe.Manifest);
    }

    [Fact]
    public async Task Without_a_manifest_install_names_the_mesa_command()
    {
        var lavapipe = new SystemLavapipe(Fedora) { ManifestDirectories = [_temp.Path] };

        Assert.False(lavapipe.IsInstalled());
        Assert.Equal("", lavapipe.Manifest);
        var exception = await Assert.ThrowsAsync<LavapipeUnavailableException>(() => lavapipe.InstallAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("Lavapipe is not installed: sudo dnf install mesa-vulkan-drivers", exception.Message);
    }

    public void Dispose() => _temp.Dispose();
}
```

- [ ] **Step 3: Run to see them fail to build**

Run: `dotnet build PS5Kitsune.slnx -c Release` — expected: the `PS5Kitsune.Platform.Linux` types do not exist.

- [ ] **Step 4: Implement the distribution side**

`src/PS5Kitsune.Platform.Linux/OsRelease.cs`:

```csharp
namespace PS5Kitsune.Platform.Linux;

/// <summary>The distribution, from /etc/os-release: its ID and the IDs it is like (ID_LIKE).</summary>
public sealed record OsRelease(string Id, IReadOnlyList<string> IdLike)
{
    public const string DefaultPath = "/etc/os-release";

    public static OsRelease Unknown { get; } = new("", []);

    /// <summary>Reads KEY=value lines; values may be quoted with " or '. Comments and other lines are skipped.</summary>
    public static OsRelease Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var id = "";
        var idLike = "";
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            var equals = line.IndexOf('=', StringComparison.Ordinal);
            if (line.StartsWith('#') || equals <= 0)
            {
                continue;
            }

            var value = Unquote(line[(equals + 1)..]);
            switch (line[..equals])
            {
                case "ID":
                    id = value;
                    break;
                case "ID_LIKE":
                    idLike = value;
                    break;
            }
        }

        return new OsRelease(id, idLike.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>The file's distribution; <see cref="Unknown"/> when it is missing or unreadable.</summary>
    public static OsRelease Read(string path = DefaultPath)
    {
        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Unknown;
        }
    }

    private static string Unquote(string value) =>
        value.Length >= 2 && value[0] is '"' or '\'' && value[^1] == value[0] ? value[1..^1] : value;
}
```

(Careful with precedence in `Unquote`: write it as `value.Length >= 2 && (value[0] is '"' or '\'') && value[^1] == value[0]`.)

`src/PS5Kitsune.Platform.Linux/DistroPackages.cs`:

```csharp
namespace PS5Kitsune.Platform.Linux;

/// <summary>What PS5Kitsune may need from the distribution.</summary>
public enum LinuxPackage
{
    Python,
    VulkanLoader,

    /// <summary>Mesa's Vulkan drivers: Lavapipe, and RADV for AMD GPUs.</summary>
    MesaVulkanDrivers,
}

/// <summary>The command that installs system packages on this distribution, shown for the user to run. See the 8a spec, "Other Linux services".</summary>
public sealed class DistroPackages(OsRelease release)
{
    private static readonly Family Debian = new("sudo apt install", "python3", "libvulkan1", "mesa-vulkan-drivers");
    private static readonly Family Fedora = new("sudo dnf install", "python3", "vulkan-loader", "mesa-vulkan-drivers");
    private static readonly Family Arch = new("sudo pacman -S", "python", "vulkan-icd-loader", "vulkan-swrast");

    /// <summary>One command for all <paramref name="packages"/>; for an unknown distribution, "Install: " and the Debian names.</summary>
    public string CommandFor(params LinuxPackage[] packages)
    {
        ArgumentNullException.ThrowIfNull(packages);
        var family = FamilyOf(release);
        var names = packages.Select(package => (family ?? Debian).Name(package));
        return family is null ? $"Install: {string.Join(", ", names)}" : $"{family.Command} {string.Join(' ', names)}";
    }

    private static Family? FamilyOf(OsRelease release) =>
        release.IdLike.Prepend(release.Id).Select(id => id switch
        {
            "debian" or "ubuntu" => Debian,
            "fedora" => Fedora,
            "arch" => Arch,
            _ => null,
        }).FirstOrDefault(family => family is not null);

    private sealed record Family(string Command, string Python, string VulkanLoader, string Mesa)
    {
        public string Name(LinuxPackage package) => package switch
        {
            LinuxPackage.Python => Python,
            LinuxPackage.VulkanLoader => VulkanLoader,
            LinuxPackage.MesaVulkanDrivers => Mesa,
            _ => throw new ArgumentOutOfRangeException(nameof(package), package, null),
        };
    }
}
```

- [ ] **Step 5: Implement the platform, Python and Lavapipe**

`src/PS5Kitsune.Platform.Linux/VulkanManifests.cs`:

```csharp
namespace PS5Kitsune.Platform.Linux;

/// <summary>Where Linux distributions install Vulkan driver manifests (ICD JSON files).</summary>
public static class VulkanManifests
{
    public static IReadOnlyList<string> Directories { get; } = ["/usr/share/vulkan/icd.d", "/etc/vulkan/icd.d"];

    /// <summary>The files matching <paramref name="pattern"/> in the folders that exist, sorted by path.</summary>
    public static IReadOnlyList<string> Find(IEnumerable<string> directories, string pattern) =>
        [.. directories.Where(Directory.Exists).SelectMany(directory => Directory.EnumerateFiles(directory, pattern)).Order(StringComparer.Ordinal)];
}
```

`src/PS5Kitsune.Platform.Linux/PathSearch.cs`:

```csharp
using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Platform.Linux;

/// <summary>Finds a program the way a shell does: the first executable file of that name in the PATH folders.</summary>
internal static class PathSearch
{
    public static string? Find(string name, string? path)
    {
        foreach (var folder in (path ?? "").Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Path.IsPathRooted(folder))
            {
                continue;
            }

            var candidate = Path.Combine(folder, name);
            if (File.Exists(candidate) && ExecutableBits.Has(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
```

`src/PS5Kitsune.Platform.Linux/LinuxPlatform.cs`:

```csharp
using System.Diagnostics;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Platform.Linux;

/// <summary>Linux: XDG folders, no executable suffix for tools, .elf games, no commit limit. See the 8a spec, "IPlatform on Linux".</summary>
public sealed class LinuxPlatform : IPlatform
{
    private readonly Func<string, string?> _variable;
    private readonly string _home;

    public LinuxPlatform(DistroPackages packages)
        : this(packages, Environment.GetEnvironmentVariable, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
    {
    }

    internal LinuxPlatform(DistroPackages packages, Func<string, string?> variable, string home)
    {
        Packages = packages;
        _variable = variable;
        _home = home;
    }

    /// <summary>The distribution's install commands, shared with the Linux checks and services.</summary>
    public DistroPackages Packages { get; }

    public string Name => "Linux";

    public string Os => ProfileOs.Linux;

    public string DefaultDataRoot => Path.Combine(XdgFolder("XDG_DATA_HOME", ".local/share"), "ps5kitsune");

    public string SettingsDirectory => Path.Combine(XdgFolder("XDG_CONFIG_HOME", ".config"), "ps5kitsune");

    public string ExecutableSuffix => "";

    public string GameExecutableSuffix => ".elf";

    public string VulkanLoaderName => "libvulkan.so.1";

    public string VulkanInstallAction =>
        $"Install Vulkan: {Packages.CommandFor(LinuxPackage.VulkanLoader, LinuxPackage.MesaVulkanDrivers)}. With an NVIDIA GPU, install NVIDIA's driver instead of Mesa.";

    public ulong? GetCommitLimitBytes() => null;

    public IReadOnlyList<string> GetVulkanDriverManifests() => VulkanManifests.Find(VulkanManifests.Directories, "*.json");

    /// <summary>Linux checks give commands to run, never a fix to open.</summary>
    public void OpenFix(CheckFix fix) => throw new ArgumentOutOfRangeException(nameof(fix), fix, "Linux has no fix to open.");

    public void OpenPath(string path)
    {
        using (Process.Start(new ProcessStartInfo("xdg-open") { ArgumentList = { path } }))
        {
        }
    }

    // XDG Base Directory: a variable that is unset, empty or relative is ignored.
    private string XdgFolder(string variable, string fallback) =>
        _variable(variable) is { Length: > 0 } value && Path.IsPathRooted(value) ? value : Path.Combine(_home, fallback);
}
```

`src/PS5Kitsune.Platform.Linux/SystemPython.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Platform.Linux;

/// <summary>The distribution's python3, found on PATH. Nothing is downloaded: a missing Python stops with the command that installs it.</summary>
public sealed class SystemPython(DistroPackages packages) : IPythonRuntime
{
    /// <summary>The PATH searched: the process's own.</summary>
    internal string? SearchPath { get; init; } = Environment.GetEnvironmentVariable("PATH");

    public string StepName => "Find Python 3";

    /// <summary>The full path of python3; null when it is not on PATH.</summary>
    public string? Executable => PathSearch.Find("python3", SearchPath);

    public bool IsInstalled() => Executable is not null;

    public Task InstallAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default) =>
        IsInstalled() ? Task.CompletedTask : Task.FromException(new PythonMissingException(MissingMessage));

    public ProcessSpec CreateScriptSpec(string script, IEnumerable<string> arguments) =>
        PythonScripts.CreateSpec(Executable ?? throw new PythonMissingException(MissingMessage), script, arguments);

    private string MissingMessage => $"Python 3 is required: {packages.CommandFor(LinuxPackage.Python)}";
}
```

`src/PS5Kitsune.Platform.Linux/SystemLavapipe.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Launch;

namespace PS5Kitsune.Platform.Linux;

/// <summary>Lavapipe from the distribution's Mesa: nothing to download, its manifest (lvp_icd*.json) is found in the Vulkan driver folders.</summary>
public sealed class SystemLavapipe(DistroPackages packages) : ILavapipe
{
    internal IReadOnlyList<string> ManifestDirectories { get; init; } = VulkanManifests.Directories;

    /// <summary>The manifest's path; empty when Mesa's Lavapipe is not installed.</summary>
    public string Manifest => Find() ?? "";

    public bool IsInstalled() => Find() is not null;

    public Task InstallAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default) =>
        IsInstalled()
            ? Task.CompletedTask
            : Task.FromException(new LavapipeUnavailableException($"Lavapipe is not installed: {packages.CommandFor(LinuxPackage.MesaVulkanDrivers)}"));

    private string? Find() => VulkanManifests.Find(ManifestDirectories, "lvp_icd*.json").FirstOrDefault();
}
```

`LaunchPreparation` lets a `LavapipeUnavailableException` from `InstallAsync` through unchanged (its download-failure filter does not match it), so `run` prints the command.

- [ ] **Step 6: Build and run every test on Windows and Linux**

Expected: 0 warnings, all pass; the Linux-only tests skip on Windows.

- [ ] **Step 7: Commit**

```bash
git add -A src/PS5Kitsune.Platform.Linux tests/PS5Kitsune.Platform.Linux.Tests PS5Kitsune.slnx
git commit -m "Add the Linux platform: XDG folders, package commands, system Python and Lavapipe"
```

---

### Task 5: Platform — pick Windows or Linux at startup

**Files:**
- Create: `src/PS5Kitsune.Platform/PS5Kitsune.Platform.csproj`, `src/PS5Kitsune.Platform/PlatformServices.cs`
- Delete: `src/PS5Kitsune.Platform.Windows/PlatformServices.cs`
- Modify: `src/PS5Kitsune.Cli/PS5Kitsune.Cli.csproj`, `src/PS5Kitsune.Cli/CommandHost.cs`, `src/PS5Kitsune.App/PS5Kitsune.App.csproj`, `src/PS5Kitsune.App/App.axaml.cs`, `src/PS5Kitsune.App/Sessions/RootSession.cs`, `PS5Kitsune.slnx`, `tests/PS5Kitsune.Platform.Windows.Tests/PS5Kitsune.Platform.Windows.Tests.csproj`, `tests/PS5Kitsune.Platform.Windows.Tests/PlatformServicesTests.cs`, `tests/PS5Kitsune.Platform.Linux.Tests/PS5Kitsune.Platform.Linux.Tests.csproj`
- Test: `tests/PS5Kitsune.Platform.Linux.Tests/LinuxServicesTests.cs` (new)

**Interfaces:**
- Consumes: `LinuxPlatform`, `DistroPackages`, `OsRelease`, `SystemPython`, `SystemLavapipe` (Task 4); `WindowsPlatform`, `WindowsGameLauncher`, `WindowsChildProcessJob`.
- Produces: `PS5Kitsune.Platform.PlatformServices.Detect()` and `PlatformServices.AddPlatform(this IServiceCollection, IPlatform)` (same signatures as the Windows ones, new namespace `PS5Kitsune.Platform`). Task 6 adds the Linux game launcher to `AddPlatform`; Task 7 adds the Linux checks.

- [ ] **Step 1: Write the failing test**

Add `<ProjectReference Include="..\..\src\PS5Kitsune.Platform\PS5Kitsune.Platform.csproj" />` to both platform test projects (the Linux one keeps its Linux reference; the Windows one keeps its Windows reference).

`tests/PS5Kitsune.Platform.Linux.Tests/LinuxServicesTests.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Launch;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Platform.Linux.Tests;

public sealed class LinuxServicesTests : IDisposable
{
    private readonly PS5Kitsune.Core.Tests.TempDirectory _temp = new();

    private ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPlatform(new LinuxPlatform(new DistroPackages(OsRelease.Parse("ID=ubuntu\n"))))
            .AddPS5KitsuneCore(new DataLayout(_temp.Path), ProfileLoader.LoadEmbedded(ProfileOs.Linux));
        return services.BuildServiceProvider();
    }

    [Fact]
    public void The_linux_platform_brings_the_system_python_and_lavapipe()
    {
        using var provider = Build();

        Assert.IsType<SystemPython>(provider.GetRequiredService<IPythonRuntime>());
        Assert.IsType<SystemLavapipe>(provider.GetRequiredService<ILavapipe>());
        Assert.IsType<LinuxPlatform>(provider.GetRequiredService<IPlatform>());
    }

    [Fact]
    public void The_linux_profile_offers_release_mode_only()
    {
        using var provider = Build();
        var installer = provider.GetRequiredService<AnyPS5Installer>();

        Assert.True(installer.Supports(AnyPS5Mode.Release));
        Assert.False(installer.Supports(AnyPS5Mode.Build));
        Assert.Equal("Find Python 3", installer.CreateSteps(AnyPS5Mode.Release)[0].Name);
    }

    public void Dispose() => _temp.Dispose();
}
```

In `tests/PS5Kitsune.Platform.Windows.Tests/PlatformServicesTests.cs`, add `using PS5Kitsune.Platform;` (the test body does not change).

- [ ] **Step 2: Run to see it fail to build**

Expected: `PS5Kitsune.Platform` does not exist.

- [ ] **Step 3: Create the project and move `PlatformServices`**

`src/PS5Kitsune.Platform/PS5Kitsune.Platform.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!-- The hosts' single entry to the OS: picks the Windows or Linux platform at startup and registers its services. -->

  <ItemGroup>
    <ProjectReference Include="..\PS5Kitsune.Core\PS5Kitsune.Core.csproj" />
    <ProjectReference Include="..\PS5Kitsune.Platform.Windows\PS5Kitsune.Platform.Windows.csproj" />
    <ProjectReference Include="..\PS5Kitsune.Platform.Linux\PS5Kitsune.Platform.Linux.csproj" />
  </ItemGroup>

</Project>
```

`src/PS5Kitsune.Platform/PlatformServices.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Launch;
using PS5Kitsune.Core.Tools;
using PS5Kitsune.Platform.Linux;
using PS5Kitsune.Platform.Windows;

namespace PS5Kitsune.Platform;

public static class PlatformServices
{
    /// <summary>The implementation of <see cref="IPlatform"/> for the running OS.</summary>
    /// <exception cref="PlatformNotSupportedException">Neither Windows nor Linux.</exception>
    public static IPlatform Detect()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsPlatform();
        }

        if (OperatingSystem.IsLinux())
        {
            return new LinuxPlatform(new DistroPackages(OsRelease.Read()));
        }

        throw new PlatformNotSupportedException("PS5Kitsune runs on Windows and Linux.");
    }

    /// <summary>Registers the platform and its services. Call it before AddPS5KitsuneCore, which keeps what is registered here.</summary>
    public static IServiceCollection AddPlatform(this IServiceCollection services, IPlatform platform)
    {
        services.AddSingleton(platform);
        // By the platform's type, not the running OS, so a test can build the Linux services anywhere.
        if (platform is LinuxPlatform linux)
        {
            services.AddSingleton(linux.Packages);
            services.AddSingleton<SystemPython>();
            services.AddSingleton<IPythonRuntime>(provider => provider.GetRequiredService<SystemPython>());
            services.AddSingleton<ILavapipe, SystemLavapipe>();
        }
        else if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<IGameLauncher, WindowsGameLauncher>();
            services.AddSingleton<IChildProcessJob, WindowsChildProcessJob>();
        }

        return services;
    }
}
```

Delete `src/PS5Kitsune.Platform.Windows/PlatformServices.cs` (`git rm`).

Add `<Project Path="src/PS5Kitsune.Platform/PS5Kitsune.Platform.csproj" />` to `PS5Kitsune.slnx` (before the Windows platform line).

- [ ] **Step 4: Point the hosts at it**

- `src/PS5Kitsune.Cli/PS5Kitsune.Cli.csproj` and `src/PS5Kitsune.App/PS5Kitsune.App.csproj`: replace the `PS5Kitsune.Platform.Windows` project reference with `<ProjectReference Include="..\PS5Kitsune.Platform\PS5Kitsune.Platform.csproj" />`.
- `src/PS5Kitsune.Cli/CommandHost.cs`, `src/PS5Kitsune.App/App.axaml.cs`, `src/PS5Kitsune.App/Sessions/RootSession.cs`: `using PS5Kitsune.Platform.Windows;` becomes `using PS5Kitsune.Platform;`. If a file still uses a Windows type directly after that (`grep -rn "Platform.Windows" src/PS5Kitsune.App src/PS5Kitsune.Cli`), keep both usings.
- If `tests/PS5Kitsune.App.Tests` references `PS5Kitsune.Platform.Windows` (`grep -n Platform tests/PS5Kitsune.App.Tests/*.csproj`), leave it: its types still exist.

- [ ] **Step 5: Build and run every test on Windows and Linux**

Expected: 0 warnings, all pass.

- [ ] **Step 6: Smoke `check` in WSL**

```bash
wsl -d Ubuntu-24.04 -e bash -c '. ~/ps5k-env.sh && src/PS5Kitsune.Cli/bin/Release/net10.0/ps5kitsune check --root /tmp/ps5k-smoke; echo exit=$?' 2>&1 | tr -d '\0'
```

Expected: the checks print; Vulkan finds `llvmpipe`; the commit limit says "Not applicable on this system."; the data folder is `/tmp/ps5k-smoke`; exit code 0 (warnings do not fail `check`). Record the output in the task report.

- [ ] **Step 7: Commit**

```bash
git add -A src tests PS5Kitsune.slnx
git commit -m "Pick the Windows or Linux platform at startup through PS5Kitsune.Platform"
```

---

### Task 6: Platform.Linux — the game launcher

**Files:**
- Create: `src/PS5Kitsune.Platform.Linux/LinuxGameLauncher.cs`, `src/PS5Kitsune.Platform.Linux/PosixSpawn.cs`, `src/PS5Kitsune.Platform.Linux/ProcStat.cs`
- Modify: `src/PS5Kitsune.Platform/PlatformServices.cs`
- Test: `tests/PS5Kitsune.Platform.Linux.Tests/ProcStatTests.cs`, `tests/PS5Kitsune.Platform.Linux.Tests/LinuxGameLauncherTests.cs`, `tests/PS5Kitsune.Platform.Linux.Tests/LinuxServicesTests.cs`

**Interfaces:**
- Consumes: `IGameLauncher`, `ILaunchedGame`, `GameLaunchRequest`, `RunningGame` (`PS5Kitsune.Core.Abstractions`).
- Produces: `LinuxGameLauncher(ILogger<LinuxGameLauncher> logger)` : `IGameLauncher`, with `internal TimeSpan StopGrace { init; }` (5 s) and `internal TimeSpan AttachPoll { init; }` (500 ms); `ProcStat` (internal): `Parse(string statLine)`, `Read(int pid)`, `StartTime(ulong startTicks, long bootTimeSeconds, long ticksPerSecond)`, `BootTime(string procStatText)`.

Behaviour (spec, "Run"):
- `Launch`: `posix_spawn` with `POSIX_SPAWN_SETSID`; file actions: `/dev/null` on fd 0, the log file on fd 1 (`O_WRONLY|O_CREAT|O_TRUNC|O_APPEND`, mode 0644), fd 2 = dup of fd 1, then `chdir` to the working directory (`posix_spawn_file_actions_addchdir_np`, glibc 2.29+); environment = inherited + `request.Environment`. The newest 9 logs are kept, as on Windows (`KeptLogs = 10`).
- A thread waits with `waitpid` as soon as the game starts, so the exit is never lost and no zombie stays. Exit code: `WEXITSTATUS` when the game exited, `128 + signal` when a signal ended it, `-1` when `waitpid` fails.
- `Attach`: null when `/proc/<pid>/stat` is gone, the process is a zombie (`Z`), its start time differs from the record, or `/proc/<pid>/exe` is not the record's executable. An attached game is polled every `AttachPoll`; its exit code is `-1`.
- `Stop`: `SIGTERM` to the game's process group (the game is a session leader: its group ID is its PID), wait `StopGrace`, then `SIGKILL`; completes once the game has exited.

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.Platform.Linux.Tests/ProcStatTests.cs` (pure text: runs on both OSes):

```csharp
namespace PS5Kitsune.Platform.Linux.Tests;

public sealed class ProcStatTests
{
    // Field 3 is the state, 6 the session, 22 the start time in clock ticks since boot.
    private const string Sleep = "4259 (sleep) S 4167 4259 4259 0 -1 4194304 101 0 0 0 0 0 0 0 20 0 1 0 123456 2367488 224 18446744073709551615 1 1 0 0 0 0 0 0 0 0 0 0 17 3 0 0 0 0 0";

    [Fact]
    public void Reads_the_state_the_session_and_the_start_ticks()
    {
        var stat = ProcStat.Parse(Sleep);

        Assert.NotNull(stat);
        Assert.Equal('S', stat.Value.State);
        Assert.Equal(4259, stat.Value.Session);
        Assert.Equal(123456UL, stat.Value.StartTicks);
    }

    [Fact]
    public void A_name_with_spaces_and_parentheses_is_skipped_whole()
    {
        var stat = ProcStat.Parse(Sleep.Replace("(sleep)", "(my (game) x)", StringComparison.Ordinal));

        Assert.Equal('S', stat!.Value.State);
        Assert.Equal(123456UL, stat.Value.StartTicks);
    }

    [Fact]
    public void A_truncated_line_is_not_a_stat() => Assert.Null(ProcStat.Parse("4259 (sleep) S 4167"));

    [Fact]
    public void The_start_time_is_boot_time_plus_ticks()
    {
        var bootTime = ProcStat.BootTime("cpu  1 2 3\nbtime 1761800000\nprocesses 12\n");

        Assert.Equal(1761800000, bootTime);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1761800000).AddMilliseconds(1234560), ProcStat.StartTime(123456, bootTime, 100));
    }
}
```

`tests/PS5Kitsune.Platform.Linux.Tests/LinuxGameLauncherTests.cs`:

```csharp
using System.ComponentModel;
using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Platform.Linux.Tests;

public sealed class LinuxGameLauncherTests : IDisposable
{
    private readonly PS5Kitsune.Core.Tests.TempDirectory _temp = new();

    private readonly LinuxGameLauncher _launcher = new(NullLogger<LinuxGameLauncher>.Instance)
    {
        StopGrace = TimeSpan.FromMilliseconds(300),
        AttachPoll = TimeSpan.FromMilliseconds(50),
    };

    private string Logs => _temp.Combine("logs");

    private ILaunchedGame Shell(string script, IReadOnlyDictionary<string, string>? environment = null) =>
        _launcher.Launch(new GameLaunchRequest("/bin/sh", _temp.Path, Logs)
        {
            Arguments = ["-c", script],
            Environment = environment ?? new Dictionary<string, string>(),
        });

    /// <summary>/usr/bin/sleep itself (not through sh), so /proc/&lt;pid&gt;/exe is /usr/bin/sleep from the start.</summary>
    private ILaunchedGame Sleep(int seconds) =>
        _launcher.Launch(new GameLaunchRequest("/usr/bin/sleep", _temp.Path, Logs) { Arguments = [seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)] });

    private static async Task<int> ExitOf(ILaunchedGame game) =>
        await game.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

    private static RunningGame RecordOf(ILaunchedGame game, string executable) =>
        new(RunningGame.CurrentSchemaVersion, game.ProcessId, game.StartedAt, executable, game.LogFile);

    [Fact]
    public async Task Writes_stdout_and_stderr_to_the_log_and_returns_the_exit_code()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        using var game = Shell("echo out; echo err >&2; exit 3");

        Assert.Equal(3, await ExitOf(game));
        var log = await File.ReadAllTextAsync(game.LogFile, TestContext.Current.CancellationToken);
        Assert.Contains("out", log, StringComparison.Ordinal);
        Assert.Contains("err", log, StringComparison.Ordinal);
        Assert.Equal(Logs, Path.GetDirectoryName(game.LogFile));
    }

    [Fact]
    public async Task Runs_in_the_working_directory_with_the_added_variables()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        using var game = Shell("pwd; echo \"v=$PS5K_TEST\"; echo \"path=$PATH\"", new Dictionary<string, string> { ["PS5K_TEST"] = "on" });

        Assert.Equal(0, await ExitOf(game));
        var log = await File.ReadAllTextAsync(game.LogFile, TestContext.Current.CancellationToken);
        Assert.Contains(_temp.Path, log, StringComparison.Ordinal);
        Assert.Contains("v=on", log, StringComparison.Ordinal);
        Assert.DoesNotContain("path=\n", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_game_leads_its_own_session()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        using var game = Shell("sleep 30");
        try
        {
            Assert.Equal(game.ProcessId, ProcStat.Read(game.ProcessId)!.Value.Session);
        }
        finally
        {
            await game.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task A_signal_gives_128_plus_its_number()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        using var game = Shell("kill -9 $$");

        Assert.Equal(137, await ExitOf(game));
    }

    [Fact]
    public async Task Stop_ends_a_game_with_sigterm()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        using var game = Sleep(30);

        await game.StopAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(128 + 15, await ExitOf(game));
    }

    [Fact]
    public async Task Stop_kills_a_game_that_ignores_sigterm()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        using var game = Shell("trap '' TERM; sleep 30");

        await game.StopAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(128 + 9, await ExitOf(game));
    }

    [Fact]
    public async Task Attach_finds_a_running_game_and_follows_it_until_it_exits()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        using var game = Sleep(1);

        using var attached = _launcher.Attach(RecordOf(game, "/usr/bin/sleep"));

        Assert.NotNull(attached);
        Assert.Equal(game.ProcessId, attached.ProcessId);
        Assert.Equal(-1, await ExitOf(attached));
    }

    [Fact]
    public async Task Attach_refuses_another_start_time_or_executable()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        using var game = Sleep(30);
        try
        {
            Assert.Null(_launcher.Attach(RecordOf(game, "/usr/bin/sleep") with { StartedAt = game.StartedAt.AddSeconds(-5) }));
            Assert.Null(_launcher.Attach(RecordOf(game, "/usr/bin/true")));
        }
        finally
        {
            await game.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Attach_returns_null_once_the_game_has_exited()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        using var game = Shell("exit 0");
        await ExitOf(game);

        Assert.Null(_launcher.Attach(RecordOf(game, "/bin/sh")));
    }

    [Fact]
    public void A_missing_executable_throws()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");

        Assert.Throws<Win32Exception>(() => _launcher.Launch(new GameLaunchRequest(_temp.Combine("missing.elf"), _temp.Path, Logs)));
    }

    [Fact]
    public async Task Keeps_the_newest_logs()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        Directory.CreateDirectory(Logs);
        for (var day = 10; day < 25; day++)
        {
            await File.WriteAllTextAsync(Path.Combine(Logs, $"202601{day}-120000.log"), "", TestContext.Current.CancellationToken);
        }

        using var game = Shell("exit 0");
        await ExitOf(game);

        Assert.Equal(10, Directory.GetFiles(Logs, "*.log").Length);
    }

    public void Dispose() => _temp.Dispose();
}
```

`/usr/bin/sleep` is where Ubuntu (and CI's ubuntu-latest) installs `sleep`. The attach tests start it directly: started through `sh -c "exec sleep …"`, `/proc/<pid>/exe` would still be the shell for the few milliseconds before the `exec`. In the ignore-SIGTERM test, `trap '' TERM` is inherited by `sleep` (an ignored signal stays ignored across `exec`), so only SIGKILL ends the group.

Append to `LinuxServicesTests`:

```csharp
    [Fact]
    public void The_linux_platform_brings_its_game_launcher()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        using var provider = Build();

        Assert.IsType<LinuxGameLauncher>(provider.GetRequiredService<IGameLauncher>());
    }
```

- [ ] **Step 2: Run to see them fail to build**

Expected: `ProcStat` and `LinuxGameLauncher` do not exist.

- [ ] **Step 3: Implement `ProcStat`**

`src/PS5Kitsune.Platform.Linux/ProcStat.cs`:

```csharp
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PS5Kitsune.Platform.Linux;

/// <summary>The fields of /proc/&lt;pid&gt;/stat the launcher reads. See proc(5).</summary>
/// <param name="State">Field 3: R, S, D, Z (zombie), T…</param>
/// <param name="Session">Field 6: the session ID.</param>
/// <param name="StartTicks">Field 22: when the process started, in clock ticks since boot.</param>
internal readonly partial record struct ProcStat(char State, int Session, ulong StartTicks)
{
    private const int ClockTicksName = 2; // _SC_CLK_TCK

    /// <summary>Null when the line is not a stat line.</summary>
    public static ProcStat? Parse(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        // The name (field 2) is in parentheses and may contain spaces and parentheses: the fields start after the last ')'.
        var close = line.LastIndexOf(')');
        if (close < 0)
        {
            return null;
        }

        var fields = line[(close + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // fields[0] is field 3.
        return fields.Length > 19
            && fields[0].Length == 1
            && int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var session)
            && ulong.TryParse(fields[19], NumberStyles.None, CultureInfo.InvariantCulture, out var startTicks)
            ? new ProcStat(fields[0][0], session, startTicks)
            : null;
    }

    /// <summary>The process's stat; null when it does not exist or cannot be read.</summary>
    public static ProcStat? Read(int pid)
    {
        try
        {
            return Parse(File.ReadAllText($"/proc/{pid.ToString(CultureInfo.InvariantCulture)}/stat"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The "btime" line of /proc/stat: the boot time in seconds since the epoch.</summary>
    /// <exception cref="InvalidDataException">No btime line.</exception>
    public static long BootTime(string procStatText)
    {
        ArgumentNullException.ThrowIfNull(procStatText);
        foreach (var line in procStatText.Split('\n'))
        {
            if (line.StartsWith("btime ", StringComparison.Ordinal)
                && long.TryParse(line.AsSpan(6).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
            {
                return seconds;
            }
        }

        throw new InvalidDataException("/proc/stat has no btime line.");
    }

    public static DateTimeOffset StartTime(ulong startTicks, long bootTimeSeconds, long ticksPerSecond) =>
        DateTimeOffset.FromUnixTimeSeconds(bootTimeSeconds).AddTicks((long)(startTicks * (ulong)TimeSpan.TicksPerSecond / (ulong)ticksPerSecond));

    /// <summary>When <paramref name="pid"/> started, in UTC; null when it does not exist. Stable across reads, so it can be compared with running.json.</summary>
    [SupportedOSPlatform("linux")]
    public static DateTimeOffset? StartTimeOf(int pid) =>
        Read(pid) is { } stat ? StartTime(stat.StartTicks, BootTime(File.ReadAllText("/proc/stat")), SysConf(ClockTicksName)) : null;

    [LibraryImport("libc", EntryPoint = "sysconf")]
    private static partial long SysConf(int name);
}
```

(If `partial record struct` with `LibraryImport` does not compile, move `SysConf` into `PosixSpawn` as `internal static partial long SysConf(int name)` and call `PosixSpawn.SysConf` here.)

- [ ] **Step 4: Implement `PosixSpawn`**

`src/PS5Kitsune.Platform.Linux/PosixSpawn.cs`:

```csharp
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PS5Kitsune.Platform.Linux;

/// <summary>The libc calls the game launcher needs. posix_spawn and its helpers return an error number instead of setting errno.</summary>
[SupportedOSPlatform("linux")]
internal static unsafe partial class PosixSpawn
{
    private const int ReadOnly = 0;          // O_RDONLY
    private const int LogFlags = 0x641;      // O_WRONLY | O_CREAT | O_TRUNC | O_APPEND
    private const int LogMode = 0x1A4;       // 0644
    private const short SetSid = 0x80;       // POSIX_SPAWN_SETSID
    private const int StructSize = 512;      // glibc: posix_spawn_file_actions_t is 80 bytes, posix_spawnattr_t 336

    public const int SigKill = 9;
    public const int SigTerm = 15;
    private const int Interrupted = 4;       // EINTR
    public const int NoSuchProcess = 3;      // ESRCH

    /// <summary>Starts <paramref name="executable"/> in a new session, stdin on /dev/null, stdout and stderr on <paramref name="logFile"/>.</summary>
    /// <returns>The PID.</returns>
    /// <exception cref="Win32Exception">posix_spawn failed, for example ENOENT or EACCES.</exception>
    public static int Start(string executable, IReadOnlyList<string> arguments, string workingDirectory, string logFile, IEnumerable<string> environment)
    {
        var actions = (nint)NativeMemory.AllocZeroed(StructSize);
        var attributes = (nint)NativeMemory.AllocZeroed(StructSize);
        var owned = new List<nint>();
        try
        {
            Check(FileActionsInit(actions));
            try
            {
                Check(AddOpen(actions, 0, "/dev/null", ReadOnly, 0));
                Check(AddOpen(actions, 1, logFile, LogFlags, LogMode));
                Check(AddDup2(actions, 1, 2));
                Check(AddChdir(actions, workingDirectory));
                Check(AttributesInit(attributes));
                try
                {
                    Check(SetFlags(attributes, SetSid));
                    var argv = Block(owned, [executable, .. arguments]);
                    var envp = Block(owned, [.. environment]);
                    fixed (nint* argvPointer = argv)
                    fixed (nint* envpPointer = envp)
                    {
                        Check(Spawn(out var pid, executable, actions, attributes, argvPointer, envpPointer));
                        return pid;
                    }
                }
                finally
                {
                    AttributesDestroy(attributes);
                }
            }
            finally
            {
                FileActionsDestroy(actions);
            }
        }
        finally
        {
            foreach (var pointer in owned)
            {
                Marshal.FreeCoTaskMem(pointer);
            }

            NativeMemory.Free((void*)actions);
            NativeMemory.Free((void*)attributes);
        }
    }

    /// <summary>Waits for a child; returns its exit code, 128 + the signal that ended it, or -1 when it cannot be waited for.</summary>
    public static int WaitForExit(int pid)
    {
        int status;
        int result;
        while ((result = WaitPid(pid, out status, 0)) == -1 && Marshal.GetLastPInvokeError() == Interrupted)
        {
        }

        if (result != pid)
        {
            return -1;
        }

        var signal = status & 0x7F;
        return signal == 0 ? (status >> 8) & 0xFF : 128 + signal;
    }

    /// <summary>Sends <paramref name="signal"/> to the process group <paramref name="group"/>; false when no process is left in it.</summary>
    /// <exception cref="Win32Exception">kill failed for another reason.</exception>
    public static bool SignalGroup(int group, int signal)
    {
        if (Kill(-group, signal) == 0)
        {
            return true;
        }

        var error = Marshal.GetLastPInvokeError();
        return error == NoSuchProcess ? false : throw new Win32Exception(error);
    }

    // A NULL-terminated array of UTF-8 strings; the strings are added to owned, which the caller frees.
    private static nint[] Block(List<nint> owned, IReadOnlyList<string> values)
    {
        var block = new nint[values.Count + 1];
        for (var index = 0; index < values.Count; index++)
        {
            block[index] = Marshal.StringToCoTaskMemUTF8(values[index]);
            owned.Add(block[index]);
        }

        return block;
    }

    private static void Check(int error)
    {
        if (error != 0)
        {
            throw new Win32Exception(error);
        }
    }

    [LibraryImport("libc", EntryPoint = "posix_spawn", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Spawn(out int pid, string path, nint fileActions, nint attributes, nint* argv, nint* envp);

    [LibraryImport("libc", EntryPoint = "posix_spawn_file_actions_init")]
    private static partial int FileActionsInit(nint fileActions);

    [LibraryImport("libc", EntryPoint = "posix_spawn_file_actions_destroy")]
    private static partial int FileActionsDestroy(nint fileActions);

    [LibraryImport("libc", EntryPoint = "posix_spawn_file_actions_addopen", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int AddOpen(nint fileActions, int descriptor, string path, int flags, int mode);

    [LibraryImport("libc", EntryPoint = "posix_spawn_file_actions_adddup2")]
    private static partial int AddDup2(nint fileActions, int descriptor, int newDescriptor);

    [LibraryImport("libc", EntryPoint = "posix_spawn_file_actions_addchdir_np", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int AddChdir(nint fileActions, string path);

    [LibraryImport("libc", EntryPoint = "posix_spawnattr_init")]
    private static partial int AttributesInit(nint attributes);

    [LibraryImport("libc", EntryPoint = "posix_spawnattr_destroy")]
    private static partial int AttributesDestroy(nint attributes);

    [LibraryImport("libc", EntryPoint = "posix_spawnattr_setflags")]
    private static partial int SetFlags(nint attributes, short flags);

    [LibraryImport("libc", EntryPoint = "waitpid", SetLastError = true)]
    private static partial int WaitPid(int pid, out int status, int options);

    [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static partial int Kill(int pid, int signal);
}
```

- [ ] **Step 5: Implement the launcher**

`src/PS5Kitsune.Platform.Linux/LinuxGameLauncher.cs`:

```csharp
using System.Collections;
using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Platform.Linux;

/// <summary>
/// Starts the game with posix_spawn in a session of its own, its stdout and stderr opened straight on the log file:
/// no pipe slows a verbose game down, and closing the terminal or the launcher does not end it. See the 8a spec, "Run".
/// </summary>
[SupportedOSPlatform("linux")]
public sealed partial class LinuxGameLauncher(ILogger<LinuxGameLauncher> logger) : IGameLauncher
{
    private const int KeptLogs = 10;

    /// <summary>How long Stop waits after SIGTERM before SIGKILL.</summary>
    internal TimeSpan StopGrace { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>How often an attached game, which is not our child, is checked for its exit.</summary>
    internal TimeSpan AttachPoll { get; init; } = TimeSpan.FromMilliseconds(500);

    public ILaunchedGame Launch(GameLaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Directory.CreateDirectory(request.LogDirectory);
        DeleteOldLogs(request.LogDirectory);
        var logFile = Path.Combine(request.LogDirectory, string.Create(CultureInfo.InvariantCulture, $"{DateTime.Now:yyyyMMdd-HHmmss}.log"));
        var pid = PosixSpawn.Start(request.Executable, request.Arguments, request.WorkingDirectory, logFile, EnvironmentOf(request.Environment));

        // The child cannot be reaped before the waiting thread starts, so its stat is readable (at worst as a zombie).
        var game = new SpawnedGame(pid, logFile, ProcStat.StartTimeOf(pid) ?? DateTimeOffset.UtcNow, StopGrace);
        var executable = request.Executable;
        LogLaunched(executable, pid, logFile);
        return game;
    }

    public ILaunchedGame? Attach(RunningGame record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!IsRecorded(record))
        {
            return null;
        }

        var executable = record.Executable;
        var pid = record.ProcessId;
        LogAttached(executable, pid);
        return new AttachedGame(record, StopGrace, AttachPoll);
    }

    /// <summary>The recorded process still runs and is the recorded game: same start time, same executable.</summary>
    internal static bool IsRecorded(RunningGame record)
    {
        try
        {
            return ProcStat.Read(record.ProcessId) is { State: not 'Z' }
                && ProcStat.StartTimeOf(record.ProcessId) == record.StartedAt
                && new FileInfo($"/proc/{record.ProcessId.ToString(CultureInfo.InvariantCulture)}/exe").LinkTarget is { } target
                && string.Equals(target, Path.GetFullPath(record.Executable), StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return false;
        }
    }

    private static List<string> EnvironmentOf(IReadOnlyDictionary<string, string> overrides)
    {
        var variables = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            variables[(string)entry.Key] = (string?)entry.Value ?? "";
        }

        foreach (var (name, value) in overrides)
        {
            variables[name] = value;
        }

        return [.. variables.Select(pair => $"{pair.Key}={pair.Value}")];
    }

    // Makes room for the new log by keeping the newest KeptLogs - 1. Names sort by date.
    private static void DeleteOldLogs(string directory)
    {
        foreach (var old in Directory.GetFiles(directory, "*.log").OrderDescending(StringComparer.Ordinal).Skip(KeptLogs - 1))
        {
            File.Delete(old);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Started {Executable} (PID {ProcessId}), output in {LogFile}")]
    private partial void LogLaunched(string executable, int processId, string logFile);

    [LoggerMessage(Level = LogLevel.Information, Message = "Found {Executable} running again (PID {ProcessId})")]
    private partial void LogAttached(string executable, int processId);

    /// <summary>Stop for both kinds: SIGTERM to the group, the grace period, then SIGKILL; completes once the game has exited.</summary>
    private static async Task StopAsync(ILaunchedGame game, TimeSpan grace, CancellationToken cancellationToken)
    {
        var exit = game.WaitForExitAsync(cancellationToken);
        if (PosixSpawn.SignalGroup(game.ProcessId, PosixSpawn.SigTerm))
        {
            var finished = await Task.WhenAny(exit, Task.Delay(grace, cancellationToken)).ConfigureAwait(false) == exit;

            // A cancelled delay also completes WhenAny: cancelling only abandons the wait, it never ends the game.
            cancellationToken.ThrowIfCancellationRequested();
            if (!finished)
            {
                PosixSpawn.SignalGroup(game.ProcessId, PosixSpawn.SigKill);
            }
        }

        await exit.ConfigureAwait(false);
    }

    /// <summary>A game this launcher started: a thread waits for it from the start, so its exit code is kept and no zombie stays.</summary>
    private sealed class SpawnedGame : ILaunchedGame
    {
        private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TimeSpan _stopGrace;

        public SpawnedGame(int processId, string logFile, DateTimeOffset startedAt, TimeSpan stopGrace)
        {
            ProcessId = processId;
            LogFile = logFile;
            StartedAt = startedAt;
            _stopGrace = stopGrace;
            new Thread(() => _exited.TrySetResult(PosixSpawn.WaitForExit(processId))) { IsBackground = true, Name = "Game exit" }.Start();
        }

        public int ProcessId { get; }

        public string LogFile { get; }

        public DateTimeOffset StartedAt { get; }

        public Task<int> WaitForExitAsync(CancellationToken cancellationToken = default) => _exited.Task.WaitAsync(cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken = default) => LinuxGameLauncher.StopAsync(this, _stopGrace, cancellationToken);

        // The waiting thread keeps running: it reaps the game whenever it exits. Nothing else to release.
        public void Dispose()
        {
        }
    }

    /// <summary>A game found again from running.json: not our child, so its exit is seen by polling /proc and its exit code is unknown (-1).</summary>
    private sealed class AttachedGame(RunningGame record, TimeSpan stopGrace, TimeSpan poll) : ILaunchedGame
    {
        public int ProcessId => record.ProcessId;

        public string LogFile => record.LogFile;

        public DateTimeOffset StartedAt => record.StartedAt;

        public async Task<int> WaitForExitAsync(CancellationToken cancellationToken = default)
        {
            while (IsRunning())
            {
                await Task.Delay(poll, cancellationToken).ConfigureAwait(false);
            }

            return -1;
        }

        public Task StopAsync(CancellationToken cancellationToken = default) => LinuxGameLauncher.StopAsync(this, stopGrace, cancellationToken);

        public void Dispose()
        {
        }

        // Gone, a zombie, or a later process reusing the PID: the game has exited.
        private bool IsRunning()
        {
            try
            {
                return ProcStat.Read(record.ProcessId) is { State: not 'Z' } && ProcStat.StartTimeOf(record.ProcessId) == record.StartedAt;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                return false;
            }
        }
    }
}
```

In `src/PS5Kitsune.Platform/PlatformServices.cs`, at the end of the `if (platform is LinuxPlatform linux)` block, add:

```csharp
            if (OperatingSystem.IsLinux())
            {
                services.AddSingleton<IGameLauncher, LinuxGameLauncher>();
            }
```

(The guard keeps CA1416 quiet and lets `LinuxServicesTests` build a Linux service set on Windows, where Core's `UnsupportedGameLauncher` stays.)

- [ ] **Step 6: Build and run every test on Windows and Linux**

Expected: 0 warnings, all pass. Run the launcher class alone in WSL a few times (`--filter-class "PS5Kitsune.Platform.Linux.Tests.LinuxGameLauncherTests"`, three runs) to check it is not flaky.

- [ ] **Step 7: Commit**

```bash
git add -A src tests
git commit -m "Launch games on Linux with posix_spawn in their own session"
```

---

### Task 7: Checks — Python and /dev/udmabuf on Linux

**Files:**
- Modify: `src/PS5Kitsune.Core/Checks/SystemChecker.cs`, `src/PS5Kitsune.Platform/PlatformServices.cs`
- Create: `src/PS5Kitsune.Platform.Linux/PythonCheck.cs`, `src/PS5Kitsune.Platform.Linux/UdmabufCheck.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Checks/SystemCheckerTests.cs`, `tests/PS5Kitsune.Platform.Linux.Tests/LinuxChecksTests.cs` (new), `tests/PS5Kitsune.Platform.Linux.Tests/LinuxServicesTests.cs`

**Interfaces:**
- Consumes: `SystemCheck`, `CheckResult`, `CheckStatus`, `SystemFacts` (Core); `SystemPython`, `DistroPackages`, `LinuxPackage` (Task 4); `TestFacts` (linked in Task 4).
- Produces: `PythonCheck(SystemPython python, DistroPackages packages)` (id `python`), `UdmabufCheck()` (id `udmabuf`; `internal string DevicePath { init; }`, `internal Func<string, bool> CanReadWrite { init; }`), both registered as `ISystemCheck` by `AddPlatform` on Linux. `SystemChecker` runs Core's checks (`SystemChecks.All`) first, in their order, then the others.

- [ ] **Step 1: Write the failing tests**

In `SystemCheckerTests.cs`, add (reusing the file's `CreateCollector()`):

```csharp
    [Fact]
    public async Task Checks_a_platform_adds_come_after_the_core_checks()
    {
        var added = new AddedCheck();
        var checker = new SystemChecker(CreateCollector(), [added, .. SystemChecks.All], NullLogger<SystemChecker>.Instance);

        var report = await checker.RunAsync(AnyPS5Mode.Release, TestContext.Current.CancellationToken);

        Assert.Equal(SystemChecks.All[0].Id, report.Results[0].Id);
        Assert.Equal("added", report.Results[^1].Id);
    }

    private sealed class AddedCheck : SystemCheck
    {
        public override string Id => "added";

        public override string Title => "Added";

        public override CheckResult Evaluate(SystemFacts facts) => Result(CheckStatus.Ok, "Fine.");
    }
```

`tests/PS5Kitsune.Platform.Linux.Tests/LinuxChecksTests.cs`:

```csharp
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Tests.Checks;

namespace PS5Kitsune.Platform.Linux.Tests;

public sealed class LinuxChecksTests : IDisposable
{
    private readonly PS5Kitsune.Core.Tests.TempDirectory _temp = new();

    private static readonly DistroPackages Ubuntu = new(OsRelease.Parse("ID=ubuntu\n"));

    private string Device()
    {
        var path = _temp.Combine("udmabuf");
        File.WriteAllText(path, "");
        return path;
    }

    [Fact]
    public void Udmabuf_with_access_is_ok()
    {
        var result = new UdmabufCheck { DevicePath = Device(), CanReadWrite = _ => true }.Evaluate(TestFacts.Healthy());

        Assert.Equal(CheckStatus.Ok, result.Status);
        Assert.Equal("Read-write access.", result.Summary);
    }

    [Fact]
    public void Udmabuf_without_access_warns_with_the_kvm_group_fix()
    {
        var result = new UdmabufCheck { DevicePath = Device(), CanReadWrite = _ => false }.Evaluate(TestFacts.Healthy());

        Assert.Equal(CheckStatus.Warning, result.Status);
        Assert.Equal("No read-write access to /dev/udmabuf; games copy shared memory instead of importing it, and some may fail.", result.Summary);
        Assert.Equal("sudo usermod -aG kvm $USER, then log out and in again.", result.Action);
    }

    [Fact]
    public void A_kernel_without_udmabuf_warns_without_a_fix()
    {
        var result = new UdmabufCheck { DevicePath = _temp.Combine("absent") }.Evaluate(TestFacts.Healthy());

        Assert.Equal(CheckStatus.Warning, result.Status);
        Assert.Equal("This kernel does not provide /dev/udmabuf; games copy shared memory instead of importing it, and some may fail.", result.Summary);
        Assert.Null(result.Action);
    }

    [Fact]
    public void Udmabuf_is_not_needed_when_every_gpu_uses_nvidia_proprietary()
    {
        var facts = TestFacts.Healthy() with { Vulkan = TestFacts.Probe(TestFacts.GeForce) };

        var result = new UdmabufCheck { DevicePath = _temp.Combine("absent") }.Evaluate(facts);

        Assert.Equal(CheckStatus.Info, result.Status);
        Assert.Equal("Not needed: every GPU uses NVIDIA's proprietary driver.", result.Summary);
    }

    [Fact]
    public void Udmabuf_is_checked_when_another_gpu_needs_it()
    {
        var facts = TestFacts.Healthy() with { Vulkan = TestFacts.Probe(TestFacts.GeForce, TestFacts.Radeon) };

        Assert.Equal(CheckStatus.Warning, new UdmabufCheck { DevicePath = _temp.Combine("absent") }.Evaluate(facts).Status);
    }

    [Fact]
    public void Python_found_is_ok_with_its_path()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        var python = _temp.Combine("python3");
        File.WriteAllText(python, "");
        File.SetUnixFileMode(python, UnixFileMode.UserRead | UnixFileMode.UserExecute);

        var result = new PythonCheck(new SystemPython(Ubuntu) { SearchPath = _temp.Path }, Ubuntu).Evaluate(TestFacts.Healthy());

        Assert.Equal(CheckStatus.Ok, result.Status);
        Assert.Equal($"{python}.", result.Summary);
    }

    [Fact]
    public void Python_missing_blocks_with_the_install_command()
    {
        var result = new PythonCheck(new SystemPython(Ubuntu) { SearchPath = "" }, Ubuntu).Evaluate(TestFacts.Healthy());

        Assert.Equal(CheckStatus.Blocking, result.Status);
        Assert.Equal("python3 was not found.", result.Summary);
        Assert.Equal("sudo apt install python3", result.Action);
    }

    public void Dispose() => _temp.Dispose();
}
```

Append to `LinuxServicesTests`:

```csharp
    [Fact]
    public void The_linux_platform_adds_the_python_and_udmabuf_checks()
    {
        using var provider = Build();

        var ids = provider.GetServices<PS5Kitsune.Core.Checks.ISystemCheck>().Select(check => check.Id).ToList();

        Assert.Contains("python", ids);
        Assert.Contains("udmabuf", ids);
    }
```

- [ ] **Step 2: Run to see them fail**

Expected: build errors (`UdmabufCheck`, `PythonCheck` missing); after Step 3 alone, the ordering test fails (the added check comes first).

- [ ] **Step 3: Order the checks**

In `SystemChecker.RunAsync`, replace `var results = checks.Select(check => check.Evaluate(facts)).ToList();` with:

```csharp
        // Core's checks first, in their order; then those the platform added (registered before Core's, so first in the sequence).
        var results = checks.OrderBy(check => SystemChecks.All.Contains(check) ? 0 : 1).Select(check => check.Evaluate(facts)).ToList();
```

(`OrderBy` is stable, so each group keeps its order.)

- [ ] **Step 4: Add the Linux checks**

`src/PS5Kitsune.Platform.Linux/PythonCheck.cs`:

```csharp
using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.Platform.Linux;

/// <summary>AnyPS5's scripts need the system python3 (the import audit, unwrapping).</summary>
public sealed class PythonCheck(SystemPython python, DistroPackages packages) : SystemCheck
{
    public override string Id => "python";

    public override string Title => "Python 3";

    public override CheckResult Evaluate(SystemFacts facts) =>
        python.Executable is { } path
            ? Result(CheckStatus.Ok, $"{path}.")
            : Result(CheckStatus.Blocking, "python3 was not found.", packages.CommandFor(LinuxPackage.Python));
}
```

`src/PS5Kitsune.Platform.Linux/UdmabufCheck.cs`:

```csharp
using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.Platform.Linux;

/// <summary>
/// AnyPS5 imports shared memory through /dev/udmabuf with every Vulkan driver but NVIDIA's proprietary one. Without it the memory is copied,
/// and some games then fail; so a warning, never blocking. See the AnyPS5 USAGE.md and the 8a spec, "Checks on Linux".
/// </summary>
public sealed class UdmabufCheck : SystemCheck
{
    private const string NvidiaProprietary = "NvidiaProprietary";

    private const string Copied = "games copy shared memory instead of importing it, and some may fail.";

    internal string DevicePath { get; init; } = "/dev/udmabuf";

    internal Func<string, bool> CanReadWrite { get; init; } = OpensForReadWrite;

    public override string Id => "udmabuf";

    public override string Title => "/dev/udmabuf";

    public override CheckResult Evaluate(SystemFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.Vulkan.Report?.Gpus is { Count: > 0 } gpus && gpus.All(gpu => gpu.DriverId == NvidiaProprietary))
        {
            return Result(CheckStatus.Info, "Not needed: every GPU uses NVIDIA's proprietary driver.");
        }

        if (!File.Exists(DevicePath))
        {
            return Result(CheckStatus.Warning, $"This kernel does not provide /dev/udmabuf; {Copied}");
        }

        return CanReadWrite(DevicePath)
            ? Result(CheckStatus.Ok, "Read-write access.")
            : Result(CheckStatus.Warning, $"No read-write access to /dev/udmabuf; {Copied}", "sudo usermod -aG kvm $USER, then log out and in again.");
    }

    private static bool OpensForReadWrite(string path)
    {
        try
        {
            using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.ReadWrite);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
```

In `PlatformServices.AddPlatform`, in the `if (platform is LinuxPlatform linux)` block, add:

```csharp
            services.AddSingleton<ISystemCheck, PythonCheck>();
            services.AddSingleton<ISystemCheck, UdmabufCheck>();
```

(with `using PS5Kitsune.Core.Checks;`).

- [ ] **Step 5: Build and run every test on Windows and Linux**

Expected: 0 warnings, all pass.

- [ ] **Step 6: Smoke `check` in WSL**

Same command as Task 5, Step 6. Expected, after the Core checks: `Python 3` OK with `/usr/bin/python3.`, and `/dev/udmabuf` Warning "This kernel does not provide /dev/udmabuf; …". Record the output in the task report.

- [ ] **Step 7: Commit**

```bash
git add -A src tests
git commit -m "Check Python 3 and /dev/udmabuf on Linux"
```

---

### Task 8: Diagnosis — the Linux errors

**Files:**
- Modify: `src/PS5Kitsune.Core/Diagnostics/DiagnosisIds.cs`, `src/PS5Kitsune.Core/Diagnostics/DiagnosisTexts.cs`, `src/PS5Kitsune.Core/Profiles/default-profile.linux.json`
- Test: `tests/PS5Kitsune.Core.Tests/Diagnostics/CrashDiagnoserTests.cs`, `tests/PS5Kitsune.Core.Tests/Diagnostics/DiagnosisTextsTests.cs`

**Interfaces:**
- Consumes: the Linux profile (Task 1), `CrashDiagnoser`, `DiagnosisTexts` (existing).
- Produces: `DiagnosisIds.VulkanMissing` (`vulkan-missing`), `DiagnosisIds.SharedLibraryMissing` (`shared-library-missing`, capture `library`), `DiagnosisIds.BdaAccess` (`bda-access`), `DiagnosisIds.UdmabufAccess` (`udmabuf-access`).

Texts, verbatim (title / explanation; no button, so `DiagnosisAction.None` and no "Fix:" line):

| Id | Title | Explanation |
| --- | --- | --- |
| `vulkan-missing` | Vulkan could not be loaded | AnyPS5 could not load the Vulkan loader or a Vulkan driver. Run ps5kitsune check: it shows the command that installs them. |
| `shared-library-missing` | A system library is missing | {library} is not installed. Install the package that provides it with your distribution's package manager. ("A library" when the capture is missing) |
| `bda-access` | The GPU could not reach some memory | GPU stores failed (BDA access failed), usually because /dev/udmabuf could not be used. Run ps5kitsune check: it tells whether this kernel provides /dev/udmabuf and whether you can open it. |
| `udmabuf-access` | No access to /dev/udmabuf | AnyPS5 could not open /dev/udmabuf, so it copied shared memory instead of importing it. Give your user access: sudo usermod -aG kvm $USER, then log out and in again. |

Rule order in the Linux profile: the seven shared rules first, then `vulkan-missing`, `shared-library-missing`, `bda-access`, `udmabuf-access`. The udmabuf line is printed at every start where access fails, even when the game then works, so it comes last: a more specific cause wins.

- [ ] **Step 1: Write the failing tests**

In `CrashDiagnoserTests.cs`, add:

```csharp
    private readonly CrashDiagnoser _linux = new(ProfileLoader.LoadEmbedded(ProfileOs.Linux), NullLogger<CrashDiagnoser>.Instance);

    /// <summary>Linux log text from AnyPS5 v0.1.3 and the dynamic loader, the rule id it must give, and its "library" capture.</summary>
    public static TheoryData<string, string, string?> LinuxSamples => new()
    {
        { "[gpu] Vulkan loader: libvulkan.so.1: cannot open shared object file: No such file or directory\n", DiagnosisIds.VulkanMissing, null },
        { "./PPSA99666.elf: error while loading shared libraries: libpulse.so.0: cannot open shared object file: No such file or directory\n", DiagnosisIds.SharedLibraryMissing, "libpulse.so.0" },
        { "[gpu] open /dev/udmabuf: Permission denied; shared direct memory is copied instead of imported, and GPU stores through FLAT/GLOBAL addresses do not reach it\nBDA access failed: address=0x7f0012345678\n", DiagnosisIds.BdaAccess, null },
        { "[gpu] open /dev/udmabuf: Permission denied; shared direct memory is copied instead of imported, and GPU stores through FLAT/GLOBAL addresses do not reach it\n", DiagnosisIds.UdmabufAccess, null },
        { "[gpu] open /dev/udmabuf: Permission denied; shared direct memory is copied instead of imported\nNgs2: reverb at 96000 Hz is not implemented\n", DiagnosisIds.NotImplemented, null },
        { "Vulkan: buffer device address and shaderInt64 are required\n", DiagnosisIds.GpuFeatures, null },
    };

    [Theory]
    [MemberData(nameof(LinuxSamples))]
    public async Task Each_linux_error_gives_its_rule(string log, string id, string? library)
    {
        var diagnosis = await _linux.DiagnoseAsync(Log("[debug:0] launcher: start\n" + log), 1, Token);

        Assert.NotNull(diagnosis);
        Assert.Equal(id, diagnosis.Id);
        Assert.Equal(library, diagnosis.Capture("library"));
    }

    [Fact]
    public async Task The_windows_dll_rules_are_not_in_the_linux_profile()
    {
        var diagnosis = await _linux.DiagnoseAsync(
            Log("Failed to load module: /g/libs/libSceAudioOut.prx\nGetLastError: 126 - The specified module could not be found.\n"), 1, Token);

        Assert.Equal(DiagnosisIds.Unexplained, diagnosis!.Id);
    }
```

In `DiagnosisTextsTests.cs`, add (reuse the file's way of building a `Diagnosis`; the record is `new Diagnosis(id, captures, line, lineNumber, exitCode, logPath)`):

```csharp
    [Theory]
    [InlineData(DiagnosisIds.VulkanMissing, "Vulkan could not be loaded", "AnyPS5 could not load the Vulkan loader or a Vulkan driver. Run ps5kitsune check: it shows the command that installs them.")]
    [InlineData(DiagnosisIds.BdaAccess, "The GPU could not reach some memory", "GPU stores failed (BDA access failed), usually because /dev/udmabuf could not be used. Run ps5kitsune check: it tells whether this kernel provides /dev/udmabuf and whether you can open it.")]
    [InlineData(DiagnosisIds.UdmabufAccess, "No access to /dev/udmabuf", "AnyPS5 could not open /dev/udmabuf, so it copied shared memory instead of importing it. Give your user access: sudo usermod -aG kvm $USER, then log out and in again.")]
    public void The_linux_diagnoses_explain_without_a_button(string id, string title, string explanation)
    {
        var text = DiagnosisTexts.For(new Diagnosis(id, new Dictionary<string, string>(), "line", 2, 1, "/g/x.log"), new GameSettings());

        Assert.Equal(title, text.Title);
        Assert.Equal(explanation, text.Explanation);
        Assert.Equal(DiagnosisAction.None, text.Action);
        Assert.Null(text.ActionText);
    }

    [Fact]
    public void A_missing_shared_library_is_named()
    {
        var diagnosis = new Diagnosis(DiagnosisIds.SharedLibraryMissing, new Dictionary<string, string> { ["library"] = "libpulse.so.0" }, "line", 2, 1, "/g/x.log");

        var text = DiagnosisTexts.For(diagnosis, new GameSettings());

        Assert.Equal("A system library is missing", text.Title);
        Assert.Equal("libpulse.so.0 is not installed. Install the package that provides it with your distribution's package manager.", text.Explanation);
        Assert.Equal(DiagnosisAction.None, text.Action);
    }
```

(If `GameSettings` has required members, build it the way the file's other tests do.)

- [ ] **Step 2: Run to see them fail**

Expected: build errors (the ids do not exist).

- [ ] **Step 3: Add the ids and texts**

In `DiagnosisIds.cs`, add the four constants with one-line doc comments ("No Vulkan loader or driver could be loaded (Linux).", "The dynamic loader could not find a system library (Linux).", "GPU stores to imported memory failed (Linux).", "/dev/udmabuf could not be opened (Linux).") and add them to `Known`.

In `DiagnosisTexts.For`, add before the `_ =>` arm:

```csharp
            DiagnosisIds.VulkanMissing => new(
                "Vulkan could not be loaded",
                "AnyPS5 could not load the Vulkan loader or a Vulkan driver. Run ps5kitsune check: it shows the command that installs them.",
                DiagnosisAction.None,
                null),
            DiagnosisIds.SharedLibraryMissing => new(
                "A system library is missing",
                $"{diagnosis.Capture("library") ?? "A library"} is not installed. Install the package that provides it with your distribution's package manager.",
                DiagnosisAction.None,
                null),
            DiagnosisIds.BdaAccess => new(
                "The GPU could not reach some memory",
                "GPU stores failed (BDA access failed), usually because /dev/udmabuf could not be used. Run ps5kitsune check: it tells whether this kernel provides /dev/udmabuf and whether you can open it.",
                DiagnosisAction.None,
                null),
            DiagnosisIds.UdmabufAccess => new(
                "No access to /dev/udmabuf",
                "AnyPS5 could not open /dev/udmabuf, so it copied shared memory instead of importing it. Give your user access: sudo usermod -aG kvm $USER, then log out and in again.",
                DiagnosisAction.None,
                null),
```

- [ ] **Step 4: Add the Linux rules to the Linux profile**

In `src/PS5Kitsune.Core/Profiles/default-profile.linux.json`, append to `diagnostics.rules`, after the `not-implemented` rule:

```json
      {
        "id": "vulkan-missing",
        "log": [ "Vulkan loader: " ]
      },
      {
        "id": "shared-library-missing",
        "log": [ "error while loading shared libraries: (?<library>[^:\\s]+)" ]
      },
      {
        "id": "bda-access",
        "log": [ "BDA access failed" ]
      },
      {
        "id": "udmabuf-access",
        "log": [ "open /dev/udmabuf: " ]
      }
```

(Match the file's indentation; JSON escapes the regex backslash as `\\s`.)

- [ ] **Step 5: Build and run every test on Windows and Linux**

Expected: 0 warnings, all pass (the Windows profile's rules are unchanged).

- [ ] **Step 6: Commit**

```bash
git add -A src tests
git commit -m "Diagnose the Linux errors: Vulkan, a missing library, udmabuf and BDA"
```

---

### Task 9: CI, docs, and a WSL run before the criterion

**Files:**
- Modify: `.github/workflows/ci.yml`, `docs/architecture.md`, `README.md`, `TODO.md`

- [ ] **Step 1: CI publishes the Linux CLI**

In `.github/workflows/ci.yml`, after the `dotnet test` step of the `build` job, add:

```yaml
      # What Linux ships: a self-contained CLI. A Windows-only dependency in it fails here.
      - name: Publish the Linux CLI
        if: runner.os == 'Linux'
        run: dotnet publish src/PS5Kitsune.Cli -c Release -r linux-x64 --self-contained -o "$RUNNER_TEMP/cli-linux"
```

Check it locally in WSL: `wsl -d Ubuntu-24.04 -e bash -c '~/ps5k-sync.sh && . ~/ps5k-env.sh && dotnet publish src/PS5Kitsune.Cli -c Release -r linux-x64 --self-contained -o /tmp/cli-linux 2>&1 | tail -3 && /tmp/cli-linux/ps5kitsune --help | head -5' 2>&1 | tr -d '\0'`. Expected: the publish succeeds with 0 warnings and `--help` lists the commands.

- [ ] **Step 2: Docs**

`docs/architecture.md`:
- **"Linux compatibility" → "What changes" table:**
  - Tool installation row, Linux: "Distribution packages (`apt`, `dnf`, `pacman`), read from `/etc/os-release`; PS5Kitsune shows the command to copy and never runs anything as root".
  - Python row, Linux: "System `python3`, found on `PATH`".
  - Lavapipe row, Linux: "System Mesa (`lvp_icd*.json` in `/usr/share/vulkan/icd.d` or `/etc/vulkan/icd.d`); nothing downloaded".
  - Memory and `/dev/udmabuf` rows: a Warning check (never blocking), skipped when every GPU uses NVIDIA's proprietary driver; fix `sudo usermod -aG kvm $USER` then logging in again.
  - Display row: "`SDL_VIDEODRIVER` if a game needs it; SDL picks Wayland, then X11, and the game inherits the environment".
  - Conversion row: "`--registry`, `.elf` output; execute bits ensured before each launch".
- **"Rules from V1 onwards":** replace "The `Platform.Linux` project is only created at the Linux milestone." with "`PS5Kitsune.Platform` picks `Platform.Windows` or `Platform.Linux` at startup; the CLI and the app reference only it."
- **"Solution architecture" tree:** `PS5Kitsune.Platform.Linux/` loses "(later)" and reads "XDG paths, package commands, posix_spawn launcher, udmabuf check"; add `PS5Kitsune.Platform/` ("picks the platform at startup") and `tests/PS5Kitsune.Platform.Linux.Tests/`. The sentence "The Linux project is planned, but not before V3." is removed.
- **"AnyPS5 profiles":** say there is one embedded profile per OS (`default-profile.windows.json`, `default-profile.linux.json`), schema 2 with `os` and `relinker.args`, and that an override for the other OS is ignored. Update the JSON example to `"schemaVersion": 2`, `"os": "windows"` and `"args"`.
- **Roadmap:** milestone 8 lists 8a (this spec's link) and 8b (the app on Linux, `linux-x64` packaging); build mode on Linux is later.

`README.md`:
- The status paragraph says milestone 8a (the Linux command-line tool, release mode) is in progress.
- Roadmap line 8 becomes `- [ ] **8. Linux** — the CLI in release mode (8a), then the app and packaging (8b)`.
- Add a "Linux" section after the CLI examples:

```markdown
### Linux

The command-line tool runs on Linux in release mode (build mode is Windows-only for now). It needs Python 3, the Vulkan loader and a Vulkan driver from the distribution; `ps5kitsune check` names the missing ones and prints the command that installs them (apt, dnf or pacman). Games import shared memory through `/dev/udmabuf` (except with NVIDIA's proprietary driver): add yourself to the `kvm` group with `sudo usermod -aG kvm $USER`, then log out and in again. Settings live in `~/.config/ps5kitsune`, data in `~/.local/share/ps5kitsune`.
```

`TODO.md`, append:

```markdown
## Build mode on Linux

Milestone 8a supports release mode only on Linux. Build mode needs the system GCC and the build dependency packages listed in AnyPS5's BUILD.md, an install command per distribution, and a child-process job: Linux has no job object, so git and CMake started by a PS5Kitsune that is killed outright keep running (`IChildProcessJob` is a no-op there).

## Exit code of a reattached game on Linux

A game found again from `running.json` is not PS5Kitsune's child on Linux, so its exit code cannot be read: `WaitForExitAsync` returns -1. The app (milestone 8b) should show "exited" without a code, and skip the diagnosis by exit code, for such a game.

## A clock-proof identity for a running game on Linux

On Linux the launcher recognises a running game by its executable and its start time, computed from the boot time in `/proc/stat`. That boot time moves when the wall clock is stepped, so the start times are compared with a 5-second tolerance. A big clock jump (WSL resyncing after the host slept) still loses the game. A robust fix records an identity that ignores the clock in `running.json`: `/proc/sys/kernel/random/boot_id` plus the process's start ticks (`/proc/<pid>/stat` field 22). Worth doing with the app (8b), which follows games for longer.
```

- [ ] **Step 3: WSL run before the criterion**

Run the release path end to end in WSL with a throwaway root, and record the output in the task report (this is a rehearsal of the criterion's first part; it does not replace it):

```bash
wsl -d Ubuntu-24.04 -e bash -c '. ~/ps5k-env.sh && cli=src/PS5Kitsune.Cli/bin/Release/net10.0/ps5kitsune && rm -rf /tmp/ps5k-8a && $cli install --root /tmp/ps5k-8a && $cli add /mnt/c/PS5Games/PPSA99666.zip --root /tmp/ps5k-8a && $cli list --root /tmp/ps5k-8a && ls -l /tmp/ps5k-8a/games/PPSA99666/PPSA99666.elf' 2>&1 | tr -d '\0'
```

(Check `$cli add --help` for the exact argument form first; if the DOOM zip is elsewhere, `ls /mnt/c/PS5Games` finds it.) Expected: the install's first step is "Find Python 3"; the add ends with the audit verdict orange 360/4/0 (as on Windows with v0.1.3); `PPSA99666.elf` exists and is executable. Then `$cli run PPSA99666 --root /tmp/ps5k-8a`: in WSL the game is expected to fail (no udmabuf, llvmpipe only); record how it ends and what `run` prints as the diagnosis. Remove `/tmp/ps5k-8a` afterwards.

If any step fails because of PS5Kitsune (not because WSL lacks udmabuf or a GPU), fix it in this task with a test, and say so in the report.

- [ ] **Step 4: Build and run every test on Windows and Linux**

Expected: 0 warnings, all pass.

- [ ] **Step 5: Commit**

```bash
git add .github/workflows/ci.yml docs/architecture.md README.md TODO.md
git commit -m "Publish the Linux CLI in CI and document Linux support"
```

---

## Criterion (run by the user, after Task 9)

By hand, from the spec:

1. **WSL Ubuntu 24.04:** `ps5kitsune check` shows llvmpipe, the udmabuf warning (device absent) and the commit limit as not applicable. `install`, then `add` DOOM (`PPSA99666.zip`): the same audit verdict as on Windows with v0.1.3, orange 360/4/0. `list`, `settings` and `diagnose PPSA99666` work.
2. **Real Ubuntu on the AMD laptop** (live USB with persistence, or dual boot): `check`, `install`, `add`, `run` DOOM, and it is playable. Then the Linux profile gets `"validated": true` (a one-line commit, with `The_linux_profile_downloads_the_linux_release_and_no_tools` asserting `True`).

The PR is opened after step 1 and merged after step 2.
