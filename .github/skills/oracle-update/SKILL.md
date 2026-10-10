---
name: oracle-update
description: Reconcile a newer dotnet/runtime JIT and CoreCLR include revision against the RyuJitSharp managed port, update its pinned evidence, and commit validated batches.
---

# Updating the RyuJIT oracle

Use this skill for a periodic or requested update from the pinned
`dotnet/runtime` revision to a newer immutable commit. Reconcile the complete
change set under `src/coreclr/jit` and `src/coreclr/inc` into the managed port;
do not treat a successful build or a partial source comparison as completion.

## Read and establish the update

1. Read `AGENTS.md`, `docs/porting/README.md`, and `docs/porting/state.json`.
   Read only the relevant sections of `PLAN.md`, `DEVIATIONS.md`,
   `UPSTREAM-REPORTS.md`, and `MILESTONES.md`. Resume any concrete
   `checkpoint.nextAction` before starting a new batch.
2. Inspect the worktree and active work. Preserve all pre-existing staged,
   unstaged, and untracked changes. Do not overlap another oracle update or
   edit files assigned to active workers. For a large diff, split work into
   dependency-coherent, file-owned lanes; keep generated inputs/outputs and
   state/evidence coordination with one integrator. Pause workers before
   shared builds and tests.
3. Obtain the oracle checkout path from the current session setup; never infer
   it from another checkout or hard-code a machine-local path. Verify its
   remote is `https://github.com/dotnet/runtime`. Fetch the intended branch,
   then capture its full commit SHA once. Do not use a moving branch name in
   later comparisons.
4. Set the old revision to
   `state.json:upstream.reconciledCommit` and the new revision to the captured
   SHA. Verify both commits exist and the old commit is an ancestor of the new
   one. If there are no relevant changes, report a no-op and leave pins and
   source untouched.

## Preserve and inventory the immutable diff

Save the diff and inventories outside the repository, in a session-local
directory. Keep the exact old/new SHAs with every artifact. Use Git pathspecs
for both relevant trees and account for additions, modifications, deletions,
and renames:

```powershell
$diffDirectory = Join-Path $sessionArtifactDirectory "oracle-update-$oldCommit-$newCommit"
New-Item -ItemType Directory -Path $diffDirectory -Force | Out-Null
$diffBase = "runtime-diff-$oldCommit-$newCommit"
$nameStatusPath = Join-Path $diffDirectory "$diffBase-name-status.tsv"
$numStatPath = Join-Path $diffDirectory "$diffBase-numstat.tsv"
$statPath = Join-Path $diffDirectory "$diffBase-stat.txt"
$patchPath = Join-Path $diffDirectory "$diffBase.patch"

git -C $oraclePath diff --name-status --find-renames $oldCommit $newCommit -- src/coreclr/jit src/coreclr/inc |
    Set-Content -LiteralPath $nameStatusPath
git -C $oraclePath diff --numstat --find-renames $oldCommit $newCommit -- src/coreclr/jit src/coreclr/inc |
    Set-Content -LiteralPath $numStatPath
git -C $oraclePath diff --stat --find-renames $oldCommit $newCommit -- src/coreclr/jit src/coreclr/inc |
    Set-Content -LiteralPath $statPath
git -C $oraclePath diff --binary --find-renames $oldCommit $newCommit -- src/coreclr/jit src/coreclr/inc > $patchPath
```

Set `$sessionArtifactDirectory` to the session-local `files` directory provided
by the current session setup. Use native PowerShell redirection for the patch;
do not pass it through text-encoding commands. Retain the complete path
inventory and patch until reconciliation is accepted.
Review every changed path and every hunk, including declarations, target
conditionals, shared headers, and table inputs. Record a disposition for each
path in the temporary inventory; do not leave changes unexplained because a
file appears generated, unrelated, or platform-specific.

## Reconcile the managed port

- Read each old/new native change against the current managed implementation.
  Verify mappings with symbol searches; the conventional
  `src/coreclr/jit/<stem>.{h,cpp}` to `sources/Core/jit/<stem>/` mapping is only
  a starting point. Shared `Compiler` partials may map to other native files.
  Add a sparse `sourceMap` entry only when the mapping is non-obvious.
- Port complete affected functions and their dependency closure. Preserve
  native phase order, target predicates, diagnostics, error paths, numeric
  semantics, and JIT/EE contracts. Update every managed target implementation
  that corresponds to the changed native behavior.
- For deleted native definitions, remove the managed implementation only
  after checking all production references. Preserve any still-used
  target-specific helper by moving it to its target-owned source area. Remove
  tests for dead helpers or replace them with tests of the live behavior; do
  not retain unreferenced code just to keep old tests compiling.
- For new cross-target dependencies, keep callers and branches intact. Add
  compilable, tracked, terminating NYI stubs for unported other-target
  dependencies where required; do not use silent no-ops or success-shaped
  fallbacks.
- When table inputs change, update the input and generator together. Run the
  generator only in an isolated copy or directory: it deletes that working
  directory's `Outputs` subtree. Review the generated diff and copy only
  intended outputs; never repair generated files alone.
- Keep unrelated cleanup out of the update. Preserve upstream behavior unless
  an existing approved deviation applies. Record a suspected native defect in
  `UPSTREAM-REPORTS.md`; record an intentional observable difference in
  `DEVIATIONS.md`. Do not silently fix native behavior in C#.
- Add focused regression tests for behavior changes. Continue to use the
  intact pinned oracle for source and behavior comparisons.

## Validate and accept

Run focused tests for each coherent unit, then validate the integrated change
at a meaningful boundary. The repository's fast inner loop is:

```powershell
.\build.cmd -fast -test -solution tests\Core\RyuJitSharp.UnitTests.csproj
```

Before accepting a batch, also run the relevant full-analysis build and tests
without fast-mode analyzer/documentation overrides, following
`docs/porting/README.md`. Run target fixtures from
`tests\Targets\RyuJitSharp.Target.UnitTests.csproj` for affected non-x64 ABIs.
Prioritize phase dumps, then disassembly and regular runtime tests when
available. Builds, empty runs, native fallback, and `CORJIT_SKIPPED` are not
parity passes.

Review the complete diff, including generated output, and verify that every
inventory entry is either reconciled or explicitly out of scope with evidence.
Do not claim complete parity while `state.json:checkpoint.dynamicTlsInputBoundary`
remains unresolved; do not change its constants or erase its evidence as part
of an oracle refresh.

## Record and commit

Only after all relevant hunks are reconciled and validation passes:

- Update `state.json:upstream.targetCommit`, `targetObservedAt`, and
  `reconciledCommit` to the captured revision. Update
  `generatorInputMapping.reconciledCommit` after checking all changed mapped
  inputs and generated outputs. Change `targetInputCommit` only when the input
  set's own provenance is being re-pinned, not merely because the overall
  oracle revision advanced.
- Advance the checkpoint to the next concrete action, summarize the validated
  scope and remaining evidence limits, and preserve unresolved boundaries.
  Clear `checkpoint.activeBatch` when no work remains active.
  Update milestone or other maintained evidence only where the change requires
  it; keep large inventories, raw dumps, and machine-local paths outside the
  repository.
- Keep `PLAN.md` as durable roadmap and milestone guidance, not a per-refresh
  changelog. Do not append a dated provenance or validation paragraph for each
  oracle update. Update it only when the roadmap, validation workflow, or a
  durable milestone materially changes.
- Commit each completed dependency-coherent batch with its tests and related
  documentation. Use an imperative subject and include the repository's
  required co-author trailer. Do not amend commits, push, open a PR, or create
  issues without explicit authorization.

If reconciliation or validation is incomplete, leave the old reconciled pin in
place, retain the immutable artifacts, and set `checkpoint.nextAction` to the
specific unresolved hunk, test, or decision. Never advance the pin merely
because a newer upstream commit was fetched.
