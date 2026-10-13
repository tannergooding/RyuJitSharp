# Porting milestones

This is a reader-facing summary of major capability transitions, not a commit
journal. Git history contains implementation and retirement details; active
evidence boundaries belong in [state.json](state.json), accepted differences in
[DEVIATIONS.md](DEVIATIONS.md), and confirmed native defects in
[UPSTREAM-REPORTS.md](UPSTREAM-REPORTS.md).

## Current capability

Selected MinOpts and optimized Windows x64 corpora execute code emitted by the
managed JIT. Optimized coverage includes inlining, value numbering and CSE, loop
and flow optimizations, profile-guided paths, and register-allocation policies;
selected tiered/PGO and GC-stress scenarios also execute.

These results apply only to the tested corpora and configurations. Broader
runtime and JIT/EE ABI/GC coverage, full primary-JIT phase-dump parity,
relocation-aware code-generation and GC/EH/unwind metadata parity, and Linux
and other-target implementation and runtime validation remain open.
Target-specific translations and fixtures do not by themselves establish
execution or parity on those targets.

Windows x64 `System.Private.CoreLib` codegen was identical for 4,284 types and
75,676 methods; 60 types and 1,267 methods were skipped. Full primary-JIT
phase-dump parity and the dynamic TLS input-ownership boundary remain open.

## Major milestones

| Period | Capability |
| --- | --- |
| 2026-10 | Extended backend source coverage across xarch, ARM/ARM64, LoongArch, RISC-V and WebAssembly, including legacy JIT32 GC serialization. Targeted fixtures exercise instruction recording, disassembly/scoring and metadata contracts; other-target execution and runtime parity remain unverified. |
| 2026-09 | Expanded managed Windows-x64 execution from the MinOpts baseline into selected optimized, tiered/PGO, and GC-stress scenarios. The supported scope remains corpus- and configuration-specific. |
| 2026-06 | Advanced the importer through block-code import, calls, and intrinsics; hardware-intrinsic import was still an outstanding boundary at that point. |
| 2026-05 | Established basic-block construction, local-variable table initialization, and first-block canonicalization. |
| 2026-04 | Retargeted the project to .NET 10 and resumed active porting. |
| 2024-02 | Established the project, core interfaces, and native-facing machinery to load as a no-op AltJIT. |

The [continuation plan](PLAN.md) records the post-port boundary. There is no
active source-port backlog; the deviation register records accepted observable
differences and the upstream report register records confirmed native defects.
