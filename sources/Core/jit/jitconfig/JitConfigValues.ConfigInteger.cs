// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Frozen;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static RyuJitSharp.JitConfigValues.ConfigInteger;

namespace RyuJitSharp;

public partial struct JitConfigValues
{
    public enum ConfigInteger
    {
#if DEBUG
        /// <summary>Max number of functions to use altjit for (decimal)</summary>
        AltJitLimit,

        /// <summary>
        ///     <para>If AltJit hits an assert, fall back to the fallback JIT.</para>
        ///     <para>Useful in conjunction with DOTNET_ContinueOnAssert=1</para>
        /// </summary>
        AltJitSkipOnAssert,

        /// <summary>Breaks when using internal logging on a particular token value.</summary>
        BreakOnDumpToken,

        /// <summary>Halts the jit on verification failure</summary>
        DebugBreakOnVerificationFailure,

        /// <summary>Display JIT loop hoisting statistics</summary>
        DisplayLoopHoistStats,

        /// <summary>
        ///     <para>Display JIT Linear Scan Register Allocator statistics</para>
        ///     <list type="bullet">
        ///         <item>If set to "1", display the stats in textual format.</item>
        ///         <item>If set to "2", display the stats in csv format.</item>
        ///         <item>If set to "3", display the stats in summarize format.</item>
        ///     </list>
        ///     <para>Recommended to use with JitStdOutFile flag.</para>
        /// </summary>
        DisplayLsraStats,

        /// <summary>Whether absolute addr be encoded as PC-rel offset by RyuJIT where possible</summary>
        EnablePCRelAddr,

        /// <summary></summary>
        JitAssertOnMaxRAPasses,

        /// <summary></summary>
        JitBreakEmitOutputInstr,

        /// <summary></summary>
        JitBreakMorphTree,

        /// <summary></summary>
        JitBreakOnBadCode,

        /// <summary>Halt if jit switches to MinOpts</summary>
        JitBreakOnMinOpts,

        /// <summary>If 0, don't clone. Otherwise clone loops for optimizations.</summary>
        JitCloneLoops,

        /// <summary>If 0, don't clone loops containing EH regions</summary>
        JitCloneLoopsWithEH,

        /// <summary>If 0, don't clone loops based on invariant type/method address tests</summary>
        JitCloneLoopsWithGdvTests,
#endif

        /// <summary>limit cloning to loops with no more than this many tree nodes</summary>
        JitCloneLoopsSizeLimit,

#if DEBUG
        /// <summary>In debug builds log places where loop cloning optimizations are performed on the fast path.</summary>
        JitDebugLogLoopCloning,

        /// <summary>In debug builds, initialize the memory allocated by the nra with this byte.</summary>
        JitDefaultFill,

        /// <summary>Minimum weight needed for the first block of a loop to make it a candidate for alignment.</summary>
        JitAlignLoopMinBlockWeight,

        /// <summary>
        ///     <para>For non-adaptive alignment, minimum loop size (in bytes) for which alignment will be done.</para>
        ///     <para>Defaults to 3 blocks of 32 bytes chunks = 96 bytes.</para>
        /// </summary>
        JitAlignLoopMaxCodeSize,

        /// <summary>
        ///     <para>For non-adaptive alignment, address boundary (power of 2) at which loop alignment should be done.</para>
        ///     <para>By default, 32B.</para>
        /// </summary>
        JitAlignLoopBoundary,

        /// <summary>If set, for non-adaptive alignment, ensure loop jmps are not on or cross alignment boundary.</summary>
        JitAlignLoopForJcc,

        /// <summary>If set, perform adaptive loop alignment that limits number of padding based on loop size.</summary>
        JitAlignLoopAdaptive,

        /// <summary>If set, try to hide align instruction (if any) behind an unconditional jump instruction (if any) that is present before the loop start.</summary>
        JitHideAlignBehindJmp,

        /// <summary>Track stores to locals done through return buffers.</summary>
        JitOptimizeStructHiddenBuffer,

        /// <summary></summary>
        JitUnrollLoopMaxIterationCount,

        /// <summary>If 0, don't unroll loops containing EH regions</summary>
        JitUnrollLoopsWithEH,

        /// <summary></summary>
        JitDirectAlloc,

        /// <summary></summary>
        JitDoubleAlign,

        /// <summary></summary>
        JitEmitPrintRefRegs,

        /// <summary>Enable devirtualization in importer</summary>
        JitEnableDevirtualization,

        /// <summary>Enable devirtualization after inlining</summary>
        JitEnableLateDevirtualization,

        /// <summary>Level indicates how much checking beyond the default to do in debug builds (currently 1-2)</summary>
        JitExpensiveDebugCheckLevel,

        /// <summary>Set to non-zero to test NOWAY assert by forcing a retry</summary>
        JitForceFallback,

        /// <summary>Forces Fully interruptible code</summary>
        JitFullyInt,

        /// <summary>If non-zero, print JIT start/end logging</summary>
        JitFunctionTrace,

        /// <summary></summary>
        JitGCChecks,

        /// <summary>If true, prints GCInfo-related output to standard output.</summary>
        JitGCInfoLogging,

        /// <summary>Same as JitBreak, but for a method hash</summary>
        JitHashBreak,

        /// <summary>Same as JitHalt, but for a method hash</summary>
        JitHashHalt,

        /// <summary></summary>
        JitInlineAdditionalMultiplier,

        /// <summary></summary>
        JitInlinePrintStats,

        /// <summary></summary>
        JitInlineSize,

        /// <summary></summary>
        JitInlineDepth,
#endif

        /// <summary></summary>
        JitInlineBudget,

#if DEBUG
        /// <summary></summary>
        JitForceInlineDepth,
#endif

        /// <summary></summary>
        JitInlineMethodsWithEH,

#if DEBUG
        /// <summary>Force using the large pseudo instruction form for long address</summary>
        JitLongAddress,

        /// <summary></summary>
        JitMaxUncheckedOffset,
#endif

        /// <summary>Enable devirtualization for generic virtual methods</summary>
        JitEnableGenericVirtualDevirtualization,

#if DEBUG
        /// <summary>Forces MinOpts</summary>
        JitMinOpts,

        /// <summary></summary>
        JitMinOptsBbCount,

        /// <summary></summary>
        JitMinOptsCodeSize,

        /// <summary></summary>
        JitMinOptsInstrCount,

        /// <summary></summary>
        JitMinOptsLvNumCount,

        /// <summary></summary>
        JitMinOptsLvRefCount,

        /// <summary></summary>
        JitNoCSE,

        /// <summary></summary>
        JitNoCSE2,

        /// <summary>
        ///     <para>Set to non-zero to prevent NOWAY assert testing.</para>
        ///     <para>Overrides DOTNET_JitForceFallback and JIT stress flags.</para>
        /// </summary>
        JitNoForceFallback,

        /// <summary>Disables forward sub</summary>
        JitNoForwardSub,

        /// <summary></summary>
        JitNoHoist,

        /// <summary>If 1, don't generate memory barriers</summary>
        JitNoMemoryBarriers,

        /// <summary>Disables struct promotion 1 - for all, 2 - for params.</summary>
        JitNoStructPromotion,

        /// <summary></summary>
        JitNoUnroll,

        /// <summary></summary>
        JitOrder,

        /// <summary></summary>
        JitQueryCurrentStaticFieldClass,

        /// <summary></summary>
        JitReportFastTailCallDecisions,

        /// <summary></summary>
        JitPInvokeCheckEnabled,

        /// <summary></summary>
        JitPInvokeEnabled,

        /// <summary>Specifies the maximum number of hoist candidates to hoist</summary>
        JitHoistLimit,

        /// <summary>
        ///     <para>Controls verbosity for JitPrintInlinedMethods.</para>
        ///     <para>Ignored for JitDump where it's always set.</para>
        /// </summary>
        JitPrintInlinedMethodsVerbose,

        /// <summary>
        ///     <para>-1: just do internal checks (CHECK_HASLIKELIHOOD | CHECK_LIKELIHOODSUM | RAISE_ASSERT)</para>
        ///     <para>Else bitflag:</para>
        ///     <list type="bullet">
        ///         <item> - 0x1: check edges have likelihoods</item>
        ///         <item> - 0x2: check edge likelihoods sum to 1.0</item>
        ///         <item> - 0x4: fully check likelihoods</item>
        ///         <item> - 0x8: assert on check failure</item>
        ///         <item> - 0x10: check block profile weights</item>
        ///     </list>
        /// </summary>
        JitProfileChecks,

        /// <summary></summary>
        JitRequired,

        /// <summary></summary>
        JitStackAllocToLocalSize,

        /// <summary></summary>
        JitSkipArrayBoundCheck,

        /// <summary>Turn on slow debug checks</summary>
        JitSlowDebugChecksEnabled,

        /// <summary>On ARM, use this as the maximum function/funclet size for creating function fragments (and creating multiple RUNTIME_FUNCTION entries)</summary>
        JitSplitFunctionSize,

        /// <summary>Perturb order of processing of blocks in SSA; 0 = no stress; 1 = use method hash; * = supplied value as random hash</summary>
        JitSsaStress,

        /// <summary></summary>
        JitStackChecks,

        /// <summary>Internal Jit stress mode: 0 = no stress, 2 = all stress, other = vary stress based on a hash of the method and this value.</summary>
        JitStress,

        /// <summary>Internal Jit stress mode</summary>
        JitStressBBProf,

        /// <summary>Always split after the first basic block.</summary>
        JitStressProcedureSplitting,

        /// <summary></summary>
        JitStressRegs,

        /// <summary>If non-negative value N, only stress split the first N trees.</summary>
        JitStressSplitTreeLimit,

        /// <summary>If non-zero, assert if # of VNF_MapSelect applications considered reaches this.</summary>
        JitVNMapSelLimit,

        /// <summary>
        ///     <para>If non-zero, and the compilation succeeds for an AltJit, then use the code.</para>
        ///     <para>If zero, then we always throw away the generated code and fall back to the default compiler.</para>
        /// </summary>
        RunAltJitCode,

        /// <summary>Run JIT component unit tests</summary>
        RunComponentUnitTests,

        /// <summary></summary>
        ShouldInjectFault,

        /// <summary></summary>
        TailcallStress,

        /// <summary>Same as JitDump, but for a method hash</summary>
        JitHashDump,

        /// <summary>Dump tier0 jit compilations</summary>
        JitDumpTier0,

        /// <summary>Dump OSR jit compilations</summary>
        JitDumpOSR,

        /// <summary>Dump only OSR jit compilations with this offset</summary>
        JitDumpAtOSROffset,

        /// <summary>Dump inline compiler phases</summary>
        JitDumpInlinePhases,

        /// <summary>Uses only ASCII characters in tree dumps</summary>
        JitDumpASCII,

        /// <summary>Produce terse dump output for LSRA</summary>
        JitDumpTerseLsra,

        /// <summary>Output JitDump output to the debugger</summary>
        JitDumpToDebugger,

        /// <summary>Produce especially verbose dump output for SSA</summary>
        JitDumpVerboseSsa,

        /// <summary>Enable more verbose tree dumps</summary>
        JitDumpVerboseTrees,

        /// <summary>Print tree IDs in dumps</summary>
        JitDumpTreeIDs,

        /// <summary>If 1, display each tree before/after morphing blocks, display "*" instead of block number for lexical "next" blocks, to reduce clutter.</summary>
        JitDumpBeforeAfterMorph,

        /// <summary></summary>
        JitDumpTerseNextBlock,

        /// <summary>Do code splitting independent of VM.</summary>
        JitFakeProcedureSplitting,

        /// <summary>Dumps Xml/Dot Flowgraph for specified method</summary>
        JitDumpFgHash,

        /// <summary>Dumps Xml/Dot Flowgraph for tier-0 compilations of specified methods</summary>
        JitDumpFgTier0,

        /// <summary>0 == dump XML format; non-zero == dump DOT format</summary>
        JitDumpFgDot,

        /// <summary>0 == no EH regions; non-zero == include EH regions</summary>
        JitDumpFgEH,

        /// <summary>0 == no loop regions; non-zero == include loop regions</summary>
        JitDumpFgLoops,

        /// <summary>0 == don't constrain to mostly linear layout; non-zero == force mostly lexical block linear layout</summary>
        JitDumpFgConstrained,

        /// <summary>0 == display block with bbNum; 1 == display with both bbNum and bbID</summary>
        JitDumpFgBlockID,

        /// <summary>0 == don't display block flags; 1 == display flags</summary>
        JitDumpFgBlockFlags,

        /// <summary>0 == don't display loop flags; 1 == display flags</summary>
        JitDumpFgLoopFlags,

        /// <summary>0 == bbNext order;  1 == bbNum order; 2 == bbID order</summary>
        JitDumpFgBlockOrder,

        /// <summary>non-zero: show memory phis + SSA/VNs</summary>
        JitDumpFgMemorySsa,

        /// <summary>Internal Jit stress: if nonzero, only enable stress modes listed in JitStressModeNames.</summary>
        JitStressModeNamesOnly,
#endif

        /// <summary>Display BEGIN METHOD/END METHOD anchors for disasm testing</summary>
        JitDisasmTesting,

        /// <summary>Make the disassembly diff-able</summary>
        JitDisasmDiffable,

        /// <summary>Prints all jitted methods to the console</summary>
        JitDisasmSummary,

        /// <summary>Hides disassembly for unoptimized codegen</summary>
        JitDisasmOnlyOptimized,

        /// <summary>Print the alignment boundaries.</summary>
        JitDisasmWithAlignmentBoundaries,

        /// <summary>Print the instruction code bytes</summary>
        JitDisasmWithCodeBytes,

#if DEBUG
        /// <summary>Dump interleaved GC Info for any method disassembled.</summary>
        JitDisasmWithGC,

        /// <summary>Dump interleaved debug info for any method disassembled.</summary>
        JitDisasmWithDebugInfo,

        /// <summary>Display native code when any register spilling occurs</summary>
        JitDisasmSpilled,

        /// <summary>Print the process address next to each instruction of the disassembly</summary>
        JitDasmWithAddress,
#endif

        /// <summary>If 1, keep rich debug info and report it back to the EE</summary>
        RichDebugInfo,

        /// <summary>If set, align inner loops</summary>
        JitAlignLoops,

        /// <summary>
        ///     <para>Controls the AltJit behavior of NYI stuff</para>
        ///     <para>AltJitAssertOnNYI should be 0 on targets where JIT is under development or bring up stage, so as to facilitate fallback to main JIT on hitting a NYI.</para>
        /// </summary>
        AltJitAssertOnNYI,

        /// <summary>Enable the register allocator to support EH-write thru: partial enregistration of vars exposed on EH boundaries</summary>
        EnableEHWriteThru,

        /// <summary>Enable the enregistration of locals that are defined or used in a multireg context.</summary>
        EnableMultiRegLocals,

        /// <summary>Disables inlining of all methods</summary>
        JitNoInline,

#if DEBUG
        /// <summary>Enable rex2 encoding for compatible instructions.</summary>
        JitStressRex2Encoding,

        /// <summary>Enable promoted EVEX encoding for compatible instructions.</summary>
        JitStressPromotedEvexEncoding,
#endif

#if DEBUG && TARGET_XARCH
        /// <summary>Enable EVEX encoding for SIMD instructions when AVX-512VL is available.</summary>
        JitStressEvexEncoding,
#endif

        /// <summary>Allows Base+ hardware intrinsics to be disabled</summary>
        EnableHWIntrinsic,

#if TARGET_XARCH
        /// <summary>Allows AVX and dependent hardware intrinsics to be disabled</summary>
        EnableAVX,

        /// <summary>Allows AVX2, BMI1, BMI2, F16C, FMA, LZCNT, MOVBE and dependent hardware intrinsics to be disabled</summary>
        EnableAVX2,

        /// <summary>Allows AVX512 F+BW+CD+DQ+VL and depdendent hardware intrinsics to be disabled</summary>
        EnableAVX512,

        /// <summary>Allows AVX10v2 and depdendent hardware intrinsics to be disabled</summary>
        EnableAVX512BMM,

        /// <summary>Allows AVX512 IFMA+VBMI and depdendent hardware intrinsics to be disabled</summary>
        EnableAVX512v2,

        /// <summary>Allows AVX512 BITALG+VBMI2+VNNI+VPOPCNTDQ and depdendent hardware intrinsics to be disabled</summary>
        EnableAVX512v3,

        /// <summary>Allows AVX10v1 and depdendent hardware intrinsics to be disabled</summary>
        EnableAVX10v1,

        /// <summary>Allows AVX10v2 and depdendent hardware intrinsics to be disabled</summary>
        EnableAVX10v2,

        /// <summary>Allows APX and dependent features to be disabled</summary>
        EnableAPX,

        /// <summary>Allows AES, PCLMULQDQ, and dependent hardware intrinsics to be disabled</summary>
        EnableAES,

        /// <summary>Allows AVX512VP2INTERSECT and dependent hardware intrinsics to be disabled</summary>
        EnableAVX512VP2INTERSECT,

        /// <summary>Allows AVXIFMA and dependent hardware intrinsics to be disabled</summary>
        EnableAVXIFMA,

        /// <summary>Allows AVXVNNI and dependent hardware intrinsics to be disabled</summary>
        EnableAVXVNNI,

        /// <summary>Allows VEX AVXVNNIINT+ hardware intrinsics to be disabled</summary>
        EnableAVXVNNIINT,

        /// <summary>Allows GFNI and dependent hardware intrinsics to be disabled</summary>
        EnableGFNI,

        /// <summary>Allows SHA and dependent hardware intrinsics to be disabled</summary>
        EnableSHA,

        /// <summary>Allows VAES, VPCLMULQDQ, and dependent hardware intrinsics to be disabled</summary>
        EnableVAES,

        /// <summary>Allows WAITPKG and dependent hardware intrinsics to be disabled</summary>
        EnableWAITPKG,

        /// <summary>Allows X86Serialize and dependent hardware intrinsics to be disabled</summary>
        EnableX86Serialize,
#elif TARGET_ARM64
        /// <summary>Allows Arm64 Aes+ hardware intrinsics to be disabled</summary>
        EnableArm64Aes,

        /// <summary>Allows Arm64 Atomics+ hardware intrinsics to be disabled</summary>
        EnableArm64Atomics,

        /// <summary>Allows Arm64 Crc32+ hardware intrinsics to be disabled</summary>
        EnableArm64Crc32,

        /// <summary>Allows Arm64 Dczva+ hardware intrinsics to be disabled</summary>
        EnableArm64Dczva,

        /// <summary>Allows Arm64 Dp+ hardware intrinsics to be disabled</summary>
        EnableArm64Dp,

        /// <summary>Allows Arm64 Rdm+ hardware intrinsics to be disabled</summary>
        EnableArm64Rdm,

        /// <summary>Allows Arm64 Sha1+ hardware intrinsics to be disabled</summary>
        EnableArm64Sha1,

        /// <summary>Allows Arm64 Sha256+ hardware intrinsics to be disabled</summary>
        EnableArm64Sha256,

        /// <summary>Allows Arm64 Sve+ hardware intrinsics to be disabled</summary>
        EnableArm64Sve,

        /// <summary>Allows Arm64 Sve2+ hardware intrinsics to be disabled</summary>
        EnableArm64Sve2,

        /// <summary>Allows Arm64 Sha3+ hardware intrinsics to be disabled</summary>
        EnableArm64Sha3,

        /// <summary>Allows Arm64 Sm4+ hardware intrinsics to be disabled</summary>
        EnableArm64Sm4,

        /// <summary>Allows Arm64 SveAes+ hardware intrinsics to be disabled</summary>
        EnableArm64SveAes,

        /// <summary>Allows Arm64 SveSha3+ hardware intrinsics to be disabled</summary>
        EnableArm64SveSha3,

        /// <summary>Allows Arm64 SveSm4+ hardware intrinsics to be disabled</summary>
        EnableArm64SveSm4,
#elif TARGET_RISCV64
        /// <summary>Allows RiscV64 Zba hardware intrinsics to be disabled</summary>
        EnableRiscV64Zba,

        /// <summary>Allows RiscV64 Zbb hardware intrinsics to be disabled</summary>
        EnableRiscV64Zbb,

        /// <summary>Allows RiscV64 Zbs hardware intrinsics to be disabled</summary>
        EnableRiscV64Zbs,
#endif

        /// <summary>Allows embedded broadcasts to be disabled</summary>
        EnableEmbeddedBroadcast,

        /// <summary>Allows embedded masking to be disabled</summary>
        EnableEmbeddedMasking,

        /// <summary>Allows APX NDD feature to be disabled</summary>
        EnableApxNDD,

        /// <summary>Allows APX conditional compare chaining</summary>
        EnableApxConditionalChaining,

        /// <summary>Allows APX PPX feature to be disabled</summary>
        EnableApxPPX,

        /// <summary>Allows APX ZU feature to be disabled</summary>
        EnableApxZU,

#if FEATURE_SIMD
        /// <summary>
        ///     <para>Default 0, ValueNumbering of SIMD nodes and HW Intrinsic nodes enabled</para>
        ///     <para>If 1, then disable ValueNumbering of SIMD nodes</para>
        ///     <para>If 2, then disable ValueNumbering of HW Intrinsic nodes</para>
        ///     <para>If 3, disable both SIMD and HW Intrinsic nodes</para>
        /// </summary>
        JitDisableSimdVN,
#endif

        /// <summary>
        ///     <para>Default 0, enable the CSE of Constants, including nearby offsets. (only for ARM/ARM64/RISCV64)</para>
        ///     <para>If 1, disable all the CSE of Constants</para>
        ///     <para>If 2, enable the CSE of Constants but don't combine with nearby offsets. (only for ARM/ARM64/RISCV64)</para>
        ///     <para>If 3, enable the CSE of Constants including nearby offsets. (all platforms)</para>
        ///     <para>If 4, enable the CSE of Constants but don't combine with nearby offsets. (all platforms)</para>
        /// </summary>
        JitConstCSE,

        /// <summary>If nonzero, use the greedy RL policy.</summary>
        JitRLCSEGreedy,

        /// <summary>If nonzero, dump out details of parameterized policy evaluation and gradient updates.</summary>
        JitRLCSEVerbose,

#if DEBUG
        /// <summary>
        ///     <para>Allow fine-grained controls of CSEs done in a particular method</para>
        ///     <para>Specify method that will respond to the CSEMask.</para>
        ///     <para>-1 means feature disabled and all methods run CSE normally.</para>
        /// </summary>
        JitCSEHash,

        /// <summary>
        ///     <para>Bitmask of allowed CSEs in methods specified by JitCSEHash.</para>
        ///     <para>These bits control the "cse attempts" made by normal jitting, for the first 32 CSEs attempted (Note this is not the same as the CSE candidate number, which reflects the order in which CSEs were discovered).</para>
        ///     <list type="bullet">
        ///         <item>0: do no CSEs</item>
        ///         <item>1: do only the first CSE</item>
        ///         <item>2: do only the second CSE</item>
        ///         <item>C: do only the third and fourth CSEs</item>
        ///         <item>F: do only the first four CSEs</item>
        ///         <item>...etc...</item>
        ///         <item>FFFFFFFF : do all the CSEs normally done</item>
        ///     </list>
        /// </summary>
        JitCSEMask,

        /// <summary>Enable metric output in jit disasm and elsewhere</summary>
        JitMetrics,

        /// <summary>When nonzero, choose CSE candidates randomly, with hash salt specified by the (decimal) value of the config.</summary>
        JitRandomCSE,

        /// <summary>If nonzero, dump candidate feature values</summary>
        JitRLCSECandidateFeatures,

        /// <summary>
        ///     <para>Enable CSE_HeuristicRLHook</para>
        ///     <para>If 1, emit RL callbacks</para>
        /// </summary>
        JitRLHook,

        /// <summary>If 1, emit feature column names</summary>
        JitRLHookEmitFeatureNames,
#endif

        /// <summary></summary>
        JitEnableNoWayAssert,

        /// <summary>
        ///     <para>Display JIT memory usage statistics</para>
        ///     <para>The following should be wrapped inside "#if MEASURE_MEM_ALLOC / #endif", but some files include this one without bringing in the definitions from "jit.h" so we don't always know what the "true" value of that flag should be.</para>
        ///     <para>For now we take the easy way out and always include the flag, even in release builds (normally MEASURE_MEM_ALLOC is off for release builds but if it's toggled on for release in "jit.h" the flag would be missing for some includers).</para>
        ///     <para>TODO-Cleanup: need to make 'MEASURE_MEM_ALLOC' well-defined here at all times.</para>
        /// </summary>
        DisplayMemStats,

#if DEBUG
        /// <summary>Display JIT enregistration statistics</summary>
        JitEnregStats,
#endif

        /// <summary>Aggressive inlining of all methods</summary>
        JitAggressiveInlining,

        /// <summary>If 1, emit Enter/Leave/TailCall callbacks</summary>
        JitELTHookEnabled,

        /// <summary></summary>
        JitInlineSIMDMultiplier,

        /// <summary>Ex lclMAX_TRACKED constant.</summary>
        JitMaxLocalsToTrack,

#if FEATURE_ENABLE_NO_RANGE_CHECKS
        /// <summary>If 1, don't generate range checks</summary>
        JitNoRngChks,
#endif

#if OPT_CONFIG
        /// <summary>Perform assertion propagation optimization</summary>
        JitDoAssertionProp,

        /// <summary>Perform copy propagation on variables that appear redundant</summary>
        JitDoCopyProp,

        /// <summary>Perform optimization of induction variables</summary>
        JitDoOptimizeIVs,

        /// <summary>Perform Early Value Propagation</summary>
        JitDoEarlyProp,

        /// <summary>Perform loop hoisting on loop invariant values</summary>
        JitDoLoopHoisting,

        /// <summary>Perform loop inversion on "for/while" loops</summary>
        JitDoLoopInversion,
#endif

        /// <summary>limit inversion to loops with no more than this many tree nodes</summary>
        JitLoopInversionSizeLimit,

#if OPT_CONFIG
        /// <summary>Perform range check analysis</summary>
        JitDoRangeAnalysis,

        /// <summary>Perform VN-based dead store removal</summary>
        JitDoVNBasedDeadStoreRemoval,

        /// <summary>Perform redundant branch optimizations</summary>
        JitDoRedundantBranchOpts,

        /// <summary>Perform Static Single Assignment (SSA) numbering on the variables</summary>
        JitDoSsa,

        /// <summary>Perform value numbering on method expressions</summary>
        JitDoValueNumber,

        /// <summary>Perform If conversion</summary>
        JitDoIfConversion,

        /// <summary>Perform optimization of mask conversions</summary>
        JitDoOptimizeMaskConversions,

        /// <summary>Perform optimization of Await intrinsics</summary>
        JitDoOptimizeAwait,
#endif

        /// <summary>
        ///     <para>Save and reuse continuation instances in runtime async functions.</para>
        ///     <para>Also implies use of shared continuation layouts for all suspension points.</para>
        /// </summary>
        JitAsyncReuseContinuations,

        /// <summary>If zero, do not allow JitOptRepeat</summary>
        JitEnableOptRepeat,

        /// <summary>Number of times to repeat opts when repeating</summary>
        JitOptRepeatCount,

        /// <summary>Max # of MapSelect's considered for a particular top-level invocation.</summary>
        JitVNMapSelBudget,

        /// <summary>Convert recursive tail calls to loops</summary>
        TailCallLoopOpt,

        /// <summary>If set, measure the IR size after some phases and report it in the time log.</summary>
        JitMeasureIR,

        /// <summary>If set, report JIT metrics back to the EE after each method compilation.</summary>
        JitReportMetrics,

        /// <summary>If set, allow fast tail calls; otherwise allow only helper-based calls for explicit tail calls.</summary>
        FastTailCalls,

        /// <summary>Set to 1 to measure noway_assert usage. Only valid if MEASURE_NOWAY is defined.</summary>
        JitMeasureNowayAssert,

#if DEBUG
        /// <summary>Make extra queries to somewhat future-proof SuperPmi method contexts.</summary>
        EnableExtraSuperPmiQueries,

        /// <summary></summary>
        JitInlineDumpData,

        /// <summary>
        ///     <list type="bullet">
        ///         <item>1 = full xml (+ failures in DEBUG)</item>
        ///         <item>2 = only methods with inlines (+ failures in DEBUG)</item>
        ///         <item>3 = only methods with inlines, no failures</item>
        ///     </list>
        /// </summary>
        JitInlineDumpXml,

        /// <summary></summary>
        JitInlinePolicyDumpXml,

        /// <summary></summary>
        JitInlineLimit,

        /// <summary></summary>
        JitInlinePolicyDiscretionary,

        /// <summary></summary>
        JitInlinePolicyFull,

        /// <summary></summary>
        JitInlinePolicySize,

        /// <summary>nonzero enables; value is the external random seed</summary>
        JitInlinePolicyRandom,

        /// <summary></summary>
        JitInlinePolicyReplay,
#endif

        /// <summary>Extended version of DefaultPolicy that includes a more precise IL scan, relies on PGO if it exists and generally is more aggressive.</summary>
        JitExtDefaultPolicy,

        /// <summary></summary>
        JitExtDefaultPolicyMaxIL,

        /// <summary></summary>
        JitExtDefaultPolicyMaxILRoot,

        /// <summary></summary>
        JitExtDefaultPolicyMaxILProf,

        /// <summary></summary>
        JitExtDefaultPolicyMaxBB,

        /// <summary>
        ///     <para>Inliner uses the following formula for PGO-driven decisions:</para><code>BM = BM * ((1.0 - ProfTrust) + ProfWeight * ProfScale)</code>
        ///     <para>Where BM is a benefit multiplier composed from various observations (e.g. "const arg makes a branch foldable").</para>
        ///     <para>If a profile data can be trusted for 100% we can safely just give up on inlining anything inside cold blocks (except the cases where inlining in cold blocks improves type info/escape analysis for the whole caller).</para>
        ///     <para>For now, it's only applied for dynamic PGO.</para>
        /// </summary>
        JitExtDefaultPolicyProfTrust,

        /// <summary></summary>
        JitExtDefaultPolicyProfScale,

        /// <summary></summary>
        JitInlinePolicyModel,

        /// <summary></summary>
        JitInlinePolicyProfile,

        /// <summary></summary>
        JitInlinePolicyProfileThreshold,

        /// <summary></summary>
        JitObjectStackAllocation,

        /// <summary></summary>
        JitObjectStackAllocationRefClass,

        /// <summary></summary>
        JitObjectStackAllocationBoxedValueClass,

        /// <summary></summary>
        JitObjectStackAllocationConditionalEscape,

        /// <summary></summary>
        JitObjectStackAllocationArray,

        /// <summary></summary>
        JitObjectStackAllocationSize,

        /// <summary></summary>
        JitObjectStackAllocationTrackFields,

#if DEBUG
        /// <summary></summary>
        JitObjectStackAllocationDumpConnGraph,
#endif

        /// <summary></summary>
        JitEECallTimingInfo,

#if DEBUG
        /// <summary></summary>
        JitEnableFinallyCloning,

        /// <summary></summary>
        JitEnableRemoveEmptyTry,

        /// <summary></summary>
        JitEnableRemoveEmptyTryCatchOrTryFault,
#endif

        /// <summary>Overall master enable for Guarded Devirtualization.</summary>
        JitEnableGuardedDevirtualization,

        /// <summary>
        ///     <para>Number of types to probe for polymorphic virtual call-sites to devirtualize them, Max number is MAX_GDV_TYPE_CHECKS defined above ^.</para>
        ///     <para>-1 means it's up to JIT to decide</para>
        /// </summary>
        JitGuardedDevirtualizationMaxTypeChecks,

        /// <summary>Various policies for GuardedDevirtualization (0x4B == 75)</summary>
        JitGuardedDevirtualizationChainLikelihood,

        /// <summary></summary>
        JitGuardedDevirtualizationChainStatements,

#if DEBUG
        /// <summary></summary>
        JitRandomGuardedDevirtualization,
#endif

        /// <summary>Enable insertion of patchpoints into Tier0 methods, switching to optimized where needed.</summary>
        TC_OnStackReplacement,

        /// <summary>Initial patchpoint counter value used by jitted code</summary>
        TC_OnStackReplacement_InitialCounter,

        /// <summary>Enable partial compilation for Tier0 methods</summary>
        TC_PartialCompilation,

#if DEBUG
        /// <summary>If partial compilation is enabled, use random heuristic for patchpoint placement</summary>
        JitRandomPartialCompilation,
#endif

        /// <summary>
        ///     <para>Patchpoint strategy:</para>
        ///     <list type="bullet">
        ///         <item>0 - backedge sources</item>
        ///         <item>1 - backedge targets</item>
        ///         <item>2 - adaptive (default)</item>
        ///     </list>
        /// </summary>
        TC_PatchpointStrategy,

#if DEBUG
        /// <summary>Randomly sprinkle patchpoints. Value is the likelihood any given stack-empty point becomes a patchpoint.</summary>
        JitRandomOnStackReplacement,

        /// <summary>
        ///     <para>Place patchpoint at the specified IL offset, if possible.</para>
        ///     <para>Overrides random placement.</para>
        /// </summary>
        JitOffsetOnStackReplacement,
#endif

        /// <summary></summary>
        JitInterlockedProfiling,

        /// <summary></summary>
        JitScalableProfiling,

        /// <summary>number of unused extra slots per counter</summary>
        JitCounterPadding,

        /// <summary></summary>
        JitMinimalJitProfiling,

        /// <summary></summary>
        JitMinimalPrejitProfiling,

        /// <summary>Value profiling, e.g. Buffer.Memmove's size</summary>
        JitProfileValues,

        /// <summary>Profile castclass/isinst</summary>
        JitProfileCasts,

        /// <summary>Consume profile data (if any) for castclass/isinst</summary>
        JitConsumeProfileForCasts,

        /// <summary>Profile virtual and interface calls</summary>
        JitClassProfiling,

        /// <summary>Profile resolved delegate call targets</summary>
        JitDelegateProfiling,

        /// <summary>Profile resolved vtable call targets</summary>
        JitVTableProfiling,

        /// <summary>Profile edges instead of blocks</summary>
        JitEdgeProfiling,

        /// <summary>Collect counts as 64-bit values.</summary>
        JitCollect64BitCounts,

#if DEBUG
        /// <summary>1: Always add instrumentation if optimizing and not prejitting</summary>
        JitInstrumentIfOptimizing,
#endif

        /// <summary>Add instrumentation to inlined methods</summary>
        JitInstrumentInlinees,

        /// <summary>Ignore PGO data for all methods</summary>
        JitDisablePGO,

#if DEBUG
        /// <summary>Substitute random values for edge counts</summary>
        JitRandomEdgeCounts,

        /// <summary></summary>
        JitCrossCheckDevirtualizationAndPGO,

        /// <summary></summary>
        JitNoteFailedExactDevirtualization,

        /// <summary>Collect 64-bit counts randomly for some methods.</summary>
        JitRandomlyCollect64BitCounts,

        /// <summary>
        ///     <list type="bullet">
        ///         <item>1: profile synthesis for root methods</item>
        ///         <item>2: profile synthesis for root methods w/o PGO data</item>
        ///         <item>3: profile synthesis for root methods, blend with existing PGO data</item>
        ///     </list>
        /// </summary>
        JitSynthesizeCounts,

        /// <summary>
        ///     <para>If instrumenting the method, run synthesis and save the synthesis results as edge or block profile data.</para>
        ///     <para>Do not actually instrument.</para>
        /// </summary>
        JitPropagateSynthesizedCountsToProfileData,

        /// <summary>Use general (gauss-seidel) solver</summary>
        JitSynthesisUseSolver,
#endif

        /// <summary>Devirtualize virtual calls with getExactClasses (NativeAOT only for now)</summary>
        JitEnableExactDevirtualization,

        /// <summary>Force the generation of CFG checks</summary>
        JitForceControlFlowGuard,

        /// <summary>
        ///     <para>JitCFGUseDispatcher values:</para>
        ///     <list type="bullet">
        ///         <item>0: Never use dispatcher</item>
        ///         <item>1: Use dispatcher on all platforms that support it</item>
        ///         <item>2: Default behavior, depends on platform (yes on x64, no on arm64)</item>
        ///     </list>
        /// </summary>
        JitCFGUseDispatcher,

        /// <summary>Enable head and tail merging</summary>
        JitEnableHeadTailMerge,

        /// <summary>Enable physical promotion</summary>
        JitEnablePhysicalPromotion,

        /// <summary>Enable cross-block local assertion prop</summary>
        JitEnableCrossBlockLocalAssertionProp,

        /// <summary>Enable postorder local assertion prop</summary>
        JitEnablePostorderLocalAssertionProp,

        /// <summary>Enable strength reduction</summary>
        JitEnableStrengthReduction,

        /// <summary>Enable IV optimizations</summary>
        JitEnableInductionVariableOpts,

#if DEBUG && TARGET_ARM64
        /// <summary>
        ///     <para>JitSaveFpLrWithCalleeSavedRegisters:</para>
        ///     <list type="bullet">
        ///         <item>0: use default frame type decision</item>
        ///         <item>1: disable frames that save FP/LR registers with the callee-saved registers (at the top of the frame)</item>
        ///         <item>2: force all frames to use the frame types that save FP/LR registers with the callee-saved registers (at the top of the frame)</item>
        ///         <item>3: force all frames to use the frame types that save FP/LR registers with the callee-saved registers (at the top of the frame) and also force using the large funclet frame variation (frame 5) if possible.</item>
        ///     </list>
        /// </summary>
        JitSaveFpLrWithCalleeSavedRegisters,

        /// <summary>Experimental support for vector length agnostic implementation of Vector&lt;T&gt;</summary>
        JitUseScalableVectorT,
#endif

#if DEBUG && TARGET_LOONGARCH64
        /// <summary>Disable emitDispIns by default</summary>
        JitDispIns,
#endif

#if DEBUG && TARGET_WASM
        /// <summary>Set this to 1 to turn NYI_WASM into R2R unsupported failures instead of asserts.</summary>
        JitWasmNyiToR2RUnsupported,
#endif

#if TARGET_WASM
        /// <summary>Enable processing methods with funclets.</summary>
        JitWasmFunclets,
#endif

        /// <summary>Allow to enregister locals with struct type.</summary>
        JitEnregStructLocals,
    }

    private static readonly unsafe FrozenDictionary<ConfigInteger, (nuint Key, int DefaultValue)> ConfigIntegerMetadata = new Dictionary<ConfigInteger, (nuint, int)> {
#if DEBUG
        [AltJitLimit] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("AltJitLimit"u8)), 0),
        [AltJitSkipOnAssert] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("AltJitSkipOnAssert"u8)), 0),
        [BreakOnDumpToken] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("BreakOnDumpToken"u8)), -1),
        [DebugBreakOnVerificationFailure] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("DebugBreakOnVerificationFailure"u8)), 0),
        [DisplayLoopHoistStats] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitLoopHoistStats"u8)), 0),
        [DisplayLsraStats] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitLsraStats"u8)), 0),
        [EnablePCRelAddr] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnablePCRelAddr"u8)), 1),
        [JitAssertOnMaxRAPasses] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAssertOnMaxRAPasses"u8)), 0),
        [JitBreakEmitOutputInstr] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitBreakEmitOutputInstr"u8)), -1),
        [JitBreakMorphTree] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitBreakMorphTree"u8)), -1),
        [JitBreakOnBadCode] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitBreakOnBadCode"u8)), 0),
        [JitBreakOnMinOpts] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JITBreakOnMinOpts"u8)), 0),
        [JitCloneLoops] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCloneLoops"u8)), 1),
        [JitCloneLoopsWithEH] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCloneLoopsWithEH"u8)), 1),
        [JitCloneLoopsWithGdvTests] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCloneLoopsWithGdvTests"u8)), 1),
#endif

        [JitCloneLoopsSizeLimit] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCloneLoopsSizeLimit"u8)), 400),

#if DEBUG
        [JitDebugLogLoopCloning] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDebugLogLoopCloning"u8)), 0),
        [JitDefaultFill] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDefaultFill"u8)), 0xdd),
        [JitAlignLoopMinBlockWeight] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAlignLoopMinBlockWeight"u8)), DEFAULT_ALIGN_LOOP_MIN_BLOCK_WEIGHT),
        [JitAlignLoopMaxCodeSize] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAlignLoopMaxCodeSize"u8)), DEFAULT_MAX_LOOPSIZE_FOR_ALIGN),
        [JitAlignLoopBoundary] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAlignLoopBoundary"u8)), DEFAULT_ALIGN_LOOP_BOUNDARY),
        [JitAlignLoopForJcc] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAlignLoopForJcc"u8)), 0),
        [JitAlignLoopAdaptive] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAlignLoopAdaptive"u8)), 1),
        [JitHideAlignBehindJmp] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitHideAlignBehindJmp"u8)), 1),
        [JitOptimizeStructHiddenBuffer] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitOptimizeStructHiddenBuffer"u8)), 1),
        [JitUnrollLoopMaxIterationCount] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitUnrollLoopMaxIterationCount"u8)), DEFAULT_UNROLL_LOOP_MAX_ITERATION_COUNT),
        [JitUnrollLoopsWithEH] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitUnrollLoopsWithEH"u8)), 0),
        [JitDirectAlloc] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDirectAlloc"u8)), 0),
        [JitDoubleAlign] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoubleAlign"u8)), 1),
        [JitEmitPrintRefRegs] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEmitPrintRefRegs"u8)), 0),
        [JitEnableDevirtualization] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableDevirtualization"u8)), 1),
        [JitEnableLateDevirtualization] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableLateDevirtualization"u8)), 1),
        [JitExpensiveDebugCheckLevel] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitExpensiveDebugCheckLevel"u8)), 0),
        [JitForceFallback] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitForceFallback"u8)), 0),
        [JitFullyInt] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitFullyInt"u8)), 0),
        [JitFunctionTrace] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitFunctionTrace"u8)), 0),
        [JitGCChecks] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitGCChecks"u8)), 0),
        [JitGCInfoLogging] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitGCInfoLogging"u8)), 0),
        [JitHashBreak] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitHashBreak"u8)), -1),
        [JitHashHalt] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitHashHalt"u8)), -1),
        [JitInlineAdditionalMultiplier] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineAdditionalMultiplier"u8)), 0),
        [JitInlinePrintStats] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlinePrintStats"u8)), 0),
        [JitInlineSize] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineSize"u8)), DEFAULT_MAX_INLINE_SIZE),
        [JitInlineDepth] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineDepth"u8)), DEFAULT_MAX_INLINE_DEPTH),
#endif

        [JitInlineBudget] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineBudget"u8)), DEFAULT_INLINE_BUDGET),

#if DEBUG
        [JitForceInlineDepth] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitForceInlineDepth"u8)), DEFAULT_MAX_FORCE_INLINE_DEPTH),
#endif

        [JitInlineMethodsWithEH] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineMethodsWithEH"u8)), 1),

#if DEBUG
        [JitLongAddress] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitLongAddress"u8)), 0),
        [JitMaxUncheckedOffset] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitMaxUncheckedOffset"u8)), 8),
#endif

        [JitEnableGenericVirtualDevirtualization] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableGenericVirtualDevirtualization"u8)), 1),

#if DEBUG
        [JitMinOpts] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JITMinOpts"u8)), 0),
        [JitMinOptsBbCount] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JITMinOptsBbCount"u8)), DEFAULT_MIN_OPTS_BB_COUNT),
        [JitMinOptsCodeSize] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JITMinOptsCodeSize"u8)), DEFAULT_MIN_OPTS_CODE_SIZE),
        [JitMinOptsInstrCount] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JITMinOptsInstrCount"u8)), DEFAULT_MIN_OPTS_INSTR_COUNT),
        [JitMinOptsLvNumCount] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JITMinOptsLvNumcount"u8)), DEFAULT_MIN_OPTS_LV_NUM_COUNT),
        [JitMinOptsLvRefCount] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JITMinOptsLvRefcount"u8)), DEFAULT_MIN_OPTS_LV_REF_COUNT),
        [JitNoCSE] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoCSE"u8)), 0),
        [JitNoCSE2] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoCSE2"u8)), 0),
        [JitNoForceFallback] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoForceFallback"u8)), 0),
        [JitNoForwardSub] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoForwardSub"u8)), 0),
        [JitNoHoist] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoHoist"u8)), 0),
        [JitNoMemoryBarriers] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoMemoryBarriers"u8)), 0),
        [JitNoStructPromotion] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoStructPromotion"u8)), 0),
        [JitNoUnroll] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoUnroll"u8)), 0),
        [JitOrder] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitOrder"u8)), 0),
        [JitQueryCurrentStaticFieldClass] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitQueryCurrentStaticFieldClass"u8)), 1),
        [JitReportFastTailCallDecisions] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitReportFastTailCallDecisions"u8)), 0),
        [JitPInvokeCheckEnabled] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JITPInvokeCheckEnabled"u8)), 0),
        [JitPInvokeEnabled] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JITPInvokeEnabled"u8)), 1),
        [JitHoistLimit] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitHoistLimit"u8)), -1),
        [JitPrintInlinedMethodsVerbose] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitPrintInlinedMethodsVerboseLevel"u8)), 0),
        [JitProfileChecks] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitProfileChecks"u8)), -1),
        [JitRequired] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JITRequired"u8)), -1),
        [JitStackAllocToLocalSize] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStackAllocToLocalSize"u8)), DEFAULT_MAX_LOCALLOC_TO_LOCAL_SIZE),
        [JitSkipArrayBoundCheck] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitSkipArrayBoundCheck"u8)), 0),
        [JitSlowDebugChecksEnabled] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitSlowDebugChecksEnabled"u8)), 1),
        [JitSplitFunctionSize] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitSplitFunctionSize"u8)), 0),
        [JitSsaStress] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitSsaStress"u8)), 0),
        [JitStackChecks] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStackChecks"u8)), 0),
        [JitStress] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStress"u8)), 0),
        [JitStressBBProf] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressBBProf"u8)), 0),
        [JitStressProcedureSplitting] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressProcedureSplitting"u8)), 0),
        [JitStressRegs] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressRegs"u8)), 0),
        [JitStressSplitTreeLimit] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressSplitTreeLimit"u8)), -1),
        [JitVNMapSelLimit] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitVNMapSelLimit"u8)), 0),
        [RunAltJitCode] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("RunAltJitCode"u8)), 1),
        [RunComponentUnitTests] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitComponentUnitTests"u8)), 0),
        [ShouldInjectFault] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("InjectFault"u8)), 0),
        [TailcallStress] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("TailcallStress"u8)), 0),
        [JitHashDump] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitHashDump"u8)), -1),
        [JitDumpTier0] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpTier0"u8)), 1),
        [JitDumpOSR] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpOSR"u8)), 1),
        [JitDumpAtOSROffset] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpAtOSROffset"u8)), -1),
        [JitDumpInlinePhases] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpInlinePhases"u8)), 1),
        [JitDumpASCII] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpASCII"u8)), 1),
        [JitDumpTerseLsra] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpTerseLsra"u8)), 1),
        [JitDumpToDebugger] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpToDebugger"u8)), 0),
        [JitDumpVerboseSsa] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpVerboseSsa"u8)), 0),
        [JitDumpVerboseTrees] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpVerboseTrees"u8)), 0),
        [JitDumpTreeIDs] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpTreeIDs"u8)), 1),
        [JitDumpBeforeAfterMorph] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpBeforeAfterMorph"u8)), 0),
        [JitDumpTerseNextBlock] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpTerseNextBlock"u8)), 0),
        [JitFakeProcedureSplitting] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitFakeProcedureSplitting"u8)), 0),
        [JitDumpFgHash] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgHash"u8)), 0),
        [JitDumpFgTier0] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgTier0"u8)), 1),
        [JitDumpFgDot] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgDot"u8)), 1),
        [JitDumpFgEH] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgEH"u8)), 0),
        [JitDumpFgLoops] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgLoops"u8)), 0),
        [JitDumpFgConstrained] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgConstrained"u8)), 1),
        [JitDumpFgBlockID] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgBlockID"u8)), 0),
        [JitDumpFgBlockFlags] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgBlockFlags"u8)), 0),
        [JitDumpFgLoopFlags] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgLoopFlags"u8)), 0),
        [JitDumpFgBlockOrder] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgBlockOrder"u8)), 0),
        [JitDumpFgMemorySsa] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgMemorySsa"u8)), 0),
        [JitStressModeNamesOnly] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressModeNamesOnly"u8)), 0),
#endif

        [JitDisasmTesting] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmTesting"u8)), 0),
        [JitDisasmDiffable] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmDiffable"u8)), 0),
        [JitDisasmSummary] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmSummary"u8)), 0),
        [JitDisasmOnlyOptimized] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmOnlyOptimized"u8)), 0),
        [JitDisasmWithAlignmentBoundaries] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmWithAlignmentBoundaries"u8)), 0),
        [JitDisasmWithCodeBytes] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmWithCodeBytes"u8)), 0),

#if DEBUG
        [JitDisasmWithGC] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmWithGC"u8)), 0),
        [JitDisasmWithDebugInfo] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmWithDebugInfo"u8)), 0),
        [JitDisasmSpilled] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmSpilled"u8)), 0),
        [JitDasmWithAddress] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDasmWithAddress"u8)), 0),
#endif

        [RichDebugInfo] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("RichDebugInfo"u8)), 0),

#if FEATURE_LOOP_ALIGN
        [JitAlignLoops] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAlignLoops"u8)), 1),
#else
        [JitAlignLoops] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAlignLoops"u8)), 0),
#endif

        [AltJitAssertOnNYI] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("AltJitAssertOnNYI"u8)), 1),
        [EnableEHWriteThru] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableEHWriteThru"u8)), 1),
        [EnableMultiRegLocals] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableMultiRegLocals"u8)), 1),
        [JitNoInline] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoInline"u8)), 0),

#if DEBUG
        [JitStressRex2Encoding] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressRex2Encoding"u8)), 0),
        [JitStressPromotedEvexEncoding] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressPromotedEvexEncoding"u8)), 0),
#endif

#if DEBUG && TARGET_XARCH
        [JitStressEvexEncoding] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressEvexEncoding"u8)), 0),
#endif

#if TARGET_LOONGARCH64
        //TODO: should implement LoongArch64's features
        [EnableHWIntrinsic] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableHWIntrinsic"u8)), 0),
#else
        [EnableHWIntrinsic] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableHWIntrinsic"u8)), 1),
#endif

#if TARGET_XARCH
        [EnableAVX] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVX"u8)), 1),
        [EnableAVX2] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVX2"u8)), 1),
        [EnableAVX512] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVX512"u8)), 1),
        [EnableAVX512BMM] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVX512BMM"u8)), 1),
        [EnableAVX512v2] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVX512v2"u8)), 1),
        [EnableAVX512v3] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVX512v3"u8)), 1),
        [EnableAVX10v1] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVX10v1"u8)), 1),
        [EnableAVX10v2] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVX10v2"u8)), 0),
        [EnableAPX] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAPX"u8)), 0),
        [EnableAES] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAES"u8)), 1),
        [EnableAVX512VP2INTERSECT] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVX512VP2INTERSECT"u8)), 1),
        [EnableAVXIFMA] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVXIFMA"u8)), 1),
        [EnableAVXVNNI] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVXVNNI"u8)), 1),
        [EnableAVXVNNIINT] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVXVNNIINT"u8)), 1),
        [EnableGFNI] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableGFNI"u8)), 1),
        [EnableSHA] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableSHA"u8)), 1),
        [EnableVAES] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableVAES"u8)), 1),
        [EnableWAITPKG] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableWAITPKG"u8)), 1),
        [EnableX86Serialize] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableX86Serialize"u8)), 1),
#elif TARGET_ARM64
        [EnableArm64Aes] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Aes"u8)), 1),
        [EnableArm64Atomics] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Atomics"u8)), 1),
        [EnableArm64Crc32] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Crc32"u8)), 1),
        [EnableArm64Dczva] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Dczva"u8)), 1),
        [EnableArm64Dp] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Dp"u8)), 1),
        [EnableArm64Rdm] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Rdm"u8)), 1),
        [EnableArm64Sha1] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Sha1"u8)), 1),
        [EnableArm64Sha256] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Sha256"u8)), 1),
        [EnableArm64Sve] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Sve"u8)), 1),
        [EnableArm64Sve2] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Sve2"u8)), 1),
        [EnableArm64Sha3] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Sha3"u8)), 1),
        [EnableArm64Sm4] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Sm4"u8)), 1),
        [EnableArm64SveAes] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64SveAes"u8)), 1),
        [EnableArm64SveSha3] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64SveSha3"u8)), 1),
        [EnableArm64SveSm4] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64SveSm4"u8)), 1),
#elif TARGET_RISCV64
        [EnableRiscV64Zba] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableRiscV64Zba"u8)), 1),
        [EnableRiscV64Zbb] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableRiscV64Zbb"u8)), 1),
        [EnableRiscV64Zbs] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableRiscV64Zbs"u8)), 1),
#endif

        [EnableEmbeddedBroadcast] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableEmbeddedBroadcast"u8)), 1),
        [EnableEmbeddedMasking] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableEmbeddedMasking"u8)), 1),
        [EnableApxNDD] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableApxNDD"u8)), 0),
        [EnableApxConditionalChaining] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableApxConditionalChaining"u8)), 0),
        [EnableApxPPX] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableApxPPX"u8)), 0),
        [EnableApxZU] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableApxZU"u8)), 0),

#if FEATURE_SIMD
        [JitDisableSimdVN] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisableSimdVN"u8)), 0),
#endif

        [JitConstCSE] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitConstCSE"u8)), CONST_CSE_ENABLE_ARM_RISCV64),
        [JitRLCSEGreedy] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRLCSEGreedy"u8)), 0),
        [JitRLCSEVerbose] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRLCSEVerbose"u8)), 0),

#if DEBUG
        [JitCSEHash] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCSEHash"u8)), -1),
        [JitCSEMask] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCSEMask"u8)), 0),
        [ConfigInteger.JitMetrics] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitMetrics"u8)), 0),
        [JitRandomCSE] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRandomCSE"u8)), 0),
        [JitRLCSECandidateFeatures] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRLCSECandidateFeatures"u8)), 0),
        [JitRLHook] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRLHook"u8)), 0),
        [JitRLHookEmitFeatureNames] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRLHookEmitFeatureNames"u8)), 0),
#endif

#if DEBUG
        [JitEnableNoWayAssert] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableNoWayAssert"u8)), 1),
#else
        [JitEnableNoWayAssert] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableNoWayAssert"u8)), 0),
#endif

        [DisplayMemStats] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitMemStats"u8)), 0),

#if DEBUG
        [JitEnregStats] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnregStats"u8)), 0),
#endif

        [JitAggressiveInlining] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAggressiveInlining"u8)), 0),
        [JitELTHookEnabled] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitELTHookEnabled"u8)), 0),
        [JitInlineSIMDMultiplier] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineSIMDMultiplier"u8)), 3),
        [JitMaxLocalsToTrack] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitMaxLocalsToTrack"u8)), 0x400),

#if FEATURE_ENABLE_NO_RANGE_CHECKS
        [JitNoRngChks] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoRngChks"u8)), 0),
#endif

#if OPT_CONFIG
        [JitDoAssertionProp] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoAssertionProp"u8)), 1),
        [JitDoCopyProp] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoCopyProp"u8)), 1),
        [JitDoOptimizeIVs] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoOptimizeIVs"u8)), 1),
        [JitDoEarlyProp] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoEarlyProp"u8)), 1),
        [JitDoLoopHoisting] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoLoopHoisting"u8)), 1),
        [JitDoLoopInversion] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoLoopInversion"u8)), 1),
#endif

        [JitLoopInversionSizeLimit] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitLoopInversionSizeLimit"u8)), 100),

#if OPT_CONFIG
        [JitDoRangeAnalysis] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoRangeAnalysis"u8)), 1),
        [JitDoVNBasedDeadStoreRemoval] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoVNBasedDeadStoreRemoval"u8)), 1),
        [JitDoRedundantBranchOpts] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoRedundantBranchOpts"u8)), 1),
        [JitDoSsa] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoSsa"u8)), 1),
        [JitDoValueNumber] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoValueNumber"u8)), 1),
        [JitDoIfConversion] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoIfConversion"u8)), 1),
        [JitDoOptimizeMaskConversions] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoOptimizeMaskConversions"u8)), 1),
        [JitOptimizeAwait] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitOptimizeAwait"u8)), 1),
#endif

        [JitAsyncReuseContinuations] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAsyncReuseContinuations"u8)), 1),
        [JitEnableOptRepeat] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableOptRepeat"u8)), 1),
        [JitOptRepeatCount] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitOptRepeatCount"u8)), 2),
        [JitVNMapSelBudget] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitVNMapSelBudget"u8)), DEFAULT_MAP_SELECT_BUDGET),
        [TailCallLoopOpt] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("TailCallLoopOpt"u8)), 1),
        [JitMeasureIR] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitMeasureIR"u8)), 0),
        [JitReportMetrics] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitReportMetrics"u8)), 0),
        [FastTailCalls] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("FastTailCalls"u8)), 1),
        [JitMeasureNowayAssert] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitMeasureNowayAssert"u8)), 0),

#if DEBUG
        [EnableExtraSuperPmiQueries] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableExtraSuperPmiQueries"u8)), 0),
        [JitInlineDumpData] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineDumpData"u8)), 0),
        [JitInlineDumpXml] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineDumpXml"u8)), 0),
        [JitInlinePolicyDumpXml] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlinePolicyDumpXml"u8)), 0),
        [JitInlineLimit] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineLimit"u8)), -1),
        [JitInlinePolicyDiscretionary] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlinePolicyDiscretionary"u8)), 0),
        [JitInlinePolicyFull] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlinePolicyFull"u8)), 0),
        [JitInlinePolicySize] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlinePolicySize"u8)), 0),
        [JitInlinePolicyRandom] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlinePolicyRandom"u8)), 0),
        [JitInlinePolicyReplay] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlinePolicyReplay"u8)), 0),
#endif

        [JitExtDefaultPolicy] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitExtDefaultPolicy"u8)), 1),
        [JitExtDefaultPolicyMaxIL] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitExtDefaultPolicyMaxIL"u8)), 0x80),
        [JitExtDefaultPolicyMaxILRoot] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitExtDefaultPolicyMaxILRoot"u8)), 0x100),
        [JitExtDefaultPolicyMaxILProf] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitExtDefaultPolicyMaxILProf"u8)), 0x400),
        [JitExtDefaultPolicyMaxBB] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitExtDefaultPolicyMaxBB"u8)), 7),
        [JitExtDefaultPolicyProfTrust] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitExtDefaultPolicyProfTrust"u8)), 0x7),
        [JitExtDefaultPolicyProfScale] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitExtDefaultPolicyProfScale"u8)), 0x2A),
        [JitInlinePolicyModel] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlinePolicyModel"u8)), 0),
        [JitInlinePolicyProfile] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlinePolicyProfile"u8)), 0),
        [JitInlinePolicyProfileThreshold] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlinePolicyProfileThreshold"u8)), 40),
        [JitObjectStackAllocation] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocation"u8)), 1),
        [JitObjectStackAllocationRefClass] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationRefClass"u8)), 1),
        [JitObjectStackAllocationBoxedValueClass] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationBoxedValueClass"u8)), 1),
        [JitObjectStackAllocationConditionalEscape] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationConditionalEscape"u8)), 1),
        [JitObjectStackAllocationArray] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationArray"u8)), 1),
        [JitObjectStackAllocationSize] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationSize"u8)), 528),
        [JitObjectStackAllocationTrackFields] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationTrackFields"u8)), 1),

#if DEBUG
        [JitObjectStackAllocationDumpConnGraph] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationDumpConnGraph"u8)), 0),
#endif

        [JitEECallTimingInfo] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEECallTimingInfo"u8)), 0),

#if DEBUG
        [JitEnableFinallyCloning] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableFinallyCloning"u8)), 1),
        [JitEnableRemoveEmptyTry] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableRemoveEmptyTry"u8)), 1),
        [JitEnableRemoveEmptyTryCatchOrTryFault] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableRemoveEmptyTryCatchOrTryFault"u8)), 1),
#endif

        [JitEnableGuardedDevirtualization] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableGuardedDevirtualization"u8)), 1),
        [JitGuardedDevirtualizationMaxTypeChecks] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitGuardedDevirtualizationMaxTypeChecks"u8)), -1),
        [JitGuardedDevirtualizationChainLikelihood] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitGuardedDevirtualizationChainLikelihood"u8)), 0x4B),
        [JitGuardedDevirtualizationChainStatements] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitGuardedDevirtualizationChainStatements"u8)), 1),

#if DEBUG
        [JitRandomGuardedDevirtualization] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRandomGuardedDevirtualization"u8)), 0),
#endif

#if FEATURE_ON_STACK_REPLACEMENT
        [TC_OnStackReplacement] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("TC_OnStackReplacement"u8)), 1),
#else
        [TC_OnStackReplacement] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("TC_OnStackReplacement"u8)), 0),
#endif

        [TC_OnStackReplacement_InitialCounter] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("TC_OnStackReplacement_InitialCounter"u8)), 1000),
        [TC_PartialCompilation] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("TC_PartialCompilation"u8)), 0),

#if DEBUG
        [JitRandomPartialCompilation] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRandomPartialCompilation"u8)), 0),
#endif

        [TC_PatchpointStrategy] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("TC_PatchpointStrategy"u8)), 2),

#if DEBUG
        [JitRandomOnStackReplacement] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRandomOnStackReplacement"u8)), 0),
        [JitOffsetOnStackReplacement] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitOffsetOnStackReplacement"u8)), -1),
#endif

        [JitInterlockedProfiling] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInterlockedProfiling"u8)), 0),
        [JitScalableProfiling] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitScalableProfiling"u8)), 1),
        [JitCounterPadding] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCounterPadding"u8)), 0),
        [JitMinimalJitProfiling] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitMinimalJitProfiling"u8)), 1),
        [JitMinimalPrejitProfiling] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitMinimalPrejitProfiling"u8)), 0),
        [JitProfileValues] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitProfileValues"u8)), 1),
        [JitProfileCasts] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitProfileCasts"u8)), 1),
        [JitConsumeProfileForCasts] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitConsumeProfileForCasts"u8)), 1),
        [JitClassProfiling] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitClassProfiling"u8)), 1),
        [JitDelegateProfiling] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDelegateProfiling"u8)), 1),
        [JitVTableProfiling] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitVTableProfiling"u8)), 0),
        [JitEdgeProfiling] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEdgeProfiling"u8)), 1),
        [JitCollect64BitCounts] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCollect64BitCounts"u8)), 0),

#if DEBUG
        [JitInstrumentIfOptimizing] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInstrumentIfOptimizing"u8)), 0),
#endif

        [JitInstrumentInlinees] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInstrumentInlinees"u8)), 1),
        [JitDisablePGO] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisablePGO"u8)), 0),

#if DEBUG
        [JitRandomEdgeCounts] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRandomEdgeCounts"u8)), 0),
        [JitCrossCheckDevirtualizationAndPGO] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCrossCheckDevirtualizationAndPGO"u8)), 0),
        [JitNoteFailedExactDevirtualization] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoteFailedExactDevirtualization"u8)), 0),
        [JitRandomlyCollect64BitCounts] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRandomlyCollect64BitCounts"u8)), 0),
        [JitSynthesizeCounts] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitSynthesizeCounts"u8)), 0),
        [JitPropagateSynthesizedCountsToProfileData] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitPropagateSynthesizedCountsToProfileData"u8)), 0),
        [JitSynthesisUseSolver] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitSynthesisUseSolver"u8)), 1),
#endif

        [JitEnableExactDevirtualization] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableExactDevirtualization"u8)), 1),
        [JitForceControlFlowGuard] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitForceControlFlowGuard"u8)), 0),
        [JitCFGUseDispatcher] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCFGUseDispatcher"u8)), 2),
        [JitEnableHeadTailMerge] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableHeadTailMerge"u8)), 1),
        [JitEnablePhysicalPromotion] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnablePhysicalPromotion"u8)), 1),
        [JitEnableCrossBlockLocalAssertionProp] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableCrossBlockLocalAssertionProp"u8)), 1),
        [JitEnablePostorderLocalAssertionProp] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnablePostorderLocalAssertionProp"u8)), 1),
        [JitEnableStrengthReduction] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableStrengthReduction"u8)), 1),
        [JitEnableInductionVariableOpts] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableInductionVariableOpts"u8)), 1),

#if DEBUG && TARGET_ARM64
        [JitSaveFpLrWithCalleeSavedRegisters] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitSaveFpLrWithCalleeSavedRegisters"u8)), 0),
        [JitUseScalableVectorT] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitUseScalableVectorT"u8)), 0),
#endif

#if DEBUG && TARGET_LOONGARCH64
        [JitDispIns] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDispIns"u8)), 0),
#endif

#if DEBUG && TARGET_WASM
        [JitWasmNyiToR2RUnsupported] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitWasmNyiToR2RUnsupported"u8)), 0),
#endif

#if TARGET_WASM
        [JitWasmFunclets] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitWasmFunclets"u8)), 0),
#endif

        [JitEnregStructLocals] = ((nuint)Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnregStructLocals"u8)), 1),
    }.ToFrozenDictionary();
}
