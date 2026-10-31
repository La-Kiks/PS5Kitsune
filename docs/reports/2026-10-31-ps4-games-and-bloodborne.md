# PS4 games in PS5Kitsune: AnyPS5 status and the Bloodborne PC port

Date: 2026-10-31. Trigger: adding Bloodborne (`S:\Games\PS5 games\BloodBorne\CUSA03173`) fails in PS5Kitsune with "sce_sys/param.json is missing".

## Summary

- **AnyPS5 v0.1.4 cannot run PS4 games yet, but PS4 support is actively in progress upstream.** Much of the groundwork is merged: the relinker fixes for PS4 executables, the PS4 (GCN) shader decoder, PS4 GPU targets and tiling, and PS4 display buffers. The missing piece is the PS4 graphics driver `libSceGnmDriver` ([#9432](https://github.com/boykopovar/AnyPS5/pull/9432)), which is still an open PR with merge conflicts (`mergeable_state: dirty`, last updated 2026-10-31). With the whole stack, a contributor runs **Limbo (CUSA01664) in game at 60 fps**. No one has reported Bloodborne running on AnyPS5 yet: it converts, but it does not start.
- **The Bloodborne PC port ([Supermedo/bloodborne_pc](https://github.com/Supermedo/bloodborne_pc)) is a different kind of project:** a port made for one game, built on deadinside28's **bbport**, which hand-writes only the PS4 libraries Bloodborne calls and reuses **shadPS4's renderer**. It isn't a general solution, and PS5Kitsune can't reuse it to run other games.
- **The dump is usable by both:** decrypted (AnyPS5's `unwrap_self.py` unwraps it into a valid ELF), version **01.09** (`APP_VER`), content ID `EP9000-CUSA03173_00-BLOODBORNE0000EU`, with the `SPEXPANSIONDLC03` (The Old Hunters) folder next to it.
- **For PS5Kitsune:** prepare for PS4 titles now (read `param.sfo`, recognize `CUSA` games, explain clearly why they can't be added yet), and enable them once AnyPS5 ships `libSceGnmDriver` in a release.

## 1. Does AnyPS5 handle PS4 games?

### What the installed v0.1.4 contains

Checked in `S:\Games\PS5Kitsune\anyps5\installs\release` (profile `doom-validated-2026-10`).

| Area | State in v0.1.4 | Evidence |
|---|---|---|
| Game metadata | **Supported.** Falls back to `param.sfo` when `param.json` is missing | `libkernel/AppMetadata/src/AppMetadata.cpp`: `parseParams()` tries `/app0/sce_sys/param.json`, then `/app0/sce_sys/param.sfo` |
| Unwrapping the SELF | **Works on this dump** | `unwrap_self.py eboot.bin out.elf` wrote a 93,670,840-byte ELF. The input magic is `4F 15 3D 1D`, a decrypted SELF |
| Relinker | Knows PS4 modules (`Guest module mixes PS4 and PS5 needed-module tags`) and has the PS4 fixes merged before the release | strings in `relinker.exe`; upstream PRs below |
| PS4 GPU data in the AGC driver | **Partly there:** PS4 (GCN) tiling, PS4 display buffers, PS4 color and depth targets, with several `PS4 … is not implemented` throws left | `libSceAgcDriver/Graphics/src/*.cpp`, `Execution/src/DisplayBuffer.cpp` |
| **`libSceGnmDriver` (PS4 graphics driver)** | **Absent** | No `libSceGnmDriver` folder in `core/libs/prx`. Only `libSceGnmDriverForNeoMode` (118 exports, PS4 Pro mode helpers) and `libSceGnmDriverCompat1` (0 exports) |
| Other PS4 libraries Bloodborne uses | Mostly present (Ajm, AudioOut, Pad, SaveData, Np*, PlayGo, Fios2, VideoOut, libkernel, libc) | Bloodborne's import names compared with the `core/libs/prx` folders. `libSceGnm`, `libSceGnmx`, `libSceGpuAddress`, `libSceShaderBinary` and the `*_stub_weak` names are compiled into the game, so they don't count |

### Upstream state (github.com/boykopovar/AnyPS5, checked 2026-10-31)

Releases: v0.1.4 (2026-10-30), v0.1.3 (10-20), v0.1.2 (10-14), v0.1.1 (09-28).

- **[#159](https://github.com/boykopovar/AnyPS5/issues/159)** "Does it support PS4 games?", closed. Its answer at the time: "theoretical; no one has tried it yet."
- **[#3093](https://github.com/boykopovar/AnyPS5/issues/3093)** (open): the tracking issue for PS4 titles, started with Bloodborne (CUSA00900 v1.09) and the OpenOrbis sample. Key points:
  - Relinker blockers: SCE dynamic tables, read-only data after the code, jump tables inside the code. These are mostly fixed by merged PRs, for example #6595, #7660 and #9429.
  - Get-fix, 2026-10-15: *"the relinker now converts two retail PS4 titles, Bloodborne (CUSA00900) and Limbo (CUSA01664) … They convert but do not start yet."* The audit at the time: **Bloodborne 686 imports, 498 implemented, 8 throwing stubs, 180 absent**, *"every `libSceGnmDriver` function is absent"*, and most of the rest are PSN libraries.
  - The checklist also lists *"PS4 titles ship `sce_sys/param.sfo`, not `param.json`"* and Bloodborne's other absent imports: 91 online (Np), 6 SaveData, 3 Ajm.
- **[#6977](https://github.com/boykopovar/AnyPS5/pull/6977)** (open draft integration branch): "run PS4 games on the AGC driver — Limbo (CUSA01664) in game". This is how the PS4 work is split:

  | Part | PR | What | State |
  |---|---|---|---|
  | A | #9429 | relinker: TLS relocations against the module's own section symbol | merged |
  | B | #9430 | PS4 (GCN) shaders re-encoded to RDNA, then run by the existing recompiler | merged 2026-10-22 |
  | C | #9431 | AGC: PS4 targets, tiling and pattern fills | merged 2026-10-29 (probably just before v0.1.4) |
  | **D** | **#9432** | **`libSceGnmDriver` and VideoOut** | **open, conflicts with `main`** |
  | E | #9433 | PS4 NpTrophy, save data v1, NpWebApi offline, Limbo's other imports | merged 2026-10-22 |

  Limbo test: *"a 60 s scripted run from start into gameplay with no errors and no not-implemented calls"* (RTX 5060 Ti, Windows 11).
- 298 merged PRs mention PS4 (for example #5991 PS4 display buffers, #8079 PS4 PlayGo chunk table, #12427 `sceKernelIsNeoMode` read from `param.sfo`, #12577 `sceKernelIsPs4Process`). The work is moving quickly.

**Conclusion:** AnyPS5 handles PS4 games **partially in v0.1.4 and not well enough to play any**. The first release that could play a PS4 game is the one that includes #9432. Bloodborne will probably need more after that, given its 180 absent imports and its use of the GPU, which is heavier than Limbo's.

## 2. How the Bloodborne PC port works (Supermedo/bloodborne_pc)

Sources: the README and `docs/original-readme/README.md` (bbport), `gpu/VENDOR.txt` and `docs/motion_vectors.md`, on the `windows` branch.

### Lineage

- **bbport** by deadinside28: a native Linux port of Bloodborne v1.09.
- **Supermedo/bloodborne_pc:** the Windows fork of it (about 5k stars, 792 commits), with a launcher, DLSS/FSR, frame generation and online play.
- Renderer: **shadPS4's** `src/video_core` and `src/shader_recompiler` (plus `sirit`, `gcn`, `half`), vendored at commit `b4e7ae7`. Upstream recompiler fixes are backported, and local changes are marked `bbport:`.
- Online play: shadPS4 0.20's network/NP/shadNet code (adapted by Awaken). The Hunter's Dream backend comes from droogie's bbhost.
- License: GPL-2.0-or-later code, distributed as GPL-3.0-or-later overall.

### How it runs the game

Quotes from the bbport README:

> The eboot is converted offline into a flat memory image; PS4 libc and libSceFios2 are linked into it as native code.
> a C loader maps it and jumps into the game.
> No CPU emulation and no per-instruction translation.
> a small runtime written for this one game replaces the PS4 system libraries.

Its scope, from the comparison table: "One game: Bloodborne v1.09".

- **CPU:** the game's x86-64 code runs natively, like AnyPS5. Only CPUs without AVX/BMI1/MOVBE/LZCNT (Intel Sandy and Ivy Bridge) get instruction translation, "best effort".
- **System libraries:** `src/runtime_*.c` (kernel, memory, thread, mutex, file, savedata, audio, ajm, pad, rtc, time, services…) implements **exactly what Bloodborne calls**, nothing more.
- **GPU:** the shadPS4 renderer handles the PS4 command stream and translates GCN shaders to SPIR-V, then Vulkan 1.3.

### How it does DLSS, FSR and frame generation

- DLSS comes from a bridge in `gpu/dlss_bridge` (adapted from IFreemz/shadPS4-Bloodborne-DLSS-FSR, NVIDIA DLSS SDK, RTX 20+). FSR 3.1 and FSR 4/4.1 come through DX12 interop (`gpu/fsr411_dx12`), and AMD ML frame generation 4.0.1 runs on RX 9000 only (`gpu/fg401_dx12`).
- **Motion vectors are rebuilt with Bloodborne-specific knowledge** (`docs/motion_vectors.md`):
  - **Camera vectors** are reconstructed from depth and the camera transforms (unjittered coordinates, with the viewport shift taken into account).
  - **Object vectors** come from a history of each G-buffer draw, keyed by its vertex streams, shader, address and a hash of its indices.
  - Draws are classified by **the size of Bloodborne's second vertex-shader buffer**: 864 bytes is the scene, 640 bytes or more a character skeleton, 96–639 bytes a weapon or prop.
  - Limits: indirect draws, tessellation, MSAA, transparency and particles stay on camera vectors.
- This answers the earlier report's question: **DLSS/FSR 2+ on a PS4 or PS5 game is possible, but only with per-game work.** A launcher can't add it in general.

### Requirements and features

From the README:

- Windows 10 1903+ or 11, a CPU with AVX, Vulkan 1.3, about 6 GB of RAM plus page file (10 GB for 1440p/4K).
- **Bloodborne v1.09 is required for the community patches:** 60/90/unlocked FPS, resolution and effects. Other versions run at 30 FPS.
- Your own decrypted CUSA03173 dump. The DLC goes in a DLC folder or `user\addcont\<title id>\`.
- Output resolutions from 720p to 4K, DLSS/FSR presets, FOV, mods and patches loaded without changing the game files, and remapping.
- The Python launcher (`launcher/bbport_*.py`) checks the eboot and game folder, sets the options, benchmarks a fixed scene, and names the cause of a crash from the log. That is the same idea as PS5Kitsune's crash diagnosis.

### bbport and AnyPS5 compared

| | bbport / bloodborne_pc | AnyPS5 |
|---|---|---|
| Scope | One game (Bloodborne v1.09) | Any PS5 game, PS4 in progress |
| Game code | Converted offline into a flat image, run natively | Relinked into a native Windows/Linux executable, run natively |
| System libraries | Hand-written for exactly what Bloodborne calls | Library reimplementations shared by all games, about 390 modules |
| GPU | shadPS4 renderer (GCN to SPIR-V) | AGC driver. PS4 GCN shaders are re-encoded to RDNA, then go through the PS5 recompiler |
| Upscaling | DLSS, FSR 3.1/4, ML frame generation, with motion vectors built for Bloodborne | None |
| Bloodborne today | Plays, with patches up to 60/90/unlocked FPS | Converts, doesn't start (no `libSceGnmDriver`) |

## 3. What this means for PS5Kitsune

### Today

- **Bloodborne:** point users to bloodborne_pc. The dump (CUSA03173 v1.09, decrypted, with Old Hunters) matches its requirements. PS5Kitsune can't do better before AnyPS5 has a PS4 graphics driver, and even then a general relinker won't have Bloodborne-specific DLSS or FPS patches.
- **The current error message is misleading.** "sce_sys/param.json is missing" makes it sound like the dump is broken, but a PS4 game's metadata is in `param.sfo`.

### Proposed changes

1. **Read `param.sfo`** (small, useful now):
   - In `GameValidator.cs` (currently `Blocking("sce_sys/param.json is missing.")` at line 60), fall back to `sce_sys/param.sfo` when `param.json` is missing, as AnyPS5 does.
   - `GameMetadata` gets `TitleId` (`TITLE_ID`), name (`TITLE`), version (`APP_VER`) and content ID from it.
   - Format: header `\0PSF`, version, key-table offset, data-table offset, entry count, then 16-byte entries (`key_off u16, fmt u16, len u32, max_len u32, data_off u32`). `fmt` 0x0204 is UTF-8 text and 0x0404 a u32.
   - Note: Bloodborne's `TITLE` ends with a non-ASCII character (®). Decode it as UTF-8 and trim the trailing NUL padding.
   - Icon: PS4 dumps ship `sce_sys/icon0.png` too. It's present here.
2. **Detect PS4 titles and say clearly what's missing** (small):
   - PS4 if the title ID starts with `CUSA`, or `param.sfo` is present without `param.json`.
   - Check the installed AnyPS5 for a PS4 graphics driver: does `libs` (release) or `core/libs/prx` (build) have `libSceGnmDriver`?
   - If it's absent, show a blocking issue: "PS4 game: AnyPS5 vX can't draw PS4 graphics yet (no libSceGnmDriver). PS4 support is in progress upstream (#6977)." Don't show "param.json is missing".
   - If it's present, continue with a warning: "PS4 support in AnyPS5 is experimental."
3. **Ready for when #9432 ships** (medium, later):
   - Add a profile flag such as `ps4: true` for AnyPS5 versions known to have `libSceGnmDriver`. Validate one PS4 game end to end (Limbo is the reference upstream).
   - PS4 games use PlayGo `playgo-chunk.dat` (no `playgo-chunkdefs.xml`) and keep DLC/add-ons in folders such as `SPEXPANSIONDLC03`. Check how AnyPS5 expects add-ons (`addcont`) before copying them.
   - The import audit will be the most useful screen there, because many PS4 imports will be missing for a while (Bloodborne: 180 absent in mid-October).
4. **Possibly: a note in the game summary for known ports.** For Bloodborne (`CUSA00900`, `CUSA03173`, …), say that a dedicated PC port exists, with its link. This is cheap and saves users time. It's up to you whether PS5Kitsune should point to third-party projects.

### Watch upstream

- [#9432](https://github.com/boykopovar/AnyPS5/pull/9432) `libSceGnmDriver`: the blocker. Once it is merged and released, test Limbo, then Bloodborne.
- [#3093](https://github.com/boykopovar/AnyPS5/issues/3093) and [#6977](https://github.com/boykopovar/AnyPS5/pull/6977): PS4 progress and the list of titles tested.
- AnyPS5 release notes after v0.1.4, for "PS4" or "GnmDriver".
