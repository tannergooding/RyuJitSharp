# Continuation plan

The plan, three-tree workflow, Windows-x64-first whole-function policy, and
centralized newline proposal were approved on 2026-09-22. Prioritize a clean port
with minimal divergence; extensive tooling tests and larger restructuring are
deferred. Exact revisions and the checkpoint are in [state.json](state.json).

## Execution order and iteration

First finish reconciling already ported code and the native residual tree with
the pinned upstream `main` revision. Then reconcile and resume the preserved WIP.
Only after that, start new phase porting, prioritizing required Windows-x64
minopts transformations toward code generation over optional optimizations.
Do not expand synchronization into implementing every currently unported phase:
record those boundaries accurately and reconcile existing implementations.

Work in substantial dependency-coherent batches during both synchronization and
new porting. Use source spot checks and localized incremental builds between
milestones, not exhaustive new fixtures or repeated full validation per helper.

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
  Commit complete, validated units; keep incomplete Windows-x64 functions as WIP
  and retain their native bodies. Do not add stubs to satisfy the boundary.
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
is a checkpoint, not a reason to stop; stop at a substantial milestone, an
explicit user pause, or a decision requiring approval.

## 0. Preserve and establish the starting point

Completed setup: the isolated C# branch starts from `fgImport`, its latest WIP
is preserved separately, and the native deletion ledger has a protected snapshot.
`runtime-port` was restored exactly, including index state. `runtime-oracle`
was established as an intact detached worktree at the original native revision.

The initial unstashed C# baseline built in Debug and Release, before native
publish/loading and phase comparisons were established; its test project had no
test sources. Current revisions and accumulated validation are recorded in
[state.json](state.json), rather than inferred from this setup milestone.

## 1. Establish reproducible comparison inputs

Capture a small baseline before changing source revisions. Inventory existing
native artifacts rather than assuming they match. Build the required native
host/JIT/SuperPMI artifacts from the original oracle revision if matching ones
are unavailable. Record commands, flavors, hashes, and the JIT/EE version.

Publish the C# JIT for Windows x64 and verify its exports, ABI, and loading.
Establish a tiny local corpus covering straight-line arithmetic, branches,
locals, direct calls, and an inline candidate. Record the actual phase frontier
and known gaps without requiring the unfinished compiler to emit code.

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

The candidate update touches 270 files across the initial JIT/interface search
areas in 501 commits. This is an inventory bound, not a claim that every change
needs translation. The source and target revisions are frozen in `state.json`;
do not chase a moving `main` while reconciling them.

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
   diagnostics. Reconcile whole functions, not only changed lines.
4. Relevant support-code/include changes outside those directories, followed by
   a final sweep of the classified inventory and target-specific compilation.

For each batch, record affected native files/symbols, C# destinations, dispositions,
and evidence. Label still-unported changes as remaining work; explicitly defer
new non-Windows-x64-only behavior with NYIs where needed. Do not introduce silent
stubs on relevant Windows-x64 paths to make reconciliation appear complete.

Use the raw old-to-new upstream diff, not the size of merge-conflict regions, to
drive reconciliation. Preserve previous deletions of ported methods, retain net
new APIs that remain unported, and apply changed-method deltas to their existing
C# implementations. Large deletion conflicts do not require reviewing or restoring
thousands of unchanged lines. If a pass becomes disproportionately expensive,
reassess its scope and approach rather than expanding the investigation.

Move the oracle to the pinned target only with a clean source tree and preserved
old-baseline evidence. Update the residual tree with its snapshots protected.
For modify/delete conflicts, compare old native code, new native code, and the C#
implementation: retain newly unported behavior in the residual view, and remove
it only when accounted for. Never resolve all such conflicts as "keep deleted."
Track mixed-revision progress until reconciliation is complete.

Rebuild matching native artifacts and recapture version-specific replay inputs
when the ABI or collection format requires it. Do not silently reuse incompatible
old collections. Compare the updated C# port against the updated native oracle;
upstream-intended changes are not C# deviations.

**Exit:** every relevant inventory entry has a disposition; both native trees
use the target revision; the ABI is reconciled; generated changes are explained;
Debug/Release and relevant checks pass; the supported phase corpus has new-baseline
evidence. Only then advance the recorded port baseline.

## 3. Reconcile and resume the saved C# work

Apply the preserved C# WIP by immutable ID, keeping the snapshot. It affects
inlining/flowgraph support, tree visitors, local sequencing, LIR, rationalization,
and support-file moves. The native deletion snapshot already includes work from
this WIP; it must not be interpreted as the committed C# baseline's coverage.

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
residual source and actual reachable frontier. Prioritize mandatory importer,
morph, rationalization, lowering, register allocation, code generation, and
GC/EH/unwind support. Inlining and other optional optimizations can wait.
Do not bypass required transformations or claim an optional phase is implemented
merely because minopts does not need it.

Port functions completely while validating milestones at phase boundaries.
Add corpus dimensions deliberately: exceptional flow, generics and structs,
floating-point edge cases, intrinsics, tailcalls, managed/native boundaries,
tiering/PGO/OSR, and stress. Broaden SuperPMI collections after the small corpus
is reliable. Compare emitted bytes/metadata and execute code once supported.

Windows x64 remains the first execution target. Compile-check affected alternate
target branches and inventory their explicit NYIs without claiming runtime
support. Never infer non-Windows behavior from a Windows run.

**Exit per batch:** complete intended functions, explicit deferrals, scoped
regression/parity evidence, residual-source update, and a small continuation
checkpoint naming the next dependency, committed together as one logical batch.
Do not start the next batch with completed work still waiting for a commit.

## 5. Restructure after establishing the clean port

Use [BACKLOG.md](BACKLOG.md) to preserve encountered bugs, rename/refactoring
candidates, and restructuring ideas without implementing them incidentally.
After the port is established, revisit module boundaries, stronger unit-test
coverage, and more substantial C#-specific designs as a separately approved
rewrite. Maintain the clean port as the behavioral reference for that work.

## Approval and escalation

With this plan approved, routine translations, validation, source mapping, and
documented checkpoint updates can proceed without repeated design questions.
Ask before changing the parity contract, accepting additional output exceptions,
substantial redesign, platform reprioritization, intentional ABI/ownership
divergence, or discarding prior work. A new upstream update is a separate pinned
batch, not an automatic background action. Publishing remains separately gated.
