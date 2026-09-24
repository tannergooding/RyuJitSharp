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

Scalar constant folding now uses replacement constructors that copy base
metadata and the logical tree ID without allocating another ID. Operation-specific
flags are cleared using the native node mask. The source must be unthreaded;
existing importer and placeholder-walker uses consume the returned replacement.
This does not implement LIR relinking or repair arbitrary cached aliases.
Overflow folding retains the native global-morph gate. Value-numbered constant
replacement now refreshes both VNs, and overflow helpers carry the native
exception-set composition (B082/B083/B100).

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

### D003: Deferred non-Windows-x64-only paths

**Status:** accepted scoped deferral; not a successful execution/parity result.

Port whole functions. A path unique to another target may explicitly terminate
with NYI while remaining compilable. Record the affected symbol, target predicate,
and missing behavior when introducing such a deferral. Windows-x64 behavior
within the function must not be replaced by stubs. Verify that the selected
failure path cannot continue as if implemented in Debug or Release.

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
