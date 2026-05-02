// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using static RyuJitSharp.Compiler.compStressArea;

namespace RyuJitSharp;

public partial class Compiler
{
    /// <summary>Always present</summary>
    public InlineContext? compInlineContext;

    public List<ICorDebugInfo.AsyncSuspensionPoint>? compSuspensionPoints;

    public List<ICorDebugInfo.AsyncContinuationVarInfo>? compAsyncVars;

    // Things that MAY belong either in CodeGen or CodeGenContext

    public unsafe FuncInfoDsc* compFuncInfos;

    public ushort compCurrFuncIdx;

    public ushort compFuncInfoCount;

    /// <summary>current live variables</summary>
    public VARSET_TP compCurLife;

    /// <summary>node after which compCurLife has been computed</summary>
    public GenTree? compCurLifeTree;

    /// <summary>The result of importing the inlinee method.</summary>
    public InlineResult? compInlineResult;

    // <summary>If true, mark every method as CORINFO_FLG_FORCEINLINE</summary>
    public bool compDoAggressiveInlining;

    // <summary>Does the method do a JMP</summary>
    public bool compJmpOpUsed;

    // <summary>Does the method use TYP_LONG</summary>
    public bool compLongUsed;

    // <summary>Does the method use TYP_FLOAT or TYP_DOUBLE</summary>
    public bool compFloatingPointUsed;

    // <summary>Does the method do a tailcall</summary>
    public bool compTailCallUsed;

    // <summary>Does the method IL have tail. prefix</summary>
    public bool compTailPrefixSeen;

    // <summary>Does the method IL have localloc opcode</summary>
    public bool compLocallocSeen;

    // <summary>Does the method use localloc.</summary>
    public bool compLocallocUsed;

    // <summary>Does the method have an optimized localloc</summary>
    public bool compLocallocOptimized;

    // <summary>Does the method use GT_QMARK/GT_COLON</summary>
    public bool compQmarkUsed;

    // <summary>Is it allowed to use a GT_QMARK/GT_COLON node.</summary>
    public bool compQmarkRationalized;

    // <summary>Does the method (or some inlinee) have a lexically backwards jump?</summary>
    public bool compHasBackwardJump;

    // <summary>Does the method have a lexically backwards jump in a handler?</summary>
    public bool compHasBackwardJumpInHandler;

    // <summary>Codegen initially was Tier0 but jit switched to FullOpts</summary>
    public bool compSwitchedToOptimized;

    // <summary>Codegen initially was Tier1/FullOpts but jit switched to MinOpts</summary>
    public bool compSwitchedToMinOpts;

    // <summary>There are vars with lvSuppressedZeroInit set</summary>
    public bool compSuppressedZeroInit;

    // <summary>Does the method have Convert Mask To Vector nodes.</summary>
    public bool compMaskConvertUsed;

    // <summary>There is a call to a THROW_HELPER for the compiled method.</summary>
    public bool compUsesThrowHelper;

    // NOTE: These values are only reliable after the importing is completely finished.

#if DEBUG
    // State information - which phases have completed?
    // These are kept together for easy discoverability

    public bool compAllowStress = true;

    public bool compCodeGenDone;

    /// <summary># of links traversed while doing debug checks</summary>
    public long compNumStatementLinksTraversed;

    /// <summary>The estimated size of the method as per `gtSetEvalOrder`.</summary>
    public nuint compSizeEstimate;

    /// <summary>The estimated cycle count of the method as per `gtSetEvalOrder`</summary>
    public nuint compCycleEstimate;

    /// <summary>Importer inserted IR before returns to poison implicit byrefs</summary>
    public bool compPoisoningAnyImplicitByrefs;
#endif

    public bool compPostImportationCleanupDone;

    public bool compRegAllocDone;

    public bool compRationalIRForm;

    public bool compGeneratingProlog;

    public bool compGeneratingEpilog;

    public bool compGeneratingUnwindProlog;

    public bool compGeneratingUnwindEpilog;

    /// <summary>There is an unsafe buffer (or localloc) on the stack.</summary>
    public bool compNeedsGSSecurityCookie;

    /// <summary>There is an unsafe buffer on the stack, reorder locals and make local copies of susceptible parameters to avoid buffer overrun attacks through locals/params</summary>
    public bool compGSReorderStackLayout;

#if DEBUG
    public string? compGSSecurityCheckBlocker;
#endif

#if DEBUG
    public static s_compStressModeNamesInlineArray s_compStressModeNames;

    public compActiveStressModesInlineArray compActiveStressModes;
#endif

    /// <summary>ABI return type descriptor for the method</summary>
    public ReturnTypeDesc compRetTypeDesc;

#if DEBUG
    /// <summary>to produce unique label names</summary>
    private static int s_compMethodsCount;
#endif

#if DEBUG
    internal int compGenTreeID;

    public uint compStatementID;
#endif

    public uint compBasicBlockID;

    public int compMethodID;

    /// <summary>the current basic block in process</summary>
    public BasicBlock? compCurBB;

    /// <summary>the current statement in process</summary>
    public Statement? compCurStmt;

    /// <summary>the current tree in process</summary>
    public GenTree? compCurTree;

    // The following is used to create the 'method JIT info' block.

    public nuint compInfoBlkSize;

    public unsafe byte* compInfoBlkAddr;

    /// <summary>array of EH data</summary>
    public unsafe EHblkDsc* compHndBBtab;

    /// <summary>element count of used elements in EH data array</summary>
    public uint compHndBBtabCount;

    /// <summary>element count of allocated elements in EH data array</summary>
    public uint compHndBBtabAllocCount;

    /// <summary>unique ID for EH data array entries</summary>
    public ushort compEHID;

    //-------------------------------------------------------------------------
    //  The following keeps track of how many bytes of local frame space we've
    //  grabbed so far in the current function, and how many argument bytes we
    //  need to pop when we return.
    //
    /// <summary>secObject+lclBlk+locals+temps</summary>
    public uint compLclFrameSize;

#if HAS_FIXED_REGISTER_SET
    /// <summary>Count of callee-saved regs we pushed in the prolog.</summary>
    /// <remarks>
    ///   <para>Does not include EBP for isFramePointerUsed() and double-aligned frames.</para>
    ///   <para>In case of Amd64 this doesn't include float regs saved on stack.</para>
    /// </remarks>
    public uint compCalleeRegsPushed;
#endif

#if TARGET_XARCH
    /// <summary>Mask of callee saved float regs on stack.</summary>
    public regMaskFlt compCalleeFPRegsSavedMask;
#endif

#if TARGET_ARM64
    public FrameInfo compFrameInfo;
#endif

    // Map to keep variables' scope indexed by varNum containing it's scope dscs at the index.
    public VarNumToScopeDscMap? compVarScopeMap;

    /// <summary>List has the offsets where variables enter scope, sorted by instr offset</summary>
    public unsafe VarScopeDsc** compEnterScopeList;

    public uint compNextEnterScope;

    /// <summary>List has the offsets where variables go out of scope, sorted by instr offset</summary>
    public unsafe VarScopeDsc** compExitScopeList;

    public uint compNextExitScope;

#if PROFILING_SUPPORTED
    // Data required for generating profiler Enter/Leave/TailCall hooks

    /// <summary>Whether profiler Enter/Leave/TailCall hook needs to be generated for the method</summary>
    protected bool compProfilerHookNeeded;

    /// <summary>Profiler handle of the method being compiled. Passed as param to ELT callbacks</summary>
    protected unsafe void* compProfilerMethHnd;

    /// <summary>Whether compProfilerHandle is pointer to the handle or is an actual handle</summary>
    protected bool compProfilerMethHndIndirected;
#endif

#if DEBUG
    public bool compDebugBreak;
#endif

#if FEATURE_JIT_METHOD_PERF
    /// <summary>Timer data structure (by phases) for current compilation.</summary>
    private unsafe JitTimer? pCompJitTimer;
    
    /// <summary>Summary of the Timer information for the whole run.</summary>
    private static CompTimeSummaryInfo s_compJitTimerSummary;
    
    /// <summary>If a log file for JIT time is desired, filename to write it to.</summary>
    private static string? compJitTimeLogFilename;
#endif

#if DEBUG
    // These variables are associated with maintaining SQM data about compile time.

    /// <summary>Raw timer count at the end of the inlining phase in the current compilation.</summary>
    private long m_compCyclesAtEndOfInlining;

    /// <summary>Wall clock elapsed time for current compilation (microseconds)</summary>
    private long m_compCycles;
#endif

#if FUNC_INFO_LOGGING
    /// <summary>If a log file for per-function information is required, this is the filename to write it to.</summary>
    public static string? compJitFuncInfoFilename;

    /// <summary>If a log file for per-function information is required, this is the stream to write to.</summary>
    public static StreamWriter? compJitFuncInfoFile;
#endif

    /// <summary>Returns true if the compiler instance is created for inlining.</summary>
    [MemberNotNullWhen(true, nameof(impInlineInfo))]
    public bool compIsForInlining => impInlineInfo is not null;

    public unsafe CorJitResult compCompileAfterInit(CORINFO_MODULE_HANDLE moduleHandle, out void* methodCodePtr, out uint methodCodeSize, in JitFlags compileFlags)
    {
        // TODO: Port compCompileAfterInit

        methodCodePtr = null;
        methodCodeSize = 0;

        return CORJIT_INTERNALERROR;
    }

    public unsafe void compFunctionTraceEnd(void* methodCodePtr, uint methodCodeSize, bool isNyi)
    {
        // TODO: Port compFunctionTraceEnd
    }

    public string compGetTieringName(bool wantShortName = false)
    {
        // TODO: Port compGetTieringName
        return "";
    }

    /// <summary>One-time initialization.</summary>
    public static void compStartup() // 11638
    {
        // TODO: Port compStartup
    }

    /// <summary>Should we actually fire the noway assert body and the exception handler?</summary>
    /// <returns></returns>
    public bool compShouldThrowOnNoway()
    {
        // TODO: Port compShouldThrowOnNoway
        return true;
    }

    /// <summary>One time finalization code.</summary>
    public static void compShutdown()
    {
        // TODO: Port compShutdown
    }

    [InlineArray((int)(STRESS_COUNT + 1))]
    public struct s_compStressModeNamesInlineArray
    {
        public string e0;
    }

    [InlineArray((int)(STRESS_COUNT))]
    public struct compActiveStressModesInlineArray
    {
        public byte e0;
    }
}
