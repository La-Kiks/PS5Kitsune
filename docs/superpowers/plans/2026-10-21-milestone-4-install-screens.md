# Milestone 4 — Install screens, release mode: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Install AnyPS5 from the app (Home → PC check → Mode → Install → Done) on a data root the user can choose, with the same result as `ps5kitsune install`.

**Architecture:** Core gains an install state (`IAnyPS5Installer.GetState()`), a `Cancelled` step event, and an app settings file that holds the chosen data root and is read by both the app and the CLI. The app gets a small hand-written navigator with a back stack. A `RootSession` (one `ServiceProvider` per data root) is replaced when the root changes. There is one view model per page, and a `PageFactory` builds them from the current session.

**Tech Stack:** .NET 10, C#, Avalonia 12 + CommunityToolkit.Mvvm 8.4, Microsoft.Extensions.DependencyInjection, NLog, xUnit v3 (Microsoft Testing Platform).

**Spec:** [docs/superpowers/specs/2026-10-21-milestone-4-install-screens-design.md](../specs/2026-10-21-milestone-4-install-screens-design.md). Task 1 amends it with the refinements below.

## Global Constraints

- License: every shipped package must be GPLv2-compatible (MIT, BSD, LGPL; never Apache-2.0). No new package is needed by this plan.
- Language: code, comments, UI text, docs and commits in English.
- Commits authored as `La-Kiks` (never the gmail address), ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Commit locally; **do not push**.
- Build settings (`Directory.Build.props`): `TreatWarningsAsErrors`, `AnalysisLevel latest-recommended`, `EnforceCodeStyleInBuild`, nullable on. Analyzer warnings fail the build: fix them, never suppress them globally. Format numbers and dates with `CultureInfo.InvariantCulture` (CA1305).
- Core stays free of Windows APIs and of Avalonia (the Ubuntu CI job builds and tests every project, App.Tests included).
- View models never reference Avalonia types: dialogs go through `IDialogService` and UI-thread posting through `IUiDispatcher`.
- Settings file: `<IPlatform.SettingsDirectory>\settings.json`, `%LOCALAPPDATA%\PS5Kitsune` on Windows. Root resolution: `--root`, then the setting, then `IPlatform.DefaultDataRoot`.
- `v0.1.2` was published `2026-10-14T08:56:52Z` (GitHub API `published_at`).
- Run commands through the Bash tool (RTK). Build: `dotnet build PS5Kitsune.slnx -c Release`. Tests: `dotnet test --solution PS5Kitsune.slnx -c Release` (filter one class with `-- --filter-class "<full class name>"`).
- Never run `install` against a test root while the build is failing: a stale exe really installs into it.

## Refinements to the spec (applied by Task 1)

1. **`IAnyPS5Installer`** (Core) is extracted from `AnyPS5Installer` (`CreateSteps`, `GetState`, `InstallAsync`) so the app's view models can be tested with a fake. `IsInstalled()` keeps its current implementation (same answer as `GetState().Status == Installed`, and it stops at the first step that is not done).
2. **No app-level container:** `App.axaml.cs` is a plain composition root (platform, profile, `AppSettingsStore`, `SessionHost`, `Navigator`, `PageFactory`, dialogs). Only `RootSession` builds a `ServiceProvider`.
3. **`IPlatform.SettingsDirectory` already exists**, unused, and points at Roaming `ApplicationData`. It moves to `LocalApplicationData`, as the spec says.
4. **"Back" returns to the previous page** through the navigator's back stack. The PC check only runs by itself the first time its page is entered (or after a cancelled check), so going back from Mode does not run the probe again.
5. **Root helpers** live in a static `DataRoots` class in Core: `Resolve(cliRoot, settings, defaultRoot)` and `Prepare(path)` (create it and check it is writable). They are used by the CLI and by Home.
6. **`AppSettingsStore.Load()`** returns the settings plus an optional problem text. The host logs that text once logging exists, because the settings decide where the log goes.

## File map

| File | Responsibility |
| --- | --- |
| `src/PS5Kitsune.Core/Profiles/AnyPS5Profile.cs` (modify) | `ProfileSource.PublishedAt` |
| `src/PS5Kitsune.Core/Profiles/default-profile.json` (modify) | `publishedAt` for v0.1.2 |
| `src/PS5Kitsune.Core/Pipeline/StepEvent.cs` (modify) | `StepEventKind.Cancelled` |
| `src/PS5Kitsune.Core/Pipeline/StepRunner.cs` (modify) | Done-check inside the try; `Cancelled` event |
| `src/PS5Kitsune.Core/AnyPS5/IAnyPS5Installer.cs` (create) | Interface used by the app |
| `src/PS5Kitsune.Core/AnyPS5/AnyPS5InstallState.cs` (create) | `AnyPS5InstallStatus`, `AnyPS5InstallState` |
| `src/PS5Kitsune.Core/AnyPS5/AnyPS5Installer.cs` (modify) | Implements the interface, `GetState()` |
| `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs` (modify) | Registers `IAnyPS5Installer` |
| `src/PS5Kitsune.Core/Settings/AppSettings.cs` (create) | `AppSettings`, `AppSettingsLoad` |
| `src/PS5Kitsune.Core/Settings/AppSettingsStore.cs` (create) | Read/write `settings.json` |
| `src/PS5Kitsune.Core/Settings/DataRoots.cs` (create) | Root resolution and preparation |
| `src/PS5Kitsune.Core/Abstractions/IPlatform.cs` (modify) | `OpenPath` |
| `src/PS5Kitsune.Platform.Windows/WindowsPlatform.cs` (modify) | `SettingsDirectory` in LocalAppData, `OpenPath` |
| `src/PS5Kitsune.Cli/CommandHost.cs`, `CliLog.cs`, `Program.cs` (modify) | Root from settings |
| `src/PS5Kitsune.App/Navigation/*.cs` (create) | `Navigator`, `INavigationAware`, `IPages`, `IUiDispatcher`, `IDialogService` |
| `src/PS5Kitsune.App/Sessions/RootSession.cs`, `SessionHost.cs` (create) | One service provider per data root |
| `src/PS5Kitsune.App/ViewModels/*.cs` | One view model per page, plus `InstallStepViewModel`, `PathOpener` |
| `src/PS5Kitsune.App/Views/*.axaml` | One view per page; `ConfirmWindow` |
| `src/PS5Kitsune.App/Shell/*.cs` (create) | `PageFactory`, `AvaloniaDialogService`, `AvaloniaUiDispatcher`, `AppLog` |
| `src/PS5Kitsune.App/App.axaml.cs`, `Views/MainWindow.axaml(.cs)` (modify) | Composition root, page templates, close guard |
| `tests/PS5Kitsune.App.Tests/Fakes.cs` (create) | Fakes shared by the app tests |
| `docs/architecture.md` (modify) | Settings file, milestone 4 result |

---

### Task 1: Spec refinements and the release date in the profile

**Files:**
- Modify: `docs/superpowers/specs/2026-10-21-milestone-4-install-screens-design.md`
- Modify: `src/PS5Kitsune.Core/Profiles/AnyPS5Profile.cs`
- Modify: `src/PS5Kitsune.Core/Profiles/default-profile.json`
- Test: `tests/PS5Kitsune.Core.Tests/Profiles/ProfileLoaderTests.cs`

**Interfaces:**
- Produces: `ProfileSource(string Kind, string? Tag = null, string? Sha = null, DateTimeOffset? PublishedAt = null)`.

- [ ] **Step 1: Amend the spec**

Append this section at the end of the spec:

```markdown
## Refinements from the implementation plan

1. `IAnyPS5Installer` (Core) is extracted from `AnyPS5Installer` (`CreateSteps`, `GetState`, `InstallAsync`) so the app's view models can be tested with a fake. `IsInstalled()` keeps its implementation, which gives the same answer.
2. No app-level container: `App.axaml.cs` is a plain composition root. Only `RootSession` builds a `ServiceProvider`.
3. `IPlatform.SettingsDirectory` already existed, unused, under Roaming `ApplicationData`; it moves to `LocalApplicationData`.
4. "Back" returns to the previous page through the navigator's back stack. The PC check runs by itself only the first time its page is entered, or after a cancelled check.
5. Root helpers live in a static `DataRoots` class in Core: `Resolve` and `Prepare`.
6. `AppSettingsStore.Load()` returns the settings and an optional problem text, which the host logs once logging exists.
```

- [ ] **Step 2: Write the failing tests**

Add to `ProfileLoaderTests`:

```csharp
    [Fact]
    public void Embedded_profile_has_the_release_date()
    {
        var profile = ProfileLoader.LoadEmbedded();

        Assert.Equal(new DateTimeOffset(2026, 10, 14, 8, 56, 52, TimeSpan.Zero), profile.Source.PublishedAt);
    }

    [Fact]
    public void The_release_date_is_optional()
    {
        var profile = Load("""
            { "schemaVersion": 1, "id": "no-date", "source": { "kind": "release", "tag": "v1" } }
            """);

        Assert.Null(profile.Source.PublishedAt);
    }
```

(`Load(string)` is the existing helper at the bottom of the class.)

- [ ] **Step 3: Run them to see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build error, `ProfileSource` has no `PublishedAt`.

- [ ] **Step 4: Implement**

In `AnyPS5Profile.cs`, replace the `ProfileSource` record:

```csharp
/// <param name="Kind">"release" (uses <paramref name="Tag"/>) or "commit" (uses <paramref name="Sha"/>).</param>
/// <param name="PublishedAt">When the release was published, shown on the Mode screen; null when unknown.</param>
public sealed record ProfileSource(string Kind, string? Tag = null, string? Sha = null, DateTimeOffset? PublishedAt = null)
{
    /// <summary>The tag of a release, the SHA of a commit.</summary>
    public string Version => Tag ?? Sha ?? "unknown";
}
```

In `default-profile.json`, replace the `source` line:

```json
  "source": { "kind": "release", "tag": "v0.1.2", "publishedAt": "2026-10-14T08:56:52Z" },
```

- [ ] **Step 5: Run the tests**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.Core.Tests.Profiles.ProfileLoaderTests"`
Expected: all pass.

- [ ] **Step 6: Commit**

```bash
git add docs/superpowers/specs/2026-10-21-milestone-4-install-screens-design.md src/PS5Kitsune.Core/Profiles tests/PS5Kitsune.Core.Tests/Profiles
git commit -m "Add the release date to the profile and refine the milestone 4 spec

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: StepRunner reports done-check failures and cancellation

**Files:**
- Modify: `src/PS5Kitsune.Core/Pipeline/StepEvent.cs`
- Modify: `src/PS5Kitsune.Core/Pipeline/StepRunner.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Pipeline/StepRunnerTests.cs`

**Interfaces:**
- Produces: `StepEventKind.Cancelled`. It is emitted with the running step's name, number and elapsed `Duration`, then `OperationCanceledException` is rethrown. A done-check that throws produces a `Failed` event and a `StepFailedException`.

- [ ] **Step 1: Write the failing tests**

In `StepRunnerTests`, give `FakeStep` a done-check failure. Replace its `IsAlreadyDone` line and add the property:

```csharp
        public Exception? DoneFailure { get; init; }

        public bool IsAlreadyDone() => DoneFailure is null ? Done : throw DoneFailure;
```

Add the tests:

```csharp
    [Fact]
    public async Task A_failing_done_check_is_reported_as_the_step_failure()
    {
        var steps = new IStep[]
        {
            new FakeStep("A", _executed) { DoneFailure = new UnauthorizedAccessException("access denied") },
            new FakeStep("B", _executed),
        };

        var exception = await Assert.ThrowsAsync<StepFailedException>(() =>
            _runner.RunAsync(steps, onEvent: _events.Add, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("A", exception.StepName);
        Assert.IsType<UnauthorizedAccessException>(exception.InnerException);
        Assert.Empty(_executed);
        Assert.Equal([(StepEventKind.Failed, "A")], _events.Select(e => (e.Kind, e.StepName)));
    }

    [Fact]
    public async Task Cancelling_during_a_step_emits_Cancelled_for_it()
    {
        using var cancellation = new CancellationTokenSource();
        var a = new FakeStep("A", _executed) { OnExecute = cancellation.Cancel, Failure = new OperationCanceledException(cancellation.Token) };

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _runner.RunAsync([a, new FakeStep("B", _executed)], onEvent: _events.Add, cancellationToken: cancellation.Token));

        Assert.Equal([(StepEventKind.Started, "A"), (StepEventKind.Cancelled, "A")], _events.Select(e => (e.Kind, e.StepName)));
        Assert.NotNull(_events[^1].Duration);
    }

    [Fact]
    public async Task Cancelling_before_the_first_step_emits_nothing()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _runner.RunAsync([new FakeStep("A", _executed)], onEvent: _events.Add, cancellationToken: cancellation.Token));

        Assert.Empty(_events);
        Assert.Empty(_executed);
    }
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build error, `StepEventKind` has no `Cancelled`.

- [ ] **Step 3: Implement**

In `StepEvent.cs`, add `Cancelled` after `Failed` and document `Duration`:

```csharp
public enum StepEventKind
{
    Started,
    Skipped,
    Completed,
    Failed,
    Cancelled,
}

/// <param name="Number">Position of the step, from 1.</param>
/// <param name="Count">Number of steps in the run.</param>
/// <param name="Duration">Set for Completed, Failed and Cancelled.</param>
/// <param name="Error">Set for Failed.</param>
public sealed record StepEvent(StepEventKind Kind, string StepName, int Number, int Count, TimeSpan? Duration = null, Exception? Error = null);
```

In `StepRunner.cs`, replace the body of the `for` loop and add a log message:

```csharp
        for (var index = 0; index < steps.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = steps[index];
            var number = index + 1;
            var stopwatch = Stopwatch.StartNew();
            try
            {
                // Inside the try: a done-check reads files, and an exception there is this step's failure.
                if (step.IsAlreadyDone())
                {
                    LogSkipped(number, steps.Count, step.Name);
                    onEvent?.Invoke(new StepEvent(StepEventKind.Skipped, step.Name, number, steps.Count));
                    continue;
                }

                LogStarted(number, steps.Count, step.Name);
                onEvent?.Invoke(new StepEvent(StepEventKind.Started, step.Name, number, steps.Count));
                stopwatch.Restart();
                await step.ExecuteAsync(progress, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                LogCancelled(number, steps.Count, step.Name, stopwatch.Elapsed.TotalSeconds);
                onEvent?.Invoke(new StepEvent(StepEventKind.Cancelled, step.Name, number, steps.Count, stopwatch.Elapsed));
                throw;
            }
            catch (Exception exception)
            {
                LogFailed(exception, number, steps.Count, step.Name, stopwatch.Elapsed.TotalSeconds);
                onEvent?.Invoke(new StepEvent(StepEventKind.Failed, step.Name, number, steps.Count, stopwatch.Elapsed, exception));
                throw new StepFailedException(step.Name, exception);
            }

            LogCompleted(number, steps.Count, step.Name, stopwatch.Elapsed.TotalSeconds);
            onEvent?.Invoke(new StepEvent(StepEventKind.Completed, step.Name, number, steps.Count, stopwatch.Elapsed));
        }
```

```csharp
    [LoggerMessage(Level = LogLevel.Warning, Message = "[{Number}/{Count}] {Step}: cancelled after {Seconds:0.0} s")]
    private partial void LogCancelled(int number, int count, string step, double seconds);
```

Update the class summary's first sentence to: `Runs steps in order, skips those already done, and stops at the first failure or at cancellation.`

- [ ] **Step 4: Run the tests**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.Core.Tests.Pipeline.StepRunnerTests"`
Expected: all pass, including the existing `Cancellation_is_not_wrapped`.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Core/Pipeline tests/PS5Kitsune.Core.Tests/Pipeline
git commit -m "Report done-check failures and cancellation as step events

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Install state and the installer interface

**Files:**
- Create: `src/PS5Kitsune.Core/AnyPS5/AnyPS5InstallState.cs`
- Create: `src/PS5Kitsune.Core/AnyPS5/IAnyPS5Installer.cs`
- Modify: `src/PS5Kitsune.Core/AnyPS5/AnyPS5Installer.cs`
- Modify: `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`
- Test: `tests/PS5Kitsune.Core.Tests/AnyPS5/AnyPS5InstallerTests.cs`

**Interfaces:**
- Consumes: `AnyPS5InstallRecord` (existing), `JsonFiles.Read<T>` (internal, Core).
- Produces:

```csharp
public enum AnyPS5InstallStatus { NotInstalled, Installed, Incomplete }
public sealed record AnyPS5InstallState(AnyPS5InstallStatus Status, AnyPS5InstallRecord? Record);
public interface IAnyPS5Installer
{
    IReadOnlyList<IStep> CreateSteps();
    AnyPS5InstallState GetState();
    Task InstallAsync(IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default);
}
```

`IAnyPS5Installer` is registered as the same singleton as `AnyPS5Installer`.

- [ ] **Step 1: Write the failing tests**

Add to `AnyPS5InstallerTests`:

```csharp
    [Fact]
    public void An_empty_root_is_not_installed()
    {
        var state = _setup.Installer.GetState();

        Assert.Equal(AnyPS5InstallStatus.NotInstalled, state.Status);
        Assert.Null(state.Record);
    }

    [Fact]
    public async Task A_complete_install_is_installed_with_its_record()
    {
        await _setup.InstallAsync();

        var state = _setup.Installer.GetState();

        Assert.Equal(AnyPS5InstallStatus.Installed, state.Status);
        Assert.Equal("v0.1.2", state.Record?.Version);
        Assert.Equal("test-profile", state.Record?.Profile);
    }

    [Fact]
    public async Task A_missing_file_makes_the_install_incomplete()
    {
        await _setup.InstallAsync();
        File.Delete(_setup.Paths.Relinker);

        Assert.Equal(AnyPS5InstallStatus.Incomplete, _setup.Installer.GetState().Status);
    }

    [Fact]
    public async Task A_record_for_another_tag_makes_the_install_incomplete()
    {
        await _setup.InstallAsync();
        var record = JsonFiles.Read<AnyPS5InstallRecord>(_setup.Paths.RecordFile)!;
        await JsonFiles.WriteAsync(_setup.Paths.RecordFile, record with { Version = "v0.1.1" }, TestContext.Current.CancellationToken);

        var state = _setup.Installer.GetState();

        Assert.Equal(AnyPS5InstallStatus.Incomplete, state.Status);
        Assert.Equal("v0.1.1", state.Record?.Version);
    }

    [Fact]
    public async Task An_unreadable_record_is_ignored()
    {
        await _setup.InstallAsync();
        await File.WriteAllTextAsync(_setup.Paths.RecordFile, "{ not json", TestContext.Current.CancellationToken);

        var state = _setup.Installer.GetState();

        Assert.Equal(AnyPS5InstallStatus.Incomplete, state.Status);
        Assert.Null(state.Record);
    }
```

(Core.Tests already sees `JsonFiles`: the existing test `Install_writes_the_install_record` uses it.)

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build error, `GetState` and `AnyPS5InstallStatus` do not exist.

- [ ] **Step 3: Implement**

`src/PS5Kitsune.Core/AnyPS5/AnyPS5InstallState.cs`:

```csharp
namespace PS5Kitsune.Core.AnyPS5;

public enum AnyPS5InstallStatus
{
    /// <summary>No step done and no install.json.</summary>
    NotInstalled,

    /// <summary>Every step done: the files have the profile's hashes and install.json describes this profile.</summary>
    Installed,

    /// <summary>Anything in between, including an install.json for another profile or tag.</summary>
    Incomplete,
}

/// <param name="Record">install.json as found, even when it describes another install; null when missing or unreadable.</param>
public sealed record AnyPS5InstallState(AnyPS5InstallStatus Status, AnyPS5InstallRecord? Record);
```

`src/PS5Kitsune.Core/AnyPS5/IAnyPS5Installer.cs`:

```csharp
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.AnyPS5;

/// <summary>Installs AnyPS5 into anyps5/current. The app's screens use it through this interface.</summary>
public interface IAnyPS5Installer
{
    /// <summary>The install steps, in order; their names are shown before the install starts.</summary>
    IReadOnlyList<IStep> CreateSteps();

    /// <summary>Where the install stands, from the steps' done-checks and install.json.</summary>
    AnyPS5InstallState GetState();

    /// <exception cref="StepFailedException">A step failed.</exception>
    /// <exception cref="OperationCanceledException">The install was cancelled; it can be resumed.</exception>
    Task InstallAsync(IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default);
}
```

In `AnyPS5Installer.cs`: add `using System.Text.Json;`, change the declaration to `public sealed class AnyPS5Installer(...) : IAnyPS5Installer`, and add after `IsInstalled()`:

```csharp
    public AnyPS5InstallState GetState()
    {
        var steps = CreateSteps();
        var done = steps.Count(IsDone);
        var record = ReadRecord();
        var status = done == steps.Count ? AnyPS5InstallStatus.Installed
            : done == 0 && record is null ? AnyPS5InstallStatus.NotInstalled
            : AnyPS5InstallStatus.Incomplete;
        return new AnyPS5InstallState(status, record);
    }

    /// <summary>A done-check that throws (a locked or unreadable file) counts as not done; the install reports the error.</summary>
    private static bool IsDone(IStep step)
    {
        try
        {
            return step.IsAlreadyDone();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private AnyPS5InstallRecord? ReadRecord()
    {
        try
        {
            return JsonFiles.Read<AnyPS5InstallRecord>(paths.RecordFile);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
```

In `ServiceCollectionExtensions.cs`, after `services.AddSingleton<AnyPS5Installer>();`:

```csharp
        services.AddSingleton<IAnyPS5Installer>(provider => provider.GetRequiredService<AnyPS5Installer>());
```

- [ ] **Step 4: Run the tests**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.Core.Tests.AnyPS5.AnyPS5InstallerTests"`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Core/AnyPS5 src/PS5Kitsune.Core/ServiceCollectionExtensions.cs tests/PS5Kitsune.Core.Tests/AnyPS5
git commit -m "Report whether AnyPS5 is installed, missing or incomplete

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Settings file, data root, platform and CLI

**Files:**
- Create: `src/PS5Kitsune.Core/Settings/AppSettings.cs`
- Create: `src/PS5Kitsune.Core/Settings/AppSettingsStore.cs`
- Create: `src/PS5Kitsune.Core/Settings/DataRoots.cs`
- Modify: `src/PS5Kitsune.Core/Abstractions/IPlatform.cs`
- Modify: `src/PS5Kitsune.Platform.Windows/WindowsPlatform.cs`
- Modify: `tests/PS5Kitsune.Core.Tests/Fakes/FakePlatform.cs`, `tests/PS5Kitsune.App.Tests/SystemCheckViewModelTests.cs` (the `RecordingPlatform` fake: add `OpenPath`)
- Modify: `src/PS5Kitsune.Cli/CommandHost.cs`, `src/PS5Kitsune.Cli/CliLog.cs`, `src/PS5Kitsune.Cli/Program.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Settings/AppSettingsStoreTests.cs`, `tests/PS5Kitsune.Core.Tests/Settings/DataRootsTests.cs`, `tests/PS5Kitsune.Platform.Windows.Tests/WindowsPlatformTests.cs`

**Interfaces:**
- Produces:

```csharp
namespace PS5Kitsune.Core.Settings;
public sealed record AppSettings(int SchemaVersion, string? DataRoot) { public const int CurrentSchemaVersion = 1; public static AppSettings Default { get; } }
public sealed record AppSettingsLoad(AppSettings Settings, string? Problem);
public sealed class AppSettingsStore(string settingsDirectory) { string FilePath; AppSettingsLoad Load(); Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default); }
public static class DataRoots { string Resolve(string? cliRoot, AppSettings settings, string defaultRoot); string? Prepare(string path); }
// IPlatform
void OpenPath(string path);
```

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.Core.Tests/Settings/AppSettingsStoreTests.cs`:

```csharp
using PS5Kitsune.Core.Settings;

namespace PS5Kitsune.Core.Tests.Settings;

public sealed class AppSettingsStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    private AppSettingsStore Store => new(_temp.Combine("settings"));

    [Fact]
    public void A_missing_file_gives_the_defaults()
    {
        var load = Store.Load();

        Assert.Equal(AppSettings.Default, load.Settings);
        Assert.Null(load.Settings.DataRoot);
        Assert.Null(load.Problem);
    }

    [Fact]
    public async Task Saved_settings_load_back()
    {
        var settings = new AppSettings(AppSettings.CurrentSchemaVersion, @"D:\PS5Kitsune");

        await Store.SaveAsync(settings, TestContext.Current.CancellationToken);

        Assert.Equal(new AppSettingsLoad(settings, null), Store.Load());
        Assert.True(File.Exists(_temp.Combine("settings", "settings.json")));
    }

    [Fact]
    public async Task A_corrupt_file_gives_the_defaults_and_a_problem()
    {
        Directory.CreateDirectory(_temp.Combine("settings"));
        await File.WriteAllTextAsync(_temp.Combine("settings", "settings.json"), "{ nope", TestContext.Current.CancellationToken);

        var load = Store.Load();

        Assert.Equal(AppSettings.Default, load.Settings);
        Assert.Contains("settings.json", load.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_schema_version_gives_the_defaults_and_a_problem()
    {
        Directory.CreateDirectory(_temp.Combine("settings"));
        await File.WriteAllTextAsync(_temp.Combine("settings", "settings.json"), """{ "schemaVersion": 7, "dataRoot": "D:\\X" }""", TestContext.Current.CancellationToken);

        var load = Store.Load();

        Assert.Equal(AppSettings.Default, load.Settings);
        Assert.Contains("schema version 7", load.Problem, StringComparison.Ordinal);
    }

    public void Dispose() => _temp.Dispose();
}
```

`tests/PS5Kitsune.Core.Tests/Settings/DataRootsTests.cs`:

```csharp
using PS5Kitsune.Core.Settings;

namespace PS5Kitsune.Core.Tests.Settings;

public sealed class DataRootsTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void The_command_line_root_wins()
    {
        Assert.Equal("cli", DataRoots.Resolve("cli", new AppSettings(1, "saved"), "default"));
    }

    [Fact]
    public void The_saved_root_beats_the_default()
    {
        Assert.Equal("saved", DataRoots.Resolve(null, new AppSettings(1, "saved"), "default"));
    }

    [Fact]
    public void The_default_root_is_the_fallback()
    {
        Assert.Equal("default", DataRoots.Resolve(null, AppSettings.Default, "default"));
    }

    [Fact]
    public void Prepare_creates_a_writable_folder()
    {
        var root = _temp.Combine("new", "root");

        Assert.Null(DataRoots.Prepare(root));
        Assert.True(Directory.Exists(root));
        Assert.Empty(Directory.EnumerateFileSystemEntries(root));
    }

    [Fact]
    public void Prepare_reports_a_folder_that_cannot_be_created()
    {
        var file = _temp.Combine("a-file");
        File.WriteAllText(file, "x");

        var problem = DataRoots.Prepare(Path.Combine(file, "root"));

        Assert.NotNull(problem);
        Assert.StartsWith("Cannot use ", problem, StringComparison.Ordinal);
    }

    public void Dispose() => _temp.Dispose();
}
```

Add to `WindowsPlatformTests`:

```csharp
    [Fact]
    public void Settings_live_in_local_app_data()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");

        Assert.Equal(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PS5Kitsune"),
            _platform.SettingsDirectory);
    }
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build error, namespace `PS5Kitsune.Core.Settings` does not exist.

- [ ] **Step 3: Implement Core**

`src/PS5Kitsune.Core/Settings/AppSettings.cs`:

```csharp
namespace PS5Kitsune.Core.Settings;

/// <summary>Contents of settings.json, shared by the app and the CLI.</summary>
/// <param name="DataRoot">The data root chosen in the app; null for the platform's default.</param>
public sealed record AppSettings(int SchemaVersion, string? DataRoot)
{
    public const int CurrentSchemaVersion = 1;

    public static AppSettings Default { get; } = new(CurrentSchemaVersion, null);
}

/// <param name="Problem">Why the file was ignored, to log once logging exists; null when it was read or absent.</param>
public sealed record AppSettingsLoad(AppSettings Settings, string? Problem);
```

`src/PS5Kitsune.Core/Settings/AppSettingsStore.cs`:

```csharp
using System.Text.Json;
using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Core.Settings;

/// <summary>Reads and writes settings.json in the platform's settings folder, outside the data root.</summary>
public sealed class AppSettingsStore(string settingsDirectory)
{
    public string FilePath { get; } = Path.Combine(settingsDirectory, "settings.json");

    /// <summary>Never throws: a file that cannot be used gives the defaults and says why.</summary>
    public AppSettingsLoad Load()
    {
        try
        {
            var settings = JsonFiles.Read<AppSettings>(FilePath);
            if (settings is null)
            {
                return new AppSettingsLoad(AppSettings.Default, null);
            }

            return settings.SchemaVersion == AppSettings.CurrentSchemaVersion
                ? new AppSettingsLoad(settings, null)
                : new AppSettingsLoad(AppSettings.Default, $"{FilePath} has schema version {settings.SchemaVersion}; using the default settings.");
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppSettingsLoad(AppSettings.Default, $"{FilePath} cannot be read ({exception.Message}); using the default settings.");
        }
    }

    /// <summary>Writes through a temporary file, so a crash never leaves half a file.</summary>
    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
        JsonFiles.WriteAsync(FilePath, settings, cancellationToken);
}
```

`src/PS5Kitsune.Core/Settings/DataRoots.cs`:

```csharp
namespace PS5Kitsune.Core.Settings;

public static class DataRoots
{
    /// <summary>The command line's root, else the root saved by the app, else the platform's default.</summary>
    public static string Resolve(string? cliRoot, AppSettings settings, string defaultRoot)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return cliRoot ?? settings.DataRoot ?? defaultRoot;
    }

    /// <summary>Creates the folder if needed and checks a file can be written in it.</summary>
    /// <returns>Null when the folder can be used, else a message for the user.</returns>
    public static string? Prepare(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            Directory.CreateDirectory(full);
            var probe = Path.Combine(full, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return $"Cannot use {path}: {exception.Message}";
        }
    }
}
```

In `IPlatform.cs`, after `OpenFix`:

```csharp
    /// <summary>Opens a folder in the file manager, or a file with its default app.</summary>
    void OpenPath(string path);
```

In `WindowsPlatform.cs`, change `SettingsDirectory` and add `OpenPath` after `OpenFix`:

```csharp
    /// <summary>Local, not roaming: the data root it names is a path on this PC.</summary>
    public string SettingsDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PS5Kitsune");
```

```csharp
    public void OpenPath(string path)
    {
        using (Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }))
        {
        }
    }
```

Add `public void OpenPath(string path) => OpenedPaths.Add(path);` and `public List<string> OpenedPaths { get; } = [];` to `FakePlatform` (Core.Tests) and to `RecordingPlatform` (App.Tests).

- [ ] **Step 4: Implement the CLI**

In `CliLog.cs`, add:

```csharp
    [LoggerMessage(Level = LogLevel.Warning, Message = "{Problem}")]
    public static partial void SettingsIgnored(this ILogger logger, string problem);
```

In `CommandHost.cs`, add `using PS5Kitsune.Core.Settings;`, change the summary's first line to `Builds the services for one command on the root from --root, else the app's settings, else the default; logs to ...`, and replace the first lines of `RunAsync`:

```csharp
        var platform = PlatformServices.Detect();
        var settings = new AppSettingsStore(platform.SettingsDirectory).Load();
        var layout = new DataLayout(DataRoots.Resolve(rootOverride?.FullName, settings.Settings, platform.DefaultDataRoot));
        var services = new ServiceCollection();
        var logFile = services.AddPS5KitsuneLogging(layout.Logs, runName, console: true);
        services.AddPlatform(platform).AddPS5KitsuneCore(layout, ProfileLoader.LoadEmbedded());

        await using var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("ps5kitsune");
        if (settings.Problem is { } problem)
        {
            logger.SettingsIgnored(problem);
        }
```

In `Program.cs`, change the `--root` description:

```csharp
    Description = "Data root (tools, AnyPS5, games). Defaults to the folder chosen in the app, else C:\\PS5Kitsune on Windows.",
```

- [ ] **Step 5: Run the tests**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: all pass.

- [ ] **Step 6: Commit**

```bash
git add src tests
git commit -m "Save the data root in a settings file read by the app and the CLI

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: App foundations: navigation, sessions, test fakes

**Files:**
- Create: `src/PS5Kitsune.App/Navigation/Navigator.cs`, `INavigationAware.cs`, `IPages.cs`, `IUiDispatcher.cs`, `IDialogService.cs`
- Create: `src/PS5Kitsune.App/Sessions/RootSession.cs`, `SessionHost.cs`
- Create: `tests/PS5Kitsune.App.Tests/Fakes.cs`
- Modify: `tests/PS5Kitsune.App.Tests/PS5Kitsune.App.Tests.csproj` (link `TempDirectory.cs`)
- Modify: `tests/PS5Kitsune.App.Tests/SystemCheckViewModelTests.cs` (use `TestPlatform` from `Fakes.cs`, delete `RecordingPlatform`)
- Test: `tests/PS5Kitsune.App.Tests/NavigatorTests.cs`, `tests/PS5Kitsune.App.Tests/SessionTests.cs`

**Interfaces:**
- Consumes: `IAnyPS5Installer`, `AnyPS5InstallState` (Task 3), `IPlatform.OpenPath` (Task 4).
- Produces:

```csharp
namespace PS5Kitsune.App.Navigation;
public interface INavigationAware { void OnNavigatedTo(); }
public interface IPages { object Home(); object SystemCheck(); object Mode(); object Install(); object Done(); }
public interface IUiDispatcher { void Post(Action action); }
public interface IDialogService
{
    Task<string?> PickFolderAsync(string title, string? startFolder);
    Task<bool> ConfirmAsync(string title, string message, string confirmText);
}
public sealed partial class Navigator : ObservableObject
{
    object? CurrentPage { get; }   // observable
    void GoTo(object page);        // pushes the current page
    void GoBack();                 // pops; does nothing on an empty stack
    void GoHome(object home);      // clears the stack
}

namespace PS5Kitsune.App.Sessions;
public sealed class RootSession : IDisposable
{
    RootSession(DataLayout layout, ServiceProvider services, string logFile);
    static RootSession Create(string root, IPlatform platform, AnyPS5Profile profile);
    DataLayout Layout { get; } string LogFile { get; } T Get<T>() where T : notnull;
}
public sealed class SessionHost : IDisposable
{
    SessionHost(Func<string, RootSession> create, string root);
    RootSession Current { get; } bool IsBusy { get; set; } event EventHandler? SessionChanged;
    void ChangeRoot(string root);   // InvalidOperationException while IsBusy
}
```

Test fakes (namespace `PS5Kitsune.App.Tests`), used by Tasks 6–11: `TestPlatform`, `FakeInstaller`, `FakePages`, `FakeDialogs`, `InlineDispatcher`, `TestSessions`.

- [ ] **Step 1: Shared fakes**

In `PS5Kitsune.App.Tests.csproj`, add to the `Compile` items (new `ItemGroup`):

```xml
  <ItemGroup>
    <Compile Include="..\PS5Kitsune.Core.Tests\TempDirectory.cs" Link="TempDirectory.cs" />
  </ItemGroup>
```

`tests/PS5Kitsune.App.Tests/Fakes.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Sessions;
using PS5Kitsune.Core;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.App.Tests;

internal sealed class TestPlatform : IPlatform
{
    public List<CheckFix> OpenedFixes { get; } = [];

    public List<string> OpenedPaths { get; } = [];

    /// <summary>Thrown by OpenPath and OpenFix when set.</summary>
    public Exception? OpenFailure { get; set; }

    public string Name => "Test";

    public string DefaultDataRoot => "unused";

    public string SettingsDirectory => "unused";

    public string ExecutableSuffix => ".exe";

    public ulong? GetCommitLimitBytes() => null;

    public IReadOnlyList<string> GetVulkanDriverManifests() => [];

    public void OpenFix(CheckFix fix)
    {
        if (OpenFailure is not null)
        {
            throw OpenFailure;
        }

        OpenedFixes.Add(fix);
    }

    public void OpenPath(string path)
    {
        if (OpenFailure is not null)
        {
            throw OpenFailure;
        }

        OpenedPaths.Add(path);
    }
}

internal delegate Task InstallBehavior(IProgress<StepProgress>? progress, Action<StepEvent>? onEvent, CancellationToken cancellationToken);

internal sealed class FakeInstaller(params string[] stepNames) : IAnyPS5Installer
{
    public AnyPS5InstallState State { get; set; } = new(AnyPS5InstallStatus.NotInstalled, null);

    public InstallBehavior Install { get; set; } = (_, _, _) => Task.CompletedTask;

    public int Runs { get; private set; }

    public IReadOnlyList<IStep> CreateSteps() => [.. stepNames.Select(name => new NamedStep(name))];

    public AnyPS5InstallState GetState() => State;

    public Task InstallAsync(IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default)
    {
        Runs++;
        return Install(progress, onEvent, cancellationToken);
    }

    private sealed class NamedStep(string name) : IStep
    {
        public string Name => name;

        public bool IsAlreadyDone() => false;

        public Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

/// <summary>Pages are plain strings, so a test asserts where the navigator went.</summary>
internal sealed class FakePages : IPages
{
    public object Home() => "home";

    public object SystemCheck() => "check";

    public object Mode() => "mode";

    public object Install() => "install";

    public object Done() => "done";
}

internal sealed class FakeDialogs : IDialogService
{
    public string? Folder { get; set; }

    public bool Confirm { get; set; }

    public List<string> Confirmations { get; } = [];

    public Task<string?> PickFolderAsync(string title, string? startFolder) => Task.FromResult(Folder);

    public Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        Confirmations.Add(message);
        return Task.FromResult(Confirm);
    }
}

internal sealed class InlineDispatcher : IUiDispatcher
{
    public void Post(Action action) => action();
}

internal static class TestSessions
{
    public static readonly AnyPS5Profile Profile = new()
    {
        SchemaVersion = AnyPS5Profile.CurrentSchemaVersion,
        Id = "test-profile",
        Validated = true,
        Source = new ProfileSource("release", Tag: "v0.1.2", PublishedAt: new DateTimeOffset(2026, 10, 14, 8, 56, 52, TimeSpan.Zero)),
    };

    /// <summary>A session whose services are the given installer and the test profile; nothing is logged.</summary>
    public static RootSession Create(string root, IAnyPS5Installer installer)
    {
        var services = new ServiceCollection();
        services.AddSingleton(installer);
        services.AddSingleton(Profile);
        return new RootSession(new DataLayout(root), services.BuildServiceProvider(), Path.Combine(root, "logs", "app.log"));
    }
}
```

In `SystemCheckViewModelTests.cs`, replace `private readonly RecordingPlatform _platform = new();` with `private readonly TestPlatform _platform = new();` and delete the nested `RecordingPlatform` class.

- [ ] **Step 2: Write the failing tests**

`tests/PS5Kitsune.App.Tests/NavigatorTests.cs`:

```csharp
using PS5Kitsune.App.Navigation;

namespace PS5Kitsune.App.Tests;

public sealed class NavigatorTests
{
    private readonly Navigator _navigator = new();

    [Fact]
    public void GoTo_shows_the_page_and_tells_it()
    {
        var page = new AwarePage();

        _navigator.GoTo(page);

        Assert.Same(page, _navigator.CurrentPage);
        Assert.Equal(1, page.Visits);
    }

    [Fact]
    public void GoBack_returns_to_the_previous_page_and_tells_it_again()
    {
        var first = new AwarePage();
        _navigator.GoTo(first);
        _navigator.GoTo("second");

        _navigator.GoBack();

        Assert.Same(first, _navigator.CurrentPage);
        Assert.Equal(2, first.Visits);
    }

    [Fact]
    public void GoBack_with_no_history_stays()
    {
        _navigator.GoTo("only");

        _navigator.GoBack();

        Assert.Equal("only", _navigator.CurrentPage);
    }

    [Fact]
    public void GoHome_clears_the_history()
    {
        _navigator.GoTo("a");
        _navigator.GoTo("b");

        _navigator.GoHome("home");
        _navigator.GoBack();

        Assert.Equal("home", _navigator.CurrentPage);
    }

    [Fact]
    public void CurrentPage_raises_PropertyChanged()
    {
        var changed = new List<string?>();
        _navigator.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _navigator.GoTo("a");

        Assert.Contains(nameof(Navigator.CurrentPage), changed);
    }

    private sealed class AwarePage : INavigationAware
    {
        public int Visits { get; private set; }

        public void OnNavigatedTo() => Visits++;
    }
}
```

`tests/PS5Kitsune.App.Tests/SessionTests.cs`:

```csharp
using Microsoft.Extensions.Logging;
using PS5Kitsune.App.Sessions;
using PS5Kitsune.Core.Tests;

namespace PS5Kitsune.App.Tests;

public sealed partial class SessionTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void Changing_the_root_replaces_and_disposes_the_session()
    {
        using var host = new SessionHost(root => TestSessions.Create(root, new FakeInstaller()), _temp.Combine("a"));
        var first = host.Current;
        var changes = 0;
        host.SessionChanged += (_, _) => changes++;

        host.ChangeRoot(_temp.Combine("b"));

        Assert.NotSame(first, host.Current);
        Assert.Equal(_temp.Combine("b"), host.Current.Layout.Root);
        Assert.True(first.IsDisposed);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void The_root_cannot_change_while_busy()
    {
        using var host = new SessionHost(root => TestSessions.Create(root, new FakeInstaller()), _temp.Combine("a"));
        host.IsBusy = true;

        Assert.Throws<InvalidOperationException>(() => host.ChangeRoot(_temp.Combine("b")));
        Assert.Equal(_temp.Combine("a"), host.Current.Layout.Root);
    }

    [Fact]
    public void Each_session_logs_under_its_own_root()
    {
        var profile = Core.Profiles.ProfileLoader.LoadEmbedded();
        string firstLog;
        using (var first = RootSession.Create(_temp.Combine("a"), new TestPlatform(), profile))
        {
            Write(first.Get<ILogger<SessionTests>>(), "first session");
            firstLog = first.LogFile;
        }

        using var second = RootSession.Create(_temp.Combine("b"), new TestPlatform(), profile);
        Write(second.Get<ILogger<SessionTests>>(), "second session");

        Assert.StartsWith(_temp.Combine("a", "logs"), firstLog, StringComparison.Ordinal);
        Assert.StartsWith(_temp.Combine("b", "logs"), second.LogFile, StringComparison.Ordinal);
        Assert.Contains("first session", File.ReadAllText(firstLog), StringComparison.Ordinal);
        Assert.Contains("second session", File.ReadAllText(second.LogFile), StringComparison.Ordinal);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Text}")]
    private static partial void Write(ILogger logger, string text);

    public void Dispose() => _temp.Dispose();
}
```

- [ ] **Step 3: Run them to see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build error, namespaces `PS5Kitsune.App.Navigation` and `PS5Kitsune.App.Sessions` do not exist.

- [ ] **Step 4: Implement navigation**

`src/PS5Kitsune.App/Navigation/INavigationAware.cs`:

```csharp
namespace PS5Kitsune.App.Navigation;

/// <summary>A page that wants to know when it is shown, including when the user comes back to it.</summary>
public interface INavigationAware
{
    void OnNavigatedTo();
}
```

`src/PS5Kitsune.App/Navigation/IPages.cs`:

```csharp
namespace PS5Kitsune.App.Navigation;

/// <summary>Creates page view models for the current data root. Pages are objects: the window picks a view by type.</summary>
public interface IPages
{
    object Home();

    object SystemCheck();

    object Mode();

    object Install();

    object Done();
}
```

`src/PS5Kitsune.App/Navigation/IUiDispatcher.cs`:

```csharp
namespace PS5Kitsune.App.Navigation;

/// <summary>Runs an action on the UI thread, in order. Tests run it inline.</summary>
public interface IUiDispatcher
{
    void Post(Action action);
}
```

`src/PS5Kitsune.App/Navigation/IDialogService.cs`:

```csharp
namespace PS5Kitsune.App.Navigation;

public interface IDialogService
{
    /// <returns>The chosen folder's local path; null when the user cancels.</returns>
    Task<string?> PickFolderAsync(string title, string? startFolder);

    /// <returns>True when the user confirms.</returns>
    Task<bool> ConfirmAsync(string title, string message, string confirmText);
}
```

`src/PS5Kitsune.App/Navigation/Navigator.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace PS5Kitsune.App.Navigation;

/// <summary>The page shown in the window, with a back stack.</summary>
public sealed partial class Navigator : ObservableObject
{
    private readonly Stack<object> _history = new();

    [ObservableProperty]
    public partial object? CurrentPage { get; private set; }

    public void GoTo(object page)
    {
        if (CurrentPage is not null)
        {
            _history.Push(CurrentPage);
        }

        Show(page);
    }

    public void GoBack()
    {
        if (_history.TryPop(out var page))
        {
            Show(page);
        }
    }

    public void GoHome(object home)
    {
        _history.Clear();
        Show(home);
    }

    private void Show(object page)
    {
        CurrentPage = page;
        (page as INavigationAware)?.OnNavigatedTo();
    }
}
```

- [ ] **Step 5: Implement sessions**

`src/PS5Kitsune.App/Sessions/RootSession.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Logging;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Platform.Windows;

namespace PS5Kitsune.App.Sessions;

/// <summary>The services for one data root: logging under its logs folder, and Core. Replaced when the root changes.</summary>
public sealed class RootSession(DataLayout layout, ServiceProvider services, string logFile) : IDisposable
{
    public DataLayout Layout { get; } = layout;

    /// <summary>This session's log file, for "Open log".</summary>
    public string LogFile { get; } = logFile;

    public bool IsDisposed { get; private set; }

    public static RootSession Create(string root, IPlatform platform, AnyPS5Profile profile)
    {
        var layout = new DataLayout(root);
        var services = new ServiceCollection();
        var logFile = services.AddPS5KitsuneLogging(layout.Logs, "app", console: false);
        services.AddPlatform(platform).AddPS5KitsuneCore(layout, profile);
        return new RootSession(layout, services.BuildServiceProvider(), logFile);
    }

    public T Get<T>()
        where T : notnull => services.GetRequiredService<T>();

    public void Dispose()
    {
        IsDisposed = true;
        services.Dispose();
    }
}
```

`src/PS5Kitsune.App/Sessions/SessionHost.cs`:

```csharp
namespace PS5Kitsune.App.Sessions;

/// <summary>Holds the session of the current data root and replaces it when the user picks another root.</summary>
public sealed class SessionHost : IDisposable
{
    private readonly Func<string, RootSession> _create;

    public SessionHost(Func<string, RootSession> create, string root)
    {
        _create = create;
        Current = create(root);
    }

    public RootSession Current { get; private set; }

    /// <summary>Set while an install runs: its services must not be disposed under it.</summary>
    public bool IsBusy { get; set; }

    public event EventHandler? SessionChanged;

    /// <exception cref="InvalidOperationException">An install is running.</exception>
    public void ChangeRoot(string root)
    {
        if (IsBusy)
        {
            throw new InvalidOperationException("The data root cannot change while an install runs.");
        }

        var previous = Current;
        Current = _create(root);
        previous.Dispose();
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => Current.Dispose();
}
```

- [ ] **Step 6: Run the tests**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: all pass. If `Each_session_logs_under_its_own_root` fails because the second session's file is empty, NLog kept the first session's configuration or shut down with it. Fix it in `LoggingSetup`: give each call its own `NLog.LogFactory` (`var factory = new LogFactory { Configuration = configuration };`, then `builder.AddNLog(_ => factory)`), so disposing one session never touches another. Rerun the whole suite, since the CLI uses the same setup.

- [ ] **Step 7: Commit**

```bash
git add src/PS5Kitsune.App tests/PS5Kitsune.App.Tests
git commit -m "Add page navigation and a service session per data root to the app

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: PC check page: Back, Continue, cancel, fix errors

**Files:**
- Modify: `src/PS5Kitsune.App/ViewModels/SystemCheckViewModel.cs`
- Modify: `src/PS5Kitsune.App/ViewModels/CheckRowViewModel.cs`
- Modify: `src/PS5Kitsune.App/Views/SystemCheckView.axaml`
- Test: `tests/PS5Kitsune.App.Tests/SystemCheckViewModelTests.cs`

**Interfaces:**
- Consumes: `Navigator`, `IPages`, `INavigationAware` (Task 5).
- Produces: `SystemCheckViewModel(ISystemChecker checker, IPlatform platform, Navigator navigator, IPages pages)`, with `bool CanContinue`, `ContinueCommand` (goes to `pages.Mode()`) and `BackCommand` (cancels a running check, then `navigator.GoBack()`). `CheckRowViewModel.FixError` (string?).

- [ ] **Step 1: Update the existing tests and write the failing ones**

In `SystemCheckViewModelTests`: change `FakeChecker` to receive the token, and build view models through a helper.

```csharp
    private readonly TestPlatform _platform = new();
    private readonly Navigator _navigator = new();

    private SystemCheckViewModel Create(Func<CancellationToken, Task<SystemCheckReport>> answer) =>
        new(new FakeChecker(answer), _platform, _navigator, new FakePages());
```

```csharp
    private sealed class FakeChecker(Func<CancellationToken, Task<SystemCheckReport>> answer) : ISystemChecker
    {
        public Task<SystemCheckReport> RunAsync(CancellationToken cancellationToken = default) => answer(cancellationToken);
    }
```

Replace every `new SystemCheckViewModel(new FakeChecker(() => X), _platform)` with `Create(_ => X)`. In `A_checker_failure_shows_its_message`, use `Create(_ => throw new IOException("disk gone"))`. Add `using PS5Kitsune.App.Navigation;`.

New tests:

```csharp
    [Theory]
    [InlineData(CheckStatus.Ok, true)]
    [InlineData(CheckStatus.Info, true)]
    [InlineData(CheckStatus.Warning, true)]
    [InlineData(CheckStatus.Blocking, false)]
    public async Task Continue_is_blocked_only_by_a_blocking_result(CheckStatus worst, bool canContinue)
    {
        var report = new SystemCheckReport([new CheckResult("a", "A", worst, "s")], Report().Vulkan);
        var viewModel = Create(_ => Task.FromResult(report));

        await viewModel.CheckCommand.ExecuteAsync(null);

        Assert.Equal(canContinue, viewModel.CanContinue);
        Assert.Equal(canContinue, viewModel.ContinueCommand.CanExecute(null));
    }

    [Fact]
    public void Continue_is_disabled_before_the_check()
    {
        Assert.False(Create(_ => Task.FromResult(Report())).CanContinue);
    }

    [Fact]
    public async Task Continue_goes_to_the_mode_page()
    {
        var viewModel = Create(_ => Task.FromResult(Report()));
        await viewModel.CheckCommand.ExecuteAsync(null);

        viewModel.ContinueCommand.Execute(null);

        Assert.Equal("mode", _navigator.CurrentPage);
    }

    [Fact]
    public async Task Entering_the_page_checks_once()
    {
        var runs = 0;
        var viewModel = Create(_ =>
        {
            runs++;
            return Task.FromResult(Report());
        });

        viewModel.OnNavigatedTo();
        await Task.Yield();
        viewModel.OnNavigatedTo();

        Assert.Equal(1, runs);
    }

    [Fact]
    public async Task Cancelling_says_so_and_allows_checking_again()
    {
        var viewModel = Create(async cancellationToken =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return Report();
        });
        var running = viewModel.CheckCommand.ExecuteAsync(null);

        viewModel.CheckCommand.Cancel();
        await running;

        Assert.False(viewModel.IsChecking);
        Assert.Null(viewModel.Overall);
        Assert.Equal("Check cancelled.", viewModel.Verdict);
        Assert.False(viewModel.CanContinue);
    }

    [Fact]
    public void Back_goes_to_the_previous_page()
    {
        _navigator.GoTo("home");
        var viewModel = Create(_ => Task.FromResult(Report()));
        _navigator.GoTo(viewModel);

        viewModel.BackCommand.Execute(null);

        Assert.Equal("home", _navigator.CurrentPage);
    }

    [Fact]
    public async Task A_fix_that_cannot_open_shows_the_error()
    {
        var viewModel = Create(_ => Task.FromResult(Report()));
        await viewModel.CheckCommand.ExecuteAsync(null);
        _platform.OpenFailure = new System.ComponentModel.Win32Exception("not found");

        viewModel.Checks[1].OpenFixCommand.Execute(null);

        Assert.Contains("not found", viewModel.Checks[1].FixError, StringComparison.Ordinal);
    }
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build error, the constructor takes two arguments.

- [ ] **Step 3: Implement**

Replace `SystemCheckViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.App.ViewModels;

/// <summary>The PC check screen: runs every check and shows the results with a verdict. A blocking result stops the install here.</summary>
public sealed partial class SystemCheckViewModel(ISystemChecker checker, IPlatform platform, Navigator navigator, IPages pages)
    : ObservableObject, INavigationAware
{
    public ObservableCollection<CheckRowViewModel> Checks { get; } = [];

    /// <summary>One line per GPU the Vulkan probe found.</summary>
    public ObservableCollection<string> Gpus { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanContinue))]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    public partial bool IsChecking { get; private set; }

    [ObservableProperty]
    public partial string Verdict { get; private set; } = "";

    /// <summary>Null while checking, before the first check and after a cancelled one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BannerStatus))]
    [NotifyPropertyChangedFor(nameof(CanContinue))]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    public partial CheckStatus? Overall { get; private set; }

    /// <summary>Colors the verdict banner: <see cref="Overall"/> with Info shown as Ok, because cpu-vendor is always Info.</summary>
    public CheckStatus? BannerStatus => Overall == CheckStatus.Info ? CheckStatus.Ok : Overall;

    /// <summary>Warnings do not block: the banner already shows them.</summary>
    public bool CanContinue => !IsChecking && Overall is { } overall && overall != CheckStatus.Blocking;

    /// <summary>Checks the first time the page is shown, and again after a cancelled check; not when coming back from Mode.</summary>
    public void OnNavigatedTo()
    {
        if (!IsChecking && Overall is null)
        {
            CheckCommand.Execute(null);
        }
    }

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
        catch (OperationCanceledException)
        {
            Verdict = "Check cancelled.";
        }
        catch (Exception exception)
        {
            Overall = CheckStatus.Blocking;
            Verdict = $"Could not check this PC: {exception.Message}";
        }
        finally
        {
            IsChecking = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanContinue))]
    private void Continue() => navigator.GoTo(pages.Mode());

    [RelayCommand]
    private void Back()
    {
        CheckCommand.Cancel();
        navigator.GoBack();
    }
}
```

(The old `catch (Exception exception) when (exception is not OperationCanceledException)` becomes two catches in that order. If CA1031 flags the general catch, keep the `when (exception is not OperationCanceledException)` filter on it.)

Replace `CheckRowViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.App.ViewModels;

/// <summary>One check on the PC check screen.</summary>
public sealed partial class CheckRowViewModel(CheckResult result, IPlatform platform) : ObservableObject
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

    /// <summary>Why the fix could not be opened; null when it opened.</summary>
    [ObservableProperty]
    public partial string? FixError { get; private set; }

    [RelayCommand]
    private void OpenFix()
    {
        if (result.Fix is { } fix)
        {
            FixError = PathOpener.TryRun(() => platform.OpenFix(fix));
        }
    }
}
```

Create `src/PS5Kitsune.App/ViewModels/PathOpener.cs` (also used by Tasks 9 and 10):

```csharp
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.App.ViewModels;

/// <summary>Opening a folder, a file or a settings dialog can fail (missing file, no associated app); the page shows why instead of crashing.</summary>
internal static class PathOpener
{
    /// <returns>Null on success, else the message to show.</returns>
    public static string? TryRun(Action open)
    {
        try
        {
            open();
            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return $"Could not open it: {exception.Message}";
        }
    }

    public static string? TryOpen(IPlatform platform, string path) => TryRun(() => platform.OpenPath(path));
}
```

In `SystemCheckView.axaml`: under the fix button's `StackPanel` text lines, add the error line:

```xml
                <TextBlock Text="{Binding FixError}" IsVisible="{Binding FixError, Converter={x:Static ObjectConverters.IsNotNull}}"
                           Foreground="#C42B1C" TextWrapping="Wrap" />
```

and add a button bar as the first child of the root `DockPanel` (docked at the bottom, before the GPU expander):

```xml
    <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right" Margin="0,12,0,0">
      <Button Content="Back" Command="{Binding BackCommand}" />
      <Button Content="Continue" Classes="accent" Command="{Binding ContinueCommand}" />
    </StackPanel>
```

- [ ] **Step 4: Run the tests**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.App.Tests.SystemCheckViewModelTests"`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.App tests/PS5Kitsune.App.Tests
git commit -m "Add Back and Continue to the PC check page and handle a cancelled check

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Home page

**Files:**
- Create: `src/PS5Kitsune.App/ViewModels/HomeViewModel.cs`
- Create: `src/PS5Kitsune.App/Views/HomeView.axaml`, `HomeView.axaml.cs`
- Test: `tests/PS5Kitsune.App.Tests/HomeViewModelTests.cs`

**Interfaces:**
- Consumes: `SessionHost`, `RootSession.Get<T>()`, `Navigator`, `IPages`, `IDialogService`, `INavigationAware` (Task 5); `AppSettingsStore`, `AppSettings`, `DataRoots.Prepare` (Task 4); `IAnyPS5Installer.GetState()` (Task 3).
- Produces: `HomeViewModel(SessionHost sessions, AppSettingsStore settings, IDialogService dialogs, Navigator navigator, IPages pages)`, with `Refresh()`, `StartCommand` (→ `pages.SystemCheck()`) and `ChangeRootCommand`. Properties: `AnyPS5InstallStatus Status`, `string StateTitle`, `string StateDetail`, `string ActionText`, `string DataRoot`, `string ProfileText`, `string? RootError`.

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.App.Tests/HomeViewModelTests.cs`:

```csharp
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Sessions;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Settings;
using PS5Kitsune.Core.Tests;

namespace PS5Kitsune.App.Tests;

public sealed class HomeViewModelTests : IDisposable
{
    private static readonly AnyPS5InstallRecord Record = new(1, "test-profile", "release", "v0.1.2", new Uri("https://example.test/u.py"), new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));

    private readonly TempDirectory _temp = new();
    private readonly FakeInstaller _installer = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly Navigator _navigator = new();
    private readonly SessionHost _sessions;
    private readonly AppSettingsStore _settings;

    public HomeViewModelTests()
    {
        _sessions = new SessionHost(root => TestSessions.Create(root, _installer), _temp.Combine("root"));
        _settings = new AppSettingsStore(_temp.Combine("settings"));
    }

    private HomeViewModel Create()
    {
        var viewModel = new HomeViewModel(_sessions, _settings, _dialogs, _navigator, new FakePages());
        viewModel.Refresh();
        return viewModel;
    }

    [Fact]
    public void Nothing_installed_offers_to_install()
    {
        var viewModel = Create();

        Assert.Equal(AnyPS5InstallStatus.NotInstalled, viewModel.Status);
        Assert.Equal("AnyPS5 is not installed", viewModel.StateTitle);
        Assert.Equal("Install AnyPS5", viewModel.ActionText);
        Assert.Equal("test-profile (validated)", viewModel.ProfileText);
        Assert.Equal(_temp.Combine("root"), viewModel.DataRoot);
    }

    [Fact]
    public void An_install_offers_to_verify_it()
    {
        _installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, Record);

        var viewModel = Create();

        Assert.Equal("AnyPS5 v0.1.2 is installed", viewModel.StateTitle);
        Assert.Contains("test-profile", viewModel.StateDetail, StringComparison.Ordinal);
        Assert.Equal("Verify / repair", viewModel.ActionText);
    }

    [Fact]
    public void An_incomplete_install_offers_to_resume()
    {
        _installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Incomplete, null);

        var viewModel = Create();

        Assert.Equal("The AnyPS5 install is incomplete", viewModel.StateTitle);
        Assert.Equal("Resume the install", viewModel.ActionText);
    }

    [Fact]
    public void Start_goes_to_the_pc_check()
    {
        Create().StartCommand.Execute(null);

        Assert.Equal("check", _navigator.CurrentPage);
    }

    [Fact]
    public void Entering_the_page_refreshes_the_state()
    {
        var viewModel = Create();
        _installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, Record);

        viewModel.OnNavigatedTo();

        Assert.Equal(AnyPS5InstallStatus.Installed, viewModel.Status);
    }

    [Fact]
    public async Task Changing_the_root_saves_it_and_replaces_the_session()
    {
        var viewModel = Create();
        var first = _sessions.Current;
        _dialogs.Folder = _temp.Combine("other");

        await viewModel.ChangeRootCommand.ExecuteAsync(null);

        Assert.Equal(_temp.Combine("other"), viewModel.DataRoot);
        Assert.Equal(_temp.Combine("other"), _sessions.Current.Layout.Root);
        Assert.True(first.IsDisposed);
        Assert.Equal(_temp.Combine("other"), _settings.Load().Settings.DataRoot);
        Assert.Null(viewModel.RootError);
    }

    [Fact]
    public async Task Cancelling_the_picker_changes_nothing()
    {
        var viewModel = Create();
        var first = _sessions.Current;

        await viewModel.ChangeRootCommand.ExecuteAsync(null);

        Assert.Same(first, _sessions.Current);
        Assert.False(File.Exists(_settings.FilePath));
    }

    [Fact]
    public async Task An_unusable_folder_keeps_the_root_and_says_why()
    {
        var viewModel = Create();
        var file = _temp.Combine("a-file");
        await File.WriteAllTextAsync(file, "x", TestContext.Current.CancellationToken);
        _dialogs.Folder = Path.Combine(file, "root");

        await viewModel.ChangeRootCommand.ExecuteAsync(null);

        Assert.Equal(_temp.Combine("root"), viewModel.DataRoot);
        Assert.StartsWith("Cannot use ", viewModel.RootError, StringComparison.Ordinal);
        Assert.False(File.Exists(_settings.FilePath));
    }

    public void Dispose()
    {
        _sessions.Dispose();
        _temp.Dispose();
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build error, `HomeViewModel` does not exist.

- [ ] **Step 3: Implement**

`src/PS5Kitsune.App/ViewModels/HomeViewModel.cs`:

```csharp
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Sessions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Settings;

namespace PS5Kitsune.App.ViewModels;

/// <summary>The first page: where AnyPS5 stands on the current data root, and the choice of that root.</summary>
public sealed partial class HomeViewModel(SessionHost sessions, AppSettingsStore settings, IDialogService dialogs, Navigator navigator, IPages pages)
    : ObservableObject, INavigationAware
{
    [ObservableProperty]
    public partial AnyPS5InstallStatus Status { get; private set; }

    [ObservableProperty]
    public partial string StateTitle { get; private set; } = "";

    [ObservableProperty]
    public partial string StateDetail { get; private set; } = "";

    [ObservableProperty]
    public partial string ActionText { get; private set; } = "";

    [ObservableProperty]
    public partial string DataRoot { get; private set; } = "";

    [ObservableProperty]
    public partial string ProfileText { get; private set; } = "";

    /// <summary>Why the chosen folder cannot be used; null otherwise.</summary>
    [ObservableProperty]
    public partial string? RootError { get; private set; }

    public void OnNavigatedTo() => Refresh();

    /// <summary>Reads the install state of the current root.</summary>
    public void Refresh()
    {
        var session = sessions.Current;
        var profile = session.Get<AnyPS5Profile>();
        var state = session.Get<IAnyPS5Installer>().GetState();
        DataRoot = session.Layout.Root;
        ProfileText = $"{profile.Id} ({(profile.Validated ? "validated" : "not validated yet")})";
        Status = state.Status;
        (StateTitle, StateDetail, ActionText) = state.Status switch
        {
            AnyPS5InstallStatus.Installed => (
                state.Record is null ? "AnyPS5 is installed" : $"AnyPS5 {state.Record.Version} is installed",
                state.Record is { } record
                    ? string.Create(CultureInfo.InvariantCulture, $"Profile {record.Profile}, installed {record.InstalledAt.LocalDateTime:yyyy-MM-dd HH:mm}.")
                    : "",
                "Verify / repair"),
            AnyPS5InstallStatus.Incomplete => (
                "The AnyPS5 install is incomplete",
                "Resume it to finish: the steps already done are skipped.",
                "Resume the install"),
            _ => (
                "AnyPS5 is not installed",
                "Install it to convert and run games. It takes a few minutes and needs no compilation.",
                "Install AnyPS5"),
        };
    }

    [RelayCommand]
    private void Start() => navigator.GoTo(pages.SystemCheck());

    [RelayCommand]
    private async Task ChangeRootAsync()
    {
        var chosen = await dialogs.PickFolderAsync("Choose the PS5Kitsune data folder", DataRoot).ConfigureAwait(true);
        if (chosen is null)
        {
            return;
        }

        var root = Path.GetFullPath(chosen);
        if (DataRoots.Prepare(root) is { } problem)
        {
            RootError = problem;
            return;
        }

        try
        {
            await settings.SaveAsync(new AppSettings(AppSettings.CurrentSchemaVersion, root)).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            RootError = $"Cannot save the setting: {exception.Message}";
            return;
        }

        sessions.ChangeRoot(root);
        RootError = null;
        Refresh();
    }
}
```

`src/PS5Kitsune.App/Views/HomeView.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:PS5Kitsune.App.ViewModels"
             x:Class="PS5Kitsune.App.Views.HomeView"
             x:DataType="vm:HomeViewModel">
  <StackPanel Spacing="24" MaxWidth="680" HorizontalAlignment="Left">
    <Border Padding="20" CornerRadius="6" BorderThickness="1" BorderBrush="#40808080">
      <StackPanel Spacing="8">
        <TextBlock Text="{Binding StateTitle}" FontSize="20" FontWeight="SemiBold" />
        <TextBlock Text="{Binding StateDetail}" TextWrapping="Wrap" Opacity="0.85" />
        <Button Content="{Binding ActionText}" Command="{Binding StartCommand}" Classes="accent" Margin="0,8,0,0" />
      </StackPanel>
    </Border>

    <StackPanel Spacing="6">
      <TextBlock Text="Data folder" FontWeight="SemiBold" />
      <Grid ColumnDefinitions="*,Auto" ColumnSpacing="8">
        <TextBlock Text="{Binding DataRoot}" VerticalAlignment="Center" TextTrimming="CharacterEllipsis" />
        <Button Grid.Column="1" Content="Change…" Command="{Binding ChangeRootCommand}" />
      </Grid>
      <TextBlock Text="{Binding RootError}" IsVisible="{Binding RootError, Converter={x:Static ObjectConverters.IsNotNull}}"
                 Foreground="#C42B1C" TextWrapping="Wrap" />
      <TextBlock Text="Tools, AnyPS5 and games go here. The path must be ASCII with no spaces." Opacity="0.7" FontSize="12" TextWrapping="Wrap" />
    </StackPanel>

    <StackPanel Spacing="6">
      <TextBlock Text="AnyPS5 profile" FontWeight="SemiBold" />
      <TextBlock Text="{Binding ProfileText}" />
    </StackPanel>
  </StackPanel>
</UserControl>
```

`src/PS5Kitsune.App/Views/HomeView.axaml.cs`:

```csharp
using Avalonia.Controls;

namespace PS5Kitsune.App.Views;

public sealed partial class HomeView : UserControl
{
    public HomeView() => InitializeComponent();
}
```

- [ ] **Step 4: Run the tests**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.App.Tests.HomeViewModelTests"`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.App tests/PS5Kitsune.App.Tests
git commit -m "Add the home page with the install state and the data folder choice

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Mode page

**Files:**
- Create: `src/PS5Kitsune.App/ViewModels/ModeViewModel.cs`
- Create: `src/PS5Kitsune.App/Views/ModeView.axaml`, `ModeView.axaml.cs`
- Test: `tests/PS5Kitsune.App.Tests/ModeViewModelTests.cs`

**Interfaces:**
- Consumes: `ProfileSource.PublishedAt` (Task 1), `Navigator`, `IPages` (Task 5).
- Produces: `ModeViewModel(AnyPS5Profile profile, Navigator navigator, IPages pages)`, with `ReleaseTitle`, `PublishedText`, `ProfileStatusText`, `BackCommand` and `InstallCommand` (→ `pages.Install()`).

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.App.Tests/ModeViewModelTests.cs`:

```csharp
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.App.Tests;

public sealed class ModeViewModelTests
{
    private readonly Navigator _navigator = new();

    [Fact]
    public void Describes_the_release()
    {
        var viewModel = new ModeViewModel(TestSessions.Profile, _navigator, new FakePages());

        Assert.Equal("Release v0.1.2", viewModel.ReleaseTitle);
        Assert.Equal("Published October 14, 2026", viewModel.PublishedText);
        Assert.Equal("Validated with DOOM for PS5", viewModel.ProfileStatusText);
    }

    [Fact]
    public void Says_when_the_date_or_validation_is_missing()
    {
        var profile = TestSessions.Profile with { Validated = false, Source = new ProfileSource("release", Tag: "v9") };

        var viewModel = new ModeViewModel(profile, _navigator, new FakePages());

        Assert.Equal("Publication date unknown", viewModel.PublishedText);
        Assert.Equal("Not validated yet", viewModel.ProfileStatusText);
    }

    [Fact]
    public void Install_goes_to_the_install_page()
    {
        var viewModel = new ModeViewModel(TestSessions.Profile, _navigator, new FakePages());

        viewModel.InstallCommand.Execute(null);

        Assert.Equal("install", _navigator.CurrentPage);
    }

    [Fact]
    public void Back_returns_to_the_pc_check()
    {
        _navigator.GoTo("check");
        var viewModel = new ModeViewModel(TestSessions.Profile, _navigator, new FakePages());
        _navigator.GoTo(viewModel);

        viewModel.BackCommand.Execute(null);

        Assert.Equal("check", _navigator.CurrentPage);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build error, `ModeViewModel` does not exist.

- [ ] **Step 3: Implement**

`src/PS5Kitsune.App/ViewModels/ModeViewModel.cs`:

```csharp
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.App.ViewModels;

/// <summary>Release or build. Build mode arrives with milestone 5, so only Release can be chosen.</summary>
public sealed partial class ModeViewModel(AnyPS5Profile profile, Navigator navigator, IPages pages)
{
    public string ReleaseTitle => $"Release {profile.Source.Version}";

    public string PublishedText => profile.Source.PublishedAt is { } publishedAt
        ? "Published " + publishedAt.UtcDateTime.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture)
        : "Publication date unknown";

    public string ProfileStatusText => profile.Validated ? "Validated with DOOM for PS5" : "Not validated yet";

    [RelayCommand]
    private void Back() => navigator.GoBack();

    [RelayCommand]
    private void Install() => navigator.GoTo(pages.Install());
}
```

`src/PS5Kitsune.App/Views/ModeView.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:PS5Kitsune.App.ViewModels"
             x:Class="PS5Kitsune.App.Views.ModeView"
             x:DataType="vm:ModeViewModel">
  <DockPanel>
    <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right" Margin="0,12,0,0">
      <Button Content="Back" Command="{Binding BackCommand}" />
      <Button Content="Install" Classes="accent" Command="{Binding InstallCommand}" />
    </StackPanel>

    <StackPanel Spacing="16">
      <TextBlock Text="How should AnyPS5 be installed?" FontSize="20" FontWeight="SemiBold" />
      <Grid ColumnDefinitions="*,*" ColumnSpacing="16">
        <Border Padding="20" CornerRadius="6" BorderThickness="2" BorderBrush="{DynamicResource SystemAccentColor}">
          <StackPanel Spacing="6">
            <TextBlock Text="{Binding ReleaseTitle}" FontSize="16" FontWeight="SemiBold" />
            <TextBlock Text="{Binding PublishedText}" Opacity="0.85" />
            <TextBlock Text="{Binding ProfileStatusText}" Opacity="0.85" />
            <TextBlock Text="A few minutes, no compilation." Opacity="0.85" />
            <TextBlock Text="Selected" FontWeight="SemiBold" Margin="0,8,0,0" />
          </StackPanel>
        </Border>
        <Border Grid.Column="1" Padding="20" CornerRadius="6" BorderThickness="1" BorderBrush="#40808080" Opacity="0.5">
          <StackPanel Spacing="6">
            <TextBlock Text="Build from source" FontSize="16" FontWeight="SemiBold" />
            <TextBlock Text="Compiles the profile's commit with a portable toolchain." TextWrapping="Wrap" />
            <TextBlock Text="Available in a later version." FontStyle="Italic" />
          </StackPanel>
        </Border>
      </Grid>
    </StackPanel>
  </DockPanel>
</UserControl>
```

`src/PS5Kitsune.App/Views/ModeView.axaml.cs`:

```csharp
using Avalonia.Controls;

namespace PS5Kitsune.App.Views;

public sealed partial class ModeView : UserControl
{
    public ModeView() => InitializeComponent();
}
```

If `SystemAccentColor` does not resolve as a brush in Avalonia 12, use `{DynamicResource SystemAccentColor}` through a `SolidColorBrush`: `<Border.BorderBrush><SolidColorBrush Color="{DynamicResource SystemAccentColor}" /></Border.BorderBrush>`.

- [ ] **Step 4: Run the tests**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.App.Tests.ModeViewModelTests"`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.App tests/PS5Kitsune.App.Tests
git commit -m "Add the mode page with release mode and build mode shown as coming later

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Install page

**Files:**
- Create: `src/PS5Kitsune.App/ViewModels/InstallViewModel.cs`, `InstallStepViewModel.cs`
- Create: `src/PS5Kitsune.App/Views/InstallView.axaml`, `InstallView.axaml.cs`
- Test: `tests/PS5Kitsune.App.Tests/InstallViewModelTests.cs`

**Interfaces:**
- Consumes: `IAnyPS5Installer` (Task 3), `StepEvent`/`StepEventKind.Cancelled` (Task 2), `StepFailedException`, `StepProgress`, `DownloadProgress`; `Navigator`, `IPages`, `IUiDispatcher`, `INavigationAware` (Task 5); `PathOpener` (Task 6).
- Produces: `InstallViewModel(IAnyPS5Installer installer, string logFile, IPlatform platform, IUiDispatcher ui, Navigator navigator, IPages pages, Action<bool>? busyChanged = null)`. Members: `Steps`, `Log`, `IsRunning`, `HasFailed`, `Failure`, `IsDownloading`, `DownloadPercent`, `DownloadIndeterminate`, `DownloadText`, `OpenError`, `Task RunAsync()`, `Task CancelAndWaitAsync()`, and the commands `CancelCommand`, `RetryCommand`, `BackHomeCommand`, `OpenLogCommand`. `InstallStepState { Pending, Running, Skipped, Done, Failed, Cancelled }`.

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.App.Tests/InstallViewModelTests.cs`:

```csharp
using System.ComponentModel;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.App.Tests;

public sealed class InstallViewModelTests
{
    private readonly FakeInstaller _installer = new("Install Python", "Download the relinker");
    private readonly TestPlatform _platform = new();
    private readonly Navigator _navigator = new();
    private readonly List<bool> _busy = [];

    private InstallViewModel Create() =>
        new(_installer, "C:/root/logs/app.log", _platform, new InlineDispatcher(), _navigator, new FakePages(), _busy.Add);

    [Fact]
    public void Lists_the_steps_before_starting()
    {
        var viewModel = Create();

        Assert.Equal(["Install Python", "Download the relinker"], viewModel.Steps.Select(step => step.Name));
        Assert.All(viewModel.Steps, step => Assert.Equal(InstallStepState.Pending, step.State));
    }

    [Fact]
    public async Task A_successful_install_goes_to_Done()
    {
        _installer.Install = (_, onEvent, _) =>
        {
            onEvent!(new StepEvent(StepEventKind.Skipped, "Install Python", 1, 2));
            onEvent(new StepEvent(StepEventKind.Started, "Download the relinker", 2, 2));
            onEvent(new StepEvent(StepEventKind.Completed, "Download the relinker", 2, 2, TimeSpan.FromSeconds(1.5)));
            return Task.CompletedTask;
        };
        var viewModel = Create();

        await viewModel.RunAsync();

        Assert.Equal("done", _navigator.CurrentPage);
        Assert.Equal([InstallStepState.Skipped, InstallStepState.Done], viewModel.Steps.Select(step => step.State));
        Assert.Equal(["Install Python: already done", "Download the relinker…", "Download the relinker: done in 1.5 s"], viewModel.Log);
        Assert.False(viewModel.IsRunning);
        Assert.Equal([true, false], _busy);
    }

    [Fact]
    public async Task A_failure_shows_the_step_and_Retry_resumes()
    {
        _installer.Install = (_, onEvent, _) =>
        {
            var cause = new IOException("disk full");
            onEvent!(new StepEvent(StepEventKind.Started, "Download the relinker", 2, 2));
            onEvent(new StepEvent(StepEventKind.Failed, "Download the relinker", 2, 2, TimeSpan.Zero, cause));
            throw new StepFailedException("Download the relinker", cause);
        };
        var viewModel = Create();

        await viewModel.RunAsync();

        Assert.True(viewModel.HasFailed);
        Assert.Equal("Download the relinker failed: disk full", viewModel.Failure);
        Assert.Equal(InstallStepState.Failed, viewModel.Steps[1].State);
        Assert.Null(_navigator.CurrentPage);

        _installer.Install = (_, _, _) => Task.CompletedTask;
        await viewModel.RetryCommand.ExecuteAsync(null);

        Assert.False(viewModel.HasFailed);
        Assert.Equal("done", _navigator.CurrentPage);
        Assert.Equal(2, _installer.Runs);
    }

    [Fact]
    public async Task Cancel_goes_back_home()
    {
        _installer.Install = async (_, onEvent, cancellationToken) =>
        {
            onEvent!(new StepEvent(StepEventKind.Started, "Install Python", 1, 2));
            await Task.Delay(Timeout.Infinite, cancellationToken);
        };
        var viewModel = Create();

        var running = viewModel.RunAsync();
        Assert.True(viewModel.IsRunning);
        viewModel.CancelCommand.Execute(null);
        await running;

        Assert.Equal("home", _navigator.CurrentPage);
        Assert.False(viewModel.IsRunning);
    }

    [Fact]
    public async Task CancelAndWaitAsync_stops_a_running_install()
    {
        _installer.Install = (_, _, cancellationToken) => Task.Delay(Timeout.Infinite, cancellationToken);
        var viewModel = Create();
        _ = viewModel.RunAsync();

        await viewModel.CancelAndWaitAsync();

        Assert.False(viewModel.IsRunning);
    }

    [Fact]
    public async Task Download_progress_drives_the_bar_and_messages_go_to_the_log()
    {
        IProgress<StepProgress>? reported = null;
        var release = new TaskCompletionSource();
        _installer.Install = (progress, _, _) =>
        {
            reported = progress;
            return release.Task;
        };
        var viewModel = Create();
        var running = viewModel.RunAsync();

        reported!.Report(new StepProgress(Download: new(50, 200)));
        Assert.True(viewModel.IsDownloading);
        Assert.False(viewModel.DownloadIndeterminate);
        Assert.Equal(25, viewModel.DownloadPercent);

        reported.Report(new StepProgress(Download: new(50, null)));
        Assert.True(viewModel.DownloadIndeterminate);

        reported.Report(new StepProgress("Extracting libs"));
        Assert.Contains("Extracting libs", viewModel.Log);

        release.SetResult();
        await running;
        Assert.False(viewModel.IsDownloading);
    }

    [Fact]
    public void Entering_the_page_starts_the_install_once()
    {
        var viewModel = Create();

        viewModel.OnNavigatedTo();
        viewModel.OnNavigatedTo();

        Assert.Equal(1, _installer.Runs);
    }

    [Fact]
    public void Open_log_opens_the_session_log_or_shows_why_not()
    {
        var viewModel = Create();

        viewModel.OpenLogCommand.Execute(null);
        Assert.Equal(["C:/root/logs/app.log"], _platform.OpenedPaths);

        _platform.OpenFailure = new Win32Exception("no app");
        viewModel.OpenLogCommand.Execute(null);
        Assert.Contains("no app", viewModel.OpenError, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build error, `InstallViewModel` does not exist.

- [ ] **Step 3: Implement**

`src/PS5Kitsune.App/ViewModels/InstallStepViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace PS5Kitsune.App.ViewModels;

public enum InstallStepState
{
    Pending,
    Running,
    Skipped,
    Done,
    Failed,
    Cancelled,
}

/// <summary>One line of the install page's step list.</summary>
public sealed partial class InstallStepViewModel(string name) : ObservableObject
{
    public string Name => name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText))]
    public partial InstallStepState State { get; set; }

    public string StateText => State switch
    {
        InstallStepState.Running => "running…",
        InstallStepState.Skipped => "already done",
        InstallStepState.Done => "done",
        InstallStepState.Failed => "failed",
        InstallStepState.Cancelled => "cancelled",
        _ => "",
    };
}
```

`src/PS5Kitsune.App/ViewModels/InstallViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.App.ViewModels;

/// <summary>
/// Runs the install when the page is shown: step list, download bar, a log built from the installer's events.
/// Events and progress arrive on worker threads and are posted to the UI thread in order.
/// </summary>
public sealed partial class InstallViewModel : ObservableObject, INavigationAware
{
    private readonly IAnyPS5Installer _installer;
    private readonly string _logFile;
    private readonly IPlatform _platform;
    private readonly IUiDispatcher _ui;
    private readonly Navigator _navigator;
    private readonly IPages _pages;
    private readonly Action<bool>? _busyChanged;
    private CancellationTokenSource? _cancellation;
    private Task _running = Task.CompletedTask;
    private bool _started;

    /// <param name="busyChanged">Told true when the install starts and false when it stops, so the data root cannot change meanwhile.</param>
    public InstallViewModel(IAnyPS5Installer installer, string logFile, IPlatform platform, IUiDispatcher ui, Navigator navigator, IPages pages, Action<bool>? busyChanged = null)
    {
        _installer = installer;
        _logFile = logFile;
        _platform = platform;
        _ui = ui;
        _navigator = navigator;
        _pages = pages;
        _busyChanged = busyChanged;
        Steps = [.. installer.CreateSteps().Select(step => new InstallStepViewModel(step.Name))];
    }

    public ObservableCollection<InstallStepViewModel> Steps { get; }

    public ObservableCollection<string> Log { get; } = [];

    [ObservableProperty]
    public partial bool IsRunning { get; private set; }

    [ObservableProperty]
    public partial bool HasFailed { get; private set; }

    [ObservableProperty]
    public partial string? Failure { get; private set; }

    [ObservableProperty]
    public partial bool IsDownloading { get; private set; }

    [ObservableProperty]
    public partial double DownloadPercent { get; private set; }

    /// <summary>True when the server sent no length.</summary>
    [ObservableProperty]
    public partial bool DownloadIndeterminate { get; private set; }

    [ObservableProperty]
    public partial string DownloadText { get; private set; } = "";

    [ObservableProperty]
    public partial string? OpenError { get; private set; }

    public void OnNavigatedTo()
    {
        if (!_started)
        {
            _ = RunAsync();
        }
    }

    /// <summary>Runs the install; completed steps are skipped, so this also resumes after a failure.</summary>
    public Task RunAsync()
    {
        _started = true;
        _running = RunCoreAsync();
        return _running;
    }

    /// <summary>For closing the window: cancels the install and waits until the running step has stopped.</summary>
    public async Task CancelAndWaitAsync()
    {
        _cancellation?.Cancel();
        await _running.ConfigureAwait(true);
    }

    private async Task RunCoreAsync()
    {
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        IsRunning = true;
        HasFailed = false;
        Failure = null;
        foreach (var step in Steps)
        {
            step.State = InstallStepState.Pending;
        }

        _busyChanged?.Invoke(true);
        try
        {
            await _installer.InstallAsync(new UiProgress(this), stepEvent => _ui.Post(() => Apply(stepEvent)), cancellation.Token).ConfigureAwait(true);
            _ui.Post(() => _navigator.GoTo(_pages.Done()));
        }
        catch (OperationCanceledException)
        {
            _ui.Post(() => _navigator.GoHome(_pages.Home()));
        }
        catch (StepFailedException exception)
        {
            var message = $"{exception.StepName} failed: {exception.InnerException?.Message ?? exception.Message}";
            _ui.Post(() => Fail(message));
        }
        catch (Exception exception)
        {
            _ui.Post(() => Fail($"The install failed: {exception.Message}"));
        }
        finally
        {
            _cancellation = null;
            _busyChanged?.Invoke(false);
            _ui.Post(() =>
            {
                IsRunning = false;
                IsDownloading = false;
            });
        }
    }

    private void Fail(string message)
    {
        HasFailed = true;
        Failure = message;
    }

    private void Apply(StepEvent stepEvent)
    {
        Steps[stepEvent.Number - 1].State = stepEvent.Kind switch
        {
            StepEventKind.Started => InstallStepState.Running,
            StepEventKind.Skipped => InstallStepState.Skipped,
            StepEventKind.Completed => InstallStepState.Done,
            StepEventKind.Failed => InstallStepState.Failed,
            _ => InstallStepState.Cancelled,
        };
        Log.Add(stepEvent.Kind switch
        {
            StepEventKind.Started => $"{stepEvent.StepName}…",
            StepEventKind.Skipped => $"{stepEvent.StepName}: already done",
            StepEventKind.Completed => string.Create(CultureInfo.InvariantCulture, $"{stepEvent.StepName}: done in {stepEvent.Duration?.TotalSeconds ?? 0:0.0} s"),
            StepEventKind.Failed => $"{stepEvent.StepName}: failed: {stepEvent.Error?.Message}",
            _ => $"{stepEvent.StepName}: cancelled",
        });
        if (stepEvent.Kind != StepEventKind.Started)
        {
            IsDownloading = false;
        }
    }

    private void Apply(StepProgress progress)
    {
        if (progress.Message is { } message)
        {
            Log.Add(message);
        }

        if (progress.Download is { } download)
        {
            var total = download.TotalBytes ?? 0;
            IsDownloading = true;
            DownloadIndeterminate = total <= 0;
            DownloadPercent = total > 0 ? download.ReceivedBytes * 100.0 / total : 0;
            DownloadText = total > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{download.ReceivedBytes / 1048576.0:0.0} / {total / 1048576.0:0.0} MB")
                : string.Create(CultureInfo.InvariantCulture, $"{download.ReceivedBytes / 1048576.0:0.0} MB");
        }
    }

    [RelayCommand]
    private void Cancel() => _cancellation?.Cancel();

    [RelayCommand]
    private Task RetryAsync() => RunAsync();

    [RelayCommand]
    private void BackHome() => _navigator.GoHome(_pages.Home());

    [RelayCommand]
    private void OpenLog() => OpenError = PathOpener.TryOpen(_platform, _logFile);

    private sealed class UiProgress(InstallViewModel owner) : IProgress<StepProgress>
    {
        public void Report(StepProgress value) => owner._ui.Post(() => owner.Apply(value));
    }
}
```

`src/PS5Kitsune.App/Views/InstallView.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:PS5Kitsune.App.ViewModels"
             xmlns:sys="using:System"
             x:Class="PS5Kitsune.App.Views.InstallView"
             x:DataType="vm:InstallViewModel">
  <DockPanel>
    <StackPanel DockPanel.Dock="Top" Spacing="8">
      <TextBlock Text="Installing AnyPS5" FontSize="20" FontWeight="SemiBold" />
      <ItemsControl ItemsSource="{Binding Steps}">
        <ItemsControl.ItemTemplate>
          <DataTemplate x:DataType="vm:InstallStepViewModel">
            <Grid ColumnDefinitions="*,Auto" Margin="0,2">
              <TextBlock Text="{Binding Name}" />
              <TextBlock Grid.Column="1" Text="{Binding StateText}" Opacity="0.7" />
            </Grid>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
      <StackPanel IsVisible="{Binding IsDownloading}" Spacing="4">
        <ProgressBar Minimum="0" Maximum="100" Value="{Binding DownloadPercent}" IsIndeterminate="{Binding DownloadIndeterminate}" />
        <TextBlock Text="{Binding DownloadText}" Opacity="0.7" FontSize="12" />
      </StackPanel>
      <TextBlock Text="{Binding Failure}" IsVisible="{Binding HasFailed}" Foreground="#C42B1C" FontWeight="SemiBold" TextWrapping="Wrap" />
      <TextBlock Text="{Binding OpenError}" IsVisible="{Binding OpenError, Converter={x:Static ObjectConverters.IsNotNull}}"
                 Foreground="#C42B1C" TextWrapping="Wrap" />
    </StackPanel>

    <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right" Margin="0,12,0,0">
      <Button Content="Open log" Command="{Binding OpenLogCommand}" />
      <Button Content="Cancel" Command="{Binding CancelCommand}" IsVisible="{Binding IsRunning}" />
      <Button Content="Back to home" Command="{Binding BackHomeCommand}" IsVisible="{Binding HasFailed}" />
      <Button Content="Retry" Classes="accent" Command="{Binding RetryCommand}" IsVisible="{Binding HasFailed}" />
    </StackPanel>

    <Border Margin="0,12,0,0" Padding="8" CornerRadius="4" Background="#14808080">
      <ScrollViewer x:Name="LogScroller">
        <ItemsControl ItemsSource="{Binding Log}">
          <ItemsControl.ItemTemplate>
            <DataTemplate x:DataType="sys:String">
              <TextBlock Text="{Binding}" FontFamily="Cascadia Mono,Consolas,monospace" FontSize="12" TextWrapping="Wrap" />
            </DataTemplate>
          </ItemsControl.ItemTemplate>
        </ItemsControl>
      </ScrollViewer>
    </Border>
  </DockPanel>
</UserControl>
```

`src/PS5Kitsune.App/Views/InstallView.axaml.cs`:

```csharp
using Avalonia.Controls;
using PS5Kitsune.App.ViewModels;

namespace PS5Kitsune.App.Views;

public sealed partial class InstallView : UserControl
{
    public InstallView() => InitializeComponent();

    /// <summary>Keeps the newest log line in view.</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is InstallViewModel viewModel)
        {
            viewModel.Log.CollectionChanged += (_, _) => LogScroller.ScrollToEnd();
        }
    }
}
```

- [ ] **Step 4: Run the tests**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.App.Tests.InstallViewModelTests"`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.App tests/PS5Kitsune.App.Tests
git commit -m "Add the install page with steps, download progress, log, cancel and retry

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Done page

**Files:**
- Create: `src/PS5Kitsune.App/ViewModels/DoneViewModel.cs`
- Create: `src/PS5Kitsune.App/Views/DoneView.axaml`, `DoneView.axaml.cs`
- Test: `tests/PS5Kitsune.App.Tests/DoneViewModelTests.cs`

**Interfaces:**
- Consumes: `AnyPS5InstallRecord`, `AnyPS5Paths` (existing, `AnyPS5Paths(DataLayout, IPlatform)`); `PathOpener` (Task 6); `Navigator`, `IPages` (Task 5).
- Produces: `DoneViewModel(AnyPS5InstallRecord? record, AnyPS5Paths paths, string logFile, IPlatform platform, Navigator navigator, IPages pages)`, with `Title`, `Relinker`, `Libs`, `OpenError`, `OpenFolderCommand`, `OpenLogCommand` and `BackHomeCommand`.

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.App.Tests/DoneViewModelTests.cs`:

```csharp
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core;
using PS5Kitsune.Core.AnyPS5;

namespace PS5Kitsune.App.Tests;

public sealed class DoneViewModelTests
{
    private static readonly AnyPS5InstallRecord Record = new(1, "test-profile", "release", "v0.1.2", new Uri("https://example.test/u.py"), DateTimeOffset.UnixEpoch);

    private readonly TestPlatform _platform = new();
    private readonly Navigator _navigator = new();
    private readonly AnyPS5Paths _paths;

    public DoneViewModelTests()
    {
        _paths = new AnyPS5Paths(new DataLayout(Path.Combine(Path.GetTempPath(), "root")), _platform);
    }

    private DoneViewModel Create(AnyPS5InstallRecord? record = null) =>
        new(record ?? Record, _paths, "app.log", _platform, _navigator, new FakePages());

    [Fact]
    public void Shows_the_version_and_the_paths()
    {
        var viewModel = Create();

        Assert.Equal("AnyPS5 v0.1.2 is installed", viewModel.Title);
        Assert.Equal(_paths.Relinker, viewModel.Relinker);
        Assert.Equal(_paths.Libs, viewModel.Libs);
    }

    [Fact]
    public void Opens_the_folder_and_the_log()
    {
        var viewModel = Create();

        viewModel.OpenFolderCommand.Execute(null);
        viewModel.OpenLogCommand.Execute(null);

        Assert.Equal([_paths.RootDirectory, "app.log"], _platform.OpenedPaths);
    }

    [Fact]
    public void Back_to_home_clears_the_history()
    {
        _navigator.GoTo("install");
        var viewModel = Create();

        viewModel.BackHomeCommand.Execute(null);
        _navigator.GoBack();

        Assert.Equal("home", _navigator.CurrentPage);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build error, `DoneViewModel` does not exist.

- [ ] **Step 3: Implement**

`src/PS5Kitsune.App/ViewModels/DoneViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;

namespace PS5Kitsune.App.ViewModels;

/// <summary>The end of the install. Adding a game arrives with milestone 6, so this page only leads home.</summary>
public sealed partial class DoneViewModel(AnyPS5InstallRecord? record, AnyPS5Paths paths, string logFile, IPlatform platform, Navigator navigator, IPages pages)
    : ObservableObject
{
    public string Title => record is null ? "AnyPS5 is installed" : $"AnyPS5 {record.Version} is installed";

    public string Relinker => paths.Relinker;

    public string Libs => paths.Libs;

    [ObservableProperty]
    public partial string? OpenError { get; private set; }

    [RelayCommand]
    private void OpenFolder() => OpenError = PathOpener.TryOpen(platform, paths.RootDirectory);

    [RelayCommand]
    private void OpenLog() => OpenError = PathOpener.TryOpen(platform, logFile);

    [RelayCommand]
    private void BackHome() => navigator.GoHome(pages.Home());
}
```

`src/PS5Kitsune.App/Views/DoneView.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:PS5Kitsune.App.ViewModels"
             x:Class="PS5Kitsune.App.Views.DoneView"
             x:DataType="vm:DoneViewModel">
  <DockPanel>
    <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right" Margin="0,12,0,0">
      <Button Content="Open folder" Command="{Binding OpenFolderCommand}" />
      <Button Content="Open log" Command="{Binding OpenLogCommand}" />
      <Button Content="Back to home" Classes="accent" Command="{Binding BackHomeCommand}" />
    </StackPanel>

    <StackPanel Spacing="12">
      <TextBlock Text="{Binding Title}" FontSize="20" FontWeight="SemiBold" />
      <TextBlock Text="Adding games from the app comes in a later version; until then, use 'ps5kitsune add'." TextWrapping="Wrap" Opacity="0.85" />
      <Grid ColumnDefinitions="Auto,*" RowDefinitions="Auto,Auto" ColumnSpacing="12" RowSpacing="6">
        <TextBlock Text="Relinker" FontWeight="SemiBold" />
        <SelectableTextBlock Grid.Column="1" Text="{Binding Relinker}" TextWrapping="Wrap" />
        <TextBlock Grid.Row="1" Text="Libraries" FontWeight="SemiBold" />
        <SelectableTextBlock Grid.Row="1" Grid.Column="1" Text="{Binding Libs}" TextWrapping="Wrap" />
      </Grid>
      <TextBlock Text="{Binding OpenError}" IsVisible="{Binding OpenError, Converter={x:Static ObjectConverters.IsNotNull}}"
                 Foreground="#C42B1C" TextWrapping="Wrap" />
    </StackPanel>
  </DockPanel>
</UserControl>
```

`src/PS5Kitsune.App/Views/DoneView.axaml.cs`:

```csharp
using Avalonia.Controls;

namespace PS5Kitsune.App.Views;

public sealed partial class DoneView : UserControl
{
    public DoneView() => InitializeComponent();
}
```

- [ ] **Step 4: Run the tests**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.App.Tests.DoneViewModelTests"`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.App tests/PS5Kitsune.App.Tests
git commit -m "Add the done page with the install paths

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: Shell: page factory, window, dialogs, close guard

**Files:**
- Create: `src/PS5Kitsune.App/Shell/PageFactory.cs`, `AvaloniaDialogService.cs`, `AvaloniaUiDispatcher.cs`, `AppLog.cs`
- Create: `src/PS5Kitsune.App/Views/ConfirmWindow.cs`
- Modify: `src/PS5Kitsune.App/ViewModels/MainWindowViewModel.cs`
- Modify: `src/PS5Kitsune.App/Views/MainWindow.axaml`, `MainWindow.axaml.cs`
- Modify: `src/PS5Kitsune.App/App.axaml.cs`
- Test: `tests/PS5Kitsune.App.Tests/MainWindowViewModelTests.cs`, `tests/PS5Kitsune.App.Tests/PageFactoryTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 4–10.
- Produces: `PageFactory(SessionHost sessions, Navigator navigator, IDialogService dialogs, AppSettingsStore settings, IUiDispatcher ui, IPlatform platform) : IPages`; `MainWindowViewModel(SessionHost sessions, Navigator navigator, IPlatform platform, IDialogService dialogs)` with `Platform`, `DataRoot`, `Profile`, `Navigator`, `IsInstallRunning` and `Task<bool> ConfirmCloseAsync()`.

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.App.Tests/MainWindowViewModelTests.cs`:

```csharp
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Sessions;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Tests;

namespace PS5Kitsune.App.Tests;

public sealed class MainWindowViewModelTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FakeInstaller _installer = new("Install Python");
    private readonly FakeDialogs _dialogs = new();
    private readonly Navigator _navigator = new();
    private readonly SessionHost _sessions;
    private readonly MainWindowViewModel _viewModel;

    public MainWindowViewModelTests()
    {
        _sessions = new SessionHost(root => TestSessions.Create(root, _installer), _temp.Combine("a"));
        _viewModel = new MainWindowViewModel(_sessions, _navigator, new TestPlatform(), _dialogs);
    }

    [Fact]
    public void The_header_follows_the_data_root()
    {
        var changed = new List<string?>();
        _viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        Assert.Equal(_temp.Combine("a"), _viewModel.DataRoot);
        Assert.Equal("test-profile (validated)", _viewModel.Profile);

        _sessions.ChangeRoot(_temp.Combine("b"));

        Assert.Equal(_temp.Combine("b"), _viewModel.DataRoot);
        Assert.Contains(nameof(MainWindowViewModel.DataRoot), changed);
    }

    [Fact]
    public async Task Closing_without_an_install_asks_nothing()
    {
        Assert.True(await _viewModel.ConfirmCloseAsync());
        Assert.Empty(_dialogs.Confirmations);
    }

    [Fact]
    public async Task Closing_during_an_install_asks_and_can_be_declined()
    {
        var install = StartRunningInstall();

        Assert.True(_viewModel.IsInstallRunning);
        Assert.False(await _viewModel.ConfirmCloseAsync());
        Assert.Single(_dialogs.Confirmations);
        Assert.True(install.IsRunning);

        await install.CancelAndWaitAsync();
    }

    [Fact]
    public async Task Confirming_cancels_the_install_before_closing()
    {
        var install = StartRunningInstall();
        _dialogs.Confirm = true;

        Assert.True(await _viewModel.ConfirmCloseAsync());
        Assert.False(install.IsRunning);
    }

    private InstallViewModel StartRunningInstall()
    {
        _installer.Install = (_, _, cancellationToken) => Task.Delay(Timeout.Infinite, cancellationToken);
        var install = new InstallViewModel(_installer, "app.log", new TestPlatform(), new InlineDispatcher(), _navigator, new FakePages());
        _navigator.GoTo(install);
        return install;
    }

    public void Dispose()
    {
        _sessions.Dispose();
        _temp.Dispose();
    }
}
```

`tests/PS5Kitsune.App.Tests/PageFactoryTests.cs`:

```csharp
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Sessions;
using PS5Kitsune.App.Shell;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Settings;
using PS5Kitsune.Core.Tests;

namespace PS5Kitsune.App.Tests;

public sealed class PageFactoryTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void Builds_every_page_from_a_real_session()
    {
        var platform = new TestPlatform();
        var profile = ProfileLoader.LoadEmbedded();
        using var sessions = new SessionHost(root => RootSession.Create(root, platform, profile), _temp.Combine("root"));
        var pages = new PageFactory(sessions, new Navigator(), new FakeDialogs(), new AppSettingsStore(_temp.Combine("settings")), new InlineDispatcher(), platform);

        Assert.IsType<HomeViewModel>(pages.Home());
        Assert.IsType<SystemCheckViewModel>(pages.SystemCheck());
        Assert.IsType<ModeViewModel>(pages.Mode());
        var install = Assert.IsType<InstallViewModel>(pages.Install());
        Assert.Equal(6, install.Steps.Count);
        Assert.IsType<DoneViewModel>(pages.Done());
    }

    public void Dispose() => _temp.Dispose();
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build error, `PageFactory` and the new `MainWindowViewModel` constructor do not exist.

- [ ] **Step 3: Implement the shell classes**

`src/PS5Kitsune.App/Shell/PageFactory.cs`:

```csharp
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Sessions;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Settings;

namespace PS5Kitsune.App.Shell;

/// <summary>Builds each page from the services of the current data root.</summary>
public sealed class PageFactory(SessionHost sessions, Navigator navigator, IDialogService dialogs, AppSettingsStore settings, IUiDispatcher ui, IPlatform platform)
    : IPages
{
    private RootSession Session => sessions.Current;

    public object Home() => new HomeViewModel(sessions, settings, dialogs, navigator, this);

    public object SystemCheck() => new SystemCheckViewModel(Session.Get<ISystemChecker>(), platform, navigator, this);

    public object Mode() => new ModeViewModel(Session.Get<AnyPS5Profile>(), navigator, this);

    public object Install() =>
        new InstallViewModel(Session.Get<IAnyPS5Installer>(), Session.LogFile, platform, ui, navigator, this, busy => sessions.IsBusy = busy);

    public object Done() =>
        new DoneViewModel(Session.Get<IAnyPS5Installer>().GetState().Record, Session.Get<AnyPS5Paths>(), Session.LogFile, platform, navigator, this);
}
```

`src/PS5Kitsune.App/Shell/AvaloniaUiDispatcher.cs`:

```csharp
using Avalonia.Threading;
using PS5Kitsune.App.Navigation;

namespace PS5Kitsune.App.Shell;

public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public void Post(Action action) => Dispatcher.UIThread.Post(action);
}
```

`src/PS5Kitsune.App/Shell/AvaloniaDialogService.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Views;

namespace PS5Kitsune.App.Shell;

/// <param name="owner">The main window, once it exists.</param>
public sealed class AvaloniaDialogService(Func<Window?> owner) : IDialogService
{
    public async Task<string?> PickFolderAsync(string title, string? startFolder)
    {
        if (owner() is not { } window)
        {
            return null;
        }

        var start = startFolder is null ? null : await window.StorageProvider.TryGetFolderFromPathAsync(startFolder).ConfigureAwait(true);
        var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = start,
        }).ConfigureAwait(true);
        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        if (owner() is not { } window)
        {
            return true;
        }

        return await new ConfirmWindow(title, message, confirmText).ShowDialog<bool>(window).ConfigureAwait(true);
    }
}
```

`src/PS5Kitsune.App/Views/ConfirmWindow.cs`:

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace PS5Kitsune.App.Views;

/// <summary>A yes/no question. Closing it with the title bar counts as no.</summary>
public sealed class ConfirmWindow : Window
{
    public ConfirmWindow(string title, string message, string confirmText)
    {
        Title = title;
        Width = 440;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var confirm = new Button { Content = confirmText };
        confirm.Click += (_, _) => Close(true);
        var cancel = new Button { Content = "Cancel", IsDefault = true, IsCancel = true };
        cancel.Click += (_, _) => Close(false);

        Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { confirm, cancel },
                },
            },
        };
    }
}
```

`src/PS5Kitsune.App/Shell/AppLog.cs`:

```csharp
using Microsoft.Extensions.Logging;

namespace PS5Kitsune.App.Shell;

internal static partial class AppLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "{Problem}")]
    public static partial void SettingsIgnored(this ILogger logger, string problem);
}
```

- [ ] **Step 4: Implement the window**

Replace `MainWindowViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Sessions;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.App.ViewModels;

/// <summary>The window shell: a header with the current data root and profile, and the page the navigator shows.</summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly SessionHost _sessions;
    private readonly IPlatform _platform;
    private readonly IDialogService _dialogs;

    public MainWindowViewModel(SessionHost sessions, Navigator navigator, IPlatform platform, IDialogService dialogs)
    {
        _sessions = sessions;
        _platform = platform;
        _dialogs = dialogs;
        Navigator = navigator;
        sessions.SessionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(DataRoot));
            OnPropertyChanged(nameof(Profile));
        };
    }

    public Navigator Navigator { get; }

    public string Platform => _platform.Name;

    public string DataRoot => _sessions.Current.Layout.Root;

    public string Profile
    {
        get
        {
            var profile = _sessions.Current.Get<AnyPS5Profile>();
            return $"{profile.Id} ({(profile.Validated ? "validated" : "not validated yet")})";
        }
    }

    public bool IsInstallRunning => Navigator.CurrentPage is InstallViewModel { IsRunning: true };

    /// <summary>Asks before closing during an install; on yes, cancels it and waits for the running step to stop.</summary>
    /// <returns>True when the window may close.</returns>
    public async Task<bool> ConfirmCloseAsync()
    {
        if (Navigator.CurrentPage is not InstallViewModel { IsRunning: true } install)
        {
            return true;
        }

        var quit = await _dialogs.ConfirmAsync(
            "Quit PS5Kitsune?",
            "An install is running. Quitting cancels it; it will resume where it stopped.",
            "Quit").ConfigureAwait(true);
        if (!quit)
        {
            return false;
        }

        await install.CancelAndWaitAsync().ConfigureAwait(true);
        return true;
    }
}
```

Replace `MainWindow.axaml` (data templates for every page; content bound to the navigator):

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
    <DataTemplate DataType="vm:HomeViewModel">
      <views:HomeView />
    </DataTemplate>
    <DataTemplate DataType="vm:SystemCheckViewModel">
      <views:SystemCheckView />
    </DataTemplate>
    <DataTemplate DataType="vm:ModeViewModel">
      <views:ModeView />
    </DataTemplate>
    <DataTemplate DataType="vm:InstallViewModel">
      <views:InstallView />
    </DataTemplate>
    <DataTemplate DataType="vm:DoneViewModel">
      <views:DoneView />
    </DataTemplate>
  </Window.DataTemplates>

  <DockPanel Margin="24">
    <StackPanel DockPanel.Dock="Top" Spacing="8" Margin="0,0,0,16">
      <TextBlock Text="PS5Kitsune" FontSize="28" FontWeight="SemiBold" />
      <Grid ColumnDefinitions="Auto,*,Auto,*,Auto,*" ColumnSpacing="8" Opacity="0.7">
        <TextBlock Grid.Column="0" Text="Platform" FontWeight="SemiBold" />
        <TextBlock Grid.Column="1" Text="{Binding Platform}" />
        <TextBlock Grid.Column="2" Text="Data root" FontWeight="SemiBold" />
        <TextBlock Grid.Column="3" Text="{Binding DataRoot}" TextTrimming="CharacterEllipsis" />
        <TextBlock Grid.Column="4" Text="Profile" FontWeight="SemiBold" />
        <TextBlock Grid.Column="5" Text="{Binding Profile}" TextTrimming="CharacterEllipsis" />
      </Grid>
    </StackPanel>
    <ContentControl Content="{Binding Navigator.CurrentPage}" />
  </DockPanel>
</Window>
```

(The data root header gains `TextTrimming`, a milestone 3 follow-up.)

Replace `MainWindow.axaml.cs`:

```csharp
using Avalonia.Controls;
using PS5Kitsune.App.ViewModels;

namespace PS5Kitsune.App.Views;

public sealed partial class MainWindow : Window
{
    private bool _closeConfirmed;

    public MainWindow() => InitializeComponent();

    /// <summary>During an install, closing waits for the user's answer and for the install to stop.</summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!_closeConfirmed && DataContext is MainWindowViewModel { IsInstallRunning: true } viewModel)
        {
            e.Cancel = true;
            _ = ConfirmAndCloseAsync(viewModel);
        }

        base.OnClosing(e);
    }

    private async Task ConfirmAndCloseAsync(MainWindowViewModel viewModel)
    {
        if (await viewModel.ConfirmCloseAsync().ConfigureAwait(true))
        {
            _closeConfirmed = true;
            Close();
        }
    }
}
```

Replace `App.axaml.cs`:

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Logging;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.Sessions;
using PS5Kitsune.App.Shell;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.App.Views;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Settings;
using PS5Kitsune.Platform.Windows;

namespace PS5Kitsune.App;

public sealed partial class App : Application
{
    private SessionHost? _sessions;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>Composition root: root-independent objects here, everything tied to a data root in its <see cref="RootSession"/>.</summary>
    public override void OnFrameworkInitializationCompleted()
    {
        var platform = PlatformServices.Detect();
        var profile = ProfileLoader.LoadEmbedded();
        var settings = new AppSettingsStore(platform.SettingsDirectory);
        var loaded = settings.Load();
        _sessions = new SessionHost(
            root => RootSession.Create(root, platform, profile),
            DataRoots.Resolve(null, loaded.Settings, platform.DefaultDataRoot));
        if (loaded.Problem is { } problem)
        {
            _sessions.Current.Get<ILogger<App>>().SettingsIgnored(problem);
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            var navigator = new Navigator();
            var dialogs = new AvaloniaDialogService(() => window);
            var pages = new PageFactory(_sessions, navigator, dialogs, settings, new AvaloniaUiDispatcher(), platform);
            window.DataContext = new MainWindowViewModel(_sessions, navigator, platform, dialogs);
            navigator.GoHome(pages.Home());
            desktop.MainWindow = window;
            desktop.Exit += (_, _) => _sessions.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
```

The app no longer builds its own `ServiceCollection`, and the check no longer starts from `Window.Opened`.

- [ ] **Step 5: Run every test and the app**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: all pass.

Then run the app (`dotnet run --project src/PS5Kitsune.App -c Release`) and click through Home → PC check → Mode → Back → Back. Do **not** start an install here; that is Task 12. Close the app.

- [ ] **Step 6: Commit**

```bash
git add src/PS5Kitsune.App tests/PS5Kitsune.App.Tests
git commit -m "Wire the install screens into the window, with a close guard during installs

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 12: Documentation and the milestone criterion

**Files:**
- Modify: `docs/architecture.md`
- Modify: `docs/superpowers/specs/2026-10-21-milestone-4-install-screens-design.md` (result of the run)
- Modify: `README.md` only if it describes the app as check-only (read it first)

- [ ] **Step 1: Update the architecture**

In `docs/architecture.md`:

- Under "Root layout", after the tree, add:

```markdown
The data root itself is chosen on the app's home screen and saved in `%LOCALAPPDATA%\PS5Kitsune\settings.json` (`{ "schemaVersion": 1, "dataRoot": "…" }`), outside the root. The CLI reads the same file: `--root`, then the saved root, then `C:\PS5Kitsune`.
```

- In "Screens" (V1), after the list, add: `Milestone 4 implements Home, PC check, Mode (build shown as coming later), Install and Done. The Tools screen comes with build mode.`
- In "Resuming steps", replace the last sentence's follow-up mention only if it names the done-check-in-try follow-up. Otherwise add: `A done-check that throws counts as that step's failure; a cancellation emits a Cancelled event for the running step.`

- [ ] **Step 2: Build and test once more**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: build succeeds, all tests pass. Only continue with a green build (a stale exe would install into the test roots).

- [ ] **Step 3: Manual criterion (the user runs it, on the AMD laptop)**

Ask the user to run these and report:

1. Start the app (`dotnet run --project src/PS5Kitsune.App -c Release`). On Home, "Change…" → an empty folder such as `C:\PS5KTestUi`. The header and Home show it.
2. Install AnyPS5 → PC check (no blocking result) → Continue → Mode (Release v0.1.2, published October 14, 2026) → Install. Steps tick, the bar moves during downloads, then Done shows the relinker and libs paths. "Open folder" and "Open log" work.
3. Back to home: "AnyPS5 v0.1.2 is installed".
4. `dotnet run --project src/PS5Kitsune.Cli -c Release -- install` with **no** `--root`: every step says "already done".
5. `dotnet run --project src/PS5Kitsune.Cli -c Release -- install --root C:\PS5KTestCli`, then compare: `diff -r /c/PS5KTestUi/anyps5/current /c/PS5KTestCli/anyps5/current` (only `install.json` may differ, in `installedAt`) and `diff -r /c/PS5KTestUi/tools /c/PS5KTestCli/tools`.
6. Change the root to another empty folder, start an install and Cancel during "Download the libraries": Home says "incomplete". Resume: completed steps say "already done".
7. Start a resume, close the window during a download: the confirmation appears. "Cancel" keeps the install running, "Quit" closes after the step stops.

- [ ] **Step 4: Record the result**

Append to the spec, under a `## Criterion result` heading, the date, what passed, and any difference found. Update `docs/architecture.md` roadmap item 4 only if the project marks finished milestones there (check how milestones 2 and 3 were recorded and do the same).

- [ ] **Step 5: Commit**

```bash
git add docs README.md
git commit -m "Document the install screens and record the milestone 4 run

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## Self-review notes

- Spec coverage: Flow and Home (Tasks 7, 11); PC check additions (Task 6); Mode with `publishedAt` (Tasks 1, 8); Install page and log (Task 9); Done (Task 10); close guard (Task 11); data root, settings and CLI (Task 4, Task 7); `AnyPS5InstallState` (Task 3); StepRunner follow-ups (Task 2); `IPlatform.SettingsDirectory`/`OpenPath` (Task 4); sessions (Task 5); error table rows (Tasks 6, 7, 9, 10); tests per spec (each task); criterion (Task 12).
- Analyzers run on test projects too (`Directory.Build.props`), so test code logs through `[LoggerMessage]` methods (CA1848).
