# Milestone 6a — Add a game from the app Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a game from the app — choose a folder or zip, see its summary before anything is copied, run the add with progress, read the verdict with the unresolved functions.

**Architecture:** Core splits the add into `PrepareAsync` (resolve + validate, returns an `IAsyncDisposable` `PreparedGame` that owns the zip's staging folder) and `AddAsync(PreparedGame, …)`, behind `IGameImporter`. The app gains four pages (Choose, Summary, Adding, Result); Install's step-list logic moves into an abstract `StepRunViewModel` that Install and Adding inherit, and the window's close handling moves from `InstallViewModel` to two interfaces (`IRunningWork`, `IPreparedGameOwner`).

**Tech Stack:** .NET 10, C#, Avalonia 11 (compiled bindings), CommunityToolkit.Mvvm, xUnit v3 on Microsoft.Testing.Platform.

**Spec:** `docs/superpowers/specs/2026-10-22-milestone-6a-add-game-screens-design.md` (revision 1fbd6ff).

## Global Constraints

- Code, docs, commit messages and UI text in English.
- `TreatWarningsAsErrors`, `AnalysisLevel latest-recommended`, `EnforceCodeStyleInBuild`: 0 warnings. `[LoggerMessage]` for logging (CA1848); InvariantCulture when formatting numbers (CA1305); constant texts on view models are get-only properties with an initializer (CA1822); compute log arguments into locals before the call (CA1873).
- No new NuGet dependency.
- The CLI's `add` output does not change; `GameImportException.Message` stays `Summary + newline + Details` when there are details, else `Summary`.
- App texts, verbatim:
  - Home: "Add a game", "Install AnyPS5 first to add games."
  - Choose: "Add a game", "Choose the game's folder or its .zip, in the DOOM for PS5 format, or drop it here.", "Choose a folder…", "Choose a .zip…", "Reading the game…", "Cancel", "Back"; dialog titles "Choose the game's folder", "Choose the game's .zip".
  - Summary: "{n} MB" (bytes / 1048576, one decimal), "No module folder" / "1 module folder" / "{n} module folders", "No SELF file." / "1 file is SELF and will be unwrapped." / "{n} files are SELF and will be unwrapped." with ", eboot.bin among them" before the final period when there are several SELF files and eboot.bin is one of them, "{titleId} is already in your games ({folder}).", "Open its folder", "A previous add of this game stopped; adding it resumes where it stopped. The Intel option only applies if the conversion has not run yet.", "Convert for an Intel CPU", "Back", "Add the game".
  - Adding: quit message "Adding a game is in progress. Quitting cancels it; adding it again resumes where it stopped."; not installed "AnyPS5 is not installed. Install it from Home first."; failure "{Step} failed: {Summary}"; other failure "Adding the game failed: {message}".
  - Result: "Green: the game can load.", "Orange: the game loads but will stop if it calls an unimplemented function.", "Red: not playable yet; wait for an AnyPS5 update.", "{implemented} implemented, {stub} stubs, {absent} absent", "Missing libraries: {a}, {b}", "Show the unresolved functions ({n})", line "{name} — {where} ({stub|absent})" (or "{name} ({stub|absent})" when where is empty), "Open the game folder", "Add another game", "Back to home".
- Colours: red `#C42B1C` (as Home's errors), orange `#9D5D00`, green `#0F7B0F`.
- Tests never use the network, the compiler or Python.
- Build: `dotnet build PS5Kitsune.slnx -c Release`. Tests: `dotnet test --solution PS5Kitsune.slnx -c Release`; one class: `-- --filter-class "<full class name>"` (a filtered run exits with code 8: not a failure). Run commands through the Bash tool.
- Commit locally after each task, author La-Kiks, message ending with `Co-Authored-By: <your model name> <noreply@anthropic.com>`. Do not push. Never add or run an `install.cmd`.

## Refinements of the spec

1. `PreparedGame` also exposes `IsDisposed` (for tests and for the pages' guards).
2. `GameImportException` keeps the exit code in `Summary` (as today's first line) and puts the output tail in `Details`; `Message` is unchanged.
3. `StepRunViewModel`'s page-specific hooks are `StepNames()`, `RunCoreAsync`, `SplitFailure`, `CanRetryAfter`, `OnSucceededAsync`, `OnCancelledAsync`, `LeaveAsync` (Back to home), `IndeterminateOnStart` and `QuitMessage`. Install keeps posting its navigation through the dispatcher, as today.
4. The icon is shown through a `PathToBitmapConverter` in the view layer that reads the file into memory and returns null (placeholder) for a missing or invalid image.

## File map

| File | Change |
| --- | --- |
| `src/PS5Kitsune.Core/Games/GameImportException.cs` | `Summary`, `Details` |
| `src/PS5Kitsune.Core/Games/Steps/UnwrapSelfStep.cs`, `RelinkStep.cs`, `AuditStep.cs` | split messages |
| `src/PS5Kitsune.Core/Games/AuditSummary.cs` | `Unresolved`, `UnresolvedImport`, `AuditClass` |
| `src/PS5Kitsune.Core/Games/AddGameResult.cs` | `GameAdded.Audit` |
| `src/PS5Kitsune.Core/Games/PreparedGame.cs`, `IGameImporter.cs` | new |
| `src/PS5Kitsune.Core/Games/GameImporter.cs`, `GameSourceResolver.cs` | prepare/add split, `StagingFor` |
| `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs` | `IGameImporter` |
| `src/PS5Kitsune.App/Navigation/IRunningWork.cs`, `IPreparedGameOwner.cs` | new |
| `src/PS5Kitsune.App/ViewModels/StepRunViewModel.cs` | new base class |
| `src/PS5Kitsune.App/ViewModels/InstallViewModel.cs` | inherits the base |
| `src/PS5Kitsune.App/ViewModels/MainWindowViewModel.cs`, `Views/MainWindow.axaml(.cs)` | interfaces, templates |
| `src/PS5Kitsune.App/Navigation/IDialogService.cs`, `Shell/AvaloniaDialogService.cs` | `PickFileAsync` |
| `src/PS5Kitsune.App/Navigation/IPages.cs`, `Shell/PageFactory.cs` | four pages |
| `src/PS5Kitsune.App/ViewModels/HomeViewModel.cs`, `Views/HomeView.axaml` | "Add a game" |
| `src/PS5Kitsune.App/ViewModels/ChooseGameViewModel.cs`, `GameSummaryViewModel.cs`, `AddGameViewModel.cs`, `AddResultViewModel.cs` + views | new |
| `src/PS5Kitsune.App/Views/PathToBitmapConverter.cs` | new |
| `tests/PS5Kitsune.App.Tests/Fakes.cs` | `FakeGameImporter`, `TestGameData`, `FakeDialogs.File`, `FakePages` |
| `docs/architecture.md`, `README.md` | Task 6 |

---

### Task 1: Core — failure details, unresolved functions, audit in the result

**Files:**
- Modify: `src/PS5Kitsune.Core/Games/GameImportException.cs`, `Steps/UnwrapSelfStep.cs`, `Steps/RelinkStep.cs`, `Steps/AuditStep.cs`, `AuditSummary.cs`, `AddGameResult.cs`, `GameImporter.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Games/AuditSummaryTests.cs`, `UnwrapSelfStepTests.cs`, `RelinkStepTests.cs`, `AuditStepTests.cs`, `GameImporterTests.cs`, `AuditFiles.cs`

**Interfaces:**
- Produces: `GameImportException(string summary, string? details = null)` with `Summary`, `Details`; `enum AuditClass { Stub, Absent }`; `record UnresolvedImport(string Name, string Where, AuditClass Class)`; `AuditSummary.Unresolved` (`IReadOnlyList<UnresolvedImport>`, init, default empty); `GameAdded(GameRecord Record, string GameDirectory, IReadOnlyList<ValidationIssue> Warnings, AuditSummary Audit)`.

- [ ] **Step 1: Write the failing tests**

In `tests/PS5Kitsune.Core.Tests/Games/AuditFiles.cs`, add a fixture shaped like DOOM's audit.json:

```csharp
    /// <summary>import_audit.py --json output with an imports list: the 4 stub entries of DOOM for PS5 v1.1.1, one duplicated, one implemented.</summary>
    public static string WithImports() => """
        {
          "unique_by_class": { "implemented": 360, "stub": 4, "absent": 0, "module": 0 },
          "missing_libraries": {},
          "imports": [
            { "nid": "+BzXYkqYeLE", "library": null, "module": "libc.prx", "class": "implemented", "name": null },
            { "nid": "vNe1w4diLCs", "library": null, "module": "libc.prx", "class": "stub", "name": "__tls_get_addr_nid_postfix" },
            { "nid": "npLpPTaSuHg", "library": "libSceLibcInternal.prx", "class": "stub", "name": "fscanf_nid_postfix" },
            { "nid": "JoBqSQt1yyA", "library": "libSceSystemService.prx", "class": "stub", "name": "sceSystemServiceLoadExec" },
            { "nid": "vNe1w4diLCs", "library": "libkernel.prx", "class": "stub", "name": "__tls_get_addr_nid_postfix" },
            { "nid": "JoBqSQt1yyA", "library": "libSceSystemService.prx", "class": "stub", "name": "sceSystemServiceLoadExec" },
            { "nid": "AbCdEfGhIjk", "library": null, "class": "absent", "name": null }
          ]
        }
        """;
```

In `AuditSummaryTests.cs`, add:

```csharp
    [Fact]
    public void Lists_the_stub_and_absent_imports_once_each()
    {
        var path = _temp.Combine("audit.json");
        File.WriteAllText(path, AuditFiles.WithImports());

        var summary = AuditSummary.Read(path);

        Assert.Equal(
            [
                new UnresolvedImport("__tls_get_addr_nid_postfix", "libc.prx", AuditClass.Stub),
                new UnresolvedImport("fscanf_nid_postfix", "libSceLibcInternal.prx", AuditClass.Stub),
                new UnresolvedImport("sceSystemServiceLoadExec", "libSceSystemService.prx", AuditClass.Stub),
                new UnresolvedImport("__tls_get_addr_nid_postfix", "libkernel.prx", AuditClass.Stub),
                new UnresolvedImport("AbCdEfGhIjk", "", AuditClass.Absent),
            ],
            summary.Unresolved);
    }

    [Fact]
    public void A_file_without_imports_has_no_unresolved_list()
    {
        var path = _temp.Combine("audit.json");
        File.WriteAllText(path, AuditFiles.Json(262, 0, 0));

        Assert.Empty(AuditSummary.Read(path).Unresolved);
    }
```

(If `AuditSummaryTests` has no `_temp`, add `private readonly TempDirectory _temp = new();` and `IDisposable` as the other test classes do.)

In `UnwrapSelfStepTests`, `RelinkStepTests` and `AuditStepTests`, find each test that asserts the failure message of a non-zero exit code and add, after the existing assertion on the caught `GameImportException` (or the `StepFailedException.InnerException`):

```csharp
        var failure = Assert.IsType<GameImportException>(exception);   // adapt to how the test obtains it
        Assert.DoesNotContain(Environment.NewLine, failure.Summary, StringComparison.Ordinal);
        Assert.Equal(failure.Summary + Environment.NewLine + failure.Details, failure.Message);
```

For the relinker with the Intel option, also assert `Assert.Contains("AMD-only instruction", failure.Summary, StringComparison.Ordinal);`. For unwrap, `Assert.Contains("must come already unwrapped from the dump tool", failure.Summary, StringComparison.Ordinal);`.

Add to `GameImportExceptionTests.cs` (new, in `tests/PS5Kitsune.Core.Tests/Games/`):

```csharp
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class GameImportExceptionTests
{
    [Fact]
    public void Without_details_the_message_is_the_summary()
    {
        var exception = new GameImportException("It failed.");

        Assert.Equal("It failed.", exception.Message);
        Assert.Null(exception.Details);
    }

    [Fact]
    public void With_details_the_message_joins_them_on_a_new_line()
    {
        var exception = new GameImportException("It failed.", "line 1");

        Assert.Equal("It failed." + Environment.NewLine + "line 1", exception.Message);
        Assert.Equal("It failed.", exception.Summary);
        Assert.Equal("line 1", exception.Details);
    }
}
```

In `GameImporterTests`, in the test that adds the game successfully (the first one that gets `GameAdded`), add:

```csharp
        Assert.Equal(262, added.Audit.Implemented);
        Assert.Empty(added.Audit.Unresolved);
```

(adapt `added` to the test's variable for the `GameAdded` result).

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: compile errors (`Unresolved`, `UnresolvedImport`, `Summary`, `GameAdded.Audit` missing).

- [ ] **Step 3: Implement**

`GameImportException.cs`:

```csharp
namespace PS5Kitsune.Core.Games;

/// <summary>
/// An add step failed for a reason the user can act on. <see cref="Summary"/> says what happened in one line;
/// <see cref="Details"/> holds the tool's output, shown on demand. The message joins them, as the CLI prints it.
/// </summary>
public sealed class GameImportException(string summary, string? details = null)
    : Exception(details is null ? summary : summary + Environment.NewLine + details)
{
    public string Summary { get; } = summary;

    public string? Details { get; } = details;
}
```

`UnwrapSelfStep.cs` (the throw):

```csharp
                throw new GameImportException(
                    $"unwrap_self.py could not unwrap {relative} (exit code {result.ExitCode}): the game must come already unwrapped from the dump tool.",
                    errors.ToString());
```

`RelinkStep.cs`: `throw new GameImportException($"The relinker failed with exit code {result.ExitCode}.{intelNote}", errors.ToString());`

`AuditStep.cs`: `throw new GameImportException($"import_audit.py could not read its input (exit code {result.ExitCode}).", errors.ToString());`

(Check that `OutputTail.ToString()` is what the interpolation used before; if the class formats differently through interpolation, use the same expression.)

`AuditSummary.cs` — add the types and the property, and fill it in `Read`:

```csharp
/// <summary>The class of an import the game needs and AnyPS5 does not implement.</summary>
public enum AuditClass
{
    Stub,
    Absent,
}

/// <param name="Name">The function's name, or its NID when the audit has no name.</param>
/// <param name="Where">The library, else the module, that the game imports it from; "" when the audit gives neither.</param>
public sealed record UnresolvedImport(string Name, string Where, AuditClass Class);
```

In the record `AuditSummary`, add:

```csharp
    /// <summary>The stub and absent entries of audit.json's imports, in file order, each name and place once.</summary>
    public IReadOnlyList<UnresolvedImport> Unresolved { get; init; } = [];
```

In `Read`, after `missing`, build the list and return it with an object initializer:

```csharp
            var unresolved = new List<UnresolvedImport>();
            if (root.TryGetProperty("imports", out var imports))
            {
                foreach (var entry in imports.EnumerateArray())
                {
                    AuditClass? kind = entry.GetProperty("class").GetString() switch
                    {
                        "stub" => AuditClass.Stub,
                        "absent" => AuditClass.Absent,
                        _ => null,
                    };
                    if (kind is not { } auditClass)
                    {
                        continue;
                    }

                    var import = new UnresolvedImport(
                        Text(entry, "name") ?? Text(entry, "nid") ?? "",
                        Text(entry, "library") ?? Text(entry, "module") ?? "",
                        auditClass);
                    if (!unresolved.Contains(import))
                    {
                        unresolved.Add(import);
                    }
                }
            }

            return new AuditSummary(
                counts.GetProperty("implemented").GetInt32(),
                counts.GetProperty("stub").GetInt32(),
                counts.GetProperty("absent").GetInt32(),
                missing)
            {
                Unresolved = unresolved,
            };
```

and the helper (inside the record):

```csharp
    private static string? Text(JsonElement entry, string property) =>
        entry.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
```

`AddGameResult.cs`:

```csharp
/// <param name="Warnings">Non-blocking validation issues, to show the user.</param>
/// <param name="Audit">The audit's counts and unresolved functions; game.json keeps only the counts.</param>
public sealed record GameAdded(GameRecord Record, string GameDirectory, IReadOnlyList<ValidationIssue> Warnings, AuditSummary Audit) : AddGameResult;
```

`GameImporter.cs`: the `return new GameAdded(...)` becomes `return new GameAdded(record, game.Root, validation.Warnings, context.Audit ?? AuditSummary.Read(game.AuditFile));` (the audit step is skipped on a resume after it ran, leaving `context.Audit` null).

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: all pass, 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add -A src tests
git commit -m "Split add failures into summary and details and list unresolved functions

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 2: Core — prepare, then add

**Files:**
- Create: `src/PS5Kitsune.Core/Games/PreparedGame.cs`, `src/PS5Kitsune.Core/Games/IGameImporter.cs`
- Modify: `src/PS5Kitsune.Core/Games/GameImporter.cs`, `src/PS5Kitsune.Core/Games/GameSourceResolver.cs`, `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Games/GameImporterTests.cs`, new `tests/PS5Kitsune.Core.Tests/Games/PrepareGameTests.cs`

**Interfaces:**
- Consumes: `GameAdded(…, AuditSummary Audit)` (Task 1).
- Produces:
  - `record GameBinary(string RelativePath, BinaryKind Kind)`;
  - `PreparedGame(GameSource source, GameValidation validation, string? iconPath, IReadOnlyList<GameBinary> binaries, string? gameDirectory, bool alreadyAdded, bool resumes, Action<Exception, string>? onCleanupFailed = null) : IAsyncDisposable` with `Source`, `Validation`, `Metadata`, `IsBlocked`, `IconPath`, `Binaries`, `GameDirectory`, `AlreadyAdded`, `Resumes`, `IsDisposed`, `DisposeAsync()`, and `internal static void DeleteStaging(string staging, Action<Exception, string>? onFailed)`;
  - `IGameImporter { Task<PreparedGame> PrepareAsync(string input, CancellationToken cancellationToken); Task<GameAdded> AddAsync(PreparedGame game, bool toIntel, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default); IReadOnlyList<string> StepNames { get; } }`;
  - `GameImporter.Names` (static, the six names) and `GameSourceResolver.StagingFor(string zipPath)`.

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.Core.Tests/Games/PrepareGameTests.cs` — reuse `GameImporterTests`'s construction (copy its constructor fields: `_temp`, `_setup`, `_importer`, `WriteZip`, the `Respond` player is not needed for preparation):

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Tests.AnyPS5;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class PrepareGameTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly AnyPS5TestSetup _setup;
    private readonly GameImporter _importer;

    public PrepareGameTests()
    {
        _setup = new AnyPS5TestSetup(_temp.Combine("root"));
        _importer = new GameImporter(
            _setup.Layout, _setup.Platform, _setup.Profile, _setup.Paths, _setup.Installer,
            new GameSourceResolver(_setup.Layout), new GameValidator(_ => long.MaxValue),
            _setup.PythonTool, _setup.Runner, _setup.StepRunner, _setup.Time, NullLogger<GameImporter>.Instance);
    }

    private string Staging => Path.Combine(_setup.Layout.Staging, TestGames.TitleId);

    private Task<PreparedGame> PrepareAsync(string input) => _importer.PrepareAsync(input, TestContext.Current.CancellationToken);

    private string WriteZip(byte[] content)
    {
        var zip = _temp.Combine("PPSA99666.zip");
        File.WriteAllBytes(zip, content);
        return zip;
    }

    [Fact]
    public async Task A_folder_is_described_without_copying_anything()
    {
        var folder = _temp.Combine("doom");
        TestGames.Create(folder);

        await using var game = await PrepareAsync(folder);

        Assert.Equal(TestGames.TitleId, game.Metadata?.TitleId);
        Assert.Equal("DOOM", game.Metadata?.Name);
        Assert.Equal(Path.Combine(folder, "sce_sys", "icon0.png"), game.IconPath);
        Assert.Equal(["sce_module"], game.Validation.ModuleDirectories);
        Assert.True(game.Validation.SizeBytes > 0);
        Assert.Equal([new GameBinary("eboot.bin", BinaryKind.Self), new GameBinary("sce_module/libc.prx", BinaryKind.Self)], game.Binaries);
        Assert.False(game.IsBlocked);
        Assert.False(game.AlreadyAdded);
        Assert.False(game.Resumes);
        Assert.Equal(_setup.Layout.GameDirectory(TestGames.TitleId), game.GameDirectory);
        Assert.Null(game.Source.StagingDirectory);
        Assert.False(Directory.Exists(_setup.Layout.Games));
    }

    [Fact]
    public async Task A_zip_is_extracted_to_staging_and_disposing_deletes_it()
    {
        var game = await PrepareAsync(WriteZip(TestGames.Zip("PPSA99666/")));

        Assert.Equal(Staging, game.Source.StagingDirectory);
        Assert.True(Directory.Exists(Staging));
        Assert.Equal("DOOM", game.Metadata?.Name);

        await game.DisposeAsync();
        await game.DisposeAsync();

        Assert.True(game.IsDisposed);
        Assert.False(Directory.Exists(Staging));
        Assert.False(Directory.Exists(Staging + ".staging"));
    }

    [Fact]
    public async Task An_added_game_and_a_stopped_add_are_recognized()
    {
        var folder = _temp.Combine("doom");
        TestGames.Create(folder);
        var gameDirectory = _setup.Layout.GameDirectory(TestGames.TitleId);
        Directory.CreateDirectory(Path.Combine(gameDirectory, "app0"));

        await using (var stopped = await PrepareAsync(folder))
        {
            Assert.True(stopped.Resumes);
            Assert.False(stopped.AlreadyAdded);
        }

        TestFiles.Write(Path.Combine(gameDirectory, ".ps5kitsune", "game.json"), "{}");
        await using var added = await PrepareAsync(folder);

        Assert.True(added.AlreadyAdded);
        Assert.False(added.Resumes);
    }

    [Fact]
    public async Task A_path_that_is_neither_is_refused()
    {
        var exception = await Assert.ThrowsAsync<InvalidGameSourceException>(() => PrepareAsync(_temp.Combine("nothing.txt")));

        Assert.Contains("is neither a folder nor a .zip file", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_folder_without_eboot_is_refused()
    {
        Directory.CreateDirectory(_temp.Combine("empty"));

        var exception = await Assert.ThrowsAsync<InvalidGameSourceException>(() => PrepareAsync(_temp.Combine("empty")));

        Assert.StartsWith("No eboot.bin found in", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_zip_without_eboot_leaves_no_staging()
    {
        var zip = WriteZip(TestFiles.ZipBytes(("PPSA99666/readme.txt", "no game"u8.ToArray())));

        await Assert.ThrowsAsync<InvalidGameSourceException>(() => PrepareAsync(zip));

        Assert.False(Directory.Exists(Staging));
        Assert.False(Directory.Exists(Staging + ".staging"));
    }

    [Fact]
    public async Task An_unreadable_zip_is_refused_and_leaves_no_staging()
    {
        var zip = WriteZip("not a zip"u8.ToArray());

        var exception = await Assert.ThrowsAsync<InvalidGameSourceException>(() => PrepareAsync(zip));

        Assert.StartsWith($"Cannot read {zip}: ", exception.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Staging));
        Assert.False(Directory.Exists(Staging + ".staging"));
    }

    [Fact]
    public async Task A_cancelled_preparation_leaves_no_staging()
    {
        var zip = WriteZip(TestGames.Zip("PPSA99666/"));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _importer.PrepareAsync(zip, cancellation.Token));

        Assert.False(Directory.Exists(Staging));
        Assert.False(Directory.Exists(Staging + ".staging"));
    }

    [Fact]
    public void Step_names_are_the_six_add_steps() =>
        Assert.Equal(
            ["Copy the game", "Copy the AnyPS5 libraries", "Unwrap SELF files", "Convert with the relinker", "Audit the imports", "Record the game"],
            ((IGameImporter)_importer).StepNames);

    public void Dispose() => _temp.Dispose();
}
```

In `GameImporterTests`, add (using the existing helpers `AddAsync`, `WriteZip`, `GameDirectory`, `Staging`, and whatever installs AnyPS5 in the existing success tests):

```csharp
    [Fact]
    public async Task A_prepared_game_is_added_and_returns_its_audit()
    {
        await _setup.InstallAsync();
        await using var game = await _importer.PrepareAsync(WriteZip(), TestContext.Current.CancellationToken);

        var added = await _importer.AddAsync(game, toIntel: false, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(TestGames.TitleId, added.Record.TitleId);
        Assert.Equal(262, added.Audit.Implemented);
    }

    [Fact]
    public async Task A_blocked_or_added_game_is_refused()
    {
        await _setup.InstallAsync();
        var folder = _temp.Combine("blocked");
        TestGames.Create(folder);
        File.Delete(Path.Combine(folder, "sce_sys", "param.json"));
        await using var blocked = await _importer.PrepareAsync(folder, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _importer.AddAsync(blocked, false, cancellationToken: TestContext.Current.CancellationToken));

        await AddAsync(WriteZip());
        await using var again = await _importer.PrepareAsync(WriteZip(), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _importer.AddAsync(again, false, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Adding_a_prepared_game_requires_the_install()
    {
        await using var game = await _importer.PrepareAsync(WriteZip(), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<AnyPS5NotInstalledException>(() => _importer.AddAsync(game, false, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_install_is_checked_before_the_input()
    {
        await Assert.ThrowsAsync<AnyPS5NotInstalledException>(() => AddAsync(_temp.Combine("nothing.txt")));
    }
```

Find the existing test (if any) asserting that the staging folder is **kept** after a copy that started and did not complete; change its assertion to `Assert.False(Directory.Exists(Staging));` and rename it to say the staging folder is deleted (spec: staging is always deleted, since it is never reused).

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: compile errors (`PrepareAsync`, `PreparedGame`, `GameBinary`, `IGameImporter`).

- [ ] **Step 3: Implement**

`GameSourceResolver.cs`: add, and use it in `ResolveAsync` instead of the inline `Path.Combine`:

```csharp
    /// <summary>Where a zip is extracted: downloads\staging\&lt;zip name without extension&gt;.</summary>
    public string StagingFor(string zipPath) => Path.Combine(layout.Staging, Path.GetFileNameWithoutExtension(zipPath));
```

`src/PS5Kitsune.Core/Games/PreparedGame.cs`:

```csharp
namespace PS5Kitsune.Core.Games;

/// <param name="RelativePath">Relative to the game root, '/' separators.</param>
public sealed record GameBinary(string RelativePath, BinaryKind Kind);

/// <summary>
/// A game read and validated, not yet added: what the summary shows. It owns the staging folder of an extracted zip,
/// deleted by <see cref="DisposeAsync"/>; a kept staging folder is never reused, since a zip is extracted again on every add.
/// </summary>
/// <param name="gameDirectory">&lt;root&gt;\games\&lt;titleId&gt;; null without metadata.</param>
/// <param name="alreadyAdded">The game folder holds a game.json.</param>
/// <param name="resumes">The game folder exists without game.json: a previous add stopped.</param>
/// <param name="onCleanupFailed">Told the exception and the folder when a staging folder cannot be deleted.</param>
public sealed class PreparedGame(
    GameSource source,
    GameValidation validation,
    string? iconPath,
    IReadOnlyList<GameBinary> binaries,
    string? gameDirectory,
    bool alreadyAdded,
    bool resumes,
    Action<Exception, string>? onCleanupFailed = null) : IAsyncDisposable
{
    private int _disposed;

    public GameSource Source { get; } = source;

    public GameValidation Validation { get; } = validation;

    public GameMetadata? Metadata => Validation.Metadata;

    public bool IsBlocked => Validation.IsBlocked;

    /// <summary>Full path of sce_sys/icon0.png; null when the game has none.</summary>
    public string? IconPath { get; } = iconPath;

    /// <summary>eboot.bin, then every file of the module folders.</summary>
    public IReadOnlyList<GameBinary> Binaries { get; } = binaries;

    public string? GameDirectory { get; } = gameDirectory;

    public bool AlreadyAdded { get; } = alreadyAdded;

    public bool Resumes { get; } = resumes;

    public bool IsDisposed => Volatile.Read(ref _disposed) == 1;

    /// <summary>Deletes the staging folder on the thread pool; does nothing the second time or without one.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1 || Source.StagingDirectory is not { } staging)
        {
            return;
        }

        await Task.Run(() => DeleteStaging(staging, onCleanupFailed)).ConfigureAwait(false);
    }

    /// <summary>Deletes a staging folder and its half-extracted sibling (&lt;staging&gt;.staging); a failure is reported, not thrown.</summary>
    internal static void DeleteStaging(string staging, Action<Exception, string>? onFailed)
    {
        foreach (var folder in (string[])[staging, staging + ".staging"])
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
                onFailed?.Invoke(exception, folder);
            }
        }
    }
}
```

`src/PS5Kitsune.Core/Games/IGameImporter.cs`:

```csharp
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.Games;

/// <summary>Adds a game in two stages, so a screen can show the summary before anything is copied.</summary>
public interface IGameImporter
{
    /// <summary>The add steps' names, in order, to list before the add starts.</summary>
    IReadOnlyList<string> StepNames { get; }

    /// <summary>Resolves the folder or zip and validates it, on the thread pool; a zip is extracted into staging.</summary>
    /// <exception cref="InvalidGameSourceException">Not a folder or zip, no eboot.bin, or an unreadable zip; no staging is left.</exception>
    /// <exception cref="OperationCanceledException">Cancelled; no staging is left.</exception>
    Task<PreparedGame> PrepareAsync(string input, CancellationToken cancellationToken);

    /// <summary>Runs the add steps; an add that stopped resumes. Does not dispose <paramref name="game"/>.</summary>
    /// <exception cref="AnyPS5NotInstalledException">AnyPS5 is not installed.</exception>
    /// <exception cref="InvalidOperationException">The game is blocked or already added.</exception>
    /// <exception cref="StepFailedException">A step failed.</exception>
    Task<GameAdded> AddAsync(PreparedGame game, bool toIntel, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default);
}
```

`GameImporter.cs` — the class implements `IGameImporter`; replace the body of `AddAsync(string …)` and add the new members:

```csharp
    public static readonly IReadOnlyList<string> Names =
        ["Copy the game", "Copy the AnyPS5 libraries", "Unwrap SELF files", "Convert with the relinker", "Audit the imports", "Record the game"];

    public IReadOnlyList<string> StepNames => Names;

    public async Task<AddGameResult> AddAsync(
        string input, bool toIntel, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default)
    {
        if (!installer.IsInstalled())
        {
            throw new AnyPS5NotInstalledException();
        }

        await using var game = await PrepareAsync(input, cancellationToken).ConfigureAwait(false);
        if (game.IsBlocked || game.Metadata is not { } metadata)
        {
            return new GameRejected(game.Validation.Issues);
        }

        if (game.AlreadyAdded)
        {
            return new GameAlreadyAdded(metadata.TitleId, game.GameDirectory!);
        }

        return await AddAsync(game, toIntel, progress, onEvent, cancellationToken).ConfigureAwait(false);
    }

    public Task<PreparedGame> PrepareAsync(string input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Task.Run(() => PrepareCoreAsync(input, cancellationToken), cancellationToken);
    }

    public async Task<GameAdded> AddAsync(
        PreparedGame game, bool toIntel, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (!installer.IsInstalled())
        {
            throw new AnyPS5NotInstalledException();
        }

        if (game.IsBlocked || game.Metadata is not { } metadata)
        {
            throw new InvalidOperationException("The game has blocking issues; it cannot be added.");
        }

        var layoutGame = new GameLayout(layout.GameDirectory(metadata.TitleId), metadata.TitleId, platform);
        if (File.Exists(layoutGame.RecordFile))
        {
            throw new InvalidOperationException($"{metadata.TitleId} is already added.");
        }

        var install = installer.ReadRecord() ?? throw new AnyPS5NotInstalledException();
        var context = new GameImportContext(game.Source, metadata, game.Validation.ModuleDirectories, layoutGame, toIntel);
        IStep[] steps =
        [
            new CopyGameStep(context),
            new CopyLibsStep(paths, layoutGame),
            new UnwrapSelfStep(context, paths, python, processRunner),
            new RelinkStep(context, paths, profile, processRunner),
            new AuditStep(context, paths, python, processRunner),
            new WriteGameRecordStep(context, install, time),
        ];
        await stepRunner.RunAsync(steps, progress, onEvent, cancellationToken).ConfigureAwait(false);

        var record = GameRecordStore.Read(layoutGame.RecordFile) ?? throw new InvalidOperationException($"{layoutGame.RecordFile} was not written.");
        return new GameAdded(record, layoutGame.Root, game.Validation.Warnings, context.Audit ?? AuditSummary.Read(layoutGame.AuditFile));
    }

    private async Task<PreparedGame> PrepareCoreAsync(string input, CancellationToken cancellationToken)
    {
        var path = Path.GetFullPath(input);
        var staging = File.Exists(path) && string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase)
            ? resolver.StagingFor(path)
            : null;
        try
        {
            GameSource source;
            try
            {
                source = await resolver.ResolveAsync(input, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (staging is not null && exception is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                throw new InvalidGameSourceException($"Cannot read {input}: {exception.Message}");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var validation = validator.Validate(source.Root, layout.Games);
            var icon = Path.Combine(source.Root, "sce_sys", "icon0.png");
            var binaries = new List<GameBinary> { new(GameSourceResolver.ExecutableName, BinaryKinds.Detect(Path.Combine(source.Root, GameSourceResolver.ExecutableName))) };
            foreach (var directory in validation.ModuleDirectories)
            {
                foreach (var file in Directory.EnumerateFiles(Path.Combine(source.Root, directory), "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
                {
                    binaries.Add(new GameBinary(Path.GetRelativePath(source.Root, file).Replace('\\', '/'), BinaryKinds.Detect(file)));
                }
            }

            string? gameDirectory = null;
            var alreadyAdded = false;
            var resumes = false;
            if (validation.Metadata is { } metadata)
            {
                var game = new GameLayout(layout.GameDirectory(metadata.TitleId), metadata.TitleId, platform);
                gameDirectory = game.Root;
                alreadyAdded = File.Exists(game.RecordFile);
                resumes = !alreadyAdded && Directory.Exists(game.Root);
            }

            return new PreparedGame(source, validation, File.Exists(icon) ? icon : null, binaries, gameDirectory, alreadyAdded, resumes, LogStagingNotDeleted);
        }
        catch (Exception) when (staging is not null)
        {
            PreparedGame.DeleteStaging(staging, LogStagingNotDeleted);
            throw;
        }
    }
```

Remove the old `finally` staging cleanup and the now-unused `resolver`/`validator` code paths from the old `AddAsync`. Keep the existing `[LoggerMessage]` `LogStagingNotDeleted(Exception, string)`; if its signature differs from `Action<Exception, string>`, wrap it in a lambda. Update the class summary: "Adds a game in two stages: prepare (resolve the folder or zip and validate it), then add (run the add steps). Without game.json, a new add resumes at the first step not done."

`ServiceCollectionExtensions.cs`, after `services.AddSingleton<GameImporter>();`:

```csharp
        services.AddSingleton<IGameImporter>(provider => provider.GetRequiredService<GameImporter>());
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.Core.Tests.Games.PrepareGameTests"`, then the full suite.
Expected: PASS, 0 warnings. Then run the CLI by hand on a temporary root to confirm `add` still behaves (no network needed for the "not installed" path):

```bash
ROOT="$(mktemp -d)"; dotnet run --project src/PS5Kitsune.Cli -c Release --no-build -- add C:/PS5Games/PPSA99666.zip --root "$ROOT"; echo "exit $?"
```

Expected: "AnyPS5 is not installed. Run 'ps5kitsune install' first.", exit 1, and no `downloads/staging` folder under `$ROOT`.

- [ ] **Step 5: Commit**

```bash
git add -A src tests
git commit -m "Prepare a game before adding it

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 3: App — shared step list and close handling

**Files:**
- Create: `src/PS5Kitsune.App/Navigation/IRunningWork.cs`, `src/PS5Kitsune.App/Navigation/IPreparedGameOwner.cs`, `src/PS5Kitsune.App/ViewModels/StepRunViewModel.cs`
- Modify: `src/PS5Kitsune.App/ViewModels/InstallViewModel.cs`, `src/PS5Kitsune.App/ViewModels/MainWindowViewModel.cs`, `src/PS5Kitsune.App/Views/MainWindow.axaml.cs`
- Test: `tests/PS5Kitsune.App.Tests/MainWindowViewModelTests.cs` (existing InstallViewModelTests must pass unchanged)

**Interfaces:**
- Produces:
  - `IRunningWork { bool IsRunning { get; } string? QuitMessage { get; } Task CancelAndWaitAsync(); }` and `IPreparedGameOwner { Task ReleaseAsync(); }` (namespace `PS5Kitsune.App.Navigation`);
  - `abstract partial class StepRunViewModel : ObservableObject, INavigationAware, IRunningWork` with the public members `Steps`, `Log`, `IsRunning`, `HasFailed`, `Failure`, `FailureDetails`, `CanRetry`, `ShowsProgress`, `ProgressPercent`, `ProgressIndeterminate`, `ProgressText`, `OpenError`, `QuitMessage`, `OnNavigatedTo()`, `RunAsync()`, `CancelAndWaitAsync()`, commands `CancelCommand`, `RetryCommand`, `BackHomeCommand`, `OpenLogCommand`; and the protected hooks of Refinement 3;
  - `MainWindowViewModel.NeedsCloseHandling` (replaces `IsInstallRunning`).

- [ ] **Step 1: Update the main window tests (they fail to compile until Step 3)**

In `MainWindowViewModelTests`, replace `IsInstallRunning` with `NeedsCloseHandling` in every test, and add:

```csharp
    [Fact]
    public async Task Closing_on_a_page_that_owns_a_game_releases_it_without_asking()
    {
        var owner = new FakeOwner();
        _navigator.GoTo(owner);   // adapt to the test class's navigator field
        var viewModel = Create();  // adapt to the test class's factory

        Assert.True(viewModel.NeedsCloseHandling);
        Assert.True(await viewModel.ConfirmCloseAsync());

        Assert.True(owner.Released);
        Assert.Empty(_dialogs.Confirmations);
    }

    [Fact]
    public async Task Running_work_without_a_quit_message_is_cancelled_without_asking()
    {
        var work = new FakeWork { IsRunning = true };
        _navigator.GoTo(work);
        var viewModel = Create();

        Assert.True(await viewModel.ConfirmCloseAsync());

        Assert.True(work.Cancelled);
        Assert.Empty(_dialogs.Confirmations);
    }

    private sealed class FakeOwner : IPreparedGameOwner
    {
        public bool Released { get; private set; }

        public Task ReleaseAsync()
        {
            Released = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWork : IRunningWork
    {
        public bool IsRunning { get; set; }

        public string? QuitMessage => null;

        public bool Cancelled { get; private set; }

        public Task CancelAndWaitAsync()
        {
            Cancelled = true;
            return Task.CompletedTask;
        }
    }
```

The existing install tests (asks, can be declined, confirming cancels) keep their assertions, including the dialog message "An install is running. Quitting cancels it; it will resume where it stopped.".

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: compile errors (`NeedsCloseHandling`, `IPreparedGameOwner`, `IRunningWork`).

- [ ] **Step 3: Implement**

`src/PS5Kitsune.App/Navigation/IRunningWork.cs`:

```csharp
namespace PS5Kitsune.App.Navigation;

/// <summary>A page doing work that closing the window must stop first.</summary>
public interface IRunningWork
{
    bool IsRunning { get; }

    /// <summary>Asked before quitting while running; null cancels without asking.</summary>
    string? QuitMessage { get; }

    /// <summary>Cancels the work and waits until it has stopped, including its clean-up.</summary>
    Task CancelAndWaitAsync();
}
```

`src/PS5Kitsune.App/Navigation/IPreparedGameOwner.cs`:

```csharp
namespace PS5Kitsune.App.Navigation;

/// <summary>A page holding a prepared game, whose staging folder must be deleted when the window closes.</summary>
public interface IPreparedGameOwner
{
    Task ReleaseAsync();
}
```

`src/PS5Kitsune.App/ViewModels/StepRunViewModel.cs` — move every member of `InstallViewModel` that is not install-specific here, keeping names, attributes and behaviour:

```csharp
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.App.ViewModels;

/// <summary>
/// A page that runs steps when shown: step list, a progress bar for downloads and counters, a log built from the step events.
/// Events and progress arrive on worker threads and are posted to the UI thread in order. Install and Adding inherit it.
/// </summary>
public abstract partial class StepRunViewModel : ObservableObject, INavigationAware, IRunningWork
{
    private readonly string _logFile;
    private readonly IPlatform _platform;
    private readonly Action<bool>? _busyChanged;
    private CancellationTokenSource? _cancellation;
    private Task _running = Task.CompletedTask;
    private bool _started;

    /// <param name="busyChanged">Told true when the run starts and false when it stops, so the data root cannot change meanwhile.</param>
    protected StepRunViewModel(string logFile, IPlatform platform, IUiDispatcher ui, Action<bool>? busyChanged)
    {
        _logFile = logFile;
        _platform = platform;
        Ui = ui;
        _busyChanged = busyChanged;
    }

    public ObservableCollection<InstallStepViewModel> Steps { get; } = [];

    public ObservableCollection<string> Log { get; } = [];

    [ObservableProperty]
    public partial bool IsRunning { get; private set; }

    [ObservableProperty]
    public partial bool HasFailed { get; private set; }

    [ObservableProperty]
    public partial string? Failure { get; private set; }

    /// <summary>The tool output of a failed step; null otherwise.</summary>
    [ObservableProperty]
    public partial string? FailureDetails { get; private set; }

    /// <summary>False when retrying cannot help (for example AnyPS5 is not installed).</summary>
    [ObservableProperty]
    public partial bool CanRetry { get; private set; } = true;

    // ShowsProgress, ProgressPercent, ProgressIndeterminate, ProgressText, OpenError: moved verbatim from InstallViewModel.

    public abstract string? QuitMessage { get; }

    protected IUiDispatcher Ui { get; }

    /// <summary>Whether a step shows an indeterminate bar as soon as it starts.</summary>
    protected abstract bool IndeterminateOnStart { get; }

    protected abstract IReadOnlyList<string> StepNames();

    protected abstract Task RunCoreAsync(IProgress<StepProgress> progress, Action<StepEvent> onEvent, CancellationToken cancellationToken);

    protected abstract (string Summary, string? Details) SplitFailure(Exception exception);

    protected virtual bool CanRetryAfter(Exception exception) => true;

    /// <summary>Runs on the UI thread after a successful run.</summary>
    protected abstract Task OnSucceededAsync();

    /// <summary>Runs on the UI thread after a cancelled run.</summary>
    protected abstract Task OnCancelledAsync();

    /// <summary>"Back to home".</summary>
    protected abstract Task LeaveAsync();

    public void OnNavigatedTo()
    {
        if (!_started)
        {
            _ = RunAsync();
        }
    }

    /// <summary>Runs the steps; completed steps are skipped, so this also resumes after a failure.</summary>
    public Task RunAsync()
    {
        _started = true;
        _running = RunLoopAsync();
        return _running;
    }

    /// <summary>For closing the window: cancels the run and waits until it and its clean-up have finished.</summary>
    public async Task CancelAndWaitAsync()
    {
        _cancellation?.Cancel();
        await _running.ConfigureAwait(true);
    }

    private async Task RunLoopAsync()
    {
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        IsRunning = true;
        HasFailed = false;
        Failure = null;
        FailureDetails = null;
        CanRetry = true;
        RebuildSteps();

        _busyChanged?.Invoke(true);
        bool? succeeded = null;
        try
        {
            await RunCoreAsync(new UiProgress(this), stepEvent => Ui.Post(() => Apply(stepEvent)), cancellation.Token).ConfigureAwait(true);
            succeeded = true;
        }
        catch (OperationCanceledException)
        {
            succeeded = false;
        }
        catch (Exception exception)
        {
            var (summary, details) = SplitFailure(exception);
            var canRetry = CanRetryAfter(exception);
            Ui.Post(() => Fail(summary, details, canRetry));
        }
        finally
        {
            _cancellation = null;
            _busyChanged?.Invoke(false);
            Ui.Post(() =>
            {
                IsRunning = false;
                ShowsProgress = false;
            });
        }

        if (succeeded == true)
        {
            await OnSucceededAsync().ConfigureAwait(true);
        }
        else if (succeeded == false)
        {
            await OnCancelledAsync().ConfigureAwait(true);
        }
    }

    private void RebuildSteps()
    {
        Steps.Clear();
        foreach (var name in StepNames())
        {
            Steps.Add(new InstallStepViewModel(name));
        }
    }

    private void Fail(string message, string? details, bool canRetry)
    {
        HasFailed = true;
        Failure = message;
        FailureDetails = details;
        CanRetry = canRetry;
    }

    // Apply(StepEvent) and Apply(StepProgress): moved verbatim from InstallViewModel, except that the
    // indeterminate start uses IndeterminateOnStart instead of `_mode == AnyPS5Mode.Build`.

    [RelayCommand]
    private void Cancel() => _cancellation?.Cancel();

    [RelayCommand(CanExecute = nameof(CanRetry))]
    private Task RetryAsync() => RunAsync();

    [RelayCommand]
    private Task BackHomeAsync() => LeaveAsync();

    [RelayCommand]
    private void OpenLog() => OpenError = PathOpener.TryOpen(_platform, _logFile);

    private sealed class UiProgress(StepRunViewModel owner) : IProgress<StepProgress>
    {
        public void Report(StepProgress value) => owner.Ui.Post(() => owner.Apply(value));
    }
}
```

Add `[NotifyCanExecuteChangedFor(nameof(RetryCommand))]` on `CanRetry`. Bring the `ShowsProgress`, `ProgressPercent`, `ProgressIndeterminate`, `ProgressText`, `OpenError` properties and the two `Apply` methods across exactly as they are in `InstallViewModel` today.

`InstallViewModel.cs` becomes:

```csharp
/// <summary>Runs the AnyPS5 install when the page is shown; see <see cref="StepRunViewModel"/>.</summary>
public sealed partial class InstallViewModel : StepRunViewModel
{
    private readonly IAnyPS5Installer _installer;
    private readonly AnyPS5Mode _mode;
    private readonly Navigator _navigator;
    private readonly IPages _pages;

    /// <param name="busyChanged">Told true when the install starts and false when it stops, so the data root cannot change meanwhile.</param>
    public InstallViewModel(IAnyPS5Installer installer, AnyPS5Mode mode, string logFile, IPlatform platform, IUiDispatcher ui, Navigator navigator, IPages pages, Action<bool>? busyChanged = null)
        : base(logFile, platform, ui, busyChanged)
    {
        _installer = installer;
        _mode = mode;
        _navigator = navigator;
        _pages = pages;
        foreach (var name in StepNames())
        {
            Steps.Add(new InstallStepViewModel(name));
        }
    }

    public override string? QuitMessage { get; } = "An install is running. Quitting cancels it; it will resume where it stopped.";

    /// <summary>Git, CMake and extraction report nothing measurable for a while; release steps are quick downloads.</summary>
    protected override bool IndeterminateOnStart => _mode == AnyPS5Mode.Build;

    /// <summary>The installer's current steps, read again on every run.</summary>
    protected override IReadOnlyList<string> StepNames() => [.. _installer.CreateSteps(_mode).Select(step => step.Name)];

    protected override Task RunCoreAsync(IProgress<StepProgress> progress, Action<StepEvent> onEvent, CancellationToken cancellationToken) =>
        _installer.InstallAsync(_mode, progress, onEvent, cancellationToken);

    protected override (string Summary, string? Details) SplitFailure(Exception exception)
    {
        if (exception is StepFailedException failed)
        {
            var build = failed.InnerException as BuildCommandException;
            return ($"{failed.StepName} failed: {build?.Summary ?? failed.InnerException?.Message ?? failed.Message}",
                string.IsNullOrEmpty(build?.Details) ? null : build.Details);
        }

        return ($"The install failed: {exception.Message}", null);
    }

    protected override Task OnSucceededAsync()
    {
        Ui.Post(() => _navigator.GoTo(_pages.Done()));
        return Task.CompletedTask;
    }

    protected override Task OnCancelledAsync()
    {
        Ui.Post(() => _navigator.GoHome(_pages.Home()));
        return Task.CompletedTask;
    }

    protected override Task LeaveAsync()
    {
        _navigator.GoHome(_pages.Home());
        return Task.CompletedTask;
    }
}
```

(Usings: keep what the file needs — `PS5Kitsune.App.Navigation`, `PS5Kitsune.Core.Abstractions`, `PS5Kitsune.Core.AnyPS5`, `PS5Kitsune.Core.AnyPS5.Build`, `PS5Kitsune.Core.Pipeline`.)

`MainWindowViewModel.cs` — replace `IsInstallRunning` and `ConfirmCloseAsync`:

```csharp
    /// <summary>The current page runs work or owns a prepared game: closing must go through <see cref="ConfirmCloseAsync"/>.</summary>
    public bool NeedsCloseHandling => Navigator.CurrentPage is IRunningWork { IsRunning: true } or IPreparedGameOwner;

    /// <summary>
    /// Asks before closing when the running work has a quit message; then cancels it and waits, then releases a prepared game.
    /// </summary>
    /// <returns>True when the window may close.</returns>
    public async Task<bool> ConfirmCloseAsync()
    {
        if (Navigator.CurrentPage is IRunningWork { IsRunning: true } work)
        {
            if (work.QuitMessage is { } message && !await _dialogs.ConfirmAsync("Quit PS5Kitsune?", message, "Quit").ConfigureAwait(true))
            {
                return false;
            }

            await work.CancelAndWaitAsync().ConfigureAwait(true);
        }

        if (Navigator.CurrentPage is IPreparedGameOwner owner)
        {
            await owner.ReleaseAsync().ConfigureAwait(true);
        }

        return true;
    }
```

`MainWindow.axaml.cs`: `MainWindowViewModel { IsInstallRunning: true }` becomes `MainWindowViewModel { NeedsCloseHandling: true }`; the summary becomes "While a page runs work or holds a prepared game, closing waits for the user's answer and for the clean-up."; the catch comment becomes "The window stays open; the work keeps running.".

- [ ] **Step 4: Run the tests**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: all pass, including every existing `InstallViewModelTests` test unchanged; 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add -A src tests
git commit -m "Share the step list and close handling between pages

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 4: App — Home entry, Choose a game, Summary

**Files:**
- Create: `src/PS5Kitsune.App/ViewModels/ChooseGameViewModel.cs`, `GameSummaryViewModel.cs`, `src/PS5Kitsune.App/Views/ChooseGameView.axaml(.cs)`, `GameSummaryView.axaml(.cs)`, `src/PS5Kitsune.App/Views/PathToBitmapConverter.cs`
- Modify: `src/PS5Kitsune.App/Navigation/IDialogService.cs`, `src/PS5Kitsune.App/Shell/AvaloniaDialogService.cs`, `src/PS5Kitsune.App/Navigation/IPages.cs`, `src/PS5Kitsune.App/Shell/PageFactory.cs`, `src/PS5Kitsune.App/ViewModels/HomeViewModel.cs`, `src/PS5Kitsune.App/Views/HomeView.axaml`, `src/PS5Kitsune.App/Views/MainWindow.axaml`, `tests/PS5Kitsune.App.Tests/Fakes.cs`
- Test: `tests/PS5Kitsune.App.Tests/HomeViewModelTests.cs`, new `ChooseGameViewModelTests.cs`, new `GameSummaryViewModelTests.cs`, `PageFactoryTests.cs`

**Interfaces:**
- Consumes: `IGameImporter`, `PreparedGame`, `GameBinary`, `BinaryKind`, `GameValidation`, `ValidationIssue`, `GameSource`, `GameMetadata` (Task 2); `IRunningWork`, `IPreparedGameOwner` (Task 3); `ICpuInfo.Read().IsIntel`.
- Produces:
  - `IDialogService.PickFileAsync(string title, string extension)`;
  - `IPages.ChooseGame()`, `IPages.GameSummary(PreparedGame game)`, `IPages.AddGame(PreparedGame game, bool toIntel)`, `IPages.AddResult(GameAdded added)` (the last two are declared here; `PageFactory` returns placeholders that Task 5 replaces — see Step 3);
  - `ChooseGameViewModel.OpenAsync(string path)`; `GameSummaryViewModel`;
  - Fakes: `FakeGameImporter`, `TestGameData.Prepared(...)`, `TestGameData.Added(...)`, `FakeDialogs.File`, `FakePages` strings `"choose"`, `"summary:{titleId}"`, `"adding:{titleId}:{toIntel}"`, `"result:{titleId}"`.

- [ ] **Step 1: Add the fakes and write the failing tests**

`tests/PS5Kitsune.App.Tests/Fakes.cs` — add:

```csharp
/// <summary>Builds prepared games and add results without touching the disk.</summary>
internal static class TestGameData
{
    public const string TitleId = "PPSA99666";

    public static PreparedGame Prepared(
        IReadOnlyList<ValidationIssue>? issues = null,
        bool hasMetadata = true,
        IReadOnlyList<GameBinary>? binaries = null,
        string? icon = null,
        bool alreadyAdded = false,
        bool resumes = false,
        IReadOnlyList<string>? moduleDirectories = null,
        long sizeBytes = 2_359_296)
    {
        var metadata = hasMetadata ? new GameMetadata(TitleId, "DOOM") : null;
        var validation = new GameValidation(metadata, moduleDirectories ?? ["sce_module"], sizeBytes, issues ?? []);
        return new PreparedGame(
            new GameSource(@"C:\games\doom", null),
            validation,
            icon,
            binaries ?? [new GameBinary("eboot.bin", BinaryKind.Self), new GameBinary("sce_module/libc.prx", BinaryKind.Self)],
            hasMetadata ? $@"C:\root\games\{TitleId}" : null,
            alreadyAdded,
            resumes);
    }

    public static GameAdded Added(AuditSummary? audit = null, IReadOnlyList<ValidationIssue>? warnings = null)
    {
        var summary = audit ?? new AuditSummary(360, 4, 0, new Dictionary<string, int>());
        var record = new GameRecord
        {
            SchemaVersion = GameRecord.CurrentSchemaVersion,
            TitleId = TitleId,
            Name = "DOOM",
            Executable = TitleId + ".exe",
            ConvertedWith = new ConversionInfo("test-profile", "release", "v0.1.2", null, false),
            Audit = new AuditRecord(summary.Implemented, summary.Stub, summary.Absent, summary.Verdict),
            AddedAt = new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero),
        };
        return new GameAdded(record, $@"C:\root\games\{TitleId}", warnings ?? [], summary);
    }
}

internal delegate Task<GameAdded> AddBehavior(PreparedGame game, bool toIntel, IProgress<StepProgress>? progress, Action<StepEvent>? onEvent, CancellationToken cancellationToken);

internal sealed class FakeGameImporter : IGameImporter
{
    public IReadOnlyList<string> StepNames { get; } =
        ["Copy the game", "Copy the AnyPS5 libraries", "Unwrap SELF files", "Convert with the relinker", "Audit the imports", "Record the game"];

    /// <summary>What PrepareAsync does; by default returns <see cref="TestGameData.Prepared"/>.</summary>
    public Func<string, CancellationToken, Task<PreparedGame>> Prepare { get; set; } = (_, _) => Task.FromResult(TestGameData.Prepared());

    public AddBehavior Add { get; set; } = (_, _, _, _, _) => Task.FromResult(TestGameData.Added());

    public List<string> Prepared { get; } = [];

    public List<(PreparedGame Game, bool ToIntel)> Adds { get; } = [];

    public Task<PreparedGame> PrepareAsync(string input, CancellationToken cancellationToken)
    {
        Prepared.Add(input);
        return Prepare(input, cancellationToken);
    }

    public Task<GameAdded> AddAsync(PreparedGame game, bool toIntel, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default)
    {
        Adds.Add((game, toIntel));
        return Add(game, toIntel, progress, onEvent, cancellationToken);
    }
}
```

In `FakeDialogs`, add `public string? File { get; set; }` and `public Task<string?> PickFileAsync(string title, string extension) => Task.FromResult(File);`.

In `FakePages`, add:

```csharp
    public object ChooseGame() => "choose";

    public object GameSummary(PreparedGame game) => $"summary:{game.Metadata?.TitleId}";

    public object AddGame(PreparedGame game, bool toIntel) => $"adding:{game.Metadata?.TitleId}:{toIntel}";

    public object AddResult(GameAdded added) => $"result:{added.Record.TitleId}";
```

(Usings: `PS5Kitsune.Core.Games`, `PS5Kitsune.Core.Library`.)

`HomeViewModelTests` — add:

```csharp
    [Fact]
    public async Task Adding_a_game_needs_an_install()
    {
        var viewModel = await CreateAsync();

        Assert.False(viewModel.AddGameCommand.CanExecute(null));
        Assert.Equal("Install AnyPS5 first to add games.", viewModel.AddGameHint);
    }

    [Fact]
    public async Task With_an_install_a_game_can_be_added()
    {
        _installer.ActiveMode = AnyPS5Mode.Release;
        _installer.State = new AnyPS5InstallState(AnyPS5InstallStatus.Installed, Record);
        var viewModel = await CreateAsync();

        Assert.True(viewModel.AddGameCommand.CanExecute(null));
        Assert.Null(viewModel.AddGameHint);
        viewModel.AddGameCommand.Execute(null);
        Assert.Equal("choose", _navigator.CurrentPage);
    }
```

`tests/PS5Kitsune.App.Tests/ChooseGameViewModelTests.cs`:

```csharp
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.App.Tests;

public sealed class ChooseGameViewModelTests
{
    private readonly FakeGameImporter _importer = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly Navigator _navigator = new();
    private readonly List<bool> _busy = [];

    private ChooseGameViewModel Create()
    {
        var viewModel = new ChooseGameViewModel(_importer, _dialogs, _navigator, new FakePages(), _busy.Add);
        _navigator.GoTo(viewModel);
        return viewModel;
    }

    [Fact]
    public async Task A_chosen_folder_opens_the_summary()
    {
        _dialogs.Folder = @"C:\games\doom";
        var viewModel = Create();

        await viewModel.ChooseFolderCommand.ExecuteAsync(null);

        Assert.Equal([@"C:\games\doom"], _importer.Prepared);
        Assert.Equal("summary:PPSA99666", _navigator.CurrentPage);
        Assert.Equal([true, false], _busy);
    }

    [Fact]
    public async Task A_chosen_zip_opens_the_summary()
    {
        _dialogs.File = @"C:\PS5Games\PPSA99666.zip";
        var viewModel = Create();

        await viewModel.ChooseZipCommand.ExecuteAsync(null);

        Assert.Equal([@"C:\PS5Games\PPSA99666.zip"], _importer.Prepared);
        Assert.Equal("summary:PPSA99666", _navigator.CurrentPage);
    }

    [Fact]
    public async Task A_cancelled_picker_does_nothing()
    {
        var viewModel = Create();

        await viewModel.ChooseFolderCommand.ExecuteAsync(null);

        Assert.Empty(_importer.Prepared);
        Assert.Same(viewModel, _navigator.CurrentPage);
    }

    [Fact]
    public async Task An_invalid_path_shows_its_message()
    {
        _importer.Prepare = (_, _) => throw new InvalidGameSourceException(@"No eboot.bin found in C:\PS5Games, in its app0 folder or in its single subfolder.");
        var viewModel = Create();

        await viewModel.OpenAsync(@"C:\PS5Games");

        Assert.Equal(@"No eboot.bin found in C:\PS5Games, in its app0 folder or in its single subfolder.", viewModel.Error);
        Assert.Same(viewModel, _navigator.CurrentPage);
        Assert.False(viewModel.IsPreparing);
    }

    [Fact]
    public async Task Preparing_shows_busy_and_can_be_cancelled()
    {
        var release = new TaskCompletionSource();
        _importer.Prepare = async (_, token) =>
        {
            await release.Task.WaitAsync(token);
            return TestGameData.Prepared();
        };
        var viewModel = Create();

        var opening = viewModel.OpenAsync(@"C:\games\doom");

        Assert.True(viewModel.IsPreparing);
        Assert.True(viewModel.IsRunning);
        Assert.Null(viewModel.QuitMessage);
        Assert.False(viewModel.ChooseFolderCommand.CanExecute(null));
        viewModel.CancelCommand.Execute(null);
        await opening;

        Assert.False(viewModel.IsPreparing);
        Assert.Null(viewModel.Error);
        Assert.Same(viewModel, _navigator.CurrentPage);
    }

    [Fact]
    public async Task A_result_arriving_after_cancel_is_disposed()
    {
        var game = TestGameData.Prepared();
        var release = new TaskCompletionSource();
        _importer.Prepare = async (_, _) =>
        {
            await release.Task;
            return game;   // ignores the token, as an extraction finishing its last file would
        };
        var viewModel = Create();

        var opening = viewModel.OpenAsync(@"C:\games\doom");
        viewModel.CancelCommand.Execute(null);
        release.SetResult();
        await opening;

        Assert.True(game.IsDisposed);
        Assert.Same(viewModel, _navigator.CurrentPage);
    }

    [Fact]
    public void Back_returns_home()
    {
        var viewModel = Create();

        viewModel.BackCommand.Execute(null);

        Assert.Equal("home", _navigator.CurrentPage);
    }
}
```

`tests/PS5Kitsune.App.Tests/GameSummaryViewModelTests.cs`:

```csharp
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.App.Tests;

public sealed class GameSummaryViewModelTests
{
    private readonly Navigator _navigator = new();
    private readonly TestPlatform _platform = new();

    private GameSummaryViewModel Create(PreparedGame game, bool intel = false)
    {
        _navigator.GoTo("choose");
        var viewModel = new GameSummaryViewModel(game, intel, _platform, _navigator, new FakePages());
        _navigator.GoTo(viewModel);
        return viewModel;
    }

    [Fact]
    public void Describes_the_game()
    {
        var viewModel = Create(TestGameData.Prepared());

        Assert.Equal("DOOM", viewModel.Name);
        Assert.Equal("PPSA99666", viewModel.TitleId);
        Assert.Equal("2.3 MB", viewModel.SizeText);
        Assert.Equal("1 module folder", viewModel.ModulesText);
        Assert.Equal("2 files are SELF and will be unwrapped, eboot.bin among them.", viewModel.BinariesText);
        Assert.Empty(viewModel.BlockingIssues);
        Assert.Null(viewModel.AlreadyAddedText);
        Assert.Null(viewModel.ResumeText);
        Assert.False(viewModel.ToIntel);
        Assert.True(viewModel.AddCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(0, "No module folder")]
    [InlineData(2, "2 module folders")]
    public void Counts_module_folders(int count, string expected)
    {
        var folders = new[] { "sce_module", "prx" }.Take(count).ToList();

        Assert.Equal(expected, Create(TestGameData.Prepared(moduleDirectories: folders)).ModulesText);
    }

    [Fact]
    public void Names_the_binaries_state()
    {
        Assert.Equal("No SELF file.", Create(TestGameData.Prepared(binaries: [new GameBinary("eboot.bin", BinaryKind.Elf)])).BinariesText);
        Assert.Equal(
            "1 file is SELF and will be unwrapped.",
            Create(TestGameData.Prepared(binaries: [new GameBinary("eboot.bin", BinaryKind.Elf), new GameBinary("sce_module/libc.prx", BinaryKind.Self)])).BinariesText);
    }

    [Fact]
    public void Blocking_issues_disable_adding_and_warnings_do_not()
    {
        var viewModel = Create(TestGameData.Prepared(issues:
        [
            new ValidationIssue(IssueSeverity.Blocking, "sce_sys/param.json is missing."),
            new ValidationIssue(IssueSeverity.Warning, "Low disk space."),
        ]));

        Assert.Equal(["sce_sys/param.json is missing."], viewModel.BlockingIssues);
        Assert.Equal(["Low disk space."], viewModel.Warnings);
        Assert.False(viewModel.AddCommand.CanExecute(null));
    }

    [Fact]
    public void An_added_game_offers_its_folder_instead()
    {
        var viewModel = Create(TestGameData.Prepared(alreadyAdded: true));

        Assert.Equal(@"PPSA99666 is already in your games (C:\root\games\PPSA99666).", viewModel.AlreadyAddedText);
        Assert.False(viewModel.AddCommand.CanExecute(null));
        viewModel.OpenFolderCommand.Execute(null);
        Assert.Equal([@"C:\root\games\PPSA99666"], _platform.OpenedPaths);
    }

    [Fact]
    public void A_stopped_add_says_it_resumes() =>
        Assert.Equal(
            "A previous add of this game stopped; adding it resumes where it stopped. The Intel option only applies if the conversion has not run yet.",
            Create(TestGameData.Prepared(resumes: true)).ResumeText);

    [Fact]
    public void The_intel_option_follows_the_cpu_and_goes_to_the_add()
    {
        var game = TestGameData.Prepared();
        var viewModel = Create(game, intel: true);

        Assert.True(viewModel.ToIntel);
        viewModel.AddCommand.Execute(null);

        Assert.Equal("adding:PPSA99666:True", _navigator.CurrentPage);
        Assert.False(game.IsDisposed);
    }

    [Fact]
    public async Task Back_disposes_the_game_and_returns_to_choose()
    {
        var game = TestGameData.Prepared();
        var viewModel = Create(game);

        await viewModel.BackCommand.ExecuteAsync(null);

        Assert.True(game.IsDisposed);
        Assert.Equal("choose", _navigator.CurrentPage);
    }

    [Fact]
    public async Task Releasing_after_the_add_started_leaves_the_game_to_the_add_page()
    {
        var game = TestGameData.Prepared();
        var viewModel = Create(game);
        viewModel.AddCommand.Execute(null);

        await viewModel.ReleaseAsync();

        Assert.False(game.IsDisposed);
    }
}
```

In `PageFactoryTests`, add the new pages to the test that resolves pages from the real container: `factory.ChooseGame()` is a `ChooseGameViewModel`; `factory.GameSummary(TestGameData.Prepared())` is a `GameSummaryViewModel`.

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: compile errors for the new members and types.

- [ ] **Step 3: Implement**

`IDialogService.cs`, add:

```csharp
    /// <param name="extension">The file extension to filter on, without the dot (e.g. "zip").</param>
    /// <returns>The chosen file's local path; null when the user cancels.</returns>
    Task<string?> PickFileAsync(string title, string extension);
```

`AvaloniaDialogService.cs`, add:

```csharp
    public async Task<string?> PickFileAsync(string title, string extension)
    {
        if (owner() is not { } window)
        {
            return null;
        }

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType($".{extension} files") { Patterns = [$"*.{extension}"] }],
        }).ConfigureAwait(true);
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }
```

`IPages.cs`, add (with `using PS5Kitsune.Core.Games;`):

```csharp
    object ChooseGame();

    /// <summary>Takes ownership of <paramref name="game"/> until it is added or the user goes back.</summary>
    object GameSummary(PreparedGame game);

    object AddGame(PreparedGame game, bool toIntel);

    object AddResult(GameAdded added);
```

`PageFactory.cs`, add (with `using PS5Kitsune.Core.Checks;` already there and `using PS5Kitsune.Core.Games;`):

```csharp
    public object ChooseGame() => new ChooseGameViewModel(Session.Get<IGameImporter>(), dialogs, navigator, this, busy => sessions.IsBusy = busy);

    public object GameSummary(PreparedGame game) => new GameSummaryViewModel(game, Session.Get<ICpuInfo>().Read().IsIntel, platform, navigator, this);

    // Task 5 replaces these two with the Adding and Result pages.
    public object AddGame(PreparedGame game, bool toIntel) => throw new NotSupportedException("The Adding page comes with Task 5.");

    public object AddResult(GameAdded added) => throw new NotSupportedException("The Result page comes with Task 5.");
```

`HomeViewModel.cs`:
- On `Status`, add `[NotifyCanExecuteChangedFor(nameof(AddGameCommand))]` and `[NotifyPropertyChangedFor(nameof(AddGameHint))]`; on `IsChecking`, add the same two attributes.
- Add:

```csharp
    public bool CanAddGame => !IsChecking && Status == AnyPS5InstallStatus.Installed;

    /// <summary>Why "Add a game" is disabled; null when it is enabled.</summary>
    public string? AddGameHint => CanAddGame ? null : "Install AnyPS5 first to add games.";

    [RelayCommand(CanExecute = nameof(CanAddGame))]
    private void AddGame() => navigator.GoTo(pages.ChooseGame());
```

`HomeView.axaml`: under the existing action button, add a button `Content="Add a game" Command="{Binding AddGameCommand}"` and a `TextBlock Text="{Binding AddGameHint}" Opacity="0.85" IsVisible="{Binding AddGameHint, Converter={x:Static ObjectConverters.IsNotNull}}"`, following the view's existing layout.

`src/PS5Kitsune.App/ViewModels/ChooseGameViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.App.ViewModels;

/// <summary>The first page of adding a game: a folder or zip, from the pickers or dropped on the page, read before anything is copied.</summary>
/// <param name="busyChanged">Told true while the game is read, so the data root cannot change meanwhile.</param>
public sealed partial class ChooseGameViewModel(IGameImporter importer, IDialogService dialogs, Navigator navigator, IPages pages, Action<bool>? busyChanged = null)
    : ObservableObject, IRunningWork
{
    private CancellationTokenSource? _cancellation;
    private Task _running = Task.CompletedTask;

    public string Title { get; } = "Add a game";

    public string Instructions { get; } = "Choose the game's folder or its .zip, in the DOOM for PS5 format, or drop it here.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    [NotifyCanExecuteChangedFor(nameof(ChooseFolderCommand), nameof(ChooseZipCommand), nameof(BackCommand))]
    public partial bool IsPreparing { get; private set; }

    /// <summary>Why the chosen path is not a game; null otherwise.</summary>
    [ObservableProperty]
    public partial string? Error { get; private set; }

    public bool IsRunning => IsPreparing;

    /// <summary>Reading a game is only cancelled, never confirmed.</summary>
    public string? QuitMessage => null;

    public bool CanChoose => !IsPreparing;

    /// <summary>Reads the game at <paramref name="path"/> (the pickers and the drop zone call it), then opens its summary.</summary>
    public Task OpenAsync(string path)
    {
        if (IsPreparing)
        {
            return Task.CompletedTask;
        }

        _running = PrepareAsync(path);
        return _running;
    }

    public async Task CancelAndWaitAsync()
    {
        _cancellation?.Cancel();
        await _running.ConfigureAwait(true);
    }

    private async Task PrepareAsync(string path)
    {
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        IsPreparing = true;
        Error = null;
        busyChanged?.Invoke(true);
        PreparedGame? game = null;
        try
        {
            game = await importer.PrepareAsync(path, cancellation.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (exception is InvalidGameSourceException or IOException or UnauthorizedAccessException)
        {
            Error = exception.Message;
        }
        finally
        {
            _cancellation = null;
            IsPreparing = false;
            busyChanged?.Invoke(false);
        }

        if (game is null)
        {
            return;
        }

        if (cancellation.IsCancellationRequested || !ReferenceEquals(navigator.CurrentPage, this))
        {
            await game.DisposeAsync().ConfigureAwait(true);
            return;
        }

        navigator.GoTo(pages.GameSummary(game));
    }

    [RelayCommand(CanExecute = nameof(CanChoose))]
    private async Task ChooseFolderAsync()
    {
        if (await dialogs.PickFolderAsync("Choose the game's folder", null).ConfigureAwait(true) is { } folder)
        {
            await OpenAsync(folder).ConfigureAwait(true);
        }
    }

    [RelayCommand(CanExecute = nameof(CanChoose))]
    private async Task ChooseZipAsync()
    {
        if (await dialogs.PickFileAsync("Choose the game's .zip", "zip").ConfigureAwait(true) is { } zip)
        {
            await OpenAsync(zip).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private void Cancel() => _cancellation?.Cancel();

    [RelayCommand(CanExecute = nameof(CanChoose))]
    private void Back() => navigator.GoHome(pages.Home());
}
```

`src/PS5Kitsune.App/ViewModels/GameSummaryViewModel.cs`:

```csharp
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.App.ViewModels;

/// <summary>What was read from the game, before anything is copied, and the Intel option. Owns the prepared game until the add starts.</summary>
public sealed partial class GameSummaryViewModel(PreparedGame game, bool cpuIsIntel, IPlatform platform, Navigator navigator, IPages pages)
    : ObservableObject, IPreparedGameOwner
{
    private bool _handedOver;

    public string Name => game.Metadata?.Name ?? "Unknown game";

    public string TitleId => game.Metadata?.TitleId ?? "";

    /// <summary>Full path of icon0.png; the view loads it into memory.</summary>
    public string? IconPath => game.IconPath;

    public string SizeText => string.Create(CultureInfo.InvariantCulture, $"{game.Validation.SizeBytes / 1048576.0:0.0} MB");

    public string ModulesText => game.Validation.ModuleDirectories.Count switch
    {
        0 => "No module folder",
        1 => "1 module folder",
        var count => string.Create(CultureInfo.InvariantCulture, $"{count} module folders"),
    };

    public string BinariesText
    {
        get
        {
            var self = game.Binaries.Where(binary => binary.Kind == BinaryKind.Self).ToList();
            if (self.Count == 0)
            {
                return "No SELF file.";
            }

            var start = self.Count == 1 ? "1 file is" : string.Create(CultureInfo.InvariantCulture, $"{self.Count} files are");
            var eboot = self.Any(binary => binary.RelativePath == GameSourceResolver.ExecutableName) && self.Count > 1 ? ", eboot.bin among them" : "";
            return $"{start} SELF and will be unwrapped{eboot}.";
        }
    }

    public IReadOnlyList<string> BlockingIssues { get; } =
        [.. game.Validation.Issues.Where(issue => issue.Severity == IssueSeverity.Blocking).Select(issue => issue.Message)];

    public IReadOnlyList<string> Warnings { get; } = [.. game.Validation.Warnings.Select(issue => issue.Message)];

    public string? AlreadyAddedText => game.AlreadyAdded ? $"{TitleId} is already in your games ({game.GameDirectory})." : null;

    public string? ResumeText => game.Resumes
        ? "A previous add of this game stopped; adding it resumes where it stopped. The Intel option only applies if the conversion has not run yet."
        : null;

    [ObservableProperty]
    public partial bool ToIntel { get; set; } = cpuIsIntel;

    [ObservableProperty]
    public partial string? OpenError { get; private set; }

    public bool CanAdd => !game.IsBlocked && !game.AlreadyAdded;

    public Task ReleaseAsync() => _handedOver ? Task.CompletedTask : game.DisposeAsync().AsTask();

    /// <summary>The Adding page owns the game from here.</summary>
    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void Add()
    {
        _handedOver = true;
        navigator.GoTo(pages.AddGame(game, ToIntel));
    }

    [RelayCommand]
    private async Task BackAsync()
    {
        await game.DisposeAsync().ConfigureAwait(true);
        navigator.GoBack();
    }

    [RelayCommand]
    private void OpenFolder() => OpenError = game.GameDirectory is { } folder ? PathOpener.TryOpen(platform, folder) : null;
}
```


`src/PS5Kitsune.App/Views/PathToBitmapConverter.cs`:

```csharp
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace PS5Kitsune.App.Views;

/// <summary>A file path to a bitmap read into memory, so the file can be deleted afterwards; null for a missing or invalid image.</summary>
public sealed class PathToBitmapConverter : IValueConverter
{
    public static readonly PathToBitmapConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || !File.Exists(path))
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(File.ReadAllBytes(path));
            return new Bitmap(stream);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
```

(If the build reports another exception type thrown by `Bitmap` for an invalid image in the analyzers' view, catch that type instead; never catch `Exception` unfiltered.)

`ChooseGameView.axaml` (`x:DataType="vm:ChooseGameViewModel"`, `DragDrop.AllowDrop="True"`): a `DockPanel` with the bottom row (Back on the left via `HorizontalAlignment`, or as the other pages do), and a `StackPanel Spacing="16"`: the title (`FontSize="20" FontWeight="SemiBold"`), the instructions, a `Border` drop zone (dashed or `BorderThickness="2"`, `BorderBrush="#40808080"`, `CornerRadius="6"`, `Padding="24"`, `MinHeight="120"`) holding the two buttons `Choose a folder…` / `Choose a .zip…` bound to `ChooseFolderCommand` / `ChooseZipCommand`; a row visible when `IsPreparing` with `TextBlock "Reading the game…"`, an indeterminate `ProgressBar Width="80"` and a `Cancel` button bound to `CancelCommand`; and the error `TextBlock` (`Foreground="#C42B1C"`, wrapped, visible when `Error` is not null).

`ChooseGameView.axaml.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using PS5Kitsune.App.ViewModels;

namespace PS5Kitsune.App.Views;

public sealed partial class ChooseGameView : UserControl
{
    public ChooseGameView()
    {
        InitializeComponent();
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private static string? SinglePath(DragEventArgs e) =>
        e.Data.GetFiles()?.ToList() is [var item] ? item.TryGetLocalPath() : null;

    private void OnDragOver(object? sender, DragEventArgs e) =>
        e.DragEffects = SinglePath(e) is null ? DragDropEffects.None : DragDropEffects.Copy;

    /// <summary>The same path the pickers give: the view model decides whether it is a game.</summary>
    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (SinglePath(e) is { } path && DataContext is ChooseGameViewModel viewModel)
        {
            await viewModel.OpenAsync(path).ConfigureAwait(true);
        }
    }
}
```

(`OpenAsync` catches the expected failures itself; if the analyzers reject `async void`, keep the handler synchronous and discard the task: `_ = viewModel.OpenAsync(path);`.)

`GameSummaryView.axaml` (`x:DataType="vm:GameSummaryViewModel"`): a header row with `Image Width="64" Height="64" Source="{Binding IconPath, Converter={x:Static views:PathToBitmapConverter.Instance}}"` (with a neutral `Border` placeholder visible when `IconPath` is null) beside the name (`FontSize="20" FontWeight="SemiBold"`) and the title ID; then `SizeText`, `ModulesText`, `BinariesText`; an `ItemsControl` for `BlockingIssues` (each `TextBlock` `Foreground="#C42B1C"`, wrapped) and one for `Warnings` (`Foreground="#9D5D00"`); the `AlreadyAddedText` line with an `Open its folder` button (both visible when `AlreadyAddedText` is not null); the `ResumeText` line (visible when not null); `OpenError` in red when not null; `CheckBox Content="Convert for an Intel CPU" IsChecked="{Binding ToIntel}"`; bottom row `Back` (`BackCommand`) and the accent `Add the game` (`AddCommand`).

`MainWindow.axaml`: add `DataTemplate`s for `vm:ChooseGameViewModel` → `views:ChooseGameView` and `vm:GameSummaryViewModel` → `views:GameSummaryView`, as the existing ones.

- [ ] **Step 4: Run the tests**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: all pass, 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add -A src tests
git commit -m "Choose a game and show its summary

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 5: App — Adding and Result

**Files:**
- Create: `src/PS5Kitsune.App/ViewModels/AddGameViewModel.cs`, `AddResultViewModel.cs`, `src/PS5Kitsune.App/Views/AddGameView.axaml(.cs)`, `AddResultView.axaml(.cs)`
- Modify: `src/PS5Kitsune.App/Shell/PageFactory.cs`, `src/PS5Kitsune.App/Views/MainWindow.axaml`
- Test: new `tests/PS5Kitsune.App.Tests/AddGameViewModelTests.cs`, new `AddResultViewModelTests.cs`, `PageFactoryTests.cs`, `MainWindowViewModelTests.cs`

**Interfaces:**
- Consumes: `StepRunViewModel` and its hooks, `IPreparedGameOwner` (Task 3); `IGameImporter`, `PreparedGame`, `GameAdded`, `AuditSummary`, `UnresolvedImport`, `AuditClass`, `GameImportException`, `AnyPS5NotInstalledException`, `StepFailedException` (Tasks 1–2); fakes from Task 4.
- Produces: `AddGameViewModel`, `AddResultViewModel`; `PageFactory.AddGame`/`AddResult` implemented.

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.App.Tests/AddGameViewModelTests.cs`:

```csharp
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.App.Tests;

public sealed class AddGameViewModelTests
{
    private readonly FakeGameImporter _importer = new();
    private readonly Navigator _navigator = new();
    private readonly PreparedGame _game = TestGameData.Prepared();
    private readonly List<bool> _busy = [];

    private AddGameViewModel Create(bool toIntel = false)
    {
        var viewModel = new AddGameViewModel(_importer, _game, toIntel, "app.log", new TestPlatform(), new InlineDispatcher(), _navigator, new FakePages(), _busy.Add);
        _navigator.GoTo(viewModel);
        return viewModel;
    }

    [Fact]
    public void Lists_the_six_steps_before_starting()
    {
        var viewModel = new AddGameViewModel(_importer, _game, false, "app.log", new TestPlatform(), new InlineDispatcher(), _navigator, new FakePages());

        Assert.Equal(_importer.StepNames, viewModel.Steps.Select(step => step.Name));
        Assert.Equal("Adding a game is in progress. Quitting cancels it; adding it again resumes where it stopped.", viewModel.QuitMessage);
    }

    [Fact]
    public async Task A_successful_add_disposes_the_game_and_opens_the_result()
    {
        var viewModel = Create(toIntel: true);

        await viewModel.RunAsync();

        Assert.Equal([(_game, true)], _importer.Adds);
        Assert.True(_game.IsDisposed);
        Assert.Equal("result:PPSA99666", _navigator.CurrentPage);
        Assert.Equal([true, false], _busy);
    }

    [Fact]
    public async Task A_running_step_shows_an_indeterminate_bar()
    {
        var release = new TaskCompletionSource<GameAdded>();
        _importer.Add = (_, _, _, onEvent, _) =>
        {
            onEvent!(new StepEvent(1, 6, "Copy the game", StepEventKind.Started));   // adapt to StepEvent's real constructor
            return release.Task;
        };
        var viewModel = Create();

        var running = viewModel.RunAsync();

        Assert.True(viewModel.ShowsProgress);
        Assert.True(viewModel.ProgressIndeterminate);
        release.SetResult(TestGameData.Added());
        await running;
    }

    [Fact]
    public async Task Cancelling_disposes_the_game_and_goes_home()
    {
        _importer.Add = (_, _, _, _, token) => Task.FromCanceled<GameAdded>(new CancellationToken(canceled: true));
        var viewModel = Create();

        await viewModel.RunAsync();

        Assert.True(_game.IsDisposed);
        Assert.Equal("home", _navigator.CurrentPage);
    }

    [Fact]
    public async Task A_failed_step_shows_its_summary_and_details_and_can_be_retried()
    {
        var failure = new GameImportException("The relinker failed with exit code 2.", "error: bad instruction");
        _importer.Add = (_, _, _, _, _) => throw new StepFailedException("Convert with the relinker", failure);   // adapt to StepFailedException's real constructor
        var viewModel = Create();

        await viewModel.RunAsync();

        Assert.Equal("Convert with the relinker failed: The relinker failed with exit code 2.", viewModel.Failure);
        Assert.Equal("error: bad instruction", viewModel.FailureDetails);
        Assert.True(viewModel.CanRetry);
        Assert.False(_game.IsDisposed);

        _importer.Add = (_, _, _, _, _) => Task.FromResult(TestGameData.Added());
        await viewModel.RetryCommand.ExecuteAsync(null);
        Assert.Equal("result:PPSA99666", _navigator.CurrentPage);
    }

    [Fact]
    public async Task Without_an_install_the_add_cannot_be_retried()
    {
        _importer.Add = (_, _, _, _, _) => throw new AnyPS5NotInstalledException();
        var viewModel = Create();

        await viewModel.RunAsync();

        Assert.Equal("AnyPS5 is not installed. Install it from Home first.", viewModel.Failure);
        Assert.False(viewModel.CanRetry);
        Assert.False(viewModel.RetryCommand.CanExecute(null));
    }

    [Fact]
    public async Task Back_to_home_after_a_failure_disposes_the_game()
    {
        _importer.Add = (_, _, _, _, _) => throw new AnyPS5NotInstalledException();
        var viewModel = Create();
        await viewModel.RunAsync();

        await viewModel.BackHomeCommand.ExecuteAsync(null);

        Assert.True(_game.IsDisposed);
        Assert.Equal("home", _navigator.CurrentPage);
    }

    [Fact]
    public async Task Releasing_disposes_the_game()
    {
        var viewModel = Create();

        await viewModel.ReleaseAsync();

        Assert.True(_game.IsDisposed);
    }
}
```

`tests/PS5Kitsune.App.Tests/AddResultViewModelTests.cs`:

```csharp
using PS5Kitsune.App.Navigation;
using PS5Kitsune.App.ViewModels;
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.App.Tests;

public sealed class AddResultViewModelTests
{
    private readonly Navigator _navigator = new();
    private readonly TestPlatform _platform = new();

    private AddResultViewModel Create(GameAdded added) => new(added, _platform, _navigator, new FakePages());

    private static AuditSummary Doom() => new(360, 4, 0, new Dictionary<string, int>())
    {
        Unresolved =
        [
            new UnresolvedImport("__tls_get_addr_nid_postfix", "libc.prx", AuditClass.Stub),
            new UnresolvedImport("fscanf_nid_postfix", "libSceLibcInternal.prx", AuditClass.Stub),
            new UnresolvedImport("sceSystemServiceLoadExec", "libSceSystemService.prx", AuditClass.Stub),
            new UnresolvedImport("__tls_get_addr_nid_postfix", "libkernel.prx", AuditClass.Stub),
        ],
    };

    [Fact]
    public void An_orange_audit_lists_its_stubs()
    {
        var viewModel = Create(TestGameData.Added(Doom()));

        Assert.Equal(AuditVerdict.Orange, viewModel.Verdict);
        Assert.Equal("Orange: the game loads but will stop if it calls an unimplemented function.", viewModel.VerdictText);
        Assert.Equal("360 implemented, 4 stubs, 0 absent", viewModel.CountsText);
        Assert.Null(viewModel.MissingLibrariesText);
        Assert.True(viewModel.HasUnresolved);
        Assert.Equal("Show the unresolved functions (4)", viewModel.UnresolvedHeader);
        Assert.Equal("sceSystemServiceLoadExec — libSceSystemService.prx (stub)", viewModel.UnresolvedLines[2]);
    }

    [Fact]
    public void A_green_audit_hides_the_list()
    {
        var viewModel = Create(TestGameData.Added(new AuditSummary(262, 0, 0, new Dictionary<string, int>())));

        Assert.Equal("Green: the game can load.", viewModel.VerdictText);
        Assert.False(viewModel.HasUnresolved);
    }

    [Fact]
    public void A_red_audit_names_missing_libraries_and_absent_functions()
    {
        var audit = new AuditSummary(250, 0, 1, new Dictionary<string, int> { ["libSceNpTrophy.prx"] = 3 })
        {
            Unresolved = [new UnresolvedImport("AbCdEfGhIjk", "", AuditClass.Absent)],
        };

        var viewModel = Create(TestGameData.Added(audit));

        Assert.Equal("Red: not playable yet; wait for an AnyPS5 update.", viewModel.VerdictText);
        Assert.Equal("Missing libraries: libSceNpTrophy.prx", viewModel.MissingLibrariesText);
        Assert.Equal(["AbCdEfGhIjk (absent)"], viewModel.UnresolvedLines);
    }

    [Fact]
    public void Shows_the_validation_warnings() =>
        Assert.Equal(
            ["Low disk space."],
            Create(TestGameData.Added(warnings: [new ValidationIssue(IssueSeverity.Warning, "Low disk space.")])).Warnings);

    [Fact]
    public void The_buttons_open_the_folder_add_another_or_go_home()
    {
        var viewModel = Create(TestGameData.Added());

        viewModel.OpenFolderCommand.Execute(null);
        Assert.Equal([@"C:\root\games\PPSA99666"], _platform.OpenedPaths);

        viewModel.AddAnotherCommand.Execute(null);
        Assert.Equal("choose", _navigator.CurrentPage);
        _navigator.GoBack();
        Assert.Equal("home", _navigator.CurrentPage);

        viewModel.BackHomeCommand.Execute(null);
        Assert.Equal("home", _navigator.CurrentPage);
    }
}
```

In `PageFactoryTests`, add: `factory.AddGame(TestGameData.Prepared(), false)` is an `AddGameViewModel`, `factory.AddResult(TestGameData.Added())` is an `AddResultViewModel`.

In `MainWindowViewModelTests`, add a test that an `AddGameViewModel` whose run is blocked (a `FakeGameImporter.Add` awaiting a `TaskCompletionSource` that honours the token) is the current page: `NeedsCloseHandling` is true; with `FakeDialogs.Confirm = true`, `ConfirmCloseAsync()` returns true, the dialog message is "Adding a game is in progress. Quitting cancels it; adding it again resumes where it stopped.", and the prepared game is disposed.

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: compile errors (`AddGameViewModel`, `AddResultViewModel`).

- [ ] **Step 3: Implement**

`src/PS5Kitsune.App/ViewModels/AddGameViewModel.cs`:

```csharp
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.App.ViewModels;

/// <summary>Runs the add steps when shown; owns the prepared game and disposes it when the add stops or the user leaves.</summary>
public sealed partial class AddGameViewModel : StepRunViewModel, IPreparedGameOwner
{
    private readonly IGameImporter _importer;
    private readonly PreparedGame _game;
    private readonly bool _toIntel;
    private readonly Navigator _navigator;
    private readonly IPages _pages;
    private GameAdded? _added;

    public AddGameViewModel(
        IGameImporter importer, PreparedGame game, bool toIntel, string logFile, IPlatform platform, IUiDispatcher ui, Navigator navigator, IPages pages, Action<bool>? busyChanged = null)
        : base(logFile, platform, ui, busyChanged)
    {
        _importer = importer;
        _game = game;
        _toIntel = toIntel;
        _navigator = navigator;
        _pages = pages;
        foreach (var name in StepNames())
        {
            Steps.Add(new InstallStepViewModel(name));
        }
    }

    public string Title => $"Adding {_game.Metadata?.Name}";

    public override string? QuitMessage { get; } = "Adding a game is in progress. Quitting cancels it; adding it again resumes where it stopped.";

    /// <summary>Copying, unwrapping and converting report nothing measurable.</summary>
    protected override bool IndeterminateOnStart => true;

    public Task ReleaseAsync() => _game.DisposeAsync().AsTask();

    protected override IReadOnlyList<string> StepNames() => _importer.StepNames;

    protected override async Task RunCoreAsync(IProgress<StepProgress> progress, Action<StepEvent> onEvent, CancellationToken cancellationToken) =>
        _added = await _importer.AddAsync(_game, _toIntel, progress, onEvent, cancellationToken).ConfigureAwait(true);

    protected override (string Summary, string? Details) SplitFailure(Exception exception) => exception switch
    {
        AnyPS5NotInstalledException => ("AnyPS5 is not installed. Install it from Home first.", null),
        StepFailedException { InnerException: GameImportException failure } failed =>
            ($"{failed.StepName} failed: {failure.Summary}", string.IsNullOrEmpty(failure.Details) ? null : failure.Details),
        StepFailedException failed => ($"{failed.StepName} failed: {failed.InnerException?.Message ?? failed.Message}", null),
        _ => ($"Adding the game failed: {exception.Message}", null),
    };

    protected override bool CanRetryAfter(Exception exception) => exception is not AnyPS5NotInstalledException;

    protected override async Task OnSucceededAsync()
    {
        await _game.DisposeAsync().ConfigureAwait(true);
        _navigator.GoTo(_pages.AddResult(_added!));
    }

    protected override async Task OnCancelledAsync()
    {
        await _game.DisposeAsync().ConfigureAwait(true);
        _navigator.GoHome(_pages.Home());
    }

    protected override async Task LeaveAsync()
    {
        await _game.DisposeAsync().ConfigureAwait(true);
        _navigator.GoHome(_pages.Home());
    }
}
```

`src/PS5Kitsune.App/ViewModels/AddResultViewModel.cs`:

```csharp
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS5Kitsune.App.Navigation;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.App.ViewModels;

/// <summary>The audit verdict of the game just added, with what AnyPS5 does not implement yet.</summary>
public sealed partial class AddResultViewModel(GameAdded added, IPlatform platform, Navigator navigator, IPages pages) : ObservableObject
{
    public string Title => $"{added.Record.Name} is added";

    public AuditVerdict Verdict => added.Audit.Verdict;

    public bool IsGreen => Verdict == AuditVerdict.Green;

    public bool IsOrange => Verdict == AuditVerdict.Orange;

    public bool IsRed => Verdict == AuditVerdict.Red;

    public string VerdictText => Verdict switch
    {
        AuditVerdict.Green => "Green: the game can load.",
        AuditVerdict.Orange => "Orange: the game loads but will stop if it calls an unimplemented function.",
        _ => "Red: not playable yet; wait for an AnyPS5 update.",
    };

    public string CountsText => string.Create(
        CultureInfo.InvariantCulture, $"{added.Audit.Implemented} implemented, {added.Audit.Stub} stubs, {added.Audit.Absent} absent");

    public string? MissingLibrariesText => added.Audit.MissingLibraries.Count == 0
        ? null
        : "Missing libraries: " + string.Join(", ", added.Audit.MissingLibraries.Keys);

    public bool HasUnresolved => added.Audit.Unresolved.Count > 0;

    public string UnresolvedHeader => string.Create(CultureInfo.InvariantCulture, $"Show the unresolved functions ({added.Audit.Unresolved.Count})");

    public IReadOnlyList<string> UnresolvedLines { get; } = [.. added.Audit.Unresolved.Select(Line)];

    public IReadOnlyList<string> Warnings { get; } = [.. added.Warnings.Select(issue => issue.Message)];

    [ObservableProperty]
    public partial string? OpenError { get; private set; }

    private static string Line(UnresolvedImport import)
    {
        var kind = import.Class == AuditClass.Stub ? "stub" : "absent";
        return import.Where.Length == 0 ? $"{import.Name} ({kind})" : $"{import.Name} — {import.Where} ({kind})";
    }

    [RelayCommand]
    private void OpenFolder() => OpenError = PathOpener.TryOpen(platform, added.GameDirectory);

    [RelayCommand]
    private void AddAnother()
    {
        navigator.GoHome(pages.Home());
        navigator.GoTo(pages.ChooseGame());
    }

    [RelayCommand]
    private void BackHome() => navigator.GoHome(pages.Home());
}
```

`PageFactory.cs` — replace the two placeholders:

```csharp
    public object AddGame(PreparedGame game, bool toIntel) =>
        new AddGameViewModel(Session.Get<IGameImporter>(), game, toIntel, Session.LogFile, platform, ui, navigator, this, busy => sessions.IsBusy = busy);

    public object AddResult(GameAdded added) => new AddResultViewModel(added, platform, navigator, this);
```

`AddGameView.axaml` (`x:DataType="vm:AddGameViewModel"`): copy `InstallView.axaml` (step list, progress bar and text, log, failure line with the "Show details" expander, Cancel / Retry / Open log / Back to home) and change only the title binding to `{Binding Title}`; bind Retry's visibility to `HasFailed` and its command to `RetryCommand` (its `CanExecute` follows `CanRetry`). If `InstallView` hard-codes the title text, keep Install's as it is.

`AddResultView.axaml` (`x:DataType="vm:AddResultViewModel"`): the title (`FontSize="20" FontWeight="SemiBold"`); `VerdictText` as three `TextBlock`s with `Foreground` `#0F7B0F` / `#9D5D00` / `#C42B1C`, visible on `IsGreen` / `IsOrange` / `IsRed` (`FontWeight="SemiBold"`, wrapped); `CountsText`; `MissingLibrariesText` in red when not null; an `Expander Header="{Binding UnresolvedHeader}" IsVisible="{Binding HasUnresolved}"` holding an `ItemsControl` of `UnresolvedLines` in a monospace font (`FontFamily="Cascadia Mono, Consolas, monospace"`), selectable if `SelectableTextBlock` is used elsewhere; an `ItemsControl` of `Warnings` in orange; `OpenError` in red; bottom row `Open the game folder`, `Add another game`, accent `Back to home`.

`MainWindow.axaml`: add `DataTemplate`s for `vm:AddGameViewModel` → `views:AddGameView` and `vm:AddResultViewModel` → `views:AddResultView`.

- [ ] **Step 4: Run the tests**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: all pass, 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add -A src tests
git commit -m "Add the game with progress and show the verdict

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```

---

### Task 6: Docs

**Files:**
- Modify: `docs/architecture.md`, `README.md`

- [ ] **Step 1: `docs/architecture.md`**

- Roadmap entry 6 ("V2 — Add-a-game screens"): split it into **6a — Add a game from the app** (spec link written like 5b's and 5c's: `docs/superpowers/specs/2026-10-22-milestone-6a-add-game-screens-design.md`) with the criterion "DOOM added from the UI with the same verdict and counts as the CLI; an already added game and a folder that is not a game are recognized", and **6b — Reconvert** (Core, CLI, app) with the existing criterion "Reconvert keeps `data\` and `_trophy\`". Remove "green audit".
- "V2 — Add a game" → "Screens": rewrite the five items as built:
  1. Choose a game: "Choose a folder…", "Choose a .zip…", or drop one on the page; read before anything is copied.
  2. Summary: icon, name, title ID, size, module folders, SELF files to unwrap, blocking issues in red and warnings in orange, an already added game, the Intel option (pre-ticked from the CPU). Copy only; Move comes with the library (milestone 7).
  3. Adding: the add steps with progress and log, Cancel and Retry; adding again resumes.
  4. Result: the verdict in its colour, the counts, missing libraries, and the unresolved functions (stubs and absent) in an expander.
  - Drop the "Add to library" button sentence: the game is registered when `game.json` is written.
- Flow step 4 ("Create the game folder … Move is only offered …"): add "(Move is deferred to milestone 7.)".

- [ ] **Step 2: `README.md`**

Where the README describes adding a game (`ps5kitsune add`), add one sentence that the app does it too: "In the app, Home's "Add a game" takes a folder or a .zip (picked or dropped), shows its summary, adds it and shows the audit verdict." Update the milestone status line to say milestone 6a is in progress, matching how 5c was written.

- [ ] **Step 3: Commit**

```bash
git add docs/architecture.md README.md
git commit -m "Document adding a game from the app

Co-Authored-By: <your model name> <noreply@anthropic.com>"
```
