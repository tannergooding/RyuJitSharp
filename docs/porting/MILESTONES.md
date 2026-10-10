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

The earlier 762-method `PortingCorpus` result was only a workload subset. At
pinned oracle `6ff62b1d7ff0`, a full Windows x64 primary-JIT PMI capture covered
4,284 types and 75,676 method slots on each side (60 skipped types and 1,267
skipped methods), producing 75,824 method blocks and 75,644 unique headers.
After matching blocks by method, all per-method instruction and `IG`-label
sequences match after canonicalizing only generated `G_M` identifiers. Both
sides emitted 8,430,400 code bytes with no code-size deltas, and all 1,689 NaN
annotation lines matched. The 36 managed-only condition-expression prefixes
were traced to redundant-branch `vnDump` calls that lacked native's `verbose`
guard; the guard now removes them from the final capture. Six process-variable
`RWD` data rows remain in three async continuation methods; a native repeat
control confirmed those absolute pointer values vary between processes. The
final one-sided recapture reorders seven header positions while preserving the
same header multiset and per-method text after canonicalizing generated `G_M`
identifiers and isolating the six `RWD` rows. A paired recapture of the final
managed JIT contains interleaved stdout and is excluded from parity evidence.
Raw stdout is therefore not byte-for-byte equal; this establishes per-method
instruction-listing parity, not relocation-aware machine-code or GC/EH/unwind
metadata parity. Captures and comparison reports are under
`artifacts\corelib-pmi-verbose-gate-jit-diff-20261010` and
`artifacts\corelib-pmi-verbose-gate-paired-jit-diff-20261010`.

Focused dump captures match through Importation and Redundant Branch Opts for
nine `System.Resources.ResourceManager` methods, and through Importation for
`System.SpanHelpers.IndexOf`; only process-local CorInfo handle fields are
canonicalized in those comparisons. A focused `EventSource` capture now
matches the static-readonly fold and local table. The managed importer now
prints the `volatile.` prefix; a three-method primary-JIT follow-up confirms
that `EventSource.Initialize`'s 1,491-line Importation body matches native
after canonicalizing only process-local CorInfo handles, including both
volatile stores. The full comparison prefix still differs before Importation:
native skips a deferred profile check while managed runs the profile-weight
check. That diagnostic is outside the phase body. The ResourceManager phase
capture is a selected-method AltJIT run; full CoreLib primary-JIT phase-dump
parity remains open. The dynamic TLS input-ownership boundary also remains
unresolved.

## Major milestones

| Period | Capability |
| --- | --- |
| 2026-10 | Expanded whole-function backend translation across xarch, ARM/ARM64, LoongArch, RISC-V, and WebAssembly paths, including selected LoongArch frame/call/epilog/profiling support, ARM32 disassembly formatting, ARM64 SVE display/scoring, and all five ARM64 debug emitter-test sections. ARM64-targeted managed tests pass; B528 alignment scoring and ARM64 execution/generated-code parity remain open. Also ported legacy JIT32 GC header and pointer-table serialization; focused win-x86 compiler-semantic tests pass, but x86 execution and generated-code parity remain unverified. Reconciled all changed JIT/include paths to a captured live oracle and added a reusable immutable-diff refresh skill; this advances source freshness, not full-runtime parity. Target-specific code-generation coverage is not an execution or parity claim. |
| 2026-09 | Expanded managed Windows-x64 execution from the MinOpts baseline into selected optimized, tiered/PGO, and GC-stress scenarios. The supported scope remains corpus- and configuration-specific. |
| 2026-06 | Advanced the importer through block-code import, calls, and intrinsics; hardware-intrinsic import was still an outstanding boundary at that point. |
| 2026-05 | Established basic-block construction, local-variable table initialization, and first-block canonicalization. |
| 2026-04 | Retargeted the project to .NET 10 and resumed active porting. |
| 2024-02 | Established the project, core interfaces, and native-facing machinery to load as a no-op AltJIT. |

The [continuation plan](PLAN.md) records the post-port boundary. There is no
active source-port backlog; the deviation register records accepted observable
differences and the upstream report register records confirmed native defects.
