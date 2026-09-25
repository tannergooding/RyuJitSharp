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
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Funclet prologs require Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
#if DEBUG
        if (_verbose)
        {
            jitprintf("*************** In genFuncletProlog()\n");
        }
#endif
        assert(!_regSet.rsRegsModified(new regMaskTP(SRBM_FPBASE)));
        assert(_compiler.bbIsFuncletBeg(block));
        assert(IsFramePointerUsed);

        GCInfo.gcResetForBB();
        _compiler.unwindBegProlog();

        var liveIn = block.CatchType is BBCT_FINALLY or BBCT_FAULT
            ? new regMaskTP(SRBM_ARG_0)
            : new regMaskTP(SRBM_ARG_0 | SRBM_ARG_2);
        var initRegZeroed = false;
        genAllocLclFrame(genFuncletInfo.fiSpDelta, REG_NA, ref initRegZeroed, liveIn);

        _compiler.unwindEndProlog();
        genClearAvxStateInProlog();
#endif
    }

    public void genFuncletEpilog(BasicBlock block)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Funclet epilogs require Windows AMD64.");
#else
        Emitter.RequireSupportedInstructionRecording();
#if DEBUG
        if (_verbose)
        {
            jitprintf("*************** In genFuncletEpilog()\n");
        }
#endif
        genClearAvxStateInEpilog();
        inst_RV_IV(INS_add, REG_SPBASE, (nint)genFuncletInfo.fiSpDelta, EA_PTRSIZE);
        instGen_Return(0);
#endif
    }

    public void genCaptureFuncletPrologEpilogInfo()
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Funclet frame capture requires Windows AMD64.");
#else
        if (_compiler.compHndBBtabCount == 0)
        {
            return;
        }

        assert(IsFramePointerUsed);
        assert(_compiler.lvaDoneFrameLayout == Compiler.FINAL_FRAME_LAYOUT);
        noway_assert(_compiler.lvaOutgoingArgSpaceSize.Value >= 0);
        var outgoing = unchecked((uint)_compiler.lvaOutgoingArgSpaceSize.Value);
        assert(outgoing % REGSIZE_BYTES == 0);
        assert(outgoing == 0 || outgoing >= 4 * REGSIZE_BYTES);

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
    }
}
