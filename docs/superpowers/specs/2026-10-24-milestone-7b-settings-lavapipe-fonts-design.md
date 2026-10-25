# Milestone 7b — Per-game settings, Lavapipe and fonts: design

October 24, 2026 · Complements [docs/architecture.md](../../architecture.md) ("V3 — Launcher", "Per-game settings") and the [milestone 7a spec](2026-10-24-milestone-7a-library-launch-design.md).

## Goal

Let the user set, per game, the AnyPS5 options that are passed as environment variables, and apply them at every launch from the app and from `run`. A game set to CPU rendering gets Mesa's Lavapipe, downloaded on its first launch. Every launch gets the Noto font substitutes, downloaded once. Four PR #9 follow-ups on the launch path are folded in.

**Criterion:** on the Intel Iris Xe laptop with DOOM, see [Criterion](#criterion).

**Out of scope:**

- The keyboard and mouse bindings editor (`anyps5-input.ini`). Until it comes, Open folder lets the user edit the file. It is not scheduled yet.
- **7c:** crash diagnosis, including reading `ANYPS5_*` errors from the log and "Reset that setting".
- **7d:** visual design.
- Editing settings from the CLI.
- App-wide settings beyond the data root (fonts have no switch).

## Facts checked on 2026-10-24

- AnyPS5 [USAGE.md](https://github.com/boykopovar/AnyPS5/blob/main/docs/user/USAGE.md):
  - **`ANYPS5_GPU`:** part of a device name, case-insensitive. If nothing matches, startup fails and lists the names.
  - **`ANYPS5_LANGUAGE`:** a number from 0 to 30, default 1 (English, US). An empty value counts as unset; any other invalid value stops the game.
  - **`ANYPS5_PRESENT_MODE`:** `fifo_latest_ready`, `mailbox` or `immediate`. The default is `fifo`. A mode the surface does not offer fails.
  - **`ANYPS5_OVERLAY=1`:** shows the overlay from the first frame; F10 toggles it. `ANYPS5_OVERLAY_POS` takes `tl` (default), `tr`, `bl` or `br`.
  - **`APS5_PLAYGO_UNLISTED_CHUNKS`:** `installed` or `absent`. It only applies to dumps with neither `playgo-chunk.dat` nor `playgo-scenario.json`. Known cases: High on Life (PPSA15388) needs `installed`; Sifu (PPSA03001) uses `absent`.
  - **`VK_LOADER_DRIVERS_DISABLE='*dzn*'`:** disables Dozen. Never do this when Dozen is the only Vulkan driver.
  - **Fonts:**
    - AnyPS5 looks in `anyps5-fonts\` beside the exe, or in the folder named by `ANYPS5_SYSTEM_FONTS`.
    - Accepted Noto substitutes: `NotoSans-{Light,Regular,Medium,Bold}.ttf`, `NotoSansMono-*.ttf`, `NotoSansThai-*.ttf`, `NotoSansArabic-*.ttf` and `NotoSansCJK-*.ttc`.
    - Without fonts, a game that opens system font sets shows no text in them or stops at startup.
- `pal1000/mesa-dist-win`: the latest release is 26.2.4 (2026-10-04). `mesa3d-26.2.4-release-msvc.7z` is 70,257,286 bytes and `mesa3d-26.2.4-release-mingw.7z` is 57,022,686 bytes.
- No project references SharpCompress yet. The architecture doc already plans it for Mesa.
- **To confirm while writing the plan:**
  - which Mesa build (msvc or mingw) to pin;
  - the path of `lvp_icd.x86_64.json` inside the archive;
  - the URL and SHA-256 of each Noto file;
  - SharpCompress's license and current version, for the license check.

## Core

### Settings

`GameSettings` in `game.json` gains four fields. The schema stays at version 1: every field has a default, so older records read unchanged.

| Field | Type | Default | Values |
| --- | --- | --- | --- |
| `gpu` | string? | null (automatic) | Any non-empty name |
| `lavapipe` | bool | false | |
| `language` | int | 1 | 0–30 |
| `presentMode` | string | `fifo` | `fifo`, `fifo_latest_ready`, `mailbox`, `immediate` |
| `disableDozen` (new) | bool | false | |
| `overlay` (new) | bool | false | |
| `overlayPosition` (new) | string | `tl` | `tl`, `tr`, `bl`, `br` |
| `playGoUnlistedChunks` (new) | string? | null | null, `installed`, `absent` |

**`GameSettingsValidator.Normalize(settings)`:**

- Replaces each out-of-range value with its default and returns what it replaced.
- An empty or whitespace `gpu` becomes null.
- `GameLibrary` applies it when reading a record and logs a warning per replaced field. It doesn't rewrite the file.
- A hand-edited `game.json` therefore never makes AnyPS5 stop on an invalid value.

**`GameLanguages`:** a fixed table of the 31 languages (number → English name), taken from AnyPS5's docs.

**`GameLibrary.SaveSettingsAsync(GameLayout, GameSettings, ct)`:**

- Holds the game lock (`GameBusyException` if busy).
- Refuses a running game (`GameRunningException`).
- Re-reads `game.json` and replaces only `settings`.

### Launch environment

`LaunchEnvironment.For(GameSettings settings, string? lavapipeManifest, string? fontsDirectory)` is a pure function. It returns the variables to add to the game process.

| Condition | Variables |
| --- | --- |
| `lavapipe` | `VK_DRIVER_FILES=<lavapipeManifest>`, `ANYPS5_GPU=llvmpipe` |
| not `lavapipe` and `gpu` set | `ANYPS5_GPU=<gpu>` |
| not `lavapipe` and `disableDozen` | `VK_LOADER_DRIVERS_DISABLE=*dzn*` |
| `language` ≠ 1 | `ANYPS5_LANGUAGE=<n>` |
| `presentMode` ≠ `fifo` | `ANYPS5_PRESENT_MODE=<mode>` |
| `overlay` | `ANYPS5_OVERLAY=1`, and `ANYPS5_OVERLAY_POS=<pos>` when it is not `tl` |
| `playGoUnlistedChunks` set | `APS5_PLAYGO_UNLISTED_CHUNKS=<value>` |
| `fontsDirectory` not null | `ANYPS5_SYSTEM_FONTS=<fontsDirectory>` |

- Default values are omitted, so a game with default settings and no fonts gets no variables.
- `lavapipe` with a null `lavapipeManifest` is a programming error (`ArgumentException`).
- `GameLaunchRequest` gains `IReadOnlyDictionary<string, string> Environment`.
- The Windows launcher adds these variables to the inherited environment of the game process only.

### Mesa and fonts

**`MesaTool`:**

- An `ArchiveTool` for the pinned Mesa 7z in `tools\mesa`, with its URL and SHA-256 in the profile (`tools.mesa`).
- 7z extraction uses SharpCompress, a new dependency.
- It exposes `LavapipeManifest`, the full path of `lvp_icd.x86_64.json`.

**`FontPack`:**

- Installs the profile's `fonts` list (file name, URL, SHA-256) into `<root>\fonts`:
  - Sans Light/Regular/Medium/Bold;
  - Mono, Thai and Arabic, Regular and Bold;
  - Sans CJK as `.ttc`.
- Each file is downloaded and verified through the existing downloader.
- A `.complete` marker is written last.
- `IsInstalled` means the marker exists.
- Progress is reported over the total size.

**`LaunchPreparation.PrepareAsync(settings, progress, ct)`** runs before the game lock, because a download can take minutes. It returns `(string? LavapipeManifest, string? FontsDirectory, string? FontsWarning)`.

- **Lavapipe on, Mesa missing:** install Mesa. A failure throws `LavapipeUnavailableException("Could not download Lavapipe: …")` and the launch stops.
- **Fonts missing:** install them. On failure, `FontsDirectory` is null and `FontsWarning` says "Fonts could not be downloaded (…); games that use system fonts may show no text or stop." The launch goes on.
- **Cancellation:** stops the download and the launch. A partial download is resumed next time, as the downloader already does.

### GamePlayer

`LaunchAsync(titleId, progress, ct)`:

1. Checks that AnyPS5 is installed.
2. Reads the record (a reader: no lock) and runs `LaunchPreparation` with its settings.
3. Takes the game lock, re-reads the record, and runs the 7a checks (running, executable).
4. If the re-read settings have Lavapipe on and Mesa is not installed, throws `LavapipeUnavailableException`. This happens when Lavapipe was turned on between the preparation and the lock.
5. Builds `LaunchEnvironment` from the re-read settings, launches, then calls `MarkPlayedAsync`, then records in `running.json`.

- A failed `MarkPlayedAsync` is logged and doesn't stop the game.
- `PlayingGame` gains `FontsWarning`.

### Lavapipe on by default

When a game is added, the import runs the Vulkan probe that already exists. If the probe succeeds and no GPU has `SupportsAnyPS5`, the new record gets `lavapipe: true`. If the probe fails, the default stays false. Reconvert keeps the existing settings, as it already does.

### PR #9 follow-ups folded in

- **`JsonFiles.WriteAsync`:** retries the temp-file `File.Move(overwrite)` up to 5 times, 50 ms apart, on an `IOException` that is a sharing violation or a lock violation (HResult `0x80070020` or `0x80070021`). After the last attempt, the exception is rethrown.
- **Played only after a start:** `lastPlayedAt` is written after `Launch` returns (GamePlayer step 5).
- **Breakaway from job:**
  - The Windows launcher starts the game with `CREATE_BREAKAWAY_FROM_JOB`.
  - If `CreateProcess` fails with `ERROR_ACCESS_DENIED` because the parent's job forbids breakaway, it retries once without the flag and logs it.
  - PS5Kitsune's own kill-on-close job is unchanged: the game is still never assigned to it.
- **Exit code 259:** `Attach` and the running checks decide whether the process has exited by waiting on its handle with a zero timeout, not by comparing the exit code with `STILL_ACTIVE`.

## App

### Settings page

`GameSettingsViewModel` is opened from "Settings" in the tile's "…" menu (the menu gets this new entry) and from a "Settings" button on the Game page. Back returns to the page it came from.

- **Header:** the game name. While the game runs, the page says "Running: changes can't be saved until it stops". Every field and Save are disabled then, and Back still works.
- **GPU:**
  - The list has "Automatic", then each GPU the Vulkan probe found. The probe runs when the page opens, and "Detecting GPUs…" shows meanwhile.
  - A GPU without `SupportsAnyPS5` is labelled "(not supported)" and can still be picked.
  - A saved GPU that isn't detected is kept in the list as "<name> (not found)".
  - If the probe fails, the list holds "Automatic" and the saved value, and the page shows "Could not detect GPUs: …".
- **CPU rendering (Lavapipe):** a checkbox with the text "Slow but works on any PC. Mesa (~70 MB) is downloaded on the first launch." When it is checked, GPU and Disable Dozen are disabled.
- **Game language:** the 31 names from `GameLanguages`.
- **Vertical sync:**
  - "On" (`fifo`).
  - "Latest frame" (`fifo_latest_ready`).
  - "Mailbox" (`mailbox`).
  - "Off, may tear" (`immediate`).
- **Disable Dozen:** shown only when the probe found a Dozen GPU and at least one other GPU. A saved true is kept when the field is hidden.
- **Performance overlay:** a checkbox, and a position picker (top left, top right, bottom left, bottom right) enabled only when the box is ticked. The help text says "F10 toggles it in game."
- **Advanced** (collapsed): PlayGo unlisted chunks. The choices are "Default", "Installed (High on Life needs this)" and "Absent (Sifu uses this)". The help text says it only matters for dumps without PlayGo files.
- **Buttons:**
  - **Save** calls `SaveSettingsAsync`. It shows "Saved" or the error: "<name> is busy with another action. Try again." or "<name> is running.".
  - **Reset to defaults** fills the defaults without saving.
  - **Back** asks "Discard changes?" when the fields differ from the saved settings.

### Game page

- Before the process starts, the status shows "Downloading Lavapipe… 34 MB / 70 MB" or "Downloading fonts… 52 MB / 120 MB", with a Cancel button. Cancel ends the launch, and the page shows "Launch cancelled" and Launch again.
- A `FontsWarning` is shown as a warning line above the log.
- `LavapipeUnavailableException` is shown through `LaunchErrors` with Launch again.
- The page gets the "Settings" button.

### Library

The tile's "…" menu gets "Settings" after "Launch". Nothing else changes.

## CLI

- **`run`:** uses the same preparation, with download progress printed like `install` prints it. A font warning is printed before the game starts. `LavapipeUnavailableException` prints its message and exits with the existing error code.
- **`ps5kitsune settings <titleId>`:**
  - It prints one line per setting, its value, and "(default)" when it has the default value.
  - Then it prints "Environment at launch:" and each variable, or "(none)".
  - The Lavapipe manifest path is printed even when Mesa is not installed yet, followed by "(downloaded on first launch)".
  - The `ANYPS5_SYSTEM_FONTS` line is printed the same way.
  - An unknown title ID gives the existing "not found" error.

## Errors

| Situation | Message |
| --- | --- |
| Mesa download or extraction fails | Could not download Lavapipe: <reason>. |
| Lavapipe on, Mesa missing after preparation | Lavapipe is not installed. Launch again to download it. |
| Font download fails | Fonts could not be downloaded (<reason>); games that use system fonts may show no text or stop. (warning, the launch goes on) |
| Save while running | <name> is running. Stop it to change its settings. |
| Save while the lock is held | <name> is busy with another action. Try again. |
| Probe fails on the Settings page | Could not detect GPUs: <reason>. |

## Tests

- `LaunchEnvironment`: a table test with one row per condition, plus all defaults → empty, Lavapipe overriding GPU and Dozen, and a null manifest with Lavapipe on throwing.
- `GameSettingsValidator`: each invalid value → its default, with the replaced-field list; an old record without the new fields reads with the defaults.
- `SaveSettingsAsync`: keeps other fields (including a `lastPlayedAt` written in between), refuses busy and running games.
- `LaunchPreparation`, with fake tools:
  - Mesa needed and missing → installed;
  - Mesa failing → exception;
  - fonts failing → warning, not an exception;
  - cancellation;
  - nothing to download → no install calls.
- `GamePlayer`:
  - the environment passed to the fake launcher;
  - Lavapipe turned on between preparation and lock → `LavapipeUnavailableException`;
  - a failed launch leaves `lastPlayedAt` unchanged.
- `MesaTool`: install from a small 7z fixture and resolve `LavapipeManifest`.
- `FontPack`:
  - install with a fake downloader;
  - the marker is written last;
  - a failed file leaves no marker.
- `JsonFiles.WriteAsync`: a destination held open is retried and succeeds once released; a destination held past the retries throws.
- Import: no supported GPU → `lavapipe: true`; probe failure → false.
- `GameSettingsViewModel`, with a fake probe:
  - the GPU list (supported, not supported, not found);
  - probe failure;
  - running → read-only;
  - Lavapipe disabling GPU and Dozen;
  - Dozen visibility;
  - Reset;
  - the discard prompt;
  - save errors.
- Game page: preparation progress, cancellation, font warning.
- CLI: `settings` output for defaults and for a full set of values.
- Windows-only tests:
  - breakaway, by starting a process from inside a job that allows breakaway and checking it is not in the job;
  - exit detection for a process that exits with code 259.

## Criterion

By hand:

1. On the Intel Iris Xe laptop, open DOOM's Settings, turn on CPU rendering and save.
2. One click on DOOM: Mesa and the fonts download with progress, then DOOM starts, and the log names llvmpipe as the device.
3. Stop it and launch again: nothing is downloaded.
4. `ps5kitsune settings <DOOM's title ID>` shows `VK_DRIVER_FILES` and `ANYPS5_SYSTEM_FONTS`.
5. On the AMD laptop, DOOM with default settings still runs on its GPU.

## Docs

- **`docs/architecture.md`:**
  - the settings table gains the new fields and the "default omitted" rule;
  - the `game.json` example shows the new fields;
  - Mesa and fonts get the pinned versions;
  - the bindings editor becomes "later";
  - "Still to confirm: Mesa 7z format" is removed.
- **README:** adds `settings`.
