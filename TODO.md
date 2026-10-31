# TODO

Developer work to do later. Problems users can hit are in [KNOWN_ISSUES.md](KNOWN_ISSUES.md), each with its planned fix.

## Build mode on Linux

Milestone 8a supports release mode only on Linux. Build mode needs the system GCC and the build dependency packages listed in AnyPS5's BUILD.md, an install command per distribution, and a child-process job: Linux has no job object, so git and CMake started by a PS5Kitsune that is killed outright keep running (`IChildProcessJob` is a no-op there).

## Required OS in profiles and system facts

`ProfileLoader.LoadEmbedded` and `LoadActive` default `os` to Windows, and `SystemFacts` defaults to the Windows texts. A caller that forgets them gets Windows behaviour on Linux silently; make them required.
