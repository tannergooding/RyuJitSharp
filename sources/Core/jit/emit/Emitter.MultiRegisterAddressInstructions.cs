// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_R_R_A(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        GenTreeIndir indir, insOpts instOptions = INS_OPTS_NONE)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                "Two-register memory instruction recording requires xarch.");
            return;
        }

        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }

        assert(IsSimdInstruction(ins) || IsApxExtendedEvexInstruction(ins));
        assert(IsThreeOperandAVXInstruction(ins) || IsApxExtendedEvexInstruction(ins));

        var offs = indir.Offset;
        var id = emitNewInstrAmd(attr, offs);
        id.idIns(ins);
        id.idReg1(reg1);
        id.idReg2(reg2);
        emitHandleMemOp(indir, id,
            (ins == INS_mulx) ? IF_XARCH_RWR_RWR_ARD : emitInsModeFormat(ins, IF_XARCH_RRD_RRD_ARD), ins);

        if (IsSimdInstruction(ins))
        {
            SetEvexBroadcastIfNeeded(id, instOptions);
            SetEvexEmbMaskIfNeeded(id, instOptions);
        }
        SetEvexNdIfNeeded(id, instOptions);
        SetEvexNfIfNeeded(id, instOptions);
        var sz = emitInsSizeAM(id, insCodeRM(ins));
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
    }

    public void emitIns_R_R_A_I(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        GenTreeIndir indir, int ival, insFormat fmt, insOpts instOptions = INS_OPTS_NONE)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                "Two-register memory-immediate recording requires xarch.");
            return;
        }

        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }

        assert(IsSimdInstruction(ins));
        assert(IsThreeOperandAVXInstruction(ins));

        var offs = indir.Offset;
        var id = emitNewInstrAmdCns(attr, offs, ival);
        id.idIns(ins);
        id.idReg1(reg1);
        id.idReg2(reg2);
        emitHandleMemOp(indir, id, fmt, ins);

        SetEvexBroadcastIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        var sz = emitInsSizeAM(id, insCodeRM(ins), ival);
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
    }

    public void emitIns_R_R_A_R(instruction ins, emitAttr attr, regNumber targetReg,
        regNumber op1Reg, regNumber op3Reg, GenTreeIndir indir, insOpts instOptions)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED,
                "Register-address-register instruction recording requires xarch.");
            return;
        }

        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }

        assert(isAvxBlendv(ins) || isAvx512Blendv(ins));
        assert(UseSimdEncoding());

        var ival = encodeRegAsIval(op3Reg);
        var offs = indir.Offset;
        var id = emitNewInstrAmdCns(attr, offs, ival);
        id.idIns(ins);
        id.idReg1(targetReg);
        id.idReg2(op1Reg);
        emitHandleMemOp(indir, id, IF_XARCH_RWR_RRD_ARD_RRD, ins);

        SetEvexBroadcastIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        var sz = emitInsSizeAM(id, insCodeRM(ins), ival);
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
    }

    public void emitIns_R_R_R_R(instruction ins, emitAttr attr, regNumber targetReg,
        regNumber reg1, regNumber reg2, regNumber reg3, insOpts instOptions = INS_OPTS_NONE,
        insScalableOpts scalableOptions = insScalableOpts.INS_SCALABLE_OPTS_NONE)
    {
        if (Configuration.TARGET_ARM64)
        {
            emitIns_R_R_R_RArm64(ins, attr, targetReg, reg1, reg2, reg3, instOptions, scalableOptions);
            return;
        }

        if (Configuration.TARGET_ARM)
        {
            if (!insOptsNone(instOptions))
            {
                FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "ARM32 four-register instruction options are not ported.");
                return;
            }

            recordArm32InsRRRR(ins, attr, targetReg, reg1, reg2, reg3);
            return;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            emitInsRRRRIloongArch64(ins, attr, targetReg, reg1, reg2, reg3);
            return;
        }

        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Four-register instruction recording requires xarch.");
            return;
        }

        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }

        assert(isAvxBlendv(ins) || isAvx512Blendv(ins));
        assert(UseSimdEncoding());

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idInsFmt(IF_XARCH_RWR_RRD_RRD_RRD);
        id.idReg1(targetReg);
        id.idReg2(reg1);
        id.idReg3(reg2);
        id.idReg4(reg3);

        assert((instOptions & INS_OPTS_EVEX_b_MASK) == 0);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        var sz = emitInsSizeRR(id, insCodeRM(ins));
        if (!isMaskReg(reg3))
        {
            // VEX encodes the fourth vector register in an immediate byte.
            sz = unchecked(sz + 1);
        }

        id.idCodeSize(sz);
        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
    }
}
