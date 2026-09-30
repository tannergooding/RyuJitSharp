// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private struct FuncletPrologEpilogInfo
    {
        public uint fiSpDelta;
    }

    private FuncletPrologEpilogInfo genFuncletInfo;

    public void genFuncletProlog(BasicBlock block)
    {
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Funclet prologs require xarch.");
#else
#if TARGET_AMD64
        Emitter.RequireSupportedInstructionRecording();
#endif
#if DEBUG
        if (_verbose)
        {
            jitprintf("*************** In genFuncletProlog()\n");
        }
#endif
#if TARGET_AMD64
        assert(!_regSet.rsRegsModified(new regMaskTP(SRBM_FPBASE)));
        assert(_compiler.bbIsFuncletBeg(block));
        assert(IsFramePointerUsed);
#endif

        GCInfo.gcResetForBB();
        _compiler.unwindBegProlog();

#if TARGET_AMD64
        var liveIn = block.CatchType is BBCT_FINALLY or BBCT_FAULT
            ? new regMaskTP(SRBM_ARG_0)
            : new regMaskTP(SRBM_ARG_0 | SRBM_ARG_2);
        var initRegZeroed = false;
        genAllocLclFrame(genFuncletInfo.fiSpDelta, REG_NA, ref initRegZeroed, liveIn);
#endif

        _compiler.unwindEndProlog();
#if TARGET_X86
#if UNIX_X86_ABI
        inst_RV_IV(INS_sub, REG_SPBASE, 12, EA_PTRSIZE);
#else
        if (!_compiler.IsTargetAbi(CORINFO_NATIVEAOT_ABI))
        {
            instGen(INS_nop);
        }
#endif
#endif
        genClearAvxStateInProlog();
#endif
    }

    public void genFuncletEpilog(BasicBlock block)
    {
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Funclet epilogs require xarch.");
#else
#if TARGET_AMD64
        Emitter.RequireSupportedInstructionRecording();
#endif
#if DEBUG
        if (_verbose)
        {
            jitprintf("*************** In genFuncletEpilog()\n");
        }
#endif
        genClearAvxStateInEpilog();
#if TARGET_AMD64
        inst_RV_IV(INS_add, REG_SPBASE, (nint)genFuncletInfo.fiSpDelta, EA_PTRSIZE);
#elif UNIX_X86_ABI
        inst_RV_IV(INS_add, REG_SPBASE, 12, EA_PTRSIZE);
#endif
        instGen_Return(0);
#endif
    }

    public void genCaptureFuncletPrologEpilogInfo()
    {
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Funclet frame capture requires xarch.");
#else
        // Native ehAnyFunclets() is compHndBBtabCount > 0.
        if (_compiler.compHndBBtabCount == 0)
        {
            return;
        }

#if TARGET_AMD64
        assert(IsFramePointerUsed);
        assert(_compiler.lvaDoneFrameLayout == Compiler.FINAL_FRAME_LAYOUT);
        noway_assert(_compiler.lvaOutgoingArgSpaceSize.Value >= 0);
        var outgoing = unchecked((uint)_compiler.lvaOutgoingArgSpaceSize.Value);
        assert(outgoing % REGSIZE_BYTES == 0);
#if WINDOWS_AMD64_ABI
        assert(outgoing == 0 || outgoing >= 4 * REGSIZE_BYTES);
#endif

        var totalFrameSize = REGSIZE_BYTES + outgoing;
        var padding = (16 - (totalFrameSize % 16)) % 16;
        genFuncletInfo.fiSpDelta = padding + outgoing;

#if DEBUG
        if (_verbose)
        {
            jitprintf($"\nFunclet prolog / epilog info\n                         SP delta: {genFuncletInfo.fiSpDelta}\n");
        }
#endif
#endif
#endif
    }
}
