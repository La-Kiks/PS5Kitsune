# Milestone 2 — end-to-end CLI path: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `ps5kitsune install`, `ps5kitsune add <DOOM folder or zip>` and `ps5kitsune run PPSA99666` run DOOM for PS5 from the command line, using the release mode of profile `doom-validated-2026-10` (AnyPS5 `v0.1.2`).

**Architecture:** Long operations are lists of `IStep` run by a `StepRunner`. A step decides whether it is already done by looking at what it produces, so an interrupted command resumes. `AnyPS5Installer` and `GameImporter` in Core build these lists. `IGameLauncher` (Core) is implemented by `WindowsGameLauncher` (Platform.Windows), which starts the game with `CreateProcess`. The game's stdout and stderr are an inherited handle to the log file, and `LogTail` (Core) follows that file. The CLI only wires these together, so the UI can reuse them later.

**Tech Stack:** .NET 10, C# latest, System.CommandLine 2.0.13, CliWrap, Microsoft.Extensions.DependencyInjection/Logging + NLog, System.Text.Json, System.IO.Compression, xUnit v3 (Microsoft.Testing.Platform). No new NuGet package.

**Spec:** [docs/superpowers/specs/2026-10-19-milestone-2-cli-path-design.md](../specs/2026-10-19-milestone-2-cli-path-design.md). Task 1 amends it with the refinements below.

## Refinements of the spec (decided while planning)

1. **Python scripts run through a bootstrap.** `import_audit.py` imports `nid_names` and `progress` from its own folder. The embeddable Python's `python314._pth` keeps the script folder off `sys.path` and makes Python ignore `PYTHONPATH`. Run directly, the script fails with `ModuleNotFoundError`; this was checked on 2026-10-20 with the pinned 3.14.8. `PythonEmbeddableTool.CreateScriptSpec` runs `python -c <bootstrap> <script> <args>`, where the bootstrap puts the script's folder first on `sys.path` and calls `runpy.run_path`. Both the import and the exit code are kept, which was also checked.
2. **Only SELF files are unwrapped.** `unwrap_self.py` accepts the magics `4F 15 3D 1D` and `54 14 F5 EE`. Any other non-ELF file in a module folder is left as is.
3. **`install.json` stores the unwrap script URL**, which contains the pinned commit, instead of a separate commit field.
4. **Two generic download steps.** `DownloadFileStep` covers the relinker and `unwrap_self.py`; `DownloadArchiveStep` covers the libs and the scripts. Step names stay distinct.
5. **The CLI shows steps through the log.** `StepRunner` logs start, skip, end and failure at Information level, and the CLI console prints Information and up. The `onEvent` callback exists for the UI; the CLI passes none.
6. **Staging folder of a zip.** `add` extracts the zip again every time. The extracted folder is deleted once the copy step is done (even if a later step fails), and kept if the copy did not complete.
7. **`AuditStep` always runs.** It is quick, and its result fills the game record.
8. **A title ID contains only ASCII letters and digits.** It becomes a folder name, so anything else is a blocking error.
9. **`run`, Ctrl+C:** the CLI stops following the log, prints the PID and the log path, and returns 130.

## Global constraints

- Target `net10.0` everywhere (Platform.Windows too, so the Linux CI job compiles it). `Nullable` enabled, `TreatWarningsAsErrors` true, `AnalysisLevel` latest-recommended, `EnforceCodeStyleInBuild` true (see `Directory.Build.props`). A warning breaks the build.
- `PS5Kitsune.Core` has no Windows API and no UI reference. Windows-only code lives in `PS5Kitsune.Platform.Windows`, marked `[SupportedOSPlatform("windows")]`.
- No new NuGet package. Everything shipped stays GPLv2-compatible (`build/allowed-licenses.json`).
- Code style, matching the existing code: file-scoped namespaces matching folders, `sealed` classes, primary constructors, `var`. Private fields are `_camelCase`; constants and static readonly fields are PascalCase. Non-capturing local functions are `static` (warning). Logging goes through `[LoggerMessage]` source-generated partial methods. Core and Platform `await`s use `.ConfigureAwait(false)`, while CLI and tests do not. Public methods call `ArgumentNullException.ThrowIfNull` on reference parameters they dereference.
- Profile `doom-validated-2026-10`: AnyPS5 tag `v0.1.2`. The source archive is `https://github.com/boykopovar/AnyPS5/archive/refs/tags/v0.1.2.zip`, SHA-256 `90c802352b5478beacfdf032b57649872ccf0b7dc18c9c868b35903378ad2545`. `unwrap_self.py` is pinned at commit `360e8bd685d32b0e9d37e6ee2fae44417c0f808c`, SHA-256 `2f9321fa41009411b9cd07730f68094967c80a6c0c31d801b31d55141c291ef7`.
- Root layout: `<root>/anyps5/current/{relinker.exe, libs/, scripts/, install.json}`, `<root>/games/<titleId>/{app0/, libs/, <titleId>.exe, *.registry.json, audit.json, .ps5kitsune/{game.json, originals/, logs/}}`, `<root>/downloads/staging/`, `<root>/logs/`. The default root is `C:\PS5Kitsune`.
- CLI exit codes: 0 success (`add` even with an orange or red verdict), 1 failure, 130 cancelled. `run` returns the game's exit code.
- Tests use xUnit v3 with `TestContext.Current.CancellationToken`, temporary folders (`TempDirectory`) and fakes. No DOOM file is ever added to the repository.
- Commits: the repo-local git config already uses the `La-Kiks` noreply address; never commit with another address. Every commit message ends with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. **Never push.**
- Commands: run them from the repository root, in Git Bash. Build with `dotnet build PS5Kitsune.slnx -c Release` and run every test with `dotnet test --solution PS5Kitsune.slnx -c Release`. To run one class: `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*ClassName"`. Test output is in French on the dev machine ("réussite", "échec").

## File map

| File | Responsibility | Task |
| --- | --- | --- |
| `tests/PS5Kitsune.Core.Tests/{TestFiles,Fakes/FakePlatform,Fakes/FakeProcessRunner,Fakes/FakeDownloader}.cs` | Shared test builders and fakes | 1 |
| `src/PS5Kitsune.Core/Tools/PythonEmbeddableTool.cs` | `CreateScriptSpec` (bootstrap) | 1 |
| `src/PS5Kitsune.Core/Profiles/{AnyPS5Profile,ProfileLoader}.cs`, `default-profile.json` | Asset URLs, `include`, new assets | 2 |
| `src/PS5Kitsune.Core/Pipeline/{IStep,StepProgress,StepEvent,StepFailedException,StepRunner}.cs` | Step pipeline | 3 |
| `src/PS5Kitsune.Core/IO/{FileHash,ZipExtraction,FileTree,JsonFiles}.cs` | Hashing, filtered extraction, tree copy, JSON records | 4 |
| `src/PS5Kitsune.Core/AnyPS5/*.cs`, `AnyPS5/Steps/*.cs` | AnyPS5 install | 5 |
| `src/PS5Kitsune.Cli/{Program,CommandHost,ToolsCommand,InstallCommand,ConsoleStepProgress}.cs` | CLI structure, `install` | 6 |
| `src/PS5Kitsune.Core/Games/{GameSource,GameSourceResolver}.cs` | Folder or zip → game root | 7 |
| `src/PS5Kitsune.Core/Games/{GameMetadata,ParamJson,GameValidation,GameValidator}.cs` | Validation | 8 |
| `src/PS5Kitsune.Core/Checks/CpuVendor.cs`, `Games/{GameLayout,GameImportContext,GameImportException}.cs`, `Games/Steps/{CopyGameStep,CopyLibsStep}.cs` | Game folder, copies | 9 |
| `src/PS5Kitsune.Core/Processes/OutputTail.cs`, `Games/BinaryKinds.cs`, `Games/Steps/UnwrapSelfStep.cs` | Unwrapping | 10 |
| `src/PS5Kitsune.Core/Games/AuditSummary.cs`, `Games/Steps/{RelinkStep,AuditStep}.cs` | Conversion, audit, verdict | 11 |
| `src/PS5Kitsune.Core/Library/{GameRecord,GameRecordStore}.cs`, `Games/Steps/WriteGameRecordStep.cs` | `game.json` | 12 |
| `src/PS5Kitsune.Core/Games/{AddGameResult,GameImporter}.cs`, `src/PS5Kitsune.Cli/AddCommand.cs` | `add` | 13 |
| `src/PS5Kitsune.Core/Abstractions/IGameLauncher.cs`, `Library/LogTail.cs`, `src/PS5Kitsune.Platform.Windows/{WindowsGameLauncher,WindowsCommandLine}.cs`, `tests/PS5Kitsune.Platform.Windows.Tests/` | Launching, following the log | 14 |
| `src/PS5Kitsune.Core/Library/GameLibrary.cs`, `src/PS5Kitsune.Cli/RunCommand.cs` | `run` | 15 |
| `default-profile.json`, `docs/architecture.md`, `README.md` | End-to-end validation with DOOM | 16 |

---

### Task 1: Shared test fakes and the Python script bootstrap

**Files:**
- Create: `tests/PS5Kitsune.Core.Tests/TestFiles.cs`
- Create: `tests/PS5Kitsune.Core.Tests/Fakes/FakePlatform.cs`
- Create: `tests/PS5Kitsune.Core.Tests/Fakes/FakeProcessRunner.cs`
- Create: `tests/PS5Kitsune.Core.Tests/Fakes/FakeDownloader.cs`
- Modify: `tests/PS5Kitsune.Core.Tests/Tools/PythonEmbeddableToolTests.cs` (whole file)
- Modify: `src/PS5Kitsune.Core/Tools/PythonEmbeddableTool.cs`
- Modify: `docs/superpowers/specs/2026-10-19-milestone-2-cli-path-design.md`

**Interfaces:**
- Produces: `TestFiles.Elf()`, `TestFiles.Self()` (byte arrays with the magics, sizes 64 and 128), `TestFiles.Write(string path, byte[] | string)`, `TestFiles.ZipBytes(params (string Name, byte[] Content)[])`, `TestFiles.Sha256(byte[])`; `FakePlatform` (suffix `.exe`); `FakeProcessRunner` with `Calls`, `LastSpec`, `Respond: Func<ProcessSpec, FakeProcessResult>` and `static string? ScriptName(ProcessSpec)`; `FakeProcessResult(int ExitCode, IReadOnlyList<string>? StandardOutput = null, IReadOnlyList<string>? StandardError = null)`; `FakeDownloader` with `Requests` and `Serve(Uri, byte[])`.
- Produces: `PythonEmbeddableTool.CreateScriptSpec(string script, IEnumerable<string> arguments) : ProcessSpec`. The arguments are `["-c", <bootstrap>, script, ..arguments]`.

- [ ] **Step 1: Create the shared test helpers**

`tests/PS5Kitsune.Core.Tests/TestFiles.cs`:

```csharp
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace PS5Kitsune.Core.Tests;

/// <summary>Builds the small files the tests need: ELF and SELF stand-ins, text files, zips.</summary>
internal static class TestFiles
{
    /// <summary>64 bytes starting with the ELF magic 7F 45 4C 46.</summary>
    public static byte[] Elf() => [0x7F, 0x45, 0x4C, 0x46, .. new byte[60]];

    /// <summary>128 bytes starting with the SELF magic 4F 15 3D 1D; a different size from <see cref="Elf"/>.</summary>
    public static byte[] Self() => [0x4F, 0x15, 0x3D, 0x1D, .. new byte[124]];

    public static void Write(string path, byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    public static void Write(string path, string content) => Write(path, Encoding.UTF8.GetBytes(content));

    public static byte[] ZipBytes(params (string Name, byte[] Content)[] entries)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var stream = archive.CreateEntry(name).Open();
                stream.Write(content);
            }
        }

        return buffer.ToArray();
    }

    public static string Sha256(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));
}
```

`tests/PS5Kitsune.Core.Tests/Fakes/FakePlatform.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Tests.Fakes;

internal sealed class FakePlatform : IPlatform
{
    public string Name => "Test";

    public string DefaultDataRoot => "unused";

    public string SettingsDirectory => "unused";

    public string ExecutableSuffix => ".exe";
}
```

`tests/PS5Kitsune.Core.Tests/Fakes/FakeProcessRunner.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Tests.Fakes;

internal sealed record FakeProcessResult(int ExitCode, IReadOnlyList<string>? StandardOutput = null, IReadOnlyList<string>? StandardError = null);

/// <summary>Records every command; <see cref="Respond"/> plays the tool (it may create files) and gives the result.</summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    public List<ProcessSpec> Calls { get; } = [];

    public ProcessSpec? LastSpec => Calls.LastOrDefault();

    public Func<ProcessSpec, FakeProcessResult> Respond { get; set; } = _ => new FakeProcessResult(0);

    /// <summary>File name of the script for a command built by PythonEmbeddableTool.CreateScriptSpec, else null.</summary>
    public static string? ScriptName(ProcessSpec spec) =>
        spec.Arguments is ["-c", _, var script, ..] ? Path.GetFileName(script) : null;

    public Task<ProcessResult> RunAsync(ProcessSpec spec, Action<OutputLine>? onOutput = null, CancellationToken cancellationToken = default)
    {
        Calls.Add(spec);
        var result = Respond(spec);
        foreach (var line in result.StandardOutput ?? [])
        {
            onOutput?.Invoke(new OutputLine(OutputSource.StandardOutput, line));
        }

        foreach (var line in result.StandardError ?? [])
        {
            onOutput?.Invoke(new OutputLine(OutputSource.StandardError, line));
        }

        return Task.FromResult(new ProcessResult(result.ExitCode, TimeSpan.Zero));
    }
}
```

`tests/PS5Kitsune.Core.Tests/Fakes/FakeDownloader.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Tests.Fakes;

/// <summary>Writes registered content instead of downloading, and records every request.</summary>
internal sealed class FakeDownloader : IDownloader
{
    private readonly Dictionary<Uri, byte[]> _content = new();

    public List<DownloadRequest> Requests { get; } = [];

    public void Serve(Uri url, byte[] content) => _content[url] = content;

    public Task DownloadAsync(DownloadRequest request, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        if (!_content.TryGetValue(request.Source, out var content))
        {
            throw new HttpRequestException($"404: {request.Source}");
        }

        TestFiles.Write(request.Destination, content);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Rewrite `PythonEmbeddableToolTests` on the shared fakes and add the failing bootstrap test**

Replace the whole content of `tests/PS5Kitsune.Core.Tests/Tools/PythonEmbeddableToolTests.cs`:

```csharp
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.Tests.Tools;

public sealed class PythonEmbeddableToolTests : IDisposable
{
    private static readonly ToolDefinition Definition = new("3.14.8", new Uri("https://example.test/python-embed.zip"), new string('a', 64));

    private readonly TempDirectory _temp = new();
    private readonly FakeDownloader _downloader = new();
    private readonly FakeProcessRunner _processRunner = new();

    public PythonEmbeddableToolTests()
    {
        _downloader.Serve(Definition.Url, TestFiles.ZipBytes(("python.exe", "fake"u8.ToArray()), ("python314.zip", [])));
    }

    private PythonEmbeddableTool CreateTool() =>
        new(Definition, new DataLayout(_temp.Path), new FakePlatform(), _downloader, _processRunner);

    [Fact]
    public async Task Install_extracts_the_archive_into_tools_python()
    {
        var tool = CreateTool();
        Assert.False(tool.IsInstalled());

        await tool.InstallAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(tool.IsInstalled());
        Assert.True(File.Exists(_temp.Combine("tools", "python", "python.exe")));
        Assert.False(Directory.Exists(_temp.Combine("tools", "python.staging")));
        Assert.Equal(Definition.Sha256, _downloader.Requests.Single().ExpectedSha256);
    }

    [Fact]
    public async Task Install_does_nothing_when_already_installed()
    {
        var tool = CreateTool();
        await tool.InstallAsync(cancellationToken: TestContext.Current.CancellationToken);

        await tool.InstallAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(_downloader.Requests);
    }

    [Fact]
    public async Task A_tool_installed_from_another_archive_is_not_installed()
    {
        await CreateTool().InstallAsync(cancellationToken: TestContext.Current.CancellationToken);

        var newer = new PythonEmbeddableTool(
            Definition with { Sha256 = new string('b', 64) },
            new DataLayout(_temp.Path), new FakePlatform(), _downloader, _processRunner);

        Assert.False(newer.IsInstalled());
    }

    [Fact]
    public async Task A_deleted_executable_means_not_installed()
    {
        var tool = CreateTool();
        await tool.InstallAsync(cancellationToken: TestContext.Current.CancellationToken);

        File.Delete(tool.Executable);

        Assert.False(tool.IsInstalled());
    }

    [Fact]
    public async Task GetVersion_parses_the_python_banner()
    {
        _processRunner.Respond = _ => new FakeProcessResult(0, ["Python 3.14.8"]);

        var version = await CreateTool().GetVersionAsync(TestContext.Current.CancellationToken);

        Assert.Equal("3.14.8", version);
        Assert.Equal(["--version"], _processRunner.LastSpec!.Arguments);
    }

    [Fact]
    public async Task GetVersion_fails_on_a_non_zero_exit_code()
    {
        _processRunner.Respond = _ => new FakeProcessResult(9009);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateTool().GetVersionAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Scripts_run_through_a_bootstrap_that_keeps_their_folder_importable()
    {
        var tool = CreateTool();
        var script = _temp.Combine("scripts", "tools", "import_audit.py");

        var spec = tool.CreateScriptSpec(script, ["a.registry.json", "--libs", "libs"]);

        Assert.Equal(tool.Executable, spec.FileName);
        Assert.Equal("-c", spec.Arguments[0]);
        Assert.Contains("sys.path.insert(0", spec.Arguments[1], StringComparison.Ordinal);
        Assert.Contains("runpy.run_path", spec.Arguments[1], StringComparison.Ordinal);
        Assert.Equal([script, "a.registry.json", "--libs", "libs"], spec.Arguments.Skip(2));
        Assert.Equal("import_audit.py", FakeProcessRunner.ScriptName(spec));
    }

    public void Dispose() => _temp.Dispose();
}
```

- [ ] **Step 3: Run the tests to verify the new one fails**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*PythonEmbeddableToolTests"`
Expected: the build fails with CS1061, `'PythonEmbeddableTool' does not contain a definition for 'CreateScriptSpec'`.

- [ ] **Step 4: Add `CreateScriptSpec` to `PythonEmbeddableTool`**

In `src/PS5Kitsune.Core/Tools/PythonEmbeddableTool.cs`, add these members right after `public const string ToolId = "python";`:

```csharp
    // The ._pth file of the embeddable distribution keeps the script's folder off sys.path and makes
    // Python ignore PYTHONPATH, so a script importing its siblings (import_audit.py imports nid_names
    // and progress) fails when run directly. This bootstrap puts the folder back first, then runs the
    // script as __main__; its SystemExit code becomes the process exit code.
    private const string ScriptBootstrap =
        "import os,runpy,sys;sys.argv=sys.argv[1:];sys.path.insert(0,os.path.dirname(os.path.abspath(sys.argv[0])));runpy.run_path(sys.argv[0],run_name='__main__')";
```

and this method after the `RequiredFile` property:

```csharp
    /// <summary>A command running <paramref name="script"/> with <paramref name="arguments"/>; the script's exit code is the process exit code.</summary>
    public ProcessSpec CreateScriptSpec(string script, IEnumerable<string> arguments) =>
        new(Executable, ["-c", ScriptBootstrap, script, .. arguments]);
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*PythonEmbeddableToolTests"`
Expected: 7 tests, all pass.

- [ ] **Step 6: Amend the spec with the planning refinements**

In `docs/superpowers/specs/2026-10-19-milestone-2-cli-path-design.md`:

(a) In the "Facts checked" table, right after the row that starts with `` | `unwrap_self.py` | **Not in `v0.1.2`.** ``, insert:

```markdown
| Embeddable Python and sibling imports | `import_audit.py` imports `nid_names` and `progress` from `tools/`. The `python314._pth` file keeps the script folder off `sys.path` and disables `PYTHONPATH`, so run directly the script fails with `ModuleNotFoundError` (checked 2026-10-20 with 3.14.8). Scripts are run as `python -c <bootstrap> <script> <args>`; the bootstrap puts the script folder first and calls `runpy.run_path`, keeping the exit code. |
| SELF magics | `unwrap_self.py` accepts `4F 15 3D 1D` and `54 14 F5 EE`. |
```

(b) Replace the sentence

```text
For each non-ELF file, back it up to `<game>/.ps5kitsune/originals/<relative path>` (outside `app0`), then run `python unwrap_self.py <backup> <original path>`. A refusal (encrypted container) fails with "the game must come already unwrapped from the dump tool". Done when every file is an ELF.
```

with

```text
For each SELF file (either magic above), back it up to `<game>/.ps5kitsune/originals/<relative path>` (outside `app0`), then run `unwrap_self.py <backup> <original path>`. Other non-ELF files are left as they are. A refusal (encrypted container) fails with "the game must come already unwrapped from the dump tool". Done when no SELF file remains and no backup lacks its unwrapped copy.
```

(c) Replace `` `install.json` (`schemaVersion`, profile id, mode `release`, version, unwrap script commit, date) `` with `` `install.json` (`schemaVersion`, profile id, mode `release`, version, unwrap script URL holding the commit, date) ``.

- [ ] **Step 7: Run the whole test suite**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: every test passes.

- [ ] **Step 8: Commit**

```bash
git add tests/PS5Kitsune.Core.Tests src/PS5Kitsune.Core/Tools/PythonEmbeddableTool.cs docs/superpowers/specs/2026-10-19-milestone-2-cli-path-design.md
git commit -m "Run Python scripts through a sys.path bootstrap

The embeddable Python's ._pth file keeps a script's folder off sys.path,
so import_audit.py cannot import nid_names and progress when run directly.
Also share the test fakes between test classes.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Profile assets with direct URLs and include lists

**Files:**
- Modify: `src/PS5Kitsune.Core/Profiles/AnyPS5Profile.cs`
- Modify: `src/PS5Kitsune.Core/Profiles/ProfileLoader.cs`
- Modify: `src/PS5Kitsune.Core/Profiles/default-profile.json`
- Test: `tests/PS5Kitsune.Core.Tests/Profiles/ProfileLoaderTests.cs`

**Interfaces:**
- Produces: `ProfileAsset(string? Name, string Sha256, Uri? Url = null, IReadOnlyList<string>? Include = null)`; `ProfileAssetKeys.{Relinker, Libs, Scripts, UnwrapScript}` (`"relinker"`, `"libs"`, `"scripts"`, `"unwrapScript"`); `AnyPS5Profile.RepositoryUrl`; `AnyPS5Profile.Asset(string key) : ProfileAsset`; `AnyPS5Profile.AssetUrl(string key) : Uri`; `ProfileSource.Version : string` (tag, else SHA).

- [ ] **Step 1: Write the failing tests**

Append these tests to `ProfileLoaderTests` (before `private static AnyPS5Profile Load`):

```csharp
    [Fact]
    public void Embedded_profile_resolves_its_assets()
    {
        var profile = ProfileLoader.LoadEmbedded();

        Assert.Equal(
            new Uri("https://github.com/boykopovar/AnyPS5/releases/download/v0.1.2/relinker-v0.1.2.exe"),
            profile.AssetUrl(ProfileAssetKeys.Relinker));
        Assert.Equal(
            new Uri("https://github.com/boykopovar/AnyPS5/releases/download/v0.1.2/prx-windows-v0.1.2.zip"),
            profile.AssetUrl(ProfileAssetKeys.Libs));
        Assert.Equal(new Uri("https://github.com/boykopovar/AnyPS5/archive/refs/tags/v0.1.2.zip"), profile.AssetUrl(ProfileAssetKeys.Scripts));
        Assert.Equal(["tools/", "core/libs/prx/"], profile.Asset(ProfileAssetKeys.Scripts).Include!);
        Assert.Contains("360e8bd685d32b0e9d37e6ee2fae44417c0f808c", profile.AssetUrl(ProfileAssetKeys.UnwrapScript).AbsolutePath, StringComparison.Ordinal);
        Assert.Equal("v0.1.2", profile.Source.Version);
    }

    [Fact]
    public void An_unknown_asset_is_an_invalid_profile()
    {
        Assert.Throws<InvalidProfileException>(() => ProfileLoader.LoadEmbedded().AssetUrl("missing"));
    }

    [Fact]
    public void Rejects_an_asset_with_neither_url_nor_name()
    {
        Assert.Throws<InvalidProfileException>(() => Load($$"""
            {
              "schemaVersion": 1, "id": "bad", "source": { "kind": "release", "tag": "v1" },
              "assets": { "scripts": { "sha256": "{{new string('a', 64)}}" } }
            }
            """));
    }

    [Fact]
    public void Rejects_an_asset_downloaded_over_plain_http()
    {
        Assert.Throws<InvalidProfileException>(() => Load($$"""
            {
              "schemaVersion": 1, "id": "bad", "source": { "kind": "release", "tag": "v1" },
              "assets": { "scripts": { "url": "http://example.test/s.zip", "sha256": "{{new string('a', 64)}}" } }
            }
            """));
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*ProfileLoaderTests"`
Expected: the build fails with CS0103, `The name 'ProfileAssetKeys' does not exist`.

- [ ] **Step 3: Extend the profile model**

In `src/PS5Kitsune.Core/Profiles/AnyPS5Profile.cs`:

Inside `AnyPS5Profile`, after `public const int CurrentSchemaVersion = 1;`, add:

```csharp
    public const string RepositoryUrl = "https://github.com/boykopovar/AnyPS5";
```

and after the `Relinker` property:

```csharp
    /// <exception cref="InvalidProfileException">The profile has no asset named <paramref name="key"/>.</exception>
    public ProfileAsset Asset(string key) =>
        Assets.TryGetValue(key, out var asset) ? asset : throw new InvalidProfileException($"Profile {Id} has no asset \"{key}\".");

    /// <summary>The asset's own URL, else the asset of that name in the release named by the source tag.</summary>
    /// <exception cref="InvalidProfileException">The profile has no asset named <paramref name="key"/>.</exception>
    public Uri AssetUrl(string key)
    {
        var asset = Asset(key);
        return asset.Url ?? new Uri($"{RepositoryUrl}/releases/download/{Source.Tag}/{asset.Name}");
    }
```

Replace the `ProfileSource` and `ProfileAsset` declarations with:

```csharp
/// <param name="Kind">"release" (uses <paramref name="Tag"/>) or "commit" (uses <paramref name="Sha"/>).</param>
public sealed record ProfileSource(string Kind, string? Tag = null, string? Sha = null)
{
    /// <summary>The tag of a release, the SHA of a commit.</summary>
    public string Version => Tag ?? Sha ?? "unknown";
}

/// <param name="Name">File name of a release asset of the source tag. Unused when <paramref name="Url"/> is set.</param>
/// <param name="Url">Direct URL, for files that are not release assets (source archive, pinned script).</param>
/// <param name="Include">For an archive: path prefixes to extract, relative to its top folder. Null or empty extracts everything.</param>
public sealed record ProfileAsset(string? Name, string Sha256, Uri? Url = null, IReadOnlyList<string>? Include = null);

/// <summary>Keys of the assets a profile must declare.</summary>
public static class ProfileAssetKeys
{
    public const string Relinker = "relinker";

    public const string Libs = "libs";

    public const string Scripts = "scripts";

    public const string UnwrapScript = "unwrapScript";
}
```

- [ ] **Step 4: Validate the new fields**

In `src/PS5Kitsune.Core/Profiles/ProfileLoader.cs`, replace the two `foreach` loops of `Validate` with:

```csharp
        foreach (var (name, asset) in profile.Assets)
        {
            RequireSha256(profile.Id, $"asset {name}", asset.Sha256);
            if (asset.Url is not null)
            {
                RequireHttps(profile.Id, $"asset {name}", asset.Url);
            }
            else if (string.IsNullOrEmpty(asset.Name) || profile.Source.Tag is null)
            {
                throw new InvalidProfileException($"Profile {profile.Id}: asset {name} needs a url, or a name and a release source tag.");
            }
        }

        foreach (var (name, tool) in profile.Tools)
        {
            RequireSha256(profile.Id, $"tool {name}", tool.Sha256);
            RequireHttps(profile.Id, $"tool {name}", tool.Url);
        }
```

and add this method after `RequireSha256`:

```csharp
    private static void RequireHttps(string profileId, string what, Uri url)
    {
        if (url.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidProfileException($"Profile {profileId}: {what} must be downloaded over HTTPS.");
        }
    }
```

- [ ] **Step 5: Add the two assets to the embedded profile**

In `src/PS5Kitsune.Core/Profiles/default-profile.json`, replace the `"assets"` object with:

```json
  "assets": {
    "relinker": {
      "name": "relinker-v0.1.2.exe",
      "sha256": "88cc311d19b1a4e41f0f34883dfb61b118bdfacd317c3960cb62e4c707bd9401"
    },
    "libs": {
      "name": "prx-windows-v0.1.2.zip",
      "sha256": "b26e507e7cc01922312f4ca9c7b36ab333ed3a1d7048d676f20214cadf2d4895"
    },
    "scripts": {
      "url": "https://github.com/boykopovar/AnyPS5/archive/refs/tags/v0.1.2.zip",
      "sha256": "90c802352b5478beacfdf032b57649872ccf0b7dc18c9c868b35903378ad2545",
      "include": [ "tools/", "core/libs/prx/" ]
    },
    "unwrapScript": {
      "url": "https://raw.githubusercontent.com/boykopovar/AnyPS5/360e8bd685d32b0e9d37e6ee2fae44417c0f808c/tools/unwrap_self.py",
      "sha256": "2f9321fa41009411b9cd07730f68094967c80a6c0c31d801b31d55141c291ef7"
    }
  },
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*ProfileLoaderTests"`
Expected: 9 tests, all pass.

- [ ] **Step 7: Commit**

```bash
git add src/PS5Kitsune.Core/Profiles tests/PS5Kitsune.Core.Tests/Profiles
git commit -m "Add the scripts and unwrap script assets to the profile

Assets can now carry a direct URL and an include list; release assets
still resolve from the source tag.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Step pipeline

**Files:**
- Create: `src/PS5Kitsune.Core/Pipeline/IStep.cs`
- Create: `src/PS5Kitsune.Core/Pipeline/StepProgress.cs`
- Create: `src/PS5Kitsune.Core/Pipeline/StepEvent.cs`
- Create: `src/PS5Kitsune.Core/Pipeline/StepFailedException.cs`
- Create: `src/PS5Kitsune.Core/Pipeline/StepRunner.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Pipeline/StepRunnerTests.cs`

**Interfaces:**
- Produces: `IStep { string Name; bool IsAlreadyDone(); Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken); }`
- Produces: `StepProgress(string? Message = null, DownloadProgress? Download = null)` and `static IProgress<DownloadProgress>? StepProgress.ForDownload(IProgress<StepProgress>?)`
- Produces: `StepEventKind { Started, Skipped, Completed, Failed }`, `StepEvent(StepEventKind Kind, string StepName, int Number, int Count, TimeSpan? Duration = null, Exception? Error = null)`
- Produces: `StepFailedException(string stepName, Exception innerException)` with `StepName`
- Produces: `StepRunner(ILogger<StepRunner>)` with `Task RunAsync(IReadOnlyList<IStep> steps, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default)`. `IsAlreadyDone()` is checked just before each step, not up front.

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.Core.Tests/Pipeline/StepRunnerTests.cs`:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.Tests.Pipeline;

public sealed class StepRunnerTests
{
    private readonly StepRunner _runner = new(NullLogger<StepRunner>.Instance);
    private readonly List<StepEvent> _events = [];
    private readonly List<string> _executed = [];

    [Fact]
    public async Task Runs_steps_in_order_and_skips_those_already_done()
    {
        var b = new FakeStep("B", _executed) { Done = true };

        await _runner.RunAsync([new FakeStep("A", _executed), b, new FakeStep("C", _executed)], onEvent: _events.Add, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["A", "C"], _executed);
        Assert.Equal(
            [(StepEventKind.Started, "A"), (StepEventKind.Completed, "A"), (StepEventKind.Skipped, "B"), (StepEventKind.Started, "C"), (StepEventKind.Completed, "C")],
            _events.Select(e => (e.Kind, e.StepName)));
        Assert.All(_events, e => Assert.Equal(3, e.Count));
        Assert.Equal(2, _events.Single(e => e.Kind == StepEventKind.Skipped).Number);
    }

    [Fact]
    public async Task Checks_whether_a_step_is_done_just_before_running_it()
    {
        var b = new FakeStep("B", _executed);
        var a = new FakeStep("A", _executed) { OnExecute = () => b.Done = true };

        await _runner.RunAsync([a, b], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["A"], _executed);
    }

    [Fact]
    public async Task Stops_at_the_first_failure_and_names_the_step()
    {
        var steps = new IStep[]
        {
            new FakeStep("A", _executed),
            new FakeStep("B", _executed) { Failure = new InvalidOperationException("disk full") },
            new FakeStep("C", _executed),
        };

        var exception = await Assert.ThrowsAsync<StepFailedException>(() =>
            _runner.RunAsync(steps, onEvent: _events.Add, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("B", exception.StepName);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains("disk full", exception.Message, StringComparison.Ordinal);
        Assert.Equal(["A", "B"], _executed);
        Assert.Equal((StepEventKind.Failed, "B"), (_events[^1].Kind, _events[^1].StepName));
    }

    [Fact]
    public async Task Cancellation_is_not_wrapped()
    {
        using var cancellation = new CancellationTokenSource();
        var a = new FakeStep("A", _executed) { OnExecute = cancellation.Cancel, Failure = new OperationCanceledException(cancellation.Token) };

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _runner.RunAsync([a, new FakeStep("B", _executed)], cancellationToken: cancellation.Token));

        Assert.Equal(["A"], _executed);
    }

    [Fact]
    public void Download_progress_is_forwarded_synchronously()
    {
        var received = new List<StepProgress>();
        var forwarded = StepProgress.ForDownload(new SyncProgress(received.Add))!;

        forwarded.Report(new(10, 100));

        Assert.Equal(new StepProgress(Download: new(10, 100)), Assert.Single(received));
        Assert.Null(StepProgress.ForDownload(null));
    }

    private sealed class SyncProgress(Action<StepProgress> report) : IProgress<StepProgress>
    {
        public void Report(StepProgress value) => report(value);
    }

    private sealed class FakeStep(string name, List<string> executed) : IStep
    {
        public string Name => name;

        public bool Done { get; set; }

        public Exception? Failure { get; init; }

        public Action? OnExecute { get; init; }

        public bool IsAlreadyDone() => Done;

        public Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)
        {
            executed.Add(Name);
            OnExecute?.Invoke();
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*StepRunnerTests"`
Expected: the build fails with CS0246, `The type or namespace name 'StepRunner' could not be found`.

- [ ] **Step 3: Write the pipeline types**

`src/PS5Kitsune.Core/Pipeline/IStep.cs`:

```csharp
namespace PS5Kitsune.Core.Pipeline;

/// <summary>
/// A long operation with progress, cancellation and resume. See docs/architecture.md, "Resuming steps".
/// </summary>
public interface IStep
{
    /// <summary>Short imperative name shown in logs and in the UI, for example "Download the relinker".</summary>
    string Name { get; }

    /// <summary>
    /// True when what the step produces is already there (a file with the right hash, a copied folder).
    /// Checked just before the step would run, so an earlier step can change the answer.
    /// </summary>
    bool IsAlreadyDone();

    Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken);
}
```

`src/PS5Kitsune.Core/Pipeline/StepProgress.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Pipeline;

/// <param name="Message">A line of detail, for example the file being unwrapped.</param>
/// <param name="Download">Progress of the file the step is downloading.</param>
public sealed record StepProgress(string? Message = null, DownloadProgress? Download = null)
{
    /// <summary>Forwards download progress to <paramref name="progress"/> synchronously, so console lines keep their order.</summary>
    public static IProgress<DownloadProgress>? ForDownload(IProgress<StepProgress>? progress) =>
        progress is null ? null : new DownloadForwarder(progress);

    private sealed class DownloadForwarder(IProgress<StepProgress> progress) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => progress.Report(new StepProgress(Download: value));
    }
}
```

`src/PS5Kitsune.Core/Pipeline/StepEvent.cs`:

```csharp
namespace PS5Kitsune.Core.Pipeline;

public enum StepEventKind
{
    Started,
    Skipped,
    Completed,
    Failed,
}

/// <param name="Number">Position of the step, from 1.</param>
/// <param name="Count">Number of steps in the run.</param>
/// <param name="Duration">Set for Completed and Failed.</param>
/// <param name="Error">Set for Failed.</param>
public sealed record StepEvent(StepEventKind Kind, string StepName, int Number, int Count, TimeSpan? Duration = null, Exception? Error = null);
```

`src/PS5Kitsune.Core/Pipeline/StepFailedException.cs`:

```csharp
namespace PS5Kitsune.Core.Pipeline;

public sealed class StepFailedException(string stepName, Exception innerException)
    : Exception($"{stepName} failed: {innerException.Message}", innerException)
{
    public string StepName { get; } = stepName;
}
```

`src/PS5Kitsune.Core/Pipeline/StepRunner.cs`:

```csharp
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace PS5Kitsune.Core.Pipeline;

/// <summary>
/// Runs steps in order, skips those already done, and stops at the first failure.
/// Logs every step at Information level (the CLI console shows them); <c>onEvent</c> is for the UI.
/// </summary>
public sealed partial class StepRunner(ILogger<StepRunner> logger)
{
    /// <exception cref="StepFailedException">A step failed; the exception names it and wraps the cause.</exception>
    /// <exception cref="OperationCanceledException">The run was cancelled; it can be resumed.</exception>
    public async Task RunAsync(IReadOnlyList<IStep> steps, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(steps);
        for (var index = 0; index < steps.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = steps[index];
            var number = index + 1;
            if (step.IsAlreadyDone())
            {
                LogSkipped(number, steps.Count, step.Name);
                onEvent?.Invoke(new StepEvent(StepEventKind.Skipped, step.Name, number, steps.Count));
                continue;
            }

            LogStarted(number, steps.Count, step.Name);
            onEvent?.Invoke(new StepEvent(StepEventKind.Started, step.Name, number, steps.Count));
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await step.ExecuteAsync(progress, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
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
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "[{Number}/{Count}] {Step}")]
    private partial void LogStarted(int number, int count, string step);

    [LoggerMessage(Level = LogLevel.Information, Message = "[{Number}/{Count}] {Step}: already done")]
    private partial void LogSkipped(int number, int count, string step);

    [LoggerMessage(Level = LogLevel.Information, Message = "[{Number}/{Count}] {Step}: done in {Seconds:0.0} s")]
    private partial void LogCompleted(int number, int count, string step, double seconds);

    [LoggerMessage(Level = LogLevel.Error, Message = "[{Number}/{Count}] {Step}: failed after {Seconds:0.0} s")]
    private partial void LogFailed(Exception exception, int number, int count, string step, double seconds);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*StepRunnerTests"`
Expected: 5 tests, all pass.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Core/Pipeline tests/PS5Kitsune.Core.Tests/Pipeline
git commit -m "Add the step pipeline

IStep reports whether its output already exists; StepRunner skips done
steps, stops at the first failure and names the failing step.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: File helpers (hash, filtered extraction, tree copy, JSON records)

**Files:**
- Create: `src/PS5Kitsune.Core/IO/FileHash.cs`
- Create: `src/PS5Kitsune.Core/IO/ZipExtraction.cs`
- Create: `src/PS5Kitsune.Core/IO/FileTree.cs`
- Create: `src/PS5Kitsune.Core/IO/JsonFiles.cs`
- Modify: `src/PS5Kitsune.Core/Downloads/HttpDownloader.cs`
- Test: `tests/PS5Kitsune.Core.Tests/IO/FileHashTests.cs`
- Test: `tests/PS5Kitsune.Core.Tests/IO/ZipExtractionTests.cs`
- Test: `tests/PS5Kitsune.Core.Tests/IO/FileTreeTests.cs`

**Interfaces:**
- Produces: `FileHash.Sha256(string path) : string`, `FileHash.Sha256Async(string path, CancellationToken) : Task<string>`, `FileHash.Matches(string path, string sha256) : bool`
- Produces: `ZipExtraction.MarkerFileName` (`".ps5kitsune-extracted"`, internal const), `ZipExtraction.ExtractAsync(string archive, string target, string? archiveSha256, Func<string, string?> map, CancellationToken) : Task`, `ZipExtraction.IsExtracted(string target, string archiveSha256) : bool`, `ZipExtraction.UnderPrefix(string prefix)`, `ZipExtraction.StripTopFolder(IReadOnlyList<string> includes)`, `ZipExtraction.Everything`
- Produces (internal): `FileTree.RelativeFiles(string root) : IEnumerable<string>`, `FileTree.Size(string root) : long`, `FileTree.SameSize(string source, string copy) : bool`, `FileTree.Copy(string sourceRoot, string targetRoot, Func<string, bool> isCopied, CancellationToken)`
- Produces (internal): `JsonFiles.Options`, `JsonFiles.Read<T>(string path) : T?` (null when the file is missing; throws `JsonException` when invalid), `JsonFiles.WriteAsync<T>(string path, T value, CancellationToken) : Task` (atomic)

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.Core.Tests/IO/FileHashTests.cs`:

```csharp
using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Core.Tests.IO;

public sealed class FileHashTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public async Task Hashes_a_file_in_lowercase_hex()
    {
        var path = _temp.Combine("file.bin");
        TestFiles.Write(path, "abc");

        // SHA-256 of "abc".
        const string expected = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
        Assert.Equal(expected, FileHash.Sha256(path));
        Assert.Equal(expected, await FileHash.Sha256Async(path, TestContext.Current.CancellationToken));
        Assert.True(FileHash.Matches(path, expected));
        Assert.False(FileHash.Matches(path, new string('0', 64)));
        Assert.False(FileHash.Matches(_temp.Combine("missing.bin"), expected));
    }

    public void Dispose() => _temp.Dispose();
}
```

`tests/PS5Kitsune.Core.Tests/IO/ZipExtractionTests.cs`:

```csharp
using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Core.Tests.IO;

public sealed class ZipExtractionTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    private string Target => _temp.Combine("target");

    private string WriteZip(params (string Name, byte[] Content)[] entries)
    {
        var path = _temp.Combine("archive.zip");
        File.WriteAllBytes(path, TestFiles.ZipBytes(entries));
        return path;
    }

    [Fact]
    public async Task Extracts_only_the_entries_under_a_prefix()
    {
        var zip = WriteZip(("libs/a.prx", "a"u8.ToArray()), ("libs/sub/b.dll", "b"u8.ToArray()), ("README.md", "r"u8.ToArray()));

        await ZipExtraction.ExtractAsync(zip, Target, "abc", ZipExtraction.UnderPrefix("libs/"), TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(Target, "a.prx")));
        Assert.True(File.Exists(Path.Combine(Target, "sub", "b.dll")));
        Assert.False(File.Exists(Path.Combine(Target, "README.md")));
        Assert.True(ZipExtraction.IsExtracted(Target, "abc"));
        Assert.False(ZipExtraction.IsExtracted(Target, "def"));
        Assert.False(Directory.Exists(Target + ".staging"));
    }

    [Fact]
    public async Task Strips_the_top_folder_and_keeps_the_included_trees()
    {
        var zip = WriteZip(
            ("AnyPS5-0.1.2/tools/import_audit.py", "t"u8.ToArray()),
            ("AnyPS5-0.1.2/core/libs/prx/libc/stub.cpp", "c"u8.ToArray()),
            ("AnyPS5-0.1.2/core/other.txt", "o"u8.ToArray()),
            ("AnyPS5-0.1.2/README.md", "r"u8.ToArray()));

        await ZipExtraction.ExtractAsync(zip, Target, "abc", ZipExtraction.StripTopFolder(["tools/", "core/libs/prx/"]), TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(Target, "tools", "import_audit.py")));
        Assert.True(File.Exists(Path.Combine(Target, "core", "libs", "prx", "libc", "stub.cpp")));
        Assert.False(File.Exists(Path.Combine(Target, "core", "other.txt")));
        Assert.False(File.Exists(Path.Combine(Target, "README.md")));
    }

    [Fact]
    public async Task Replaces_a_previous_extraction()
    {
        TestFiles.Write(Path.Combine(Target, "old.txt"), "old");
        var zip = WriteZip(("new.txt", "n"u8.ToArray()));

        await ZipExtraction.ExtractAsync(zip, Target, "abc", ZipExtraction.Everything, TestContext.Current.CancellationToken);

        Assert.False(File.Exists(Path.Combine(Target, "old.txt")));
        Assert.True(File.Exists(Path.Combine(Target, "new.txt")));
    }

    [Fact]
    public async Task Without_a_hash_no_marker_is_written()
    {
        var zip = WriteZip(("eboot.bin", "e"u8.ToArray()));

        await ZipExtraction.ExtractAsync(zip, Target, archiveSha256: null, ZipExtraction.Everything, TestContext.Current.CancellationToken);

        Assert.Equal(["eboot.bin"], Directory.GetFiles(Target).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Rejects_an_entry_that_escapes_the_target()
    {
        var zip = WriteZip(("../evil.txt", "e"u8.ToArray()));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            ZipExtraction.ExtractAsync(zip, Target, "abc", ZipExtraction.Everything, TestContext.Current.CancellationToken));

        Assert.False(File.Exists(_temp.Combine("evil.txt")));
    }

    public void Dispose() => _temp.Dispose();
}
```

`tests/PS5Kitsune.Core.Tests/IO/FileTreeTests.cs`:

```csharp
using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Core.Tests.IO;

public sealed class FileTreeTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void Copies_the_files_not_yet_copied()
    {
        var source = _temp.Combine("source");
        var target = _temp.Combine("target");
        TestFiles.Write(Path.Combine(source, "a.txt"), "aaa");
        TestFiles.Write(Path.Combine(source, "sub", "b.txt"), "bb");
        TestFiles.Write(Path.Combine(target, "a.txt"), "old");

        FileTree.Copy(source, target, relative => relative == "a.txt", TestContext.Current.CancellationToken);

        Assert.Equal("old", File.ReadAllText(Path.Combine(target, "a.txt")));
        Assert.Equal("bb", File.ReadAllText(Path.Combine(target, "sub", "b.txt")));
        Assert.Equal(5, FileTree.Size(source));
        Assert.Equal(["a.txt", Path.Combine("sub", "b.txt")], FileTree.RelativeFiles(source).Order(StringComparer.Ordinal));
        Assert.True(FileTree.SameSize(Path.Combine(source, "a.txt"), Path.Combine(target, "a.txt")));
        Assert.False(FileTree.SameSize(Path.Combine(source, "a.txt"), Path.Combine(target, "missing.txt")));
        Assert.Empty(FileTree.RelativeFiles(_temp.Combine("missing")));
    }

    public void Dispose() => _temp.Dispose();
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*FileHashTests"`
Expected: the build fails with CS0234, `The type or namespace name 'IO' does not exist in the namespace 'PS5Kitsune.Core'`.

- [ ] **Step 3: Write the helpers**

`src/PS5Kitsune.Core/IO/FileHash.cs`:

```csharp
using System.Security.Cryptography;

namespace PS5Kitsune.Core.IO;

public static class FileHash
{
    private const int BufferSize = 81920;

    /// <summary>Lowercase hex SHA-256 of a file.</summary>
    public static string Sha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    /// <summary>Lowercase hex SHA-256 of a file.</summary>
    public static async Task<string> Sha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>True when the file exists and has this SHA-256.</summary>
    public static bool Matches(string path, string sha256) => File.Exists(path) && Sha256(path) == sha256;
}
```

`src/PS5Kitsune.Core/IO/ZipExtraction.cs`:

```csharp
using System.IO.Compression;

namespace PS5Kitsune.Core.IO;

/// <summary>
/// Extracts part of a zip through a sibling staging folder swapped in at the end, so an interrupted
/// extraction never leaves a half-filled target. A marker file can record the archive hash.
/// </summary>
public static class ZipExtraction
{
    internal const string MarkerFileName = ".ps5kitsune-extracted";

    /// <summary>Keeps every entry at its own path.</summary>
    public static Func<string, string?> Everything { get; } = static name => name;

    /// <summary>Keeps the entries under <paramref name="prefix"/> (for example "libs/"), relative to it.</summary>
    public static Func<string, string?> UnderPrefix(string prefix) =>
        name => name.StartsWith(prefix, StringComparison.Ordinal) && name.Length > prefix.Length ? name[prefix.Length..] : null;

    /// <summary>
    /// Drops the archive's top folder (GitHub source archives have one) and keeps the entries under one of
    /// <paramref name="includes"/>, tree layout kept. No includes keeps everything.
    /// </summary>
    public static Func<string, string?> StripTopFolder(IReadOnlyList<string> includes) => name =>
    {
        var slash = name.IndexOf('/', StringComparison.Ordinal);
        if (slash < 0)
        {
            return null;
        }

        var inner = name[(slash + 1)..];
        return includes.Count == 0 || includes.Any(include => inner.StartsWith(include, StringComparison.Ordinal)) ? inner : null;
    };

    /// <param name="archiveSha256">Recorded in a marker file inside the target; null writes no marker.</param>
    /// <param name="map">Entry name ('/' separators) to a path relative to the target, or null to skip the entry.</param>
    /// <exception cref="InvalidDataException">An entry would land outside the target.</exception>
    public static async Task ExtractAsync(string archive, string target, string? archiveSha256, Func<string, string?> map, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(map);
        var staging = Path.GetFullPath(target) + ".staging";
        if (Directory.Exists(staging))
        {
            Directory.Delete(staging, recursive: true);
        }

        Directory.CreateDirectory(staging);
        var stagingRoot = staging + Path.DirectorySeparatorChar;
        using (var zip = ZipFile.OpenRead(archive))
        {
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.EndsWith('/') || map(entry.FullName) is not { } relative)
                {
                    continue;
                }

                var destination = Path.GetFullPath(Path.Combine(staging, relative));
                if (!destination.StartsWith(stagingRoot, StringComparison.Ordinal))
                {
                    throw new InvalidDataException($"{archive}: entry {entry.FullName} would be extracted outside {target}.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await using var source = entry.Open();
                await using var output = File.Create(destination);
                await source.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }
        }

        if (archiveSha256 is not null)
        {
            await File.WriteAllTextAsync(Path.Combine(staging, MarkerFileName), archiveSha256, cancellationToken).ConfigureAwait(false);
        }

        if (Directory.Exists(target))
        {
            Directory.Delete(target, recursive: true);
        }

        Directory.Move(staging, target);
    }

    /// <summary>True when <paramref name="target"/> was extracted from the archive with this hash.</summary>
    public static bool IsExtracted(string target, string archiveSha256)
    {
        var marker = Path.Combine(target, MarkerFileName);
        return File.Exists(marker) && File.ReadAllText(marker).Trim() == archiveSha256;
    }
}
```

`src/PS5Kitsune.Core/IO/FileTree.cs`:

```csharp
namespace PS5Kitsune.Core.IO;

internal static class FileTree
{
    /// <summary>Every file under <paramref name="root"/>, relative to it; nothing when the folder does not exist.</summary>
    public static IEnumerable<string> RelativeFiles(string root) =>
        Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(root, path))
            : [];

    /// <summary>Total size in bytes of the files under <paramref name="root"/>.</summary>
    public static long Size(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length);

    /// <summary>True when <paramref name="copy"/> exists with the size of <paramref name="source"/>.</summary>
    public static bool SameSize(string source, string copy) =>
        File.Exists(copy) && new FileInfo(source).Length == new FileInfo(copy).Length;

    /// <summary>Copies every file of <paramref name="sourceRoot"/> for which <paramref name="isCopied"/> is false.</summary>
    public static void Copy(string sourceRoot, string targetRoot, Func<string, bool> isCopied, CancellationToken cancellationToken)
    {
        foreach (var relative in RelativeFiles(sourceRoot).ToList())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (isCopied(relative))
            {
                continue;
            }

            var target = Path.Combine(targetRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(sourceRoot, relative), target, overwrite: true);
        }
    }
}
```

`src/PS5Kitsune.Core/IO/JsonFiles.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PS5Kitsune.Core.IO;

/// <summary>The app's JSON records (install.json, game.json): camelCase, indented, enums as camelCase strings.</summary>
internal static class JsonFiles
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <returns>Null when the file does not exist.</returns>
    /// <exception cref="JsonException">The file is not valid JSON for <typeparamref name="T"/>.</exception>
    public static T? Read<T>(string path)
        where T : class =>
        File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) : null;

    /// <summary>Writes through a temporary file, so a crash never leaves half a record.</summary>
    public static async Task WriteAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(value, Options), cancellationToken).ConfigureAwait(false);
        File.Move(temporary, path, overwrite: true);
    }
}
```

- [ ] **Step 4: Make `HttpDownloader` use `FileHash`**

In `src/PS5Kitsune.Core/Downloads/HttpDownloader.cs`:
- Replace `using System.Security.Cryptography;` with nothing (delete the line), and add `using PS5Kitsune.Core.IO;` after `using PS5Kitsune.Core.Abstractions;`.
- Replace both calls `ComputeSha256Async(` with `FileHash.Sha256Async(`.
- Delete the whole `internal static async Task<string> ComputeSha256Async(...)` method.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: every test passes, including the 7 new ones and `HttpDownloaderTests`.

- [ ] **Step 6: Commit**

```bash
git add src/PS5Kitsune.Core/IO src/PS5Kitsune.Core/Downloads/HttpDownloader.cs tests/PS5Kitsune.Core.Tests/IO
git commit -m "Add file hashing, filtered zip extraction, tree copy and JSON records

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: AnyPS5 install (Core)

**Files:**
- Modify: `src/PS5Kitsune.Core/DataLayout.cs`
- Create: `src/PS5Kitsune.Core/AnyPS5/AnyPS5Paths.cs`
- Create: `src/PS5Kitsune.Core/AnyPS5/AnyPS5InstallRecord.cs`
- Create: `src/PS5Kitsune.Core/AnyPS5/AnyPS5NotInstalledException.cs`
- Create: `src/PS5Kitsune.Core/AnyPS5/AnyPS5Installer.cs`
- Create: `src/PS5Kitsune.Core/AnyPS5/Steps/InstallPythonStep.cs`
- Create: `src/PS5Kitsune.Core/AnyPS5/Steps/DownloadFileStep.cs`
- Create: `src/PS5Kitsune.Core/AnyPS5/Steps/DownloadArchiveStep.cs`
- Create: `src/PS5Kitsune.Core/AnyPS5/Steps/WriteInstallRecordStep.cs`
- Modify: `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`
- Create: `tests/PS5Kitsune.Core.Tests/Fakes/FixedTimeProvider.cs`
- Create: `tests/PS5Kitsune.Core.Tests/AnyPS5/AnyPS5TestSetup.cs`
- Test: `tests/PS5Kitsune.Core.Tests/AnyPS5/AnyPS5InstallerTests.cs`

**Interfaces:**
- Consumes: `IStep`, `StepProgress`, `StepRunner` (Task 3); `FileHash`, `ZipExtraction`, `JsonFiles` (Task 4); `AnyPS5Profile.Asset/AssetUrl`, `ProfileAssetKeys`, `ProfileSource.Version` (Task 2); `FakeDownloader`, `FakePlatform`, `FakeProcessRunner`, `TestFiles` (Task 1).
- Produces: `DataLayout.AnyPS5Current`, `DataLayout.Games`, `DataLayout.Staging`, `DataLayout.GameDirectory(string titleId)`.
- Produces: `AnyPS5Paths(DataLayout, IPlatform)` with `RootDirectory`, `Relinker`, `Libs`, `Scripts`, `ImportAudit`, `UnwrapSelf`, `RecordFile`.
- Produces: `AnyPS5InstallRecord(int SchemaVersion, string Profile, string Mode, string Version, Uri UnwrapScript, DateTimeOffset InstalledAt)`.
- Produces: `AnyPS5NotInstalledException()` (message: "AnyPS5 is not installed. Run 'ps5kitsune install' first.").
- Produces: `AnyPS5Installer(AnyPS5Profile, AnyPS5Paths, DataLayout, PythonEmbeddableTool, IDownloader, StepRunner, TimeProvider)` with `IReadOnlyList<IStep> CreateSteps()`, `bool IsInstalled()`, and `Task InstallAsync(IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default)`.
- Produces (tests): `FixedTimeProvider(DateTimeOffset)`, and `AnyPS5TestSetup(string root)` with `Layout`, `Platform`, `Downloader`, `Runner`, `Time`, `Profile`, `PythonTool`, `Paths`, `StepRunner`, `Installer`, `InstallAsync()`, the static contents (`RelinkerContent`, `LibsArchive`, `SourceArchive`, `UnwrapScriptContent`, `PythonArchive`) and `UnwrapScriptUrl`. The fake libs contain `libs/libSceAudioOut.prx` and `libs/anyps5.dll`.

- [ ] **Step 1: Write the test setup and the failing tests**

`tests/PS5Kitsune.Core.Tests/Fakes/FixedTimeProvider.cs`:

```csharp
namespace PS5Kitsune.Core.Tests.Fakes;

/// <summary>A clock stopped at <paramref name="now"/>, in UTC.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
}
```

`tests/PS5Kitsune.Core.Tests/AnyPS5/AnyPS5TestSetup.cs`:

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.Tests.AnyPS5;

/// <summary>A profile whose assets are served by a <see cref="FakeDownloader"/>, and an installer wired to it.</summary>
internal sealed class AnyPS5TestSetup
{
    public static readonly byte[] RelinkerContent = "fake relinker"u8.ToArray();

    public static readonly byte[] LibsArchive = TestFiles.ZipBytes(
        ("libs/libSceAudioOut.prx", "prx"u8.ToArray()),
        ("libs/anyps5.dll", "dll"u8.ToArray()),
        ("README.md", "not extracted"u8.ToArray()));

    public static readonly byte[] SourceArchive = TestFiles.ZipBytes(
        ("AnyPS5-0.1.2/tools/import_audit.py", "audit"u8.ToArray()),
        ("AnyPS5-0.1.2/tools/nid_names.py", "names"u8.ToArray()),
        ("AnyPS5-0.1.2/core/libs/prx/libc/stub.cpp", "cpp"u8.ToArray()),
        ("AnyPS5-0.1.2/docs/BUILD.md", "not extracted"u8.ToArray()));

    public static readonly byte[] UnwrapScriptContent = "unwrap"u8.ToArray();

    public static readonly byte[] PythonArchive = TestFiles.ZipBytes(("python.exe", "fake"u8.ToArray()), ("python314._pth", "python314.zip"u8.ToArray()));

    public static readonly Uri SourceUrl = new("https://example.test/AnyPS5-v0.1.2.zip");

    public static readonly Uri UnwrapScriptUrl = new("https://example.test/360e8bd/tools/unwrap_self.py");

    private static readonly Uri PythonUrl = new("https://example.test/python-embed.zip");

    public AnyPS5TestSetup(string root)
    {
        Layout = new DataLayout(root);
        Profile = new AnyPS5Profile
        {
            SchemaVersion = AnyPS5Profile.CurrentSchemaVersion,
            Id = "test-profile",
            Source = new ProfileSource("release", Tag: "v0.1.2"),
            Assets = new Dictionary<string, ProfileAsset>
            {
                [ProfileAssetKeys.Relinker] = new("relinker-v0.1.2.exe", TestFiles.Sha256(RelinkerContent)),
                [ProfileAssetKeys.Libs] = new("prx-windows-v0.1.2.zip", TestFiles.Sha256(LibsArchive)),
                [ProfileAssetKeys.Scripts] = new(null, TestFiles.Sha256(SourceArchive), SourceUrl, ["tools/", "core/libs/prx/"]),
                [ProfileAssetKeys.UnwrapScript] = new(null, TestFiles.Sha256(UnwrapScriptContent), UnwrapScriptUrl),
            },
            Tools = new Dictionary<string, ToolDefinition>
            {
                [PythonEmbeddableTool.ToolId] = new("3.14.8", PythonUrl, TestFiles.Sha256(PythonArchive)),
            },
            Relinker = new RelinkerOptions { WindowsArgs = ["--windows", "--registry"], IntelArg = "--to-intel" },
        };

        Downloader.Serve(Profile.AssetUrl(ProfileAssetKeys.Relinker), RelinkerContent);
        Downloader.Serve(Profile.AssetUrl(ProfileAssetKeys.Libs), LibsArchive);
        Downloader.Serve(SourceUrl, SourceArchive);
        Downloader.Serve(UnwrapScriptUrl, UnwrapScriptContent);
        Downloader.Serve(PythonUrl, PythonArchive);

        PythonTool = new PythonEmbeddableTool(Profile.Tools[PythonEmbeddableTool.ToolId], Layout, Platform, Downloader, Runner);
        Paths = new AnyPS5Paths(Layout, Platform);
        Installer = new AnyPS5Installer(Profile, Paths, Layout, PythonTool, Downloader, StepRunner, Time);
    }

    public DataLayout Layout { get; }

    public FakePlatform Platform { get; } = new();

    public FakeDownloader Downloader { get; } = new();

    public FakeProcessRunner Runner { get; } = new();

    public FixedTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));

    public AnyPS5Profile Profile { get; }

    public PythonEmbeddableTool PythonTool { get; }

    public AnyPS5Paths Paths { get; }

    public StepRunner StepRunner { get; } = new(NullLogger<StepRunner>.Instance);

    public AnyPS5Installer Installer { get; }

    public Task InstallAsync() => Installer.InstallAsync(cancellationToken: TestContext.Current.CancellationToken);
}
```

`tests/PS5Kitsune.Core.Tests/AnyPS5/AnyPS5InstallerTests.cs`:

```csharp
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Core.Tests.AnyPS5;

public sealed class AnyPS5InstallerTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly AnyPS5TestSetup _setup;

    public AnyPS5InstallerTests()
    {
        _setup = new AnyPS5TestSetup(_temp.Path);
    }

    private string Current => _temp.Combine("anyps5", "current");

    [Fact]
    public async Task Install_lays_out_anyps5_current()
    {
        Assert.False(_setup.Installer.IsInstalled());

        await _setup.InstallAsync();

        Assert.Equal(AnyPS5TestSetup.RelinkerContent, File.ReadAllBytes(Path.Combine(Current, "relinker.exe")));
        Assert.True(File.Exists(Path.Combine(Current, "libs", "libSceAudioOut.prx")));
        Assert.True(File.Exists(Path.Combine(Current, "libs", "anyps5.dll")));
        Assert.False(File.Exists(Path.Combine(Current, "libs", "README.md")));
        Assert.True(File.Exists(Path.Combine(Current, "scripts", "tools", "import_audit.py")));
        Assert.True(File.Exists(Path.Combine(Current, "scripts", "tools", "nid_names.py")));
        Assert.True(File.Exists(Path.Combine(Current, "scripts", "core", "libs", "prx", "libc", "stub.cpp")));
        Assert.False(Directory.Exists(Path.Combine(Current, "scripts", "docs")));
        Assert.Equal(AnyPS5TestSetup.UnwrapScriptContent, File.ReadAllBytes(Path.Combine(Current, "scripts", "tools", "unwrap_self.py")));
        Assert.True(File.Exists(_temp.Combine("tools", "python", "python.exe")));
        Assert.True(_setup.Installer.IsInstalled());
    }

    [Fact]
    public async Task Install_writes_the_install_record()
    {
        await _setup.InstallAsync();

        var record = JsonFiles.Read<AnyPS5InstallRecord>(Path.Combine(Current, "install.json"));

        Assert.Equal(
            new AnyPS5InstallRecord(1, "test-profile", "release", "v0.1.2", AnyPS5TestSetup.UnwrapScriptUrl, _setup.Time.GetLocalNow()),
            record);
    }

    [Fact]
    public async Task Release_assets_are_downloaded_from_the_source_tag()
    {
        await _setup.InstallAsync();

        Assert.Contains(_setup.Downloader.Requests, request =>
            request.Source == new Uri("https://github.com/boykopovar/AnyPS5/releases/download/v0.1.2/relinker-v0.1.2.exe"));
        Assert.Contains(_setup.Downloader.Requests, request =>
            request.Source == new Uri("https://github.com/boykopovar/AnyPS5/releases/download/v0.1.2/prx-windows-v0.1.2.zip"));
    }

    [Fact]
    public async Task A_second_install_downloads_nothing()
    {
        await _setup.InstallAsync();
        var requests = _setup.Downloader.Requests.Count;

        await _setup.InstallAsync();

        Assert.Equal(requests, _setup.Downloader.Requests.Count);
    }

    [Fact]
    public async Task A_modified_relinker_is_downloaded_again()
    {
        await _setup.InstallAsync();
        File.WriteAllText(_setup.Paths.Relinker, "tampered");
        Assert.False(_setup.Installer.IsInstalled());
        var before = _setup.Downloader.Requests.Count;

        await _setup.InstallAsync();

        var request = Assert.Single(_setup.Downloader.Requests.Skip(before));
        Assert.EndsWith("relinker-v0.1.2.exe", request.Source.AbsolutePath, StringComparison.Ordinal);
        Assert.True(_setup.Installer.IsInstalled());
    }

    public void Dispose() => _temp.Dispose();
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*AnyPS5InstallerTests"`
Expected: the build fails with CS0234, `The type or namespace name 'AnyPS5' does not exist in the namespace 'PS5Kitsune.Core'`.

- [ ] **Step 3: Extend `DataLayout`**

In `src/PS5Kitsune.Core/DataLayout.cs`, add after the `Profiles` property:

```csharp
    /// <summary>The AnyPS5 install used to convert games: relinker, libs, scripts.</summary>
    public string AnyPS5Current => Path.Combine(Root, "anyps5", "current");

    public string Games => Path.Combine(Root, "games");

    /// <summary>Zips of games extracted before they are copied.</summary>
    public string Staging => Path.Combine(Downloads, "staging");

    public string GameDirectory(string titleId) => Path.Combine(Games, titleId);
```

- [ ] **Step 4: Write the AnyPS5 types**

`src/PS5Kitsune.Core/AnyPS5/AnyPS5Paths.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.AnyPS5;

/// <summary>Files of the AnyPS5 install. See docs/architecture.md, "Root layout".</summary>
public sealed class AnyPS5Paths(DataLayout layout, IPlatform platform)
{
    public string RootDirectory => layout.AnyPS5Current;

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

`src/PS5Kitsune.Core/AnyPS5/AnyPS5InstallRecord.cs`:

```csharp
namespace PS5Kitsune.Core.AnyPS5;

/// <summary>Contents of anyps5/current/install.json.</summary>
/// <param name="Mode">"release" until build mode exists (milestone 5).</param>
/// <param name="Version">Tag of the release.</param>
/// <param name="UnwrapScript">URL of unwrap_self.py; it holds the pinned commit.</param>
public sealed record AnyPS5InstallRecord(int SchemaVersion, string Profile, string Mode, string Version, Uri UnwrapScript, DateTimeOffset InstalledAt)
{
    public const int CurrentSchemaVersion = 1;
}
```

`src/PS5Kitsune.Core/AnyPS5/AnyPS5NotInstalledException.cs`:

```csharp
namespace PS5Kitsune.Core.AnyPS5;

public sealed class AnyPS5NotInstalledException()
    : Exception("AnyPS5 is not installed. Run 'ps5kitsune install' first.");
```

`src/PS5Kitsune.Core/AnyPS5/Steps/InstallPythonStep.cs`:

```csharp
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.AnyPS5.Steps;

public sealed class InstallPythonStep(PythonEmbeddableTool python) : IStep
{
    public string Name => "Install Python";

    public bool IsAlreadyDone() => python.IsInstalled();

    public Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken) =>
        python.InstallAsync(StepProgress.ForDownload(progress), cancellationToken);
}
```

`src/PS5Kitsune.Core/AnyPS5/Steps/DownloadFileStep.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.AnyPS5.Steps;

/// <summary>Downloads one file to its final place. Done when it is there with the expected hash.</summary>
public sealed class DownloadFileStep(string name, Uri url, string destination, string sha256, IDownloader downloader) : IStep
{
    public string Name => name;

    public bool IsAlreadyDone() => FileHash.Matches(destination, sha256);

    public Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken) =>
        downloader.DownloadAsync(new DownloadRequest(url, destination, sha256), StepProgress.ForDownload(progress), cancellationToken);
}
```

`src/PS5Kitsune.Core/AnyPS5/Steps/DownloadArchiveStep.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.AnyPS5.Steps;

/// <summary>
/// Downloads a zip into downloads/ and extracts the entries <paramref name="map"/> keeps into <paramref name="target"/>.
/// Done when the target's marker holds the archive hash.
/// </summary>
public sealed class DownloadArchiveStep(
    string name, Uri url, string archive, string sha256, string target, Func<string, string?> map, IDownloader downloader) : IStep
{
    public string Name => name;

    public bool IsAlreadyDone() => ZipExtraction.IsExtracted(target, sha256);

    public async Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        await downloader.DownloadAsync(new DownloadRequest(url, archive, sha256), StepProgress.ForDownload(progress), cancellationToken).ConfigureAwait(false);
        await ZipExtraction.ExtractAsync(archive, target, sha256, map, cancellationToken).ConfigureAwait(false);
    }
}
```

`src/PS5Kitsune.Core/AnyPS5/Steps/WriteInstallRecordStep.cs`:

```csharp
using System.Text.Json;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.AnyPS5.Steps;

/// <summary>Writes install.json last. Done when it describes this profile's install.</summary>
public sealed class WriteInstallRecordStep(AnyPS5Profile profile, AnyPS5Paths paths, TimeProvider time) : IStep
{
    public string Name => "Record the install";

    public bool IsAlreadyDone()
    {
        try
        {
            return JsonFiles.Read<AnyPS5InstallRecord>(paths.RecordFile) is { } record
                && record with { InstalledAt = default } == Expected(default);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken) =>
        JsonFiles.WriteAsync(paths.RecordFile, Expected(time.GetLocalNow()), cancellationToken);

    private AnyPS5InstallRecord Expected(DateTimeOffset installedAt) => new(
        AnyPS5InstallRecord.CurrentSchemaVersion,
        profile.Id,
        profile.Source.Kind,
        profile.Source.Version,
        profile.AssetUrl(ProfileAssetKeys.UnwrapScript),
        installedAt);
}
```

`src/PS5Kitsune.Core/AnyPS5/AnyPS5Installer.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5.Steps;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.AnyPS5;

/// <summary>
/// Installs the release of the active profile into anyps5/current. A single mode exists until build mode
/// (milestone 5), so there is no IAnyPS5Source yet.
/// </summary>
public sealed class AnyPS5Installer(
    AnyPS5Profile profile,
    AnyPS5Paths paths,
    DataLayout layout,
    PythonEmbeddableTool python,
    IDownloader downloader,
    StepRunner stepRunner,
    TimeProvider time)
{
    public IReadOnlyList<IStep> CreateSteps()
    {
        var relinker = profile.Asset(ProfileAssetKeys.Relinker);
        var libs = profile.Asset(ProfileAssetKeys.Libs);
        var scripts = profile.Asset(ProfileAssetKeys.Scripts);
        var unwrapScript = profile.Asset(ProfileAssetKeys.UnwrapScript);
        return
        [
            new InstallPythonStep(python),
            new DownloadFileStep("Download the relinker", profile.AssetUrl(ProfileAssetKeys.Relinker), paths.Relinker, relinker.Sha256, downloader),
            new DownloadArchiveStep(
                "Download the libraries", profile.AssetUrl(ProfileAssetKeys.Libs), ArchivePath(ProfileAssetKeys.Libs, libs),
                libs.Sha256, paths.Libs, ZipExtraction.UnderPrefix("libs/"), downloader),
            new DownloadArchiveStep(
                "Download the scripts", profile.AssetUrl(ProfileAssetKeys.Scripts), ArchivePath(ProfileAssetKeys.Scripts, scripts),
                scripts.Sha256, paths.Scripts, ZipExtraction.StripTopFolder(scripts.Include ?? []), downloader),
            new DownloadFileStep("Download unwrap_self.py", profile.AssetUrl(ProfileAssetKeys.UnwrapScript), paths.UnwrapSelf, unwrapScript.Sha256, downloader),
            new WriteInstallRecordStep(profile, paths, time),
        ];
    }

    /// <summary>True when every install step is done: files present with the profile's hashes and install.json written.</summary>
    public bool IsInstalled() => CreateSteps().All(step => step.IsAlreadyDone());

    public Task InstallAsync(IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default) =>
        stepRunner.RunAsync(CreateSteps(), progress, onEvent, cancellationToken);

    private string ArchivePath(string key, ProfileAsset asset) =>
        Path.Combine(layout.Downloads, $"anyps5-{key}-{asset.Sha256[..12]}.zip");
}
```

- [ ] **Step 5: Register the services**

In `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`, add `using PS5Kitsune.Core.AnyPS5;` and `using PS5Kitsune.Core.Pipeline;` to the usings. Then, right after `services.AddSingleton<IProcessRunner, CliWrapProcessRunner>();`, add:

```csharp
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<StepRunner>();
        services.AddSingleton<AnyPS5Paths>();
        services.AddSingleton<AnyPS5Installer>();
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*AnyPS5InstallerTests"`
Expected: 5 tests, all pass.

- [ ] **Step 7: Commit**

```bash
git add src/PS5Kitsune.Core tests/PS5Kitsune.Core.Tests
git commit -m "Install the AnyPS5 release as resumable steps

Python, relinker, libs, scripts (tools/ and core/libs/prx/ of the source
archive) and the pinned unwrap_self.py go to anyps5/current, then
install.json records the profile.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: CLI structure and `ps5kitsune install`

**Files:**
- Create: `src/PS5Kitsune.Cli/CommandHost.cs`
- Create: `src/PS5Kitsune.Cli/ToolsCommand.cs`
- Create: `src/PS5Kitsune.Cli/InstallCommand.cs`
- Create: `src/PS5Kitsune.Cli/ConsoleStepProgress.cs`
- Modify: `src/PS5Kitsune.Cli/Program.cs` (whole file)
- Modify: `src/PS5Kitsune.Cli/CliLog.cs`

**Interfaces:**
- Consumes: `AnyPS5Installer.InstallAsync`, `AnyPS5Paths.RootDirectory`, `ProfileSource.Version`, `StepProgress`.
- Produces: `CommandHost.RunAsync(DirectoryInfo? rootOverride, string runName, Func<IServiceProvider, ILogger, Task<int>> action) : Task<int>`. It maps `OperationCanceledException` to 130 and any other exception to 1 (message plus log path). Each command is a class `XCommand.Create(Option<DirectoryInfo?> rootOption) : Command`. `ConsoleStepProgress : IProgress<StepProgress>`.

There is no unit test project for the CLI. Here the "test" is building and running the command.

- [ ] **Step 1: Move the host code out of `Program.cs`**

`src/PS5Kitsune.Cli/CommandHost.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PS5Kitsune.Core;
using PS5Kitsune.Core.Logging;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Platform.Windows;

namespace PS5Kitsune.Cli;

internal static class CommandHost
{
    /// <summary>
    /// Builds the services for one command, logs to &lt;root&gt;/logs/&lt;runName&gt;-&lt;date&gt;.log, and maps
    /// cancellation to exit code 130 and any other exception to 1.
    /// </summary>
    public static async Task<int> RunAsync(DirectoryInfo? rootOverride, string runName, Func<IServiceProvider, ILogger, Task<int>> action)
    {
        var platform = PlatformServices.Detect();
        var layout = new DataLayout(rootOverride?.FullName ?? platform.DefaultDataRoot);
        var services = new ServiceCollection();
        var logFile = services.AddPS5KitsuneLogging(layout.Logs, runName, console: true);
        services.AddPlatform(platform).AddPS5KitsuneCore(layout, ProfileLoader.LoadEmbedded());

        await using var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("ps5kitsune");
        try
        {
            return await action(provider, logger);
        }
        catch (OperationCanceledException)
        {
            logger.Cancelled();
            return 130;
        }
        catch (Exception exception)
        {
            logger.Failed(exception, exception.Message);
            logger.FullLog(logFile);
            return 1;
        }
    }
}
```

`src/PS5Kitsune.Cli/ToolsCommand.cs` (the `tools` commands moved verbatim from `Program.cs`):

```csharp
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Cli;

internal static class ToolsCommand
{
    public static Command Create(Option<DirectoryInfo?> rootOption)
    {
        var toolArgument = new Argument<string>("tool") { Description = "Tool to install, as shown by 'tools list'." };

        var listCommand = new Command("list", "List the portable tools of the active profile and whether they are installed.");
        listCommand.SetAction(parseResult => CommandHost.RunAsync(parseResult.GetValue(rootOption), "tools-list", (services, _) =>
        {
            foreach (var tool in services.GetServices<IPortableTool>())
            {
                Console.WriteLine($"{tool.Id,-10} {(tool.IsInstalled() ? "installed" : "missing")}");
            }

            return Task.FromResult(0);
        }));

        var installCommand = new Command("install", "Download, verify and extract a portable tool, then check that it runs.") { toolArgument };
        installCommand.SetAction((parseResult, cancellationToken) => CommandHost.RunAsync(parseResult.GetValue(rootOption), "tools-install", async (services, logger) =>
        {
            var id = parseResult.GetRequiredValue(toolArgument);
            var tool = services.GetServices<IPortableTool>().FirstOrDefault(candidate => candidate.Id == id);
            if (tool is null)
            {
                logger.UnknownTool(id);
                return 1;
            }

            await tool.InstallAsync(new ConsoleDownloadProgress(), cancellationToken);
            var version = await tool.GetVersionAsync(cancellationToken);
            logger.ToolReady(tool.Id, version);
            return 0;
        }));

        return new Command("tools", "Manage the portable tools (Python, Git, WinLibs…).") { listCommand, installCommand };
    }
}
```

`src/PS5Kitsune.Cli/ConsoleStepProgress.cs`:

```csharp
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Cli;

/// <summary>Prints step details and download progress, synchronously so lines stay in order.</summary>
internal sealed class ConsoleStepProgress : IProgress<StepProgress>
{
    private readonly ConsoleDownloadProgress _download = new();

    public void Report(StepProgress value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Message is not null)
        {
            Console.WriteLine($"  {value.Message}");
        }

        if (value.Download is { } download)
        {
            _download.Report(download);
        }
    }
}
```

`src/PS5Kitsune.Cli/InstallCommand.cs`:

```csharp
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Cli;

internal static class InstallCommand
{
    public static Command Create(Option<DirectoryInfo?> rootOption)
    {
        var command = new Command("install", "Install the AnyPS5 release of the active profile: Python, relinker, libraries and scripts.");
        command.SetAction((parseResult, cancellationToken) => CommandHost.RunAsync(parseResult.GetValue(rootOption), "install", async (services, logger) =>
        {
            await services.GetRequiredService<AnyPS5Installer>().InstallAsync(new ConsoleStepProgress(), cancellationToken: cancellationToken);
            logger.AnyPS5Ready(services.GetRequiredService<AnyPS5Profile>().Source.Version, services.GetRequiredService<AnyPS5Paths>().RootDirectory);
            return 0;
        }));
        return command;
    }
}
```

Replace the whole content of `src/PS5Kitsune.Cli/Program.cs`:

```csharp
using System.CommandLine;
using PS5Kitsune.Cli;

var rootOption = new Option<DirectoryInfo?>("--root")
{
    Description = "Data root (tools, AnyPS5, games). Defaults to C:\\PS5Kitsune on Windows.",
    Recursive = true,
};

var rootCommand = new RootCommand("PS5Kitsune: installer and launcher for AnyPS5.")
{
    InstallCommand.Create(rootOption),
    ToolsCommand.Create(rootOption),
};
rootCommand.Options.Add(rootOption);

return await rootCommand.Parse(args).InvokeAsync();
```

In `src/PS5Kitsune.Cli/CliLog.cs`, add before the closing brace:

```csharp

    [LoggerMessage(Level = LogLevel.Information, Message = "AnyPS5 {Version} is installed in {Directory}.")]
    public static partial void AnyPS5Ready(this ILogger logger, string version, string directory);
```

- [ ] **Step 2: Build**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: the build succeeds with no warning.

- [ ] **Step 3: Check that `tools` still works**

Run: `dotnet run --project src/PS5Kitsune.Cli -c Release -- tools list`
Expected: one line, `python     installed` or `python     missing`.

- [ ] **Step 4: Install AnyPS5 for real (downloads about 100 MB)**

Run: `dotnet run --project src/PS5Kitsune.Cli -c Release -- install`
Expected: the six steps `[1/6] Install Python` … `[6/6] Record the install`, each ending `done in …` or `already done`, then `AnyPS5 v0.1.2 is installed in C:\PS5Kitsune\anyps5\current.` with exit code 0. Then check the layout:

Run: `ls /c/PS5Kitsune/anyps5/current /c/PS5Kitsune/anyps5/current/scripts/tools | head -40 && cat /c/PS5Kitsune/anyps5/current/install.json`
Expected: `relinker.exe`, `libs/`, `scripts/`, `install.json`; `scripts/tools/` holds `import_audit.py`, `nid_names.py`, `progress.py`, `unwrap_self.py`; `install.json` has `"version": "v0.1.2"`.

A checksum mismatch here means GitHub regenerated the source archive (see the spec). If that happens, stop and report the expected and actual hashes to the user rather than editing the profile.

- [ ] **Step 5: Check that a second install does nothing**

Run: `dotnet run --project src/PS5Kitsune.Cli -c Release -- install`
Expected: all six steps print `already done`; exit code 0.

- [ ] **Step 6: Check that the real `import_audit.py` starts through the bootstrap**

Run: `/c/PS5Kitsune/tools/python/python.exe -c "import os,runpy,sys;sys.argv=sys.argv[1:];sys.path.insert(0,os.path.dirname(os.path.abspath(sys.argv[0])));runpy.run_path(sys.argv[0],run_name='__main__')" /c/PS5Kitsune/anyps5/current/scripts/tools/import_audit.py --help`
Expected: the argparse usage text (`usage: import_audit.py [-h] --libs LIBS …`) with no `ModuleNotFoundError`.

- [ ] **Step 7: Commit**

```bash
git add src/PS5Kitsune.Cli
git commit -m "Add the install command

Split the CLI into one file per command around a shared CommandHost.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Resolve a game folder or zip

**Files:**
- Create: `src/PS5Kitsune.Core/Games/GameSource.cs`
- Create: `src/PS5Kitsune.Core/Games/GameSourceResolver.cs`
- Create: `tests/PS5Kitsune.Core.Tests/Games/TestGames.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Games/GameSourceResolverTests.cs`

**Interfaces:**
- Consumes: `ZipExtraction.ExtractAsync`, `ZipExtraction.Everything` (Task 4); `DataLayout.Staging` (Task 5).
- Produces: `GameSource(string Root, string? StagingDirectory)`, `InvalidGameSourceException(string message)`, `GameSourceResolver(DataLayout)` with `const string ExecutableName = "eboot.bin"` and `Task<GameSource> ResolveAsync(string input, CancellationToken)`.
- Produces (tests): `TestGames.TitleId` (`"PPSA99666"`), `TestGames.ParamJson`, `TestGames.Create(string root)` (SELF `eboot.bin` and `sce_module/libc.prx`, `sce_sys/param.json`, `sce_sys/icon0.png`, `wads/doom1.wad`), and `TestGames.Zip(string prefix) : byte[]`.

- [ ] **Step 1: Write the test game builder and the failing tests**

`tests/PS5Kitsune.Core.Tests/Games/TestGames.cs`:

```csharp
using System.Text;

namespace PS5Kitsune.Core.Tests.Games;

/// <summary>A tiny game shaped like DOOM for PS5 (lowbit/ps5-doom v1.1.1): SELF binaries, param.json, icon, data.</summary>
internal static class TestGames
{
    public const string TitleId = "PPSA99666";

    public const string ParamJson = """
        { "titleId": "PPSA99666", "localizedParameters": { "defaultLanguage": "en-US", "en-US": { "titleName": "DOOM" } } }
        """;

    public static void Create(string root)
    {
        foreach (var (name, content) in Files(""))
        {
            TestFiles.Write(Path.Combine(root, name), content);
        }
    }

    /// <summary>The same files as zip entries under <paramref name="prefix"/> ("" for a flat zip, "PPSA99666/" for a top folder).</summary>
    public static byte[] Zip(string prefix) => TestFiles.ZipBytes([.. Files(prefix)]);

    private static IEnumerable<(string Name, byte[] Content)> Files(string prefix) =>
    [
        (prefix + "eboot.bin", TestFiles.Self()),
        (prefix + "sce_module/libc.prx", TestFiles.Self()),
        (prefix + "sce_sys/param.json", Encoding.UTF8.GetBytes(ParamJson)),
        (prefix + "sce_sys/icon0.png", "png"u8.ToArray()),
        (prefix + "wads/doom1.wad", "wad"u8.ToArray()),
    ];
}
```

`tests/PS5Kitsune.Core.Tests/Games/GameSourceResolverTests.cs`:

```csharp
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class GameSourceResolverTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly DataLayout _layout;
    private readonly GameSourceResolver _resolver;

    public GameSourceResolverTests()
    {
        _layout = new DataLayout(_temp.Combine("root"));
        _resolver = new GameSourceResolver(_layout);
    }

    private Task<GameSource> ResolveAsync(string input) => _resolver.ResolveAsync(input, TestContext.Current.CancellationToken);

    [Fact]
    public async Task A_folder_holding_eboot_bin_is_the_game_root()
    {
        TestGames.Create(_temp.Combine("doom"));

        var source = await ResolveAsync(_temp.Combine("doom"));

        Assert.Equal(_temp.Combine("doom"), source.Root);
        Assert.Null(source.StagingDirectory);
    }

    [Fact]
    public async Task An_app0_subfolder_is_the_game_root()
    {
        TestGames.Create(_temp.Combine("dump", "app0"));

        var source = await ResolveAsync(_temp.Combine("dump"));

        Assert.Equal(_temp.Combine("dump", "app0"), source.Root);
    }

    [Fact]
    public async Task A_single_subfolder_is_the_game_root()
    {
        TestGames.Create(_temp.Combine("download", "PPSA99666"));

        var source = await ResolveAsync(_temp.Combine("download"));

        Assert.Equal(_temp.Combine("download", "PPSA99666"), source.Root);
    }

    [Fact]
    public async Task A_zip_with_a_top_folder_is_extracted_to_staging()
    {
        var zip = _temp.Combine("PPSA99666.zip");
        File.WriteAllBytes(zip, TestGames.Zip("PPSA99666/"));

        var source = await ResolveAsync(zip);

        var staging = Path.Combine(_layout.Staging, "PPSA99666");
        Assert.Equal(staging, source.StagingDirectory);
        Assert.Equal(Path.Combine(staging, "PPSA99666"), source.Root);
        Assert.True(File.Exists(Path.Combine(source.Root, "eboot.bin")));
    }

    [Fact]
    public async Task A_flat_zip_is_its_own_game_root()
    {
        var zip = _temp.Combine("doom.zip");
        File.WriteAllBytes(zip, TestGames.Zip(""));

        var source = await ResolveAsync(zip);

        Assert.Equal(source.StagingDirectory, source.Root);
        Assert.Equal(5, Directory.GetFiles(source.Root, "*", SearchOption.AllDirectories).Length);
    }

    [Fact]
    public async Task A_folder_without_eboot_bin_is_rejected()
    {
        TestFiles.Write(_temp.Combine("empty", "readme.txt"), "nothing here");
        TestFiles.Write(_temp.Combine("empty", "other.txt"), "nothing here");

        var exception = await Assert.ThrowsAsync<InvalidGameSourceException>(() => ResolveAsync(_temp.Combine("empty")));

        Assert.Contains("eboot.bin", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_path_is_rejected()
    {
        await Assert.ThrowsAsync<InvalidGameSourceException>(() => ResolveAsync(_temp.Combine("missing")));
    }

    public void Dispose() => _temp.Dispose();
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*GameSourceResolverTests"`
Expected: the build fails with CS0234, `The type or namespace name 'Games' does not exist in the namespace 'PS5Kitsune.Core'`.

- [ ] **Step 3: Write the resolver**

`src/PS5Kitsune.Core/Games/GameSource.cs`:

```csharp
namespace PS5Kitsune.Core.Games;

/// <param name="Root">Folder holding eboot.bin.</param>
/// <param name="StagingDirectory">Folder a zip was extracted to; null when the input was a folder.</param>
public sealed record GameSource(string Root, string? StagingDirectory);

public sealed class InvalidGameSourceException(string message) : Exception(message);
```

`src/PS5Kitsune.Core/Games/GameSourceResolver.cs`:

```csharp
using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Core.Games;

/// <summary>
/// Turns what the user gave (a folder or a .zip) into the folder holding eboot.bin: the input itself,
/// its app0, or its single subfolder (or that subfolder's app0). A zip is extracted to downloads/staging first.
/// </summary>
public sealed class GameSourceResolver(DataLayout layout)
{
    public const string ExecutableName = "eboot.bin";

    /// <exception cref="InvalidGameSourceException">The input does not exist or holds no eboot.bin where expected.</exception>
    public async Task<GameSource> ResolveAsync(string input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var path = Path.GetFullPath(input);
        if (File.Exists(path) && string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            var staging = Path.Combine(layout.Staging, Path.GetFileNameWithoutExtension(path));
            await ZipExtraction.ExtractAsync(path, staging, archiveSha256: null, ZipExtraction.Everything, cancellationToken).ConfigureAwait(false);
            return new GameSource(FindRoot(staging, input), staging);
        }

        if (Directory.Exists(path))
        {
            return new GameSource(FindRoot(path, input), null);
        }

        throw new InvalidGameSourceException($"{input} is neither a folder nor a .zip file.");
    }

    private static string FindRoot(string folder, string input)
    {
        List<string> candidates = [folder, Path.Combine(folder, "app0")];
        if (Directory.GetDirectories(folder) is [var single])
        {
            candidates.Add(single);
            candidates.Add(Path.Combine(single, "app0"));
        }

        return candidates.FirstOrDefault(candidate => File.Exists(Path.Combine(candidate, ExecutableName)))
            ?? throw new InvalidGameSourceException($"No {ExecutableName} found in {input}, in its app0 folder or in its single subfolder.");
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*GameSourceResolverTests"`
Expected: 7 tests, all pass.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Core/Games tests/PS5Kitsune.Core.Tests/Games
git commit -m "Resolve a game folder or zip to the folder holding eboot.bin

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Validate a game

**Files:**
- Create: `src/PS5Kitsune.Core/Games/GameMetadata.cs`
- Create: `src/PS5Kitsune.Core/Games/ParamJson.cs`
- Create: `src/PS5Kitsune.Core/Games/GameValidation.cs`
- Create: `src/PS5Kitsune.Core/Games/GameValidator.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Games/GameValidatorTests.cs`

**Interfaces:**
- Consumes: `FileTree.Size` (Task 4); `GameSourceResolver.ExecutableName`, `TestGames` (Task 7).
- Produces: `GameMetadata(string TitleId, string Name)`; `IssueSeverity { Warning, Blocking }`; `ValidationIssue(IssueSeverity Severity, string Message)`; `GameValidation(GameMetadata? Metadata, IReadOnlyList<string> ModuleDirectories, long SizeBytes, IReadOnlyList<ValidationIssue> Issues)` with `IsBlocked` and `Warnings`.
- Produces: `GameValidator(Func<string, long> availableBytes)` and `GameValidator()`, with `Validate(string gameRoot, string destination) : GameValidation`, `static IReadOnlyList<string> ModuleDirectoryNames` (`sce_module`, `sce_modules`, `prx`) and `static bool IsValidTitleId(string)`.

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.Core.Tests/Games/GameValidatorTests.cs`:

```csharp
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class GameValidatorTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public GameValidatorTests()
    {
        TestGames.Create(Game);
    }

    private string Game => _temp.Combine("game");

    private GameValidation Validate(long available = long.MaxValue) =>
        new GameValidator(_ => available).Validate(Game, _temp.Combine("games"));

    private void WriteParam(string json) => TestFiles.Write(Path.Combine(Game, "sce_sys", "param.json"), json);

    [Fact]
    public void A_doom_like_game_is_valid()
    {
        var validation = Validate();

        Assert.False(validation.IsBlocked);
        Assert.Empty(validation.Issues);
        Assert.Equal(new GameMetadata("PPSA99666", "DOOM"), validation.Metadata);
        Assert.Equal(["sce_module"], validation.ModuleDirectories);
        Assert.Equal(new DirectoryInfo(Game).EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length), validation.SizeBytes);
    }

    [Theory]
    [InlineData("""{ "titleId": "PPSA1", "localizedParameters": { "defaultLanguage": "fr-FR", "en-US": { "titleName": "English" }, "fr-FR": { "titleName": "Francais" } } }""", "Francais")]
    [InlineData("""{ "titleId": "PPSA1", "defaultLanguage": "fr-FR", "localizedParameters": { "en-US": { "titleName": "English" }, "fr-FR": { "titleName": "Francais" } } }""", "Francais")]
    [InlineData("""{ "titleId": "PPSA1", "localizedParameters": { "ja-JP": { "titleName": "Japanese" }, "en-US": { "titleName": "English" } } }""", "English")]
    [InlineData("""{ "titleId": "PPSA1", "localizedParameters": { "defaultLanguage": "de-DE", "ja-JP": { "titleName": "Japanese" } } }""", "Japanese")]
    [InlineData("""{ "titleId": "PPSA1", "localizedParameters": { "defaultLanguage": "en-US" } }""", "PPSA1")]
    public void The_name_comes_from_the_default_language_then_en_US_then_the_first_language(string json, string expected)
    {
        WriteParam(json);

        Assert.Equal(expected, Validate().Metadata!.Name);
    }

    [Theory]
    [InlineData("{ not json", "not valid JSON")]
    [InlineData("""{ "localizedParameters": { "en-US": { "titleName": "X" } } }""", "no titleId")]
    [InlineData("""{ "titleId": "PPSA1" }""", "no localizedParameters")]
    [InlineData("""{ "titleId": "PPSA1", "localizedParameters": {} }""", "no localizedParameters")]
    [InlineData("""{ "titleId": "../evil", "localizedParameters": { "en-US": { "titleName": "X" } } }""", "letters and digits")]
    public void An_invalid_param_json_is_blocking(string json, string expected)
    {
        WriteParam(json);

        var validation = Validate();

        Assert.True(validation.IsBlocked);
        Assert.Contains(validation.Issues, issue => issue.Severity == IssueSeverity.Blocking && issue.Message.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void A_missing_param_json_is_blocking()
    {
        File.Delete(Path.Combine(Game, "sce_sys", "param.json"));

        Assert.Contains(Validate().Issues, issue => issue.Severity == IssueSeverity.Blocking && issue.Message.Contains("param.json is missing", StringComparison.Ordinal));
    }

    [Fact]
    public void Sce_module_and_sce_modules_together_are_blocking()
    {
        Directory.CreateDirectory(Path.Combine(Game, "sce_modules"));

        Assert.Contains(Validate().Issues, issue => issue.Severity == IssueSeverity.Blocking && issue.Message.Contains("sce_module and sce_modules", StringComparison.Ordinal));
    }

    [Fact]
    public void Prx_beside_sce_module_is_accepted()
    {
        TestFiles.Write(Path.Combine(Game, "prx", "extra.prx"), TestFiles.Elf());

        var validation = Validate();

        Assert.False(validation.IsBlocked);
        Assert.Equal(["sce_module", "prx"], validation.ModuleDirectories);
    }

    [Fact]
    public void No_module_folder_is_a_warning()
    {
        Directory.Delete(Path.Combine(Game, "sce_module"), recursive: true);

        var validation = Validate();

        Assert.False(validation.IsBlocked);
        Assert.Equal(IssueSeverity.Warning, Assert.Single(validation.Warnings).Severity);
        Assert.Empty(validation.ModuleDirectories);
    }

    [Fact]
    public void Not_enough_free_space_is_blocking()
    {
        Assert.Contains(Validate(available: 10).Issues, issue => issue.Severity == IssueSeverity.Blocking && issue.Message.Contains("free space", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_blocking_issue_is_reported_at_once()
    {
        File.Delete(Path.Combine(Game, "eboot.bin"));
        File.Delete(Path.Combine(Game, "sce_sys", "param.json"));

        var validation = Validate(available: 10);

        Assert.Equal(3, validation.Issues.Count(issue => issue.Severity == IssueSeverity.Blocking));
        Assert.Null(validation.Metadata);
    }

    public void Dispose() => _temp.Dispose();
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*GameValidatorTests"`
Expected: the build fails with CS0246, `The type or namespace name 'GameValidation' could not be found`.

- [ ] **Step 3: Write the validation types**

`src/PS5Kitsune.Core/Games/GameMetadata.cs`:

```csharp
namespace PS5Kitsune.Core.Games;

/// <param name="TitleId">From param.json; also the name of the game folder.</param>
/// <param name="Name">Display name, resolved from localizedParameters.</param>
public sealed record GameMetadata(string TitleId, string Name);
```

`src/PS5Kitsune.Core/Games/GameValidation.cs`:

```csharp
namespace PS5Kitsune.Core.Games;

public enum IssueSeverity
{
    Warning,
    Blocking,
}

public sealed record ValidationIssue(IssueSeverity Severity, string Message);

/// <param name="Metadata">Null when param.json is missing or invalid.</param>
/// <param name="ModuleDirectories">Module folders present in the game root, among <see cref="GameValidator.ModuleDirectoryNames"/>.</param>
public sealed record GameValidation(GameMetadata? Metadata, IReadOnlyList<string> ModuleDirectories, long SizeBytes, IReadOnlyList<ValidationIssue> Issues)
{
    public bool IsBlocked => Issues.Any(issue => issue.Severity == IssueSeverity.Blocking);

    public IReadOnlyList<ValidationIssue> Warnings => [.. Issues.Where(issue => issue.Severity == IssueSeverity.Warning)];
}
```

`src/PS5Kitsune.Core/Games/ParamJson.cs`:

```csharp
using System.Text.Json;

namespace PS5Kitsune.Core.Games;

/// <summary>Reads what AnyPS5 needs from sce_sys/param.json. See docs/architecture.md, "V2 — Add a game".</summary>
internal static class ParamJson
{
    /// <exception cref="InvalidDataException">The file is not what AnyPS5 reads; the message says why.</exception>
    public static GameMetadata Read(string path)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"sce_sys/param.json is not valid JSON: {exception.Message}", exception);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("sce_sys/param.json must hold a JSON object.");
            }

            var titleId = GetString(root, "titleId");
            if (string.IsNullOrWhiteSpace(titleId))
            {
                throw new InvalidDataException("sce_sys/param.json has no titleId.");
            }

            if (!GameValidator.IsValidTitleId(titleId))
            {
                throw new InvalidDataException($"Title ID \"{titleId}\" must contain only letters and digits.");
            }

            if (!root.TryGetProperty("localizedParameters", out var localized)
                || localized.ValueKind != JsonValueKind.Object
                || !localized.EnumerateObject().Any())
            {
                throw new InvalidDataException("sce_sys/param.json has no localizedParameters.");
            }

            return new GameMetadata(titleId, ResolveName(root, localized) ?? titleId);
        }
    }

    // titleName of the default language (declared in localizedParameters or at the top level), else en-US, else the first language.
    private static string? ResolveName(JsonElement root, JsonElement localized)
    {
        var defaultLanguage = GetString(localized, "defaultLanguage") ?? GetString(root, "defaultLanguage");
        foreach (var language in new[] { defaultLanguage, "en-US" })
        {
            if (language is not null && localized.TryGetProperty(language, out var entry) && TitleName(entry) is { } name)
            {
                return name;
            }
        }

        return localized.EnumerateObject().Select(property => TitleName(property.Value)).FirstOrDefault(name => name is not null);
    }

    private static string? TitleName(JsonElement entry) =>
        entry.ValueKind == JsonValueKind.Object && GetString(entry, "titleName") is { } name && !string.IsNullOrWhiteSpace(name) ? name : null;

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
```

`src/PS5Kitsune.Core/Games/GameValidator.cs`:

```csharp
using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Core.Games;

/// <summary>
/// Checks a game root before anything is copied, and reports every issue at once.
/// See docs/architecture.md, "V2 — Add a game", step 2.
/// </summary>
/// <param name="availableBytes">Free space for a destination path; replaced in tests.</param>
public sealed class GameValidator(Func<string, long> availableBytes)
{
    public GameValidator()
        : this(AvailableBytesOnDrive)
    {
    }

    public static IReadOnlyList<string> ModuleDirectoryNames { get; } = ["sce_module", "sce_modules", "prx"];

    /// <summary>A title ID becomes a folder name: ASCII letters and digits only.</summary>
    public static bool IsValidTitleId(string titleId) =>
        !string.IsNullOrEmpty(titleId) && titleId.All(char.IsAsciiLetterOrDigit);

    /// <param name="destination">Folder the game will be copied under; only its drive's free space matters.</param>
    public GameValidation Validate(string gameRoot, string destination)
    {
        var issues = new List<ValidationIssue>();
        if (!File.Exists(Path.Combine(gameRoot, GameSourceResolver.ExecutableName)))
        {
            issues.Add(Blocking($"{GameSourceResolver.ExecutableName} is missing."));
        }

        var metadata = ReadMetadata(gameRoot, issues);

        List<string> modules = [.. ModuleDirectoryNames.Where(name => Directory.Exists(Path.Combine(gameRoot, name)))];
        if (modules.Contains("sce_module") && modules.Contains("sce_modules"))
        {
            issues.Add(Blocking("sce_module and sce_modules both exist: the relinker refuses this layout. Keep only one of them."));
        }

        if (modules.Count == 0)
        {
            issues.Add(new ValidationIssue(IssueSeverity.Warning, "No module folder (sce_module, sce_modules or prx): libraries kept beside eboot.bin are not converted yet."));
        }

        var size = FileTree.Size(gameRoot);
        var available = availableBytes(destination);
        if (available < size)
        {
            issues.Add(Blocking($"Not enough free space for {destination}: {size / 1024 / 1024} MiB needed, {available / 1024 / 1024} MiB available."));
        }

        return new GameValidation(metadata, modules, size, issues);
    }

    private static GameMetadata? ReadMetadata(string gameRoot, List<ValidationIssue> issues)
    {
        var path = Path.Combine(gameRoot, "sce_sys", "param.json");
        if (!File.Exists(path))
        {
            issues.Add(Blocking("sce_sys/param.json is missing."));
            return null;
        }

        try
        {
            return ParamJson.Read(path);
        }
        catch (InvalidDataException exception)
        {
            issues.Add(Blocking(exception.Message));
            return null;
        }
    }

    private static ValidationIssue Blocking(string message) => new(IssueSeverity.Blocking, message);

    private static long AvailableBytesOnDrive(string path) =>
        new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!).AvailableFreeSpace;
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*GameValidatorTests"`
Expected: 17 tests (theory rows count separately), all pass.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Core/Games tests/PS5Kitsune.Core.Tests/Games
git commit -m "Validate a game before copying it

Reads titleId and the display name from param.json, checks the module
folders and free space, and reports every blocking issue at once.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: CPU vendor, game folder layout and copy steps

**Files:**
- Create: `src/PS5Kitsune.Core/Checks/CpuVendor.cs`
- Create: `src/PS5Kitsune.Core/Games/GameLayout.cs`
- Create: `src/PS5Kitsune.Core/Games/GameImportContext.cs`
- Create: `src/PS5Kitsune.Core/Games/GameImportException.cs`
- Create: `src/PS5Kitsune.Core/Games/Steps/CopyGameStep.cs`
- Create: `src/PS5Kitsune.Core/Games/Steps/CopyLibsStep.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Checks/CpuVendorTests.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Games/CopyStepsTests.cs`

**Interfaces:**
- Consumes: `FileTree`, `ZipExtraction.MarkerFileName` (Task 4); `AnyPS5Paths` (Task 5); `GameSource` (Task 7); `GameMetadata` (Task 8).
- Produces: `CpuVendor.Detect() : string` (`"GenuineIntel"`, `"AuthenticAMD"`, … or `"unknown"`) and `CpuVendor.IsIntel(string) : bool`.
- Produces: `GameLayout(string root, string titleId, IPlatform platform)` with `Root`, `TitleId`, `App0`, `Libs`, `ExecutableName` (`<titleId>.exe`), `Executable`, `Registry` (`<titleId>.registry.json`), `AuditFile`, `Meta` (`.ps5kitsune`), `Originals`, `Logs`, `RecordFile` (`.ps5kitsune/game.json`).
- Produces: `GameImportContext(GameSource source, GameMetadata metadata, IReadOnlyList<string> moduleDirectories, GameLayout game, bool toIntel)` with get-only properties of the same names and a settable `AuditSummary? Audit`. `AuditSummary` arrives in Task 11, so this task declares the context **without** `Audit`; Task 11 adds it.
- Produces: `GameImportException(string message)`; `CopyGameStep(GameImportContext)`; `CopyLibsStep(AnyPS5Paths, GameLayout)`.

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.Core.Tests/Checks/CpuVendorTests.cs`:

```csharp
using System.Runtime.Intrinsics.X86;
using PS5Kitsune.Core.Checks;

namespace PS5Kitsune.Core.Tests.Checks;

public sealed class CpuVendorTests
{
    [Fact]
    public void Reads_a_twelve_character_vendor_on_x86()
    {
        Assert.SkipUnless(X86Base.IsSupported, "Not an x86 CPU.");

        var vendor = CpuVendor.Detect();

        Assert.Equal(12, vendor.Length);
        Assert.All(vendor, character => Assert.True(char.IsAsciiLetterOrDigit(character) || character == ' '));
    }

    [Theory]
    [InlineData("GenuineIntel", true)]
    [InlineData("AuthenticAMD", false)]
    [InlineData("unknown", false)]
    public void Only_GenuineIntel_is_Intel(string vendor, bool expected)
    {
        Assert.Equal(expected, CpuVendor.IsIntel(vendor));
    }
}
```

`tests/PS5Kitsune.Core.Tests/Games/CopyStepsTests.cs`:

```csharp
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Games.Steps;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class CopyStepsTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly GameLayout _game;
    private readonly GameImportContext _context;

    public CopyStepsTests()
    {
        var source = _temp.Combine("source");
        TestGames.Create(source);
        _game = new GameLayout(_temp.Combine("games", TestGames.TitleId), TestGames.TitleId, new FakePlatform());
        _context = new GameImportContext(new GameSource(source, null), new GameMetadata(TestGames.TitleId, "DOOM"), ["sce_module"], _game, toIntel: false);
    }

    [Fact]
    public async Task CopyGame_copies_the_game_into_app0()
    {
        var step = new CopyGameStep(_context);
        Assert.False(step.IsAlreadyDone());

        await step.ExecuteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(TestFiles.Self(), File.ReadAllBytes(Path.Combine(_game.App0, "eboot.bin")));
        Assert.True(File.Exists(Path.Combine(_game.App0, "wads", "doom1.wad")));
        Assert.True(step.IsAlreadyDone());
    }

    [Fact]
    public async Task CopyGame_counts_a_file_backed_up_before_unwrapping_as_copied()
    {
        var step = new CopyGameStep(_context);
        await step.ExecuteAsync(null, TestContext.Current.CancellationToken);

        Directory.CreateDirectory(_game.Originals);
        File.Move(Path.Combine(_game.App0, "eboot.bin"), Path.Combine(_game.Originals, "eboot.bin"));
        TestFiles.Write(Path.Combine(_game.App0, "eboot.bin"), TestFiles.Elf());

        Assert.True(step.IsAlreadyDone());
    }

    [Fact]
    public async Task CopyLibs_copies_the_install_libs_without_the_extraction_marker()
    {
        var paths = new AnyPS5Paths(new DataLayout(_temp.Combine("root")), new FakePlatform());
        TestFiles.Write(Path.Combine(paths.Libs, "libSceAudioOut.prx"), "prx");
        TestFiles.Write(Path.Combine(paths.Libs, ".ps5kitsune-extracted"), "hash");
        var step = new CopyLibsStep(paths, _game);
        Assert.False(step.IsAlreadyDone());

        await step.ExecuteAsync(null, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(_game.Libs, "libSceAudioOut.prx")));
        Assert.False(File.Exists(Path.Combine(_game.Libs, ".ps5kitsune-extracted")));
        Assert.True(step.IsAlreadyDone());
    }

    [Fact]
    public void The_game_layout_follows_the_architecture()
    {
        Assert.Equal(Path.Combine(_game.Root, "PPSA99666.exe"), _game.Executable);
        Assert.Equal(Path.Combine(_game.Root, "PPSA99666.registry.json"), _game.Registry);
        Assert.Equal(Path.Combine(_game.Root, ".ps5kitsune", "game.json"), _game.RecordFile);
        Assert.Equal(Path.Combine(_game.Root, ".ps5kitsune", "originals"), _game.Originals);
        Assert.Equal(Path.Combine(_game.Root, ".ps5kitsune", "logs"), _game.Logs);
    }

    public void Dispose() => _temp.Dispose();
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*CopyStepsTests"`
Expected: the build fails with CS0246, `The type or namespace name 'GameLayout' could not be found`.

- [ ] **Step 3: Write the types and steps**

`src/PS5Kitsune.Core/Checks/CpuVendor.cs`:

```csharp
using System.Buffers.Binary;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace PS5Kitsune.Core.Checks;

/// <summary>CPU vendor from CPUID leaf 0. The full PC checks come with milestone 3.</summary>
public static class CpuVendor
{
    public static string Detect()
    {
        if (!X86Base.IsSupported)
        {
            return "unknown";
        }

        // The vendor string is EBX, EDX, ECX in that order, 4 ASCII bytes each.
        var (_, ebx, ecx, edx) = X86Base.CpuId(0, 0);
        Span<byte> vendor = stackalloc byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(vendor, ebx);
        BinaryPrimitives.WriteInt32LittleEndian(vendor[4..], edx);
        BinaryPrimitives.WriteInt32LittleEndian(vendor[8..], ecx);
        return Encoding.ASCII.GetString(vendor);
    }

    /// <summary>True for Intel CPUs, which need the relinker's --to-intel option.</summary>
    public static bool IsIntel(string vendor) => vendor == "GenuineIntel";
}
```

`src/PS5Kitsune.Core/Games/GameLayout.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Games;

/// <summary>Files of one game folder. See docs/architecture.md, "Root layout".</summary>
public sealed class GameLayout(string root, string titleId, IPlatform platform)
{
    public string Root { get; } = Path.GetFullPath(root);

    public string TitleId { get; } = titleId;

    /// <summary>The game as supplied (eboot.bin unwrapped in place).</summary>
    public string App0 => Path.Combine(Root, "app0");

    public string Libs => Path.Combine(Root, "libs");

    /// <summary>Name of the converted executable, which also names its registry files.</summary>
    public string ExecutableName => TitleId + platform.ExecutableSuffix;

    public string Executable => Path.Combine(Root, ExecutableName);

    public string Registry => Path.Combine(Root, TitleId + ".registry.json");

    public string AuditFile => Path.Combine(Root, "audit.json");

    /// <summary>The app's own folder inside the game, kept out of app0.</summary>
    public string Meta => Path.Combine(Root, ".ps5kitsune");

    /// <summary>Files as they were before unwrapping, at their path relative to app0.</summary>
    public string Originals => Path.Combine(Meta, "originals");

    public string Logs => Path.Combine(Meta, "logs");

    public string RecordFile => Path.Combine(Meta, "game.json");
}
```

`src/PS5Kitsune.Core/Games/GameImportContext.cs`:

```csharp
namespace PS5Kitsune.Core.Games;

/// <summary>What the add steps share: the source, the validated metadata, the game folder and the results.</summary>
public sealed class GameImportContext(GameSource source, GameMetadata metadata, IReadOnlyList<string> moduleDirectories, GameLayout game, bool toIntel)
{
    public GameSource Source { get; } = source;

    public GameMetadata Metadata { get; } = metadata;

    /// <summary>Module folders of the game (sce_module, sce_modules, prx), relative to app0.</summary>
    public IReadOnlyList<string> ModuleDirectories { get; } = moduleDirectories;

    public GameLayout Game { get; } = game;

    /// <summary>Whether the relinker gets its Intel option.</summary>
    public bool ToIntel { get; } = toIntel;
}
```

`src/PS5Kitsune.Core/Games/GameImportException.cs`:

```csharp
namespace PS5Kitsune.Core.Games;

/// <summary>An add step failed for a reason the user can act on; the message says what happened.</summary>
public sealed class GameImportException(string message) : Exception(message);
```

`src/PS5Kitsune.Core/Games/Steps/CopyGameStep.cs`:

```csharp
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.Games.Steps;

/// <summary>Copies the game root into app0 as it is. Done when every source file is there with the same size.</summary>
public sealed class CopyGameStep(GameImportContext context) : IStep
{
    public string Name => "Copy the game";

    public bool IsAlreadyDone() => FileTree.RelativeFiles(context.Source.Root).All(IsCopied);

    public Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken) =>
        Task.Run(() => FileTree.Copy(context.Source.Root, context.Game.App0, IsCopied, cancellationToken), cancellationToken);

    // An unwrapped file has a new size in app0; its copy as supplied is in originals.
    private bool IsCopied(string relative)
    {
        var source = Path.Combine(context.Source.Root, relative);
        return FileTree.SameSize(source, Path.Combine(context.Game.App0, relative))
            || FileTree.SameSize(source, Path.Combine(context.Game.Originals, relative));
    }
}
```

`src/PS5Kitsune.Core/Games/Steps/CopyLibsStep.cs`:

```csharp
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.Games.Steps;

/// <summary>
/// Copies the install's libs/ beside the game. The folder is fully controlled by the app,
/// so no Sony .prx can slip in (error 193).
/// </summary>
public sealed class CopyLibsStep(AnyPS5Paths paths, GameLayout game) : IStep
{
    public string Name => "Copy the AnyPS5 libraries";

    public bool IsAlreadyDone() => LibFiles().All(IsCopied);

    public Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken) =>
        Task.Run(() => FileTree.Copy(paths.Libs, game.Libs, relative => relative == ZipExtraction.MarkerFileName || IsCopied(relative), cancellationToken), cancellationToken);

    private IEnumerable<string> LibFiles() =>
        FileTree.RelativeFiles(paths.Libs).Where(relative => relative != ZipExtraction.MarkerFileName);

    private bool IsCopied(string relative) =>
        FileTree.SameSize(Path.Combine(paths.Libs, relative), Path.Combine(game.Libs, relative));
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*CopyStepsTests"` then `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*CpuVendorTests"`
Expected: 4 tests pass, then 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Core tests/PS5Kitsune.Core.Tests
git commit -m "Copy a game and the AnyPS5 libraries into the games folder

Also detect the CPU vendor for the relinker's Intel option.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Unwrap SELF files

**Files:**
- Create: `src/PS5Kitsune.Core/Processes/OutputTail.cs`
- Create: `src/PS5Kitsune.Core/Games/BinaryKinds.cs`
- Create: `src/PS5Kitsune.Core/Games/Steps/UnwrapSelfStep.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Games/BinaryKindsTests.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Games/UnwrapSelfStepTests.cs`

**Interfaces:**
- Consumes: `PythonEmbeddableTool.CreateScriptSpec` (Task 1); `AnyPS5Paths.UnwrapSelf` (Task 5); `GameImportContext`, `GameImportException`, `FileTree` (Tasks 4, 9).
- Produces: `OutputTail(int capacity = 20)` with `void Collect(OutputLine)` (keeps standard-error lines only) and `ToString()` (last lines joined by newlines).
- Produces: `BinaryKind { Missing, Elf, Self, Other }`, `BinaryKinds.Detect(string path) : BinaryKind`.
- Produces: `UnwrapSelfStep(GameImportContext, AnyPS5Paths, PythonEmbeddableTool, IProcessRunner)`. It runs the script with arguments `[unwrap_self.py, <backup>, <original>]`: `eboot.bin` first, then the module files in ordinal order.

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.Core.Tests/Games/BinaryKindsTests.cs`:

```csharp
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class BinaryKindsTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Theory]
    [InlineData(new byte[] { 0x7F, 0x45, 0x4C, 0x46, 0x02 }, BinaryKind.Elf)]
    [InlineData(new byte[] { 0x4F, 0x15, 0x3D, 0x1D, 0x00 }, BinaryKind.Self)]
    [InlineData(new byte[] { 0x54, 0x14, 0xF5, 0xEE, 0x00 }, BinaryKind.Self)]
    [InlineData(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, BinaryKind.Other)]
    [InlineData(new byte[] { 0x7F, 0x45 }, BinaryKind.Other)]
    public void Detects_the_kind_from_the_first_four_bytes(byte[] content, BinaryKind expected)
    {
        var path = _temp.Combine("file");
        TestFiles.Write(path, content);

        Assert.Equal(expected, BinaryKinds.Detect(path));
    }

    [Fact]
    public void A_missing_file_is_missing()
    {
        Assert.Equal(BinaryKind.Missing, BinaryKinds.Detect(_temp.Combine("missing")));
    }

    public void Dispose() => _temp.Dispose();
}
```

`tests/PS5Kitsune.Core.Tests/Games/UnwrapSelfStepTests.cs`:

```csharp
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Games.Steps;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class UnwrapSelfStepTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FakeProcessRunner _runner = new();
    private readonly GameLayout _game;
    private readonly AnyPS5Paths _paths;
    private readonly PythonEmbeddableTool _python;

    public UnwrapSelfStepTests()
    {
        var platform = new FakePlatform();
        var layout = new DataLayout(_temp.Combine("root"));
        _game = new GameLayout(_temp.Combine("games", TestGames.TitleId), TestGames.TitleId, platform);
        _paths = new AnyPS5Paths(layout, platform);
        _python = new PythonEmbeddableTool(new ToolDefinition("3.14.8", new Uri("https://example.test/p.zip"), new string('a', 64)), layout, platform, new FakeDownloader(), _runner);

        TestFiles.Write(App0("eboot.bin"), TestFiles.Self());
        TestFiles.Write(App0("sce_module", "libc.prx"), TestFiles.Self());
        TestFiles.Write(App0("sce_module", "libfoo.prx"), TestFiles.Elf());
        TestFiles.Write(App0("sce_module", "notes.txt"), "not a binary");
        TestFiles.Write(App0("wads", "doom1.wad"), TestFiles.Self());

        // Plays unwrap_self.py: writes an ELF to the output path (last argument).
        _runner.Respond = spec =>
        {
            TestFiles.Write(spec.Arguments[^1], TestFiles.Elf());
            return new FakeProcessResult(0);
        };
    }

    private string App0(params string[] parts) => Path.Combine([_game.App0, .. parts]);

    private string Original(params string[] parts) => Path.Combine([_game.Originals, .. parts]);

    private UnwrapSelfStep CreateStep() => new(
        new GameImportContext(new GameSource(_temp.Combine("unused"), null), new GameMetadata(TestGames.TitleId, "DOOM"), ["sce_module"], _game, toIntel: false),
        _paths, _python, _runner);

    [Fact]
    public async Task Unwraps_the_self_files_after_backing_them_up()
    {
        var step = CreateStep();
        Assert.False(step.IsAlreadyDone());

        await step.ExecuteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(2, _runner.Calls.Count);
        Assert.Equal(TestFiles.Self(), File.ReadAllBytes(Original("eboot.bin")));
        Assert.Equal(TestFiles.Self(), File.ReadAllBytes(Original("sce_module", "libc.prx")));
        Assert.Equal(TestFiles.Elf(), File.ReadAllBytes(App0("eboot.bin")));
        Assert.Equal(TestFiles.Elf(), File.ReadAllBytes(App0("sce_module", "libc.prx")));
        Assert.False(File.Exists(Original("sce_module", "libfoo.prx")));
        Assert.False(File.Exists(Original("sce_module", "notes.txt")));
        Assert.False(File.Exists(Original("wads", "doom1.wad")));
        Assert.True(step.IsAlreadyDone());
    }

    [Fact]
    public async Task Runs_unwrap_self_py_from_the_backup_to_the_original_path()
    {
        await CreateStep().ExecuteAsync(null, TestContext.Current.CancellationToken);

        var call = _runner.Calls[0];
        Assert.Equal(_python.Executable, call.FileName);
        Assert.Equal([_paths.UnwrapSelf, Original("eboot.bin"), App0("eboot.bin")], call.Arguments.Skip(2));
    }

    [Fact]
    public async Task A_refused_container_says_the_game_must_come_already_unwrapped()
    {
        _runner.Respond = _ => new FakeProcessResult(2, StandardError: ["FAIL: eboot.bin: every load segment looks encrypted"]);

        var exception = await Assert.ThrowsAsync<GameImportException>(() => CreateStep().ExecuteAsync(null, TestContext.Current.CancellationToken));

        Assert.Contains("already unwrapped from the dump tool", exception.Message, StringComparison.Ordinal);
        Assert.Contains("every load segment looks encrypted", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resumes_from_the_backup_when_the_unwrapped_file_is_missing()
    {
        Directory.CreateDirectory(_game.Originals);
        File.Move(App0("eboot.bin"), Original("eboot.bin"));
        var step = CreateStep();
        Assert.False(step.IsAlreadyDone());

        await step.ExecuteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(TestFiles.Elf(), File.ReadAllBytes(App0("eboot.bin")));
        Assert.Equal(TestFiles.Self(), File.ReadAllBytes(Original("eboot.bin")));
    }

    public void Dispose() => _temp.Dispose();
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*UnwrapSelfStepTests"`
Expected: the build fails with CS0246, `The type or namespace name 'UnwrapSelfStep' could not be found` (and `BinaryKind`).

- [ ] **Step 3: Write `OutputTail`, `BinaryKinds` and the step**

`src/PS5Kitsune.Core/Processes/OutputTail.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.Processes;

/// <summary>Keeps the last lines a tool wrote to its error output, to quote them in an error message.</summary>
public sealed class OutputTail(int capacity = 20)
{
    private readonly Queue<string> _lines = new();
    private readonly Lock _lock = new();

    /// <summary>Pass as the output callback of <see cref="IProcessRunner.RunAsync"/>.</summary>
    public void Collect(OutputLine line)
    {
        if (line.Source != OutputSource.StandardError)
        {
            return;
        }

        lock (_lock)
        {
            _lines.Enqueue(line.Text);
            if (_lines.Count > capacity)
            {
                _lines.Dequeue();
            }
        }
    }

    public override string ToString()
    {
        lock (_lock)
        {
            return string.Join(Environment.NewLine, _lines);
        }
    }
}
```

`src/PS5Kitsune.Core/Games/BinaryKinds.cs`:

```csharp
namespace PS5Kitsune.Core.Games;

public enum BinaryKind
{
    Missing,
    Elf,
    Self,
    Other,
}

public static class BinaryKinds
{
    /// <summary>
    /// ELF: 7F 45 4C 46. SELF: 4F 15 3D 1D or 54 14 F5 EE, the two magics unwrap_self.py accepts.
    /// </summary>
    public static BinaryKind Detect(string path)
    {
        if (!File.Exists(path))
        {
            return BinaryKind.Missing;
        }

        Span<byte> magic = stackalloc byte[4];
        using var stream = File.OpenRead(path);
        if (stream.ReadAtLeast(magic, magic.Length, throwOnEndOfStream: false) < magic.Length)
        {
            return BinaryKind.Other;
        }

        return magic switch
        {
            [0x7F, 0x45, 0x4C, 0x46] => BinaryKind.Elf,
            [0x4F, 0x15, 0x3D, 0x1D] or [0x54, 0x14, 0xF5, 0xEE] => BinaryKind.Self,
            _ => BinaryKind.Other,
        };
    }
}
```

`src/PS5Kitsune.Core/Games/Steps/UnwrapSelfStep.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Processes;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.Games.Steps;

/// <summary>
/// Unwraps the SELF containers among eboot.bin and the module files: the relinker skips an unwrapped SELF
/// module with only a warning. Each original goes to .ps5kitsune/originals first, outside app0, because the
/// relinker may skip files whose name extends another module's.
/// </summary>
public sealed class UnwrapSelfStep(GameImportContext context, AnyPS5Paths paths, PythonEmbeddableTool python, IProcessRunner processRunner) : IStep
{
    public string Name => "Unwrap SELF files";

    public bool IsAlreadyDone() => !FilesToUnwrap().Any();

    public async Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        foreach (var relative in FilesToUnwrap().ToList())
        {
            var original = Path.Combine(context.Game.App0, relative);
            var backup = Path.Combine(context.Game.Originals, relative);
            if (File.Exists(original))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                File.Move(original, backup, overwrite: true);
            }

            progress?.Report(new StepProgress($"Unwrapping {relative}"));
            var errors = new OutputTail();
            var spec = python.CreateScriptSpec(paths.UnwrapSelf, [backup, original]);
            var result = await processRunner.RunAsync(spec, errors.Collect, cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                throw new GameImportException(
                    $"unwrap_self.py could not unwrap {relative} (exit code {result.ExitCode}): the game must come already unwrapped from the dump tool.{Environment.NewLine}{errors}");
            }
        }
    }

    private IEnumerable<string> FilesToUnwrap() => Candidates().Where(NeedsUnwrap);

    // eboot.bin, then the files of the module folders, as paths relative to app0. Backups count too,
    // so a file moved to originals by an interrupted run is still found.
    private IEnumerable<string> Candidates()
    {
        yield return GameSourceResolver.ExecutableName;
        foreach (var directory in context.ModuleDirectories)
        {
            var files = FileTree.RelativeFiles(Path.Combine(context.Game.App0, directory))
                .Concat(FileTree.RelativeFiles(Path.Combine(context.Game.Originals, directory)))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal);
            foreach (var file in files)
            {
                yield return Path.Combine(directory, file);
            }
        }
    }

    // A SELF in app0, or a backup whose unwrapped copy is missing or incomplete.
    private bool NeedsUnwrap(string relative)
    {
        var current = BinaryKinds.Detect(Path.Combine(context.Game.App0, relative));
        return current == BinaryKind.Self
            || (current != BinaryKind.Elf && File.Exists(Path.Combine(context.Game.Originals, relative)));
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*UnwrapSelfStepTests"` then `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*BinaryKindsTests"`
Expected: 4 tests pass, then 6 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Core tests/PS5Kitsune.Core.Tests
git commit -m "Unwrap SELF files with unwrap_self.py, keeping the originals

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: Relink, audit and verdict

**Files:**
- Create: `src/PS5Kitsune.Core/Games/AuditSummary.cs`
- Modify: `src/PS5Kitsune.Core/Games/GameImportContext.cs`
- Create: `src/PS5Kitsune.Core/Games/Steps/RelinkStep.cs`
- Create: `src/PS5Kitsune.Core/Games/Steps/AuditStep.cs`
- Create: `tests/PS5Kitsune.Core.Tests/Games/AuditFiles.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Games/AuditSummaryTests.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Games/RelinkStepTests.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Games/AuditStepTests.cs`

**Interfaces:**
- Consumes: `OutputTail` (Task 10); `PythonEmbeddableTool.CreateScriptSpec` (Task 1); `AnyPS5Paths.Relinker/ImportAudit` (Task 5); `AnyPS5Profile.Relinker.WindowsArgs/IntelArg`; `GameImportContext`, `GameLayout` (Task 9).
- Produces: `AuditVerdict { Green, Orange, Red }`; `AuditSummary(int Implemented, int Stub, int Absent, IReadOnlyDictionary<string, int> MissingLibraries)` with `Verdict` and `static AuditSummary Read(string path)`, which throws `InvalidDataException`. This reads `unique_by_class.{implemented,stub,absent}` and `missing_libraries` from import_audit.py v0.1.2.
- Produces: `GameImportContext.Audit { get; set; }` (`AuditSummary?`); `RelinkStep(GameImportContext, AnyPS5Paths, AnyPS5Profile, IProcessRunner)`; `AuditStep(GameImportContext, AnyPS5Paths, PythonEmbeddableTool, IProcessRunner)`.
- Produces (tests): `AuditFiles.Json(int implemented, int stub, int absent, string missingLibraries = "{}") : string`.

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.Core.Tests/Games/AuditFiles.cs`:

```csharp
namespace PS5Kitsune.Core.Tests.Games;

/// <summary>The part of import_audit.py's --json output that PS5Kitsune reads (AnyPS5 v0.1.2).</summary>
internal static class AuditFiles
{
    public static string Json(int implemented, int stub, int absent, string missingLibraries = "{}") => $$"""
        {
          "source_commit": null,
          "references": 400,
          "unique_imports": {{implemented + stub + absent}},
          "unique_by_class": { "implemented": {{implemented}}, "stub": {{stub}}, "absent": {{absent}}, "module": 0 },
          "missing_libraries": {{missingLibraries}},
          "imports": []
        }
        """;
}
```

`tests/PS5Kitsune.Core.Tests/Games/AuditSummaryTests.cs`:

```csharp
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class AuditSummaryTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Theory]
    [InlineData(0, 0, "{}", AuditVerdict.Green)]
    [InlineData(5, 0, "{}", AuditVerdict.Orange)]
    [InlineData(5, 2, "{}", AuditVerdict.Red)]
    [InlineData(0, 0, """{ "libSceFoo.prx": 4 }""", AuditVerdict.Red)]
    public void The_verdict_follows_absent_stub_and_missing_libraries(int stub, int absent, string missing, AuditVerdict expected)
    {
        var path = _temp.Combine("audit.json");
        TestFiles.Write(path, AuditFiles.Json(262, stub, absent, missing));

        var summary = AuditSummary.Read(path);

        Assert.Equal(expected, summary.Verdict);
        Assert.Equal((262, stub, absent), (summary.Implemented, summary.Stub, summary.Absent));
    }

    [Fact]
    public void A_file_that_is_not_an_audit_result_is_invalid_data()
    {
        var path = _temp.Combine("audit.json");
        TestFiles.Write(path, """{ "something": "else" }""");

        Assert.Throws<InvalidDataException>(() => AuditSummary.Read(path));
    }

    public void Dispose() => _temp.Dispose();
}
```

`tests/PS5Kitsune.Core.Tests/Games/RelinkStepTests.cs`:

```csharp
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Games.Steps;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class RelinkStepTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FakeProcessRunner _runner = new();
    private readonly GameLayout _game;
    private readonly AnyPS5Paths _paths;

    public RelinkStepTests()
    {
        var platform = new FakePlatform();
        _game = new GameLayout(_temp.Combine("games", TestGames.TitleId), TestGames.TitleId, platform);
        _paths = new AnyPS5Paths(new DataLayout(_temp.Combine("root")), platform);

        // Plays the relinker: writes the executable and its registry in the working directory.
        _runner.Respond = spec =>
        {
            TestFiles.Write(Path.Combine(spec.WorkingDirectory!, "PPSA99666.exe"), "exe");
            TestFiles.Write(Path.Combine(spec.WorkingDirectory!, "PPSA99666.registry.json"), "[]");
            return new FakeProcessResult(0);
        };
    }

    private RelinkStep CreateStep(bool toIntel) => new(
        new GameImportContext(new GameSource(_temp.Combine("unused"), null), new GameMetadata(TestGames.TitleId, "DOOM"), ["sce_module"], _game, toIntel),
        _paths, ProfileLoader.LoadEmbedded(), _runner);

    [Fact]
    public async Task Runs_the_relinker_from_the_game_folder()
    {
        var step = CreateStep(toIntel: false);
        Assert.False(step.IsAlreadyDone());

        await step.ExecuteAsync(null, TestContext.Current.CancellationToken);

        var call = Assert.Single(_runner.Calls);
        Assert.Equal(_paths.Relinker, call.FileName);
        Assert.Equal(_game.Root, call.WorkingDirectory);
        Assert.Equal(["--windows", "--registry", Path.Combine("app0", "eboot.bin"), "PPSA99666.exe"], call.Arguments);
        Assert.True(step.IsAlreadyDone());
    }

    [Fact]
    public async Task Adds_the_intel_option_on_an_intel_cpu()
    {
        await CreateStep(toIntel: true).ExecuteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(["--windows", "--registry", "--to-intel", Path.Combine("app0", "eboot.bin"), "PPSA99666.exe"], _runner.Calls[0].Arguments);
    }

    [Fact]
    public async Task A_failure_quotes_the_relinker_errors_and_names_the_intel_option()
    {
        _runner.Respond = _ => new FakeProcessResult(2, StandardError: ["error: unsupported instruction vpdpbusd"]);

        var exception = await Assert.ThrowsAsync<GameImportException>(() => CreateStep(toIntel: true).ExecuteAsync(null, TestContext.Current.CancellationToken));

        Assert.Contains("exit code 2", exception.Message, StringComparison.Ordinal);
        Assert.Contains("--to-intel", exception.Message, StringComparison.Ordinal);
        Assert.Contains("unsupported instruction vpdpbusd", exception.Message, StringComparison.Ordinal);
    }

    public void Dispose() => _temp.Dispose();
}
```

`tests/PS5Kitsune.Core.Tests/Games/AuditStepTests.cs`:

```csharp
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Games.Steps;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class AuditStepTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FakeProcessRunner _runner = new();
    private readonly GameLayout _game;
    private readonly AnyPS5Paths _paths;
    private readonly PythonEmbeddableTool _python;
    private readonly GameImportContext _context;

    public AuditStepTests()
    {
        var platform = new FakePlatform();
        var layout = new DataLayout(_temp.Combine("root"));
        _game = new GameLayout(_temp.Combine("games", TestGames.TitleId), TestGames.TitleId, platform);
        _paths = new AnyPS5Paths(layout, platform);
        _python = new PythonEmbeddableTool(new ToolDefinition("3.14.8", new Uri("https://example.test/p.zip"), new string('a', 64)), layout, platform, new FakeDownloader(), _runner);
        _context = new GameImportContext(new GameSource(_temp.Combine("unused"), null), new GameMetadata(TestGames.TitleId, "DOOM"), ["sce_module"], _game, toIntel: false);

        TestFiles.Write(Path.Combine(_game.Root, "PPSA99666.registry.json"), "[]");
        TestFiles.Write(Path.Combine(_game.Root, "PPSA99666.libc.registry.json"), "[]");
        TestFiles.Write(Path.Combine(_game.Root, "PPSA99666.needed.json"), "[]");
    }

    private AuditStep CreateStep() => new(_context, _paths, _python, _runner);

    private void RespondWith(int exitCode, string auditJson) => _runner.Respond = spec =>
    {
        TestFiles.Write(Path.Combine(spec.WorkingDirectory!, "audit.json"), auditJson);
        return new FakeProcessResult(exitCode);
    };

    [Fact]
    public async Task Passes_every_registry_and_the_module_folders()
    {
        RespondWith(0, AuditFiles.Json(262, 0, 0));

        await CreateStep().ExecuteAsync(null, TestContext.Current.CancellationToken);

        var call = Assert.Single(_runner.Calls);
        Assert.Equal(_game.Root, call.WorkingDirectory);
        Assert.Equal(
            [_paths.ImportAudit, "PPSA99666.libc.registry.json", "PPSA99666.registry.json", "--libs", "libs", "--modules", Path.Combine("app0", "sce_module"), "--json", "audit.json"],
            call.Arguments.Skip(2));
        Assert.Equal(AuditVerdict.Green, _context.Audit!.Verdict);
        Assert.Equal(262, _context.Audit.Implemented);
    }

    [Fact]
    public async Task Exit_code_1_still_reads_the_audit()
    {
        RespondWith(1, AuditFiles.Json(250, 0, 3));

        await CreateStep().ExecuteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(AuditVerdict.Red, _context.Audit!.Verdict);
        Assert.Equal(3, _context.Audit.Absent);
    }

    [Fact]
    public async Task Exit_code_2_fails_with_the_script_errors()
    {
        _runner.Respond = _ => new FakeProcessResult(2, StandardError: ["FAIL: PPSA99666.registry.json: bad registry"]);

        var exception = await Assert.ThrowsAsync<GameImportException>(() => CreateStep().ExecuteAsync(null, TestContext.Current.CancellationToken));

        Assert.Contains("bad registry", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_a_registry_the_audit_does_not_run()
    {
        File.Delete(Path.Combine(_game.Root, "PPSA99666.registry.json"));
        File.Delete(Path.Combine(_game.Root, "PPSA99666.libc.registry.json"));

        await Assert.ThrowsAsync<GameImportException>(() => CreateStep().ExecuteAsync(null, TestContext.Current.CancellationToken));

        Assert.Empty(_runner.Calls);
    }

    [Fact]
    public void The_audit_always_runs_again()
    {
        Assert.False(CreateStep().IsAlreadyDone());
    }

    public void Dispose() => _temp.Dispose();
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*AuditStepTests"`
Expected: the build fails with CS0246, `The type or namespace name 'AuditSummary' could not be found` (and `RelinkStep`, `AuditStep`).

- [ ] **Step 3: Write the summary, the context property and the steps**

`src/PS5Kitsune.Core/Games/AuditSummary.cs`:

```csharp
using System.Text.Json;

namespace PS5Kitsune.Core.Games;

public enum AuditVerdict
{
    /// <summary>Nothing absent, no stub: the game can load.</summary>
    Green,

    /// <summary>Stubs only: the game loads but stops if it calls an unimplemented function.</summary>
    Orange,

    /// <summary>Absent imports or a missing library: not playable yet.</summary>
    Red,
}

/// <summary>Unique imports by class, from the audit.json written by import_audit.py --json.</summary>
public sealed record AuditSummary(int Implemented, int Stub, int Absent, IReadOnlyDictionary<string, int> MissingLibraries)
{
    public AuditVerdict Verdict =>
        Absent > 0 || MissingLibraries.Count > 0 ? AuditVerdict.Red
        : Stub > 0 ? AuditVerdict.Orange
        : AuditVerdict.Green;

    /// <exception cref="InvalidDataException">The file is not an import_audit.py result.</exception>
    public static AuditSummary Read(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            var counts = root.GetProperty("unique_by_class");
            var missing = root.TryGetProperty("missing_libraries", out var libraries)
                ? libraries.EnumerateObject().ToDictionary(library => library.Name, library => library.Value.GetInt32())
                : new Dictionary<string, int>();
            return new AuditSummary(
                counts.GetProperty("implemented").GetInt32(),
                counts.GetProperty("stub").GetInt32(),
                counts.GetProperty("absent").GetInt32(),
                missing);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"{path} is not an import_audit.py result: {exception.Message}", exception);
        }
    }
}
```

In `src/PS5Kitsune.Core/Games/GameImportContext.cs`, add after the `ToIntel` property:

```csharp

    /// <summary>Set by the audit step; the game record is written from it.</summary>
    public AuditSummary? Audit { get; set; }
```

`src/PS5Kitsune.Core/Games/Steps/RelinkStep.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Processes;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Games.Steps;

/// <summary>
/// Runs the relinker from the game folder: relinker --windows --registry [--to-intel] app0/eboot.bin &lt;titleId&gt;.exe.
/// Done when the executable and its registry exist.
/// </summary>
public sealed class RelinkStep(GameImportContext context, AnyPS5Paths paths, AnyPS5Profile profile, IProcessRunner processRunner) : IStep
{
    public string Name => "Convert with the relinker";

    public bool IsAlreadyDone() => File.Exists(context.Game.Executable) && File.Exists(context.Game.Registry);

    public async Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        List<string> arguments = [.. profile.Relinker.WindowsArgs];
        if (context.ToIntel && profile.Relinker.IntelArg is { } intelArg)
        {
            arguments.Add(intelArg);
        }

        arguments.Add(Path.Combine("app0", GameSourceResolver.ExecutableName));
        arguments.Add(context.Game.ExecutableName);

        var errors = new OutputTail();
        var spec = new ProcessSpec(paths.Relinker, arguments) { WorkingDirectory = context.Game.Root };
        var result = await processRunner.RunAsync(spec, errors.Collect, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            var intelNote = context.ToIntel
                ? $" It ran with {profile.Relinker.IntelArg}: the game may use an AMD-only instruction that cannot be converted for Intel CPUs."
                : "";
            throw new GameImportException($"The relinker failed with exit code {result.ExitCode}.{intelNote}{Environment.NewLine}{errors}");
        }
    }
}
```

`src/PS5Kitsune.Core/Games/Steps/AuditStep.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Processes;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.Games.Steps;

/// <summary>
/// Runs import_audit.py on every registry the relinker wrote, reads audit.json and stores the summary in the context.
/// </summary>
public sealed class AuditStep(GameImportContext context, AnyPS5Paths paths, PythonEmbeddableTool python, IProcessRunner processRunner) : IStep
{
    public string Name => "Audit the imports";

    // Always runs again: it is quick, and its result feeds the game record.
    public bool IsAlreadyDone() => false;

    public async Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        var game = context.Game;
        List<string> registries = [.. Directory.GetFiles(game.Root, "*.registry.json").Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)];
        if (registries.Count == 0)
        {
            throw new GameImportException($"No *.registry.json in {game.Root}: the relinker did not write its registry.");
        }

        List<string> arguments = [.. registries, "--libs", "libs"];
        foreach (var directory in context.ModuleDirectories)
        {
            arguments.Add("--modules");
            arguments.Add(Path.Combine("app0", directory));
        }

        arguments.Add("--json");
        arguments.Add(Path.GetFileName(game.AuditFile));
        File.Delete(game.AuditFile);

        var errors = new OutputTail();
        var spec = python.CreateScriptSpec(paths.ImportAudit, arguments) with { WorkingDirectory = game.Root };
        var result = await processRunner.RunAsync(spec, errors.Collect, cancellationToken).ConfigureAwait(false);

        // 0: nothing blocks; 1: absent imports or missing libraries (the verdict shows it); 2: unreadable input.
        if (result.ExitCode is not (0 or 1))
        {
            throw new GameImportException($"import_audit.py could not read its input (exit code {result.ExitCode}).{Environment.NewLine}{errors}");
        }

        context.Audit = AuditSummary.Read(game.AuditFile);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: every test passes, including `AuditSummaryTests` (5), `RelinkStepTests` (3) and `AuditStepTests` (5).

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Core/Games tests/PS5Kitsune.Core.Tests/Games
git commit -m "Convert a game with the relinker and audit its imports

The verdict is green, orange or red from import_audit.py's JSON output.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 12: Game record (`game.json`)

**Files:**
- Create: `src/PS5Kitsune.Core/Library/GameRecord.cs`
- Create: `src/PS5Kitsune.Core/Library/GameRecordStore.cs`
- Create: `src/PS5Kitsune.Core/Games/Steps/WriteGameRecordStep.cs`
- Create: `tests/PS5Kitsune.Core.Tests/Library/TestRecords.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Library/GameRecordStoreTests.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Games/WriteGameRecordStepTests.cs`

**Interfaces:**
- Consumes: `JsonFiles` (Task 4); `AuditVerdict`, `AuditSummary`, `GameImportContext.Audit` (Task 11); `ProfileSource.Version` (Task 2); `FixedTimeProvider` (Task 5).
- Produces: `GameRecord` with required `SchemaVersion`, `TitleId`, `Name`, `Executable`, `ConvertedWith`, `Audit`, `AddedAt`; optional `Icon` and `LastPlayedAt`; `Settings` defaulting to `new()`; and `const int CurrentSchemaVersion = 1`. Also `ConversionInfo(string Profile, string Mode, string Version, string? Commit, bool ToIntel)`, `AuditRecord(int Implemented, int Stub, int Absent, AuditVerdict Verdict)`, and `GameSettings` with `Gpu` (null), `Lavapipe` (false), `Language` (1) and `PresentMode` ("fifo").
- Produces: `InvalidGameRecordException(string message)`; `GameRecordStore.Read(string path) : GameRecord?` (null when missing; throws `InvalidGameRecordException`) and `GameRecordStore.WriteAsync(string path, GameRecord record, CancellationToken)`.
- Produces: `WriteGameRecordStep(GameImportContext, AnyPS5Profile, TimeProvider)`.
- Produces (tests): `TestRecords.Sample() : GameRecord`.

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.Core.Tests/Library/TestRecords.cs`:

```csharp
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Tests.Library;

internal static class TestRecords
{
    public static GameRecord Sample() => new()
    {
        SchemaVersion = GameRecord.CurrentSchemaVersion,
        TitleId = "PPSA99666",
        Name = "DOOM",
        Executable = "PPSA99666.exe",
        Icon = "app0/sce_sys/icon0.png",
        ConvertedWith = new ConversionInfo("doom-validated-2026-10", "release", "v0.1.2", null, false),
        Audit = new AuditRecord(262, 3, 0, AuditVerdict.Orange),
        AddedAt = new DateTimeOffset(2026, 10, 20, 15, 0, 0, TimeSpan.FromHours(2)),
    };
}
```

`tests/PS5Kitsune.Core.Tests/Library/GameRecordStoreTests.cs`:

```csharp
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Tests.Library;

public sealed class GameRecordStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    private string RecordFile => _temp.Combine(".ps5kitsune", "game.json");

    [Fact]
    public async Task A_record_round_trips_through_game_json()
    {
        var record = TestRecords.Sample();

        await GameRecordStore.WriteAsync(RecordFile, record, TestContext.Current.CancellationToken);

        Assert.Equal(record, GameRecordStore.Read(RecordFile));
        var json = File.ReadAllText(RecordFile);
        Assert.Contains("\"schemaVersion\": 1", json, StringComparison.Ordinal);
        Assert.Contains("\"verdict\": \"orange\"", json, StringComparison.Ordinal);
        Assert.Contains("\"presentMode\": \"fifo\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_record_reads_as_null()
    {
        Assert.Null(GameRecordStore.Read(RecordFile));
    }

    [Fact]
    public async Task Another_schema_version_is_refused()
    {
        await GameRecordStore.WriteAsync(RecordFile, TestRecords.Sample() with { SchemaVersion = 2 }, TestContext.Current.CancellationToken);

        var exception = Assert.Throws<InvalidGameRecordException>(() => GameRecordStore.Read(RecordFile));

        Assert.Contains("schema version 2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_malformed_record_is_refused()
    {
        TestFiles.Write(RecordFile, "{ \"titleId\": ");

        Assert.Throws<InvalidGameRecordException>(() => GameRecordStore.Read(RecordFile));
    }

    public void Dispose() => _temp.Dispose();
}
```

`tests/PS5Kitsune.Core.Tests/Games/WriteGameRecordStepTests.cs`:

```csharp
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Games.Steps;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class WriteGameRecordStepTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
    private readonly GameImportContext _context;

    public WriteGameRecordStepTests()
    {
        var game = new GameLayout(_temp.Combine("games", TestGames.TitleId), TestGames.TitleId, new FakePlatform());
        _context = new GameImportContext(new GameSource(_temp.Combine("unused"), null), new GameMetadata(TestGames.TitleId, "DOOM"), ["sce_module"], game, toIntel: true);
        TestFiles.Write(Path.Combine(game.App0, "sce_sys", "icon0.png"), "png");
    }

    private WriteGameRecordStep CreateStep() => new(_context, ProfileLoader.LoadEmbedded(), _time);

    [Fact]
    public async Task Records_the_game_with_default_settings()
    {
        _context.Audit = new AuditSummary(262, 0, 0, new Dictionary<string, int>());
        var step = CreateStep();
        Assert.False(step.IsAlreadyDone());

        await step.ExecuteAsync(null, TestContext.Current.CancellationToken);

        var record = GameRecordStore.Read(_context.Game.RecordFile)!;
        Assert.Equal("PPSA99666", record.TitleId);
        Assert.Equal("DOOM", record.Name);
        Assert.Equal("PPSA99666.exe", record.Executable);
        Assert.Equal("app0/sce_sys/icon0.png", record.Icon);
        Assert.Equal(new ConversionInfo("doom-validated-2026-10", "release", "v0.1.2", null, true), record.ConvertedWith);
        Assert.Equal(new AuditRecord(262, 0, 0, AuditVerdict.Green), record.Audit);
        Assert.Equal(new GameSettings(), record.Settings);
        Assert.Equal(_time.GetLocalNow(), record.AddedAt);
        Assert.Null(record.LastPlayedAt);
        Assert.True(step.IsAlreadyDone());
    }

    [Fact]
    public async Task The_audit_must_run_first()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateStep().ExecuteAsync(null, TestContext.Current.CancellationToken));
    }

    public void Dispose() => _temp.Dispose();
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*GameRecordStoreTests"`
Expected: the build fails with CS0234, `The type or namespace name 'Library' does not exist in the namespace 'PS5Kitsune.Core'`.

- [ ] **Step 3: Write the record, the store and the step**

`src/PS5Kitsune.Core/Library/GameRecord.cs`:

```csharp
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Core.Library;

/// <summary>Contents of &lt;game&gt;/.ps5kitsune/game.json. See docs/architecture.md, "Game record".</summary>
public sealed record GameRecord
{
    public const int CurrentSchemaVersion = 1;

    public required int SchemaVersion { get; init; }

    public required string TitleId { get; init; }

    public required string Name { get; init; }

    /// <summary>Converted executable, relative to the game folder.</summary>
    public required string Executable { get; init; }

    /// <summary>icon0.png relative to the game folder, '/' separators; null when the game has none.</summary>
    public string? Icon { get; init; }

    public required ConversionInfo ConvertedWith { get; init; }

    public required AuditRecord Audit { get; init; }

    public GameSettings Settings { get; init; } = new();

    public required DateTimeOffset AddedAt { get; init; }

    public DateTimeOffset? LastPlayedAt { get; init; }
}

/// <param name="Commit">Commit SHA in commit mode; null for a release.</param>
/// <param name="ToIntel">Whether the relinker ran with its Intel option; Reconvert reuses it.</param>
public sealed record ConversionInfo(string Profile, string Mode, string Version, string? Commit, bool ToIntel);

public sealed record AuditRecord(int Implemented, int Stub, int Absent, AuditVerdict Verdict);

/// <summary>Per-game settings (milestone 7 edits them); defaults until then.</summary>
public sealed record GameSettings
{
    public string? Gpu { get; init; }

    public bool Lavapipe { get; init; }

    public int Language { get; init; } = 1;

    public string PresentMode { get; init; } = "fifo";
}
```

`src/PS5Kitsune.Core/Library/GameRecordStore.cs`:

```csharp
using System.Text.Json;
using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Core.Library;

public sealed class InvalidGameRecordException(string message, Exception? innerException = null) : Exception(message, innerException);

public static class GameRecordStore
{
    /// <returns>Null when the file does not exist.</returns>
    /// <exception cref="InvalidGameRecordException">The file is malformed or has another schema version.</exception>
    public static GameRecord? Read(string path)
    {
        GameRecord? record;
        try
        {
            record = JsonFiles.Read<GameRecord>(path);
        }
        catch (JsonException exception)
        {
            throw new InvalidGameRecordException($"{path} is not a valid game record: {exception.Message}", exception);
        }

        if (record is not null && record.SchemaVersion != GameRecord.CurrentSchemaVersion)
        {
            throw new InvalidGameRecordException($"{path} has schema version {record.SchemaVersion}; this app reads version {GameRecord.CurrentSchemaVersion}.");
        }

        return record;
    }

    public static Task WriteAsync(string path, GameRecord record, CancellationToken cancellationToken) =>
        JsonFiles.WriteAsync(path, record, cancellationToken);
}
```

`src/PS5Kitsune.Core/Games/Steps/WriteGameRecordStep.cs`:

```csharp
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Games.Steps;

/// <summary>Writes game.json last: its presence means the game is added.</summary>
public sealed class WriteGameRecordStep(GameImportContext context, AnyPS5Profile profile, TimeProvider time) : IStep
{
    private const string IconPath = "app0/sce_sys/icon0.png";

    public string Name => "Record the game";

    public bool IsAlreadyDone() => File.Exists(context.Game.RecordFile);

    public Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        var audit = context.Audit ?? throw new InvalidOperationException("The audit must run before the game is recorded.");
        var record = new GameRecord
        {
            SchemaVersion = GameRecord.CurrentSchemaVersion,
            TitleId = context.Metadata.TitleId,
            Name = context.Metadata.Name,
            Executable = context.Game.ExecutableName,
            Icon = File.Exists(Path.Combine(context.Game.Root, IconPath)) ? IconPath : null,
            ConvertedWith = new ConversionInfo(
                profile.Id,
                profile.Source.Kind,
                profile.Source.Version,
                profile.Source.Kind == "commit" ? profile.Source.Sha : null,
                context.ToIntel),
            Audit = new AuditRecord(audit.Implemented, audit.Stub, audit.Absent, audit.Verdict),
            AddedAt = time.GetLocalNow(),
        };
        return GameRecordStore.WriteAsync(context.Game.RecordFile, record, cancellationToken);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*GameRecordStoreTests"` then `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*WriteGameRecordStepTests"`
Expected: 4 tests pass, then 2 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Core tests/PS5Kitsune.Core.Tests
git commit -m "Record an added game in game.json

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 13: Game importer and `ps5kitsune add`

**Files:**
- Create: `src/PS5Kitsune.Core/Games/AddGameResult.cs`
- Create: `src/PS5Kitsune.Core/Games/GameImporter.cs`
- Modify: `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`
- Create: `src/PS5Kitsune.Cli/AddCommand.cs`
- Modify: `src/PS5Kitsune.Cli/Program.cs`
- Modify: `src/PS5Kitsune.Cli/CliLog.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Games/GameImporterTests.cs`

**Interfaces:**
- Consumes: every add step (Tasks 9 to 12), `GameSourceResolver` (7), `GameValidator` (8), `AnyPS5Installer.IsInstalled`, `AnyPS5NotInstalledException` (5), `StepRunner` (3), `AnyPS5TestSetup`, `TestGames`, `AuditFiles` (tests), and `CpuVendor` (9).
- Produces: `abstract record AddGameResult`, with the cases `GameAdded(GameRecord Record, string GameDirectory, IReadOnlyList<ValidationIssue> Warnings)`, `GameRejected(IReadOnlyList<ValidationIssue> Issues)` and `GameAlreadyAdded(string TitleId, string GameDirectory)`.
- Produces: `GameImporter(DataLayout, IPlatform, AnyPS5Profile, AnyPS5Paths, AnyPS5Installer, GameSourceResolver, GameValidator, PythonEmbeddableTool, IProcessRunner, StepRunner, TimeProvider)` with `Task<AddGameResult> AddAsync(string input, bool toIntel, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default)`. It throws `AnyPS5NotInstalledException`, `InvalidGameSourceException` and `StepFailedException`.

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.Core.Tests/Games/GameImporterTests.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Tests.AnyPS5;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class GameImporterTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly AnyPS5TestSetup _setup;
    private readonly GameImporter _importer;
    private int _relinkerExitCode;

    public GameImporterTests()
    {
        _setup = new AnyPS5TestSetup(_temp.Combine("root"));
        _setup.Runner.Respond = Respond;
        _importer = new GameImporter(
            _setup.Layout, _setup.Platform, _setup.Profile, _setup.Paths, _setup.Installer,
            new GameSourceResolver(_setup.Layout), new GameValidator(_ => long.MaxValue),
            _setup.PythonTool, _setup.Runner, _setup.StepRunner, _setup.Time);
    }

    private string GameDirectory => _setup.Layout.GameDirectory(TestGames.TitleId);

    private string Staging => Path.Combine(_setup.Layout.Staging, TestGames.TitleId);

    private Task<AddGameResult> AddAsync(string input) =>
        _importer.AddAsync(input, toIntel: false, cancellationToken: TestContext.Current.CancellationToken);

    private string WriteZip()
    {
        var zip = _temp.Combine("PPSA99666.zip");
        File.WriteAllBytes(zip, TestGames.Zip("PPSA99666/"));
        return zip;
    }

    // Plays unwrap_self.py, the relinker and import_audit.py.
    private FakeProcessResult Respond(ProcessSpec spec)
    {
        switch (FakeProcessRunner.ScriptName(spec))
        {
            case "unwrap_self.py":
                TestFiles.Write(spec.Arguments[^1], TestFiles.Elf());
                return new FakeProcessResult(0);
            case "import_audit.py":
                TestFiles.Write(Path.Combine(spec.WorkingDirectory!, "audit.json"), AuditFiles.Json(262, 0, 0));
                return new FakeProcessResult(0);
        }

        if (spec.FileName != _setup.Paths.Relinker)
        {
            throw new InvalidOperationException($"Unexpected command {spec.FileName}");
        }

        if (_relinkerExitCode != 0)
        {
            return new FakeProcessResult(_relinkerExitCode, StandardError: ["relinker: failure"]);
        }

        TestFiles.Write(Path.Combine(spec.WorkingDirectory!, "PPSA99666.exe"), "exe");
        TestFiles.Write(Path.Combine(spec.WorkingDirectory!, "PPSA99666.registry.json"), "[]");
        return new FakeProcessResult(0);
    }

    [Fact]
    public async Task Adding_requires_the_anyps5_install()
    {
        await Assert.ThrowsAsync<AnyPS5NotInstalledException>(() => AddAsync(WriteZip()));
    }

    [Fact]
    public async Task Adds_a_zip_end_to_end()
    {
        await _setup.InstallAsync();

        var added = Assert.IsType<GameAdded>(await AddAsync(WriteZip()));

        Assert.Equal("DOOM", added.Record.Name);
        Assert.Equal(AuditVerdict.Green, added.Record.Audit.Verdict);
        Assert.Equal(GameDirectory, added.GameDirectory);
        Assert.Equal(TestFiles.Elf(), File.ReadAllBytes(Path.Combine(GameDirectory, "app0", "eboot.bin")));
        Assert.Equal(TestFiles.Self(), File.ReadAllBytes(Path.Combine(GameDirectory, ".ps5kitsune", "originals", "eboot.bin")));
        Assert.True(File.Exists(Path.Combine(GameDirectory, "libs", "libSceAudioOut.prx")));
        Assert.True(File.Exists(Path.Combine(GameDirectory, ".ps5kitsune", "game.json")));
        Assert.False(Directory.Exists(Staging));
    }

    [Fact]
    public async Task A_game_already_added_is_refused()
    {
        await _setup.InstallAsync();
        await AddAsync(WriteZip());

        var already = Assert.IsType<GameAlreadyAdded>(await AddAsync(WriteZip()));

        Assert.Equal(TestGames.TitleId, already.TitleId);
        Assert.Equal(GameDirectory, already.GameDirectory);
        Assert.False(Directory.Exists(Staging));
    }

    [Fact]
    public async Task An_invalid_game_is_rejected_before_anything_is_copied()
    {
        await _setup.InstallAsync();
        TestFiles.Write(_temp.Combine("broken", "eboot.bin"), TestFiles.Self());

        var rejected = Assert.IsType<GameRejected>(await AddAsync(_temp.Combine("broken")));

        Assert.Contains(rejected.Issues, issue => issue.Severity == IssueSeverity.Blocking && issue.Message.Contains("param.json", StringComparison.Ordinal));
        Assert.False(Directory.Exists(_setup.Layout.Games));
    }

    [Fact]
    public async Task A_failed_add_resumes_without_copying_or_unwrapping_again()
    {
        await _setup.InstallAsync();
        _relinkerExitCode = 2;

        var failure = await Assert.ThrowsAsync<StepFailedException>(() => AddAsync(WriteZip()));

        Assert.Equal("Convert with the relinker", failure.StepName);
        Assert.False(File.Exists(Path.Combine(GameDirectory, ".ps5kitsune", "game.json")));
        Assert.False(Directory.Exists(Staging));

        _relinkerExitCode = 0;
        var unwrapsBefore = _setup.Runner.Calls.Count(spec => FakeProcessRunner.ScriptName(spec) == "unwrap_self.py");

        Assert.IsType<GameAdded>(await AddAsync(WriteZip()));

        Assert.Equal(unwrapsBefore, _setup.Runner.Calls.Count(spec => FakeProcessRunner.ScriptName(spec) == "unwrap_self.py"));
    }

    public void Dispose() => _temp.Dispose();
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*GameImporterTests"`
Expected: the build fails with CS0246, `The type or namespace name 'GameImporter' could not be found`.

- [ ] **Step 3: Write the importer**

`src/PS5Kitsune.Core/Games/AddGameResult.cs`:

```csharp
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Games;

public abstract record AddGameResult;

/// <param name="Warnings">Non-blocking validation issues, to show the user.</param>
public sealed record GameAdded(GameRecord Record, string GameDirectory, IReadOnlyList<ValidationIssue> Warnings) : AddGameResult;

/// <summary>Validation found blocking issues; nothing was copied.</summary>
public sealed record GameRejected(IReadOnlyList<ValidationIssue> Issues) : AddGameResult;

/// <summary>game.json already exists; delete the game folder to add the game again.</summary>
public sealed record GameAlreadyAdded(string TitleId, string GameDirectory) : AddGameResult;
```

`src/PS5Kitsune.Core/Games/GameImporter.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Games.Steps;
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.Games;

/// <summary>
/// Adds a game: resolve the folder or zip, validate it, then run the add steps. Without game.json,
/// a new add resumes at the first step not done. See docs/architecture.md, "V2 — Add a game".
/// </summary>
public sealed class GameImporter(
    DataLayout layout,
    IPlatform platform,
    AnyPS5Profile profile,
    AnyPS5Paths paths,
    AnyPS5Installer installer,
    GameSourceResolver resolver,
    GameValidator validator,
    PythonEmbeddableTool python,
    IProcessRunner processRunner,
    StepRunner stepRunner,
    TimeProvider time)
{
    /// <param name="toIntel">Convert for an Intel CPU (relinker Intel option).</param>
    /// <exception cref="AnyPS5NotInstalledException">Run install first.</exception>
    /// <exception cref="InvalidGameSourceException">The input is not a game folder or zip.</exception>
    /// <exception cref="StepFailedException">A step failed; running add again resumes.</exception>
    public async Task<AddGameResult> AddAsync(
        string input, bool toIntel, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default)
    {
        if (!installer.IsInstalled())
        {
            throw new AnyPS5NotInstalledException();
        }

        var source = await resolver.ResolveAsync(input, cancellationToken).ConfigureAwait(false);
        CopyGameStep? copy = null;
        try
        {
            var validation = validator.Validate(source.Root, layout.Games);
            if (validation.IsBlocked || validation.Metadata is not { } metadata)
            {
                return new GameRejected(validation.Issues);
            }

            var game = new GameLayout(layout.GameDirectory(metadata.TitleId), metadata.TitleId, platform);
            if (File.Exists(game.RecordFile))
            {
                return new GameAlreadyAdded(metadata.TitleId, game.Root);
            }

            var context = new GameImportContext(source, metadata, validation.ModuleDirectories, game, toIntel);
            copy = new CopyGameStep(context);
            IStep[] steps =
            [
                copy,
                new CopyLibsStep(paths, game),
                new UnwrapSelfStep(context, paths, python, processRunner),
                new RelinkStep(context, paths, profile, processRunner),
                new AuditStep(context, paths, python, processRunner),
                new WriteGameRecordStep(context, profile, time),
            ];
            await stepRunner.RunAsync(steps, progress, onEvent, cancellationToken).ConfigureAwait(false);

            var record = GameRecordStore.Read(game.RecordFile) ?? throw new InvalidOperationException($"{game.RecordFile} was not written.");
            return new GameAdded(record, game.Root, validation.Warnings);
        }
        finally
        {
            // An extracted zip is only needed until the game is copied; keep it when the copy did not complete.
            if (source.StagingDirectory is { } staging && Directory.Exists(staging) && (copy is null || copy.IsAlreadyDone()))
            {
                Directory.Delete(staging, recursive: true);
            }
        }
    }
}
```

In `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`, add `using PS5Kitsune.Core.Games;` to the usings, and after `services.AddSingleton<AnyPS5Installer>();` add:

```csharp
        services.AddSingleton<GameSourceResolver>();
        services.AddSingleton(_ => new GameValidator());
        services.AddSingleton<GameImporter>();
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*GameImporterTests"`
Expected: 5 tests, all pass.

- [ ] **Step 5: Add the `add` command**

`src/PS5Kitsune.Cli/AddCommand.cs`:

```csharp
using System.CommandLine;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PS5Kitsune.Core.Checks;
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Cli;

internal static class AddCommand
{
    public static Command Create(Option<DirectoryInfo?> rootOption)
    {
        var pathArgument = new Argument<string>("path") { Description = "Game folder or .zip, in the DOOM for PS5 format." };
        var command = new Command("add", "Validate a game, copy it into the games folder, convert it and audit its imports.") { pathArgument };
        command.SetAction((parseResult, cancellationToken) => CommandHost.RunAsync(parseResult.GetValue(rootOption), "add", async (services, logger) =>
        {
            var vendor = CpuVendor.Detect();
            var toIntel = CpuVendor.IsIntel(vendor);
            logger.CpuDetected(vendor, toIntel ? "yes" : "no");

            var importer = services.GetRequiredService<GameImporter>();
            var result = await importer.AddAsync(parseResult.GetRequiredValue(pathArgument), toIntel, new ConsoleStepProgress(), cancellationToken: cancellationToken);
            switch (result)
            {
                case GameRejected rejected:
                    Report(logger, rejected.Issues);
                    return 1;
                case GameAlreadyAdded already:
                    logger.AlreadyAdded(already.TitleId, already.GameDirectory);
                    return 1;
                case GameAdded added:
                    Report(logger, added.Warnings);
                    var record = added.Record;
                    logger.GameAdded(record.Name, record.TitleId, added.GameDirectory);
                    logger.AuditCounts(record.Audit.Implemented, record.Audit.Stub, record.Audit.Absent);
                    switch (record.Audit.Verdict)
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

                    return 0;
                default:
                    throw new UnreachableException();
            }
        }));
        return command;
    }

    private static void Report(ILogger logger, IReadOnlyList<ValidationIssue> issues)
    {
        foreach (var issue in issues)
        {
            if (issue.Severity == IssueSeverity.Blocking)
            {
                logger.BlockingIssue(issue.Message);
            }
            else
            {
                logger.WarningIssue(issue.Message);
            }
        }
    }
}
```

In `src/PS5Kitsune.Cli/CliLog.cs`, add before the closing brace:

```csharp

    [LoggerMessage(Level = LogLevel.Information, Message = "CPU: {Vendor}. Intel conversion (--to-intel): {ToIntel}.")]
    public static partial void CpuDetected(this ILogger logger, string vendor, string toIntel);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Message}")]
    public static partial void BlockingIssue(this ILogger logger, string message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Message}")]
    public static partial void WarningIssue(this ILogger logger, string message);

    [LoggerMessage(Level = LogLevel.Error, Message = "{TitleId} is already added in {Directory}. Delete that folder to add the game again.")]
    public static partial void AlreadyAdded(this ILogger logger, string titleId, string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Name} ({TitleId}) is added in {Directory}.")]
    public static partial void GameAdded(this ILogger logger, string name, string titleId, string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "Audit: {Implemented} implemented, {Stub} stubs, {Absent} absent.")]
    public static partial void AuditCounts(this ILogger logger, int implemented, int stub, int absent);

    [LoggerMessage(Level = LogLevel.Information, Message = "Verdict: green. The game can load.")]
    public static partial void VerdictGreen(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Verdict: orange. The game loads but will stop if it calls an unimplemented function.")]
    public static partial void VerdictOrange(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Verdict: red. Not playable yet: wait for an AnyPS5 update. The game stays added and will need a reconversion.")]
    public static partial void VerdictRed(this ILogger logger);
```

In `src/PS5Kitsune.Cli/Program.cs`, add `AddCommand.Create(rootOption),` right after `InstallCommand.Create(rootOption),`.

- [ ] **Step 6: Build and check the error path**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: the build succeeds with no warning.

Run: `dotnet run --project src/PS5Kitsune.Cli -c Release -- add /c/Windows`
Expected: a CPU line, then `No eboot.bin found in C:/Windows, in its app0 folder or in its single subfolder.` and `Full log: …`; exit code 1.

- [ ] **Step 7: Commit**

```bash
git add src/PS5Kitsune.Core src/PS5Kitsune.Cli tests/PS5Kitsune.Core.Tests
git commit -m "Add a game from the command line

GameImporter resolves, validates, then copies, unwraps, converts, audits
and records the game; a failed add resumes where it stopped.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 14: Launch a game and follow its log

**Files:**
- Create: `src/PS5Kitsune.Core/Abstractions/IGameLauncher.cs`
- Create: `src/PS5Kitsune.Core/Library/LogTail.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Library/LogTailTests.cs`
- Modify: `src/PS5Kitsune.Platform.Windows/PS5Kitsune.Platform.Windows.csproj`
- Create: `src/PS5Kitsune.Platform.Windows/WindowsCommandLine.cs`
- Create: `src/PS5Kitsune.Platform.Windows/WindowsGameLauncher.cs`
- Modify: `src/PS5Kitsune.Platform.Windows/PlatformServices.cs`
- Create: `tests/PS5Kitsune.Platform.Windows.Tests/PS5Kitsune.Platform.Windows.Tests.csproj`
- Test: `tests/PS5Kitsune.Platform.Windows.Tests/WindowsCommandLineTests.cs`
- Test: `tests/PS5Kitsune.Platform.Windows.Tests/WindowsGameLauncherTests.cs`
- Modify: `PS5Kitsune.slnx`
- Modify: `README.md` (project table)

**Interfaces:**
- Produces: `GameLaunchRequest(string Executable, string WorkingDirectory, string LogDirectory)`, with `IReadOnlyList<string> Arguments { get; init; }` defaulting to empty.
- Produces: `ILaunchedGame : IDisposable` with `int ProcessId`, `string LogFile` and `Task<int> WaitForExitAsync(CancellationToken cancellationToken = default)`; `IGameLauncher` with `ILaunchedGame Launch(GameLaunchRequest request)`.
- Produces: `LogTail.FollowAsync(string path, Action<string> onLine, Task stop, CancellationToken cancellationToken = default) : Task`.
- Produces: `WindowsGameLauncher(ILogger<WindowsGameLauncher>) : IGameLauncher`, which writes logs named `yyyyMMdd-HHmmss.log` and keeps the 10 newest; `WindowsCommandLine.Build(string executable, IReadOnlyList<string> arguments) : string` (internal). `AddPlatform` registers `IGameLauncher` on Windows.

- [ ] **Step 1: Write the failing `LogTail` tests**

`tests/PS5Kitsune.Core.Tests/Library/LogTailTests.cs`:

```csharp
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Core.Tests.Library;

public sealed class LogTailTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public async Task Follows_the_lines_written_until_the_process_exits()
    {
        var path = _temp.Combine("game.log");
        await using var writer = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
        var lines = new List<string>();
        var firstLine = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource();

        var follow = LogTail.FollowAsync(path, line => { lines.Add(line); firstLine.TrySetResult(); }, exited.Task, TestContext.Current.CancellationToken);
        await writer.WriteAsync("one\n");
        await firstLine.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await writer.WriteAsync("two\r\npar");
        await writer.WriteAsync("tial");
        exited.SetResult();
        await follow.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(["one", "two", "partial"], lines);
    }

    [Fact]
    public async Task Cancelling_stops_following()
    {
        var path = _temp.Combine("game.log");
        TestFiles.Write(path, "");
        using var cancellation = new CancellationTokenSource();

        var follow = LogTail.FollowAsync(path, _ => { }, new TaskCompletionSource().Task, cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => follow);
    }

    public void Dispose() => _temp.Dispose();
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*LogTailTests"`
Expected: the build fails with CS0103, `The name 'LogTail' does not exist in the current context`.

- [ ] **Step 3: Write the launcher abstraction and `LogTail`**

`src/PS5Kitsune.Core/Abstractions/IGameLauncher.cs`:

```csharp
namespace PS5Kitsune.Core.Abstractions;

/// <param name="Executable">Program to start.</param>
/// <param name="WorkingDirectory">The game folder: the game resolves its files and creates data\ there.</param>
/// <param name="LogDirectory">Folder of the launch logs; only the newest are kept.</param>
public sealed record GameLaunchRequest(string Executable, string WorkingDirectory, string LogDirectory)
{
    public IReadOnlyList<string> Arguments { get; init; } = [];
}

/// <summary>A started game. Disposing releases the launcher's handle; it never stops the game.</summary>
public interface ILaunchedGame : IDisposable
{
    int ProcessId { get; }

    /// <summary>File receiving the game's stdout and stderr.</summary>
    string LogFile { get; }

    /// <summary>Completes with the exit code when the game exits.</summary>
    Task<int> WaitForExitAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Starts a game detached from the launcher, its output written straight to a log file.
/// See docs/architecture.md, "Launching".
/// </summary>
public interface IGameLauncher
{
    ILaunchedGame Launch(GameLaunchRequest request);
}
```

`src/PS5Kitsune.Core/Library/LogTail.cs`:

```csharp
using System.Text;

namespace PS5Kitsune.Core.Library;

/// <summary>Follows a log file another process writes to, line by line.</summary>
public static class LogTail
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Calls <paramref name="onLine"/> for each line written to <paramref name="path"/> until <paramref name="stop"/>
    /// completes, then for the rest of the file (a last line without a newline included).
    /// </summary>
    public static async Task FollowAsync(string path, Action<string> onLine, Task stop, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onLine);
        ArgumentNullException.ThrowIfNull(stop);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var pending = new StringBuilder();
        var buffer = new char[4096];
        while (true)
        {
            var finished = stop.IsCompleted;
            int read;
            while ((read = await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                Emit(read);
            }

            if (finished)
            {
                break;
            }

            await Task.WhenAny(stop, Task.Delay(PollInterval, cancellationToken)).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (pending.Length > 0)
        {
            onLine(pending.ToString());
        }

        void Emit(int count)
        {
            for (var index = 0; index < count; index++)
            {
                if (buffer[index] == '\n')
                {
                    onLine(pending.ToString().TrimEnd('\r'));
                    pending.Clear();
                }
                else
                {
                    pending.Append(buffer[index]);
                }
            }
        }
    }
}
```

- [ ] **Step 4: Run the `LogTail` tests to verify they pass**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*LogTailTests"`
Expected: 2 tests, both pass.

- [ ] **Step 5: Create the Windows test project with failing tests**

`tests/PS5Kitsune.Platform.Windows.Tests/PS5Kitsune.Platform.Windows.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
    <IsPublishable>false</IsPublishable>
    <!-- Test code: allow underscores in test names. -->
    <NoWarn>$(NoWarn);CA1707</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit.v3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\PS5Kitsune.Platform.Windows\PS5Kitsune.Platform.Windows.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Compile Include="..\PS5Kitsune.Core.Tests\TempDirectory.cs" Link="TempDirectory.cs" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

</Project>
```

In `PS5Kitsune.slnx`, inside `<Folder Name="/tests/">`, add:

```xml
    <Project Path="tests/PS5Kitsune.Platform.Windows.Tests/PS5Kitsune.Platform.Windows.Tests.csproj" />
```

`tests/PS5Kitsune.Platform.Windows.Tests/WindowsCommandLineTests.cs`:

```csharp
namespace PS5Kitsune.Platform.Windows.Tests;

public sealed class WindowsCommandLineTests
{
    [Fact]
    public void Quotes_the_executable_and_leaves_simple_arguments_bare()
    {
        Assert.Equal("\"C:\\Program Files\\cmd.exe\" /c exit 7", WindowsCommandLine.Build(@"C:\Program Files\cmd.exe", ["/c", "exit", "7"]));
    }

    [Fact]
    public void Quotes_and_escapes_the_arguments_that_need_it()
    {
        var commandLine = WindowsCommandLine.Build("x.exe", ["a b", "say \"hi\"", @"dir\", @"C:\a b\", ""]);

        Assert.Equal("\"x.exe\" \"a b\" \"say \\\"hi\\\"\" dir\\ \"C:\\a b\\\\\" \"\"", commandLine);
    }
}
```

`tests/PS5Kitsune.Platform.Windows.Tests/WindowsGameLauncherTests.cs`:

```csharp
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Tests;

namespace PS5Kitsune.Platform.Windows.Tests;

[SupportedOSPlatform("windows")]
public sealed class WindowsGameLauncherTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly WindowsGameLauncher _launcher = new(NullLogger<WindowsGameLauncher>.Instance);

    private string Logs => _temp.Combine("logs");

    private async Task<(int ExitCode, string Log, string LogFile)> RunCmdAsync(params string[] arguments)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var game = _launcher.Launch(new GameLaunchRequest(Path.Combine(Environment.SystemDirectory, "cmd.exe"), _temp.Path, Logs) { Arguments = arguments });
        var exitCode = await game.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        return (exitCode, await File.ReadAllTextAsync(game.LogFile, cancellationToken), game.LogFile);
    }

    [Fact]
    public async Task Writes_the_output_to_a_log_file()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");

        var (exitCode, log, logFile) = await RunCmdAsync("/c", "echo", "hello");

        Assert.Equal(0, exitCode);
        Assert.Contains("hello", log, StringComparison.Ordinal);
        Assert.Equal(Logs, Path.GetDirectoryName(logFile));
    }

    [Fact]
    public async Task Returns_the_exit_code()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");

        var (exitCode, _, _) = await RunCmdAsync("/c", "exit", "7");

        Assert.Equal(7, exitCode);
    }

    [Fact]
    public async Task Runs_in_the_working_directory()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");

        var (_, log, _) = await RunCmdAsync("/c", "cd");

        Assert.Contains(_temp.Path, log, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Keeps_the_ten_newest_logs()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows only.");
        for (var second = 0; second < 12; second++)
        {
            File.WriteAllText(Path.Combine(Directory.CreateDirectory(Logs).FullName, $"20000101-0000{second:00}.log"), "old");
        }

        await RunCmdAsync("/c", "exit", "0");

        var logs = Directory.GetFiles(Logs, "*.log");
        Assert.Equal(10, logs.Length);
        Assert.DoesNotContain(Path.Combine(Logs, "20000101-000000.log"), logs);
        Assert.Contains(Path.Combine(Logs, "20000101-000011.log"), logs);
    }

    public void Dispose() => _temp.Dispose();
}
```

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: the build fails with CS0103/CS0246, `WindowsCommandLine` and `WindowsGameLauncher` not found.

- [ ] **Step 6: Write the Windows launcher**

In `src/PS5Kitsune.Platform.Windows/PS5Kitsune.Platform.Windows.csproj`, add before `</Project>`:

```xml
  <PropertyGroup>
    <!-- Required by the LibraryImport source generator (P/Invoke for the game launcher). -->
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="PS5Kitsune.Platform.Windows.Tests" />
  </ItemGroup>
```

`src/PS5Kitsune.Platform.Windows/WindowsCommandLine.cs`:

```csharp
using System.Text;

namespace PS5Kitsune.Platform.Windows;

/// <summary>Builds a CreateProcess command line that the C runtime splits back into the same arguments.</summary>
internal static class WindowsCommandLine
{
    public static string Build(string executable, IReadOnlyList<string> arguments)
    {
        var builder = new StringBuilder().Append('"').Append(executable).Append('"');
        foreach (var argument in arguments)
        {
            builder.Append(' ');
            AppendArgument(builder, argument);
        }

        return builder.ToString();
    }

    // Backslashes are literal except before a quote: 2n backslashes + quote → n backslashes, end of quoting;
    // 2n+1 backslashes + quote → n backslashes and a literal quote.
    private static void AppendArgument(StringBuilder builder, string argument)
    {
        if (argument.Length > 0 && argument.IndexOfAny([' ', '\t', '"']) < 0)
        {
            builder.Append(argument);
            return;
        }

        builder.Append('"');
        var backslashes = 0;
        foreach (var character in argument)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }

            if (character == '"')
            {
                builder.Append('\\', (backslashes * 2) + 1).Append('"');
            }
            else
            {
                builder.Append('\\', backslashes).Append(character);
            }

            backslashes = 0;
        }

        builder.Append('\\', backslashes * 2).Append('"');
    }
}
```

`src/PS5Kitsune.Platform.Windows/WindowsGameLauncher.cs`:

```csharp
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Platform.Windows;

/// <summary>
/// Starts the game with CreateProcess, its stdout and stderr being an inherited handle to the log file:
/// no pipe slows a verbose game down, and closing the launcher neither blocks nor kills it (no job object).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsGameLauncher(ILogger<WindowsGameLauncher> logger) : IGameLauncher
{
    private const int KeptLogs = 10;
    private const uint CreateNoWindow = 0x08000000;
    private const int StartfUseStdHandles = 0x00000100;
    private const uint HandleFlagInherit = 0x00000001;

    public ILaunchedGame Launch(GameLaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Directory.CreateDirectory(request.LogDirectory);
        DeleteOldLogs(request.LogDirectory);
        var logFile = Path.Combine(request.LogDirectory, $"{DateTime.Now:yyyyMMdd-HHmmss}.log");

        using var log = File.OpenHandle(logFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        if (!SetHandleInformation(log, HandleFlagInherit, HandleFlagInherit))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        var startupInfo = new StartupInfo
        {
            Size = Marshal.SizeOf<StartupInfo>(),
            Flags = StartfUseStdHandles,
            StdOutput = log.DangerousGetHandle(),
            StdError = log.DangerousGetHandle(),
        };
        var commandLine = (WindowsCommandLine.Build(request.Executable, request.Arguments) + '\0').ToCharArray();
        if (!CreateProcess(request.Executable, ref commandLine[0], 0, 0, inheritHandles: true, CreateNoWindow, 0, request.WorkingDirectory, ref startupInfo, out var process))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), $"Could not start {request.Executable}.");
        }

        CloseHandle(process.Thread);
        LogLaunched(request.Executable, process.ProcessId, logFile);
        return new LaunchedGame(new SafeProcessHandle(process.Process, ownsHandle: true), process.ProcessId, logFile);
    }

    // Makes room for the new log by keeping the newest KeptLogs - 1. Names sort by date.
    private static void DeleteOldLogs(string directory)
    {
        foreach (var old in Directory.GetFiles(directory, "*.log").OrderDescending(StringComparer.Ordinal).Skip(KeptLogs - 1))
        {
            File.Delete(old);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Started {Executable} (PID {ProcessId}), output in {LogFile}")]
    private partial void LogLaunched(string executable, int processId, string logFile);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateProcessW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CreateProcess(
        string applicationName,
        ref char commandLine,
        nint processAttributes,
        nint threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint creationFlags,
        nint environment,
        string currentDirectory,
        ref StartupInfo startupInfo,
        out ProcessInformation processInformation);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetHandleInformation(SafeFileHandle handle, uint mask, uint flags);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetExitCodeProcess(SafeProcessHandle process, out uint exitCode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);

#pragma warning disable CS0649 // Native layouts: Windows fills or reads fields the C# code never touches.
    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        public int Size;
        public nint Reserved;
        public nint Desktop;
        public nint Title;
        public int X;
        public int Y;
        public int XSize;
        public int YSize;
        public int XCountChars;
        public int YCountChars;
        public int FillAttribute;
        public int Flags;
        public short ShowWindow;
        public short Reserved2Size;
        public nint Reserved2;
        public nint StdInput;
        public nint StdOutput;
        public nint StdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public nint Process;
        public nint Thread;
        public int ProcessId;
        public int ThreadId;
    }
#pragma warning restore CS0649

    private sealed class LaunchedGame(SafeProcessHandle process, int processId, string logFile) : ILaunchedGame
    {
        public int ProcessId { get; } = processId;

        public string LogFile { get; } = logFile;

        public async Task<int> WaitForExitAsync(CancellationToken cancellationToken = default)
        {
            using var waitHandle = new ProcessWaitHandle(process);
            var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var registration = ThreadPool.RegisterWaitForSingleObject(waitHandle, (_, _) => exited.TrySetResult(), null, Timeout.Infinite, executeOnlyOnce: true);
            try
            {
                await exited.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                registration.Unregister(null);
            }

            if (!GetExitCodeProcess(process, out var exitCode))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            return unchecked((int)exitCode);
        }

        public void Dispose() => process.Dispose();
    }

    // Lets the thread pool wait on the process handle without taking ownership of it.
    private sealed class ProcessWaitHandle : WaitHandle
    {
        public ProcessWaitHandle(SafeProcessHandle process)
        {
            SafeWaitHandle = new SafeWaitHandle(process.DangerousGetHandle(), ownsHandle: false);
        }
    }
}
```

In `src/PS5Kitsune.Platform.Windows/PlatformServices.cs`, replace the `AddPlatform` method with:

```csharp
    public static IServiceCollection AddPlatform(this IServiceCollection services, IPlatform platform)
    {
        services.AddSingleton(platform);
        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<IGameLauncher, WindowsGameLauncher>();
        }

        return services;
    }
```

If the build reports CS0169 for the struct fields as well, add it to the same `#pragma warning disable`/`restore` lines: `CS0649, CS0169`. If it reports IDE0051 or IDE0044 there, do the same with those IDs; the struct fields must stay exactly as declared because they define the native layout.

- [ ] **Step 7: Run every test**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: every test passes, including `WindowsCommandLineTests` (2) and `WindowsGameLauncherTests` (4) on Windows.

- [ ] **Step 8: List the new test project in the README**

In `README.md`, in the project table, add after the `tests/PS5Kitsune.Core.Tests` row:

```markdown
| `tests/PS5Kitsune.Platform.Windows.Tests` | Windows tests (skipped on Linux) |
```

- [ ] **Step 9: Commit**

```bash
git add src/PS5Kitsune.Core src/PS5Kitsune.Platform.Windows tests PS5Kitsune.slnx README.md
git commit -m "Launch a game detached, its output written straight to a log

WindowsGameLauncher uses CreateProcess with the log file as inherited
stdout and stderr, keeps the 10 newest logs, and LogTail follows the file.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 15: Game library and `ps5kitsune run`

**Files:**
- Create: `src/PS5Kitsune.Core/Library/GameLibrary.cs`
- Modify: `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Library/GameLibraryTests.cs`
- Create: `src/PS5Kitsune.Cli/RunCommand.cs`
- Modify: `src/PS5Kitsune.Cli/Program.cs`
- Modify: `src/PS5Kitsune.Cli/CliLog.cs`

**Interfaces:**
- Consumes: `GameRecordStore`, `TestRecords` (Task 12); `GameLayout`, `GameValidator.IsValidTitleId` (Tasks 8 and 9); `IGameLauncher`, `LogTail` (Task 14); `AnyPS5Installer.IsInstalled`, `AnyPS5NotInstalledException` (Task 5).
- Produces: `GameNotFoundException(string message)`; `GameLibrary(DataLayout, IPlatform, TimeProvider)` with `(GameLayout Game, GameRecord Record) Find(string titleId)`, which throws `GameNotFoundException`, and `Task MarkPlayedAsync(GameLayout game, GameRecord record, CancellationToken)`.

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.Core.Tests/Library/GameLibraryTests.cs`:

```csharp
using PS5Kitsune.Core.Library;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.Library;

public sealed class GameLibraryTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly DataLayout _layout;
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 20, 0, 0, TimeSpan.Zero));
    private readonly GameLibrary _library;

    public GameLibraryTests()
    {
        _layout = new DataLayout(_temp.Path);
        _library = new GameLibrary(_layout, new FakePlatform(), _time);
    }

    private Task WriteSampleAsync() => GameRecordStore.WriteAsync(
        Path.Combine(_layout.GameDirectory("PPSA99666"), ".ps5kitsune", "game.json"), TestRecords.Sample(), TestContext.Current.CancellationToken);

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

    public void Dispose() => _temp.Dispose();
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*GameLibraryTests"`
Expected: the build fails with CS0246, `The type or namespace name 'GameLibrary' could not be found`.

- [ ] **Step 3: Write the library**

`src/PS5Kitsune.Core/Library/GameLibrary.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Core.Library;

public sealed class GameNotFoundException(string message) : Exception(message);

/// <summary>Games added under &lt;root&gt;/games, looked up by title ID. library.json comes with milestone 7.</summary>
public sealed class GameLibrary(DataLayout layout, IPlatform platform, TimeProvider time)
{
    /// <exception cref="GameNotFoundException">No game.json for this title ID.</exception>
    /// <exception cref="InvalidGameRecordException">The game.json is malformed.</exception>
    public (GameLayout Game, GameRecord Record) Find(string titleId)
    {
        if (GameValidator.IsValidTitleId(titleId))
        {
            var game = new GameLayout(layout.GameDirectory(titleId), titleId, platform);
            if (GameRecordStore.Read(game.RecordFile) is { } record)
            {
                return (game, record);
            }
        }

        throw new GameNotFoundException($"No game {titleId} in {layout.Games}.");
    }

    public Task MarkPlayedAsync(GameLayout game, GameRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(record);
        return GameRecordStore.WriteAsync(game.RecordFile, record with { LastPlayedAt = time.GetLocalNow() }, cancellationToken);
    }
}
```

In `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`, add `using PS5Kitsune.Core.Library;` to the usings, and after `services.AddSingleton<GameImporter>();` add:

```csharp
        services.AddSingleton<GameLibrary>();
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/PS5Kitsune.Core.Tests/PS5Kitsune.Core.Tests.csproj --filter-class "*GameLibraryTests"`
Expected: 4 tests, all pass.

- [ ] **Step 5: Add the `run` command**

`src/PS5Kitsune.Cli/RunCommand.cs`:

```csharp
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Library;

namespace PS5Kitsune.Cli;

internal static class RunCommand
{
    public static Command Create(Option<DirectoryInfo?> rootOption)
    {
        var titleIdArgument = new Argument<string>("titleId") { Description = "Title ID of an added game, for example PPSA99666." };
        var command = new Command("run", "Launch an added game from its folder and follow its log until it exits.") { titleIdArgument };
        command.SetAction((parseResult, cancellationToken) => CommandHost.RunAsync(parseResult.GetValue(rootOption), "run", async (services, logger) =>
        {
            if (!services.GetRequiredService<AnyPS5Installer>().IsInstalled())
            {
                throw new AnyPS5NotInstalledException();
            }

            var library = services.GetRequiredService<GameLibrary>();
            var (game, record) = library.Find(parseResult.GetRequiredValue(titleIdArgument));
            var executable = Path.Combine(game.Root, record.Executable);
            if (!File.Exists(executable))
            {
                logger.ExecutableMissing(executable);
                return 1;
            }

            await library.MarkPlayedAsync(game, record, cancellationToken);
            using var launched = services.GetRequiredService<IGameLauncher>().Launch(new GameLaunchRequest(executable, game.Root, game.Logs));
            logger.GameStarted(record.Name, launched.ProcessId, launched.LogFile);

            var exit = launched.WaitForExitAsync(cancellationToken);
            try
            {
                await LogTail.FollowAsync(launched.LogFile, Console.WriteLine, exit, cancellationToken);
                var exitCode = await exit;
                logger.GameExited(record.Name, exitCode);
                return exitCode;
            }
            catch (OperationCanceledException)
            {
                // Ctrl+C stops following the log, not the game. Let the wait unregister before the handle is released.
                await exit.ContinueWith(static _ => { }, TaskScheduler.Default);
                logger.StoppedFollowing(record.Name, launched.ProcessId, launched.LogFile);
                return 130;
            }
        }));
        return command;
    }
}
```

In `src/PS5Kitsune.Cli/CliLog.cs`, add before the closing brace:

```csharp

    [LoggerMessage(Level = LogLevel.Error, Message = "{Executable} is missing; an antivirus may have deleted it. Add an exclusion for the games folder, delete the game folder, then add the game again.")]
    public static partial void ExecutableMissing(this ILogger logger, string executable);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Name} started (PID {ProcessId}). Log: {LogFile}")]
    public static partial void GameStarted(this ILogger logger, string name, int processId, string logFile);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Name} exited with code {ExitCode}.")]
    public static partial void GameExited(this ILogger logger, string name, int exitCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stopped following the log. {Name} is still running (PID {ProcessId}). Log: {LogFile}")]
    public static partial void StoppedFollowing(this ILogger logger, string name, int processId, string logFile);
```

In `src/PS5Kitsune.Cli/Program.cs`, add `RunCommand.Create(rootOption),` right after `AddCommand.Create(rootOption),`.

- [ ] **Step 6: Build and check the error path**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: the build succeeds with no warning.

Run: `dotnet run --project src/PS5Kitsune.Cli -c Release -- run PPSA00001`
Expected: `No game PPSA00001 in C:\PS5Kitsune\games.` then `Full log: …`; exit code 1.

- [ ] **Step 7: Run every test, then commit**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: every test passes.

```bash
git add src/PS5Kitsune.Core src/PS5Kitsune.Cli tests/PS5Kitsune.Core.Tests
git commit -m "Run an added game from the command line

The CLI starts the game from its folder, follows its log to the console
and returns its exit code; Ctrl+C stops following, not the game.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 16: End-to-end validation with DOOM

This task is done by hand on the test machine (AMD Ryzen 7 7730U, so no `--to-intel`). Only the user can see whether DOOM actually renders. **Ask the user to confirm** before marking the profile validated.

**Files:**
- Modify: `src/PS5Kitsune.Core/Profiles/default-profile.json`
- Modify: `tests/PS5Kitsune.Core.Tests/Profiles/ProfileLoaderTests.cs`
- Modify: `docs/architecture.md`
- Modify: `README.md`

- [ ] **Step 1: Download DOOM for PS5 outside the repository**

Run:

```bash
mkdir -p "$TEMP/ps5-doom" && gh release download v1.1.1 --repo lowbit/ps5-doom --pattern "PPSA99666.zip*" --dir "$TEMP/ps5-doom" --clobber && cat "$TEMP/ps5-doom/PPSA99666.zip.sha256" && sha256sum "$TEMP/ps5-doom/PPSA99666.zip"
```

Expected: both hashes are identical.

- [ ] **Step 2: Install (already done in Task 6; it must report every step done)**

Run: `dotnet run --project src/PS5Kitsune.Cli -c Release -- install`
Expected: six `already done` lines; exit code 0.

- [ ] **Step 3: Add DOOM**

Run: `dotnet run --project src/PS5Kitsune.Cli -c Release -- add "$TEMP/ps5-doom/PPSA99666.zip"`
Expected:
- `CPU: AuthenticAMD. Intel conversion (--to-intel): no.`
- steps `[1/6] Copy the game` … `[6/6] Record the game`; step 3 prints `Unwrapping eboot.bin` and `Unwrapping sce_module/libc.prx` (with backslashes on Windows)
- `DOOM (PPSA99666) is added in C:\PS5Kitsune\games\PPSA99666.`, the audit counts and `Verdict: green. The game can load.`
- exit code 0, and no `C:\PS5Kitsune\downloads\staging\PPSA99666` folder left.

If a step fails, open the log printed after `Full log:`. The relinker and script output is copied there line by line. Fix the cause and run `add` again: it resumes. If the verdict is orange or red, write down the counts and the absent functions listed in `C:\PS5Kitsune\games\PPSA99666\audit.json`.

- [ ] **Step 4: Check that a second add is refused**

Run: `dotnet run --project src/PS5Kitsune.Cli -c Release -- add "$TEMP/ps5-doom/PPSA99666.zip"`
Expected: `PPSA99666 is already added in C:\PS5Kitsune\games\PPSA99666. Delete that folder to add the game again.`; exit code 1.

- [ ] **Step 5: Run DOOM**

Run: `dotnet run --project src/PS5Kitsune.Cli -c Release -- run PPSA99666`
Expected: `DOOM started (PID …). Log: C:\PS5Kitsune\games\PPSA99666\.ps5kitsune\logs\<date>.log`, then the game's log lines stream to the console. **Ask the user** whether the DOOM window opens and the game is playable. Then ask them to quit DOOM, and check that the CLI prints `DOOM exited with code …` and returns that code. Also try Ctrl+C on a second run: the CLI must print `Stopped following the log. DOOM is still running …` while the game keeps running.

If DOOM fails to start, look for the known messages from the architecture's "Crash diagnosis" table in the log. A commit-limit message means the page file must grow. Report the log excerpt to the user.

- [ ] **Step 6: Record the outcome**

**If DOOM runs:**

In `src/PS5Kitsune.Core/Profiles/default-profile.json`, change `"validated": false` to `"validated": true`.

In `tests/PS5Kitsune.Core.Tests/Profiles/ProfileLoaderTests.cs`, in `Embedded_profile_loads_with_a_pinned_python`, add `Assert.True(profile.Validated);` after `Assert.Equal("release", profile.Source.Kind);`.

In `docs/architecture.md`, replace

```text
- [ ] Default mode of the first profile: release `v0.1.2` if it runs DOOM, otherwise a build of a validated commit
```

with

```text
- [x] Default mode of the first profile: **release `v0.1.2`**, which runs DOOM for PS5 (validated 2026-10-XX with milestone 2's CLI)
```

using the actual date.

In `README.md`, replace the "Status" section's text, from `Early development — milestone 1 of 8` down to `The default data root is \`C:\PS5Kitsune\`.`, with:

````markdown
Early development — milestone 2 of 8 (end-to-end command-line path). There is no graphical interface yet. The design and roadmap are in [docs/architecture.md](docs/architecture.md).

The command-line tool installs AnyPS5, adds a game and runs it:

```powershell
ps5kitsune install                   # Python, relinker, libraries and scripts of AnyPS5 v0.1.2
ps5kitsune add <game folder or zip>  # validate, copy, unwrap, convert and audit
ps5kitsune run <title ID>            # launch from the game folder, log in .ps5kitsune\logs
```

Every command takes `--root <data root>`; the default is `C:\PS5Kitsune`.
````

Run: `dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: every test passes.

```bash
git add src/PS5Kitsune.Core/Profiles/default-profile.json tests/PS5Kitsune.Core.Tests/Profiles/ProfileLoaderTests.cs docs/architecture.md README.md
git commit -m "Validate the doom-validated-2026-10 profile with DOOM for PS5

Release v0.1.2 installed, converted and ran DOOM through the CLI.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

**If DOOM does not run with `v0.1.2`:** leave `"validated": false`. Under "If DOOM fails" in the spec, add a dated paragraph with the failing step or the log excerpt and the audit counts. Commit that with the README status update (same text, without claiming DOOM runs), then **stop and ask the user** which way to go. The spec gives two options: move milestone 5 (build mode) before the screens, or place a hand-built AnyPS5 in `anyps5\current\` as a stopgap.

---

## Self-review

- **Spec coverage.** Profile changes: Task 2. Pipeline: Task 3. Install steps 1 to 6: Task 5. `DataLayout` additions: Task 5. `GameSourceResolver`: Task 7. `GameValidator`: Task 8. `CpuVendor` and `GameImportContext`: Task 9. Add steps 1 to 6: Tasks 9 to 12. "Already added", resume and staging rules: Task 13. `GameRecordStore`: Task 12. `IGameLauncher`, `WindowsGameLauncher` and the 10 kept logs: Task 14. Following the log, Ctrl+C and `lastPlayedAt`: Task 15. Each row of the error table: install not done (Tasks 13 and 15), checksum (existing `HttpDownloader`), blocking validation (13), unwrap refusal (10), relinker exit (11), audit exit 2 (11), red verdict (13), unknown title ID (15), missing executable (15), Ctrl+C (`CommandHost` and Task 15). Exit codes: `CommandHost` and Task 15. Per-command logs: existing `LoggingSetup`. Every unit test listed in the spec has a test task. End-to-end validation and "If DOOM fails": Task 16.
- **Out of scope, as in the spec.** Build mode, PC checks, Reconvert and `--move`, settings translated to environment variables, error diagnosis, Lavapipe, fonts and `library.json`.
