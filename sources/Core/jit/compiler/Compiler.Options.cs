// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
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

#if TARGET_XARCH
        public int preferredVectorByteLength;
#endif

        public bool canUseTier0Opts;

        public bool canUseAllOpts;

        public bool compMinOpts;

        public bool compMinOptsIsSet;

#if DEBUG
        public bool compMinOptsIsUsed;

        public bool MinOpts
        {
            get
            {
                assert(compMinOptsIsSet);
                compMinOptsIsUsed = true;
                return compMinOpts;
            }
        }

#else
        public readonly bool MinOpts => compMinOpts;
#endif

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

        public unsafe void SetMinOpts(bool val)
        {
#if DEBUG
            assert(!compMinOptsIsUsed);
            assert(!compMinOptsIsSet || (compMinOpts == val));
#endif

            compMinOpts = val;
            compMinOptsIsSet = true;

            canUseTier0Opts = !compDbgCode && !jitFlags->IsSet(JitFlags.JIT_FLAG_MIN_OPT);
            canUseAllOpts = canUseTier0Opts && !val;
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
#if TARGET_ARM64 || TARGET_AMD64
                // On these platforms we assume the register that the target is passed in is preserved by the validator and take care to get the target from the register for the call (even in debug mode).
                // RBM_INT_CALLEE_TRASH is not known at compile time on TARGET_AMD64 since it's dependent on APX support.
#if TARGET_AMD64
                assert((RBM_VALIDATE_INDIRECT_CALL_TRASH_ALL & RBM_VALIDATE_INDIRECT_CALL_ADDR) == RBM_NONE);
#else
                assert((RBM_VALIDATE_INDIRECT_CALL_TRASH & RBM_VALIDATE_INDIRECT_CALL_ADDR) == RBM_NONE);
#endif
                if (JitConfig.JitForceControlFlowGuard != 0)
                {
                    return true;
                }
                return jitFlags->IsSet(JitFlags.JIT_FLAG_ENABLE_CFG);
#else
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
#endif
            }
        }

#if FEATURE_ON_STACK_REPLACEMENT
        public readonly unsafe bool IsOSR => jitFlags->IsSet(JitFlags.JIT_FLAG_OSR);
#else
        public readonly bool IsOSR => false;
#endif

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

#if PROFILING_SUPPORTED
        public bool compNoPInvokeInlineCB;
#else
        public const bool compNoPInvokeInlineCB;
#endif

#if DEBUG
        /// <summary>Check arguments and return values to ensure they are sane</summary>
        public bool compGcChecks;
#endif

#if DEBUG && TARGET_XARCH
        /// <summary>Check stack pointer on return to ensure it is correct.</summary>
        public bool compStackCheckOnRet;
#endif

#if DEBUG && TARGET_X86
        /// <summary>Check stack pointer after call to ensure it is correct. Only for x86.</summary>
        public bool compStackCheckOnCall;
#endif

        /// <summary>Generate relocs for pointers in code, true for all AOT codegen</summary>
        public bool compReloc;

#if DEBUG && (TARGET_XARCH || TARGET_RISCV64)
        /// <summary>Whether absolute addr be encoded as PC-rel offset by RyuJIT where possible</summary>
        public bool compEnablePCRelAddr;
#endif

#if UNIX_AMD64_ABI
        /// <summary>This flag is indicating if there is a need to align the frame.</summary>
        /// <remarks>
        ///   <para>On AMD64-Windows, if there are calls, 4 slots for the outgoing ars are allocated, except for FastTailCall.</para>
        ///   <para>This slots makes the frame size non-zero, so alignment logic will be called.</para>
        ///   <para>On AMD64-Unix, there are no such slots, but there is a possibility to have calls in the method with frame size of 0 and so the frame alignment logic won't kick in.</para>
        ///   <para>This flags takes care of the AMD64-Unix case by remembering that there are calls and making sure the frame alignment logic is executed.</para>
        /// </remarks>
        public bool compNeedToAlignFrame;
#endif

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

#if DEBUG
        /// <summary>Separate cold code from hot code for functions with EH</summary>
        public bool compProcedureSplittingEH;

        /// <summary>Display native code generated</summary>
        public bool dspCode;

        /// <summary>Display the EH table reported to the VM</summary>
        public bool dspEHTable;

        /// <summary>Display the Debug info reported to the VM</summary>
        public bool dspDebugInfo;

        /// <summary>Display the IL instructions intermixed with the native code output</summary>
        public bool dspInstrs;

        /// <summary>Display source-code lines intermixed with native code output</summary>
        public bool dspLines;

        /// <summary>Display variables names in native code output</summary>
        public bool varNames;

        /// <summary>Display native code when any register spilling occurs</summary>
        public bool disAsmSpilled;

        /// <summary>Display GC info interleaved with disassembly.</summary>
        public bool disasmWithGC;

        /// <summary>Display process address next to each instruction in disassembly code</summary>
        public bool disAddr;

        /// <summary>Display native code after it is generated using external disassembler</summary>
        public bool disAsm2;

        /// <summary>Display names of each of the methods that we compile</summary>
        public bool dspOrder;

        /// <summary>Display the unwind info output</summary>
        public bool dspUnwind;

        /// <summary>Force using large pseudo instructions for long address (IF_LARGEJMP/IF_LARGEADR/IF_LARGLDC)</summary>
        public bool compLongAddress;

        /// <summary>Display the GC tables</summary>
        public bool dspGCtbls;

        /// <summary>Display metrics</summary>
        public bool dspMetrics;

        /// <summary>If set, for non-adaptive alignment, ensure loop jmps are not on or cross alignment boundary.</summary>
        public bool compJitAlignLoopForJcc;
#endif

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

#if LATE_DISASM
        /// <summary>Run the late disassembler</summary>
        public bool doLateDisasm;
#endif

#if DUMP_GC_TABLES && !DEBUG
#warning NOTE: this non-debug build has GC ptr table dumping always enabled!
        public const bool dspGCtbls = true;
#endif

#if PROFILING_SUPPORTED
        /// <summary>Whether to emit Enter/Leave/TailCall hooks using a dummy stub (DummyProfilerELTStub()).</summary>
        /// <remarks>This option helps make the JIT behave as if it is running under a profiler.</remarks>
        public bool compJitELTHookEnabled;
#endif

#if FEATURE_TAILCALL_OPT
        /// <summary>Whether opportunistic or implicit tail call optimization is enabled.</summary>
        public bool compTailCallOpt;

        /// <summary>Whether optimization of transforming a recursive tail call into a loop is enabled.</summary>
        public bool compTailCallLoopOpt;
#endif

#if FEATURE_FASTTAILCALL
        /// <summary>Whether fast tail calls are allowed.</summary>
        bool compFastTailCalls;
#endif

#if TARGET_ARM64
        /// <summary>Decision about whether to save FP/LR registers with callee-saved registers (see DOTNET_JitSaveFpLrWithCalleSavedRegisters).</summary>
        int compJitSaveFpLrWithCalleeSavedRegisters;
#endif

#if CONFIGURABLE_ARM_ABI
        public bool compUseSoftFP;
#elif ARM_SOFTFP
        public const bool compUseSoftFP = true;
#else
        public const bool compUseSoftFP = false;
#endif

        /// <summary>Collect 64 bit counts for PGO data.</summary>
        public bool compCollect64BitCounts;

        /// <summary>Allow inlining of methods with EH.</summary>
        public bool compInlineMethodsWithEH;
    }
}
