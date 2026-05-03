// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
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

    protected nuint compMaxUncheckedOffsetForNullObject;

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
    public static nuint compJitFuncInfoFilename;

    /// <summary>If a log file for per-function information is required, this is the stream to write to.</summary>
    public static StreamWriter? compJitFuncInfoFile;
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

    /// <summary>Returns true if the compiler instance is created for inlining.</summary>
    [MemberNotNullWhen(true, nameof(impInlineInfo), nameof(compInlineResult))]
    [MemberNotNullWhen(false, nameof(codeGen), nameof(m_inlineStrategy))]
    public bool compIsForInlining => impInlineInfo is not null;

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

    public unsafe CorJitResult compCompileAfterInit(CORINFO_MODULE_HANDLE moduleHandle, out void* methodCodePtr, out uint methodCodeSize, in JitFlags compileFlags)
    {
        // compInit should have set these already.
        noway_assert(info.compMethodInfo is not null);
        noway_assert(info.compCompHnd is not null);
        noway_assert(info.compMethodHnd is not null);

#if FEATURE_JIT_METHOD_PERF
        static bool checkedForJitTimeLog = false;

        if (!checkedForJitTimeLog)
        {
            InterlockedCompareExchangeT(&Compiler::compJitTimeLogFilename, JitConfig.JitTimeLogFile(), NULL);

            // At a process or module boundary clear the file and start afresh.
            JitTimer::PrintCsvHeader();

            checkedForJitTimeLog = true;
        }

        if ((compJitTimeLogFilename is not null) || (JitTimeLogCsv() is not null))
        {
            pCompJitTimer = JitTimer::Create(this, info.compMethodInfo->ILCodeSize);
        }
#endif

#if FUNC_INFO_LOGGING
        var pTmpJitFuncInfoFilenameUtf8 = JitConfig[ConfigString.JitFuncInfoFile];

        if (pTmpJitFuncInfoFilenameUtf8 is not null)
        {
            var pOldFuncInfoFileNameUtf8 = (byte*)(Interlocked.CompareExchange(ref compJitFuncInfoFilename, (nuint)(pTmpJitFuncInfoFilenameUtf8), 0));

            if (pOldFuncInfoFileNameUtf8 is null)
            {
                var tmpJitFuncInfoFilenameUtf8 = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(pTmpJitFuncInfoFilenameUtf8);
                var tmpJitFuncInfoFilename = Encoding.UTF8.GetString(tmpJitFuncInfoFilenameUtf8);

                assert(compJitFuncInfoFile is null);
                compJitFuncInfoFile = new StreamWriter(tmpJitFuncInfoFilename, append: true);
            }
        }
#endif

        // if (s_compMethodsCount==0) setvbuf(jitstdout(), NULL, _IONBF, 0);

        if (compIsForInlining)
        {
            compileFlags.Clear(JitFlag.JIT_FLAG_OSR);
            info.compILEntry = 0;
            info.compPatchpointInfo = null;
        }
        else if (compileFlags.IsSet(JitFlag.JIT_FLAG_OSR))
        {
            // Fetch OSR info from the runtime
            fixed (uint* pILEntry = &info.compILEntry)
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
        // Note that it might be better to do this immediately when setting the JIT flags in CILJit::compileMethod()
        // (when JitFlags::SetFromFlags() is called), but this is close enough. (To move this logic to
        // CILJit::compileMethod() would require moving the info.compMatchedVM computation there as well.)
        //
        // We additionally want to do this for AltJit so that we can validate ISAs that the underlying CPU may
        // not support directly. Doing this check later, after opts.altJit has been initialized might be better
        // but it requires moving the whole set of logic down into compCompileHelper after compInitOptions has
        // run and we're going to end up exiting early if JIT_FLAG_ALT_JIT and opts.altJit don't match anyways

        var enableAvailableIsas = !info.compMatchedVM;

#if DEBUG
        if (compileFlags.IsSet(JitFlag.JIT_FLAG_ALT_JIT) && (JitConfig[ConfigInteger.RunAltJitCode] == 0))
        {
            enableAvailableIsas = true;
        }
#endif

        if (enableAvailableIsas)
        {
            var currentInstructionSetFlags = compileFlags.GetInstructionSetFlags();
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
                if ((JitConfig[ConfigInteger.JitUseScalableVectorT] != 0) && currentInstructionSetFlags.HasInstructionSet(InstructionSet_VectorT))
                {
                    // Vector<T> will use SVE instead of NEON.
                    instructionSetFlags.RemoveInstructionSet(InstructionSet_VectorT128);
                    instructionSetFlags.AddInstructionSet(InstructionSet_VectorT);
                }
#endif
            }

            instructionSetFlags.AddInstructionSet(InstructionSet_ArmBase);
            instructionSetFlags.AddInstructionSet(InstructionSet_AdvSimd);

            if (JitConfig[ConfigInteger.EnableArm64Aes] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_Aes);
            }

            if (JitConfig[ConfigInteger.EnableArm64Crc32] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_Crc32);
            }

            if (JitConfig[ConfigInteger.EnableArm64Dp] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_Dp);
            }

            if (JitConfig[ConfigInteger.EnableArm64Rdm] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_Rdm);
            }

            if (JitConfig[ConfigInteger.EnableArm64Sha1] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_Sha1);
            }

            if (JitConfig[ConfigInteger.EnableArm64Sha256] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_Sha256);
            }

            if (JitConfig[ConfigInteger.EnableArm64Atomics] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_Atomics);
            }

            if (JitConfig[ConfigInteger.EnableArm64Dczva] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_Dczva);
            }

            if (JitConfig[ConfigInteger.EnableArm64Sve] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_Sve);
            }

            if (JitConfig[ConfigInteger.EnableArm64Sve2] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_Sve2);
            }

            if (JitConfig[ConfigInteger.EnableArm64Sha3] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_Sha3);
            }

            if (JitConfig[ConfigInteger.EnableArm64Sm4] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_Sm4);
            }

            if (JitConfig[ConfigInteger.EnableArm64SveAes] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_SveAes);
            }

            if (JitConfig[ConfigInteger.EnableArm64SveSha3] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_SveSha3);
            }

            if (JitConfig[ConfigInteger.EnableArm64SveSm4] != 0)
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

            if (JitConfig[ConfigInteger.EnableAVX] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_AVX);
            }

            if (JitConfig[ConfigInteger.EnableAVX2] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_AVX2);
            }

            if (JitConfig[ConfigInteger.EnableAVX512] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_AVX512);
            }

            if (JitConfig[ConfigInteger.EnableAVX512v2] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_AVX512v2);
            }

            if (JitConfig[ConfigInteger.EnableAVX512v3] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_AVX512v3);
            }

            if (JitConfig[ConfigInteger.EnableAVX10v1] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_AVX10v1);
            }

            if (JitConfig[ConfigInteger.EnableAVX10v2] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_AVX10v2);
            }

            if (JitConfig[ConfigInteger.EnableAPX] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_APX);
            }

            if (JitConfig[ConfigInteger.EnableAES] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_AES);

                if (JitConfig[ConfigInteger.EnableVAES] != 0)
                {
                    instructionSetFlags.AddInstructionSet(InstructionSet_AES_V256);
                    instructionSetFlags.AddInstructionSet(InstructionSet_AES_V512);
                }
            }

            if (JitConfig[ConfigInteger.EnableAVX512VP2INTERSECT] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_AVX512VP2INTERSECT);
            }

            if (JitConfig[ConfigInteger.EnableAVXIFMA] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_AVXIFMA);
            }

            if (JitConfig[ConfigInteger.EnableAVXVNNI] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_AVXVNNI);
            }

            if (JitConfig[ConfigInteger.EnableGFNI] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_GFNI);
                instructionSetFlags.AddInstructionSet(InstructionSet_GFNI_V256);
                instructionSetFlags.AddInstructionSet(InstructionSet_GFNI_V512);
            }

            if (JitConfig[ConfigInteger.EnableSHA] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_SHA);
            }

            if (JitConfig[ConfigInteger.EnableWAITPKG] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_WAITPKG);
            }

            if (JitConfig[ConfigInteger.EnableX86Serialize] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_X86Serialize);
            }
#elif TARGET_RISCV64
            instructionSetFlags.AddInstructionSet(InstructionSet_RiscV64Base);

            if (JitConfig[ConfigInteger.EnableRiscV64Zba] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_Zba);
            }

            if (JitConfig[ConfigInteger.EnableRiscV64Zbb] != 0)
            {
                instructionSetFlags.AddInstructionSet(InstructionSet_Zbb);
            }
#endif

            // These calls are important and explicitly ordered to ensure that the flags are correct in
            // the face of missing or removed instruction sets. Without them, we might end up with incorrect
            // downstream checks.

            instructionSetFlags.Set64BitInstructionSetVariants();
            instructionSetFlags = EnsureInstructionSetFlagsAreValid(instructionSetFlags);

            compileFlags.SetInstructionSetFlags(instructionSetFlags);
        }

        // Set the context for token lookup.
        if (compIsForInlining)
        {
            impTokenLookupContextHandle = impInlineInfo.tokenLookupContextHandle;

            var inlineCandidateInfo = impInlineInfo.inlineCandidateInfo;
            assert(inlineCandidateInfo.clsHandle == info.compClassHnd);
            assert(inlineCandidateInfo.clsAttr == info.compCompHnd->getClassAttribs(info.compClassHnd));

            // printf("%x != %x\n", inlineCandidateInfo.clsAttr,
            // info.compCompHnd->getClassAttribs(info.compClassHnd));
            info.compClassAttr = inlineCandidateInfo.clsAttr;
        }
        else
        {
            impTokenLookupContextHandle = METHOD_BEING_COMPILED_CONTEXT();
            info.compClassAttr = info.compCompHnd->getClassAttribs(info.compClassHnd);
        }

#if DEBUG
        if (JitConfig[ConfigInteger.EnableExtraSuperPmiQueries] != 0)
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
                    eeGetClassName(sig.sigInst.classInst[i]);
                }
            }

            var methodInst = sig.sigInst.methInstCount;

            if (methodInst > 0)
            {
                for (var i = 0; i < methodInst; i++)
                {
                    eeGetClassName(sig.sigInst.methInst[i]);
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
            result = compCompileHelper(moduleHandle, info.compCompHnd, info.compMethodInfo, out methodCodePtr, out methodCodeSize, compileFlags);
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
    public bool compCanHavePatchpoints()
        => compCanHavePatchpoints(out Unsafe.NullRef<string>());

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

        Unsafe.SkipInit(out reason);

        if (!Unsafe.IsNullRef(in reason))
        {
            reason = whyNot;
        }
        return whyNot.Length != 0;
    }

    public unsafe CorJitResult compCompileHelper(CORINFO_MODULE_HANDLE classPtr, COMP_HANDLE compHnd, CORINFO_METHOD_INFO* methodInfo, out void* methodCodePtr, out uint methodCodeSize, in JitFlags compileFlags)
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
            compInlineContext = m_inlineStrategy.RootContext;
        }

        // compInitOptions will set the correct verbose flag.

        compInitOptions(compileFlags);

        if (!compIsForInlining && !opts.altJit && opts.jitFlags->IsSet(JitFlag.JIT_FLAG_ALT_JIT))
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
        if (JitConfig[ConfigInteger.JitAggressiveInlining] != 0)
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
            JITLOG(LL_INFO100000, $"\nINLINER impTokenLookupContextHandle for {eeGetMethodFullName(info.compMethodHnd)} is 0x{dspPtr(impTokenLookupContextHandle):X}.\n");
        }

#endif // DEBUG

        impCanReimport = compStressCompile(STRESS_CHK_REIMPORT, 15);

        // Initialize set a bunch of global values

        info.compScopeHnd = classPtr;
        info.compXcptnsCount = methodInfo->EHcount;
        info.compMaxStack = methodInfo->maxStack;

        if (!compIsForInlining)
        {
            // Initialize emitter
            codeGen.Emitter.emitBegCG(this, compHnd);
        }

        info.compIsStatic = (info.compFlags & CORINFO_FLG_STATIC) != 0;
        info.compPublishStubParam = opts.jitFlags->IsSet(JitFlag.JIT_FLAG_PUBLISH_SECRET_PARAM);

        if (opts.IsReversePInvoke)
        {
            bool unused;
            info.compCallConv = info.compCompHnd->getUnmanagedCallConv(methodInfo->ftn, null, &unused);
            info.compArgOrder = Target.g_tgtUnmanagedArgOrder;
        }
        else
        {
            info.compCallConv = CorInfoCallConvExtension.Managed;
            info.compArgOrder = Target.g_tgtArgOrder;
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

        if (!info.compMatchedVM && compileFlags.IsSet(JitFlag.JIT_FLAG_OSR))
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
                JITDUMP("Mismatched altjit + OSR -- updating tier0 frame size from %d to %d\n", totalFrameSize, totalFrameSize + frameSizeUpdate);

                // Allocate a local copy with altered frame size.
                //
                var patchpointInfoSize = PatchpointInfo.ComputeSize(info.compLocalsCount);
                var newInfo = (PatchpointInfo*)(NativeMemory.Alloc(patchpointInfoSize));

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
            // We're AOT compiling the root method. We also will analyze it as
            // a potential inline candidate.
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

            // Find the basic blocks. We must do this regardless of
            // inlineability, since we are prejitting this method.
            //
            // This will also update the status of this method as
            // an inline candidate.
            fgFindBasicBlocks();

            // Undo the temporary setup.
            assert(compInlineResult == prejitResult);
            compInlineResult = null;

            // If still a viable, discretionary inline, assess
            // profitability.
            if (prejitResult.IsDiscretionaryCandidate)
            {
                prejitResult.DetermineProfitability(methodInfo);
            }

            m_inlineStrategy.NotePrejitDecision(prejitResult);

            // Handle the results of the inline analysis.
            if (prejitResult.IsFailure)
            {
                // This method is a bad inlinee according to our
                // analysis.  We will let the InlineResult destructor
                // mark it as noinline in the prejit image to save the
                // jit some work.
                //
                // This decision better not be context-dependent.
                assert(prejitResult.IsNever);
            }
            else
            {
                // This looks like a viable inline candidate.  Since
                // we're not actually inlining, don't report anything.
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

        // We may decide to optimize this method,
        // to avoid spending a long time stuck in Tier0 code.
        //
        if (fgCanSwitchToOptimized)
        {
            // We only expect to be able to do this at Tier0.
            //
            assert(opts.jitFlags->IsSet(JitFlag.JIT_FLAG_TIER0));

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

            if (compTailPrefixSeen && !opts.jitFlags->IsSet(JitFlag.JIT_FLAG_BBINSTR))
            {
                reason = "tail.call and not BBINSTR";
            }
            else if (compHasBackwardJump && ((info.compFlags & CORINFO_FLG_DISABLE_TIER0_FOR_LOOPS) != 0))
            {
                reason = "loop";
            }

            if (compHasBackwardJump && (reason.Length == 0) && (JitConfig[ConfigInteger.TC_OnStackReplacement] > 0))
            {
                var canEscapeViaOSR = compCanHavePatchpoints(out reason);

#if DEBUG
                if (canEscapeViaOSR)
                {
                    // Optionally disable OSR by method hash.
                    // This will force any method that might otherwise get trapped in Tier0 to be optimized.
                    s_jitEnableOsrRange.EnsureInit(JitConfig[ConfigString.JitEnableOsrRange]);
                    var hash = impInlineRoot.info.compMethodHash();

                    if (!s_jitEnableOsrRange.Contains(hash))
                    {
                        canEscapeViaOSR = false;
                        reason = "OSR disabled by JitEnableOsrRange";
                    }
                }
#endif

                if (canEscapeViaOSR)
                {
                    JITDUMP("\nOSR enabled for this method\n");
                    if (compHasBackwardJump && !compTailPrefixSeen && opts.jitFlags->IsSet(JitFlag.JIT_FLAG_BBINSTR_IF_LOOPS) && opts.IsTier0)
                    {
                        assert((info.compFlags & CORINFO_FLG_DISABLE_TIER0_FOR_LOOPS) == 0);
                        opts.jitFlags->Set(JitFlag.JIT_FLAG_BBINSTR);
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
        if ((JitConfig[ConfigInteger.JitInstrumentIfOptimizing] != 0) && opts.OptimizationEnabled && !IsReadyToRun)
        {
            // Optionally disable by range
            s_jitInstrumentIfOptimizingRange.EnsureInit(JitConfig[ConfigString.JitInstrumentIfOptimizingRange]);
            var hash = impInlineRoot.info.compMethodHash();

            if (s_jitInstrumentIfOptimizingRange.Contains(hash))
            {
                JITDUMP("\nEnabling instrumentation\n");
                opts.jitFlags->Set(JitFlag.JIT_FLAG_BBINSTR);
            }
        }
#endif

        if ((JitConfig[ConfigInteger.JitDisasmOnlyOptimized] != 0) && (!opts.OptimizationEnabled))
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
            compInlineResult.NoteInt(InlineObservation.CALLEE_NUMBER_OF_BASIC_BLOCKS, (int)(fgBBcount));

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

        compCompile(out methodCodePtr, out methodCodeSize, compileFlags);

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
                if (compiler.opts.jitFlags->IsSet(JitFlag.JIT_FLAG_ALT_JIT) && (JitConfig[ConfigInteger.RunAltJitCode] == 0))
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

    public unsafe void compFunctionTraceEnd(void* methodCodePtr, uint methodCodeSize, bool isNyi)
    {
        // TODO: Port compFunctionTraceEnd
    }

    public unsafe void compInitOptions(in JitFlags compileFlags)
    {
        // TODO: Port compInitOptions
    }

    public string compGetTieringName(bool wantShortName = false)
    {
        // TODO: Port compGetTieringName
        return "";
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
    public bool compStressCompile(compStressArea stressArea, uint weightPercentage)
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

    public bool compStressCompileHelper(compStressArea stressArea, uint weightPercentage)
    {
        // TODO: Port Compiler.compStressCompileHelper
        return false;
    }
#endif

    /// <summary>Should we actually fire the noway assert body and the exception handler?</summary>
    /// <returns></returns>
    public bool compShouldThrowOnNoway()
    {
        // TODO: Port compShouldThrowOnNoway
        return true;
    }

#if OPT_CONFIG
    private static ConfigMethodRange s_onlyOptimizeRange;
#endif

    protected void compInitDebuggingInfo()
    {
        // TODO: Port compInitDebuggingInfo
    }

    /// <summary>run phases needed for compilation</summary>
    /// <param name="methodCodePtr">address of generated code</param>
    /// <param name="methodCodeSize">size of the generated code (hot + cold sections)</param>
    /// <param name="compileFlags">flags controlling jit behavior</param>
    /// <remarks>
    ///   <para>This is the most interesting 'toplevel' function in the JIT and goes through the operations of importing, morphing, optimizations and code generation. </para>
    ///   <para>This is called from the EE through the CILJit.compileMethod function.</para>
    ///   <para>For an overview of the structure of the JIT, see: https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/jit/ryujit-overview.md</para>
    ///   <para>Also called for inlinees, though they will only be run through the first few phases.</para>
    /// </remarks>
    protected unsafe void compCompile(out void* methodCodePtr, out uint methodCodeSize, in JitFlags compileFlags)
    {
        // TODO: Port compCompile
        methodCodePtr = null;
        methodCodeSize = 0;
    }

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
            JITLOG(LL_INFO100, $"CLFLG_MINOPT set for method {info.compFullName}\n");
            theMinOptsValue = true;
        }

#if DEBUG
        var jitMinOpts = JitConfig[ConfigInteger.JitMinOpts];

        if (!theMinOptsValue && (jitMinOpts > 0))
        {
            // jitTotalMethodCompiled does not include the method that is being compiled now, so make +1.
            var methodCount = jitTotalMethodCompiled + 1;
            var methodCountMask = methodCount & 0xFFF;
            var kind = (jitMinOpts & 0xF000000) >> 24;

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
                    var firstMinopts = (jitMinOpts >> 12) & 0xFFF;
                    var secondMinopts = (jitMinOpts >> 0) & 0xFFF;

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
                    var startMinopts = (jitMinOpts >> 12) & 0xFFF;
                    var endMinopts = (jitMinOpts >> 0) & 0xFFF;

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
                    var bitsZero = (jitMinOpts >> 12) & 0xFFF;
                    var bitsOne = (jitMinOpts >> 0) & 0xFFF;

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
            if (JitConfig[ConfigMethodSet.JitMinOptsName].contains(info.compMethodHnd, info.compClassHnd, &info.compMethodInfo->args))
            {
                theMinOptsValue = true;
            }
        }

#if OPT_CONFIG
        s_onlyOptimizeRange.EnsureInit(JitConfig[ConfigString.JitOnlyOptimizeRange]);

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
            if ((uint)(JitConfig[ConfigInteger.JitMinOptsCodeSize]) < info.compILCodeSize)
            {
                JITLOG(LL_INFO10, $"IL Code Size exceeded, using MinOpts for method {info.compFullName}\n");
                theMinOptsValue = true;
            }
            else if ((uint)(JitConfig[ConfigInteger.JitMinOptsInstrCount]) < opts.instrCount)
            {
                JITLOG(LL_INFO10, $"IL instruction count exceeded, using MinOpts for method {info.compFullName}\n");
                theMinOptsValue = true;
            }
            else if ((uint)(JitConfig[ConfigInteger.JitMinOptsBbCount]) < fgBBcount)
            {
                JITLOG(LL_INFO10, $"Basic Block count exceeded, using MinOpts for method {info.compFullName}\n");
                theMinOptsValue = true;
            }
            else if ((uint)(JitConfig[ConfigInteger.JitMinOptsLvNumCount]) < lvaCount)
            {
                JITLOG(LL_INFO10, $"Local Variable Num count exceeded, using MinOpts for method {info.compFullName}\n");
                theMinOptsValue = true;
            }
            else if ((uint)(JitConfig[ConfigInteger.JitMinOptsLvRefCount]) < opts.lvRefCount)
            {
                JITLOG(LL_INFO10, $"Local Variable Ref count exceeded, using MinOpts for method {info.compFullName}\n");
                theMinOptsValue = true;
            }

            if (theMinOptsValue)
            {
                JITLOG(LL_INFO10000, $"IL Code Size,Instr {info.compILCodeSize:D4},{opts.instrCount:D4}, Basic Block count {fgBBcount:D3}, Local Variable Num,Ref count {lvaCount:D3},{opts.lvRefCount:D3} for method {info.compFullName}\n");

                if (JitConfig[ConfigInteger.JitBreakOnMinOpts] != 0)
                {
                    assert(false, "MinOpts enabled");
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

        JITLOG(LL_INFO10000, $"IL Code Size,Instr {info.compILCodeSize:D4},{opts.instrCount:D4}, Basic Block count {fgBBcount:D3}, Local Variable Num,Ref count {lvaCount:D3},{opts.lvRefCount:D3} for method {info.compFullName}\n");
        SetMinOpts(this, theMinOptsValue);

        static void SetMinOpts(Compiler compiler, bool theMinOptsValue)
        {
            // Set the MinOpts value
            compiler.opts.SetMinOpts(theMinOptsValue);

            // Notify the VM if MinOpts is being used when not requested
            if (theMinOptsValue && !compiler.compIsForInlining && !compiler.opts.compDbgCode)
            {
                if (!compiler.opts.jitFlags->IsSet(JitFlag.JIT_FLAG_TIER0) && !compiler.opts.jitFlags->IsSet(JitFlag.JIT_FLAG_MIN_OPT))
                {
                    compiler.info.compCompHnd->setMethodAttribs(compiler.info.compMethodHnd, CORINFO_FLG_SWITCHED_TO_MIN_OPT);
                    compiler.opts.jitFlags->Clear(JitFlag.JIT_FLAG_TIER1);
                    compiler.opts.jitFlags->Clear(JitFlag.JIT_FLAG_BBOPT);
                    compiler.compSwitchedToMinOpts = true;
                }
            }

#if DEBUG
            if (compiler.verbose && !compiler.compIsForInlining)
            {
                jitprintf("OPTIONS: opts.MinOpts() == %s\n", compiler.opts.MinOpts ? "true" : "false");
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
                // The VM sets JitFlag.JIT_FLAG_FRAMED for two reasons:
                //   1. the DOTNET_JitFramed variable is set, or
                //   2. the function is marked "noinline".
                //
                // The reason for #2 is that people mark functions noinline to ensure the show up on in a stack walk.
                // But for AMD64, we don't need a frame pointer for the frame to show up in stack walk.
                if (compiler.opts.jitFlags->IsSet(JitFlag.JIT_FLAG_FRAMED))
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
                    codeGen.ShouldAlignLoops = JitConfig[ConfigInteger.JitAlignLoops] == 1;
                }

#if DEBUG
                var tieringName = compiler.compGetTieringName(true);
                JitMetadata.report(compiler, JitMetadata.TieringName, tieringName);
#endif
            }
        }
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

    /// <summary>Answer the question: Is a particular ISA allowed to be used implicitly by optimizations?</summary>
    /// <param name="isa"></param>
    /// <returns></returns>
    /// <remarks>
    ///   <para>The result of this api call will match the target machine if the result is true.</para>
    ///   <para>If the result is false, then the target machine may have support for the instruction.</para>
    /// </remarks>
    private bool compOpportunisticallyDependsOn(CORINFO_InstructionSet isa)
        => opts.compSupportsISA.HasInstructionSet(isa) && compExactlyDependsOn(isa);

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
