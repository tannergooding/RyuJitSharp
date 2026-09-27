# Porting milestones

A newest-first history of what the port can do and how it has developed.

Windows x64 is the first execution/parity baseline. Selected minopts and
optimized corpora execute code emitted by the managed JIT. Implemented optimized
work documented below includes inlining, value numbering and CSE, loop and flow
optimizations, profile-guided paths, and register-allocation policies; selected
tiered/PGO and GC-stress scenarios also have execution coverage. These results
are scoped to their tested corpora and configurations, not blanket pipeline or
runtime coverage. Remaining work includes broader runtime and JIT/EE ABI/GC
coverage, full dump/code parity, and Linux and other target implementation and
runtime validation.

The [continuation plan](PLAN.md) describes the work ahead.
[Known limitations and deviations](DEVIATIONS.md) and the [backlog](BACKLOG.md)
cover outstanding issues.

## 2026-09-27: ARM64 allocator construction

The ARM64 allocator now constructs intervals with native integer, floating/vector
and predicate register banks. It excludes reserved registers and LR, preserves
EnC restrictions and does not apply AMD64-only patchpoint restrictions.
Caller/callee-save sets and register traversal indices retain native ordering.

The Linux-ARM64 target suite passes 189 cases in Debug and Release; existing
Windows allocator/frame controls pass 23 Debug/22 Release. These are managed
tests, not ARM64 generated-code execution. Frame selection remains blocked on
complete ARM64 frame layout: the native reservation check must perform layout
even though it ultimately returns true.

## 2026-09-27: ARM64 ABI and compiler prerequisites

ARM64 now compiles with full analysis in Debug and Release. Its classifier
preserves HFA/HVA banks, register exhaustion, Windows varargs and Apple packed
stack slots. Target register metadata, immediate encodings, fixed-width SIMD/mask
queries, intrinsic importing, two-register GC return layouts and FP/LR placement
policy have focused coverage across all three ABI variants.

Integration exposed an unsigned-enum assertion helper defect, missing assignment
flags after spilling long-vector multiply operands, and incorrect cross-call CSE
spill costs for ARM64 and SysV. The fixes retain native algorithms and have
before-fix behavioral failures, not just successful compilation.

Targeted suites pass 185 Linux-ARM64, 188 Windows-ARM64 and 186 Apple-ARM64 cases
in each configuration. Linux-x64 controls pass 102 in each, and shared Windows-x64
controls pass 350 Debug/335 Release. A fresh Windows NativeAOT JIT retains all
152 matching instruction streams and 304 selected allocation-phase slices,
including default and GC-stressed execution of the unchanged upstream corpus.

The non-Windows target results are managed unit tests on Windows, not runtime
parity. ARM64 lowering, register-allocation policies, instruction recording and
scalable frame/storage paths still contain explicit limitations. The unchanged
string-constant cost discrepancy is recorded separately as B375.

## 2026-09-27: SysV classification and Swift ABI helpers

The SysV x64 classifier now preserves independent integer/floating register
banks, atomic aggregate spilling, EE eightbyte layouts and native stack rounding.
Multireg calls initialize their return descriptors and track assigned registers;
copy/reload masks retain holes. A constructor-initialization defect previously
made a two-register return appear to occupy four registers.

Swift helpers now cache stable readonly lowering records, validate special
parameters, lower struct arguments and preserve post-call error stores. Linux-x64
target tests pass 61 cases in both Debug and Release, with affected Windows
controls also passing. These are managed unit results on Windows, not Linux
runtime parity. Swift parameter classification and the System V large-call
descriptor layout remain explicitly unsupported.

ARM64 declarations now expose the next register/intrinsic semantic dependencies.
Its classifier, HFA/HVA and declaration tests have not executed; that packet
remains uncommitted.

## 2026-09-27: Cross-target build prerequisites

Public target-RID selection now reaches the compiler's target configuration,
while retaining host fallback and explicit internal overrides. Shared ABI
support now represents two ordered passing segments and packed stack slots.
Dormant multireg-return declarations and misplaced xarch/intrinsic guards no
longer cause the original cross-target syntax failures. Swift parameter
classification remains explicitly unsupported rather than falling through.

Windows-x64 ABI, emitter and value-numbering controls pass in Debug and Release.
Linux-x64 compilation now exposes missing multireg/Swift helpers and stale member
spellings; ARM64 exposes further target/emitter declarations. Neither target's
classifier tests have executed. Their integrated implementation packets remain
uncommitted while those dependencies are resolved; this is build progress, not
Linux or ARM64 runtime support.

## 2026-09-27: Object layouts, delegates and UNBOX ordering

Expanded Windows-x64 execution to the complete unchanged upstream object-stack
allocation entrypoint and all three delegate entrypoints. Default execution
retains the original stack/heap allocation assertions, including boxed values,
GC-containing layouts, spans and escaping controls. Both default and GC-stressed
runs complete explicit collections and exercise stack-delegate invoke rewriting.

The comparisons exposed a known-type UNBOX translation defect: its payload
address was constructed before cloning the operand, then had that operand
replaced. This changed tree creation order and retained stale assignment flags
when cloning spilled an effectful operand. Construction now follows cloning,
as in native, while preserving the original assignment exactly once and the
null-check/byref ordering dependency. UNBOX.ANY retains its own load path.

The fixed compiler matches all 79 default and 73 GC-stressed instruction streams,
plus all 304 selected object-allocation and stack-array-expansion phase slices.
The original GC-stress path intentionally omits ZeroAllocTest and its five
dependencies and relaxes allocation-kind assertions; it is not equivalent to
the default assertion set. Whole-pipeline dump and raw relocated-byte parity
remain outside this evidence.

## 2026-09-27: Additional allocator policy coverage

Extended matched GC-stressed execution to reversed caller/callee preference,
extended lifetimes, rotated block boundaries, optional-register avoidance and
explicit heuristic reordering. These configurations change allocation while
preserving native-identical instructions and spill-event counts.

Two older native stress bits do not implement their named reverse/nearest
selection policies. They remain controls, not positive policy coverage.
The capture runner now exposes the actual native heuristic-order setting.
A free-first reordered sequence works; fully reversing the sequence asserts
in native framework code before selected compilation and remains a limitation.

## 2026-09-27: Incoming parameter spills and allocation stress

Incoming register parameters can now spill before the first block boundary.
The nullable allocation cursor previously rejected this valid path during
forced spilling or ordinary low-weight parameter eviction, aborting optimized
compilation. It now starts at the minimum location while preserving native
spill and block-entry map updates. Register-stress settings and method ranges
are exposed by the capture runner, including native enabled/disabled diagnostics.

The four upstream regressions retain all original assertions and emit
native-identical instructions under GC stress with small register sets and
scoped forced spills. Small-set allocation changes nine bodies; forced spilling
changes seventeen, with native and managed reporting identical spill counts.
A pinned native assertion requires leaving one assertion-caller method outside
the forced-spill range; that method still compiles and executes its assertions.
Unrestricted forced-spill support is not claimed.

## 2026-09-27: Upstream regressions and loop-unrolling diagnostics

Broadened Windows-x64 execution to unchanged upstream regressions for checked
subtraction loops, SIMD comparison complements, scalar fused multiply-add and
multiple stack-array allocations. Their original assertions cover overflow,
unordered comparisons, signed zero and NaN bits, including AVX-512 paths.
The selected methods emit native-identical instructions.

The comparisons exposed omitted loop-unrolling diagnostics, not different
transformations. Restored native loop graphs, cloned trees, final block tables
and operator names in entry-condition messages. All selected unrolling and
stack-expansion phases now match native. The capture runner also accepts exact
counts of overloaded method names while retaining case-sensitive selection.

## 2026-09-27: Reused importer state and collected-profile inlining

Corrected the shared list expansion used by importer pending-block and
spill-clique membership. Clearing a byte list does not erase its backing
storage, and increasing its count did not initialize the newly exposed bytes.
Stale membership flags could therefore skip a predecessor in a later inline,
giving merging paths different spill locals and producing incorrect results.
Expansion now initializes new entries while preserving live entries.

The warmed branch probe now executes correctly with actual collected dynamic
PGO and ProfilePolicy. Native and managed select the same two profitable
inlines and emit identical instructions at Tier0, instrumented Tier0 and Tier1.
Independently collected frequencies still differ; this does not establish
whole-pipeline dump equality or identical profile inputs.

## 2026-09-27: Redundant-branch phase diagnostics

Restored the native PHI-threading, threading-core, local-relop forwarding and
branch-inference diagnostics. The missing trace was not missing optimization:
the transformations and final generated instructions were already correct.
Branch-specific messages, value numbers, tree identities, memory-PHI CSE guards
and original native spelling are now preserved.

Both original policy-probe RBO phases match native byte-for-byte, including
positive PHI substitution and edge redirection. Main and Evaluate retain
native-identical instructions and 277/59-byte sizes; 37 Debug and 37 Release
branch/scalar-evolution controls pass. Evidence:
`artifacts\rbo-diagnostics\comparison-1.json`. This closes B361, not
whole-pipeline dump parity.

## 2026-09-27: Precise inline-scan constant propagation

Restored native precise-scan behavior for literal constants, binary operations,
and unary/binary branches. Constants now remain on the abstract stack instead
of immediately becoming unknown. Observations distinguish literal folding from
constant-argument benefits, preserve argument-dependent results, and recognize
array-length comparisons symmetrically. Comparison-producing opcodes use native
binary-expression handling rather than branch classification.

Eighteen old observation failures now pass. The original policy probe matches
native ExtendedDefault observations, estimates, inline decisions, and both
instruction streams/sizes (277-byte Main and 59-byte Evaluate). No profitability
coefficients changed. The 29-configuration/143-body execution baseline passes;
Core passes 12,103 Debug and 10,958 Release cases. B361's remaining RBO dump
differences are retained in `artifacts\inline-scan\comparison-1.json`.

## 2026-09-27: Native-width branch implication

Corrected both wide value-number-function conversions in
`optRelopImpliesRelop`. Native narrows them to byte-sized `genTreeOps`;
checked C# conversions instead threw on valid extended comparison functions.
Six direct and nested comparison cases reproduce the old overflow, and all
37 focused branch/scalar-evolution cases pass in Debug and Release.

The original exception-path probe now compiles and executes through the
managed JIT. Its previously failing Main matches native instructions and
277-byte code size. Whole RBO dump equality is not established: B361 tracks
PhiDef-threading/diagnostic differences, and B362 tracks separate default
inline-profitability differences in Evaluate. Both remain visible in
`artifacts\relop-narrowing\comparison-1.json`.

## 2026-09-27: Optional inline policies and replay

Completed Discretionary policy's opcode histograms, method-signature
observations, native size/performance models and CSV data, together with
Model, Profile, Random, Full and Size policies. Replay now reads the native
bounded text format, caches root lookup positions, traverses nested contexts,
matches token/hash/offset keys, controls forced inlines and closes its file
through existing XML finalization. Shared method hashing supplies identical
keys to the reader and diagnostic writer.

The integrated area passes 98 Debug and 25 Release focused cases and complete
Core passes 12,078 Debug and 10,933 Release cases. Six configured policies
produce matching selected method XML except timing, exact native schemas/data,
and identical instruction streams and sizes across 12 checked compilations.
Two additional replay configurations match four checked compilations: a full
fixture accepts four inlines; a pruned fixture retains one, rejecting a forced
child and the other callsite. An old checked-JIT control ignores the same replay
log and retains DefaultPolicy decisions.

Pinned native diagnostic inconsistencies remain explicit: Discretionary CSV
has two unlabelled data columns and swapped ThrowCount/ReturnCount labels
(B358); DEBUG replay expects raw call offsets while XML emits statement
offsets (B360). Positive replay therefore uses controlled inputs containing
the required call and context keys, not a claimed native XML round trip.
Profile policy has deterministic PGO-input coverage but not positive host-PGO
evidence. The original exception-path probe exposed separate redundant-branch
overflow B359; its source and failed capture are preserved.
Evidence: `artifacts\inline-policies\simple\comparison-2.json`,
`replay-comparison-2.json` and `old-replay-control.json`.

## 2026-09-27: Shared diagnostic output and automatic shutdown

Diagnostic files now permit other JIT writers and append each encoded block at
the current EOF, including after external truncation. The old managed writer
denied the native JIT's lazy append open during shutdown. Isolated callback
tracing established that ordinary teardown never entered the managed callback,
whereas explicitly closing it first allowed subsequent runtime dispatch.

With shared output, ordinary checked-host teardown produces the managed
four-method timing summary, aggregate reports and inline XML footer without
instrumentation or an explicit shutdown hook. All four selected instruction
streams and sizes and 21 non-timing/non-allocation CSV fields match native.
The two new sharing regressions fail against the old writer; complete Core
passes 11,999 Debug and 10,908 Release cases. B357 is resolved; the R005 clock,
D001 allocation and interleaved-XML limitations remain. Cross-JIT simultaneous
write atomicity is not established.
Evidence: `artifacts\shutdown-dispatch\comparison-1.json`.

## 2026-09-27: Compiler shutdown and timing diagnostics

Completed the enabled `compShutdown` paths: release assembly-name lists,
finish configured inline XML before the zero-method guard, write timing
summaries, close timing CSV and emit selected aggregate statistics.
The existing public shutdown callback retains its ownership and ordering.
Timing collection now activates for configured CSV rather than disabled CSV;
termination updates its aggregate by reference instead of modifying a copy.
Timing counters retain native unsigned widths and wraparound, native report
formatting and NaN spelling. CSV output preserves existing rows and reports
managed allocation rather than a fabricated zero.

Complete Core passes 11,997 Debug and 10,906 Release cases. The 24 new Debug
and 17 new Release cases cover lifecycle, configuration, aggregation, formatting
and file behavior. Both old-code controls fail for their intended defects.
The old checked JIT also produces a header-only timing CSV; the new JIT produces
the four expected rows, with all 21 non-timing/non-allocation columns matching
native and unchanged instruction streams and code sizes.

A focused host probe calls the live alternate-JIT shutdown vtable slot, verifies
that `getJit` then returns null, and captures its four-method timing summary,
XML footer and aggregate reports. Ordinary side-by-side host teardown did not
produce those alternate-JIT reports and remains a separate investigation (B357).
Shared XML contains interleaved forests from both JITs; this is not a claim of
whole-file XML validity. Timing units retain the existing Stopwatch limitation
(R005), and allocation statistics follow D001.
Evidence: `artifacts\compiler-shutdown\comparison-1.json`.

## 2026-09-27: Per-method compilation finalization

Completed `compCompileFinish`: method counting, managed allocation metrics,
loop-hoist aggregation, inline CSV/XML, ordered summaries, EE metric reporting,
the final verbose metric block and referenced-local enregistration statistics.
Inline output preserves native selection, schemas, tree ordering, escaping,
locking and append/stdout fallback. The table generator now emits native
six-decimal floating metric formatting.

Complete Core passes 11,973 Debug and 10,889 Release cases, including 65 new
Debug cases and two Release reporting cases. The public output regression
fails against the old empty finalization implementation. Twelve checked
FullOpts/MinOpts compilations preserve runtime output and instruction streams
and sizes. Each compares all 71 non-allocation metric lines exactly.
Ordered summaries differ only in the expected AltJIT region column; eight
selected CSV rows and four complete XML method records match after excluding
their timing fields. The comparator retains raw XML whitespace and line endings.

Allocation bytes follow D001, not native arena accounting. Optional
Discretionary-derived policy data and Replay finalization remain explicit
unsupported dependencies (B354). Process-wide shutdown, including invoking
the XML footer and aggregate statistics, remains a separate unported entry;
this is not whole-file XML or whole-jitdump parity.
Evidence: `artifacts\compilation-finish\comparison-2.json`.

## 2026-09-27: Native flow-graph dumping

The phase hook now writes DOT and XML graphs with native method/tier/phase
selection, filename escaping and byte-length limits, collision handling,
append behavior and standard-stream ownership. Graphs include compact branch
conditions, profile and memory-SSA annotations, lexical edge constraints,
nested EH regions and natural loops. Debug builds include graph-dump support;
the explicitly enabled non-Debug path also compiles.

All 58 new cases pass, and the original public stub fails all three output
regressions. Complete Core passes 11,908 Debug and 10,887 Release cases.
Six checked execution captures produce 38 DOT graphs and four XML graphs
byte-identical to the pinned native files, including instrumented Tier0.
Their 22 managed compilations preserve runtime output, instruction streams,
code sizes and 18 CSE phases. The broader B349 baseline is retained rather
than recaptured for this diagnostic-only area.

The native XML jump-kind table is stale: its defined labels remain unchanged,
while the out-of-bounds switch entry fails explicitly instead of reading
undefined memory (B350/R004). No label correction has been approved.
Evidence: `artifacts\flowgraph-dumps\comparison-3.json`.

## 2026-09-27: Active phase invariant checking

The phase driver now verifies entry-block constraints, cached flow-graph
annotations, local-list execution order, canonical-loop boundaries and unique
node IDs instead of calling five empty methods. Graph construction and validation
share the native callback-style DFS walk; validation observes existing numbering
without overwriting it. Local checking includes physical call definitions, and
uniqueness checking covers unthreaded trees, both threading modes and LIR.

All 45 new cases pass; 29 reject deliberately corrupt states that the original
compiler silently accepted. Complete Core passes 11,850 Debug and 10,887 Release
cases. The checked JIT executes the 29-configuration/143-body baseline and both
two- and twelve-iteration range captures with phase checking active. Baseline
instruction streams, sizes and 97 CSE phases remain equal to native, as do the
eight repeated bodies, 56 CSE phases and 48 recomputation fragments.
Evidence: `artifacts\phase-invariants`.

## 2026-09-27: Post-inlining compilation timing

Debug builds now capture the end-of-inlining counter and record the remaining
compilation interval in integer microseconds, including clearing stale elapsed
values when the interval is nonpositive. The hooks use the existing Stopwatch
clock convention, matching native's Windows high-resolution counter, and remain
empty in Release.

Six Debug and one Release cases pass; five cases fail with the old empty hooks.
These timing fields currently have no managed consumers. This is state-recording
coverage, not a performance or new runtime-parity claim.
Evidence: `artifacts\compilation-timing`.

## 2026-09-27: Repeated optimization state and graph reconstruction

`JitOptRepeat` now clears SSA/VN/assertion/CSE annotations and memoized analysis
state between iterations, resets nonprofile weights, and reconstructs DFS,
loops, weights and dominators in native order. Previously both entry points
were empty and the checked JIT aborted in second-iteration liveness without a
DFS tree. Profile weights are retained.

The range corpus executes all four selected methods with both two and twelve
optimization iterations. All eight emitted instruction streams and sizes,
56 complete CSE phases and 48 inter-iteration recomputation fragments match the
pinned native JIT exactly. The twelve-iteration run also verifies hexadecimal
CLR configuration encoding in the capture runner.

Focused regressions pass 98 Debug and 97 Release cases; all six reset/recompute
cases fail with the original stubs. Complete Core passes 11,799 Debug and
10,886 Release cases. The 29-configuration/143-body execution baseline retains
its instruction streams, sizes and 97 CSE phases.
Evidence: `artifacts\opt-repeat`. Repeated runtime profiling, whole dumps and
relocation-aware machine-byte comparisons remain separate coverage.

## 2026-09-27: Full Core validation restored

The full Core suite passes 11,791 Debug and 10,878 Release cases with none
skipped. The original 200 Debug and 161 Release failures are retained as the
baseline, with checked intrinsic bit conversions fixed in production and stale
fixture contracts corrected separately.

The final fixture group distinguishes LSRA dispatcher-owned AVX flags from
direct indirection building, checks effective comparison width without expecting
constant retyping, and preserves stack-array header order while allowing native
commutative operand ordering. Dispatcher, boundary-value and subtraction controls
pass with the complete group: 124 cases per configuration.
This closes the recorded full-suite failures, not compiler-wide execution or
dump/code parity. Evidence: `artifacts\core-validation`.

## 2026-09-27: Enum folding fixture nullability

Enum equality tests now provide a known-nonnull boxed argument when exercising
unboxing. Native's distinction remains unchanged: a null receiver throws, while
a null argument returns false. Explicit nullable-local and null-constant cases
retain the call; unsupported underlying types and inexact/different classes
remain rejected without being hidden behind the nullability guard.
All 32 call-folding and object-use cases pass in both configurations.

## 2026-09-27: Null-check and implicit-byref fixture contracts

Early-propagation and tailcall fixtures now initialize the absent return-buffer
sentinel, preserving ordinary receiver nullability. Local-threading tests model
native integer pointers for ordinary implicit-byref parameters and managed byrefs
for async parameters, with all three replacement positions covered in both modes.
The complete affected fixtures and SSA/liveness controls pass 60 Debug and
57 Release cases without production changes.

## 2026-09-27: Intrinsic integer bit conversions

Integral broadcasts, SIMD mask evaluation and hardware-intrinsic folding now
preserve native truncation and signed/unsigned bit reinterpretation in checked
builds. Negative element indices still remain unfolded, oversized shift counts
retain native handling, and bit scans of zero retain their undefined-result
guard. Unsigned lane extraction and 32-bit move masks preserve high bits.

The intrinsic/SIMD/VN group passes 1,094 Debug and 1,071 Release cases. With the
same corrected tests, old production fails 83 Debug cases and passes 386 controls;
all 465 old Release cases pass, confirming the checked-build distinction.
The fresh checked JIT executes all 29 baseline configurations and 143 bodies,
with all instruction traces/sizes and 97 CSE phases still native-equal.
Full Core validation is now at 18 Debug and 12 Release failures, retained for the
next batch. Evidence: `artifacts\intrinsic-bit-conversions`.

## 2026-09-27: Descriptor and division test adapters

Descriptor tests now use the public `Emitter.CnsVal` type directly, and division
ownership tests target the integrated `Lowering.ReplaceWithLclVar` helper.
These adapter corrections restore 142 failing cases per configuration without
changing production or weakening payload, layout, relocation or ownership checks.
The complete fixtures and call-target controls pass 179 Debug and 183 Release
cases. Other full-suite failures remain under investigation.

## 2026-09-27: Indirect-call fixture fault contracts

The indirect-call fixture now initializes an absent return-buffer argument to
`BAD_VAR_NUM`. Its zero-initialized compiler had incorrectly identified receiver
local zero as nonnull, suppressing five expected method-table load faults.
Production behavior is unchanged, including native's explicit exception-flag
clearing during global morph.

The original failing cases now pass alongside explicit receiver-nullability,
method-table nonfaulting-flag and ordinary-local/return-buffer controls.
Indirect-call and related argument fixtures pass 160 Debug and 158 Release cases
with full analysis. The original five failures and 55 passing controls remain
recorded in `artifacts\indirect-call-faults`.

The full Core run exposed the same sentinel defect in five older lowering,
VN and register-allocation fixtures. Correct initialization restores seven
fault/write-barrier expectations per configuration; all 154 Debug and 153 Release
cases in the affected fixtures and indirect-call controls pass unchanged.
The full-suite baseline remains recorded in `artifacts\core-validation`:
200 Debug and 161 Release failures, with other failure groups still under review.

## 2026-09-27: Floating local evaluation costs

Floating-point local uses and definitions now retain native AMD64 size costs:
two for likely-register operands and four for memory operands. Memory operands
retain the ordinary indirection execution cost rather than an extra floating
penalty. Small-local normalization, nonfloating costs and constant costs are
unchanged.

All 54 `FoldFloating` cost lines across both stress modes now match native,
including the local load and its parent. All 143 baseline plus 38 stress
instruction traces and sizes remain identical, as do all 97 CSE phases and
38 stress phases. This completes the recorded B330 discrepancies without
normalizing dumps; native deferred-profile diagnostics remain separate.

## 2026-09-27: Range and map value-number diagnostics

Range assertion merging now prints the conservative normal value number and
the selected phi-edge assertions. Precise map stores/selects and physical
selects use native hexadecimal value-number notation rather than decimal IDs.
The range and map algorithms are unchanged.

All 34 affected diagnostic lines across both tree-splitting stress modes now
match native, while all 38 complete stress phases, instruction traces and code
sizes remain identical. The separately observed floating-tree cost discrepancy
and native deferred-profile limitation still prevent whole-dump parity.

## 2026-09-27: Native paired value-number allocation order

Six allocating paired exception and normal-unique operations now preserve
Windows-x64 native constructor-argument evaluation order: conservative first,
then liberal, without changing the semantic roles of the returned values.
Explicitly sequenced pair/load operations and shared normal values are unchanged.

All 97 complete CSE phases in the baseline now match native exactly, up from 77.
The original Boolean indirection's value numbers match without normalization.
All 143 native instruction traces and code sizes remain identical, with runtime
results and required collections preserved. Whole dumps, raw machine bytes and
other targets remain outside this comparison.

## 2026-09-27: CSE candidate and dataflow diagnostics

CSE now emits native candidate-registration, generated/available-set and
definition/use diagnostics, including exception rejection and newly detected
cross-call liveness. Heuristic cutoffs retain native six-decimal formatting.
Shared-constant keys also retain native unsigned hash truncation in checked
builds instead of throwing.

All 44 positive candidate/dataflow/availability traces match native exactly.
Complete CSE phase matches increase from 53 to 77 of 97, including 24 positive
phases. The 143-body execution baseline retains identical native instruction
traces and sizes. Remaining phase differences include liberal/conservative
VN numbering and are not normalized away.

## 2026-09-27: Array identity and baseline instruction traces

Array type printing now returns after the element type and rank suffix, as
native does, instead of appending the VM's dynamic-class placeholder. This
corrects method signatures, their hash inputs and array-related annotations.
Recursive arrays and generic element types retain their existing formatting.

All 143 bodies in the 29-configuration execution baseline now match native
method identities, code sizes and emitted `IN` instruction lines. Fourteen
identities and 23 annotated streams differed before this correction; sizes and
runtime results are unchanged. The complete positive array-morph phase also
matches. These are exact scoped comparisons, not whole-dump or raw-machine-byte
parity.

## 2026-09-27: CSE and array diagnostic alignment

CSE temporary-allocation reasons now use the native two-digit candidate index.
Array-element names now print one fewer comma than their rank. Both corrections
preserve optimization decisions and generated instructions. The selected native
CSE allocation lines and array transformation body match exactly; full CSE logs
and the array method-signature announcement remain separate parity gaps.

## 2026-09-27: Profile diagnostic alignment

Profile tables now use native decimal digit counting for IBC-column widths,
and shared weight formatters preserve native lowercase exponents and precision.
Default and stress-profile block-weight and repair-phase captures match exactly
within those phases and their before/after tables, including two positive repairs.
Profile values and generated instruction streams are unchanged. The separate
native deferred-profile flag issue remains visible in default prechecks.

## 2026-09-27: Native clone construction and metadata

Expression cloning now allocates simple-node and hardware-intrinsic parents
before recursively cloning their operands, matching native construction order.
The separate array-element, compare-exchange and select orders are preserved.
Clones retain independent operands, flags, costs and value numbers.

The positive finally-clone and runtime-lookup phase output and post-phase trees
now match the pinned native captures exactly, including node identities.
Existing runtime results and async instruction streams are preserved.
Integer clones also retain their native type and clone-specific diagnostic
cookie policy. Finally and GC-poll instruction streams now match including
static-handle descriptions. Deferred-profile diagnostics still prevent
whole-dump parity.

## 2026-09-27: Complete EH-table verification

Debug compilations now check the full native EH-table contract at the existing
phase boundaries. The verifier checks clause identity, retained boundaries,
lexical nesting and disjointness, filters, extracted funclets, block-region
indices and legal handler exits.

The checks distinguish pre-normalization shared starts from normalized regions
and preserve mutually protected tries and shared ends. A temporary lexical map
allows these checks without renumbering blocks or changing later compilation.
Runtime EH and async execution retain their established behavior; this closes
the diagnostic stub, not the remaining whole-dump parity gaps.

## 2026-09-26: Async state-machine transformation

Runtime-async methods now execute the complete transformation after
rationalization: continuation layout and reuse, normal and tail suspension,
live-state storage, execution/synchronization contexts, resumption dispatch and
exception handling. Post-morph pseudoarguments are consumed without invalidating
the completed ABI classification, and symbolic continuation offsets are resolved
in their final owning blocks.

The runtime corpus exercises genuinely pending awaits, results, exceptions and
`AsyncLocal` restoration. Its Tier0 wrapper provides a positive native tail-await
case; the same DLL under full optimization provides a no-tail control. Existing
LIR operands retain their costs and sequence numbers during context-helper
construction, while helper-specific context arguments preserve native spilling.

Transformation execution logs, post-phase control flow and continuation records
match native in FullOpts, Tier0 and GCStress4 captures. FullOpts and GCStress4
instruction streams match; with optional relocations disabled, all seven
transformed Tier0 bodies match as well. Three non-transformed adapters retain
code-order differences. Input address types and previously recorded diagnostics
still prevent whole-dump parity; the async corpus does not assert a full
collection count.

## 2026-09-26: OSR address and phase-status alignment

Patchpoint counter addresses now use native integer-pointer types rather than
GC byrefs. The throw-edge phase also preserves the native functor wrapper's
status contract instead of forwarding the helper's no-change result.

All six strengthened OSR corpus bodies now match native phase boundaries,
phase statuses and emitted instruction lines, including Tier0 counter-address
loads and actual Tier1-OSR carried-state reads. Initialization still executes
once. Whole dumps remain different at the previously recorded deferred
profile-check diagnostics.

## 2026-09-26: OSR entry redirection and carried state

OSR import now starts at the designated entry block rather than replaying the
method prolog and loop initialization. Entry-edge predecessors, weights,
profile flags and inconsistent original-entry loop profiles follow native
handling.

The strengthened corpus detects the previous defect with independent
initialization counters: the old JIT increments both twice; the new JIT and
native increment each once. Both actual Tier1-OSR bodies read carried frame
state and match native 46/78-byte instruction streams. Tier0 metadata and
importer redirection also match. Tier0 `lea` operand formatting and the
`Adjust throw edge likelihoods` phase-status difference remain visible rather
than included in a whole-dump parity claim.

## 2026-09-26: Physical struct promotion

Physical promotion now selects profitable primitive replacements, computes
replacement and remainder liveness, and rewrites or decomposes aggregate
accesses. Runtime-fill stores retain their required readbacks; weighted costs
preserve native arithmetic order, and address offsets wrap at target width.
The shared local-store factory now preserves the native normalization type for
small locals and parameters.

Each UTF8 interpolated-handler method selects twelve replacements, omits two
dying remainders, and matches the native 16-block post-phase IR and 75-byte,
23-instruction body. All nine struct controls retain native no-change decisions
and emitted instructions. Physical-promotion diagnostics match through the
phase and resulting IR; the existing deferred profile-check difference remains
outside that parity claim. The ordinary execution baseline is unchanged.

## 2026-09-26: OSR patchpoint metadata publication

Tier0 compilations now publish their patchpoint frame metadata through the EE's
allocation and ownership contract. The publisher records original and shadow
local offsets, address exposure, generic and async context slots, security and
monitor state, and callee-save registers, including the pseudo-return frame slot.

The hot-loop and generic-loop corpus matches native Tier0 patchpoint phases,
120/136-byte frame sizes, local and special offsets, and callee-save masks.
Both loops produce actual managed Tier1-OSR compilations, but correct resumption
is not established: the importer still enters at the original method start
rather than redirecting to the OSR entry, repeating initialization and losing
carried loop state. The corpus's unchanged final values do not prove correct
resumption. That capture predates the entry-redirection fix above;
equal Tier0 code sizes are not claimed as instruction or byte parity.

## 2026-09-26: Tree-splitting stress modes

The tree-splitting stress phase now supports native deterministic random
selection and complete comma removal. It preserves split limits, statement
evaluation order, side effects, block-operation remorphing and restart points.
Random mode takes precedence when both modes are selected.

The existing corpus matches all 74 native random split locations and 22
successful splits, including the eight-split limit in `Main`. Comma mode removes
the same seven nodes from `Main` and `SynchronizedReturn`. Both modes execute
19 bodies with matching phase diagnostics, instruction sequences and sizes.
Surrounding profile checks, VN rendering and floating-tree metadata retain
separate dump differences; the ordinary execution baseline is unchanged.

## 2026-09-26: Profile instrumentation and tiered execution

Method instrumentation now prepares and emits block or sparse-edge counters,
handle histograms and value histograms through the runtime's schema allocation
contract. Critical-edge handling, probe relocation, inline-return processing
and native-width synthesized counts follow the pinned implementation.
Atomic counter nodes preserve their concrete indirection type through cloning.

The edge and block corpora each execute 20 native-matching bodies. Eager GDV
callers insert and execute class-profile probes; optimized span copy/equality
methods each insert two value probes and match the native 106/101-byte bodies.
Probe schemas, post-phase trees and emitted instruction sequences match;
process-dependent addresses and existing profile-check diagnostics remain
outside whole-dump parity.

This work also corrects the earlier tiered heap failure: compiler cleanup no
longer frees borrowed EE-owned OSR metadata, while locally allocated
mismatched-target copies remain owned and released by the compiler. The
15-body hot-tier GDV corpus now executes successfully, matching native positive
interface/virtual guard resolution and the unresolved-receiver control.

## 2026-09-26: Weighted mask-local conversion

Mask conversion now compares block-weighted costs across all definitions and
uses of a local, retaining it as a mask when that removes more conversions
than it adds. Ties and uses that could lose vector data remain unchanged;
converted locals preserve the native SIMD metadata and local threading.

The AVX-512 corpus removes one store conversion and two use conversions in
the positive method. Its 52-byte body and the two unchanged control bodies
match native emitted instruction slices under actual FullOpts execution.
The native mask-local debug-range assertion also affects managed full dumps,
which retry at MinOpts; separate disassembly captures establish optimized
execution rather than counting that retry as a pass.

## 2026-09-26: Last-use implicit-byref copy omission

The pre-morph phase now marks last-use struct locals eligible for passing
their existing storage to an implicit-byref argument. Marking happens before
argument reordering, preserving effects when the callee can mutate that storage.
Promoted fields, implicit-byref parameters and by-value arguments retain their
existing treatment.

The nine-body struct corpus matches native marking decisions and emitted
instruction slices. `FourFieldCopy` now passes the existing local's address
without an outgoing copy: its body shrinks from 46 to 34 bytes and its frame
from 72 to 56 bytes, matching native. Adjacent profile-check diagnostics remain
outside whole-dump parity.

## 2026-09-26: Original struct-local promotion

The original struct-promotion phase now dispatches through the existing
eligibility and profitability rules, clears conservative inline-time type
information, and promotes only original locals into ordered field locals.
SIMD/register-struct handling and tracking-limit decisions follow native.
Promoted-local value-numbering diagnostics also preserve native null
indentation instead of aborting compilation.

The focused corpus matches native promotion decisions and field layouts;
all six positively promoted bodies have matching emitted instruction slices.
A downstream copy remains in the unpromoted four-field control. UTF-8 helper
execution also benefits from promotion, but its remaining graph and code-size
differences are not resolved.

## 2026-09-26: GDV guard resolution

Guarded-devirtualization checks can now become unconditional branches when
inlining establishes an exact, single-definition local of the guarded type.
Resolution preserves the method-table read's exception effects and updates
predecessor references and profiles through the existing repair logic.

The native tiered corpus reaches positive interface and virtual guard
resolution. Managed default execution matches the no-change controls, but
managed tiered execution fails before reaching Tier1. Positive managed
resolution and its runtime parity remain unverified.

## 2026-09-26: Profile repair

The repair phase now reconstructs inconsistent profiles through the existing
synthesis engine while retaining edge likelihoods. Methods without profile
weights and already-consistent profiles remain unchanged; an unsuccessful
reconstruction is not relabeled consistent.

Profile-stress execution reaches positive post-morph repairs in `Main` and
`ManyReturns`; native and managed post-repair tables match. The same four precise
inconsistencies in `Main` disappear after reconstruction. Displayed weights and
likelihoods remain unchanged, but the dumps do not expose their unrounded
values. Ordinary execution follows the no-PGO path. Exponent casing and existing
profile-check/padding diagnostics remain outside exact whole-dump parity.

## 2026-09-26: Post-inline no-return cleanup

The post-inline cleanup phase now finds no-return calls in native preorder
execution order, preserves earlier effects with early tree splitting, removes
later statements and converts blocks to throws. Calls under qmarks are ignored;
a qmark anywhere in a statement requiring splitting vetoes that block.

The focused corpus executes a positive nested-call trim identified while an
inline candidate is examined and rejected as unprofitable. Its phase log,
resulting CFG/IR and 24-byte instruction slice match native; the preceding side
effect and exception are preserved. All fourteen optimized/minopts emitted
instruction sequences and sizes match. Minopts skips the phase. Raw hex and
deferred profile-check diagnostics still differ in other slices, so this is not
whole-dump or whole-corpus machine-byte parity.

## 2026-09-26: Missing block weights

Block-weight computation now propagates missing weights using native
predecessor and successor rules. It preserves profile-weight protection,
successor precedence, splitting-only handler adjustments, call-finally
continuations and the ten-pass bound for nonconverging graphs.

Seeded comparisons against the extracted native functions cover inference,
EH rules and convergence. The optimized runtime corpus executes under ordinary,
fake-split and stress-profile configurations, but all 57 selected public phases
report no changes. This is runtime preservation, not positive transformation
parity. Four stress-profile CFG tables retain an existing IBC-column padding
difference; whole-method IR and codegen parity remain separate.

## 2026-09-26: Cold-section selection

The cold-section phase now selects a trailing rare-block suffix using native
size estimates, preserving the eight-byte thresholds, entry-block protection,
funclet grouping, call-finally pairs, EH gates and forced-splitting policy.
Existing code allocation and unwind publication handle the selected boundary.

The cold-throw and GC-poll corpora execute hot paths, cold exceptions and EH
across ordinary, fake-split, forced-stress and EH-veto configurations. All
23 selected bodies match native cold boundaries and code sizes. Six of seven
positive phase/CFG slices match exactly; the remaining slice retains existing
EH node identities. Emitted instructions and offsets match, with existing EH
static-data annotation differences. These DEBUG fake-split runs allocate hot
and cold code contiguously; they do not establish discontiguous-allocation
runtime parity.

## 2026-09-26: Optimized block layout

The layout phase now starts from profile-ordered, loop-aware traversal and runs
native hot-jump compaction and greedy partition swaps. It preserves priority
ties, call-finally pairs, contiguous EH regions and try ends, then invalidates
the reused flowgraph annotations. LSRA uses the same extracted traversal without
changing its block sequence.

Cold GC-poll blocks now move behind hot returns as native does, resolving the
observed layout code-size differences. All twelve selected corpus code sizes
match; six of eight optimized layout slices match exactly. Remaining EH
differences are pre-existing IR identities and static-handle descriptions, not
layout or instruction-order differences. Cold-section selection was completed
in the following batch.

## 2026-09-26: GC-poll insertion

Suppressed-transition unmanaged calls now receive the native inline trap check
or GC helper call. Optimized code scans surviving trees and recognizes regular
unmanaged calls that already poll; minopts retains native flag-based selection.
The phase preserves early tail-call placement, sequence points, EH boundaries,
outgoing edges and block weights.

The process-ID corpus executes suppressed and regular P/Invokes in optimized,
minopts and GC-stress modes, including a finally handler. Eight positive
transformation/CFG/IR slices and both no-change controls match native exactly.
Two EH slices retain node IDs that already differ before insertion. The remaining
optimized code-size gaps come from the still-unported layout phase, which native
uses to move cold polls behind hot returns. Minopts selected code sizes match.

## 2026-09-26: Late cast expansion

The late-cast phase now turns eligible helpers into null and method-table guards
with native exact-type, speculative, profile-driven and multiple-candidate
selection. It preserves specialized fallbacks, known-failure throws, shared
generic handles, conditional edge probabilities and profile repair.

Base/subclass, string, array and shared-generic casts execute with matching native
success, null and exception behavior, including reference identity after GC.
Six positive transformation/CFG/IR slices match exactly; array slices retain the
existing dynamic-class name diagnostic difference. Matching selected code sizes
are not a claim of complete machine-code parity.

Execution exposed a missing exit after importing reference `unbox.any` as a cast;
native comparison exposed discarded indirection costs in the shared evaluation
walker. Both are corrected, with failing-before execution/cost checks and the
established execution matrix preserved. Continuation proceeds to GC-poll insertion.

## 2026-09-26: Value-numbered UTF-8 literal expansion

The VN-intrinsic phase now expands immutable UTF-16 literals into guarded UTF-8
constant stores. It preserves native source/content validation, unroll limits,
overlapping final stores and insufficient-capacity results. The bounded converter
matches the pinned minipal implementation across all individual UTF-16 code units
and a deterministic boundary/random corpus, including malformed surrogate fallback.

ASCII, multibyte and malformed literals execute with destination lengths around
the required capacity in fullopts, minopts and GCStress. Optimized native and
managed expansions have matching store widths, offsets, bytes and returned
lengths. Whole CFGs and machine code still differ because the incoming graph and
struct-promotion decisions differ; these are not claimed as parity.

The execution closure also corrects byref-addition assertions and native string
literal printing, including quotes, truncation, embedded NULs, replacement of
unmatched surrogates and unavailable EE data. The established execution matrix
and newly integrated static-init/TLS corpora remain intact.

## 2026-09-26: Thread-local access expansion

The Windows x64 TLS phase now replaces optimized thread-static helpers with
module TLS loads, direct non-GC offsets or guarded cached-block accesses. Missing
GC blocks retain the original helper fallback. The NativeAOT branch preserves a
GC-tracked TLS root and slow-path result; runtime validation currently covers
CoreCLR, not NativeAOT.

Selected integer and reference accesses execute across two threads and forced
collections, including GCStress. Eight positive optimized phase slices match
native decisions, CFGs and IR without normalizing node IDs. NativeAOT statement
creation order and CoreCLR node allocation order were reconciled during
integration. Minopts remains unexpanded; the existing execution matrix still
passes. Full dumps and generated code are not yet identical.

## 2026-09-26: Static-initialization expansion

The public static-init phase now replaces eligible GC and non-GC static-base
helpers with the native initialization guard, retaining the original helper on
the cold path. Both CoreCLR and NativeAOT layouts, evaluation order, EH region
inheritance and block weights follow the pinned implementation. Runtime lookups
now use the same shared helper traversal and block-splitting support.

The explicit-cctor corpus executes first and repeated accesses, two helpers in
one expression and a reference surviving compacting GC. Fullopts and GCStress
produce native-matching transformation logs and CFGs; CoreCLR minopts leaves the
calls unexpanded. Post-phase VN identifiers and profile diagnostics still differ,
so this is not full dump or machine-code parity. TLS and VN-intrinsic expansion
remain inactive.

Reproduce by building `sources\PortingStaticInitCorpus` in Release, then running
`scripts\porting\Invoke-PortingCorpus.ps1` against a matching CoreRoot with
`-TypeName RyuJitSharp.StaticInitCases -ExpectedMethods ReadInt,ReadObject,ReadPair`.
Use a fresh output directory per capture; compare native, managed execution,
`-MinOpts` and `-GcStress` configurations.

## 2026-09-26: Upstream synchronization

The oracle, residual native source and existing C# implementations now track
upstream `33baf8e`. The JIT/EE contract carries negative instruction-set
dependencies, and IL-stub secret arguments use the ordinary parameter ABI.
Implicit-byref classification, SIMD operand ordering and value numbering,
comparison containment, conditional escape analysis and final liveness follow
the refreshed native algorithms.

The established optimized, minopts and GC-stress corpora continue to execute
managed-generated code against a matching native host. Saved static-init, TLS
and VN-intrinsic work remains inactive and separate from this synchronization.
Full dump, generated-code and runtime-metadata parity remains incomplete.

## 2026-09-26: Thread-static EE contract alignment

The thread-static metadata query now matches the pinned native interface across
all three managed EE wrappers and the vtable. The obsolete extra argument is
removed, and the final metadata field identifies the base of thread-local data
rather than a GC-data pointer.

This restores a prerequisite for TLS-access expansion without changing the
vtable slot or structure layout. TLS expansion itself remains in progress.

## 2026-09-26: Runtime-lookup expansion

Generic-handle lookup expansion now emits dictionary loads and the required
size/null guards while retaining the original helper as a fallback. It preserves
evaluation order, shared result ownership, generic context, flow weights and
the enclosing EH region.

Selected shared-generic methods resolve constructed types for different
reference-type arguments and repeated lookups, with native-matching expansion
decisions and CFGs. The combined array/lookup corpus now executes both selected
managed bodies without bypassing lowering's expanded-lookup requirement.
Post-phase node IDs and generated-code layout still differ. Runtime coverage of
size guards, indirect offsets and lookups inside EH regions remains open.

## 2026-09-26: Multidimensional array morphing

Array morphing now expands multidimensional element addresses into ordered
lower-bound adjustments, per-dimension bounds checks and row-major address
arithmetic. It preserves effectful index evaluation, unsigned widening before
pointer-sized scaling, and temporary reuse between blocks.

A selected rank-two method executes with both zero and nonzero lower bounds and
rejects each dimension's lower and upper out-of-range indices. Its constructed
and remorphed expressions and post-phase CFG/IR match native byte-for-byte.
Established optimized and minopts/GC-stress corpora continue to execute.
Higher-rank runtime coverage, pre-expansion rank/signature diagnostics and full
generated-code parity remain open.

## 2026-09-26: Finally cloning and chain merging

Finally optimization now shares callfinally chains with a common continuation
and clones eligible handlers onto normal exit paths. When every normal call is
redirected, the original handler becomes a fault handler for exceptional exits.
Cloning preserves the native budget, profile-based path selection, EH ownership
and continuation updates.

Selected phase bodies match native for cloning, chain merging and rejection
decisions. Execution covers normal and exceptional cleanup and optimized
object/struct lifetimes across collections. This batch also corrects a
statement-versus-LIR emptiness mix-up that could incorrectly remove a throwing
try region before cloning. Cloned node IDs, profile diagnostics and a remaining
struct-finally code-size gap prevent a full dump/code parity claim.

## 2026-09-26: Empty exception-region cleanup

The native cleanup phases now remove empty finally/fault handlers, promote
finally bodies out of empty try regions, and remove catch/fault regions whose
try bodies cannot throw. They preserve continuations, surviving effects,
predecessor weights and EH-table nesting/index updates.

A three-method probe matches native across all cleanup rounds and the resulting
control-flow graphs, including a retained static increment. Established
optimized and minopts/GC-stress corpora continue to execute. The probe's empty
finally is consumed by the earlier empty-try pass, so positive execution of the
empty-finally phase and broader nested/profiled EH cases remain unverified.

## 2026-09-26: Forward substitution

Forward substitution now moves eligible expressions into their next uses,
removes temporary stores and clones cheap addresses for bounded multi-use
cases. It preserves evaluation and exception ordering, tree ownership and
local last-use information.

The range probe matches native allocation-call forwarding: the original call
moves directly into its destination store, the intermediate temporary disappears
and surrounding effects retain their order. The other three loop methods remain
unchanged in this phase. Established optimized and minopts/GC-stress corpora
execute; broader dump/code parity remains outstanding.

## 2026-09-26: Head and tail merging

Early and post-morph head/tail merging now share equivalent statement sequences
across predecessors and successors, including common return and throw tails.
The phase preserves native matching order, side-effect restrictions, EH regions
and flow weights.

Six Boolean/switch probes now have the same shared returns, SSA form and CSE
promotion choices as native, resolving the remaining input-shape differences
tracked in B300. Their reported code sizes also match; this does not establish
instruction or runtime-metadata parity. The two `Main` methods retain their
earlier size gaps, and diagnostic/tree-cost differences remain.

## 2026-09-26: Induction-variable optimization

Induction-variable optimization now strength-reduces derived expressions,
converts eligible loops to downward counting, widens profitable integer IVs
on Windows x64 and removes unused updates. Selection preserves native loop
order, exit handling and GC safety constraints for derived byrefs.

Range probes match native widening, byref start/step values, trip-count tests
and removal decisions. Array loads and externally visible stores retain their
ordering, including exception paths. Established optimized and GC-stress corpora
execute with the phase enabled. Broader dump/code parity and other-target
coverage remain outstanding.

## 2026-09-26: Scalar-evolution analysis

Scalar-evolution analysis now models loop-invariant expressions and induction
recurrences from SSA definitions. It simplifies those expressions, materializes
IR and value numbers, infers comparisons from dominating branches, and computes
loop exit counts with native width and overflow rules.

This completes the analysis prerequisite for induction-variable optimization.
The optimization phase itself remains inactive until its transformation closure
is integrated; production IV execution and dump parity are not yet established.

## 2026-09-26: Range-check cloning

Range-check cloning now replaces eligible groups of bounds checks with guarded
fast and fallback paths. It preserves native value-number grouping, selection
order and complexity limits. The fast path removes the selected checks while
the fallback retains their original exception behavior.

A four-access variable-index probe produces the same guards, edge likelihoods
and fast/fallback structure as native, retaining every array load and index
store. Both implementations preserve successful sums and null, negative,
short-array and overflowing-index exceptions. Established optimized and
GC-stress corpora continue to execute. Broader dump/code parity remains open.

## 2026-09-26: Flow optimization phases

Flow optimization now runs at the native early, pre-layout and post-layout
points. The early pass enables tail duplication and propagates cold-block
information; the pre-layout pass performs cleanup and profitable branch
duplication. After register allocation, reversible conditions and their existing
edges are flipped to favor fallthrough without introducing IR nodes.

Selected optimized and GC-stress corpora execute with these phases enabled,
including the array/span exception-ordering probes. Native comparisons confirm
cold-block marking, removal of six extra switch blocks, return-block compaction
and in-place branch reversals. Separate-return differences in six Boolean/switch
methods and broader generated-code parity remain outstanding.

## 2026-09-26: Bounds-check coalescing

Bounds-check coalescing now groups checks by block, conservative array-length
value number and side-effect barriers. It strengthens the first eligible check
to cover the group's highest index, allowing later assertion propagation to
remove redundant checks. Exception ordering and handler-visible stores constrain
which checks can share a group.

Selected corpora execute with the phase enabled. Native array and span probes
retain their checks because intervening ordering or exception barriers prevent
coalescing; positive strengthening is covered by focused tests, not yet by
native-host execution evidence.

## 2026-09-26: Early propagation

Early propagation now follows SSA copies back to constant array allocations and
removes bounds checks proved safe before value numbering. It also folds eligible
explicit null checks into later accesses while preserving exception ordering,
side-effect barriers and handler-visible stores.

The production phase is enabled in the established optimized execution corpora.
Range and fixed-array probes remove the same checks as native while preserving
the surrounding accesses and effects. Positive null-check-folding execution
coverage and full dump/code parity remain outstanding.

## 2026-09-26: Loop hoisting

Loop-invariant expressions now move into loop preheaders through the production
hoisting phase. Selection preserves native loop order, value-number and memory
dependencies, exception ordering and register-pressure limits. Cloned expressions
feed the subsequent CSE phase without removing their original uses prematurely.

The range probes hoist the same four expressions into the same preheaders as
native, restoring the previously missing CSE uses and weighted counts. Boolean
and switch probes likewise agree on not hoisting. Their earlier return-merging
differences remain; generated-code and full dump parity are still incomplete.

## 2026-09-26: CSE activation

Value-number CSE now runs through the production pipeline, selecting the configured
native policy, classifying definitions and uses, and rewriting profitable
expressions into SSA-repaired temporaries. Repeated invocations clear prior tree
markers. Optional emission metrics use the same policy and method-local counts
rather than rejecting the request.

The established optimized and GC-stress corpora execute with CSE enabled.
Native comparisons agree on decisions for matching input shapes; earlier return
merging and expression-occurrence differences still produce different candidates
and phi requirements. Broader dump and generated-code parity remain outstanding.

## 2026-09-26: Learning-based CSE policies

The Debug RLHook and RL policies now implement configured decisions, feature
reporting, stochastic selection and policy-gradient updates using the shared
candidate model. Choice order, numeric policy and update diagnostics follow
the native implementations.

All CSE policy families are implemented. Production phase activation and optional
emission metrics still require their integrated execution boundary.

## 2026-09-26: Random and replay CSE

Debug CSE policies now support native-seeded random selection and configured
candidate replay. Replay and learning-based modes share the native integer
configuration parser, including its adjacent-minus tokenization, wrapping
arithmetic and persistent sign behavior.

These complete policy prerequisites do not activate production CSE. Learning-based
modes and the full phase dispatcher remain the next integration boundary.

## 2026-09-26: Parameterized CSE

Parameterized CSE now computes the native feature vector and scores candidate
choices for greedy selection. Stopping decisions, tie-breaking, choice order and
rebuilding after use-count changes retain native behavior. The same choice and
feature representation is available to the remaining learning-based modes.

The production CSE phase remains inactive until the remaining modes and their
dispatch are integrated.

## 2026-09-26: Standard CSE selection and rewriting

The standard CSE heuristic now ranks and selects candidates, creates temporary
definitions, and rewrites uses with incremental SSA and value-number repairs.
Its shared initialization and diagnostics follow native dispatch and ordering.
Register-state copying also preserves the native cross-operator replacement
contract without broadening platform-specific multi-register behavior.

The production CSE phase remains inactive while the other native heuristic
families are completed. Existing backend execution is preserved; this is not
evidence of active CSE optimization or broader platform parity.

## 2026-09-26: Loop cloning

Loop cloning now creates guarded fast paths while retaining checked slow copies,
and applies optimizations whose conditions can be proven without cloning.
Candidate discovery, profitability, ordered guards, EH extents and profile
invalidation follow the native phase.

The established optimized and GC-stress corpora still execute. The array corpus
matches native clone decisions, guards and fast/slow block structure. Its recorded
bounds-check removal placement difference is resolved. Broader candidate, EH and
generated-code parity remain outstanding.

## 2026-09-26: Incremental SSA

Incremental SSA now records inserted definitions and uses, propagates liveness
backward, and creates phi definitions as needed by expression rewrites. Block/local
keys retain block identity even when numbering changes, and new definitions start
with the native unset value numbers.

This prepares CSE rewriting without activating the CSE phase. Broader EH and
cyclic-phi behavior still needs execution coverage.

## 2026-09-26: CSE cost and rewrite prerequisites

CSE support now initializes the native cost model, orders candidates by execution
or size cost, and extracts retained side effects when removing CSE uses. Weighted
use counts, nested definitions, evaluation order and comma value numbers are
preserved. Candidate selection and SSA rewriting remain separate prerequisites;
the production CSE phase is still inactive.

## 2026-09-26: If-conversion

If-conversion now replaces eligible conditional stores and return diamonds with
select expressions, preserving native profitability checks, evaluation order,
profile updates and reachability limits. The shared reachability helper accepts
the native null-merge case without skipping scratch-state initialization.

Optimized and GC-stress execution remains intact. Three focused return shapes
now match native emitted bytes and evaluation costs across both arms, integer
overflow and independent operands. The comparison also exposed and corrected a
shared cost-calculation bug that discarded computed special-operator costs and
evaluation levels. Broader select coverage and complete dump/code parity remain
outstanding.

## 2026-09-26: Switch recognition and dominant-case peeling

Switch recognition now converts eligible comparison chains into switches and
peels profiled dominant cases while evaluating the switch value once. Signed
normalization, predecessor multiplicities and profile redistribution follow
native behavior.

A targeted execution probe converts the same four blocks as native and preserves
results and side effects across sparse, signed-offset and integer-boundary cases.
Existing optimized and GC-stress execution remains intact. The new control flow
also exposed a block-dump padding failure, now corrected to preserve native
`printf` behavior for negative field widths. Full code parity and runtime
coverage of profiled dominant-case peeling remain outstanding.

## 2026-09-26: Boolean optimization

Boolean optimization now folds adjacent conditions and range tests, combines
eligible compare chains, and preserves native branch polarity and profile
updates. Its APX path uses the complete switch-detection mode needed to avoid
inappropriate compare chaining; switch conversion remains separate.

A targeted execution probe performs the same four Boolean folds as native while
preserving results and side effects across integer boundary inputs. The established
optimized and GC-stress cases still execute, and `Counted` and `Nested` retain
their native-matching bodies. Full generated-code parity and broader APX coverage
remain outstanding.

## 2026-09-26: CSE candidate and dataflow support

Common-subexpression elimination now has native-order candidate discovery,
hashing and indexing, tree eligibility checks, and forward availability analysis.
Hash buckets and candidate tables share descriptor identity and preserve the
order of expression occurrences.

This prepares the selection and rewriting machinery; the production CSE phase
remains inactive, so no generated-code improvement is claimed yet.

## 2026-09-26: Loop-cloning models and guards

Loop-cloning support now represents array, span, type-test and delegate-target
candidates, simplifies symbolic conditions, and constructs ordered null and
bounds guards with short-circuit control flow. Candidate descriptors preserve
native value-copy behavior while retaining references to the original IR.

These are prerequisites, not an active cloning pass. Candidate discovery,
fast-path transformations and the complete phase driver remain to be integrated.

## 2026-09-26: Redundant-branch optimization

Redundant-branch optimization now performs native dominator inference, local
comparison forwarding and jump threading, including SSA/phi-use repair and
edge, profile and exception-region handling. Its VN relation helpers preserve
signed, unsigned and unordered comparison distinctions.

The phase simplifies selected loop and array methods while preserving established
execution, including optimized unrolled loops under GCStress4. It also invalidates
DFS/SSA analysis when no branch changes, matching native and removing stale
SSA-memory annotations from the `Counted` dump. `Counted` and `Nested` retain
their native-matching code bodies; complex global-phi paths and full parity
still need broader coverage.

## 2026-09-26: Native loop unrolling

Loop unrolling now uses native iteration proofs, growth limits and duplication
rules, including secondary exits, nested-loop retries and unreachable-loop
cleanup. `Nested` shrinks from 32 to 29 bytes and now matches native's complete
machine-code body; `Counted` retains its matching 17-byte body. Other code and
diagnostic differences remain, and no throughput improvement is claimed.

Optimized unrolled loops execute through forced collections and GCStress4.
Activation also exposed scalar constant folding during threaded assertion
remorphing; replacement now carries the existing threading context while owners
and remorphing retain responsibility for links. EH-region duplication is ported
but still lacks focused execution evidence.

## 2026-09-26: VN-based dead-store removal

VN-based dead-store removal now applies native conservative-value equality to
full and partial local stores. It retains explicit initialization, the
first-primitive-definition profitability rule, composite definitions and async
byrefs that may cross suspension points.

Removed stores become comma expressions that preserve evaluation effects and
logical tree identity. Their detached, typed SSA-node references are cleared
after SSA invalidation; the descriptors retain the values needed by subsequent
comparisons within the pass. Established optimized and minopts/GC-stress
execution remains intact. Focused runtime candidates also preserve volatile
observations and checked-overflow behavior, but neither JIT removes their
stores; successful removal is currently covered by IR regressions rather
than runtime removal parity.

## 2026-09-26: VN copy propagation

VN copy propagation now rewrites equivalent local uses using native dominance,
liveness, type and profitability rules. Definition stacks preserve live SSA
identity and sibling-block isolation; candidate iteration retains native hash
order, and rewritten statements use the shared side-effect repair path.

The phase performs substitutions in the loop and array corpora while preserving
their results and established minopts/GC-stress execution. The aligned-loop
machine-code bodies remain unchanged, including `Counted`'s native-matching
17 bytes; this activation does not establish a size or throughput improvement.
Redundant-branch optimization and VN-based dead-store removal remain inactive.

## 2026-09-26: Range-check elimination

Range analysis now drives the native bounds-check elimination phase after
assertion propagation. Constant and symbolic bounds proofs retain native
overflow rules and analysis budgets. Removing a check preserves required
index/length side effects and repairs ancestor flags and statement threading.

The phase retains its bounds-check and completed-SSA gates. Established optimized,
minopts and GC-stress execution remains intact; those corpora do not demonstrate
additional check removal by this phase. A dedicated array corpus exercises
managed check removal while preserving results, exceptions and pre-throw side
effects. Native removes the corresponding loop check earlier, so matching
transformation placement is not established. VN copy propagation remains the
next integration boundary, and full dump/code parity is still open.

## 2026-09-26: Assertion-propagation phase activation

Global assertion propagation now runs the complete discovery, predicate-sensitive
dataflow and application sequence, including switch-derived facts and statement
remorphing. Conditional folding repairs outgoing facts for the retained edge;
local-mode dispatch retains its nullable statement/block contract.

The established optimized corpora execute with assertion propagation enabled,
while minopts and GC-stress execution remain intact. `Counted` now uses native's
zero-based entry test and emits the same 17-byte body. `Nested` shrinks from
38 to 32 bytes, compared with native's 29; this is a size result, not a throughput
claim. Full dump and code parity remain open, including cloned tree IDs and
SSA-memory annotations.

## 2026-09-26: Arithmetic assertions and statement propagation

Assertion application now handles checked arithmetic, division and modulo,
casts and bounds checks using the native range proofs and fault conditions.
The VN statement visitor preserves execution order, updates owning uses and
non-null facts, and repairs traversal after remorphing removes statements.

Global assertion propagation remains inactive. Switch-derived assertions and
the complete phase orchestration still need integrated execution evidence.

## 2026-09-26: VN-based folding and statement remorphing

VN-based folding now handles proven constants, memory-intrinsic simplification
and conditional propagation while retaining native side-effect and exception
ordering. Statement remorphing uses the native insertion-time path, including
removal and control-flow updates, rather than rerunning whole blocks.

These complete the folding and morphing prerequisites for global assertion
propagation. Arithmetic application, switch-derived assertions and statement
visitors still need integration before activating the full phase.

## 2026-09-26: Range analysis and assertion dataflow

Range analysis now follows SSA definitions and edge assertions, including
symbolic bounds, overflow, widening and cyclic phis. Relational assertion
application uses those proofs while preserving floating-point comparisons and
side effects. Signed long-shift ranges retain the subsequent assertion
refinement, and floating assertion diagnostics use native spellings.

Forward dataflow now visits reachable blocks in reverse postorder, iterates
changed cyclic graphs and merges exception handlers from their try-entry facts.
Assertion initialization and generation retain separate true/false edge sets.
These are prerequisites, not activation of global assertion propagation or
range-check elimination; arithmetic application, folding and phase integration
remain.

## 2026-09-26: Value-numbering phase activation

The full VN phase now numbers initial SSA values, local and memory phis, loop
effects and statement trees in native order. Loop-header refinement retains
invariant values, and modified-location maps preserve native hash traversal
when allocating new value numbers.

The established scalar, loop, object and hardware corpora execute with VN
enabled, including optimized and minopts GC stress. `Counted` now receives
native's zero value number for its entry induction-variable read; assertion
propagation remains necessary to substitute that value into the comparison.

VN diagnostics use native operator names rather than enum prefixes. Full dump
and code parity remain open: raw loop comparisons still differ in clone IDs or
block traversal, and some generated bodies differ in size.

## 2026-09-26: VN tree and intrinsic evaluation

The complete Windows-x64 tree-numbering dispatcher now connects arithmetic,
memory, calls, casts and hardware intrinsics. Math and SIMD evaluators retain
native constant-folding rules and exception propagation. Reachability tracking
uses normal liberal values while preserving shared conditional edges.

The phase entrypoint remains inactive. Loop-side-effect analysis and
block/phi orchestration must be integrated before testing actual VN-phase
execution and its effect on optimized code.

## 2026-09-26: VN memory accesses and calls

Value numbering now models local, field, array and byref loads and stores,
including physical memory maps and immutable-data reads. Pointer extensions
retain field/array identity, offsets and exception sets. Call numbering preserves
helper semantics, allocation identity and memory effects.

The native load-offset truncation order is preserved even for offsets outside
32 bits. Debug VN annotations can check both complete and exception-normalized
values. These are evaluation prerequisites; the full VN phase remains inactive
while intrinsic, tree and loop traversal integration continues.

## 2026-09-26: VN exceptions and array-address reconstruction

Value-numbering support now preserves paired normal and exceptional values
through numeric casts, bitcasts, arithmetic, bounds checks and indirections.
Heap and address-exposed memory updates retain their native shared or separate
SSA state.

Array-address reconstruction recovers element indices from scaled byte offsets
without discarding constant contributions. Unparseable addresses preserve the
caller's index result and report no array, matching native behavior.

These are prerequisites for tree evaluation. The full VN phase remains inactive;
memory-access, intrinsic and call evaluation still require integration.

Local SSA definitions now start with the native unset value-number pair rather
than zero, which denotes null. This prevents unnumbered loop inputs from looking
like existing constants during phi processing.

## 2026-09-26: Memory value numbering and diagnostics

Value numbering now has precise and physical memory-map operations, including
local and memory-SSA phi traversal, fixed-point limits and loop dependencies.
SSA memory definitions start with the native unset value-number pair.

Diagnostic support covers constants, expressions, maps and phis, retaining
native floating-point special-value text. The value-numbering phase itself
remains inactive; these prerequisites do not establish optimized-code or
phase-dump parity.

## 2026-09-26: Object and array stack allocation

Object-allocation orchestration now runs escape analysis, conditional cloning,
allocation morphing and pointer/use repair in native order. Stack-array helper
expansion initializes method-table and length fields, preserves evaluation order
and replaces each helper with its stack address.

Real nonescaping classes and fixed-size value arrays execute on the stack.
Reference-lifetime, boxed-value and escaping controls retain native heap
decisions. The object and hardware corpora execute together, including optimized
and minopts GC stress. Remaining work includes conditional-clone execution,
additional EE layouts and optimizer-driven code differences.

Threaded scalar zero initialization now uses the existing whole-node replacement
path with an explicit threading mode, preserving node identity and links.

## 2026-09-26: Hardware import and GS protection

Windows-x64 hardware import now dispatches portable and platform intrinsics,
preserving argument order, precise operand types, immediate fallback and deferred
call handling. GS preparation initializes security cookies and shadows vulnerable
parameters before code generation, including copies back for jump tailcalls.

Nonconstant vector construction, addition, lane access, shifts, BMI extraction
and CRC execute managed-generated code. Six hardware helpers match native bytes;
the stack-buffer load/store case executes but still differs in code generation.
The constant `FoldHardware` method now matches native at four bytes, down from
726. These results do not establish general SIMD or dump parity.

## 2026-09-26: Object cloning and stack-use rewriting

Conditional object cloning now specializes fast and slow paths while preserving
profiles and cloned node identity. Allocation morphing and use rewriting can
construct stack homes, update pointer/layout types and preserve write-barrier
and side-effect rules.

Production stack allocation remains gated until phase orchestration and real
JIT/EE-backed execution are established, including arrays, boxed layouts,
unboxing and delegate paths.

## 2026-09-26: Portable and xarch hardware-import paths

The portable vector and xarch-special import implementations now have their
SIMD constructor dependencies, including conversions, saturation, lane
rearrangement, reductions and memory operations. Import paths preserve native
ISA checks, operand evaluation order and fallback decisions.

The generic hardware dispatcher remains inactive. Structural coverage of these
primitives does not establish executed SIMD-result or native-host parity.

## 2026-09-26: Hardware creation and fallback prerequisites

Hardware import now has native SIMD creation, nonconstant shift/rotate fallback,
signature-derived vector widths and AVX-only compatibility lookup. These
primitives preserve operand-pop order and constant lane construction.

The complete generic, xarch-special and portable import bodies still require
their constructor dependencies before the dispatcher can be activated.

## 2026-09-26: Loop inversion and iteration analysis

Top-tested loops can now duplicate their entry conditions using native size,
cost and profile heuristics. Iteration analysis recognizes induction-variable
tests so already bottom-tested loops retain their shape. Inversion preserves
EH boundaries, repairs edges and profiles, and rebuilds loop information.

Selected optimized methods execute with this phase active, including call-free
and collecting loops. Their machine code still differs from the native oracle;
remaining optimizer work and code parity are not implied by execution.

The remaining scalar entry-test difference requires value numbering and
assertion propagation. Memory-PHI storage and loop-ownership queries are now
available as prerequisites; those optimization phases are not yet active.

## 2026-09-26: Allocation-site analysis and EH cloning

Object-allocation analysis now walks allocation sites and aliases, checks stack
viability, and records guarded enumerator uses and clone appearances. The EH
cloner can insert complete try regions, including nested handlers, filters and
callfinally pairs, while preserving successor maps and block state.

Object-specific clone transformations and stack/heap morphing still gate stack
allocation. The existing heap-only production path remains unchanged.

## 2026-09-26: Hardware argument normalization

Hardware-intrinsic arguments now use native SIMD/mask stack normalization,
including call-like struct results that need return buffers. Scalar arguments
retain the native implicit-coercion check and invalid-IL failure behavior.

The complete generic and special hardware-import paths are still required
before activating the dispatcher.

## 2026-09-26: Hardware-import prerequisites

Hardware-intrinsic import now has native argument-signature reading, table-driven
eligibility, element-type checks, xarch immediate discovery and conditional
immediate range checks. Argument order and precise unsigned types are preserved.

The importer itself remains unported. SIMD signature recognition, stack operand
handling and the complete special-import/fallback paths are still required;
these prerequisites do not reduce the existing hardware-code differences.

## 2026-09-26: Conditional escape and clone analysis

Object-allocation analysis now propagates escapes, recognizes cloning guards,
and evaluates clone regions for overlap, profitability and legal control flow.
EH clone feasibility includes nested and mutually protecting regions, filters,
handlers and callfinally pairs without changing the graph or EH table.

These complete analysis functions do not enable stack allocation. Allocation-site
walking, actual cloning and stack/heap morphing remain required before the
existing phase gate can be removed.

## 2026-09-26: Optimized native-code execution

The Windows-x64 optimized pipeline now emits and executes the standard corpus
and focused GC-loop and call-free arithmetic cases. Loop-alignment placement
follows native loop
eligibility, normalized weight thresholds, EH exclusions and hidden-padding
selection. Checked allocation also preserves the native spill-weight cursor;
using the traversal cursor had incorrectly rejected a live delayed use.
Call-free execution covers zero-padding decisions and emitted adaptive padding,
including the actual three-byte x64 NOP sequence.

The established minopts GC-stress cases remain executable. This is an execution
milestone, not dump or machine-code parity: hardware-intrinsic import still
produces substantially different code, and other optimization and diagnostic
differences remain.

## 2026-09-25: Full register-allocation phase

The native Windows-x64 allocation traversal is now connected to interval
construction and resolution. Optimized compilation and enregistered locals use
the full allocator; ordinary minopts retains its existing path. The traversal
preserves entry assignments, block maps, spills, copies, delayed frees and
upper-vector handling.

The optimized pipeline now passes allocation and its IR checks. Enabled
loop-alignment placement is the next boundary before code generation; optimized
execution is not yet established. All 32 established minopts methods continue
to execute managed-generated native code under GC stress.

## 2026-09-25: Object-allocation graph foundations

Object allocation now has native connection-graph preparation and closure:
tracked-local indexing, reserved clone and pseudo indices, unknown-source
tracking, and stack-pointer propagation. Preparation retains the native
configuration, OSR and field-tracking rules.

These are prerequisites for escape analysis, not enabled stack allocation.
Escape discovery and clone viability remain unported, and the existing
heap-only execution path is unchanged.

## 2026-09-25: Enregistered-local resolution

Windows-x64 LSRA now replays local-enabled register assignments into the IR,
including parameter and dummy definitions, temporary spills and reloads, and
upper-vector saves and restores. It reconciles block edges, finalizes local
register and stack homes, and checks the resulting allocation with the native
verification rules.

The resolver remains a prerequisite, not an activation of optimized execution:
the full allocation traversal must be integrated before the production gate is
removed. The existing minopts resolver is unchanged.

## 2026-09-25: IR and SSA phase verification

Native CHECK_IR verification is now active: tree phases check types, side-effect
flags and statement links; linear IR checks use/definition ordering, threading
and local semantics. SSA verification runs afterward at the native phase
boundary, including after loop canonicalization and SSA construction.

The checks preserve native relaxed-mode notices rather than suppressing them.
Selected minopts execution remains intact. Hardware-intrinsic import differences
still produce additional flag notices, so this does not establish full
diagnostic parity.

## 2026-09-25: Loop canonicalization and block weights

The optimized pipeline now discovers and compacts natural loops, creates
preheaders, merges backedges and splits exits in native order. It preserves EH
regions when placing new blocks or splitting loop headers, then recomputes
invalidated loop information before later phases use it.

Block weighting now receives that required loop state and follows native
profile or heuristic weighting. Optimized corpus methods proceed through these
phases to the existing allocation boundary; selected minopts execution remains
intact. Profile repair and the remaining loop optimizations are still separate
work.

## 2026-09-25: General register selection and assignment

Windows-x64 LSRA now selects registers using the full native heuristic sequence,
including related-interval preferences, call preservation, fixed-register
conflicts and spill costs. Assignment preserves inactive register histories
and constant reuse, including integer-width restrictions, signed zero and NaN
payloads. Copy-register assignment retains the interval's original home.

These are prerequisites for the optimized allocation traversal. The production
minopts path is unchanged, and optimized allocation remains explicitly gated
until its complete allocation and resolution drivers are integrated.

## 2026-09-25: Register resolution across edges

Windows-x64 LSRA now reconciles local register homes across split, join and
critical edges. It orders parallel moves, breaks cycles with swaps, scratch
registers or spills, and preserves EH write-through homes and split-block
location maps. Scratch selection shares the allocator's native stress policy.

This completes the edge-resolution prerequisite without activating optimized
allocation or changing the production minopts allocator.

## 2026-09-25: SSA construction

The optimized pipeline now constructs SSA: it inserts local and memory PHIs,
renames definitions and uses, propagates names through EH regions, and supports
deep rebuilds. The driver preserves native liveness and zero-initialization
cleanup order. Retiring obsolete local-thread links also lets implicit-byref
morphing perform managed whole-node replacements safely.

Standard, loop and EH methods now finish managed SSA and reach the explicit
optimized-allocation boundary. Matching pre-SSA IR produces matching post-SSA
IR in the selected comparisons; broader input differences and the unfinished
general IR verifier still prevent full diagnostic parity. The established
minopts corpus continues to execute managed-generated code under GC stress.

Standalone SSA checking now cross-checks definitions, uses, PHIs and descriptor
flags, including the results of full builds and rebuilds. Its native notices
and failures are preserved; production CHECK_IR integration still requires the
remaining general flag, type and LIR checks.

## 2026-09-25: Struct-store lowering continuity

Lowering now retains the live traversal cursor when scalarizing a struct store
replaces its node. Diagnostics also use the replacement rather than the
detached original. This corrects the observed optimized `FoldHardware` lowering
failure without emulating native node bashing or adding Release-only overhead
for diagnostics.

## 2026-09-25: Critical-edge splitting

The flow graph can now split an edge while preserving EH placement,
predecessor multiplicity, profile weight and live sets. Adjacent edges extend
their region directly; other edges use the existing region-aware insertion.
This supplies the graph operation needed by optimized LSRA edge resolution.

## 2026-09-25: Enregistered-local reference resolution

Windows-x64 LSRA now resolves individual local references, preserving register
homes, copy registers, fixed-register moves, spill/reload flags and EH
write-through behavior. Promoted multi-register fields and single-definition
spills retain their native handling.

This completes another prerequisite for optimized allocation, not its
activation. The production minopts allocator remains unchanged.

## 2026-09-25: SSA graph, memory and zero-initialization prerequisites

The dominator phase now marks blocks dominated by exceptional entries. Shared
successor traversal preserves regular and EH ordering, while memory SSA maps
retain native inline-root ownership and GC-heap/byref aliasing.

Redundant zero-initialization cleanup now handles first references, promoted
fields, EH restrictions and potential GC safe points. These are prerequisites
for the full SSA builder; PHI insertion and renaming are not yet activated.
The established minopts corpus continues to execute managed-generated code
under GC stress.

## 2026-09-25: Dominator trees and dominance frontiers

The flow graph now supports native immediate-dominator construction, ordered
dominance frontiers and iterated frontiers for PHI placement. EH entries retain
their distinct dominance predecessors, and dominator traversal preserves native
entry/exit order without recursion or per-walk allocation.

SSA construction still needs zero-initialization cleanup, PHI insertion and the
renaming driver. These graph algorithms do not activate incomplete optimization
phases or change the established minopts execution path.

## 2026-09-25: Enregistered-local block locations

Windows-x64 LSRA now preserves local register homes across block boundaries,
including predecessor changes, EH stack homes, delayed spills and copy-register
references. Allocation and resolution retain their distinct map-update rules;
dead register occupants and upper-vector state follow native handling.

These routines remain prerequisites for optimized allocation. The production
minopts allocator and its execution boundary are unchanged.

## 2026-09-25: SSA rename state and diagnostics

SSA renaming now has native block-scoped local and memory stacks, including
same-block definition replacement, restoration on dominator-tree exit and
reuse of popped entries. SSA lifetime summaries and annotation-constraint
checks are also implemented, retaining full-width label identity and native
diagnostic formatting.

Full SSA construction still requires dominator/frontier construction,
zero-initialization cleanup, PHI insertion and the renaming driver.

## 2026-09-25: Complete memory-kind iteration

Memory-kind iteration now includes byref-exposed memory before the GC heap,
matching native order. The previous enumerator skipped its first element,
which omitted byref state from shared liveness and SSA bookkeeping loops.

The established 32-method minopts corpus continues to execute managed-generated
code under GC stress, including loop-carried references, interior and pinned
references, catches, filters and finally paths. This is scoped execution
coverage, not full SSA or code-generation parity.

## 2026-09-25: Enregistered-local interval construction

Windows-x64 LSRA can now construct intervals for enregistered locals, including
parameter definitions, predecessor-dependent live-ins, EH stack homes,
upper-vector restores and exposed uses at backedges and method jumps. Native
reference-location ordering, last-use handling and write-through preferences
are retained.

The production allocator still uses the established minopts path. Optimized
allocation and resolution must be completed before this builder is activated.

## 2026-09-25: Finally block-placement fidelity

Finally-call blocks now retain the native insertion positions when an EH-region
walk reaches the main method body. The enclosing-region helper preserves its
incoming region kind instead of overwriting it at the end of the walk.

This removes three extra bytes from each of the selected finally and
nested-finally methods. Their insertion choices, branch forms, instruction
counts and code sizes now match native, while the existing exceptional-path
and collection checks continue to pass. Full byte-for-byte and general EH
parity remain separate work.

## 2026-09-25: Composite SSA bookkeeping

Promoted-struct SSA numbers now preserve field values when moving from compact
packing into outlined storage, including growth, storage reuse and aliased
updates. Stress encoding retains native unsigned arithmetic. Memory PHIs now
distinguish an absent definition from a definition awaiting arguments.

These repairs complete bookkeeping prerequisites; full SSA construction and
optimized allocation remain incomplete.

## 2026-09-25: SSA reset

SSA reset now implements both native modes: removing PHI functions alone and
clearing definition tables, memory maps, composite-number storage and retained
local-node SSA numbers before rebuilding. PHI-prefix removal preserves the
remaining statement links without changing unrelated flowgraph flags.

Full SSA construction is still incomplete. Separate composite-number growth
and empty-memory-PHI sentinel defects remain tracked for that work.

## 2026-09-25: Higher-arity value numbers

Three- and four-operand value-number functions now use native ordered interning
and the existing chunk storage. This includes variable-arity SIMD attributes
and the special fourth-argument contract for memory-map stores. These overloads
do not perform constant folding; map-selection algorithms and the full
value-numbering phase remain incomplete.

## 2026-09-25: Filters and nested finally execution

Selected minopts methods now execute accepted and rejected exception filters
and normal and exceptional nested-finally paths while retaining references
across confirmed full collections. Values and identities match the native
runs. A remaining nested-finally code-size difference is recorded separately
from this execution result.

## 2026-09-25: SSA tree liveness

The SSA liveness policy now performs native memory/local dataflow and backward
analysis of non-phi statements, including dead-store elimination, retained side
effects and EH keep-alive requirements. Interior rewrites preserve logical node
identity and metadata, update aliased owners and retain native diagnostics.

Early-tree and post-lowering liveness remain separate policy specializations.
This completes SSA-policy liveness, not full SSA construction or optimized
compilation.

## 2026-09-25: Entry-local allocation definitions

Register-allocation prerequisites now include initial parameter definitions and
entry-local initialization references. Parameters retain their incoming-register
preferences; live-in GC references and `initlocals` values receive zero
definitions, while potentially undefined non-GC locals keep stack homes.
OSR initialization and finally-local deduplication follow native rules.

These helpers do not activate enregistered-local allocation; complete optimized
interval construction, allocation and resolution remain required.

## 2026-09-25: Binary value-number interning

Binary expressions now use native value-number interning, constant folding,
algebraic identities and related-comparison rules. Cast and runtime-type
comparisons preserve the runtime's definite and unknown answers, including
exact-type restrictions and exception values. Constant creation retains native
ordering so equivalent results do not silently renumber later expressions.

This completes another prerequisite for optimized compilation; the full
value-numbering phase is not yet active.

## 2026-09-25: Catch and finally execution

Selected minopts methods now execute normal and exceptional catch/finally paths
while preserving objects and reference-bearing structs across confirmed full
collections. Both native and managed-generated code retain the expected values
and identities. Generated sizes still differ for two of the three methods;
this establishes focused EH/GC execution, not code or full EH parity.

## 2026-09-25: References across loop backedges

Selected minopts methods now retain objects, array interiors, conditional
references and reference-bearing structs across loop backedges and confirmed
full collections. Conditional stack merging exposed a temporary-allocation
defect: allocating multiple locals initialized only the first descriptor.
Every descriptor now receives the native initial type, temporary flag and
stack-home state.

The loop cases execute managed-generated code under GC stress, alongside the
existing scalar and interior/pinning cases. This extends execution coverage;
it does not establish general GC, optimized allocation or dump/code parity.

## 2026-09-25: Local register-candidate construction

The optimized-allocation prerequisites now include native local eligibility,
candidate interval creation, EH write-through/spill marking, collective promoted
field rejection, FP callee-save preferences and large-vector upper-save
intervals. FP preferences retain the native weighted-reference thresholds,
register-argument adjustment and single-exit loop heuristic.

The existing minopts path is unchanged. Optimized interval construction,
allocation and resolution still need their remaining closure; the production
allocator continues to reject optimized/enregistered-local compilation explicitly.

## 2026-09-25: Early tree liveness

Early liveness now follows the native optimized-tree policy: it computes local
use/def and inter-block liveness, preserves exception-handler keepalives, removes
dead stores without losing side effects, and repeats when removal changes live
sets. Conditional definitions inside a qmark do not kill another branch's uses.

The native minopts and debug method-range gates remain intact. Existing minopts
execution and interior-reference/pinning behavior remain intact under GC stress.
SSA liveness and optimized allocation are still separate boundaries; this phase
alone does not enable optimized execution.

## 2026-09-25: Binary value-number constant folding

Value numbering now has native binary folding eligibility, exception and
overflow guards, scalar constant evaluation, numeric casts and bitcasts.
Relocatable handles retain their operation restrictions; mixed operands,
signed zero, NaN payloads and narrowing follow the native contracts.

Binary function interning, algebraic identities, runtime type comparisons and
the full value-numbering phase remain separate prerequisites. This does not
enable optimized execution.

## 2026-09-25: Interior-reference and pinning execution

Focused Windows-x64 minopts cases now preserve field and array interior
references across confirmed full collections. A pinned-array case also checks
address stability while pinned, then verifies writes and reference identity.
All three execute with JIT-instruction GC stress enabled.

These cases exposed two diagnostic translation defects: GC-qualified LEAs
incorrectly entered the ordinary-size branch, and block-header flags used checked
truncation instead of native unsigned word extraction. Both are corrected
without changing GC metadata. The fixtures do not establish exclusive rooting
or measure object movement; broad GC correctness remains unproved.

## 2026-09-25: Lifetime-enabled lowering cleanup

Windows-x64 lowering now supports both native local-lifetime modes. The
lifetime-enabled path runs dead-code liveness and flowgraph cleanup, refreshes
reachability and reruns liveness when the graph changes, then recomputes local
reference counts.

Minopts execution remains intact under GC stress, including the focused
forced-collection reference cases. Optimized register allocation remains a
separate explicit boundary; completing lowering does not enable an optimized JIT.

## 2026-09-25: Binary value-number evaluation primitives

Value numbering now has complete scalar binary arithmetic and comparison
primitives for the native integer, native-sized and floating-point
instantiations. They preserve wrapping arithmetic, unsigned ordering, valid
division and checked-overflow preconditions, signed zero and target-specific
NaN behavior.

The binary folding dispatcher and value-numbering phase remain unactivated.
These primitives are prerequisites, not evidence of optimized execution.

## 2026-09-25: Hash-vector set operations

Sparse hash vectors now support native AND, OR, subtraction, comparison,
independent copying and compound set operations across equal and unequal bucket
counts. The implementation preserves traversal order, change flags and the
observable distinction between absent and physically present empty nodes.

Vector XOR and intersection remain deferred because the pinned native paths
contain a dropped-node link and a nonadvancing traversal, respectively.
Node-level operations are independent and available. This completes another
optimization prerequisite without activating an optimization phase.

## 2026-09-25: Initial GC-stress execution and backend diagnostic parity

The twenty-method Windows-x64 minopts corpus also executes with JIT-instruction
GC stress enabled. A separate three-method probe preserves object, graph and
struct references across confirmed full collections, checking payload and
reference identity afterward. These are focused execution results, not broad
runtime or GC correctness guarantees.

Block-mapping and unwind-allocation diagnostics now match native formatting.
Nineteen emission phase bodies and ten metadata phase bodies match exactly.
Remaining generation differences include native minopts diagnostics reading
uninitialized estimates; the port does not fabricate native memory-poison values.

The six address-sensitive method streams differ only inside decoded address
fields. Relocation records validate those fields, but indirect-call cell contents
and absolute target identities remain unproved. Hardware import still produces
different code. Exact dump and machine-code parity remain incomplete.

## 2026-09-25: First managed native-code execution

The complete Windows-x64 generation driver now runs generation, emission and
runtime-metadata phases, then reports successful compilation to the runtime.
The minopts corpus executes all twenty selected methods using managed-generated
code, including ordinary and indirect calls, signed-zero arithmetic, exception
handling, synchronization, P/Invoke, reverse P/Invoke and implicit-byref arguments.

Native code sizes match for nineteen methods; thirteen raw instruction streams
are byte-for-byte equal across the captured processes. Other streams still need
relocation-aware comparison, and the hardware-intrinsic case retains a different
implementation. This is a first execution milestone, not full backend or GC-stress
parity. Remaining dump differences and broader runtime coverage are the next
validation boundary.

## 2026-09-25: GC encoding and final runtime metadata

Windows-x64 GC metadata now covers header fields, register and stack slots,
filter lifetimes, call-site states and fully interruptible ranges using native
compression and ordering. Final publication connects unwind, instruction
mappings, rich and async debug data, scopes, EH clauses and GC info in native
order, followed by spill and temporary cleanup. Async diagnostics remain valid
after ownership of their buffers transfers to the runtime.

Immediate instruction and instruction-group diagnostics now accompany ordinary
emission, removing the former verbose-JitDump restriction. Optional CoreDisTools
late disassembly still rejects explicitly because its buffered API does not
preserve decoder-error output; no diagnostic difference is accepted.

The top-level generation driver remains the next activation boundary. These
components do not yet establish managed production execution or whole-pipeline
parity.

## 2026-09-25: Generation phases and EH publication

The Windows-x64 generation and emission phase bodies now connect frame
finalization, instruction recording, prolog/epilog materialization, jump binding,
unwind reservation and final byte output in native order. Forced fallback occurs
before runtime allocation. EH publication preserves VM clause ordering,
same-try identity, final code offsets and outgoing diagnostics.

The GC encoder's bitstream foundation preserves native fixed-width and
variable-length encodings. Full GC metadata construction and final metadata
orchestration, including late disassembly, still precede production activation.
The top-level generation boundary remains explicitly skipped; these phase bodies
do not yet constitute an executing managed JIT.

## 2026-09-25: Final instruction emission and executable allocation

Saved Windows-x64 instruction groups can now be issued into EE-allocated hot
and cold code buffers, with writable aliases and aligned constant-data sections.
The driver preserves per-instruction size corrections, forward-branch fixups,
GC lifetime boundaries and unused-buffer padding. Managed tracking tables stay
stable across allocation callbacks and garbage collections.

Instruction performance scoring uses the native width- and memory-sensitive
algorithms and generated latency/throughput metadata. Generation/emission phase
orchestration and the remaining runtime metadata still precede production
activation; standalone emission is not yet managed JIT execution.

## 2026-09-25: Instruction dispatch and GC/data output

Recorded Windows-x64 instructions now have a complete byte-output dispatcher.
Calls preserve stack-variable deaths at the call start and register transitions
at the call end, with compact and full GC records, byref returns and async
continuations. Removed jumps and loop-alignment compensation retain their
native byte and descriptor behavior.

Constant-data output covers raw data, absolute and relative label tables, and
async resume records. Instruction and GC diagnostics accompany byte output.
The final emission driver, executable allocation and remaining runtime metadata
still precede production activation; these capabilities do not yet establish
managed execution or whole-method codegen parity.

## 2026-09-25: Memory and branch byte encoding

Windows-x64 byte output now covers indirect memory, stack locals and spill temps,
static fields and constant data, including SIMD/APX addressing and compressed
displacements. GC stack lifetimes and outgoing pointer arguments retain their
native code offsets and tracking rules.

Label branches and calls, label-address loads and stores, and alignment padding
now have byte-output support. Relocations preserve immediate-width adjustments,
hot/cold targets and writable aliases on either side of executable memory.
Instruction dispatch, the final emission driver, executable allocation and
remaining runtime metadata still precede production activation; managed native
code size remains zero.

## 2026-09-25: Register byte encoding and unwind publication

Windows-x64 register and immediate instructions now have byte-output support,
including legacy, REX/REX2, VEX and EVEX/APX encodings. Writes use the runtime's
writable code alias, relocations preserve their executable locations, and GC
register changes retain native ordering and code offsets.

Unwind records can now be reserved and published for root methods and funclets,
including hot/cold splits. Memory-operand and branch encoding, the final emission
driver, executable allocation and remaining runtime metadata still precede
production activation. These byte-level capabilities do not yet make the port
an executing JIT.

## 2026-09-25: Complete prolog and epilog materialization

Windows-x64 root and funclet prologs and epilogs now replace their reserved
instruction groups. Frame setup, callee-save restoration, argument homes,
GC boundaries and parameter scopes retain native ordering, including localloc,
EnC, varargs and tailcalls.

OSR support reconstructs inherited frames and reloads live locals; profiling
entry preserves incoming arguments around the helper callback. Final group
offsets are recomputed after materialization. Machine-code encoding, executable
allocation and remaining runtime metadata publication are still required;
production emission remains explicitly unavailable.

## 2026-09-25: Prolog frame setup, unwind recording and argument homes

Windows-x64 prolog support now allocates and probes stack frames, establishes
the frame pointer, and saves integer and floating callee-saved registers.
APX paired pushes retain native alignment and register order. Unwind recording
preserves the native header and reverse-written operation encodings.

Incoming parameters can now move to their stack and register homes, including
promoted locals, write-through homes and register cycles. Varargs preserve the
four incoming integer registers in their ABI shadow slots. Full prolog/epilog
materialization, machine-code encoding and runtime metadata publication remain
necessary before production emission can be enabled.

## 2026-09-25: Prolog initialization and final jump placement

Prolog support now initializes stack locals, GC spill temps and floating
registers, sets security cookies and preserves generic-context reporting slots.
Block clearing retains native scalar tails, SIMD alignment and overlap choices,
and the three-store loop. OSR reuses the original frame's cookie and context.

Jump-distance binding now shortens eligible branches to a fixed point while
preserving forced widths and hot/cold boundaries. Final loop alignment adjusts
padding and group offsets after binding. Full prolog/epilog generation, encoding
and runtime metadata are still required before executable code can be produced.

## 2026-09-25: Final frame layout and redundant-jump removal

Windows-x64 frame finalization now restores entry locations, accounts for local
initialization, selects homing scratch registers and separates integer pushes
from floating-register saves. Final layout preserves argument homes, spill
temps, security-cookie ordering, async contexts and OSR frame reuse.

Redundant jumps to the next instruction group can now be removed while preserving
descriptor identity, cumulative offsets and the return-address NOP before an
epilog. Prolog/epilog materialization, distance binding, alignment adjustment,
encoding and runtime metadata remain ahead of production emission.

## 2026-09-25: Native debug-map publication

IP mapping publication now preserves native duplicate-offset selection, label
priority, prolog/epilog boundaries, call sites and async flags. Rich debug
publication encodes successful inline contexts and recorded mappings in native
order, including the optional diagnostic file format.

The publication routines transfer their buffers to the EE under the native
ownership contract. They require final emitter offsets and remain disconnected
from production until encoding and the remaining runtime metadata are complete.

## 2026-09-25: Windows-x64 block generation

The block driver now traverses the root function and EH funclets, restores
register locations and GC roots, generates LIR nodes, and records IL and rich
debug locations. Block exits preserve EH return-address boundaries, hot/cold
jumps, GS-cookie checks, frame poisoning and epilog reservations.

Loop alignment now records native padding descriptors and backedge relationships.
Integration also corrected a missing alignment opcode initialization. Final frame
layout, prolog/epilog materialization, jump binding, encoding and runtime metadata
publication still precede production emission; managed native-code size remains
zero.

## 2026-09-25: Windows-x64 node dispatch

The complete Windows-x64 node dispatcher now connects lowered LIR nodes to the
scalar, call, atomic, block, async and hardware generators. Register reuse is
handled before containment, comparisons consume operands before generating
conditions, and copy/reload markers leave their work to the consuming parent.

GC transitions and pending call labels preserve their native boundaries. The
block driver, final encoding and runtime publication remain ahead of production
emission; this does not yet produce executable managed native code.

## 2026-09-25: Hardware-intrinsic generation

Hardware-intrinsic generation now covers table-driven instructions and the
base, X86Base, AVX and FMA families. Embedded masking and rounding preserve
operand lifetimes, while variable immediates use the native masked or clamped
jump tables. FMA and ternary logic retain their destination-alias decisions.

The supporting emitters record blends, gathers, masked stores and multioperand
SIMD instructions with native register ordering and addressing. These are
instruction-generation capabilities; complete node dispatch and final
encoding/publication still precede managed native-code execution.

## 2026-09-25: Async resume-table recording

Async resume tables now retain native entry sizes, alignment and state offsets,
obtain the resume stub from the EE, and capture instruction-location cookies for
diagnostic addresses. Removed states remain invalid, while recorded locations
track later instruction-size changes instead of freezing estimated offsets.

Address generation reuses constant-data references. Final absolute pointers,
relocations and runtime resume execution still depend on production emission.

## 2026-09-25: Async and patchpoint transfers

Suspension returns now transfer the continuation and clear GC-bearing result
registers. Continuation reads, nonlocal jumps and function-entry addresses
preserve native register and label behavior. Unary instruction operands cover
registers, locals, indirect memory, constants and static data.

Regular and forced patchpoint nodes now call their matching helpers and use the
non-epilog indirect jump required by the Windows unwinder. Bound label references
retain typed instruction-group targets. This is generation support, not
patchpoint metadata, OSR execution or production emission.

## 2026-09-25: Exception-transfer generation

Finally calls now preserve native continuation selection and GC-reporting
boundaries. Retless calls add an unreachable breakpoint only where needed to
keep the return address in the correct EH region. Catch returns record the
relocatable target address in the ABI return register.

These block-generation paths reuse the existing label emitter. Final encoding,
unwind publication and production execution remain pending.

## 2026-09-25: Scalar value and conditional-compare generation

Saturating increments and bit modifications now preserve native carry and
register-aliasing behavior. Physical-register reads and catch arguments maintain
GC roots, while reused zero constants preserve instruction-group GC boundaries.
APX conditional comparisons carry the native default flags and use the shorter
conditional-test encoding for zero operands.

The node dispatcher still has async, hardware-intrinsic and patchpoint
dependencies; production emission remains unavailable.

## 2026-09-25: Block-memory generation

Windows-x64 block-memory generation now handles unrolled copies, overlapping
moves and unrolled or loop initialization. Scalar and SIMD tails follow native
width and overlap decisions. Memmove captures every source byte before writing
the destination; heap reference slots retain pointer-sized atomic stores.

Loop initialization preserves the initial nullcheck, and GC-unsafe copies use
the native noninterruptible region. These complete block-generation dependencies
without activating production emission or claiming machine-code parity.

## 2026-09-25: Atomic instruction generation

Windows-x64 atomic generation now records locked additions, exchanges,
compare-exchanges and bitwise retry loops with native instruction selection,
operand ordering, small-result extension and GC lifetimes. Full memory barriers
and the required unary/immediate memory instructions are implemented.

Indirect-address liveness now handles atomic nodes through their common operand
layout rather than an invalid managed-class cast. Production generation remains
unavailable; descriptor recording is not machine-code execution.

## 2026-09-25: Local heap generation and stack probing

Windows-x64 local heap generation now handles constant and dynamic sizes,
initialized allocations, outgoing argument space and aligned result addresses.
Page probes retain native ordering, including overflow clamping and final
touches at exact-page boundaries. Dynamic initialization uses the native
zero-push loop; constant initialization remains a separate lowered block store.

Immediate-only instruction recording and internal-register counting complete
the supporting closure. These paths remain below the production emission
boundary; machine-code encoding and execution are still pending.

## 2026-09-25: JMP argument placement and GC boundaries

JMP argument preparation now spills allocated register homes before restoring
ABI registers, avoiding register cycles and preserving the homes expected by
other blocks. Profiler callbacks observe the intermediate stack roots. Windows
varargs restore both integer and floating register views from caller shadow
space, with unknown arguments isolated in native no-GC instruction groups.

Nested GC-disable requests, deferred group boundaries and debugger sequence-point
padding are implemented. These are generation dependencies, not final transfer
encoding; production emission remains unavailable.

## 2026-09-25: Full flowgraph update

Flowgraph update now runs the native fixed-point cleanup driver, combining
branch reversal and threading, switch simplification, block compaction and
unreachable-block removal. Optional tail duplication is bounded across
conditional cycles. EH endpoints, edge likelihoods and OSR profile accounting
follow native ordering, and the optimized phase entrypoints are active.

The first native-host capture exposed and corrected a graph-dump buffer bug
after compaction. All nineteen optimized corpus methods complete the early
update phase; remaining differences come from existing string diagnostics and
hardware import. A preexisting implicit-byref global-morph assertion still
prevents the optimized corpus from completing. Minopts bypasses this phase,
and production emission remains explicitly unavailable.

## 2026-09-25: Remaining flowgraph cleanup helpers

Comparison returns can now normalize into conditional returns for shared cleanup.
Small conditional tails can be duplicated when their local-value information
makes that profitable; forward substitution then folds constant comparisons,
including small-local truncation.

The helpers retain EH extents, profile likelihood provenance, debug locations and
statement ownership. Compaction, empty/switch cleanup and these optional helpers
are ready for integration into the full flowgraph-update driver, which remains
unported.

## 2026-09-25: General call generation

General calls now connect argument placement, null checks, P/Invoke GC boundaries,
AVX transition handling and control transfer. Return values move from ABI registers
to their allocated homes; pending return labels exclude intervening helper calls.
Fast tailcalls retain target-address roots through the epilog and defer the jump.

Allocation and code generation share the native `vzeroupper` classification.
These paths remain below the production emission boundary; remaining node/block
generation, encoding and metadata dependencies still prevent managed native code.

## 2026-09-25: Switch and conditional-return optimization

Switch cleanup now bypasses eligible empty branches, removes single-target
dispatches and converts simple case sets into equality or unsigned range tests.
Boolean-return successors can fold into a branchless return while preserving
EH boundaries, profile accounting, side effects and epilogue-sharing limits.

Replacement nodes retain logical identity and update their statement or linear-IR
owners. Cycle detection now uses native block emptiness rather than range
emptiness. Full flowgraph update still awaits its remaining optimization helpers.

## 2026-09-25: Variable scope reporting

Variable locations now coalesce across prolog and body ranges, retain argument
entry visibility and append call-return locations using finalized emitter
offsets. Hidden variables, invalid locations and Debug diagnostics follow the
native reporting rules.

The EE receives native-layout records with explicit allocation and ownership
transfer, including release of buffers made empty by filtering. Production
emission still awaits its remaining generation, encoding and metadata dependencies.

## 2026-09-25: Empty-block optimization

Empty-block optimization now preserves init and OSR entries, profile bookkeeping
and catch-return EH boundaries. Removing a block redirects its predecessors
through the existing graph helpers; an EH-sensitive catch-return target gains a
real no-op, lowered immediately when already in linear IR.

Statement phis/NOPs and linear-IR offset markers use native emptiness rules.
Full flowgraph update still awaits its remaining optimization dependencies.

## 2026-09-25: Stack argument generation

Stack arguments now support scalar and SIMD stores, field lists and all native
struct-copy modes: unrolling, repeated byte moves and partial repeated moves
with individually recorded GC slots. Fast tailcalls select incoming argument
homes; ordinary calls use the outgoing area.

Copies preserve native load sizes, remainder widths and the threshold for
repeated non-GC runs. General call orchestration, remaining node/block generation,
encoding and metadata publication still prevent production machine-code emission.

## 2026-09-25: Flowgraph block compaction

Block compaction now merges statement and linear-IR blocks while retaining
predecessor and outgoing-edge identity, EH endpoints, profile consistency,
liveness, flags and IL ranges. Eligibility preserves entry blocks, protected
regions and call-finally adjacency; block emptiness follows the native rules
for each IR form.

The remaining switch and empty-block cleanup paths still precede full flowgraph
update activation. A suspected upstream phi-splice defect is preserved rather
than silently corrected only in the port.

## 2026-09-25: Call-target instruction generation

Call instructions now preserve direct, register, memory and indirection-cell
targets, GC return classification and async continuation metadata. Fast tailcalls
retain their already-consumed targets and epilog register checks; NativeAOT TLS
calls retain the linker's prefix sequence and sentinel.

This completes control-transfer recording, not general call orchestration.
Stack-argument generation, remaining node/block generation, encoding and metadata
publication still prevent production machine-code emission.

## 2026-09-25: Register arguments and return traps

Register arguments now retain native consumption order, ABI moves and fast-tailcall
GC lifetimes. Windows varargs duplicate floating-point arguments into the matching
integer registers after placement, including arguments evaluated early.

Return traps compare the thread's flag and conditionally invoke the GC helper
using the assigned integer temporary, preserving roots at the continuation.
Stack arguments, general calls, remaining node/block generation, encoding and
metadata publication still prevent production machine-code emission.

## 2026-09-25: Return values and profiler leave callbacks

Return generation now moves scalar and field-list values into their ABI registers
and restores GC return roots before profiler callbacks. Void and filter returns
retain their distinct behavior; async returns clear and report the continuation
after the callback. Debug stack-pointer checks remain limited to the root function.

Profiler leave and tailcall callbacks preserve direct/indirect handles and
tentative/final frame addressing. Caller-relative offsets now retain the native
frame-delta signs, EnC/localloc rules and OSR root-frame adjustment. General calls,
remaining node/block generation, encoding and metadata publication still prevent
production machine-code emission.

## 2026-09-25: Inline exception helpers

Overflow, bounds and finite checks now support inline throw helpers as well as
shared exception blocks. Conditional checks branch around the helper and restore
the normal path's GC state at the continuation; unconditional throws avoid an
unnecessary branch and label. Both forms retain native exception-helper selection.

This removes the shared-block-only restriction from arithmetic, multiplication,
indexed addresses and checked casts. General call-node generation, remaining
node/block generation, final encoding and metadata publication still prevent
production machine-code emission.

## 2026-09-25: Indirect stores and GC write barriers

Indirect stores now cover scalar values, read-modify-write operations, contained
byte swaps and hardware-intrinsic extraction/narrowing. GC stores preserve fixed
write-barrier argument registers and native checked/unchecked helper selection;
null and known non-heap stores avoid the barrier.

SIMD12 stores write eight bytes followed by four without overwriting padding.
Local-address stores update the store's lifetime after recording, and extraction
immediates retain the emitter's signed-byte representation. General call-node
generation, remaining node/block generation, final encoding and metadata
publication still prevent production machine-code emission.

## 2026-09-25: Helper-call and call-instruction recording

Helper calls now retain native direct, memory-indirect and register-materialized
target selection. Call recording preserves GC snapshots, helper-specific register
kills, tail-jump prefixes, relocation choices and small/large descriptor layouts.
Managed return-value diagnostics retain root IL locations and register or
return-buffer homes.

This supplies the helper-call dependency for GC write barriers. Indirect stores,
general call-node generation, remaining node/block generation, final encoding
and metadata publication still prevent production machine-code emission.

## 2026-09-25: Register swap generation

Register swaps now exchange local homes and update GC-reference/byref ownership
without consuming the still-live operands. Native XCHG attributes distinguish
GC-to-non-GC swaps while preserving unrelated roots.

Indirect stores and write barriers, calls, remaining node/block generation,
encoding and metadata publication remain before production emission.

## 2026-09-25: Table-based switch generation

Switch tables now preserve case order, duplicate targets and native alignment.
Dispatch loads a 32-bit entry and adds the first basic block's address, rather
than treating the entry as an offset from the table itself.

Basic-block address instructions retain relocation flags, long label references
and catch-return diagnostics. This records tables and instructions; final label
binding, encoding and publication are still required before managed execution.

## 2026-09-25: Finite checks and intrinsic generation

Finite checks now test the floating exponent and preserve the original value,
including signed zero, while targeting the native arithmetic-exception block.
Scalar rounding, ceiling, floor, truncation, square root and absolute value
generation retain native instruction choices, rounding modes and operand forms.

Intrinsic dispatch also handles upper-lane preservation: SIMD32 saves retain
their register or upper-stack-half representation, while SIMD64 uses its full
stack home. Table switches, calls, write-barrier stores and remaining node/block,
encoding and metadata work still prevent production machine-code emission.

## 2026-09-25: Scalar cast generation

Integer casts now preserve checked signed/unsigned ranges, truncation and
extension widths, including contained loads and already-extended spill values.
Floating casts retain legacy/VEX/EVEX instruction selection and the native
unsigned-long rounding sequence rather than approximating it with an offset.

Cast dispatch uses the managed unary node representation. Floating-to-integer
casts still require the hardware-intrinsic path selected by xarch lowering.
Finite checks, calls, write-barrier stores and the remaining node/block, encoding
and metadata closure keep production emission explicitly skipped.

## 2026-09-25: Conditional selection and branches

Conditional moves now preserve destination/source conflicts, including registers
inside contained memory addresses, and the two-part conditions needed for
floating comparisons. Boolean and flag branches retain native short-circuit
sequences and cannot fall through across the hot/cold code boundary.

SETCC nodes publish their register results through the existing lifetime path.
Scalar casts, calls, write-barrier stores and the remaining node/block, encoding
and metadata closure still keep production emission explicitly skipped.

## 2026-09-25: Scalar comparisons and Boolean results

Integer and floating comparisons now retain native signed/unsigned conditions,
test-mask narrowing, bit-index widths and sign-comparison shortcuts. Floating
conditions preserve operand swaps, unordered results and the same-register NaN
check.

Boolean materialization uses native short-circuit labels, byte widening and
optional APX zero-upper instructions. Flag reuse retains instruction-history
bounds, width and flag-effect checks, including updates to the owning condition
consumer. Conditional selection and branches, calls, write-barrier stores and
the remaining block/encoding/metadata closure still keep production emission
explicitly skipped.

## 2026-09-25: Explicit addresses and runtime checks

Explicit address generation now handles base, scaled-index and combined forms
with native operand consumption and GC result tracking. Null checks record the
faulting memory comparison rather than materializing a result.

Bounds checks preserve the zero-index shortcut, immediate-index comparison
reversal, comparison widths and shared exception targets. Address-destination
recording retains read/write formats and instruction options. Comparisons,
calls, write-barrier stores and the remaining block/encoding/metadata closure
still keep production emission explicitly skipped.

## 2026-09-25: Indirect reads and indexed addresses

Indirect reads now preserve native narrow-load extension, TLS segment access
and GC result kinds. SIMD12 reads compose eight- and four-byte loads without
overreading, preserving the native address adjustment and unused-lane clearing.

Indexed addresses retain index widening, scale selection and bounds branches,
with the array base kept as a GC root through address generation. Internal
temporaries retain native Debug-only consumption. Remaining node/block
generation, final encoding and runtime metadata publication still keep
production emission explicitly skipped.

## 2026-09-25: Local stores, bitcasts and multi-register results

Local variable and field stores now preserve native stack/register homes,
contained bitcasts, constant reuse and lifetime updates. Zero rematerialization
retains the distinction between positive and negative floating zero. SIMD12
stores write exactly twelve bytes, including the zero-vector shortcut.

Multi-register intrinsic results are consumed and assigned one field at a time,
preserving copy/reload ordering, narrow field stores, write-through homes and
GC liveness. The native-unsupported Windows x64 multi-register SIMD return case
rejects before consumption. Remaining node/block generators, final encoding and
runtime metadata publication still keep production emission explicitly skipped.

## 2026-09-25: Local reads and addresses

Local address and load generation now preserve stack offsets, GC result kinds,
narrow signed/unsigned extension and aligned SIMD loads. Register candidates,
multi-register locals and deferred spill reloads retain their native
load-at-use behavior.

SIMD12 field loads read the lower eight bytes and insert the final float without
overreading adjacent data, clearing the unused fourth lane. Local stores and
other node/block generators, final encoding and runtime metadata publication
remain; production emission is still explicitly skipped.

## 2026-09-25: Integer division and remainder

Division and remainder generation now preserve the native signed and unsigned
instructions, 32/64-bit dividend preparation, implicit quotient/remainder
registers and GC-state updates. Register, local and spilled divisors use the
existing operand recorder, with result spilling after the divide.

Local access and other node generators, block generation, final encoding and
runtime metadata publication remain incomplete; production emission is still
explicitly skipped.

## 2026-09-25: Integer multiplication and high-half results

Integer multiplication generation now preserves native immediate forms, LEA
shortcuts, memory operands and APX destinations. Checked signed and unsigned
products retain implicit-register constraints and branch before result spilling.
High-half multiplication uses the native RDX:RAX pair or BMI2 MULX, preserving
source reuse and destination aliasing.

Remaining node and block generators, final encoding and runtime metadata
publication are still required. Production emission remains explicitly skipped.

## 2026-09-25: Binary arithmetic and overflow branches

Binary arithmetic generation now preserves native operand selection, scalar
floating-point register order, LEA and increment/decrement shortcuts, APX
destinations and GC-root transitions. Checked arithmetic branches to the
prepared shared throw-helper block before producing or spilling its result.
Inline throw-helper calls remain explicitly unsupported.

Jump recording retains target identity, hot/cold-region restrictions, backward
short-jump estimates, relocation and removable-jump metadata. Jump opcodes are
generated from the existing native table input. Final instruction encoding,
remaining node and block generators, and runtime metadata publication are still
required; production emission remains explicitly skipped.

## 2026-09-25: Scalar shifts, rotates and memory operands

Shift and rotate generation now preserves native ADD/LEA shortcuts, flag-setting
requirements, BMI2 forms, RCX count moves and memory read-modify-write operations.
Shared operand classification handles stack locals, spill temporaries, indirect
addresses, scalar/vector constants and contained broadcasts without narrowing
the native dispatchers to register-only operands.

The supporting emitter records complete memory/immediate and SIMD forms,
including legacy copies and EVEX/APX options. Node and block generation, final
encoding and runtime metadata publication remain incomplete; production
emission is still explicitly skipped.

## 2026-09-25: Binary memory operands and byte swaps

Binary instruction recording now handles registers, immediates, local and
indirect memory, and spill temporaries, preserving native operand direction,
implicit register pairs and APX non-destructive destinations. Spill descriptors
are removed by node identity and their temporary storage is returned in native
order.

Byte-swap generation supports register and contained-memory operands, including
MOVBE/APX selection and the native adjacent-cast rule for omitting 16-bit
normalization. Remaining node and block generators, final encoding and runtime
metadata publication are still required; production emission remains explicitly
skipped.

## 2026-09-25: Unary operations and floating sign masks

Unary node generation now consumes operands, records integer negation or
complement, and produces or spills the result in native order. Floating
negation and the shared absolute-value helper use exact packed sign masks,
preserving the native bitwise treatment of signed zero and NaN payloads.

Single-register recording retains APX unary forms, legacy destination copies
and native prefix/register encodings. General memory-operand dispatch,
remaining node generators, final encoding and runtime metadata publication
remain; production emission is still explicitly skipped.

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
