# Game resolution and extra variables: design

October 31, 2026 · Planned for PS5Kitsune 0.3.0. Source: [the Demon's Souls performance report](../../reports/2026-10-31-demons-souls-performance.md), features 1 and 2.

## Goal

Two per-game settings on the game's Settings page:

- **Resolution:** the screen size AnyPS5 tells the game about (`APS5_OUTPUT_RESOLUTION`). On a 4K screen, Demon's Souls renders at 3840×2160 and moves four times the memory of 1080p; choosing 1080p is the biggest performance lever the report found.
- **Extra variables:** a list of `NAME=value` lines for AnyPS5 tuning variables PS5Kitsune has no setting for, such as `APS5_HOST_IMPORT_MIB`, without waiting for a new PS5Kitsune release.

**Criterion:** Demon's Souls set to 1080p logs `guest 1920x1080` in its `[mouse] video driver` line instead of `guest 3840x2160`; with `APS5_HOST_IMPORT_MIB=6144` in the extra variables, its `[gpu] host import budget` line says `APS5_HOST_IMPORT_MIB sets`. A game with neither setting gets exactly the environment it gets today.

**Out of scope:**

- **720p, 480p or any other size.** AnyPS5 v0.1.4 has only two video-out modes (`VIDEO_OUT_RESOLUTION_1080P = 1`, `_2160P = 2` in `libSceVideoOut/include/VideoOutDriver.hpp`); `resolutionIndex()` maps every `<width>x<height>` to one of them. A smaller mode needs AnyPS5 first, and many games would not render below 1080p anyway.
- **Render scaling for any game** ("render at 50 %") and **FSR 1 / NIS upscaling.** Both belong in AnyPS5's renderer. Filed upstream as feature requests, with the two above, after this ships.
- **Choosing the resolution on the Game page or at launch.** The Settings page only, like vertical sync.
- **Editing settings from the CLI.** `ps5kitsune settings` keeps showing them only.
- **Hiding the setting for AnyPS5 versions before v0.1.4.** They ignore unknown variables, so the setting does nothing there and harms nothing.
- **Variables outside AnyPS5's prefixes** (`VK_*`, `MESA_*`, `DXVK_*`…).

## Behaviour

### Resolution

AnyPS5 v0.1.4 behaviour, read in `libSceVideoOut/src/VideoOutDriver.cpp`:

- Unset: the mode of the display the window is on (`resolutionIndex(mode.w, mode.h)`), so 4K on a 4K screen and 1080p otherwise.
- Set: `<width>x<height>` with positive integers; 2160p if width ≥ 3840 or height ≥ 2160, else 1080p. Anything else throws `VideoOut: APS5_OUTPUT_RESOLUTION must be <width>x<height> with positive integers, got "…"`.

PS5Kitsune:

| Choice on the page | `game.json` `settings.resolution` | `APS5_OUTPUT_RESOLUTION` |
|---|---|---|
| Match the display (default) | `null` | not set |
| 1080p | `"1080p"` | `1920x1080` |
| 4K | `"2160p"` | `3840x2160` |

- A box after "Vertical sync", labelled "Resolution", with the help text: "The screen size the game is told about. A lower size can raise the frame rate on a 4K screen. It has no effect on a 1080p screen."
- Any other value in a hand-edited `game.json` reads as `null`, reported like the other normalized fields.
- If AnyPS5 still refuses the value, the crash diagnosis's existing `invalid-setting` rule (`(?<variable>A(?:NY)?PS5_[A-Z_]+) must `) catches it and offers "Reset resolution".

### Extra variables

- A multi-line box in the page's "Advanced" section, labelled "Extra AnyPS5 variables", one `NAME=value` per line, with the help text: "For AnyPS5 options PS5Kitsune has no setting for. One NAME=value per line; names start with APS5_ or ANYPS5_."
- Reading the box: each line is trimmed; blank lines are skipped; the line splits at its first `=`; the name and the value are trimmed. An empty value is allowed (`NAME=`).
- Errors, checked as the user types and shown under the box as `Line N: …`; Save is disabled while there is one:
  - no `=`: "Line 3: write NAME=value.";
  - a name that does not match `^A(NY)?PS5_[A-Z0-9_]+$`: "Line 3: the name must start with APS5_ or ANYPS5_ and use only capital letters, digits and _.";
  - a name already on an earlier line: "Line 3: APS5_HOST_IMPORT_MIB is already set on line 1.";
  - a name PS5Kitsune sets itself: "Line 3: ANYPS5_GPU is set by the GPU setting; use that instead.", naming the setting by its `SettingVariables` label ("the vertical sync setting", "the resolution setting"). For a reserved name with no setting (`ANYPS5_SYSTEM_FONTS`): "Line 3: ANYPS5_SYSTEM_FONTS is set by PS5Kitsune itself."
- Saved in `game.json` as an object, written sorted by name:

  ```json
  "extraEnvironment": { "APS5_HOST_IMPORT_MIB": "6144" }
  ```

  A file without the field reads as empty. When the file is read, entries whose name breaks the rules above are dropped and `extraEnvironment` is reported as normalized, like the other fields.
- The page shows the saved variables sorted by name. Reset empties the box with the other settings. The Advanced section opens by itself when the saved settings have extra variables or a PlayGo value.
- At launch they are added after PS5Kitsune's own variables. As none is reserved, nothing is overridden.
- The bug report (`ps5kitsune diagnose <title ID> --report`, Report in the app) lists them on its `Settings:` line, which already shows every variable a launch passes. No line is added to the game's log: the platform launchers open that file, and AnyPS5 writes to it from its first instruction.
- When AnyPS5 refuses one of them (the same `invalid-setting` rule), the diagnosis says "APS5_HOST_IMPORT_MIB has a value AnyPS5 refuses." and offers "Remove APS5_HOST_IMPORT_MIB", which saves the settings without it.
- `ps5kitsune settings <title ID>` shows a "Resolution" line and an "Extra variables" line (their count, or "none"); the variables themselves appear under "Environment at launch" like the others.

## Code

### Core

- **`Library/GameRecord.cs`, `GameSettings`:**
  - `string? Resolution { get; init; }`: null, or one of `GameSettingsValidator.Resolutions`.
  - `IReadOnlyDictionary<string, string> ExtraEnvironment { get; init; }`, empty by default, keys compared ordinally.
  - Its own `Equals(GameSettings?)` and `GetHashCode()`, comparing `ExtraEnvironment` by content (same keys and values, in any order). The record compares a dictionary by reference otherwise, and the page's Save/Reset state and the diagnosis compare settings.
  - JSON: `resolution` and `extraEnvironment` with the existing camelCase options; the dictionary is written sorted by key.
- **`Library/GameSettingsValidator.cs`:**
  - `Resolutions = ["1080p", "2160p"]`: the only list of choices; a future AnyPS5 mode is one more entry here and in the launch mapping.
  - `Normalize`: an unknown `Resolution` becomes null (`"resolution"`); invalid or reserved `ExtraEnvironment` names are dropped (`"extraEnvironment"`).
- **`Launch/LaunchEnvironment.cs`:**
  - `OutputResolution = "APS5_OUTPUT_RESOLUTION"` and the mapping `1080p` → `1920x1080`, `2160p` → `3840x2160`.
  - `ReservedNames`: every variable `For` can set (`ANYPS5_GPU`, `VK_DRIVER_FILES`, `ANYPS5_LANGUAGE`, `ANYPS5_PRESENT_MODE`, `VK_LOADER_DRIVERS_DISABLE`, `ANYPS5_OVERLAY`, `ANYPS5_OVERLAY_POS`, `APS5_PLAYGO_UNLISTED_CHUNKS`, `ANYPS5_SYSTEM_FONTS`, `APS5_OUTPUT_RESOLUTION`).
  - `For`: emits `APS5_OUTPUT_RESOLUTION` when `Resolution` is set, then each extra variable.
- **New `Launch/ExtraVariables.cs`** (static; not named `ExtraEnvironment`, which is the settings property):
  - `ExtraVariablesParse Parse(string? text)`: the variables and a list of `ExtraVariableError(int Line, string Message)`, with the rules and texts above.
  - `string Format(IReadOnlyDictionary<string, string> variables)`: `NAME=value` lines sorted by name, joined with `\n`.
  - `string? Problem(string name)`: why a name is not allowed, or null. Used by `Parse` and by `Normalize`.
- **`Diagnostics/SettingVariables.cs`:** `new("APS5_OUTPUT_RESOLUTION", "resolution", settings => settings with { Resolution = Defaults.Resolution })`.
- **`Diagnostics/DiagnosisTexts.cs`:** new `DiagnosisAction.RemoveExtraVariable`; when the refused variable is a key of `settings.ExtraEnvironment` (ignoring case), the button is "Remove NAME".
- **`Launch/GameSettingsReport.cs`:** the "Resolution" (`match the display`, `1080p` or `4K`) and "Extra variables" lines, after "PlayGo unlisted chunks".
- **`Diagnostics/CrashReporter.cs`:** unchanged; its `Settings:` line comes from `LaunchEnvironment.For`.

### App

- **`GameSettingsViewModel`:** `Resolutions` (labels "Match the display", "1080p", "4K"), `SelectedResolution`, `ExtraEnvironmentText` (two-way), `ExtraEnvironmentErrors` (`string?`, the error lines joined, refreshed on each change), `IsAdvancedOpen`; `SaveCommand` can run only with no errors; Save writes the parsed variables; Reset and load fill the text with `ExtraVariables.Format`; leaving with an error in the box counts as unsaved changes.
- **`GameSettingsView.axaml`:** the Resolution box after Vertical sync; the Advanced expander bound to `IsAdvancedOpen`; in Advanced, after PlayGo, a `TextBox` with `AcceptsReturn="True"`, a monospace font, about 4 lines high, its label and help, and the errors in the `error` style with `AutomationProperties.LiveSetting="Polite"`.
- **`DiagnosisViewModel`:** `RemoveExtraVariable` saves the settings without the captured name, as `ResetSetting` does for a setting.

## Tests

- **`ExtraVariables`** (Core.Tests): valid lines; blank lines and spaces; a value containing `=`; an empty value; each error with its line number and text; a lower-case name is refused; `Format` sorted, and `Parse(Format(x))` gives `x`.
- **`GameSettingsValidator`:** an unknown resolution becomes null; a bad and a reserved extra name are dropped; the `replaced` names.
- **`LaunchEnvironment`:** default settings still give no variables; each resolution gives its value; extra variables are added; `ReservedNames` contains every name `For` emits (checked by turning every setting on).
- **`game.json`:** a file without the new fields loads with null and empty; both round-trip; the dictionary is written sorted.
- **`GameSettings` equality:** the same variables in another order are equal; a different value is not; equal settings have equal hash codes.
- **Diagnosis:** a refused `APS5_OUTPUT_RESOLUTION` offers "Reset resolution"; a refused extra variable offers "Remove NAME"; one that is in neither keeps no action.
- **`GameSettingsReport`:** the two new lines, and the variables under "Environment at launch".
- **`CrashReporter`:** the `Settings:` line lists an extra variable.
- **App.Tests:** an error disables Save and lists the line; fixing it enables Save; saving writes the variables and the resolution; Reset clears both; the diagnosis action removes only that name.
- **Visual:** headless screenshots of the Settings page with Advanced open and one error, light and dark, attached to the PR.

## Criterion

1. Unit tests pass on Windows and Linux CI.
2. Manual, Windows: Demon's Souls with "1080p" logs `guest 1920x1080`; with "Match the display" on a 4K screen it logs `guest 3840x2160` as before.
3. Manual: with `APS5_HOST_IMPORT_MIB=6144` in the box, the bug report's `Settings:` line lists it and AnyPS5's budget line names it; a typo such as `APS5-HOST=1` shows the error and cannot be saved.
4. The report's "Result of the workaround" line is filled in with the FPS seen at 1080p.
