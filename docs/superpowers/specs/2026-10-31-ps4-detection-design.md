# PS4 game detection: design

October 31, 2026 · Planned for PS5Kitsune 0.3.0. Source: [the PS4 and Bloodborne report](../../reports/2026-10-31-ps4-games-and-bloodborne.md), proposed changes 1 and 2.

## Goal

Adding a PS4 dump today stops on "sce_sys/param.json is missing.", which reads as a broken dump. PS4 games keep their metadata in `sce_sys/param.sfo`, and AnyPS5 v0.1.4 cannot run them yet: it has no PS4 graphics driver (`libSceGnmDriver`), which is still an open upstream PR (#9432).

PS5Kitsune reads `param.sfo`, recognizes a PS4 game, and says plainly why it cannot be added. When an AnyPS5 install ships the driver, the add goes ahead with a warning, without a new PS5Kitsune release.

**Criterion:** adding Bloodborne (CUSA03173, decrypted v1.09) with AnyPS5 v0.1.4, from the app and from `ps5kitsune add`, shows "PS4 game: AnyPS5 v0.1.4 cannot draw PS4 graphics yet (no libSceGnmDriver). PS4 support is in progress upstream." and no "param.json is missing". PS5 games get exactly the issues they get today.

**Out of scope:**

- Recording "PS4" in `game.json` or showing it in the Library.
- PS4 add-ons (folders such as `SPEXPANSIONDLC03`) and `playgo-chunk.dat`.
- The crash diagnosis's `param-json` text.
- Pointing to third-party ports (the Bloodborne PC port): one neutral message for every PS4 game.
- Validating a PS4 game end to end: no AnyPS5 release has the driver yet.

## Behaviour

### Metadata

- `sce_sys/param.json` present: read as today. Every PS5 game ships it.
- Otherwise `sce_sys/param.sfo` present: read it.
  - `TITLE_ID` is the title ID (also the game's folder name), with the existing rule: letters and digits only.
  - `TITLE` is the name, decoded as UTF-8, trailing NUL padding trimmed (Bloodborne's name ends with `®`). A missing or empty `TITLE` falls back to the title ID, as `param.json` does.
  - A file that cannot be read gives one blocking issue: "sce_sys/param.sfo is not a valid PSF file." (bad magic, truncated header, an offset or length past the end of the file), "sce_sys/param.sfo has no TITLE_ID.", or the existing "Title ID "…" must contain only letters and digits.".
- Neither file: "sce_sys/param.json is missing (PS5) and so is sce_sys/param.sfo (PS4)."

PSF layout, little-endian: magic `00 50 53 46` (`\0PSF`), u32 version, u32 key table offset, u32 data table offset, u32 entry count, then one 16-byte entry per field from offset 20: u16 key offset (into the key table, NUL-terminated ASCII key), u16 format, u32 used length, u32 max length, u32 data offset (into the data table). Format `0x0204` is UTF-8 text ending with NUL, `0x0004` UTF-8 text without NUL, `0x0404` a u32. Only text entries are read.

### PS4 rule

A game is PS4 when its metadata came from `param.sfo`, or its title ID starts with `CUSA`.

### What a PS4 game gets

The active AnyPS5 install has the PS4 graphics driver when `<install>\libs\libSceGnmDriver.prx` exists. Release and build installs both keep their converted libraries there; v0.1.4 has only `libSceGnmDriverCompat1.prx` and `libSceGnmDriverForNeoMode.prx`.

- Driver absent: a blocking issue, "PS4 game: AnyPS5 {version} cannot draw PS4 graphics yet (no libSceGnmDriver). PS4 support is in progress upstream." `{version}` is the active install's version (`v0.1.4`, or the build's short commit); "the installed AnyPS5" replaces "AnyPS5 {version}" when no install is active. The other issues (eboot.bin, modules, free space) are still listed.
- Driver present: a warning, "PS4 game: PS4 support in AnyPS5 is experimental. Expect missing imports in the audit." The add runs as usual.

Both appear through the existing issue list: the app's Summary page and `ps5kitsune add`. No screen changes.

## Code

### Core (`src/PS5Kitsune.Core/Games/`)

- **New `ParamSfo.cs`** (internal static, beside `ParamJson`): `GameMetadata Read(string path)`, throwing `InvalidDataException` with the messages above. Every offset and length is checked against the file size before it is read.
- **`GameMetadata`**: gains `bool IsPs4 = false` as an optional last parameter, so existing callers are unchanged. `ParamSfo` sets it; `ParamJson` sets it for a `CUSA` title ID.
- **New `Ps4Support` record** `(bool DriverPresent, string AnyPS5Name)`: `AnyPS5Name` is "AnyPS5 v0.1.4" or "the installed AnyPS5".
- **`GameValidator.Validate(string gameRoot, string destination, Ps4Support ps4)`**: reads `param.json`, else `param.sfo`; for a PS4 game, adds the blocking issue or the warning.
- **`GameImporter`**: builds `Ps4Support` from the active install (`paths.Libs`, `installer.ReadRecord()`), and passes it to `Validate`. The file name `libSceGnmDriver.prx` is a constant in one place.
- **`docs/architecture.md`**, "V2 — Add a game", step 2: the `param.sfo` fallback and the PS4 rule.

## Tests

- **`ParamSfoTests`** (Core.Tests), with a test helper that writes the real PSF layout: title ID and name read, with a UTF-8 `®` and NUL padding; format `0x0004` text read; a missing `TITLE` falls back to the title ID; a bad magic, a truncated header, an entry or data offset past the end, a missing `TITLE_ID`, and a title ID with a dash each give their message.
- **`GameValidatorTests`**: a PS4 dump without the driver gets the blocking text with the version and no "param.json is missing"; with the driver, the warning and no blocking issue; a `param.json` with a `CUSA` title ID counts as PS4; neither file gives the combined message; existing PS5 cases are unchanged.
- **`PrepareGameTests` or `GameImporterTests`**: a `libSceGnmDriver.prx` in the active install's `libs` turns the block into the warning; no active install names "the installed AnyPS5".

## Criterion

1. Unit tests pass on Windows and Linux CI.
2. Manual, on the PC that holds the Bloodborne dump: the app's Summary page and `ps5kitsune add` show the PS4 message for CUSA03173, with the version, and no "param.json is missing"; DOOM (PPSA99666) still adds as before.
