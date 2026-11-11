// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime. Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private unsafe void genFuncletPrologLoongArch64(BasicBlock block)
    {
        if (Configuration.DEBUG && _verbose)
        {
            jitprintf("*************** In genFuncletProlog()\n");
        }

        assert(block is not null);
        assert(_compiler.bbIsFuncletBeg(block));

        GCInfo.gcResetForBB();
        _compiler.unwindBegProlog();

        genStackPointerAdjustment(
            -Configuration.STACK_ALIGN,
            REG_LOONGARCH64_R21,
            null,
            reportUnwindData: true);

        Emitter.emitIns_R_R_I(
            INS_st_d,
            EA_PTRSIZE,
            REG_LOONGARCH64_RA,
            REG_SPBASE_LOONGARCH64,
            Configuration.REGSIZE_BYTES);
        _compiler.unwindSaveReg(REG_LOONGARCH64_RA, Configuration.REGSIZE_BYTES);

        if (genFuncletInfo.fiSpDelta > 0)
        {
            genStackPointerAdjustment(
                unchecked(-unchecked((int)genFuncletInfo.fiSpDelta)),
                REG_LOONGARCH64_R21,
                null,
                reportUnwindData: true);
        }

        _compiler.unwindEndProlog();
    }

    private unsafe void genFuncletEpilogLoongArch64()
    {
        if (Configuration.DEBUG && _verbose)
        {
            jitprintf("*************** In genFuncletEpilog()\n");
        }

        _compiler.unwindBegEpilog();

        if (genFuncletInfo.fiSpDelta > 0)
        {
            genStackPointerAdjustment(
                unchecked((int)genFuncletInfo.fiSpDelta),
                REG_LOONGARCH64_R21,
                null,
                reportUnwindData: true);
        }

        Emitter.emitIns_R_R_I(
            INS_ld_d,
            EA_PTRSIZE,
            REG_LOONGARCH64_RA,
            REG_SPBASE_LOONGARCH64,
            Configuration.REGSIZE_BYTES);
        _compiler.unwindSaveReg(REG_LOONGARCH64_RA, Configuration.REGSIZE_BYTES);

        genStackPointerAdjustment(
            Configuration.STACK_ALIGN,
            REG_LOONGARCH64_R21,
            null,
            reportUnwindData: true);

        Emitter.emitIns_R_R_I(INS_jirl, TYP_I_IMPL.EmitActualSize, REG_LOONGARCH64_R0, REG_LOONGARCH64_RA, 0);
        _compiler.unwindReturn(REG_LOONGARCH64_RA);

        _compiler.unwindEndEpilog();
    }

    private void genCaptureFuncletPrologEpilogInfoLoongArch64()
    {
        if (_compiler.compHndBBtabCount == 0)
        {
            return;
        }

        assert(IsFramePointerUsed);
        assert(_compiler.lvaDoneFrameLayout == Compiler.FINAL_FRAME_LAYOUT);

        noway_assert(_compiler.lvaOutgoingArgSpaceSize.Value >= 0);
        var outgoingArgSpaceSize = unchecked((uint)_compiler.lvaOutgoingArgSpaceSize.Value);
        assert((outgoingArgSpaceSize % Configuration.REGSIZE_BYTES) == 0);
        genFuncletInfo.fiSpDelta = roundUp(outgoingArgSpaceSize, (uint)Configuration.STACK_ALIGN);

        if (Configuration.DEBUG)
        {
            if (_verbose)
            {
                jitprintf("\n");
                jitprintf("Funclet prolog / epilog info\n");
                jitprintf($"              Outgoing arg space: {genFuncletInfo.fiSpDelta}\n");
            }
        }
    }
}
