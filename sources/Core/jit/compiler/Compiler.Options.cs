// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public bool MinOpts
    {
        get
        {
            assert(opts.compMinOptsIsSet);
            if (Configuration.DEBUG)
            {
                GetDebugData().CompMinOptsIsUsed = true;
            }

            return opts.compMinOpts;
        }
    }

    public unsafe void SetMinOpts(bool val)
    {
        if (Configuration.DEBUG)
        {
            assert(!GetDebugData().CompMinOptsIsUsed);
            assert(!opts.compMinOptsIsSet || (opts.compMinOpts == val));
        }

        opts.compMinOpts = val;
        opts.compMinOptsIsSet = true;
        opts.canUseTier0Opts = !opts.compDbgCode && !opts.jitFlags->IsSet(JitFlags.JIT_FLAG_MIN_OPT);
        opts.canUseAllOpts = opts.canUseTier0Opts && !val;
    }

    public bool compAllowStress
    {
        get
        {
            return Configuration.DEBUG && GetDebugData().CompAllowStress;
        }

        set
        {
            if (Configuration.DEBUG)
            {
                GetDebugData().CompAllowStress = value;
            }
        }
    }

    internal bool compGcChecks
    {
        get
        {
            return Configuration.DEBUG && GetDebugData().CompGcChecks;
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.CompilerDebugDataIsOnlyAvailableInDebugBuilds);
            }

            GetDebugData().CompGcChecks = value;
        }
    }

    internal bool compEnablePCRelAddr
    {
        get
        {
            return Configuration.DEBUG && GetDebugData().CompEnablePCRelAddr;
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.CompilerDebugDataIsOnlyAvailableInDebugBuilds);
            }

            GetDebugData().CompEnablePCRelAddr = value;
        }
    }

    internal bool compProcedureSplittingEH
    {
        get
        {
            return Configuration.DEBUG && GetDebugData().CompProcedureSplittingEH;
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.CompilerDebugDataIsOnlyAvailableInDebugBuilds);
            }

            GetDebugData().CompProcedureSplittingEH = value;
        }
    }

    internal bool dspCode
    {
        get
        {
            return Configuration.DEBUG && GetDebugData().DspCode;
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.CompilerDisplayOptionsAreOnlyAvailableInDebugBuilds);
            }

            GetDebugData().DspCode = value;
        }
    }

    internal bool dspEHTable
    {
        get
        {
            return Configuration.DEBUG && GetDebugData().DspEHTable;
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.CompilerDisplayOptionsAreOnlyAvailableInDebugBuilds);
            }

            GetDebugData().DspEHTable = value;
        }
    }

    internal bool dspDebugInfo
    {
        get
        {
            return Configuration.DEBUG && GetDebugData().DspDebugInfo;
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.CompilerDisplayOptionsAreOnlyAvailableInDebugBuilds);
            }

            GetDebugData().DspDebugInfo = value;
        }
    }

    internal bool dspInstrs
    {
        get
        {
            return Configuration.DEBUG && GetDebugData().DspInstrs;
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.CompilerDisplayOptionsAreOnlyAvailableInDebugBuilds);
            }

            GetDebugData().DspInstrs = value;
        }
    }

    internal bool dspLines
    {
        get
        {
            return Configuration.DEBUG && GetDebugData().DspLines;
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.CompilerDisplayOptionsAreOnlyAvailableInDebugBuilds);
            }

            GetDebugData().DspLines = value;
        }
    }

    internal bool varNames
    {
        get
        {
            return Configuration.DEBUG && GetDebugData().VarNames;
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.CompilerDisplayOptionsAreOnlyAvailableInDebugBuilds);
            }

            GetDebugData().VarNames = value;
        }
    }

    internal bool disAsmSpilled
    {
        get
        {
            return Configuration.DEBUG && GetDebugData().DisAsmSpilled;
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.CompilerDisplayOptionsAreOnlyAvailableInDebugBuilds);
            }

            GetDebugData().DisAsmSpilled = value;
        }
    }

    internal bool disasmWithGC
    {
        get
        {
            return Configuration.DEBUG && GetDebugData().DisasmWithGC;
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.CompilerDisplayOptionsAreOnlyAvailableInDebugBuilds);
            }

            GetDebugData().DisasmWithGC = value;
        }
    }

    internal bool disAddr
    {
        get
        {
            return Configuration.DEBUG && GetDebugData().DisAddr;
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.CompilerDisplayOptionsAreOnlyAvailableInDebugBuilds);
            }

            GetDebugData().DisAddr = value;
        }
    }

    internal bool disAsm2
    {
        get
        {
            return Configuration.DEBUG && GetDebugData().DisAsm2;
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.CompilerDisplayOptionsAreOnlyAvailableInDebugBuilds);
            }

            GetDebugData().DisAsm2 = value;
        }
    }

    internal bool dspOrder
    {
        get
        {
            return Configuration.DEBUG && GetDebugData().DspOrder;
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.CompilerDisplayOptionsAreOnlyAvailableInDebugBuilds);
            }

            GetDebugData().DspOrder = value;
        }
    }

    internal bool dspUnwind
    {
        get
        {
            return Configuration.DEBUG && GetDebugData().DspUnwind;
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.CompilerDisplayOptionsAreOnlyAvailableInDebugBuilds);
            }

            GetDebugData().DspUnwind = value;
        }
    }

    internal bool compLongAddress
    {
        get
        {
            return Configuration.DEBUG && GetDebugData().CompLongAddress;
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.CompilerDebugDataIsOnlyAvailableInDebugBuilds);
            }

            GetDebugData().CompLongAddress = value;
        }
    }

    internal bool dspGCtbls
    {
        get
        {
            return (Configuration.DUMP_GC_TABLES && !Configuration.DEBUG) ||
                (Configuration.DEBUG && GetDebugData().DspGCtbls);
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.CompilerDisplayOptionsAreOnlyAvailableInDebugBuilds);
            }

            GetDebugData().DspGCtbls = value;
        }
    }

    internal bool dspMetrics
    {
        get
        {
            return Configuration.DEBUG && GetDebugData().DspMetrics;
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.CompilerDisplayOptionsAreOnlyAvailableInDebugBuilds);
            }

            GetDebugData().DspMetrics = value;
        }
    }

    internal bool compJitAlignLoopForJcc
    {
        get
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.LoopAlignmentDiagnosticOptionsAreOnlyAvailableInDebugBuilds);
            }

            return GetDebugData().CompJitAlignLoopForJcc;
        }

        set
        {
            if (!Configuration.DEBUG)
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.LoopAlignmentDiagnosticOptionsAreOnlyAvailableInDebugBuilds);
            }

            GetDebugData().CompJitAlignLoopForJcc = value;
        }
    }

    public struct Options
    {
        /// <summary>All flags passed from the EE</summary>
        public unsafe JitFlags* jitFlags;

        // The instruction sets that the compiler is allowed to emit.
        public CORINFO_InstructionSetFlags compSupportsISA;

        // The instruction sets that were reported to the VM as being used by the current method. Subset of compSupportsISA.
        public CORINFO_InstructionSetFlags compSupportsISAReported;

        // The instruction sets that the compiler is allowed to take advantage of implicitly during optimizations.
        // Subset of compSupportsISA.
        // The instruction sets available in compSupportsISA and not available in compSupportsISAExactly can be only used via explicit hardware intrinsics.
        public CORINFO_InstructionSetFlags compSupportsISAExactly;

        public void setSupportedISAs(CORINFO_InstructionSetFlags isas)
        {
            compSupportsISA = isas;
        }

        /// <summary>method attributes</summary>
        public int compFlags;          

        /// <summary>number of IL opcodes</summary>
        public int instrCount;

        /// <summary>number of IL opcodes (calls only).</summary>
        public int callInstrCount;

        public int lvRefCount;

        /// <summary>what type of code optimizations</summary>
        public codeOptimize compCodeOpt;

        public int preferredVectorByteLength;

        public bool canUseTier0Opts;

        public bool canUseAllOpts;

        public bool compMinOpts;

        public bool compMinOptsIsSet;

        public readonly bool IsMinOptsSet => compMinOptsIsSet;

        // TODO: we should convert these into a single OptimizationLevel

        public readonly bool OptimizationDisabled
        {
            get
            {
                assert(compMinOptsIsSet);
                return !canUseAllOpts;
            }
        }

        public readonly bool OptimizationEnabled
        {
            get
            {
                assert(compMinOptsIsSet);
                return canUseAllOpts;
            }
        }
        public readonly bool Tier0OptimizationEnabled
        {
            get
            {
                assert(compMinOptsIsSet);
                return canUseTier0Opts;
            }
        }

        /// <summary>true if the CLFLG_* for an optimization is set</summary>
        /// <param name="optFlag"></param>
        /// <returns></returns>
        public readonly bool OptEnabled(int optFlag) => (compFlags & optFlag) != 0;

        // Check if the compilation is control-flow guard enabled.
        public readonly unsafe bool IsCFGEnabled
        {
            get
            {
                if (Configuration.TARGET_ARM64 || Configuration.TARGET_AMD64)
                {
                    // On these platforms we assume the register that the target is passed in is preserved by the validator and take care to get the target from the register for the call (even in debug mode).
                    // SRBM_INT_CALLEE_TRASH is not known at compile time on TARGET_AMD64 since it's dependent on APX support.
                    if (Configuration.TARGET_AMD64)
                    {
                        assert((SRBM_VALIDATE_INDIRECT_CALL_TRASH_ALL & SRBM_VALIDATE_INDIRECT_CALL_ADDR) == SRBM_NONE);
                    }
                    else
                    {
                        assert((SRBM_VALIDATE_INDIRECT_CALL_TRASH_ARM64 &
                            genSingleTypeRegMask(REG_VALIDATE_INDIRECT_CALL_ADDR_ARM64)) == SRBM_NONE);
                    }

                    if (JitConfig.JitForceControlFlowGuard != 0)
                    {
                        return true;
                    }

                    return jitFlags->IsSet(JitFlags.JIT_FLAG_ENABLE_CFG);
                }

                // The remaining platforms are not supported and would require some
                // work to support.
                //
                // ARM32:
                //   The ARM32 validator does not preserve any volatile registers
                //   which means we have to take special care to allocate and use a
                //   callee-saved register (reloading the target from memory is a
                //   security issue).
                //
                // x86:
                //   On x86 some VSD calls disassemble the call site and expect an
                //   indirect call which is fundamentally incompatible with CFG.
                //   This would require a different way to pass this information
                //   through.
                //
                return false;
            }
        }

        public readonly unsafe bool IsOSR =>
            Configuration.FEATURE_ON_STACK_REPLACEMENT && jitFlags->IsSet(JitFlags.JIT_FLAG_OSR);

        public readonly unsafe bool IsTier0 => jitFlags->IsSet(JitFlags.JIT_FLAG_TIER0);

        public readonly unsafe bool IsInstrumented => jitFlags->IsSet(JitFlags.JIT_FLAG_BBINSTR);

        public readonly unsafe bool IsOptimizedWithProfile => OptimizationEnabled && jitFlags->IsSet(JitFlags.JIT_FLAG_BBOPT);

        public readonly unsafe bool IsInstrumentedAndOptimized => IsInstrumented && jitFlags->IsSet(JitFlags.JIT_FLAG_BBOPT);

        public readonly unsafe bool DoEarlyBlockMerging => !jitFlags->IsSet(JitFlags.JIT_FLAG_DEBUG_EnC)
                                                        && !jitFlags->IsSet(JitFlags.JIT_FLAG_DEBUG_CODE)
                                                        && (!jitFlags->IsSet(JitFlags.JIT_FLAG_MIN_OPT) || jitFlags->IsSet(JitFlags.JIT_FLAG_TIER0));

        /// <summary>true if we should use the PINVOKE_{BEGIN,END} helpers instead of generating PInvoke transitions inline.</summary>
        /// <remarks>Normally used by R2R, but also used when generating a reverse pinvoke frame, as the current logic for frame setup initializes and pushes the InlinedCallFrame before performing the Reverse PInvoke transition, which is invalid (as frames cannot safely be pushed/popped while the thread is in a preemptive state.).</remarks>
        public readonly unsafe bool ShouldUsePInvokeHelpers => jitFlags->IsSet(JitFlags.JIT_FLAG_USE_PINVOKE_HELPERS)
                                                            || jitFlags->IsSet(JitFlags.JIT_FLAG_REVERSE_PINVOKE);

        // true if the JIT should use helpers for interface dispatch
        // instead of virtual stub dispatch
        public readonly unsafe bool ShouldUseDispatchHelpers => jitFlags->IsSet(JitFlags.JIT_FLAG_USE_DISPATCH_HELPERS);

        /// <summary>true if we should use insert the REVERSE_PINVOKE_{ENTER,EXIT} helpers in the method prolog/epilog</summary>
        public readonly unsafe bool IsReversePInvoke => jitFlags->IsSet(JitFlags.JIT_FLAG_REVERSE_PINVOKE);

        /// <summary>Generate the LocalVar info ?</summary>
        public bool compScopeInfo;

        /// <summary>Generate debugger-friendly code?</summary>
        public bool compDbgCode;

        /// <summary>Gather debugging info?</summary>
        public bool compDbgInfo;

        public bool compDbgEnC;

        public bool compNoPInvokeInlineCB;

        /// <summary>Generate relocs for pointers in code, true for all AOT codegen</summary>
        public bool compReloc;

        /// <summary>This flag is indicating if there is a need to align the frame.</summary>
        /// <remarks>
        ///   <para>On AMD64-Windows, if there are calls, 4 slots for the outgoing ars are allocated, except for FastTailCall.</para>
        ///   <para>This slots makes the frame size non-zero, so alignment logic will be called.</para>
        ///   <para>On AMD64-Unix, there are no such slots, but there is a possibility to have calls in the method with frame size of 0 and so the frame alignment logic won't kick in.</para>
        ///   <para>This flags takes care of the AMD64-Unix case by remembering that there are calls and making sure the frame alignment logic is executed.</para>
        /// </remarks>
        public bool compNeedToAlignFrame;

        /// <summary>Separate cold code from hot code</summary>
        public bool compProcedureSplitting;

        /// <summary>Preserve FP order (operations are non-commutative)</summary>
        public bool genFPorder; 

        /// <summary>Can we do frame-pointer-omission optimization?</summary>
        public bool genFPopt;   

        /// <summary>True if we are an altjit and are compiling this method</summary>
        public bool altJit;     

        /// <summary>Repeat optimizer phases k times</summary>
        public bool optRepeat;

        /// <summary>The current optRepeat iteration: from 0 to optRepeatCount.</summary>
        /// <remarks>
        ///   <para>optRepeatCount can be zero, in which case no optimizations in the set of repeated optimizations are performed.</para>
        ///   <para>optRepeatIteration will only be zero if optRepeatCount is zero.</para>
        /// </remarks>
        public int optRepeatIteration; 

        /// <summary>How many times to repeat. By default, comes from JitConfig.JitOptRepeatCount().</summary>
        public int optRepeatCount;     

        /// <summary>`true` if we are in the range of phases being repeated.</summary>
        public bool optRepeatActive;    

        /// <summary>Display native code as it is generated</summary>
        public bool disAsm;       

        /// <summary>Display BEGIN METHOD/END METHOD anchors for disasm testing</summary>
        public bool disTesting;   

        /// <summary>Makes the Jit Dump 'diff-able' (currently uses same DOTNET_* flag as disDiffable)</summary>
        public bool dspDiffable;  

        /// <summary>Makes the Disassembly code 'diff-able'</summary>
        public bool disDiffable;  

        /// <summary>Display alignment boundaries in disassembly code</summary>
        public bool disAlignment;

        /// <summary>Display instruction code bytes in disassembly code</summary>
        public bool disCodeBytes; 

        /// <summary>For non-adaptive alignment, minimum loop size (in bytes) for which alignment will be done.</summary>
        public ushort compJitAlignLoopMaxCodeSize;

        /// <summary>Minimum weight needed for the first block of a loop to make it a candidate for alignment.</summary>
        public ushort compJitAlignLoopMinBlockWeight;

        /// <summary>For non-adaptive alignment, address boundary (power of 2) at which loop alignment should be done.</summary>
        /// <remarks>By default, 32B.</remarks>
        public ushort compJitAlignLoopBoundary;

        /// <summary>Padding limit to align a loop.</summary>
        public ushort compJitAlignPaddingLimit;

        /// <summary>If set, perform adaptive loop alignment that limits number of padding based on loop size.</summary>
        public bool compJitAlignLoopAdaptive;

        /// <summary>If set, tries to hide alignment instructions behind unconditional jumps.</summary>
        public bool compJitHideAlignBehindJmp;

        /// <summary>If set, tracks the hidden return buffer for struct arg.</summary>
        public bool compJitOptimizeStructHiddenBuffer;

        /// <summary>Iteration limit to unroll a loop.</summary>
        public ushort compJitUnrollLoopMaxIterationCount;

        /// <summary>Run the late disassembler</summary>
        public bool doLateDisasm;

        /// <summary>Whether to emit Enter/Leave/TailCall hooks using a dummy stub (DummyProfilerELTStub()).</summary>
        /// <remarks>This option helps make the JIT behave as if it is running under a profiler.</remarks>
        public bool compJitELTHookEnabled;

        /// <summary>Whether opportunistic or implicit tail call optimization is enabled.</summary>
        public bool compTailCallOpt;

        /// <summary>Whether optimization of transforming a recursive tail call into a loop is enabled.</summary>
        public bool compTailCallLoopOpt;

        /// <summary>Whether fast tail calls are allowed.</summary>
        public bool compFastTailCalls;

        /// <summary>Decision about whether to save FP/LR registers with callee-saved registers (see DOTNET_JitSaveFpLrWithCalleSavedRegisters).</summary>
        public int compJitSaveFpLrWithCalleeSavedRegisters;

        public bool compUseSoftFP;

        /// <summary>Collect 64 bit counts for PGO data.</summary>
        public bool compCollect64BitCounts;

        /// <summary>Allow inlining of methods with EH.</summary>
        public bool compInlineMethodsWithEH;
    }
}
