// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using static RyuJitSharp.ICorJitInfo;

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

    public unsafe ushort* compVMClauseOrderToEHTabOrder;

    public unsafe ushort* compEHTabOrderToVMClauseOrder;

    /// <summary>current live variables</summary>
    public VARSET_TP compCurLife = [];

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
    public nint compSizeEstimate;

    /// <summary>The estimated cycle count of the method as per `gtSetEvalOrder`</summary>
    public nint compCycleEstimate;

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
    public static InlineArrayCompilerStressCount<string> s_compStressModeNames;

    public InlineArrayCompilerStressCount<byte> compActiveStressModes;
#endif

    /// <summary>ABI return type descriptor for the method</summary>
    public ReturnTypeDesc compRetTypeDesc;

#if DEBUG
    /// <summary>to produce unique label names</summary>
    private static int s_compMethodsCount;
#endif

#if DEBUG
    internal int compGenTreeID;
#endif

    public int compStatementID;

    public int compBasicBlockID;

    public int compMethodID;

    /// <summary>the current basic block in process</summary>
    public BasicBlock? compCurBB;

    /// <summary>the current statement in process</summary>
    public Statement? compCurStmt;

    /// <summary>the current tree in process</summary>
    public GenTree? compCurTree;

    // The following is used to create the 'method JIT info' block.

    public nint compInfoBlkSize;

    public unsafe byte* compInfoBlkAddr;

    /// <summary>array of EH data</summary>
    public EHblkDsc[] compHndBBtab = [];

    /// <summary>element count of used elements in EH data array</summary>
    public ushort compHndBBtabCount;

    /// <summary>unique ID for EH data array entries</summary>
    public ushort compEHID;

    /// <summary>secObject+lclBlk+locals+temps</summary>
    /// <remarks>keeps track of how many bytes of local frame space we've grabbed so far in the current function, and how many argument bytes we need to pop when we return.</remarks>
    public int compLclFrameSize;

#if HAS_FIXED_REGISTER_SET
    /// <summary>Count of callee-saved regs we pushed in the prolog.</summary>
    /// <remarks>
    ///   <para>Does not include EBP for IsFramePointerUsed and double-aligned frames.</para>
    ///   <para>In case of Amd64 this doesn't include float regs saved on stack.</para>
    /// </remarks>
    public int compCalleeRegsPushed = -1;
#endif

#if TARGET_XARCH
    /// <summary>Mask of callee saved float regs on stack.</summary>
    public regMask compCalleeFPRegsSavedMask;
#endif

#if TARGET_ARM64
    public FrameInfo compFrameInfo;
#endif

    // Map to keep variables' scope indexed by varNum containing it's scope dscs at the index.
    public VarNumToScopeDscMap? compVarScopeMap;

    /// <summary>List has the offsets where variables enter scope, sorted by instr offset</summary>
    private int[] compEnterScopeIndices = [];

    public ref VarScopeDsc compEnterScopeList(int index) => ref info.compVarScopes[index];

    public int compNextEnterScopeIndex;

    /// <summary>List has the offsets where variables go out of scope, sorted by instr offset</summary>
    private int[] compExitScopeIndices = [];

    public ref VarScopeDsc compExitScopeList(int index) => ref info.compVarScopes[index];

    public int compNextExitScopeIndex;

    protected int compMaxUncheckedOffsetForNullObject;

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
    private unsafe JitTimer? compJitTimer;
    
    /// <summary>Summary of the Timer information for the whole run.</summary>
    private static CompTimeSummaryInfo s_compJitTimerSummary;
    
    /// <summary>If a log file for JIT time is desired, filename to write it to.</summary>
    private static nint compJitTimeLogFilename;
#endif

#if DEBUG
    // These variables are associated with maintaining SQM data about compile time.

    /// <summary>Raw timer count at the end of the inlining phase in the current compilation.</summary>
    private long _compCyclesAtEndOfInlining;

    /// <summary>Wall clock elapsed time for current compilation (microseconds)</summary>
    private long _compCycles;
#endif

#if FUNC_INFO_LOGGING
    /// <summary>If a log file for per-function information is required, this is the filename to write it to.</summary>
    public static nint compJitFuncInfoFilename;

    /// <summary>If a log file for per-function information is required, this is the stream to write to.</summary>
    public static StreamWriter? compJitFuncInfoFile;
#endif

#if false
    // Switching between size & speed has measurable throughput impact
    // (3.5% on AOT CoreLib when measured). It used to be enabled for
    // DEBUG, but should generate identical code between CHK & RET builds,
    // so that's not acceptable.
    // TODO-Throughput: Figure out what to do about size vs. speed & throughput.
    //                  Investigate the cause of the throughput regression.
    public codeOptimize compCodeOpt => opts.compCodeOpt;
#else
    public codeOptimize compCodeOpt => BLENDED_CODE;
#endif

    [MemberNotNullWhen(true, nameof(impInlineInfo), nameof(compInlineResult))]
    public bool compDonotInline
    {
        get
        {
            if (compIsForInlining)
            {
                assert(compInlineResult is not null);
                return compInlineResult.IsFailure;
            }
            else
            {
                return false;
            }
        }
    }

    public bool compEnregLocals => (opts.compFlags & CLFLG_REGVAR) != 0;

    public unsafe bool compIsAsync => opts.jitFlags->IsSet(JitFlags.JIT_FLAG_ASYNC);

    /// <summary>Returns true if the compiler instance is created for inlining.</summary>
    [MemberNotNullWhen(true, nameof(impInlineInfo), nameof(compInlineResult))]
    [MemberNotNullWhen(false, nameof(codeGen), nameof(_inlineStrategy))]
    public bool compIsForInlining => impInlineInfo is not null;

    /// <summary>Does this method return a multi-reg value?</summary>
    public bool compMethodReturnsMultiRegRetType => compRetTypeDesc.IsMultiRegRetType;

    /// <summary>Returns true if the method being compiled returns RetBuf addr as its return value</summary>
    /// <remarks>
    ///   <para>There are cases where implicit RetBuf argument should be explicitly returned in a register.</para>
    ///   <para>In such cases the return type is changed to TYP_BYREF and appropriate IR is generated.</para>
    /// </remarks>
#if TARGET_AMD64
    // 1. on x64 Windows and Unix the address of RetBuf needs to be returned by
    //    methods with hidden RetBufArg in RAX. In such case GT_RETURN is of TYP_BYREF,
    //    returning the address of RetBuf.
    public bool compMethodReturnsRetBufAddr => info.compRetBuffArg != BAD_VAR_NUM;
#else
    public bool compMethodReturnsRetBufAddr
    {
        get
        {
#if PROFILING_SUPPORTED
            // 2. Profiler Leave callback expects the address of retbuf as return value for
            //    methods with hidden RetBuf argument.  impReturnInstruction() when profiler
            //    callbacks are needed creates GT_RETURN(TYP_BYREF, op1 = Addr of RetBuf) for
            //    methods with hidden RetBufArg.
            if (compIsProfilerHookNeeded)
            {
                return info.compRetBuffArg is not BAD_VAR_NUM;
            }
#endif

#if TARGET_ARM64
            if (TargetOS.IsWindows)
            {
                // 3. Windows ARM64 native instance calling convention requires the address of RetBuff to be returned in x0.

                if (callConvIsInstanceMethodCallConv(info.compCallConv))
                {
                    return info.compRetBuffArg is not BAD_VAR_NUM;
                }
            }
#elif TARGET_X86
            if (info.compCallConv is not CorInfoCallConvExtension.Managed)
            {
                // 4. x86 unmanaged calling conventions require the address of RetBuff to be returned in eax.
                return info.compRetBuffArg is not BAD_VAR_NUM;
            }
#endif

            return false;
        }
    }
#endif

    // Object stack allocation takes the address of locals around suspension points. Disable entirely under async for now.
    public bool compObjectStackAllocation => !compIsAsync && (JitConfig.JitObjectStackAllocation != 0);

    /// <summary>get a string describing PGO source</summary>
    public string compPgoSourceName => fgPgoSource switch {
        PgoSource.Unknown => "Unknown PGO",
        PgoSource.Static => "Static PGO",
        PgoSource.Dynamic => "Dynamic PGO",
        PgoSource.Blend => "Blended PGO",
        PgoSource.Text => "Textual PGO",
        PgoSource.IBC => "Classic IBC",
        PgoSource.Sampling => "Sample-based PGO",
        PgoSource.Synthesis => "Synthesized PGO",
        _ => "Unknown PGO",
    };

    /// <summary>Should we actually fire the noway assert body and the exception handler?</summary>
    /// <returns></returns>
    /// <remarks>In min opts, we don't want the noway assert to go through the exception path. Instead we want it to just silently go through codegen for compat reasons.</remarks>
    public bool compShouldThrowOnNoway => !opts.MinOpts;

    /// <summary>get a string describing jitstress capability for this method</summary>
    /// <remarks>Returns an empty string if stress is not enabled, else a string describing if this method is subject to stress or is excluded by name or hash.</remarks>
    public unsafe string compStressMessage
    {
        get
        {
            var stressMessage = "";

#if DEBUG
            // Is stress enabled via mode name or level?
            if ((JitConfig.JitStressModeNames is not null) || (JitStressLevel > 0))
            {
                // Is the method being jitted excluded from stress via range?
                if (compAllowStress)
                {
                    // Not excluded -- stress can happen
                    stressMessage = " JitStress";
                }
                else
                {
                    stressMessage = " NoJitStress";
                }
            }
#endif

            return stressMessage;
        }
    }

#if DEBUG
    public unsafe bool compTailCallStress
    {
        get
        {
            // Do not stress tailcalls in IL stubs as the runtime creates several IL stubs to implement the tailcall mechanism, which would then recursively create more IL stubs.
            // Tailcalls are also not allowed out of async methods, so do not stress in those either.
            var result = false;

            if (!opts.jitFlags->IsSet(JitFlags.JIT_FLAG_IL_STUB) && !compIsAsync)
            {
                if ((JitConfig.TailcallStress is not 0) || compStressCompile(STRESS_TAILCALL, 5))
                {
                    result = true;
                }
            }
            return result;
        }
    }
#else
    public bool compTailCallStress => false;
#endif

    /// <summary>Classify the type of GDV probe to use for a call site.</summary>
    /// <param name="call">The call</param>
    /// <returns>The type of probe to use.</returns>
    public unsafe GDVProbeType compClassifyGDVProbeType(GenTreeCall call)
    {
        if (!opts.jitFlags->IsSet(JitFlags.JIT_FLAG_BBINSTR) || IsAot)
        {
            return GDVProbeType.None;
        }

        var createTypeHistogram = false;

        if (JitConfig.JitClassProfiling > 0)
        {
            createTypeHistogram = call.IsVirtualStub || call.IsVirtualVtable;

            // Cast helpers may conditionally (depending on whether the class is
            // exact or not) have probes. For those helpers we do not use this
            // function to classify the probe type until after we have decided on
            // whether we probe them or not.
            createTypeHistogram = createTypeHistogram || (impIsCastHelperEligibleForClassProbe(call) && (call._handleHistogramProfileCandidateInfo is not null));
        }

        var createMethodHistogram = ((JitConfig.JitDelegateProfiling > 0) && call.IsDelegateInvoke) ||
                                    ((JitConfig.JitVTableProfiling > 0) && call.IsVirtualVtable);

        if (createTypeHistogram)
        {
            return createMethodHistogram ? GDVProbeType.MethodAndClassProfile : GDVProbeType.ClassProfile;
        }

        if (createMethodHistogram)
        {
            return GDVProbeType.MethodProfile;
        }

        return GDVProbeType.None;
    }

    public static void compDisplayStaticSizes()
    {
#if MEASURE_NODE_SIZE
        GenTree.DumpNodeSizes();
#endif

#if EMITTER_STATS
        emitterStaticStats();
#endif
    }

#if DEBUG
    public unsafe void compDispLocalVars()
    {
        jitprintf($"info.compVarScopesCount = {info.compVarScopesCount}\n");

        if (info.compVarScopesCount > 0)
        {
            jitprintf("    \tVarNum \tLVNum \t      Name \tBeg \tEnd\n");
        }

        var varScopes = info.compVarScopes.AsSpan(0, info.compVarScopesCount);

        for (var i = 0; i < varScopes.Length; i++)
        {
            ref var varScope = ref varScopes[i];
            jitprintf($"{i,2}: \t{varScope.vsdVarNum:X2}h \t{varScope.vsdLVnum:X2}h \t{varScope.vsdName ?? "UNKNOWN",10} \t{varScope.vsdLifeBeg:X3}h   \t{varScope.vsdLifeEnd:X3}h\n");
        }
    }

    /// <summary>Components used by the compiler may write unit test suites, and have them run within this method.</summary>
    /// <remarks>
    ///   <para>They will be run only once per process, and only in debug (Perhaps should be under the control of a DOTNET_ flag.)</para>
    ///   <para>These should fail by asserting.</para>
    /// </remarks>
    public void compDoComponentUnitTestsOnce()
    {
        // TODO: Port Compiler.compDoComponentUnitTestsOnce
    }
#endif

    /// <summary>One time finalization code.</summary>
    public static void compShutdown()
    {
        // TODO: Port compShutdown
    }

    /// <summary>One-time initialization.</summary>
    public static void compStartup()
    {
#if DISPLAY_SIZES
        grossVMsize = grossNCsize = totalNCsize = 0;
#endif

        // Initialize the table of tree node sizes
        GenTree.InitNodeSize();

#if JIT32_GCENCODER
        // Initialize the GC encoder lookup table
        GCInfo.gcInitEncoderLookupTable();
#endif

        // Initialize the emitter
        Emitter.emitInit();

        // Static vars of ValueNumStore
        ValueNumStore.ValidateValueNumStoreStatics();

        compDisplayStaticSizes();
    }

#if DEBUG
    private static ConfigMethodRange s_jitInstrumentIfOptimizingRange;
#endif

    private static bool s_checkedForJitTimeLog;

    public unsafe CorJitResult compCompileAfterInit(CORINFO_MODULE_HANDLE moduleHandle, out void* methodCodePtr, out int methodCodeSize, JitFlags* jitFlags)
    {
        // compInit should have set these already.
        noway_assert(info.compMethodInfo is not null);
        noway_assert(info.compCompHnd is not null);
        noway_assert(info.compMethodHnd is not null);

#if FEATURE_JIT_METHOD_PERF
        if (!s_checkedForJitTimeLog)
        {
            _ = Interlocked.CompareExchange(ref compJitTimeLogFilename, (nint)(JitConfig.JitTimeLogFile), comparand: 0);

            // At a process or module boundary clear the file and start afresh.
            JitTimer.PrintCsvHeader();

            s_checkedForJitTimeLog = true;
        }

        if ((compJitTimeLogFilename is not 0) || (JitTimeLogCsv is ""))
        {
            compJitTimer = new JitTimer(info.compMethodInfo->ILCodeSize);
        }
#endif

#if FUNC_INFO_LOGGING
        var pTmpJitFuncInfoFilenameUtf8 = JitConfig.JitFuncInfoFile;

        if (pTmpJitFuncInfoFilenameUtf8 is not null)
        {
            var pOldFuncInfoFileNameUtf8 = (byte*)(Interlocked.CompareExchange(ref compJitFuncInfoFilename, (nint)(pTmpJitFuncInfoFilenameUtf8), 0));

            if (pOldFuncInfoFileNameUtf8 is null)
            {
                var tmpJitFuncInfoFilenameUtf8 = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(pTmpJitFuncInfoFilenameUtf8);
                var tmpJitFuncInfoFilename = Encoding.UTF8.GetString(tmpJitFuncInfoFilenameUtf8);

                assert(compJitFuncInfoFile is null);
                compJitFuncInfoFile = new StreamWriter(tmpJitFuncInfoFilename, append: true);
            }
        }
#endif

        // if (s_compMethodsCount==0) setvbuf(jitstdout(), null, _IONBF, 0);

        if (compIsForInlining)
        {
            jitFlags->Clear(JitFlags.JIT_FLAG_OSR);
            info.compILEntry = 0;
            info.compPatchpointInfo = null;
        }
        else if (jitFlags->IsSet(JitFlags.JIT_FLAG_OSR))
        {
            // Fetch OSR info from the runtime
            fixed (int* pILEntry = &info.compILEntry)
            {
                info.compPatchpointInfo = info.compCompHnd->getOSRInfo(pILEntry);
            }
            assert(info.compPatchpointInfo is not null);
        }

        // If we are not compiling for a matched VM, then we are getting JIT flags that don't match our target
        // architecture. The two main examples here are an ARM targeting altjit hosted on x86 and an ARM64
        // targeting altjit hosted on x64. (Though with cross-bitness work, the host doesn't necessarily need
        // to be of the same bitness.) In these cases, we need to fix up the JIT flags to be appropriate for
        // the target, as the VM's expected target may overlap bit flags with different meaning to our target.
        // Note that it might be better to do this immediately when setting the JIT flags in CILJit.compileMethod()
        // (when JitFlags.SetFromFlags() is called), but this is close enough. (To move this logic to
        // CILJit.compileMethod() would require moving the info.compMatchedVM computation there as well.)
        //
        // We additionally want to do this for AltJit so that we can validate ISAs that the underlying CPU may
        // not support directly. Doing this check later, after opts.altJit has been initialized might be better
        // but it requires moving the whole set of logic down into compCompileHelper after compInitOptions has
        // run and we're going to end up exiting early if JIT_FLAG_ALT_JIT and opts.altJit don't match anyways

        var enableAvailableIsas = !info.compMatchedVM;

#if DEBUG
        if (jitFlags->IsSet(JitFlags.JIT_FLAG_ALT_JIT) && (JitConfig.RunAltJitCode == 0))
        {
            enableAvailableIsas = true;
        }
#endif

        if (enableAvailableIsas)
        {
            var currentInstructionSetFlags = jitFlags->GetInstructionSetFlags();
            Unsafe.SkipInit(out CORINFO_InstructionSetFlags instructionSetFlags);

            // We need to assume, by default, that all flags coming from the VM are invalid.
            instructionSetFlags.Reset();

            // We then add each available instruction set for the target architecture provided
            // that the corresponding JitConfig switch hasn't explicitly asked for it to be
            // disabled. This allows us to default to "everything" supported for altjit scenarios
            // while also still allowing instruction set opt-out providing users with the ability
            // to, for example, see and debug ARM64 codegen for any desired CPU configuration without
            // needing to have the hardware in question.

#if TARGET_ARM64
            if (info.compMatchedVM)
            {
                // Keep the existing VectorT* ISAs.
                if (currentInstructionSetFlags.HasInstructionSet(InstructionSet_VectorT128))
                {
                    instructionSetFlags.AddInstructionSet(InstructionSet_VectorT128);
                }

#if DEBUG
                if ((JitConfig.JitUseScalableVectorT != 0) && currentInstructionSetFlags.HasInstructionSet(InstructionSet_VectorT))
                {
                    // Vector<T> will use SVE instead of NEON.
                    instructionSetFlags.RemoveInstructionSet(InstructionSet_VectorT128);
                    instructionSetFlags.AddInstructionSet(InstructionSet_VectorT);
                }
#endif
            }

            instructionSetFlags.AddInstructionSet(InstructionSet_ArmBase);
            instructionSetFlags.AddInstructionSet(InstructionSet_AdvSimd);

            if (JitConfig.EnableArm64Aes != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_Aes);
            }

            if (JitConfig.EnableArm64Crc32 != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_Crc32);
            }

            if (JitConfig.EnableArm64Dp != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_Dp);
            }

            if (JitConfig.EnableArm64Rdm != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_Rdm);
            }

            if (JitConfig.EnableArm64Sha1 != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_Sha1);
            }

            if (JitConfig.EnableArm64Sha256 != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_Sha256);
            }

            if (JitConfig.EnableArm64Atomics != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_Atomics);
            }

            if (JitConfig.EnableArm64Dczva != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_Dczva);
            }

            if (JitConfig.EnableArm64Sve != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_Sve);
            }

            if (JitConfig.EnableArm64Sve2 != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_Sve2);
            }

            if (JitConfig.EnableArm64Sha3 != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_Sha3);
            }

            if (JitConfig.EnableArm64Sm4 != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_Sm4);
            }

            if (JitConfig.EnableArm64SveAes != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_SveAes);
            }

            if (JitConfig.EnableArm64SveSha3 != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_SveSha3);
            }

            if (JitConfig.EnableArm64SveSm4 != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_SveSm4);
            }
#elif TARGET_XARCH
            if (info.compMatchedVM)
            {
                // Keep the existing VectorT* ISAs.
                if (currentInstructionSetFlags.HasInstructionSet(InstructionSet_VectorT128))
                {
                    instructionSetFlags.AddInstructionSet(InstructionSet_VectorT128);
                }
                if (currentInstructionSetFlags.HasInstructionSet(InstructionSet_VectorT256))
                {
                    instructionSetFlags.AddInstructionSet(InstructionSet_VectorT256);
                }
                if (currentInstructionSetFlags.HasInstructionSet(InstructionSet_VectorT512))
                {
                    instructionSetFlags.AddInstructionSet(InstructionSet_VectorT512);
                }
            }

            instructionSetFlags.AddInstructionSet(InstructionSet_X86Base);

            if (JitConfig.EnableAVX != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_AVX);
            }

            if (JitConfig.EnableAVX2 != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_AVX2);
            }

            if (JitConfig.EnableAVX512 != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_AVX512);
            }

            if (JitConfig.EnableAVX512v2 != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_AVX512v2);
            }

            if (JitConfig.EnableAVX512v3 != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_AVX512v3);
            }

            if (JitConfig.EnableAVX10v1 != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_AVX10v1);
            }

            if (JitConfig.EnableAVX10v2 != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_AVX10v2);
            }

            if (JitConfig.EnableAPX != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_APX);
            }

            if (JitConfig.EnableAES != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_AES);

                if (JitConfig.EnableVAES != 0)
                {
                    instructionSetFlags.AddInstructionSet(InstructionSet_AES_V256);
                    instructionSetFlags.AddInstructionSet(InstructionSet_AES_V512);
                }
            }

            if (JitConfig.EnableAVX512VP2INTERSECT != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_AVX512VP2INTERSECT);
            }

            if (JitConfig.EnableAVXIFMA != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_AVXIFMA);
            }

            if (JitConfig.EnableAVXVNNI != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_AVXVNNI);
            }

            if (JitConfig.EnableGFNI != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_GFNI);
                instructionSetFlags.AddInstructionSet(InstructionSet_GFNI_V256);
                instructionSetFlags.AddInstructionSet(InstructionSet_GFNI_V512);
            }

            if (JitConfig.EnableSHA != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_SHA);
            }

            if (JitConfig.EnableWAITPKG != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_WAITPKG);
            }

            if (JitConfig.EnableX86Serialize != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_X86Serialize);
            }
#elif TARGET_RISCV64
            instructionSetFlags.AddInstructionSet(InstructionSet_RiscV64Base);

            if (JitConfig.EnableRiscV64Zba != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_Zba);
            }

            if (JitConfig.EnableRiscV64Zbb != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_Zbb);
            }
#endif

            // These calls are important and explicitly ordered to ensure that the flags are correct in
            // the face of missing or removed instruction sets. Without them, we might end up with incorrect
            // downstream checks.

            instructionSetFlags.Set64BitInstructionSetVariants();
            instructionSetFlags = EnsureInstructionSetFlagsAreValid(instructionSetFlags);

            jitFlags->SetInstructionSetFlags(instructionSetFlags);
        }

        // Set the context for token lookup.
        if (compIsForInlining)
        {
            impTokenLookupContextHandle = impInlineInfo.tokenLookupContextHandle;

            var inlineCandidateInfo = impInlineInfo.inlineCandidateInfo;
            assert(inlineCandidateInfo.clsHandle == info.compClassHnd);
            assert(inlineCandidateInfo.clsAttr == info.compCompHnd->getClassAttribs(info.compClassHnd));

            // jitprintf($"{inlineCandidateInfo.clsAttr:x} != {info.compCompHnd->getClassAttribs(info.compClassHnd):x}\n");
            info.compClassAttr = inlineCandidateInfo.clsAttr;
        }
        else
        {
            impTokenLookupContextHandle = METHOD_BEING_COMPILED_CONTEXT();
            info.compClassAttr = info.compCompHnd->getClassAttribs(info.compClassHnd);
        }

#if DEBUG
        if (JitConfig.EnableExtraSuperPmiQueries != 0)
        {
            // Get the assembly name, to aid finding any particular SuperPMI method context function
            _ = eeGetClassAssemblyName(info.compClassHnd);

            // Fetch class names for the method's generic parameters.
            CORINFO_SIG_INFO sig;
            info.compCompHnd->getMethodSig(info.compMethodHnd, &sig, null);

            var classInst = sig.sigInst.classInstCount;

            if (classInst > 0)
            {
                for (var i = 0; i < classInst; i++)
                {
                    _ = eeGetClassName(sig.sigInst.classInst[i]);
                }
            }

            var methodInst = sig.sigInst.methInstCount;

            if (methodInst > 0)
            {
                for (var i = 0; i < methodInst; i++)
                {
                    _ = eeGetClassName(sig.sigInst.methInst[i]);
                }
            }
        }
#endif

#if DEBUG
        if (!compIsForInlining)
        {
            JitTls.LogEnv.Compiler = this;
        }

        // Have we been told to be more selective in our Jitting?
        if (SkipMethod())
        {
            methodCodePtr = null;
            methodCodeSize = 0;

            if (compIsForInlining)
            {
                compInlineResult.NoteFatal(InlineObservation.CALLEE_MARKED_AS_SKIPPED);
            }
            return CORJIT_SKIPPED;
        }
#endif

        var result = CORJIT_INTERNALERROR;

        try
        {
            result = compCompileHelper(moduleHandle, info.compCompHnd, info.compMethodInfo, out methodCodePtr, out methodCodeSize, jitFlags);
        }
        finally
        {
            if (!compIsForInlining)
            {
                // Tell the emitter that we're done with this function
                codeGen.Emitter.emitEndCG();
            }
            compDone();
        }

        return result;
    }

    public void compCompileFinish()
    {
        // TODO: Port Compiler.compCompileFinish
    }

#if DEBUG
    private static ConfigMethodRange s_jitEnableOsrRange;
#endif

    /// <summary>Returns true if the jit supports having patchpoints in this method.</summary>
    public bool compCanHavePatchpoints() => compCanHavePatchpoints(out _);

    /// <summary>Returns true if the jit supports having patchpoints in this method.</summary>
    public bool compCanHavePatchpoints(out string reason)
    {
        var whyNot = "";

#if FEATURE_ON_STACK_REPLACEMENT
        if (compLocallocSeen)
        {
            whyNot = "OSR can't handle localloc";
        }
        else if (compHasBackwardJumpInHandler)
        {
            whyNot = "OSR can't handle loop in handler";
        }
        else if (opts.IsReversePInvoke)
        {
            whyNot = "OSR can't handle reverse pinvoke";
        }
        else if (!info.compIsStatic && !lvaIsOriginalThisReadOnly)
        {
            whyNot = "OSR can't handle modifiable this";
        }
#else
        whyNot = "OSR feature not defined in build";
#endif

        reason = whyNot;
        return whyNot.Length == 0;
    }

    public unsafe CorJitResult compCompileHelper(CORINFO_MODULE_HANDLE classPtr, COMP_HANDLE compHnd, CORINFO_METHOD_INFO* methodInfo, out void* methodCodePtr, out int methodCodeSize, JitFlags* jitFlags)
    {
        if (info.compILCodeSize == 0)
        {
            BADCODE("code size is zero");
        }

        if (compIsForInlining)
        {
            var inlineCandidateInfo = impInlineInfo.inlineCandidateInfo;

#if DEBUG
            var methAttr_Old = inlineCandidateInfo.methAttr;
            var methAttr_New = info.compCompHnd->getMethodAttribs(info.compMethodHnd);
            var flagsToIgnore = CORINFO_FLG_DONT_INLINE | CORINFO_FLG_FORCEINLINE;
            assert((methAttr_Old & (~flagsToIgnore)) == (methAttr_New & (~flagsToIgnore)));
#endif

            info.compFlags = inlineCandidateInfo.methAttr;
            compInlineContext = impInlineInfo.inlineContext;
        }
        else
        {
            info.compFlags = info.compCompHnd->getMethodAttribs(info.compMethodHnd);
            compInlineContext = _inlineStrategy.RootContext;
        }

        // compInitOptions will set the correct verbose flag.

        compInitOptions(jitFlags);

        if (!compIsForInlining && !opts.altJit && opts.jitFlags->IsSet(JitFlags.JIT_FLAG_ALT_JIT))
        {
            // We're an altjit, but the DOTNET_AltJit configuration did not say to compile this method, so skip it.
            methodCodePtr = null;
            methodCodeSize = 0;
            return CORJIT_SKIPPED;
        }

#if DEBUG
        if (verbose)
        {
            jitprintf("IL to import:\n");
            dumpILRange(info.compCode, info.compILCodeSize);
        }
#endif

        // Check for DOTNET_AggressiveInlining
        if (JitConfig.JitAggressiveInlining != 0)
        {
            compDoAggressiveInlining = true;
        }

        if (compDoAggressiveInlining)
        {
            info.compFlags |= CORINFO_FLG_FORCEINLINE;
        }

#if DEBUG
        // Check for ForceInline stress.
        if (compStressCompile(STRESS_FORCE_INLINE, 0))
        {
            info.compFlags |= CORINFO_FLG_FORCEINLINE;
        }

        if (compIsForInlining)
        {
            JITLOG(LL_INFO100000, $"\nINLINER impTokenLookupContextHandle for {eeGetMethodFullName(info.compMethodHnd)} == 0x{FMT_DSP_PTR(impTokenLookupContextHandle)}.\n");
        }
#endif

        impCanReimport = compStressCompile(STRESS_CHK_REIMPORT, 15);

        // Initialize set a bunch of global values

        info.compScopeHnd = classPtr;
        info.compXcptnsCount = (ushort)(methodInfo->EHcount);
        info.compMaxStack = methodInfo->maxStack;

        if (!compIsForInlining)
        {
            // Initialize emitter
            codeGen.Emitter.emitBegCG(this, compHnd);
        }

        info.compIsStatic = (info.compFlags & CORINFO_FLG_STATIC) != 0;
        info.compPublishStubParam = opts.jitFlags->IsSet(JitFlags.JIT_FLAG_PUBLISH_SECRET_PARAM);

        if (opts.IsReversePInvoke)
        {
            bool unused;
            info.compCallConv = info.compCompHnd->getUnmanagedCallConv(methodInfo->ftn, null, &unused);
            info.compArgOrder = Target.TgtUnmanagedArgOrder;
        }
        else
        {
            info.compCallConv = CorInfoCallConvExtension.Managed;
            info.compArgOrder = Target.TgtArgOrder;
        }

        switch (methodInfo->args.getCallConv())
        {
            case CORINFO_CALLCONV_NATIVEVARARG:
            case CORINFO_CALLCONV_VARARG:
            {
                info.compIsVarArgs = true;
                break;
            }

            default:
            {
                break;
            }
        }

        info.compRetType = methodInfo->args.retType.VarType;

        if (info.compRetType is TYP_STRUCT)
        {
            info.compRetType = impNormStructType(methodInfo->args.retTypeClass);
        }

        info.compInitMem = (methodInfo->options & CORINFO_OPT_INIT_LOCALS) != 0;

        // Allocate the local variable table
        lvaInitTypeRef();

        compInitDebuggingInfo();

        // If are an altjit and have patchpoint info, we might need to tweak the frame size so it's plausible for the altjit architecture.
        if (!info.compMatchedVM && jitFlags->IsSet(JitFlags.JIT_FLAG_OSR))
        {
            assert(info.compLocalsCount == info.compPatchpointInfo->NumberOfLocals);
            var totalFrameSize = info.compPatchpointInfo->TotalFrameSize;

            var frameSizeUpdate = 0;

#if TARGET_AMD64
            if ((totalFrameSize % 16) != 8)
            {
                frameSizeUpdate = 8;
            }
#elif TARGET_ARM64 || TARGET_LOONGARCH64 || TARGET_RISCV64
            if ((totalFrameSize % 16) != 0)
            {
                frameSizeUpdate = 8;
            }
#endif

            if (frameSizeUpdate != 0)
            {
                JITDUMP($"Mismatched altjit + OSR -- updating tier0 frame size from {totalFrameSize} to {totalFrameSize + frameSizeUpdate}\n");

                // Allocate a local copy with altered frame size.
                var patchpointInfoSize = PatchpointInfo.ComputeSize(info.compLocalsCount);
                var newInfo = (PatchpointInfo*)(NativeMemory.Alloc((uint)(patchpointInfoSize)));

                newInfo->Initialize(info.compLocalsCount, totalFrameSize + frameSizeUpdate);
                newInfo->Copy(info.compPatchpointInfo);

                // Swap it in place.
                info.compPatchpointInfo = newInfo;
            }
        }

        if (compIsForInlining)
        {
            var inlinerCompiler = impInlineInfo.InlinerCompiler;
            compBasicBlockID = inlinerCompiler.compBasicBlockID;
        }

        var forceInline = (info.compFlags & CORINFO_FLG_FORCEINLINE) != 0;

        if (!compIsForInlining && IsAot)
        {
            // We're AOT compiling the root method.
            // We also will analyze it as a potential inline candidate.
            var prejitResult = new InlineResult(this, info.compMethodHnd, "prejit");

            // Profile data allows us to avoid early "too many IL bytes" outs.
            prejitResult.NoteBool(InlineObservation.CALLSITE_HAS_PROFILE_WEIGHTS, fgHaveSufficientProfileWeights);

            // Do the initial inline screen.
            impCanInlineIL(info.compMethodHnd, methodInfo, forceInline, prejitResult);

            // Temporarily install the prejitResult as the
            // compInlineResult so it's available to fgFindJumpTargets
            // and can accumulate more observations as the IL is
            // scanned.
            //
            // We don't pass prejitResult in as a parameter to avoid
            // potential aliasing confusion -- the other call to
            // fgFindBasicBlocks may have set up compInlineResult and
            // the code in fgFindJumpTargets references that data
            // member extensively.
            assert(compInlineResult is null);
            assert(impInlineInfo is null);
            compInlineResult = prejitResult;

            // Find the basic blocks.
            // We must do this regardless of inlineability, since we are prejitting this method.
            // This will also update the status of this method as an inline candidate.
            fgFindBasicBlocks();

            // Undo the temporary setup.
            assert(compInlineResult == prejitResult);
            compInlineResult = null;

            // If still a viable, discretionary inline, assess profitability.
            if (prejitResult.IsDiscretionaryCandidate)
            {
                prejitResult.DetermineProfitability(in *methodInfo);
            }

            _inlineStrategy.NotePrejitDecision(prejitResult);

            // Handle the results of the inline analysis.
            if (prejitResult.IsFailure)
            {
                // This method is a bad inlinee according to our analysis.
                // We will let the InlineResult destructor mark it as noinline in the prejit image to save the jit some work.
                // This decision better not be context-dependent.
                assert(prejitResult.IsNever);
            }
            else
            {
                // This looks like a viable inline candidate.
                // Since we're not actually inlining, don't report anything.
                prejitResult.Result = INLINE_PREJIT_SUCCESS;
            }
        }
        else
        {
            // We are jitting the root method, or inlining.
            fgFindBasicBlocks();
        }

        // If we're inlining and the candidate is bad, bail out.
        if (compDonotInline)
        {
            methodCodePtr = null;
            methodCodeSize = 0;
            return GetResult(this, compInlineResult);
        }

        // We may decide to optimize this method, to avoid spending a long time stuck in Tier0 code.
        if (fgCanSwitchToOptimized)
        {
            // We only expect to be able to do this at Tier0.
            assert(opts.jitFlags->IsSet(JitFlags.JIT_FLAG_TIER0));

            // Normal tiering should bail us out of Tier0 tail call induced loops.
            // So keep these methods in Tier0 if we're gathering PGO data.
            // If we're not gathering PGO, then switch these to optimized to
            // minimize the number of tail call helper stubs we might need.
            // Reconsider this if/when we're able to share those stubs.
            //
            // Honor the config setting that tells the jit to
            // always optimize methods with loops.
            //
            // If neither of those apply, and OSR is enabled, the jit may still
            // decide to optimize, if there's something in the method that
            // OSR currently cannot handle, or we're optionally suppressing
            // OSR by method hash.
            //
            var reason = "";

            if (compTailPrefixSeen && !opts.jitFlags->IsSet(JitFlags.JIT_FLAG_BBINSTR))
            {
                reason = "tail.call and not BBINSTR";
            }
            else if (compHasBackwardJump && ((info.compFlags & CORINFO_FLG_DISABLE_TIER0_FOR_LOOPS) != 0))
            {
                reason = "loop";
            }

            if (compHasBackwardJump && (reason.Length == 0) && (JitConfig.TC_OnStackReplacement > 0))
            {
                var canEscapeViaOSR = compCanHavePatchpoints(out reason);

#if DEBUG
                if (canEscapeViaOSR)
                {
                    // Optionally disable OSR by method hash.
                    // This will force any method that might otherwise get trapped in Tier0 to be optimized.
                    s_jitEnableOsrRange.EnsureInit(JitConfig.JitEnableOsrRange);

                    if (!s_jitEnableOsrRange.Contains(impInlineRoot.info.compMethodHash()))
                    {
                        canEscapeViaOSR = false;
                        reason = "OSR disabled by JitEnableOsrRange";
                    }
                }
#endif

                if (canEscapeViaOSR)
                {
                    JITDUMP("\nOSR enabled for this method\n");
                    if (compHasBackwardJump && !compTailPrefixSeen && opts.jitFlags->IsSet(JitFlags.JIT_FLAG_BBINSTR_IF_LOOPS) && opts.IsTier0)
                    {
                        assert((info.compFlags & CORINFO_FLG_DISABLE_TIER0_FOR_LOOPS) == 0);
                        opts.jitFlags->Set(JitFlags.JIT_FLAG_BBINSTR);
                        JITDUMP("\nEnabling instrumentation for this method so OSR'd version will have a profile.\n");
                    }
                }
                else
                {
                    JITDUMP($"\nOSR disabled for this method: {reason}\n");
                    assert(reason.Length != 0);
                }
            }

            if (reason.Length != 0)
            {
                fgSwitchToOptimized(reason);
            }
        }

        compSetOptimizationLevel();

#if DEBUG
        if ((JitConfig.JitInstrumentIfOptimizing != 0) && opts.OptimizationEnabled && !IsReadyToRun)
        {
            // Optionally disable by range
            s_jitInstrumentIfOptimizingRange.EnsureInit(JitConfig.JitInstrumentIfOptimizingRange);

            if (s_jitInstrumentIfOptimizingRange.Contains(impInlineRoot.info.compMethodHash()))
            {
                JITDUMP("\nEnabling instrumentation\n");
                opts.jitFlags->Set(JitFlags.JIT_FLAG_BBINSTR);
            }
        }
#endif

        if ((JitConfig.JitDisasmOnlyOptimized != 0) && (!opts.OptimizationEnabled))
        {
            // Disable JitDisasm for non-optimized code.
            opts.disAsm = false;
        }

#if COUNT_BASIC_BLOCKS
        bbCntTable.record(fgBBcount);

        if (fgBBcount == 1)
        {
            bbOneBBSizeTable.record(methodInfo->ILCodeSize);
        }
#endif

#if DEBUG
        if (verbose)
        {
            jitprintf($"Basic block list for '{info.compFullName}'\n");
            fgDispBasicBlocks();
        }

        /* Give the function a unique number */

        if (opts.disAsm || verbose)
        {
            compMethodID = ~info.compMethodHash() & 0xffff;
        }
        else
        {
            compMethodID = Interlocked.Increment(ref s_compMethodsCount);
        }
#endif

        if (compIsForInlining)
        {
            compInlineResult.NoteInt(InlineObservation.CALLEE_NUMBER_OF_BASIC_BLOCKS, fgBBcount);

            if (compInlineResult.IsFailure)
            {
                methodCodePtr = null;
                methodCodeSize = 0;
                return GetResult(this, compInlineResult);
            }

#if DEBUG
            compGenTreeID = impInlineInfo.InlinerCompiler.compGenTreeID;
            compStatementID = impInlineInfo.InlinerCompiler.compStatementID;
#endif
        }

        compCompile(out methodCodePtr, out methodCodeSize, jitFlags);

        if (compIsForInlining)
        {
            var inlinerCompiler = impInlineInfo.InlinerCompiler;

#if DEBUG
            inlinerCompiler.compGenTreeID = compGenTreeID;
            inlinerCompiler.compStatementID = compStatementID;
#endif

            inlinerCompiler.compBasicBlockID = compBasicBlockID;
        }

        return GetResult(this, compInlineResult);

        static CorJitResult GetResult(Compiler compiler, InlineResult? compInlineResult)
        {
            if (compiler.compDonotInline)
            {
                // Verify we have only one inline result in play.
                assert(compiler.impInlineInfo.inlineResult == compInlineResult);
            }

            if (!compiler.compIsForInlining)
            {
                compiler.compCompileFinish();

                // Did we just compile for a target architecture that the VM isn't expecting? If so, the VM
                // can't used the generated code (and we better be an AltJit!).

                if (!compiler.info.compMatchedVM)
                {
                    return CORJIT_SKIPPED;
                }

#if DEBUG
                if (compiler.opts.jitFlags->IsSet(JitFlags.JIT_FLAG_ALT_JIT) && (JitConfig.RunAltJitCode == 0))
                {
                    return CORJIT_SKIPPED;
                }
#endif
            }

            return CORJIT_OK;
        }
    }

    public unsafe void compDone()
    {
#if LATE_DISASM
        codeGen?.Disassembler.disDone();
#endif

        if (info.compPatchpointInfo is not null)
        {
            NativeMemory.Free(info.compPatchpointInfo);
            info.compPatchpointInfo = null;
        }
    }

    public unsafe void compFunctionTraceEnd(void* methodCodePtr, int methodCodeSize, bool isNyi)
    {
#if DEBUG
        assert(!compIsForInlining);

        if ((JitConfig.JitFunctionTrace != 0) && !opts.disDiffable)
        {
            var newJitNestingLevel = Interlocked.Decrement(ref jitNestingLevel);

            if (newJitNestingLevel < 0)
            {
                jitprintf($"{{ Illegal nesting level {newJitNestingLevel} }}\n");
            }

            for (var i = 0; i < newJitNestingLevel; i++)
            {
                jitprintf("  ");
            }

            // Note: that is incorrect if we are compiling several methods at the same time.
            var methodNumber = jitTotalMethodCompiled - 1;

            jitprintf($"}} Jitted Method {methodNumber,4} at {FMT_DBG_ADDR(methodCodePtr)} method {info.compFullName} size {methodCodeSize:x8}{(isNyi ? "NYI" : "")}{(opts.altJit ? " altjit" : "")}\n");
        }
#endif
    }

    public unsafe void compFunctionTraceStart()
    {
#if DEBUG
        if (compIsForInlining)
        {
            return;
        }

        if ((JitConfig.JitFunctionTrace != 0) && !opts.disDiffable)
        {
            var newJitNestingLevel = Interlocked.Increment(ref jitNestingLevel);

            if (newJitNestingLevel <= 0)
            {
                jitprintf($"{{ Illegal nesting level {newJitNestingLevel} }}\n");
            }

            for (var i = 0; i < newJitNestingLevel - 1; i++)
            {
                jitprintf("  ");
            }

            jitprintf($"{{ Start Jitting Method {jitTotalMethodCompiled,4} {info.compFullName} (MethodHash={info.compMethodHash():x8}) {compGetTieringName()}\n");
        }
#endif // DEBUG
    }

    public unsafe CORINFO_CONST_LOOKUP compGetHelperFtn(CorInfoHelpFunc ftnNum)
    {
        Unsafe.SkipInit<CORINFO_CONST_LOOKUP>(out var lookup);

        if (info.compMatchedVM)
        {
            _ = info.compCompHnd->getHelperFtn(ftnNum, &lookup);

            // The JIT only expects these two possible access types
            assert(lookup.accessType is IAT_VALUE or IAT_PVALUE);
        }
        else
        {
            // If we don't have a matched VM, we won't get valid results when asking for a helper function.
            lookup.addr = unchecked((void*)(0xCA11CA11)); // "callcall"
            lookup.accessType = IAT_VALUE;
        }
        return lookup;
    }

#if DEBUG
    private static ConfigMethodRange s_jitEnablePgoRange;
    private static ConfigMethodRange s_jitInlineMethodsWithEHRange;
    private static ConfigMethodRange s_jitOptRepeatRange;
#endif

    public unsafe void compInitOptions(JitFlags* jitFlags)
    {
        opts = new Options();

        if (compIsForInlining)
        {
            // The following flags are lost when inlining. (They are removed in
            // Compiler.fgInvokeInlineeCompiler().)
            assert(!jitFlags->IsSet(JitFlags.JIT_FLAG_PROF_ENTERLEAVE));
            assert(!jitFlags->IsSet(JitFlags.JIT_FLAG_DEBUG_EnC));
            assert(!jitFlags->IsSet(JitFlags.JIT_FLAG_REVERSE_PINVOKE));
            assert(!jitFlags->IsSet(JitFlags.JIT_FLAG_TRACK_TRANSITIONS));
        }

        opts.jitFlags = jitFlags;
        opts.compFlags = CLFLG_MAXOPT; // Default value is for full optimization

        if (jitFlags->IsSet(JitFlags.JIT_FLAG_DEBUG_CODE) || jitFlags->IsSet(JitFlags.JIT_FLAG_MIN_OPT) || jitFlags->IsSet(JitFlags.JIT_FLAG_TIER0))
        {
            opts.compFlags = CLFLG_MINOPT;
        }

        // Default value is to generate a blend of size and speed optimizations
        opts.compCodeOpt = BLENDED_CODE;

        if (jitFlags->IsSet(JitFlags.JIT_FLAG_SIZE_OPT) || ((info.compFlags & FLG_CCTOR) == FLG_CCTOR))
        {
            // If the EE sets SIZE_OPT or if we are compiling a Class constructor we will optimize for code size at the expense of speed
            opts.compCodeOpt = SMALL_CODE;
        }
        else if (jitFlags->IsSet(JitFlags.JIT_FLAG_SPEED_OPT) || (jitFlags->IsSet(JitFlags.JIT_FLAG_TIER1) && !jitFlags->IsSet(JitFlags.JIT_FLAG_MIN_OPT)))
        {
            // If the EE sets SPEED_OPT we will optimize for speed at the expense of code size
            opts.compCodeOpt = FAST_CODE;
            assert(!jitFlags->IsSet(JitFlags.JIT_FLAG_SIZE_OPT));
        }

        opts.compDbgCode = jitFlags->IsSet(JitFlags.JIT_FLAG_DEBUG_CODE);
        opts.compDbgInfo = jitFlags->IsSet(JitFlags.JIT_FLAG_DEBUG_INFO);
        opts.compDbgEnC = jitFlags->IsSet(JitFlags.JIT_FLAG_DEBUG_EnC);

#if DEBUG
        opts.compJitAlignLoopAdaptive = JitConfig.JitAlignLoopAdaptive == 1;
        opts.compJitAlignLoopBoundary = (ushort)(JitConfig.JitAlignLoopBoundary);
        opts.compJitAlignLoopMinBlockWeight = (ushort)(JitConfig.JitAlignLoopMinBlockWeight);
        opts.compJitAlignLoopForJcc = JitConfig.JitAlignLoopForJcc == 1;
        opts.compJitAlignLoopMaxCodeSize = (ushort)(JitConfig.JitAlignLoopMaxCodeSize);
        opts.compJitHideAlignBehindJmp = JitConfig.JitHideAlignBehindJmp == 1;
        opts.compJitOptimizeStructHiddenBuffer = JitConfig.JitOptimizeStructHiddenBuffer == 1;
        opts.compJitUnrollLoopMaxIterationCount = (ushort)(JitConfig.JitUnrollLoopMaxIterationCount);
#else
        opts.compJitAlignLoopAdaptive           = true;
        opts.compJitAlignLoopBoundary           = DEFAULT_ALIGN_LOOP_BOUNDARY;
        opts.compJitAlignLoopMinBlockWeight     = DEFAULT_ALIGN_LOOP_MIN_BLOCK_WEIGHT;
        opts.compJitAlignLoopMaxCodeSize        = DEFAULT_MAX_LOOPSIZE_FOR_ALIGN;
        opts.compJitHideAlignBehindJmp          = true;
        opts.compJitOptimizeStructHiddenBuffer  = true;
        opts.compJitUnrollLoopMaxIterationCount = DEFAULT_UNROLL_LOOP_MAX_ITERATION_COUNT;
#endif

#if TARGET_XARCH
        if (opts.compJitAlignLoopAdaptive)
        {
            // For adaptive alignment, padding limit is equal to the max instruction encoding size which == 15 bytes.
            // Hence (32 >>> 1) - 1 = 15 bytes.
            opts.compJitAlignPaddingLimit = (ushort)((opts.compJitAlignLoopBoundary >>> 1) - 1);
        }
        else
        {
            // For non-adaptive alignment, padding limit == 1 less than the alignment boundary specified.
            opts.compJitAlignPaddingLimit = (ushort)(opts.compJitAlignLoopBoundary - 1);
        }
#elif TARGET_ARM64
        if (opts.compJitAlignLoopAdaptive)
        {
            // For adaptive alignment, padding limit is same as specified by the alignment boundary because all instructions are 4 bytes long.
            // Hence (32 >>> 1) = 16 bytes.
            opts.compJitAlignPaddingLimit = (ushort)(opts.compJitAlignLoopBoundary >>> 1);
        }
        else
        {
            // For non-adaptive, padding limit is same as specified by the alignment.
            opts.compJitAlignPaddingLimit = opts.compJitAlignLoopBoundary;
        }
#endif

        assert(ushort.IsPow2(opts.compJitAlignLoopBoundary));

#if TARGET_ARM64
        // The minimum encoding size for Arm64 == 4 bytes.
        assert(opts.compJitAlignLoopBoundary >= 4);
#endif

#if REGEN_SHORTCUTS || REGEN_CALLPAT
        // We never want to have debugging enabled when regenerating GC encoding patterns
        opts.compDbgCode = false;
        opts.compDbgInfo = false;
        opts.compDbgEnC  = false;
#endif

        compSetProcessor();

#if DEBUG
        opts.dspOrder = false;

        // Optionally suppress inliner compiler instance dumping.
        if (compIsForInlining)
        {
            if (JitConfig.JitDumpInlinePhases > 0)
            {
                verbose = impInlineInfo.InlinerCompiler.verbose;
            }
            else
            {
                verbose = false;
            }
        }
        else
        {
            verbose = false;
            codeGen.Verbose = false;
        }

        verboseTrees = verbose && ShouldUseVerboseTrees;
        verboseSsa = verbose && ShouldUseVerboseSsa;
        asciiTrees = ShouldDumpAsciiTrees;
        opts.dspDiffable = compIsForInlining && impInlineInfo.InlinerCompiler.opts.dspDiffable;
#endif

        opts.altJit = false;

#if LATE_DISASM && !DEBUG
        // For non-debug builds with the late disassembler built in, we currently always do late disassembly
        // (we have no way to determine when not to, since we don't have class/method names).
        // In the DEBUG case, this is initialized to false, below.
        opts.doLateDisasm = true;
#endif

#if DEBUG
        var pfAltJit = jitFlags->IsSet(JitFlags.JIT_FLAG_AOT) ? JitConfig.AltJitNgen : JitConfig.AltJit;

        if (jitFlags->IsSet(JitFlags.JIT_FLAG_ALT_JIT))
        {
            if (pfAltJit.contains(info.compMethodHnd, info.compClassHnd, &info.compMethodInfo->args))
            {
                opts.altJit = true;
            }

            var altJitLimit = ReinterpretHexAsDecimal(JitConfig.AltJitLimit);

            if ((altJitLimit > 0) && (jitTotalMethodCompiled >= altJitLimit))
            {
                opts.altJit = false;
            }
        }
#else
        var altJitVal = (jitFlags->IsSet(JitFlags.JIT_FLAG_AOT) ? JitConfig.AltJitNgen : JitConfig.AltJit).list();

        if (jitFlags->IsSet(JitFlags.JIT_FLAG_ALT_JIT))
        {
            // In release mode, you either get all methods or no methods.
            //   * You must use "*" as the parameter, or we ignore it.
            //   * You don't get to give a regular expression of methods to match.
            // (Partially, this is because we haven't computed and stored the method and class name except in debug, and it might be expensive to do so.)
            if ((altJitVal is not null) && ((altJitVal[0] == '*') || altJitVal[1] == '\0'))
            {
                opts.altJit = true;
            }
        }
#endif

        // Take care of DOTNET_AltJitExcludeAssemblies.
        if (opts.altJit)
        {
            // First, initialize the AltJitExcludeAssemblies list, but only do it once.
            if (!s_pAltJitExcludeAssembliesListInitialized)
            {
                var wszAltJitExcludeAssemblyList = JitConfig.AltJitExcludeAssemblies;

                if (wszAltJitExcludeAssemblyList is not null)
                {
                    // NOTE: The Assembly name list is allocated in the process heap, not in the no-release heap, which is reclaimed for every compilation.
                    // This is ok because we only allocate once, due to the static.
                    s_pAltJitExcludeAssembliesList = new AssemblyNamesList2(wszAltJitExcludeAssemblyList);
                }
                s_pAltJitExcludeAssembliesListInitialized = true;
            }

            if (s_pAltJitExcludeAssembliesList is not null)
            {
                // We have an exclusion list, so see if this method is in an assembly that is on the list.
                // Note that we check this for every method, since we might inline across modules, and if the inlinee module is on the list, we don't want to use the altjit for it.
                var methodAssemblyName = eeGetClassAssemblyName(info.compClassHnd);

                if (s_pAltJitExcludeAssembliesList.IsInList(methodAssemblyName))
                {
                    opts.altJit = false;
                }
            }
        }

#if DEBUG
        // Setup assembly name list for disassembly and dump, if not already set up.
        if (!s_pJitDisasmIncludeAssembliesListInitialized)
        {
            var assemblyNameList = JitConfig.JitDisasmAssemblies;

            if (assemblyNameList is not null)
            {
                s_pJitDisasmIncludeAssembliesList = new AssemblyNamesList2(assemblyNameList);
            }
            s_pJitDisasmIncludeAssembliesListInitialized = true;
        }

        // Check for a specific set of assemblies to dump.
        // If we have an assembly name list for disassembly, also check this method's assembly.

        var assemblyInIncludeList = true; // assume we'll dump, if there's not an include list (or it's empty).

        if ((s_pJitDisasmIncludeAssembliesList is not null) && !s_pJitDisasmIncludeAssembliesList.IsEmpty)
        {
            var assemblyName = eeGetClassAssemblyName(info.compClassHnd);

            if (!s_pJitDisasmIncludeAssembliesList.IsInList(assemblyName))
            {
                // We have a list, and the current assembly is not in it, so we won't dump.
                assemblyInIncludeList = false;
            }
        }

        var altJitConfig = !pfAltJit.isEmpty();
        var verboseDump = true;

        if (!altJitConfig || opts.altJit)
        {
            // We should only enable 'verboseDump' when we are actually compiling a matching method
            // and not enable it when we are just considering inlining a matching method.
            //
            if (!compIsForInlining)
            {
                var jitDump = JitConfig.JitDump;

                if (jitDump.contains(info.compMethodHnd, info.compClassHnd, &info.compMethodInfo->args))
                {
                    verboseDump = true;
                }

                var jitHashDumpVal = JitConfig.JitHashDump;

                if ((jitHashDumpVal is not -1) && (jitHashDumpVal == info.compMethodHash()))
                {
                    verboseDump = true;
                }
            }
        }

        // Optionally suppress dumping if not in specified list of included assemblies.
        if (verboseDump && !assemblyInIncludeList)
        {
            verboseDump = false;
        }

        // Optionally suppress dumping Tier0 jit requests.
        if (verboseDump && jitFlags->IsSet(JitFlags.JIT_FLAG_TIER0))
        {
            verboseDump = JitConfig.JitDumpTier0 > 0;
        }

        // Optionally suppress dumping OSR jit requests.
        if (verboseDump && jitFlags->IsSet(JitFlags.JIT_FLAG_OSR))
        {
            verboseDump = (JitConfig.JitDumpOSR > 0);
        }

        // Optionally suppress dumping except for a specific OSR jit request.
        var dumpAtOsrOffset = JitConfig.JitDumpAtOSROffset;

        if (verboseDump && (dumpAtOsrOffset is not -1))
        {
            if (jitFlags->IsSet(JitFlags.JIT_FLAG_OSR))
            {
                verboseDump = (dumpAtOsrOffset == info.compILEntry);
            }
            else
            {
                verboseDump = false;
            }
        }

        if (verboseDump)
        {
            verbose = true;
        }
#endif

#if FEATURE_SIMD
        assert(_usesSimdTypes == false);
#endif

        lvaEnregEHVars = compEnregLocals && (JitConfig.EnableEHWriteThru != 0);
        lvaEnregMultiRegVars = compEnregLocals && (JitConfig.EnableMultiRegLocals != 0);

#if FEATURE_TAILCALL_OPT
        // By default opportunistic tail call optimization is enabled.
        // Recognition is done in the importer so this must be set for inlinees as well.
        opts.compTailCallOpt = true;
#endif

#if FEATURE_FASTTAILCALL
        // By default fast tail calls are enabled.
        opts.compFastTailCalls = true;
#endif

        // Profile data
        fgPgoQueryResult = E_FAIL;

        assert(fgPgoSchema is null);
        assert(fgPgoData is null);
        assert(fgPgoSchemaCount == 0);
        assert(fgPgoFailReason is null);
        assert(fgPgoSource is PgoSource.Unknown);
        assert(fgPgoHaveWeights is false);
        assert(fgPgoSynthesized is false);
        assert(fgPgoConsistent is false);
        assert(fgPgoDynamic is false);

        if (jitFlags->IsSet(JitFlags.JIT_FLAG_BBOPT))
        {
            fixed (PgoInstrumentationSchema** pSchema = &fgPgoSchema)
            fixed (int* pCountSchemaItems = &fgPgoSchemaCount)
            fixed (byte** pInstrumentationData = &fgPgoData)
            fixed (PgoSource* pPgoSource = &fgPgoSource)
            fixed (bool* pDynamicPgo = &fgPgoDynamic)
            {
                fgPgoQueryResult = info.compCompHnd->getPgoInstrumentationResults(info.compMethodHnd, pSchema, pCountSchemaItems, pInstrumentationData, pPgoSource, pDynamicPgo);
            }

            if (FAILED(fgPgoQueryResult))
            {
                // a failed result that also has a non-null fgPgoSchema indicates that the ILSize for the method no longer matches the ILSize for the method when profile data was collected.
                // We will discard the IBC data in this case

                fgPgoFailReason = (fgPgoSchema is not null) ? "No matching PGO data" : "No PGO data";
                fgPgoData = null;
                fgPgoSchema = null;
            }
            else if (JitConfig.JitDisablePGO > 0)
            {
                // Optionally, disable use of profile data.
                fgPgoFailReason = "PGO data available, but JitDisablePGO > 0";
                fgPgoQueryResult = E_FAIL;
                fgPgoData = null;
                fgPgoSchema = null;
                fgPgoDisabled = true;
                fgPgoDynamic = false;
            }
#if DEBUG
            else
            {
                // Optionally, enable use of profile data for only some methods.
                s_jitEnablePgoRange.EnsureInit(JitConfig.JitEnablePGORange);

                // Base this decision on the root method hash, so a method either sees all available profile data (including that for inlinees), or none of it.
                if (!s_jitEnablePgoRange.Contains(impInlineRoot.info.compMethodHash()))
                {
                    fgPgoFailReason = "PGO data available, but method hash NOT within JitEnablePGORange";
                    fgPgoQueryResult = E_FAIL;
                    fgPgoData = null;
                    fgPgoSchema = null;
                    fgPgoDisabled = true;
                }
            }
#endif

            // A successful result implies a non-null fgPgoSchema
            if (SUCCEEDED(fgPgoQueryResult))
            {
                assert(fgPgoSchema is not null);

                for (var i = 0; i < fgPgoSchemaCount; i++)
                {
                    var kind = fgPgoSchema[i].InstrumentationKind;

                    if (kind is PgoInstrumentationKind.BasicBlockIntCount
                             or PgoInstrumentationKind.BasicBlockLongCount
                             or PgoInstrumentationKind.EdgeIntCount
                             or PgoInstrumentationKind.EdgeLongCount)
                    {
                        fgPgoHaveWeights = true;
                        break;
                    }
                }

                // Stash pointers to PGO info on the context so we can access it contextually later.
                assert(compInlineContext is not null);
                compInlineContext.PgoInfo = new PgoInfo(this);
            }

            // A failed result implies a null fgPgoSchema
            //   see implementation of Compiler.fgHaveProfileData()
            if (FAILED(fgPgoQueryResult))
            {
                assert(fgPgoSchema is null);
            }
        }

        var enableInliningMethodsWithEH = JitConfig.JitInlineMethodsWithEH > 0;

#if DEBUG
        s_jitInlineMethodsWithEHRange.EnsureInit(JitConfig.JitInlineMethodsWithEHRange);

        var inRange = s_jitInlineMethodsWithEHRange.Contains(impInlineRoot.info.compMethodHash());
        enableInliningMethodsWithEH &= inRange;
#endif

        opts.compInlineMethodsWithEH = enableInliningMethodsWithEH;

        if (compIsForInlining)
        {
            return;
        }

        // The rest of the opts fields that we initialize here should only be used when we generate code for the method
        // They should not be used when importing or inlining

#if FEATURE_TAILCALL_OPT
        opts.compTailCallLoopOpt = true;
#endif

        opts.genFPorder = true;
        opts.genFPopt = true;

        assert(opts.instrCount == 0);
        assert(opts.callInstrCount == 0);
        assert(opts.lvRefCount == 0);

#if PROFILING_SUPPORTED
        assert(opts.compJitELTHookEnabled is false);
#endif

#if TARGET_ARM64
        // 0 is default: use the appropriate frame type based on the function.
        assert(opts.compJitSaveFpLrWithCalleeSavedRegisters == 0);
#endif

        assert(opts.disAsm is false);
        assert(opts.disDiffable is false);
        assert(opts.dspDiffable is false);
        assert(opts.disAlignment is false);
        assert(opts.disCodeBytes is false);

        opts.optRepeatCount = 1;
        assert(opts.optRepeat is false);
        assert(opts.optRepeatIteration == 0);
        assert(opts.optRepeatActive is false);

#if DEBUG
        assert(opts.dspInstrs is false);
        assert(opts.dspLines is false);
        assert(opts.varNames is false);
        assert(opts.disAsmSpilled is false);
        assert(opts.disAddr is false);
        assert(opts.dspCode is false);
        assert(opts.dspEHTable is false);
        assert(opts.dspDebugInfo is false);
        assert(opts.dspGCtbls is false);
        assert(opts.dspMetrics is false);
        assert(opts.disAsm2 is false);
        assert(opts.dspUnwind is false);
        assert(opts.compLongAddress is false);

#if LATE_DISASM
        assert(opts.doLateDisasm is false);
#endif

        assert(compDebugBreak is false);

        //  If we have a non-empty AltJit config then we change all of these other config values to refer only to the AltJit.
        if (!altJitConfig || opts.altJit)
        {
            var disEnabled = true;

            // Optionally suppress dumping if not in specified list of included assemblies.
            if (!assemblyInIncludeList)
            {
                disEnabled = false;
            }

            if (disEnabled)
            {
                if ((JitConfig.JitOrder & 1) == 1)
                {
                    opts.dspOrder = true;
                }

                if (JitConfig.JitGCDump.contains(info.compMethodHnd, info.compClassHnd, &info.compMethodInfo->args))
                {
                    opts.dspGCtbls = true;
                }

                if (JitConfig.JitDisasm.contains(info.compMethodHnd, info.compClassHnd, &info.compMethodInfo->args))
                {
                    opts.disAsm = true;
                }

                if (JitConfig.JitDisasmSpilled != 0)
                {
                    opts.disAsmSpilled = true;
                }

                if (JitConfig.JitUnwindDump.contains(info.compMethodHnd, info.compClassHnd, &info.compMethodInfo->args))
                {
                    opts.dspUnwind = true;
                }

                if (JitConfig.JitEHDump.contains(info.compMethodHnd, info.compClassHnd, &info.compMethodInfo->args))
                {
                    opts.dspEHTable = true;
                }

                if (JitConfig.JitDebugDump.contains(info.compMethodHnd, info.compClassHnd, &info.compMethodInfo->args))
                {
                    opts.dspDebugInfo = true;
                }
            }

            if (opts.disAsm && (JitConfig.JitDisasmWithGC != 0))
            {
                opts.disasmWithGC = true;
            }

#if LATE_DISASM
            if (JitConfig.JitLateDisasm.contains(info.compMethodHnd, info.compClassHnd, &info.compMethodInfo->args))
            {
                opts.doLateDisasm = true;
            }
#endif // LATE_DISASM

            if (JitConfig.JitDisasmWithAddress != 0)
            {
                opts.disAddr = true;
            }

            if (JitConfig.JitLongAddress != 0)
            {
                opts.compLongAddress = true;
            }

            if ((JitConfig.JitEnableOptRepeat != 0) &&
                (JitConfig.JitOptRepeat.contains(info.compMethodHnd, info.compClassHnd, &info.compMethodInfo->args)))
            {
                opts.optRepeat = true;
                opts.optRepeatCount = JitConfig.JitOptRepeatCount;
            }

            opts.dspMetrics = (JitConfig.JitMetrics != 0);
        }

        if (verboseDump)
        {
            opts.dspCode = true;
            opts.dspEHTable = true;
            opts.dspGCtbls = true;
            opts.disAsm2 = true;
            opts.dspUnwind = true;
            verbose = true;
            verboseTrees = ShouldUseVerboseTrees;
            verboseSsa = ShouldUseVerboseSsa;
            codeGen.Verbose = true;
        }

        treesBeforeAfterMorph = (JitConfig.JitDumpBeforeAfterMorph == 1);
        morphNum = 0; // Initialize the morphed-trees counting.

        expensiveDebugCheckLevel = JitConfig.JitExpensiveDebugCheckLevel;

        if (expensiveDebugCheckLevel == 0)
        {
            // If we're in a stress mode that modifies the flowgraph, make 1 the default.
            if (fgStressBBProf() || compStressCompile(STRESS_DO_WHILE_LOOPS, 30))
            {
                expensiveDebugCheckLevel = 1;
            }
        }

        if (verbose)
        {
            jitprintf($"****** START compiling {info.compFullName} (MethodHash={info.compMethodHash():x8})\n");
            jitprintf($"Generating code for {Target.TgtPlatformName} {Target.TgtCpuName}\n");
            jitprintf(""); // in our logic this causes a flush
        }

        if (JitConfig.JitBreak.contains(info.compMethodHnd, info.compClassHnd, &info.compMethodInfo->args))
        {
            NO_WAY("JitBreak reached");
        }

        var jitHashBreakVal = JitConfig.JitHashBreak;

        if ((jitHashBreakVal is not -1) && (jitHashBreakVal == info.compMethodHash()))
        {
            NO_WAY("JitHashBreak reached");
        }

        if (verbose ||
            JitConfig.JitDebugBreak.contains(info.compMethodHnd, info.compClassHnd, &info.compMethodInfo->args) ||
            JitConfig.JitBreak.contains(info.compMethodHnd, info.compClassHnd, &info.compMethodInfo->args))
        {
            compDebugBreak = true;
        }

        assert(!((ReadOnlySpan<byte>)(compActiveStressModes)).ContainsAnyExcept((byte)(0)));

        // Read function list, if not already read, and there exists such a list.
        if (!s_pJitFunctionFileInitialized)
        {
            var functionFileName = JitConfig.JitFunctionFile;

            if (functionFileName is not null)
            {
                s_pJitMethodSet = new MethodSet2(functionFileName);
            }
            s_pJitFunctionFileInitialized = true;
        }
#else
        if (JitConfig.JitDisasm.contains(info.compMethodHnd, info.compClassHnd, &info.compMethodInfo->args))
        {
            opts.disAsm = true;
        }

        if ((JitConfig.JitEnableOptRepeat != 0) &&
            (JitConfig.JitOptRepeat.contains(info.compMethodHnd, info.compClassHnd, &info.compMethodInfo->args)))
        {
            opts.optRepeat      = true;
            opts.optRepeatCount = JitConfig.JitOptRepeatCount;
        }
#endif

#if !DEBUG
        if (opts.disAsm)
#endif
        {
            if (JitConfig.JitDisasmTesting != 0)
            {
                opts.disTesting = true;
            }

            if (JitConfig.JitDisasmWithAlignmentBoundaries != 0)
            {
                opts.disAlignment = true;
            }

            if (JitConfig.JitDisasmWithCodeBytes != 0)
            {
                opts.disCodeBytes = true;
            }

            if (JitConfig.JitDisasmDiffable != 0)
            {
                opts.disDiffable = true;
                opts.dspDiffable = true;
            }
        }

        if (opts.optRepeat)
        {
            // Defer printing this until now, after the "START" line printed above.
            JITDUMP($"\n*************** JitOptRepeat enabled; repetition count: {opts.optRepeatCount}\n\n");
        }
        else if (JitConfig.JitEnableOptRepeat != 0)
        {
#if DEBUG
            // Opt-in to JitOptRepeat based on method hash ranges.
            // The default is no JitOptRepeat.
            s_jitOptRepeatRange.EnsureInit(JitConfig.JitOptRepeatRange);
            assert(!s_jitOptRepeatRange.Error);

            if (!s_jitOptRepeatRange.IsEmpty && s_jitOptRepeatRange.Contains(info.compMethodHash()))
            {
                opts.optRepeat = true;
                opts.optRepeatCount = JitConfig.JitOptRepeatCount;

                JITDUMP($"\n*************** JitOptRepeat enabled by JitOptRepeatRange; repetition count: {opts.optRepeatCount}\n\n");
            }

            if (!opts.optRepeat && compStressCompile(STRESS_OPT_REPEAT, 10))
            {
                // Turn on optRepeat as part of JitStress.
                // In this case, decide how many iterations to do, from 2 to 5, based on a random number seeded by the method hash.

                opts.optRepeat = true;
                opts.optRepeatCount = new Random(info.compMethodHash()).Next(4) + 2; // generates [2..5]

                JITDUMP($"\n*************** JitOptRepeat for stress; repetition count: {opts.optRepeatCount}\n\n");
            }
#endif
        }

#if DEBUG
        assert(!codeGen.IsGcTypeFixed);
        opts.compGcChecks = (JitConfig.JitGCChecks != 0) || compStressCompile(STRESS_GENERIC_VARN, 5);
#endif

#if DEBUG && TARGET_XARCH
        const int STACK_CHECK_ON_RETURN = 0x1;
        // const int STACK_CHECK_ON_CALL = 0x2;
        const int STACK_CHECK_ALL = 0x3;

        var dwJitStackChecks = JitConfig.JitStackChecks;

        if (compStressCompile(STRESS_GENERIC_VARN, 5))
        {
            dwJitStackChecks = STACK_CHECK_ALL;
        }
        opts.compStackCheckOnRet = (dwJitStackChecks & STACK_CHECK_ON_RETURN) != 0;

#if TARGET_X86
        opts.compStackCheckOnCall = (dwJitStackChecks & STACK_CHECK_ON_CALL) != 0;
#endif
#endif

#if MEASURE_MEM_ALLOC
        s_dspMemStats = JitConfig.DisplayMemStats != 0;
#endif

#if PROFILING_SUPPORTED
        opts.compNoPInvokeInlineCB = jitFlags->IsSet(JitFlags.JIT_FLAG_PROF_NO_PINVOKE_INLINE);

        // Cache the profiler handle
        if (jitFlags->IsSet(JitFlags.JIT_FLAG_PROF_ENTERLEAVE))
        {
            bool hookNeeded;
            bool indirected;

            fixed (void** pProfilerHandle = &compProfilerMethHnd)
            {
                info.compCompHnd->GetProfilingHandle(&hookNeeded, pProfilerHandle, &indirected);
            }

            compProfilerHookNeeded = hookNeeded;
            compProfilerMethHndIndirected = indirected;
        }
        else
        {
            assert(compProfilerHookNeeded is false);
            assert(compProfilerMethHnd is null);
            assert(compProfilerMethHndIndirected is false);
        }

        // Honour DOTNET_JitELTHookEnabled or STRESS_PROFILER_CALLBACKS stress mode
        // only if VM has not asked us to generate profiler hooks in the first place.
        // That is, override VM only if it hasn't asked for a profiler callback for this method.
        // Don't run this stress mode under AOT, as we would need to emit a relocation
        // for the call to the fake ELT hook, which wouldn't make sense, as we can't store that
        // in the AOT image.
        if (!compProfilerHookNeeded)
        {
            if ((JitConfig.JitELTHookEnabled != 0) ||
                (!jitFlags->IsSet(JitFlags.JIT_FLAG_AOT) && compStressCompile(STRESS_PROFILER_CALLBACKS, 5)))
            {
                opts.compJitELTHookEnabled = true;
            }
        }

        // TBD: Exclude PInvoke stubs
        if (opts.compJitELTHookEnabled)
        {
#if DEBUG
            // We currently only know if we're running under SuperPMI in DEBUG
            // We don't want to get spurious SuperPMI asm diffs because profile stress kicks in and we use the address of `DummyProfilerELTStub` in the JIT binary, without relocation.
            // So just use a fixed address in this case; It's SuperPMI replay, so the generated code won't be run.
            if (RunningSuperPmiReplay)
            {
#if HOST_64BIT
                assert(sizeof(void*) == 8);
                compProfilerMethHnd = (void*)(0x0BADF00DBEADCAFE);
#else
                assert(sizeof(void*) == 4);
                compProfilerMethHnd = (void*)(0x0BADF00D);
#endif
            }
            else
#endif
            {
#if TARGET_AMD64
                compProfilerMethHnd = (delegate*<nint, nint, void>)(&DummyProfilerELTStub);
#else
                compProfilerMethHnd = (delegate*<nint, void>)(&DummyProfilerELTStub);
#endif
            }
            compProfilerMethHndIndirected = false;
        }

#endif

#if FEATURE_TAILCALL_OPT
        var pStrTailCallOpt = JitConfig.TailCallOpt;

        if (pStrTailCallOpt is not null)
        {
            var strTailCallOpt = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(pStrTailCallOpt);
            opts.compTailCallOpt = int.TryParse(strTailCallOpt, out var numTailCallOpt) && (numTailCallOpt != 0);
        }

        if (JitConfig.TailCallLoopOpt == 0)
        {
            opts.compTailCallLoopOpt = false;
        }
#endif

#if FEATURE_FASTTAILCALL
        if (JitConfig.FastTailCalls == 0)
        {
            opts.compFastTailCalls = false;
        }
#endif

#if CONFIGURABLE_ARM_ABI
        opts.compUseSoftFP = jitFlags->IsSet(JitFlags.JIT_FLAG_SOFTFP_ABI);

        var softFPConfig = opts.compUseSoftFP ? 2 : 1;
        var oldSoftFPConfig = Interlocked.CompareExchange(ref GlobalJitOptions.compUseSoftFPConfigured, softFPConfig, 0);

        if ((oldSoftFPConfig != softFPConfig) && (oldSoftFPConfig != 0))
        {
            // There are no current scenarios where the abi can change during the lifetime of a process
            // that uses the JIT. If such a change occurs, either compFeatureHfa will need to change to a TLS static
            // or we will need to have some means to reset the flag safely.
            NO_WAY("SoftFP ABI setting changed during lifetime of process");
        }

        GlobalJitOptions.compFeatureHfa = !opts.compUseSoftFP;
#elif ARM_SOFTFP && TARGET_ARM
        // Armel is unconditionally enabled in the JIT. Verify that the VM side agrees.
        assert(jitFlags->IsSet(JitFlags.JIT_FLAG_SOFTFP_ABI));
#elif TARGET_ARM
        assert(!jitFlags->IsSet(JitFlags.JIT_FLAG_SOFTFP_ABI));
#endif

        opts.compScopeInfo = opts.compDbgInfo;

#if LATE_DISASM
        codeGen.Disassembler.disOpenForLateDisAsm(info.compMethodName, info.compClassName, info.compMethodInfo->args.pSig);
#endif

        opts.compReloc = jitFlags->IsSet(JitFlags.JIT_FLAG_RELOC);

        var enableFakeSplitting = false;

#if DEBUG
        enableFakeSplitting = JitConfig.JitFakeProcedureSplitting != 0;

#if TARGET_XARCH || TARGET_RISCV64
        // Whether encoding of absolute addr as PC-rel offset is enabled
        opts.compEnablePCRelAddr = JitConfig.EnablePCRelAddr != 0;
#endif
#endif

        opts.compProcedureSplitting = jitFlags->IsSet(JitFlags.JIT_FLAG_PROCSPLIT) || enableFakeSplitting;

#if FEATURE_CFI_SUPPORT
        // Hot/cold splitting is not being tested on NativeAOT.
        if (generateCFIUnwindCodes())
        {
            opts.compProcedureSplitting = false;
        }
#endif

#if TARGET_LOONGARCH64 || TARGET_RISCV64
        opts.compProcedureSplitting = false;
#endif

#if DEBUG
        opts.compProcedureSplittingEH = opts.compProcedureSplitting;
#endif

        if (opts.compProcedureSplitting)
        {
            // Note that opts.compDbgCode is true under AOT for checked assemblies!
            opts.compProcedureSplitting = !opts.compDbgCode || enableFakeSplitting;

#if DEBUG
            // JitForceProcedureSplitting is used to force procedure splitting on checked assemblies.
            // This is useful for debugging on a checked build.
            // Note that we still only do procedure splitting in the zapper.
            if (JitConfig.JitForceProcedureSplitting.contains(info.compMethodHnd, info.compClassHnd, &info.compMethodInfo->args))
            {
                opts.compProcedureSplitting = true;
            }

            // JitNoProcedureSplitting will always disable procedure splitting.
            if (JitConfig.JitNoProcedureSplitting.contains(info.compMethodHnd, info.compClassHnd, &info.compMethodInfo->args))
            {
                opts.compProcedureSplitting = false;
            }

            // JitNoProcedureSplittingEH will disable procedure splitting in functions with EH.
            if (JitConfig.JitNoProcedureSplittingEH.contains(info.compMethodHnd, info.compClassHnd, &info.compMethodInfo->args))
            {
                opts.compProcedureSplittingEH = false;
            }
#endif
        }

#if TARGET_64BIT
        opts.compCollect64BitCounts = JitConfig.JitCollect64BitCounts != 0;

#if DEBUG
        if (JitConfig.JitRandomlyCollect64BitCounts != 0)
        {
            opts.compCollect64BitCounts = new Random(info.compMethodHash() ^ JitConfig.JitRandomlyCollect64BitCounts ^ 0x3485E20E).Next(2) == 0;
        }
#endif
#else
        opts.compCollect64BitCounts = false;
#endif

#if DEBUG
        // Now, set compMaxUncheckedOffsetForNullObject for STRESS_NULL_OBJECT_CHECK
        if (compStressCompile(STRESS_NULL_OBJECT_CHECK, 30))
        {
            compMaxUncheckedOffsetForNullObject = JitConfig.JitMaxUncheckedOffset;

            if (verbose)
            {
                jitprintf($"STRESS_NULL_OBJECT_CHECK: compMaxUncheckedOffsetForNullObject=0x{compMaxUncheckedOffsetForNullObject:X}\n");
            }
        }

        if (verbose)
        {
            // If we are compiling for a specific tier, make that very obvious in the output.
            // Note that we don't expect multiple TIER flags to be set at one time, but there
            // is nothing preventing that.
            if (jitFlags->IsSet(JitFlags.JIT_FLAG_TIER0))
            {
                jitprintf("OPTIONS: Tier-0 compilation (set DOTNET_TieredCompilation=0 to disable)\n");
            }

            if (jitFlags->IsSet(JitFlags.JIT_FLAG_TIER1))
            {
                jitprintf("OPTIONS: Tier-1 compilation\n");
            }

            if (compSwitchedToOptimized)
            {
                jitprintf("OPTIONS: Tier-0 compilation, switched to FullOpts\n");
            }

            if (compSwitchedToMinOpts)
            {
                jitprintf("OPTIONS: Tier-1/FullOpts compilation, switched to MinOpts\n");
            }

            if (jitFlags->IsSet(JitFlags.JIT_FLAG_OSR))
            {
                jitprintf($"OPTIONS: OSR variant with entry point 0x{info.compILEntry:x}\n");
            }

            jitprintf($"OPTIONS: compCodeOpt = {(opts.compCodeOpt is BLENDED_CODE ? "BLENDED_CODE"
                                                 : opts.compCodeOpt is SMALL_CODE ? "SMALL_CODE"
                                                  : opts.compCodeOpt is FAST_CODE ? "FAST_CODE"
                                                                                  : "UNKNOWN_CODE")}\n");

            jitprintf($"OPTIONS: compDbgCode = {dspBool(opts.compDbgCode)}\n");
            jitprintf($"OPTIONS: compDbgInfo = {dspBool(opts.compDbgInfo)}\n");
            jitprintf($"OPTIONS: compDbgEnC  = {dspBool(opts.compDbgEnC)}\n");
            jitprintf($"OPTIONS: compProcedureSplitting   = {dspBool(opts.compProcedureSplitting)}\n");
            jitprintf($"OPTIONS: compProcedureSplittingEH = {dspBool(opts.compProcedureSplittingEH)}\n");

            // This is rare; don't clutter up the dump with it normally.

#if PROFILING_SUPPORTED
            if (compProfilerHookNeeded)
            {
                jitprintf($"OPTIONS: compProfilerHookNeeded   = {dspBool(compProfilerHookNeeded)}\n");
            }
#endif

            if (jitFlags->IsSet(JitFlags.JIT_FLAG_BBOPT))
            {
                jitprintf("OPTIONS: optimizer should use profile data\n");
            }

            if (jitFlags->IsSet(JitFlags.JIT_FLAG_AOT))
            {
                jitprintf("OPTIONS: Jit invoked for AOT\n");
            }

            if (compIsAsync)
            {
                jitprintf("OPTIONS: compilation is an async state machine\n");
            }
        }
#endif

#if PROFILING_SUPPORTED && UNIX_AMD64_ABI
        if (compIsProfilerHookNeeded())
        {
            opts.compNeedToAlignFrame = true;
        }
#endif

#if DEBUG && TARGET_ARM64
        if ((s_pJitMethodSet is null) || s_pJitMethodSet->IsActiveMethod(info.compFullName, info.compMethodHash()))
        {
            opts.compJitSaveFpLrWithCalleeSavedRegisters = JitConfig.JitSaveFpLrWithCalleeSavedRegisters();
        }
#endif

#if TARGET_AMD64
        srbmAllFloat = SRBM_ALLFLOAT_INIT;
        srbmFltCalleeTrash = SRBM_FLT_CALLEE_TRASH_INIT;
        cntCalleeTrashFloat = CNT_CALLEE_TRASH_FLOAT_INIT;

        srbmAllInt = SRBM_ALLINT_INIT;
        srbmIntCalleeTrash = SRBM_INT_CALLEE_TRASH_INIT;
        cntCalleeTrashInt = CNT_CALLEE_TRASH_INT_INIT;
        regIntLast = REG_R15;

        if (canUseEvexEncoding())
        {
            srbmAllFloat |= SRBM_HIGHFLOAT;
            srbmFltCalleeTrash |= SRBM_HIGHFLOAT;
            cntCalleeTrashFloat += CNT_CALLEE_TRASH_HIGHFLOAT;
        }

        if (canUseApxEncoding())
        {
            srbmAllInt |= SRBM_HIGHINT;
            srbmIntCalleeTrash |= SRBM_HIGHINT;
            cntCalleeTrashInt += CNT_CALLEE_TRASH_HIGHINT;
            regIntLast = REG_R31;
        }
#endif

#if TARGET_XARCH
        srbmAllMask = SRBM_ALLMASK_INIT;
        srbmMskCalleeTrash = SRBM_MSK_CALLEE_TRASH_INIT;
        cntCalleeTrashMask = CNT_CALLEE_TRASH_MASK_INIT;

        if (canUseEvexEncoding())
        {
            srbmAllMask |= SRBM_ALLMASK_EVEX;
            srbmMskCalleeTrash |= SRBM_MSK_CALLEE_TRASH_EVEX;
            cntCalleeTrashMask += CNT_CALLEE_TRASH_MASK_EVEX;
        }

        // Make sure we copy the register info and initialize the trash regs after the underlying fields are initialized
        compInitVarTypeCalleeTrashRegMasks();

        codeGen.CopyRegisterInfo();
#endif
    }

    public void compInitScopeLists()
    {
        var varScopesCount = info.compVarScopesCount;

        if (varScopesCount is 0)
        {
            return;
        }

        // Populate the 'compEnterScopeList' and 'compExitScopeList' lists
        compEnterScopeIndices = new int[varScopesCount];
        compExitScopeIndices = new int[varScopesCount];

        for (var i = 0; i < varScopesCount; i++)
        {
            compExitScopeIndices[i] = i;
            compEnterScopeIndices[i] = i;
        }

        compEnterScopeIndices.AsSpan().Sort((left, right) => compEnterScopeList(left).vsdLifeBeg.CompareTo(compEnterScopeList(right).vsdLifeBeg));
        compExitScopeIndices.AsSpan().Sort((left, right) => compExitScopeList(left).vsdLifeEnd.CompareTo(compExitScopeList(right).vsdLifeEnd));
    }

    /// <summary>Create a scope map so it can be looked up by varNum</summary>
    public void compInitVarScopeMap()
    {
        // Description:
        //    Map.K => Map.V . varNum => List(ScopeDsc)
        //
        //    Create a scope map that can be indexed by varNum and can be iterated
        //    on it's values to look for matching scope when given an offs or
        //    lifeBeg and lifeEnd.
        //
        // Notes:
        //    1. Build the map only when we think linear search is slow, i.e.,
        //    MAX_LINEAR_FIND_LCL_SCOPELIST is large.
        //    2. Linked list preserves original array order.

        var varScopesCount = info.compVarScopesCount;

        if (varScopesCount < MAX_LINEAR_FIND_LCL_SCOPELIST)
        {
            return;
        }

        assert(compVarScopeMap is null);
        compVarScopeMap = [];

        // 599 prime to limit huge allocations; for ex: duplicated scopes on single var.
        _ = compVarScopeMap.EnsureCapacity(int.Min(varScopesCount, 599));

        var varScopes = info.compVarScopes.AsSpan(0, varScopesCount);

        for (var i = 0; i < varScopes.Length; i++)
        {
            ref var varScope = ref varScopes[i];
            var varNum = varScope.vsdVarNum;

            var node = new VarScopeListNode(i);

            // Index by varNum and if the list exists append "node" to the "list".
            ref var info = ref CollectionsMarshal.GetValueRefOrAddDefault(compVarScopeMap, varNum, out var exists);

            if (exists)
            {
                info.Tail.Next = node;
                info.Tail = node;
            }
            else
            {
                info = new VarScopeMapInfo(node);
            }
        }
    }

    public void compJitStats()
    {
        // TODO: Port Compiler.compJitStats
    }

    public ref VarScopeDsc compGetNextEnterScope(int offs, bool scan = false)
    {
        assert(info.compVarScopesCount is not 0);

        if (compNextEnterScopeIndex < info.compVarScopesCount)
        {
            ref var nextEnterScope = ref compEnterScopeList(compNextEnterScopeIndex);
            assert(!Unsafe.IsNullRef(in nextEnterScope));

            var nextEnterOff = nextEnterScope.vsdLifeBeg;
            assert(scan || (offs <= nextEnterOff));

            if (!scan)
            {
                if (offs == nextEnterOff)
                {
                    compNextEnterScopeIndex++;
                    return ref nextEnterScope;
                }
            }
            else
            {
                if (nextEnterOff <= offs)
                {
                    compNextEnterScopeIndex++;
                    return ref nextEnterScope;
                }
            }
        }
        return ref Unsafe.NullRef<VarScopeDsc>();
    }

    public ref VarScopeDsc compGetNextExitScope(int offs, bool scan = false)
    {
        assert(info.compVarScopesCount is not 0);

        if (compNextExitScopeIndex < info.compVarScopesCount)
        {
            ref var nextExitScope = ref compExitScopeList(compNextExitScopeIndex);
            assert(!Unsafe.IsNullRef(in nextExitScope));

            var nextExitOffs = nextExitScope.vsdLifeEnd;
            assert(scan || (offs <= nextExitOffs));

            if (!scan)
            {
                if (offs == nextExitOffs)
                {
                    compNextExitScopeIndex++;
                    return ref nextExitScope;
                }
            }
            else
            {
                if (nextExitOffs <= offs)
                {
                    compNextExitScopeIndex++;
                    return ref nextExitScope;
                }
            }
        }
        return ref Unsafe.NullRef<VarScopeDsc>();
    }

    /// <summary>get a string describing tiered compilation settings for this method</summary>
    /// <param name="wantShortName">true if a short name is ok (say for using in file names)</param>
    /// <returns>String describing tiering decisions for this method, including cases where the jit codegen will differ from what the runtime requested.</returns>
    public unsafe string compGetTieringName(bool wantShortName = false)
    {
        var tier0 = opts.jitFlags->IsSet(JitFlags.JIT_FLAG_TIER0);
        var tier1 = opts.jitFlags->IsSet(JitFlags.JIT_FLAG_TIER1);
        var instrumenting = opts.jitFlags->IsSet(JitFlags.JIT_FLAG_BBINSTR);

        if (!opts.compMinOptsIsSet)
        {
            // If 'compMinOptsIsSet' is not set, just return here. Otherwise, if this method is called
            // by the assertAbort(), we would recursively call assert while trying to get MinOpts()
            // and eventually stackoverflow.
            return "Optimization-Level-Not-Yet-Set";
        }

        assert(!tier0 || !tier1); // We don't expect multiple TIER flags to be set at one time.

        if (tier0)
        {
            return instrumenting ? "Instrumented Tier0" : "Tier0";
        }
        else if (tier1)
        {
            if (opts.IsOSR)
            {
                return instrumenting ? "Instrumented Tier1-OSR" : "Tier1-OSR";
            }
            else
            {
                return instrumenting ? "Instrumented Tier1" : "Tier1";
            }
        }
        else if (opts.OptimizationEnabled)
        {
            if (compSwitchedToOptimized)
            {
                return wantShortName ? "Tier0-FullOpts" : "Tier-0 switched to FullOpts";
            }
            else
            {
                return "FullOpts";
            }
        }
        else if (opts.MinOpts)
        {
            if (compSwitchedToMinOpts)
            {
                if (compSwitchedToOptimized)
                {
                    return wantShortName ? "Tier0-FullOpts-MinOpts" : "Tier-0 switched to FullOpts, then to MinOpts";
                }
                else
                {
                    return wantShortName ? "Tier0-MinOpts" : "Tier-0 switched MinOpts";
                }
            }
            else
            {
                return "MinOpts";
            }
        }
        else if (opts.compDbgCode)
        {
            return "Debug";
        }
        else
        {
            return wantShortName ? "Unknown" : "Unknown optimization level";
        }
    }

    public string compRegVarName(regNumber reg, bool displayVar = false, bool isFloatReg = false)
    {
#if TARGET_ARM
        isFloatReg = genIsValidFloatReg(reg);
#endif

#if DEBUG
        if (displayVar && (reg != REG_NA))
        {
            var varName = compVarName(reg, isFloatReg);

            if (varName is not null)
            {
                return $"{reg.Name}'{varName}'";
            }
        }
#endif

        // no debug info required or no variable in that register -> return standard name
        return reg.Name;
    }

    public unsafe void compResetScopeLists()
    {
        if (info.compVarScopesCount is 0)
        {
            return;
        }

        compNextExitScopeIndex = 0;
        compNextEnterScopeIndex = 0;
    }

#if DEBUG
    public bool compInlineStress() => compStressCompile(STRESS_LEGACY_INLINE, 50);

    /// <summary>determine if a stress mode should be enabled</summary>
    /// <param name="stressArea">stress mode to possibly enable</param>
    /// <param name="weightPercentage">percent of time this mode should be turned on (range 0 to 100); weight 0 effectively disables</param>
    /// <returns>true if this stress mode is enabled</returns>
    /// <remarks>
    ///   <para>Methods may be excluded from stress via name or hash.</para>
    ///   <para>Particular stress modes may be disabled or forcibly enabled.</para>
    ///   <para>With JitStress=2, some stress modes are enabled regardless of weight; these modes are the ones after COUNT_VARN in the enumeration.</para>
    ///   <para>For other modes or for nonzero JitStress values, stress will be enabled selectively for roughly weight% of methods.</para>
    /// </remarks>
    public bool compStressCompile(compStressArea stressArea, int weightPercentage)
    {
        // This can be called early, before info is fully set up.
        if ((info.compMethodName is null) || (info.compFullName is null))
        {
            return false;
        }

        // Inlinees defer to the root method for stress, so that we can
        // more easily isolate methods that cause stress failures.
        if (compIsForInlining)
        {
            return impInlineRoot.compStressCompile(stressArea, weightPercentage);
        }

        var doStress = compStressCompileHelper(stressArea, weightPercentage);

        if (doStress && (compActiveStressModes[(int)(stressArea)] != 0))
        {
            if (verbose)
            {
                jitprintf($"\n\n*** JitStress: {stressArea} ***\n\n");
            }
            compActiveStressModes[(int)(stressArea)] = 1;
        }
        return doStress;
    }

    public bool compStressCompileHelper(compStressArea stressArea, int weightPercentage)
    {
        // TODO: Port Compiler.compStressCompileHelper
        return false;
    }
#else
    public bool compStressCompile(compStressArea stressArea, int weightPercentage) => false;
#endif

#if OPT_CONFIG
    private static ConfigMethodRange s_onlyOptimizeRange;
#endif

    protected void compInitDebuggingInfo()
    {
#if DEBUG
        if (verbose)
        {
            jitprintf($"*************** In compInitDebuggingInfo() for {info.compFullName}\n");
        }
#endif

        //
        // Get hold of the local variable records, if there are any
        //

        info.compVarScopesCount = 0;

        if (opts.compScopeInfo)
        {
            eeGetVars();
        }

        compInitVarScopeMap();

        if (opts.compScopeInfo || opts.compDbgCode)
        {
            compInitScopeLists();
        }

        //
        // Read the stmt-offsets table and the line-number table
        //

        info.compStmtOffsetsImplicit = ICorDebugInfo.NO_BOUNDARIES;

        // We can only report debug info for EnC at places where the stack is empty.
        // Actually, at places where there are not live temps. Else, we won't be able
        // to map between the old and the new versions correctly as we won't have
        // any info for the live temps.

        assert(!opts.compDbgEnC || !opts.compDbgInfo || ((info.compStmtOffsetsImplicit & ~ICorDebugInfo.STACK_EMPTY_BOUNDARIES) == 0));

        info.compStmtOffsetsCount = 0;

        if (opts.compDbgInfo)
        {
            // Get hold of the line# records, if there are any
            eeGetStmtOffsets();

#if DEBUG
            if (verbose)
            {
                jitprintf($"info.compStmtOffsetsCount    = {info.compStmtOffsetsCount}\n");
                jitprintf($"info.compStmtOffsetsImplicit = {(int)(info.compStmtOffsetsImplicit):X4}h");

                if (info.compStmtOffsetsImplicit != ICorDebugInfo.NO_BOUNDARIES)
                {
                    jitprintf(" ( ");

                    if ((info.compStmtOffsetsImplicit & ICorDebugInfo.STACK_EMPTY_BOUNDARIES) != 0)
                    {
                        jitprintf("STACK_EMPTY ");
                    }

                    if ((info.compStmtOffsetsImplicit & ICorDebugInfo.NOP_BOUNDARIES) != 0)
                    {
                        jitprintf("NOP ");
                    }

                    if ((info.compStmtOffsetsImplicit & ICorDebugInfo.CALL_SITE_BOUNDARIES) != 0)
                    {
                        jitprintf("CALL_SITE ");
                    }

                    jitprintf(")");
                }
                jitprintf("\n");

                var stmtOffsets = info.compStmtOffsets.AsSpan(0, info.compStmtOffsetsCount);

                for (var i = 0; i < stmtOffsets.Length; i++)
                {
                    jitprintf($"{i:D2}) IL_{stmtOffsets[i]:X4}h\n");
                }
            }
#endif
        }
    }

    /// <summary>run phases needed for compilation</summary>
    /// <param name="methodCodePtr">address of generated code</param>
    /// <param name="methodCodeSize">size of the generated code (hot + cold sections)</param>
    /// <param name="jitFlags">flags controlling jit behavior</param>
    /// <remarks>
    ///   <para>This is the most interesting 'top level' function in the JIT and goes through the operations of importing, morphing, optimizations and code generation. </para>
    ///   <para>This is called from the EE through the CILJit.compileMethod function.</para>
    ///   <para>For an overview of the structure of the JIT, see: https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/jit/ryujit-overview.md</para>
    ///   <para>Also called for inlinees, though they will only be run through the first few phases.</para>
    /// </remarks>
    protected unsafe void compCompile(out void* methodCodePtr, out int methodCodeSize, JitFlags* jitFlags)
    {
        compFunctionTraceStart();

        // Enable flow graph checks
        activePhaseChecks |= PhaseChecks.CHECK_FG;

        // Prepare for importation
        DoPhase(this, PHASE_PRE_IMPORT, () => {
            if (compIsForInlining)
            {
                // Notify root instance that an inline attempt is about to import IL
                assert(impInlineRoot._inlineStrategy is not null);
                impInlineRoot._inlineStrategy.NoteImport();
            }

            hashBv.Init(this);

            VarSetOps.AssignAllowUninitRhs(this, ref compCurLife, VarSetOps.UninitVal());

            // The temp holding the secret stub argument is used by fgImport() when importing the intrinsic.
            if (info.compPublishStubParam)
            {
                assert(lvaStubArgumentVar == BAD_VAR_NUM);
                lvaStubArgumentVar = lvaGrabTempWithImplicitUse(false, "stub argument");
                lvaGetDesc(lvaStubArgumentVar).Type = TYP_I_IMPL;
            }
        });

        // If we're going to instrument code, we may need to prepare before we import.
        // Also do this before we read in any profile data.
        if (jitFlags->IsSet(JitFlags.JIT_FLAG_BBINSTR))
        {
            DoPhase(this, PHASE_IBCPREP, fgPrepareToInstrumentMethod);
        }

        // Incorporate profile data.
        //   Note: the importer is sensitive to block weights, so this has to happen before importation.
        activePhaseChecks |= PhaseChecks.CHECK_PROFILE | PhaseChecks.CHECK_PROFILE_FLAGS;
        DoPhase(this, PHASE_INCPROFILE, fgIncorporateProfileData);

        activePhaseChecks |= PhaseChecks.CHECK_FG_INIT_BLOCK;
        DoPhase(this, PHASE_CANONICALIZE_ENTRY, fgCanonicalizeFirstBB);

        // If we are doing OSR, update flow to initially reach the appropriate IL offset.
        if (opts.IsOSR)
        {
            fgFixEntryFlowForOSR();
        }

        // Enable the post-phase checks that use internal logic to decide when checking makes sense.
        activePhaseChecks |= PhaseChecks.CHECK_EH | PhaseChecks.CHECK_LOOPS | PhaseChecks.CHECK_UNIQUE | PhaseChecks.CHECK_LINKED_LOCALS;

        // Import: convert the instrs in each basic block to a tree based intermediate representation
        DoPhase(this, PHASE_IMPORTATION, fgImport);

        // If this is a failed inline attempt, we're done.
        if (compIsForInlining && compInlineResult.IsFailure)
        {
#if FEATURE_JIT_METHOD_PERF
            if (compJitTimer is not null)
            {
#if MEASURE_CLRAPI_CALLS
                EndPhase(PHASE_CLR_API);
#endif
                compJitTimer.Terminate(this, CompTimeSummaryInfo.s_compTimeSummary, includePhases: false);
            }
#endif

            methodCodePtr = null;
            methodCodeSize = 0;
            return;
        }

        DoPhase(this, PHASE_EARLY_QMARK_EXPANSION, () => fgExpandQmarkNodes(/*early*/ true));

        // If instrumenting, add block and class probes.
        if (jitFlags->IsSet(JitFlags.JIT_FLAG_BBINSTR))
        {
            DoPhase(this, PHASE_IBCINSTR, fgInstrumentMethod);
        }

        // Expand any patchpoints
        DoPhase(this, PHASE_PATCHPOINTS, fgTransformPatchpoints);

        // Transform indirect calls that require control flow expansion.
        DoPhase(this, PHASE_INDXCALL, fgTransformIndirectCalls);

        // Cleanup un-imported BBs, cleanup un-imported or partially imported try regions, add OSR step blocks.
        DoPhase(this, PHASE_POST_IMPORT, fgPostImportationCleanup);

        // Capture and restore contexts around the body, if needed.
        DoPhase(this, PHASE_ASYNC_SAVE_CONTEXTS, SaveAsyncContexts);

        // If we're importing for inlining, we're done.
        if (compIsForInlining)
        {
#if FEATURE_JIT_METHOD_PERF
            if (compJitTimer is not null)
            {
#if MEASURE_CLRAPI_CALLS
                EndPhase(PHASE_CLR_API);
#endif
                compJitTimer.Terminate(this, CompTimeSummaryInfo.s_compTimeSummary, false);
            }
#endif

            methodCodePtr = null;
            methodCodeSize = 0;
            return;
        }

        // At this point in the phase list, all the inlinee phases have
        // been run, and inlinee compiles have exited, so we should only
        // get this far if we are jitting the root method.
        noway_assert(!compIsForInlining);

        // Prepare for the morph phases
        DoPhase(this, PHASE_MORPH_INIT, fgMorphInit);

        // Inline callee methods into this root method
        DoPhase(this, PHASE_MORPH_INLINE, fgInline);

        // Record "start" values for post-inlining cycles and elapsed time.
        RecordStateAtEndOfInlining();

        if (opts.OptimizationEnabled)
        {
            // Trim dead code that follows no-return calls introduced by inlining, so subsequent phases see a cleaner flow graph.
            DoPhase(this, PHASE_POST_INLINE_NORETURN, fgPostInlineNoReturnCleanup);

            // Try and resolve GDV checks if improved types were found during inlining
            DoPhase(this, PHASE_RESOLVE_GDVS, fgResolveGDVs);

            // Build post-order and remove dead blocks
            DoPhase(this, PHASE_DFS_BLOCKS1, fgDfsBlocksAndRemove);
        }

        // Transform each GT_ALLOCOBJ node into either an allocation helper call or local variable allocation on the stack.
        var objectAllocator = new ObjectAllocator(this);

        if (compObjectStackAllocation && opts.OptimizationEnabled)
        {
            objectAllocator.EnableObjectStackAllocation();
        }

        objectAllocator.Run(); // PHASE_ALLOCATE_OBJECTS

        // Add any internal blocks/trees we may need
        DoPhase(this, PHASE_MORPH_ADD_INTERNAL, fgAddInternal);

#if SWIFT_SUPPORT
        // Transform GT_RETURN nodes into GT_SWIFT_ERROR_RET nodes if this method has Swift error handling
        DoPhase(this, PHASE_SWIFT_ERROR_RET, fgAddSwiftErrorReturns);
#endif

        // Remove empty try regions (try/finally)
        DoPhase(this, PHASE_EMPTY_TRY, fgRemoveEmptyTry);

        // Remove empty try regions (try/catch/fault)
        DoPhase(this, PHASE_EMPTY_TRY_CATCH_FAULT, fgRemoveEmptyTryCatchOrTryFault);

        // Remove empty finally regions
        DoPhase(this, PHASE_EMPTY_FINALLY, fgRemoveEmptyFinally);

        // Streamline chains of finally invocations
        DoPhase(this, PHASE_MERGE_FINALLY_CHAINS, fgMergeFinallyChains);

        // Clone code in finallys to reduce overhead for non-exceptional paths
        DoPhase(this, PHASE_CLONE_FINALLY, fgCloneFinally);

        // Do some flow-related optimizations
        if (opts.OptimizationEnabled)
        {
            // Tail merge
            DoPhase(this, PHASE_HEAD_TAIL_MERGE, () => fgHeadTailMerge(true));

            // Merge common throw blocks
            DoPhase(this, PHASE_MERGE_THROWS, fgTailMergeThrows);

            // Run an early flow graph simplification pass
            DoPhase(this, PHASE_EARLY_UPDATE_FLOW_GRAPH, fgUpdateFlowGraphPhase);
        }

        // Promote struct locals
        DoPhase(this, PHASE_PROMOTE_STRUCTS, fgPromoteStructs);

        // Enable early ref counting of locals
        lvaRefCountState = RCS_EARLY;

        if (opts.OptimizationEnabled)
        {
            // Build post-order and remove dead blocks
            DoPhase(this, PHASE_DFS_BLOCKS2, fgDfsBlocksAndRemove);

            fgNodeThreading = NodeThreading.AllLocals;
        }

        // Simplify local accesses and analyze address exposure.
        DoPhase(this, PHASE_LOCAL_MORPH, fgLocalMorph);

        // Optimize away conversions to/from masks in local variables.
        DoPhase(this, PHASE_OPTIMIZE_MASK_CONVERSIONS, fgOptimizeMaskConversions);

        // Do an early pass of liveness for forward sub and morph.
        // This data is valid until after morph.
        DoPhase(this, PHASE_EARLY_LIVENESS, fgEarlyLiveness);

        // Run a simple forward substitution pass.
        DoPhase(this, PHASE_FWD_SUB, fgForwardSub);

        // Promote struct locals based on primitive access patterns
        DoPhase(this, PHASE_PHYSICAL_PROMOTION, PhysicalPromotion);

        // Expose candidates for implicit byref last-use copy elision.
        DoPhase(this, PHASE_IMPBYREF_COPY_OMISSION, fgMarkImplicitByRefCopyOmissionCandidates);

        // Locals tree list is no longer kept valid.
        fgNodeThreading = NodeThreading.None;

        // Apply the type update to implicit byref parameters; also choose (based on address-exposed
        // analysis) which implicit byref promotions to keep (requires copy to initialize) or discard.
        DoPhase(this, PHASE_MORPH_IMPBYREF, fgRetypeImplicitByRefArgs);

#if DEBUG
        // Now that locals have address-taken and implicit byref marked, we can safely apply stress.
        lvaStressLclFld();
        fgStress64RsltMul();
#endif

        // Morph the trees in all the blocks of the method
        var preMorphBBCount = fgBBcount;
        DoPhase(this, PHASE_MORPH_GLOBAL, fgMorphBlocks);

        DoPhase(this, PHASE_POST_MORPH, () => {
            // Fix any LclVar annotations on discarded struct promotion temps for implicit by-ref args
            fgMarkDemotedImplicitByRefArgs();
            lvaRefCountState = RCS_INVALID;
            fgLocalVarLivenessDone = false;

            fgExpandQmarkNodes(early: false);

#if DEBUG
            compCurBB = null;
#endif

            // Enable IR checks
            activePhaseChecks |= PhaseChecks.CHECK_IR;
        });

        if (opts.OptimizationEnabled)
        {
            // Compute the block weights
            DoPhase(this, PHASE_COMPUTE_BLOCK_WEIGHTS, fgComputeBlockWeights);

            // Try again to remove empty try finally/fault clauses
            DoPhase(this, PHASE_EMPTY_FINALLY_2, fgRemoveEmptyFinally);

            // Remove empty try regions (try/finally)
            DoPhase(this, PHASE_EMPTY_TRY_2, fgRemoveEmptyTry);

            // Remove empty try regions (try/catch/fault)
            DoPhase(this, PHASE_EMPTY_TRY_CATCH_FAULT_2, fgRemoveEmptyTryCatchOrTryFault);

            // Run some flow graph optimizations (but don't reorder)
            DoPhase(this, PHASE_OPTIMIZE_FLOW, optOptimizeFlow);

            // Second pass of tail merge
            DoPhase(this, PHASE_HEAD_TAIL_MERGE2, () => fgHeadTailMerge(false));

            // Compute DFS tree and remove all unreachable blocks.
            DoPhase(this, PHASE_DFS_BLOCKS3, fgDfsBlocksAndRemove);

            // Adjust heuristic-derived edge likelihoods into paths that are known to throw.
            DoPhase(this, PHASE_ADJUST_THROW_LIKELIHOODS, () => ProfileSynthesis.AdjustThrowEdgeLikelihoods(this));

            // Discover and classify natural loops (e.g. mark iterative loops as such).
            DoPhase(this, PHASE_FIND_LOOPS, optFindLoopsPhase);

            // Re-establish profile consistency, now that inlining and morph have run.
            DoPhase(this, PHASE_REPAIR_PROFILE_POST_MORPH, fgRepairProfile);

            // Invert loops
            DoPhase(this, PHASE_INVERT_LOOPS, optInvertLoops);

            // Scale block weights and mark run rarely blocks.
            DoPhase(this, PHASE_SET_BLOCK_WEIGHTS, optSetBlockWeights);

            // Clone loops with optimization opportunities, and choose one based on dynamic condition evaluation.
            DoPhase(this, PHASE_CLONE_LOOPS, optCloneLoops);

            // Unroll loops
            DoPhase(this, PHASE_UNROLL_LOOPS, optUnrollLoops);

            // Compute dominators and exceptional entry blocks
            DoPhase(this, PHASE_COMPUTE_DOMINATORS, fgComputeDominators);
        }

#if DEBUG
        fgDebugCheckLinks();
#endif

        // Decide the kind of code we want to generate. Done here, after the second
        // round of empty-EH removal above, so that EH eliminated post-morph doesn't
        // force fully-interruptible codegen / a frame pointer.
        fgSetOptions();

        // Morph multi-dimensional array operations.
        // (Consider deferring all array operation morphing, including single-dimensional array ops, from global morph to here, so cloning doesn't have to deal with morphed forms.)
        DoPhase(this, PHASE_MORPH_MDARR, fgMorphArrayOps);

        // Create the variable table (and compute variable ref counts)
        DoPhase(this, PHASE_MARK_LOCAL_VARS, lvaMarkLocalVars);

        // IMPORTANT, after this point, locals are ref counted.
        // However, ref counts are not kept incrementally up to date.
        assert(lvaLocalVarRefCounted);

        // Figure out the order in which operators are to be evaluated
        DoPhase(this, PHASE_FIND_OPER_ORDER, fgFindOperOrder);

        // Weave the tree lists.
        // Anyone who modifies the tree shapes after this point is responsible for calling fgSetStmtSeq() to keep the nodes properly linked.
        DoPhase(this, PHASE_SET_BLOCK_ORDER, fgSetBlockOrder);

        fgNodeThreading = NodeThreading.AllTrees;

        // At this point we know if we are fully interruptible or not
        if (opts.OptimizationEnabled)
        {
            var doSsa = true;
            var doEarlyProp = true;
            var doValueNum = true;
            var doLoopHoisting = true;
            var doCopyProp = true;
            var doOptimizeIVs = true;
            var doBranchOpt = true;
            var doCse = true;
            var doAssertionProp = true;
            var doVNBasedIntrinExpansion = true;
            var doRangeAnalysis = true;
            var doRangeCheckCloning = true;
            var doVNBasedDeadStoreRemoval = true;

#if OPT_CONFIG
            doSsa = (JitConfig.JitDoSsa != 0);
            doEarlyProp = doSsa && (JitConfig.JitDoEarlyProp != 0);
            doValueNum = doSsa && (JitConfig.JitDoValueNumber != 0);
            doLoopHoisting = doValueNum && (JitConfig.JitDoLoopHoisting != 0);
            doCopyProp = doValueNum && (JitConfig.JitDoCopyProp != 0);
            doBranchOpt = doValueNum && (JitConfig.JitDoRedundantBranchOpts != 0);
            doCse = doValueNum;
            doAssertionProp = doValueNum && (JitConfig.JitDoAssertionProp != 0);
            doRangeAnalysis = doAssertionProp && (JitConfig.JitDoRangeAnalysis != 0);
            doRangeCheckCloning = doValueNum && doRangeAnalysis;
            doOptimizeIVs = doAssertionProp && (JitConfig.JitDoOptimizeIVs != 0);
            doVNBasedDeadStoreRemoval = doValueNum && (JitConfig.JitDoVNBasedDeadStoreRemoval != 0);
            doVNBasedIntrinExpansion = doValueNum;
#endif

            if (opts.optRepeat)
            {
                opts.optRepeatActive = true;
            }

            while (++opts.optRepeatIteration <= opts.optRepeatCount)
            {
#if DEBUG
                if (verbose && opts.optRepeat)
                {
                    jitprintf($"\n*************** JitOptRepeat: iteration {opts.optRepeatIteration} of {opts.optRepeatCount}\n\n");
                }
#endif

                fgModified = false;

                if (doSsa)
                {
                    // Build up SSA form for the IR
                    DoPhase(this, PHASE_BUILD_SSA, fgSsaBuild);
                }
                else
                {
                    // At least do local var liveness; lowering depends on this.
                    fgSsaLiveness();
                }

                if (doEarlyProp)
                {
                    // Propagate array length and rewrite getType() method call
                    DoPhase(this, PHASE_EARLY_PROP, optEarlyProp);
                }

                if (doValueNum)
                {
                    // Value number the trees
                    DoPhase(this, PHASE_VALUE_NUMBER, fgValueNumber);
                }

                if (doLoopHoisting)
                {
                    // Hoist invariant code out of loops
                    DoPhase(this, PHASE_HOIST_LOOP_CODE, optHoistLoopCode);
                }

                if (doCopyProp)
                {
                    // Perform VN based copy propagation
                    DoPhase(this, PHASE_VN_COPY_PROP, optVnCopyProp);
                }

                if (doBranchOpt)
                {
                    // Optimize redundant branches
                    DoPhase(this, PHASE_OPTIMIZE_BRANCHES, optRedundantBranches);
                }
                else
                {
                    // DFS tree is always invalid after this point.
                    fgInvalidateDfsTree();
                }

                if (doCse)
                {
                    // Remove common sub-expressions
                    DoPhase(this, PHASE_OPTIMIZE_VALNUM_CSES, optOptimizeCSEs);
                }

                if (doAssertionProp)
                {
                    // Coalesce groups of constant-indexed bounds checks.
                    DoPhase(this, PHASE_BOUNDS_CHECK_COALESCE, optBoundsCheckCoalesce);

                    // Assertion propagation
                    DoPhase(this, PHASE_ASSERTION_PROP_MAIN, optAssertionPropMain);
                }

                if (doRangeAnalysis)
                {
                    // Bounds check elimination via range analysis
                    DoPhase(this, PHASE_OPTIMIZE_INDEX_CHECKS, rangeCheckPhase);
                }

                if (doOptimizeIVs)
                {
                    // Simplify and optimize induction variables used in natural loops
                    DoPhase(this, PHASE_OPTIMIZE_INDUCTION_VARIABLES, optInductionVariables);
                }

                fgInvalidateDfsTree();

                if (doVNBasedDeadStoreRemoval)
                {
                    // Note: this invalidates SSA and value numbers on tree nodes.
                    DoPhase(this, PHASE_VN_BASED_DEAD_STORE_REMOVAL, optVNBasedDeadStoreRemoval);
                }

                if (doRangeCheckCloning)
                {
                    // Clone blocks with subsequent bounds checks
                    DoPhase(this, PHASE_RANGE_CHECK_CLONING, optRangeCheckCloning);
                }

                if (doVNBasedIntrinExpansion)
                {
                    // Expand some intrinsics based on VN data
                    DoPhase(this, PHASE_VN_BASED_INTRINSIC_EXPAND, fgVNBasedIntrinsicExpansion);
                }

                // Conservatively mark all VNs as stale
                vnStore = null;

                if (fgModified)
                {
                    // update the flowgraph if we modified it during the optimization phase
                    DoPhase(this, PHASE_OPT_UPDATE_FLOW_GRAPH, fgUpdateFlowGraphPhase);

                    // Clean up unreachable blocks.
                    // In opt-repeat builds, RecomputeFlowGraphAnnotations() will call
                    // fgDfsBlocksAndRemove() when resetting annotations between iterations.
                    // To avoid doing this expensive work twice per iteration, only run this
                    // phase on non-optRepeat builds or on the final optRepeat iteration.

                    if (!opts.optRepeat || (opts.optRepeatIteration == opts.optRepeatCount))
                    {
                        DoPhase(this, PHASE_OPT_DFS_BLOCKS, fgDfsBlocksAndRemove);
                        fgInvalidateDfsTree();
                    }
                }

                // Iterate if requested, resetting annotations first.
                if (opts.optRepeatIteration == opts.optRepeatCount)
                {
                    // If we're done optimizing, just remove the PHIs
                    fgResetForSsa(deepClean: false);
                    break;
                }

                assert(opts.optRepeat);

                ResetOptAnnotations();
                RecomputeFlowGraphAnnotations();

#if DEBUG
                if (verbose)
                {
                    jitprintf("Trees before next JitOptRepeat iteration:\n");
                    fgDispBasicBlocks(true);
                }
#endif
            }

            if (opts.optRepeat)
            {
                opts.optRepeatActive = false;
            }
        }

        optLoopsCanonical = false;

#if DEBUG
        DoPhase(this, PHASE_STRESS_SPLIT_TREE, StressSplitTree);
#endif

        // Try again to remove empty try finally/fault clauses
        DoPhase(this, PHASE_EMPTY_FINALLY_3, fgRemoveEmptyFinally);

        // Remove empty try regions (try/finally)
        DoPhase(this, PHASE_EMPTY_TRY_3, fgRemoveEmptyTry);

        // Remove empty try regions (try/catch/fault)
        DoPhase(this, PHASE_EMPTY_TRY_CATCH_FAULT_3, fgRemoveEmptyTryCatchOrTryFault);

        // Create funclets from the EH handlers.
        DoPhase(this, PHASE_CREATE_FUNCLETS, fgCreateFunclets);

        // Expand casts
        DoPhase(this, PHASE_EXPAND_CASTS, fgLateCastExpansion);

        // Expand runtime lookups (an optimization but we'd better run it in tier0 too)
        DoPhase(this, PHASE_EXPAND_RTLOOKUPS, fgExpandRuntimeLookups);

        // Partially inline static initializations
        DoPhase(this, PHASE_EXPAND_STATIC_INIT, fgExpandStaticInit);

        // Expand thread local access
        DoPhase(this, PHASE_EXPAND_TLS, fgExpandThreadLocalAccess);

        // Expand stack allocated arrays
        DoPhase(this, PHASE_EXPAND_STACK_ARR, fgExpandStackArrayAllocations);

        // Insert GC Polls
        DoPhase(this, PHASE_INSERT_GC_POLLS, fgInsertGCPolls);

        if (opts.OptimizationEnabled)
        {
            // Conditional to switch conversion, and switch peeling
            DoPhase(this, PHASE_SWITCH_RECOGNITION, optRecognizeAndOptimizeSwitchJumps);

            // Optimize boolean conditions
            DoPhase(this, PHASE_OPTIMIZE_BOOLS, optOptimizeBools);

            // If conversion
            DoPhase(this, PHASE_IF_CONVERSION, optIfConversion);

            // Run flow optimizations before reordering blocks
            DoPhase(this, PHASE_OPTIMIZE_PRE_LAYOUT, optOptimizePreLayout);

            // Ensure profile is consistent before starting backend phases
            DoPhase(this, PHASE_REPAIR_PROFILE_PRE_LAYOUT, fgRepairProfile);
        }

#if DEBUG
        // Stash the current estimate of the function's size if necessary.
        if (verbose && opts.OptimizationEnabled)
        {
            compSizeEstimate = 0;
            compCycleEstimate = 0;

            foreach (var block in Blocks)
            {
                foreach (var stmt in block.Statements)
                {
                    compSizeEstimate += stmt.CostSz;
                    compCycleEstimate += stmt.CostEx;
                }
            }
        }
#endif

        // rationalize trees
        var rat = new Rationalizer(this);
        rat.Run(); // PHASE_RATIONALIZE

        fgNodeThreading = NodeThreading.LIR;

        // Enable this to gather statistical data such as
        // call and register argument info, flowgraph and loop info, etc.
        compJitStats();

        if (compIsAsync)
        {
            DoPhase(this, PHASE_ASYNC, TransformAsync);
        }

        // GS security checks for unsafe buffers
        DoPhase(this, PHASE_GS_COOKIE, gsPhase);

#if TARGET_WASM
        // Make EH continuation flow explicit
        DoPhase(this, PHASE_WASM_EH_FLOW, fgWasmEhFlow);

        // Clean up unreachable blocks.
        DoPhase(this, PHASE_DFS_BLOCKS_WASM, fgDfsBlocksAndRemove);

        // Transform any strongly connected components into reducible flow.
        DoPhase(this, PHASE_WASM_TRANSFORM_SCCS, fgWasmTransformSccs);
#endif

        // Assign registers to variables, etc.

        // Create the RA before Lowering, so that Lowering can call RA methods for
        // determining whether locals are register candidates and (for xarch) whether
        // a node is a containable memory op.
        _regAlloc = GetRegisterAllocator(this);

        // Lower
        _pLowering = new Lowering(this, _regAlloc);
        _pLowering.Run(); // PHASE_LOWERING

        // Set stack levels and analyze throw helper usage.
        var stackLevelSetter = new StackLevelSetter(this);
        stackLevelSetter.Run();
        _pLowering.FinalizeOutgoingArgSpace();

#if TARGET_WASM
        // Determine if a Virtual IP is needed and add code as needed to keep the Virtual IP updated.
        DoPhase(this, PHASE_WASM_VIRTUAL_IP, fgWasmVirtualIP);
#endif

        FinalizeEH();

        // We can not add any new tracked variables after this point.
        lvaTrackedFixed = true;

        // Now that lowering is completed we can proceed to perform register allocation
        DoPhase(this, PHASE_LINEAR_SCAN, _regAlloc.DoRegisterAllocation);

        // Copied from rpPredictRegUse()
        IsFullPtrRegMapRequired = codeGen.Interruptible || !codeGen.IsFramePointerUsed;

#if TARGET_WASM
        // Reorder blocks for wasm and figure out wasm control flow nesting
        DoPhase(this, PHASE_WASM_CONTROL_FLOW, fgWasmControlFlow);
#else
        if (opts.OptimizationEnabled)
        {
            // We won't introduce new blocks from here on out, so run the new block layout.
            DoPhase(this, PHASE_OPTIMIZE_LAYOUT, fgSearchImprovedLayout);

            // Now that the flowgraph is finalized, run post-layout optimizations.
            DoPhase(this, PHASE_OPTIMIZE_POST_LAYOUT, optOptimizePostLayout);

            // Determine start of cold region if we are hot/cold splitting
            DoPhase(this, PHASE_DETERMINE_FIRST_COLD_BLOCK, fgDetermineFirstColdBlock);
        }
#endif

#if FEATURE_LOOP_ALIGN
        // Place loop alignment instructions
        DoPhase(this, PHASE_ALIGN_LOOPS, placeLoopAlignInstructions);
#endif

        // The common phase checks and dumps are no longer relevant past this point.
        activePhaseChecks = PhaseChecks.CHECK_NONE;
        activePhaseDumps = PhaseDumps.DUMP_NONE;

        // Generate code
        codeGen.genGenerateCode(out methodCodePtr, out methodCodeSize);

#if TRACK_LSRA_STATS
        if (JitConfig.DisplayLsraStats == 2)
        {
            _regAlloc.dumpLsraStatsCsv(jitstdout());
        }
#endif

        // We're done -- set the active phase to the last phase (which isn't really a phase)
        mostRecentlyActivePhase = PHASE_POST_EMIT;

#if FEATURE_JIT_METHOD_PERF
        if (compJitTimer is not null)
        {
#if MEASURE_CLRAPI_CALLS
            EndPhase(PHASE_CLR_API);
#else
            EndPhase(PHASE_POST_EMIT);
#endif
            compJitTimer.Terminate(this, CompTimeSummaryInfo.s_compTimeSummary, true);
        }
#endif

        // Generate PatchpointInfo
        generatePatchpointInfo();

        RecordStateAtEndOfCompilation();

        var methodsCompiled = Interlocked.Increment(ref jitTotalMethodCompiled);

        if ((JitConfig.JitDisasmSummary != 0) && !compIsForInlining)
        {
            // Tiering name already includes "OSR", we just want the IL offset
            var osrName = opts.IsOSR ? $"@0x{info.compILEntry}" : "";

#if DEBUG
            var fullName = info.compFullName;
            var debugPart = $", hash=0x{info.compMethodHash():x8}{compStressMessage}";
#else
            var fullName = eeGetMethodFullName(info.compMethodHnd, includeReturnType: false, includeThisSpecifier: false);
            var debugPart = "";
#endif

            var metricPart = "";
#if DEBUG
            if (JitConfig.JitMetrics > 0)
            {
                metricPart = $", perfScore={Metrics.PerfScore:F2}, numCse={optCSEcount}";
            }
#endif

            var hasProf = fgHaveProfileData;
            jitprintf($"{methodsCompiled,4}: JIT compiled {fullName} [{compGetTieringName()}{osrName}{(hasProf ? " with " : "")}{(hasProf ? compPgoSourceName : "")}, IL size={info.compILCodeSize}, code size={methodCodeSize}{debugPart}{metricPart}]\n");
            jitprintf(""); // flush
        }

        compFunctionTraceEnd(methodCodePtr, methodCodeSize, isNyi: false);
        JITDUMP($"Method code size: {methodCodeSize}\n");

#if FUNC_INFO_LOGGING
        if (compJitFuncInfoFile is not null)
        {
            assert(!compIsForInlining);
#if DEBUG
            // We only have access to info.compFullName in DEBUG builds.
            flogf(compJitFuncInfoFile, $"{info.compFullName}\n");
            flogf(compJitFuncInfoFile, ""); // flush
#elif FEATURE_SIMD
            compJitFuncInfoFile.WriteLine($" {eeGetMethodFullName(info.compMethodHnd)}");
            compJitFuncInfoFile.Flush();
#endif
        }
#endif
    }

#if DEBUG
    public bool compRandomInlineStress() => compStressCompile(STRESS_RANDOM_INLINE, 50);
#endif

    protected unsafe void compSetOptimizationLevel()
    {
        var theMinOptsValue = false;

        if (compIsForInlining)
        {
            theMinOptsValue = impInlineInfo.InlinerCompiler.opts.MinOpts;
            SetMinOpts(this, theMinOptsValue);
            return;
        }

        if (opts.compFlags == CLFLG_MINOPT)
        {
#if DEBUG
            JITLOG(LL_INFO100, $"CLFLG_MINOPT set for method {info.compFullName}\n");
#endif

            theMinOptsValue = true;
        }

#if DEBUG
        var jitMinOpts = JitConfig.JitMinOpts;

        if (!theMinOptsValue && (jitMinOpts > 0))
        {
            // jitTotalMethodCompiled does not include the method that is being compiled now, so make +1.
            var methodCount = jitTotalMethodCompiled + 1;
            var methodCountMask = methodCount & 0xFFF;
            var kind = (jitMinOpts & 0xF000000) >>> 24;

            switch (kind)
            {
                default:
                {
                    if (jitMinOpts <= methodCount)
                    {
                        if (verbose)
                        {
                            jitprintf(" Optimizations disabled by JitMinOpts and methodCount\n");
                        }
                        theMinOptsValue = true;
                    }
                    break;
                }

                case 0xD:
                {
                    var firstMinopts = (jitMinOpts >>> 12) & 0xFFF;
                    var secondMinopts = (jitMinOpts >>> 0) & 0xFFF;

                    if ((firstMinopts == methodCountMask) || (secondMinopts == methodCountMask))
                    {
                        if (verbose)
                        {
                            jitprintf("0xD: Optimizations disabled by JitMinOpts and methodCountMask\n");
                        }
                        theMinOptsValue = true;
                    }
                    break;
                }

                case 0xE:
                {
                    var startMinopts = (jitMinOpts >>> 12) & 0xFFF;
                    var endMinopts = (jitMinOpts >>> 0) & 0xFFF;

                    if ((startMinopts <= methodCountMask) && (endMinopts >= methodCountMask))
                    {
                        if (verbose)
                        {
                            jitprintf("0xE: Optimizations disabled by JitMinOpts and methodCountMask\n");
                        }
                        theMinOptsValue = true;
                    }
                    break;
                }

                case 0xF:
                {
                    var bitsZero = (jitMinOpts >>> 12) & 0xFFF;
                    var bitsOne = (jitMinOpts >>> 0) & 0xFFF;

                    if (((methodCountMask & bitsOne) == bitsOne) && ((~methodCountMask & bitsZero) == bitsZero))
                    {
                        if (verbose)
                        {
                            jitprintf("0xF: Optimizations disabled by JitMinOpts and methodCountMask\n");
                        }
                        theMinOptsValue = true;
                    }
                    break;
                }
            }
        }

        if (!theMinOptsValue)
        {
            if (JitConfig.JitMinOptsName.contains(info.compMethodHnd, info.compClassHnd, &info.compMethodInfo->args))
            {
                theMinOptsValue = true;
            }
        }

#if OPT_CONFIG
        s_onlyOptimizeRange.EnsureInit(JitConfig.JitOnlyOptimizeRange);

        if (!theMinOptsValue && !s_onlyOptimizeRange.IsEmpty)
        {
            var methHash = info.compMethodHash();
            theMinOptsValue = !s_onlyOptimizeRange.Contains(methHash);
        }
#endif

        if (compStressCompile(STRESS_MIN_OPTS, 5))
        {
            theMinOptsValue = true;
        }
        else if (!IsAot)
        {
            // For AOT we never drop down to MinOpts unless unless CLFLG_MINOPT is set
            if (JitConfig.JitMinOptsCodeSize < info.compILCodeSize)
            {
                JITLOG(LL_INFO10, $"IL Code Size exceeded, using MinOpts for method {info.compFullName}\n");
                theMinOptsValue = true;
            }
            else if (JitConfig.JitMinOptsInstrCount < opts.instrCount)
            {
                JITLOG(LL_INFO10, $"IL instruction count exceeded, using MinOpts for method {info.compFullName}\n");
                theMinOptsValue = true;
            }
            else if (JitConfig.JitMinOptsBbCount < fgBBcount)
            {
                JITLOG(LL_INFO10, $"Basic Block count exceeded, using MinOpts for method {info.compFullName}\n");
                theMinOptsValue = true;
            }
            else if (JitConfig.JitMinOptsLvNumCount < lvaCount)
            {
                JITLOG(LL_INFO10, $"Local Variable Num count exceeded, using MinOpts for method {info.compFullName}\n");
                theMinOptsValue = true;
            }
            else if (JitConfig.JitMinOptsLvRefCount < opts.lvRefCount)
            {
                JITLOG(LL_INFO10, $"Local Variable Ref count exceeded, using MinOpts for method {info.compFullName}\n");
                theMinOptsValue = true;
            }

            if (theMinOptsValue)
            {
                JITLOG(LL_INFO10000, $"IL Code Size,Instr {info.compILCodeSize,4},{opts.instrCount,4}, Basic Block count {fgBBcount,3}, Local Variable Num,Ref count {lvaCount,3},{opts.lvRefCount,3} for method {info.compFullName}\n");

                if (JitConfig.JitBreakOnMinOpts != 0)
                {
                    NO_WAY("MinOpts enabled");
                }
            }
        }
#else
        // Retail check if we should force Minopts due to the complexity of the method.
        // For AOT we never drop down to MinOpts unless unless CLFLG_MINOPT is set.
        if (!theMinOptsValue && !IsAot)
        {
            if ((DEFAULT_MIN_OPTS_CODE_SIZE < info.compILCodeSize) ||
                (DEFAULT_MIN_OPTS_INSTR_COUNT < opts.instrCount) ||
                (DEFAULT_MIN_OPTS_BB_COUNT < fgBBcount) ||
                (DEFAULT_MIN_OPTS_LV_NUM_COUNT < lvaCount) ||
                (DEFAULT_MIN_OPTS_LV_REF_COUNT < opts.lvRefCount))
            {
                theMinOptsValue = true;
            }
        }
#endif

#if DEBUG
        JITLOG(LL_INFO10000, $"IL Code Size,Instr {info.compILCodeSize,4},{opts.instrCount,4}, Basic Block count {fgBBcount,3}, Local Variable Num,Ref count {lvaCount,3},{opts.lvRefCount,3} for method {info.compFullName}\n");
#endif
        SetMinOpts(this, theMinOptsValue);

        static void SetMinOpts(Compiler compiler, bool theMinOptsValue)
        {
            // Set the MinOpts value
            compiler.opts.SetMinOpts(theMinOptsValue);

            // Notify the VM if MinOpts is being used when not requested
            if (theMinOptsValue && !compiler.compIsForInlining && !compiler.opts.compDbgCode)
            {
                if (!compiler.opts.jitFlags->IsSet(JitFlags.JIT_FLAG_TIER0) && !compiler.opts.jitFlags->IsSet(JitFlags.JIT_FLAG_MIN_OPT))
                {
                    compiler.info.compCompHnd->setMethodAttribs(compiler.info.compMethodHnd, CORINFO_FLG_SWITCHED_TO_MIN_OPT);
                    compiler.opts.jitFlags->Clear(JitFlags.JIT_FLAG_TIER1);
                    compiler.opts.jitFlags->Clear(JitFlags.JIT_FLAG_BBOPT);
                    compiler.compSwitchedToMinOpts = true;
                }
            }

#if DEBUG
            if (compiler.verbose && !compiler.compIsForInlining)
            {
                jitprintf($"OPTIONS: opts.MinOpts() == {dspBool(compiler.opts.MinOpts)}\n");
            }
#endif

            // Control the optimizations

            if (compiler.opts.OptimizationDisabled)
            {
                compiler.opts.compFlags &= ~CLFLG_MAXOPT;
                compiler.opts.compFlags |= CLFLG_MINOPT;

                var compEnregLocals = compiler.compEnregLocals;
                compiler.lvaEnregEHVars &= compEnregLocals;
                compiler.lvaEnregMultiRegVars &= compEnregLocals;

                // Scrub any profile data we might have fetched
                compiler.fgRemoveProfileData("compiling with minopt");
            }

            if (!compiler.compIsForInlining)
            {
                var codeGen = compiler.codeGen;

                codeGen.IsFramePointerRequired = false;
                codeGen.IsFrameRequired = compiler.opts.OptimizationDisabled;

#if !TARGET_AMD64
                // The VM sets JitFlags.JIT_FLAG_FRAMED for two reasons:
                //   1. the DOTNET_JitFramed variable is set, or
                //   2. the function is marked "noinline".
                //
                // The reason for #2 is that people mark functions noinline to ensure the show up on in a stack walk.
                // But for AMD64, we don't need a frame pointer for the frame to show up in stack walk.
                if (compiler.opts.jitFlags->IsSet(JitFlags.JIT_FLAG_FRAMED))
                {
                    codeGen.IsFrameRequired = true;
                }
#endif

                if (compiler.opts.OptimizationDisabled || compiler.IsReadyToRun)
                {
                    // The JIT doesn't currently support loop alignment for AOT images outside NativeAOT.
                    // (The JIT doesn't know the final address of the code, hence it can't align code based on unknown addresses.)

                    // loop alignment not supported for AOT code
                    codeGen.ShouldAlignLoops = false;
                }
                else
                {
                    codeGen.ShouldAlignLoops = JitConfig.JitAlignLoops == 1;
                }

#if DEBUG
                var tieringName = compiler.compGetTieringName(true);
                JitMetadata.report(compiler, JitMetadata.TieringName, tieringName);
#endif
            }
        }
    }

    protected unsafe void compSetProcessor()
    {
        //
        // NOTE: This function needs to be kept in sync with EEJitManager.SetCpuInfo() in vm\codeman.cpp
        //

        ref var jitFlags = ref *opts.jitFlags;

        // Processor specific optimizations

        var instructionSetFlags = jitFlags.GetInstructionSetFlags();
        opts.compSupportsISA.Reset();
        opts.compSupportsISAReported.Reset();
        opts.compSupportsISAExactly.Reset();

        // The VM will set the ISA flags depending on actual hardware support and any
        // config values specified by the user. Config may cause the VM to omit baseline
        // ISAs from the supported set. We force their inclusion here so that JIT code
        // can use them unconditionally, but we will honor the config when resolving
        // managed HWIntrinsic methods.
        //
        // We also take care of adding the virtual vector ISAs (i.e. Vector64/128/256/512)
        // here, based on the combination of hardware ISA support and config values.

#if TARGET_XARCH
        // If the VM passed in a virtual vector ISA, it was done to communicate PreferredVectorBitWidth.
        // No check is done for the validity of the value, since it will be clamped to max supported by
        // hardware and config when queried.  We will, therefore, remove the marker ISA and allow it to
        // be re-added if appropriate based on the hardware ISA evaluations below.

        var preferredVectorBitWidth = 0;

        if (instructionSetFlags.HasInstructionSet(InstructionSet_Vector128))
        {
            instructionSetFlags.RemoveInstructionSet(InstructionSet_Vector128);
            preferredVectorBitWidth = 128;
        }
        else if (instructionSetFlags.HasInstructionSet(InstructionSet_Vector256))
        {
            instructionSetFlags.RemoveInstructionSet(InstructionSet_Vector256);
            preferredVectorBitWidth = 256;
        }
        else if (instructionSetFlags.HasInstructionSet(InstructionSet_Vector512))
        {
            instructionSetFlags.RemoveInstructionSet(InstructionSet_Vector512);
            preferredVectorBitWidth = 512;
        }

        opts.preferredVectorByteLength = preferredVectorBitWidth / BITS_PER_BYTE;

        // Only one marker ISA should have been passed in, and it should now be cleared.
        assert(!instructionSetFlags.HasInstructionSet(InstructionSet_Vector128) &&
               !instructionSetFlags.HasInstructionSet(InstructionSet_Vector256) &&
               !instructionSetFlags.HasInstructionSet(InstructionSet_Vector512));

        // Ensure required baseline ISAs are supported in JIT code, even if not passed in by the VM.
        instructionSetFlags.AddInstructionSet(InstructionSet_X86Base);

#if TARGET_AMD64
        instructionSetFlags.AddInstructionSet(InstructionSet_X86Base_X64);
#endif

        // We can now add the virtual vector ISAs as appropriate. Vector128 is part of the required baseline.
        instructionSetFlags.AddInstructionSet(InstructionSet_Vector128);

        if (instructionSetFlags.HasInstructionSet(InstructionSet_AVX))
        {
            instructionSetFlags.AddInstructionSet(InstructionSet_Vector256);
        }

        if (instructionSetFlags.HasInstructionSet(InstructionSet_AVX512))
        {
            instructionSetFlags.AddInstructionSet(InstructionSet_Vector512);
        }
#elif TARGET_ARM64
        // Ensure required baseline ISAs are supported in JIT code, even if not passed in by the VM.
        instructionSetFlags.AddInstructionSet(InstructionSet_ArmBase);
        instructionSetFlags.AddInstructionSet(InstructionSet_ArmBase_Arm64);
        instructionSetFlags.AddInstructionSet(InstructionSet_AdvSimd);
        instructionSetFlags.AddInstructionSet(InstructionSet_AdvSimd_Arm64);

        // Add virtual vector ISAs. These are both supported as part of the required baseline.
        instructionSetFlags.AddInstructionSet(InstructionSet_Vector64);
        instructionSetFlags.AddInstructionSet(InstructionSet_Vector128);
#endif

        assert(instructionSetFlags.Equals(EnsureInstructionSetFlagsAreValid(instructionSetFlags)));
        opts.setSupportedISAs(instructionSetFlags);

#if TARGET_XARCH
        if (!compIsForInlining)
        {
            var emitter = codeGen.Emitter;

            if (canUseVexEncoding())
            {
                emitter.UseVexEncodings = true;

                // Assume each JITted method does not contain AVX instruction at first
                emitter.ContainsAvxInstruction = false;
                emitter.Contains256BitOrMoreAvxInstruction = false;
                emitter.ContainsCallNeedingVzeroupper = false;

                if (canUseEvexEncoding())
                {
                    emitter.UseEvexEncodings = true;
                }
            }

            if (canUseApxEncoding())
            {
                emitter.UseRex2Encodings = true;

                if (emitter.UseEvexEncodings)
                {
                    emitter.UsePromotedEvexEncodings = true;
                }
            }
        }
#endif
    }

    /// <summary>Answer the question: Is a particular ISA allowed to be used implicitly by optimizations?</summary>
    /// <param name="isa"></param>
    /// <returns></returns>
    /// <remarks>The result of this api call will exactly match the target machine on which the function is executed (except for CoreLib, where there are special rules)</remarks>
    private bool compExactlyDependsOn(CORINFO_InstructionSet isa)
    {
#if TARGET_XARCH || TARGET_ARM64 || TARGET_RISCV64
        if (!opts.compSupportsISAReported.HasInstructionSet(isa))
        {
            if (notifyInstructionSetUsage(isa, opts.compSupportsISA.HasInstructionSet(isa)))
            {
                opts.compSupportsISAExactly.AddInstructionSet(isa);
            }
            opts.compSupportsISAReported.AddInstructionSet(isa);
        }
        return (opts.compSupportsISAExactly.HasInstructionSet(isa));
#else
        return false;
#endif
    }

    /// <summary>Search for variable's scope containing offset.</summary>
    /// <param name="varNum">The variable number to search for in the array of scopes.</param>
    /// <param name="offs">The offset value which should occur within the life of the variable.</param>
    /// <returns>VarScopeDsc* of a matching variable that contains the offset within its life begin and life end or NULL if one couldn't be found.</returns>
    /// <remarks>Linear search for matching variables with their life begin and end containing the offset only when the scope count is &lt; MAX_LINEAR_FIND_LCL_SCOPELIST, else use the hashtable lookup.</remarks>
    public ref VarScopeDsc compFindLocalVar(int varNum, int offs)
    {
        if (info.compVarScopesCount < MAX_LINEAR_FIND_LCL_SCOPELIST)
        {
            return ref compFindLocalVarLinear(varNum, offs);
        }
        else
        {
            ref var ret = ref compFindLocalVar(varNum, offs, offs);
            assert(Unsafe.AreSame(ref ret, ref compFindLocalVarLinear(varNum, offs)));
            return ref ret;
        }
    }

    /// <summary>Search for variable's scope containing offset.</summary>
    /// <param name="varNum">The variable number to search for in the array of scopes.</param>
    /// <param name="lifeBeg">The life begin of the variable's scope</param>
    /// <param name="lifeEnd">The life end of the variable's scope</param>
    /// <returns>VarScopeDsc reference of a matching variable that contains the offset within its life begin and life end, or NULL if one couldn't be found.</returns>
    public ref VarScopeDsc compFindLocalVar(int varNum, int lifeBeg, int lifeEnd)
    {
        // Following are the steps used:
        //   1. Index into the hashtable using varNum.
        //   2. Iterate through the linked list at index varNum to find a matching var scope.

        assert(compVarScopeMap is not null);

        if (compVarScopeMap.TryGetValue(varNum, out var varScopeMapInfo))
        {
            var entry = varScopeMapInfo.Head;

            while (entry is not null)
            {
                ref var data = ref info.compVarScopes[entry.DataIndex];

                if ((data.vsdLifeBeg <= lifeBeg) && (data.vsdLifeEnd > lifeEnd))
                {
                    return ref data;
                }
                entry = entry.Next;
            }
        }
        return ref Unsafe.NullRef<VarScopeDsc>();
    }

    /// <summary>Linear search for variable's scope containing offset.</summary>
    /// <param name="varNum">The variable number to search for in the array of scopes.</param>
    /// <param name="offs">The offset value which should occur within the life of the variable.</param>
    /// <returns>A VarScopeDsc reference of a matching variable that contains the offset within its life begin and life end or null when there is no match found.</returns>
    /// <remarks>
    ///   <para>Linear search for matching variables with their life begin and end containing the offset or NULL if one couldn't be found.</para>
    ///   <para>Usually called for scope count = 4. Could be called for values upto 8.</para>
    /// </remarks>
    public ref VarScopeDsc compFindLocalVarLinear(int varNum, int offs)
    {
        for (var i = 0; i < info.compVarScopesCount; i++)
        {
            ref var dsc = ref info.compVarScopes[i];

            if ((dsc.vsdVarNum == varNum) && (dsc.vsdLifeBeg <= offs) && (dsc.vsdLifeEnd > offs))
            {
                return ref dsc;
            }
        }
        return ref Unsafe.NullRef<VarScopeDsc>();

    }

    /// <summary>Answer the question: Is a particular ISA supported?</summary>
    /// <param name="isa"></param>
    /// <returns></returns>
    /// <remarks>Use this api when asking the question so that future ISA questions can be asked correctly or when asserting support/nonsupport for an instruction set</remarks>
    private bool compIsaSupportedDebugOnly(CORINFO_InstructionSet isa)
    {
#if DEBUG && (TARGET_XARCH || TARGET_ARM64)
        return opts.compSupportsISA.HasInstructionSet(isa);
#else
        unreached();
        return false;
#endif
    }

    public int compMapILargNum(int ILargNum)
    {
        assert((ILargNum >= 0) && (ILargNum < info.compILargsCount));

#if TARGET_WASM
        if ((lvaWasmSpArg >= 0) && (lvaWasmSpArg <= ILargNum) && lvaGetDesc(lvaWasmSpArg).lvIsParam)
        {
            ILargNum++;
            assert(ILargNum < info.compLocalsCount); // compLocals count already adjusted.
        }
#endif

        // Note that this works because if compRetBuffArg/compTypeCtxtArg/lvVarargsHandleArg are not present
        // they will be BAD_VAR_NUM (MAX_UINT), which is larger than any variable number.
        if ((info.compRetBuffArg >= 0) && (info.compRetBuffArg <= ILargNum))
        {
            ILargNum++;
            assert(ILargNum < info.compLocalsCount); // compLocals count already adjusted.
        }

        if ((info.compTypeCtxtArg >= 0) && (info.compTypeCtxtArg <= ILargNum))
        {
            ILargNum++;
            assert(ILargNum < info.compLocalsCount); // compLocals count already adjusted.
        }

        if ((lvaAsyncContinuationArg >= 0) && (lvaAsyncContinuationArg <= ILargNum))
        {
            ILargNum++;
            assert(ILargNum < info.compLocalsCount); // compLocals count already adjusted.
        }

        if ((lvaVarargsHandleArg >= 0) && (lvaVarargsHandleArg <= ILargNum))
        {
            ILargNum++;
            assert(ILargNum < info.compLocalsCount); // compLocals count already adjusted.
        }

        assert((ILargNum >= 0) && (ILargNum < info.compArgsCount));
        return ILargNum;
    }

    public int compMapILvarNum(int ILvarNum)
    {
        noway_assert((ILvarNum > ICorDebugInfo.UNKNOWN_ILNUM) && (ILvarNum < info.compILlocalsCount));
        var varNum = BAD_VAR_NUM;

        if (ILvarNum == ICorDebugInfo.VARARGS_HND_ILNUM)
        {
            // The varargs cookie is the last argument in lvaTable[]
            noway_assert(info.compIsVarArgs);

            varNum = lvaVarargsHandleArg;
            noway_assert(lvaTable[varNum].lvIsParam);
        }
        else if (ILvarNum == ICorDebugInfo.RETBUF_ILNUM)
        {
            noway_assert(info.compRetBuffArg != BAD_VAR_NUM);
            varNum = info.compRetBuffArg;
        }
        else if (ILvarNum == ICorDebugInfo.TYPECTXT_ILNUM)
        {
            noway_assert(info.compTypeCtxtArg >= 0);
            varNum = info.compTypeCtxtArg;
        }
        else if (ILvarNum == ICorDebugInfo.ASYNC_CONTINUATION_ILNUM)
        {
            noway_assert(lvaAsyncContinuationArg != BAD_VAR_NUM);
            varNum = lvaAsyncContinuationArg;
        }
        else if (ILvarNum < info.compILargsCount)
        {
            // Parameter
            assert(ILvarNum >= 0);

            varNum = compMapILargNum(ILvarNum);
            noway_assert(lvaTable[varNum].lvIsParam);
        }
        else if (ILvarNum < info.compILlocalsCount)
        {
            // Local variable
            assert(ILvarNum >= info.compILargsCount);

            var lclNum = ILvarNum - info.compILargsCount;
            varNum = info.compArgsCount + lclNum;
            noway_assert(!lvaTable[varNum].lvIsParam);
        }
        else
        {
            unreached();
        }

        noway_assert(varNum < info.compLocalsCount);
        return varNum;
    }

    /// <summary>Returns the IL variable number given our internal varNum or UNKNOWN_ILNUM if it cannot be mapped.</summary>
    /// <param name="varNum"></param>
    /// <returns></returns>
    /// <remarks>Special return values are VARG_ILNUM, RETBUF_ILNUM, TYPECTXT_ILNUM, ASYNC_CONTINUATION_ILNUM.</remarks>
    public unsafe int compMap2ILvarNum(int varNum)
    {
        if (compIsForInlining)
        {
            return impInlineInfo.InlinerCompiler.compMap2ILvarNum(varNum);
        }

        noway_assert((varNum >= 0) && (varNum < lvaCount));

        if (varNum == info.compRetBuffArg)
        {
            return ICorDebugInfo.RETBUF_ILNUM;
        }

        // Is this a varargs function?
        if (info.compIsVarArgs && (varNum == lvaVarargsHandleArg))
        {
            return ICorDebugInfo.VARARGS_HND_ILNUM;
        }

        // We create an extra argument for the type context parameter
        // needed for shared generic code.
        if (((info.compMethodInfo->args.callConv & CORINFO_CALLCONV_PARAMTYPE) is not 0) && (varNum == info.compTypeCtxtArg))
        {
            return ICorDebugInfo.TYPECTXT_ILNUM;
        }

#if FEATURE_FIXED_OUT_ARGS
        if (varNum == lvaOutgoingArgSpaceVar)
        {
            return ICorDebugInfo.UNKNOWN_ILNUM; // Cannot be mapped
        }
#endif

        if (varNum == lvaAsyncContinuationArg)
        {
            return ICorDebugInfo.ASYNC_CONTINUATION_ILNUM;
        }

#if TARGET_WASM
        if (varNum == lvaWasmSpArg)
        {
            return ICorDebugInfo.UNKNOWN_ILNUM;
        }
#endif

        var originalVarNum = varNum;

        // Now mutate varNum to remove extra parameters from the count.
        if (((info.compMethodInfo->args.callConv & CORINFO_CALLCONV_PARAMTYPE) is not 0) && (info.compTypeCtxtArg >= 0) && (info.compTypeCtxtArg < originalVarNum))
        {
            varNum--;
        }

        if (info.compIsVarArgs && (lvaVarargsHandleArg >= 0) && (lvaVarargsHandleArg < originalVarNum))
        {
            varNum--;
        }

        if ((lvaAsyncContinuationArg != BAD_VAR_NUM) && (lvaAsyncContinuationArg >= 0) && (lvaAsyncContinuationArg < originalVarNum))
        {
            varNum--;
        }

        // Is there a hidden argument for the return buffer. Note that this code
        // works because if the RetBuffArg is not present, compRetBuffArg will be
        // BAD_VAR_NUM
        if ((info.compRetBuffArg != BAD_VAR_NUM) && (info.compRetBuffArg >= 0) && (info.compRetBuffArg < originalVarNum))
        {
            varNum--;
        }

#if TARGET_WASM
        if ((lvaWasmSpArg >= 0) && (lvaWasmSpArg < originalVarNum) && lvaGetDesc(lvaWasmSpArg).lvIsParam)
        {
            varNum--;
        }
#endif

        if ((varNum < 0) || (varNum >= info.compLocalsCount))
        {
            // Cannot be mapped
            return ICorDebugInfo.UNKNOWN_ILNUM;
        }
        return varNum;
    }

    /// <summary>Answer the question: Is a particular ISA allowed to be used implicitly by optimizations?</summary>
    /// <param name="isa"></param>
    /// <returns></returns>
    /// <remarks>
    ///   <para>The result of this api call will match the target machine if the result is true.</para>
    ///   <para>If the result is false, then the target machine may have support for the instruction.</para>
    /// </remarks>
    private bool compOpportunisticallyDependsOn(CORINFO_InstructionSet isa)
        => opts.compSupportsISA.HasInstructionSet(isa) && compExactlyDependsOn(isa);

#if DEBUG
    public VarName? compVarName(regNumber reg, bool isFloatReg = false)
    {
        if (isFloatReg)
        {
            assert(genIsValidFloatReg(reg));
        }
        else
        {
            assert(genIsValidReg(reg));
        }

        if ((info.compVarScopesCount > 0) && (compCurBB is not null) && opts.varNames)
        {
            // Look for the matching register
            for (var lclNum = 0; lclNum < lvaCount; lclNum++)
            {
                ref var varDsc = ref lvaGetDesc(lclNum);

                // If the variable is not in a register, or not in the register we're looking for, quit.
                // Also, if it is a compiler generated variable (i.e. slot# > info.compVarScopesCount), don't bother.

                if (varDsc.lvRegister && (varDsc.RegNum == reg) && (varDsc.lvSlotNum < info.compVarScopesCount))
                {
                    // check if variable in that register is live
                    if (VarSetOps.IsMember(this, compCurLife, varDsc._varIndex))
                    {
                        // variable is live - find the corresponding slot
                        ref var varScope = ref compFindLocalVar(varDsc.lvSlotNum, compCurBB.bbCodeOffs, compCurBB.bbCodeOffsEnd);

                        if (!Unsafe.IsNullRef(in varScope))
                        {
                            return varScope.vsdName;
                        }
                    }
                }
            }
        }
        return null;
    }
#endif
}
