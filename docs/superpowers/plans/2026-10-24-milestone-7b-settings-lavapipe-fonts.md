# Milestone 7b — Per-game settings, Lavapipe and fonts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the user set, per game, the AnyPS5 options passed as environment variables, and apply them at every launch from the app and from `run`; download Mesa's Lavapipe on the first launch that needs it and the Noto fonts once; fold in four PR #9 follow-ups on the launch path.

**Architecture:** `GameSettings` gains four fields, normalized on read by `GameSettingsValidator`. `LaunchEnvironment.For` turns settings into variables (pure). `MesaTool` (7z through SharpCompress) and `FontPack` are installed by `LaunchPreparation` before the game lock is taken; `GamePlayer.LaunchAsync` prepares, locks, re-reads, builds the environment, launches, then marks the game played. The Windows launcher passes the environment block, starts the game with `CREATE_BREAKAWAY_FROM_JOB` (falling back without it), and detects exit through the process handle. The app's Game page now performs the launch itself, showing download progress; a new Settings page edits the settings. The CLI gets `settings`.

**Tech Stack:** .NET 10, C#, Avalonia 12 (compiled bindings), CommunityToolkit.Mvvm, xUnit v3 on Microsoft.Testing.Platform, P/Invoke through `LibraryImport`, SharpCompress 0.50.5 (new).

**Spec:** `docs/superpowers/specs/2026-10-24-milestone-7b-settings-lavapipe-fonts-design.md` (revision b896de1).

## Global Constraints

- Code, docs, commit messages and UI text in English.
- `TreatWarningsAsErrors`, `AnalysisLevel latest-recommended`, `EnforceCodeStyleInBuild`: 0 warnings. `[LoggerMessage]` for logging (CA1848); InvariantCulture when formatting numbers and dates (CA1305); no `ToLowerInvariant` for display (CA1308: use a switch); constant texts on view models are get-only properties with an initializer (CA1822); compute log arguments into locals before the call (CA1873).
- One new NuGet dependency: `SharpCompress` 0.50.5 (MIT, published 2026-10-09), in `src/PS5Kitsune.Core` only. No other package.
- `game.json` stays at schema version 1. New `settings` fields, JSON names and defaults: `disableDozen` (false), `overlay` (false), `overlayPosition` (`"tl"`), `playGoUnlistedChunks` (null). Valid values: `language` 0–30; `presentMode` `fifo`, `fifo_latest_ready`, `mailbox`, `immediate`; `overlayPosition` `tl`, `tr`, `bl`, `br`; `playGoUnlistedChunks` null, `installed`, `absent`; `gpu` null or non-blank.
- Environment variables, verbatim names: `ANYPS5_GPU`, `VK_DRIVER_FILES`, `ANYPS5_LANGUAGE`, `ANYPS5_PRESENT_MODE`, `VK_LOADER_DRIVERS_DISABLE` (value `*dzn*`), `ANYPS5_OVERLAY` (value `1`), `ANYPS5_OVERLAY_POS`, `APS5_PLAYGO_UNLISTED_CHUNKS`, `ANYPS5_SYSTEM_FONTS`. Lavapipe sets `ANYPS5_GPU=llvmpipe`. A value equal to its default is never passed.
- Mesa: `mesa3d-26.2.4-release-msvc.7z`, version `26.2.4`, URL `https://github.com/pal1000/mesa-dist-win/releases/download/26.2.4/mesa3d-26.2.4-release-msvc.7z`, SHA-256 `351fc8c8b695878ffb3eaa044b3ead08672a48b1a045e3c3e3975811df0f6695`, size 70257286. Only `x64/lvp_icd.x86_64.json` and `x64/vulkan_lvp.dll` are extracted, flat, into `<root>\tools\mesa` (the manifest's `library_path` is `.\vulkan_lvp.dll`).
- Fonts: the 24 files of the profile's `fonts` list (Task 3) into `<root>\fonts`, a `.complete` marker written last.
- The game process is never assigned to PS5Kitsune's job object. Tests never use the network, the compiler or Python.
- Core texts, verbatim:
  - `LavapipeUnavailableException`: "Could not download Lavapipe: {reason}" (reason = the inner exception's message), "Could not download Lavapipe: this profile has no Mesa download.", "Lavapipe is not installed. Launch again to download it."
  - Fonts warning: "Fonts could not be downloaded ({reason}); games that use system fonts may show no text or stop."
- CLI texts, verbatim: `settings` lines (Task 2, `GameSettingsReport`), "Downloading Lavapipe…", "Downloading fonts…".
- App texts, verbatim:
  - Game page: "Starting…", "Downloading {Lavapipe|fonts}… {n} MB / {m} MB" (or "Downloading {what}… {n} MB" without a total; MB = bytes / 1,048,576, integer), "Launch cancelled", "Not running"; buttons "Cancel", "Settings" (plus the 7a ones).
  - Settings page: title "{name} settings"; "Running: changes can't be saved until it stops"; labels "GPU", "CPU rendering (Lavapipe)", "Game language", "Vertical sync", "Disable Dozen", "Performance overlay", "Overlay position", "Advanced", "PlayGo unlisted chunks"; GPU choices "Automatic", "{name} (not supported)", "{name} (not found)"; "Detecting GPUs…", "Could not detect GPUs: {reason}"; Lavapipe help "Slow but works on any PC. Mesa (~70 MB) is downloaded on the first launch."; vsync choices "On", "Latest frame", "Mailbox", "Off, may tear"; overlay help "F10 toggles it in game."; positions "Top left", "Top right", "Bottom left", "Bottom right"; PlayGo choices "Default", "Installed (High on Life needs this)", "Absent (Sifu uses this)" and help "Only matters for dumps without PlayGo files."; Dozen help "Dozen can crash while GPUs are listed. Lavapipe ignores this."; buttons "Save", "Reset to defaults", "Back"; "Saved"; errors "{name} is running. Stop it to change its settings.", "{name} is busy with another action. Try again.", "Could not save the settings of {name}: {message}", "Cannot read the settings of {titleId}: {message}"; discard dialog title "Discard changes?", message "Your changes to the settings of {name} are not saved.", button "Discard".
  - Library menu: "Settings" after "Launch".
- Windows-only tests start with `Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.")` (Platform tests) or `if (!OperatingSystem.IsWindows()) { Assert.Skip("..."); }` (Core tests).
- Build: `dotnet build PS5Kitsune.slnx -c Release`. Tests: `dotnet test --solution PS5Kitsune.slnx -c Release`; one class: `dotnet test --project <test project> -c Release -- --filter-class "<full class name>"` (a filtered run exits with code 8 when it selects nothing: check the summary). Run commands through the Bash tool. The test output is in French ("échec" = failed, "réussi" = passed, "ignoré" = skipped).
- Close any running `PS5Kitsune.exe` before building the App (it locks `bin\Release`).
- Line endings: keep each edited file's existing endings (most C# files are CRLF; never mix LF lines into a CRLF file); new files may be LF.
- Commit locally after each task, author La-Kiks, message ending with `Co-Authored-By: <your model name> <noreply@anthropic.com>`. Do not push. Never add or run an `install.cmd`.

## Refinements of the spec

1. **`SaveSettingsAsync` lives on `GamePlayer`**, not `GameLibrary`: it needs `RunningGames` to refuse a running game, and `GameLibrary` does not depend on it. Signature: `Task<GameRecord> SaveSettingsAsync(string titleId, GameSettings settings, CancellationToken)`; it normalizes the settings before writing.
2. **The JSON move retry also catches access denied.** Checked on Windows 11 on 2026-10-24: `File.Move(overwrite: true)` onto a file another handle holds open throws `UnauthorizedAccessException` (0x80070005), not a sharing violation. The retry covers `UnauthorizedAccessException` and `IOException` with HResult `0x80070020` or `0x80070021`.
3. **Mesa extraction keeps two files.** The x64 folder of the archive is 381 MB; Lavapipe needs only `lvp_icd.x86_64.json` and `vulkan_lvp.dll` (56 MB, imports only Windows system DLLs). SharpCompress's sequential reader (`ExtractAllEntries`) extracts them in about 14 s; opening entries one by one took 113 s on the solid archive. `ArchiveTool` gains a virtual `ExtractAsync` (zip by default) that `MesaTool` overrides.
4. **The Noto files and their sources** (pinned commits): Noto Sans (regular and italic), Thai and Arabic from `notofonts/notofonts.github.io@42af67ccd12bc0e31ed85384bf73e39248265374` (`fonts/<Family>/full/ttf/`, Mono from `fonts/NotoSansMono/hinted/ttf/`, which has no `full` folder); CJK from `notofonts/noto-cjk@f8d157532fbfaeda587e826d4cd5b21a49186f7c` (`Sans/OTC/`). 24 files, 86,522,532 bytes. AnyPS5's USAGE.md also lists the four Noto Sans italics, so they are included.
5. **`ILavapipe` and `IFontPack`** (Core/Launch) are what `LaunchPreparation` needs; `MesaTool` and `FontPack` implement them. They are registered only when the profile declares `tools.mesa` and a non-empty `fonts` list; `LaunchPreparation` takes them as nullable. A test profile without them launches with no Lavapipe and no fonts.
6. **`GamePlayer.LaunchAsync(string titleId, IProgress<LaunchProgress>? progress, CancellationToken)`**; the 7a overload `LaunchAsync(titleId, cancellationToken)` stays and passes no progress. `PlayingGame` gains `string? FontsWarning { get; init; }`. After `Launch` returns, `MarkPlayedAsync` and `RecordAsync` run with `CancellationToken.None`: the game is started and must be recorded.
7. **The Game page performs the launch.** The Library navigates to `IPages.Launch(titleId, name, firstLaunch)`, a `GameViewModel` that launches on navigation, shows the download progress and a Cancel button, then follows the game. Launch errors move from the Library to the Game page ("Not running" plus the 7a `LaunchErrors` text, and Launch again). `IPages.Game(PlayingGame, bool)` stays for a running game. `GameViewModel.HasExited` becomes `CanLaunchAgain` (not running and not preparing).
8. **The Lavapipe default at add** runs the probe in `GameImporter.AddAsync(PreparedGame…)`, before the steps; `WriteGameRecordStep` takes `bool lavapipe`. Lavapipe is on when the probe succeeded, found the Vulkan loader, and no GPU has `SupportsAnyPS5` (an empty GPU list counts). `GameImporter` takes `IVulkanProbeRunner` after `StepRunner`.
9. **Disable Dozen is shown** when the probe found a Dozen GPU and a non-Dozen GPU, or when it only succeeded with Dozen disabled (`SucceededWithoutDozen`).
10. **`GameSettingsReport.Lines`** (Core/Launch) builds the `settings` output, so it is tested in Core (there is no CLI test project). The CLI prints each line through `[LoggerMessage]`, like `list`.
11. **`LaunchEnvironment.For` returns a dictionary; printing sorts by name** (ordinal), so the output is deterministic.
12. **Breakaway fallback:** `CreateProcess` failing with `ERROR_ACCESS_DENIED` (5) while `CREATE_BREAKAWAY_FROM_JOB` is set is retried once without it, and logged. The breakaway test skips when the test process is already in a job (nested-job breakaway then depends on the outer job).
13. **`settings` with an unknown title ID** throws `GameNotFoundException` from `GameLibrary.Find`; `CommandHost` prints its message and exits with 1, like `run`.

## File map

| File | Change |
| --- | --- |
| `src/PS5Kitsune.Core/Library/GameRecord.cs` | `GameSettings` new fields |
| `src/PS5Kitsune.Core/Library/GameSettingsValidator.cs`, `GameLanguages.cs` | new |
| `src/PS5Kitsune.Core/Library/GameLibrary.cs` | normalize settings on read |
| `src/PS5Kitsune.Core/IO/JsonFiles.cs` | move retry |
| `src/PS5Kitsune.Core/Games/GameImporter.cs`, `Games/Steps/WriteGameRecordStep.cs` | Lavapipe default |
| `src/PS5Kitsune.Core/Launch/LaunchEnvironment.cs`, `GameSettingsReport.cs` | new (Task 2) |
| `Directory.Packages.props`, `src/PS5Kitsune.Core/PS5Kitsune.Core.csproj` | SharpCompress |
| `src/PS5Kitsune.Core/Profiles/AnyPS5Profile.cs`, `ProfileLoader.cs`, `default-profile.json` | `tools.mesa`, `fonts` |
| `src/PS5Kitsune.Core/DataLayout.cs` | `Fonts` |
| `src/PS5Kitsune.Core/Tools/ArchiveTool.cs`, `MesaTool.cs`, `FontPack.cs` | extract hook, new tools |
| `src/PS5Kitsune.Core/Launch/ILavapipe.cs`, `IFontPack.cs`, `LaunchProgress.cs`, `LaunchPreparation.cs`, `LavapipeUnavailableException.cs` | new (Tasks 3–4) |
| `src/PS5Kitsune.Core/Abstractions/IGameLauncher.cs` | `GameLaunchRequest.Environment` |
| `src/PS5Kitsune.Core/Library/GamePlayer.cs`, `PlayingGame.cs` | preparation, environment, order, `SaveSettingsAsync`, `FontsWarning` |
| `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs` | Mesa, fonts, preparation |
| `src/PS5Kitsune.Platform.Windows/WindowsGameLauncher.cs` | environment block, breakaway, exit by handle |
| `src/PS5Kitsune.Cli/RunCommand.cs`, `SettingsCommand.cs`, `ConsoleLaunchProgress.cs`, `CliLog.cs`, `Program.cs` | downloads, `settings` |
| `src/PS5Kitsune.App/ViewModels/GameViewModel.cs`, `LaunchErrors.cs`, `DispatchedProgress.cs`, `Views/GameView.axaml` | the Game page launches |
| `src/PS5Kitsune.App/ViewModels/LibraryViewModel.cs`, `Views/LibraryView.axaml` | navigate to the launch page, Settings menu |
| `src/PS5Kitsune.App/ViewModels/GameSettingsViewModel.cs`, `Views/GameSettingsView.axaml(.cs)`, `Views/MainWindow.axaml` | new Settings page |
| `src/PS5Kitsune.App/Navigation/IPages.cs`, `Shell/PageFactory.cs` | `Launch`, `GameSettings` |
| `docs/architecture.md`, `README.md` | Task 9 |

---

### Task 1: Core — settings, validation, the JSON move retry and the Lavapipe default

**Files:**
- Modify: `src/PS5Kitsune.Core/Library/GameRecord.cs`
- Create: `src/PS5Kitsune.Core/Library/GameSettingsValidator.cs`, `src/PS5Kitsune.Core/Library/GameLanguages.cs`
- Modify: `src/PS5Kitsune.Core/Library/GameLibrary.cs`
- Modify: `src/PS5Kitsune.Core/IO/JsonFiles.cs`
- Modify: `src/PS5Kitsune.Core/Games/GameImporter.cs`, `src/PS5Kitsune.Core/Games/Steps/WriteGameRecordStep.cs`
- Create: `tests/PS5Kitsune.Core.Tests/Fakes/FakeVulkanProbeRunner.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Library/GameSettingsValidatorTests.cs` (new), `tests/PS5Kitsune.Core.Tests/Library/GameLibraryTests.cs`, `tests/PS5Kitsune.Core.Tests/IO/JsonFilesTests.cs` (new), `tests/PS5Kitsune.Core.Tests/Games/GameImporterTests.cs`, `tests/PS5Kitsune.Core.Tests/Games/GameReconverterTests.cs`, `tests/PS5Kitsune.Core.Tests/Games/PrepareGameTests.cs`

**Interfaces:**
- Consumes: `GameRecordStore`, `GameLibrary`, `IVulkanProbeRunner`, `VulkanProbeOutcome`, `VulkanGpu.SupportsAnyPS5`.
- Produces:
  - `GameSettings { string? Gpu; bool Lavapipe; int Language = 1; string PresentMode = "fifo"; bool DisableDozen; bool Overlay; string OverlayPosition = "tl"; string? PlayGoUnlistedChunks }`
  - `GameSettingsValidator.DefaultPresentMode` (`"fifo"`), `.DefaultOverlayPosition` (`"tl"`), `.PresentModes`, `.OverlayPositions`, `.PlayGoValues` (`IReadOnlyList<string>`), `.Normalize(GameSettings) → (GameSettings Settings, IReadOnlyList<string> Replaced)`
  - `GameLanguage(int Number, string Name)`, `GameLanguages.Default` (1), `GameLanguages.All` (31 entries, 0–30), `GameLanguages.Name(int) → string`
  - `GameLibrary.Find`, `All`, `ReadHeld` return normalized settings.
  - `JsonFiles.WriteAsync<T>(path, value, attempts, delay, ct)` (internal overload).
  - `GameImporter(..., StepRunner stepRunner, IVulkanProbeRunner probe, TimeProvider time, ILogger<GameImporter> logger)`, `internal static bool GameImporter.DefaultLavapipe(VulkanProbeOutcome)`.
  - `WriteGameRecordStep(GameImportContext context, AnyPS5InstallRecord install, TimeProvider time, bool lavapipe)`.
  - `tests/.../Fakes/FakeVulkanProbeRunner` (`Outcome` settable, `Runs` count).

- [ ] **Step 1: Write the failing tests for the settings**

Create `tests/PS5Kitsune.Core.Tests/Library/GameSettingsValidatorTests.cs`:

```csharp
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Tests.Library;

public sealed class GameSettingsValidatorTests
{
    [Fact]
    public void Defaults_are_valid()
    {
        var (settings, replaced) = GameSettingsValidator.Normalize(new GameSettings());

        Assert.Equal(new GameSettings(), settings);
        Assert.Empty(replaced);
    }

    [Fact]
    public void Valid_values_are_kept()
    {
        var chosen = new GameSettings
        {
            Gpu = "Radeon", Lavapipe = true, Language = 30, PresentMode = "immediate", DisableDozen = true, Overlay = true, OverlayPosition = "br", PlayGoUnlistedChunks = "absent",
        };

        var (settings, replaced) = GameSettingsValidator.Normalize(chosen);

        Assert.Equal(chosen, settings);
        Assert.Empty(replaced);
    }

    [Fact]
    public void Each_invalid_value_becomes_its_default()
    {
        var invalid = new GameSettings { Gpu = "  ", Language = 31, PresentMode = "vsync", OverlayPosition = "middle", PlayGoUnlistedChunks = "maybe" };

        var (settings, replaced) = GameSettingsValidator.Normalize(invalid);

        Assert.Equal(new GameSettings(), settings);
        Assert.Equal(["gpu", "language", "presentMode", "overlayPosition", "playGoUnlistedChunks"], replaced);
    }

    [Fact]
    public void A_negative_language_and_a_null_mode_become_defaults()
    {
        var (settings, replaced) = GameSettingsValidator.Normalize(new GameSettings { Language = -1, PresentMode = null!, OverlayPosition = null! });

        Assert.Equal(new GameSettings(), settings);
        Assert.Equal(["language", "presentMode", "overlayPosition"], replaced);
    }

    [Fact]
    public void Languages_cover_0_to_30_by_name()
    {
        Assert.Equal(Enumerable.Range(0, 31), GameLanguages.All.Select(language => language.Number));
        Assert.Equal("English (United States)", GameLanguages.Name(1));
        Assert.Equal("Arabic", GameLanguages.Name(21));
        Assert.Equal("Ukrainian", GameLanguages.Name(30));
    }
}
```

Add to `tests/PS5Kitsune.Core.Tests/Library/GameLibraryTests.cs` (inside the class):

```csharp
    [Fact]
    public async Task An_invalid_setting_reads_as_its_default_and_the_file_is_kept()
    {
        var path = Path.Combine(_layout.GameDirectory("PPSA99666"), ".ps5kitsune", "game.json");
        await GameRecordStore.WriteAsync(path, TestRecords.Sample() with { Settings = new GameSettings { Language = 99, PresentMode = "vsync" } }, TestContext.Current.CancellationToken);

        var (_, record) = _library.Find("PPSA99666");

        Assert.Equal(new GameSettings(), record.Settings);
        Assert.Equal(new GameSettings(), Assert.Single(_library.All()).Record.Settings);
        Assert.Equal(99, GameRecordStore.Read(path)!.Settings.Language);
    }

    [Fact]
    public async Task A_record_without_the_new_settings_reads_with_their_defaults()
    {
        var path = Path.Combine(_layout.GameDirectory("PPSA99666"), ".ps5kitsune", "game.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, """
            {
              "schemaVersion": 1, "titleId": "PPSA99666", "name": "DOOM", "executable": "PPSA99666.exe",
              "convertedWith": { "profile": "doom-validated-2026-10", "mode": "release", "version": "v0.1.2", "commit": null, "toIntel": false },
              "audit": { "implemented": 262, "stub": 0, "absent": 0, "verdict": "green" },
              "settings": { "gpu": null, "lavapipe": true, "language": 2, "presentMode": "mailbox" },
              "addedAt": "2026-10-18T15:00:00+02:00", "lastPlayedAt": null
            }
            """, TestContext.Current.CancellationToken);

        var (_, record) = _library.Find("PPSA99666");

        Assert.Equal(new GameSettings { Lavapipe = true, Language = 2, PresentMode = "mailbox" }, record.Settings);
    }
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build errors — `GameSettingsValidator`, `GameLanguages`, `DisableDozen`, `Overlay`, `OverlayPosition`, `PlayGoUnlistedChunks` do not exist.

- [ ] **Step 3: Add the settings, the validator and the languages**

In `src/PS5Kitsune.Core/Library/GameRecord.cs`, replace the `GameSettings` record (and its summary) with:

```csharp
/// <summary>Per-game settings, passed to the game as environment variables at launch. See docs/architecture.md, "Per-game settings".</summary>
public sealed record GameSettings
{
    /// <summary>Part of a GPU name (ANYPS5_GPU); null lets AnyPS5 choose.</summary>
    public string? Gpu { get; init; }

    /// <summary>CPU rendering with Mesa's Lavapipe; overrides <see cref="Gpu"/> and <see cref="DisableDozen"/>.</summary>
    public bool Lavapipe { get; init; }

    /// <summary>Console language number, 0 to 30 (<see cref="GameLanguages"/>).</summary>
    public int Language { get; init; } = GameLanguages.Default;

    /// <summary>One of <see cref="GameSettingsValidator.PresentModes"/>.</summary>
    public string PresentMode { get; init; } = GameSettingsValidator.DefaultPresentMode;

    /// <summary>Hides Mesa's Dozen driver from the Vulkan loader.</summary>
    public bool DisableDozen { get; init; }

    /// <summary>Shows the performance overlay from the first frame.</summary>
    public bool Overlay { get; init; }

    /// <summary>One of <see cref="GameSettingsValidator.OverlayPositions"/>.</summary>
    public string OverlayPosition { get; init; } = GameSettingsValidator.DefaultOverlayPosition;

    /// <summary>"installed" or "absent" for dumps without PlayGo files; null keeps AnyPS5's error.</summary>
    public string? PlayGoUnlistedChunks { get; init; }
}
```

Create `src/PS5Kitsune.Core/Library/GameSettingsValidator.cs`:

```csharp
namespace PS5Kitsune.Core.Library;

/// <summary>The values AnyPS5 accepts for each setting. AnyPS5 stops on an invalid value, so a hand-edited game.json is normalized on read.</summary>
public static class GameSettingsValidator
{
    public const string DefaultPresentMode = "fifo";

    public const string DefaultOverlayPosition = "tl";

    public static IReadOnlyList<string> PresentModes { get; } = ["fifo", "fifo_latest_ready", "mailbox", "immediate"];

    public static IReadOnlyList<string> OverlayPositions { get; } = ["tl", "tr", "bl", "br"];

    public static IReadOnlyList<string> PlayGoValues { get; } = ["installed", "absent"];

    /// <summary>Replaces each value AnyPS5 would refuse with its default; a blank GPU name becomes null.</summary>
    /// <returns>The settings to use, and the JSON names of the fields replaced, in declaration order.</returns>
    public static (GameSettings Settings, IReadOnlyList<string> Replaced) Normalize(GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var replaced = new List<string>();
        var normalized = settings;
        if (settings.Gpu is { } gpu && string.IsNullOrWhiteSpace(gpu))
        {
            normalized = normalized with { Gpu = null };
            replaced.Add("gpu");
        }

        if (!GameLanguages.All.Any(language => language.Number == settings.Language))
        {
            normalized = normalized with { Language = GameLanguages.Default };
            replaced.Add("language");
        }

        if (!PresentModes.Contains(settings.PresentMode))
        {
            normalized = normalized with { PresentMode = DefaultPresentMode };
            replaced.Add("presentMode");
        }

        if (!OverlayPositions.Contains(settings.OverlayPosition))
        {
            normalized = normalized with { OverlayPosition = DefaultOverlayPosition };
            replaced.Add("overlayPosition");
        }

        if (settings.PlayGoUnlistedChunks is { } chunks && !PlayGoValues.Contains(chunks))
        {
            normalized = normalized with { PlayGoUnlistedChunks = null };
            replaced.Add("playGoUnlistedChunks");
        }

        return (normalized, replaced);
    }
}
```

Create `src/PS5Kitsune.Core/Library/GameLanguages.cs` (numbers and names from AnyPS5's USAGE.md, "Console language"):

```csharp
namespace PS5Kitsune.Core.Library;

public sealed record GameLanguage(int Number, string Name);

/// <summary>The console languages a game can be given through ANYPS5_LANGUAGE, as AnyPS5's USAGE.md lists them.</summary>
public static class GameLanguages
{
    /// <summary>English (United States), AnyPS5's default.</summary>
    public const int Default = 1;

    public static IReadOnlyList<GameLanguage> All { get; } =
    [
        new(0, "Japanese"),
        new(1, "English (United States)"),
        new(2, "French"),
        new(3, "Spanish"),
        new(4, "German"),
        new(5, "Italian"),
        new(6, "Dutch"),
        new(7, "Portuguese (Portugal)"),
        new(8, "Russian"),
        new(9, "Korean"),
        new(10, "Chinese (Traditional)"),
        new(11, "Chinese (Simplified)"),
        new(12, "Finnish"),
        new(13, "Swedish"),
        new(14, "Danish"),
        new(15, "Norwegian"),
        new(16, "Polish"),
        new(17, "Portuguese (Brazil)"),
        new(18, "English (United Kingdom)"),
        new(19, "Turkish"),
        new(20, "Spanish (Latin America)"),
        new(21, "Arabic"),
        new(22, "French (Canada)"),
        new(23, "Czech"),
        new(24, "Hungarian"),
        new(25, "Greek"),
        new(26, "Romanian"),
        new(27, "Thai"),
        new(28, "Vietnamese"),
        new(29, "Indonesian"),
        new(30, "Ukrainian"),
    ];

    /// <exception cref="ArgumentOutOfRangeException">Not a console language number.</exception>
    public static string Name(int number) =>
        All.FirstOrDefault(language => language.Number == number)?.Name ?? throw new ArgumentOutOfRangeException(nameof(number), number, "Not a console language number.");
}
```

In `src/PS5Kitsune.Core/Library/GameLibrary.cs`:
- In `Find`, return `(game, Normalized(GameRecordStore.Read(game.RecordFile) ?? throw NotFound(titleId)))`.
- In `All`, add `Normalized(record)` instead of `record`.
- In `ReadHeld`, return `Normalized(GameRecordStore.Read(game.RecordFile) ?? throw NotFound(game.TitleId))`.
- `MarkPlayedAsync` keeps writing the record as read (it does not rewrite settings).
- Add, before `NotFound`:

```csharp
    // game.json may say "settings": null; a value AnyPS5 would refuse is replaced in memory only, so the file shows what was written.
    private GameRecord Normalized(GameRecord record)
    {
        var (settings, replaced) = GameSettingsValidator.Normalize(record.Settings ?? new GameSettings());
        foreach (var field in replaced)
        {
            var titleId = record.TitleId;
            LogSettingReplaced(titleId, field);
        }

        return record with { Settings = settings };
    }
```

- Add with the other log methods:

```csharp
    [LoggerMessage(Level = LogLevel.Warning, Message = "{TitleId}: the setting {Field} in game.json is not valid; its default is used.")]
    private partial void LogSettingReplaced(string titleId, string field);
```

- [ ] **Step 4: Run the settings tests**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests -c Release -- --filter-class "PS5Kitsune.Core.Tests.Library.GameSettingsValidatorTests"` then the same with `PS5Kitsune.Core.Tests.Library.GameLibraryTests`.
Expected: all pass.

- [ ] **Step 5: Write the failing JSON move tests**

Create `tests/PS5Kitsune.Core.Tests/IO/JsonFilesTests.cs`:

```csharp
using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Core.Tests.IO;

public sealed class JsonFilesTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    private sealed record Sample(int Value);

    [Fact]
    public async Task A_destination_held_open_for_a_moment_is_replaced_once_released()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Only Windows refuses to replace an open file.");
        }

        var path = _temp.Combine("game.json");
        await File.WriteAllTextAsync(path, "{}", TestContext.Current.CancellationToken);
        var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        // A dedicated thread, not the pool: the retry delays must not starve the release.
        var release = new Thread(() =>
        {
            Thread.Sleep(100);
            held.Dispose();
        });
        release.Start();

        await JsonFiles.WriteAsync(path, new Sample(7), attempts: 100, delay: TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken);
        release.Join();

        Assert.Equal(7, JsonFiles.Read<Sample>(path)!.Value);
    }

    [Fact]
    public async Task A_destination_held_past_the_retries_throws()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Only Windows refuses to replace an open file.");
        }

        var path = _temp.Combine("game.json");
        await File.WriteAllTextAsync(path, "{}", TestContext.Current.CancellationToken);
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => JsonFiles.WriteAsync(path, new Sample(7), attempts: 2, delay: TimeSpan.FromMilliseconds(10), TestContext.Current.CancellationToken));
    }

    public void Dispose() => _temp.Dispose();
}
```

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build error — no `WriteAsync` overload takes `attempts` and `delay`.

- [ ] **Step 6: Retry the move**

Replace the `WriteAsync` method of `src/PS5Kitsune.Core/IO/JsonFiles.cs` with:

```csharp
    private const int MoveAttempts = 5;

    private static readonly TimeSpan MoveRetryDelay = TimeSpan.FromMilliseconds(50);

    /// <summary>Writes through a temporary file, so a crash never leaves half a record.</summary>
    public static Task WriteAsync<T>(string path, T value, CancellationToken cancellationToken) =>
        WriteAsync(path, value, MoveAttempts, MoveRetryDelay, cancellationToken);

    /// <summary>
    /// The final move is retried: while another handle holds the destination open (an antivirus scan, a reader), Windows refuses to replace it
    /// with access denied, or a sharing or lock violation.
    /// </summary>
    internal static async Task WriteAsync<T>(string path, T value, int attempts, TimeSpan delay, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(value, Options), cancellationToken).ConfigureAwait(false);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(temporary, path, overwrite: true);
                return;
            }
            catch (Exception exception) when (attempt < attempts && IsTransient(exception))
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool IsTransient(Exception exception) =>
        exception is UnauthorizedAccessException
        || (exception is IOException && exception.HResult is unchecked((int)0x80070020) or unchecked((int)0x80070021));
```

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests -c Release -- --filter-class "PS5Kitsune.Core.Tests.IO.JsonFilesTests"`
Expected: 2 passed (skipped on Linux).

- [ ] **Step 7: Write the failing Lavapipe-default tests**

Create `tests/PS5Kitsune.Core.Tests/Fakes/FakeVulkanProbeRunner.cs`:

```csharp
using PS5Kitsune.Core.Checks.Vulkan;

namespace PS5Kitsune.Core.Tests.Fakes;

/// <summary>Returns a set outcome; by default one AMD GPU that supports AnyPS5.</summary>
internal sealed class FakeVulkanProbeRunner : IVulkanProbeRunner
{
    public VulkanProbeOutcome Outcome { get; set; } = new(
        VulkanProbeStatus.Succeeded,
        new VulkanProbeReport(true, null, [new VulkanGpu("AMD Radeon(TM) Graphics", 0x1002, "integrated", "1.3.280", "AmdProprietary", "AMD proprietary driver", "25.10.1", true, true)]),
        null);

    public int Runs { get; private set; }

    public Task<VulkanProbeOutcome> RunAsync(CancellationToken cancellationToken = default)
    {
        Runs++;
        return Task.FromResult(Outcome);
    }
}
```

In `GameImporterTests.cs`, `GameReconverterTests.cs` and `PrepareGameTests.cs`, pass the probe to `new GameImporter(...)` after `_setup.StepRunner`. In `GameImporterTests.cs`, keep it in a field:

```csharp
    private readonly FakeVulkanProbeRunner _probe = new();
```

```csharp
        _importer = new GameImporter(
            _setup.Layout, _setup.Platform, _setup.Profile, _setup.Paths, _setup.Installer,
            new GameSourceResolver(_setup.Layout), new GameValidator(_ => long.MaxValue),
            _setup.PythonTool, _setup.Runner, _setup.StepRunner, _probe, _setup.Time, NullLogger<GameImporter>.Instance);
```

In the two other files, pass `new FakeVulkanProbeRunner()` in the same position.

Add to `GameImporterTests.cs` (use the existing add-a-game helper the class already uses for a successful add; it adds `TestGames.TitleId`):

```csharp
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Lavapipe_is_on_by_default_when_no_gpu_supports_anyps5(bool gpuSupportsAnyPS5, bool lavapipe)
    {
        _probe.Outcome = new VulkanProbeOutcome(
            VulkanProbeStatus.Succeeded,
            new VulkanProbeReport(true, null, [new VulkanGpu("Intel(R) Iris(R) Xe Graphics", 0x8086, "integrated", "1.3.289", "IntelProprietaryWindows", "Intel Corporation", "101.5762", gpuSupportsAnyPS5, true)]),
            null);

        var added = await AddTheGameAsync();

        Assert.Equal(lavapipe, added.Record.Settings.Lavapipe);
    }

    [Fact]
    public async Task A_failed_probe_leaves_lavapipe_off()
    {
        _probe.Outcome = VulkanProbeOutcome.Failed("crashed");

        var added = await AddTheGameAsync();

        Assert.False(added.Record.Settings.Lavapipe);
    }

    [Fact]
    public void The_default_needs_the_vulkan_loader_and_counts_an_empty_gpu_list()
    {
        Assert.True(GameImporter.DefaultLavapipe(new VulkanProbeOutcome(VulkanProbeStatus.Succeeded, new VulkanProbeReport(true, null, []), null)));
        Assert.False(GameImporter.DefaultLavapipe(new VulkanProbeOutcome(VulkanProbeStatus.Succeeded, new VulkanProbeReport(false, "no loader", []), null)));
    }
```

Add the helper they use, the same flow as `Adds_a_zip_end_to_end`:

```csharp
    private async Task<GameAdded> AddTheGameAsync()
    {
        await _setup.InstallAsync();
        return Assert.IsType<GameAdded>(await AddAsync(WriteZip()));
    }
```

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build errors — `GameImporter` has no constructor taking the probe, `DefaultLavapipe` does not exist.

- [ ] **Step 8: Run the probe at add**

In `src/PS5Kitsune.Core/Games/GameImporter.cs`:
- Add `using PS5Kitsune.Core.Checks.Vulkan;`.
- Add the parameter `IVulkanProbeRunner probe,` after `StepRunner stepRunner,` in the primary constructor.
- In `AddAsync(PreparedGame game, ...)`, after `var context = ...;` and before the steps array:

```csharp
        var lavapipe = DefaultLavapipe(await probe.RunAsync(cancellationToken).ConfigureAwait(false));
        if (lavapipe)
        {
            var titleId = metadata.TitleId;
            LogLavapipeByDefault(titleId);
        }
```

- Pass it to the record step: `new WriteGameRecordStep(context, install, time, lavapipe),`.
- Add:

```csharp
    /// <summary>
    /// Lavapipe starts on when Vulkan works but no GPU has shaderInt64 and bufferDeviceAddress (none at all counts); a failed probe
    /// or a missing loader leaves it off, since Lavapipe needs the loader too.
    /// </summary>
    internal static bool DefaultLavapipe(VulkanProbeOutcome outcome) =>
        outcome.Status != VulkanProbeStatus.Failed
        && outcome.Report is { LoaderFound: true } report
        && !report.Gpus.Any(gpu => gpu.SupportsAnyPS5);

    [LoggerMessage(Level = LogLevel.Information, Message = "No GPU supports AnyPS5: CPU rendering (Lavapipe) is on for {TitleId}.")]
    private partial void LogLavapipeByDefault(string titleId);
```

(`GameImporter` is already `partial` with a logger; if it has no `[LoggerMessage]` yet, keep the `logger` primary-constructor parameter as the one the generator uses — match the existing log methods in that file.)

In `src/PS5Kitsune.Core/Games/Steps/WriteGameRecordStep.cs`, add the parameter `bool lavapipe` to the primary constructor (after `TimeProvider time`), document it in the summary ("<paramref name="lavapipe"/>: the initial CPU rendering setting."), and add to the record initializer:

```csharp
            Settings = new GameSettings { Lavapipe = lavapipe },
```

`ServiceCollectionExtensions` needs no change: `GameImporter` is built by the container, which already has `IVulkanProbeRunner`.

- [ ] **Step 9: Run the Core tests**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests -c Release`
Expected: 0 failed.

- [ ] **Step 10: Build everything and commit**

Run: `dotnet build PS5Kitsune.slnx -c Release` — 0 warnings. Then `dotnet test --solution PS5Kitsune.slnx -c Release` — 0 failed.

```bash
git add -A src/PS5Kitsune.Core tests/PS5Kitsune.Core.Tests
git commit -m "Add the new game settings, normalize them on read, retry the JSON move and default Lavapipe at add

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 2: Core — the launch environment and the settings report

**Files:**
- Create: `src/PS5Kitsune.Core/Launch/LaunchEnvironment.cs`, `src/PS5Kitsune.Core/Launch/GameSettingsReport.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Launch/LaunchEnvironmentTests.cs`, `tests/PS5Kitsune.Core.Tests/Launch/GameSettingsReportTests.cs` (new)

**Interfaces:**
- Consumes: `GameSettings`, `GameSettingsValidator` defaults, `GameLanguages.Name` (Task 1).
- Produces:
  - `LaunchEnvironment.For(GameSettings settings, string? lavapipeManifest, string? fontsDirectory) → IReadOnlyDictionary<string, string>`; constants `LaunchEnvironment.Gpu` (`"ANYPS5_GPU"`), `DriverFiles` (`"VK_DRIVER_FILES"`), `Language`, `PresentMode`, `DisableDrivers`, `Overlay`, `OverlayPosition`, `PlayGoUnlistedChunks`, `SystemFonts`, `LavapipeGpu` (`"llvmpipe"`), `DozenPattern` (`"*dzn*"`).
  - `GameSettingsReport.Lines(GameSettings settings, string? lavapipeManifest, bool lavapipeInstalled, string? fontsDirectory, bool fontsInstalled) → IReadOnlyList<string>`.

- [ ] **Step 1: Write the failing environment tests**

Create `tests/PS5Kitsune.Core.Tests/Launch/LaunchEnvironmentTests.cs`:

```csharp
using PS5Kitsune.Core.Launch;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Tests.Launch;

public sealed class LaunchEnvironmentTests
{
    private const string Manifest = @"C:\PS5Kitsune\tools\mesa\lvp_icd.x86_64.json";

    private const string Fonts = @"C:\PS5Kitsune\fonts";

    [Fact]
    public void Default_settings_without_fonts_pass_nothing() =>
        Assert.Empty(LaunchEnvironment.For(new GameSettings(), null, null));

    [Theory]
    [MemberData(nameof(OneSetting))]
    public void Each_setting_becomes_its_variable(GameSettings settings, string name, string value) =>
        Assert.Equal(new Dictionary<string, string> { [name] = value }, LaunchEnvironment.For(settings, null, null));

    public static TheoryData<GameSettings, string, string> OneSetting => new()
    {
        { new GameSettings { Gpu = "Radeon" }, "ANYPS5_GPU", "Radeon" },
        { new GameSettings { DisableDozen = true }, "VK_LOADER_DRIVERS_DISABLE", "*dzn*" },
        { new GameSettings { Language = 21 }, "ANYPS5_LANGUAGE", "21" },
        { new GameSettings { Language = 0 }, "ANYPS5_LANGUAGE", "0" },
        { new GameSettings { PresentMode = "mailbox" }, "ANYPS5_PRESENT_MODE", "mailbox" },
        { new GameSettings { Overlay = true }, "ANYPS5_OVERLAY", "1" },
        { new GameSettings { PlayGoUnlistedChunks = "installed" }, "APS5_PLAYGO_UNLISTED_CHUNKS", "installed" },
    };

    [Fact]
    public void The_overlay_position_is_passed_only_with_the_overlay_and_when_not_top_left()
    {
        Assert.Empty(LaunchEnvironment.For(new GameSettings { OverlayPosition = "br" }, null, null));
        Assert.Equal(
            new Dictionary<string, string> { ["ANYPS5_OVERLAY"] = "1", ["ANYPS5_OVERLAY_POS"] = "br" },
            LaunchEnvironment.For(new GameSettings { Overlay = true, OverlayPosition = "br" }, null, null));
    }

    [Fact]
    public void Lavapipe_overrides_the_gpu_and_dozen()
    {
        var environment = LaunchEnvironment.For(new GameSettings { Lavapipe = true, Gpu = "Radeon", DisableDozen = true }, Manifest, null);

        Assert.Equal(new Dictionary<string, string> { ["VK_DRIVER_FILES"] = Manifest, ["ANYPS5_GPU"] = "llvmpipe" }, environment);
    }

    [Fact]
    public void Fonts_are_passed_when_installed() =>
        Assert.Equal(new Dictionary<string, string> { ["ANYPS5_SYSTEM_FONTS"] = Fonts }, LaunchEnvironment.For(new GameSettings(), null, Fonts));

    [Fact]
    public void Lavapipe_without_a_manifest_is_a_programming_error() =>
        Assert.Throws<ArgumentException>(() => LaunchEnvironment.For(new GameSettings { Lavapipe = true }, null, null));
}
```

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build error — namespace `PS5Kitsune.Core.Launch` does not exist.

- [ ] **Step 2: Write `LaunchEnvironment`**

Create `src/PS5Kitsune.Core/Launch/LaunchEnvironment.cs`:

```csharp
using System.Globalization;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Launch;

/// <summary>The environment variables a game's settings become, added to the game process only. See docs/architecture.md, "Per-game settings".</summary>
public static class LaunchEnvironment
{
    public const string Gpu = "ANYPS5_GPU";

    public const string DriverFiles = "VK_DRIVER_FILES";

    public const string Language = "ANYPS5_LANGUAGE";

    public const string PresentMode = "ANYPS5_PRESENT_MODE";

    public const string DisableDrivers = "VK_LOADER_DRIVERS_DISABLE";

    public const string Overlay = "ANYPS5_OVERLAY";

    public const string OverlayPosition = "ANYPS5_OVERLAY_POS";

    public const string PlayGoUnlistedChunks = "APS5_PLAYGO_UNLISTED_CHUNKS";

    public const string SystemFonts = "ANYPS5_SYSTEM_FONTS";

    /// <summary>The name AnyPS5 matches against Lavapipe's device.</summary>
    public const string LavapipeGpu = "llvmpipe";

    public const string DozenPattern = "*dzn*";

    /// <summary>A value equal to AnyPS5's default is left out, so a game with default settings and no fonts gets nothing.</summary>
    /// <param name="lavapipeManifest">Mesa's Lavapipe manifest; required when <see cref="GameSettings.Lavapipe"/> is on.</param>
    /// <param name="fontsDirectory">The installed Noto fonts; null when they are not installed.</param>
    /// <exception cref="ArgumentException">Lavapipe is on and <paramref name="lavapipeManifest"/> is null.</exception>
    public static IReadOnlyDictionary<string, string> For(GameSettings settings, string? lavapipeManifest, string? fontsDirectory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        if (settings.Lavapipe)
        {
            variables[DriverFiles] = lavapipeManifest ?? throw new ArgumentException("Lavapipe is on: its manifest is required.", nameof(lavapipeManifest));
            variables[Gpu] = LavapipeGpu;
        }
        else
        {
            if (settings.Gpu is { } gpu)
            {
                variables[Gpu] = gpu;
            }

            if (settings.DisableDozen)
            {
                variables[DisableDrivers] = DozenPattern;
            }
        }

        if (settings.Language != GameLanguages.Default)
        {
            variables[Language] = settings.Language.ToString(CultureInfo.InvariantCulture);
        }

        if (settings.PresentMode != GameSettingsValidator.DefaultPresentMode)
        {
            variables[PresentMode] = settings.PresentMode;
        }

        if (settings.Overlay)
        {
            variables[Overlay] = "1";
            if (settings.OverlayPosition != GameSettingsValidator.DefaultOverlayPosition)
            {
                variables[OverlayPosition] = settings.OverlayPosition;
            }
        }

        if (settings.PlayGoUnlistedChunks is { } chunks)
        {
            variables[PlayGoUnlistedChunks] = chunks;
        }

        if (fontsDirectory is not null)
        {
            variables[SystemFonts] = fontsDirectory;
        }

        return variables;
    }
}
```

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests -c Release -- --filter-class "PS5Kitsune.Core.Tests.Launch.LaunchEnvironmentTests"`
Expected: all pass.

- [ ] **Step 3: Write the failing report tests**

Create `tests/PS5Kitsune.Core.Tests/Launch/GameSettingsReportTests.cs`:

```csharp
using PS5Kitsune.Core.Launch;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Tests.Launch;

public sealed class GameSettingsReportTests
{
    private const string Manifest = @"C:\PS5Kitsune\tools\mesa\lvp_icd.x86_64.json";

    private const string Fonts = @"C:\PS5Kitsune\fonts";

    [Fact]
    public void Defaults_with_fonts_installed()
    {
        var lines = GameSettingsReport.Lines(new GameSettings(), Manifest, lavapipeInstalled: false, Fonts, fontsInstalled: true);

        Assert.Equal(
            [
                "GPU: automatic (default)",
                "CPU rendering (Lavapipe): off (default)",
                "Game language: 1, English (United States) (default)",
                "Vertical sync: fifo (default)",
                "Disable Dozen: off (default)",
                "Performance overlay: off (default)",
                "Overlay position: tl (default)",
                "PlayGo unlisted chunks: unset (default)",
                "Environment at launch:",
                @"  ANYPS5_SYSTEM_FONTS=C:\PS5Kitsune\fonts",
            ],
            lines);
    }

    [Fact]
    public void Defaults_without_fonts_in_the_profile_pass_nothing()
    {
        var lines = GameSettingsReport.Lines(new GameSettings(), null, lavapipeInstalled: false, null, fontsInstalled: false);

        Assert.Equal(["Environment at launch:", "  (none)"], lines.Skip(8));
    }

    [Fact]
    public void A_full_set_before_the_first_launch()
    {
        var settings = new GameSettings
        {
            Gpu = "Radeon", Lavapipe = true, Language = 2, PresentMode = "mailbox", DisableDozen = true, Overlay = true, OverlayPosition = "br", PlayGoUnlistedChunks = "absent",
        };

        var lines = GameSettingsReport.Lines(settings, Manifest, lavapipeInstalled: false, Fonts, fontsInstalled: false);

        Assert.Equal(
            [
                "GPU: Radeon",
                "CPU rendering (Lavapipe): on",
                "Game language: 2, French",
                "Vertical sync: mailbox",
                "Disable Dozen: on",
                "Performance overlay: on",
                "Overlay position: br",
                "PlayGo unlisted chunks: absent",
                "Environment at launch:",
                "  ANYPS5_GPU=llvmpipe",
                "  ANYPS5_LANGUAGE=2",
                "  ANYPS5_OVERLAY=1",
                "  ANYPS5_OVERLAY_POS=br",
                "  ANYPS5_PRESENT_MODE=mailbox",
                @"  ANYPS5_SYSTEM_FONTS=C:\PS5Kitsune\fonts (downloaded on first launch)",
                "  APS5_PLAYGO_UNLISTED_CHUNKS=absent",
                @"  VK_DRIVER_FILES=C:\PS5Kitsune\tools\mesa\lvp_icd.x86_64.json (downloaded on first launch)",
            ],
            lines);
    }

    [Fact]
    public void Lavapipe_without_a_mesa_download_says_so()
    {
        var lines = GameSettingsReport.Lines(new GameSettings { Lavapipe = true }, null, lavapipeInstalled: false, null, fontsInstalled: false);

        Assert.Equal(["Environment at launch:", "  (none)", "  Lavapipe is not available: this profile has no Mesa download."], lines.Skip(8));
    }
}
```

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build error — `GameSettingsReport` does not exist.

- [ ] **Step 4: Write `GameSettingsReport`**

Create `src/PS5Kitsune.Core/Launch/GameSettingsReport.cs`:

```csharp
using System.Globalization;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Launch;

/// <summary>What `ps5kitsune settings` prints: each setting, then the variables a launch passes, sorted by name.</summary>
public static class GameSettingsReport
{
    private const string FirstLaunch = " (downloaded on first launch)";

    /// <param name="lavapipeManifest">Where Mesa's manifest is or will be; null when the profile has no Mesa download.</param>
    /// <param name="fontsDirectory">Where the fonts are or will be; null when the profile has no fonts.</param>
    public static IReadOnlyList<string> Lines(GameSettings settings, string? lavapipeManifest, bool lavapipeInstalled, string? fontsDirectory, bool fontsInstalled)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var defaults = new GameSettings();
        List<string> lines =
        [
            Line("GPU", settings.Gpu ?? "automatic", settings.Gpu == defaults.Gpu),
            Line("CPU rendering (Lavapipe)", OnOff(settings.Lavapipe), settings.Lavapipe == defaults.Lavapipe),
            Line(
                "Game language",
                string.Create(CultureInfo.InvariantCulture, $"{settings.Language}, {GameLanguages.Name(settings.Language)}"),
                settings.Language == defaults.Language),
            Line("Vertical sync", settings.PresentMode, settings.PresentMode == defaults.PresentMode),
            Line("Disable Dozen", OnOff(settings.DisableDozen), settings.DisableDozen == defaults.DisableDozen),
            Line("Performance overlay", OnOff(settings.Overlay), settings.Overlay == defaults.Overlay),
            Line("Overlay position", settings.OverlayPosition, settings.OverlayPosition == defaults.OverlayPosition),
            Line("PlayGo unlisted chunks", settings.PlayGoUnlistedChunks ?? "unset", settings.PlayGoUnlistedChunks == defaults.PlayGoUnlistedChunks),
            "Environment at launch:",
        ];

        var lavapipeMissing = settings.Lavapipe && lavapipeManifest is null;
        var effective = lavapipeMissing ? settings with { Lavapipe = false, Gpu = null, DisableDozen = false } : settings;
        var variables = LaunchEnvironment.For(effective, lavapipeManifest, fontsDirectory);
        if (variables.Count == 0)
        {
            lines.Add("  (none)");
        }

        foreach (var (name, value) in variables.OrderBy(variable => variable.Key, StringComparer.Ordinal))
        {
            var pending = (name == LaunchEnvironment.DriverFiles && !lavapipeInstalled) || (name == LaunchEnvironment.SystemFonts && !fontsInstalled);
            lines.Add($"  {name}={value}{(pending ? FirstLaunch : "")}");
        }

        if (lavapipeMissing)
        {
            lines.Add("  Lavapipe is not available: this profile has no Mesa download.");
        }

        return lines;
    }

    private static string Line(string label, string value, bool isDefault) => $"{label}: {value}{(isDefault ? " (default)" : "")}";

    private static string OnOff(bool value) => value ? "on" : "off";
}
```

(With Lavapipe on and no Mesa download, the report shows what a launch would refuse: `LaunchPreparation` throws in that case, Task 4. The GPU and Dozen variables are left out of the effective set because Lavapipe would override them.)

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests -c Release -- --filter-class "PS5Kitsune.Core.Tests.Launch.GameSettingsReportTests"`
Expected: 4 passed.

- [ ] **Step 5: Build and commit**

Run: `dotnet build PS5Kitsune.slnx -c Release` — 0 warnings.

```bash
git add -A src/PS5Kitsune.Core/Launch tests/PS5Kitsune.Core.Tests/Launch
git commit -m "Turn game settings into launch variables and describe them

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 3: Core — Mesa and the fonts

**Files:**
- Modify: `Directory.Packages.props`, `src/PS5Kitsune.Core/PS5Kitsune.Core.csproj`
- Modify: `src/PS5Kitsune.Core/Profiles/AnyPS5Profile.cs`, `src/PS5Kitsune.Core/Profiles/ProfileLoader.cs`, `src/PS5Kitsune.Core/Profiles/default-profile.json`
- Modify: `src/PS5Kitsune.Core/DataLayout.cs`
- Modify: `src/PS5Kitsune.Core/Tools/ArchiveTool.cs`
- Create: `src/PS5Kitsune.Core/Tools/MesaTool.cs`, `src/PS5Kitsune.Core/Tools/FontPack.cs`, `src/PS5Kitsune.Core/Launch/ILavapipe.cs`, `src/PS5Kitsune.Core/Launch/IFontPack.cs`
- Modify: `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Tools/MesaToolTests.cs`, `tests/PS5Kitsune.Core.Tests/Tools/FontPackTests.cs` (new), `tests/PS5Kitsune.Core.Tests/Profiles/ProfileLoaderTests.cs`, `tests/PS5Kitsune.Core.Tests/Checks/SystemCheckerTests.cs`

**Interfaces:**
- Consumes: `ArchiveTool`, `ToolDefinition`, `IDownloader`, `DownloadRequest`, `DownloadProgress`, `ProfileLoader`.
- Produces:
  - `ILavapipe { string Manifest { get; } bool IsInstalled(); Task InstallAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default); }` (namespace `PS5Kitsune.Core.Launch`)
  - `IFontPack { string Folder { get; } bool IsInstalled(); Task InstallAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default); }`
  - `MesaTool(ToolDefinition definition, DataLayout layout, IDownloader downloader) : ArchiveTool, ILavapipe`, `MesaTool.ToolId` (`"mesa"`)
  - `FontPack(AnyPS5Profile profile, DataLayout layout, IDownloader downloader) : IFontPack`; marker `.complete`
  - `FontFile(string Name, Uri Url, string Sha256, long Size)`, `AnyPS5Profile.Fonts : IReadOnlyList<FontFile>` (default empty)
  - `DataLayout.Fonts` (`<root>\fonts`)
  - `protected virtual Task ArchiveTool.ExtractAsync(string archive, string directory, CancellationToken cancellationToken)`
  - Registrations: `MesaTool` (as itself, `IPortableTool` and `ILavapipe`) when the profile has `tools.mesa`; `FontPack` (as itself and `IFontPack`) when `fonts` is not empty.

- [ ] **Step 1: Add SharpCompress**

In `Directory.Packages.props`, in the shipped group, before `Silk.NET.Vulkan`:

```xml
    <PackageVersion Include="SharpCompress" Version="0.50.5" />
```

In `src/PS5Kitsune.Core/PS5Kitsune.Core.csproj`, with the other package references:

```xml
    <PackageReference Include="SharpCompress" />
```

Run: `dotnet build PS5Kitsune.slnx -c Release` — 0 warnings.

- [ ] **Step 2: Write the failing Mesa tests**

The fixtures were made with Windows' `tar.exe --format 7zip --options 7zip:compression=lzma2` (libarchive), which SharpCompress 0.50.5 reads. `Archive` holds `readme.txt`, `x64/lvp_icd.x86_64.json` (43 bytes), `x64/vulkan_lvp.dll` (`dll`) and `x86/vulkan_lvp.dll` (`x86`); `NoDriver` holds only `x64/lvp_icd.x86_64.json`.

Create `tests/PS5Kitsune.Core.Tests/Tools/MesaToolTests.cs`:

```csharp
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.Tests.Tools;

public sealed class MesaToolTests : IDisposable
{
    private static readonly byte[] Archive = Convert.FromBase64String(
        "N3q8ryccAAPrlY7Y/wAAAAAAAAAcAAAAAAAAAOi3OSoBADZyZWFkbWV7IklDRCI6eyJsaWJyYXJ5X3BhdGgiOiIuXHZ1bGthbl9sdnAuZGxsIn19ZGxseDg2AOABkwC8XQAAgTMHrg/Rp0+UoJCgd166WD4RDBCY3aM1dc5wGlPAfkmjISyciJFkv+Y8uFGQRkcNAHQBkfjCW5uO9F1v2WbwOYqBEtJHwgAVto/rGoGh9iRdGWKfnBPmpgCHpayyLTjrkfFiextbGplDnkqEnq/+pWKt2GslPoDVpctWdSdv0VG4d3H7OyOipOBh/VWJgERsb5dVLwJ2fl4kTDYCKU08ee8n8a/r4R+giGd0/mqH2GjVBRNIG/++uAAAAAAXBjsBCYDEAAcLAQABISEBFgyBlAoBJT2LUQAA");

    private static readonly byte[] NoDriver = Convert.FromBase64String(
        "N3q8ryccAANqITCgqwAAAAAAAAAbAAAAAAAAAMc0v2EBACp7IklDRCI6eyJsaWJyYXJ5X3BhdGgiOiIuXHZ1bGthbl9sdnAuZGxsIn19AOAApgB0XQAAgTMHrg/Q8bH8nz9HQQQEMnr/wJP0rNXvnyHovKj7wNSTQRJIgdRPpO2g/CFJoEMDB+YcvhYdyjSrSa5YuLB6VGcKu7EFrU/+KkltCC8gukEbZS8aVsGo1JaNHTb1I/f2vxudRPryp89w9HSK7NkacQAAAAAXBi8BCXwABwsBAAEhIQEWDICnCgHClYxYAAA=");

    private static readonly ToolDefinition Definition = new("26.2.4", new Uri("https://example.test/mesa3d-26.2.4-release-msvc.7z"), new string('a', 64));

    private readonly TempDirectory _temp = new();
    private readonly FakeDownloader _downloader = new();

    private MesaTool CreateTool() => new(Definition, new DataLayout(_temp.Path), _downloader);

    [Fact]
    public async Task Install_keeps_only_lavapipe_flat_in_tools_mesa()
    {
        _downloader.Serve(Definition.Url, Archive);
        var tool = CreateTool();
        Assert.False(tool.IsInstalled());

        await tool.InstallAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(tool.IsInstalled());
        Assert.Equal(_temp.Combine("tools", "mesa", "lvp_icd.x86_64.json"), tool.Manifest);
        Assert.Equal("dll", await File.ReadAllTextAsync(_temp.Combine("tools", "mesa", "vulkan_lvp.dll"), TestContext.Current.CancellationToken));
        Assert.Equal(
            [".ps5kitsune-tool.json", "lvp_icd.x86_64.json", "vulkan_lvp.dll"],
            Directory.GetFileSystemEntries(_temp.Combine("tools", "mesa")).Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal));
        Assert.Equal("26.2.4", await tool.GetVersionAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task An_archive_without_the_driver_is_refused_and_nothing_is_installed()
    {
        _downloader.Serve(Definition.Url, NoDriver);
        var tool = CreateTool();

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => tool.InstallAsync(cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("x64/vulkan_lvp.dll", exception.Message, StringComparison.Ordinal);
        Assert.False(tool.IsInstalled());
        Assert.False(Directory.Exists(_temp.Combine("tools", "mesa")));
    }

    [Fact]
    public async Task A_file_that_is_not_a_7z_is_refused()
    {
        _downloader.Serve(Definition.Url, "not an archive"u8.ToArray());
        var tool = CreateTool();

        await Assert.ThrowsAsync<InvalidDataException>(() => tool.InstallAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(tool.IsInstalled());
    }

    public void Dispose() => _temp.Dispose();
}
```

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build error — `MesaTool` does not exist.

- [ ] **Step 3: Write `ILavapipe`, the extract hook and `MesaTool`**

Create `src/PS5Kitsune.Core/Launch/ILavapipe.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Launch;

/// <summary>Mesa's Lavapipe (CPU rendering), downloaded on the first launch that needs it.</summary>
public interface ILavapipe
{
    /// <summary>The Vulkan driver manifest passed in VK_DRIVER_FILES.</summary>
    string Manifest { get; }

    bool IsInstalled();

    Task InstallAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default);
}
```

Create `src/PS5Kitsune.Core/Launch/IFontPack.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Launch;

/// <summary>The Noto font substitutes, downloaded once for every game.</summary>
public interface IFontPack
{
    /// <summary>The folder passed in ANYPS5_SYSTEM_FONTS.</summary>
    string Folder { get; }

    bool IsInstalled();

    Task InstallAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default);
}
```

In `src/PS5Kitsune.Core/Tools/ArchiveTool.cs`, update the class summary ("A tool distributed as an archive, a zip unless <see cref="ExtractAsync"/> is overridden. …"), replace the two lines

```csharp
        // ExtractToDirectory rejects entries that would land outside the target folder.
        await ZipFile.ExtractToDirectoryAsync(archive, staging, cancellationToken).ConfigureAwait(false);
```

with

```csharp
        await ExtractAsync(archive, staging, cancellationToken).ConfigureAwait(false);
```

and add, before `PrepareExtracted`:

```csharp
    /// <summary>Extracts the downloaded archive into the staging folder.</summary>
    protected virtual Task ExtractAsync(string archive, string directory, CancellationToken cancellationToken) =>
        // ExtractToDirectory rejects entries that would land outside the target folder.
        ZipFile.ExtractToDirectoryAsync(archive, directory, cancellationToken);
```

Create `src/PS5Kitsune.Core/Tools/MesaTool.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Launch;
using PS5Kitsune.Core.Profiles;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace PS5Kitsune.Core.Tools;

/// <summary>
/// Mesa for Windows (pal1000/mesa-dist-win), for Lavapipe only: its manifest and driver are extracted side by side, the manifest naming
/// the driver as .\vulkan_lvp.dll. See docs/architecture.md, "Portable tools".
/// </summary>
public sealed class MesaTool(ToolDefinition definition, DataLayout layout, IDownloader downloader)
    : ArchiveTool(ToolId, definition, layout, downloader), ILavapipe
{
    public const string ToolId = "mesa";

    private const string ManifestName = "lvp_icd.x86_64.json";

    private const string DriverName = "vulkan_lvp.dll";

    private static readonly string[] Kept = [$"x64/{ManifestName}", $"x64/{DriverName}"];

    public string Manifest => Path.Combine(InstallDirectory, ManifestName);

    protected override IEnumerable<string> RequiredFiles => [Manifest, Path.Combine(InstallDirectory, DriverName)];

    /// <summary>Lavapipe is a driver, not a program to run: the version is the profile's.</summary>
    public override Task<string> GetVersionAsync(CancellationToken cancellationToken = default) => Task.FromResult(Definition.Version);

    // The x64 folder alone is 381 MB, so only Lavapipe is kept (56 MB). The reader walks the solid archive once; opening entries one by one
    // decompresses it again for each (14 s against 113 s, measured on 26.2.4).
    protected override Task ExtractAsync(string archive, string directory, CancellationToken cancellationToken) =>
        Task.Run(() => Extract(archive, directory, cancellationToken), cancellationToken);

    private static void Extract(string archive, string directory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        var found = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            using var sevenZip = SevenZipArchive.OpenArchive(archive, new ReaderOptions());
            using var reader = sevenZip.ExtractAllEntries();
            while (found.Count < Kept.Length && reader.MoveToNextEntry())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var key = reader.Entry.Key?.Replace('\\', '/');
                if (key is null || reader.Entry.IsDirectory || !Kept.Contains(key))
                {
                    continue;
                }

                using var output = File.Create(Path.Combine(directory, Path.GetFileName(key)));
                reader.WriteEntryTo(output);
                found.Add(key);
            }
        }
        catch (SharpCompressException exception)
        {
            throw new InvalidDataException($"{archive} is not a readable Mesa archive: {exception.Message}", exception);
        }

        if (Kept.FirstOrDefault(name => !found.Contains(name)) is { } missing)
        {
            throw new InvalidDataException($"{archive} does not contain {missing}.");
        }
    }
}
```

(If SharpCompress reports a non-7z file with an exception that does not derive from `SharpCompressException`, add that type to the `catch` — the test `A_file_that_is_not_a_7z_is_refused` tells. Checked on 2026-10-24: `SevenZipArchive.OpenArchive(string, ReaderOptions)`, `IArchive.ExtractAllEntries()`, `IReader.MoveToNextEntry()`, `IReader.WriteEntryTo(Stream)` exist in 0.50.5; `SevenZipArchive.Open` does not.)

`ArchiveTool.InstallAsync` deletes a failed staging folder only at the start of the next install; the "nothing is installed" test checks `tools\mesa`, not the staging folder.

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests -c Release -- --filter-class "PS5Kitsune.Core.Tests.Tools.MesaToolTests"`
Expected: 3 passed.

- [ ] **Step 4: Write the failing fonts and profile tests**

Create `tests/PS5Kitsune.Core.Tests/Tools/FontPackTests.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.Tests.Tools;

public sealed class FontPackTests : IDisposable
{
    private static readonly byte[] Regular = "regular"u8.ToArray();
    private static readonly byte[] Bold = "bold!"u8.ToArray();

    private static readonly FontFile RegularFont = new("NotoSans-Regular.ttf", new Uri("https://example.test/NotoSans-Regular.ttf"), TestFiles.Sha256(Regular), Regular.Length);
    private static readonly FontFile BoldFont = new("NotoSans-Bold.ttf", new Uri("https://example.test/NotoSans-Bold.ttf"), TestFiles.Sha256(Bold), Bold.Length);

    private readonly TempDirectory _temp = new();
    private readonly FakeDownloader _downloader = new();

    private FontPack CreatePack(params FontFile[] fonts) => new(
        new AnyPS5Profile { SchemaVersion = 1, Id = "test", Source = new ProfileSource("release", Tag: "v1"), Fonts = fonts },
        new DataLayout(_temp.Path),
        _downloader);

    [Fact]
    public async Task Install_downloads_every_file_into_fonts_then_writes_the_marker()
    {
        _downloader.Serve(RegularFont.Url, Regular);
        _downloader.Serve(BoldFont.Url, Bold);
        var pack = CreatePack(RegularFont, BoldFont);
        var reports = new List<DownloadProgress>();

        await pack.InstallAsync(new SyncProgress(reports.Add), TestContext.Current.CancellationToken);

        Assert.True(pack.IsInstalled());
        Assert.Equal(_temp.Combine("fonts"), pack.Folder);
        Assert.True(File.Exists(_temp.Combine("fonts", "NotoSans-Regular.ttf")));
        Assert.True(File.Exists(_temp.Combine("fonts", ".complete")));
        Assert.Equal(new DownloadProgress(12, 12), reports[^1]);
        Assert.All(reports, report => Assert.Equal(12, report.TotalBytes));
    }

    [Fact]
    public async Task A_failed_file_leaves_no_marker()
    {
        _downloader.Serve(RegularFont.Url, Regular);
        var pack = CreatePack(RegularFont, BoldFont);

        await Assert.ThrowsAsync<HttpRequestException>(() => pack.InstallAsync(cancellationToken: TestContext.Current.CancellationToken));

        Assert.False(pack.IsInstalled());
        Assert.False(File.Exists(_temp.Combine("fonts", ".complete")));
        Assert.True(File.Exists(_temp.Combine("fonts", "NotoSans-Regular.ttf")));
    }

    [Fact]
    public async Task Another_list_of_fonts_is_not_installed_yet()
    {
        _downloader.Serve(RegularFont.Url, Regular);
        await CreatePack(RegularFont).InstallAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(CreatePack(RegularFont, BoldFont).IsInstalled());
    }

    [Fact]
    public async Task An_installed_pack_downloads_nothing()
    {
        _downloader.Serve(RegularFont.Url, Regular);
        var pack = CreatePack(RegularFont);
        await pack.InstallAsync(cancellationToken: TestContext.Current.CancellationToken);

        await pack.InstallAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(_downloader.Requests);
    }

    public void Dispose() => _temp.Dispose();

    private sealed class SyncProgress(Action<DownloadProgress> report) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => report(value);
    }
}
```

Add to `tests/PS5Kitsune.Core.Tests/Profiles/ProfileLoaderTests.cs` (it has a `Load(string json)` helper):

```csharp
    [Fact]
    public void Embedded_profile_pins_mesa_and_the_fonts()
    {
        var profile = ProfileLoader.LoadEmbedded();

        var mesa = profile.Tools["mesa"];
        Assert.Equal("26.2.4", mesa.Version);
        Assert.Equal("351fc8c8b695878ffb3eaa044b3ead08672a48b1a045e3c3e3975811df0f6695", mesa.Sha256);
        Assert.Equal(24, profile.Fonts.Count);
        Assert.Equal(86_522_532, profile.Fonts.Sum(font => font.Size));
        Assert.Contains(profile.Fonts, font => font.Name == "NotoSansCJK-Regular.ttc");
    }

    [Theory]
    [InlineData("fonts/NotoSans-Regular.ttf")]
    [InlineData(@"fonts\NotoSans-Regular.ttf")]
    [InlineData("..")]
    [InlineData("")]
    public void Rejects_a_font_name_that_is_not_a_plain_file_name(string name)
    {
        Assert.Throws<InvalidProfileException>(() => Load($$"""
            {
              "schemaVersion": 1, "id": "bad", "source": { "kind": "release", "tag": "v1" },
              "fonts": [ { "name": "{{name.Replace(@"\", @"\\", StringComparison.Ordinal)}}", "url": "https://example.test/f.ttf", "sha256": "{{new string('a', 64)}}", "size": 1 } ]
            }
            """));
    }

    [Fact]
    public void Rejects_a_font_listed_twice()
    {
        var font = $$"""{ "name": "NotoSans-Regular.ttf", "url": "https://example.test/f.ttf", "sha256": "{{new string('a', 64)}}", "size": 1 }""";
        Assert.Throws<InvalidProfileException>(() => Load($$"""
            {
              "schemaVersion": 1, "id": "bad", "source": { "kind": "release", "tag": "v1" },
              "fonts": [ {{font}}, {{font}} ]
            }
            """));
    }

    [Fact]
    public void Rejects_a_font_over_plain_http()
    {
        Assert.Throws<InvalidProfileException>(() => Load($$"""
            {
              "schemaVersion": 1, "id": "bad", "source": { "kind": "release", "tag": "v1" },
              "fonts": [ { "name": "f.ttf", "url": "http://example.test/f.ttf", "sha256": "{{new string('a', 64)}}", "size": 1 } ]
            }
            """));
    }
```

Add to `tests/PS5Kitsune.Core.Tests/Checks/SystemCheckerTests.cs`, after `Core_registration_alone_falls_back_to_no_job_and_no_launcher` (same setup as that test):

```csharp
    [Fact]
    public void Core_registration_offers_lavapipe_and_the_fonts_from_the_embedded_profile()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IPlatform>(_platform);
        services.AddPS5KitsuneCore(new DataLayout(_temp.Path), ProfileLoader.LoadEmbedded());
        using var provider = services.BuildServiceProvider();

        Assert.IsType<MesaTool>(provider.GetRequiredService<ILavapipe>());
        Assert.Contains(provider.GetServices<IPortableTool>(), tool => tool.Id == "mesa");
        Assert.IsType<FontPack>(provider.GetRequiredService<IFontPack>());
        Assert.Equal(_temp.Combine("fonts"), provider.GetRequiredService<IFontPack>().Folder);
    }
```

(Add the `using PS5Kitsune.Core.Launch;` and `using PS5Kitsune.Core.Tools;` the file needs.)

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build errors — `FontFile`, `AnyPS5Profile.Fonts`, `FontPack` do not exist.

- [ ] **Step 5: Add the fonts to the profile model, the layout and the loader**

In `src/PS5Kitsune.Core/Profiles/AnyPS5Profile.cs`, add to `AnyPS5Profile` after `Tools`:

```csharp
    /// <summary>The Noto font substitutes passed to games through ANYPS5_SYSTEM_FONTS; empty when the profile has none.</summary>
    public IReadOnlyList<FontFile> Fonts { get; init; } = [];
```

and after `ToolDefinition`:

```csharp
/// <param name="Name">File name in &lt;root&gt;/fonts, as AnyPS5 looks it up (for example NotoSans-Regular.ttf).</param>
/// <param name="Size">Bytes, for the download progress.</param>
public sealed record FontFile(string Name, Uri Url, string Sha256, long Size);
```

In `src/PS5Kitsune.Core/DataLayout.cs`, after `Games`:

```csharp
    /// <summary>The Noto fonts, shared by every game through ANYPS5_SYSTEM_FONTS.</summary>
    public string Fonts => Path.Combine(Root, "fonts");
```

In `src/PS5Kitsune.Core/Profiles/ProfileLoader.cs`, in `Validate`, after the tools loop:

```csharp
        var fontNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var font in profile.Fonts)
        {
            if (string.IsNullOrEmpty(font.Name) || font.Name is "." or ".." || font.Name.IndexOfAny(['/', '\\']) >= 0 || !fontNames.Add(font.Name))
            {
                throw new InvalidProfileException($"Profile {profile.Id}: font \"{font.Name}\" needs a unique file name without a folder.");
            }

            RequireSha256(profile.Id, $"font {font.Name}", font.Sha256);
            RequireHttps(profile.Id, $"font {font.Name}", font.Url);
            if (font.Size <= 0)
            {
                throw new InvalidProfileException($"Profile {profile.Id}: font {font.Name} size must be positive.");
            }
        }
```

- [ ] **Step 6: Pin Mesa and the fonts in the embedded profile**

In `src/PS5Kitsune.Core/Profiles/default-profile.json`, add to `"tools"` after `"winlibs"` (mind the comma after the `winlibs` object):

```json
    "mesa": {
      "version": "26.2.4",
      "url": "https://github.com/pal1000/mesa-dist-win/releases/download/26.2.4/mesa3d-26.2.4-release-msvc.7z",
      "sha256": "351fc8c8b695878ffb3eaa044b3ead08672a48b1a045e3c3e3975811df0f6695",
      "size": 70257286
    }
```

and, after the `"tools"` object, a `"fonts"` array (values downloaded and hashed on 2026-10-24; the commits are pinned, so the hashes never change):

```json
  "fonts": [
    { "name": "NotoSans-Light.ttf", "url": "https://raw.githubusercontent.com/notofonts/notofonts.github.io/42af67ccd12bc0e31ed85384bf73e39248265374/fonts/NotoSans/full/ttf/NotoSans-Light.ttf", "sha256": "ca1cf4f5ae2949be8f65fbdd1c3915be2037c12e6532b202b932c5d9a4414a9d", "size": 808456 },
    { "name": "NotoSans-Regular.ttf", "url": "https://raw.githubusercontent.com/notofonts/notofonts.github.io/42af67ccd12bc0e31ed85384bf73e39248265374/fonts/NotoSans/full/ttf/NotoSans-Regular.ttf", "sha256": "f5f552c8c5edb61fe6efb824baf4d4de47b1a8689ab4925ff43f7bd6a4ebece5", "size": 825628 },
    { "name": "NotoSans-Medium.ttf", "url": "https://raw.githubusercontent.com/notofonts/notofonts.github.io/42af67ccd12bc0e31ed85384bf73e39248265374/fonts/NotoSans/full/ttf/NotoSans-Medium.ttf", "sha256": "1d1570dd66d70cbcd56646c55580c3cc453c7abc505534230c165f93b55ad394", "size": 824408 },
    { "name": "NotoSans-Bold.ttf", "url": "https://raw.githubusercontent.com/notofonts/notofonts.github.io/42af67ccd12bc0e31ed85384bf73e39248265374/fonts/NotoSans/full/ttf/NotoSans-Bold.ttf", "sha256": "3a08a47daa00cade516425c15c57615aef2fd418ec9811a7b9f465088f92cc05", "size": 838072 },
    { "name": "NotoSans-LightItalic.ttf", "url": "https://raw.githubusercontent.com/notofonts/notofonts.github.io/42af67ccd12bc0e31ed85384bf73e39248265374/fonts/NotoSans/full/ttf/NotoSans-LightItalic.ttf", "sha256": "6164841f339c49462cf9b70d6df2f61878844e6ad7f7fccb7e7cdf51fa00904e", "size": 836816 },
    { "name": "NotoSans-Italic.ttf", "url": "https://raw.githubusercontent.com/notofonts/notofonts.github.io/42af67ccd12bc0e31ed85384bf73e39248265374/fonts/NotoSans/full/ttf/NotoSans-Italic.ttf", "sha256": "126522ae1bb9cd92120287fc47dfc74ef981e73931d93e52c565fb7e09b2d74a", "size": 845312 },
    { "name": "NotoSans-MediumItalic.ttf", "url": "https://raw.githubusercontent.com/notofonts/notofonts.github.io/42af67ccd12bc0e31ed85384bf73e39248265374/fonts/NotoSans/full/ttf/NotoSans-MediumItalic.ttf", "sha256": "b49c370b3809895db3d35e235c1f4d521dc85605b2aacfccb3c62a81290886e9", "size": 838056 },
    { "name": "NotoSans-BoldItalic.ttf", "url": "https://raw.githubusercontent.com/notofonts/notofonts.github.io/42af67ccd12bc0e31ed85384bf73e39248265374/fonts/NotoSans/full/ttf/NotoSans-BoldItalic.ttf", "sha256": "2e34b41a4b9c234b1be7dff6d06cba18811ecb694b41350873edf0ec16a0f0fa", "size": 853344 },
    { "name": "NotoSansMono-Light.ttf", "url": "https://raw.githubusercontent.com/notofonts/notofonts.github.io/42af67ccd12bc0e31ed85384bf73e39248265374/fonts/NotoSansMono/hinted/ttf/NotoSansMono-Light.ttf", "sha256": "9c9dc46df01c3acee14690fc1be60f6c863675b5ac9ee190f304656dbb9ba9aa", "size": 585580 },
    { "name": "NotoSansMono-Regular.ttf", "url": "https://raw.githubusercontent.com/notofonts/notofonts.github.io/42af67ccd12bc0e31ed85384bf73e39248265374/fonts/NotoSansMono/hinted/ttf/NotoSansMono-Regular.ttf", "sha256": "65b5e2b2c4a1fba9ae8be1f026cb35b03dcb8886d9b2a4147054fde12f7e767d", "size": 596428 },
    { "name": "NotoSansMono-Medium.ttf", "url": "https://raw.githubusercontent.com/notofonts/notofonts.github.io/42af67ccd12bc0e31ed85384bf73e39248265374/fonts/NotoSansMono/hinted/ttf/NotoSansMono-Medium.ttf", "sha256": "53e262499b3e6b4e7274dcc4840e9374c8d9c4a4fad48227a38d8cedd2e34c40", "size": 597720 },
    { "name": "NotoSansMono-Bold.ttf", "url": "https://raw.githubusercontent.com/notofonts/notofonts.github.io/42af67ccd12bc0e31ed85384bf73e39248265374/fonts/NotoSansMono/hinted/ttf/NotoSansMono-Bold.ttf", "sha256": "a21ea0ba6ea49fda7b34ca39a504b487f1130885d36e1a4f9f4255b3ba6994bc", "size": 610908 },
    { "name": "NotoSansThai-Light.ttf", "url": "https://raw.githubusercontent.com/notofonts/notofonts.github.io/42af67ccd12bc0e31ed85384bf73e39248265374/fonts/NotoSansThai/full/ttf/NotoSansThai-Light.ttf", "sha256": "a2a40fbdd72b56b1e6fcc5a45992adc5b3b5b071876156a1f67b1e27972ecfa6", "size": 69352 },
    { "name": "NotoSansThai-Regular.ttf", "url": "https://raw.githubusercontent.com/notofonts/notofonts.github.io/42af67ccd12bc0e31ed85384bf73e39248265374/fonts/NotoSansThai/full/ttf/NotoSansThai-Regular.ttf", "sha256": "2101faa471942570d506a394df7da555d15246c7814c7522c63d4fa85b0d8d88", "size": 72812 },
    { "name": "NotoSansThai-Medium.ttf", "url": "https://raw.githubusercontent.com/notofonts/notofonts.github.io/42af67ccd12bc0e31ed85384bf73e39248265374/fonts/NotoSansThai/full/ttf/NotoSansThai-Medium.ttf", "sha256": "c985a1b03881f1b82777b2e6ead45f9233a32871430140d9c5444dbbfa41e38a", "size": 72652 },
    { "name": "NotoSansThai-Bold.ttf", "url": "https://raw.githubusercontent.com/notofonts/notofonts.github.io/42af67ccd12bc0e31ed85384bf73e39248265374/fonts/NotoSansThai/full/ttf/NotoSansThai-Bold.ttf", "sha256": "dd99014fba00806b1d8da54afa978b37149b950b56c6b44b5e0b5c5dcd78f245", "size": 72840 },
    { "name": "NotoSansArabic-Light.ttf", "url": "https://raw.githubusercontent.com/notofonts/notofonts.github.io/42af67ccd12bc0e31ed85384bf73e39248265374/fonts/NotoSansArabic/full/ttf/NotoSansArabic-Light.ttf", "sha256": "fdad48f549b7d44e2926d0f19d300a221ae0a480d40e57b394df7359d2f6cf59", "size": 260596 },
    { "name": "NotoSansArabic-Regular.ttf", "url": "https://raw.githubusercontent.com/notofonts/notofonts.github.io/42af67ccd12bc0e31ed85384bf73e39248265374/fonts/NotoSansArabic/full/ttf/NotoSansArabic-Regular.ttf", "sha256": "7ed3fe069312aceac454f17cf613a30f95271d6ed7ce58005ed4d016bd3823d7", "size": 271652 },
    { "name": "NotoSansArabic-Medium.ttf", "url": "https://raw.githubusercontent.com/notofonts/notofonts.github.io/42af67ccd12bc0e31ed85384bf73e39248265374/fonts/NotoSansArabic/full/ttf/NotoSansArabic-Medium.ttf", "sha256": "23c6d6520915f455585ef796db6897393e486637510dd3b255047f7095a11478", "size": 293280 },
    { "name": "NotoSansArabic-Bold.ttf", "url": "https://raw.githubusercontent.com/notofonts/notofonts.github.io/42af67ccd12bc0e31ed85384bf73e39248265374/fonts/NotoSansArabic/full/ttf/NotoSansArabic-Bold.ttf", "sha256": "5ccd1a8914f7c7e8aa8050f2c7c37b10fc5e855f06583c2a2248a436aad3fc0f", "size": 298372 },
    { "name": "NotoSansCJK-Light.ttc", "url": "https://raw.githubusercontent.com/notofonts/noto-cjk/f8d157532fbfaeda587e826d4cd5b21a49186f7c/Sans/OTC/NotoSansCJK-Light.ttc", "sha256": "872def437cf8b9c41bf1736da891e34a8c25b764c0c4a51418d3745570660f42", "size": 18160344 },
    { "name": "NotoSansCJK-Regular.ttc", "url": "https://raw.githubusercontent.com/notofonts/noto-cjk/f8d157532fbfaeda587e826d4cd5b21a49186f7c/Sans/OTC/NotoSansCJK-Regular.ttc", "sha256": "b76b0433203017ca80401b2ee0dd69350349871c4b19d504c34dbdd80541690a", "size": 19484784 },
    { "name": "NotoSansCJK-Medium.ttc", "url": "https://raw.githubusercontent.com/notofonts/noto-cjk/f8d157532fbfaeda587e826d4cd5b21a49186f7c/Sans/OTC/NotoSansCJK-Medium.ttc", "sha256": "197d5e1e019faca33a4d55931c7d68b8056f3b97cb862049f5cb8de9efdfb8ce", "size": 18354360 },
    { "name": "NotoSansCJK-Bold.ttc", "url": "https://raw.githubusercontent.com/notofonts/noto-cjk/f8d157532fbfaeda587e826d4cd5b21a49186f7c/Sans/OTC/NotoSansCJK-Bold.ttc", "sha256": "faa5f3656a78b2e2d450d27fe8382c778bc2b6bb5ea29c986664a6a435056ceb", "size": 20050760 }
  ],
```

- [ ] **Step 7: Write `FontPack` and register both**

Create `src/PS5Kitsune.Core/Tools/FontPack.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Launch;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Tools;

/// <summary>
/// The profile's Noto fonts in &lt;root&gt;/fonts. The marker, written last, lists the files and hashes it was installed from,
/// so a profile with other fonts downloads again; files already there with the right hash are not downloaded twice.
/// </summary>
public sealed class FontPack(AnyPS5Profile profile, DataLayout layout, IDownloader downloader) : IFontPack
{
    internal const string MarkerFileName = ".complete";

    public string Folder => layout.Fonts;

    private string Marker => Path.Combine(Folder, MarkerFileName);

    public bool IsInstalled() =>
        File.Exists(Marker)
        && File.ReadAllText(Marker) == Fingerprint()
        && profile.Fonts.All(font => File.Exists(Path.Combine(Folder, font.Name)));

    /// <summary>Reports the bytes received over the whole list.</summary>
    public async Task InstallAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (IsInstalled())
        {
            return;
        }

        Directory.CreateDirectory(Folder);
        File.Delete(Marker);
        var total = profile.Fonts.Sum(font => font.Size);
        long done = 0;
        foreach (var font in profile.Fonts)
        {
            var before = done;
            var fileProgress = progress is null ? null : new Offset(progress, before, total);
            await downloader.DownloadAsync(new DownloadRequest(font.Url, Path.Combine(Folder, font.Name), font.Sha256), fileProgress, cancellationToken).ConfigureAwait(false);
            done += font.Size;
            progress?.Report(new DownloadProgress(done, total));
        }

        await File.WriteAllTextAsync(Marker, Fingerprint(), cancellationToken).ConfigureAwait(false);
    }

    private string Fingerprint() => string.Join('\n', profile.Fonts.Select(font => $"{font.Name} {font.Sha256}"));

    // One file's progress, as a part of the whole list.
    private sealed class Offset(IProgress<DownloadProgress> target, long before, long total) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => target.Report(new DownloadProgress(before + value.ReceivedBytes, total));
    }
}
```

In `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`, add `using PS5Kitsune.Core.Launch;` and, after `AddTool<WinLibsTool>(...)`:

```csharp
        AddTool<MesaTool>(services, profile, MesaTool.ToolId);
        if (profile.Tools.ContainsKey(MesaTool.ToolId))
        {
            services.AddSingleton<ILavapipe>(provider => provider.GetRequiredService<MesaTool>());
        }

        if (profile.Fonts.Count > 0)
        {
            services.AddSingleton<FontPack>();
            services.AddSingleton<IFontPack>(provider => provider.GetRequiredService<FontPack>());
        }
```

`MesaTool` is not a build tool: `BuildToolsSummary` lists Python, MinGit and WinLibs by id, so the Tools screen is unchanged; `ps5kitsune tools list` and `tools install mesa` now include it.

- [ ] **Step 8: Run the tests**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests -c Release`
Expected: 0 failed. (`Core_registration_resolves_the_checker` still counts 10 checks.)

- [ ] **Step 9: Check the license and commit**

Run: `dotnet build PS5Kitsune.slnx -c Release` — 0 warnings. Then the CI license check, if the tool is installed (`dotnet tool restore` first; `dotnet-tools.json` lists it): `dotnet nuget-license -i PS5Kitsune.slnx -t -exclude-projects "*Tests*" -a build/allowed-licenses.json -err` — exit code 0 (SharpCompress is MIT). If the tool cannot be restored offline, say so in the report; CI runs it.

```bash
git add Directory.Packages.props src/PS5Kitsune.Core tests/PS5Kitsune.Core.Tests
git commit -m "Download Lavapipe from Mesa's 7z and the Noto fonts

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 4: Core — launch preparation and GamePlayer

**Files:**
- Create: `src/PS5Kitsune.Core/Launch/LaunchProgress.cs`, `src/PS5Kitsune.Core/Launch/LaunchPreparation.cs`, `src/PS5Kitsune.Core/Launch/LavapipeUnavailableException.cs`
- Modify: `src/PS5Kitsune.Core/Abstractions/IGameLauncher.cs` (`GameLaunchRequest.Environment`)
- Modify: `src/PS5Kitsune.Core/Library/GamePlayer.cs`, `src/PS5Kitsune.Core/Library/PlayingGame.cs`
- Modify: `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`
- Create: `tests/PS5Kitsune.Core.Tests/Fakes/FakeLaunchTools.cs`
- Modify: `tests/PS5Kitsune.App.Tests/PS5Kitsune.App.Tests.csproj` (link the new fakes), `tests/PS5Kitsune.App.Tests/Fakes.cs` (`TestLibrary`)
- Test: `tests/PS5Kitsune.Core.Tests/Launch/LaunchPreparationTests.cs` (new), `tests/PS5Kitsune.Core.Tests/Library/GamePlayerTests.cs`

**Interfaces:**
- Consumes: `ILavapipe`, `IFontPack` (Task 3), `LaunchEnvironment` (Task 2), `GameSettingsValidator` (Task 1), `GameLibrary.Find/ReadHeld/MarkPlayedAsync`, `RunningGames`, `GameLock`.
- Produces:
  - `enum LaunchDownload { Lavapipe, Fonts }`, `readonly record struct LaunchProgress(LaunchDownload Download, DownloadProgress Progress)`, `sealed record LaunchResources(string? LavapipeManifest, string? FontsDirectory, string? FontsWarning)`
  - `LavapipeUnavailableException(string message, Exception? innerException = null)`
  - `LaunchPreparation(ILavapipe? lavapipe, IFontPack? fonts, ILogger<LaunchPreparation> logger)`: `Task<LaunchResources> PrepareAsync(GameSettings settings, IProgress<LaunchProgress>? progress, CancellationToken)`, `string? InstalledLavapipe()`
  - `GameLaunchRequest.Environment : IReadOnlyDictionary<string, string>` (default empty)
  - `GamePlayer(GameLibrary library, IAnyPS5Installer installer, IGameLauncher launcher, RunningGames running, LaunchPreparation preparation, ILogger<GamePlayer> logger)`
  - `GamePlayer.LaunchAsync(string titleId, IProgress<LaunchProgress>? progress, CancellationToken)` (and the 7a overload without progress)
  - `GamePlayer.SaveSettingsAsync(string titleId, GameSettings settings, CancellationToken) → Task<GameRecord>`
  - `PlayingGame.FontsWarning : string?` (init)
  - Test fakes `FakeLavapipe`, `FakeFontPack` (also linked into App.Tests).

- [ ] **Step 1: Write the fakes**

Create `tests/PS5Kitsune.Core.Tests/Fakes/FakeLaunchTools.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Launch;

namespace PS5Kitsune.Core.Tests.Fakes;

/// <summary>Mesa without a download: installing reports <see cref="Reports"/>, waits for <see cref="Gate"/> when set, then fails or installs. Also linked into the App tests.</summary>
internal sealed class FakeLavapipe : ILavapipe
{
    public const long Total = 70L << 20;

    public string Manifest { get; set; } = @"C:\root\tools\mesa\lvp_icd.x86_64.json";

    public bool Installed { get; set; }

    public Exception? Failure { get; set; }

    public int Installs { get; private set; }

    /// <summary>Bytes received, reported in order with <see cref="Total"/>.</summary>
    public long[] Reports { get; set; } = [];

    public TaskCompletionSource? Gate { get; set; }

    public bool IsInstalled() => Installed;

    public async Task InstallAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        Installs++;
        foreach (var received in Reports)
        {
            progress?.Report(new DownloadProgress(received, Total));
        }

        if (Gate is not null)
        {
            await Gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (Failure is not null)
        {
            throw Failure;
        }

        Installed = true;
    }
}

/// <summary>The fonts without a download; same behavior as <see cref="FakeLavapipe"/>.</summary>
internal sealed class FakeFontPack : IFontPack
{
    public const long Total = 86L << 20;

    public string Folder { get; set; } = @"C:\root\fonts";

    public bool Installed { get; set; }

    public Exception? Failure { get; set; }

    public int Installs { get; private set; }

    public long[] Reports { get; set; } = [];

    public TaskCompletionSource? Gate { get; set; }

    public bool IsInstalled() => Installed;

    public async Task InstallAsync(IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        Installs++;
        foreach (var received in Reports)
        {
            progress?.Report(new DownloadProgress(received, Total));
        }

        if (Gate is not null)
        {
            await Gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (Failure is not null)
        {
            throw Failure;
        }

        Installed = true;
    }
}
```

In `tests/PS5Kitsune.App.Tests/PS5Kitsune.App.Tests.csproj`, link it next to `FakeGameLauncher.cs`:

```xml
    <Compile Include="..\PS5Kitsune.Core.Tests\Fakes\FakeLaunchTools.cs" Link="FakeLaunchTools.cs" />
```

- [ ] **Step 2: Write the failing preparation tests**

Create `tests/PS5Kitsune.Core.Tests/Launch/LaunchPreparationTests.cs`:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Launch;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.Launch;

public sealed class LaunchPreparationTests
{
    private readonly FakeLavapipe _lavapipe = new();
    private readonly FakeFontPack _fonts = new();

    private LaunchPreparation Create(bool withLavapipe = true, bool withFonts = true) =>
        new(withLavapipe ? _lavapipe : null, withFonts ? _fonts : null, NullLogger<LaunchPreparation>.Instance);

    private static readonly GameSettings WithLavapipe = new() { Lavapipe = true };

    [Fact]
    public async Task Lavapipe_and_fonts_missing_are_installed_lavapipe_first_with_progress()
    {
        _lavapipe.Reports = [1L << 20];
        _fonts.Reports = [2L << 20];
        var reports = new List<LaunchProgress>();

        var resources = await Create().PrepareAsync(WithLavapipe, new SyncProgress(reports.Add), TestContext.Current.CancellationToken);

        Assert.Equal(new LaunchResources(_lavapipe.Manifest, _fonts.Folder, null), resources);
        Assert.Equal(
            [new LaunchProgress(LaunchDownload.Lavapipe, new DownloadProgress(1L << 20, FakeLavapipe.Total)), new LaunchProgress(LaunchDownload.Fonts, new DownloadProgress(2L << 20, FakeFontPack.Total))],
            reports);
    }

    [Fact]
    public async Task Nothing_to_download_installs_nothing()
    {
        _lavapipe.Installed = true;
        _fonts.Installed = true;

        var resources = await Create().PrepareAsync(WithLavapipe, null, TestContext.Current.CancellationToken);

        Assert.Equal(new LaunchResources(_lavapipe.Manifest, _fonts.Folder, null), resources);
        Assert.Equal(0, _lavapipe.Installs);
        Assert.Equal(0, _fonts.Installs);
    }

    [Fact]
    public async Task Without_lavapipe_in_the_settings_mesa_is_left_alone()
    {
        _fonts.Installed = true;

        var resources = await Create().PrepareAsync(new GameSettings(), null, TestContext.Current.CancellationToken);

        Assert.Null(resources.LavapipeManifest);
        Assert.Equal(0, _lavapipe.Installs);
    }

    [Fact]
    public async Task A_failed_mesa_download_stops_the_launch()
    {
        _lavapipe.Failure = new HttpRequestException("404");

        var exception = await Assert.ThrowsAsync<LavapipeUnavailableException>(() => Create().PrepareAsync(WithLavapipe, null, TestContext.Current.CancellationToken));

        Assert.Equal("Could not download Lavapipe: 404", exception.Message);
        Assert.Equal(0, _fonts.Installs);
    }

    [Fact]
    public async Task A_profile_without_mesa_cannot_use_lavapipe()
    {
        var exception = await Assert.ThrowsAsync<LavapipeUnavailableException>(
            () => Create(withLavapipe: false).PrepareAsync(WithLavapipe, null, TestContext.Current.CancellationToken));

        Assert.Equal("Could not download Lavapipe: this profile has no Mesa download.", exception.Message);
    }

    [Fact]
    public async Task A_failed_font_download_only_warns()
    {
        _fonts.Failure = new IOException("disk full");

        var resources = await Create().PrepareAsync(new GameSettings(), null, TestContext.Current.CancellationToken);

        Assert.Null(resources.FontsDirectory);
        Assert.Equal("Fonts could not be downloaded (disk full); games that use system fonts may show no text or stop.", resources.FontsWarning);
    }

    [Fact]
    public async Task A_timeout_is_a_failure_not_a_cancellation()
    {
        _fonts.Failure = new TaskCanceledException("timed out");

        var resources = await Create().PrepareAsync(new GameSettings(), null, TestContext.Current.CancellationToken);

        Assert.NotNull(resources.FontsWarning);
    }

    [Fact]
    public async Task Cancelling_stops_the_download()
    {
        _lavapipe.Gate = new TaskCompletionSource();
        using var cancellation = new CancellationTokenSource();
        var preparing = Create().PrepareAsync(WithLavapipe, null, cancellation.Token);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preparing);
        Assert.False(_lavapipe.Installed);
    }

    [Fact]
    public async Task No_fonts_in_the_profile_means_no_folder_and_no_warning()
    {
        var resources = await Create(withFonts: false).PrepareAsync(new GameSettings(), null, TestContext.Current.CancellationToken);

        Assert.Equal(new LaunchResources(null, null, null), resources);
    }

    [Fact]
    public void The_installed_manifest_is_known_only_once_installed()
    {
        Assert.Null(Create().InstalledLavapipe());
        _lavapipe.Installed = true;
        Assert.Equal(_lavapipe.Manifest, Create().InstalledLavapipe());
        Assert.Null(Create(withLavapipe: false).InstalledLavapipe());
    }

    private sealed class SyncProgress(Action<LaunchProgress> report) : IProgress<LaunchProgress>
    {
        public void Report(LaunchProgress value) => report(value);
    }
}
```

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build errors — `LaunchPreparation`, `LaunchProgress`, `LaunchResources`, `LavapipeUnavailableException` do not exist.

- [ ] **Step 3: Write the preparation**

Create `src/PS5Kitsune.Core/Launch/LaunchProgress.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Launch;

public enum LaunchDownload
{
    Lavapipe,
    Fonts,
}

/// <summary>A download a launch waits for.</summary>
public readonly record struct LaunchProgress(LaunchDownload Download, DownloadProgress Progress);

/// <param name="LavapipeManifest">Mesa's manifest when the settings use Lavapipe; null otherwise.</param>
/// <param name="FontsDirectory">The installed fonts; null when the profile has none or their download failed.</param>
/// <param name="FontsWarning">Why the fonts are missing after a failed download; null otherwise.</param>
public sealed record LaunchResources(string? LavapipeManifest, string? FontsDirectory, string? FontsWarning);
```

Create `src/PS5Kitsune.Core/Launch/LavapipeUnavailableException.cs`:

```csharp
namespace PS5Kitsune.Core.Launch;

/// <summary>The game uses Lavapipe and Mesa cannot be installed: the launch stops. The message is shown as is.</summary>
public sealed class LavapipeUnavailableException(string message, Exception? innerException = null) : Exception(message, innerException);
```

Create `src/PS5Kitsune.Core/Launch/LaunchPreparation.cs`:

```csharp
using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Launch;

/// <summary>
/// Downloads what a launch needs before the game lock is taken (it can take minutes): Mesa when the game uses Lavapipe, and the fonts.
/// Mesa failing stops the launch; the fonts failing only warns. See the 7b spec, "Mesa and fonts".
/// </summary>
/// <param name="lavapipe">Null when the profile has no Mesa download.</param>
/// <param name="fonts">Null when the profile has no fonts.</param>
public sealed partial class LaunchPreparation(ILavapipe? lavapipe, IFontPack? fonts, ILogger<LaunchPreparation> logger)
{
    /// <exception cref="LavapipeUnavailableException">The settings use Lavapipe and Mesa cannot be installed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<LaunchResources> PrepareAsync(GameSettings settings, IProgress<LaunchProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string? manifest = null;
        if (settings.Lavapipe)
        {
            if (lavapipe is null)
            {
                throw new LavapipeUnavailableException("Could not download Lavapipe: this profile has no Mesa download.");
            }

            if (!lavapipe.IsInstalled())
            {
                try
                {
                    await lavapipe.InstallAsync(Relay(progress, LaunchDownload.Lavapipe), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (IsDownloadFailure(exception, cancellationToken))
                {
                    throw new LavapipeUnavailableException($"Could not download Lavapipe: {exception.Message}", exception);
                }
            }

            manifest = lavapipe.Manifest;
        }

        if (fonts is null)
        {
            return new LaunchResources(manifest, null, null);
        }

        if (!fonts.IsInstalled())
        {
            try
            {
                await fonts.InstallAsync(Relay(progress, LaunchDownload.Fonts), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsDownloadFailure(exception, cancellationToken))
            {
                var reason = exception.Message;
                LogFontsFailed(exception, reason);
                return new LaunchResources(manifest, null, $"Fonts could not be downloaded ({reason}); games that use system fonts may show no text or stop.");
            }
        }

        return new LaunchResources(manifest, fonts.Folder, null);
    }

    /// <summary>Mesa's manifest when it is installed; null otherwise. For settings that turned Lavapipe on after the preparation.</summary>
    public string? InstalledLavapipe() => lavapipe is not null && lavapipe.IsInstalled() ? lavapipe.Manifest : null;

    // An HttpClient timeout surfaces as a cancellation the caller did not ask for: it is a failure.
    private static bool IsDownloadFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException or IOException or UnauthorizedAccessException or ChecksumMismatchException or InvalidDataException
        || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);

    private static Relayed? Relay(IProgress<LaunchProgress>? progress, LaunchDownload download) => progress is null ? null : new Relayed(progress, download);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The fonts could not be downloaded: {Reason}. The game starts without them.")]
    private partial void LogFontsFailed(Exception exception, string reason);

    private sealed class Relayed(IProgress<LaunchProgress> target, LaunchDownload download) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => target.Report(new LaunchProgress(download, value));
    }
}
```

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests -c Release -- --filter-class "PS5Kitsune.Core.Tests.Launch.LaunchPreparationTests"`
Expected: 10 passed.

- [ ] **Step 4: Write the failing GamePlayer tests**

In `tests/PS5Kitsune.Core.Tests/Library/GamePlayerTests.cs`:
- Add fields `private readonly FakeLavapipe _lavapipe = new();` and `private readonly FakeFontPack _fonts = new() { Installed = true };`.
- Build the player as:

```csharp
        _player = new GamePlayer(
            _library, _setup.Installer, _launcher, _running,
            new LaunchPreparation(_lavapipe, _fonts, NullLogger<LaunchPreparation>.Instance), NullLogger<GamePlayer>.Instance);
```

- Add a helper and the tests:

```csharp
    private Task SetSettingsAsync(GameSettings settings) =>
        GameRecordStore.WriteAsync(Game.RecordFile, GameRecordStore.Read(Game.RecordFile)! with { Settings = settings }, TestContext.Current.CancellationToken);

    [Fact]
    public async Task The_settings_and_the_fonts_become_the_game_environment()
    {
        await AddDoomAsync();
        await SetSettingsAsync(new GameSettings { Language = 2 });

        await LaunchAsync();

        Assert.Equal(
            new Dictionary<string, string> { ["ANYPS5_LANGUAGE"] = "2", ["ANYPS5_SYSTEM_FONTS"] = _fonts.Folder },
            Assert.Single(_launcher.Launches).Environment);
    }

    [Fact]
    public async Task Lavapipe_is_downloaded_then_passed()
    {
        await AddDoomAsync();
        await SetSettingsAsync(new GameSettings { Lavapipe = true });

        await LaunchAsync();

        Assert.Equal(1, _lavapipe.Installs);
        var environment = Assert.Single(_launcher.Launches).Environment;
        Assert.Equal(_lavapipe.Manifest, environment["VK_DRIVER_FILES"]);
        Assert.Equal("llvmpipe", environment["ANYPS5_GPU"]);
    }

    [Fact]
    public async Task Lavapipe_turned_on_after_the_preparation_is_refused_when_mesa_is_missing()
    {
        await AddDoomAsync();
        _fonts.Installed = false;
        _fonts.Gate = new TaskCompletionSource();
        var launching = LaunchAsync();
        await SetSettingsAsync(new GameSettings { Lavapipe = true });
        _fonts.Gate.SetResult();

        var exception = await Assert.ThrowsAsync<LavapipeUnavailableException>(() => launching);

        Assert.Equal("Lavapipe is not installed. Launch again to download it.", exception.Message);
        Assert.Empty(_launcher.Launches);
        Assert.False(GameLock.IsHeld(Game));
    }

    [Fact]
    public async Task A_failed_launch_is_not_counted_as_played()
    {
        await AddDoomAsync();
        _launcher.LaunchFailure = new System.ComponentModel.Win32Exception(2);

        await Assert.ThrowsAsync<System.ComponentModel.Win32Exception>(LaunchAsync);

        Assert.Null(_library.Find(TestGames.TitleId).Record.LastPlayedAt);
        Assert.Null(_running.Check(Game));
    }

    [Fact]
    public async Task A_font_warning_reaches_the_caller()
    {
        await AddDoomAsync();
        _fonts.Installed = false;
        _fonts.Failure = new HttpRequestException("offline");

        var playing = await LaunchAsync();

        Assert.Equal("Fonts could not be downloaded (offline); games that use system fonts may show no text or stop.", playing.FontsWarning);
        Assert.False(Assert.Single(_launcher.Launches).Environment.ContainsKey("ANYPS5_SYSTEM_FONTS"));
    }

    [Fact]
    public async Task A_running_game_is_refused_before_any_download()
    {
        await AddDoomAsync();
        await LaunchAsync();
        _fonts.Installed = false;

        await Assert.ThrowsAsync<GameRunningException>(LaunchAsync);

        Assert.Equal(0, _fonts.Installs);
    }

    [Fact]
    public async Task Saving_settings_keeps_the_rest_of_the_record()
    {
        await AddDoomAsync();
        var played = DateTimeOffset.UnixEpoch;
        await GameRecordStore.WriteAsync(Game.RecordFile, GameRecordStore.Read(Game.RecordFile)! with { LastPlayedAt = played }, TestContext.Current.CancellationToken);

        var saved = await _player.SaveSettingsAsync(TestGames.TitleId, new GameSettings { Overlay = true, Gpu = " " }, TestContext.Current.CancellationToken);

        Assert.Equal(new GameSettings { Overlay = true }, saved.Settings);
        var record = GameRecordStore.Read(Game.RecordFile)!;
        Assert.Equal(new GameSettings { Overlay = true }, record.Settings);
        Assert.Equal(played, record.LastPlayedAt);
        Assert.Equal("DOOM", record.Name);
    }

    [Fact]
    public async Task Saving_settings_is_refused_while_the_game_runs_or_is_busy()
    {
        await AddDoomAsync();
        using (GameLock.TryAcquireNow(Game))
        {
            await Assert.ThrowsAsync<GameBusyException>(() => _player.SaveSettingsAsync(TestGames.TitleId, new GameSettings(), TestContext.Current.CancellationToken));
        }

        await LaunchAsync();
        await Assert.ThrowsAsync<GameRunningException>(() => _player.SaveSettingsAsync(TestGames.TitleId, new GameSettings(), TestContext.Current.CancellationToken));
    }
```

(The `GameBusyException` case waits for the lock's ~250 ms retry window; that is expected.)

The 7a test `Launching_starts_the_game_from_its_folder_records_it_and_marks_it_played` keeps passing unchanged.

In `tests/PS5Kitsune.App.Tests/Fakes.cs`, in `TestLibrary`:
- add `public FakeLavapipe Lavapipe { get; } = new();` and `public FakeFontPack Fonts { get; } = new() { Installed = true };`;
- build the player with `new GamePlayer(Library, Installer, Launcher, Running, new LaunchPreparation(Lavapipe, Fonts, NullLogger<LaunchPreparation>.Instance), NullLogger<GamePlayer>.Instance)`. (Properties are initialized before the constructor body runs, so `Lavapipe` and `Fonts` exist there.)

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build errors — no `GamePlayer` constructor takes the preparation, no `SaveSettingsAsync`, no `Environment`, no `FontsWarning`.

- [ ] **Step 5: Pass the environment and change the launch order**

In `src/PS5Kitsune.Core/Abstractions/IGameLauncher.cs`, add to `GameLaunchRequest`:

```csharp
    /// <summary>Variables added to the inherited environment, for the game process only.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();
```

Replace `src/PS5Kitsune.Core/Library/PlayingGame.cs`'s record with:

```csharp
/// <summary>A game launched or found running. Disposing <see cref="Process"/> releases the handle, never the game.</summary>
public sealed record PlayingGame(GameLayout Game, GameRecord Record, ILaunchedGame Process)
{
    /// <summary>Why the fonts were not passed to this launch; null when they were, or when the profile has none.</summary>
    public string? FontsWarning { get; init; }
}
```

Replace `src/PS5Kitsune.Core/Library/GamePlayer.cs` with:

```csharp
using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Launch;

namespace PS5Kitsune.Core.Library;

/// <summary>Lists, launches and configures the added games: the one path for `run` and the app. See the 7a and 7b specs, "GamePlayer".</summary>
public sealed partial class GamePlayer(
    GameLibrary library, IAnyPS5Installer installer, IGameLauncher launcher, RunningGames running, LaunchPreparation preparation, ILogger<GamePlayer> logger)
{
    /// <summary>Every readable game, by name, with whether it runs.</summary>
    public IReadOnlyList<LibraryGame> List() =>
        [.. library.All().Select(entry => new LibraryGame(entry.Game, entry.Record, running.Check(entry.Game)))];

    /// <inheritdoc cref="LaunchAsync(string, IProgress{LaunchProgress}?, CancellationToken)"/>
    public Task<PlayingGame> LaunchAsync(string titleId, CancellationToken cancellationToken) => LaunchAsync(titleId, null, cancellationToken);

    /// <summary>
    /// Downloads what the settings need (outside the lock: it can take minutes), then launches the game from its folder with its settings
    /// as environment variables, its output in a new log, marks it played and records it in running.json. The game lock is held from the
    /// checks until the record is written, so a Reconvert or a removal cannot start in between.
    /// </summary>
    /// <exception cref="AnyPS5NotInstalledException">No AnyPS5 install is in use.</exception>
    /// <exception cref="GameNotFoundException">No game with this title ID.</exception>
    /// <exception cref="InvalidGameRecordException">The game.json is malformed.</exception>
    /// <exception cref="GameBusyException">Another action holds the game's lock.</exception>
    /// <exception cref="GameRunningException">The game is already running.</exception>
    /// <exception cref="GameExecutableMissingException">The converted executable is missing.</exception>
    /// <exception cref="LavapipeUnavailableException">The game uses Lavapipe and Mesa cannot be installed.</exception>
    public async Task<PlayingGame> LaunchAsync(string titleId, IProgress<LaunchProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(titleId);
        if (installer.ReadRecord() is null)
        {
            throw new AnyPS5NotInstalledException();
        }

        var (game, before) = library.Find(titleId);
        if (running.Check(game) is { } early)
        {
            throw new GameRunningException(before.Name, early);
        }

        var resources = await preparation.PrepareAsync(before.Settings, progress, cancellationToken).ConfigureAwait(false);

        using var held = GameLock.TryAcquire(game) ?? throw new GameBusyException(titleId);
        var record = library.ReadHeld(game);
        if (running.Check(game) is { } current)
        {
            throw new GameRunningException(record.Name, current);
        }

        var executable = Path.Combine(game.Root, record.Executable);
        if (!File.Exists(executable))
        {
            throw new GameExecutableMissingException(executable);
        }

        // The settings may have turned Lavapipe on since the preparation.
        var manifest = !record.Settings.Lavapipe
            ? null
            : resources.LavapipeManifest ?? preparation.InstalledLavapipe() ?? throw new LavapipeUnavailableException("Lavapipe is not installed. Launch again to download it.");
        var environment = LaunchEnvironment.For(record.Settings, manifest, resources.FontsDirectory);
        var process = launcher.Launch(new GameLaunchRequest(executable, game.Root, game.Logs) { Environment = environment });
        GameRecord played;
        try
        {
            // The game has started: recording it is not cancelled.
            played = await MarkPlayedAsync(game, record).ConfigureAwait(false);
            await running.RecordAsync(game, process, executable).ConfigureAwait(false);
        }
        catch
        {
            process.Dispose();
            throw;
        }

        return new PlayingGame(game, played, process) { FontsWarning = resources.FontsWarning };
    }

    /// <summary>Replaces the game's settings (normalized), keeping the rest of game.json as it is now.</summary>
    /// <returns>The record written.</returns>
    /// <exception cref="GameNotFoundException">No game with this title ID.</exception>
    /// <exception cref="InvalidGameRecordException">The game.json is malformed.</exception>
    /// <exception cref="GameBusyException">Another action holds the game's lock.</exception>
    /// <exception cref="GameRunningException">The game is running: settings only apply at launch.</exception>
    public async Task<GameRecord> SaveSettingsAsync(string titleId, GameSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(titleId);
        ArgumentNullException.ThrowIfNull(settings);
        var game = library.Layout(titleId);
        using var held = GameLock.TryAcquire(game) ?? throw new GameBusyException(titleId);
        var record = library.ReadHeld(game);
        if (running.Check(game) is { } current)
        {
            throw new GameRunningException(record.Name, current);
        }

        var saved = record with { Settings = GameSettingsValidator.Normalize(settings).Settings };
        await GameRecordStore.WriteAsync(game.RecordFile, saved, cancellationToken).ConfigureAwait(false);
        return saved;
    }

    /// <summary>Deletes the game's running.json once its exit was seen.</summary>
    public void Forget(PlayingGame playing)
    {
        ArgumentNullException.ThrowIfNull(playing);
        running.Forget(playing.Game, playing.Process);
    }

    // A record that cannot be written does not stop a game that already runs.
    private async Task<GameRecord> MarkPlayedAsync(GameLayout game, GameRecord record)
    {
        try
        {
            return await library.MarkPlayedAsync(game, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidGameRecordException or GameNotFoundException)
        {
            var titleId = record.TitleId;
            LogNotMarkedPlayed(exception, titleId);
            return record;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{TitleId} started, but its last played date could not be written.")]
    private partial void LogNotMarkedPlayed(Exception exception, string titleId);
}
```

In `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`, before `services.AddSingleton<GamePlayer>();`:

```csharp
        services.AddSingleton(provider => new LaunchPreparation(
            provider.GetService<ILavapipe>(), provider.GetService<IFontPack>(), provider.GetRequiredService<ILogger<LaunchPreparation>>()));
```

(Add `using Microsoft.Extensions.Logging;` if missing.)

- [ ] **Step 6: Run the tests**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: 0 failed. The App tests build again with the new `TestLibrary`; the `GameViewModel` and Library tests are unchanged in this task and still pass (fonts installed, Lavapipe off: same launches as before, now with `ANYPS5_SYSTEM_FONTS` in the request).

- [ ] **Step 7: Commit**

Run: `dotnet build PS5Kitsune.slnx -c Release` — 0 warnings.

```bash
git add -A src/PS5Kitsune.Core tests
git commit -m "Prepare Lavapipe and the fonts before a launch, pass the settings, and mark a game played only once started

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 5: Platform — the game's environment, breakaway and exit detection

**Files:**
- Modify: `src/PS5Kitsune.Platform.Windows/WindowsGameLauncher.cs`
- Modify: `tests/PS5Kitsune.Platform.Windows.Tests/PS5Kitsune.Platform.Windows.Tests.csproj` (`AllowUnsafeBlocks` for the test's `LibraryImport`)
- Test: `tests/PS5Kitsune.Platform.Windows.Tests/WindowsGameLauncherTests.cs`

**Interfaces:**
- Consumes: `GameLaunchRequest.Environment` (Task 4).
- Produces: no new API. Behavior: the environment block, `CREATE_BREAKAWAY_FROM_JOB` with a fallback, exit detected with `WaitForSingleObject(handle, 0)`.

- [ ] **Step 1: Write the failing tests**

In `tests/PS5Kitsune.Platform.Windows.Tests/PS5Kitsune.Platform.Windows.Tests.csproj`, add to a `PropertyGroup`:

```xml
    <!-- Required by the LibraryImport source generator (the job object used by the breakaway test). -->
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
```

In `WindowsGameLauncherTests.cs`, make `RunCmdAsync` take an optional environment (the class becomes `partial` for the `LibraryImport` methods below):

```csharp
    private async Task<(int ExitCode, string Log, string LogFile)> RunCmdAsync(IReadOnlyDictionary<string, string>? environment, params string[] arguments)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var game = _launcher.Launch(new GameLaunchRequest(Path.Combine(Environment.SystemDirectory, "cmd.exe"), _temp.Path, Logs)
        {
            Arguments = arguments,
            Environment = environment ?? new Dictionary<string, string>(),
        });
        var exitCode = await game.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        return (exitCode, await File.ReadAllTextAsync(game.LogFile, cancellationToken), game.LogFile);
    }

    private Task<(int ExitCode, string Log, string LogFile)> RunCmdAsync(params string[] arguments) => RunCmdAsync(null, arguments);
```

Add the tests:

```csharp
    [Fact]
    public async Task Passes_the_settings_to_the_game_only_on_top_of_the_inherited_environment()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");

        var (_, log, _) = await RunCmdAsync(
            new Dictionary<string, string> { ["PS5KITSUNE_TEST_SETTING"] = "from-settings" },
            "/c", "echo", "%PS5KITSUNE_TEST_SETTING%|%SystemRoot%");

        Assert.Contains("from-settings|", log, StringComparison.Ordinal);
        Assert.Contains(Environment.GetEnvironmentVariable("SystemRoot")!, log, StringComparison.OrdinalIgnoreCase);
        Assert.Null(Environment.GetEnvironmentVariable("PS5KITSUNE_TEST_SETTING"));
    }

    [Fact]
    public async Task A_game_that_exited_with_code_259_is_not_found_running()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        using var game = _launcher.Launch(new GameLaunchRequest(cmd, _temp.Path, Logs) { Arguments = ["/c", "exit", "259"] });

        // The handle held here keeps the PID from being reused while Attach looks at it.
        var exitCode = await game.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.Equal(259, exitCode);
        Assert.Null(_launcher.Attach(RecordOf(game, cmd)));
    }

    [Fact]
    public async Task The_game_breaks_away_from_a_job_that_allows_it()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        using var self = System.Diagnostics.Process.GetCurrentProcess();
        if (IsProcessInJob(self.SafeHandle, 0, out var alreadyInJob) && alreadyInJob)
        {
            // Breaking away from nested jobs also depends on the outer job.
            Assert.Skip("The test process already runs in a job.");
        }

        using var job = CreateJobObject(0, null);
        var limits = new ExtendedLimitInformation { BasicLimitInformation = new BasicLimitInformation { LimitFlags = JobObjectLimitBreakawayOk } };
        Assert.True(SetInformationJobObject(job, JobObjectExtendedLimitInformation, ref limits, (uint)Marshal.SizeOf<ExtendedLimitInformation>()));
        // The test process stays in this job: it has no limit but allowing breakaway, and closing it ends nothing.
        Assert.True(AssignProcessToJobObject(job, self.SafeHandle));

        using var game = StartPing();
        using var process = System.Diagnostics.Process.GetProcessById(game.ProcessId);

        Assert.True(IsProcessInJob(process.SafeHandle, job.DangerousGetHandle(), out var inJob));
        Assert.False(inJob);
        await game.StopAsync(TestContext.Current.CancellationToken);
    }

    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JobObjectLimitBreakawayOk = 0x00000800;

    [LibraryImport("kernel32.dll", EntryPoint = "CreateJobObjectW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateJobObject(nint jobAttributes, string? name);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetInformationJobObject(SafeFileHandle job, int infoClass, ref ExtendedLimitInformation info, uint length);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AssignProcessToJobObject(SafeFileHandle job, SafeProcessHandle process);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsProcessInJob(SafeProcessHandle process, nint job, [MarshalAs(UnmanagedType.Bool)] out bool result);

#pragma warning disable CS0649 // Native layouts: Windows reads fields the C# code never touches.
    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimitInformation
    {
        public BasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }
#pragma warning restore CS0649
```

(Usings to add: `System.Runtime.InteropServices`, `Microsoft.Win32.SafeHandles`. A job handle closes with `CloseHandle`, which is what `SafeFileHandle` releases with. Make the class `public sealed partial class`.)

Run: `dotnet test --project tests/PS5Kitsune.Platform.Windows.Tests -c Release -- --filter-class "PS5Kitsune.Platform.Windows.Tests.WindowsGameLauncherTests"`
Expected: the environment test fails (the variable is not set: `%PS5KITSUNE_TEST_SETTING%` is echoed literally), the 259 test fails (`Attach` returns a game), the breakaway test fails (`inJob` is true) or skips.

- [ ] **Step 2: Pass the environment, break away, detect the exit by handle**

In `src/PS5Kitsune.Platform.Windows/WindowsGameLauncher.cs`:

- Update the class summary's end: "…closing the launcher neither blocks nor kills it: the game is started out of the launcher's job when the job allows it (see docs/architecture.md, "Launching")."
- Constants: remove `StillActive`; add

```csharp
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const uint CreateBreakawayFromJob = 0x01000000;
    private const int ErrorAccessDenied = 5;
    private const uint WaitObject0 = 0;
```

- In `Launch`, replace `var process = Start(request.Executable, commandLine, request.WorkingDirectory, log.DangerousGetHandle());` with:

```csharp
        var environment = request.Environment.Count == 0 ? null : EnvironmentBlock(request.Environment);
        ProcessInformation process;
        try
        {
            process = Start(request.Executable, commandLine, request.WorkingDirectory, log.DangerousGetHandle(), environment, breakaway: true);
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == ErrorAccessDenied)
        {
            // The job PS5Kitsune runs in (a terminal, another launcher) forbids breakaway: the game then stays in that job.
            var executable = request.Executable;
            LogBreakawayRefused(executable);
            process = Start(request.Executable, commandLine, request.WorkingDirectory, log.DangerousGetHandle(), environment, breakaway: false);
        }
```

- In `Attach`, replace `&& GetExitCodeProcess(process, out var exitCode) && exitCode == StillActive` with `&& !HasExited(process)`.
- Change `Start`'s signature to `private static unsafe ProcessInformation Start(string executable, string commandLine, string workingDirectory, nint log, string? environment, bool breakaway)` and, inside, replace the `CreateProcess` call with:

```csharp
                var flags = CreateNoWindow | ExtendedStartupInfoPresent
                    | (breakaway ? CreateBreakawayFromJob : 0)
                    | (environment is null ? 0 : CreateUnicodeEnvironment);
                var commandLineBuffer = (commandLine + '\0').ToCharArray();
                fixed (char* block = environment)
                {
                    if (!CreateProcess(executable, ref commandLineBuffer[0], 0, 0, inheritHandles: true, flags, (nint)block, workingDirectory, ref startupInfo, out var process))
                    {
                        throw new Win32Exception(Marshal.GetLastPInvokeError(), $"Could not start {executable}.");
                    }

                    return process;
                }
```

(`fixed` on a null string gives a null pointer, so a game without variables inherits the launcher's environment as before.)

- Add after `ImagePath`:

```csharp
    // An exit code of 259 (STILL_ACTIVE) is also a real exit code: only the handle tells whether the process ended.
    private static bool HasExited(SafeProcessHandle process) => WaitForSingleObject(process, 0) == WaitObject0;

    // The launcher's environment with the game's variables on top, sorted by name as CreateProcess expects, double null-terminated.
    private static string EnvironmentBlock(IReadOnlyDictionary<string, string> overrides)
    {
        var variables = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            variables[(string)entry.Key] = (string?)entry.Value ?? "";
        }

        foreach (var (name, value) in overrides)
        {
            variables[name] = value;
        }

        var block = new System.Text.StringBuilder();
        foreach (var (name, value) in variables)
        {
            block.Append(name).Append('=').Append(value).Append('\0');
        }

        return block.Append('\0').ToString();
    }
```

- In `LaunchedGame.StopAsync`, replace `if (!exit.IsCompleted && !(GetExitCodeProcess(process, out var code) && code != StillActive))` with `if (!exit.IsCompleted && !HasExited(process))`.
- Add the log method and the import:

```csharp
    [LoggerMessage(Level = LogLevel.Information, Message = "Started {Executable} inside PS5Kitsune's parent job: that job does not allow breakaway, so closing it also ends the game.")]
    private partial void LogBreakawayRefused(string executable);
```

```csharp
    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial uint WaitForSingleObject(SafeProcessHandle handle, uint milliseconds);
```

`GetExitCodeProcess` stays (used by `WaitForExitAsync`).

- [ ] **Step 3: Run the tests**

Run: `dotnet test --project tests/PS5Kitsune.Platform.Windows.Tests -c Release`
Expected: 0 failed (the breakaway test may be skipped, see its message).

- [ ] **Step 4: Commit**

Run: `dotnet build PS5Kitsune.slnx -c Release` — 0 warnings.

```bash
git add src/PS5Kitsune.Platform.Windows tests/PS5Kitsune.Platform.Windows.Tests
git commit -m "Pass the game's environment, start it out of the launcher's job, and detect its exit by handle

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 6: CLI — downloads in `run`, and `settings`

**Files:**
- Create: `src/PS5Kitsune.Cli/ConsoleLaunchProgress.cs`, `src/PS5Kitsune.Cli/SettingsCommand.cs`
- Modify: `src/PS5Kitsune.Cli/RunCommand.cs`, `src/PS5Kitsune.Cli/CliLog.cs`, `src/PS5Kitsune.Cli/Program.cs`

**Interfaces:**
- Consumes: `GamePlayer.LaunchAsync(titleId, IProgress<LaunchProgress>?, ct)`, `PlayingGame.FontsWarning` (Task 4), `GameSettingsReport.Lines` (Task 2), `ILavapipe`, `IFontPack` (Task 3), `ConsoleDownloadProgress`.
- Produces: the `settings` command; `run` prints "Downloading Lavapipe…" / "Downloading fonts…" then the progress, and the fonts warning.

There is no CLI test project: the logic is in Core (`GameSettingsReport`, tested in Task 2). This task is checked by building and running the commands.

- [ ] **Step 1: Print the downloads and the fonts warning in `run`**

Create `src/PS5Kitsune.Cli/ConsoleLaunchProgress.cs`:

```csharp
using PS5Kitsune.Core.Launch;

namespace PS5Kitsune.Cli;

/// <summary>Prints which download a launch waits for, then its progress every 10 %.</summary>
internal sealed class ConsoleLaunchProgress : IProgress<LaunchProgress>
{
    private LaunchDownload? _current;
    private ConsoleDownloadProgress _progress = new();

    public void Report(LaunchProgress value)
    {
        if (value.Download != _current)
        {
            _current = value.Download;
            _progress = new ConsoleDownloadProgress();
            Console.WriteLine(value.Download == LaunchDownload.Lavapipe ? "Downloading Lavapipe…" : "Downloading fonts…");
        }

        _progress.Report(value.Progress);
    }
}
```

In `src/PS5Kitsune.Cli/RunCommand.cs`:
- Replace `var playing = await player.LaunchAsync(parseResult.GetRequiredValue(titleIdArgument), cancellationToken);` with:

```csharp
            // Ctrl+C during a download cancels the launch; LavapipeUnavailableException is printed by CommandHost (exit code 1).
            var playing = await player.LaunchAsync(parseResult.GetRequiredValue(titleIdArgument), new ConsoleLaunchProgress(), cancellationToken);
```

- Update the comment above it so it also names the Lavapipe refusal (the existing comment lists what throws).
- Before `logger.GameStarted(...)`, add:

```csharp
            if (playing.FontsWarning is { } warning)
            {
                logger.FontsMissing(warning);
            }
```

In `src/PS5Kitsune.Cli/CliLog.cs`, add after `GameRemoved`:

```csharp
    [LoggerMessage(Level = LogLevel.Warning, Message = "{Warning}")]
    public static partial void FontsMissing(this ILogger logger, string warning);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Line}")]
    public static partial void SettingsLine(this ILogger logger, string line);
```

- [ ] **Step 2: Add `settings`**

Create `src/PS5Kitsune.Cli/SettingsCommand.cs`:

```csharp
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core.Launch;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Cli;

internal static class SettingsCommand
{
    public static Command Create(Option<DirectoryInfo?> rootOption)
    {
        var titleIdArgument = new Argument<string>("titleId") { Description = "Title ID of an added game, for example PPSA99666." };
        var command = new Command("settings", "Show a game's settings and the environment variables they become at launch. Edit them in the app.") { titleIdArgument };
        command.SetAction((parseResult, _) => CommandHost.RunAsync(parseResult.GetValue(rootOption), "settings", (services, logger) =>
        {
            // An unknown title ID throws GameNotFoundException: CommandHost prints it and exits with 1.
            var (_, record) = services.GetRequiredService<GameLibrary>().Find(parseResult.GetRequiredValue(titleIdArgument));
            var lavapipe = services.GetService<ILavapipe>();
            var fonts = services.GetService<IFontPack>();
            var lines = GameSettingsReport.Lines(record.Settings, lavapipe?.Manifest, lavapipe?.IsInstalled() == true, fonts?.Folder, fonts?.IsInstalled() == true);
            foreach (var line in lines)
            {
                logger.SettingsLine(line);
            }

            return Task.FromResult(0);
        }));
        return command;
    }
}
```

In `src/PS5Kitsune.Cli/Program.cs`, add `SettingsCommand.Create(rootOption),` after `ListCommand.Create(rootOption),`.

- [ ] **Step 3: Build and try the commands**

Run: `dotnet build PS5Kitsune.slnx -c Release` — 0 warnings.

Then, with an empty temporary root (the scratch folder is fine; do not use `C:\PS5Kitsune`):

```bash
dotnet run --project src/PS5Kitsune.Cli -c Release --no-build -- settings --help
dotnet run --project src/PS5Kitsune.Cli -c Release --no-build -- settings PPSA99999 --root "<temp folder>"
dotnet run --project src/PS5Kitsune.Cli -c Release --no-build -- tools list --root "<temp folder>"
```

Expected: the help shows the `titleId` argument; `settings PPSA99999` prints "No game PPSA99999 in <temp folder>\games." and exits with 1; `tools list` lists `mesa  missing` with the other tools. Put the outputs in the report.

- [ ] **Step 4: Run the tests and commit**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release` — 0 failed.

```bash
git add src/PS5Kitsune.Cli
git commit -m "Show downloads in run and add the settings command

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 7: App — the Game page launches, with download progress

**Files:**
- Modify: `src/PS5Kitsune.App/ViewModels/GameViewModel.cs`, `src/PS5Kitsune.App/ViewModels/LaunchErrors.cs`, `src/PS5Kitsune.App/Views/GameView.axaml`
- Create: `src/PS5Kitsune.App/ViewModels/DispatchedProgress.cs`
- Modify: `src/PS5Kitsune.App/ViewModels/LibraryViewModel.cs`
- Modify: `src/PS5Kitsune.App/Navigation/IPages.cs`, `src/PS5Kitsune.App/Shell/PageFactory.cs`
- Test: `tests/PS5Kitsune.App.Tests/GameViewModelTests.cs`, `tests/PS5Kitsune.App.Tests/LibraryViewModelTests.cs`, `tests/PS5Kitsune.App.Tests/Fakes.cs` (`FakePages`)

**Interfaces:**
- Consumes: `GamePlayer.LaunchAsync(titleId, IProgress<LaunchProgress>?, ct)`, `PlayingGame.FontsWarning`, `LavapipeUnavailableException`, `TestLibrary.Lavapipe`/`Fonts` (Task 4).
- Produces:
  - `IPages.Launch(GameLayout game, string name, bool firstLaunch)` (FakePages: `$"launch:{game.TitleId}"`)
  - `GameViewModel(GameLayout game, string name, bool firstLaunch, GamePlayer player, IPlatform platform, IUiDispatcher ui, Navigator navigator, IPages pages)` (launches when shown); the 7a constructor `GameViewModel(PlayingGame playing, bool firstLaunch, …)` stays.
  - `GameViewModel.IsPreparing`, `CanLaunchAgain` (replaces `HasExited`), `FontsWarning`, `Launching` (Task), `CancelLaunchCommand`.
  - `DispatchedProgress<T>(IUiDispatcher ui, Action<T> report) : IProgress<T>` (internal).

- [ ] **Step 1: Write the failing tests**

In `tests/PS5Kitsune.App.Tests/Fakes.cs`, add to `FakePages`:

```csharp
    public object Launch(GameLayout game, string name, bool firstLaunch) => $"launch:{game.TitleId}";
```

In `tests/PS5Kitsune.App.Tests/LibraryViewModelTests.cs`:
- Replace `Clicking_a_game_launches_it_and_opens_its_page` with:

```csharp
    [Fact]
    public async Task Clicking_a_game_opens_its_page_which_launches_it()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        var page = await CreateAsync();

        await page.OpenCommand.ExecuteAsync(Tile(page, "Alpha"));

        Assert.Empty(_library.Launcher.Launches);
        Assert.Equal("launch:PPSA00001", _navigator.CurrentPage);
    }
```

- Delete `A_busy_game_says_why` and `A_missing_executable_suggests_a_reconvert`: launch errors are now shown by the Game page (tests below).

In `tests/PS5Kitsune.App.Tests/GameViewModelTests.cs`:
- Replace `Assert.True(page.HasExited);` with `Assert.True(page.CanLaunchAgain);`.
- Add `using PS5Kitsune.Core.Library;` and `using PS5Kitsune.Core.Launch;` if missing, and these helpers and tests:

```csharp
    private async Task<GameViewModel> OpenLaunchPageAsync(bool firstLaunch = true)
    {
        await _library.AddAsync("PPSA99666", "DOOM");
        return new GameViewModel(_library.Game("PPSA99666"), "DOOM", firstLaunch, _library.Player, _library.Platform, new InlineDispatcher(), _navigator, new FakePages());
    }

    // The launch runs on the thread pool; its progress reaches the page through the (inline) dispatcher.
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out.");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    private Task SetSettingsAsync(GameSettings settings) =>
        _library.Player.SaveSettingsAsync("PPSA99666", settings, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Opening_the_page_launches_the_game_and_follows_it()
    {
        var page = await OpenLaunchPageAsync();
        Assert.Equal("Starting…", page.StatusText);

        page.OnNavigatedTo();
        await page.Launching;

        Assert.Single(_library.Launcher.Launches);
        Assert.Equal("Running (PID 4812)", page.StatusText);
        Assert.True(page.IsRunning);
        Assert.False(page.IsPreparing);
        Assert.Equal("The first launch may stutter while shaders are prepared.", page.FirstLaunchHint);
    }

    [Fact]
    public async Task A_font_download_shows_its_progress_and_can_be_cancelled()
    {
        _library.Fonts.Installed = false;
        _library.Fonts.Reports = [34L << 20];
        _library.Fonts.Gate = new TaskCompletionSource();
        var page = await OpenLaunchPageAsync();

        page.OnNavigatedTo();
        await WaitUntilAsync(() => page.StatusText.StartsWith("Downloading", StringComparison.Ordinal));

        Assert.Equal("Downloading fonts… 34 MB / 86 MB", page.StatusText);
        Assert.True(page.IsPreparing);
        Assert.False(page.CanLaunchAgain);
        page.CancelLaunchCommand.Execute(null);
        await page.Launching;
        Assert.Equal("Launch cancelled", page.StatusText);
        Assert.Empty(_library.Launcher.Launches);
        Assert.True(page.CanLaunchAgain);
    }

    [Fact]
    public async Task Lavapipe_is_downloaded_first_then_the_game_starts()
    {
        var page = await OpenLaunchPageAsync();
        await SetSettingsAsync(new GameSettings { Lavapipe = true });
        _library.Lavapipe.Reports = [34L << 20];
        _library.Lavapipe.Gate = new TaskCompletionSource();

        page.OnNavigatedTo();
        await WaitUntilAsync(() => page.StatusText.StartsWith("Downloading", StringComparison.Ordinal));
        Assert.Equal("Downloading Lavapipe… 34 MB / 70 MB", page.StatusText);
        _library.Lavapipe.Gate.SetResult();
        await page.Launching;

        Assert.True(page.IsRunning);
        Assert.Equal(_library.Lavapipe.Manifest, Assert.Single(_library.Launcher.Launches).Environment["VK_DRIVER_FILES"]);
    }

    [Fact]
    public async Task A_font_warning_is_shown_and_the_game_still_starts()
    {
        _library.Fonts.Installed = false;
        _library.Fonts.Failure = new HttpRequestException("offline");
        var page = await OpenLaunchPageAsync();

        page.OnNavigatedTo();
        await page.Launching;

        Assert.Equal("Fonts could not be downloaded (offline); games that use system fonts may show no text or stop.", page.FontsWarning);
        Assert.True(page.IsRunning);
    }

    [Fact]
    public async Task A_failed_lavapipe_download_offers_to_launch_again()
    {
        var page = await OpenLaunchPageAsync();
        await SetSettingsAsync(new GameSettings { Lavapipe = true });
        _library.Lavapipe.Failure = new HttpRequestException("404");

        page.OnNavigatedTo();
        await page.Launching;

        Assert.Equal("Could not download Lavapipe: 404", page.Message);
        Assert.Equal("Not running", page.StatusText);
        Assert.True(page.CanLaunchAgain);
    }

    [Fact]
    public async Task A_busy_game_says_why()
    {
        var page = await OpenLaunchPageAsync();

        using (GameLock.TryAcquireNow(_library.Game("PPSA99666")))
        {
            page.OnNavigatedTo();
            await page.Launching;
        }

        Assert.Equal("Another PS5Kitsune action is using DOOM. Try again when it finishes.", page.Message);
        Assert.Equal("Not running", page.StatusText);
    }

    [Fact]
    public async Task A_missing_executable_suggests_a_reconvert()
    {
        var page = await OpenLaunchPageAsync();
        File.Delete(_library.Game("PPSA99666").Executable);

        page.OnNavigatedTo();
        await page.Launching;

        Assert.Equal("The executable is missing: Reconvert DOOM.", page.Message);
    }

    [Fact]
    public async Task Leaving_during_a_download_cancels_the_launch()
    {
        _library.Fonts.Installed = false;
        _library.Fonts.Gate = new TaskCompletionSource();
        var page = await OpenLaunchPageAsync();
        page.OnNavigatedTo();
        await WaitUntilAsync(() => page.IsPreparing && _library.Fonts.Installs == 1);

        await page.BackToLibraryCommand.ExecuteAsync(null);

        Assert.Equal("library", _navigator.CurrentPage);
        Assert.Empty(_library.Launcher.Launches);
    }
```

(`A_refused_launch_again_is_shown` keeps its assertions: a refused relaunch keeps the previous status, "Exited with code 0".)

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build errors — `IPages.Launch`, the new `GameViewModel` constructor, `Launching`, `IsPreparing`, `CanLaunchAgain`, `FontsWarning`, `CancelLaunchCommand` do not exist.

- [ ] **Step 2: Add the page to the navigation**

In `src/PS5Kitsune.App/Navigation/IPages.cs`, after `Game(...)`:

```csharp
    /// <summary>A game to launch: the page downloads what it needs, launches it, then follows it.</summary>
    /// <param name="firstLaunch">The game was never played before: shaders are prepared, so it may stutter.</param>
    object Launch(GameLayout game, string name, bool firstLaunch);
```

(add `using PS5Kitsune.Core.Games;` if missing).

In `src/PS5Kitsune.App/Shell/PageFactory.cs`, after `Game(...)`:

```csharp
    public object Launch(GameLayout game, string name, bool firstLaunch) =>
        new GameViewModel(game, name, firstLaunch, Session.Get<GamePlayer>(), platform, ui, navigator, this);
```

In `src/PS5Kitsune.App/ViewModels/LibraryViewModel.cs`, in `LaunchAsync`, replace everything from `PlayingGame playing;` to the end of the method with:

```csharp
        navigator.GoTo(pages.Launch(tile.Entry.Game, tile.Name, firstLaunch: tile.LastPlayedAt is null));
```

and update the method's comment if it mentions launching here. The one-game-at-a-time dialog stays before it.

- [ ] **Step 3: Launch from the Game page**

Create `src/PS5Kitsune.App/ViewModels/DispatchedProgress.cs`:

```csharp
namespace PS5Kitsune.App.ViewModels;

/// <summary>Reports progress on the UI thread; the downloads report from the thread pool.</summary>
internal sealed class DispatchedProgress<T>(IUiDispatcher ui, Action<T> report) : IProgress<T>
{
    public void Report(T value) => ui.Post(() => report(value));
}
```

In `src/PS5Kitsune.App/ViewModels/LaunchErrors.cs`, add `using PS5Kitsune.Core.Launch;` and, before the `GameNotFoundException or …` case:

```csharp
        LavapipeUnavailableException => exception.Message,
```

Replace `src/PS5Kitsune.App/ViewModels/GameViewModel.cs` with:

```csharp
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Launch;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.App.ViewModels;

/// <summary>
/// A game's page: launches it (downloading Lavapipe and the fonts first when needed), follows its log live, Stop, then its exit code.
/// Leaving the page never stops the game; leaving during a download cancels the launch.
/// </summary>
public sealed partial class GameViewModel : ObservableObject, INavigationAware
{
    /// <summary>Log lines kept on screen; the file keeps them all.</summary>
    public const int KeptLines = 500;

    private readonly GameLayout _game;
    private readonly GamePlayer _player;
    private readonly IPlatform _platform;
    private readonly IUiDispatcher _ui;
    private readonly Navigator _navigator;
    private readonly IPages _pages;
    private PlayingGame? _playing;
    private CancellationTokenSource? _following;
    private CancellationTokenSource? _launching;
    private bool _started;

    /// <summary>A game found running: the page follows it.</summary>
    public GameViewModel(PlayingGame playing, bool firstLaunch, GamePlayer player, IPlatform platform, IUiDispatcher ui, Navigator navigator, IPages pages)
        : this(playing.Game, playing.Record.Name, firstLaunch, player, platform, ui, navigator, pages)
    {
        _playing = playing;
        IsPreparing = false;
        ShowRunning(playing);
    }

    /// <summary>A game to launch: the page launches it when shown.</summary>
    public GameViewModel(GameLayout game, string name, bool firstLaunch, GamePlayer player, IPlatform platform, IUiDispatcher ui, Navigator navigator, IPages pages)
    {
        _game = game;
        _player = player;
        _platform = platform;
        _ui = ui;
        _navigator = navigator;
        _pages = pages;
        Title = name;
        FirstLaunchHint = firstLaunch ? "The first launch may stutter while shaders are prepared." : null;
        IsPreparing = true;
    }

    public string Title { get; }

    public ObservableCollection<string> Log { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLaunchAgain))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand), nameof(LaunchAgainCommand))]
    public partial bool IsRunning { get; private set; }

    /// <summary>Downloading or starting: Cancel stops the launch.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLaunchAgain))]
    [NotifyCanExecuteChangedFor(nameof(CancelLaunchCommand), nameof(LaunchAgainCommand))]
    public partial bool IsPreparing { get; private set; }

    public bool CanLaunchAgain => !IsRunning && !IsPreparing;

    /// <summary>"Starting…", a download, "Running (PID n)", "Exited with code n", "Launch cancelled" or "Not running".</summary>
    [ObservableProperty]
    public partial string StatusText { get; private set; } = "Starting…";

    [ObservableProperty]
    public partial string? FirstLaunchHint { get; private set; }

    /// <summary>The fonts warning of the current launch; null otherwise.</summary>
    [ObservableProperty]
    public partial string? FontsWarning { get; private set; }

    /// <summary>Why the last action failed; null otherwise.</summary>
    [ObservableProperty]
    public partial string? Message { get; private set; }

    /// <summary>Completes when the current launch has started the game, failed or been cancelled.</summary>
    public Task Launching { get; private set; } = Task.CompletedTask;

    /// <summary>Completes when the page stops following the current process: it exited, or the page was left.</summary>
    public Task Following { get; private set; } = Task.CompletedTask;

    public void OnNavigatedTo()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        if (_playing is { } playing)
        {
            Following = FollowAsync(playing);
        }
        else
        {
            Launching = LaunchAsync();
        }
    }

    private void ShowRunning(PlayingGame playing)
    {
        IsRunning = true;
        StatusText = string.Create(CultureInfo.InvariantCulture, $"Running (PID {playing.Process.ProcessId})");
    }

    // Launches, then starts following the game without waiting for it to exit.
    private async Task LaunchAsync()
    {
        var previousStatus = StatusText;
        Message = null;
        FontsWarning = null;
        IsPreparing = true;
        StatusText = "Starting…";
        using var cancellation = new CancellationTokenSource();
        _launching = cancellation;
        PlayingGame playing;
        try
        {
            var progress = new DispatchedProgress<LaunchProgress>(_ui, ShowDownload);
            playing = await Task.Run(() => _player.LaunchAsync(_game.TitleId, progress, cancellation.Token), CancellationToken.None).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Launch cancelled";
            return;
        }
        catch (Exception exception) when (LaunchErrors.Describe(exception, Title) is { } message)
        {
            Message = message;
            // A refused relaunch keeps the exit code on screen.
            StatusText = _playing is null ? "Not running" : previousStatus;
            return;
        }
        finally
        {
            _launching = null;
            IsPreparing = false;
        }

        _playing?.Process.Dispose();
        _playing = playing;
        Log.Clear();
        FontsWarning = playing.FontsWarning;
        ShowRunning(playing);
        Following = FollowAsync(playing);
    }

    private void ShowDownload(LaunchProgress value)
    {
        // A report can arrive after the launch moved on.
        if (!IsPreparing)
        {
            return;
        }

        var what = value.Download == LaunchDownload.Lavapipe ? "Lavapipe" : "fonts";
        var received = value.Progress.ReceivedBytes >> 20;
        StatusText = value.Progress.TotalBytes is { } total
            ? string.Create(CultureInfo.InvariantCulture, $"Downloading {what}… {received} MB / {total >> 20} MB")
            : string.Create(CultureInfo.InvariantCulture, $"Downloading {what}… {received} MB");
    }

    private async Task FollowAsync(PlayingGame playing)
    {
        using var cancellation = new CancellationTokenSource();
        _following = cancellation;
        var cancellationToken = cancellation.Token;
        var exit = playing.Process.WaitForExitAsync(cancellationToken);
        try
        {
            try
            {
                await Task.Run(
                    () => LogTail.FollowAsync(playing.Process.LogFile, line => _ui.Post(() => AddLine(line)), exit, cancellationToken),
                    cancellationToken).ConfigureAwait(true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Message = $"Cannot follow the log: {exception.Message}";
            }

            var exitCode = await exit.ConfigureAwait(true);
            _player.Forget(playing);
            IsRunning = false;
            StatusText = string.Create(CultureInfo.InvariantCulture, $"Exited with code {exitCode}");
        }
        catch (OperationCanceledException)
        {
            // The page was left: the game keeps running and stays recorded.
        }
        finally
        {
            _following = null;
        }
    }

    private void AddLine(string line)
    {
        Log.Add(line);
        while (Log.Count > KeptLines)
        {
            Log.RemoveAt(0);
        }
    }

    [RelayCommand(CanExecute = nameof(IsPreparing))]
    private void CancelLaunch() => _launching?.Cancel();

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private async Task StopAsync()
    {
        if (_playing is not { } playing)
        {
            return;
        }

        Message = null;
        try
        {
            await playing.Process.StopAsync().ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Message = $"Could not stop {Title}: {exception.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanLaunchAgain))]
    private Task LaunchAgainAsync()
    {
        FirstLaunchHint = null;
        Launching = LaunchAsync();
        return Launching;
    }

    [RelayCommand]
    private async Task BackToLibraryAsync()
    {
        if (_launching is { } launching)
        {
            await launching.CancelAsync().ConfigureAwait(true);
        }

        await Launching.ConfigureAwait(true);
        if (_following is { } following)
        {
            await following.CancelAsync().ConfigureAwait(true);
        }

        await Following.ConfigureAwait(true);
        _playing?.Process.Dispose();
        _navigator.GoHome(_pages.Library());
    }

    [RelayCommand]
    private void OpenFolder() => Message = PathOpener.TryOpen(_platform, _game.Root);
}
```

(The chained constructor runs the launch constructor first, which sets `IsPreparing = true`; the running constructor then clears it. If an analyzer flags `playing` being used before a null check in the `: this(...)` call, it was the same in 7a.)

- [ ] **Step 4: Update the view**

In `src/PS5Kitsune.App/Views/GameView.axaml`:
- After the `FirstLaunchHint` TextBlock, add:

```xml
      <TextBlock Text="{Binding FontsWarning}" IsVisible="{Binding FontsWarning, Converter={x:Static ObjectConverters.IsNotNull}}"
                 Foreground="#9D5D00" TextWrapping="Wrap" />
```

- In the buttons, before "Stop":

```xml
      <Button Content="Cancel" Command="{Binding CancelLaunchCommand}" IsVisible="{Binding IsPreparing}" />
```

- Change "Launch again"'s `IsVisible="{Binding HasExited}"` to `IsVisible="{Binding CanLaunchAgain}"`.

- [ ] **Step 5: Run the tests**

Run: `dotnet test --project tests/PS5Kitsune.App.Tests -c Release`
Expected: 0 failed.

- [ ] **Step 6: Check the page by hand and commit**

Run: `dotnet build PS5Kitsune.slnx -c Release` — 0 warnings. If an AnyPS5 install with a game exists on this machine, start the app (`dotnet run --project src/PS5Kitsune.App -c Release`) and click the game: the Game page shows "Starting…", the font download progress on the first launch, then the log. Otherwise say in the report that it was not checked by hand.

```bash
git add src/PS5Kitsune.App tests/PS5Kitsune.App.Tests
git commit -m "Launch from the Game page, showing the Lavapipe and font downloads

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 8: App — the Settings page

**Files:**
- Create: `src/PS5Kitsune.App/ViewModels/GameSettingsViewModel.cs`, `src/PS5Kitsune.App/Views/GameSettingsView.axaml`, `src/PS5Kitsune.App/Views/GameSettingsView.axaml.cs`
- Modify: `src/PS5Kitsune.App/Views/MainWindow.axaml`, `src/PS5Kitsune.App/Navigation/IPages.cs`, `src/PS5Kitsune.App/Shell/PageFactory.cs`
- Modify: `src/PS5Kitsune.App/ViewModels/LibraryViewModel.cs`, `src/PS5Kitsune.App/Views/LibraryView.axaml` (menu entry)
- Modify: `src/PS5Kitsune.App/ViewModels/GameViewModel.cs`, `src/PS5Kitsune.App/Views/GameView.axaml` (Settings button)
- Modify: `tests/PS5Kitsune.App.Tests/PS5Kitsune.App.Tests.csproj` (link `FakeVulkanProbeRunner.cs`), `tests/PS5Kitsune.App.Tests/Fakes.cs` (`FakePages`)
- Test: `tests/PS5Kitsune.App.Tests/GameSettingsViewModelTests.cs` (new), `LibraryViewModelTests.cs`, `GameViewModelTests.cs`

**Interfaces:**
- Consumes: `GamePlayer.SaveSettingsAsync` (Task 4), `GameLibrary.Find`, `RunningGames.Check`, `IVulkanProbeRunner`, `GameLanguages`, `GameSettings`.
- Produces:
  - `IPages.GameSettings(string titleId)` (FakePages: `$"settings:{titleId}"`)
  - `GameSettingsViewModel(string titleId, GamePlayer player, GameLibrary library, RunningGames running, IVulkanProbeRunner probe, IDialogService dialogs, Navigator navigator)`
  - `GpuChoice(string? Name, string Label)`, `SettingChoice(string? Value, string Label)`
  - `LibraryViewModel.SettingsCommand`, `GameViewModel.OpenSettingsCommand`

- [ ] **Step 1: Write the failing tests**

In `tests/PS5Kitsune.App.Tests/PS5Kitsune.App.Tests.csproj`, link the probe fake next to the others:

```xml
    <Compile Include="..\PS5Kitsune.Core.Tests\Fakes\FakeVulkanProbeRunner.cs" Link="FakeVulkanProbeRunner.cs" />
```

In `tests/PS5Kitsune.App.Tests/Fakes.cs`, add to `FakePages`:

```csharp
    public object GameSettings(string titleId) => $"settings:{titleId}";
```

Create `tests/PS5Kitsune.App.Tests/GameSettingsViewModelTests.cs`:

```csharp
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Checks.Vulkan;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.App.Tests;

public sealed class GameSettingsViewModelTests : IDisposable
{
    private static readonly VulkanGpu Radeon = new("AMD Radeon(TM) Graphics", 0x1002, "integrated", "1.3.280", "AmdProprietary", "AMD proprietary driver", "25.10.1", true, true);
    private static readonly VulkanGpu IrisXe = new("Intel(R) Iris(R) Xe Graphics", 0x8086, "integrated", "1.3.289", "IntelProprietaryWindows", "Intel Corporation", "101.5762", false, true);
    private static readonly VulkanGpu Dozen = new("Microsoft Direct3D12 (Intel(R) Iris(R) Xe Graphics)", 0x8086, "integrated", "1.2.335", "MesaDozen", "Dozen", "26.0", false, false);

    private readonly TestLibrary _library = new();
    private readonly FakeVulkanProbeRunner _probe = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly Navigator _navigator = new();

    private static VulkanProbeOutcome Probe(VulkanProbeStatus status, params VulkanGpu[] gpus) => new(status, new VulkanProbeReport(true, null, gpus), null);

    private async Task<GameSettingsViewModel> OpenAsync(GameSettings? saved = null, string titleId = "PPSA99666")
    {
        if (!File.Exists(_library.Game("PPSA99666").RecordFile))
        {
            await _library.AddAsync("PPSA99666", "DOOM");
        }

        if (saved is not null)
        {
            await _library.Player.SaveSettingsAsync("PPSA99666", saved, TestContext.Current.CancellationToken);
        }

        var page = new GameSettingsViewModel(titleId, _library.Player, _library.Library, _library.Running, _probe, _dialogs, _navigator);
        _navigator.GoTo("library");
        _navigator.GoTo(page);
        await page.Loading;
        return page;
    }

    private GameSettings Saved() => _library.Library.Find("PPSA99666").Record.Settings;

    [Fact]
    public async Task Shows_the_saved_settings_and_the_detected_gpus()
    {
        _probe.Outcome = Probe(VulkanProbeStatus.Succeeded, Radeon, IrisXe);

        var page = await OpenAsync(new GameSettings { Gpu = IrisXe.Name, Language = 2 });

        Assert.Equal("DOOM settings", page.Title);
        Assert.Equal(["Automatic", "AMD Radeon(TM) Graphics", "Intel(R) Iris(R) Xe Graphics (not supported)"], page.Gpus.Select(choice => choice.Label));
        Assert.Equal(IrisXe.Name, page.SelectedGpu?.Name);
        Assert.Equal(2, page.SelectedLanguage.Number);
        Assert.Equal("fifo", page.SelectedPresentMode.Value);
        Assert.Null(page.GpuStatus);
        Assert.True(page.CanEdit);
        Assert.False(page.ShowDisableDozen);
    }

    [Fact]
    public async Task A_saved_gpu_that_is_not_detected_is_kept_and_marked()
    {
        _probe.Outcome = Probe(VulkanProbeStatus.Succeeded, Radeon);

        var page = await OpenAsync(new GameSettings { Gpu = "GeForce" });

        Assert.Equal(["Automatic", "AMD Radeon(TM) Graphics", "GeForce (not found)"], page.Gpus.Select(choice => choice.Label));
        Assert.Equal("GeForce", page.SelectedGpu?.Name);
    }

    [Fact]
    public async Task A_failed_probe_keeps_automatic_and_the_saved_gpu()
    {
        _probe.Outcome = VulkanProbeOutcome.Failed("the driver crashed");

        var page = await OpenAsync(new GameSettings { Gpu = "GeForce" });

        Assert.Equal(["Automatic", "GeForce"], page.Gpus.Select(choice => choice.Label));
        Assert.Equal("Could not detect GPUs: the driver crashed", page.GpuStatus);
        Assert.True(page.CanEdit);
    }

    [Theory]
    [InlineData(VulkanProbeStatus.Succeeded, true, true)]
    [InlineData(VulkanProbeStatus.Succeeded, false, false)]
    [InlineData(VulkanProbeStatus.SucceededWithoutDozen, false, true)]
    public async Task Disable_dozen_shows_when_dozen_is_there_with_another_driver(VulkanProbeStatus status, bool dozenListed, bool shown)
    {
        _probe.Outcome = dozenListed ? Probe(status, Dozen, Radeon) : Probe(status, Radeon);

        var page = await OpenAsync();

        Assert.Equal(shown, page.ShowDisableDozen);
    }

    [Fact]
    public async Task Lavapipe_disables_the_gpu_and_dozen_choices()
    {
        var page = await OpenAsync();
        Assert.True(page.CanChooseGpu);

        page.Lavapipe = true;

        Assert.False(page.CanChooseGpu);
    }

    [Fact]
    public async Task The_overlay_position_is_chosen_only_with_the_overlay()
    {
        var page = await OpenAsync();
        Assert.False(page.CanChooseOverlayPosition);

        page.Overlay = true;

        Assert.True(page.CanChooseOverlayPosition);
    }

    [Fact]
    public async Task A_running_game_is_read_only()
    {
        await _library.AddAsync("PPSA99666", "DOOM");
        await _library.Player.LaunchAsync("PPSA99666", TestContext.Current.CancellationToken);

        var page = await OpenAsync();

        Assert.True(page.IsRunning);
        Assert.False(page.CanEdit);
        Assert.False(page.SaveCommand.CanExecute(null));
        Assert.Equal("Running: changes can't be saved until it stops", page.RunningNotice);
    }

    [Fact]
    public async Task Save_writes_the_settings()
    {
        var page = await OpenAsync();
        page.Overlay = true;
        page.SelectedOverlayPosition = page.OverlayPositions[3];
        page.SelectedPresentMode = page.PresentModes[3];
        page.SelectedPlayGo = page.PlayGoChoices[1];

        await page.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Saved", page.Notice);
        Assert.Null(page.Message);
        Assert.Equal(new GameSettings { Overlay = true, OverlayPosition = "br", PresentMode = "immediate", PlayGoUnlistedChunks = "installed" }, Saved());
    }

    [Fact]
    public async Task Reset_fills_the_defaults_without_saving()
    {
        var page = await OpenAsync(new GameSettings { Language = 2, Lavapipe = true });

        page.ResetCommand.Execute(null);

        Assert.Equal(1, page.SelectedLanguage.Number);
        Assert.False(page.Lavapipe);
        Assert.Equal(2, Saved().Language);
    }

    [Fact]
    public async Task Back_with_unsaved_changes_asks_first()
    {
        var page = await OpenAsync();
        page.Overlay = true;

        await page.BackCommand.ExecuteAsync(null);
        Assert.Equal(["Your changes to the settings of DOOM are not saved."], _dialogs.Confirmations);
        Assert.Same(page, _navigator.CurrentPage);

        _dialogs.Confirm = true;
        await page.BackCommand.ExecuteAsync(null);
        Assert.Equal("library", _navigator.CurrentPage);
    }

    [Fact]
    public async Task Back_without_changes_goes_back_directly()
    {
        var page = await OpenAsync();

        await page.BackCommand.ExecuteAsync(null);

        Assert.Empty(_dialogs.Confirmations);
        Assert.Equal("library", _navigator.CurrentPage);
    }

    [Fact]
    public async Task Saving_a_busy_game_says_so()
    {
        var page = await OpenAsync();
        page.Overlay = true;

        using (GameLock.TryAcquireNow(_library.Game("PPSA99666")))
        {
            await page.SaveCommand.ExecuteAsync(null);
        }

        Assert.Equal("DOOM is busy with another action. Try again.", page.Message);
        Assert.False(Saved().Overlay);
    }

    [Fact]
    public async Task Saving_a_game_started_meanwhile_says_so()
    {
        var page = await OpenAsync();
        page.Overlay = true;
        await _library.Player.LaunchAsync("PPSA99666", TestContext.Current.CancellationToken);

        await page.SaveCommand.ExecuteAsync(null);

        Assert.Equal("DOOM is running. Stop it to change its settings.", page.Message);
        Assert.True(page.IsRunning);
    }

    [Fact]
    public async Task An_unreadable_game_says_why()
    {
        var page = await OpenAsync(titleId: "PPSA00009");

        Assert.StartsWith("Cannot read the settings of PPSA00009: ", page.Message, StringComparison.Ordinal);
        Assert.False(page.CanEdit);
    }

    public void Dispose() => _library.Dispose();
}
```

(If `TestLibrary` is not `IDisposable` under that name, match how `GameViewModelTests` disposes it.)

Add to `LibraryViewModelTests.cs`:

```csharp
    [Fact]
    public async Task Settings_opens_the_game_settings()
    {
        await _library.AddAsync("PPSA00001", "Alpha");
        var page = await CreateAsync();

        page.SettingsCommand.Execute(Tile(page, "Alpha"));

        Assert.Equal("settings:PPSA00001", _navigator.CurrentPage);
    }
```

Add to `GameViewModelTests.cs`:

```csharp
    [Fact]
    public async Task Settings_opens_the_game_settings()
    {
        var (page, _) = await LaunchAsync();

        page.OpenSettingsCommand.Execute(null);

        Assert.Equal("settings:PPSA99666", _navigator.CurrentPage);
    }
```

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build errors — `GameSettingsViewModel`, `IPages.GameSettings`, `SettingsCommand`, `OpenSettingsCommand` do not exist.

- [ ] **Step 2: Write the view model**

Create `src/PS5Kitsune.App/ViewModels/GameSettingsViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Checks.Vulkan;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.App.ViewModels;

/// <param name="Name">The value saved in ANYPS5_GPU; null for Automatic.</param>
public sealed record GpuChoice(string? Name, string Label);

/// <summary>A setting's value with its label; <paramref name="Value"/> is null for "unset".</summary>
public sealed record SettingChoice(string? Value, string Label);

/// <summary>A game's settings, saved to game.json and applied at its next launch. See the 7b spec, "Settings page".</summary>
public sealed partial class GameSettingsViewModel(
    string titleId, GamePlayer player, GameLibrary library, RunningGames running, IVulkanProbeRunner probe, IDialogService dialogs, Navigator navigator)
    : ObservableObject, INavigationAware
{
    private static readonly GpuChoice Automatic = new(null, "Automatic");

    private static readonly SettingChoice[] PresentModeChoices =
        [new("fifo", "On"), new("fifo_latest_ready", "Latest frame"), new("mailbox", "Mailbox"), new("immediate", "Off, may tear")];

    private static readonly SettingChoice[] OverlayPositionChoices =
        [new("tl", "Top left"), new("tr", "Top right"), new("bl", "Bottom left"), new("br", "Bottom right")];

    private static readonly SettingChoice[] PlayGo =
        [new(null, "Default"), new("installed", "Installed (High on Life needs this)"), new("absent", "Absent (Sifu uses this)")];

    private GameSettings _saved = new();
    private string _name = "";
    private bool _loaded;
    private bool _readFailed;
    private IReadOnlyList<VulkanGpu> _detected = [];
    private bool _detectionDone;

    [ObservableProperty]
    public partial string Title { get; private set; } = "Settings";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit), nameof(CanChooseGpu), nameof(CanChooseOverlayPosition))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(ResetCommand))]
    public partial bool IsLoading { get; private set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit), nameof(CanChooseGpu), nameof(CanChooseOverlayPosition))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(ResetCommand))]
    public partial bool IsRunning { get; private set; }

    public bool CanEdit => !IsLoading && !IsRunning && !_readFailed;

    public bool CanChooseGpu => CanEdit && !Lavapipe;

    public bool CanChooseOverlayPosition => CanEdit && Overlay;

    public string RunningNotice { get; } = "Running: changes can't be saved until it stops";

    public string LavapipeHelp { get; } = "Slow but works on any PC. Mesa (~70 MB) is downloaded on the first launch.";

    public string DozenHelp { get; } = "Dozen can crash while GPUs are listed. Lavapipe ignores this.";

    public string OverlayHelp { get; } = "F10 toggles it in game.";

    public string PlayGoHelp { get; } = "Only matters for dumps without PlayGo files.";

    public ObservableCollection<GpuChoice> Gpus { get; } = [Automatic];

    [ObservableProperty]
    public partial GpuChoice? SelectedGpu { get; set; } = Automatic;

    /// <summary>"Detecting GPUs…", why the detection failed, or null.</summary>
    [ObservableProperty]
    public partial string? GpuStatus { get; private set; } = "Detecting GPUs…";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChooseGpu))]
    public partial bool Lavapipe { get; set; }

    public IReadOnlyList<GameLanguage> Languages { get; } = GameLanguages.All;

    [ObservableProperty]
    public partial GameLanguage SelectedLanguage { get; set; } = GameLanguages.All[GameLanguages.Default];

    public IReadOnlyList<SettingChoice> PresentModes { get; } = PresentModeChoices;

    [ObservableProperty]
    public partial SettingChoice SelectedPresentMode { get; set; } = PresentModeChoices[0];

    [ObservableProperty]
    public partial bool ShowDisableDozen { get; private set; }

    [ObservableProperty]
    public partial bool DisableDozen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChooseOverlayPosition))]
    public partial bool Overlay { get; set; }

    public IReadOnlyList<SettingChoice> OverlayPositions { get; } = OverlayPositionChoices;

    [ObservableProperty]
    public partial SettingChoice SelectedOverlayPosition { get; set; } = OverlayPositionChoices[0];

    public IReadOnlyList<SettingChoice> PlayGoChoices { get; } = PlayGo;

    [ObservableProperty]
    public partial SettingChoice SelectedPlayGo { get; set; } = PlayGo[0];

    /// <summary>"Saved"; null otherwise.</summary>
    [ObservableProperty]
    public partial string? Notice { get; private set; }

    /// <summary>Why the page cannot read or save; null otherwise.</summary>
    [ObservableProperty]
    public partial string? Message { get; private set; }

    /// <summary>Completes once the settings are read and the GPUs detected.</summary>
    public Task Loading { get; private set; } = Task.CompletedTask;

    public void OnNavigatedTo()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        Loading = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var (record, isRunning) = await Task.Run(() =>
            {
                var (game, found) = library.Find(titleId);
                return (found, running.Check(game) is not null);
            }).ConfigureAwait(true);
            _name = record.Name;
            Title = $"{record.Name} settings";
            IsRunning = isRunning;
            _saved = record.Settings;
            Apply(record.Settings);
        }
        catch (Exception exception) when (exception is GameNotFoundException or InvalidGameRecordException or IOException or UnauthorizedAccessException)
        {
            _readFailed = true;
            GpuStatus = null;
            Message = $"Cannot read the settings of {titleId}: {exception.Message}";
            IsLoading = false;
            return;
        }

        IsLoading = false;
        var outcome = await Task.Run(() => probe.RunAsync()).ConfigureAwait(true);
        _detectionDone = outcome.Report is not null;
        if (outcome.Report is { } report)
        {
            _detected = report.Gpus;
            GpuStatus = null;
            ShowDisableDozen = outcome.Status == VulkanProbeStatus.SucceededWithoutDozen
                || (report.Gpus.Any(gpu => gpu.IsDozen) && report.Gpus.Any(gpu => !gpu.IsDozen));
        }
        else
        {
            GpuStatus = $"Could not detect GPUs: {outcome.FailureReason}";
        }

        RebuildGpus(SelectedGpu?.Name);
    }

    // Automatic, each detected GPU, then the chosen one when it was not detected. Names compare exactly: that is what is saved.
    private void RebuildGpus(string? selected)
    {
        Gpus.Clear();
        Gpus.Add(Automatic);
        foreach (var gpu in _detected)
        {
            Gpus.Add(new GpuChoice(gpu.Name, gpu.SupportsAnyPS5 ? gpu.Name : $"{gpu.Name} (not supported)"));
        }

        if (selected is not null && !Gpus.Any(choice => choice.Name == selected))
        {
            Gpus.Add(new GpuChoice(selected, _detectionDone ? $"{selected} (not found)" : selected));
        }

        SelectedGpu = Gpus.First(choice => choice.Name == selected);
    }

    private void Apply(GameSettings settings)
    {
        RebuildGpus(settings.Gpu);
        Lavapipe = settings.Lavapipe;
        SelectedLanguage = Languages.First(language => language.Number == settings.Language);
        SelectedPresentMode = PresentModes.First(choice => choice.Value == settings.PresentMode);
        DisableDozen = settings.DisableDozen;
        Overlay = settings.Overlay;
        SelectedOverlayPosition = OverlayPositions.First(choice => choice.Value == settings.OverlayPosition);
        SelectedPlayGo = PlayGoChoices.First(choice => choice.Value == settings.PlayGoUnlistedChunks);
    }

    // A hidden Disable Dozen keeps its saved value.
    private GameSettings Current() => _saved with
    {
        Gpu = SelectedGpu?.Name,
        Lavapipe = Lavapipe,
        Language = SelectedLanguage.Number,
        PresentMode = SelectedPresentMode.Value!,
        DisableDozen = DisableDozen,
        Overlay = Overlay,
        OverlayPosition = SelectedOverlayPosition.Value!,
        PlayGoUnlistedChunks = SelectedPlayGo.Value,
    };

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task SaveAsync()
    {
        Message = null;
        Notice = null;
        var settings = Current();
        try
        {
            var saved = await Task.Run(() => player.SaveSettingsAsync(titleId, settings, CancellationToken.None)).ConfigureAwait(true);
            _saved = saved.Settings;
            Notice = "Saved";
        }
        catch (GameRunningException)
        {
            IsRunning = true;
            Message = $"{_name} is running. Stop it to change its settings.";
        }
        catch (GameBusyException)
        {
            Message = $"{_name} is busy with another action. Try again.";
        }
        catch (Exception exception) when (exception is GameNotFoundException or InvalidGameRecordException or IOException or UnauthorizedAccessException)
        {
            Message = $"Could not save the settings of {_name}: {exception.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Reset()
    {
        Notice = null;
        Apply(new GameSettings());
    }

    [RelayCommand]
    private async Task BackAsync()
    {
        if (CanEdit && Current() != _saved
            && !await dialogs.ConfirmAsync("Discard changes?", $"Your changes to the settings of {_name} are not saved.", "Discard").ConfigureAwait(true))
        {
            return;
        }

        navigator.GoBack();
    }
}
```

(Notes for the implementer: `GameLanguages.All[GameLanguages.Default]` is index 1, English (United States), because the list is ordered by number. `Task.Run(() => probe.RunAsync())` keeps the probe's start off the UI thread. If an analyzer warns that a primary-constructor parameter is captured and also used in an initializer (CS9124), no initializer here uses one — keep it that way.)

- [ ] **Step 3: Write the view**

Create `src/PS5Kitsune.App/Views/GameSettingsView.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:PS5Kitsune.App.ViewModels"
             xmlns:library="using:PS5Kitsune.Core.Library"
             x:Class="PS5Kitsune.App.Views.GameSettingsView"
             x:DataType="vm:GameSettingsViewModel">
  <DockPanel>
    <StackPanel DockPanel.Dock="Top" Spacing="6">
      <TextBlock Text="{Binding Title}" FontSize="20" FontWeight="SemiBold" />
      <TextBlock Text="{Binding RunningNotice}" IsVisible="{Binding IsRunning}" Foreground="#005FB8" TextWrapping="Wrap" />
      <TextBlock Text="{Binding Message}" IsVisible="{Binding Message, Converter={x:Static ObjectConverters.IsNotNull}}"
                 Foreground="#C42B1C" TextWrapping="Wrap" />
      <TextBlock Text="{Binding Notice}" IsVisible="{Binding Notice, Converter={x:Static ObjectConverters.IsNotNull}}"
                 Foreground="#0F7B0F" />
    </StackPanel>

    <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right" Margin="0,12,0,0">
      <Button Content="Back" Command="{Binding BackCommand}" />
      <Button Content="Reset to defaults" Command="{Binding ResetCommand}" />
      <Button Content="Save" Classes="accent" Command="{Binding SaveCommand}" />
    </StackPanel>

    <ScrollViewer Margin="0,12,0,0">
      <StackPanel Spacing="14" MaxWidth="560" HorizontalAlignment="Left">
        <StackPanel Spacing="4">
          <TextBlock Text="GPU" FontWeight="SemiBold" />
          <ComboBox ItemsSource="{Binding Gpus}" SelectedItem="{Binding SelectedGpu}" IsEnabled="{Binding CanChooseGpu}" MinWidth="320">
            <ComboBox.ItemTemplate>
              <DataTemplate x:DataType="vm:GpuChoice">
                <TextBlock Text="{Binding Label}" />
              </DataTemplate>
            </ComboBox.ItemTemplate>
          </ComboBox>
          <TextBlock Text="{Binding GpuStatus}" IsVisible="{Binding GpuStatus, Converter={x:Static ObjectConverters.IsNotNull}}"
                     Opacity="0.85" TextWrapping="Wrap" />
        </StackPanel>

        <StackPanel Spacing="4">
          <CheckBox Content="CPU rendering (Lavapipe)" IsChecked="{Binding Lavapipe}" IsEnabled="{Binding CanEdit}" />
          <TextBlock Text="{Binding LavapipeHelp}" Opacity="0.85" TextWrapping="Wrap" />
        </StackPanel>

        <StackPanel Spacing="4">
          <TextBlock Text="Game language" FontWeight="SemiBold" />
          <ComboBox ItemsSource="{Binding Languages}" SelectedItem="{Binding SelectedLanguage}" IsEnabled="{Binding CanEdit}" MinWidth="320">
            <ComboBox.ItemTemplate>
              <DataTemplate x:DataType="library:GameLanguage">
                <TextBlock Text="{Binding Name}" />
              </DataTemplate>
            </ComboBox.ItemTemplate>
          </ComboBox>
        </StackPanel>

        <StackPanel Spacing="4">
          <TextBlock Text="Vertical sync" FontWeight="SemiBold" />
          <ComboBox ItemsSource="{Binding PresentModes}" SelectedItem="{Binding SelectedPresentMode}" IsEnabled="{Binding CanEdit}" MinWidth="320">
            <ComboBox.ItemTemplate>
              <DataTemplate x:DataType="vm:SettingChoice">
                <TextBlock Text="{Binding Label}" />
              </DataTemplate>
            </ComboBox.ItemTemplate>
          </ComboBox>
        </StackPanel>

        <StackPanel Spacing="4" IsVisible="{Binding ShowDisableDozen}">
          <CheckBox Content="Disable Dozen" IsChecked="{Binding DisableDozen}" IsEnabled="{Binding CanChooseGpu}" />
          <TextBlock Text="{Binding DozenHelp}" Opacity="0.85" TextWrapping="Wrap" />
        </StackPanel>

        <StackPanel Spacing="4">
          <CheckBox Content="Performance overlay" IsChecked="{Binding Overlay}" IsEnabled="{Binding CanEdit}" />
          <TextBlock Text="{Binding OverlayHelp}" Opacity="0.85" />
          <TextBlock Text="Overlay position" />
          <ComboBox ItemsSource="{Binding OverlayPositions}" SelectedItem="{Binding SelectedOverlayPosition}" IsEnabled="{Binding CanChooseOverlayPosition}" MinWidth="200">
            <ComboBox.ItemTemplate>
              <DataTemplate x:DataType="vm:SettingChoice">
                <TextBlock Text="{Binding Label}" />
              </DataTemplate>
            </ComboBox.ItemTemplate>
          </ComboBox>
        </StackPanel>

        <Expander Header="Advanced">
          <StackPanel Spacing="4">
            <TextBlock Text="PlayGo unlisted chunks" FontWeight="SemiBold" />
            <ComboBox ItemsSource="{Binding PlayGoChoices}" SelectedItem="{Binding SelectedPlayGo}" IsEnabled="{Binding CanEdit}" MinWidth="320">
              <ComboBox.ItemTemplate>
                <DataTemplate x:DataType="vm:SettingChoice">
                  <TextBlock Text="{Binding Label}" />
                </DataTemplate>
              </ComboBox.ItemTemplate>
            </ComboBox>
            <TextBlock Text="{Binding PlayGoHelp}" Opacity="0.85" TextWrapping="Wrap" />
          </StackPanel>
        </Expander>
      </StackPanel>
    </ScrollViewer>
  </DockPanel>
</UserControl>
```

Create `src/PS5Kitsune.App/Views/GameSettingsView.axaml.cs` like the other simple views (for example `DoneView.axaml.cs`):

```csharp
using Avalonia.Controls;

namespace PS5Kitsune.App.Views;

public sealed partial class GameSettingsView : UserControl
{
    public GameSettingsView() => InitializeComponent();
}
```

In `src/PS5Kitsune.App/Views/MainWindow.axaml`, add with the other templates:

```xml
    <DataTemplate DataType="vm:GameSettingsViewModel">
      <views:GameSettingsView />
    </DataTemplate>
```

- [ ] **Step 4: Open the page from the Library and the Game page**

In `src/PS5Kitsune.App/Navigation/IPages.cs`:

```csharp
    /// <summary>A game's settings; Back returns to the page that opened it.</summary>
    object GameSettings(string titleId);
```

In `src/PS5Kitsune.App/Shell/PageFactory.cs` (add `using PS5Kitsune.Core.Checks.Vulkan;`):

```csharp
    public object GameSettings(string titleId) =>
        new GameSettingsViewModel(
            titleId, Session.Get<GamePlayer>(), Session.Get<GameLibrary>(), Session.Get<RunningGames>(), Session.Get<IVulkanProbeRunner>(), dialogs, navigator);
```

In `src/PS5Kitsune.App/ViewModels/LibraryViewModel.cs`, after `OpenFolder`:

```csharp
    [RelayCommand]
    private void Settings(GameTileViewModel tile) => navigator.GoTo(pages.GameSettings(tile.TitleId));
```

In `src/PS5Kitsune.App/Views/LibraryView.axaml`, after the "Launch" menu item:

```xml
                        <MenuItem Header="Settings" Command="{Binding Owner.SettingsCommand}" CommandParameter="{Binding}" />
```

In `src/PS5Kitsune.App/ViewModels/GameViewModel.cs`, after `OpenFolder`:

```csharp
    [RelayCommand]
    private void OpenSettings() => _navigator.GoTo(_pages.GameSettings(_game.TitleId));
```

In `src/PS5Kitsune.App/Views/GameView.axaml`, after the "Open folder" button:

```xml
      <Button Content="Settings" Command="{Binding OpenSettingsCommand}" />
```

- [ ] **Step 5: Run the tests**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: 0 failed.

- [ ] **Step 6: Check the page by hand and commit**

Run: `dotnet build PS5Kitsune.slnx -c Release` — 0 warnings. If a game is added on this machine, open the app, open the tile's "…" menu → Settings: the GPU list fills after "Detecting GPUs…", Lavapipe greys out the GPU, Save shows "Saved", Back with a change asks first. Otherwise say in the report that it was not checked by hand.

```bash
git add src/PS5Kitsune.App tests/PS5Kitsune.App.Tests
git commit -m "Add the game Settings page

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 9: Docs

**Files:**
- Modify: `docs/architecture.md`, `README.md`

- [ ] **Step 1: Update the architecture doc**

In `docs/architecture.md`:
- **Archives row** of the stack table: "Mesa for Windows ships as `.7z` (to be confirmed at the Lavapipe milestone)" becomes "Mesa for Windows ships as `.7z`: only Lavapipe's manifest and driver are extracted, with SharpCompress's sequential reader".
- **Portable tools table**, Mesa row: source `pal1000/mesa-dist-win` release 26.2.4, `mesa3d-26.2.4-release-msvc.7z`; notes "Downloaded on the first launch with CPU rendering on. Only `x64/lvp_icd.x86_64.json` and `x64/vulkan_lvp.dll` (56 MB) are kept, in `tools\mesa`".
- **Library section**: "Settings comes with 7b." becomes the menu with "Settings" after "Launch", and a sentence on the Game page doing the launch: downloads (Lavapipe, fonts) with progress and Cancel, then the log.
- **Launching section**: add that the game gets its settings as environment variables on top of the launcher's environment, is started with `CREATE_BREAKAWAY_FROM_JOB` (without it when the parent job forbids breakaway), and that exit is detected through the process handle (an exit code of 259 is a real exit code). Add that `lastPlayedAt` is written once the process has started.
- **Per-game settings table**: add the JSON field name to each row; Vertical sync lists the app labels (On, Latest frame, Mailbox, Off) with their values; Disable Dozen says "shown when Dozen and another driver are found, or the probe only worked without Dozen"; Performance overlay mentions the position (`tl` default, `tr`, `bl`, `br`); the bindings row says "Later milestone: until then, Open folder and edit `anyps5-input.ini`". Add the rule: "A value equal to AnyPS5's default is not passed. A value AnyPS5 would refuse (hand-edited `game.json`) is read as its default and logged; the file is not rewritten." Add the Lavapipe default: "on for a new game when Vulkan works but no GPU has `shaderInt64` and `bufferDeviceAddress`".
- **Fonts paragraph**: the list (Noto Sans regular and italic weights, Mono, Thai, Arabic, CJK `.ttc`; 24 files, 87 MB) pinned in the profile by commit and SHA-256, downloaded on the first launch into `<root>\fonts` with a `.complete` marker; a failed download warns and the game still starts.
- **`game.json` example**: `"settings": { "gpu": null, "lavapipe": false, "language": 1, "presentMode": "fifo", "disableDozen": false, "overlay": false, "overlayPosition": "tl", "playGoUnlistedChunks": null }`.
- **Data layout**: `tools\mesa\` says "Lavapipe only (manifest and driver), downloaded on first use"; `fonts\` says "Noto fonts, `.complete` marker, passed via ANYPS5_SYSTEM_FONTS".
- **"Still to confirm"** row: remove "Mesa 7z format (at the Lavapipe milestone)".
- **Roadmap**: mark 7b as the current one in the same way 7a was marked when it was done (keep the style of the surrounding entries); its criterion stays "DOOM launched in one click with Lavapipe on the Iris Xe".

- [ ] **Step 2: Update the README**

In `README.md`, add `ps5kitsune settings <titleId>` to the command list next to `list`, described as "Show a game's settings and the environment variables they become at launch (edit them in the app)". If the README lists what `tools list` shows, add `mesa`.

- [ ] **Step 3: Check and commit**

Run: `git diff --stat` — only the two docs. Read the changed sections once for consistency with the code (names of fields, variables, labels).

```bash
git add docs/architecture.md README.md
git commit -m "Document the 7b settings, Lavapipe and fonts

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

## Criterion (run by the user, after Task 9)

By hand:

1. On the Intel Iris Xe laptop, open DOOM's Settings (Library → "…" → Settings), tick CPU rendering (Lavapipe) and Save.
2. One click on DOOM: the Game page shows "Downloading Lavapipe…" then "Downloading fonts…" with progress, then DOOM starts; the log names llvmpipe as the device.
3. Stop it and Launch again: nothing is downloaded.
4. `ps5kitsune settings <DOOM's title ID>` shows `VK_DRIVER_FILES` and `ANYPS5_SYSTEM_FONTS` without "(downloaded on first launch)".
5. On the AMD laptop, DOOM with default settings still runs on its GPU.
