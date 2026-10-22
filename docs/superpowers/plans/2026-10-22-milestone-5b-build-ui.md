# Milestone 5b — Build mode UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build AnyPS5 from source from the app: a selectable Build card on Mode, a Tools screen, and build progress on Install.

**Architecture:** The flow becomes Home → Mode → PC check (chosen mode) → Tools (build only) → Install → Done, with the mode passed from page to page through `IPages`. Core gains tool sizes in the profile, a `BuildToolsSummary` for the Tools screen, `IAnyPS5Installer.Supports`, a per-command `FractionTracker` in `BuildCommands`, and `Summary`/`Details` on `BuildCommandException`. The app's view models stay thin and are tested with the existing fakes.

**Tech Stack:** .NET 10, Avalonia 11 (compiled bindings), CommunityToolkit.Mvvm, xUnit v3 on Microsoft.Testing.Platform.

Spec: [docs/superpowers/specs/2026-10-22-milestone-5b-build-ui-design.md](../specs/2026-10-22-milestone-5b-build-ui-design.md).

## Global Constraints

- Code, docs, commit messages and UI text are in English.
- `Directory.Build.props` sets `TreatWarningsAsErrors`, `AnalysisLevel latest-recommended` and `EnforceCodeStyleInBuild`: 0 warnings. Use `CultureInfo.InvariantCulture` (or `string.Create(CultureInfo.InvariantCulture, …)`) when formatting numbers into strings (CA1305).
- No new NuGet dependency (GPL-2.0-only license check).
- Tool sizes in bytes: Python `12601679`, MinGit `39806486`, WinLibs `267593703`. Sizes are shown in MB as `bytes / 1048576.0` with format `0.0` (total of the three: `305.2 MB`).
- Build step names, in order: "Install Python", "Install MinGit", "Install WinLibs", "Get the AnyPS5 source", "Update the submodules", "Configure the build", "Compile AnyPS5", "Build the libraries", "Copy the build", "Record the install". Release: "Install Python", then the release's download steps, then "Record the install" (6 steps in all with the embedded profile).
- UI text, exactly:
  - Mode, Build card: `Build from source`, `Commit {7 chars}`, `About 15 minutes the first time, faster for updates.`, `Needs about {buildMinFreeDiskGiB} GiB of free disk space.`, `This profile does not define a build.`; button `Continue`.
  - Tools: `Installed`, `Missing`, `size unknown`, `To download: {n} MB`, `To download: at least {n} MB`, `To download: size unknown`, `All tools are present.`, `Disk space for the build: about 3 GB.`, buttons `Download what's missing and build` / `Build`.
  - Install: fraction text `{done} / {total} units`; details expander header `Show details`.
  - Done: `Built from source, commit {7 chars}.`
- Tests never use the network or the compiler.
- Build: `dotnet build PS5Kitsune.slnx -c Release`. Tests: `dotnet test --solution PS5Kitsune.slnx -c Release`; one class: add `-- --filter-class "<full class name>"`. A filtered run exits with code 8 because the other test assemblies match zero tests: that is not a failure. Run CLI commands through the Bash tool.
- Commit locally after each task, author La-Kiks, message ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Do not push. Never add or run the untracked `install.cmd` at the repo root.

## File Structure

| File | Change | Responsibility |
| --- | --- | --- |
| `src/PS5Kitsune.Core/Profiles/AnyPS5Profile.cs` | modify | `ToolDefinition.Size` |
| `src/PS5Kitsune.Core/Profiles/ProfileLoader.cs` | modify | validate `size` |
| `src/PS5Kitsune.Core/Profiles/default-profile.json` | modify | the three sizes |
| `src/PS5Kitsune.Core/Tools/BuildToolsSummary.cs` | create | `PortableToolInfo`, `BuildToolsSummary` |
| `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs` | modify | register `BuildToolsSummary` |
| `src/PS5Kitsune.Core/Pipeline/FractionTracker.cs` | create | keep the largest-total counter |
| `src/PS5Kitsune.Core/AnyPS5/Build/BuildCommands.cs` | modify | use the tracker; `BuildCommandException` Summary/Details |
| `src/PS5Kitsune.Core/AnyPS5/IAnyPS5Installer.cs`, `AnyPS5Installer.cs` | modify | `Supports(mode)` |
| `src/PS5Kitsune.App/Navigation/IPages.cs` | modify | mode-aware pages, `Tools()` |
| `src/PS5Kitsune.App/Shell/PageFactory.cs` | modify | build the new pages |
| `src/PS5Kitsune.App/ViewModels/ToolsViewModel.cs`, `ToolRowViewModel.cs` | create | Tools screen |
| `src/PS5Kitsune.App/Views/ToolsView.axaml(.cs)` | create | Tools screen view |
| `src/PS5Kitsune.App/Views/MainWindow.axaml` | modify | data template for Tools |
| `src/PS5Kitsune.App/ViewModels/SystemCheckViewModel.cs` | modify | takes the mode |
| `src/PS5Kitsune.App/ViewModels/ModeViewModel.cs`, `Views/ModeView.axaml` | modify | selectable cards |
| `src/PS5Kitsune.App/ViewModels/HomeViewModel.cs` | modify | always enabled, opens Mode |
| `src/PS5Kitsune.App/ViewModels/InstallViewModel.cs`, `Views/InstallView.axaml` | modify | progress area, details |
| `src/PS5Kitsune.App/ViewModels/DoneViewModel.cs`, `Views/DoneView.axaml` | modify | build line |
| `tests/PS5Kitsune.App.Tests/Fakes.cs` | modify | `FakePages`, `FakeInstaller.Supports`, `FakeTool` |
| `docs/architecture.md`, `README.md`, the 5b spec | modify | docs |

---

### Task 1: Tool sizes and the build tools summary (Core)

**Files:**
- Modify: `src/PS5Kitsune.Core/Profiles/AnyPS5Profile.cs` (record `ToolDefinition`)
- Modify: `src/PS5Kitsune.Core/Profiles/ProfileLoader.cs` (the `foreach (var (name, tool) in profile.Tools)` loop in `Validate`)
- Modify: `src/PS5Kitsune.Core/Profiles/default-profile.json`
- Create: `src/PS5Kitsune.Core/Tools/BuildToolsSummary.cs`
- Modify: `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Profiles/ProfileLoaderTests.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Tools/BuildToolsSummaryTests.cs` (create)

**Interfaces:**
- Produces: `ToolDefinition(string Version, Uri Url, string Sha256, long? Size = null)`; `PortableToolInfo(string Id, string DisplayName, string Version, long? SizeBytes, bool IsInstalled)`; `BuildToolsSummary(AnyPS5Profile profile, IEnumerable<IPortableTool> tools)` with `IReadOnlyList<PortableToolInfo> Read()`, registered as a singleton.

- [ ] **Step 1: Write the failing profile tests**

Add to `ProfileLoaderTests` (the class already has a private `Load(string json)` helper used by the other tests):

```csharp
    [Fact]
    public void Embedded_profile_gives_the_build_tool_sizes()
    {
        var profile = ProfileLoader.LoadEmbedded();

        Assert.Equal(12601679, profile.Tools["python"].Size);
        Assert.Equal(39806486, profile.Tools["mingit"].Size);
        Assert.Equal(267593703, profile.Tools["winlibs"].Size);
    }

    [Fact]
    public void A_tool_size_is_optional()
    {
        var profile = Load($$"""
            {
              "schemaVersion": 1, "id": "ok", "source": { "kind": "release", "tag": "v1" },
              "tools": { "python": { "version": "3", "url": "https://example.test/p.zip", "sha256": "{{new string('a', 64)}}" } }
            }
            """);

        Assert.Null(profile.Tools["python"].Size);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Rejects_a_tool_size_that_is_not_positive(long size)
    {
        var exception = Assert.Throws<InvalidProfileException>(() => Load($$"""
            {
              "schemaVersion": 1, "id": "bad", "source": { "kind": "release", "tag": "v1" },
              "tools": { "python": { "version": "3", "url": "https://example.test/p.zip", "sha256": "{{new string('a', 64)}}", "size": {{size}} } }
            }
            """));

        Assert.Contains("size", exception.Message, StringComparison.Ordinal);
    }
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.Core.Tests.Profiles.ProfileLoaderTests"`
Expected: build error, `ToolDefinition` has no `Size`.

- [ ] **Step 3: Add `Size`, its validation and the sizes**

In `AnyPS5Profile.cs`, replace the `ToolDefinition` record:

```csharp
/// <param name="Size">Download size of the archive in bytes, shown on the Tools screen; null when the profile does not give it.</param>
public sealed record ToolDefinition(string Version, Uri Url, string Sha256, long? Size = null);
```

In `ProfileLoader.Validate`, extend the tools loop:

```csharp
        foreach (var (name, tool) in profile.Tools)
        {
            RequireSha256(profile.Id, $"tool {name}", tool.Sha256);
            RequireHttps(profile.Id, $"tool {name}", tool.Url);
            if (tool.Size is <= 0)
            {
                throw new InvalidProfileException($"Profile {profile.Id}: tool {name} size must be positive, got {tool.Size.Value.ToString(CultureInfo.InvariantCulture)}.");
            }
        }
```

Add `using System.Globalization;` at the top of `ProfileLoader.cs`.

In `default-profile.json`, add a `size` after each tool's `sha256`:

```json
    "python": {
      "version": "3.14.8",
      "url": "https://www.python.org/ftp/python/3.14.8/python-3.14.8-embed-amd64.zip",
      "sha256": "a93abe456ab01bd96d7a085b3cdb6566b3063f4241360d114142fbdb07f0a310",
      "size": 12601679
    },
    "mingit": {
      "version": "2.56.0.2",
      "url": "https://github.com/git-for-windows/git/releases/download/v2.56.0.windows.2/MinGit-2.56.0.2-64-bit.zip",
      "sha256": "da35e72aa21c005a5a0d298cfbae110bc1609a815730ea0dde84b01a1b3cd3be",
      "size": 39806486
    },
    "winlibs": {
      "version": "15.2.0-r7",
      "url": "https://github.com/brechtsanders/winlibs_mingw/releases/download/15.2.0posix-14.0.0-ucrt-r7/winlibs-x86_64-posix-seh-gcc-15.2.0-mingw-w64ucrt-14.0.0-r7.zip",
      "sha256": "cb2fbad6162540cdf5e1facdce08d4dac359e8cf64f7f696a99274291763b815",
      "size": 267593703
    }
```

- [ ] **Step 4: Run the profile tests**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.Core.Tests.Profiles.ProfileLoaderTests"`
Expected: PASS.

- [ ] **Step 5: Write the failing summary tests**

Create `tests/PS5Kitsune.Core.Tests/Tools/BuildToolsSummaryTests.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.Tests.Tools;

public sealed class BuildToolsSummaryTests
{
    private static ToolDefinition Definition(string version, long? size) =>
        new(version, new Uri("https://example.test/t.zip"), new string('a', 64), size);

    private static AnyPS5Profile Profile(params (string Id, ToolDefinition Definition)[] tools) => new()
    {
        SchemaVersion = AnyPS5Profile.CurrentSchemaVersion,
        Id = "test",
        Source = new ProfileSource("release", Tag: "v1"),
        Tools = tools.ToDictionary(tool => tool.Id, tool => tool.Definition),
    };

    [Fact]
    public void Lists_the_build_tools_in_install_order_with_their_profile_data()
    {
        var profile = Profile(
            ("winlibs", Definition("15.2.0-r7", 300)),
            ("python", Definition("3.14.8", 10)),
            ("mingit", Definition("2.56.0.2", null)));
        var summary = new BuildToolsSummary(profile, [new FakeTool("winlibs", false), new FakeTool("python", true), new FakeTool("mingit", false)]);

        var tools = summary.Read();

        Assert.Equal(
            [
                new PortableToolInfo("python", "Python", "3.14.8", 10, true),
                new PortableToolInfo("mingit", "MinGit", "2.56.0.2", null, false),
                new PortableToolInfo("winlibs", "WinLibs", "15.2.0-r7", 300, false),
            ],
            tools);
    }

    [Fact]
    public void Leaves_out_a_tool_the_profile_or_the_container_lacks()
    {
        var profile = Profile(("python", Definition("3.14.8", 10)), ("mingit", Definition("2.56.0.2", 20)));
        var summary = new BuildToolsSummary(profile, [new FakeTool("python", true), new FakeTool("winlibs", false)]);

        Assert.Equal(["python"], summary.Read().Select(tool => tool.Id));
    }

    [Fact]
    public void Reads_the_installed_state_each_time()
    {
        var python = new FakeTool("python", false);
        var summary = new BuildToolsSummary(Profile(("python", Definition("3.14.8", 10))), [python]);
        Assert.False(summary.Read()[0].IsInstalled);

        python.Installed = true;

        Assert.True(summary.Read()[0].IsInstalled);
    }

    private sealed class FakeTool(string id, bool installed) : IPortableTool
    {
        public string Id => id;

        public bool Installed { get; set; } = installed;

        public bool IsInstalled() => Installed;

        public Task InstallAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<string> GetVersionAsync(CancellationToken cancellationToken = default) => Task.FromResult("1");
    }
}
```

- [ ] **Step 6: Run them to see them fail**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.Core.Tests.Tools.BuildToolsSummaryTests"`
Expected: build error, `BuildToolsSummary` does not exist.

- [ ] **Step 7: Write `BuildToolsSummary` and register it**

Create `src/PS5Kitsune.Core/Tools/BuildToolsSummary.cs`:

```csharp
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Tools;

/// <summary>A portable tool as the Tools screen shows it.</summary>
/// <param name="SizeBytes">Download size from the profile; null when the profile does not give it.</param>
public sealed record PortableToolInfo(string Id, string DisplayName, string Version, long? SizeBytes, bool IsInstalled);

/// <summary>The tools a build-mode install downloads, in install order, with their profile data and whether each is installed.</summary>
public sealed class BuildToolsSummary(AnyPS5Profile profile, IEnumerable<IPortableTool> tools)
{
    private static readonly (string Id, string DisplayName)[] BuildTools =
    [
        (PythonEmbeddableTool.ToolId, "Python"),
        (MinGitTool.ToolId, "MinGit"),
        (WinLibsTool.ToolId, "WinLibs"),
    ];

    /// <summary>Reads the installed state now, with the same check the install steps use. A tool the profile or the container lacks is left out.</summary>
    public IReadOnlyList<PortableToolInfo> Read()
    {
        var registered = tools.ToDictionary(tool => tool.Id);
        return
        [
            .. BuildTools
                .Where(entry => profile.Tools.ContainsKey(entry.Id) && registered.ContainsKey(entry.Id))
                .Select(entry =>
                {
                    var definition = profile.Tools[entry.Id];
                    return new PortableToolInfo(entry.Id, entry.DisplayName, definition.Version, definition.Size, registered[entry.Id].IsInstalled());
                }),
        ];
    }
}
```

In `ServiceCollectionExtensions.AddPS5KitsuneCore`, after the three `AddTool<…>` lines, add:

```csharp
        services.AddSingleton<BuildToolsSummary>();
```

- [ ] **Step 8: Run the Core tests**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.Core.Tests.Tools.BuildToolsSummaryTests"`
Expected: PASS. Then `dotnet build PS5Kitsune.slnx -c Release`: 0 warnings, 0 errors.

- [ ] **Step 9: Commit**

```bash
git add src/PS5Kitsune.Core tests/PS5Kitsune.Core.Tests
git commit -m "Give the tools a download size and summarize the build tools

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: One build counter per command, and failure details (Core)

**Files:**
- Create: `src/PS5Kitsune.Core/Pipeline/FractionTracker.cs`
- Modify: `src/PS5Kitsune.Core/AnyPS5/Build/BuildCommands.cs` (`BuildCommandException` at line 16, `RunAsync`)
- Test: `tests/PS5Kitsune.Core.Tests/Pipeline/FractionTrackerTests.cs` (create)
- Test: `tests/PS5Kitsune.Core.Tests/AnyPS5/Build/BuildPlumbingTests.cs`

**Interfaces:**
- Produces: `FractionTracker` with `bool TryAccept(StepFraction fraction)`; `BuildCommandException(string summary, string details = "")` with `string Summary` and `string Details`; `Message` is `summary` alone when `details` is empty, else `summary + Environment.NewLine + details`.

The 5a criterion run showed a sub-build's own Ninja printing a smaller `[n/m]` between the main counter's lines. A tracker created per command in `BuildCommands.RunAsync` keeps the counter with the largest total, so the app and the CLI both get the fix and no step boundary has to reset it.

- [ ] **Step 1: Write the failing tracker tests**

Create `tests/PS5Kitsune.Core.Tests/Pipeline/FractionTrackerTests.cs`:

```csharp
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.Tests.Pipeline;

public sealed class FractionTrackerTests
{
    [Fact]
    public void Accepts_the_first_counter()
    {
        Assert.True(new FractionTracker().TryAccept(new StepFraction(1, 10)));
    }

    [Fact]
    public void Ignores_a_counter_with_a_smaller_total()
    {
        var tracker = new FractionTracker();
        tracker.TryAccept(new StepFraction(1, 10));

        Assert.False(tracker.TryAccept(new StepFraction(2, 3)));
        Assert.True(tracker.TryAccept(new StepFraction(2, 10)));
    }

    [Fact]
    public void A_larger_total_takes_over()
    {
        var tracker = new FractionTracker();
        tracker.TryAccept(new StepFraction(1, 10));

        Assert.True(tracker.TryAccept(new StepFraction(1, 20)));
        Assert.False(tracker.TryAccept(new StepFraction(3, 10)));
    }
}
```

- [ ] **Step 2: Add a failing plumbing test and failure-details asserts**

In `BuildPlumbingTests`, add after `Ninja_counters_and_error_lines_are_reported`:

```csharp
    [Fact]
    public async Task A_sub_build_counter_does_not_move_the_main_one()
    {
        _setup.Runner.Respond = _ => new FakeProcessResult(0, ["[1/10] a", "[1/3] sub", "[2/3] sub", "[2/10] b", "[1/20] c"]);
        var reports = new List<StepProgress>();

        await _setup.Commands.RunAsync(_setup.Commands.CMake("--build", "x"), _ => "", new SynchronousProgress(reports.Add), TestContext.Current.CancellationToken);

        Assert.Equal(
            [new StepFraction(1, 10), new StepFraction(2, 10), new StepFraction(1, 20)],
            reports.Where(report => report.Fraction is not null).Select(report => report.Fraction!.Value));
    }

    [Fact]
    public void A_command_failure_without_details_is_its_summary()
    {
        var exception = new BuildCommandException("no");

        Assert.Equal("no", exception.Message);
        Assert.Equal("no", exception.Summary);
        Assert.Equal("", exception.Details);
    }
```

In the existing `A_failure_gives_the_message_the_exit_code_and_the_last_lines`, append these asserts after the current ones:

```csharp
        Assert.Equal("The build failed.", exception.Summary);
        Assert.StartsWith("cmake exited with code 1.", exception.Details, StringComparison.Ordinal);
        Assert.Contains("main.cpp:1: error: boom", exception.Details, StringComparison.Ordinal);
        Assert.Equal(exception.Summary + Environment.NewLine + exception.Details, exception.Message);
```

Check how `BuildTestSetup` names the CMake executable (`_setup.Environment.CMake`): if its file name without extension is not `cmake`, use that name in the `StartsWith` assert instead.

In `Low_disk_space_replaces_the_message`, append:

```csharp
        Assert.StartsWith("Not enough disk space on ", exception.Summary, StringComparison.Ordinal);
```

- [ ] **Step 3: Run them to see them fail**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.Core.Tests.Pipeline.FractionTrackerTests"`
Expected: build error, `FractionTracker` and `Summary` do not exist.

- [ ] **Step 4: Write the tracker**

Create `src/PS5Kitsune.Core/Pipeline/FractionTracker.cs`:

```csharp
namespace PS5Kitsune.Core.Pipeline;

/// <summary>
/// Keeps one counter per command. A sub-build that runs its own Ninja prints a smaller "[n/m]" between the main
/// counter's lines, so the counter with the largest total wins and a counter with a smaller total is ignored.
/// </summary>
public sealed class FractionTracker
{
    private int _total;

    /// <returns>True when <paramref name="fraction"/> belongs to the counter being followed.</returns>
    public bool TryAccept(StepFraction fraction)
    {
        if (fraction.Total < _total)
        {
            return false;
        }

        _total = fraction.Total;
        return true;
    }
}
```

- [ ] **Step 5: Use it, and split the exception**

In `BuildCommands.cs`, replace the exception declaration:

```csharp
/// <summary>A git or CMake command failed.</summary>
/// <param name="summary">The targeted message, shown first.</param>
/// <param name="details">The exit code and the last lines of output; empty when there are none.</param>
public sealed class BuildCommandException(string summary, string details = "")
    : Exception(string.IsNullOrEmpty(details) ? summary : summary + Environment.NewLine + details)
{
    public string Summary { get; } = summary;

    public string Details { get; } = details;
}
```

In `RunAsync`, create the tracker next to `tail` and filter the fractions:

```csharp
        var tail = new OutputTail();
        var fractions = new FractionTracker();
        var sawCertificateError = false;
        var result = await runner.RunAsync(spec, line =>
        {
            if (NinjaProgress.TryParse(line.Text, out var fraction))
            {
                if (fractions.TryAccept(fraction))
                {
                    progress?.Report(new StepProgress(Fraction: fraction));
                }

                return;
            }
```

And replace the `throw` at the end of `RunAsync`:

```csharp
        var lines = tail.ToString();
        var message = LowDiskMessage() ?? describeFailure(new CommandFailure(result.ExitCode, lines, sawCertificateError));
        throw new BuildCommandException(
            message,
            $"{Path.GetFileNameWithoutExtension(spec.FileName)} exited with code {result.ExitCode}.{Environment.NewLine}{lines}");
```

`Message` stays exactly as before, so the CLI output does not change.

- [ ] **Step 6: Run the tests**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.Core.Tests.Pipeline.FractionTrackerTests"`, then `-- --filter-class "PS5Kitsune.Core.Tests.AnyPS5.Build.BuildPlumbingTests"`, then `-- --filter-class "PS5Kitsune.Core.Tests.AnyPS5.Build.BuildStepsTests"`.
Expected: PASS for all three.

- [ ] **Step 7: Commit**

```bash
git add src/PS5Kitsune.Core tests/PS5Kitsune.Core.Tests
git commit -m "Follow one build counter per command and split failure details

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Mode-aware pages, the PC check by mode, and the Tools screen

**Files:**
- Modify: `src/PS5Kitsune.App/Navigation/IPages.cs`
- Modify: `src/PS5Kitsune.App/Shell/PageFactory.cs`
- Modify: `src/PS5Kitsune.App/ViewModels/SystemCheckViewModel.cs`
- Modify: `src/PS5Kitsune.App/ViewModels/HomeViewModel.cs` (`Start` only)
- Modify: `src/PS5Kitsune.App/ViewModels/ModeViewModel.cs` (`Install` only)
- Create: `src/PS5Kitsune.App/ViewModels/ToolRowViewModel.cs`
- Create: `src/PS5Kitsune.App/ViewModels/ToolsViewModel.cs`
- Create: `src/PS5Kitsune.App/Views/ToolsView.axaml`, `src/PS5Kitsune.App/Views/ToolsView.axaml.cs`
- Modify: `src/PS5Kitsune.App/Views/MainWindow.axaml`
- Test: `tests/PS5Kitsune.App.Tests/Fakes.cs`, `SystemCheckViewModelTests.cs`, `HomeViewModelTests.cs`, `ModeViewModelTests.cs`, `PageFactoryTests.cs`
- Test: `tests/PS5Kitsune.App.Tests/ToolsViewModelTests.cs` (create)

**Interfaces:**
- Consumes: `BuildToolsSummary.Read()` and `PortableToolInfo` (Task 1).
- Produces:
  ```csharp
  public interface IPages
  {
      object Home();
      object Mode();
      object SystemCheck(AnyPS5Mode mode);
      object Tools();
      object Install(AnyPS5Mode mode);
      object Done();
  }
  ```
  `SystemCheckViewModel(ISystemChecker checker, AnyPS5Mode mode, IPlatform platform, Navigator navigator, IPages pages)`; `ToolsViewModel(BuildToolsSummary summary, Navigator navigator, IPages pages)`. `FakePages` returns `"home"`, `"mode"`, `$"check:{mode}"`, `"tools"`, `$"install:{mode}"`, `"done"`.

Until Task 4, Home still opens the release check and Mode still opens the release install: this task only changes the page contract, the check and the Tools screen.

- [ ] **Step 1: Update the fakes and the existing navigation asserts**

In `tests/PS5Kitsune.App.Tests/Fakes.cs`, replace `FakePages`:

```csharp
/// <summary>Pages are plain strings, so a test asserts where the navigator went, and with which mode.</summary>
internal sealed class FakePages : IPages
{
    public object Home() => "home";

    public object Mode() => "mode";

    public object SystemCheck(AnyPS5Mode mode) => $"check:{mode}";

    public object Tools() => "tools";

    public object Install(AnyPS5Mode mode) => $"install:{mode}";

    public object Done() => "done";
}
```

Add to the same file, for the Tools tests:

```csharp
internal sealed class FakeTool(string id, bool installed) : IPortableTool
{
    public string Id => id;

    public bool Installed { get; set; } = installed;

    public bool IsInstalled() => Installed;

    public Task InstallAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<string> GetVersionAsync(CancellationToken cancellationToken = default) => Task.FromResult("1");
}
```

with `using PS5Kitsune.Core.Tools;` at the top (`DownloadProgress` is in `PS5Kitsune.Core.Abstractions`, already imported).

Update the existing asserts:
- `HomeViewModelTests.Start_goes_to_the_pc_check`: `Assert.Equal("check:Release", _navigator.CurrentPage);`
- `ModeViewModelTests.Install_goes_to_the_install_page`: `Assert.Equal("install:Release", _navigator.CurrentPage);`

- [ ] **Step 2: Write the failing check tests**

In `SystemCheckViewModelTests`, change `FakeChecker` to record the mode:

```csharp
    private sealed class FakeChecker(Func<CancellationToken, Task<SystemCheckReport>> answer) : ISystemChecker
    {
        public AnyPS5Mode? Mode { get; private set; }

        public Task<SystemCheckReport> RunAsync(AnyPS5Mode mode, CancellationToken cancellationToken = default)
        {
            Mode = mode;
            return answer(cancellationToken);
        }
    }
```

Change the factory so a test can pick the mode and see the checker:

```csharp
    private FakeChecker? _checker;

    private SystemCheckViewModel Create(Func<CancellationToken, Task<SystemCheckReport>> answer, AnyPS5Mode mode = AnyPS5Mode.Release)
    {
        _checker = new FakeChecker(answer);
        return new(_checker, mode, _platform, _navigator, new FakePages());
    }
```

Replace `Continue_goes_to_the_mode_page` with:

```csharp
    [Theory]
    [InlineData(AnyPS5Mode.Release, "install:Release")]
    [InlineData(AnyPS5Mode.Build, "tools")]
    public async Task Checks_for_the_mode_and_continues_to_its_next_page(AnyPS5Mode mode, string next)
    {
        var viewModel = Create(_ => Task.FromResult(Report()), mode);
        await viewModel.CheckCommand.ExecuteAsync(null);

        viewModel.ContinueCommand.Execute(null);

        Assert.Equal(mode, _checker!.Mode);
        Assert.Equal(next, _navigator.CurrentPage);
    }
```

- [ ] **Step 3: Write the failing Tools tests**

Create `tests/PS5Kitsune.App.Tests/ToolsViewModelTests.cs`:

```csharp
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.App.Tests;

public sealed class ToolsViewModelTests
{
    private readonly Navigator _navigator = new();
    private readonly FakeTool _python = new("python", true);
    private readonly FakeTool _mingit = new("mingit", false);
    private readonly FakeTool _winlibs = new("winlibs", false);

    private ToolsViewModel Create(long? winLibsSize = 267593703)
    {
        var profile = TestSessions.Profile with
        {
            Tools = new Dictionary<string, ToolDefinition>
            {
                ["python"] = Definition("3.14.8", 12601679),
                ["mingit"] = Definition("2.56.0.2", 39806486),
                ["winlibs"] = Definition("15.2.0-r7", winLibsSize),
            },
        };
        var viewModel = new ToolsViewModel(new BuildToolsSummary(profile, [_python, _mingit, _winlibs]), _navigator, new FakePages());
        viewModel.OnNavigatedTo();
        return viewModel;
    }

    private static ToolDefinition Definition(string version, long? size) =>
        new(version, new Uri("https://example.test/t.zip"), new string('a', 64), size);

    [Fact]
    public void Shows_each_tool_with_its_state_and_size()
    {
        var viewModel = Create();

        Assert.Equal(["Python", "MinGit", "WinLibs"], viewModel.Tools.Select(tool => tool.Name));
        Assert.Equal(["3.14.8", "2.56.0.2", "15.2.0-r7"], viewModel.Tools.Select(tool => tool.Version));
        Assert.Equal(["Installed", "Missing", "Missing"], viewModel.Tools.Select(tool => tool.StatusText));
        Assert.Equal(["", "38.0 MB", "255.2 MB"], viewModel.Tools.Select(tool => tool.SizeText));
    }

    [Fact]
    public void Totals_the_missing_tools()
    {
        var viewModel = Create();

        Assert.Equal("To download: 293.2 MB", viewModel.DownloadText);
        Assert.Equal("Disk space for the build: about 3 GB.", viewModel.DiskText);
        Assert.Equal("Download what's missing and build", viewModel.ContinueText);
    }

    [Fact]
    public void A_missing_tool_without_a_size_makes_the_total_a_minimum()
    {
        var viewModel = Create(winLibsSize: null);

        Assert.Equal("size unknown", viewModel.Tools[2].SizeText);
        Assert.Equal("To download: at least 38.0 MB", viewModel.DownloadText);
    }

    [Fact]
    public void Only_tools_without_a_size_missing_gives_an_unknown_total()
    {
        _mingit.Installed = true;
        var viewModel = Create(winLibsSize: null);

        Assert.Equal("To download: size unknown", viewModel.DownloadText);
    }

    [Fact]
    public void Nothing_missing_says_so_and_offers_to_build()
    {
        _mingit.Installed = true;
        _winlibs.Installed = true;

        var viewModel = Create();

        Assert.Equal("All tools are present.", viewModel.DownloadText);
        Assert.Equal("Build", viewModel.ContinueText);
    }

    [Fact]
    public void Entering_the_page_reads_the_tools_again()
    {
        var viewModel = Create();
        _mingit.Installed = true;
        _winlibs.Installed = true;

        viewModel.OnNavigatedTo();

        Assert.Equal("All tools are present.", viewModel.DownloadText);
        Assert.Equal(3, viewModel.Tools.Count);
    }

    [Fact]
    public void Continue_installs_in_build_mode()
    {
        Create().ContinueCommand.Execute(null);

        Assert.Equal("install:Build", _navigator.CurrentPage);
    }

    [Fact]
    public void Back_returns_to_the_previous_page()
    {
        _navigator.GoTo("check:Build");
        var viewModel = Create();
        _navigator.GoTo(viewModel);

        viewModel.BackCommand.Execute(null);

        Assert.Equal("check:Build", _navigator.CurrentPage);
    }
}
```

(38.0 = 39806486 / 1048576; 255.2 = 267593703 / 1048576; 293.2 is their sum.)

- [ ] **Step 4: Update the page factory test**

Replace the body of `PageFactoryTests.Builds_every_page_from_a_real_session` after `var pages = …`:

```csharp
        Assert.IsType<HomeViewModel>(pages.Home());
        Assert.IsType<ModeViewModel>(pages.Mode());
        Assert.IsType<SystemCheckViewModel>(pages.SystemCheck(AnyPS5Mode.Build));
        var tools = Assert.IsType<ToolsViewModel>(pages.Tools());
        tools.OnNavigatedTo();
        Assert.Equal(3, tools.Tools.Count);
        Assert.Equal(6, Assert.IsType<InstallViewModel>(pages.Install(AnyPS5Mode.Release)).Steps.Count);
        Assert.Equal(10, Assert.IsType<InstallViewModel>(pages.Install(AnyPS5Mode.Build)).Steps.Count);
        Assert.IsType<DoneViewModel>(pages.Done());
```

with `using PS5Kitsune.Core.AnyPS5;` added.

- [ ] **Step 5: Run the App tests to see them fail**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.App.Tests.ToolsViewModelTests"`
Expected: build errors (`IPages` members, `ToolsViewModel`).

- [ ] **Step 6: Change the page contract and the factory**

`src/PS5Kitsune.App/Navigation/IPages.cs`:

```csharp
using PS5Kitsune.Core.AnyPS5;

namespace PS5Kitsune.App.Navigation;

/// <summary>
/// Creates page view models for the current data root. Pages are objects: the window picks a view by type.
/// The chosen mode travels from page to page as an argument, never in the session.
/// </summary>
public interface IPages
{
    object Home();

    object Mode();

    object SystemCheck(AnyPS5Mode mode);

    /// <summary>The build tools screen; only build mode goes through it.</summary>
    object Tools();

    object Install(AnyPS5Mode mode);

    object Done();
}
```

`PageFactory` (add `using PS5Kitsune.Core.Tools;`):

```csharp
    public object Home() => new HomeViewModel(sessions, settings, dialogs, navigator, this);

    public object Mode() => new ModeViewModel(Session.Get<AnyPS5Profile>(), navigator, this);

    public object SystemCheck(AnyPS5Mode mode) => new SystemCheckViewModel(Session.Get<ISystemChecker>(), mode, platform, navigator, this);

    public object Tools() => new ToolsViewModel(Session.Get<BuildToolsSummary>(), navigator, this);

    public object Install(AnyPS5Mode mode) =>
        new InstallViewModel(Session.Get<IAnyPS5Installer>(), mode, Session.LogFile, platform, ui, navigator, this, busy => sessions.IsBusy = busy);

    public object Done() =>
        new DoneViewModel(Session.Get<IAnyPS5Installer>().ReadRecord(), Session.Get<AnyPS5Paths>(), Session.LogFile, platform, navigator, this);
```

Interim callers (Task 4 rewires them):
- `HomeViewModel.Start`: `private void Start() => navigator.GoTo(pages.SystemCheck(AnyPS5Mode.Release));`
- `ModeViewModel.Install`: `private void Install() => navigator.GoTo(pages.Install(AnyPS5Mode.Release));` (add `using PS5Kitsune.Core.AnyPS5;`)

- [ ] **Step 7: Give the check its mode**

In `SystemCheckViewModel`:
- primary constructor: `SystemCheckViewModel(ISystemChecker checker, AnyPS5Mode mode, IPlatform platform, Navigator navigator, IPages pages)`;
- class summary: `/// <summary>The PC check screen for the chosen mode: runs every check and shows the results with a verdict. A blocking result stops the install here.</summary>`;
- `OnNavigatedTo` summary: `/// <summary>Checks the first time the page is shown, and again after a cancelled check; not when coming back from a later page.</summary>`;
- in `CheckAsync`: `var report = await checker.RunAsync(mode, cancellationToken).ConfigureAwait(true);`
- `Continue`:

```csharp
    /// <summary>Build mode shows its tools first; release goes straight to the install.</summary>
    [RelayCommand(CanExecute = nameof(CanContinue))]
    private void Continue() => navigator.GoTo(mode == AnyPS5Mode.Build ? pages.Tools() : pages.Install(mode));
```

- [ ] **Step 8: Write the Tools view models**

`src/PS5Kitsune.App/ViewModels/ToolRowViewModel.cs`:

```csharp
using System.Globalization;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.App.ViewModels;

/// <summary>One tool on the Tools screen.</summary>
public sealed class ToolRowViewModel(PortableToolInfo info)
{
    public string Name => info.DisplayName;

    public string Version => info.Version;

    public bool IsInstalled => info.IsInstalled;

    public string StatusText => info.IsInstalled ? "Installed" : "Missing";

    /// <summary>Empty for an installed tool.</summary>
    public string SizeText => info.IsInstalled ? ""
        : info.SizeBytes is { } size ? FormatMegabytes(size)
        : "size unknown";

    internal static string FormatMegabytes(long bytes) => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1048576.0:0.0} MB");
}
```

`src/PS5Kitsune.App/ViewModels/ToolsViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.App.ViewModels;

/// <summary>
/// Build mode's tools: what is installed, what is missing and its download size. The install's first steps
/// download what is missing, so this page only hands off to Install.
/// </summary>
public sealed partial class ToolsViewModel(BuildToolsSummary summary, Navigator navigator, IPages pages)
    : ObservableObject, INavigationAware
{
    public ObservableCollection<ToolRowViewModel> Tools { get; } = [];

    /// <summary>The 5a criterion run measured a full build root at 3.2 GB.</summary>
    public string DiskText => "Disk space for the build: about 3 GB.";

    [ObservableProperty]
    public partial string DownloadText { get; private set; } = "";

    [ObservableProperty]
    public partial string ContinueText { get; private set; } = "";

    /// <summary>Reads the tools on every visit: coming back after a cancelled install may find more of them.</summary>
    public void OnNavigatedTo()
    {
        Tools.Clear();
        foreach (var tool in summary.Read())
        {
            Tools.Add(new ToolRowViewModel(tool));
        }

        var missing = Tools.Where(tool => !tool.IsInstalled).Select(tool => tool.Info).ToList();
        var known = missing.Sum(tool => tool.SizeBytes ?? 0);
        var anyUnknown = missing.Any(tool => tool.SizeBytes is null);
        DownloadText = missing.Count == 0 ? "All tools are present."
            : known == 0 ? "To download: size unknown"
            : anyUnknown ? $"To download: at least {ToolRowViewModel.FormatMegabytes(known)}"
            : $"To download: {ToolRowViewModel.FormatMegabytes(known)}";
        ContinueText = missing.Count == 0 ? "Build" : "Download what's missing and build";
    }

    [RelayCommand]
    private void Back() => navigator.GoBack();

    [RelayCommand]
    private void Continue() => navigator.GoTo(pages.Install(AnyPS5Mode.Build));
}
```

`ToolsViewModel` reads `tool.Info`, so add to `ToolRowViewModel`:

```csharp
    public PortableToolInfo Info => info;
```

- [ ] **Step 9: Write the Tools view and its template**

`src/PS5Kitsune.App/Views/ToolsView.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:PS5Kitsune.App.ViewModels"
             x:Class="PS5Kitsune.App.Views.ToolsView"
             x:DataType="vm:ToolsViewModel">
  <DockPanel>
    <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right" Margin="0,12,0,0">
      <Button Content="Back" Command="{Binding BackCommand}" />
      <Button Content="{Binding ContinueText}" Classes="accent" Command="{Binding ContinueCommand}" />
    </StackPanel>

    <StackPanel Spacing="16" MaxWidth="680" HorizontalAlignment="Left">
      <TextBlock Text="Build tools" FontSize="20" FontWeight="SemiBold" />
      <TextBlock Text="Build mode compiles AnyPS5 with portable tools kept in the data folder. Nothing is installed system-wide."
                 TextWrapping="Wrap" Opacity="0.85" />
      <ItemsControl ItemsSource="{Binding Tools}">
        <ItemsControl.ItemTemplate>
          <DataTemplate x:DataType="vm:ToolRowViewModel">
            <Grid ColumnDefinitions="90,*,Auto" ColumnSpacing="12" Margin="0,6">
              <Border CornerRadius="4" Padding="8,2" HorizontalAlignment="Left" VerticalAlignment="Center"
                      Classes.installed="{Binding IsInstalled}">
                <Border.Styles>
                  <Style Selector="Border">
                    <Setter Property="Background" Value="#808080" />
                  </Style>
                  <Style Selector="Border.installed">
                    <Setter Property="Background" Value="#2E7D32" />
                  </Style>
                </Border.Styles>
                <TextBlock Text="{Binding StatusText}" Foreground="White" FontSize="12" FontWeight="SemiBold" />
              </Border>
              <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="8" VerticalAlignment="Center">
                <TextBlock Text="{Binding Name}" FontWeight="SemiBold" />
                <TextBlock Text="{Binding Version}" Opacity="0.7" />
              </StackPanel>
              <TextBlock Grid.Column="2" Text="{Binding SizeText}" Opacity="0.85" VerticalAlignment="Center" />
            </Grid>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
      <StackPanel Spacing="4">
        <TextBlock Text="{Binding DownloadText}" FontWeight="SemiBold" />
        <TextBlock Text="{Binding DiskText}" Opacity="0.85" />
      </StackPanel>
    </StackPanel>
  </DockPanel>
</UserControl>
```

Before writing the badge colors, check `src/PS5Kitsune.App/Converters/CheckStatusBrushConverter.cs`: if it exposes the green it uses for `CheckStatus.Ok`, use that same value instead of `#2E7D32`, so the screens match.

`src/PS5Kitsune.App/Views/ToolsView.axaml.cs`, following the other views' code-behind (e.g. `ModeView.axaml.cs`):

```csharp
using Avalonia.Controls;

namespace PS5Kitsune.App.Views;

public partial class ToolsView : UserControl
{
    public ToolsView() => InitializeComponent();
}
```

Copy the exact shape of `ModeView.axaml.cs` if it differs (namespace usings, `sealed`, constructor form).

In `MainWindow.axaml`, add after the `ModeViewModel` template:

```xml
    <DataTemplate DataType="vm:ToolsViewModel">
      <views:ToolsView />
    </DataTemplate>
```

- [ ] **Step 10: Run all tests and the build**

Run: `dotnet build PS5Kitsune.slnx -c Release`, then `dotnet test --solution PS5Kitsune.slnx -c Release`.
Expected: 0 warnings; all tests pass except the 1 explicit network test, which is skipped.

- [ ] **Step 11: Commit**

```bash
git add src/PS5Kitsune.App tests/PS5Kitsune.App.Tests
git commit -m "Pass the mode between pages, check by mode and add the Tools screen

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Mode before the check, selectable cards, and Home for build installs

**Files:**
- Modify: `src/PS5Kitsune.Core/AnyPS5/IAnyPS5Installer.cs`, `src/PS5Kitsune.Core/AnyPS5/AnyPS5Installer.cs`
- Modify: `src/PS5Kitsune.App/ViewModels/ModeViewModel.cs`, `src/PS5Kitsune.App/Views/ModeView.axaml`
- Modify: `src/PS5Kitsune.App/ViewModels/HomeViewModel.cs`, `src/PS5Kitsune.App/Shell/PageFactory.cs` (`Mode()`)
- Test: `tests/PS5Kitsune.Core.Tests/AnyPS5/AnyPS5InstallerTests.cs`
- Test: `tests/PS5Kitsune.App.Tests/Fakes.cs`, `ModeViewModelTests.cs`, `HomeViewModelTests.cs`

**Interfaces:**
- Consumes: `IPages.SystemCheck(AnyPS5Mode)` (Task 3).
- Produces: `bool IAnyPS5Installer.Supports(AnyPS5Mode mode)`; `ModeViewModel(AnyPS5Profile profile, IAnyPS5Installer installer, Navigator navigator, IPages pages)` with `SelectedMode`, `IsReleaseSelected`, `IsBuildSelected`, `CanBuild`, `SelectReleaseCommand`, `SelectBuildCommand`, `ContinueCommand`, `BackCommand`; `FakeInstaller.BuildSupported` (default true).

- [ ] **Step 1: Write the failing Core test**

In `AnyPS5InstallerTests`, add after `A_mode_without_a_source_is_refused`:

```csharp
    [Fact]
    public void Says_which_modes_the_profile_supports()
    {
        var releaseOnly = new AnyPS5Installer([_setup.ReleaseSource], _setup.PythonTool, _setup.Paths, _setup.StepRunner, _setup.Time);

        Assert.True(releaseOnly.Supports(AnyPS5Mode.Release));
        Assert.False(releaseOnly.Supports(AnyPS5Mode.Build));
    }
```

- [ ] **Step 2: Add `Supports`**

In `IAnyPS5Installer`, after `RequestedMode`:

```csharp
    /// <summary>True when the active profile can install <paramref name="mode"/>; build needs a build section and the build tools.</summary>
    bool Supports(AnyPS5Mode mode);
```

In `AnyPS5Installer`, after `CurrentMode`:

```csharp
    public bool Supports(AnyPS5Mode mode) => Source(mode) is not null;
```

In `tests/PS5Kitsune.App.Tests/Fakes.cs`, add to `FakeInstaller`:

```csharp
    /// <summary>False simulates a profile without a build section.</summary>
    public bool BuildSupported { get; set; } = true;

    public bool Supports(AnyPS5Mode mode) => mode == AnyPS5Mode.Release || BuildSupported;
```

Run: `dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.Core.Tests.AnyPS5.AnyPS5InstallerTests"`
Expected: PASS.

- [ ] **Step 3: Write the failing Mode tests**

Replace `tests/PS5Kitsune.App.Tests/ModeViewModelTests.cs`:

```csharp
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.App.Tests;

public sealed class ModeViewModelTests
{
    private static readonly AnyPS5Profile BuildProfile = TestSessions.Profile with
    {
        Build = new ProfileBuild(new Uri("https://github.com/boykopovar/AnyPS5.git"), "3fd1cc393b8512c2e989860053f30dee5a8cd1aa"),
        Checks = new CheckThresholds { BuildMinFreeDiskGiB = 10 },
    };

    private readonly Navigator _navigator = new();
    private readonly FakeInstaller _installer = new();

    private ModeViewModel Create(AnyPS5Profile? profile = null) => new(profile ?? BuildProfile, _installer, _navigator, new FakePages());

    [Fact]
    public void Describes_the_release()
    {
        var viewModel = Create();

        Assert.Equal("Release v0.1.2", viewModel.ReleaseTitle);
        Assert.Equal("Published October 14, 2026", viewModel.PublishedText);
        Assert.Equal("Validated with DOOM for PS5", viewModel.ProfileStatusText);
    }

    [Fact]
    public void Says_when_the_date_or_validation_is_missing()
    {
        var viewModel = Create(BuildProfile with { Validated = false, Source = new ProfileSource("release", Tag: "v9") });

        Assert.Equal("Publication date unknown", viewModel.PublishedText);
        Assert.Equal("Not validated yet", viewModel.ProfileStatusText);
    }

    [Fact]
    public void Describes_the_build()
    {
        var viewModel = Create();

        Assert.True(viewModel.CanBuild);
        Assert.Equal("Commit 3fd1cc3", viewModel.BuildCommitText);
        Assert.Equal("About 15 minutes the first time, faster for updates.", viewModel.BuildDurationText);
        Assert.Equal("Needs about 10 GiB of free disk space.", viewModel.BuildDiskText);
    }

    [Theory]
    [InlineData(AnyPS5Mode.Release)]
    [InlineData(AnyPS5Mode.Build)]
    public void Preselects_the_installer_s_current_mode(AnyPS5Mode current)
    {
        _installer.CurrentMode = current;

        var viewModel = Create();

        Assert.Equal(current, viewModel.SelectedMode);
        Assert.Equal(current == AnyPS5Mode.Release, viewModel.IsReleaseSelected);
        Assert.Equal(current == AnyPS5Mode.Build, viewModel.IsBuildSelected);
    }

    [Fact]
    public void Without_a_build_the_card_is_disabled_and_release_is_preselected()
    {
        _installer.CurrentMode = AnyPS5Mode.Build;
        _installer.BuildSupported = false;

        var viewModel = Create(TestSessions.Profile);

        Assert.False(viewModel.CanBuild);
        Assert.False(viewModel.SelectBuildCommand.CanExecute(null));
        Assert.Equal(AnyPS5Mode.Release, viewModel.SelectedMode);
        Assert.Equal("", viewModel.BuildCommitText);
    }

    [Fact]
    public void Selecting_a_card_changes_the_mode()
    {
        var viewModel = Create();

        viewModel.SelectBuildCommand.Execute(null);
        Assert.True(viewModel.IsBuildSelected);

        viewModel.SelectReleaseCommand.Execute(null);
        Assert.True(viewModel.IsReleaseSelected);
    }

    [Theory]
    [InlineData(false, "check:Release")]
    [InlineData(true, "check:Build")]
    public void Continue_checks_the_pc_for_the_selected_mode(bool build, string next)
    {
        var viewModel = Create();
        if (build)
        {
            viewModel.SelectBuildCommand.Execute(null);
        }

        viewModel.ContinueCommand.Execute(null);

        Assert.Equal(next, _navigator.CurrentPage);
    }

    [Fact]
    public void Back_returns_home()
    {
        _navigator.GoTo("home");
        var viewModel = Create();
        _navigator.GoTo(viewModel);

        viewModel.BackCommand.Execute(null);

        Assert.Equal("home", _navigator.CurrentPage);
    }
}
```

- [ ] **Step 4: Rewrite the Home tests for build installs**

In `HomeViewModelTests`, replace the four tests `A_build_install_is_shown_and_repaired_only_from_the_command_line`, `An_unfinished_build_is_shown_and_resumed_only_from_the_command_line`, `Leaving_build_mode_enables_the_action_again` and `Start_goes_to_the_pc_check` with:

```csharp
    [Fact]
    public void A_build_install_is_shown_and_can_be_repaired()
    {
        _installer.CurrentMode = AnyPS5Mode.Build;
        _installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, BuildRecord);

        var viewModel = Create();

        Assert.Equal("AnyPS5 48d61f5 is installed (built from source)", viewModel.StateTitle);
        Assert.Contains(BuildRecord.InstalledAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), viewModel.StateDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("command line", viewModel.StateDetail, StringComparison.Ordinal);
        Assert.Equal("Verify / repair", viewModel.ActionText);
        Assert.True(viewModel.StartCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(AnyPS5InstallStatus.Incomplete)]
    [InlineData(AnyPS5InstallStatus.NotInstalled)]
    public void An_unfinished_build_can_be_resumed(AnyPS5InstallStatus status)
    {
        _installer.CurrentMode = AnyPS5Mode.Build;
        _installer.State = new AnyPS5InstallState(status, null);

        var viewModel = Create();

        Assert.Equal("The AnyPS5 build is incomplete", viewModel.StateTitle);
        Assert.Equal("Resume it to finish: the steps already done are skipped.", viewModel.StateDetail);
        Assert.Equal("Resume the install", viewModel.ActionText);
        Assert.True(viewModel.StartCommand.CanExecute(null));
    }

    [Fact]
    public void Start_goes_to_the_mode_page()
    {
        Create().StartCommand.Execute(null);

        Assert.Equal("mode", _navigator.CurrentPage);
    }
```

- [ ] **Step 5: Run them to see them fail**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.App.Tests.ModeViewModelTests"`
Expected: build errors (`ModeViewModel` constructor and members).

- [ ] **Step 6: Rewrite `ModeViewModel`**

```csharp
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.App.ViewModels;

/// <summary>Release or build, chosen before the PC check because the check depends on the mode.</summary>
public sealed partial class ModeViewModel : ObservableObject
{
    private readonly AnyPS5Profile _profile;
    private readonly Navigator _navigator;
    private readonly IPages _pages;

    /// <summary>Preselects the mode the CLI would use without --mode: an unfinished install's, else the installed one's, else release.</summary>
    public ModeViewModel(AnyPS5Profile profile, IAnyPS5Installer installer, Navigator navigator, IPages pages)
    {
        _profile = profile;
        _navigator = navigator;
        _pages = pages;
        CanBuild = installer.Supports(AnyPS5Mode.Build);
        SelectedMode = installer.CurrentMode == AnyPS5Mode.Build && CanBuild ? AnyPS5Mode.Build : AnyPS5Mode.Release;
    }

    public string ReleaseTitle => $"Release {_profile.Source.Version}";

    public string PublishedText => _profile.Source.PublishedAt is { } publishedAt
        ? "Published " + publishedAt.UtcDateTime.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture)
        : "Publication date unknown";

    public string ProfileStatusText => _profile.Validated ? "Validated with DOOM for PS5" : "Not validated yet";

    /// <summary>False when the active profile has no build section or no build tools.</summary>
    public bool CanBuild { get; }

    public string BuildCommitText => _profile.Build is { } build ? $"Commit {build.Commit[..7]}" : "";

    public string BuildDurationText => "About 15 minutes the first time, faster for updates.";

    public string BuildDiskText => string.Create(CultureInfo.InvariantCulture, $"Needs about {_profile.Checks.BuildMinFreeDiskGiB} GiB of free disk space.");

    public string BuildUnavailableText => "This profile does not define a build.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReleaseSelected))]
    [NotifyPropertyChangedFor(nameof(IsBuildSelected))]
    public partial AnyPS5Mode SelectedMode { get; private set; }

    public bool IsReleaseSelected => SelectedMode == AnyPS5Mode.Release;

    public bool IsBuildSelected => SelectedMode == AnyPS5Mode.Build;

    [RelayCommand]
    private void SelectRelease() => SelectedMode = AnyPS5Mode.Release;

    [RelayCommand(CanExecute = nameof(CanBuild))]
    private void SelectBuild() => SelectedMode = AnyPS5Mode.Build;

    [RelayCommand]
    private void Back() => _navigator.GoBack();

    [RelayCommand]
    private void Continue() => _navigator.GoTo(_pages.SystemCheck(SelectedMode));
}
```

In `PageFactory`: `public object Mode() => new ModeViewModel(Session.Get<AnyPS5Profile>(), Session.Get<IAnyPS5Installer>(), navigator, this);`

- [ ] **Step 7: Rewrite `ModeView.axaml`**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:PS5Kitsune.App.ViewModels"
             x:Class="PS5Kitsune.App.Views.ModeView"
             x:DataType="vm:ModeViewModel">
  <UserControl.Styles>
    <Style Selector="Button.card">
      <Setter Property="HorizontalAlignment" Value="Stretch" />
      <Setter Property="VerticalAlignment" Value="Stretch" />
      <Setter Property="HorizontalContentAlignment" Value="Left" />
      <Setter Property="VerticalContentAlignment" Value="Top" />
      <Setter Property="Padding" Value="20" />
      <Setter Property="CornerRadius" Value="6" />
      <Setter Property="BorderThickness" Value="2" />
    </Style>
    <Style Selector="Button.card /template/ ContentPresenter#PART_ContentPresenter">
      <Setter Property="BorderBrush" Value="#40808080" />
    </Style>
    <Style Selector="Button.card:pointerover /template/ ContentPresenter#PART_ContentPresenter">
      <Setter Property="BorderBrush" Value="#80808080" />
    </Style>
    <Style Selector="Button.card.selected /template/ ContentPresenter#PART_ContentPresenter">
      <Setter Property="BorderBrush">
        <SolidColorBrush Color="{DynamicResource SystemAccentColor}" />
      </Setter>
    </Style>
    <Style Selector="Button.card:disabled">
      <Setter Property="Opacity" Value="0.5" />
    </Style>
  </UserControl.Styles>

  <DockPanel>
    <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right" Margin="0,12,0,0">
      <Button Content="Back" Command="{Binding BackCommand}" />
      <Button Content="Continue" Classes="accent" Command="{Binding ContinueCommand}" />
    </StackPanel>

    <StackPanel Spacing="16">
      <TextBlock Text="How should AnyPS5 be installed?" FontSize="20" FontWeight="SemiBold" />
      <Grid ColumnDefinitions="*,*" ColumnSpacing="16">
        <Button Classes="card" Classes.selected="{Binding IsReleaseSelected}" Command="{Binding SelectReleaseCommand}">
          <StackPanel Spacing="6">
            <TextBlock Text="{Binding ReleaseTitle}" FontSize="16" FontWeight="SemiBold" />
            <TextBlock Text="{Binding PublishedText}" Opacity="0.85" />
            <TextBlock Text="{Binding ProfileStatusText}" Opacity="0.85" />
            <TextBlock Text="A few minutes, no compilation." Opacity="0.85" />
            <TextBlock Text="Selected" FontWeight="SemiBold" Margin="0,8,0,0" IsVisible="{Binding IsReleaseSelected}" />
          </StackPanel>
        </Button>
        <Button Grid.Column="1" Classes="card" Classes.selected="{Binding IsBuildSelected}" Command="{Binding SelectBuildCommand}">
          <StackPanel Spacing="6">
            <TextBlock Text="Build from source" FontSize="16" FontWeight="SemiBold" />
            <StackPanel Spacing="6" IsVisible="{Binding CanBuild}">
              <TextBlock Text="{Binding BuildCommitText}" Opacity="0.85" />
              <TextBlock Text="{Binding ProfileStatusText}" Opacity="0.85" />
              <TextBlock Text="{Binding BuildDurationText}" Opacity="0.85" TextWrapping="Wrap" />
              <TextBlock Text="{Binding BuildDiskText}" Opacity="0.85" TextWrapping="Wrap" />
            </StackPanel>
            <TextBlock Text="{Binding BuildUnavailableText}" FontStyle="Italic" TextWrapping="Wrap" IsVisible="{Binding !CanBuild}" />
            <TextBlock Text="Selected" FontWeight="SemiBold" Margin="0,8,0,0" IsVisible="{Binding IsBuildSelected}" />
          </StackPanel>
        </Button>
      </Grid>
    </StackPanel>
  </DockPanel>
</UserControl>
```

- [ ] **Step 8: Simplify Home**

In `HomeViewModel`:
- delete the `BuildDetail` constant and the `CanStart` property with its comment;
- `[RelayCommand(CanExecute = nameof(CanStart))]` becomes `[RelayCommand]`, and `Start` becomes `private void Start() => navigator.GoTo(pages.Mode());`
- replace the block from `CanStart = mode != AnyPS5Mode.Build;` to the end of `Refresh` with:

```csharp
        if (mode == AnyPS5Mode.Build)
        {
            StateTitle = state.Status == AnyPS5InstallStatus.Installed
                ? (state.Record is null ? "AnyPS5 is installed (built from source)" : $"AnyPS5 {state.Record.Version} is installed (built from source)")
                : "The AnyPS5 build is incomplete";
            if (state.Status != AnyPS5InstallStatus.Installed)
            {
                StateDetail = "Resume it to finish: the steps already done are skipped.";
                ActionText = "Resume the install";
            }
        }
```

- update the class summary if it mentions the command line (it does not today), and remove `using` lines the build reports as unused.

- [ ] **Step 9: Run all tests and the build**

Run: `dotnet build PS5Kitsune.slnx -c Release`, then `dotnet test --solution PS5Kitsune.slnx -c Release`.
Expected: 0 warnings; all tests pass except the explicit one.

- [ ] **Step 10: Commit**

```bash
git add src tests
git commit -m "Choose the mode before the PC check and repair builds from Home

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Build progress and failure details on Install

**Files:**
- Modify: `src/PS5Kitsune.App/ViewModels/InstallViewModel.cs`
- Modify: `src/PS5Kitsune.App/Views/InstallView.axaml`
- Test: `tests/PS5Kitsune.App.Tests/InstallViewModelTests.cs`

**Interfaces:**
- Consumes: `StepProgress.Fraction` (5a), `BuildCommandException.Summary`/`Details` (Task 2).
- Produces: `InstallViewModel` properties `ShowsProgress`, `ProgressPercent`, `ProgressIndeterminate`, `ProgressText` (replacing `IsDownloading`, `DownloadPercent`, `DownloadIndeterminate`, `DownloadText`), and `FailureDetails` (string?, null when there are none).

- [ ] **Step 1: Write the failing tests**

In `InstallViewModelTests`, add a build-mode factory next to `Create()`:

```csharp
    private InstallViewModel CreateBuild() =>
        new(_installer, AnyPS5Mode.Build, "C:/root/logs/app.log", _platform, new InlineDispatcher(), _navigator, new FakePages(), _busy.Add);
```

Rename the properties in `Download_progress_drives_the_bar_and_messages_go_to_the_log`: `IsDownloading` → `ShowsProgress`, `DownloadIndeterminate` → `ProgressIndeterminate`, `DownloadPercent` → `ProgressPercent`, and add after the first `ProgressPercent` assert:

```csharp
        Assert.Equal("0.0 / 0.0 MB", viewModel.ProgressText);
```

(50 and 200 bytes round to 0.0 MB.)

Update the existing navigation asserts that use `FakePages` strings if any reads `"install"` (none should after Task 3).

Add:

```csharp
    [Fact]
    public async Task A_build_counter_drives_the_bar()
    {
        IProgress<StepProgress>? reported = null;
        Action<StepEvent>? events = null;
        var release = new TaskCompletionSource();
        _installer.Install = (progress, onEvent, _) =>
        {
            reported = progress;
            events = onEvent;
            return release.Task;
        };
        var viewModel = CreateBuild();
        var running = viewModel.RunAsync();

        events!(new StepEvent(StepEventKind.Started, "Compile AnyPS5", 2, 2));
        Assert.True(viewModel.ShowsProgress);
        Assert.True(viewModel.ProgressIndeterminate);
        Assert.Equal("Compile AnyPS5", viewModel.ProgressText);

        reported!.Report(new StepProgress(Fraction: new StepFraction(91, 364)));
        Assert.False(viewModel.ProgressIndeterminate);
        Assert.Equal(25, viewModel.ProgressPercent);
        Assert.Equal("91 / 364 units", viewModel.ProgressText);

        events(new StepEvent(StepEventKind.Completed, "Compile AnyPS5", 2, 2, TimeSpan.FromSeconds(1)));
        Assert.False(viewModel.ShowsProgress);

        release.SetResult();
        await running;
    }

    [Fact]
    public async Task A_release_step_without_a_download_shows_no_bar()
    {
        Action<StepEvent>? events = null;
        var release = new TaskCompletionSource();
        _installer.Install = (_, onEvent, _) =>
        {
            events = onEvent;
            return release.Task;
        };
        var viewModel = Create();
        var running = viewModel.RunAsync();

        events!(new StepEvent(StepEventKind.Started, "Download the relinker", 2, 2));

        Assert.False(viewModel.ShowsProgress);
        release.SetResult();
        await running;
    }

    [Fact]
    public async Task A_build_failure_shows_its_summary_and_details()
    {
        _installer.Install = (_, _, _) => throw new StepFailedException(
            "Compile AnyPS5",
            new BuildCommandException("The build failed. The log holds the full compiler output.", "cmake exited with code 1.\nmain.cpp:1: error: boom"));
        var viewModel = CreateBuild();

        await viewModel.RunAsync();

        Assert.Equal("Compile AnyPS5 failed: The build failed. The log holds the full compiler output.", viewModel.Failure);
        Assert.Equal("cmake exited with code 1.\nmain.cpp:1: error: boom", viewModel.FailureDetails);
    }

    [Fact]
    public async Task Another_failure_has_no_details()
    {
        _installer.Install = (_, _, _) => throw new StepFailedException("Download the relinker", new IOException("disk full"));
        var viewModel = Create();

        await viewModel.RunAsync();

        Assert.Equal("Download the relinker failed: disk full", viewModel.Failure);
        Assert.Null(viewModel.FailureDetails);
    }

    [Fact]
    public async Task Retry_clears_the_details()
    {
        _installer.Install = (_, _, _) => throw new StepFailedException("Compile AnyPS5", new BuildCommandException("The build failed.", "details"));
        var viewModel = CreateBuild();
        await viewModel.RunAsync();

        _installer.Install = (_, _, _) => Task.CompletedTask;
        await viewModel.RetryCommand.ExecuteAsync(null);

        Assert.Null(viewModel.FailureDetails);
    }
```

Add `using PS5Kitsune.Core.AnyPS5.Build;` for `BuildCommandException`.

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.App.Tests.InstallViewModelTests"`
Expected: build errors (`ShowsProgress`, `FailureDetails`).

- [ ] **Step 3: Generalize the progress area**

In `InstallViewModel`:

Replace the four download properties with:

```csharp
    /// <summary>A download, a build counter, or a running build step with neither yet.</summary>
    [ObservableProperty]
    public partial bool ShowsProgress { get; private set; }

    [ObservableProperty]
    public partial double ProgressPercent { get; private set; }

    /// <summary>True when a download has no length, or a build step reports no counter yet.</summary>
    [ObservableProperty]
    public partial bool ProgressIndeterminate { get; private set; }

    [ObservableProperty]
    public partial string ProgressText { get; private set; } = "";

    /// <summary>The exit code and last output lines of a failed build command; null otherwise.</summary>
    [ObservableProperty]
    public partial string? FailureDetails { get; private set; }
```

In `RunCoreAsync`: after `Failure = null;` add `FailureDetails = null;`; replace the `StepFailedException` catch with:

```csharp
        catch (StepFailedException exception)
        {
            var build = exception.InnerException as BuildCommandException;
            var message = $"{exception.StepName} failed: {build?.Summary ?? exception.InnerException?.Message ?? exception.Message}";
            var details = string.IsNullOrEmpty(build?.Details) ? null : build.Details;
            _ui.Post(() => Fail(message, details));
        }
```

and the generic catch with `_ui.Post(() => Fail($"The install failed: {exception.Message}", null));`; in `finally`, `IsDownloading = false;` becomes `ShowsProgress = false;`.

`Fail`:

```csharp
    private void Fail(string message, string? details)
    {
        HasFailed = true;
        Failure = message;
        FailureDetails = details;
    }
```

In `Apply(StepEvent)`, replace the final `if (stepEvent.Kind != StepEventKind.Started) { IsDownloading = false; }` with:

```csharp
        if (stepEvent.Kind == StepEventKind.Started && _mode == AnyPS5Mode.Build)
        {
            // Git, CMake and extraction report nothing measurable for a while: show that the step is working.
            ShowsProgress = true;
            ProgressIndeterminate = true;
            ProgressPercent = 0;
            ProgressText = stepEvent.StepName;
        }
        else if (stepEvent.Kind != StepEventKind.Started)
        {
            ShowsProgress = false;
        }
```

In `Apply(StepProgress)`, rename the download block's properties and add the fraction:

```csharp
        if (progress.Download is { } download)
        {
            var total = download.TotalBytes ?? 0;
            ShowsProgress = true;
            ProgressIndeterminate = total <= 0;
            ProgressPercent = total > 0 ? download.ReceivedBytes * 100.0 / total : 0;
            ProgressText = total > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{download.ReceivedBytes / 1048576.0:0.0} / {total / 1048576.0:0.0} MB")
                : string.Create(CultureInfo.InvariantCulture, $"{download.ReceivedBytes / 1048576.0:0.0} MB");
        }

        if (progress.Fraction is { } fraction)
        {
            ShowsProgress = true;
            ProgressIndeterminate = false;
            ProgressPercent = fraction.Done * 100.0 / fraction.Total;
            ProgressText = string.Create(CultureInfo.InvariantCulture, $"{fraction.Done} / {fraction.Total} units");
        }
```

Add `using PS5Kitsune.Core.AnyPS5.Build;`. Update the class summary: `/// Runs the install when the page is shown: step list, a progress bar for downloads and build counters, a log built from the installer's events.`

- [ ] **Step 4: Update the view**

In `InstallView.axaml`, replace the download `StackPanel` and the failure `TextBlock` with:

```xml
      <StackPanel IsVisible="{Binding ShowsProgress}" Spacing="4">
        <ProgressBar Minimum="0" Maximum="100" Value="{Binding ProgressPercent}" IsIndeterminate="{Binding ProgressIndeterminate}" />
        <TextBlock Text="{Binding ProgressText}" Opacity="0.7" FontSize="12" />
      </StackPanel>
      <TextBlock Text="{Binding Failure}" IsVisible="{Binding HasFailed}" Foreground="#C42B1C" FontWeight="SemiBold" TextWrapping="Wrap" />
      <Expander Header="Show details" HorizontalAlignment="Stretch"
                IsVisible="{Binding FailureDetails, Converter={x:Static ObjectConverters.IsNotNull}}">
        <ScrollViewer MaxHeight="220">
          <SelectableTextBlock Text="{Binding FailureDetails}" FontFamily="Cascadia Mono,Consolas,monospace" FontSize="12" TextWrapping="Wrap" />
        </ScrollViewer>
      </Expander>
```

- [ ] **Step 5: Run all tests and the build**

Run: `dotnet build PS5Kitsune.slnx -c Release`, then `dotnet test --solution PS5Kitsune.slnx -c Release`.
Expected: 0 warnings; all tests pass except the explicit one.

- [ ] **Step 6: Commit**

```bash
git add src/PS5Kitsune.App tests/PS5Kitsune.App.Tests
git commit -m "Show build progress and failure details on the Install page

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: The build line on Done, and the docs

**Files:**
- Modify: `src/PS5Kitsune.App/ViewModels/DoneViewModel.cs`, `src/PS5Kitsune.App/Views/DoneView.axaml`
- Test: `tests/PS5Kitsune.App.Tests/DoneViewModelTests.cs`
- Modify: `docs/architecture.md`, `README.md`, `docs/superpowers/specs/2026-10-22-milestone-5b-build-ui-design.md`

**Interfaces:**
- Produces: `DoneViewModel.BuildText` (string?, null for a release record or no record).

- [ ] **Step 1: Write the failing Done tests**

In `DoneViewModelTests`, add:

```csharp
    [Fact]
    public void A_build_names_its_commit()
    {
        var build = new AnyPS5InstallRecord(2, "test-profile", AnyPS5Mode.Build, "3fd1cc3", "3fd1cc393b8512c2e989860053f30dee5a8cd1aa", null, DateTimeOffset.UnixEpoch);

        var viewModel = Create(build);

        Assert.Equal("AnyPS5 3fd1cc3 is installed", viewModel.Title);
        Assert.Equal("Built from source, commit 3fd1cc3.", viewModel.BuildText);
    }

    [Fact]
    public void A_release_has_no_build_line()
    {
        Assert.Null(Create().BuildText);
    }
```

Run: `dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.App.Tests.DoneViewModelTests"`
Expected: build error, no `BuildText`.

- [ ] **Step 2: Add the line**

In `DoneViewModel`, after `Title`:

```csharp
    public string? BuildText => record is { Mode: AnyPS5Mode.Build, Commit: { Length: >= 7 } commit } ? $"Built from source, commit {commit[..7]}." : null;
```

In `DoneView.axaml`, after the title `TextBlock`:

```xml
      <TextBlock Text="{Binding BuildText}" IsVisible="{Binding BuildText, Converter={x:Static ObjectConverters.IsNotNull}}" Opacity="0.85" />
```

Run the Done tests again. Expected: PASS.

- [ ] **Step 3: Update `docs/architecture.md`**

- Replace the bullet starting `- The app shows a build install on Home ("built from source") but repairs or updates it only from the command line` (line 199) with:
  `- The app shows a build install on Home ("built from source") and repairs, resumes or updates it like a release: Home opens Mode, which preselects build.`
- Replace the "Screens" list of the V1 section (items 1 to 6 under the first `### Screens`) and the paragraph after it (`Milestone 4 implements Home, PC check, Mode (build shown as coming later), Install and Done. The Tools screen comes with build mode.`) with:

```markdown
1. **Home**: current state (nothing installed, or installed version + commit, active profile, new validated version available). Its action opens Mode.
2. **Mode**: release or build, with date, commit, profile status, estimated duration and disk space of each option. It preselects the mode of an unfinished install, else the installed mode, else release. Build is disabled when the active profile has no build.
3. **PC check**: list of checks for the chosen mode (build mode needs `buildMinFreeDiskGiB` free), with status badge and action button.
4. **Tools** (build mode only): Python, MinGit and WinLibs, each installed or missing with its download size (the profile's `size`), the total to download and the disk space a build takes. Its button opens Install, whose first steps download what is missing.
5. **Install**: steps ticked as they complete, a progress bar (download bytes, Ninja's `[n/m]` counter, or indeterminate while a build step reports nothing), scrolling log, Cancel button. A failed build command shows its targeted message, with the exit code and last output lines under "Show details".
6. **Done**: relinker and libs paths, the commit for a build, "Open log" button, continue to V2.

Mode comes before the PC check because the check depends on the mode. The mode is passed from page to page, never stored in the session. Milestone 4 implemented Home, PC check, Mode, Install and Done for release mode; milestone 5b added build mode and the Tools screen.
```

- In the roadmap, replace `   - **5b — Build mode UI.** The Build card on Mode, the Tools screen, and the build progress on Install.` with:
  `   - **5b — Build mode UI.** The Build card on Mode, the Tools screen, and the build progress on Install. Spec: [milestone 5b](superpowers/specs/2026-10-22-milestone-5b-build-ui-design.md).`
- In the "Tool output" bullet of the build section, append: `A sub-build that runs its own Ninja prints a smaller counter between the main one's lines: each command follows the counter with the largest total, so the progress never jumps back.`

- [ ] **Step 4: Update `README.md`**

- Line 19: replace `Early development — milestone 4 of 8 (install screens). The app installs AnyPS5 (release mode) from its screens; adding games still goes through the command line.` with `Early development — milestone 5 of 8 (build mode). The app installs AnyPS5 from its screens, as a release or built from source; adding games still goes through the command line.`
- Line 49: replace the last two sentences (`The app's Install page installs in release mode until the build UI arrives. The app's Home page shows a build install, but repairs or updates it only from the command line (\`ps5kitsune install\`) until then.`) with: `In the app, choose "Build from source" on the Mode screen: the PC check then uses the build threshold, and a Tools screen shows what will be downloaded before the build starts.`

- [ ] **Step 5: Record the plan's refinements in the spec**

Append to `docs/superpowers/specs/2026-10-22-milestone-5b-build-ui-design.md`:

```markdown
## Refinements from the implementation plan

1. `FractionTracker` lives in `Core.Pipeline` but is created per command inside `BuildCommands.RunAsync`, so it needs no `Reset()` and the console's 10 % printing gets the fix without change.
2. Mode asks the installer, through `IAnyPS5Installer.Supports(mode)`, whether build is available: build also needs the MinGit and WinLibs tools, not only the `build` section.
3. In build mode every running step shows the indeterminate bar until a download or a counter replaces it, tool steps included. Release mode shows the bar for downloads only, as before.
4. The Tools total reads "To download: size unknown" when every missing tool lacks a size.
```

- [ ] **Step 6: Run all tests and the build**

Run: `dotnet build PS5Kitsune.slnx -c Release`, then `dotnet test --solution PS5Kitsune.slnx -c Release`.
Expected: 0 warnings; all tests pass except the explicit one.

- [ ] **Step 7: Commit**

```bash
git add src/PS5Kitsune.App tests/PS5Kitsune.App.Tests docs README.md
git commit -m "Name the commit on Done and document the build mode UI

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## After the tasks

The user runs the spec's criterion (4 points) on the AMD laptop with the Release build of the app. Record the result in the spec under "Criterion result", then open the PR with the pr-description skill when the user asks.
