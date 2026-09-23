# Porting milestones

Completed milestones are recorded newest first. Each entry identifies the
commits, useful capability gained, validation, remaining execution frontier,
and next dependency boundary. Detailed evidence and local recovery snapshots
remain in [state.json](state.json); deviations and unresolved defects remain in
[DEVIATIONS.md](DEVIATIONS.md) and [BACKLOG.md](BACKLOG.md).

Logical commits are intermediate checkpoints. At a validated milestone, update
this journal, commit the checkpoint, push the current porting branch, and
continue. A milestone does not require a conversational stop. Pause for an
explicit user request, an unresolved decision requiring approval, a publication
conflict, or a blocker that cannot be safely resolved.

## 2026-09-23: General assertion creation and branch facts

**Commit:** `f56d1b4`.

**Result:** Complete local/global assertion creation and JTRUE generation,
including scalar/vector constants, copy normalization, exact/subtype facts and
edge polarity. CSE comma-wrapped stores preserve the underlying value query.
Corrected unsigned null-check offset comparison so negative offsets cannot
establish invalid non-null facts. Four complete native definitions retired.

**Evidence:** 101 Debug / 99 Release selected cases passed, zero skipped.
Coverage includes small-local stores versus comparisons, copy gates,
integral-only SIMD equality, exact-type edges and negative-offset rejection.

**Frontier:** Node-wide assertion generation and morph completion are still
unported. No new phase or code-generation parity is claimed.

**Next:** VN zero/vector constants and complete unary-expression support needed
by `optAssertionGen`, followed by the morph completion path.

## 2026-09-23: Scalar folding through VN-backed assertion support

**Commits:** `d6aae98` through `302c229` (eight implementation commits).

**Result:** Scalar and floating constant-operand folding, integral ranges,
assertion descriptors/tables and local invalidation, scalar VN storage,
local/global assertion insertion, cyclic-phi non-negativity, and conditional
bounds-assertion generation. The final three commits retired 62 complete native
definitions. Default SSA-array allocation and full bit-vector construction
defects were corrected as required by these paths.

**Evidence:** Debug/Release validation and native retirement evidence are
recorded per batch in `state.json`. The latest bounds-generation selection
passed 93 Debug / 91 Release cases with no skips. The last corpus capture
retains five exactly matching import prefixes; support-only batches did not
recapture an unchanged execution frontier.

**Frontier:** This is not full import-phase or code-generation parity.
General assertion generation, morph completion and optional inline-phase
activation still require their remaining dependencies. ARM64 scalable-vector
storage remains explicitly unsupported.

**Next:** Complete the remaining assertion creation/generation prerequisites,
then `fgAssertionGen` and `fgMorphTreeDone`, preserving native semantics rather
than substituting local-only generation or `SetMorphed`.
