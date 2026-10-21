# Milestone 3 — PC checks: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Check that a PC can run AnyPS5 — CPU, Vulkan (isolated probe), commit limit, disk, data folder — from `ps5kitsune check`, before `ps5kitsune install`, and on the app's first real screen.

**Architecture:** `SystemFactsCollector` gathers every fact once (the Vulkan probe runs in a child process: the host relaunches itself with `--probe-vulkan`), then ten pure `ISystemCheck`s turn `SystemFacts` into `CheckResult`s. `SystemChecker` ties both together; the CLI prints the report, the Avalonia `SystemCheckViewModel` binds it.

**Tech Stack:** .NET 10, C#, Silk.NET.Vulkan 2.23.0 (MIT), System.CommandLine, Avalonia 12 + CommunityToolkit.Mvvm, xUnit v3 (Microsoft Testing Platform).

**Spec:** [docs/superpowers/specs/2026-10-21-milestone-3-pc-checks-design.md](../specs/2026-10-21-milestone-3-pc-checks-design.md). Task 1 amends it with the refinements below.

## Global Constraints

- License: every shipped package must be GPLv2-compatible (MIT, BSD, LGPL; never Apache-2.0). CI checks it with `dotnet nuget-license`; `build/allowed-licenses.json` lists what is allowed.
- Language: code, comments, UI text, docs and commits in English.
- Commits authored as `La-Kiks` (never the gmail address), ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Commit locally; **do not push**.
- Build settings (`Directory.Build.props`): `TreatWarningsAsErrors`, `AnalysisLevel latest-recommended`, `EnforceCodeStyleInBuild`, nullable on. Analyzer warnings fail the build: fix them, never suppress them globally.
- Core stays free of Windows APIs (the Ubuntu CI job builds and tests it). Windows-only code lives in `PS5Kitsune.Platform.Windows` under `[SupportedOSPlatform("windows")]`.
- Packages are versioned centrally in `Directory.Packages.props`.
- Exit codes: 0 success, 1 error, 2 blocking check result (new), 130 cancelled.
- Thresholds from the profile: `commitLimitMiB` 13824, `minFreeDiskGiB` 5, `nvidiaBadDrivers` `["610.47"]`.
- Probe: argument `--probe-vulkan`, timeout 15 s, retry with `VK_LOADER_DRIVERS_DISABLE=*dzn*`.
- Run commands through the Bash tool (RTK). Build: `dotnet build PS5Kitsune.slnx -c Release`. Tests: `dotnet test --solution PS5Kitsune.slnx -c Release` (filter one class with `-- --filter-class "<full class name>"`).

## Refinements to the spec (applied by Task 1)

1. **Real probe test runs in-process.** No test project exists for the CLI, so the real-probe test calls `VulkanProbe.Run()` inside the Core test process and checks the JSON round trip. The end-to-end `ps5kitsune --probe-vulkan` run is part of the manual verification (Task 11).
2. **The `dozen` check is not skipped** when the driver check is blocking: it reads the manifests too and may explain the failure. Only `vulkan-features` and `nvidia-driver` are skipped, so a failed probe gives one blocking line instead of three.
3. **CLI action lines start with `->`**, not `→`: the Windows console's default code page can garble the arrow.
4. **Dozen is also detected** from the probe's `driverId` (`MesaDozen`), because the loader can find Dozen without a registry manifest.
5. **An app test project is added** (`tests/PS5Kitsune.App.Tests`) for the `SystemCheckViewModel` test the spec asks for.

## File map

| File | Responsibility |
| --- | --- |
| `src/PS5Kitsune.Core/Profiles/AnyPS5Profile.cs` (modify) | `CheckThresholds` record and `Checks` property |
| `src/PS5Kitsune.Core/Profiles/ProfileLoader.cs` (modify) | Validate thresholds |
| `src/PS5Kitsune.Core/Profiles/default-profile.json` (modify) | `checks` section |
| `src/PS5Kitsune.Core/Checks/Vulkan/VulkanProbeReport.cs` | Probe JSON: `VulkanProbeReport`, `VulkanGpu` |
| `src/PS5Kitsune.Core/Checks/Vulkan/VulkanProbe.cs` | Silk.NET enumeration, run in the child process |
| `src/PS5Kitsune.Core/Checks/Vulkan/VulkanProbeRunner.cs` | Relaunch, timeout, Dozen retry; `IVulkanProbeRunner`, `VulkanProbeOutcome` |
| `src/PS5Kitsune.Core/Checks/CpuInfo.cs` | `ICpuInfo`, `CpuFacts`, `X86CpuInfo` (replaces `CpuVendor.cs`) |
| `src/PS5Kitsune.Core/Checks/CheckModel.cs` | `CheckStatus`, `CheckFix`, `CheckResult`, `ISystemCheck`, `SystemCheck` base |
| `src/PS5Kitsune.Core/Checks/SystemFacts.cs` | The facts every check reads |
| `src/PS5Kitsune.Core/Checks/HardwareChecks.cs` | `os-arch`, `cpu-extensions`, `cpu-vendor`, `commit-limit` |
| `src/PS5Kitsune.Core/Checks/StorageChecks.cs` | `disk-space`, `install-path` |
| `src/PS5Kitsune.Core/Checks/VulkanChecks.cs` | `vulkan-driver`, `vulkan-features`, `dozen`, `nvidia-driver` |
| `src/PS5Kitsune.Core/Checks/SystemChecker.cs` | `SystemFactsCollector`, `SystemChecker`, `ISystemChecker`, `SystemCheckReport`, `SystemChecks.All` |
| `src/PS5Kitsune.Core/Abstractions/IPlatform.cs` (modify) | Commit limit, driver manifests, `OpenFix` |
| `src/PS5Kitsune.Platform.Windows/WindowsPlatform.cs` (modify) | Windows implementations |
| `src/PS5Kitsune.Cli/Program.cs`, `CheckCommand.cs`, `CheckReportPrinter.cs`, `InstallCommand.cs`, `AddCommand.cs`, `CliLog.cs` | Probe entry, `check`, install gate, CPU via `ICpuInfo` |
| `src/PS5Kitsune.App/Program.cs`, `App.axaml.cs`, `ViewModels/*`, `Views/*`, `Converters/CheckStatusBrushConverter.cs` | Probe entry, shell, check page |
| `tests/PS5Kitsune.App.Tests/*` | View model tests |

---

### Task 1: Profile thresholds and spec refinements

**Files:**
- Modify: `src/PS5Kitsune.Core/Profiles/AnyPS5Profile.cs`
- Modify: `src/PS5Kitsune.Core/Profiles/ProfileLoader.cs`
- Modify: `src/PS5Kitsune.Core/Profiles/default-profile.json`
- Modify: `docs/superpowers/specs/2026-10-21-milestone-3-pc-checks-design.md`
- Test: `tests/PS5Kitsune.Core.Tests/Profiles/ProfileLoaderTests.cs`

**Interfaces:**
- Produces: `CheckThresholds { long CommitLimitMiB; long MinFreeDiskGiB; IReadOnlyList<string> NvidiaBadDrivers }` in `PS5Kitsune.Core.Profiles`; `AnyPS5Profile.Checks` (never null; defaults 13824 / 5 / empty).

- [ ] **Step 1: Write the failing tests**

Append to `ProfileLoaderTests` (the class already has a private `Load(string json)` helper used by the other tests):

```csharp
    [Fact]
    public void Embedded_profile_has_check_thresholds()
    {
        var checks = ProfileLoader.LoadEmbedded().Checks;

        Assert.Equal(13824, checks.CommitLimitMiB);
        Assert.Equal(5, checks.MinFreeDiskGiB);
        Assert.Contains("610.47", checks.NvidiaBadDrivers);
    }

    [Fact]
    public void A_profile_without_checks_gets_the_default_thresholds()
    {
        var profile = Load("""
            { "schemaVersion": 1, "id": "minimal", "source": { "kind": "release", "tag": "v1" } }
            """);

        Assert.Equal(13824, profile.Checks.CommitLimitMiB);
        Assert.Equal(5, profile.Checks.MinFreeDiskGiB);
        Assert.Empty(profile.Checks.NvidiaBadDrivers);
    }

    [Fact]
    public void Rejects_a_non_positive_commit_limit()
    {
        var exception = Assert.Throws<InvalidProfileException>(() => Load("""
            { "schemaVersion": 1, "id": "bad", "source": { "kind": "release", "tag": "v1" }, "checks": { "commitLimitMiB": 0 } }
            """));

        Assert.Contains("commitLimitMiB", exception.Message, StringComparison.Ordinal);
    }
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build error `'AnyPS5Profile' does not contain a definition for 'Checks'`.

- [ ] **Step 3: Implement**

In `AnyPS5Profile.cs`, add after `public RelinkerOptions Relinker { get; init; } = new();`:

```csharp

    /// <summary>PC check thresholds; a profile without a "checks" section gets the defaults.</summary>
    public CheckThresholds Checks { get; init; } = new();
```

At the end of `AnyPS5Profile.cs`:

```csharp

/// <summary>PC check thresholds that depend on the AnyPS5 version.</summary>
public sealed record CheckThresholds
{
    /// <summary>Memory a game can commit; RAM + page file must reach it.</summary>
    public long CommitLimitMiB { get; init; } = 13824;

    /// <summary>Free space wanted on the data root's drive for AnyPS5 and a game.</summary>
    public long MinFreeDiskGiB { get; init; } = 5;

    /// <summary>NVIDIA driver versions, as Vulkan's driverInfo reports them, known to crash AnyPS5 games.</summary>
    public IReadOnlyList<string> NvidiaBadDrivers { get; init; } = [];
}
```

In `ProfileLoader.Validate`, after the `foreach (var (name, tool) in profile.Tools)` loop:

```csharp

        if (profile.Checks.CommitLimitMiB <= 0 || profile.Checks.MinFreeDiskGiB < 0)
        {
            throw new InvalidProfileException($"Profile {profile.Id}: checks.commitLimitMiB must be positive and checks.minFreeDiskGiB not negative.");
        }
```

In `default-profile.json`, after the `"relinker"` object (add a comma after its closing brace):

```json
  "checks": {
    "commitLimitMiB": 13824,
    "minFreeDiskGiB": 5,
    "nvidiaBadDrivers": [ "610.47" ]
  }
```

- [ ] **Step 4: Run the tests and see them pass**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.Core.Tests.Profiles.ProfileLoaderTests"`
Expected: all ProfileLoaderTests pass.

- [ ] **Step 5: Amend the spec**

In `docs/superpowers/specs/2026-10-21-milestone-3-pc-checks-design.md`:

1. Replace the sentence `The Vulkan checks are skipped (\`Info\`, "not checked") when \`vulkan-driver\` is blocking, so a failed probe produces one blocking line rather than four.` with:
   `` `vulkan-features` and `nvidia-driver` are skipped (`Info`, "not checked") when `vulkan-driver` is blocking, so a failed probe produces one blocking line rather than three. `dozen` is still evaluated: it reads the manifests and may explain the failure. ``
2. In the `dozen` row of the checks table, replace `No manifest name contains \`dzn\` and the probe did not need the retry` with `No manifest name contains \`dzn\`, no GPU reports driver \`MesaDozen\`, and the probe did not need the retry`.
3. In the CLI section, replace ``followed by an indented `→ action` line`` with ``followed by an indented `-> action` line (ASCII: the console's default code page can garble an arrow)``.
4. In the Tests section, replace the "Real probe" paragraph with:
   `` **Real probe:** a Core test calls `VulkanProbe.Run()` in-process and checks that its JSON round-trips; on the Ubuntu CI there is usually no loader (`loaderFound: false`), and nothing is asserted about hardware. `ps5kitsune --probe-vulkan` itself is run by hand in the manual verification. ``
5. In the Tests section, replace the bullet `` `SystemCheckViewModel` with a fake checker: loading state, then results and verdict.`` with `` `SystemCheckViewModel` with a fake checker, in a new `tests/PS5Kitsune.App.Tests` project: loading state, then results and verdict.``

- [ ] **Step 6: Commit**

```bash
git add src/PS5Kitsune.Core/Profiles tests/PS5Kitsune.Core.Tests/Profiles docs/superpowers/specs/2026-10-21-milestone-3-pc-checks-design.md
git commit -m "Add PC check thresholds to the profile

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Vulkan probe (child side)

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `src/PS5Kitsune.Core/PS5Kitsune.Core.csproj`
- Create: `src/PS5Kitsune.Core/Checks/Vulkan/VulkanProbeReport.cs`
- Create: `src/PS5Kitsune.Core/Checks/Vulkan/VulkanProbe.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Checks/VulkanProbeTests.cs`

**Interfaces:**
- Produces (namespace `PS5Kitsune.Core.Checks.Vulkan`):
  - `record VulkanProbeReport(bool LoaderFound, string? Error, IReadOnlyList<VulkanGpu> Gpus)` with `string ToJson()` and `static VulkanProbeReport FromJson(string json)` (throws `JsonException`).
  - `record VulkanGpu(string Name, uint VendorId, string DeviceType, string ApiVersion, string? DriverId, string? DriverName, string? DriverInfo, bool ShaderInt64, bool BufferDeviceAddress)` with `[JsonIgnore]` computed `bool IsNvidia`, `bool IsDozen`, `bool SupportsAnyPS5`, and `string Describe()`.
  - `static class VulkanProbe { static VulkanProbeReport Run(); }`

- [ ] **Step 1: Add the package**

In `Directory.Packages.props`, in the shipped group, after `NLog.Extensions.Logging`:

```xml
    <PackageVersion Include="Silk.NET.Vulkan" Version="2.23.0" />
```

In `src/PS5Kitsune.Core/PS5Kitsune.Core.csproj`, add to the package `ItemGroup`:

```xml
    <PackageReference Include="Silk.NET.Vulkan" />
```

and a new `PropertyGroup` before the first `ItemGroup`:

```xml
  <PropertyGroup>
    <!-- Vulkan structs are chained through pointers in the probe. -->
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
```

Check the licenses of the new packages (Silk.NET.Vulkan, Silk.NET.Core and their dependencies):

Run: `dotnet tool restore && dotnet restore PS5Kitsune.slnx && dotnet nuget-license -i PS5Kitsune.slnx -t -exclude-projects "*Tests*" -a build/allowed-licenses.json -err`
Expected: exit code 0. If a dependency has a license outside the list, stop and report it to the user: do not widen `allowed-licenses.json`.

- [ ] **Step 2: Write the failing tests**

`tests/PS5Kitsune.Core.Tests/Checks/VulkanProbeTests.cs`:

```csharp
using PS5Kitsune.Core.Checks.Vulkan;

namespace PS5Kitsune.Core.Tests.Checks;

public sealed class VulkanProbeTests
{
    internal const string IrisXeJson = """
        { "loaderFound": true, "error": null, "gpus": [
          { "name": "Intel(R) Iris(R) Xe Graphics", "vendorId": 32902, "deviceType": "integrated", "apiVersion": "1.3.289",
            "driverId": "IntelProprietaryWindows", "driverName": "Intel Corporation", "driverInfo": "101.5762",
            "shaderInt64": false, "bufferDeviceAddress": true } ] }
        """;

    [Fact]
    public void Reads_the_probe_json()
    {
        var report = VulkanProbeReport.FromJson(IrisXeJson);

        Assert.True(report.LoaderFound);
        Assert.Null(report.Error);
        var gpu = Assert.Single(report.Gpus);
        Assert.Equal("Intel(R) Iris(R) Xe Graphics", gpu.Name);
        Assert.Equal(0x8086u, gpu.VendorId);
        Assert.False(gpu.ShaderInt64);
        Assert.True(gpu.BufferDeviceAddress);
        Assert.False(gpu.SupportsAnyPS5);
        Assert.False(gpu.IsNvidia);
    }

    [Fact]
    public void Writes_camel_case_json_without_computed_properties()
    {
        var gpu = new VulkanGpu("GPU", VulkanGpu.NvidiaVendorId, "discrete", "1.3.0", "NvidiaProprietary", "NVIDIA", "610.47", true, true);

        var json = new VulkanProbeReport(true, null, [gpu]).ToJson();

        Assert.Contains("\"loaderFound\":true", json, StringComparison.Ordinal);
        Assert.Contains("\"driverInfo\":\"610.47\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("isNvidia", json, StringComparison.Ordinal);
        Assert.DoesNotContain("supportsAnyPS5", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_output_that_is_not_json()
    {
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => VulkanProbeReport.FromJson("Segmentation fault"));
    }

    [Fact]
    public void Describes_a_gpu_in_one_line()
    {
        var gpu = new VulkanGpu("Radeon", 0x1002, "integrated", "1.3.280", "AmdProprietary", "AMD", "25.10", true, false);

        Assert.Equal("Radeon (integrated), driver 25.10, shaderInt64: yes, bufferDeviceAddress: no", gpu.Describe());
    }

    [Fact]
    public void Dozen_is_recognized_by_its_driver_id()
    {
        var gpu = new VulkanGpu("Microsoft Direct3D12", 0x1414, "integrated", "1.2.0", "MesaDozen", "Dozen", "Mesa 24", true, true);

        Assert.True(gpu.IsDozen);
    }

    // Runs the real probe in this process: on a machine whose driver crashes enumeration this test crashes too,
    // which is acceptable for a test run (the app itself never probes in-process).
    [Fact]
    public void Real_probe_output_round_trips_through_json()
    {
        var report = VulkanProbe.Run();

        var parsed = VulkanProbeReport.FromJson(report.ToJson());

        Assert.Equal(report.LoaderFound, parsed.LoaderFound);
        Assert.Equal(report.Error, parsed.Error);
        Assert.Equal(report.Gpus.Count, parsed.Gpus.Count);
        Assert.All(parsed.Gpus, gpu => Assert.False(string.IsNullOrEmpty(gpu.Name)));
    }
}
```

- [ ] **Step 3: Run and see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build error, `VulkanProbeReport` not found.

- [ ] **Step 4: Implement the report types**

`src/PS5Kitsune.Core/Checks/Vulkan/VulkanProbeReport.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PS5Kitsune.Core.Checks.Vulkan;

/// <summary>What the probe child process found, as it writes it to its standard output.</summary>
/// <param name="LoaderFound">False when the Vulkan loader (vulkan-1.dll, libvulkan.so.1) cannot be loaded.</param>
/// <param name="Error">Why enumeration stopped (a VkResult name or a message); null when it completed.</param>
public sealed record VulkanProbeReport(bool LoaderFound, string? Error, IReadOnlyList<VulkanGpu> Gpus)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <exception cref="JsonException">The text is not a probe report.</exception>
    public static VulkanProbeReport FromJson(string json) =>
        JsonSerializer.Deserialize<VulkanProbeReport>(json, Options) ?? throw new JsonException("The probe wrote an empty report.");
}

/// <param name="DeviceType">"integrated", "discrete", "virtual", "cpu" or "other".</param>
/// <param name="ApiVersion">Vulkan version the driver supports, for example "1.3.280".</param>
/// <param name="DriverId">VkDriverId name, for example "NvidiaProprietary"; null below Vulkan 1.2.</param>
/// <param name="DriverInfo">Driver version text ("610.47" on NVIDIA); null below Vulkan 1.2.</param>
public sealed record VulkanGpu(
    string Name,
    uint VendorId,
    string DeviceType,
    string ApiVersion,
    string? DriverId,
    string? DriverName,
    string? DriverInfo,
    bool ShaderInt64,
    bool BufferDeviceAddress)
{
    public const uint NvidiaVendorId = 0x10DE;

    [JsonIgnore]
    public bool IsNvidia => VendorId == NvidiaVendorId;

    /// <summary>Dozen: Mesa's Vulkan driver over Direct3D 12.</summary>
    [JsonIgnore]
    public bool IsDozen => DriverId == "MesaDozen";

    /// <summary>AnyPS5 needs both features on the GPU it renders with.</summary>
    [JsonIgnore]
    public bool SupportsAnyPS5 => ShaderInt64 && BufferDeviceAddress;

    public string Describe() =>
        $"{Name} ({DeviceType}), driver {DriverInfo ?? "unknown"}, shaderInt64: {YesNo(ShaderInt64)}, bufferDeviceAddress: {YesNo(BufferDeviceAddress)}";

    private static string YesNo(bool value) => value ? "yes" : "no";
}
```

- [ ] **Step 5: Implement the probe**

`src/PS5Kitsune.Core/Checks/Vulkan/VulkanProbe.cs`:

```csharp
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace PS5Kitsune.Core.Checks.Vulkan;

/// <summary>
/// Lists the GPUs and the features AnyPS5 needs. Runs only in the probe child process
/// (<c>--probe-vulkan</c>): some drivers crash the process during enumeration.
/// </summary>
public static unsafe class VulkanProbe
{
    private const uint ApiVersion11 = (1u << 22) | (1u << 12);
    private const uint ApiVersion12 = (1u << 22) | (2u << 12);

    public static VulkanProbeReport Run()
    {
        Vk vk;
        try
        {
            vk = Vk.GetApi();
        }
        catch (Exception exception)
        {
            // Silk.NET reports a missing loader with different exception types depending on the OS.
            return new VulkanProbeReport(LoaderFound: false, exception.Message, []);
        }

        using (vk)
        {
            return Enumerate(vk);
        }
    }

    private static VulkanProbeReport Enumerate(Vk vk)
    {
        uint loaderVersion;
        try
        {
            if (vk.EnumerateInstanceVersion(&loaderVersion) != Result.Success)
            {
                loaderVersion = 0;
            }
        }
        catch (Exception)
        {
            // vkEnumerateInstanceVersion does not exist in a Vulkan 1.0 loader.
            loaderVersion = 0;
        }

        if (loaderVersion < ApiVersion11)
        {
            return new VulkanProbeReport(true, "The Vulkan loader is older than 1.1.", []);
        }

        var instanceVersion = loaderVersion >= ApiVersion12 ? ApiVersion12 : ApiVersion11;
        var applicationName = (byte*)Marshal.StringToHGlobalAnsi("PS5Kitsune");
        try
        {
            var applicationInfo = new ApplicationInfo
            {
                SType = StructureType.ApplicationInfo,
                PApplicationName = applicationName,
                ApiVersion = instanceVersion,
            };
            var createInfo = new InstanceCreateInfo
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &applicationInfo,
            };
            Instance instance;
            var result = vk.CreateInstance(&createInfo, null, &instance);
            if (result != Result.Success)
            {
                return new VulkanProbeReport(true, $"vkCreateInstance returned {result}.", []);
            }

            try
            {
                return ListGpus(vk, instance, instanceVersion);
            }
            finally
            {
                vk.DestroyInstance(instance, null);
            }
        }
        finally
        {
            Marshal.FreeHGlobal((nint)applicationName);
        }
    }

    private static VulkanProbeReport ListGpus(Vk vk, Instance instance, uint instanceVersion)
    {
        uint count = 0;
        var result = vk.EnumeratePhysicalDevices(instance, &count, null);
        if (result != Result.Success)
        {
            return new VulkanProbeReport(true, $"vkEnumeratePhysicalDevices returned {result}.", []);
        }

        var devices = new PhysicalDevice[count];
        fixed (PhysicalDevice* pointer = devices)
        {
            result = vk.EnumeratePhysicalDevices(instance, &count, pointer);
        }

        if (result is not (Result.Success or Result.Incomplete))
        {
            return new VulkanProbeReport(true, $"vkEnumeratePhysicalDevices returned {result}.", []);
        }

        return new VulkanProbeReport(true, null, [.. devices.Take((int)count).Select(device => Describe(vk, device, instanceVersion))]);
    }

    private static VulkanGpu Describe(Vk vk, PhysicalDevice device, uint instanceVersion)
    {
        PhysicalDeviceProperties properties;
        vk.GetPhysicalDeviceProperties(device, &properties);

        // The 1.2 structs may only be chained when both the instance and the device speak Vulkan 1.2.
        var has12 = instanceVersion >= ApiVersion12 && properties.ApiVersion >= ApiVersion12;

        var driver = new PhysicalDeviceDriverProperties { SType = StructureType.PhysicalDeviceDriverProperties };
        var properties2 = new PhysicalDeviceProperties2
        {
            SType = StructureType.PhysicalDeviceProperties2,
            PNext = has12 ? &driver : null,
        };
        vk.GetPhysicalDeviceProperties2(device, &properties2);

        var features12 = new PhysicalDeviceVulkan12Features { SType = StructureType.PhysicalDeviceVulkan12Features };
        var features2 = new PhysicalDeviceFeatures2
        {
            SType = StructureType.PhysicalDeviceFeatures2,
            PNext = has12 ? &features12 : null,
        };
        vk.GetPhysicalDeviceFeatures2(device, &features2);

        return new VulkanGpu(
            Text(properties.DeviceName),
            properties.VendorID,
            DeviceType(properties.DeviceType),
            FormatVersion(properties.ApiVersion),
            has12 ? driver.DriverID.ToString() : null,
            has12 ? Text(driver.DriverName) : null,
            has12 ? Text(driver.DriverInfo) : null,
            features2.Features.ShaderInt64,
            has12 && features12.BufferDeviceAddress);
    }

    private static string Text(byte* nullTerminated) => Marshal.PtrToStringUTF8((nint)nullTerminated) ?? "";

    private static string DeviceType(PhysicalDeviceType type) => type switch
    {
        PhysicalDeviceType.IntegratedGpu => "integrated",
        PhysicalDeviceType.DiscreteGpu => "discrete",
        PhysicalDeviceType.VirtualGpu => "virtual",
        PhysicalDeviceType.Cpu => "cpu",
        _ => "other",
    };

    private static string FormatVersion(uint version) => $"{(version >> 22) & 0x7F}.{(version >> 12) & 0x3FF}.{version & 0xFFF}";
}
```

Silk.NET 2.23 member names used above: `Vk.GetApi`, `EnumerateInstanceVersion(uint*)`, `CreateInstance`, `DestroyInstance`, `EnumeratePhysicalDevices`, `GetPhysicalDeviceProperties`, `GetPhysicalDeviceProperties2`, `GetPhysicalDeviceFeatures2`, `PhysicalDeviceProperties.{DeviceName, VendorID, DeviceType, ApiVersion}`, `PhysicalDeviceDriverProperties.{DriverID, DriverName, DriverInfo}`, `PhysicalDeviceVulkan12Features.BufferDeviceAddress`, and the `Bool32` → `bool` implicit conversion. If one differs, use the name Silk.NET 2.23 exposes for the same Vulkan field; keep the logic unchanged.

If the analyzers flag `catch (Exception)` (CA1031), keep the catch and add a justification with `#pragma warning disable CA1031 // <reason>` / `restore` around that block only: both catches are on purpose (the loader's failure types are not documented).

- [ ] **Step 6: Run the tests and see them pass**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.Core.Tests.Checks.VulkanProbeTests"`
Expected: 6 passed. On the AMD laptop, `Real_probe_output_round_trips_through_json` sees one GPU.

- [ ] **Step 7: Commit**

```bash
git add Directory.Packages.props src/PS5Kitsune.Core tests/PS5Kitsune.Core.Tests/Checks/VulkanProbeTests.cs
git commit -m "Add the Vulkan probe and its JSON report

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Probe runner (parent side)

**Files:**
- Create: `src/PS5Kitsune.Core/Checks/Vulkan/VulkanProbeRunner.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Checks/VulkanProbeRunnerTests.cs`

**Interfaces:**
- Consumes: `VulkanProbeReport.FromJson` (Task 2); `IProcessRunner`, `ProcessSpec`, `OutputLine`, `OutputSource` (`PS5Kitsune.Core.Abstractions`); `OutputTail` (`PS5Kitsune.Core.Processes`).
- Produces (namespace `PS5Kitsune.Core.Checks.Vulkan`):
  - `enum VulkanProbeStatus { Succeeded, SucceededWithoutDozen, Failed }`
  - `record VulkanProbeOutcome(VulkanProbeStatus Status, VulkanProbeReport? Report, string? FailureReason)` with `static VulkanProbeOutcome Failed(string reason)`.
  - `interface IVulkanProbeRunner { Task<VulkanProbeOutcome> RunAsync(CancellationToken cancellationToken = default); }`
  - `class VulkanProbeRunner : IVulkanProbeRunner` with `const string ProbeArgument = "--probe-vulkan"`, `const string DisableDriversVariable = "VK_LOADER_DRIVERS_DISABLE"`, `const string DozenPattern = "*dzn*"`; public ctor `(IProcessRunner, ILogger<VulkanProbeRunner>)`; internal ctor `(IProcessRunner, ILogger<VulkanProbeRunner>, string executable, TimeSpan timeout)`.

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.Core.Tests/Checks/VulkanProbeRunnerTests.cs`:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Checks.Vulkan;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.Checks;

public sealed class VulkanProbeRunnerTests
{
    private const int AccessViolation = unchecked((int)0xC0000005);

    private readonly FakeProcessRunner _runner = new();

    private VulkanProbeRunner CreateRunner(IProcessRunner? runner = null, TimeSpan? timeout = null) =>
        new(runner ?? _runner, NullLogger<VulkanProbeRunner>.Instance, "probe.exe", timeout ?? TimeSpan.FromSeconds(15));

    private static FakeProcessResult Json() => new(0, [VulkanProbeTests.IrisXeJson]);

    [Fact]
    public async Task Returns_the_report_of_a_successful_probe()
    {
        _runner.Respond = _ => Json();

        var outcome = await CreateRunner().RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(VulkanProbeStatus.Succeeded, outcome.Status);
        Assert.Single(outcome.Report!.Gpus);
        var call = Assert.Single(_runner.Calls);
        Assert.Equal("probe.exe", call.FileName);
        Assert.Equal(["--probe-vulkan"], call.Arguments);
        Assert.Empty(call.Environment);
    }

    [Fact]
    public async Task Retries_without_Dozen_after_a_crash()
    {
        _runner.Respond = spec => spec.Environment.ContainsKey(VulkanProbeRunner.DisableDriversVariable) ? Json() : new FakeProcessResult(AccessViolation);

        var outcome = await CreateRunner().RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(VulkanProbeStatus.SucceededWithoutDozen, outcome.Status);
        Assert.NotNull(outcome.Report);
        Assert.Equal(2, _runner.Calls.Count);
        Assert.Equal("*dzn*", _runner.Calls[1].Environment[VulkanProbeRunner.DisableDriversVariable]);
    }

    [Fact]
    public async Task Fails_when_both_attempts_crash()
    {
        _runner.Respond = _ => new FakeProcessResult(AccessViolation, StandardError: ["driver exploded"]);

        var outcome = await CreateRunner().RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(VulkanProbeStatus.Failed, outcome.Status);
        Assert.Null(outcome.Report);
        Assert.Contains("0xC0000005", outcome.FailureReason, StringComparison.Ordinal);
        Assert.Contains("driver exploded", outcome.FailureReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fails_when_the_output_is_not_json()
    {
        _runner.Respond = _ => new FakeProcessResult(0, ["not json"]);

        var outcome = await CreateRunner().RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(VulkanProbeStatus.Failed, outcome.Status);
        Assert.Contains("not valid JSON", outcome.FailureReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_probe_that_hangs_times_out_twice()
    {
        var hanging = new HangingProcessRunner();

        var outcome = await CreateRunner(hanging, TimeSpan.FromMilliseconds(50)).RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(VulkanProbeStatus.Failed, outcome.Status);
        Assert.Contains("did not answer", outcome.FailureReason, StringComparison.Ordinal);
        Assert.Equal(2, hanging.Calls);
    }

    [Fact]
    public async Task User_cancellation_is_not_a_timeout()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateRunner(new HangingProcessRunner()).RunAsync(cancellation.Token));
    }

    private sealed class HangingProcessRunner : IProcessRunner
    {
        public int Calls { get; private set; }

        public async Task<ProcessResult> RunAsync(ProcessSpec spec, Action<OutputLine>? onOutput = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Unreachable.");
        }
    }
}
```

- [ ] **Step 2: Run and see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build error, `VulkanProbeRunner` not found.

- [ ] **Step 3: Implement**

`src/PS5Kitsune.Core/Checks/Vulkan/VulkanProbeRunner.cs`:

```csharp
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Processes;

namespace PS5Kitsune.Core.Checks.Vulkan;

public enum VulkanProbeStatus
{
    Succeeded,

    /// <summary>The first attempt failed; the second, with Dozen disabled, succeeded.</summary>
    SucceededWithoutDozen,

    Failed,
}

/// <param name="Report">What the probe found; null when <paramref name="Status"/> is Failed.</param>
/// <param name="FailureReason">Why both attempts failed; null otherwise.</param>
public sealed record VulkanProbeOutcome(VulkanProbeStatus Status, VulkanProbeReport? Report, string? FailureReason)
{
    public static VulkanProbeOutcome Failed(string reason) => new(VulkanProbeStatus.Failed, null, reason);
}

public interface IVulkanProbeRunner
{
    /// <summary>Probes Vulkan in a child process. A crash or a hang becomes a Failed outcome, not an exception.</summary>
    Task<VulkanProbeOutcome> RunAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Relaunches the running executable with <see cref="ProbeArgument"/>, so a driver that crashes
/// during enumeration takes down the child, not the app. See docs/architecture.md, "Isolated Vulkan probe".
/// </summary>
public sealed partial class VulkanProbeRunner : IVulkanProbeRunner
{
    public const string ProbeArgument = "--probe-vulkan";

    public const string DisableDriversVariable = "VK_LOADER_DRIVERS_DISABLE";

    public const string DozenPattern = "*dzn*";

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    private readonly IProcessRunner _runner;
    private readonly ILogger<VulkanProbeRunner> _logger;
    private readonly string _executable;
    private readonly TimeSpan _timeout;

    public VulkanProbeRunner(IProcessRunner runner, ILogger<VulkanProbeRunner> logger)
        : this(runner, logger, Environment.ProcessPath ?? throw new InvalidOperationException("The path of the running executable is unknown."), DefaultTimeout)
    {
    }

    internal VulkanProbeRunner(IProcessRunner runner, ILogger<VulkanProbeRunner> logger, string executable, TimeSpan timeout)
    {
        _runner = runner;
        _logger = logger;
        _executable = executable;
        _timeout = timeout;
    }

    public async Task<VulkanProbeOutcome> RunAsync(CancellationToken cancellationToken = default)
    {
        var (report, failure) = await AttemptAsync(withoutDozen: false, cancellationToken).ConfigureAwait(false);
        if (report is not null)
        {
            return new VulkanProbeOutcome(VulkanProbeStatus.Succeeded, report, null);
        }

        LogRetrying(failure);
        var (retryReport, retryFailure) = await AttemptAsync(withoutDozen: true, cancellationToken).ConfigureAwait(false);
        if (retryReport is not null)
        {
            return new VulkanProbeOutcome(VulkanProbeStatus.SucceededWithoutDozen, retryReport, null);
        }

        LogFailed(retryFailure);
        return VulkanProbeOutcome.Failed($"{failure} With Dozen disabled: {retryFailure}");
    }

    private async Task<(VulkanProbeReport? Report, string Failure)> AttemptAsync(bool withoutDozen, CancellationToken cancellationToken)
    {
        var spec = new ProcessSpec(_executable, [ProbeArgument])
        {
            Environment = withoutDozen
                ? new Dictionary<string, string?> { [DisableDriversVariable] = DozenPattern }
                : new Dictionary<string, string?>(),
        };
        var output = new StringBuilder();
        var errors = new OutputTail(5);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        ProcessResult result;
        try
        {
            result = await _runner.RunAsync(spec, line =>
            {
                if (line.Source == OutputSource.StandardOutput)
                {
                    output.AppendLine(line.Text);
                }

                errors.Collect(line);
            }, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (null, string.Create(CultureInfo.InvariantCulture, $"The probe did not answer within {_timeout.TotalSeconds:0.##} seconds."));
        }

        if (result.ExitCode != 0)
        {
            var stderr = errors.ToString();
            var detail = stderr.Length == 0 ? "" : $" ({stderr.ReplaceLineEndings(" ")})";
            return (null, string.Create(CultureInfo.InvariantCulture, $"The probe exited with code {result.ExitCode} (0x{result.ExitCode:X8}){detail}."));
        }

        try
        {
            return (VulkanProbeReport.FromJson(output.ToString()), "");
        }
        catch (JsonException exception)
        {
            return (null, $"The probe output is not valid JSON: {exception.Message}");
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Vulkan probe failed: {Failure} Retrying with Dozen disabled.")]
    private partial void LogRetrying(string failure);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Vulkan probe failed again with Dozen disabled: {Failure}")]
    private partial void LogFailed(string failure);
}
```

- [ ] **Step 4: Run the tests and see them pass**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.Core.Tests.Checks.VulkanProbeRunnerTests"`
Expected: 6 passed.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Core/Checks/Vulkan/VulkanProbeRunner.cs tests/PS5Kitsune.Core.Tests/Checks/VulkanProbeRunnerTests.cs
git commit -m "Run the Vulkan probe in a child process with a Dozen retry

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: CPU facts

**Files:**
- Create: `src/PS5Kitsune.Core/Checks/CpuInfo.cs`
- Delete: `src/PS5Kitsune.Core/Checks/CpuVendor.cs`, `tests/PS5Kitsune.Core.Tests/Checks/CpuVendorTests.cs`
- Modify: `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`
- Modify: `src/PS5Kitsune.Cli/AddCommand.cs:18-21`
- Test: `tests/PS5Kitsune.Core.Tests/Checks/CpuInfoTests.cs`

**Interfaces:**
- Produces (namespace `PS5Kitsune.Core.Checks`): `record CpuFacts(string Vendor, IReadOnlyList<string> MissingExtensions)` with `bool IsIntel`; `interface ICpuInfo { CpuFacts Read(); }`; `class X86CpuInfo : ICpuInfo` with `static IReadOnlyList<string> RequiredExtensions`. Registered as a singleton `ICpuInfo` in `AddPS5KitsuneCore`.

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.Core.Tests/Checks/CpuInfoTests.cs`:

```csharp
using System.Runtime.Intrinsics.X86;
using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.Core.Tests.Checks;

public sealed class CpuInfoTests
{
    [Fact]
    public void Reads_a_twelve_character_vendor_on_x86()
    {
        Assert.SkipUnless(X86Base.IsSupported, "Not an x86 CPU.");

        var vendor = new X86CpuInfo().Read().Vendor;

        Assert.Equal(12, vendor.Length);
        Assert.All(vendor, character => Assert.True(char.IsAsciiLetterOrDigit(character) || character == ' '));
    }

    // The development laptop and the CI runners all have these extensions; this checks the detection itself.
    [Fact]
    public void A_modern_x86_cpu_misses_no_extension()
    {
        Assert.SkipUnless(X86Base.IsSupported, "Not an x86 CPU.");

        Assert.Empty(new X86CpuInfo().Read().MissingExtensions);
    }

    [Theory]
    [InlineData("GenuineIntel", true)]
    [InlineData("AuthenticAMD", false)]
    [InlineData("unknown", false)]
    public void Only_GenuineIntel_is_Intel(string vendor, bool expected)
    {
        Assert.Equal(expected, new CpuFacts(vendor, []).IsIntel);
    }

    [Fact]
    public void Lists_the_seven_required_extensions()
    {
        Assert.Equal(["AVX2", "FMA", "F16C", "BMI1", "BMI2", "LZCNT", "MOVBE"], X86CpuInfo.RequiredExtensions);
    }
}
```

Delete `tests/PS5Kitsune.Core.Tests/Checks/CpuVendorTests.cs` (its cases move here).

- [ ] **Step 2: Run and see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build error, `X86CpuInfo` not found.

- [ ] **Step 3: Implement**

Delete `src/PS5Kitsune.Core/Checks/CpuVendor.cs`. Create `src/PS5Kitsune.Core/Checks/CpuInfo.cs`:

```csharp
using System.Buffers.Binary;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace PS5Kitsune.Core.Checks;

/// <param name="Vendor">CPUID vendor string, for example "AuthenticAMD"; "unknown" off x86.</param>
/// <param name="MissingExtensions">Required extensions this CPU lacks, in <see cref="X86CpuInfo.RequiredExtensions"/> order.</param>
public sealed record CpuFacts(string Vendor, IReadOnlyList<string> MissingExtensions)
{
    /// <summary>True for Intel CPUs, which need the relinker's --to-intel option.</summary>
    public bool IsIntel => Vendor == "GenuineIntel";
}

public interface ICpuInfo
{
    CpuFacts Read();
}

/// <summary>Reads the CPU through .NET intrinsics and CPUID.</summary>
public sealed class X86CpuInfo : ICpuInfo
{
    /// <summary>Extensions converted games use; a game stops at startup when one is missing.</summary>
    public static IReadOnlyList<string> RequiredExtensions { get; } = ["AVX2", "FMA", "F16C", "BMI1", "BMI2", "LZCNT", "MOVBE"];

    public CpuFacts Read() => new(ReadVendor(), MissingExtensions());

    private static string ReadVendor()
    {
        if (!X86Base.IsSupported)
        {
            return "unknown";
        }

        // The vendor string is EBX, EDX, ECX in that order, 4 ASCII bytes each.
        var (_, ebx, ecx, edx) = X86Base.CpuId(0, 0);
        Span<byte> vendor = stackalloc byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(vendor, ebx);
        BinaryPrimitives.WriteInt32LittleEndian(vendor[4..], edx);
        BinaryPrimitives.WriteInt32LittleEndian(vendor[8..], ecx);
        return Encoding.ASCII.GetString(vendor);
    }

    private static List<string> MissingExtensions()
    {
        if (!X86Base.IsSupported)
        {
            return [.. RequiredExtensions];
        }

        // .NET has no IsSupported for F16C and MOVBE: CPUID leaf 1, ECX bits 29 and 22.
        var (_, _, ecx, _) = X86Base.CpuId(1, 0);
        var present = new Dictionary<string, bool>
        {
            ["AVX2"] = Avx2.IsSupported,
            ["FMA"] = Fma.IsSupported,
            ["F16C"] = (ecx & (1 << 29)) != 0,
            ["BMI1"] = Bmi1.IsSupported,
            ["BMI2"] = Bmi2.IsSupported,
            ["LZCNT"] = Lzcnt.IsSupported,
            ["MOVBE"] = (ecx & (1 << 22)) != 0,
        };
        return [.. RequiredExtensions.Where(extension => !present[extension])];
    }
}
```

In `ServiceCollectionExtensions.AddPS5KitsuneCore`, after `services.AddSingleton(TimeProvider.System);`, add (and `using PS5Kitsune.Core.Checks;` at the top):

```csharp
        services.AddSingleton<ICpuInfo, X86CpuInfo>();
```

In `src/PS5Kitsune.Cli/AddCommand.cs`, replace:

```csharp
            var vendor = CpuVendor.Detect();
            var toIntel = CpuVendor.IsIntel(vendor);
            var toIntelText = toIntel ? "yes" : "no";
            logger.CpuDetected(vendor, toIntelText);
```

with:

```csharp
            var cpu = services.GetRequiredService<ICpuInfo>().Read();
            var vendor = cpu.Vendor;
            var toIntel = cpu.IsIntel;
            var toIntelText = toIntel ? "yes" : "no";
            logger.CpuDetected(vendor, toIntelText);
```

- [ ] **Step 4: Run the tests and see them pass**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: build succeeds (no remaining `CpuVendor` reference), all tests pass.

- [ ] **Step 5: Commit**

```bash
git add -A src/PS5Kitsune.Core/Checks src/PS5Kitsune.Core/ServiceCollectionExtensions.cs src/PS5Kitsune.Cli/AddCommand.cs tests/PS5Kitsune.Core.Tests/Checks
git commit -m "Read the CPU vendor and required extensions through ICpuInfo

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Platform facts (commit limit, driver manifests, fixes)

**Files:**
- Create: `src/PS5Kitsune.Core/Checks/CheckModel.cs` (only `CheckFix` in this task; Task 6 adds the rest of the file)
- Modify: `src/PS5Kitsune.Core/Abstractions/IPlatform.cs`
- Modify: `src/PS5Kitsune.Platform.Windows/WindowsPlatform.cs`
- Modify: `tests/PS5Kitsune.Core.Tests/Fakes/FakePlatform.cs`
- Test: `tests/PS5Kitsune.Platform.Windows.Tests/WindowsPlatformTests.cs`

**Interfaces:**
- Produces: `enum CheckFix { OpenPerformanceSettings }` (namespace `PS5Kitsune.Core.Checks`); on `IPlatform`: `ulong? GetCommitLimitBytes()`, `IReadOnlyList<string> GetVulkanDriverManifests()`, `void OpenFix(CheckFix fix)`. `FakePlatform` gains settable `CommitLimitBytes`, `VulkanDriverManifests` and a `List<CheckFix> OpenedFixes`.

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.Platform.Windows.Tests/WindowsPlatformTests.cs`:

```csharp
using System.Runtime.Versioning;

namespace PS5Kitsune.Platform.Windows.Tests;

[SupportedOSPlatform("windows")]
public sealed class WindowsPlatformTests
{
    private readonly WindowsPlatform _platform = new();

    [Fact]
    public void Commit_limit_is_at_least_the_physical_memory()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");

        var commitLimit = _platform.GetCommitLimitBytes();

        Assert.NotNull(commitLimit);
        Assert.True(commitLimit >= (ulong)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 2, $"Commit limit {commitLimit} looks too small.");
    }

    [Fact]
    public void Driver_manifests_can_be_listed()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");

        var manifests = _platform.GetVulkanDriverManifests();

        Assert.All(manifests, manifest => Assert.False(string.IsNullOrWhiteSpace(manifest)));
    }
}
```

- [ ] **Step 2: Run and see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build error, `GetCommitLimitBytes` not found.

- [ ] **Step 3: Implement**

`src/PS5Kitsune.Core/Checks/CheckModel.cs`:

```csharp
namespace PS5Kitsune.Core.Checks;

/// <summary>A fix the host can offer as a button; the platform knows how to open it.</summary>
public enum CheckFix
{
    /// <summary>Windows "Performance Options", where the page file is set.</summary>
    OpenPerformanceSettings,
}
```

In `IPlatform.cs`, add `using PS5Kitsune.Core.Checks;` and, after `ExecutableSuffix`:

```csharp

    /// <summary>Memory the OS can commit (RAM + page file), in bytes; null where the OS has no such limit.</summary>
    ulong? GetCommitLimitBytes();

    /// <summary>Registered Vulkan driver manifests (ICD JSON paths); empty when none are registered this way.</summary>
    IReadOnlyList<string> GetVulkanDriverManifests();

    /// <summary>Opens what the user needs to apply a check's fix, for example a settings dialog.</summary>
    void OpenFix(CheckFix fix);
```

Replace `src/PS5Kitsune.Platform.Windows/WindowsPlatform.cs` with:

```csharp
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed partial class WindowsPlatform : IPlatform
{
    public string Name => "Windows";

    /// <summary>
    /// C:\PS5Kitsune rather than %LOCALAPPDATA%: the user name can contain a space,
    /// which breaks WinLibs and CMake.
    /// </summary>
    public string DefaultDataRoot => Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\", "PS5Kitsune");

    public string SettingsDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PS5Kitsune");

    public string ExecutableSuffix => ".exe";

    /// <summary>ullTotalPageFile is the commit limit (RAM + page files), despite its name.</summary>
    public ulong? GetCommitLimitBytes()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        return status.TotalPageFile;
    }

    /// <summary>
    /// Manifests registered under HKLM\SOFTWARE\Khronos\Vulkan\Drivers, where Mesa (Dozen, Lavapipe) registers.
    /// GPU vendors' drivers register under their display adapter key instead; the probe sees those.
    /// </summary>
    public IReadOnlyList<string> GetVulkanDriverManifests()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Khronos\Vulkan\Drivers");
        return key?.GetValueNames() ?? [];
    }

    public void OpenFix(CheckFix fix)
    {
        switch (fix)
        {
            case CheckFix.OpenPerformanceSettings:
                using (Process.Start(new ProcessStartInfo("SystemPropertiesPerformance.exe") { UseShellExecute = true }))
                {
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(fix), fix, "Unknown fix.");
        }
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

#pragma warning disable CS0649 // Native layout: Windows fills fields the C# code never reads.
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }
#pragma warning restore CS0649
}
```

Replace `tests/PS5Kitsune.Core.Tests/Fakes/FakePlatform.cs` with:

```csharp
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.Core.Tests.Fakes;

internal sealed class FakePlatform : IPlatform
{
    public string Name => "Test";

    public string DefaultDataRoot => "unused";

    public string SettingsDirectory => "unused";

    public string ExecutableSuffix => ".exe";

    public ulong? CommitLimitBytes { get; set; } = 32UL << 30;

    public IReadOnlyList<string> VulkanDriverManifests { get; set; } = [];

    public List<CheckFix> OpenedFixes { get; } = [];

    public ulong? GetCommitLimitBytes() => CommitLimitBytes;

    public IReadOnlyList<string> GetVulkanDriverManifests() => VulkanDriverManifests;

    public void OpenFix(CheckFix fix) => OpenedFixes.Add(fix);
}
```

- [ ] **Step 4: Run the tests and see them pass**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: all pass; the two `WindowsPlatformTests` pass on Windows.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Core/Checks/CheckModel.cs src/PS5Kitsune.Core/Abstractions/IPlatform.cs src/PS5Kitsune.Platform.Windows/WindowsPlatform.cs tests/PS5Kitsune.Core.Tests/Fakes/FakePlatform.cs tests/PS5Kitsune.Platform.Windows.Tests/WindowsPlatformTests.cs
git commit -m "Read the commit limit and Vulkan driver manifests on Windows

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Check model and the hardware and storage checks

**Files:**
- Modify: `src/PS5Kitsune.Core/Checks/CheckModel.cs`
- Create: `src/PS5Kitsune.Core/Checks/SystemFacts.cs`
- Create: `src/PS5Kitsune.Core/Checks/HardwareChecks.cs`
- Create: `src/PS5Kitsune.Core/Checks/StorageChecks.cs`
- Create: `tests/PS5Kitsune.Core.Tests/Checks/TestFacts.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Checks/HardwareChecksTests.cs`, `tests/PS5Kitsune.Core.Tests/Checks/StorageChecksTests.cs`

**Interfaces:**
- Consumes: `CpuFacts` (Task 4), `VulkanProbeOutcome` (Task 3), `CheckThresholds` (Task 1), `CheckFix` (Task 5).
- Produces (namespace `PS5Kitsune.Core.Checks`):
  - `enum CheckStatus { Ok, Info, Warning, Blocking }` (ordered: higher is worse).
  - `record CheckResult(string Id, string Title, CheckStatus Status, string Summary, string? Action = null, CheckFix? Fix = null)`.
  - `interface ISystemCheck { string Id; string Title; CheckResult Evaluate(SystemFacts facts); }`; `abstract class SystemCheck : ISystemCheck` with `protected CheckResult Result(CheckStatus, string summary, string? action = null, CheckFix? fix = null)`.
  - `record SystemFacts { Architecture OSArchitecture; CpuFacts Cpu; VulkanProbeOutcome Vulkan; ulong? CommitLimitBytes; IReadOnlyList<string> VulkanDriverManifests; string DataRoot; long? FreeDiskBytes; CheckThresholds Limits; }`
  - Checks: `OsArchitectureCheck` ("os-arch"), `CpuExtensionsCheck` ("cpu-extensions"), `CpuVendorCheck` ("cpu-vendor"), `CommitLimitCheck` ("commit-limit"), `DiskSpaceCheck` ("disk-space"), `InstallPathCheck` ("install-path").
  - Test helper `TestFacts.Healthy()` (all-OK facts with one AMD GPU that has both features).

- [ ] **Step 1: Write the test helper and the failing tests**

`tests/PS5Kitsune.Core.Tests/Checks/TestFacts.cs`:

```csharp
using System.Runtime.InteropServices;
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Checks.Vulkan;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Tests.Checks;

internal static class TestFacts
{
    public static VulkanGpu Radeon { get; } = new("AMD Radeon(TM) Graphics", 0x1002, "integrated", "1.3.280", "AmdProprietary", "AMD proprietary driver", "25.10.1", true, true);

    public static VulkanGpu IrisXe { get; } = new("Intel(R) Iris(R) Xe Graphics", 0x8086, "integrated", "1.3.289", "IntelProprietaryWindows", "Intel Corporation", "101.5762", false, true);

    public static VulkanGpu GeForce { get; } = new("NVIDIA GeForce RTX 3050 Laptop GPU", VulkanGpu.NvidiaVendorId, "discrete", "1.4.303", "NvidiaProprietary", "NVIDIA", "581.29", true, true);

    public static VulkanProbeOutcome Probe(params VulkanGpu[] gpus) => new(VulkanProbeStatus.Succeeded, new VulkanProbeReport(true, null, gpus), null);

    /// <summary>A PC on which every check is OK (cpu-vendor is always Info).</summary>
    public static SystemFacts Healthy() => new()
    {
        OSArchitecture = Architecture.X64,
        Cpu = new CpuFacts("AuthenticAMD", []),
        Vulkan = Probe(Radeon),
        CommitLimitBytes = 32UL << 30,
        DataRoot = @"C:\PS5Kitsune",
        FreeDiskBytes = 100L << 30,
        Limits = new CheckThresholds { NvidiaBadDrivers = ["610.47"] },
    };
}
```

`tests/PS5Kitsune.Core.Tests/Checks/HardwareChecksTests.cs`:

```csharp
using System.Runtime.InteropServices;
using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.Core.Tests.Checks;

public sealed class HardwareChecksTests
{
    [Fact]
    public void X64_is_ok()
    {
        var result = new OsArchitectureCheck().Evaluate(TestFacts.Healthy());

        Assert.Equal(CheckStatus.Ok, result.Status);
        Assert.Equal("os-arch", result.Id);
    }

    [Fact]
    public void Arm64_is_blocking()
    {
        var result = new OsArchitectureCheck().Evaluate(TestFacts.Healthy() with { OSArchitecture = Architecture.Arm64 });

        Assert.Equal(CheckStatus.Blocking, result.Status);
        Assert.Contains("Arm64", result.Summary, StringComparison.Ordinal);
        Assert.NotNull(result.Action);
    }

    [Fact]
    public void All_extensions_present_is_ok()
    {
        Assert.Equal(CheckStatus.Ok, new CpuExtensionsCheck().Evaluate(TestFacts.Healthy()).Status);
    }

    [Fact]
    public void A_missing_extension_is_blocking_and_named()
    {
        var facts = TestFacts.Healthy() with { Cpu = new CpuFacts("GenuineIntel", ["F16C", "MOVBE"]) };

        var result = new CpuExtensionsCheck().Evaluate(facts);

        Assert.Equal(CheckStatus.Blocking, result.Status);
        Assert.Contains("F16C, MOVBE", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Intel_is_info_and_mentions_to_intel()
    {
        var result = new CpuVendorCheck().Evaluate(TestFacts.Healthy() with { Cpu = new CpuFacts("GenuineIntel", []) });

        Assert.Equal(CheckStatus.Info, result.Status);
        Assert.Contains("--to-intel", result.Summary, StringComparison.Ordinal);
        Assert.Contains("AMD", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Amd_is_info()
    {
        var result = new CpuVendorCheck().Evaluate(TestFacts.Healthy());

        Assert.Equal(CheckStatus.Info, result.Status);
        Assert.Contains("AMD", result.Summary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(13824, CheckStatus.Ok)]
    [InlineData(13823, CheckStatus.Warning)]
    public void Commit_limit_threshold(long mib, CheckStatus expected)
    {
        var facts = TestFacts.Healthy() with { CommitLimitBytes = (ulong)mib << 20 };

        var result = new CommitLimitCheck().Evaluate(facts);

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public void A_low_commit_limit_offers_the_performance_settings()
    {
        var facts = TestFacts.Healthy() with { CommitLimitBytes = 8UL << 30 };

        var result = new CommitLimitCheck().Evaluate(facts);

        Assert.Equal(CheckFix.OpenPerformanceSettings, result.Fix);
        Assert.Contains("8,192", result.Summary, StringComparison.Ordinal);
        Assert.Contains("13,824", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void No_commit_limit_on_this_os_is_info()
    {
        var result = new CommitLimitCheck().Evaluate(TestFacts.Healthy() with { CommitLimitBytes = null });

        Assert.Equal(CheckStatus.Info, result.Status);
    }
}
```

`tests/PS5Kitsune.Core.Tests/Checks/StorageChecksTests.cs`:

```csharp
using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.Core.Tests.Checks;

public sealed class StorageChecksTests
{
    [Theory]
    [InlineData(@"C:\PS5Kitsune", CheckStatus.Ok)]
    [InlineData(@"D:\Games\PS5", CheckStatus.Ok)]
    [InlineData(@"C:\PS5 Kitsune", CheckStatus.Blocking)]
    [InlineData(@"C:\Jeux\Café", CheckStatus.Blocking)]
    public void Install_path_must_be_ascii_without_spaces(string root, CheckStatus expected)
    {
        var result = new InstallPathCheck().Evaluate(TestFacts.Healthy() with { DataRoot = root });

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public void A_blocked_path_names_the_path_and_suggests_one()
    {
        var result = new InstallPathCheck().Evaluate(TestFacts.Healthy() with { DataRoot = @"C:\PS5 Kitsune" });

        Assert.Contains(@"C:\PS5 Kitsune", result.Summary, StringComparison.Ordinal);
        Assert.Contains(@"C:\PS5Kitsune", result.Action, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(5L << 30, CheckStatus.Ok)]
    [InlineData((5L << 30) - 1, CheckStatus.Warning)]
    public void Disk_space_threshold(long free, CheckStatus expected)
    {
        var result = new DiskSpaceCheck().Evaluate(TestFacts.Healthy() with { FreeDiskBytes = free });

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public void Low_disk_space_shows_the_free_space()
    {
        var result = new DiskSpaceCheck().Evaluate(TestFacts.Healthy() with { FreeDiskBytes = 3L << 30 });

        Assert.Contains("3.0 GiB", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_disk_space_is_info()
    {
        var result = new DiskSpaceCheck().Evaluate(TestFacts.Healthy() with { FreeDiskBytes = null });

        Assert.Equal(CheckStatus.Info, result.Status);
    }
}
```

- [ ] **Step 2: Run and see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build error, `SystemFacts` not found.

- [ ] **Step 3: Implement the model**

Replace `src/PS5Kitsune.Core/Checks/CheckModel.cs` with:

```csharp
namespace PS5Kitsune.Core.Checks;

/// <summary>Ordered from best to worst: the overall status of a report is the highest one.</summary>
public enum CheckStatus
{
    Ok,
    Info,
    Warning,
    Blocking,
}

/// <summary>A fix the host can offer as a button; the platform knows how to open it.</summary>
public enum CheckFix
{
    /// <summary>Windows "Performance Options", where the page file is set.</summary>
    OpenPerformanceSettings,
}

/// <param name="Summary">What was found, in one or two sentences.</param>
/// <param name="Action">What the user should do; null when nothing is needed.</param>
/// <param name="Fix">A button the host can offer; null when there is none.</param>
public sealed record CheckResult(string Id, string Title, CheckStatus Status, string Summary, string? Action = null, CheckFix? Fix = null);

/// <summary>One PC check: a pure function of the facts collected once for all checks.</summary>
public interface ISystemCheck
{
    string Id { get; }

    string Title { get; }

    CheckResult Evaluate(SystemFacts facts);
}

public abstract class SystemCheck : ISystemCheck
{
    public abstract string Id { get; }

    public abstract string Title { get; }

    public abstract CheckResult Evaluate(SystemFacts facts);

    protected CheckResult Result(CheckStatus status, string summary, string? action = null, CheckFix? fix = null) =>
        new(Id, Title, status, summary, action, fix);
}
```

`src/PS5Kitsune.Core/Checks/SystemFacts.cs`:

```csharp
using System.Runtime.InteropServices;
using PS5Kitsune.Core.Checks.Vulkan;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Checks;

/// <summary>Everything the checks read, collected once by <see cref="SystemFactsCollector"/>.</summary>
public sealed record SystemFacts
{
    public required Architecture OSArchitecture { get; init; }

    public required CpuFacts Cpu { get; init; }

    public required VulkanProbeOutcome Vulkan { get; init; }

    /// <summary>RAM + page file, in bytes; null where the OS has no commit limit.</summary>
    public ulong? CommitLimitBytes { get; init; }

    public IReadOnlyList<string> VulkanDriverManifests { get; init; } = [];

    /// <summary>Full path of the data root.</summary>
    public required string DataRoot { get; init; }

    /// <summary>Free space on the data root's drive; null when it could not be read.</summary>
    public long? FreeDiskBytes { get; init; }

    public required CheckThresholds Limits { get; init; }
}
```

- [ ] **Step 4: Implement the checks**

`src/PS5Kitsune.Core/Checks/HardwareChecks.cs`:

```csharp
using System.Globalization;
using System.Runtime.InteropServices;

namespace PS5Kitsune.Core.Checks;

public sealed class OsArchitectureCheck : SystemCheck
{
    public override string Id => "os-arch";

    public override string Title => "64-bit x86 system";

    public override CheckResult Evaluate(SystemFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return facts.OSArchitecture == Architecture.X64
            ? Result(CheckStatus.Ok, "64-bit x86 system.")
            : Result(CheckStatus.Blocking, $"This is an {facts.OSArchitecture} system; AnyPS5 produces x86-64 code only.", "Use a PC with a 64-bit Intel or AMD processor.");
    }
}

public sealed class CpuExtensionsCheck : SystemCheck
{
    public override string Id => "cpu-extensions";

    public override string Title => "CPU instruction sets";

    public override CheckResult Evaluate(SystemFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var missing = facts.Cpu.MissingExtensions;
        return missing.Count == 0
            ? Result(CheckStatus.Ok, $"{string.Join(", ", X86CpuInfo.RequiredExtensions)} are present.")
            : Result(CheckStatus.Blocking, $"Missing: {string.Join(", ", missing)}. A converted game stops at startup without them.", "Use a PC with a newer processor: Intel 4th generation (Haswell) or AMD Ryzen and later.");
    }
}

public sealed class CpuVendorCheck : SystemCheck
{
    public override string Id => "cpu-vendor";

    public override string Title => "CPU vendor";

    public override CheckResult Evaluate(SystemFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return facts.Cpu.Vendor switch
        {
            "GenuineIntel" => Result(CheckStatus.Info, "Intel processor: games are converted with --to-intel. Games that generate AMD instructions while running still need an AMD processor."),
            "AuthenticAMD" => Result(CheckStatus.Info, "AMD processor: games are converted without --to-intel."),
            var vendor => Result(CheckStatus.Info, $"Processor vendor \"{vendor}\": games are converted without --to-intel."),
        };
    }
}

public sealed class CommitLimitCheck : SystemCheck
{
    private const ulong MiB = 1 << 20;

    public override string Id => "commit-limit";

    public override string Title => "Memory commit limit (RAM + page file)";

    public override CheckResult Evaluate(SystemFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.CommitLimitBytes is not { } bytes)
        {
            return Result(CheckStatus.Info, "Not checked on this system.");
        }

        var available = bytes / MiB;
        var needed = (ulong)facts.Limits.CommitLimitMiB;
        return available >= needed
            ? Result(CheckStatus.Ok, string.Create(CultureInfo.InvariantCulture, $"{available:N0} MiB available; a game can commit up to {needed:N0} MiB."))
            : Result(
                CheckStatus.Warning,
                string.Create(CultureInfo.InvariantCulture, $"RAM + page file allow {available:N0} MiB; a game can commit up to {needed:N0} MiB and may crash with less."),
                string.Create(CultureInfo.InvariantCulture, $"Enlarge the page file until RAM + page file reach {needed:N0} MiB: Performance Options > Advanced > Virtual memory > Change."),
                CheckFix.OpenPerformanceSettings);
    }
}
```

`src/PS5Kitsune.Core/Checks/StorageChecks.cs`:

```csharp
using System.Globalization;

namespace PS5Kitsune.Core.Checks;

public sealed class DiskSpaceCheck : SystemCheck
{
    private const double GiB = 1L << 30;

    public override string Id => "disk-space";

    public override string Title => "Free disk space";

    public override CheckResult Evaluate(SystemFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.FreeDiskBytes is not { } bytes)
        {
            return Result(CheckStatus.Info, "Could not read the free space of the data folder's drive.");
        }

        var free = bytes / GiB;
        var needed = facts.Limits.MinFreeDiskGiB;
        return free >= needed
            ? Result(CheckStatus.Ok, string.Create(CultureInfo.InvariantCulture, $"{free:0.0} GiB free on the data folder's drive."))
            : Result(
                CheckStatus.Warning,
                string.Create(CultureInfo.InvariantCulture, $"{free:0.0} GiB free on the data folder's drive; AnyPS5 and a game need about {needed} GiB."),
                "Free some space, or choose a data folder on another drive.");
    }
}

public sealed class InstallPathCheck : SystemCheck
{
    public override string Id => "install-path";

    public override string Title => "Data folder path";

    public override CheckResult Evaluate(SystemFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var root = facts.DataRoot;
        return root.All(character => char.IsAscii(character) && character != ' ')
            ? Result(CheckStatus.Ok, root)
            : Result(
                CheckStatus.Blocking,
                $"{root} contains spaces or non-ASCII characters; WinLibs and CMake fail on such paths.",
                @"Choose a data folder without spaces or accents, for example C:\PS5Kitsune (command line: --root).");
    }
}
```

- [ ] **Step 5: Run the tests and see them pass**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.Core.Tests.Checks.HardwareChecksTests" --filter-class "PS5Kitsune.Core.Tests.Checks.StorageChecksTests"`
Expected: all pass.

- [ ] **Step 6: Commit**

```bash
git add src/PS5Kitsune.Core/Checks tests/PS5Kitsune.Core.Tests/Checks
git commit -m "Add the check model and the hardware and storage checks

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Vulkan checks

**Files:**
- Create: `src/PS5Kitsune.Core/Checks/VulkanChecks.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Checks/VulkanChecksTests.cs`

**Interfaces:**
- Consumes: `SystemCheck`, `SystemFacts`, `CheckStatus` (Task 6); `VulkanProbeOutcome`, `VulkanProbeStatus`, `VulkanGpu` (Tasks 2-3); `TestFacts` (Task 6).
- Produces: `VulkanDriverCheck` ("vulkan-driver"), `VulkanFeaturesCheck` ("vulkan-features"), `DozenCheck` ("dozen"), `NvidiaDriverCheck` ("nvidia-driver").

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.Core.Tests/Checks/VulkanChecksTests.cs`:

```csharp
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Checks.Vulkan;

namespace PS5Kitsune.Core.Tests.Checks;

public sealed class VulkanChecksTests
{
    private static SystemFacts With(VulkanProbeOutcome probe) => TestFacts.Healthy() with { Vulkan = probe };

    [Fact]
    public void A_gpu_makes_the_driver_check_ok()
    {
        var result = new VulkanDriverCheck().Evaluate(TestFacts.Healthy());

        Assert.Equal(CheckStatus.Ok, result.Status);
        Assert.Contains("AMD Radeon(TM) Graphics", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void A_failed_probe_is_blocking_with_its_reason()
    {
        var result = new VulkanDriverCheck().Evaluate(With(VulkanProbeOutcome.Failed("The probe exited with code -1.")));

        Assert.Equal(CheckStatus.Blocking, result.Status);
        Assert.Contains("exited with code -1", result.Summary, StringComparison.Ordinal);
        Assert.NotNull(result.Action);
    }

    [Fact]
    public void A_missing_loader_is_blocking()
    {
        var probe = new VulkanProbeOutcome(VulkanProbeStatus.Succeeded, new VulkanProbeReport(false, "vulkan-1.dll not found", []), null);

        Assert.Equal(CheckStatus.Blocking, new VulkanDriverCheck().Evaluate(With(probe)).Status);
    }

    [Fact]
    public void No_gpu_is_blocking()
    {
        Assert.Equal(CheckStatus.Blocking, new VulkanDriverCheck().Evaluate(With(TestFacts.Probe())).Status);
    }

    [Fact]
    public void Iris_xe_without_shaderInt64_is_a_warning_mentioning_lavapipe()
    {
        var facts = With(new VulkanProbeOutcome(VulkanProbeStatus.Succeeded, VulkanProbeReport.FromJson(VulkanProbeTests.IrisXeJson), null));

        var result = new VulkanFeaturesCheck().Evaluate(facts);

        Assert.Equal(CheckStatus.Warning, result.Status);
        Assert.Contains("Iris(R) Xe", result.Summary, StringComparison.Ordinal);
        Assert.Contains("shaderInt64", result.Summary, StringComparison.Ordinal);
        Assert.Contains("Lavapipe", result.Action, StringComparison.Ordinal);
    }

    [Fact]
    public void Intel_igpu_plus_nvidia_names_the_usable_gpu()
    {
        var result = new VulkanFeaturesCheck().Evaluate(With(TestFacts.Probe(TestFacts.IrisXe, TestFacts.GeForce)));

        Assert.Equal(CheckStatus.Ok, result.Status);
        Assert.StartsWith("NVIDIA GeForce RTX 3050 Laptop GPU", result.Summary, StringComparison.Ordinal);
        Assert.Contains("Iris(R) Xe Graphics lacks shaderInt64", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void A_gpu_with_both_features_is_ok()
    {
        Assert.Equal(CheckStatus.Ok, new VulkanFeaturesCheck().Evaluate(TestFacts.Healthy()).Status);
    }

    [Fact]
    public void Features_and_nvidia_are_skipped_without_a_working_driver()
    {
        var facts = With(VulkanProbeOutcome.Failed("crash"));

        Assert.Equal(CheckStatus.Info, new VulkanFeaturesCheck().Evaluate(facts).Status);
        Assert.Equal(CheckStatus.Info, new NvidiaDriverCheck().Evaluate(facts).Status);
    }

    [Fact]
    public void Nvidia_610_47_is_a_warning()
    {
        var bad = TestFacts.GeForce with { DriverInfo = "610.47" };

        var result = new NvidiaDriverCheck().Evaluate(With(TestFacts.Probe(bad)));

        Assert.Equal(CheckStatus.Warning, result.Status);
        Assert.Contains("610.47", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Another_nvidia_driver_is_ok()
    {
        Assert.Equal(CheckStatus.Ok, new NvidiaDriverCheck().Evaluate(With(TestFacts.Probe(TestFacts.GeForce))).Status);
    }

    [Fact]
    public void No_nvidia_gpu_is_ok()
    {
        Assert.Equal(CheckStatus.Ok, new NvidiaDriverCheck().Evaluate(TestFacts.Healthy()).Status);
    }

    [Fact]
    public void No_dozen_is_ok()
    {
        Assert.Equal(CheckStatus.Ok, new DozenCheck().Evaluate(TestFacts.Healthy()).Status);
    }

    [Fact]
    public void A_dozen_manifest_is_info()
    {
        var facts = TestFacts.Healthy() with { VulkanDriverManifests = [@"C:\Mesa\dzn_icd.x86_64.json"] };

        Assert.Equal(CheckStatus.Info, new DozenCheck().Evaluate(facts).Status);
    }

    [Fact]
    public void A_dozen_gpu_is_info()
    {
        var dozen = TestFacts.Radeon with { Name = "Microsoft Direct3D12 (AMD Radeon)", DriverId = "MesaDozen" };

        Assert.Equal(CheckStatus.Info, new DozenCheck().Evaluate(With(TestFacts.Probe(dozen))).Status);
    }

    [Fact]
    public void A_retry_without_dozen_says_it_crashed()
    {
        var probe = TestFacts.Probe(TestFacts.Radeon) with { Status = VulkanProbeStatus.SucceededWithoutDozen };

        var result = new DozenCheck().Evaluate(With(probe));

        Assert.Equal(CheckStatus.Info, result.Status);
        Assert.Contains("crash", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Dozen_is_still_checked_when_the_probe_failed()
    {
        var facts = With(VulkanProbeOutcome.Failed("crash")) with { VulkanDriverManifests = [@"C:\Mesa\dzn_icd.x86_64.json"] };

        Assert.Equal(CheckStatus.Info, new DozenCheck().Evaluate(facts).Status);
    }
}
```

- [ ] **Step 2: Run and see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build error, `VulkanDriverCheck` not found.

- [ ] **Step 3: Implement**

`src/PS5Kitsune.Core/Checks/VulkanChecks.cs`:

```csharp
using PS5Kitsune.Core.Checks.Vulkan;

namespace PS5Kitsune.Core.Checks;

internal static class VulkanFacts
{
    /// <summary>The GPUs when the driver check passes; null otherwise, so later checks report "not checked".</summary>
    public static IReadOnlyList<VulkanGpu>? WorkingGpus(this SystemFacts facts) =>
        facts.Vulkan.Report is { LoaderFound: true, Gpus.Count: > 0 } report ? report.Gpus : null;

    public static string Names(IEnumerable<VulkanGpu> gpus) => string.Join(", ", gpus.Select(gpu => gpu.Name));
}

public sealed class VulkanDriverCheck : SystemCheck
{
    private const string UpdateDriver = "Install or update the graphics driver from your GPU maker's website (AMD, Intel or NVIDIA).";

    public override string Id => "vulkan-driver";

    public override string Title => "Vulkan driver";

    public override CheckResult Evaluate(SystemFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return facts.Vulkan.Report switch
        {
            null => Result(CheckStatus.Blocking, $"Vulkan enumeration failed. {facts.Vulkan.FailureReason}", UpdateDriver),
            { LoaderFound: false } => Result(CheckStatus.Blocking, "No Vulkan loader was found (vulkan-1.dll).", UpdateDriver),
            { Gpus.Count: 0 } report => Result(CheckStatus.Blocking, report.Error is null ? "Vulkan found no GPU." : $"Vulkan found no GPU: {report.Error}", UpdateDriver),
            var report => Result(CheckStatus.Ok, $"{report.Gpus.Count} GPU(s): {VulkanFacts.Names(report.Gpus)}."),
        };
    }
}

public sealed class VulkanFeaturesCheck : SystemCheck
{
    public override string Id => "vulkan-features";

    public override string Title => "GPU features (shaderInt64, bufferDeviceAddress)";

    public override CheckResult Evaluate(SystemFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.WorkingGpus() is not { } gpus)
        {
            return Result(CheckStatus.Info, "Not checked: no working Vulkan driver.");
        }

        var lacking = gpus.Where(gpu => !gpu.SupportsAnyPS5).Select(gpu => $"{gpu.Name} lacks {Missing(gpu)}").ToList();
        var usable = gpus.FirstOrDefault(gpu => gpu.SupportsAnyPS5);
        if (usable is null)
        {
            return Result(
                CheckStatus.Warning,
                $"{string.Join("; ", lacking)}. Games cannot render on this GPU.",
                "Games can still run with Lavapipe, a slower software renderer that the launcher will offer.");
        }

        return lacking.Count == 0
            ? Result(CheckStatus.Ok, gpus.Count == 1 ? $"{usable.Name} has both features." : "Every GPU has both features.")
            : Result(CheckStatus.Ok, $"{usable.Name} has both features and will run games; {string.Join("; ", lacking)}.");
    }

    private static string Missing(VulkanGpu gpu) => (gpu.ShaderInt64, gpu.BufferDeviceAddress) switch
    {
        (false, false) => "shaderInt64 and bufferDeviceAddress",
        (false, true) => "shaderInt64",
        _ => "bufferDeviceAddress",
    };
}

public sealed class DozenCheck : SystemCheck
{
    public override string Id => "dozen";

    public override string Title => "Dozen driver (Vulkan over Direct3D 12)";

    public override CheckResult Evaluate(SystemFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.Vulkan.Status == VulkanProbeStatus.SucceededWithoutDozen)
        {
            return Result(CheckStatus.Info, "Dozen made Vulkan enumeration crash; it was disabled for the check and the launcher will disable it when it starts a game.");
        }

        var installed = facts.VulkanDriverManifests.Any(manifest => manifest.Contains("dzn", StringComparison.OrdinalIgnoreCase))
            || (facts.Vulkan.Report?.Gpus.Any(gpu => gpu.IsDozen) ?? false);
        return installed
            ? Result(CheckStatus.Info, "Dozen is installed. It can crash Vulkan enumeration; the launcher can disable it when it starts a game.")
            : Result(CheckStatus.Ok, "Not installed.");
    }
}

public sealed class NvidiaDriverCheck : SystemCheck
{
    public override string Id => "nvidia-driver";

    public override string Title => "NVIDIA driver";

    public override CheckResult Evaluate(SystemFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.WorkingGpus() is not { } gpus)
        {
            return Result(CheckStatus.Info, "Not checked: no working Vulkan driver.");
        }

        var nvidia = gpus.Where(gpu => gpu.IsNvidia).ToList();
        if (nvidia.Count == 0)
        {
            return Result(CheckStatus.Ok, "No NVIDIA GPU.");
        }

        var bad = nvidia.FirstOrDefault(gpu => gpu.DriverInfo is { } version && facts.Limits.NvidiaBadDrivers.Contains(version));
        return bad is null
            ? Result(CheckStatus.Ok, $"Driver {string.Join(", ", nvidia.Select(gpu => gpu.DriverInfo ?? "unknown"))}.")
            : Result(CheckStatus.Warning, $"{bad.Name} uses driver {bad.DriverInfo}, known to crash AnyPS5 games (610.47 crashes in nvgpucomp64.dll).", "Install another NVIDIA driver version.");
    }
}
```

- [ ] **Step 4: Run the tests and see them pass**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.Core.Tests.Checks.VulkanChecksTests"`
Expected: 16 passed.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Core/Checks/VulkanChecks.cs tests/PS5Kitsune.Core.Tests/Checks/VulkanChecksTests.cs
git commit -m "Add the Vulkan driver, features, Dozen and NVIDIA checks

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Collector, checker and registration

**Files:**
- Create: `src/PS5Kitsune.Core/Checks/SystemChecker.cs`
- Modify: `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Checks/SystemCheckerTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 1-7; `IPlatform`, `DataLayout`, `AnyPS5Profile`.
- Produces (namespace `PS5Kitsune.Core.Checks`):
  - `class SystemFactsCollector(IPlatform, ICpuInfo, IVulkanProbeRunner, DataLayout, AnyPS5Profile)` with `Task<SystemFacts> CollectAsync(CancellationToken = default)`.
  - `record SystemCheckReport(IReadOnlyList<CheckResult> Results, VulkanProbeOutcome Vulkan)` with `CheckStatus Overall`, `bool HasBlocking`, `string Verdict`.
  - `interface ISystemChecker { Task<SystemCheckReport> RunAsync(CancellationToken cancellationToken = default); }`, `class SystemChecker`.
  - `static class SystemChecks { IReadOnlyList<ISystemCheck> All }` in spec order.
  - Registered in `AddPS5KitsuneCore`: `IVulkanProbeRunner`, `SystemFactsCollector`, `ISystemChecker`, each `ISystemCheck`.

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.Core.Tests/Checks/SystemCheckerTests.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Checks.Vulkan;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.Checks;

public sealed class SystemCheckerTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FakePlatform _platform = new();
    private readonly FakeCpuInfo _cpu = new();
    private readonly FakeProbeRunner _probe = new();

    public void Dispose() => _temp.Dispose();

    private SystemFactsCollector CreateCollector() =>
        new(_platform, _cpu, _probe, new DataLayout(_temp.Path), ProfileLoader.LoadEmbedded());

    [Fact]
    public async Task Collects_every_fact()
    {
        _platform.CommitLimitBytes = 16UL << 30;
        _platform.VulkanDriverManifests = ["dzn_icd.json"];

        var facts = await CreateCollector().CollectAsync(TestContext.Current.CancellationToken);

        Assert.Equal(16UL << 30, facts.CommitLimitBytes);
        Assert.Equal(["dzn_icd.json"], facts.VulkanDriverManifests);
        Assert.Equal(Path.GetFullPath(_temp.Path), facts.DataRoot);
        Assert.True(facts.FreeDiskBytes > 0);
        Assert.Equal(13824, facts.Limits.CommitLimitMiB);
        Assert.Same(_probe.Outcome, facts.Vulkan);
        Assert.Equal("AuthenticAMD", facts.Cpu.Vendor);
    }

    [Fact]
    public async Task Runs_the_checks_in_order_and_reports_the_worst_status()
    {
        _platform.CommitLimitBytes = 8UL << 30;
        var checker = new SystemChecker(CreateCollector(), SystemChecks.All, NullLogger<SystemChecker>.Instance);

        var report = await checker.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            ["os-arch", "cpu-extensions", "cpu-vendor", "vulkan-driver", "vulkan-features", "commit-limit", "dozen", "nvidia-driver", "disk-space", "install-path"],
            report.Results.Select(result => result.Id));
        Assert.Equal(CheckStatus.Warning, report.Overall);
        Assert.False(report.HasBlocking);
        Assert.Same(_probe.Outcome, report.Vulkan);
    }

    [Theory]
    [InlineData(CheckStatus.Ok, "This PC can run AnyPS5.")]
    [InlineData(CheckStatus.Info, "This PC can run AnyPS5.")]
    [InlineData(CheckStatus.Warning, "This PC can run AnyPS5, with warnings.")]
    [InlineData(CheckStatus.Blocking, "Blocking problems: fix them before installing.")]
    public void Verdict_follows_the_worst_status(CheckStatus worst, string verdict)
    {
        var report = new SystemCheckReport([new CheckResult("a", "A", CheckStatus.Ok, "s"), new CheckResult("b", "B", worst, "s")], TestFacts.Probe());

        Assert.Equal(worst, report.Overall);
        Assert.Equal(verdict, report.Verdict);
        Assert.Equal(worst == CheckStatus.Blocking, report.HasBlocking);
    }

    [Fact]
    public void Core_registration_resolves_the_checker()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IPlatform>(_platform);
        services.AddPS5KitsuneCore(new DataLayout(_temp.Path), ProfileLoader.LoadEmbedded());
        using var provider = services.BuildServiceProvider();

        Assert.IsType<SystemChecker>(provider.GetRequiredService<ISystemChecker>());
        Assert.Equal(10, provider.GetServices<ISystemCheck>().Count());
    }

    private sealed class FakeCpuInfo : ICpuInfo
    {
        public CpuFacts Read() => new("AuthenticAMD", []);
    }

    private sealed class FakeProbeRunner : IVulkanProbeRunner
    {
        public VulkanProbeOutcome Outcome { get; } = TestFacts.Probe(TestFacts.Radeon);

        public Task<VulkanProbeOutcome> RunAsync(CancellationToken cancellationToken = default) => Task.FromResult(Outcome);
    }
}
```

- [ ] **Step 2: Run and see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build error, `SystemFactsCollector` not found.

- [ ] **Step 3: Implement**

`src/PS5Kitsune.Core/Checks/SystemChecker.cs`:

```csharp
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Checks.Vulkan;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Checks;

/// <summary>Gathers every fact once, so each check stays a pure function and the probe runs once.</summary>
public sealed class SystemFactsCollector(IPlatform platform, ICpuInfo cpu, IVulkanProbeRunner vulkan, DataLayout layout, AnyPS5Profile profile)
{
    public async Task<SystemFacts> CollectAsync(CancellationToken cancellationToken = default) => new()
    {
        OSArchitecture = RuntimeInformation.OSArchitecture,
        Cpu = cpu.Read(),
        Vulkan = await vulkan.RunAsync(cancellationToken).ConfigureAwait(false),
        CommitLimitBytes = platform.GetCommitLimitBytes(),
        VulkanDriverManifests = platform.GetVulkanDriverManifests(),
        DataRoot = layout.Root,
        FreeDiskBytes = FreeSpace(layout.Root),
        Limits = profile.Checks,
    };

    // The data root may not exist yet: its drive is what matters.
    private static long? FreeSpace(string root)
    {
        try
        {
            return new DriveInfo(Path.GetPathRoot(root) ?? root).AvailableFreeSpace;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}

public sealed record SystemCheckReport(IReadOnlyList<CheckResult> Results, VulkanProbeOutcome Vulkan)
{
    public CheckStatus Overall => Results.Count == 0 ? CheckStatus.Ok : Results.Max(result => result.Status);

    public bool HasBlocking => Overall == CheckStatus.Blocking;

    public string Verdict => Overall switch
    {
        CheckStatus.Blocking => "Blocking problems: fix them before installing.",
        CheckStatus.Warning => "This PC can run AnyPS5, with warnings.",
        _ => "This PC can run AnyPS5.",
    };
}

public interface ISystemChecker
{
    Task<SystemCheckReport> RunAsync(CancellationToken cancellationToken = default);
}

public sealed partial class SystemChecker(SystemFactsCollector collector, IEnumerable<ISystemCheck> checks, ILogger<SystemChecker> logger) : ISystemChecker
{
    public async Task<SystemCheckReport> RunAsync(CancellationToken cancellationToken = default)
    {
        var facts = await collector.CollectAsync(cancellationToken).ConfigureAwait(false);
        var results = checks.Select(check => check.Evaluate(facts)).ToList();
        foreach (var result in results)
        {
            LogResult(result.Id, result.Status, result.Summary);
        }

        return new SystemCheckReport(results, facts.Vulkan);
    }

    // Debug: the hosts print the results themselves; the log file keeps them.
    [LoggerMessage(Level = LogLevel.Debug, Message = "Check {Id}: {Status}. {Summary}")]
    private partial void LogResult(string id, CheckStatus status, string summary);
}

/// <summary>Every check, in the order the hosts show them (see the spec's checks table).</summary>
public static class SystemChecks
{
    public static IReadOnlyList<ISystemCheck> All { get; } =
    [
        new OsArchitectureCheck(),
        new CpuExtensionsCheck(),
        new CpuVendorCheck(),
        new VulkanDriverCheck(),
        new VulkanFeaturesCheck(),
        new CommitLimitCheck(),
        new DozenCheck(),
        new NvidiaDriverCheck(),
        new DiskSpaceCheck(),
        new InstallPathCheck(),
    ];
}
```

In `ServiceCollectionExtensions.AddPS5KitsuneCore`, after `services.AddSingleton<ICpuInfo, X86CpuInfo>();` (add `using PS5Kitsune.Core.Checks.Vulkan;`):

```csharp
        services.AddSingleton<IVulkanProbeRunner, VulkanProbeRunner>();
        services.AddSingleton<SystemFactsCollector>();
        services.AddSingleton<ISystemChecker, SystemChecker>();
        foreach (var check in SystemChecks.All)
        {
            // Registered against the interface: SystemChecker receives them as IEnumerable<ISystemCheck>, in this order.
            services.AddSingleton<ISystemCheck>(check);
        }
```

- [ ] **Step 4: Run the tests and see them pass**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Core/Checks/SystemChecker.cs src/PS5Kitsune.Core/ServiceCollectionExtensions.cs tests/PS5Kitsune.Core.Tests/Checks/SystemCheckerTests.cs
git commit -m "Collect the system facts once and run every check

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: CLI — probe entry, `check`, install gate

**Files:**
- Modify: `src/PS5Kitsune.Cli/Program.cs`
- Create: `src/PS5Kitsune.Cli/CheckCommand.cs`
- Create: `src/PS5Kitsune.Cli/CheckReportPrinter.cs`
- Modify: `src/PS5Kitsune.Cli/InstallCommand.cs`
- Modify: `src/PS5Kitsune.Cli/CliLog.cs`

**Interfaces:**
- Consumes: `VulkanProbe.Run`, `VulkanProbeReport.ToJson`, `VulkanProbeRunner.ProbeArgument`, `ISystemChecker`, `SystemCheckReport`, `CheckStatus`, `CheckFix`, `VulkanGpu.Describe`.
- Produces: `ps5kitsune check`, exit code 2 on a blocking result (`CheckReportPrinter.BlockingExitCode`).

No CLI test project exists; this task is verified by running the commands.

- [ ] **Step 1: Probe entry point**

At the very top of `src/PS5Kitsune.Cli/Program.cs`, below the `using` lines, insert:

```csharp
using PS5Kitsune.Core.Checks.Vulkan;

// The Vulkan probe child (see VulkanProbeRunner): nothing else is loaded, so a crashing driver only takes this process down.
if (args is [VulkanProbeRunner.ProbeArgument])
{
    Console.Out.Write(VulkanProbe.Run().ToJson());
    return 0;
}

```

so the file starts with `using System.CommandLine;`, `using PS5Kitsune.Cli;`, `using PS5Kitsune.Core.Checks.Vulkan;`, then this block, then `var rootOption = ...`. Add `CheckCommand.Create(rootOption),` to the `RootCommand` initializer, before `InstallCommand.Create(rootOption),`.

- [ ] **Step 2: Log messages**

Append to `CliLog`:

```csharp
    [LoggerMessage(Level = LogLevel.Information, Message = "[{Badge}] {Title}: {Summary}")]
    public static partial void CheckPassed(this ILogger logger, string badge, string title, string summary);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[WARN] {Title}: {Summary}")]
    public static partial void CheckWarning(this ILogger logger, string title, string summary);

    [LoggerMessage(Level = LogLevel.Error, Message = "[BLOCK] {Title}: {Summary}")]
    public static partial void CheckBlocking(this ILogger logger, string title, string summary);

    [LoggerMessage(Level = LogLevel.Information, Message = "    -> {Action}")]
    public static partial void CheckAction(this ILogger logger, string action);

    [LoggerMessage(Level = LogLevel.Information, Message = "GPU: {Description}")]
    public static partial void GpuFound(this ILogger logger, string description);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Verdict}")]
    public static partial void CheckVerdict(this ILogger logger, string verdict);

    [LoggerMessage(Level = LogLevel.Error, Message = "Installation stopped before downloading anything. Fix the blocking problems above, then run 'ps5kitsune install' again; 'ps5kitsune check' shows every check.")]
    public static partial void InstallBlocked(this ILogger logger);
```

- [ ] **Step 3: Printer**

`src/PS5Kitsune.Cli/CheckReportPrinter.cs`:

```csharp
using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.Cli;

internal static class CheckReportPrinter
{
    /// <summary>Exit code of check and install when a check is blocking.</summary>
    public const int BlockingExitCode = 2;

    /// <param name="onlyProblems">True to print Warning and Blocking results only (install).</param>
    public static void Print(ILogger logger, SystemCheckReport report, bool onlyProblems)
    {
        foreach (var result in report.Results)
        {
            if (onlyProblems && result.Status < CheckStatus.Warning)
            {
                continue;
            }

            var title = result.Title;
            var summary = result.Summary;
            switch (result.Status)
            {
                case CheckStatus.Blocking:
                    logger.CheckBlocking(title, summary);
                    break;
                case CheckStatus.Warning:
                    logger.CheckWarning(title, summary);
                    break;
                default:
                    logger.CheckPassed(result.Status == CheckStatus.Ok ? "OK" : "INFO", title, summary);
                    break;
            }

            if (result.Action is { } action)
            {
                logger.CheckAction(result.Fix == CheckFix.OpenPerformanceSettings ? $"{action} Run SystemPropertiesPerformance.exe." : action);
            }
        }
    }

    public static void PrintGpus(ILogger logger, SystemCheckReport report)
    {
        foreach (var gpu in report.Vulkan.Report?.Gpus ?? [])
        {
            logger.GpuFound(gpu.Describe());
        }
    }
}
```

- [ ] **Step 4: `check` command**

`src/PS5Kitsune.Cli/CheckCommand.cs`:

```csharp
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.Cli;

internal static class CheckCommand
{
    public static Command Create(Option<DirectoryInfo?> rootOption)
    {
        var command = new Command("check", "Check that this PC can run AnyPS5: CPU, Vulkan driver and GPU features, memory, disk and data folder.");
        command.SetAction((parseResult, cancellationToken) => CommandHost.RunAsync(parseResult.GetValue(rootOption), "check", async (services, logger) =>
        {
            var report = await services.GetRequiredService<ISystemChecker>().RunAsync(cancellationToken);
            CheckReportPrinter.Print(logger, report, onlyProblems: false);
            CheckReportPrinter.PrintGpus(logger, report);
            logger.CheckVerdict(report.Verdict);
            return report.HasBlocking ? CheckReportPrinter.BlockingExitCode : 0;
        }));
        return command;
    }
}
```

- [ ] **Step 5: Install gate**

In `InstallCommand.cs`, add `using PS5Kitsune.Core.Checks;` and replace the action body's first line:

```csharp
            await services.GetRequiredService<AnyPS5Installer>().InstallAsync(new ConsoleStepProgress(), cancellationToken: cancellationToken);
```

with:

```csharp
            var report = await services.GetRequiredService<ISystemChecker>().RunAsync(cancellationToken);
            CheckReportPrinter.Print(logger, report, onlyProblems: true);
            if (report.HasBlocking)
            {
                logger.InstallBlocked();
                return CheckReportPrinter.BlockingExitCode;
            }

            await services.GetRequiredService<AnyPS5Installer>().InstallAsync(new ConsoleStepProgress(), cancellationToken: cancellationToken);
```

- [ ] **Step 6: Build and run**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: build succeeds, all tests pass.

Run: `src/PS5Kitsune.Cli/bin/Release/net10.0/ps5kitsune.exe --probe-vulkan`
Expected: one line of JSON with `"loaderFound":true` and the Radeon GPU.

Run: `src/PS5Kitsune.Cli/bin/Release/net10.0/ps5kitsune.exe check; echo "exit $?"`
Expected: ten `[OK]`/`[INFO]`/`[WARN]`/`[BLOCK]` lines, one `GPU:` line, the verdict, `exit 0` (a commit-limit `[WARN]` with its `->` line is possible).

Run: `src/PS5Kitsune.Cli/bin/Release/net10.0/ps5kitsune.exe check --root "C:/PS5 Kitsune Test"; echo "exit $?"`
Expected: `[BLOCK] Data folder path: ...`, verdict "Blocking problems...", `exit 2`.

Run: `src/PS5Kitsune.Cli/bin/Release/net10.0/ps5kitsune.exe install --root "C:/PS5 Kitsune Test"; echo "exit $?"`
Expected: the blocking line, the "Installation stopped" line, `exit 2`, and no `C:\PS5 Kitsune Test\downloads` folder created. Afterwards delete `C:\PS5 Kitsune Test` (it only holds `logs`).

- [ ] **Step 7: Commit**

```bash
git add src/PS5Kitsune.Cli
git commit -m "Add ps5kitsune check and run the checks before install

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: App — probe entry, shell and check screen

**Files:**
- Modify: `src/PS5Kitsune.App/Program.cs`
- Modify: `src/PS5Kitsune.App/App.axaml.cs`
- Modify: `src/PS5Kitsune.App/ViewModels/MainWindowViewModel.cs`
- Create: `src/PS5Kitsune.App/ViewModels/SystemCheckViewModel.cs`
- Create: `src/PS5Kitsune.App/ViewModels/CheckRowViewModel.cs`
- Create: `src/PS5Kitsune.App/Converters/CheckStatusBrushConverter.cs`
- Modify: `src/PS5Kitsune.App/Views/MainWindow.axaml`
- Create: `src/PS5Kitsune.App/Views/SystemCheckView.axaml`, `src/PS5Kitsune.App/Views/SystemCheckView.axaml.cs`
- Create: `tests/PS5Kitsune.App.Tests/PS5Kitsune.App.Tests.csproj`, `tests/PS5Kitsune.App.Tests/SystemCheckViewModelTests.cs`
- Modify: `PS5Kitsune.slnx`, `README.md` (projects table)

**Interfaces:**
- Consumes: `ISystemChecker`, `SystemCheckReport`, `CheckResult`, `CheckStatus`, `CheckFix`, `IPlatform.OpenFix`, `VulkanGpu.Describe`, `VulkanProbe`, `VulkanProbeRunner.ProbeArgument`.
- Produces: `SystemCheckViewModel(ISystemChecker, IPlatform)` with `ObservableCollection<CheckRowViewModel> Checks`, `ObservableCollection<string> Gpus`, `bool IsChecking`, `string Verdict`, `CheckStatus? Overall`, `IAsyncRelayCommand CheckCommand`; `CheckRowViewModel(CheckResult, IPlatform)` with `Title`, `Summary`, `Action`, `HasAction`, `Status`, `StatusText`, `HasFix`, `IRelayCommand OpenFixCommand`.

- [ ] **Step 1: Test project**

`tests/PS5Kitsune.App.Tests/PS5Kitsune.App.Tests.csproj`:

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
    <ProjectReference Include="..\..\src\PS5Kitsune.App\PS5Kitsune.App.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

</Project>
```

In `PS5Kitsune.slnx`, inside `<Folder Name="/tests/">`, first line:

```xml
    <Project Path="tests/PS5Kitsune.App.Tests/PS5Kitsune.App.Tests.csproj" />
```

In `README.md`'s projects table, after the `tests/PS5Kitsune.Core.Tests` row:

```markdown
| `tests/PS5Kitsune.App.Tests` | View model tests |
```

- [ ] **Step 2: Write the failing tests**

`tests/PS5Kitsune.App.Tests/SystemCheckViewModelTests.cs`:

```csharp
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Checks.Vulkan;

namespace PS5Kitsune.App.Tests;

public sealed class SystemCheckViewModelTests
{
    private static readonly VulkanGpu Radeon = new("AMD Radeon(TM) Graphics", 0x1002, "integrated", "1.3.280", "AmdProprietary", "AMD", "25.10.1", true, true);

    private static SystemCheckReport Report() => new(
        [
            new CheckResult("os-arch", "64-bit x86 system", CheckStatus.Ok, "64-bit x86 system."),
            new CheckResult("commit-limit", "Memory commit limit", CheckStatus.Warning, "Too low.", "Enlarge the page file.", CheckFix.OpenPerformanceSettings),
        ],
        new VulkanProbeOutcome(VulkanProbeStatus.Succeeded, new VulkanProbeReport(true, null, [Radeon]), null));

    private readonly RecordingPlatform _platform = new();

    [Fact]
    public async Task Shows_the_results_and_the_verdict()
    {
        var viewModel = new SystemCheckViewModel(new FakeChecker(() => Task.FromResult(Report())), _platform);

        await viewModel.CheckCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsChecking);
        Assert.Equal(CheckStatus.Warning, viewModel.Overall);
        Assert.Equal("This PC can run AnyPS5, with warnings.", viewModel.Verdict);
        Assert.Equal(["OK", "Warning"], viewModel.Checks.Select(row => row.StatusText));
        Assert.Equal([Radeon.Describe()], viewModel.Gpus);
    }

    [Fact]
    public async Task Is_checking_until_the_checker_answers()
    {
        var answer = new TaskCompletionSource<SystemCheckReport>();
        var viewModel = new SystemCheckViewModel(new FakeChecker(() => answer.Task), _platform);

        var running = viewModel.CheckCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsChecking);
        Assert.Equal("Checking this PC…", viewModel.Verdict);
        Assert.Null(viewModel.Overall);
        answer.SetResult(Report());
        await running;
        Assert.False(viewModel.IsChecking);
    }

    [Fact]
    public async Task Checking_again_replaces_the_results()
    {
        var viewModel = new SystemCheckViewModel(new FakeChecker(() => Task.FromResult(Report())), _platform);

        await viewModel.CheckCommand.ExecuteAsync(null);
        await viewModel.CheckCommand.ExecuteAsync(null);

        Assert.Equal(2, viewModel.Checks.Count);
        Assert.Single(viewModel.Gpus);
    }

    [Fact]
    public async Task A_checker_failure_shows_its_message()
    {
        var viewModel = new SystemCheckViewModel(new FakeChecker(() => throw new IOException("disk gone")), _platform);

        await viewModel.CheckCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsChecking);
        Assert.Equal(CheckStatus.Blocking, viewModel.Overall);
        Assert.Contains("disk gone", viewModel.Verdict, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_fix_button_asks_the_platform()
    {
        var viewModel = new SystemCheckViewModel(new FakeChecker(() => Task.FromResult(Report())), _platform);
        await viewModel.CheckCommand.ExecuteAsync(null);

        var withFix = viewModel.Checks[1];
        withFix.OpenFixCommand.Execute(null);

        Assert.False(viewModel.Checks[0].HasFix);
        Assert.True(withFix.HasFix);
        Assert.True(withFix.HasAction);
        Assert.Equal([CheckFix.OpenPerformanceSettings], _platform.OpenedFixes);
    }

    private sealed class FakeChecker(Func<Task<SystemCheckReport>> answer) : ISystemChecker
    {
        public Task<SystemCheckReport> RunAsync(CancellationToken cancellationToken = default) => answer();
    }

    private sealed class RecordingPlatform : IPlatform
    {
        public List<CheckFix> OpenedFixes { get; } = [];

        public string Name => "Test";

        public string DefaultDataRoot => "unused";

        public string SettingsDirectory => "unused";

        public string ExecutableSuffix => ".exe";

        public ulong? GetCommitLimitBytes() => null;

        public IReadOnlyList<string> GetVulkanDriverManifests() => [];

        public void OpenFix(CheckFix fix) => OpenedFixes.Add(fix);
    }
}
```

- [ ] **Step 3: Run and see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build error, `SystemCheckViewModel` not found.

- [ ] **Step 4: View models**

`src/PS5Kitsune.App/ViewModels/CheckRowViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.App.ViewModels;

/// <summary>One check on the PC check screen.</summary>
public sealed partial class CheckRowViewModel(CheckResult result, IPlatform platform)
{
    public string Title => result.Title;

    public string Summary => result.Summary;

    public string? Action => result.Action;

    public bool HasAction => result.Action is not null;

    public CheckStatus Status => result.Status;

    public string StatusText => result.Status switch
    {
        CheckStatus.Ok => "OK",
        CheckStatus.Info => "Info",
        CheckStatus.Warning => "Warning",
        _ => "Blocking",
    };

    public bool HasFix => result.Fix is not null;

    [RelayCommand]
    private void OpenFix()
    {
        if (result.Fix is { } fix)
        {
            platform.OpenFix(fix);
        }
    }
}
```

`src/PS5Kitsune.App/ViewModels/SystemCheckViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.App.ViewModels;

/// <summary>The PC check screen: runs every check and shows the results with a verdict.</summary>
public sealed partial class SystemCheckViewModel(ISystemChecker checker, IPlatform platform) : ObservableObject
{
    public ObservableCollection<CheckRowViewModel> Checks { get; } = [];

    /// <summary>One line per GPU the Vulkan probe found.</summary>
    public ObservableCollection<string> Gpus { get; } = [];

    [ObservableProperty]
    public partial bool IsChecking { get; private set; }

    [ObservableProperty]
    public partial string Verdict { get; private set; } = "";

    /// <summary>Null while checking; colors the verdict banner.</summary>
    [ObservableProperty]
    public partial CheckStatus? Overall { get; private set; }

    [RelayCommand]
    private async Task CheckAsync(CancellationToken cancellationToken)
    {
        IsChecking = true;
        Verdict = "Checking this PC…";
        Overall = null;
        Checks.Clear();
        Gpus.Clear();
        try
        {
            var report = await checker.RunAsync(cancellationToken).ConfigureAwait(true);
            foreach (var result in report.Results)
            {
                Checks.Add(new CheckRowViewModel(result, platform));
            }

            foreach (var gpu in report.Vulkan.Report?.Gpus ?? [])
            {
                Gpus.Add(gpu.Describe());
            }

            Overall = report.Overall;
            Verdict = report.Verdict;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Overall = CheckStatus.Blocking;
            Verdict = $"Could not check this PC: {exception.Message}";
        }
        finally
        {
            IsChecking = false;
        }
    }
}
```

The `FakeChecker` in the failure test throws synchronously from `RunAsync`; the `try` covers that call, so the test still sees the message.

Replace `src/PS5Kitsune.App/ViewModels/MainWindowViewModel.cs` with:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using PS5Kitsune.Core;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.App.ViewModels;

/// <summary>The window shell: a header with what the app resolved at startup, and the current page.</summary>
public sealed partial class MainWindowViewModel(IPlatform platform, DataLayout layout, AnyPS5Profile profile, SystemCheckViewModel systemCheck) : ObservableObject
{
    public string Platform => platform.Name;

    public string DataRoot => layout.Root;

    public string Profile => $"{profile.Id} ({(profile.Validated ? "validated" : "not validated yet")})";

    public SystemCheckViewModel SystemCheck => systemCheck;

    /// <summary>The page shown. Milestone 4 adds the other pages and the navigation between them.</summary>
    public object CurrentPage => systemCheck;
}
```

- [ ] **Step 5: Converter**

`src/PS5Kitsune.App/Converters/CheckStatusBrushConverter.cs`:

```csharp
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.App.Converters;

/// <summary>Badge and banner color of a check status; gray while checking (null).</summary>
public sealed class CheckStatusBrushConverter : IValueConverter
{
    public static CheckStatusBrushConverter Instance { get; } = new();

    private static readonly IBrush Ok = new ImmutableSolidColorBrush(Color.Parse("#2E7D32"));
    private static readonly IBrush Info = new ImmutableSolidColorBrush(Color.Parse("#1565C0"));
    private static readonly IBrush Warning = new ImmutableSolidColorBrush(Color.Parse("#E65100"));
    private static readonly IBrush Blocking = new ImmutableSolidColorBrush(Color.Parse("#C62828"));
    private static readonly IBrush Pending = new ImmutableSolidColorBrush(Color.Parse("#616161"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        CheckStatus.Ok => Ok,
        CheckStatus.Info => Info,
        CheckStatus.Warning => Warning,
        CheckStatus.Blocking => Blocking,
        _ => Pending,
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
```

- [ ] **Step 6: Views**

`src/PS5Kitsune.App/Views/SystemCheckView.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:PS5Kitsune.App.ViewModels"
             xmlns:conv="using:PS5Kitsune.App.Converters"
             xmlns:sys="using:System"
             x:Class="PS5Kitsune.App.Views.SystemCheckView"
             x:DataType="vm:SystemCheckViewModel">
  <DockPanel>
    <Border DockPanel.Dock="Top" Padding="16,12" CornerRadius="6"
            Background="{Binding Overall, Converter={x:Static conv:CheckStatusBrushConverter.Instance}}">
      <Grid ColumnDefinitions="*,Auto" ColumnSpacing="12">
        <StackPanel Orientation="Horizontal" Spacing="12" VerticalAlignment="Center">
          <ProgressBar IsIndeterminate="True" IsVisible="{Binding IsChecking}" Width="80" VerticalAlignment="Center" />
          <TextBlock Text="{Binding Verdict}" Foreground="White" FontSize="16" FontWeight="SemiBold" TextWrapping="Wrap" />
        </StackPanel>
        <Button Grid.Column="1" Content="Check again" Command="{Binding CheckCommand}" />
      </Grid>
    </Border>

    <Expander DockPanel.Dock="Bottom" Header="Detected GPUs" Margin="0,12,0,0" HorizontalAlignment="Stretch">
      <ItemsControl ItemsSource="{Binding Gpus}">
        <ItemsControl.ItemTemplate>
          <DataTemplate x:DataType="sys:String">
            <TextBlock Text="{Binding}" TextWrapping="Wrap" Margin="0,2" />
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
    </Expander>

    <ScrollViewer Margin="0,12,0,0">
      <ItemsControl ItemsSource="{Binding Checks}">
        <ItemsControl.ItemTemplate>
          <DataTemplate x:DataType="vm:CheckRowViewModel">
            <Grid ColumnDefinitions="90,*,Auto" ColumnSpacing="12" Margin="0,6">
              <Border CornerRadius="4" Padding="8,2" HorizontalAlignment="Left" VerticalAlignment="Top"
                      Background="{Binding Status, Converter={x:Static conv:CheckStatusBrushConverter.Instance}}">
                <TextBlock Text="{Binding StatusText}" Foreground="White" FontSize="12" FontWeight="SemiBold" />
              </Border>
              <StackPanel Grid.Column="1" Spacing="2">
                <TextBlock Text="{Binding Title}" FontWeight="SemiBold" />
                <TextBlock Text="{Binding Summary}" TextWrapping="Wrap" Opacity="0.85" />
                <TextBlock Text="{Binding Action}" IsVisible="{Binding HasAction}" TextWrapping="Wrap" FontStyle="Italic" />
              </StackPanel>
              <Button Grid.Column="2" Content="Open performance settings" VerticalAlignment="Top"
                      Command="{Binding OpenFixCommand}" IsVisible="{Binding HasFix}" />
            </Grid>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
    </ScrollViewer>
  </DockPanel>
</UserControl>
```

`src/PS5Kitsune.App/Views/SystemCheckView.axaml.cs`:

```csharp
using Avalonia.Controls;

namespace PS5Kitsune.App.Views;

public sealed partial class SystemCheckView : UserControl
{
    public SystemCheckView() => InitializeComponent();
}
```

Replace `src/PS5Kitsune.App/Views/MainWindow.axaml` with:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:PS5Kitsune.App.ViewModels"
        xmlns:views="using:PS5Kitsune.App.Views"
        x:Class="PS5Kitsune.App.Views.MainWindow"
        x:DataType="vm:MainWindowViewModel"
        Title="PS5Kitsune"
        Icon="/Assets/kitsune.ico"
        Width="860" Height="680" MinWidth="600" MinHeight="480">
  <Window.DataTemplates>
    <DataTemplate DataType="vm:SystemCheckViewModel">
      <views:SystemCheckView />
    </DataTemplate>
  </Window.DataTemplates>

  <DockPanel Margin="24">
    <StackPanel DockPanel.Dock="Top" Spacing="8" Margin="0,0,0,16">
      <TextBlock Text="PS5Kitsune" FontSize="28" FontWeight="SemiBold" />
      <Grid ColumnDefinitions="Auto,*,Auto,*,Auto,*" ColumnSpacing="8" Opacity="0.7">
        <TextBlock Grid.Column="0" Text="Platform" FontWeight="SemiBold" />
        <TextBlock Grid.Column="1" Text="{Binding Platform}" />
        <TextBlock Grid.Column="2" Text="Data root" FontWeight="SemiBold" />
        <TextBlock Grid.Column="3" Text="{Binding DataRoot}" />
        <TextBlock Grid.Column="4" Text="Profile" FontWeight="SemiBold" />
        <TextBlock Grid.Column="5" Text="{Binding Profile}" TextTrimming="CharacterEllipsis" />
      </Grid>
    </StackPanel>
    <ContentControl Content="{Binding CurrentPage}" />
  </DockPanel>
</Window>
```

- [ ] **Step 7: Probe entry and startup**

Replace `src/PS5Kitsune.App/Program.cs` with:

```csharp
using Avalonia;
using PS5Kitsune.Core.Checks.Vulkan;

namespace PS5Kitsune.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // The Vulkan probe child (see VulkanProbeRunner): nothing else is loaded, so a crashing driver only takes this process down.
        if (args is [VulkanProbeRunner.ProbeArgument])
        {
            Console.Out.Write(VulkanProbe.Run().ToJson());
            return 0;
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Also used by the Avalonia designer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
```

In `App.axaml.cs`, after `services.AddTransient<MainWindowViewModel>();` add:

```csharp
        services.AddTransient<SystemCheckViewModel>();
```

and replace the `if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)` block with:

```csharp
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = _services.GetRequiredService<MainWindowViewModel>();
            var window = new MainWindow { DataContext = viewModel };
            window.Opened += (_, _) => viewModel.SystemCheck.CheckCommand.Execute(null);
            desktop.MainWindow = window;
            desktop.Exit += (_, _) => _services.Dispose();
        }
```

- [ ] **Step 8: Run the tests and see them pass**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: all tests pass, including 5 in `SystemCheckViewModelTests`.

`[ObservableProperty]` on partial properties needs C# 13 or later; the solution's `LangVersion latest` provides it. If the generator rejects the `private set` accessors, make the setters public.

- [ ] **Step 9: Run the app**

Run: `dotnet run --project src/PS5Kitsune.App -c Release`
Expected: the window opens, the banner is gray with "Checking this PC…" and a progress bar, then turns green or orange with the verdict; ten rows with badges; "Detected GPUs" lists the Radeon. If the commit limit is a warning, "Open performance settings" opens Windows' Performance Options. "Check again" reruns. Close the window.

- [ ] **Step 10: Commit**

```bash
git add PS5Kitsune.slnx README.md src/PS5Kitsune.App tests/PS5Kitsune.App.Tests
git commit -m "Add the PC check screen to the app

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: Docs and manual verification

**Files:**
- Modify: `docs/architecture.md`
- Modify: `README.md`
- Modify: `docs/superpowers/specs/2026-10-21-milestone-3-pc-checks-design.md`

- [ ] **Step 1: Architecture**

In `docs/architecture.md`:

1. In the PC checks table, replace the "Free disk space" row's last cell `Warning; threshold to measure at the first build (open point)` with `Warning below the profile's \`checks.minFreeDiskGiB\` (5 GiB for release mode; to revisit after measuring a build at milestone 5)`.
2. In the same table, replace the "Dozen driver" row's "How" cell `List of Vulkan driver manifests` with `Vulkan driver manifests in the registry, and the probe's \`driverId\` (\`MesaDozen\`)`.
3. In "Isolated Vulkan probe", after the sentence ending `` `vulkaninfo` is not used: it ships with the Vulkan SDK and is missing on most PCs. ``, add: `` Both executables handle `--probe-vulkan` before anything else starts (`PS5Kitsune.exe` and `ps5kitsune.exe`); the parent waits 15 seconds at most per attempt. The thresholds (commit limit, disk space, NVIDIA drivers) live in the profile's `checks` section. ``
4. In the roadmap, replace milestone 3's criterion line `- Criterion: an Intel Iris Xe laptop reports \`shaderInt64\` as a warning with the Lavapipe suggestion.` with:
   `` - Criterion: `ps5kitsune check` and the check screen agree on the AMD reference laptop, with no blocking result; on an Intel + NVIDIA PC the probe sees both GPUs and names the usable one. A GPU without `shaderInt64` (the Iris Xe case) is covered by tests from probe fixtures. ``

- [ ] **Step 2: README**

In `README.md`, replace `Early development — milestone 2 of 8 (end-to-end command-line path). There is no graphical interface yet.` with `Early development — milestone 3 of 8 (PC checks). The app shows a PC check screen; installing and adding games still go through the command line.`

Replace the command block with:

```powershell
ps5kitsune check                     # can this PC run AnyPS5? CPU, Vulkan, memory, disk, data folder
ps5kitsune install                   # checks, then Python, relinker, libraries and scripts of AnyPS5 v0.1.2
ps5kitsune add <game folder or zip>  # validate, copy, unwrap, convert and audit
ps5kitsune run <title ID>            # launch from the game folder, log in .ps5kitsune\logs
```

After `Every command takes \`--root <data root>\`; the default is \`C:\PS5Kitsune\`.`, add: `` `check` and `install` exit with code 2 when a check is blocking. ``

- [ ] **Step 3: Manual verification on the AMD laptop**

Run each and keep the output:

```bash
dotnet build PS5Kitsune.slnx -c Release
dotnet test --solution PS5Kitsune.slnx -c Release
src/PS5Kitsune.Cli/bin/Release/net10.0/ps5kitsune.exe --probe-vulkan
src/PS5Kitsune.Cli/bin/Release/net10.0/ps5kitsune.exe check; echo "exit $?"
src/PS5Kitsune.App/bin/Release/net10.0/PS5Kitsune.exe --probe-vulkan | cat
```

Expected: tests green; both `--probe-vulkan` print the same JSON (the app's output is visible because the pipe redirects it); `check` exits 0 with no `[BLOCK]` line.

Then ask the user to open the app (`dotnet run --project src/PS5Kitsune.App -c Release`) and confirm the screen shows the same statuses as `check`. Do not claim this step passed before the user confirms.

- [ ] **Step 4: Record the result in the spec**

At the end of the spec's "Criterion" section, add a dated line with what was observed, for example:

`` **2026-10-2X, AMD laptop:** `check` exit 0; os-arch OK, cpu-extensions OK, cpu-vendor Info (AMD), vulkan-driver OK (AMD Radeon(TM) Graphics), vulkan-features OK, commit-limit <OK or Warning: N MiB>, dozen OK, nvidia-driver OK, disk-space OK, install-path OK. The screen shows the same. Intel + NVIDIA run: pending. ``

Use the real values from Step 3.

- [ ] **Step 5: Commit**

```bash
git add docs/architecture.md README.md docs/superpowers/specs/2026-10-21-milestone-3-pc-checks-design.md
git commit -m "Document the PC checks and record the AMD run

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

Then stop: opening the PR waits for the user (push policy).
