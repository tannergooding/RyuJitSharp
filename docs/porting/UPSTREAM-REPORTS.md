# Upstream reports

This register records confirmed defects in the pinned upstream source that
should be reported upstream. It is not an issue tracker; entries here do not
imply that a report has been filed. Keep the managed port aligned with pinned
behavior until an upstream correction is incorporated, unless an intentional
deviation is explicitly approved.

| Reference | Native symbol | Finding | Suggested correction | Port handling |
| --- | --- | --- | --- | --- |
| B017 | `ExtendedDefaultPolicy::DetermineMultiplier` | The non-exact argument-unbox branch emits a diagnostic naming `m_ArgUnboxExact`, although the branch is controlled by `m_ArgUnbox`. This is a diagnostic typo; the managed port currently preserves the pinned text. | Name `m_ArgUnbox` in the non-exact argument-unbox diagnostic. | Keep the pinned diagnostic text in managed code until the upstream correction is incorporated. |
| B498 | `getBitMaskOnes` / `getBitMaskZeroes` (`src/coreclr/jit/emitarm64.h`) | The SVE MOV/DUPM disassembly-alias preference evaluates shifts whose counts can equal 64; `getBitMaskOnes` can also left-shift a negative signed value. These operations have undefined behavior in C++, so the selected alias at width boundaries is not specified, even though ARM64 toolchains commonly produce masked-shift behavior. The helpers affect only the disassembly alias; the encoded immediate is unchanged. | Define the width-boundary behavior with unsigned operations and explicit shift-count handling, preserving the intended ARM64 masked-shift result if that is the contract. | Keep the source-shaped managed expressions and the deterministic width-boundary tests. C# defines the shift behavior; compare the alias selection after upstream defines its contract. |
| B117 | `Compiler::fgExpandQmarkStmt` (`src/coreclr/jit/morph.cpp`) | In the true-only qmark expansion, the condition is reversed and `thenEdge` targets the remainder, while the sole true-arm block inherits `thenLikelihood`. The code nevertheless assigns `thenLikelihood` to the remainder edge and `elseLikelihood` to the arm edge, inverting the edge probabilities for an 80/20 qmark. The pinned and current upstream implementations share this assignment. | Assign `elseLikelihood` to the edge targeting the remainder and `thenLikelihood` to the edge targeting the true arm. | Preserve the pinned edge likelihoods and CFG shape in the managed port until an upstream correction is incorporated. |
| B121 | `IndirectCallTransformer::Transformer::ChainFlow` (`src/coreclr/jit/indirectcalltransformer.cpp`) | The fat-pointer transform assigns 80/20 weights to the thin/fat blocks, but its shared check-block flow assigns 0.5 to both outgoing edges. The native source labels this `Todo: get likelihoods right`; the managed test asserts the same edge probabilities. | Assign each check edge the likelihood corresponding to its target block's weight rather than hard-coding 0.5. | Preserve the pinned check-edge likelihoods in the managed port until an upstream correction is incorporated. |
| B124 | `IndirectCallTransformer::GuardedDevirtualizationTransformer::Run` (`src/coreclr/jit/indirectcalltransformer.cpp`) | The single-check, non-exact branch increments `GDV` and then tests whether `GetChecksCount() > 1` before incrementing `MultiGuessGDV`. That nested condition is unreachable, so the multi-guess metric is never incremented. Exact candidates continue through a separate transform path without this metric increment; that exclusion is retained because no contrary counter contract is established. | Count multi-guess transformations independently of the single-check chaining predicate, and clarify the intended exact-candidate accounting. | Preserve the pinned metric guards and emitted values in managed code until upstream defines the counter contract. |
| B503 | `Compiler::dFindTreeInTree` (`src/coreclr/jit/compiler.cpp`) | The function visits each operand but recurses using `child`, which is initialized to null and never assigned the operand. As a result, lookup finds the current root but not descendant trees. | Recurse on the visited operand before deciding whether to abort traversal. | Preserve the pinned debugger lookup behavior in managed code until an upstream correction is incorporated. |
| B504 | `DumpUnwindInfo` (`src/coreclr/jit/unwindloongarch64.cpp`) | The `0xD0` and `0xDC` three-byte opcode branches assert only that one byte remains, then read two bytes. If only one byte follows the opcode at the end of the declared unwind-code region, the second read crosses that boundary. | Require two remaining bytes before reading the operands of either three-byte opcode. | Preserve the pinned bounds checks in managed diagnostics until an upstream correction is incorporated. |
| B509 | `LinearScan::BuildCall` (`src/coreclr/jit/lsraloongarch64.cpp`) | In the relative-indirection fast-tail-call branch, `candidates` includes the caller-clobbered `T0`/`T1` GS-cookie temporaries. When a cookie is needed, the code removes those registers from the unrelated, empty `ctrlExprCandidates` mask instead of `candidates`, so they remain eligible for the call-target temporary. | Remove the GS-cookie temporary mask from `candidates` before allocating the call-target temporary. | Preserve the pinned behavior in managed LSRA until an upstream correction is incorporated. |
| B507 | `CodeGen::genUnknownSizeFrame` (`src/coreclr/jit/codegenarmarch.cpp`) | For frames larger than 32 vectors, native emits `MSUB` with SP as both destination and addend. `MSUB` uses general-register operands, so register 31 denotes XZR rather than SP; Release code therefore does not apply the computed decrement to SP, while Debug asserts reject the operands. | Compute the vector-size product in a general register, then update SP with an instruction form that permits SP. | Preserve the pinned instruction sequence in managed code until an upstream correction is incorporated. |
| B505 | `InlineStrategy::DumpData` (`src/coreclr/jit/inline.cpp`) | `Compiler::RecordStateAtEndOfCompilation` converts the elapsed counter delta to microseconds, and `Compiler::getInlineCycleCount()` returns that value. The inline diagnostic then treats the microsecond value as raw timer ticks and scales it by `1,000,000 / CachedCyclesPerSecond()` again, producing an incorrectly small `JitTime` field. | Emit the already-converted microsecond value without applying a second timer-frequency conversion. | Preserve the pinned Debug diagnostic output in managed code until an upstream correction is incorporated. |
| B525 | `FloatingPointUtils::ilogb(double)` (`src/coreclr/jit/utils.cpp`) | Native value numbering maps `Math.ILogB` to `VNF_ILogB` and calls this helper for constant doubles. After handling zero and NaN, the helper calls unqualified `ilogb(value)`, which resolves back to itself; an MSVC overload-context reproduction confirms the recursive resolution. The float overload instead calls `ilogbf`. | Explicitly call the intended C math-library `ilogb` overload so it cannot resolve to the class member. | Keep managed `Math.ILogB` constant evaluation; the approved difference is recorded in D013. This report has not been filed. |
| B144 | `LocalAddressVisitor::MorphStructField` (`src/coreclr/jit/lclmorph.cpp`) | The volatile-indirection guard normally prevents promotion to a local, but permits it when the address is a dereferenced `FIELD_ADDR`. The native comment says this transformation is illegal; the managed port preserves the exception. | Remove the exception unless the volatile ordering semantics are proven safe for this transformed access. | Preserve the pinned predicate; no managed/native difference is introduced. |
| B158 | `SideEffectSet` constructors | Both constructors leave `m_preciseExceptions` indeterminate, but `AddNode` reads it through `|=`; a default local set can reach `AddNode` before `Clear`. | Initialize `m_preciseExceptions` to `ExceptionSetFlags::None` in both constructors. | Keep managed zero-initialization; do not emulate an indeterminate native read. |
| B185 | ARM unwind decoder | The extended `CodeWords` field permits streams beyond 64 words, but native indexes its fixed 256-entry epilog-start array across the stream, exceeding it at the 65-word boundary. | Size or bounds-check epilog-start tracking according to the decoded stream length. | Keep the managed stream-sized span and its 65-word boundary test; do not reproduce the native out-of-bounds access. |
| B322 | `Compiler::fgPgoDeferredInconsistency` | The field is not initialized in native `Compiler`; Checked allocations poison it, and the checker can read it when profile synthesis did not assign it. This produces a skipped-check diagnostic where managed zero-initialization reports no profiled blocks. | Initialize the native field to `false` before any possible read. | Keep the deterministic managed value; do not emulate a poisoned native `bool`. The diagnostic difference is recorded in D015. |
| B218 | xarch `emitIns_Call` absolute-indirect range assertion | For `EC_INDIR_ARD`, the address is stored in `params.disp` and `params.addr` must be null, but the no-base/no-index assertion range-checks `params.addr`, making the check vacuous. | Range-check `params.disp` against the signed 32-bit displacement bounds. | Preserve the pinned assertion until the native correction is incorporated. |
| B246 | `XorAction::LeftGap` | The native action links the inserted RHS node to `(*l)->next` before replacing `*l`, dropping the existing LHS node while incrementing `numNodes`. Managed code keeps the LHS node, producing the complete XOR result. | Link the inserted node to the existing LHS node before replacing the list head. | Keep the managed result; `HashBvSetAlgebraTests.XorPreservesTheLeftNodeWhenTheRightNodeHasAnEarlierBase` covers the discrepancy. |
| B247 | `IntersectsAction::BothPresent` | When equal-base nodes have disjoint bits, native advances neither cursor, so `MultiTraverseEqual` repeats indefinitely. Managed code advances both cursors and terminates. | Advance both cursors when the nodes do not intersect. | Keep the terminating managed behavior; `HashBvSetAlgebraTests.IntersectsTerminatesWhenEqualBaseNodesHaveDisjointBits` covers the discrepancy. |
| B266 | `Compiler` loop-inversion diagnostic (`src/coreclr/jit/optimizer.cpp`) | The zero-weight condition tests `weightStayInLoopSucc` but prints `preheader->bbNum`, identifying the wrong block. | Print `stayInLoopSucc->bbNum`. | Preserve the pinned diagnostic until the native correction is incorporated. |
| B327 | Checked JIT mask-local debug-range diagnostic (`src/coreclr/jit/scopeinfo.cpp`) | A checked optimized compilation asserts while printing the positive mask local's debug live range after mask conversion and codegen IR; the managed checked path retries at MinOpts. FullOpts execution succeeds on both sides with the same 52-byte body. | Avoid querying an unavailable debug range for the synthesized mask local, or establish the invariant that makes the range valid. | Retain the managed retry; do not claim checked-phase diagnostic parity. The observed difference is recorded in D017. |
| B379 | ARM64 loop-forwarding scan over predecessor blocks (`src/coreclr/jit/lower.cpp`) | An empty predecessor skips `CheckNodes`, leaving `hasDef` from the prior predecessor live; the stale value can skip that empty block's predecessors and miss an indirect store. | Reset the scan outputs for every popped predecessor before inspecting its nodes. | Preserve the pinned scan until the native correction is incorporated; no generated-code parity claim is made. |
| B381 | ARM64 conditional-select lowering and `CodeGen::genCodeForSelect` | Lowering can create `GT_SELECT_INC` for an explicit condition, but codegen handles explicit conditions only for `GT_SELECT`/`INV`/`NEG` and then requires a flags-based condition. The two-constant conversion can also select the wrong CSINC arm. | Keep explicit-condition CSEL nodes unless lowering and codegen agree on a valid CSINC representation, and correct constant-arm mapping. | Preserve the pinned lowering shape until the native correction is incorporated; no ARM64 runtime parity claim is made. |
| B398 | ARM64 consecutive-register reuse (`src/coreclr/jit/lsraarm64.cpp`) | The V0 reuse calculation shifts by `firstRegNum - 1`; at V0 this is a negative shift count and therefore undefined in C++. C# defines a deterministic masked shift, so source-shaped expressions do not establish output parity. | Handle V0-to-V31 wrap explicitly and define all shift-count boundaries. | Keep the managed behavior deterministic and retain the other pinned masks; no ARM64 execution/parity claim is made. |
| B514 | `Compiler::compSizeEstimate` / `compCycleEstimate` (`src/coreclr/jit/compiler.h`) | Native leaves both estimates uninitialized for verbose MinOpts compilations but prints them in `codegenlinear.cpp`; Checked Windows-x64 captures show arena poison values. Managed fields remain zero. | Initialize both estimates before any verbose diagnostic can print them. | Keep deterministic managed values; do not emulate poisoned native memory. |
| B365 | `Compiler::gtCallGetDefinedRetBufLclAddr` | Forced register spilling with GC stress triggers a Checked assertion when an outer reload hides a `PUTARG_REG` wrapper around the retbuf local address. Ordinary configuration succeeds; no ordinary miscompilation or memory-safety impact is established. | Inspect reload/copy wrappers before checking the putarg and retbuf shape. | Preserve pinned managed behavior; report contains the native repro and supporting dump. |
| B394 | `ValueNumStore::RunTests` | Enabling native component tests asserts because a self-test expects the pre-canonicalized operand order for a commutative VN expression. Production canonicalization puts the non-handle constant in argument one. | Update the self-test to assert the canonical operand order. | Preserve pinned canonicalization and assertion; report contains the native repro. |
| B524 | `jitstd::list::merge` | When source nodes remain after merging, native links the source tail into the destination but does not clear the source's head, tail, or size. Both lists then share nodes, making later traversal/destruction unsafe. The pinned JIT has no `list.merge` call sites. | Clear the source list after transferring the remaining chain, while preserving merge ordering. | Preserve pinned managed behavior until the upstream correction is incorporated; the defect was reported in [dotnet/runtime#135435](https://github.com/dotnet/runtime/issues/135435). |

## Detailed reports

### B365: Forced register spilling exposes a retbuf wrapper-order assertion

**Confirmed in native RyuJIT**, Windows x64 Checked, at dotnet/runtime
`33baf8ee337b20dd0f184b69a6f09be92850bf9e`. With `JitStressRegs=0xC03`
and `GCStress=4`, compilation of
`Runtime_133521.ScalarFmaNegation(int, int)` asserts during linear scan register
allocation. The same input with GC stress but without register stress succeeds.
No ordinary-configuration miscompilation or memory-safety impact is established.

#### Failure and relevant tree shape

The assertion is in
[`Compiler::gtCallGetDefinedRetBufLclAddr`](https://github.com/dotnet/runtime/blob/33baf8ee337b20dd0f184b69a6f09be92850bf9e/src/coreclr/jit/gentree.cpp#L22020-L22048):

```cpp
node->OperIs(GT_LCL_ADDR) &&
lvaGetDesc(node->AsLclVarCommon())->IsDefinedViaAddress()
```

The retained post-allocation dump contains this retbuf argument for the call to
`Runtime_133521.NegatedScalarFma(Vector128<float>, ...)`:

```text
t672 RELOAD
  t579 PUTARG_REG rcx
    t671 RELOAD
      t28 LCL_ADDR V05
```

The helper checks for `PUTARG_REG` or `PUTARG_STK` **before** calling
`gtSkipReloadOrCopy`. An outer `RELOAD` therefore bypasses the putarg check;
the subsequent skip exposes `PUTARG_REG`, not the required `LCL_ADDR`.
[`gtSkipReloadOrCopy`](https://github.com/dotnet/runtime/blob/33baf8ee337b20dd0f184b69a6f09be92850bf9e/src/coreclr/jit/gentree.h#L10176-L10184)
intentionally removes one reload/copy layer, not a putarg wrapper.

This strongly points to handling an outer reload/copy before inspecting the
putarg, while preserving the existing inner reload/copy handling. No debugger
capture identifies the exact failing call object, and no proposed native fix
has been applied or tested; the dump and helper ordering are the supporting
evidence, not a validated fix.

#### Reproduction

The verified input is the retained `UpstreamRegressionProbe.dll`, built from
the pinned upstream regression sources, including
[`src/tests/JIT/Regression_ro_2/Runtime_133521.cs`](https://github.com/dotnet/runtime/blob/33baf8ee337b20dd0f184b69a6f09be92850bf9e/src/tests/JIT/Regression_ro_2/Runtime_133521.cs).
Its driver invokes `ScalarFmaNegation` with the upstream cases
`(0, -11)`, `(1, -13)`, `(2, 13)` and `(3, 11)`, along with other regression
entry points. It requires AVX and FMA; the verified host also supports AVX512F.
The reproducer has not been minimized.

In a fresh shell, with `$CoreRoot` pointing to the matching Checked build and
`$Corpus` to that DLL:

```powershell
$env:DOTNET_ReadyToRun = '0'
$env:DOTNET_TieredCompilation = '0'
$env:DOTNET_JitName = 'clrjit.dll'
$env:DOTNET_JitPath = Join-Path $CoreRoot 'clrjit.dll'
$env:DOTNET_GCStress = '4'
$env:DOTNET_JitStressRegs = 'c03'
$env:DOTNET_JitStressRegsRange = '4f3e2341 8075f3c3 36970323 5eb69743 f18764f8 076e31f8'
$env:DOTNET_JitDump = 'Runtime_133521:*'
$env:DOTNET_JitDumpASCII = '1'
$env:DOTNET_JitDisasmDiffable = '1'
$env:DOTNET_JitStdOutFile = Join-Path $PWD 'spill-jitdump.txt'
& (Join-Path $CoreRoot 'corerun.exe') $Corpus
```

The hashes above belong to the retained binary, not a portable method selector.
If rebuilding or changing the harness, first capture its method hashes without
register stress and update the range. Unrestricted `JitStressRegs=0xC03` also
asserted in `EventSource.Initialize` before the selected regression compiled;
the scoped range avoids that earlier failure.

Observed failure:

```text
Runtime_133521:ScalarFmaNegation(int,int)
phase: Linear scan register alloc
IL size: 341
method hash: 0x8075f3c3
optimization: FullOpts
source: gentree.cpp:22046
process exit: 0xC0000602
```

With `DOTNET_JitStressRegs` and `DOTNET_JitStressRegsRange` removed, the fresh
control exits zero, verifies the regression results, and reports 3,404 GC-stress
collections. That collection count is observational, not a stable expectation.
Both runs use native `clrjit.dll` and neither times out.

#### Retained evidence

Fresh captures, input hashes, stdout, stderr and dumps:

- `artifacts/upstream-reports/spill-native-1/`
- `artifacts/upstream-reports/spill-native-control-1/`

The tree above is recorded in `spill-native-1/jitdump.txt` around lines
112186-112202. Original source/dependency hashes and the original scoped failure
remain under `artifacts/upstream-regressions/spill-selected-native-1/`.
The exact driver and project are retained in
`artifacts/upstream-regressions/corpus-built-3/source/`.

Repeat with fresh output directories:

```powershell
python artifacts\upstream-reports\capture-native.py `
  artifacts\upstream-regressions\spill-selected-native-1\Runtime_133521\manifest.json `
  artifacts\upstream-reports\spill-native-2
python artifacts\upstream-reports\capture-native.py `
  artifacts\upstream-regressions\gcstress-native-1\Runtime_133521\manifest.json `
  artifacts\upstream-reports\spill-native-control-2
```

The local runner verifies the recorded inputs, clears inherited runtime settings,
and explicitly selects native `clrjit.dll`. The port retains pinned behavior;
this report does not propose changing only RyuJitSharp to hide the assertion.

### B394: VN component test asserts against the current commutative operand order

**Confirmed in native RyuJIT**, Windows x64 Checked, at dotnet/runtime
`33baf8ee337b20dd0f184b69a6f09be92850bf9e`. Enabling
`DOTNET_JitComponentUnitTests=1` terminates the native process at
`valuenum.cpp:11816`. The same application succeeds with the setting disabled.
This is a stale self-test expectation, not a demonstrated generated-code defect.

#### Cause

[`ValueNumStore::VNForFunc`](https://github.com/dotnet/runtime/blob/33baf8ee337b20dd0f184b69a6f09be92850bf9e/src/coreclr/jit/valuenum.cpp#L2865-L2881)
canonicalizes commutative operands by VN number, then moves a non-handle constant
to argument one. Consequently, `ADD(1, vnRandom1)` is stored as
`ADD(vnRandom1, 1)`.

[`ValueNumStore::RunTests`](https://github.com/dotnet/runtime/blob/33baf8ee337b20dd0f184b69a6f09be92850bf9e/src/coreclr/jit/valuenum.cpp#L11806-L11816)
constructs that expression, retrieves its function application, and asserts the
opposite order:

```cpp
fa2a.GetArg(0) == vnFor1 && fa2a.GetArg(1) == vnRandom1
```

The likely correction is to update the test expectation to the canonical order,
not change production canonicalization. No native patch has been applied or
validated as part of this report.

#### Reproduction

Use a Checked Core_Root built from the revision above, in a fresh shell without
other JIT stress settings. `$CoreRoot` denotes that build and `$Application`
denotes a managed application compatible with it:

```powershell
$env:DOTNET_ReadyToRun = '0'
$env:DOTNET_TieredCompilation = '0'
$env:DOTNET_JitName = 'clrjit.dll'
$env:DOTNET_JitPath = Join-Path $CoreRoot 'clrjit.dll'
$env:DOTNET_JitComponentUnitTests = '1'
& (Join-Path $CoreRoot 'corerun.exe') $Application
```

The verified application was the retained `ParameterCycles.dll` corpus. With
component tests enabled, stderr contains the assertion above and the process
exits `0xC0000602`, without timing out. With the setting changed to `0`, it exits
zero and reports `ParameterCycles: 2048 floating permutations verified`.
[`Compiler::compDoComponentUnitTestsOnce`](https://github.com/dotnet/runtime/blob/33baf8ee337b20dd0f184b69a6f09be92850bf9e/src/coreclr/jit/compiler.cpp#L1731-L1747)
runs this test once during JIT initialization when the option is enabled;
the corpus does not need to construct a particular expression.

Exact local captures and verified input hashes are retained under:

- `artifacts/upstream-reports/component-native-enabled-1/`
- `artifacts/upstream-reports/component-native-control-1/`

To repeat those captures from this repository, select fresh output directories:

```powershell
python artifacts\upstream-reports\capture-native.py `
  artifacts\primary-parameter-cycles\reference-after\manifest.json `
  artifacts\upstream-reports\component-native-enabled-2 --component-tests 1
python artifacts\upstream-reports\capture-native.py `
  artifacts\primary-parameter-cycles\reference-after\manifest.json `
  artifacts\upstream-reports\component-native-control-2 --component-tests 0
```

The runner verifies the reference input hashes and explicitly selects native
`clrjit.dll`; RyuJitSharp is not executing these tests. The C# port retains the
pinned assertion and canonicalization pending an upstream correction.

### B524: `jitstd::list::merge` leaves source tail nodes in both lists

**Status:** reported in
[dotnet/runtime#135435](https://github.com/dotnet/runtime/issues/135435).
The implementation was verified at pinned revision
`33baf8ee337b20dd0f184b69a6f09be92850bf9e` and current upstream commit
`7fd4be4b29175b1007cb5a1bd701f057b2fe08be`.

#### Defect

`list<T, Allocator>::merge` copies source elements into the destination while
the comparator selects them. If source nodes remain after that loop, it links
the remaining source chain onto the destination and assigns the source tail to
the destination tail, but does not update the source list's head, tail, or
size. Both lists therefore retain links to the same nodes.

The native `list` destructor calls `destroy_helper`, which walks backward from
each list's tail and destroys/deallocates the nodes. Clearing or destroying
either list can therefore leave the other list linked to already-destroyed
nodes; later traversal or destruction can access or destroy those nodes again.
Not using the source list after `merge` does not avoid its destructor.

For example, with the default comparator, merging destination values
`[1, 4, 6]` and source values `[2, 3, 5, 7]` produces destination values
`[1, 2, 3, 4, 5, 6, 7]` while the source still reports `[2, 3, 5, 7]`; the
destination and source share the final node. The pinned JIT tree contains no
`list.merge` call sites, so this is a source-confirmed generic-container defect,
not a demonstrated JIT execution failure.

#### Port handling

`JitStdList.merge` mirrors the pinned insertion and tail-link algorithm,
including the shared tail, and its regression test verifies value order,
source metadata, shared tail identity, and forward iterator termination. The
test does not claim safe destruction of both lists after aliasing. Keep the
managed behavior aligned until the upstream correction is accepted, then apply
that correction to both implementations.
