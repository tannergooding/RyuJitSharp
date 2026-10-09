# Open port findings and follow-up work

This file tracks unresolved findings, decisions, blockers, coverage gaps, and
deferred work. Remove an item when its action is complete; do not retain
completion receipts or test-run summaries here. Keep validation evidence with
the tests, artifacts, and commits. Record intentional observable differences in
[DEVIATIONS.md](DEVIATIONS.md), not as completed backlog items.
IDs remain stable; gaps indicate resolved entries that were removed.

Fix translation defects that block correctness or parity in the relevant batch.
Record suspected upstream bugs and ask before introducing an intentional C#-only
behavior change. Defer unrelated renames, refactoring, and restructuring. Link
existing deviation entries rather than maintaining competing descriptions.

| ID | Category | Evidence and impact | Proposed action | Status |
| --- | --- | --- | --- | --- |

## Deferred

| ID | Deferred work | Reason and revisit condition |
| --- | --- | --- |
| B129 | Add a native fixed-arity bridge for forwarding formatted `JITLOG` text to the EE. | Deferred by owner as a non-core scenario. Native `ICorJitInfo::logMsg` requires a `va_list`, so a bridge needs a native variadic adapter and build/package support across the six host RIDs; managed code must not fabricate that ABI. Revisit if routing JIT diagnostics through the EE's configured logging destinations becomes a core requirement. |

Larger module decomposition and broad unit-test infrastructure belong to the
post-port phase. Add concrete candidates here as evidence appears, rather than
preemptively planning a redesign of every compiler subsystem.
