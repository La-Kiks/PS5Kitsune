# Milestone 5a — Build mode, Core and CLI: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `ps5kitsune install --mode build` compiles AnyPS5 from the profile's commit with portable MinGit and WinLibs and produces the same `anyps5\current` as a release install, with resume and incremental update.

**Architecture:** `AnyPS5Installer` composes Python + the steps of an `IAnyPS5Source` (`ReleaseSource` or `BuildSource`) + the shared install record step. Build steps run git and CMake through `BuildCommands`, which uses `IProcessRunner` with a child environment from `BuildEnvironment`. Each step is done when a marker in `anyps5\state\` holds the profile's commit. A valid `<root>\profiles\override.json` replaces the embedded profile.

**Tech stack:** .NET 10, C#, CliWrap 3.10.5, Microsoft.Extensions.DependencyInjection, xUnit v3 on Microsoft Testing Platform, System.CommandLine.

**Spec:** [2026-10-21-milestone-5a-build-mode-design.md](../specs/2026-10-21-milestone-5a-build-mode-design.md)

## Global Constraints

- Code, docs, commit messages and UI text are in English.
- `Directory.Build.props` sets `TreatWarningsAsErrors`, `AnalysisLevel latest-recommended` and `EnforceCodeStyleInBuild`: 0 warnings. Use `[LoggerMessage]` for logging (CA1848), `CultureInfo.InvariantCulture` for formatting numbers into strings (CA1305), and never `ToLowerInvariant()` for display text (CA1308).
- License GPL-2.0-only: no new NuGet dependency in this milestone.
- Profile `build` section: `"repository": "https://github.com/boykopovar/AnyPS5.git"`, `"commit": "48d61f53b46f1d0726889e4dd39c5ef2b3eb30cd"`.
- MinGit: version `2.56.0.2`, URL `https://github.com/git-for-windows/git/releases/download/v2.56.0.windows.2/MinGit-2.56.0.2-64-bit.zip`, SHA-256 `da35e72aa21c005a5a0d298cfbae110bc1609a815730ea0dde84b01a1b3cd3be`. The CA bundle is at `ucrt64\etc\ssl\certs\ca-bundle.crt` (checked in the zip on 2026-10-21); `mingw64\…` is the fallback.
- WinLibs: version `15.2.0-r7`, URL `https://github.com/brechtsanders/winlibs_mingw/releases/download/15.2.0posix-14.0.0-ucrt-r7/winlibs-x86_64-posix-seh-gcc-15.2.0-mingw-w64ucrt-14.0.0-r7.zip`, SHA-256 `cb2fbad6162540cdf5e1facdce08d4dac359e8cf64f7f696a99274291763b815`. The zip's top folder is `mingw64\`.
- `checks.buildMinFreeDiskGiB` = 15 (provisional; the criterion run measures it).
- Layout: `<root>\anyps5\source`, `<root>\anyps5\build`, `<root>\anyps5\state`, `<root>\anyps5\current`.
- Step names, in order, for build mode: "Install Python", "Install MinGit", "Install WinLibs", "Get the AnyPS5 source", "Update the submodules", "Configure the build", "Compile AnyPS5", "Build the libraries", "Copy the build", "Record the install".
- Marker names: `checkout`, `submodules`, `configure`, `compile`, `libs` (files `<name>.done` in `anyps5\state`, holding the full commit).
- Parallel jobs: `N = max(1, min(logical processors, floor(physical RAM in GiB / 2)))`.
- `install.json` schema 2: `schemaVersion`, `profile`, `mode` (`release` or `build`), `version` (tag, or the first 7 characters of the commit), `commit` (null for release), `unwrapScript` (null for build), `installedAt`. Schema 1 is still read.
- CLI opening lines: `Data root: <root> (<--root|saved setting|default>)`, `Profile: <id> (<embedded|override>)`, `Mode: <release|build> (<--mode|current install|default>)`.
- Tests never use the real network or compiler, except the one test marked `Explicit = true` in Task 11.
- Build: `dotnet build PS5Kitsune.slnx -c Release`. Tests: `dotnet test --solution PS5Kitsune.slnx -c Release`; one class: add `-- --filter-class "<full class name>"`. A filtered run exits with code 8 because the other test assemblies match zero tests: that is not a failure. Run CLI commands through the Bash tool.
- Commit locally after each task, author La-Kiks. Do not push.

---

### Task 1: Profile build section and build tools

**Files:**
- Modify: `src/PS5Kitsune.Core/Profiles/AnyPS5Profile.cs`
- Modify: `src/PS5Kitsune.Core/Profiles/ProfileLoader.cs`
- Modify: `src/PS5Kitsune.Core/Profiles/default-profile.json`
- Test: `tests/PS5Kitsune.Core.Tests/Profiles/ProfileLoaderTests.cs`

**Interfaces:**
- Produces: `ProfileBuild(Uri Repository, string Commit)`, `AnyPS5Profile.Build` (nullable), `AnyPS5Profile.RequireBuild()`, `CheckThresholds.BuildMinFreeDiskGiB` (default 15). `AnyPS5Profile.AlternateSource` is removed.

- [ ] **Step 1: Write the failing tests** (append to `ProfileLoaderTests`)

```csharp
    [Fact]
    public void Embedded_profile_pins_the_build_commit_and_the_build_tools()
    {
        var profile = ProfileLoader.LoadEmbedded();

        Assert.Equal(new Uri("https://github.com/boykopovar/AnyPS5.git"), profile.Build?.Repository);
        Assert.Equal("48d61f53b46f1d0726889e4dd39c5ef2b3eb30cd", profile.Build?.Commit);
        Assert.Equal("da35e72aa21c005a5a0d298cfbae110bc1609a815730ea0dde84b01a1b3cd3be", profile.Tools["mingit"].Sha256);
        Assert.Equal("cb2fbad6162540cdf5e1facdce08d4dac359e8cf64f7f696a99274291763b815", profile.Tools["winlibs"].Sha256);
        Assert.Equal(15, profile.Checks.BuildMinFreeDiskGiB);
    }

    [Theory]
    [InlineData("http://github.com/boykopovar/AnyPS5.git", "48d61f53b46f1d0726889e4dd39c5ef2b3eb30cd")]
    [InlineData("https://github.com/boykopovar/AnyPS5.git", "48d61f5")]
    [InlineData("https://github.com/boykopovar/AnyPS5.git", "48D61F53B46F1D0726889E4DD39C5EF2B3EB30CD")]
    public void Rejects_an_invalid_build_section(string repository, string commit)
    {
        Assert.Throws<InvalidProfileException>(() => Load($$"""
            {
              "schemaVersion": 1, "id": "bad", "source": { "kind": "release", "tag": "v1" },
              "build": { "repository": "{{repository}}", "commit": "{{commit}}" }
            }
            """));
    }

    [Fact]
    public void Rejects_a_negative_build_disk_threshold()
    {
        Assert.Throws<InvalidProfileException>(() => Load("""
            {
              "schemaVersion": 1, "id": "bad", "source": { "kind": "release", "tag": "v1" },
              "checks": { "buildMinFreeDiskGiB": -1 }
            }
            """));
    }

    [Fact]
    public void RequireBuild_names_the_profile_when_it_has_no_build_section()
    {
        var profile = Load("""{ "schemaVersion": 1, "id": "release-only", "source": { "kind": "release", "tag": "v1" } }""");

        var exception = Assert.Throws<InvalidProfileException>(() => profile.RequireBuild());

        Assert.Contains("release-only", exception.Message, StringComparison.Ordinal);
    }
```

`Load(string)` already exists in `ProfileLoaderTests` (it wraps `ProfileLoader.Load` over a UTF-8 stream). If not, add:

```csharp
    private static AnyPS5Profile Load(string json) => ProfileLoader.Load(new MemoryStream(Encoding.UTF8.GetBytes(json)));
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.Core.Tests.Profiles.ProfileLoaderTests"`
Expected: build errors (`Build`, `RequireBuild`, `BuildMinFreeDiskGiB` don't exist).

- [ ] **Step 3: Implement**

In `AnyPS5Profile.cs`, replace the `AlternateSource` property with:

```csharp
    /// <summary>What build mode compiles; null when the profile only supports release mode.</summary>
    public ProfileBuild? Build { get; init; }
```

Add this method after `AssetUrl`:

```csharp
    /// <exception cref="InvalidProfileException">The profile has no build section.</exception>
    public ProfileBuild RequireBuild() =>
        Build ?? throw new InvalidProfileException($"Profile {Id} has no build section: build mode is not available.");
```

Add this record after `ProfileSource`:

```csharp
/// <param name="Repository">Git URL of AnyPS5, over HTTPS.</param>
/// <param name="Commit">Full lowercase SHA of the commit to build.</param>
public sealed record ProfileBuild(Uri Repository, string Commit);
```

In `CheckThresholds`, after `MinFreeDiskGiB`:

```csharp
    /// <summary>Free space wanted for build mode: the clone, the build folder, the tools and a game.</summary>
    public long BuildMinFreeDiskGiB { get; init; } = 15;
```

In `ProfileLoader.Validate`, before the `checks` test, add:

```csharp
        if (profile.Build is { } build)
        {
            RequireHttps(profile.Id, "build repository", build.Repository);
            if (build.Commit.Length != 40 || !build.Commit.All(char.IsAsciiHexDigitLower))
            {
                throw new InvalidProfileException($"Profile {profile.Id}: build.commit needs a full lowercase hex SHA, got \"{build.Commit}\".");
            }
        }
```

and change the `checks` condition to:

```csharp
        if (profile.Checks.CommitLimitMiB <= 0 || profile.Checks.MinFreeDiskGiB < 0 || profile.Checks.BuildMinFreeDiskGiB < 0)
        {
            throw new InvalidProfileException($"Profile {profile.Id}: checks.commitLimitMiB must be positive, checks.minFreeDiskGiB and checks.buildMinFreeDiskGiB not negative.");
        }
```

In `default-profile.json`, replace the `alternateSource` line with:

```json
  "build": {
    "repository": "https://github.com/boykopovar/AnyPS5.git",
    "commit": "48d61f53b46f1d0726889e4dd39c5ef2b3eb30cd"
  },
```

add to `tools`, after `python`:

```json
    "mingit": {
      "version": "2.56.0.2",
      "url": "https://github.com/git-for-windows/git/releases/download/v2.56.0.windows.2/MinGit-2.56.0.2-64-bit.zip",
      "sha256": "da35e72aa21c005a5a0d298cfbae110bc1609a815730ea0dde84b01a1b3cd3be"
    },
    "winlibs": {
      "version": "15.2.0-r7",
      "url": "https://github.com/brechtsanders/winlibs_mingw/releases/download/15.2.0posix-14.0.0-ucrt-r7/winlibs-x86_64-posix-seh-gcc-15.2.0-mingw-w64ucrt-14.0.0-r7.zip",
      "sha256": "cb2fbad6162540cdf5e1facdce08d4dac359e8cf64f7f696a99274291763b815"
    }
```

and add `"buildMinFreeDiskGiB": 15,` after `"minFreeDiskGiB": 5,` in `checks`.

- [ ] **Step 4: Run the tests**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.Core.Tests.Profiles.ProfileLoaderTests"`
Expected: all pass. Then the full build (`dotnet build PS5Kitsune.slnx -c Release`): 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Core/Profiles tests/PS5Kitsune.Core.Tests/Profiles
git commit -m "Add the build section and the build tools to the profile"
```

---

### Task 2: Active profile with a local override

**Files:**
- Create: `src/PS5Kitsune.Core/Profiles/ActiveProfile.cs`
- Modify: `src/PS5Kitsune.Core/Profiles/ProfileLoader.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Profiles/ActiveProfileTests.cs`

**Interfaces:**
- Consumes: `DataLayout.Profiles` (`<root>\profiles`).
- Produces: `ProfileOrigin { Embedded, Override }`, `ActiveProfile(AnyPS5Profile Profile, ProfileOrigin Origin, string? Problem)`, `ProfileLoader.OverrideFileName` (`"override.json"`), `ProfileLoader.LoadActive(DataLayout layout)`.

- [ ] **Step 1: Write the failing tests**

```csharp
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Tests.Profiles;

public sealed class ActiveProfileTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    private DataLayout Layout => new(_temp.Path);

    private string OverridePath => _temp.Combine("profiles", "override.json");

    [Fact]
    public void Without_an_override_the_embedded_profile_is_active()
    {
        var active = ProfileLoader.LoadActive(Layout);

        Assert.Equal(ProfileLoader.LoadEmbedded(), active.Profile);
        Assert.Equal(ProfileOrigin.Embedded, active.Origin);
        Assert.Null(active.Problem);
    }

    [Fact]
    public void A_valid_override_replaces_the_embedded_profile()
    {
        TestFiles.Write(OverridePath, """{ "schemaVersion": 1, "id": "my-override", "source": { "kind": "release", "tag": "v9" } }""");

        var active = ProfileLoader.LoadActive(Layout);

        Assert.Equal("my-override", active.Profile.Id);
        Assert.Equal(ProfileOrigin.Override, active.Origin);
        Assert.Null(active.Problem);
    }

    [Fact]
    public void An_invalid_override_is_ignored_with_a_problem()
    {
        TestFiles.Write(OverridePath, "{ not json");

        var active = ProfileLoader.LoadActive(Layout);

        Assert.Equal(ProfileOrigin.Embedded, active.Origin);
        Assert.Equal(ProfileLoader.LoadEmbedded().Id, active.Profile.Id);
        Assert.Contains("override.json", active.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unreadable_override_is_ignored_with_a_problem()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Only Windows refuses to open a file another handle holds with FileShare.None.");
        }

        TestFiles.Write(OverridePath, """{ "schemaVersion": 1, "id": "locked", "source": { "kind": "release", "tag": "v9" } }""");
        using var lockHandle = new FileStream(OverridePath, FileMode.Open, FileAccess.Read, FileShare.None);

        var active = ProfileLoader.LoadActive(Layout);

        Assert.Equal(ProfileOrigin.Embedded, active.Origin);
        Assert.Contains("override.json", active.Problem, StringComparison.Ordinal);
    }

    public void Dispose() => _temp.Dispose();
}
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.Core.Tests.Profiles.ActiveProfileTests"`
Expected: build errors (`LoadActive`, `ProfileOrigin` don't exist).

- [ ] **Step 3: Implement**

`src/PS5Kitsune.Core/Profiles/ActiveProfile.cs`:

```csharp
namespace PS5Kitsune.Core.Profiles;

public enum ProfileOrigin
{
    /// <summary>The profile shipped inside the app.</summary>
    Embedded,

    /// <summary>&lt;root&gt;\profiles\override.json.</summary>
    Override,
}

/// <param name="Problem">Why the override was ignored; null when nothing was ignored.</param>
public sealed record ActiveProfile(AnyPS5Profile Profile, ProfileOrigin Origin, string? Problem);
```

In `ProfileLoader`, add after `EmbeddedResourceName`:

```csharp
    public const string OverrideFileName = "override.json";
```

and after `LoadEmbedded`:

```csharp
    /// <summary>
    /// The override in the data root's profiles folder when it is valid, else the embedded profile.
    /// An override that cannot be read or validated is ignored, and <see cref="ActiveProfile.Problem"/> says why.
    /// </summary>
    public static ActiveProfile LoadActive(DataLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var path = Path.Combine(layout.Profiles, OverrideFileName);
        if (!File.Exists(path))
        {
            return new ActiveProfile(LoadEmbedded(), ProfileOrigin.Embedded, null);
        }

        try
        {
            using var stream = File.OpenRead(path);
            return new ActiveProfile(Load(stream), ProfileOrigin.Override, null);
        }
        catch (Exception exception) when (exception is InvalidProfileException or IOException or UnauthorizedAccessException)
        {
            return new ActiveProfile(LoadEmbedded(), ProfileOrigin.Embedded, $"Ignored {path}: {exception.Message} Using the embedded profile.");
        }
    }
```

- [ ] **Step 4: Run the tests**

Run the same filter. Expected: 4 pass (one skipped on Linux).

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Core/Profiles tests/PS5Kitsune.Core.Tests/Profiles
git commit -m "Load a local profile override when it is valid"
```

---

### Task 3: MinGit and WinLibs tools, build environment

**Files:**
- Modify: `src/PS5Kitsune.Core/Tools/ArchiveTool.cs` (`RequiredFile` becomes `RequiredFiles`)
- Modify: `src/PS5Kitsune.Core/Tools/PythonEmbeddableTool.cs`
- Create: `src/PS5Kitsune.Core/Tools/MinGitTool.cs`
- Create: `src/PS5Kitsune.Core/Tools/WinLibsTool.cs`
- Create: `src/PS5Kitsune.Core/Tools/BuildEnvironment.cs`
- Modify: `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Tools/BuildToolsTests.cs`

**Interfaces:**
- Produces:
  - `MinGitTool.ToolId = "mingit"`, `Executable` (`tools\mingit\cmd\git.exe`), `CommandDirectory` (`tools\mingit\cmd`), `CaBundle` (throws `InvalidOperationException` when missing).
  - `WinLibsTool.ToolId = "winlibs"`, `Bin` (`tools\winlibs\mingw64\bin`), `Gcc`, `Gxx`, `CMake`, `Ninja`, `RuntimeDlls` (the 3 DLL paths, in the order `libgcc_s_seh-1.dll`, `libstdc++-6.dll`, `libwinpthread-1.dll`).
  - `BuildEnvironment(MinGitTool mingit, WinLibsTool winlibs)` with `Git`, `CMake`, `Ninja`, `Gcc`, `Gxx`, `CaBundle` (paths) and `IReadOnlyDictionary<string, string?> Variables()`.
  - Both tools are registered as themselves and as `IPortableTool` when the profile declares them; `BuildEnvironment` is a singleton.

- [ ] **Step 1: Write the failing tests**

```csharp
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.Tests.Tools;

public sealed class BuildToolsTests : IDisposable
{
    private static readonly byte[] MinGitArchive = TestFiles.ZipBytes(
        ("cmd/git.exe", "git"u8.ToArray()),
        ("ucrt64/etc/ssl/certs/ca-bundle.crt", "certs"u8.ToArray()));

    private static readonly byte[] WinLibsArchive = TestFiles.ZipBytes(
        ("mingw64/bin/gcc.exe", "gcc"u8.ToArray()),
        ("mingw64/bin/g++.exe", "g++"u8.ToArray()),
        ("mingw64/bin/cmake.exe", "cmake"u8.ToArray()),
        ("mingw64/bin/ninja.exe", "ninja"u8.ToArray()));

    private static readonly ToolDefinition MinGitDefinition = new("2.56.0.2", new Uri("https://example.test/mingit.zip"), TestFiles.Sha256(MinGitArchive));

    private static readonly ToolDefinition WinLibsDefinition = new("15.2.0-r7", new Uri("https://example.test/winlibs.zip"), TestFiles.Sha256(WinLibsArchive));

    private readonly TempDirectory _temp = new();
    private readonly FakeDownloader _downloader = new();
    private readonly FakeProcessRunner _runner = new();

    public BuildToolsTests()
    {
        _downloader.Serve(MinGitDefinition.Url, MinGitArchive);
        _downloader.Serve(WinLibsDefinition.Url, WinLibsArchive);
    }

    private DataLayout Layout => new(_temp.Path);

    private MinGitTool MinGit() => new(MinGitDefinition, Layout, new FakePlatform(), _downloader, _runner);

    private WinLibsTool WinLibs() => new(WinLibsDefinition, Layout, new FakePlatform(), _downloader, _runner);

    [Fact]
    public async Task MinGit_installs_into_tools_mingit()
    {
        var tool = MinGit();

        await tool.InstallAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(tool.IsInstalled());
        Assert.Equal(_temp.Combine("tools", "mingit", "cmd", "git.exe"), tool.Executable);
        Assert.Equal(_temp.Combine("tools", "mingit", "ucrt64", "etc", "ssl", "certs", "ca-bundle.crt"), tool.CaBundle);
    }

    [Fact]
    public async Task WinLibs_is_not_installed_without_ninja()
    {
        var tool = WinLibs();
        await tool.InstallAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(tool.IsInstalled());

        File.Delete(tool.Ninja);

        Assert.False(tool.IsInstalled());
    }

    [Fact]
    public void The_CA_bundle_falls_back_to_mingw64()
    {
        TestFiles.Write(_temp.Combine("tools", "mingit", "mingw64", "etc", "ssl", "certs", "ca-bundle.crt"), "certs");

        Assert.Equal(_temp.Combine("tools", "mingit", "mingw64", "etc", "ssl", "certs", "ca-bundle.crt"), MinGit().CaBundle);
    }

    [Fact]
    public void A_missing_CA_bundle_names_both_places()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => MinGit().CaBundle);

        Assert.Contains("ucrt64", exception.Message, StringComparison.Ordinal);
        Assert.Contains("mingw64", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Versions_are_read_from_the_tools()
    {
        _runner.Respond = spec => Path.GetFileName(spec.FileName) == "git.exe"
            ? new FakeProcessResult(0, ["git version 2.56.0.windows.2"])
            : new FakeProcessResult(0, ["15.2.0"]);

        Assert.Equal("2.56.0.windows.2", await MinGit().GetVersionAsync(TestContext.Current.CancellationToken));
        Assert.Equal("15.2.0", await WinLibs().GetVersionAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["-dumpfullversion"], _runner.LastSpec!.Arguments);
    }

    [Fact]
    public async Task The_build_environment_puts_the_tools_first_and_sets_the_CA_bundle()
    {
        var mingit = MinGit();
        var winlibs = WinLibs();
        await mingit.InstallAsync(cancellationToken: TestContext.Current.CancellationToken);

        var variables = new BuildEnvironment(mingit, winlibs).Variables();

        var path = variables["PATH"]!.Split(Path.PathSeparator);
        Assert.Equal(winlibs.Bin, path[0]);
        Assert.Equal(mingit.CommandDirectory, path[1]);
        Assert.Equal(mingit.CaBundle, variables["SSL_CERT_FILE"]);
    }

    public void Dispose() => _temp.Dispose();
}
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.Core.Tests.Tools.BuildToolsTests"`
Expected: build errors.

- [ ] **Step 3: Implement**

In `ArchiveTool.cs`, replace the `RequiredFile` property and the first check in `IsInstalled`:

```csharp
    /// <summary>Files that must exist once the tool is extracted.</summary>
    protected abstract IEnumerable<string> RequiredFiles { get; }

    public bool IsInstalled()
    {
        var marker = Path.Combine(InstallDirectory, MarkerFileName);
        if (!File.Exists(marker) || !RequiredFiles.All(File.Exists))
        {
            return false;
        }
```

(the rest of `IsInstalled` is unchanged). In `PythonEmbeddableTool.cs`: `protected override IEnumerable<string> RequiredFiles => [Executable];`

`src/PS5Kitsune.Core/Tools/MinGitTool.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Tools;

/// <summary>The official MinGit zip: git for build mode, and the CA bundle CMake needs to download over HTTPS.</summary>
public sealed class MinGitTool(
    ToolDefinition definition,
    DataLayout layout,
    IPlatform platform,
    IDownloader downloader,
    IProcessRunner processRunner)
    : ArchiveTool(ToolId, definition, layout, downloader)
{
    public const string ToolId = "mingit";

    private const string VersionPrefix = "git version ";

    public string CommandDirectory => Path.Combine(InstallDirectory, "cmd");

    public string Executable => Path.Combine(CommandDirectory, "git" + platform.ExecutableSuffix);

    /// <summary>The CA bundle, under ucrt64 (MinGit 2.56) or mingw64 (older MinGit), as AnyPS5's CI looks for it.</summary>
    /// <exception cref="InvalidOperationException">Neither file exists.</exception>
    public string CaBundle
    {
        get
        {
            string[] candidates =
            [
                Path.Combine(InstallDirectory, "ucrt64", "etc", "ssl", "certs", "ca-bundle.crt"),
                Path.Combine(InstallDirectory, "mingw64", "etc", "ssl", "certs", "ca-bundle.crt"),
            ];
            return candidates.FirstOrDefault(File.Exists)
                ?? throw new InvalidOperationException($"MinGit has no CA bundle: neither {candidates[0]} nor {candidates[1]} exists.");
        }
    }

    protected override IEnumerable<string> RequiredFiles => [Executable];

    public override async Task<string> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var lines = new List<string>();
        var result = await processRunner.RunAsync(new ProcessSpec(Executable, ["--version"]), line => lines.Add(line.Text), cancellationToken).ConfigureAwait(false);

        // "git version 2.56.0.windows.2"
        var output = string.Join(' ', lines).Trim();
        if (result.ExitCode != 0 || !output.StartsWith(VersionPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{Executable} --version exited with code {result.ExitCode}: {output}");
        }

        return output[VersionPrefix.Length..];
    }
}
```

`src/PS5Kitsune.Core/Tools/WinLibsTool.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.Tools;

/// <summary>The WinLibs GCC zip, the only toolchain AnyPS5 supports on Windows. It ships CMake and Ninja too.</summary>
public sealed class WinLibsTool(
    ToolDefinition definition,
    DataLayout layout,
    IPlatform platform,
    IDownloader downloader,
    IProcessRunner processRunner)
    : ArchiveTool(ToolId, definition, layout, downloader)
{
    public const string ToolId = "winlibs";

    public string Bin => Path.Combine(InstallDirectory, "mingw64", "bin");

    public string Gcc => Tool("gcc");

    public string Gxx => Tool("g++");

    public string CMake => Tool("cmake");

    public string Ninja => Tool("ninja");

    /// <summary>The GCC runtime DLLs the relinker and the libraries load; they go into anyps5\current\libs.</summary>
    public IReadOnlyList<string> RuntimeDlls =>
    [
        Path.Combine(Bin, "libgcc_s_seh-1.dll"),
        Path.Combine(Bin, "libstdc++-6.dll"),
        Path.Combine(Bin, "libwinpthread-1.dll"),
    ];

    protected override IEnumerable<string> RequiredFiles => [Gxx, CMake, Ninja];

    public override async Task<string> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var lines = new List<string>();
        var result = await processRunner.RunAsync(new ProcessSpec(Gcc, ["-dumpfullversion"]), line => lines.Add(line.Text), cancellationToken).ConfigureAwait(false);

        // "15.2.0"
        var output = lines.FirstOrDefault()?.Trim() ?? "";
        if (result.ExitCode != 0 || output.Length == 0)
        {
            throw new InvalidOperationException($"{Gcc} -dumpfullversion exited with code {result.ExitCode}: {output}");
        }

        return output;
    }

    private string Tool(string name) => Path.Combine(Bin, name + platform.ExecutableSuffix);
}
```

`src/PS5Kitsune.Core/Tools/BuildEnvironment.cs`:

```csharp
namespace PS5Kitsune.Core.Tools;

/// <summary>
/// The tools build mode runs, and the environment of their processes: WinLibs then MinGit first on the child's PATH,
/// and SSL_CERT_FILE on MinGit's CA bundle so CMake can download FFmpeg (otherwise curl error 60).
/// The app's own environment is never changed.
/// </summary>
public sealed class BuildEnvironment(MinGitTool mingit, WinLibsTool winlibs)
{
    public string Git => mingit.Executable;

    public string CMake => winlibs.CMake;

    public string Ninja => winlibs.Ninja;

    public string Gcc => winlibs.Gcc;

    public string Gxx => winlibs.Gxx;

    public string CaBundle => mingit.CaBundle;

    /// <summary>Read when a command starts: the CA bundle only exists once MinGit is installed.</summary>
    /// <exception cref="InvalidOperationException">MinGit has no CA bundle.</exception>
    public IReadOnlyDictionary<string, string?> Variables()
    {
        string[] path = [winlibs.Bin, mingit.CommandDirectory, .. (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)];
        return new Dictionary<string, string?>
        {
            ["PATH"] = string.Join(Path.PathSeparator, path),
            ["SSL_CERT_FILE"] = CaBundle,
        };
    }
}
```

In `ServiceCollectionExtensions.AddPS5KitsuneCore`, replace the Python block with a helper used for the three tools, then register `BuildEnvironment`:

```csharp
        AddTool<PythonEmbeddableTool>(services, profile, PythonEmbeddableTool.ToolId);
        AddTool<MinGitTool>(services, profile, MinGitTool.ToolId);
        AddTool<WinLibsTool>(services, profile, WinLibsTool.ToolId);
        services.AddSingleton<BuildEnvironment>();

        return services;
    }

    /// <summary>Registers a tool the profile declares, as itself and as <see cref="IPortableTool"/>, so 'tools list' shows it.</summary>
    private static void AddTool<TTool>(IServiceCollection services, AnyPS5Profile profile, string id)
        where TTool : class, IPortableTool
    {
        if (profile.Tools.TryGetValue(id, out var definition))
        {
            services.AddSingleton(provider => ActivatorUtilities.CreateInstance<TTool>(provider, definition));
            services.AddSingleton<IPortableTool>(provider => provider.GetRequiredService<TTool>());
        }
    }
```

- [ ] **Step 4: Run the tests**

Run the `BuildToolsTests` filter, then `PS5Kitsune.Core.Tests.Tools.PythonEmbeddableToolTests`. Expected: all pass. Full build: 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Core/Tools src/PS5Kitsune.Core/ServiceCollectionExtensions.cs tests/PS5Kitsune.Core.Tests/Tools
git commit -m "Add the MinGit and WinLibs tools and the build environment"
```

---

### Task 4: Process tree kill, error output level, Ninja progress

**Files:**
- Modify: `src/PS5Kitsune.Core/Processes/CliWrapProcessRunner.cs`
- Modify: `src/PS5Kitsune.Core/Processes/OutputTail.cs` (add `Add(string)`)
- Modify: `src/PS5Kitsune.Core/Pipeline/StepProgress.cs` (add `Fraction`)
- Create: `src/PS5Kitsune.Core/Pipeline/StepFraction.cs`
- Create: `src/PS5Kitsune.Core/Processes/NinjaProgress.cs`
- Modify: `src/PS5Kitsune.Cli/ConsoleStepProgress.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Processes/CliWrapProcessRunnerTests.cs`, `tests/PS5Kitsune.Core.Tests/Processes/NinjaProgressTests.cs`

**Interfaces:**
- Produces: `StepFraction(int Done, int Total)` (readonly record struct), `StepProgress(string? Message = null, DownloadProgress? Download = null, StepFraction? Fraction = null)`, `NinjaProgress.TryParse(string line, out StepFraction fraction)`, `OutputTail.Add(string text)`.
- Behavior: cancelling `IProcessRunner.RunAsync` kills the whole process tree, then throws `OperationCanceledException`. Error-output lines are logged at Information unless the spec is quiet.

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.Core.Tests/Processes/NinjaProgressTests.cs`:

```csharp
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Processes;

namespace PS5Kitsune.Core.Tests.Processes;

public sealed class NinjaProgressTests
{
    [Fact]
    public void Reads_the_counter_of_a_status_line()
    {
        Assert.True(NinjaProgress.TryParse("[123/2400] Building CXX object core/CMakeFiles/core.dir/main.cpp.obj", out var fraction));
        Assert.Equal(new StepFraction(123, 2400), fraction);
    }

    [Theory]
    [InlineData("[0/0] Re-running CMake")]
    [InlineData("[12/abc] nope")]
    [InlineData("[5/3] more done than total")]
    [InlineData("Building CXX object without a counter")]
    [InlineData("  [1/2] indented")]
    [InlineData("")]
    public void Ignores_other_lines(string line)
    {
        Assert.False(NinjaProgress.TryParse(line, out _));
    }
}
```

Append to `CliWrapProcessRunnerTests` (add `using System.Diagnostics;`):

```csharp
    [Fact]
    public async Task Cancelling_kills_the_children_too()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Uses powershell.exe to start a child process.");
        }

        using var temp = new TempDirectory();
        var pidFile = temp.Combine("child.pid");
        var spec = new ProcessSpec("powershell.exe",
        [
            "-NoProfile", "-Command",
            "$c = Start-Process powershell.exe -ArgumentList '-NoProfile','-Command','Start-Sleep 120' -PassThru -NoNewWindow; Set-Content -LiteralPath $env:PS5K_PID_FILE -Value $c.Id; Wait-Process -Id $c.Id",
        ])
        {
            Environment = new Dictionary<string, string?> { ["PS5K_PID_FILE"] = pidFile },
        };
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var running = _runner.RunAsync(spec, cancellationToken: cancellation.Token);
        var waited = Stopwatch.StartNew();
        while (!File.Exists(pidFile) || new FileInfo(pidFile).Length == 0)
        {
            Assert.True(waited.Elapsed < TimeSpan.FromSeconds(30), "The child never wrote its PID.");
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        var childId = int.Parse(File.ReadAllText(pidFile).Trim(), System.Globalization.CultureInfo.InvariantCulture);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        try
        {
            using var child = Process.GetProcessById(childId);
            Assert.True(child.WaitForExit(5000), "The child process is still running.");
        }
        catch (ArgumentException)
        {
            // Already gone.
        }
    }

    [Fact]
    public async Task Error_output_is_logged_at_Information()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Uses cmd.exe to write to the error output.");
        }

        var logger = new CapturingLogger();
        var runner = new CliWrapProcessRunner(logger);

        await runner.RunAsync(new ProcessSpec("cmd.exe", ["/c", "echo oops 1>&2"]), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Information && entry.Message.Contains("oops", StringComparison.Ordinal));
    }
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.Core.Tests.Processes.NinjaProgressTests"` (build error) and the `CliWrapProcessRunnerTests` filter (after Step 3's types exist, `Error_output_is_logged_at_Information` fails because stderr is logged at Debug; `Cancelling_kills_the_children_too` may pass or fail depending on CliWrap's own kill: the change below makes it deterministic either way).

- [ ] **Step 3: Implement**

`src/PS5Kitsune.Core/Pipeline/StepFraction.cs`:

```csharp
namespace PS5Kitsune.Core.Pipeline;

/// <summary>Work units done out of a total, for example Ninja's [123/2400].</summary>
public readonly record struct StepFraction(int Done, int Total);
```

`StepProgress` gains the parameter (keep the rest of the file):

```csharp
/// <param name="Message">A line of detail, for example the file being unwrapped.</param>
/// <param name="Download">Progress of the file the step is downloading.</param>
/// <param name="Fraction">Progress counted in work units, for example compiled files.</param>
public sealed record StepProgress(string? Message = null, DownloadProgress? Download = null, StepFraction? Fraction = null)
```

`src/PS5Kitsune.Core/Processes/NinjaProgress.cs`:

```csharp
using System.Globalization;
using System.Text.RegularExpressions;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.Processes;

/// <summary>Reads Ninja's status lines, "[123/2400] Building CXX object …".</summary>
public static partial class NinjaProgress
{
    public static bool TryParse(string line, out StepFraction fraction)
    {
        ArgumentNullException.ThrowIfNull(line);
        fraction = default;
        var match = StatusPattern().Match(line);
        if (!match.Success
            || !int.TryParse(match.Groups[1].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var done)
            || !int.TryParse(match.Groups[2].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var total)
            || total <= 0 || done > total)
        {
            return false;
        }

        fraction = new StepFraction(done, total);
        return true;
    }

    [GeneratedRegex(@"^\[(\d+)/(\d+)\]")]
    private static partial Regex StatusPattern();
}
```

`OutputTail`: split `Collect` so other callers can add lines of any source:

```csharp
    /// <summary>Pass as the output callback of <see cref="IProcessRunner.RunAsync"/>: keeps the error output only.</summary>
    public void Collect(OutputLine line)
    {
        if (line.Source == OutputSource.StandardError)
        {
            Add(line.Text);
        }
    }

    /// <summary>Keeps a line whatever its source; build tools print compiler errors on the standard output.</summary>
    public void Add(string text)
    {
        lock (_lock)
        {
            _lines.Enqueue(text);
            if (_lines.Count > capacity)
            {
                _lines.Dequeue();
            }
        }
    }
```

`CliWrapProcessRunner.RunAsync`: replace the `ExecuteAsync` line and `Forward`:

```csharp
        var stopwatch = Stopwatch.StartNew();

        // CliWrap's cancellation may stop only the direct child; Ninja's g++ and cc1plus would keep running.
        // So the command runs without CliWrap's token, and cancellation kills the whole tree.
        var task = command.ExecuteAsync(CancellationToken.None);
        CommandResult result;
        using (cancellationToken.Register(() => KillTree(task.ProcessId)))
        {
            result = await task.ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
```

(keep the logging of the exit and the `return` that follow), and:

```csharp
        void Forward(OutputSource source, string text)
        {
            if (source == OutputSource.StandardError && !spec.Quiet)
            {
                LogErrorOutput(spec.FileName, text);
            }
            else
            {
                LogOutput(spec.FileName, text);
            }

            onOutput?.Invoke(new OutputLine(source, text));
        }
    }

    private static void KillTree(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Already exited.
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "[{FileName}] {Text}")]
    private partial void LogErrorOutput(string fileName, string text);
```

`CommandResult` is `CliWrap.CommandResult` (already imported through `using CliWrap;`).

`ConsoleStepProgress` (CLI) prints fractions every 10 %, like downloads, and starts over when the total changes (the libs build has its own counter):

```csharp
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Cli;

/// <summary>Prints step details, download progress and build progress, synchronously so lines stay in order.</summary>
internal sealed class ConsoleStepProgress : IProgress<StepProgress>
{
    private readonly ConsoleDownloadProgress _download = new();
    private int _fractionTotal = -1;
    private long _fractionTenth = -1;

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

        if (value.Fraction is { } fraction)
        {
            ReportFraction(fraction);
        }
    }

    private void ReportFraction(StepFraction fraction)
    {
        if (fraction.Total != _fractionTotal)
        {
            _fractionTotal = fraction.Total;
            _fractionTenth = -1;
        }

        var tenth = (long)fraction.Done * 10 / fraction.Total;
        if (tenth != _fractionTenth)
        {
            _fractionTenth = tenth;
            Console.WriteLine($"  {tenth * 10,3} %  ({fraction.Done} / {fraction.Total})");
        }
    }
}
```

- [ ] **Step 4: Run the tests**

Run the `NinjaProgressTests` and `CliWrapProcessRunnerTests` filters, then the whole suite (the Vulkan probe runner relies on cancellation of `IProcessRunner`). Expected: all pass, 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Core/Processes src/PS5Kitsune.Core/Pipeline src/PS5Kitsune.Cli/ConsoleStepProgress.cs tests/PS5Kitsune.Core.Tests/Processes
git commit -m "Kill the whole process tree on cancel and report Ninja progress"
```

---

### Task 5: Installer modes and install.json schema 2

**Files:**
- Create: `src/PS5Kitsune.Core/AnyPS5/AnyPS5Mode.cs`
- Create: `src/PS5Kitsune.Core/AnyPS5/IAnyPS5Source.cs`
- Create: `src/PS5Kitsune.Core/AnyPS5/ReleaseSource.cs`
- Create: `src/PS5Kitsune.Core/AnyPS5/Steps/InstallToolStep.cs`
- Delete: `src/PS5Kitsune.Core/AnyPS5/Steps/InstallPythonStep.cs`
- Modify: `src/PS5Kitsune.Core/AnyPS5/AnyPS5InstallRecord.cs`, `AnyPS5Installer.cs`, `IAnyPS5Installer.cs`, `Steps/WriteInstallRecordStep.cs`
- Modify: `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`
- Modify: `src/PS5Kitsune.Cli/InstallCommand.cs` (pass `AnyPS5Mode.Release` until Task 10)
- Modify: `src/PS5Kitsune.App/ViewModels/HomeViewModel.cs`, `InstallViewModel.cs`, `src/PS5Kitsune.App/Shell/PageFactory.cs`
- Test: `tests/PS5Kitsune.Core.Tests/AnyPS5/AnyPS5TestSetup.cs`, `AnyPS5InstallerTests.cs`; `tests/PS5Kitsune.App.Tests/Fakes.cs`, `InstallViewModelTests.cs` and any App test calling the changed members

**Interfaces:**
- Produces:
  - `enum AnyPS5Mode { Release, Build }` (serialized `release`/`build` by `JsonFiles`).
  - `AnyPS5InstallRecord(int SchemaVersion, string Profile, AnyPS5Mode Mode, string Version, string? Commit, Uri? UnwrapScript, DateTimeOffset InstalledAt)`, `CurrentSchemaVersion = 2`.
  - `IAnyPS5Source { AnyPS5Mode Mode; IReadOnlyList<IStep> CreateSteps(); AnyPS5InstallRecord CreateRecord(DateTimeOffset installedAt); }`.
  - `ReleaseSource(AnyPS5Profile profile, AnyPS5Paths paths, DataLayout layout, IDownloader downloader) : IAnyPS5Source`.
  - `InstallToolStep(string name, IPortableTool tool) : IStep`.
  - `IAnyPS5Installer`: `AnyPS5Mode CurrentMode { get; }`, `AnyPS5InstallRecord? ReadRecord()`, `IReadOnlyList<IStep> CreateSteps(AnyPS5Mode mode)`, `AnyPS5InstallState GetState(AnyPS5Mode mode)`, `Task InstallAsync(AnyPS5Mode mode, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default)`.
  - `AnyPS5Installer(IEnumerable<IAnyPS5Source> sources, PythonEmbeddableTool python, AnyPS5Paths paths, StepRunner stepRunner, TimeProvider time)`; `IsInstalled()` keeps its meaning for the current mode.
  - `InstallViewModel(IAnyPS5Installer installer, AnyPS5Mode mode, string logFile, IPlatform platform, IUiDispatcher ui, Navigator navigator, IPages pages, Action<bool>? busyChanged = null)`.

- [ ] **Step 1: Write the failing tests**

Update `AnyPS5TestSetup`: replace the installer construction with

```csharp
        ReleaseSource = new ReleaseSource(Profile, Paths, Layout, Downloader);
        Installer = new AnyPS5Installer([ReleaseSource], PythonTool, Paths, StepRunner, Time);
```

add `public ReleaseSource ReleaseSource { get; }`, and change `InstallAsync` to

```csharp
    public Task InstallAsync(AnyPS5Mode mode = AnyPS5Mode.Release) => Installer.InstallAsync(mode, cancellationToken: TestContext.Current.CancellationToken);
```

In `AnyPS5InstallerTests`, replace every `GetState()` with `GetState(AnyPS5Mode.Release)`, change the expected record in `Install_writes_the_install_record` to

```csharp
            new AnyPS5InstallRecord(2, "test-profile", AnyPS5Mode.Release, "v0.1.2", null, AnyPS5TestSetup.UnwrapScriptUrl, _setup.Time.GetLocalNow()),
```

and add:

```csharp
    [Fact]
    public async Task A_schema_1_record_still_counts_as_installed()
    {
        await _setup.InstallAsync();
        await File.WriteAllTextAsync(_setup.Paths.RecordFile, $$"""
            {
              "schemaVersion": 1, "profile": "test-profile", "mode": "release", "version": "v0.1.2",
              "unwrapScript": "{{AnyPS5TestSetup.UnwrapScriptUrl}}", "installedAt": "2026-10-20T12:00:00+00:00"
            }
            """, TestContext.Current.CancellationToken);

        var state = _setup.Installer.GetState(AnyPS5Mode.Release);

        Assert.Equal(AnyPS5InstallStatus.Installed, state.Status);
        Assert.Equal(AnyPS5Mode.Release, state.Record?.Mode);
        Assert.Null(state.Record?.Commit);
    }

    [Fact]
    public async Task The_current_mode_comes_from_the_record()
    {
        Assert.Equal(AnyPS5Mode.Release, _setup.Installer.CurrentMode);
        await _setup.InstallAsync();
        var record = _setup.Installer.ReadRecord()!;
        await JsonFiles.WriteAsync(_setup.Paths.RecordFile, record with { Mode = AnyPS5Mode.Build }, TestContext.Current.CancellationToken);

        Assert.Equal(AnyPS5Mode.Build, _setup.Installer.CurrentMode);
        Assert.Equal(AnyPS5InstallStatus.Incomplete, _setup.Installer.GetState(AnyPS5Mode.Release).Status);
    }

    [Fact]
    public async Task Installing_another_mode_empties_current_first()
    {
        await _setup.InstallAsync();
        var record = _setup.Installer.ReadRecord()!;
        await JsonFiles.WriteAsync(_setup.Paths.RecordFile, record with { Mode = AnyPS5Mode.Build }, TestContext.Current.CancellationToken);
        TestFiles.Write(Path.Combine(Current, "libs", "libTracyClient.dll"), "left by the other mode");

        await _setup.InstallAsync();

        Assert.False(File.Exists(Path.Combine(Current, "libs", "libTracyClient.dll")));
        Assert.Equal(AnyPS5InstallStatus.Installed, _setup.Installer.GetState(AnyPS5Mode.Release).Status);
    }

    [Fact]
    public void A_mode_without_a_source_is_refused()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => _setup.Installer.CreateSteps(AnyPS5Mode.Build));

        Assert.Contains("build", exception.Message, StringComparison.Ordinal);
    }
```

In the App tests, `FakeInstaller` implements the new interface:

```csharp
    public AnyPS5Mode CurrentMode { get; set; } = AnyPS5Mode.Release;

    public AnyPS5Mode? InstalledMode { get; private set; }

    public AnyPS5InstallRecord? ReadRecord() => State.Record;

    public IReadOnlyList<IStep> CreateSteps(AnyPS5Mode mode) => [.. stepNames.Select(name => new NamedStep(name))];

    public AnyPS5InstallState GetState(AnyPS5Mode mode) => State;

    public Task InstallAsync(AnyPS5Mode mode, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default)
    {
        InstalledMode = mode;
        // existing body of the old InstallAsync
    }
```

Every `new InstallViewModel(installer, …)` in the App tests gets `AnyPS5Mode.Release` as second argument. Records built in App tests use the 7-argument constructor (`Commit: null` after `Version`). Add to `InstallViewModelTests`:

```csharp
    [Fact]
    public async Task Installs_in_the_mode_it_was_created_with()
    {
        var installer = new FakeInstaller("Step");
        var viewModel = CreateViewModel(installer, AnyPS5Mode.Release);

        await viewModel.RunAsync();

        Assert.Equal(AnyPS5Mode.Release, installer.InstalledMode);
    }
```

(adapt to the file's existing helper that builds the view model; if it has none, construct it the way the other tests in the file do).

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build errors on the new members.

- [ ] **Step 3: Implement**

`AnyPS5Mode.cs`:

```csharp
namespace PS5Kitsune.Core.AnyPS5;

/// <summary>How AnyPS5 gets into anyps5\current. See docs/architecture.md, "Getting AnyPS5".</summary>
public enum AnyPS5Mode
{
    /// <summary>Download the release of the profile's tag.</summary>
    Release,

    /// <summary>Compile the profile's commit with MinGit and WinLibs.</summary>
    Build,
}
```

`AnyPS5InstallRecord.cs`:

```csharp
namespace PS5Kitsune.Core.AnyPS5;

/// <summary>Contents of anyps5/current/install.json. Schema 1 (release only, no commit) is still read.</summary>
/// <param name="Version">The release tag, or the first 7 characters of the commit in build mode.</param>
/// <param name="Commit">The commit built; null in release mode.</param>
/// <param name="UnwrapScript">URL of unwrap_self.py in release mode, which holds its pinned commit; null in build mode, where it comes from the clone.</param>
public sealed record AnyPS5InstallRecord(
    int SchemaVersion,
    string Profile,
    AnyPS5Mode Mode,
    string Version,
    string? Commit,
    Uri? UnwrapScript,
    DateTimeOffset InstalledAt)
{
    public const int CurrentSchemaVersion = 2;
}
```

`IAnyPS5Source.cs`:

```csharp
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.AnyPS5;

/// <summary>The steps that fill anyps5\current for one mode, and what install.json says about it.</summary>
public interface IAnyPS5Source
{
    AnyPS5Mode Mode { get; }

    /// <summary>The mode's steps, between "Install Python" and "Record the install".</summary>
    IReadOnlyList<IStep> CreateSteps();

    /// <summary>The record this source's install writes; <paramref name="installedAt"/> is default when comparing.</summary>
    AnyPS5InstallRecord CreateRecord(DateTimeOffset installedAt);
}
```

`ReleaseSource.cs` (the download steps move here unchanged from `AnyPS5Installer`):

```csharp
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5.Steps;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.AnyPS5;

/// <summary>The release of the profile's tag: relinker, libraries, scripts and unwrap_self.py, each checked against the profile's hash.</summary>
public sealed class ReleaseSource(AnyPS5Profile profile, AnyPS5Paths paths, DataLayout layout, IDownloader downloader) : IAnyPS5Source
{
    public AnyPS5Mode Mode => AnyPS5Mode.Release;

    public IReadOnlyList<IStep> CreateSteps()
    {
        var relinker = profile.Asset(ProfileAssetKeys.Relinker);
        var libs = profile.Asset(ProfileAssetKeys.Libs);
        var scripts = profile.Asset(ProfileAssetKeys.Scripts);
        var unwrapScript = profile.Asset(ProfileAssetKeys.UnwrapScript);
        return
        [
            new DownloadFileStep("Download the relinker", profile.AssetUrl(ProfileAssetKeys.Relinker), paths.Relinker, relinker.Sha256, downloader),
            new DownloadArchiveStep(
                "Download the libraries", profile.AssetUrl(ProfileAssetKeys.Libs), ArchivePath(ProfileAssetKeys.Libs, libs),
                libs.Sha256, paths.Libs, ZipExtraction.UnderPrefix("libs/"), downloader),
            new DownloadArchiveStep(
                "Download the scripts", profile.AssetUrl(ProfileAssetKeys.Scripts), ArchivePath(ProfileAssetKeys.Scripts, scripts),
                scripts.Sha256, paths.Scripts, ZipExtraction.StripTopFolder(scripts.Include ?? []), downloader),
            new DownloadFileStep("Download unwrap_self.py", profile.AssetUrl(ProfileAssetKeys.UnwrapScript), paths.UnwrapSelf, unwrapScript.Sha256, downloader),
        ];
    }

    public AnyPS5InstallRecord CreateRecord(DateTimeOffset installedAt) => new(
        AnyPS5InstallRecord.CurrentSchemaVersion,
        profile.Id,
        AnyPS5Mode.Release,
        profile.Source.Version,
        Commit: null,
        profile.AssetUrl(ProfileAssetKeys.UnwrapScript),
        installedAt);

    private string ArchivePath(string key, ProfileAsset asset) =>
        Path.Combine(layout.Downloads, $"anyps5-{key}-{asset.Sha256[..12]}.zip");
}
```

`Steps/InstallToolStep.cs` (replaces `InstallPythonStep.cs`, which is deleted):

```csharp
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.AnyPS5.Steps;

public sealed class InstallToolStep(string name, IPortableTool tool) : IStep
{
    public string Name => name;

    public bool IsAlreadyDone() => tool.IsInstalled();

    public Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken) =>
        tool.InstallAsync(StepProgress.ForDownload(progress), cancellationToken);
}
```

`Steps/WriteInstallRecordStep.cs`:

```csharp
using System.Text.Json;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.AnyPS5.Steps;

/// <summary>Writes install.json last. Done when it describes the source's install, whatever its schema version.</summary>
public sealed class WriteInstallRecordStep(IAnyPS5Source source, AnyPS5Paths paths, TimeProvider time) : IStep
{
    public string Name => "Record the install";

    public bool IsAlreadyDone()
    {
        try
        {
            var expected = source.CreateRecord(default);
            return JsonFiles.Read<AnyPS5InstallRecord>(paths.RecordFile) is { } record
                && record with { InstalledAt = default, SchemaVersion = expected.SchemaVersion } == expected;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken) =>
        JsonFiles.WriteAsync(paths.RecordFile, source.CreateRecord(time.GetLocalNow()), cancellationToken);
}
```

`IAnyPS5Installer.cs`:

```csharp
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.AnyPS5;

/// <summary>Installs AnyPS5 into anyps5/current, in release or build mode. The app's screens use it through this interface.</summary>
public interface IAnyPS5Installer
{
    /// <summary>The mode of install.json, else <see cref="AnyPS5Mode.Release"/>.</summary>
    AnyPS5Mode CurrentMode { get; }

    /// <summary>install.json as found; null when missing or unreadable.</summary>
    AnyPS5InstallRecord? ReadRecord();

    /// <summary>The install steps of <paramref name="mode"/>, in order; their names are shown before the install starts.</summary>
    /// <exception cref="InvalidOperationException">The active profile does not support <paramref name="mode"/>.</exception>
    IReadOnlyList<IStep> CreateSteps(AnyPS5Mode mode);

    /// <summary>Where the install of <paramref name="mode"/> stands, from the steps' done-checks and install.json.</summary>
    AnyPS5InstallState GetState(AnyPS5Mode mode);

    /// <summary>Empties anyps5/current first when install.json records the other mode.</summary>
    /// <exception cref="StepFailedException">A step failed.</exception>
    /// <exception cref="OperationCanceledException">The install was cancelled; it can be resumed.</exception>
    Task InstallAsync(AnyPS5Mode mode, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default);
}
```

`AnyPS5Installer.cs`:

```csharp
using System.Text.Json;
using PS5Kitsune.Core.AnyPS5.Steps;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.AnyPS5;

/// <summary>Installs AnyPS5 into anyps5/current: Python, then the steps of the mode's source, then install.json.</summary>
public sealed class AnyPS5Installer(
    IEnumerable<IAnyPS5Source> sources,
    PythonEmbeddableTool python,
    AnyPS5Paths paths,
    StepRunner stepRunner,
    TimeProvider time) : IAnyPS5Installer
{
    public AnyPS5Mode CurrentMode => ReadRecord()?.Mode ?? AnyPS5Mode.Release;

    public IReadOnlyList<IStep> CreateSteps(AnyPS5Mode mode)
    {
        var source = Source(mode);
        return [new InstallToolStep("Install Python", python), .. source.CreateSteps(), new WriteInstallRecordStep(source, paths, time)];
    }

    /// <summary>True when every step of the current mode is done.</summary>
    public bool IsInstalled() => GetState(CurrentMode).Status == AnyPS5InstallStatus.Installed;

    public AnyPS5InstallState GetState(AnyPS5Mode mode)
    {
        var steps = CreateSteps(mode);
        var done = steps.Count(IsDone);
        var record = ReadRecord();
        var status = done == steps.Count ? AnyPS5InstallStatus.Installed
            : done == 0 && record is null ? AnyPS5InstallStatus.NotInstalled
            : AnyPS5InstallStatus.Incomplete;
        return new AnyPS5InstallState(status, record);
    }

    public AnyPS5InstallRecord? ReadRecord()
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

    public Task InstallAsync(AnyPS5Mode mode, IProgress<StepProgress>? progress = null, Action<StepEvent>? onEvent = null, CancellationToken cancellationToken = default)
    {
        var steps = CreateSteps(mode);

        // The other mode's files must not linger: the release zip has libTracyClient.dll, a build does not.
        if (ReadRecord() is { } record && record.Mode != mode && Directory.Exists(paths.RootDirectory))
        {
            Directory.Delete(paths.RootDirectory, recursive: true);
        }

        return stepRunner.RunAsync(steps, progress, onEvent, cancellationToken);
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

    private IAnyPS5Source Source(AnyPS5Mode mode) =>
        sources.FirstOrDefault(source => source.Mode == mode)
        ?? throw new InvalidOperationException(mode == AnyPS5Mode.Build
            ? "Mode build is not available: the active profile has no build section or no build tools."
            : "Mode release is not available.");
}
```

`ServiceCollectionExtensions`: before `services.AddSingleton<AnyPS5Installer>();` add `services.AddSingleton<IAnyPS5Source, ReleaseSource>();`.

`InstallCommand`: `InstallAsync(AnyPS5Mode.Release, new ConsoleStepProgress(), cancellationToken: cancellationToken)`; the version printed stays `profile.Source.Version` (Task 10 changes it).

App:
- `HomeViewModel.Refresh`: `var installer = session.Get<IAnyPS5Installer>(); var state = installer.GetState(installer.CurrentMode);`
- `PageFactory.Install()`: `new InstallViewModel(Session.Get<IAnyPS5Installer>(), AnyPS5Mode.Release, Session.LogFile, …)` (the Mode screen only offers Release until 5b).
- `PageFactory.Done()`: `Session.Get<IAnyPS5Installer>().ReadRecord()` instead of `GetState().Record`.
- `InstallViewModel`: add the `AnyPS5Mode mode` parameter after `installer`, store it in a `private readonly AnyPS5Mode _mode;`, use `installer.CreateSteps(mode)` in the constructor and `_installer.InstallAsync(_mode, new UiProgress(this), …)` in `RunAsync`.

Fix any other compile error the same way: release behavior must stay identical.

- [ ] **Step 4: Run the tests**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: all pass, 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add -A src tests
git commit -m "Give the installer a mode and write install.json schema 2"
```

---

### Task 6: Build plumbing: paths, markers, commands, parallel jobs

**Files:**
- Modify: `src/PS5Kitsune.Core/DataLayout.cs` (add `AnyPS5`)
- Create: `src/PS5Kitsune.Core/AnyPS5/Build/BuildPaths.cs`
- Create: `src/PS5Kitsune.Core/AnyPS5/Build/BuildMarkers.cs`
- Create: `src/PS5Kitsune.Core/AnyPS5/Build/BuildCommands.cs`
- Create: `src/PS5Kitsune.Core/AnyPS5/Build/BuildCommandStep.cs`
- Create: `src/PS5Kitsune.Core/AnyPS5/Build/ParallelJobs.cs`
- Create: `src/PS5Kitsune.Core/IO/FreeSpace.cs`
- Modify: `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`
- Create: `tests/PS5Kitsune.Core.Tests/Fakes/FakeFreeSpace.cs`
- Create: `tests/PS5Kitsune.Core.Tests/AnyPS5/Build/BuildTestSetup.cs`
- Test: `tests/PS5Kitsune.Core.Tests/AnyPS5/Build/BuildPlumbingTests.cs`

**Interfaces:**
- Consumes: `BuildEnvironment` (Task 3), `NinjaProgress`, `OutputTail.Add`, `StepProgress.Fraction` (Task 4), `AnyPS5Profile.RequireBuild()` (Task 1).
- Produces:
  - `DataLayout.AnyPS5` (`<root>\anyps5`); `AnyPS5Current` becomes `Path.Combine(AnyPS5, "current")`.
  - `BuildPaths(DataLayout layout, IPlatform platform)`: `Root` (`anyps5`), `Source`, `Build`, `State`, `Marker(string name)`, `BuiltRelinker` (`build\core\relinker\relinker.exe`), `BuiltLibs` (`build\core\libs\libs`).
  - `BuildMarkers(BuildPaths paths, AnyPS5Profile profile)`: `string Commit`, `bool IsCurrent(string name)`, `void Clear(string name)`, `Task WriteAsync(string name, CancellationToken)`.
  - `IFreeSpace { long? FreeBytes(string path); }`, `DriveFreeSpace : IFreeSpace`.
  - `CommandFailure(int ExitCode, string Tail, bool SawCertificateError)`.
  - `BuildCommandException(string message) : Exception`.
  - `BuildCommands(IProcessRunner runner, BuildEnvironment environment, BuildPaths paths, IFreeSpace freeSpace)`: `ProcessSpec Git(params string[] arguments)` (working directory `Source`), `ProcessSpec CMake(params string[] arguments)` (working directory `Root`), `Task RunAsync(ProcessSpec spec, Func<CommandFailure, string> describeFailure, IProgress<StepProgress>? progress, CancellationToken cancellationToken)`.
  - `abstract class BuildCommandStep(BuildMarkers markers, string marker) : IStep` with `abstract string Name` and `protected abstract Task RunCommandsAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)`.
  - `ParallelJobs.Compute(int logicalProcessors, long physicalMemoryBytes)`, `ParallelJobs.ForThisMachine()`.
  - DI: `BuildPaths`, `BuildMarkers`, `IFreeSpace`, `BuildCommands` singletons.

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.Core.Tests/Fakes/FakeFreeSpace.cs`:

```csharp
using PS5Kitsune.Core.IO;

namespace PS5Kitsune.Core.Tests.Fakes;

internal sealed class FakeFreeSpace : IFreeSpace
{
    public long? Bytes { get; set; } = 100L << 30;

    public long? FreeBytes(string path) => Bytes;
}
```

`tests/PS5Kitsune.Core.Tests/AnyPS5/Build/BuildTestSetup.cs` (fake tool files written directly; nothing is downloaded):

```csharp
using PS5Kitsune.Core.AnyPS5.Build;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tests.Fakes;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.Tests.AnyPS5.Build;

/// <summary>Build services over a temp root, with MinGit and WinLibs files in place and a fake process runner.</summary>
internal sealed class BuildTestSetup
{
    public const string Commit = "48d61f53b46f1d0726889e4dd39c5ef2b3eb30cd";

    public static readonly Uri Repository = new("https://github.com/boykopovar/AnyPS5.git");

    public BuildTestSetup(string root, string commit = Commit)
    {
        Layout = new DataLayout(root);
        Profile = new AnyPS5Profile
        {
            SchemaVersion = AnyPS5Profile.CurrentSchemaVersion,
            Id = "test-profile",
            Source = new ProfileSource("release", Tag: "v0.1.2"),
            Build = new ProfileBuild(Repository, commit),
        };
        var unused = new ToolDefinition("1", new Uri("https://example.test/unused.zip"), new string('0', 64));
        MinGit = new MinGitTool(unused, Layout, Platform, new FakeDownloader(), Runner);
        WinLibs = new WinLibsTool(unused, Layout, Platform, new FakeDownloader(), Runner);
        var caBundle = Path.Combine(Layout.ToolDirectory(MinGitTool.ToolId), "ucrt64", "etc", "ssl", "certs", "ca-bundle.crt");
        foreach (var file in (string[])[MinGit.Executable, caBundle, WinLibs.Gcc, WinLibs.Gxx, WinLibs.CMake, WinLibs.Ninja, .. WinLibs.RuntimeDlls])
        {
            TestFiles.Write(file, Path.GetFileName(file));
        }

        Environment = new BuildEnvironment(MinGit, WinLibs);
        Paths = new BuildPaths(Layout, Platform);
        Markers = new BuildMarkers(Paths, Profile);
        Commands = new BuildCommands(Runner, Environment, Paths, FreeSpace);
    }

    public DataLayout Layout { get; }

    public FakePlatform Platform { get; } = new();

    public FakeProcessRunner Runner { get; } = new();

    public FakeFreeSpace FreeSpace { get; } = new();

    public AnyPS5Profile Profile { get; }

    public MinGitTool MinGit { get; }

    public WinLibsTool WinLibs { get; }

    public BuildEnvironment Environment { get; }

    public BuildPaths Paths { get; }

    public BuildMarkers Markers { get; }

    public BuildCommands Commands { get; }
}
```

`tests/PS5Kitsune.Core.Tests/AnyPS5/Build/BuildPlumbingTests.cs`:

```csharp
using PS5Kitsune.Core.AnyPS5.Build;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.AnyPS5.Build;

public sealed class BuildPlumbingTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly BuildTestSetup _setup;

    public BuildPlumbingTests()
    {
        _setup = new BuildTestSetup(_temp.Path);
    }

    [Fact]
    public void Build_folders_sit_beside_current()
    {
        Assert.Equal(_temp.Combine("anyps5", "source"), _setup.Paths.Source);
        Assert.Equal(_temp.Combine("anyps5", "build"), _setup.Paths.Build);
        Assert.Equal(_temp.Combine("anyps5", "state", "compile.done"), _setup.Paths.Marker("compile"));
        Assert.Equal(_temp.Combine("anyps5", "build", "core", "relinker", "relinker.exe"), _setup.Paths.BuiltRelinker);
    }

    [Fact]
    public async Task A_marker_is_current_only_for_the_profile_commit()
    {
        Assert.False(_setup.Markers.IsCurrent("compile"));

        await _setup.Markers.WriteAsync("compile", TestContext.Current.CancellationToken);
        Assert.True(_setup.Markers.IsCurrent("compile"));
        Assert.Equal(BuildTestSetup.Commit, File.ReadAllText(_setup.Paths.Marker("compile")));

        var newer = new BuildTestSetup(_temp.Path, new string('a', 40));
        Assert.False(newer.Markers.IsCurrent("compile"));

        _setup.Markers.Clear("compile");
        Assert.False(_setup.Markers.IsCurrent("compile"));
    }

    [Theory]
    [InlineData(16, 32, 16)]
    [InlineData(16, 16, 8)]
    [InlineData(4, 64, 4)]
    [InlineData(8, 1, 1)]
    [InlineData(1, 0, 1)]
    public void Parallel_jobs_follow_cores_and_memory(int cores, int memoryGiB, int expected)
    {
        Assert.Equal(expected, ParallelJobs.Compute(cores, (long)memoryGiB << 30));
    }

    [Fact]
    public async Task Commands_run_with_the_build_environment()
    {
        await _setup.Commands.RunAsync(_setup.Commands.Git("status"), _ => "unused", null, TestContext.Current.CancellationToken);

        var spec = _setup.Runner.LastSpec!;
        Assert.Equal(_setup.MinGit.Executable, spec.FileName);
        Assert.Equal(_setup.Paths.Source, spec.WorkingDirectory);
        Assert.Equal(_setup.Environment.CaBundle, spec.Environment["SSL_CERT_FILE"]);
        Assert.Equal(_setup.Paths.Root, _setup.Commands.CMake("--version").WorkingDirectory);
    }

    [Fact]
    public async Task A_failure_gives_the_message_the_exit_code_and_the_last_lines()
    {
        _setup.Runner.Respond = _ => new FakeProcessResult(1, ["FAILED: core/main.cpp.obj", "main.cpp:1: error: boom"], ["warning: noise"]);

        var exception = await Assert.ThrowsAsync<BuildCommandException>(() =>
            _setup.Commands.RunAsync(_setup.Commands.CMake("--build", "x"), _ => "The build failed.", null, TestContext.Current.CancellationToken));

        Assert.StartsWith("The build failed.", exception.Message, StringComparison.Ordinal);
        Assert.Contains("exited with code 1", exception.Message, StringComparison.Ordinal);
        Assert.Contains("main.cpp:1: error: boom", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_certificate_error_is_flagged()
    {
        _setup.Runner.Respond = _ => new FakeProcessResult(1, StandardError: ["  status_code: 60", "  status_string: \"SSL peer certificate or SSH remote key was not OK\""]);
        CommandFailure? seen = null;

        await Assert.ThrowsAsync<BuildCommandException>(() => _setup.Commands.RunAsync(
            _setup.Commands.CMake("-S", "x"), failure => { seen = failure; return "configure"; }, null, TestContext.Current.CancellationToken));

        Assert.True(seen?.SawCertificateError);
    }

    [Fact]
    public async Task Low_disk_space_replaces_the_message()
    {
        _setup.FreeSpace.Bytes = 200L << 20;
        _setup.Runner.Respond = _ => new FakeProcessResult(1);

        var exception = await Assert.ThrowsAsync<BuildCommandException>(() =>
            _setup.Commands.RunAsync(_setup.Commands.CMake("--build", "x"), _ => "The build failed.", null, TestContext.Current.CancellationToken));

        Assert.StartsWith("Not enough disk space on ", exception.Message, StringComparison.Ordinal);
        Assert.Contains("0.2 GiB left", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ninja_counters_and_error_lines_are_reported()
    {
        _setup.Runner.Respond = _ => new FakeProcessResult(0, ["[1/4] Building CXX a.obj", "[2/4] Building CXX b.obj", "a.cpp:3: error: x", "-- ordinary line"]);
        var reports = new List<StepProgress>();

        await _setup.Commands.RunAsync(_setup.Commands.CMake("--build", "x"), _ => "", new SynchronousProgress(reports.Add), TestContext.Current.CancellationToken);

        Assert.Equal([new StepFraction(1, 4), new StepFraction(2, 4)], reports.Where(report => report.Fraction is not null).Select(report => report.Fraction!.Value));
        Assert.Equal(["a.cpp:3: error: x"], reports.Where(report => report.Message is not null).Select(report => report.Message!));
    }

    [Fact]
    public async Task A_step_writes_its_marker_only_after_success()
    {
        var failing = new ProbeStep(_setup.Markers, () => throw new BuildCommandException("no"));
        await Assert.ThrowsAsync<BuildCommandException>(() => failing.ExecuteAsync(null, TestContext.Current.CancellationToken));
        Assert.False(failing.IsAlreadyDone());

        var working = new ProbeStep(_setup.Markers, () => Task.CompletedTask);
        await working.ExecuteAsync(null, TestContext.Current.CancellationToken);
        Assert.True(working.IsAlreadyDone());
    }

    [Fact]
    public async Task A_step_clears_its_marker_before_running()
    {
        await _setup.Markers.WriteAsync("probe", TestContext.Current.CancellationToken);
        var step = new ProbeStep(_setup.Markers, () => throw new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => step.ExecuteAsync(null, TestContext.Current.CancellationToken));

        Assert.False(step.IsAlreadyDone());
    }

    public void Dispose() => _temp.Dispose();

    private sealed class ProbeStep(BuildMarkers markers, Func<Task> run) : BuildCommandStep(markers, "probe")
    {
        public override string Name => "Probe";

        protected override Task RunCommandsAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken) => run();
    }

    private sealed class SynchronousProgress(Action<StepProgress> report) : IProgress<StepProgress>
    {
        public void Report(StepProgress value) => report(value);
    }
}
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.Core.Tests.AnyPS5.Build.BuildPlumbingTests"`
Expected: build errors.

- [ ] **Step 3: Implement**

`DataLayout`:

```csharp
    /// <summary>The AnyPS5 install and, in build mode, its clone, build folder and step markers.</summary>
    public string AnyPS5 => Path.Combine(Root, "anyps5");

    /// <summary>The AnyPS5 install used to convert games: relinker, libs, scripts.</summary>
    public string AnyPS5Current => Path.Combine(AnyPS5, "current");
```

`src/PS5Kitsune.Core/IO/FreeSpace.cs`:

```csharp
namespace PS5Kitsune.Core.IO;

public interface IFreeSpace
{
    /// <summary>Free bytes on the drive holding <paramref name="path"/>; null when it cannot be read.</summary>
    long? FreeBytes(string path);
}

public sealed class DriveFreeSpace : IFreeSpace
{
    public long? FreeBytes(string path)
    {
        try
        {
            return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path)) ?? path).AvailableFreeSpace;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
```

`Build/BuildPaths.cs`:

```csharp
using PS5Kitsune.Core.Abstractions;

namespace PS5Kitsune.Core.AnyPS5.Build;

/// <summary>Build mode's folders under anyps5\. See the milestone 5a spec, "Layout".</summary>
public sealed class BuildPaths(DataLayout layout, IPlatform platform)
{
    public string Root => layout.AnyPS5;

    /// <summary>The git clone.</summary>
    public string Source => Path.Combine(Root, "source");

    /// <summary>The CMake build folder, outside the clone so a forced checkout never touches it.</summary>
    public string Build => Path.Combine(Root, "build");

    /// <summary>One marker per step, written after the step succeeds.</summary>
    public string State => Path.Combine(Root, "state");

    public string BuiltRelinker => Path.Combine(Build, "core", "relinker", "relinker" + platform.ExecutableSuffix);

    /// <summary>The .prx files of the libs target.</summary>
    public string BuiltLibs => Path.Combine(Build, "core", "libs", "libs");

    public string Marker(string name) => Path.Combine(State, name + ".done");
}
```

`Build/BuildMarkers.cs`:

```csharp
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Core.AnyPS5.Build;

/// <summary>
/// Step markers: a file holding the commit a step completed for. Written after success, so a step a tool
/// left half done is never trusted; a marker for another commit means the step runs again.
/// </summary>
public sealed class BuildMarkers(BuildPaths paths, AnyPS5Profile profile)
{
    /// <exception cref="Profiles.InvalidProfileException">The profile has no build section.</exception>
    public string Commit => profile.RequireBuild().Commit;

    public bool IsCurrent(string name)
    {
        var marker = paths.Marker(name);
        return File.Exists(marker) && File.ReadAllText(marker).Trim() == Commit;
    }

    public void Clear(string name) => File.Delete(paths.Marker(name));

    public async Task WriteAsync(string name, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(paths.State);
        var marker = paths.Marker(name);
        await File.WriteAllTextAsync(marker + ".tmp", Commit, cancellationToken).ConfigureAwait(false);
        File.Move(marker + ".tmp", marker, overwrite: true);
    }
}
```

`Build/BuildCommands.cs`:

```csharp
using System.Globalization;
using System.Text.RegularExpressions;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Processes;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.AnyPS5.Build;

/// <param name="Tail">The last lines of output, Ninja counters excluded.</param>
/// <param name="SawCertificateError">An HTTPS certificate error appeared in the error output (curl error 60 and friends).</param>
public sealed record CommandFailure(int ExitCode, string Tail, bool SawCertificateError);

public sealed class BuildCommandException(string message) : Exception(message);

/// <summary>Runs git and CMake for build mode with the build environment, reports progress, and turns failures into messages.</summary>
public sealed partial class BuildCommands(IProcessRunner runner, BuildEnvironment environment, BuildPaths paths, IFreeSpace freeSpace)
{
    private const long LowDiskBytes = 1L << 30;

    public ProcessSpec Git(params string[] arguments) =>
        new(environment.Git, arguments) { WorkingDirectory = paths.Source, Environment = environment.Variables() };

    public ProcessSpec CMake(params string[] arguments) =>
        new(environment.CMake, arguments) { WorkingDirectory = paths.Root, Environment = environment.Variables() };

    /// <exception cref="BuildCommandException">The command exited with a non-zero code.</exception>
    public async Task RunAsync(ProcessSpec spec, Func<CommandFailure, string> describeFailure, IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(describeFailure);
        var tail = new OutputTail();
        var sawCertificateError = false;
        var result = await runner.RunAsync(spec, line =>
        {
            if (NinjaProgress.TryParse(line.Text, out var fraction))
            {
                progress?.Report(new StepProgress(Fraction: fraction));
                return;
            }

            tail.Add(line.Text);
            if (line.Source == OutputSource.StandardError && CertificateErrorPattern().IsMatch(line.Text))
            {
                sawCertificateError = true;
            }

            if (ErrorLinePattern().IsMatch(line.Text))
            {
                progress?.Report(new StepProgress(Message: line.Text));
            }
        }, cancellationToken).ConfigureAwait(false);

        if (result.ExitCode == 0)
        {
            return;
        }

        var lines = tail.ToString();
        var message = LowDiskMessage() ?? describeFailure(new CommandFailure(result.ExitCode, lines, sawCertificateError));
        throw new BuildCommandException(
            $"{message}{Environment.NewLine}{Path.GetFileNameWithoutExtension(spec.FileName)} exited with code {result.ExitCode}.{Environment.NewLine}{lines}");
    }

    private string? LowDiskMessage()
    {
        if (freeSpace.FreeBytes(paths.Root) is not { } free || free >= LowDiskBytes)
        {
            return null;
        }

        var drive = Path.GetPathRoot(Path.GetFullPath(paths.Root));
        return string.Create(CultureInfo.InvariantCulture, $"Not enough disk space on {drive}: {free / (double)(1L << 30):0.0} GiB left.");
    }

    [GeneratedRegex(@"status_code: (35|60|77)\b|SSL peer certificate|SSL certificate problem|certificate verify failed", RegexOptions.IgnoreCase)]
    private static partial Regex CertificateErrorPattern();

    [GeneratedRegex(@"\berror:|\bfatal:|CMake Error|^FAILED: ")]
    private static partial Regex ErrorLinePattern();
}
```

`Build/BuildCommandStep.cs`:

```csharp
using PS5Kitsune.Core.Pipeline;

namespace PS5Kitsune.Core.AnyPS5.Build;

/// <summary>A build step done when its marker holds the profile's commit. The marker is removed first and written after success.</summary>
public abstract class BuildCommandStep(BuildMarkers markers, string marker) : IStep
{
    public abstract string Name { get; }

    public bool IsAlreadyDone() => markers.IsCurrent(marker);

    public async Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        markers.Clear(marker);
        await RunCommandsAsync(progress, cancellationToken).ConfigureAwait(false);
        await markers.WriteAsync(marker, cancellationToken).ConfigureAwait(false);
    }

    protected abstract Task RunCommandsAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken);
}
```

`Build/ParallelJobs.cs`:

```csharp
namespace PS5Kitsune.Core.AnyPS5.Build;

/// <summary>Jobs for cmake --build: GCC can use about 2 GiB per job on large C++ files, so memory caps the cores.</summary>
public static class ParallelJobs
{
    private const long BytesPerJob = 2L << 30;

    public static int Compute(int logicalProcessors, long physicalMemoryBytes) =>
        Math.Max(1, Math.Min(logicalProcessors, (int)(physicalMemoryBytes / BytesPerJob)));

    public static int ForThisMachine() =>
        Compute(Environment.ProcessorCount, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes);
}
```

DI, after `services.AddSingleton<BuildEnvironment>();`:

```csharp
        services.AddSingleton<IFreeSpace, DriveFreeSpace>();
        services.AddSingleton<BuildPaths>();
        services.AddSingleton<BuildMarkers>();
        services.AddSingleton<BuildCommands>();
```

- [ ] **Step 4: Run the tests**

Run the `BuildPlumbingTests` filter, then the full suite. Expected: all pass, 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Core tests/PS5Kitsune.Core.Tests
git commit -m "Add the build paths, step markers and command runner"
```

---

### Task 7: Source, submodules, configure, compile and libs steps

**Files:**
- Create: `src/PS5Kitsune.Core/AnyPS5/Build/BuildSteps.cs`
- Test: `tests/PS5Kitsune.Core.Tests/AnyPS5/Build/BuildStepsTests.cs`

**Interfaces:**
- Consumes: Task 6 (`BuildCommandStep`, `BuildCommands`, `BuildMarkers`, `BuildPaths`), Task 3 (`BuildEnvironment`).
- Produces (all `sealed`, all `BuildCommandStep`):
  - `GetSourceStep(BuildCommands commands, BuildMarkers markers, BuildPaths paths, ProfileBuild build)`: "Get the AnyPS5 source", marker `checkout`.
  - `SubmodulesStep(BuildCommands commands, BuildMarkers markers)`: "Update the submodules", marker `submodules`.
  - `ConfigureStep(BuildCommands commands, BuildMarkers markers, BuildPaths paths, BuildEnvironment environment)`: "Configure the build", marker `configure`.
  - `CompileStep(BuildCommands commands, BuildMarkers markers, BuildPaths paths, int jobs)`: "Compile AnyPS5", marker `compile`.
  - `LibsStep(BuildCommands commands, BuildMarkers markers, BuildPaths paths, int jobs)`: "Build the libraries", marker `libs`.

- [ ] **Step 1: Write the failing tests**

```csharp
using PS5Kitsune.Core.AnyPS5.Build;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.AnyPS5.Build;

public sealed class BuildStepsTests : IDisposable
{
    private const string Commit = BuildTestSetup.Commit;

    private readonly TempDirectory _temp = new();
    private readonly BuildTestSetup _setup;

    public BuildStepsTests()
    {
        _setup = new BuildTestSetup(_temp.Path);
    }

    private GetSourceStep Source => new(_setup.Commands, _setup.Markers, _setup.Paths, _setup.Profile.Build!);

    private static string Slashes(string path) => path.Replace('\\', '/');

    private List<string[]> Arguments => [.. _setup.Runner.Calls.Select(call => call.Arguments.ToArray())];

    [Fact]
    public async Task Getting_the_source_into_an_empty_folder_fetches_the_commit_only()
    {
        await Source.ExecuteAsync(null, TestContext.Current.CancellationToken);

        string[][] expected =
        [
            ["init"],
            ["config", "core.longpaths", "true"],
            ["config", "remote.origin.url", "https://github.com/boykopovar/AnyPS5.git"],
            ["fetch", "--depth", "1", "origin", Commit],
            ["checkout", "--detach", "--force", Commit],
        ];
        Assert.Equal(expected, Arguments);
        Assert.All(_setup.Runner.Calls, call => Assert.Equal(_setup.Paths.Source, call.WorkingDirectory));
        Assert.True(Source.IsAlreadyDone());
    }

    [Fact]
    public async Task A_new_commit_makes_the_source_stale()
    {
        await Source.ExecuteAsync(null, TestContext.Current.CancellationToken);

        var newer = new BuildTestSetup(_temp.Path, new string('b', 40));

        Assert.False(new GetSourceStep(newer.Commands, newer.Markers, newer.Paths, newer.Profile.Build!).IsAlreadyDone());
    }

    [Fact]
    public async Task A_failed_fetch_says_to_check_the_connection()
    {
        _setup.Runner.Respond = spec => spec.Arguments[0] == "fetch" ? new FakeProcessResult(128, StandardError: ["fatal: unable to access"]) : new FakeProcessResult(0);

        var exception = await Assert.ThrowsAsync<BuildCommandException>(() => Source.ExecuteAsync(null, TestContext.Current.CancellationToken));

        Assert.StartsWith("Cannot download from GitHub: check the connection, then run install again.", exception.Message, StringComparison.Ordinal);
        Assert.False(Source.IsAlreadyDone());
    }

    [Fact]
    public async Task Submodules_are_synced_then_updated_at_depth_1()
    {
        await new SubmodulesStep(_setup.Commands, _setup.Markers).ExecuteAsync(null, TestContext.Current.CancellationToken);

        string[][] expected =
        [
            ["submodule", "sync", "--recursive"],
            ["submodule", "update", "--init", "--recursive", "--depth", "1", "--force"],
        ];
        Assert.Equal(expected, Arguments);
    }

    [Fact]
    public async Task Configure_uses_Ninja_and_the_WinLibs_compilers()
    {
        await new ConfigureStep(_setup.Commands, _setup.Markers, _setup.Paths, _setup.Environment).ExecuteAsync(null, TestContext.Current.CancellationToken);

        var call = Assert.Single(_setup.Runner.Calls);
        Assert.Equal(_setup.WinLibs.CMake, call.FileName);
        Assert.Equal(
            [
                "-S", Slashes(_setup.Paths.Source), "-B", Slashes(_setup.Paths.Build), "-G", "Ninja",
                "-DCMAKE_BUILD_TYPE=Release", "-DBUILD_TESTING=OFF",
                $"-DCMAKE_MAKE_PROGRAM={Slashes(_setup.WinLibs.Ninja)}",
                $"-DCMAKE_C_COMPILER={Slashes(_setup.WinLibs.Gcc)}",
                $"-DCMAKE_CXX_COMPILER={Slashes(_setup.WinLibs.Gxx)}",
            ],
            call.Arguments);
    }

    [Fact]
    public async Task A_configure_certificate_error_names_the_CA_bundle()
    {
        _setup.Runner.Respond = _ => new FakeProcessResult(1, StandardError: ["  status_code: 60"]);

        var exception = await Assert.ThrowsAsync<BuildCommandException>(() =>
            new ConfigureStep(_setup.Commands, _setup.Markers, _setup.Paths, _setup.Environment).ExecuteAsync(null, TestContext.Current.CancellationToken));

        Assert.StartsWith($"CMake could not download a dependency over HTTPS. CA bundle used: {_setup.Environment.CaBundle}.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Another_configure_failure_has_its_own_message()
    {
        _setup.Runner.Respond = _ => new FakeProcessResult(1, StandardError: ["CMake Error at CMakeLists.txt:1"]);

        var exception = await Assert.ThrowsAsync<BuildCommandException>(() =>
            new ConfigureStep(_setup.Commands, _setup.Markers, _setup.Paths, _setup.Environment).ExecuteAsync(null, TestContext.Current.CancellationToken));

        Assert.StartsWith("CMake could not configure the build.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Compile_and_libs_build_with_the_given_jobs()
    {
        await new CompileStep(_setup.Commands, _setup.Markers, _setup.Paths, 6).ExecuteAsync(null, TestContext.Current.CancellationToken);
        await new LibsStep(_setup.Commands, _setup.Markers, _setup.Paths, 6).ExecuteAsync(null, TestContext.Current.CancellationToken);

        string[][] expected =
        [
            ["--build", Slashes(_setup.Paths.Build), "--parallel", "6"],
            ["--build", Slashes(_setup.Paths.Build), "--target", "libs", "--parallel", "6"],
        ];
        Assert.Equal(expected, Arguments);
        Assert.True(_setup.Markers.IsCurrent("compile"));
        Assert.True(_setup.Markers.IsCurrent("libs"));
    }

    [Fact]
    public async Task A_failed_compile_points_to_the_log()
    {
        _setup.Runner.Respond = _ => new FakeProcessResult(1, ["FAILED: a.obj"]);

        var exception = await Assert.ThrowsAsync<BuildCommandException>(() =>
            new CompileStep(_setup.Commands, _setup.Markers, _setup.Paths, 2).ExecuteAsync(null, TestContext.Current.CancellationToken));

        Assert.StartsWith("The build failed. The log holds the full compiler output.", exception.Message, StringComparison.Ordinal);
        Assert.False(_setup.Markers.IsCurrent("compile"));
    }

    public void Dispose() => _temp.Dispose();
}
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release -- --filter-class "PS5Kitsune.Core.Tests.AnyPS5.Build.BuildStepsTests"`
Expected: build errors.

- [ ] **Step 3: Implement** `src/PS5Kitsune.Core/AnyPS5/Build/BuildSteps.cs`:

```csharp
using System.Globalization;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.AnyPS5.Build;

// CMake reads backslashes in -D values as escapes: it gets forward slashes.
file static class CMakePaths
{
    public static string Of(string path) => path.Replace('\\', '/');
}

/// <summary>
/// Fetches only the profile's commit (depth 1, by SHA, which GitHub allows) and checks it out.
/// Every command is idempotent, so a run interrupted anywhere resumes cleanly.
/// </summary>
public sealed class GetSourceStep(BuildCommands commands, BuildMarkers markers, BuildPaths paths, ProfileBuild build)
    : BuildCommandStep(markers, "checkout")
{
    private const string NetworkMessage = "Cannot download from GitHub: check the connection, then run install again.";

    public override string Name => "Get the AnyPS5 source";

    protected override async Task RunCommandsAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(paths.Source);
        var prepare = $"Git could not prepare {paths.Source}.";
        await commands.RunAsync(commands.Git("init"), _ => prepare, progress, cancellationToken).ConfigureAwait(false);
        await commands.RunAsync(commands.Git("config", "core.longpaths", "true"), _ => prepare, progress, cancellationToken).ConfigureAwait(false);
        await commands.RunAsync(commands.Git("config", "remote.origin.url", build.Repository.ToString()), _ => prepare, progress, cancellationToken).ConfigureAwait(false);
        await commands.RunAsync(commands.Git("fetch", "--depth", "1", "origin", build.Commit), _ => NetworkMessage, progress, cancellationToken).ConfigureAwait(false);
        await commands.RunAsync(commands.Git("checkout", "--detach", "--force", build.Commit), _ => $"Git could not check out {build.Commit}.", progress, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Submodules at depth 1, as AnyPS5's CI gets them through actions/checkout.</summary>
public sealed class SubmodulesStep(BuildCommands commands, BuildMarkers markers) : BuildCommandStep(markers, "submodules")
{
    private const string NetworkMessage = "Cannot download from GitHub: check the connection, then run install again.";

    public override string Name => "Update the submodules";

    protected override async Task RunCommandsAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        await commands.RunAsync(commands.Git("submodule", "sync", "--recursive"), _ => NetworkMessage, progress, cancellationToken).ConfigureAwait(false);
        await commands.RunAsync(commands.Git("submodule", "update", "--init", "--recursive", "--depth", "1", "--force"), _ => NetworkMessage, progress, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Configures with Ninja and the WinLibs compilers. BUILD_TESTING stays OFF, so the libs target is built separately.</summary>
public sealed class ConfigureStep(BuildCommands commands, BuildMarkers markers, BuildPaths paths, BuildEnvironment environment)
    : BuildCommandStep(markers, "configure")
{
    public override string Name => "Configure the build";

    protected override Task RunCommandsAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken) =>
        commands.RunAsync(
            commands.CMake(
                "-S", CMakePaths.Of(paths.Source), "-B", CMakePaths.Of(paths.Build), "-G", "Ninja",
                "-DCMAKE_BUILD_TYPE=Release", "-DBUILD_TESTING=OFF",
                $"-DCMAKE_MAKE_PROGRAM={CMakePaths.Of(environment.Ninja)}",
                $"-DCMAKE_C_COMPILER={CMakePaths.Of(environment.Gcc)}",
                $"-DCMAKE_CXX_COMPILER={CMakePaths.Of(environment.Gxx)}"),
            failure => failure.SawCertificateError
                ? $"CMake could not download a dependency over HTTPS. CA bundle used: {environment.CaBundle}."
                : "CMake could not configure the build.",
            progress,
            cancellationToken);
}

/// <summary>Builds the default target. Ninja resumes from what an interrupted run already compiled.</summary>
public sealed class CompileStep(BuildCommands commands, BuildMarkers markers, BuildPaths paths, int jobs) : BuildCommandStep(markers, "compile")
{
    public override string Name => "Compile AnyPS5";

    protected override Task RunCommandsAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken) =>
        commands.RunAsync(
            commands.CMake("--build", CMakePaths.Of(paths.Build), "--parallel", jobs.ToString(CultureInfo.InvariantCulture)),
            _ => "The build failed. The log holds the full compiler output.",
            progress,
            cancellationToken);
}

/// <summary>Builds the patched .prx libraries (target libs).</summary>
public sealed class LibsStep(BuildCommands commands, BuildMarkers markers, BuildPaths paths, int jobs) : BuildCommandStep(markers, "libs")
{
    public override string Name => "Build the libraries";

    protected override Task RunCommandsAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken) =>
        commands.RunAsync(
            commands.CMake("--build", CMakePaths.Of(paths.Build), "--target", "libs", "--parallel", jobs.ToString(CultureInfo.InvariantCulture)),
            _ => "The build failed. The log holds the full compiler output.",
            progress,
            cancellationToken);
}
```

- [ ] **Step 4: Run the tests**

Run the `BuildStepsTests` filter. Expected: all pass, 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Core/AnyPS5/Build tests/PS5Kitsune.Core.Tests/AnyPS5/Build
git commit -m "Add the git and CMake build steps"
```

---

### Task 8: Copy the build, build source, end-to-end install

**Files:**
- Create: `src/PS5Kitsune.Core/AnyPS5/Build/CopyBuildStep.cs`
- Create: `src/PS5Kitsune.Core/AnyPS5/Build/BuildSource.cs`
- Modify: `src/PS5Kitsune.Core/ServiceCollectionExtensions.cs`
- Modify: `tests/PS5Kitsune.Core.Tests/AnyPS5/AnyPS5TestSetup.cs`
- Test: `tests/PS5Kitsune.Core.Tests/AnyPS5/Build/CopyBuildStepTests.cs`, `tests/PS5Kitsune.Core.Tests/AnyPS5/BuildInstallTests.cs`

**Interfaces:**
- Consumes: Tasks 3, 5, 6, 7.
- Produces:
  - `CopyBuildStep(BuildPaths build, AnyPS5Paths current, WinLibsTool winlibs)`: "Copy the build". Done when every planned target exists with the size and SHA-256 of its source (extra files in `current` are ignored by the check; Python may add `__pycache__`). Execution copies, then removes files under `current\libs` and `current\scripts` that are not planned.
  - `BuildSource(AnyPS5Profile profile, MinGitTool mingit, WinLibsTool winlibs, BuildEnvironment environment, BuildPaths paths, BuildMarkers markers, BuildCommands commands, AnyPS5Paths current) : IAnyPS5Source` (mode `Build`).
  - DI: `BuildSource` registered as `IAnyPS5Source` only when `profile.Build` is set and the profile declares `mingit` and `winlibs`.

- [ ] **Step 1: Write the failing tests**

`tests/PS5Kitsune.Core.Tests/AnyPS5/Build/CopyBuildStepTests.cs`:

```csharp
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.AnyPS5.Build;

namespace PS5Kitsune.Core.Tests.AnyPS5.Build;

public sealed class CopyBuildStepTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly BuildTestSetup _setup;
    private readonly AnyPS5Paths _current;

    public CopyBuildStepTests()
    {
        _setup = new BuildTestSetup(_temp.Path);
        _current = new AnyPS5Paths(_setup.Layout, _setup.Platform);
        TestFiles.Write(_setup.Paths.BuiltRelinker, "relinker");
        TestFiles.Write(Path.Combine(_setup.Paths.BuiltLibs, "libSceAudioOut.prx"), "prx");
        TestFiles.Write(Path.Combine(_setup.Paths.BuiltLibs, "CMakeFiles", "ignored.txt"), "not a prx");
        TestFiles.Write(Path.Combine(_setup.Paths.Source, "tools", "import_audit.py"), "audit");
        TestFiles.Write(Path.Combine(_setup.Paths.Source, "tools", "unwrap_self.py"), "unwrap");
        TestFiles.Write(Path.Combine(_setup.Paths.Source, "core", "libs", "prx", "libc", "stub.cpp"), "cpp");
        TestFiles.Write(Path.Combine(_setup.Paths.Source, "docs", "BUILD.md"), "not copied");
    }

    private CopyBuildStep Step => new(_setup.Paths, _current, _setup.WinLibs);

    [Fact]
    public async Task Copies_the_relinker_libraries_DLLs_and_scripts()
    {
        Assert.False(Step.IsAlreadyDone());

        await Step.ExecuteAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal("relinker", File.ReadAllText(_current.Relinker));
        Assert.True(File.Exists(Path.Combine(_current.Libs, "libSceAudioOut.prx")));
        Assert.True(File.Exists(Path.Combine(_current.Libs, "libstdc++-6.dll")));
        Assert.False(File.Exists(Path.Combine(_current.Libs, "ignored.txt")));
        Assert.True(File.Exists(_current.ImportAudit));
        Assert.True(File.Exists(_current.UnwrapSelf));
        Assert.True(File.Exists(Path.Combine(_current.Scripts, "core", "libs", "prx", "libc", "stub.cpp")));
        Assert.False(Directory.Exists(Path.Combine(_current.Scripts, "docs")));
        Assert.True(Step.IsAlreadyDone());
    }

    [Fact]
    public async Task Stray_files_are_removed_and_extra_files_do_not_undo_the_step()
    {
        TestFiles.Write(Path.Combine(_current.Libs, "libTracyClient.dll"), "from a release");
        await Step.ExecuteAsync(null, TestContext.Current.CancellationToken);
        Assert.False(File.Exists(Path.Combine(_current.Libs, "libTracyClient.dll")));

        TestFiles.Write(Path.Combine(_current.Scripts, "tools", "__pycache__", "x.pyc"), "cache");

        Assert.True(Step.IsAlreadyDone());
    }

    [Fact]
    public async Task A_rebuilt_relinker_is_copied_again()
    {
        await Step.ExecuteAsync(null, TestContext.Current.CancellationToken);

        TestFiles.Write(_setup.Paths.BuiltRelinker, "relinker v2");

        Assert.False(Step.IsAlreadyDone());
    }

    [Fact]
    public async Task A_missing_relinker_is_reported()
    {
        File.Delete(_setup.Paths.BuiltRelinker);

        var exception = await Assert.ThrowsAsync<FileNotFoundException>(() => Step.ExecuteAsync(null, TestContext.Current.CancellationToken));

        Assert.Contains("relinker", exception.Message, StringComparison.Ordinal);
    }

    public void Dispose() => _temp.Dispose();
}
```

`AnyPS5TestSetup` gains the build side. Add these fields:

```csharp
    public const string Commit = "48d61f53b46f1d0726889e4dd39c5ef2b3eb30cd";

    public static readonly byte[] MinGitArchive = TestFiles.ZipBytes(
        ("cmd/git.exe", "git"u8.ToArray()),
        ("ucrt64/etc/ssl/certs/ca-bundle.crt", "certs"u8.ToArray()));

    public static readonly byte[] WinLibsArchive = TestFiles.ZipBytes(
        ("mingw64/bin/gcc.exe", "gcc"u8.ToArray()),
        ("mingw64/bin/g++.exe", "g++"u8.ToArray()),
        ("mingw64/bin/cmake.exe", "cmake"u8.ToArray()),
        ("mingw64/bin/ninja.exe", "ninja"u8.ToArray()),
        ("mingw64/bin/libgcc_s_seh-1.dll", "dll"u8.ToArray()),
        ("mingw64/bin/libstdc++-6.dll", "dll"u8.ToArray()),
        ("mingw64/bin/libwinpthread-1.dll", "dll"u8.ToArray()));

    private static readonly Uri MinGitUrl = new("https://example.test/mingit.zip");

    private static readonly Uri WinLibsUrl = new("https://example.test/winlibs.zip");
```

give the constructor a `string commit = Commit` parameter; in the profile add `Build = new ProfileBuild(new Uri("https://github.com/boykopovar/AnyPS5.git"), commit),` and the two tools:

```csharp
                [MinGitTool.ToolId] = new("2.56.0.2", MinGitUrl, TestFiles.Sha256(MinGitArchive)),
                [WinLibsTool.ToolId] = new("15.2.0-r7", WinLibsUrl, TestFiles.Sha256(WinLibsArchive)),
```

serve both archives, and build the installer with both sources:

```csharp
        Downloader.Serve(MinGitUrl, MinGitArchive);
        Downloader.Serve(WinLibsUrl, WinLibsArchive);

        PythonTool = new PythonEmbeddableTool(Profile.Tools[PythonEmbeddableTool.ToolId], Layout, Platform, Downloader, Runner);
        var mingit = new MinGitTool(Profile.Tools[MinGitTool.ToolId], Layout, Platform, Downloader, Runner);
        var winlibs = new WinLibsTool(Profile.Tools[WinLibsTool.ToolId], Layout, Platform, Downloader, Runner);
        var environment = new BuildEnvironment(mingit, winlibs);
        Paths = new AnyPS5Paths(Layout, Platform);
        BuildPaths = new BuildPaths(Layout, Platform);
        var markers = new BuildMarkers(BuildPaths, Profile);
        var commands = new BuildCommands(Runner, environment, BuildPaths, new FakeFreeSpace());
        ReleaseSource = new ReleaseSource(Profile, Paths, Layout, Downloader);
        BuildSource = new BuildSource(Profile, mingit, winlibs, environment, BuildPaths, markers, commands, Paths);
        Installer = new AnyPS5Installer([ReleaseSource, BuildSource], PythonTool, Paths, StepRunner, Time);
```

with `public BuildPaths BuildPaths { get; }` and `public BuildSource BuildSource { get; }`, plus a helper that plays the build tools:

```csharp
    /// <summary>Makes the fake process runner produce what CMake would: the relinker after the build, the .prx after libs, and the scripts after checkout.</summary>
    public void PlayTheBuild(string relinker = "built relinker")
    {
        Runner.Respond = spec =>
        {
            if (spec.Arguments is ["checkout", ..])
            {
                TestFiles.Write(Path.Combine(BuildPaths.Source, "tools", "import_audit.py"), "audit");
                TestFiles.Write(Path.Combine(BuildPaths.Source, "tools", "unwrap_self.py"), "unwrap");
                TestFiles.Write(Path.Combine(BuildPaths.Source, "core", "libs", "prx", "libc", "stub.cpp"), "cpp");
            }
            else if (spec.Arguments is ["--build", _, "--parallel", _])
            {
                TestFiles.Write(BuildPaths.BuiltRelinker, relinker);
            }
            else if (spec.Arguments is ["--build", _, "--target", "libs", ..])
            {
                TestFiles.Write(Path.Combine(BuildPaths.BuiltLibs, "libSceAudioOut.prx"), "prx");
            }

            return new FakeProcessResult(0);
        };
    }
```

`tests/PS5Kitsune.Core.Tests/AnyPS5/BuildInstallTests.cs`:

```csharp
using PS5Kitsune.Core.AnyPS5;

namespace PS5Kitsune.Core.Tests.AnyPS5;

public sealed class BuildInstallTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly AnyPS5TestSetup _setup;

    public BuildInstallTests()
    {
        _setup = new AnyPS5TestSetup(_temp.Path);
        _setup.PlayTheBuild();
    }

    [Fact]
    public void Build_mode_lists_its_steps_in_order()
    {
        Assert.Equal(
            [
                "Install Python", "Install MinGit", "Install WinLibs", "Get the AnyPS5 source", "Update the submodules",
                "Configure the build", "Compile AnyPS5", "Build the libraries", "Copy the build", "Record the install",
            ],
            _setup.Installer.CreateSteps(AnyPS5Mode.Build).Select(step => step.Name));
    }

    [Fact]
    public async Task A_build_install_fills_current_and_records_the_commit()
    {
        await _setup.InstallAsync(AnyPS5Mode.Build);

        Assert.Equal("built relinker", File.ReadAllText(_setup.Paths.Relinker));
        Assert.True(File.Exists(Path.Combine(_setup.Paths.Libs, "libSceAudioOut.prx")));
        Assert.True(File.Exists(_setup.Paths.UnwrapSelf));
        Assert.Equal(
            new AnyPS5InstallRecord(2, "test-profile", AnyPS5Mode.Build, "48d61f5", AnyPS5TestSetup.Commit, null, _setup.Time.GetLocalNow()),
            _setup.Installer.ReadRecord());
        Assert.Equal(AnyPS5InstallStatus.Installed, _setup.Installer.GetState(AnyPS5Mode.Build).Status);
        Assert.Equal(AnyPS5Mode.Build, _setup.Installer.CurrentMode);
    }

    [Fact]
    public async Task A_second_build_install_runs_nothing()
    {
        await _setup.InstallAsync(AnyPS5Mode.Build);
        var calls = _setup.Runner.Calls.Count;

        await _setup.InstallAsync(AnyPS5Mode.Build);

        Assert.Equal(calls, _setup.Runner.Calls.Count);
    }

    [Fact]
    public async Task A_new_commit_rebuilds_without_reinstalling_the_tools()
    {
        await _setup.InstallAsync(AnyPS5Mode.Build);
        var downloads = _setup.Downloader.Requests.Count;

        var updated = new AnyPS5TestSetup(_temp.Path, new string('c', 40));
        updated.PlayTheBuild("relinker of the new commit");
        await updated.InstallAsync(AnyPS5Mode.Build);

        Assert.Empty(updated.Downloader.Requests);
        Assert.Contains(updated.Runner.Calls, call => call.Arguments is ["fetch", "--depth", "1", "origin", var sha] && sha == new string('c', 40));
        Assert.Equal("relinker of the new commit", File.ReadAllText(updated.Paths.Relinker));
        Assert.Equal(new string('c', 40), updated.Installer.ReadRecord()?.Commit);
        Assert.True(downloads > 0);
    }

    [Fact]
    public async Task Going_back_to_release_replaces_current_and_keeps_the_build_folders()
    {
        await _setup.InstallAsync(AnyPS5Mode.Build);

        await _setup.InstallAsync(AnyPS5Mode.Release);

        Assert.Equal(AnyPS5TestSetup.RelinkerContent, File.ReadAllBytes(_setup.Paths.Relinker));
        Assert.False(File.Exists(Path.Combine(_setup.Paths.Libs, "libstdc++-6.dll")));
        Assert.True(File.Exists(_setup.BuildPaths.BuiltRelinker));
        Assert.Equal(AnyPS5Mode.Release, _setup.Installer.CurrentMode);
    }

    public void Dispose() => _temp.Dispose();
}
```

`A_new_commit_rebuilds_without_reinstalling_the_tools` uses a second `AnyPS5TestSetup` over the same root: its `FakeDownloader` is new, so `Assert.Empty(updated.Downloader.Requests)` proves no tool was downloaded again.

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build errors (`CopyBuildStep`, `BuildSource`).

- [ ] **Step 3: Implement**

`Build/CopyBuildStep.cs`:

```csharp
using PS5Kitsune.Core.IO;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.AnyPS5.Build;

/// <summary>
/// Copies the build into anyps5\current so it matches a release install: relinker, libs (.prx + the 3 WinLibs DLLs)
/// and the scripts of the same commit. libs and scripts are mirrored on execution; the done-check ignores extra files,
/// such as the __pycache__ Python writes beside the scripts.
/// </summary>
public sealed class CopyBuildStep(BuildPaths build, AnyPS5Paths current, WinLibsTool winlibs) : IStep
{
    public string Name => "Copy the build";

    public bool IsAlreadyDone() =>
        Plan().All(copy => File.Exists(copy.Source) && FileTree.SameSize(copy.Source, copy.Target) && FileHash.Sha256(copy.Source) == FileHash.Sha256(copy.Target));

    public Task ExecuteAsync(IProgress<StepProgress>? progress, CancellationToken cancellationToken)
    {
        var plan = Plan();
        foreach (var (source, _) in plan.Where(copy => !File.Exists(copy.Source)))
        {
            throw new FileNotFoundException($"The build did not produce {source}.", source);
        }

        foreach (var (source, target) in plan)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: true);
        }

        var planned = plan.Select(copy => Path.GetFullPath(copy.Target)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var root in (string[])[current.Libs, current.Scripts])
        {
            foreach (var stray in FileTree.RelativeFiles(root).Select(relative => Path.GetFullPath(Path.Combine(root, relative))).Where(path => !planned.Contains(path)).ToList())
            {
                File.Delete(stray);
            }
        }

        return Task.CompletedTask;
    }

    private List<(string Source, string Target)> Plan()
    {
        var plan = new List<(string Source, string Target)> { (build.BuiltRelinker, current.Relinker) };
        if (Directory.Exists(build.BuiltLibs))
        {
            plan.AddRange(Directory.EnumerateFiles(build.BuiltLibs, "*.prx").Select(prx => (prx, Path.Combine(current.Libs, Path.GetFileName(prx)))));
        }

        plan.AddRange(winlibs.RuntimeDlls.Select(dll => (dll, Path.Combine(current.Libs, Path.GetFileName(dll)))));
        foreach (var folder in (string[])["tools", Path.Combine("core", "libs", "prx")])
        {
            var sourceRoot = Path.Combine(build.Source, folder);
            plan.AddRange(FileTree.RelativeFiles(sourceRoot).Select(relative => (Path.Combine(sourceRoot, relative), Path.Combine(current.Scripts, folder, relative))));
        }

        return plan;
    }
}
```

`Build/BuildSource.cs`:

```csharp
using PS5Kitsune.Core.AnyPS5.Steps;
using PS5Kitsune.Core.Pipeline;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tools;

namespace PS5Kitsune.Core.AnyPS5.Build;

/// <summary>Build mode: MinGit and WinLibs, the profile's commit, configure, compile, libs, then a copy into anyps5\current.</summary>
public sealed class BuildSource(
    AnyPS5Profile profile,
    MinGitTool mingit,
    WinLibsTool winlibs,
    BuildEnvironment environment,
    BuildPaths paths,
    BuildMarkers markers,
    BuildCommands commands,
    AnyPS5Paths current) : IAnyPS5Source
{
    private const int ShortCommitLength = 7;

    public AnyPS5Mode Mode => AnyPS5Mode.Build;

    public IReadOnlyList<IStep> CreateSteps()
    {
        var build = profile.RequireBuild();
        var jobs = ParallelJobs.ForThisMachine();
        return
        [
            new InstallToolStep("Install MinGit", mingit),
            new InstallToolStep("Install WinLibs", winlibs),
            new GetSourceStep(commands, markers, paths, build),
            new SubmodulesStep(commands, markers),
            new ConfigureStep(commands, markers, paths, environment),
            new CompileStep(commands, markers, paths, jobs),
            new LibsStep(commands, markers, paths, jobs),
            new CopyBuildStep(paths, current, winlibs),
        ];
    }

    public AnyPS5InstallRecord CreateRecord(DateTimeOffset installedAt)
    {
        var commit = profile.RequireBuild().Commit;
        return new AnyPS5InstallRecord(
            AnyPS5InstallRecord.CurrentSchemaVersion,
            profile.Id,
            AnyPS5Mode.Build,
            commit[..ShortCommitLength],
            commit,
            UnwrapScript: null,
            installedAt);
    }
}
```

DI, after `services.AddSingleton<IAnyPS5Source, ReleaseSource>();`:

```csharp
        if (profile.Build is not null && profile.Tools.ContainsKey(MinGitTool.ToolId) && profile.Tools.ContainsKey(WinLibsTool.ToolId))
        {
            services.AddSingleton<IAnyPS5Source, BuildSource>();
        }
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: all pass, 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Core tests/PS5Kitsune.Core.Tests
git commit -m "Copy the build into current and register build mode"
```

---

### Task 9: Disk space threshold per mode

**Files:**
- Modify: `src/PS5Kitsune.Core/Checks/SystemFacts.cs`, `SystemChecker.cs`, `StorageChecks.cs`
- Modify: `src/PS5Kitsune.Cli/CheckCommand.cs`, `src/PS5Kitsune.Cli/InstallCommand.cs`, `src/PS5Kitsune.App/ViewModels/SystemCheckViewModel.cs`
- Test: `tests/PS5Kitsune.Core.Tests/Checks/*` (the disk check tests and any `ISystemChecker` fake), `tests/PS5Kitsune.App.Tests/SystemCheckViewModelTests.cs`

**Interfaces:**
- Produces: `SystemFacts.Mode` (`AnyPS5Mode`, default `Release`), `SystemFactsCollector.CollectAsync(AnyPS5Mode mode, CancellationToken cancellationToken = default)`, `ISystemChecker.RunAsync(AnyPS5Mode mode, CancellationToken cancellationToken = default)`.

- [ ] **Step 1: Write the failing test**

In the test class covering `DiskSpaceCheck` (find it with `grep -rn "DiskSpaceCheck" tests`), add, building facts the way the other tests of that file do:

```csharp
    [Fact]
    public void Build_mode_needs_the_build_threshold()
    {
        var facts = Facts() with
        {
            Mode = AnyPS5Mode.Build,
            FreeDiskBytes = 10L << 30,
            Limits = new CheckThresholds { MinFreeDiskGiB = 5, BuildMinFreeDiskGiB = 15 },
        };

        var result = new DiskSpaceCheck().Evaluate(facts);

        Assert.Equal(CheckStatus.Warning, result.Status);
        Assert.Contains("15 GiB", result.Summary, StringComparison.Ordinal);
        Assert.Contains("built from source", result.Summary, StringComparison.Ordinal);
    }
```

(`Facts()` stands for that file's existing facts builder; use its real name.)

- [ ] **Step 2: Run it and see it fail**

Run the filter for that class. Expected: build error (`Mode` doesn't exist).

- [ ] **Step 3: Implement**

`SystemFacts`: add

```csharp
    /// <summary>The install mode the check is for: build mode needs more disk space.</summary>
    public AnyPS5Mode Mode { get; init; } = AnyPS5Mode.Release;
```

`SystemFactsCollector.CollectAsync(AnyPS5Mode mode, CancellationToken cancellationToken = default)` sets `Mode = mode`. `ISystemChecker.RunAsync(AnyPS5Mode mode, CancellationToken cancellationToken = default)` and `SystemChecker.RunAsync` pass it to the collector.

`DiskSpaceCheck.Evaluate`:

```csharp
        var build = facts.Mode == AnyPS5Mode.Build;
        var needed = build ? facts.Limits.BuildMinFreeDiskGiB : facts.Limits.MinFreeDiskGiB;
        var what = build ? "AnyPS5 built from source and a game" : "AnyPS5 and a game";
        return free >= needed
            ? Result(CheckStatus.Ok, string.Create(CultureInfo.InvariantCulture, $"{shown:0.0} GiB free on the data folder's drive."))
            : Result(
                CheckStatus.Warning,
                string.Create(CultureInfo.InvariantCulture, $"{shown:0.0} GiB free on the data folder's drive; {what} need about {needed} GiB."),
                "Free some space, or choose a data folder on another drive.");
```

Callers pass `AnyPS5Mode.Release`: `CheckCommand`, `InstallCommand` (Task 10 passes the chosen mode), `SystemCheckViewModel`. Update `ISystemChecker` fakes in the tests to the new signature.

- [ ] **Step 4: Run the tests**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release`
Expected: all pass, 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add -A src tests
git commit -m "Use the build disk threshold in build mode"
```

---

### Task 10: CLI install mode, opening lines and active profile

**Files:**
- Create: `src/PS5Kitsune.Core/AnyPS5/InstallModes.cs`
- Modify: `src/PS5Kitsune.Core/Settings/DataRoots.cs` (add `Describe`)
- Create: `src/PS5Kitsune.Cli/CommandContext.cs`
- Modify: `src/PS5Kitsune.Cli/CommandHost.cs`, `InstallCommand.cs`, `CliLog.cs`
- Modify: `src/PS5Kitsune.App/App.axaml.cs`
- Test: `tests/PS5Kitsune.Core.Tests/AnyPS5/InstallModesTests.cs`, `tests/PS5Kitsune.Core.Tests/Settings/DataRootsTests.cs` (create if absent)

**Interfaces:**
- Produces:
  - `InstallModeChoice(AnyPS5Mode Mode, string Source)`; `InstallModes.Resolve(AnyPS5Mode? option, AnyPS5InstallRecord? record)` (sources `--mode`, `current install`, `default`); `InstallModes.Name(AnyPS5Mode mode)` (`release`, `build`).
  - `DataRoots.Describe(string? cliRoot, AppSettings settings)` (`--root`, `saved setting`, `default`), consistent with `Resolve`.
  - `CommandContext(string DataRootSource, ProfileOrigin ProfileOrigin)` registered by `CommandHost`.

- [ ] **Step 1: Write the failing tests**

`InstallModesTests.cs`:

```csharp
using PS5Kitsune.Core.AnyPS5;

namespace PS5Kitsune.Core.Tests.AnyPS5;

public sealed class InstallModesTests
{
    private static readonly AnyPS5InstallRecord BuildRecord =
        new(2, "p", AnyPS5Mode.Build, "48d61f5", new string('a', 40), null, DateTimeOffset.UnixEpoch);

    [Fact]
    public void The_option_wins()
    {
        Assert.Equal(new InstallModeChoice(AnyPS5Mode.Release, "--mode"), InstallModes.Resolve(AnyPS5Mode.Release, BuildRecord));
    }

    [Fact]
    public void Without_the_option_the_current_install_decides()
    {
        Assert.Equal(new InstallModeChoice(AnyPS5Mode.Build, "current install"), InstallModes.Resolve(null, BuildRecord));
    }

    [Fact]
    public void Without_option_nor_install_release_is_the_default()
    {
        Assert.Equal(new InstallModeChoice(AnyPS5Mode.Release, "default"), InstallModes.Resolve(null, null));
    }

    [Theory]
    [InlineData(AnyPS5Mode.Release, "release")]
    [InlineData(AnyPS5Mode.Build, "build")]
    public void Modes_have_command_line_names(AnyPS5Mode mode, string name)
    {
        Assert.Equal(name, InstallModes.Name(mode));
    }
}
```

`DataRootsTests.cs` (add these to the existing file if one exists):

```csharp
using PS5Kitsune.Core.Settings;

namespace PS5Kitsune.Core.Tests.Settings;

public sealed class DataRootsDescribeTests
{
    [Fact]
    public void Describes_where_the_root_comes_from()
    {
        var saved = AppSettings.Default with { DataRoot = @"D:\Saved" };

        Assert.Equal("--root", DataRoots.Describe(@"E:\Cli", saved));
        Assert.Equal("saved setting", DataRoots.Describe(null, saved));
        Assert.Equal("default", DataRoots.Describe(null, AppSettings.Default));
        Assert.Equal("default", DataRoots.Describe(null, AppSettings.Default with { DataRoot = "  " }));
    }
}
```

- [ ] **Step 2: Run them and see them fail**

Run the two filters. Expected: build errors.

- [ ] **Step 3: Implement**

`InstallModes.cs`:

```csharp
namespace PS5Kitsune.Core.AnyPS5;

/// <param name="Source">Where the mode came from, shown on the "Mode:" line: "--mode", "current install" or "default".</param>
public sealed record InstallModeChoice(AnyPS5Mode Mode, string Source);

public static class InstallModes
{
    /// <summary>The --mode option, else the mode of the current install, else release; so a plain 'install' updates a build.</summary>
    public static InstallModeChoice Resolve(AnyPS5Mode? option, AnyPS5InstallRecord? record) =>
        option is { } chosen ? new InstallModeChoice(chosen, "--mode")
        : record is not null ? new InstallModeChoice(record.Mode, "current install")
        : new InstallModeChoice(AnyPS5Mode.Release, "default");

    public static string Name(AnyPS5Mode mode) => mode switch
    {
        AnyPS5Mode.Release => "release",
        AnyPS5Mode.Build => "build",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };
}
```

`DataRoots.Describe`:

```csharp
    /// <summary>Where <see cref="Resolve"/> takes the root from: "--root", "saved setting" or "default".</summary>
    public static string Describe(string? cliRoot, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return cliRoot is not null ? "--root" : string.IsNullOrWhiteSpace(settings.DataRoot) ? "default" : "saved setting";
    }
```

`src/PS5Kitsune.Cli/CommandContext.cs`:

```csharp
using PS5Kitsune.Core.Profiles;

namespace PS5Kitsune.Cli;

/// <summary>How the command's data root and profile were chosen, for the lines 'install' prints first.</summary>
internal sealed record CommandContext(string DataRootSource, ProfileOrigin ProfileOrigin);
```

`CommandHost.RunAsync`: replace `ProfileLoader.LoadEmbedded()` with the active profile and register the context:

```csharp
        var active = ProfileLoader.LoadActive(layout);
        services.AddPlatform(platform).AddPS5KitsuneCore(layout, active.Profile);
        services.AddSingleton(new CommandContext(DataRoots.Describe(rootOverride?.FullName, settings.Settings), active.Origin));
```

and after the `SettingsIgnored` warning:

```csharp
        if (active.Problem is { } profileProblem)
        {
            logger.ProfileIgnored(profileProblem);
        }
```

`CliLog` additions:

```csharp
    [LoggerMessage(Level = LogLevel.Warning, Message = "{Problem}")]
    public static partial void ProfileIgnored(this ILogger logger, string problem);

    [LoggerMessage(Level = LogLevel.Information, Message = "Data root: {Root} ({Source})")]
    public static partial void DataRootUsed(this ILogger logger, string root, string source);

    [LoggerMessage(Level = LogLevel.Information, Message = "Profile: {Profile} ({Origin})")]
    public static partial void ProfileUsed(this ILogger logger, string profile, string origin);

    [LoggerMessage(Level = LogLevel.Information, Message = "Mode: {Mode} ({Source})")]
    public static partial void ModeUsed(this ILogger logger, string mode, string source);
```

`InstallCommand.Create`:

```csharp
    public static Command Create(Option<DirectoryInfo?> rootOption)
    {
        var modeOption = new Option<AnyPS5Mode?>("--mode")
        {
            Description = "release: download the release. build: compile the profile's commit. Defaults to the mode of the current install, else release.",
        };
        var command = new Command("install", "Install AnyPS5 from the active profile: Python, relinker, libraries and scripts.") { modeOption };
        command.SetAction((parseResult, cancellationToken) => CommandHost.RunAsync(parseResult.GetValue(rootOption), "install", async (services, logger) =>
        {
            var installer = services.GetRequiredService<AnyPS5Installer>();
            var profile = services.GetRequiredService<AnyPS5Profile>();
            var context = services.GetRequiredService<CommandContext>();
            var choice = InstallModes.Resolve(parseResult.GetValue(modeOption), installer.ReadRecord());
            logger.DataRootUsed(services.GetRequiredService<DataLayout>().Root, context.DataRootSource);
            logger.ProfileUsed(profile.Id, context.ProfileOrigin == ProfileOrigin.Override ? "override" : "embedded");
            logger.ModeUsed(InstallModes.Name(choice.Mode), choice.Source);

            var report = await services.GetRequiredService<ISystemChecker>().RunAsync(choice.Mode, cancellationToken);
            CheckReportPrinter.Print(logger, report, onlyProblems: true);
            if (report.HasBlocking)
            {
                logger.InstallBlocked();
                return CheckReportPrinter.BlockingExitCode;
            }

            await installer.InstallAsync(choice.Mode, new ConsoleStepProgress(), cancellationToken: cancellationToken);
            var version = installer.ReadRecord()?.Version ?? profile.Source.Version;
            logger.AnyPS5Ready(version, services.GetRequiredService<AnyPS5Paths>().RootDirectory);
            return 0;
        }));
        return command;
    }
```

(`using PS5Kitsune.Core;` for `DataLayout`.) System.CommandLine parses enum values case-insensitively, so `--mode build` works; check it with `ps5kitsune install --help` and `--mode nope` (an error listing `Release` and `Build`).

`App.axaml.cs`: drop `var profile = ProfileLoader.LoadEmbedded();` and create sessions with the root's active profile:

```csharp
            root => RootSession.Create(root, platform, ProfileLoader.LoadActive(new DataLayout(root)).Profile),
```

(the app does not show an ignored override yet: that is 5b).

- [ ] **Step 4: Run the tests and the CLI**

Run: `dotnet test --solution PS5Kitsune.slnx -c Release` (all pass, 0 warnings), then with an empty temporary root the user does not use:

```bash
dotnet run --project src/PS5Kitsune.Cli -c Release -- install --help
dotnet run --project src/PS5Kitsune.Cli -c Release -- install --root "C:\PS5KTest5a" --mode nope
```

Expected: the help lists `--mode`; the second command fails on the option value without installing anything. Do not run a real install here: the build runs at the criterion.

- [ ] **Step 5: Commit**

```bash
git add -A src tests
git commit -m "Add install --mode and print the root, profile and mode in use"
```

---

### Task 11: Network integration test and docs

**Files:**
- Create: `tests/PS5Kitsune.Core.Tests/AnyPS5/Build/RealSourceTests.cs`
- Modify: `docs/architecture.md`, `README.md`
- Modify: `docs/superpowers/specs/2026-10-21-milestone-5a-build-mode-design.md` (refinements section)

**Interfaces:**
- Consumes: everything above. Produces nothing new in code.

- [ ] **Step 1: Write the explicit integration test**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PS5Kitsune.Core.Abstractions;
using PS5Kitsune.Core.AnyPS5;
using PS5Kitsune.Core.Profiles;
using PS5Kitsune.Core.Tests.Fakes;

namespace PS5Kitsune.Core.Tests.AnyPS5.Build;

/// <summary>
/// Downloads MinGit and fetches the real AnyPS5 commit and submodules (a few hundred MB).
/// Explicit: run it with -- --filter-class "PS5Kitsune.Core.Tests.AnyPS5.Build.RealSourceTests" --explicit on.
/// </summary>
public sealed class RealSourceTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact(Explicit = true)]
    [Trait("Category", "Network")]
    public async Task The_profile_commit_and_its_submodules_can_be_fetched()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("MinGit is Windows only.");
        }

        var services = new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton<IPlatform>(new FakePlatform())
            .AddPS5KitsuneCore(new DataLayout(_temp.Path), ProfileLoader.LoadEmbedded())
            .BuildServiceProvider();
        var steps = services.GetServices<IAnyPS5Source>().Single(source => source.Mode == AnyPS5Mode.Build).CreateSteps();
        var token = TestContext.Current.CancellationToken;

        // Install MinGit, get the source, update the submodules: WinLibs is not needed for git.
        foreach (var step in new[] { steps[0], steps[2], steps[3] })
        {
            await step.ExecuteAsync(null, token);
            Assert.True(step.IsAlreadyDone(), step.Name);
        }

        var lines = new List<string>();
        var commands = services.GetRequiredService<PS5Kitsune.Core.AnyPS5.Build.BuildCommands>();
        await services.GetRequiredService<IProcessRunner>().RunAsync(commands.Git("submodule", "status", "--recursive"), line => lines.Add(line.Text), token);
        Assert.NotEmpty(lines);
        Assert.All(lines, line => Assert.False(line.StartsWith('-') || line.StartsWith('+'), line));
    }

    public void Dispose() => _temp.Dispose();
}
```

`ExecutableSuffix` of `FakePlatform` is `.exe`, so the paths match Windows. Check the default run ignores it: `dotnet test --solution PS5Kitsune.slnx -c Release` (same count as before plus 0 run from this class). Do not run it with `--explicit on` in the task: the criterion run covers the network, and the user can run it on demand.

- [ ] **Step 2: Update the docs**

`docs/architecture.md`:
- **Build mode** (section "Getting AnyPS5"): replace steps 1–5 with the layout `anyps5\source`, `anyps5\build`, `anyps5\state`; the step table of the spec (names, commands, markers); fetch by SHA at depth 1; `BUILD_TESTING=OFF` so `libs` is built separately; `-DCMAKE_MAKE_PROGRAM` and full WinLibs compiler paths with forward slashes; parallel jobs formula; mode switch empties `current`; resume and update behavior. Correct the CI note: AnyPS5's CI configures with `BUILD_TESTING=ON` and ccache, and puts WinLibs first on PATH; WinLibs r7 ships CMake 4.3.1 and Ninja 1.13.2, so the fallback to official zips is not needed.
- **Portable tools**: MinGit 2.56.0.2 (CA bundle under `ucrt64`, `mingw64` as fallback) and WinLibs r7 zip; remove "To confirm at first extraction".
- **AnyPS5 profiles**: the `build` section replaces `alternateSource`; resolution order is now "embedded, or a valid `<root>\profiles\override.json`", with the downloaded profile still to come.
- **Root layout**: add `state\` and `build\` under `anyps5\`; `install.json` lists mode, version, commit.
- **Data and configuration**: `install.json` schema 2 (fields and meaning, schema 1 still read).
- **Checks table**: free disk space uses `buildMinFreeDiskGiB` in build mode.
- **Roadmap**: split milestone 5 into "5a — Build mode, Core and CLI" (the current criterion) and "5b — Build mode UI" (Build card, Tools screen, build progress on Install).

`README.md`: document `ps5kitsune install --mode build`, the default mode rule, the three opening lines, and `<root>\profiles\override.json` (copy the profile, change `build.commit`, run `install`).

Spec: append a section "Refinements from the implementation plan":
1. MinGit 2.56 keeps its CA bundle under `ucrt64`: it is tried first, `mingw64` second.
2. The process runner kills the whole tree itself on cancellation (no Job Object needed).
3. Error-like lines reach the console from both outputs: Ninja prints compiler errors on the standard output. The tail quoted in errors holds both outputs, Ninja counters excluded.
4. The console prints the build fraction every 10 %, like downloads, instead of overwriting one line.
5. `GetSourceStep` sets `remote.origin.url` with `git config`, so a rerun after any interruption needs no special case; configure passes `-DCMAKE_MAKE_PROGRAM` and forward-slash paths.
6. The copy step's done-check ignores extra files in `current` (Python's `__pycache__`); execution still mirrors `libs` and `scripts`.
7. The app's Install page installs in release mode (the only card on Mode until 5b); Home and Done read the current mode from `install.json`. The app also loads `override.json` per root, without showing an ignored one until 5b.
8. The network integration test is `[Fact(Explicit = true)]`, so neither the default run nor CI runs it.

- [ ] **Step 3: Check the build and tests one last time**

Run: `dotnet build PS5Kitsune.slnx -c Release` and `dotnet test --solution PS5Kitsune.slnx -c Release`. Expected: 0 warnings, all pass.

- [ ] **Step 4: Commit**

```bash
git add tests/PS5Kitsune.Core.Tests/AnyPS5/Build/RealSourceTests.cs docs README.md
git commit -m "Document build mode and add an explicit network test"
```

---

## Criterion (user, after Task 11)

Run on the user's PC, on empty roots, with the Bash tool or a terminal:

1. `ps5kitsune install --root C:\PS5KBuild --mode build`: no intervention needed. Note the duration and the size of `C:\PS5KBuild\anyps5\source` plus `build`; set `buildMinFreeDiskGiB` from that size (rounded up, plus room for a game) in `default-profile.json` and the docs.
2. `ps5kitsune install --root C:\PS5KBuild2 --mode build`, Ctrl+C during "Compile AnyPS5", then `ps5kitsune install --root C:\PS5KBuild2`: steps before compile say "already done" and the counter resumes near where it stopped.
3. Copy `default-profile.json` to `C:\PS5KBuild\profiles\override.json` with a newer commit of `main`, then `ps5kitsune install --root C:\PS5KBuild`: `Profile: … (override)`, `Mode: build (current install)`, and a much shorter build.
4. Bonus: `ps5kitsune add --root C:\PS5KBuild <DOOM folder>` gives a green audit.
