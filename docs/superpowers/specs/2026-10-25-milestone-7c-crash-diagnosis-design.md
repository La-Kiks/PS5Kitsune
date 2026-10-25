# Milestone 7c — Crash diagnosis: design

October 25, 2026 · Complements [docs/architecture.md](../../architecture.md) ("V3 — Launcher", "Crash diagnosis") and the [milestone 7b spec](2026-10-24-milestone-7b-settings-lavapipe-fonts-design.md).

## Goal

After a game exits, search its log for the known AnyPS5 errors, explain the one found, and offer the fix that goes with it. A crash with no known cause gets a Report button that prepares a text for AnyPS5's Game bring-up Discussions. The patterns come from the profile, so a new AnyPS5 message only needs a profile update. Four PR #10 follow-ups are folded in.

**Criterion:** a deliberately provoked error 126 on the AMD laptop is recognized and repaired. See [Criterion](#criterion).

**Out of scope:**

- Diagnosis while the game runs. A game that hangs after an error is diagnosed once it is stopped.
- A diagnosis line in the Library. A crash seen after the fact is found with `ps5kitsune diagnose`.
- Fixes from the CLI. The CLI only prints, as `settings` does.
- Sending anything automatically. Report copies a text and opens a web page.
- **7d:** visual design, including the empty log box while the downloads run.

## Facts checked on 2026-10-25

All from the AnyPS5 v0.1.2 source archive (the release the embedded profile pins):

- **Errors 126 and 193:** the relinker's entry stub (`WindowsEntryStubBuilder.cpp`) writes to stderr, which the log captures:

  ```
  Loading PRX: <path> -> FAILED
  Failed to load module: <path>
  GetLastError: 126 - The specified module could not be found.
  ```

  The message after the number comes from `FormatMessageA` in US English. `USAGE.md` gives the causes: 193 is an original PS5 module placed in `libs`; 126 is one of `libgcc_s_seh-1.dll`, `libstdc++-6.dll` and `libwinpthread-1.dll` missing from `libs`, since the libraries are loaded without searching `PATH`.
- **Native crash report** (`CrashReport.cpp`):

  ```
  FATAL: unhandled exception 0x<code> on thread <id> '<name>'
    rip 0x<address> <module>+0x<offset>
  ```

  So the NVIDIA case is `unhandled exception 0xc0000005` with `nvgpucomp64.dll+` on the `rip` line, and an illegal instruction is `unhandled exception 0xc000001d`.
- **Fixed texts:**
  - GPU: `buffer device address and shaderInt64 are required` (`BdaFeatures.cpp`).
  - Metadata: `param.json not found` (`AppMetadata.cpp`).
  - Settings: `sceSystemServiceParamGetInt: ANYPS5_LANGUAGE must be a console language number from …` and `libSceFont: ANYPS5_SYSTEM_FONTS is not a directory: …`.
  - Memory (`USAGE.md`): `replace guest placeholder with resident direct memory`, `commit direct memory pages`, `keep unmapped direct memory`, `create direct memory backing`.
- **Unimplemented features** have no single message: several libraries end a line with `is not implemented` or `are not implemented`.
- **The exit code is not saved anywhere.** Only the Game page or `run`, whichever watched the exit, knows it.
- **`game.json`** records the install that converted the game (`convertedWith.mode`, `convertedWith.version`), and each install's `install.json` records its mode and version.
- **To confirm while writing the plan:** the other `param.json` errors in `AppMetadata.cpp` (an invalid file), the remaining `ANYPS5_*` error texts, and whether `CrashReport.cpp` runs for `0xc000001d` or the game only exits with that code.

## Core

### Profile `diagnostics`

The profile gains a section:

```json
"diagnostics": {
  "reportUrl": "https://github.com/boykopovar/AnyPS5/discussions/categories/game-bring-up",
  "rules": [
    { "id": "nvidia-shader-compiler", "log": ["unhandled exception 0xc0000005", "rip 0x[0-9a-f]+ nvgpucomp64\\.dll\\+"] },
    { "id": "libs-not-dll", "log": ["Failed to load module: (?<module>.+)", "GetLastError: 193 "] },
    { "id": "libs-missing-dll", "log": ["Failed to load module: (?<module>.+)", "GetLastError: 126 "] },
    { "id": "commit-limit", "match": "any", "log": ["replace guest placeholder with resident direct memory", "commit direct memory pages", "keep unmapped direct memory", "create direct memory backing"] },
    { "id": "gpu-features", "log": ["buffer device address and shaderInt64 are required"] },
    { "id": "cpu-too-old", "match": "any", "log": ["unhandled exception 0xc000001d"], "exitCodes": ["0xC000001D"] },
    { "id": "param-json", "log": ["param\\.json not found"] },
    { "id": "invalid-setting", "log": ["(?<variable>A(?:NY)?PS5_[A-Z_]+) (must|is not)"] },
    { "id": "not-implemented", "log": ["(is|are) not implemented"] }
  ]
}
```

- **A rule:**
  - `id`: names the explanation and the fix in the app.
  - `log`: .NET regexes, each one searched in every line.
  - `exitCodes`: hex strings, compared with the exit code as a 32-bit unsigned value.
  - `match`: `all` (the default) needs every `log` regex to match somewhere in the log, and, when `exitCodes` is given, one of those codes. `any` needs one of them.
- **Validation:**
  - A rule needs at least one of `log` and `exitCodes`.
  - Every regex must compile. Regexes are compiled once, when the profile loads, with a 1-second match timeout.
  - An invalid rule makes the profile invalid, so an override with one is ignored with a warning, like any invalid override.
  - A missing `diagnostics` section is valid and means no rules.
- **Named groups** (`module`, `variable`, `function`) are captured for the explanation. The first match of a group wins.
- **Order matters:** the first rule whose conditions are met wins. The most specific rules come first (NVIDIA before the generic crash rules, 193 and 126 before the rest).

### Exit line

After the game exits, whoever watched the exit (the Game page or `run`) appends one line to that launch's log:

```
[ps5kitsune] exited with code 3221225477 (0xC0000005)
```

The game has released the file by then. A failed write is logged and changes nothing else. A game whose exit nobody watched (the app was closed) has no exit line.

A game ended with Stop gets `[ps5kitsune] stopped by the user (exit code …)` instead, and is never diagnosed. Stop kills the process, so its exit code says nothing about a crash.

### CrashDiagnoser

`CrashDiagnoser.DiagnoseAsync(string logPath, uint? exitCode, CancellationToken)` returns a `Diagnosis?`:

- It reads the log line by line, off the UI thread.
- With `exitCode` null, it uses the exit line when the log has one.
- A log whose last line is the "stopped by the user" line returns null, and so does a call from the Game page after Stop.
- It returns the first rule, in profile order, whose conditions are met.
- With no match: a non-zero exit code returns the `unexplained` diagnosis. A zero or unknown exit code returns null.
- A rule whose `id` the app does not know is skipped, with a warning in the log (once per profile load).
- `Diagnosis`: `Id`, `Captures` (name → value), `Line` (the first matched line, trimmed to 300 characters), `LineNumber`, `ExitCode`, `LogPath`.

### Fixes

- **`LibsRepair.RepairAsync(titleId)`:**
  - It takes the game lock and refuses a running game, with the existing exceptions.
  - It reads `convertedWith` from `game.json` and finds the install of that mode. If that install is missing, or its version differs from `convertedWith.version`, it throws `LibsSourceChangedException`: "The {mode} install has changed since {name} was converted. Reconvert it instead."
  - It deletes the game's `libs` and copies the install's `libs` again, with the same filter as `CopyLibsStep`.
- **Turn on CPU rendering and Reset {setting}** use `GamePlayer.SaveSettingsAsync`.
  - Variable → setting: `ANYPS5_GPU` → GPU, `ANYPS5_LANGUAGE` → language, `ANYPS5_PRESENT_MODE` → vertical sync, `ANYPS5_OVERLAY` and `ANYPS5_OVERLAY_POS` → overlay and its position, `APS5_PLAYGO_UNLISTED_CHUNKS` → PlayGo.
  - Any other variable, `ANYPS5_SYSTEM_FONTS` included, has no reset.
- **`CrashReport.Build(...)`** returns the report text:
  - PS5Kitsune version, Windows version;
  - CPU, GPUs with driver versions, installed RAM and commit limit;
  - AnyPS5 mode and version, profile id;
  - title ID, name, audit counts;
  - the settings that are not at their default;
  - exit code, diagnosis id, matched line, the last 30 log lines;
  - the log path.

  It contains no other full path.

### PR #10 follow-ups folded in

- **Preparations run one at a time:** `<root>\tools\.prepare.lock`, opened with `FileShare.None` and released when its handle closes, covers the Mesa and font preparation across processes. A second launch retries every 500 ms and reports `LaunchProgress` "Waiting for another launch to finish its downloads…". Cancellation works while waiting.
- **Leftovers:**
  - After the fonts install, `*.part` files left in `<root>\fonts` are deleted.
  - After a successful extraction, the Mesa 7z is deleted from `downloads`.
- **Breakaway wording:**
  - When `CREATE_BREAKAWAY_FROM_JOB` is refused with error 5, the log says "Starting the game inside the launcher's job (breakaway refused)." with no claim about why.
  - If the retry fails too, the retry's error is the one reported.

## App

### Game page

- After an exit it watched, the page runs the diagnoser on the launch's log. The exit code shows as before, while the diagnosis runs.
- A match shows a panel under the status line:
  - a title and the explanation;
  - the matched line, in monospace;
  - the action button, if any.
- After a fix, the panel says what was done, for example "Libs repaired. Launch again to check.", and Launch again works as before.
- A fix runs off the UI thread, and its button is disabled while it runs. A failed fix shows its message in the panel.
- Launch again hides the panel.

| id | Title | Explanation | Action |
| --- | --- | --- | --- |
| `libs-missing-dll` | A library could not load | {module} needs a DLL that is missing from the game's libs folder. | Repair libs |
| `libs-not-dll` | A file in libs is not a Windows DLL | {module} is not a Windows library. Repair libs copies the folder again from the AnyPS5 install. | Repair libs |
| `commit-limit` | Windows ran out of memory to commit | The page file plus RAM is too small for this game. Enlarge the page file, or close other programs such as the browser. | Open page file settings |
| `gpu-features` | No GPU here supports AnyPS5 | The graphics driver lacks buffer device address or shaderInt64. CPU rendering works on any PC, more slowly. | Turn on CPU rendering |
| `nvidia-shader-compiler` | Known NVIDIA driver crash | NVIDIA's shader compiler (nvgpucomp64.dll) crashed. Driver 610.47 is known to do this. | Open NVIDIA drivers page |
| `cpu-too-old` | This CPU is not supported | The CPU lacks instructions AnyPS5 needs. | None |
| `param-json` | The game's metadata is missing | sce_sys\param.json is missing or damaged. Remove the game and add it again from its original folder. | Open folder |
| `invalid-setting` | A setting has a value AnyPS5 refuses | {variable} has a value AnyPS5 refuses. | Reset {setting}, or none |
| `not-implemented` | AnyPS5 does not handle this game yet | The game uses a feature AnyPS5 has not implemented. | Report |
| `unexplained` | The game stopped with an error | AnyPS5 stopped with an error it does not explain yet (exit code {code}). | Report |

- **Actions:**
  - **Open page file settings** runs `SystemPropertiesPerformance.exe`.
  - **Open NVIDIA drivers page** opens `https://www.nvidia.com/en-us/drivers/`.
  - **Open folder** is the existing action.
  - **Report** copies the report text to the clipboard, then opens `reportUrl`. The panel says "Report copied. Paste it in a new thread for {title ID}."

### Settings page

When the page opens on a running game, it attaches to the process and clears "running" when the game exits, so Save comes back without reopening the page.

## CLI

- **`run`:** after the exit, it appends the exit line, runs the diagnoser and prints:
  - the title and the explanation;
  - `Line {n}: {line}`;
  - the fix in words, for example `Fix: in the app, open DOOM and choose Repair libs.` For Report: `Fix: run ps5kitsune diagnose {titleId} --report and paste the text in a new Game bring-up thread.`

  The exit code of `run` stays the game's.
- **`ps5kitsune diagnose <titleId> [--report]`:**
  - It diagnoses the game's newest log and prints the same lines, or "No known error in {log}."
  - `--report` prints the report text instead.
  - It exits with 0 whether or not something matched, and with 1 for an unknown game or a game with no log.

## Errors

| Situation | Message |
| --- | --- |
| Log unreadable, or a regex times out | Warning in the log; no diagnosis shown |
| Repair libs, install changed or missing | The {mode} install has changed since {name} was converted. Reconvert it instead. |
| Repair libs fails | Could not repair libs: {reason} |
| Any fix while the game runs or is busy | The existing running and busy messages |
| Opening a program or page fails | Could not open {what}: {reason} |
| Clipboard fails | Could not copy the report: {reason}. Use `ps5kitsune diagnose {titleId} --report`. |

## Tests

- **Rules:**
  - Each of the 9 rules against a sample log built from the v0.1.2 source texts above, with the expected captures.
  - Order: a log with both the NVIDIA crash and a generic line gives `nvidia-shader-compiler`.
  - `all` against `any`, and exit codes alone and combined.
  - `unexplained` for a non-zero code, null for zero and for an unknown code.
  - The exit line read back when no code is passed, and a "stopped by the user" log giving null.
  - An unknown `id` is skipped.
- **Profile:**
  - A bad regex, or a rule with neither `log` nor `exitCodes`, makes the profile invalid.
  - The embedded profile loads with its 9 rules.
- **`LibsRepair`:** repairs a temporary game folder from the recorded install, refuses a running or busy game, and throws when the install is missing or has another version.
- **`CrashReport`:** has the expected fields, and no full path other than the log's.
- **Elsewhere:**
  - the exit line;
  - the preparation lock (a second preparation waits, then runs; cancellation while waiting);
  - the `.part` and 7z cleanup;
  - the breakaway retry's error.
- **App view models:**
  - the Game page panel for each action;
  - fix success and failure;
  - Launch again hiding the panel;
  - the Settings page clearing "running" on exit.
- **CLI:** no tests, as there is no CLI test project.

## Criterion

By hand, on the AMD laptop:

1. Delete `libs\libwinpthread-1.dll` from DOOM's folder.
2. Launch DOOM from the Library. The Game page says a library could not load, names the module, and offers Repair libs.
3. `ps5kitsune diagnose PPSA99666` prints the same diagnosis.
4. Repair libs, then Launch again: DOOM runs.

## Docs

- **`docs/architecture.md`:**
  - The crash-diagnosis table gets the ids, the profile section, the exit line and the actions.
  - The roadmap's 7c entry gets this spec's link.
  - The 7b criterion line becomes "DOOM launched in one click with Lavapipe forced on", since neither test laptop lacks a supported GPU.
- **README:** adds `diagnose`.
