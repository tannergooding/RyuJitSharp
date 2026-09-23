# Continuation plan

The plan, three-tree workflow, Windows-x64-first whole-function policy, and
centralized newline proposal were approved on 2026-09-22. Prioritize a clean port
with minimal divergence; extensive tooling tests and larger restructuring are
deferred. Exact revisions and the checkpoint are in [state.json](state.json).

## 0. Preserve and establish the starting point

Completed setup: the isolated C# branch starts from `fgImport`, its latest WIP
is preserved separately, and the native deletion ledger has a protected snapshot.
`runtime-port` has been restored exactly, including index state. `runtime-oracle`
is an intact detached worktree at the original native revision.

The unstashed C# baseline builds in Debug and Release. This establishes a build
baseline only: native publish/loading, phase parity, and executable codegen have
not been validated. The test project currently contains no test sources.

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

Select the next complete function/dependency cluster from the residual source
and actual reachable frontier. Prioritize finishing the importer/inlining/morph
support needed by the corpus, then the remaining analysis/optimization phases,
lowering, register allocation, code generation, and GC/EH/unwind emission.
This is a dependency guide, not permission to bypass earlier required phases.

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
checkpoint naming the next dependency.

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
