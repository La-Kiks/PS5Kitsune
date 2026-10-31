# Milestone 8b — The app on Linux and the AppImage: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The Avalonia app runs on Linux and ships, with the CLI, as one AppImage built in CI; Linux exit codes, unknown exit codes, a clock-proof running-game identity and a process-group Stop are done.

**Architecture:** Core gains `ExitCodes` (human-readable codes per OS), nullable exit codes end to end, a schema 2 `running.json`, and platform texts carried by `IAnyPS5Installer` and `ILavapipe`. `Platform.Linux` fills the boot identity and kills the group with `waitid(WNOWAIT)`. A shell script builds the AppImage from two self-contained publishes sharing one folder.

**Tech Stack:** .NET 10, Avalonia 12.1.4, xUnit v3, POSIX `sh`, `appimagetool` 1.9.1 + `type2-runtime` 20251108, GitHub Actions.

Spec: [docs/superpowers/specs/2026-10-31-milestone-8b-linux-app-design.md](../specs/2026-10-31-milestone-8b-linux-app-design.md).

## Global Constraints

- The App project has no OS check (`OperatingSystem.Is*`); OS differences come from `IPlatform`, `IAnyPS5Installer`, `ILavapipe` or the profile.
- Tests: `dotnet test --solution PS5Kitsune.slnx -c Release` must pass on Windows (this machine) and in WSL Ubuntu 24.04 (`wsl -e bash -lc 'source ~/ps5k-env.sh && cd /mnt/c/Users/kilia/source/repos/PS5Kitsune && dotnet test --solution PS5Kitsune.slnx -c Release'`). Linux-only tests use `Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.")`.
- Build with 0 warnings (warnings are errors in Release).
- Commit messages: English, imperative, ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Commit locally; never push.
- Match the surrounding code: XML doc comments on public members, `string.Create(CultureInfo.InvariantCulture, …)` for formatted numbers, `[LoggerMessage]` for logs.
- Pinned tools (download URLs and SHA-256):
  - `https://github.com/AppImage/appimagetool/releases/download/1.9.1/appimagetool-x86_64.AppImage` — `ed4ce84f0d9caff66f50bcca6ff6f35aae54ce8135408b3fa33abfc3cb384eb0`
  - `https://github.com/AppImage/type2-runtime/releases/download/20251108/runtime-x86_64` — `2fca8b443c92510f1483a883f60061ad09b46b978b2631c807cd873a47ec260d`

**Deviations from the spec, decided while planning** (recorded in the spec by Task 8):

- The build card's text comes from `IAnyPS5Installer.BuildUnavailableReason`, not a new `IPlatform` member: the installer already knows the profile's OS and owns the CLI's refusal text.
- Exit codes are described by a Core helper, `ExitCodes.Describe`, not `IPlatform.DescribeExitCode`: `ExitLines`, `DiagnosisTexts` and `CrashReporter` are static or platform-free, and an exit code always comes from the OS PS5Kitsune runs on.
- Dozen "not applicable" comes from a new `SystemFacts.Os`, filled from `IPlatform.Os`.
- `ILavapipe` gets two texts, `HowToGet` (a sentence) and `WhenMissing` (what to do now).
- The CI version is `0.0.0-ci.<run number>` (no `+sha`: the SDK may add its own source revision to the informational version).

---

### Task 1: Exit codes described per OS, and the unknown exit line

**Files:**
- Create: `src/PS5Kitsune.Core/Diagnostics/ExitCodes.cs`
- Modify: `src/PS5Kitsune.Core/Diagnostics/ExitLines.cs`
- Modify: `src/PS5Kitsune.Core/Diagnostics/DiagnosisTexts.cs:153`
- Modify: `src/PS5Kitsune.Core/Diagnostics/CrashReporter.cs:85`
- Modify: `src/PS5Kitsune.Core/Diagnostics/CrashDiagnoser.cs` (`FinishAsync`, `AppendExitLineAsync`)
- Test: `tests/PS5Kitsune.Core.Tests/Diagnostics/ExitCodesTests.cs` (create), `tests/PS5Kitsune.Core.Tests/Diagnostics/CrashDiagnoserTests.cs`

**Interfaces:**
- Produces:
  - `public static class ExitCodes { public static string Describe(uint code); internal static string Describe(uint code, bool linux); }`
  - `ExitLines.Exited(int exitCode)` (OS of the process), `internal static string Exited(int exitCode, bool linux)`, `public static string Unknown()`
  - `internal readonly record struct ExitRecord(uint? Code, bool Stopped)`
  - `CrashDiagnoser.FinishAsync(string logPath, int? exitCode, bool stopped, CancellationToken)`; `AppendExitLineAsync(string logPath, int? exitCode, bool stopped)`. A null `exitCode` writes `ExitLines.Unknown()` (when not stopped) and diagnoses with log rules only.

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.Core.Tests/Diagnostics/ExitCodesTests.cs`:

```csharp
using PS5Kitsune.Core.Diagnostics;

namespace PS5Kitsune.Core.Tests.Diagnostics;

public sealed class ExitCodesTests
{
    [Theory]
    [InlineData(0u, "0x00000000")]
    [InlineData(0xC0000005u, "0xC0000005")]
    public void Windows_codes_are_hexadecimal(uint code, string text) => Assert.Equal(text, ExitCodes.Describe(code, linux: false));

    [Theory]
    [InlineData(0u, "0")]
    [InlineData(1u, "1")]
    [InlineData(139u, "139 (signal 11, SIGSEGV)")]
    [InlineData(137u, "137 (signal 9, SIGKILL)")]
    [InlineData(143u, "143 (signal 15, SIGTERM)")]
    [InlineData(162u, "162 (signal 34)")]
    [InlineData(200u, "200")]
    [InlineData(4294967295u, "4294967295")]
    public void Linux_codes_are_decimal_with_the_signal(uint code, string text) => Assert.Equal(text, ExitCodes.Describe(code, linux: true));
}
```

Add to `CrashDiagnoserTests.cs` (replace `Exit_lines_print_the_code_unsigned_and_in_hex`):

```csharp
    [Fact]
    public void Exit_lines_print_the_code_unsigned_and_in_hex_on_windows() =>
        Assert.Equal("[ps5kitsune] exited with code 3221225477 (0xC0000005)", ExitLines.Exited(unchecked((int)0xC0000005), linux: false));

    [Theory]
    [InlineData(139, "[ps5kitsune] exited with code 139 (signal 11, SIGSEGV)")]
    [InlineData(1, "[ps5kitsune] exited with code 1")]
    public void Exit_lines_name_the_signal_on_linux(int code, string line) => Assert.Equal(line, ExitLines.Exited(code, linux: true));

    [Fact]
    public async Task Linux_and_windows_exit_lines_both_give_their_code_back()
    {
        var log = Log("all good\n" + ExitLines.Exited(139, linux: true) + "\n");

        var diagnosis = await _diagnoser.DiagnoseAsync(log, null, Token);

        Assert.Equal(139u, diagnosis?.ExitCode);
    }

    [Fact]
    public async Task An_unknown_exit_code_is_written_and_only_log_rules_are_applied()
    {
        var log = Log("all good\n");

        var diagnosis = await _diagnoser.FinishAsync(log, null, stopped: false, Token);

        Assert.Null(diagnosis);
        Assert.EndsWith("[ps5kitsune] exited (exit code unknown)" + Environment.NewLine, await File.ReadAllTextAsync(log, Token));
    }

    [Fact]
    public async Task A_known_error_is_found_after_an_unknown_exit_code()
    {
        var log = Log("Vulkan: buffer device address and shaderInt64 are required\n");

        var diagnosis = await _diagnoser.FinishAsync(log, null, stopped: false, Token);

        Assert.Equal(DiagnosisIds.GpuFeatures, diagnosis?.Id);
        Assert.Null(diagnosis?.ExitCode);
    }
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests -c Release --filter-class "*ExitCodesTests" --filter-class "*CrashDiagnoserTests"`
Expected: build errors (`ExitCodes` missing, `Exited(int, bool)` missing, `FinishAsync` takes `int`).

- [ ] **Step 3: Implement**

`src/PS5Kitsune.Core/Diagnostics/ExitCodes.cs`:

```csharp
using System.Globalization;

namespace PS5Kitsune.Core.Diagnostics;

/// <summary>An exit code as people read it on the OS that produced it. See the 8b spec, "Linux exit codes".</summary>
public static class ExitCodes
{
    // The usual Linux signals by number; others are shown by number only.
    private static readonly string?[] SignalNames =
    [
        null, "SIGHUP", "SIGINT", "SIGQUIT", "SIGILL", "SIGTRAP", "SIGABRT", "SIGBUS", "SIGFPE", "SIGKILL", "SIGUSR1",
        "SIGSEGV", "SIGUSR2", "SIGPIPE", "SIGALRM", "SIGTERM",
    ];

    /// <summary>The code of a process that ran on this OS: hexadecimal on Windows (0xC0000005), decimal on Linux with the signal (139 (signal 11, SIGSEGV)).</summary>
    public static string Describe(uint code) => Describe(code, OperatingSystem.IsLinux());

    internal static string Describe(uint code, bool linux)
    {
        if (!linux)
        {
            return string.Create(CultureInfo.InvariantCulture, $"0x{code:X8}");
        }

        // The Linux launcher returns 128 + the signal that ended a process; real-time signals go up to 64.
        var signal = code - 128;
        if (code <= 128 || signal > 64)
        {
            return code.ToString(CultureInfo.InvariantCulture);
        }

        return signal < SignalNames.Length && SignalNames[signal] is { } name
            ? string.Create(CultureInfo.InvariantCulture, $"{code} (signal {signal}, {name})")
            : string.Create(CultureInfo.InvariantCulture, $"{code} (signal {signal})");
    }
}
```

`ExitLines.cs`: add the unknown line and the OS-aware exited line; `ExitRecord.Code` becomes `uint?`.

```csharp
    private const string UnknownLine = "exited (exit code unknown)";

    /// <summary>The exit line of a game that ran on this OS.</summary>
    public static string Exited(int exitCode) => Exited(exitCode, OperatingSystem.IsLinux());

    // The decimal code comes first on both OSes, so Parse reads every line ever written.
    internal static string Exited(int exitCode, bool linux) =>
        linux
            ? $"{Prefix}{ExitedStart}{ExitCodes.Describe((uint)exitCode, linux: true)}"
            : string.Create(CultureInfo.InvariantCulture, $"{Prefix}{ExitedStart}{(uint)exitCode} ({ExitCodes.Describe((uint)exitCode, linux: false)})");

    /// <summary>The exit line of a game whose exit code cannot be read: a game found again on Linux, which is not PS5Kitsune's child.</summary>
    public static string Unknown() => Prefix + UnknownLine;
```

In `Parse`, before the `TryCode` calls:

```csharp
        if (rest.SequenceEqual(UnknownLine))
        {
            return new ExitRecord(null, Stopped: false);
        }
```

and change the record: `internal readonly record struct ExitRecord(uint? Code, bool Stopped);`

`CrashDiagnoser.cs`:

```csharp
    /// <summary>Appends the exit line to the log, then diagnoses it unless the user stopped the game (`stopped`, or a stopped line already in the log).</summary>
    /// <param name="exitCode">Null when the exit code cannot be read: only the log rules can then match.</param>
    public async Task<Diagnosis?> FinishAsync(string logPath, int? exitCode, bool stopped, CancellationToken cancellationToken)
    {
        await AppendExitLineAsync(logPath, exitCode, stopped).ConfigureAwait(false);
        return stopped ? null : await DiagnoseAsync(logPath, (uint?)exitCode, cancellationToken).ConfigureAwait(false);
    }

    public async Task AppendExitLineAsync(string logPath, int? exitCode, bool stopped)
    {
        ArgumentNullException.ThrowIfNull(logPath);
        var line = (stopped, exitCode) switch
        {
            (true, { } code) => ExitLines.Stopped(code),
            (true, null) => ExitLines.Stopped(-1),
            (false, { } code) => ExitLines.Exited(code),
            (false, null) => ExitLines.Unknown(),
        };
        // … rest unchanged
```

`Diagnose` already treats a null code as "log rules only, never unexplained": keep it. `DiagnosisTexts.Code` and `CrashReporter` line 85 use `ExitCodes.Describe(value)` instead of `0x{value:X8}`:

```csharp
    private static string Code(uint? code) => code is { } value ? ExitCodes.Describe(value) : "unknown";
```

```csharp
        text.AppendLine(CultureInfo.InvariantCulture, $"Exit code: {(exitCode is { } code ? ExitCodes.Describe(code) : "unknown")}");
```

Tests in `DiagnosisTextsTests` and `CrashReporterTests` that expect `0x…` text and run on Linux CI: make them pass on both OSes by building the expected text with `ExitCodes.Describe(code)`.

- [ ] **Step 4: Run the Core tests**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests -c Release`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Core/Diagnostics tests/PS5Kitsune.Core.Tests/Diagnostics
git commit -m "Describe exit codes per OS and write an exit line for an unknown code"
```

---

### Task 2: An unknown exit code from the launcher to the screens

**Files:**
- Modify: `src/PS5Kitsune.Core/Abstractions/IGameLauncher.cs:34` (`Task<int?> WaitForExitAsync`)
- Modify: `src/PS5Kitsune.Platform.Windows/WindowsGameLauncher.cs:364`
- Modify: `src/PS5Kitsune.Platform.Linux/LinuxGameLauncher.cs` (`SpawnedGame`, `AttachedGame`)
- Modify: `src/PS5Kitsune.Cli/RunCommand.cs:42-58`, `src/PS5Kitsune.Cli/CliLog.cs:58-59`
- Modify: `src/PS5Kitsune.App/ViewModels/GameViewModel.cs:110,276-277`
- Modify: `tests/PS5Kitsune.Core.Tests/Fakes/FakeGameLauncher.cs` (`FakeLaunchedGame`), `tests/PS5Kitsune.Platform.Linux.Tests/LinuxGameLauncherTests.cs` (`ExitOf`, the Attach test)
- Test: `tests/PS5Kitsune.App.Tests/GameViewModelTests.cs`

**Interfaces:**
- Consumes: `CrashDiagnoser.FinishAsync(string, int?, bool, CancellationToken)`, `ExitCodes.Describe(uint)` (Task 1).
- Produces: `ILaunchedGame.WaitForExitAsync` returns `Task<int?>`; `FakeLaunchedGame.Exit(int? exitCode)`; `CliLog.GameExited(this ILogger, string name, string exitCode)` and `CliLog.GameExitedUnknown(this ILogger, string name)`.

- [ ] **Step 1: Write the failing tests**

In `GameViewModelTests.cs`, next to the existing test that checks `"Exited with code …"`, add (reuse that test's setup to open the page and launch through `FakeGameLauncher`):

```csharp
    [Fact]
    public async Task A_game_whose_exit_code_is_unknown_shows_exited_without_a_code()
    {
        // Same arrangement as the "Exited with code" test above: open the page, let the fake launch start.
        var (page, process) = await OpenAndLaunchAsync();

        process.Exit(null);

        await WaitUntil(() => !page.IsRunning);
        Assert.Equal("Exited", page.StatusText);
    }
```

If the file has no `OpenAndLaunchAsync`/`WaitUntil` helpers, write the arrangement inline exactly as the neighbouring exit-code test does.

In `LinuxGameLauncherTests.cs`, `ExitOf` returns `Task<int?>`, and `Attach_finds_a_running_game_and_follows_it_until_it_exits` asserts `Assert.Null(await ExitOf(attached));`.

- [ ] **Step 2: Run to see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: compile errors (`Exit(null)`, `Assert.Null` on `int`).

- [ ] **Step 3: Implement**

- `IGameLauncher.cs`: `/// <summary>Completes with the exit code when the game exits; null when the code cannot be read (a game found again on Linux).</summary> Task<int?> WaitForExitAsync(CancellationToken cancellationToken = default);`
- Windows: `public async Task<int?> WaitForExitAsync(...)`; body unchanged (it returns an `int`). Its own `StopAsync` awaits it: unchanged.
- Linux: `SpawnedGame`'s field becomes `TaskCompletionSource<int?>` (still set with `TrySetResult(PosixSpawn.WaitForExit(processId))`), and `WaitForExitAsync` returns `Task<int?>`. `AttachedGame.WaitForExitAsync` returns `null` instead of `-1`; update its summary ("its exit code is unknown (null)").
- `FakeLaunchedGame`: `TaskCompletionSource<int?>`, `public void Exit(int? exitCode)`, `Task<int?> WaitForExitAsync`.
- `RunCommand.cs`:

```csharp
                var exitCode = await exit;
                player.Forget(playing);
                if (exitCode is { } code)
                {
                    logger.GameExited(record.Name, ExitCodes.Describe((uint)code));
                }
                else
                {
                    logger.GameExitedUnknown(record.Name);
                }

                var diagnosis = await services.GetRequiredService<CrashDiagnoser>().FinishAsync(launched.LogFile, exitCode, stopped: false, CancellationToken.None);
                // … unchanged
                return exitCode ?? 0;
```

- `CliLog.cs`:

```csharp
    [LoggerMessage(Level = LogLevel.Information, Message = "{Name} exited with code {ExitCode}.")]
    public static partial void GameExited(this ILogger logger, string name, string exitCode);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Name} exited.")]
    public static partial void GameExitedUnknown(this ILogger logger, string name);
```

- `GameViewModel.cs` line 277:

```csharp
            StatusText = exitCode is { } code ? $"Exited with code {ExitCodes.Describe((uint)code)}" : "Exited";
```

and the summary on line 110: `"Exited with code n", "Exited" (code unknown)`. Existing App tests that expect `"Exited with code 0"` keep passing on Windows only if `Describe(0)` gives `0x00000000`: update those expectations to `$"Exited with code {ExitCodes.Describe(0)}"` so they pass on both OSes.

- `GameSettingsViewModel.cs:252` only awaits the task: compiles unchanged.

- [ ] **Step 4: Run all tests**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: all pass. Then the same command in WSL (see Global Constraints): all pass.

- [ ] **Step 5: Commit**

```bash
git add -A src tests
git commit -m "Carry an unknown exit code from the launcher to run and the Game page"
```

---

### Task 3: A running game recognised by boot ID and start ticks

**Files:**
- Modify: `src/PS5Kitsune.Core/Abstractions/IGameLauncher.cs` (`RunningGame`, `ILaunchedGame`)
- Modify: `src/PS5Kitsune.Core/Library/RunningGames.cs` (`RecordAsync`, `Read`)
- Modify: `src/PS5Kitsune.Platform.Linux/ProcStat.cs`, `src/PS5Kitsune.Platform.Linux/LinuxGameLauncher.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Library/RunningGamesTests.cs`, `tests/PS5Kitsune.Platform.Linux.Tests/LinuxGameLauncherTests.cs`, `tests/PS5Kitsune.Platform.Linux.Tests/ProcStatTests.cs`

**Interfaces:**
- Produces:
  - `RunningGame` with `CurrentSchemaVersion = 2` and `public string? BootId { get; init; }`, `public ulong? StartTicks { get; init; }`.
  - `ILaunchedGame`: `string? BootId => null;` and `ulong? StartTicks => null;` (default interface members; the Linux games override them).
  - `ProcStat.BootId()` (`string?`, the trimmed `/proc/sys/kernel/random/boot_id`, null when unreadable).

- [ ] **Step 1: Write the failing tests**

`RunningGamesTests.cs`:

```csharp
    [Fact]
    public async Task The_record_keeps_the_boot_identity_of_the_process()
    {
        _launcher.Identity = ("b7c1d0b4-0000-4000-8000-000000000001", 123_456UL);
        await LaunchAsync();

        var found = _running.Check(_game);

        Assert.Equal(2, found?.SchemaVersion);
        Assert.Equal("b7c1d0b4-0000-4000-8000-000000000001", found?.BootId);
        Assert.Equal(123_456UL, found?.StartTicks);
    }

    [Fact]
    public async Task A_schema_1_record_is_still_read()
    {
        var process = await LaunchAsync();
        var json = await File.ReadAllTextAsync(_game.RunningRecord, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(_game.RunningRecord, json.Replace("\"schemaVersion\": 2", "\"schemaVersion\": 1", StringComparison.Ordinal), TestContext.Current.CancellationToken);

        Assert.Same(process, _running.Attach(_game));
    }
```

`FakeGameLauncher` gets `public (string BootId, ulong StartTicks)? Identity { get; set; }`, passed to each `FakeLaunchedGame` it creates, which overrides `BootId` and `StartTicks` from it. Check how `JsonFiles` names properties (camelCase is assumed above: confirm by reading an existing `running.json` test or `JsonFiles` options, and adjust the `Replace` string to the exact written form).

`LinuxGameLauncherTests.cs` — `RecordOf` copies the identity:

```csharp
    private static RunningGame RecordOf(ILaunchedGame game, string executable) =>
        new(RunningGame.CurrentSchemaVersion, game.ProcessId, game.StartedAt, executable, game.LogFile) { BootId = game.BootId, StartTicks = game.StartTicks };

    [Fact]
    public async Task A_launched_game_carries_its_boot_identity()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        using var game = Sleep(30);
        try
        {
            Assert.Equal((await File.ReadAllTextAsync("/proc/sys/kernel/random/boot_id", TestContext.Current.CancellationToken)).Trim(), game.BootId);
            Assert.NotNull(game.StartTicks);
        }
        finally
        {
            await game.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Attach_with_a_boot_identity_ignores_the_wall_clock()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        using var game = Sleep(30);
        try
        {
            // A clock stepped by a minute after the launch: the identity still matches.
            using var attached = _launcher.Attach(RecordOf(game, "/usr/bin/sleep") with { StartedAt = game.StartedAt.AddMinutes(1) });
            Assert.NotNull(attached);

            Assert.Null(_launcher.Attach(RecordOf(game, "/usr/bin/sleep") with { BootId = "00000000-0000-0000-0000-000000000000" }));
            Assert.Null(_launcher.Attach(RecordOf(game, "/usr/bin/sleep") with { StartTicks = game.StartTicks + 1 }));
        }
        finally
        {
            await game.StopAsync(TestContext.Current.CancellationToken);
        }
    }
```

`Attach_refuses_another_start_time_or_executable` keeps testing the 8a check: build its record without the identity (`with { BootId = null, StartTicks = null, StartedAt = … }`).

- [ ] **Step 2: Run to see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release` — expected compile errors (`BootId`, `Identity` missing).

- [ ] **Step 3: Implement**

`IGameLauncher.cs`:

```csharp
/// <summary>A launched game, as recorded in .ps5kitsune\running.json. See docs/architecture.md, "Launching".</summary>
/// <param name="StartedAt">The process creation time read from the OS, in UTC: with the PID, it tells the game from a later process reusing the PID.</param>
/// <param name="Executable">The full path of the game's executable.</param>
public sealed record RunningGame(int SchemaVersion, int ProcessId, DateTimeOffset StartedAt, string Executable, string LogFile)
{
    public const int CurrentSchemaVersion = 2;

    /// <summary>Linux: /proc/sys/kernel/random/boot_id when the game started; null on Windows and in schema 1.</summary>
    public string? BootId { get; init; }

    /// <summary>Linux: the game's start time in clock ticks since boot (/proc/&lt;pid&gt;/stat field 22); null on Windows and in schema 1.</summary>
    public ulong? StartTicks { get; init; }
}
```

and in `ILaunchedGame`:

```csharp
    /// <summary>Linux: the boot the game started in; null elsewhere or when unreadable. Recorded with <see cref="StartTicks"/> so a clock change cannot hide the game.</summary>
    string? BootId => null;

    /// <summary>Linux: the game's start in clock ticks since boot; null elsewhere or when unreadable.</summary>
    ulong? StartTicks => null;
```

`RunningGames.RecordAsync`: `new RunningGame(…) { BootId = process.BootId, StartTicks = process.StartTicks }`. `Read`: accept `SchemaVersion is 1 or RunningGame.CurrentSchemaVersion`; the message becomes `$"Schema version {record.SchemaVersion}; this app reads versions 1 and {RunningGame.CurrentSchemaVersion}."`.

`ProcStat.cs`:

```csharp
    /// <summary>This boot's ID; null when it cannot be read. Unlike btime, it never moves with the wall clock.</summary>
    public static string? BootId()
    {
        try
        {
            return File.ReadAllText("/proc/sys/kernel/random/boot_id").Trim() is { Length: > 0 } id ? id : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
```

`LinuxGameLauncher.cs`:
- `Launch`: read `var stat = ProcStat.Read(pid);` and pass `BootId = ProcStat.BootId()` and `StartTicks = stat?.StartTicks` to `SpawnedGame` (new constructor parameters `string? bootId, ulong? startTicks`, exposed as `BootId`/`StartTicks` properties).
- `AttachedGame` exposes `record.BootId` and `record.StartTicks`.
- `IsRecorded(record)`: the start check becomes

```csharp
                && SameStart(record)
```

with

```csharp
    // A record with the boot identity is compared exactly; an older one (or one whose identity could not be read) by start time within the tolerance.
    private static bool SameStart(RunningGame record) =>
        record is { BootId: { } bootId, StartTicks: { } ticks }
            ? ProcStat.BootId() == bootId && ProcStat.Read(record.ProcessId)?.StartTicks == ticks
            : ProcStat.StartTimeOf(record.ProcessId) is { } started && ProcStat.SameStart(started, record.StartedAt);
```

- [ ] **Step 4: Run tests on Windows and in WSL**

Run both commands from Global Constraints. Expected: all pass; the two new Linux tests run in WSL.

- [ ] **Step 5: Commit**

```bash
git add -A src tests
git commit -m "Recognise a running game on Linux by its boot ID and start ticks"
```

---

### Task 4: Stop ends the whole process group on Linux

**Files:**
- Modify: `src/PS5Kitsune.Platform.Linux/PosixSpawn.cs` (add `WaitForExitWithoutReaping`)
- Modify: `src/PS5Kitsune.Platform.Linux/LinuxGameLauncher.cs` (`SpawnedGame`)
- Test: `tests/PS5Kitsune.Platform.Linux.Tests/LinuxGameLauncherTests.cs`

**Interfaces:**
- Produces: `PosixSpawn.WaitForExitWithoutReaping(int pid)` → `bool` (true once the child has exited and is still unreaped; false if `waitid` failed for a reason other than EINTR).

- [ ] **Step 1: Write the failing tests**

```csharp
    private static int ChildPid(string log) =>
        int.Parse(log.Split('\n').First(line => line.StartsWith("child ", StringComparison.Ordinal))["child ".Length..].Trim(), System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<bool> GoneAsync(int pid)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (ProcStat.Read(pid) is null or { State: 'Z' })
            {
                return true;
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        return false;
    }

    [Fact]
    public async Task Stop_kills_a_child_that_outlives_the_leader()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        // The leader exits on SIGTERM; its child ignores it (an ignored signal stays ignored across exec).
        using var game = Shell("(trap '' TERM; echo ready; exec sleep 30) & echo \"child $!\"; wait");
        await LogContains(game, "ready");
        await LogContains(game, "child ");
        var child = ChildPid(await File.ReadAllTextAsync(game.LogFile, TestContext.Current.CancellationToken));

        await game.StopAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.True(await GoneAsync(child), "The child survived Stop.");
    }

    [Fact]
    public async Task A_game_that_exits_on_its_own_leaves_its_group_alone()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux only.");
        using var game = Shell("(trap '' TERM; echo ready; exec sleep 30) & echo \"child $!\"");
        await LogContains(game, "ready");
        await LogContains(game, "child ");
        var child = ChildPid(await File.ReadAllTextAsync(game.LogFile, TestContext.Current.CancellationToken));
        try
        {
            Assert.Equal(0, await ExitOf(game));

            Assert.Equal('S', ProcStat.Read(child)?.State);
        }
        finally
        {
            using var leftover = System.Diagnostics.Process.GetProcessById(child);
            leftover.Kill();
        }
    }
```

`ProcStat` is `internal`: the Linux test project already sees it if `InternalsVisibleTo` is set (it is used by `ProcStatTests`).

- [ ] **Step 2: Run in WSL to see the first test fail**

Run: `wsl -e bash -lc 'source ~/ps5k-env.sh && cd /mnt/c/Users/kilia/source/repos/PS5Kitsune && dotnet test --project tests/PS5Kitsune.Platform.Linux.Tests -c Release --filter-method "*Stop_kills_a_child_that_outlives_the_leader"'`
Expected: FAIL "The child survived Stop."

- [ ] **Step 3: Implement**

`PosixSpawn.cs`:

```csharp
    private const int IdTypePid = 1;            // P_PID
    private const int WaitExited = 4;           // WEXITED
    private const int WaitNoWait = 0x01000000;  // WNOWAIT
    private const int SigInfoSize = 128;        // sizeof(siginfo_t)

    /// <summary>
    /// Waits until the child exits without reaping it: as a zombie it keeps its PID, and so its process group ID, from being reused,
    /// so the group can still be signalled safely. False when waitid fails (the caller then reaps as before).
    /// </summary>
    public static unsafe bool WaitForExitWithoutReaping(int pid)
    {
        var info = stackalloc byte[SigInfoSize];
        int result;
        while ((result = WaitId(IdTypePid, pid, (nint)info, WaitExited | WaitNoWait)) == -1 && Marshal.GetLastPInvokeError() == Interrupted)
        {
        }

        return result == 0;
    }

    [LibraryImport("libc", EntryPoint = "waitid", SetLastError = true)]
    private static partial int WaitId(int idType, int id, nint info, int options);
```

(If the project does not allow `unsafe`, allocate with `NativeMemory.AllocZeroed(SigInfoSize)` and free it in a `finally`, as `Start` does for its sigset.)

`LinuxGameLauncher.SpawnedGame`:

```csharp
        private volatile bool _stopRequested;

        // In the constructor, the thread becomes:
        new Thread(() => _exited.TrySetResult(WaitAndReap(processId))) { IsBackground = true, Name = "Game exit" }.Start();

        // The leader is seen exiting before it is reaped: after a Stop, its whole group is killed while the group ID cannot be reused.
        private int WaitAndReap(int processId)
        {
            if (PosixSpawn.WaitForExitWithoutReaping(processId) && _stopRequested)
            {
                PosixSpawn.SignalGroup(processId, PosixSpawn.SigKill);
            }

            return PosixSpawn.WaitForExit(processId);
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (_exited.Task.IsCompleted)
            {
                return _exited.Task;
            }

            _stopRequested = true;
            return LinuxGameLauncher.StopAsync(this, _stopGrace, cancellationToken);
        }
```

`SignalGroup` throws `Win32Exception` on errors other than ESRCH: on the waiting thread, catch it and log nothing (the reap must still happen): wrap the call in `try { … } catch (Win32Exception) { }` with a comment saying the reap must not be skipped.

- [ ] **Step 4: Run the Linux tests in WSL, then everything on Windows**

Run: the WSL test command for `tests/PS5Kitsune.Platform.Linux.Tests` (all pass), then `dotnet test --solution PS5Kitsune.slnx -c Release` on Windows.

- [ ] **Step 5: Commit**

```bash
git add -A src/PS5Kitsune.Platform.Linux tests/PS5Kitsune.Platform.Linux.Tests
git commit -m "Kill the whole process group when a stopped game's leader exits first"
```

---

### Task 5: The build card and the Dozen check on Linux

**Files:**
- Modify: `src/PS5Kitsune.Core/AnyPS5/IAnyPS5Installer.cs`, `src/PS5Kitsune.Core/AnyPS5/AnyPS5Installer.cs:33-38`
- Modify: `src/PS5Kitsune.App/ViewModels/ModeViewModel.cs:54`
- Modify: `src/PS5Kitsune.Core/Checks/SystemFacts.cs`, `src/PS5Kitsune.Core/Checks/SystemChecker.cs:13-27`, `src/PS5Kitsune.Core/Checks/VulkanChecks.cs:70-90`
- Modify: `tests/PS5Kitsune.App.Tests/Fakes.cs` (`FakeInstaller`)
- Test: `tests/PS5Kitsune.Core.Tests/AnyPS5/AnyPS5InstallerTests.cs` (or the existing installer test file), `tests/PS5Kitsune.Core.Tests/Checks/VulkanChecksTests.cs`, `tests/PS5Kitsune.App.Tests/ModeViewModelTests.cs` (or wherever Mode is tested)

**Interfaces:**
- Produces: `IAnyPS5Installer.BuildUnavailableReason` (`string`); `AnyPS5Installer.LinuxBuildUnavailable` (`public const string`, "Build mode is not available on Linux yet."); `SystemFacts.Os` (`string`, default `ProfileOs.Windows`).

- [ ] **Step 1: Write the failing tests**

Installer (use the test file's existing way of building an installer from a profile; `ProfileLoader.LoadEmbedded(ProfileOs.Linux)` gives the Linux profile):

```csharp
    [Fact]
    public void The_build_reason_names_linux_on_linux()
    {
        var installer = CreateInstaller(ProfileLoader.LoadEmbedded(ProfileOs.Linux));

        Assert.Equal("Build mode is not available on Linux yet.", installer.BuildUnavailableReason);
    }

    [Fact]
    public void The_build_reason_on_windows_names_the_profile()
    {
        var installer = CreateInstaller(ProfileLoader.LoadEmbedded(ProfileOs.Windows));

        Assert.Equal("This profile does not define a build.", installer.BuildUnavailableReason);
    }
```

`VulkanChecksTests.cs`:

```csharp
    [Fact]
    public void Dozen_is_not_applicable_on_linux()
    {
        var result = new DozenCheck().Evaluate(TestFacts.Healthy() with { Os = ProfileOs.Linux });

        Assert.Equal(CheckStatus.Info, result.Status);
        Assert.Equal("Not applicable on this system.", result.Message);
    }
```

(Use the `CheckResult` property that holds the text; it is the one `CommitLimitCheck`'s tests assert on.)

Mode page:

```csharp
    [Fact]
    public void The_build_card_shows_the_installer_reason()
    {
        var page = new ModeViewModel(profile, new FakeInstaller { BuildUnavailableReason = "Build mode is not available on Linux yet." }, new Navigator(), new FakePages());

        Assert.Equal("Build mode is not available on Linux yet.", page.BuildUnavailableText);
    }
```

- [ ] **Step 2: Run to see them fail** — `dotnet build PS5Kitsune.slnx -c Release`: compile errors.

- [ ] **Step 3: Implement**

`IAnyPS5Installer`: `/// <summary>Why build mode cannot be installed here, for the Mode page's build card.</summary> string BuildUnavailableReason { get; }`

`AnyPS5Installer`:

```csharp
    public const string LinuxBuildUnavailable = "Build mode is not available on Linux yet.";

    public string BuildUnavailableReason => profile?.Os == ProfileOs.Linux ? LinuxBuildUnavailable : "This profile does not define a build.";

    internal string UnavailableMessage(AnyPS5Mode mode) => mode switch
    {
        AnyPS5Mode.Build when profile?.Os == ProfileOs.Linux => $"{LinuxBuildUnavailable[..^1]}; use release mode.",
        // … unchanged
    };
```

`ModeViewModel`: `BuildUnavailableText = installer.BuildUnavailableReason;` (assigned in the constructor; the property becomes `{ get; }` without initializer). `FakeInstaller`: `public string BuildUnavailableReason { get; init; } = "This profile does not define a build.";`

`SystemFacts`: `/// <summary>The <see cref="Profiles.ProfileOs"/> value of the OS the checks run on.</summary> public string Os { get; init; } = ProfileOs.Windows;` — `SystemFactsCollector` sets `Os = platform.Os`. `DozenCheck.Evaluate` starts with:

```csharp
        if (facts.Os == ProfileOs.Linux)
        {
            // Dozen is Mesa's Vulkan over Direct3D 12: Windows only.
            return Result(CheckStatus.Info, "Not applicable on this system.");
        }
```

"Disable Dozen" on the Settings page is already hidden on Linux: `ShowDisableDozen` needs a Dozen GPU, which Linux never lists. No change.

- [ ] **Step 4: Run all tests** (Windows; WSL for the Linux profile paths). Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add -A src tests
git commit -m "Say why build mode is unavailable on Linux and show Dozen as not applicable there"
```

---

### Task 6: Lavapipe texts from the platform

**Files:**
- Modify: `src/PS5Kitsune.Core/Launch/ILavapipe.cs`, `src/PS5Kitsune.Core/Tools/MesaTool.cs`, `src/PS5Kitsune.Platform.Linux/SystemLavapipe.cs`
- Modify: `src/PS5Kitsune.Core/Launch/LaunchPreparation.cs`, `src/PS5Kitsune.Core/Library/GamePlayer.cs:68-70`
- Modify: `src/PS5Kitsune.Core/Launch/GameSettingsReport.cs`, `src/PS5Kitsune.Cli/SettingsCommand.cs:18-20`
- Modify: `src/PS5Kitsune.App/ViewModels/GameSettingsViewModel.cs:66`, `src/PS5Kitsune.App/ViewModels/DiagnosisViewModel.cs:77-79`
- Modify: `tests/PS5Kitsune.Core.Tests/Fakes/FakeLaunchTools.cs` (`FakeLavapipe`)
- Test: `tests/PS5Kitsune.Platform.Linux.Tests/SystemLavapipeTests.cs`, `tests/PS5Kitsune.Core.Tests/Launch/GameSettingsReportTests.cs`, `tests/PS5Kitsune.App.Tests/GameSettingsViewModelTests.cs`

**Interfaces:**
- Produces:
  - `ILavapipe.HowToGet` (`string`, a full sentence) and `ILavapipe.WhenMissing` (`string`, lower-case phrase to follow "Lavapipe is not installed: ").
  - `LaunchPreparation.LavapipeHowToGet` / `LavapipeWhenMissing` (`string?`, null without Lavapipe); `GamePlayer.LavapipeHowToGet` (`string?`).
  - `GameSettingsReport.Lines(GameSettings, string? lavapipeManifest, bool lavapipeInstalled, string? lavapipeWhenMissing, string? fontsDirectory, bool fontsInstalled)`.

Texts:

| | `HowToGet` | `WhenMissing` |
|---|---|---|
| Windows (`MesaTool`) | `Mesa (~70 MB) is downloaded on the first launch.` | `launch the game again to download it` |
| Linux (`SystemLavapipe`) | `It comes from the system's Mesa; if it is missing, install it with: {command}` | `install it with: {command}` |

`{command}` is `packages.CommandFor(LinuxPackage.MesaVulkanDrivers)`.

- [ ] **Step 1: Write the failing tests**

`SystemLavapipeTests.cs` (use the file's existing `DistroPackages` for Ubuntu):

```csharp
    [Fact]
    public void The_texts_give_the_install_command()
    {
        var lavapipe = new SystemLavapipe(UbuntuPackages);

        Assert.Equal("It comes from the system's Mesa; if it is missing, install it with: sudo apt install mesa-vulkan-drivers", lavapipe.HowToGet);
        Assert.Equal("install it with: sudo apt install mesa-vulkan-drivers", lavapipe.WhenMissing);
    }
```

(Copy the exact command the existing `DistroPackagesTests` expect for `MesaVulkanDrivers` on Ubuntu.)

`GameSettingsReportTests.cs`: the pending Lavapipe variable now ends with the note:

```csharp
    [Fact]
    public void A_missing_lavapipe_says_what_to_do()
    {
        var lines = GameSettingsReport.Lines(new GameSettings { Lavapipe = true }, "/usr/share/vulkan/icd.d/lvp_icd.x86_64.json", lavapipeInstalled: false, "install it with: sudo apt install mesa-vulkan-drivers", null, false);

        Assert.Contains("  VK_DRIVER_FILES=/usr/share/vulkan/icd.d/lvp_icd.x86_64.json (not installed yet: install it with: sudo apt install mesa-vulkan-drivers)", lines);
    }
```

Update the existing report tests' calls with the new parameter (`"launch the game again to download it"`), and their expected Lavapipe suffix to ` (not installed yet: launch the game again to download it)`. The fonts suffix stays ` (downloaded on first launch)`.

`GameSettingsViewModelTests.cs`:

```csharp
    [Fact]
    public void The_lavapipe_help_comes_from_the_platform()
    {
        var page = CreatePage();

        Assert.Equal("Slow but works on any PC. " + FakeLavapipe.HowToGetText, page.LavapipeHelp);
    }
```

with `FakeLavapipe.HowToGetText = "Mesa is fetched by the test."` as a `public const string` on the fake, and `WhenMissing => "launch the test again"`.

- [ ] **Step 2: Run to see them fail** — build errors.

- [ ] **Step 3: Implement**

- `ILavapipe`: add the two members with XML docs; update the interface summary to "Mesa's Lavapipe (CPU rendering): downloaded on Windows, the system's Mesa on Linux."
- `MesaTool`: `public string HowToGet => "Mesa (~70 MB) is downloaded on the first launch.";` and `public string WhenMissing => "launch the game again to download it";`
- `SystemLavapipe`: as in the table, using `packages.CommandFor(LinuxPackage.MesaVulkanDrivers)`; its `InstallAsync` message becomes `$"Lavapipe is not installed: {WhenMissing}"`.
- `LaunchPreparation`: `public string? LavapipeHowToGet => lavapipe?.HowToGet;` and `public string? LavapipeWhenMissing => lavapipe?.WhenMissing;`
- `GamePlayer`: `public string? LavapipeHowToGet => preparation.LavapipeHowToGet;` and the throw at line 70 becomes `throw new LavapipeUnavailableException($"Lavapipe is not installed: {preparation.LavapipeWhenMissing ?? "this profile has no Mesa download"}.")`.
- `GameSettingsReport.Lines`: new `string? lavapipeWhenMissing` parameter after `lavapipeInstalled`; the pending suffix for `DriverFiles` is `$" (not installed yet: {lavapipeWhenMissing})"` when it is not null, else the old `FirstLaunch`.
- `SettingsCommand`: pass `lavapipe?.WhenMissing`.
- `GameSettingsViewModel`: `public string LavapipeHelp => player.LavapipeHowToGet is { } how ? $"Slow but works on any PC. {how}" : "Slow but works on any PC.";`
- `DiagnosisViewModel` (`TurnOnLavapipe`):

```csharp
            case DiagnosisAction.TurnOnLavapipe:
                return await ChangeSettingsAsync(
                    settings => settings with { Lavapipe = true },
                    _player.LavapipeHowToGet is { } how ? $"CPU rendering is on. {how} Launch again to start the game." : "CPU rendering is on. Launch again to start the game.").ConfigureAwait(true);
```

- [ ] **Step 4: Run all tests** on Windows and in WSL. Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add -A src tests
git commit -m "Take the Lavapipe texts from the platform: a download on Windows, the system's Mesa on Linux"
```

---

### Task 7: The AppImage and the CI artifacts

**Files:**
- Create: `packaging/linux/build-appimage.sh` (executable: `git update-index --chmod=+x`)
- Create: `packaging/linux/AppRun`, `packaging/linux/ps5kitsune.desktop`, `packaging/linux/ps5kitsune.png`
- Modify: `src/PS5Kitsune.App/Program.cs` (X11 window class)
- Modify: `.github/workflows/ci.yml` (Ubuntu and Windows publish steps)
- Modify: `README.md` (Linux app section)

- [ ] **Step 1: Extract the icon**

The 256×256 entry of `kitsune.ico` is a PNG; copy it out with the standard library:

```bash
python3 - <<'EOF'
import struct
data = open('src/PS5Kitsune.App/Assets/kitsune.ico', 'rb').read()
count = struct.unpack('<H', data[4:6])[0]
for index in range(count):
    width, _, _, _, _, _, size, offset = struct.unpack('<BBBBHHII', data[6 + 16 * index:22 + 16 * index])
    if width == 0:  # 0 means 256
        open('packaging/linux/ps5kitsune.png', 'wb').write(data[offset:offset + size])
EOF
```

Check: `file packaging/linux/ps5kitsune.png` says `PNG image data, 256 x 256`.

- [ ] **Step 2: Write `AppRun` and the desktop entry**

`packaging/linux/AppRun`:

```sh
#!/bin/sh
# PS5Kitsune AppImage entry point: no argument opens the app, any argument runs the command-line tool.
HERE=$(dirname "$(readlink -f "$0")")
if [ "$#" -eq 0 ]; then
    exec "$HERE/usr/bin/PS5Kitsune"
fi
exec "$HERE/usr/bin/ps5kitsune" "$@"
```

`packaging/linux/ps5kitsune.desktop`:

```ini
[Desktop Entry]
Type=Application
Name=PS5Kitsune
Comment=Installer and launcher for AnyPS5
Exec=PS5Kitsune
Icon=ps5kitsune
Categories=Game;Utility;
StartupWMClass=PS5Kitsune
Terminal=false
```

- [ ] **Step 3: Set the window class**

`Program.BuildAvaloniaApp`, after `.UsePlatformDetect()`:

```csharp
            // Matches StartupWMClass in packaging/linux/ps5kitsune.desktop, so the dock shows the app's icon. Ignored on Windows.
            .With(new X11PlatformOptions { WmClass = "PS5Kitsune" })
```

If `X11PlatformOptions.WmClass` is not public in Avalonia 12.1.4 (the build fails), remove the call and check in the WSL rehearsal (Task 8) what `xprop WM_CLASS` reports for the window; set `StartupWMClass` to that value.

- [ ] **Step 4: Write `build-appimage.sh`**

```sh
#!/bin/sh
# Builds PS5Kitsune-<version>-x86_64.AppImage: the app and the CLI published self-contained into one folder. See the 8b spec, "Packaging".
set -eu

version=${1:?usage: build-appimage.sh <version>}
repo=$(cd "$(dirname "$0")/../.." && pwd)
out="$repo/artifacts/appimage"
appdir="$out/AppDir"
tools="$out/tools"

tool_url=https://github.com/AppImage/appimagetool/releases/download/1.9.1/appimagetool-x86_64.AppImage
tool_sha=ed4ce84f0d9caff66f50bcca6ff6f35aae54ce8135408b3fa33abfc3cb384eb0
runtime_url=https://github.com/AppImage/type2-runtime/releases/download/20251108/runtime-x86_64
runtime_sha=2fca8b443c92510f1483a883f60061ad09b46b978b2631c807cd873a47ec260d

fetch() { # url sha256 file
    if [ ! -f "$3" ] || ! echo "$2  $3" | sha256sum -c --status; then
        curl -fsSL --retry 3 -o "$3" "$1"
        echo "$2  $3" | sha256sum -c --quiet || { echo "Hash mismatch for $1" >&2; exit 1; }
    fi
}

rm -rf "$appdir" "$out/app" "$out/cli"
mkdir -p "$appdir/usr/bin" "$tools"

for project in App Cli; do
    dotnet publish "$repo/src/PS5Kitsune.$project" -c Release -r linux-x64 --self-contained \
        -p:Version="$version" -o "$out/$(echo "$project" | tr '[:upper:]' '[:lower:]')"
done

# The two outputs share the runtime and every common library: a file in both must be identical.
(cd "$out/app" && find . -type f) | while read -r file; do
    if [ -f "$out/cli/$file" ] && ! cmp -s "$out/app/$file" "$out/cli/$file"; then
        echo "The app and the CLI publish different $file" >&2
        exit 1
    fi
done || exit 1
cp -a "$out/app/." "$out/cli/." "$appdir/usr/bin/"

cp "$repo/packaging/linux/AppRun" "$appdir/AppRun"
chmod +x "$appdir/AppRun"
cp "$repo/packaging/linux/ps5kitsune.desktop" "$repo/packaging/linux/ps5kitsune.png" "$appdir/"

fetch "$tool_url" "$tool_sha" "$tools/appimagetool"
fetch "$runtime_url" "$runtime_sha" "$tools/runtime-x86_64"
chmod +x "$tools/appimagetool"

# No FUSE on CI runners or in WSL: appimagetool is itself an AppImage.
APPIMAGE_EXTRACT_AND_RUN=1 ARCH=x86_64 "$tools/appimagetool" --no-appstream \
    --runtime-file "$tools/runtime-x86_64" "$appdir" "$out/PS5Kitsune-$version-x86_64.AppImage"
echo "$out/PS5Kitsune-$version-x86_64.AppImage"
```

`exit 1` inside the piped `while` only leaves its subshell; `done || exit 1` stops the script.

- [ ] **Step 5: Build it in WSL and check it**

```bash
wsl -e bash -lc 'source ~/ps5k-env.sh && cd /mnt/c/Users/kilia/source/repos/PS5Kitsune && sh packaging/linux/build-appimage.sh 0.0.0-local && ./artifacts/appimage/PS5Kitsune-0.0.0-local-x86_64.AppImage --appimage-extract-and-run --version'
```

Expected: the last line starts with `0.0.0-local`. Then `--appimage-extract-and-run list` prints the WSL games list (or "No game added").

- [ ] **Step 6: CI**

In `.github/workflows/ci.yml`, replace the "Publish the Linux CLI" step with:

```yaml
      # What Linux ships: the app and the CLI in one AppImage. A Windows-only dependency fails here.
      - name: Build the AppImage
        if: runner.os == 'Linux'
        run: |
          version="0.0.0-ci.${{ github.run_number }}"
          sh packaging/linux/build-appimage.sh "$version"
          image="artifacts/appimage/PS5Kitsune-$version-x86_64.AppImage"
          "./$image" --appimage-extract-and-run --version | grep -F "$version"
          cd "$RUNNER_TEMP" && "$GITHUB_WORKSPACE/$image" --appimage-extract > /dev/null
          test -x squashfs-root/usr/bin/PS5Kitsune && test -x squashfs-root/usr/bin/ps5kitsune
      - name: Upload the AppImage
        if: runner.os == 'Linux'
        uses: actions/upload-artifact@v4
        with:
          name: PS5Kitsune-linux-x64-AppImage
          path: artifacts/appimage/*.AppImage
      - name: Publish the Windows app
        if: runner.os == 'Windows'
        run: dotnet publish src/PS5Kitsune.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=0.0.0-ci.${{ github.run_number }} -o artifacts/windows
      - name: Upload the Windows app
        if: runner.os == 'Windows'
        uses: actions/upload-artifact@v4
        with:
          name: PS5Kitsune-win-x64
          path: artifacts/windows/PS5Kitsune.exe
```

Use the `upload-artifact` major version the repository's other workflows use if any; otherwise `v4`. (`actions/checkout@v7` is already used, so recent majors are available.)

- [ ] **Step 7: README**

Add a "Linux" subsection under the install instructions:

```markdown
### Linux

Download `PS5Kitsune-linux-x64-AppImage` from the latest CI run (Actions → CI → Artifacts), unzip it, then:

    chmod +x PS5Kitsune-*-x86_64.AppImage
    ./PS5Kitsune-*-x86_64.AppImage            # the app
    ./PS5Kitsune-*-x86_64.AppImage check      # any argument runs the command-line tool

If it does not start because FUSE is missing, add `--appimage-extract-and-run` before the other arguments. To add it to your app menu, open it with Gear Lever or AppImageLauncher.
```

Keep the existing "run from source on Linux" commands.

- [ ] **Step 8: Run all tests, then commit**

`dotnet test --solution PS5Kitsune.slnx -c Release` (Windows) must pass.

```bash
git add packaging src/PS5Kitsune.App/Program.cs .github/workflows/ci.yml README.md
git update-index --chmod=+x packaging/linux/build-appimage.sh packaging/linux/AppRun
git commit -m "Package the app and the CLI as one AppImage, and publish both apps as CI artifacts"
```

---

### Task 8: Docs, TODO and the WSL rehearsal

**Files:**
- Modify: `docs/architecture.md` (roadmap 8b line, the stack's publishing row)
- Modify: `docs/superpowers/specs/2026-10-31-milestone-8b-linux-app-design.md` (the deviations listed at the top of this plan)
- Modify: `TODO.md`, `README.md` (status line)

- [ ] **Step 1: Architecture**

- Roadmap, 8b: `- **8b — The app on Linux.** The Avalonia app on Linux, shipped with the CLI as one AppImage built in CI; Linux exit codes, a clock-proof running-game identity and a process-group Stop. Spec: [milestone 8b](superpowers/specs/2026-10-31-milestone-8b-linux-app-design.md).` followed by `  - Criterion: the AppImage rehearsed in WSLg, then DOOM installed, added and played from the app on a real Ubuntu install.`
- Publishing row: add `Linux: an AppImage holding the app and the CLI (packaging/linux/build-appimage.sh)`.

- [ ] **Step 2: Spec deviations**

In the spec, change the sections to match the plan's "Deviations from the spec": the build card text from `IAnyPS5Installer.BuildUnavailableReason`; `ExitCodes.Describe` instead of `IPlatform.DescribeExitCode`; `SystemFacts.Os` for Dozen; `ILavapipe.HowToGet` and `WhenMissing`; CI version `0.0.0-ci.<run number>`.

- [ ] **Step 3: TODO.md**

Delete the sections "Exit code of a reattached game on Linux", "A clock-proof identity for a running game on Linux", "Lavapipe text on Linux", "Exit codes on Linux", "The Dozen check on Linux" and "Stop the whole process group on Linux". Keep the others.

- [ ] **Step 4: Commit**

```bash
git add docs TODO.md README.md
git commit -m "Document milestone 8b: the AppImage, Linux exit codes and the group Stop"
```

- [ ] **Step 5: WSL rehearsal (criterion step 1, the controller runs it, not a subagent)**

Build the AppImage in WSL (Task 7, Step 5), then start the app through WSLg: `./artifacts/appimage/PS5Kitsune-0.0.0-local-x86_64.AppImage --appimage-extract-and-run &`. Walk Home → Mode (build card: "Build mode is not available on Linux yet.") → Check → Install release v0.1.4 → add DOOM (`/mnt/c/PS5Games/PPSA99666.zip`), expecting green 357/0/0. Take screenshots of the window if possible (`import -window root` from ImageMagick in WSL, if installed). Through the AppImage, run `check` and `list`. Record the results in the ledger; report anything that fails before handing step 2 (real Ubuntu) to the user.
