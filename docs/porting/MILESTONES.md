# Porting milestones

This is a reader-facing summary of major capability transitions, not a commit
journal. Git history contains implementation and retirement details; active
decisions and unresolved work belong in [state.json](state.json),
[BACKLOG.md](BACKLOG.md), and [DEVIATIONS.md](DEVIATIONS.md).

## Current capability

Windows x64 is the first execution and parity baseline. Selected MinOpts and
optimized corpora execute code emitted by the managed JIT. Optimized coverage
includes inlining, value numbering and CSE, loop and flow optimizations,
profile-guided paths, and register-allocation policies; selected tiered/PGO and
GC-stress scenarios also execute.

These results apply only to the tested corpora and configurations. Broader
runtime and JIT/EE ABI/GC coverage, full dump and generated-code parity, and
Linux and other-target implementation and runtime validation remain open.
Target-specific translations and fixtures do not by themselves establish
execution or parity on those targets.

## Major milestones

| Period | Capability |
| --- | --- |
| 2026-10 | Expanded whole-function backend translation across xarch, ARM/ARM64, RISC-V, and WebAssembly paths. Ported legacy JIT32 GC header and pointer-table serialization; focused win-x86 compiler-semantic tests pass, but x86 execution and generated-code parity remain unverified. Windows x64 remains the first execution target; target-specific code-generation coverage is not a parity claim. |
| 2026-09 | Expanded managed Windows-x64 execution from the MinOpts baseline into selected optimized, tiered/PGO, and GC-stress scenarios. The supported scope remains corpus- and configuration-specific. |
| 2026-06 | Advanced the importer through block-code import, calls, and intrinsics; hardware-intrinsic import was still an outstanding boundary at that point. |
| 2026-05 | Established basic-block construction, local-variable table initialization, and first-block canonicalization. |
| 2026-04 | Retargeted the project to .NET 10 and resumed active porting. |
| 2024-02 | Established the project, core interfaces, and native-facing machinery to load as a no-op AltJIT. |

The [continuation plan](PLAN.md) describes upcoming work. The backlog tracks
unresolved findings and the deviation register records accepted observable
differences.
