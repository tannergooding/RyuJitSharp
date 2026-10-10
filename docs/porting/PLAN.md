# Continuation plan

The whole-function porting sequence below is historical context. The managed
port is implemented and its source-port backlog is retired. The current
checkpoint and remaining parity evidence boundary are in [state.json](state.json).
Accepted behavior differences and confirmed upstream defects are recorded in
[DEVIATIONS.md](DEVIATIONS.md) and [UPSTREAM-REPORTS.md](UPSTREAM-REPORTS.md).

## Execution order and iteration

Resume from `checkpoint.nextAction` in [state.json]. Use the pinned upstream
revision for behavior comparisons; do not replay completed translation or setup
work.

The compiler pipeline remains the main parity boundary: rationalization,
lowering, LSRA, code generation and emission. Resolve tracked gaps without
making unrelated optional features prerequisites, and keep remaining
differences explicit rather than hiding them.

Parallelize substantial work as bounded, non-overlapping feature or platform
packets when dependencies and ownership allow. Select packets by dependency
readiness, ownership, and validation boundaries; do not gate one target's
progress on unrelated integration for another target. Give each packet explicit
source ownership, scope, and a completion boundary; do not invent assignments or
duplicate work. The coordinator owns dependency selection,
shared JIT/EE and ABI contracts, cross-packet integration, and parity decisions.
Resolve shared-contract changes there before dependent packets rely on them.
Integrate packets at target-aware gates: build the affected target and collect
the phase, code-generation, metadata, or execution evidence appropriate to its
implemented frontier. Record evidence per target and distinguish compilation
from execution parity. Use Sol for implementation, Luna for documentation, and
Astra for complex investigations, shared-contract decisions, and final
integration/review.

Work in substantial dependency-coherent batches during both synchronization and
new porting. Use source spot checks and localized incremental builds between
milestones, not exhaustive new fixtures or repeated full validation per helper.
The default completion boundary is an entire phase or a larger runnable section,
not each helper that it depends on. Keep native-source review, ownership analysis
and numeric-semantic care in the implementation loop; defer test authoring and
validation runs to that completion boundary unless a concrete defect blocks
progress. Unit tests remain valuable, especially given RyuJIT's limited unit
coverage, but completing missing compiler code is the immediate goal. Reuse the
port's existing unit coverage and add focused regressions for concrete defects
rather than growing a new validation matrix for every ported area. Keep builds
and relevant existing checks at coherent integration boundaries. Do not turn
helper-level commits into repeated testing, capture and documentation cycles.

At the port's completion gate, automate broader oracle/port dump comparisons
and run the official runtime test suite with the port as the primary JIT, not
an AltJIT. Preserve raw dumps and explicitly enumerate comparison exclusions
for accepted non-semantic differences such as D001 arena allocation counts and
bytes. Do not normalize away compiler decisions, IR, phase ordering, or generated
instructions. Selected-method AltJIT execution remains useful scoped evidence,
not a substitute for that primary-JIT test-suite gate.

Reuse the matching oracle product build and `Core_Root`. The native setup is
`.\build.cmd -subset clr -config checked`, then
`.\build.cmd -subset clr+libs -config release`, then
`.\src\tests\build.cmd x64 checked generatelayoutonly`.
Do not repeat that setup unless the native revision or required artifacts change.

Choose a required phase, port a substantial dependency-coherent section with
localized incremental builds or spot checks, then build RyuJitSharp and run a
small hello-world-style program using its DLL/PDB as the AltJIT against that
layout. The small program should execute in milliseconds; builds and publication
are separate costs, not part of each test invocation. Follow the next real
failure or missing transformation. Deep analysis and focused regression checks
belong at concrete bugs or mismatches, not automatically at every helper.
Use dumps and eventual disassembly to preserve fidelity without mistaking native
fallback or stub phases for managed code generation.

### Active batch contract

Before editing, record four fields in `checkpoint.activeBatch` in
[state.json](state.json), and use them when resuming work:

- **Scope:** the capability or bounded dependency closure being ported, and the
  larger milestone it serves. A helper alone is not the default batch boundary.
- **Done when:** the concrete completion condition, including the logical commit.
  Commit complete, validated units; keep incomplete functions as WIP and retain
  their native bodies. Do not add stubs to satisfy the boundary.
- **Validation:** select the smallest checks that establish this batch's outcome
  before writing tests. Default to a batch build and relevant existing checks.
  Add focused coverage for concrete defects or non-obvious managed adaptations,
  not a fixture for every translated helper.
- **Deferred:** explicitly name adjacent work that is not needed for this batch.
  Keep the original milestone visible when following prerequisites.

Read the relevant native contracts and managed APIs together before translating;
avoid discovering routine API differences through repeated build attempts.
Validation is not an automatic per-helper cycle: before/after replays, exhaustive
edge-case matrices, full suites, and NativeAOT captures need a specific reason.
Recapture native comparisons when the reachable path or observed output can
change, not merely because more support code exists.

Expand investigation only for a named failure, dump mismatch, unresolved
ownership/ABI contract, or numeric-semantic risk. Record that reason in the
active batch before expanding its validation. Fix and rerun failed checks as
needed; this is a scope constraint, not permission to leave failures unresolved.
If a prerequisite starts requiring another substantial subsystem, new tooling,
or extensive fixtures, reassess the boundary rather than silently expanding it.

After committing a batch, select the next batch and continue. A logical commit
is a checkpoint, not a reason to stop. At a substantial validated milestone,
update [MILESTONES.md](MILESTONES.md), commit the checkpoint locally, then continue
into the next dependency boundary. The completed handoff was published to main
with explicit authorization on 2026-09-26. That authorization does not extend to
future pushes or PRs; obtain explicit authorization before either.
Keep the journal reader-facing: explain capabilities, their significance and
remaining limitations, rather than test results, provenance or recovery details.
Keep only evidence needed for active decisions in `state.json`; completed
capabilities belong in the milestone journal and Git history.
Pause only for an explicit user request, a decision requiring approval,
a publication conflict, or a blocker that cannot be safely resolved.

## 0. Preserve and establish the starting point

Completed setup: the isolated C# branch started from `fgImport`, its latest WIP
was preserved separately, and the pinned upstream source was established as the
reference revision. The managed implementation and its tests now live in this
repository; current maintenance proceeds from the managed code and tracked gaps.

The initial unstashed C# baseline built in Debug and Release, before native
publish/loading and phase comparisons were established; its test project had no
test sources. Current revisions and accumulated validation are recorded in
[state.json](state.json), rather than inferred from this setup milestone.

## 1. Establish reproducible comparison inputs

Capture a small baseline before changing source revisions. Inventory existing
native artifacts rather than assuming they match. Build the required native
host/JIT/SuperPMI artifacts from the original oracle revision if matching ones
are unavailable. Record commands, flavors, hashes, and the JIT/EE version.

Publish the C# JIT for a supported host/target combination and verify its
exports, ABI, and loading. Establish a tiny local corpus covering straight-line
arithmetic, branches, locals, direct calls, and an inline candidate. Record the
actual phase frontier and known gaps without requiring the unfinished compiler
to emit code.

Check table-generator input provenance and baseline reproducibility in an
isolated output directory. Use a small comparison runner and existing tooling,
not a general orchestration framework. The runner must detect
wrong JIT selection, empty output, missing methods/phases, crashes, and fallback.
Use focused probes/fixtures to establish those safeguards; extensive unit-test
infrastructure is not required. Reconcile output newline handling at the shared
boundary while separately correcting any ported message-layout differences.

**Exit:** reproducible build/load commands, an explicit baseline report, and
focused checks of the comparison runner's failure cases. Any unavailable evidence is
named; no claim of current dump parity is required or implied.

## 2. Synchronize already ported code to the pinned upstream head

The initial synchronization covered 270 candidate files in 501 commits. The
2026-09-26 refresh starts from the previously reconciled `206bf81` and targets
immutable `33baf8e`; its raw JIT/interface delta contains 67 candidate files.
Inventory size does not imply that every change needs translation. Preserve both
revisions in `state.json`; do not chase a moving `main` while reconciling them.

The 2026-10-08 refresh reconciles immutable `33baf8e` to `6ff62b1d` across all
92 changed `src/coreclr/jit` and `src/coreclr/inc` paths (60 JIT, 32 include
paths). The repeatable capture, disposition, validation, and pin-update procedure
is [the oracle-update skill](../../.github/skills/oracle-update/SKILL.md).
Isolated regeneration matches all five affected generated outputs. The
21-method phase corpus matches for 20 methods; its sole `GenericCatch`
diagnostic difference is the already accepted D015. This evidence ends before
Importation and does not establish code-generation or runtime parity; the
dynamic TLS input-ownership boundary also remains unresolved. Full-runtime
parity is a separate stage in section 6.

The 2026-10-10 refresh reconciles immutable `6ff62b1d` to `e23d559a` across
9 changed `src/coreclr/jit` and `src/coreclr/inc` paths (16 hunks). It ports
Wasm equality-to-zero containment and code generation, constant reassociation
ownership, signedness-qualified boolean range folds, and checked-bound range
inference. Six CoreCLR include/runtime hunks have no managed counterpart, and
the Wasm comment-only hunk requires no managed change. No managed table input
or output changed, so table regeneration was not required. Full Debug Core
analysis/tests pass (17,653); focused Wasm comparison tests pass in Debug and
Release (11 each). This is source and focused-target evidence, not full runtime
parity; D015 remains accepted, dynamic TLS input ownership remains unresolved,
and `genAsyncResumeInfo` stays deferred until B209's target-width table/emitter
dependency closure is ported and tested.

Prefer substantial source sections and their dependency closure over individual
helper batches. Use builds and small focused checks for concrete behavior changes
or translation defects; reserve NativeAOT publication and corpus recapture for
milestones, ABI changes, or changes that can affect the observed dump frontier.
Do not repeatedly capture an unchanged frontier merely because another helper
was ported. Commit each verified coherent batch, keeping checkpoint updates brief.

Process dependency-coherent batches in this order:

1. JIT/EE interfaces, GUID, native layouts, calling conventions, flags, enums,
   and interface thunks. Audit the contract; changing the GUID alone is unsafe.
2. Table-generator inputs and generated definitions, including instruction sets,
   registers, opcodes, phases, intrinsics, and configuration defaults.
3. Shared types and already ported functions, following their actual dependencies:
   initialization/ABI, blocks/IR/locals, importer/calls/inlining policy, and
   diagnostics. Apply their actual old/new upstream deltas, inspecting enough
   surrounding context to preserve the contract without re-porting unchanged bodies.
4. Relevant support-code/include changes outside those directories, followed by
   a final sweep of the classified inventory and target-specific compilation.

For each batch, record affected native files/symbols, C# destinations, dispositions,
and evidence. Preserve new target branches and their dependency calls; use tracked,
terminating helper stubs for unported other-target dependencies where needed.
An entire inline branch replaced with NYI remains a partial translation. Do not
introduce silent stubs on required target paths to make reconciliation
appear complete.

Use the raw old-to-new upstream diff, not the size of merge-conflict regions, to
identify managed behavior that needs synchronization. Apply changed-method
deltas to the existing C# implementation and review changed APIs, tables,
generators, types and JIT/EE contracts. If a pass becomes disproportionately
expensive, reassess its scope and approach rather than expanding the investigation.

Advance the recorded upstream baseline only with preserved old-baseline evidence.
Compare the old/new native delta with the existing C# implementation, and track
unresolved deltas as explicit mixed-revision exceptions until reconciliation is
complete.

Rebuild matching native artifacts and recapture version-specific replay inputs
when the ABI or collection format requires it. Do not silently reuse incompatible
old collections. Compare the updated C# port against the updated native oracle;
upstream-intended changes are not C# deviations.

**Exit:** every relevant upstream change has a disposition; the ABI is
reconciled; generated changes are explained; Debug/Release and relevant checks
pass; the supported phase corpus has new-baseline evidence. Only then advance
the recorded port baseline.

## 3. Reconcile and resume the saved C# work

Apply preserved C# WIP by immutable ID, keeping the snapshot. It affects
inlining/flowgraph support, tree visitors, local sequencing, LIR, rationalization,
and support-file moves. A preserved snapshot is not evidence that its contents
are already integrated or covered.

Resolve conflicts against the updated native definitions and completed sync
batches. Preserve the WIP's intent and file moves; do not gratuitously repeat or
reformat it. Inventory its whole-function completion and remaining stubs before
marking anything ported. Validate newly reachable paths and extend the phase
corpus through each supported boundary.

**Exit:** WIP integrated without losing behavior, complete functions reconciled,
new platform deferrals documented, and focused regression/phase evidence saved.
Retain recovery refs until these checks are complete.

## 4. Continue by compiler dependencies

Select the next required minopts phase and its dependency cluster from the
backlog and actual reachable frontier. Prioritize mandatory importer,
morph, rationalization, lowering, register allocation, code generation, and
GC/EH/unwind support. Inlining and other optional optimizations can wait.
Do not bypass required transformations or claim an optional phase is implemented
merely because minopts does not need it.

Port functions completely while validating milestones at phase boundaries.
Add corpus dimensions deliberately: exceptional flow, generics and structs,
floating-point edge cases, intrinsics, tailcalls, managed/native boundaries,
tiering/PGO/OSR, and stress. Broaden SuperPMI collections after the small corpus
is reliable. Compare emitted bytes/metadata and execute code once supported.

Execution and parity evidence are target-specific, not a serialization gate for
independent architecture packets. Compile-check affected target branches and
inventory explicit NYIs without claiming runtime support. Never infer one
target's behavior from another target's run, and do not treat compile-only work,
native fallback, or `CORJIT_SKIPPED` as execution parity.

**Exit per batch:** complete intended behavior, explicit deferrals, scoped
regression/parity evidence, and a small continuation checkpoint naming the next
dependency, committed together as one logical batch.
Do not start the next batch with completed work still waiting for a commit.

## 5. Post-port work

The source port is established. Keep post-port refactoring, stronger unit-test
coverage, and more substantial C#-specific designs outside the porting contract
unless separately approved. Use the deviation and upstream-report registers for
the existing behavior record; do not recreate the retired source-port backlog.

## 6. Full-runtime parity verification

Start this stage only after the upstream synchronization exit in section 2 is
accepted and its recorded baseline has advanced. The synchronization phase
corpus is a prerequisite for accepting that update; the broader captures below
are a separate completion gate and must not be generated early.

### Primary-JIT setup

Build the checked native runtime, libraries, and `Core_Root` from the accepted
oracle revision. Publish RyuJitSharp for the same host, target, and ABI, then use
an isolated `Core_Root` copy with the managed native image placed as `clrjit.dll`.
Keep an untouched native control root. Record build identities, file hashes,
JIT selection, flags, and environment; verify the managed JIT is the main JIT
and that no AltJIT or native fallback supplied the observed output.

Inspect `CILJit.compileMethod` for a process-wide compilation lock before
parallel captures or suite runs. If it serializes independent compilations,
remove it only after checking shared mutable state and adding concurrency
coverage; do not trade correctness for throughput.

### Differential sequence

1. Start with `System.Private.CoreLib` disassembly. Use the same deterministic
   workload or SPMI collection and capture its complete CoreLib compilation set,
   not only hand-picked methods. Hold host/target/ISA, tier, PGO inputs, stress
   settings, and JIT options constant for native and managed runs. Enable
   upstream diffable disassembly, retain raw outputs, and establish a native
   repeat control. Require exact method/compilation counts and investigate each
   managed mismatch. Compare relocation-aware machine code and GC/EH/unwind
   metadata where applicable; text disassembly alone is not a full code-generation
   pass.
2. Compare full compilation dumps for the same corpus and configuration.
   Reuse `Compare-PortingDumps.ps1` where its phase coverage is sufficient;
   extend the existing comparator narrowly when needed to cover the remaining
   phases. Preserve raw captures and semantic output such as tree/block IDs,
   ordering, diagnostics, and costs. Apply only applicable exceptions already
   recorded in `DEVIATIONS.md`; D001 permits allocation-statistics differences
   only and does not imply other allocator-related output. Investigate every
   other mismatch instead of normalizing it away. Require comparison-tool checks
   that prove real compiler differences are still detected.
3. Expand the same disassembly and dump comparisons from CoreLib to the
   relevant runtime libraries, then to the general runtime test corpus. Keep
   a native-JIT control run for the same `Core_Root`, test selection, and
   environment. Compare per-test outcomes and diagnostics as well as generated
   code; investigate failures, timeouts, skipped compilations, and evidence of
   fallback rather than treating them as passes.
4. Run the required runtime/CoreCLR tests with RyuJitSharp as the primary JIT,
   not as an AltJIT. The gate is the full required test selection passing under
   the managed `clrjit.dll`, including the RyuJitSharp unit suite and the full
   CoreCLR/runtime-library test suites supported by the built `Core_Root`.
   Report suites blocked by infrastructure or unsupported configurations
   explicitly; they do not count as passing. Selected-method AltJIT runs and
   SPMI replay are useful diagnostics, not substitutes for this result.

After the CoreLib baseline and common JIT setup are accepted, library-capture
cohorts and independent test-suite groups may run in parallel when resources
permit. Use separate `Core_Root` copies and output directories; do not share
mutable build, capture, or comparison outputs between workers.

Use the pinned runtime's SuperPMI tooling when its collection format and JIT/EE
ABI match the accepted oracle. First verify that collection and replay select
the intended JIT and produce non-empty, complete results. Parallelize
independent deterministic cohorts only after confirming compilation is
thread-safe; keep tiering, PGO, OSR, and concurrency as explicit later
dimensions.

The completion report must include both revisions and binary hashes, toolchain
and `Core_Root` identity, host/target/ISA, exact commands and environment,
corpus identity, expected and observed counts, native repeat-control results,
failures/skips, every applied exception, and raw/diff artifact locations.
Preserve captures outside maintained documentation; update the checkpoint and
deviation register only for unresolved evidence or accepted observable
differences.

## Approval and escalation

With this plan approved, routine translations, validation, source mapping, and
documented checkpoint updates can proceed without repeated design questions.
Ask before changing the parity contract, accepting additional output exceptions,
substantial redesign, intentional ABI/ownership divergence, or discarding prior
work. A new upstream update is a separate pinned batch, not an automatic
background action. Publishing remains separately gated.
