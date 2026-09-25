# Porting milestones

A newest-first history of what the port can do and how it has developed.

The primary target is Windows x64. The port can import methods, expand inline
calls, lower heap allocations, simplify local accesses and construct internal
method entry/exit paths, rationalize expression trees into linear IR, and lower
minopts methods, create EH funclets and allocate registers with stack-resident
locals for Windows x64, but does not yet generate native code. Remaining
work includes hardware-intrinsic import, lifetime-enabled lowering cleanup,
optimized register allocation and code generation.

The [continuation plan](PLAN.md) describes the work ahead.
[Known limitations and deviations](DEVIATIONS.md) and the [backlog](BACKLOG.md)
cover outstanding issues.

## 2026-09-25: Scalar, SIMD and mask constant materialization

Code generation can now select and record native instruction sequences for all
constant forms: integer handles, scalar floating values, SIMD vectors and masks.
It preserves signed-zero distinctions, all-bits-set shortcuts, ISA-dependent
forms, TLS GC transitions and the assigned registers of vector and mask nodes.

SIMD constants use native repeated-pattern broadcasts or narrower zero-extending
loads where applicable. Static-field load descriptors retain relocations,
segment prefixes, operand options and exact instruction sizes. Block/node
generation, final encoding and runtime metadata publication remain;
production emission is still explicitly skipped.

## 2026-09-25: Constant data and SIMD register recording

The emitter now owns constant-data sections and block-address tables, preserving
native alignment, insertion order, bounded prefix reuse and tagged data offsets.
Floating constants retain signed zero and NaN payloads; single-precision
conversion follows the Windows-x64 rounding behavior.

Register-only SIMD recording preserves legacy destination copies, VEX operand
selection and EVEX/APX descriptor options and sizes. Static-field loads,
complete constant-node generation and final encoding remain; production
emission is still explicitly skipped.

## 2026-09-25: Immediate values and address-mode sizing

Integer-immediate support now chooses native zeroing, MOV or PC-relative LEA
forms without letting incidental address placement change instruction selection.
It preserves relocation hints, section-relative constants, TLS relaxation
prefixes and register-use tracking.

Address-mode recording and sizing cover base/index combinations, SIB bytes,
compressed displacements and instruction prefixes. Floating/SIMD constant-data
materialization, node generation and final encoding remain; production emission
is still explicitly skipped.

## 2026-09-25: Register values, copies and reloads

Code-generation support now consumes and produces register values in native
order, including local-home changes, temporary spills, reloads and GC-register
transitions. Multi-register copies preserve a source before a later field reload
can overwrite it. Reloads retain narrow-local normalization and debug live-range
boundaries.

The emitter records register moves, register/immediate operations and stack
loads, preserving instruction sizes, relocation metadata and native move
elision. These are dependencies of node generation, not emitted machine code:
the production emission boundary remains explicitly skipped.

## 2026-09-25: Native EH funclet creation

The funclet-creation phase now relocates handlers, keeps filters adjacent to
their handlers and orders funclets consistently with the runtime's EH clauses.
Handler-entry loops receive separate prolog blocks so backedges do not repeat
the prolog. Code generation can select the resulting function descriptors.

This removes the two EH-order differences in the minopts allocation corpus:
allocation dumps now match native for nineteen of twenty methods. Funclet phase
bodies and resulting graphs match for all twenty; two post-phase profile-check
diagnostics still differ. Hardware import remains the allocation mismatch.
Machine-code emission and final unwind metadata remain unimplemented.

## 2026-09-25: Prolog and epilog reservations

Code generation can now reserve main-function and funclet prolog/epilog groups,
retaining the GC snapshots needed for later out-of-order generation. Reservations
preserve group ordering, estimated offsets and funclet debug mappings, and force
the following group to report its GC state.

Epilogs separate preceding calls from their unwind region with native padding
and end active no-GC regions when more code follows. Actual prolog/epilog
generation, unwind metadata and production block generation remain unported.

## 2026-09-25: Block debug scopes and IL mappings

Block-level debug support now opens untracked-local scopes, catches up across IL
gaps and suppresses funclet scopes according to native policy. IL mappings retain
source flags, ordering and emitter positions, including padding when a debug
sequence point would otherwise have no code.

Scope cursors now follow their sorted indices instead of descriptor input order.
This also restores debug basic-block boundaries for unordered variable scopes.
Per-node generation, final debug metadata publication and machine-code emission
remain unintegrated.

## 2026-09-25: Block-entry locations and emitter labels

Code-generation support now restores live-in local locations and rehomes debug
ranges across block boundaries, including skipped call-finally tails. Emitter
labels preserve GC snapshots and instruction-group boundaries; inline labels
retain the current GC state.

Labels reached after a GC-capable call insert native padding when the return
address would otherwise describe conflicting liveness. This includes loop
alignment bookkeeping and the required zero-operand instruction recording.
Block scopes, per-node generation and final emission remain unintegrated.

## 2026-09-25: Whole-live-set transitions

Block-boundary liveness can now transfer registers and GC roots between locals,
processing deaths before births so two locals can safely reuse one register.
Transitions preserve tracked-stack reporting rules, owned liveness sets and
half-open variable location ranges. Unchanged sets retain the existing state,
and analysis-only updates do not require code-generation state.

Per-block location restoration and emitter labels are the next integration
dependencies. Production code generation remains explicitly skipped.

## 2026-09-25: Code-generation initialization

Code-generation preparation now classifies tracked GC locals with stack homes
and creates the tree-lifetime updater. Block-list initialization follows native
ordering for scope cursors, variable live ranges, call-return storage, pointer
tracking, parameter registers, current liveness and stack level.

The GC reset paths retain the distinction between full register maps and call
descriptors, without discarding the prepared tracked-stack classification.
Per-block liveness transitions, node generation and final encoding still precede
production activation; managed native-code emission remains zero.

## 2026-09-25: Tree-lifetime updates

Tree-driven liveness now follows native birth, death and partial-definition
rules for tracked locals, promoted fields, indirect accesses and call-defined
locals. Code-generation mode updates register and stack-GC state, records real
local spills and reports variable locations in native order. Per-field updates
support separately consumed multi-register values without treating every field
as born or dead together.

Both native mode predicates are preserved, including analysis without codegen
state and the two local-address handling modes. Duplicate-tree suppression and
liveness-delta diagnostics are retained. Code-generation initialization and
per-node emission remain ahead; production emission is still explicitly skipped.

## 2026-09-25: Stack-store recording and local spills

Windows-x64 local spills now record real instruction descriptors, including the
two stores needed for SIMD12 values. Spills preserve stack-home normalization,
register death, GC-root transitions and the ordering of variable live-range
updates. Constant and displacement descriptors retain native width, compact
encodings and logical sizes; redundant stack moves respect native side effects
and GC-region boundaries.

Recording currently supports the native no-instruction-disassembly mode.
Requested Debug instruction disassembly rejects before allocation rather than
silently losing output. Tree-lifetime code generation and final instruction
encoding remain ahead; production emission is still explicitly skipped.

## 2026-09-25: Instruction prefixes and stack sizing

Windows-x64 instruction sizing now accounts for legacy, VEX, EVEX and APX
prefixes, operand widths, extended registers and stack addressing. EVEX
displacement compression retains native tuple scaling, embedded broadcasts,
signed boundaries and the preference for smaller VEX encodings where possible.
Generated opcode tables preserve native ordering and pre-encoded prefix bits.

This closes sizing dependencies needed by real local spill stores. Instruction
recording, store emission and tree-lifetime activation remain ahead; production
code generation is still explicitly skipped.

## 2026-09-25: Stack-local instruction metadata

Instruction descriptors now retain native stack-local address encodings,
including spill temporaries and large local numbers/offsets, with the same
implementation limits. Generated instruction formats, update modes and scheduling
metadata preserve native ordering and APX new-destination selection.
Store-opcode selection distinguishes integer, floating-point, vector and mask
registers; vector alignment uses the actual frame base and stack bias.

This supplies metadata needed by local spill stores. Complete instruction sizing,
prefix selection, recording and encoding still precede store and tree-lifetime
activation; managed code emission remains zero.

## 2026-09-25: Emitter locations and variable live ranges

Captured code locations now retain group identity and native instruction/offset
cookies. Final offset lookup accounts for changed instruction sizes rather than
using stale estimates. Variable live ranges preserve native coalescing,
zero-length ranges, IL-local filtering, prolog/body separation and diagnostics.
Windows-x64 variable homes use the existing EE location layout.

These are prerequisites for tree-lifetime updates and eventual debug-scope
reporting. Spill stores, per-node generation and scope-emission orchestration
remain unported; production emission is still explicitly skipped.

## 2026-09-25: Instruction descriptor storage

Instruction groups can now save their descriptor data and entry GC state for
later encoding. Managed descriptor objects retain native logical byte sizes and
offsets, including the debug-info pointer prefix used by Release disassembly.
Saving preserves jump and alignment order, independent GC snapshots and
last-instruction references, including extension and out-of-order groups.
Group and placeholder diagnostics retain native formatting.

Instruction allocation now preserves the native buffer and instruction-count
limits, forced GC-region boundaries and stress-mode splitting. Operand sizes,
GC classification, relocation flags and backward descriptor links are initialized
with the native rules.

Method-emitter startup now creates the initial prolog and body groups and resets
locations, counters, GC-register state and stack-depth tracking. Allocation runs
through this startup path.

Basic, jump and alignment descriptor layouts are established. Additional
descriptor families, tree-lifetime updates, per-node generation and final
encoding remain ahead. This support does not activate production emission or
change the zero-byte managed execution boundary.

## 2026-09-25: Code-generation preparation

Code-generation support now marks branch, switch, throw-helper and EH-region
labels, respecting fallthrough and hot/cold boundaries. Emitter initialization
creates independent GC-variable sets, and pointer-register tracking preserves
live local registers while maintaining distinct reference and byref state.
Register diagnostics retain native names, widths and transition order.

Instruction groups now preserve numbering, funclet identity, list ordering and
prolog/epilog flag propagation. Local register-location updates cover scalar and
multi-register values, and lifetime transitions preserve the native priority of
death over birth for dead stores.

GC tracking is now enabled in Release as well as Debug; it is required compiler
behavior, not a diagnostic feature. Descriptor-buffer storage, per-node
generation and final metadata emission remain unported, so
the production boundary still explicitly skips code generation.

## 2026-09-25: Active minimal register allocation

The production backend now constructs intervals, allocates and resolves registers,
and prepares spill homes for minopts methods with stack-resident locals. It
preserves phase boundaries, allocation statistics and final verification.
Morph-time frame selection also establishes the native EH, P/Invoke and GC
reporting requirements before allocation.

The complete allocation-phase dump matches the pinned native compiler for
seventeen of twenty corpus methods. Three retain the known hardware-import and
EH-order differences. All twenty reach the explicit unfinished code-generation
boundary; these results establish allocation behavior, not managed execution.
Instruction generation, emission and runtime metadata remain ahead.

## 2026-09-24: Minimal register resolution and verification

Register resolution now connects interval construction and minimal allocation
to final node assignments, internal-register masks, copy/reload insertion and
stack-home preparation. It preserves full spills for temporary vectors and
optional memory uses. Checked-build verification replays physical assignments,
spills, reloads, copies and GC kills in native reference order.

This completes resolution for the initial stack-resident-local backend path.
The production allocation driver remains to be activated before the emission
boundary can advance; the compiler still does not generate native code.

## 2026-09-24: Register-resolution support

Register writeback and copy/reload insertion now preserve indexed results,
owning LIR uses and native insertion order. Spill accounting tracks peak
concurrent temporary requirements by normalized stack-home type, retaining
distinct GC and non-GC homes and avoiding double-counting upper-vector saves.
Final local marking preserves dependent struct fields, unused-local
initialization policy and frame-pointer selection.

Tuple diagnostics now include reference positions and final register
assignments as well as the pre-allocation view. The resolution traversal and
its checked-build verification remain before allocation can become active;
these support routines do not yet advance the native-code execution boundary.

## 2026-09-24: Interval construction without enregistered locals

The complete native interval-building mode for stack-resident locals now
connects block sequencing, incoming parameter-register liveness and node
references. It preserves block and node locations, cold-code boundaries,
frame-poison and security-cookie kills, and physical register masks. The
node-reference entrypoint also retains checked-build register-stress contracts
and pre-allocation tuple diagnostics.

This completes interval construction for the initial minopts backend path.
Register resolution and the allocation driver remain before activation;
the compiler still explicitly declines compilation at allocation and does
not generate native code.

## 2026-09-24: Complete Windows-x64 node-reference construction

The native node dispatcher now connects scalar, call, memory, local-store,
return and hardware-intrinsic register requirements. Local stores preserve
candidate liveness and multireg field ordering; returns retain ABI register
constraints. Hardware operations model implicit registers, delayed uses,
gather temporaries and APX/EVEX restrictions, including both DivRem and BigMul
result registers.

Build-state reset preserves argument placements across intervening nodes, and
SIMD element operations share an implicitly live spill temp that grows as
needed. Interval construction and register resolution remain before allocation
can become active; this completes node-level requirements, not code generation.

## 2026-09-24: Call and memory register-allocation constraints

Reference construction now models calls, register and stack arguments, write
barriers, block initialization and copies, indirections, comparisons and local
stack allocation. Calls preserve ABI returns, varargs register duplication,
tailcall target restrictions and async-continuation lifetimes. Memory operations
retain their native internal-register counts, SIMD widths and fixed-register
requirements.

Block sizing retains the unsigned native range, and local stack allocation uses
the target page size reported by the EE. Local-store, return and hardware-intrinsic
builders, node dispatch, interval construction and resolution remain before
allocation can become active.

## 2026-09-24: Scalar register-allocation constraints

Register-reference construction now covers shifts and rotates, multiplication,
division and remainder, casts, scalar math intrinsics and conditional selection.
These builders preserve native fixed-register requirements, kill ordering,
operand preferences and delayed register release, including the APX/EVEX
encoding restrictions and floating-point temporary rules.

The allocation phase remains inactive. The remaining node builders, interval
construction and register resolution must be complete before it can replace
the explicit native-fallback boundary.

## 2026-09-24: Minopts lowering activation

The compiler now runs lowering through P/Invoke method preparation, local
enregistration marking, block traversal and unreachable-block removal in the
native mode that does not require local-variable lifetimes. Stack and exception
helper preparation follow it; unfinished allocation explicitly declines
compilation rather than reporting an internal error.

All twenty corpus methods reach allocation. Seventeen lowering phase sections
and their resulting IR match the pinned native compiler exactly. The remaining
differences originate in hardware import and EH-funclet ordering before
lowering. P/Invoke frame types, switch node creation order and register/local
diagnostics retain native behavior.

Lifetime-enabled lowering still rejects before mutation until full flowgraph
cleanup is available. Register allocation, code emission and metadata remain
required for managed execution; the corpus continues through native fallback.

## 2026-09-24: Windows-x64 lowering integration

Common linear-IR lowering now brings together calls and argument placement,
control-flow guard checks, P/Invoke transitions, returns, switches, local and
indirect stores, block operations and memory helpers. Stack-level preparation
constructs and accounts for exception-helper blocks.

Fast tailcalls preserve incoming stack arguments that outgoing arguments would
overwrite, place profiler hooks and non-GC regions in native order, and avoid
reserving ordinary outgoing call space.

Hardware-intrinsic lowering now combines construction and element access,
scalar extraction, dot products, comparisons, conditional selection, ternary
logic and operand containment. Rounding operands and the ABI/gather exceptions
to scalar or reinterpret elision retain their native contracts.

Binary lowering includes BMI transformations and APX conditional-compare
chains. It preserves flag dependencies, memory read-modify-write forms,
variable shift-count semantics and linear-IR ownership when nodes are replaced.
The shared dispatcher now applies the native bitwise pretransforms and
byte-swap handling without rejecting operators that need no lowering.

These implementations are integrated, but the compiler's lowering phase is
not yet active. Phase orchestration and execution comparison are next;
register allocation and machine-code emission remain unfinished.

## 2026-09-24: Scalar condition and array lowering

Lowering now includes integer comparison narrowing, bit-test reductions,
condition reversal and reuse of processor flags for branches and conditional
selection. Floating comparisons retain native unordered/NaN handling and
compound condition checks. Instruction flags and jump kinds come from the
native header tables.

Boolean flag producers can bypass adjacent zero-test and negation chains,
feeding a conditional branch directly or materializing a boolean for other
consumers. Branch replacement preserves logical identity; intervening
instructions prevent unsafe reuse of flags.

Fused multiply helpers fold scalar negations and contained vector sign masks
into the native add/subtract and negated variants. They preserve scalar
upper-lane behavior and interpret vector sign masks using the operation's
floating-point type rather than the bitwise operand's type.

Array and string lengths, multidimensional lengths and lower bounds become
loads at the runtime's native offsets, preserving null faults. Bounds checks
use the native immediate and memory-operand containment rules.

Scalar lowering helpers fold constant casts, shifts and rotates while preserving
linear-IR ownership and condition-flag dependencies. Variable single-bit masks
can become bit-set, bit-clear or bit-invert operations, and redundant all-ones
masks can be removed.

These are backend building blocks, not an active lowering phase. Remaining
node and phase integration, register allocation and emission still prevent
native-code generation.

## 2026-09-24: Register allocation foundations

The backend now has target register state, interval/reference associations,
per-block variable maps and the native minimal-register selection policy.
This includes selection heuristics, fixed-register conflicts, reference
diagnostics and allocation statistics. Managed register maps preserve
split-block aliases without reproducing an oversized native byte copy.

These foundations do not yet activate register allocation. Interval building,
optimized allocation, resolution and machine-code emission remain unfinished.

Register assignment and lifetime support now track active and inactive
intervals, spills, reusable constant registers, delayed frees and block-entry
locations. Windows-x64 upper-vector preservation follows the native ABI and
spill-weight rules; register-state verification distinguishes pending frees
from registers that should already be available.

Incoming parameter registers are mapped to independently promoted fields,
preserving ABI segment order and existing rationalization mappings. Stack-only
and dependently promoted parameters do not acquire register mappings.

Local-reference accounting now handles both expression trees and linear IR,
including weighted uses, implicit parameter and P/Invoke references, EH
definition costs, and single-definition eligibility. Minopts retains the
native implicit-reference fast path. The local-marking phase now initializes
these counts, diagnostic slot numbers and generic-context lifetimes before
rationalization.

Tracked-local selection now applies native eligibility, reference-weight and
tie-breaking rules, preserving early-liveness and address-exposure policies.
It maintains tracked indices, reverse mappings and correctly sized local
bitsets. Liveness initialization rebuilds each block's local sets at the new
tracking epoch and clears memory use/def/live sets while retaining memory-havoc
and SSA state.

Per-block liveness generation follows depth-first graph order and collects
local and memory use/definition sets, including promoted fields, address-exposed
locals, P/Invoke frame roots and definitions that occur at calls rather than
argument evaluation. Expression-tree and linear-IR policies retain their
different partial-store and conditional-definition rules.

Inter-block liveness now propagates local and memory state to a fixed point,
including exception-handler edges, filter bypass and second-pass handler flow.
It preserves argument and generic-context lifetimes and identifies locals that
need initialization.

Backward linear-IR analysis now marks local and promoted-field last uses,
accounts for call-time definitions and P/Invoke frame roots, and removes dead
stores and values when the policy permits it. It retains required faults,
side effects and explicit GC initialization. Traversal follows replacement
nodes when unused loads become null checks.

The linear-IR liveness driver now combines initialization, use/def generation,
fixed-point propagation and backward analysis. It repeats analysis when dead
code removal changes block-entry lifetimes, including handler keepalive and
initialization requirements. Compiler phase activation, expression-tree
liveness orchestration and the allocation driver remain unfinished.

Post-lowering analysis now has its compiler entrypoint. Empty-block branch
threading preserves exception-region boundaries, loop cycles, edge likelihoods
and profile weights. The general flowgraph-cleanup pass remains unfinished.

Register kills and GC-specific spills now preserve live values and advance
fixed-register constraints. Temporary copy-register allocation retains the
interval's primary assignment and reports the native selection heuristic.

The minimal allocator now walks prepared reference streams, handles block and
location transitions, and releases ordinary, delayed-use and temporary-copy
registers in native order. It preserves optional-register and stress-spill
behavior. This is not yet connected to the compiler's allocation phase.

Minopts candidate preparation now gathers exception and finally liveness sets
and keeps locals stack-assigned. Frame selection honors required frame pointers
and P/Invoke frame discovery, removing the frame register from the allocation
pool when needed.

Definition and use construction now connects temporary values and tracked
locals to allocation intervals, including target preferences, delayed frees
and register-optional upper-vector restores. New intervals emit the native
creation diagnostics before their references are attached.

Operand-use construction handles contained addresses, call-argument registers
and binary read-modify-write operations. It preserves destination preferences
and delayed operand lifetimes, including memory-address registers that must
survive the operation. Remaining instruction-specific builders still prevent
preparing complete reference streams for allocation.

Internal-register temporaries now receive matching definition/use references,
and call results are defined after their register kills. Kill construction
accounts for helper-specific clobbers, write barriers, unmanaged transitions
and live upper-vector values. Register preferences avoid clobbered registers
without changing the native treatment of write-through locals.

## 2026-09-24: Rationalization and linear IR

Rationalization now converts sequenced expression trees into linear IR, removes
tree-only wrappers and unused values, and preserves call-argument evaluation
order. It includes Windows-x64 shuffle and mask rewrites, managed-call fallback
for unsupported intrinsics, and incoming-parameter register mappings.

Hardware-import and EH-funclet differences remain open. ARM64-specific
rationalization and non-xarch shuffle construction remain deferred.

## 2026-09-24: Global morph and outgoing-call ABI

Global morph now runs through trees, statements and blocks. It rewrites field
and byref accesses, applies local assertions, simplifies scalar and vector
expressions, and prepares Windows-x64 outgoing arguments. Eligible recursive
tail calls become loops, while branch folding and return merging simplify
control flow. Rewrites preserve evaluation order, exceptions and ABI requirements.

VN/SSA-based global assertion propagation remains separate. Hardware-intrinsic
import and the later lowering, register-allocation and code-generation phases
remain unfinished.

## 2026-09-23: Implicit-byref parameter preparation

Struct parameters passed through pointers now receive their byref descriptor
types before global morph. Promoted parameters keep their fields in a new
struct temporary when worthwhile; otherwise their field annotations prepare
global morph to access the incoming pointer directly. Retained promotion gets
an entry copy, and field ownership and OSR/register annotations are updated.

Rewriting those parameter accesses remains part of the upcoming global-morph
work; this preparation does not yet provide managed execution.

## 2026-09-23: Active local morphing

Local morph now simplifies indirect accesses to locals and promoted fields,
tracks escaping addresses, updates early reference counts and rebuilds
statement effects. Its optimized path propagates local addresses through
control flow and avoids exposing locals when their address temporaries become
unread.

Whole-node replacement preserves logical identity and locals-only sequencing
without changing CLR object types. Required implicit-byref retyping and global
morphing remain ahead of lowering, register allocation and code generation.

## 2026-09-23: Required heap allocation lowering

Object allocations now become runtime helper calls in minopts and when object
stack allocation is disabled. Replacement nodes retain logical identity, value
numbers, argument effects and ReadyToRun entry points, and their owning stores
are updated.

Optional stack-allocation analysis and cloning remain unported. Requesting that
path reports an implementation limitation instead of silently substituting heap
allocation. Required local and global morphing remain ahead before lowering,
register allocation and managed code generation can execute methods.

## 2026-09-23: Internal method entry and exit

The internal-block phase now constructs synchronized-method protection,
runtime generic exception filters, P/Invoke frame locals and reverse-P/Invoke
entry/exit calls. It also preserves redirected `this` arguments and adds
JustMyCode guards.

Return merging groups constant returns and constructs shared return locals,
with general-return rewrites reserved for global morph. Compiler-generated
statements retain unknown source locations rather than being attributed to
the first IL instruction. Allocation lowering and later required morphing
remain ahead on the path to minopts code generation.

## 2026-09-23: Active inlining and nested compilation

The inliner now expands candidate calls, replaces their return placeholders,
repairs calls that cannot be inlined, and reports its decisions. Nested inline
compilation uses a fresh compiler context for each attempt. Boxing-pattern
recognition and nullable field conversions support the additional method bodies
reached during inlining.

Hardware-intrinsic calls still take managed fallback paths rather than becoming
intrinsic IR.

## 2026-09-23: Post-import cleanup and OSR entry repair

Post-import cleanup removes unimported blocks, repairs predecessor lists,
refines inline return temporaries and updates exception-region boundaries.
It also constructs entry paths through nested or shared try regions for
on-stack replacement (OSR). This supplies the control-flow preparation for OSR,
not runtime OSR execution.

## 2026-09-23: Indirect-call transformation

Fat-pointer calls and guarded devirtualization now have their control-flow
transformations. These include class, method and delegate guards, multiple
likely targets, chained fast paths and fallback paths. Argument spilling
preserves evaluation order, while return repair reconnects transformed calls
with their consumers.

## 2026-09-23: Patchpoint transformation

Loop patchpoints can be expanded into counter updates, checks and helper-call
paths. Forced partial-compilation patchpoints replace the affected block's
statements. Patchpoint code generation and runtime OSR support remain later
steps.

## 2026-09-23: Qmark expansion

Early and late conditional-expression expansion turns top-level qmark trees
into control flow. It handles nested arms, local and field writeback, comma
expressions, throwing branches, exception-region boundaries and profile weights.

## 2026-09-23: Morph initialization

Morph initialization prepares class-constructor calls, Edit and Continue frame
requirements, debug GC argument checks and stack-check locals. Supporting
hash-vector storage tracks outgoing-argument temporaries.

## 2026-09-23: Windows-x64 hardware-intrinsic expression folding

The xarch hardware-intrinsic folding dispatcher now handles constant evaluation,
comparison modes, conversion cancellation, mask rewrites and identities with
one constant operand. It preserves scalar upper lanes, floating-point edge
cases and side-effect ordering.

This operates on existing intrinsic IR; importing hardware-intrinsic calls is
a separate, unfinished part of the port.

## 2026-09-23: Hardware-intrinsic node and mask analysis

Added vector-to-mask construction, conversion recognition, sign-bit extraction,
per-element mask analysis and reconfiguration of hardware-intrinsic nodes.
Changes that require a different managed node type use whole-node replacement
rather than reinterpreting the existing object.

## 2026-09-23: Mask evaluation and vector conversion

Added fixed-width mask unary and binary evaluation and conversions between masks
and vectors. Corrected all-bits-set mask construction to respect the requested
element count, including inactive storage and non-normalized conversion results.

## 2026-09-23: Fixed-width vector evaluation

Added unary and binary vector constant evaluation, including lane arithmetic,
comparison masks, shifts, rotates and floating-point bitwise operations.
Scalar operations retain the upper lanes required by xarch semantics, and
in-place operations support aliased inputs.

## 2026-09-23: Hardware-intrinsic constants and operation classification

Added fixed-width vector creation constants, lane zero/one queries and bitwise
queries. Hardware-intrinsic operation classification maps supported operations
to their constant-folding behavior, including effective negation and complement
patterns.

## 2026-09-23: Non-hardware-intrinsic expression folding

Binary folding now handles integer identities and masks, identical comparisons,
conditional selection and nullable-box comparisons. These transformations
preserve side effects and evaluation order while updating the resulting trees.

## 2026-09-23: Value-numbered constant and overflow folding

Tree constants now receive value numbers, including embedded class handles and
field-address metadata. Replacing a constant refreshes its value numbers;
overflow folding represents the corresponding helper exception behavior.

## 2026-09-23: Node assertions and morph completion

Added node-wide assertion generation and morph-completion support. These track
local and global facts, conditional branches, implied boolean ranges and the
invalidation of facts when values are redefined. They are prerequisites for the
larger morphing pass rather than an implementation of that entire pass.

## 2026-09-23: Unary value-number expressions

Added unary expression interning and folding, known array lengths and ordered
exception-value composition. Field sequences use managed identity tokens so
their identity remains stable when objects move during garbage collection.

## 2026-09-23: Fixed-width value-number constants

Added vector and mask constant storage and retrieval, zero values by type and
widening-cast normalization. Vector constants retain their bit-exact payloads;
SIMD12 values keep inactive storage clear.

## 2026-09-23: General assertion creation and branch facts

Added local and global assertion creation for constants, copies, type facts and
conditional branches. Corrected null-check offset handling so negative offsets
cannot incorrectly establish that a reference is non-null.

## 2026-09-23: Scalar folding and assertion support

Added scalar and floating-point constant folding, integral ranges, assertion
tables and invalidation, scalar value-number storage, non-negativity reasoning
through cyclic SSA definitions, and conditional bounds assertions.

## Earlier port milestones

| Date | Development |
| --- | --- |
| 2026-06-19 | Hello World could complete through the AltJIT path with native fallback. Class-layout and segment-list support formed part of this baseline. |
| 2026-06-17 to 18 | Import-completion fixes and operand-order analysis advanced the importer, with hardware-intrinsic import still outstanding. |
| 2026-06-15 to 17 | Added a static table-generation tool, expanded generated table-driven types and introduced basic inline-policy support. |
| 2026-06-14 to 15 | Added importer support routines, restored Release builds and improved dump formatting. |
| 2026-05-12 to 06-07 | Progressively ported block-code import, calls and intrinsics up to the hardware-intrinsic boundary, alongside updates from dotnet/runtime. |
| 2026-05-09 | Added basic-block construction, local-variable table initialization and first-block canonicalization. |
| 2026-05-02 to 04 | Extended compiler initialization and introduced the phase-dispatch framework, initially with placeholder phases. |
| 2026-04-18 to 26 | Retargeted to .NET 10 and refreshed the project to resume porting. |
| 2024-02-18 | Established the project, core interfaces and native-facing machinery needed to load as a no-op AltJIT. |
