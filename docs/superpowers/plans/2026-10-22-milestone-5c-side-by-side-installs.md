# Milestone 5c — Side-by-side installs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep the release and build installs in their own folders under `anyps5\installs\`, with `anyps5\state\active` naming the one in use, so a failed or cancelled install never removes a working one and a switch runs no step.

**Architecture:** `AnyPS5Paths` gains a per-mode view (`For(mode)`, an `AnyPS5InstallPaths`) used by the install steps, while its existing properties follow the active install, so game conversion keeps its code. `AnyPS5Installer` writes `active` after a successful install and gains `Use(mode)`, `ActiveMode`, `ReadRecord(mode)` and `MigrateLegacyInstall()`. The app (Home, Mode) and the CLI (`use`, migration before `install`/`use`/`add`/`run`) build on that.

**Tech Stack:** .NET 10, C#, Avalonia 11 (compiled bindings), CommunityToolkit.Mvvm, System.CommandLine, xUnit v3 on Microsoft.Testing.Platform.

**Spec:** `docs/superpowers/specs/2026-10-22-milestone-5c-side-by-side-installs-design.md`

## Global Constraints

- Layout: installs in `anyps5\installs\release` and `anyps5\installs\build`; the active mode in `anyps5\state\active` holding `release` or `build`; `anyps5\state\requested-mode` unchanged; `anyps5\source\`, `anyps5\build\` and the build markers unchanged.
- `active` is written only after every install step succeeded; a failure or a cancel never touches it.
- `Use(mode)` runs no step and no done-check; it throws `InvalidOperationException` with "The {mode} install is not finished; run install --mode {mode} first." when `ReadRecord(mode)` is null (`{mode}` is `release` or `build`).
- Migration message: "Cannot move anyps5\current: {reason}. Close any game using it and retry."
- App texts, verbatim: "Also installed: {mode} {version}", "The {mode} install did not finish.", "In use", "Use this install", "Resume the install", "Verify / repair", "Continue", "Cannot read the AnyPS5 install".
- CLI text: "AnyPS5 {mode} {version} is now in use ({directory})."
- `GameRecord.CurrentSchemaVersion` stays 1; `install.json` schema unchanged.
- Build: `TreatWarningsAsErrors`, `AnalysisLevel` latest-recommended. Constant texts on view models are get-only properties with an initializer (CA1822).
- No test uses the network or the compiler.
- Commits: author La-Kiks (already configured), message ends with `Co-Authored-By: <your model name> <noreply@anthropic.com>`. Never commit `install.cmd`. Run CLI commands through the Bash tool.
- Test command: `dotnet test --solution PS5Kitsune.slnx -c Release` (filter one class with `-- --filter-class "<full class name>"`).

## Refinements of the spec

1. The migration is exposed as `IAnyPS5Installer.MigrateLegacyInstall()`, which calls the static `AnyPS5Migration.Run(AnyPS5Paths)`. Home and the CLI call it through the installer, so the app's sessions and test fakes need no new service.
2. There is no CLI test project. `use` is covered by the Core tests of `Use` and by criterion point 3; the CLI migration failure reuses `CommandHost`'s existing handler (message, exit code 1).
3. `InstallModes.Resolve` keeps its signature; the CLI passes `installer.ReadRecord()`, which now returns the active install's record.

## File map

| File | Change |
| --- | --- |
| `src/PS5Kitsune.Core/DataLayout.cs` | `AnyPS5Installs`; `AnyPS5Current` documented as legacy |
| `src/PS5Kitsune.Core/AnyPS5/AnyPS5InstallPaths.cs` | new: files of one install folder |
| `src/PS5Kitsune.Core/AnyPS5/AnyPS5Paths.cs` | `For(mode)`, `ActiveModeFile`, `ActiveMode`, `RequestedMode`, `WriteActiveMode`, `LegacyCurrent`; existing properties follow the active install |
| `src/PS5Kitsune.Core/AnyPS5/InstallModes.cs` | `Parse(string)` |
| `src/PS5Kitsune.Core/AnyPS5/IAnyPS5Installer.cs`, `AnyPS5Installer.cs` | `ActiveMode`, `ReadRecord(mode)`, `Use`, `MigrateLegacyInstall`, activation, no clear step |
| `src/PS5Kitsune.Core/AnyPS5/ReleaseSource.cs`, `Build/BuildSource.cs`, `Build/CopyBuildStep.cs`, `Steps/WriteInstallRecordStep.cs` | write into `For(mode)` |
| `src/PS5Kitsune.Core/AnyPS5/Steps/ClearOtherModeStep.cs` | deleted |
| `src/PS5Kitsune.Core/AnyPS5/AnyPS5Migration.cs`, `AnyPS5MigrationException.cs` | new |
| `src/PS5Kitsune.Core/Games/Steps/WriteGameRecordStep.cs`, `Games/GameImporter.cs` | `ConversionInfo` from the active install's record |
| `src/PS5Kitsune.Cli/UseCommand.cs`, `CliLog.cs`, `Program.cs`, `InstallCommand.cs`, `AddCommand.cs`, `RunCommand.cs` | `use`, migration, final line |
| `src/PS5Kitsune.App/ViewModels/HomeViewModel.cs`, `Views/HomeView.axaml` | migration, active install, "Also installed", interrupted switch |
| `src/PS5Kitsune.App/ViewModels/ModeViewModel.cs`, `Views/ModeView.axaml` | "In use", "Use this install" |
| tests | see each task |
| `docs/architecture.md`, `README.md` | Task 6 |

---

### Task 1: Per-mode install folders and the active install

**Files:**
- Modify: `src/PS5Kitsune.Core/DataLayout.cs`
- Create: `src/PS5Kitsune.Core/AnyPS5/AnyPS5InstallPaths.cs`
- Modify: `src/PS5Kitsune.Core/AnyPS5/AnyPS5Paths.cs`
- Modify: `src/PS5Kitsune.Core/AnyPS5/InstallModes.cs`
- Modify: `src/PS5Kitsune.Core/AnyPS5/IAnyPS5Installer.cs`, `src/PS5Kitsune.Core/AnyPS5/AnyPS5Installer.cs`
- Modify: `src/PS5Kitsune.Core/AnyPS5/ReleaseSource.cs`, `src/PS5Kitsune.Core/AnyPS5/Build/BuildSource.cs`, `src/PS5Kitsune.Core/AnyPS5/Build/CopyBuildStep.cs`, `src/PS5Kitsune.Core/AnyPS5/Steps/WriteInstallRecordStep.cs`
- Delete: `src/PS5Kitsune.Core/AnyPS5/Steps/ClearOtherModeStep.cs`
- Modify: `tests/PS5Kitsune.App.Tests/Fakes.cs` (only so the solution compiles: the new interface members)
- Test: `tests/PS5Kitsune.Core.Tests/AnyPS5/AnyPS5InstallerTests.cs`, `BuildInstallTests.cs`, `Build/CopyBuildStepTests.cs`, new `AnyPS5PathsTests.cs`

**Interfaces:**
- Produces:
  - `DataLayout.AnyPS5Installs` (`string`).
  - `AnyPS5InstallPaths(string root, IPlatform platform)` with `RootDirectory`, `Relinker`, `Libs`, `Scripts`, `ImportAudit`, `UnwrapSelf`, `RecordFile`.
  - `AnyPS5Paths.For(AnyPS5Mode mode)`, `ActiveModeFile`, `RequestedModeFile`, `ActiveMode` (`AnyPS5Mode?`), `RequestedMode` (`AnyPS5Mode?`), `WriteActiveMode(AnyPS5Mode)`, `LegacyCurrent`; `RootDirectory`, `Relinker`, `Libs`, `Scripts`, `ImportAudit`, `UnwrapSelf`, `RecordFile` follow the active install.
  - `InstallModes.Parse(string name)` → `AnyPS5Mode?`.
  - `IAnyPS5Installer`: `AnyPS5Mode? ActiveMode`, `AnyPS5InstallRecord? ReadRecord(AnyPS5Mode mode)`, `void Use(AnyPS5Mode mode)`. (`MigrateLegacyInstall()` is added in Task 2, not here.)
  - `FakeInstaller` (app tests): `ActiveMode` (settable), `Records` (`Dictionary<AnyPS5Mode, AnyPS5InstallRecord>`), `Used` (`List<AnyPS5Mode>`).

- [ ] **Step 1: Write the failing tests**

Create `tests/PS5Kitsune.Core.Tests/AnyPS5/AnyPS5PathsTests.cs`:

```csharp
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.AnyPS5;

public sealed class AnyPS5PathsTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly AnyPS5Paths _paths;

    public AnyPS5PathsTests()
    {
        _paths = new AnyPS5Paths(new DataLayout(_temp.Path), new FakePlatform());
    }

    [Fact]
    public void Each_mode_has_its_own_folder()
    {
        var build = _paths.For(AnyPS5Mode.Build);

        Assert.Equal(_temp.Combine("anyps5", "installs", "build"), build.RootDirectory);
        Assert.Equal(_temp.Combine("anyps5", "installs", "build", "relinker.exe"), build.Relinker);
        Assert.Equal(_temp.Combine("anyps5", "installs", "build", "libs"), build.Libs);
        Assert.Equal(_temp.Combine("anyps5", "installs", "build", "scripts"), build.Scripts);
        Assert.Equal(_temp.Combine("anyps5", "installs", "build", "scripts", "tools", "import_audit.py"), build.ImportAudit);
        Assert.Equal(_temp.Combine("anyps5", "installs", "build", "scripts", "tools", "unwrap_self.py"), build.UnwrapSelf);
        Assert.Equal(_temp.Combine("anyps5", "installs", "build", "install.json"), build.RecordFile);
        Assert.Equal(_temp.Combine("anyps5", "installs", "release"), _paths.For(AnyPS5Mode.Release).RootDirectory);
    }

    [Fact]
    public void The_active_view_follows_the_active_file()
    {
        Assert.Null(_paths.ActiveMode);
        Assert.Equal(_temp.Combine("anyps5", "installs", "release"), _paths.RootDirectory);

        _paths.WriteActiveMode(AnyPS5Mode.Build);

        Assert.Equal(_temp.Combine("anyps5", "state", "active"), _paths.ActiveModeFile);
        Assert.Equal("build", File.ReadAllText(_paths.ActiveModeFile));
        Assert.Equal(AnyPS5Mode.Build, _paths.ActiveMode);
        Assert.Equal(_paths.For(AnyPS5Mode.Build).Relinker, _paths.Relinker);
        Assert.Equal(_paths.For(AnyPS5Mode.Build).RecordFile, _paths.RecordFile);
    }

    [Theory]
    [InlineData("nightly")]
    [InlineData("")]
    public void An_unknown_active_file_means_no_active_install(string content)
    {
        TestFiles.Write(_paths.ActiveModeFile, content);

        Assert.Null(_paths.ActiveMode);
    }

    [Fact]
    public void The_legacy_folder_is_anyps5_current() =>
        Assert.Equal(_temp.Combine("anyps5", "current"), _paths.LegacyCurrent);

    public void Dispose() => _temp.Dispose();
}
```

(`TestFiles.Write(path, string)` creates the parent folder; check `tests/PS5Kitsune.Core.Tests/TestFiles.cs` for its exact overloads.)

In `tests/PS5Kitsune.Core.Tests/AnyPS5/AnyPS5InstallerTests.cs`:

- Replace `private string Current => _temp.Combine("anyps5", "current");` with `private string Release => _temp.Combine("anyps5", "installs", "release");` and use `Release` wherever `Current` was used; rename `Install_lays_out_anyps5_current` to `Install_lays_out_installs_release`.
- Delete `Installing_another_mode_empties_current_first`, `A_locked_file_of_the_other_mode_fails_the_clear_step_and_keeps_the_record`, `The_same_mode_lists_no_clear_step` and `The_current_mode_comes_from_the_record`.
- In `A_mode_without_a_source_is_incomplete_instead_of_throwing`, replace the three lines that rewrite the record with a build record written into the build folder and an active file:

```csharp
        var record = _setup.Installer.ReadRecord()!;
        await JsonFiles.WriteAsync(_setup.Paths.For(AnyPS5Mode.Build).RecordFile, record with { Mode = AnyPS5Mode.Build }, TestContext.Current.CancellationToken);
        _setup.Paths.WriteActiveMode(AnyPS5Mode.Build);
```

- Add these tests:

```csharp
    [Fact]
    public async Task A_successful_install_becomes_the_active_one()
    {
        Assert.Null(_setup.Installer.ActiveMode);

        await _setup.InstallAsync();

        Assert.Equal(AnyPS5Mode.Release, _setup.Installer.ActiveMode);
        Assert.Equal("release", File.ReadAllText(_setup.Paths.ActiveModeFile));
        Assert.Equal("v0.1.2", _setup.Installer.ReadRecord()?.Version);
        Assert.Equal("v0.1.2", _setup.Installer.ReadRecord(AnyPS5Mode.Release)?.Version);
        Assert.Null(_setup.Installer.ReadRecord(AnyPS5Mode.Build));
    }

    [Fact]
    public void No_step_clears_the_other_mode()
    {
        Assert.Equal("Install Python", _setup.Installer.CreateSteps(AnyPS5Mode.Release)[0].Name);
        Assert.Equal("Install Python", _setup.Installer.CreateSteps(AnyPS5Mode.Build)[0].Name);
    }

    [Fact]
    public async Task The_current_mode_is_the_requested_then_the_active_then_release()
    {
        Assert.Equal(AnyPS5Mode.Release, _setup.Installer.CurrentMode);
        _setup.Paths.WriteActiveMode(AnyPS5Mode.Build);
        Assert.Equal(AnyPS5Mode.Build, _setup.Installer.CurrentMode);

        Directory.CreateDirectory(Path.GetDirectoryName(_setup.Paths.RequestedModeFile)!);
        await File.WriteAllTextAsync(_setup.Paths.RequestedModeFile, "release", TestContext.Current.CancellationToken);

        Assert.Equal(AnyPS5Mode.Release, _setup.Installer.CurrentMode);
    }

    [Fact]
    public async Task Use_switches_without_running_a_step()
    {
        await _setup.InstallAsync();
        var record = _setup.Installer.ReadRecord()!;
        await JsonFiles.WriteAsync(_setup.Paths.For(AnyPS5Mode.Build).RecordFile, record with { Mode = AnyPS5Mode.Build, Version = "48d61f5" }, TestContext.Current.CancellationToken);
        var requests = _setup.Downloader.Requests.Count;

        _setup.Installer.Use(AnyPS5Mode.Build);

        Assert.Equal(AnyPS5Mode.Build, _setup.Installer.ActiveMode);
        Assert.Equal("48d61f5", _setup.Installer.ReadRecord()?.Version);
        Assert.Equal(_setup.Paths.For(AnyPS5Mode.Build).RootDirectory, _setup.Paths.RootDirectory);
        Assert.Equal(requests, _setup.Downloader.Requests.Count);
        Assert.Empty(_setup.Runner.Calls);
    }

    [Fact]
    public async Task Use_refuses_an_install_that_never_finished()
    {
        await _setup.InstallAsync();

        var exception = Assert.Throws<InvalidOperationException>(() => _setup.Installer.Use(AnyPS5Mode.Build));

        Assert.Equal("The build install is not finished; run install --mode build first.", exception.Message);
        Assert.Equal(AnyPS5Mode.Release, _setup.Installer.ActiveMode);
    }

    [Fact]
    public async Task The_state_of_a_mode_ignores_the_active_install()
    {
        await _setup.InstallAsync();
        _setup.Paths.WriteActiveMode(AnyPS5Mode.Build);

        var state = _setup.Installer.GetState(AnyPS5Mode.Release);

        Assert.Equal(AnyPS5InstallStatus.Installed, state.Status);
        Assert.Equal("v0.1.2", state.Record?.Version);
        Assert.False(_setup.Installer.IsInstalled());
    }

    [Fact]
    public async Task An_unfinished_install_of_the_other_mode_keeps_the_active_one_usable()
    {
        await _setup.InstallAsync();
        Directory.CreateDirectory(Path.GetDirectoryName(_setup.Paths.RequestedModeFile)!);
        await File.WriteAllTextAsync(_setup.Paths.RequestedModeFile, "build", TestContext.Current.CancellationToken);

        Assert.Equal(AnyPS5Mode.Build, _setup.Installer.CurrentMode);
        Assert.True(_setup.Installer.IsInstalled());
    }
```

`_setup.Runner.Calls` is the fake process runner's call list; check `tests/PS5Kitsune.Core.Tests/Fakes/FakeProcessRunner.cs` for its real name and use it.

In `tests/PS5Kitsune.Core.Tests/AnyPS5/BuildInstallTests.cs`:

- `A_build_install_fills_current_and_records_the_commit` → rename to `A_build_install_fills_installs_build_and_records_the_commit`; its paths become `_setup.Paths.For(AnyPS5Mode.Build)` (or the active view, since the build becomes active), and add `Assert.Equal(AnyPS5Mode.Build, _setup.Installer.ActiveMode);`.
- `Build_mode_lists_its_steps_in_order`: drop "Remove the other mode's files" from the expected names if present.
- `A_cancelled_switch_to_build_resumes_in_build_mode`: add `Assert.Equal(AnyPS5Mode.Release, _setup.Installer.ActiveMode);` and `Assert.True(_setup.Installer.IsInstalled());` after the cancel.
- `A_failed_first_build_resumes_in_build_mode`: add `Assert.Null(_setup.Installer.ActiveMode);` after the failure.
- Replace `Going_back_to_release_replaces_current_and_keeps_the_build_folders` with:

```csharp
    [Fact]
    public async Task Both_installs_stay_side_by_side()
    {
        await _setup.InstallAsync(AnyPS5Mode.Build);
        await _setup.InstallAsync(AnyPS5Mode.Release);

        var release = _setup.Paths.For(AnyPS5Mode.Release);
        var build = _setup.Paths.For(AnyPS5Mode.Build);
        Assert.Equal(AnyPS5TestSetup.RelinkerContent, File.ReadAllBytes(release.Relinker));
        Assert.False(File.Exists(Path.Combine(release.Libs, "libstdc++-6.dll")));
        Assert.True(File.Exists(build.Relinker));
        Assert.True(File.Exists(Path.Combine(build.Libs, "libstdc++-6.dll")));
        Assert.Equal(AnyPS5Mode.Release, _setup.Installer.ActiveMode);
        Assert.Equal(AnyPS5InstallStatus.Installed, _setup.Installer.GetState(AnyPS5Mode.Build).Status);
    }
```

In `tests/PS5Kitsune.Core.Tests/AnyPS5/Build/CopyBuildStepTests.cs`: `_current` becomes `AnyPS5InstallPaths`, built with `new AnyPS5Paths(_setup.Layout, _setup.Platform).For(AnyPS5Mode.Build)`; assertions on `_current.*` keep working.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: compile errors (`For`, `WriteActiveMode`, `ActiveMode`, `ReadRecord(mode)`, `Use` do not exist).

- [ ] **Step 3: Implement**

`src/PS5Kitsune.Core/DataLayout.cs`: replace the `AnyPS5Current` member with:

```csharp
    /// <summary>One folder per install mode (release, build), each with relinker, libs, scripts and install.json.</summary>
    public string AnyPS5Installs => Path.Combine(AnyPS5, "installs");

    /// <summary>The single install folder of milestones 4 to 5b; moved into <see cref="AnyPS5Installs"/> by the migration.</summary>
    public string AnyPS5Current => Path.Combine(AnyPS5, "current");
```

`src/PS5Kitsune.Core/AnyPS5/AnyPS5InstallPaths.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.AnyPS5;

/// <summary>Files of one install folder, anyps5\installs\&lt;mode&gt;. See docs/architecture.md, "Root layout".</summary>
public sealed class AnyPS5InstallPaths(string root, IPlatform platform)
{
    public string RootDirectory { get; } = root;

    public string Relinker => Path.Combine(RootDirectory, "relinker" + platform.ExecutableSuffix);

    /// <summary>The .prx files and DLLs copied beside each game.</summary>
    public string Libs => Path.Combine(RootDirectory, "libs");

    /// <summary>tools/ and core/libs/prx/ of the source tree, layout kept so import_audit.py finds ../core/libs/prx.</summary>
    public string Scripts => Path.Combine(RootDirectory, "scripts");

    public string ImportAudit => Path.Combine(Scripts, "tools", "import_audit.py");

    public string UnwrapSelf => Path.Combine(Scripts, "tools", "unwrap_self.py");

    public string RecordFile => Path.Combine(RootDirectory, "install.json");
}
```

`src/PS5Kitsune.Core/AnyPS5/AnyPS5Paths.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.AnyPS5;

/// <summary>
/// The AnyPS5 installs and the state files that choose between them. The file properties point at the active install,
/// read from anyps5\state\active on each access, so game conversion always uses the install in use. See
/// docs/architecture.md, "Root layout".
/// </summary>
public sealed class AnyPS5Paths(DataLayout layout, IPlatform platform)
{
    public string RootDirectory => Active.RootDirectory;

    public string Relinker => Active.Relinker;

    /// <inheritdoc cref="AnyPS5InstallPaths.Libs"/>
    public string Libs => Active.Libs;

    /// <inheritdoc cref="AnyPS5InstallPaths.Scripts"/>
    public string Scripts => Active.Scripts;

    public string ImportAudit => Active.ImportAudit;

    public string UnwrapSelf => Active.UnwrapSelf;

    public string RecordFile => Active.RecordFile;

    public string RequestedModeFile => Path.Combine(layout.AnyPS5, "state", "requested-mode");

    public string ActiveModeFile => Path.Combine(layout.AnyPS5, "state", "active");

    /// <summary>anyps5\current, the single install folder before milestone 5c.</summary>
    public string LegacyCurrent => layout.AnyPS5Current;

    /// <summary>The mode in anyps5\state\active; null when the file is missing, unreadable or holds an unknown value.</summary>
    public AnyPS5Mode? ActiveMode => ReadMode(ActiveModeFile);

    /// <summary>The mode in anyps5\state\requested-mode; null when the file is missing, unreadable or holds an unknown value.</summary>
    public AnyPS5Mode? RequestedMode => ReadMode(RequestedModeFile);

    public AnyPS5InstallPaths For(AnyPS5Mode mode) => new(Path.Combine(layout.AnyPS5Installs, InstallModes.Name(mode)), platform);

    public void WriteActiveMode(AnyPS5Mode mode)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ActiveModeFile)!);
        File.WriteAllText(ActiveModeFile, InstallModes.Name(mode));
    }

    /// <summary>With no active install, the release folder, where nothing is installed: callers check IsInstalled first.</summary>
    private AnyPS5InstallPaths Active => For(ActiveMode ?? AnyPS5Mode.Release);

    private static AnyPS5Mode? ReadMode(string file)
    {
        try
        {
            return InstallModes.Parse(File.ReadAllText(file).Trim());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
```

`src/PS5Kitsune.Core/AnyPS5/InstallModes.cs`: add, after `Name`:

```csharp
    /// <summary>The mode whose <see cref="Name"/> is <paramref name="name"/>; null for any other text.</summary>
    public static AnyPS5Mode? Parse(string name) =>
        Enum.GetValues<AnyPS5Mode>().Where(mode => Name(mode) == name).Cast<AnyPS5Mode?>().FirstOrDefault();
```

and in the `Resolve` summary replace "the mode of the current install" with "the mode of the active install".

`src/PS5Kitsune.Core/AnyPS5/Steps/WriteInstallRecordStep.cs`: the constructor parameter `AnyPS5Paths paths` becomes `AnyPS5InstallPaths paths`; nothing else changes.

`src/PS5Kitsune.Core/AnyPS5/ReleaseSource.cs`: keep the constructor (`AnyPS5Paths paths`), and at the top of `CreateSteps` add `var target = paths.For(AnyPS5Mode.Release);`, then use `target.Relinker`, `target.Libs`, `target.Scripts`, `target.UnwrapSelf` in the four steps. `IAnyPS5Source` does not change: the installer gives `WriteInstallRecordStep` its folder.

`src/PS5Kitsune.Core/AnyPS5/Build/CopyBuildStep.cs`: the parameter `AnyPS5Paths current` becomes `AnyPS5InstallPaths current`; in the summary replace "anyps5\current" with "anyps5\installs\build".

`src/PS5Kitsune.Core/AnyPS5/Build/BuildSource.cs`: the last constructor parameter stays `AnyPS5Paths current`; the step becomes `new CopyBuildStep(paths, current.For(AnyPS5Mode.Build), winlibs)`; in the summary replace "anyps5\current" with "anyps5\installs\build".

Delete `src/PS5Kitsune.Core/AnyPS5/Steps/ClearOtherModeStep.cs`.

`src/PS5Kitsune.Core/AnyPS5/IAnyPS5Installer.cs`: change the class summary to "Installs AnyPS5 into anyps5\installs\&lt;mode&gt; and chooses the active install. The app's screens use it through this interface.", replace the `CurrentMode` doc with `/// <summary><see cref="RequestedMode"/>, else <see cref="ActiveMode"/>, else <see cref="AnyPS5Mode.Release"/>.</summary>`, replace the `ReadRecord()` doc with `/// <summary>The active install's install.json; null with no active install, or when missing or unreadable.</summary>`, and add:

```csharp
    /// <summary>The install in use (anyps5\state\active); null when there is none, or the file is unreadable or unknown. Never throws.</summary>
    AnyPS5Mode? ActiveMode { get; }

    /// <summary>anyps5\installs\&lt;mode&gt;\install.json as found; null when missing or unreadable.</summary>
    AnyPS5InstallRecord? ReadRecord(AnyPS5Mode mode);

    /// <summary>Makes <paramref name="mode"/>'s finished install the active one, with no step and no done-check.</summary>
    /// <exception cref="InvalidOperationException"><paramref name="mode"/> has no install.json: its install never finished.</exception>
    void Use(AnyPS5Mode mode);
```

Replace the `GetState` doc with "Where the install of <paramref name="mode"/> stands in its own folder, whatever is active, from the steps' done-checks and its install.json; Incomplete when the active profile does not support <paramref name="mode"/>." and the `InstallAsync` doc's first sentence with "Runs the steps of <see cref="CreateSteps"/> into anyps5\installs\&lt;mode&gt;, then makes <paramref name="mode"/> the active install. A failure or a cancel leaves the active install as it was."

`src/PS5Kitsune.Core/AnyPS5/AnyPS5Installer.cs` — the members that change:

```csharp
/// <summary>Installs AnyPS5 into anyps5\installs\&lt;mode&gt;: Python, then the steps of the mode's source, then install.json; then makes it active.</summary>
public sealed class AnyPS5Installer(
    IEnumerable<IAnyPS5Source> sources,
    PythonEmbeddableTool python,
    AnyPS5Paths paths,
    StepRunner stepRunner,
    TimeProvider time) : IAnyPS5Installer
{
    public AnyPS5Mode? RequestedMode => paths.RequestedMode;

    public AnyPS5Mode? ActiveMode => paths.ActiveMode;

    public AnyPS5Mode CurrentMode => RequestedMode ?? ActiveMode ?? AnyPS5Mode.Release;

    public bool Supports(AnyPS5Mode mode) => Source(mode) is not null;

    public IReadOnlyList<IStep> CreateSteps(AnyPS5Mode mode)
    {
        var source = Source(mode)
            ?? throw new InvalidOperationException(mode == AnyPS5Mode.Build
                ? "Mode build is not available: the active profile has no build section or no build tools."
                : "Mode release is not available.");
        return [new InstallToolStep("Install Python", python), .. source.CreateSteps(), new WriteInstallRecordStep(source, paths.For(mode), time)];
    }

    /// <summary>True when there is an active install and every one of its steps is done.</summary>
    public bool IsInstalled() => ActiveMode is { } active && GetState(active).Status == AnyPS5InstallStatus.Installed;

    public AnyPS5InstallState GetState(AnyPS5Mode mode)
    {
        var record = ReadRecord(mode);
        // … the rest of the existing body, unchanged …
    }

    public AnyPS5InstallRecord? ReadRecord() => ActiveMode is { } active ? ReadRecord(active) : null;

    public AnyPS5InstallRecord? ReadRecord(AnyPS5Mode mode)
    {
        try
        {
            return JsonFiles.Read<AnyPS5InstallRecord>(paths.For(mode).RecordFile);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Use(AnyPS5Mode mode)
    {
        if (ReadRecord(mode) is null)
        {
            var name = InstallModes.Name(mode);
            throw new InvalidOperationException($"The {name} install is not finished; run install --mode {name} first.");
        }

        paths.WriteActiveMode(mode);
    }

    /// <summary>
    /// Writes the requested mode first and deletes it once every step succeeded and the mode is active, so an install
    /// that fails or is cancelled resumes in the same mode and leaves the active install untouched.
    /// </summary>
    public async Task InstallAsync(AnyPS5Mode mode, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default)
    {
        var steps = CreateSteps(mode);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.RequestedModeFile)!);
        await File.WriteAllTextAsync(paths.RequestedModeFile, InstallModes.Name(mode), cancellationToken).ConfigureAwait(false);
        await stepRunner.RunAsync(steps, progress, onEvent, cancellationToken).ConfigureAwait(false);
        paths.WriteActiveMode(mode);
        File.Delete(paths.RequestedModeFile);
    }

    // IsDone and Source unchanged.
}
```

Remove the `using PS5Kitsune.Core.AnyPS5.Steps;` only if nothing else in the file needs it (`InstallToolStep` and `WriteInstallRecordStep` live there: keep it).

`tests/PS5Kitsune.App.Tests/Fakes.cs`, so the app tests compile (Task 5 extends this):

```csharp
    public AnyPS5Mode? ActiveMode { get; set; }

    /// <summary>Records by mode; a mode not listed falls back to State.Record when that record has the mode.</summary>
    public Dictionary<AnyPS5Mode, AnyPS5InstallRecord> Records { get; } = [];

    /// <summary>The modes passed to Use, in order.</summary>
    public List<AnyPS5Mode> Used { get; } = [];

    public AnyPS5InstallRecord? ReadRecord(AnyPS5Mode mode) =>
        Records.TryGetValue(mode, out var record) ? record : State.Record is { } stateRecord && stateRecord.Mode == mode ? stateRecord : null;

    public void Use(AnyPS5Mode mode)
    {
        Used.Add(mode);
        ActiveMode = mode;
    }
```

Other compile errors: `GameImporter` and the game steps use `AnyPS5Paths`'s active view and compile unchanged. `DoneViewModel` uses `paths.RootDirectory` and compiles unchanged. Fix any other reference to `ClearOtherModeStep` or to `AnyPS5Paths` in a step constructor the same way.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: build with 0 warnings; all tests pass except the explicit network test (skipped). The games tests (`GameImporterTests`, etc.) install a release through `AnyPS5TestSetup`, which now becomes active, so they pass unchanged; if one builds `anyps5\current` paths by hand, point it at `_setup.Paths` instead.

- [ ] **Step 5: Commit**

```bash
git add -A src tests
git commit -m "Install each mode in its own folder and record the active one

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 2: Migrate anyps5\current

**Files:**
- Create: `src/PS5Kitsune.Core/AnyPS5/AnyPS5Migration.cs`, `src/PS5Kitsune.Core/AnyPS5/AnyPS5MigrationException.cs`
- Modify: `src/PS5Kitsune.Core/AnyPS5/IAnyPS5Installer.cs`, `src/PS5Kitsune.Core/AnyPS5/AnyPS5Installer.cs`, `tests/PS5Kitsune.App.Tests/Fakes.cs`
- Test: `tests/PS5Kitsune.Core.Tests/AnyPS5/AnyPS5MigrationTests.cs`

**Interfaces:**
- Consumes: `AnyPS5Paths.LegacyCurrent`, `For(mode)`, `RequestedMode`, `ActiveModeFile`, `WriteActiveMode` (Task 1).
- Produces: `static class AnyPS5Migration { static void Run(AnyPS5Paths paths) }`; `AnyPS5MigrationException : Exception`; `IAnyPS5Installer.MigrateLegacyInstall()`; `FakeInstaller.MigrateFailure` (`Exception?`) and `FakeInstaller.Migrations` (`int`).

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.Core.Tests/AnyPS5/AnyPS5MigrationTests.cs`:

```csharp
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Core.Tests.AnyPS5;

public sealed class AnyPS5MigrationTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly AnyPS5TestSetup _setup;

    public AnyPS5MigrationTests()
    {
        _setup = new AnyPS5TestSetup(_temp.Path);
    }

    private string Current => _temp.Combine("anyps5", "current");

    private async Task WriteCurrentAsync(AnyPS5Mode? recordMode)
    {
        TestFiles.Write(Path.Combine(Current, "relinker.exe"), "relinker");
        if (recordMode is { } mode)
        {
            var record = new AnyPS5InstallRecord(2, "test-profile", mode, "v0.1.2", null, null, _setup.Time.GetLocalNow());
            await JsonFiles.WriteAsync(Path.Combine(Current, "install.json"), record, TestContext.Current.CancellationToken);
        }
    }

    [Theory]
    [InlineData(AnyPS5Mode.Release)]
    [InlineData(AnyPS5Mode.Build)]
    public async Task Current_moves_to_the_folder_of_its_mode_and_becomes_active(AnyPS5Mode mode)
    {
        await WriteCurrentAsync(mode);

        _setup.Installer.MigrateLegacyInstall();

        Assert.False(Directory.Exists(Current));
        Assert.True(File.Exists(_setup.Paths.For(mode).Relinker));
        Assert.Equal(mode, _setup.Installer.ActiveMode);
        Assert.Equal(mode, _setup.Installer.ReadRecord()?.Mode);
    }

    [Fact]
    public async Task Without_a_record_the_requested_mode_decides()
    {
        await WriteCurrentAsync(null);
        TestFiles.Write(_setup.Paths.RequestedModeFile, "build");

        _setup.Installer.MigrateLegacyInstall();

        Assert.True(File.Exists(_setup.Paths.For(AnyPS5Mode.Build).Relinker));
        Assert.Equal(AnyPS5Mode.Build, _setup.Installer.ActiveMode);
    }

    [Fact]
    public async Task Without_a_record_nor_a_requested_mode_it_is_a_release()
    {
        await WriteCurrentAsync(null);

        _setup.Installer.MigrateLegacyInstall();

        Assert.True(File.Exists(_setup.Paths.For(AnyPS5Mode.Release).Relinker));
        Assert.Equal(AnyPS5Mode.Release, _setup.Installer.ActiveMode);
    }

    [Fact]
    public void Nothing_happens_without_current()
    {
        _setup.Installer.MigrateLegacyInstall();

        Assert.False(Directory.Exists(_temp.Combine("anyps5", "installs")));
        Assert.Null(_setup.Installer.ActiveMode);
    }

    [Fact]
    public async Task Nothing_moves_when_the_target_exists()
    {
        await WriteCurrentAsync(AnyPS5Mode.Release);
        TestFiles.Write(Path.Combine(_setup.Paths.For(AnyPS5Mode.Release).RootDirectory, "kept.txt"), "already there");

        _setup.Installer.MigrateLegacyInstall();

        Assert.True(Directory.Exists(Current));
        Assert.True(File.Exists(Path.Combine(_setup.Paths.For(AnyPS5Mode.Release).RootDirectory, "kept.txt")));
        Assert.Null(_setup.Installer.ActiveMode);
    }

    [Fact]
    public async Task An_existing_active_file_is_kept()
    {
        await WriteCurrentAsync(AnyPS5Mode.Build);
        _setup.Paths.WriteActiveMode(AnyPS5Mode.Release);

        _setup.Installer.MigrateLegacyInstall();

        Assert.True(File.Exists(_setup.Paths.For(AnyPS5Mode.Build).Relinker));
        Assert.Equal(AnyPS5Mode.Release, _setup.Installer.ActiveMode);
    }

    [Fact]
    public async Task A_locked_file_stops_the_move_and_leaves_current_in_place()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Only Windows refuses to move a folder holding an open file.");
        }

        await WriteCurrentAsync(AnyPS5Mode.Release);
        using (new FileStream(Path.Combine(Current, "relinker.exe"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var exception = Assert.Throws<AnyPS5MigrationException>(() => _setup.Installer.MigrateLegacyInstall());

            Assert.StartsWith(@"Cannot move anyps5\current: ", exception.Message, StringComparison.Ordinal);
            Assert.EndsWith(". Close any game using it and retry.", exception.Message, StringComparison.Ordinal);
        }

        Assert.True(File.Exists(Path.Combine(Current, "relinker.exe")));
        Assert.Null(_setup.Installer.ActiveMode);
    }

    public void Dispose() => _temp.Dispose();
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: compile errors (`MigrateLegacyInstall`, `AnyPS5MigrationException` do not exist).

- [ ] **Step 3: Implement**

`src/PS5Kitsune.Core/AnyPS5/AnyPS5MigrationException.cs`:

```csharp
namespace PS5Kitsune.Core.AnyPS5;

/// <summary>anyps5\current could not be moved into anyps5\installs; nothing was changed.</summary>
public sealed class AnyPS5MigrationException(Exception inner)
    : Exception($"Cannot move anyps5\\current: {inner.Message.TrimEnd('.')}. Close any game using it and retry.", inner);
```

`src/PS5Kitsune.Core/AnyPS5/AnyPS5Migration.cs`:

```csharp
using System.Text.Json;
using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Core.AnyPS5;

/// <summary>
/// Moves the single install folder of milestones 4 to 5b, anyps5\current, into anyps5\installs\&lt;mode&gt; and makes it
/// active. Idempotent: nothing happens without current or when the target exists.
/// </summary>
public static class AnyPS5Migration
{
    /// <exception cref="AnyPS5MigrationException">The move failed, for example on a file held by a running game.</exception>
    public static void Run(AnyPS5Paths paths)
    {
        var current = paths.LegacyCurrent;
        if (!Directory.Exists(current))
        {
            return;
        }

        var mode = RecordMode(current) ?? paths.RequestedMode ?? AnyPS5Mode.Release;
        var target = paths.For(mode).RootDirectory;
        if (Directory.Exists(target))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            Directory.Move(current, target);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new AnyPS5MigrationException(exception);
        }

        if (!File.Exists(paths.ActiveModeFile))
        {
            paths.WriteActiveMode(mode);
        }
    }

    private static AnyPS5Mode? RecordMode(string current)
    {
        try
        {
            return JsonFiles.Read<AnyPS5InstallRecord>(Path.Combine(current, "install.json"))?.Mode;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
```

`IAnyPS5Installer`:

```csharp
    /// <summary>Moves anyps5\current, from before milestone 5c, into anyps5\installs and makes it active; does nothing otherwise.</summary>
    /// <exception cref="AnyPS5MigrationException">The move failed; nothing was changed.</exception>
    void MigrateLegacyInstall();
```

`AnyPS5Installer`: `public void MigrateLegacyInstall() => AnyPS5Migration.Run(paths);`

`tests/PS5Kitsune.App.Tests/Fakes.cs`, in `FakeInstaller`:

```csharp
    /// <summary>Thrown by MigrateLegacyInstall when set.</summary>
    public Exception? MigrateFailure { get; set; }

    public int Migrations { get; private set; }

    public void MigrateLegacyInstall()
    {
        Migrations++;
        if (MigrateFailure is not null)
        {
            throw MigrateFailure;
        }
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.Core.Tests.AnyPS5.AnyPS5MigrationTests"`, then the full suite.
Expected: PASS (the locked test is skipped off Windows).

- [ ] **Step 5: Commit**

```bash
git add -A src tests
git commit -m "Move anyps5\current into the installs folder

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 3: Game records name the install that converted them

**Files:**
- Modify: `src/PS5Kitsune.Core/Games/Steps/WriteGameRecordStep.cs`, `src/PS5Kitsune.Core/Games/GameImporter.cs`, `src/PS5Kitsune.Core/Library/GameRecord.cs` (doc of `ConversionInfo`)
- Test: `tests/PS5Kitsune.Core.Tests/Games/WriteGameRecordStepTests.cs`, `tests/PS5Kitsune.Core.Tests/Games/GameImporterTests.cs`

**Interfaces:**
- Consumes: `AnyPS5Installer.ReadRecord()` (active install's record, Task 1), `InstallModes.Name`.
- Produces: `WriteGameRecordStep(GameImportContext context, AnyPS5InstallRecord install, TimeProvider time)`.

- [ ] **Step 1: Write the failing tests**

In `WriteGameRecordStepTests`, replace `CreateStep` and the `ConversionInfo` assertion, and add a build case:

```csharp
    private static readonly AnyPS5InstallRecord Release = new(2, "doom-validated-2026-10", AnyPS5Mode.Release, "v0.1.2", null, new Uri("https://example.test/u.py"), default);

    private static readonly AnyPS5InstallRecord Build = new(2, "doom-validated-2026-10", AnyPS5Mode.Build, "3fd1cc3", "3fd1cc393b8512c2e989860053f30dee5a8cd1aa", null, default);

    private WriteGameRecordStep CreateStep(AnyPS5InstallRecord? install = null) => new(_context, install ?? Release, _time);
```

(keep `Assert.Equal(new ConversionInfo("doom-validated-2026-10", "release", "v0.1.2", null, true), record.ConvertedWith);` in the first test), and:

```csharp
    [Fact]
    public async Task A_game_converted_with_a_build_records_the_build()
    {
        _context.Audit = new AuditSummary(262, 0, 0, new Dictionary<string, int>());

        await CreateStep(Build).ExecuteAsync(null, TestContext.Current.CancellationToken);

        var record = GameRecordStore.Read(_context.Game.RecordFile)!;
        Assert.Equal(new ConversionInfo("doom-validated-2026-10", "build", "3fd1cc3", "3fd1cc393b8512c2e989860053f30dee5a8cd1aa", true), record.ConvertedWith);
    }
```

Add `using PS5Kitsune.Core.AnyPS5;` and remove `using PS5Kitsune.Core.Profiles;` if unused.

In `GameImporterTests`, in the test that checks the written game record after a successful add (search for `ConvertedWith` or `GameRecordStore.Read`; if none, the first test that adds a game successfully), add `Assert.Equal("release", record.ConvertedWith.Mode);` and `Assert.Equal("v0.1.2", record.ConvertedWith.Version);`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: compile error on `new WriteGameRecordStep(_context, AnyPS5InstallRecord, …)`.

- [ ] **Step 3: Implement**

`WriteGameRecordStep`:

```csharp
/// <summary>Writes game.json last: its presence means the game is added. It names the install that converted the game.</summary>
public sealed class WriteGameRecordStep(GameImportContext context, AnyPS5InstallRecord install, TimeProvider time) : IStep
{
    …
            ConvertedWith = new ConversionInfo(install.Profile, InstallModes.Name(install.Mode), install.Version, install.Commit, context.ToIntel),
    …
}
```

(usings: add `PS5Kitsune.Core.AnyPS5`, drop `PS5Kitsune.Core.Profiles` if unused.)

`GameImporter.AddAsync`: right after the `IsInstalled()` check, `var install = installer.ReadRecord() ?? throw new AnyPS5NotInstalledException();`, and the step becomes `new WriteGameRecordStep(context, install, time)`. If `profile` becomes unused in `GameImporter`, keep it only if `RelinkStep` still takes it (it does).

`GameRecord.cs`, the `ConversionInfo` doc:

```csharp
/// <param name="Mode">The install mode that converted the game: "release" or "build".</param>
/// <param name="Version">The release tag, or the first 7 characters of the commit for a build.</param>
/// <param name="Commit">The commit built; null for a release.</param>
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A src tests
git commit -m "Record the install that converted a game

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 4: CLI — `use`, migration, and the install in use

**Files:**
- Create: `src/PS5Kitsune.Cli/UseCommand.cs`
- Modify: `src/PS5Kitsune.Cli/CliLog.cs`, `Program.cs`, `InstallCommand.cs`, `AddCommand.cs`, `RunCommand.cs`

**Interfaces:**
- Consumes: `AnyPS5Installer.Use`, `ReadRecord(mode)`, `MigrateLegacyInstall`, `AnyPS5Paths.For` (Tasks 1–2).

No CLI test project exists (see Refinements); verify by running the CLI by hand on a temporary root in Step 3.

- [ ] **Step 1: Implement**

`CliLog.cs`: replace `AnyPS5Ready` with:

```csharp
    [LoggerMessage(Level = LogLevel.Information, Message = "AnyPS5 {Mode} {Version} is now in use ({Directory}).")]
    public static partial void InstallInUse(this ILogger logger, string mode, string version, string directory);
```

and add:

```csharp
    [LoggerMessage(Level = LogLevel.Error, Message = "{Problem}")]
    public static partial void UseRefused(this ILogger logger, string problem);
```

`UseCommand.cs`:

```csharp
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core.AnyPS5;

namespace PS5Kitsune.Cli;

internal static class UseCommand
{
    public static Command Create(Option<DirectoryInfo?> rootOption)
    {
        var modeArgument = new Argument<AnyPS5Mode>("mode") { Description = "release or build: the finished install to use." };
        var command = new Command("use", "Switch to the release or build install, both already installed. Runs no step.") { modeArgument };
        command.SetAction((parseResult, cancellationToken) => CommandHost.RunAsync(parseResult.GetValue(rootOption), "use", (services, logger) =>
        {
            var installer = services.GetRequiredService<AnyPS5Installer>();
            installer.MigrateLegacyInstall();
            var mode = parseResult.GetValue(modeArgument);
            try
            {
                installer.Use(mode);
            }
            catch (InvalidOperationException exception)
            {
                logger.UseRefused(exception.Message);
                return Task.FromResult(1);
            }

            var paths = services.GetRequiredService<AnyPS5Paths>();
            logger.InstallInUse(InstallModes.Name(mode), installer.ReadRecord(mode)!.Version, paths.For(mode).RootDirectory);
            return Task.FromResult(0);
        }));
        return command;
    }
}
```

`Program.cs`: add `UseCommand.Create(rootOption),` after `InstallCommand.Create(rootOption),`.

`InstallCommand.cs`:
- First line of the action, after getting `installer`: `installer.MigrateLegacyInstall();` (before `InstallModes.Resolve`).
- `--mode` description: "release: download the release. build: compile the profile's commit. Defaults to the mode of an unfinished install, else of the active install, else release."
- Replace the last three lines before `return 0;` with:

```csharp
            var record = installer.ReadRecord(choice.Mode)!;
            logger.InstallInUse(modeName, record.Version, services.GetRequiredService<AnyPS5Paths>().For(choice.Mode).RootDirectory);
```

`AddCommand.cs` and `RunCommand.cs`: call `services.GetRequiredService<AnyPS5Installer>().MigrateLegacyInstall();` first in the action, before anything reads the install. A failure throws `AnyPS5MigrationException`, which `CommandHost` already logs with its message and maps to exit code 1.

- [ ] **Step 2: Build**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: 0 warnings, 0 errors. Fix every remaining `AnyPS5Ready` reference.

- [ ] **Step 3: Check by hand on a temporary root**

Run (Bash, from the repo root; uses no network):

```bash
ROOT="$(mktemp -d)"
dotnet run --project src/PS5Kitsune.Cli -c Release --no-build -- use build --root "$ROOT"; echo "exit $?"
mkdir -p "$ROOT/anyps5/current" && echo x > "$ROOT/anyps5/current/relinker.exe"
dotnet run --project src/PS5Kitsune.Cli -c Release --no-build -- use release --root "$ROOT"; echo "exit $?"
ls "$ROOT/anyps5" "$ROOT/anyps5/installs"; cat "$ROOT/anyps5/state/active" 2>/dev/null; echo
```

Expected:
- first `use build`: "The build install is not finished; run install --mode build first.", exit 1;
- after creating `current` (no record): the migration moves it to `installs/release` and writes `active` = release; `use release` then fails with "The release install is not finished; …" because there is no `install.json`, exit 1;
- `ls` shows `installs` and no `current`; `active` holds `release`.

Put the output in your report.

- [ ] **Step 4: Run the full suite**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A src
git commit -m "Add the use command and migrate before reading the install

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 5: App — Home and Mode know both installs

**Files:**
- Modify: `src/PS5Kitsune.App/ViewModels/HomeViewModel.cs`, `src/PS5Kitsune.App/Views/HomeView.axaml`
- Modify: `src/PS5Kitsune.App/ViewModels/ModeViewModel.cs`, `src/PS5Kitsune.App/Views/ModeView.axaml`
- Test: `tests/PS5Kitsune.App.Tests/HomeViewModelTests.cs`, `tests/PS5Kitsune.App.Tests/ModeViewModelTests.cs`

**Interfaces:**
- Consumes: `IAnyPS5Installer.ActiveMode`, `RequestedMode`, `ReadRecord(mode)`, `Use(mode)`, `MigrateLegacyInstall()`; `FakeInstaller.ActiveMode`, `Records`, `Used`, `MigrateFailure`, `Migrations` (Tasks 1–2); `InstallModes.Name`.
- Produces: `HomeViewModel.OtherInstallText` (`string?`); `ModeViewModel.IsReleaseInUse`, `IsBuildInUse` (`bool`), `ContinueText` values per the Global Constraints.

- [ ] **Step 1: Write the failing tests**

`HomeViewModelTests` — first update the existing tests to the new model: Home now checks `ActiveMode ?? RequestedMode ?? Release`, so every test that set `_installer.CurrentMode = AnyPS5Mode.Build` to show a build install sets `_installer.ActiveMode = AnyPS5Mode.Build` instead (and a test of an unfinished build with nothing active sets `_installer.RequestedMode = AnyPS5Mode.Build`). Then add:

```csharp
    [Fact]
    public async Task The_check_migrates_the_old_install_first()
    {
        await CreateAsync();

        Assert.Equal(1, _installer.Migrations);
    }

    [Fact]
    public async Task A_failed_migration_is_shown_and_retried_on_the_next_refresh()
    {
        _installer.MigrateFailure = new AnyPS5MigrationException(new IOException("The file is in use."));

        var viewModel = await CreateAsync();

        Assert.Equal("Cannot read the AnyPS5 install", viewModel.StateTitle);
        Assert.Equal(@"Cannot move anyps5\current: The file is in use. Close any game using it and retry.", viewModel.StateDetail);
        Assert.False(viewModel.IsChecking);

        _installer.MigrateFailure = null;
        await viewModel.RefreshAsync();

        Assert.Equal(2, _installer.Migrations);
        Assert.Equal("AnyPS5 is not installed", viewModel.StateTitle);
    }

    [Fact]
    public async Task The_other_finished_install_is_listed()
    {
        _installer.ActiveMode = AnyPS5Mode.Release;
        _installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, Record);
        _installer.Records[AnyPS5Mode.Build] = BuildRecord;

        var viewModel = await CreateAsync();

        Assert.Equal("AnyPS5 v0.1.2 is installed", viewModel.StateTitle);
        Assert.Equal("Also installed: build 48d61f5", viewModel.OtherInstallText);
    }

    [Fact]
    public async Task No_other_install_lists_nothing()
    {
        _installer.ActiveMode = AnyPS5Mode.Release;
        _installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, Record);

        var viewModel = await CreateAsync();

        Assert.Null(viewModel.OtherInstallText);
    }

    [Fact]
    public async Task An_interrupted_switch_keeps_the_active_install_and_offers_to_resume()
    {
        _installer.ActiveMode = AnyPS5Mode.Release;
        _installer.RequestedMode = AnyPS5Mode.Build;
        _installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, Record);
        _installer.Records[AnyPS5Mode.Build] = BuildRecord;

        var viewModel = await CreateAsync();

        Assert.Equal("AnyPS5 v0.1.2 is installed", viewModel.StateTitle);
        Assert.Equal("The build install did not finish.", viewModel.StateDetail);
        Assert.Equal("Resume the install", viewModel.ActionText);
        Assert.Null(viewModel.OtherInstallText);
        Assert.Equal(AnyPS5Mode.Release, _installer.CheckedModes.Last());
    }
```

`_installer.CheckedModes` is a new `List<AnyPS5Mode>` on `FakeInstaller` that `GetState` appends its `mode` to; add it in `Fakes.cs`. Add `using PS5Kitsune.Core.AnyPS5;` if missing.

`ModeViewModelTests` — update the existing tests that set a record (`_installer.State = … Record …`) to also set `_installer.ActiveMode` to that record's mode, since "Verify / repair" now means "the active install". Then add:

```csharp
    private static readonly AnyPS5InstallRecord ReleaseRecord = new(2, "test-profile", AnyPS5Mode.Release, "v0.1.2", null, new Uri("https://example.test/u.py"), default);

    private static readonly AnyPS5InstallRecord BuildRecord = new(2, "test-profile", AnyPS5Mode.Build, "3fd1cc3", "3fd1cc393b8512c2e989860053f30dee5a8cd1aa", null, default);

    [Fact]
    public void Both_installs_show_their_version_and_the_active_one_is_in_use()
    {
        _installer.Records[AnyPS5Mode.Release] = ReleaseRecord;
        _installer.Records[AnyPS5Mode.Build] = BuildRecord;
        _installer.ActiveMode = AnyPS5Mode.Release;

        var viewModel = Create();

        Assert.Equal("Installed: v0.1.2", viewModel.ReleaseInstalledText);
        Assert.Equal("Installed: 3fd1cc3", viewModel.BuildInstalledText);
        Assert.True(viewModel.IsReleaseInUse);
        Assert.False(viewModel.IsBuildInUse);
    }

    [Fact]
    public void The_button_names_what_happens_to_the_selected_install()
    {
        _installer.Records[AnyPS5Mode.Release] = ReleaseRecord;
        _installer.Records[AnyPS5Mode.Build] = BuildRecord;
        _installer.ActiveMode = AnyPS5Mode.Release;
        _installer.CurrentMode = AnyPS5Mode.Release;
        var viewModel = Create();

        Assert.Equal("Verify / repair", viewModel.ContinueText);
        viewModel.SelectBuildCommand.Execute(null);
        Assert.Equal("Use this install", viewModel.ContinueText);
    }

    [Fact]
    public void An_unfinished_install_is_resumed_before_anything_else()
    {
        _installer.Records[AnyPS5Mode.Build] = BuildRecord;
        _installer.ActiveMode = AnyPS5Mode.Build;
        _installer.RequestedMode = AnyPS5Mode.Build;
        _installer.CurrentMode = AnyPS5Mode.Build;

        Assert.Equal("Resume the install", Create().ContinueText);
    }

    [Fact]
    public void A_mode_never_installed_continues_to_the_check()
    {
        _installer.Records[AnyPS5Mode.Release] = ReleaseRecord;
        _installer.ActiveMode = AnyPS5Mode.Release;
        var viewModel = Create();

        viewModel.SelectBuildCommand.Execute(null);
        Assert.Equal("Continue", viewModel.ContinueText);
        viewModel.ContinueCommand.Execute(null);

        Assert.Equal("check:Build", _navigator.CurrentPage);
        Assert.Empty(_installer.Used);
    }

    [Fact]
    public void Use_this_install_switches_and_goes_home()
    {
        _installer.Records[AnyPS5Mode.Release] = ReleaseRecord;
        _installer.Records[AnyPS5Mode.Build] = BuildRecord;
        _installer.ActiveMode = AnyPS5Mode.Release;
        var viewModel = Create();

        viewModel.SelectBuildCommand.Execute(null);
        viewModel.ContinueCommand.Execute(null);

        Assert.Equal([AnyPS5Mode.Build], _installer.Used);
        Assert.Equal("home", _navigator.CurrentPage);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: compile errors (`OtherInstallText`, `IsReleaseInUse`, `CheckedModes`).

- [ ] **Step 3: Implement**

`Fakes.cs`, `FakeInstaller`: add `public List<AnyPS5Mode> CheckedModes { get; } = [];` and, as the first line of `GetState`, `CheckedModes.Add(mode);`.

`HomeViewModel`:

```csharp
    /// <summary>"Also installed: {mode} {version}" when the install not in use is finished too; null otherwise.</summary>
    [ObservableProperty]
    public partial string? OtherInstallText { get; private set; }
```

Replace `RefreshAsync`'s body from `var mode = installer.CurrentMode;` to the end with:

```csharp
        IsChecking = true;
        (StateTitle, StateDetail, ActionText) = ("Checking the AnyPS5 install…", "", "");
        OtherInstallText = null;

        HomeCheck check;
        try
        {
            check = await Task.Run(() => Check(installer)).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            if (generation == _generation)
            {
                (StateTitle, StateDetail, ActionText) = ("Cannot read the AnyPS5 install", exception.Message, "Install AnyPS5");
                IsChecking = false;
            }

            return;
        }

        if (generation != _generation)
        {
            return;
        }

        Apply(check.State, check.Mode);
        ApplyBothInstalls(check);
        IsChecking = false;
    }

    /// <param name="Mode">The install described: the active one, else an unfinished one, else release.</param>
    /// <param name="Other">The record of the other mode's install; null when it never finished or no install is active.</param>
    private sealed record HomeCheck(AnyPS5Mode Mode, AnyPS5InstallState State, AnyPS5Mode? Active, AnyPS5Mode? Requested, AnyPS5InstallRecord? Other);

    /// <summary>Runs on a worker thread: the migration moves a folder and the done-checks can hash hundreds of MB.</summary>
    private static HomeCheck Check(IAnyPS5Installer installer)
    {
        installer.MigrateLegacyInstall();
        var active = installer.ActiveMode;
        var requested = installer.RequestedMode;
        var mode = active ?? requested ?? AnyPS5Mode.Release;
        var other = active is { } activeMode ? installer.ReadRecord(OtherMode(activeMode)) : null;
        return new HomeCheck(mode, installer.GetState(mode), active, requested, other);
    }

    private static AnyPS5Mode OtherMode(AnyPS5Mode mode) => mode == AnyPS5Mode.Release ? AnyPS5Mode.Build : AnyPS5Mode.Release;

    /// <summary>An unfinished install of the other mode is offered for resuming; a finished one is listed.</summary>
    private void ApplyBothInstalls(HomeCheck check)
    {
        if (check.Active is not { } active)
        {
            return;
        }

        if (check.Requested is { } requested && requested != active)
        {
            StateDetail = $"The {InstallModes.Name(requested)} install did not finish.";
            ActionText = "Resume the install";
            return;
        }

        if (check.Other is { } other)
        {
            OtherInstallText = $"Also installed: {InstallModes.Name(other.Mode)} {other.Version}";
        }
    }
```

`Apply(state, mode)` stays as it is. Update the class summary only if it mentions a single install.

`HomeView.axaml`: under the `StateDetail` TextBlock add

```xml
        <TextBlock Text="{Binding OtherInstallText}" Opacity="0.85" IsVisible="{Binding OtherInstallText, Converter={x:Static ObjectConverters.IsNotNull}}" />
```

`ModeViewModel`:
- Fields: replace `_record` with `private readonly IAnyPS5Installer _installer;`, `private readonly AnyPS5InstallRecord? _releaseRecord;`, `private readonly AnyPS5InstallRecord? _buildRecord;`, `private readonly AnyPS5Mode? _activeMode;`. In the constructor: `_installer = installer; _releaseRecord = installer.ReadRecord(AnyPS5Mode.Release); _buildRecord = installer.ReadRecord(AnyPS5Mode.Build); _activeMode = installer.ActiveMode;`.
- Replace the installed-text members, `ContinueText` and `Continue`:

```csharp
    /// <summary>"Installed: {version}" on the release card when the release install is finished; null otherwise.</summary>
    public string? ReleaseInstalledText => _releaseRecord is { } record ? $"Installed: {record.Version}" : null;

    /// <summary>"Installed: {version}" on the build card when the build install is finished; null otherwise.</summary>
    public string? BuildInstalledText => _buildRecord is { } record ? $"Installed: {record.Version}" : null;

    public bool IsReleaseInUse => _activeMode == AnyPS5Mode.Release;

    public bool IsBuildInUse => _activeMode == AnyPS5Mode.Build;

    /// <summary>Names what Continue does for the selected mode.</summary>
    public string ContinueText => _requestedMode == SelectedMode ? "Resume the install"
        : _activeMode == SelectedMode ? "Verify / repair"
        : SwitchesOnly ? "Use this install"
        : "Continue";

    /// <summary>The selected install is finished and not in use, and no install of it is pending: Continue only switches.</summary>
    private bool SwitchesOnly => _requestedMode != SelectedMode && _activeMode != SelectedMode
        && (SelectedMode == AnyPS5Mode.Release ? _releaseRecord : _buildRecord) is not null;

    [RelayCommand]
    private void Continue()
    {
        if (SwitchesOnly)
        {
            _installer.Use(SelectedMode);
            _navigator.GoHome(_pages.Home());
            return;
        }

        _navigator.GoTo(_pages.SystemCheck(SelectedMode));
    }
```

Remove the old `InstalledText` helper. Keep the constructor's summary about preselection; it still holds (`CurrentMode` is requested, else active, else release).

`ModeView.axaml`: in each card, after the installed line, add

```xml
            <TextBlock Text="In use" FontWeight="SemiBold" IsVisible="{Binding IsReleaseInUse}" />
```

(and `IsBuildInUse` on the build card).

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.App.Tests.HomeViewModelTests"`, the same for `ModeViewModelTests`, then the full suite.
Expected: PASS, 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add -A src tests
git commit -m "Show both installs on Home and switch from Mode

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 6: Docs

**Files:**
- Modify: `docs/architecture.md`, `README.md`

- [ ] **Step 1: Update `docs/architecture.md`**

- Root layout (the tree around line 364): replace the `current\` branch with

```text
│   ├── state\                   one marker per build step, holding the commit (build mode); active (release or build); requested-mode while an install is unfinished
│   └── installs\
│       ├── release\             the release install
│       └── build\               the build install, copied from build\
│           ├── relinker.exe
│           ├── libs\            .prx + 3 DLLs, ready to copy
│           ├── scripts\         tools\ and core\libs\prx of the same commit, tree layout kept
│           └── install.json     profile, mode, version, commit, date
```

  and, right after the tree, a short paragraph: both installs stay side by side; `state\active` names the one games are converted with; an install becomes active only once every step succeeded; `ps5kitsune use release|build` and Mode's "Use this install" switch with no step; a root from before 5c has its `anyps5\current` moved into `installs\<mode>` the first time the CLI or the app opens it.
- Remove every mention of the "Remove the other mode's files" step (search for it) and of `anyps5\current` outside the migration paragraph.
- Game record section: after the `convertedWith` example, one sentence: "`convertedWith.mode` is the install mode that converted the game, `release` or `build`; `commit` is set for a build."
- CLI section: add `use` next to `install`, with one line: "`ps5kitsune use release|build` switches to an install that is already finished, with no step; it refuses an unfinished one."
- Screens: Home lists the other finished install ("Also installed: …") and an interrupted switch; Mode shows "In use" and "Use this install".
- Roadmap entry 5c (line ~502): replace the folder names with `anyps5\installs\release` and `anyps5\installs\build` (the build folder name was taken by CMake), and add the spec link like 5b's: `Spec: [2026-10-22-milestone-5c-side-by-side-installs-design.md](superpowers/specs/2026-10-22-milestone-5c-side-by-side-installs-design.md).` (match how 5b's link is written).

- [ ] **Step 2: Update `README.md`**

Where the README lists commands, add `ps5kitsune use release|build` with "switch between the release and build installs, both kept side by side". Update the milestone status line to say milestone 5c is in progress, matching how 5b was written.

- [ ] **Step 3: Check and commit**

Run: `grep -n "Remove the other mode\|anyps5\\\\current" docs/architecture.md README.md`
Expected: only the migration paragraph mentions `anyps5\current`.

```bash
git add docs/architecture.md README.md
git commit -m "Document side-by-side installs and the use command

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```
