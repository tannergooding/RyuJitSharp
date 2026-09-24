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

## 2026-09-23: Mask evaluation and vector conversion

**Commit:** `a07c607`.

**Result:** Fixed-width mask unary/binary evaluation and mask/vector conversions
are complete. Corrected `simdmask_t.AllBitsSet`, which ignored its element count
and always set only 32 bits. Preserved native eight-bit minimum mask operations,
all-true normalization and non-normalized conversion results. Ten complete
Windows-x64 definitions retired.

**Evidence:** Five cases reproduce the storage defect before correction.
Twenty-eight new cases pass; the combined HWI/scalar selection passes 337 Debug /
333 Release, zero skipped, including bit ordering, inactive storage, aliased
operands and raw floating lane bits.

**Frontier:** HWI folding remains inactive. ARM64 predicate policies are
source-ported but unexercised; scalable evaluation remains pending. No new
NativeAOT/corpus capture or phase/codegen parity claim.

**Next:** Remaining HWI construction and constant-evaluation prerequisites.

## 2026-09-23: Fixed-width vector evaluation

**Commits:** `a7448a3`, `f500c0a`.

**Result:** Unary and binary vector constant evaluation now preserve native
lane arithmetic, comparison masks, shift/rotate boundaries and floating
bitwise data. Scalar operations retain xarch upper lanes; in-place updates
preserve inactive storage and support aliased inputs. Twenty-one complete
Windows-x64 native definitions retired across the two batches.

**Evidence:** 73 new cases; the latest combined HWI/scalar selection passes
309 Debug / 305 Release, zero skipped. Includes integer wrapping and narrow
division, signed and unsigned overshifts, NaN payloads, signed zero, all fixed
widths and explicit partial evaluation widths.

**Frontier:** The HWI dispatcher remains inactive. ARM64 scalar clearing and
other-target shift policies are source-ported but unexercised; scalable ARM64
unary evaluation remains explicitly NYI. The existing six exact import prefixes
remain the last corpus evidence; these support-only changes make no new
phase/codegen parity claim.

**Next:** Mask evaluation and remaining HWI construction prerequisites.

## 2026-09-23: HWI constants and operation classification

**Commits:** `a947c3c`, `88097ce`.

**Result:** Fixed-width vector creation constants preserve native lane bits,
scalar upper-lane policy and partial-output behavior. HWI operation mapping
retains all 146 target-guarded cases, scalar flags and effective NEG/NOT
patterns. Lane zero/one and bitwise queries are complete. Ten native
definitions retired across the two dependency batches.

**Evidence:** 62 new cases across the batches; the latest combined HWI/scalar
selection passes 236 Debug / 232 Release, zero skipped. Coverage includes
integer narrowing, floating signed zero/NaN bits, fixed-width lane ordering,
multiply type gates and effective-operation recognition.

**Frontier:** The HWI folding dispatcher is not activated. ARM64/Wasm mapping
branches and decomposed 32-bit long assembly remain unexercised. These support
changes do not advance the import corpus or establish codegen parity.

**Next:** Vector unary/binary evaluation and remaining HWI construction
prerequisites.

## 2026-09-23: Non-HWI expression folding

**Commit:** `1ff2dd7`.

**Result:** Binary folding now includes integer identities and masks, identical
comparisons, conditional selection and nullable-box comparisons. Effect/order
gates, fresh comparison IDs, morph completion and nested conditional flags follow
native behavior. Five complete functions and one visitor retired.

**Evidence:** 254 Debug / 248 Release selected cases passed, zero skipped;
45 cases fail against the previous dispatcher. Refreshed NativeAOT/native
captures contain exactly ten methods with six identical import prefixes.
The new `FoldInteger` prefix includes the mask-to-zero-extension fold;
the five previously matching prefixes remain exact.

**Frontier:** Four inline-candidate diagnostic/processing differences remain.
Conditional and nullable-box paths have focused IR coverage, not corpus or
codegen parity. Hardware-intrinsic folding and full inline/global-morph
activation remain pending.

**Next:** Complete the HWI folding dependency closure, then reassess inline
activation against its remaining prerequisites.

## 2026-09-23: VN-backed constant and overflow folding

**Commit:** `741ad12`.

**Result:** Tree constants now receive value numbers and register embedded
class-handle/field-address metadata. Constant replacement refreshes both VNs;
overflow folding supplies the native helper exception set and dummy zero.
Eleven complete Windows-x64 native definitions retired.

**Evidence:** 197 Debug / 191 Release selected cases passed, zero skipped.
Coverage includes stale-pair replacement, unknown compile-time handles,
field registration, reference/byref zero, vectors/masks and overflow exceptions.

**Frontier:** The global-morph restriction on overflow replacement remains.
ARM64 scalable/mask storage and general VN-phase activation remain unported;
the reachable import corpus is unchanged.

**Next:** Complete one-constant integer/comparison folding, including its
nullable-box and conditional-tree dependencies.

## 2026-09-23: Node assertions and morph completion

**Commit:** `761762d`.

**Result:** Complete `optAssertionGen`, `fgAssertionGen` and `fgMorphTreeDone`.
Local/global fact selection, conditional edge sets, implied boolean ranges and
physical-definition kill-before-gen ordering retain the native behavior.
Four complete native definitions retired.

**Evidence:** 152 Debug / 149 Release selected cases passed, zero skipped.
Coverage includes conditional suppression, both successor sets, stale-fact
invalidation, morph gates, conservative VNs, array helpers and tail-call checks.

**Frontier:** These complete support routines do not activate block/global
morph. General local assertion propagation and the remaining folding dispatcher
are still pending.

**Next:** Resume the scalar/integer/comparison folding closure and its
morph-completion callers toward inline activation.

## 2026-09-23: Unary VN expressions

**Commit:** `75e9074`.

**Result:** Complete unary function interning/folding, known array lengths,
canonical field-sequence identity and ordered exception-value composition.
Field sequences use managed identity tokens rather than movable addresses;
their diagnostics retain native symbolic formatting. Eighteen complete native
definitions retired.

**Evidence:** 136 Debug / 133 Release selected cases passed, zero skipped.
Coverage includes wraparound, signed zero, NaN payloads, EE calls and caching,
allocation length bounds, identity after compacting GC and exception unions.

**Frontier:** No new compiler phase is activated. Multi-argument VN evaluation
and the general VN dumper remain incomplete.

**Next:** Complete node-wide assertion generation and morph completion.

## 2026-09-23: Fixed-width VN constants

**Commit:** `c068366`.

**Result:** Fixed-vector and mask interning/retrieval, bounded generic constant
import, zero-by-type, and widening-cast normalization. All five vector widths
preserve owned bit-exact payloads; SIMD12 retrieval zeroes inactive bytes.
Eighteen complete Windows-x64 native definitions retired.

**Evidence:** 108 Debug / 106 Release selected cases passed, zero skipped.
Coverage includes vector ownership and padding, signed/unsigned imports,
signed zero, handles, mask bits and unsigned widening restrictions.

**Frontier:** General unary VN expressions and node-wide assertion generation
remain pending. ARM64 scalable/mask storage is still explicitly NYI. The
unrelated existing mask `AllBitsSet` defect is recorded as B096, not changed.

**Next:** Complete unary VN expressions with native folding, known array
lengths, exception-value composition and field-sequence identity.

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
