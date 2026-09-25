// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_R_A(instruction ins, emitAttr attr, regNumber reg1, GenTreeIndir indir,
        insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Register-memory instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        var offs = indir.Offset;
        var id = emitNewInstrAmd(attr, offs);
        id.idIns(ins);
        id.idReg1(reg1);
        emitHandleMemOp(indir, id, emitInsModeFormat(ins, IF_RRD_ARD), ins);

        SetEvexBroadcastIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        var sz = emitInsSizeAM(id, insCodeRM(ins));
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }

    public void emitIns_R_R_A(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        GenTreeIndir indir, insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Two-register memory instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(IsSimdInstruction(ins) || IsApxExtendedEvexInstruction(ins));
        assert(IsThreeOperandAVXInstruction(ins) || IsApxExtendedEvexInstruction(ins));

        var offs = indir.Offset;
        var id = emitNewInstrAmd(attr, offs);
        id.idIns(ins);
        id.idReg1(reg1);
        id.idReg2(reg2);
        emitHandleMemOp(indir, id,
            (ins == INS_mulx) ? IF_RWR_RWR_ARD : emitInsModeFormat(ins, IF_RRD_RRD_ARD), ins);

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
#endif
    }

    public void emitIns_SIMD_R_R_I(instruction ins, emitAttr attr, regNumber targetReg, regNumber op1Reg,
        int ival, insOpts instOptions)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD register-immediate recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        if (UseSimdEncoding() || IsDstSrcImmAvxInstruction(ins))
        {
            emitIns_R_R_I(ins, attr, targetReg, op1Reg, ival, instOptions);
        }
        else
        {
            assert(instOptions == INS_OPTS_NONE);
            _ = emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, canSkip: true);
            emitIns_R_I(ins, attr, targetReg, ival);
        }
#endif
    }

    public void emitIns_SIMD_R_R_A(instruction ins, emitAttr attr, regNumber targetReg, regNumber op1Reg,
        GenTreeIndir indir, insOpts instOptions)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD memory instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        if (UseSimdEncoding())
        {
            emitIns_R_R_A(ins, attr, targetReg, op1Reg, indir, instOptions);
        }
        else
        {
            assert(instOptions == INS_OPTS_NONE);
            _ = emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, canSkip: true);
            emitIns_R_A(ins, attr, targetReg, indir);
        }
#endif
    }

    public void emitIns_SIMD_R_R_S(instruction ins, emitAttr attr, regNumber targetReg, regNumber op1Reg,
        int varx, int offs, insOpts instOptions)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD stack instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        if (UseSimdEncoding())
        {
            emitIns_R_R_S(ins, attr, targetReg, op1Reg, varx, offs, instOptions);
        }
        else
        {
            assert(instOptions == INS_OPTS_NONE);
            _ = emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, canSkip: true);
            emitIns_R_S(ins, attr, targetReg, varx, offs);
        }
#endif
    }

    public void emitIns_SIMD_R_R_A_I(instruction ins, emitAttr attr, regNumber targetReg, regNumber op1Reg,
        GenTreeIndir indir, int ival, insOpts instOptions)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD address-immediate recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        if (UseSimdEncoding())
        {
            emitIns_R_R_A_I(ins, attr, targetReg, op1Reg, indir, ival, IF_RWR_RRD_ARD_CNS, instOptions);
        }
        else
        {
            assert(instOptions == INS_OPTS_NONE);
            _ = emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, canSkip: true);
            emitIns_R_A_I(ins, attr, targetReg, indir, ival);
        }
#endif
    }

    public void emitIns_SIMD_R_R_S_I(instruction ins, emitAttr attr, regNumber targetReg, regNumber op1Reg,
        int varx, int offs, int ival, insOpts instOptions)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD stack-immediate recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        if (UseSimdEncoding())
        {
            emitIns_R_R_S_I(ins, attr, targetReg, op1Reg, varx, offs, ival, instOptions);
        }
        else
        {
            assert(instOptions == INS_OPTS_NONE);
            _ = emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, canSkip: true);
            emitIns_R_S_I(ins, attr, targetReg, varx, offs, ival);
        }
#endif
    }

#if TARGET_AMD64
    private static bool IsDstSrcImmAvxInstruction(instruction ins)
    {
        // These forms do not encode an operand in VEX.vvvv, and retain separate
        // destination/source operands even with legacy encoding.
        return ins is INS_aeskeygenassist or INS_extractps or INS_pextrb or INS_pextrw or INS_pextrd or
            INS_pextrq or INS_pshufd or INS_pshufhw or INS_pshuflw or INS_roundpd or INS_roundps;
    }
#endif
}
