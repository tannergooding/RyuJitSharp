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

## 2026-10-03: Wasm struct-return field-list acceptance

Completed the `TARGET_WASM` field-list branch of `CodeGen::genStructReturn`,
including return-register liveness consumption and the single-register
assertions. Non-field-list shapes still terminate through the explicit Wasm
dependency boundary. The same Wasm build closure adds its missing managed
emitter/type/register-mask accessors and target-specific control-flow/prolog
bindings. Focused full-analysis browser-Wasm target tests pass 58/58 in Debug
and Release; the full Wasm target suites pass 142/143 Debug and 143/144 Release
with one known fixture failure in each configuration. The full Windows-x64
target controls pass 146/146 Debug and 147/147 Release.

The exact Wasm method body was removed from the consolidated native residual at
HEAD `dad0c5310e69f95bfab4d99e20b1f78dc148cc5e`, solely parented to pinned
oracle `33baf8ee337b20dd0f184b69a6f09be92850bf9e`. The shared
`genStructReturn` declaration remains for the native common implementation.
Recovery ref `refs/copilot-recovery/wasm-gen-struct-return-before-253f1e51`
preserves the prior native HEAD. The full Wasm target suite still has one
independent `CodeGenBlockDriverTargetTests` fixture failure; this acceptance
does not claim Wasm backend or generated-code parity.

## 2026-10-03: Funclet-count accessor retirement

Completed the shared `Compiler::compFuncCount` binding with its native
`fgFuncletsCreated` assertion and added a direct assertion to the root-only
funclet test. Full-analysis Windows-x64 Debug and Release compiler builds pass,
and the focused test passes 1/1 in both configurations.

The exact 5-line inline accessor was removed from the consolidated native
residual at HEAD `2f7bc1c5b189d663a7f09ea11c35de6dbd0478b3`, solely parented to
pinned oracle `33baf8ee337b20dd0f184b69a6f09be92850bf9e`. Recovery ref
`refs/copilot-recovery/comp-func-count-before-86b0cad` preserves the prior
HEAD. Untranslated Wasm register-allocation call sites remain; no Wasm
execution or global retirement-debt claim is made.

## 2026-10-03: ARM64 condition mapping

Added the complete mapping from ARM64 jump kinds to instruction condition
codes, preserving the native invalid-condition assertion path. This is a
scoped managed utility; the full ARM64 backend still has unrelated compilation
blockers, so this milestone does not claim ARM64 code-generation parity.

## 2026-10-03: Memory SSA accessor retirement

Retired the residual `Compiler::GetMemoryPerSsaData` accessor after confirming
the existing managed ref-return reaches the same 1-based `SsaDefArray` storage
slot for valid allocated memory-SSA numbers. Direct tests verify stable
same-slot references, distinct slots, and reset/reallocation behavior. The
focused Windows-x64 `ValueNum`/`MemorySsa`/`SsaMemory` selection passes 869
Debug and 811 Release tests with no skips, and Debug/Release compiler builds pass
with full analysis. The ordinary Core test project remains blocked by unrelated
test-source compile/analyzer failures; the focused run excluded those fixtures
without changing the project.

The getter and its comment were removed from the consolidated native residual
at HEAD `86b0cad45b83454d4c125ca2f594d03405498aa9`, solely parented to pinned
oracle `33baf8ee337b20dd0f184b69a6f09be92850bf9e`. Recovery ref
`refs/copilot-recovery/memory-ssa-before-3df89eb` preserves the prior HEAD.
This does not establish runtime, generated-code, phase-dump, or global
retirement-debt parity.

## 2026-10-03: Block reachability accessor retirement

Closed the existing `BlockReachabilitySets` mapping by removing its final
residual native class shell, including the oracle-identical `GetDfsTree`
accessor and constructor. Reused the committed full-analysis block-statistics
selection, which includes `BlockWeightTests` and passes 98 Debug / 46 Release;
its reachability test asserts that the accessor returns the identical DFS-tree
instance. No managed source changed in this retirement-only follow-up.

The remaining native shell was removed from the consolidated residual at HEAD
`3df89eb47bab90710556d80be3576ee74abadc5e`, solely parented to pinned oracle
`33baf8ee337b20dd0f184b69a6f09be92850bf9e`. Recovery refs
`refs/copilot-recovery/blockreach-getdfs-before-060e787` and
`refs/copilot-recovery/blockreach-shell-before-eec6fe7` preserve the prior
residual states. This does not establish global retirement debt clearance or
runtime/codegen parity.

## 2026-10-03: Shared xarch IMUL helper retirement

Completed the shared three-operand IMUL register-selection and reverse-decoding
helpers for x86 and AMD64, including native register/opcode layout assertions
and target-isolated regression tests. Focused Windows-x64 target tests pass 1/1
in Debug and Release with full analysis and no skips. The ordinary Core test
project remains blocked by unrelated compile/analyzer failures; the x86 Core
build remains blocked by existing missing backend symbols, so neither x86 build
nor execution parity is claimed.

The three exact pinned-oracle helper bodies and their declarations were removed
from the consolidated native residual (86 deletion lines) at HEAD
`060e7870836b6d0be58775ae062ce840bed77c88`, solely parented to pinned oracle
`33baf8ee337b20dd0f184b69a6f09be92850bf9e`. Recovery ref
`refs/copilot-recovery/imul-helper-before-060e787` preserves the previous HEAD.

## 2026-10-03: Managed edge-weight diagnostics

Added the DEBUG-only `Compiler.fgPrintEdgeWeights` binding, preserving block
and predecessor traversal order, `BB_MAX_WEIGHT` formatting, and six-digit
finite weights. The focused Windows-x64 `FlowGraphDumpTests` selection passes
55/55 Debug with zero skips.

The exact native DEBUG body and its exclusive `compiler.h` declaration were
removed from the consolidated residual (46 deletion lines). Native HEAD
`2d65721c8cc3d98ef6ebb81a74176ca4a3b24ef6` remains solely parented to the
pinned oracle; recovery ref
`refs/copilot-recovery/fg-print-edge-weights-before-90fee7a` preserves the
previous HEAD. This focused diagnostic result does not establish runtime,
generated-code, or other-target parity.

## 2026-10-03: Retiring primitive-wrapper struct promotion

Added eight data-driven cases for `TryPromoteValueClassAsPrimitive`, covering
valid integer and long wrappers plus field-count, offset, storage-size,
pointer-size, alignment, and non-primitive rejection boundaries. The focused
Windows-x64 selection passes 25/25 Debug and 21/21 Release with no skips; its
temporary test import excludes unrelated test files with existing compile and
analyzer failures. The full-analysis Windows-x64 compiler build also passes
after the `IDE0007` fix in `fa7dcc25`.

The 108-line `Compiler::TryPromoteValueClassAsPrimitive` body in `lclvars.cpp`
matched the pinned oracle and managed implementation; no native caller or
declaration remained. It was removed from the consolidated native residual at
HEAD `90fee7a580d10628500cbd8a341ef8e0ac1e5b83`, solely parented to pinned
oracle `33baf8ee337b20dd0f184b69a6f09be92850bf9e`. Recovery ref
`refs/copilot-recovery/struct-promotion-helper-before-4ae417c8` preserves the
prior HEAD. This records focused helper coverage and retirement, not broader
runtime or other-target parity.

## 2026-10-03: Retiring the LSRA reference-node pool

The committed managed `RefInfoListNodePool` and `LinearScan` reference-building
call sites match the retained native pool contract. Its 70-line constructor,
`GetNode`, and `ReturnNode` source block matched the pinned oracle exactly
(SHA-256 `ebebdbd75d2e9743d89887f3c16a00a5a89301190d36b8e24a42fdb92c8ed8ce`).
The existing LIFO reuse test passed 1/1 in Debug and Release. Consolidated
native HEAD `4ae417c8b07522b8e11f19703aa9942d6db4ea1b` removes the three bodies,
pool class/member, and exclusive friendship declarations (119 lines); no
`RefInfoListNodePool` or `listNodePool` references remain in `runtime-port`.
The commit remains solely parented to pinned oracle `33baf8ee337b20dd0f184b69a6f09be92850bf9e`;
recovery ref `refs/copilot-recovery/lsra-refinfo-pool-before-dc3a4c3c`
preserves prior HEAD. No new managed behavior or parity claim is made.

## 2026-10-03: Isolating xarch epilog sources

Moved the x86/x64 root epilog and callee-save restoration implementation into
`codegenxarch/CodeGen.Epilog.cs`. The shared frame-pointer helper and
non-xarch/Wasm entry-point dependencies remain in
`codegencommon/CodeGen.EpilogDependencies.cs`, preserving the existing
target-specific behavior. The eight-RID compile-item check includes the xarch
implementation only for x64 and x86 while retaining the common dependency file
for all targets. `CodeGenEpilogTests` passed 11/11 Debug and Release with no
skips using the existing validation import for unrelated emitter-test compile
failures. The xarch native definitions were already absent; declarations
remain for unported non-xarch definitions, so this source-ownership change
adds no native-retirement credit or other-target parity claim.

## 2026-10-03: Isolating AMD64 APX epilog emission

Moved the complete `genPopCalleeSavedRegistersFromMaskAPX` implementation from
the mixed xarch epilog partial to `codegenamd64/CodeGen.CalleeSavedRegisters.cs`.
Its native definition in `codegenxarch.cpp` is guarded by `TARGET_AMD64`, and
its managed caller is likewise AMD64-only; the new source glob is excluded when
`RyuJitTargetArchitecture` is not `x64`. The native definition was already
absent from `runtime-port`, so no new retirement is claimed. The eight-RID
compile-item check includes the new source only for x64. Windows-x64
`CodeGenEpilogTests` passed 11/11 Debug and 11/11 Release with no skips. This
source-ownership change does not claim other-target or generated-code parity.

## 2026-10-03: Keeping ARM64 block initialization target-owned

Guarded the xarch block-initialization implementation out of `TARGET_ARM64`,
where `codegenarm64/CodeGen.BlockInitialization.cs` supplies the target-owned
method. The residual native ARM64 implementation was already retired, so this
avoids a duplicate managed definition without claiming new native retirement.
Windows-x64 `CodeGenPrologInitializationTests` passed 47/47 Debug and 42/42
Release with no skips. The focused ARM64 target build still stops before test
discovery on the existing backend/API gaps recorded in `activeBatch.filePortWave`;
no ARM64 test or parity claim is made.

## 2026-10-03: Isolating xarch block-memory sources

The pinned native CMake source set includes `codegenxarch.cpp` only for AMD64
and I386; block-memory implementations for ARM, LoongArch64, RISC-V64, and Wasm
belong to their respective target sources. The managed xarch algorithms remain
in `CodeGen.BlockMemory.cs`, while terminating unported-target dependencies
move to `codegencommon/CodeGen.BlockMemoryDependencies.cs`. The xarch file is
no longer re-included for non-xarch targets, avoiding a duplicate
`genCodeForInitBlkUnroll` alongside the LoongArch implementation. Eight-RID
compile-item evaluation confirms the intended source ownership. The Windows-x64
Debug compiler build passes with code-style enforcement disabled for an
unrelated existing IDE0007 diagnostic; the LoongArch build remains blocked by
unsupported HWIntrinsic platform directives, with no block-memory duplicate
reported. This is source-selection evidence, not non-xarch compilation or
runtime parity.

## 2026-10-03: Shared call-label helpers

Moved `CodeGenInterface::genGetThisArgReg` to the shared call-target partial and
`CodeGen::genDefinePendingCallLabel` to shared label generation, matching their
native ownership in `codegencommon.cpp`. The helper-call exclusions and pending
label reset order are unchanged. Both native definitions were already retired
from `runtime-port`; this change only aligns managed source ownership.
Windows-x64 `CodeGenCallTests` passed 42/42 Debug and 31/31 Release. The focused
SysV x64 pending-call-label cases passed 6/6 in each configuration with no
skips. An unrelated varargs case failed when the entire SysV orchestration
fixture was tried; it is outside this helper change and is not counted as
acceptance evidence.

## 2026-10-03: Isolating xarch call generation

`CodeGen.Calls.cs` now stays in the xarch source set instead of being
re-included for other targets. Its terminating non-xarch `genCall` dependency
is declared in `codegencommon/CodeGen.CallDependencies.cs` for non-Wasm,
non-xarch targets; Wasm retains its separate call implementation. The xarch
call algorithm and its x86/AMD64 branches are unchanged. Compile-item
evaluation across eight RIDs selects `CodeGen.Calls.cs` only for `win-x64` and
`win-x86`, while retaining the common dependency source. Focused Windows-x64
`CodeGenCallTests` passed 42/42 Debug and 31/31 Release, with no skips. This
does not establish non-xarch compilation or runtime parity.

## 2026-10-03: Isolating xarch call-instruction emission

`CodeGen.CallInstructions.cs` now follows the same xarch-only source boundary.
Its terminating non-xarch `genCallInstruction` dependency remains in the shared
call-dependency partial because the mixed epilog binding can reference it;
Wasm continues to use its own call-instruction implementation. The xarch
emission algorithm is unchanged. Eight-RID item evaluation selects both xarch
call source files only for `win-x64` and `win-x86`. Windows-x64
`CodeGenCallTests` and `CodeGenCallInstructionTests` passed 72/72 Debug and
55/55 Release, with no skips. This is focused test/source-selection evidence,
not non-xarch compilation or parity.

## 2026-10-03: LSRA multi-register definition-list removal

The complete native `RefInfoList::removeListNode(GenTree*, unsigned)` overload
matched the pinned oracle. Its managed `BuildUse` binding removes the matching
tree/multi-register entry; a matching tree without a reference position now
fails explicitly instead of being silently skipped, preserving the native
non-null invariant. A regression verifies the failure leaves the definition
list unchanged. Focused Windows-x64 `LinearScanReferenceBuildingTests` passed
8/8 Debug and 7/7 Release, with zero skips. Code-style enforcement was disabled
to bypass unrelated compile failures; temporary imports excluded only
`EmitterPrefixTests.cs` in both configurations and
`EmitterRegisterInstructionTests.cs` in Release. This is focused test evidence,
not a full-suite or cross-target parity claim.

The consolidated native residual removes the 23-line `lsrabuild.cpp` overload
and its exclusive `lsra.h` declaration. Native HEAD
`3f4d6f6dc96dfb1fb590b0ecfa58447bc5c6e9d5` (tree
`353bbbf18899000e7b189951d7c3534d66b84f6d`) is sole-parented to the pinned
oracle. Recovery ref
`refs/porting/recovery/2026-10-03-remove-list-node-before-amend` preserves
prior HEAD `7738d438682246a7b2126461c6dc026f8bd75cef`.

## 2026-10-03: LSRA single-register definition-list removal

The remaining native `RefInfoList::removeListNode(GenTree*)` body and its inline
unlink helper matched the pinned oracle. The managed
`RemoveListNode(tree, 0)` path is covered by the existing focused LSRA tests,
including index-0 removal and list-order preservation. The native helper,
overload, and exclusive declaration were retired together; no native residual
callers remain. This is the same focused test evidence recorded above, not a
new test run or a parity claim.

The consolidated native residual is now HEAD
`b142eedc8ee2131af89d4085f75ea76cf9048dec` (tree
`1e0f76fb6b9b5ad475793f7bbb70043bdef935bf`), sole-parented to the pinned
oracle. The deletion removes 52 lines across `lsrabuild.cpp` and `lsra.h`.
Recovery ref
`refs/porting/recovery/2026-10-03-remove-list-node-single-before-amend`
preserves pre-amend HEAD `3f4d6f6dc96dfb1fb590b0ecfa58447bc5c6e9d5`.

## 2026-10-03: EE scope-name release stubs

The two retained non-DEBUG `LATE_DISASM` null stubs for
`CodeGen::siRegVarName` and `CodeGen::siStackVarName` matched the pinned
oracle exactly. Their managed implementations preserve the DEBUG scope lookup
and null fallback; `ScopeReportingTests` already covers register- and
stack-variable name lookup. Only the release stub block was removed here: the
original DEBUG bodies in `scopeinfo.cpp` were already absent, and the native
`codegen.h` declarations were already absent. Three unported `disasm.cpp`
call sites remain in the non-buildable residual. No new test run, parity claim,
or credit for the earlier DEBUG-body retirement.

The consolidated native residual is now HEAD
`dc3a4c3c765cb442714c1c8926fb3fb180e137f1` (tree
`7d5ea8e33a2a8414e69ff955a7aa7e336f9bf25d`), sole-parented to the pinned
oracle. The amendment removes the 30-line stub block and adjacent trailing
blank lines from `codegencommon.cpp`. Recovery ref
`refs/porting/recovery/2026-10-03-scope-var-name-stubs-before-amend`
preserves pre-amend HEAD `b142eedc8ee2131af89d4085f75ea76cf9048dec`.

## 2026-10-03: Physical-promotion remainder strategy

Completed `DecompositionPlan::DetermineRemainderStrategy` with the native
`TARGET_64BIT`, `FEATURE_SIMD`, and nested `TARGET_XARCH` selection gates.
Focused Windows-x64 `PhysicalPromotionTests` passed 60/60 in Debug and 59/59
in Release, with zero skipped tests. The runs retained `AnalysisLevel=latest-all`
and analyzers, but set `EnforceCodeStyleInBuild=false` and
`TreatWarningsAsErrors=false`; temporary imports excluded only unselected,
pre-existing compile-broken fixtures (`EmitterPrefixTests.cs` in both
configurations and `EmitterRegisterInstructionTests.cs` in Release). This is
focused regression evidence, not a clean full-project pass or generated-code
parity claim.

The consolidated native residual retires the complete 133-line
`promotiondecomposition.cpp` file. Native HEAD `5c045e8f` is sole-parented to
the pinned oracle, and recovery ref
`refs/heads/recovery/decomposition-remainder-11154dd6104f` preserves the prior
residual HEAD.

## 2026-10-03: Target-owned source filtering

Conditional compile-item filters exclude pure ARM, ARM64, LoongArch64,
RISC-V64, Wasm, AMD64, and x86 source folders from nonmatching target builds.
On non-xarch targets, the `jit\*xarch\**\*.cs` folder glob is followed by
re-inclusion of mixed fallback sources, leaving the 53 complete
`TARGET_XARCH`-only files excluded. There is no `*armarch` folder in this
revision; the single complete `TARGET_ARMARCH` source is covered by a recursive
filename glob. ARMv6 is included in the ARM32 source group. MSBuild item
evaluation was checked for `win-x64`, `win-x86`, `linux-arm`, `linux-arm64`,
`linux-armv6`, `linux-loongarch64`, `linux-riscv64`, and `browser-wasm`.
AMD64 unwind sources are now selected only for `win-x64`; the xarch and
ARMARCH groups use `RyuJitTargetIsXarch` and `RyuJitTargetIsArmarch`, while
the ARM32 group uses `RyuJitTargetIsArm`. The focused Windows-x64 Debug suite
passes 63/63. The browser-Wasm rebuild no longer reports
the 14 duplicate xarch definitions, but remains blocked by 263 compilation and
analyzer diagnostics, and no tests ran. This verifies source selection and
selected Windows tests, not build or execution parity for the other targets.

## 2026-10-03: Call-argument ABI reclassification

The complete managed `CallArgs.AddFinalArgsAndDetermineABIInfo` method and both
caller bindings match the pinned native method across all 217 lines. A
regression reclassifies a call from five integer arguments to one and verifies
register/stack flags and outgoing stack size are recomputed. Focused Windows-x64
`CallArgumentMorphTests` passed 71/71 Debug and 69/69 Release, with zero skips.
Native HEAD `7738d438` is sole-parented to the pinned oracle and removes the
complete `morph.cpp` definition and now-unused `gentree.h` declaration.
Recovery refs `recovery/add-final-args-5c045e8f-20261003-0841` and
`recovery/add-final-args-body-a4217c0ab4-20261003` preserve the prior native
state. The Wasm shadow-stack branch is source-preserved but not validated by
these Windows-x64 tests; no other-target parity is claimed.

## 2026-10-03: Shared and AMD64 CFI unwind

Completed the shared function-location, CFI recording, debug-dump, and
publication paths together with AMD64 DWARF mapping and unwind dispatch. The
Unix CFI feature symbol follows native `target.h`; Windows x64 continues to use
its existing unwind encoding. Focused Windows-x64 Debug tests passed 63/63.
Unix x64 CFI/frame target selections passed 66/66 in both Debug and Release,
with zero skips.

The consolidated native residual now removes 13 complete definitions and 12
exclusive declarations, retaining the shared DWARF-map declaration for other
unported targets. Native HEAD `11154dd6` remains sole-parented to the pinned
oracle; recovery ref `refs/heads/recovery/unwind-shared-amd64-503e8263795e`
preserves its prior HEAD. These tests validate selected recording/publication
contracts, not Linux generated-code execution or general target parity.

## 2026-10-03: Shared lowering helper retirement

Retired the complete shared `Lowering` helper family: `ContainCheckLclHeap`,
`ContainCheckRet`, and `InsertNewSimdCreateScalarUnsafeNode`, mapped to their
existing managed implementations. The focused Windows-x64
`LocalHeapLoweringTests`, `ReturnLoweringTests`, and
`HardwareConstructionLoweringTests` passed together, 130/130 in Debug and
130/130 in Release, with zero skips.

The complete helpers and their exclusive declarations are retired from the
native residual. The residual `lowerwasm.cpp` caller remains as unported Wasm
code; this focused Windows-x64 coverage does not establish other-target or
full-pipeline parity.

## 2026-10-03: Shared block fall-through predicate

Retired `BasicBlock::CanRemoveJumpToTarget` after preserving the native
fall-through guards and Wasm interval-boundary branch. The Windows-x64
`FlowGraphBranchOptimizationTests` passed 22/22 in Debug. The browser-Wasm
rebuild after the xarch guards reports no CS0111 duplicates but remains blocked
before test discovery by 263 compiler/analyzer diagnostics, including missing
`GetEmitter`, `genActualType`, and Wasm opcode/tree/register APIs; zero tests ran.
The complete managed
`WasmInterval` class is implemented and its native class definition retired;
target coverage remains pending combined validation. This does not establish
Wasm execution parity.

## 2026-10-03: xarch register-info binding

Reconciled the native `CodeGenInterface::CopyRegisterInfo` binding with the
existing `CodeGen.CopyRegisterInfo` implementation. The managed method copies
the initialized AMD64 floating-point/integer register masks, callee-trash masks,
last integer register, and xarch mask-register state. Added a Windows-x64
regression that checks all seven copied values against the compiler state.
`CodeGenEmissionPhaseTests` passes 9/9 Debug and 3/3 Release, with zero skips.

The residual contained only a seven-line empty `TARGET_XARCH` definition; the
native declaration was already absent. Consolidated native HEAD `e01ac74f`
removes the definition and remains sole-parented to the pinned oracle. Recovery
ref `refs/copilot-recovery/copy-register-info-before-b390652e` preserves the
previous native HEAD. This binding-level test is not generated-code or runtime
parity.

## 2026-10-03: LSRA kill-reference construction

Reconciled the existing managed `LinearScan.addKillForRegs` helper with its
retained native definition. The complete native body matched the pinned oracle
exactly (720 bytes, 18 lines; SHA-256
`1c168d1a7a512a10ac3955b52d9acef063f1cd91778fde4690e667a47154f2cd`). Managed
register tracking preserves the full mask, the native `DEBUGARG(true)` dump
suppression, the low-bank initial assignment, and kill-list insertion order.

The focused Windows-x64
`KillBuilderLinksInLocationOrderAndMarksFullMasksModified` fixture passes 1/1
in Debug and Release, with zero skips. Native `b390652e` amends the consolidated
residual commit, sole-parented to the pinned oracle, and removes the complete
definition and its exclusive `lsra.h` declaration (27 deletion lines). No
native callers remained in the residual tree. Recovery ref
`refs/copilot-recovery/lsra-add-kill-for-regs-before-4e92683f` preserves the
previous native HEAD. This helper-level result does not establish full
compiler-pipeline, runtime, or generated-code parity.

## 2026-10-03: LSRA async-continuation busy reference

Reconciled the retained shared `LinearScan::MarkAsyncContinuationBusyForCall`
with the existing AMD64 and ARM managed caller paths. The complete native body
matched the pinned oracle exactly (640 bytes, 12 lines; SHA-256
`7f852e4d7b7138d09e53e9e9c4f970e2949943e58a2bb432f0f3c1677cf103e7`). The
AMD64 path reserves the async-continuation return register and delays its kill
until after the call; the managed regression confirms location and ordering.

The focused Windows-x64 `AsyncContinuationKillIsDelayedBeforeTheCallKill`
fixture passes 1/1 in Debug and Release with zero skips. A broader Debug class
filter also encountered four `WindowsVarargsFloatsReserveTheCorrespondingIntegerRegister`
`IndexOutOfRangeException` failures at the existing fixture's line 291; these
are outside the async-continuation case and were not changed.

Native `4e92683f` amends the consolidated residual commit, sole-parented to
the pinned oracle, and removes the complete method and its `lsra.h` declaration
(22 deletion lines). Recovery ref
`refs/copilot-recovery/lsra-async-continuation-before-18ed4305` preserves the
previous native HEAD. Unported LoongArch64/RISC-V caller definitions remain in
the residual ledger without this translated declaration. This is focused call
reference validation, not full compiler-pipeline, runtime, or generated-code
parity.

## 2026-10-03: LSRA allocation reference-row diagnostics

Reconciled the complete DEBUG reference-row formatter family with its existing
managed mappings in `LinearScan.AllocationDiagnostics.cs`. The managed short
reference formatter now asserts that an unassociated reference is a kill or
kill-GC-refs reference, matching the native diagnostic contract.
Focused Windows-x64 allocation, record, traversal, and interval diagnostic
fixtures pass 117/117 Debug and 17/17 Release, with zero skips.

All five native bodies matched the pinned oracle byte-for-byte. Their
SHA-256 hashes are `d7673ff473915fe8aab3d4522ca491a3a8057799f402890ec22737b078927a40`
(`dumpIntervalName`), `56113963fcca54a1caa0f95746f2239d910e9b87279aa7ac836070903bcfdaa4`
(`dumpEmptyTreeID`), `219bf42dc5ebba5e9c111a764fb4d7a28734210dabd4cae2e767164b5b525f40`
(`dumpEmptyRefPosition`), `ab331300d0084d95793de85d8ad64558234e1aeeee2ec105805d03b76de86ba9`
(`dumpNewBlock`), and `6fe449e1565efabcadde3afa9735301ddb4aa3caba072dfd586e231811aa5b39`
(`dumpRefPositionShort`).

Native `18ed4305` amends the consolidated residual commit, sole-parented to
the pinned oracle, and removes all five definitions and their exclusive
DEBUG declarations (158 deletion lines). No native callers remain outside
the helper cluster. Recovery ref
`refs/copilot-recovery/lsra-reference-dump-before-fd8d2384` preserves the
previous native HEAD. The managed formatter keeps duplicate-row suppression
per `LinearScan` instance rather than reproducing the native function-static
pointer's stale cross-instance behavior; see [D011](DEVIATIONS.md#d011-lsra-reference-row-duplicate-state).
These DEBUG-only checks establish neither cross-instance dump parity nor
generated-code or runtime parity.

## 2026-10-03: LSRA interval and def-use native retirement

Reconciled the already-implemented `LinearScan.newInterval` and
`LinearScan.resolveConflictingDefAndUse` managed methods with the remaining
native definitions and exclusive declarations. Focused Windows-x64
`LinearScanRegisterSelectionTests`, `LinearScanReferenceBuildingTests`,
`LinearScanDefinitionConstructionTests`, and `LinearScanAllocationEventTests`
pass 28/28 Debug and 23/23 Release, with zero skips.

Both complete native bodies matched the pinned oracle exactly. Native
`df0cda1e` amends the consolidated residual commit, sole-parented to the pinned
oracle, and removes the definitions, their rationale, and two exclusive
`lsra.h` declarations (178 deletion lines). Recovery ref
`refs/copilot-recovery/lsra-defuse-before-209f008a` preserves the prior native
HEAD. `addKillForRegs` remains because residual LoongArch64/RISC-V callers are
unported. This is a mapped-retirement reconciliation, not a new runtime or
generated-code parity claim.

## 2026-10-03: LSRA call-argument-use native retirement

Reconciled the existing `LinearScan.buildCallArgUses` mapping with its retained
native definition. Focused Windows-x64 `LinearScanOperandAndCallUsesTests` pass
6/6 Debug and 6/6 Release, with zero skips.

The complete 58-line native body matched the pinned oracle exactly (1,901
bytes; SHA-256
`79974608a86b03b96f7315b9671acb7f24605833da46fa263c367fe8f726bf45`).
Native `5c8d0cb5` amends the consolidated residual commit and removes the body
and rationale (65 deletion lines). Recovery ref
`refs/copilot-recovery/lsra-call-args-before-df0cda1e` preserves the previous
native HEAD. The shared `lsra.h` declaration remains for untranslated
LoongArch64 and RISC-V callers. This mapped retirement is not a new runtime or
generated-code parity claim.

## 2026-10-03: LSRA interval-validation native retirement

Reconciled the complete DEBUG-only `LinearScan.validateIntervals` mapping with
its accepted managed implementation. Focused Windows-x64
`LinearScanBuildIntervalsTests` and `LinearScanLocalIntervalConstructionTests`
pass 18/18 Debug and 17/17 Release, with zero skips. The managed diagnostic now
matches native null handling for `compMethodName`, including the distinction
between null and an empty name.

The complete 60-line native body matched the pinned oracle exactly (2,245
bytes; SHA-256
`cb8d2fc0e6bae5a23ed21529d03d99a1a97466d62645c3df261826c52ff92184`).
Native `fd8d2384` amends the consolidated residual commit, sole-parented to the
pinned oracle, and removes the DEBUG-only definition and its exclusive
`lsra.h` declaration (76 deletion lines). Recovery ref
`refs/copilot-recovery/lsra-validate-intervals-before-5c8d0cb5` preserves the
previous native HEAD. The native residual had no remaining callers. This
mapped retirement is not a runtime or generated-code parity claim.

## 2026-10-03: Bounded peephole history traversal

The managed emitter now preserves the complete instruction-history traversal
used by xarch register peepholes: it visits a bounded sequence of prior
instructions and stops at unsafe group or GC-interrupt boundaries. Other
targets retain the native direct-last-instruction fallback. Existing caller
semantics and peephole candidate rules are unchanged.

## 2026-10-03: Xarch memory-operand size calculation

The managed xarch emitter now handles memory-operand widths for tuple-encoded
instructions, including embedded broadcasts and immediate-dependent forms.
Instruction display and static output continue to share the native sizing
rules. The complete definition and its exclusive declaration are retired from
the residual native tree; retirement alone does not claim runtime or
generated-code parity.

## 2026-10-03: Jump-list diagnostic retirement

Completed the whole DEBUG `emitter::emitDispJumpList` mapping in
`Emitter.GroupDiagnostics.cs`, preserving the native common target-cookie
display, ARM64 `IF_LARGEADR`/`IF_LARGELDC` register branch, xarch removable-jump
marker, AMD64 after-call marker, and target-aware instruction names.
Full-analysis Windows-x64 `EmitterJumpInstructionTests` pass 78/78 Debug and
70/70 Release. ARM64-target projections cover both large-address formats (2/2
Debug); they execute managed target logic on Windows x64, not ARM64 code.

The complete 60-line residual method matches the pinned oracle exactly
(SHA-256 `4A60C9C96B35B7D37CB61DB6A87BBCE14C9E431E66C68D98F3D4E8A0D759DA1D`).
Native `dbb6f11a` amends the consolidated residual commit, sole-parented to the
pinned oracle, and removes the definition/rationale plus its exclusive
declaration (65 deletions). Recovery ref
`refs/copilot-recovery/emit-disp-jump-list-before-60dcfd8a` preserves the prior
native HEAD. A broader ARM64 label-class selection has two failures in
`ChangedCallLivenessReachesTheRequiredPaddingDependency`, outside this
diagnostic; no generated-code or runtime parity is claimed.

## 2026-10-03: Frame-reference diagnostic retirement

Completed the target-family `emitter::emitDispFrameRef` mapping in
`Emitter.InstructionDisplay.Support.cs`. The existing xarch frame-layout
formatter is unchanged; ARM/ARM64 preserve temporary/local names, lowercase
hex displacements, and optional scoped local names. LoongArch64/RISC-V retain
their terminating target-specific NYI boundaries.

Full-analysis Windows-x64 `EmitterXarchDiagnosticHelpersTests` pass 25/25 in
Debug and Release. ARM32- and ARM64-target output tests pass 3/3 Debug and 1/1
Release each on a Windows-x64 host; these validate managed target logic, not
target execution or generated-code parity. LoongArch64/RISC-V target builds
remain blocked by unrelated unsupported-platform/compiler diagnostics and are
not claimed as passing.

Native ARM, ARM64, LoongArch64, and RISC-V definitions were each verified
byte-identical to the pinned oracle before retirement. Native `20a01c44`
retires the four definitions and shared `emit.h` declaration (108 lines total)
in the consolidated residual commit, sole-parented to the pinned oracle.
Residual ARM/ARM64 callers intentionally need no retained declaration.
Recovery ref
`refs/copilot-recovery/emit-frame-reference-decl-before-99901ba` preserves
prior native HEAD `09779822`. No new runtime or emitted-code parity is claimed.

## 2026-10-03: GC diagnostic prototype reconciliation

Reused the accepted shared-GC diagnostic mapping and its full-analysis evidence:
111 Debug / 49 Release Windows executions, 100 / 44 Linux-target Core
executions, and 5/5 SysV target executions. `EmitterSharedGCDiagnosticClosureTests`
directly exercises the managed `emitDispGCinfo` implementation. The pinned
oracle has no native call sites for this standalone diagnostic, and the native
body was already absent from `runtime-port`; native `0803f7da` removes its last
one-line `emit.h` prototype while amending the consolidated residual commit.
Recovery ref
`refs/copilot-recovery/emit-disp-gcinfo-decl-before-43f9ce50` preserves the
prior native HEAD. This declaration-only reconciliation reuses prior evidence
and makes no new parity claim.

## 2026-10-03: Group-list prototype reconciliation

Reused the accepted `Emitter.GroupDiagnostics.cs::emitDispIGlist` mapping and
existing full-analysis Windows-x64 `EmitterGroupDiagnosticsTests` evidence
(298 Debug / 281 Release, zero skips in the prolog-materialization selection).
The managed method is unchanged from the committed `8d85aa87` version. The
residual native body and callers were already absent; native `43f9ce50` removes
the final one-line `emit.h` prototype, amending the consolidated residual
commit. Recovery ref
`refs/copilot-recovery/emit-disp-iglist-decl-before-dbb6f11a` preserves the prior
native HEAD. The accepted `emitDispIG` implementation in the same managed
partial was also already absent, along with its residual native callers; native
`20a01c44` removes its stale four-line shared prototype. Recovery ref
`refs/copilot-recovery/emit-disp-ig-decl-before-09779822` preserves the prior
native HEAD `09779822`. Both declaration-only reconciliations reuse existing
full-analysis Windows-x64 `EmitterGroupDiagnosticsTests` evidence (298 Debug /
281 Release); they required no new managed test run and make no new parity
claim.

## 2026-10-03: Instruction-group allocator retirement

Accepted the existing complete `emitter::emitAllocIG` mapping in
`Emitter.InstructionGroups.cs`, preserving DEBUG initialization,
`EMITTER_STATS` accounting, and the `emitInitIG` call order. Focused
full-analysis Windows-x64 `EmitterInstructionGroupTests` pass 10/10 in Debug
and Release, covering group metadata, initialization, flags, and allocation
linking.

The 26-line native method matches the pinned oracle exactly. Native
`60dcfd8a` amends the consolidated residual commit, sole-parented to the pinned
oracle, and removes the method/rationale plus its exclusive declaration (33
deletions). Recovery ref
`refs/copilot-recovery/emit-alloc-ig-before-279c8d19` preserves the previous
native HEAD. No generated-code or runtime parity is claimed; unsupported
`EMITTER_STATS` sizing remains an explicit terminating target boundary.

## 2026-10-03: Shared handle-comment formatting

Moved the complete `emitter::emitDispCommentForHandle` binding from the xarch
partial into the shared `Emitter` partial, preserving the native cookie and
handle cases, DEBUG object descriptions, and xarch (`;`), Wasm (`;;`) and
other-target (`//`) comment prefixes. Full-analysis Windows-x64
instruction-display and handle-comment selections pass 95 Debug / 52 Release;
ARM64-target and browser-Wasm target-prefix projections pass 8/8 Debug each.

The residual and pinned oracle definitions match after line-ending
normalization. Native `279c8d19` amends the consolidated residual commit,
sole-parented to the pinned oracle, and removes the complete method with its
attached rationale plus the exclusive declaration (119 deletion lines).
Recovery ref
`refs/copilot-recovery/emit-handle-display-before-555d7a3d` preserves the prior
native HEAD. These target tests exercise managed target logic on Windows x64;
no ARM64/Wasm machine execution, generated-code, runtime, or whole-pipeline dump
parity is claimed.

## 2026-10-03: SIMD local-alignment retirement

Revalidated the existing whole `Compiler::isSIMDTypeLocalAligned` mapping in
`Compiler.LocalAlignment.cs`; live codegen callers use it when selecting local
load/store instructions. Full-analysis Windows-x64 `CodeGenStoreSelectionTests`
pass 28/28 in Debug and Release, including frame-pointer and stack-pointer
alignment, vector widths and the return-address bias.

The pre-amend residual contained a C#-shaped 35-line method, so it was not
treated as textually equal to the pinned C++ body. Full-flow comparison and the
focused alignment tests verified the managed mapping. Native `555d7a3d` amends
the consolidated residual commit, sole-parented to the pinned oracle, and
removes the complete native method and its attached rationale. Recovery ref
`refs/copilot-recovery/is-simd-local-aligned-before-6d229a56` preserves the
previous native HEAD. No emitted-code or runtime parity is claimed.

## 2026-10-03: EE variable diagnostic retirement

Accepted the existing Debug-only `Compiler::eeDispVar` binding, used by the
managed `eeDispVars` reporter. `ScopeReportingTests` and
`CodeGenSiVarLocDiagnosticsTests` pass 30/30 Debug and 19/19 Release. New
focused cases cover the varargs, return-buffer and type-context names and the
unknown-variable fallback; existing location tests cover Windows-x64 register,
floating-register, stack and register-pair formatting.

Native `6d229a56` amends the consolidated residual commit, sole-parented to the
pinned oracle, and removes the complete 135-line method plus its DEBUG guard
and exclusive `compiler.h` declaration. Recovery ref
`refs/copilot-recovery/ee-disp-var-before-00cad7dc` preserves the previous
native HEAD. The method is Debug-only diagnostic output; no runtime or
non-Windows-x64 parity is claimed.

## 2026-10-03: Register-allocation stack-home method retirement

Accepted the existing whole `Compiler::raMarkStkVars` implementation after
confirming its managed LSRA callers and matching the pinned native method.
Focused Windows-x64 `RegisterAllocationStackHomeTests` pass 5/5 in Debug and
Release, covering stack homes, register locals, dependent promoted fields,
frame-pointer classification and zero-sized outgoing argument space.

Native `00cad7dc` amends the consolidated residual commit, sole-parented to the
pinned oracle, and removes the complete 119-line `regalloc.cpp` definition.
The `compiler.h` declaration remains for the unported `regallocwasm.cpp` caller.
Recovery ref `refs/copilot-recovery/ra-mark-stk-vars-before-a7b5d0b` preserves
the previous native HEAD. Other-target callers still terminate through the
managed NYI boundary; no non-Windows-x64 runtime parity is claimed.

## 2026-10-03: GS shadow-copy helper retirement

Completed the managed `Compiler::gsCopyIntoShadow` binding used by
`gsParamsToShadows`, including the Windows-x64 LIR insertion and debug tree-range
dump. The x86 IJW branch now checks `argRequiresSpecialCopy` and rejects only
the unported special-copy helper path; ordinary struct arguments retain the
native direct-copy path. Full-analysis Windows-x64 `GSSecurityPhaseTests` pass
14/14 in Debug and Release, with no skips.

Native `a7b5d0b0` amends the consolidated residual commit, sole-parented to the
pinned oracle, and removes the complete `gschecks.cpp` definition and its
exclusive `compiler.h` declaration. Recovery ref
`refs/copilot-recovery/gs-copy-into-shadow-before-c96fd54` preserves the prior
native HEAD. The guarded x86 regression is source-only because the existing x86
target build is blocked by unrelated missing definitions; no x86 execution or
generated-code parity is claimed.

## 2026-10-03: Error-trap forwarding wrappers

Added direct tests for the existing typed managed error-trap wrappers,
verifying the selected normal/SPMI EE callback, opaque parameter forwarding,
successful invocation and EE rejection. Full-analysis Windows-x64
`ErrorTrapTests` pass 17/17 in Debug and Release.

Native `c96fd54f` amends the consolidated residual commit, sole-parented to the
pinned oracle, and removes both complete `eeRunWithErrorTrapImp` and
`eeRunWithSPMIErrorTrapImp` definitions. Their declarations remain for native
template callers that are not yet ported. Recovery ref
`refs/copilot-recovery/error-trap-before-be5f661d` preserves the previous
native HEAD. Existing managed callback exception, terminal-HRESULT and
SPMI-only behavior is unchanged.

## 2026-10-03: Unwind EE callback wrappers

Retired the native definitions for `Compiler::eeReserveUnwindInfo` and
`Compiler::eeAllocUnwindInfo` after confirming the complete managed wrappers
preserve debug diagnostics, matched-VM gating and callback arguments.
Full-analysis Windows-x64 `UnwindPublicationTests` pass 11/11 Debug and 6/6
Release, covering reservations, allocations, hot/cold records, funclets and
diagnostics.

Native `be5f661d` amends the consolidated residual commit, sole-parented to the
pinned oracle, and removes both complete wrapper definitions. The declarations
remain because native callers in the unported unwind implementations still
depend on them. Recovery ref
`refs/copilot-recovery/unwind-ee-wrappers-before-1f5cb902` preserves the
previous native HEAD. Other-target unwind generation, B395 CFI and runtime
parity are not claimed.

## 2026-10-03: Cross-target code allocation

Completed the whole `Compiler::eeAllocMem` method and its relocation-policy
helper, preserving the Windows-x64 separate-chunk path and the ARM64, LoongArch,
and RISC-V aligned embedded-data and relocation-chunk branches. Full-analysis
Windows-x64 allocation tests pass 6/6 Debug and 4/4 Release; focused
linux-arm64-target tests pass 2/2 Debug and 2/2 Release.

Native `1f5cb902` amends the consolidated residual commit, sole-parented to the
pinned oracle, and retires both complete `ee_il_dll.cpp` definitions and the
eligible `eeAllocMem` declaration. The relocation-helper declaration remains
for three unported native codegen callers. Recovery ref
`refs/copilot-recovery/ee-alloc-mem-before-5792c511` preserves the previous
native HEAD. This validates allocation-policy fixtures, not generated-code or
runtime parity; the remaining native callers are not claimed as ported.

## 2026-10-03: Late liveness native retirement

Restored the omitted `assert(backendRequiresLocalVarLifetimes())` in the already
translated whole `Compiler::fgLateLiveness` method. Full-analysis focused
Windows-x64 `LoweringPhaseTests` and `LivenessOrchestrationLIRTests` pass 35/35
Debug and 32/32 Release.

Native `5792c511` amends the consolidated residual commit, sole-parented to the
pinned oracle, and removes the complete residual `lower.cpp` definition (48
deletion lines). Recovery ref
`refs/copilot-recovery/fg-late-liveness-before-2ab01486` preserves its previous
HEAD. These phase tests do not establish generated-code or whole-runtime parity;
the remaining optional debt cursor is recorded in `state.json`.

## 2026-10-03: Cross-target local reference counting

Removed the Windows-AMD64-only rejection from the whole
`Compiler::lvaComputeRefCounts` method, retaining its native imprecise fast path
and shared precise-refcount dispatch. Full-analysis Windows-x64
`LocalReferenceAccountingTests` plus the target-neutral fixture pass 34/34
Debug and 33/33 Release. The target fixture passes 2/2 Debug and Release for
linux-arm64 and 2/2 Debug for browser-wasm.

Native `2ab01486` retires the complete 100-line residual definition and its
declaration; the two `lower.cpp` callsites remain because their caller is
unported. Cross-target cases exercise MinOpts initialization and precise
accounting with an empty block list, not whole-pipeline or generated-code
parity. Evidence is recorded in `state.json`.

## 2026-10-03: Cross-target funclet creation

Completed the whole `Compiler::fgCreateFunclets` method without the prior
AMD64-only rejection. The common EH ordering and relocation algorithm is retained,
with native non-fixed-register and Wasm descriptor defaults. Full-analysis
Windows-x64 funclet tests pass 6/6 in Debug and Release; target-neutral descriptor
tests pass 1/1 Debug and Release for linux-arm64 and 1/1 Debug for browser-wasm.
The browser-wasm source build has no warnings or errors.

Native `2ab01486` retains the complete 126-line residual definition retirement
before this managed completion. The recovery ref preserves the prior
consolidated native HEAD. Cross-target tests cover descriptor defaults, not
generated-code or EH runtime parity; `FuncInfoRange` remains required by the
unported Wasm allocator.

## 2026-10-03: Emitter method statistics

Ported the whole `emitterStats(FILE*)` report for xarch and bound the shutdown
caller. Unsupported targets terminate explicitly until their counter sources are
ported. Focused stats-enabled Windows-x64 tests pass 27/27 Debug and 26/26
Release. The full stats-enabled suites retain 18 Debug and 13 Release failures
outside the reporter and histogram paths; these are not full-suite passes.
Deviation R006 records the managed safety correction for the pinned native
histogram's undefined leading-zero behavior, so those rows do not claim native
output parity.

Native `98392fc2` amends the consolidated residual commit, preserving the
pinned oracle as its sole parent. It retires the complete reporter, its global
prototype/friend, and reporter-only native storage before this managed
completion. Evidence is recorded in `state.json` and
`artifacts\emit-stat-report`.

## 2026-10-03: Small support helpers and native census

Completed the remaining target metadata and Wasm value-name/reference-increment
helpers, `LIR.InsertBeforeTerminator`/`dumpLIRFlags`, and host-allocation
wrappers in managed commits `20992fa8` and `b7e75c7d`. Focused full-analysis
support tests pass 40/40 in Debug and Release; six target metadata selections
pass in both configurations (12 executions). These are helper/metadata checks,
not backend execution or generated-code parity.

The pinned-residual census checked 159 files (151 direct plus eight `jitstd`
headers), including all 61 files at or below 10,240 bytes. It retired 31 files,
modified 107, and removed 3,257 lines before the subsequent reporter retirement;
14 of the 61 small files were retired and 47 retained with concrete blockers.
The final native delta also includes the reporter. `valuenum.cpp` contained
only dead scaffolding and `s_specialRefConsts[] = {nullptr, nullptr, nullptr}`,
represented by the accepted zero-initialized three-entry `TYP_REF` chunk
(reserved VNs 0/1/2). The original acceptance receipt was reused. The scan
does not claim global debt clearance. Exact dispositions and receipts are in
`artifacts\native-retirement-census`.

## 2026-10-02: Managed bitset iterator

Added the stateful `BitSetOps<TEnv, TBitSetTraits>.Iter` over the existing
managed word representation. It preserves ascending bit order, unsigned bit
indexes, native word-width advancement and the caller's output value when
iteration ends. Full-analysis focused Windows-x64 `BitSetSupportTests` pass 2/2
in Debug and Release. Native `018dc5cb` retires the remaining
`bitsetasshortlong.h` specialization from the consolidated residual commit.
The disabled `BITSET_TRACK_OPCOUNTS` hook remains an explicit NYI boundary under
D010; the optional counter wrapper is not wired into production.

## 2026-10-02: Managed bitset operation counter

Ported `BitSetSupport.Operation`, the operation-name table, and the complete
`BitSetOpCounter.RecordOp` diagnostic, preserving unsigned counter wrap, the
million-operation interval, native ordering and output formatting. Full-analysis
focused Windows-x64 `BitSetSupportTests` pass 1/1 in Debug and Release. Native
`c5d0fdd1` retires the mapped counter implementation from the consolidated
residual commit. `BitSetOpsWithCounter` and its disabled activation path remain
untranslated; this unit claims neither production counter wiring nor runtime
parity.

## 2026-10-02: Managed ArrayStack

Ported the complete `ArrayStack<T>` helper, including growth, push/emplace/pop,
indexed access and references, reset/reverse, contiguous active data, and
bottom-up/top-down views with Debug mutation checks. Managed `T[]` storage
replaces the native allocator-backed buffer under D001. Focused `ArrayStackTests`
pass 3/3 in Debug and Release. Native `33b0adc9` retires `arraystack.h`, its
allocation-kind entry and the associated forward declaration in the consolidated
residual commit. Remaining native callsites are untranslated; no generated-code
or runtime parity is claimed. Evidence is recorded in `state.json`.

## 2026-10-02: Managed allocation statistics

Enabled optional `MEASURE_MEM_ALLOC` reporting from per-root-compilation managed
GC-allocation deltas, with thread-safe aggregate/maximum summaries and a
rounded-up KB histogram. Shutdown explicitly reports native arena-used bytes as
unavailable, consistent with D001. Full-analysis focused Windows-x64 shutdown
tests pass 4/4 Debug and 3/3 Release with the feature enabled, and 2/2 Debug and
1/1 Release with the normal configuration. Native `edb48985` retires the
corresponding `JitMemStatsInfo` declarations/definitions and histogram globals
from the consolidated residual commit. Evidence is recorded in `state.json`;
these results are diagnostic unit coverage, not runtime or generated-code
parity.

## 2026-10-02: ARM/ARM64 callee-save construction

Completed ARM32/ARM64 callee-save algorithms and bound the production
prolog/epilog entry points. ARM64 retains its distinct
`genPopCalleeSavedRegistersAndFreeLclFrame` contract; frame types, register
pairing, save-next handling, varargs saves and frame-pointer restoration retain
the native ordering. Unwind and pointer-authentication helpers remain explicit
terminating dependencies.

Full-analysis ARM64-target tests pass 74 Debug/74 Release cases, and Windows-x64
controls pass 125 Debug/125 Release cases. The `win-arm` attempt fails during
project compilation before test discovery at the existing unsupported-platform
and conditional-syntax errors; no ARM32 tests ran. Native `9b3a6d65` retires
19 complete oracle-matching definitions and ARM/ARM64-specific declarations in
the consolidated cleanup commit. The residual keeps the pinned oracle as its
sole parent and retains the pre-retirement recovery ref. This is managed target
coverage, not ARM execution or generated-code parity. Evidence is recorded in
`state.json` and `artifacts/callee-save-{arm64,windows-x64}`.

## 2026-10-02: ARM64 call and long-jump output

Bound the ARM64 call and long-jump production paths to their completed output
cores. The 82 focused call/long-jump tests pass in Debug and Release using the
Windows ARM64 target on a Windows x64 host, including call GC bookkeeping,
branch-range boundaries, label relocations and literal loads.

Native `0bbeacfe` retires seven complete output definitions, seven declarations
and their exclusive headings (722 lines), each verified against the pinned
oracle. The consolidated residual retains its sole oracle parent and a recovery
ref. Non-Windows ARM64 call-descriptor layout remains an explicit terminating
dependency; `emitOutput_Instr` remains outside this unit. These are managed
target tests, not ARM64 execution or generated-code parity. Evidence:
`artifacts/tst/{Debug,Release}/Arm64CallAndLongJump.WinArm64.*.trx`.

## 2026-10-02: ARM64 intrinsic codegen and opcode emission

Completed ARM64 hardware-intrinsic codegen and the ordinary `emitInsCode`
selector. `GenerateTables` now emits all nine compact opcode columns from
`instrsarm64.h`; an isolated regeneration matches the checked-in table exactly.
Full-analysis ARM64-target tests pass 98 Debug/94 Release opcode cases and 72
Debug/72 Release intrinsic codegen/immediate cases. Windows-x64 controls pass
125 cases in each configuration.

Native `544f5216` retires the complete oracle-matching `emitInsCode` definition
and its `emitarm64.h` declaration before the C# completion commit, retaining the
pinned oracle as the residual commit's sole parent. These are managed
ARM64-target tests on a Windows x64 host, not ARM64 execution or generated-code
parity. Evidence: `artifacts/tst/{Debug,Release}/Arm64InstructionCode.*.trx`.

## 2026-10-02: Primitive rotation retirement

Closed the four retained 32/64-bit left/right rotation mappings to the existing
BCL and compiler/VN callers. Fresh Windows-x64 full-analysis runs pass 128 Debug
and 128 Release cases, with 320 independently derived equality checks per
configuration. Both native shift counts are masked, including zero and
oversized offsets; production callers are unchanged.

Native `36204bee` removes four complete definitions, four declarations and their
exclusive documentation: 64 lines. Exact reconstruction and the sole oracle
parent are verified. This replaces missing historical rotation evidence without
reopening scalar folding. It is helper/VN execution, not generated-code parity.
Evidence: `artifacts/primitive-rotations-review52/v2` and
`artifacts/residual-reconciliation/primitive-rotations-review52-*`.

## 2026-10-01: ARM64 Debug spill classification

Connected shared store classification to the existing committed ARM64 predicate,
preserving false results for negative and out-of-table instruction values.
The private emitter implementation and concurrent emitter WIP remain unchanged.

ARM64-target passes 50 Debug/50 Release cases, including 38 spill/lifecycle cases
and 12 classification cases. Normal scalar, GC, vector and narrow OSR spills now
record in Debug as well as Release. Windows passes 57 cases per configuration.
SIMD12 emission and enabled non-AMD64 location reporting remain separate
dependencies. No native retirement is credited for an already translated
predicate. Evidence: `artifacts/arm64-store-binding-e4389c55/v2`.

## 2026-10-01: Shared local-variable spills

Restored the complete fixed-register-target scope of local spilling, retaining
stack-home normalization, GC/liveness updates, spill-flag handling and location
updates before live-range reporting. The existing AMD64 algorithm is unchanged.

Windows passes 57 Debug/57 Release cases; ARM64-target passes 18 Debug/38 Release.
ARM64 Release exercises real scalar, GC, vector and narrow OSR stores. Debug
currently retains the store-classification dependency boundary; SIMD12 emission
and enabled non-AMD64 location reporting also remain explicit dependencies.
Retired one complete body and declaration, 87 lines. Evidence:
`artifacts/shared-local-spill-baf6db94/v2` and
`artifacts/residual-reconciliation/shared-local-spill-baf6db94-*`.

## 2026-10-01: Shared struct argument setup

Restored the native shared scope of struct-stack source setup and field-list
stores while preserving existing Windows callers. Source consumption precedes
destination moves; local addresses retain their GC attributes, field stores
retain width/order, and optional sizes preserve unsigned native semantics.

Windows passes 53 Debug/51 Release cases, including 14 new cases per configuration
and existing caller controls. ARM64-target passes 11 cases per configuration,
including normal scalar/address recording and the genuine SIMD12 termination
boundary. Retired two complete bodies and declarations, 112 lines including one
trailing blank. Native SIMD12 helpers remain unported. Evidence:
`artifacts/shared-struct-setup-64f99aa2/v2` and the final
`artifacts/residual-reconciliation/shared-struct-setup-64f99aa2-v2-*` records.

## 2026-10-01: Shared block-operation operand setup

Completed four block operand helpers, preserving source selection, unsigned
pointer-size materialization and consumption of both operands before fixed
register moves. Coverage checks emitted descriptors, GC classification, source
home lifetime, local offsets and unsigned size boundaries.

Windows and Linux-x64-target selections each pass 24 Debug and 23 Release cases.
Other targets remain source-reviewed; LoongArch64/RISC-V immediate materialization
is an explicit terminating dependency whose native bodies remain. Retired four
bodies and four declarations, 96 lines. Evidence:
`artifacts/block-operand-setup-985e50af/v2` and
`artifacts/residual-reconciliation/block-operand-setup-985e50af-*`.

## 2026-10-01: Retained HFA count reconciliation

Removed the retained native `GetHfaCount` body and declaration after confirming
the existing managed body is unchanged from its accepted source. Reused the
original eight HFA/HVA count cases in each ARM64-target Debug/Release run; no
implementation change or test rerun. ARM32 weighting remains source-reviewed.
The retirement removes 17 lines, including one trailing blank. Evidence:
`artifacts/residual-reconciliation/hfa-count-76490085-*` and the original
`artifacts/parallel-port/arm64-target-{debug-10,release-1}.trx`.

## 2026-10-01: Shared stack register operations

Completed `genSinglePop`, `genPushRegs` and `genPopRegs`, preserving output-mask
write order, target/feature branches, register order, GC classification and stack
accounting. The native fixed-output-argument NYI and its configured continuation
remain unchanged; this does not enable nonempty Windows-x64 register pushes.

Windows-x64 and ARM64-target selections each pass 12 Debug and 12 Release cases,
covering empty/aliased outputs, stack decrements and native NYI behavior.
Variable-output x86 paths remain source-reviewed under existing target blockers.
Retired three bodies and three declarations, 118 lines. Evidence:
`artifacts/shared-stack-operations-54c11278/v1` and
`artifacts/residual-reconciliation/shared-stack-operations-54c11278-*`.

## 2026-10-01: Register-mask string diagnostics

Completed DEBUG `regMaskToString` and `regMaskIntToString` using the existing
format definitions. Output retains unsigned width, uppercase/minimum padding,
lower-bank-only behavior and the native integer mask, including AMD64 APX bits.
Existing printing helpers remain unchanged.

Windows-x64 and ARM64-target Debug selections each pass 17 cases. Tests cover
high-bit patterns, integer filtering, ignored upper-bank bits, fixed-buffer
capacity and independent returned strings. No Release execution is claimed for
DEBUG-only APIs. Retired two bodies and their guards, 24 lines. Evidence:
`artifacts/mask-string-diagnostics-3d027fbc/v1` and
`artifacts/residual-reconciliation/mask-string-diagnostics-3d027fbc-*`.

## 2026-10-01: Argument-register mappings and coupled tables

Completed eight argument-index/register/mask mappings, including floating and
generic reverse dispatch. Existing signed fixed-buffer and integer reverse APIs
remain unchanged. The new callers preserve unsigned conversions, native
diagnostics, ARM double-pair masks and all target branches; missing small
register tables follow the pinned target data.

Windows passes 39 Debug/28 Release cases; Linux-x64 target passes 45/34;
Windows-ARM64 and Linux-ARM64 targets each pass 48/37 on Windows. Coverage includes
ordinary/fixed-buffer round trips, SIMD/mask routing, table boundaries and
diagnostic ordering. Other target branches remain source-reviewed, and their
existing integer reverse dependency is still explicitly unimplemented.

Retired eight bodies plus coupled data/declarations, 169 lines. Evidence:
`artifacts/argument-register-maps-afa82027/v2` and
`artifacts/residual-reconciliation/argument-register-maps-afa82027-*`.

## 2026-10-01: Local and argument type queries

Completed local parameter/register-argument predicates, actual-type normalization
and ARM varargs type mangling. The latter retains Windows-varargs/softFP selection,
float-to-int, double-to-long and SIMD-to-struct mappings. Existing callers,
ABI storage and option definitions remain unchanged.

Windows-x64 passes 62 Debug/62 Release cases. Windows-ARM64 and Linux-ARM64 target
selections each pass 60 Debug/60 Release on the Windows host, exercising the
OS-dependent branch without feature overrides. ARM32 softFP remains source-only.
Retired four bodies and their declarations, 42 lines. Evidence:
`artifacts/local-type-queries-04be8f2d/v1` and
`artifacts/residual-reconciliation/local-type-queries-04be8f2d-*`.

## 2026-10-01: Shared register-parameter and GC type queries

Completed `isRegParamType` and DEBUG `varTypeGCstring`, preserving the x86
type-order rule, unrestricted other-target result and exact diagnostic tokens.
Existing ABI classifiers, generated type tables and callers remain unchanged.

Windows passes 48 Debug/24 Release cases; ARM64-target passes 46 Debug/23 Release,
covering every declared target type and the default byte value. The x86 branch
is source-reviewed only under the existing protected parser limit. Retired two
bodies and the DEBUG guard block, 24 lines. Evidence:
`artifacts/type-queries-1162be15/v1` and
`artifacts/residual-reconciliation/type-queries-1162be15-*`.

## 2026-10-01: Little-endian scalar readers

Completed all nine byte-stream readers for unsigned, signed and floating values.
Unaligned integer reads reuse the repository's `BinaryPrimitives` pattern;
floating reads retain the integer-read/bit-reinterpretation sequence.

Windows passes 264 Debug/264 Release cases covering every offset modulo eight,
signed extrema, byte order, signed zero, subnormals, infinities and signaling/
quiet NaN payloads. Input bytes remain unchanged. These are Windows-host results,
not other-host or generated-code parity. Retired nine definitions, 38 lines.
Evidence: `artifacts/little-endian-9c10e424/v1` and
`artifacts/residual-reconciliation/little-endian-9c10e424-*`.

## 2026-10-01: Arbitrary-block funclet region queries

Completed `bbFuncletRegionOf` and `bbIsInSameFunclet`. Unlike the existing
entry-only query, these handle main-method and interior blocks, including the
physical distinction between a filter and its filter-handler.

Windows and ARM64-target selections each pass 24 Debug/22 Release cases using
the existing EH descriptor and linked-range implementation. Coverage includes
main/try/catch/filter/handler interiors, symmetric equality and the post-creation
assertion on each query. Retired two bodies and their declarations, 26 lines.
Evidence: `artifacts/funclet-region-90bf0d50/v1` and
`artifacts/residual-reconciliation/funclet-region-90bf0d50-*`.

## 2026-10-01: Bit counts and exclusive bit intervals

Completed the scalar/full-mask `genCountBits` wrappers and `BitsBetween`.
Counts reuse the accepted primitives; interval extraction retains unsigned
arithmetic, exclusive endpoints and all four ordered native assertions.

Windows and ARM64-target selections each pass 35 Debug/30 Release cases,
including both mask banks, 12 endpoint pairs across five value patterns and
invalid-input diagnostics. The focused selection excludes earlier accepted
bit-shape tests. Retired three whole definitions, 18 lines. Evidence:
`artifacts/bit-count-intervals-9539dfb8/v1` and
`artifacts/residual-reconciliation/bit-count-intervals-9539dfb8-*`.

## 2026-10-01: Shared scalar and full-mask bit shapes

Completed lowest-bit, at-most-one and exactly-one queries plus the unsigned
logarithm overloads. Narrow signed predicates retain C++ integer promotion;
full masks retain bank-width guards and lower-bank priority. Logarithms reuse
the existing scan primitive and its ordered assertion contract. Accepted LSRA
overloads and callers remain unchanged.

Windows and ARM64-target builds each pass 226 Debug/218 Release cases on Windows.
Coverage includes native-defined signed/unsigned inputs, both mask banks, every
32/64-bit logarithm position and invalid-input diagnostics. Zero-scan numeric
continuation and signed overflow are deliberately not claimed as parity.

Retired eight definitions and the platform forwarding guards, 59 lines. Evidence:
`artifacts/shared-bit-shapes-3c23a9a7/v2` and
`artifacts/residual-reconciliation/shared-bit-shapes-3c23a9a7-*`.

## 2026-10-01: Scalar narrowing and word extraction

Completed both forced narrowing helpers and unsigned low/high word extraction.
Narrowing materializes a width-specific local before its volatile read, matching
the native wrapper rather than relying on a widened intermediate. Existing
callers remain unchanged.

All 108 cases pass Debug and Release on Windows64: signed-zero, tie, subnormal,
finite and infinity bit patterns; NaN classification; native-defined UInt32
truncation; and unsigned word extraction across every bit. Out-of-range UInt32
behavior and NaN payload parity are not claimed.

Retired four whole inline definitions, 18 lines. Two pre-existing residual
`uint`/oracle `unsigned` spelling differences were accounted explicitly without
changing adjacent bodies. Evidence: `artifacts/scalar-narrowing-74c0a78b/v3` and
`artifacts/residual-reconciliation/scalar-narrowing-74c0a78b-*`.

## 2026-10-01: Existing frame-state mappings

Retired both frame-pointer phase-reset methods and the JIT32-only GC setter
without source changes or a compiler rerun. All three methods match the original
accepted EH source. Original ARM64 localloc cases also execute the used-frame
reset during setup; their saved result hashes remain unchanged.

This reuses existing evidence, not new direct required-reset/GC-setter or JIT32
execution. Retired three whole inline definitions, 14 lines. Evidence:
`artifacts/shared-eh-validation-225573e`,
`artifacts/arm64-compile-closure-06f419d7/v9` and
`artifacts/residual-reconciliation/frame-state-e6ce501f-*`.

## 2026-10-01: Shared GS-cookie temporary masks

Completed the shared selector's ARM32, LoongArch64 and RISC-V64 return branch
with the exact native temporary masks. Existing xarch and ARM64 selection,
Wasm exclusion and all caller contracts remain unchanged.

Windows passes seven Debug/seven Release cases, including the existing
secret-stub/tailcall controls. ARM64-target passes six/six cases on Windows,
including actual LSRA call-target exclusions. The newly enabled target branches
are source-reviewed, not claimed as positive target builds or runtime execution.

Retired one whole definition, its declaration and three mask aliases, 46 lines.
Evidence: `artifacts/gs-cookie-temp-masks-9b20e33d/v2` and
`artifacts/residual-reconciliation/gs-cookie-temp-masks-9b20e33d-*`.

## 2026-10-01: High-resolution performance counter

Completed the whole `PerfCounter` type under its native feature guard. It retains
frequency-to-milliseconds conversion, restart behavior and signed integer tick
subtraction before floating conversion. `Stopwatch` uses the same Windows clock
APIs; its pinned Unix implementation calls the native minipal clock directly.
No production caller or feature configuration changed.

All eight cases pass Debug and Release on Windows64. Clock-bracketed checks cover
capture, restart, independent instances and elapsed arithmetic at positive and
negative offsets without sleeps or machine-speed thresholds.

Retired two definitions and the complete native class, 22 lines. Evidence:
`artifacts/performance-counter-ce409700/v1` and
`artifacts/residual-reconciliation/performance-counter-ce409700-*`.

## 2026-10-01: Existing raw-hex diagnostic mapping

Retired the existing Debug-only `hexDump` mapping without source changes or a
compiler rerun. The implementation and fixture match the original accepted
async-publication snapshot byte-for-byte. Its passing direct test confirms
zero-length null input and uppercase byte order without separators or newlines.
The original Windows Debug run passed 32 cases.

Retired one whole definition and one declaration, 15 lines. Evidence:
`artifacts/async-transfer-helpers-1aaf363` and
`artifacts/residual-reconciliation/hex-dump-ddfb555e-*`.

## 2026-10-01: Shared write-barrier dispatch

Completed the shared caller rather than replacing non-AMD64 execution wholesale
with NYI. All targets select the native helper and invoke the genuine helper-call
dependency; AMD64 recording remains unchanged. The existing terminating helper
declaration now also covers Wasm without changing jump scope or helper bodies.
Both native optimized-policy overloads map the existing unused-input predicate.

Windows passes 18 Debug/18 Release cases, including direct helper-call recording
and existing GC-store controls. ARM64-target passes 15/15 selection and typed
dependency-boundary cases on Windows; these do not claim successful ARM64 calls.
Full analysis remains enabled with default compiler processors and telemetry-only
memory observation.

Retired two whole bodies and three declarations, 19 lines. Evidence:
`artifacts/shared-write-barrier-165a2ddd/v1` and
`artifacts/residual-reconciliation/shared-write-barrier-165a2ddd-*`.

## 2026-10-01: EH frame requirement mapping

Retired the existing frame-pointer/EH interruptibility setter without changing
its implementation or rerunning the accepted family. Its original `225573e`
archive entry and extracted source were authenticated, and the current method
is unchanged. Original EH evidence passes 18 Debug/16 Release cases; this is not
a new direct-setter execution claim.

Retired one whole definition and one declaration, 26 lines. Evidence:
`artifacts/shared-eh-validation-225573e` and
`artifacts/residual-reconciliation/eh-frame-requirement-1347036d-*`.

## 2026-10-01: Phase-variable initialization and override

Completed the initial-value constructor and forced assignment for `PhasedVar<T>`.
Forced assignment preserves the current phase, including after a read; existing
accessors, resets and the unconstrained generic contract remain unchanged.

All 11 cases pass Debug and Release on the actual Windows64 host, covering
integer, Boolean and nullable-reference values, default initialization, phase
transitions and independent copies without EE assertions.

Retired two whole inline definitions, 15 lines. Evidence:
`artifacts/phase-variable-1342eb3e/v1` and
`artifacts/residual-reconciliation/phase-variable-1342eb3e-*`.

## 2026-10-01: Floating register diagnostics

Completed target-aware floating register names through `GetFloatName`, reusing
unchanged generated base names. Cached x/y/z tables preserve native width
selection for every register entry. ARM32 retains its exact even-register
literal switch, while ARM64 and LoongArch retain their base names.

Windows-x64 Debug passes 26 new cases; ARM64-target Debug passes 23 on Windows.
Release builds pass 26/25 existing classification controls. Other target
branches remain source-reviewed, and no ARM64 machine-code execution is claimed.
Existing register-name APIs, callers and generator files remain unchanged.

Retired the whole 120-line native definition and its declaration, 121 lines.
Evidence: `artifacts/floating-register-names-dac55795/v2` and
`artifacts/residual-reconciliation/floating-register-names-dac55795-*`.

## 2026-10-01: Scalar numeric queries

Completed generic power-of-two and sign queries plus the unsigned arbitrary-base
digit counter. Native ordered comparisons retain zero for NaNs and signed zero;
the digit counter preserves its Debug guard, default base and assertion text.
Existing signed and floating digit helpers remain unchanged.

All 62 Debug/40 Release cases pass full analysis on the actual Windows64 host,
including 23 existing profile diagnostic controls per configuration. New coverage
checks ten integral types, floating encodings and every valid radix at its power
boundaries.

Retired three whole definitions and one declaration, 33 lines. Evidence:
`artifacts/scalar-queries-751995f7/v1` and
`artifacts/residual-reconciliation/scalar-queries-751995f7-*`.

## 2026-10-01: Fixed bit-vector type

Completed the whole fixed bit-vector type with zero-initialized managed chunk
storage, set/clear/test, union/intersection, and ascending non-destructive and
destructive scans. Native 32-bit chunks, unsigned sentinel, mutation order and
assertion text remain intact, including the inclusive size boundary.

All 108 Debug/91 Release cases pass full analysis on the actual Windows64 host.
Coverage includes ten sizes around chunk boundaries, empty/full/sparse vectors,
self-aliasing set algebra and safe assertion continuations. No native
out-of-storage access or allocator-byte parity is claimed.

Retired eleven out-of-line definitions and the complete native type containing
the twelfth inline method, storage and declarations: 220 lines. Evidence:
`artifacts/fixed-bit-vector-9a05c00b/v3` and
`artifacts/residual-reconciliation/fixed-bit-vector-9a05c00b-*`.

## 2026-10-01: Floating conversion helpers

Completed unsigned64-to-floating conversions, double-to-unsigned64 truncation
and single-to-double widening. Widening preserves the native RISC-V-host
NaN payload expansion branch; existing narrowing and caller contracts are
unchanged.

All 36 cases pass Debug and Release on the actual Windows64 host, covering
precision boundaries, native-defined truncation limits and signed raw widening
encodings. RISC-V host execution and native-undefined integer conversion results
are not claimed.

Retired four whole definitions and four declarations, 32 lines. Evidence:
`artifacts/fp-conversions-6bc9ec00/v1` and
`artifacts/residual-reconciliation/fp-conversions-6bc9ec00-*`.

## 2026-10-01: Floating rounding and special values

Completed both ties-to-even rounding algorithms, positive-infinity constructors
and all-one-bit predicates. Rounding retains width-specific integer boundaries
and signed offset arithmetic, including final sign restoration for negative
zero. Existing BCL rounding callers and tree predicates remain unchanged.

All 48 cases pass Debug and Release on the actual Windows64 host. They cover
92 signed rounding inputs, including adjacent ties, integer boundaries and NaN
encodings, both infinity constructors, and every single-bit-clear rejection for
the all-one predicates. Other hosts and nondefault rounding modes are not claimed.

Retired six whole definitions and six declarations, 104 lines. Evidence:
`artifacts/fp-rounding-ae496f62/v2` and
`artifacts/residual-reconciliation/fp-rounding-ae496f62-*`.

## 2026-10-01: IEEE floating-point selection

Completed all sixteen single/double maximum/minimum variants, including
magnitude and Number forms. Native comparison order, signed-zero and equal-
magnitude ties, numeric preference and original selected NaN payloads remain
intact; no BCL Min/Max substitution or caller changes were made.

All 96 cases pass Debug and Release on the actual Windows64 host. Explicit
operand expectations cover 768 selections per configuration, including finite
ordering, infinities, subnormals, signed zeros and signed quiet/signaling NaNs.
This is helper execution evidence, not target-runtime parity.

Retired sixteen whole definitions and sixteen declarations, 264 lines. Evidence:
`artifacts/fp-selection-83936c2e/v1` and
`artifacts/residual-reconciliation/fp-selection-83936c2e-*`.

## 2026-10-01: Floating-point classification and normalization

Completed normal/finite/sign/NaN classification, double signed-zero predicates
and host-specific NaN normalization. The implementation retains native raw-bit
masks and signed reinterpretation rather than numeric comparison shortcuts.
Only x86-host normalization sets the double quiet bit; other hosts preserve
the original encoding.

All 44 cases pass Debug and Release on the actual Windows64 host, covering
22 encoding categories per precision and unchanged input payloads. The x86-host
quieting branch remains source-reviewed, not executed. Exponent helpers remain
native pending the recorded double `ilogb` source finding.

Retired eleven whole definitions and eleven declarations, 79 lines. Evidence:
`artifacts/fp-classification-01e75fde/v1` and
`artifacts/residual-reconciliation/fp-classification-01e75fde-*`.

## 2026-10-01: Floating-point bit copies

Completed all four single/double bit-copy helpers using same-width bit
reinterpretation. Both directions retain signed zero, subnormal and finite
boundaries, infinities and signed quiet/signaling NaN payloads; no floating
arithmetic or canonicalization is introduced.

All 32 cases pass Debug and Release on the actual Windows64 host. Each direction
is checked independently against raw bitcasts, including every one-hot position.
No x86-host or target-runtime parity is claimed.

Retired four whole definitions and four declarations, 28 lines. Evidence:
`artifacts/bit-conversions-15eec733/v1` and
`artifacts/residual-reconciliation/bit-conversions-15eec733-*`.

## 2026-10-01: Scalar bit positions and zero counts

Completed eight primitive forward-scan, leading/trailing-zero-count and integer
logarithm overloads. Scans preserve native nonzero assertions and host-specific
fallbacks. The total functions preserve zero results explicitly: counts return
the input width, while logarithms return zero. Existing mask overloads and BCL
callers remain unchanged.

Actual Windows64-host checks pass 155 Debug/151 Release cases, including
120/118 new cases and 35/33 existing mask-bit controls for overload compatibility.
All one-hot positions, mixed boundaries and distinct zero contracts are covered.
Other host branches remain source-reviewed, not executed.

Retired eight whole definitions and six declarations, 122 lines. Evidence:
`artifacts/bit-positions-41196dd4/v1` and
`artifacts/residual-reconciliation/bit-positions-41196dd4-*`.

## 2026-10-01: Reverse bit scans

Completed both unsigned reverse-scan overloads, preserving nonzero assertions,
highest-bit priority, the Windows32-host split and the non-Windows leading-zero
conversion. The existing BCL operations provide native compiler-intrinsic
equivalents without shadowing the BCL type or changing accepted callers.

Actual Windows64-host checks pass 118 Debug/116 Release cases, including all
96 one-hot positions. Zero checks cover assertion count and text only, not the
native undefined continuation result. Other host branches are source-reviewed;
changing a target RID alone would not execute them.

Retired two whole definitions and two declarations, 48 lines. Evidence:
`artifacts/reverse-scan-70af055c/v1` and
`artifacts/residual-reconciliation/reverse-scan-70af055c-*`.

## 2026-10-01: Population-count primitive mapping

Retired the two retained native population-count primitives and their declarations
against the existing BCL mappings, without changing compiler source or rerunning
accepted tests. Original source seals and receipt hashes match; the prior Windows
79 Debug/74 Release runs include 17 population-count cases per configuration.
Unsigned input widths and bounded result conversions remain unchanged.

The retirement removes 44 lines. Evidence:
`artifacts/primitive-popcount-e467bb37/mapping.json` and
`artifacts/residual-reconciliation/primitive-popcount-e467bb37-*`.
The separate rotation debt was subsequently closed with fresh focused evidence;
see the 2026-10-02 primitive rotation retirement entry.

## 2026-10-01: Fixed-width bit reversal

Completed the 32-bit and 64-bit bit-reversal algorithms, retaining the native
sequence of adjacent-bit, pair, nibble, byte and larger-group swaps. The utility
overloads live in the existing `Globals` layout rather than introducing a type
that would shadow accepted BCL bit operations.

All 18 cases pass Debug and Release for Windows-x64, Linux-x64 and Linux-ARM64
configurations hosted on Windows. Both numeric widths cover fixed patterns,
involution and every one-hot input position. This is utility execution evidence,
not target machine-code parity.

Retired two whole definitions and two associated declarations, 53 lines.
Evidence: `artifacts/bit-reversal-35e79b71/v1` and
`artifacts/residual-reconciliation/bit-reversal-35e79b71-*`.

## 2026-10-01: Numeric register-mask operations

Completed boolean and unsigned-integer conversion, the integer-mask factory,
logical right shift and shift assignment. Boolean conversion reads both banks;
integer conversion truncates only the lower bank. Right shift deliberately
discards the upper bank even when shifting by zero. The native assignment's
returned owner reference maps to a static-ref helper without changing the
readonly representation.

All 43 cases pass Debug and Release for Windows-x64, Linux-x64 and Linux-ARM64
configurations hosted on Windows, including returned-alias identity and updates.
Only native-defined shift counts are covered. Existing 32-bit compilation
boundaries remain, with no claimed fixture execution.

Retired six whole definitions, 33 lines. This includes the already implemented
complement, whose original mask-operation evidence is reused without rewriting
or replaying it. Evidence: `artifacts/mask-numeric-b0c76ca2/v2` and
`artifacts/residual-reconciliation/mask-numeric-b0c76ca2-*`.

## 2026-10-01: Register classification

Completed floating-register type classification and target-specific byte-register
classification. The latter deliberately remains unconditional on targets without
restricted byte registers; it is not a general register-validity check. Native
target guards and type-table classification remain unchanged.

Full-analysis checks pass 26 Debug/26 Release for Windows-x64 and Linux-x64,
and 25/25 for Linux-ARM64, including scalable-vector classification. An unmasked
x86 attempt stops before tests at four existing parser diagnostics in two
protected files. Their live WIP was excluded; restricted-byte fixture branches
are not claimed executed.

Retired three whole target bodies covering two helpers, 13 lines.
Evidence: `artifacts/register-classification-3a9e8074/v2` and
`artifacts/residual-reconciliation/register-classification-3a9e8074-*`.

## 2026-10-01: Register-mask construction

Completed scalar and typed mask construction using the accepted scalar lookup
and static-ref mask operations. The existing scalar wrapper remains in
`LsraGlobals`; three missing wrappers now share the target implementation.
ARM typed operations retain first-half pairing rather than normalizing a double
register, while other targets ignore the type. Bank-local scalar bits remain
distinct from full-register-space masks.

All 17 Debug/13 Release cases pass for Windows-x64, Linux-x64 and Linux-ARM64
configurations hosted on Windows. ARM32's existing compilation boundaries remain;
its guarded fixture branches are not execution evidence. No LSRA caller, ABI,
register representation or protected source changed.

Retired four whole definitions and two associated declarations, 39 lines.
Evidence: `artifacts/mask-construction-814faec7/v2` and
`artifacts/residual-reconciliation/mask-construction-814faec7-*`.

## 2026-10-01: Typed register masks and iteration

Completed register capacity, type normalization, bank-local mask generation
and next-register iteration. The shared implementation reuses the existing ARM
normalization algorithm and three static-ref mask-operation callers, preserving
lookup/assertion ordering before owner updates. It retains native double pairing,
unchecked register arithmetic, xarch-only mask bounds and nonARM type ignoring.
The directly used ARM double-register predicate's malformed closing parenthesis
is corrected without changing its algorithm.

Full-analysis checks pass 593 Debug/593 Release for Windows-x64 and Linux-x64,
and 633/633 for Linux-ARM64, hosted on Windows. ARM32 compilation still stops
before fixtures at 12 existing parser/platform diagnostics, including two
protected source files. No protected WIP or parser-only mask was used as
accepted code; its 464 fixture cases remain unexecuted projections.

Retired five whole target definitions covering four helpers, 91 lines.
Evidence: `artifacts/typed-registers-e46ebf41/v3` and
`artifacts/residual-reconciliation/typed-registers-e46ebf41-*`.

## 2026-10-01: Argument-register validity

Completed the integer and floating argument-register predicates and eight
missing target endpoint constants. Integer classification preserves the native
bank-local mask lookup, including overlapping bank positions; it does not
substitute a general register-class check. Floating classification excludes
`REG_NA` before testing the inclusive argument-register range.

All 16 focused cases pass Debug and Release on Windows-x64, Linux-x64 and
Linux-ARM64 configurations hosted on Windows. Other-target constants match
pinned headers and generated enum bindings without claiming their execution.
ABI, feature flags and existing argument-mask contracts remain unchanged.

Retired two whole definitions and eight constant declarations, 23 lines.
Evidence: `artifacts/argument-validity-bffd7a6c` and
`artifacts/residual-reconciliation/argument-validity-bffd7a6c-*`.

## 2026-10-01: Fixed return-buffer argument masks

Completed the fixed return-buffer mask helper and connected the existing full
integer-argument mask algorithm to it. Ordinary argument registers, ARM64's
fixed return buffer, AMD64 Swift's return buffer and Swift self/error registers
retain their native masks and target conditions. No ABI, argument-number
representation or feature defaults changed.

Full-analysis checks pass 12 Debug/9 Release on stock Windows, 29/26 for
Linux-x64 and 26/26 for Linux-ARM64, all hosted on Windows. Unix x64 and ARM64
use their existing Swift configuration and include all 17 Swift ABI controls;
stock Windows does not enable Swift. Tests cover all nine calling conventions
and native Debug assertion ordering/continuation. These are policy and
descriptor checks, not target runtime parity.

Retired the two complete native definitions, 35 lines. The separate signed
argument-number mapping remains outside this batch. Evidence:
`artifacts/fixed-retbuf-c46a68bb/v3` and
`artifacts/residual-reconciliation/fixed-retbuf-c46a68bb-*`.

## 2026-10-01: ARM64 stack and branch execution

Completed ARM64 localloc, frame-pointer establishment, frame probing and
constant-instruction emission with the native probe boundaries, SP ordering
and register/reference updates. Public dispatch preserves existing xarch
algorithms. Whole ARM64 branch recording and short-form selection use the
existing generated jump mappings, now visible to ARM64; the generator and
generated output changed together. The throwing unwind property is now an
explicit method with the same terminating contract, not simulated metadata.

Full-analysis ARM64-target checks on Windows pass **422 Debug/394 Release**.
These execute the SVE opcode, output and driver fixtures, all eight corrected
scalable-recording cases, 109/98 branch cases and 36 stack cases. They establish
managed descriptor/encoding-byte/algorithm behavior, not ARM machine-code or
runtime parity. Earlier compilation and execution failures remain preserved.

Windows controls pass 153 of 154 Debug cases and all 140 Release cases. The
single Debug assertion-message mismatch is reproduced exactly from the
untouched committed baseline. Release's process exits successfully; a later
runner check incorrectly demanded a Debug-only fixture. Exact independent
five-fixture accounting verifies the 140 executed cases without suppressing
tests or claiming a clean Debug baseline.

Retired ten whole definitions and two declarations, **672 native lines**.
Unwind metadata, shared ARMARCH probes, private-only helper copies and ordinary
opcode/call/long-jump output remain untranslated dependencies. Evidence:
`artifacts/arm64-compile-closure-06f419d7/v9` and
`artifacts/residual-reconciliation/arm64-stack-branch-490e46d4-*`.

## 2026-10-01: Register-mask population and first-set-bit operations

Completed the four shared scalar/full-mask population-count and forward-scan
overloads. Unsigned reinterpretation preserves the target's bank width and
signed high bits. Counts include both raw banks; scans prioritize the lower
bank and offset upper-bank positions by 64. Existing callers and the readonly
mask representation are unchanged.

Full-analysis Windows checks pass 79 Debug/74 Release cases, including
35 Debug/33 Release new cases covering bank priority, all bits, high ordinals
and the native nonzero assertion. Native zero scanning has no defined numerical
result after assertion; the fixture checks only the assertion contract.
Other-target guards are preserved without an execution claim.

Retired four whole native definitions, 31 lines, with no separate declaration
or underlying utility retirement. Evidence:
`artifacts/register-mask-bits-06f419d7` and
`artifacts/residual-reconciliation/register-mask-bits-06f419d7-*`.

## 2026-10-01: SVE opcode generation and ARM64 output source closure

Completed SVE opcode generation, lookup and output together with the whole
ARM64 instruction-output driver and its coupled encoding/GC helpers. All 606
pinned instruction rows match the generated 13 compact opcode arrays and
1,123 entries; five existing xarch outputs remain unchanged. Generator and
generated output are updated together.

Windows output controls pass 119 Debug/119 Release cases. The final ARM64
candidate retains exactly the fresh baseline's 40 Debug/32 Release diagnostics.
Its fixtures have not compiled or executed, and early baseline failures can
mask later errors. The three new fixtures project 268 Debug/251 Release cases,
not execution coverage. Eight existing recording cases now perform their full
checks in Debug instead of expecting the removed SVE sanity NYI; fixed-frame
recorders normalize incoming `EA_8BYTE` to `EA_SCALABLE` before sanity checking.

Retired 76 whole native definitions and 62 standalone declarations: 6,747 lines.
Native `emitOutputInstr` maps to `emitOutputInstrArm64`, and six architecture
classification/GC helpers use `Arm64`-suffixed names. Ordinary opcode, call and
long-jump output, shared descriptor sizing/layout and other-target output remain.
The shared native output-entry declaration was already absent before this batch;
that residual exception receives no deletion credit. No ARM64 generated-byte
execution or runtime parity is claimed.

Evidence: `artifacts/sve-output-acceptance-f5af4ea9` (generation verification,
v2 Windows controls and final v3 source/diagnostic comparison) and
`artifacts/residual-reconciliation/sve-output-f5af4ea9-*`.

## 2026-10-01: ARM32 node-reference construction source closure

Translated the whole ARM32 node dispatcher, long-shift carry handling and six
normal support builders: indirect access, calls, stack arguments, block stores,
casts and local heap allocation. Register-reference ordering and ARM masks
follow the pinned algorithms. Optional Swift register mapping remains an
explicit terminating dependency.

Full-analysis Windows-x64 controls passed 79 Debug/78 Release cases. ARM32
execution remains blocked: untouched and candidate Linux-ARM builds report the
same 14 parser diagnostics. Reusing the existing eight-file diagnostic-only
parser overlay, Windows-ARM and Linux-ARM Debug/Release pairs each retain the
same four declaration errors, with zero warnings. Missing declarations are
`Arm32Classifier`, `MAX_MULTIREG_COUNT`, `MAX_RET_REG_COUNT` and `Target.ArgOrder`.
These failures precede later body checking; the fixture's 168 Debug/166 Release
projections are not executed tests or proof of successful ARM32 compilation.

Retired eight whole native bodies and the ARM-exclusive carry declaration:
1,233 lines. Shared ARMARCH bodies also map to the previously accepted ARM64
implementations; shared declarations and independent helpers remain. Evidence:
`artifacts/arm32-builders-acceptance-88a8563f` and
`artifacts/residual-reconciliation/arm32-builders-88a8563f-*`.

## 2026-10-01: Whole compiler shutdown and optional-statistics boundaries

Completed the whole shutdown caller, preserving cleanup, zero-method return,
timing-file closure, report order, unsigned statistics arithmetic and native
operator sorting. Unported collectors, native logical sizes and optional
reports terminate explicitly rather than supplying invented data.

Full-analysis Windows checks passed 14 Debug/7 Release stock cases and
36 Debug/37 Release cases with eight combined optional-statistics flags enabled
only in the isolated validation configuration. The latter covers early exit,
the first unsupported report, all 26 direct dependency boundaries and native
sorting. A Release measurement prerequisite now enables the existing structure
type table alongside its accessor; the generator and regenerated output agree.
Production feature selection is unchanged.

Retired the whole 370-line native caller; its declaration was already absent.
All unported helper bodies remain. Standalone Release pointer-table measurement
still has the pinned counter-guard limitation B501; successful combined flags
do not establish that configuration or complete statistics-output parity.
Evidence: `artifacts/shutdown-acceptance-d01ea182/v4` and
`artifacts/residual-reconciliation/shutdown-d01ea182-*`.

Retirement follow-up: the complete managed `CompTimeSummaryInfo.AddInfo`
mapping and existing caller were already accepted. Removed its empty residual
native shell from consolidated HEAD `a0c67aff`; the native declaration and
residual callers were already absent. The focused full-analysis Windows-x64
`CompilerTimingTests` selection passes 18/18 Debug and 12/12 Release with zero
skips. This adds no generated-code or runtime parity claim.

## 2026-10-01: Funclet metadata identity and CFI record layout

Completed funclet kind/index metadata, Wasm metadata declarations and the
CFI record declaration/constructor. Funclet indices use actual owned-array
identity and GC-tracked byrefs, including across compacting collection.
The internal Wasm exception-index property preserves the native `UINT_MAX`
default for zero-initialized structs and arrays.

Full-analysis checks passed 16 Debug/15 Release Windows cases and 25/24 cases
with a validation-only CFI-enabled Linux-x64 target configuration on Windows.
The latter checks the eight-byte CFI record's field offsets and independently
verifies four-byte embedded alignment. A missing whole native CFI policy query
was implemented using existing OS and runtime-ABI APIs; production feature
selection remains unchanged.

Retired five methods, three types, two declarations and seven fields: 72 native
lines, counting the CFI constructor within its type only. Native unwind
algorithms and embedded hot/cold state remain unported. No Wasm execution,
native unwind publication or whole-funclet-layout parity is claimed. Evidence:
`artifacts/func-info-acceptance-79bed296/v4` and
`artifacts/residual-reconciliation/func-info-79bed296-*`.

## 2026-10-01: Immutable register-mask operation family

Completed the twelve remaining native register-mask operations, including typed
ARM pairs and register-bank queries. Mutators use the established explicit
`ref regMaskTP` owner-writeback pattern; readonly fields, layout, existing public
APIs and callers remain unchanged. GPR availability keeps native intersection
semantics and assertion-before-write ordering.

Full-analysis Windows checks passed 35 Debug and 32 Release cases, including
25/22 new cases and ten existing bank controls each. The twelve definitions and
declarations were retired together: 119 body lines, twelve declaration lines
and one exclusive trailing blank. ARM branches were source-reviewed, not
executed; the existing public `IsSet` single-bank limitation is unchanged.
Evidence: `artifacts/regmask-operations-acceptance-ba11eab2` and
`artifacts/residual-reconciliation/regmask-operations-ba11eab2-*`.

## 2026-10-01: Try-aware loop-aware traversal

Completed the whole reverse-postorder visitor that keeps catch-protected regions
and loop bodies together. It marks blocks before invoking the callback, reads
catch state after the callback, visits catch regions before loops, and visits
each reachable block once. Methods without catch regions delegate to the existing
loop-aware traversal before allocating visited state.

Isolated full-analysis Windows checks passed 63 Debug and 58 Release cases,
including all 13 new traversal cases in each configuration, existing try-region
controls and five loop-aware/acyclic controls. The native template definition
and declaration were retired together: 80 lines. This is managed traversal
coverage, not generated-code parity. Evidence:
`artifacts/try-traversal-acceptance-cf92ffc9/v2` and
`artifacts/residual-reconciliation/try-traversal-cf92ffc9-*`.

## 2026-10-01: Accepted register-bank selector reconciled

Retired the already-accepted `regMaskTP::GetRegSetForType` definition and its
declaration: 15 body lines, eight attached documentation lines and one
declaration, 24 total. The committed managed method matches the original
allocation-activation patch; its ten bank-selection cases passed in each
historical Debug/Release run. No compiler or test replay was needed.

The remaining AMD64 argument-mask tables, x86 tables/classifier and other
register-mask operations are retained. Register arrays, aggregate masks and
partial immutable operations do not establish those complete mappings.
Evidence is under
`artifacts/residual-reconciliation/register-bank-selection-bc6a48dd-*`.

## 2026-10-01: Descriptor format headers and xarch classification

Completed the native format-header target branches while preserving the existing
xarch register/memory classification and scheduling-table behavior. Stored
formats retain their target widths and assertion-before-write ordering;
LoongArch retains its native zero/no-op behavior, and RISC-V retains the existing
configurable NYI policy.

The independent source review found no concrete defect. Full-analysis Windows
Debug and Release each pass 52 cases, including 33 new header/classification
cases and 19 existing schema/primitive controls. The checks distinguish
operand kinds rather than inferring memory/constant behavior from format names.
No fixture correction or replay was required after review.

Retired 31 whole inline definitions, 151 lines: six target-specific header
alternatives and 25 xarch classification queries. The already-retired scheduling
helper receives no additional credit. ARM/Wasm generated format metadata remains
unported; raw-width source controls are not valid-format execution coverage.
No table or descriptor-layout retirement is claimed.

Evidence is under `artifacts/descriptor-format-acceptance-8aab5675` and
`artifacts/residual-reconciliation/descriptor-format-8aab5675-*`.

## 2026-10-01: Descriptor bound-bit aliases and target payloads

The bound flag now uses the native shared custom-payload bit instead of
independent Boolean storage. Writes through the bound, EVEX mask and custom
payload views agree without disturbing adjacent bits. Completed target payload
accessors preserve ARM instruction-size/flag semantics and LoongArch/RISC-V
register, option and code-size widths. LoongArch's register and local-address
views share the second union word; RISC-V uses the first.

Full-analysis Windows Debug passes 221 cases and Release passes 225, including
18 new payload cases in each configuration. Coverage includes bidirectional
aliases, unrelated-bit preservation, Debug assertion ordering and Release
truncation. Runs retain memory telemetry and the full correctness checks without
either free-memory threshold.

Retired 28 whole inline definitions (141 body lines) and four complete
prerequisite enums (43 lines), 184 native lines total. Wasm code-size computation
remains a typed terminating dependency with its native body/declaration retained.
Descriptor container/layout fields, encoding helpers and the independent format
family are not retired by this batch. Other-target source controls do not imply
compilation, execution or physical-layout parity.

Evidence is under `artifacts/descriptor-payload-acceptance-8aab5675` and
`artifacts/residual-reconciliation/descriptor-payload-8aab5675-*`.

## 2026-10-01: Local per-element SIMD mask contracts

Completed the local mask element-width mapper's native `unreached()` failure
contract. Existing compatibility queries still short-circuit on an unset marker,
and repeated annotations retain the maximum element width without changing
unrelated flags. Callers and the logical one-bit marker/two-bit width encoding
are unchanged.

Full-analysis Windows Debug and Release each pass 51 focused cases: 27 new
local-mask cases, 20 existing hardware-intrinsic recognition controls and four
value-number/phi propagation controls. Both runs retain memory telemetry,
default compiler processors and compiler-only GCConserveMemory5 without
free-memory admission or abort thresholds.

Retired three whole inline definitions (42 body lines), two mapped flag
declarations and nine exclusive surrounding lines from `compiler.h`, 53 total.
Evidence is under `artifacts/local-mask-acceptance-8aab5675` and
`artifacts/residual-reconciliation/local-mask-8aab5675-*`. No other-target
execution, physical-layout equivalence or generated-code parity is claimed.

## 2026-10-01: Shared LSRA definition and kill builders

Completed the shared definition, call-result, kill, cast-use, register-argument
and comparison builders across their native target branches. Native register
masks, argument preferences, fixed-register ordering and contained-operand flow
are preserved. The new high-register fixture initializes the real EVEX register
banks before constructing the allocator rather than modifying only its available
register mask.

Corrected Windows Debug/Release selections pass 121/108 cases. Linux Debug
passes 115/121 and Release passes 103/108; all 33/23 new cases pass. The six
Debug and five Release control failures match an unmodified baseline exactly,
including full messages, and remain recorded as B502 rather than suppressed.
ARM64 retains the exact 39-diagnostic compilation baseline; no ARM64 fixture
execution or generated-code parity is claimed.

An explicit resource-policy change removes free-memory admission and running
abort thresholds for new versioned runners, while retaining memory telemetry,
hidden serial execution, full analysis, source/test checks, default compiler
processors and compiler-only GCConserveMemory5. Historical runners, failures and
admission timeouts are unchanged.

Retired 13 whole definitions and 12 declarations: 392 body lines, 52 exclusive
body-envelope lines, 12 declaration lines and three header-guard lines, 459
total. Evidence is under `artifacts/shared-lsra-builders-acceptance-a828ad0f/v2`
and `artifacts/residual-reconciliation/shared-lsra-builders-a828ad0f-*`.

## 2026-10-01: Committed compiler-option mappings reconciled

Reconciled the retained native Options members against already-committed managed
code, without changing compiler behavior or using protected working-tree repairs
as acceptance evidence. The complete archived Options source matches the current
committed counterpart; its ten mapped constants also match.

Retired 21 inline definitions (139 body lines), 72 field declarations and ten
constants, plus 161 exclusive comment, guard and blank lines: 382 lines total.
Inline signatures are included, with no separate prototype credit. Existing
control-flow-guard evidence is reused; no new tests or broader execution coverage
are claimed.

The type container and six exception declarations remain: the nonprofiling
callback constant whose managed initializer is still protected WIP, four
unsigned numeric fields mapped to signed storage without full-domain evidence,
and the fast-tailcall field with a wider managed Wasm guard. The callback
constant's existing native initializer is retained. Exact reconstruction,
protected-WIP hashes and the consolidated native commit's oracle parent are
verified. Evidence is under
`artifacts/residual-reconciliation/compiler-options-a828ad0f-*`.

## 2026-10-01: ARM64 SVE register-pair recording

Translated the whole two-register and two-register/immediate SVE recorders and
their 90/270-degree rotation decoder. Aliases, register-bank and SP encoding,
descriptor allocation, immediate reduction/packing, delegation and diagnostic
ordering retain the native algorithms. The decoder deliberately accepts encoded
values 2 and 3 through the native two-bit assertion and returns zero.

Source-only ARM64 acceptance retains the exact 39-diagnostic compilation
baseline. The fixture has not compiled or executed; its 178 Debug/168 Release
cases are source projections. Windows collateral controls pass 58 Debug and
58 Release cases, not SVE execution. The only acceptance correction removes a
blank line left by stub deletion; the initial failed snapshot is preserved.

Retired three whole definitions and three declarations: 888 body and 14
declaration lines, 902 total. Prior recorder fixtures remain unchanged; the old
six-argument move dependency and its native body/declaration remain separate.
Evidence is under `artifacts/sve-register-pair-acceptance-605023a8/v2`.
No backend activation, B498/D009 change or generated-code parity is claimed.

## 2026-10-01: ARM64 SVE multioperand recording

Translated the whole three-register/two-immediate, four-register and
four-register/immediate SVE recorders. Aliases and operand swaps, memory/address
formats, immediate packing, prefix ordering/elision and native option forwarding
are retained. Existing helpers are reused without changes.

Source-only ARM64 acceptance retains the exact 39-diagnostic compilation
baseline. The new fixture has not compiled or executed; its 151 Debug/143 Release
cases remain source projections. Windows collateral controls pass 58 Debug and
58 Release cases, not SVE execution. Two analyzer corrections remove a dead
initializer and simplify a predicate while preserving its native assertion text.
All gates remain enabled, and the failed initial snapshot is preserved.

Retired three whole definitions and three declarations: 1,586 body and 27
declaration lines, 1,613 total. Native `st1b` recorder/sanity assertion differences
are preserved rather than relaxed. The unconditional PSEL failure and genuine
remaining dependency boundaries also remain. Evidence is under
`artifacts/sve-multioperand-acceptance-d691baa3/v2`; no backend activation or
generated-code parity is claimed.

## 2026-10-01: ARM64 SVE three-register recording

Translated the whole three-register and three-register/immediate SVE recorders,
including instruction/format dispatch, aliases, register banks, widths/options,
scaled immediates, prefix dependencies and display/sanity-before-append order.
Coupled predicates, immediate validation and rotation decoding retain the native
algorithms and signed/unsigned conversions.

This is source-only ARM64 acceptance. The corrected snapshot retains the exact
39-diagnostic compilation baseline; the new fixture has neither compiled nor
executed, so its 199 Debug/197 Release cases remain source-count projections. Windows
collateral controls pass 58 Debug and 58 Release cases, not SVE execution.
The initial missing-helper failure is preserved alongside the corrected
`isFloatReg` forwarding dependency and unchanged full-analysis settings.

Retired 11 definitions and three declarations, 3,029 native lines. The new
seven/eight-argument move overload contains the whole native algorithm, but
unrelated callers still bind a six-argument terminating stub. Its native
55-line body and eight-line declaration remain with no retirement credit.
Evidence is under `artifacts/sve-three-register-acceptance-cabc2554/v2`.
No backend activation, B498/D009 change or generated-code parity is claimed.

## 2026-10-01: Try-region construction and traversal

Completed `FlowGraphTryRegion` and `FlowGraphTryRegions`, including construction,
membership, entry edges, runtime-resumption flags, reverse-postorder traversal
and Debug dumps. The port preserves EH table-slot identity, stable-ID gaps,
mutual-protection ancestry, handler exclusion, unreachable-block handling,
predecessor order and visitor abortion.

Windows and Linux-target selections each pass 103 Debug and 98 Release cases:
402 executions, including 170 new cases and 232 DFS controls. ARM64 retains
the exact 39-diagnostic compilation baseline; no target tests ran. Full-analysis
gates remained enabled. Integration corrected property invocation, discarded
visitor results, fixture lambda/discard binding and collection syntax without
changing the algorithm or case selection.

Retired 462 native lines across the two complete types, their forward
declaration and implementations: 27 definitions (nine out-of-line, one template
and 17 inline) and ten function declarations. Evidence and preserved failed
snapshots are under `artifacts/try-regions-acceptance-a92829d5`, with final
acceptance in `v5`. No phase activation, Wasm execution or generated-code parity
is claimed.

## 2026-10-01: LSRA allocation-event and register-table diagnostics

Completed the 33-event Debug allocation dispatcher, strict register-table header
and register records. Native event ordinals, action/row ordering, scored
allocations, stored location normalization and target-specific seed sets are
preserved. Busy cells retain the native process-static format behavior: no text
before the first empty cell, then the last initialized column width across
allocator instances. Three existing title methods map unchanged.

All 64 new Windows and 63 new Linux-target cases pass. The complete selected
boundary passes 117/117 on Windows and 110/111 on Linux. Its sole failure remains
B493's unchanged Windows-shaped header expectation; the corrected SysV seed
prints R12/R13 and ten columns. This is a failed Linux selection, not a pass.
Five existing fixture setups now provide the references required by the native
header assertion, without changing expected output. ARM64 retains the exact
39-diagnostic compilation baseline and no fixture execution credit.

Retired six definitions, six declarations and the complete event enum:
437 body, 11 declaration and 47 type lines, 495 total. The enum closing brace is
included; unported diagnostic helpers remain. Full-analysis evidence, the
immutable fixture-compilation failure and corrected acceptance are recorded in
`artifacts/lsra-allocation-records-acceptance-c9cd5bb3/v2`. No Release execution,
backend activation or generated-code parity is claimed.

## 2026-10-01: Compiler scope-list diagnostics

Translated the whole Debug `compDispScopeLists` into `Compiler.ScopeDiagnostics`.
It displays the existing enter/exit index order without sorting or mutating
descriptors or cursors. Native headers, cursor arrows, integer/hexadecimal
widths and null-only `UNKNOWN` names are preserved. Name padding counts UTF-8
bytes, matching native `%10s`, rather than UTF-16 characters.

Five new exact-output/purity cases and eight existing scope controls pass on
both Windows and Linux-target Debug, 26 executions total. Both new files are
entirely Debug-guarded; no Release execution is claimed. ARM64 retains the exact
39-diagnostic compilation baseline, with no target fixture execution credit.

Retired one 45-line definition, one declaration and seven exclusive guard/spacing
lines, 53 total. The historically C#-shaped residual body has a distinct raw hash
from the oracle; whole-flow correspondence and exact reconstruction are recorded
in `artifacts/compiler-scope-display-acceptance-9687d9b9`. Existing
`compDispLocalVars`, accepted local/scope metadata and shutdown WIP are unchanged.

## 2026-10-01: ARM64 SVE instruction sanity translation

Translated the whole Debug SVE sanity dispatcher and 20 support algorithms.
The 337 format labels retain native register, option, size and immediate
predicates, assertion order, four fallthroughs and unexpected-format output.
The signed-magnitude decoder preserves pointer-sized output writes, sign bits
and final assertions. A continuing EE callback is not replaced by an exception.

The exact six-path snapshot retains the 39 existing ARM64 compilation
diagnostics, including locations, under full analysis. The 71 new Debug sanity
cases and updated recorder controls remain uncompiled and unexecuted;
122 Debug/50 Release are source projections, not successful tests or parity.
No backend, feature gate, descriptor layout or D009 policy changed.
The from-one signed-minimum case is managed-only because native subtraction
overflows (B500); the earlier B498 shift uncertainty remains unchanged.

Retired 21 definitions, five declarations and exclusive Debug scaffolding:
2060 body lines, one template prefix, five declaration lines and eight exclusive
lines, 2074 total. Exact oracle correspondence and native reconstruction are
recorded in `artifacts/sve-sanity-acceptance-c3d0bccd`.

## 2026-10-01: Compiler local metadata and scope traversal

Translated whole `compLocalVarName` and `compProcessScopesUntil` methods.
Name lookup preserves counted-array order, unsigned half-open lifetimes and
the first matching null name. Scope traversal preserves deferred enter/exit
cursors, equal-offset exit-before-entry ordering, descriptor identity and
callback replacement of the bitmap reference. Method-scope labels retain the
native control flow without an unsupported jump into a C# loop body.

The retained parameter-register lookup maps to the existing private
`LinearScan.findParameterRegisterLocalMappingByLocal`; no duplicate API or
caller rewiring was introduced. Its historical acceptance is not new execution
credit. The two new methods and existing scope controls pass 21 Debug and
12 Release cases on each x64 target, 66 executions total. ARM64 retains the exact
39-diagnostic compilation baseline and no fixture execution credit.

Retired three definitions and three associated declarations, 146 native lines.
The historically C#-shaped residual text has distinct raw hashes from the oracle;
whole-flow correspondence and exact retirement reconstruction are recorded in
`artifacts/compiler-local-scope-acceptance-b80f5d67`.

## 2026-10-01: LSRA copy/move busy-reference helper

Translated the whole `copyOrMoveRegInUse` helper into `LsraGlobals`, preserving
its early exits, inclusive unsigned reference ends and interval cursor lookup.
Tree comparison retains reference identity, including equal null pointers;
delayed-end overflow retains native unsigned wraparound. The existing
`isRefPositionActive` predicate has different semantics and remains unchanged.
No native callers were found, so no wiring or runtime-impact claim was added.

Thirty boundary cases pass in each full-analysis Windows and Linux-target
Debug/Release configuration, 120 executions total. ARM64 retains the exact
39-diagnostic compilation baseline, with no fixture execution credit.

Retired the 18-line definition and its ten-line exclusive heading, with no
separate declaration. Exact native reconstruction and fresh corrected-fixture
evidence are recorded in `artifacts/lsra-copy-move-acceptance-30b51a3b/v2`;
the initial missing-import failure remains preserved.

## 2026-10-01: Emitter descriptor primitive closure

Added the retained variadic `instrDesc.idInsIs` predicate as ordered,
short-circuit span membership. The constructor, single-instruction predicate
and Debug GC predicates/names map to existing initialization, predicates and
diagnostic behavior; no duplicate helpers or constructor changes were needed.

Seven new descriptor cases preserve instruction identity across matching
positions, misses and an empty tail. With existing initialization and invalid-GC
diagnostic controls, full-analysis Windows and Linux-target selections each pass
10 Debug and nine Release cases, 38 executions total. ARM64 retains the exact
39-diagnostic compilation baseline and has no fixture execution credit.

Retired five definitions, including one template prefix, and exclusive text:
52 native lines, with no separate declarations. Source seals, the preserved
first fixture analyzer failure, corrected acceptance and exact native
reconstruction are recorded in `artifacts/emitter-primitives-acceptance-30b51a3b`.
No generated-code or whole-runtime parity claim is made.

## 2026-10-01: Cross-platform intrinsic caller closure

Completed the retained `impXplatIntrinsic` mapping, most of which was previously
accepted implementation debt. The focused repairs restore Vector<T> destination
identity and width assertions, signed-packing base-type state, per-lane shift
width checks, the complete non-xarch geometric-sequence fallback and the native
min/max invariant. Existing helper APIs, feature defaults and ABI remain unchanged.

Full-analysis Windows and Linux-target selections each pass 28 Debug and
27 Release cases, 110 executions total. New controls exercise conversion
identity/assertion behavior and preserve narrowing/shift operand order.
ARM64 retains the exact 39-diagnostic baseline; its 26 selected source cases
remain uncompiled/unexecuted. No valid-input generated-code change or other-target
execution parity has been demonstrated.

Retired the 2888-line whole definition, its eight-line declaration and 24 exclusive
heading lines. The now-empty `hwintrinsic.cpp` then contained only nine scaffold
lines and was deleted, for 2929 native lines total. The whole body/declaration
match the pin after newline normalization; exact span reconstruction and the
subsequent scaffold-only deletion are recorded separately in
`artifacts/xplat-caller-acceptance-1ab6f43e`.

## 2026-10-01: ARM64 SVE immediate recorder source completion

Translated the five whole immediate-only, single-register, register-immediate,
floating-immediate and predicate-pattern recorders, with eight supporting
bitmask, element-size and register/option helpers. Descriptor selection, alias
preference, signed immediate shifting and display-before-append ordering retain
the pinned algorithms. The independent Debug SVE sanity dispatcher still
terminates before group append.

The isolated ARM64 probe retains exactly 39 pre-existing compiler diagnostics,
including locations. Consequently, the 46 new and four updated source-selected
cases remain uncompiled and unexecuted; this is source completion, not a target
build/test or generated-code parity pass. All changed bodies are ARM64-guarded.
B498 records native undefined shifts affecting bitmask alias preference; the
literal translation introduces no width-specific repair or native-parity claim.

Thirteen definitions, six declarations and exclusive text retire 588 native
lines: 530 body, 12 declaration and 46 heading/spacing lines. Exact mappings,
sealed source, the guarded target probe and its diagnostic comparison are in
`artifacts/sve-recording-acceptance-7334256e`. Unported sanity/output dependencies
and unrelated working changes remain untouched.

## 2026-10-01: Intrinsic import and patchpoint callers

Completed the whole `impHWIntrinsic` caller, retaining ARM64 struct/tuple,
scalar-variant, immediate-validation and masking branches, and Wasm lane handling.
Two ARM64 flag queries are implemented; genuinely unported special/fallback,
scalar-variant and immediate-bound helpers remain typed terminating dependencies.

`generatePatchpointInfo` now preserves ARM64, LoongArch and RISC-V frame adjustments,
special local offsets and FP/LR/RA save masks without changing EE allocation or
publication ordering. The AMD64 fixture independently checks ABI-specific XMM6
preservation and initializes its non-Swift register masks before recording usage.

Full-analysis Windows and Linux-target Debug/Release each pass 18 focused cases,
72 executions total. ARM64 retains the exact 39-diagnostic negative baseline;
the new intrinsic/patchpoint ARM fixtures remain source-only. This is not a
generated-code or other-target execution-parity claim.

Four definitions, one associated declaration and exclusive headings/spacing retire
1011 native lines. The patchpoint residual was historically C#-shaped, so its
evidence uses whole-flow review and distinct native/oracle hashes rather than byte
equality. Reproduce the focused selections and exact ARM diagnostic comparison
from `artifacts/intrinsic-patchpoint-acceptance-f845b543`, whose sealed snapshot
excludes independent SVE/Xplat and protected WIP.

## 2026-10-01: Residual source remnants

Removed `codegenxarch.cpp` and `jiteh.cpp` after confirming that only comments,
includes and empty target guards remained. Existing accepted codegen/EH mappings
provide the translation evidence; this removes 141 scaffold lines, not additional
definitions or declarations. No new compiler or test run was needed.

Also removed `regalloc.h` after matching both enum definitions, conditional
members and the default alias to `regalloc/FrameType.cs`. Removed `emitdef.h`
and `emitfmts.h`, which contained only target-selected include dispatch;
the included target definitions and format data were not removed. These three
headers account for another 74 lines of previously mapped types and scaffolding,
not new implementation or execution coverage.

The final four removals were the empty `instr.cpp`, include-only
`regallocimpl.h`, and the mapped `lsra_score.h` and `lsra_reftypes.h` tables.
All 17 score values, order IDs and diagnostic labels match the handwritten
register-selector/statistics mappings; all 13 reference values and labels match
`RefType.cs` and allocation diagnostics. Neither table is a generator input or
source/build dependency. This adds 146 lines, for nine files and 361 lines total.

The bounded review covered 75 files. The other 66 contain untranslated work or
have uncertain mappings and were not removed. Review coverage does not imply
translation completeness or execution parity.

## 2026-10-01: LSRA tuple and record diagnostics

Definition-list sequence numbers retain native unsigned formatting; tuple local
and interval fields and reference candidate counts retain native signed-bit
interpretation. Unknown reference names now follow native null-name output.
Interval diagnostics restore the related-upper-vector assertion, and register
records expose the native forwarding dump without adding a newline.

The existing private `LinearScan.dumpInterval` remains the canonical mapping of
native `Interval.dump`; no public API relocation or duplicate algorithm is added.
Tuple traversal, operand ordering, score names and tiny/micro dumps retain their
existing algorithms.

Full-analysis fixtures pass 39 Debug/4 Release on Windows and 38/4 with the
Linux target: 85 executions, including nine new cases in each Debug selection.
Release runs unchanged allocation controls, not Debug-only diagnostic cases.
ARM64 retains its exact 39-diagnostic baseline. B493 remains unmodified and outside
this focused selection; no generated-code or other-target execution is claimed.

Sixteen whole definitions and fourteen associated declarations retire 798 lines:
771 body lines, 15 declaration lines and 12 exclusive heading/guard lines.
Retirement preserves actual untranslated work, not declarations needed only
to compile residual callers.

## 2026-10-01: Shared call registers and frame initialization

Shared bitcast, indirection-cell and tailcall checks retain their native target
branches. Swift error-register production now preserves move, GC-state transfer
and register-production order. ARM tailcall checks include both halves of a
double register. Frame poisoning preserves native pointer widths, stack-slot
selection, immediate reuse and target-specific large-local initialization;
unknown-size ARM64 poisoning remains a terminating dependency.

Full-analysis focused fixtures pass 27 Debug/17 Release on Windows and 23/23
with the Linux target: 90 executions. Swift controls run with the existing
Linux-target feature settings, not newly enabled Windows features. ARM64
retains the exact 39-diagnostic baseline after correcting missing mask aliases.
The final correction is confined to the non-xarch branch; an exact excluded-branch
comparison supports reusing the x64 results. No generated-code parity is claimed.

Seven whole definitions and 21 declarations retire 284 lines. Six declarations
belong to this family; fifteen clear known graph/statistics and intrinsic/issue
declaration debt using existing accepted evidence, without body re-audit or new
tests for that debt. Residual callers are not a retention gate. The bitcast
declaration shared with the unported Wasm definition and the unported ARM64
poison-helper declaration remain for those definitions, not for callers.

## 2026-10-01: Parameter-homing graph and LSRA statistics

The retained register-interference graph preserves insertion and incoming-edge
order, first-sink/last-cycle selection, copied-register release and unsigned
overlap checks. Its dump now prints unsigned destination offsets with native
signed `%d` interpretation.

LSRA statistics aggregate block zero and actual pre-resolution blocks, excluding
detached slots and resolution blocks. Text counters preserve signed bit
interpretation; CSV/summary counters remain unsigned, totals wrap at 32 bits,
and weighted aggregation retains unsigned inputs. Name/score helpers retain the
native `TRACK_LSRA_STATS` guard and the existing table and heuristic mapping.

Full-analysis Windows fixtures pass 28 Debug/8 Release; Linux-target Release
passes eight. Linux-target Debug executes 26 cases with 25 passing and one
unchanged register-table-header failure, reproduced with identical error text
on the committed baseline (B493). The failure is retained, not suppressed or
counted as a pass. All four new graph and five new statistics cases pass in
both Debug selections. ARM64 retains the exact 39-diagnostic baseline.

Thirteen whole definitions retire 383 body lines, with no declaration credit.
All match the pinned oracle after line-ending normalization; graph scaffolding,
the separate `Get` declaration and the native statistics enclosure remain.

## 2026-10-01: Intrinsic properties and instruction-issue bookkeeping

Six retained hardware-intrinsic property predicates preserve native containment,
RMW, EVEX, broadcast and embedded-masking decisions. The RMW predicate restores
its compiler-context assertion. Focused cases cover all 256 ternary controls
within one case and reject masking that would suppress a contained memory fault.

Shared instruction issue preserves unsigned size deltas, signed adjustment wrap,
target-specific descriptor updates and native diagnostic text. Instruction lookup
retains first and one-past-end positions; Debug callsite recording preserves lazy
signature queries, method-scoped storage and EE callback order.

Full-analysis fixtures pass 92 Debug/89 Release on Windows and 75/74 with the
Linux target: 330 executions, including 24 intrinsic-property cases per
configuration and 13 Windows/10 Linux emitter cases. Fixture-only retries reuse
unchanged Core outputs. ARM64 retains its exact 39-diagnostic baseline; these
results do not establish generated-code or other-target runtime parity.

Nine definitions retire 677 body lines and 15 exclusive heading lines.
The six historical C#-shaped GenTree bodies retain separate residual/oracle
hashes; three emitter bodies match after line-ending normalization. Live
declarations, feature-disabled fallbacks and backend dependencies remain.

## 2026-10-01: VN constant extraction and register selection

Scalar and handle extraction now shares the native internal path, including
reference-offset and Windows pointer-destination assertions, handle fast return,
and strict reinterpretation-versus-coercion diagnostics. Floating comparisons
preserve storage bits. Pointer aliases use actual host storage width; the
existing width preflight prevents an overread after a continued assertion.
Floating-to-integer equivalence is limited to native-defined inputs.

Register selection retains all 24 native bodies. Five restore ARM paired-half
cost and optional-register behavior, ARM64 consecutive-register exceptions,
and raw next-reference classification; 19 are unchanged retirement debt.
Native heuristic priorities and asymmetric tie rules remain unchanged.

Full-analysis Windows and Linux-target fixtures each pass 82 Debug/66 Release:
296 executions, including 39 Debug/27 Release VN cases per target. Fixture-only
retries reused unchanged Core outputs. ARM64 retains its exact 39-diagnostic
baseline; the eleven new ARM32/ARM64 selector entries remain unexecuted.

Thirty definitions retire 854 native body lines, including template introducers.
All bodies match the pinned oracle after line-ending normalization. Separate
helper declarations, type scaffolding and selector drivers remain.

## 2026-10-01: LSRA resolution and SIMD memory constructors

Critical-edge resolution now uses native ARM register-pair masks and the
recoverable switch-operand assertion. Final verification preserves the native
double-half skip and exact move, swap and resolution-block diagnostic rows.
Existing classifier and verifier modes remain unchanged.

Aligned and non-temporal SIMD memory constructors retain their ARM64/Wasm
ordinary-load/store fallbacks, optimization assertions and operand ordering.
The existing xarch algorithms and default features are unchanged.

Full-analysis Windows and Linux-target fixtures each pass 30 Debug/8 Release:
76 executions. Five new Debug cases per target cover the assertion and
diagnostic corrections. ARM64 retains its exact 39-diagnostic baseline;
ARM32 pairing and ARM64/Wasm memory projections remain source-only.

Ten definitions retire 1,263 native body lines. Six LSRA bodies match the pinned
oracle after line-ending normalization. Four historical memory-constructor
bodies retain separate residual/oracle mappings and hashes; declarations remain.

## 2026-10-01: GC diagnostics and generation orchestration

Shared emitter diagnostics retain native target indentation, assertion ordering,
descriptor traversal and GC delta cursor updates. The GC state dump prints six
real pinned field addresses with the existing diffable masking policy.

Generation, emission, metadata and prolog orchestration retain their native
target branches. A guarded first-phase callback preserves unsupported-backend
rejection, phase tracking and borrowed-output cleanup. Wasm funclet-location
updates remain a terminating dependency; publication and ABI policies are unchanged.

Full-analysis fixtures pass 111 Debug/49 Release on Windows, 100/44 in
Linux-target Core and 5/5 in SysV Targets: 314 executions. ARM64 retains exactly
39 existing diagnostics. These checks do not establish other-target runtime parity.

Thirteen whole definitions retire 955 native lines: 907 body lines and 48
exclusive headings and spacing. All bodies match the pinned oracle after
line-ending normalization; live declarations and native dependencies remain.

## 2026-10-01: Upper-vector state and SIMD sequences

Upper-vector save/restore retains the native target predicates and the
recoverable assertion before an empty conditional/switch block is inspected.
The unchanged save algorithm reuses existing coverage; two new optimized-Debug
cases verify the error class without inserting nodes or changing register homes.

Index and sequence construction now retains fixed ARM64/Wasm and scalable ARM64
branches. Reductions preserve native pairwise widths and Wasm shuffle/add
grouping, including capture-before-reload ordering. The common ToScalar return
is available to explicit-HW Wasm without enabling that feature by default.

Full-analysis fixtures pass 17 Debug/15 Release on Windows and 12/12 on
Linux-target, 56 executions. The exact 39-diagnostic ARM64 baseline remains;
99 ARM64 and 28 Wasm sequence-fixture cases are source projections only.

Seven whole definitions retire 803 native body lines, retaining live
declarations, feature guards and the independently used vector-save type helper.
The three LSRA bodies match the pinned oracle after line-ending normalization;
four historical C#-shaped constructor bodies retain separate raw/oracle hashes.

## 2026-10-01: Loop alignment and root prologs

Shared loop-alignment recording, sizing and adjustment retain the native x86
and ARM64 branches, including fixed-width descriptors and ARM64 adaptive
budgets. Padding keeps native-sized offsets, and mismatch diagnostics preserve
text and assertion ordering.

Frame finalization and root prologs retain whole target branches and the
native-empty non-Wasm begin hook. Signed register masks preserve their unsigned
bit patterns during scratch selection and counting. ARM stack-allocation and
push-mask unwind helpers, ARM64 unknown-size frames, target unwind padding and
the distinct Wasm begin hook remain terminating dependencies. The existing
SysV NativeAOT/varargs rejection policy is unchanged.

Full-analysis Windows fixtures pass 144 Debug/134 Release; Linux-target Core
fixtures pass 49 Debug/44 Release, with 12 existing SysV target controls in each
configuration: 395 executions. Two subsequent corrections affect only
non-x64 branches; the final ARM64 comparison retains exactly 39 diagnostics,
including source locations. Other-target projections are not execution or
generated-code parity evidence.

Fourteen definitions retire 2,002 native lines: 1,854 body lines and 148
exclusive headings, guards and spacing. Exact three-file reconstruction
retains live declarations and native dependency implementations.

## 2026-10-01: LSRA register states and numeric SIMD constructors

Register-state/block-start retirement includes a Debug rotation correction:
selecting or wrapping XMM31 preserves the signed enum's bit pattern under checked
arithmetic. Eight unchanged bodies reuse established LSRA implementation and
coverage, including the existing ARM paired-register helper placement.

Ceiling, floor, rounding, conversion and FMA constructors retain complete
ARM64/Wasm branches. ARM64 preserves native FMA operand order; Wasm retains
unsupported FMA and long-conversion behavior rather than emulating different
numeric semantics. The scalable-VN wrapper reuses stored constants or the
existing scalar-broadcast helper.

Full-analysis Windows fixtures pass 113 Debug/102 Release and Linux-target
fixtures pass 45 Debug/41 Release, 301 executions. Four Debug rotation cases
cover FLOAT/DOUBLE selection and wrap; 18 numeric constructor cases run per
configuration. ARM64 retains its exact 39-diagnostic baseline; its 61 new cases
and Wasm's 14 remain source-only.

Sixteen whole definitions retire 772 native lines. All six constructor
declarations remain for live intrinsic-import callers; no declarations or
surrounding target guards are removed.

## 2026-10-01: Frame helpers and parameter homing

Frame-size estimation, SP-relative conversions, temporary ordering and Async
header allocation retain their whole native target branches. Initial-SP
conversion preserves unsigned offset bits, and ARM prespill intersections
normalize bank-relative masks while retaining both halves of a double.

Register/stack parameter homing, split-parameter homing and generic-context
reporting retain target-specific algorithms. Swift stack reassembly is restored
at its native block-driver position. LoongArch address emission, RISC-V reserved
register selection and the distinct Wasm local-load implementation remain
explicit terminating dependencies rather than success-shaped fallbacks.

Full-analysis Windows fixtures pass 102 Debug/83 Release and Linux-target
fixtures pass 62 Debug/54 Release, 301 executions. Six direct Swift-body cases
run on Linux-target; four Windows+Swift driver cases remain source-only because
default Windows disables Swift and Linux block generation remains Windows-gated.
ARM64 retains its exact 39-diagnostic baseline; other-target source projections
are not execution or generated-code parity evidence.

Eighteen definitions and one exclusive declaration retire 858 net native lines.
Exact reconstruction preserves two live prototypes and the closing
`!TARGET_WASM` guard included in the 861 selected lines. Historical C#-shaped
frame spans are hashed separately from the pinned oracle.

## 2026-10-01: Emission finalization and hardware construction

Emission finalization retains the whole target/feature body, including profile
alignment, GC state, forward patches and statistics. Unsigned allocation sizes
remain unsigned through code-offset bounds and arithmetic. Unsupported-target
issuing and diagnostic dependencies terminate explicitly; the existing label
output helper is reused.

Broadcast construction now includes ARM64 scalable repeated constants and the
native Wasm nonconstant path. Load/store classification retains native Wasm
metadata and address selection, while preserving all xarch/ARM64 cases. This
closes B477 without enabling default Wasm hardware intrinsics.

Full-analysis Windows fixtures pass 88 Debug/86 Release and Linux-target fixtures
pass 64 Debug/66 Release, 304 executions. High-bit hot/cold allocation regressions
exercise bounded buffers. ARM64 retains its exact 39-diagnostic baseline; its
89 new cases and Wasm's 54 remain source projections, not execution evidence.
Two definitions retire 1,023 native lines; the already-absent broadcast and
classification bodies receive no new retirement credit.

## 2026-10-01: Frame offsets and shared codegen

Local and temporary frame layout now retains the complete target branches,
including ARM prespills, 32-bit alignment and OSR diagnostic identity. Shared
helper-call masks, stack adjustment and method exit preserve native policies;
the already-complete lifetime transition body is also retired. Debug-only stack
sentinel assertions retain their native configuration scope.

Full-analysis Windows fixtures pass 122 Debug/98 Release; Linux-target fixtures
pass 64 Debug/51 Release, 335 executions in total. All four exit return-root
cases run on both ABIs after constructing the fixture's return descriptor.
Existing Windows-only orchestration and ABI-specific controls remain selected
on Windows. ARM64 retains 39 existing diagnostics; these results do not establish
other-target execution or generated-code parity.

Seventeen definitions and two declarations retire 2,060 net native lines:
2,061 lines removed and one ARM prespill accessor prototype retained for live
callers. The native Async helper remains. Historical C#-shaped residual bodies
are separately hashed from the intact oracle, not treated as byte-equal.

## 2026-10-01: Emitter binding and SIMD comparisons

Five shared emitter bodies now preserve whole target branches, Windows binding
diagnostics and native last-jump cache behavior. The main and All SIMD comparison
callers also retain Wasm's unsigned-64 signed-bias fallback. Other-target emitter
classification/encoding helpers remain explicit terminating dependencies.

Combined Windows/Linux Debug runs pass 346 cases each and Release runs pass 320,
1,332 executions. ARM64 retains its 39 existing diagnostics. Five definitions and
two declarations retire 1,171 native lines; B478 has no new retirement credit.
Live-caller declarations remain. Explicit-HW-Wasm broadcast and memory
classification blockers remain; source completion is not Wasm execution parity.

## 2026-10-01: LSRA lifetime, spills and kills

Twelve whole retained definitions are closed: ten reuse their accepted
implementations and evidence; register-bank selection and constant matching
restore missing target conditions and native assertion checks. LoongArch64 and
RISC-V64 floating arguments can select the integer bank, and only 64-bit targets
reject negative constant reuse across types.

Thirteen focused cases and eight existing constant controls pass per
Windows/Linux Debug/Release configuration, 84 executions. ARM64 retains exactly
39 historical diagnostics. Twelve definitions and nine declarations retire
512 lines; three declarations remain for live native callers. This does not
establish other-target execution or new generated-code parity.

## 2026-10-01: Value-number constructor retention lag

The already-translated store constructor is removed from the residual, retiring
84 lines without changing managed code or tests. Its initialization and budget
algorithm were compared with the pinned constructor; accepted compiled-source
hashes and dispatcher coverage were reused rather than replayed. The residual
had already lost five scalable/mask initializer lines whose managed fields are
present, so raw source equality is not claimed. Independent native helpers and
declarations remain. This adds no execution or parity credit.

## 2026-10-01: Wasm SIMD operation mapping

The binary and comparison mappers retain all 17 pinned Wasm branches, including
LIR-only AndNot and native unsupported byte-multiply, rotate and ordered-ulong
cases. Existing xarch/ARM64 selection is preserved. Wasm caller fixtures now
assert exact two-source scatter masks and operand order rather than obsolete
failure boundaries.

Fifteen mapper controls and 151 constructor/shuffle controls pass in each
Windows/Linux Debug/Release configuration, 664 cases total. ARM64 retains 39
historical diagnostics. Wasm cases remain source-only: explicit-HW memory
classification (B477) and the unsigned-64 comparison caller (B478) are separate
limitations. Both mapper definitions were already absent from the residual;
this repairs B476 without native retirement credit.

## 2026-10-01: Hardware-intrinsic value-number dispatch

The whole dispatcher retains non-xarch execution paths, xarch-only barrier and
imprecise-address cases, and native value-number diagnostic formatting. Heap
mutation precedes operand numbering; precise and opaque arities preserve
exception unions, address-load dependencies and unique mask-conversion results.

Windows/Linux Debug passes 176 cases and Release 175 each, 702 total.
The 36/35 new dispatcher cases run with 140 evaluator controls. One definition
and four exclusive declarations retire 229 native lines. ARM64 retains exactly
39 historical diagnostics. Explicit-HW-Wasm memory classification remains
incomplete (B477); neither target has new execution or generated-code parity.

## 2026-10-01: Shared SIMD shuffle construction

Constant and variable shuffle builders retain complete ARM64/Wasm byte-index
expansion, zero-fill, native/safe distinctions, shifts and capture ordering.
ARM64 half swaps use the native extraction shortcut. Xarch selection and the
native eligibility scan remain intact.

Twenty-three new cases plus 128 existing constructor/pair/rearrangement controls
pass per Windows/Linux Debug/Release configuration, 604 cases total. Three
definitions and three declarations retire 1,359 exact residual lines. Historical
C#-shaped residual bodies were compared with the intact oracle, not claimed
byte-identical. ARM64 retains 39 compiler diagnostics; Wasm binary/comparison
mapping gaps remain, including the unexpected native absence recorded in B476.

## 2026-10-01: Scalar math value numbering across targets

Unary and binary math evaluation retain complete target branches and AOT
folding predicates. ARM power-by-one preserves the native subnormal fixup;
RISC-V integer min/max retains signed/unsigned comparisons. Floating min/max
keeps Wasm NaN propagation and RISC-V number preference, including native
operand selection and signed-zero ordering.

Seventy-six new cases plus 40 controls pass in each Windows/Linux Debug/Release
configuration, 464 cases total. Two definitions and two declarations retire
694 native lines. ARM64 retains 39 historical diagnostics; other-target numeric
execution and transcendental parity remain unverified. Existing libm and
unchecked integer-round conversion policy are unchanged.

## 2026-10-01: Ternary hardware-intrinsic value numbering

Ternary evaluation preserves whole target branches, raw conditional-selection
bits, integral lane replacement and the existing floating conversion boundary.
SVE fixed-vector selection retains native predicate/AND/AND_NOT/OR interning
order; unrepresentable predicates and unsupported folds retain symbolic VNs.

Fifty-one new cases pass in each Windows/Linux Debug/Release configuration.
Together with SIMD rearrangement and existing controls, 1,260 focused cases
pass. Two whole definitions retire 224 native lines; declarations needed by
remaining compiler callers stay. ARM64 retains 39 historical diagnostics,
and SVE/Wasm numeric execution remains unverified.

## 2026-10-01: SIMD rearrangement construction

Unzip and Reverse retain ARM64 and Wasm target algorithms alongside existing
xarch selection. The Wasm two-source helper constructs two scatter shuffles
and combines them in native order. Single-lane paths preserve comma hoisting,
temporary capture and exactly-once observable effects.

Twenty-two new cases plus pair/constructor controls pass on Windows/Linux
Debug and Release. Three definitions and three declarations retire 310 native
lines. ARM64 retains 39 historical compiler diagnostics; nontrivial Wasm and
general ARM64 shuffles still reach the independent single-source shuffle
dependency. Other-target execution and generated-code parity remain unproven.

## 2026-10-01: LSRA constructor and register banks

The constructor and caller/callee-save queries now retain their whole target
contracts. Separate float/double availability preserves ARM32 even-register
homes; target exclusions, xarch EVEX/APX choices, EnC and patchpoint restrictions
remain intact. Minimal handwritten register-bank constants match pinned target
headers, including legacy x86 and LoongArch64 count/mask distinctions.

Nineteen new cases pass in each Windows/Linux Debug/Release configuration.
With construction, sequencing, selection and stress controls, 156 focused cases
pass. Three definitions and one declaration retire 239 native lines. ARM64
retains 39 historical compiler diagnostics and x86 four preexisting parser
diagnostics; other-target execution and full allocator parity remain unproven.

## 2026-10-01: Hardware-intrinsic value numbering

Broadcasts, mask conversions and unary/binary intrinsic evaluation preserve
whole-target algorithms, scalar-before-vector interning, NaN payloads and signed
zero. SIMD12 long/double operations use native complete-element counts without
touching incomplete tails. GetElement and WithElement reinterpret negative
indices unsigned, retaining symbolic VNs instead of checked-overflow exceptions.

Forty-nine new cases pass in each Windows/Linux Debug/Release configuration with
existing intrinsic and SIMD controls. Seven definitions and two declarations
retire 1,539 native lines; higher arities and shared native declarations remain.
ARM64 and Wasm behavior is source-only behind existing target limitations.

## 2026-10-01: SIMD pair construction

Narrow, Concat and Zip retain their complete target branches. ARM64 half
extraction/insertion and selected-scalar ordering are restored; Wasm callers
construct their native selectors before the explicit unported two-source shuffle
dependency. Existing xarch algorithms and single-lane side-effect ordering remain.

Forty-two pair-construction cases and 64 existing constructor controls pass in
Windows/Linux Debug/Release. Three definitions and three declarations retire
788 native lines. ARM64 remains behind the same 39 compiler diagnostics; Wasm
shuffle execution and generated-code parity are not established.

## 2026-10-01: Shared emitter diagnostics and costs

Instruction-format names and address/offset helpers now live in shared emitter
code. Address output follows native raw host-width formatting, and execution
costs preserve target write latencies and first-operand `std::max` behavior,
including negative zero. The unhandled-instruction diagnostic retains its
whole target-independent body.

Twenty-seven Debug/thirteen Release new cases pass on Windows and Linux targets
with existing display and cost controls. Five definitions and five declarations
retire 84 native lines. Other-target execution characteristics still terminate;
unrelated xarch output algorithms and D009 are unchanged.

## 2026-10-01: LSRA sequencing and visitation

LSRA now initializes reachable metadata before appending unreachable blocks and
visits each unreachable block before publishing its sequence entry. Critical-edge
classification uses the shared unique-predecessor contract, including the
implicit entry-prolog predecessor.

Six Debug/five Release new cases and four existing block-order cases pass on
Windows and Linux targets. Eight definitions and five declarations retire 355
native lines, including unchanged complete visitation/conflict helpers. The
constructor remains separate pending exact target register-bank prerequisites.

## 2026-10-01: Shared generation initialization and driver

The merged code-generator constructors retain native target initialization and
owner aliases. The common driver preserves Wasm rejection predicates, generation
phase and SPMI query order, and borrowed output lifetimes. Unsupported machine
and emission dependencies still terminate; D009 is unchanged.

Generation cases pass 3 Debug/2 Release on Windows and 4 Debug/3 Release on the
Linux target, with Windows metadata and emission controls. Three definitions
and four declarations retire 140 native lines. Wasm predicates and ARM64
initialization remain source-only; this does not establish target code generation.

## 2026-10-01: Shared scope publication

Scope allocation, recording and publication now use their common algorithms
outside Windows AMD64. EE storage ownership and publication order are unchanged.
The x86 varargs branch preserves cookie-relative unsigned arithmetic and mutates
the original live-range location rather than a copy.

New scope cases pass 16 Debug/14 Release on Windows and Linux targets, alongside
22 Debug/19 Release existing scope controls. One definition and one declaration
retire 102 native lines; five already-absent bodies receive no duplicate credit.
The x86 cases remain source-only behind existing parser failures. High-count
signed diagnostic walks remain an independent limitation.

## 2026-10-01: Shared EH publication

EH table construction, VM-order publication and emitter-cookie/offset helpers
now retain their common target paths. Count publication precedes clause
publication; filter offsets, native end-offset fields, same-try identity,
unsigned offset bits and metrics ordering remain unchanged. The missing cookie
assertion is restored with its native recoverable-failure regression.

Focused full-analysis selections pass 18 Debug/16 Release Windows and
12 Debug/12 Release Linux-target cases, including twelve EH cases in every run.
Four definitions retire 145 native lines; shared declarations and the
independent Wasm publisher remain. Reconstruction also removes the single
blank line left at the end of the emptied `jiteh.cpp` body to satisfy Git's
whitespace check. Existing EE dependencies receive no duplicate retirement
credit. These Windows-host fixtures do not establish target execution or parity.

## 2026-10-01: Register-set constructor retirement debt

The existing complete `RegSet` constructor and its exclusive declaration retire
35 retained native lines. Spill initialization, reserved/callee-saved/prespill
masks, Swift support and debug state already have their managed equivalents;
no compiler source changes or new capability are credited. Current Windows
fixture construction and accepted Windows/Linux shared-codegen execution are
reused without a retirement-only build.

Exact reconstruction preserves the native register-mask table, x86 FP-stack
spill and ARM32 prespill-mask helper. A recovery ref protects the preceding
residual revision; native history remains the pinned oracle plus one removal
commit. The non-obvious `regset` to `regsset` directory mapping is recorded in
the checkpoint. This clears one concrete historical debt item, not all debt or
an execution/parity milestone.

## 2026-10-01: Object-description NUL termination

Frozen-object diagnostics now stop at the first NUL, matching native `%s`
printing while retaining the bounded EE-written span and newline replacement.
Seven direct callback cases cover leading/embedded NUL, bytes after the reported
extent, empty/plain/UTF8 descriptions and newline replacement. Full-analysis
Windows VN-intrinsic selections pass 38 Debug/24 Release cases. The native
definition was already retired; this correction receives no new retirement
credit. B475 is fixed in source, with fresh AOT capture still pending; the
historical helper comparison and B135 remain unchanged.

## 2026-10-01: Fresh helper captures and native repeat control

A fresh Debug NativeAOT JIT from committed source `6d8ef365` executes the eleven
selected static-init, TLS and UTF8 methods against the verified `33baf8ee` host.
Nine captures preserve full named helper phases and raw method bytes, including
independent native repeats. The strict comparer passes its twenty focused tests
but the real comparison fails: five methods' bytes vary between native runs,
and ten native/managed full-phase sections differ. Profile-check text retains
B135; two UTF8 methods expose object-description NUL handling tracked as B475.
Three explicit environment differences also remain in the report.

These are diagnostic findings, not a parity pass or accepted output differences.
No address masking is applied and no new native retirement is credited.
Source porting and exact retirement continue independently. The three small
corpus builds take 0.92-1.39 seconds each; the focused tooling tests take
0.501 seconds. Historical captures and the accepted AOT publication remain
unchanged. Reproduction inputs and differences are under
`artifacts\windows-helper-parity-after-shared-codegen-1`.

## 2026-10-01: Shared operand, frame and debug-publication closure

Operand adapters, frame initialization, OSR argument homing and stack-segment
homing now retain their whole native target branches. Float-mask overlap keeps
native seed reuse and copy instructions; zero-register selection preserves
target-specific scratch state. Stack segments retain rounded native store widths
and optional zero-state updates rather than exact-width copies.

Line and rich-debug publication no longer require Windows AMD64. The three EE
line-mapping dependencies preserve allocation, recording and ownership transfer
on the shared path. High-bit count tests retain native unsigned nonzero semantics.
Rich mappings preserve allocation order, inline ordinals, coincident offsets and
unsigned offset bits. Existing Windows algorithms and callback layouts remain.

One combined full-analysis matrix passes 117 Debug/109 Release Windows cases and
90 Debug/87 Release Linux-target cases. All 403 cases are matched to their exact
source-typed identities. Six named diagnostic pairs retain the existing
14/78/39/87/87/85 x86/ARM32/ARM64/LoongArch64/RISC-V64/Wasm errors, with no warnings.
The non-xarch flags correction changes only those three target signatures;
unchanged x64 results are reused, not replayed.

Twelve whole definitions and nine exclusive declarations retire 1008 native
lines, with full residual reconstruction and the pinned oracle preserved.
Three previously absent EE definitions receive no additional retirement credit.
ARM prespill and other-64-bit constant-emission helpers remain terminating
dependencies. Linux-target fixtures are Windows-host tests, not Linux runtime
or generated-code parity; early target failures can mask downstream bodies.
The existing high-count diagnostic-loop discrepancy is tracked as B474.

## 2026-09-30: Shared condition branches and result publication

Whole `genCodeForJcc`, `inst_JCC` and `genCodeForSetcc` now preserve the native
x86/ARM branches rather than requiring AMD64. Single, OR and AND condition
sequences retain branch order, short-circuit labels, false-target fallthrough
and result-register publication. The independent ARM `inst_SETCC` helper
remains a typed terminating dependency.

Four final Windows/Linux Debug/Release runs each pass twelve selected cases,
including eight new fallthrough and signed/unsigned condition controls. Three
fresh named target diagnostics preserve the exact fourteen x86, seventy-eight
ARM32 and thirty-nine ARM64 errors against authenticated original-path cast
baselines. Raw NUnit identities remain preserved; equal-valued condition enum
aliases are matched to their exact source-typed cases, not accepted by count.

Three whole callers retire 66 native lines, including their exclusive inner
guard. The outer non-Wasm guard, shared declarations and independent ARM helpers
remain. Exact oracle proof and residual reconstruction do not establish
other-target execution or generated-code parity; early diagnostic failures can
mask downstream bodies.

## 2026-09-30: Shared scalar casts and 32-bit long stores

The shared cast dispatcher and integer-cast description now retain their native
target branches instead of requiring AMD64. Wasm keeps 64-bit scalar checks on
its 32-bit target; LoongArch64 and RISC-V64 narrowing retains ABI-required sign
extension. The shared 32-bit long local store consumes both halves in native
order and uses their four-byte homes. Independent non-xarch cast emitters and
ARM32 long-to-int emission remain terminating dependencies.

Four final Windows/Linux Debug/Release configurations each pass forty-six
selected cases, including ten new range, load-width, signedness and overflow
controls. The four matching baselines each pass thirty-six controls. Seventeen
fresh full-analysis commands and three authenticated historical negative
baselines preserve exact target diagnostic identities and multiplicities.
Historical x86/ARM32/ARM64 evidence retains its original paths; it is not
relabeled as fresh execution. Eight LoongArch64/RISC-V64/Wasm cases remain
source-only, and early target errors can mask later bodies.

Three whole definitions and three exclusive declarations retire 272 native
lines. Oracle authentication and exact residual reconstruction preserve the
outer non-Wasm guard, following condition callers and independent cast helpers.
Focused fixtures and native retirement do not establish other-target execution
or generated-code parity.

## 2026-09-30: Xarch write barriers, casts and GC publication

The remaining xarch codegen family now preserves x86 register-specific write
barriers, checked long-to-int cast ordering, JIT32 GC publication callers and
the native x86 OSR unreachable path. AMD64 conditional comparison and modern GC
publication retain their native SysV branches instead of Windows-only guards.
JIT32 encoding/filter dependencies and the Unix GC encoder remain separate;
the opaque JIT32 header carrier is not a native-layout claim.

Windows controls pass thirty-seven Debug and thirty-five Release cases; Linux
controls pass thirty-one in each configuration. Both Linux baselines reproduce
six conditional-compare guard failures and twenty-two passing controls; the
same six identities pass after the change. Windows baseline controls and three
new AMD64 cases are preserved. Eighteen x86 cases remain source-only. Paired
x86 Debug/Release, ARM32 Debug and ARM64 Debug diagnostics retain their exact
earlier error identities and occurrence counts, with zero warnings/tests.

Seven whole functions, the complete condition map and three exclusive
declarations retire 543 native lines. Exact oracle authentication and native
reconstruction preserve shared other-target declarations, independent GC
encoders, filters and emitter helpers. Live dump diagnostics remain; the native
DEBUG `if (0)` hexdump is unreachable and excluded. Focused target checks do not
establish other-target execution, Unix GC publication or generated-code parity.

## 2026-09-30: ARM32 immediate validation and materialization

Whole ARM32 immediate/displacement validators, Thumb modified-immediate
predicates, low-register classification and register-immediate materialization
now preserve the native algorithms. Relocation addresses and zero checks remain
host-sized before nonrelocatable values narrow to 32 bits; MOVW/MOVT, SXTH and
flag-setting choices retain their native order. Recording helpers remain
independent terminating dependencies.

This batch also corrects the common frontend's class-closing target guard:
non-xarch preprocessing now retains the class closing brace, without changing
xarch code. Eight baseline/final Windows/Linux Debug/Release controls each pass
forty-eight cases. Paired ARM32 diagnostics retain the same seventy-eight errors
and zero warnings, with no ARM32 tests executed. The sixty-two new ARM32 cases
remain source-only; declaration failures can mask later body diagnostics.

Seventeen whole definitions and fifteen exclusive declarations retire 297
native lines. Every body and guard is oracle-authenticated; the already-absent
intervening branch-link validator is not counted again. Exact reconstruction and
native consolidation preserve independent classifiers, encoding helpers,
recording bodies and tables. The original parser failure and memory-stopped
run remain invalid historical evidence, separate from the fresh accepted runs.
These checks do not establish ARM32 execution or generated-code parity.

## 2026-09-30: Common instruction frontend and target tables

Instruction names, operand-size strings, zero-operand generation, FP and
pseudo-name metadata, Wasm SIMD element widths and return dispatch now preserve
their whole native target branches. The table generator supplies ARM32,
ARM64/SVE, LoongArch64, RISC-V64 and Wasm names and metadata in native order.
Existing xarch table entries remain unchanged; synthetic `lea` metadata is
zero-initialized where native includes it, without adding a synthetic name.
The live instruction enum is unchanged.

The same forty-eight focused controls pass in Windows and Linux Debug/Release.
Seven whole definitions and seven exclusive prototypes retire 207 native lines,
including headings and owned guards. Independent emitter tables and recording
helpers remain native; the previously absent ARM64 metadata table is not counted
again. ARM32 return remains intentionally folded into its epilog; ARM64's
unreachable path and other-target NYIs terminate.

The x86 diagnostic retains fourteen earlier errors and zero warnings/tests.
Two positive x86 X87 controls, two ARM32 FP controls, one ARM64 SVE-name control
and three Wasm SIMD-width controls remain source-only. Focused AMD64 checks do
not establish other-target execution or generated-code parity.

## 2026-09-30: Floating conversion and math instruction selection

The whole xarch conversion selector is reachable on x86, and ARM32 conversion
and math selection preserve native VFP opcodes. Unsupported ARM32 long
conversions retain their four native NYI diagnostics and explicitly terminate.
The existing xarch math selector is unchanged.

Eighteen focused controls pass in Windows and Linux Debug/Release: eight math
and ten existing AMD64 conversion cases. Three whole definitions and two
exclusive prototypes retire 181 native lines, including the complete shared
xarch/ARM conditional enclosure. The xarch math body was already absent and
is not counted again. The x86 diagnostic retains fourteen earlier errors and
zero warnings; its ten new conversion controls and seventeen ARM32 controls
remain unexecuted. These checks are not generated-code parity.

## 2026-09-30: Scalar condition, extension and store selection

Whole condition-setting, extending-move and source-register store selectors
retain xarch SETcc/APX selection, ARMARCH register/memory extension rules and
integer/floating register-bank conversions. ARM64 condition classification
remains a typed terminating dependency; existing target emitter bindings are
reused rather than duplicated.

The same forty-seven focused controls pass in Windows and Linux Debug/Release.
Four whole definitions and four exclusive prototypes retire 375 native lines,
including headings. `CodeGenInterface::ins_StoreFromSrc` maps to the managed
`CodeGen.StoreFromSource.cs`. Independent condition and emitter helpers remain
native. The x86 diagnostic retains fourteen earlier errors and zero warnings;
nine ARM64-only cases remain unexecuted. These checks are not generated-code
parity.

## 2026-09-30: Register-pair and constant-shift adapters

Whole register-pair and shift adapters preserve default actual-type sizing,
explicit-size overrides, ARM32 flags and xarch implicit-one instruction
selection. Target32 shift arguments retain their signed conversion. Unknown
targets keep the native NYI diagnostic and explicitly terminate if it returns.
The upper encodable count 255 records the native emitter's canonical 127.

The same twenty-six focused controls pass in Windows and Linux Debug/Release.
Two whole definitions and two exclusive declarations retire 81 native lines,
including headings. Independent emitters and ARM immediate helpers remain
native. The x86 diagnostic retains fourteen earlier errors and zero warnings;
its unsigned-maximum control remains unexecuted. These checks do not establish
other-target or generated-code parity.

## 2026-09-30: Local and spill-temporary store adapters

Whole local and spill-store adapters retain native assertions, local bounds,
actual-type widths and negative temporary numbers. ARM64 keeps the SVE store
classification exception. The xarch stack-only adapter preserves the unsigned
offset's bit pattern at the signed emitter boundary.

The same sixteen focused controls pass in Windows and Linux Debug/Release,
including separate local/spill homes and byte/int actual-width checks.
Three whole definitions and three exclusive declarations retire 61 native
lines, including headings and guards. Independent emitter classification and
recording bodies remain native; missing target bindings terminate.
The x86 diagnostic retains fourteen earlier errors and zero warnings, not
successful x86 compilation or execution. These checks are not generated-code
parity.

## 2026-09-30: Register/immediate adapters and tail-jump argument placement

Common register/immediate adapters retain native target dispatch, target32
signed truncation and AMD64 zero-extended MOV width selection. Unused
LoongArch/RISC-V single-register paths retain their native diagnostics and
explicitly terminate if the configured NYI handler returns. ARM32 immediate
validation and materialization remain independent terminating dependencies.

Shared tail-jump placement preserves the non-Wasm enclosure and
spill/profiler/reload order without rewriting local register homes. Register
masks retain their integer or floating bank; the new floating-home regression
checks overlapping RDX/XMM2 bits and restoration to the ABI floating register.
X86 varargs remain stack-only, while Windows AMD64 restores unknown shadow-space
arguments inside the native GC-disabled interval.

Selected Windows controls pass 31 Debug and 29 Release cases; Linux controls
pass 18 in each configuration. The x86 diagnostic retains fourteen earlier
errors and zero warnings; its new controls remain unexecuted.
Four whole definitions and three exclusive declarations retire 275 native lines,
including headings. Other-target varargs bodies, parameter-stack type selection
and instruction dependencies remain native. These checks do not establish
other-target or generated-code parity.

## 2026-09-30: Whole argument placement and common scalar generation

Xarch argument placement retains x86 pushes, field-list padding, GC-slot order,
struct copies and register moves. SysV incoming stack arguments are selected
from parameter ABI segments. The shared register-placement caller retains its
non-Wasm target branches and Windows floating-varargs duplication; fixed x86
temporary-register masks preserve the native register sets.

Common catch arguments preserve the incoming exception root, move, root clearing
and produce order across targets. Register reuse retains the native instruction-
group boundary for an integral zero; the previously failing Linux dispatcher
case now passes in both configurations. Existing non-xarch recording helpers
remain terminating dependencies rather than duplicated overloads.

Twelve whole definitions and ten exclusive declarations retire 867 native lines
including headings and isolated guards. Selected Windows controls pass 85 Debug
and 79 Release cases; Linux controls pass 36 in each configuration. The x86 build
retains the exact fourteen earlier errors and zero warnings; new x86 controls
remain unexecuted. These are selected managed behavior checks, not x86 runtime
or whole-pipeline generated-code parity.

## 2026-09-30: Whole xarch frame initialization and call generation

Funclet prologs, epilogs and frame capture retain the separate x86 algorithms.
Block frame initialization preserves SIMD width, unaligned bounds, scalar
head/tail handling and loop thresholds. Call generation retains x86 stack
adjustment, signed caller-pop sizes, floating returns and Unix alignment
accounting. The shared pending-call-label helper preserves helper exclusions,
label publication and clearing order.

Fourteen whole definitions and one exclusive declaration retire 959 native
lines, including headings. Selected Windows controls pass 95 Debug and 78
Release cases. The x86 build retains the exact fourteen earlier errors and
zero warnings; new x86 controls remain unexecuted. Independent x86 unwind,
floating-return spill and instruction-recording dependencies still terminate.
These results establish selected managed behavior, not x86 execution or
generated-code parity.

## 2026-09-30: Whole xarch dispatch and profiling callbacks

The node dispatcher retains its complete x86 and SysV branches, including split
long operations, TLS restrictions and Swift-error dispatch. Profiling callbacks
retain x86 handle pushes, signed caller-pop sizes and Unix alignment accounting.
Shared stack helpers preserve unsigned arithmetic and the nested-alignment
high-water mark.

Seven definitions and one declaration are retired together. Windows controls
pass; Linux reaches the independent, still Windows-only register-reuse helper
in one selected case per configuration. X86 retains its fourteen earlier
declaration errors, so body compilation and callback execution remain unproved.
Swift-error generation and lower recording helpers remain independent work.

## 2026-09-30: Whole xarch epilog and callee saves

Root epilogs and integer/floating callee saves retain the complete x86 paths,
including double-aligned frame teardown, callee-pop returns and Unix hidden
return-buffer handling. AMD64 APX, OSR and unwind ordering remain intact.
AVX clearing preserves both target paths, including native empty/no-op decisions.
The existing LSRA policy now stores double-alignment state through the shared
code-generation contract rather than a terminating setter.

Eight xarch definitions, five complete inline accessors and five exclusive
declarations are removed together. Eighteen Windows controls pass in each
configuration and one Linux root-epilog control passes. X86 still has fourteen
earlier declaration errors; its three new state/no-op cases are not executed.
Other-target definitions, funclets and independent recording dependencies remain.

## 2026-09-30: Non-Wasm return helper closure

ARM64, LoongArch and RISC-V simple returns preserve ABI register selection and
extending moves for aliased narrow locals. ARMARCH SIMD splitting extracts
lanes in ascending order without overwriting an unread source lane. Swift moves
the error value before the normal return, and DEBUG stack checks retain the
whole xarch control flow.

Six whole native definitions and four declarations are removed together.
Focused final controls reuse authenticated baseline evidence rather than
rebuilding unchanged baselines. Four new Unix-x64 Swift cases pass; nineteen
preexisting Linux profiler assertions remain genuine failures. Other-target
builds still fail, and independent LoongArch/RISC-V recording dependencies
terminate. This is source completion, not new target execution or ABI parity.

## 2026-09-30: Whole shared struct-return callers

Struct-return classification preserves field-list precedence and the distinct
Windows-AMD64 policy; other targets, including Wasm, use the native struct-type
predicate. Non-Wasm generation retains register moves, reload/copy aliases,
spilled fields and Swift offsets. LoongArch/RISC-V loads use each descriptor's
field offset with native unsigned arithmetic, and SIMD splitting keeps its
feature guard. Descriptor layout and return-classification ABI are unchanged.

Two whole common definitions and the classification declaration are removed
from the residual tree. Six focused classification cases preserve old-source
behavior alongside existing return controls. Linux retains its nineteen
profiler dependency failures; other-target builds still fail. Wasm-specific
generation, ARMARCH SIMD splitting and lower emitter implementations remain
native, with explicit terminating managed boundaries. This caller closure does
not establish other-target execution or new generated-code parity.

## 2026-09-30: Whole shared patchpoint transfers

Regular and forced patchpoints retain argument moves, helper selection and all
five native target jumps. Xarch uses the jump that is not recognized as an
epilog; non-xarch records tail-call state after the helper and before the transfer
so future GC publication can disable unsafe return-address hijacking.

The whole caller, its declaration and two inline state accessors are removed from
the residual tree. Windows controls preserve both helper forms; Linux-target
controls now reach the existing helper and jump recording rather than the former
Windows-only gate. Other-target recording and GC-header publication still
terminate. Storing the state does not publish the non-xarch GC-header bit or
complete the target-specific emitter APIs, and is not new runtime parity.

## 2026-09-30: Whole shared return support

Return generation retains x86/ARM32 low/high pairs, floating and target-specific
simple-return dispatch, ARM soft-float/varargs transfers, and Wasm async
continuation clearing. Return GC roots are restored before profiler callbacks;
async clearing follows profiling, and DEBUG stack checks remain xarch-only.
Required return-register aliases and ARM instruction flags match the pinned
native definitions. Unported target helpers have typed terminating declarations.

Three whole common definitions and three declarations are removed from the
residual tree. Existing Windows controls preserve their behavior; Linux-target
controls retain nineteen genuine profiler dependency failures per configuration.
ARM32 resolves fourteen flag-binding diagnostics without adding errors; other
target diagnostic sets remain unchanged. Failed target builds, x64 fixtures
that exclude the x86 long-pair case, and retained emission dependencies do not
establish other-target execution or new generated-code parity.

## 2026-09-30: Shared async transfers and debug publication

Suspension returns and continuation values retain the complete native non-Wasm
algorithms: continuation-register moves, GC-root transfer and clearing only
GC-typed ordinary return values. Required x86, ARM32, ARM64, RISC-V and LoongArch
register aliases match their pinned native definitions. Debug publication keeps
location capture, allocation/copy order, EE ownership and diagnostics.

Three whole common definitions and the publication declaration are removed from
the residual tree. Wasm-specific transfer bodies and their shared declarations
remain, as do return-marking and instruction-emission dependencies. Existing
Windows and Linux-target controls preserve their results; the publication fixture
remains Windows-only and supplies no direct Linux publication coverage. This
source closure does not establish other-target execution or generated-code parity.

## 2026-09-30: Shared data-section output and diagnostics

Final data-section output and display retain the whole native target branches:
ARM Thumb tagging, Wasm ReadyToRun diagnostic-IP relocations, 32-bit absolute
addresses and assembler syntax, and 64-bit address forms. Raw bytes write only
the writable allocation alias; relative tables preserve unsigned offsets, and
removed async locations retain null diagnostic IPs.

Two whole definitions, their declarations and the now-shared inline hot/cold
offset helper are removed from the residual tree. Linux-target data-output
controls now execute rather than stopping at the Windows-AMD64 restriction.
Other-target instruction emission and runtime validation remain separate;
these Windows-hosted controls do not establish new generated-code parity.

## 2026-09-30: Shared async-resume metadata

Location recording, singleton table registration, state-handle generation and
emitter allocation retain native size/offset arithmetic, pointer alignment,
default locations, section linking and EE resumption-stub caching. The actual
two-pointer `CORINFO_AsyncResumeInfo` layout is unchanged.

Four whole definitions and four completed declarations are removed from the
residual tree. Linux-target controls now reach and exercise these helpers
instead of their former Windows-AMD64 gates. Final data-section materialization,
other-target instruction recording, suspension/continuation and patchpoint
bodies remain separate. These Windows-hosted controls do not establish Linux
runtime execution, other-target EE layout or new generated-code parity.

## 2026-09-30: Whole xarch transfer addresses

Nonlocal jumps retain register, local and indirect operands; function-entry
addresses bind to the first prolog group rather than the current body group.
Async-resume address generation preserves unsigned-32 state truncation,
pointer-sized LEA, relocation and result production. Existing AMD64 algorithms
and non-xarch failure branches are unchanged.

Three whole xarch definitions are removed from the residual tree. Shared
declarations and independent async table, handle, recording and emitter
dependencies remain. Linux-target nonlocal and function-entry controls now
execute, while async addresses reach the retained shared helper boundary.
These Windows-hosted controls do not establish Linux runtime execution or new
generated-code parity; x86 compilation and emission remain unresolved.

## 2026-09-30: Whole xarch register transfers

Physical-register reads preserve move, GC-state transfer and result production.
Local-register swaps retain enregistered home exchange, pointer-width `xchg`,
mixed-GC annotation and type-specific root restoration. Existing AMD64
algorithms are unchanged; the whole xarch bodies no longer have caller-level
Windows/AMD64 restrictions.

Two whole definitions are removed from the residual tree. Both shared
declarations remain for other-target bodies, as do independent scalar, async
and emitter helpers. Four Linux-target physical-register controls now execute
instead of stopping at the caller NYI. These Windows-hosted descriptor and
GC-state controls are not Linux runtime execution or new generated-code parity;
x86 compilation and emitter boundaries remain unresolved.

## 2026-09-30: Whole xarch switch tables

Table-switch dispatch and jump-table address generation retain the whole native
xarch algorithms. Dispatch loads a 32-bit offset relative to `fgFirstBB`, adds
that block's address at pointer width, then jumps indirectly. Shared table
emission preserves case order and duplicates, relative/absolute mode,
diagnostics and data-generation ordering, with the native non-Wasm enclosure.
Existing AMD64 behavior is unchanged.

Three whole definitions and the completed shared helper declaration are removed
from the residual tree. Caller declarations needed by other-target bodies and
independent async-transfer and scalar helpers remain. Linux data-section output
still terminates explicitly, and other-target build/emitter boundaries remain;
this source closure does not establish new runtime or generated-code parity.

## 2026-09-30: Whole xarch address generation and checks

Indexed addresses, LEA, null checks and bounds checks retain their native
x86/AMD64 branches. X86 indexing uses pointer-width comparisons, acquires a
temporary only for scales that cannot be encoded directly, and preserves the
signed 32-bit multiply immediate. AMD64 widening, instruction ordering and
base-register GC liveness are unchanged.

Four whole xarch definitions are removed from the residual tree. Shared
other-target declarations and independent indirect-load, SIMD and emitter
dependencies remain. X86 compilation and emission boundaries are still
unresolved; this source closure does not establish new runtime or generated-code
parity.

## 2026-09-30: Whole xarch local access

Local address generation, field and variable loads, and field and variable
stores retain their native x86/AMD64 control flow. The x86 SIMD12 variable-load
branch uses the existing twelve-byte helper; 32-bit long stores dispatch to
their separately retained helper before SIMD or ordinary stores. Existing
Windows AMD64 and SysV x64 behavior is unchanged.

Five whole xarch definitions are removed from the residual tree. Shared
other-target declarations and independent SIMD12 and long-store definitions
remain. The long-store dependency terminates explicitly, and x86 emitter and
build boundaries remain unresolved; this source closure does not establish
x86 execution or new generated-code parity.

## 2026-09-30: Xarch struct argument copy closure

Unrolled and REP-MOVS struct copies retain the native x86/AMD64 branches.
Eight-byte x86 chunks use XMM registers and MOVQ; stack stores retain the
push-versus-SP-relative distinction, and REP destinations start at SP on x86.
Windows AMD64 behavior is unchanged. Four whole bodies and four declarations,
including the required eight-byte move and stack-store helpers, retire 219
native lines in `7c5dcedf`.

Windows old/final controls pass 39 Debug and 37 Release cases, including three
new preservation cases. A fresh combined snapshot at `9884fded` passes 98/90,
the exact disjoint union of struct-copy and paired-LSRA controls. Linux retains
36/34 caller-level Windows-AMD64 NYIs, with identical identities, messages and
origins; it does not execute these copies. Paired x86 diagnostics retain the
same fourteen build errors. Non-xarch expansion differs only by a blank line;
all nonblank lines match across forty target/feature combinations.

Evidence: `artifacts/xarch-struct-putarg-5bd5cf8`, `work-st-v2` and
`final-integrated`, with exact archives, inventories, commands, ten actual TRXs
and paired raw x86 diagnostics independently checked. The initial four fixture
IDE0048 failures remain preserved. The generic `codegenlinear.cpp`
`genConsumePutStructArgStk` body and declaration remain: its current managed
partial is xarch-only, not a complete translation for every non-Wasm target.
The x86 push helper, stack-argument callers and independent push/partial-REP
bodies also remain. No native runtime or generated-code parity claim.

## 2026-09-30: Paired-register assignment and spilling

LSRA assignment, spilling, lifetime and selector helpers retain ARM32's
overlapping float/double register bookkeeping. Pair lookup, availability,
next-reference/spill costs and constant masks account for both halves.
Unassigning the odd half still reaches the even home register's active-interval
spill and previous-interval restoration rather than taking the temporary-copy
early return. Existing single-register behavior and its private accessor
contracts are preserved.

Twenty complete CPP definitions, eight inline header bodies and twenty
associated declarations retire 862 native lines in `ee70e896`. The independent
`isMatchingConstant` and `getRegisterType` bodies and declarations remain.
`getMatchingConstants` was already absent and is not restored or counted again.
Windows old/final direct controls pass 59 Debug and 53 Release cases; integration
passes 102/89. Linux integration passes 97/84, while its direct controls retain
the same three `Interval.isUpperVector` reflection failures per configuration.
ARM64 controls fail compilation on identical baseline errors; all five other
target diagnostic pairs also match and remain failed builds.

Evidence: `artifacts/lsra-paired-assignment-3d2f285`, `final-v3`, with an immutable
2,503-file archive and exact 2,504-file overlays. The parent independently
reconstructed all eight parser masks and verified thirty complete commands,
receipt timing/memory floors, sixteen actual TRXs and fourteen raw diagnostic
runs through the versioned proof. Initial duplicate-helper and accessor failures
remain preserved. ARM32 constructor/build boundaries still prevent paired
register execution; these preservation controls and source retirements do not
establish native runtime or generated-code parity.

## 2026-09-30: Whole xarch block emission

Memmove, block stores, unrolled/loop initialization and unrolled copies retain
the native scalar/SIMD ordering and x86 tail loops. Windows AMD64 instruction
paths are preservation work; the x86 branches are now source-complete, and
Linux x64 reaches the same selected descriptor-generation paths without the
old Windows-only caller guard. Non-xarch methods retain their original typed
failure branches rather than compiling calls to xarch-only helpers.

Five definitions and their headings retire 747 native lines in `c8c49205`.
Independent struct PUTARG bodies, load-offset callers and shared declarations
remain. Windows old/final controls pass 51 Debug and 49 Release cases.
The old Linux caller guards fail those same cases; final passes all 51/49.
The new five-method fixture is a Windows preservation control, not an
old-source algorithm regression.

Evidence: `artifacts/xarch-block-emission-dae5831`, `work-guard-v2`, anchored
to the fresh `bbc8e2c` 2,501-file archive. Exact original/versioned inventories,
eight reused TRXs, exact commands, raw-log provenance, parser masks and the
approved x86 XMM constant are independently verified. Guard-only corrections
preserve complete preprocessed Windows/Linux AMD64 and x86 source; non-xarch
source matches the original typed stubs. Four fresh diagnostic sets match
their baselines; the unchanged x86 pair is verified from its original logs.
All five target pairs remain failed builds. These managed descriptor controls
do not establish native runtime or generated-code parity.

## 2026-09-30: Whole shift and rotate lowering

Shift-count mask removal uses the operand's native width, including unsigned
64-bit types, and preserves containment and LIR ownership when removing the
AND and mask nodes. Rotate lowering retains ARM32-width constant arithmetic,
ARM/LoongArch rotate-left conversion and RISC-V/Wasm containment calls.
Six definitions and headings retire 196 native lines, with three declarations
bringing the batch to 199 lines in `35eb6e97`. The xarch rotate definition was
already absent and is not counted again; target containment and Zba helpers
remain native dependencies.

The corrected old Windows fixture passes five cases and fails only the
unsigned-long count-32/mask-31 case. Final Windows controls pass 45 Debug and
44 Release cases; Linux passes six each and ARM64 passes 183 each. The unchanged
old ARM64 rotate fixture passes four cases, all retained in final controls.
Five paired target diagnostic sets match and remain failed builds.

Evidence: `artifacts/lower-shift-rotate-62d0336`, `work-v2`, with a fresh
2,499-file archive, exact 2,501-file final/control inventories, eight actual
TRXs, exact commands and byte-reconstructed diagnostic masks independently
verified. Rejected v1 constructor failures are preserved; v2 initializes
CodeGen before constructing LinearScan and changes no production algorithm.
Other-target dependency/build limits remain explicit; no native runtime or
generated-code parity claim.

## 2026-09-30: Whole xarch frame setup, probing and localloc

Frame setup, local-stack allocation, constant/dynamic stack probing and localloc
retain the native x86 and AMD64 branches, including secret-argument preservation,
hidden x86 stack adjustments, outgoing-area handling and the x86 localloc SP
save. Probe controls cover the EE-provided page size and exact page boundaries.
The Windows AMD64 algorithms are preservation work; the batch completes the
other-target branches and removes caller-level platform exclusions.

Seven definitions and their headings, plus four xarch-specific declarations,
retire 568 native lines in `6c77d2bd`. Shared ARM/ARM64 declarations, non-xarch
overloads, AVX-state clearing and independent emitter/unwind helpers remain.
Windows old/final controls pass 57 Debug and 55 Release cases. Linux improves
from 23/27 passing Debug/Release cases to 50/52 without losing an old pass;
the seven Debug and three Release failures retain their old messages and
origin frames. They are dependency boundaries, not Linux parity.

Evidence: `artifacts/xarch-frame-probing-62d0336`, `work-v2`, with the immutable
2,499-file baseline, exact snapshot inventories, eight actual old/final TRXs
and five identical failed diagnostic pairs independently verified. Both
diagnostic sides explicitly include the approved x86 constants and eight
parser masks. Rejected v1 fixture and IDE0058 failures are preserved; v2 changes
only the native-int assertion and three explicit ignored Boolean results.
No native runtime or generated-code parity claim.

## 2026-09-30: Whole DEBUG register stress limiting

Register stress limiting retains the native callee-bank constraints, minimum
candidate counts, busy-register filtering, edit-and-continue handling and fixed
references across the translated target branches. Unix AMD64's small integer
set uses R12/R13 rather than the Windows RSI/RDI pair. One definition, heading
and declaration retire 95 native lines in `188a735b`; the already-retired
constraint helper is not counted again.

The corrected Linux fixture fails only the ABI small-set case on old production
and passes all five cases on final production. Windows old/final focused controls
pass five cases each. Linux integration passes 96 old/final Debug cases; ARM64
passes 110 each, plus its focused stress case. Final Windows integration passes
101 Debug and 88 Release. Both owned deltas are entirely DEBUG-only; Linux and
ARM64 Release controls were not rerun. Five paired target diagnostic sets are
identical and remain failed builds.

Evidence: `artifacts/lsra-stress-limiting-62d0336`, `final-v2`, with a fresh
2,499-file archive, exact snapshot inventories, 12 actual TRXs, same-snapshot
Windows binary reuse and paired diagnostic overlays independently verified.
The rejected first fixture's extra failure was missing caller-trash setup;
its evidence is preserved. These are managed target controls, not native
runtime or generated-code parity.

## 2026-09-30: Whole control-flow-guard call lowering

CFG call lowering preserves validated target expressions, VSD cloning,
validator placement and late dispatch-argument ownership. The complete native
target-register branches now remain in C#, including native-unreachable
x86/ARM32 dispatch handling. One definition, heading and declaration retire
216 native lines in `f7a7c3bf`; independent shift and rotate bodies remain.
The AMD64/ARM64 paths were already translated; this batch completes the
other-target branches rather than claiming new Windows CFG capability.

Old Windows/ARM64 Debug preservation controls pass 7 cases each.
Final Windows controls pass 27 Debug/27 Release, ARM64 passes 9/8, and Linux
passes 7/7. The ARM64 Release selector correctly excludes its Debug-only
dispatch fixture. Five isolated target diagnostic sets match and remain
failed builds. Evidence: `artifacts/lower-cfg-62d0336`, final `work-v1`,
with five exact 2,499-file snapshots, eight actual TRXs and paired diagnostic
overlays/logs independently verified. Newly translated target branches remain
unexecuted behind build/dependency boundaries; no native runtime or
generated-code parity claim.

## 2026-09-30: Whole general and copy register assignment

General allocation preserves ARM32 double-register spill selection and
previous-interval bookkeeping. `isAssigned` checks the second half of a double
when the chosen even register is empty. Independently unported paired-register
helpers remain terminating dependencies, not omitted caller branches.
The existing general and minimal copy-assignment bodies preserve the interval's
home register, related interval and active state after creating a temporary copy.

Four definitions, templates, headings and declarations retire 239 native lines
in `7593d46f`: two newly completed definitions account for 137 lines and two
already-translated copy-assignment bodies account for 102 lines of retirement
catchup. Linux integration passes 97 Debug/84 Release and Windows passes
102/89; direct Windows/Linux controls pass 24/23. ARM64 passes 110/91.
The new minimal-copy fixture passes old production and final; all earlier
selected passing identities remain unchanged. Five isolated target diagnostic
sets match and remain failed builds.
Evidence: `artifacts/lsra-register-assignment-3f9200d`, final `final-v2`,
with 2,499-file snapshots, 26 actual TRXs and paired diagnostic overlays/logs
independently verified. ARM32 double allocation remains source-only behind
target build failures and retained pair helpers; no generated-code parity claim.

## 2026-09-30: Whole tailcall lowering

Tailcall lowering preserves profiler-hook placement, argument mark clearing,
overlapping incoming/outgoing stack copies, defensive temporary ownership and
GC-free-region ordering. ARM32 long rehoming retains the low/high field
construction and normal node-lowering dependency. The x86 JIT-helper path
preserves placeholder replacement order, helper flags and stack-word counts.
Five definitions, headings and declarations retire 499 native lines in
`e1622d06`; CFG and independent helper dependencies remain retained.

Windows controls pass 58 Debug/58 Release. Linux has 56 passing/2 failing in
both configurations, with exactly the previously accepted outgoing-area
failure identities and messages. ARM64 controls pass 15/15. Old-production
tailcall preservation controls pass 13 cases on Windows/Linux Debug and ARM64
Debug/Release; all selected previously accepted argument cases remain unchanged.
Five isolated target diagnostic sets match and remain failed builds.
Evidence: `artifacts/lower-tailcalls-9fea404`, final `work-v2`,
with 2,499 source files, ten current TRXs and six reused accepted argument TRXs
independently verified. x86 helper and ARM32 execution remain blocked by target
dependencies/build failures; these controls do not establish generated-code parity.

## 2026-09-30: Whole minimal register selection and allocation

Minimal selection retains native assertion and heuristic behavior, ARM32
double-register busy filtering, and fixed-reference conflict lookup in the
correct register bank. A mask-register conflict regression proves that a busy
high-bank register is excluded: the new test fails old production and passes
final in both Linux-target configurations. All existing direct cases remain
passing. The general selector shares the same double-half mask arithmetic.

Two definitions, headings and declarations retire 239 native lines in
`18db2a26`, plus two trailing EOF blanks. Newly completed minimal selection
accounts for 202 lines; the previously translated, unchanged minimal allocation
body accounts for 37 lines of retirement catchup. Independent paired-register
assignment and stress helpers remain retained.
Linux integration passes 96 Debug/83 Release and Windows passes 101/88.
Direct Windows/Linux controls pass 28/27; ARM64 integration passes 110/91.
Five isolated target diagnostic sets match and remain failed builds.
Evidence: `artifacts/lsra-minimal-selection-b3aa70b`, final `final`,
with 16 actual TRXs and exact source/diagnostic-overlay verification.
ARM32 paired-register behavior remains source-only; these controls do not
establish target runtime or generated-code parity.

## 2026-09-30: Whole call-argument lowering

Call-argument lowering retains all target branches, split field-list owner
chains and placement order. x86 IJW copy-helper insertion and three compiler
argument-marker helpers preserve native placeholder and lowering order.
Eight definitions and associated headings/declarations/enclosures retire 627
native lines in `0e571e3d`: six newly completed definitions account for 289
lines; two already-translated split/legalization bodies account for 338 lines
of retirement catchup. Tailcall, CFG and independent stack-argument helpers
remain retained.

Windows controls pass 45 Debug/45 Release; old/final Linux results are 43
passing/2 failing in both configurations with identical failure messages.
ARM64 passes 18/18, including early/late split-owner preservation cases.
An introduced x86 `sealed partial Compiler` declaration was corrected:
the corrected isolated diagnostic set returns from 88 errors to the same 14
baseline errors. The other four target error sets also match and remain
failed builds. x86 special-copy execution remains blocked by those existing
target failures.
Evidence: `artifacts/lower-call-arguments-50e4df9`, final `work-v3`,
with eight actual TRXs and exact source/diagnostic-overlay verification.
Unaffected x64 `work-v1` and ARM64 `work-v2` results are reused; the last
delta is exclusively an x86 IJW declaration correction.
These controls do not establish target execution or generated-code parity.

## 2026-09-30: Whole general register selection

The general register selector preserves all target branches and heuristic
ordering, including x86 byte-register preferences and ARM32 double-register
busy masks. Its former Windows/ARM64 caller gate is removed.
One whole definition, template, heading and declaration retire 477 native lines
in `47ee3737`; minimal selection and independent target helpers remain retained.

All five previously gated Linux integration cases now pass, yielding 96/96
Debug and 83/83 Release. All fifteen previously gated direct cases also pass:
23/23 in both configurations. Exact old/final test identities match.
Windows integration controls pass 101/88 and direct controls 23/23; ARM64
integration passes 110/91. Five isolated target diagnostic sets match and remain
failed builds. Evidence: `artifacts/lsra-register-selection-1e0ae1a`, final
`final`, with 14 actual TRXs and exact source/diagnostic-overlay verification.
These Windows-hosted controls do not establish Linux runtime or generated-code parity.

## 2026-09-30: Whole register allocation traversal

Optimized and minimal allocation traversal retain all target branches, ARM
double-register bookkeeping and the 32-bit GC `this` postpass. Lower-bank mask
removal now preserves other banks without using members absent on single-bank
targets. Two whole definitions, template guards, headings and declarations
retire 1,959 native lines in `b99e9953`; register selection, ARM32 pair lookup and
ARM64 consecutive-allocation dependencies remain independent.

Windows integration controls pass 101 Debug/88 Release and direct controls
65/52; ARM64 integration passes 110/91. Linux integration retains 91 passing/5
failing Debug and 78/5 Release, but the five failures now reach the separately
retained `RegisterSelection.select` boundary. Direct Linux controls add one
mask-preservation pass in each configuration, reaching 28/15 Debug and 20/15
Release; all fifteen failure identities and messages match the baseline.
The mask test also passes on Windows in both configurations.
Five isolated target diagnostic sets match and remain failed builds.
Evidence: `artifacts/lsra-allocation-traversal-7270c48`, final `final-v2`,
with 14 actual TRXs and exact snapshot/diagnostic-overlay verification.
ARM32 pair fixtures remain source-only; these controls do not establish
target execution or generated-code parity.

## 2026-09-30: Whole call and PInvoke lowering

Method-jump, direct-call and PInvoke lowering preserve frame publication and
GC-transition ordering across targets, including x86 stack-byte/SP bookkeeping
and 32-bit method-exit unlinking. ARM call-range checks retain their codegen
dependency through `ICodeGen.validImmForBL`; the ARM32 relocation/AOT and ARM64
jump-stub contracts are translated in `instr/CodeGen.CallTargets.cs`.
Twelve definitions and associated headings/declarations retire 743 native lines
in `93a65445`. Argument, tailcall and control-flow-guard helpers remain independent.

Windows controls pass 147 Debug/139 Release and ARM64 passes 23/23. Exact-class
Linux Debug controls improve from 78 passing/6 failing to 83 passing/2 failing:
four method-jump cases now run and one publication-order case is new.
Linux Release has 75 passing/2 failing cases; the two Windows-specific outgoing
area expectations match the baseline failures. Five isolated other-target
diagnostic sets match but remain failed builds. An initial broad Linux selection
included an unrelated crashing struct-return fixture; its aborted result is
preserved, not counted as a completed control.
Evidence: `artifacts/lower-call-pinvoke-f212bd2`, final `work-v4` and unaffected
`work-v2` x64 results. These source/IR controls do not establish generated-code parity.

## 2026-09-30: Whole register edge resolution

Edge traversal, register-map reconciliation, move/swap insertion, scratch
selection and ARM double-register cycle handling retain the native ordering
and target branches. Regression controls cover source moves before destination
reloads and exclusion of live and terminator-consumed scratch registers.
Seven definitions, headings and declarations retire 1,255 native lines in
`db978a4c`; independent allocation, critical-edge and register-state helpers remain.

Windows integration passes 101 Debug/88 Release, direct edge controls pass
30/13, and ARM64 integration passes 110/91. Linux integration improves from
82 to 91 passing cases out of 96 Debug and from 70 to 78 out of 83 Release.
Direct Linux edge controls fix 13 Debug/12 Release baseline failures and add
two Debug/one Release cases. Five allocation-traversal failures remain, as
does one baseline-identical Linux Debug stress expectation that assumes
Windows XMM6 callee-save status. No old passing case regresses.
Five parser-unblocked target diagnostic sets match; these remain failed builds.
Evidence: `artifacts/lsra-edge-resolution-2869bc1`, immutable `final-v3`,
16 actual TRX runs and separately verified diagnostic overlays.
These are source/IR controls, not generated-code parity.

## 2026-09-30: Whole lowering traversal and phase setup

Node, block, phase and containment traversal preserve target dispatch, cursor
ownership, long-decomposition dependencies and phase ordering. Outgoing argument
space retains native unsigned arithmetic and frame-pointer thresholds.
The block-entry diagnostic now checks semantic statement emptiness instead of
the unrelated LIR range. Eight definitions, headings and declarations retire
735 native lines in `8b2d36a2`, with three trailing blank lines also removed.
Independent 32-bit decomposition, RISC-V shift and Wasm helpers remain retained.

Windows controls pass 226 Debug/219 Release and ARM64 passes 257/257. Eight
formerly gated Linux phase/traversal cases pass in each configuration; broader
Linux method-jump, ABI-fixture and liveness failures remain separately scoped.
Final Debug controls pass four cases covering empty and nonempty non-LIR blocks,
with unaffected prior results reused. The unsigned-size fixture fails on old
production code and passes after correction. Other-target raw and isolated
semantic diagnostic sets match but do not establish successful builds.
Evidence: `artifacts/lower-traversal-phase-1c17c5e`, immutable `work-v7`.
These are source/IR controls, not generated-code parity.

## 2026-09-30: Whole local and block register resolution

Block-start/end location tracking, local-reference resolution and copy/reload
insertion preserve predecessor and exception-edge maps, register-state ordering
and owner-aware LIR replacement across targets. ARM live masks include both
halves of double registers; unported paired-register operations terminate at
their typed helper boundaries rather than replacing caller algorithms.
Four definitions, headings and declarations retire 857 native lines in
`d4e8570d`; independent edge-resolution, allocation and ARM helpers remain.

Final Windows controls pass 101 Debug/88 Release and ARM64 passes 110/91.
Linux integration improves from 76 to 82 passes out of 96 Debug cases and
from 67 to 70 out of 83 Release cases; remaining failures reach retained
edge-resolution or allocation helpers. Direct-family controls gain 28/27
passes, retaining three unchanged Windows-only fixture expectations.
No old passing case regresses. Other-target raw and parser-unblocked diagnostics
match but still prevent successful builds. Evidence:
`artifacts/lsra-local-resolution-c8eee52`, immutable `final-v2`.
These are source/IR controls, not generated-code parity.

## 2026-09-30: Whole comparison and conditional lowering

Shared comparison, branch, select and condition-code lowering retain all target
branches. The 32-bit long-comparison decomposition preserves wrapping constant
adjustment, signed/unsigned boundaries, flag ordering, successor cursors and
owner-aware replacements with cleared value numbers. RISC-V bit-test transforms
are preserved; independent target helpers and branch overrides remain retained.
Seven native definitions, headings, guards and declarations retire 1,120 lines
in `237f75d1`; `LowerJTrue` retains its declaration for unported target overrides.

Windows controls pass 82 Debug/81 Release, ARM64 passes 182/182, and Linux-target
Debug passes 82. Unaffected evidence is reused for the final 32-bit corrections.
Other-target raw diagnostics match the old source. Isolated parser-unblocked
x86/ARM32/RISC-V probes also match, with no owned comparison diagnostics, but
independent baseline errors prevent execution of the new long-comparison code.
Evidence: `artifacts/comparison-conditions-add9598`. These are source/IR controls,
not generated-code parity.

## 2026-09-30: Whole register-resolution and allocation dispatch

The allocation phase dispatcher preserves all target branches and phase order,
including the ARM64 consecutive-register path with its retained terminating
allocator dependency. Both register-resolution modes preserve local/frame/spill
ordering, xarch SIMD AVX-state bookkeeping and ARM parameter diagnostics.
The two native definitions and their declarations retire 743 lines in `151ef319`.

Initial Windows controls pass 97 Debug/86 Release and ARM64 passes 110/91.
Linux improves from 41 to 74 passes out of 92 Debug cases, and from 33 to 65
out of 81 Release cases, without losing an old passing case. Remaining failures
reach retained allocation, block-location or copy/reload helpers. Final focused
Windows controls pass 54/44; unaffected evidence is retained.
Other-target old/final compiler diagnostics match, including isolated probes
past shared parser blockers; those targets still do not build successfully.
Evidence: `artifacts/lsra-resolution-dispatch-62d8205`. These are source/IR
controls, not generated-code parity.

## 2026-09-30: Whole block-store lowering

Block copy and initialization preserve GC-pointer atomicity, stack-copy
unroll limits, target zero-register containment and Wasm memory opcodes.
Common lowering and scalar-store replacement retain owner-aware node
replacement and lowering order. Five definitions, including the separate
Wasm initialization body, retire 361 native lines in `a2168066`.
Unported target containment and Wasm multiply-use helpers remain retained.

Windows controls pass 26 Debug/26 Release; ARM64 passes 78/78. Final Debug
runs include the native assertion split; unchanged Release evidence is reused.
Wasm, RISC-V and LoongArch64 old/final builds stop at identical baseline
diagnostics. Evidence: `artifacts/block-store-family-7e7da03`.
These are IR preservation controls, not generated-code parity.

## 2026-09-29: Whole integer division and remainder lowering

Signed and unsigned constant division retain target-width conversion, 32-bit
long exclusions, multiply-high availability and target-specific magic selection.
The Wasm dispatcher preserves exception-check operand reuse and diagnostic
arguments; its multiply-use helper remains a terminating dependency. Five shared
definitions and the separate Wasm dispatcher retire 690 native lines in
`01f7e85f`.

Windows controls pass 44 Debug/44 Release; ARM64 passes 179/179. Final Debug
runs include restored assertions, with unaffected Release evidence reused.
Old Windows/ARM64 controls pass; RISC-V, LoongArch64 and Wasm old/final builds
stop at identical baseline diagnostics. Evidence:
`artifacts/integer-divmod-47f3cb8`. These are preservation controls, not
generated-code parity.

## 2026-09-29: Whole candidate construction and frame selection

Both candidate-construction modes preserve x86 double-alignment policy and ARM
diagnostics. Shared candidate construction is no longer restricted to Windows
x64/ARM64, and frame selection retains target reservation order and assertions.
EH dataflow support, the candidate template and frame selection retire 560
native lines in `67ab3db3`; unported target policy helpers remain native.

Windows controls pass 67 Debug/63 Release and ARM64 88/70, including four direct
frame-reservation cases per configuration. Thirteen Linux candidate cases that
failed at the old target gate now pass; the candidate family passes 17 Debug/16
Release. Eighteen broader Linux failures remain unchanged allocation/resolution
NYIs. x86 alignment fixtures remain source-only behind baseline syntax failures.
Evidence: `artifacts/lsra-candidate-frame-8363a2f`. No generated-code parity is
claimed.

## 2026-09-29: Whole address-mode construction and add lowering

Address-mode construction retains the common algorithm on every target and
RISC-V's explicit base-plus-index addition. Add lowering preserves the RISC-V
Zba helper sequence and Wasm binary-arithmetic call; those unported dependencies
remain terminating managed boundaries and retained native definitions. The two
completed callers and declarations retire 398 lines in `56370b03`.

Windows and Linux-target controls pass 190 Debug/190 Release each, and ARM64
passes 34/34. Old Windows and ARM64 Debug controls also pass; these are
preservation results. RISC-V and Wasm old/final builds stop at identical baseline
diagnostics before tests. Evidence: `artifacts/address-mode-add-770748d`.
No generated-code or other-target execution parity is claimed.

## 2026-09-29: Whole indirect-load and floating-store retyping

Indirect loads preserve target-specific ordering between unused-load conversion
and address containment, including ARM64 volatile floating-load bitcasts.
Unused-load conversion and floating-store retyping retain every native target
branch, signed-zero policy and access width. Three definitions and declarations
retire 237 native lines in `f4bd7f46`; independent address-mode and target
containment helpers remain native.

Windows 85/85, ARM64 78/78 and Linux 90/90 controls pass in Debug/Release.
The previously memory-stopped Linux Release run passes in a fresh snapshot with
the six user-authorized analyzer exclusions: 36.31 seconds, at least 45.46 GiB
free memory. Earlier unaffected results are retained, not relabeled as new runs.
Evidence: `artifacts/indir-floating-retyping-73b7e9b`. These are translation and
preservation controls, not generated-code parity.

## 2026-09-29: Whole interval and reference construction

Both interval-building specializations retain native target branches, including
x86 frame alignment and varargs scratch kills. Minimal construction again
activates initial-parameter stress even without candidate parameters, and the
EH heuristic restores its floating-register assertion (B473). Initial-definition
and reference-association support is also complete. Ten definitions, template
instantiations and declarations retire 1,224 native lines in `c69394bf`.

Final Windows controls pass 120 Debug/106 Release. The focused old Debug run
fails stress activation and passes liveness formatting; final passes both.
Earlier ARM64 85 Debug/67 Release and Linux 22 Debug/15 Release controls precede
the final Debug correction. Three broader Linux resolution NYIs and four x86
syntax errors remain baseline limitations. Evidence:
`artifacts/lsra-build-intervals-dd9fc73`. No generated-code or other-target runtime
parity is claimed.

## 2026-09-29: Whole common indirect-store lowering

The common store caller now retains Wasm's write-barrier dispatch and all
non-xarch mutable-object publication branches, without replacing the whole
caller with a target guard. Retyping, address construction, write-barrier
handling, coalescing and target dispatch keep native ordering. The function,
heading and declaration retire 58 lines in `d57f87d2`; independent helper
definitions remain native.

Full-analysis controls pass Windows 46/46, Linux 46/46 and ARM64 78/78 in
Debug/Release. The new write-barrier case is a preservation control. Wasm
compilation remains blocked by five unchanged baseline syntax diagnostics;
no target execution or generated-code parity is claimed. Evidence:
`artifacts/store-indir-common-8bc9635`.

## 2026-09-29: Whole mixed-store coalescing

Local and indirect coalescing now retain the complete shared native algorithm,
including ARM64 atomic-pair rules, load metadata, unsigned access sizes and
unsupported-target no-op behavior. SIMD widening clears the whole constant
payload, temporary insertion follows native ordering, and both stores retain
the write-barrier assertion (B472). Seven top-level functions, declarations and
the private data record retire 891 native lines in `81049525`; the common
indirect-store caller remains native.

Full-analysis controls pass Windows 117/117, ARM64 78/78 and Linux 122/122 in
Debug/Release. The final fixture-only refinement directly checks null load values
and unsigned sizes, passing both Windows cases in each configuration. Earlier
versions fail seven optimized ARM64, two data-helper and two inactive-payload
cases. Evidence: `artifacts/lower-coalescing-e3c557a`. These establish native
contracts, not generated-code differences or other-target runtime parity.

## 2026-09-29: Shared node-reference and physical-register construction

Node-reference traversal now preserves all-target entry, xarch contained-local
handling, target-specific stress masks and ARM64's consecutive-definition
exemption (B471). The existing physical-register initializer retains native
register-bank ordering. Two functions, their declarations and eight associated
register-order array/size definitions retire 276 native lines in `2102dee8`.

ARM64 controls pass 84 Debug/66 Release; old Debug fails the two corrected stress
cases. Windows node/reference/physical-register controls pass 20 Debug/13 Release,
and Linux Debug controls pass 18. Physical-register acceptance reuses unchanged
full-analysis binaries. Paired x86 builds stop at four existing syntax errors;
other-target dependencies remain explicit. Evidence:
`artifacts/lsra-node-traversal-e6a812b`. No target runtime or generated-code parity
is claimed.

## 2026-09-29: Whole xarch indirect loads and stores

Indirect load/store generation now retains the x86 TLS segment, byte-swap and
long-intrinsic branches as well as AMD64 behavior. Two definitions retire
359 lines in `8bc4e5b9`; shared declarations and the independent optimized
x86 write-barrier helper remain native.

All 63 old Linux indirect-store guard failures pass. Full-analysis focused
controls pass 343 Debug/327 Release on both Windows and Linux targets.
Paired x86 builds retain four pre-existing syntax diagnostics, preventing the
two new x86 fixtures from running. Evidence: `artifacts/xarch-indirect-9e4b720`.
Target runtime execution and generated-code parity remain unverified.

## 2026-09-29: Local-candidate and upper-vector construction

The two candidate checks and three upper-vector interval/save/restore builders
retain their whole native bodies. The save builder now restores the omitted
Debug block-liveness subset diagnostic (B470), and struct-local classification
uses the native descriptor overload. Five definitions, declarations and their
feature enclosure retire 314 lines in `93423e1a`.

Full-analysis Windows controls pass 80 Debug/76 Release across five construction
fixtures. Old-source controls pass 3/4, failing only the missing diagnostic;
normalized struct-local classifications are preservation cases, not newly found
behavioral bugs. Evidence: `artifacts/lsra-local-vector-construction-0860136-v3`.
No generated-code divergence or other-target execution is claimed.

## 2026-09-29: Whole xarch shift generation

Scalar, split-long and read-modify-write shifts retain their whole x86/AMD64
branches. Three definitions and the xarch-only declaration retire 250 lines
in `4a79d031`; native ARM and other-target declarations remain.

Full-analysis preservation controls pass Windows 415 Debug/399 Release and
Linux 350 Debug/337 Release. Old Linux shifts already pass 39/39; broad old/final
runs retain the same 63 indirect-store guard failures. Both x86 builds stop at
the same four pre-existing syntax diagnostics before compiling shifts, so the
four new x86 cases have not executed. Evidence: `artifacts/xarch-shifts-0860136`.
This establishes translation and preservation, not x86 execution or code parity.

## 2026-09-29: Shared throw-helper and overflow generation

The whole throw-helper and overflow functions preserve Unix-x86 funclet
inline throws, stack-depth assertions and ARM jump selection. Typed terminating
jump/helper-call dependencies remain for other targets. Two definitions and
declarations retire 140 lines in `6b4cd26d`.

All 22 Linux-target cast cases failing the old Windows-only guard pass.
Full-analysis focused controls pass Linux 352 Debug/340 Release and Windows
378 Debug/365 Release; ARM64 Core compiles in both configurations. Broad Linux
Debug retains one baseline SIMD16 helper-return failure (377/378), reproduced
on old and final snapshots and tracked as B469. Passing Linux selections exclude
that helper-call fixture class. Evidence: `artifacts/xarch-throw-helpers-6a23316`.
These are Windows-hosted target controls, not Linux or ARM64 runtime parity.

## 2026-09-29: Shared local-store lowering and struct-result spilling

`LowerStoreLocCommon` and `SpillStructCallResult` now retain their whole native
target branches. ARM64 local-store coalescing preserves its optimization-disabled
early return, while the optimized dependency remains explicitly unported and
its native body remains. Two definitions and their declarations retire 263 lines
in `77c21b36`.

Full-analysis controls pass Windows 87 Debug/87 Release, Linux 66 Debug/66 Release
and ARM64 71 Debug/71 Release. Final Windows Debug repeats 87 cases after removing
a redundant owner assignment; `LIR.Range.ReplaceNode` already replaces the owning
use. The new identity/LIR test is therefore a preservation control, not a defect
fix. Other results precede that redundant-write removal; ARM64-selected code is
unchanged. Evidence: `artifacts/lower-local-spill-c84e404-isolated/manifest-v5.json`.
Other-target execution and generated-code parity are not established.

## 2026-09-29: Shared LSRA kill-set helper closure

The six remaining arithmetic, block, intrinsic and profiler kill-set helpers
retain their complete native target branches, including x86 multiply-long
classification and ARM void-return profiler kills. Their bodies, headings and
declarations retire 164 native lines in `1c56e6d4`.

Full-analysis Windows controls pass 75 Debug/74 Release and ARM64 target
controls pass 80 Debug/65 Release. Five additional arithmetic cases preserve
ARM64's native no-fixed-register masks. These are preservation controls,
not old-failing regressions or target generated-code parity. Evidence:
`artifacts/lsra-killset-helpers-fc74a22` and corresponding retirement records.

The already-translated kill-position builder and interval-preference update
also retire their two definitions and declarations, 134 lines in `81e9df83`.
Their unchanged source and fixture hashes match the same Windows evidence,
including GC-reference kills and call-register preferences; no behavior changes
or additional compiler runs were needed.

## 2026-09-29: Shared LSRA kill-set construction

Shift/rotate, call and debug node kill-set construction retain the native
target branches and dependent helper calls. Acceptance review also corrected
an ARM64 port defect: calls without floating-register kills must remove the
predicate bank too (B468). Three definitions and their declarations retire
198 native lines in `b735522c`; independent helper bodies remain.

Corrected ARM64 controls pass 75 Debug/65 Release. Identical old-source
controls fail both no-floating-use call-mask expectations, passing 1/3.
Windows 53 Debug/52 Release and Linux 26 Debug/25 Release remain applicable
because the correction changes only ARM64-selected text. X86 retains 14
baseline diagnostics. Evidence: `artifacts/lsra-killset-correction-c84e404`
and the `lsra-shared-killset` retirement records. No new generated-code parity
claim.

## 2026-09-29: Shared multi-register local storage

Multi-register local stores preserve field-by-field liveness, Swift and
LoongArch64/RISC-V offsets and 32-bit bounds. Xarch SIMD assembly handles
copy/reload substitutions and destination aliasing without losing either
half. Two definitions and the common declaration retire 301 native lines in
`0d914d38`; the independent ARM SIMD helper remains.

Windows controls pass 83 Debug/81 Release and Linux-target controls pass
89 Debug/87 Release. All eight identical old Linux cases fail at the former
production guard. ARM64 core builds pass; x86 retains the same 14 diagnostic
identities. Existing Windows unsupported-SIMD rejection is unchanged.
Evidence: `artifacts/shared-multireg-stores` final snapshots and
`shared-multireg-store` retirement records. These are managed contract
checks, not new generated-code parity.

## 2026-09-29: Xarch returns and integer casts

SIMD split returns, x86 floating returns and integer cast generation preserve
their complete native target branches. Four definitions and the x86-only
return declaration retire 283 native lines in `05077bfc`. Independent
other-target definitions and the unported x87 stack-store helper remain.

Windows return controls pass 46 Debug/41 Release; integer-cast controls pass
81 Debug/80 Release. Three SysV return alias cases pass on both immutable
sides, and supported Linux cast controls pass 59 Debug/58 Release. Broader
Linux cast runs retain 22 independent throw-helper failures; x86 retains
14 existing diagnostic identities. ARM64 source compilation succeeds.
Evidence: the `xarch-simd-returns` and `xarch-integer-casts` proposals and
`xarch-return-cast` retirement records. No new generated-code parity claim.

## 2026-09-29: Shared return and call-storage lowering

Return dispatch and single-register struct-call storage preserve their native
target branches, ABI types and P/Invoke ordering. Two complete definitions
and their declarations retire 141 native lines in `50e6bcf4`; the independent
struct-call spill helper remains.

Old/final Windows controls pass 50 cases in each configuration. Linux return
controls improve from 1/24 to 24/24 in Debug; combined Release controls improve
from 3/26 to 26/26. Paired ARM64 builds pass with identical recorded prerequisite
repairs. RISC-V/Wasm remain blocked; the Wasm classifier is an existing
unresolved dependency, not a claimed working conversion. Evidence:
`artifacts/lower-ret-store-bcb453d` and `xarch-lower-ret-store` retirement
records. No target runtime or generated-code parity claim.

## 2026-09-29: Xarch floating casts

Floating-width and integer-to-floating casts retain their whole xarch
algorithms outside Windows AMD64, including unsigned-long correction and
dependency clearing. Two definitions and headings retire 186 native lines
in `15c47e9b`; independent other-target definitions and declarations remain.

Windows controls pass 71 Debug/69 Release, and supported Linux controls
68 Debug/66 Release. All 24 selected old Linux cases fail at the former guards
and pass after translation. ARM64 compiles with explicitly recorded, now
committed prerequisite overlays. The three broader Linux spill failures and
14 x86 build errors remain qualified on those historical snapshots.
Evidence: `artifacts/xarch-floating-casts-fd47f2f` and retirement records;
these are not new generated-code parity results.

## 2026-09-29: Xarch scalar intrinsic closure

Finite checks now include the x86 double-lane shuffle and restoration path;
floating bitwise, rounding and intrinsic dispatch preserve their complete
xarch bodies and file-level target boundaries. Four definitions and two
xarch-only declarations retire 324 native lines in `006835cc`.

Windows controls pass 153 Debug/148 Release. Focused Linux controls pass 42
cases on both immutable sides; broader runs retain the same 27 independent
spill/throw-helper failures on their earlier source baseline. X86 retains
14 existing build errors, so its new cases remain unexecuted.
Evidence: `artifacts/xarch-scalar-intrinsics-910f48f` and corresponding
retirement records. No new generated-code parity claim.

## 2026-09-29: Shared LSRA return and write-barrier builders

Return and write-barrier reference building preserves 32-bit long returns,
target return registers, x86 optimized-barrier constraints and native Wasm
empty masks. Three whole definitions and their declarations retire 251 lines
in `3240bd59`.

Windows controls pass 53 Debug/52 Release, Linux-target controls 23 in each
configuration, and ARM64 source compilation succeeds. The earlier Linux
struct-return failure was a fixture type mismatch: correcting the normalized
return type also passes on unchanged production source. X86 retains 14 existing
build errors; other blocked targets and unexecuted branches remain qualified.
Evidence: `artifacts/lsra-shared-return-writebarrier-ready-20260929-a12cc63`
and the `xarch-lsra-return` retirement records. No generated-code parity claim.

## 2026-09-29: Shared register spilling

Tree and local spilling now preserve the native fixed-register/non-Wasm
boundaries, temporary ownership, typed loads/stores and local write-through
rules. Four definitions and their declarations retire 193 lines in `c966d60e`.

Windows controls pass 83 Debug/81 Release; ARM64 passes 63 Debug/63 Release.
All nine identical old ARM64 cases fail at the intended guards. Linux focused
controls pass 56 cases; the broader 68/83 result retains 15 separate
multi-register local-store failures. This integration also repairs the recent
atomic ARM64 compilation regression by restoring xarch boundaries (B467);
the original xarch bodies are byte-identical and receive no retirement credit.
Evidence: `artifacts/shared-register-spills`, `shared-register-spills-arm64`
and the `shared-spill` retirement and boundary-repair records. These are
host-side target controls, not generated-code parity.

## 2026-09-29: Shared struct call and return lowering

Struct call/return lowering preserves SysV single-register results, ARM HFA
conditions, narrow memory reads and Wasm zero-constant retyping. Three complete
definitions and their declarations retire 316 native lines in `db6214e`.

Old/final Windows controls pass 43 Debug/43 Release; both direct Linux
regressions fail on old source and pass after translation. A fixture-field guard
correction preserves the exact previously tested target-selected source.
Paired ARM64 core builds pass, but target-test compilation remains separately
blocked; identical baseline syntax errors prevent Wasm validation. Broader Linux
return controls still reach the independent retained `LowerRet` guard.
Evidence: `artifacts/lower-struct-14d46ad`, its guard supplement and the
`struct-return` retirement records. No generated-code parity claim.

## 2026-09-29: Xarch atomics and memory barriers

Whole lock-add, atomic exchange/update, compare-exchange and memory-barrier
generation now retain non-Windows xarch paths. Four definitions and the
xarch-only lock-add declaration block retire 224 native lines in `0fe483d`;
declarations with independent other-target implementations remain.

Windows and Linux each pass 43 Debug/40 Release. All 24 selected old Linux cases
fail at the former guards; the same selection passes on the final Debug binary.
Coverage preserves implicit exchange locks, unused/used results, narrow-result
extension and full versus load/store barriers. X86 retains 14 existing build
errors. Evidence: `artifacts/xarch-atomics-f2ed014` and corresponding retirement
records. These target controls are not generated-code parity.

## 2026-09-29: Shared register copies, reloads and selectors

Register production, local reloads, both copy overloads, indexed GenTree queries
and load/store selectors now retain their complete native target branches.
Nine definitions and associated declarations retire 977 native lines in
`e8c18c5`. The separate variable-range constructor guard is corrected without
retirement credit because its native body was already absent (B466).

Combined ARM64 controls pass 76 Debug/76 Release and Windows controls pass
218 Debug/213 Release. Coverage includes GC kinds, indexed copies, local
respill/last-use ownership and load/store widths. Component old-source failures
and earlier integration failures remain recorded. A final selector preprocessor
correction preserves the validated active Windows/ARM64 source while removing
two unreachable-code diagnostics from an unsuppressed Wasm comparison; that is
not a successful Wasm build.

Separate spill-store helpers remain native. X86 and other-target compilation
limitations remain explicit; no generated-code parity is claimed. Evidence:
`artifacts/shared-local-copy` final-4/windows-3, the selector/GenTree packets
and `shared-register` integration records.

## 2026-09-29: Shared LSRA local stores

Three whole local-store builders preserve x86 byte-register constraints,
32-bit long halves and ARM misaligned-field temporaries. Removing invented
whole-function target guards also restores their shared LoongArch64/RISC-V64
source paths. Definitions, headings and declarations retire 269 native lines
in `3c88ed4`.

Windows controls pass 23 Debug/23 Release; Linux store controls pass 21 Debug.
The broader Linux run remains 21/22 because a separate retained return-path
fixture expects one source but gets zero. Guard-corrected Windows/Linux focused
controls each pass four cases. X86's four new cases remain blocked by 14 existing
errors; ARM, LoongArch64 and RISC-V builds fail earlier in unrelated shared
sources, so those bodies are not claimed compiled or executed. Evidence:
`artifacts/lsra-shared-store-20260929-a143b50`, its guard supplement and the
`lsra-local-store` retirement records. No generated-code parity claim.

## 2026-09-29: Xarch flag reuse

Whole zero/sign flag reuse and consumer discovery preserve condition mutation,
resolution-node skipping and first-consumer ordering. Two definitions and their
xarch-only declaration block retire 108 native lines in `25f2429`; the separate
Wasm comparison declarations remain.

Windows controls pass 38 Debug/35 Release and Linux passes 38 Debug, including
native diagnostic text. X86 retains 14 existing build errors, leaving its new
non-register early-return case unexecuted. Evidence:
`artifacts/xarch-flag-reuse-19939c8` and corresponding retirement records.
This is not generated-code parity.

## 2026-09-29: Shared call and argument lowering

Whole call and argument dispatch preserve target-specific lowering order,
special-copy handling and tailcall dependencies. Two complete definitions and
their associated declarations retire 263 native lines in `e4f55b1`.
Separate special-copy, helper-tailcall and portable-entrypoint helper bodies remain
native behind typed terminating managed dependencies.

Windows controls pass 119 Debug/119 Release, ARM64 passes 33 Debug and Linux
passes 12 Release. A subsequent `unsafe` keyword correction affects only the
unexecuted x86/IJW dependency; its exact source difference is recorded separately.
No x86, Wasm, RISC-V or generated-code execution parity is claimed. Evidence:
the `shared-lower-call-dispatch` proposal/supplement and `shared-call-lowering`
integration records under `artifacts/residual-reconciliation`.

## 2026-09-29: Integer and floating comparison closure

Whole xarch comparison helpers preserve x86's byte-addressable-register
restriction for narrow `TEST` immediates. Two definitions and headings retire
175 native lines in `d5045551`; shared declarations remain for the separate
Wasm comparison implementations.

Windows controls pass 66 Debug/65 Release and Linux passes 63 Debug, covering
flag-only comparisons alongside relational, NaN and bit-test cases. X86 retains
14 existing build errors, so its new byte-register cases remain unexecuted.
Evidence: `artifacts/xarch-compare-float-int-6c96f10` and the corresponding
retirement records. No generated-code parity claim.

## 2026-09-29: Shared switch and memory-comparison lowering

Whole switch/bit-test lowering restores Unix/x86 paths, Wasm's degenerate-switch
handling before `br_table`, and ARM32/RISC-V target decisions. Memory comparison
preserves ARM64's scalar equality/AND form and SIMD XOR/OR form. Three complete
definitions and all three associated declarations retire 1,014 native lines in
`d4db3e8d`; independent native callers remain.

Switch controls pass 27 Debug/23 Release on Windows and Linux. Both fresh Linux
snapshots explicitly include the now-committed call-builder declaration repair;
six identical old-source cases fail at the old target guards. Memcmp Windows
old/final controls pass 25 per configuration; three ARM64 cases fail on old
source and pass in both final configurations. Paired switch x86/Wasm builds
retain 14/81 existing diagnostics. These are managed target controls, not target
machine-code execution. Evidence: the `shared-lower-switch`, repaired Linux
supplement, `shared-lower-memcmp` and `shared-lowering` records under
`artifacts/residual-reconciliation`.

## 2026-09-29: Condition and operand instruction dispatch

Whole comparison/condition/trap and operand-dispatch routines preserve x86
branches and independent emitter dependencies. Seven definitions and four
xarch-only declarations retire 437 native lines in `b80a54a7`; shared
compare/condition/trap declarations remain for other targets.

Combined Windows controls pass 84 Debug/81 Release. Linux passes 83 of 84
Debug cases; the remaining fixture reaches the independent Windows-only tree
spill dependency before operand classification. It is not a full Linux pass.
X86 retains 14 baseline errors. A direct PSHUFD immediate `0xE4` assertion
matches the pinned native restriction and remains unchanged (B465).
Evidence: `artifacts/condition-operand-integration` and the corresponding
`condition-operand` retirement and limitation records. No execution-parity
claim follows from translation retirement.

## 2026-09-29: Xarch LSRA file closure

The final hardware-intrinsic builder preserves x86 byte-register and CRC32
constraints and target-specific APX/EVEX behavior. Its complete definition and
remaining scaffolding close the 1,123-line `lsraxarch.cpp` in `a892d056`.
The shared declaration remains for other targets.

The native APX-aware `BigMul` constraint also repairs an unnecessarily restricted
Windows-x64 candidate mask: R16 is available when EVEX semantics permit it.
The recent call-builder translation's Windows-only float-vararg local now has
a matching declaration guard, fixing Unix CS0219 without changing ABI behavior.

Original Windows controls pass 42 Debug/42 Release. Supplemental controls pass
10 Debug/10 Release; identical old cases fail only the BigMul mask in each
configuration. Linux call/BigMul controls pass three cases per configuration
after the declaration repair. The initial broader Linux run retains one Debug
SIMD16 return-description fixture failure (five of six pass); Release passes six.
X86 retains 14 baseline errors, leaving six new cases unexecuted. Evidence:
`artifacts/lsra-xarch-hw-intrinsic-20260929-f37322e` and the corresponding proposal
and `lsra-final` whole-file records, plus `lsra-xarch-hw-bigmul` and
`lsra-call-linux-bigmul` supplements under `artifacts/residual-reconciliation`.
File closure and allocator-mask controls are not execution parity.

## 2026-09-29: Shared copy/move and intrinsic operand helpers

Whole copy selectors and move dispatch preserve ARM, ARM64, x86, LoongArch64
and RISC-V64 branches, including register-class normalization and ARM flags.
Three intrinsic operand wrappers retain their complete x86 paths. Six
definitions and their declarations retire 600 native lines in `c953db8a`.
Independent ARM/SVE recording and x86 operand/recording dependencies remain.

Combined Windows controls pass 133 Debug/126 Release. ARM64 selector, move
operand/width and elision controls pass 26 Debug/26 Release; all 26 identical
old cases fail at the old target guards. Wrapper Linux controls pass 15 Debug.
X86 retains 14 baseline errors. No generated-code execution parity is claimed.
Evidence: `artifacts/shared-copy-move`, `artifacts/shared-instr-integrated`,
`artifacts/instr-intrinsic-wrappers-packet` and the `shared-instr` retirement records.

## 2026-09-29: Xarch constant generation

Three whole scalar/vector/mask constant-generation overloads retire 310 native
lines in `eff075a1`, preserving relocation and x86 paths. The shared scalar
declaration stays for other targets. Windows controls pass 71 Debug/67 Release,
Linux passes 71 Debug, and ARM64 source compilation passes. X86 retains 14
baseline errors and independent recording dependencies. Evidence:
`artifacts/xarch-constants-db8d8e4` and the `xarch-constants` retirement records;
no new generated-code parity is claimed.

## 2026-09-29: Xarch lowering file closure

Restoring the x86 virtual-stub call-containment path closes the final two
definitions in `lowerxarch.cpp`. Its remaining 131 lines retire in `66d75df4`;
shared declarations stay for other targets.

Windows 135 Debug/135 Release and Linux 135 Debug controls pass. Two new Windows
controls also pass on old source; the actual x86 repair remains unexecuted
behind 14 unchanged baseline errors. File retirement is not complete execution
parity. Evidence: `artifacts/xarch-final-88b686b` and the `xarch-lower-final`
proposal and `lower-final` whole-file records under `artifacts/residual-reconciliation`.

## 2026-09-29: Xarch LSRA call and node dispatch

Whole call and node builders preserve x86 ABI constraints, split arithmetic and
shifts, multi-result multiplication, byte-register restrictions and address
temporaries. Two definitions retire 856 native lines in `bdf33799`; independent
builders and shared declarations remain.

Each packet passes 46 Debug/46 Release Windows controls on old and final source.
Paired x86 builds retain 14 identical errors, leaving 17 new x86 cases unexecuted.
Evidence: `artifacts/lsra-xarch-call-20260929-d1f820a`,
`artifacts/lsra-xarch-node-20260929-c824e24` and the `lsra-call-node` retirement
records. These are translation and control results, not generated-code parity.

## 2026-09-29: Xarch conditional codegen

Four complete JTrue/conditional-move/select/jump definitions retire 183 native
lines in `694ccb44`, including the x86 stack-depth assertion. Shared declarations
and condition maps remain; the surviving ARM64 guard comment is corrected.
Windows old/final controls pass 20 Debug/19 Release and Linux passes 20 Debug.
ARM64 source compilation passes; x86 retains 14 baseline errors and its new
branch is unexecuted. Evidence: `artifacts/xarch-conditional-c635bcd` and the
`xarch-conditional` retirement records under `artifacts/residual-reconciliation`.

## 2026-09-29: Xarch hardware-intrinsic dispatcher closure

The base-family routines preserve Unix/x86 entry, x86 scalar creation and
AMD64-only APX selection. The four final definitions close
`hwintrinsiccodegenxarch.cpp`; its 1,694 lines and two xarch declarations retire
in `f895d51f`. The shared dispatcher declaration remains for ARM64/Wasm.

Windows controls pass 21 Debug/21 Release and Linux passes 21 Debug, versus
four passes and 17 failures with identical old cases. ARM64 source compilation
passes; x86 retains 14 baseline errors and an independent immediate-insertion
dependency. No generated-code parity is claimed. Evidence:
`artifacts/hwintrinsic-dispatch-packet` and its corrected boundary/header
supplement and whole-file retirement records under `artifacts/residual-reconciliation`.

## 2026-09-29: Existing intrinsic scalar and containment retirement

The complete existing ToScalar, containability and intrinsic-containment
translations retire 2,244 native lines in `56b834df`, without managed changes.
Shared declarations and independent call-containment definitions remain.
Windows 28 Debug/28 Release and Linux 28 Debug controls pass; x86 retains 14
established errors. This is translation retirement, not new execution coverage.
Evidence: `artifacts/xarch-containment-03a2661` and the
`xarch-lower-containment-scalar` records under `artifacts/residual-reconciliation`.

## 2026-09-29: Shared register consumption and reload control

Six whole consumption/reload routines preserve 32-bit long operands, ARM64
contained address and AND paths, fixed-register guards, and native GC/liveness
ordering. Their definitions and declarations retire 506 lines in `f89207a1`.
Non-xarch move declarations now compile while their separate unported bodies
still terminate; local reload, spill and register-copy dependencies remain.

Windows controls pass 176 Debug/166 Release. Nine ARM64 cases pass in both
configurations and all nine fail on identical old source. These establish GC
clearing, operand order and no-spill exits, not ARM64 move/load emission.
X86 retains 14 baseline errors. Evidence: `artifacts/shared-register-consumption`
and its validation and retirement records under `artifacts/residual-reconciliation`.

## 2026-09-29: Xarch widening multiplication and binary arithmetic

High/widening multiplication now preserves the x86 `MUL_LONG` second result
through the shared register contract. Binary arithmetic retains split add/subtract
carry and borrow cases and their overflow diagnostics. Two whole definitions
retire 298 lines in `07e3838b`; common declarations and other-target bodies remain.

Windows controls pass 71 Debug/68 Release; ARM64 source builds. Linux old and
final selections both have 55 passes and the same 16 independent dependency
failures. X86 retains 14 first-pass errors, so five added x86 cases remain
unexecuted. Evidence: `artifacts/xarch-mulhi-binary-fd29708` and its proposal and
retirement receipts.

## 2026-09-29: Xarch scalar and select register requirements

Select, scalar intrinsic and cast builders now retain their x86 branches,
including select interference rules, intrinsic-mask temporaries, byte-register
constraints and contained long operands. Three whole definitions retire
275 lines in `83a45ef5`; shared declarations and other-target definitions remain.

Old and final Windows controls pass 25 Debug/25 Release. X86 retains the same
14 first-pass errors, so its three new cases remain unexecuted. Evidence:
`artifacts/lsra-xarch-scalar-selection-20260929-784ed0b` and its proposal and
retirement receipts.

## 2026-09-29: Element and dot-product retirement catch-up

Four existing whole translations cover element extraction/insertion and
dot-product lowering with its inner multiply/sum helper. No managed repair was
needed. Their definitions and three xarch-only declarations retire 1,427 lines
in `36b302c1`; the shared dot declaration and independent scalar/containment
dependencies remain.

Unchanged-source controls pass 45 Debug/45 Release on Windows and 45 Debug for
Linux. X86 retains 14 baseline errors. This is retirement catch-up, not new
implementation or execution coverage. Evidence:
`artifacts/xarch-element-dot-bf1d73b` and its proposal and retirement receipts.

## 2026-09-29: Xarch multiplication, division and long remainder

Multiplication and division preserve their whole xarch paths. The x86 long
unsigned remainder helper now performs native conditional high-word reduction
and two-stage division, retaining separate consumption/copy dependencies.
Three definitions and xarch-only declarations retire 279 lines in `f589d7e7`;
the shared division declaration and widening-multiply body remain.

Windows controls pass 49 Debug/46 Release; ARM64 source builds. Linux old and
final selections both have 38 passes and the same 11 throw-helper/tree-spill
failures. X86 retains 14 baseline errors, so its new remainder body is not an
execution result. Evidence: `artifacts/xarch-mul-div-74d4617` and its proposal,
header reanchoring and retirement receipts.

## 2026-09-29: Shared multi-register result assignment

`GenTreeMultiRegOp` now has the native mutable second-result register and
explicit spill-state initialization. Whole `lsraAssignRegToTree` and
`writeRegisters` preserve primary and indexed assignment across target guards.
Their definitions and declaration retire 56 lines in `a9bdcdfd`. The native
node structure was already absent despite the incomplete managed contract;
repairing that bounded exception adds no struct-retirement credit.

Combined Windows controls pass 280 Debug/267 Release; ARM64 source builds pass
both configurations. Two Linux writeback cases pass and fail on identical old
source. X86 retains 14 first-pass errors, so its new state/copy/spill case remains
unexecuted. Secondary copy coverage follows native `FEATURE_MULTIREG_RET`,
correcting a prior Windows fixture that exercised an invalid native target case.
Evidence: `artifacts/multireg-writeback-closure`,
`artifacts/xarch-shared-writeback-integration` and their integration records.

## 2026-09-29: Whole xarch AVX-family code generation

AVX-family generation, FMA, two-source permutation and mask-lane clearing now
retain their complete xarch paths instead of rejecting Unix AMD64 or x86 at
entry. Native operand ordering, FMA forms, destination selection and mask shifts
are preserved. Four definitions and their declarations retire 1,258 lines in
`4a007f25`; separate HW dispatchers and recording dependencies remain.

Focused Windows controls pass 28 Debug/28 Release and Linux controls pass 28;
all 28 identical old Linux controls fail. The combined integration snapshot
passes 280 Debug/267 Release Windows controls and ARM64 source builds in both
configurations. X86 still has 14 baseline errors. These are managed controls,
not generated-code parity. Evidence: `artifacts/hwintrinsic-avx-packet`,
`artifacts/xarch-shared-writeback-integration` and their integration records.

## 2026-09-29: Xarch stack-argument register requirements

Whole `BuildPutArgStk` now includes x86 field-list packing, byte-register
constraints, SIMD12 temporaries, odd-sized struct unrolls and push/rep handling.
The 157-line definition retires in `0166db8c`; its shared declaration and
other-target implementations remain.

Old and final Windows controls pass 37 Debug/37 Release. X86 retains the same
14 first-pass errors, so the added x86 cases are not execution evidence.
Evidence: `artifacts/lsra-xarch-stack-argument-20260929` and its proposal and
retirement receipts under `artifacts/residual-reconciliation`.

## 2026-09-29: Xarch comparison, selection and construction lowering

Four whole intrinsic helpers preserve comparison, conditional selection,
ternary logic and vector construction. Native diagnostics are restored, and
the conditional-select Debug assertion no longer reports an AVX dependency.
Normal downstream AVX lowering still reports its legitimate dependency.
Four definitions and one xarch-only declaration retire 2,018 lines in
`81f0e045`; shared declarations and separate element/containment helpers remain.

Windows controls pass 125 Debug/124 Release; Linux controls pass 125 Debug.
The identical old fixture fails only the new ISA-notification regression.
X86 retains 14 baseline errors; no x86 execution or generated-code parity is
claimed. Evidence: `artifacts/xarch-intrinsic-helpers-134f10e` and the proposal
and retirement receipts under `artifacts/residual-reconciliation`.

## 2026-09-29: Shared call GC capture and return reporting

The whole shared call routine now preserves non-AMD64 control flow: fixed-register
retbuffer unwrapping, x86 stack-pop bias and x87 return locations, ARM64 piece-size
restrictions and other targets' debugger register limitations. Current GC state
is captured before calling the independent recorder, in native order.
The definition and common declaration retire 158 lines in `822520d2`.

Windows controls pass 144 Debug/133 Release; ARM64 allocation and capture
controls pass 20 Debug/20 Release. Two identical old ARM64 capture cases fail.
These ARM64 checks stop at the terminating recorder and do not establish call
emission or post-call reporting. Other-target variable-location dependencies and
the 14 x86 baseline errors remain. Evidence: `artifacts/common-call-gc-closure`
and its validation/integration records under `artifacts/residual-reconciliation`.

## 2026-09-29: Xarch unary, byte-swap and scalar operations

Negation/not, byte swap, saturating increment and bit modification retain their
whole xarch paths. Four definitions retire 155 lines in `a30146a8`. Floating
bitwise operations and high/widening multiplication remain separate work; the
latter needs the shared second-result-register contract completed.

Windows controls pass 98 Debug/95 Release. Focused Linux controls pass 65,
including ten increment/bit-operation cases that fail on unchanged source.
Two broader spill cases remain blocked by the independent `rsSpillTree`
dependency. ARM64 source builds; x86 retains 14 baseline errors. Evidence:
`artifacts/xarch-scalar-f87562b` and its proposal/retirement receipts.

## 2026-09-29: Xarch block-store requirements and RMW last use

Block-store construction preserves native x86 byte-register requirements,
including the fixed accumulator temporary for high-pressure odd-sized copies.
The standalone RMW helper now transfers last-use flags between matching locals,
replacing the dependency retained by the earlier indirection batch. Two whole
definitions retire 258 lines in `1e1ddea2`.

Old and final Windows controls pass 63 Debug/63 Release. X86 retains the same
14 baseline errors and its native unfinished-memmove assertion; no x86 execution
is claimed. Evidence: `artifacts/lsra-xarch-rmw-block-20260929` and its proposal
and retirement receipts under `artifacts/residual-reconciliation`.

## 2026-09-29: Whole xarch hardware-intrinsic lowering dispatcher

The complete `LowerHWIntrinsic` dispatcher retains its native transformations,
nested gather selection, containment and separate-helper calls. Missing native
diagnostic structure is restored without changing valid instruction selection.
The 1,355-line definition and heading retire in `60ab6147`; separate intrinsic
lowering helpers and the shared declaration remain.

Windows controls pass 60 Debug/59 Release; Linux-target controls pass 60.
An identical old extract-shape control fails because two native assertions had
been combined into one. Impossible comparison-ID defaults are bounded by the
outer switch and are not dynamically tested. X86 retains 14 baseline errors.
Evidence: `artifacts/xarch-hwintrinsic-a0d3f34` and the original proposal,
assertion supplement and retirement receipts under
`artifacts/residual-reconciliation`.

## 2026-09-29: Xarch immediate materialization and helper calls

Immediate materialization and helper-call generation preserve their x86 paths,
including PC-relative helper selection and target-width address checks, while
retaining the AMD64 register fallback. Two complete definitions retire 127 lines
in `0a241073`; call/GC emission and x86 instruction recording remain independent
dependencies.

Windows controls pass 90 Debug/84 Release. The focused Linux controls pass 46;
the broader return-location fixture has one SIMD16 failure reproduced on the
unchanged baseline. X86 still has 14 baseline errors. The ARM64 error reported
with this packet was the SIMD target-guard regression already repaired in
`cd16cba`, not an immediate/helper-call regression. Evidence:
`artifacts/xarch-immediate-helper-a196428` and its proposal/retirement receipts.

## 2026-09-29: Close the xarch emitter residual source

The seven remaining free register helpers now preserve native decode assertions,
x86 register bounds and shared absolute/opcode encodings. Register naming and
EVEX encoding use the native helper algorithms. The existing displacement
relocation constant is accounted for too: the remaining 585-line
`emitxarch.cpp`, mostly comments and guards after earlier batches, is deleted
in `b38c1f34`.

Windows controls pass 484 Debug/417 Release; Linux-target controls pass 21.
Six identical old controls fail on the missing decode assertions. X86 retains
the same 14 baseline compile errors. This closes the residual file, not all
emitter dependencies or generated-code parity. Evidence:
`artifacts/xarch-register-helper-closure` and
`artifacts/residual-reconciliation/register-helpers-whole-file-20260929.json`.

## 2026-09-29: Restore the SIMD upper-clear target boundary

The SIMD completion batch accidentally exposed `genSimd12UpperClear` to ARM64,
where `INS_insertps` does not exist. Its declaration now retains the native
`TARGET_XARCH` boundary without changing either xarch body. ARM64 Debug and
Release source builds pass; the unchanged pre-fix snapshot fails at that symbol.
Evidence: `artifacts/xarch-register-helper-closure/final-arm64-guard-*` and
`artifacts/xarch-immediate-helper-a196428/arm64-old-debug.log`.

## 2026-09-29: Xarch memory register construction

Local-heap and indirection builders retain native x86 counter-register and
byte-register constraints, both RMW memory-operand positions and base/index
last-use-helper calls. Two definitions retire 137 lines in `4962ffef`.
The separate `CheckAndMoveRMWLastUse` helper remains native and terminating
in the managed x86 path.

Old and final Windows controls pass 26 Debug/26 Release. Four new x86 cases
remain unexecuted behind the same 14 baseline build errors. Evidence:
`artifacts/lsra-xarch-memory-20260929` and the memory proposal and retirement
receipts under `artifacts/residual-reconciliation`.

## 2026-09-29: Whole xarch cast lowering

`LowerCast` now includes the native x86 unsigned-int/floating conversion paths,
with the original vector operations, target-width constants, LIR replacement
and recursive lowering order. The whole definition and heading retire 495 lines
in `b4e56280`; its shared declaration and separate intrinsic-lowering dependency
remain native.

Windows controls pass 24 Debug/24 Release, with the same 24 old-source Debug
passes; Linux-target controls also pass 24. Four new x86 cases remain unexecuted
behind the same 14 baseline compilation errors. Evidence:
`artifacts/xarch-lowercast-f527abe` and the LowerCast proposal and retirement
receipts under `artifacts/residual-reconciliation`.

## 2026-09-29: Xarch cookie and EH code generation

Security-cookie initialization/checking now retain their native x86 paths, and
finally-call/catch-return generation no longer rejects non-Windows xarch.
Four whole definitions retire 186 lines in `b30154f4`. Separate x86 immediate
materialization, helper-call and instruction-recording dependencies remain.

Windows controls pass 88 Debug/81 Release; Linux-target controls pass 35, with
18 EH cases failing against the unchanged baseline. ARM64 source compiles.
X86 remains blocked by the same 14 baseline diagnostics. These are managed
contract controls, not target execution parity. Evidence:
`artifacts/xarch-general-codegen-f527abe` and the general-codegen proposal and
retirement receipts under `artifacts/residual-reconciliation`.

## 2026-09-29: Complete xarch SIMD code generation

All eleven retained SIMD functions now preserve their x86 branches, including
SIMD12 indirect/local loads and stores, stack arguments, upper-lane saves/restores
and clearing. Unix AMD64 no longer hits the inappropriate Windows-only guards.
The native `simdcodegenxarch.cpp` is deleted, with its eleven declarations:
487 lines retired in `d7b27c72`.

Combined Windows controls pass 18 Debug/18 Release. The first Unix regression
set changes from ten failures to ten passes; eight additional unchanged-path
controls pass on both Windows and Unix targets. Fourteen existing x86 compilation
errors still prevent execution. General argument dispatch and the seven retained
HW intrinsic codegen definitions are independent remaining work. Evidence:
`artifacts/simdcodegenxarch-packet`, `artifacts/simdcodegenxarch-integrated` and
the superseding proposal and whole-file receipt under
`artifacts/residual-reconciliation`.

## 2026-09-29: Xarch scalar register construction

Shift/rotate, modulo/divide and multiply builders now retain their x86 long-value
paths: paired shift operands, fixed EAX/EDX modulo uses and widening-multiply
definitions, including MULX. Native source counts, assertions, delay-free uses and
kill/internal-register ordering are preserved.

Old and final Windows controls pass 22 Debug/22 Release. The same 14 pre-existing
x86 errors still prevent executing six new x86 controls. Three complete
definitions retire 282 native lines in `76a2525c`; this is branch translation,
not an x86 execution claim. Evidence is in `artifacts/lsra-xarch-scalar-20260929`
and its proposal/integration records under `artifacts/residual-reconciliation`.

## 2026-09-29: ARM64 constant-node materialization

Constant-node generation now retains the complete scalar, fixed-vector,
scalable-vector and mask dispatch. Scalar signed zero and NaN payloads survive
constant-pool selection; fixed-vector broadcasts retain native width precedence
and SIMD12 padding decisions. Scalable paths retain register extraction and pool
load ordering before their still-unported SVE recorders.

Constant-address recording preserves cold/relocatable long forms and ordinary
jump-list binding. Shared floating data retains target-specific alignment, and
double-to-single conversion preserves the native RISC-V-host NaN rule. TLS
deferral and relocation-dependent GC attributes retain native behavior.

Full-analysis ARM64 recording controls pass 630 Debug/600 Release, including the
committed xarch name/cost changes; shared Windows controls pass 115/114. All 50
identical old controls fail at missing methods. Six definitions, one enum and
four declarations retire 576 native lines in `1273a804`.

SVE recorders remain explicit dependencies, not successful no-ops. No ARM64
generated-code parity or RISC-V-host execution is claimed. Evidence is in
`artifacts/arm64-constant-nodes` and its integration records under
`artifacts/residual-reconciliation`.

## 2026-09-29: Xarch names, jump classification and execution costs

Shared instruction display-name dispatch preserves its target paths; non-xarch
names still depend on an explicit unported table helper. Xarch jump/no-code
classification retains the after-call nop exception, and x86 stack-level tracking
retains group and maximum-depth updates. Cost classification preserves all 212
instruction cases, including x86 `fld`/`fstp`, and the native timing tables.

The jump and execution-table generator guards now cover both xarch targets.
A fresh combined generator run reproduces both maintained outputs. Combined
Windows controls pass 316 Debug/249 Release; focused Linux-target controls pass
174 name/jump and 31 cost cases. ARM64 compiles. The same 14 unrelated x86 build
errors still prevent x86 execution, including the new x87 controls.

Eleven definitions and their dedicated tables/declarations retire 1,708 lines in
`82af7a2a`. `emitxarch.cpp` now contains 585 physical lines; this is not a count
of untranslated implementation. Other-target names, x86 unhandled-instruction
diagnostics and the existing non-DEBUG `LATE_DISASM` alignment-metadata boundary
remain separate. Evidence is in `artifacts/xarch-name-cost-integration` and the
combined-generator receipt under `artifacts/residual-reconciliation`.

## 2026-09-29: Xarch instruction and operand diagnostics

Instruction display retains all 120 native format labels and the whole x86/AMD64
control flow. Register-pair formatting now preserves EVEX-mask and tuple-size
assertions and the AMD64-only `movsxd` case. Register names, addresses, frame and
static operands, hex columns and embedded options retain native formatting.
The standalone x86 instruction-name dependency remains explicit and unported;
incomplete inline logic is not treated as a separate dependency.

Combined full-analysis Windows controls pass 200 Debug/149 Release. Focused
Linux-target controls pass 102 helper and 10 format cases. Broader Linux fixtures
still have host-newline and Windows-callee-save assumptions. Old/final x86 builds
with identical parser overlays retain the same 14 unrelated errors; no x86
execution is claimed.

Sixteen definitions and twelve declarations retire 2,255 native lines in
`31084941`. Evidence is in `artifacts/xarch-display-integration` and its component
packets. Shared declarations remain for other targets. These contract checks do
not establish new native dump or generated-code parity.

## 2026-09-29: Immediate and relocatable-address materialization

ARM64 constants now preserve native MOVN/MOVZ selection, halfword skipping,
MOVK ordering, low-halfword debug metadata and relocation precedence. Base-plus-
immediate materialization preserves SP operand placement. ADR and ADRP-plus-ADD
record their native relocations. Shared zero materialization retains every target
branch, with explicit unported recording dependencies outside xarch/ARM64.

Full-analysis ARM64 controls pass 657 Debug/628 Release; shared Windows controls
pass 99/93. Thirty old controls fail at the former dependencies. Large local-stack
offsets now record through the reserved register instead of stopping at
materialization. SVE recording/sanity and load/store optimization remain separate.
B462 records the pinned nonzero flag-request path's unencodable `tst #0`; it is
not silently corrected or claimed to be a reproduced native runtime failure.

Four definitions and two declarations retire 210 native lines in `541759eb`.
Evidence is in `artifacts/arm64-immediate-materialization` and its integration
records under `artifacts/residual-reconciliation`. No new ARM64 generated-code
parity is claimed.

## 2026-09-29: Descriptor sizing and register-bit encoding

Xarch descriptor sizing now dispatches on native operand categories and flags,
including fat-call precedence, rather than managed subclass size. Address-mode
allocation retains packed displacement selection and ownership. Register-bit
encoding retains APX/REX/VEX/EVEX writebacks, x86 register assertions and native
BMI/SSE selector assertion-and-recovery behavior.

Combined Windows controls pass 377 Debug/372 Release, including memory output.
Component Linux-target descriptor controls pass 196 cases and ARM64 builds pass.
With identical parser-only overlays, combined x86 compilation has 14 of the 15
established errors and no new diagnostics; the corrected local register-width
constant removes one error. X86 execution and fat-call descriptor size remain
unverified, with the latter an explicit terminating dependency.

Twenty-seven definitions and 25 private declarations retire 857 native lines in
`8a8bffd4`. The shared descriptor-size declaration remains for other targets.
Evidence is in `artifacts/xarch-descriptor-register-integration`, its component
packets and `artifacts/xarch-descriptor-x86-supplement`. These contract checks do
not claim new generated-code parity.

## 2026-09-29: Local-stack single and paired recording

ARM64 stack loads/stores now preserve fixed and scalable frame paths, local and
spill-temp addressing, displacement scaling, paired-register GC types and debug
reference metadata. The flexible-immediate wrapper and reserved-register lookup
preserve their native dispatch and reservation invariant.

Full-analysis ARM64 controls pass 631 Debug/605 Release. All 39 identical baseline
controls fail at missing methods. Scalable descriptor metadata is checked in
Release; Debug explicitly stops at the retained SVE sanity-check dependency.
Immediate/base-plus-immediate materialization and load/store optimization remain
terminating dependencies, not omitted calls or claims of generated-code parity.

Seven definitions and their declarations retire 689 native lines in `53cdacff`.
The shared reserved-register definition remains for other targets. Evidence is in
`artifacts/arm64-local-stack-recording` and the local-stack integration records
under `artifacts/residual-reconciliation`.

## 2026-09-29: Multioperand, conditional and barrier recording

Ten ARM64 recorders now preserve multi-immediate packing, four-register
operations, RMW copy widths/order, conditional operations and barriers. The
packed condition/flags/immediate representation and condition/barrier enums
retain native values and truncation. Three SVE dependencies remain explicit.

Full-analysis ARM64 controls pass 583 Debug/557 Release; Windows SIMD/address
controls pass 409/392 against the exact four-register overload guard. All 64
identical positive cases fail at missing methods on baseline implementations;
those controls add only the two enum declarations needed to compile the fixture.
Additional controls exercise reserved conditions, immediate bounds and SVE
termination. No ARM64 generated-code parity is claimed.

Ten definitions, declarations, one packed union and two enums retire 752 native
lines in `5059bf26`. Other-target barrier enums and unported SVE bodies remain.
Evidence is in `artifacts/arm64-multioperand-recording` and the multioperand
integration records under `artifacts/residual-reconciliation`.

## 2026-09-29: Labels, ABI stores and instruction prefixes

Xarch label/jump recording, SIMD12 stores and argument-register homing now retain
their whole target paths. Prefix construction includes x86 EVEX/REX2 behavior
and SIMD-only register-write classification while preserving AMD64 encoding
gates, opcode bits and prefix sizes.

The exact combined Windows overlay passes 638 Debug/607 Release controls,
including the complete prolog-frame fixture in both configurations. Linux-x64
target controls pass 96 label/store/homing and 514 prefix cases on Windows.
ARM64 builds pass. Existing x86 compiler/GenTree/target build errors still
prevent execution; the old/final prefix builds have the same 15 first-pass
errors after identical parser overlays. No new generated-code parity is claimed.

Fifteen definitions, corresponding declarations and EVEX constants retire
1,011 native lines in `e2524f9e`. Shared declarations needed by untranslated
other-target definitions remain. Evidence is in `artifacts/xarch-label-prefix`
and its component and integration records under `artifacts`.

## 2026-09-29: Three-register immediates and paired locals

ARM64 three-register/immediate recording now preserves all 74 native instruction
cases, including indexed vector elements, shifted/extended arithmetic, SP
encoding, RMW copies and scaled load/store pairs. Local-pair recording preserves
both local addresses, independent GC types and debug reference offsets. Its
small/large constant descriptors use the shared allocation and ownership rules.

Full-analysis ARM64 controls pass 512 Debug/490 Release. All 55 identical selected
positive cases fail against the unchanged baseline. Boundary controls cover
indexed-halfword register restrictions, signed pair offsets and reserved
encodings. Native optional arguments and assertion distinctions are preserved.
Allocation counters/histograms retain the existing D001 representation policy;
no allocation-statistics equality or ARM64 machine-code parity is claimed.

Eight definitions, their declarations and the indexed-halfword register mask
retire 722 native lines in `33f25165`. SVE recording remains a separate terminating
dependency with its native body retained. Evidence is in
`artifacts/arm64-three-immediate-recording` and the corresponding final
integration records under `artifacts/residual-reconciliation`.

## 2026-09-29: Tree operands and GC-register handling

Seven xarch tree-level load/store/binary/RMW/base dispatchers now preserve their
whole target paths. GC-reference register handling includes x86 and retains
death-before-birth ordering during exchanges. The two upper-bit queries were
already complete and remain unchanged.

The exact combined Windows overlay passes 614 Debug/589 Release controls.
Component Linux-x64 target checks pass 282 supported tree cases and 202
upper-bit/GC cases on Windows. Three additional tree cases still stop in the
pre-existing non-Windows `RegSet.rsSpillTree` gate before reaching the recorders;
their failed results remain recorded. ARM64 builds pass. Existing x86 build
blockers still prevent execution.

Ten definitions and their declarations retire 1,109 native lines in `be05d130`.
Other-target binary/store definitions and signatures remain native. No new
machine-code parity is claimed. Evidence is in `artifacts/xarch-tree-upper-gc`
and the tree/GC integration records under `artifacts/residual-reconciliation`.

## 2026-09-29: Three-register and extended-memory recording

ARM64 three-register recording now preserves all 286 native instruction cases,
including RMW copy-before-update, scalar/vector arithmetic, atomic and structure
memory operations, and MOV/store-add aliases. Its complete extended-register
memory dependency retains SP encoding, natural and explicit shifts, and scaled
register metadata. The flags-only two-register forwarding overload is complete.

Full-analysis ARM64 recording/predicate controls pass 451 Debug/437 Release.
All 61 identical positive recorder cases fail at the old missing APIs. Windows
SIMD controls pass 373/352 against the exact xarch overload exclusions; the
remaining changes are ARM64-only.

Three definitions and their declarations retire 862 native lines in `04034591`.
General three-register/immediate and SVE recording remain explicit dependencies.
No ARM64 machine-code parity is claimed. Evidence is in
`artifacts/arm64-three-register-recording` and the corresponding three-register
integration records under `artifacts/residual-reconciliation`.

## 2026-09-29: Move, basic and flag-dependent recording

ARM64 move and two-register recording now preserve the whole native dispatch,
including scalar/vector conversions, narrowing, reductions, acquire/release and
structure memory forms. Move elision retains optimization gates, upper-bit
clearing, preceding loads/moves and instruction-group boundaries. The ARM64 move
API keeps its native `void`/`insOpts` contract, separate from xarch's Boolean API.
Five xarch immediate/basic recorders and their stack-depth helper preserve x86
paths; flag-reuse and register-write queries retain their target-specific gates.

The exact combined Windows overlay passes 441 Debug/426 Release controls; ARM64
recording/predicate controls pass 381/367. All 77 identical positive recorder
cases fail at missing APIs on the old source. Component Linux-x64 target checks
pass 84 and 332 Debug cases on the Windows host. Existing x86 build blockers
still prevent execution.

Sixteen definitions and their declarations retire 1,801 native lines in
`630e1993`. Separate SVE, three-register and optimization dependencies remain
explicit. B461 diagnostic restrictions are unchanged. No new machine-code
parity is claimed. Evidence is in `artifacts/arm64-move-recording` and the move
integration records under `artifacts/residual-reconciliation`.

## 2026-09-29: Two-register immediates and direct addresses

ARM64 two-register/immediate recording now preserves the complete native
instruction dispatch, including vector aliases, arithmetic reversal, packed
logical immediates, scaled/unscaled/indexed memory forms, register lists and TLS
tokens. Its optimization and SVE calls remain explicit separate dependencies.
Eight xarch direct-address recorders retain their x86 paths, including push/pop
stack-depth updates and target-width absolute addresses. Sixteen instruction,
encoding and flag classifiers are complete.

The exact combined Windows overlay passes 1,105 Debug/1,045 Release controls;
ARM64 recording/predicate controls pass 283/269. All 76 identical old-source
recorder cases fail at the missing API. Linux-x64 target controls cover 416
classification cases and two direct-address cases that previously stopped at a
Windows-only guard. Known x86 build blockers still prevent execution.

Thirty-one definitions and their declarations retire 1,523 native lines in
`7d519f03`. Two source-level native recorder/sanity disagreements are preserved,
not corrected silently; B461 records their Debug/Release contracts. No new
machine-code parity is claimed. Evidence is in
`artifacts/arm64-register-pair-immediate` and the pair integration records under
`artifacts/residual-reconciliation`.

## 2026-09-29: Shifted-immediate and address recording

ARM64 shifted-halfword recording preserves MOV aliasing, all legal 32/64-bit
halfword positions, packed immediates and the separate SVE fallback. Four xarch
address/blend recorders and three address helpers now retain their x86 paths,
along with nine SIMD wrappers and six instruction classifiers.

Combined Windows controls pass 681 Debug/639 Release. Two obsolete x86-only
missing-recorder expectations are replaced by positive operand checks, passing
separately in Debug and Release against identical production bytes. ARM64
recording controls pass 112/98, including 96 shifted descriptors; eight identical
old-source cases fail at the absent recorder API. Linux-x64 target wrapper
controls pass 471 Debug; existing x86 build blockers still prevent execution.

Twenty-three definitions and corresponding declarations retire 908 native lines
in `5406118b`. These are translation and descriptor-contract results, not new
machine-code parity. Evidence is in `artifacts/arm64-shifted-immediate` and the
shifted integration records under `artifacts/residual-reconciliation`.

## 2026-09-29: ARM64 floating-immediate recording

Scalar/vector FMOV and zero-compare recording now preserve the native floating
immediate encoding. The encoder returns its packed sign/exponent/mantissa fields,
and the matching decoder is translated. Both signs of zero remain valid for
floating compares, while FMOV retains its exact representability restrictions.

Full-analysis ARM64 controls pass 180 Debug/166 Release, including every byte
encoding across five scalar/vector arrangements (1,280 descriptor records).
Nine old-source cases fail at the absent recorder API. Exact pinned native
helpers independently round-trip all 256 encodings and reject eight invalid
values in Debug and optimized probes. This is scoped algorithm evidence, not
ARM64 machine-code parity.

Three functions and one packed union retire 195 lines in native `0278fa5c`.
The SVE floating recorder remains a separate terminating dependency. Evidence
is in `artifacts/arm64-floating-immediate` and the floating integration records
under `artifacts/residual-reconciliation`.

## 2026-09-29: Immediate and multioperand recording

ARM64 immediate, unary and register-immediate recording now builds descriptors
instead of stopping at xarch-only guards. Bitmask, halfword and byte-shifted
encoders preserve their optional native writebacks and packed aliases, including
MOV/MOVN preference, replicated masks, vector LSL/MSL forms and negative-compare
reversal. Separate SVE recorder fallbacks still terminate explicitly.

Six xarch multi-register memory recorders and eleven SIMD multioperand wrappers
retain their full x86 paths, operand ordering, relocation and encoding options.
The combined full-analysis Windows overlay passes 644 Debug/605 Release.
ARM64 recording/predicate controls pass 213/199, with all 31 identical old-source
positive cases failing at the former recording guards. Linux-x64 wrapper controls
pass 331 Debug; existing x86 build blockers still prevent x86 execution.

Thirty-one complete functions, three packed unions and one enum retire 1,934
native lines in `783ff54d`. These are translation and managed contract results,
not new target machine-code parity. Evidence is in
`artifacts/arm64-immediate-recording` and the recording integration/component
records under `artifacts/residual-reconciliation`.

## 2026-09-29: ARM64 register and option descriptors

Third/fourth-register access is shared across xarch and ARM targets. ARM64
preserves the local/SVE word and its separate packed register word, including
second-result GC typing and the scaled/merging/vector-length bit aliases.
SVE pattern and prefetch enums retain all native values; prefetch aliases the
seven-bit fourth register. Tiny-descriptor shift access avoids tail storage.

Scoped native data-member probes verify 131,072 combinations in Debug and
optimized builds. Full-analysis controls pass Windows 135 Debug/129 Release and
ARM64 112/95; five identical old-source cases fail at the former register guards.
Twenty-two methods and two enums retire 167 lines in native `b02951eb`.
ARM32 execution, SVE sanity and ARM64 machine-code parity remain unestablished.
Evidence is in `artifacts/arm64-descriptor-registers` and the register-descriptor
retirement records under `artifacts/residual-reconciliation`.

## 2026-09-29: Stack sizing and EVEX displacement compression

Stack-variable sizing and EVEX displacement compression now retain their whole
x86 paths. This includes pushed-stack displacement wrapping, restrictions on
optimistic compression without fixed outgoing arguments, tuple scaling and
the native early-exit invariants. The immediate overload keeps AMD64-only
relocation assertions and the shared immediate-width rules.

Full-analysis controls pass Windows 241 Debug/225 Release for stack sizing and
53/53 for compression, plus 53 Linux-x64 target cases and ARM64 builds. Existing
x86 build blockers still prevent execution of the three new x86 compression
cases. Six complete definitions retire 452 lines in native `1b167498`.
Evidence and exact retirement records are under
`artifacts/residual-reconciliation/*sizing*-20260929.json`.

## 2026-09-29: Stack and local-variable recording

Seven stack/local recorder and move-elision bodies retain their complete x86
paths, including signed offset handling, byte-register validation and
post-record stack-depth adjustment. Windows behavior remains unchanged.

Full-analysis controls pass Windows 221 Debug/205 Release, with ARM64 builds in
both configurations. Existing x86 parser errors and separately unported stack
sizing still prevent x86 execution. The completed definitions retire 298 lines
in native `48547f3d`; multi-register stack recorders and stack-sizing definitions
remain. Evidence is in `artifacts/xarch-stack-local-6840ddea` and the stack-local
retirement records under `artifacts/residual-reconciliation`.

## 2026-09-29: Xarch address-mode sizing

Both address-mode sizing overloads now retain their x86 bodies, including
absolute-EAX MOV shortening, base/index swapping and displacement widths.
AMD64-only SIB and relocation conditions remain target-specific. The two base
register predicates, prefetch query and scale decoder complete the same family.

Full-analysis controls pass Windows 76 Debug/73 Release and Linux-x64 target
5 Debug cases; ARM64 builds. Identical preexisting x86 diagnostics still prevent
execution of the six new x86-specific cases. Separate EVEX compression and tuple
metadata dependencies remain explicit.

Six complete definitions retire 332 lines in native `1c9d0d6d`. Exact evidence
and retirement records are under
`artifacts/residual-reconciliation/*address-sizing*-20260929.json`.

## 2026-09-29: Common constant and descriptor-flag access

Instruction constant access now preserves the complete native target flow,
including the ARM-only frame-address path. ARM64 reads compact and full-width
constants instead of stopping at its Debug stub. TLS, relocation, local-variable
and frame-base flag accessors preserve native behavior and the existing xarch
TLS/custom-bit alias.

Full-analysis controls pass Windows 409 Debug/402 Release and ARM64 69/57.
Seven old-source constant cases fail at the former guard; positive immediate
sanity checks now execute. Twelve native definitions retire 90 lines in
`0cd74028`. ARM FP classification, ARM64 extra-register access and SVE sanity
remain separate dependencies. Evidence is in `artifacts/descriptor-access-packet`
and the descriptor-access retirement records under `artifacts/residual-reconciliation`.

## 2026-09-29: Common peephole eligibility and xarch classification

Move and side-effect classification and the complete Debug relocation checker
now retain their x86 paths. The two instruction-group peephole eligibility
helpers are common to all targets, preserving boundary, no-GC and forced-group
checks rather than duplicating their xarch implementation.

Full-analysis Windows controls pass 163 Debug/157 Release, and ARM64 builds in
both configurations. Existing x86/Wasm parser failures remain independent.
Five definitions and associated declarations retire 274 lines in native
`30e5e933`; distinct other-target sanity definitions and the separate peephole
iterator remain. Evidence is in `artifacts/xarch-classification-common-d40c126`
and `artifacts/residual-reconciliation/classification-*-20260929.json`.

## 2026-09-29: Register-address recording

Six register-address recorders and their direct helpers retain their complete
x86/AMD64 paths, including instruction formats, address operands, vector options,
zero-LEA elision and x86 stack adjustment. Unix AMD64 unary-address recording no
longer stops at the former Windows-only guard.

Full-analysis controls pass Windows 76 Debug/73 Release and Linux-x64 target
5/5. The identical old Linux fixture fails the two newly reachable cases while
its three existing controls pass. ARM64 compiles; identical preexisting x86
diagnostics still prevent execution, and x86 address sizing remains a terminating
dependency rather than an estimated size.

Six definitions and six declarations retire 182 lines in native `fd64b328`.
Previously absent wrappers are not counted again. The component proposal,
combined retirement, applied receipt and reconstruction are under
`artifacts/residual-reconciliation/*register-address*-20260929.json`.

## 2026-09-29: Common call allocation and GC-register encoding

Direct and indirect call allocation now preserve the complete target conditions,
including second-register GC returns, xarch-only displacement selection and
fat-call allocation statistics. Both compact GC-register codecs retain every
fixed-register target mapping. Descriptor constants use the native target/host
width formula, and register setters preserve their native bitfield widths.

Final full-analysis controls pass Windows 282 Debug/276 Release and ARM64 with
emitter statistics 109/100. Four identical old argument-count cases fail at the
former ARM64 guard. The complete codec statement sequences also match pinned
native after explicit representation adaptations.

Eleven complete definitions retire 589 lines in native `7cc9d4e1`. Large-call
layouts outside Windows-host AMD64/ARM64 and separately unported scratch masks
remain terminating dependencies; this is not machine-code execution evidence.
Exact snapshots and codec proof are in `artifacts/call-allocation-packet`;
the retirement manifest, applied receipt and reconstruction are in
`artifacts/residual-reconciliation/call-allocation-*-20260929.json`.

## 2026-09-29: Xarch move analysis and static recording

Accumulator sign-extension folding, redundant-move analysis and push/pop
stack-depth accounting now retain their x86 bodies. Static unary and
register-address recording also preserve x86 sizing and Unix AMD64 paths.
Separate x86 classification and peephole helpers remain explicit dependencies.

Focused full-analysis Windows controls pass 101 Debug/95 Release for move/unary
behavior and 49/45 for static recording. The two Unix static-recording cases
pass in both configurations and fail at the old guard on the previous source.
ARM64 builds remain valid; preexisting x86 diagnostics still prevent execution.

Five complete definitions retire 354 lines in native `5825cada`. Already-absent
static store/immediate recorders are not counted again. The independent packet
excludes ongoing common call-allocation work. Evidence:
`artifacts/residual-reconciliation/move-static-combined-20260929.json`, its
applied receipt, verification and component proposals.

## 2026-09-29: ARM64 instruction sanity and xarch simple recording

The complete ARM64 Debug instruction checker and its immediate, register,
arrangement and conversion predicates are translated. Zero-operand recording
now completes its Debug validation instead of stopping at the architecture
guard. Separate SVE, descriptor and reserved-register dependencies still
terminate explicitly; this does not establish ARM64 machine-code execution.

Both xarch zero-operand overloads, immediate-only, unary and move recording
retain their x86 paths. The immediate-only entry also supports Unix AMD64.
Unported x86 stack-depth and move-elision helpers remain separate dependencies;
existing independent syntax errors still block x86 compilation.

The exact combined overlay passes full-analysis Windows 132 Debug/122 Release
and ARM64 with emitter statistics 98/89. Identical old-source controls fail
all 17 Debug zero-operand cases at the previous sanity guard. Unix immediate
recording passes 2/2, with both old Debug cases failing at its previous guard.
The checker also preserves the pinned statement sequence and all 107 ordered
case labels, including helper evaluation and native shift-bound behavior.

Forty-four complete definitions retire 1,588 lines in native `8a3441f2`, still
the sole oracle child. The shared sanity declaration and distinct unported
helpers remain. Evidence is in `artifacts/arm64-sanity-packet` and
`artifacts/residual-reconciliation/sanity-simple-recording-combined-20260929.json`
with its applied receipt.

## 2026-09-29: Register recording and no-GC call preservation

Four xarch register recorders, three sizing overloads and their K/EVEX/APX
helpers retain both xarch target paths. Common no-GC call classification and
register preservation now cover ARM64 as well as AMD64. Native-named masks
preserve dynamic register configuration and profiler/write-barrier behavior;
unimplemented other-target masks terminate explicitly.

The combined isolated batch passes full-analysis Windows 16 Debug/15 Release.
Corrected mask controls pass Windows 2/2, ARM64 1/1 and Unix AMD64 1/1. The old
ARM64 fixture reaches the original target guard. Existing independent x86
diagnostics still block that target; no x86 execution or new runtime parity is
claimed.

Nineteen complete definitions retire 622 exact-span lines plus two exposed
trailing separators: 624 pure deletions in native `8249af01`, still the sole
oracle child. The move recorder and x86 Debug sanity checker remain separate
dependencies. This batch does not wait for the larger ARM64 sanity translation.

Evidence: `artifacts/register-nogc-integrated` and
`artifacts/residual-reconciliation/register-nogc-combined-20260929.json`
with its applied receipt and exact reconstruction results.

## 2026-09-29: Stack GC accounting, call recording and ARM64 descriptor sizes

Stack push/pop, argument killing and register-mask death retain the native
general/JIT32 encoder paths. Large-stack counting preserves safe-integer
overflow behavior, including each Debug assertion when MinOpts continues and
the EE ignores an assertion. The general encoder can record a zero-count call
while using simple stack tracking. Existing pre-write tracking-table checks
remain in place.

Xarch call recording and indirect-call displacement access retain the x86
branches, including host-width conversion of unsigned stack-level bits. The
shared call declaration and generic allocator definitions remain native for
their unported target paths; x86 descriptor allocation remains explicit.

ARM64 descriptor sizing now dispatches on native operand categories and payload
flags, including local-variable pairs, jumps, calls and alignment. Within-group
traversal no longer stops at the size dependency. Pinned inputs generate all
599 operand categories and the ARM64-specific operand enum. Independent native
macro expansion matches every row; scoped MSVC layout probes establish the
Windows 64-bit-host sizes, including 80-byte ARM64 fat calls. Non-Windows ARM64
fat-call layouts remain unsupported.

The combined full-analysis matrix passes Windows 183 Debug/157 Release and
ARM64 41/41. Separate Linux-target ARM64 size contracts pass 34/34, including
the explicit unsupported fat-call layout. The final stack delta passes Windows
27 Debug/19 Release and ARM64 6/6; unchanged descriptor/call evidence is reused.
Old-source controls
expose the prior size guards, stack guards, missing safe-integer assertions and
simple-stack call assertion.

Twelve complete functions, two descriptor types and both complete ARM64 format
headers retire 1,601 native lines. Retirement records translation, not new
runtime or whole-dump parity. ARM64 Debug instruction sanity checking remains
the next recording dependency; independent x86 compilation blockers remain.

Evidence: `artifacts/emitter-stack-call-size-integrated`,
`artifacts/arm64-descriptor-size-packet`, and
`artifacts/residual-reconciliation/stack-call-size-combined-20260929.json`
with its applied and final-verification receipts. Component proposals retain
the bounded controls and native-source contracts.

## 2026-09-29: GC tracking, branch output and descriptor locations

GC live-set and call tracking retain their JIT32/general-encoder and Unix paths.
ARM64 now records stack-slot lifetimes, register death-before-birth transitions
and call descriptors. Shared descriptor traversal preserves active/saved storage,
empty groups and captured end positions; location queries include ARM64
replacement instructions that moved into the next group.

Branch output preserves native host-width signed displacement storage and
unsigned jump-size arithmetic. A backward logical jump can have a positive
physical displacement when hot/cold buffers are reversed; it now remains long
rather than being incorrectly shortened. Prolog offsets preserve unsigned
wrapping, call diagnostics print unsigned stack-level bits, and location/offset
failures retain native recoverable-error and MinOpts continuation policies.

Combined full-analysis checks pass Windows 231 Debug/209 Release, ARM64 13/13
and Linux-target 2/2, without exclusions or skips. The final offset-truncation
delta passes a focused 10 Debug/9 Release call-output suite. Identical old-source
controls expose the prior guards, arithmetic, diagnostics and recovery failures.
A file-local disabled-GC compilation control also catches the misplaced method
brace; it is not evidence of full no-GC or Wasm support.

Thirty-one complete definitions and associated scaffolding retire 1,777 native
lines in `2c4db8b0`, still the sole oracle child. Non-AMD64 descriptor sizes and
unwind-NOP encoders remain explicit dependencies: ARM64 traversal across
one-descriptor groups works, while within-group advancement reaches sizing.
x86 still has independent declaration/type blockers; duplicate declarations
were removed without claiming x86 execution. No new runtime or GC-encoding
parity is claimed.

Evidence: `artifacts/emitter-gc-branch-location-integrated` and
`artifacts/residual-reconciliation/gc-branch-location-combined-20260929.json`,
its applied receipt and the final-verification receipt. Component proposals
retain the focused old-source controls.

## 2026-09-29: GC bookkeeping, output dispatch and ARM64 recording

Frame/NoGC/epilog bookkeeping and the three-register/main output dispatchers
retain their whole target paths. ARM64 zero-operand recording now preserves
authentication/return formats, native operand/option/format widths, code sizes
and memory-barrier tracking. Release label padding records the required nop or
breakpoint and advances instruction groups. Debug still terminates at the
separate unported sanity checker; this is not ARM64 machine-code execution.

Pinned table inputs and their generator now produce ARM64 and SVE metadata.
Independent native macro expansion verifies all 599 format ordinals and 1,158
instruction names, formats and info bytes, including loop alignment and excluding
synthetic `lea`. Native flags are preserved, not reinterpreted.

Integrated Windows checks pass 187 Debug/174 Release with and without statistics.
Unexcluded ARM64 checks pass 78 per configuration, or 83 Debug/84 Release with
statistics. Three identical old allocation/label cases fail per configuration
at the prior target guards. The integration also repairs missing non-xarch guards
in the preceding register/immediate packet; earlier ARM64 component checks
predated that integration or explicitly excluded those files. No such exclusion
is used in the accepted combined build. x86 fixtures remain unexecuted behind
independent declaration/type errors.

Twenty-two complete definitions, the instruction-info table/constants and their
associated scaffolding retire 2,486 lines in `af6f32b5`, still the sole oracle
child. Distinct sanity/display/traversal helpers, ARM32 memory classification,
other-target format accessors and header operand-category modes remain native.
Evidence: `artifacts/residual-reconciliation/bookkeeping-dispatch-arm64-combined-20260929.json`
and its applied receipt; `artifacts/arm64-recording-packet` contains the native
metadata probe and exact integration/negative-control evidence.

## 2026-09-29: Lifecycle, label contexts and register/immediate output

Prolog/epilog and placeholder lifecycle retains target-specific stack and GC
bookkeeping, including placeholder statistics and unsigned JIT32 offset
calculations. Ordinary and inline labels now retain their non-AMD64 paths;
cookie lookup and call/NoGC descriptor contexts are target-neutral. Label padding
selects the native target breakpoint rather than an x86-only opcode. Register
and immediate byte output retains its x86 compact forms, widths and relocations.

Integrated Windows checks pass 182 Debug/174 Release. ARM64 label/cookie checks
pass 27 per configuration; lifecycle checks pass seven, or eight with statistics.
Scoped Windows lifecycle statistics checks pass 41 Debug/38 Release. Identical
old-source fixtures fail at the original target guards or missing methods.
ARM64 padding and prolog/epilog cases reaching retained recording/codegen
dependencies establish those boundaries, not generated instruction execution.
The x86 fixture bypasses unported recording entrypoints but remains unexecuted
while independent target build blockers remain.

Twenty-six complete definitions retire 1,734 body lines, with 139 own
heading/comment/guard lines, 33 declarations and six constants: 1,912 total in
`0dcfd18e`, still the sole oracle child. The cookie getter's native body was
already absent; its remaining declaration adds no new body credit. General
statistics, x86 epilog-list encoding and distinct target dependencies remain.
Evidence: `artifacts/residual-reconciliation/lifecycle-label-output-combined-20260929.json`
and its applied receipt.

## 2026-09-29: Allocation, static layouts and address/stack output

Emitter allocation now retains target and statistics paths while preserving
managed descriptor ownership. Static reporting uses native logical sizes for
proved Windows AMD64/ARM64 feature layouts, including the distinct 72/80-byte
call descriptors. Unsupported layouts terminate. Address and stack byte output
retain their whole target paths, including x86 widths, prefixes, relocations and
GC ordering; unported encoding dependencies remain explicit.

Integrated Windows checks pass 226 Debug/218 Release and, with statistics,
230 Debug/222 Release. Scoped Linux address/stack checks pass three per
configuration, with two old-source stack-output guard failures and one unchanged
address control. ARM64 allocation/init checks pass four per configuration with
and without statistics, reaching a separate typed operand-size dependency.
Numeric static-report cases pass Windows AMD64/ARM64 Debug and Release.

MSVC declaration-layout probes establish the reported sizes and offsets, not
full native-header or runtime parity. Independent x86 declaration/type errors
still prevent that target's fixtures from running. General statistics reporting,
prolog/epilog and distinct operand-size/encoding dependencies remain native.

Five complete definitions retire 1,888 body lines, ten own heading lines and
six declaration/friend lines: 1,904 total in `f46405d5`, still the sole oracle
child. Evidence: `artifacts/residual-reconciliation/allocation-address-output-combined-20260929.json`
and its applied receipt.

## 2026-09-29: Static-field output and CodeGen target helpers

Static-field output now retains its Unix AMD64 and x86 paths, including absolute
accumulator addresses, width selection, displacement/immediate ordering and
relocations. Temporary labels no longer reject non-AMD64 targets and retain the
Unix-x86 nested-alignment adjustment. Local-register masks preserve ARM
double-register pairs through the translated floating-mask helpers.

Integrated Windows checks pass 197 Debug/193 Release; ARM64 label/mask checks
pass 19 per configuration and Linux-x64 static-output checks pass five per
configuration. Identical old-source fixtures fail at the original target guards:
three ARM64 label cases in both configurations and five Linux output cases in
Debug. ARM32/Unix-x86 builds remain blocked by preexisting parser/platform
errors, and the separate x86 output fixture remains blocked by its known
declaration errors. No target runtime or generated-code parity is inferred.

Five complete definitions retire 549 body lines and five associated declaration
lines in `01edbed9`, still the sole oracle child. Distinct x86 data-offset,
unprefixed REX.W, alignment-mutating and other backend dependencies remain.
Evidence: `artifacts/residual-reconciliation/static-output-codegen-combined-20260929.json`
and its applied receipt.

## 2026-09-29: Method initialization and sizing closure

Method entry now retains native target-specific resets, backward navigation
and method-local statistics initialization. Group allocation accounts for the
supported native x64/ARM64 layouts rather than managed object sizes; unsupported
layouts terminate explicitly. The xarch sizing/prefix family now includes
complete instruction-size adjustment, signed-byte immediate restrictions and
EVEX/REX2 decisions.

Integrated full-analysis Windows checks pass 204 Debug/183 Release. Scoped
ARM64 checks pass three cases per configuration, statistics one per configuration,
and ARM64 statistics/backward-navigation three Debug cases each. Both new ARM64
frame-pointer cases fail at the original method-entry stub on `15354a9`, then
pass after restoration. Independent x86 declarations still prevent that target's
fixture execution; there is no x86 compilation or generated-code parity claim.

Twelve definitions retire 575 body lines, with five own comment/blank lines and
35 associated declaration lines: 615 total in `2a41b4dd`. Thirteen declarations
complete cleanup for the preceding accepted static-field packets, not additional
implementation. Native remains the sole oracle child. General allocation,
statistics reporting and static-field output dependencies remain separate.
Evidence: `artifacts/residual-reconciliation/emitter-init-prefix-combined-20260929.json`
and its applied receipt.

## 2026-09-28: Group transitions and static-field recording

Group preparation and transitions now retain the native target buffer sizes,
reset/GC behavior and optional allocation/extension statistics. Static-field
store recording retains its x86 size restriction and EAX addressing case;
distinct unported x86 helpers remain typed terminating dependencies.

The combined packet passes 189 Debug/174 Release integrated Windows cases.
Scoped ARM64 and statistics checks pass three and five cases respectively in
each configuration. Both new ARM64 transition cases fail at the original
whole-target stub on `9d035f9` in Debug and Release, then pass after restoration.
The x86 baseline and repaired builds have the same seventeen
independent declaration/type errors after identical parser overlays; neither is
an x86 compilation or execution pass. LoongArch64 also remains blocked by
unrelated hardware-intrinsic platform directives.

Eight complete definitions plus associated declarations/comments retire 266
native lines in `c6188147`, with no duplicate-cleanup credit. Native remains the
sole child of the pinned oracle. General allocation, method-entry reset and
the unported x86 helper bodies remain visible in the residual.
Evidence: `artifacts/residual-reconciliation/emitter-recording-combined-20260928.json`
and its applied receipt.

The next dependency packet completes ten register-restriction, static-field
sizing and prefix helpers, retiring another 311 definition lines in `bf2ee593`.
Windows baseline/final controls remain 176 Debug/155 Release. The x86 fixture is
linked with the correct target symbol, but the existing declaration errors still
prevent compilation and execution. Remaining instruction-size, EVEX/REX2 and
diagnostic/output dependencies stay explicit. Evidence:
`artifacts/residual-reconciliation/xarch-static-field-dependencies-proposed-20260928.json`.

## 2026-09-28: Runtime vector length and type-size utilities

Runtime `Vector<T>` length now uses the known compile-time width or the native
EE metadata lookup. The utility packet also completes unsigned SIMD element
mapping, conditional SIMD rounding and maximum-type selection. GPR and SIMD
rounding preserve native unsigned input bits; invalid zero-size GPR types
terminate instead of returning an undefined type.

Nine boundary regressions fail before correction in both configurations.
Final Windows utility checks pass 50 Debug/51 Release; unaffected ARM64,
classification and runtime-length evidence is reused. Ordinary boundaries and
the native Release zero-size maximum-type result remain unchanged.
Thirteen complete native definitions retire 152 lines in `53af680b`, still the
sole oracle child. This is source/contract closure, not generated-code parity.
See B460 and `artifacts/residual-reconciliation/simd-type-size-cluster-proposed-20260928.json`
with its applied receipt.

## 2026-09-28: Complete instruction-group saving

`emitSavIG` now retains its ARM barrier reset, optional native-width statistics,
xarch-only removable-jump marking and conditional backward navigation instead
of rejecting every non-AMD64 or statistics-enabled configuration. The established
managed descriptor storage and saved-reference representation is unchanged.

Focused full-analysis checks pass 23 Windows cases and one ARM64 save-contract
case per configuration, plus one statistics-enabled case per configuration.
The Release Windows/statistics builds exclude unrelated untracked utility-test
WIP, not emitter tests. The complete native definition, comment and declaration
retire 353 lines in `857083f7`, still the sole child of the pinned oracle.
Group initialization and static statistics reporting remain explicit dependencies;
no generated ARM64 execution is claimed. Exact source hashes, results and removal
spans are recorded in `artifacts/residual-reconciliation/emit-sav-ig-pending-20260928.json`
and `emit-sav-ig-applied-20260928.json`.

## 2026-09-28: ARM64 folding dependencies and intrinsic-layout retirement

The folding helper's two retained dependencies are now implemented: all twelve
SVE mask mappings, and narrowing/saturation/duplication for all ten element types
at both 8- and 16-byte SIMD widths. Intrinsic aggregate-layout dispatch also
retains all native cases; its SVE cases call a terminating runtime-vector-length
dependency rather than replacing the caller branch with NYI.

Combined full-analysis ARM64 checks pass 72 Debug/72 Release. The reviewed
definitions and declaration remove 183 native lines; a separately identified
duplicate layout body/comment removes another 81 lines. Native `59f2f2d4` remains
exactly one commit above the pinned oracle. Runtime vector-length resolution stays
visible in the residual, and no ARM64 generated-code execution is claimed.

## 2026-09-28: All remaining merged-array variants execute

Both primary JITs pass all 41 selected merged-array variants from eleven sources
across the five official Methodical runners. Captures verify identical assembly
and host hashes, selected test identities, passing result XML, exit100, positive
selected-body compilation and no NYI completions. This supplements the earlier
59 standalone array-project results; it is regular runtime execution evidence,
not phase-dump or generated-code parity.

The managed compiler is the immutable `447f6b3` snapshot plus the SSA ownership
correction, not a build containing later ARM64/Wasm work. Reproduction and exact
identities are in `artifacts/official-array-merged/comparison-ssa-owner.json`.

## 2026-09-28: SSA dead-store ownership after statement removal

VN-based dead-store removal no longer assumes that an SSA definition's original
block still owns its store. Induction-variable optimization can remove that
statement while retaining the definition. The managed replacement now finds a
moved owning use or handles an already-detached definition, preserving native
NOP allocation, SSA value numbers, threading and effects.

Four regression cases fail before correction in both configurations; focused
DSE/SSA/induction coverage passes 42 Debug/40 Release. The original
`Grisu3.TryDigitGenShortest` primary-compilation failure is resolved, and the
official d1 lcs runner completes all eight selected tests and its result XML.
This does not yet establish the remaining merged-array variants or dump/code
parity. See B459 and `artifacts/official-array-merged/ssa-owner-evidence.json`.

## 2026-09-28: Complete hardware-folding body and native retirement

`gtFoldExprHWIntrinsic` now preserves its complete native target control flow
instead of replacing ARM64 with a whole-helper exception. This includes scalar
bit operations, vector constants, scalable mask conversion and conditional
selection. Two separate ARM64 dependencies still terminate explicitly:
`GetMaskVariant` and `NarrowAndDuplicateSimdLong`.

Full-analysis target checks pass 15 Debug/15 Release ARM64 cases and fresh Windows
folding controls pass 637 Debug/629 Release. The completed 2,036-line native helper
is retired with its leading comment and declaration: 2,046 removed lines in native
commit `366e9f52`. Both unimplemented dependency definitions remain in the residual.
This is whole-function translation and focused contract evidence, not ARM64
execution or a compiler-completion percentage.

## 2026-09-28: Native-width address folding

Address-mode construction now preserves the native signed-32 candidate guards
before accumulating constants. Array-index scales, shifts and products retain
native width and wrapping behavior rather than narrowing to 32 bits or throwing
in checked builds. Fifty new boundary cases pass alongside existing address and
arithmetic lowering coverage: 190 Debug/190 Release with full analysis.

The pinned native merged-array runners pass all 41 selected variants. The managed
primary JIT now compiles `TimeZoneInfo.TransitionTimeToDateTime`, which previously
aborted with overflow. Execution advances through all eight d1 lcs test-pass
messages, then fails while compiling the runner's number-formatting dependency
in SSA dead-store removal (B459), before final result XML. This is not a completed
managed merged-runner pass or dump/code parity. The isolated source/JIT identity
and reproduction evidence are in `artifacts/official-array-merged`.

## 2026-09-28: Whole-function folding dispatch and native work-list repair

Expression folding now retains the native hardware-intrinsic dispatch on all
applicable targets, reaching a terminating dependency stub where the folding
body is unported. Windows behavior is unchanged. A pre-fix ARM64 dispatch failure
and tier-zero control are covered; integrated checks pass 24 Debug/24 Release
ARM64 cases and 376 Debug/367 Release Windows cases.

The completed dispatcher is removed from the native remainder. The unfinished
hardware-fold helper, which had incorrectly been absent, is restored exactly
from the pinned oracle. Completed callers no longer wait for other-target
dependencies to execute; those dependencies remain visible as untranslated work.
The helper's remaining algorithms and Wasm call-argument compilation are separate
active repairs.

## 2026-09-28: Runtime type comparison argument selection

Type comparison folding now selects and counts user arguments, matching the
pinned native contract when special arguments precede a type handle. Coverage
includes handle/null, handle/handle and handle/GetType comparisons in both
operand orders, preserving EE query handles, comparison flags and object null
checks. Twelve failures and twelve controls in each configuration become 24
passing cases; broader folding coverage passes 306 Debug and 299 Release cases.
Normal Windows reachability of the special-argument prefix remains unestablished;
this is contract evidence, not a generated-code failure or parity claim. See B455.

## 2026-09-28: Remaining standalone array execution and importer spills

Both primary JITs now pass the remaining 17 `JIT/Methodical/Arrays` standalone
projects: two longest-common-subsequence tests and fifteen rank-32 array tests,
including the separate root-level `huge_struct.ilproj`.
Together with the earlier range and miscellaneous slices, all 59 standalone
projects have matching native/managed execution results. Nine separately merged
C# tests remain outside that coverage.

The object-array case exposed unary branch access through the binary accessor.
The same mismatch is corrected for switch spills, discarded casts and hoist
annotations. Switch spilling also now updates the actual selector slot before
its old stack temp is overwritten; the previous by-value assignment failed the
Release IR regression. Ten Debug failures and one Release failure are corrected;
final focused coverage passes 199 Debug and 179 Release cases.

Entry compilations, exits and output match the pinned native baseline. This is
bounded execution and IR-contract evidence, not full dump or generated-byte
parity. See B453/B454.

## 2026-09-28: First matching operand edges

Operand lookup now returns the first matching writable slot in PHI, field-list,
array-index, hardware and call argument containers. It preserves native control
expression precedence, per-argument early/late ordering and hardware storage
order even when execution order is reversed.

Nine cases fail before correction; 119 Debug and 119 Release use-edge/LIR cases
pass afterward. Native LIR validation rejects multiply-used value nodes, so this
closes B452's generic lookup contract without claiming a valid Windows LIR
miscompilation or new execution parity.

## 2026-09-28: Array initializer numeric and field-token contracts

Array initialization now preserves native unsigned32 element and byte counts,
rejects invalid host-sized narrowing, and carries overflow through the original
element-type query. Existing EE argument bits and unsigned block layouts are
unchanged. Both array and span intrinsic importers recognize indirect field
tokens through the correct unary-node accessor.

Twenty regressions fail before correction; final focused coverage passes
170 Debug and 167 Release cases. Thirteen boundary results match the pinned
native safe-integer implementation, and all four official initializer projects
retain matching primary-JIT execution and output. Large-size cases validate IR
and callback contracts without allocating large arrays. See B450/B451.

## 2026-09-28: Official miscellaneous-array execution

All 21 standalone `JIT/Methodical/Arrays/misc` projects pass with both primary
JITs, including priority-one GC finalization/resurrection and initialization
cases. Exit codes and output match; C# cases require compilation of the actual
`TestEntryPoint`, not just a wrapper.

The optimized explicit-lower-bound initializer exposed B449: a stale comma
alias made the importer revisit a dimension's length argument. The corrected
walk advances from its current position and retains rank-one array layout
selection. Six regression failures and three controls establish the defect;
final focused coverage passes 146 Debug and 143 Release cases.

This excludes the separately merged `IndexingSideEffects` test and full-suite
or whole-dump parity. Adjacent unsigned-size and indirect-token findings are
tracked separately as B450/B451.

## 2026-09-28: Official array-range execution and comparison import

All 21 standalone `JIT/Methodical/Arrays/range` projects pass with the pinned
native and managed primary JITs, with matching output and positive entry
compilations. The first managed run exposed B448: comparison import checked
cached operand types from before native-int widening and rejected valid IL.

Both value and branch import now validate the widened operands while preserving
their distinct native extension rules. Sixteen regressions reproduce the old
Debug assertion; focused coverage passes 111 Debug and 111 Release cases.
This is a bounded official execution result, not full-suite or whole-dump parity.
See `checkpoint.importerComparisonTypes`.

## 2026-09-28: Emitted-code size statistics completed

The optional Windows-x64 `DISPLAY_SIZES` mode now builds and reports emitted
IL, native code/data and GC-information totals, including interruptible and
non-interruptible method counts. It preserves native unsigned arithmetic,
formatting and shutdown order.

These are emitted payload sizes, not estimates of managed compiler-object
allocations. Native JIT32 header/map recording remains a separate target boundary.
The option stays disabled by default; see `checkpoint.emittedSizeStatistics`.

## 2026-09-28: Shared diagnostic counters completed

Optional scalar and node counters preserve signed64 output, atomic32 recording,
unsigned count ordering and opcode tie breaks. Generated canonical opcode names
are now available under the native statistics guards; enum aliases no longer
change labels such as `CNS_INT`.

Combined statistics coverage passes 157 Debug and 105 Release cases, including
nine counter cases; default controls pass 125 and 73. Of 48 regenerated files,
only the intended name-table guard changes. This completes shared support, not
native node-size accounting or all optional statistics consumers.

## 2026-09-28: Fatal-error statistics completed

`MEASURE_FATAL` now compiles in Release and emits the native shutdown report.
Counters preserve unsigned wraparound, Debug-only argument-body accounting and
the original compilation error results.

The three completed statistics modes also work together: combined coverage passes
148 Debug and 96 Release cases, with 125 and 73 default-configuration controls.
They remain disabled by default. This closes B445, not native execution parity
or the other allocation-size/statistics modes.

## 2026-09-28: Assertion occurrence reporting completed

Optional `MEASURE_NOWAY` reports now collect and rank executed assertions,
including successful ones, with stdout and append-file output. Original caller
locations are retained instead of merging assertions at the recording wrapper.
Native hash traversal and sorting are preserved for equal-count rows.

Enabled statistics, error, liveness and shutdown coverage passes 59 Debug and
53 Release cases; default controls pass 42 and 36. Both configurations reproduce
the old caller-location defect before correction. The option remains disabled
by default, and native optional-map concurrency quirks remain unchanged.
See B444 and `checkpoint.nowayStatistics`.

## 2026-09-28: Optional basic-block statistics completed

The `COUNT_BASIC_BLOCKS` configuration now records block counts, single-block IL
sizes and reachability convergence iterations, and emits their native histogram
layouts during shutdown. Shared histogram support includes atomic counters,
unsigned wraparound, the 64-counter limit and the overflow bucket.

Enabled coverage passes 98 Debug and 46 Release cases; default-configuration
controls pass 95 and 43. The two convergence regressions fail with the original
recording omission. Statistics remain disabled by default; this is not new
generated-code or native execution evidence. See B385 and
`checkpoint.blockCountStatistics`.

## 2026-09-28: SIMD length support completed

Both native SIMD-length overloads are implemented, including unsigned byte-size
division and type-handle recognition with its existing SIMD-use tracking.
Focused SIMD recognition coverage passes 79 Debug and 79 Release cases. The
type-handle overload has no pinned native callers; this completes retained
support, not a newly reachable code-generation path.

## 2026-09-28: Concurrent and reentrant primary compilation

The managed entry no longer serializes all compilations for debugging. Shared
diagnostic buffers are synchronized, and Debug TLS cleanup now stays on the
owning thread rather than running from a GC finalizer.

Pinned native and managed primary JITs each execute 512 independently calculated
results from 32 cold methods prepared by eight workers. Function traces show
eight overlapping compilations, compared with one in the same-source locked
control. Both compilers also execute a same-thread assembly-resolution callback
whose method is JIT-compiled while the outer method is still being imported.

This closes the temporary serialization limitation, not every optional-mode
race or whole-runtime parity question. Evidence: `checkpoint.compilerConcurrency`,
B002, B442/B443 and R001.

## 2026-09-28: Integral narrowing executes across x64 ISA levels

Vector128/256/512 integral narrowing now preserves truncation and source-lane
order under default, AVX2 and legacy instruction selection. The AVX2 64-to-32
path was incorrectly selecting double-to-float conversion; restoring the native
unpack/permute construction fixes the generated values. The same comparison
also restored native unsigned permute metadata for 32-to-16 narrowing.

Each native/managed primary pair checks 14,112 independently computed lanes
and matches 54 raw phase slices and 18 complete instruction traces. These are
selected integral narrowing results, not exhaustive SIMD or machine-byte parity.
Evidence: `checkpoint.avx2IntegerNarrowing` and B437.

## 2026-09-28: Complete instruction comparison corrects TLS evidence

The recent primary-JIT evidence now includes hexadecimal instruction IDs and
checks each scheduler stream against its reported count. Across 19 preserved
capture pairs, 94 of 100 body comparisons match. Five known async-adapter TLS
differences remain; the worker-thread naming method also has three TLS
instructions that the old decimal-only selector omitted. Its earlier
instruction-equality claim was incorrect. These operands preserve the EE's
differing assigned slots, not a newly established compiler defect.

The recheck preserves original execution status and missing Tier1 bodies; it
does not rerun or relabel captures. Evidence: `checkpoint.completeInstructionRecheck`
and B417.

## 2026-09-28: AMD64 Debug emitter payloads are available

The native optional emitter-test dispatcher and all six AMD64 synthetic payloads
are implemented at the last-block callsite. Section selection, diagnostics and
the branch that skips the payload preserve native behavior.

The primary managed JIT executes the existing Add corpus with SSE2 injection.
Its complete 41-instruction, 146-byte stream and import/morph/cost trees match
native. The other sections have enabled descriptor-recording coverage, not an
encoding-parity claim. Native default `all` requires promoted-EVEX configuration
and asserts without it. Other-target payloads remain unsupported.

Evidence: `checkpoint.amd64EmitterPayloads` and D007.

## 2026-09-28: Primary class profiling and selected value profiling execute

Interface and virtual dispatch now run with actual class-profile probes under
the managed primary JIT. All five selected Tier0 instruction streams match
native. The profiled callers' morph/cost trees differ only at their raw
profile-buffer addresses; these remain visible rather than normalized away.

The corrected JIT also executes both optimized Copy/Equal value-probe bodies
through the established selected-method path, with native-equal probe counts
and instruction streams. This does not close the separate primary-JIT timing
boundary: that fixed-duration program still exits before the selected managed
instrumented Tier1 bodies appear.

Evidence: `checkpoint.primaryClassProfiles` and `checkpoint.primaryPgoImport`.

## 2026-09-28: Primary tiered-PGO startup no longer crashes

Profile-guided call specialization now rebinds the selected argument edge rather
than writing through a null managed reference. Guarded devirtualization restores
native zero-initialized guesses and the generic-virtual exclusion, preventing
method-only profiles from passing garbage class handles to the EE.

The existing value-profile program now completes with the managed primary JIT.
Its two common Tier0 bodies match native trees and instructions. The native run
also reaches instrumented Tier1 bodies that the managed run does not reach before
this fixed-duration corpus exits; those absent bodies are not parity passes.
Focused regressions and the precise runtime boundary are recorded in
`checkpoint.primaryPgoImport` and `artifacts/primary-value-profile`.

## 2026-09-28: Primary GC-stress and OSR controls match

The existing allocation corpus completes under the primary managed JIT with
GCStress=4 in both FullOpts and MinOpts. Its six selected bodies in each mode
match native import/morph/cost trees and complete instructions.

The strengthened OSR corpus also completes with actual Tier0-to-OSR transitions
for both the hot loop and generic-context loop, preserving their once-only
initialization checks. All six selected bodies match native trees and
instructions, including both OSR entry variants. This uses the established
TieredPGO-disabled configuration; broader tiered-PGO and runtime-suite results
remain separate.

Evidence: `checkpoint.primaryObjectGcStress` and `checkpoint.primaryOsr`.

## 2026-09-28: Primary EH, allocation and intrinsic controls match

Primary-JIT execution now also completes the existing catch/finally,
filter/nested-finally, object-allocation and hardware-intrinsic corpora. All 19
selected bodies match native import, global-morph and operation-cost trees and
complete instruction streams. Both EH programs report 11 Gen2/helper collections.
The eight native/managed processes exit successfully with identical output.
These are scoped controls, not whole-dump or official runtime-suite results.

Evidence: `checkpoint.primaryEhObjectsAndHardware`; raw captures and comparisons
are under `artifacts/primary-{eh,filters,objects,hardware}`.

## 2026-09-28: Primary runtime-async and GC-loop execution complete

Box cleanup now retains the morph state of already-processed allocation/copy
statements and updates the live statement when narrowing a struct source. The
runtime-async corpus completes under the primary managed JIT, including
thread-pool startup, suspension, exception handling and AsyncLocal context checks.
All six transformed async bodies match native. Five Task adapters and the
527-byte worker-thread naming method retain dynamic thread-static index/offset
differences, recorded separately rather than counted as instruction parity.
The naming-method correction comes from B417's complete instruction recheck.

The existing GC-loop corpus also completes under both primary JITs, reports 12
Gen2/helper collections, and matches native import/morph/cost trees and complete
instructions for all four selected bodies. These remain bounded corpus results,
not official runtime-suite or whole-dump parity.

Evidence: `checkpoint.boxCleanupAndPrimaryAsync`, `checkpoint.primaryGcLoops`,
and reports under `artifacts/box-cleanup` and `artifacts/primary-gc-loops`.

## 2026-09-28: Standard and array primary-JIT execution match

The existing standard and array/runtime-lookup corpora now also complete with the
managed compiler as the primary JIT, including startup and teardown, without
AltJIT. All 19 standard bodies and both array/lookup bodies have native-equal
post-import, post-global-morph and post-cost trees and complete instruction
streams. This extends the arithmetic scenario to synchronization, generic EH,
P/Invoke and reverse P/Invoke, implicit-byref arguments, multidimensional arrays
and generic type lookup. Whole dumps and the official runtime suite remain
separate.

Evidence: `checkpoint.primaryStandardAndArray` and the comparison reports under
`artifacts/primary-standard` and `artifacts/primary-array`.

## 2026-09-28: Runtime method tree diagnostics match

Early `isinst` expansion now preserves Windows native node-construction order,
and struct-array address dumps no longer insert an extra separator. The reflection
invoker's complete post-import trees and ManifestBuilder's complete post-promotion
trees now match native, including node IDs and whitespace. Their complete
1502-byte and 6959-byte instruction streams remain identical. The primary
arithmetic process still completes without AltJIT; this does not establish
whole-dump or general runtime-suite parity.

Evidence: `checkpoint.runtimeTreeDiagnostics` and
`artifacts/runtime-tree-diagnostics/comparison-1.json`.

## 2026-09-28: Monitor address-folding parity restored

The arithmetic dispatcher now includes byref types in constant reassociation,
matching native. `Monitor.Enter` folds its opposing field offsets while retaining
the field annotation; post-global-morph and post-cost trees, including node IDs,
and the complete 150-byte instruction stream now match native exactly. The prior
managed body was 153 bytes. The primary arithmetic process still completes all
27 checks and exits successfully without AltJIT. This is scoped method parity,
not a whole-dump or runtime-suite result.

Evidence: `checkpoint.addressConstantReassociation` and
`artifacts/address-folding/comparison-1.json`.

## 2026-09-28: Primary-JIT startup and teardown complete

The arithmetic probe now completes as a primary-managed-JIT process, including
runtime startup and shutdown, without AltJIT fallback. Loop insertion now accepts
a selected preferred block even when no fallback was needed. The teardown
method's complete post-loop trees and emitted instructions match native.

This establishes one end-to-end Windows x64 primary-JIT scenario, not general
runtime-suite coverage. Known address-folding and dump-order/formatting
differences remain active port work.

## 2026-09-28: Primary-JIT execution reaches application code

The managed compiler can now take the primary-JIT role through Windows runtime
startup and execute the arithmetic probe, rather than compiling only selected
AltJIT methods. Intrinsic cancellation preserves shared call ownership, operation
costs retain native-width arithmetic, and importer/EH/range/promotion corrections
allow the required framework methods to compile.

Complete instruction streams match the native event-provider callback,
reflection invoker and manifest builder captures. Whole-tree and dump parity
remain incomplete. Process teardown still fails while finding a loop insertion
point in `AssemblyLoadContext.OnProcessExit`, so this is not yet a successful
primary-JIT process run or runtime-suite result.

Residual cleanup also removed complete forward-substitution, Boolean optimization
and mask-local conversion units using their existing committed port mappings.

## 2026-09-28: Scalable element lookup and unary folding

Scalable integral element lookup now follows native raw 64-bit arithmetic.
Unary folding preserves repeated, sequence or scalar representation when
possible, including exact floating bits and signed zero; unsupported narrow
leading-zero counts and unrepresentable upper lanes leave the tree unchanged.

Full-analysis managed ARM64 coverage passes 145 Debug/128 Release cases and
Windows folding/tree controls pass 76 Debug/76 Release. Ordinary native
GetElement/SetElement and broadcast helpers also lack scalable support, so those
managed gates remain. Public allocation and ARM64 execution/parity stay separate.

Native retirement has advanced through `d1ac17a`, removing 1551 lines since the
recovered boundary. Separately, 370 lines restore the prematurely removed
`gtDispConst` and its declaration because scalable-mask dumping is unfinished.
The restored function matches the pinned oracle. Neither removal counts nor
a clean tree establish catch-up.

## 2026-09-28: Scalable vector constant dumps

ARM64 constant dumps now print repeated, sequence and scalar values in the native
three-element format, including element-width integer wraparound, float-precision
arithmetic, signed zero and NaNs. Floating text reuses the existing native-style
significant-zero formatter.

Full-analysis coverage passes 57 ARM64 Debug cases, including 16 exact-output
dump cases, and 110 Windows formatting/constant controls. All 40 ARM64 Release
metadata controls pass; dump code is Debug-only. This is constant-dump coverage,
not complete ARM64 phase-dump or generated-code parity.

## 2026-09-28: Scalable IR and allocator constants

ARM64 scalable constants now preserve their payload through queries, equality,
cloning, zero/all-ones/byte-pattern factories and debug hashing. LSRA reserves
zero, one or two integer temporaries according to repeated, sequence or scalar
encoding constraints. Fixed-vector consumers explicitly reject unsupported
scalable operations instead of reading unrelated fixed-size storage.

Full-analysis coverage passes 160 Debug/154 Release managed ARM64 target cases
and 290 Debug/286 Release Windows controls. Scalable constant dumps, element
operations/folding, VN/assertion storage and masks remain incomplete; public
allocation and ARM64 generated-code execution/parity remain gated.

Parallel native cleanup advanced to `ec9d37b6`, retiring 785 lines of complete
consecutive-register allocation and scalable encoding support. New commits now
receive explicit retirement dispositions; mixed-path and unreviewed remainders
are distinguished. Removal is still not fully caught up.

## 2026-09-28: ARM64 scalable constant model

Scalable vector values now have a standalone representation for repeated,
sequence and scalar forms. Decoding preserves signed extension, unsigned-64
immediate limits and floating-point payload bits; encoding predicates reproduce
ARM64 repeated, sequence and scalar immediate constraints.

Full-analysis model/metadata coverage passes 69 Debug/69 Release cases on the
Windows-host ARM64 target. Tree storage and consumers, LSRA temporary selection
and public allocator activation remain separate integration work. No ARM64
generated-code execution or parity is claimed.

## 2026-09-28: Native remainder reconciliation

The remaining-work view is reconciled through native cleanup commit `8f39f402`,
covering another 28 JIT files and 4690 removed lines of completed ABI, lowering,
LSRA, emitter and codegen support. Six restored lines preserve the complete
retained `LowerCFGCall` switch; that function matches the pinned oracle.
The residual tree is clean atop the pin and the oracle source remains unchanged.
Mixed-target/mode functions and unclassified backend areas remain; this is
source-work tracking, not a new execution or parity result.

## 2026-09-28: SysV call orchestration

SysV AMD64 call generation now places scalar and multireg field-list arguments,
preserves fast-tailcall argument and indirect-target roots through the epilog,
and transfers scalar/multireg results to allocated registers. Indirection-cell
selection, epilog-register checks, pending call labels and P/Invoke GC boundaries
follow the native ordering. Windows-only floating-point vararg duplication stays
Windows-only; unsupported SysV varargs reject before consuming arguments.

Full-analysis related SysV coverage passes 181 Debug/179 Release cases, including
null checks through RDI and unmanaged Vector3 return-copy/upper-clear ordering.
Windows call controls pass 118 Debug/106 Release. General generation, emission
and final metadata orchestration remain gated; this is not Linux execution or
parity evidence.

## 2026-09-28: ARM64 local allocation and resolution

ARM64 can now construct tracked-local intervals, allocate and resolve local
homes, write multireg call results, and resolve minimal temporary references.
Entry-frame kills include native poisoning and unknown-size-frame scratch
registers. Upper-vector saves use double-register temporaries and ARM64's
explicit spill/reload flags; integer edge cycles use scratch registers or
stack spills rather than xarch swaps. Edge move sets retain both register banks,
including predicate-register destinations.

Full-analysis managed target coverage passes 853 Debug/846 Release cases;
Windows LSRA controls pass 672 Debug/566 Release. Focused regressions cover
local spills, parameter homes, upper-vector transfers, edge cycles and predicate
moves. The public allocator remains gated pending whole-pipeline dependency
closure; these results do not establish ARM64 generated-code execution or parity.

## 2026-09-28: Unsigned checked multiplication

The importer now preserves `mul.ovf.un` signedness through its arithmetic
helpers. Previously, valid unsigned products could throw and overflowing
products could return wrapped results. Signed and unchecked multiplication
retain their existing flags and operand ordering.

Six real-importer cases distinguish both integer widths and all three opcodes;
the original code fails the two unsigned cases. Full-analysis arithmetic
coverage passes 286 Debug/286 Release. A matching-host probe passes all 27
signed/unsigned 32-bit, 64-bit and native-width checks, versus nine failures
before the fix. All six import spans, emitted instruction streams and code
sizes match native; no broader parity claim is implied.

## 2026-09-28: SysV return and method-exit generation

SysV AMD64 returns now support scalar, field-list, multireg, SIMD and stack-home
values, including Swift error/offset handling and async continuation transfers.
Return GC roots are restored before profiler callbacks; method exits record
mappings, check cookies and reserve epilogs. Register-copy/production and
indexed call-spill metadata dependencies are included.

Full-analysis return-related coverage passes 151 Debug/150 Release SysV cases;
the final return/exit fixture passes 26 Debug/25 Release, including all SIMD
source-register alias cases. Windows controls pass 96 Debug/90 Release.
Call orchestration and broader generation/metadata activation remain separate,
and these managed checks do not establish Linux execution or parity.

## 2026-09-28: ARM64 consecutive-register allocation

ARM64 LSRA now filters and ranks consecutive register sequences, assigns
wrapped vector-register groups and handles their copies/spills in full
allocation traversal. Stress recovery retains fixed-register exclusions;
upper-vector restores do not inherit xarch's stack-only suppression.
Native predecessor arithmetic and recorded wrap quirks remain unchanged.

Full-analysis target coverage passes 829 Debug/825 Release cases and Windows
LSRA/HWI controls pass 758 Debug/652 Release. Final focused coverage passes
51 Debug/46 Release, with discriminating prior-behavior failures for all three
review corrections. Local-interval construction, resolution and the minimal
consecutive path remain separate; the public allocation phase stays gated.
This is managed target coverage, not ARM64 execution.

## 2026-09-28: Windows shared-closure execution

After the shared CFG, LIR and EH work, the unfiltered Windows Core suite passes
12348 Debug/11111 Release cases with full analysis and no skipped tests.
A fresh NativeAOT JIT from committed source `8a92330`, excluding platform WIP,
also passes the established 29-configuration/143-body execution baseline on
the matching pinned host, with expected runtime outputs and no fallback.
This refreshes execution coverage, not whole-dump or instruction-stream parity.

## 2026-09-27: SysV root prolog materialization

SysV AMD64 now materializes root prologs and reserved prologs/epilogs, including
OSR frame reconstruction and Vector3 upper-lane clearing before parameter
homing. Unix varargs and NativeAOT CFI fail before instruction/unwind state
changes; no Windows shadow-space convention is introduced for SysV.

Full-analysis managed coverage passes 209 Debug/209 Release Linux-target cases
and 241 Debug/217 Release Windows controls. General machine-code generation,
emission orchestration and final metadata publication remain Windows-gated.
These results do not establish Linux generated-code execution or parity.

## 2026-09-27: Remaining EH region queries

The legacy `fgGetNestingLevel` query now preserves native handler counting and
the innermost finally-protected try boundary. `ehTrueEnclosingTryIndexIL`
compares import-time IL spans rather than normalized block identity, preserving
the distinction from the existing post-import helper. Neither adds callsites.

Thirteen cases cover nested handlers/finally regions, empty tables, mutually
protecting clauses and differing IL/block boundaries. Combined EH and loop
coverage passes 97 Debug/47 Release cases. EH verifier fixtures also clear
stale list endpoints when relinking blocks; production link assertions remain
unchanged.

## 2026-09-27: LIR operand ranges and dummy-use copies

`GetRangeOfOperandTrees` now implements the distinct native operand-only range
query, excluding the root and effects outside the operand span while retaining
closure reporting and clearing traversal marks. No new callsites were added.

Dummy `LIR.Use` copies now keep independent definitions, matching native copy
assignment. An explicit discriminator replaces the interior self-reference
that C# struct copies retained; ordinary uses still share the actual operand
edge. Three copy/spill regressions fail before the correction, with three
default/real-use controls. Full-analysis LIR, lowering and rationalization
coverage passes 1000 Debug/988 Release cases.

Native retirement is consolidated through `4ec12e43`, including completed
ARM64 reference construction, AMD64 OSR helpers, and further frontend, EH,
register/GC, lowering and emitter support. The broad remaining-code cleanup
is still in progress; retained target gaps are not Windows production gaps.

## 2026-09-27: Promoted-field frame diagnostics

Dependent promoted-field frame diagnostics now preserve native `%u` formatting
for parent and resulting offsets, including negative FP-relative offsets.
Stored offsets and layout are unchanged. Two negative-offset cases fail before
the correction while a positive-offset control passes; final frame coverage
passes 28 Debug/20 Release cases, including quiet-output checks. This closes
B396 as a scoped diagnostic correction, not a new execution/parity result.

## 2026-09-27: SysV final frame layout

Final frame layout and `genFinalizeFrame` now support SysV argument homes,
outgoing space, FP/SP offsets, alignment, OSR slot reuse and promoted fields.
Profiler hooks reserve R14/R15 before counting callee saves. AMD64 Tier0 OSR
unwind reconstruction, additional callee saves and GS-cookie initialization
also support SysV.

Full-analysis managed coverage passes 199 Debug/199 Release Linux-target cases
and 208 Debug/193 Release Windows controls. Root prolog materialization remains
gated while its remaining SysV dependencies are completed, including Vector3
upper-bit clearing and the native varargs feature boundary. NativeAOT CFI
remains explicitly unsupported. No Linux execution or parity claim is made.

## 2026-09-27: ARM64 register-reference construction

ARM64 LSRA now constructs node references for scalar, memory, atomic, call and
hardware-intrinsic operations, including consecutive vector-register chains,
partial-vector restores, predicate restrictions and embedded masked operations.
Call constraints preserve predicate kills and reserve the native GS-cookie
scratch registers for fast tailcalls. Reference ordering and contained block
address handling follow the pinned native builders.

The unfiltered Linux-ARM64 managed target suite passes 810 Debug/809 Release
cases; Windows LSRA/hardware-intrinsic controls pass 758 Debug/652 Release.
These are construction and cross-target policy checks, not ARM64 execution.
Consecutive-register selection/assignment, allocation and resolution remain
gated. Scalable vector constants and the undefined ARM64 async-continuation
return register remain explicit failures; cookie emission is not enabled.

## 2026-09-27: SysV incoming-parameter homing

Register and stack argument homing now handles SysV segments, including the
native upper-eightbyte SIMD shuffle and register-cycle scratch preservation.
Generic-context reporting and OSR Tier0 local loading are also enabled.
Root prolog materialization remains gated on final frame layout and
`genFinalizeFrame`; two cases confirm rejection before recording unwind data
or instructions. Coverage passes 184 Debug/184 Release Linux-target managed
cases and 221 Debug/206 Release Windows controls. No Linux execution or parity
claim is made.

Native retirement also removed the completed block-list verifier and supporting
checks, label/register-life helpers, funclet updates and AMD64 funclet/Windows
unwind workers. Mixed-target emitter storage and mixed-format CFI dispatch,
reservation and publication remain.

## 2026-09-27: SysV frame and unwind support

SysV AMD64 now supports frame allocation/probing, callee-save recording and
restoration, stack initialization, root/OSR epilogs and funclet frames. CoreCLR
unwind recording/publication includes Unix's large frame-pointer-offset opcode.
NativeAOT CFI mode explicitly rejects recording, reservation and publication
before changing unwind state; it cannot silently receive CoreCLR-format data.
The native descending callee-save order is unchanged.

Linux-target managed coverage passes 174 Debug/174 Release cases, including
11 CFI rejection cases; Windows controls pass 193 Debug/178 Release. Root
prolog materialization still awaits SysV incoming-parameter homing. These are
Windows-host managed results, not Linux execution or generated-code parity.

Native retirement also incorporated completed SSA construction/renaming,
IR diagnostics, SysV classification, optimizer policies, tree liveness,
floating WithElement and CFG query support. Broader backend reconciliation
and mixed-target/CFI paths remain; retirement is not additional parity evidence.

## 2026-09-27: Remaining shared CFG queries

The legacy predecessor verifier now checks block-list membership and the native
conditional/single-target branch kinds without adding callsites. Cold-section
and multiple-return queries, bounded statement counting and range-wide tree
complexity queries are also implemented. Counting preserves early cutoff,
inclusive block-range bounds and native accumulation across empty blocks.
Shared CFG/tree coverage passes 158 Debug/85 Release cases; the verifier remains
Debug-only. This closes unused/shared support, not a newly enabled phase.

## 2026-09-27: WithElement floating conversion semantics

Constant float lane replacement now preserves native float-to-double-to-float
conversion, including signaling-NaN quieting without changing sign or payload.
A non-inlined double-valued helper prevents the managed JIT from eliminating
the round trip. Double lanes remain bit-preserving, and untouched vector lanes
are unchanged. Intrinsic VN coverage passes 40 Debug/40 Release cases, including
four cases that fail with the original copy or optimized source casts.

The pinned Checked `valuenum.cpp.obj` retains `CVTSS2SD` and `CVTPD2PS`.
An MSVC `/O2 /fp:precise /arch:SSE4.2` probe using `_mm_cvtss_sd` followed by
`_mm_cvtpd_ps` confirms the expected raw bits for the inputs in
`ValueNumIntrinsicEvaluationTests.WithElementSinglePreservesNativeWideningAndNarrowing`.
This is instruction-level native evidence plus managed evaluator coverage, not
execution of the complete native VN helper or new generated-code parity.

## 2026-09-27: ARM64 optimizer target policies

Range analysis now recognizes ARM64 leading-zero/sign-count bounds. CSE uses
native target-specific register, frame-size and small-code costs, and induction
strength reduction respects address-mode scaling and post-use update placement.
Latest-statement selection preserves the native identity fast path and
two-cursor search. ARM64 managed coverage passes 777 Debug/776 Release cases,
with 16 final optimizer regressions and 168 Windows optimizer controls in
Debug. Allocator dependencies and target execution remain separate.

## 2026-09-27: Shared frame support corrections

Frame-location dumps now append padding after the location, matching native
column layout. Boxed `ValueSize` equality recognizes `ValueSize` rather than
recursively dispatching through `System.ValueType`; exact and symbolic sizes
retain their typed equality and hash behavior. Frame and value-size coverage
passes 44 Debug/39 Release cases.

## 2026-09-27: CFG debug invariants

Flow-edge visitation now checks state transitions, and block-list iterators
verify that the current block remains doubly linked while allowing edits
elsewhere. Basic-block membership always searches in Debug; scratch-range
membership retains the optional expensive-check gate. Ten regression cases
fail with the original behavior, with five controls passing. CFG and layout
coverage passes 156 Debug/141 Release cases without changing Release behavior.

## 2026-09-27: SysV helper-driven code generation

SysV x64 now generates return traps, checked/unchecked write barriers and
profiler enter/leave/tailcall callbacks, including register consumption,
reload and caller-SP offset dependencies. Profiler entry uses R14/R15 without
Windows argument homing; return GC registers remain live across callbacks.
Managed SysV coverage passes 148 cases in each configuration, with 292
Debug/274 Release Windows controls. Prolog/epilog frame setup and callee-save
handling are next; Linux generated-code execution remains unverified.

## 2026-09-27: Custom-awaiter importer optimization

Optimized `AwaitAwaiter` and `UnsafeAwaitAwaiter` imports now request the EE
helper that reads a struct awaiter from its continuation. Replacement preserves
the exact generic context and ReadyToRun entrypoint, marks the hidden awaiter
argument and appends its symbolic member offset before adding other hidden
arguments. Reference awaiters, intrinsic `YieldAwaiter`, declined replacements
and unsupported generic lookups retain their original call representation.
Async importer and continuation coverage passes 99 Debug/95 Release cases;
this is not new native execution or generated-code parity evidence.

## 2026-09-27: ARM64 rationalization

ARM64 rationalization now dispatches comparison-mask reductions, scalar
popcount/zero-count consumers, general most-significant-bit extraction and
signature-dependent immediate handling. The signed division/shift subtraction
rewrite produces remainder nodes with cleared value numbers and unchecked
machine-width shift conversion. Managed target coverage passes 764 Debug/763
Release cases, with 31 focused Debug cases after the final shift-boundary
regressions. Allocation, emission and target execution remain separate.

## 2026-09-27: ARM64 block and switch lowering

Block lowering resets indirection-pairing candidates and FFR state at each
block boundary. Switches preserve index evaluation and select native ARM64
shift/AND bit tests or jump tables, including 64-case tables. Managed ARM64
coverage passes 737 Debug/736 Release cases. Full lowering-phase activation,
allocation, emission and target execution remain separate.

## 2026-09-27: SysV call recording and emission

SysV x64 calls preserve both return registers' GC types through instruction
recording and output. Large descriptors represent the second GC return, while
ordinary scalar returns retain the small form. Profiler helpers preserve the
native SysV argument/return masks. Call and helper generation, GC handoff and
final emission now use the implemented AMD64 paths. Managed SysV checks pass
75 cases in each configuration, with 122 Debug/113 Release Windows controls;
Linux generated-code execution remains unverified.

## 2026-09-27: VN diagnostics and component checks

Map selection now emits the native precise-store, physical-store and bitcast
traces at their evaluation points. Startup reconstructs VN attributes
independently from operator properties and generated function metadata.
The scalar-one invariant and optional once-per-process VN/bitset component
tests are implemented. The pinned component test's obsolete constant-first
assertion is preserved and tracked as B394 rather than silently corrected.
Windows-x64 VN, bitset and lowering controls pass 636 Debug/582 Release cases;
this does not establish whole-pipeline or other-target execution parity.

## 2026-09-27: Pre-morph tree stress

The debug local-field and 64-bit-result multiplication stress hooks now perform
their native transformations instead of returning without work. Local-field
stress first excludes unsuitable locals across every use, then builds padded
custom layouts with aligned GC slots and rewrites loads/stores through the
existing typed-node replacement model. Multiplication stress widens eligible
unchecked integer products and prevents narrowing from undoing the stress.
Both retain native selection and traversal policies.

## 2026-09-27: Unused-tree side-effect extraction

Unused `GetType` expressions now reduce to a null check when needed and otherwise
retain only receiver side effects. Extracted comma trees preserve execution order
and compose liberal/conservative exception value numbers. Unused block loads use
the existing replacement helper to retain native tree IDs and common metadata,
while clearing operator-specific flags and value numbers under native bashing
policy. This closes the remaining extraction implementation against the pinned
native function; it does not establish whole-pipeline parity.

## 2026-09-27: SysV x64 local stores and fast tailcalls

SysV x64 now lowers local variable and field stores through the shared native
algorithm, including scalar retyping, promoted single fields, block
initialization and copies, and irregular struct-call result spills. Fast
tailcalls can rehome overlapping stack parameters through lowered defensive
copies. The Linux-x64 target fixture includes these stores and all fast-tailcall
cases without enabling unrelated ARM64 backend fixtures. On a Windows host,
full-analysis target tests pass 119 Debug/119 Release, and selected Windows-x64
controls pass 31 in both configurations. No Linux execution parity is claimed.

## 2026-09-27: ARM64 hardware containment

ARM64 containment now handles hardware immediate families, paired operands,
zero comparisons and MOVI/FMOV constants. SVE conditional selects preserve
embedded-operation ownership, mask/auxiliary element widths and zero-merge
exceptions for unpredicated pairwise instructions. Bounds checks use native
index-first immediate selection without xarch memory containment.

The shared containment walker reaches these hardware actions. General hardware
rewriting and node/block/phase activation remain separate; managed target
coverage is not ARM64 generated-code execution evidence.

## 2026-09-27: ARM64 call transitions

Private ARM64 PInvoke lowering now preserves frame initialization and links,
inline/helper GC transitions and return-trap containment. Swift error-register
consumers move immediately after their calls, ahead of inserted epilogs.
Fast tailcalls preserve defensive argument copies, non-GC boundaries and
profiler-hook placement, including copied local diagnostic metadata.

CFG follows native ARM64 dispatcher policy and avoids duplicating constant
stub-cell addresses. Its validate-and-call transformation still requires the
inactive general dispatcher. These target-policy results are not ARM64
generated-code execution evidence.

## 2026-09-27: ARM64 calls and split arguments

Private ARM64 call lowering now preserves the VM-backed direct-call range
policy, uncontained indirect targets, register/stack argument placement and
single-register HFA/struct result normalization. Windows ARM64 split arguments
retain early/late ordering, ABI segment ownership and local-field offsets.
Field lists split at clean boundaries or spill once when a field overlaps.

Block-indirection splits and field-list register repacking still need the
inactive general dispatcher. PInvoke, CFG and fast-tailcall dependencies remain
separate; managed target coverage does not establish ARM64 execution parity.

## 2026-09-27: ARM64 returns and stack arguments

Private ARM64 return lowering now normalizes SIMD-backed HFAs to their
multi-register ABI and preserves primitive bitcasts and struct-local field
types. Incompatible return field lists spill through the private local actions,
while stack-passed structs retain their native contained representation.
Return containment correctly distinguishes Swift's value and error operands.

Register repacking, PInvoke epilogs and general call/phase integration remain
ahead of full ARM64 lowering; these entrypoints do not establish execution
parity.

## 2026-09-27: ARM64 block-memory lowering

Private ARM64 block initialization and copying now preserve native unroll
thresholds, fill patterns and zero-register use. GC-pointer initialization
uses atomic stores or loops; small stack copies remain non-interruptible.
Unrolled address formation checks both narrowing and end-offset overflow,
with managed replacement preserving the address's owning edge.

Local block stores, small scalar copies and single-register call-result stores
use the private block-store action. Large helper calls and GC decomposition
still require the unfinished general dispatcher, and ARM64 execution remains
unverified.

## 2026-09-27: ARM64 casts, division and local temporaries

Private ARM64 lowering now handles cast/load extension, constant signed and
unsigned division, and power-of-two remainder with native multiply selection
and conditional-negate semantics. Repeated dividend uses preserve single
evaluation, including volatile loads. Local-store support includes odd-size
return spilling and pointer-update scheduling for post-indexed addressing.

Shared containment traversal supports the scalar dependency layer while
explicitly rejecting unported hardware-intrinsic containment. General
node/block/phase dispatch and ARM64 execution remain unsupported.

## 2026-09-27: ARM64 conditional lowering

Private comparison, conditional-branch and select lowering now supports ARM64's
full-width byte-mask tests, direct zero/sign-bit branches and conditional
negate/invert/increment instructions. Managed node replacements preserve their
owning edges, while condition reversal, unsigned-zero flags and increment
wrapping retain native semantics.

The incomplete ARM64 phase dispatcher and backend remain inactive. A source
inconsistency in native non-flags conditional increment handling is recorded as
B381 rather than silently changed in the port.

## 2026-09-27: ARM64 scalar arithmetic lowering

Private ARM64 addition, multiplication, negation, shifts and rotates now preserve
the native widening-multiply overflow proof, multiply-add/subtract/negate
transformations and extended bitfield shifts. Cast removal retains extension
signedness and clears containment, while count-mask stripping and rotate
conversion preserve the native width and wrapping rules.

This completes the arithmetic dependency layer without enabling the unfinished
node/block/phase dispatcher or general hardware-intrinsic lowering. ARM64
generated-code execution remains unverified.

## 2026-09-27: ARM64 private binary arithmetic

Private ARM64 binary arithmetic now includes NOT combinations, conditional-compare
chaining, bitfield extraction and widening-multiply subtraction. Supporting
condition descriptors, truthifying flags and chain movement preserve native
signed/unsigned and ordered/unordered behavior. Widening-multiply addition is
available as a dependency for the separate ADD entrypoint.

Full-analysis target suites pass 433 Debug/433 Release cases, including 58 new
arithmetic cases. Windows arithmetic, condition and select controls pass 184 in
each configuration. Coverage includes CCMP operand preference, nested chains,
bitfield boundaries, overflow/interference exclusions, node ownership and distinct
successor contracts. ARM64 node/block/phase and hardware-intrinsic dispatch remain
inactive; these are managed helper results, not generated-code execution evidence.

## 2026-09-27: ARM64 scalar operand containment

ARM64 immediate and compound-operand containment now preserves instruction
encoding limits, LSE atomic gating, Windows NativeAOT section relocations,
operand-width shift bounds, rotate normalization, cast/load-extension precedence
and overflow/flag/interference exclusions. Binary, shift, negate and not
containment are implemented without activating incomplete arithmetic dispatch.

Full-analysis Linux-ARM64 suites pass 375 Debug/375 Release, including 74 new
cases. Windows-ARM64 operand cases pass 62 in each configuration, and Windows-x64
comparison/arithmetic/bounds/cast controls pass 168 in each. CCMP, bitfield and
multiply-long optimization dependencies remain ahead of ARM64 arithmetic
lowering. These managed results are not generated-code or runtime parity.

## 2026-09-27: ARM64 private memory lowering

Private ARM64 load/store lowering now integrates address formation, containment
and pair scheduling. It preserves unused-probe width ordering, volatile
floating-point bitcasts, positive-zero store retyping, mutable-object release
stores and write-barrier early returns. Store coalescing respects ARM64's
64-bit-aligned 128-bit write guarantees and reuses SIMD16 constants without
widening beyond SIMD16.

Full-analysis target suites pass 301 Debug/301 Release cases; Windows memory,
bitcast and coalescing controls pass 121 in each configuration. B380 records
the pinned optimized volatile-load bitcast round trip, which remains unchanged.
Full ARM64 node/block/phase dispatch is still unsupported, so this is not
generated-code execution or runtime parity.

## 2026-09-27: ARM64 indirection pairing

The ARM64 memory-lowering dependency layer now includes complete load/store
pair scheduling, alias-aware adjacency reordering and loop store-to-load-
forwarding safeguards. It preserves consumed-candidate handling, store-data
motion, recursive mark cleanup, the 16-node reordering limit and the 100-node
backward scan budget.

Full-analysis Linux-ARM64 target suites pass 276 cases in each configuration,
including 24 new pairing/forwarding cases and boundary checks. B379 records a
potential native empty-predecessor scan-state issue; pinned behavior is retained.
Public ARM64 block/load/store dispatch and candidate reset remain to be integrated.
These managed helper results are not ARM64 execution or generated-code parity.

## 2026-09-27: ARM64 address formation and containment

ARM64 address lowering now preserves access-width scaling, RCPC2 signed-nine-bit
volatile offsets, CAST/BFIZ extended indices, interference checks and LIR owner
replacement. Load/store containment handles stack addresses, TLS handles,
SIMD12 restrictions, null-check probes and integer-zero stores.

The Linux-ARM64 target suite passes 252 Debug/252 Release with full analysis;
Windows address and indirect-store controls pass 61 Debug/61 Release. These are
managed helper results, not ARM64 execution evidence. Load/store entrypoints
remain unsupported while their pair-reordering and loop-forwarding dependency
closure is ported next. The Windows execution baseline is unchanged.

## 2026-09-27: ARM64 frame layout and reservations

ARM64 frame selection now performs conservative layout before reserving IP1,
and reserves x19 when scalable-vector storage is present. Virtual and final
layout preserve varargs homes, FP/LR relocation, Apple NativeAOT placement,
OSR frame boundaries, local/temp alignment and scalable-vector offsets.

Integration corrected B377: the pinned native compiler still treats masks as
exact eight-byte values. Scalable-mask block bookkeeping remains available but
is not activated for mask locals or temps. Replaying the old value-size mapping
produces three regression failures with one passing control.

Full-analysis target suites pass 211 Linux-ARM64, 214 Windows-ARM64 and 212
Apple-ARM64 cases in each configuration. Windows-x64 frame/allocator controls
pass 47 Debug/46 Release, and Linux-x64 controls pass 102 in each. These are
managed unit results; ARM64 lowering, allocation and stack-space emission remain
unported. The Windows execution baseline is unchanged and was not recaptured.

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

## 2026-10-03: Wasm interval-aware jump elision

Basic-block jump elision now preserves Wasm try and exception-reference wrapper
boundaries while retaining the existing shared fall-through decisions. The
Windows-x64 branch-optimization fixture passes 22/22, and the focused Wasm
target tests pass 81/81 with a clean full-analysis Wasm Core build. These tests
run Wasm-targeted managed logic on the Windows host; they do not establish
browser-Wasm execution or generated-code parity.

## 2026-10-04: Optional call-argument statistics

Completed the `CALL_ARG_STATS` collection/reporting closure. The compilation
finish caller now collects call kinds from the post-rationalization LIR chain;
the reporter preserves the pinned labels, percentage arithmetic, signed
`%d` counter output, histogram tables, and shutdown early-return behavior.
Focused Windows-x64 full-analysis tests pass 6/6 Debug and 6/6 Release with
zero skips using a validation-only feature import; production defaults remain
unchanged.

Retired the complete residual globals, collector, reporter and both
declarations: 206 lines from `compiler.cpp` and 4 from `compiler.h`; also
removed the obsolete `CALL_ARG_STATS` term from the retained `genMethodCnt`
guard. The consolidated native commit is `ef01ee6fe767168c0ffed810d1d08495f4235922`,
sole-parented to pinned oracle `33baf8ee337b20dd0f184b69a6f09be92850bf9e`;
recovery refs `refs/copilot-recovery/call-arg-stats-before-a0c67aff` and
`refs/copilot-recovery/call-arg-stats-guard-before-amend` preserve the prior
heads. `compJitStats` was already absent and was not re-retired. No
generated-code, phase-dump or runtime parity is claimed.

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
