// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.CORINFO_RUNTIME_ABI;
using static RyuJitSharp.JitFlags.JitFlag;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    // Output locations are borrowed for the synchronous generation invocation.
    private unsafe void** _codePtr;
    private unsafe int* _nativeSizeOfCode;
    private unsafe void* _codePtrRW;
    private unsafe void* _coldCodePtr;
    private unsafe void* _coldCodePtrRW;
    private uint _codeSize;
    private uint _prologSize;
    private uint _epilogSize;

    internal unsafe void genGenerateMachineCode()
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Machine-code generation requires Windows AMD64.");
#else
#if DEBUG
        _genInterruptibleUsed = true;
        _compiler.fgDebugCheckBBlist();
#endif
        genPrepForCompiler();
        Emitter.Init();

#if DEBUG
        if (_compiler.opts.disAsmSpilled && RegSet.NeededSpillReg)
        {
            _compiler.opts.disAsm = true;
        }
#endif
        _compiler.compCurBB = _compiler.fgFirstBB;
        if (_compiler.opts.disAsm)
        {
#if DEBUG
            var fullName = _compiler.info.compFullName;
#else
            var fullName = _compiler.eeGetMethodFullName(_compiler.info.compMethodHnd);
#endif
            jitprintf($"; Assembly listing for method {fullName} ({_compiler.compGetTieringName(true)})\n");
            jitprintf("; Emitting ");
            if (_compiler.compCodeOpt == Compiler.SMALL_CODE)
            {
                jitprintf("SMALL_CODE");
            }
            else if (_compiler.compCodeOpt == Compiler.FAST_CODE)
            {
                jitprintf("FAST_CODE");
            }
            else
            {
                jitprintf("BLENDED_CODE");
            }
            jitprintf($" for {Target.TgtCpuName}");

            // Inspect the selected ISA directly; opportunistic dependency queries
            // here would add JIT/EE calls to diagnostic-only execution.
            if (_compiler.opts.compSupportsISA.HasInstructionSet(InstructionSet_AVX))
            {
                jitprintf(" + VEX");
            }
            if (_compiler.opts.compSupportsISA.HasInstructionSet(InstructionSet_AVX512))
            {
                jitprintf(" + EVEX");
            }
            if (_compiler.opts.compSupportsISA.HasInstructionSet(InstructionSet_APX))
            {
                jitprintf(" + APX");
            }
            if (TargetOS.IsWindows)
            {
                jitprintf(" on Windows");
            }
            else if (TargetOS.IsApplePlatform)
            {
                jitprintf(" on Apple");
            }
            else if (TargetOS.IsUnix)
            {
                jitprintf(" on Unix");
            }
            jitprintf($"\n; {_compiler.compGetTieringName(false)} code\n");

            if (_compiler.IsAot)
            {
                jitprintf(_compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI)
                    ? "; NativeAOT compilation\n"
                    : "; ReadyToRun compilation\n");
            }
            if (_compiler.opts.IsOSR)
            {
                jitprintf($"; OSR variant for entry point 0x{_compiler.info.compILEntry:x}\n");
            }
            if (_compiler.compIsAsync)
            {
                jitprintf("; async\n");
            }
            if ((_compiler.opts.compFlags & CLFLG_MAXOPT) == CLFLG_MAXOPT)
            {
                jitprintf("; optimized code\n");
            }
            else if (_compiler.opts.compDbgEnC)
            {
                jitprintf("; EnC code\n");
            }
            else if (_compiler.opts.compDbgCode)
            {
                jitprintf("; debuggable code\n");
            }
            if (_compiler.opts.jitFlags->IsSet(JIT_FLAG_BBOPT) && _compiler.fgHaveProfileWeights)
            {
                jitprintf($"; optimized using {_compiler.compPgoSourceName}\n");
            }
            jitprintf($"; {(IsFramePointerUsed ? STR_FPBASE : STR_SPBASE)} based frame\n");
            jitprintf(Interruptible ? "; fully interruptible\n" : "; partially interruptible\n");

            if (_compiler.fgHaveProfileWeights)
            {
                jitprintf($"; with {_compiler.compPgoSourceName}: fgCalledCount is {FMT_WT(_compiler.fgCalledCount)}\n");
            }
            if (_compiler.fgPgoFailReason is not null)
            {
                jitprintf($"; {_compiler.fgPgoFailReason}\n");
            }
            if ((_compiler.fgPgoInlineePgo + _compiler.fgPgoInlineeNoPgo + _compiler.fgPgoInlineeNoPgoSingleBlock) > 0)
            {
                jitprintf($"; {_compiler.fgPgoInlineePgo} inlinees with PGO data; {_compiler.fgPgoInlineeNoPgoSingleBlock} single block inlinees; {_compiler.fgPgoInlineeNoPgo} inlinees without PGO data\n");
            }
            if (_compiler.opts.IsCFGEnabled)
            {
                jitprintf("; control-flow guard enabled\n");
            }
            if (_compiler.opts.jitFlags->IsSet(JIT_FLAG_ALT_JIT))
            {
                jitprintf("; invoked as altjit\n");
            }
        }

        // LSRA has computed the exact concurrent spill requirements, so finalize
        // the frame before recording instructions rather than estimating it.
        genFinalizeFrame();
        Emitter.emitBegFN(IsFramePointerUsed
#if DEBUG
            , (_compiler.compCodeOpt != Compiler.SMALL_CODE) && !_compiler.IsAot
#endif
            );
        genCodeForBBlist();
#if DEBUG
        if (_verbose)
        {
            _compiler.lvaTableDump();
        }
#endif
        genGeneratePrologsAndEpilogs();
        Emitter.emitRemoveJumpToNextInst();
        Emitter.emitJumpDistBind();
#if FEATURE_LOOP_ALIGN
        Emitter.emitLoopAlignAdjustments();
#endif
#endif
    }

    internal unsafe void genEmitMachineCode()
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Machine-code emission requires Windows AMD64.");
#else
#if DEBUG
        // The CSE policy constructors and DumpMetrics hierarchy are not ported.
        // Reject this native diagnostic mode before reserving or allocating memory.
        if (_compiler.opts.dspMetrics)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Emission with CSE metrics is not implemented.");
        }
#endif
        assert(_codePtr is not null);
        assert(_nativeSizeOfCode is not null);
        Emitter.emitComputeCodeSizes();

#if DEBUG
        if (!_compiler.jitFallbackCompile && (JitConfig.JitNoForceFallback == 0))
        {
            if ((JitConfig.JitForceFallback != 0) ||
                _compiler.compStressCompile(Compiler.compStressArea.STRESS_GENERIC_VARN, 5))
            {
                JITDUMP("\n\n*** forcing no-way fallback -- current jit request will be abandoned ***\n\n");
                NO_WAY_NOASSERT("Stress failure");
            }
        }
        uint instrCount;
#endif
        _compiler.unwindReserve();
        if (_compiler.opts.disAsm && _compiler.opts.disTesting)
        {
            jitprintf($"; BEGIN METHOD {_compiler.eeGetMethodFullName(_compiler.info.compMethodHnd)}\n");
        }

        fixed (uint* prologSize = &_prologSize, epilogSize = &_epilogSize)
        fixed (void** codePtrRW = &_codePtrRW, coldCodePtr = &_coldCodePtr, coldCodePtrRW = &_coldCodePtrRW)
        {
            _codeSize = Emitter.emitEndCodeGen(_compiler, false, Interruptible, IsFullPtrRegMapRequired,
                _compiler.compHndBBtabCount, prologSize, epilogSize, _codePtr, codePtrRW, coldCodePtr, coldCodePtrRW
#if DEBUG
                , &instrCount
#endif
                );
        }
#if DEBUG
        assert(!_compiler.compCodeGenDone);
        _compiler.compCodeGenDone = true;
#endif
        if (_compiler.opts.disAsm && _compiler.opts.disTesting)
        {
            jitprintf($"; END METHOD {_compiler.eeGetMethodFullName(_compiler.info.compMethodHnd)}\n");
        }

#if DEBUG
        if (_compiler.opts.disAsm || _verbose)
        {
            jitprintf($"\n; Total bytes of code {_codeSize}, prolog size {_prologSize}, PerfScore {_compiler.Metrics.PerfScore:F2}, instruction count {instrCount}, allocated bytes for code {Emitter.emitTotalHotCodeSize + Emitter.emitTotalColdCodeSize}");
#if TRACK_LSRA_STATS
            if (JitConfig.DisplayLsraStats == 3)
            {
                var allocator = _compiler.RegisterAllocator;
                assert(allocator is not null);
                allocator.dumpLsraStatsSummary(jitstdout());
            }
#endif
            jitprintf($" (MethodHash={_compiler.info.compMethodHash():x8}) for method {_compiler.info.compFullName} ({_compiler.compGetTieringName(true)})\n");
            jitprintf("; ============================================================\n\n");
            jitstdout().Flush();
        }
        if (_verbose)
        {
            jitprintf("*************** After end code gen, before unwindEmit()\n");
            Emitter.emitDispIGlist(displayInstructions: true);
        }
#else
        if (_compiler.opts.disAsm)
        {
            jitprintf($"\n; Total bytes of code {_codeSize} for method {_compiler.eeGetMethodFullName(_compiler.info.compMethodHnd)} ({_compiler.compGetTieringName(true)})\n\n");
        }
#endif
        *_nativeSizeOfCode = unchecked((int)_codeSize);
        _compiler.info.compNativeCodeSize = unchecked((int)_codeSize);
#endif
    }
}
