# Deviations and limitations

This is a seed register, not an exhaustive audit of the existing port.
An existing implementation is evidence of a difference, not automatic approval
of every consequence. Record new differences here as they are encountered.

Each entry needs a stable ID, status, native/C# locations, rationale, observable
effect, and validation or remaining action. Keep unfinished implementation out
of the accepted-output exception list.

## Accepted policy

### D001: Managed allocation instead of the native arena

**Status:** accepted representation change; allocation-statistics differences
are accepted. No other output difference is implied.

Native arena-allocated compiler objects can use managed allocation and lifetimes.
For example, native `src/coreclr/jit/gentree.h` corresponds to the managed class
in `sources/Core/jit/gentree/GenTree.cs`. Native allocation counts/bytes are not
expected to match. Compiler decisions, traversal order, logical node IDs,
generated code, and diagnostics other than those statistics still must match.

Custom layouts with no GC pointers do not allocate unused per-slot GC arrays,
including layouts whose unsigned byte sizes exceed `Int32.MaxValue`. Native
allocates zero-filled storage for large no-GC layouts; managed queries use the
same zero-GC-count early return without materializing that storage.

Each inline attempt constructs a fresh managed `Compiler` and updates the
inliner's `InlineeCompiler` reference. Native reuses storage but invokes its
constructor again with placement-new; retaining the managed instance instead
retained the previous method's metadata and SIMD state (B132). Allocation reuse
is not part of the semantic contract. The native inlinee's apparently
uninitialized profile-diagnostic flag is tracked separately in B135; managed
fields are not poisoned to reproduce it, and full diagnostic parity is not
claimed.

**Remaining action:** audit identity/lifetime assumptions as functions are
ported. If a comparator excludes allocation statistics, identify the exact
fields/lines and test that neighboring semantic metrics still compare.

### D002: Idiomatic C# representation

**Status:** accepted when behavior is preserved; not an output exception.

Partial classes, properties, managed references/collections, spans, and existing
C# helpers may replace native structure where appropriate. Examples include
`Compiler` partials and `GenTree` properties. Preserve equality/identity,
collection ordering, integer semantics, ownership, and native interop contracts.
Larger algorithmic or architectural changes require separate approval.

`ILLocation` stores the bitwise complement of its IL offset so that
zero-initialized C# values, including locations embedded in `default(DebugInfo)`,
represent the native invalid offset. Explicit offsets and source flags retain
their original values through the public API. This avoids adding a validity
field or incorrectly assigning compiler-generated statements to IL offset zero.

Whole-node replacement instead of native cross-kind node bashing is an explicitly
approved safety and idiomatic-C# deviation. Keep the managed type hierarchy;
adapt callers to accept replacement nodes and update all owning uses, cached
references and, where applicable, LIR links. Native physical address identity
is not itself a requirement. Preserve the operation's semantic relationships,
evaluation order, metadata and dump-visible logical IDs without introducing
spurious ID-allocation changes. Existing unthreaded inline-argument replacement
is prior art, not proof that other replacement contexts already work. Do not
reinterpret incompatible CLR layouts or introduce a mutable-payload redesign
merely to emulate native bashing (B064).

Local addresses and local-field loads/stores all use `GenTreeLclFld`, so
indirection finalization can retag that compatible object in place while
preserving its logical ID. The caller consumes the returned local in place of
the indirection. Promoted struct returns instead replace their owning use with
a field-list node; they do not retag the original local into a different CLR type.

Comparison morphing installs replacement constants in their owning operands,
preserving logical node IDs without changing existing aliases into other node
kinds. Range-proven constant results allocate fresh nodes, matching native.
Relational operator swaps preserve value numbers; comparison reversal retains
the native unordered-floating flag behavior.

Arithmetic binary-to-unary rewrites use replacement constructors that preserve
logical IDs, common flags and value numbers. Returned local-field reads become
whole-local replacements with native `SetOper` value-number clearing. Comma-throw
propagation installs its retyped zero in the comma's operand slot. These remain
tree-form contracts, not general LIR replacement support.

Repeated-addition reduction replaces its left subtree with a constant in the
new multiplication's operand slot. The constant retains the left subtree's
logical ID and native node-mask flags, clears VNs, and does not mutate aliases
of the original subtree into a different managed kind.

TLS field-address expansion returns an identity-preserving binary ADD instead
of retagging the unary `GenTreeFieldAddr`. It retains native common flags and
clears VNs, without prematurely inheriting the newly constructed children's
effects. The field driver consumes the returned owner; old aliases remain
field nodes. Module-index arithmetic explicitly reinterprets the EE's managed
`int` as native `unsigned` before multiplication and pointer-width widening.

Zero-object assertion propagation takes an owning operand by reference and installs
the replacement zero constant instead of changing the local node's managed kind.
Ordinary returns update their unary operand; Swift error returns update the value
operand without changing the error operand. Global assertion updates use the
managed owning edge or statement root and preserve the forward traversal link;
the native later remorph remains responsible for rebuilding all statement links.

Scalar constant assertion replacements preserve logical identity and explicitly
accept all-trees threading when a statement owns the use. Constructors do not
copy links; assertion update installs the replacement and its forward link.
Fresh handle and vector constants still allocate new identities, as native does.
Local propagation clears VNs; global propagation assigns the assertion's constant
VN to both kinds. Signed floating zero is not propagated through equality facts.
ARM64 scalable-vector constant application explicitly throws until its storage
representation is ported, consistent with assertion creation.

Constant field-sequence annotations are mutable metadata, allowing negation
motion to clear an annotation without replacing the constant. `SetValueTruncating`
accepts a `long` instead of a native signed-integer template; its int-width
truncation and long-width preservation are unchanged.

Integer narrowing takes its owning use by reference so that a 32-bit target can
replace a long constant with an integer constant through the existing
identity-preserving replacement helper. Its probe pass does not mutate the IR.
Cast target types remain mutable metadata within `GenTreeCast`, allowing native
cast cancellation without changing the node's CLR type.

Scalar constant folding now uses replacement constructors that copy base
metadata and the logical tree ID without allocating another ID. Operation-specific
flags are cleared using the native node mask. The source must be unthreaded;
existing importer and placeholder-walker uses consume the returned replacement.
This does not implement LIR relinking or repair arbitrary cached aliases.
Overflow folding retains the native global-morph gate. Value-numbered constant
replacement now refreshes both VNs, and overflow helpers carry the native
exception-set composition (B082/B083/B100).

Block initialization uses the same replacement contract for primitive stores and
`GenTree.BashToZeroConst`. The latter returns a replacement instead of mutating
the node's CLR kind; both the source value and owning store are replaced, with
native VN clearing and logical IDs preserved. These replacements are unthreaded.
The block helper keeps descriptor indices and reacquires descriptors instead of
retaining managed references across local-table growth.

Debug morph-stress replacement uses a same-CLR-type shallow copy instead of
allocating an unrelated native node and overwriting its storage. It preserves
the source logical ID and metadata, consumes the native destination-allocation
ID, and resets the sequence number. Array-index storage and hardware-intrinsic
operand slots that native stores inline are copied; external operand arrays
remain shared. It requires unthreaded source nodes and does not emulate poisoning
the discarded native node. The recursive morph dispatcher is not yet active.

Allocation-to-helper conversion uses a `GenTreeCall` replacement constructor
and the source-node overload of `gtNewHelperCallNode`. Unlike constant folding,
native `ChangeOper` preserves the common flag mask. The replacement preserves
that mask, value numbers and logical identity, then recomputes helper/argument
effects without the fresh-call factory's extra global-reference flag. The
owning local store installs the returned node. General argument morphing remains
outside this construction helper; no unported argument morphing is implied.

B141 currently activates only the stack-allocation-disabled path, including
minopts. Requests for the unported stack-allocation analysis/cloning path end
compilation with `CORJIT_IMPLLIMITATION`; they do not silently allocate on the
heap. This is a staged implementation limitation, not completion of the native
phase or an accepted dump/codegen difference. The complete native phase and
combined allocation traversal remain in the residual ledger.

Local-address values retain their owning statement/operand slot rather than a
native `GenTree**`. Unary and binary operands use direct slot identifiers;
special-node operands use an ordinal in the owner's stable operand list. This
keeps managed references GC-safe and distinguishes two slots containing the
same node. The owner must not restructure its operand list before consuming
its child values. Local-morph replacement constructors explicitly accept
locals-only threading; the existing unthreaded constructor contract is unchanged.
`LocalSequencer.ReplaceNode` transfers transient links and the append cursor,
including root-sentinel self-links, before the visitor installs the replacement
in its owning slot. Replacements preserve logical IDs, clear VNs and apply the
native `SetOper`/`ChangeOper` flag policy. The complete local-morph traversal and
phase now use this contract; it does not support LIR replacement. Deferred
exposure cleanup replaces an unread store's data and immediately rebuilds the
whole statement's locals list instead of transferring transient links.

`GenTree.EffectiveUse` returns a managed reference to the actual owning slot,
including through comma expressions. The post-morph implicit-byref query uses
`ref` outputs so non-load rejection preserves both outputs, while rejected loads
still publish their complete address and peeled offset as native does.
Source-aware indirection construction preserves logical identity and clears
value numbers; callers restore operator-specific flags when native uses `SetOper`.
Store-indirection replacements use `GenTreeStoreInd`, not its indirection base.
Value retyping can explicitly retain simple or composite SSA identity when the
local number is unchanged; local-address conversion still discards that identity.

Local-address assertions use a value-record key, a lookup-only dictionary and an
insertion-ordered assertion list; dictionary enumeration is not observable.
Loop-definition maps do expose their iteration order in diagnostics, so their
managed specialization retains native bucket sizes, growth-before-overwrite,
chain insertion and rehash order from `jithashtable.{h,cpp}`. It stores only keys
because every native value in this particular map is `true`; this is not a
general replacement for the native hash-table API.

Identical-operand comparisons allocate fresh constants, as native does, rather
than using the scalar-bashing replacement constructors. Comparisons and SELECTs
copy the original sequencing links only outside global morph; callers still own
installing the returned node. QMARK flag cleanup uses the standard managed
preorder visitor and skips nested COLON subtrees. The nullable `hasValue`
zero-offset invariant is checked in the folding fixture instead of a C++
`static_assert` (B101).

The existing `CheckedOps.Try*` arithmetic APIs return success (the inverse of
native `CheckedOps::*Overflows`) and expose the wrapped result through an out
parameter. Addition/subtraction use sign-bit checks or unsigned ordering;
signed multiplication checks that the full product is the sign extension of its low
half. These implement the native `ClrSafeInt` overflow decisions without using
exceptions for expected overflow. Callers must invert success when asking
whether an operation overflows (B065).

`FitsIn(var_types, T)` intersects the destination range with the supported source
integer type using generic-math saturated bounds, avoiding signed/unsigned
comparison promotion. The cast-overflow predicates retain native overflow
polarity and floating-point bounds; casts to floating-point destinations never
report overflow, including narrowing to infinity (B066).

`IntegralRange` is a readonly value type with ordered enum bounds and managed
value equality. Its native signed-domain interpretation is unchanged, including
unsigned cast inputs whose bit patterns appear negative before widening.
Tree-based non-negativity inference and the conservative-VN fallback are active.
The VN predicate follows native phi traversal and intrinsic rules (B086/B091);
this does not activate the broader value-numbering phase.

Assertion descriptors use immutable managed objects with value-type operands;
reversal creates a new descriptor. Vector constants own a copied byte array
containing only the active payload, instead of native inline/arena storage.
Equality preserves signed-zero and NaN bit patterns, handle flags, and native
field-sequence exclusion. Dependency vectors and complementary indices use
managed collections without changing index or traversal order (B087).
Insertion and complementary creation now cover both local and global assertions,
including underlying `VN + constant` dependencies. The non-negativity-dependent
factories are also implemented; generation remains unported (B090/B091). ARM64 scalable-vector assertion constants
explicitly report NYI, matching the existing scalable-vector representation gap.

VN chunks use typed managed arrays and preserve native reserved IDs, 64-value
chunk boundaries and allocation grouping. Function applications borrow readonly
memory from stable chunk arrays rather than pointers into an arena. Floating
interning keys are raw bits; function keys retain native argument order and omit
result type. Failed function queries preserve ref outputs. Scalar constant
access uses generic numeric conversion rather than reinterpretation; Debug checks
require compatible storage size and floating/integral categories. SIMD12 storage
is exactly 12 bytes. ARM64 scalable/mask storage remains explicitly NYI; scalar
storage support does not imply VN folding or phase activation (B090).
Fixed-width vector/mask constants now use value-type dictionary keys and typed
chunk arrays. Generic constant import accepts a bounded readonly byte span and
uses unaligned-safe reads; SIMD retrieval copies the active payload into a
zero-initialized maximum-width value. Scalar numeric and vector access remain
separate managed helpers rather than emulating C++ template specialization.
ARM64 scalable/mask storage is still explicitly NYI (B097).

Unary function folding publishes its dictionary result after recursive calls,
without keeping a managed entry reference across possible dictionary growth.
Canonical field sequences use store-owned reference-identity tokens, with zero
reserved for null, rather than movable object addresses or pinned GC handles.
These tokens remain metadata: EE calls receive the sequence's actual field
handle. Field-sequence diagnostics retain native symbolic formatting; the
general VN dumper is not yet ported. Exception lists retain native unsigned-VN
ordering and recursive union rather than managed collection enumeration (B098).
Tree constant numbering reuses the bounded vector-import helper instead of
native stack temporaries and `memcpy`. Embedded-handle and field-address maps
are lazy managed dictionaries; failed embedded-handle lookup leaves its `ref`
output unchanged. Unknown compile-time class handles do not overwrite an
existing mapping, matching the pinned native guard (B100).

HWI creation-constant helpers accept the existing maximum-width `simd_t` by
reference instead of templating over native SIMD storage structs. They preserve
native lane order, full output zeroing for creation candidates and untouched
output for other intrinsic IDs. Nonconstant lanes stay zero, while recognized
lanes are populated even when the whole creation cannot fold (B102).

Vector unary evaluation uses bounded byte spans and typed `MemoryMarshal.Cast`
views instead of native template storage and per-lane `memcpy`. Integral lanes
use generic-math operations; floating arithmetic has separate overloads, and
floating bitwise operations are reinterpreted before any floating load. The
node wrapper copies back only active bytes, preserving inactive storage, while
xarch scalar operations retain upper lanes from the input (B104).

Binary evaluation uses the same bounded views, with an explicit evaluation
width in place of the native template's default storage size. Generic integer
and IEEE floating helpers retain wrapping arithmetic, exact comparison masks,
unmasked SIMD overshifts and masked rotates. Floating bitwise operations never
load floating values. Division retains native valid-input preconditions; no
fallback value is introduced for invalid integer division (B105).

Mask operations share the native element-size dispatch and mask-width policy.
Conversions use bounded byte spans: lane expansion fills raw bytes, and xarch
extraction reads each lane's sign bit using the supported little-endian layout.
This avoids loading floating values, preserving signaling-NaN bits. Mask
arithmetic normalizes all-true results to 64 set bits; vector-to-mask conversion
deliberately retains only the produced lane bits, matching native behavior.
ARM64 predicates use spaced bits and nonzero-lane extraction (B106).

Sequential MSB extraction is shared with xarch vector-to-mask conversion.
Conversion recognition uses inherited `GenTree` properties rather than separate
base/derived query implementations. Constant conversion creates a fresh typed
mask through the existing managed constructor; the outer folding dispatcher
remains responsible for morph and VN finalization (B107).

Per-element mask recognition checks bounded raw byte lanes rather than separate
native integer template instantiations. Only an entirely zero or entirely
one lane qualifies, so floating signed zero and NaN payloads are not treated as
numeric comparisons. Local and intrinsic mask-width compatibility is unchanged
(B108).

HWI reconfiguration changes only the ID and operands within the same
`GenTreeHWIntrinsic` object, not its CLR type. Partial changes retain the operand
array; full resets install the supplied managed array and invalidate borrowed
operand spans/refs. The native compiler/inline-array allocation parameters are
unnecessary. Existing ID normalization is applied, but side-effect flags and
other metadata remain caller-owned, as native requires (B109).

Xarch comparison normalization uses
`System.Runtime.Intrinsics.X86.FloatComparisonMode` rather than duplicating the
native enum. All 32 names/encodings and its byte representation were verified
against the pinned header. The native mapping and signaling-mode normalization
are unchanged (B110).

The Windows-x64 HWI folding dispatcher reuses the fixed-width evaluators and
element-access helpers. Scalar bit scans use framework leading/trailing-zero
counts while retaining the native no-fold rule for zero BSF/BSR inputs. Native
fallthrough becomes explicit `goto case`; operand-count invariants establish
non-null operands without suppressing nullable analysis. Node reuse, conversion
cancellation, side-effect ordering, morph marking and VN refresh are unchanged.
Other-target dispatchers remain deferred (B111). Hardware-intrinsic constant
reassociation (`fgOptimizeHWIntrinsicAssociative`) depends on that dispatcher;
its folding path explicitly terminates compilation on targets without it.

Floating reciprocal predicates use `BitConverter` and managed `IsNormal`
classification instead of native reinterpretation. The native exponent and
fraction tests are unchanged: normal signed powers of two qualify except
positive and negative one, even when the reciprocal is subnormal.

Managed debug destruction clears all use edges, unlike native's simple-node
operand clearing. It must traverse those edges before poisoning type/flags:
poisoning first sets `GTF_REVERSE_OPS` and breaks nonbinary HWI traversal.
Mask-zero construction now uses the existing zero-initialized mask constructor,
matching the native factory (B111).

Root class-initialization construction reuses the managed shared-cctor and
ReadyToRun factories. Its nullable result retains their existing helper-rejection
contract; zero-initialized resolved tokens use `default` instead of `memset`.
Helper selection, argument ordering and generic-context reporting match native
(B113).

Helper equivalence reuses the existing managed helper dictionary on the inline
root. Lookup preserves the output handle on failure through a `ref` parameter.
Virtual method-pointer construction omits native's unused call-info parameter;
method and parent token lookups retain their ordering and root-method context.

The outgoing-argument `hashBv` storage uses managed nodes and bucket arrays, not
compiler-owned arena free lists; `Init` therefore has no allocator state to reset.
Native pointer-to-link operations use managed byrefs, and growth keeps a tail
reference per destination bucket. Shrink merging, resize thresholds, 16-bit node
counts and bucket/node/bit traversal order are preserved, including the native
32-bit logical element stride on AMD64's 64-bit element storage. Callback traversal
uses a delegate in place of the native template functor. Bulk/set algebra and the
separate iterator are not yet ported (B114/B115).

`SharedTempsScope` is a managed `IDisposable` scope rather than a native destructor.
It restores the enclosing temporary stack before releasing its own locals back
to the pool, including on exception unwinding. `Stack<int>` enumeration preserves
the native top-down release order.

`CallArg.EarlyNode` is nullable, matching native argument placement: a late-only
argument has no early evaluation. The effective `Node` remains non-null, and
pre-morph consumers retain their early-node invariant through assertions.

Morph initialization composes `gtNewStmt` and `fgInsertStmtAtBeg`, exactly as the
native `fgNewStmtAtBeg` wrapper does. An EE request to use the class-init helper
establishes the required non-null tree invariant. Entry insertion order and phase
status match native, including E&C frame requirements not alone marking IR as
modified (B114).

Qmark discovery uses an `out` destination instead of an optional pointer-to-pointer.
Expansion updates the qmark's owning condition reference after reversal, including
when reversal creates a replacement node. The existing statement/block iterators
visit the newly inserted arms and remainder; managed throw conversion retains the
native callfinally-unpairing and profile-update order. Native single-arm likelihood
assignments are deliberately retained, not corrected only in C# (B116/B117).

Patchpoint transformation composes existing statement factories/insertion helpers
in place of native `fgNewStmtAtBeg`/`fgNewStmtAtEnd` wrappers. Managed unary and
binary node factories preserve `GT_PATCHPOINT`/`GT_PATCHPOINT_FORCED` shapes and
call flags; shared-counter lifetime, branch layout and probabilities are unchanged
(B118). This does not supply patchpoint code generation or OSR metadata.

`InlineResult` implements `IDisposable`; production owners use lexical `using`
scopes to replace the native destructor's decision reporting, including early
returns, loop continues and unwinding. Reporting retains native suppression,
Debug failure observations, permanent NOINLINE propagation and structured EE
notifications. UTF-8 reason strings use the existing scoped marshaling helper
(B128). The shared `vlogf` EE text-logging stub is still unimplemented (B129);
this is not an accepted diagnostic-output difference.

Post-import cleanup uses byrefs to EH table entries, retaining the native
inner-to-outer traversal and retry of a slot after descriptor removal.
Unlinked blocks retain their links for region trimming. The OSR conditional-flow
lambda is a local function, and statement creation uses the existing insertion
helpers. `double.MinNumber(1.0, ratio)` preserves native `std::min` when both
profile weights are zero: the resulting NaN ratio selects 1.0, not NaN.
The early-return completion-flag behavior is unchanged (B126). The preexisting
`fgVerifyHandlerTab` diagnostic stub remains a separate gap (B127).

Indirect-call spilling keeps managed byrefs to owning use slots and compares
them with `Unsafe.AreSame`, preserving native spill order while replacing nodes.
The shared transformer receives its non-null original call at construction;
CFG blocks remain nullable until their corresponding creation steps establish
the native invariants. Fat-pointer expansion preserves the tagged two-word tuple,
call cloning, argument placement and native 80/20 block versus 50/50 edge weights
(B119/B121). B123 completes guarded devirtualization and activates the pass.
The inferred-local type correction in B120 restores native behavior, rather than
introducing a managed deviation.

Candidate cloning shares the existing managed `gtCloneCall` implementation with
ordinary expression cloning. Candidate metadata and return placeholders retain
their native shared relationships until the caller repairs them. Vtable expansion
interprets the EE's managed `int` offset outputs as native unsigned values before
pointer-sized conversion and preserves 32-bit wrapping of their sum (B122).

Owning-link lookup returns a `ref struct` containing the actual owning byref.
The visitor records the parent and exact operand index, resolving that slot
immediately after traversal: C# visitor callbacks cannot retain their input
byref in visitor state. This preserves first-use selection even when multiple
operands share a node or execution order differs from tree-walk order.
GDV scouting replaces fixed-up return placeholders through their owning refs;
candidate metadata remains shared only where native shares it. The exact-GDV
fallback remains in the graph without incoming edges for later cleanup, and
native metric guards are retained (B123/B124).

Phi definitions own copied SSA-number arrays and expose readonly memory.
Reaching-VN traversal uses a managed stack and membership set, preserving native
push/pop order, conservative SSA lookup, duplicate suppression, cycles and early
abort. Memory phis are not traversed, matching native behavior (B091).

Checked-bound/index registries use managed membership sets; no enumeration order
is observed. Unsigned comparison results use a readonly record and failed queries
leave ref outputs unchanged. JTRUE bounds generation retains native edge
polarity and usefulness gates; it does not activate general assertion generation
or morph completion (B093). General assertion creation and JTRUE equality/type
facts are now also implemented. Unary native operand access uses the managed
`GenTreeUnOp` base rather than casting unary nodes to `GenTreeOp`; CSE comma
queries preserve the native single-store/same-local condition (B094).
Node-wide generation and morph completion now preserve native edge selection,
boolean implications and physical-definition kill/gen ordering. Native bitset
reference parameters are managed array references; the in-place bit operations
do not replace those arrays. The morph diagnostic invocation number is accepted
in both builds but used only in Debug, like the existing invalidation diagnostic
tree parameter (B099).

Profile weight lookup uses a managed `ref` output: native leaves the pointed-to
value unchanged when no profile weights are available, so an unconditional
`out` initialization would change that contract (B067).

Profile spanning traversal uses `Stack<BasicBlock>` for pending blocks and a
`List<BasicBlock>` successor snapshot. The snapshot is indexed from its end,
matching native `ArrayStack::Top(i)`; callbacks must not change later successor
selection through mutations of the live successor list (B068).

Sparse edge reconstruction stores managed edge/block-info objects, with a typed
`BasicBlock.bbSparseCountInfo` reference instead of a `void*`. The two dictionaries
are lookup-only: key equality, native duplicate-key assertions and Release
replacement, and the edge-key hash are retained; iteration order is not observed. Incoming/outgoing
model edges remain prepended linked lists, preserving summation and solver order
independently of dictionary storage (B069).

EH predecessor caches hold nullable managed edge heads: a cached empty list is
distinct from an uncached block. New exceptional edges prepend to the original
regular predecessor chain; SSA stress retains native in-place link shuffling.
Second-pass filter successor visitation is shared with DFS via a managed
callback, preserving region order and early abort (B071).

Natural-loop descriptors use managed references, ordered edge lists exposed as
read-only spans, and the existing native-word bit vectors. Discovery retains
DFS/header order, exceptional predecessors, backward worklist order and
parent/child/sibling identity. Loop and bit visitors use managed callbacks with
the same abort contract; each bit-vector word is captured before callbacks,
while later words are read when reached. Header-relative containment rejects
negative indices without checked-conversion exceptions (B072).

Profile checkers preserve the native flag-selection and failure policy (B074).
Missing-likelihood diagnostics format a snapshot of the managed edge's current
address; it is never dereferenced or retained for later use. These failure
messages have pointer-format coverage, not native-address parity, and introduce
no dump-comparison normalization.

Synthesis likelihood snapshots use ordered managed lists and cyclic gains use
a managed array indexed by natural-loop index. Reversal preserves unique-edge
order; seeded random draws retain the native sequence. Capping adjusts only the
first eligible conditional exit in loop order, not all exits (B076). Compiler
DFS construction is accessible to the synthesis class in place of native
friend-class access.

The profile solver's zero-initialized count vector is a managed array indexed by
block number. EH descriptors use the existing nullable-byref convention. Its
`std::max` comparisons retain native operand selection, including NaN and signed
zero, rather than adopting managed `Max` semantics (B077).

The synthesis driver retains entry-loop normalization, reachable EH input
seeding, four repair retries, metadata/source selection and deferred profile
checks (B078). Its call-count clamp and blend-factor bound preserve native
`std::max`/`std::min` operand selection. Debug double configuration uses a managed
array and read-only span, with the native two-pass allocation scheme. Conversion
uses .NET 11's invariant UTF-8 `double.TryParsePartial`, including hexadecimal
floats. R003 records the grammar, range and invalid-input differences.

Managed error-trap callbacks capture exceptions before leaving their
`UnmanagedCallersOnly` shim. An owned `GCHandle` keeps the action and captured
exception alive until the native trap returns. The regular trap reports
nonterminal managed failures as false; terminal HRESULTs and SPMI-only managed
failures are rethrown with their original identity and stack. Native exceptions
remain subject to the EE's trap. This adapts exception ownership to the managed
boundary without broadening the native recovery policy (B061).

`FatalJitException` carries the native `CorJitResult` exception payload. The
compilation boundary reports and returns that result instead of collapsing
skipped, invalid-code and implementation-limit failures into internal errors.
Lowering is active for the complete native mode where
`backendRequiresLocalVarLifetimes()` is false. The lifetime-enabled cleanup mode
rejects compilation with `CORJIT_SKIPPED` before P/Invoke or IR mutation, pending
the full `fgUpdateFlowGraph` dependency closure. The mixed-mode native body stays
in the residual tree. Non-Wasm `AfterLowerBlocks` is genuinely empty.
The unfinished allocation driver now explicitly reports `CORJIT_SKIPPED` after
lowering and stack preparation. This preserves the temporary native-fallback
boundary in G001, not a successful managed backend.

Liveness template policies use a static-interface generic parameter. The
tracked-local reverse-map array's length supplies its allocation capacity rather
than storing a second size field. Sorting retains native pivot and comparison
order because tolerance-based profile-weight comparisons can make that order
observable; it is not replaced with framework sorting.

`GenTreeCall` stores its tailcall, async-call, and unmanaged-call-convention
variants separately. The native union cannot be reproduced with explicit
overlapping fields because async debug information contains managed references.
Call flags still select the active variant, and cloning copies the same grouped
storage. These are managed IR objects, not JIT/EE interop structures; see B019.

`GenTree.BashToNOP` retains the existing managed object, links, and logical node ID.
`GT_NOP` uses only base `GenTree` fields; the object's CLR subtype does not change.
Traversal and dumping must classify the node by its operator rather than treating
its former subtype's operands as live. This preserves native in-place folding
without allocating a replacement node or changing subsequent node IDs.

The use-edge enumerator represents native pointer/function-pointer iteration
with managed byrefs and explicit index/cursor state. Only actual operand storage
is yielded; absence is a terminal state, never a dereferenced null byref. Reset
clears every linked-list cursor. Writable edge identity and execution order are
preserved, including distinct early/late argument order (B040).

Native `LocalDefProvider`/generic definition callbacks map to the five original
readonly descriptor structs implementing `ILocalDef`, constrained
`ILocalDefVisitor.Visit<TDef>` calls, and shared SSA extension methods.
Visitors are passed by reference so mutable visitor state survives traversal
without boxing descriptors or allocating a callback per definition. Descriptor
queries remain lazy; promoted-field order, offsets, SSA indices, and early abort
match `compiler.hpp`.

`LclVarSet` keeps the native empty/single/set transition, including expansion on
a second insertion of the same local and retaining expanded storage after clear.
Its expanded membership storage uses `HashSet<int>` instead of `hashBv`; only
membership, intersection, and emptiness are observable through this API, not
iteration order. The separate general-purpose `hashBv` port remains incomplete.
Pinned upstream quirks B049/B052 are preserved rather than silently corrected.

`SplitTreeVisitor` stores a node owner and operand ordinal in its managed
use stack, reacquiring actual writable operand references through `UseEdges`.
The statement owns the root slot. This avoids keeping managed byrefs inside
heap collections or exposing `Stack<T>` backing storage; resolving an operand
scans that owner's edges. Slot identity, rather than the referenced node's
identity, governs matching and rewrites. `gtSplitTree` returns the split use
by reference and exposes native change reporting through `out bool madeChanges`.
Splitting retains native execution/spill order and statement debug information.

GC-safe-point cycle detection uses a `List<GCSafePointSuccessorEnumerator>` as
the native DFS stack, accessing its top through a transient `CollectionsMarshal`
span. No reference is used after resizing the list. The enumerator retains the
native two-successor inline storage and separate larger-array storage; diagnostic
traversal remains top-down. `BasicBlock.GetLastNode()` maps native `lastNode()`
without hiding the inherited LIR-only `LastNode` property.

Single-use inline arguments cannot use native `GenTree::ReplaceWith`, which
copies a node's complete representation (including its vtable and source tree
ID). `fgReplaceInlineArgument` instead rewrites matching edges in the unthreaded
inlinee's statements and detached return substitution before block splicing.
It retains the source object and ID, updates shared references, and permits an
already-eliminated use. This costs a scan of the inlinee per eligible argument,
unlike native pointer bashing; no throughput equivalence is claimed. It is not
a general-purpose replacement for native object retagging.

Continuation members are immutable managed descriptors in a root-owned
`List<ContinuationMember>`. Indices use managed collection-sized integers; lookup
preserves native insertion order and layout/depth compatibility. Reading a member
returns the descriptor by value rather than exposing a reference invalidated by
list growth. Symbolic offsets retain the member index until async layout, including
the native object-header adjustment and diagnostic text.

The debug tree hash preserves native operator, payload and operand ordering.
Where native hashes a `ClassLayout` address, it uses stable managed reference
identity instead; the hash is not printed and is used only to detect tree
changes for diagnostics.

Rationalization uses insertion-ordered lists for parameter uses and incoming
register mappings, preserving native bottom-up iteration. A mapping stores its
classified ABI segment by value rather than holding a native pointer into the
parameter's ABI storage. Ancestor access uses the existing managed stack's
struct enumerator, without copying or reversing the traversal stack.

Xarch ternary intrinsic input-use flags are computed from the control byte's
truth table; they agree with the native decomposition table for all 256 controls.
The complete decomposition table is also generated from the pinned native
rows. Each constant word stores three seven-bit operation/use pairs, preserving
native step and operand order without a managed object table.
Mask sizes, broadcast tuple compatibility and EVEX instruction flags are generated
from the existing native instruction headers. Encoding eligibility uses the
emitter's vector/promoted EVEX modes and per-instruction ISA restrictions.
Embedded broadcast additionally requires vector EVEX, a compatible tuple and a
contained scalar-broadcast operand. The temporary compiler-capability query and
its duplicate broadcast/EVEX boolean tables have been replaced by these shared
native queries.

Tree dump callers always supply an indentation stack. An empty stack still
corresponds to a non-null native stack, so it must not trigger the fractional
sequence labels reserved for native calls without a stack.

Code generation owns the canonical mutable `GCInfo`, `RegSet` and disassembler
values. Their collaborators use managed owner references and ref-returning
accessors instead of copying state represented by native pointers/references.
GC descriptor unions use typed storage selected by their native discriminants;
spill/GC descriptor links remain managed references. Disassembler streams reuse
the existing managed text-output types.

Spill temporary preallocation creates managed `TempDsc` objects rather than
arena allocations. The native negative numbering, normalized types, size-slot
lists, acquisition/release order and total stack-space accounting are retained;
equal-size GC and non-GC types still require distinct matching descriptors.

LSRA variable-to-register maps use managed register arrays while retaining native
tracked-index mappings, padded entry counts and split-block aliases.
`setInVarToRegMap` copies the logical entry count into the existing destination;
it does not reproduce the pinned native function's oversized byte copy from a
`regNumberSmall` buffer using `sizeof(regNumber)`. No native callsites were found
for that function; B165 records this as a source discrepancy, not a reproduced
runtime failure.

`GenTreeFieldList.Uses` likewise returns its mutable list by reference, so
cloning and lowering update the owning node rather than a discarded copy.

LSRA intervals, physical registers and reference positions use managed objects
and links; growing the owning reference list does not move its entries.
The reference-kind and physical-register discriminants select typed referents
and mask fields instead of overlapping native union storage. AMD64 callee-save
sets reuse the generated `typelist.h` register classification.

The pending-definition list uses managed links and the existing node pool,
matching native removal by node identity and multi-register index. Temporary
uses consume those entries in native order; local uses retain their owning node.
Interval creation reuses the canonical diagnostic formatter rather than a
separate managed representation of the same dump.

The common operand-use builder accepts `GenTreeUnOp` and obtains a second
operand only from `GenTreeOp`. Native `GenTreeOp` represents both shapes;
requiring the managed binary subclass would exclude valid unary callers.
Target-preference outputs use a value tuple instead of two native output
pointers, preserving their initialization and update rules.

`identifyCandidatesMinimal` represents the complete native
`identifyCandidates<false>` specialization, including EH exception/finally
sets. Optimized candidate selection remains separate. Its diagnostic set
conversion scans local descriptors in local-number order rather than allocating
native's temporary byte-per-local array; printed local names and order are
unchanged.

`buildIntervalsMinimal` implements the complete AMD64 `buildIntervals<false>`
specialization and rejects enregistered-local mode before mutation. Candidate
selection leaves no local candidates, so native initial-parameter definitions,
parameter preferences, local zero initialization and interval validation have
no work in this mode. Incoming parameter-register liveness and all temporary
references remain required and are preserved. The mixed-mode native body is
retained for optimized allocation.

`tupleStyleDumpPre` and its operand/node formatters specialize the existing
`LSRA_DUMP_PRE` predicate, preserving the complete pre-allocation dump. The
reference-position and post-allocation modes remain required before activating
the allocation driver. Node sequence numbers retain their native unsigned
display through the bit representation of the existing managed integer field.

Internal-register definitions, call definitions and kill references retain
native location ordering, register preferences and upper-vector save rules.
Write-barrier classification is exposed through `ICodeGen`, matching the native
codegen interface rather than requiring a concrete implementation. The compiler
owns the shared GC-reference kill query.

Floating-point preference sets and local upper-vector intervals belong to
optimized candidate selection, which remains unported. There is no separate
kill-state initialization phase or second candidate scan. The minopts
specialization does not produce those local sets; upper-vector saves for
temporary values remain supported.

Liveness policies use static interface members in place of native template
traits. Per-block scratch and stored sets retain independent managed storage.
An empty bitset span is a valid initialized set when its trait environment has
zero elements, even though the same representation also denotes uninitialized
storage in nonempty environments; canonical assignment distinguishes those
cases rather than bypassing liveness for zero-local methods.

Handler-liveness marking has internal access to the descriptor's existing raw
handler-live bit. The checked public getter and underlying flag storage are
unchanged, so marking can reset untracked locals without weakening consumer
invariants or adding duplicate state.

Backward LIR liveness reuses lowering's unused-indirection transformation and
retargets its cached predecessor when that operation replaces the node. Native
in-place bashing leaves the cached address valid; the managed replacement
detaches the old object. Both local and indirect dead-store paths preserve
reverse traversal rather than revisiting detached nodes or stopping early.

`RunLIR` and `InterBlockLocalVarLivenessLIR` explicitly specialize native
liveness orchestration for non-early linear IR. Both reject incompatible
policies before diagnostics or compiler mutation. The native phase sequence,
backward analysis and repeat condition are unchanged; expression-tree and
early-liveness orchestration remain separate, rather than silently entering
an incomplete generic driver.

`Compiler.fgPostLowerLiveness` selects the native post-lowering policy through
that LIR driver: dead-code elimination is enabled, while SSA, memory liveness,
early mode and address-exposed-local tracking remain disabled. This entrypoint
does not itself activate the lowering phase.

LIR node replacement uses the existing metadata-preserving constructors with
`NodeThreading.LIR`, followed by `LIR.Range.ReplaceNode` to transfer the owning
use and range links. Address-mode lowering takes its address alias by reference,
so replacing `GT_ADD` with `GT_LEA` preserves logical IDs without retaining the
old managed node. Operands eliminated by the native transformation are removed
in the same order.

Conditional-compare chains likewise replace relational and boolean nodes with
CCMP and SETCC nodes, preserving logical identity and clearing value numbers
as native cross-kind changes do. The traversal continuation comes from the
attached replacement, not the detached original node.

Unused-load conversion likewise replaces indirection nodes and returns the
current node to its caller. The constructors retain the nonfaulting flag that
native `ChangeOper` preserves. Stack-argument `GT_BLK` to `GT_IND` conversion
preserves all flags and clears value numbers, matching native `SetOper` with its
default `CLEAR_VN` argument, and rewrites the owning LIR use before lowering the
new indirection. `BashToConst` also calls `SetOper`; retyped floating constants
must clear VNs even when their metadata-preserving constructor copies them.

### D003: Deferred non-Windows-x64-only paths

**Status:** accepted scoped deferral; not a successful execution/parity result.

Port whole functions. A path unique to another target may explicitly terminate
with NYI while remaining compilable. Record the affected symbol, target predicate,
and missing behavior when introducing such a deferral. Windows-x64 behavior
within the function must not be replaced by stubs. Verify that the selected
failure path cannot continue as if implemented in Debug or Release.

`LowerTailCallViaJitHelper` remains deferred to Windows x86. Its native body
assumes four 4-byte special stack arguments and x86 register-restoration flags;
native `fgCanTailCallViaJitHelper` rejects every other target. The generic call
branch retains an explicit rejection rather than accepting an unexpectedly
flagged AMD64 call. Windows-AMD64 fast-tailcall lowering is implemented.

`RegSet` initializes Swift callee-saved masks under `SWIFT_SUPPORT`, as native
does. Its non-AMD64 Swift path reports NYI and then terminates with
`fatal(CORJIT_IMPLLIMITATION)` until the ARM64 target masks are available.
The native constructor remains in the residual tree.

`LinearScan.setFrameType` implements AMD64 frame selection. Other targets report
NYI and terminate with `CORJIT_IMPLLIMITATION`; their double-alignment and
target-specific frame/register policies remain in the native tree.

`Lowering.IsCallTargetInRange` implements the xarch policy. Other targets report
NYI and terminate with `fatal(CORJIT_IMPLLIMITATION)` pending their call-target
range checks; the shared direct-call lowering body remains in the native tree.

`Lowering.IsContainableImmed` implements the xarch immediate and relocation
rules. Other targets throw `NotImplementedException` pending their
instruction-specific immediate checks; no non-xarch lowering is activated.

`Lowering.TryCreateAddrMode` implements xarch address folding and interference
checks. Other targets throw `NotImplementedException` pending their volatile,
scaled-index and explicit-address-add handling.

`Lowering.LowerIndir`, `ContainCheckIndir` and `LowerPutArgStk` implement the
xarch bodies. Other targets throw `NotImplementedException` pending their
target-specific indirection containment and argument placement rules.

`Lowering.ContainCheckCast` and `ContainCheckBinary` implement xarch containment;
other targets throw `NotImplementedException`. Xarch read-modify-write
recognition retains native status caching, whole-address interference checks
and temporary LIR marks. These helpers do not activate arithmetic or cast
lowering, including floating conversion expansion and optimized transforms.

`LinearScan.calleeSaveRegs` currently supports AMD64. Other targets report NYI
and terminate with `fatal(CORJIT_IMPLLIMITATION)` pending their callee-save sets.
The interval/reference support does not activate register allocation.

`LinearScan.buildNode`, `buildStoreLoc`, `buildMultiRegStoreLoc` and `buildReturn`
implement AMD64 node dispatch, local stores and ABI return constraints. Other
targets report NYI and throw `FatalJitException`, pending their target-specific
dispatch and register constraints. Node-reference and interval construction
do not activate the unfinished allocation driver.

`LinearScan.buildIntervalsMinimal` and `buildRefPositionsForNode` explicitly
throw outside AMD64, pending target-specific interval/reference requirements.
`CodeGen.genGetGSCookieTempRegs` implements the native xarch selector and
explicitly rejects other targets. Swift conditional code is translated but is
not enabled or execution-validated by the Windows-x64 configuration.

Current target-sync additions under `TARGET_WASM` are
`Compiler.fgWasmRepairTryEntries` and `Compiler.fgWasmSpillRefs` in
`Compiler.fg.cs`. They throw `NotImplementedException` rather than returning a
successful phase status. Try-entry repair and GC-reference spilling remain
unported; no Wasm execution support is claimed.

The parameterless `CLRRandom.Init` in `sources/Core/inc/random/CLRRandom.cs`
currently supports only Windows, using the native performance-counter, OS-thread,
and process inputs. It explicitly throws on other hosts. Explicitly seeded
initialization is portable; no unseeded cross-host sequence equivalence is claimed.

`GenTreeVecCon.Equals` defers `TARGET_ARM64`'s `TYP_SIMD` scalable storage.
`GenTreeMskCon.Equals` likewise defers the scalable-mask branch selected by
`TARGET_ARM64 && DEBUG` and `JitUseScalableVectorT`. Both report NYI and then
call the nonreturning `fatal(CORJIT_IMPLLIMITATION)` path, even if NYI reporting
itself returns. Fixed-width comparisons are implemented; the native vector/mask
equality bodies remain in the residual tree as the scalable-storage reference.
Windows-x64 comparisons are covered; ARM64 builds/execution remain unverified.

`Compiler.gtNewConWithPattern` implements scalar and fixed-width vector byte
patterns. Its `TARGET_ARM64` / `TYP_SIMD` branch throws `NotImplementedException`
until scalable-vector constant construction is ported. The native factory and
declaration remain in the residual tree as that branch's reference.

`CallArgs.AddFinalArgsAndDetermineAbiInfo` implements outgoing argument
classification and non-standard argument insertion for Windows x64. Its Wasm
branch throws `NotImplementedException` pending shadow-stack argument insertion;
the complete native body/declaration remain available in the residual tree.
Unix varargs also throw explicitly, matching the native unsupported path rather
than continuing with an unsupported ABI. Other target-specific register rules
are source-ported, not execution-validated. Classification is not argument
evaluation/scheduling and does not activate call morphing.

`Compiler.gtHashValue` explicitly throws for ARM64 scalable-vector constants
pending their representation support. Its native body remains in the residual
tree as the deferred branch's reference.

`Rationalizer.RewriteHWIntrinsic`, scalar intrinsic handling in `RewriteNode`,
and `RewriteSubLshDiv` explicitly throw for their unported ARM64 mask-reduction
and arithmetic rewrites. Non-xarch constant/variable shuffle factories and
target-specific hardware immediate/MSB rationalization paths likewise throw.
`GenTree.GetIntegralVectorConstElement` defers ARM64 scalable-vector storage.
The mixed-target native hardware bodies remain in the residual tree; this
batch establishes Windows-x64 rationalization, not other-target execution.

### D004: Separate local assertion application from global analysis

Global morph's three assertion-application callsites use local mode, with no
statement or block argument. They call `optLocalAssertionPropTree` rather than
the mixed local/global native dispatcher. Local cast and comparison application
retain their algorithms; the native arithmetic, division, bounds, ordered
comparison, array-length, hardware-intrinsic and JTRUE cases make no changes in
this mode. No optimization flag is disabled to obtain this boundary.

The global VN/SSA-based dispatcher and its range-analysis dependencies remain
unported. Their shared native bodies are retained in the residual tree. This
mode split prevents an optional later phase from blocking required morphing,
without adding success-shaped fallbacks on the active path.

## Implementation notes and parity findings

### R001: Temporary serialization for debugging

`sources/Core/jit/ee_il_dll/CILJit.cs`, `compileMethod`, uses a process-local
static lock around compilation. The corresponding native entry is in
`src/coreclr/jit/ee_il_dll.cpp`.

**Status:** confirmed temporary debugging aid, not a permanent concurrency design
or evidence that serialization is required for correctness.

The host can invoke the AltJit concurrently from multiple JIT threads. The lock
was added to keep those invocations from running in parallel while debugging.
It may be removed, excluded, or replaced with a more suitable debugging mechanism
when needed. Before claiming concurrent compilation support, review shared/TLS
state and validate parallel/reentrant compilation; the lock currently masks that
dimension of behavior.

### R002: Windows dump line endings

**Status:** output-path mismatch corrected by `JitTextWriter`, not waived as an
output exception. Full compiler dump parity remains outstanding.

Native `src/coreclr/jit/ee_il_dll.cpp`, `jitstdoutInit`, uses CRT text-mode
output on Windows, which translates LF to CRLF. The managed implementation in
`sources/Core/jit/ee_il_dll/Globals.cs` uses `StreamWriter`, and `jitprintf` in
`sources/Core/jit/host/Globals.cs` calls `Write(message)`. Embedded LF characters
are written unchanged. Setting `StreamWriter.NewLine` affects `WriteLine`, not
those embedded characters. A standalone Windows CRT/.NET probe confirmed
`probe\n` produces `70726F62650D0A` natively and `70726F62650A` through this managed
writer setup. This is a byte-level line-ending difference, not different IR or
generated instructions.

The shared writer now applies the native host's text-mode encoding below
`StreamWriter`, including embedded newlines and fragmented writes. Dump output,
function-info logging, and timing CSV writers use it. Sixteen focused cases pass
in Debug and Release on Windows, including explicit CR/LF, write overloads,
append behavior, and stream ownership. The initial six-method native/managed
corpus confirms that this change affects only newline encoding in the captured
managed dump. The Unix branch preserves LF; execution on Unix is not yet verified.

**Remaining action:** compare message layout as more functions become reachable;
the writer cannot restore newlines omitted at individual ported call sites.
Using managed output is not itself a defect. Broader culture/encoding fidelity
still needs comparison. The later numeric-IL investigation found and corrected
fixed-point and non-finite formatting differences; see B006 in the backlog.

The earlier object-identity concern was too broad: the two `PendingDsc` hash-code
diagnostics in `Compiler.imp.cs` are both behind `if (false && verbose)`, matching
disabled native diagnostics in `importer.cpp`. They do not currently affect dump
output. `Compiler.dspOffset` also preserves the upstream nonzero
`0xD1FFAB1E` substitution in diffable mode. If those diagnostics are enabled later,
review their identity and pointer formatting then; they are not a demonstrated
active parity failure or justification for stripping hashes from comparisons.

### R003: Debug double-configuration parsing

**Status:** managed conversion selected by the user; not a general dump-normalization exception.

Pinned `utils.cpp:1025-1066` can loop indefinitely when `strtod` makes no
progress, retains stale `errno`, and dereferences null despite documenting it as
allowed. `fgprofilesynthesis.cpp:1164-1173` indexes the first element even when
the array is empty (B075).

`ConfigDoubleArray` uses .NET 11's UTF-8 `double.TryParsePartial` with invariant
culture and `NumberStyles.Float` or `NumberStyles.HexFloat`. A small fallback
accepts the CRT's signed, case-insensitive `inf` abbreviation. There is no native
import, Windows restriction or `errno` dependency.

Ordinary decimal/hexadecimal values, signed zero, infinities and incremental
consumption are retained. Managed overflow produces infinity and underflow
produces zero; representable subnormals are accepted. Hexadecimal values require
a `p`/`P` exponent. Plain signed NaNs are accepted without preserving CRT payload
bits; payload spellings such as `nan(ind)` and `nan(snan)` are unsupported.
Consequently NaN dump spelling can differ from the CRT input's spelling. Do not
add payload compatibility without an actual consumer need.

Malformed input and unsupported spellings throw `FormatException` rather than
hanging or silently discarding values. Null/empty input produces an initialized
empty array; trailing ASCII whitespace and commas are accepted. The synthesis
caller separately rejects an empty configured setting because it requires a
first value.

Finite factors outside `[0, 1]`, infinities and NaNs still retain the native
default exception weight; they are not configuration syntax errors. No caller
other than profile synthesis has been wired to this new helper.

Twenty-eight Debug cases cover conversion, range boundaries, special values,
separators, invariant culture, explicit failure and ordinary dump layout.
Complete synthesis dump parity remains unestablished. Do not normalize
malformed-configuration or special-NaN output differences in parity reports.

## Incomplete implementation, not intentional deviations

| ID | Evidence at the recorded C# baseline | Required action |
| --- | --- | --- |
| G001 | `Compiler.comp.cs`, `compCompileHelper`/local `GetResult`, deliberately returns `CORJIT_SKIPPED` until codegen exists. | Separate phase validation from codegen success and native fallback. Return success only after real code and required metadata exist. |
| G002 | `Compiler.cs`, `Compiler.fg.cs`, and `Compiler.opt.cs` contain phase methods returning `MODIFIED_NOTHING` with port TODOs. | Treat each as a stub, not a verified no-op. Replace complete functions in dependency order. |
| G003 | `Compiler.imp.cs`, `impHWIntrinsic`, currently returns `null` under a port TODO. | Reconcile the full intrinsic contract, including `mustExpand`; ordinary-call fallback is not proof of parity. |
| G004 | The original baseline had an empty `tests/Core/RyuJitSharp.UnitTests.csproj`. | Focused output regression cases now exist. Do not infer compiler-wide coverage; prioritize dump/disassembly and runtime-test validation. |

These entries describe the unstashed baseline. Reassess the affected gaps when
the saved WIP is integrated; do not overwrite existing work based on this table.
