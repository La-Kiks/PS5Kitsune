# Milestone 6b — Reconvert Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reconvert an added game after an AnyPS5 update (new libs, executable and audit) without copying `app0` again, without touching what the game created, and leaving the game exactly as it was when the Reconvert fails or is cancelled.

**Architecture:** Core gains `ConversionBackup` (set the conversion outputs aside into `.ps5kitsune\previous`, restore them, discard them), two steps (`SetAsideStep`, `UpdateGameRecordStep`) and `GameReconverter` behind `IGameReconverter`, which runs set-aside → the add's libs/unwrap/relink/audit steps → record, and restores on failure or cancel. `GameLibrary` lists the games, restores an interrupted Reconvert when it reads a game, and tells which games need reconversion. The CLI gets `reconvert` and a warning in `run`; the app gets a Reconverting page (a `StepRunViewModel`), reuses the Result page, and offers Reconvert from Home's list and from the Summary page of an already added game.

**Tech Stack:** .NET 10, C#, Avalonia 12 (compiled bindings), CommunityToolkit.Mvvm, xUnit v3 on Microsoft.Testing.Platform.

**Spec:** `docs/superpowers/specs/2026-10-23-milestone-6b-reconvert-design.md` (revision 1e57504).

## Global Constraints

- Code, docs, commit messages and UI text in English.
- `TreatWarningsAsErrors`, `AnalysisLevel latest-recommended`, `EnforceCodeStyleInBuild`: 0 warnings. `[LoggerMessage]` for logging (CA1848); InvariantCulture when formatting numbers (CA1305); constant texts on view models are get-only properties with an initializer (CA1822); compute log arguments into locals before the call (CA1873).
- No new NuGet dependency.
- Reconvert never touches: `data\`, `download0\`, `_trophy\`, `anyps5-input.ini`, `anyps5-fonts\`, `shader_cache\`, every file of `app0` other than `*.guest.prx`, `.ps5kitsune\originals\`, `.ps5kitsune\logs\`.
- Conversion outputs (set aside, restored, discarded as a whole): the executable, the relinked marker (`.ps5kitsune\relinked`), `audit.json`, `*.registry.json` and `*.needed.json` in the game folder, `libs\`, `app0\**\*.guest.prx`.
- Folders: `.ps5kitsune\previous.partial\`, `.ps5kitsune\previous\`, `.ps5kitsune\previous.discarded\`.
- Reconvert step names, in order: "Set the old conversion aside", "Copy the AnyPS5 libraries", "Unwrap SELF files", "Convert with the relinker", "Audit the imports", "Record the game".
- A conversion's label is `{mode} {version}`, e.g. `build 3fd1cc3`, `release v0.1.2`.
- Core texts, verbatim: set-aside failure summary "Cannot set the old conversion aside: {reason}", details "Close the game if it is running.".
- CLI texts, verbatim:
  - "{Name} ({TitleId}) is reconverted in {Directory}."
  - "Before: {Implemented} implemented, {Stub} stubs, {Absent} absent."
  - "Already converted with {Conversion}; reconverting anyway."
  - "The previous conversion was restored."
  - "The previous conversion could not be restored yet; it will be restored on the next start or reconvert."
  - `run`: "Converted with {ConvertedWith}; the AnyPS5 in use is {InUse}. Run ps5kitsune reconvert {TitleId}."
  - Exit codes: 0 success, 1 failure, 130 cancelled.
- App texts, verbatim:
  - Home: section title "Games to reconvert", row detail "Converted with {label} · in use: {label}", button "Reconvert".
  - Summary: "Converted with the AnyPS5 in use.", "Converted with {label}; the AnyPS5 in use is {label}.", "Converted with {label}." (no install in use), button "Reconvert".
  - Reconverting: title "Reconverting {name}"; quit message "A Reconvert is in progress. Quitting cancels it and puts the previous conversion back."; failure "{Step} failed: {Summary}", "AnyPS5 is not installed. Install it from Home first.", other "Reconverting the game failed: {message}", each followed by a new line and "The previous conversion was kept; the game runs as before." or, when the restore failed, "The previous conversion could not be restored yet; it will be restored on the next start or Reconvert."
  - Result: title "{name} reconverted", line "Before: {implemented} implemented, {stub} stubs, {absent} absent".
  - Any step-run page whose success or cancel handling throws: "Something went wrong after the steps: {message}".
- Colours: red `#C42B1C`, orange `#9D5D00`, green `#0F7B0F`.
- Tests never use the network, the compiler or Python.
- Build: `dotnet build PS5Kitsune.slnx -c Release`. Tests: `dotnet test --solution PS5Kitsune.slnx -c Release`; one class: `-- --filter-class "<full class name>"` (a filtered run exits with code 8: not a failure). Run commands through the Bash tool.
- Commit locally after each task, author La-Kiks, message ending with `Co-Authored-By: <your model name> <noreply@anthropic.com>`. Do not push. Never add or run an `install.cmd`.

## Refinements of the spec

1. **The executable lock is checked first.** Windows lets a running executable be renamed, so moving it would not fail. `SetAsideStep` first opens the executable with `FileShare.None`; a running game makes that fail with the "Close the game" details before anything moves.
2. **Discarding is a rename, then a delete.** "Record the game" renames `previous` to `previous.discarded` before deleting it, so a delete that fails halfway can never be restored over the new conversion. A leftover `previous.discarded` is deleted (best effort) by the next restore.
3. **Step names live as constants on the step classes** (`public const string StepName`), and `GameImporter.Names` and `GameReconverter.Names` reference them. That is the 6a follow-up "step names written in two places" (Core's list and each step's `Name`); the app already reads `IGameImporter.StepNames`.
4. **`GameLibrary.All()` skips a game whose restore fails**, with the same Warning as a malformed `game.json`.
5. **There is no CLI test project.** The CLI's texts live in `CliLog`; its logic is covered by Core's tests and by the criterion, as for `add` and `run`. The verdict lines move into a shared `VerdictReport.Print` used by `add` and `reconvert`.
6. **`StepRunViewModel` takes an optional `ILogger`** (last constructor parameter of the base and of each page) to log a failing success or cancel hook.
7. **Labels:** `ConversionLabels.Of(ConversionInfo)` and `ConversionLabels.Of(AnyPS5InstallRecord)` in `Core/Library`.
8. **The Reconverting page has its own view**, a copy of `AddGameView` (a shared step-run view stays a 7b follow-up). The "kept" sentence is the second line of `Failure`.

## File map

| File | Change |
| --- | --- |
| `src/PS5Kitsune.Core/Games/GameLayout.cs` | `Previous`, `PreviousPartial`, `PreviousDiscarded` |
| `src/PS5Kitsune.Core/Games/ConversionBackup.cs` | new |
| `src/PS5Kitsune.Core/Games/Steps/SetAsideStep.cs`, `UpdateGameRecordStep.cs` | new |
| `src/PS5Kitsune.Core/Games/Steps/*Step.cs` (existing six) | `StepName` constants |
| `src/PS5Kitsune.Core/Games/GameImporter.cs`, `IGameImporter.cs` | names from constants, `IsDisposed` guard |
| `src/PS5Kitsune.Core/Library/GameRecord.cs` | `ReconvertedAt` |
| `src/PS5Kitsune.Core/Library/GameLibrary.cs` | logger, `All`, `NeedsReconversion`, restore in `Find` |
| `src/PS5Kitsune.Core/Library/ConversionLabels.cs` | new |
| `src/PS5Kitsune.Core/Games/GameReconverter.cs`, `IGameReconverter.cs`, `GameReconverted.cs`, `ReconvertRestoreException.cs` | new |
| `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs` | `GameReconverter`, `IGameReconverter` |
| `src/PS5Kitsune.Cli/ReconvertCommand.cs`, `VerdictReport.cs` | new |
| `src/PS5Kitsune.Cli/AddCommand.cs`, `RunCommand.cs`, `CliLog.cs`, `Program.cs` | shared verdict, warning, texts, command |
| `src/PS5Kitsune.App/ViewModels/StepRunViewModel.cs`, `InstallViewModel.cs`, `AddGameViewModel.cs` | optional logger, hook failures |
| `src/PS5Kitsune.App/ViewModels/ReconvertViewModel.cs`, `Views/ReconvertView.axaml(.cs)` | new |
| `src/PS5Kitsune.App/ViewModels/AddResultViewModel.cs`, `Views/AddResultView.axaml` | reconverted result, "Before" line |
| `src/PS5Kitsune.App/Navigation/IPages.cs`, `Shell/PageFactory.cs`, `Views/MainWindow.axaml`, `Shell/AppLog.cs` | pages, template, log |
| `src/PS5Kitsune.App/ViewModels/HomeViewModel.cs`, `GameToReconvert.cs`, `Views/HomeView.axaml` | games to reconvert |
| `src/PS5Kitsune.App/ViewModels/GameSummaryViewModel.cs`, `Views/GameSummaryView.axaml` | Reconvert |
| `tests/PS5Kitsune.Core.Tests/Games/ConvertedGames.cs` | new helper |
| `tests/PS5Kitsune.App.Tests/Fakes.cs` | `FakeGameReconverter`, `TestGameData.Reconverted`, `FakePages`, `TestSessions` |
| `docs/architecture.md`, `README.md` | Task 7 |

---

### Task 1: Core — set the conversion aside and restore it

**Files:**
- Modify: `src/PS5Kitsune.Core/Games/GameLayout.cs`
- Create: `src/PS5Kitsune.Core/Games/ConversionBackup.cs`, `src/PS5Kitsune.Core/Games/Steps/SetAsideStep.cs`
- Modify: `src/PS5Kitsune.Core/Games/Steps/CopyGameStep.cs`, `CopyLibsStep.cs`, `UnwrapSelfStep.cs`, `RelinkStep.cs`, `AuditStep.cs`, `WriteGameRecordStep.cs`, `src/PS5Kitsune.Core/Games/GameImporter.cs`
- Create: `tests/PS5Kitsune.Core.Tests/Games/ConvertedGames.cs`, `tests/PS5Kitsune.Core.Tests/Games/ConversionBackupTests.cs`

**Interfaces:**
- Produces: `GameLayout.Previous`, `GameLayout.PreviousPartial`, `GameLayout.PreviousDiscarded` (strings); `static class ConversionBackup { IReadOnlyList<string> Outputs(GameLayout); void SetAside(GameLayout); bool Restore(GameLayout); void Discard(GameLayout); }`; `SetAsideStep(GameLayout game) : IStep` with `const string StepName`; `const string StepName` on `CopyGameStep`, `CopyLibsStep`, `UnwrapSelfStep`, `RelinkStep`, `AuditStep`, `WriteGameRecordStep`; test helper `ConvertedGames.Create(GameLayout)`, `ConvertedGames.Snapshot(string root)`, `ConvertedGames.IsConversionOutput(string relative)`.

- [ ] **Step 1: Write the test helper**

`tests/PS5Kitsune.Core.Tests/Games/ConvertedGames.cs`:

```csharp
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Core.Tests.Games;

/// <summary>A game folder as an add leaves it, after the game ran once: conversion outputs, and the files Reconvert must never touch.</summary>
internal static class ConvertedGames
{
    /// <summary>Files the game or the user created, relative to the game folder.</summary>
    public static readonly string[] Untouched =
    [
        "data/save.dat", "download0/dlc.bin", "_trophy/trophy.dat", "anyps5-input.ini", "anyps5-fonts/font.ttf", "shader_cache/v1/cache.bin",
        "app0/eboot.bin", "app0/sce_module/libc.prx", "app0/sce_sys/param.json", ".ps5kitsune/originals/eboot.bin", ".ps5kitsune/logs/2026-10-23.log",
    ];

    public static void Create(GameLayout game)
    {
        foreach (var relative in Untouched)
        {
            TestFiles.Write(Path.Combine(game.Root, relative), relative == "app0/eboot.bin" || relative == "app0/sce_module/libc.prx" ? TestFiles.Elf() : System.Text.Encoding.UTF8.GetBytes("kept " + relative));
        }

        TestFiles.Write(game.Executable, "old exe");
        TestFiles.Write(game.Registry, "old registry");
        TestFiles.Write(Path.Combine(game.Root, "PPSA99666.libc.registry.json"), "old module registry");
        TestFiles.Write(Path.Combine(game.Root, "PPSA99666.needed.json"), "old needed");
        TestFiles.Write(game.AuditFile, AuditFiles.Json(360, 4, 0));
        TestFiles.Write(game.RelinkedMarker, "");
        TestFiles.Write(Path.Combine(game.Libs, "libSceAudioOut.prx"), "old prx");
        TestFiles.Write(Path.Combine(game.Libs, "libOnlyInOldVersion.prx"), "old only");
        TestFiles.Write(Path.Combine(game.App0, "sce_module", "libc.guest.prx"), "old pe");
    }

    /// <summary>Every file under <paramref name="root"/> with its content in hex, by '/' relative path.</summary>
    public static SortedDictionary<string, string> Snapshot(string root) => new(
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(file => Path.GetRelativePath(root, file).Replace('\\', '/'), file => Convert.ToHexString(File.ReadAllBytes(file))),
        StringComparer.Ordinal);

    /// <summary>True for a conversion output, or a file Reconvert rewrites (game.json, the audit's module copies).</summary>
    public static bool IsConversionOutput(string relative) =>
        relative.EndsWith(".exe", StringComparison.Ordinal)
        || relative.EndsWith(".registry.json", StringComparison.Ordinal)
        || relative.EndsWith(".needed.json", StringComparison.Ordinal)
        || relative.EndsWith(".guest.prx", StringComparison.Ordinal)
        || relative is "audit.json" or ".ps5kitsune/relinked" or ".ps5kitsune/game.json"
        || relative.StartsWith("libs/", StringComparison.Ordinal)
        || relative.StartsWith(".ps5kitsune/audit-modules/", StringComparison.Ordinal);
}
```

- [ ] **Step 2: Write the failing tests**

`tests/PS5Kitsune.Core.Tests/Games/ConversionBackupTests.cs`:

```csharp
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Games.Steps;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class ConversionBackupTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly GameLayout _game;

    public ConversionBackupTests()
    {
        _game = new GameLayout(_temp.Combine("games", TestGames.TitleId), TestGames.TitleId, new FakePlatform());
        ConvertedGames.Create(_game);
    }

    private void WriteNewOutputs()
    {
        TestFiles.Write(_game.Executable, "new exe");
        TestFiles.Write(_game.Registry, "new registry");
        TestFiles.Write(Path.Combine(_game.Libs, "libSceAudioOut.prx"), "new prx");
        TestFiles.Write(Path.Combine(_game.Libs, "libOnlyInNewVersion.prx"), "new only");
        TestFiles.Write(Path.Combine(_game.App0, "sce_module", "libc.guest.prx"), "new pe");
    }

    [Fact]
    public void Lists_every_conversion_output()
    {
        var outputs = ConversionBackup.Outputs(_game).Select(path => path.Replace('\\', '/')).ToList();

        Assert.Equal(
            ["PPSA99666.exe", ".ps5kitsune/relinked", "audit.json", "PPSA99666.libc.registry.json", "PPSA99666.registry.json", "PPSA99666.needed.json", "libs", "app0/sce_module/libc.guest.prx"],
            outputs);
    }

    [Fact]
    public void Setting_aside_moves_the_outputs_and_nothing_else()
    {
        var before = ConvertedGames.Snapshot(_game.Root);

        ConversionBackup.SetAside(_game);

        Assert.True(Directory.Exists(_game.Previous));
        Assert.False(Directory.Exists(_game.PreviousPartial));
        Assert.Empty(ConversionBackup.Outputs(_game));
        var after = ConvertedGames.Snapshot(_game.Root);
        foreach (var relative in ConvertedGames.Untouched)
        {
            Assert.Equal(before[relative], after[relative]);
        }

        Assert.Equal(before["PPSA99666.exe"], after[".ps5kitsune/previous/PPSA99666.exe"]);
        Assert.Equal(before["libs/libOnlyInOldVersion.prx"], after[".ps5kitsune/previous/libs/libOnlyInOldVersion.prx"]);
        Assert.Equal(before["app0/sce_module/libc.guest.prx"], after[".ps5kitsune/previous/app0/sce_module/libc.guest.prx"]);
    }

    [Fact]
    public void Restoring_after_new_outputs_puts_the_old_conversion_back_exactly()
    {
        var before = ConvertedGames.Snapshot(_game.Root);
        ConversionBackup.SetAside(_game);
        WriteNewOutputs();

        Assert.True(ConversionBackup.Restore(_game));

        Assert.Equal(before, ConvertedGames.Snapshot(_game.Root));
        Assert.False(Directory.Exists(_game.Previous));
    }

    [Fact]
    public void An_interrupted_set_aside_is_moved_back()
    {
        var before = ConvertedGames.Snapshot(_game.Root);
        // As if the process died after moving two outputs: they sit in previous.partial, the rest is in place.
        TestFiles.Write(Path.Combine(_game.PreviousPartial, "PPSA99666.exe"), "old exe");
        File.Delete(_game.Executable);
        Directory.Move(_game.Libs, Path.Combine(_game.PreviousPartial, "libs"));

        Assert.True(ConversionBackup.Restore(_game));

        Assert.Equal(before, ConvertedGames.Snapshot(_game.Root));
        Assert.False(Directory.Exists(_game.PreviousPartial));
    }

    [Fact]
    public void Nothing_to_restore_changes_nothing()
    {
        var before = ConvertedGames.Snapshot(_game.Root);

        Assert.False(ConversionBackup.Restore(_game));

        Assert.Equal(before, ConvertedGames.Snapshot(_game.Root));
    }

    [Fact]
    public void Discarding_deletes_the_set_aside_conversion()
    {
        ConversionBackup.SetAside(_game);
        WriteNewOutputs();
        var withNew = ConvertedGames.Snapshot(_game.Root).Where(entry => !entry.Key.StartsWith(".ps5kitsune/previous/", StringComparison.Ordinal)).ToList();

        ConversionBackup.Discard(_game);

        Assert.False(Directory.Exists(_game.Previous));
        Assert.False(Directory.Exists(_game.PreviousDiscarded));
        Assert.Equal(withNew, ConvertedGames.Snapshot(_game.Root).ToList());
        Assert.False(ConversionBackup.Restore(_game));
    }

    [Fact]
    public void A_leftover_discarded_folder_is_deleted_by_the_next_restore()
    {
        TestFiles.Write(Path.Combine(_game.PreviousDiscarded, "PPSA99666.exe"), "older exe");

        ConversionBackup.Restore(_game);

        Assert.False(Directory.Exists(_game.PreviousDiscarded));
        Assert.Equal("old exe", File.ReadAllText(_game.Executable));
    }

    [Fact]
    public async Task The_step_sets_the_outputs_aside_and_is_never_done()
    {
        var step = new SetAsideStep(_game);
        Assert.Equal("Set the old conversion aside", step.Name);
        Assert.False(step.IsAlreadyDone());

        await step.ExecuteAsync(null, TestContext.Current.CancellationToken);

        Assert.True(Directory.Exists(_game.Previous));
        Assert.False(step.IsAlreadyDone());
    }

    [Fact]
    public async Task A_running_game_stops_the_step_before_anything_moves()
    {
        var before = ConvertedGames.Snapshot(_game.Root);
        var step = new SetAsideStep(_game);

        GameImportException failure;
        using (new FileStream(_game.Executable, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            failure = await Assert.ThrowsAsync<GameImportException>(() => step.ExecuteAsync(null, TestContext.Current.CancellationToken));
        }

        Assert.StartsWith("Cannot set the old conversion aside: ", failure.Summary, StringComparison.Ordinal);
        Assert.Equal("Close the game if it is running.", failure.Details);
        Assert.Equal(before, ConvertedGames.Snapshot(_game.Root));
        Assert.False(Directory.Exists(_game.PreviousPartial));
    }

    public void Dispose() => _temp.Dispose();
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build errors: `ConversionBackup`, `SetAsideStep`, `GameLayout.Previous` do not exist.

- [ ] **Step 4: Add the folders to `GameLayout`**

In `src/PS5Kitsune.Core/Games/GameLayout.cs`, after `RecordFile`:

```csharp
    /// <summary>The conversion outputs set aside by a Reconvert that has not finished.</summary>
    public string Previous => Path.Combine(Meta, "previous");

    /// <summary>Outputs being set aside; renamed to <see cref="Previous"/> once all are moved.</summary>
    public string PreviousPartial => Path.Combine(Meta, "previous.partial");

    /// <summary>A set-aside conversion being deleted after a successful Reconvert; never restored.</summary>
    public string PreviousDiscarded => Path.Combine(Meta, "previous.discarded");
```

- [ ] **Step 5: Write `ConversionBackup`**

`src/PS5Kitsune.Core/Games/ConversionBackup.cs`:

```csharp
namespace PS5Kitsune.Core.Games;

/// <summary>
/// Sets a game's conversion outputs aside during a Reconvert and moves them back when it does not finish. One rule per folder:
/// .ps5kitsune/previous.partial means the move was interrupted (nothing new was built); .ps5kitsune/previous means the new
/// conversion did not finish. See docs/superpowers/specs/2026-10-23-milestone-6b-reconvert-design.md, "Recovery".
/// </summary>
public static class ConversionBackup
{
    /// <summary>The conversion outputs now in the game folder, relative to it: files, and libs as one folder.</summary>
    public static IReadOnlyList<string> Outputs(GameLayout game)
    {
        ArgumentNullException.ThrowIfNull(game);
        var outputs = new List<string>();
        foreach (var file in new[] { game.Executable, game.RelinkedMarker, game.AuditFile })
        {
            if (File.Exists(file))
            {
                outputs.Add(Path.GetRelativePath(game.Root, file));
            }
        }

        if (Directory.Exists(game.Root))
        {
            outputs.AddRange(Directory.EnumerateFiles(game.Root, "*.registry.json").Order(StringComparer.Ordinal).Select(file => Path.GetFileName(file)));
            outputs.AddRange(Directory.EnumerateFiles(game.Root, "*.needed.json").Order(StringComparer.Ordinal).Select(file => Path.GetFileName(file)));
        }

        if (Directory.Exists(game.Libs))
        {
            outputs.Add(Path.GetRelativePath(game.Root, game.Libs));
        }

        if (Directory.Exists(game.App0))
        {
            outputs.AddRange(Directory.EnumerateFiles(game.App0, "*.guest.prx", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(game.Root, file))
                .Order(StringComparer.Ordinal));
        }

        return outputs;
    }

    /// <summary>Moves the outputs into previous.partial, then renames it to previous in one rename.</summary>
    public static void SetAside(GameLayout game)
    {
        ArgumentNullException.ThrowIfNull(game);
        Directory.CreateDirectory(game.PreviousPartial);
        foreach (var relative in Outputs(game))
        {
            Move(Path.Combine(game.Root, relative), Path.Combine(game.PreviousPartial, relative));
        }

        Directory.Move(game.PreviousPartial, game.Previous);
    }

    /// <summary>Puts an unfinished Reconvert's set-aside conversion back.</summary>
    /// <returns>False when there was nothing to restore.</returns>
    public static bool Restore(GameLayout game)
    {
        ArgumentNullException.ThrowIfNull(game);
        TryDelete(game.PreviousDiscarded);
        if (Directory.Exists(game.Previous))
        {
            foreach (var relative in Outputs(game))
            {
                Delete(Path.Combine(game.Root, relative));
            }

            MoveBack(game.Previous, game);
            return true;
        }

        if (Directory.Exists(game.PreviousPartial))
        {
            MoveBack(game.PreviousPartial, game);
            return true;
        }

        return false;
    }

    /// <summary>Drops the set-aside conversion once the new one is complete: a rename first, so it can never be restored.</summary>
    public static void Discard(GameLayout game)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (Directory.Exists(game.Previous))
        {
            Directory.Move(game.Previous, game.PreviousDiscarded);
        }

        TryDelete(game.PreviousDiscarded);
    }

    private static void MoveBack(string folder, GameLayout game)
    {
        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).ToList())
        {
            Move(file, Path.Combine(game.Root, Path.GetRelativePath(folder, file)));
        }

        Directory.Delete(folder, recursive: true);
    }

    private static void Move(string source, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (Directory.Exists(source))
        {
            Directory.Move(source, target);
        }
        else
        {
            File.Move(source, target, overwrite: true);
        }
    }

    private static void Delete(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
        else
        {
            File.Delete(path);
        }
    }

    // A leftover discarded folder only takes space: a failed delete is retried on the next restore.
    private static void TryDelete(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
```

Note: `MoveBack` moves `libs\` back file by file (its target folder may already have been deleted by `Restore`); `Move` creates the parent folders.

- [ ] **Step 6: Write `SetAsideStep`**

`src/PS5Kitsune.Core/Games/Steps/SetAsideStep.cs`:

```csharp
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.Games.Steps;

/// <summary>
/// Reconvert's first step: moves the conversion outputs into .ps5kitsune/previous, so a failure can put them back.
/// Windows lets a running executable be renamed, so the executable is first opened exclusively: a running game stops the step.
/// </summary>
public sealed class SetAsideStep(GameLayout game) : IStep
{
    public const string StepName = "Set the old conversion aside";

    public string Name => StepName;

    public bool IsAlreadyDone() => false;

    public Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        try
        {
            if (File.Exists(game.Executable))
            {
                using (new FileStream(game.Executable, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                }
            }

            ConversionBackup.SetAside(game);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new GameImportException($"Cannot set the old conversion aside: {exception.Message}", "Close the game if it is running.");
        }

        return Task.CompletedTask;
    }
}
```

- [ ] **Step 7: Step name constants**

In each existing step, replace the `Name` property with a constant and a property that returns it. For example in `CopyLibsStep.cs`:

```csharp
    public const string StepName = "Copy the AnyPS5 libraries";

    public string Name => StepName;
```

Do the same with these exact values: `CopyGameStep` "Copy the game", `UnwrapSelfStep` "Unwrap SELF files", `RelinkStep` "Convert with the relinker", `AuditStep` "Audit the imports", `WriteGameRecordStep` "Record the game".

In `src/PS5Kitsune.Core/Games/GameImporter.cs`, replace the `Names` initializer:

```csharp
    public static readonly IReadOnlyList<string> Names =
        [CopyGameStep.StepName, CopyLibsStep.StepName, UnwrapSelfStep.StepName, RelinkStep.StepName, AuditStep.StepName, WriteGameRecordStep.StepName];
```

- [ ] **Step 8: Run the tests**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.Core.Tests.Games.ConversionBackupTests"`
Expected: all pass (exit code 8 for a filtered run). Then the whole suite: `dotnet test --solution PS5Kitsune.slnx -c Release`; expected: 0 failed (the existing `PrepareGameTests` step-name test still passes).

- [ ] **Step 9: Commit**

```bash
git add src/PS5Kitsune.Core tests/PS5Kitsune.Core.Tests
git commit -m "Set a game's conversion aside and restore it

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 2: Core — the game record and the library

**Files:**
- Modify: `src/PS5Kitsune.Core/Library/GameRecord.cs`, `src/PS5Kitsune.Core/Library/GameLibrary.cs`
- Create: `src/PS5Kitsune.Core/Library/ConversionLabels.cs`, `src/PS5Kitsune.Core/Games/Steps/UpdateGameRecordStep.cs`
- Modify: `tests/PS5Kitsune.Core.Tests/Library/GameLibraryTests.cs`, `tests/PS5Kitsune.Core.Tests/Library/GameRecordStoreTests.cs`
- Create: `tests/PS5Kitsune.Core.Tests/Games/UpdateGameRecordStepTests.cs`

**Interfaces:**
- Consumes: `ConversionBackup.Restore(GameLayout)`, `ConversionBackup.Discard(GameLayout)`, `GameLayout.Previous`, `WriteGameRecordStep.StepName` (Task 1); test helper `ConvertedGames` (Task 1).
- Produces: `GameRecord.ReconvertedAt` (`DateTimeOffset?`); `GameLibrary(DataLayout layout, IPlatform platform, TimeProvider time, ILogger<GameLibrary> logger)`; `IReadOnlyList<(GameLayout Game, GameRecord Record)> GameLibrary.All()`; `static bool GameLibrary.NeedsReconversion(GameRecord record, AnyPS5InstallRecord install)`; `GameLibrary.Find` restores first; `static class ConversionLabels { string Of(ConversionInfo); string Of(AnyPS5InstallRecord); }`; `UpdateGameRecordStep(GameImportContext context, GameRecord previous, AnyPS5InstallRecord install, TimeProvider time) : IStep` with `const string StepName = "Record the game"`.

- [ ] **Step 1: Write the failing tests**

Append to `tests/PS5Kitsune.Core.Tests/Library/GameRecordStoreTests.cs` (inside the class):

```csharp
    [Fact]
    public void A_record_without_reconverted_at_reads_as_never_reconverted()
    {
        TestFiles.Write(RecordFile, """
            {
              "schemaVersion": 1, "titleId": "PPSA99666", "name": "DOOM", "executable": "PPSA99666.exe",
              "convertedWith": { "profile": "p", "mode": "release", "version": "v0.1.2", "commit": null, "toIntel": false },
              "audit": { "implemented": 361, "stub": 3, "absent": 0, "verdict": "orange" },
              "addedAt": "2026-10-23T10:00:00+02:00"
            }
            """);

        Assert.Null(GameRecordStore.Read(RecordFile)!.ReconvertedAt);
    }
```

Replace `tests/PS5Kitsune.Core.Tests/Library/GameLibraryTests.cs` with:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tests.Games;

namespace PS5Kitsune.Core.Tests.Library;

public sealed class GameLibraryTests : IDisposable
{
    private static readonly AnyPS5InstallRecord Release = new(2, "doom-validated-2026-10", AnyPS5Mode.Release, "v0.1.2", null, null, DateTimeOffset.UnixEpoch);

    private readonly TempDirectory _temp = new();
    private readonly DataLayout _layout;
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 20, 0, 0, TimeSpan.Zero));
    private readonly GameLibrary _library;

    public GameLibraryTests()
    {
        _layout = new DataLayout(_temp.Path);
        _library = new GameLibrary(_layout, new FakePlatform(), _time, NullLogger<GameLibrary>.Instance);
    }

    private Task WriteAsync(string titleId, string name) => GameRecordStore.WriteAsync(
        Path.Combine(_layout.GameDirectory(titleId), ".ps5kitsune", "game.json"), TestRecords.Sample() with { TitleId = titleId, Name = name }, TestContext.Current.CancellationToken);

    private Task WriteSampleAsync() => WriteAsync("PPSA99666", "DOOM");

    [Fact]
    public async Task Finds_a_recorded_game()
    {
        await WriteSampleAsync();

        var (game, record) = _library.Find("PPSA99666");

        Assert.Equal("DOOM", record.Name);
        Assert.Equal(_layout.GameDirectory("PPSA99666"), game.Root);
    }

    [Fact]
    public void An_unknown_title_id_names_the_games_folder()
    {
        var exception = Assert.Throws<GameNotFoundException>(() => _library.Find("PPSA00001"));

        Assert.Contains("PPSA00001", exception.Message, StringComparison.Ordinal);
        Assert.Contains(_layout.Games, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_title_id_with_path_characters_is_not_looked_up()
    {
        await WriteSampleAsync();

        Assert.Throws<GameNotFoundException>(() => _library.Find(Path.Combine("..", "games", "PPSA99666")));
    }

    [Fact]
    public async Task Marking_a_game_played_records_the_time()
    {
        await WriteSampleAsync();
        var (game, record) = _library.Find("PPSA99666");

        await _library.MarkPlayedAsync(game, record, TestContext.Current.CancellationToken);

        Assert.Equal(_time.GetLocalNow(), _library.Find("PPSA99666").Record.LastPlayedAt);
    }

    [Fact]
    public async Task Finding_a_game_restores_an_interrupted_reconvert()
    {
        var game = new GameLayout(_layout.GameDirectory("PPSA99666"), "PPSA99666", new FakePlatform());
        ConvertedGames.Create(game);
        await WriteSampleAsync();
        var before = ConvertedGames.Snapshot(game.Root);
        ConversionBackup.SetAside(game);
        TestFiles.Write(game.Executable, "half-built exe");

        _library.Find("PPSA99666");

        Assert.Equal(before, ConvertedGames.Snapshot(game.Root));
    }

    [Fact]
    public async Task Lists_the_games_by_name_and_skips_a_malformed_record()
    {
        await WriteAsync("PPSA00002", "Zeta");
        await WriteAsync("PPSA00001", "Alpha");
        TestFiles.Write(Path.Combine(_layout.GameDirectory("PPSA00003"), ".ps5kitsune", "game.json"), "{ \"titleId\": ");
        Directory.CreateDirectory(_layout.GameDirectory("PPSA00004"));

        var games = _library.All();

        Assert.Equal(["Alpha", "Zeta"], games.Select(entry => entry.Record.Name));
        Assert.Equal(_layout.GameDirectory("PPSA00001"), games[0].Game.Root);
    }

    [Fact]
    public void No_games_folder_lists_nothing()
    {
        Assert.Empty(_library.All());
    }

    [Fact]
    public void A_game_converted_with_the_install_in_use_needs_nothing()
    {
        Assert.False(GameLibrary.NeedsReconversion(TestRecords.Sample(), Release));
    }

    [Theory]
    [InlineData("other-profile", AnyPS5Mode.Release, "v0.1.2", null)]
    [InlineData("doom-validated-2026-10", AnyPS5Mode.Build, "v0.1.2", null)]
    [InlineData("doom-validated-2026-10", AnyPS5Mode.Release, "v0.1.3", null)]
    [InlineData("doom-validated-2026-10", AnyPS5Mode.Release, "v0.1.2", "3fd1cc3")]
    public void Any_difference_in_the_install_needs_reconversion(string profile, AnyPS5Mode mode, string version, string? commit)
    {
        var install = Release with { Profile = profile, Mode = mode, Version = version, Commit = commit };

        Assert.True(GameLibrary.NeedsReconversion(TestRecords.Sample(), install));
    }

    [Fact]
    public void Labels_name_the_mode_and_the_version()
    {
        Assert.Equal("release v0.1.2", ConversionLabels.Of(TestRecords.Sample().ConvertedWith));
        Assert.Equal("build 3fd1cc3", ConversionLabels.Of(Release with { Mode = AnyPS5Mode.Build, Version = "3fd1cc3" }));
    }

    public void Dispose() => _temp.Dispose();
}
```

`tests/PS5Kitsune.Core.Tests/Games/UpdateGameRecordStepTests.cs`:

```csharp
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Games.Steps;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tests.Library;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class UpdateGameRecordStepTests : IDisposable
{
    private static readonly AnyPS5InstallRecord Build = new(2, "doom-validated-2026-10", AnyPS5Mode.Build, "3fd1cc3", new string('3', 40), null, DateTimeOffset.UnixEpoch);

    private readonly TempDirectory _temp = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 23, 18, 0, 0, TimeSpan.Zero));
    private readonly GameLayout _game;
    private readonly GameImportContext _context;

    public UpdateGameRecordStepTests()
    {
        _game = new GameLayout(_temp.Combine("games", TestGames.TitleId), TestGames.TitleId, new FakePlatform());
        _context = new GameImportContext(new GameSource(_game.App0, null), new GameMetadata(TestGames.TitleId, "DOOM"), ["sce_module"], _game, toIntel: true)
        {
            Audit = new AuditSummary(360, 4, 0, new Dictionary<string, int>()),
        };
    }

    [Fact]
    public async Task Discards_the_old_conversion_and_rewrites_the_conversion_fields_only()
    {
        ConvertedGames.Create(_game);
        ConversionBackup.SetAside(_game);
        var previous = TestRecords.Sample() with
        {
            ConvertedWith = new ConversionInfo("doom-validated-2026-10", "release", "v0.1.2", null, true),
            Settings = new GameSettings { Lavapipe = true, Language = 2 },
            LastPlayedAt = new DateTimeOffset(2026, 10, 22, 21, 0, 0, TimeSpan.Zero),
        };
        var step = new UpdateGameRecordStep(_context, previous, Build, _time);
        Assert.Equal("Record the game", step.Name);
        Assert.False(step.IsAlreadyDone());

        await step.ExecuteAsync(null, TestContext.Current.CancellationToken);

        Assert.False(Directory.Exists(_game.Previous));
        Assert.False(Directory.Exists(_game.PreviousDiscarded));
        var record = GameRecordStore.Read(_game.RecordFile)!;
        Assert.Equal(new ConversionInfo("doom-validated-2026-10", "build", "3fd1cc3", new string('3', 40), true), record.ConvertedWith);
        Assert.Equal(new AuditRecord(360, 4, 0, AuditVerdict.Orange), record.Audit);
        Assert.Equal(_time.GetLocalNow(), record.ReconvertedAt);
        Assert.Equal(previous with { ConvertedWith = record.ConvertedWith, Audit = record.Audit, ReconvertedAt = record.ReconvertedAt }, record);
    }

    public void Dispose() => _temp.Dispose();
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build errors: `ReconvertedAt`, the four-argument `GameLibrary` constructor, `All`, `NeedsReconversion`, `ConversionLabels`, `UpdateGameRecordStep` do not exist.

- [ ] **Step 3: `GameRecord.ReconvertedAt`**

In `src/PS5Kitsune.Core/Library/GameRecord.cs`, after `AddedAt`:

```csharp
    /// <summary>When Reconvert last rewrote the conversion; null for a game never reconverted. Optional, so schema 1 still reads.</summary>
    public DateTimeOffset? ReconvertedAt { get; init; }
```

- [ ] **Step 4: `ConversionLabels`**

`src/PS5Kitsune.Core/Library/ConversionLabels.cs`:

```csharp
using PS5Kitsune.Core.AnyPS5;

namespace PS5Kitsune.Core.Library;

/// <summary>"{mode} {version}", e.g. "build 3fd1cc3" or "release v0.1.2", for a game's conversion and for an install.</summary>
public static class ConversionLabels
{
    public static string Of(ConversionInfo conversion)
    {
        ArgumentNullException.ThrowIfNull(conversion);
        return $"{conversion.Mode} {conversion.Version}";
    }

    public static string Of(AnyPS5InstallRecord install)
    {
        ArgumentNullException.ThrowIfNull(install);
        return $"{InstallModes.Name(install.Mode)} {install.Version}";
    }
}
```

- [ ] **Step 5: `GameLibrary`**

Replace `src/PS5Kitsune.Core/Library/GameLibrary.cs` with:

```csharp
using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Core.Library;

public sealed class GameNotFoundException(string message) : Exception(message);

/// <summary>
/// Games added under &lt;root&gt;/games, looked up by title ID. library.json comes with milestone 7.
/// Reading a game first restores a Reconvert that did not finish, so a half-built conversion is never used.
/// </summary>
public sealed partial class GameLibrary(DataLayout layout, IPlatform platform, TimeProvider time, ILogger<GameLibrary> logger)
{
    /// <exception cref="GameNotFoundException">No game.json for this title ID.</exception>
    /// <exception cref="InvalidGameRecordException">The game.json is malformed.</exception>
    public (GameLayout Game, GameRecord Record) Find(string titleId)
    {
        if (GameValidator.IsValidTitleId(titleId))
        {
            var game = new GameLayout(layout.GameDirectory(titleId), titleId, platform);
            RestoreInterrupted(game);
            if (GameRecordStore.Read(game.RecordFile) is { } record)
            {
                return (game, record);
            }
        }

        throw new GameNotFoundException($"No game {titleId} in {layout.Games}.");
    }

    /// <summary>Every game whose game.json reads, ordered by name; a game that cannot be read is skipped and logged.</summary>
    public IReadOnlyList<(GameLayout Game, GameRecord Record)> All()
    {
        if (!Directory.Exists(layout.Games))
        {
            return [];
        }

        var games = new List<(GameLayout Game, GameRecord Record)>();
        foreach (var directory in Directory.EnumerateDirectories(layout.Games))
        {
            var titleId = Path.GetFileName(directory);
            if (!GameValidator.IsValidTitleId(titleId))
            {
                continue;
            }

            var game = new GameLayout(directory, titleId, platform);
            try
            {
                RestoreInterrupted(game);
                if (GameRecordStore.Read(game.RecordFile) is { } record)
                {
                    games.Add((game, record));
                }
            }
            catch (Exception exception) when (exception is InvalidGameRecordException or IOException or UnauthorizedAccessException)
            {
                LogGameSkipped(exception, directory);
            }
        }

        return [.. games.OrderBy(entry => entry.Record.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>True when the game was converted with another profile, mode, version or commit than <paramref name="install"/>.</summary>
    public static bool NeedsReconversion(GameRecord record, AnyPS5InstallRecord install)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(install);
        var converted = record.ConvertedWith;
        return converted.Profile != install.Profile
            || converted.Mode != InstallModes.Name(install.Mode)
            || converted.Version != install.Version
            || converted.Commit != install.Commit;
    }

    public Task MarkPlayedAsync(GameLayout game, GameRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(record);
        return GameRecordStore.WriteAsync(game.RecordFile, record with { LastPlayedAt = time.GetLocalNow() }, cancellationToken);
    }

    private void RestoreInterrupted(GameLayout game)
    {
        if (ConversionBackup.Restore(game))
        {
            var root = game.Root;
            LogRestored(root);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Restored the previous conversion of {Game}: a Reconvert did not finish.")]
    private partial void LogRestored(string game);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipped the game in {Directory}: it cannot be read.")]
    private partial void LogGameSkipped(Exception exception, string directory);
}
```

- [ ] **Step 6: `UpdateGameRecordStep`**

`src/PS5Kitsune.Core/Games/Steps/UpdateGameRecordStep.cs`:

```csharp
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.Games.Steps;

/// <summary>
/// Reconvert's last step: discards the set-aside conversion, then rewrites game.json with the install in use and the new audit.
/// A crash between the two leaves the new conversion with the old record: the game only shows as needing another Reconvert.
/// </summary>
public sealed class UpdateGameRecordStep(GameImportContext context, GameRecord previous, AnyPS5InstallRecord install, TimeProvider time) : IStep
{
    public const string StepName = WriteGameRecordStep.StepName;

    public string Name => StepName;

    public bool IsAlreadyDone() => false;

    public Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        var audit = context.Audit ?? throw new InvalidOperationException("The audit must run before the game is recorded.");
        ConversionBackup.Discard(context.Game);
        var record = previous with
        {
            ConvertedWith = new ConversionInfo(install.Profile, InstallModes.Name(install.Mode), install.Version, install.Commit, previous.ConvertedWith.ToIntel),
            Audit = new AuditRecord(audit.Implemented, audit.Stub, audit.Absent, audit.Verdict),
            ReconvertedAt = time.GetLocalNow(),
        };
        return GameRecordStore.WriteAsync(context.Game.RecordFile, record, cancellationToken);
    }
}
```

- [ ] **Step 7: Run the tests**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: 0 failed. (`GameLibrary` is resolved from DI in the CLI and the app; the container supplies the new logger.)

- [ ] **Step 8: Commit**

```bash
git add src/PS5Kitsune.Core tests/PS5Kitsune.Core.Tests
git commit -m "List games, tell which need reconversion, and record a reconversion

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 3: Core — GameReconverter

**Files:**
- Create: `src/PS5Kitsune.Core/Games/IGameReconverter.cs`, `GameReconverter.cs`, `GameReconverted.cs`, `ReconvertRestoreException.cs`
- Modify: `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`, `src/PS5Kitsune.Core/Games/GameImporter.cs`, `src/PS5Kitsune.Core/Games/IGameImporter.cs`
- Create: `tests/PS5Kitsune.Core.Tests/Games/GameReconverterTests.cs`
- Modify: `tests/PS5Kitsune.Core.Tests/Games/GameImporterTests.cs`

**Interfaces:**
- Consumes: `SetAsideStep`, `ConversionBackup.Restore`, step `StepName` constants (Task 1); `GameLibrary.Find`, `GameLibrary(…, ILogger<GameLibrary>)`, `UpdateGameRecordStep`, `GameLibrary.NeedsReconversion` (Task 2).
- Produces:
  - `public sealed record GameReconverted(GameRecord Record, string GameDirectory, AuditSummary Audit, AuditRecord PreviousAudit);`
  - `public sealed class ReconvertRestoreException(Exception failure, Exception restoreFailure) : Exception` with `Exception RestoreFailure`; `InnerException` is the run's failure (`StepFailedException` or `OperationCanceledException`); `Message` is the failure's message.
  - `public interface IGameReconverter { IReadOnlyList<string> StepNames { get; } Task<GameReconverted> ReconvertAsync(string titleId, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default); }`
  - `GameReconverter` (registered as itself and as `IGameReconverter`), `static IReadOnlyList<string> GameReconverter.Names`.
  - `GameImporter.AddAsync(PreparedGame, …)` throws `ObjectDisposedException` for a disposed game.

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.Core.Tests/Games/GameReconverterTests.cs`:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Tests.AnyPS5;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class GameReconverterTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly AnyPS5TestSetup _setup;
    private readonly GameImporter _importer;
    private readonly GameLibrary _library;
    private readonly GameReconverter _reconverter;
    private readonly CancellationTokenSource _cancellation = new();
    private int _relinkerExitCode;
    private int _auditExitCode;
    private bool _cancelDuringRelink;
    private string _relinkerOutput = "add";

    public GameReconverterTests()
    {
        _setup = new AnyPS5TestSetup(_temp.Combine("root"));
        _setup.Runner.Respond = Respond;
        _importer = new GameImporter(
            _setup.Layout, _setup.Platform, _setup.Profile, _setup.Paths, _setup.Installer,
            new GameSourceResolver(_setup.Layout), new GameValidator(_ => long.MaxValue),
            _setup.PythonTool, _setup.Runner, _setup.StepRunner, _setup.Time, NullLogger<GameImporter>.Instance);
        _library = new GameLibrary(_setup.Layout, _setup.Platform, _setup.Time, NullLogger<GameLibrary>.Instance);
        _reconverter = new GameReconverter(
            _library, _setup.Paths, _setup.Profile, _setup.Installer, _setup.PythonTool, _setup.Runner, _setup.StepRunner, _setup.Time,
            NullLogger<GameReconverter>.Instance);
    }

    private GameLayout Game => new(_setup.Layout.GameDirectory(TestGames.TitleId), TestGames.TitleId, _setup.Platform);

    // Plays unwrap_self.py, the relinker and import_audit.py. The relinker's outputs carry _relinkerOutput, so a test sees which run wrote them.
    private FakeProcessResult Respond(ProcessSpec spec)
    {
        switch (FakeProcessRunner.ScriptName(spec))
        {
            case "unwrap_self.py":
                TestFiles.Write(spec.Arguments[^1], TestFiles.Elf());
                return new FakeProcessResult(0);
            case "import_audit.py":
                if (_auditExitCode != 0)
                {
                    return new FakeProcessResult(_auditExitCode, StandardError: ["audit: unreadable"]);
                }

                TestFiles.Write(Path.Combine(spec.WorkingDirectory!, "audit.json"), AuditFiles.Json(361, 3, 0));
                return new FakeProcessResult(0);
        }

        var root = spec.WorkingDirectory!;
        TestFiles.Write(Path.Combine(root, "PPSA99666.exe"), _relinkerOutput + " exe");
        TestFiles.Write(Path.Combine(root, "PPSA99666.registry.json"), _relinkerOutput + " registry");
        TestFiles.Write(Path.Combine(root, "app0", "sce_module", "libc.guest.prx"), _relinkerOutput + " pe");
        if (_cancelDuringRelink)
        {
            _cancellation.Cancel();
        }

        return _relinkerExitCode != 0 ? new FakeProcessResult(_relinkerExitCode, StandardError: ["relinker: failure"]) : new FakeProcessResult(0);
    }

    /// <summary>Installs the release, adds DOOM, then records it as converted by the build (360/4/0) and lets the game create its files.</summary>
    private async Task AddDoomConvertedWithTheBuildAsync(bool toIntel = false)
    {
        await _setup.InstallAsync();
        var zip = _temp.Combine("PPSA99666.zip");
        File.WriteAllBytes(zip, TestGames.Zip("PPSA99666/"));
        Assert.IsType<GameAdded>(await _importer.AddAsync(zip, toIntel: false, cancellationToken: TestContext.Current.CancellationToken));

        var (game, record) = _library.Find(TestGames.TitleId);
        await GameRecordStore.WriteAsync(game.RecordFile, record with
        {
            ConvertedWith = new ConversionInfo("test-profile", "build", "3fd1cc3", new string('3', 40), toIntel),
            Audit = new AuditRecord(360, 4, 0, AuditVerdict.Orange),
        }, TestContext.Current.CancellationToken);
        foreach (var relative in new[] { "data/save.dat", "download0/dlc.bin", "_trophy/trophy.dat", "anyps5-input.ini", "anyps5-fonts/font.ttf", "shader_cache/v1/cache.bin" })
        {
            TestFiles.Write(Path.Combine(game.Root, relative), "kept " + relative);
        }

        TestFiles.Write(Path.Combine(game.Libs, "libOnlyInOldVersion.prx"), "old only");
        _relinkerOutput = "reconvert";
    }

    private Task<GameReconverted> ReconvertAsync() =>
        _reconverter.ReconvertAsync(TestGames.TitleId, cancellationToken: _cancellation.Token);

    [Fact]
    public void Lists_the_six_steps()
    {
        Assert.Equal(
            ["Set the old conversion aside", "Copy the AnyPS5 libraries", "Unwrap SELF files", "Convert with the relinker", "Audit the imports", "Record the game"],
            _reconverter.StepNames);
    }

    [Fact]
    public async Task Reconverting_rewrites_the_conversion_and_keeps_everything_else()
    {
        await AddDoomConvertedWithTheBuildAsync();
        var before = ConvertedGames.Snapshot(Game.Root);
        var (_, previous) = _library.Find(TestGames.TitleId);

        var result = await ReconvertAsync();

        var after = ConvertedGames.Snapshot(Game.Root);
        foreach (var (relative, content) in before.Where(entry => !ConvertedGames.IsConversionOutput(entry.Key)))
        {
            Assert.Equal(content, after[relative]);
        }

        Assert.Equal("reconvert exe", File.ReadAllText(Game.Executable));
        Assert.Equal("reconvert pe", File.ReadAllText(Path.Combine(Game.App0, "sce_module", "libc.guest.prx")));
        Assert.False(File.Exists(Path.Combine(Game.Libs, "libOnlyInOldVersion.prx")));
        Assert.True(File.Exists(Path.Combine(Game.Libs, "libSceAudioOut.prx")));
        Assert.False(Directory.Exists(Game.Previous));

        Assert.Equal(new AuditRecord(360, 4, 0, AuditVerdict.Orange), result.PreviousAudit);
        Assert.Equal(361, result.Audit.Implemented);
        Assert.Equal(Game.Root, result.GameDirectory);
        var record = GameRecordStore.Read(Game.RecordFile)!;
        Assert.Equal(record, result.Record);
        Assert.Equal(new ConversionInfo("test-profile", "release", "v0.1.2", null, false), record.ConvertedWith);
        Assert.Equal(new AuditRecord(361, 3, 0, AuditVerdict.Orange), record.Audit);
        Assert.Equal(_setup.Time.GetLocalNow(), record.ReconvertedAt);
        Assert.Equal(previous.AddedAt, record.AddedAt);
        Assert.Equal(previous.Settings, record.Settings);
        Assert.False(GameLibrary.NeedsReconversion(record, _setup.Installer.ReadRecord()!));
    }

    [Fact]
    public async Task The_intel_option_of_the_game_is_reused()
    {
        await AddDoomConvertedWithTheBuildAsync(toIntel: true);

        await ReconvertAsync();

        var relink = _setup.Runner.Calls.Last(spec => spec.FileName == _setup.Paths.Relinker);
        Assert.Contains("--to-intel", relink.Arguments);
        Assert.True(GameRecordStore.Read(Game.RecordFile)!.ConvertedWith.ToIntel);
    }

    [Fact]
    public async Task A_failed_relink_restores_the_previous_conversion()
    {
        await AddDoomConvertedWithTheBuildAsync();
        var before = ConvertedGames.Snapshot(Game.Root);
        _relinkerExitCode = 2;

        var failure = await Assert.ThrowsAsync<StepFailedException>(ReconvertAsync);

        Assert.Equal("Convert with the relinker", failure.StepName);
        Assert.Equal(before, ConvertedGames.Snapshot(Game.Root));
    }

    [Fact]
    public async Task A_failed_audit_restores_the_previous_conversion()
    {
        await AddDoomConvertedWithTheBuildAsync();
        var before = ConvertedGames.Snapshot(Game.Root);
        _auditExitCode = 2;

        var failure = await Assert.ThrowsAsync<StepFailedException>(ReconvertAsync);

        Assert.Equal("Audit the imports", failure.StepName);
        Assert.Equal(before, ConvertedGames.Snapshot(Game.Root));
    }

    [Fact]
    public async Task A_cancelled_reconvert_restores_the_previous_conversion()
    {
        await AddDoomConvertedWithTheBuildAsync();
        var before = ConvertedGames.Snapshot(Game.Root);
        _cancelDuringRelink = true;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(ReconvertAsync);

        Assert.Equal(before, ConvertedGames.Snapshot(Game.Root));
    }

    [Fact]
    public async Task A_running_game_stops_the_reconvert_with_nothing_changed()
    {
        await AddDoomConvertedWithTheBuildAsync();
        var before = ConvertedGames.Snapshot(Game.Root);

        StepFailedException failure;
        using (new FileStream(Game.Executable, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            failure = await Assert.ThrowsAsync<StepFailedException>(ReconvertAsync);
        }

        Assert.Equal("Set the old conversion aside", failure.StepName);
        Assert.Equal("Close the game if it is running.", Assert.IsType<GameImportException>(failure.InnerException).Details);
        Assert.Equal(before, ConvertedGames.Snapshot(Game.Root));
    }

    [Fact]
    public async Task An_interrupted_reconvert_is_restored_before_running()
    {
        await AddDoomConvertedWithTheBuildAsync();
        ConversionBackup.SetAside(Game);
        TestFiles.Write(Game.Executable, "half-built exe");

        await ReconvertAsync();

        Assert.Equal("reconvert exe", File.ReadAllText(Game.Executable));
        Assert.False(File.Exists(Path.Combine(Game.Libs, "libOnlyInOldVersion.prx")));
        Assert.False(Directory.Exists(Game.Previous));
    }

    [Fact]
    public async Task Reconverting_requires_the_install()
    {
        await Assert.ThrowsAsync<AnyPS5NotInstalledException>(ReconvertAsync);
    }

    [Fact]
    public async Task An_unknown_game_is_reported()
    {
        await _setup.InstallAsync();

        await Assert.ThrowsAsync<GameNotFoundException>(ReconvertAsync);
    }

    public void Dispose()
    {
        _cancellation.Dispose();
        _temp.Dispose();
    }
}
```

Append to `tests/PS5Kitsune.Core.Tests/Games/GameImporterTests.cs` (inside the class):

```csharp
    [Fact]
    public async Task A_disposed_game_is_refused()
    {
        await _setup.InstallAsync();
        var game = await _importer.PrepareAsync(WriteZip(), TestContext.Current.CancellationToken);
        await game.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => _importer.AddAsync(game, false, cancellationToken: TestContext.Current.CancellationToken));
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build errors: `GameReconverter` does not exist.

- [ ] **Step 3: The result, the exception and the interface**

`src/PS5Kitsune.Core/Games/GameReconverted.cs`:

```csharp
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Games;

/// <param name="Audit">The new audit's counts and unresolved functions.</param>
/// <param name="PreviousAudit">The counts game.json held before, to show what changed.</param>
public sealed record GameReconverted(GameRecord Record, string GameDirectory, AuditSummary Audit, AuditRecord PreviousAudit);
```

`src/PS5Kitsune.Core/Games/ReconvertRestoreException.cs`:

```csharp
namespace PS5Kitsune.Core.Games;

/// <summary>
/// A Reconvert failed or was cancelled, and putting the previous conversion back failed too. .ps5kitsune/previous is kept,
/// so the next Reconvert or the next read of the game restores it.
/// </summary>
/// <param name="failure">Why the Reconvert stopped (a <c>StepFailedException</c> or an <see cref="OperationCanceledException"/>); the inner exception.</param>
/// <param name="restoreFailure">Why the restore failed.</param>
public sealed class ReconvertRestoreException(Exception failure, Exception restoreFailure) : Exception(failure?.Message, failure)
{
    public Exception RestoreFailure { get; } = restoreFailure;
}
```

`src/PS5Kitsune.Core/Games/IGameReconverter.cs`:

```csharp
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.Games;

/// <summary>Converts an added game again with the AnyPS5 in use, keeping what the game created. See the 6b spec.</summary>
public interface IGameReconverter
{
    /// <summary>The Reconvert steps' names, in order, to list before it starts.</summary>
    IReadOnlyList<string> StepNames { get; }

    /// <summary>Sets the conversion aside, rebuilds it and rewrites game.json; a failure or a cancel puts the previous conversion back.</summary>
    /// <exception cref="AnyPS5NotInstalledException">AnyPS5 is not installed.</exception>
    /// <exception cref="Library.GameNotFoundException">No game with this title ID.</exception>
    /// <exception cref="Library.InvalidGameRecordException">The game.json is malformed.</exception>
    /// <exception cref="StepFailedException">A step failed; the previous conversion is back.</exception>
    /// <exception cref="OperationCanceledException">Cancelled; the previous conversion is back.</exception>
    /// <exception cref="ReconvertRestoreException">A step failed or the run was cancelled, and the restore failed.</exception>
    Task<GameReconverted> ReconvertAsync(string titleId, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: `GameReconverter`**

`src/PS5Kitsune.Core/Games/GameReconverter.cs`:

```csharp
using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games.Steps;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.Games;

/// <summary>
/// Reconvert: sets the conversion outputs aside, runs the add's libs, unwrap, relink and audit steps on the game folder, then
/// rewrites game.json. app0 is not copied again, and nothing the game created is touched. See docs/architecture.md, "V2 — Add a game".
/// </summary>
public sealed partial class GameReconverter(
    GameLibrary library,
    AnyPS5Paths paths,
    AnyPS5Profile profile,
    AnyPS5Installer installer,
    PythonEmbeddableTool python,
    IProcessRunner processRunner,
    StepRunner stepRunner,
    TimeProvider time,
    ILogger<GameReconverter> logger) : IGameReconverter
{
    public static readonly IReadOnlyList<string> Names =
        [SetAsideStep.StepName, CopyLibsStep.StepName, UnwrapSelfStep.StepName, RelinkStep.StepName, AuditStep.StepName, UpdateGameRecordStep.StepName];

    public IReadOnlyList<string> StepNames => Names;

    public async Task<GameReconverted> ReconvertAsync(
        string titleId, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(titleId);
        if (!installer.IsInstalled())
        {
            throw new AnyPS5NotInstalledException();
        }

        var install = installer.ReadRecord() ?? throw new AnyPS5NotInstalledException();
        // Find restores a Reconvert that did not finish, so this run starts from the last complete conversion.
        var (game, record) = library.Find(titleId);
        List<string> modules = [.. GameValidator.ModuleDirectoryNames.Where(name => Directory.Exists(Path.Combine(game.App0, name)))];
        var context = new GameImportContext(
            new GameSource(game.App0, null), new GameMetadata(record.TitleId, record.Name), modules, game, record.ConvertedWith.ToIntel);
        IStep[] steps =
        [
            new SetAsideStep(game),
            new CopyLibsStep(paths, game),
            new UnwrapSelfStep(context, paths, python, processRunner),
            new RelinkStep(context, paths, profile, processRunner),
            new AuditStep(context, paths, python, processRunner),
            new UpdateGameRecordStep(context, record, install, time),
        ];

        try
        {
            await stepRunner.RunAsync(steps, progress, onEvent, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is StepFailedException or OperationCanceledException)
        {
            try
            {
                ConversionBackup.Restore(game);
            }
            catch (Exception restoreFailure) when (restoreFailure is IOException or UnauthorizedAccessException)
            {
                var root = game.Root;
                LogRestoreFailed(restoreFailure, root);
                throw new ReconvertRestoreException(exception, restoreFailure);
            }

            throw;
        }

        var updated = GameRecordStore.Read(game.RecordFile) ?? throw new InvalidOperationException($"{game.RecordFile} was not written.");
        return new GameReconverted(updated, game.Root, context.Audit ?? AuditSummary.Read(game.AuditFile), record.Audit);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not restore the previous conversion of {Game}; it will be restored on the next start or Reconvert.")]
    private partial void LogRestoreFailed(Exception exception, string game);
}
```

- [ ] **Step 5: Register it, and guard `AddAsync`**

In `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`, after `services.AddSingleton<GameLibrary>();`:

```csharp
        services.AddSingleton<GameReconverter>();
        services.AddSingleton<IGameReconverter>(provider => provider.GetRequiredService<GameReconverter>());
```

In `src/PS5Kitsune.Core/Games/GameImporter.cs`, in `AddAsync(PreparedGame game, …)`, right after `ArgumentNullException.ThrowIfNull(game);`:

```csharp
        ObjectDisposedException.ThrowIf(game.IsDisposed, game);
```

In `src/PS5Kitsune.Core/Games/IGameImporter.cs`, add to `AddAsync(PreparedGame …)`'s doc comment:

```csharp
    /// <exception cref="ObjectDisposedException"><paramref name="game"/> was disposed (its staging folder may be gone).</exception>
```

- [ ] **Step 6: Run the tests**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.Core.Tests.Games.GameReconverterTests"`
Expected: all pass (exit code 8). Then `dotnet test --solution PS5Kitsune.slnx -c Release`: 0 failed.

- [ ] **Step 7: Commit**

```bash
git add src/PS5Kitsune.Core tests/PS5Kitsune.Core.Tests
git commit -m "Reconvert a game and restore it on failure or cancel

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 4: CLI — reconvert, and a warning in run

**Files:**
- Create: `src/PS5Kitsune.Cli/ReconvertCommand.cs`, `src/PS5Kitsune.Cli/VerdictReport.cs`
- Modify: `src/PS5Kitsune.Cli/AddCommand.cs`, `RunCommand.cs`, `CliLog.cs`, `Program.cs`

**Interfaces:**
- Consumes: `GameReconverter.ReconvertAsync`, `GameReconverted`, `ReconvertRestoreException` (Task 3); `GameLibrary.Find`, `GameLibrary.NeedsReconversion`, `ConversionLabels.Of` (Task 2).
- Produces: the `reconvert` command; `VerdictReport.Print(ILogger logger, AuditRecord audit)`.

- [ ] **Step 1: Log messages**

Append to `src/PS5Kitsune.Cli/CliLog.cs` (inside the class):

```csharp
    [LoggerMessage(Level = LogLevel.Information, Message = "{Name} ({TitleId}) is reconverted in {Directory}.")]
    public static partial void GameReconverted(this ILogger logger, string name, string titleId, string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "Before: {Implemented} implemented, {Stub} stubs, {Absent} absent.")]
    public static partial void AuditBefore(this ILogger logger, int implemented, int stub, int absent);

    [LoggerMessage(Level = LogLevel.Information, Message = "Already converted with {Conversion}; reconverting anyway.")]
    public static partial void AlreadyConverted(this ILogger logger, string conversion);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The previous conversion was restored.")]
    public static partial void PreviousRestored(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "The previous conversion could not be restored yet; it will be restored on the next start or reconvert.")]
    public static partial void PreviousNotRestored(this ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Converted with {ConvertedWith}; the AnyPS5 in use is {InUse}. Run ps5kitsune reconvert {TitleId}.")]
    public static partial void NeedsReconversion(this ILogger logger, string convertedWith, string inUse, string titleId);
```

- [ ] **Step 2: Shared verdict lines**

`src/PS5Kitsune.Cli/VerdictReport.cs`:

```csharp
using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Cli;

/// <summary>The audit counts and the verdict line, as add and reconvert print them.</summary>
internal static class VerdictReport
{
    public static void Print(ILogger logger, AuditRecord audit)
    {
        var implemented = audit.Implemented;
        var stub = audit.Stub;
        var absent = audit.Absent;
        logger.AuditCounts(implemented, stub, absent);
        switch (audit.Verdict)
        {
            case AuditVerdict.Green:
                logger.VerdictGreen();
                break;
            case AuditVerdict.Orange:
                logger.VerdictOrange();
                break;
            default:
                logger.VerdictRed();
                break;
        }
    }
}
```

In `src/PS5Kitsune.Cli/AddCommand.cs`, in the `case GameAdded added:` branch, replace everything from `var audit = record.Audit;` up to (not including) `return 0;` with:

```csharp
                    VerdictReport.Print(logger, record.Audit);
```

- [ ] **Step 3: The command**

`src/PS5Kitsune.Cli/ReconvertCommand.cs`:

```csharp
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Cli;

internal static class ReconvertCommand
{
    public static Command Create(Option<DirectoryInfo?> rootOption)
    {
        var titleIdArgument = new Argument<string>("titleId") { Description = "Title ID of an added game, for example PPSA99666." };
        var command = new Command(
            "reconvert",
            "Convert an added game again with the AnyPS5 in use: new libraries, executable and audit. Saves and settings are kept; a failure puts the previous conversion back.")
        {
            titleIdArgument,
        };
        command.SetAction((parseResult, cancellationToken) => CommandHost.RunAsync(parseResult.GetValue(rootOption), "reconvert", async (services, logger) =>
        {
            var installer = services.GetRequiredService<AnyPS5Installer>();
            installer.MigrateLegacyInstall();
            var titleId = parseResult.GetRequiredValue(titleIdArgument);
            if (installer.IsInstalled() && installer.ReadRecord() is { } install)
            {
                var (_, current) = services.GetRequiredService<GameLibrary>().Find(titleId);
                if (!GameLibrary.NeedsReconversion(current, install))
                {
                    var conversion = ConversionLabels.Of(install);
                    logger.AlreadyConverted(conversion);
                }
            }

            GameReconverted result;
            try
            {
                result = await services.GetRequiredService<GameReconverter>().ReconvertAsync(titleId, new ConsoleStepProgress(), cancellationToken: cancellationToken);
            }
            catch (StepFailedException exception)
            {
                var message = exception.Message;
                logger.Failed(exception, message);
                logger.PreviousRestored();
                return 1;
            }
            catch (OperationCanceledException)
            {
                logger.PreviousRestored();
                throw;
            }
            catch (ReconvertRestoreException exception)
            {
                var message = exception.Message;
                logger.Failed(exception, message);
                logger.PreviousNotRestored(exception.RestoreFailure);
                return 1;
            }

            var record = result.Record;
            var name = record.Name;
            var reconvertedTitleId = record.TitleId;
            var directory = result.GameDirectory;
            logger.GameReconverted(name, reconvertedTitleId, directory);
            VerdictReport.Print(logger, record.Audit);
            var before = result.PreviousAudit;
            var implemented = before.Implemented;
            var stub = before.Stub;
            var absent = before.Absent;
            logger.AuditBefore(implemented, stub, absent);
            return 0;
        }));
        return command;
    }
}
```

In `src/PS5Kitsune.Cli/Program.cs`, add `ReconvertCommand.Create(rootOption),` after `AddCommand.Create(rootOption),`.

- [ ] **Step 4: The warning in `run`**

In `src/PS5Kitsune.Cli/RunCommand.cs`, right after `var (game, record) = library.Find(parseResult.GetRequiredValue(titleIdArgument));`:

```csharp
            if (installer.ReadRecord() is { } install && GameLibrary.NeedsReconversion(record, install))
            {
                var convertedWith = ConversionLabels.Of(record.ConvertedWith);
                var inUse = ConversionLabels.Of(install);
                var titleId = record.TitleId;
                logger.NeedsReconversion(convertedWith, inUse, titleId);
            }
```

- [ ] **Step 5: Build and smoke-test**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: 0 warnings, 0 errors.

Run: `src/PS5Kitsune.Cli/bin/Release/net10.0/ps5kitsune.exe reconvert --help`
Expected: the usage lists `<titleId>` and the description above.

Run (an empty root, so nothing is installed): `src/PS5Kitsune.Cli/bin/Release/net10.0/ps5kitsune.exe --root "$(mktemp -d)" reconvert PPSA99666; echo "exit $?"`
Expected: an error saying AnyPS5 is not installed, then `exit 1`.

Run: `dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: 0 failed.

- [ ] **Step 6: Commit**

```bash
git add src/PS5Kitsune.Cli
git commit -m "Add ps5kitsune reconvert and warn when run finds an older conversion

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 5: App — Reconverting page and its result

**Files:**
- Modify: `src/PS5Kitsune.App/ViewModels/StepRunViewModel.cs`, `InstallViewModel.cs`, `AddGameViewModel.cs`, `src/PS5Kitsune.App/Shell/AppLog.cs`
- Create: `src/PS5Kitsune.App/ViewModels/ReconvertViewModel.cs`, `src/PS5Kitsune.App/Views/ReconvertView.axaml`, `src/PS5Kitsune.App/Views/ReconvertView.axaml.cs`
- Modify: `src/PS5Kitsune.App/ViewModels/AddResultViewModel.cs`, `src/PS5Kitsune.App/Views/AddResultView.axaml`
- Modify: `src/PS5Kitsune.App/Navigation/IPages.cs`, `src/PS5Kitsune.App/Shell/PageFactory.cs`, `src/PS5Kitsune.App/Views/MainWindow.axaml`
- Modify: `tests/PS5Kitsune.App.Tests/Fakes.cs`, `tests/PS5Kitsune.App.Tests/AddResultViewModelTests.cs`, `tests/PS5Kitsune.App.Tests/PageFactoryTests.cs`
- Create: `tests/PS5Kitsune.App.Tests/ReconvertViewModelTests.cs`, `tests/PS5Kitsune.App.Tests/StepRunViewModelTests.cs`

**Interfaces:**
- Consumes: `IGameReconverter`, `GameReconverted`, `ReconvertRestoreException` (Task 3).
- Produces: `IPages.Reconvert(string titleId, string name)`, `IPages.ReconvertResult(GameReconverted reconverted)`; `ReconvertViewModel(IGameReconverter reconverter, string titleId, string name, string logFile, IPlatform platform, IUiDispatcher ui, Navigator navigator, IPages pages, Action<bool>? busyChanged = null, ILogger? logger = null)`; `AddResultViewModel(GameReconverted, IPlatform, Navigator, IPages)` and `AddResultViewModel.BeforeText`; `StepRunViewModel`'s base constructor gains `ILogger? logger = null` as its last parameter, and so do `InstallViewModel` and `AddGameViewModel`; test fakes `FakeGameReconverter`, `TestGameData.Reconverted()`, `FakePages.Reconvert` → `"reconvert:{titleId}"`, `FakePages.ReconvertResult` → `"reconverted:{titleId}"`.

- [ ] **Step 1: Test fakes**

In `tests/PS5Kitsune.App.Tests/Fakes.cs`:

Add to `FakePages`:

```csharp
    public object Reconvert(string titleId, string name) => $"reconvert:{titleId}";

    public object ReconvertResult(GameReconverted reconverted) => $"reconverted:{reconverted.Record.TitleId}";
```

Add to `TestGameData`:

```csharp
    public static GameReconverted Reconverted()
    {
        var added = Added(new AuditSummary(361, 3, 0, new Dictionary<string, int>()));
        return new GameReconverted(added.Record, added.GameDirectory, added.Audit, new AuditRecord(360, 4, 0, AuditVerdict.Orange));
    }
```

Add at the end of the file:

```csharp
internal delegate Task<GameReconverted> ReconvertBehavior(string titleId, IProgress<StepProgress>? progress, Action<StepEvent>? onEvent, CancellationToken cancellationToken);

internal sealed class FakeGameReconverter : IGameReconverter
{
    public IReadOnlyList<string> StepNames { get; } =
        ["Set the old conversion aside", "Copy the AnyPS5 libraries", "Unwrap SELF files", "Convert with the relinker", "Audit the imports", "Record the game"];

    public ReconvertBehavior Reconvert { get; set; } = (_, _, _, _) => Task.FromResult(TestGameData.Reconverted());

    public List<string> Reconverted { get; } = [];

    public Task<GameReconverted> ReconvertAsync(string titleId, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default)
    {
        Reconverted.Add(titleId);
        return Reconvert(titleId, progress, onEvent, cancellationToken);
    }
}

/// <summary>Keeps the level and message of each entry.</summary>
internal sealed class ListLogger : Microsoft.Extensions.Logging.ILogger
{
    public List<(Microsoft.Extensions.Logging.LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception)));
}
```

- [ ] **Step 2: Write the failing tests**

`tests/PS5Kitsune.App.Tests/StepRunViewModelTests.cs`:

```csharp
using Microsoft.Extensions.Logging;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.App.Tests;

public sealed class StepRunViewModelTests
{
    [Fact]
    public async Task A_failing_success_hook_is_logged_and_shown()
    {
        var logger = new ListLogger();
        var viewModel = new HookFailingRun(logger);

        await viewModel.RunAsync();

        Assert.True(viewModel.HasFailed);
        Assert.Equal("Something went wrong after the steps: boom", viewModel.Failure);
        Assert.False(viewModel.CanRetry);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    private sealed class HookFailingRun(ILogger logger) : StepRunViewModel("app.log", new TestPlatform(), new InlineDispatcher(), null, logger)
    {
        public override string? QuitMessage => null;

        protected override bool IndeterminateOnStart => false;

        protected override IReadOnlyList<string> StepNames() => ["Only step"];

        protected override Task RunCoreAsync(IProgress<StepProgress> progress, Action<StepEvent> onEvent, CancellationToken cancellationToken) => Task.CompletedTask;

        protected override (string Summary, string? Details) SplitFailure(Exception exception) => (exception.Message, null);

        protected override Task OnSucceededAsync() => throw new InvalidOperationException("boom");

        protected override Task OnCancelledAsync() => Task.CompletedTask;

        protected override Task LeaveAsync() => Task.CompletedTask;
    }
}
```

`tests/PS5Kitsune.App.Tests/ReconvertViewModelTests.cs`:

```csharp
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.App.Tests;

public sealed class ReconvertViewModelTests
{
    private const string Kept = "The previous conversion was kept; the game runs as before.";

    private readonly FakeGameReconverter _reconverter = new();
    private readonly Navigator _navigator = new();
    private readonly List<bool> _busy = [];

    // Not navigated to: showing the page would start the run, and the tests start it themselves.
    private ReconvertViewModel Create() =>
        new(_reconverter, "PPSA99666", "DOOM", "app.log", new TestPlatform(), new InlineDispatcher(), _navigator, new FakePages(), _busy.Add);

    [Fact]
    public void Lists_the_steps_and_names_the_game()
    {
        var viewModel = Create();

        Assert.Equal("Reconverting DOOM", viewModel.Title);
        Assert.Equal(_reconverter.StepNames, viewModel.Steps.Select(step => step.Name));
        Assert.Equal("A Reconvert is in progress. Quitting cancels it and puts the previous conversion back.", viewModel.QuitMessage);
    }

    [Fact]
    public async Task A_successful_reconvert_opens_the_result()
    {
        var viewModel = Create();

        await viewModel.RunAsync();

        Assert.Equal(["PPSA99666"], _reconverter.Reconverted);
        Assert.Equal("reconverted:PPSA99666", _navigator.CurrentPage);
        Assert.Equal([true, false], _busy);
    }

    [Fact]
    public async Task A_failed_step_says_the_previous_conversion_was_kept_and_can_be_retried()
    {
        _reconverter.Reconvert = (_, _, _, _) =>
            throw new StepFailedException("Convert with the relinker", new GameImportException("The relinker failed with exit code 2.", "error: bad instruction"));
        var viewModel = Create();

        await viewModel.RunAsync();

        Assert.Equal("Convert with the relinker failed: The relinker failed with exit code 2." + Environment.NewLine + Kept, viewModel.Failure);
        Assert.Equal("error: bad instruction", viewModel.FailureDetails);
        Assert.True(viewModel.CanRetry);

        _reconverter.Reconvert = (_, _, _, _) => Task.FromResult(TestGameData.Reconverted());
        await viewModel.RetryCommand.ExecuteAsync(null);
        Assert.Equal("reconverted:PPSA99666", _navigator.CurrentPage);
    }

    [Fact]
    public async Task A_failed_restore_says_when_it_will_be_restored()
    {
        _reconverter.Reconvert = (_, _, _, _) => throw new ReconvertRestoreException(
            new StepFailedException("Audit the imports", new GameImportException("import_audit.py could not read its input (exit code 2).")),
            new IOException("in use"));
        var viewModel = Create();

        await viewModel.RunAsync();

        Assert.Equal(
            "Audit the imports failed: import_audit.py could not read its input (exit code 2)." + Environment.NewLine
                + "The previous conversion could not be restored yet; it will be restored on the next start or Reconvert.",
            viewModel.Failure);
    }

    [Fact]
    public async Task Without_an_install_it_cannot_be_retried()
    {
        _reconverter.Reconvert = (_, _, _, _) => throw new AnyPS5NotInstalledException();
        var viewModel = Create();

        await viewModel.RunAsync();

        Assert.Equal("AnyPS5 is not installed. Install it from Home first." + Environment.NewLine + Kept, viewModel.Failure);
        Assert.False(viewModel.CanRetry);
    }

    [Fact]
    public async Task Cancelling_goes_home()
    {
        _reconverter.Reconvert = (_, _, _, _) => Task.FromCanceled<GameReconverted>(new CancellationToken(canceled: true));
        var viewModel = Create();

        await viewModel.RunAsync();

        Assert.Equal("home", _navigator.CurrentPage);
    }

    [Fact]
    public async Task Closing_the_window_cancels_and_waits_for_the_restore()
    {
        var restored = false;
        _reconverter.Reconvert = async (_, _, _, token) =>
        {
            try
            {
                await Task.Delay(Timeout.Infinite, token);
            }
            finally
            {
                restored = true;
            }

            return TestGameData.Reconverted();
        };
        var viewModel = Create();
        var running = viewModel.RunAsync();

        Assert.True(viewModel.IsRunning);
        await viewModel.CancelAndWaitAsync();

        Assert.True(restored);
        await running;
    }
}
```

In `tests/PS5Kitsune.App.Tests/AddResultViewModelTests.cs`, add (inside the class; it already builds `AddResultViewModel` with a `TestPlatform`, a `Navigator` and `FakePages`):

```csharp
    [Fact]
    public void A_reconverted_game_shows_the_counts_before()
    {
        var viewModel = new AddResultViewModel(TestGameData.Reconverted(), new TestPlatform(), new Navigator(), new FakePages());

        Assert.Equal("DOOM reconverted", viewModel.Title);
        Assert.Equal("361 implemented, 3 stubs, 0 absent", viewModel.CountsText);
        Assert.Equal("Before: 360 implemented, 4 stubs, 0 absent", viewModel.BeforeText);
    }

    [Fact]
    public void An_added_game_has_no_before_line()
    {
        Assert.Null(new AddResultViewModel(TestGameData.Added(), new TestPlatform(), new Navigator(), new FakePages()).BeforeText);
    }
```

In `tests/PS5Kitsune.App.Tests/PageFactoryTests.cs`, add before the closing brace of the test:

```csharp
        Assert.Equal(6, Assert.IsType<ReconvertViewModel>(pages.Reconvert("PPSA99666", "DOOM")).Steps.Count);
        Assert.IsType<AddResultViewModel>(pages.ReconvertResult(TestGameData.Reconverted()));
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build errors: `ReconvertViewModel`, `IPages.Reconvert`, the `StepRunViewModel` constructor with a logger, `AddResultViewModel(GameReconverted …)` do not exist.

- [ ] **Step 4: `StepRunViewModel` — optional logger, hook failures**

In `src/PS5Kitsune.App/Shell/AppLog.cs`, add:

```csharp
    [LoggerMessage(Level = LogLevel.Error, Message = "A page failed after its steps finished.")]
    public static partial void StepRunHookFailed(this ILogger logger, Exception exception);
```

In `src/PS5Kitsune.App/ViewModels/StepRunViewModel.cs`:
- add `using Microsoft.Extensions.Logging;` and `using PS5Kitsune.App.Shell;`;
- add the field `private readonly ILogger? _logger;`;
- change the constructor to:

```csharp
    /// <param name="busyChanged">Told true when the run starts and false when it stops, so the data root cannot change meanwhile.</param>
    /// <param name="logger">Logs a success or cancel hook that throws.</param>
    protected StepRunViewModel(string logFile, IPlatform platform, IUiDispatcher ui, Action<bool>? busyChanged, ILogger? logger = null)
    {
        _logger = logger;
        _logFile = logFile;
        _platform = platform;
        Ui = ui;
        _busyChanged = busyChanged;
    }
```

- replace the end of `RunLoopAsync` (the `if (succeeded == true) … else if (succeeded == false) …` block) with:

```csharp
        // A hook navigates or releases resources; an exception there must reach the user, not vanish in a discarded task.
        try
        {
            if (succeeded == true)
            {
                await OnSucceededAsync().ConfigureAwait(true);
            }
            else if (succeeded == false)
            {
                await OnCancelledAsync().ConfigureAwait(true);
            }
        }
        catch (Exception exception)
        {
            _logger?.StepRunHookFailed(exception);
            Ui.Post(() => Fail($"Something went wrong after the steps: {exception.Message}", null, canRetry: false));
        }
```

In `InstallViewModel` and `AddGameViewModel`, add `ILogger? logger = null` as the last constructor parameter (with `using Microsoft.Extensions.Logging;`) and pass it to `base(logFile, platform, ui, busyChanged, logger)`.

- [ ] **Step 5: `ReconvertViewModel`**

`src/PS5Kitsune.App/ViewModels/ReconvertViewModel.cs`:

```csharp
using Microsoft.Extensions.Logging;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.App.ViewModels;

/// <summary>Reconverts an added game when shown. Core puts the previous conversion back after a failure or a cancel.</summary>
public sealed partial class ReconvertViewModel : StepRunViewModel
{
    private const string Kept = "The previous conversion was kept; the game runs as before.";
    private const string NotRestored = "The previous conversion could not be restored yet; it will be restored on the next start or Reconvert.";

    private readonly IGameReconverter _reconverter;
    private readonly string _titleId;
    private readonly Navigator _navigator;
    private readonly IPages _pages;
    private GameReconverted? _reconverted;

    public ReconvertViewModel(
        IGameReconverter reconverter, string titleId, string name, string logFile, IPlatform platform, IUiDispatcher ui, Navigator navigator, IPages pages,
        Action<bool>? busyChanged = null, ILogger? logger = null)
        : base(logFile, platform, ui, busyChanged, logger)
    {
        _reconverter = reconverter;
        _titleId = titleId;
        _navigator = navigator;
        _pages = pages;
        Title = $"Reconverting {name}";
        foreach (var stepName in StepNames())
        {
            Steps.Add(new InstallStepViewModel(stepName));
        }
    }

    public string Title { get; }

    public override string? QuitMessage { get; } = "A Reconvert is in progress. Quitting cancels it and puts the previous conversion back.";

    /// <summary>Copying libraries, converting and auditing report nothing measurable.</summary>
    protected override bool IndeterminateOnStart => true;

    protected override IReadOnlyList<string> StepNames() => _reconverter.StepNames;

    protected override async Task RunCoreAsync(IProgress<StepProgress> progress, Action<StepEvent> onEvent, CancellationToken cancellationToken) =>
        _reconverted = await _reconverter.ReconvertAsync(_titleId, progress, onEvent, cancellationToken).ConfigureAwait(true);

    protected override (string Summary, string? Details) SplitFailure(Exception exception)
    {
        var (failure, outcome) = exception is ReconvertRestoreException { InnerException: { } inner } ? (inner, NotRestored) : (exception, Kept);
        var (summary, details) = Describe(failure);
        return (summary + Environment.NewLine + outcome, details);
    }

    protected override bool CanRetryAfter(Exception exception) => exception is not (AnyPS5NotInstalledException or GameNotFoundException);

    protected override Task OnSucceededAsync()
    {
        _navigator.GoTo(_pages.ReconvertResult(_reconverted!));
        return Task.CompletedTask;
    }

    protected override Task OnCancelledAsync()
    {
        _navigator.GoHome(_pages.Home());
        return Task.CompletedTask;
    }

    protected override Task LeaveAsync()
    {
        _navigator.GoHome(_pages.Home());
        return Task.CompletedTask;
    }

    private static (string Summary, string? Details) Describe(Exception exception)
    {
        if (exception is AnyPS5NotInstalledException)
        {
            return ("AnyPS5 is not installed. Install it from Home first.", null);
        }

        if (exception is StepFailedException { InnerException: GameImportException failure } failed)
        {
            return ($"{failed.StepName} failed: {failure.Summary}", string.IsNullOrEmpty(failure.Details) ? null : failure.Details);
        }

        if (exception is StepFailedException other)
        {
            return ($"{other.StepName} failed: {other.InnerException?.Message ?? other.Message}", null);
        }

        return ($"Reconverting the game failed: {exception.Message}", null);
    }
}
```

- [ ] **Step 6: `ReconvertView`**

`src/PS5Kitsune.App/Views/ReconvertView.axaml`: a copy of `AddGameView.axaml` with `x:Class="PS5Kitsune.App.Views.ReconvertView"` and `x:DataType="vm:ReconvertViewModel"`; nothing else changes (the "kept" sentence is part of `Failure`).

`src/PS5Kitsune.App/Views/ReconvertView.axaml.cs`:

```csharp
using Avalonia.Controls;
using PS5Kitsune.App.ViewModels;

namespace PS5Kitsune.App.Views;

public sealed partial class ReconvertView : UserControl
{
    public ReconvertView() => InitializeComponent();

    /// <summary>Keeps the newest log line in view.</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is ReconvertViewModel viewModel)
        {
            viewModel.Log.CollectionChanged += (_, _) => LogScroller.ScrollToEnd();
        }
    }
}
```

In `src/PS5Kitsune.App/Views/MainWindow.axaml`, after the `AddGameViewModel` template:

```xml
    <DataTemplate DataType="vm:ReconvertViewModel">
      <views:ReconvertView />
    </DataTemplate>
```

- [ ] **Step 7: The result page**

Replace `src/PS5Kitsune.App/ViewModels/AddResultViewModel.cs` with:

```csharp
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.App.ViewModels;

/// <summary>The audit verdict of the game just added or reconverted, with what AnyPS5 does not implement yet.</summary>
public sealed partial class AddResultViewModel : ObservableObject
{
    private readonly AuditSummary _audit;
    private readonly string _gameDirectory;
    private readonly IPlatform _platform;
    private readonly Navigator _navigator;
    private readonly IPages _pages;

    public AddResultViewModel(GameAdded added, IPlatform platform, Navigator navigator, IPages pages)
        : this($"{added.Record.Name} is added", added.GameDirectory, added.Audit, added.Warnings, null, platform, navigator, pages)
    {
    }

    public AddResultViewModel(GameReconverted reconverted, IPlatform platform, Navigator navigator, IPages pages)
        : this($"{reconverted.Record.Name} reconverted", reconverted.GameDirectory, reconverted.Audit, [], Before(reconverted.PreviousAudit), platform, navigator, pages)
    {
    }

    private AddResultViewModel(
        string title, string gameDirectory, AuditSummary audit, IReadOnlyList<ValidationIssue> warnings, string? beforeText, IPlatform platform, Navigator navigator, IPages pages)
    {
        Title = title;
        _gameDirectory = gameDirectory;
        _audit = audit;
        BeforeText = beforeText;
        _platform = platform;
        _navigator = navigator;
        _pages = pages;
        UnresolvedLines = [.. audit.Unresolved.Select(Line)];
        Warnings = [.. warnings.Select(issue => issue.Message)];
    }

    public string Title { get; }

    public AuditVerdict Verdict => _audit.Verdict;

    public bool IsGreen => Verdict == AuditVerdict.Green;

    public bool IsOrange => Verdict == AuditVerdict.Orange;

    public bool IsRed => Verdict == AuditVerdict.Red;

    public string VerdictText => Verdict switch
    {
        AuditVerdict.Green => "Green: the game can load.",
        AuditVerdict.Orange => "Orange: the game loads but will stop if it calls an unimplemented function.",
        _ => "Red: not playable yet; wait for an AnyPS5 update.",
    };

    public string CountsText => string.Create(CultureInfo.InvariantCulture, $"{_audit.Implemented} implemented, {_audit.Stub} stubs, {_audit.Absent} absent");

    /// <summary>The counts before a Reconvert; null after an add.</summary>
    public string? BeforeText { get; }

    public string? MissingLibrariesText => _audit.MissingLibraries.Count == 0
        ? null
        : "Missing libraries: " + string.Join(", ", _audit.MissingLibraries.Keys);

    public bool HasUnresolved => _audit.Unresolved.Count > 0;

    public string UnresolvedHeader => string.Create(CultureInfo.InvariantCulture, $"Show the unresolved functions ({_audit.Unresolved.Count})");

    public IReadOnlyList<string> UnresolvedLines { get; }

    public IReadOnlyList<string> Warnings { get; }

    [ObservableProperty]
    public partial string? OpenError { get; private set; }

    private static string Before(AuditRecord audit) =>
        string.Create(CultureInfo.InvariantCulture, $"Before: {audit.Implemented} implemented, {audit.Stub} stubs, {audit.Absent} absent");

    private static string Line(UnresolvedImport import)
    {
        var kind = import.Class == AuditClass.Stub ? "stub" : "absent";
        return import.Where.Length == 0 ? $"{import.Name} ({kind})" : $"{import.Name} — {import.Where} ({kind})";
    }

    [RelayCommand]
    private void OpenFolder() => OpenError = PathOpener.TryOpen(_platform, _gameDirectory);

    [RelayCommand]
    private void AddAnother()
    {
        _navigator.GoHome(_pages.Home());
        _navigator.GoTo(_pages.ChooseGame());
    }

    [RelayCommand]
    private void BackHome() => _navigator.GoHome(_pages.Home());
}
```

In `src/PS5Kitsune.App/Views/AddResultView.axaml`, right after the `CountsText` TextBlock:

```xml
        <TextBlock Text="{Binding BeforeText}" Opacity="0.8" IsVisible="{Binding BeforeText, Converter={x:Static ObjectConverters.IsNotNull}}" />
```

- [ ] **Step 8: Pages**

In `src/PS5Kitsune.App/Navigation/IPages.cs`, add:

```csharp
    object Reconvert(string titleId, string name);

    object ReconvertResult(GameReconverted reconverted);
```

In `src/PS5Kitsune.App/Shell/PageFactory.cs`, add the two pages, and pass the logger to the step-run pages:

```csharp
    public object Install(AnyPS5Mode mode) =>
        new InstallViewModel(Session.Get<IAnyPS5Installer>(), mode, Session.LogFile, platform, ui, navigator, this, busy => sessions.IsBusy = busy, Session.Get<ILogger<StepRunViewModel>>());

    public object AddGame(PreparedGame game, bool toIntel) =>
        new AddGameViewModel(Session.Get<IGameImporter>(), game, toIntel, Session.LogFile, platform, ui, navigator, this, busy => sessions.IsBusy = busy, Session.Get<ILogger<StepRunViewModel>>());

    public object Reconvert(string titleId, string name) =>
        new ReconvertViewModel(Session.Get<IGameReconverter>(), titleId, name, Session.LogFile, platform, ui, navigator, this, busy => sessions.IsBusy = busy, Session.Get<ILogger<StepRunViewModel>>());

    public object ReconvertResult(GameReconverted reconverted) => new AddResultViewModel(reconverted, platform, navigator, this);
```

(`Install` and `AddGame` replace the existing members.)

- [ ] **Step 9: Run the tests**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: 0 failed, and the build has 0 warnings.

- [ ] **Step 10: Commit**

```bash
git add src/PS5Kitsune.App tests/PS5Kitsune.App.Tests
git commit -m "Reconvert a game from the app and show what changed

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 6: App — Home's games to reconvert, Summary's Reconvert

**Files:**
- Create: `src/PS5Kitsune.App/ViewModels/GameToReconvert.cs`
- Modify: `src/PS5Kitsune.App/ViewModels/HomeViewModel.cs`, `src/PS5Kitsune.App/Views/HomeView.axaml`
- Modify: `src/PS5Kitsune.App/ViewModels/GameSummaryViewModel.cs`, `src/PS5Kitsune.App/Views/GameSummaryView.axaml`, `src/PS5Kitsune.App/Shell/PageFactory.cs`
- Modify: `tests/PS5Kitsune.App.Tests/Fakes.cs`, `tests/PS5Kitsune.App.Tests/HomeViewModelTests.cs`, `tests/PS5Kitsune.App.Tests/GameSummaryViewModelTests.cs`

**Interfaces:**
- Consumes: `GameLibrary.All`, `GameLibrary.Find`, `GameLibrary.NeedsReconversion`, `ConversionLabels.Of` (Task 2); `IPages.Reconvert(string titleId, string name)` (Task 5).
- Produces: `public sealed record GameToReconvert(string TitleId, string Name, string Detail)`; `HomeViewModel.GamesToReconvert`, `HasGamesToReconvert`, `ReconvertCommand` (parameter `GameToReconvert`); `GameSummaryViewModel(PreparedGame game, bool cpuIsIntel, IPlatform platform, Navigator navigator, IPages pages, string? conversionText = null)` with `ConversionText`, `ReconvertCommand`, `static string ConversionLine(GameRecord record, AnyPS5InstallRecord? install)`.

- [ ] **Step 1: Sessions in tests get a library**

In `tests/PS5Kitsune.App.Tests/Fakes.cs`, in `TestSessions.Create`, register a real `GameLibrary` over the root (add `using Microsoft.Extensions.Logging.Abstractions;`):

```csharp
        services.AddSingleton(new GameLibrary(new DataLayout(root), new TestPlatform(), TimeProvider.System, NullLogger<GameLibrary>.Instance));
```

- [ ] **Step 2: Write the failing tests**

In `tests/PS5Kitsune.App.Tests/HomeViewModelTests.cs`, add `using PS5Kitsune.Core.Games;` and `using PS5Kitsune.Core.Library;`, then add inside the class:

```csharp
    private Task WriteGameAsync(string titleId, string name, ConversionInfo convertedWith) => GameRecordStore.WriteAsync(
        Path.Combine(_temp.Combine("root"), "games", titleId, ".ps5kitsune", "game.json"),
        new GameRecord
        {
            SchemaVersion = GameRecord.CurrentSchemaVersion,
            TitleId = titleId,
            Name = name,
            Executable = titleId + ".exe",
            ConvertedWith = convertedWith,
            Audit = new AuditRecord(360, 4, 0, AuditVerdict.Orange),
            AddedAt = DateTimeOffset.UnixEpoch,
        },
        TestContext.Current.CancellationToken);

    [Fact]
    public async Task Lists_the_games_converted_with_another_install()
    {
        _installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, Record);
        await WriteGameAsync("PPSA99666", "DOOM", new ConversionInfo("test-profile", "build", "3fd1cc3", new string('3', 40), false));
        await WriteGameAsync("PPSA00001", "Up to date", new ConversionInfo("test-profile", "release", "v0.1.2", null, false));

        var viewModel = await CreateAsync();

        Assert.True(viewModel.HasGamesToReconvert);
        var game = Assert.Single(viewModel.GamesToReconvert);
        Assert.Equal(new GameToReconvert("PPSA99666", "DOOM", "Converted with build 3fd1cc3 · in use: release v0.1.2"), game);
        Assert.True(viewModel.ReconvertCommand.CanExecute(game));

        viewModel.ReconvertCommand.Execute(game);

        Assert.Equal("reconvert:PPSA99666", _navigator.CurrentPage);
    }

    [Fact]
    public async Task No_game_to_reconvert_hides_the_section()
    {
        _installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, Record);

        var viewModel = await CreateAsync();

        Assert.False(viewModel.HasGamesToReconvert);
        Assert.Empty(viewModel.GamesToReconvert);
    }

    [Fact]
    public async Task Without_an_install_no_game_is_listed_and_reconvert_is_disabled()
    {
        await WriteGameAsync("PPSA99666", "DOOM", new ConversionInfo("test-profile", "build", "3fd1cc3", new string('3', 40), false));

        var viewModel = await CreateAsync();

        Assert.Empty(viewModel.GamesToReconvert);
        Assert.False(viewModel.ReconvertCommand.CanExecute(new GameToReconvert("PPSA99666", "DOOM", "")));
    }
```

In `tests/PS5Kitsune.App.Tests/GameSummaryViewModelTests.cs`, add `using PS5Kitsune.Core.AnyPS5;` and `using PS5Kitsune.Core.Library;`, then add inside the class:

```csharp
    [Fact]
    public async Task An_added_game_can_be_reconverted()
    {
        var game = TestGameData.Prepared(alreadyAdded: true);
        _navigator.GoTo("choose");
        var viewModel = new GameSummaryViewModel(game, false, _platform, _navigator, new FakePages(), "Converted with the AnyPS5 in use.");
        _navigator.GoTo(viewModel);

        Assert.Equal("Converted with the AnyPS5 in use.", viewModel.ConversionText);
        Assert.True(viewModel.ReconvertCommand.CanExecute(null));

        await viewModel.ReconvertCommand.ExecuteAsync(null);

        Assert.True(game.IsDisposed);
        Assert.Equal("reconvert:PPSA99666", _navigator.CurrentPage);
        Assert.False(viewModel.BackCommand.CanExecute(null));
    }

    [Fact]
    public void A_new_game_cannot_be_reconverted()
    {
        var viewModel = Create(TestGameData.Prepared());

        Assert.Null(viewModel.ConversionText);
        Assert.False(viewModel.ReconvertCommand.CanExecute(null));
    }

    [Fact]
    public void The_conversion_line_compares_with_the_install_in_use()
    {
        var record = TestGameData.Added().Record with { ConvertedWith = new ConversionInfo("test-profile", "build", "3fd1cc3", new string('3', 40), false) };
        var release = new AnyPS5InstallRecord(2, "test-profile", AnyPS5Mode.Release, "v0.1.2", null, null, DateTimeOffset.UnixEpoch);
        var build = new AnyPS5InstallRecord(2, "test-profile", AnyPS5Mode.Build, "3fd1cc3", new string('3', 40), null, DateTimeOffset.UnixEpoch);

        Assert.Equal("Converted with build 3fd1cc3; the AnyPS5 in use is release v0.1.2.", GameSummaryViewModel.ConversionLine(record, release));
        Assert.Equal("Converted with the AnyPS5 in use.", GameSummaryViewModel.ConversionLine(record, build));
        Assert.Equal("Converted with build 3fd1cc3.", GameSummaryViewModel.ConversionLine(record, null));
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build errors: `GameToReconvert`, `GamesToReconvert`, `ReconvertCommand`, `ConversionText`, `ConversionLine` do not exist.

- [ ] **Step 4: `GameToReconvert` and Home**

`src/PS5Kitsune.App/ViewModels/GameToReconvert.cs`:

```csharp
namespace PS5Kitsune.App.ViewModels;

/// <summary>A row of Home's "Games to reconvert".</summary>
/// <param name="Detail">"Converted with {label} · in use: {label}".</param>
public sealed record GameToReconvert(string TitleId, string Name, string Detail);
```

In `src/PS5Kitsune.App/ViewModels/HomeViewModel.cs` (add `using PS5Kitsune.Core.Library;`):

- add `ReconvertCommand` to both `NotifyCanExecuteChangedFor` attributes (on `Status` and on `IsChecking`): `[NotifyCanExecuteChangedFor(nameof(AddGameCommand), nameof(ReconvertCommand))]` and `[NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(AddGameCommand), nameof(ReconvertCommand))]`;
- add the property:

```csharp
    /// <summary>Games converted with another AnyPS5 than the one in use; empty hides the section.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGamesToReconvert))]
    public partial IReadOnlyList<GameToReconvert> GamesToReconvert { get; private set; } = [];

    public bool HasGamesToReconvert => GamesToReconvert.Count > 0;
```

- in `RefreshAsync`, read the library with the installer (`var library = session.Get<GameLibrary>();` after `var installer = …`), call `Task.Run(() => Check(installer, library))`, set `GamesToReconvert = [];` in the `catch` block (inside `if (generation == _generation)`), and after `ApplyBothInstalls(check);` add `GamesToReconvert = check.ToReconvert;`;
- extend `HomeCheck` with a last parameter `IReadOnlyList<GameToReconvert> ToReconvert` (and its `<param>` doc: "Games converted with another install than the one in use; empty unless AnyPS5 is installed."), and `Check`:

```csharp
    /// <summary>Runs on a worker thread: the migration moves a folder, the done-checks can hash hundreds of MB, and the library reads every game.json.</summary>
    private static HomeCheck Check(IAnyPS5Installer installer, GameLibrary library)
    {
        installer.MigrateLegacyInstall();
        var active = installer.ActiveMode;
        var requested = installer.RequestedMode;
        var mode = active ?? requested ?? AnyPS5Mode.Release;
        var other = active is { } activeMode ? installer.ReadRecord(OtherMode(activeMode)) : null;
        var state = installer.GetState(mode);
        IReadOnlyList<GameToReconvert> toReconvert = state.Status == AnyPS5InstallStatus.Installed && installer.ReadRecord() is { } install
            ? [.. library.All()
                .Where(entry => GameLibrary.NeedsReconversion(entry.Record, install))
                .Select(entry => new GameToReconvert(
                    entry.Record.TitleId, entry.Record.Name, $"Converted with {ConversionLabels.Of(entry.Record.ConvertedWith)} · in use: {ConversionLabels.Of(install)}"))]
            : [];
        return new HomeCheck(mode, state, active, requested, other, toReconvert);
    }
```

- add the command:

```csharp
    [RelayCommand(CanExecute = nameof(CanAddGame))]
    private void Reconvert(GameToReconvert game) => navigator.GoTo(pages.Reconvert(game.TitleId, game.Name));
```

In `src/PS5Kitsune.App/Views/HomeView.axaml`, between the install `Border` and the "Data folder" `StackPanel`:

```xml
    <StackPanel Spacing="8" IsVisible="{Binding HasGamesToReconvert}">
      <TextBlock Text="Games to reconvert" FontWeight="SemiBold" />
      <ItemsControl ItemsSource="{Binding GamesToReconvert}">
        <ItemsControl.ItemTemplate>
          <DataTemplate x:DataType="vm:GameToReconvert">
            <Grid ColumnDefinitions="*,Auto" ColumnSpacing="8" Margin="0,4">
              <StackPanel>
                <TextBlock Text="{Binding Name}" FontWeight="SemiBold" />
                <TextBlock Text="{Binding TitleId}" Opacity="0.85" />
                <TextBlock Text="{Binding Detail}" Opacity="0.85" TextWrapping="Wrap" />
              </StackPanel>
              <Button Grid.Column="1" VerticalAlignment="Center" Content="Reconvert"
                      Command="{Binding $parent[ItemsControl].((vm:HomeViewModel)DataContext).ReconvertCommand}" CommandParameter="{Binding}" />
            </Grid>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
    </StackPanel>
```

- [ ] **Step 5: Summary**

In `src/PS5Kitsune.App/ViewModels/GameSummaryViewModel.cs` (add `using PS5Kitsune.Core.AnyPS5;` and `using PS5Kitsune.Core.Library;`):

- add `string? conversionText = null` as the last primary-constructor parameter, with the doc `/// <param name="conversionText">For an added game, whether it was converted with the AnyPS5 in use (<see cref="ConversionLine"/>).</param>`;
- add:

```csharp
    /// <summary>For an already added game, how its conversion compares with the AnyPS5 in use; null otherwise.</summary>
    public string? ConversionText { get; } = conversionText;

    public bool CanReconvert => game.AlreadyAdded && !game.IsDisposed;

    public static string ConversionLine(GameRecord record, AnyPS5InstallRecord? install)
    {
        ArgumentNullException.ThrowIfNull(record);
        var converted = ConversionLabels.Of(record.ConvertedWith);
        return install is null ? $"Converted with {converted}."
            : GameLibrary.NeedsReconversion(record, install) ? $"Converted with {converted}; the AnyPS5 in use is {ConversionLabels.Of(install)}."
            : "Converted with the AnyPS5 in use.";
    }
```

- in `BackAsync`, after `BackCommand.NotifyCanExecuteChanged();`, add `ReconvertCommand.NotifyCanExecuteChanged();`;
- add the command:

```csharp
    /// <summary>The prepared game (and a zip's staging folder) is released first: Reconvert works on the added game's folder.</summary>
    [RelayCommand(CanExecute = nameof(CanReconvert))]
    private async Task ReconvertAsync()
    {
        var disposing = game.DisposeAsync();
        AddCommand.NotifyCanExecuteChanged();
        BackCommand.NotifyCanExecuteChanged();
        ReconvertCommand.NotifyCanExecuteChanged();
        await disposing.ConfigureAwait(true);
        navigator.GoTo(pages.Reconvert(TitleId, Name));
    }
```

In `src/PS5Kitsune.App/Views/GameSummaryView.axaml`, replace the already-added `StackPanel` with:

```xml
        <StackPanel Spacing="8" IsVisible="{Binding AlreadyAddedText, Converter={x:Static ObjectConverters.IsNotNull}}">
          <TextBlock Text="{Binding AlreadyAddedText}" TextWrapping="Wrap" />
          <TextBlock Text="{Binding ConversionText}" TextWrapping="Wrap" Opacity="0.85"
                     IsVisible="{Binding ConversionText, Converter={x:Static ObjectConverters.IsNotNull}}" />
          <StackPanel Orientation="Horizontal" Spacing="8">
            <Button Content="Open its folder" Command="{Binding OpenFolderCommand}" />
            <Button Content="Reconvert" Command="{Binding ReconvertCommand}" />
          </StackPanel>
        </StackPanel>
```

In `src/PS5Kitsune.App/Shell/PageFactory.cs` (add `using PS5Kitsune.Core.Library;`), replace `GameSummary` with:

```csharp
    public object GameSummary(PreparedGame game) =>
        new GameSummaryViewModel(game, Session.Get<ICpuInfo>().Read().IsIntel, platform, navigator, this, ConversionText(game));

    /// <summary>How an already added game's conversion compares with the install in use; null for a new game or an unreadable record.</summary>
    private string? ConversionText(PreparedGame game)
    {
        if (!game.AlreadyAdded || game.Metadata is not { } metadata)
        {
            return null;
        }

        try
        {
            var (_, record) = Session.Get<GameLibrary>().Find(metadata.TitleId);
            return GameSummaryViewModel.ConversionLine(record, Session.Get<IAnyPS5Installer>().ReadRecord());
        }
        catch (Exception exception) when (exception is GameNotFoundException or InvalidGameRecordException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
```

- [ ] **Step 6: Run the tests**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: 0 failed, 0 build warnings.

- [ ] **Step 7: Commit**

```bash
git add src/PS5Kitsune.App tests/PS5Kitsune.App.Tests
git commit -m "Offer Reconvert from Home and from an added game's summary

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 7: Docs

**Files:**
- Modify: `docs/architecture.md`, `README.md`

- [ ] **Step 1: `docs/architecture.md`**

- "V2 — Add a game", the Reconvert paragraph ("A **Reconvert** action reruns steps 5 to 9 …"): keep it, and add after it: "Before rebuilding, the conversion outputs (the executable, the relinked marker, `audit.json`, the `*.registry.json` and `*.needed.json` files, `libs\` and `app0\**\*.guest.prx`) are moved into `.ps5kitsune\previous\`; they are deleted once the new `game.json` is about to be written, and moved back if the Reconvert fails, is cancelled or the process dies (`.ps5kitsune\previous\` found on the next read of the game). A running game stops it before anything moves. Available as `ps5kitsune reconvert <titleId>`, from Home's "Games to reconvert" and from the summary of an already added game."
- "Screens" of V2: add "5. Reconverting: the Reconvert steps with progress and log, Cancel and Retry; the result page shows the counts before. Home lists the games converted with another install than the one in use."
- "Game record (`game.json`)": add `"reconvertedAt": null` after `"lastPlayedAt": null` in the example, and a sentence: "`reconvertedAt` is set by Reconvert, which also rewrites `convertedWith` and `audit` and keeps everything else."
- Roadmap entry 6b: add the spec link written like 6a's: `Spec: [milestone 6b](superpowers/specs/2026-10-23-milestone-6b-reconvert-design.md).`

- [ ] **Step 2: `README.md`**

Where the README describes `ps5kitsune add` and the app's "Add a game", add: "After an AnyPS5 update, `ps5kitsune reconvert <titleId>` (or Reconvert in the app) converts a game again without copying it, keeping its saves; a failure puts the previous conversion back." Update the status line to say milestone 6b is in progress, written like 6a's.

- [ ] **Step 3: Commit**

```bash
git add docs/architecture.md README.md
git commit -m "Document Reconvert

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```
