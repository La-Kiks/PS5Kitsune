# Milestone 7c — Crash diagnosis Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** After a game exits, find the known AnyPS5 error in its log, explain it and offer its fix (Repair libs, Turn on CPU rendering, Reset a setting, page file settings, NVIDIA drivers, Report), from the Game page, `run` and a new `diagnose` command; fold in four PR #10 follow-ups.

**Architecture:** The profile gains a `diagnostics` section (rules: id, regexes, exit codes, match mode, fixed captures), validated and compiled by `DiagnosticRules`. `CrashDiagnoser` appends an exit line to the launch's log, then returns the first rule met, else `unexplained` for a non-zero exit. `DiagnosisTexts` (Core, shared by the app and the CLI) holds each id's title, explanation and action. The fixes are Core services (`LibsRepair`, `GamePlayer.UpdateSettingsAsync`, `CrashReporter`); the Game page shows a `DiagnosisViewModel` panel. `LaunchPreparation` serializes downloads with a file lock.

**Tech Stack:** .NET 10, C#, Avalonia 12.1.4 (compiled bindings), CommunityToolkit.Mvvm, xUnit v3 on Microsoft.Testing.Platform, `System.Text.RegularExpressions`. No new package.

**Spec:** `docs/superpowers/specs/2026-10-25-milestone-7c-crash-diagnosis-design.md` (revision 01f3c6f).

## Global Constraints

- Code, docs, commit messages and UI text in English.
- `TreatWarningsAsErrors`, `AnalysisLevel latest-recommended`, `EnforceCodeStyleInBuild`: 0 warnings. `[LoggerMessage]` for logging (CA1848); InvariantCulture when formatting numbers (CA1305: `string.Create(CultureInfo.InvariantCulture, $"…")`, `StringBuilder.AppendLine(CultureInfo.InvariantCulture, $"…")`); no `ToLowerInvariant` for display (CA1308); constant texts on view models are get-only properties with an initializer (CA1822); compute log arguments into locals before the call (CA1873).
- No new NuGet package. No profile schema version change: `diagnostics` is optional (a profile without it has no rules).
- Profile `diagnostics` JSON names: `reportUrl`, `rules`; rule fields `id`, `log`, `exitCodes`, `match` (`"all"` default, or `"any"`), `captures`. Regexes: `RegexOptions.CultureInvariant | RegexOptions.IgnoreCase`, match timeout 1 second. Exit codes: hex strings starting with `0x`, compared as `uint`.
- Rule ids, verbatim: `nvidia-shader-compiler`, `libs-not-dll`, `libs-missing-dll`, `commit-limit`, `gpu-features`, `cpu-too-old`, `param-json`, `invalid-setting`, `not-implemented`; plus `unexplained` (app only, never a profile rule).
- Exit lines, verbatim: `[ps5kitsune] exited with code {unsigned decimal} (0x{8 upper hex digits})` and `[ps5kitsune] stopped by the user (exit code {unsigned decimal})`.
- Report URL: `https://github.com/boykopovar/AnyPS5/discussions/categories/game-bring-up`. NVIDIA drivers page: `https://www.nvidia.com/en-us/drivers/`. Page file settings: `CheckFix.OpenPerformanceSettings` (runs `SystemPropertiesPerformance.exe`).
- Diagnosis texts (title / explanation / button), verbatim, in `DiagnosisTexts` (Task 2):
  - `libs-missing-dll`: "A library could not load" / "{module} needs a DLL that is missing from the game's libs folder." / "Repair libs"
  - `libs-not-dll`: "A file in libs is not a Windows DLL" / "{module} is not a Windows library. Repair libs copies the folder again from the AnyPS5 install." / "Repair libs"
  - `commit-limit`: "Windows ran out of memory to commit" / "The page file plus RAM is too small for this game. Enlarge the page file, or close other programs such as the browser." / "Open page file settings"
  - `gpu-features`: "No GPU here supports AnyPS5" / "The graphics driver lacks buffer device address or shaderInt64. CPU rendering works on any PC, more slowly." / "Turn on CPU rendering"
  - `nvidia-shader-compiler`: "Known NVIDIA driver crash" / "NVIDIA's shader compiler (nvgpucomp64.dll) crashed. Driver 610.47 is known to do this." / "Open NVIDIA drivers page"
  - `cpu-too-old`: "This CPU is not supported" / "The CPU lacks instructions AnyPS5 needs." / no button
  - `param-json`: "The game's metadata is missing" / "sce_sys\param.json is missing or damaged. Remove the game and add it again from its original folder." / "Open folder"
  - `invalid-setting`: "A setting has a value AnyPS5 refuses" / "{variable} has a value AnyPS5 refuses." / "Reset {setting label}", or no button
  - `not-implemented`: "AnyPS5 does not handle this game yet" / "The game uses a feature AnyPS5 has not implemented." / "Report"
  - `unexplained`: "The game stopped with an error" / "AnyPS5 stopped with an error it does not explain yet (exit code 0x{X8})." / "Report"
- Fix results and errors, verbatim (Task 7): "Libs repaired. Launch again to check."; "CPU rendering is on. Launch again to download Mesa and start the game."; "{Setting label, first letter upper} reset. Launch again to check."; "Report copied. Paste it in a new thread for {titleId}."; "The {mode} install has changed since {name} was converted. Reconvert it instead."; "Could not repair libs: {reason}"; "Could not save the settings of {name}: {reason}"; "{name} is running. Stop it first."; "{name} is busy with another action. Try again."; "Could not open {what}: {reason}" with {what} = "the page file settings", "the NVIDIA drivers page", "the Game bring-up page"; "Could not copy the report: {reason}. Use ps5kitsune diagnose {titleId} --report."
- Waiting text, verbatim (Tasks 4, 6, 7): "Waiting for another launch to finish its downloads…".
- CLI texts, verbatim (Task 6): "{Title}: {Explanation}", "Line {n}: {line}", "Fix: …" (Task 2, `DiagnosisTexts.Lines`); "No known error in {log}."; "{name} has no log yet. Launch it first."
- Windows-only tests start with `Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.")` (Platform tests) or `if (!OperatingSystem.IsWindows()) { Assert.Skip("Windows only."); }` (Core tests). Tests that build Windows paths in strings must not depend on `Path` parsing them (Ubuntu CI runs Core and App tests).
- Build: `dotnet build PS5Kitsune.slnx -c Release`. Tests: `dotnet test --solution PS5Kitsune.slnx -c Release`; one class: `dotnet test --project <test project> -c Release -- --filter-class "<full class name>"` (a filtered run exits with code 8 when it selects nothing: check the summary). Run commands through the Bash tool. The test output is in French ("échec" = failed, "réussi" = passed, "ignoré" = skipped).
- Close any running `PS5Kitsune.exe` before building the App (it locks `bin\Release`).
- Line endings: keep each edited file's existing endings (most C# files are CRLF; never mix LF lines into a CRLF file); new files may be LF.
- Commit locally after each task, author La-Kiks, message ending with `Co-Authored-By: <your model name> <noreply@anthropic.com>`. Do not push. Never add or run an `install.cmd`.

## Refinements of the spec

1. **Facts confirmed in AnyPS5 v0.1.2's source** (2026-10-25):
   - `param.json`: besides `param.json not found`, `ParamJsonParser.cpp` throws `failed to open param.json`, `unexpected end of param.json`, `malformed param.json` and a dozen texts ending `… in param.json` (invalid UTF-8, unterminated string, …). The rule uses `match: any` over those forms.
   - `CrashReport.cpp` reports `EXCEPTION_ILLEGAL_INSTRUCTION` (`0xc000001d`) as fatal unless its SSE4a emulation handled it, so `cpu-too-old` matches the crash line or the exit code (`match: any`).
   - Other `ANYPS5_*` errors: `Vulkan: no usable device whose name contains ANYPS5_GPU="…"; devices: …`, `Vulkan presentation: ANYPS5_PRESENT_MODE must be fifo, fifo_latest_ready, mailbox or immediate` and `Vulkan presentation: the surface does not support the requested present mode` (no variable named). v0.1.2 reads no `ANYPS5_OVERLAY_POS` or `APS5_PLAYGO_UNLISTED_CHUNKS`. So `invalid-setting` is four rules sharing the id; the last one names its variable through `captures`.
2. **Several rules may share an id; rules get optional fixed `captures`** (merged under the regex's named groups). This is how the present-mode rule names `ANYPS5_PRESENT_MODE`.
3. **`DiagnosisTexts` lives in Core** (`Core/Diagnostics`), not in the App: the CLI prints the same texts and Core is what both share. "In the app" in the spec means "in code, not in the profile".
4. **Settings fixes go through a new `GamePlayer.UpdateSettingsAsync(titleId, Func<GameSettings, GameSettings>, ct)`**, which changes what game.json holds under the lock; `SaveSettingsAsync` becomes a call to it.
5. **`CrashReporter.BuildAsync(titleId, logPath, Diagnosis?, ct)`** takes a null diagnosis for `diagnose --report` with no match. Its header fields contain no full path but the log's; the quoted log lines are copied as they are and may name paths (the user reads the text before posting). The AnyPS5 line is the game's `convertedWith` mode and version (what the game runs with), with the active profile's id. RAM is `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes`. Settings are the non-default launch variables (`LaunchEnvironment.For`, the Mesa manifest named `lvp_icd.x86_64.json` without a folder).
6. **The Game page gets its diagnosis services as one optional record**, `DiagnosisServices` (App), passed last to both `GameViewModel` constructors; null means no diagnosis (keeps the 7a/7b tests unchanged). `IDialogService` gains `Task CopyTextAsync(string text)`.
7. **The preparation lock is only taken when something must be downloaded.** A launch with Mesa and the fonts already installed takes no lock. `LaunchPreparation` takes `PreparationLock?` as a last optional constructor parameter; `LaunchDownload` gains `Waiting` (its `DownloadProgress` is `default`).
8. **Only Mesa's archive is deleted after install**: `ArchiveTool` gains `protected virtual bool KeepsArchive => true`, `MesaTool` returns false. Python, MinGit and WinLibs keep theirs as before.
9. **Breakaway log:** the line is written after the retry succeeds, reading "Started {Executable} inside the launcher's job (breakaway refused): closing that job also ends the game." A retry that fails logs nothing and its exception propagates (it already did; a test now pins it).
10. **`diagnose` picks the newest `*.log` in `<game>\.ps5kitsune\logs`** by file name (names are `yyyyMMdd-HHmmss.log`). `--report` prints the report with `Console.WriteLine`, so no log prefix is added.
11. **The Game page diagnoses after the exit it watched** (`FollowAsync`), after `Forget` and the status line; a page whose user pressed Stop passes `stopped: true`. A newer launch started meanwhile wins: a late diagnosis is dropped.

## File map

| File | Change |
| --- | --- |
| `src/PS5Kitsune.Core/Profiles/AnyPS5Profile.cs`, `ProfileLoader.cs`, `default-profile.json` | `diagnostics` section, validation (Task 1) |
| `src/PS5Kitsune.Core/Diagnostics/DiagnosticRules.cs` | new: compile and validate rules (Task 1) |
| `src/PS5Kitsune.Core/Diagnostics/DiagnosisIds.cs`, `Diagnosis.cs`, `ExitLines.cs`, `CrashDiagnoser.cs`, `SettingVariables.cs`, `DiagnosisTexts.cs` | new (Task 2) |
| `src/PS5Kitsune.Core/Diagnostics/LibsRepair.cs`, `LibsSourceChangedException.cs`, `CrashReporter.cs` | new (Task 3) |
| `src/PS5Kitsune.Core/Library/GamePlayer.cs` | `UpdateSettingsAsync` (Task 3) |
| `src/PS5Kitsune.Core/Launch/PreparationLock.cs`, `LaunchPreparation.cs`, `LaunchProgress.cs` | lock, `Waiting` (Task 4) |
| `src/PS5Kitsune.Core/Tools/ArchiveTool.cs`, `MesaTool.cs`, `FontPack.cs` | cleanup (Task 4) |
| `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs` | registrations (Tasks 2–4) |
| `src/PS5Kitsune.Platform.Windows/WindowsGameLauncher.cs` | breakaway log (Task 5) |
| `src/PS5Kitsune.Cli/RunCommand.cs`, `DiagnoseCommand.cs`, `ConsoleLaunchProgress.cs`, `CliLog.cs`, `Program.cs` | diagnosis in `run`, `diagnose` (Task 6) |
| `src/PS5Kitsune.App/ViewModels/DiagnosisViewModel.cs`, `DiagnosisServices.cs`, `GameViewModel.cs`, `Views/GameView.axaml`, `Navigation/IDialogService.cs`, `Shell/AvaloniaDialogService.cs`, `Shell/PageFactory.cs` | diagnosis panel (Task 7) |
| `src/PS5Kitsune.App/ViewModels/GameSettingsViewModel.cs` | "running" clears on exit (Task 8) |
| `docs/architecture.md`, `README.md` | docs (Task 9) |

Tests: `tests/PS5Kitsune.Core.Tests/Profiles/ProfileLoaderTests.cs`, `tests/PS5Kitsune.Core.Tests/Diagnostics/*` (new folder), `Library/GamePlayerTests.cs`, `Launch/LaunchPreparationTests.cs`, `Launch/PreparationLockTests.cs`, `Tools/MesaToolTests.cs`, `Tools/FontPackTests.cs`, `tests/PS5Kitsune.Platform.Windows.Tests/WindowsGameLauncherTests.cs`, `tests/PS5Kitsune.App.Tests/DiagnosisViewModelTests.cs`, `GameViewModelTests.cs`, `GameSettingsViewModelTests.cs`, `Fakes.cs`.

---

### Task 1: Core — the profile's diagnostics rules

**Files:**
- Modify: `src/PS5Kitsune.Core/Profiles/AnyPS5Profile.cs`, `src/PS5Kitsune.Core/Profiles/ProfileLoader.cs`, `src/PS5Kitsune.Core/Profiles/default-profile.json`
- Create: `src/PS5Kitsune.Core/Diagnostics/DiagnosticRules.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Profiles/ProfileLoaderTests.cs`

**Interfaces:**
- Produces: `AnyPS5Profile.Diagnostics` (`ProfileDiagnostics`: `Uri? ReportUrl`, `IReadOnlyList<DiagnosticRule> Rules`); `DiagnosticRule` (`Id`, `Log`, `ExitCodes`, `Match`, `Captures`, constants `MatchAll = "all"`, `MatchAny = "any"`); `internal sealed record CompiledRule(DiagnosticRule Rule, IReadOnlyList<Regex> Log, IReadOnlySet<uint> ExitCodes)` with `bool MatchesAll`; `internal static class DiagnosticRules` with `IReadOnlyList<CompiledRule> Compile(string profileId, ProfileDiagnostics diagnostics)` (throws `InvalidProfileException`) and `TimeSpan MatchTimeout`.

- [ ] **Step 1: Write the failing tests**

Add to `ProfileLoaderTests` (before the private `Load` helper):

```csharp
    [Fact]
    public void The_embedded_profile_has_the_diagnostics_rules_in_order()
    {
        var diagnostics = ProfileLoader.LoadEmbedded().Diagnostics;

        Assert.Equal(new Uri("https://github.com/boykopovar/AnyPS5/discussions/categories/game-bring-up"), diagnostics.ReportUrl);
        Assert.Equal(
            ["nvidia-shader-compiler", "libs-not-dll", "libs-missing-dll", "commit-limit", "gpu-features", "cpu-too-old", "param-json",
             "invalid-setting", "invalid-setting", "invalid-setting", "invalid-setting", "not-implemented"],
            diagnostics.Rules.Select(rule => rule.Id));
        Assert.Equal("ANYPS5_PRESENT_MODE", diagnostics.Rules[10].Captures["variable"]);
    }

    [Fact]
    public void A_profile_without_diagnostics_has_no_rules()
    {
        var profile = Load("""{ "schemaVersion": 1, "id": "plain", "source": { "kind": "release", "tag": "v1" } }""");

        Assert.Empty(profile.Diagnostics.Rules);
        Assert.Null(profile.Diagnostics.ReportUrl);
    }

    [Theory]
    [InlineData("""{ "id": "gpu-features" }""")]
    [InlineData("""{ "id": "", "log": [ "x" ] }""")]
    [InlineData("""{ "id": "gpu-features", "log": [ "(unclosed" ] }""")]
    [InlineData("""{ "id": "cpu-too-old", "exitCodes": [ "3221225501" ] }""")]
    [InlineData("""{ "id": "gpu-features", "log": [ "x" ], "match": "most" }""")]
    public void Rejects_an_invalid_diagnostics_rule(string rule)
    {
        Assert.Throws<InvalidProfileException>(() => Load($$"""
            {
              "schemaVersion": 1, "id": "bad", "source": { "kind": "release", "tag": "v1" },
              "diagnostics": { "rules": [ {{rule}} ] }
            }
            """));
    }

    [Fact]
    public void Rejects_a_report_url_over_plain_http()
    {
        Assert.Throws<InvalidProfileException>(() => Load("""
            {
              "schemaVersion": 1, "id": "bad", "source": { "kind": "release", "tag": "v1" },
              "diagnostics": { "reportUrl": "http://example.test/report", "rules": [] }
            }
            """));
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests -c Release -- --filter-class "PS5Kitsune.Core.Tests.Profiles.ProfileLoaderTests"`
Expected: build error, `AnyPS5Profile` has no `Diagnostics`.

- [ ] **Step 3: Add the model**

In `AnyPS5Profile.cs`, after the `Checks` property:

```csharp
    /// <summary>Known AnyPS5 errors, searched in a game's log after it exits; a profile without the section has no rules.</summary>
    public ProfileDiagnostics Diagnostics { get; init; } = new();
```

At the end of the file:

```csharp
/// <summary>The crash diagnosis rules. See docs/architecture.md, "Crash diagnosis".</summary>
public sealed record ProfileDiagnostics
{
    /// <summary>The page Report opens after copying its text; null when the profile names none.</summary>
    public Uri? ReportUrl { get; init; }

    /// <summary>Tried in order: the first rule whose conditions are met wins.</summary>
    public IReadOnlyList<DiagnosticRule> Rules { get; init; } = [];
}

/// <summary>A known error: .NET regexes searched in every line of the log, and exit codes.</summary>
public sealed record DiagnosticRule
{
    public const string MatchAll = "all";

    public const string MatchAny = "any";

    /// <summary>Names the explanation and the fix in the app; several rules may share one.</summary>
    public required string Id { get; init; }

    public IReadOnlyList<string> Log { get; init; } = [];

    /// <summary>Hex strings such as "0xC000001D", compared with the exit code as a 32-bit unsigned value.</summary>
    public IReadOnlyList<string> ExitCodes { get; init; } = [];

    /// <summary>
    /// "all": every regex matches somewhere in the log and, when exit codes are given, the exit code is one of them.
    /// "any": one regex matches, or the exit code is one of them.
    /// </summary>
    public string Match { get; init; } = MatchAll;

    /// <summary>Values for the explanation that no named group of the regexes supplies, for example the variable of a message that names none.</summary>
    public IReadOnlyDictionary<string, string> Captures { get; init; } = new Dictionary<string, string>();
}
```

- [ ] **Step 4: Compile and validate the rules**

Create `src/PS5Kitsune.Core/Diagnostics/DiagnosticRules.cs`:

```csharp
using System.Globalization;
using System.Text.RegularExpressions;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Diagnostics;

/// <summary>A profile rule with its regexes compiled and its exit codes parsed.</summary>
internal sealed record CompiledRule(DiagnosticRule Rule, IReadOnlyList<Regex> Log, IReadOnlySet<uint> ExitCodes)
{
    public bool MatchesAll => Rule.Match == DiagnosticRule.MatchAll;
}

internal static class DiagnosticRules
{
    /// <summary>How long one regex may run on one line.</summary>
    public static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    /// <exception cref="InvalidProfileException">
    /// The report URL is not HTTPS, or a rule has no id, no condition, an unknown match mode, a regex that does not compile or an exit code that is not hex.
    /// </exception>
    public static IReadOnlyList<CompiledRule> Compile(string profileId, ProfileDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        if (diagnostics.ReportUrl is { } url && url.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidProfileException($"Profile {profileId}: diagnostics.reportUrl must use HTTPS.");
        }

        var compiled = new List<CompiledRule>();
        foreach (var rule in diagnostics.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Id))
            {
                throw new InvalidProfileException($"Profile {profileId}: a diagnostics rule has no id.");
            }

            if (rule.Log.Count == 0 && rule.ExitCodes.Count == 0)
            {
                throw new InvalidProfileException($"Profile {profileId}: diagnostics rule {rule.Id} needs log patterns or exit codes.");
            }

            if (rule.Match is not (DiagnosticRule.MatchAll or DiagnosticRule.MatchAny))
            {
                throw new InvalidProfileException($"Profile {profileId}: diagnostics rule {rule.Id} has match \"{rule.Match}\"; use \"all\" or \"any\".");
            }

            var regexes = rule.Log.Select(pattern => CompileRegex(profileId, rule.Id, pattern)).ToList();
            var codes = rule.ExitCodes.Select(code => ParseExitCode(profileId, rule.Id, code)).ToHashSet();
            compiled.Add(new CompiledRule(rule, regexes, codes));
        }

        return compiled;
    }

    private static Regex CompileRegex(string profileId, string id, string pattern)
    {
        try
        {
            return new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, MatchTimeout);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidProfileException($"Profile {profileId}: diagnostics rule {id} has an invalid pattern \"{pattern}\": {exception.Message}", exception);
        }
    }

    private static uint ParseExitCode(string profileId, string id, string code)
    {
        if (code.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && uint.TryParse(code.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        throw new InvalidProfileException($"Profile {profileId}: diagnostics rule {id} has exit code \"{code}\"; use hex such as 0xC000001D.");
    }
}
```

In `ProfileLoader.Validate`, after the `checks` test (last statement), add:

```csharp
        DiagnosticRules.Compile(profile.Id, profile.Diagnostics);
```

and `using PS5Kitsune.Core.Diagnostics;` at the top of `ProfileLoader.cs`.

- [ ] **Step 5: Add the rules to the embedded profile**

In `default-profile.json`, add a comma after the `checks` object and this section before the final `}` (two-space indentation like the rest of the file; in JSON a regex backslash is written `\\`):

```json
  "diagnostics": {
    "reportUrl": "https://github.com/boykopovar/AnyPS5/discussions/categories/game-bring-up",
    "rules": [
      { "id": "nvidia-shader-compiler", "log": [ "unhandled exception 0xc0000005", "rip 0x[0-9a-f]+ nvgpucomp64\\.dll\\+" ] },
      { "id": "libs-not-dll", "log": [ "Failed to load module: (?<module>.+)", "GetLastError: 193 " ] },
      { "id": "libs-missing-dll", "log": [ "Failed to load module: (?<module>.+)", "GetLastError: 126 " ] },
      {
        "id": "commit-limit",
        "match": "any",
        "log": [
          "replace guest placeholder with resident direct memory",
          "commit direct memory pages",
          "keep unmapped direct memory",
          "create direct memory backing"
        ]
      },
      { "id": "gpu-features", "log": [ "buffer device address and shaderInt64 are required" ] },
      { "id": "cpu-too-old", "match": "any", "log": [ "unhandled exception 0xc000001d" ], "exitCodes": [ "0xC000001D" ] },
      {
        "id": "param-json",
        "match": "any",
        "log": [ "param\\.json not found", "failed to open param\\.json", "unexpected end of param\\.json", "malformed param\\.json", " in param\\.json" ]
      },
      { "id": "invalid-setting", "log": [ "(?<variable>A(?:NY)?PS5_[A-Z_]+) must " ] },
      { "id": "invalid-setting", "log": [ "(?<variable>ANYPS5_SYSTEM_FONTS) is not a directory" ] },
      { "id": "invalid-setting", "log": [ "whose name contains (?<variable>ANYPS5_GPU)=" ] },
      {
        "id": "invalid-setting",
        "log": [ "the surface does not support the requested present mode" ],
        "captures": { "variable": "ANYPS5_PRESENT_MODE" }
      },
      { "id": "not-implemented", "log": [ "(is|are) not implemented" ] }
    ]
  }
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests -c Release -- --filter-class "PS5Kitsune.Core.Tests.Profiles.ProfileLoaderTests"`
Expected: all pass (the 5 theory cases included).

- [ ] **Step 7: Commit**

```bash
git add src/PS5Kitsune.Core/Profiles src/PS5Kitsune.Core/Diagnostics tests/PS5Kitsune.Core.Tests/Profiles
git commit -m "Add the profile's crash diagnosis rules

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 2: Core — the diagnoser, the exit line and the diagnosis texts

**Files:**
- Create: `src/PS5Kitsune.Core/Diagnostics/DiagnosisIds.cs`, `Diagnosis.cs`, `ExitLines.cs`, `CrashDiagnoser.cs`, `SettingVariables.cs`, `DiagnosisTexts.cs`
- Modify: `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Diagnostics/CrashDiagnoserTests.cs`, `tests/PS5Kitsune.Core.Tests/Diagnostics/DiagnosisTextsTests.cs`

**Interfaces:**
- Consumes: `DiagnosticRules.Compile`, `CompiledRule` (Task 1); `GameSettings`, `GameLanguages` (Core/Library, 7b).
- Produces:
  - `DiagnosisIds` (string constants listed in Global Constraints, `IReadOnlySet<string> Known` without `Unexplained`).
  - `sealed record Diagnosis(string Id, IReadOnlyDictionary<string, string> Captures, string? Line, int? LineNumber, uint? ExitCode, string LogPath)` with `string? Capture(string name)`.
  - `ExitLines.Exited(int)`, `ExitLines.Stopped(int)`, `ExitLines.Prefix`; internal `ExitLines.Parse(string) : ExitRecord?`.
  - `CrashDiagnoser(AnyPS5Profile, ILogger<CrashDiagnoser>)`: `Task<Diagnosis?> FinishAsync(string logPath, int exitCode, bool stopped, CancellationToken)`, `Task AppendExitLineAsync(string logPath, int exitCode, bool stopped)`, `Task<Diagnosis?> DiagnoseAsync(string logPath, uint? exitCode, CancellationToken)`.
  - `SettingVariable(string Variable, string Label, Func<GameSettings, GameSettings> Reset)`, `SettingVariables.Find(string?)`.
  - `enum DiagnosisAction { None, RepairLibs, OpenPageFileSettings, TurnOnLavapipe, OpenNvidiaDrivers, OpenFolder, ResetSetting, Report }`; `DiagnosisText(string Title, string Explanation, DiagnosisAction Action, string? ActionText)`; `DiagnosisTexts.For(Diagnosis)`, `DiagnosisTexts.Lines(Diagnosis, string name, string titleId)`, `DiagnosisTexts.NvidiaDriversUrl`.

- [ ] **Step 1: Write the failing tests**

Create `tests/PS5Kitsune.Core.Tests/Diagnostics/CrashDiagnoserTests.cs`:

```csharp
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Diagnostics;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Tests.Diagnostics;

public sealed class CrashDiagnoserTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly CrashDiagnoser _diagnoser = new(ProfileLoader.LoadEmbedded(), NullLogger<CrashDiagnoser>.Instance);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string Log(string text)
    {
        var path = _temp.Combine("logs", "20261025-101500.log");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    /// <summary>Log text built from AnyPS5 v0.1.2's messages, the rule id it must give, and its capture ("module" or "variable") when it has one.</summary>
    public static TheoryData<string, string, string?> Samples => new()
    {
        { "FATAL: unhandled exception 0xc0000005 on thread 4321 'Render'\n  rip 0x00007ffb1a2b3c4d nvgpucomp64.dll+0x1a2b3c\n", DiagnosisIds.NvidiaShaderCompiler, null },
        { "Loading PRX: C:\\G\\libs\\libSceAudioOut.prx -> FAILED\nFailed to load module: C:\\G\\libs\\libSceAudioOut.prx\nGetLastError: 193 - %1 is not a valid Win32 application.\n", DiagnosisIds.LibsNotDll, "C:\\G\\libs\\libSceAudioOut.prx" },
        { "Loading PRX: C:\\G\\libs\\libSceAudioOut.prx -> FAILED\nFailed to load module: C:\\G\\libs\\libSceAudioOut.prx\nGetLastError: 126 - The specified module could not be found.\n", DiagnosisIds.LibsMissingDll, "C:\\G\\libs\\libSceAudioOut.prx" },
        { "what():  replace guest placeholder with resident direct memory: The paging file is too small for this operation to complete.\n", DiagnosisIds.CommitLimit, null },
        { "what():  commit direct memory pages\n", DiagnosisIds.CommitLimit, null },
        { "Vulkan: buffer device address and shaderInt64 are required\n", DiagnosisIds.GpuFeatures, null },
        { "\nFATAL: unhandled exception 0xc000001d on thread 1 'main'\n", DiagnosisIds.CpuTooOld, null },
        { "what():  param.json not found\n", DiagnosisIds.ParamJson, null },
        { "what():  unterminated string in param.json\n", DiagnosisIds.ParamJson, null },
        { "what():  sceSystemServiceParamGetInt: ANYPS5_LANGUAGE must be a console language number from 0 to 30\n", DiagnosisIds.InvalidSetting, "ANYPS5_LANGUAGE" },
        { "libSceFont: ANYPS5_SYSTEM_FONTS is not a directory: C:\\fonts\n", DiagnosisIds.InvalidSetting, "ANYPS5_SYSTEM_FONTS" },
        { "what():  Vulkan: no usable device whose name contains ANYPS5_GPU=\"GeForce\"; devices: AMD Radeon(TM) Graphics\n", DiagnosisIds.InvalidSetting, "ANYPS5_GPU" },
        { "what():  Vulkan presentation: the surface does not support the requested present mode\n", DiagnosisIds.InvalidSetting, "ANYPS5_PRESENT_MODE" },
        { "Ngs2: reverb at 96000 Hz is not implemented\n", DiagnosisIds.NotImplemented, null },
    };

    [Theory]
    [MemberData(nameof(Samples))]
    public async Task Each_known_error_gives_its_rule(string log, string id, string? capture)
    {
        var diagnosis = await _diagnoser.DiagnoseAsync(Log("[debug:0] launcher: start\n" + log), 1, Token);

        Assert.NotNull(diagnosis);
        Assert.Equal(id, diagnosis.Id);
        Assert.Equal(1u, diagnosis.ExitCode);
        Assert.NotNull(diagnosis.Line);
        if (capture is not null)
        {
            Assert.Contains(capture, diagnosis.Captures.Values);
        }
    }

    [Fact]
    public async Task The_first_rule_in_profile_order_wins()
    {
        var log = Log("Ngs2: reverb at 96000 Hz is not implemented\nFATAL: unhandled exception 0xc0000005 on thread 1 'Render'\n  rip 0x00007ffb1a2b3c4d nvgpucomp64.dll+0x10\n");

        var diagnosis = await _diagnoser.DiagnoseAsync(log, 0xC0000005, Token);

        Assert.Equal(DiagnosisIds.NvidiaShaderCompiler, diagnosis?.Id);
        Assert.Equal(2, diagnosis?.LineNumber);
    }

    [Fact]
    public async Task An_all_rule_needs_every_pattern()
    {
        var diagnosis = await _diagnoser.DiagnoseAsync(Log("Failed to load module: C:\\G\\libs\\a.prx\n"), 1, Token);

        Assert.Equal(DiagnosisIds.Unexplained, diagnosis?.Id);
    }

    [Fact]
    public async Task An_exit_code_alone_can_match_an_any_rule()
    {
        var diagnosis = await _diagnoser.DiagnoseAsync(Log("nothing known\n"), 0xC000001D, Token);

        Assert.Equal(DiagnosisIds.CpuTooOld, diagnosis?.Id);
        Assert.Null(diagnosis?.Line);
        Assert.Null(diagnosis?.LineNumber);
    }

    [Theory]
    [InlineData(1u, DiagnosisIds.Unexplained)]
    [InlineData(0u, null)]
    [InlineData(null, null)]
    public async Task With_no_match_only_a_failed_exit_is_unexplained(uint? exitCode, string? id)
    {
        var diagnosis = await _diagnoser.DiagnoseAsync(Log("all good\n"), exitCode, Token);

        Assert.Equal(id, diagnosis?.Id);
    }

    [Fact]
    public async Task The_exit_line_gives_the_exit_code_when_none_is_passed()
    {
        var log = Log("all good\n" + ExitLines.Exited(unchecked((int)0xC000001D)) + "\n");

        var diagnosis = await _diagnoser.DiagnoseAsync(log, null, Token);

        Assert.Equal(DiagnosisIds.CpuTooOld, diagnosis?.Id);
        Assert.Equal(0xC000001Du, diagnosis?.ExitCode);
    }

    [Fact]
    public async Task A_game_stopped_by_the_user_is_not_diagnosed()
    {
        var log = Log("Vulkan: buffer device address and shaderInt64 are required\n" + ExitLines.Stopped(1) + "\n");

        Assert.Null(await _diagnoser.DiagnoseAsync(log, null, Token));
    }

    [Fact]
    public async Task Finishing_appends_the_exit_line_on_its_own_line_then_diagnoses()
    {
        var log = Log("Vulkan: buffer device address and shaderInt64 are required\nlast line without newline");

        var diagnosis = await _diagnoser.FinishAsync(log, 3, stopped: false, Token);

        Assert.Equal(DiagnosisIds.GpuFeatures, diagnosis?.Id);
        var lines = await File.ReadAllLinesAsync(log, Token);
        Assert.Equal(["last line without newline", "[ps5kitsune] exited with code 3 (0x00000003)"], lines[^2..]);
    }

    [Fact]
    public async Task Finishing_a_stopped_game_writes_the_stopped_line_and_diagnoses_nothing()
    {
        var log = Log("Vulkan: buffer device address and shaderInt64 are required\n");

        var diagnosis = await _diagnoser.FinishAsync(log, unchecked((int)0xC0000005), stopped: true, Token);

        Assert.Null(diagnosis);
        Assert.Equal("[ps5kitsune] stopped by the user (exit code 3221225477)", (await File.ReadAllLinesAsync(log, Token))[^1]);
    }

    [Fact]
    public void Exit_lines_print_the_code_unsigned_and_in_hex()
    {
        Assert.Equal("[ps5kitsune] exited with code 3221225477 (0xC0000005)", ExitLines.Exited(unchecked((int)0xC0000005)));
    }

    [Fact]
    public async Task A_missing_log_gives_no_diagnosis()
    {
        Assert.Null(await _diagnoser.DiagnoseAsync(_temp.Combine("missing.log"), 1, Token));
    }

    [Fact]
    public async Task The_matched_line_is_trimmed_to_300_characters()
    {
        var diagnosis = await _diagnoser.DiagnoseAsync(Log("  this is not implemented " + new string('x', 400) + "\n"), 1, Token);

        Assert.Equal(300, diagnosis?.Line?.Length);
        Assert.StartsWith("this is not implemented", diagnosis?.Line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_rule_with_an_id_this_app_does_not_know_is_skipped()
    {
        var profile = ProfileLoader.Load(new MemoryStream(Encoding.UTF8.GetBytes("""
            {
              "schemaVersion": 1, "id": "future", "source": { "kind": "release", "tag": "v1" },
              "diagnostics": { "rules": [ { "id": "future-thing", "log": [ "boom" ] }, { "id": "gpu-features", "log": [ "boom" ] } ] }
            }
            """)));
        var diagnoser = new CrashDiagnoser(profile, NullLogger<CrashDiagnoser>.Instance);

        Assert.Equal(DiagnosisIds.GpuFeatures, (await diagnoser.DiagnoseAsync(Log("boom\n"), 1, Token))?.Id);
    }

    public void Dispose() => _temp.Dispose();
}
```

Create `tests/PS5Kitsune.Core.Tests/Diagnostics/DiagnosisTextsTests.cs`:

```csharp
using PS5Kitsune.Core.Diagnostics;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Tests.Diagnostics;

public sealed class DiagnosisTextsTests
{
    private static Diagnosis Of(string id, string? capture = null, string? value = null, uint? exitCode = 1, string? line = "the line") =>
        new(id, capture is null ? new Dictionary<string, string>() : new Dictionary<string, string> { [capture] = value! }, line, line is null ? null : 2, exitCode, "C:\\G\\logs\\1.log");

    [Fact]
    public void A_missing_dll_names_the_module_file_and_offers_repair_libs()
    {
        var text = DiagnosisTexts.For(Of(DiagnosisIds.LibsMissingDll, "module", "C:\\G\\libs\\libSceAudioOut.prx"));

        Assert.Equal("A library could not load", text.Title);
        Assert.Equal("libSceAudioOut.prx needs a DLL that is missing from the game's libs folder.", text.Explanation);
        Assert.Equal(DiagnosisAction.RepairLibs, text.Action);
        Assert.Equal("Repair libs", text.ActionText);
    }

    [Fact]
    public void A_refused_setting_variable_offers_to_reset_its_setting()
    {
        var text = DiagnosisTexts.For(Of(DiagnosisIds.InvalidSetting, "variable", "ANYPS5_LANGUAGE"));

        Assert.Equal("ANYPS5_LANGUAGE has a value AnyPS5 refuses.", text.Explanation);
        Assert.Equal(DiagnosisAction.ResetSetting, text.Action);
        Assert.Equal("Reset game language", text.ActionText);
    }

    [Fact]
    public void A_variable_no_setting_sets_has_no_action()
    {
        var text = DiagnosisTexts.For(Of(DiagnosisIds.InvalidSetting, "variable", "ANYPS5_SYSTEM_FONTS"));

        Assert.Equal(DiagnosisAction.None, text.Action);
        Assert.Null(text.ActionText);
    }

    [Fact]
    public void An_unexplained_exit_shows_the_code_in_hex_and_offers_report()
    {
        var text = DiagnosisTexts.For(Of(DiagnosisIds.Unexplained, exitCode: 0xC0000005, line: null));

        Assert.Equal("AnyPS5 stopped with an error it does not explain yet (exit code 0xC0000005).", text.Explanation);
        Assert.Equal(DiagnosisAction.Report, text.Action);
    }

    [Fact]
    public void The_cli_lines_give_the_explanation_the_line_and_the_fix()
    {
        var lines = DiagnosisTexts.Lines(Of(DiagnosisIds.LibsMissingDll, "module", "C:\\G\\libs\\libSceAudioOut.prx"), "DOOM", "PPSA99666");

        Assert.Equal(
            [
                "A library could not load: libSceAudioOut.prx needs a DLL that is missing from the game's libs folder.",
                "Line 2: the line",
                "Fix: in the app, open DOOM and choose Repair libs.",
            ],
            lines);
    }

    [Fact]
    public void The_report_fix_names_the_diagnose_command_and_a_cpu_error_has_no_fix()
    {
        Assert.Equal(
            "Fix: run ps5kitsune diagnose PPSA99666 --report and paste the text in a new Game bring-up thread.",
            DiagnosisTexts.Lines(Of(DiagnosisIds.NotImplemented), "DOOM", "PPSA99666")[^1]);
        Assert.Equal(2, DiagnosisTexts.Lines(Of(DiagnosisIds.CpuTooOld), "DOOM", "PPSA99666").Count);
    }

    [Fact]
    public void Resetting_a_setting_restores_only_its_default()
    {
        var reset = SettingVariables.Find("anyps5_language")!.Reset(new GameSettings { Language = 5, Overlay = true });

        Assert.Equal(new GameSettings { Overlay = true }, reset);
        Assert.Null(SettingVariables.Find("ANYPS5_SYSTEM_FONTS"));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests -c Release -- --filter-class "PS5Kitsune.Core.Tests.Diagnostics.CrashDiagnoserTests"`
Expected: build error, `CrashDiagnoser` does not exist.

- [ ] **Step 3: Add the ids, the diagnosis and the exit line**

Create `src/PS5Kitsune.Core/Diagnostics/DiagnosisIds.cs`:

```csharp
namespace PS5Kitsune.Core.Diagnostics;

/// <summary>The rule ids the app explains. See the 7c spec, "Game page".</summary>
public static class DiagnosisIds
{
    public const string NvidiaShaderCompiler = "nvidia-shader-compiler";

    public const string LibsNotDll = "libs-not-dll";

    public const string LibsMissingDll = "libs-missing-dll";

    public const string CommitLimit = "commit-limit";

    public const string GpuFeatures = "gpu-features";

    public const string CpuTooOld = "cpu-too-old";

    public const string ParamJson = "param-json";

    public const string InvalidSetting = "invalid-setting";

    public const string NotImplemented = "not-implemented";

    /// <summary>A non-zero exit that no rule explains; never a profile rule.</summary>
    public const string Unexplained = "unexplained";

    /// <summary>The ids a profile rule may use.</summary>
    public static readonly IReadOnlySet<string> Known = new HashSet<string>(StringComparer.Ordinal)
    {
        NvidiaShaderCompiler, LibsNotDll, LibsMissingDll, CommitLimit, GpuFeatures, CpuTooOld, ParamJson, InvalidSetting, NotImplemented,
    };
}
```

Create `src/PS5Kitsune.Core/Diagnostics/Diagnosis.cs`:

```csharp
namespace PS5Kitsune.Core.Diagnostics;

/// <summary>The known error found in a launch's log.</summary>
/// <param name="Id">One of <see cref="DiagnosisIds"/>.</param>
/// <param name="Captures">The named groups of the matched regexes, then the rule's fixed captures: "module", "variable".</param>
/// <param name="Line">The first matched line, trimmed, at most 300 characters; null when only the exit code matched.</param>
/// <param name="LineNumber">1-based; null when <paramref name="Line"/> is.</param>
/// <param name="ExitCode">Null when nobody saw the exit.</param>
public sealed record Diagnosis(string Id, IReadOnlyDictionary<string, string> Captures, string? Line, int? LineNumber, uint? ExitCode, string LogPath)
{
    public string? Capture(string name) => Captures.TryGetValue(name, out var value) ? value : null;
}
```

Create `src/PS5Kitsune.Core/Diagnostics/ExitLines.cs`:

```csharp
using System.Globalization;

namespace PS5Kitsune.Core.Diagnostics;

/// <summary>The line PS5Kitsune appends to a launch's log once the game has exited. See the 7c spec, "Exit line".</summary>
public static class ExitLines
{
    public const string Prefix = "[ps5kitsune] ";

    private const string ExitedStart = "exited with code ";

    private const string StoppedStart = "stopped by the user (exit code ";

    public static string Exited(int exitCode) =>
        string.Create(CultureInfo.InvariantCulture, $"{Prefix}{ExitedStart}{(uint)exitCode} (0x{(uint)exitCode:X8})");

    public static string Stopped(int exitCode) =>
        string.Create(CultureInfo.InvariantCulture, $"{Prefix}{StoppedStart}{(uint)exitCode})");

    /// <summary>What an exit line says; null for any other line.</summary>
    internal static ExitRecord? Parse(string line)
    {
        if (!line.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = line.AsSpan(Prefix.Length);
        if (TryCode(rest, ExitedStart, out var code))
        {
            return new ExitRecord(code, Stopped: false);
        }

        return TryCode(rest, StoppedStart, out code) ? new ExitRecord(code, Stopped: true) : null;
    }

    private static bool TryCode(ReadOnlySpan<char> rest, string start, out uint code)
    {
        code = 0;
        if (!rest.StartsWith(start, StringComparison.Ordinal))
        {
            return false;
        }

        var digits = rest[start.Length..];
        var end = digits.IndexOfAnyExceptInRange('0', '9');
        return uint.TryParse(end < 0 ? digits : digits[..end], NumberStyles.None, CultureInfo.InvariantCulture, out code);
    }
}

/// <param name="Stopped">True when the user ended the game with Stop.</param>
internal readonly record struct ExitRecord(uint Code, bool Stopped);
```

- [ ] **Step 4: Add the diagnoser**

Create `src/PS5Kitsune.Core/Diagnostics/CrashDiagnoser.cs`:

```csharp
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Diagnostics;

/// <summary>
/// Finds the known AnyPS5 error in a launch's log once the game has exited: the first profile rule whose conditions are met,
/// else "unexplained" for a non-zero exit. See the 7c spec, "CrashDiagnoser".
/// </summary>
public sealed partial class CrashDiagnoser
{
    private const int MaxLineLength = 300;

    private readonly IReadOnlyList<CompiledRule> _rules;
    private readonly ILogger<CrashDiagnoser> _logger;

    public CrashDiagnoser(AnyPS5Profile profile, ILogger<CrashDiagnoser> logger)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _logger = logger;
        var rules = new List<CompiledRule>();
        // The loader validated the profile, so this compiles the same rules again without throwing.
        foreach (var rule in DiagnosticRules.Compile(profile.Id, profile.Diagnostics))
        {
            if (DiagnosisIds.Known.Contains(rule.Rule.Id))
            {
                rules.Add(rule);
            }
            else
            {
                var id = rule.Rule.Id;
                LogUnknownRule(id);
            }
        }

        _rules = rules;
    }

    /// <summary>Appends the exit line to the log, then diagnoses it unless the user stopped the game.</summary>
    public async Task<Diagnosis?> FinishAsync(string logPath, int exitCode, bool stopped, CancellationToken cancellationToken)
    {
        await AppendExitLineAsync(logPath, exitCode, stopped).ConfigureAwait(false);
        return stopped ? null : await DiagnoseAsync(logPath, (uint)exitCode, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Appends the exit line on a line of its own; a failed write is logged and changes nothing else.</summary>
    public async Task AppendExitLineAsync(string logPath, int exitCode, bool stopped)
    {
        ArgumentNullException.ThrowIfNull(logPath);
        var line = stopped ? ExitLines.Stopped(exitCode) : ExitLines.Exited(exitCode);
        try
        {
            var stream = new FileStream(logPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
            await using (stream.ConfigureAwait(false))
            {
                var separator = "";
                if (stream.Length > 0)
                {
                    stream.Seek(-1, SeekOrigin.End);
                    separator = stream.ReadByte() == '\n' ? "" : Environment.NewLine;
                }

                stream.Seek(0, SeekOrigin.End);
                await stream.WriteAsync(Encoding.UTF8.GetBytes(separator + line + Environment.NewLine)).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogExitLineFailed(exception, logPath);
        }
    }

    /// <param name="exitCode">The exit code; null to use the log's exit line, when it has one.</param>
    /// <returns>
    /// The first rule met, in profile order; else "unexplained" for a non-zero exit code; else null (a zero or unknown exit code, or a game
    /// the user stopped). An unreadable log or a regex that times out is logged and gives null.
    /// </returns>
    public async Task<Diagnosis?> DiagnoseAsync(string logPath, uint? exitCode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(logPath);
        try
        {
            return await Task.Run(() => Diagnose(logPath, exitCode, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or RegexMatchTimeoutException)
        {
            LogDiagnosisFailed(exception, logPath);
            return null;
        }
    }

    private Diagnosis? Diagnose(string logPath, uint? exitCode, CancellationToken cancellationToken)
    {
        var states = _rules.Select(rule => new RuleState(rule)).ToList();
        ExitRecord? exit = null;
        using (var reader = new StreamReader(new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)))
        {
            var number = 0;
            while (reader.ReadLine() is { } line)
            {
                cancellationToken.ThrowIfCancellationRequested();
                number++;
                if (ExitLines.Parse(line) is { } parsed)
                {
                    exit = parsed;
                    continue;
                }

                foreach (var state in states)
                {
                    state.See(line, number);
                }
            }
        }

        if (exitCode is null && exit is { Stopped: true })
        {
            return null;
        }

        var code = exitCode ?? exit?.Code;
        foreach (var state in states)
        {
            if (state.IsMet(code))
            {
                return state.Diagnosis(code, logPath);
            }
        }

        return code is { } failed and not 0
            ? new Diagnosis(DiagnosisIds.Unexplained, new Dictionary<string, string>(), null, null, failed, logPath)
            : null;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The profile's diagnostics rule {Id} is not known to this app: it is skipped.")]
    private partial void LogUnknownRule(string id);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not add the exit line to {LogPath}.")]
    private partial void LogExitLineFailed(Exception exception, string logPath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not diagnose {LogPath}: no diagnosis is shown.")]
    private partial void LogDiagnosisFailed(Exception exception, string logPath);

    // What one rule has seen so far: which of its regexes matched, their named groups, and the first matched line.
    private sealed class RuleState(CompiledRule rule)
    {
        private readonly bool[] _matched = new bool[rule.Log.Count];
        private readonly Dictionary<string, string> _captures = new(StringComparer.Ordinal);
        private string? _line;
        private int? _lineNumber;

        public void See(string line, int number)
        {
            for (var index = 0; index < rule.Log.Count; index++)
            {
                if (_matched[index] || rule.Log[index].Match(line) is not { Success: true } match)
                {
                    continue;
                }

                _matched[index] = true;
                foreach (Group group in match.Groups)
                {
                    // Numbered groups are named "0", "1"…: only named groups are captures.
                    if (group.Success && group.Value.Length > 0 && !char.IsAsciiDigit(group.Name[0]))
                    {
                        _captures.TryAdd(group.Name, group.Value.Trim());
                    }
                }

                if (_line is null)
                {
                    var text = line.Trim();
                    _line = text.Length > MaxLineLength ? text[..MaxLineLength] : text;
                    _lineNumber = number;
                }
            }
        }

        public bool IsMet(uint? code)
        {
            var codeMatches = code is { } value && rule.ExitCodes.Contains(value);
            return rule.MatchesAll
                ? _matched.All(matched => matched) && (rule.ExitCodes.Count == 0 || codeMatches)
                : _matched.Any(matched => matched) || codeMatches;
        }

        public Diagnosis Diagnosis(uint? code, string logPath)
        {
            var captures = new Dictionary<string, string>(_captures, StringComparer.Ordinal);
            foreach (var (name, value) in rule.Rule.Captures)
            {
                captures.TryAdd(name, value);
            }

            return new Diagnosis(rule.Rule.Id, captures, _line, _lineNumber, code, logPath);
        }
    }
}
```

- [ ] **Step 5: Add the setting variables and the texts**

Create `src/PS5Kitsune.Core/Diagnostics/SettingVariables.cs`:

```csharp
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Diagnostics;

/// <param name="Label">As in "Reset game language": lower case, except names such as GPU.</param>
/// <param name="Reset">The settings with this one back to its default.</param>
public sealed record SettingVariable(string Variable, string Label, Func<GameSettings, GameSettings> Reset);

/// <summary>The game setting behind each environment variable AnyPS5 may refuse. See the 7c spec, "Fixes".</summary>
public static class SettingVariables
{
    private static readonly GameSettings Defaults = new();

    private static readonly SettingVariable[] All =
    [
        new("ANYPS5_GPU", "GPU", settings => settings with { Gpu = Defaults.Gpu }),
        new("ANYPS5_LANGUAGE", "game language", settings => settings with { Language = Defaults.Language }),
        new("ANYPS5_PRESENT_MODE", "vertical sync", settings => settings with { PresentMode = Defaults.PresentMode }),
        new("ANYPS5_OVERLAY", "performance overlay", settings => settings with { Overlay = Defaults.Overlay }),
        new("ANYPS5_OVERLAY_POS", "overlay position", settings => settings with { OverlayPosition = Defaults.OverlayPosition }),
        new("APS5_PLAYGO_UNLISTED_CHUNKS", "PlayGo unlisted chunks", settings => settings with { PlayGoUnlistedChunks = Defaults.PlayGoUnlistedChunks }),
    ];

    /// <summary>The setting behind <paramref name="variable"/>, compared ignoring case; null for a variable no game setting sets.</summary>
    public static SettingVariable? Find(string? variable) =>
        All.FirstOrDefault(item => string.Equals(item.Variable, variable, StringComparison.OrdinalIgnoreCase));
}
```

Create `src/PS5Kitsune.Core/Diagnostics/DiagnosisTexts.cs`:

```csharp
using System.Globalization;

namespace PS5Kitsune.Core.Diagnostics;

/// <summary>What the Game page offers for a diagnosis. See the 7c spec, "Game page".</summary>
public enum DiagnosisAction
{
    None,
    RepairLibs,
    OpenPageFileSettings,
    TurnOnLavapipe,
    OpenNvidiaDrivers,
    OpenFolder,
    ResetSetting,
    Report,
}

/// <param name="ActionText">The button's text; null with <see cref="DiagnosisAction.None"/>.</param>
public sealed record DiagnosisText(string Title, string Explanation, DiagnosisAction Action, string? ActionText);

/// <summary>Each diagnosis id's title, explanation and action, shared by the app and the CLI.</summary>
public static class DiagnosisTexts
{
    public const string NvidiaDriversUrl = "https://www.nvidia.com/en-us/drivers/";

    private const string SettingTitle = "A setting has a value AnyPS5 refuses";

    public static DiagnosisText For(Diagnosis diagnosis)
    {
        ArgumentNullException.ThrowIfNull(diagnosis);
        var module = FileName(diagnosis.Capture("module")) ?? "A library";
        return diagnosis.Id switch
        {
            DiagnosisIds.LibsMissingDll => new(
                "A library could not load", $"{module} needs a DLL that is missing from the game's libs folder.", DiagnosisAction.RepairLibs, "Repair libs"),
            DiagnosisIds.LibsNotDll => new(
                "A file in libs is not a Windows DLL",
                $"{module} is not a Windows library. Repair libs copies the folder again from the AnyPS5 install.",
                DiagnosisAction.RepairLibs,
                "Repair libs"),
            DiagnosisIds.CommitLimit => new(
                "Windows ran out of memory to commit",
                "The page file plus RAM is too small for this game. Enlarge the page file, or close other programs such as the browser.",
                DiagnosisAction.OpenPageFileSettings,
                "Open page file settings"),
            DiagnosisIds.GpuFeatures => new(
                "No GPU here supports AnyPS5",
                "The graphics driver lacks buffer device address or shaderInt64. CPU rendering works on any PC, more slowly.",
                DiagnosisAction.TurnOnLavapipe,
                "Turn on CPU rendering"),
            DiagnosisIds.NvidiaShaderCompiler => new(
                "Known NVIDIA driver crash",
                "NVIDIA's shader compiler (nvgpucomp64.dll) crashed. Driver 610.47 is known to do this.",
                DiagnosisAction.OpenNvidiaDrivers,
                "Open NVIDIA drivers page"),
            DiagnosisIds.CpuTooOld => new("This CPU is not supported", "The CPU lacks instructions AnyPS5 needs.", DiagnosisAction.None, null),
            DiagnosisIds.ParamJson => new(
                "The game's metadata is missing",
                @"sce_sys\param.json is missing or damaged. Remove the game and add it again from its original folder.",
                DiagnosisAction.OpenFolder,
                "Open folder"),
            DiagnosisIds.InvalidSetting => Setting(diagnosis.Capture("variable")),
            DiagnosisIds.NotImplemented => new(
                "AnyPS5 does not handle this game yet", "The game uses a feature AnyPS5 has not implemented.", DiagnosisAction.Report, "Report"),
            _ => new(
                "The game stopped with an error",
                $"AnyPS5 stopped with an error it does not explain yet (exit code {Code(diagnosis.ExitCode)}).",
                DiagnosisAction.Report,
                "Report"),
        };
    }

    /// <summary>What `run` and `diagnose` print: the title and explanation, the matched line, then the fix in words when there is one.</summary>
    public static IReadOnlyList<string> Lines(Diagnosis diagnosis, string name, string titleId)
    {
        var text = For(diagnosis);
        var lines = new List<string> { $"{text.Title}: {text.Explanation}" };
        if (diagnosis.Line is { } line)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"Line {diagnosis.LineNumber}: {line}"));
        }

        var fix = text.Action switch
        {
            DiagnosisAction.RepairLibs or DiagnosisAction.TurnOnLavapipe or DiagnosisAction.ResetSetting or DiagnosisAction.OpenPageFileSettings =>
                $"Fix: in the app, open {name} and choose {text.ActionText}.",
            DiagnosisAction.OpenNvidiaDrivers => $"Fix: update the NVIDIA driver from {NvidiaDriversUrl}",
            DiagnosisAction.OpenFolder => "Fix: remove the game and add it again from its original folder.",
            DiagnosisAction.Report => $"Fix: run ps5kitsune diagnose {titleId} --report and paste the text in a new Game bring-up thread.",
            _ => null,
        };
        if (fix is not null)
        {
            lines.Add(fix);
        }

        return lines;
    }

    private static DiagnosisText Setting(string? variable)
    {
        var explanation = $"{variable ?? "A setting"} has a value AnyPS5 refuses.";
        return SettingVariables.Find(variable) is { } setting
            ? new(SettingTitle, explanation, DiagnosisAction.ResetSetting, $"Reset {setting.Label}")
            : new(SettingTitle, explanation, DiagnosisAction.None, null);
    }

    // The log names Windows paths; Path.GetFileName would not split them on Linux, where the tests also run.
    private static string? FileName(string? path) => path?[(path.LastIndexOfAny(['\\', '/']) + 1)..];

    private static string Code(uint? code) => code is { } value ? string.Create(CultureInfo.InvariantCulture, $"0x{value:X8}") : "unknown";
}
```

- [ ] **Step 6: Register the diagnoser**

In `ServiceCollectionExtensions.AddPS5KitsuneCore`, after `services.AddSingleton<GameCleaner>();`:

```csharp
        services.AddSingleton<CrashDiagnoser>();
```

with `using PS5Kitsune.Core.Diagnostics;`.

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests -c Release -- --filter-class "PS5Kitsune.Core.Tests.Diagnostics.CrashDiagnoserTests"`, then the same with `PS5Kitsune.Core.Tests.Diagnostics.DiagnosisTextsTests`.
Expected: all pass.

- [ ] **Step 8: Commit**

```bash
git add src/PS5Kitsune.Core/Diagnostics src/PS5Kitsune.Core/ServiceCollectionExtensions.cs tests/PS5Kitsune.Core.Tests/Diagnostics
git commit -m "Diagnose a launch's log after the exit and describe each known error

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 3: Core — the fixes: Repair libs, settings changes and the report

**Files:**
- Create: `src/PS5Kitsune.Core/Diagnostics/LibsRepair.cs`, `LibsSourceChangedException.cs`, `CrashReporter.cs`
- Modify: `src/PS5Kitsune.Core/Library/GamePlayer.cs`, `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Diagnostics/LibsRepairTests.cs`, `tests/PS5Kitsune.Core.Tests/Diagnostics/CrashReporterTests.cs`, `tests/PS5Kitsune.Core.Tests/Library/GamePlayerTests.cs`

**Interfaces:**
- Consumes: `Diagnosis` (Task 2); `GameLibrary.Layout(string)`, `GameLibrary.ReadHeld(GameLayout)`, `GameLibrary.Find(string)`, `GameLock.TryAcquire`, `RunningGames.Check`, `IAnyPS5Installer.ReadRecord(AnyPS5Mode)`, `AnyPS5Paths.For(AnyPS5Mode).Libs`, `InstallModes.Parse`, `FileTree.Copy`, `ZipExtraction.MarkerFileName`, `LaunchEnvironment.For(GameSettings, string?, string?)`, `ICpuInfo`, `IVulkanProbeRunner`, `IPlatform.GetCommitLimitBytes()` (existing).
- Produces:
  - `LibsRepair(GameLibrary, IAnyPS5Installer, AnyPS5Paths, RunningGames)`: `Task RepairAsync(string titleId, CancellationToken)`; throws `GameNotFoundException`, `InvalidGameRecordException`, `GameBusyException`, `GameRunningException`, `LibsSourceChangedException`, `IOException`, `UnauthorizedAccessException`.
  - `LibsSourceChangedException(string mode, string name)`.
  - `GamePlayer.UpdateSettingsAsync(string titleId, Func<GameSettings, GameSettings> change, CancellationToken) : Task<GameRecord>`.
  - `CrashReporter(GameLibrary, AnyPS5Profile, ICpuInfo, IVulkanProbeRunner, IPlatform)`: `Task<string> BuildAsync(string titleId, string logPath, Diagnosis? diagnosis, CancellationToken)`; `const int LogLines = 30`; internal `Format(...)` and `LastLinesAsync(...)`; `CrashReportFacts` record.

- [ ] **Step 1: Write the failing tests**

Create `tests/PS5Kitsune.Core.Tests/Diagnostics/LibsRepairTests.cs`:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Diagnostics;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Tests.AnyPS5;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tests.Games;
using PS5Kitsune.Core.Tests.Library;

namespace PS5Kitsune.Core.Tests.Diagnostics;

public sealed class LibsRepairTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly AnyPS5TestSetup _setup;
    private readonly FakeGameLauncher _launcher = new();
    private readonly GameLibrary _library;
    private readonly RunningGames _running;
    private readonly LibsRepair _repair;

    public LibsRepairTests()
    {
        _setup = new AnyPS5TestSetup(_temp.Combine("root"));
        _library = new GameLibrary(_setup.Layout, _setup.Platform, _setup.Time, NullLogger<GameLibrary>.Instance);
        _running = new RunningGames(_launcher, NullLogger<RunningGames>.Instance);
        _repair = new LibsRepair(_library, _setup.Installer, _setup.Paths, _running);
    }

    private GameLayout Game => new(_setup.Layout.GameDirectory(TestGames.TitleId), TestGames.TitleId, _setup.Platform);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // DOOM converted by the release install, with a libs folder that lost a DLL and gained a stray PS5 module.
    private async Task AddDoomAsync(string? version = null, string mode = "release")
    {
        await _setup.InstallAsync();
        ConvertedGames.Create(Game);
        var install = _setup.Installer.ReadRecord(AnyPS5Mode.Release)!;
        await GameRecordStore.WriteAsync(
            Game.RecordFile,
            TestRecords.Sample() with { ConvertedWith = new ConversionInfo(install.Profile, mode, version ?? install.Version, null, false) },
            Token);
        TestFiles.Write(Path.Combine(Game.Libs, "libSceAudioOut.prx"), "prx");
        TestFiles.Write(Path.Combine(Game.Libs, "libSceNpManager.sprx"), "sony module");
    }

    [Fact]
    public async Task Repair_copies_the_libs_again_from_the_install_that_converted_the_game()
    {
        await AddDoomAsync();

        await _repair.RepairAsync(TestGames.TitleId, Token);

        var source = _setup.Paths.For(AnyPS5Mode.Release).Libs;
        Assert.Equal(
            FileTree.RelativeFiles(source).Where(relative => relative != ZipExtraction.MarkerFileName).Order(StringComparer.Ordinal),
            FileTree.RelativeFiles(Game.Libs).Order(StringComparer.Ordinal));
        Assert.False(GameLock.IsHeld(Game));
    }

    [Fact]
    public async Task Repair_refuses_a_running_game()
    {
        await AddDoomAsync();
        var process = _launcher.Launch(new GameLaunchRequest(Game.Executable, Game.Root, Game.Logs));
        await _running.RecordAsync(Game, process, Game.Executable);

        await Assert.ThrowsAsync<GameRunningException>(() => _repair.RepairAsync(TestGames.TitleId, Token));
        Assert.True(File.Exists(Path.Combine(Game.Libs, "libSceNpManager.sprx")));
    }

    [Fact]
    public async Task Repair_refuses_a_busy_game()
    {
        await AddDoomAsync();
        using var held = GameLock.TryAcquire(Game);

        await Assert.ThrowsAsync<GameBusyException>(() => _repair.RepairAsync(TestGames.TitleId, Token));
    }

    [Fact]
    public async Task Repair_refuses_when_the_install_has_another_version()
    {
        await AddDoomAsync(version: "v0.0.9");

        var exception = await Assert.ThrowsAsync<LibsSourceChangedException>(() => _repair.RepairAsync(TestGames.TitleId, Token));

        Assert.Equal("The release install has changed since DOOM was converted. Reconvert it instead.", exception.Message);
        Assert.True(File.Exists(Path.Combine(Game.Libs, "libSceNpManager.sprx")));
    }

    [Fact]
    public async Task Repair_refuses_when_the_install_is_missing()
    {
        await AddDoomAsync(mode: "build");

        await Assert.ThrowsAsync<LibsSourceChangedException>(() => _repair.RepairAsync(TestGames.TitleId, Token));
    }

    public void Dispose() => _temp.Dispose();
}
```

(`GameLayout.Libs`, `TestFiles.Write(string, string)`, `FileTree.RelativeFiles` and `GameLock.IsHeld` already exist; check `TestFiles.Write`'s overloads and use the `byte[]` one with `"prx"u8.ToArray()` if there is no string overload.)

Create `tests/PS5Kitsune.Core.Tests/Diagnostics/CrashReporterTests.cs`:

```csharp
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Checks.Vulkan;
using PS5Kitsune.Core.Diagnostics;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Tests.Library;

namespace PS5Kitsune.Core.Tests.Diagnostics;

public sealed class CrashReporterTests : IDisposable
{
    private const string LogPath = "C:\\PS5K\\games\\PPSA99666\\.ps5kitsune\\logs\\20261025-101500.log";

    private static readonly CrashReportFacts Facts = new(
        "0.7.0",
        "Microsoft Windows 10.0.26300",
        new CpuFacts("AuthenticAMD", []),
        [new VulkanGpu("AMD Radeon(TM) Graphics", 0x1002, "integrated", "1.3.280", "AmdProprietary", "AMD proprietary driver", "25.10.1", true, true)],
        null,
        16L << 30,
        32UL << 30);

    private readonly TempDirectory _temp = new();

    private static Diagnosis Unexplained => new(DiagnosisIds.Unexplained, new Dictionary<string, string>(), null, null, 0xC0000005, LogPath);

    private static string Format(Diagnosis? diagnosis, GameRecord? record = null) =>
        CrashReporter.Format(Facts, "doom-validated-2026-10", record ?? TestRecords.Sample(), diagnosis, LogPath, ["line a", "line b"]);

    [Fact]
    public void The_report_has_the_pc_the_game_the_exit_and_the_log()
    {
        var report = Format(Unexplained, TestRecords.Sample() with { Settings = new GameSettings { Language = 5 } });

        Assert.Contains("PS5Kitsune 0.7.0 on Microsoft Windows 10.0.26300", report, StringComparison.Ordinal);
        Assert.Contains("CPU: AuthenticAMD", report, StringComparison.Ordinal);
        Assert.Contains("GPU: AMD Radeon(TM) Graphics, driver 25.10.1", report, StringComparison.Ordinal);
        Assert.Contains("Memory: 16.0 GiB RAM, commit limit 32.0 GiB", report, StringComparison.Ordinal);
        Assert.Contains("AnyPS5: release v0.1.2, profile doom-validated-2026-10", report, StringComparison.Ordinal);
        Assert.Contains("Game: PPSA99666 DOOM, audit 262 implemented, 3 stubs, 0 absent", report, StringComparison.Ordinal);
        Assert.Contains("Settings: ANYPS5_LANGUAGE=5", report, StringComparison.Ordinal);
        Assert.Contains("Exit code: 0xC0000005", report, StringComparison.Ordinal);
        Assert.Contains("Diagnosis: unexplained", report, StringComparison.Ordinal);
        Assert.Contains("Log: " + LogPath, report, StringComparison.Ordinal);
        Assert.EndsWith("```text" + Environment.NewLine + "line a" + Environment.NewLine + "line b" + Environment.NewLine + "```" + Environment.NewLine, report, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_the_log_line_names_a_full_path()
    {
        var header = Format(Unexplained, TestRecords.Sample() with { Settings = new GameSettings { Lavapipe = true } }).Split("Last 30 log lines:")[0];

        Assert.DoesNotContain(":\\", header.Replace("Log: " + LogPath, "", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public void Without_a_diagnosis_the_report_says_none_and_default_settings_say_defaults()
    {
        var report = Format(null);

        Assert.Contains("Diagnosis: none", report, StringComparison.Ordinal);
        Assert.Contains("Exit code: unknown", report, StringComparison.Ordinal);
        Assert.Contains("Settings: defaults", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_last_30_lines_of_the_log_are_quoted()
    {
        var log = _temp.Combine("game.log");
        await File.WriteAllLinesAsync(log, Enumerable.Range(1, 40).Select(index => $"line {index}"), TestContext.Current.CancellationToken);

        var lines = await CrashReporter.LastLinesAsync(log, TestContext.Current.CancellationToken);

        Assert.Equal(Enumerable.Range(11, 30).Select(index => $"line {index}"), lines);
    }

    public void Dispose() => _temp.Dispose();
}
```

In `GamePlayerTests`, add:

```csharp
    [Fact]
    public async Task Updating_settings_changes_what_game_json_holds_now()
    {
        await AddDoomAsync();
        await _player.SaveSettingsAsync(TestGames.TitleId, new GameSettings { Language = 5, Overlay = true }, TestContext.Current.CancellationToken);

        var saved = await _player.UpdateSettingsAsync(TestGames.TitleId, settings => settings with { Language = 1 }, TestContext.Current.CancellationToken);

        Assert.Equal(new GameSettings { Overlay = true }, saved.Settings);
        Assert.Equal(new GameSettings { Overlay = true }, _library.Find(TestGames.TitleId).Record.Settings);
    }

    [Fact]
    public async Task Updating_settings_refuses_a_running_game()
    {
        await AddDoomAsync();
        await LaunchAsync();

        await Assert.ThrowsAsync<GameRunningException>(
            () => _player.UpdateSettingsAsync(TestGames.TitleId, settings => settings with { Lavapipe = true }, TestContext.Current.CancellationToken));
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests -c Release -- --filter-class "PS5Kitsune.Core.Tests.Diagnostics.LibsRepairTests"`
Expected: build error, `LibsRepair` does not exist.

- [ ] **Step 3: Add `UpdateSettingsAsync` and make `SaveSettingsAsync` use it**

In `GamePlayer.cs`, replace the body of `SaveSettingsAsync` (keep its doc comment and exceptions) and add the new method after it:

```csharp
    public Task<GameRecord> SaveSettingsAsync(string titleId, GameSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return UpdateSettingsAsync(titleId, _ => settings, cancellationToken);
    }

    /// <summary>
    /// Changes the settings game.json holds now, under the game lock, then normalizes and writes them: for a diagnosis' fixes,
    /// which change one setting and keep the others.
    /// </summary>
    /// <returns>The record written.</returns>
    /// <exception cref="GameNotFoundException">No game with this title ID.</exception>
    /// <exception cref="InvalidGameRecordException">The game.json is malformed.</exception>
    /// <exception cref="GameBusyException">Another action holds the game's lock.</exception>
    /// <exception cref="GameRunningException">The game is running: settings only apply at launch.</exception>
    public async Task<GameRecord> UpdateSettingsAsync(string titleId, Func<GameSettings, GameSettings> change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(titleId);
        ArgumentNullException.ThrowIfNull(change);
        var game = library.Layout(titleId);
        using var held = GameLock.TryAcquire(game) ?? throw new GameBusyException(titleId);
        var record = library.ReadHeld(game);
        if (running.Check(game) is { } current)
        {
            throw new GameRunningException(record.Name, current);
        }

        var saved = record with { Settings = GameSettingsValidator.Normalize(change(record.Settings)).Settings };
        await GameRecordStore.WriteAsync(game.RecordFile, saved, cancellationToken).ConfigureAwait(false);
        return saved;
    }
```

(`SaveSettingsAsync` stops being `async`: it returns the task.)

- [ ] **Step 4: Add Repair libs**

Create `src/PS5Kitsune.Core/Diagnostics/LibsSourceChangedException.cs`:

```csharp
namespace PS5Kitsune.Core.Diagnostics;

/// <summary>The install that converted a game is missing or has another version, so its libs may not match the conversion.</summary>
public sealed class LibsSourceChangedException(string mode, string name)
    : Exception($"The {mode} install has changed since {name} was converted. Reconvert it instead.");
```

Create `src/PS5Kitsune.Core/Diagnostics/LibsRepair.cs`:

```csharp
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Diagnostics;

/// <summary>
/// Repair libs, the fix for errors 126 and 193: deletes the game's libs and copies them again from the install that converted the game.
/// The libs must match the conversion, so the active install is not used. See the 7c spec, "Fixes".
/// </summary>
public sealed class LibsRepair(GameLibrary library, IAnyPS5Installer installer, AnyPS5Paths paths, RunningGames running)
{
    /// <exception cref="GameNotFoundException">No game with this title ID.</exception>
    /// <exception cref="InvalidGameRecordException">The game.json is malformed.</exception>
    /// <exception cref="GameBusyException">Another action holds the game's lock.</exception>
    /// <exception cref="GameRunningException">The game is running: its libs are in use.</exception>
    /// <exception cref="LibsSourceChangedException">The install that converted the game is missing or has another version.</exception>
    public async Task RepairAsync(string titleId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(titleId);
        var game = library.Layout(titleId);
        using var held = GameLock.TryAcquire(game) ?? throw new GameBusyException(titleId);
        var record = library.ReadHeld(game);
        if (running.Check(game) is { } current)
        {
            throw new GameRunningException(record.Name, current);
        }

        var convertedWith = record.ConvertedWith;
        if (InstallModes.Parse(convertedWith.Mode) is not { } mode
            || installer.ReadRecord(mode) is not { } install
            || install.Version != convertedWith.Version
            || !Directory.Exists(paths.For(mode).Libs))
        {
            throw new LibsSourceChangedException(convertedWith.Mode, record.Name);
        }

        var source = paths.For(mode).Libs;
        await Task.Run(
            () =>
            {
                if (Directory.Exists(game.Libs))
                {
                    Directory.Delete(game.Libs, recursive: true);
                }

                FileTree.Copy(source, game.Libs, relative => relative == ZipExtraction.MarkerFileName, cancellationToken);
            },
            cancellationToken).ConfigureAwait(false);
    }
}
```

(`FileTree.Copy(sourceRoot, targetRoot, isCopied, ct)` skips the files `isCopied` returns true for, as `CopyLibsStep` uses it.)

- [ ] **Step 5: Add the report**

Create `src/PS5Kitsune.Core/Diagnostics/CrashReporter.cs`:

```csharp
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Checks.Vulkan;
using PS5Kitsune.Core.Launch;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Diagnostics;

/// <param name="GpuFailure">Why no GPU was detected; null when the probe ran.</param>
/// <param name="RamBytes">Null when unknown.</param>
/// <param name="CommitLimitBytes">Null where the OS has no commit limit.</param>
public sealed record CrashReportFacts(
    string AppVersion, string Os, CpuFacts Cpu, IReadOnlyList<VulkanGpu> Gpus, string? GpuFailure, long? RamBytes, ulong? CommitLimitBytes);

/// <summary>
/// The text Report copies for AnyPS5's Game bring-up Discussions: the PC, the game, the exit and the end of the log.
/// Nothing is sent. Its fields name no full path but the log's. See the 7c spec, "Fixes".
/// </summary>
public sealed class CrashReporter(GameLibrary library, AnyPS5Profile profile, ICpuInfo cpu, IVulkanProbeRunner vulkan, IPlatform platform)
{
    public const int LogLines = 30;

    // Lavapipe's manifest named without its folder, so the report holds no path.
    private const string ManifestName = "lvp_icd.x86_64.json";

    /// <param name="diagnosis">Null when nothing matched (diagnose --report).</param>
    /// <exception cref="GameNotFoundException">No game with this title ID.</exception>
    /// <exception cref="InvalidGameRecordException">The game.json is malformed.</exception>
    public async Task<string> BuildAsync(string titleId, string logPath, Diagnosis? diagnosis, CancellationToken cancellationToken)
    {
        var (_, record) = library.Find(titleId);
        var outcome = await vulkan.RunAsync(cancellationToken).ConfigureAwait(false);
        var facts = new CrashReportFacts(
            typeof(CrashReporter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown",
            RuntimeInformation.OSDescription,
            cpu.Read(),
            outcome.Report?.Gpus ?? [],
            outcome.FailureReason,
            GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            platform.GetCommitLimitBytes());
        var lastLines = await LastLinesAsync(logPath, cancellationToken).ConfigureAwait(false);
        return Format(facts, profile.Id, record, diagnosis, logPath, lastLines);
    }

    internal static string Format(CrashReportFacts facts, string profileId, GameRecord record, Diagnosis? diagnosis, string logPath, IReadOnlyList<string> lastLines)
    {
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"PS5Kitsune {facts.AppVersion} on {facts.Os}");
        text.AppendLine(CultureInfo.InvariantCulture, $"CPU: {facts.Cpu.Vendor}{(facts.Cpu.MissingExtensions.Count == 0 ? "" : $", missing {string.Join(", ", facts.Cpu.MissingExtensions)}")}");
        if (facts.Gpus.Count == 0)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"GPU: none detected{(facts.GpuFailure is { } reason ? $" ({reason})" : "")}");
        }

        foreach (var gpu in facts.Gpus)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"GPU: {gpu.Name}, driver {gpu.DriverInfo ?? "unknown"}{(gpu.SupportsAnyPS5 ? "" : " (not supported)")}");
        }

        text.AppendLine(CultureInfo.InvariantCulture, $"Memory: {GiB(facts.RamBytes)} RAM, commit limit {GiB((long?)facts.CommitLimitBytes)}");
        text.AppendLine(CultureInfo.InvariantCulture, $"AnyPS5: {record.ConvertedWith.Mode} {record.ConvertedWith.Version}, profile {profileId}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Game: {record.TitleId} {record.Name}, audit {record.Audit.Implemented} implemented, {record.Audit.Stub} stubs, {record.Audit.Absent} absent");
        var variables = LaunchEnvironment.For(record.Settings, record.Settings.Lavapipe ? ManifestName : null, null)
            .OrderBy(variable => variable.Key, StringComparer.Ordinal)
            .Select(variable => $"{variable.Key}={variable.Value}")
            .ToList();
        text.AppendLine(CultureInfo.InvariantCulture, $"Settings: {(variables.Count == 0 ? "defaults" : string.Join(", ", variables))}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Exit code: {(diagnosis?.ExitCode is { } code ? string.Create(CultureInfo.InvariantCulture, $"0x{code:X8}") : "unknown")}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Diagnosis: {diagnosis?.Id ?? "none"}");
        if (diagnosis?.Line is { } line)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"Matched line {diagnosis.LineNumber}: {line}");
        }

        text.AppendLine(CultureInfo.InvariantCulture, $"Log: {logPath}");
        text.AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"Last {LogLines} log lines:");
        text.AppendLine("```text");
        foreach (var logLine in lastLines)
        {
            text.AppendLine(logLine);
        }

        text.AppendLine("```");
        return text.ToString();
    }

    /// <summary>The last <see cref="LogLines"/> lines; one line saying why when the log cannot be read.</summary>
    internal static async Task<IReadOnlyList<string>> LastLinesAsync(string logPath, CancellationToken cancellationToken)
    {
        try
        {
            var kept = new Queue<string>(LogLines);
            using var reader = new StreamReader(new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                if (kept.Count == LogLines)
                {
                    kept.Dequeue();
                }

                kept.Enqueue(line);
            }

            return [.. kept];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [$"(the log could not be read: {exception.Message})"];
        }
    }

    private static string GiB(long? bytes) =>
        bytes is { } value ? string.Create(CultureInfo.InvariantCulture, $"{value / (double)(1L << 30):0.0} GiB") : "unknown";
}
```

Check while implementing: `LaunchEnvironment.For` returns `IReadOnlyDictionary<string, string>` (7b Refinement 11); `VulkanProbeOutcome` exposes `Report` and `FailureReason`; `VulkanGpu.SupportsAnyPS5` exists; `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes` is a `long`. Adjust only the member access, not the output, if a name differs.

- [ ] **Step 6: Register the fixes**

In `ServiceCollectionExtensions`, after the `CrashDiagnoser` line:

```csharp
        services.AddSingleton<LibsRepair>();
        services.AddSingleton<CrashReporter>();
```

- [ ] **Step 7: Run the tests to verify they pass**

Run each class: `PS5Kitsune.Core.Tests.Diagnostics.LibsRepairTests`, `PS5Kitsune.Core.Tests.Diagnostics.CrashReporterTests`, `PS5Kitsune.Core.Tests.Library.GamePlayerTests`.
Expected: all pass, including the 7b `SaveSettingsAsync` tests.

- [ ] **Step 8: Commit**

```bash
git add src/PS5Kitsune.Core tests/PS5Kitsune.Core.Tests
git commit -m "Add the diagnosis fixes: Repair libs, settings changes and the report

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---
### Task 4: Core — one preparation at a time, and the download leftovers

**Files:**
- Create: `src/PS5Kitsune.Core/Launch/PreparationLock.cs`
- Modify: `src/PS5Kitsune.Core/Launch/LaunchPreparation.cs`, `src/PS5Kitsune.Core/Launch/LaunchProgress.cs`, `src/PS5Kitsune.Core/Tools/ArchiveTool.cs`, `src/PS5Kitsune.Core/Tools/MesaTool.cs`, `src/PS5Kitsune.Core/Tools/FontPack.cs`, `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`, `src/PS5Kitsune.Cli/ConsoleLaunchProgress.cs`, `src/PS5Kitsune.App/ViewModels/GameViewModel.cs`, `tests/PS5Kitsune.App.Tests/Fakes.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Launch/PreparationLockTests.cs`, `tests/PS5Kitsune.Core.Tests/Launch/LaunchPreparationTests.cs`, `tests/PS5Kitsune.Core.Tests/Tools/MesaToolTests.cs`, `tests/PS5Kitsune.Core.Tests/Tools/FontPackTests.cs`, `tests/PS5Kitsune.App.Tests/GameViewModelTests.cs`

**Interfaces:**
- Consumes: `LaunchPreparation`, `ILavapipe`, `IFontPack`, `DataLayout.Tools` (7b).
- Produces: `PreparationLock(DataLayout)` with `Task<IDisposable> AcquireAsync(Action? waiting, CancellationToken)`, `string FilePath`, `const string FileName = ".prepare.lock"`, `TimeSpan RetryDelay { get; init; }` (public, so the App tests can shorten it); `LaunchDownload.Waiting`; `LaunchPreparation(ILavapipe?, IFontPack?, ILogger<LaunchPreparation>, PreparationLock? preparationLock = null)`; `TestLibrary.Lock` (App tests).

- [ ] **Step 1: Write the failing tests**

Create `tests/PS5Kitsune.Core.Tests/Launch/PreparationLockTests.cs`:

```csharp
using PS5Kitsune.Core.Launch;

namespace PS5Kitsune.Core.Tests.Launch;

public sealed class PreparationLockTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // Two instances on the same root stand for two processes: the lock is the file, not the object.
    private PreparationLock Create() => new(new DataLayout(_temp.Path)) { RetryDelay = TimeSpan.FromMilliseconds(20) };

    [Fact]
    public async Task A_second_holder_waits_until_the_first_releases_and_says_so_once()
    {
        var first = await Create().AcquireAsync(null, Token);
        var waited = 0;

        var second = Create().AcquireAsync(() => waited++, Token);
        await Task.Delay(200, Token);

        Assert.False(second.IsCompleted);
        first.Dispose();
        using var held = await second.WaitAsync(TimeSpan.FromSeconds(10), Token);
        Assert.Equal(1, waited);
        Assert.True(File.Exists(_temp.Combine("tools", PreparationLock.FileName)));
    }

    [Fact]
    public async Task Waiting_can_be_cancelled()
    {
        using var first = await Create().AcquireAsync(null, Token);
        using var cancellation = new CancellationTokenSource();

        var second = Create().AcquireAsync(null, cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
    }

    public void Dispose() => _temp.Dispose();
}
```

In `LaunchPreparationTests`, add:

```csharp
    [Fact]
    public async Task A_download_waits_for_another_preparation_and_says_so()
    {
        using var temp = new TempDirectory();
        var preparationLock = new PreparationLock(new DataLayout(temp.Path)) { RetryDelay = TimeSpan.FromMilliseconds(20) };
        var other = await preparationLock.AcquireAsync(null, TestContext.Current.CancellationToken);
        var reports = new List<LaunchProgress>();

        var preparing = new LaunchPreparation(null, _fonts, NullLogger<LaunchPreparation>.Instance, preparationLock)
            .PrepareAsync(new GameSettings(), new SyncProgress(reports.Add), TestContext.Current.CancellationToken);
        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.False(preparing.IsCompleted);
        Assert.Equal([new LaunchProgress(LaunchDownload.Waiting, default)], reports);
        other.Dispose();
        await preparing.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.True(_fonts.Installed);
    }

    [Fact]
    public async Task Nothing_to_download_takes_no_lock()
    {
        using var temp = new TempDirectory();
        var preparationLock = new PreparationLock(new DataLayout(temp.Path));
        using var other = await preparationLock.AcquireAsync(null, TestContext.Current.CancellationToken);
        _fonts.Installed = true;

        await new LaunchPreparation(null, _fonts, NullLogger<LaunchPreparation>.Instance, preparationLock)
            .PrepareAsync(new GameSettings(), null, TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }
```

In `MesaToolTests`, add:

```csharp
    [Fact]
    public async Task Install_deletes_the_downloaded_archive()
    {
        _downloader.Serve(Definition.Url, Archive);

        await CreateTool().InstallAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(File.Exists(_temp.Combine("downloads", "mesa3d-26.2.4-release-msvc.7z")));
    }
```

In `FontPackTests`, add:

```csharp
    [Fact]
    public async Task Install_deletes_part_files_left_by_an_interrupted_download()
    {
        _downloader.Serve(RegularFont.Url, Regular);
        TestFiles.Write(_temp.Combine("fonts", "NotoSans-Old.ttf.part"), "partial");

        await CreatePack(RegularFont).InstallAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(Directory.EnumerateFiles(_temp.Combine("fonts"), "*.part"));
    }
```

In `tests/PS5Kitsune.App.Tests/Fakes.cs`, `TestLibrary`: add a property and pass it to the preparation:

```csharp
    /// <summary>The preparation lock on the test root, retried every 20 ms.</summary>
    public PreparationLock Lock { get; }
```

In the `TestLibrary` constructor, before `Player = …`:

```csharp
        Lock = new PreparationLock(Layout) { RetryDelay = TimeSpan.FromMilliseconds(20) };
```

and change the preparation argument to `new LaunchPreparation(Lavapipe, Fonts, NullLogger<LaunchPreparation>.Instance, Lock)`.

In `GameViewModelTests`, add:

```csharp
    [Fact]
    public async Task A_launch_waiting_for_another_download_says_so()
    {
        _library.Fonts.Installed = false;
        var other = await _library.Lock.AcquireAsync(null, TestContext.Current.CancellationToken);
        var page = await OpenLaunchPageAsync();

        page.OnNavigatedTo();
        await WaitUntilAsync(() => page.StatusText.StartsWith("Waiting", StringComparison.Ordinal));

        Assert.Equal("Waiting for another launch to finish its downloads…", page.StatusText);
        other.Dispose();
        await page.Launching;
        Assert.Equal("Running (PID 4812)", page.StatusText);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests -c Release -- --filter-class "PS5Kitsune.Core.Tests.Launch.PreparationLockTests"`
Expected: build error, `PreparationLock` does not exist.

- [ ] **Step 3: Add the lock and the `Waiting` report**

Create `src/PS5Kitsune.Core/Launch/PreparationLock.cs`:

```csharp
namespace PS5Kitsune.Core.Launch;

/// <summary>
/// &lt;root&gt;\tools\.prepare.lock, held while a launch downloads Mesa or the fonts, so two launches (the app and the CLI, or two games)
/// never install them at once. The OS releases it when its handle closes, even after a crash. See the 7c spec, "PR #10 follow-ups".
/// </summary>
public sealed class PreparationLock(DataLayout layout)
{
    public const string FileName = ".prepare.lock";

    public string FilePath => Path.Combine(layout.Tools, FileName);

    /// <summary>How long to wait before trying again.</summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <param name="waiting">Called once, when another launch holds the lock.</param>
    /// <returns>The held lock: dispose it to release.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled while waiting.</exception>
    public async Task<IDisposable> AcquireAsync(Action? waiting, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(layout.Tools);
        var reported = false;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(FilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException exception) when (IsHeldElsewhere(exception))
            {
                if (!reported)
                {
                    reported = true;
                    waiting?.Invoke();
                }

                await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    // Windows reports a sharing or lock violation. Elsewhere .NET emulates FileShare.None with an advisory lock and throws a plain IOException.
    private static bool IsHeldElsewhere(IOException exception) =>
        exception.HResult is unchecked((int)0x80070020) or unchecked((int)0x80070021)
        || (!OperatingSystem.IsWindows() && exception is not (FileNotFoundException or DirectoryNotFoundException));
}
```

In `LaunchProgress.cs`, add to `LaunchDownload` after `Fonts`:

```csharp
    /// <summary>Another launch holds the preparation lock; the report's progress is empty.</summary>
    Waiting,
```

- [ ] **Step 4: Take the lock in the preparation**

In `LaunchPreparation.cs`, change the primary constructor to:

```csharp
/// <param name="preparationLock">Null in tests that do not need it: downloads are then not serialized.</param>
public sealed partial class LaunchPreparation(ILavapipe? lavapipe, IFontPack? fonts, ILogger<LaunchPreparation> logger, PreparationLock? preparationLock = null)
```

(keep the existing `<param>` lines and add this one). At the start of `PrepareAsync`, after `ArgumentNullException.ThrowIfNull(settings);`:

```csharp
        // Nothing to download takes no lock, so a launch never waits behind another game's download for nothing. Once the lock is held,
        // the checks below run again: the other launch may have installed what this one needed.
        using var held = preparationLock is not null && NeedsDownload(settings)
            ? await preparationLock.AcquireAsync(() => progress?.Report(new LaunchProgress(LaunchDownload.Waiting, default)), cancellationToken).ConfigureAwait(false)
            : null;
```

and add the helper before `IsDownloadFailure`:

```csharp
    private bool NeedsDownload(GameSettings settings)
    {
        if (settings.Lavapipe && lavapipe is not null && !lavapipe.IsInstalled())
        {
            return true;
        }

        try
        {
            return fonts is not null && !fonts.IsInstalled();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // An unreadable marker is handled below like a failed download; taking the lock first is harmless.
            return true;
        }
    }
```

- [ ] **Step 5: Delete the leftovers**

In `ArchiveTool.cs`, add after `RequiredFiles`:

```csharp
    /// <summary>False deletes the downloaded archive once the tool is installed.</summary>
    protected virtual bool KeepsArchive => true;
```

At the end of `InstallAsync`, after `Directory.Move(staging, InstallDirectory);`:

```csharp
        if (!KeepsArchive)
        {
            try
            {
                File.Delete(archive);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // The tool is installed: an archive left behind only costs disk space.
            }
        }
```

In `MesaTool.cs`, after `RequiredFiles`:

```csharp
    // 70 MB that nothing reads once Lavapipe is extracted.
    protected override bool KeepsArchive => false;
```

In `FontPack.InstallAsync`, after the marker is written:

```csharp
        // An interrupted download leaves its .part file; once every font is installed, nothing resumes it.
        foreach (var part in Directory.EnumerateFiles(Folder, "*.part"))
        {
            try
            {
                File.Delete(part);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // The fonts are installed: a file left behind only costs disk space.
            }
        }
```

- [ ] **Step 6: Show the wait in the app and the CLI, and register the lock**

In `GameViewModel.ShowDownload`, after the `if (!IsPreparing)` early return:

```csharp
        if (value.Download == LaunchDownload.Waiting)
        {
            StatusText = "Waiting for another launch to finish its downloads…";
            return;
        }
```

Replace the body of `ConsoleLaunchProgress.Report` with:

```csharp
        if (value.Download != _current)
        {
            _current = value.Download;
            _progress = new ConsoleDownloadProgress();
            Console.WriteLine(value.Download switch
            {
                LaunchDownload.Lavapipe => "Downloading Lavapipe…",
                LaunchDownload.Fonts => "Downloading fonts…",
                _ => "Waiting for another launch to finish its downloads…",
            });
        }

        if (value.Download != LaunchDownload.Waiting)
        {
            _progress.Report(value.Progress);
        }
```

and update its summary to "Prints which download a launch waits for (or that it waits for another launch), then its progress every 10 %."

In `ServiceCollectionExtensions`, before the `LaunchPreparation` registration:

```csharp
        services.AddSingleton<PreparationLock>();
```

and pass `provider.GetRequiredService<PreparationLock>()` as the last argument of `new LaunchPreparation(...)`.

- [ ] **Step 7: Run the tests to verify they pass**

Run the Core classes `PS5Kitsune.Core.Tests.Launch.PreparationLockTests`, `PS5Kitsune.Core.Tests.Launch.LaunchPreparationTests`, `PS5Kitsune.Core.Tests.Tools.MesaToolTests`, `PS5Kitsune.Core.Tests.Tools.FontPackTests`, then `dotnet test --project tests/PS5Kitsune.App.Tests -c Release -- --filter-class "PS5Kitsune.App.Tests.GameViewModelTests"`.
Expected: all pass.

- [ ] **Step 8: Commit**

```bash
git add src tests
git commit -m "Run one launch preparation at a time and delete download leftovers

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 5: Platform — the breakaway log line

**Files:**
- Modify: `src/PS5Kitsune.Platform.Windows/WindowsGameLauncher.cs:51-61, 221-222`
- Test: `tests/PS5Kitsune.Platform.Windows.Tests/WindowsGameLauncherTests.cs`

**Interfaces:**
- Consumes: `WindowsGameLauncher(ILogger<WindowsGameLauncher>)`, `GameLaunchRequest` (7a/7b).
- Produces: nothing new.

- [ ] **Step 1: Write the failing test**

In `WindowsGameLauncherTests`, add `using System.ComponentModel;` and `using Microsoft.Extensions.Logging;`, then:

```csharp
    [Fact]
    public void A_program_that_cannot_start_reports_the_retry_error_and_logs_no_breakaway_line()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        var logger = new CapturingLogger();
        var launcher = new WindowsGameLauncher(logger);

        // CreateProcess refuses a folder with access denied, with or without breakaway: the retry fails too.
        var exception = Assert.Throws<Win32Exception>(() => launcher.Launch(new GameLaunchRequest(_temp.Path, _temp.Path, Logs)));

        Assert.Equal(5, exception.NativeErrorCode);
        Assert.DoesNotContain(logger.Messages, message => message.Contains("breakaway", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class CapturingLogger : ILogger<WindowsGameLauncher>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
```

If the first run shows a native error code other than 5 for a folder, stop and report it (the test's premise is that `CreateProcess` gives 5 here).

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --project tests/PS5Kitsune.Platform.Windows.Tests -c Release -- --filter-class "PS5Kitsune.Platform.Windows.Tests.WindowsGameLauncherTests"`
Expected: the new test fails: the message list holds the current "…does not allow breakaway…" line.

- [ ] **Step 3: Log the fallback only once it worked**

Replace the `catch` block in `Launch`:

```csharp
        catch (Win32Exception exception) when (exception.NativeErrorCode == ErrorAccessDenied)
        {
            // Access denied comes from a job that forbids breakaway (a terminal, another launcher), and also from a program that cannot run
            // at all: only a retry without breakaway tells them apart, and when it fails too, its error is the one reported.
            process = Start(request.Executable, commandLine, request.WorkingDirectory, log.DangerousGetHandle(), environment, breakaway: false);
            var executable = request.Executable;
            LogBreakawayRefused(executable);
        }
```

and the message:

```csharp
    [LoggerMessage(Level = LogLevel.Information, Message = "Started {Executable} inside the launcher's job (breakaway refused): closing that job also ends the game.")]
    private partial void LogBreakawayRefused(string executable);
```

- [ ] **Step 4: Run the tests to verify they pass**

Run the same class. Expected: all pass (the breakaway test still skips inside a job, as in 7b).

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Platform.Windows tests/PS5Kitsune.Platform.Windows.Tests
git commit -m "Log the breakaway fallback only once the retry has started the game

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 6: CLI — the diagnosis in `run`, and `diagnose`

**Files:**
- Create: `src/PS5Kitsune.Cli/DiagnoseCommand.cs`
- Modify: `src/PS5Kitsune.Cli/RunCommand.cs`, `src/PS5Kitsune.Cli/CliLog.cs`, `src/PS5Kitsune.Cli/Program.cs`

**Interfaces:**
- Consumes: `CrashDiagnoser.FinishAsync`, `CrashDiagnoser.DiagnoseAsync`, `DiagnosisTexts.Lines` (Task 2); `CrashReporter.BuildAsync` (Task 3); `GameLibrary.Find`, `GameLayout.Logs`, `CommandHost.RunAsync` (existing).
- Produces: `ps5kitsune diagnose <titleId> [--report]`.

There is no CLI test project: the texts are tested in Core (`DiagnosisTextsTests`), and this task ends with a manual run.

- [ ] **Step 1: Add the log messages**

At the end of `CliLog`:

```csharp
    [LoggerMessage(Level = LogLevel.Warning, Message = "{Line}")]
    public static partial void DiagnosisLine(this ILogger logger, string line);

    [LoggerMessage(Level = LogLevel.Information, Message = "No known error in {Log}.")]
    public static partial void NoKnownError(this ILogger logger, string log);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Name} has no log yet. Launch it first.")]
    public static partial void NoLogYet(this ILogger logger, string name);
```

- [ ] **Step 2: Diagnose after `run`**

In `RunCommand`, add `using PS5Kitsune.Core.Diagnostics;` and replace the lines after `var exitCode = await exit;` up to `return exitCode;` with:

```csharp
                var exitCode = await exit;
                player.Forget(playing);
                logger.GameExited(record.Name, exitCode);
                // The exit is seen: it goes to the log, then the log is searched. Ctrl+C below saw no exit and writes nothing.
                var diagnosis = await services.GetRequiredService<CrashDiagnoser>().FinishAsync(launched.LogFile, exitCode, stopped: false, CancellationToken.None);
                if (diagnosis is not null)
                {
                    foreach (var line in DiagnosisTexts.Lines(diagnosis, record.Name, record.TitleId))
                    {
                        logger.DiagnosisLine(line);
                    }
                }

                return exitCode;
```

- [ ] **Step 3: Add `diagnose`**

Create `src/PS5Kitsune.Cli/DiagnoseCommand.cs`:

```csharp
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core.Diagnostics;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Cli;

internal static class DiagnoseCommand
{
    public static Command Create(Option<DirectoryInfo?> rootOption)
    {
        var titleIdArgument = new Argument<string>("titleId") { Description = "Title ID of an added game, for example PPSA99666." };
        var reportOption = new Option<bool>("--report") { Description = "Print the text to paste in a new Game bring-up thread instead." };
        var command = new Command("diagnose", "Look for a known AnyPS5 error in a game's last log and say how to fix it.") { titleIdArgument, reportOption };
        command.SetAction((parseResult, cancellationToken) => CommandHost.RunAsync(parseResult.GetValue(rootOption), "diagnose", async (services, logger) =>
        {
            // An unknown title ID throws GameNotFoundException: CommandHost prints it and exits with 1.
            var (game, record) = services.GetRequiredService<GameLibrary>().Find(parseResult.GetRequiredValue(titleIdArgument));

            // Log names are yyyyMMdd-HHmmss.log: the last in ordinal order is the newest.
            var log = Directory.Exists(game.Logs) ? Directory.EnumerateFiles(game.Logs, "*.log").Order(StringComparer.Ordinal).LastOrDefault() : null;
            if (log is null)
            {
                var name = record.Name;
                logger.NoLogYet(name);
                return 1;
            }

            var diagnosis = await services.GetRequiredService<CrashDiagnoser>().DiagnoseAsync(log, null, cancellationToken);
            if (parseResult.GetValue(reportOption))
            {
                // Printed as is, without a log prefix, so it pastes cleanly.
                Console.Write(await services.GetRequiredService<CrashReporter>().BuildAsync(record.TitleId, log, diagnosis, cancellationToken));
                return 0;
            }

            if (diagnosis is null)
            {
                logger.NoKnownError(log);
                return 0;
            }

            foreach (var line in DiagnosisTexts.Lines(diagnosis, record.Name, record.TitleId))
            {
                logger.DiagnosisLine(line);
            }

            return 0;
        }));
        return command;
    }
}
```

In `Program.cs`, add `DiagnoseCommand.Create(rootOption),` after `SettingsCommand.Create(rootOption),`.

- [ ] **Step 4: Build and run it by hand**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: 0 warnings, 0 errors.

Run (read-only, on the data root the app uses): `src/PS5Kitsune.Cli/bin/Release/net10.0/ps5kitsune.exe diagnose PPSA99666`
Expected: either "No known error in …\.ps5kitsune\logs\<newest>.log." or a diagnosis with its lines; exit code 0. Then `… diagnose PPSA99666 --report`: the report text, ending with the fenced last log lines. Then `… diagnose PPSA00000`: the existing "not found" error, exit code 1. If DOOM is not added on this machine, run only the last command and say so in the report.

- [ ] **Step 5: Run the full suite**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: 0 failed.

- [ ] **Step 6: Commit**

```bash
git add src/PS5Kitsune.Cli
git commit -m "Print the diagnosis after run, and add the diagnose command

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---
### Task 7: App — the diagnosis panel on the Game page

**Files:**
- Create: `src/PS5Kitsune.App/ViewModels/DiagnosisServices.cs`, `src/PS5Kitsune.App/ViewModels/DiagnosisViewModel.cs`
- Modify: `src/PS5Kitsune.App/ViewModels/GameViewModel.cs`, `src/PS5Kitsune.App/Views/GameView.axaml`, `src/PS5Kitsune.App/Navigation/IDialogService.cs`, `src/PS5Kitsune.App/Shell/AvaloniaDialogService.cs`, `src/PS5Kitsune.App/Shell/PageFactory.cs`, `tests/PS5Kitsune.App.Tests/Fakes.cs`
- Test: `tests/PS5Kitsune.App.Tests/DiagnosisViewModelTests.cs`, `tests/PS5Kitsune.App.Tests/GameViewModelTests.cs`

**Interfaces:**
- Consumes: `CrashDiagnoser.FinishAsync`, `Diagnosis`, `DiagnosisTexts.For`, `DiagnosisAction`, `SettingVariables.Find`, `DiagnosisTexts.NvidiaDriversUrl` (Task 2); `LibsRepair.RepairAsync`, `LibsSourceChangedException`, `GamePlayer.UpdateSettingsAsync`, `CrashReporter.BuildAsync` (Task 3); `IPlatform.OpenFix(CheckFix.OpenPerformanceSettings)`, `IPlatform.OpenPath`, `PathOpener.TryOpen` (existing).
- Produces: `DiagnosisServices(CrashDiagnoser Diagnoser, LibsRepair Libs, CrashReporter Reporter, IDialogService Dialogs, Uri? ReportUrl)`; `DiagnosisViewModel(Diagnosis, GameLayout, string name, GamePlayer, IPlatform, DiagnosisServices)` with `Title`, `Explanation`, `LineText`, `ActionText`, `HasAction`, `Result`, `IsFixing`, `FixCommand`; `GameViewModel.Diagnosis` (`DiagnosisViewModel?`); `IDialogService.CopyTextAsync(string)`; in the App tests `TestLibrary.Diagnosis(Uri? reportUrl = null)`, `TestLibrary.Dialogs`, `TestLibrary.Paths`, `FakeCpuInfo`.

- [ ] **Step 1: Give the fakes a clipboard, a CPU and the diagnosis services**

In `tests/PS5Kitsune.App.Tests/Fakes.cs`:

`FakeDialogs` gains:

```csharp
    public List<string> Copied { get; } = [];

    /// <summary>Thrown by CopyTextAsync when set.</summary>
    public Exception? CopyFailure { get; set; }

    public Task CopyTextAsync(string text)
    {
        if (CopyFailure is not null)
        {
            throw CopyFailure;
        }

        Copied.Add(text);
        return Task.CompletedTask;
    }
```

A new fake, next to `FakeDialogs`:

```csharp
internal sealed class FakeCpuInfo : ICpuInfo
{
    public CpuFacts Read() => new("AuthenticAMD", []);
}
```

`TestLibrary` gains (with `using PS5Kitsune.Core.Diagnostics;` and `using PS5Kitsune.Core.Profiles;` at the top of the file if missing):

```csharp
    public AnyPS5Paths Paths => new(Layout, Platform);

    public FakeDialogs Dialogs { get; } = new();

    /// <summary>The real diagnoser with the embedded profile's rules, and the real fixes on this library.</summary>
    public DiagnosisServices Diagnosis(Uri? reportUrl = null) => new(
        new CrashDiagnoser(ProfileLoader.LoadEmbedded(), NullLogger<CrashDiagnoser>.Instance),
        new LibsRepair(Library, Installer, Paths, Running),
        new CrashReporter(Library, ProfileLoader.LoadEmbedded(), new FakeCpuInfo(), new FakeVulkanProbeRunner(), Platform),
        Dialogs,
        reportUrl);
```

- [ ] **Step 2: Write the failing tests**

Create `tests/PS5Kitsune.App.Tests/DiagnosisViewModelTests.cs`:

```csharp
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Diagnostics;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.App.Tests;

public sealed class DiagnosisViewModelTests : IDisposable
{
    private static readonly Uri ReportUrl = new("https://github.com/boykopovar/AnyPS5/discussions/categories/game-bring-up");

    private readonly TestLibrary _library = new();

    private static Diagnosis Of(string id, string? variable = null) => new(
        id,
        variable is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["variable"] = variable },
        "the line",
        4,
        1,
        "missing.log");

    private async Task<DiagnosisViewModel> OpenAsync(Diagnosis diagnosis)
    {
        var game = await _library.AddAsync("PPSA99666", "DOOM");
        return new DiagnosisViewModel(diagnosis, game, "DOOM", _library.Player, _library.Platform, _library.Diagnosis(ReportUrl));
    }

    private GameSettings Saved() => _library.Library.Find("PPSA99666").Record.Settings;

    [Fact]
    public async Task Shows_the_explanation_the_line_and_the_action()
    {
        var panel = await OpenAsync(Of(DiagnosisIds.GpuFeatures));

        Assert.Equal("No GPU here supports AnyPS5", panel.Title);
        Assert.Equal("The graphics driver lacks buffer device address or shaderInt64. CPU rendering works on any PC, more slowly.", panel.Explanation);
        Assert.Equal("Line 4: the line", panel.LineText);
        Assert.Equal("Turn on CPU rendering", panel.ActionText);
        Assert.True(panel.FixCommand.CanExecute(null));
    }

    [Fact]
    public async Task Turn_on_cpu_rendering_saves_lavapipe()
    {
        var panel = await OpenAsync(Of(DiagnosisIds.GpuFeatures));

        await panel.FixCommand.ExecuteAsync(null);

        Assert.True(Saved().Lavapipe);
        Assert.Equal("CPU rendering is on. Launch again to download Mesa and start the game.", panel.Result);
    }

    [Fact]
    public async Task Reset_puts_back_only_the_refused_setting()
    {
        var panel = await OpenAsync(Of(DiagnosisIds.InvalidSetting, "ANYPS5_LANGUAGE"));
        await _library.Player.SaveSettingsAsync("PPSA99666", new GameSettings { Language = 5, Overlay = true }, TestContext.Current.CancellationToken);

        await panel.FixCommand.ExecuteAsync(null);

        Assert.Equal(new GameSettings { Overlay = true }, Saved());
        Assert.Equal("Game language reset. Launch again to check.", panel.Result);
    }

    [Fact]
    public async Task Repair_libs_copies_them_from_the_install()
    {
        var source = _library.Paths.For(AnyPS5Mode.Release).Libs;
        Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "libwinpthread-1.dll"), "dll", TestContext.Current.CancellationToken);
        var panel = await OpenAsync(Of(DiagnosisIds.LibsMissingDll));

        await panel.FixCommand.ExecuteAsync(null);

        Assert.True(File.Exists(Path.Combine(_library.Game("PPSA99666").Libs, "libwinpthread-1.dll")));
        Assert.Equal("Libs repaired. Launch again to check.", panel.Result);
        Assert.False(panel.IsFixing);
    }

    [Fact]
    public async Task Repair_libs_is_refused_while_the_game_runs()
    {
        var panel = await OpenAsync(Of(DiagnosisIds.LibsMissingDll));
        await _library.Player.LaunchAsync("PPSA99666", TestContext.Current.CancellationToken);

        await panel.FixCommand.ExecuteAsync(null);

        Assert.Equal("DOOM is running. Stop it first.", panel.Result);
    }

    [Fact]
    public async Task Repair_libs_after_an_update_says_to_reconvert()
    {
        var panel = await OpenAsync(Of(DiagnosisIds.LibsNotDll));
        _library.Installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, TestLibrary.Release with { Version = "v0.1.3" });

        await panel.FixCommand.ExecuteAsync(null);

        Assert.Equal("The release install has changed since DOOM was converted. Reconvert it instead.", panel.Result);
    }

    [Fact]
    public async Task Report_copies_the_text_then_opens_the_bring_up_page()
    {
        var panel = await OpenAsync(Of(DiagnosisIds.Unexplained));

        await panel.FixCommand.ExecuteAsync(null);

        Assert.Contains("Diagnosis: unexplained", Assert.Single(_library.Dialogs.Copied), StringComparison.Ordinal);
        Assert.Equal([ReportUrl.AbsoluteUri], _library.Platform.OpenedPaths);
        Assert.Equal("Report copied. Paste it in a new thread for PPSA99666.", panel.Result);
    }

    [Fact]
    public async Task A_failed_copy_points_to_the_cli()
    {
        var panel = await OpenAsync(Of(DiagnosisIds.NotImplemented));
        _library.Dialogs.CopyFailure = new InvalidOperationException("No clipboard");

        await panel.FixCommand.ExecuteAsync(null);

        Assert.Equal("Could not copy the report: No clipboard. Use ps5kitsune diagnose PPSA99666 --report.", panel.Result);
        Assert.Empty(_library.Platform.OpenedPaths);
    }

    [Fact]
    public async Task Open_page_file_settings_opens_them_or_says_why_not()
    {
        var panel = await OpenAsync(Of(DiagnosisIds.CommitLimit));

        await panel.FixCommand.ExecuteAsync(null);
        Assert.Equal([CheckFix.OpenPerformanceSettings], _library.Platform.OpenedFixes);
        Assert.Null(panel.Result);

        _library.Platform.OpenFailure = new InvalidOperationException("blocked");
        await panel.FixCommand.ExecuteAsync(null);
        Assert.Equal("Could not open the page file settings: blocked", panel.Result);
    }

    [Fact]
    public async Task An_unsupported_cpu_has_no_action()
    {
        var panel = await OpenAsync(Of(DiagnosisIds.CpuTooOld));

        Assert.False(panel.HasAction);
        Assert.False(panel.FixCommand.CanExecute(null));
    }

    public void Dispose() => _library.Dispose();
}
```

In `GameViewModelTests`, change the `LaunchAsync` helper to take the services:

```csharp
    private async Task<(GameViewModel Page, FakeLaunchedGame Process)> LaunchAsync(bool firstLaunch = true, bool diagnose = false)
    {
        await _library.AddAsync("PPSA99666", "DOOM");
        var playing = await _library.Player.LaunchAsync("PPSA99666", TestContext.Current.CancellationToken);
        var page = new GameViewModel(
            playing, firstLaunch, _library.Player, _library.Platform, new InlineDispatcher(), _navigator, new FakePages(), null, diagnose ? _library.Diagnosis() : null);
        return (page, (FakeLaunchedGame)playing.Process);
    }
```

and add:

```csharp
    [Fact]
    public async Task An_exit_with_a_known_error_shows_its_diagnosis_and_writes_the_exit_line()
    {
        var (page, process) = await LaunchAsync(diagnose: true);
        await File.AppendAllTextAsync(process.LogFile, "Vulkan: buffer device address and shaderInt64 are required\n", TestContext.Current.CancellationToken);
        process.Exit(1);

        page.OnNavigatedTo();
        await FinishedAsync(page);

        Assert.Equal("No GPU here supports AnyPS5", page.Diagnosis?.Title);
        Assert.Equal("Exited with code 1", page.StatusText);
        Assert.Equal("[ps5kitsune] exited with code 1 (0x00000001)", (await File.ReadAllLinesAsync(process.LogFile, TestContext.Current.CancellationToken))[^1]);
    }

    [Fact]
    public async Task A_clean_exit_shows_no_diagnosis()
    {
        var (page, process) = await LaunchAsync(diagnose: true);
        process.Exit(0);

        page.OnNavigatedTo();
        await FinishedAsync(page);

        Assert.Null(page.Diagnosis);
    }

    [Fact]
    public async Task A_game_ended_with_stop_is_not_diagnosed()
    {
        var (page, process) = await LaunchAsync(diagnose: true);
        await File.AppendAllTextAsync(process.LogFile, "Vulkan: buffer device address and shaderInt64 are required\n", TestContext.Current.CancellationToken);
        page.OnNavigatedTo();

        await page.StopCommand.ExecuteAsync(null);
        await FinishedAsync(page);

        Assert.Null(page.Diagnosis);
        Assert.Equal("[ps5kitsune] stopped by the user (exit code 1)", (await File.ReadAllLinesAsync(process.LogFile, TestContext.Current.CancellationToken))[^1]);
    }

    [Fact]
    public async Task Launch_again_hides_the_diagnosis()
    {
        var (page, process) = await LaunchAsync(diagnose: true);
        process.Exit(1);
        page.OnNavigatedTo();
        await FinishedAsync(page);
        Assert.NotNull(page.Diagnosis);

        await page.LaunchAgainCommand.ExecuteAsync(null);

        Assert.Null(page.Diagnosis);
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test --project tests/PS5Kitsune.App.Tests -c Release -- --filter-class "PS5Kitsune.App.Tests.DiagnosisViewModelTests"`
Expected: build error, `DiagnosisViewModel` does not exist.

- [ ] **Step 4: Add the clipboard to the dialog service**

In `IDialogService`, add:

```csharp
    /// <exception cref="InvalidOperationException">There is no window, so no clipboard.</exception>
    Task CopyTextAsync(string text);
```

In `AvaloniaDialogService` (add `using Avalonia.Input.Platform;`, which holds the `SetTextAsync` extension):

```csharp
    public async Task CopyTextAsync(string text)
    {
        var clipboard = owner()?.Clipboard ?? throw new InvalidOperationException("There is no window to copy from.");
        await clipboard.SetTextAsync(text).ConfigureAwait(true);
    }
```

- [ ] **Step 5: Add the panel's view model**

Create `src/PS5Kitsune.App/ViewModels/DiagnosisServices.cs`:

```csharp
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Diagnostics;

namespace PS5Kitsune.App.ViewModels;

/// <summary>What the Game page needs to diagnose an exit and apply its fix. See the 7c spec, "Game page".</summary>
/// <param name="ReportUrl">The page Report opens after copying its text; null copies only.</param>
public sealed record DiagnosisServices(CrashDiagnoser Diagnoser, LibsRepair Libs, CrashReporter Reporter, IDialogService Dialogs, Uri? ReportUrl);
```

Create `src/PS5Kitsune.App/ViewModels/DiagnosisViewModel.cs`:

```csharp
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Diagnostics;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.App.ViewModels;

/// <summary>The Game page's panel after an exit with a known error: the explanation, the matched line and its fix. See the 7c spec, "Game page".</summary>
public sealed partial class DiagnosisViewModel : ObservableObject
{
    private readonly Diagnosis _diagnosis;
    private readonly GameLayout _game;
    private readonly string _name;
    private readonly GamePlayer _player;
    private readonly IPlatform _platform;
    private readonly DiagnosisServices _services;
    private readonly DiagnosisText _text;

    public DiagnosisViewModel(Diagnosis diagnosis, GameLayout game, string name, GamePlayer player, IPlatform platform, DiagnosisServices services)
    {
        _diagnosis = diagnosis;
        _game = game;
        _name = name;
        _player = player;
        _platform = platform;
        _services = services;
        _text = DiagnosisTexts.For(diagnosis);
        LineText = diagnosis.Line is { } line ? string.Create(CultureInfo.InvariantCulture, $"Line {diagnosis.LineNumber}: {line}") : null;
    }

    public string Title => _text.Title;

    public string Explanation => _text.Explanation;

    /// <summary>"Line n: …"; null when only the exit code matched.</summary>
    public string? LineText { get; }

    public string? ActionText => _text.ActionText;

    public bool HasAction => _text.Action != DiagnosisAction.None;

    /// <summary>What the fix did, or why it failed; null before it ran, and after a fix that only opens something.</summary>
    [ObservableProperty]
    public partial string? Result { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FixCommand))]
    public partial bool IsFixing { get; private set; }

    private bool CanFix => HasAction && !IsFixing;

    [RelayCommand(CanExecute = nameof(CanFix))]
    private async Task FixAsync()
    {
        IsFixing = true;
        Result = null;
        try
        {
            Result = await RunFixAsync().ConfigureAwait(true);
        }
        finally
        {
            IsFixing = false;
        }
    }

    private async Task<string?> RunFixAsync()
    {
        switch (_text.Action)
        {
            case DiagnosisAction.RepairLibs:
                return await RepairLibsAsync().ConfigureAwait(true);
            case DiagnosisAction.TurnOnLavapipe:
                return await ChangeSettingsAsync(
                    settings => settings with { Lavapipe = true }, "CPU rendering is on. Launch again to download Mesa and start the game.").ConfigureAwait(true);
            case DiagnosisAction.ResetSetting when SettingVariables.Find(_diagnosis.Capture("variable")) is { } setting:
                var label = char.ToUpperInvariant(setting.Label[0]) + setting.Label[1..];
                return await ChangeSettingsAsync(setting.Reset, $"{label} reset. Launch again to check.").ConfigureAwait(true);
            case DiagnosisAction.OpenPageFileSettings:
                return Open("the page file settings", () => _platform.OpenFix(CheckFix.OpenPerformanceSettings));
            case DiagnosisAction.OpenNvidiaDrivers:
                return Open("the NVIDIA drivers page", () => _platform.OpenPath(DiagnosisTexts.NvidiaDriversUrl));
            case DiagnosisAction.OpenFolder:
                return PathOpener.TryOpen(_platform, _game.Root);
            case DiagnosisAction.Report:
                return await ReportAsync().ConfigureAwait(true);
            default:
                return null;
        }
    }

    private async Task<string> RepairLibsAsync()
    {
        try
        {
            await Task.Run(() => _services.Libs.RepairAsync(_game.TitleId, CancellationToken.None)).ConfigureAwait(true);
            return "Libs repaired. Launch again to check.";
        }
        catch (LibsSourceChangedException exception)
        {
            return exception.Message;
        }
        catch (Exception exception) when (exception is GameRunningException or GameBusyException)
        {
            return Refused(exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or GameNotFoundException or InvalidGameRecordException)
        {
            return $"Could not repair libs: {exception.Message}";
        }
    }

    private async Task<string> ChangeSettingsAsync(Func<GameSettings, GameSettings> change, string done)
    {
        try
        {
            await Task.Run(() => _player.UpdateSettingsAsync(_game.TitleId, change, CancellationToken.None)).ConfigureAwait(true);
            return done;
        }
        catch (Exception exception) when (exception is GameRunningException or GameBusyException)
        {
            return Refused(exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or GameNotFoundException or InvalidGameRecordException)
        {
            return $"Could not save the settings of {_name}: {exception.Message}";
        }
    }

    private async Task<string> ReportAsync()
    {
        var titleId = _game.TitleId;
        try
        {
            var text = await Task.Run(() => _services.Reporter.BuildAsync(titleId, _diagnosis.LogPath, _diagnosis, CancellationToken.None)).ConfigureAwait(true);
            await _services.Dialogs.CopyTextAsync(text).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return $"Could not copy the report: {exception.Message}. Use ps5kitsune diagnose {titleId} --report.";
        }

        if (_services.ReportUrl is { } url && Open("the Game bring-up page", () => _platform.OpenPath(url.AbsoluteUri)) is { } failure)
        {
            return failure;
        }

        return $"Report copied. Paste it in a new thread for {titleId}.";
    }

    private string Refused(Exception exception) => exception is GameRunningException
        ? $"{_name} is running. Stop it first."
        : $"{_name} is busy with another action. Try again.";

    private static string? Open(string what, Action open)
    {
        try
        {
            open();
            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return $"Could not open {what}: {exception.Message}";
        }
    }
}
```

- [ ] **Step 6: Diagnose on the Game page**

In `GameViewModel.cs` (add `using PS5Kitsune.Core.Diagnostics;` only if needed by the code below):

1. Both constructors gain a last parameter `DiagnosisServices? diagnosis = null`; the first passes it on to the second (`: this(playing.Game, …, pages, logger, diagnosis)`), the second stores it: `_diagnosis = diagnosis;`. Add the fields:

```csharp
    private readonly DiagnosisServices? _diagnosis;
    private bool _stopped;
    private int _launches;
```

and update the class summary to "…follows its log live, Stop, then its exit code and, after an error AnyPS5 is known to print, its diagnosis."

2. Add the property after `Message`:

```csharp
    /// <summary>The known error of the last exit this page watched; null otherwise, and once Launch again starts.</summary>
    [ObservableProperty]
    public partial DiagnosisViewModel? Diagnosis { get; private set; }
```

3. At the start of `LaunchAsync`, before `var previousStatus = StatusText;`:

```csharp
        _launches++;
        _stopped = false;
        Diagnosis = null;
```

4. In `FollowAsync`, after `StatusText = string.Create(CultureInfo.InvariantCulture, $"Exited with code {exitCode}");`:

```csharp
            if (_diagnosis is { } services)
            {
                var launch = _launches;
                var stopped = _stopped;
                var logFile = playing.Process.LogFile;
                var found = await Task.Run(() => services.Diagnoser.FinishAsync(logFile, exitCode, stopped, CancellationToken.None), CancellationToken.None)
                    .ConfigureAwait(true);

                // A launch started meanwhile owns the page.
                if (found is not null && launch == _launches)
                {
                    Diagnosis = new DiagnosisViewModel(found, _game, Title, _player, _platform, services);
                }
            }
```

5. In `StopAsync`, set `_stopped = true;` just before `await playing.Process.StopAsync()`, and `_stopped = false;` first thing in its `catch` (the game keeps running, so its later exit is diagnosed).

- [ ] **Step 7: Show the panel**

In `GameView.axaml`, after the `Message` `TextBlock` (inside the top `StackPanel`):

```xml
      <Border IsVisible="{Binding Diagnosis, Converter={x:Static ObjectConverters.IsNotNull}}"
              Padding="10" CornerRadius="4" BorderThickness="1" BorderBrush="#C42B1C">
        <StackPanel DataContext="{Binding Diagnosis}" x:DataType="vm:DiagnosisViewModel" Spacing="6">
          <TextBlock Text="{Binding Title}" FontWeight="SemiBold" />
          <TextBlock Text="{Binding Explanation}" TextWrapping="Wrap" />
          <TextBlock Text="{Binding LineText}" IsVisible="{Binding LineText, Converter={x:Static ObjectConverters.IsNotNull}}"
                     FontFamily="Cascadia Mono,Consolas,monospace" FontSize="12" TextWrapping="Wrap" />
          <StackPanel Orientation="Horizontal" Spacing="8">
            <Button Content="{Binding ActionText}" Command="{Binding FixCommand}" IsVisible="{Binding HasAction}" />
            <TextBlock Text="{Binding Result}" IsVisible="{Binding Result, Converter={x:Static ObjectConverters.IsNotNull}}"
                       VerticalAlignment="Center" TextWrapping="Wrap" />
          </StackPanel>
        </StackPanel>
      </Border>
```

- [ ] **Step 8: Pass the services from the page factory**

In `PageFactory.cs` (add `using PS5Kitsune.Core.Diagnostics;` and `using PS5Kitsune.Core.Profiles;` if missing), pass `Diagnosis()` as the last argument of both `new GameViewModel(...)` calls and add:

```csharp
    private DiagnosisServices Diagnosis() => new(
        Session.Get<CrashDiagnoser>(), Session.Get<LibsRepair>(), Session.Get<CrashReporter>(), dialogs, Session.Get<AnyPS5Profile>().Diagnostics.ReportUrl);
```

- [ ] **Step 9: Run the tests to verify they pass**

Run: `dotnet test --project tests/PS5Kitsune.App.Tests -c Release -- --filter-class "PS5Kitsune.App.Tests.DiagnosisViewModelTests"`, then `PS5Kitsune.App.Tests.GameViewModelTests` and `PS5Kitsune.App.Tests.PageFactoryTests`.
Expected: all pass.

- [ ] **Step 10: Check the panel renders**

Run the full suite: `dotnet test --solution PS5Kitsune.slnx -c Release` (0 failed), then build the App and start it (`src/PS5Kitsune.App/bin/Release/net10.0/PS5Kitsune.exe`) only if a compiled-binding error could hide at runtime: compiled bindings fail at build time, so a clean build is the check. Do not launch a game.

- [ ] **Step 11: Commit**

```bash
git add src/PS5Kitsune.App tests/PS5Kitsune.App.Tests
git commit -m "Show the diagnosis and its fix on the Game page after an exit

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 8: App — the Settings page notices the game has stopped

**Files:**
- Modify: `src/PS5Kitsune.App/ViewModels/GameSettingsViewModel.cs`
- Test: `tests/PS5Kitsune.App.Tests/GameSettingsViewModelTests.cs`

**Interfaces:**
- Consumes: `GamePlayer.Attach(string titleId) : PlayingGame?` (7b), `ILaunchedGame.WaitForExitAsync`.
- Produces: `GameSettingsViewModel.Watching` (`Task`, completes when the page stops watching the running game).

- [ ] **Step 1: Write the failing tests**

In `GameSettingsViewModelTests`, add:

```csharp
    [Fact]
    public async Task The_page_becomes_editable_when_the_running_game_exits()
    {
        await _library.AddAsync("PPSA99666", "DOOM");
        var playing = await _library.Player.LaunchAsync("PPSA99666", TestContext.Current.CancellationToken);
        var page = await OpenAsync();
        Assert.True(page.IsRunning);

        ((FakeLaunchedGame)playing.Process).Exit(0);
        await page.Watching.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.False(page.IsRunning);
        Assert.True(page.CanEdit);
        Assert.True(page.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_refused_save_clears_once_the_game_exits()
    {
        var page = await OpenAsync();
        var playing = await _library.Player.LaunchAsync("PPSA99666", TestContext.Current.CancellationToken);
        page.Overlay = true;

        await page.SaveCommand.ExecuteAsync(null);
        Assert.Equal("DOOM is running. Stop it to change its settings.", page.Message);
        ((FakeLaunchedGame)playing.Process).Exit(0);
        await page.Watching.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.False(page.IsRunning);
        Assert.Null(page.Message);
    }
```

(`page.Overlay` is the 7b settable property bound to "Performance overlay"; use whichever editable property the page exposes if its name differs.)

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/PS5Kitsune.App.Tests -c Release -- --filter-class "PS5Kitsune.App.Tests.GameSettingsViewModelTests"`
Expected: build error, `Watching` does not exist.

- [ ] **Step 3: Watch the running game**

In `GameSettingsViewModel`, add a field and a property:

```csharp
    private CancellationTokenSource? _watch;

    /// <summary>Completes when the page stops watching the running game: it exited, or the page was left.</summary>
    public Task Watching { get; private set; } = Task.CompletedTask;
```

the helpers:

```csharp
    private const string RunningMessageEnd = " is running. Stop it to change its settings.";

    // Settings only apply at launch, so a running game makes the page read-only until it exits.
    private void StartWatching()
    {
        if (_watch is not null)
        {
            return;
        }

        _watch = new CancellationTokenSource();
        Watching = WatchAsync(_watch.Token);
    }

    private async Task WatchAsync(CancellationToken cancellationToken)
    {
        try
        {
            var playing = await Task.Run(() => player.Attach(titleId), cancellationToken).ConfigureAwait(true);
            if (playing is not null)
            {
                using (playing.Process)
                {
                    await playing.Process.WaitForExitAsync(cancellationToken).ConfigureAwait(true);
                }
            }

            IsRunning = false;
            if (Message?.EndsWith(RunningMessageEnd, StringComparison.Ordinal) == true)
            {
                Message = null;
            }
        }
        catch (OperationCanceledException)
        {
            // The page was left.
        }
        catch (Exception exception) when (exception is GameNotFoundException or InvalidGameRecordException or IOException or UnauthorizedAccessException)
        {
            // The game can no longer be read: the page stays read-only, as when it opened.
        }
        finally
        {
            _watch?.Dispose();
            _watch = null;
        }
    }
```

Use `$"{_name}{RunningMessageEnd}"` for the message in `SaveAsync`'s `GameRunningException` branch (same text as before), and call `StartWatching();` right after it. In `LoadAsync`, after `IsRunning = isRunning;`:

```csharp
            if (isRunning)
            {
                StartWatching();
            }
```

In `BackAsync`, before `navigator.GoBack();`:

```csharp
        if (_watch is { } watch)
        {
            await watch.CancelAsync().ConfigureAwait(true);
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run the same class. Expected: all pass, including the 7b `A_running_game_is_read_only`.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.App/ViewModels/GameSettingsViewModel.cs tests/PS5Kitsune.App.Tests/GameSettingsViewModelTests.cs
git commit -m "Make the Settings page editable again once the running game exits

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 9: Docs

**Files:**
- Modify: `docs/architecture.md`, `README.md`

- [ ] **Step 1: Update the architecture**

In `docs/architecture.md`:

1. **"Crash diagnosis" section:** keep the opening sentence, then replace the table with the 7c one: columns "Rule id", "Pattern in the log (or exit code)", "Explanation", "Action", one row per id of the spec's Game page table, the patterns as in `default-profile.json`. Add after the table:
   - "Rules live in the profile's `diagnostics` section (`reportUrl`, `rules`: `id`, `log` regexes, `exitCodes`, `match` `all`/`any`, `captures`); the first rule met, in profile order, wins. An id the app does not know is skipped with a warning; an invalid rule makes the profile invalid."
   - "After the exit it watched, the Game page or `run` appends `[ps5kitsune] exited with code N (0x…)` to the launch's log (`stopped by the user` after Stop, which is never diagnosed). With no match, a non-zero exit is `unexplained` and offers Report."
   - "Report copies a text (PC, AnyPS5, game, settings, exit, matched line, last 30 log lines, log path) and opens the Game bring-up Discussions; nothing is sent. `ps5kitsune diagnose <titleId> [--report]` does the same from the game's newest log."
2. **Roadmap, 7c entry:** add "Spec: [milestone 7c](superpowers/specs/2026-10-25-milestone-7c-crash-diagnosis-design.md)." and a one-line summary: "Rules in the profile, the exit line, the Game page panel with its fixes (Repair libs, CPU rendering, Reset a setting, page file settings, NVIDIA drivers, Report), `diagnose`; one launch preparation at a time."
3. **Roadmap, 7b criterion line:** replace "on the Iris Xe" with "with Lavapipe forced on (neither test laptop lacks a supported GPU)".
4. **Fonts / Mesa paragraph (7b):** add that the Mesa 7z is deleted after extraction, interrupted font downloads' `.part` files are deleted after install, and `<root>\tools\.prepare.lock` serializes the downloads.

- [ ] **Step 2: Update the README**

After the `settings` line in the command list:

```
ps5kitsune diagnose <title ID>       # look for a known AnyPS5 error in the game's last log and say how to fix it (--report: text for a bug report)
```

and add one sentence to the paragraph about the Library and launch: "After a game stops with an error AnyPS5 is known to print, the Game page explains it and offers its fix (Repair libs, Turn on CPU rendering, Reset a setting, …), or Report for an error nobody explains yet."

- [ ] **Step 3: Commit**

```bash
git add docs/architecture.md README.md
git commit -m "Document the 7c crash diagnosis

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

## Criterion (run by the user, after Task 9)

On the AMD laptop, with DOOM added (PPSA99666) and the app built from this branch:

1. Delete `libs\libwinpthread-1.dll` from DOOM's folder (`<root>\games\PPSA99666\libs`).
2. Launch DOOM from the Library. The Game page says "A library could not load", names the module, shows the matched line and offers **Repair libs**.
3. `src\PS5Kitsune.Cli\bin\Release\net10.0\ps5kitsune.exe diagnose PPSA99666` prints the same diagnosis and "Fix: in the app, open DOOM and choose Repair libs."
4. **Repair libs**, then **Launch again**: DOOM runs.
