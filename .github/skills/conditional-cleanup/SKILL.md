---
name: conditional-cleanup
description: Complete a RyuJitSharp C# preprocessor-cleanup batch using a nested-aware inventory, verified Release dependencies, and combined source/test acceptance. Use for DEBUG, target, or feature guard cleanup.
---

# Conditional cleanup

Follow `AGENTS.md`, `.github\copilot-instructions.md`, and
`docs\porting\README.md`. Read the current `checkpoint` and relevant pinned
revisions in `docs\porting\state.json`; reuse an active packet instead of
restarting discovery. This skill does not authorize new ABI/layout policies,
observable deviations, or cleanup outside the assigned scope.

## Establish the packet

- Select a complete file group or proven pattern family. For a repository-wide
  request, account for both `sources` and `tests`, including the Target project
  and related generator inputs/templates. Do not silently narrow the request
  to one diagnostic call or label every unexamined family out of scope.
- Preserve the existing worktree and index, including intentionally new files.
  Record the starting revision and source snapshot separately from the planned
  edits. Keep inventories, source snapshots, and logs under `artifacts`.
- Record scope, completion condition, validation, and concrete deferrals in
  `checkpoint.activeBatch`. Define which Debug/Release and host/target
  configurations cover the changed surface before implementation.
- Work directly unless delegation is useful and authorized. Give workers
  disjoint file-owned packets. Keep shared helpers, generated dependencies,
  checkpoint updates, and shared build outputs with one integrator.

## Inventory once

Create one inventory for the selected source snapshot. Reuse a saved inventory
only after checking its scope and source identity. Use C# syntax-aware
directive traversal when available; otherwise track the full
`#if`/`#elif`/`#else`/`#endif` stack lexically, excluding strings and comments.
Record nested guards individually, their parent branches, and complete
alternative-branch conditions. Match the `DEBUG` identifier, not just lines
beginning `#if DEBUG`; include relevant `!DEBUG` and compound expressions.

Each candidate needs:

- A stable ID, file/symbol, original directive span, source hash, condition,
  parent branch context, and pattern family.
- The full branch text or a reference into the preserved source snapshot.
  Truncated display excerpts are not classification inputs.
- A disposition, concrete dependency/reason, and supporting source or
  validation evidence.

Use these dispositions:

| Disposition | Required evidence |
| --- | --- |
| Pending | Classification or implementation is unfinished. |
| Converted | The final patch transforms this exact candidate and its dependent surfaces. |
| Retained | A named declaration, state, layout, or behavior contract requires the guard. |
| Deferred | A specific prerequisite or authorized scope boundary prevents completion; record the next action. |

Do not infer conversion because a file contains a helper call, or retention
because an identifier looks Debug-specific. Do not bulk-classify unexamined
blocks as retained. An inventory with pending candidates does not close its
packet; directive totals are not an eligibility or parity measurement.

## Prove the dependency closure

For each family, inspect complete functions and referenced declarations:

- `[Conditional("DEBUG")]` omits Release calls and argument evaluation, but
  every referenced type, member, local, and overload must still bind in Release.
  A runtime `if (Configuration.DEBUG)` does not fix missing declarations.
- Check the enclosing type and backing fields, not just the final property or
  callee. A Release-visible compiler option does not make a Debug-only
  `_compiler` field available. Preserve nullable-flow contracts established
  by conditional assertions.
- Preserve Debug formatting, gating, evaluation order, assertion behavior,
  and side effects. Use `Configuration` for compiler dispatch and
  `TestConfiguration.IsDebug` for runtime-gated test assertions.
- Reuse `TestConfiguration` for Debug-only test setup. Preserve the exact EE
  callback pointer and its lifetime, Debug TLS nesting, and Release thread-static
  compiler save/restore. A no-op helper still evaluates its arguments.
- Verify helper linkage and namespaces in every consuming project. The Target
  project explicitly links Core fixtures; a Core-only capture dependency is
  not automatically available there. Include required partials or isolate the
  Core-only dependency without duplicating helper semantics.
- Treat generated declarations and native-facing layout/enum changes as
  dependency closures. Change generator inputs/templates with their output;
  preserve native numeric identities and target aliases.

For a genuinely new uncertain family, one early Release compilation probe can
establish an API dependency. Record the result once and continue the eligible
remainder. Do not repeatedly remove and restore the same rejected candidate.
Do not add allocations, backing state, unsafe casts, or success-shaped shims
solely to reduce directive counts.

## Convert the coherent family

Apply the proven transformation across the packet before combined acceptance.
Preserve original encodings, line endings, and surrounding layout. Review
scripted changes for indentation, branch boundaries, and accidentally changed
assertion scope; matching text is not proof that a rewrite is safe.

Logical intermediate commits do not create new validation boundaries. Expand
checks early only for a concrete failure or material risk. After edits, verify
the original inventory against the changed locations, updating mappings/hashes
as needed; do not generate another whole-repository inventory to rediscover
known candidates.

## Validate and close

Use normal developer commands from the porting contract, not a new qualification
runner. Combine affected fixtures into one selection per configuration.

```powershell
.\build.cmd -fast -test -solution tests\Core\RyuJitSharp.UnitTests.csproj
dotnet test tests\Core\RyuJitSharp.UnitTests.csproj -c Debug --filter "FullyQualifiedName~RegisterTrackingTests"
dotnet test tests\Targets\RyuJitSharp.Target.UnitTests.csproj -c Debug -r win-x64 -p:TargetRuntimeIdentifier=win-arm64 --filter "FullyQualifiedName~Arm64EmitterInstructionRemovalTests"
```

Replace the example filters/RIDs with the packet's actual affected selections;
repeat them in Release. Fast iterations may disable analyzers/documentation,
but acceptance requires the relevant full-analysis checks. Use
`parity-validation` when diagnostics, generated output, or native contracts
require comparison at the available target frontier.

- Verify nonempty executed selections and that every changed fixture is covered.
  Do not remove fixtures, change target semantics, or add exclusions to create
  a passing result. Keep filtered results distinct from full-suite evidence.
- Compare failures with the same baseline/configuration, including downstream
  dependencies. "No errors in changed files" is not sufficient. Fix regressions
  caused by this packet; report unrelated blockers without expanding scope.
- Retain command exit status, logs, TRX results, and final source/configuration
  identity. Reuse valid evidence and rerun only invalidated checks.
- Required checks that remain blocked mean incomplete acceptance, not a pass.
  Preserve the WIP and set the cursor to the failing command, symbol, and
  unresolved dependency. Do not weaken checks or advance the completion claim.

Close only when every selected candidate has an evidence-backed disposition,
no eligible candidate remains pending, the full diff is reviewed, and required
acceptance is satisfied. Commit only the accepted scoped changes with related
tests/documentation. Remove resolved checkpoint narratives; retain the concrete
next action, unresolved boundaries, and evidence links. Keep the detailed
inventory in `artifacts`. Do not push or open a PR without authorization.
