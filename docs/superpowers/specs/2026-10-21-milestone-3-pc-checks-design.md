# Milestone 3 — PC checks: design

October 21, 2026 · Complements [docs/architecture.md](../../architecture.md) (milestone 3 of the roadmap, "V1a — PC checks").

## Goal

Check that a PC can run AnyPS5 before anything is installed, from the command line and from a first real screen:

```text
ps5kitsune check
```

and the same results on the app's PC check screen. `ps5kitsune install` runs the checks first and stops on a blocking result.

**Out of scope:** Mesa download and Lavapipe activation (milestone 7: a missing `shaderInt64` only gets a message here), `ANYPS5_GPU` selection (milestone 7), navigation to the install screens (milestone 4), the `StepRunner` follow-ups (they matter for the install screen, milestone 4), a JSON export of the results, build mode (milestone 5).

## Check model (Core)

```csharp
public enum CheckStatus { Ok, Info, Warning, Blocking }

public enum CheckFix { OpenPerformanceSettings }

public sealed record CheckResult(string Id, string Title, CheckStatus Status, string Summary, string? Action, CheckFix? Fix);

public interface ISystemCheck
{
    string Id { get; }
    CheckResult Evaluate(SystemFacts facts);
}
```

**Facts first, then pure checks.** `SystemFactsCollector` gathers everything once into a `SystemFacts` record: OS architecture, CPU extensions and vendor, the Vulkan probe result, the commit limit, the Vulkan driver manifests, free space and the data root path. Each `ISystemCheck` is then a pure function of `SystemFacts`, so every check is tested without hardware, and the probe runs once for all Vulkan checks. `SystemChecker` runs the collector, then every registered check, and returns the results in a fixed order with the overall status.

**Fact sources, behind interfaces:**

- `ICpuInfo` (Core): extensions through .NET intrinsics (`Avx2`, `Fma`, `Bmi1`, `Bmi2`, `Lzcnt`), F16C and MOVBE through `X86Base.CpuId` leaf 1 ECX (bits 29 and 22), vendor through leaf 0. It replaces the static `CpuVendor` class, which `add` uses for `--to-intel`.
- `IVulkanProbeRunner` (Core): see "Isolated Vulkan probe".
- `IPlatform` gains, implemented in Platform.Windows:
  - `ulong GetCommitLimitBytes()`: `GlobalMemoryStatusEx`, field `ullTotalPageFile` (which is the commit limit, not the page file size).
  - `IReadOnlyList<string> GetVulkanDriverManifests()`: value names under `HKLM\SOFTWARE\Khronos\Vulkan\Drivers`; empty when the key is missing.
  - `void OpenFix(CheckFix fix)`: `OpenPerformanceSettings` starts `SystemPropertiesPerformance.exe`.

**Overall status** is the worst status. `Blocking` prevents installing; `Warning` is shown and does not block.

### Profile thresholds

Thresholds depend on the AnyPS5 version, so they go into the profile, in a new `checks` section:

```json
"checks": {
  "commitLimitMiB": 13824,
  "minFreeDiskGiB": 5,
  "nvidiaBadDrivers": [ "610.47" ]
}
```

`minFreeDiskGiB` closes the architecture's open point for release mode: about 1 GiB for AnyPS5 and Python, plus room for a game such as DOOM. Milestone 5 revisits it after measuring a build.

### The checks

| Id | Check | OK | Not OK |
| --- | --- | --- | --- |
| `os-arch` | 64-bit OS on x86-64 | `RuntimeInformation.OSArchitecture == X64` | Blocking: ARM is not supported |
| `cpu-extensions` | AVX2, FMA, F16C, BMI1, BMI2, LZCNT, MOVBE | All present | Blocking, naming each missing extension: a converted game stops at startup without it |
| `cpu-vendor` | Vendor string | — | Always Info. Intel: `add` converts with `--to-intel`; games that generate AMD instructions at runtime still need an AMD CPU |
| `vulkan-driver` | Vulkan driver and GPUs | Probe succeeded with at least one GPU | Blocking: probe failed, loader missing, or no GPU |
| `vulkan-features` | `shaderInt64` and `bufferDeviceAddress` per GPU | At least one GPU has both; when several GPUs exist and only some qualify, the summary names the usable one | Warning when no GPU has both, naming each GPU and the missing feature: Lavapipe (software rendering, slow) will let games run and comes with the launcher |
| `commit-limit` | RAM + page file | ≥ `commitLimitMiB` | Warning with the current value and `Fix = OpenPerformanceSettings`; no automatic change |
| `dozen` | Dozen (D3D12 over Vulkan) driver | No manifest name contains `dzn`, no GPU reports driver `MesaDozen`, and the probe did not need the retry | Info when present: it can crash enumeration and will be disabled at launch; the text says so when the probe needed the retry |
| `nvidia-driver` | NVIDIA driver known to crash | No NVIDIA GPU's `driverInfo` is in `nvidiaBadDrivers` | Warning: 610.47 crashes in `nvgpucomp64.dll`, update the driver |
| `disk-space` | Free space on the data root's drive | ≥ `minFreeDiskGiB` | Warning with the free space |
| `install-path` | Data root path | ASCII only, no spaces | Blocking: WinLibs and CMake fail otherwise; choose another root with `--root` |

`vulkan-features` and `nvidia-driver` are skipped (`Info`, "not checked") when `vulkan-driver` is blocking, so a failed probe produces one blocking line rather than three. `dozen` is still evaluated: it reads the manifests and may explain the failure.

## Isolated Vulkan probe

**Child process** (`<exe> --probe-vulkan`):

- `VulkanProbe.Run()` (Core, Silk.NET.Vulkan, MIT) creates an instance with no layers and no extensions, asking for API 1.2 when `vkEnumerateInstanceVersion` reports it and 1.1 otherwise, then enumerates the GPUs. For each GPU it calls `vkGetPhysicalDeviceProperties2` with `VkPhysicalDeviceDriverProperties` chained and `vkGetPhysicalDeviceFeatures2` with `VkPhysicalDeviceVulkan12Features` chained (`shaderInt64` is in the core features, `bufferDeviceAddress` in the 1.2 features).
- It writes one JSON object to stdout and exits with 0:

  ```json
  { "loaderFound": true, "error": null, "gpus": [ { "name": "AMD Radeon(TM) Graphics", "vendorId": 4098,
    "deviceType": "integrated", "apiVersion": "1.3.280", "driverId": "AmdProprietary",
    "driverName": "AMD proprietary driver", "driverInfo": "...", "shaderInt64": true, "bufferDeviceAddress": true } ] }
  ```

- `vulkan-1` cannot be loaded: `loaderFound: false`. `vkCreateInstance` fails: `gpus` empty and `error` holds the `VkResult` name.
- Both executables check for `--probe-vulkan` at the very top of `Main`, before Avalonia, NLog and System.CommandLine, so the child loads nothing else. The argument is not shown in `--help`.
- A GPU whose `apiVersion` is below 1.2 (or an instance limited to 1.1) gets neither 1.2 struct chained: it reports `bufferDeviceAddress: false` and empty driver fields.

**Parent**, `VulkanProbeRunner` (Core):

1. Runs `Environment.ProcessPath --probe-vulkan` through `IProcessRunner`, with a 15-second timeout (a linked `CancellationTokenSource`; the runner kills the child on cancellation).
2. Exit code 0 and valid JSON: result `Succeeded`.
3. Otherwise (crash, non-zero code, timeout, unreadable JSON): a second attempt with `VK_LOADER_DRIVERS_DISABLE=*dzn*` in the child's environment. Success gives `SucceededWithoutDozen`, which the `dozen` check reports. A second failure gives `Failed` with the reason and the tail of stderr.
4. Logs both attempts.

User cancellation (not the timeout) still throws `OperationCanceledException`.

The NVIDIA driver version is read from `driverInfo` ("610.47" on NVIDIA), because the encoding of `driverVersion` differs between vendors.

## CLI

**`ps5kitsune check [--root]`**

- One line per check: `[OK]`, `[INFO]`, `[WARN]` or `[BLOCK]`, the title and the summary. A non-OK line is followed by an indented `-> action` line (ASCII: the console's default code page can garble an arrow); for the commit limit it names the command to run, `SystemPropertiesPerformance.exe`.
- Then the GPUs the probe found: name, driver, `shaderInt64`, `bufferDeviceAddress`.
- Exit codes: **0** with no blocking result (warnings allowed), **2** with at least one. 1 (error) and 130 (cancelled) keep their meaning.
- Logs to `logs/check-<date>.log`.

**`install`** runs the same `SystemChecker` before its first step and prints only the Warning and Blocking lines (cpu-vendor is always Info). A blocking result stops it with exit code 2 before anything is downloaded; warnings are printed and the install continues. `add` and `run` do not run the checks.

## Screen (Avalonia)

The first real screen; milestone 4 adds the full navigation.

- `MainWindow` becomes a shell: a small header (platform, data root, profile, as shown today) and a content area bound to `CurrentPage`. This milestone has one page, `SystemCheckViewModel`.
- The page runs the checks in the background when it opens and shows "Checking this PC…" meanwhile (the probe can take a few seconds, 30 at worst with the retry).
- A banner gives the overall verdict ("This PC can run AnyPS5", "This PC can run AnyPS5, with warnings", "Blocking problems") and a "Check again" button.
- One row per check: a colored status badge (green OK, blue Info, orange Warning, red Blocking), the title, the summary and the action text. An "Open performance settings" button appears only when `Fix` is set; it calls `IPlatform.OpenFix`, so the view model knows nothing about Windows.
- A collapsible "Detected GPUs" section lists the probe's GPUs.
- UI text is in English.

## Tests

**Core.Tests, no hardware:**

- Every `ISystemCheck` with hand-built `SystemFacts`, at its boundaries: commit limit at 13,823 and 13,824 MiB, paths `C:\PS5 Kitsune` and `C:\Jeux\Café`, MOVBE missing, no GPU, `driverInfo` "610.47", `dzn` manifest present.
- **Iris Xe case** from a probe JSON fixture: an Intel iGPU with `shaderInt64: false` gives a `vulkan-features` warning mentioning Lavapipe and no blocking result. A second fixture, Intel iGPU without `shaderInt64` plus an NVIDIA GPU with both features, gives OK with a summary naming the NVIDIA GPU.
- `VulkanProbeRunner` with a fake `IProcessRunner`: valid JSON; first attempt crashes and second succeeds (`SucceededWithoutDozen`, and the second spec sets `VK_LOADER_DRIVERS_DISABLE`); both crash (`Failed`); timeout.
- The overall status is the worst one; the Vulkan checks are skipped when the driver check is blocking; the profile loader reads the `checks` section.
- `SystemCheckViewModel` with a fake checker, in a new `tests/PS5Kitsune.App.Tests` project: loading state, then results and verdict.

**Platform.Windows.Tests:** `GetCommitLimitBytes()` returns more than 0; `GetVulkanDriverManifests()` does not throw when the key is missing.

**Real probe:** a Core test calls `VulkanProbe.Run()` in-process and checks that its JSON round-trips; on the Ubuntu CI there is usually no loader (`loaderFound: false`), and nothing is asserted about hardware. `ps5kitsune --probe-vulkan` itself is run by hand in the manual verification.

## Criterion

This replaces the roadmap's criterion ("an Intel Iris Xe laptop reports `shaderInt64` as a warning with the Lavapipe suggestion"), since no Iris Xe is at hand:

1. On the AMD laptop (Ryzen 7 7730U, Radeon iGPU), `ps5kitsune check` and the screen show the same results, with no blocking result. The commit limit may be a warning, depending on the page file.
2. On an Intel + NVIDIA PC (available in a few days), the probe sees both GPUs; if the iGPU lacks `shaderInt64` it is reported and the NVIDIA GPU is named as usable. The result is recorded in the architecture. This run may happen after the PR is merged: the logic is covered by the fixtures, the run confirms it on real hardware.

**2026-10-21, AMD laptop:** `check` exit 0; os-arch OK, cpu-extensions OK, cpu-vendor Info (AMD), vulkan-driver OK (AMD Radeon (TM) Graphics), vulkan-features OK, commit-limit OK (18,333 MiB available; limit 13,824 MiB), dozen OK, nvidia-driver OK, disk-space OK, install-path OK. The screen shows the same statuses (confirmed by the user, 2026-10-21; the verdict banner was blue because cpu-vendor is always Info, fixed to green). Intel + NVIDIA run: pending.
