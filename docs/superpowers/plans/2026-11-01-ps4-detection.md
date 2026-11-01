# PS4 Game Detection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Read a PS4 dump's `sce_sys/param.sfo`, recognize PS4 games, and block them with a clear message while the active AnyPS5 install has no `libSceGnmDriver.prx` (a warning once it has one).

**Architecture:** A new `ParamSfo` reader beside `ParamJson` returns `GameMetadata` with `IsPs4`. `GameValidator.Validate` falls back to `param.sfo` and, for a PS4 game, adds a blocking issue or a warning from a `Ps4Support` value the `GameImporter` builds from the active install. The app's Summary page and `ps5kitsune add` already show validation issues.

**Tech Stack:** .NET 10, C#, `System.Buffers.Binary`, xUnit v3.

**Spec:** `docs/superpowers/specs/2026-10-31-ps4-detection-design.md`

## Global Constraints

- Texts, exactly:
  - `sce_sys/param.sfo is not a valid PSF file.`
  - `sce_sys/param.sfo has no TITLE_ID.`
  - `Title ID "{titleId}" must contain only letters and digits.` (existing text)
  - `sce_sys/param.json is missing (PS5) and so is sce_sys/param.sfo (PS4).`
  - `PS4 game: {AnyPS5Name} cannot draw PS4 graphics yet (no libSceGnmDriver). PS4 support is in progress upstream.` with `{AnyPS5Name}` = `AnyPS5 {record.Version}` (e.g. `AnyPS5 v0.1.4`, or `AnyPS5 7fcab08` for a build) or `the installed AnyPS5` when no install is active.
  - `PS4 game: PS4 support in AnyPS5 is experimental. Expect missing imports in the audit.`
- PS4 rule: metadata came from `param.sfo`, or the title ID starts with `CUSA` (ordinal).
- Driver present: `<active install>\libs\libSceGnmDriver.prx` exists.
- `param.json` present → read as today; PS5 games get exactly today's issues.
- PSF layout, little-endian: magic `00 50 53 46`; u32 version @4; u32 key table offset @8; u32 data table offset @12; u32 entry count @16; 16-byte entries from @20: u16 key offset, u16 format, u32 used length, u32 max length, u32 data offset. Formats `0x0204` (UTF-8 + NUL) and `0x0004` (UTF-8) are text; others are skipped. Every offset and length is checked against the file size.
- Build rules: `TreatWarningsAsErrors`, `AnalysisLevel latest-recommended`, `EnforceCodeStyleInBuild`. Core code uses `ConfigureAwait(false)`.
- Commits end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Commit locally only; no push.

## File Structure

| File | Responsibility |
|---|---|
| `src/PS5Kitsune.Core/Games/ParamSfo.cs` (new) | Read TITLE_ID and TITLE from a PSF file |
| `src/PS5Kitsune.Core/Games/GameMetadata.cs` | `IsPs4` |
| `src/PS5Kitsune.Core/Games/ParamJson.cs` | `IsPs4` for a `CUSA` title ID |
| `src/PS5Kitsune.Core/Games/Ps4Support.cs` (new) | What the active install offers PS4 games |
| `src/PS5Kitsune.Core/Games/GameValidator.cs` | `param.sfo` fallback, PS4 issues |
| `src/PS5Kitsune.Core/Games/GameImporter.cs` | Builds `Ps4Support` from the active install |
| `tests/PS5Kitsune.Core.Tests/Games/TestSfo.cs` (new) | Writes PSF files for tests |
| `docs/architecture.md` | "V2 — Add a game", step 2 |

---

### Task 1: Read param.sfo

**Files:**
- Create: `src/PS5Kitsune.Core/Games/ParamSfo.cs`
- Modify: `src/PS5Kitsune.Core/Games/GameMetadata.cs`, `src/PS5Kitsune.Core/Games/ParamJson.cs`
- Test: create `tests/PS5Kitsune.Core.Tests/Games/TestSfo.cs`, `tests/PS5Kitsune.Core.Tests/Games/ParamSfoTests.cs`

**Interfaces:**
- Produces: `GameMetadata(string TitleId, string Name, bool IsPs4 = false)`; `internal static class ParamSfo { GameMetadata Read(string path); }` (throws `InvalidDataException`); test helper `TestSfo.Build(params (string Key, ushort Format, byte[] Data)[] entries)`, `TestSfo.Texts(params (string Key, string Value)[] texts)`, `TestSfo.Bloodborne` (bytes of a PSF with `TITLE_ID` = `CUSA03173`, `TITLE` = `Bloodborne®`).

- [ ] **Step 1: Write the test helper and the failing tests**

Create `tests/PS5Kitsune.Core.Tests/Games/TestSfo.cs`:

```csharp
using System.Buffers.Binary;
using System.Text;

namespace PS5Kitsune.Core.Tests.Games;

/// <summary>Writes param.sfo files in the PSF layout PS4 dumps use: header, entries, key table, data table.</summary>
internal static class TestSfo
{
    public const ushort TextWithNul = 0x0204;

    public const ushort Text = 0x0004;

    public const ushort Number = 0x0404;

    public static byte[] Bloodborne { get; } = Texts(("APP_VER", "01.09"), ("TITLE", "Bloodborne®"), ("TITLE_ID", "CUSA03173"));

    /// <summary>Each value as UTF-8 text with a NUL and padding to 4 bytes, format 0x0204.</summary>
    public static byte[] Texts(params (string Key, string Value)[] texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        return Build([.. texts.Select(text => (text.Key, TextWithNul, Padded(text.Value)))]);
    }

    public static byte[] Build(params (string Key, ushort Format, byte[] Data)[] entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var table = new List<byte>();
        var keys = new List<byte>();
        var data = new List<byte>();
        foreach (var (key, format, value) in entries)
        {
            var entry = new byte[16];
            BinaryPrimitives.WriteUInt16LittleEndian(entry.AsSpan(0), (ushort)keys.Count);
            BinaryPrimitives.WriteUInt16LittleEndian(entry.AsSpan(2), format);
            BinaryPrimitives.WriteUInt32LittleEndian(entry.AsSpan(4), (uint)value.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(entry.AsSpan(8), (uint)value.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(entry.AsSpan(12), (uint)data.Count);
            table.AddRange(entry);
            keys.AddRange(Encoding.ASCII.GetBytes(key));
            keys.Add(0);
            data.AddRange(value);
        }

        while (keys.Count % 4 != 0)
        {
            keys.Add(0);
        }

        var header = new byte[20];
        "\0PSF"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), 0x0101);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), (uint)(20 + table.Count));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12), (uint)(20 + table.Count + keys.Count));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(16), (uint)entries.Length);
        return [.. header, .. table, .. keys, .. data];
    }

    private static byte[] Padded(string value)
    {
        var bytes = new List<byte>(Encoding.UTF8.GetBytes(value)) { 0 };
        while (bytes.Count % 4 != 0)
        {
            bytes.Add(0);
        }

        return [.. bytes];
    }
}
```

Create `tests/PS5Kitsune.Core.Tests/Games/ParamSfoTests.cs`:

```csharp
using System.Buffers.Binary;
using System.Text;
using PS5Kitsune.Core.Games;

namespace PS5Kitsune.Core.Tests.Games;

public sealed class ParamSfoTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    private GameMetadata Read(byte[] bytes)
    {
        var path = _temp.Combine("param.sfo");
        File.WriteAllBytes(path, bytes);
        return ParamSfo.Read(path);
    }

    private string ReadError(byte[] bytes) => Assert.Throws<InvalidDataException>(() => Read(bytes)).Message;

    [Fact]
    public void The_title_id_and_the_utf8_name_are_read_and_marked_ps4() =>
        Assert.Equal(new GameMetadata("CUSA03173", "Bloodborne®", IsPs4: true), Read(TestSfo.Bloodborne));

    [Fact]
    public void Text_without_a_nul_is_read() =>
        Assert.Equal(
            new GameMetadata("CUSA01664", "LIMBO", IsPs4: true),
            Read(TestSfo.Build(("TITLE", TestSfo.Text, Encoding.UTF8.GetBytes("LIMBO")), ("TITLE_ID", TestSfo.Text, Encoding.UTF8.GetBytes("CUSA01664")))));

    [Fact]
    public void Number_entries_are_skipped() =>
        Assert.Equal("CUSA03173", Read(TestSfo.Build(("PARENTAL_LEVEL", TestSfo.Number, [5, 0, 0, 0]), ("TITLE_ID", TestSfo.Text, "CUSA03173"u8.ToArray()))).TitleId);

    [Fact]
    public void A_missing_or_empty_title_falls_back_to_the_title_id()
    {
        Assert.Equal("CUSA03173", Read(TestSfo.Texts(("TITLE_ID", "CUSA03173"))).Name);
        Assert.Equal("CUSA03173", Read(TestSfo.Texts(("TITLE", ""), ("TITLE_ID", "CUSA03173"))).Name);
    }

    [Fact]
    public void A_bad_magic_is_not_a_psf_file()
    {
        var bytes = TestSfo.Bloodborne.ToArray();
        bytes[1] = (byte)'X';

        Assert.Equal("sce_sys/param.sfo is not a valid PSF file.", ReadError(bytes));
    }

    [Fact]
    public void A_truncated_header_is_not_a_psf_file() =>
        Assert.Equal("sce_sys/param.sfo is not a valid PSF file.", ReadError(TestSfo.Bloodborne[..12]));

    [Fact]
    public void More_entries_than_the_file_holds_is_not_a_psf_file()
    {
        var bytes = TestSfo.Bloodborne.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 1000);

        Assert.Equal("sce_sys/param.sfo is not a valid PSF file.", ReadError(bytes));
    }

    [Fact]
    public void A_data_offset_past_the_end_is_not_a_psf_file()
    {
        var bytes = TestSfo.Bloodborne.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20 + 12), 100_000);

        Assert.Equal("sce_sys/param.sfo is not a valid PSF file.", ReadError(bytes));
    }

    [Fact]
    public void A_key_offset_past_the_end_is_not_a_psf_file()
    {
        var bytes = TestSfo.Bloodborne.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(20), 60_000);

        Assert.Equal("sce_sys/param.sfo is not a valid PSF file.", ReadError(bytes));
    }

    [Fact]
    public void A_missing_title_id_is_named() =>
        Assert.Equal("sce_sys/param.sfo has no TITLE_ID.", ReadError(TestSfo.Texts(("TITLE", "Bloodborne®"))));

    [Fact]
    public void A_title_id_that_is_not_a_folder_name_is_refused() =>
        Assert.Equal("Title ID \"CUSA-03173\" must contain only letters and digits.", ReadError(TestSfo.Texts(("TITLE_ID", "CUSA-03173"))));

    public void Dispose() => _temp.Dispose();
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build fails with `The name 'ParamSfo' does not exist` and an error on the `IsPs4` named argument.

- [ ] **Step 3: Implement**

`GameMetadata.cs`:

```csharp
namespace PS5Kitsune.Core.Games;

/// <param name="TitleId">From param.json or param.sfo; also the name of the game folder.</param>
/// <param name="Name">Display name, resolved from localizedParameters or param.sfo's TITLE.</param>
/// <param name="IsPs4">A PS4 game: read from param.sfo, or a CUSA title ID.</param>
public sealed record GameMetadata(string TitleId, string Name, bool IsPs4 = false);
```

`ParamJson.cs`: replace `return new GameMetadata(titleId, ResolveName(root, localized) ?? titleId);` with:

```csharp
            return new GameMetadata(titleId, ResolveName(root, localized) ?? titleId, ParamSfo.IsPs4TitleId(titleId));
```

Create `src/PS5Kitsune.Core/Games/ParamSfo.cs`:

```csharp
using System.Buffers.Binary;
using System.Text;

namespace PS5Kitsune.Core.Games;

/// <summary>
/// Reads the title ID and name from a PS4 game's sce_sys/param.sfo (PSF layout, little-endian). See the PS4 detection spec.
/// </summary>
internal static class ParamSfo
{
    private const int HeaderSize = 20;
    private const int EntrySize = 16;
    private const ushort TextWithNul = 0x0204;
    private const ushort Text = 0x0004;
    private const string NotPsf = "sce_sys/param.sfo is not a valid PSF file.";

    /// <summary>PS4 title IDs start with CUSA; PS5 ones with PPSA.</summary>
    public static bool IsPs4TitleId(string titleId) => titleId.StartsWith("CUSA", StringComparison.Ordinal);

    /// <exception cref="InvalidDataException">The file is not a PSF file with a usable TITLE_ID; the message says why.</exception>
    public static GameMetadata Read(string path)
    {
        var texts = ReadTexts(File.ReadAllBytes(path));
        if (!texts.TryGetValue("TITLE_ID", out var titleId) || string.IsNullOrWhiteSpace(titleId))
        {
            throw new InvalidDataException("sce_sys/param.sfo has no TITLE_ID.");
        }

        if (!GameValidator.IsValidTitleId(titleId))
        {
            throw new InvalidDataException($"Title ID \"{titleId}\" must contain only letters and digits.");
        }

        var name = texts.TryGetValue("TITLE", out var title) && !string.IsNullOrWhiteSpace(title) ? title : titleId;
        return new GameMetadata(titleId, name, IsPs4: true);
    }

    // Every text entry by key; trailing NULs (terminator and padding) are trimmed.
    private static Dictionary<string, string> ReadTexts(byte[] bytes)
    {
        if (bytes.Length < HeaderSize || !bytes.AsSpan(0, 4).SequenceEqual("\0PSF"u8))
        {
            throw new InvalidDataException(NotPsf);
        }

        long keyTable = U32(bytes, 8);
        long dataTable = U32(bytes, 12);
        long count = U32(bytes, 16);
        if (HeaderSize + (count * EntrySize) > bytes.Length)
        {
            throw new InvalidDataException(NotPsf);
        }

        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < count; index++)
        {
            var entry = HeaderSize + (index * EntrySize);
            var format = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(entry + 2));
            if (format is not (TextWithNul or Text))
            {
                continue;
            }

            var key = Key(bytes, keyTable + BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(entry)));
            long length = U32(bytes, entry + 4);
            var start = dataTable + U32(bytes, entry + 12);
            if (start + length > bytes.Length)
            {
                throw new InvalidDataException(NotPsf);
            }

            texts[key] = Encoding.UTF8.GetString(bytes, (int)start, (int)length).TrimEnd('\0');
        }

        return texts;
    }

    private static string Key(byte[] bytes, long start)
    {
        if (start >= bytes.Length)
        {
            throw new InvalidDataException(NotPsf);
        }

        var end = bytes.AsSpan((int)start).IndexOf((byte)0);
        if (end < 0)
        {
            throw new InvalidDataException(NotPsf);
        }

        return Encoding.ASCII.GetString(bytes, (int)start, end);
    }

    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
}
```

- [ ] **Step 4: Run the tests to see them pass**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --project tests/PS5Kitsune.Core.Tests -c Release --no-build`
Expected: all tests pass (the existing `GameMetadata` equality assertions still hold: `IsPs4` defaults to false for `PPSA` IDs).

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Core/Games tests/PS5Kitsune.Core.Tests/Games
git commit -m "Read the title ID and name from a PS4 game's param.sfo

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Block or warn about PS4 games when adding

**Files:**
- Create: `src/PS5Kitsune.Core/Games/Ps4Support.cs`
- Modify: `src/PS5Kitsune.Core/Games/GameValidator.cs`, `src/PS5Kitsune.Core/Games/GameImporter.cs` (method `PrepareAsync`, the `validator.Validate(...)` call), `src/PS5Kitsune.Core/Games/GameValidation.cs` (the `Metadata` doc comment), `docs/architecture.md` ("V2 — Add a game", Flow step 2)
- Test: `tests/PS5Kitsune.Core.Tests/Games/GameValidatorTests.cs`, `tests/PS5Kitsune.Core.Tests/Games/PrepareGameTests.cs`

**Interfaces:**
- Consumes (Task 1): `ParamSfo.Read(string path)`, `GameMetadata.IsPs4`, `TestSfo.Bloodborne`.
- Produces: `public sealed record Ps4Support(bool DriverPresent, string AnyPS5Name)` with `public const string DriverFile = "libSceGnmDriver.prx"` and `public static Ps4Support Unknown { get; }` (driver absent, "the installed AnyPS5"); `GameValidator.Validate(string gameRoot, string destination, Ps4Support? ps4 = null)` (null means `Ps4Support.Unknown`).

- [ ] **Step 1: Write the failing tests**

In `GameValidatorTests`, add a helper and tests:

```csharp
    private GameValidation ValidatePs4(Ps4Support? ps4 = null) =>
        new GameValidator(_ => long.MaxValue).Validate(Game, _temp.Combine("games"), ps4);

    private void MakePs4()
    {
        File.Delete(Path.Combine(Game, "sce_sys", "param.json"));
        File.WriteAllBytes(Path.Combine(Game, "sce_sys", "param.sfo"), TestSfo.Bloodborne);
    }

    [Fact]
    public void A_ps4_game_without_the_driver_is_blocked_with_the_version()
    {
        MakePs4();

        var validation = ValidatePs4(new Ps4Support(DriverPresent: false, "AnyPS5 v0.1.4"));

        Assert.True(validation.IsBlocked);
        Assert.Equal(new GameMetadata("CUSA03173", "Bloodborne®", IsPs4: true), validation.Metadata);
        var issue = Assert.Single(validation.Issues);
        Assert.Equal(new ValidationIssue(IssueSeverity.Blocking, "PS4 game: AnyPS5 v0.1.4 cannot draw PS4 graphics yet (no libSceGnmDriver). PS4 support is in progress upstream."), issue);
    }

    [Fact]
    public void Without_an_active_install_the_message_names_the_installed_anyps5()
    {
        MakePs4();

        Assert.Equal(
            "PS4 game: the installed AnyPS5 cannot draw PS4 graphics yet (no libSceGnmDriver). PS4 support is in progress upstream.",
            Assert.Single(ValidatePs4().Issues).Message);
    }

    [Fact]
    public void A_ps4_game_with_the_driver_gets_a_warning_only()
    {
        MakePs4();

        var validation = ValidatePs4(new Ps4Support(DriverPresent: true, "AnyPS5 v0.2.0"));

        Assert.False(validation.IsBlocked);
        Assert.Equal(
            new ValidationIssue(IssueSeverity.Warning, "PS4 game: PS4 support in AnyPS5 is experimental. Expect missing imports in the audit."),
            Assert.Single(validation.Issues));
    }

    [Fact]
    public void A_cusa_title_id_in_param_json_counts_as_ps4()
    {
        WriteParam("""{ "titleId": "CUSA01664", "localizedParameters": { "en-US": { "titleName": "LIMBO" } } }""");

        var validation = ValidatePs4(new Ps4Support(DriverPresent: false, "AnyPS5 v0.1.4"));

        Assert.True(validation.Metadata!.IsPs4);
        Assert.Contains(validation.Issues, issue => issue.Message.StartsWith("PS4 game: AnyPS5 v0.1.4 cannot draw", StringComparison.Ordinal));
    }

    [Fact]
    public void A_damaged_param_sfo_is_blocking_with_its_reason()
    {
        File.Delete(Path.Combine(Game, "sce_sys", "param.json"));
        File.WriteAllBytes(Path.Combine(Game, "sce_sys", "param.sfo"), [1, 2, 3]);

        var validation = ValidatePs4();

        Assert.Null(validation.Metadata);
        Assert.Equal(new ValidationIssue(IssueSeverity.Blocking, "sce_sys/param.sfo is not a valid PSF file."), Assert.Single(validation.Issues));
    }

    [Fact]
    public void Neither_metadata_file_names_both()
    {
        File.Delete(Path.Combine(Game, "sce_sys", "param.json"));

        Assert.Contains(
            new ValidationIssue(IssueSeverity.Blocking, "sce_sys/param.json is missing (PS5) and so is sce_sys/param.sfo (PS4)."),
            Validate().Issues);
    }

    [Fact]
    public void A_ps5_game_gets_no_ps4_issue_whatever_the_driver()
    {
        Assert.Empty(ValidatePs4(new Ps4Support(DriverPresent: false, "AnyPS5 v0.1.4")).Issues);
        Assert.Empty(ValidatePs4(new Ps4Support(DriverPresent: true, "AnyPS5 v0.2.0")).Issues);
    }
```

In `PrepareGameTests`, add:

```csharp
    private string Ps4Folder()
    {
        var folder = _temp.Combine("bloodborne");
        TestGames.Create(folder);
        File.Delete(Path.Combine(folder, "sce_sys", "param.json"));
        File.WriteAllBytes(Path.Combine(folder, "sce_sys", "param.sfo"), TestSfo.Bloodborne);
        return folder;
    }

    [Fact]
    public async Task A_ps4_game_is_blocked_with_the_active_install_version()
    {
        await _setup.InstallAsync();
        var version = _setup.Installer.ReadRecord()!.Version;

        await using var game = await PrepareAsync(Ps4Folder());

        Assert.True(game.IsBlocked);
        Assert.Equal(
            $"PS4 game: AnyPS5 {version} cannot draw PS4 graphics yet (no libSceGnmDriver). PS4 support is in progress upstream.",
            Assert.Single(game.Validation.Issues).Message);
        Assert.Equal(_setup.Layout.GameDirectory("CUSA03173"), game.GameDirectory);
    }

    [Fact]
    public async Task The_ps4_driver_in_the_active_install_turns_the_block_into_a_warning()
    {
        await _setup.InstallAsync();
        File.WriteAllBytes(Path.Combine(_setup.Paths.For(_setup.Installer.ReadRecord()!.Mode).Libs, Ps4Support.DriverFile), [0]);

        await using var game = await PrepareAsync(Ps4Folder());

        Assert.False(game.IsBlocked);
        Assert.Equal(IssueSeverity.Warning, Assert.Single(game.Validation.Issues).Severity);
    }

    [Fact]
    public async Task Without_an_install_a_ps4_game_names_the_installed_anyps5()
    {
        await using var game = await PrepareAsync(Ps4Folder());

        Assert.StartsWith("PS4 game: the installed AnyPS5 cannot draw", Assert.Single(game.Validation.Issues).Message, StringComparison.Ordinal);
    }
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet build PS5Kitsune.slnx -c Release`
Expected: build fails with `The type or namespace name 'Ps4Support' could not be found`.

- [ ] **Step 3: Implement**

Create `src/PS5Kitsune.Core/Games/Ps4Support.cs`:

```csharp
namespace PS5Kitsune.Core.Games;

/// <summary>What the active AnyPS5 install offers PS4 games. See the PS4 detection spec.</summary>
/// <param name="DriverPresent">The install's libs folder has <see cref="DriverFile"/>, the PS4 graphics driver.</param>
/// <param name="AnyPS5Name">"AnyPS5 v0.1.4" (the install's version), or "the installed AnyPS5" when none is active.</param>
public sealed record Ps4Support(bool DriverPresent, string AnyPS5Name)
{
    public const string DriverFile = "libSceGnmDriver.prx";

    /// <summary>No active install: the driver is taken as absent.</summary>
    public static Ps4Support Unknown { get; } = new(DriverPresent: false, "the installed AnyPS5");
}
```

`GameValidator.cs`: change `Validate` to:

```csharp
    /// <param name="destination">Folder the game will be copied under; only its drive's free space matters.</param>
    /// <param name="ps4">What the active install offers PS4 games; null is <see cref="Ps4Support.Unknown"/>.</param>
    public GameValidation Validate(string gameRoot, string destination, Ps4Support? ps4 = null)
    {
        var issues = new List<ValidationIssue>();
        if (!File.Exists(Path.Combine(gameRoot, GameSourceResolver.ExecutableName)))
        {
            issues.Add(Blocking($"{GameSourceResolver.ExecutableName} is missing."));
        }

        var metadata = ReadMetadata(gameRoot, issues);
        if (metadata is { IsPs4: true })
        {
            issues.Add(Ps4Issue(ps4 ?? Ps4Support.Unknown));
        }
```

(the rest of `Validate` is unchanged). Replace `ReadMetadata` with:

```csharp
    // param.json for a PS5 game; param.sfo, which PS4 games ship instead, when it is absent.
    private static GameMetadata? ReadMetadata(string gameRoot, List<ValidationIssue> issues)
    {
        var json = Path.Combine(gameRoot, "sce_sys", "param.json");
        var sfo = Path.Combine(gameRoot, "sce_sys", "param.sfo");
        try
        {
            if (File.Exists(json))
            {
                return ParamJson.Read(json);
            }

            if (File.Exists(sfo))
            {
                return ParamSfo.Read(sfo);
            }
        }
        catch (InvalidDataException exception)
        {
            issues.Add(Blocking(exception.Message));
            return null;
        }

        issues.Add(Blocking("sce_sys/param.json is missing (PS5) and so is sce_sys/param.sfo (PS4)."));
        return null;
    }

    private static ValidationIssue Ps4Issue(Ps4Support ps4) => ps4.DriverPresent
        ? new ValidationIssue(IssueSeverity.Warning, "PS4 game: PS4 support in AnyPS5 is experimental. Expect missing imports in the audit.")
        : Blocking($"PS4 game: {ps4.AnyPS5Name} cannot draw PS4 graphics yet (no libSceGnmDriver). PS4 support is in progress upstream.");
```

`GameImporter.cs`: replace `var validation = validator.Validate(source.Root, layout.Games);` with:

```csharp
            var validation = validator.Validate(source.Root, layout.Games, Ps4SupportOfActiveInstall());
```

and add to the class:

```csharp
    // The PS4 graphics driver ships as a converted library, so it sits in the install's libs folder in both modes.
    private Ps4Support Ps4SupportOfActiveInstall() =>
        installer.ReadRecord() is { } record
            ? new Ps4Support(File.Exists(Path.Combine(paths.For(record.Mode).Libs, Ps4Support.DriverFile)), $"AnyPS5 {record.Version}")
            : Ps4Support.Unknown;
```

`GameValidation.cs`: change the `Metadata` doc comment to `/// <param name="Metadata">Null when neither param.json nor param.sfo can be read.</param>`.

`docs/architecture.md`, "V2 — Add a game", Flow step 2: after the `sce_sys/param.json` bullet, add:

```markdown
   - otherwise `sce_sys/param.sfo` (PS4 dumps): `TITLE_ID` and `TITLE` read from the PSF table (UTF-8 text entries). A game is PS4 when its metadata came from `param.sfo` or its title ID starts with `CUSA`. Without the PS4 graphics driver in the active install (`libs\libSceGnmDriver.prx`, absent in AnyPS5 v0.1.4), a PS4 game is blocked: "PS4 game: AnyPS5 v0.1.4 cannot draw PS4 graphics yet (no libSceGnmDriver)…"; with it, the add runs with an "experimental" warning. Neither file: "sce_sys/param.json is missing (PS5) and so is sce_sys/param.sfo (PS4).";
```

- [ ] **Step 4: Run the tests to see them pass**

Run: `dotnet build PS5Kitsune.slnx -c Release && dotnet test --solution PS5Kitsune.slnx -c Release --no-build`
Expected: all tests pass, including the existing `A_missing_param_json_is_blocking` and `Every_blocking_issue_is_reported_at_once` (3 blocking issues).

- [ ] **Step 5: Commit**

```bash
git add src/PS5Kitsune.Core/Games tests/PS5Kitsune.Core.Tests/Games docs/architecture.md
git commit -m "Block PS4 games while AnyPS5 has no PS4 graphics driver

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## After the tasks (controller, not a subagent)

- Manual check on the PC with the Bloodborne dump (CUSA03173): the app's Summary page and `ps5kitsune add` show "PS4 game: AnyPS5 v0.1.4 cannot draw PS4 graphics yet (no libSceGnmDriver). PS4 support is in progress upstream." and no "param.json is missing"; DOOM still adds as before. Needs the user.
