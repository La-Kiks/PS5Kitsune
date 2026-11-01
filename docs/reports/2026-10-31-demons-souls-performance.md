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

### Longer sessions (2026-11-01)

Three more runs, each played past the main menu: two with `APS5_HOST_IMPORT_MIB=6144`, one with `7168`.

**`r_Wants4K` in the game's render configs does not help.** `app0/misc/renderconfig_ps5_high.txt` (Cinematic, `WantsSuperHigh`, 30 FPS) and `renderconfig_ps5_perf.txt` (Performance, `WantsHigh`, 60 FPS) set the engine's `EWantsDisplayMode`; the PS4 configs use the lower `WantsPerf` (PS4) and `WantsBase` (PS4 Pro). With both PS5 files set to `WantsPerf` (`20261101-100249.log`), the game still switched to `guest 3840x2160` and the GPU frame time did not change (43–65 ms). The files are back to the originals.

**A crash in AnyPS5, not caused by the config change.** That run stopped with `driver failure: compute shader 0x2489fbb00: runtime image descriptor is invalid: image 0 first used at pc 0x2dc` (exit code `0xC0000409`). It was first blamed on the edited files, but `20261101-105548.log`, with the original files, stops with the same shader and the same `pc 0x2dc`. Both crashes come after hundreds of thousands of refused imports, and the descriptor the shader reads holds plain data (`3f000000 3d4ccccd …`, floats such as 0.5 and 0.05, in the first crash), so the shader seems to read memory that was not updated in time. It is an AnyPS5 bug to report upstream with `ps5kitsune diagnose PPSA01342 --report`; PS5Kitsune's diagnosis already says it has no explanation for it and offers Report.

**The 6144 MiB budget is not enough past the menus.** In `20261101-102743.log`:

- The budget is full from the main menu on (6143 of 6144 MiB live); 520,213 imports (about 5 TB) are refused by the end, 503,097 of them by draw building.
- Most refusals are the same 120 MiB block (`last 0x385800000+0x7800000 by build`), so the CPU copies that block again and again.
- The GPU frame time climbs from 43 ms in the menu to 99–139 ms at character creation, about 4–7 FPS, which matches what was seen. Ten `WAIT_REG_MEM … timed out after 1000ms` lines show the queues stalling on each other.

**`7168` is more than the driver can give** (`20261101-105548.log`). AnyPS5 accepted the setting, then `vkAllocateMemory` failed at 7072 MiB of imports (`host import of … failed at vkAllocateMemory (-2)`), with the heap at 7731 MiB used for a 7491 MiB budget, and AnyPS5 lowered its budget to 7072 MiB by itself. Refusals came back (237,916 before the crash), and the game ran at 5–12 FPS, barely better than 6144. Use 6144: it leaves room for the driver.

**Conclusion for this PC:** no setting makes Demon's Souls playable on 16 GB. The game reserves 13.5 GB, the driver lets the GPU read at most about 7 GB of it in place, and everything else is copied by the CPU. 32 GB of RAM would roughly double the driver's budget for that heap (it is about half the RAM). The crash above is separate and needs an AnyPS5 fix.
