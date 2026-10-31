// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#nullable enable annotations

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public partial class Compiler
{
    internal sealed partial class DebugData
    {
        internal sealed class JitConfigValuesData
        {
            internal int _altJitLimit;
            internal int _altJitSkipOnAssert;
            internal int _breakOnDumpToken;
            internal int _debugBreakOnVerificationFailure;
            internal int _displayLoopHoistStats;
            internal int _displayLsraStats;
            internal unsafe byte* _jitLsraOrdering;
            internal int _enablePCRelAddr;
            internal int _jitAssertOnMaxRAPasses;
            internal int _jitBreakEmitOutputInstr;
            internal int _jitBreakMorphTree;
            internal int _jitBreakOnBadCode;
            internal int _jitBreakOnMinOpts;
            internal int _jitCloneLoops;
            internal int _jitCloneLoopsWithEH;
            internal int _jitCloneLoopsWithGdvTests;
            internal int _jitDebugLogLoopCloning;
            internal int _jitAlignLoopMinBlockWeight;
            internal int _jitAlignLoopMaxCodeSize;
            internal int _jitAlignLoopBoundary;
            internal int _jitAlignLoopForJcc;
            internal int _jitAlignLoopAdaptive;
            internal int _jitHideAlignBehindJmp;
            internal int _jitOptimizeStructHiddenBuffer;
            internal int _jitUnrollLoopMaxIterationCount;
            internal int _jitUnrollLoopsWithEH;
            internal int _jitDirectAlloc;
            internal int _jitDoubleAlign;
            internal int _jitEmitPrintRefRegs;
            internal int _jitEnableDevirtualization;
            internal int _jitEnableLateDevirtualization;
            internal int _jitExpensiveDebugCheckLevel;
            internal int _jitForceFallback;
            internal int _jitFullyInt;
            internal int _jitFunctionTrace;
            internal int _jitGCChecks;
            internal int _jitGCInfoLogging;
            internal int _jitHashBreak;
            internal int _jitHashHalt;
            internal int _jitInlineAdditionalMultiplier;
            internal int _jitInlinePrintStats;
            internal int _jitInlineSize;
            internal int _jitInlineDepth;
            internal int _jitForceInlineDepth;
            internal unsafe byte* _jitInlineMethodsWithEHRange;
            internal unsafe byte* _jitOptimizeAwaitRange;
            internal int _jitAwaitHashBreak;
            internal int _jitLongAddress;
            internal int _jitMaxUncheckedOffset;
            internal int _jitMinOpts;
            internal JitConfigValues.MethodSet _jitMinOptsName;
            internal int _jitMinOptsBbCount;
            internal int _jitMinOptsCodeSize;
            internal int _jitMinOptsInstrCount;
            internal int _jitMinOptsLvNumCount;
            internal int _jitMinOptsLvRefCount;
            internal int _jitNoCSE;
            internal int _jitNoCSE2;
            internal int _jitNoForceFallback;
            internal int _jitNoForwardSub;
            internal int _jitNoHoist;
            internal int _jitNoMemoryBarriers;
            internal int _jitNoStructPromotion;
            internal int _jitNoUnroll;
            internal int _jitOrder;
            internal int _jitQueryCurrentStaticFieldClass;
            internal int _jitReportFastTailCallDecisions;
            internal int _jitPInvokeCheckEnabled;
            internal int _jitPInvokeEnabled;
            internal int _jitHoistLimit;
            internal int _jitPrintInlinedMethodsVerbose;
            internal JitConfigValues.MethodSet _jitPrintInlinedMethods;
            internal JitConfigValues.MethodSet _jitPrintDevirtualizedMethods;
            internal int _jitProfileChecks;
            internal int _jitRequired;
            internal int _jitStackAllocToLocalSize;
            internal int _jitSkipArrayBoundCheck;
            internal int _jitEnablePhaseChecks;
            internal int _jitSplitFunctionSize;
            internal int _jitSsaStress;
            internal int _jitStackChecks;
            internal int _jitStress;
            internal int _jitStressBBProf;
            internal int _jitStressProcedureSplitting;
            internal int _jitStressRegs;
            internal unsafe byte* _jitStressRegsRange;
            internal int _jitStressSplitTreeLimit;
            internal int _jitVNMapSelLimit;
            internal int _runAltJitCode;
            internal int _runComponentUnitTests;
            internal int _shouldInjectFault;
            internal int _tailcallStress;
            internal JitConfigValues.MethodSet _jitBreak;
            internal JitConfigValues.MethodSet _jitDebugBreak;
            internal JitConfigValues.MethodSet _jitDump;
            internal int _jitHashDump;
            internal int _jitDumpTier0;
            internal int _jitDumpOSR;
            internal int _jitDumpAtOSROffset;
            internal int _jitDumpInlinePhases;
            internal int _jitDumpASCII;
            internal int _jitDumpTerseLsra;
            internal int _jitDumpToDebugger;
            internal int _jitDumpVerboseSsa;
            internal int _jitDumpVerboseTrees;
            internal int _jitDumpTreeIDs;
            internal int _jitDumpBeforeAfterMorph;
            internal int _jitDumpTerseNextBlock;
            internal JitConfigValues.MethodSet _jitEHDump;
            internal JitConfigValues.MethodSet _jitExclude;
            internal int _jitFakeProcedureSplitting;
            internal JitConfigValues.MethodSet _jitForceProcedureSplitting;
            internal JitConfigValues.MethodSet _jitGCDump;
            internal JitConfigValues.MethodSet _jitDebugDump;
            internal JitConfigValues.MethodSet _jitHalt;
            internal JitConfigValues.MethodSet _jitInclude;
            internal JitConfigValues.MethodSet _jitLateDisasm;
            internal unsafe byte* _jitLateDisasmTo;
            internal JitConfigValues.MethodSet _jitNoProcedureSplitting;
            internal JitConfigValues.MethodSet _jitNoProcedureSplittingEH;
            internal JitConfigValues.MethodSet _jitStressOnly;
            internal JitConfigValues.MethodSet _jitUnwindDump;
            internal JitConfigValues.MethodSet _jitDumpFg;
            internal int _jitDumpFgHash;
            internal int _jitDumpFgTier0;
            internal unsafe byte* _jitDumpFgDir;
            internal unsafe byte* _jitDumpFgFile;
            internal unsafe byte* _jitDumpFgPhase;
            internal unsafe byte* _jitDumpFgPrePhase;
            internal int _jitDumpFgDot;
            internal int _jitDumpFgEH;
            internal int _jitDumpFgLoops;
            internal int _jitDumpFgConstrained;
            internal int _jitDumpFgBlockID;
            internal int _jitDumpFgBlockFlags;
            internal int _jitDumpFgLoopFlags;
            internal int _jitDumpFgBlockOrder;
            internal int _jitDumpFgMemorySsa;
            internal unsafe byte* _jitRange;
            internal unsafe byte* _jitStressModeNames;
            internal int _jitStressModeNamesOnly;
            internal unsafe byte* _jitStressModeNamesAllow;
            internal unsafe byte* _jitStressModeNamesNot;
            internal unsafe byte* _jitStressRange;
            internal JitConfigValues.MethodSet _jitEmitUnitTests;
            internal unsafe byte* _jitEmitUnitTestsSections;
            internal unsafe byte* _jitDisasmAssemblies;
            internal int _jitDisasmWithGC;
            internal int _jitDisasmWithDebugInfo;
            internal int _jitDisasmSpilled;
            internal int _jitDisasmWithAddress;
            internal unsafe byte* _writeRichDebugInfoFile;
            internal int _jitStressRex2Encoding;
            internal int _jitStressPromotedEvexEncoding;
            internal int _jitStressEvexEncoding;
            internal int _jitCSEHash;
            internal int _jitCSEMask;
            internal int _jitMetrics;
            internal int _jitRandomCSE;
            internal unsafe byte* _jitReplayCSE;
            internal unsafe byte* _jitReplayCSEReward;
            internal unsafe byte* _jitRLCSE;
            internal unsafe byte* _jitRLCSEAlpha;
            internal int _jitRLCSECandidateFeatures;
            internal int _jitRLHook;
            internal int _jitRLHookEmitFeatureNames;
            internal unsafe byte* _jitRLHookCSEDecisions;
            internal int _jitEnregStats;
            internal int _jitDoAssertionProp;
            internal int _jitDoCopyProp;
            internal int _jitDoOptimizeIVs;
            internal int _jitDoEarlyProp;
            internal int _jitDoLoopHoisting;
            internal int _jitDoLoopInversion;
            internal int _jitDoRangeAnalysis;
            internal int _jitDoVNBasedDeadStoreRemoval;
            internal int _jitDoRedundantBranchOpts;
            internal unsafe byte* _jitEnableRboRange;
            internal unsafe byte* _jitEnableHeadTailMergeRange;
            internal unsafe byte* _jitEnableVNBasedDeadStoreRemovalRange;
            internal unsafe byte* _jitEnableEarlyLivenessRange;
            internal unsafe byte* _jitOnlyOptimizeRange;
            internal unsafe byte* _jitEnablePhysicalPromotionRange;
            internal unsafe byte* _jitEnableCrossBlockLocalAssertionPropRange;
            internal unsafe byte* _jitEnableInductionVariableOptsRange;
            internal unsafe byte* _jitEnableLocalAddrPropagationRange;
            internal int _jitDoSsa;
            internal int _jitDoValueNumber;
            internal unsafe byte* _jitOptRepeatRange;
            internal int _jitDoIfConversion;
            internal int _jitDoOptimizeMaskConversions;
            internal int _jitOptimizeAwait;
            internal unsafe byte* _jitAsyncDefaultValueAnalysisRange;
            internal unsafe byte* _jitAsyncPreservedValueAnalysisRange;
            internal int _enableExtraSuperPmiQueries;
            internal int _jitInlineDumpData;
            internal int _jitInlineDumpXml;
            internal unsafe byte* _jitInlineDumpXmlFile;
            internal int _jitInlinePolicyDumpXml;
            internal int _jitInlineLimit;
            internal int _jitInlinePolicyDiscretionary;
            internal int _jitInlinePolicyFull;
            internal int _jitInlinePolicySize;
            internal int _jitInlinePolicyRandom;
            internal int _jitInlinePolicyReplay;
            internal unsafe byte* _jitNoInlineRange;
            internal unsafe byte* _jitInlineReplayFile;
            internal int _jitStressAsyncInlining;
            internal int _jitStressAsyncInliningMaxDepth;
            internal int _jitStressAsyncInliningPct;
            internal unsafe byte* _jitObjectStackAllocationRange;
            internal unsafe byte* _jitObjectStackAllocationConditionalEscapeRange;
            internal unsafe byte* _jitObjectStackAllocationTrackFieldsRange;
            internal int _jitObjectStackAllocationDumpConnGraph;
            internal int _jitEnableFinallyCloning;
            internal int _jitEnableRemoveEmptyTry;
            internal int _jitEnableRemoveEmptyTryCatchOrTryFault;
            internal int _jitEnableRemoveUnreachableTry;
            internal unsafe byte* _jitGuardedDevirtualizationRange;
            internal int _jitRandomGuardedDevirtualization;
            internal int _jitRandomPartialCompilation;
            internal int _jitRandomOnStackReplacement;
            internal int _jitOffsetOnStackReplacement;
            internal unsafe byte* _jitEnableOsrRange;
            internal unsafe byte* _jitEnablePatchpointRange;
            internal int _jitInstrumentIfOptimizing;
            internal unsafe byte* _jitInstrumentIfOptimizingRange;
            internal unsafe byte* _jitEnablePGORange;
            internal int _jitRandomEdgeCounts;
            internal int _jitCrossCheckDevirtualizationAndPGO;
            internal int _jitNoteFailedExactDevirtualization;
            internal int _jitRandomlyCollect64BitCounts;
            internal int _jitSynthesizeCounts;
            internal int _jitPropagateSynthesizedCountsToProfileData;
            internal int _jitSynthesisUseSolver;
            internal unsafe byte* _jitSynthesisExceptionWeight;
            internal unsafe byte* _jitFunctionFile;
            internal JitConfigValues.MethodSet _jitRawHexCode;
            internal unsafe byte* _jitRawHexCodeFile;
            internal int _jitSaveFpLrWithCalleeSavedRegisters;
            internal int _jitUseScalableVectorT;
            internal int _jitDispIns;
            internal unsafe byte* _jitR2RUnsupportedRange;
            internal JitConfigValuesData Clone() => (JitConfigValuesData)MemberwiseClone();
        }
    }
}

public partial struct JitConfigValues
{
    private ConfigurationData? _configurationData;

    private int _jitCloneLoopsSizeLimit;
    private int _jitCloneLoopsMinPerCallRatio;
    private int _jitEnableStoreLclFldCoalescing;
    private int _jitInlineBudget;
    private int _jitInlineMethodsWithEH;
    private int _jitEnableGenericVirtualDevirtualization;
    private MethodSet _jitDisasm;
    private int _jitDisasmTesting;
    private int _jitDisasmDiffable;
    private int _jitDisasmSummary;
    private int _jitDisasmOnlyOptimized;
    private int _jitDisasmWithAlignmentBoundaries;
    private int _jitDisasmWithCodeBytes;
    private unsafe byte* _jitStdOutFile;
    private int _richDebugInfo;
    private int _jitAlignLoops;
    private int _altJitAssertOnNYI;
    private int _enableEHWriteThru;
    private int _enableMultiRegLocals;
    private int _jitNoInline;
    private int _enableHWIntrinsic;
    private int _enableEmbeddedBroadcast;
    private int _enableEmbeddedMasking;
    private int _enableApxNDD;
    private int _enableApxConditionalChaining;
    private int _enableApxPPHint;
    private int _enableApxPP2;
    private int _enableApxZU;
    private int _jitConstCSE;
    private int _jitRLCSEGreedy;
    private int _jitRLCSEVerbose;
    private int _jitEnableNoWayAssert;
    private int _displayMemStats;
    private int _jitAggressiveInlining;
    private int _jitELTHookEnabled;
    private int _jitInlineSIMDMultiplier;
    private int _jitMaxLocalsToTrack;
    private int _jitLoopInversionSizeLimit;
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
    private int _jitObjectStackAllocation;
    private int _jitObjectStackAllocationRefClass;
    private int _jitObjectStackAllocationBoxedValueClass;
    private int _jitObjectStackAllocationConditionalEscape;
    private int _jitObjectStackAllocationArray;
    private int _jitObjectStackAllocationSize;
    private int _jitObjectStackAllocationTrackFields;
    private int _jitEECallTimingInfo;
    private int _jitEnableGuardedDevirtualization;
    private int _jitGuardedDevirtualizationMaxTypeChecks;
    private int _jitGuardedDevirtualizationRequireInlining;
    private int _jitGuardedDevirtualizationChainLikelihood;
    private int _jitGuardedDevirtualizationChainStatements;
    private int _tC_OnStackReplacement;
    private int _tC_OnStackReplacement_InitialCounter;
    private int _tC_PartialCompilation;
    private int _tC_PatchpointStrategy;
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
    private int _jitInstrumentInlinees;
    private int _jitDisablePGO;
    private int _jitEnableExactDevirtualization;
    private int _jitForceControlFlowGuard;
    private int _jitCFGUseDispatcher;
    private int _jitEnableHeadTailMerge;
    private int _jitEnablePhysicalPromotion;
    private int _jitEnableCrossBlockLocalAssertionProp;
    private int _jitEnablePostorderLocalAssertionProp;
    private int _jitEnableStrengthReduction;
    private int _jitEnableInductionVariableOpts;
    private int _jitEnregStructLocals;

    internal sealed class FeatureSimdData
    {
        internal int _jitDisableSimdVN;
    }

    internal sealed class TargetArm64Data
    {
        internal int _jitPacEnabled;
        internal int _enableArm64Aes;
        internal int _enableArm64Atomics;
        internal int _enableArm64Crc32;
        internal int _enableArm64Cssc;
        internal int _enableArm64Dczva;
        internal int _enableArm64Dp;
        internal int _enableArm64Fp16;
        internal int _enableArm64Rdm;
        internal int _enableArm64Sha1;
        internal int _enableArm64Sha256;
        internal int _enableArm64Sve;
        internal int _enableArm64Sve2;
        internal int _enableArm64Sha3;
        internal int _enableArm64Sm4;
        internal int _enableArm64SveAes;
        internal int _enableArm64SvePmull128;
        internal int _enableArm64SveSha3;
        internal int _enableArm64SveSm4;
    }

    internal sealed class TargetRiscV64Data
    {
        internal int _enableRiscV64Zba;
        internal int _enableRiscV64Zbb;
        internal int _enableRiscV64Zbs;
        internal int _enableRiscV64Zicond;
    }

    internal sealed class TargetWasmData
    {
        internal int _jitWasmFunclets;
    }

    internal sealed class TargetXarchData
    {
        internal int _enableAVX;
        internal int _enableAVX2;
        internal int _enableAVX512;
        internal int _enableAVX512BMM;
        internal int _enableAVX512v2;
        internal int _enableAVX512v3;
        internal int _enableAVX10v1;
        internal int _enableAVX10v2;
        internal int _enableAPX;
        internal int _enableAES;
        internal int _enableAVX512VP2INTERSECT;
        internal int _enableAVXIFMA;
        internal int _enableAVXVNNI;
        internal int _enableAVXVNNIINT;
        internal int _enableGFNI;
        internal int _enableSHA;
        internal int _enableVAES;
        internal int _enableWAITPKG;
        internal int _enableX86Serialize;
    }


    private sealed class ConfigurationData
    {
        internal Compiler.DebugData.JitConfigValuesData? _debugData;
        internal FeatureSimdData? _featureSimdData;
        internal TargetArm64Data? _targetArm64Data;
        internal TargetRiscV64Data? _targetRiscV64Data;
        internal TargetWasmData? _targetWasmData;
        internal TargetXarchData? _targetXarchData;
        internal ConfigurationData Clone() => (ConfigurationData)MemberwiseClone();
    }

    public int AltJitLimit
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._altJitLimit;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "AltJitLimit" : default);
            return default!;
        }
    }
    public int AltJitSkipOnAssert
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._altJitSkipOnAssert;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "AltJitSkipOnAssert" : default);
            return default!;
        }
    }
    public int BreakOnDumpToken
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._breakOnDumpToken;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "BreakOnDumpToken" : default);
            return default!;
        }
    }
    public int DebugBreakOnVerificationFailure
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._debugBreakOnVerificationFailure;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "DebugBreakOnVerificationFailure" : default);
            return default!;
        }
    }
    public int DisplayLoopHoistStats
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._displayLoopHoistStats;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "DisplayLoopHoistStats" : default);
            return default!;
        }
    }
    public int DisplayLsraStats
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._displayLsraStats;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "DisplayLsraStats" : default);
            return default!;
        }
    }
    public unsafe byte* JitLsraOrdering
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitLsraOrdering;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitLsraOrdering" : default);
            return default!;
        }
    }
    public int EnablePCRelAddr
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._enablePCRelAddr;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "EnablePCRelAddr" : default);
            return default!;
        }
    }
    public int JitAssertOnMaxRAPasses
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitAssertOnMaxRAPasses;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitAssertOnMaxRAPasses" : default);
            return default!;
        }
    }
    public int JitBreakEmitOutputInstr
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitBreakEmitOutputInstr;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitBreakEmitOutputInstr" : default);
            return default!;
        }
    }
    public int JitBreakMorphTree
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitBreakMorphTree;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitBreakMorphTree" : default);
            return default!;
        }
    }
    public int JitBreakOnBadCode
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitBreakOnBadCode;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitBreakOnBadCode" : default);
            return default!;
        }
    }
    public int JitBreakOnMinOpts
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitBreakOnMinOpts;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitBreakOnMinOpts" : default);
            return default!;
        }
    }
    public int JitCloneLoops
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitCloneLoops;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitCloneLoops" : default);
            return default!;
        }
    }
    public int JitCloneLoopsWithEH
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitCloneLoopsWithEH;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitCloneLoopsWithEH" : default);
            return default!;
        }
    }
    public int JitCloneLoopsWithGdvTests
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitCloneLoopsWithGdvTests;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitCloneLoopsWithGdvTests" : default);
            return default!;
        }
    }
    public int JitCloneLoopsSizeLimit => _jitCloneLoopsSizeLimit;
    public int JitCloneLoopsMinPerCallRatio => _jitCloneLoopsMinPerCallRatio;
    public int JitDebugLogLoopCloning
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDebugLogLoopCloning;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDebugLogLoopCloning" : default);
            return default!;
        }
    }
    public int JitAlignLoopMinBlockWeight
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitAlignLoopMinBlockWeight;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitAlignLoopMinBlockWeight" : default);
            return default!;
        }
    }
    public int JitAlignLoopMaxCodeSize
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitAlignLoopMaxCodeSize;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitAlignLoopMaxCodeSize" : default);
            return default!;
        }
    }
    public int JitAlignLoopBoundary
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitAlignLoopBoundary;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitAlignLoopBoundary" : default);
            return default!;
        }
    }
    public int JitAlignLoopForJcc
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitAlignLoopForJcc;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitAlignLoopForJcc" : default);
            return default!;
        }
    }
    public int JitAlignLoopAdaptive
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitAlignLoopAdaptive;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitAlignLoopAdaptive" : default);
            return default!;
        }
    }
    public int JitHideAlignBehindJmp
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitHideAlignBehindJmp;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitHideAlignBehindJmp" : default);
            return default!;
        }
    }
    public int JitOptimizeStructHiddenBuffer
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitOptimizeStructHiddenBuffer;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitOptimizeStructHiddenBuffer" : default);
            return default!;
        }
    }
    public int JitEnableStoreLclFldCoalescing => _jitEnableStoreLclFldCoalescing;
    public int JitUnrollLoopMaxIterationCount
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitUnrollLoopMaxIterationCount;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitUnrollLoopMaxIterationCount" : default);
            return default!;
        }
    }
    public int JitUnrollLoopsWithEH
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitUnrollLoopsWithEH;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitUnrollLoopsWithEH" : default);
            return default!;
        }
    }
    public int JitDirectAlloc
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDirectAlloc;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDirectAlloc" : default);
            return default!;
        }
    }
    public int JitDoubleAlign
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDoubleAlign;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDoubleAlign" : default);
            return default!;
        }
    }
    public int JitEmitPrintRefRegs
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitEmitPrintRefRegs;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEmitPrintRefRegs" : default);
            return default!;
        }
    }
    public int JitEnableDevirtualization
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitEnableDevirtualization;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnableDevirtualization" : default);
            return default!;
        }
    }
    public int JitEnableLateDevirtualization
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitEnableLateDevirtualization;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnableLateDevirtualization" : default);
            return default!;
        }
    }
    public int JitExpensiveDebugCheckLevel
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitExpensiveDebugCheckLevel;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitExpensiveDebugCheckLevel" : default);
            return default!;
        }
    }
    public int JitForceFallback
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitForceFallback;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitForceFallback" : default);
            return default!;
        }
    }
    public int JitFullyInt
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitFullyInt;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitFullyInt" : default);
            return default!;
        }
    }
    public int JitFunctionTrace
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitFunctionTrace;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitFunctionTrace" : default);
            return default!;
        }
    }
    public int JitGCChecks
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitGCChecks;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitGCChecks" : default);
            return default!;
        }
    }
    public int JitGCInfoLogging
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitGCInfoLogging;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitGCInfoLogging" : default);
            return default!;
        }
    }
    public int JitHashBreak
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitHashBreak;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitHashBreak" : default);
            return default!;
        }
    }
    public int JitHashHalt
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitHashHalt;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitHashHalt" : default);
            return default!;
        }
    }
    public int JitInlineAdditionalMultiplier
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitInlineAdditionalMultiplier;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlineAdditionalMultiplier" : default);
            return default!;
        }
    }
    public int JitInlinePrintStats
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitInlinePrintStats;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlinePrintStats" : default);
            return default!;
        }
    }
    public int JitInlineSize
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitInlineSize;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlineSize" : default);
            return default!;
        }
    }
    public int JitInlineDepth
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitInlineDepth;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlineDepth" : default);
            return default!;
        }
    }
    public int JitInlineBudget => _jitInlineBudget;
    public int JitForceInlineDepth
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitForceInlineDepth;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitForceInlineDepth" : default);
            return default!;
        }
    }
    public int JitInlineMethodsWithEH => _jitInlineMethodsWithEH;
    public unsafe byte* JitInlineMethodsWithEHRange
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitInlineMethodsWithEHRange;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlineMethodsWithEHRange" : default);
            return default!;
        }
    }
    public unsafe byte* JitOptimizeAwaitRange
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitOptimizeAwaitRange;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitOptimizeAwaitRange" : default);
            return default!;
        }
    }
    public int JitAwaitHashBreak
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitAwaitHashBreak;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitAwaitHashBreak" : default);
            return default!;
        }
    }
    public int JitLongAddress
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitLongAddress;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitLongAddress" : default);
            return default!;
        }
    }
    public int JitMaxUncheckedOffset
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitMaxUncheckedOffset;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitMaxUncheckedOffset" : default);
            return default!;
        }
    }
    public int JitPacEnabled => _configurationData is { } configurationData ? Configuration.TARGET_ARM64 && configurationData._targetArm64Data is { } sidecarData0 ? sidecarData0._jitPacEnabled : default : default;
    public int JitEnableGenericVirtualDevirtualization => _jitEnableGenericVirtualDevirtualization;
    public int JitMinOpts
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitMinOpts;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitMinOpts" : default);
            return default!;
        }
    }
    public MethodSet JitMinOptsName
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitMinOptsName;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitMinOptsName" : default);
            return default!;
        }
    }
    public int JitMinOptsBbCount
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitMinOptsBbCount;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitMinOptsBbCount" : default);
            return default!;
        }
    }
    public int JitMinOptsCodeSize
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitMinOptsCodeSize;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitMinOptsCodeSize" : default);
            return default!;
        }
    }
    public int JitMinOptsInstrCount
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitMinOptsInstrCount;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitMinOptsInstrCount" : default);
            return default!;
        }
    }
    public int JitMinOptsLvNumCount
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitMinOptsLvNumCount;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitMinOptsLvNumCount" : default);
            return default!;
        }
    }
    public int JitMinOptsLvRefCount
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitMinOptsLvRefCount;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitMinOptsLvRefCount" : default);
            return default!;
        }
    }
    public int JitNoCSE
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitNoCSE;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitNoCSE" : default);
            return default!;
        }
    }
    public int JitNoCSE2
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitNoCSE2;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitNoCSE2" : default);
            return default!;
        }
    }
    public int JitNoForceFallback
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitNoForceFallback;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitNoForceFallback" : default);
            return default!;
        }
    }
    public int JitNoForwardSub
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitNoForwardSub;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitNoForwardSub" : default);
            return default!;
        }
    }
    public int JitNoHoist
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitNoHoist;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitNoHoist" : default);
            return default!;
        }
    }
    public int JitNoMemoryBarriers
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitNoMemoryBarriers;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitNoMemoryBarriers" : default);
            return default!;
        }
    }
    public int JitNoStructPromotion
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitNoStructPromotion;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitNoStructPromotion" : default);
            return default!;
        }
    }
    public int JitNoUnroll
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitNoUnroll;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitNoUnroll" : default);
            return default!;
        }
    }
    public int JitOrder
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitOrder;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitOrder" : default);
            return default!;
        }
    }
    public int JitQueryCurrentStaticFieldClass
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitQueryCurrentStaticFieldClass;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitQueryCurrentStaticFieldClass" : default);
            return default!;
        }
    }
    public int JitReportFastTailCallDecisions
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitReportFastTailCallDecisions;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitReportFastTailCallDecisions" : default);
            return default!;
        }
    }
    public int JitPInvokeCheckEnabled
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitPInvokeCheckEnabled;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitPInvokeCheckEnabled" : default);
            return default!;
        }
    }
    public int JitPInvokeEnabled
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitPInvokeEnabled;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitPInvokeEnabled" : default);
            return default!;
        }
    }
    public int JitHoistLimit
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitHoistLimit;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitHoistLimit" : default);
            return default!;
        }
    }
    public int JitPrintInlinedMethodsVerbose
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitPrintInlinedMethodsVerbose;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitPrintInlinedMethodsVerbose" : default);
            return default!;
        }
    }
    public MethodSet JitPrintInlinedMethods
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitPrintInlinedMethods;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitPrintInlinedMethods" : default);
            return default!;
        }
    }
    public MethodSet JitPrintDevirtualizedMethods
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitPrintDevirtualizedMethods;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitPrintDevirtualizedMethods" : default);
            return default!;
        }
    }
    public int JitProfileChecks
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitProfileChecks;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitProfileChecks" : default);
            return default!;
        }
    }
    public int JitRequired
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitRequired;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRequired" : default);
            return default!;
        }
    }
    public int JitStackAllocToLocalSize
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitStackAllocToLocalSize;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStackAllocToLocalSize" : default);
            return default!;
        }
    }
    public int JitSkipArrayBoundCheck
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitSkipArrayBoundCheck;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitSkipArrayBoundCheck" : default);
            return default!;
        }
    }
    public int JitEnablePhaseChecks
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitEnablePhaseChecks;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnablePhaseChecks" : default);
            return default!;
        }
    }
    public int JitSplitFunctionSize
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitSplitFunctionSize;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitSplitFunctionSize" : default);
            return default!;
        }
    }
    public int JitSsaStress
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitSsaStress;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitSsaStress" : default);
            return default!;
        }
    }
    public int JitStackChecks
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitStackChecks;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStackChecks" : default);
            return default!;
        }
    }
    public int JitStress
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitStress;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStress" : default);
            return default!;
        }
    }
    public int JitStressBBProf
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitStressBBProf;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressBBProf" : default);
            return default!;
        }
    }
    public int JitStressProcedureSplitting
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitStressProcedureSplitting;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressProcedureSplitting" : default);
            return default!;
        }
    }
    public int JitStressRegs
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitStressRegs;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressRegs" : default);
            return default!;
        }
    }
    public unsafe byte* JitStressRegsRange
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitStressRegsRange;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressRegsRange" : default);
            return default!;
        }
    }
    public int JitStressSplitTreeLimit
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitStressSplitTreeLimit;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressSplitTreeLimit" : default);
            return default!;
        }
    }
    public int JitVNMapSelLimit
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitVNMapSelLimit;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitVNMapSelLimit" : default);
            return default!;
        }
    }
    public int RunAltJitCode
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._runAltJitCode;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "RunAltJitCode" : default);
            return default!;
        }
    }
    public int RunComponentUnitTests
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._runComponentUnitTests;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "RunComponentUnitTests" : default);
            return default!;
        }
    }
    public int ShouldInjectFault
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._shouldInjectFault;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "ShouldInjectFault" : default);
            return default!;
        }
    }
    public int TailcallStress
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._tailcallStress;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "TailcallStress" : default);
            return default!;
        }
    }
    public MethodSet JitBreak
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitBreak;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitBreak" : default);
            return default!;
        }
    }
    public MethodSet JitDebugBreak
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDebugBreak;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDebugBreak" : default);
            return default!;
        }
    }
    public MethodSet JitDump
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDump;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDump" : default);
            return default!;
        }
    }
    public int JitHashDump
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitHashDump;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitHashDump" : default);
            return default!;
        }
    }
    public int JitDumpTier0
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDumpTier0;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpTier0" : default);
            return default!;
        }
    }
    public int JitDumpOSR
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDumpOSR;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpOSR" : default);
            return default!;
        }
    }
    public int JitDumpAtOSROffset
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDumpAtOSROffset;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpAtOSROffset" : default);
            return default!;
        }
    }
    public int JitDumpInlinePhases
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDumpInlinePhases;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpInlinePhases" : default);
            return default!;
        }
    }
    public int JitDumpASCII
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDumpASCII;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpASCII" : default);
            return default!;
        }
    }
    public int JitDumpTerseLsra
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDumpTerseLsra;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpTerseLsra" : default);
            return default!;
        }
    }
    public int JitDumpToDebugger
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDumpToDebugger;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpToDebugger" : default);
            return default!;
        }
    }
    public int JitDumpVerboseSsa
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDumpVerboseSsa;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpVerboseSsa" : default);
            return default!;
        }
    }
    public int JitDumpVerboseTrees
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDumpVerboseTrees;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpVerboseTrees" : default);
            return default!;
        }
    }
    public int JitDumpTreeIDs
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDumpTreeIDs;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpTreeIDs" : default);
            return default!;
        }
    }
    public int JitDumpBeforeAfterMorph
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDumpBeforeAfterMorph;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpBeforeAfterMorph" : default);
            return default!;
        }
    }
    public int JitDumpTerseNextBlock
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDumpTerseNextBlock;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpTerseNextBlock" : default);
            return default!;
        }
    }
    public MethodSet JitEHDump
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitEHDump;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEHDump" : default);
            return default!;
        }
    }
    public MethodSet JitExclude
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitExclude;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitExclude" : default);
            return default!;
        }
    }
    public int JitFakeProcedureSplitting
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitFakeProcedureSplitting;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitFakeProcedureSplitting" : default);
            return default!;
        }
    }
    public MethodSet JitForceProcedureSplitting
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitForceProcedureSplitting;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitForceProcedureSplitting" : default);
            return default!;
        }
    }
    public MethodSet JitGCDump
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitGCDump;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitGCDump" : default);
            return default!;
        }
    }
    public MethodSet JitDebugDump
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDebugDump;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDebugDump" : default);
            return default!;
        }
    }
    public MethodSet JitHalt
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitHalt;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitHalt" : default);
            return default!;
        }
    }
    public MethodSet JitInclude
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitInclude;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInclude" : default);
            return default!;
        }
    }
    public MethodSet JitLateDisasm
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitLateDisasm;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitLateDisasm" : default);
            return default!;
        }
    }
    public unsafe byte* JitLateDisasmTo
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitLateDisasmTo;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitLateDisasmTo" : default);
            return default!;
        }
    }
    public MethodSet JitNoProcedureSplitting
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitNoProcedureSplitting;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitNoProcedureSplitting" : default);
            return default!;
        }
    }
    public MethodSet JitNoProcedureSplittingEH
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitNoProcedureSplittingEH;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitNoProcedureSplittingEH" : default);
            return default!;
        }
    }
    public MethodSet JitStressOnly
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitStressOnly;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressOnly" : default);
            return default!;
        }
    }
    public MethodSet JitUnwindDump
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitUnwindDump;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitUnwindDump" : default);
            return default!;
        }
    }
    public MethodSet JitDumpFg
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDumpFg;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFg" : default);
            return default!;
        }
    }
    public int JitDumpFgHash
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDumpFgHash;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgHash" : default);
            return default!;
        }
    }
    public int JitDumpFgTier0
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDumpFgTier0;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgTier0" : default);
            return default!;
        }
    }
    public unsafe byte* JitDumpFgDir
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDumpFgDir;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgDir" : default);
            return default!;
        }
    }
    public unsafe byte* JitDumpFgFile
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDumpFgFile;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgFile" : default);
            return default!;
        }
    }
    public unsafe byte* JitDumpFgPhase
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDumpFgPhase;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgPhase" : default);
            return default!;
        }
    }
    public unsafe byte* JitDumpFgPrePhase
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDumpFgPrePhase;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgPrePhase" : default);
            return default!;
        }
    }
    public int JitDumpFgDot
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDumpFgDot;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgDot" : default);
            return default!;
        }
    }
    public int JitDumpFgEH
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDumpFgEH;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgEH" : default);
            return default!;
        }
    }
    public int JitDumpFgLoops
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDumpFgLoops;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgLoops" : default);
            return default!;
        }
    }
    public int JitDumpFgConstrained
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDumpFgConstrained;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgConstrained" : default);
            return default!;
        }
    }
    public int JitDumpFgBlockID
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDumpFgBlockID;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgBlockID" : default);
            return default!;
        }
    }
    public int JitDumpFgBlockFlags
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDumpFgBlockFlags;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgBlockFlags" : default);
            return default!;
        }
    }
    public int JitDumpFgLoopFlags
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDumpFgLoopFlags;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgLoopFlags" : default);
            return default!;
        }
    }
    public int JitDumpFgBlockOrder
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDumpFgBlockOrder;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgBlockOrder" : default);
            return default!;
        }
    }
    public int JitDumpFgMemorySsa
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDumpFgMemorySsa;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDumpFgMemorySsa" : default);
            return default!;
        }
    }
    public unsafe byte* JitRange
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitRange;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRange" : default);
            return default!;
        }
    }
    public unsafe byte* JitStressModeNames
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitStressModeNames;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressModeNames" : default);
            return default!;
        }
    }
    public int JitStressModeNamesOnly
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitStressModeNamesOnly;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressModeNamesOnly" : default);
            return default!;
        }
    }
    public unsafe byte* JitStressModeNamesAllow
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitStressModeNamesAllow;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressModeNamesAllow" : default);
            return default!;
        }
    }
    public unsafe byte* JitStressModeNamesNot
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitStressModeNamesNot;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressModeNamesNot" : default);
            return default!;
        }
    }
    public unsafe byte* JitStressRange
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitStressRange;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressRange" : default);
            return default!;
        }
    }
    public MethodSet JitEmitUnitTests
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitEmitUnitTests;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEmitUnitTests" : default);
            return default!;
        }
    }
    public unsafe byte* JitEmitUnitTestsSections
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitEmitUnitTestsSections;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEmitUnitTestsSections" : default);
            return default!;
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
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDisasmAssemblies;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDisasmAssemblies" : default);
            return default!;
        }
    }
    public int JitDisasmWithGC
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDisasmWithGC;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDisasmWithGC" : default);
            return default!;
        }
    }
    public int JitDisasmWithDebugInfo
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDisasmWithDebugInfo;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDisasmWithDebugInfo" : default);
            return default!;
        }
    }
    public int JitDisasmSpilled
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDisasmSpilled;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDisasmSpilled" : default);
            return default!;
        }
    }
    public int JitDisasmWithAddress
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDisasmWithAddress;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDisasmWithAddress" : default);
            return default!;
        }
    }
    public unsafe byte* JitStdOutFile => _jitStdOutFile;
    public int RichDebugInfo => _richDebugInfo;
    public unsafe byte* WriteRichDebugInfoFile
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._writeRichDebugInfoFile;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "WriteRichDebugInfoFile" : default);
            return default!;
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
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitStressRex2Encoding;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressRex2Encoding" : default);
            return default!;
        }
    }
    public int JitStressPromotedEvexEncoding
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitStressPromotedEvexEncoding;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressPromotedEvexEncoding" : default);
            return default!;
        }
    }
    public int JitStressEvexEncoding
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitStressEvexEncoding;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressEvexEncoding" : default);
            return default!;
        }
    }
    public int EnableHWIntrinsic => _enableHWIntrinsic;
    public int EnableAVX => _configurationData is { } configurationData ? Configuration.TARGET_XARCH && configurationData._targetXarchData is { } sidecarData0 ? sidecarData0._enableAVX : default : default;
    public int EnableAVX2 => _configurationData is { } configurationData ? Configuration.TARGET_XARCH && configurationData._targetXarchData is { } sidecarData0 ? sidecarData0._enableAVX2 : default : default;
    public int EnableAVX512 => _configurationData is { } configurationData ? Configuration.TARGET_XARCH && configurationData._targetXarchData is { } sidecarData0 ? sidecarData0._enableAVX512 : default : default;
    public int EnableAVX512BMM => _configurationData is { } configurationData ? Configuration.TARGET_XARCH && configurationData._targetXarchData is { } sidecarData0 ? sidecarData0._enableAVX512BMM : default : default;
    public int EnableAVX512v2 => _configurationData is { } configurationData ? Configuration.TARGET_XARCH && configurationData._targetXarchData is { } sidecarData0 ? sidecarData0._enableAVX512v2 : default : default;
    public int EnableAVX512v3 => _configurationData is { } configurationData ? Configuration.TARGET_XARCH && configurationData._targetXarchData is { } sidecarData0 ? sidecarData0._enableAVX512v3 : default : default;
    public int EnableAVX10v1 => _configurationData is { } configurationData ? Configuration.TARGET_XARCH && configurationData._targetXarchData is { } sidecarData0 ? sidecarData0._enableAVX10v1 : default : default;
    public int EnableAVX10v2 => _configurationData is { } configurationData ? Configuration.TARGET_XARCH && configurationData._targetXarchData is { } sidecarData0 ? sidecarData0._enableAVX10v2 : default : default;
    public int EnableAPX => _configurationData is { } configurationData ? Configuration.TARGET_XARCH && configurationData._targetXarchData is { } sidecarData0 ? sidecarData0._enableAPX : default : default;
    public int EnableAES => _configurationData is { } configurationData ? Configuration.TARGET_XARCH && configurationData._targetXarchData is { } sidecarData0 ? sidecarData0._enableAES : default : default;
    public int EnableAVX512VP2INTERSECT => _configurationData is { } configurationData ? Configuration.TARGET_XARCH && configurationData._targetXarchData is { } sidecarData0 ? sidecarData0._enableAVX512VP2INTERSECT : default : default;
    public int EnableAVXIFMA => _configurationData is { } configurationData ? Configuration.TARGET_XARCH && configurationData._targetXarchData is { } sidecarData0 ? sidecarData0._enableAVXIFMA : default : default;
    public int EnableAVXVNNI => _configurationData is { } configurationData ? Configuration.TARGET_XARCH && configurationData._targetXarchData is { } sidecarData0 ? sidecarData0._enableAVXVNNI : default : default;
    public int EnableAVXVNNIINT => _configurationData is { } configurationData ? Configuration.TARGET_XARCH && configurationData._targetXarchData is { } sidecarData0 ? sidecarData0._enableAVXVNNIINT : default : default;
    public int EnableGFNI => _configurationData is { } configurationData ? Configuration.TARGET_XARCH && configurationData._targetXarchData is { } sidecarData0 ? sidecarData0._enableGFNI : default : default;
    public int EnableSHA => _configurationData is { } configurationData ? Configuration.TARGET_XARCH && configurationData._targetXarchData is { } sidecarData0 ? sidecarData0._enableSHA : default : default;
    public int EnableVAES => _configurationData is { } configurationData ? Configuration.TARGET_XARCH && configurationData._targetXarchData is { } sidecarData0 ? sidecarData0._enableVAES : default : default;
    public int EnableWAITPKG => _configurationData is { } configurationData ? Configuration.TARGET_XARCH && configurationData._targetXarchData is { } sidecarData0 ? sidecarData0._enableWAITPKG : default : default;
    public int EnableX86Serialize => _configurationData is { } configurationData ? Configuration.TARGET_XARCH && configurationData._targetXarchData is { } sidecarData0 ? sidecarData0._enableX86Serialize : default : default;
    public int EnableArm64Aes => _configurationData is { } configurationData ? Configuration.TARGET_ARM64 && configurationData._targetArm64Data is { } sidecarData0 ? sidecarData0._enableArm64Aes : default : default;
    public int EnableArm64Atomics => _configurationData is { } configurationData ? Configuration.TARGET_ARM64 && configurationData._targetArm64Data is { } sidecarData0 ? sidecarData0._enableArm64Atomics : default : default;
    public int EnableArm64Crc32 => _configurationData is { } configurationData ? Configuration.TARGET_ARM64 && configurationData._targetArm64Data is { } sidecarData0 ? sidecarData0._enableArm64Crc32 : default : default;
    public int EnableArm64Cssc => _configurationData is { } configurationData ? Configuration.TARGET_ARM64 && configurationData._targetArm64Data is { } sidecarData0 ? sidecarData0._enableArm64Cssc : default : default;
    public int EnableArm64Dczva => _configurationData is { } configurationData ? Configuration.TARGET_ARM64 && configurationData._targetArm64Data is { } sidecarData0 ? sidecarData0._enableArm64Dczva : default : default;
    public int EnableArm64Dp => _configurationData is { } configurationData ? Configuration.TARGET_ARM64 && configurationData._targetArm64Data is { } sidecarData0 ? sidecarData0._enableArm64Dp : default : default;
    public int EnableArm64Fp16 => _configurationData is { } configurationData ? Configuration.TARGET_ARM64 && configurationData._targetArm64Data is { } sidecarData0 ? sidecarData0._enableArm64Fp16 : default : default;
    public int EnableArm64Rdm => _configurationData is { } configurationData ? Configuration.TARGET_ARM64 && configurationData._targetArm64Data is { } sidecarData0 ? sidecarData0._enableArm64Rdm : default : default;
    public int EnableArm64Sha1 => _configurationData is { } configurationData ? Configuration.TARGET_ARM64 && configurationData._targetArm64Data is { } sidecarData0 ? sidecarData0._enableArm64Sha1 : default : default;
    public int EnableArm64Sha256 => _configurationData is { } configurationData ? Configuration.TARGET_ARM64 && configurationData._targetArm64Data is { } sidecarData0 ? sidecarData0._enableArm64Sha256 : default : default;
    public int EnableArm64Sve => _configurationData is { } configurationData ? Configuration.TARGET_ARM64 && configurationData._targetArm64Data is { } sidecarData0 ? sidecarData0._enableArm64Sve : default : default;
    public int EnableArm64Sve2 => _configurationData is { } configurationData ? Configuration.TARGET_ARM64 && configurationData._targetArm64Data is { } sidecarData0 ? sidecarData0._enableArm64Sve2 : default : default;
    public int EnableArm64Sha3 => _configurationData is { } configurationData ? Configuration.TARGET_ARM64 && configurationData._targetArm64Data is { } sidecarData0 ? sidecarData0._enableArm64Sha3 : default : default;
    public int EnableArm64Sm4 => _configurationData is { } configurationData ? Configuration.TARGET_ARM64 && configurationData._targetArm64Data is { } sidecarData0 ? sidecarData0._enableArm64Sm4 : default : default;
    public int EnableArm64SveAes => _configurationData is { } configurationData ? Configuration.TARGET_ARM64 && configurationData._targetArm64Data is { } sidecarData0 ? sidecarData0._enableArm64SveAes : default : default;
    public int EnableArm64SvePmull128 => _configurationData is { } configurationData ? Configuration.TARGET_ARM64 && configurationData._targetArm64Data is { } sidecarData0 ? sidecarData0._enableArm64SvePmull128 : default : default;
    public int EnableArm64SveSha3 => _configurationData is { } configurationData ? Configuration.TARGET_ARM64 && configurationData._targetArm64Data is { } sidecarData0 ? sidecarData0._enableArm64SveSha3 : default : default;
    public int EnableArm64SveSm4 => _configurationData is { } configurationData ? Configuration.TARGET_ARM64 && configurationData._targetArm64Data is { } sidecarData0 ? sidecarData0._enableArm64SveSm4 : default : default;
    public int EnableRiscV64Zba => _configurationData is { } configurationData ? Configuration.TARGET_RISCV64 && configurationData._targetRiscV64Data is { } sidecarData0 ? sidecarData0._enableRiscV64Zba : default : default;
    public int EnableRiscV64Zbb => _configurationData is { } configurationData ? Configuration.TARGET_RISCV64 && configurationData._targetRiscV64Data is { } sidecarData0 ? sidecarData0._enableRiscV64Zbb : default : default;
    public int EnableRiscV64Zbs => _configurationData is { } configurationData ? Configuration.TARGET_RISCV64 && configurationData._targetRiscV64Data is { } sidecarData0 ? sidecarData0._enableRiscV64Zbs : default : default;
    public int EnableRiscV64Zicond => _configurationData is { } configurationData ? Configuration.TARGET_RISCV64 && configurationData._targetRiscV64Data is { } sidecarData0 ? sidecarData0._enableRiscV64Zicond : default : default;
    public int EnableEmbeddedBroadcast => _enableEmbeddedBroadcast;
    public int EnableEmbeddedMasking => _enableEmbeddedMasking;
    public int EnableApxNDD => _enableApxNDD;
    public int EnableApxConditionalChaining => _enableApxConditionalChaining;
    public int EnableApxPPHint => _enableApxPPHint;
    public int EnableApxPP2 => _enableApxPP2;
    public int EnableApxZU => _enableApxZU;
    public int JitDisableSimdVN => _configurationData is { } configurationData ? Configuration.FEATURE_SIMD && configurationData._featureSimdData is { } sidecarData0 ? sidecarData0._jitDisableSimdVN : default : default;
    public int JitConstCSE => _jitConstCSE;
    public int JitRLCSEGreedy => _jitRLCSEGreedy;
    public int JitRLCSEVerbose => _jitRLCSEVerbose;
    public int JitCSEHash
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitCSEHash;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitCSEHash" : default);
            return default!;
        }
    }
    public int JitCSEMask
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitCSEMask;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitCSEMask" : default);
            return default!;
        }
    }
    public int JitMetrics
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitMetrics;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitMetrics" : default);
            return default!;
        }
    }
    public int JitRandomCSE
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitRandomCSE;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRandomCSE" : default);
            return default!;
        }
    }
    public unsafe byte* JitReplayCSE
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitReplayCSE;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitReplayCSE" : default);
            return default!;
        }
    }
    public unsafe byte* JitReplayCSEReward
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitReplayCSEReward;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitReplayCSEReward" : default);
            return default!;
        }
    }
    public unsafe byte* JitRLCSE
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitRLCSE;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRLCSE" : default);
            return default!;
        }
    }
    public unsafe byte* JitRLCSEAlpha
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitRLCSEAlpha;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRLCSEAlpha" : default);
            return default!;
        }
    }
    public int JitRLCSECandidateFeatures
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitRLCSECandidateFeatures;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRLCSECandidateFeatures" : default);
            return default!;
        }
    }
    public int JitRLHook
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitRLHook;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRLHook" : default);
            return default!;
        }
    }
    public int JitRLHookEmitFeatureNames
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitRLHookEmitFeatureNames;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRLHookEmitFeatureNames" : default);
            return default!;
        }
    }
    public unsafe byte* JitRLHookCSEDecisions
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitRLHookCSEDecisions;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRLHookCSEDecisions" : default);
            return default!;
        }
    }
    public int JitEnableNoWayAssert => _jitEnableNoWayAssert;
    public int DisplayMemStats => _displayMemStats;
    public int JitEnregStats
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitEnregStats;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnregStats" : default);
            return default!;
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
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDoAssertionProp;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDoAssertionProp" : default);
            return default!;
        }
    }
    public int JitDoCopyProp
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDoCopyProp;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDoCopyProp" : default);
            return default!;
        }
    }
    public int JitDoOptimizeIVs
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDoOptimizeIVs;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDoOptimizeIVs" : default);
            return default!;
        }
    }
    public int JitDoEarlyProp
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDoEarlyProp;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDoEarlyProp" : default);
            return default!;
        }
    }
    public int JitDoLoopHoisting
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDoLoopHoisting;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDoLoopHoisting" : default);
            return default!;
        }
    }
    public int JitDoLoopInversion
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDoLoopInversion;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDoLoopInversion" : default);
            return default!;
        }
    }
    public int JitLoopInversionSizeLimit => _jitLoopInversionSizeLimit;
    public int JitDoRangeAnalysis
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDoRangeAnalysis;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDoRangeAnalysis" : default);
            return default!;
        }
    }
    public int JitDoVNBasedDeadStoreRemoval
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDoVNBasedDeadStoreRemoval;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDoVNBasedDeadStoreRemoval" : default);
            return default!;
        }
    }
    public int JitDoRedundantBranchOpts
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDoRedundantBranchOpts;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDoRedundantBranchOpts" : default);
            return default!;
        }
    }
    public unsafe byte* JitEnableRboRange
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitEnableRboRange;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnableRboRange" : default);
            return default!;
        }
    }
    public unsafe byte* JitEnableHeadTailMergeRange
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitEnableHeadTailMergeRange;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnableHeadTailMergeRange" : default);
            return default!;
        }
    }
    public unsafe byte* JitEnableVNBasedDeadStoreRemovalRange
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitEnableVNBasedDeadStoreRemovalRange;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnableVNBasedDeadStoreRemovalRange" : default);
            return default!;
        }
    }
    public unsafe byte* JitEnableEarlyLivenessRange
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitEnableEarlyLivenessRange;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnableEarlyLivenessRange" : default);
            return default!;
        }
    }
    public unsafe byte* JitOnlyOptimizeRange
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitOnlyOptimizeRange;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitOnlyOptimizeRange" : default);
            return default!;
        }
    }
    public unsafe byte* JitEnablePhysicalPromotionRange
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitEnablePhysicalPromotionRange;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnablePhysicalPromotionRange" : default);
            return default!;
        }
    }
    public unsafe byte* JitEnableCrossBlockLocalAssertionPropRange
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitEnableCrossBlockLocalAssertionPropRange;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnableCrossBlockLocalAssertionPropRange" : default);
            return default!;
        }
    }
    public unsafe byte* JitEnableInductionVariableOptsRange
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitEnableInductionVariableOptsRange;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnableInductionVariableOptsRange" : default);
            return default!;
        }
    }
    public unsafe byte* JitEnableLocalAddrPropagationRange
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitEnableLocalAddrPropagationRange;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnableLocalAddrPropagationRange" : default);
            return default!;
        }
    }
    public int JitDoSsa
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDoSsa;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDoSsa" : default);
            return default!;
        }
    }
    public int JitDoValueNumber
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDoValueNumber;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDoValueNumber" : default);
            return default!;
        }
    }
    public unsafe byte* JitOptRepeatRange
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitOptRepeatRange;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitOptRepeatRange" : default);
            return default!;
        }
    }
    public int JitDoIfConversion
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDoIfConversion;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDoIfConversion" : default);
            return default!;
        }
    }
    public int JitDoOptimizeMaskConversions
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDoOptimizeMaskConversions;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDoOptimizeMaskConversions" : default);
            return default!;
        }
    }
    public int JitOptimizeAwait
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitOptimizeAwait;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitOptimizeAwait" : default);
            return default!;
        }
    }
    public unsafe byte* JitAsyncDefaultValueAnalysisRange
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitAsyncDefaultValueAnalysisRange;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitAsyncDefaultValueAnalysisRange" : default);
            return default!;
        }
    }
    public unsafe byte* JitAsyncPreservedValueAnalysisRange
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitAsyncPreservedValueAnalysisRange;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitAsyncPreservedValueAnalysisRange" : default);
            return default!;
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
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._enableExtraSuperPmiQueries;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "EnableExtraSuperPmiQueries" : default);
            return default!;
        }
    }
    public int JitInlineDumpData
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitInlineDumpData;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlineDumpData" : default);
            return default!;
        }
    }
    public int JitInlineDumpXml
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitInlineDumpXml;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlineDumpXml" : default);
            return default!;
        }
    }
    public unsafe byte* JitInlineDumpXmlFile
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitInlineDumpXmlFile;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlineDumpXmlFile" : default);
            return default!;
        }
    }
    public int JitInlinePolicyDumpXml
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitInlinePolicyDumpXml;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlinePolicyDumpXml" : default);
            return default!;
        }
    }
    public int JitInlineLimit
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitInlineLimit;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlineLimit" : default);
            return default!;
        }
    }
    public int JitInlinePolicyDiscretionary
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitInlinePolicyDiscretionary;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlinePolicyDiscretionary" : default);
            return default!;
        }
    }
    public int JitInlinePolicyFull
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitInlinePolicyFull;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlinePolicyFull" : default);
            return default!;
        }
    }
    public int JitInlinePolicySize
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitInlinePolicySize;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlinePolicySize" : default);
            return default!;
        }
    }
    public int JitInlinePolicyRandom
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitInlinePolicyRandom;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlinePolicyRandom" : default);
            return default!;
        }
    }
    public int JitInlinePolicyReplay
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitInlinePolicyReplay;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlinePolicyReplay" : default);
            return default!;
        }
    }
    public unsafe byte* JitNoInlineRange
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitNoInlineRange;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitNoInlineRange" : default);
            return default!;
        }
    }
    public unsafe byte* JitInlineReplayFile
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitInlineReplayFile;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInlineReplayFile" : default);
            return default!;
        }
    }
    public int JitStressAsyncInlining
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitStressAsyncInlining;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressAsyncInlining" : default);
            return default!;
        }
    }
    public int JitStressAsyncInliningMaxDepth
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitStressAsyncInliningMaxDepth;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressAsyncInliningMaxDepth" : default);
            return default!;
        }
    }
    public int JitStressAsyncInliningPct
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitStressAsyncInliningPct;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitStressAsyncInliningPct" : default);
            return default!;
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
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitObjectStackAllocationRange;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitObjectStackAllocationRange" : default);
            return default!;
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
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitObjectStackAllocationConditionalEscapeRange;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitObjectStackAllocationConditionalEscapeRange" : default);
            return default!;
        }
    }
    public int JitObjectStackAllocationArray => _jitObjectStackAllocationArray;
    public int JitObjectStackAllocationSize => _jitObjectStackAllocationSize;
    public int JitObjectStackAllocationTrackFields => _jitObjectStackAllocationTrackFields;
    public unsafe byte* JitObjectStackAllocationTrackFieldsRange
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitObjectStackAllocationTrackFieldsRange;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitObjectStackAllocationTrackFieldsRange" : default);
            return default!;
        }
    }
    public int JitObjectStackAllocationDumpConnGraph
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitObjectStackAllocationDumpConnGraph;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitObjectStackAllocationDumpConnGraph" : default);
            return default!;
        }
    }
    public int JitEECallTimingInfo => _jitEECallTimingInfo;
    public int JitEnableFinallyCloning
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitEnableFinallyCloning;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnableFinallyCloning" : default);
            return default!;
        }
    }
    public int JitEnableRemoveEmptyTry
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitEnableRemoveEmptyTry;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnableRemoveEmptyTry" : default);
            return default!;
        }
    }
    public int JitEnableRemoveEmptyTryCatchOrTryFault
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitEnableRemoveEmptyTryCatchOrTryFault;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnableRemoveEmptyTryCatchOrTryFault" : default);
            return default!;
        }
    }
    public int JitEnableRemoveUnreachableTry
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitEnableRemoveUnreachableTry;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnableRemoveUnreachableTry" : default);
            return default!;
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
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitGuardedDevirtualizationRange;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitGuardedDevirtualizationRange" : default);
            return default!;
        }
    }
    public int JitRandomGuardedDevirtualization
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitRandomGuardedDevirtualization;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRandomGuardedDevirtualization" : default);
            return default!;
        }
    }
    public int TC_OnStackReplacement => _tC_OnStackReplacement;
    public int TC_OnStackReplacement_InitialCounter => _tC_OnStackReplacement_InitialCounter;
    public int TC_PartialCompilation => _tC_PartialCompilation;
    public int JitRandomPartialCompilation
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitRandomPartialCompilation;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRandomPartialCompilation" : default);
            return default!;
        }
    }
    public int TC_PatchpointStrategy => _tC_PatchpointStrategy;
    public int JitRandomOnStackReplacement
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitRandomOnStackReplacement;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRandomOnStackReplacement" : default);
            return default!;
        }
    }
    public int JitOffsetOnStackReplacement
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitOffsetOnStackReplacement;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitOffsetOnStackReplacement" : default);
            return default!;
        }
    }
    public unsafe byte* JitEnableOsrRange
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitEnableOsrRange;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnableOsrRange" : default);
            return default!;
        }
    }
    public unsafe byte* JitEnablePatchpointRange
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitEnablePatchpointRange;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnablePatchpointRange" : default);
            return default!;
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
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitInstrumentIfOptimizing;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInstrumentIfOptimizing" : default);
            return default!;
        }
    }
    public unsafe byte* JitInstrumentIfOptimizingRange
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitInstrumentIfOptimizingRange;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitInstrumentIfOptimizingRange" : default);
            return default!;
        }
    }
    public int JitInstrumentInlinees => _jitInstrumentInlinees;
    public int JitDisablePGO => _jitDisablePGO;
    public unsafe byte* JitEnablePGORange
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitEnablePGORange;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitEnablePGORange" : default);
            return default!;
        }
    }
    public int JitRandomEdgeCounts
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitRandomEdgeCounts;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRandomEdgeCounts" : default);
            return default!;
        }
    }
    public int JitCrossCheckDevirtualizationAndPGO
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitCrossCheckDevirtualizationAndPGO;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitCrossCheckDevirtualizationAndPGO" : default);
            return default!;
        }
    }
    public int JitNoteFailedExactDevirtualization
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitNoteFailedExactDevirtualization;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitNoteFailedExactDevirtualization" : default);
            return default!;
        }
    }
    public int JitRandomlyCollect64BitCounts
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitRandomlyCollect64BitCounts;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRandomlyCollect64BitCounts" : default);
            return default!;
        }
    }
    public int JitSynthesizeCounts
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitSynthesizeCounts;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitSynthesizeCounts" : default);
            return default!;
        }
    }
    public int JitPropagateSynthesizedCountsToProfileData
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitPropagateSynthesizedCountsToProfileData;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitPropagateSynthesizedCountsToProfileData" : default);
            return default!;
        }
    }
    public int JitSynthesisUseSolver
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitSynthesisUseSolver;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitSynthesisUseSolver" : default);
            return default!;
        }
    }
    public unsafe byte* JitSynthesisExceptionWeight
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitSynthesisExceptionWeight;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitSynthesisExceptionWeight" : default);
            return default!;
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
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitFunctionFile;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitFunctionFile" : default);
            return default!;
        }
    }
    public MethodSet JitRawHexCode
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitRawHexCode;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRawHexCode" : default);
            return default!;
        }
    }
    public unsafe byte* JitRawHexCodeFile
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitRawHexCodeFile;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitRawHexCodeFile" : default);
            return default!;
        }
    }
    public int JitSaveFpLrWithCalleeSavedRegisters
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitSaveFpLrWithCalleeSavedRegisters;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitSaveFpLrWithCalleeSavedRegisters" : default);
            return default!;
        }
    }
    public int JitUseScalableVectorT
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitUseScalableVectorT;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitUseScalableVectorT" : default);
            return default!;
        }
    }
    public int JitDispIns
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitDispIns;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitDispIns" : default);
            return default!;
        }
    }
    public unsafe byte* JitR2RUnsupportedRange
    {
        get
        {
            if (Configuration.DEBUG)
            {
                var debugData = _configurationData?._debugData;
                return debugData is null ? default! : debugData._jitR2RUnsupportedRange;
            }
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.JitConfigValueIsOnlyAvailableInDebugBuilds, Configuration.DETAILED_FATAL_ERROR_MESSAGES ? "JitR2RUnsupportedRange" : default);
            return default!;
        }
    }
    public int JitWasmFunclets => _configurationData is { } configurationData ? Configuration.TARGET_WASM && configurationData._targetWasmData is { } sidecarData0 ? sidecarData0._jitWasmFunclets : default : default;
    public int JitEnregStructLocals => _jitEnregStructLocals;

    internal Compiler.DebugData.JitConfigValuesData getDebugDataForWrite()
    {
        var configurationData = _configurationData?.Clone() ?? new ConfigurationData();
        var debugData = configurationData._debugData;
        if (debugData is null)
        {
            debugData = new Compiler.DebugData.JitConfigValuesData();
        }
        else
        {
            debugData = debugData.Clone();
        }
        configurationData._debugData = debugData;
        _configurationData = configurationData;
        return debugData;
    }

    public unsafe void destroy(ICorJitHost* jitHost)
    {
        if (!_isInitialized)
        {
            return;
        }

        // _configurationData!._debugData!._altJitLimit = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._altJitSkipOnAssert = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._breakOnDumpToken = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._debugBreakOnVerificationFailure = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._displayLoopHoistStats = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._displayLsraStats = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitLsraOrdering);
        }
        // _configurationData!._debugData!._enablePCRelAddr = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitAssertOnMaxRAPasses = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitBreakEmitOutputInstr = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitBreakMorphTree = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitBreakOnBadCode = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitBreakOnMinOpts = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitCloneLoops = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitCloneLoopsWithEH = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitCloneLoopsWithGdvTests = unchecked((int)(0xCDCDCDCD));
        // _jitCloneLoopsSizeLimit = unchecked((int)(0xCDCDCDCD));
        // _jitCloneLoopsMinPerCallRatio = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDebugLogLoopCloning = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitAlignLoopMinBlockWeight = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitAlignLoopMaxCodeSize = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitAlignLoopBoundary = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitAlignLoopForJcc = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitAlignLoopAdaptive = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitHideAlignBehindJmp = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitOptimizeStructHiddenBuffer = unchecked((int)(0xCDCDCDCD));
        // _jitEnableStoreLclFldCoalescing = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitUnrollLoopMaxIterationCount = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitUnrollLoopsWithEH = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDirectAlloc = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDoubleAlign = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitEmitPrintRefRegs = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitEnableDevirtualization = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitEnableLateDevirtualization = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitExpensiveDebugCheckLevel = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitForceFallback = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitFullyInt = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitFunctionTrace = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitGCChecks = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitGCInfoLogging = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitHashBreak = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitHashHalt = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitInlineAdditionalMultiplier = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitInlinePrintStats = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitInlineSize = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitInlineDepth = unchecked((int)(0xCDCDCDCD));
        // _jitInlineBudget = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitForceInlineDepth = unchecked((int)(0xCDCDCDCD));
        // _jitInlineMethodsWithEH = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitInlineMethodsWithEHRange);
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitOptimizeAwaitRange);
        }
        // _configurationData!._debugData!._jitAwaitHashBreak = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitLongAddress = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitMaxUncheckedOffset = unchecked((int)(0xCDCDCDCD));
        if (Configuration.TARGET_ARM64)
        {
            // _configurationData!._targetArm64Data!._jitPacEnabled = unchecked((int)(0xCDCDCDCD));
        }
        // _jitEnableGenericVirtualDevirtualization = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitMinOpts = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            _configurationData!._debugData!._jitMinOptsName.destroy(jitHost);
        }
        // _configurationData!._debugData!._jitMinOptsBbCount = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitMinOptsCodeSize = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitMinOptsInstrCount = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitMinOptsLvNumCount = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitMinOptsLvRefCount = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitNoCSE = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitNoCSE2 = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitNoForceFallback = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitNoForwardSub = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitNoHoist = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitNoMemoryBarriers = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitNoStructPromotion = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitNoUnroll = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitOrder = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitQueryCurrentStaticFieldClass = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitReportFastTailCallDecisions = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitPInvokeCheckEnabled = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitPInvokeEnabled = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitHoistLimit = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitPrintInlinedMethodsVerbose = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            _configurationData!._debugData!._jitPrintInlinedMethods.destroy(jitHost);
            _configurationData!._debugData!._jitPrintDevirtualizedMethods.destroy(jitHost);
        }
        // _configurationData!._debugData!._jitProfileChecks = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitRequired = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitStackAllocToLocalSize = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitSkipArrayBoundCheck = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitEnablePhaseChecks = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitSplitFunctionSize = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitSsaStress = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitStackChecks = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitStress = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitStressBBProf = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitStressProcedureSplitting = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitStressRegs = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitStressRegsRange);
        }
        // _configurationData!._debugData!._jitStressSplitTreeLimit = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitVNMapSelLimit = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._runAltJitCode = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._runComponentUnitTests = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._shouldInjectFault = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._tailcallStress = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            _configurationData!._debugData!._jitBreak.destroy(jitHost);
            _configurationData!._debugData!._jitDebugBreak.destroy(jitHost);
            _configurationData!._debugData!._jitDump.destroy(jitHost);
        }
        // _configurationData!._debugData!._jitHashDump = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDumpTier0 = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDumpOSR = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDumpAtOSROffset = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDumpInlinePhases = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDumpASCII = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDumpTerseLsra = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDumpToDebugger = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDumpVerboseSsa = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDumpVerboseTrees = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDumpTreeIDs = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDumpBeforeAfterMorph = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDumpTerseNextBlock = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            _configurationData!._debugData!._jitEHDump.destroy(jitHost);
            _configurationData!._debugData!._jitExclude.destroy(jitHost);
        }
        // _configurationData!._debugData!._jitFakeProcedureSplitting = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            _configurationData!._debugData!._jitForceProcedureSplitting.destroy(jitHost);
            _configurationData!._debugData!._jitGCDump.destroy(jitHost);
            _configurationData!._debugData!._jitDebugDump.destroy(jitHost);
            _configurationData!._debugData!._jitHalt.destroy(jitHost);
            _configurationData!._debugData!._jitInclude.destroy(jitHost);
            _configurationData!._debugData!._jitLateDisasm.destroy(jitHost);
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitLateDisasmTo);
            _configurationData!._debugData!._jitNoProcedureSplitting.destroy(jitHost);
            _configurationData!._debugData!._jitNoProcedureSplittingEH.destroy(jitHost);
            _configurationData!._debugData!._jitStressOnly.destroy(jitHost);
            _configurationData!._debugData!._jitUnwindDump.destroy(jitHost);
            _configurationData!._debugData!._jitDumpFg.destroy(jitHost);
        }
        // _configurationData!._debugData!._jitDumpFgHash = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDumpFgTier0 = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitDumpFgDir);
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitDumpFgFile);
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitDumpFgPhase);
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitDumpFgPrePhase);
        }
        // _configurationData!._debugData!._jitDumpFgDot = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDumpFgEH = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDumpFgLoops = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDumpFgConstrained = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDumpFgBlockID = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDumpFgBlockFlags = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDumpFgLoopFlags = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDumpFgBlockOrder = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDumpFgMemorySsa = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitRange);
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitStressModeNames);
        }
        // _configurationData!._debugData!._jitStressModeNamesOnly = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitStressModeNamesAllow);
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitStressModeNamesNot);
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitStressRange);
            _configurationData!._debugData!._jitEmitUnitTests.destroy(jitHost);
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitEmitUnitTestsSections);
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
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitDisasmAssemblies);
        }
        // _configurationData!._debugData!._jitDisasmWithGC = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDisasmWithDebugInfo = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDisasmSpilled = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDisasmWithAddress = unchecked((int)(0xCDCDCDCD));
        jitHost->freeStringConfigValue(_jitStdOutFile);
        // _richDebugInfo = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_configurationData!._debugData!._writeRichDebugInfoFile);
        }
        // _jitAlignLoops = unchecked((int)(0xCDCDCDCD));
        // _altJitAssertOnNYI = unchecked((int)(0xCDCDCDCD));
        // _enableEHWriteThru = unchecked((int)(0xCDCDCDCD));
        // _enableMultiRegLocals = unchecked((int)(0xCDCDCDCD));
        // _jitNoInline = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            // _configurationData!._debugData!._jitStressRex2Encoding = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._debugData!._jitStressPromotedEvexEncoding = unchecked((int)(0xCDCDCDCD));
        }
        if (Configuration.TARGET_AMD64 || Configuration.TARGET_X86)
        {
            // _configurationData!._debugData!._jitStressEvexEncoding = unchecked((int)(0xCDCDCDCD));
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
            // _configurationData!._targetXarchData!._enableAVX = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetXarchData!._enableAVX2 = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetXarchData!._enableAVX512 = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetXarchData!._enableAVX512BMM = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetXarchData!._enableAVX512v2 = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetXarchData!._enableAVX512v3 = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetXarchData!._enableAVX10v1 = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetXarchData!._enableAVX10v2 = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetXarchData!._enableAPX = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetXarchData!._enableAES = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetXarchData!._enableAVX512VP2INTERSECT = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetXarchData!._enableAVXIFMA = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetXarchData!._enableAVXVNNI = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetXarchData!._enableAVXVNNIINT = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetXarchData!._enableGFNI = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetXarchData!._enableSHA = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetXarchData!._enableVAES = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetXarchData!._enableWAITPKG = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetXarchData!._enableX86Serialize = unchecked((int)(0xCDCDCDCD));
        }
        else if (Configuration.TARGET_ARM64)
        {
            // _configurationData!._targetArm64Data!._enableArm64Aes = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetArm64Data!._enableArm64Atomics = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetArm64Data!._enableArm64Crc32 = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetArm64Data!._enableArm64Cssc = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetArm64Data!._enableArm64Dczva = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetArm64Data!._enableArm64Dp = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetArm64Data!._enableArm64Fp16 = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetArm64Data!._enableArm64Rdm = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetArm64Data!._enableArm64Sha1 = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetArm64Data!._enableArm64Sha256 = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetArm64Data!._enableArm64Sve = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetArm64Data!._enableArm64Sve2 = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetArm64Data!._enableArm64Sha3 = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetArm64Data!._enableArm64Sm4 = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetArm64Data!._enableArm64SveAes = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetArm64Data!._enableArm64SvePmull128 = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetArm64Data!._enableArm64SveSha3 = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetArm64Data!._enableArm64SveSm4 = unchecked((int)(0xCDCDCDCD));
        }
        else if (Configuration.TARGET_RISCV64)
        {
            // _configurationData!._targetRiscV64Data!._enableRiscV64Zba = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetRiscV64Data!._enableRiscV64Zbb = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetRiscV64Data!._enableRiscV64Zbs = unchecked((int)(0xCDCDCDCD));
            // _configurationData!._targetRiscV64Data!._enableRiscV64Zicond = unchecked((int)(0xCDCDCDCD));
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
            // _configurationData!._featureSimdData!._jitDisableSimdVN = unchecked((int)(0xCDCDCDCD));
        }
        // _jitConstCSE = unchecked((int)(0xCDCDCDCD));
        // _jitRLCSEGreedy = unchecked((int)(0xCDCDCDCD));
        // _jitRLCSEVerbose = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitCSEHash = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitCSEMask = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitMetrics = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitRandomCSE = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitReplayCSE);
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitReplayCSEReward);
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitRLCSE);
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitRLCSEAlpha);
        }
        // _configurationData!._debugData!._jitRLCSECandidateFeatures = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitRLHook = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitRLHookEmitFeatureNames = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitRLHookCSEDecisions);
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
        // _configurationData!._debugData!._jitEnregStats = unchecked((int)(0xCDCDCDCD));
        // _jitAggressiveInlining = unchecked((int)(0xCDCDCDCD));
        // _jitELTHookEnabled = unchecked((int)(0xCDCDCDCD));
        // _jitInlineSIMDMultiplier = unchecked((int)(0xCDCDCDCD));
        // _jitMaxLocalsToTrack = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDoAssertionProp = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDoCopyProp = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDoOptimizeIVs = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDoEarlyProp = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDoLoopHoisting = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDoLoopInversion = unchecked((int)(0xCDCDCDCD));
        // _jitLoopInversionSizeLimit = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDoRangeAnalysis = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDoVNBasedDeadStoreRemoval = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDoRedundantBranchOpts = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitEnableRboRange);
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitEnableHeadTailMergeRange);
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitEnableVNBasedDeadStoreRemovalRange);
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitEnableEarlyLivenessRange);
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitOnlyOptimizeRange);
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitEnablePhysicalPromotionRange);
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitEnableCrossBlockLocalAssertionPropRange);
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitEnableInductionVariableOptsRange);
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitEnableLocalAddrPropagationRange);
        }
        // _configurationData!._debugData!._jitDoSsa = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDoValueNumber = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitOptRepeatRange);
        }
        // _configurationData!._debugData!._jitDoIfConversion = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitDoOptimizeMaskConversions = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitOptimizeAwait = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitAsyncDefaultValueAnalysisRange);
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitAsyncPreservedValueAnalysisRange);
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
        // _configurationData!._debugData!._enableExtraSuperPmiQueries = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitInlineDumpData = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitInlineDumpXml = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitInlineDumpXmlFile);
        }
        // _configurationData!._debugData!._jitInlinePolicyDumpXml = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitInlineLimit = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitInlinePolicyDiscretionary = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitInlinePolicyFull = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitInlinePolicySize = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitInlinePolicyRandom = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitInlinePolicyReplay = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitNoInlineRange);
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitInlineReplayFile);
        }
        // _configurationData!._debugData!._jitStressAsyncInlining = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitStressAsyncInliningMaxDepth = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitStressAsyncInliningPct = unchecked((int)(0xCDCDCDCD));
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
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitObjectStackAllocationRange);
        }
        // _jitObjectStackAllocation = unchecked((int)(0xCDCDCDCD));
        // _jitObjectStackAllocationRefClass = unchecked((int)(0xCDCDCDCD));
        // _jitObjectStackAllocationBoxedValueClass = unchecked((int)(0xCDCDCDCD));
        // _jitObjectStackAllocationConditionalEscape = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitObjectStackAllocationConditionalEscapeRange);
        }
        // _jitObjectStackAllocationArray = unchecked((int)(0xCDCDCDCD));
        // _jitObjectStackAllocationSize = unchecked((int)(0xCDCDCDCD));
        // _jitObjectStackAllocationTrackFields = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitObjectStackAllocationTrackFieldsRange);
        }
        // _configurationData!._debugData!._jitObjectStackAllocationDumpConnGraph = unchecked((int)(0xCDCDCDCD));
        // _jitEECallTimingInfo = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitEnableFinallyCloning = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitEnableRemoveEmptyTry = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitEnableRemoveEmptyTryCatchOrTryFault = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitEnableRemoveUnreachableTry = unchecked((int)(0xCDCDCDCD));
        // _jitEnableGuardedDevirtualization = unchecked((int)(0xCDCDCDCD));
        // _jitGuardedDevirtualizationMaxTypeChecks = unchecked((int)(0xCDCDCDCD));
        // _jitGuardedDevirtualizationRequireInlining = unchecked((int)(0xCDCDCDCD));
        // _jitGuardedDevirtualizationChainLikelihood = unchecked((int)(0xCDCDCDCD));
        // _jitGuardedDevirtualizationChainStatements = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitGuardedDevirtualizationRange);
        }
        // _configurationData!._debugData!._jitRandomGuardedDevirtualization = unchecked((int)(0xCDCDCDCD));
        // _tC_OnStackReplacement = unchecked((int)(0xCDCDCDCD));
        // _tC_OnStackReplacement_InitialCounter = unchecked((int)(0xCDCDCDCD));
        // _tC_PartialCompilation = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitRandomPartialCompilation = unchecked((int)(0xCDCDCDCD));
        // _tC_PatchpointStrategy = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitRandomOnStackReplacement = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitOffsetOnStackReplacement = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitEnableOsrRange);
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitEnablePatchpointRange);
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
        // _configurationData!._debugData!._jitInstrumentIfOptimizing = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitInstrumentIfOptimizingRange);
        }
        // _jitInstrumentInlinees = unchecked((int)(0xCDCDCDCD));
        // _jitDisablePGO = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitEnablePGORange);
        }
        // _configurationData!._debugData!._jitRandomEdgeCounts = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitCrossCheckDevirtualizationAndPGO = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitNoteFailedExactDevirtualization = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitRandomlyCollect64BitCounts = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitSynthesizeCounts = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitPropagateSynthesizedCountsToProfileData = unchecked((int)(0xCDCDCDCD));
        // _configurationData!._debugData!._jitSynthesisUseSolver = unchecked((int)(0xCDCDCDCD));
        if (Configuration.DEBUG)
        {
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitSynthesisExceptionWeight);
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
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitFunctionFile);
            _configurationData!._debugData!._jitRawHexCode.destroy(jitHost);
            jitHost->freeStringConfigValue(_configurationData!._debugData!._jitRawHexCodeFile);
        }
        if (Configuration.TARGET_ARM64)
        {
            // _configurationData!._debugData!._jitSaveFpLrWithCalleeSavedRegisters = unchecked((int)(0xCDCDCDCD));
        }
        // _configurationData!._debugData!._jitUseScalableVectorT = unchecked((int)(0xCDCDCDCD));
        if (Configuration.TARGET_LOONGARCH64)
        {
            // _configurationData!._debugData!._jitDispIns = unchecked((int)(0xCDCDCDCD));
        }
        if (Configuration.TARGET_WASM)
        {
            if (Configuration.DEBUG)
            {
                jitHost->freeStringConfigValue(_configurationData!._debugData!._jitR2RUnsupportedRange);
            }
            // _configurationData!._targetWasmData!._jitWasmFunclets = unchecked((int)(0xCDCDCDCD));
        }
        // _jitEnregStructLocals = unchecked((int)(0xCDCDCDCD));
        _isInitialized = false;
    }

    public unsafe void initialize(ICorJitHost* jitHost)
    {
        assert(!_isInitialized);

        if (Configuration.DEBUG || Configuration.FEATURE_SIMD || Configuration.TARGET_ARM64 || Configuration.TARGET_RISCV64 || Configuration.TARGET_WASM || Configuration.TARGET_XARCH)
        {
            _configurationData = new ConfigurationData();
            if (Configuration.DEBUG)
            {
                _configurationData._debugData = new Compiler.DebugData.JitConfigValuesData();
            }
            if (Configuration.FEATURE_SIMD)
            {
                _configurationData._featureSimdData = new FeatureSimdData();
            }
            if (Configuration.TARGET_ARM64)
            {
                _configurationData._targetArm64Data = new TargetArm64Data();
            }
            if (Configuration.TARGET_RISCV64)
            {
                _configurationData._targetRiscV64Data = new TargetRiscV64Data();
            }
            if (Configuration.TARGET_WASM)
            {
                _configurationData._targetWasmData = new TargetWasmData();
            }
            if (Configuration.TARGET_XARCH)
            {
                _configurationData._targetXarchData = new TargetXarchData();
            }
        }

        if (Configuration.DEBUG)
        {
            _configurationData!._debugData!._altJitLimit = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("AltJitLimit"u8))), 0);
            _configurationData!._debugData!._altJitSkipOnAssert = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("AltJitSkipOnAssert"u8))), 0);
            _configurationData!._debugData!._breakOnDumpToken = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("BreakOnDumpToken"u8))), unchecked((int)(0xffffffff)));
            _configurationData!._debugData!._debugBreakOnVerificationFailure = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("DebugBreakOnVerificationFailure"u8))), 0);
            _configurationData!._debugData!._displayLoopHoistStats = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitLoopHoistStats"u8))), 0);
            _configurationData!._debugData!._displayLsraStats = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitLsraStats"u8))), 0);
            _configurationData!._debugData!._jitLsraOrdering = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitLsraOrdering"u8))));
            _configurationData!._debugData!._enablePCRelAddr = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnablePCRelAddr"u8))), 1);
            _configurationData!._debugData!._jitAssertOnMaxRAPasses = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAssertOnMaxRAPasses"u8))), 0);
            _configurationData!._debugData!._jitBreakEmitOutputInstr = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitBreakEmitOutputInstr"u8))), -1);
            _configurationData!._debugData!._jitBreakMorphTree = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitBreakMorphTree"u8))), unchecked((int)(0xffffffff)));
            _configurationData!._debugData!._jitBreakOnBadCode = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitBreakOnBadCode"u8))), 0);
            _configurationData!._debugData!._jitBreakOnMinOpts = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JITBreakOnMinOpts"u8))), 0);
            _configurationData!._debugData!._jitCloneLoops = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCloneLoops"u8))), 1);
            _configurationData!._debugData!._jitCloneLoopsWithEH = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCloneLoopsWithEH"u8))), 1);
            _configurationData!._debugData!._jitCloneLoopsWithGdvTests = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCloneLoopsWithGdvTests"u8))), 1);
        }
        _jitCloneLoopsSizeLimit = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCloneLoopsSizeLimit"u8))), 400);
        _jitCloneLoopsMinPerCallRatio = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCloneLoopsMinPerCallRatio"u8))), 4);
        if (Configuration.DEBUG)
        {
            _configurationData!._debugData!._jitDebugLogLoopCloning = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDebugLogLoopCloning"u8))), 0);
            _configurationData!._debugData!._jitAlignLoopMinBlockWeight = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAlignLoopMinBlockWeight"u8))), DEFAULT_ALIGN_LOOP_MIN_BLOCK_WEIGHT);
            _configurationData!._debugData!._jitAlignLoopMaxCodeSize = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAlignLoopMaxCodeSize"u8))), DEFAULT_MAX_LOOPSIZE_FOR_ALIGN);
            _configurationData!._debugData!._jitAlignLoopBoundary = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAlignLoopBoundary"u8))), DEFAULT_ALIGN_LOOP_BOUNDARY);
            _configurationData!._debugData!._jitAlignLoopForJcc = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAlignLoopForJcc"u8))), 0);
            _configurationData!._debugData!._jitAlignLoopAdaptive = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAlignLoopAdaptive"u8))), 1);
            _configurationData!._debugData!._jitHideAlignBehindJmp = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitHideAlignBehindJmp"u8))), 1);
            _configurationData!._debugData!._jitOptimizeStructHiddenBuffer = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitOptimizeStructHiddenBuffer"u8))), 1);
        }
        _jitEnableStoreLclFldCoalescing = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableStoreLclFldCoalescing"u8))), 1);
        if (Configuration.DEBUG)
        {
            _configurationData!._debugData!._jitUnrollLoopMaxIterationCount = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitUnrollLoopMaxIterationCount"u8))), DEFAULT_UNROLL_LOOP_MAX_ITERATION_COUNT);
            _configurationData!._debugData!._jitUnrollLoopsWithEH = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitUnrollLoopsWithEH"u8))), 0);
            _configurationData!._debugData!._jitDirectAlloc = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDirectAlloc"u8))), 0);
            _configurationData!._debugData!._jitDoubleAlign = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoubleAlign"u8))), 1);
            _configurationData!._debugData!._jitEmitPrintRefRegs = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEmitPrintRefRegs"u8))), 0);
            _configurationData!._debugData!._jitEnableDevirtualization = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableDevirtualization"u8))), 1);
            _configurationData!._debugData!._jitEnableLateDevirtualization = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableLateDevirtualization"u8))), 1);
            _configurationData!._debugData!._jitExpensiveDebugCheckLevel = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitExpensiveDebugCheckLevel"u8))), 0);
            _configurationData!._debugData!._jitForceFallback = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitForceFallback"u8))), 0);
            _configurationData!._debugData!._jitFullyInt = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitFullyInt"u8))), 0);
            _configurationData!._debugData!._jitFunctionTrace = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitFunctionTrace"u8))), 0);
            _configurationData!._debugData!._jitGCChecks = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitGCChecks"u8))), 0);
            _configurationData!._debugData!._jitGCInfoLogging = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitGCInfoLogging"u8))), 0);
            _configurationData!._debugData!._jitHashBreak = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitHashBreak"u8))), -1);
            _configurationData!._debugData!._jitHashHalt = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitHashHalt"u8))), -1);
            _configurationData!._debugData!._jitInlineAdditionalMultiplier = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineAdditionalMultiplier"u8))), 0);
            _configurationData!._debugData!._jitInlinePrintStats = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlinePrintStats"u8))), 0);
            _configurationData!._debugData!._jitInlineSize = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineSize"u8))), DEFAULT_MAX_INLINE_SIZE);
            _configurationData!._debugData!._jitInlineDepth = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineDepth"u8))), DEFAULT_MAX_INLINE_DEPTH);
        }
        _jitInlineBudget = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineBudget"u8))), DEFAULT_INLINE_BUDGET);
        if (Configuration.DEBUG)
        {
            _configurationData!._debugData!._jitForceInlineDepth = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitForceInlineDepth"u8))), DEFAULT_MAX_FORCE_INLINE_DEPTH);
        }
        _jitInlineMethodsWithEH = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineMethodsWithEH"u8))), 1);
        if (Configuration.DEBUG)
        {
            _configurationData!._debugData!._jitInlineMethodsWithEHRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineMethodsWithEHRange"u8))));
            _configurationData!._debugData!._jitOptimizeAwaitRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitOptimizeAwaitRange"u8))));
            _configurationData!._debugData!._jitAwaitHashBreak = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAwaitHashBreak"u8))), -1);
            _configurationData!._debugData!._jitLongAddress = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitLongAddress"u8))), 0);
            _configurationData!._debugData!._jitMaxUncheckedOffset = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitMaxUncheckedOffset"u8))), 8);
        }
        if (Configuration.TARGET_ARM64)
        {
            _configurationData!._targetArm64Data!._jitPacEnabled = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitPacEnabled"u8))), 0);
        }
        _jitEnableGenericVirtualDevirtualization = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableGenericVirtualDevirtualization"u8))), 1);
        if (Configuration.DEBUG)
        {
            _configurationData!._debugData!._jitMinOpts = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JITMinOpts"u8))), 0);
            var jitMinOptsNameValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JITMinOptsName"u8))));
            _configurationData!._debugData!._jitMinOptsName = new MethodSet(jitMinOptsNameValue, jitHost);
            _configurationData!._debugData!._jitMinOptsBbCount = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JITMinOptsBbCount"u8))), DEFAULT_MIN_OPTS_BB_COUNT);
            _configurationData!._debugData!._jitMinOptsCodeSize = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JITMinOptsCodeSize"u8))), DEFAULT_MIN_OPTS_CODE_SIZE);
            _configurationData!._debugData!._jitMinOptsInstrCount = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JITMinOptsInstrCount"u8))), DEFAULT_MIN_OPTS_INSTR_COUNT);
            _configurationData!._debugData!._jitMinOptsLvNumCount = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JITMinOptsLvNumcount"u8))), DEFAULT_MIN_OPTS_LV_NUM_COUNT);
            _configurationData!._debugData!._jitMinOptsLvRefCount = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JITMinOptsLvRefcount"u8))), DEFAULT_MIN_OPTS_LV_REF_COUNT);
            _configurationData!._debugData!._jitNoCSE = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoCSE"u8))), 0);
            _configurationData!._debugData!._jitNoCSE2 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoCSE2"u8))), 0);
            _configurationData!._debugData!._jitNoForceFallback = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoForceFallback"u8))), 0);
            _configurationData!._debugData!._jitNoForwardSub = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoForwardSub"u8))), 0);
            _configurationData!._debugData!._jitNoHoist = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoHoist"u8))), 0);
            _configurationData!._debugData!._jitNoMemoryBarriers = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoMemoryBarriers"u8))), 0);
            _configurationData!._debugData!._jitNoStructPromotion = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoStructPromotion"u8))), 0);
            _configurationData!._debugData!._jitNoUnroll = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoUnroll"u8))), 0);
            _configurationData!._debugData!._jitOrder = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitOrder"u8))), 0);
            _configurationData!._debugData!._jitQueryCurrentStaticFieldClass = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitQueryCurrentStaticFieldClass"u8))), 1);
            _configurationData!._debugData!._jitReportFastTailCallDecisions = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitReportFastTailCallDecisions"u8))), 0);
            _configurationData!._debugData!._jitPInvokeCheckEnabled = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JITPInvokeCheckEnabled"u8))), 0);
            _configurationData!._debugData!._jitPInvokeEnabled = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JITPInvokeEnabled"u8))), 1);
            _configurationData!._debugData!._jitHoistLimit = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitHoistLimit"u8))), -1);
            _configurationData!._debugData!._jitPrintInlinedMethodsVerbose = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitPrintInlinedMethodsVerboseLevel"u8))), 0);
            var jitPrintInlinedMethodsValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitPrintInlinedMethods"u8))));
            _configurationData!._debugData!._jitPrintInlinedMethods = new MethodSet(jitPrintInlinedMethodsValue, jitHost);
            var jitPrintDevirtualizedMethodsValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitPrintDevirtualizedMethods"u8))));
            _configurationData!._debugData!._jitPrintDevirtualizedMethods = new MethodSet(jitPrintDevirtualizedMethodsValue, jitHost);
            _configurationData!._debugData!._jitProfileChecks = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitProfileChecks"u8))), -1);
            _configurationData!._debugData!._jitRequired = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JITRequired"u8))), -1);
            _configurationData!._debugData!._jitStackAllocToLocalSize = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStackAllocToLocalSize"u8))), DEFAULT_MAX_LOCALLOC_TO_LOCAL_SIZE);
            _configurationData!._debugData!._jitSkipArrayBoundCheck = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitSkipArrayBoundCheck"u8))), 0);
            _configurationData!._debugData!._jitEnablePhaseChecks = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnablePhaseChecks"u8))), 1);
            _configurationData!._debugData!._jitSplitFunctionSize = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitSplitFunctionSize"u8))), 0);
            _configurationData!._debugData!._jitSsaStress = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitSsaStress"u8))), 0);
            _configurationData!._debugData!._jitStackChecks = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStackChecks"u8))), 0);
            _configurationData!._debugData!._jitStress = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStress"u8))), 0);
            _configurationData!._debugData!._jitStressBBProf = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressBBProf"u8))), 0);
            _configurationData!._debugData!._jitStressProcedureSplitting = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressProcedureSplitting"u8))), 0);
            _configurationData!._debugData!._jitStressRegs = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressRegs"u8))), 0);
            _configurationData!._debugData!._jitStressRegsRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressRegsRange"u8))));
            _configurationData!._debugData!._jitStressSplitTreeLimit = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressSplitTreeLimit"u8))), -1);
            _configurationData!._debugData!._jitVNMapSelLimit = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitVNMapSelLimit"u8))), 0);
            _configurationData!._debugData!._runAltJitCode = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("RunAltJitCode"u8))), 1);
            _configurationData!._debugData!._runComponentUnitTests = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitComponentUnitTests"u8))), 0);
            _configurationData!._debugData!._shouldInjectFault = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("InjectFault"u8))), 0);
            _configurationData!._debugData!._tailcallStress = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("TailcallStress"u8))), 0);
            var jitBreakValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitBreak"u8))));
            _configurationData!._debugData!._jitBreak = new MethodSet(jitBreakValue, jitHost);
            var jitDebugBreakValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDebugBreak"u8))));
            _configurationData!._debugData!._jitDebugBreak = new MethodSet(jitDebugBreakValue, jitHost);
            var jitDumpValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDump"u8))));
            _configurationData!._debugData!._jitDump = new MethodSet(jitDumpValue, jitHost);
            _configurationData!._debugData!._jitHashDump = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitHashDump"u8))), -1);
            _configurationData!._debugData!._jitDumpTier0 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpTier0"u8))), 1);
            _configurationData!._debugData!._jitDumpOSR = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpOSR"u8))), 1);
            _configurationData!._debugData!._jitDumpAtOSROffset = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpAtOSROffset"u8))), -1);
            _configurationData!._debugData!._jitDumpInlinePhases = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpInlinePhases"u8))), 1);
            _configurationData!._debugData!._jitDumpASCII = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpASCII"u8))), 1);
            _configurationData!._debugData!._jitDumpTerseLsra = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpTerseLsra"u8))), 1);
            _configurationData!._debugData!._jitDumpToDebugger = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpToDebugger"u8))), 0);
            _configurationData!._debugData!._jitDumpVerboseSsa = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpVerboseSsa"u8))), 0);
            _configurationData!._debugData!._jitDumpVerboseTrees = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpVerboseTrees"u8))), 0);
            _configurationData!._debugData!._jitDumpTreeIDs = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpTreeIDs"u8))), 1);
            _configurationData!._debugData!._jitDumpBeforeAfterMorph = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpBeforeAfterMorph"u8))), 0);
            _configurationData!._debugData!._jitDumpTerseNextBlock = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpTerseNextBlock"u8))), 0);
            var jitEHDumpValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEHDump"u8))));
            _configurationData!._debugData!._jitEHDump = new MethodSet(jitEHDumpValue, jitHost);
            var jitExcludeValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitExclude"u8))));
            _configurationData!._debugData!._jitExclude = new MethodSet(jitExcludeValue, jitHost);
            _configurationData!._debugData!._jitFakeProcedureSplitting = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitFakeProcedureSplitting"u8))), 0);
            var jitForceProcedureSplittingValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitForceProcedureSplitting"u8))));
            _configurationData!._debugData!._jitForceProcedureSplitting = new MethodSet(jitForceProcedureSplittingValue, jitHost);
            var jitGCDumpValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitGCDump"u8))));
            _configurationData!._debugData!._jitGCDump = new MethodSet(jitGCDumpValue, jitHost);
            var jitDebugDumpValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDebugDump"u8))));
            _configurationData!._debugData!._jitDebugDump = new MethodSet(jitDebugDumpValue, jitHost);
            var jitHaltValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitHalt"u8))));
            _configurationData!._debugData!._jitHalt = new MethodSet(jitHaltValue, jitHost);
            var jitIncludeValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInclude"u8))));
            _configurationData!._debugData!._jitInclude = new MethodSet(jitIncludeValue, jitHost);
            var jitLateDisasmValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitLateDisasm"u8))));
            _configurationData!._debugData!._jitLateDisasm = new MethodSet(jitLateDisasmValue, jitHost);
            _configurationData!._debugData!._jitLateDisasmTo = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitLateDisasmTo"u8))));
            var jitNoProcedureSplittingValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoProcedureSplitting"u8))));
            _configurationData!._debugData!._jitNoProcedureSplitting = new MethodSet(jitNoProcedureSplittingValue, jitHost);
            var jitNoProcedureSplittingEHValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoProcedureSplittingEH"u8))));
            _configurationData!._debugData!._jitNoProcedureSplittingEH = new MethodSet(jitNoProcedureSplittingEHValue, jitHost);
            var jitStressOnlyValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressOnly"u8))));
            _configurationData!._debugData!._jitStressOnly = new MethodSet(jitStressOnlyValue, jitHost);
            var jitUnwindDumpValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitUnwindDump"u8))));
            _configurationData!._debugData!._jitUnwindDump = new MethodSet(jitUnwindDumpValue, jitHost);
            var jitDumpFgValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFg"u8))));
            _configurationData!._debugData!._jitDumpFg = new MethodSet(jitDumpFgValue, jitHost);
            _configurationData!._debugData!._jitDumpFgHash = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgHash"u8))), 0);
            _configurationData!._debugData!._jitDumpFgTier0 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgTier0"u8))), 1);
            _configurationData!._debugData!._jitDumpFgDir = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgDir"u8))));
            _configurationData!._debugData!._jitDumpFgFile = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgFile"u8))));
            _configurationData!._debugData!._jitDumpFgPhase = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgPhase"u8))));
            _configurationData!._debugData!._jitDumpFgPrePhase = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgPrePhase"u8))));
            _configurationData!._debugData!._jitDumpFgDot = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgDot"u8))), 1);
            _configurationData!._debugData!._jitDumpFgEH = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgEH"u8))), 0);
            _configurationData!._debugData!._jitDumpFgLoops = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgLoops"u8))), 0);
            _configurationData!._debugData!._jitDumpFgConstrained = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgConstrained"u8))), 1);
            _configurationData!._debugData!._jitDumpFgBlockID = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgBlockID"u8))), 0);
            _configurationData!._debugData!._jitDumpFgBlockFlags = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgBlockFlags"u8))), 0);
            _configurationData!._debugData!._jitDumpFgLoopFlags = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgLoopFlags"u8))), 0);
            _configurationData!._debugData!._jitDumpFgBlockOrder = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgBlockOrder"u8))), 0);
            _configurationData!._debugData!._jitDumpFgMemorySsa = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDumpFgMemorySsa"u8))), 0);
            _configurationData!._debugData!._jitRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRange"u8))));
            _configurationData!._debugData!._jitStressModeNames = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressModeNames"u8))));
            _configurationData!._debugData!._jitStressModeNamesOnly = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressModeNamesOnly"u8))), 0);
            _configurationData!._debugData!._jitStressModeNamesAllow = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressModeNamesAllow"u8))));
            _configurationData!._debugData!._jitStressModeNamesNot = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressModeNamesNot"u8))));
            _configurationData!._debugData!._jitStressRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressRange"u8))));
            var jitEmitUnitTestsValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEmitUnitTests"u8))));
            _configurationData!._debugData!._jitEmitUnitTests = new MethodSet(jitEmitUnitTestsValue, jitHost);
            _configurationData!._debugData!._jitEmitUnitTestsSections = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEmitUnitTestsSections"u8))));
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
            _configurationData!._debugData!._jitDisasmAssemblies = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmAssemblies"u8))));
            _configurationData!._debugData!._jitDisasmWithGC = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmWithGC"u8))), 0);
            _configurationData!._debugData!._jitDisasmWithDebugInfo = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmWithDebugInfo"u8))), 0);
            _configurationData!._debugData!._jitDisasmSpilled = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmSpilled"u8))), 0);
            _configurationData!._debugData!._jitDisasmWithAddress = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisasmWithAddress"u8))), 0);
        }
        _jitStdOutFile = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStdOutFile"u8))));
        _richDebugInfo = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("RichDebugInfo"u8))), 0);
        if (Configuration.DEBUG)
        {
            _configurationData!._debugData!._writeRichDebugInfoFile = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("WriteRichDebugInfoFile"u8))));
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
                _configurationData!._debugData!._jitStressRex2Encoding = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressRex2Encoding"u8))), 0);
                _configurationData!._debugData!._jitStressPromotedEvexEncoding = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressPromotedEvexEncoding"u8))), 0);
            }
        }
        if (Configuration.TARGET_AMD64 || Configuration.TARGET_X86)
        {
            if (Configuration.DEBUG)
            {
                _configurationData!._debugData!._jitStressEvexEncoding = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressEvexEncoding"u8))), 0);
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
            _configurationData!._targetXarchData!._enableAVX = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVX"u8))), 1);
            _configurationData!._targetXarchData!._enableAVX2 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVX2"u8))), 1);
            _configurationData!._targetXarchData!._enableAVX512 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVX512"u8))), 1);
            _configurationData!._targetXarchData!._enableAVX512BMM = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVX512BMM"u8))), 1);
            _configurationData!._targetXarchData!._enableAVX512v2 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVX512v2"u8))), 1);
            _configurationData!._targetXarchData!._enableAVX512v3 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVX512v3"u8))), 1);
            _configurationData!._targetXarchData!._enableAVX10v1 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVX10v1"u8))), 1);
            _configurationData!._targetXarchData!._enableAVX10v2 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVX10v2"u8))), 0);
            _configurationData!._targetXarchData!._enableAPX = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAPX"u8))), 0);
            _configurationData!._targetXarchData!._enableAES = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAES"u8))), 1);
            _configurationData!._targetXarchData!._enableAVX512VP2INTERSECT = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVX512VP2INTERSECT"u8))), 1);
            _configurationData!._targetXarchData!._enableAVXIFMA = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVXIFMA"u8))), 1);
            _configurationData!._targetXarchData!._enableAVXVNNI = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVXVNNI"u8))), 1);
            _configurationData!._targetXarchData!._enableAVXVNNIINT = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableAVXVNNIINT"u8))), 1);
            _configurationData!._targetXarchData!._enableGFNI = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableGFNI"u8))), 1);
            _configurationData!._targetXarchData!._enableSHA = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableSHA"u8))), 1);
            _configurationData!._targetXarchData!._enableVAES = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableVAES"u8))), 1);
            _configurationData!._targetXarchData!._enableWAITPKG = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableWAITPKG"u8))), 1);
            _configurationData!._targetXarchData!._enableX86Serialize = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableX86Serialize"u8))), 1);
        }
        else if (Configuration.TARGET_ARM64)
        {
            _configurationData!._targetArm64Data!._enableArm64Aes = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Aes"u8))), 1);
            _configurationData!._targetArm64Data!._enableArm64Atomics = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Atomics"u8))), 1);
            _configurationData!._targetArm64Data!._enableArm64Crc32 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Crc32"u8))), 1);
            _configurationData!._targetArm64Data!._enableArm64Cssc = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Cssc"u8))), 1);
            _configurationData!._targetArm64Data!._enableArm64Dczva = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Dczva"u8))), 1);
            _configurationData!._targetArm64Data!._enableArm64Dp = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Dp"u8))), 1);
            _configurationData!._targetArm64Data!._enableArm64Fp16 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Fp16"u8))), 1);
            _configurationData!._targetArm64Data!._enableArm64Rdm = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Rdm"u8))), 1);
            _configurationData!._targetArm64Data!._enableArm64Sha1 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Sha1"u8))), 1);
            _configurationData!._targetArm64Data!._enableArm64Sha256 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Sha256"u8))), 1);
            _configurationData!._targetArm64Data!._enableArm64Sve = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Sve"u8))), 1);
            _configurationData!._targetArm64Data!._enableArm64Sve2 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Sve2"u8))), 1);
            _configurationData!._targetArm64Data!._enableArm64Sha3 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Sha3"u8))), 1);
            _configurationData!._targetArm64Data!._enableArm64Sm4 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64Sm4"u8))), 1);
            _configurationData!._targetArm64Data!._enableArm64SveAes = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64SveAes"u8))), 1);
            _configurationData!._targetArm64Data!._enableArm64SvePmull128 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64SvePmull128"u8))), 1);
            _configurationData!._targetArm64Data!._enableArm64SveSha3 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64SveSha3"u8))), 1);
            _configurationData!._targetArm64Data!._enableArm64SveSm4 = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableArm64SveSm4"u8))), 1);
        }
        else if (Configuration.TARGET_RISCV64)
        {
            _configurationData!._targetRiscV64Data!._enableRiscV64Zba = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableRiscV64Zba"u8))), 1);
            _configurationData!._targetRiscV64Data!._enableRiscV64Zbb = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableRiscV64Zbb"u8))), 1);
            _configurationData!._targetRiscV64Data!._enableRiscV64Zbs = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableRiscV64Zbs"u8))), 1);
            _configurationData!._targetRiscV64Data!._enableRiscV64Zicond = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableRiscV64Zicond"u8))), 1);
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
            _configurationData!._featureSimdData!._jitDisableSimdVN = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisableSimdVN"u8))), 0);
        }
        _jitConstCSE = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitConstCSE"u8))), CONST_CSE_ENABLE_ARM_RISCV64);
        _jitRLCSEGreedy = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRLCSEGreedy"u8))), 0);
        _jitRLCSEVerbose = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRLCSEVerbose"u8))), 0);
        if (Configuration.DEBUG)
        {
            _configurationData!._debugData!._jitCSEHash = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCSEHash"u8))), -1);
            _configurationData!._debugData!._jitCSEMask = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCSEMask"u8))), 0);
            _configurationData!._debugData!._jitMetrics = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitMetrics"u8))), 0);
            _configurationData!._debugData!._jitRandomCSE = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRandomCSE"u8))), 0);
            _configurationData!._debugData!._jitReplayCSE = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitReplayCSE"u8))));
            _configurationData!._debugData!._jitReplayCSEReward = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitReplayCSEReward"u8))));
            _configurationData!._debugData!._jitRLCSE = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRLCSE"u8))));
            _configurationData!._debugData!._jitRLCSEAlpha = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRLCSEAlpha"u8))));
            _configurationData!._debugData!._jitRLCSECandidateFeatures = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRLCSECandidateFeatures"u8))), 0);
            _configurationData!._debugData!._jitRLHook = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRLHook"u8))), 0);
            _configurationData!._debugData!._jitRLHookEmitFeatureNames = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRLHookEmitFeatureNames"u8))), 0);
            _configurationData!._debugData!._jitRLHookCSEDecisions = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRLHookCSEDecisions"u8))));
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
            _configurationData!._debugData!._jitEnregStats = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnregStats"u8))), 0);
        }
        _jitAggressiveInlining = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAggressiveInlining"u8))), 0);
        _jitELTHookEnabled = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitELTHookEnabled"u8))), 0);
        _jitInlineSIMDMultiplier = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineSIMDMultiplier"u8))), 3);
        _jitMaxLocalsToTrack = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitMaxLocalsToTrack"u8))), 0x400);
        if (Configuration.DEBUG)
        {
            _configurationData!._debugData!._jitDoAssertionProp = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoAssertionProp"u8))), 1);
            _configurationData!._debugData!._jitDoCopyProp = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoCopyProp"u8))), 1);
            _configurationData!._debugData!._jitDoOptimizeIVs = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoOptimizeIVs"u8))), 1);
            _configurationData!._debugData!._jitDoEarlyProp = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoEarlyProp"u8))), 1);
            _configurationData!._debugData!._jitDoLoopHoisting = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoLoopHoisting"u8))), 1);
            _configurationData!._debugData!._jitDoLoopInversion = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoLoopInversion"u8))), 1);
        }
        _jitLoopInversionSizeLimit = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitLoopInversionSizeLimit"u8))), 100);
        if (Configuration.DEBUG)
        {
            _configurationData!._debugData!._jitDoRangeAnalysis = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoRangeAnalysis"u8))), 1);
            _configurationData!._debugData!._jitDoVNBasedDeadStoreRemoval = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoVNBasedDeadStoreRemoval"u8))), 1);
            _configurationData!._debugData!._jitDoRedundantBranchOpts = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoRedundantBranchOpts"u8))), 1);
            _configurationData!._debugData!._jitEnableRboRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableRboRange"u8))));
            _configurationData!._debugData!._jitEnableHeadTailMergeRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableHeadTailMergeRange"u8))));
            _configurationData!._debugData!._jitEnableVNBasedDeadStoreRemovalRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableVNBasedDeadStoreRemovalRange"u8))));
            _configurationData!._debugData!._jitEnableEarlyLivenessRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableEarlyLivenessRange"u8))));
            _configurationData!._debugData!._jitOnlyOptimizeRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitOnlyOptimizeRange"u8))));
            _configurationData!._debugData!._jitEnablePhysicalPromotionRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnablePhysicalPromotionRange"u8))));
            _configurationData!._debugData!._jitEnableCrossBlockLocalAssertionPropRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableCrossBlockLocalAssertionPropRange"u8))));
            _configurationData!._debugData!._jitEnableInductionVariableOptsRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableInductionVariableOptsRange"u8))));
            _configurationData!._debugData!._jitEnableLocalAddrPropagationRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableLocalAddrPropagationRange"u8))));
            _configurationData!._debugData!._jitDoSsa = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoSsa"u8))), 1);
            _configurationData!._debugData!._jitDoValueNumber = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoValueNumber"u8))), 1);
            _configurationData!._debugData!._jitOptRepeatRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitOptRepeatRange"u8))));
            _configurationData!._debugData!._jitDoIfConversion = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoIfConversion"u8))), 1);
            _configurationData!._debugData!._jitDoOptimizeMaskConversions = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDoOptimizeMaskConversions"u8))), 1);
            _configurationData!._debugData!._jitOptimizeAwait = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitOptimizeAwait"u8))), 1);
            _configurationData!._debugData!._jitAsyncDefaultValueAnalysisRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAsyncDefaultValueAnalysisRange"u8))));
            _configurationData!._debugData!._jitAsyncPreservedValueAnalysisRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitAsyncPreservedValueAnalysisRange"u8))));
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
            _configurationData!._debugData!._enableExtraSuperPmiQueries = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("EnableExtraSuperPmiQueries"u8))), 0);
            _configurationData!._debugData!._jitInlineDumpData = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineDumpData"u8))), 0);
            _configurationData!._debugData!._jitInlineDumpXml = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineDumpXml"u8))), 0);
            _configurationData!._debugData!._jitInlineDumpXmlFile = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineDumpXmlFile"u8))));
            _configurationData!._debugData!._jitInlinePolicyDumpXml = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlinePolicyDumpXml"u8))), 0);
            _configurationData!._debugData!._jitInlineLimit = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineLimit"u8))), -1);
            _configurationData!._debugData!._jitInlinePolicyDiscretionary = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlinePolicyDiscretionary"u8))), 0);
            _configurationData!._debugData!._jitInlinePolicyFull = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlinePolicyFull"u8))), 0);
            _configurationData!._debugData!._jitInlinePolicySize = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlinePolicySize"u8))), 0);
            _configurationData!._debugData!._jitInlinePolicyRandom = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlinePolicyRandom"u8))), 0);
            _configurationData!._debugData!._jitInlinePolicyReplay = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlinePolicyReplay"u8))), 0);
            _configurationData!._debugData!._jitNoInlineRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoInlineRange"u8))));
            _configurationData!._debugData!._jitInlineReplayFile = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInlineReplayFile"u8))));
            _configurationData!._debugData!._jitStressAsyncInlining = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressAsyncInlining"u8))), 0);
            _configurationData!._debugData!._jitStressAsyncInliningMaxDepth = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressAsyncInliningMaxDepth"u8))), 8);
            _configurationData!._debugData!._jitStressAsyncInliningPct = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitStressAsyncInliningPct"u8))), 75);
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
            _configurationData!._debugData!._jitObjectStackAllocationRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationRange"u8))));
        }
        _jitObjectStackAllocation = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocation"u8))), 1);
        _jitObjectStackAllocationRefClass = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationRefClass"u8))), 1);
        _jitObjectStackAllocationBoxedValueClass = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationBoxedValueClass"u8))), 1);
        _jitObjectStackAllocationConditionalEscape = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationConditionalEscape"u8))), 1);
        if (Configuration.DEBUG)
        {
            _configurationData!._debugData!._jitObjectStackAllocationConditionalEscapeRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationConditionalEscapeRange"u8))));
        }
        _jitObjectStackAllocationArray = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationArray"u8))), 1);
        _jitObjectStackAllocationSize = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationSize"u8))), 528);
        _jitObjectStackAllocationTrackFields = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationTrackFields"u8))), 1);
        if (Configuration.DEBUG)
        {
            _configurationData!._debugData!._jitObjectStackAllocationTrackFieldsRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationTrackFieldsRange"u8))));
            _configurationData!._debugData!._jitObjectStackAllocationDumpConnGraph = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitObjectStackAllocationDumpConnGraph"u8))), 0);
        }
        _jitEECallTimingInfo = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEECallTimingInfo"u8))), 0);
        if (Configuration.DEBUG)
        {
            _configurationData!._debugData!._jitEnableFinallyCloning = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableFinallyCloning"u8))), 1);
            _configurationData!._debugData!._jitEnableRemoveEmptyTry = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableRemoveEmptyTry"u8))), 1);
            _configurationData!._debugData!._jitEnableRemoveEmptyTryCatchOrTryFault = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableRemoveEmptyTryCatchOrTryFault"u8))), 1);
            _configurationData!._debugData!._jitEnableRemoveUnreachableTry = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableRemoveUnreachableTry"u8))), 1);
        }
        _jitEnableGuardedDevirtualization = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableGuardedDevirtualization"u8))), 1);
        _jitGuardedDevirtualizationMaxTypeChecks = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitGuardedDevirtualizationMaxTypeChecks"u8))), -1);
        _jitGuardedDevirtualizationRequireInlining = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitGuardedDevirtualizationRequireInlining"u8))), 0);
        _jitGuardedDevirtualizationChainLikelihood = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitGuardedDevirtualizationChainLikelihood"u8))), 0x4B);
        _jitGuardedDevirtualizationChainStatements = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitGuardedDevirtualizationChainStatements"u8))), 1);
        if (Configuration.DEBUG)
        {
            _configurationData!._debugData!._jitGuardedDevirtualizationRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitGuardedDevirtualizationRange"u8))));
            _configurationData!._debugData!._jitRandomGuardedDevirtualization = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRandomGuardedDevirtualization"u8))), 0);
        }
        _tC_OnStackReplacement = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("TC_OnStackReplacement"u8))), Configuration.FEATURE_ON_STACK_REPLACEMENT ? 1 : 0);
        _tC_OnStackReplacement_InitialCounter = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("TC_OnStackReplacement_InitialCounter"u8))), 1000);
        _tC_PartialCompilation = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("TC_PartialCompilation"u8))), 0);
        if (Configuration.DEBUG)
        {
            _configurationData!._debugData!._jitRandomPartialCompilation = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRandomPartialCompilation"u8))), 0);
        }
        _tC_PatchpointStrategy = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("TC_PatchpointStrategy"u8))), 2);
        if (Configuration.DEBUG)
        {
            _configurationData!._debugData!._jitRandomOnStackReplacement = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRandomOnStackReplacement"u8))), 0);
            _configurationData!._debugData!._jitOffsetOnStackReplacement = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitOffsetOnStackReplacement"u8))), -1);
            _configurationData!._debugData!._jitEnableOsrRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnableOsrRange"u8))));
            _configurationData!._debugData!._jitEnablePatchpointRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnablePatchpointRange"u8))));
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
            _configurationData!._debugData!._jitInstrumentIfOptimizing = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInstrumentIfOptimizing"u8))), 0);
            _configurationData!._debugData!._jitInstrumentIfOptimizingRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInstrumentIfOptimizingRange"u8))));
        }
        _jitInstrumentInlinees = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitInstrumentInlinees"u8))), 1);
        _jitDisablePGO = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDisablePGO"u8))), 0);
        if (Configuration.DEBUG)
        {
            _configurationData!._debugData!._jitEnablePGORange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnablePGORange"u8))));
            _configurationData!._debugData!._jitRandomEdgeCounts = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRandomEdgeCounts"u8))), 0);
            _configurationData!._debugData!._jitCrossCheckDevirtualizationAndPGO = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitCrossCheckDevirtualizationAndPGO"u8))), 0);
            _configurationData!._debugData!._jitNoteFailedExactDevirtualization = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitNoteFailedExactDevirtualization"u8))), 0);
            _configurationData!._debugData!._jitRandomlyCollect64BitCounts = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRandomlyCollect64BitCounts"u8))), 0);
            _configurationData!._debugData!._jitSynthesizeCounts = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitSynthesizeCounts"u8))), 0);
            _configurationData!._debugData!._jitPropagateSynthesizedCountsToProfileData = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitPropagateSynthesizedCountsToProfileData"u8))), 0);
            _configurationData!._debugData!._jitSynthesisUseSolver = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitSynthesisUseSolver"u8))), 1);
            _configurationData!._debugData!._jitSynthesisExceptionWeight = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitSynthesisExceptionWeight"u8))));
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
            _configurationData!._debugData!._jitFunctionFile = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitFunctionFile"u8))));
            var jitRawHexCodeValue = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRawHexCode"u8))));
            _configurationData!._debugData!._jitRawHexCode = new MethodSet(jitRawHexCodeValue, jitHost);
            _configurationData!._debugData!._jitRawHexCodeFile = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitRawHexCodeFile"u8))));
        }
        if (Configuration.TARGET_ARM64)
        {
            if (Configuration.DEBUG)
            {
                _configurationData!._debugData!._jitSaveFpLrWithCalleeSavedRegisters = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitSaveFpLrWithCalleeSavedRegisters"u8))), 0);
            }
        }
        if (Configuration.DEBUG)
        {
            _configurationData!._debugData!._jitUseScalableVectorT = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitUseScalableVectorT"u8))), 0);
        }
        if (Configuration.TARGET_LOONGARCH64)
        {
            if (Configuration.DEBUG)
            {
                _configurationData!._debugData!._jitDispIns = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitDispIns"u8))), 0);
            }
        }
        if (Configuration.TARGET_WASM)
        {
            if (Configuration.DEBUG)
            {
                _configurationData!._debugData!._jitR2RUnsupportedRange = jitHost->getStringConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitR2RUnsupportedRange"u8))));
            }
            _configurationData!._targetWasmData!._jitWasmFunclets = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitWasmFunclets"u8))), 1);
        }
        _jitEnregStructLocals = jitHost->getIntConfigValue((byte*)(Unsafe.AsPointer(in MemoryMarshal.GetReference("JitEnregStructLocals"u8))), 1);
        _isInitialized = true;
    }
}