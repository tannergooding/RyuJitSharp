# Porting milestones

A newest-first history of what the port can do and how it has developed.

The primary target is Windows x64. The port can import methods, expand inline
calls, lower heap allocations, simplify local accesses and construct internal
method entry/exit paths, but does not yet generate native code. Remaining work
includes hardware-intrinsic import, later morphing phases and code generation.

The [continuation plan](PLAN.md) describes the work ahead.
[Known limitations and deviations](DEVIATIONS.md) and the [backlog](BACKLOG.md)
cover outstanding issues.

## 2026-09-24: Global-morph foundations and outgoing-call ABI

Global morph now has transformations for local references, primitive and
promoted-field block initialization, and removal of expressions after no-return
calls while preserving earlier side effects. Literal construction and debug
stress copies preserve native handle shapes, logical identity and operand
ownership.

Outgoing calls can classify signature types into Windows-x64 argument registers
and stack slots, account for shadow space, insert virtual-stub and ReadyToRun
cells, and remove those cells before reclassification. Argument scheduling,
including spill decisions and evaluation ordering, preserves stores, exceptions,
nested calls, stack allocations and control-flow-guard checks. Struct-argument
support maps promoted fields to ABI slots and scopes temporary reuse across
nested calls. Fast-tail-call eligibility checks incoming stack space and whether
struct arguments would retain the caller's frame, preserving rejection reasons
and last-use rules.

Materializing argument temporaries, tail-call transformation and the recursive
tree/block drivers remain unfinished; these foundations are not yet activated
as a global-morph phase.

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
