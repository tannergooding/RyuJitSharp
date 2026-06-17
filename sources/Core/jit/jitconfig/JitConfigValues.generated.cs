// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public partial struct JitConfigValues
{
#if DEBUG
    private int _altJitLimit;
    private int _altJitSkipOnAssert;
    private int _breakOnDumpToken;
    private int _debugBreakOnVerificationFailure;
    private int _displayLoopHoistStats;
    private int _displayLsraStats;
    private unsafe byte* _jitLsraOrdering;
    private int _enablePCRelAddr;
    private int _jitAssertOnMaxRAPasses;
    private int _jitBreakEmitOutputInstr;
    private int _jitBreakMorphTree;
    private int _jitBreakOnBadCode;
    private int _jitBreakOnMinOpts;
    private int _jitCloneLoops;
    private int _jitCloneLoopsWithEH;
    private int _jitCloneLoopsWithGdvTests;
#endif
    private int _jitCloneLoopsSizeLimit;
    private int _jitCloneLoopsMinPerCallRatio;
#if DEBUG
    private int _jitDebugLogLoopCloning;
    private int _jitDefaultFill;
    private int _jitAlignLoopMinBlockWeight;
    private int _jitAlignLoopMaxCodeSize;
    private int _jitAlignLoopBoundary;
    private int _jitAlignLoopForJcc;
    private int _jitAlignLoopAdaptive;
    private int _jitHideAlignBehindJmp;
    private int _jitOptimizeStructHiddenBuffer;
#endif
    private int _jitEnableStoreLclFldCoalescing;
#if DEBUG
    private int _jitUnrollLoopMaxIterationCount;
    private int _jitUnrollLoopsWithEH;
    private int _jitDirectAlloc;
    private int _jitDoubleAlign;
    private int _jitEmitPrintRefRegs;
    private int _jitEnableDevirtualization;
    private int _jitEnableLateDevirtualization;
    private int _jitExpensiveDebugCheckLevel;
    private int _jitForceFallback;
    private int _jitFullyInt;
    private int _jitFunctionTrace;
    private int _jitGCChecks;
    private int _jitGCInfoLogging;
    private int _jitHashBreak;
    private int _jitHashHalt;
    private int _jitInlineAdditionalMultiplier;
    private int _jitInlinePrintStats;
    private int _jitInlineSize;
    private int _jitInlineDepth;
#endif
    private int _jitInlineBudget;
#if DEBUG
    private int _jitForceInlineDepth;
#endif
    private int _jitInlineMethodsWithEH;
#if DEBUG
    private unsafe byte* _jitInlineMethodsWithEHRange;
    private int _jitLongAddress;
    private int _jitMaxUncheckedOffset;
#endif
#if TARGET_ARM64
    private int _jitPacEnabled;
#endif
    private int _jitEnableGenericVirtualDevirtualization;
#if DEBUG
    private int _jitMinOpts;
    private MethodSet _jitMinOptsName;
    private int _jitMinOptsBbCount;
    private int _jitMinOptsCodeSize;
    private int _jitMinOptsInstrCount;
    private int _jitMinOptsLvNumCount;
    private int _jitMinOptsLvRefCount;
    private int _jitNoCSE;
    private int _jitNoCSE2;
    private int _jitNoForceFallback;
    private int _jitNoForwardSub;
    private int _jitNoHoist;
    private int _jitNoMemoryBarriers;
    private int _jitNoStructPromotion;
    private int _jitNoUnroll;
    private int _jitOrder;
    private int _jitQueryCurrentStaticFieldClass;
    private int _jitReportFastTailCallDecisions;
    private int _jitPInvokeCheckEnabled;
    private int _jitPInvokeEnabled;
    private int _jitHoistLimit;
    private int _jitPrintInlinedMethodsVerbose;
    private MethodSet _jitPrintInlinedMethods;
    private MethodSet _jitPrintDevirtualizedMethods;
    private int _jitProfileChecks;
    private int _jitRequired;
    private int _jitStackAllocToLocalSize;
    private int _jitSkipArrayBoundCheck;
    private int _jitSlowDebugChecksEnabled;
    private int _jitSplitFunctionSize;
    private int _jitSsaStress;
    private int _jitStackChecks;
    private int _jitStress;
    private int _jitStressBBProf;
    private int _jitStressProcedureSplitting;
    private int _jitStressRegs;
    private unsafe byte* _jitStressRegsRange;
    private int _jitStressSplitTreeLimit;
    private int _jitVNMapSelLimit;
    private int _runAltJitCode;
    private int _runComponentUnitTests;
    private int _shouldInjectFault;
    private int _tailcallStress;
    private MethodSet _jitBreak;
    private MethodSet _jitDebugBreak;
    private MethodSet _jitDump;
    private int _jitHashDump;
    private int _jitDumpTier0;
    private int _jitDumpOSR;
    private int _jitDumpAtOSROffset;
    private int _jitDumpInlinePhases;
    private int _jitDumpASCII;
    private int _jitDumpTerseLsra;
    private int _jitDumpToDebugger;
    private int _jitDumpVerboseSsa;
    private int _jitDumpVerboseTrees;
    private int _jitDumpTreeIDs;
    private int _jitDumpBeforeAfterMorph;
    private int _jitDumpTerseNextBlock;
    private MethodSet _jitEHDump;
    private MethodSet _jitExclude;
    private int _jitFakeProcedureSplitting;
    private MethodSet _jitForceProcedureSplitting;
    private MethodSet _jitGCDump;
    private MethodSet _jitDebugDump;
    private MethodSet _jitHalt;
    private MethodSet _jitInclude;
    private MethodSet _jitLateDisasm;
    private unsafe byte* _jitLateDisasmTo;
    private MethodSet _jitNoProcedureSplitting;
    private MethodSet _jitNoProcedureSplittingEH;
    private MethodSet _jitStressOnly;
    private MethodSet _jitUnwindDump;
    private MethodSet _jitDumpFg;
    private int _jitDumpFgHash;
    private int _jitDumpFgTier0;
    private unsafe byte* _jitDumpFgDir;
    private unsafe byte* _jitDumpFgFile;
    private unsafe byte* _jitDumpFgPhase;
    private unsafe byte* _jitDumpFgPrePhase;
    private int _jitDumpFgDot;
    private int _jitDumpFgEH;
    private int _jitDumpFgLoops;
    private int _jitDumpFgConstrained;
    private int _jitDumpFgBlockID;
    private int _jitDumpFgBlockFlags;
    private int _jitDumpFgLoopFlags;
    private int _jitDumpFgBlockOrder;
    private int _jitDumpFgMemorySsa;
    private unsafe byte* _jitRange;
    private unsafe byte* _jitStressModeNames;
    private int _jitStressModeNamesOnly;
    private unsafe byte* _jitStressModeNamesAllow;
    private unsafe byte* _jitStressModeNamesNot;
    private unsafe byte* _jitStressRange;
    private MethodSet _jitEmitUnitTests;
    private unsafe byte* _jitEmitUnitTestsSections;
#endif
    private MethodSet _jitDisasm;
    private int _jitDisasmTesting;
    private int _jitDisasmDiffable;
    private int _jitDisasmSummary;
    private int _jitDisasmOnlyOptimized;
    private int _jitDisasmWithAlignmentBoundaries;
    private int _jitDisasmWithCodeBytes;
#if DEBUG
    private unsafe byte* _jitDisasmAssemblies;
    private int _jitDisasmWithGC;
    private int _jitDisasmWithDebugInfo;
    private int _jitDisasmSpilled;
    private int _jitDisasmWithAddress;
#endif
    private unsafe byte* _jitStdOutFile;
    private int _richDebugInfo;
#if DEBUG
    private unsafe byte* _writeRichDebugInfoFile;
#endif
#if FEATURE_LOOP_ALIGN
    private int _jitAlignLoops;
#else
    private int _jitAlignLoops;
#endif
    private int _altJitAssertOnNYI;
    private int _enableEHWriteThru;
    private int _enableMultiRegLocals;
    private int _jitNoInline;
#if DEBUG
#if DEBUG
    private int _jitStressRex2Encoding;
    private int _jitStressPromotedEvexEncoding;
#endif
#if TARGET_AMD64 || TARGET_X86
    private int _jitStressEvexEncoding;
#endif
#endif
#if TARGET_LOONGARCH64
    private int _enableHWIntrinsic;
#else
    private int _enableHWIntrinsic;
#endif
#if TARGET_AMD64 || TARGET_X86
    private int _enableAVX;
    private int _enableAVX2;
    private int _enableAVX512;
    private int _enableAVX512BMM;
    private int _enableAVX512v2;
    private int _enableAVX512v3;
    private int _enableAVX10v1;
    private int _enableAVX10v2;
    private int _enableAPX;
    private int _enableAES;
    private int _enableAVX512VP2INTERSECT;
    private int _enableAVXIFMA;
    private int _enableAVXVNNI;
    private int _enableAVXVNNIINT;
    private int _enableGFNI;
    private int _enableSHA;
    private int _enableVAES;
    private int _enableWAITPKG;
    private int _enableX86Serialize;
#elif TARGET_ARM64
    private int _enableArm64Aes;
    private int _enableArm64Atomics;
    private int _enableArm64Crc32;
    private int _enableArm64Dczva;
    private int _enableArm64Dp;
    private int _enableArm64Rdm;
    private int _enableArm64Sha1;
    private int _enableArm64Sha256;
    private int _enableArm64Sve;
    private int _enableArm64Sve2;
    private int _enableArm64Sha3;
    private int _enableArm64Sm4;
    private int _enableArm64SveAes;
    private int _enableArm64SveSha3;
    private int _enableArm64SveSm4;
#elif TARGET_RISCV64
    private int _enableRiscV64Zba;
    private int _enableRiscV64Zbb;
    private int _enableRiscV64Zbs;
#endif
    private int _enableEmbeddedBroadcast;
    private int _enableEmbeddedMasking;
    private int _enableApxNDD;
    private int _enableApxConditionalChaining;
    private int _enableApxPPHint;
    private int _enableApxPP2;
    private int _enableApxZU;
#if FEATURE_SIMD
    private int _jitDisableSimdVN;
#endif
    private int _jitConstCSE;
    private int _jitRLCSEGreedy;
    private int _jitRLCSEVerbose;
#if DEBUG
    private int _jitCSEHash;
    private int _jitCSEMask;
    private int _jitMetrics;
    private int _jitRandomCSE;
    private unsafe byte* _jitReplayCSE;
    private unsafe byte* _jitReplayCSEReward;
    private unsafe byte* _jitRLCSE;
    private unsafe byte* _jitRLCSEAlpha;
    private int _jitRLCSECandidateFeatures;
    private int _jitRLHook;
    private int _jitRLHookEmitFeatureNames;
    private unsafe byte* _jitRLHookCSEDecisions;
#endif
#if !DEBUG && !_DEBUG
    private int _jitEnableNoWayAssert;
#else
    private int _jitEnableNoWayAssert;
#endif
    private int _displayMemStats;
#if DEBUG
    private int _jitEnregStats;
#endif
    private int _jitAggressiveInlining;
    private int _jitELTHookEnabled;
    private int _jitInlineSIMDMultiplier;
    private int _jitMaxLocalsToTrack;
#if FEATURE_ENABLE_NO_RANGE_CHECKS
    private int _jitNoRngChks;
#endif
#if OPT_CONFIG
    private int _jitDoAssertionProp;
    private int _jitDoCopyProp;
    private int _jitDoOptimizeIVs;
    private int _jitDoEarlyProp;
    private int _jitDoLoopHoisting;
    private int _jitDoLoopInversion;
#endif
    private int _jitLoopInversionSizeLimit;
#if OPT_CONFIG
    private int _jitDoRangeAnalysis;
    private int _jitDoVNBasedDeadStoreRemoval;
    private int _jitDoRedundantBranchOpts;
    private unsafe byte* _jitEnableRboRange;
    private unsafe byte* _jitEnableHeadTailMergeRange;
    private unsafe byte* _jitEnableVNBasedDeadStoreRemovalRange;
    private unsafe byte* _jitEnableEarlyLivenessRange;
    private unsafe byte* _jitOnlyOptimizeRange;
    private unsafe byte* _jitEnablePhysicalPromotionRange;
    private unsafe byte* _jitEnableCrossBlockLocalAssertionPropRange;
    private unsafe byte* _jitEnableInductionVariableOptsRange;
    private unsafe byte* _jitEnableLocalAddrPropagationRange;
    private int _jitDoSsa;
    private int _jitDoValueNumber;
    private unsafe byte* _jitOptRepeatRange;
    private int _jitDoIfConversion;
    private int _jitDoOptimizeMaskConversions;
    private int _jitOptimizeAwait;
    private unsafe byte* _jitAsyncDefaultValueAnalysisRange;
    private unsafe byte* _jitAsyncPreservedValueAnalysisRange;
    private unsafe byte* _jitAsyncReuseContinuationsRange;
#endif
    private int _jitAsyncReuseContinuations;
    private int _jitEnableOptRepeat;
    private MethodSet _jitOptRepeat;
    private int _jitOptRepeatCount;
    private int _jitVNMapSelBudget;
    private int _tailCallLoopOpt;
    private MethodSet _altJit;
    private MethodSet _altJitNgen;
    private unsafe byte* _altJitExcludeAssemblies;
    private int _jitMeasureIR;
    private int _jitReportMetrics;
    private unsafe byte* _jitFuncInfoFile;
    private unsafe byte* _jitTimeLogCsv;
    private unsafe byte* _jitTimeLogFile;
    private unsafe byte* _tailCallOpt;
    private int _fastTailCalls;
    private int _jitMeasureNowayAssert;
    private unsafe byte* _jitMeasureNowayAssertFile;
#if DEBUG
    private int _enableExtraSuperPmiQueries;
    private int _jitInlineDumpData;
    private int _jitInlineDumpXml;
    private unsafe byte* _jitInlineDumpXmlFile;
    private int _jitInlinePolicyDumpXml;
    private int _jitInlineLimit;
    private int _jitInlinePolicyDiscretionary;
    private int _jitInlinePolicyFull;
    private int _jitInlinePolicySize;
    private int _jitInlinePolicyRandom;
    private int _jitInlinePolicyReplay;
    private unsafe byte* _jitNoInlineRange;
    private unsafe byte* _jitInlineReplayFile;
#endif
    private int _jitExtDefaultPolicy;
    private int _jitExtDefaultPolicyMaxIL;
    private int _jitExtDefaultPolicyMaxILRoot;
    private int _jitExtDefaultPolicyMaxILProf;
    private int _jitExtDefaultPolicyMaxBB;
    private int _jitExtDefaultPolicyProfTrust;
    private int _jitExtDefaultPolicyProfScale;
    private int _jitInlinePolicyModel;
    private int _jitInlinePolicyProfile;
    private int _jitInlinePolicyProfileThreshold;
#if DEBUG
    private unsafe byte* _jitObjectStackAllocationRange;
#endif
    private int _jitObjectStackAllocation;
    private int _jitObjectStackAllocationRefClass;
    private int _jitObjectStackAllocationBoxedValueClass;
    private int _jitObjectStackAllocationConditionalEscape;
#if DEBUG
    private unsafe byte* _jitObjectStackAllocationConditionalEscapeRange;
#endif
    private int _jitObjectStackAllocationArray;
    private int _jitObjectStackAllocationSize;
    private int _jitObjectStackAllocationTrackFields;
#if DEBUG
    private unsafe byte* _jitObjectStackAllocationTrackFieldsRange;
    private int _jitObjectStackAllocationDumpConnGraph;
#endif
    private int _jitEECallTimingInfo;
#if DEBUG
    private int _jitEnableFinallyCloning;
    private int _jitEnableRemoveEmptyTry;
    private int _jitEnableRemoveEmptyTryCatchOrTryFault;
#endif
    private int _jitEnableGuardedDevirtualization;
    private int _jitGuardedDevirtualizationMaxTypeChecks;
    private int _jitGuardedDevirtualizationChainLikelihood;
    private int _jitGuardedDevirtualizationChainStatements;
#if DEBUG
    private unsafe byte* _jitGuardedDevirtualizationRange;
    private int _jitRandomGuardedDevirtualization;
#endif
#if FEATURE_ON_STACK_REPLACEMENT
    private int _tC_OnStackReplacement;
#else
    private int _tC_OnStackReplacement;
#endif
    private int _tC_OnStackReplacement_InitialCounter;
    private int _tC_PartialCompilation;
#if DEBUG
    private int _jitRandomPartialCompilation;
#endif
    private int _tC_PatchpointStrategy;
#if DEBUG
    private int _jitRandomOnStackReplacement;
    private int _jitOffsetOnStackReplacement;
    private unsafe byte* _jitEnableOsrRange;
    private unsafe byte* _jitEnablePatchpointRange;
#endif
    private int _jitInterlockedProfiling;
    private int _jitScalableProfiling;
    private int _jitCounterPadding;
    private int _jitMinimalJitProfiling;
    private int _jitMinimalPrejitProfiling;
    private int _jitProfileValues;
    private int _jitProfileCasts;
    private int _jitConsumeProfileForCasts;
    private int _jitClassProfiling;
    private int _jitDelegateProfiling;
    private int _jitVTableProfiling;
    private int _jitEdgeProfiling;
    private int _jitCollect64BitCounts;
#if DEBUG
    private int _jitInstrumentIfOptimizing;
    private unsafe byte* _jitInstrumentIfOptimizingRange;
#endif
    private int _jitInstrumentInlinees;
    private int _jitDisablePGO;
#if DEBUG
    private unsafe byte* _jitEnablePGORange;
    private int _jitRandomEdgeCounts;
    private int _jitCrossCheckDevirtualizationAndPGO;
    private int _jitNoteFailedExactDevirtualization;
    private int _jitRandomlyCollect64BitCounts;
    private int _jitSynthesizeCounts;
    private int _jitPropagateSynthesizedCountsToProfileData;
    private int _jitSynthesisUseSolver;
    private unsafe byte* _jitSynthesisExceptionWeight;
#endif
    private int _jitEnableExactDevirtualization;
    private int _jitForceControlFlowGuard;
    private int _jitCFGUseDispatcher;
    private int _jitEnableHeadTailMerge;
    private int _jitEnablePhysicalPromotion;
    private int _jitEnableCrossBlockLocalAssertionProp;
    private int _jitEnablePostorderLocalAssertionProp;
    private int _jitEnableStrengthReduction;
    private int _jitEnableInductionVariableOpts;
#if DEBUG
    private unsafe byte* _jitFunctionFile;
    private MethodSet _jitRawHexCode;
    private unsafe byte* _jitRawHexCodeFile;
#if TARGET_ARM64
    private int _jitSaveFpLrWithCalleeSavedRegisters;
    private int _jitUseScalableVectorT;
#endif
#if TARGET_LOONGARCH64
    private int _jitDispIns;
#endif
#endif
#if TARGET_WASM
    private int _jitWasmNyiToR2RUnsupported;
#if DEBUG
    private unsafe byte* _jitR2RUnsupportedRange;
#endif
    private int _jitWasmFunclets;
#endif
    private int _jitEnregStructLocals;

#if DEBUG
    public int AltJitLimit => _altJitLimit;
    public int AltJitSkipOnAssert => _altJitSkipOnAssert;
    public int BreakOnDumpToken => _breakOnDumpToken;
    public int DebugBreakOnVerificationFailure => _debugBreakOnVerificationFailure;
    public int DisplayLoopHoistStats => _displayLoopHoistStats;
    public int DisplayLsraStats => _displayLsraStats;
    public unsafe byte* JitLsraOrdering => _jitLsraOrdering;
    public int EnablePCRelAddr => _enablePCRelAddr;
    public int JitAssertOnMaxRAPasses => _jitAssertOnMaxRAPasses;
    public int JitBreakEmitOutputInstr => _jitBreakEmitOutputInstr;
    public int JitBreakMorphTree => _jitBreakMorphTree;
    public int JitBreakOnBadCode => _jitBreakOnBadCode;
    public int JitBreakOnMinOpts => _jitBreakOnMinOpts;
    public int JitCloneLoops => _jitCloneLoops;
    public int JitCloneLoopsWithEH => _jitCloneLoopsWithEH;
    public int JitCloneLoopsWithGdvTests => _jitCloneLoopsWithGdvTests;
#endif
    public int JitCloneLoopsSizeLimit => _jitCloneLoopsSizeLimit;
    public int JitCloneLoopsMinPerCallRatio => _jitCloneLoopsMinPerCallRatio;
#if DEBUG
    public int JitDebugLogLoopCloning => _jitDebugLogLoopCloning;
    public int JitDefaultFill => _jitDefaultFill;
    public int JitAlignLoopMinBlockWeight => _jitAlignLoopMinBlockWeight;
    public int JitAlignLoopMaxCodeSize => _jitAlignLoopMaxCodeSize;
    public int JitAlignLoopBoundary => _jitAlignLoopBoundary;
    public int JitAlignLoopForJcc => _jitAlignLoopForJcc;
    public int JitAlignLoopAdaptive => _jitAlignLoopAdaptive;
    public int JitHideAlignBehindJmp => _jitHideAlignBehindJmp;
    public int JitOptimizeStructHiddenBuffer => _jitOptimizeStructHiddenBuffer;
#endif
    public int JitEnableStoreLclFldCoalescing => _jitEnableStoreLclFldCoalescing;
#if DEBUG
    public int JitUnrollLoopMaxIterationCount => _jitUnrollLoopMaxIterationCount;
    public int JitUnrollLoopsWithEH => _jitUnrollLoopsWithEH;
    public int JitDirectAlloc => _jitDirectAlloc;
    public int JitDoubleAlign => _jitDoubleAlign;
    public int JitEmitPrintRefRegs => _jitEmitPrintRefRegs;
    public int JitEnableDevirtualization => _jitEnableDevirtualization;
    public int JitEnableLateDevirtualization => _jitEnableLateDevirtualization;
    public int JitExpensiveDebugCheckLevel => _jitExpensiveDebugCheckLevel;
    public int JitForceFallback => _jitForceFallback;
    public int JitFullyInt => _jitFullyInt;
    public int JitFunctionTrace => _jitFunctionTrace;
    public int JitGCChecks => _jitGCChecks;
    public int JitGCInfoLogging => _jitGCInfoLogging;
    public int JitHashBreak => _jitHashBreak;
    public int JitHashHalt => _jitHashHalt;
    public int JitInlineAdditionalMultiplier => _jitInlineAdditionalMultiplier;
    public int JitInlinePrintStats => _jitInlinePrintStats;
    public int JitInlineSize => _jitInlineSize;
    public int JitInlineDepth => _jitInlineDepth;
#endif
    public int JitInlineBudget => _jitInlineBudget;
#if DEBUG
    public int JitForceInlineDepth => _jitForceInlineDepth;
#endif
    public int JitInlineMethodsWithEH => _jitInlineMethodsWithEH;
#if DEBUG
    public unsafe byte* JitInlineMethodsWithEHRange => _jitInlineMethodsWithEHRange;
    public int JitLongAddress => _jitLongAddress;
    public int JitMaxUncheckedOffset => _jitMaxUncheckedOffset;
#endif
#if TARGET_ARM64
    public int JitPacEnabled => _jitPacEnabled;
#endif
    public int JitEnableGenericVirtualDevirtualization => _jitEnableGenericVirtualDevirtualization;
#if DEBUG
    public int JitMinOpts => _jitMinOpts;
    public MethodSet JitMinOptsName => _jitMinOptsName;
    public int JitMinOptsBbCount => _jitMinOptsBbCount;
    public int JitMinOptsCodeSize => _jitMinOptsCodeSize;
    public int JitMinOptsInstrCount => _jitMinOptsInstrCount;
    public int JitMinOptsLvNumCount => _jitMinOptsLvNumCount;
    public int JitMinOptsLvRefCount => _jitMinOptsLvRefCount;
    public int JitNoCSE => _jitNoCSE;
    public int JitNoCSE2 => _jitNoCSE2;
    public int JitNoForceFallback => _jitNoForceFallback;
    public int JitNoForwardSub => _jitNoForwardSub;
    public int JitNoHoist => _jitNoHoist;
    public int JitNoMemoryBarriers => _jitNoMemoryBarriers;
    public int JitNoStructPromotion => _jitNoStructPromotion;
    public int JitNoUnroll => _jitNoUnroll;
    public int JitOrder => _jitOrder;
    public int JitQueryCurrentStaticFieldClass => _jitQueryCurrentStaticFieldClass;
    public int JitReportFastTailCallDecisions => _jitReportFastTailCallDecisions;
    public int JitPInvokeCheckEnabled => _jitPInvokeCheckEnabled;
    public int JitPInvokeEnabled => _jitPInvokeEnabled;
    public int JitHoistLimit => _jitHoistLimit;
    public int JitPrintInlinedMethodsVerbose => _jitPrintInlinedMethodsVerbose;
    public MethodSet JitPrintInlinedMethods => _jitPrintInlinedMethods;
    public MethodSet JitPrintDevirtualizedMethods => _jitPrintDevirtualizedMethods;
    public int JitProfileChecks => _jitProfileChecks;
    public int JitRequired => _jitRequired;
    public int JitStackAllocToLocalSize => _jitStackAllocToLocalSize;
    public int JitSkipArrayBoundCheck => _jitSkipArrayBoundCheck;
    public int JitSlowDebugChecksEnabled => _jitSlowDebugChecksEnabled;
    public int JitSplitFunctionSize => _jitSplitFunctionSize;
    public int JitSsaStress => _jitSsaStress;
    public int JitStackChecks => _jitStackChecks;
    public int JitStress => _jitStress;
    public int JitStressBBProf => _jitStressBBProf;
    public int JitStressProcedureSplitting => _jitStressProcedureSplitting;
    public int JitStressRegs => _jitStressRegs;
    public unsafe byte* JitStressRegsRange => _jitStressRegsRange;
    public int JitStressSplitTreeLimit => _jitStressSplitTreeLimit;
    public int JitVNMapSelLimit => _jitVNMapSelLimit;
    public int RunAltJitCode => _runAltJitCode;
    public int RunComponentUnitTests => _runComponentUnitTests;
    public int ShouldInjectFault => _shouldInjectFault;
    public int TailcallStress => _tailcallStress;
    public MethodSet JitBreak => _jitBreak;
    public MethodSet JitDebugBreak => _jitDebugBreak;
    public MethodSet JitDump => _jitDump;
    public int JitHashDump => _jitHashDump;
    public int JitDumpTier0 => _jitDumpTier0;
    public int JitDumpOSR => _jitDumpOSR;
    public int JitDumpAtOSROffset => _jitDumpAtOSROffset;
    public int JitDumpInlinePhases => _jitDumpInlinePhases;
    public int JitDumpASCII => _jitDumpASCII;
    public int JitDumpTerseLsra => _jitDumpTerseLsra;
    public int JitDumpToDebugger => _jitDumpToDebugger;
    public int JitDumpVerboseSsa => _jitDumpVerboseSsa;
    public int JitDumpVerboseTrees => _jitDumpVerboseTrees;
    public int JitDumpTreeIDs => _jitDumpTreeIDs;
    public int JitDumpBeforeAfterMorph => _jitDumpBeforeAfterMorph;
    public int JitDumpTerseNextBlock => _jitDumpTerseNextBlock;
    public MethodSet JitEHDump => _jitEHDump;
    public MethodSet JitExclude => _jitExclude;
    public int JitFakeProcedureSplitting => _jitFakeProcedureSplitting;
    public MethodSet JitForceProcedureSplitting => _jitForceProcedureSplitting;
    public MethodSet JitGCDump => _jitGCDump;
    public MethodSet JitDebugDump => _jitDebugDump;
    public MethodSet JitHalt => _jitHalt;
    public MethodSet JitInclude => _jitInclude;
    public MethodSet JitLateDisasm => _jitLateDisasm;
    public unsafe byte* JitLateDisasmTo => _jitLateDisasmTo;
    public MethodSet JitNoProcedureSplitting => _jitNoProcedureSplitting;
    public MethodSet JitNoProcedureSplittingEH => _jitNoProcedureSplittingEH;
    public MethodSet JitStressOnly => _jitStressOnly;
    public MethodSet JitUnwindDump => _jitUnwindDump;
    public MethodSet JitDumpFg => _jitDumpFg;
    public int JitDumpFgHash => _jitDumpFgHash;
    public int JitDumpFgTier0 => _jitDumpFgTier0;
    public unsafe byte* JitDumpFgDir => _jitDumpFgDir;
    public unsafe byte* JitDumpFgFile => _jitDumpFgFile;
    public unsafe byte* JitDumpFgPhase => _jitDumpFgPhase;
    public unsafe byte* JitDumpFgPrePhase => _jitDumpFgPrePhase;
    public int JitDumpFgDot => _jitDumpFgDot;
    public int JitDumpFgEH => _jitDumpFgEH;
    public int JitDumpFgLoops => _jitDumpFgLoops;
    public int JitDumpFgConstrained => _jitDumpFgConstrained;
    public int JitDumpFgBlockID => _jitDumpFgBlockID;
    public int JitDumpFgBlockFlags => _jitDumpFgBlockFlags;
    public int JitDumpFgLoopFlags => _jitDumpFgLoopFlags;
    public int JitDumpFgBlockOrder => _jitDumpFgBlockOrder;
    public int JitDumpFgMemorySsa => _jitDumpFgMemorySsa;
    public unsafe byte* JitRange => _jitRange;
    public unsafe byte* JitStressModeNames => _jitStressModeNames;
    public int JitStressModeNamesOnly => _jitStressModeNamesOnly;
    public unsafe byte* JitStressModeNamesAllow => _jitStressModeNamesAllow;
    public unsafe byte* JitStressModeNamesNot => _jitStressModeNamesNot;
    public unsafe byte* JitStressRange => _jitStressRange;
    public MethodSet JitEmitUnitTests => _jitEmitUnitTests;
    public unsafe byte* JitEmitUnitTestsSections => _jitEmitUnitTestsSections;
#endif
    public MethodSet JitDisasm => _jitDisasm;
    public int JitDisasmTesting => _jitDisasmTesting;
    public int JitDisasmDiffable => _jitDisasmDiffable;
    public int JitDisasmSummary => _jitDisasmSummary;
    public int JitDisasmOnlyOptimized => _jitDisasmOnlyOptimized;
    public int JitDisasmWithAlignmentBoundaries => _jitDisasmWithAlignmentBoundaries;
    public int JitDisasmWithCodeBytes => _jitDisasmWithCodeBytes;
#if DEBUG
    public unsafe byte* JitDisasmAssemblies => _jitDisasmAssemblies;
    public int JitDisasmWithGC => _jitDisasmWithGC;
    public int JitDisasmWithDebugInfo => _jitDisasmWithDebugInfo;
    public int JitDisasmSpilled => _jitDisasmSpilled;
    public int JitDisasmWithAddress => _jitDisasmWithAddress;
#endif
    public unsafe byte* JitStdOutFile => _jitStdOutFile;
    public int RichDebugInfo => _richDebugInfo;
#if DEBUG
    public unsafe byte* WriteRichDebugInfoFile => _writeRichDebugInfoFile;
#endif
#if FEATURE_LOOP_ALIGN
    public int JitAlignLoops => _jitAlignLoops;
#else
    public int JitAlignLoops => _jitAlignLoops;
#endif
    public int AltJitAssertOnNYI => _altJitAssertOnNYI;
    public int EnableEHWriteThru => _enableEHWriteThru;
    public int EnableMultiRegLocals => _enableMultiRegLocals;
    public int JitNoInline => _jitNoInline;
#if DEBUG
#if DEBUG
    public int JitStressRex2Encoding => _jitStressRex2Encoding;
    public int JitStressPromotedEvexEncoding => _jitStressPromotedEvexEncoding;
#endif
#if TARGET_AMD64 || TARGET_X86
    public int JitStressEvexEncoding => _jitStressEvexEncoding;
#endif
#endif
#if TARGET_LOONGARCH64
    public int EnableHWIntrinsic => _enableHWIntrinsic;
#else
    public int EnableHWIntrinsic => _enableHWIntrinsic;
#endif
#if TARGET_AMD64 || TARGET_X86
    public int EnableAVX => _enableAVX;
    public int EnableAVX2 => _enableAVX2;
    public int EnableAVX512 => _enableAVX512;
    public int EnableAVX512BMM => _enableAVX512BMM;
    public int EnableAVX512v2 => _enableAVX512v2;
    public int EnableAVX512v3 => _enableAVX512v3;
    public int EnableAVX10v1 => _enableAVX10v1;
    public int EnableAVX10v2 => _enableAVX10v2;
    public int EnableAPX => _enableAPX;
    public int EnableAES => _enableAES;
    public int EnableAVX512VP2INTERSECT => _enableAVX512VP2INTERSECT;
    public int EnableAVXIFMA => _enableAVXIFMA;
    public int EnableAVXVNNI => _enableAVXVNNI;
    public int EnableAVXVNNIINT => _enableAVXVNNIINT;
    public int EnableGFNI => _enableGFNI;
    public int EnableSHA => _enableSHA;
    public int EnableVAES => _enableVAES;
    public int EnableWAITPKG => _enableWAITPKG;
    public int EnableX86Serialize => _enableX86Serialize;
#elif TARGET_ARM64
    public int EnableArm64Aes => _enableArm64Aes;
    public int EnableArm64Atomics => _enableArm64Atomics;
    public int EnableArm64Crc32 => _enableArm64Crc32;
    public int EnableArm64Dczva => _enableArm64Dczva;
    public int EnableArm64Dp => _enableArm64Dp;
    public int EnableArm64Rdm => _enableArm64Rdm;
    public int EnableArm64Sha1 => _enableArm64Sha1;
    public int EnableArm64Sha256 => _enableArm64Sha256;
    public int EnableArm64Sve => _enableArm64Sve;
    public int EnableArm64Sve2 => _enableArm64Sve2;
    public int EnableArm64Sha3 => _enableArm64Sha3;
    public int EnableArm64Sm4 => _enableArm64Sm4;
    public int EnableArm64SveAes => _enableArm64SveAes;
    public int EnableArm64SveSha3 => _enableArm64SveSha3;
    public int EnableArm64SveSm4 => _enableArm64SveSm4;
#elif TARGET_RISCV64
    public int EnableRiscV64Zba => _enableRiscV64Zba;
    public int EnableRiscV64Zbb => _enableRiscV64Zbb;
    public int EnableRiscV64Zbs => _enableRiscV64Zbs;
#endif
    public int EnableEmbeddedBroadcast => _enableEmbeddedBroadcast;
    public int EnableEmbeddedMasking => _enableEmbeddedMasking;
    public int EnableApxNDD => _enableApxNDD;
    public int EnableApxConditionalChaining => _enableApxConditionalChaining;
    public int EnableApxPPHint => _enableApxPPHint;
    public int EnableApxPP2 => _enableApxPP2;
    public int EnableApxZU => _enableApxZU;
#if FEATURE_SIMD
    public int JitDisableSimdVN => _jitDisableSimdVN;
#endif
    public int JitConstCSE => _jitConstCSE;
    public int JitRLCSEGreedy => _jitRLCSEGreedy;
    public int JitRLCSEVerbose => _jitRLCSEVerbose;
#if DEBUG
    public int JitCSEHash => _jitCSEHash;
    public int JitCSEMask => _jitCSEMask;
    public int JitMetrics => _jitMetrics;
    public int JitRandomCSE => _jitRandomCSE;
    public unsafe byte* JitReplayCSE => _jitReplayCSE;
    public unsafe byte* JitReplayCSEReward => _jitReplayCSEReward;
    public unsafe byte* JitRLCSE => _jitRLCSE;
    public unsafe byte* JitRLCSEAlpha => _jitRLCSEAlpha;
    public int JitRLCSECandidateFeatures => _jitRLCSECandidateFeatures;
    public int JitRLHook => _jitRLHook;
    public int JitRLHookEmitFeatureNames => _jitRLHookEmitFeatureNames;
    public unsafe byte* JitRLHookCSEDecisions => _jitRLHookCSEDecisions;
#endif
#if !DEBUG && !_DEBUG
    public int JitEnableNoWayAssert => _jitEnableNoWayAssert;
#else
    public int JitEnableNoWayAssert => _jitEnableNoWayAssert;
#endif
    public int DisplayMemStats => _displayMemStats;
#if DEBUG
    public int JitEnregStats => _jitEnregStats;
#endif
    public int JitAggressiveInlining => _jitAggressiveInlining;
    public int JitELTHookEnabled => _jitELTHookEnabled;
    public int JitInlineSIMDMultiplier => _jitInlineSIMDMultiplier;
    public int JitMaxLocalsToTrack => _jitMaxLocalsToTrack;
#if FEATURE_ENABLE_NO_RANGE_CHECKS
    public int JitNoRngChks => _jitNoRngChks;
#endif
#if OPT_CONFIG
    public int JitDoAssertionProp => _jitDoAssertionProp;
    public int JitDoCopyProp => _jitDoCopyProp;
    public int JitDoOptimizeIVs => _jitDoOptimizeIVs;
    public int JitDoEarlyProp => _jitDoEarlyProp;
    public int JitDoLoopHoisting => _jitDoLoopHoisting;
    public int JitDoLoopInversion => _jitDoLoopInversion;
#endif
    public int JitLoopInversionSizeLimit => _jitLoopInversionSizeLimit;
#if OPT_CONFIG
    public int JitDoRangeAnalysis => _jitDoRangeAnalysis;
    public int JitDoVNBasedDeadStoreRemoval => _jitDoVNBasedDeadStoreRemoval;
    public int JitDoRedundantBranchOpts => _jitDoRedundantBranchOpts;
    public unsafe byte* JitEnableRboRange => _jitEnableRboRange;
    public unsafe byte* JitEnableHeadTailMergeRange => _jitEnableHeadTailMergeRange;
    public unsafe byte* JitEnableVNBasedDeadStoreRemovalRange => _jitEnableVNBasedDeadStoreRemovalRange;
    public unsafe byte* JitEnableEarlyLivenessRange => _jitEnableEarlyLivenessRange;
    public unsafe byte* JitOnlyOptimizeRange => _jitOnlyOptimizeRange;
    public unsafe byte* JitEnablePhysicalPromotionRange => _jitEnablePhysicalPromotionRange;
    public unsafe byte* JitEnableCrossBlockLocalAssertionPropRange => _jitEnableCrossBlockLocalAssertionPropRange;
    public unsafe byte* JitEnableInductionVariableOptsRange => _jitEnableInductionVariableOptsRange;
    public unsafe byte* JitEnableLocalAddrPropagationRange => _jitEnableLocalAddrPropagationRange;
    public int JitDoSsa => _jitDoSsa;
    public int JitDoValueNumber => _jitDoValueNumber;
    public unsafe byte* JitOptRepeatRange => _jitOptRepeatRange;
    public int JitDoIfConversion => _jitDoIfConversion;
    public int JitDoOptimizeMaskConversions => _jitDoOptimizeMaskConversions;
    public int JitOptimizeAwait => _jitOptimizeAwait;
    public unsafe byte* JitAsyncDefaultValueAnalysisRange => _jitAsyncDefaultValueAnalysisRange;
    public unsafe byte* JitAsyncPreservedValueAnalysisRange => _jitAsyncPreservedValueAnalysisRange;
    public unsafe byte* JitAsyncReuseContinuationsRange => _jitAsyncReuseContinuationsRange;
#endif
    public int JitAsyncReuseContinuations => _jitAsyncReuseContinuations;
    public int JitEnableOptRepeat => _jitEnableOptRepeat;
    public MethodSet JitOptRepeat => _jitOptRepeat;
    public int JitOptRepeatCount => _jitOptRepeatCount;
    public int JitVNMapSelBudget => _jitVNMapSelBudget;
    public int TailCallLoopOpt => _tailCallLoopOpt;
    public MethodSet AltJit => _altJit;
    public MethodSet AltJitNgen => _altJitNgen;
    public unsafe byte* AltJitExcludeAssemblies => _altJitExcludeAssemblies;
    public int JitMeasureIR => _jitMeasureIR;
    public int JitReportMetrics => _jitReportMetrics;
    public unsafe byte* JitFuncInfoFile => _jitFuncInfoFile;
    public unsafe byte* JitTimeLogCsv => _jitTimeLogCsv;
    public unsafe byte* JitTimeLogFile => _jitTimeLogFile;
    public unsafe byte* TailCallOpt => _tailCallOpt;
    public int FastTailCalls => _fastTailCalls;
    public int JitMeasureNowayAssert => _jitMeasureNowayAssert;
    public unsafe byte* JitMeasureNowayAssertFile => _jitMeasureNowayAssertFile;
#if DEBUG
    public int EnableExtraSuperPmiQueries => _enableExtraSuperPmiQueries;
    public int JitInlineDumpData => _jitInlineDumpData;
    public int JitInlineDumpXml => _jitInlineDumpXml;
    public unsafe byte* JitInlineDumpXmlFile => _jitInlineDumpXmlFile;
    public int JitInlinePolicyDumpXml => _jitInlinePolicyDumpXml;
    public int JitInlineLimit => _jitInlineLimit;
    public int JitInlinePolicyDiscretionary => _jitInlinePolicyDiscretionary;
    public int JitInlinePolicyFull => _jitInlinePolicyFull;
    public int JitInlinePolicySize => _jitInlinePolicySize;
    public int JitInlinePolicyRandom => _jitInlinePolicyRandom;
    public int JitInlinePolicyReplay => _jitInlinePolicyReplay;
    public unsafe byte* JitNoInlineRange => _jitNoInlineRange;
    public unsafe byte* JitInlineReplayFile => _jitInlineReplayFile;
#endif
    public int JitExtDefaultPolicy => _jitExtDefaultPolicy;
    public int JitExtDefaultPolicyMaxIL => _jitExtDefaultPolicyMaxIL;
    public int JitExtDefaultPolicyMaxILRoot => _jitExtDefaultPolicyMaxILRoot;
    public int JitExtDefaultPolicyMaxILProf => _jitExtDefaultPolicyMaxILProf;
    public int JitExtDefaultPolicyMaxBB => _jitExtDefaultPolicyMaxBB;
    public int JitExtDefaultPolicyProfTrust => _jitExtDefaultPolicyProfTrust;
    public int JitExtDefaultPolicyProfScale => _jitExtDefaultPolicyProfScale;
    public int JitInlinePolicyModel => _jitInlinePolicyModel;
    public int JitInlinePolicyProfile => _jitInlinePolicyProfile;
    public int JitInlinePolicyProfileThreshold => _jitInlinePolicyProfileThreshold;
#if DEBUG
    public unsafe byte* JitObjectStackAllocationRange => _jitObjectStackAllocationRange;
#endif
    public int JitObjectStackAllocation => _jitObjectStackAllocation;
    public int JitObjectStackAllocationRefClass => _jitObjectStackAllocationRefClass;
    public int JitObjectStackAllocationBoxedValueClass => _jitObjectStackAllocationBoxedValueClass;
    public int JitObjectStackAllocationConditionalEscape => _jitObjectStackAllocationConditionalEscape;
#if DEBUG
    public unsafe byte* JitObjectStackAllocationConditionalEscapeRange => _jitObjectStackAllocationConditionalEscapeRange;
#endif
    public int JitObjectStackAllocationArray => _jitObjectStackAllocationArray;
    public int JitObjectStackAllocationSize => _jitObjectStackAllocationSize;
    public int JitObjectStackAllocationTrackFields => _jitObjectStackAllocationTrackFields;
#if DEBUG
    public unsafe byte* JitObjectStackAllocationTrackFieldsRange => _jitObjectStackAllocationTrackFieldsRange;
    public int JitObjectStackAllocationDumpConnGraph => _jitObjectStackAllocationDumpConnGraph;
#endif
    public int JitEECallTimingInfo => _jitEECallTimingInfo;
#if DEBUG
    public int JitEnableFinallyCloning => _jitEnableFinallyCloning;
    public int JitEnableRemoveEmptyTry => _jitEnableRemoveEmptyTry;
    public int JitEnableRemoveEmptyTryCatchOrTryFault => _jitEnableRemoveEmptyTryCatchOrTryFault;
#endif
    public int JitEnableGuardedDevirtualization => _jitEnableGuardedDevirtualization;
    public int JitGuardedDevirtualizationMaxTypeChecks => _jitGuardedDevirtualizationMaxTypeChecks;
    public int JitGuardedDevirtualizationChainLikelihood => _jitGuardedDevirtualizationChainLikelihood;
    public int JitGuardedDevirtualizationChainStatements => _jitGuardedDevirtualizationChainStatements;
#if DEBUG
    public unsafe byte* JitGuardedDevirtualizationRange => _jitGuardedDevirtualizationRange;
    public int JitRandomGuardedDevirtualization => _jitRandomGuardedDevirtualization;
#endif
#if FEATURE_ON_STACK_REPLACEMENT
    public int TC_OnStackReplacement => _tC_OnStackReplacement;
#else
    public int TC_OnStackReplacement => _tC_OnStackReplacement;
#endif
    public int TC_OnStackReplacement_InitialCounter => _tC_OnStackReplacement_InitialCounter;
    public int TC_PartialCompilation => _tC_PartialCompilation;
#if DEBUG
    public int JitRandomPartialCompilation => _jitRandomPartialCompilation;
#endif
    public int TC_PatchpointStrategy => _tC_PatchpointStrategy;
#if DEBUG
    public int JitRandomOnStackReplacement => _jitRandomOnStackReplacement;
    public int JitOffsetOnStackReplacement => _jitOffsetOnStackReplacement;
    public unsafe byte* JitEnableOsrRange => _jitEnableOsrRange;
    public unsafe byte* JitEnablePatchpointRange => _jitEnablePatchpointRange;
#endif
    public int JitInterlockedProfiling => _jitInterlockedProfiling;
    public int JitScalableProfiling => _jitScalableProfiling;
    public int JitCounterPadding => _jitCounterPadding;
    public int JitMinimalJitProfiling => _jitMinimalJitProfiling;
    public int JitMinimalPrejitProfiling => _jitMinimalPrejitProfiling;
    public int JitProfileValues => _jitProfileValues;
    public int JitProfileCasts => _jitProfileCasts;
    public int JitConsumeProfileForCasts => _jitConsumeProfileForCasts;
    public int JitClassProfiling => _jitClassProfiling;
    public int JitDelegateProfiling => _jitDelegateProfiling;
    public int JitVTableProfiling => _jitVTableProfiling;
    public int JitEdgeProfiling => _jitEdgeProfiling;
    public int JitCollect64BitCounts => _jitCollect64BitCounts;
#if DEBUG
    public int JitInstrumentIfOptimizing => _jitInstrumentIfOptimizing;
    public unsafe byte* JitInstrumentIfOptimizingRange => _jitInstrumentIfOptimizingRange;
#endif
    public int JitInstrumentInlinees => _jitInstrumentInlinees;
    public int JitDisablePGO => _jitDisablePGO;
#if DEBUG
    public unsafe byte* JitEnablePGORange => _jitEnablePGORange;
    public int JitRandomEdgeCounts => _jitRandomEdgeCounts;
    public int JitCrossCheckDevirtualizationAndPGO => _jitCrossCheckDevirtualizationAndPGO;
    public int JitNoteFailedExactDevirtualization => _jitNoteFailedExactDevirtualization;
    public int JitRandomlyCollect64BitCounts => _jitRandomlyCollect64BitCounts;
    public int JitSynthesizeCounts => _jitSynthesizeCounts;
    public int JitPropagateSynthesizedCountsToProfileData => _jitPropagateSynthesizedCountsToProfileData;
    public int JitSynthesisUseSolver => _jitSynthesisUseSolver;
    public unsafe byte* JitSynthesisExceptionWeight => _jitSynthesisExceptionWeight;
#endif
    public int JitEnableExactDevirtualization => _jitEnableExactDevirtualization;
    public int JitForceControlFlowGuard => _jitForceControlFlowGuard;
    public int JitCFGUseDispatcher => _jitCFGUseDispatcher;
    public int JitEnableHeadTailMerge => _jitEnableHeadTailMerge;
    public int JitEnablePhysicalPromotion => _jitEnablePhysicalPromotion;
    public int JitEnableCrossBlockLocalAssertionProp => _jitEnableCrossBlockLocalAssertionProp;
    public int JitEnablePostorderLocalAssertionProp => _jitEnablePostorderLocalAssertionProp;
    public int JitEnableStrengthReduction => _jitEnableStrengthReduction;
    public int JitEnableInductionVariableOpts => _jitEnableInductionVariableOpts;
#if DEBUG
    public unsafe byte* JitFunctionFile => _jitFunctionFile;
    public MethodSet JitRawHexCode => _jitRawHexCode;
    public unsafe byte* JitRawHexCodeFile => _jitRawHexCodeFile;
#if TARGET_ARM64
    public int JitSaveFpLrWithCalleeSavedRegisters => _jitSaveFpLrWithCalleeSavedRegisters;
    public int JitUseScalableVectorT => _jitUseScalableVectorT;
#endif
#if TARGET_LOONGARCH64
    public int JitDispIns => _jitDispIns;
#endif
#endif
#if TARGET_WASM
    public int JitWasmNyiToR2RUnsupported => _jitWasmNyiToR2RUnsupported;
#if DEBUG
    public unsafe byte* JitR2RUnsupportedRange => _jitR2RUnsupportedRange;
#endif
    public int JitWasmFunclets => _jitWasmFunclets;
#endif
    public int JitEnregStructLocals => _jitEnregStructLocals;

    public unsafe void destroy(ICorJitHost* jitHost)
    {
        if (!_isInitialized)
        {
            return;
        }

#if DEBUG
        // _altJitLimit = unchecked((int)(0xCDCDCDCD));
        // _altJitSkipOnAssert = unchecked((int)(0xCDCDCDCD));
        // _breakOnDumpToken = unchecked((int)(0xCDCDCDCD));
        // _debugBreakOnVerificationFailure = unchecked((int)(0xCDCDCDCD));
        // _displayLoopHoistStats = unchecked((int)(0xCDCDCDCD));
        // _displayLsraStats = unchecked((int)(0xCDCDCDCD));
        jitHost->freeStringConfigValue(_jitLsraOrdering);
        // _enablePCRelAddr = unchecked((int)(0xCDCDCDCD));
        // _jitAssertOnMaxRAPasses = unchecked((int)(0xCDCDCDCD));
        // _jitBreakEmitOutputInstr = unchecked((int)(0xCDCDCDCD));
        // _jitBreakMorphTree = unchecked((int)(0xCDCDCDCD));
        // _jitBreakOnBadCode = unchecked((int)(0xCDCDCDCD));
        // _jitBreakOnMinOpts = unchecked((int)(0xCDCDCDCD));
        // _jitCloneLoops = unchecked((int)(0xCDCDCDCD));
        // _jitCloneLoopsWithEH = unchecked((int)(0xCDCDCDCD));
        // _jitCloneLoopsWithGdvTests = unchecked((int)(0xCDCDCDCD));
#endif
        // _jitCloneLoopsSizeLimit = unchecked((int)(0xCDCDCDCD));
        // _jitCloneLoopsMinPerCallRatio = unchecked((int)(0xCDCDCDCD));
#if DEBUG
        // _jitDebugLogLoopCloning = unchecked((int)(0xCDCDCDCD));
        // _jitDefaultFill = unchecked((int)(0xCDCDCDCD));
        // _jitAlignLoopMinBlockWeight = unchecked((int)(0xCDCDCDCD));
        // _jitAlignLoopMaxCodeSize = unchecked((int)(0xCDCDCDCD));
        // _jitAlignLoopBoundary = unchecked((int)(0xCDCDCDCD));
        // _jitAlignLoopForJcc = unchecked((int)(0xCDCDCDCD));
        // _jitAlignLoopAdaptive = unchecked((int)(0xCDCDCDCD));
        // _jitHideAlignBehindJmp = unchecked((int)(0xCDCDCDCD));
        // _jitOptimizeStructHiddenBuffer = unchecked((int)(0xCDCDCDCD));
#endif
        // _jitEnableStoreLclFldCoalescing = unchecked((int)(0xCDCDCDCD));
#if DEBUG
        // _jitUnrollLoopMaxIterationCount = unchecked((int)(0xCDCDCDCD));
        // _jitUnrollLoopsWithEH = unchecked((int)(0xCDCDCDCD));
        // _jitDirectAlloc = unchecked((int)(0xCDCDCDCD));
        // _jitDoubleAlign = unchecked((int)(0xCDCDCDCD));
        // _jitEmitPrintRefRegs = unchecked((int)(0xCDCDCDCD));
        // _jitEnableDevirtualization = unchecked((int)(0xCDCDCDCD));
        // _jitEnableLateDevirtualization = unchecked((int)(0xCDCDCDCD));
        // _jitExpensiveDebugCheckLevel = unchecked((int)(0xCDCDCDCD));
        // _jitForceFallback = unchecked((int)(0xCDCDCDCD));
        // _jitFullyInt = unchecked((int)(0xCDCDCDCD));
        // _jitFunctionTrace = unchecked((int)(0xCDCDCDCD));
        // _jitGCChecks = unchecked((int)(0xCDCDCDCD));
        // _jitGCInfoLogging = unchecked((int)(0xCDCDCDCD));
        // _jitHashBreak = unchecked((int)(0xCDCDCDCD));
        // _jitHashHalt = unchecked((int)(0xCDCDCDCD));
        // _jitInlineAdditionalMultiplier = unchecked((int)(0xCDCDCDCD));
        // _jitInlinePrintStats = unchecked((int)(0xCDCDCDCD));
        // _jitInlineSize = unchecked((int)(0xCDCDCDCD));
        // _jitInlineDepth = unchecked((int)(0xCDCDCDCD));
#endif
        // _jitInlineBudget = unchecked((int)(0xCDCDCDCD));
#if DEBUG
        // _jitForceInlineDepth = unchecked((int)(0xCDCDCDCD));
#endif
        // _jitInlineMethodsWithEH = unchecked((int)(0xCDCDCDCD));
#if DEBUG
        jitHost->freeStringConfigValue(_jitInlineMethodsWithEHRange);
        // _jitLongAddress = unchecked((int)(0xCDCDCDCD));
        // _jitMaxUncheckedOffset = unchecked((int)(0xCDCDCDCD));
#endif
#if TARGET_ARM64
        // _jitPacEnabled = unchecked((int)(0xCDCDCDCD));
#endif
        // _jitEnableGenericVirtualDevirtualization = unchecked((int)(0xCDCDCDCD));
#if DEBUG
        // _jitMinOpts = unchecked((int)(0xCDCDCDCD));
        _jitMinOptsName.destroy(jitHost);
        // _jitMinOptsBbCount = unchecked((int)(0xCDCDCDCD));
        // _jitMinOptsCodeSize = unchecked((int)(0xCDCDCDCD));
        // _jitMinOptsInstrCount = unchecked((int)(0xCDCDCDCD));
        // _jitMinOptsLvNumCount = unchecked((int)(0xCDCDCDCD));
        // _jitMinOptsLvRefCount = unchecked((int)(0xCDCDCDCD));
        // _jitNoCSE = unchecked((int)(0xCDCDCDCD));
        // _jitNoCSE2 = unchecked((int)(0xCDCDCDCD));
        // _jitNoForceFallback = unchecked((int)(0xCDCDCDCD));
        // _jitNoForwardSub = unchecked((int)(0xCDCDCDCD));
        // _jitNoHoist = unchecked((int)(0xCDCDCDCD));
        // _jitNoMemoryBarriers = unchecked((int)(0xCDCDCDCD));
        // _jitNoStructPromotion = unchecked((int)(0xCDCDCDCD));
        // _jitNoUnroll = unchecked((int)(0xCDCDCDCD));
        // _jitOrder = unchecked((int)(0xCDCDCDCD));
        // _jitQueryCurrentStaticFieldClass = unchecked((int)(0xCDCDCDCD));
        // _jitReportFastTailCallDecisions = unchecked((int)(0xCDCDCDCD));
        // _jitPInvokeCheckEnabled = unchecked((int)(0xCDCDCDCD));
        // _jitPInvokeEnabled = unchecked((int)(0xCDCDCDCD));
        // _jitHoistLimit = unchecked((int)(0xCDCDCDCD));
        // _jitPrintInlinedMethodsVerbose = unchecked((int)(0xCDCDCDCD));
        _jitPrintInlinedMethods.destroy(jitHost);
        _jitPrintDevirtualizedMethods.destroy(jitHost);
        // _jitProfileChecks = unchecked((int)(0xCDCDCDCD));
        // _jitRequired = unchecked((int)(0xCDCDCDCD));
        // _jitStackAllocToLocalSize = unchecked((int)(0xCDCDCDCD));
        // _jitSkipArrayBoundCheck = unchecked((int)(0xCDCDCDCD));
        // _jitSlowDebugChecksEnabled = unchecked((int)(0xCDCDCDCD));
        // _jitSplitFunctionSize = unchecked((int)(0xCDCDCDCD));
        // _jitSsaStress = unchecked((int)(0xCDCDCDCD));
        // _jitStackChecks = unchecked((int)(0xCDCDCDCD));
        // _jitStress = unchecked((int)(0xCDCDCDCD));
        // _jitStressBBProf = unchecked((int)(0xCDCDCDCD));
        // _jitStressProcedureSplitting = unchecked((int)(0xCDCDCDCD));
        // _jitStressRegs = unchecked((int)(0xCDCDCDCD));
        jitHost->freeStringConfigValue(_jitStressRegsRange);
        // _jitStressSplitTreeLimit = unchecked((int)(0xCDCDCDCD));
        // _jitVNMapSelLimit = unchecked((int)(0xCDCDCDCD));
        // _runAltJitCode = unchecked((int)(0xCDCDCDCD));
        // _runComponentUnitTests = unchecked((int)(0xCDCDCDCD));
        // _shouldInjectFault = unchecked((int)(0xCDCDCDCD));
        // _tailcallStress = unchecked((int)(0xCDCDCDCD));
        _jitBreak.destroy(jitHost);
        _jitDebugBreak.destroy(jitHost);
        _jitDump.destroy(jitHost);
        // _jitHashDump = unchecked((int)(0xCDCDCDCD));
        // _jitDumpTier0 = unchecked((int)(0xCDCDCDCD));
        // _jitDumpOSR = unchecked((int)(0xCDCDCDCD));
        // _jitDumpAtOSROffset = unchecked((int)(0xCDCDCDCD));
        // _jitDumpInlinePhases = unchecked((int)(0xCDCDCDCD));
        // _jitDumpASCII = unchecked((int)(0xCDCDCDCD));
        // _jitDumpTerseLsra = unchecked((int)(0xCDCDCDCD));
        // _jitDumpToDebugger = unchecked((int)(0xCDCDCDCD));
        // _jitDumpVerboseSsa = unchecked((int)(0xCDCDCDCD));
        // _jitDumpVerboseTrees = unchecked((int)(0xCDCDCDCD));
        // _jitDumpTreeIDs = unchecked((int)(0xCDCDCDCD));
        // _jitDumpBeforeAfterMorph = unchecked((int)(0xCDCDCDCD));
        // _jitDumpTerseNextBlock = unchecked((int)(0xCDCDCDCD));
        _jitEHDump.destroy(jitHost);
        _jitExclude.destroy(jitHost);
        // _jitFakeProcedureSplitting = unchecked((int)(0xCDCDCDCD));
        _jitForceProcedureSplitting.destroy(jitHost);
        _jitGCDump.destroy(jitHost);
        _jitDebugDump.destroy(jitHost);
        _jitHalt.destroy(jitHost);
        _jitInclude.destroy(jitHost);
        _jitLateDisasm.destroy(jitHost);
        jitHost->freeStringConfigValue(_jitLateDisasmTo);
        _jitNoProcedureSplitting.destroy(jitHost);
        _jitNoProcedureSplittingEH.destroy(jitHost);
        _jitStressOnly.destroy(jitHost);
        _jitUnwindDump.destroy(jitHost);
        _jitDumpFg.destroy(jitHost);
        // _jitDumpFgHash = unchecked((int)(0xCDCDCDCD));
        // _jitDumpFgTier0 = unchecked((int)(0xCDCDCDCD));
        jitHost->freeStringConfigValue(_jitDumpFgDir);
        jitHost->freeStringConfigValue(_jitDumpFgFile);
        jitHost->freeStringConfigValue(_jitDumpFgPhase);
        jitHost->freeStringConfigValue(_jitDumpFgPrePhase);
        // _jitDumpFgDot = unchecked((int)(0xCDCDCDCD));
        // _jitDumpFgEH = unchecked((int)(0xCDCDCDCD));
        // _jitDumpFgLoops = unchecked((int)(0xCDCDCDCD));
        // _jitDumpFgConstrained = unchecked((int)(0xCDCDCDCD));
        // _jitDumpFgBlockID = unchecked((int)(0xCDCDCDCD));
        // _jitDumpFgBlockFlags = unchecked((int)(0xCDCDCDCD));
        // _jitDumpFgLoopFlags = unchecked((int)(0xCDCDCDCD));
        // _jitDumpFgBlockOrder = unchecked((int)(0xCDCDCDCD));
        // _jitDumpFgMemorySsa = unchecked((int)(0xCDCDCDCD));
        jitHost->freeStringConfigValue(_jitRange);
        jitHost->freeStringConfigValue(_jitStressModeNames);
        // _jitStressModeNamesOnly = unchecked((int)(0xCDCDCDCD));
        jitHost->freeStringConfigValue(_jitStressModeNamesAllow);
        jitHost->freeStringConfigValue(_jitStressModeNamesNot);
        jitHost->freeStringConfigValue(_jitStressRange);
        _jitEmitUnitTests.destroy(jitHost);
        jitHost->freeStringConfigValue(_jitEmitUnitTestsSections);
#endif
        _jitDisasm.destroy(jitHost);
        // _jitDisasmTesting = unchecked((int)(0xCDCDCDCD));
        // _jitDisasmDiffable = unchecked((int)(0xCDCDCDCD));
        // _jitDisasmSummary = unchecked((int)(0xCDCDCDCD));
        // _jitDisasmOnlyOptimized = unchecked((int)(0xCDCDCDCD));
        // _jitDisasmWithAlignmentBoundaries = unchecked((int)(0xCDCDCDCD));
        // _jitDisasmWithCodeBytes = unchecked((int)(0xCDCDCDCD));
#if DEBUG
        jitHost->freeStringConfigValue(_jitDisasmAssemblies);
        // _jitDisasmWithGC = unchecked((int)(0xCDCDCDCD));
        // _jitDisasmWithDebugInfo = unchecked((int)(0xCDCDCDCD));
        // _jitDisasmSpilled = unchecked((int)(0xCDCDCDCD));
        // _jitDisasmWithAddress = unchecked((int)(0xCDCDCDCD));
#endif
        jitHost->freeStringConfigValue(_jitStdOutFile);
        // _richDebugInfo = unchecked((int)(0xCDCDCDCD));
#if DEBUG
        jitHost->freeStringConfigValue(_writeRichDebugInfoFile);
#endif
#if FEATURE_LOOP_ALIGN
        // _jitAlignLoops = unchecked((int)(0xCDCDCDCD));
#else
        // _jitAlignLoops = unchecked((int)(0xCDCDCDCD));
#endif
        // _altJitAssertOnNYI = unchecked((int)(0xCDCDCDCD));
        // _enableEHWriteThru = unchecked((int)(0xCDCDCDCD));
        // _enableMultiRegLocals = unchecked((int)(0xCDCDCDCD));
        // _jitNoInline = unchecked((int)(0xCDCDCDCD));
#if DEBUG
#if DEBUG
        // _jitStressRex2Encoding = unchecked((int)(0xCDCDCDCD));
        // _jitStressPromotedEvexEncoding = unchecked((int)(0xCDCDCDCD));
#endif
#if TARGET_AMD64 || TARGET_X86
        // _jitStressEvexEncoding = unchecked((int)(0xCDCDCDCD));
#endif
#endif
#if TARGET_LOONGARCH64
        // _enableHWIntrinsic = unchecked((int)(0xCDCDCDCD));
#else
        // _enableHWIntrinsic = unchecked((int)(0xCDCDCDCD));
#endif
#if TARGET_AMD64 || TARGET_X86
        // _enableAVX = unchecked((int)(0xCDCDCDCD));
        // _enableAVX2 = unchecked((int)(0xCDCDCDCD));
        // _enableAVX512 = unchecked((int)(0xCDCDCDCD));
        // _enableAVX512BMM = unchecked((int)(0xCDCDCDCD));
        // _enableAVX512v2 = unchecked((int)(0xCDCDCDCD));
        // _enableAVX512v3 = unchecked((int)(0xCDCDCDCD));
        // _enableAVX10v1 = unchecked((int)(0xCDCDCDCD));
        // _enableAVX10v2 = unchecked((int)(0xCDCDCDCD));
        // _enableAPX = unchecked((int)(0xCDCDCDCD));
        // _enableAES = unchecked((int)(0xCDCDCDCD));
        // _enableAVX512VP2INTERSECT = unchecked((int)(0xCDCDCDCD));
        // _enableAVXIFMA = unchecked((int)(0xCDCDCDCD));
        // _enableAVXVNNI = unchecked((int)(0xCDCDCDCD));
        // _enableAVXVNNIINT = unchecked((int)(0xCDCDCDCD));
        // _enableGFNI = unchecked((int)(0xCDCDCDCD));
        // _enableSHA = unchecked((int)(0xCDCDCDCD));
        // _enableVAES = unchecked((int)(0xCDCDCDCD));
        // _enableWAITPKG = unchecked((int)(0xCDCDCDCD));
        // _enableX86Serialize = unchecked((int)(0xCDCDCDCD));
#elif TARGET_ARM64
        // _enableArm64Aes = unchecked((int)(0xCDCDCDCD));
        // _enableArm64Atomics = unchecked((int)(0xCDCDCDCD));
        // _enableArm64Crc32 = unchecked((int)(0xCDCDCDCD));
        // _enableArm64Dczva = unchecked((int)(0xCDCDCDCD));
        // _enableArm64Dp = unchecked((int)(0xCDCDCDCD));
        // _enableArm64Rdm = unchecked((int)(0xCDCDCDCD));
        // _enableArm64Sha1 = unchecked((int)(0xCDCDCDCD));
        // _enableArm64Sha256 = unchecked((int)(0xCDCDCDCD));
        // _enableArm64Sve = unchecked((int)(0xCDCDCDCD));
        // _enableArm64Sve2 = unchecked((int)(0xCDCDCDCD));
        // _enableArm64Sha3 = unchecked((int)(0xCDCDCDCD));
        // _enableArm64Sm4 = unchecked((int)(0xCDCDCDCD));
        // _enableArm64SveAes = unchecked((int)(0xCDCDCDCD));
        // _enableArm64SveSha3 = unchecked((int)(0xCDCDCDCD));
        // _enableArm64SveSm4 = unchecked((int)(0xCDCDCDCD));
#elif TARGET_RISCV64
        // _enableRiscV64Zba = unchecked((int)(0xCDCDCDCD));
        // _enableRiscV64Zbb = unchecked((int)(0xCDCDCDCD));
        // _enableRiscV64Zbs = unchecked((int)(0xCDCDCDCD));
#endif
        // _enableEmbeddedBroadcast = unchecked((int)(0xCDCDCDCD));
        // _enableEmbeddedMasking = unchecked((int)(0xCDCDCDCD));
        // _enableApxNDD = unchecked((int)(0xCDCDCDCD));
        // _enableApxConditionalChaining = unchecked((int)(0xCDCDCDCD));
        // _enableApxPPHint = unchecked((int)(0xCDCDCDCD));
        // _enableApxPP2 = unchecked((int)(0xCDCDCDCD));
        // _enableApxZU = unchecked((int)(0xCDCDCDCD));
#if FEATURE_SIMD
        // _jitDisableSimdVN = unchecked((int)(0xCDCDCDCD));
#endif
        // _jitConstCSE = unchecked((int)(0xCDCDCDCD));
        // _jitRLCSEGreedy = unchecked((int)(0xCDCDCDCD));
        // _jitRLCSEVerbose = unchecked((int)(0xCDCDCDCD));
#if DEBUG
        // _jitCSEHash = unchecked((int)(0xCDCDCDCD));
        // _jitCSEMask = unchecked((int)(0xCDCDCDCD));
        // _jitMetrics = unchecked((int)(0xCDCDCDCD));
        // _jitRandomCSE = unchecked((int)(0xCDCDCDCD));
        jitHost->freeStringConfigValue(_jitReplayCSE);
        jitHost->freeStringConfigValue(_jitReplayCSEReward);
        jitHost->freeStringConfigValue(_jitRLCSE);
        jitHost->freeStringConfigValue(_jitRLCSEAlpha);
        // _jitRLCSECandidateFeatures = unchecked((int)(0xCDCDCDCD));
        // _jitRLHook = unchecked((int)(0xCDCDCDCD));
        // _jitRLHookEmitFeatureNames = unchecked((int)(0xCDCDCDCD));
        jitHost->freeStringConfigValue(_jitRLHookCSEDecisions);
#endif
#if !DEBUG && !_DEBUG
        // _jitEnableNoWayAssert = unchecked((int)(0xCDCDCDCD));
#else
        // _jitEnableNoWayAssert = unchecked((int)(0xCDCDCDCD));
#endif
        // _displayMemStats = unchecked((int)(0xCDCDCDCD));
#if DEBUG
        // _jitEnregStats = unchecked((int)(0xCDCDCDCD));
#endif
        // _jitAggressiveInlining = unchecked((int)(0xCDCDCDCD));
        // _jitELTHookEnabled = unchecked((int)(0xCDCDCDCD));
        // _jitInlineSIMDMultiplier = unchecked((int)(0xCDCDCDCD));
        // _jitMaxLocalsToTrack = unchecked((int)(0xCDCDCDCD));
#if FEATURE_ENABLE_NO_RANGE_CHECKS
        // _jitNoRngChks = unchecked((int)(0xCDCDCDCD));
#endif
#if OPT_CONFIG
        // _jitDoAssertionProp = unchecked((int)(0xCDCDCDCD));
        // _jitDoCopyProp = unchecked((int)(0xCDCDCDCD));
        // _jitDoOptimizeIVs = unchecked((int)(0xCDCDCDCD));
        // _jitDoEarlyProp = unchecked((int)(0xCDCDCDCD));
        // _jitDoLoopHoisting = unchecked((int)(0xCDCDCDCD));
        // _jitDoLoopInversion = unchecked((int)(0xCDCDCDCD));
#endif
        // _jitLoopInversionSizeLimit = unchecked((int)(0xCDCDCDCD));
#if OPT_CONFIG
        // _jitDoRangeAnalysis = unchecked((int)(0xCDCDCDCD));
        // _jitDoVNBasedDeadStoreRemoval = unchecked((int)(0xCDCDCDCD));
        // _jitDoRedundantBranchOpts = unchecked((int)(0xCDCDCDCD));
        jitHost->freeStringConfigValue(_jitEnableRboRange);
        jitHost->freeStringConfigValue(_jitEnableHeadTailMergeRange);
        jitHost->freeStringConfigValue(_jitEnableVNBasedDeadStoreRemovalRange);
        jitHost->freeStringConfigValue(_jitEnableEarlyLivenessRange);
        jitHost->freeStringConfigValue(_jitOnlyOptimizeRange);
        jitHost->freeStringConfigValue(_jitEnablePhysicalPromotionRange);
        jitHost->freeStringConfigValue(_jitEnableCrossBlockLocalAssertionPropRange);
        jitHost->freeStringConfigValue(_jitEnableInductionVariableOptsRange);
        jitHost->freeStringConfigValue(_jitEnableLocalAddrPropagationRange);
        // _jitDoSsa = unchecked((int)(0xCDCDCDCD));
        // _jitDoValueNumber = unchecked((int)(0xCDCDCDCD));
        jitHost->freeStringConfigValue(_jitOptRepeatRange);
        // _jitDoIfConversion = unchecked((int)(0xCDCDCDCD));
        // _jitDoOptimizeMaskConversions = unchecked((int)(0xCDCDCDCD));
        // _jitOptimizeAwait = unchecked((int)(0xCDCDCDCD));
        jitHost->freeStringConfigValue(_jitAsyncDefaultValueAnalysisRange);
        jitHost->freeStringConfigValue(_jitAsyncPreservedValueAnalysisRange);
        jitHost->freeStringConfigValue(_jitAsyncReuseContinuationsRange);
#endif
        // _jitAsyncReuseContinuations = unchecked((int)(0xCDCDCDCD));
        // _jitEnableOptRepeat = unchecked((int)(0xCDCDCDCD));
        _jitOptRepeat.destroy(jitHost);
        // _jitOptRepeatCount = unchecked((int)(0xCDCDCDCD));
        // _jitVNMapSelBudget = unchecked((int)(0xCDCDCDCD));
        // _tailCallLoopOpt = unchecked((int)(0xCDCDCDCD));
        _altJit.destroy(jitHost);
        _altJitNgen.destroy(jitHost);
        jitHost->freeStringConfigValue(_altJitExcludeAssemblies);
        // _jitMeasureIR = unchecked((int)(0xCDCDCDCD));
        // _jitReportMetrics = unchecked((int)(0xCDCDCDCD));
        jitHost->freeStringConfigValue(_jitFuncInfoFile);
        jitHost->freeStringConfigValue(_jitTimeLogCsv);
        jitHost->freeStringConfigValue(_jitTimeLogFile);
        jitHost->freeStringConfigValue(_tailCallOpt);
        // _fastTailCalls = unchecked((int)(0xCDCDCDCD));
        // _jitMeasureNowayAssert = unchecked((int)(0xCDCDCDCD));
        jitHost->freeStringConfigValue(_jitMeasureNowayAssertFile);
#if DEBUG
        // _enableExtraSuperPmiQueries = unchecked((int)(0xCDCDCDCD));
        // _jitInlineDumpData = unchecked((int)(0xCDCDCDCD));
        // _jitInlineDumpXml = unchecked((int)(0xCDCDCDCD));
        jitHost->freeStringConfigValue(_jitInlineDumpXmlFile);
        // _jitInlinePolicyDumpXml = unchecked((int)(0xCDCDCDCD));
        // _jitInlineLimit = unchecked((int)(0xCDCDCDCD));
        // _jitInlinePolicyDiscretionary = unchecked((int)(0xCDCDCDCD));
        // _jitInlinePolicyFull = unchecked((int)(0xCDCDCDCD));
        // _jitInlinePolicySize = unchecked((int)(0xCDCDCDCD));
        // _jitInlinePolicyRandom = unchecked((int)(0xCDCDCDCD));
        // _jitInlinePolicyReplay = unchecked((int)(0xCDCDCDCD));
        jitHost->freeStringConfigValue(_jitNoInlineRange);
        jitHost->freeStringConfigValue(_jitInlineReplayFile);
#endif
        // _jitExtDefaultPolicy = unchecked((int)(0xCDCDCDCD));
        // _jitExtDefaultPolicyMaxIL = unchecked((int)(0xCDCDCDCD));
        // _jitExtDefaultPolicyMaxILRoot = unchecked((int)(0xCDCDCDCD));
        // _jitExtDefaultPolicyMaxILProf = unchecked((int)(0xCDCDCDCD));
        // _jitExtDefaultPolicyMaxBB = unchecked((int)(0xCDCDCDCD));
        // _jitExtDefaultPolicyProfTrust = unchecked((int)(0xCDCDCDCD));
        // _jitExtDefaultPolicyProfScale = unchecked((int)(0xCDCDCDCD));
        // _jitInlinePolicyModel = unchecked((int)(0xCDCDCDCD));
        // _jitInlinePolicyProfile = unchecked((int)(0xCDCDCDCD));
        // _jitInlinePolicyProfileThreshold = unchecked((int)(0xCDCDCDCD));
#if DEBUG
        jitHost->freeStringConfigValue(_jitObjectStackAllocationRange);
#endif
        // _jitObjectStackAllocation = unchecked((int)(0xCDCDCDCD));
        // _jitObjectStackAllocationRefClass = unchecked((int)(0xCDCDCDCD));
        // _jitObjectStackAllocationBoxedValueClass = unchecked((int)(0xCDCDCDCD));
        // _jitObjectStackAllocationConditionalEscape = unchecked((int)(0xCDCDCDCD));
#if DEBUG
        jitHost->freeStringConfigValue(_jitObjectStackAllocationConditionalEscapeRange);
#endif
        // _jitObjectStackAllocationArray = unchecked((int)(0xCDCDCDCD));
        // _jitObjectStackAllocationSize = unchecked((int)(0xCDCDCDCD));
        // _jitObjectStackAllocationTrackFields = unchecked((int)(0xCDCDCDCD));
#if DEBUG
        jitHost->freeStringConfigValue(_jitObjectStackAllocationTrackFieldsRange);
        // _jitObjectStackAllocationDumpConnGraph = unchecked((int)(0xCDCDCDCD));
#endif
        // _jitEECallTimingInfo = unchecked((int)(0xCDCDCDCD));
#if DEBUG
        // _jitEnableFinallyCloning = unchecked((int)(0xCDCDCDCD));
        // _jitEnableRemoveEmptyTry = unchecked((int)(0xCDCDCDCD));
        // _jitEnableRemoveEmptyTryCatchOrTryFault = unchecked((int)(0xCDCDCDCD));
#endif
        // _jitEnableGuardedDevirtualization = unchecked((int)(0xCDCDCDCD));
        // _jitGuardedDevirtualizationMaxTypeChecks = unchecked((int)(0xCDCDCDCD));
        // _jitGuardedDevirtualizationChainLikelihood = unchecked((int)(0xCDCDCDCD));
        // _jitGuardedDevirtualizationChainStatements = unchecked((int)(0xCDCDCDCD));
#if DEBUG
        jitHost->freeStringConfigValue(_jitGuardedDevirtualizationRange);
        // _jitRandomGuardedDevirtualization = unchecked((int)(0xCDCDCDCD));
#endif
#if FEATURE_ON_STACK_REPLACEMENT
        // _tC_OnStackReplacement = unchecked((int)(0xCDCDCDCD));
#else
        // _tC_OnStackReplacement = unchecked((int)(0xCDCDCDCD));
#endif
        // _tC_OnStackReplacement_InitialCounter = unchecked((int)(0xCDCDCDCD));
        // _tC_PartialCompilation = unchecked((int)(0xCDCDCDCD));
#if DEBUG
        // _jitRandomPartialCompilation = unchecked((int)(0xCDCDCDCD));
#endif
        // _tC_PatchpointStrategy = unchecked((int)(0xCDCDCDCD));
#if DEBUG
        // _jitRandomOnStackReplacement = unchecked((int)(0xCDCDCDCD));
        // _jitOffsetOnStackReplacement = unchecked((int)(0xCDCDCDCD));
        jitHost->freeStringConfigValue(_jitEnableOsrRange);
        jitHost->freeStringConfigValue(_jitEnablePatchpointRange);
#endif
        // _jitInterlockedProfiling = unchecked((int)(0xCDCDCDCD));
        // _jitScalableProfiling = unchecked((int)(0xCDCDCDCD));
        // _jitCounterPadding = unchecked((int)(0xCDCDCDCD));
        // _jitMinimalJitProfiling = unchecked((int)(0xCDCDCDCD));
        // _jitMinimalPrejitProfiling = unchecked((int)(0xCDCDCDCD));
        // _jitProfileValues = unchecked((int)(0xCDCDCDCD));
        // _jitProfileCasts = unchecked((int)(0xCDCDCDCD));
        // _jitConsumeProfileForCasts = unchecked((int)(0xCDCDCDCD));
        // _jitClassProfiling = unchecked((int)(0xCDCDCDCD));
        // _jitDelegateProfiling = unchecked((int)(0xCDCDCDCD));
        // _jitVTableProfiling = unchecked((int)(0xCDCDCDCD));
        // _jitEdgeProfiling = unchecked((int)(0xCDCDCDCD));
        // _jitCollect64BitCounts = unchecked((int)(0xCDCDCDCD));
#if DEBUG
        // _jitInstrumentIfOptimizing = unchecked((int)(0xCDCDCDCD));
        jitHost->freeStringConfigValue(_jitInstrumentIfOptimizingRange);
#endif
        // _jitInstrumentInlinees = unchecked((int)(0xCDCDCDCD));
        // _jitDisablePGO = unchecked((int)(0xCDCDCDCD));
#if DEBUG
        jitHost->freeStringConfigValue(_jitEnablePGORange);
        // _jitRandomEdgeCounts = unchecked((int)(0xCDCDCDCD));
        // _jitCrossCheckDevirtualizationAndPGO = unchecked((int)(0xCDCDCDCD));
        // _jitNoteFailedExactDevirtualization = unchecked((int)(0xCDCDCDCD));
        // _jitRandomlyCollect64BitCounts = unchecked((int)(0xCDCDCDCD));
        // _jitSynthesizeCounts = unchecked((int)(0xCDCDCDCD));
        // _jitPropagateSynthesizedCountsToProfileData = unchecked((int)(0xCDCDCDCD));
        // _jitSynthesisUseSolver = unchecked((int)(0xCDCDCDCD));
        jitHost->freeStringConfigValue(_jitSynthesisExceptionWeight);
#endif
        // _jitEnableExactDevirtualization = unchecked((int)(0xCDCDCDCD));
        // _jitForceControlFlowGuard = unchecked((int)(0xCDCDCDCD));
        // _jitCFGUseDispatcher = unchecked((int)(0xCDCDCDCD));
        // _jitEnableHeadTailMerge = unchecked((int)(0xCDCDCDCD));
        // _jitEnablePhysicalPromotion = unchecked((int)(0xCDCDCDCD));
        // _jitEnableCrossBlockLocalAssertionProp = unchecked((int)(0xCDCDCDCD));
        // _jitEnablePostorderLocalAssertionProp = unchecked((int)(0xCDCDCDCD));
        // _jitEnableStrengthReduction = unchecked((int)(0xCDCDCDCD));
        // _jitEnableInductionVariableOpts = unchecked((int)(0xCDCDCDCD));
#if DEBUG
        jitHost->freeStringConfigValue(_jitFunctionFile);
        _jitRawHexCode.destroy(jitHost);
        jitHost->freeStringConfigValue(_jitRawHexCodeFile);
#if TARGET_ARM64
        // _jitSaveFpLrWithCalleeSavedRegisters = unchecked((int)(0xCDCDCDCD));
        // _jitUseScalableVectorT = unchecked((int)(0xCDCDCDCD));
#endif
#if TARGET_LOONGARCH64
        // _jitDispIns = unchecked((int)(0xCDCDCDCD));
#endif
#endif
#if TARGET_WASM
        // _jitWasmNyiToR2RUnsupported = unchecked((int)(0xCDCDCDCD));
#if DEBUG
        jitHost->freeStringConfigValue(_jitR2RUnsupportedRange);
#endif
        // _jitWasmFunclets = unchecked((int)(0xCDCDCDCD));
#endif
        // _jitEnregStructLocals = unchecked((int)(0xCDCDCDCD));
        _isInitialized = false;
    }

    public unsafe void initialize(ICorJitHost* jitHost)
    {
        assert(!_isInitialized);

#if DEBUG
        _altJitLimit = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("AltJitLimit"u8))), 0);
        _altJitSkipOnAssert = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("AltJitSkipOnAssert"u8))), 0);
        _breakOnDumpToken = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("BreakOnDumpToken"u8))), unchecked((int)(0xffffffff)));
        _debugBreakOnVerificationFailure = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("DebugBreakOnVerificationFailure"u8))), 0);
        _displayLoopHoistStats = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitLoopHoistStats"u8))), 0);
        _displayLsraStats = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitLsraStats"u8))), 0);
        _jitLsraOrdering = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitLsraOrdering"u8))));
        _enablePCRelAddr = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnablePCRelAddr"u8))), 1);
        _jitAssertOnMaxRAPasses = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAssertOnMaxRAPasses"u8))), 0);
        _jitBreakEmitOutputInstr = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitBreakEmitOutputInstr"u8))), -1);
        _jitBreakMorphTree = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitBreakMorphTree"u8))), unchecked((int)(0xffffffff)));
        _jitBreakOnBadCode = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitBreakOnBadCode"u8))), 0);
        _jitBreakOnMinOpts = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JITBreakOnMinOpts"u8))), 0);
        _jitCloneLoops = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCloneLoops"u8))), 1);
        _jitCloneLoopsWithEH = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCloneLoopsWithEH"u8))), 1);
        _jitCloneLoopsWithGdvTests = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCloneLoopsWithGdvTests"u8))), 1);
#endif
        _jitCloneLoopsSizeLimit = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCloneLoopsSizeLimit"u8))), 400);
        _jitCloneLoopsMinPerCallRatio = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCloneLoopsMinPerCallRatio"u8))), 4);
#if DEBUG
        _jitDebugLogLoopCloning = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDebugLogLoopCloning"u8))), 0);
        _jitDefaultFill = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDefaultFill"u8))), 0xdd);
        _jitAlignLoopMinBlockWeight = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAlignLoopMinBlockWeight"u8))), DEFAULT_ALIGN_LOOP_MIN_BLOCK_WEIGHT);
        _jitAlignLoopMaxCodeSize = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAlignLoopMaxCodeSize"u8))), DEFAULT_MAX_LOOPSIZE_FOR_ALIGN);
        _jitAlignLoopBoundary = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAlignLoopBoundary"u8))), DEFAULT_ALIGN_LOOP_BOUNDARY);
        _jitAlignLoopForJcc = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAlignLoopForJcc"u8))), 0);
        _jitAlignLoopAdaptive = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAlignLoopAdaptive"u8))), 1);
        _jitHideAlignBehindJmp = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitHideAlignBehindJmp"u8))), 1);
        _jitOptimizeStructHiddenBuffer = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitOptimizeStructHiddenBuffer"u8))), 1);
#endif
        _jitEnableStoreLclFldCoalescing = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableStoreLclFldCoalescing"u8))), 1);
#if DEBUG
        _jitUnrollLoopMaxIterationCount = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitUnrollLoopMaxIterationCount"u8))), DEFAULT_UNROLL_LOOP_MAX_ITERATION_COUNT);
        _jitUnrollLoopsWithEH = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitUnrollLoopsWithEH"u8))), 0);
        _jitDirectAlloc = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDirectAlloc"u8))), 0);
        _jitDoubleAlign = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoubleAlign"u8))), 1);
        _jitEmitPrintRefRegs = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEmitPrintRefRegs"u8))), 0);
        _jitEnableDevirtualization = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableDevirtualization"u8))), 1);
        _jitEnableLateDevirtualization = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableLateDevirtualization"u8))), 1);
        _jitExpensiveDebugCheckLevel = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitExpensiveDebugCheckLevel"u8))), 0);
        _jitForceFallback = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitForceFallback"u8))), 0);
        _jitFullyInt = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitFullyInt"u8))), 0);
        _jitFunctionTrace = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitFunctionTrace"u8))), 0);
        _jitGCChecks = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitGCChecks"u8))), 0);
        _jitGCInfoLogging = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitGCInfoLogging"u8))), 0);
        _jitHashBreak = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitHashBreak"u8))), -1);
        _jitHashHalt = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitHashHalt"u8))), -1);
        _jitInlineAdditionalMultiplier = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineAdditionalMultiplier"u8))), 0);
        _jitInlinePrintStats = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlinePrintStats"u8))), 0);
        _jitInlineSize = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineSize"u8))), DEFAULT_MAX_INLINE_SIZE);
        _jitInlineDepth = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineDepth"u8))), DEFAULT_MAX_INLINE_DEPTH);
#endif
        _jitInlineBudget = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineBudget"u8))), DEFAULT_INLINE_BUDGET);
#if DEBUG
        _jitForceInlineDepth = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitForceInlineDepth"u8))), DEFAULT_MAX_FORCE_INLINE_DEPTH);
#endif
        _jitInlineMethodsWithEH = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineMethodsWithEH"u8))), 1);
#if DEBUG
        _jitInlineMethodsWithEHRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineMethodsWithEHRange"u8))));
        _jitLongAddress = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitLongAddress"u8))), 0);
        _jitMaxUncheckedOffset = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitMaxUncheckedOffset"u8))), 8);
#endif
#if TARGET_ARM64
        _jitPacEnabled = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitPacEnabled"u8))), 0);
#endif
        _jitEnableGenericVirtualDevirtualization = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableGenericVirtualDevirtualization"u8))), 1);
#if DEBUG
        _jitMinOpts = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JITMinOpts"u8))), 0);
        var jitMinOptsNameValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JITMinOptsName"u8))));
        _jitMinOptsName = new MethodSet(jitMinOptsNameValue, jitHost);
        _jitMinOptsBbCount = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JITMinOptsBbCount"u8))), DEFAULT_MIN_OPTS_BB_COUNT);
        _jitMinOptsCodeSize = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JITMinOptsCodeSize"u8))), DEFAULT_MIN_OPTS_CODE_SIZE);
        _jitMinOptsInstrCount = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JITMinOptsInstrCount"u8))), DEFAULT_MIN_OPTS_INSTR_COUNT);
        _jitMinOptsLvNumCount = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JITMinOptsLvNumcount"u8))), DEFAULT_MIN_OPTS_LV_NUM_COUNT);
        _jitMinOptsLvRefCount = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JITMinOptsLvRefcount"u8))), DEFAULT_MIN_OPTS_LV_REF_COUNT);
        _jitNoCSE = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoCSE"u8))), 0);
        _jitNoCSE2 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoCSE2"u8))), 0);
        _jitNoForceFallback = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoForceFallback"u8))), 0);
        _jitNoForwardSub = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoForwardSub"u8))), 0);
        _jitNoHoist = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoHoist"u8))), 0);
        _jitNoMemoryBarriers = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoMemoryBarriers"u8))), 0);
        _jitNoStructPromotion = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoStructPromotion"u8))), 0);
        _jitNoUnroll = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoUnroll"u8))), 0);
        _jitOrder = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitOrder"u8))), 0);
        _jitQueryCurrentStaticFieldClass = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitQueryCurrentStaticFieldClass"u8))), 1);
        _jitReportFastTailCallDecisions = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitReportFastTailCallDecisions"u8))), 0);
        _jitPInvokeCheckEnabled = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JITPInvokeCheckEnabled"u8))), 0);
        _jitPInvokeEnabled = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JITPInvokeEnabled"u8))), 1);
        _jitHoistLimit = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitHoistLimit"u8))), -1);
        _jitPrintInlinedMethodsVerbose = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitPrintInlinedMethodsVerboseLevel"u8))), 0);
        var jitPrintInlinedMethodsValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitPrintInlinedMethods"u8))));
        _jitPrintInlinedMethods = new MethodSet(jitPrintInlinedMethodsValue, jitHost);
        var jitPrintDevirtualizedMethodsValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitPrintDevirtualizedMethods"u8))));
        _jitPrintDevirtualizedMethods = new MethodSet(jitPrintDevirtualizedMethodsValue, jitHost);
        _jitProfileChecks = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitProfileChecks"u8))), -1);
        _jitRequired = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JITRequired"u8))), -1);
        _jitStackAllocToLocalSize = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStackAllocToLocalSize"u8))), DEFAULT_MAX_LOCALLOC_TO_LOCAL_SIZE);
        _jitSkipArrayBoundCheck = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitSkipArrayBoundCheck"u8))), 0);
        _jitSlowDebugChecksEnabled = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitSlowDebugChecksEnabled"u8))), 1);
        _jitSplitFunctionSize = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitSplitFunctionSize"u8))), 0);
        _jitSsaStress = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitSsaStress"u8))), 0);
        _jitStackChecks = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStackChecks"u8))), 0);
        _jitStress = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStress"u8))), 0);
        _jitStressBBProf = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressBBProf"u8))), 0);
        _jitStressProcedureSplitting = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressProcedureSplitting"u8))), 0);
        _jitStressRegs = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressRegs"u8))), 0);
        _jitStressRegsRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressRegsRange"u8))));
        _jitStressSplitTreeLimit = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressSplitTreeLimit"u8))), -1);
        _jitVNMapSelLimit = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitVNMapSelLimit"u8))), 0);
        _runAltJitCode = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("RunAltJitCode"u8))), 1);
        _runComponentUnitTests = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitComponentUnitTests"u8))), 0);
        _shouldInjectFault = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("InjectFault"u8))), 0);
        _tailcallStress = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("TailcallStress"u8))), 0);
        var jitBreakValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitBreak"u8))));
        _jitBreak = new MethodSet(jitBreakValue, jitHost);
        var jitDebugBreakValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDebugBreak"u8))));
        _jitDebugBreak = new MethodSet(jitDebugBreakValue, jitHost);
        var jitDumpValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDump"u8))));
        _jitDump = new MethodSet(jitDumpValue, jitHost);
        _jitHashDump = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitHashDump"u8))), -1);
        _jitDumpTier0 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpTier0"u8))), 1);
        _jitDumpOSR = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpOSR"u8))), 1);
        _jitDumpAtOSROffset = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpAtOSROffset"u8))), -1);
        _jitDumpInlinePhases = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpInlinePhases"u8))), 1);
        _jitDumpASCII = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpASCII"u8))), 1);
        _jitDumpTerseLsra = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpTerseLsra"u8))), 1);
        _jitDumpToDebugger = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpToDebugger"u8))), 0);
        _jitDumpVerboseSsa = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpVerboseSsa"u8))), 0);
        _jitDumpVerboseTrees = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpVerboseTrees"u8))), 0);
        _jitDumpTreeIDs = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpTreeIDs"u8))), 1);
        _jitDumpBeforeAfterMorph = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpBeforeAfterMorph"u8))), 0);
        _jitDumpTerseNextBlock = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpTerseNextBlock"u8))), 0);
        var jitEHDumpValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEHDump"u8))));
        _jitEHDump = new MethodSet(jitEHDumpValue, jitHost);
        var jitExcludeValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitExclude"u8))));
        _jitExclude = new MethodSet(jitExcludeValue, jitHost);
        _jitFakeProcedureSplitting = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitFakeProcedureSplitting"u8))), 0);
        var jitForceProcedureSplittingValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitForceProcedureSplitting"u8))));
        _jitForceProcedureSplitting = new MethodSet(jitForceProcedureSplittingValue, jitHost);
        var jitGCDumpValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitGCDump"u8))));
        _jitGCDump = new MethodSet(jitGCDumpValue, jitHost);
        var jitDebugDumpValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDebugDump"u8))));
        _jitDebugDump = new MethodSet(jitDebugDumpValue, jitHost);
        var jitHaltValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitHalt"u8))));
        _jitHalt = new MethodSet(jitHaltValue, jitHost);
        var jitIncludeValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInclude"u8))));
        _jitInclude = new MethodSet(jitIncludeValue, jitHost);
        var jitLateDisasmValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitLateDisasm"u8))));
        _jitLateDisasm = new MethodSet(jitLateDisasmValue, jitHost);
        _jitLateDisasmTo = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitLateDisasmTo"u8))));
        var jitNoProcedureSplittingValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoProcedureSplitting"u8))));
        _jitNoProcedureSplitting = new MethodSet(jitNoProcedureSplittingValue, jitHost);
        var jitNoProcedureSplittingEHValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoProcedureSplittingEH"u8))));
        _jitNoProcedureSplittingEH = new MethodSet(jitNoProcedureSplittingEHValue, jitHost);
        var jitStressOnlyValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressOnly"u8))));
        _jitStressOnly = new MethodSet(jitStressOnlyValue, jitHost);
        var jitUnwindDumpValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitUnwindDump"u8))));
        _jitUnwindDump = new MethodSet(jitUnwindDumpValue, jitHost);
        var jitDumpFgValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFg"u8))));
        _jitDumpFg = new MethodSet(jitDumpFgValue, jitHost);
        _jitDumpFgHash = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgHash"u8))), 0);
        _jitDumpFgTier0 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgTier0"u8))), 1);
        _jitDumpFgDir = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgDir"u8))));
        _jitDumpFgFile = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgFile"u8))));
        _jitDumpFgPhase = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgPhase"u8))));
        _jitDumpFgPrePhase = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgPrePhase"u8))));
        _jitDumpFgDot = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgDot"u8))), 1);
        _jitDumpFgEH = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgEH"u8))), 0);
        _jitDumpFgLoops = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgLoops"u8))), 0);
        _jitDumpFgConstrained = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgConstrained"u8))), 1);
        _jitDumpFgBlockID = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgBlockID"u8))), 0);
        _jitDumpFgBlockFlags = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgBlockFlags"u8))), 0);
        _jitDumpFgLoopFlags = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgLoopFlags"u8))), 0);
        _jitDumpFgBlockOrder = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgBlockOrder"u8))), 0);
        _jitDumpFgMemorySsa = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgMemorySsa"u8))), 0);
        _jitRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRange"u8))));
        _jitStressModeNames = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressModeNames"u8))));
        _jitStressModeNamesOnly = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressModeNamesOnly"u8))), 0);
        _jitStressModeNamesAllow = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressModeNamesAllow"u8))));
        _jitStressModeNamesNot = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressModeNamesNot"u8))));
        _jitStressRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressRange"u8))));
        var jitEmitUnitTestsValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEmitUnitTests"u8))));
        _jitEmitUnitTests = new MethodSet(jitEmitUnitTestsValue, jitHost);
        _jitEmitUnitTestsSections = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEmitUnitTestsSections"u8))));
#endif
        var jitDisasmValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasm"u8))));
        _jitDisasm = new MethodSet(jitDisasmValue, jitHost);
        _jitDisasmTesting = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmTesting"u8))), 0);
        _jitDisasmDiffable = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmDiffable"u8))), 0);
        _jitDisasmSummary = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmSummary"u8))), 0);
        _jitDisasmOnlyOptimized = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmOnlyOptimized"u8))), 0);
        _jitDisasmWithAlignmentBoundaries = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmWithAlignmentBoundaries"u8))), 0);
        _jitDisasmWithCodeBytes = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmWithCodeBytes"u8))), 0);
#if DEBUG
        _jitDisasmAssemblies = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmAssemblies"u8))));
        _jitDisasmWithGC = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmWithGC"u8))), 0);
        _jitDisasmWithDebugInfo = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmWithDebugInfo"u8))), 0);
        _jitDisasmSpilled = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmSpilled"u8))), 0);
        _jitDisasmWithAddress = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmWithAddress"u8))), 0);
#endif
        _jitStdOutFile = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStdOutFile"u8))));
        _richDebugInfo = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("RichDebugInfo"u8))), 0);
#if DEBUG
        _writeRichDebugInfoFile = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("WriteRichDebugInfoFile"u8))));
#endif
#if FEATURE_LOOP_ALIGN
        _jitAlignLoops = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAlignLoops"u8))), 1);
#else
        _jitAlignLoops = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAlignLoops"u8))), 0);
#endif
        _altJitAssertOnNYI = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("AltJitAssertOnNYI"u8))), 1);
        _enableEHWriteThru = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableEHWriteThru"u8))), 1);
        _enableMultiRegLocals = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableMultiRegLocals"u8))), 1);
        _jitNoInline = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoInline"u8))), 0);
#if DEBUG
#if DEBUG
        _jitStressRex2Encoding = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressRex2Encoding"u8))), 0);
        _jitStressPromotedEvexEncoding = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressPromotedEvexEncoding"u8))), 0);
#endif
#if TARGET_AMD64 || TARGET_X86
        _jitStressEvexEncoding = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressEvexEncoding"u8))), 0);
#endif
#endif
#if TARGET_LOONGARCH64
        _enableHWIntrinsic = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableHWIntrinsic"u8))), 0);
#else
        _enableHWIntrinsic = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableHWIntrinsic"u8))), 1);
#endif
#if TARGET_AMD64 || TARGET_X86
        _enableAVX = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVX"u8))), 1);
        _enableAVX2 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVX2"u8))), 1);
        _enableAVX512 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVX512"u8))), 1);
        _enableAVX512BMM = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVX512BMM"u8))), 1);
        _enableAVX512v2 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVX512v2"u8))), 1);
        _enableAVX512v3 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVX512v3"u8))), 1);
        _enableAVX10v1 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVX10v1"u8))), 1);
        _enableAVX10v2 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVX10v2"u8))), 0);
        _enableAPX = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAPX"u8))), 0);
        _enableAES = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAES"u8))), 1);
        _enableAVX512VP2INTERSECT = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVX512VP2INTERSECT"u8))), 1);
        _enableAVXIFMA = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVXIFMA"u8))), 1);
        _enableAVXVNNI = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVXVNNI"u8))), 1);
        _enableAVXVNNIINT = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVXVNNIINT"u8))), 1);
        _enableGFNI = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableGFNI"u8))), 1);
        _enableSHA = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableSHA"u8))), 1);
        _enableVAES = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableVAES"u8))), 1);
        _enableWAITPKG = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableWAITPKG"u8))), 1);
        _enableX86Serialize = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableX86Serialize"u8))), 1);
#elif TARGET_ARM64
        _enableArm64Aes = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Aes"u8))), 1);
        _enableArm64Atomics = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Atomics"u8))), 1);
        _enableArm64Crc32 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Crc32"u8))), 1);
        _enableArm64Dczva = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Dczva"u8))), 1);
        _enableArm64Dp = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Dp"u8))), 1);
        _enableArm64Rdm = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Rdm"u8))), 1);
        _enableArm64Sha1 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Sha1"u8))), 1);
        _enableArm64Sha256 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Sha256"u8))), 1);
        _enableArm64Sve = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Sve"u8))), 1);
        _enableArm64Sve2 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Sve2"u8))), 1);
        _enableArm64Sha3 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Sha3"u8))), 1);
        _enableArm64Sm4 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Sm4"u8))), 1);
        _enableArm64SveAes = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64SveAes"u8))), 1);
        _enableArm64SveSha3 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64SveSha3"u8))), 1);
        _enableArm64SveSm4 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64SveSm4"u8))), 1);
#elif TARGET_RISCV64
        _enableRiscV64Zba = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableRiscV64Zba"u8))), 1);
        _enableRiscV64Zbb = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableRiscV64Zbb"u8))), 1);
        _enableRiscV64Zbs = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableRiscV64Zbs"u8))), 1);
#endif
        _enableEmbeddedBroadcast = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableEmbeddedBroadcast"u8))), 1);
        _enableEmbeddedMasking = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableEmbeddedMasking"u8))), 1);
        _enableApxNDD = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableApxNDD"u8))), 0);
        _enableApxConditionalChaining = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableApxConditionalChaining"u8))), 0);
        _enableApxPPHint = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableApxPPHint"u8))), 0);
        _enableApxPP2 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableApxPP2"u8))), 0);
        _enableApxZU = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableApxZU"u8))), 0);
#if FEATURE_SIMD
        _jitDisableSimdVN = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisableSimdVN"u8))), 0);
#endif
        _jitConstCSE = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitConstCSE"u8))), CONST_CSE_ENABLE_ARM_RISCV64);
        _jitRLCSEGreedy = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRLCSEGreedy"u8))), 0);
        _jitRLCSEVerbose = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRLCSEVerbose"u8))), 0);
#if DEBUG
        _jitCSEHash = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCSEHash"u8))), -1);
        _jitCSEMask = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCSEMask"u8))), 0);
        _jitMetrics = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitMetrics"u8))), 0);
        _jitRandomCSE = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRandomCSE"u8))), 0);
        _jitReplayCSE = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitReplayCSE"u8))));
        _jitReplayCSEReward = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitReplayCSEReward"u8))));
        _jitRLCSE = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRLCSE"u8))));
        _jitRLCSEAlpha = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRLCSEAlpha"u8))));
        _jitRLCSECandidateFeatures = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRLCSECandidateFeatures"u8))), 0);
        _jitRLHook = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRLHook"u8))), 0);
        _jitRLHookEmitFeatureNames = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRLHookEmitFeatureNames"u8))), 0);
        _jitRLHookCSEDecisions = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRLHookCSEDecisions"u8))));
#endif
#if !DEBUG && !_DEBUG
        _jitEnableNoWayAssert = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableNoWayAssert"u8))), 0);
#else
        _jitEnableNoWayAssert = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableNoWayAssert"u8))), 1);
#endif
        _displayMemStats = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitMemStats"u8))), 0);
#if DEBUG
        _jitEnregStats = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnregStats"u8))), 0);
#endif
        _jitAggressiveInlining = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAggressiveInlining"u8))), 0);
        _jitELTHookEnabled = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitELTHookEnabled"u8))), 0);
        _jitInlineSIMDMultiplier = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineSIMDMultiplier"u8))), 3);
        _jitMaxLocalsToTrack = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitMaxLocalsToTrack"u8))), 0x400);
#if FEATURE_ENABLE_NO_RANGE_CHECKS
        _jitNoRngChks = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoRngChks"u8))), 0);
#endif
#if OPT_CONFIG
        _jitDoAssertionProp = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoAssertionProp"u8))), 1);
        _jitDoCopyProp = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoCopyProp"u8))), 1);
        _jitDoOptimizeIVs = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoOptimizeIVs"u8))), 1);
        _jitDoEarlyProp = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoEarlyProp"u8))), 1);
        _jitDoLoopHoisting = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoLoopHoisting"u8))), 1);
        _jitDoLoopInversion = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoLoopInversion"u8))), 1);
#endif
        _jitLoopInversionSizeLimit = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitLoopInversionSizeLimit"u8))), 100);
#if OPT_CONFIG
        _jitDoRangeAnalysis = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoRangeAnalysis"u8))), 1);
        _jitDoVNBasedDeadStoreRemoval = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoVNBasedDeadStoreRemoval"u8))), 1);
        _jitDoRedundantBranchOpts = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoRedundantBranchOpts"u8))), 1);
        _jitEnableRboRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableRboRange"u8))));
        _jitEnableHeadTailMergeRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableHeadTailMergeRange"u8))));
        _jitEnableVNBasedDeadStoreRemovalRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableVNBasedDeadStoreRemovalRange"u8))));
        _jitEnableEarlyLivenessRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableEarlyLivenessRange"u8))));
        _jitOnlyOptimizeRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitOnlyOptimizeRange"u8))));
        _jitEnablePhysicalPromotionRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnablePhysicalPromotionRange"u8))));
        _jitEnableCrossBlockLocalAssertionPropRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableCrossBlockLocalAssertionPropRange"u8))));
        _jitEnableInductionVariableOptsRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableInductionVariableOptsRange"u8))));
        _jitEnableLocalAddrPropagationRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableLocalAddrPropagationRange"u8))));
        _jitDoSsa = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoSsa"u8))), 1);
        _jitDoValueNumber = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoValueNumber"u8))), 1);
        _jitOptRepeatRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitOptRepeatRange"u8))));
        _jitDoIfConversion = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoIfConversion"u8))), 1);
        _jitDoOptimizeMaskConversions = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoOptimizeMaskConversions"u8))), 1);
        _jitOptimizeAwait = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitOptimizeAwait"u8))), 1);
        _jitAsyncDefaultValueAnalysisRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAsyncDefaultValueAnalysisRange"u8))));
        _jitAsyncPreservedValueAnalysisRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAsyncPreservedValueAnalysisRange"u8))));
        _jitAsyncReuseContinuationsRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAsyncReuseContinuationsRange"u8))));
#endif
        _jitAsyncReuseContinuations = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAsyncReuseContinuations"u8))), 1);
        _jitEnableOptRepeat = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableOptRepeat"u8))), 1);
        var jitOptRepeatValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitOptRepeat"u8))));
        _jitOptRepeat = new MethodSet(jitOptRepeatValue, jitHost);
        _jitOptRepeatCount = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitOptRepeatCount"u8))), 2);
        _jitVNMapSelBudget = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitVNMapSelBudget"u8))), DEFAULT_MAP_SELECT_BUDGET);
        _tailCallLoopOpt = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("TailCallLoopOpt"u8))), 1);
        var altJitValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("AltJit"u8))));
        _altJit = new MethodSet(altJitValue, jitHost);
        var altJitNgenValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("AltJitNgen"u8))));
        _altJitNgen = new MethodSet(altJitNgenValue, jitHost);
        _altJitExcludeAssemblies = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("AltJitExcludeAssemblies"u8))));
        _jitMeasureIR = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitMeasureIR"u8))), 0);
        _jitReportMetrics = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitReportMetrics"u8))), 0);
        _jitFuncInfoFile = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitFuncInfoLogFile"u8))));
        _jitTimeLogCsv = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitTimeLogCsv"u8))));
        _jitTimeLogFile = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitTimeLogFile"u8))));
        _tailCallOpt = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("TailCallOpt"u8))));
        _fastTailCalls = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("FastTailCalls"u8))), 1);
        _jitMeasureNowayAssert = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitMeasureNowayAssert"u8))), 0);
        _jitMeasureNowayAssertFile = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitMeasureNowayAssertFile"u8))));
#if DEBUG
        _enableExtraSuperPmiQueries = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableExtraSuperPmiQueries"u8))), 0);
        _jitInlineDumpData = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineDumpData"u8))), 0);
        _jitInlineDumpXml = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineDumpXml"u8))), 0);
        _jitInlineDumpXmlFile = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineDumpXmlFile"u8))));
        _jitInlinePolicyDumpXml = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlinePolicyDumpXml"u8))), 0);
        _jitInlineLimit = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineLimit"u8))), -1);
        _jitInlinePolicyDiscretionary = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlinePolicyDiscretionary"u8))), 0);
        _jitInlinePolicyFull = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlinePolicyFull"u8))), 0);
        _jitInlinePolicySize = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlinePolicySize"u8))), 0);
        _jitInlinePolicyRandom = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlinePolicyRandom"u8))), 0);
        _jitInlinePolicyReplay = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlinePolicyReplay"u8))), 0);
        _jitNoInlineRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoInlineRange"u8))));
        _jitInlineReplayFile = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineReplayFile"u8))));
#endif
        _jitExtDefaultPolicy = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitExtDefaultPolicy"u8))), 1);
        _jitExtDefaultPolicyMaxIL = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitExtDefaultPolicyMaxIL"u8))), 0x80);
        _jitExtDefaultPolicyMaxILRoot = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitExtDefaultPolicyMaxILRoot"u8))), 0x100);
        _jitExtDefaultPolicyMaxILProf = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitExtDefaultPolicyMaxILProf"u8))), 0x400);
        _jitExtDefaultPolicyMaxBB = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitExtDefaultPolicyMaxBB"u8))), 7);
        _jitExtDefaultPolicyProfTrust = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitExtDefaultPolicyProfTrust"u8))), 0x7);
        _jitExtDefaultPolicyProfScale = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitExtDefaultPolicyProfScale"u8))), 0x2A);
        _jitInlinePolicyModel = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlinePolicyModel"u8))), 0);
        _jitInlinePolicyProfile = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlinePolicyProfile"u8))), 0);
        _jitInlinePolicyProfileThreshold = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlinePolicyProfileThreshold"u8))), 40);
#if DEBUG
        _jitObjectStackAllocationRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationRange"u8))));
#endif
        _jitObjectStackAllocation = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocation"u8))), 1);
        _jitObjectStackAllocationRefClass = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationRefClass"u8))), 1);
        _jitObjectStackAllocationBoxedValueClass = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationBoxedValueClass"u8))), 1);
        _jitObjectStackAllocationConditionalEscape = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationConditionalEscape"u8))), 1);
#if DEBUG
        _jitObjectStackAllocationConditionalEscapeRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationConditionalEscapeRange"u8))));
#endif
        _jitObjectStackAllocationArray = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationArray"u8))), 1);
        _jitObjectStackAllocationSize = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationSize"u8))), 528);
        _jitObjectStackAllocationTrackFields = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationTrackFields"u8))), 1);
#if DEBUG
        _jitObjectStackAllocationTrackFieldsRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationTrackFieldsRange"u8))));
        _jitObjectStackAllocationDumpConnGraph = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationDumpConnGraph"u8))), 0);
#endif
        _jitEECallTimingInfo = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEECallTimingInfo"u8))), 0);
#if DEBUG
        _jitEnableFinallyCloning = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableFinallyCloning"u8))), 1);
        _jitEnableRemoveEmptyTry = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableRemoveEmptyTry"u8))), 1);
        _jitEnableRemoveEmptyTryCatchOrTryFault = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableRemoveEmptyTryCatchOrTryFault"u8))), 1);
#endif
        _jitEnableGuardedDevirtualization = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableGuardedDevirtualization"u8))), 1);
        _jitGuardedDevirtualizationMaxTypeChecks = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitGuardedDevirtualizationMaxTypeChecks"u8))), -1);
        _jitGuardedDevirtualizationChainLikelihood = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitGuardedDevirtualizationChainLikelihood"u8))), 0x4B);
        _jitGuardedDevirtualizationChainStatements = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitGuardedDevirtualizationChainStatements"u8))), 1);
#if DEBUG
        _jitGuardedDevirtualizationRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitGuardedDevirtualizationRange"u8))));
        _jitRandomGuardedDevirtualization = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRandomGuardedDevirtualization"u8))), 0);
#endif
#if FEATURE_ON_STACK_REPLACEMENT
        _tC_OnStackReplacement = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("TC_OnStackReplacement"u8))), 1);
#else
        _tC_OnStackReplacement = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("TC_OnStackReplacement"u8))), 0);
#endif
        _tC_OnStackReplacement_InitialCounter = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("TC_OnStackReplacement_InitialCounter"u8))), 1000);
        _tC_PartialCompilation = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("TC_PartialCompilation"u8))), 0);
#if DEBUG
        _jitRandomPartialCompilation = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRandomPartialCompilation"u8))), 0);
#endif
        _tC_PatchpointStrategy = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("TC_PatchpointStrategy"u8))), 2);
#if DEBUG
        _jitRandomOnStackReplacement = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRandomOnStackReplacement"u8))), 0);
        _jitOffsetOnStackReplacement = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitOffsetOnStackReplacement"u8))), -1);
        _jitEnableOsrRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableOsrRange"u8))));
        _jitEnablePatchpointRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnablePatchpointRange"u8))));
#endif
        _jitInterlockedProfiling = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInterlockedProfiling"u8))), 0);
        _jitScalableProfiling = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitScalableProfiling"u8))), 1);
        _jitCounterPadding = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCounterPadding"u8))), 0);
        _jitMinimalJitProfiling = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitMinimalJitProfiling"u8))), 1);
        _jitMinimalPrejitProfiling = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitMinimalPrejitProfiling"u8))), 0);
        _jitProfileValues = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitProfileValues"u8))), 1);
        _jitProfileCasts = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitProfileCasts"u8))), 1);
        _jitConsumeProfileForCasts = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitConsumeProfileForCasts"u8))), 1);
        _jitClassProfiling = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitClassProfiling"u8))), 1);
        _jitDelegateProfiling = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDelegateProfiling"u8))), 1);
        _jitVTableProfiling = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitVTableProfiling"u8))), 0);
        _jitEdgeProfiling = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEdgeProfiling"u8))), 1);
        _jitCollect64BitCounts = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCollect64BitCounts"u8))), 0);
#if DEBUG
        _jitInstrumentIfOptimizing = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInstrumentIfOptimizing"u8))), 0);
        _jitInstrumentIfOptimizingRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInstrumentIfOptimizingRange"u8))));
#endif
        _jitInstrumentInlinees = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInstrumentInlinees"u8))), 1);
        _jitDisablePGO = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisablePGO"u8))), 0);
#if DEBUG
        _jitEnablePGORange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnablePGORange"u8))));
        _jitRandomEdgeCounts = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRandomEdgeCounts"u8))), 0);
        _jitCrossCheckDevirtualizationAndPGO = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCrossCheckDevirtualizationAndPGO"u8))), 0);
        _jitNoteFailedExactDevirtualization = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoteFailedExactDevirtualization"u8))), 0);
        _jitRandomlyCollect64BitCounts = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRandomlyCollect64BitCounts"u8))), 0);
        _jitSynthesizeCounts = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitSynthesizeCounts"u8))), 0);
        _jitPropagateSynthesizedCountsToProfileData = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitPropagateSynthesizedCountsToProfileData"u8))), 0);
        _jitSynthesisUseSolver = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitSynthesisUseSolver"u8))), 1);
        _jitSynthesisExceptionWeight = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitSynthesisExceptionWeight"u8))));
#endif
        _jitEnableExactDevirtualization = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableExactDevirtualization"u8))), 1);
        _jitForceControlFlowGuard = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitForceControlFlowGuard"u8))), 0);
        _jitCFGUseDispatcher = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCFGUseDispatcher"u8))), 2);
        _jitEnableHeadTailMerge = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableHeadTailMerge"u8))), 1);
        _jitEnablePhysicalPromotion = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnablePhysicalPromotion"u8))), 1);
        _jitEnableCrossBlockLocalAssertionProp = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableCrossBlockLocalAssertionProp"u8))), 1);
        _jitEnablePostorderLocalAssertionProp = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnablePostorderLocalAssertionProp"u8))), 1);
        _jitEnableStrengthReduction = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableStrengthReduction"u8))), 1);
        _jitEnableInductionVariableOpts = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableInductionVariableOpts"u8))), 1);
#if DEBUG
        _jitFunctionFile = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitFunctionFile"u8))));
        var jitRawHexCodeValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRawHexCode"u8))));
        _jitRawHexCode = new MethodSet(jitRawHexCodeValue, jitHost);
        _jitRawHexCodeFile = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRawHexCodeFile"u8))));
#if TARGET_ARM64
        _jitSaveFpLrWithCalleeSavedRegisters = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitSaveFpLrWithCalleeSavedRegisters"u8))), 0);
        _jitUseScalableVectorT = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitUseScalableVectorT"u8))), 0);
#endif
#if TARGET_LOONGARCH64
        _jitDispIns = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDispIns"u8))), 0);
#endif
#endif
#if TARGET_WASM
        _jitWasmNyiToR2RUnsupported = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitWasmNyiToR2RUnsupported"u8))), 0);
#if DEBUG
        _jitR2RUnsupportedRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitR2RUnsupportedRange"u8))));
#endif
        _jitWasmFunclets = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitWasmFunclets"u8))), 1);
#endif
        _jitEnregStructLocals = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnregStructLocals"u8))), 1);
        _isInitialized = true;
    }
}