# Game Resolution and Extra Variables Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add two per-game settings to PS5Kitsune: a Resolution choice (`APS5_OUTPUT_RESOLUTION`) and a list of extra `APS5_*`/`ANYPS5_*` environment variables, on the game's Settings page.

**Architecture:** Both are fields of `GameSettings` (saved in `game.json`), normalized on read by `GameSettingsValidator`, turned into variables by `LaunchEnvironment.For`, shown by `GameSettingsReport`, and handled by the crash diagnosis. A new `ExtraVariables` class holds the parsing and naming rules, shared by the validator and the Settings page.

**Tech Stack:** .NET 10, C#, System.Text.Json, Avalonia 12.1.4, CommunityToolkit.Mvvm, xUnit v3.

**Spec:** `docs/superpowers/specs/2026-10-31-game-resolution-and-extra-variables-design.md`

## Global Constraints

- Resolution values in `game.json` `settings.resolution`: `null` (default, "Match the display"), `"1080p"`, `"2160p"`. Variable: `APS5_OUTPUT_RESOLUTION` = `1920x1080` for `1080p`, `3840x2160` for `2160p`; not set for `null`.
- Page labels: "Match the display", "1080p", "4K". Report labels: `match the display`, `1080p`, `4K`.
- Resolution help: "The screen size the game is told about. A lower size can raise the frame rate on a 4K screen. It has no effect on a 1080p screen."
- Extra variables in `game.json`: `"extraEnvironment": { "NAME": "value" }`, written sorted by name (ordinal); a missing or null field reads as empty.
- Extra variable name rule: `^A(NY)?PS5_[A-Z0-9_]+$`, and not one of `LaunchEnvironment.ReservedNames`.
- Error texts, exactly: `Line N: write NAME=value.` · `Line N: the name must start with APS5_ or ANYPS5_ and use only capital letters, digits and _.` · `Line N: NAME is already set on line M.` · `Line N: NAME is set by the <label> setting; use that instead.` (label from `SettingVariables`) · `Line N: NAME is set by PS5Kitsune itself.` (reserved, no setting).
- Box label "Extra AnyPS5 variables"; help "For AnyPS5 options PS5Kitsune has no setting for. One NAME=value per line; names start with APS5_ or ANYPS5_."
- Extra variables are added after PS5Kitsune's own variables and never replace one.
- Diagnosis of a refused extra variable: explanation `NAME has a value AnyPS5 refuses.`, button `Remove NAME`, result `NAME removed. Launch again to check.`
- A game with default settings gets exactly the variables it gets today.
- Build rules: `TreatWarningsAsErrors`, `AnalysisLevel latest-recommended`, `EnforceCodeStyleInBuild`. Core code uses `ConfigureAwait(false)`; App code uses `ConfigureAwait(true)`. Format numbers with `CultureInfo.InvariantCulture` (`string.Create(CultureInfo.InvariantCulture, $"…")`).
- Commits end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Commit locally only; no push.

## File Structure

| File | Responsibility |
|---|---|
| `src/PS5Kitsune.Core/Library/GameRecord.cs` | `GameSettings.Resolution`, `GameSettings.ExtraEnvironment` (kept sorted), value equality |
| `src/PS5Kitsune.Core/Library/GameSettingsValidator.cs` | `Resolutions`; normalizing both fields |
| `src/PS5Kitsune.Core/Launch/LaunchEnvironment.cs` | `OutputResolution`, `ResolutionValue`, `ReservedNames`; emitting both |
| `src/PS5Kitsune.Core/Launch/ExtraVariables.cs` (new) | `Parse`, `Format`, `Problem` |
| `src/PS5Kitsune.Core/Launch/GameSettingsReport.cs` | "Resolution" and "Extra variables" lines |
| `src/PS5Kitsune.Core/Diagnostics/SettingVariables.cs` | `resolution` entry |
| `src/PS5Kitsune.Core/Diagnostics/DiagnosisTexts.cs` | `RemoveExtraVariable` action and text |
| `src/PS5Kitsune.App/ViewModels/DiagnosisViewModel.cs` | running `RemoveExtraVariable` |
| `src/PS5Kitsune.App/ViewModels/GameSettingsViewModel.cs` | resolution choice, extra variables text and errors, Advanced open state |
| `src/PS5Kitsune.App/Views/GameSettingsView.axaml` | the two controls |
| `tests/PS5Kitsune.App.Tests/Headless/PageGallery.cs`, `Screenshots.cs` | a `GameSettings-Advanced` screenshot |
| `docs/architecture.md` | settings table, diagnosis table, `game.json` example |

---

### Task 1: Resolution setting in Core

**Files:**
- Modify: `src/PS5Kitsune.Core/Library/GameRecord.cs` (class `GameSettings`)
- Modify: `src/PS5Kitsune.Core/Library/GameSettingsValidator.cs`
- Modify: `src/PS5Kitsune.Core/Launch/LaunchEnvironment.cs`
- Modify: `src/PS5Kitsune.Core/Launch/GameSettingsReport.cs`
- Modify: `src/PS5Kitsune.Core/Diagnostics/SettingVariables.cs`
- Modify: `docs/architecture.md` (section "Per-game settings", the `invalid-setting` row, the `game.json` example)
- Test: `tests/PS5Kitsune.Core.Tests/Launch/LaunchEnvironmentTests.cs`, `tests/PS5Kitsune.Core.Tests/Library/GameSettingsValidatorTests.cs`, `tests/PS5Kitsune.Core.Tests/Launch/GameSettingsReportTests.cs`, `tests/PS5Kitsune.Core.Tests/Library/GameRecordStoreTests.cs`, `tests/PS5Kitsune.Core.Tests/Diagnostics/DiagnosisTextsTests.cs`

**Interfaces:**
- Produces: `GameSettings.Resolution` (`string?`); `GameSettingsValidator.Resolutions` (`IReadOnlyList<string>` = `["1080p", "2160p"]`); `LaunchEnvironment.OutputResolution` (`const string` = `"APS5_OUTPUT_RESOLUTION"`); `LaunchEnvironment.ResolutionValue(string? resolution)` → `string?`; `SettingVariables` entry with label `"resolution"`.

- [ ] **Step 1: Write the failing tests**

In `LaunchEnvironmentTests.OneSetting`, add two rows after the PlayGo row:

```csharp
        { new GameSettings { Resolution = "1080p" }, "APS5_OUTPUT_RESOLUTION", "1920x1080" },
        { new GameSettings { Resolution = "2160p" }, "APS5_OUTPUT_RESOLUTION", "3840x2160" },
```

In `GameSettingsValidatorTests`, add:

```csharp
    [Fact]
    public void An_unknown_resolution_reads_as_following_the_display()
    {
        var (settings, replaced) = GameSettingsValidator.Normalize(new GameSettings { Resolution = "1440p" });

        Assert.Null(settings.Resolution);
        Assert.Equal(["resolution"], replaced);
    }

    [Theory]
    [InlineData("1080p")]
    [InlineData("2160p")]
    public void A_known_resolution_is_kept(string resolution)
    {
        var (settings, replaced) = GameSettingsValidator.Normalize(new GameSettings { Resolution = resolution });

        Assert.Equal(resolution, settings.Resolution);
        Assert.Empty(replaced);
    }
```

In `GameSettingsReportTests`, every test that asserts the whole list of lines gets `"Resolution: match the display (default)",` right after its `"PlayGo unlisted chunks: …"` line (with `(default)` only where the test's settings leave `Resolution` null). Then add:

```csharp
    [Theory]
    [InlineData("1080p", "Resolution: 1080p")]
    [InlineData("2160p", "Resolution: 4K")]
    public void The_resolution_line_names_the_choice(string resolution, string line) =>
        Assert.Contains(line, GameSettingsReport.Lines(new GameSettings { Resolution = resolution }, null, lavapipeInstalled: false, null, null, fontsInstalled: false));
```

In `GameRecordStoreTests`, add:

```csharp
    [Fact]
    public async Task The_resolution_round_trips()
    {
        var record = TestRecords.Sample() with { Settings = new GameSettings { Resolution = "2160p" } };

        await GameRecordStore.WriteAsync(RecordFile, record, TestContext.Current.CancellationToken);

        Assert.Equal("2160p", GameRecordStore.Read(RecordFile)!.Settings.Resolution);
        Assert.Contains("\"resolution\": \"2160p\"", File.ReadAllText(RecordFile), StringComparison.Ordinal);
    }
```

In `DiagnosisTextsTests`, add:

```csharp
    [Fact]
    public void A_refused_resolution_offers_to_reset_it()
    {
        var text = DiagnosisTexts.For(Of(DiagnosisIds.InvalidSetting, "variable", "APS5_OUTPUT_RESOLUTION"), new GameSettings { Resolution = "1080p" });

        Assert.Equal(DiagnosisAction.ResetSetting, text.Action);
        Assert.Equal("Reset resolution", text.ActionText);
    }
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build fails with `'GameSettings' does not contain a definition for 'Resolution'`.

- [ ] **Step 3: Implement**

`GameRecord.cs`, in `GameSettings` after `PlayGoUnlistedChunks`:

```csharp
    /// <summary>One of <see cref="GameSettingsValidator.Resolutions"/>; null lets AnyPS5 follow the display.</summary>
    public string? Resolution { get; init; }
```

`GameSettingsValidator.cs`: after `PlayGoValues`:

```csharp
    /// <summary>The video-out modes AnyPS5 v0.1.4 has; a future mode is one more entry here and in <see cref="Launch.LaunchEnvironment.ResolutionValue"/>.</summary>
    public static IReadOnlyList<string> Resolutions { get; } = ["1080p", "2160p"];
```

and in `Normalize`, after the PlayGo block:

```csharp
        if (settings.Resolution is { } resolution && !Resolutions.Contains(resolution))
        {
            normalized = normalized with { Resolution = null };
            replaced.Add("resolution");
        }
```

`LaunchEnvironment.cs`: after `SystemFonts`:

```csharp
    public const string OutputResolution = "APS5_OUTPUT_RESOLUTION";
```

after `DozenPattern`:

```csharp
    /// <summary>The <c>&lt;width&gt;x&lt;height&gt;</c> AnyPS5 reads for a resolution choice; null for null or an unknown choice.</summary>
    public static string? ResolutionValue(string? resolution) => resolution switch
    {
        "1080p" => "1920x1080",
        "2160p" => "3840x2160",
        _ => null,
    };
```

and in `For`, after the PlayGo block:

```csharp
        if (ResolutionValue(settings.Resolution) is { } size)
        {
            variables[OutputResolution] = size;
        }
```

`GameSettingsReport.cs`: after the PlayGo `Line(…)`:

```csharp
            Line("Resolution", ResolutionLabel(settings.Resolution), settings.Resolution == defaults.Resolution),
```

and next to `OnOff`:

```csharp
    private static string ResolutionLabel(string? resolution) => resolution switch
    {
        "1080p" => "1080p",
        "2160p" => "4K",
        _ => "match the display",
    };
```

`SettingVariables.cs`: after the PlayGo entry:

```csharp
        new("APS5_OUTPUT_RESOLUTION", "resolution", settings => settings with { Resolution = Defaults.Resolution }),
```

`docs/architecture.md`, "Per-game settings" table, after the Vertical sync row:

```markdown
| Resolution | `resolution` | `APS5_OUTPUT_RESOLUTION` (`1920x1080` or `3840x2160`) | Match the display (`null`, default: AnyPS5 uses the mode of the display the window is on), 1080p (`1080p`), 4K (`2160p`). AnyPS5 v0.1.4 has only these two modes |
```

In the `invalid-setting` row, change `Reset {setting} for GPU, language, vertical sync, overlay or PlayGo` to `Reset {setting} for GPU, language, vertical sync, resolution, overlay or PlayGo`. In the `game.json` example, add `"resolution": null` after `"playGoUnlistedChunks": null`.

- [ ] **Step 4: Run the tests to see them pass**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --project tests/PS5Kitsune.Core.Tests -c Release --no-build`
Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Core tests/PS5Kitsune.Core.Tests docs/architecture.md
git commit -m "Add the per-game resolution setting (APS5_OUTPUT_RESOLUTION) to Core

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Extra variables in Core

**Files:**
- Create: `src/PS5Kitsune.Core/Launch/ExtraVariables.cs`
- Modify: `src/PS5Kitsune.Core/Library/GameRecord.cs` (class `GameSettings`)
- Modify: `src/PS5Kitsune.Core/Library/GameSettingsValidator.cs`
- Modify: `src/PS5Kitsune.Core/Launch/LaunchEnvironment.cs`
- Modify: `src/PS5Kitsune.Core/Launch/GameSettingsReport.cs`
- Modify: `docs/architecture.md`
- Test: create `tests/PS5Kitsune.Core.Tests/Launch/ExtraVariablesTests.cs`, `tests/PS5Kitsune.Core.Tests/Library/GameSettingsTests.cs`; modify `LaunchEnvironmentTests.cs`, `GameSettingsValidatorTests.cs`, `GameSettingsReportTests.cs`, `GameRecordStoreTests.cs`, `tests/PS5Kitsune.Core.Tests/Diagnostics/CrashReporterTests.cs`

**Interfaces:**
- Consumes (Task 1): `GameSettings.Resolution`, `LaunchEnvironment.OutputResolution`, `SettingVariables` with the `resolution` entry.
- Produces:
  - `GameSettings.ExtraEnvironment`: `IReadOnlyDictionary<string, string>`, never null, enumerates sorted by name (ordinal).
  - `sealed record ExtraVariableError(int Line, string Message)`; `sealed record ExtraVariablesParse(IReadOnlyDictionary<string, string> Variables, IReadOnlyList<ExtraVariableError> Errors)`.
  - `static class ExtraVariables` (namespace `PS5Kitsune.Core.Launch`): `ExtraVariablesParse Parse(string? text)`, `string Format(IReadOnlyDictionary<string, string> variables)`, `string? Problem(string name)`.
  - `LaunchEnvironment.ReservedNames`: `IReadOnlySet<string>`.

- [ ] **Step 1: Write the failing tests**

Create `tests/PS5Kitsune.Core.Tests/Launch/ExtraVariablesTests.cs`:

```csharp
using PS5Kitsune.Core.Launch;

namespace PS5Kitsune.Core.Tests.Launch;

public sealed class ExtraVariablesTests
{
    private const string BadName = "Line 1: the name must start with APS5_ or ANYPS5_ and use only capital letters, digits and _.";

    [Fact]
    public void Lines_become_variables_and_spaces_and_blank_lines_are_ignored()
    {
        var parse = ExtraVariables.Parse("  APS5_HOST_IMPORT_MIB = 6144  \r\n\r\nANYPS5_TRACE=\n");

        Assert.Empty(parse.Errors);
        Assert.Equal(new Dictionary<string, string> { ["APS5_HOST_IMPORT_MIB"] = "6144", ["ANYPS5_TRACE"] = "" }, parse.Variables);
    }

    [Fact]
    public void A_value_may_contain_an_equals_sign() =>
        Assert.Equal("a=b", ExtraVariables.Parse("APS5_X=a=b").Variables["APS5_X"]);

    [Fact]
    public void Empty_text_gives_nothing()
    {
        var parse = ExtraVariables.Parse(null);

        Assert.Empty(parse.Variables);
        Assert.Empty(parse.Errors);
    }

    [Theory]
    [InlineData("APS5_X", "Line 1: write NAME=value.")]
    [InlineData("VK_DRIVER_FILES=x", BadName)]
    [InlineData("aps5_host_import_mib=1", BadName)]
    [InlineData("APS5-HOST=1", BadName)]
    [InlineData("APS5_=1", BadName)]
    [InlineData("ANYPS5_GPU=Radeon", "Line 1: ANYPS5_GPU is set by the GPU setting; use that instead.")]
    [InlineData("ANYPS5_PRESENT_MODE=mailbox", "Line 1: ANYPS5_PRESENT_MODE is set by the vertical sync setting; use that instead.")]
    [InlineData("APS5_OUTPUT_RESOLUTION=1920x1080", "Line 1: APS5_OUTPUT_RESOLUTION is set by the resolution setting; use that instead.")]
    [InlineData("ANYPS5_SYSTEM_FONTS=C:\\fonts", "Line 1: ANYPS5_SYSTEM_FONTS is set by PS5Kitsune itself.")]
    public void A_bad_line_gives_its_error_and_no_variable(string text, string error)
    {
        var parse = ExtraVariables.Parse(text);

        Assert.Equal([new ExtraVariableError(1, error)], parse.Errors);
        Assert.Empty(parse.Variables);
    }

    [Fact]
    public void A_repeated_name_names_its_first_line()
    {
        var parse = ExtraVariables.Parse("APS5_A=1\n\nAPS5_A=2");

        Assert.Equal([new ExtraVariableError(3, "Line 3: APS5_A is already set on line 1.")], parse.Errors);
        Assert.Equal("1", parse.Variables["APS5_A"]);
    }

    [Fact]
    public void Format_sorts_by_name_and_parses_back()
    {
        var variables = new Dictionary<string, string> { ["APS5_B"] = "2", ["ANYPS5_A"] = "x=y" };

        var text = ExtraVariables.Format(variables);

        Assert.Equal("ANYPS5_A=x=y\nAPS5_B=2", text);
        Assert.Equal(variables, ExtraVariables.Parse(text).Variables);
    }
}
```

Create `tests/PS5Kitsune.Core.Tests/Library/GameSettingsTests.cs`:

```csharp
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Tests.Library;

public sealed class GameSettingsTests
{
    [Fact]
    public void Extra_variables_compare_by_content()
    {
        var first = new GameSettings { ExtraEnvironment = new Dictionary<string, string> { ["APS5_A"] = "1", ["APS5_B"] = "2" } };
        var second = new GameSettings { ExtraEnvironment = new Dictionary<string, string> { ["APS5_B"] = "2", ["APS5_A"] = "1" } };

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, second with { ExtraEnvironment = new Dictionary<string, string> { ["APS5_A"] = "1", ["APS5_B"] = "3" } });
        Assert.NotEqual(first, new GameSettings());
    }

    [Fact]
    public void Every_other_field_still_takes_part_in_equality()
    {
        var defaults = new GameSettings();
        GameSettings[] changed =
        [
            defaults with { Gpu = "Radeon" },
            defaults with { Lavapipe = true },
            defaults with { Language = 2 },
            defaults with { PresentMode = "mailbox" },
            defaults with { DisableDozen = true },
            defaults with { Overlay = true },
            defaults with { OverlayPosition = "br" },
            defaults with { PlayGoUnlistedChunks = "installed" },
            defaults with { Resolution = "1080p" },
        ];

        Assert.All(changed, settings => Assert.NotEqual(defaults, settings));

        // A new field must be added to Equals, GetHashCode and the list above.
        Assert.Equal(10, typeof(GameSettings).GetProperties().Length);
    }

    [Fact]
    public void Extra_variables_are_kept_sorted_and_null_reads_as_none()
    {
        var settings = new GameSettings { ExtraEnvironment = new Dictionary<string, string> { ["APS5_B"] = "2", ["ANYPS5_A"] = "1" } };

        Assert.Equal(["ANYPS5_A", "APS5_B"], settings.ExtraEnvironment.Keys);
        Assert.Empty(new GameSettings { ExtraEnvironment = null! }.ExtraEnvironment);
    }
}
```

In `LaunchEnvironmentTests`, add:

```csharp
    [Fact]
    public void Extra_variables_come_last_and_never_replace_a_setting()
    {
        var settings = new GameSettings { Language = 5, ExtraEnvironment = new Dictionary<string, string> { ["APS5_HOST_IMPORT_MIB"] = "6144", ["ANYPS5_LANGUAGE"] = "9" } };

        Assert.Equal(
            new Dictionary<string, string> { ["ANYPS5_LANGUAGE"] = "5", ["APS5_HOST_IMPORT_MIB"] = "6144" },
            LaunchEnvironment.For(settings, null, null));
    }

    [Fact]
    public void Every_variable_the_settings_can_set_is_reserved()
    {
        var all = new GameSettings { Gpu = "Radeon", DisableDozen = true, Language = 2, PresentMode = "mailbox", Overlay = true, OverlayPosition = "br", PlayGoUnlistedChunks = "installed", Resolution = "1080p" };

        var names = LaunchEnvironment.For(all, null, Fonts).Keys.Concat(LaunchEnvironment.For(all with { Lavapipe = true }, Manifest, Fonts).Keys);

        Assert.All(names, name => Assert.Contains(name, LaunchEnvironment.ReservedNames));
    }
```

In `GameSettingsValidatorTests`, add:

```csharp
    [Fact]
    public void Extra_variables_with_a_bad_or_reserved_name_or_no_value_are_dropped()
    {
        var (settings, replaced) = GameSettingsValidator.Normalize(new GameSettings
        {
            ExtraEnvironment = new Dictionary<string, string> { ["APS5_HOST_IMPORT_MIB"] = "6144", ["ANYPS5_GPU"] = "x", ["vk_x"] = "1", ["APS5_NULL"] = null! },
        });

        Assert.Equal(new Dictionary<string, string> { ["APS5_HOST_IMPORT_MIB"] = "6144" }, settings.ExtraEnvironment);
        Assert.Equal(["extraEnvironment"], replaced);
    }

    [Fact]
    public void Valid_extra_variables_are_kept()
    {
        var chosen = new GameSettings { ExtraEnvironment = new Dictionary<string, string> { ["APS5_HOST_IMPORT_MIB"] = "6144" } };

        var (settings, replaced) = GameSettingsValidator.Normalize(chosen);

        Assert.Equal(chosen, settings);
        Assert.Empty(replaced);
    }
```

In `GameSettingsReportTests`, every test that asserts the whole list of lines gets `"Extra variables: none (default)",` right after its `"Resolution: …"` line. Then add:

```csharp
    [Fact]
    public void Extra_variables_are_counted_and_listed_at_launch()
    {
        var settings = new GameSettings { ExtraEnvironment = new Dictionary<string, string> { ["APS5_HOST_IMPORT_MIB"] = "6144", ["APS5_OTHER"] = "1" } };

        var lines = GameSettingsReport.Lines(settings, null, lavapipeInstalled: false, null, null, fontsInstalled: false);

        Assert.Contains("Extra variables: 2", lines);
        Assert.Contains("  APS5_HOST_IMPORT_MIB=6144", lines);
        Assert.Contains("  APS5_OTHER=1", lines);
    }
```

In `GameRecordStoreTests`, add (with `using System.Text.Json.Nodes;` at the top):

```csharp
    [Fact]
    public async Task Extra_variables_round_trip_sorted_by_name()
    {
        var record = TestRecords.Sample() with { Settings = new GameSettings { ExtraEnvironment = new Dictionary<string, string> { ["APS5_B"] = "2", ["APS5_A"] = "1" } } };

        await GameRecordStore.WriteAsync(RecordFile, record, TestContext.Current.CancellationToken);

        Assert.Equal(record, GameRecordStore.Read(RecordFile));
        var json = File.ReadAllText(RecordFile);
        Assert.Contains("\"extraEnvironment\": {", json, StringComparison.Ordinal);
        Assert.True(json.IndexOf("\"APS5_A\": \"1\"", StringComparison.Ordinal) < json.IndexOf("\"APS5_B\": \"2\"", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_record_without_the_new_settings_reads_with_their_defaults(bool asNull)
    {
        await GameRecordStore.WriteAsync(RecordFile, TestRecords.Sample(), TestContext.Current.CancellationToken);
        var node = JsonNode.Parse(File.ReadAllText(RecordFile))!;
        var settingsNode = node["settings"]!.AsObject();
        settingsNode.Remove("resolution");
        settingsNode.Remove("extraEnvironment");
        if (asNull)
        {
            settingsNode["extraEnvironment"] = null;
        }

        File.WriteAllText(RecordFile, node.ToJsonString());

        var settings = GameRecordStore.Read(RecordFile)!.Settings;
        Assert.Null(settings.Resolution);
        Assert.Empty(settings.ExtraEnvironment);
    }
```

In `CrashReporterTests`, add:

```csharp
    [Fact]
    public void The_settings_line_lists_the_extra_variables()
    {
        var report = Format(Unexplained, TestRecords.Sample() with { Settings = new GameSettings { ExtraEnvironment = new Dictionary<string, string> { ["APS5_HOST_IMPORT_MIB"] = "6144" } } });

        Assert.Contains("Settings: APS5_HOST_IMPORT_MIB=6144", report, StringComparison.Ordinal);
    }
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build fails with `The name 'ExtraVariables' does not exist` and `'GameSettings' does not contain a definition for 'ExtraEnvironment'`.

- [ ] **Step 3: Implement**

Create `src/PS5Kitsune.Core/Launch/ExtraVariables.cs`:

```csharp
using System.Globalization;
using System.Text.RegularExpressions;
using PS5Kitsune.Core.Diagnostics;

namespace PS5Kitsune.Core.Launch;

/// <param name="Line">1 for the first line of the text.</param>
/// <param name="Message">The whole text shown, starting with "Line N: ".</param>
public sealed record ExtraVariableError(int Line, string Message);

/// <param name="Variables">The valid lines, sorted by name; a line with an error is left out.</param>
public sealed record ExtraVariablesParse(IReadOnlyDictionary<string, string> Variables, IReadOnlyList<ExtraVariableError> Errors);

/// <summary>The extra AnyPS5 variables of a game: one NAME=value per line on the Settings page. See the resolution and extra variables spec.</summary>
public static partial class ExtraVariables
{
    /// <summary>Reads the Settings page's text: lines and names trimmed, blank lines skipped, each line split at its first '='.</summary>
    public static ExtraVariablesParse Parse(string? text)
    {
        var variables = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var firstLines = new Dictionary<string, int>(StringComparer.Ordinal);
        var errors = new List<ExtraVariableError>();
        var lines = (text ?? "").Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var number = index + 1;
            var line = lines[index].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var equals = line.IndexOf('=', StringComparison.Ordinal);
            if (equals < 0)
            {
                errors.Add(Error(number, "write NAME=value."));
                continue;
            }

            var name = line[..equals].Trim();
            if (Problem(name) is { } problem)
            {
                errors.Add(Error(number, problem));
                continue;
            }

            if (firstLines.TryGetValue(name, out var first))
            {
                errors.Add(Error(number, string.Create(CultureInfo.InvariantCulture, $"{name} is already set on line {first}.")));
                continue;
            }

            firstLines[name] = number;
            variables[name] = line[(equals + 1)..].Trim();
        }

        return new ExtraVariablesParse(variables, errors);
    }

    /// <summary>The text the Settings page shows: NAME=value lines sorted by name, joined with '\n'.</summary>
    public static string Format(IReadOnlyDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        return string.Join('\n', variables.OrderBy(variable => variable.Key, StringComparer.Ordinal).Select(variable => $"{variable.Key}={variable.Value}"));
    }

    /// <summary>Why <paramref name="name"/> cannot be an extra variable, without the "Line N: " start; null when it can.</summary>
    public static string? Problem(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!NamePattern().IsMatch(name))
        {
            return "the name must start with APS5_ or ANYPS5_ and use only capital letters, digits and _.";
        }

        if (LaunchEnvironment.ReservedNames.Contains(name))
        {
            return SettingVariables.Find(name) is { } setting
                ? $"{name} is set by the {setting.Label} setting; use that instead."
                : $"{name} is set by PS5Kitsune itself.";
        }

        return null;
    }

    private static ExtraVariableError Error(int line, string problem) =>
        new(line, string.Create(CultureInfo.InvariantCulture, $"Line {line}: {problem}"));

    [GeneratedRegex(@"^A(?:NY)?PS5_[A-Z0-9_]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();
}
```

`GameRecord.cs`: add `using System.Collections.ObjectModel;` at the top. In `GameSettings`, at the top of the class body:

```csharp
    private static readonly IReadOnlyDictionary<string, string> NoVariables =
        new ReadOnlyDictionary<string, string>(new SortedDictionary<string, string>(StringComparer.Ordinal));

    private readonly IReadOnlyDictionary<string, string> _extraEnvironment = NoVariables;
```

after `Resolution`:

```csharp
    /// <summary>
    /// Extra AnyPS5 variables (names allowed by <see cref="Launch.ExtraVariables.Problem"/>), added last at launch.
    /// Kept sorted by name, so game.json lists them in order; null reads as none.
    /// </summary>
    public IReadOnlyDictionary<string, string> ExtraEnvironment
    {
        get => _extraEnvironment;
        init => _extraEnvironment = Sorted(value);
    }

    // The record would compare the dictionary by reference: the Settings page and the diagnosis compare settings by value.
    public bool Equals(GameSettings? other) =>
        other is not null
        && Gpu == other.Gpu
        && Lavapipe == other.Lavapipe
        && Language == other.Language
        && PresentMode == other.PresentMode
        && DisableDozen == other.DisableDozen
        && Overlay == other.Overlay
        && OverlayPosition == other.OverlayPosition
        && PlayGoUnlistedChunks == other.PlayGoUnlistedChunks
        && Resolution == other.Resolution
        && ExtraEnvironment.Count == other.ExtraEnvironment.Count
        && ExtraEnvironment.All(variable => other.ExtraEnvironment.TryGetValue(variable.Key, out var value) && value == variable.Value);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Gpu);
        hash.Add(Lavapipe);
        hash.Add(Language);
        hash.Add(PresentMode);
        hash.Add(DisableDozen);
        hash.Add(Overlay);
        hash.Add(OverlayPosition);
        hash.Add(PlayGoUnlistedChunks);
        hash.Add(Resolution);

        // Sorted by name, so equal contents hash the same.
        foreach (var (name, value) in ExtraEnvironment)
        {
            hash.Add(name);
            hash.Add(value);
        }

        return hash.ToHashCode();
    }

    private static IReadOnlyDictionary<string, string> Sorted(IReadOnlyDictionary<string, string>? variables)
    {
        if (variables is null || variables.Count == 0)
        {
            return NoVariables;
        }

        var sorted = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in variables)
        {
            sorted[name] = value;
        }

        return new ReadOnlyDictionary<string, string>(sorted);
    }
```

`LaunchEnvironment.cs`: after `ResolutionValue`:

```csharp
    /// <summary>Every variable <see cref="For"/> can set: an extra variable may not use one.</summary>
    public static IReadOnlySet<string> ReservedNames { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        Gpu, DriverFiles, Language, PresentMode, DisableDrivers, Overlay, OverlayPosition, PlayGoUnlistedChunks, SystemFonts, OutputResolution,
    };
```

and at the end of `For`, before `return variables;`:

```csharp
        // Last, and never over a setting's variable: a reserved name is refused on the page and dropped on read.
        foreach (var (name, value) in settings.ExtraEnvironment)
        {
            variables.TryAdd(name, value);
        }
```

`GameSettingsValidator.cs`: add `using PS5Kitsune.Core.Launch;`; in `Normalize`, after the resolution block:

```csharp
        var kept = settings.ExtraEnvironment
            .Where(variable => variable.Value is not null && ExtraVariables.Problem(variable.Key) is null)
            .ToDictionary(StringComparer.Ordinal);
        if (kept.Count != settings.ExtraEnvironment.Count)
        {
            normalized = normalized with { ExtraEnvironment = kept };
            replaced.Add("extraEnvironment");
        }
```

`GameSettingsReport.cs`: after the Resolution `Line(…)`:

```csharp
            Line(
                "Extra variables",
                settings.ExtraEnvironment.Count == 0 ? "none" : settings.ExtraEnvironment.Count.ToString(CultureInfo.InvariantCulture),
                settings.ExtraEnvironment.Count == 0),
```

`docs/architecture.md`, "Per-game settings" table, after the PlayGo row:

```markdown
| Advanced: extra AnyPS5 variables | `extraEnvironment` (object, sorted by name) | Each `NAME=value`, after the variables above, never replacing one | Names match `A(NY)?PS5_[A-Z0-9_]+` and are not set by PS5Kitsune; the page refuses other lines, and a hand-edited entry that breaks the rule is dropped on read. Empty by default |
```

In the `game.json` example, add `"extraEnvironment": {}` after `"resolution": null`.

- [ ] **Step 4: Run the tests to see them pass**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --project tests/PS5Kitsune.Core.Tests -c Release --no-build`
Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Core tests/PS5Kitsune.Core.Tests docs/architecture.md
git commit -m "Add extra AnyPS5 variables per game to Core

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Diagnosis removes a refused extra variable

**Files:**
- Modify: `src/PS5Kitsune.Core/Diagnostics/DiagnosisTexts.cs` (enum `DiagnosisAction`, method `Setting`)
- Modify: `src/PS5Kitsune.App/ViewModels/DiagnosisViewModel.cs` (method `RunFixAsync`)
- Modify: `docs/architecture.md` (the `invalid-setting` row)
- Test: `tests/PS5Kitsune.Core.Tests/Diagnostics/DiagnosisTextsTests.cs`, `tests/PS5Kitsune.App.Tests/DiagnosisViewModelTests.cs`

**Interfaces:**
- Consumes (Task 2): `GameSettings.ExtraEnvironment`.
- Produces: `DiagnosisAction.RemoveExtraVariable`.

- [ ] **Step 1: Write the failing tests**

In `DiagnosisTextsTests`, add:

```csharp
    [Fact]
    public void A_refused_extra_variable_offers_to_remove_it()
    {
        var settings = new GameSettings { ExtraEnvironment = new Dictionary<string, string> { ["APS5_HOST_IMPORT_MIB"] = "lots" } };

        var text = DiagnosisTexts.For(Of(DiagnosisIds.InvalidSetting, "variable", "APS5_HOST_IMPORT_MIB"), settings);

        Assert.Equal("A setting has a value AnyPS5 refuses", text.Title);
        Assert.Equal("APS5_HOST_IMPORT_MIB has a value AnyPS5 refuses.", text.Explanation);
        Assert.Equal(DiagnosisAction.RemoveExtraVariable, text.Action);
        Assert.Equal("Remove APS5_HOST_IMPORT_MIB", text.ActionText);
    }

    [Fact]
    public void A_refused_variable_that_nothing_sets_has_no_action()
    {
        var text = DiagnosisTexts.For(Of(DiagnosisIds.InvalidSetting, "variable", "APS5_UNKNOWN"), new GameSettings());

        Assert.Equal(DiagnosisAction.None, text.Action);
        Assert.Null(text.ActionText);
    }
```

In `DiagnosisViewModelTests`, add:

```csharp
    [Fact]
    public async Task Remove_drops_only_the_refused_extra_variable()
    {
        var settings = new GameSettings { Overlay = true, ExtraEnvironment = new Dictionary<string, string> { ["APS5_HOST_IMPORT_MIB"] = "lots", ["APS5_OTHER"] = "1" } };
        var panel = await OpenAsync(Of(DiagnosisIds.InvalidSetting, "APS5_HOST_IMPORT_MIB"), settings);
        await _library.Player.SaveSettingsAsync("PPSA99666", settings, TestContext.Current.CancellationToken);

        Assert.Equal("Remove APS5_HOST_IMPORT_MIB", panel.ActionText);
        await panel.FixCommand.ExecuteAsync(null);

        Assert.Equal(new GameSettings { Overlay = true, ExtraEnvironment = new Dictionary<string, string> { ["APS5_OTHER"] = "1" } }, Saved());
        Assert.Equal("APS5_HOST_IMPORT_MIB removed. Launch again to check.", panel.Result);
    }
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build fails with `'DiagnosisAction' does not contain a definition for 'RemoveExtraVariable'`.

- [ ] **Step 3: Implement**

`DiagnosisTexts.cs`: add `RemoveExtraVariable,` after `Report,` in `DiagnosisAction`. In `Setting`, after the Lavapipe `if` block and before `var explanation = …`:

```csharp
        if (settings.ExtraEnvironment.Keys.FirstOrDefault(name => string.Equals(name, variable, StringComparison.OrdinalIgnoreCase)) is { } extra)
        {
            return new(SettingTitle, $"{extra} has a value AnyPS5 refuses.", DiagnosisAction.RemoveExtraVariable, $"Remove {extra}");
        }
```

`DiagnosisViewModel.cs`: in `RunFixAsync`, after the `ResetSetting` case:

```csharp
            case DiagnosisAction.RemoveExtraVariable when _diagnosis.Capture("variable") is { } variable:
                return await ChangeSettingsAsync(settings => WithoutExtraVariable(settings, variable), $"{variable} removed. Launch again to check.").ConfigureAwait(true);
```

and after `ChangeSettingsAsync`:

```csharp
    private static GameSettings WithoutExtraVariable(GameSettings settings, string variable) => settings with
    {
        ExtraEnvironment = settings.ExtraEnvironment
            .Where(pair => !string.Equals(pair.Key, variable, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(StringComparer.Ordinal),
    };
```

`docs/architecture.md`, `invalid-setting` row: change `Reset {setting} for GPU, language, vertical sync, resolution, overlay or PlayGo; none for ANYPS5_SYSTEM_FONTS` so it reads `Reset {setting} for GPU, language, vertical sync, resolution, overlay or PlayGo; Remove {variable} for an extra variable; none for ANYPS5_SYSTEM_FONTS` (keep the row's backticks as they are).

- [ ] **Step 4: Run the tests to see them pass**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --project tests/PS5Kitsune.Core.Tests -c Release --no-build -- --filter-class "PS5Kitsune.Core.Tests.Diagnostics.DiagnosisTextsTests" && dotnet test --project tests/PS5Kitsune.App.Tests -c Release --no-build -- --filter-class "PS5Kitsune.App.Tests.DiagnosisViewModelTests"`
Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add src tests docs/architecture.md
git commit -m "Diagnosis: offer to remove an extra variable AnyPS5 refuses

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Settings page

**Files:**
- Modify: `src/PS5Kitsune.App/ViewModels/GameSettingsViewModel.cs`
- Modify: `src/PS5Kitsune.App/Views/GameSettingsView.axaml`
- Modify: `tests/PS5Kitsune.App.Tests/Headless/PageGallery.cs`, `tests/PS5Kitsune.App.Tests/Screenshots.cs`
- Test: `tests/PS5Kitsune.App.Tests/GameSettingsViewModelTests.cs`

**Interfaces:**
- Consumes (Tasks 1–2): `GameSettings.Resolution`, `GameSettings.ExtraEnvironment`, `ExtraVariables.Parse`/`Format`.
- Produces: `GameSettingsViewModel.Resolutions`, `SelectedResolution`, `ResolutionHelp`, `ExtraEnvironmentText`, `ExtraEnvironmentErrors`, `ExtraEnvironmentHelp`, `IsAdvancedOpen`, `CanSave`.

- [ ] **Step 1: Write the failing tests**

In `GameSettingsViewModelTests`, add:

```csharp
    [Fact]
    public async Task Default_settings_follow_the_display_and_keep_advanced_closed()
    {
        var page = await OpenAsync();

        Assert.Equal(["Match the display", "1080p", "4K"], page.Resolutions.Select(choice => choice.Label));
        Assert.Null(page.SelectedResolution.Value);
        Assert.Equal("", page.ExtraEnvironmentText);
        Assert.False(page.IsAdvancedOpen);
    }

    [Fact]
    public async Task Shows_the_saved_resolution_and_extra_variables_with_advanced_open()
    {
        var page = await OpenAsync(new GameSettings
        {
            Resolution = "1080p",
            ExtraEnvironment = new Dictionary<string, string> { ["APS5_HOST_IMPORT_MIB"] = "6144", ["ANYPS5_A"] = "1" },
        });

        Assert.Equal("1080p", page.SelectedResolution.Value);
        Assert.Equal("ANYPS5_A=1\nAPS5_HOST_IMPORT_MIB=6144", page.ExtraEnvironmentText);
        Assert.Null(page.ExtraEnvironmentErrors);
        Assert.True(page.IsAdvancedOpen);
    }

    [Fact]
    public async Task A_bad_line_shows_its_error_and_blocks_save_until_fixed()
    {
        var page = await OpenAsync();

        page.ExtraEnvironmentText = "APS5_HOST_IMPORT_MIB=6144\nAPS5-HOST=1";

        Assert.Equal("Line 2: the name must start with APS5_ or ANYPS5_ and use only capital letters, digits and _.", page.ExtraEnvironmentErrors);
        Assert.False(page.SaveCommand.CanExecute(null));

        page.ExtraEnvironmentText = "APS5_HOST_IMPORT_MIB=6144";

        Assert.Null(page.ExtraEnvironmentErrors);
        Assert.True(page.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task Save_writes_the_resolution_and_the_extra_variables()
    {
        var page = await OpenAsync();
        page.SelectedResolution = page.Resolutions.First(choice => choice.Value == "2160p");
        page.ExtraEnvironmentText = " APS5_HOST_IMPORT_MIB = 6144 \n";

        await page.SaveCommand.ExecuteAsync(null);

        Assert.Equal(new GameSettings { Resolution = "2160p", ExtraEnvironment = new Dictionary<string, string> { ["APS5_HOST_IMPORT_MIB"] = "6144" } }, Saved());
        Assert.Equal("Saved", page.Notice);
    }

    [Fact]
    public async Task Reset_clears_the_resolution_and_the_extra_variables()
    {
        var page = await OpenAsync(new GameSettings { Resolution = "1080p", ExtraEnvironment = new Dictionary<string, string> { ["APS5_HOST_IMPORT_MIB"] = "6144" } });
        page.ExtraEnvironmentText = "bad";

        page.ResetCommand.Execute(null);

        Assert.Null(page.SelectedResolution.Value);
        Assert.Equal("", page.ExtraEnvironmentText);
        Assert.Null(page.ExtraEnvironmentErrors);
    }

    [Fact]
    public async Task Leaving_with_an_invalid_line_asks_first()
    {
        var page = await OpenAsync();
        page.ExtraEnvironmentText = "bad";

        _dialogs.Confirm = false;

        Assert.False(await page.LeaveAsync());
        Assert.Equal(["Your changes to the settings of DOOM are not saved."], _dialogs.Confirmations);
    }

    [Fact]
    public async Task Leaving_after_reformatting_the_same_variables_does_not_ask()
    {
        var page = await OpenAsync(new GameSettings { ExtraEnvironment = new Dictionary<string, string> { ["APS5_HOST_IMPORT_MIB"] = "6144" } });
        page.ExtraEnvironmentText = "  APS5_HOST_IMPORT_MIB = 6144\n\n";

        Assert.True(await page.LeaveAsync());
        Assert.Empty(_dialogs.Confirmations);
    }
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build fails with `'GameSettingsViewModel' does not contain a definition for 'Resolutions'`.

- [ ] **Step 3: Implement the view model**

`GameSettingsViewModel.cs`: add `using PS5Kitsune.Core.Launch;`.

After the `PlayGo` field:

```csharp
    private static readonly SettingChoice[] ResolutionChoices =
        [new(null, "Match the display"), new("1080p", "1080p"), new("2160p", "4K")];
```

Change the attributes of `IsLoading` and `IsRunning` so they also notify `CanSave`:

```csharp
    [NotifyPropertyChangedFor(nameof(CanEdit), nameof(CanChooseGpu), nameof(CanChooseOverlayPosition), nameof(CanSave))]
```

After `CanChooseOverlayPosition`:

```csharp
    /// <summary>Save also needs the extra variables to be valid.</summary>
    public bool CanSave => CanEdit && ExtraEnvironmentErrors is null;
```

After `PlayGoHelp`:

```csharp
    public string ResolutionHelp { get; } = "The screen size the game is told about. A lower size can raise the frame rate on a 4K screen. It has no effect on a 1080p screen.";

    public string ExtraEnvironmentHelp { get; } = "For AnyPS5 options PS5Kitsune has no setting for. One NAME=value per line; names start with APS5_ or ANYPS5_.";
```

After `SelectedPresentMode`:

```csharp
    public IReadOnlyList<SettingChoice> Resolutions { get; } = ResolutionChoices;

    [ObservableProperty]
    public partial SettingChoice SelectedResolution { get; set; } = ResolutionChoices[0];
```

After `SelectedPlayGo`:

```csharp
    /// <summary>NAME=value lines, as typed.</summary>
    [ObservableProperty]
    public partial string ExtraEnvironmentText { get; set; } = "";

    /// <summary>One "Line N: …" per problem in <see cref="ExtraEnvironmentText"/>, joined with '\n'; null when it is valid.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string? ExtraEnvironmentErrors { get; private set; }

    /// <summary>Opened on load when the game has Advanced settings, so they are not hidden.</summary>
    [ObservableProperty]
    public partial bool IsAdvancedOpen { get; set; }

    partial void OnExtraEnvironmentTextChanged(string value)
    {
        var errors = ExtraVariables.Parse(value).Errors;
        ExtraEnvironmentErrors = errors.Count == 0 ? null : string.Join('\n', errors.Select(error => error.Message));
    }
```

In `LoadAsync`, after `Apply(record.Settings);`:

```csharp
            IsAdvancedOpen = record.Settings.ExtraEnvironment.Count > 0 || record.Settings.PlayGoUnlistedChunks is not null;
```

In `Apply`, after `SelectedPresentMode = …`:

```csharp
        SelectedResolution = Resolutions.First(choice => choice.Value == settings.Resolution);
```

and after `SelectedPlayGo = …`:

```csharp
        ExtraEnvironmentText = ExtraVariables.Format(settings.ExtraEnvironment);
```

In `Current()`, after `PlayGoUnlistedChunks = SelectedPlayGo.Value,`:

```csharp
        Resolution = SelectedResolution.Value,
        ExtraEnvironment = ExtraVariables.Parse(ExtraEnvironmentText).Variables,
```

Change the Save command's attribute to `[RelayCommand(CanExecute = nameof(CanSave))]` (Reset keeps `CanEdit`).

In `LeaveAsync`, change the condition so a box with an error counts as unsaved:

```csharp
        if (CanEdit && (ExtraEnvironmentErrors is not null || Current() != _saved)
```

- [ ] **Step 4: Implement the view**

`GameSettingsView.axaml`: after the Vertical sync `StackPanel`:

```xml
        <StackPanel Spacing="4">
          <TextBlock x:Name="ResolutionLabel" Text="Resolution" FontWeight="SemiBold" />
          <ComboBox ItemsSource="{Binding Resolutions}" SelectedItem="{Binding SelectedResolution}" IsEnabled="{Binding CanEdit}" MinWidth="320"
                    AutomationProperties.LabeledBy="{Binding #ResolutionLabel}">
            <ComboBox.ItemTemplate>
              <DataTemplate x:DataType="vm:SettingChoice">
                <TextBlock Text="{Binding Label}" />
              </DataTemplate>
            </ComboBox.ItemTemplate>
          </ComboBox>
          <TextBlock Text="{Binding ResolutionHelp}" Classes="caption" TextWrapping="Wrap" />
        </StackPanel>
```

Replace the Advanced `Expander` with:

```xml
        <Expander Header="Advanced" IsExpanded="{Binding IsAdvancedOpen, Mode=TwoWay}">
          <StackPanel Spacing="16">
            <StackPanel Spacing="4">
              <TextBlock x:Name="PlayGoLabel" Text="PlayGo unlisted chunks" FontWeight="SemiBold" />
              <ComboBox ItemsSource="{Binding PlayGoChoices}" SelectedItem="{Binding SelectedPlayGo}" IsEnabled="{Binding CanEdit}" MinWidth="320"
                        AutomationProperties.LabeledBy="{Binding #PlayGoLabel}">
                <ComboBox.ItemTemplate>
                  <DataTemplate x:DataType="vm:SettingChoice">
                    <TextBlock Text="{Binding Label}" />
                  </DataTemplate>
                </ComboBox.ItemTemplate>
              </ComboBox>
              <TextBlock Text="{Binding PlayGoHelp}" Classes="caption" TextWrapping="Wrap" />
            </StackPanel>

            <StackPanel Spacing="4">
              <TextBlock x:Name="ExtraEnvironmentLabel" Text="Extra AnyPS5 variables" FontWeight="SemiBold" />
              <TextBox Text="{Binding ExtraEnvironmentText}" IsEnabled="{Binding CanEdit}" AcceptsReturn="True" TextWrapping="NoWrap"
                       FontFamily="Cascadia Mono,Consolas,monospace" FontSize="12" MinHeight="84" MaxWidth="560" HorizontalAlignment="Left" MinWidth="320"
                       AutomationProperties.LabeledBy="{Binding #ExtraEnvironmentLabel}" />
              <TextBlock Text="{Binding ExtraEnvironmentErrors}" Classes="error" TextWrapping="Wrap" AutomationProperties.LiveSetting="Polite"
                         IsVisible="{Binding ExtraEnvironmentErrors, Converter={x:Static ObjectConverters.IsNotNull}}" />
              <TextBlock Text="{Binding ExtraEnvironmentHelp}" Classes="caption" TextWrapping="Wrap" />
            </StackPanel>
          </StackPanel>
        </Expander>
```

- [ ] **Step 5: Add the screenshot**

`PageGallery.cs`: in the list returned by `BuildAsync`, after `("GameSettings", await GameSettingsAsync()),` add `("GameSettings-Advanced", await GameSettingsAdvancedAsync()),`, and after `GameSettingsAsync`:

```csharp
    private async Task<GameSettingsViewModel> GameSettingsAdvancedAsync()
    {
        var page = await GameSettingsAsync();
        page.SelectedResolution = page.Resolutions.First(choice => choice.Value == "1080p");
        page.IsAdvancedOpen = true;
        page.ExtraEnvironmentText = "APS5_HOST_IMPORT_MIB=6144\nAPS5-HOST=1";
        return page;
    }
```

`Screenshots.cs`, in `Render_every_page`: change `Height = 680,` of the page window to:

```csharp
                            // The Advanced section is at the bottom of the Settings page.
                            Height = name.EndsWith("-Advanced", StringComparison.Ordinal) ? 1100 : 680,
```

- [ ] **Step 6: Run the tests to see them pass**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --project tests/PS5Kitsune.App.Tests -c Release --no-build`
Expected: all tests pass (the screenshot test is skipped without `PS5K_SCREENSHOTS`).

Then render the screenshots once (PowerShell):

```powershell
$env:PS5K_SCREENSHOTS = "$env:TEMP\ps5k-shots"; dotnet test --project tests/PS5Kitsune.App.Tests -c Release --no-build -- --filter-class "PS5Kitsune.App.Tests.Screenshots"
```

Expected: `GameSettings-Advanced-Light.png` and `GameSettings-Advanced-Dark.png` in that folder, showing the Resolution box set to 1080p and, in Advanced, the two lines with the "Line 2: …" error under the box. Report the folder path.

- [ ] **Step 7: Run the whole suite**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --solution PS5Kitsune.slnx -c Release --no-build`
Expected: all tests pass.

- [ ] **Step 8: Commit**

```bash
git add src/PS5Kitsune.App tests/PS5Kitsune.App.Tests
git commit -m "Settings page: resolution choice and extra AnyPS5 variables

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## After the tasks (controller, not a subagent)

- Manual check on Windows with Demon's Souls (PPSA01342), per the spec's Criterion: "1080p" gives `guest 1920x1080` in the `[mouse] video driver` line; "Match the display" on the 4K screen gives `guest 3840x2160`; `APS5_HOST_IMPORT_MIB=6144` appears in the report's `Settings:` line and AnyPS5's budget line says `APS5_HOST_IMPORT_MIB sets`; `APS5-HOST=1` shows the error and cannot be saved. Needs the user's PC and the game.
- Fill in the Demon's Souls report's "Result of the workaround" line with the FPS seen at 1080p.
