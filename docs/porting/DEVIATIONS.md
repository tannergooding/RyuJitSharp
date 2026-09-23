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

The existing `CheckedOps.Try*` arithmetic APIs return success (the inverse of
native `CheckedOps::*Overflows`) and expose the wrapped result through an out
parameter. Addition/subtraction use sign-bit checks or unsigned ordering;
signed multiplication checks that the full product is the sign extension of its low
half. These implement the native `ClrSafeInt` overflow decisions without using
exceptions for expected overflow. Callers must invert success when asking
whether an operation overflows (B065).

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

## Incomplete implementation, not intentional deviations

| ID | Evidence at the recorded C# baseline | Required action |
| --- | --- | --- |
| G001 | `Compiler.comp.cs`, `compCompileHelper`/local `GetResult`, deliberately returns `CORJIT_SKIPPED` until codegen exists. | Separate phase validation from codegen success and native fallback. Return success only after real code and required metadata exist. |
| G002 | `Compiler.cs`, `Compiler.fg.cs`, and `Compiler.opt.cs` contain phase methods returning `MODIFIED_NOTHING` with port TODOs. | Treat each as a stub, not a verified no-op. Replace complete functions in dependency order. |
| G003 | `Compiler.imp.cs`, `impHWIntrinsic`, currently returns `null` under a port TODO. | Reconcile the full intrinsic contract, including `mustExpand`; ordinary-call fallback is not proof of parity. |
| G004 | The original baseline had an empty `tests/Core/RyuJitSharp.UnitTests.csproj`. | Focused output regression cases now exist. Do not infer compiler-wide coverage; prioritize dump/disassembly and runtime-test validation. |

These entries describe the unstashed baseline. Reassess the affected gaps when
the saved WIP is integrated; do not overwrite existing work based on this table.
