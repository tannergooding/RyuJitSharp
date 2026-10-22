// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public partial struct JitConfigValues
{
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
    private int _jitCloneLoopsSizeLimit;
    private int _jitCloneLoopsMinPerCallRatio;
    private int _jitDebugLogLoopCloning;
    private int _jitAlignLoopMinBlockWeight;
    private int _jitAlignLoopMaxCodeSize;
    private int _jitAlignLoopBoundary;
    private int _jitAlignLoopForJcc;
    private int _jitAlignLoopAdaptive;
    private int _jitHideAlignBehindJmp;
    private int _jitOptimizeStructHiddenBuffer;
    private int _jitEnableStoreLclFldCoalescing;
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
    private int _jitInlineBudget;
    private int _jitForceInlineDepth;
    private int _jitInlineMethodsWithEH;
    private unsafe byte* _jitInlineMethodsWithEHRange;
    private unsafe byte* _jitOptimizeAwaitRange;
    private int _jitAwaitHashBreak;
    private int _jitLongAddress;
    private int _jitMaxUncheckedOffset;
    private int _jitPacEnabled;
    private int _jitEnableGenericVirtualDevirtualization;
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
    private int _jitEnablePhaseChecks;
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
    private MethodSet _jitDisasm;
    private int _jitDisasmTesting;
    private int _jitDisasmDiffable;
    private int _jitDisasmSummary;
    private int _jitDisasmOnlyOptimized;
    private int _jitDisasmWithAlignmentBoundaries;
    private int _jitDisasmWithCodeBytes;
    private unsafe byte* _jitDisasmAssemblies;
    private int _jitDisasmWithGC;
    private int _jitDisasmWithDebugInfo;
    private int _jitDisasmSpilled;
    private int _jitDisasmWithAddress;
    private unsafe byte* _jitStdOutFile;
    private int _richDebugInfo;
    private unsafe byte* _writeRichDebugInfoFile;
    private int _jitAlignLoops;
    private int _altJitAssertOnNYI;
    private int _enableEHWriteThru;
    private int _enableMultiRegLocals;
    private int _jitNoInline;
    private int _jitStressRex2Encoding;
    private int _jitStressPromotedEvexEncoding;
    private int _jitStressEvexEncoding;
    private int _enableHWIntrinsic;
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
    private int _enableArm64Aes;
    private int _enableArm64Atomics;
    private int _enableArm64Crc32;
    private int _enableArm64Cssc;
    private int _enableArm64Dczva;
    private int _enableArm64Dp;
    private int _enableArm64Fp16;
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
    private int _enableRiscV64Zba;
    private int _enableRiscV64Zbb;
    private int _enableRiscV64Zbs;
    private int _enableRiscV64Zicond;
    private int _enableEmbeddedBroadcast;
    private int _enableEmbeddedMasking;
    private int _enableApxNDD;
    private int _enableApxConditionalChaining;
    private int _enableApxPPHint;
    private int _enableApxPP2;
    private int _enableApxZU;
    private int _jitDisableSimdVN;
    private int _jitConstCSE;
    private int _jitRLCSEGreedy;
    private int _jitRLCSEVerbose;
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
    private int _jitEnableNoWayAssert;
    private int _displayMemStats;
    private int _jitEnregStats;
    private int _jitAggressiveInlining;
    private int _jitELTHookEnabled;
    private int _jitInlineSIMDMultiplier;
    private int _jitMaxLocalsToTrack;
    private int _jitDoAssertionProp;
    private int _jitDoCopyProp;
    private int _jitDoOptimizeIVs;
    private int _jitDoEarlyProp;
    private int _jitDoLoopHoisting;
    private int _jitDoLoopInversion;
    private int _jitLoopInversionSizeLimit;
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
    private int _jitAsyncInlining;
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
    private int _jitStressAsyncInlining;
    private int _jitStressAsyncInliningMaxDepth;
    private int _jitStressAsyncInliningPct;
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
    private unsafe byte* _jitObjectStackAllocationRange;
    private int _jitObjectStackAllocation;
    private int _jitObjectStackAllocationRefClass;
    private int _jitObjectStackAllocationBoxedValueClass;
    private int _jitObjectStackAllocationConditionalEscape;
    private unsafe byte* _jitObjectStackAllocationConditionalEscapeRange;
    private int _jitObjectStackAllocationArray;
    private int _jitObjectStackAllocationSize;
    private int _jitObjectStackAllocationTrackFields;
    private unsafe byte* _jitObjectStackAllocationTrackFieldsRange;
    private int _jitObjectStackAllocationDumpConnGraph;
    private int _jitEECallTimingInfo;
    private int _jitEnableFinallyCloning;
    private int _jitEnableRemoveEmptyTry;
    private int _jitEnableRemoveEmptyTryCatchOrTryFault;
    private int _jitEnableRemoveUnreachableTry;
    private int _jitEnableGuardedDevirtualization;
    private int _jitGuardedDevirtualizationMaxTypeChecks;
    private int _jitGuardedDevirtualizationRequireInlining;
    private int _jitGuardedDevirtualizationChainLikelihood;
    private int _jitGuardedDevirtualizationChainStatements;
    private unsafe byte* _jitGuardedDevirtualizationRange;
    private int _jitRandomGuardedDevirtualization;
    private int _tC_OnStackReplacement;
    private int _tC_OnStackReplacement_InitialCounter;
    private int _tC_PartialCompilation;
    private int _jitRandomPartialCompilation;
    private int _tC_PatchpointStrategy;
    private int _jitRandomOnStackReplacement;
    private int _jitOffsetOnStackReplacement;
    private unsafe byte* _jitEnableOsrRange;
    private unsafe byte* _jitEnablePatchpointRange;
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
    private int _jitInstrumentIfOptimizing;
    private unsafe byte* _jitInstrumentIfOptimizingRange;
    private int _jitInstrumentInlinees;
    private int _jitDisablePGO;
    private unsafe byte* _jitEnablePGORange;
    private int _jitRandomEdgeCounts;
    private int _jitCrossCheckDevirtualizationAndPGO;
    private int _jitNoteFailedExactDevirtualization;
    private int _jitRandomlyCollect64BitCounts;
    private int _jitSynthesizeCounts;
    private int _jitPropagateSynthesizedCountsToProfileData;
    private int _jitSynthesisUseSolver;
    private unsafe byte* _jitSynthesisExceptionWeight;
    private int _jitEnableExactDevirtualization;
    private int _jitForceControlFlowGuard;
    private int _jitCFGUseDispatcher;
    private int _jitEnableHeadTailMerge;
    private int _jitEnablePhysicalPromotion;
    private int _jitEnableCrossBlockLocalAssertionProp;
    private int _jitEnablePostorderLocalAssertionProp;
    private int _jitEnableStrengthReduction;
    private int _jitEnableInductionVariableOpts;
    private unsafe byte* _jitFunctionFile;
    private MethodSet _jitRawHexCode;
    private unsafe byte* _jitRawHexCodeFile;
    private int _jitSaveFpLrWithCalleeSavedRegisters;
    private int _jitUseScalableVectorT;
    private int _jitDispIns;
    private unsafe byte* _jitR2RUnsupportedRange;
    private int _jitWasmFunclets;
    private int _jitEnregStructLocals;

    public int AltJitLimit
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "AltJitLimit" : default);
                return default!;
            }
            return _altJitLimit;
        }
    }
    public int AltJitSkipOnAssert
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "AltJitSkipOnAssert" : default);
                return default!;
            }
            return _altJitSkipOnAssert;
        }
    }
    public int BreakOnDumpToken
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "BreakOnDumpToken" : default);
                return default!;
            }
            return _breakOnDumpToken;
        }
    }
    public int DebugBreakOnVerificationFailure
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "DebugBreakOnVerificationFailure" : default);
                return default!;
            }
            return _debugBreakOnVerificationFailure;
        }
    }
    public int DisplayLoopHoistStats
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "DisplayLoopHoistStats" : default);
                return default!;
            }
            return _displayLoopHoistStats;
        }
    }
    public int DisplayLsraStats
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "DisplayLsraStats" : default);
                return default!;
            }
            return _displayLsraStats;
        }
    }
    public unsafe byte* JitLsraOrdering
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitLsraOrdering" : default);
                return default!;
            }
            return _jitLsraOrdering;
        }
    }
    public int EnablePCRelAddr
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "EnablePCRelAddr" : default);
                return default!;
            }
            return _enablePCRelAddr;
        }
    }
    public int JitAssertOnMaxRAPasses
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitAssertOnMaxRAPasses" : default);
                return default!;
            }
            return _jitAssertOnMaxRAPasses;
        }
    }
    public int JitBreakEmitOutputInstr
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitBreakEmitOutputInstr" : default);
                return default!;
            }
            return _jitBreakEmitOutputInstr;
        }
    }
    public int JitBreakMorphTree
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitBreakMorphTree" : default);
                return default!;
            }
            return _jitBreakMorphTree;
        }
    }
    public int JitBreakOnBadCode
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitBreakOnBadCode" : default);
                return default!;
            }
            return _jitBreakOnBadCode;
        }
    }
    public int JitBreakOnMinOpts
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitBreakOnMinOpts" : default);
                return default!;
            }
            return _jitBreakOnMinOpts;
        }
    }
    public int JitCloneLoops
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitCloneLoops" : default);
                return default!;
            }
            return _jitCloneLoops;
        }
    }
    public int JitCloneLoopsWithEH
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitCloneLoopsWithEH" : default);
                return default!;
            }
            return _jitCloneLoopsWithEH;
        }
    }
    public int JitCloneLoopsWithGdvTests
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitCloneLoopsWithGdvTests" : default);
                return default!;
            }
            return _jitCloneLoopsWithGdvTests;
        }
    }
    public int JitCloneLoopsSizeLimit => _jitCloneLoopsSizeLimit;
    public int JitCloneLoopsMinPerCallRatio => _jitCloneLoopsMinPerCallRatio;
    public int JitDebugLogLoopCloning
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDebugLogLoopCloning" : default);
                return default!;
            }
            return _jitDebugLogLoopCloning;
        }
    }
    public int JitAlignLoopMinBlockWeight
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitAlignLoopMinBlockWeight" : default);
                return default!;
            }
            return _jitAlignLoopMinBlockWeight;
        }
    }
    public int JitAlignLoopMaxCodeSize
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitAlignLoopMaxCodeSize" : default);
                return default!;
            }
            return _jitAlignLoopMaxCodeSize;
        }
    }
    public int JitAlignLoopBoundary
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitAlignLoopBoundary" : default);
                return default!;
            }
            return _jitAlignLoopBoundary;
        }
    }
    public int JitAlignLoopForJcc
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitAlignLoopForJcc" : default);
                return default!;
            }
            return _jitAlignLoopForJcc;
        }
    }
    public int JitAlignLoopAdaptive
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitAlignLoopAdaptive" : default);
                return default!;
            }
            return _jitAlignLoopAdaptive;
        }
    }
    public int JitHideAlignBehindJmp
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitHideAlignBehindJmp" : default);
                return default!;
            }
            return _jitHideAlignBehindJmp;
        }
    }
    public int JitOptimizeStructHiddenBuffer
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitOptimizeStructHiddenBuffer" : default);
                return default!;
            }
            return _jitOptimizeStructHiddenBuffer;
        }
    }
    public int JitEnableStoreLclFldCoalescing => _jitEnableStoreLclFldCoalescing;
    public int JitUnrollLoopMaxIterationCount
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitUnrollLoopMaxIterationCount" : default);
                return default!;
            }
            return _jitUnrollLoopMaxIterationCount;
        }
    }
    public int JitUnrollLoopsWithEH
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitUnrollLoopsWithEH" : default);
                return default!;
            }
            return _jitUnrollLoopsWithEH;
        }
    }
    public int JitDirectAlloc
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDirectAlloc" : default);
                return default!;
            }
            return _jitDirectAlloc;
        }
    }
    public int JitDoubleAlign
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDoubleAlign" : default);
                return default!;
            }
            return _jitDoubleAlign;
        }
    }
    public int JitEmitPrintRefRegs
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEmitPrintRefRegs" : default);
                return default!;
            }
            return _jitEmitPrintRefRegs;
        }
    }
    public int JitEnableDevirtualization
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnableDevirtualization" : default);
                return default!;
            }
            return _jitEnableDevirtualization;
        }
    }
    public int JitEnableLateDevirtualization
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnableLateDevirtualization" : default);
                return default!;
            }
            return _jitEnableLateDevirtualization;
        }
    }
    public int JitExpensiveDebugCheckLevel
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitExpensiveDebugCheckLevel" : default);
                return default!;
            }
            return _jitExpensiveDebugCheckLevel;
        }
    }
    public int JitForceFallback
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitForceFallback" : default);
                return default!;
            }
            return _jitForceFallback;
        }
    }
    public int JitFullyInt
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitFullyInt" : default);
                return default!;
            }
            return _jitFullyInt;
        }
    }
    public int JitFunctionTrace
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitFunctionTrace" : default);
                return default!;
            }
            return _jitFunctionTrace;
        }
    }
    public int JitGCChecks
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitGCChecks" : default);
                return default!;
            }
            return _jitGCChecks;
        }
    }
    public int JitGCInfoLogging
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitGCInfoLogging" : default);
                return default!;
            }
            return _jitGCInfoLogging;
        }
    }
    public int JitHashBreak
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitHashBreak" : default);
                return default!;
            }
            return _jitHashBreak;
        }
    }
    public int JitHashHalt
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitHashHalt" : default);
                return default!;
            }
            return _jitHashHalt;
        }
    }
    public int JitInlineAdditionalMultiplier
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlineAdditionalMultiplier" : default);
                return default!;
            }
            return _jitInlineAdditionalMultiplier;
        }
    }
    public int JitInlinePrintStats
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlinePrintStats" : default);
                return default!;
            }
            return _jitInlinePrintStats;
        }
    }
    public int JitInlineSize
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlineSize" : default);
                return default!;
            }
            return _jitInlineSize;
        }
    }
    public int JitInlineDepth
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlineDepth" : default);
                return default!;
            }
            return _jitInlineDepth;
        }
    }
    public int JitInlineBudget => _jitInlineBudget;
    public int JitForceInlineDepth
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitForceInlineDepth" : default);
                return default!;
            }
            return _jitForceInlineDepth;
        }
    }
    public int JitInlineMethodsWithEH => _jitInlineMethodsWithEH;
    public unsafe byte* JitInlineMethodsWithEHRange
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlineMethodsWithEHRange" : default);
                return default!;
            }
            return _jitInlineMethodsWithEHRange;
        }
    }
    public unsafe byte* JitOptimizeAwaitRange
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitOptimizeAwaitRange" : default);
                return default!;
            }
            return _jitOptimizeAwaitRange;
        }
    }
    public int JitAwaitHashBreak
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitAwaitHashBreak" : default);
                return default!;
            }
            return _jitAwaitHashBreak;
        }
    }
    public int JitLongAddress
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitLongAddress" : default);
                return default!;
            }
            return _jitLongAddress;
        }
    }
    public int JitMaxUncheckedOffset
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitMaxUncheckedOffset" : default);
                return default!;
            }
            return _jitMaxUncheckedOffset;
        }
    }
    public int JitPacEnabled => _jitPacEnabled;
    public int JitEnableGenericVirtualDevirtualization => _jitEnableGenericVirtualDevirtualization;
    public int JitMinOpts
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitMinOpts" : default);
                return default!;
            }
            return _jitMinOpts;
        }
    }
    public MethodSet JitMinOptsName
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitMinOptsName" : default);
                return default!;
            }
            return _jitMinOptsName;
        }
    }
    public int JitMinOptsBbCount
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitMinOptsBbCount" : default);
                return default!;
            }
            return _jitMinOptsBbCount;
        }
    }
    public int JitMinOptsCodeSize
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitMinOptsCodeSize" : default);
                return default!;
            }
            return _jitMinOptsCodeSize;
        }
    }
    public int JitMinOptsInstrCount
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitMinOptsInstrCount" : default);
                return default!;
            }
            return _jitMinOptsInstrCount;
        }
    }
    public int JitMinOptsLvNumCount
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitMinOptsLvNumCount" : default);
                return default!;
            }
            return _jitMinOptsLvNumCount;
        }
    }
    public int JitMinOptsLvRefCount
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitMinOptsLvRefCount" : default);
                return default!;
            }
            return _jitMinOptsLvRefCount;
        }
    }
    public int JitNoCSE
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitNoCSE" : default);
                return default!;
            }
            return _jitNoCSE;
        }
    }
    public int JitNoCSE2
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitNoCSE2" : default);
                return default!;
            }
            return _jitNoCSE2;
        }
    }
    public int JitNoForceFallback
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitNoForceFallback" : default);
                return default!;
            }
            return _jitNoForceFallback;
        }
    }
    public int JitNoForwardSub
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitNoForwardSub" : default);
                return default!;
            }
            return _jitNoForwardSub;
        }
    }
    public int JitNoHoist
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitNoHoist" : default);
                return default!;
            }
            return _jitNoHoist;
        }
    }
    public int JitNoMemoryBarriers
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitNoMemoryBarriers" : default);
                return default!;
            }
            return _jitNoMemoryBarriers;
        }
    }
    public int JitNoStructPromotion
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitNoStructPromotion" : default);
                return default!;
            }
            return _jitNoStructPromotion;
        }
    }
    public int JitNoUnroll
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitNoUnroll" : default);
                return default!;
            }
            return _jitNoUnroll;
        }
    }
    public int JitOrder
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitOrder" : default);
                return default!;
            }
            return _jitOrder;
        }
    }
    public int JitQueryCurrentStaticFieldClass
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitQueryCurrentStaticFieldClass" : default);
                return default!;
            }
            return _jitQueryCurrentStaticFieldClass;
        }
    }
    public int JitReportFastTailCallDecisions
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitReportFastTailCallDecisions" : default);
                return default!;
            }
            return _jitReportFastTailCallDecisions;
        }
    }
    public int JitPInvokeCheckEnabled
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitPInvokeCheckEnabled" : default);
                return default!;
            }
            return _jitPInvokeCheckEnabled;
        }
    }
    public int JitPInvokeEnabled
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitPInvokeEnabled" : default);
                return default!;
            }
            return _jitPInvokeEnabled;
        }
    }
    public int JitHoistLimit
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitHoistLimit" : default);
                return default!;
            }
            return _jitHoistLimit;
        }
    }
    public int JitPrintInlinedMethodsVerbose
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitPrintInlinedMethodsVerbose" : default);
                return default!;
            }
            return _jitPrintInlinedMethodsVerbose;
        }
    }
    public MethodSet JitPrintInlinedMethods
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitPrintInlinedMethods" : default);
                return default!;
            }
            return _jitPrintInlinedMethods;
        }
    }
    public MethodSet JitPrintDevirtualizedMethods
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitPrintDevirtualizedMethods" : default);
                return default!;
            }
            return _jitPrintDevirtualizedMethods;
        }
    }
    public int JitProfileChecks
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitProfileChecks" : default);
                return default!;
            }
            return _jitProfileChecks;
        }
    }
    public int JitRequired
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRequired" : default);
                return default!;
            }
            return _jitRequired;
        }
    }
    public int JitStackAllocToLocalSize
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStackAllocToLocalSize" : default);
                return default!;
            }
            return _jitStackAllocToLocalSize;
        }
    }
    public int JitSkipArrayBoundCheck
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitSkipArrayBoundCheck" : default);
                return default!;
            }
            return _jitSkipArrayBoundCheck;
        }
    }
    public int JitEnablePhaseChecks
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnablePhaseChecks" : default);
                return default!;
            }
            return _jitEnablePhaseChecks;
        }
    }
    public int JitSplitFunctionSize
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitSplitFunctionSize" : default);
                return default!;
            }
            return _jitSplitFunctionSize;
        }
    }
    public int JitSsaStress
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitSsaStress" : default);
                return default!;
            }
            return _jitSsaStress;
        }
    }
    public int JitStackChecks
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStackChecks" : default);
                return default!;
            }
            return _jitStackChecks;
        }
    }
    public int JitStress
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStress" : default);
                return default!;
            }
            return _jitStress;
        }
    }
    public int JitStressBBProf
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressBBProf" : default);
                return default!;
            }
            return _jitStressBBProf;
        }
    }
    public int JitStressProcedureSplitting
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressProcedureSplitting" : default);
                return default!;
            }
            return _jitStressProcedureSplitting;
        }
    }
    public int JitStressRegs
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressRegs" : default);
                return default!;
            }
            return _jitStressRegs;
        }
    }
    public unsafe byte* JitStressRegsRange
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressRegsRange" : default);
                return default!;
            }
            return _jitStressRegsRange;
        }
    }
    public int JitStressSplitTreeLimit
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressSplitTreeLimit" : default);
                return default!;
            }
            return _jitStressSplitTreeLimit;
        }
    }
    public int JitVNMapSelLimit
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitVNMapSelLimit" : default);
                return default!;
            }
            return _jitVNMapSelLimit;
        }
    }
    public int RunAltJitCode
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "RunAltJitCode" : default);
                return default!;
            }
            return _runAltJitCode;
        }
    }
    public int RunComponentUnitTests
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "RunComponentUnitTests" : default);
                return default!;
            }
            return _runComponentUnitTests;
        }
    }
    public int ShouldInjectFault
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "ShouldInjectFault" : default);
                return default!;
            }
            return _shouldInjectFault;
        }
    }
    public int TailcallStress
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "TailcallStress" : default);
                return default!;
            }
            return _tailcallStress;
        }
    }
    public MethodSet JitBreak
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitBreak" : default);
                return default!;
            }
            return _jitBreak;
        }
    }
    public MethodSet JitDebugBreak
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDebugBreak" : default);
                return default!;
            }
            return _jitDebugBreak;
        }
    }
    public MethodSet JitDump
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDump" : default);
                return default!;
            }
            return _jitDump;
        }
    }
    public int JitHashDump
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitHashDump" : default);
                return default!;
            }
            return _jitHashDump;
        }
    }
    public int JitDumpTier0
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpTier0" : default);
                return default!;
            }
            return _jitDumpTier0;
        }
    }
    public int JitDumpOSR
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpOSR" : default);
                return default!;
            }
            return _jitDumpOSR;
        }
    }
    public int JitDumpAtOSROffset
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpAtOSROffset" : default);
                return default!;
            }
            return _jitDumpAtOSROffset;
        }
    }
    public int JitDumpInlinePhases
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpInlinePhases" : default);
                return default!;
            }
            return _jitDumpInlinePhases;
        }
    }
    public int JitDumpASCII
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpASCII" : default);
                return default!;
            }
            return _jitDumpASCII;
        }
    }
    public int JitDumpTerseLsra
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpTerseLsra" : default);
                return default!;
            }
            return _jitDumpTerseLsra;
        }
    }
    public int JitDumpToDebugger
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpToDebugger" : default);
                return default!;
            }
            return _jitDumpToDebugger;
        }
    }
    public int JitDumpVerboseSsa
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpVerboseSsa" : default);
                return default!;
            }
            return _jitDumpVerboseSsa;
        }
    }
    public int JitDumpVerboseTrees
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpVerboseTrees" : default);
                return default!;
            }
            return _jitDumpVerboseTrees;
        }
    }
    public int JitDumpTreeIDs
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpTreeIDs" : default);
                return default!;
            }
            return _jitDumpTreeIDs;
        }
    }
    public int JitDumpBeforeAfterMorph
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpBeforeAfterMorph" : default);
                return default!;
            }
            return _jitDumpBeforeAfterMorph;
        }
    }
    public int JitDumpTerseNextBlock
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpTerseNextBlock" : default);
                return default!;
            }
            return _jitDumpTerseNextBlock;
        }
    }
    public MethodSet JitEHDump
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEHDump" : default);
                return default!;
            }
            return _jitEHDump;
        }
    }
    public MethodSet JitExclude
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitExclude" : default);
                return default!;
            }
            return _jitExclude;
        }
    }
    public int JitFakeProcedureSplitting
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitFakeProcedureSplitting" : default);
                return default!;
            }
            return _jitFakeProcedureSplitting;
        }
    }
    public MethodSet JitForceProcedureSplitting
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitForceProcedureSplitting" : default);
                return default!;
            }
            return _jitForceProcedureSplitting;
        }
    }
    public MethodSet JitGCDump
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitGCDump" : default);
                return default!;
            }
            return _jitGCDump;
        }
    }
    public MethodSet JitDebugDump
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDebugDump" : default);
                return default!;
            }
            return _jitDebugDump;
        }
    }
    public MethodSet JitHalt
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitHalt" : default);
                return default!;
            }
            return _jitHalt;
        }
    }
    public MethodSet JitInclude
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInclude" : default);
                return default!;
            }
            return _jitInclude;
        }
    }
    public MethodSet JitLateDisasm
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitLateDisasm" : default);
                return default!;
            }
            return _jitLateDisasm;
        }
    }
    public unsafe byte* JitLateDisasmTo
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitLateDisasmTo" : default);
                return default!;
            }
            return _jitLateDisasmTo;
        }
    }
    public MethodSet JitNoProcedureSplitting
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitNoProcedureSplitting" : default);
                return default!;
            }
            return _jitNoProcedureSplitting;
        }
    }
    public MethodSet JitNoProcedureSplittingEH
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitNoProcedureSplittingEH" : default);
                return default!;
            }
            return _jitNoProcedureSplittingEH;
        }
    }
    public MethodSet JitStressOnly
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressOnly" : default);
                return default!;
            }
            return _jitStressOnly;
        }
    }
    public MethodSet JitUnwindDump
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitUnwindDump" : default);
                return default!;
            }
            return _jitUnwindDump;
        }
    }
    public MethodSet JitDumpFg
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFg" : default);
                return default!;
            }
            return _jitDumpFg;
        }
    }
    public int JitDumpFgHash
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgHash" : default);
                return default!;
            }
            return _jitDumpFgHash;
        }
    }
    public int JitDumpFgTier0
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgTier0" : default);
                return default!;
            }
            return _jitDumpFgTier0;
        }
    }
    public unsafe byte* JitDumpFgDir
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgDir" : default);
                return default!;
            }
            return _jitDumpFgDir;
        }
    }
    public unsafe byte* JitDumpFgFile
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgFile" : default);
                return default!;
            }
            return _jitDumpFgFile;
        }
    }
    public unsafe byte* JitDumpFgPhase
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgPhase" : default);
                return default!;
            }
            return _jitDumpFgPhase;
        }
    }
    public unsafe byte* JitDumpFgPrePhase
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgPrePhase" : default);
                return default!;
            }
            return _jitDumpFgPrePhase;
        }
    }
    public int JitDumpFgDot
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgDot" : default);
                return default!;
            }
            return _jitDumpFgDot;
        }
    }
    public int JitDumpFgEH
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgEH" : default);
                return default!;
            }
            return _jitDumpFgEH;
        }
    }
    public int JitDumpFgLoops
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgLoops" : default);
                return default!;
            }
            return _jitDumpFgLoops;
        }
    }
    public int JitDumpFgConstrained
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgConstrained" : default);
                return default!;
            }
            return _jitDumpFgConstrained;
        }
    }
    public int JitDumpFgBlockID
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgBlockID" : default);
                return default!;
            }
            return _jitDumpFgBlockID;
        }
    }
    public int JitDumpFgBlockFlags
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgBlockFlags" : default);
                return default!;
            }
            return _jitDumpFgBlockFlags;
        }
    }
    public int JitDumpFgLoopFlags
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgLoopFlags" : default);
                return default!;
            }
            return _jitDumpFgLoopFlags;
        }
    }
    public int JitDumpFgBlockOrder
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgBlockOrder" : default);
                return default!;
            }
            return _jitDumpFgBlockOrder;
        }
    }
    public int JitDumpFgMemorySsa
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgMemorySsa" : default);
                return default!;
            }
            return _jitDumpFgMemorySsa;
        }
    }
    public unsafe byte* JitRange
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRange" : default);
                return default!;
            }
            return _jitRange;
        }
    }
    public unsafe byte* JitStressModeNames
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressModeNames" : default);
                return default!;
            }
            return _jitStressModeNames;
        }
    }
    public int JitStressModeNamesOnly
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressModeNamesOnly" : default);
                return default!;
            }
            return _jitStressModeNamesOnly;
        }
    }
    public unsafe byte* JitStressModeNamesAllow
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressModeNamesAllow" : default);
                return default!;
            }
            return _jitStressModeNamesAllow;
        }
    }
    public unsafe byte* JitStressModeNamesNot
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressModeNamesNot" : default);
                return default!;
            }
            return _jitStressModeNamesNot;
        }
    }
    public unsafe byte* JitStressRange
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressRange" : default);
                return default!;
            }
            return _jitStressRange;
        }
    }
    public MethodSet JitEmitUnitTests
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEmitUnitTests" : default);
                return default!;
            }
            return _jitEmitUnitTests;
        }
    }
    public unsafe byte* JitEmitUnitTestsSections
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEmitUnitTestsSections" : default);
                return default!;
            }
            return _jitEmitUnitTestsSections;
        }
    }
    public MethodSet JitDisasm => _jitDisasm;
    public int JitDisasmTesting => _jitDisasmTesting;
    public int JitDisasmDiffable => _jitDisasmDiffable;
    public int JitDisasmSummary => _jitDisasmSummary;
    public int JitDisasmOnlyOptimized => _jitDisasmOnlyOptimized;
    public int JitDisasmWithAlignmentBoundaries => _jitDisasmWithAlignmentBoundaries;
    public int JitDisasmWithCodeBytes => _jitDisasmWithCodeBytes;
    public unsafe byte* JitDisasmAssemblies
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDisasmAssemblies" : default);
                return default!;
            }
            return _jitDisasmAssemblies;
        }
    }
    public int JitDisasmWithGC
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDisasmWithGC" : default);
                return default!;
            }
            return _jitDisasmWithGC;
        }
    }
    public int JitDisasmWithDebugInfo
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDisasmWithDebugInfo" : default);
                return default!;
            }
            return _jitDisasmWithDebugInfo;
        }
    }
    public int JitDisasmSpilled
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDisasmSpilled" : default);
                return default!;
            }
            return _jitDisasmSpilled;
        }
    }
    public int JitDisasmWithAddress
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDisasmWithAddress" : default);
                return default!;
            }
            return _jitDisasmWithAddress;
        }
    }
    public unsafe byte* JitStdOutFile => _jitStdOutFile;
    public int RichDebugInfo => _richDebugInfo;
    public unsafe byte* WriteRichDebugInfoFile
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "WriteRichDebugInfoFile" : default);
                return default!;
            }
            return _writeRichDebugInfoFile;
        }
    }
    public int JitAlignLoops => _jitAlignLoops;
    public int AltJitAssertOnNYI => _altJitAssertOnNYI;
    public int EnableEHWriteThru => _enableEHWriteThru;
    public int EnableMultiRegLocals => _enableMultiRegLocals;
    public int JitNoInline => _jitNoInline;
    public int JitStressRex2Encoding
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressRex2Encoding" : default);
                return default!;
            }
            return _jitStressRex2Encoding;
        }
    }
    public int JitStressPromotedEvexEncoding
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressPromotedEvexEncoding" : default);
                return default!;
            }
            return _jitStressPromotedEvexEncoding;
        }
    }
    public int JitStressEvexEncoding
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressEvexEncoding" : default);
                return default!;
            }
            return _jitStressEvexEncoding;
        }
    }
    public int EnableHWIntrinsic => _enableHWIntrinsic;
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
    public int EnableArm64Aes => _enableArm64Aes;
    public int EnableArm64Atomics => _enableArm64Atomics;
    public int EnableArm64Crc32 => _enableArm64Crc32;
    public int EnableArm64Cssc => _enableArm64Cssc;
    public int EnableArm64Dczva => _enableArm64Dczva;
    public int EnableArm64Dp => _enableArm64Dp;
    public int EnableArm64Fp16 => _enableArm64Fp16;
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
    public int EnableRiscV64Zba => _enableRiscV64Zba;
    public int EnableRiscV64Zbb => _enableRiscV64Zbb;
    public int EnableRiscV64Zbs => _enableRiscV64Zbs;
    public int EnableRiscV64Zicond => _enableRiscV64Zicond;
    public int EnableEmbeddedBroadcast => _enableEmbeddedBroadcast;
    public int EnableEmbeddedMasking => _enableEmbeddedMasking;
    public int EnableApxNDD => _enableApxNDD;
    public int EnableApxConditionalChaining => _enableApxConditionalChaining;
    public int EnableApxPPHint => _enableApxPPHint;
    public int EnableApxPP2 => _enableApxPP2;
    public int EnableApxZU => _enableApxZU;
    public int JitDisableSimdVN => _jitDisableSimdVN;
    public int JitConstCSE => _jitConstCSE;
    public int JitRLCSEGreedy => _jitRLCSEGreedy;
    public int JitRLCSEVerbose => _jitRLCSEVerbose;
    public int JitCSEHash
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitCSEHash" : default);
                return default!;
            }
            return _jitCSEHash;
        }
    }
    public int JitCSEMask
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitCSEMask" : default);
                return default!;
            }
            return _jitCSEMask;
        }
    }
    public int JitMetrics
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitMetrics" : default);
                return default!;
            }
            return _jitMetrics;
        }
    }
    public int JitRandomCSE
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRandomCSE" : default);
                return default!;
            }
            return _jitRandomCSE;
        }
    }
    public unsafe byte* JitReplayCSE
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitReplayCSE" : default);
                return default!;
            }
            return _jitReplayCSE;
        }
    }
    public unsafe byte* JitReplayCSEReward
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitReplayCSEReward" : default);
                return default!;
            }
            return _jitReplayCSEReward;
        }
    }
    public unsafe byte* JitRLCSE
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRLCSE" : default);
                return default!;
            }
            return _jitRLCSE;
        }
    }
    public unsafe byte* JitRLCSEAlpha
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRLCSEAlpha" : default);
                return default!;
            }
            return _jitRLCSEAlpha;
        }
    }
    public int JitRLCSECandidateFeatures
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRLCSECandidateFeatures" : default);
                return default!;
            }
            return _jitRLCSECandidateFeatures;
        }
    }
    public int JitRLHook
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRLHook" : default);
                return default!;
            }
            return _jitRLHook;
        }
    }
    public int JitRLHookEmitFeatureNames
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRLHookEmitFeatureNames" : default);
                return default!;
            }
            return _jitRLHookEmitFeatureNames;
        }
    }
    public unsafe byte* JitRLHookCSEDecisions
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRLHookCSEDecisions" : default);
                return default!;
            }
            return _jitRLHookCSEDecisions;
        }
    }
    public int JitEnableNoWayAssert => _jitEnableNoWayAssert;
    public int DisplayMemStats => _displayMemStats;
    public int JitEnregStats
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnregStats" : default);
                return default!;
            }
            return _jitEnregStats;
        }
    }
    public int JitAggressiveInlining => _jitAggressiveInlining;
    public int JitELTHookEnabled => _jitELTHookEnabled;
    public int JitInlineSIMDMultiplier => _jitInlineSIMDMultiplier;
    public int JitMaxLocalsToTrack => _jitMaxLocalsToTrack;
    public int JitDoAssertionProp
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDoAssertionProp" : default);
                return default!;
            }
            return _jitDoAssertionProp;
        }
    }
    public int JitDoCopyProp
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDoCopyProp" : default);
                return default!;
            }
            return _jitDoCopyProp;
        }
    }
    public int JitDoOptimizeIVs
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDoOptimizeIVs" : default);
                return default!;
            }
            return _jitDoOptimizeIVs;
        }
    }
    public int JitDoEarlyProp
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDoEarlyProp" : default);
                return default!;
            }
            return _jitDoEarlyProp;
        }
    }
    public int JitDoLoopHoisting
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDoLoopHoisting" : default);
                return default!;
            }
            return _jitDoLoopHoisting;
        }
    }
    public int JitDoLoopInversion
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDoLoopInversion" : default);
                return default!;
            }
            return _jitDoLoopInversion;
        }
    }
    public int JitLoopInversionSizeLimit => _jitLoopInversionSizeLimit;
    public int JitDoRangeAnalysis
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDoRangeAnalysis" : default);
                return default!;
            }
            return _jitDoRangeAnalysis;
        }
    }
    public int JitDoVNBasedDeadStoreRemoval
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDoVNBasedDeadStoreRemoval" : default);
                return default!;
            }
            return _jitDoVNBasedDeadStoreRemoval;
        }
    }
    public int JitDoRedundantBranchOpts
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDoRedundantBranchOpts" : default);
                return default!;
            }
            return _jitDoRedundantBranchOpts;
        }
    }
    public unsafe byte* JitEnableRboRange
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnableRboRange" : default);
                return default!;
            }
            return _jitEnableRboRange;
        }
    }
    public unsafe byte* JitEnableHeadTailMergeRange
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnableHeadTailMergeRange" : default);
                return default!;
            }
            return _jitEnableHeadTailMergeRange;
        }
    }
    public unsafe byte* JitEnableVNBasedDeadStoreRemovalRange
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnableVNBasedDeadStoreRemovalRange" : default);
                return default!;
            }
            return _jitEnableVNBasedDeadStoreRemovalRange;
        }
    }
    public unsafe byte* JitEnableEarlyLivenessRange
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnableEarlyLivenessRange" : default);
                return default!;
            }
            return _jitEnableEarlyLivenessRange;
        }
    }
    public unsafe byte* JitOnlyOptimizeRange
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitOnlyOptimizeRange" : default);
                return default!;
            }
            return _jitOnlyOptimizeRange;
        }
    }
    public unsafe byte* JitEnablePhysicalPromotionRange
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnablePhysicalPromotionRange" : default);
                return default!;
            }
            return _jitEnablePhysicalPromotionRange;
        }
    }
    public unsafe byte* JitEnableCrossBlockLocalAssertionPropRange
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnableCrossBlockLocalAssertionPropRange" : default);
                return default!;
            }
            return _jitEnableCrossBlockLocalAssertionPropRange;
        }
    }
    public unsafe byte* JitEnableInductionVariableOptsRange
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnableInductionVariableOptsRange" : default);
                return default!;
            }
            return _jitEnableInductionVariableOptsRange;
        }
    }
    public unsafe byte* JitEnableLocalAddrPropagationRange
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnableLocalAddrPropagationRange" : default);
                return default!;
            }
            return _jitEnableLocalAddrPropagationRange;
        }
    }
    public int JitDoSsa
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDoSsa" : default);
                return default!;
            }
            return _jitDoSsa;
        }
    }
    public int JitDoValueNumber
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDoValueNumber" : default);
                return default!;
            }
            return _jitDoValueNumber;
        }
    }
    public unsafe byte* JitOptRepeatRange
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitOptRepeatRange" : default);
                return default!;
            }
            return _jitOptRepeatRange;
        }
    }
    public int JitDoIfConversion
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDoIfConversion" : default);
                return default!;
            }
            return _jitDoIfConversion;
        }
    }
    public int JitDoOptimizeMaskConversions
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDoOptimizeMaskConversions" : default);
                return default!;
            }
            return _jitDoOptimizeMaskConversions;
        }
    }
    public int JitOptimizeAwait
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitOptimizeAwait" : default);
                return default!;
            }
            return _jitOptimizeAwait;
        }
    }
    public unsafe byte* JitAsyncDefaultValueAnalysisRange
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitAsyncDefaultValueAnalysisRange" : default);
                return default!;
            }
            return _jitAsyncDefaultValueAnalysisRange;
        }
    }
    public unsafe byte* JitAsyncPreservedValueAnalysisRange
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitAsyncPreservedValueAnalysisRange" : default);
                return default!;
            }
            return _jitAsyncPreservedValueAnalysisRange;
        }
    }
    public int JitAsyncInlining => _jitAsyncInlining;
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
    public int EnableExtraSuperPmiQueries
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "EnableExtraSuperPmiQueries" : default);
                return default!;
            }
            return _enableExtraSuperPmiQueries;
        }
    }
    public int JitInlineDumpData
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlineDumpData" : default);
                return default!;
            }
            return _jitInlineDumpData;
        }
    }
    public int JitInlineDumpXml
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlineDumpXml" : default);
                return default!;
            }
            return _jitInlineDumpXml;
        }
    }
    public unsafe byte* JitInlineDumpXmlFile
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlineDumpXmlFile" : default);
                return default!;
            }
            return _jitInlineDumpXmlFile;
        }
    }
    public int JitInlinePolicyDumpXml
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlinePolicyDumpXml" : default);
                return default!;
            }
            return _jitInlinePolicyDumpXml;
        }
    }
    public int JitInlineLimit
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlineLimit" : default);
                return default!;
            }
            return _jitInlineLimit;
        }
    }
    public int JitInlinePolicyDiscretionary
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlinePolicyDiscretionary" : default);
                return default!;
            }
            return _jitInlinePolicyDiscretionary;
        }
    }
    public int JitInlinePolicyFull
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlinePolicyFull" : default);
                return default!;
            }
            return _jitInlinePolicyFull;
        }
    }
    public int JitInlinePolicySize
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlinePolicySize" : default);
                return default!;
            }
            return _jitInlinePolicySize;
        }
    }
    public int JitInlinePolicyRandom
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlinePolicyRandom" : default);
                return default!;
            }
            return _jitInlinePolicyRandom;
        }
    }
    public int JitInlinePolicyReplay
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlinePolicyReplay" : default);
                return default!;
            }
            return _jitInlinePolicyReplay;
        }
    }
    public unsafe byte* JitNoInlineRange
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitNoInlineRange" : default);
                return default!;
            }
            return _jitNoInlineRange;
        }
    }
    public unsafe byte* JitInlineReplayFile
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlineReplayFile" : default);
                return default!;
            }
            return _jitInlineReplayFile;
        }
    }
    public int JitStressAsyncInlining
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressAsyncInlining" : default);
                return default!;
            }
            return _jitStressAsyncInlining;
        }
    }
    public int JitStressAsyncInliningMaxDepth
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressAsyncInliningMaxDepth" : default);
                return default!;
            }
            return _jitStressAsyncInliningMaxDepth;
        }
    }
    public int JitStressAsyncInliningPct
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressAsyncInliningPct" : default);
                return default!;
            }
            return _jitStressAsyncInliningPct;
        }
    }
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
    public unsafe byte* JitObjectStackAllocationRange
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitObjectStackAllocationRange" : default);
                return default!;
            }
            return _jitObjectStackAllocationRange;
        }
    }
    public int JitObjectStackAllocation => _jitObjectStackAllocation;
    public int JitObjectStackAllocationRefClass => _jitObjectStackAllocationRefClass;
    public int JitObjectStackAllocationBoxedValueClass => _jitObjectStackAllocationBoxedValueClass;
    public int JitObjectStackAllocationConditionalEscape => _jitObjectStackAllocationConditionalEscape;
    public unsafe byte* JitObjectStackAllocationConditionalEscapeRange
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitObjectStackAllocationConditionalEscapeRange" : default);
                return default!;
            }
            return _jitObjectStackAllocationConditionalEscapeRange;
        }
    }
    public int JitObjectStackAllocationArray => _jitObjectStackAllocationArray;
    public int JitObjectStackAllocationSize => _jitObjectStackAllocationSize;
    public int JitObjectStackAllocationTrackFields => _jitObjectStackAllocationTrackFields;
    public unsafe byte* JitObjectStackAllocationTrackFieldsRange
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitObjectStackAllocationTrackFieldsRange" : default);
                return default!;
            }
            return _jitObjectStackAllocationTrackFieldsRange;
        }
    }
    public int JitObjectStackAllocationDumpConnGraph
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitObjectStackAllocationDumpConnGraph" : default);
                return default!;
            }
            return _jitObjectStackAllocationDumpConnGraph;
        }
    }
    public int JitEECallTimingInfo => _jitEECallTimingInfo;
    public int JitEnableFinallyCloning
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnableFinallyCloning" : default);
                return default!;
            }
            return _jitEnableFinallyCloning;
        }
    }
    public int JitEnableRemoveEmptyTry
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnableRemoveEmptyTry" : default);
                return default!;
            }
            return _jitEnableRemoveEmptyTry;
        }
    }
    public int JitEnableRemoveEmptyTryCatchOrTryFault
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnableRemoveEmptyTryCatchOrTryFault" : default);
                return default!;
            }
            return _jitEnableRemoveEmptyTryCatchOrTryFault;
        }
    }
    public int JitEnableRemoveUnreachableTry
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnableRemoveUnreachableTry" : default);
                return default!;
            }
            return _jitEnableRemoveUnreachableTry;
        }
    }
    public int JitEnableGuardedDevirtualization => _jitEnableGuardedDevirtualization;
    public int JitGuardedDevirtualizationMaxTypeChecks => _jitGuardedDevirtualizationMaxTypeChecks;
    public int JitGuardedDevirtualizationRequireInlining => _jitGuardedDevirtualizationRequireInlining;
    public int JitGuardedDevirtualizationChainLikelihood => _jitGuardedDevirtualizationChainLikelihood;
    public int JitGuardedDevirtualizationChainStatements => _jitGuardedDevirtualizationChainStatements;
    public unsafe byte* JitGuardedDevirtualizationRange
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitGuardedDevirtualizationRange" : default);
                return default!;
            }
            return _jitGuardedDevirtualizationRange;
        }
    }
    public int JitRandomGuardedDevirtualization
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRandomGuardedDevirtualization" : default);
                return default!;
            }
            return _jitRandomGuardedDevirtualization;
        }
    }
    public int TC_OnStackReplacement => _tC_OnStackReplacement;
    public int TC_OnStackReplacement_InitialCounter => _tC_OnStackReplacement_InitialCounter;
    public int TC_PartialCompilation => _tC_PartialCompilation;
    public int JitRandomPartialCompilation
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRandomPartialCompilation" : default);
                return default!;
            }
            return _jitRandomPartialCompilation;
        }
    }
    public int TC_PatchpointStrategy => _tC_PatchpointStrategy;
    public int JitRandomOnStackReplacement
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRandomOnStackReplacement" : default);
                return default!;
            }
            return _jitRandomOnStackReplacement;
        }
    }
    public int JitOffsetOnStackReplacement
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitOffsetOnStackReplacement" : default);
                return default!;
            }
            return _jitOffsetOnStackReplacement;
        }
    }
    public unsafe byte* JitEnableOsrRange
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnableOsrRange" : default);
                return default!;
            }
            return _jitEnableOsrRange;
        }
    }
    public unsafe byte* JitEnablePatchpointRange
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnablePatchpointRange" : default);
                return default!;
            }
            return _jitEnablePatchpointRange;
        }
    }
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
    public int JitInstrumentIfOptimizing
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInstrumentIfOptimizing" : default);
                return default!;
            }
            return _jitInstrumentIfOptimizing;
        }
    }
    public unsafe byte* JitInstrumentIfOptimizingRange
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInstrumentIfOptimizingRange" : default);
                return default!;
            }
            return _jitInstrumentIfOptimizingRange;
        }
    }
    public int JitInstrumentInlinees => _jitInstrumentInlinees;
    public int JitDisablePGO => _jitDisablePGO;
    public unsafe byte* JitEnablePGORange
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnablePGORange" : default);
                return default!;
            }
            return _jitEnablePGORange;
        }
    }
    public int JitRandomEdgeCounts
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRandomEdgeCounts" : default);
                return default!;
            }
            return _jitRandomEdgeCounts;
        }
    }
    public int JitCrossCheckDevirtualizationAndPGO
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitCrossCheckDevirtualizationAndPGO" : default);
                return default!;
            }
            return _jitCrossCheckDevirtualizationAndPGO;
        }
    }
    public int JitNoteFailedExactDevirtualization
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitNoteFailedExactDevirtualization" : default);
                return default!;
            }
            return _jitNoteFailedExactDevirtualization;
        }
    }
    public int JitRandomlyCollect64BitCounts
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRandomlyCollect64BitCounts" : default);
                return default!;
            }
            return _jitRandomlyCollect64BitCounts;
        }
    }
    public int JitSynthesizeCounts
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitSynthesizeCounts" : default);
                return default!;
            }
            return _jitSynthesizeCounts;
        }
    }
    public int JitPropagateSynthesizedCountsToProfileData
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitPropagateSynthesizedCountsToProfileData" : default);
                return default!;
            }
            return _jitPropagateSynthesizedCountsToProfileData;
        }
    }
    public int JitSynthesisUseSolver
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitSynthesisUseSolver" : default);
                return default!;
            }
            return _jitSynthesisUseSolver;
        }
    }
    public unsafe byte* JitSynthesisExceptionWeight
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitSynthesisExceptionWeight" : default);
                return default!;
            }
            return _jitSynthesisExceptionWeight;
        }
    }
    public int JitEnableExactDevirtualization => _jitEnableExactDevirtualization;
    public int JitForceControlFlowGuard => _jitForceControlFlowGuard;
    public int JitCFGUseDispatcher => _jitCFGUseDispatcher;
    public int JitEnableHeadTailMerge => _jitEnableHeadTailMerge;
    public int JitEnablePhysicalPromotion => _jitEnablePhysicalPromotion;
    public int JitEnableCrossBlockLocalAssertionProp => _jitEnableCrossBlockLocalAssertionProp;
    public int JitEnablePostorderLocalAssertionProp => _jitEnablePostorderLocalAssertionProp;
    public int JitEnableStrengthReduction => _jitEnableStrengthReduction;
    public int JitEnableInductionVariableOpts => _jitEnableInductionVariableOpts;
    public unsafe byte* JitFunctionFile
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitFunctionFile" : default);
                return default!;
            }
            return _jitFunctionFile;
        }
    }
    public MethodSet JitRawHexCode
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRawHexCode" : default);
                return default!;
            }
            return _jitRawHexCode;
        }
    }
    public unsafe byte* JitRawHexCodeFile
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRawHexCodeFile" : default);
                return default!;
            }
            return _jitRawHexCodeFile;
        }
    }
    public int JitSaveFpLrWithCalleeSavedRegisters
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitSaveFpLrWithCalleeSavedRegisters" : default);
                return default!;
            }
            return _jitSaveFpLrWithCalleeSavedRegisters;
        }
    }
    public int JitUseScalableVectorT
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitUseScalableVectorT" : default);
                return default!;
            }
            return _jitUseScalableVectorT;
        }
    }
    public int JitDispIns
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDispIns" : default);
                return default!;
            }
            return _jitDispIns;
        }
    }
    public unsafe byte* JitR2RUnsupportedRange
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitR2RUnsupportedRange" : default);
                return default!;
            }
            return _jitR2RUnsupportedRange;
        }
    }
    public int JitWasmFunclets => _jitWasmFunclets;
    public int JitEnregStructLocals => _jitEnregStructLocals;

    public unsafe void destroy(ICorJitHost* jitHost)
    {
        if (!_isInitialized)
        {
            return;
        }

        // _altJitLimit = unchecked((int)(0xCDCDCDCD));
        // _altJitSkipOnAssert = unchecked((int)(0xCDCDCDCD));
        // _breakOnDumpToken = unchecked((int)(0xCDCDCDCD));
        // _debugBreakOnVerificationFailure = unchecked((int)(0xCDCDCDCD));
        // _displayLoopHoistStats = unchecked((int)(0xCDCDCDCD));
        // _displayLsraStats = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_jitLsraOrdering);
        }
        // _enablePCRelAddr = unchecked((int)(0xCDCDCDCD));
        // _jitAssertOnMaxRAPasses = unchecked((int)(0xCDCDCDCD));
        // _jitBreakEmitOutputInstr = unchecked((int)(0xCDCDCDCD));
        // _jitBreakMorphTree = unchecked((int)(0xCDCDCDCD));
        // _jitBreakOnBadCode = unchecked((int)(0xCDCDCDCD));
        // _jitBreakOnMinOpts = unchecked((int)(0xCDCDCDCD));
        // _jitCloneLoops = unchecked((int)(0xCDCDCDCD));
        // _jitCloneLoopsWithEH = unchecked((int)(0xCDCDCDCD));
        // _jitCloneLoopsWithGdvTests = unchecked((int)(0xCDCDCDCD));
        // _jitCloneLoopsSizeLimit = unchecked((int)(0xCDCDCDCD));
        // _jitCloneLoopsMinPerCallRatio = unchecked((int)(0xCDCDCDCD));
        // _jitDebugLogLoopCloning = unchecked((int)(0xCDCDCDCD));
        // _jitAlignLoopMinBlockWeight = unchecked((int)(0xCDCDCDCD));
        // _jitAlignLoopMaxCodeSize = unchecked((int)(0xCDCDCDCD));
        // _jitAlignLoopBoundary = unchecked((int)(0xCDCDCDCD));
        // _jitAlignLoopForJcc = unchecked((int)(0xCDCDCDCD));
        // _jitAlignLoopAdaptive = unchecked((int)(0xCDCDCDCD));
        // _jitHideAlignBehindJmp = unchecked((int)(0xCDCDCDCD));
        // _jitOptimizeStructHiddenBuffer = unchecked((int)(0xCDCDCDCD));
        // _jitEnableStoreLclFldCoalescing = unchecked((int)(0xCDCDCDCD));
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
        // _jitInlineBudget = unchecked((int)(0xCDCDCDCD));
        // _jitForceInlineDepth = unchecked((int)(0xCDCDCDCD));
        // _jitInlineMethodsWithEH = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_jitInlineMethodsWithEHRange);
            jitHost->freeStringConfigValue(_jitOptimizeAwaitRange);
        }
        // _jitAwaitHashBreak = unchecked((int)(0xCDCDCDCD));
        // _jitLongAddress = unchecked((int)(0xCDCDCDCD));
        // _jitMaxUncheckedOffset = unchecked((int)(0xCDCDCDCD));
        if (Configuration.TARGET_ARM64)
        {
            // _jitPacEnabled = unchecked((int)(0xCDCDCDCD));
        }
        // _jitEnableGenericVirtualDevirtualization = unchecked((int)(0xCDCDCDCD));
        // _jitMinOpts = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            _jitMinOptsName.destroy(jitHost);
        }
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
        if (Configuration.DEBUG)
        {
            _jitPrintInlinedMethods.destroy(jitHost);
            _jitPrintDevirtualizedMethods.destroy(jitHost);
        }
        // _jitProfileChecks = unchecked((int)(0xCDCDCDCD));
        // _jitRequired = unchecked((int)(0xCDCDCDCD));
        // _jitStackAllocToLocalSize = unchecked((int)(0xCDCDCDCD));
        // _jitSkipArrayBoundCheck = unchecked((int)(0xCDCDCDCD));
        // _jitEnablePhaseChecks = unchecked((int)(0xCDCDCDCD));
        // _jitSplitFunctionSize = unchecked((int)(0xCDCDCDCD));
        // _jitSsaStress = unchecked((int)(0xCDCDCDCD));
        // _jitStackChecks = unchecked((int)(0xCDCDCDCD));
        // _jitStress = unchecked((int)(0xCDCDCDCD));
        // _jitStressBBProf = unchecked((int)(0xCDCDCDCD));
        // _jitStressProcedureSplitting = unchecked((int)(0xCDCDCDCD));
        // _jitStressRegs = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_jitStressRegsRange);
        }
        // _jitStressSplitTreeLimit = unchecked((int)(0xCDCDCDCD));
        // _jitVNMapSelLimit = unchecked((int)(0xCDCDCDCD));
        // _runAltJitCode = unchecked((int)(0xCDCDCDCD));
        // _runComponentUnitTests = unchecked((int)(0xCDCDCDCD));
        // _shouldInjectFault = unchecked((int)(0xCDCDCDCD));
        // _tailcallStress = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            _jitBreak.destroy(jitHost);
            _jitDebugBreak.destroy(jitHost);
            _jitDump.destroy(jitHost);
        }
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
        if (Configuration.DEBUG)
        {
            _jitEHDump.destroy(jitHost);
            _jitExclude.destroy(jitHost);
        }
        // _jitFakeProcedureSplitting = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
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
        }
        // _jitDumpFgHash = unchecked((int)(0xCDCDCDCD));
        // _jitDumpFgTier0 = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_jitDumpFgDir);
            jitHost->freeStringConfigValue(_jitDumpFgFile);
            jitHost->freeStringConfigValue(_jitDumpFgPhase);
            jitHost->freeStringConfigValue(_jitDumpFgPrePhase);
        }
        // _jitDumpFgDot = unchecked((int)(0xCDCDCDCD));
        // _jitDumpFgEH = unchecked((int)(0xCDCDCDCD));
        // _jitDumpFgLoops = unchecked((int)(0xCDCDCDCD));
        // _jitDumpFgConstrained = unchecked((int)(0xCDCDCDCD));
        // _jitDumpFgBlockID = unchecked((int)(0xCDCDCDCD));
        // _jitDumpFgBlockFlags = unchecked((int)(0xCDCDCDCD));
        // _jitDumpFgLoopFlags = unchecked((int)(0xCDCDCDCD));
        // _jitDumpFgBlockOrder = unchecked((int)(0xCDCDCDCD));
        // _jitDumpFgMemorySsa = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_jitRange);
            jitHost->freeStringConfigValue(_jitStressModeNames);
        }
        // _jitStressModeNamesOnly = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_jitStressModeNamesAllow);
            jitHost->freeStringConfigValue(_jitStressModeNamesNot);
            jitHost->freeStringConfigValue(_jitStressRange);
            _jitEmitUnitTests.destroy(jitHost);
            jitHost->freeStringConfigValue(_jitEmitUnitTestsSections);
        }
        _jitDisasm.destroy(jitHost);
        // _jitDisasmTesting = unchecked((int)(0xCDCDCDCD));
        // _jitDisasmDiffable = unchecked((int)(0xCDCDCDCD));
        // _jitDisasmSummary = unchecked((int)(0xCDCDCDCD));
        // _jitDisasmOnlyOptimized = unchecked((int)(0xCDCDCDCD));
        // _jitDisasmWithAlignmentBoundaries = unchecked((int)(0xCDCDCDCD));
        // _jitDisasmWithCodeBytes = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_jitDisasmAssemblies);
        }
        // _jitDisasmWithGC = unchecked((int)(0xCDCDCDCD));
        // _jitDisasmWithDebugInfo = unchecked((int)(0xCDCDCDCD));
        // _jitDisasmSpilled = unchecked((int)(0xCDCDCDCD));
        // _jitDisasmWithAddress = unchecked((int)(0xCDCDCDCD));
        jitHost->freeStringConfigValue(_jitStdOutFile);
        // _richDebugInfo = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_writeRichDebugInfoFile);
        }
        // _jitAlignLoops = unchecked((int)(0xCDCDCDCD));
        // _altJitAssertOnNYI = unchecked((int)(0xCDCDCDCD));
        // _enableEHWriteThru = unchecked((int)(0xCDCDCDCD));
        // _enableMultiRegLocals = unchecked((int)(0xCDCDCDCD));
        // _jitNoInline = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            // _jitStressRex2Encoding = unchecked((int)(0xCDCDCDCD));
            // _jitStressPromotedEvexEncoding = unchecked((int)(0xCDCDCDCD));
        }
        if (Configuration.TARGET_AMD64 || Configuration.TARGET_X86)
        {
            // _jitStressEvexEncoding = unchecked((int)(0xCDCDCDCD));
        }
        if (Configuration.TARGET_LOONGARCH64)
        {
            // _enableHWIntrinsic = unchecked((int)(0xCDCDCDCD));
        }
        else
        {
            // _enableHWIntrinsic = unchecked((int)(0xCDCDCDCD));
        }
        if (Configuration.TARGET_AMD64 || Configuration.TARGET_X86)
        {
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
        }
        else if (Configuration.TARGET_ARM64)
        {
            // _enableArm64Aes = unchecked((int)(0xCDCDCDCD));
            // _enableArm64Atomics = unchecked((int)(0xCDCDCDCD));
            // _enableArm64Crc32 = unchecked((int)(0xCDCDCDCD));
            // _enableArm64Cssc = unchecked((int)(0xCDCDCDCD));
            // _enableArm64Dczva = unchecked((int)(0xCDCDCDCD));
            // _enableArm64Dp = unchecked((int)(0xCDCDCDCD));
            // _enableArm64Fp16 = unchecked((int)(0xCDCDCDCD));
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
        }
        else if (Configuration.TARGET_RISCV64)
        {
            // _enableRiscV64Zba = unchecked((int)(0xCDCDCDCD));
            // _enableRiscV64Zbb = unchecked((int)(0xCDCDCDCD));
            // _enableRiscV64Zbs = unchecked((int)(0xCDCDCDCD));
            // _enableRiscV64Zicond = unchecked((int)(0xCDCDCDCD));
        }
        // _enableEmbeddedBroadcast = unchecked((int)(0xCDCDCDCD));
        // _enableEmbeddedMasking = unchecked((int)(0xCDCDCDCD));
        // _enableApxNDD = unchecked((int)(0xCDCDCDCD));
        // _enableApxConditionalChaining = unchecked((int)(0xCDCDCDCD));
        // _enableApxPPHint = unchecked((int)(0xCDCDCDCD));
        // _enableApxPP2 = unchecked((int)(0xCDCDCDCD));
        // _enableApxZU = unchecked((int)(0xCDCDCDCD));
        if (Configuration.FEATURE_SIMD)
        {
            // _jitDisableSimdVN = unchecked((int)(0xCDCDCDCD));
        }
        // _jitConstCSE = unchecked((int)(0xCDCDCDCD));
        // _jitRLCSEGreedy = unchecked((int)(0xCDCDCDCD));
        // _jitRLCSEVerbose = unchecked((int)(0xCDCDCDCD));
        // _jitCSEHash = unchecked((int)(0xCDCDCDCD));
        // _jitCSEMask = unchecked((int)(0xCDCDCDCD));
        // _jitMetrics = unchecked((int)(0xCDCDCDCD));
        // _jitRandomCSE = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_jitReplayCSE);
            jitHost->freeStringConfigValue(_jitReplayCSEReward);
            jitHost->freeStringConfigValue(_jitRLCSE);
            jitHost->freeStringConfigValue(_jitRLCSEAlpha);
        }
        // _jitRLCSECandidateFeatures = unchecked((int)(0xCDCDCDCD));
        // _jitRLHook = unchecked((int)(0xCDCDCDCD));
        // _jitRLHookEmitFeatureNames = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_jitRLHookCSEDecisions);
        }
        if (!Configuration.DEBUG)
        {
            // _jitEnableNoWayAssert = unchecked((int)(0xCDCDCDCD));
        }
        else
        {
            // _jitEnableNoWayAssert = unchecked((int)(0xCDCDCDCD));
        }
        // _displayMemStats = unchecked((int)(0xCDCDCDCD));
        // _jitEnregStats = unchecked((int)(0xCDCDCDCD));
        // _jitAggressiveInlining = unchecked((int)(0xCDCDCDCD));
        // _jitELTHookEnabled = unchecked((int)(0xCDCDCDCD));
        // _jitInlineSIMDMultiplier = unchecked((int)(0xCDCDCDCD));
        // _jitMaxLocalsToTrack = unchecked((int)(0xCDCDCDCD));
        // _jitDoAssertionProp = unchecked((int)(0xCDCDCDCD));
        // _jitDoCopyProp = unchecked((int)(0xCDCDCDCD));
        // _jitDoOptimizeIVs = unchecked((int)(0xCDCDCDCD));
        // _jitDoEarlyProp = unchecked((int)(0xCDCDCDCD));
        // _jitDoLoopHoisting = unchecked((int)(0xCDCDCDCD));
        // _jitDoLoopInversion = unchecked((int)(0xCDCDCDCD));
        // _jitLoopInversionSizeLimit = unchecked((int)(0xCDCDCDCD));
        // _jitDoRangeAnalysis = unchecked((int)(0xCDCDCDCD));
        // _jitDoVNBasedDeadStoreRemoval = unchecked((int)(0xCDCDCDCD));
        // _jitDoRedundantBranchOpts = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_jitEnableRboRange);
            jitHost->freeStringConfigValue(_jitEnableHeadTailMergeRange);
            jitHost->freeStringConfigValue(_jitEnableVNBasedDeadStoreRemovalRange);
            jitHost->freeStringConfigValue(_jitEnableEarlyLivenessRange);
            jitHost->freeStringConfigValue(_jitOnlyOptimizeRange);
            jitHost->freeStringConfigValue(_jitEnablePhysicalPromotionRange);
            jitHost->freeStringConfigValue(_jitEnableCrossBlockLocalAssertionPropRange);
            jitHost->freeStringConfigValue(_jitEnableInductionVariableOptsRange);
            jitHost->freeStringConfigValue(_jitEnableLocalAddrPropagationRange);
        }
        // _jitDoSsa = unchecked((int)(0xCDCDCDCD));
        // _jitDoValueNumber = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_jitOptRepeatRange);
        }
        // _jitDoIfConversion = unchecked((int)(0xCDCDCDCD));
        // _jitDoOptimizeMaskConversions = unchecked((int)(0xCDCDCDCD));
        // _jitOptimizeAwait = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_jitAsyncDefaultValueAnalysisRange);
            jitHost->freeStringConfigValue(_jitAsyncPreservedValueAnalysisRange);
        }
        // _jitAsyncInlining = unchecked((int)(0xCDCDCDCD));
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
        // _enableExtraSuperPmiQueries = unchecked((int)(0xCDCDCDCD));
        // _jitInlineDumpData = unchecked((int)(0xCDCDCDCD));
        // _jitInlineDumpXml = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_jitInlineDumpXmlFile);
        }
        // _jitInlinePolicyDumpXml = unchecked((int)(0xCDCDCDCD));
        // _jitInlineLimit = unchecked((int)(0xCDCDCDCD));
        // _jitInlinePolicyDiscretionary = unchecked((int)(0xCDCDCDCD));
        // _jitInlinePolicyFull = unchecked((int)(0xCDCDCDCD));
        // _jitInlinePolicySize = unchecked((int)(0xCDCDCDCD));
        // _jitInlinePolicyRandom = unchecked((int)(0xCDCDCDCD));
        // _jitInlinePolicyReplay = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_jitNoInlineRange);
            jitHost->freeStringConfigValue(_jitInlineReplayFile);
        }
        // _jitStressAsyncInlining = unchecked((int)(0xCDCDCDCD));
        // _jitStressAsyncInliningMaxDepth = unchecked((int)(0xCDCDCDCD));
        // _jitStressAsyncInliningPct = unchecked((int)(0xCDCDCDCD));
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
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_jitObjectStackAllocationRange);
        }
        // _jitObjectStackAllocation = unchecked((int)(0xCDCDCDCD));
        // _jitObjectStackAllocationRefClass = unchecked((int)(0xCDCDCDCD));
        // _jitObjectStackAllocationBoxedValueClass = unchecked((int)(0xCDCDCDCD));
        // _jitObjectStackAllocationConditionalEscape = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_jitObjectStackAllocationConditionalEscapeRange);
        }
        // _jitObjectStackAllocationArray = unchecked((int)(0xCDCDCDCD));
        // _jitObjectStackAllocationSize = unchecked((int)(0xCDCDCDCD));
        // _jitObjectStackAllocationTrackFields = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_jitObjectStackAllocationTrackFieldsRange);
        }
        // _jitObjectStackAllocationDumpConnGraph = unchecked((int)(0xCDCDCDCD));
        // _jitEECallTimingInfo = unchecked((int)(0xCDCDCDCD));
        // _jitEnableFinallyCloning = unchecked((int)(0xCDCDCDCD));
        // _jitEnableRemoveEmptyTry = unchecked((int)(0xCDCDCDCD));
        // _jitEnableRemoveEmptyTryCatchOrTryFault = unchecked((int)(0xCDCDCDCD));
        // _jitEnableRemoveUnreachableTry = unchecked((int)(0xCDCDCDCD));
        // _jitEnableGuardedDevirtualization = unchecked((int)(0xCDCDCDCD));
        // _jitGuardedDevirtualizationMaxTypeChecks = unchecked((int)(0xCDCDCDCD));
        // _jitGuardedDevirtualizationRequireInlining = unchecked((int)(0xCDCDCDCD));
        // _jitGuardedDevirtualizationChainLikelihood = unchecked((int)(0xCDCDCDCD));
        // _jitGuardedDevirtualizationChainStatements = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_jitGuardedDevirtualizationRange);
        }
        // _jitRandomGuardedDevirtualization = unchecked((int)(0xCDCDCDCD));
        // _tC_OnStackReplacement = unchecked((int)(0xCDCDCDCD));
        // _tC_OnStackReplacement_InitialCounter = unchecked((int)(0xCDCDCDCD));
        // _tC_PartialCompilation = unchecked((int)(0xCDCDCDCD));
        // _jitRandomPartialCompilation = unchecked((int)(0xCDCDCDCD));
        // _tC_PatchpointStrategy = unchecked((int)(0xCDCDCDCD));
        // _jitRandomOnStackReplacement = unchecked((int)(0xCDCDCDCD));
        // _jitOffsetOnStackReplacement = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_jitEnableOsrRange);
            jitHost->freeStringConfigValue(_jitEnablePatchpointRange);
        }
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
        // _jitInstrumentIfOptimizing = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_jitInstrumentIfOptimizingRange);
        }
        // _jitInstrumentInlinees = unchecked((int)(0xCDCDCDCD));
        // _jitDisablePGO = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_jitEnablePGORange);
        }
        // _jitRandomEdgeCounts = unchecked((int)(0xCDCDCDCD));
        // _jitCrossCheckDevirtualizationAndPGO = unchecked((int)(0xCDCDCDCD));
        // _jitNoteFailedExactDevirtualization = unchecked((int)(0xCDCDCDCD));
        // _jitRandomlyCollect64BitCounts = unchecked((int)(0xCDCDCDCD));
        // _jitSynthesizeCounts = unchecked((int)(0xCDCDCDCD));
        // _jitPropagateSynthesizedCountsToProfileData = unchecked((int)(0xCDCDCDCD));
        // _jitSynthesisUseSolver = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_jitSynthesisExceptionWeight);
        }
        // _jitEnableExactDevirtualization = unchecked((int)(0xCDCDCDCD));
        // _jitForceControlFlowGuard = unchecked((int)(0xCDCDCDCD));
        // _jitCFGUseDispatcher = unchecked((int)(0xCDCDCDCD));
        // _jitEnableHeadTailMerge = unchecked((int)(0xCDCDCDCD));
        // _jitEnablePhysicalPromotion = unchecked((int)(0xCDCDCDCD));
        // _jitEnableCrossBlockLocalAssertionProp = unchecked((int)(0xCDCDCDCD));
        // _jitEnablePostorderLocalAssertionProp = unchecked((int)(0xCDCDCDCD));
        // _jitEnableStrengthReduction = unchecked((int)(0xCDCDCDCD));
        // _jitEnableInductionVariableOpts = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_jitFunctionFile);
            _jitRawHexCode.destroy(jitHost);
            jitHost->freeStringConfigValue(_jitRawHexCodeFile);
        }
        if (Configuration.TARGET_ARM64)
        {
            // _jitSaveFpLrWithCalleeSavedRegisters = unchecked((int)(0xCDCDCDCD));
        }
        // _jitUseScalableVectorT = unchecked((int)(0xCDCDCDCD));
        if (Configuration.TARGET_LOONGARCH64)
        {
            // _jitDispIns = unchecked((int)(0xCDCDCDCD));
        }
        if (Configuration.TARGET_WASM)
        {
            if (Configuration.DEBUG)
            {
                jitHost->freeStringConfigValue(_jitR2RUnsupportedRange);
            }
            // _jitWasmFunclets = unchecked((int)(0xCDCDCDCD));
        }
        // _jitEnregStructLocals = unchecked((int)(0xCDCDCDCD));
        _isInitialized = false;
    }

    public unsafe void initialize(ICorJitHost* jitHost)
    {
        assert(!_isInitialized);

        if (Configuration.DEBUG)
        {
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
        }
        _jitCloneLoopsSizeLimit = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCloneLoopsSizeLimit"u8))), 400);
        _jitCloneLoopsMinPerCallRatio = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCloneLoopsMinPerCallRatio"u8))), 4);
        if (Configuration.DEBUG)
        {
            _jitDebugLogLoopCloning = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDebugLogLoopCloning"u8))), 0);
            _jitAlignLoopMinBlockWeight = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAlignLoopMinBlockWeight"u8))), DEFAULT_ALIGN_LOOP_MIN_BLOCK_WEIGHT);
            _jitAlignLoopMaxCodeSize = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAlignLoopMaxCodeSize"u8))), DEFAULT_MAX_LOOPSIZE_FOR_ALIGN);
            _jitAlignLoopBoundary = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAlignLoopBoundary"u8))), DEFAULT_ALIGN_LOOP_BOUNDARY);
            _jitAlignLoopForJcc = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAlignLoopForJcc"u8))), 0);
            _jitAlignLoopAdaptive = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAlignLoopAdaptive"u8))), 1);
            _jitHideAlignBehindJmp = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitHideAlignBehindJmp"u8))), 1);
            _jitOptimizeStructHiddenBuffer = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitOptimizeStructHiddenBuffer"u8))), 1);
        }
        _jitEnableStoreLclFldCoalescing = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableStoreLclFldCoalescing"u8))), 1);
        if (Configuration.DEBUG)
        {
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
        }
        _jitInlineBudget = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineBudget"u8))), DEFAULT_INLINE_BUDGET);
        if (Configuration.DEBUG)
        {
            _jitForceInlineDepth = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitForceInlineDepth"u8))), DEFAULT_MAX_FORCE_INLINE_DEPTH);
        }
        _jitInlineMethodsWithEH = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineMethodsWithEH"u8))), 1);
        if (Configuration.DEBUG)
        {
            _jitInlineMethodsWithEHRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineMethodsWithEHRange"u8))));
            _jitOptimizeAwaitRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitOptimizeAwaitRange"u8))));
            _jitAwaitHashBreak = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAwaitHashBreak"u8))), -1);
            _jitLongAddress = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitLongAddress"u8))), 0);
            _jitMaxUncheckedOffset = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitMaxUncheckedOffset"u8))), 8);
        }
        if (Configuration.TARGET_ARM64)
        {
            _jitPacEnabled = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitPacEnabled"u8))), 0);
        }
        _jitEnableGenericVirtualDevirtualization = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableGenericVirtualDevirtualization"u8))), 1);
        if (Configuration.DEBUG)
        {
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
            _jitEnablePhaseChecks = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnablePhaseChecks"u8))), 1);
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
        }
        var jitDisasmValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasm"u8))));
        _jitDisasm = new MethodSet(jitDisasmValue, jitHost);
        _jitDisasmTesting = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmTesting"u8))), 0);
        _jitDisasmDiffable = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmDiffable"u8))), 0);
        _jitDisasmSummary = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmSummary"u8))), 0);
        _jitDisasmOnlyOptimized = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmOnlyOptimized"u8))), 0);
        _jitDisasmWithAlignmentBoundaries = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmWithAlignmentBoundaries"u8))), 0);
        _jitDisasmWithCodeBytes = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmWithCodeBytes"u8))), 0);
        if (Configuration.DEBUG)
        {
            _jitDisasmAssemblies = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmAssemblies"u8))));
            _jitDisasmWithGC = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmWithGC"u8))), 0);
            _jitDisasmWithDebugInfo = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmWithDebugInfo"u8))), 0);
            _jitDisasmSpilled = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmSpilled"u8))), 0);
            _jitDisasmWithAddress = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmWithAddress"u8))), 0);
        }
        _jitStdOutFile = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStdOutFile"u8))));
        _richDebugInfo = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("RichDebugInfo"u8))), 0);
        if (Configuration.DEBUG)
        {
            _writeRichDebugInfoFile = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("WriteRichDebugInfoFile"u8))));
        }
        if (Configuration.FEATURE_LOOP_ALIGN)
        {
            _jitAlignLoops = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAlignLoops"u8))), 1);
        }
        else
        {
            _jitAlignLoops = 0;
        }
        _altJitAssertOnNYI = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("AltJitAssertOnNYI"u8))), 1);
        _enableEHWriteThru = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableEHWriteThru"u8))), 1);
        _enableMultiRegLocals = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableMultiRegLocals"u8))), 1);
        _jitNoInline = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoInline"u8))), 0);
        if (Configuration.DEBUG)
        {
            if (Configuration.DEBUG)
            {
                _jitStressRex2Encoding = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressRex2Encoding"u8))), 0);
                _jitStressPromotedEvexEncoding = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressPromotedEvexEncoding"u8))), 0);
            }
        }
        if (Configuration.TARGET_AMD64 || Configuration.TARGET_X86)
        {
            if (Configuration.DEBUG)
            {
                _jitStressEvexEncoding = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressEvexEncoding"u8))), 0);
            }
        }
        if (Configuration.TARGET_LOONGARCH64)
        {
            _enableHWIntrinsic = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableHWIntrinsic"u8))), 0);
        }
        else
        {
            _enableHWIntrinsic = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableHWIntrinsic"u8))), 1);
        }
        if (Configuration.TARGET_AMD64 || Configuration.TARGET_X86)
        {
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
        }
        else if (Configuration.TARGET_ARM64)
        {
            _enableArm64Aes = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Aes"u8))), 1);
            _enableArm64Atomics = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Atomics"u8))), 1);
            _enableArm64Crc32 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Crc32"u8))), 1);
            _enableArm64Cssc = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Cssc"u8))), 1);
            _enableArm64Dczva = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Dczva"u8))), 1);
            _enableArm64Dp = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Dp"u8))), 1);
            _enableArm64Fp16 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Fp16"u8))), 1);
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
        }
        else if (Configuration.TARGET_RISCV64)
        {
            _enableRiscV64Zba = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableRiscV64Zba"u8))), 1);
            _enableRiscV64Zbb = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableRiscV64Zbb"u8))), 1);
            _enableRiscV64Zbs = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableRiscV64Zbs"u8))), 1);
            _enableRiscV64Zicond = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableRiscV64Zicond"u8))), 1);
        }
        _enableEmbeddedBroadcast = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableEmbeddedBroadcast"u8))), 1);
        _enableEmbeddedMasking = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableEmbeddedMasking"u8))), 1);
        _enableApxNDD = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableApxNDD"u8))), 0);
        _enableApxConditionalChaining = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableApxConditionalChaining"u8))), 0);
        _enableApxPPHint = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableApxPPHint"u8))), 0);
        _enableApxPP2 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableApxPP2"u8))), 0);
        _enableApxZU = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableApxZU"u8))), 0);
        if (Configuration.FEATURE_SIMD)
        {
            _jitDisableSimdVN = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisableSimdVN"u8))), 0);
        }
        _jitConstCSE = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitConstCSE"u8))), CONST_CSE_ENABLE_ARM_RISCV64);
        _jitRLCSEGreedy = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRLCSEGreedy"u8))), 0);
        _jitRLCSEVerbose = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRLCSEVerbose"u8))), 0);
        if (Configuration.DEBUG)
        {
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
        }
        if (!Configuration.DEBUG)
        {
            _jitEnableNoWayAssert = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableNoWayAssert"u8))), 0);
        }
        else
        {
            _jitEnableNoWayAssert = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableNoWayAssert"u8))), 1);
        }
        _displayMemStats = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitMemStats"u8))), 0);
        if (Configuration.DEBUG)
        {
            _jitEnregStats = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnregStats"u8))), 0);
        }
        _jitAggressiveInlining = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAggressiveInlining"u8))), 0);
        _jitELTHookEnabled = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitELTHookEnabled"u8))), 0);
        _jitInlineSIMDMultiplier = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineSIMDMultiplier"u8))), 3);
        _jitMaxLocalsToTrack = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitMaxLocalsToTrack"u8))), 0x400);
        if (Configuration.DEBUG)
        {
            _jitDoAssertionProp = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoAssertionProp"u8))), 1);
            _jitDoCopyProp = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoCopyProp"u8))), 1);
            _jitDoOptimizeIVs = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoOptimizeIVs"u8))), 1);
            _jitDoEarlyProp = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoEarlyProp"u8))), 1);
            _jitDoLoopHoisting = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoLoopHoisting"u8))), 1);
            _jitDoLoopInversion = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoLoopInversion"u8))), 1);
        }
        _jitLoopInversionSizeLimit = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitLoopInversionSizeLimit"u8))), 100);
        if (Configuration.DEBUG)
        {
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
        }
        _jitAsyncInlining = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAsyncInlining"u8))), 1);
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
        if (Configuration.DEBUG)
        {
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
            _jitStressAsyncInlining = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressAsyncInlining"u8))), 0);
            _jitStressAsyncInliningMaxDepth = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressAsyncInliningMaxDepth"u8))), 8);
            _jitStressAsyncInliningPct = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressAsyncInliningPct"u8))), 75);
        }
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
        if (Configuration.DEBUG)
        {
            _jitObjectStackAllocationRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationRange"u8))));
        }
        _jitObjectStackAllocation = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocation"u8))), 1);
        _jitObjectStackAllocationRefClass = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationRefClass"u8))), 1);
        _jitObjectStackAllocationBoxedValueClass = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationBoxedValueClass"u8))), 1);
        _jitObjectStackAllocationConditionalEscape = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationConditionalEscape"u8))), 1);
        if (Configuration.DEBUG)
        {
            _jitObjectStackAllocationConditionalEscapeRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationConditionalEscapeRange"u8))));
        }
        _jitObjectStackAllocationArray = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationArray"u8))), 1);
        _jitObjectStackAllocationSize = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationSize"u8))), 528);
        _jitObjectStackAllocationTrackFields = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationTrackFields"u8))), 1);
        if (Configuration.DEBUG)
        {
            _jitObjectStackAllocationTrackFieldsRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationTrackFieldsRange"u8))));
            _jitObjectStackAllocationDumpConnGraph = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationDumpConnGraph"u8))), 0);
        }
        _jitEECallTimingInfo = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEECallTimingInfo"u8))), 0);
        if (Configuration.DEBUG)
        {
            _jitEnableFinallyCloning = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableFinallyCloning"u8))), 1);
            _jitEnableRemoveEmptyTry = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableRemoveEmptyTry"u8))), 1);
            _jitEnableRemoveEmptyTryCatchOrTryFault = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableRemoveEmptyTryCatchOrTryFault"u8))), 1);
            _jitEnableRemoveUnreachableTry = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableRemoveUnreachableTry"u8))), 1);
        }
        _jitEnableGuardedDevirtualization = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableGuardedDevirtualization"u8))), 1);
        _jitGuardedDevirtualizationMaxTypeChecks = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitGuardedDevirtualizationMaxTypeChecks"u8))), -1);
        _jitGuardedDevirtualizationRequireInlining = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitGuardedDevirtualizationRequireInlining"u8))), 0);
        _jitGuardedDevirtualizationChainLikelihood = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitGuardedDevirtualizationChainLikelihood"u8))), 0x4B);
        _jitGuardedDevirtualizationChainStatements = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitGuardedDevirtualizationChainStatements"u8))), 1);
        if (Configuration.DEBUG)
        {
            _jitGuardedDevirtualizationRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitGuardedDevirtualizationRange"u8))));
            _jitRandomGuardedDevirtualization = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRandomGuardedDevirtualization"u8))), 0);
        }
        _tC_OnStackReplacement = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("TC_OnStackReplacement"u8))), Configuration.FEATURE_ON_STACK_REPLACEMENT ? 1 : 0);
        _tC_OnStackReplacement_InitialCounter = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("TC_OnStackReplacement_InitialCounter"u8))), 1000);
        _tC_PartialCompilation = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("TC_PartialCompilation"u8))), 0);
        if (Configuration.DEBUG)
        {
            _jitRandomPartialCompilation = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRandomPartialCompilation"u8))), 0);
        }
        _tC_PatchpointStrategy = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("TC_PatchpointStrategy"u8))), 2);
        if (Configuration.DEBUG)
        {
            _jitRandomOnStackReplacement = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRandomOnStackReplacement"u8))), 0);
            _jitOffsetOnStackReplacement = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitOffsetOnStackReplacement"u8))), -1);
            _jitEnableOsrRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableOsrRange"u8))));
            _jitEnablePatchpointRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnablePatchpointRange"u8))));
        }
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
        if (Configuration.DEBUG)
        {
            _jitInstrumentIfOptimizing = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInstrumentIfOptimizing"u8))), 0);
            _jitInstrumentIfOptimizingRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInstrumentIfOptimizingRange"u8))));
        }
        _jitInstrumentInlinees = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInstrumentInlinees"u8))), 1);
        _jitDisablePGO = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisablePGO"u8))), 0);
        if (Configuration.DEBUG)
        {
            _jitEnablePGORange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnablePGORange"u8))));
            _jitRandomEdgeCounts = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRandomEdgeCounts"u8))), 0);
            _jitCrossCheckDevirtualizationAndPGO = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCrossCheckDevirtualizationAndPGO"u8))), 0);
            _jitNoteFailedExactDevirtualization = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoteFailedExactDevirtualization"u8))), 0);
            _jitRandomlyCollect64BitCounts = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRandomlyCollect64BitCounts"u8))), 0);
            _jitSynthesizeCounts = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitSynthesizeCounts"u8))), 0);
            _jitPropagateSynthesizedCountsToProfileData = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitPropagateSynthesizedCountsToProfileData"u8))), 0);
            _jitSynthesisUseSolver = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitSynthesisUseSolver"u8))), 1);
            _jitSynthesisExceptionWeight = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitSynthesisExceptionWeight"u8))));
        }
        _jitEnableExactDevirtualization = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableExactDevirtualization"u8))), 1);
        _jitForceControlFlowGuard = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitForceControlFlowGuard"u8))), 0);
        _jitCFGUseDispatcher = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCFGUseDispatcher"u8))), 2);
        _jitEnableHeadTailMerge = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableHeadTailMerge"u8))), 1);
        _jitEnablePhysicalPromotion = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnablePhysicalPromotion"u8))), 1);
        _jitEnableCrossBlockLocalAssertionProp = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableCrossBlockLocalAssertionProp"u8))), 1);
        _jitEnablePostorderLocalAssertionProp = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnablePostorderLocalAssertionProp"u8))), 1);
        _jitEnableStrengthReduction = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableStrengthReduction"u8))), 1);
        _jitEnableInductionVariableOpts = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableInductionVariableOpts"u8))), 1);
        if (Configuration.DEBUG)
        {
            _jitFunctionFile = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitFunctionFile"u8))));
            var jitRawHexCodeValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRawHexCode"u8))));
            _jitRawHexCode = new MethodSet(jitRawHexCodeValue, jitHost);
            _jitRawHexCodeFile = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRawHexCodeFile"u8))));
        }
        if (Configuration.TARGET_ARM64)
        {
            if (Configuration.DEBUG)
            {
                _jitSaveFpLrWithCalleeSavedRegisters = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitSaveFpLrWithCalleeSavedRegisters"u8))), 0);
            }
        }
        if (Configuration.DEBUG)
        {
            _jitUseScalableVectorT = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitUseScalableVectorT"u8))), 0);
        }
        if (Configuration.TARGET_LOONGARCH64)
        {
            if (Configuration.DEBUG)
            {
                _jitDispIns = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDispIns"u8))), 0);
            }
        }
        if (Configuration.TARGET_WASM)
        {
            if (Configuration.DEBUG)
            {
                _jitR2RUnsupportedRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitR2RUnsupportedRange"u8))));
            }
            _jitWasmFunclets = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitWasmFunclets"u8))), 1);
        }
        _jitEnregStructLocals = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnregStructLocals"u8))), 1);
        _isInitialized = true;
    }
}