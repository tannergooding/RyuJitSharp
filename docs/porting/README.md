# Porting contract and workflow

RyuJitSharp ports RyuJIT to C# while preserving compiler behavior. The intended
observable results are identical generated code, `jitdisasm`, and `jitdump` for
the same inputs and configuration, except for explicitly approved differences.
Internal C# representation may differ without changing those results.

Start with [state.json](state.json) for revisions and the current checkpoint.
The [continuation plan](PLAN.md) defines milestones; the
[deviation register](DEVIATIONS.md) distinguishes accepted changes from existing
limitations. Neither is an assertion that the current port is complete.
Record bugs and future improvements in the [backlog](BACKLOG.md). The first goal
is a clean, recognizable C# port that provides the foundation for a later rewrite,
not that rewrite performed incrementally during translation.

## Three working trees

| Tree | Responsibility |
| --- | --- |
| This repository | C# implementation, tests, source mappings, decisions, and parity evidence. |
| `runtime-port` | Residual native sources, with ported code removed to expose remaining work. Not a build oracle. |
| `runtime-oracle` | Intact upstream checkout at an exact revision, used for native builds and reference behavior. |

The native trees can be Git worktrees sharing an object database. Keep deletion
edits out of the oracle. Its source must be clean; ignored build artifacts are
expected. Native source revisions match at completed synchronization checkpoints.
During synchronization, explicitly retain the old source revision and the target
revision. A fetched upstream head is not automatically the port's new baseline.

Before moving either baseline, preserve native and C# WIP, including untracked
files and index state. Record immutable snapshot IDs and protect them with local
refs. These refs are recovery aids, not published dependencies. Keep local paths
and recovery commands in a session artifact. Never use `stash pop` as the only
backup or interpret `stash@{0}` as a stable identity.
The `localRecovery` entries in `state.json` identify these local refs and objects;
they are not expected to exist in a fresh clone.

## Unit of work

Complete upstream synchronization before resuming saved WIP and new phase
porting. Batch changes by source/dependency area; after synchronization, target
required minopts phases before optional optimizations. Reuse the prepared native
build and `Core_Root`, with small AltJIT programs at meaningful boundaries.
Intermediate checks should be localized and fast; deeper investigation is driven
by actual failures or mismatches, not a mandatory cycle for every helper.

Port a whole native function and its required support, rather than a fragment
selected to get one test running. Preserve all Windows-x64 behavior in that
function. Paths unique to other targets may be explicit NYIs, with their target,
symbol, and missing behavior recorded. Other targets must remain compilable;
compilation is not proof of their execution support.

Preserve phase order, traversal and insertion order, identity semantics, enum
values, integer widths, overflow/truncation, signedness, shifts, floating-point
NaNs and signed zero, and error propagation. Managed objects must not invalidate
native pointer lifetimes or JIT/EE layout and calling-convention contracts.
Review collection substitutions for ordering and equality changes.

Keep useful upstream comments and recognizable symbol names. Use existing C#
helpers and conventions rather than copying native machinery unnecessarily.
Do not change algorithmic choices merely because a different implementation
looks more idiomatic. Ask before substantial redesigns.
Fix translation defects needed for correctness/parity as part of the relevant
port. Record suspected upstream bugs separately: silently fixing them only in
C# would itself change behavior. Defer unrelated renames, refactoring, and
restructuring until after the clean-port milestone.

For a new non-Windows-x64 stub, ensure the unsupported path actually terminates
compilation through the established failure mechanism. `Globals.NYI` can return
under some configurations; calling it alone does not prove the path cannot
fall through. Do not globally change that policy as an incidental porting edit.

## Sparse tracking, not a second copy of the source

Use the native residual tree as the remaining-work view. Most source locations
follow `src/coreclr/jit/<stem>.{h,cpp}` to `sources/Core/jit/<stem>/`, with similar
`inc` and `jitshared` mappings. This is a navigation convention, not a completeness
test: native implementation files such as `importer.cpp`, `fginline.cpp`, and
`lclvars.cpp` contribute to C# `Compiler` partials.

The initial `sourceMap` in `state.json` contains coarse navigation hints for
active and cross-cutting areas, not a complete function inventory. Extend it
when discovering a non-obvious mapping. Split it by native area if it becomes
large; do not load or rewrite an entire inventory for each task.
An entry with `csharpSnapshot` describes preserved WIP, which may not exist in
the working tree yet; resolve its paths in that Git tree until integration.

Record function-level information only for active work, partial reconciliation,
target-specific NYIs, non-obvious deviations, or validation gaps. For such a
record include the native symbol/path, C# destination, source revision,
implementation status, and remaining action. A `reconciledCommit` of `null`
means no full reconciliation has been established. Advance a file's reconciled
revision only after all relevant changes have been handled; use function-level
exceptions for partial progress.

Keep implementation and evidence separate:

| Implementation status | Meaning |
| --- | --- |
| Remaining | Native implementation has not been ported. |
| Stub | A declaration/no-op/NYI exists, not an implementation. |
| In progress | Work is preserved but incomplete or not integrated. |
| Ported | The whole intended function is implemented; deferred platform paths are explicit. |

Evidence is scoped separately to build, unit behavior, phase dumps, codegen, or
execution, and to a particular revision, configuration, target, and corpus.
No percentage should be inferred from removed lines, file counts, or TODO counts.

## Keeping context bounded

Load this contract and the small current checkpoint, then look up the relevant
symbols and source-map entry. Read complete functions and necessary dependencies,
not entire `Compiler` partials. Keep one active, dependency-coherent batch.

Finish each completed, validated batch with a logical commit before starting
the next batch. Include the related implementation, focused coverage, and
checkpoint/deviation updates together; do not accumulate unrelated work into
a later catch-all commit. Keep incomplete work uncommitted. Recovery snapshots
remain useful for artifacts and saved WIP, but supplement rather than replace
regular commits. Local commits do not authorize pushing or opening a PR.

For upstream synchronization, obtain the changed-file/commit inventory first.
Classify each affected ported area, generator input, JIT/EE interface, and relevant
native dependency. Save classifications and unresolved symbols once, rather than
rediscovering them in each session. Record an explicit reason for an exclusion.
Git retains old native code even when it has been removed from `runtime-port`.
Generate a review inventory from the pinned revisions and preserved residual
snapshot with:

```powershell
.\scripts\porting\Get-UpstreamChanges.ps1 -RuntimeRepository <native-repository-path> -OutputPath <inventory.csv>
```

The native repository must contain the snapshot objects in `state.json`.
The inventory combines residual edit status, sparse mappings, generator inputs,
and baseline directory conventions. Generator inputs are read from the current
working tree, including newly added tables. These are navigation hints, not completion
or exclusion decisions; every row initially remains pending review. Its search
scope covers JIT, shared JIT support, interfaces, and JitInterface tooling.
Follow dependencies outside those directories when an affected change requires
them. Keep the generated inventory and review notes with the batch artifacts.

When delegation is appropriate and authorized, give a worker a coherent objective
and explicit stopping criteria. Use completion/blocker/decision handoffs, not
polling or duplicate investigation. Request results and evidence, not transcripts.

## Parity contract

Use a native JIT and host/SuperPMI tooling matched to the same upstream revision
and JIT/EE ABI as the C# port. Do not compare against whichever `clrjit` happens
to ship with the installed SDK. The SDK used to build the C# port is a separate
toolchain choice, recorded in the run manifest.

Control the target, CPU/ISA settings, build feature defines, JIT flags, tier,
PGO inputs, stress seeds, method selection, and corpus. Start with deterministic
single-process cases; add tiering, PGO, OSR, and concurrency as explicit dimensions.
Use upstream diffable-output settings on both sides where applicable
(`JitDisasmDiffable`, `JitDumpASCII`); retain raw output. Do not strip tree/block
IDs, instruction order, whitespace, costs, or diagnostics to manufacture equality.
Unavoidable address/timing/path differences require a narrowly documented rule
and approval before any post-processing allowance is added. Allocation statistics
are the already accepted exception in [DEVIATIONS.md](DEVIATIONS.md).

Until codegen exists, compare named phase boundaries and their IR/dumps.
Explicitly report the phase reached and compilation result. Phase equality is
not full-pipeline equality. Once codegen is available, compare disassembly and
machine code with relocation-aware tooling, plus GC/EH/unwind metadata, and
execute focused semantic tests. Text equality alone is insufficient.

Reuse runtime's SuperPMI tooling where feasible; see the pinned oracle's
`src/coreclr/tools/superpmi/readme.md` and `src/coreclr/scripts/superpmi.md`.
SuperPMI's native replay/result comparison may need a separate phase-dump path
while this port deliberately returns `CORJIT_SKIPPED`. Treat that as an explicit
harness capability, not a successful replay.

A parity report must record both revisions and binary hashes, toolchain/flavors,
host/target/ISA, relevant environment and commands, corpus identity, expected and
observed method/phase counts, failures/skips, applied exception IDs, and raw/diff
artifact locations. Fail empty selections and missing/truncated output. Separate
native fallback output from C# output. Every comparison rule needs tests proving
that meaningful differences are still detected, but these can be small targeted
fixtures/probes rather than a new test framework. Do not make extensive tooling
unit tests a prerequisite for porting. Dumps are the primary early validation,
followed by disassembly and regular runtime tests as the port becomes capable.

Centralize host-specific newline encoding at the shared output boundary,
including embedded LF characters, `WriteLine`, and fragmented writes. Preserve
message layout at the call sites: missing/extra newlines from translation need
comparison against their native statements, not guessed correction in a writer.
Use native behavior to define explicit CR/LF handling. Do not normalize away
differences in the comparison tooling.

### Small deterministic corpus

Build `sources\PortingCorpus\PortingCorpus.csproj` in Release. Its output is
`artifacts\bin\sources\PortingCorpus\Release\net10.0\PortingCorpus.dll`.
It checks seven entry points covering arithmetic, branches, locals,
direct and managed indirect calls, and an inline candidate. It is a standalone fixture, not a compiler
coverage claim.

Use `scripts\porting\Invoke-PortingCorpus.ps1` with `-CoreRoot`, `-Corpus`,
`-OutputDirectory`, and the exact `-NativeCommit`. Capture the native run first.
For the managed run, additionally supply `-ManagedJit` and `-ManagedSource`
identifying the C# revision and any preserved uncommitted source snapshot.
The host must be Checked/debug and ABI-compatible with that managed binary.
The runner isolates the child environment, rejects reused output directories,
requires exactly the expected case-sensitive compilation headers, and records
binary hashes, settings, timeout/exit status, and the raw dump. Native fallback
is expected for this incomplete port; process success is not generated-code
success.

`scripts\porting\Compare-PortingDumps.ps1 -NativeDump <file> -ManagedDump <file>
-OutputPath <report.json>` compares each compilation from its start header up
to, but excluding, `Finishing PHASE Importation`. It preserves CR/LF and reports
the first differing line. Changed compilation order/identity, missing methods,
or missing phase boundaries are errors;
ordinary differences are diagnostic report entries, not a failing process exit.
This deliberately narrow prefix comparison does not include the final
post-import phase dump and must not be reported as full phase or pipeline parity.

## Build and generated code

From this repository's root, the baseline commands are:

```powershell
dotnet restore .\RyuJitSharp.slnx --verbosity quiet
dotnet build .\RyuJitSharp.slnx --no-restore --configuration Debug --verbosity quiet
dotnet build .\RyuJitSharp.slnx --no-restore --configuration Release --verbosity quiet
```

Restore when assets are absent or dependencies changed. Existing wrappers in
`scripts\build.ps1` also support restore/build/test and binary logging. Use the
smallest relevant test selection once tests exist, and verify a nonzero count.
Focused output tests do not establish compiler coverage. NativeAOT publish and
native loading are separate gates from these managed builds.

`sources\GenerateTables\Program.cs` reads `Inputs` and writes `Outputs` relative
to its working directory, deleting an existing `Outputs` subtree first. Run it
only in an explicitly identified generator workspace, never an arbitrary root.
Check input provenance, review generated output, and update the corresponding
files under `sources\Core`; generating output does not integrate it automatically.
Establish clean-baseline reproducibility before regenerating against new inputs.
`NamedIntrinsic` uses the pinned `namedintrinsiclist.h` enum body as well as the
hardware tables; validate the complete ordered enum, not just HWI row counts.
Continue sharing native table definitions through these generators. Small tools
for repetitive translation, provenance checks, inventories, or maintenance are
appropriate when they reduce repeated work or mistakes. Keep their scope narrow
and output reviewable; a general C++-to-C# translator is not a prerequisite.
