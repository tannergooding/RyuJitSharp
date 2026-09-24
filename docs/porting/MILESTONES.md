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

## 2026-09-23: Active inlining and nested compilation

**Commits:** `b5d8fe4`, `d3d1ca9`, `6839eb3`.

**Result:** The inline phase now expands candidates, substitutes return
placeholders, repairs failed calls, and reports decisions deterministically.
Every attempt starts with a newly constructed compiler, matching native
placement-new rather than retaining a previous inlinee's metadata and SIMD
state. Successful inline IR returns `CORJIT_OK`; root methods still return
`CORJIT_SKIPPED` until codegen exists. Complete boxing patterns and nullable
field helpers replace a false-success stub exposed by actual nested imports.

**Evidence:** The related selection passes 336 Debug / 281 Release, zero skipped.
All eleven corpus methods complete through the native host, including one
successful scalar inline and 43 nested vector-fallback inlines. Ten import
prefixes and ten post-inline IR sections match native exactly. Nine complete
inline-phase sections match, without normalization or new exclusions.

**Frontier:** Hardware-intrinsic import remains incomplete, so `FoldHardware`
imports managed fallback bodies instead of native intrinsic IR. Scalar
post-inline IR is exact, but native inlinee profile checks appear to read an
uninitialized diagnostic flag (B135); two native captures reproduce the
difference. No managed flag poisoning or diagnostic suppression was added.
Full EH checking, EE text logging, global morph and codegen remain incomplete;
native runtime fallback is not managed codegen success.

**Next:** Correct the recorded flag-filtered search-pruning defect (B125), then
identify the next complete global-morph dependency from the pinned oracle.

## 2026-09-23: Post-import cleanup and OSR entry repair

**Commit:** `14891be`.

**Result:** Complete post-import cleanup: inline return-spill refinement,
unimported-block removal with predecessor repair, EH deletion and extent
trimming, and normalized OSR entry flow through nested or mutually protecting
tries. Removed blocks retain the links used by trimming. Zero-weight profiles
preserve native `std::min` behavior rather than propagating a NaN likelihood.

**Evidence:** Twenty-nine new cases; the related selection passes 132 Debug /
132 Release, zero skipped. NativeAOT publication and the eleven-method corpus
succeed. All eleven outer-method no-change post-import sections match native
exactly, and the six exact import prefixes remain unchanged.

**Frontier:** The corpus does not exercise cleanup's EH/OSR transformations;
those are covered by constructed IR, not executed managed-JIT code. The existing
`fgVerifyHandlerTab` diagnostic stub remains explicit (B127). Codegen parity
and actual OSR execution remain unavailable.

**Next:** The named B112 prerequisite phases are implemented. Complete
`fgInline` and validate successful nested compilation before treating the
inliner as active.

## 2026-09-23: Indirect-call transformation

**Commits:** `d76a831`, `36016c0`, `e5e5b74`.

**Result:** Complete fat-pointer expansion and guarded devirtualization, including
class, method and delegate guards; multiple and exact guesses; return-placeholder
repair; and chained hot paths with cold-path bypass and profile repair. Shared
spilling preserves evaluation order and owning uses. Candidate cloning and
absolute/relative vtable targets are available, and the full indirect-call phase
is active. The native `indirectcalltransformer.cpp` is retired.

**Evidence:** Sixty new cases across the three commits; the combined related
selection passes 138 Debug / 138 Release, zero skipped. NativeAOT publication
and the eleven-method corpus succeed, retaining six exact import prefixes.
The outer-method no-candidate phase sections match native exactly for all eleven
methods; native `InlineCaller` also compiles an inlinee, which is not counted as
another outer-method comparison.

**Frontier:** The corpus has no fat-pointer/GDV candidates, so expansion is
established by constructed IR and controlled EE cases, not native execution
parity. Native profile and metric inconsistencies remain deliberately unchanged
(B121/B124). The required inferred-local normalization defect is fixed (B120);
an unrelated node-search pruning defect is recorded for later work (B125).
Post-import cleanup still gates inliner activation, and codegen remains unavailable.

**Next:** Complete post-import cleanup, including EH and OSR handling, before
activating the inliner.

## 2026-09-23: Patchpoint transformation

**Commit:** `2b0f3d2`.

**Result:** Complete regular loop and forced partial-compilation patchpoint
expansion. Regular patchpoints share one frame-local counter, initialized once
at entry, with native decrement/test/helper control flow and 99/1 probabilities.
Forced patchpoints replace the block's statements without allocating a counter.
The complete native `patchpoint.cpp` is retired.

**Evidence:** Seven new IR/CFG cases; the patchpoint/block-splitting selection
passes 50 Debug / 50 Release, zero skipped. NativeAOT publication and the
eleven-method corpus succeed, retaining the six exact import prefixes.

**Frontier:** The corpus is not Tier0 and does not execute patchpoints.
Constructed IR cases cover the transformations; patchpoint metadata/codegen and
actual OSR execution remain unimplemented. Indirect-call transformation and
post-import cleanup still gate inliner activation.

**Next:** Complete indirect-call transformation, then post-import cleanup.

## 2026-09-23: Qmark expansion

**Commit:** `2a53751`.

**Result:** Complete early and late top-level qmark expansion, including nested
arms, local/field writeback, comma splitting, throwing arms, EH extension and
profile propagation. Complete throwing-block conversion with ordered callfinally
unpairing. Seven native definitions retired.

**Evidence:** Seventeen new CFG cases; the qmark/block-splitting selection passes
60 Debug / 60 Release, zero skipped. NativeAOT publication and the eleven-method
corpus succeed; all early-expansion invocations make no changes in this corpus,
and the six exact import prefixes remain unchanged.

**Frontier:** Constructed IR cases establish expansion behavior; the corpus does
not yet establish native parity for actual qmark transformations. Native
true-only likelihood behavior is preserved and recorded for later investigation
(B117). Patchpoint/indirect-call transformations and post-import cleanup remain
the prerequisites to inliner activation.

**Next:** Port patchpoint transformation, then indirect-call transformation and
post-import cleanup.

## 2026-09-23: Morph initialization

**Commits:** `99515ae`, `6d69f5e`, `b8b331a`.

**Result:** Complete root class-initialization construction and the morph
initializer: E&C frame requirements, constructor insertion, Debug GC argument
checks and stack-check locals. Add the outgoing-argument hash-vector storage
with native bucket/node/bit ordering and resize behavior. Retire 29 native
definitions across the constructor, phase and storage batches.

**Evidence:** The focused selection passes 47 Debug / 44 Release cases, zero
skipped, covering runtime-context/helper selection, entry order, frame/local
requirements, sparse collisions, growth/shrink and temporary consumption.
NativeAOT publication succeeds. Both corpus captures finish morph initialization
for all eleven methods without entry modifications; the six exact import
prefixes remain unchanged.

**Frontier:** Constructor and Debug-check insertion are established by controlled
EE cases, not native execution parity. Early qmark expansion, patchpoint and
indirect-call transforms, and post-import cleanup still gate successful inline
compilation. Inlining remains disabled. Hash-vector bulk/set algebra and iterator
APIs remain native; suspected upstream arithmetic defects are recorded in B115.
The vector-import gap and lack of codegen parity remain unchanged.

**Next:** Complete early qmark expansion, then the remaining early-transform and
post-import prerequisites before activating the inliner.

## 2026-09-23: Windows-x64 HWI expression folding

**Commits:** `2f9f9d8`, `f7676d0`.

**Result:** Complete and wire the xarch HWI folding dispatcher, including
comparison-mode normalization, conversion cancellation, mask rewrites,
constant evaluation and one-constant identities. Preserve scalar upper lanes,
signed-zero/NaN restrictions, effect ordering, node identity and morph/VN
updates. Correct mask-zero construction and Debug destruction's premature
flag poisoning. Three native definitions and the comparison enum retired.

**Evidence:** 65 new Debug / 61 Release dispatcher cases; the combined
HWI/scalar selection passes 462 Debug / 454 Release, zero skipped.
Forty-one cases fail against the previous dispatcher. Six conversion cases
exposed the Debug poisoning defect, and a false-mask comparison exposed the
missing zero factory case. NativeAOT publication and an eleven-method corpus
capture retain all six previously exact import prefixes.

**Frontier:** `FoldHardware` records the existing vector-import boundary:
`impHWIntrinsic` still leaves the Vector128 calls rather than importing HWI
nodes. It therefore does not establish managed vector folding execution parity.
The four inline-related dump differences remain. Non-xarch dispatchers, full
inlining/global morph and codegen parity remain pending.

**Next:** Audit `fgInline` activation against its remaining phase closure now
that Windows-x64 expression folding is complete.

## 2026-09-23: HWI node and mask-analysis prerequisites

**Commits:** `716025f`, `7f3d065`, `dd3e8b9`.

**Result:** Vector-to-mask construction and constant conversion folding,
conversion recognition, sequential sign-bit extraction, per-element mask
analysis and same-class HWI reconfiguration are complete. Existing local facts
and operation metadata are reused. Seventeen complete Windows-x64 native
definitions retired.

**Evidence:** 34 new cases; the latest combined HWI/scalar selection passes
371 Debug / 367 Release, zero skipped. Covers fresh conversion results,
unchanged inputs, mask-width compatibility, recursive expressions, operand
growth/shrinkage and caller-owned side-effect flags.

**Frontier:** These helpers do not activate the full HWI folding dispatcher.
Cross-kind changes still require whole-node replacement. ARM64 fixed-width
branches are source-ported only; scalable conversion folding is explicitly NYI.
No new NativeAOT/corpus or codegen parity claim.

**Next:** Full HWI folding, including remaining metadata dependencies and
reachable-behavior validation before dispatcher activation.

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

## Earlier port milestones

These entries backfill the port's history before the September continuation,
using repository commits and their author dates. They summarize the capability
and limitations recorded at those points, not newly executed validation of the
historical revisions. They do not imply that every supporting function or phase
was complete.

| Date | Commits | Milestone and boundary |
| --- | --- | --- |
| 2026-06-19 | `16baaa7` | The inherited baseline: cleanup to let the Hello World path complete without failure, including class-layout and segment-list support. This was not managed code generation; the port still relied on native fallback. Uncommitted continuation work was preserved separately at the start of the September work. |
| 2026-06-17–18 | `9a77e8b`, `82560cd` | Import-completion fixes, followed by `fgFindOperOrder`. The import-completion commit explicitly retained the `impHWIntrinsic` null-return boundary. |
| 2026-06-15–17 | `53c98a1`, `cf8a1e3`, `19e8466`, `986e355` | Added the static table-generation tool, expanded generation of already ported table-driven types, and introduced basic inline-policy support. Inline policy was support code, not an active inline-expansion phase. |
| 2026-06-14–15 | `bf72466`, `1d903b2` | Filled newly required support functions, restored the Release build, and corrected dump inconsistencies while progressing through importation. |
| 2026-05-12–06-07 | `d07a5b2`, `b25af34`, `8eb9726`, `6fcc75b`, `dde20d1` | Advanced `fgImport` through the block-code, call and intrinsic boundaries, reaching the hardware-intrinsic boundary. Upstream synchronization ended at runtime commit `65b74673`, the source baseline inherited by the September continuation. |
| 2026-05-09 | `9420d96`, `85d225a`, `fb8cc74` | Added basic-block construction, initialized the local-variable table, and ported first-block canonicalization. |
| 2026-05-02–04 | `00c589d`, `ff7f8f2`, `38b9178`, `97f18d4` | Extended compiler initialization toward `compCompile` and installed the phase-dispatch framework. The phase-addition commit explicitly made the phases no-ops; their presence did not establish implemented transformations. |
| 2026-04-18–26 | `dcb6da3`, `0157c66` | Retargeted to .NET 10 and refreshed the project to resume porting. |
| 2024-02-18 | `a40a297`, `67d2c9b`, `21b7101`, `4e66abc` | Established the project, core include/interface ports, and the minimal native-facing machinery needed to load and no-op as an AltJIT. This was the interop/loading foundation, not a compiler producing native code. |
