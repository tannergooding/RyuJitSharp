// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public partial class Emitter
{
    [Conditional("DEBUG")]
    private static void emitInsSanityCheckRiscV64(instrDesc id)
    {
    }

    public bool emitIns_MovRiscV64(
        instruction ins,
        emitAttr attr,
        regNumber dstReg,
        regNumber srcReg,
        bool canSkip,
        insOpts opt = insOpts.INS_OPTS_NONE)
    {
        if (canSkip && (dstReg == srcReg))
        {
            return false;
        }

        if ((attr == EA_4BYTE) && (ins == INS_mov))
        {
            assert(isGeneralRegisterOrR0(srcReg));
            assert(isGeneralRegisterOrR0(dstReg));
            emitIns_R_R(INS_sext_w, attr, dstReg, srcReg);
        }
        else if (ins is INS_fsgnj_s or INS_fsgnj_d)
        {
            assert(isFloatReg(srcReg));
            assert(isFloatReg(dstReg));
            emitIns_R_R_R(ins, attr, dstReg, srcReg, srcReg);
        }
        else if (genIsValidFloatReg(srcReg) || genIsValidFloatReg(dstReg))
        {
            emitIns_R_R(ins, attr, dstReg, srcReg);
        }
        else
        {
            assert(isGeneralRegisterOrR0(srcReg));
            assert(isGeneralRegisterOrR0(dstReg));
            emitIns_R_R(INS_mov, attr, dstReg, srcReg);
        }

        return true;
    }

    public void emitIns_R_LRiscV64(instruction ins, emitAttr attr, BasicBlock dst, regNumber reg)
    {
        assert(dst.HasFlag(BBF_HAS_LABEL));

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idInsOpt(insOpts.INS_OPTS_RL);
        id.RiscVBBlabel = dst;
        if (_compiler?.opts.compReloc == true)
        {
            id.idSetIsDspReloc();
        }
        id.idCodeSize(2 * sizeof(uint));
        id.idReg1(reg);

        if (Configuration.DEBUG && (_compiler?.compCurBB?.Kind == BBJ_EHCATCHRET))
        {
            global::RyuJitSharp.Emitter.instrDescDebugInfo __jitExceptionResult381;
            var __jitExceptionCoalesceLeft382 = id.idDebugOnlyInfo();
            if (__jitExceptionCoalesceLeft382 != null)
            {
                __jitExceptionResult381 = __jitExceptionCoalesceLeft382;
            }
            else
            {
                JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.RISCVLabelInstructionsRequireDebugDescriptorInfo);
                __jitExceptionResult381 = default;
            }
            var debugInfo = __jitExceptionResult381;
            debugInfo.idCatchRet = true;
        }

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitIns_R_LRiscV64(instruction ins, emitAttr attr, insGroup dst, regNumber reg)
    {
        assert(dst is not null);

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idInsOpt(insOpts.INS_OPTS_RL);
        id.RiscVIGlabel = dst;
        id.idSetIsBound();
        if (_compiler?.opts.compReloc == true)
        {
            id.idSetIsDspReloc();
        }
        id.idCodeSize(2 * sizeof(uint));
        id.idReg1(reg);
        appendToCurIG(id);
    }
}
