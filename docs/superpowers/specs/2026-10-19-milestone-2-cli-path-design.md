# Milestone 2 — end-to-end CLI path: design

October 19, 2026 · Complements [docs/architecture.md](../../architecture.md) (milestone 2 of the roadmap).

## Goal

From the command line only, on Windows with the release mode of profile `doom-validated-2026-10` (AnyPS5 `v0.1.2`):

```text
ps5kitsune install
ps5kitsune add <DOOM folder or PPSA99666.zip>
ps5kitsune run PPSA99666
```

runs DOOM for PS5. The milestone also tells whether release `v0.1.2` is enough or a build is needed (see "If DOOM fails").

**Out of scope:** build mode (milestone 5), PC checks (milestone 3), Reconvert and `--move` (milestone 6), per-game settings, error diagnosis, Lavapipe, fonts and `library.json` (milestone 7), any screen.

## Facts checked on 2026-10-19

| Point | Finding |
| --- | --- |
| DOOM for PS5 | `lowbit/ps5-doom` release `v1.1.1` ships `PPSA99666.zip` (+ `.sha256`). The zip holds a single `PPSA99666/` folder with `eboot.bin`, `sce_module/libc.prx`, `sce_sys/param.json`, `sce_sys/icon0.png`, `wads/`, `licenses/`; no `app0`. |
| DOOM binaries | `eboot.bin` and `sce_module/libc.prx` are SELF containers (first bytes `4F 15 3D 1D`), so unwrapping is exercised. |
| `param.json` | `titleId` = `PPSA99666`, `localizedParameters.defaultLanguage` = `en-US`, `localizedParameters.en-US.titleName` = `DOOM`. |
| Release `v0.1.2` assets | `relinker-v0.1.2.exe` and `prx-windows-v0.1.2.zip`; their API `digest` values match the profile hashes. |
| Source archive of `v0.1.2` | `https://github.com/boykopovar/AnyPS5/archive/refs/tags/v0.1.2.zip`, SHA-256 `90c802352b5478beacfdf032b57649872ccf0b7dc18c9c868b35903378ad2545`, top folder `AnyPS5-0.1.2/`; the tag points to commit `a48c00660eae7afd204fdb7513a52380dddf5a04`. |
| `import_audit.py` in `v0.1.2` | Present; `--libs` (required, repeatable), `--modules`, `--source` (default `<script>/../core/libs/prx`), `--json`; exit codes 0 / 1 / 2. |
| `unwrap_self.py` | **Not in `v0.1.2`.** Added later, standard library only, fixed several times a day. Pinned at commit `360e8bd685d32b0e9d37e6ee2fae44417c0f808c`, SHA-256 `2f9321fa41009411b9cd07730f68094967c80a6c0c31d801b31d55141c291ef7`. Usage: `unwrap_self.py <input> <output> [--verbose]`. |
| Embeddable Python and sibling imports | `import_audit.py` imports `nid_names` and `progress` from `tools/`. The `python314._pth` file keeps the script folder off `sys.path` and disables `PYTHONPATH`, so run directly the script fails with `ModuleNotFoundError` (checked 2026-10-20 with 3.14.8). Scripts are run as `python -c <bootstrap> <script> <args>`; the bootstrap puts the script folder first and calls `runpy.run_path`, keeping the exit code. |
| SELF magics | `unwrap_self.py` accepts `4F 15 3D 1D` and `54 14 F5 EE`. |
| Relinker module output | Relinker v0.1.2 writes each converted bundled module as a PE (`MZ`) named `<module>.guest.prx` beside the original in `app0/sce_module`, where the game loads it. `import_audit.py` reads every `--modules` file as an ELF, so `AuditStep` passes a copy of the ELF modules only, in `.ps5kitsune/audit-modules/` (found 2026-10-20 with DOOM). |
| Test machine | AMD Ryzen 7 7730U, Radeon iGPU, 15.4 GB RAM: no `--to-intel`; the commit limit may require a larger page file. |

## Profile changes

Two assets are added; the relinker and libs entries are unchanged.

```json
"assets": {
  "relinker": { "name": "relinker-v0.1.2.exe", "sha256": "88cc311d…" },
  "libs":     { "name": "prx-windows-v0.1.2.zip", "sha256": "b26e507e…" },
  "scripts": {
    "url": "https://github.com/boykopovar/AnyPS5/archive/refs/tags/v0.1.2.zip",
    "sha256": "90c802352b5478beacfdf032b57649872ccf0b7dc18c9c868b35903378ad2545",
    "include": [ "tools/", "core/libs/prx/" ]
  },
  "unwrapScript": {
    "url": "https://raw.githubusercontent.com/boykopovar/AnyPS5/360e8bd685d32b0e9d37e6ee2fae44417c0f808c/tools/unwrap_self.py",
    "sha256": "2f9321fa41009411b9cd07730f68094967c80a6c0c31d801b31d55141c291ef7"
  }
}
```

`ProfileAsset` gains an optional `Url` (release assets keep resolving from `source.tag` and `name`) and an optional `Include` list. Release asset URLs are built as `https://github.com/boykopovar/AnyPS5/releases/download/<tag>/<name>`, so no GitHub API call is needed. If GitHub regenerates the source archive and its hash changes, `install` fails with a checksum error and the profile is updated.

## Components

### Pipeline (`Core/Pipeline/`)

- `IStep`: `Name`, `bool IsAlreadyDone()`, `Task ExecuteAsync(IProgress<StepProgress>?, CancellationToken)`.
- `StepRunner`: runs a list of steps in order; skips those already done; logs start, end and duration; stops at the first failure and reports the step name; raises events (started, skipped, completed, failed) the CLI prints and the UI will reuse.
- `StepProgress`: optional message and optional `DownloadProgress`.

### AnyPS5 install (`Core/AnyPS5/`) — `ps5kitsune install`

Steps, all writing under `<root>/anyps5/current/`:

1. `InstallPythonStep`: wraps the existing `PythonEmbeddableTool`.
2. `DownloadRelinkerStep`: `relinker-v0.1.2.exe` → `relinker.exe`, verified by SHA-256.
3. `DownloadLibsStep`: `prx-windows-v0.1.2.zip` extracted so that its `libs/` folder becomes `libs/`.
4. `DownloadScriptsStep`: source archive; only entries under the `include` prefixes (after stripping the top folder) are extracted into `scripts/`, keeping the tree, so `import_audit.py` finds `../core/libs/prx` by default.
5. `DownloadUnwrapScriptStep`: `unwrap_self.py` → `scripts/tools/unwrap_self.py`.
6. `WriteInstallRecordStep`: `install.json` (`schemaVersion`, profile id, mode `release`, version, unwrap script URL holding the commit, date).

`IsAlreadyDone()` checks outputs: a downloaded file present with the right hash, or, for an extracted archive, a `.ps5kitsune-extracted` marker holding the archive hash. `IAnyPS5Source` is not created yet: there is a single mode until milestone 5. `DataLayout` gains `AnyPS5Current`, `Games`, `Staging` and per-game paths.

### Add a game (`Core/Games/`) — `ps5kitsune add <folder|zip>`

- `GameSourceResolver`: a `.zip` is extracted to `<root>/downloads/staging/<zip name>/`. The game root is the folder holding `eboot.bin`: the input itself, its `app0`, or its single subfolder. Anything else is a blocking error.
- `GameValidator` (nothing is copied before it passes): `eboot.bin` present; `sce_sys/param.json` with a non-empty `titleId` and a non-empty `localizedParameters` object; name = `titleName` of `defaultLanguage`, else `en-US`, else the first language; module folder `sce_module`, `sce_modules` or `prx` (`sce_module` + `sce_modules` together is blocking; none of them is a warning, and `--module-dir .` is not added in this milestone); free space at the destination ≥ game size. Returns all blocking errors and warnings at once.
- `CpuVendor`: vendor string from `X86Base.CpuId` leaf 0. Intel enables `--to-intel`. The full PC checks stay in milestone 3.
- `GameImportContext`: source root, title ID, name, game folder, `toIntel`, registries produced, audit result. Steps read from and add to it.

Steps:

1. `CopyGameStep`: copy the game root into `<root>/games/<titleId>/app0/` as-is. Done when every source file exists at the destination with the same size.
2. `CopyLibsStep`: copy `anyps5/current/libs/` into `<game>/libs/`.
3. `UnwrapSelfStep`: for `app0/eboot.bin` and every file of the module folders, read the first 4 bytes. For each SELF file (either magic above), back it up to `<game>/.ps5kitsune/originals/<relative path>` (outside `app0`), then run `unwrap_self.py <backup> <original path>.partial` and move the output to the original path only after exit 0 (a failed run deletes it). Other non-ELF files are left as they are. A refusal (encrypted container) fails with "the game must come already unwrapped from the dump tool". Done when no SELF file remains and no backup lacks its unwrapped copy.
4. `RelinkStep`: from the game folder, `relinker --windows --registry [--to-intel] app0/eboot.bin <titleId>.exe` with options from the profile. Exit 0 succeeds; 1 or 2 fails with the last stderr lines, naming `--to-intel` when it was used. Before running, it deletes the previous outputs (marker, executable, `*.registry.json`, `*.needed.json`, `app0/**/*.guest.prx`). Done when the success marker `.ps5kitsune/relinked`, written after exit 0, exists along with `<titleId>.exe` and `<titleId>.registry.json` (this only matters when resuming a partial add).
5. `AuditStep`: `python scripts/tools/import_audit.py <all *.registry.json> --libs libs --modules <ELF-only copy of each module folder> --json audit.json` from the game folder. Exit 0 or 1 → read `audit.json` and compute the verdict (green: absent = 0 and stub = 0; orange: absent = 0, stub > 0; red: absent > 0 or a missing library). Exit 2 fails.
6. `WriteGameRecordStep`: `<game>/.ps5kitsune/game.json` as in the architecture, `settings` with defaults.

If `game.json` already exists, `add` refuses ("already added", with the folder to delete to start over). Without `game.json`, `add` resumes at the first step not done. After a successful copy, the staging folder of a zip is deleted; it is kept on failure.

### Run (`Core/Library/`, `Platform.Windows`) — `ps5kitsune run <titleId>`

- `GameRecordStore`: reads and writes `game.json` (`System.Text.Json`, `schemaVersion` checked). `run` resolves `<root>/games/<titleId>/` directly.
- `IGameLauncher` (Core) / `WindowsGameLauncher` (Platform.Windows): opens `<game>/.ps5kitsune/logs/<yyyyMMdd-HHmmss>.log`, starts `<titleId>.exe` with `CreateProcess`, the log handle inherited as stdout and stderr, working directory = game folder, `CREATE_NO_WINDOW`, no job object. Returns the PID and log path. Keeps the 10 newest logs.
- The CLI follows the log file (tail) to the console until the process exits, then prints the exit code and returns it. Ctrl+C stops following, not the game, and prints the PID and log path. `lastPlayedAt` is updated at launch.

## Errors

| Case | Behavior |
| --- | --- |
| `install` not done | `add` and `run` stop: "run `ps5kitsune install` first" |
| Checksum mismatch | Step fails, file deleted, expected and actual hashes shown |
| Blocking validation | All errors listed, nothing copied |
| `unwrap_self.py` refuses | "The game must come already unwrapped from the dump tool" |
| Relinker exit 1 or 2 | Step fails with the last stderr lines |
| Audit exit 2 | Step fails (unreadable input) |
| Red verdict | Game still recorded, with a warning |
| Unknown title ID at `run` | "No game PPSA… in <games folder>" |
| Game exe missing at `run` | "File deleted, possibly by an antivirus" |
| Ctrl+C | `install` / `add`: cancelled, exit 130, resumable. `run`: stops following only |

CLI exit codes: 0 success (`add` even with an orange or red verdict, which is printed), 1 failure, 130 cancelled; `run` returns the game's exit code. Each command keeps its log in `<root>/logs/`, with the relinker and script output copied line by line (including `Guest module:` and `Executable file:`).

## Tests

Unit tests (xUnit, fake `IProcessRunner`, temporary folders; no DOOM file in the repository, fixtures use a few bytes with ELF or SELF magic):

- `StepRunner`: skip when done, stop on failure, cancellation, events.
- `GameSourceResolver`: plain folder, folder with `app0`, single subfolder, zip with a top folder, flat zip, no `eboot.bin`.
- `GameValidator`: name resolution order, invalid `param.json`, module folder rules.
- Add steps: exact relinker arguments (with and without `--to-intel`), audit arguments with all registries, backup path, exit code handling.
- Verdict computation from a sample `audit.json`; `game.json` round trip.
- Install steps: partial extraction of a small generated source archive, extraction marker.
- `WindowsGameLauncher` (Windows only, skipped on Linux in CI): launches `cmd /c echo` and checks the log content, working directory and exit code.

End-to-end validation by hand on the test machine: `install` on an empty root, `add PPSA99666.zip` (validated 2026-10-20: orange, 361 implemented, 3 stubs, 0 absent), `run PPSA99666` starts DOOM. The `--to-intel` path is only covered by unit tests (AMD machine).

## If DOOM fails

The first `run` tells whether `v0.1.2` is enough. If DOOM crashes with it, either milestone 5 (build mode) moves before the screens, or a manually built AnyPS5 is placed in `anyps5/current/` as a stopgap (running `install` again would then replace it, since the hashes no longer match the profile). `add` and `run` stay the same in both cases. On success, the profile gets `"validated": true` and the architecture's open decision "default mode of the first profile" is closed.
