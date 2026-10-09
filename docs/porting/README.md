# Port behavior and parity contract

RyuJitSharp ports RyuJIT to C# while preserving compiler behavior. The intended
observable results are identical generated code, `jitdisasm`, and `jitdump` for
the same inputs and configuration, except for explicitly approved differences.
Internal C# representation may differ without changing those results.

Start with [state.json](state.json) for revisions and the current checkpoint.
Read the [milestone journal](MILESTONES.md) to catch up on completed capabilities,
project history and remaining work without replaying the conversation.
The [continuation plan](PLAN.md) records the porting sequence; the
[deviation register](DEVIATIONS.md) distinguishes accepted changes from existing
limitations.
Use the [backlog](BACKLOG.md) only for unresolved findings, decisions, blockers,
coverage gaps, and deferred work; remove entries when their actions are complete.
Keep completion evidence with tests, artifacts, and commits, and record accepted
observable differences only in the deviation register. The port is implemented
in this repository; the exact upstream source revision used for reference is
recorded in [state.json](state.json). Remaining correctness, parity and coverage
work belongs in the backlog, not in a parallel inventory of native definitions.
Confirmed upstream defects that should be reported, but must remain unchanged in
the managed port for parity, are listed in [UPSTREAM-REPORTS.md](UPSTREAM-REPORTS.md).
Items needing an owner decision are separated under the backlog's
owner-review section and should not be implemented as approved changes before
sign-off.

An implemented managed body, a passing build, or an explicit unsupported-target
path does not by itself establish runtime or generated-code parity. Compare
against the pinned upstream behavior for the relevant configuration, and state
the scope of evidence precisely. Do not infer completeness from file counts,
source-line counts, compilation, or the absence of native source in this repo.

Preserve pre-existing changes, including untracked files and staged/unstaged
distinctions. If a recovery snapshot is necessary, record its immutable ID and
keep machine-local paths and commands outside maintained documentation. Never
use `stash pop` as the only backup or interpret `stash@{0}` as a stable identity.

## Unit of work

Resolve backlog items in dependency-coherent batches. Use the pinned native
reference and managed tests to establish the relevant behavior, and validate at
the narrowest meaningful integration boundary. Before a substantial batch,
record its scope, completion condition, validation and deferred work in
`checkpoint.activeBatch`. After validation, update the backlog, deviations,
checkpoint and evidence as applicable; commit locally and continue.
Push or open a PR only with explicit authorization; a local commit is not
publication approval. Do not rewrite published history or update other branches
without separate approval.

Fix the whole relevant behavior rather than a fragment selected to get one test
running. Preserve target-specific control flow and dependencies. Keep unsupported
paths explicit and terminating, and record their remaining behavior when it
affects a tracked item. Other targets must remain compilable; compilation is not
proof of their execution support.

A shared native dispatcher may be split along its existing phase/mode predicates
when that avoids making an unsupported optional phase a prerequisite of the
active path. Implement the complete supported mode and make that mode explicit
at its callsites. This does not permit returning a fallback for required
behavior.

Preserve phase order, traversal and insertion order, identity semantics, enum
values, integer widths, overflow/truncation, signedness, shifts, floating-point
NaNs and signed zero, and error propagation. Managed objects must not invalidate
native pointer lifetimes or JIT/EE layout and calling-convention contracts.
Review collection substitutions for ordering and equality changes.

Native cross-kind node bashing is intentionally replaced by whole-node
replacement in C#. Update callers, owning uses, cached aliases and LIR links
as required; do not treat unchanged physical object identity as a porting goal.
Preserve semantic relationships and dump-visible IDs/order. This approved
representation policy, with existing inline-argument prior art, is covered by
D002/B064; it does not require an IR type-hierarchy redesign.

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

### Fast inner-loop builds

Use `.\build.cmd -fast -test` for routine compile/test iterations, optionally
with `-solution tests\Core\RyuJitSharp.UnitTests.csproj` to select a project.
The Bash equivalent is `./build.sh --fast --test`. The switch applies
`RunAnalyzers=false` and `GenerateDocumentationFile=false` only to the build
action. It defers .NET analyzer/style and trimming analysis and XML documentation,
not C# compilation, nullable checking, source generation or requested tests.
Restore, test and packaging actions retain their existing behavior.

For direct `dotnet build` or `dotnet test` iterations, use
`-p:RunAnalyzers=false -p:GenerateDocumentationFile=false`. Full analysis remains
the default: build and test without these overrides in the same configuration
before committing a porting batch, and keep final NativeAOT publication and
execution validation unchanged. Fast-mode success is not a full-analysis checkpoint.
CLR API timing can be compiled into Release with
`-p:RyuJitMeasureClrApiCalls=true`; Debug intentionally rejects this option because
timing Debug compiler code is not meaningful.

Full analysis uses the repository's analyzer configuration. The root
`.editorconfig` temporarily disables CA1508, CA2329, CA2330, CA3001, CA5390 and
CA5403 because of upstream dataflow-analysis performance issues; the rule entries
link their tracking issues and fix. Leave the remaining analyzers enabled for
acceptance builds. Isolated validation snapshots must include this configuration
and record it separately from compiler-source changes.

Cross-target ABI, intrinsic and register-policy fixtures also run through
`tests\Targets\RyuJitSharp.Target.UnitTests.csproj`. This project links the relevant
fixtures without compiling the Windows-x64 backend suite; it does not remove or
disable fixtures in the original Core test project. For example:

```powershell
dotnet test tests\Targets\RyuJitSharp.Target.UnitTests.csproj -c Debug -r win-x64 -p:TargetRuntimeIdentifier=linux-arm64
```

Use Release as well, and select `win-arm64`, `osx-arm64` or `linux-x64` for the
other covered ABI variants. The runtime RID selects the test host; the target
RID selects compiler semantics. These are targeted managed unit results, not
execution of generated code on those targets or a pass of the entire Core suite.
Wasm normally excludes profiler support; pass
`-p:RyuJitEnableWasmProfilingTests=true` to compile and run the Wasm profiling
leave-callback boundary tests without changing the default target configuration.
For the non-Debug ARM64 late-disassembly path, pass
`-p:RyuJitEnableArm64LateDisasm=true` with an ARM64 target RID:

```powershell
dotnet test tests\Targets\RyuJitSharp.Target.UnitTests.csproj -c Release -r win-x64 -p:TargetRuntimeIdentifier=win-arm64 -p:RyuJitEnableArm64LateDisasm=true --filter "FullyQualifiedName~Arm64EmitterAlignmentCostTests"
```

This enables the `LATE_DISASM` and `USE_COREDISTOOLS` symbols for that target
without changing the ordinary Release configuration.

### C# layout

Preserve recognizable algorithms without copying native layout or compressing
the translation. Follow the surrounding C# conventions: multiline control-flow
bodies, braced switch sections, and one executable statement per line. Separate
switch sections with a blank line, and wrap long calls and expressions at
meaningful boundaries.

Use whitespace for visual balance and readable flow, like paragraph structure.
Assertions, values, computations, and returns may belong together or form
separate groups depending on their purpose in the surrounding code. Keep things
together when that makes their relationship clearer; separate unrelated steps
and setup or cleanup that forms its own logical component. A blank line before
a return or around control flow should clarify that structure, not satisfy a
mechanical rule based on the statement kind.

`.editorconfig` governs mechanical formatting, but cannot identify logical
groups or choose useful line breaks. Its preservation of single-line blocks
is not a reason to compress newly ported control flow. Review these aspects
explicitly before committing, including tests and supporting code.

## Sparse tracking

Use the pinned upstream source and existing `sourceMap` as navigation aids. Most
source locations follow `src/coreclr/jit/<stem>.{h,cpp}` to
`sources/Core/jit/<stem>/`, with similar `inc` and `jitshared` mappings. This is
a navigation convention, not a completeness test: native implementation files
such as `importer.cpp`, `fginline.cpp`, and `lclvars.cpp` contribute to C#
`Compiler` partials.

The initial `sourceMap` in `state.json` contains coarse navigation hints for
active and cross-cutting areas, not a complete function inventory. Extend it
when discovering a non-obvious mapping. Split it by native area if it becomes
large; do not load or rewrite an entire inventory for each task.
An entry with `csharpSnapshot` describes preserved WIP, which may not exist in
the working tree yet; resolve its paths in that Git tree until integration.

Keep `state.json` as a concise resume cursor, not a completion catalog. Record
only active work, target-specific NYIs, non-obvious deviations, unresolved
decisions and validation gaps. Include the upstream symbol/path, C# destination
and remaining action only when a simple directory convention or search cannot
recover them. Advance the recorded upstream revision only after relevant
changes have been handled.

Keep implementation and evidence separate:

| Implementation status | Meaning |
| --- | --- |
| Not implemented | Managed behavior is missing or incomplete. |
| Unsupported | The managed path terminates explicitly rather than claiming support. |
| In progress | Work is preserved but incomplete or not integrated. |
| Implemented | Managed behavior is present; parity still requires relevant validation. |

Retain validation evidence in the active checkpoint only while it informs an
open decision or uncommitted unit. Git history, the milestone journal and
reproducible artifact paths preserve completed work; do not duplicate a
function-by-function acceptance ledger in `state.json`. No percentage should be
inferred from removed lines, file counts, or TODO counts.

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

Keep artifact retention bounded as well. Retain active comparison inputs,
saved-WIP recovery material and reproducers for unresolved defects. Remove
completed temporary build exports after their code and validation checkpoint are
committed; do not touch another worker's active snapshot or shared build outputs.

Superseded session sources, patches, manifests and diagnostic evidence may be
compacted into `port-history.zip` in the session's artifact directory. Verify
the archived bytes before removing loose originals. Discard obsolete rebuildable
binaries rather than archiving every build. Historical build paths in older
records are not guaranteed to remain live; preserve their source/revision and
build instructions, while keeping current checkpoint inputs directly available.
The milestone journal remains the capability history, not an artifact inventory.

For upstream synchronization, compare immutable old and new oracle revisions,
then apply relevant deltas to the managed implementation. Review changed tables,
generators, types and JIT/EE contracts along with affected methods. Track only
unresolved exceptions; do not recreate a native-definition inventory. Git history
and the pinned upstream revision retain the reference source for translated code.
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
`artifacts\bin\sources\PortingCorpus\Release\net11.0\PortingCorpus.dll`.
It covers arithmetic, branches, locals, direct and managed indirect calls,
inlining, scalar and hardware-intrinsic folding, synchronized methods, generic
exception handling, P/Invoke transitions, return merging and local addresses.
`FoldConstants` uses `BitConverter` intrinsics to keep constants in IL until JIT
import, exercising casts and arithmetic without Roslyn folding them first.
`FoldFloating` exercises addition of negative zero, multiplication/division by
one, and subtraction of positive zero; its caller checks the negative-zero bit
pattern. The native and managed import prefixes contain all four folding
diagnostics with identical logical node IDs.
`LocalAddressStore` exercises a partial store through a local address.
`LocalAddressDifference` exercises pointer subtraction, optimized address
propagation and cleanup of unread address temporaries. These cases compare
minopts and optimized local morph without requiring managed code generation.
`ImplicitByRefArgument` passes a three-long struct through the Windows-x64
implicit-byref ABI and exercises parameter descriptor retyping before global
morph.
`LoopSum` exercises a counted loop, while `IndexedArray` exercises dynamic
array allocation, length-based iteration, and variable-indexed loads and stores.
It is a standalone fixture, not a compiler coverage claim.

Use `scripts\porting\Invoke-PortingCorpus.ps1` with `-CoreRoot`, `-Corpus`,
`-OutputDirectory`, and the exact `-NativeCommit`. Capture the native run first.
For the managed run, additionally supply `-ManagedJit` and `-ManagedSource`
identifying the C# revision and any preserved uncommitted source snapshot.
The host must be Checked/debug and ABI-compatible with that managed binary.
The runner isolates the child environment, rejects reused output directories,
requires exactly the expected case-sensitive compilation headers, and records
binary hashes, settings, timeout/exit status, and the raw dump. It sets
`DOTNET_GCConserveMemory=5` for each compiler execution.
Repeat a name in `-ExpectedMethods` when multiple overloads or generic
instantiations are expected; the runner checks that exact count for each name.
Names use ordinal equality, so canonically equivalent Unicode spellings remain
distinct. By default, the runner sets `RunAltJitCode=0`, requesting nonexecuting
mode from a Debug
managed JIT; process success is not generated-code success. Add
`-ExecuteManagedCode` only when validating the implemented backend.
It requires `-ManagedJit` and records the execution request in the manifest.
Check that every selected method completed managed compilation without a skip
or fallback before treating a successful corpus run as managed execution.

Use `-RegisterStress` with a native LSRA stress mask to exercise allocation
under restricted registers or forced spills. For example, `3` selects the
small register set and `0xC03` additionally forces spilling and reloads.
The runner formats this integer as the hexadecimal `JitStressRegs` setting;
zero leaves the setting absent. Combine it with `-GcStress` to check stressed
allocation while collections occur.
Use `-RegisterStressRange` with native hexadecimal method hashes or ranges
to limit stress to the intended methods; it requires nonzero `-RegisterStress`.
This can avoid stressing unrelated framework startup, but does not establish
that an unrestricted stress configuration works.

Use `-LsraOrdering` to supply the native 17-letter heuristic order, using
`A` through `Q`; repeated letters are allowed. Unlike `-RegisterStressRange`,
this setting applies to all compilations in the process. The pinned native
reverse-selection and nearest-selection stress bits do not implement those
selection policies, so enabling those bits alone is not evidence of exercising
different heuristic ordering.

Use `-MinOpts` on both captures to exercise required, unoptimized compilation;
the default method set then includes `InlineCandidate`, which is no longer
inlined. For optimized captures that exclude the still-unported object stack
allocation analysis, use `-DisableObjectStackAllocation` on both runs. These
switches are recorded in the manifest and do not themselves enable managed
execution.

Use `-OptimizationRepeatCount 2` on native and managed optimized captures to
exercise two optimization iterations, including annotation reset and CFG
recomputation between them. Zero leaves repeated optimization disabled.
This cannot be combined with `-MinOpts` or `-InstrumentedTier0`. Verify the
iteration diagnostics as well as generated-code execution; compilation headers
alone do not prove that the selected iterations ran.

Use `-FlowGraphFormat Dot` or `Xml` to capture `graphs.dot` or `graphs.fgx`
alongside the compilation dump. `-FlowGraphPhase` selects post-phase enum
suffixes (for example, `IMPORTATION,VALUE_NUMBER`), not display abbreviations;
the default is `DETERMINE_FIRST_COLD_BLOCK`. DOT additionally supports
`-FlowGraphEH`, `-FlowGraphLoops`, and `-FlowGraphMemorySsa`. Compare the actual
graph files: single-block methods are deliberately excluded by native selection.
The pinned native XML jump-kind table is stale (B350); its defined labels are
preserved, while its out-of-bounds switch entry produces an explicit managed
error instead of an undefined memory read. DOT does not have this limitation.

Use `-ReportMetrics` to publish final metrics to the EE and `-DumpOrder` to
capture ordered method summaries, restricted to the corpus assembly.
`-InlineDumpData 1` captures inline CSV on stderr; `-InlineDumpXml 1` captures
inline trees, with `-InlineXmlFile` selecting an append-mode `inlines.xml`.
XML mode 2 omits methods without inlines, mode 3 also omits failed inline nodes,
and data mode 2 embeds per-inline policy data. Optional policies whose native
data hooks remain unported fail explicitly. Compare selected method records:
the native JIT also compiles framework methods. Allocation bytes use D001's
managed accounting, timing values vary between runs, and the AltJIT summary
region is intentionally different from the native control's region.

Use `-TimingCsv` and `-TimingSummary` for `timing.csv` and `timing.txt`.
`-LoopHoistStats` and `-EnregistrationStats` select process-wide shutdown
reports. Verify actual data rows, XML closure and report output rather than
file creation alone. The AltJIT's aggregates cover its own compilations, not
the framework methods compiled by the default JIT. Timing counter units differ
from native RDTSC (R005); allocation columns follow D001.

Use `-InstrumentedTier0` to request eager Tier0 profiling without call-count
promotion to later tiers. This sets `TieredPGO_InstrumentOnlyHotCode=0` and
disables runtime call counting; it is distinct from forced `-MinOpts` and
cannot be combined with it. Add `-BlockCounters` to select block rather than
edge probes. Check the instrumentation schemas and inserted probes, not just
the tier flags or phase completion. The default method set includes the
non-inlined `InlineCandidate` in this mode.

Use `-RawHexCode` on native and managed captures to retain emitted hot-code
bytes in the dump. This is independent of `-ExecuteManagedCode`; raw bytes that
contain relocated addresses can differ between processes and require
relocation-aware comparison.

`scripts\porting\compare_helper_captures.py` compares configured helper cohorts
using `--config`, `--native-root`, `--native-repeat-root`, `--managed-root`,
`--managed-source`, `--native-host-receipt`, and `--output`. Each capture root
contains one directory per cohort produced by `Invoke-PortingCorpus.ps1`.
It checks exact methods, inputs, positive helper work, complete named phases
through the next phase start, and raw-byte coverage against complete method size.
Only CRLF-to-LF normalization is applied. Missing cold bytes, fallback, invalid
captures, differences, and native/native repeat variation produce a failing exit
and a JSON report; no address or relocation masking is performed. Consequently,
address-bearing methods can fail even the native repeat control. Referenced
data and relocation-target contents are outside this comparison's coverage.
The focused tooling tests run with
`python -B .\tests\PortingTools\test_helper_capture_comparison.py`.

Use `-GcStress` to request collections at every allowable JIT-compiled
instruction (`GCStress=4`). Run a native baseline with the same switch; add
`-ExecuteManagedCode` for the managed capture. A normal execution pass does not
establish GC-stress correctness, and a timeout or skipped compilation is not a
stress pass.

`scripts\porting\Compare-PortingDumps.ps1 -NativeDump <file> -ManagedDump <file>
-OutputPath <report.json>` compares each compilation from its start header up
to, but excluding, `Finishing PHASE Importation`. It preserves CR/LF and reports
the first differing line. Changed compilation order/identity, missing methods,
or missing phase boundaries are errors;
ordinary differences are diagnostic report entries, not a failing process exit.
This deliberately narrow prefix comparison does not include the final
post-import phase dump and must not be reported as full phase or pipeline parity.

## Build and generated code

Use the .NET 11 RC1 SDK selected by `global.json`; managed partial/hexadecimal
double parsing requires .NET 11. Both build wrappers use that file when
bootstrapping an architecture-specific SDK. The installed SDK and the pinned
native oracle remain independent toolchains.

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

The core test project obtains compilation symbols from the referenced core
project before compiling. Target- and feature-guarded tests therefore use the
implementation's actual configuration, rather than a separate symbol list.

`AnalysisModeStyle=Default` preserves the prior style-rule selection: .NET 11
otherwise also applies `AnalysisLevel=latest-all` to style diagnostics. Explicit
`.editorconfig` rules, build-time style enforcement, all quality analyzers and
warnings-as-errors remain enabled.

For Windows NativeAOT publication, initialize the matching Visual C++ environment
and run `dotnet publish sources\Core\RyuJitSharp.csproj -c Debug -r win-x64
--no-restore -p:Platform=AnyCPU -p:NativeLib=Shared
-p:IlcUseEnvironmentalTools=true -o artifacts\<output>` in that same process.
The environmental-tools setting uses the initialized linker rather than SDK
rediscovery; keep the Visual Studio Installer directory containing `vswhere.exe`
on that process's `PATH`.

`sources\GenerateTables\Program.cs` reads `Inputs` and writes `Outputs` relative
to its working directory. It verifies that the working directory contains
`GenerateTables.csproj` and `Inputs` before deleting an existing `Outputs`
subtree, preventing an invocation from an unrelated directory from deleting its
`Outputs`. Run it from `sources\GenerateTables`, check input provenance, review
generated output, and update the corresponding files under `sources\Core`;
generating output does not integrate it automatically. The CLR API timing
generator produces both the managed forwarding methods and their
`UnmanagedCallersOnly` `ICorJitInfo` vtable proxy.
Establish clean-baseline reproducibility before regenerating against new inputs.
`NamedIntrinsic` uses the pinned `namedintrinsiclist.h` enum body as well as the
hardware tables; validate the complete ordered enum, not just HWI row counts.
The xarch instruction tuple table uses the same ordered instruction inputs as
the instruction enum, preserving combined tuple flags for lowering and emission.
`Inputs\ternarylogic.inc` contains the 256 ordered initializer rows extracted
from the pinned `hwintrinsic.cpp` ternary decomposition table. Preserve all
operation/use pairs and row order when refreshing it; the generator packs each
row into the managed lookup table.
Continue sharing native table definitions through these generators. Small tools
for repetitive translation, provenance checks, inventories, or maintenance are
appropriate when they reduce repeated work or mistakes. Keep their scope narrow
and output reviewable; a general C++-to-C# translator is not a prerequisite.
