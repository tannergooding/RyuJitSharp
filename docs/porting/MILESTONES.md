# Porting milestones

This is a reader-facing summary of current capabilities and remaining limits,
not a commit journal. Git history contains implementation and retirement details;
active evidence boundaries belong in [state.json](state.json), accepted differences in
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

Selected Windows x64 `System.Private.CoreLib` codegen matches; full primary-JIT
phase-dump parity and the dynamic TLS input-ownership boundary remain open.

The [continuation plan](PLAN.md) records the post-port boundary. There is no
active source-port backlog; the deviation register records accepted observable
differences and the upstream report register records confirmed native defects.
