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

`compCompileFinish` reports `BytesAllocated` as the current compilation thread's
GC-allocation delta from the root `Compiler` constructor body to finalization.
It includes intervening inlinee allocations on that thread, but excludes the
root object and field initializers allocated before that body and non-GC native
allocations. It is not native arena capacity or page usage. Collection is enabled
by `JitReportMetrics` or timing CSV collection. The CSV's `Total Bytes Allocated`
column reads the same root-thread delta at its own reporting point; native uses
arena allocation rather than arena usage for that column. B352's twelve checked compilations compare all
71 neighboring metric lines exactly while recording both allocation values.

Optional `DISPLAY_SIZES` totals measure emitted IL, code/data and GC payloads,
not managed compiler allocations. They retain native arithmetic and diagnostic
formatting and are not covered by the allocation-statistics exclusion.

Expression cloning constructs simple nodes and hardware intrinsics before their
operand clones, preserving native logical IDs despite managed allocation.
Array-element, compare-exchange and select nodes retain their distinct native
construction order. This closes B307's finally and runtime-lookup tree differences;
it does not accept ID normalization or establish whole-dump equality.

Integer clones preserve native types and compile-time handles. Their diagnostic
target cookie follows the native cloning mode: late-disassembly handle clones
omit it, and cheap `gtClone` omits it for ordinary constants too. Matching this
metadata policy closes B320 without changing the instruction-comment formatter.

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

Profile diagnostic helpers retain native decimal digit counts and seven- or
three-significant-digit lowercase general formats (B321/B323). Managed string
formatting does not exempt column spacing, precision or exponent case.

CSE temporary reasons retain native two-digit candidate indices, and array
node names retain rank-minus-one commas (B305/B308). Intentionally unpadded RL
feature records are unchanged. These corrections do not establish full CSE
diagnostic or array method-identity parity.

Array type printing now terminates after the rank suffix, matching the native
EE-query boundary (B301). The prior extra VM class-name query was a translation
defect, not an allowed name or hash difference. All baseline method identities
and emitted instruction traces now match native; whole dumps and raw machine
bytes remain separate evidence requirements.

CSE diagnostics use read-only spans over existing availability sets, preserving
native bit order, candidate labels and cross-call suffixes (B338). Checked
arithmetic does not exempt native unsigned hash truncation (B339).

Allocating paired exception wrappers and normal-value replacement explicitly
evaluate the conservative operation first, matching native Windows-x64
constructor-argument evaluation (B340). The returned pair retains its original
liberal/conservative roles. Other explicitly sequenced pair operations and the
shared unique normal value are unchanged. All 97 baseline CSE phases now match
without normalizing value numbers; this is not other-target or whole-dump parity.

Range-assertion diagnostics include the conservative normal VN and phi-edge
assertion indices. Precise map stores/selects and physical selects print native
dollar-prefixed hexadecimal VNs (B330). All 34 affected lines in the two stress
captures now match. Floating local costs also retain native AMD64 size adjustments
for register and memory operands, without an extra floating memory execution
penalty. All observed local/parent cost lines now match; constants and general
operand ordering are unchanged.

Integral SIMD broadcasts and mask/folding bit conversions explicitly retain
native truncation and signed/unsigned reinterpretation under checked builds
(B343). This preserves sign bits, all-true masks and overshift sentinels without
changing negative-index rejection or bit-scan undefined-zero guards.
The project's general overflow-checking policy is unchanged.

SSA memory maps use the existing managed node-identity dictionary. The caller's
shared-memory flag selects the map kind before the inline root supplies storage,
preserving native aliasing and ownership. Shared all-successor visitation retains
native regular/EH order, early abort and callfinally-target suppression; it does
not change the separate EH-only traversal.

Constant-data sections own managed byte arrays, block-reference arrays or
emitter-location arrays instead of a native payload union. The linked section
order, logical offsets, alignment and payload-kind checks remain native.
Span inputs are copied into owned storage; tagged EE data-offset handles retain
their native bits. This does not implement final runtime data publication.

Emitter instruction descriptors remain managed objects rather than packed native
records (B207). Temporary and saved groups retain the pinned native logical
sizes and offsets, including debug pointer prefixes and GC headers. Saving
transfers descriptors into group-owned arrays and resolves jump, alignment and
last-instruction references through those arrays. It preserves native list
reversal and append order without copying CLR objects into unmanaged storage.
Actual emitted-code patch addresses remain native pointers; they are not
narrowed to managed descriptor offsets.

ARM64 sizing follows native format categories and payload flags, including
local-variable pairs. On the Windows 64-bit host, ARM64 fat-call descriptors
reserve 80 bytes rather than AMD64's 72 because of native bitfield padding.
Non-Windows ARM64 fat-call layouts and x86 descriptor layouts remain explicit
dependencies, not inferred sizes.

Final emission retains pinned managed arrays for native-pointer data-chunk,
logical-offset, frame-offset and large argument-tracking tables. Their owners
remain attached to the emitter, preserving compilation lifetime across EE
callbacks and subsequent emitter queries. Inline argument storage is pinned
throughout instruction issue. Executable and writable code/data buffers remain
EE-owned; this does not change their allocation or relocation contract.

Basic-block label cookies hold managed instruction-group references rather than
native `void*` values. They identify those same groups without fabricating
unmanaged addresses or pinning managed objects.

Constant/displacement descriptor families retain native pointer-sized payloads
and logical sizes. Managed inheritance and ref aliases provide the shared first
constant slot used by native descriptor casts without depending on CLR object
layout.

The descriptor's pointer-sized, reference-free address union overlays packed
local addresses, xarch address modes and additional register fields. Explicit
layout preserves their native aliasing; custom EVEX/APX context bits retain
their shared interpretations. Neither representation changes logical descriptor
sizes or authorizes different instruction sizes or encoding choices.

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

B141 now includes complete object-allocation orchestration, retaining native
mode/configuration gates and heap fallback decisions. Minopts still uses heap
allocation. Optimized stack allocation no longer stops at the earlier phase
gate; execution evidence and remaining layout/clone coverage are tracked
separately from implementation completeness.

Connection-graph escape closure, allocation-site/alias traversal and conditional
clone viability are implemented, including guarded enumerator tracking, stack
viability, overlap and profitability checks. `fgCloneTryRegionFeasibility`
retains the native no-insertion mode, while `fgCloneTryRegion` now implements
insertion, EH renumbering, block-state cloning and mapped successors.
Object-specific clone transformations, stack/heap morphing and pointer/use
rewriting are now implemented. Self-copy elimination uses the existing
base-field-only `BashToNOP`, preserving aliases and logical node identity
without cross-kind CLR retagging. Production orchestration and stack-array
expansion are active. Real JIT/EE-backed classes and value arrays execute on the
stack; boxed/reference-lifetime controls retain the native heap decisions.
Conditional-clone execution and additional layouts remain coverage gaps.
Split-created block operations are remorphed before expansion proceeds; scalar
zero replacement passes the current threading mode to the existing node factory
rather than relaxing its unthreaded-default contract.

Hardware-import prerequisites preserve native argument order and precise types,
table-driven eligibility and xarch immediate/range-check rules. SIMD/mask stack
normalization, call return buffers and scalar argument coercion are implemented.
SIMD creation, nonconstant shift/rotate fallback, signature-derived SIMD size
lookup and AVX-only compatibility flags are also implemented.
Portable vector and xarch-special import bodies and their SIMD constructors are
active through the complete Windows-x64 generic dispatcher. Table eligibility
preserves both `SpecialImport` and `InvalidNodeId`, as native does. GS preparation
initializes cookies and shadows vulnerable parameters before code generation.
Nonconstant hardware execution covers vector construction, addition, lane access,
shifts, memory operations, BMI extraction and CRC, including GC-stress runs.
Six hardware helpers and the constant `FoldHardware` method have exact native
bytes; the stack-buffer case and other remaining code/dump differences are not
accepted parity exceptions. Other-target execution remains unvalidated.

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
The VN predicate follows native phi traversal and intrinsic rules (B086/B091).

Binary VN constant evaluation includes native eligibility and exception guards,
numeric casts and bitcasts. Binary interning, algebraic identities and VM type
comparisons are implemented, preserving constant-interning order and runtime
definite/unknown answers. Related-comparison tables retain native entries and
ordering. Scalable ARM64 all-bits constants explicitly report NYI. Ternary and
quaternary interning use the existing chunk/app storage and preserve variable
arity and `MapStore`'s fourth-argument exception contract. Map selection and full
VN phase orchestration are implemented.

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
handle. Field-sequence and general VN diagnostics retain native symbolic
formatting; EE-dependent names and full phase dumps still need runtime evidence.
Exception lists retain native unsigned-VN
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
The early-return completion-flag behavior is unchanged (B126).
`fgVerifyHandlerTab` now implements the complete native Debug contract (B127).
Its temporary lexical-order, handler-start and raw-index maps use owned arrays
instead of native stack scratch space; block numbers are not modified. The
managed table's array length supplies native allocation capacity. Normalization,
mutual protection and funclet-sensitive checks retain their native conditions,
including the disabled last-block normalization.

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

Map selection has its own local/memory-phi fixed-point traversal and native
budgets. Its cache retains ordered memory dependencies; managed membership
storage must not change insertion order or omit the entry that promotes a
small set. SSA memory allocation explicitly initializes the VN pair, since
allocating a managed struct array does not invoke element constructors.
Local SSA definitions likewise initialize current and Debug original VN pairs;
initial definitions explicitly invoke their constructor after SSA allocation.

Incremental SSA uses the same explicit initialization for inserted definitions.
Its block/local dictionary key compares block references and local numbers,
independent of mutable block numbering. Definition lookup, backward liveness and
lazy phi construction retain native traversal and insertion order. These
prerequisites do not activate CSE or establish broader EH/cyclic-phi parity.

VN pair operations preserve lane ordering and native sharing decisions rather
than always allocating two opaque values. Memory SSA recording changes only the
liberal lane; GC-heap stores invalidate separate byref state, while shared states
retain one value number. Array-address parsing exposes the native failure
contract as a nullable array output and an unchanged `ref` index VN. Its constants
use target-pointer width, not the host-sized `VNForIntPtrCon` API.

Physical loads retain native's unsigned offset truncation before the
whole-location check; the later bounds check still uses the original signed
offset (B278). Pointer extension uses native host-sized offsets and preserves
the source liberal exception set. Call and memory numbering use these contracts
in the active VN phase.

Tree dispatch, Windows-x64 math/hardware-intrinsic evaluators and full VN phase
orchestration are active. Reachability uses normal liberal branch values and
retains shared-edge behavior; later redundant-branch optimization still owns
removal of proven-dead edges. Per-loop effect storage is an array, matching
native indexing.

Loop field/element maps retain managed membership storage but expose native-order
enumerators that reproduce bucket traversal, collision-chain order and rehashing,
including growth before overwrite lookup. VN allocation consumes those
enumerators, not dictionary iteration. Phase execution is established for the
selected corpora; clone numbering, loop-block traversal and later optimization
differences prevent a full dump/code parity claim.

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

Range analysis, relational application and forward assertion dataflow now feed
the active global assertion phase. The complete range-check elimination phase
also runs with native bounds-check/SSA gates, analysis budgets and side-effect
repair. Established execution corpora preserve their behavior but show no
additional removals in that phase; focused tests cover successful removal.
A dedicated array corpus also exercises managed removal and preserves exception
and side-effect behavior. With loop cloning active, its recorded `SumByLength`
check is removed during cloning by both JITs; B283's specific phase-placement
difference is resolved without changing range analysis.
Duplicate conditional edges preserve native BitVec assignment:
single-word values are copied before destructive intersection, while multi-word
storage remains aliased. The forward-analysis callback interface replaces the
native template protocol without changing traversal or convergence.

VN copy propagation is active. Managed definition records retain compiler/local/
SSA identity and resolve the live descriptor rather than copying SSA state.
Candidate iteration reproduces native hash-bucket order, including insertion and
rehashing; the managed dictionary's enumeration order is not used. Dominator
scoping, liveness and profitability gates remain native. Selected loop/array
captures exercise substitutions, without establishing full phase/dump parity.

VN-based dead-store removal is active after the native SSA-invalidation boundary.
Native retags a store as `GT_COMMA`; managed code replaces its owning use,
preserves the logical tree ID and data effects, and repairs threading and
ancestor flags. The typed SSA `DefNode` reference is cleared rather than left
pointing to the detached store; its descriptor and VN pair remain available for
later comparisons within the pass. SSA-dependent consumers precede this phase.
SSA definitions can outlive removed statements or retain an old block after
statement movement. Managed replacement searches live owning uses when needed;
an already-detached definition requires no live-tree edit. Both paths preserve
the native NOP allocation and clear the typed definition reference.
The bounded runtime probe preserves effects and exceptions but triggers no
removals in either JIT. Positive-removal execution and code parity remain
unestablished; focused IR cases exercise the transformation.

Native loop unrolling is active, including iteration proofs, cost limits,
secondary exits, nested-loop retries and natural-loop duplication. The latter
methods are mapped to the compiler's unrolling partials and reuse existing
CFG/EH cloning primitives. EH-region duplication remains unexercised by focused
fixtures. `Nested` now matches native's 29 emitted bytes; that does not establish
general loop, dump or metadata parity.

Scalar constant-fold replacements accept the compiler's current node-threading
mode. They do not transfer ownership or links implicitly: assertion application
retains the forward traversal cursor, and statement remorphing rebuilds links.
The unthreaded-source invariant is still enforced when the mode is `None`.

Redundant-branch optimization and its VN relational predicates are active.
The native postorder traversal, retry rules, SSA/phi repairs and edge/profile/EH
updates are retained. DFS/SSA invalidation occurs even on an unchanged pass,
resolving the observed stale managed `SSA MEM:` annotations after assertion
propagation (B281). Complex global-phi execution and full dump parity remain
unestablished.

Loop cloning is active, including candidate discovery, profitability, condition
derivation, guarded fast/slow duplication and static optimizations. Jagged-array
and span candidates copy
descriptor values rather than aliasing mutable descriptors (B286). Jagged-array
copies share the already-populated index/check buffers, as native value copies
do; consumers do not grow those buffers. IR, block and statement references
retain their native identity. EH endpoint updates scan all enclosing clauses,
including different-try entries (B290). Selected array execution exercises cloning
and static bounds-check removal with matching native guards and block structure.
The recorded `SumByLength` removal now occurs in the native phase (B283); broad
candidate, EH and machine-code parity remain unestablished.

CSE candidate discovery/indexing, descriptor local counting, tree eligibility
and availability/dataflow are implemented without activating the CSE phase.
Managed reference arrays replace internal descriptor-pointer tables; both
tables reference the same descriptors and retain native occurrence ordering.
No unmanaged-layout consumer is changed. Native cost initialization, candidate
ordering, use-count unmarking and ordered side-effect extraction are implemented.
Nested CSE definitions remain in the tree and comma VNs retain the native
composition rules. Standard candidate ranking, profitability, selection and SSA
rewriting are also implemented, including native virtual initialization.
Parameterized feature scoring and greedy choice traversal are implemented as well.
Its ordered managed choice list preserves native reverse-stack traversal and
mutable choice references; no extra greedy iteration cap is introduced.
Debug random and replay policies are implemented with the shared native integer
configuration parser. Adjacent minus tokens and the persistent negative sign
retain pinned behavior (B295/B296). Debug RLHook and RL policies also implement
decision replay, feature reporting, stochastic selection and parameter updates.
The production phase now selects and caches the configured native policy, performs
candidate discovery and availability analysis, and rewrites profitable expressions.
Its wrapper clears prior CSE markers on repeated runs. Managed indirection
eligibility uses the typed address accessor instead of native's unary-capable
`GenTreeOp` cast (B298). Matching-input comparisons have not isolated a CSE
algorithm difference. Production loop hoisting now supplies the four missing
range-probe preheader expressions and restores native CSE weighted uses.
The Boolean/switch pre-CSE return-merging differences remain tracked in B300.

Register-state copying accepts ordinary cross-operator replacements and delegates
call/COPY/RELOAD state as native does (B292). Calls still require call sources, and
the COPY/RELOAD helper requires matching operators. Its extra-register copy remains
Unix-AMD64-only; Windows does not acquire invented multi-register behavior.
Windows register/tag tests and existing backend execution pass. Cross-target
compilation blocks Unix execution coverage (B294), and the preexisting non-Windows
call-helper spill-flag discrepancy remains separate (B293).

Boolean optimization is active, including conditional/range folding, compare
chains and native Debug GC-stress bookkeeping. The APX switch-recognition
dependency implements the complete existing detection-only mode; it cannot
accept a conversion-mode request. The separate complete conversion mode is now active.
The positive probe matches native fold counts and pass counts with preserved
results/side effects, but not complete generated code. A conservative Debug
modified-phase status is not evidence that a condition was folded.

Switch recognition and dominant-case peeling are active. Signed-offset chain
conversion, successor multiplicity and profile updates retain native behavior;
the CCMP detection-only entry remains explicitly separate. The positive probe
matches native converted-block decisions and runtime effects, not complete
generated code. Dominant-case peeling is covered by IR tests, including single
evaluation, but has no positive profiled-runtime capture yet. Block-table padding
uses the magnitude of a signed empty-field width, preserving native `printf`
instead of throwing for long targets (B289).

If-conversion is active for Windows x64. Its descriptor retains the native
eligibility, profitability, operand and CFG transformations. The reachability
work stack admits a null merge: equality with the excluded block is checked
before dereferencing the item, after scratch initialization or clearing and
without consuming budget (B287). This preserves native behavior rather than
adding a null-merge shortcut. RISC-V-specific select arithmetic remains explicitly
unsupported; other-target execution and full dump/code parity are not established.
Special-operator evaluation now returns its calculated costs and level rather than
discarding them (B291). The three positive return probes match native emitted
bytes and SELECT/RETURN costs; that is scoped evidence, not general code parity.

VN-based folding and insertion-time statement morphing are also complete
prerequisites. Local-address folds retain the native 16-bit offset invariant;
the native constructor's unsigned parameter does not imply wider field storage.
Arithmetic/cast/bounds application, VN statement propagation and switch-derived
facts preserve native fault conditions, int32 offset peeling and removal cursors.
The complete phase now runs discovery, dataflow, application and outgoing-edge
repair. Local-mode calls retain nullable statement/block contexts; global range
queries establish the required block before use. Selected execution is verified,
but tree-ID and SSA-memory diagnostic differences still prevent full dump parity.

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
Windows-AMD64 lowering supports both native local-lifetime modes. The
lifetime-enabled path runs post-lowering liveness, flowgraph cleanup and any
required second liveness pass before recounting references and invalidating DFS.
Non-Wasm `AfterLowerBlocks` is genuinely empty. The native body remains in the
residual tree for the unported target paths.
Minopts allocation and backend execution are active; optimized register
allocation still explicitly reports `CORJIT_SKIPPED`. Completing lowering does
not establish optimized execution or remove that later boundary.

Liveness template policies use a static-interface generic parameter. The
tracked-local reverse-map array's length supplies its allocation capacity rather
than storing a second size field. Sorting retains native pivot and comparison
order because tolerance-based profile-weight comparisons can make that order
observable; it is not replaced with framework sorting.

The same native sorting implementation is shared with optional assertion
occurrence reports. Their managed file/line map preserves native bucket growth,
collision-chain and rehash order before sorting, since tied counts make that
order observable. Strings and managed nodes replace native-owned key buffers;
host-sized unsigned counts and the signed descending comparator are unchanged.
Native optional-mode initialization and mutation remain unsynchronized.

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

`tupleStyleDump` preserves all native PRE, REFPOS and POST modes using managed
strings and a reference-list index in place of the native character buffer and
iterator. `tupleStyleDumpPre` retains the explicit pre-allocation callsite.
Node sequence numbers retain their native unsigned display through the bit
representation of the existing managed integer field.

Copy/reload insertion updates the existing LIR owning use and places a newly
allocated node immediately after its source, matching the pinned implementation
rather than its stale before-parent description (B200). A second multireg result
reuses the existing copy/reload node.

Spill accounting retains native unsigned counters and normalized temporary
types, using the existing `RegSet` preallocation rather than introducing another
spill allocator. Final local stack-home marking preserves native dependent-field,
reference-count, initialization and frame-pointer predicates.

`resolveRegistersMinimal` specializes native `resolveRegisters<false>`, with
an explicit rejection of enregistered-local mode before mutation. Tree temps
still require full spills at upper-vector save references; local upper-half
save/restore intervals and interblock local-resolution moves cannot arise
after `identifyCandidates<false>`. The full mixed-mode native body is retained.
Resolution reuses existing spill accounting and copy/reload insertion, writes
internal-register masks to the owning codegen table, and finalizes local homes.

`verifyFinalAllocationMinimal` specializes the checked-build final-allocation
replay for the same no-enregistered-local mode. It rejects unsupported local
resolution and resolution blocks before resetting assignments, then preserves
physical-register, copy/move, spill/reload and GC-kill checks. Allocation-table
diagnostics share native block-row formatting, node-location widths and register
name casing; the earlier translation mismatches are corrected, not accepted
differences (B201).

The allocation driver uses the native minimal/full-allocation and local-enabled
construction/resolution predicates, including the no-tracked-locals adjustment.
It retains build/allocation/resolution phase boundaries, statistics, tuple dumps,
completion flags and DFS invalidation.

Post-allocation loop-alignment placement implements both native modes, including
innermost-loop eligibility, call/EH exclusions, normalized weight thresholds and
padding behind retained jumps. Selected optimized and minopts corpora execute
managed-generated code; this does not establish broad execution or code parity.

Code-generation preparation includes complete non-Wasm block-label marking,
native hot/cold jump-elision predicates, emitter `Init`, and GC register/stack
pointer state with live-register protection and native diagnostics.
`EMIT_GENERATE_GCINFO` is enabled in every configuration, correcting B206.
Wasm interval-aware jump elision, non-fixed-register GC register clearing and
non-xarch emitter register names explicitly reject. These support routines do
not activate machine-code generation or complete instruction-group storage,
frame layout, per-tree liveness updates, encoding or runtime metadata.

Instruction-group allocation/initialization/linking and local register-location,
register-mask and birth/death transitions are complete. Groups retain typed
descriptor references; native descriptor-buffer copying and relocation remain
unimplemented pending the storage decision recorded in B207. ARM32 floating
register-variable masks explicitly reject rather than dropping the second
register of a pair.

Morph-time `fgSetOptions` and its frame/GC setters now establish the native
frame-pointer and interruptibility policy before allocation. Register-mask bank
selection, physical-register availability aliases and block-boundary constant
reset defects found during activation are corrected (B202-B205), not accepted
output differences. Allocation activation initially matched seventeen of twenty
minopts corpus methods. Complete Windows-AMD64 `fgCreateFunclets` subsequently
removed both EH-order mismatches; nineteen allocation-phase bodies now match,
with hardware import remaining.

Funclet creation retains native handler relocation, shared-try clause ordering,
filter-before-handler indices and loop-head prolog insertion. Compiler-owned
managed arrays replace the unused descriptor and clause-map pointers under D002;
ref-returning accessors retain descriptor identity. Only the common function
descriptor metadata is needed here; unwind/location payloads and final emission
remain unported. All twenty funclet phase bodies and resulting graphs match
native. The full sections including post-phase checks retain two B135 diagnostic
differences, which are not accepted output exceptions.

Internal-register definitions, call definitions and kill references retain
native location ordering, register preferences and upper-vector save rules.
Write-barrier classification is exposed through `ICodeGen`, matching the native
codegen interface rather than requiring a concrete implementation. The compiler
owns the shared GC-reference kill query.

Local candidate selection now implements native eligibility, EH exposure,
FP preference sets and local upper-vector intervals for Windows AMD64. Its
production callsite remains gated until optimized interval construction,
allocation and resolution are complete. There is no separate kill-state
initialization phase or second candidate scan. The minopts specialization does
not produce those local sets; upper-vector saves for temporary values remain
supported.

Initial parameter definitions and live-in/finally-local zero definitions follow
native register preferences, stress predicates, OSR initialization and spill
rules.
The complete Windows-AMD64 `buildIntervals<true>` specialization is implemented
as `buildIntervalsWithLocals`, alongside the unchanged minopts builder. It
includes predecessor selection, parameter stress and local interval validation.
The full Windows-AMD64 register selector and assignment/copy primitives are
implemented alongside the unchanged minimal versions. They reuse the native
heuristic sequence and preserve fixed-register conflicts in both register-mask
banks, related-interval horizons, spill costs and constant identity. The unused
native reverse-selection local does not reorder heuristics.
Windows-AMD64 block-location processing includes native allocation and resolution
map semantics, register reassignment, EH write-through homes and dead candidates.
The complete `processBlockEndAllocation<true>` specialization is named
`processBlockEndAllocationWithLocals`. These entrypoints reject unsupported
targets or disabled local enregistration before mutating maps or visitation.
The complete Windows-AMD64 `resolveRegisters<true>` specialization is now
`resolveRegistersWithLocals`. It replays entry and block references, inserts
upper-vector operations, resolves edges and finalizes local homes before native
final-allocation verification and stack/spill accounting. The local interval
array is required in this mode, not treated as an empty map when absent.
The complete allocation traversal is now dispatched under the native
`enregisterLocalVars || OptimizationEnabled` predicate. Local-enabled building
and resolution use only `enregisterLocalVars`; the minimal paths are unchanged.
Production optimized compilation now passes allocation and loop placement,
emits native code and executes the selected standard and GC-loop corpora.
The spill-weight assertion uses the native interval-building cursor rather than
the allocation traversal cursor (B264). This is a translation correction, not
an accepted algorithmic deviation. Full dump/code parity remains unestablished.

Loop discovery and canonicalization now preserve native preheader,
backedge, exit and EH-header ordering, rebuilding cached DFS and loop data when
required. Block weighting consumes that loop state rather than substituting
lazy discovery for the native loop phase. Profile repair and other unported
loop optimizations remain separate limitations. Loop inversion and its complete
iteration-analysis prerequisite now preserve native eligibility, cost/profile
decisions and CFG updates. Cloned statements use `gtNewStmt` plus insertion,
matching native `fgNewStmtAtEnd` without premature cost preparation or threading.
The downstream `NaturalLoopIterInfo.ArrLenLimit` cloning accessor remains
unported; it is not required by iteration analysis or inversion.

Liveness policies use static interface members in place of native template
traits. Per-block scratch and stored sets retain independent managed storage.
The early-tree specialization preserves native conditional-definition handling,
dead-store side effects and the null current-statement cursor. Other tree/SSA
policies remain explicitly rejected by this driver.
The separate SSA-policy driver includes memory liveness, non-phi backward
traversal and native dead-store diagnostics. Interior COMMA replacements use
metadata-preserving constructors and update every owning edge before
rethreading; NOP conversion uses the existing base-field-only transformation.
The full SSA builder now inserts and renames local and memory PHIs, including
EH propagation and shared GC-heap/byref state, in native traversal order.
Its phase driver preserves liveness, zero-init cleanup and deep-rebuild order.
The general `fgDebugCheckLinks` closure now preserves native tree flag/type and
link checks, LIR use/definition and local-semantic checks, followed by SSA
verification at CHECK_IR. Relaxed extra-flag notices remain visible and counted.
The hardware-import-divergent `FoldHardware` input has additional `GTF_CALL`
notices; these are not accepted diagnostic differences or evidence of a checker
defect on identical IR. Full diagnostic parity remains unestablished.
The SSA verifier's tuple-keyed managed
dictionary is queried in native local/SSA descriptor order, not enumerated,
so hash layout does not affect notice or failure ordering.
At the end of local-list maintenance, managed code detaches obsolete local
links and statement list heads before entering `NodeThreading.None`. This
retires cached ownership under D002 without weakening replacement checks or
changing the IR, logical IDs or early-liveness flags.
SSA reset supports both native PHI-only and deep-clean modes. Memory-SSA maps
are nullable, reflecting the native absent-map state. Deep reset retains the
definition-array and composite-list storage for reuse while dropping map
references; it does not mutate aliased dictionaries or removed PHI nodes.
Composite SSA numbers retain native compact packing and outlined index/alias
semantics. New outlined slots are explicitly zeroed after list growth, including
reuse after reset, matching `JitExpandArray` value initialization. A readonly
managed singleton represents native memory-PHI sentinel `0x1`; null continues
to mean that no PHI exists.
SSA rename state uses managed linked stack nodes and the native free-node pool.
Per-local history and the cross-stack block-pop list remain distinct; memory
stacks stay independent, with shared-kind selection left to the builder.
SSA annotation labels retain native pointer-width identity while diagnostics
truncate them to signed 32-bit values at the native formatting sites.
Dominator trees use managed child/sibling and numbering arrays while immediate
dominators remain on the blocks. Rebuilding clears both exceptional and
dominance-predecessor caches; those caches remain separate. Frontier lists keep
native postorder insertion and iterated-frontier discovery order.
`IDomTreeVisitor<TSelf>` uses constrained struct callbacks and native
child/sibling/parent traversal. All hooks are explicit, including empty hooks,
to avoid boxing through default interface implementations. Walking an existing
tree requires neither recursion nor allocation.
The enclosing EH-region helper takes a managed `ref bool`, preserving the
native in/out value when no enclosing region exists. Output-only wrappers
remain valid only where callers consume the kind for a present region; EH
insertion and normalization retain the value across outward walks.
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

Emitter locations pair a typed group reference with the unchanged native
instruction/estimated-offset cookie. Final offset lookup walks saved descriptor
objects when instruction sizes change; descriptor-storage sizes are not machine
code sizes. Variable live ranges use managed ordered storage with native
coalescing and separate prolog/body collections. Variable homes retain the
existing `ICorDebugInfo.VarLoc` EE layout. These representation changes do not
authorize different debug ranges or generated offsets.

Stack-local descriptor addresses retain the native packed 32-bit representation
and tagged limits. The descriptor exposes its managed address storage by
reference; adding the local-address variant does not change its native logical
size or pretend that the remaining union variants are implemented. Instruction
format storage retains the native seven-bit width; generated operand,
scheduling and update-mode tables use the pinned header inputs.

Outgoing EH clauses retain a stable descriptor-table index instead of a native
descriptor pointer. VM-order sorting preserves that identity for same-try
classification; unmanaged clause payloads are pinned only during publication.
GC bitstream storage uses ordered managed 128-byte blocks instead of native
linked allocations, preserving least-significant-bit-first 64-bit packing,
variable-length encodings and the exact byte count.

GC encoding uses managed slot, transition and bit-vector storage while retaining
native slot IDs, sorting, compression and exact encoded bytes. The encoder owns
its bitstream writers through `IDisposable`; emission releases scratch storage
without taking ownership of the EE's allocated GC-info buffer.

Async debug publication copies records into EE-owned arrays and transfers them
at the callback. Subsequent diagnostics read the original managed records,
preserving native values and ordering without relying on transferred storage
remaining readable (B239).

The generation driver owns stack-local output storage for its synchronous phase
calls. It copies successful results to the caller and clears borrowed emitter
output addresses on both success and failure; EE code and metadata buffers
remain runtime-owned.

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

`RegSet` initializes Swift callee-saved masks for AMD64 and ARM64 using their
target masks. Other Swift targets report NYI and then terminate with
`fatal(CORJIT_IMPLLIMITATION)`. The native constructor remains in the residual tree.

The SysV x64 classifier, multireg return helpers and Swift argument/special-parameter
helpers have Linux-target unit coverage on a Windows host. This does not establish
Linux runtime support. Swift parameter classification in `lvaClassifyParameterAbi`
still terminates with `CORJIT_SKIPPED`. SysV call recording now includes the second
return register's GC type and native helper kill masks. The large-call descriptor
accounts for the shared GC-type/async bitfield allocation and retains the native
72-byte AMD64 logical size. Call instruction/helper dispatch, current-GC handoff,
instruction output and final emission have managed SysV coverage; broader Linux
backend activation and generated-code execution remain separate.

SysV AMD64 frame allocation/probing, callee saves, stack initialization,
root/OSR epilogs, funclet frames and CoreCLR unwind metadata are implemented.
Register/stack parameter homing, SIMD segment insertion, generic-context
reporting and OSR Tier0 local loading are implemented. Final frame layout,
`genFinalizeFrame`, root prologs and reserved prolog/epilog materialization
are implemented, including Vector3 upper-lane clearing before parameter homing.
Unix varargs remain unsupported, matching `compFeatureVarArg`; root entries
reject them before changing instruction/unwind state. General machine-code
generation/emission orchestration and final metadata publication remain
Windows-gated.
SysV return/exit helpers now cover scalar and multireg values, SIMD splitting,
Swift lowered offsets/error returns, async continuations, return GC roots,
cookie checks and epilog reservation. Call orchestration and argument placement
remain separate from the previously completed call-instruction emitter.
Unix AMD64 with `CORINFO_NATIVEAOT_ABI` requires CFI unwind metadata, which
remains unported (B395). All public unwind recording, reservation and publication
entries terminate with `CORJIT_SKIPPED` before changing unwind state in that
mode, rather than substituting the CoreCLR format. Eleven rejection cases
cover these boundaries in both Debug and Release.

ARM64 target metadata, ABI classification, immediate predicates, fixed-width
SIMD/mask queries, cross-platform intrinsic importing, FP/LR placement policy
and two-register GC return layouts now compile and have managed unit coverage
for Linux, Windows and Apple ABI variants on a Windows-x64 host. There is no
verified ARM64 runtime host or generated-code parity result.

ARM64 memory-address formation and load/store containment now preserve native
natural scales, RCPC2 volatile-offset limits, extended-index containment,
stack-probe/SIMD12 restrictions and integer-zero stores. These helpers have
managed target coverage. Pair reordering and loop store-to-load-forwarding
checks are also implemented, preserving native distance/budget limits,
alias checks and dataflow cleanup. Private load/store lowering now integrates
these helpers with volatile floating bitcasts, positive-zero store retyping,
mutable-object release stores and ARM64 coalescing/atomicity. Block traversal
resets per-block pairing candidates; phase activation remains separate. B380 tracks the
pinned volatile-load bitcast round trip without changing it.

ARM64 hardware-intrinsic and mask rewrites now have a private node-dispatch path
for inserted operations, including FFR stores and reloads after calls. Fixed-width
true-mask construction uses the native predicate pattern. Scalable vector constants
now have separate storage, factories, queries, cloning, debug hashing and LSRA
temporary selection and constant dumps. Raw integral element lookup and
representability-aware unary folding are also implemented; scalable masks and
VN/assertion storage remain explicit limitations. `LowerBlock` initializes
FFR-trashed state, and switch lowering supports ARM64 bit tests and jump tables.
`DoPhase`, allocation and emission remain separate. Managed target tests do not establish
ARM64 execution or generated-code parity.

Non-AMD64 `Emitter.RequireSupportedInstructionRecording`, `emitCheckIGList`
(Debug), `emitGCregDeadUpdMask`, and `CodeGen.inst_ST_RV` terminate with
`CORJIT_SKIPPED`; their instruction-recording, group-validation, GC-output and
spill-store bodies remain unported. Unhandled-instruction performance diagnostics
also terminate outside AMD64 in Debug; Release retains the native default costs.
ARM64 unknown-size frame metadata and address printing now support scalable
vectors. Scalable vector/mask constant queries remain explicitly unsupported
rather than reading fixed-width storage; the shared frame-location padding
discrepancy is tracked as B376.

Xarch local address/load/store callers retain the x86 SIMD12 local-load path
and 32-bit long-store dispatch. `CodeGen.genStoreLongLclVar(GenTreeLclVar)`,
guarded by `TARGET_XARCH && !TARGET_64BIT`, terminates with `CORJIT_SKIPPED`;
its long-store implementation remains in native `codegenlinear.cpp`. Completing
these callers does not activate x86 emission or complete that shared helper.

Shared `genRecordAsyncResume`, `genEmitAsyncResumeInfoTable`,
`genEmitAsyncResumeInfo` and `Emitter.emitAsyncResumeTable` retain the complete
native metadata algorithms without their former Windows-AMD64 whole-body gates.
Their native definitions and completed declarations are removed. The two-pointer
EE record layout, pointer alignment, singleton registration, location capture,
section linking and resumption-stub cache are unchanged. Other-target
instruction recording still terminates through its separate support boundary.
Shared final data-section output/display preserve native ARM, Wasm and
target-width branches, writable-alias stores, relocation ordering and async
null-location handling. Their native definitions and declarations, and the
newly shared existing inline hot/cold offset helper, are removed. Native
`emitLocation` diagnostic geometry remains a host pointer plus an aligned
unsigned field; the EE record layout is not redesigned.
Shared non-Wasm suspension/continuation transfers and async debug publication
retain their complete native algorithms. Five non-AMD64 continuation-register
aliases now match the pinned target headers. Their three common definitions and
the publication declaration are removed; native Wasm-specific transfer bodies
and their shared declarations remain. Instruction emission keeps its separate
terminating target boundaries; other-target patchpoint execution remains blocked.
The publication fixture is still Windows-only;
these shared helpers do not establish Linux publication coverage, other-target
instruction emission or runtime/generated-code parity.

Shared `genReturn`, x86/ARM32 `genLongReturn` and non-Wasm
`genMarkReturnGCInfo` retain their complete native caller algorithms; their
definitions and declarations are removed. ARM soft-float/varargs moves use the
native unsigned instruction-flags enum and an explicitly selected flags-typed
three-register recording boundary. Target-specific `genSimpleReturn`, Wasm
`genClearAsyncContinuationGlobal` and ARM recording dependencies terminate;
their native implementations remain. Linux profiler controls retain their
existing failures. Other-target diagnostic builds still fail, so this caller closure does
not establish complete compilability, GC-header publication or execution there.

Shared non-Wasm patchpoint generation retains regular/forced helper selection,
target jumps and non-xarch tail-call state. Its whole common body/declaration and
the two native inline state accessors are removed. `CodeGen.HasTailCalls` retains
native target guards and false initialization, but modern GC-header publication
still terminates outside Windows AMD64; the state is not yet emitted into other
targets' GC metadata. Native GC/header/encoder implementations remain, and their
tail-call bit is not replaced by AMD64's `WantsReportOnlyLeaf`.
ARM unary recording has its native three-argument terminating declaration.
LoongArch/RISC-V's five-argument `nint` NYI binds only the caller's omitted-options
arity (`INS_OPTS_NONE=0`), not the native six-parameter emitter API or target-wide
options enum. Those native emitters remain untranslated.

Shared struct-return classification now preserves field-list precedence and the
Windows-AMD64-only policy, using the native struct-type predicate elsewhere,
including Wasm. Non-Wasm generation retains the whole common algorithm,
including `FEATURE_SIMD`, Swift offsets and LoongArch/RISC-V descriptor field
offsets. Existing signed offsets are converted bit-preservingly for native
unsigned addition/comparison, then converted back at the emitter boundary;
descriptor representation and ABI classification are unchanged.
The two common definitions and classification declaration are removed.
Wasm-specific generation and its shared declaration remain, as do independent
native emitter bodies. The new five-argument
ARM32/LoongArch/RISC-V local-stack NYI is a terminating call-arity boundary,
not a translation of ARM's optional sixth base-register argument or full target
recording. Failed target builds can mask body diagnostics and do not establish
complete compilability or execution.

ARMARCH SIMD splitting, the three ARM64/LoongArch/RISC-V simple-return bodies,
Swift error returns and xarch DEBUG stack-pointer checks now retain their whole
native control flow. Their six definitions and four declarations are removed
together. ARM64 uses existing recording APIs, including its void move helper.
LoongArch/RISC-V float-return aliases match native F0/FA0; new two/three-register
declarations terminate and bind only the caller arity, not the optional
`insOpts` or full native emitter API. Those independent emitters remain.
The nineteen retained Linux profiler failures are preexisting register and
frame-delta assertions, not newly exposed NYIs. Failed target builds can still
mask later body diagnostics; this closure does not establish runtime parity.

Xarch callee-save push/pop, floating preservation/restoration, root epilog and
AVX-clearing callers now include their native x86 paths. Caller-entry recording
checks remain AMD64-only so x86 empty/no-op paths do not fail before reaching
their native predicates; unsupported independent recording helpers still
terminate when called. `DOUBLE_ALIGN` state/readback is represented through
`CodeGen`, `ICodeGen`, `Compiler` and the existing LSRA policy. The five complete
native inline accessors, including both `doubleAlignOrFramePointerUsed` variants,
are retired; native fields and guards remain. Shared push/pop/root declarations
remain for other-target definitions. X86 compilation still fails before full
body checking, and new x86 controls remain unexecuted; funclets and general
frame/GC encoding are separate work.

The xarch node dispatcher and profiling callbacks retain their x86 and SysV
control flow. Stack-level addition is shared across targets; single-push
accounting retains the native non-Wasm guard, and nested-alignment accounting
remains Unix x86-only. Native fields and guards remain after retiring completed
helper definitions. Their managed mapping is `CodeGen.ProfilingDependencies.cs`.
The common `genCodeForReuseVal` now retains its native integral-zero
instruction-group boundary across targets; the previously failing SysV
dispatcher case passes in both configurations. The Swift-error generator and
x86 helper-call recording primitive also remain typed terminating dependencies.
The failed x86 declaration build
does not establish compilation or execution of the restored caller bodies.

Xarch funclet and block-initialization callers now retain the separate native
x86 paths. X86 unwind operations remain independent typed terminating helpers;
AMD64 funclet layout and algorithms are unchanged. Call generation and call
instruction selection retain x86 stack adjustment, floating-return spill,
virtual-stub/PInvoke registers and Unix alignment accounting. The three
unsigned alignment immediates convert explicitly to the native signed-width
instruction boundary. X86 excludes `FEATURE_FASTTAILCALL`, so the fast-tail
epilog callsite does not impose an extra-argument-zero contract there.
The shared non-Wasm pending-call-label definition and exclusive declaration are
retired; pending-label state remains native. Four Unix x86 CallArgs inline
definitions retire without restoring fields already absent from the residual.
The exact fourteen earlier x86 declaration errors still prevent full body
checking; floating spill, unwind and recording dependencies remain untranslated.
Windows frame/call controls do not establish x86 runtime or generated-code parity.

Xarch register/stack argument placement now retains the complete x86 push,
field-list and struct-copy callers, with fixed native integer/floating masks.
SysV incoming stack selection reads the existing parameter ABI segments.
The shared non-Wasm register-placement caller preserves target branches and
Windows varargs duplication. Its definition, the xarch-only definitions and
their completed exclusive declarations are retired; shared declarations remain
for independent target definitions. `getFirstArgWithStackSlot` and the non-x86
field-list definition remain native because their other-target bodies are
incomplete.

The common catch-argument caller preserves the handler predicate, incoming
exception GC root, move, conditional root clearing and produce order on every
target. Its exception-register aliases match the pinned headers; Wasm keeps
the native never-dispatched common path instead of fabricating catch support.
Existing ARM32/LoongArch/RISC-V move bindings terminate at their independent
recording dependency. New optional overloads would duplicate or ambiguously
bind those calls, so no additional emitter move API is introduced.
The x86 diagnostic still stops at fourteen earlier declaration errors; neither
its caller bodies nor the new x86 controls have execution evidence.

Common `inst_RV` and `inst_RV_IV` retain native sizing and target dispatch,
including target32 immediate truncation and AMD64 zero-extended MOV selection.
LoongArch/RISC-V retain the upstream unused-path NYI diagnostics followed by
explicit termination if `AltJitAssertOnNYI` permits those diagnostics to return.
The ARM32 immediate validator and register-immediate materializer remain typed
terminating dependencies; their native bodies and declarations remain.
The adjacent register/register adapter is unchanged and remains native.

Shared tail-jump placement retains the native non-Wasm scope,
spill/profiler/reload order, GC-root transitions and unchanged descriptor homes.
Live-register masks are constructed in the actual register bank; a floating-home
control checks that removing XMM2 does not clear the overlapping integer RDX bit
and that reloading XMM0 restores the floating bank. X86 varargs require no
register placement; Windows AMD64 restores unknown shadow-space bits inside the
native GC-disabled interval. Other-target varargs bodies and the common
parameter-stack type helper remain independently incomplete.
The retained x86 declaration failures still prevent execution of its new
adapter/tail-jump controls. No non-xarch recording or generated-code parity is
claimed by the selected Windows/Linux checks.

Common local and spill-store adapters retain native DEBUG spilled/write-through
assertions, local bounds, actual-type widths and negative temporary numbers.
ARM64 retains its SVE store-classification exception. The stack-only adapter is
xarch-wide, not x86-only; its unsigned offset converts unchecked to the signed
emitter offset, preserving the native bit pattern.
CPU-load/store classification and missing target recording arities are typed
terminating dependencies, not successful stores. Their independent native
definitions remain. The selected sixteen-case Windows/Linux controls do not
establish other-target or generated-code parity; fourteen earlier x86
declaration errors still prevent execution.

`LinearScan.setFrameType` implements AMD64 and ARM64 frame selection. ARM64
performs conservative frame layout before reserving IP1 and reserves x19 when
the layout contains scalable vectors. The layout includes varargs homes,
FP/LR relocation, Apple NativeAOT frame placement, OSR boundaries, local/temp
alignment and unknown-size frame initialization/finalization. Other targets
retain their explicit unsupported frame-selection paths.

Pinned native `ValueSize::FromJitType` still treats masks as exact eight-byte
values, not scalable values. B377 corrects the managed mismatch. The native
unknown-frame mask-block allocator is ported and tested separately without
activating scalable mask locals or temps. Neither these layout helpers nor
their managed target tests activate ARM64 lowering, allocation or stack-space
emission.

ARM64 allocator construction now initializes integer, floating/vector and
predicate banks, reserved-register exclusions, EnC restrictions and caller-save
sets. Patchpoints do not apply the AMD64-only floating-register restriction.
Managed Linux-ARM64 target tests cover construction and interval preferences;
this does not activate ARM64 register allocation or establish runtime parity.

Consecutive-register candidate filtering, spill ranking, sequence assignment
and full-allocation traversal now have managed ARM64 coverage. Fixed-reference
conflicts remain excluded during stress recovery, and partial-vector restores
retain their ARM64 allocation behavior. Local-interval construction, resolution
and minimal-path consecutive allocation remain gated, as does the public
allocation phase. B398 records preserved native wrap-mask quirks; there is no
ARM64 execution/parity claim.

`Lowering.IsCallTargetInRange` implements the xarch policy. Other targets report
NYI and terminate with `fatal(CORJIT_IMPLLIMITATION)` pending their call-target
range checks; the shared direct-call lowering body remains in the native tree.

`Lowering.IsContainableImmed` implements xarch and ARM64 instruction-specific
immediate and relocation rules, including the Windows NativeAOT section-offset
exception. ARM64 compound containment covers multiply, shift, rotate, negate
and cast operands with native overflow, flag-setting and interference checks.
Shift bounds use the operand width even for INT-valued comparisons of LONGs;
rotate normalization retains native mutation before containment rejection.
Binary and unary containment preserve cast/load-extension precedence and the
minopts negated-multiply exception. Other targets retain explicit NYIs.
Private ARM64 `LowerBinaryArithmetic` now includes NOT combinations, CCMP
chaining, bitfield extraction and widening-multiply subtraction. Its dependency
layer also supports widening-multiply addition, condition-to-flags chain movement,
ARM64 condition descriptors and truthifying flags. The BFX path preserves the
native empty containment action without activating general containment dispatch.
Private ADD/MUL/NEG and shift/rotate entrypoints now include the complete
widening-multiply overflow proof, negated-multiply containment, multiply-long
fusion and extended-shift recognition. Rotate count conversion retains the native
unmasked subtraction; redundant mask removal remains a separate dispatch action.
General hardware-intrinsic lowering remains separate; these helpers do not
activate node/block/phase dispatch or establish runtime parity.
Private comparison, branch and select lowering now implements the native ARM64
byte-mask tests, signed-bit direct branches and conditional negate/invert/increment
transformations. JTRUE-to-JCMP/JTEST and SELECT-to-SELECTCC use whole managed
node-class replacement, preserving owning edges and identity. B381 records the
pinned non-CC increment path's downstream inconsistency; it remains unchanged
pending native execution evidence.
Private casts, constant signed/unsigned division and power-of-two remainder now
preserve ARM64 load-width restrictions, native magic-number selection and
conditional-negate ownership. Their temporary-local dependencies include
local-store target paths, odd-size call-result spilling and post-indexed
pointer-update scheduling. Shared containment traversal includes ARM64 hardware
intrinsics. Private block-memory lowering
now preserves zero-register initialization, atomic GC-zeroing loops,
non-interruptible small stack copies and checked unrolled-address bounds.
Local-to-block stores use the extracted native block-store action; small
copies and single-register call stores preserve whole-node replacement.
Large-copy/helper and GC decomposition paths still require the inactive
ARM64 general dispatcher. These private actions do not activate the phase
or establish generated-code parity.
Private ARM64 return and stack-argument entrypoints now preserve SIMD/HFA
normalization, primitive register-class bitcasts and struct-local return types.
Field-list spills use private local-store/load actions; register repacking that
needs `LowerRange` still reaches the inactive general dispatcher. Return
containment selects the native return-value operand, including Swift's second
operand rather than its error value.
Private ARM64 call entrypoints now preserve the VM's branch-relocation stub
policy, empty call containment, ordinary argument placement and single-register
HFA/struct result normalization. Windows split arguments preserve ABI segments,
early/late links and local/field-list ownership, including spills when fields
overlap the register/stack boundary. The complete block-indirection split source
still needs inactive `LowerRange`; it is not an execution-supported path.
Private PInvoke frame/call transitions and fast-tailcall lowering now support
ARM64, including inline/helper GC transitions, Swift error-consumer adjacency,
defensive copies and profiler hooks. CFG uses ARM64's validate-by-default policy
and xarch-only constant cloning; its validate-and-call path still needs general
`LowerRange`, so full CFG/phase activation remains separate. SysV x64 now
supports common local-store lowering and irregular single-register struct-call
result spilling through the existing xarch block-store paths. Its fast-tailcall
fixture is linked separately from the ARM64-only PInvoke and CFG fixtures;
managed target coverage does not establish Linux generated-code execution.

ARM64 hardware containment preserves immediate positions and paired-immediate
constraints, signed-only ordered comparisons with zero, MOVI/FMOV constant
selection and SVE embedded-mask ownership. SVE conversion auxiliary widths and
pairwise all-true-mask exceptions retain native behavior. Bounds containment
prefers the index immediate and does not use xarch memory/register-optional
forms. Hardware/mask rewrites and node/block traversal are implemented separately;
these managed checks do not establish phase activation or ARM64 execution parity.

`Lowering.TryCreateAddrMode` implements xarch address folding and interference
checks. Other targets throw `NotImplementedException` pending their volatile,
scaled-index and explicit-address-add handling.

`Lowering.LowerIndir`, `ContainCheckIndir` and `LowerPutArgStk` implement the
xarch bodies. Other targets throw `NotImplementedException` pending their
target-specific indirection containment and argument placement rules.

`Lowering.ContainCheckCast` and `ContainCheckBinary` implement xarch and ARM64
containment; other targets throw `NotImplementedException`. Xarch read-modify-write
recognition retains native status caching, whole-address interference checks
and temporary LIR marks. These helpers do not activate arithmetic or cast
lowering, including floating conversion expansion and optimized transforms.

`LinearScan.calleeSaveRegs` supports AMD64 and ARM64 using the `lsra.h`/`typelist.h`
register-bank mappings. Other targets report NYI
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

`LinearScan.writeRegisters` and `insertCopyOrReload` currently support Windows
AMD64, explicitly rejecting other target/ABI configurations. `Compiler.raMarkStkVars`
likewise rejects non-AMD64 targets; their double-alignment frame policy remains
unported. These routines do not activate register resolution or emission.

`LinearScan.resolveRegistersMinimal` and the generic
`GenTree.SetRegSpillFlagByIdx` dispatcher explicitly reject non-Windows-AMD64
targets. Indexed call-result spill storage is not available in this ABI;
hardware and scalar-local multireg flags use their existing packed storage.

Current target-sync additions under `TARGET_WASM` are
`Compiler.fgWasmRepairTryEntries` and `Compiler.fgWasmSpillRefs` in
`Compiler.fg.cs`. They throw `NotImplementedException` rather than returning a
successful phase status. Try-entry repair and GC-reference spilling remain
unported; no Wasm execution support is claimed.

The parameterless `CLRRandom.Init` in `sources/Core/inc/random/CLRRandom.cs`
currently supports only Windows, using the native performance-counter, OS-thread,
and process inputs. It explicitly throws on other hosts. Explicitly seeded
initialization is portable; no unseeded cross-host sequence equivalence is claimed.

`GenTreeVecCon.Equals` supports `TARGET_ARM64`'s `TYP_SIMD` scalable payload,
including native cross-kind/type zero equality and otherwise exact operand bits.
`GenTreeMskCon.Equals` still defers the scalable-mask branch selected by
`TARGET_ARM64 && DEBUG` and `JitUseScalableVectorT`, reporting NYI followed by
the nonreturning `fatal(CORJIT_IMPLLIMITATION)` path. Fixed-width and scalable-vector
comparisons have managed target coverage, not ARM64 generated-code execution.

`Compiler.gtNewConWithPattern` implements scalar, fixed-width and ARM64 scalable
vector byte patterns. Scalable zero/all-ones factories and cloning likewise
preserve the separate payload. Fixed-byte access and unsupported element/binary
consumers reject scalable values instead of treating them as SIMD16. Native's
ordinary GetElement/SetElement and EvaluateBroadcastInPlace switches also omit
`TYP_SIMD`; those gates are not missing scalable translations.

`Compiler.gtFoldExpr` retains the native `FEATURE_HW_INTRINSICS` dispatch.
The whole `gtFoldExprHWIntrinsic` body now preserves its xarch, ARM64 and masked
branches, including scalar/vector constants, mask conversion and conditional
selection. Both the dispatcher and folding helper are retired from the residual.
ARM64 `HWIntrinsicInfo.GetMaskVariant` and `Compiler.NarrowAndDuplicateSimdLong`
are implemented and retired, including the complete mask mapping and both SIMD
widths with native saturation/duplication behavior. Target fixtures establish
selected folds, not ARM64 generated-code execution.

`GenTreeHWIntrinsic.GetLayout` now preserves its complete fixed and SVE aggregate
layout dispatch and is retired. SVE cases call the typed, terminating
`Compiler.getRuntimeVectorTByteLength` dependency; that native helper remains.
The fixed-size cases and SVE dependency boundary have managed target coverage.

`CallArgs.AddFinalArgsAndDetermineAbiInfo` implements outgoing argument
classification and non-standard argument insertion for Windows x64. Its Wasm
branch throws `NotImplementedException` pending shadow-stack argument insertion;
the complete native body/declaration remain available in the residual tree.
Unix varargs also throw explicitly, matching the native unsupported path rather
than continuing with an unsupported ABI. Other target-specific register rules
are source-ported, not execution-validated. Classification is not argument
evaluation/scheduling and does not activate call morphing.

`Compiler.gtHashValue` includes ARM64 scalable kind, base type and index/step
words in native order. Scalable-vector `gtDispConst` implements sequence/scalar
element arithmetic and native alternate-form floating rendering using the shared
formatter. Exact managed constant-dump cases are covered; complete ARM64 phase
dump and generated-code parity remain unverified.

`Rationalizer.RewriteHWIntrinsic`, scalar intrinsic handling in `RewriteNode`,
and `RewriteSubLshDiv` explicitly throw for their unported ARM64 mask-reduction
and arithmetic rewrites. Non-xarch constant/variable shuffle factories and
target-specific hardware immediate/MSB rationalization paths likewise throw.
`GenTree.GetIntegralVectorConstElement` supports ARM64 scalable payloads using
the native raw 64-bit sequence arithmetic, without narrow-element truncation or
signed extension. `TryEvaluateUnaryInPlace` folds representable scalable results
and leaves the tree unchanged otherwise.
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

### D005: Instruction recording without optional disassembly

**Status:** closed for Windows AMD64; not an accepted diagnostic difference.

Debug `opts.dspCode` now uses the native immediate instruction printer rather
than rejecting compilation. `dispIns` preserves sanity checking, then prints
before the remaining stack-depth, logical-size and conditional statistics checks
(`emit.cpp:1611`). Release retains native omission of this Debug call.

Instruction-group diagnostics walk saved descriptors at their final offsets,
including native handling of removed terminal jumps; group-list display forwards
the instruction-selection flag (`emit.cpp:4099-4330`). Ordinary verbose JitDump,
which also enables `dspCode`, no longer hits the old pre-recording rejection.
Recording tests exercise diagnostics together with descriptor, GC, ownership and
move-elision behavior. CoreDisTools late disassembly is a distinct mode (D009).

### D006: Shared throw-helper blocks before inline helper calls

**Status:** closed for Windows AMD64; not an accepted output difference.

`genJumpToThrowHlpBlk` now implements both native `fgUseThrowHelperBlocks()`
arms (`codegencommon.cpp:2000-2069`). Shared blocks retain explicit targets,
exception-target lookup and Debug consistency checks. Inline throws reverse
conditional jumps around the selected runtime helper, then define the normal
continuation with its GC roots restored. Unconditional throws emit no redundant
branch or continuation label.

Arithmetic, multiplication, bounds checks, indexed addresses, integer casts and
finite checks use the complete function. Their former shared-only restrictions
are removed. Shared-block construction and inline calls use the same native
`acdHelper` mapping. Immediate instruction disassembly is available (D005);
other targets explicitly reject throw generation. Windows-AMD64 production
emission is now active for the supported backend modes.

### D007: Optional emitter-test injection

**Status:** resolved for Windows AMD64; other-target payloads remain an explicit
implementation boundary, not an accepted output difference.

Windows-AMD64 Debug block generation now invokes the native dispatcher at the
last block, after GC-root checks. Method matching, null-section rejection,
case-sensitive substring selection, section order, branch-over wrapper and
diagnostics follow native. All six AMD64 payloads are implemented, including
their ISA/encoding guards; Release does not inject payloads.

The selected SSE2 capture completes with the primary native and managed JITs:
all 41 instructions, 146-byte size, branch target and import/morph/cost trees
match. An explicitly enabled recording fixture reaches all six sections without
executing their synthetic instructions. This does not establish encoding parity
for the other five sections. Native `all` with default encoding settings asserts
at `UsePromotedEVEXEncoding()`; the port does not bypass that configuration
requirement. Evidence is recorded in `checkpoint.amd64EmitterPayloads`.

### D008: Optional CSE emission metrics

**Status:** resolved implementation boundary; no accepted output difference.

Windows-AMD64 `genEmitMachineCode` now implements the native Debug `opts.dspMetrics`
path, including metrics-only versus code-summary framing, method-local CSE counts,
policy-specific metrics, optional SPMI index and method identity. It constructs
the configured policy when necessary rather than substituting empty metrics.
Output flushing and native-code-size publication retain their native ordering.
Ordinary emission and the separate late-disassembly boundary are unchanged.

### D009: Final metadata without optional late disassembly

**Status:** temporary implementation boundary along the existing
`opts.doLateDisasm` predicate, not an accepted output difference.

Requested `JitLateDisasm` terminates with `CORJIT_SKIPPED` before generation,
emission allocation or final metadata publication. Ordinary emitter
`jitdisasm` and immediate instruction diagnostics are separate from this mode.

CoreDisTools' buffered interface matches the native printer for the tested valid
instructions, but sends decoder errors to stderr instead of returning the text
that RyuJIT's callbacks write to the selected output stream. The native callbacks
are variadic and cannot be supplied directly by `UnmanagedCallersOnly`. A native
callback bridge or another exact ABI solution needs approval; the buffered draft
is not used as a success-shaped approximation (B241).

## Implementation notes and parity findings

### R001: Temporary serialization for debugging

**Status:** removed after shared-state review and primary-JIT parallel/reentrant
execution. `CILJit.compileMethod` now follows the native entry without a global
compilation lock; this was a debugging aid, not a required execution policy.

B442 synchronizes the shared diagnostic writer's character buffers, raw
inline-name bytes and LSRA CSV operations while preserving native text-mode
semantics. B443 removes the Debug TLS finalizer, which incorrectly restored a
terminated worker's outer compiler on the finalizer thread. Deterministic scope
disposal preserves native stack lifetime; normal and exceptional nested scopes
are covered on four threads.

Timing CSV, timing summaries, inline XML and replay reads retain their existing
locks. Lazy configuration lists, component-test initialization and optional
diagnostic counters retain the pinned native policies; their unsynchronized
initialization patterns are not silently redesigned in the port. This review
and the bounded executions below do not establish race freedom for every optional
configuration or diagnostic mode.

Pinned native and unlocked managed primary JITs each prepare 32 cold methods on
eight threads, execute 512 independently calculated results and show eight
simultaneously active compilations in their function traces. The same-source
serialized control shows one. A separate deferred-assembly probe JIT-compiles its
resolver while importing the outer method on the same thread; native, locked and
unlocked compilers all preserve the nested trace and result 120.

The tested unlocked NativeAOT source is `45c49ea` plus only the entry-lock removal,
verified against 1,784 compiler/build inputs; its entry file matches the integrated
source byte-for-byte. Full-analysis controls pass 182 Debug / 47 Release.
`artifacts/compilation-concurrency/compiler-evidence.json` records the inputs,
capture commands and limits. These are execution and overlap results, not
whole-dump, machine-byte or exhaustive runtime parity.

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

### R004: Stale native XML flow-graph jump-kind table

**Status:** native bug retained where behavior is defined; unsafe table access
is rejected explicitly. Correcting the labels is deferred, not an accepted
output deviation.

At `33baf8ee`, `fgdiagnostic.cpp:fgDumpFlowGraph` indexes an obsolete 11-entry
`kindImage` table with the 12-value `BBKinds` enum. Native captures confirm that
conditional blocks become `SWITCH` and returns become `NONE`; a real switch
indexes beyond the array. `Compiler.FlowGraphDump.cs` preserves the defined
labels and throws `FatalJitException` for the missing entry. It does not emulate
an undefined pointer read or silently substitute a corrected label.

Four complete XML graphs and 38 DOT graphs match the current-pin native files
byte for byte. DOT supports switches independently of this XML limitation.
See B350 and `artifacts\flowgraph-dumps\comparison-3.json`.

### R005: Timing counter representation

**Status:** existing implementation limitation, not native cycle-count parity.

On x64, native `compiler.cpp:_our_GetThreadCycles` uses RDTSC and
`CachedCyclesPerSecond` supplies its calibrated frequency. Managed `JitTimer`
uses `Stopwatch.GetTimestamp` and `Stopwatch.Frequency`. The CSV's cycle columns
therefore contain Stopwatch ticks, and the summary's `Mcycles` labels mean
millions of those counter units. Millisecond conversion uses the corresponding
frequency; raw counts, frequency and timing results are not interchangeable with
the native report.

Shutdown integration retains the existing managed clock instead of adding a
native helper library or executable-memory counter thunk. Diagnostic comparisons
must identify and exclude timing/cycle/frequency fields rather than claiming
performance or complete timing-report parity. Native-compatible RDTSC collection
and calibration remain open.

## Incomplete implementation, not intentional deviations

| ID | Evidence at the recorded C# baseline | Required action |
| --- | --- | --- |
| G001 | `Compiler.comp.cs`, `compCompileHelper`/local `GetResult`, deliberately returns `CORJIT_SKIPPED` until codegen exists. | Separate phase validation from codegen success and native fallback. Return success only after real code and required metadata exist. |
| G002 | `Compiler.cs`, `Compiler.fg.cs`, and `Compiler.opt.cs` contain phase methods returning `MODIFIED_NOTHING` with port TODOs. | Treat each as a stub, not a verified no-op. Replace complete functions in dependency order. |
| G003 | `Compiler.imp.cs`, `impHWIntrinsic`, currently returns `null` under a port TODO. | Reconcile the full intrinsic contract, including `mustExpand`; ordinary-call fallback is not proof of parity. |
| G004 | The original baseline had an empty `tests/Core/RyuJitSharp.UnitTests.csproj`. | Focused output regression cases now exist. Do not infer compiler-wide coverage; prioritize dump/disassembly and runtime-test validation. |

These entries describe the unstashed baseline. Reassess the affected gaps when
the saved WIP is integrated; do not overwrite existing work based on this table.
