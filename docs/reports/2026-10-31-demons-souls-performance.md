# Demon's Souls (PPSA01342): black screen, then under 1 FPS

Date: 2026-10-31. Setup: PS5Kitsune 0.1.0 (Windows `.exe`), AnyPS5 v0.1.4 release, Ryzen 5 5600X, 16 GB RAM, RTX 3060 12 GB, Windows display scaling 125% (window 1536×864).

## Summary

- The "black screen at start" was not a crash. On the first launch the game spends about 30 s initializing and builds about 945 shaders (about 870 MB) before its first frame. It was stopped about a minute in, before anything showed.
- Once it got past loading, the game ran at under 1 FPS, with CPU and RAM at 100% and the GPU at 10–20%. **The GPU was in use** (RTX 3060 selected, no CPU rendering). The bottleneck is memory traffic between RAM and the GPU, made worse by 16 GB of RAM and a 4K internal resolution.
- Two AnyPS5 environment variables looked useful: `APS5_OUTPUT_RESOLUTION` and `APS5_HOST_IMPORT_MIB`. PS5Kitsune 0.3.0 exposes both (see [Feature proposals](#feature-proposals)). Tested on 2026-11-01 (see [Result](#result-2026-11-01)): `APS5_HOST_IMPORT_MIB=6144` removes every refused import and cuts the GPU frame time by about a third; `APS5_OUTPUT_RESOLUTION` changes nothing on this 1080p screen, because the game picks 4K by itself.
- The `.esbak` backup files are no longer an issue: the latest AnyPS5 release handles them itself, so the TODO.md item can be dropped.

## Log analysis

Logs: `games\PPSA01342\.ps5kitsune\logs\20261031-163140.log` (first launch), `…-163432.log` (stopped during init), `…-163825.log` (ran in game).

### First launch (16:31:40): loading, not a hang

```
[MSG-Init] Finished Initialization: 29.9618s
[MSG-Init] Starting Script: $/scripts/cp11/_cmn/cp11main.cgpr : 29.9619s
[MSG-Init] ResourcePool::GatherResourceFileInfo() took 8.8841s
[shader-disk-cache] ... (totals: 0 hits, 945 misses, 945 writes ...)
[ps5kitsune] stopped by the user (exit code 0)
```

No error. The files that fail to open (`findWriteRepository.txt`, `/devlog/app/commandlineargs.txt`, the `devlog` log file) are optional files the game probes for, and that is normal.

### The three audit stubs are not the cause

The audit marks the game "orange" because of 3 stubs: `sceAgcUpdateInterpolantMapping_0100`, `sceHttp2WaitAsync` and `sceHttp2ReadDataAsync`. In AnyPS5 v0.1.4, each one calls `NotImplemented_nid_no_patch(__func__)`, which is `[[noreturn]]`. A call would stop the game with a "not implemented" message, not leave it on a black screen. None of the logs has that message.

- `sceHttp2*`: online features (PSN), probably never reached offline.
- `sceAgcUpdateInterpolantMapping_0100`: the one to watch. AnyPS5 implements `_10000` but not the older `_0100` this game was built against. If it is ever called, it is an AnyPS5 issue to report upstream.

### The run that reached the game (16:38:25): GPU starved by memory copies

The shader cache works on later launches (`945 hits, 0 misses`). The main loop starts at 59 s.

**1. The host import budget is full and imports are refused all the time.** AnyPS5 lets the GPU read guest memory in place ("host imports"), up to a budget that defaults to half of the driver's system-memory heap (8155 MiB here, so 4077 MiB). The budget filled up as soon as gameplay started:

```
[gpu] host import budget: 4077 MiB live of 4077 MiB (the default; host heap 1 reports a budget of 7491 MiB and a usage of 4810 MiB); 59236 imports (5051361 MiB) refused so far
[gpu] host imports at shutdown: 705 live (4077.5 MiB); made 782 (4848.5 MiB), refused 61123 (...), failed 1; by requester (made/refused/failed): map 699/164/0, build 78/28945/1, access 5/32014/0
```

Every refused import falls back to a copy done by the CPU. That is the 100% CPU with an idle GPU.

**2. The game renders at 4K.**

```
[mouse] video driver windows: guest 3840x2160 presented at 1536x864, delta scale 2.5000 x 2.5000
```

The game renders at 3840×2160, then AnyPS5 scales that down to a 1536×864 window. That is 4× the pixels, and 4× the render-target memory to move, for no visible gain.

> **Correction (2026-11-01).** This report first said AnyPS5 reports a 4K display to the game. It does not. The screen is 1080p (1536×864 at 125% scaling), and AnyPS5 v0.1.4 reports the screen's mode, so the game was told 1080p. The `guest` size in this line is the size of the buffers the game flips (`MouseInput::SetPresentation`), not the mode AnyPS5 reports (`sceVideoOutGetResolutionStatus`, `libSceVideoOut/src/Output.cpp`). Every run logs `guest 1920x1080` when the main loop starts, then `guest 3840x2160` a few seconds later: Demon's Souls chooses 4K buffers itself.

**3. GPU frame time is 110–130 ms** (`[staging] adaptive: ... GPU 120.8 ms/frame staged`), so about 8 FPS at best before the CPU copies are counted.

**4. RAM is the underlying limit.** The game reserves 13.5 GB of PS5 memory (`Total Direct Memory: 13824.00 MiB`) on a 16 GB PC that also runs Windows, the driver and AnyPS5. RAM at 100% means Windows swaps to disk.

VRAM was not the limit: the texture cache used about 500 MiB of a 3.8 GB budget, and the GPU's heap used 4.4 of 11.3 GB. So `APS5_TEXTURE_CACHE_MIB` would not help.

### Why the GPU cannot simply "take over" the RAM

On a PS5, the CPU and GPU share one memory pool. The game's CPU code now runs natively on the PC and writes its data to host RAM, so the 13.5 GB it reserves has to live in RAM. AnyPS5 cannot move it into VRAM. The GPU can only work on it through imports, which are limited by the budget, or through copies, which cost CPU time. The way to make the GPU busier is to make the game move less memory, which is what a lower resolution does.

## Workaround that needs no PS5Kitsune change

Both variables are read by AnyPS5 v0.1.4 (`libSceVideoOut/src/VideoOutDriver.cpp`, `libSceAgcDriver/Graphics/src/GuestBufferMemory.cpp`). Setting them for the Windows user makes the game inherit them. Restart PS5Kitsune afterwards.

```powershell
setx APS5_OUTPUT_RESOLUTION 1920x1080   # tells the game the screen is 1080p (no effect here, see Result)
setx APS5_HOST_IMPORT_MIB 6144          # optional, try second: larger GPU import budget (default here: 4077)
```

Undo:

```powershell
reg delete HKCU\Environment /v APS5_OUTPUT_RESOLUTION /f
reg delete HKCU\Environment /v APS5_HOST_IMPORT_MIB /f
```

`APS5_HOST_IMPORT_MIB` is a gamble on 16 GB of RAM: it pins more RAM, which can cut the copies or make the swapping worse. Compare the FPS with and without it. The real fix for this game is 32 GB of RAM.

Result of the workaround: see below. In PS5Kitsune 0.3.0, both are per-game settings (Resolution, and Advanced, then Extra AnyPS5 variables), so `setx` is no longer needed.

### Result (2026-11-01)

PS5Kitsune 0.3.0 (main at `2d9c5d4`), AnyPS5 v0.1.4 release, same PC. The two 2026-11-01 runs went from launch to the PlayStation Studios logo video, a few minutes each; the 2026-10-31 run, with no setting, played longer.

| | No setting (`20261031-163825.log`) | Resolution 1080p (`20261101-085601.log`) | 1080p + `APS5_HOST_IMPORT_MIB=6144` (`20261101-092242.log`) |
|---|---|---|---|
| `guest` line | 1920x1080, then 3840x2160 | 1920x1080, then 3840x2160 | 1920x1080, then 3840x2160 |
| Import budget | 4077 MiB, the default | 4077 MiB, the default | 6144 MiB, `APS5_HOST_IMPORT_MIB sets` |
| Imports refused | 61,123 | 30,478 (441,624 MiB the CPU copied instead) | **0** |
| GPU frame time (`[staging] adaptive`) | 82–121 ms | 60–87 ms | 38–65 ms |

The first two columns differ mostly by session length (the frame time rises the longer the game runs), not by the resolution setting: the `guest` lines are the same.

- **`APS5_OUTPUT_RESOLUTION=1920x1080` does nothing on a 1080p screen**, as its help text says: AnyPS5 already reports 1080p, and the game still switches to 4K buffers. Whether it changes anything on a 4K screen is not tested.
- **`APS5_HOST_IMPORT_MIB=6144` works.** No import is refused for the whole run, the CPU copies are gone, and the GPU frame time drops from 60–87 ms to 38–65 ms, about 15–26 FPS instead of 11–16. It felt faster in game.
- **The new budget is nearly full too:** 6052 of 6144 MiB live at shutdown. A longer session in the game world may refuse imports again. The driver's budget for that heap is 7491 MiB, so 7168 is the ceiling to try; each MiB is pinned RAM, which a 16 GB PC with a 13.5 GB game has little of.
- **What is left is GPU work at 4K,** which no setting PS5Kitsune has can lower.

## Feature proposals

### 1. Per-game resolution setting (recommended, small)

A "Game resolution" choice in the per-game settings, also offered before launch.

- **AnyPS5 side, as of v0.1.4:** `APS5_OUTPUT_RESOLUTION=<width>x<height>` is parsed, then mapped by `resolutionIndex()` to one of **two** PS5 video modes: 2160p if width ≥ 3840 or height ≥ 2160, otherwise 1080p. An invalid value throws `VideoOut: APS5_OUTPUT_RESOLUTION must be <width>x<height> with positive integers, got "…"`. So the useful choices today are only **1080p** and **4K**, written as `1920x1080` and `3840x2160`. Any other value just picks one of the two. Don't offer 1440p until AnyPS5 supports it.
- **Default:** unset (AnyPS5 reports the screen's mode, 1080p here). Making 1080p the PS5Kitsune default would change nothing on this PC: the game chooses 4K buffers itself (see the correction above).
- **PS5Kitsune changes**, following the existing `presentMode` pattern:
  - `GameRecord.cs` / `GameSettings`: add `resolution` (`null` | `"1080p"` | `"2160p"`), so it lands in `game.json` `settings`.
  - `GameSettingsValidator.cs`: allowed values, normalize unknown ones to `null` and report them in `replaced`.
  - `LaunchEnvironment.cs`: add the `APS5_OUTPUT_RESOLUTION` constant and emit it only when the value is not `null`. Note that this variable uses the `APS5_` prefix, not `ANYPS5_`.
  - `SettingVariables.cs`: add an entry so the crash diagnosis can offer "Reset resolution" if AnyPS5 rejects the value.
  - `GameSettingsReport.cs` and `ps5kitsune settings <titleId>`: show it.
  - `GameSettingsView.axaml` / `GameSettingsViewModel.cs`: a combo box.
  - "Choose before starting": an optional pre-launch prompt or a dropdown next to the Launch button on the Game page, saved to the game's settings so the next launch remembers it.
  - Profile: record that the variable exists from AnyPS5 v0.1.4 onward, and hide the setting for older installs.

### 2. Advanced: extra environment variables per game (small)

A free-form `KEY=VALUE` list in the per-game settings (`settings.extraEnvironment`), added last by `LaunchEnvironment.For`. This covers `APS5_HOST_IMPORT_MIB` and any future AnyPS5 tuning variable without a new release of PS5Kitsune. Only allow names that start with `APS5_` / `ANYPS5_`, and log the variables applied at launch so bug reports show them.

### 3. RAM warning in `check` and on the Game page (small)

The game logs how much memory it reserves (`[MSG-Init] Total Direct Memory: …` for this engine; others may differ). `check` could warn below 32 GB, and the Game page could show "this game reserved 13.5 GB on a 16 GB PC; expect heavy slowdowns" after a run. The `[gpu] host import budget: … refused` line with a large refused count is a good signal for the crash diagnosis to explain as a performance hint.

### 4. DLSS / FSR upscaling and frame generation (AnyPS5's job; PS5Kitsune only exposes it)

AnyPS5 v0.1.4 has no upscaling code at all. `libSceAgcDriver/Execution/src/AspectFit.cpp` only computes the scale from the game image to the window. What follows is an assessment of what could come, not an announced plan. There are three levels, from most to least likely.

#### Level 1: spatial upscaling, for every game (likely)

- **FSR 1, NVIDIA NIS, or a sharpened bilinear filter** only needs the final image, which AnyPS5 already has when it scales the game to the window. It is general code that touches no specific game, so it fits AnyPS5's rules (see Level 3). Many console emulators offer FSR 1 for exactly this reason.
- Combined with feature 1, the game renders at 1080p and FSR 1 upscales to a 1440p or 4K window. That is the realistic "upscaling for all games".
- That would be a feature request to AnyPS5. Once AnyPS5 has a variable for it, PS5Kitsune adds a per-game setting like the others.

#### Level 2: frame generation, for every game (already exists outside AnyPS5)

- These work on any window by interpolating the final images, with no change to AnyPS5:
  - Lossless Scaling (a paid Steam app) does upscaling (LS1, FSR) and frame generation on any GPU, including an RTX 3060.
  - AMD Fluid Motion Frames works on AMD GPUs only, and NVIDIA Smooth Motion on RTX 40/50 only.
- The quality is lower than DLSS Frame Generation, which needs game data and an RTX 40/50, so an RTX 3060 can't use it anyway. But it is universal. AnyPS5 has no reason to rebuild it, and PS5Kitsune can mention it in its docs.
- **Frame generation needs a decent base frame rate** (about 30 FPS or more for a good result, about 60 recommended). At 1–8 FPS it produces mostly artifacts and adds latency. It is only worth looking at once the memory bottleneck above is solved.

#### Level 3: temporal upscaling (DLSS SR, FSR 2+, XeSS) per engine (possible, hard, not soon)

- **What these upscalers need:** a low-resolution image taken before the UI, depth, motion vectors and the camera jitter. An emulator only sees GPU commands. Nothing tells it which target is the scene, which is the UI, or where the camera is.
- **The per-game approach**, as in the Bloodborne PC port (see `2026-10-31-ps4-games-and-bloodborne.md`): it rebuilds camera vectors from depth and the camera transforms, and object vectors from a history of G-buffer draws classified by Bloodborne's vertex-buffer sizes. That works, but for one game only.
- **The per-engine approach, which could cover far more games:** almost every PS5 game already does its own temporal anti-aliasing or upscaling (Unreal TAA/TSR, checkerboard rendering, FSR 2 compiled into the game, Insomniac's IGTI). So it **already computes motion vectors, depth and jitter**. An emulator wouldn't have to rebuild them. It would only have to find the game's own TAA pass and replace it with DLSS/FSR, feeding it the game's own data. That's how OptiScaler works on PC: it replaces FSR 2 or XeSS with DLSS in games that support only one of them. One profile for Unreal Engine 4/5 could cover dozens of PS5 games.
- **Why it stays hard:**
  - Finding the pass means recognizing its shaders, which change with each engine version.
  - Each engine encodes its vectors differently (scale, sign, resolution, dilation).
  - The jitter has to be read from the game's data.
  - Some games draw the UI before their TAA.
  - The game's own sharpening has to be turned off.
- **Why it is unlikely in AnyPS5's core:** AnyPS5's CI rejects game-specific code. `scripts/tools/check_conventions.py`, rule `title-specific`, fails core code that contains a `PPSA`/`CUSA` title ID: *"Implement the general behaviour, not what one title needs; title-specific code belongs in its sce_module"*. Per-engine profiles are a borderline case. They would more likely live in an optional layer or a fork, as bloodborne_pc did with shadPS4. And the project's priority today is getting games to run (it doesn't even have the PS4 graphics driver yet), not image quality.

#### What PS5Kitsune does at each level

| Level | PS5Kitsune's role |
|---|---|
| 1. FSR 1 / NIS in AnyPS5 | A per-game "Upscaling" setting, once AnyPS5 exposes a variable for it |
| 2. Frame generation outside AnyPS5 | A short docs section: Lossless Scaling and the driver-level options, and the minimum base frame rate |
| 3. Per-engine DLSS/FSR | If it ever appears, show the game's engine in the summary and whether it is supported. That information would come from AnyPS5's audit and logs, not from PS5Kitsune |

### Suggested order

1. Resolution setting (feature 1): biggest effect, smallest change.
2. Extra environment variables (feature 2).
3. RAM and import-budget hints (feature 3).
4. A docs section on frame generation with Lossless Scaling and the driver-level options (Level 2).
5. Open an AnyPS5 issue for spatial upscaling (FSR 1 / NIS) when scaling to the window (Level 1), and one for `sceAgcUpdateInterpolantMapping_0100` if it ever stops the game.
