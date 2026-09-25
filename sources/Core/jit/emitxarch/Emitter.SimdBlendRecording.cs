// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_XARCH
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_R_R_A_R(instruction ins, emitAttr attr, regNumber targetReg,
        regNumber op1Reg, regNumber op3Reg, GenTreeIndir indir, insOpts instOptions)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD blend recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(isAvxBlendv(ins) || isAvx512Blendv(ins));
        assert(UseSimdEncoding());

        var ival = encodeRegAsIval(op3Reg);
        var offs = indir.Offset;
        var id = emitNewInstrAmdCns(attr, offs, ival);
        id.idIns(ins);
        id.idReg1(targetReg);
        id.idReg2(op1Reg);
        emitHandleMemOp(indir, id, IF_RWR_RRD_ARD_RRD, ins);

        SetEvexBroadcastIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        var sz = emitInsSizeAM(id, insCodeRM(ins), ival);
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }

    public unsafe void emitIns_R_R_C_R(instruction ins, emitAttr attr, regNumber targetReg,
        regNumber op1Reg, regNumber op3Reg, CORINFO_FIELD_HANDLE fldHnd, int offs, insOpts instOptions)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD blend recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(isAvxBlendv(ins) || isAvx512Blendv(ins));
        assert(UseSimdEncoding());
        if (!jitStaticFldIsGlobAddr(fldHnd))
        {
            attr |= EA_DSP_RELOC_FLG;
        }

        var ival = encodeRegAsIval(op3Reg);
        var id = emitNewInstrCnsDsp(attr, ival, offs);
        id.idIns(ins);
        id.idReg1(targetReg);
        id.idReg2(op1Reg);
        id.idInsFmt(IF_RWR_RRD_MRD_RRD);
        id.idAddr().iiaFieldHnd = fldHnd;

        SetEvexBroadcastIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        var sz = emitInsSizeCV(id, insCodeRM(ins), ival);
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }

    public void emitIns_R_R_S_R(instruction ins, emitAttr attr, regNumber targetReg,
        regNumber op1Reg, regNumber op3Reg, int varx, int offs, insOpts instOptions)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD blend recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(isAvxBlendv(ins) || isAvx512Blendv(ins));
        assert(UseSimdEncoding());

        var ival = encodeRegAsIval(op3Reg);
        var id = emitNewInstrCns(attr, ival);
        id.idIns(ins);
        id.idReg1(targetReg);
        id.idReg2(op1Reg);
        id.idInsFmt(IF_RWR_RRD_SRD_RRD);
        id.idAddr().iiaLclVar.initLclVarAddr(varx, unchecked((uint)offs));

        SetEvexBroadcastIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        var sz = emitInsSizeSV(id, insCodeRM(ins), varx, offs, ival);
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }

    public void emitIns_R_R_R_R(instruction ins, emitAttr attr, regNumber targetReg,
        regNumber reg1, regNumber reg2, regNumber reg3, insOpts instOptions)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD blend recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(isAvxBlendv(ins) || isAvx512Blendv(ins));
        assert(UseSimdEncoding());

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idInsFmt(IF_RWR_RRD_RRD_RRD);
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
#endif
    }

#if TARGET_AMD64
    public static bool IsAVXVNNIFamilyInstruction(instruction ins)
    {
        return ((ins >= FIRST_AVXVNNI_INSTRUCTION) && (ins <= LAST_AVXVNNI_INSTRUCTION))
            || IsAVXVNNIINTInstruction(ins);
    }

    public static bool IsAVXVNNIINTInstruction(instruction ins)
    {
        return ((ins >= FIRST_AVXVNNIINT8_INSTRUCTION) && (ins <= LAST_AVXVNNIINT8_INSTRUCTION))
            || ((ins >= FIRST_AVXVNNIINT16_INSTRUCTION) && (ins <= LAST_AVXVNNIINT16_INSTRUCTION));
    }

    public static bool Is3OpRmwInstruction(instruction ins)
    {
        switch (ins)
        {
            case INS_vpermi2d:
            case INS_vpermi2pd:
            case INS_vpermi2ps:
            case INS_vpermi2q:
            case INS_vpermt2d:
            case INS_vpermt2pd:
            case INS_vpermt2ps:
            case INS_vpermt2q:
            case INS_vpermi2w:
            case INS_vpermt2w:
            case INS_vpermi2b:
            case INS_vpermt2b:
            {
                return true;
            }

            default:
            {
                // instrsxarch.h:1050-1083 defines the AVX10v1 FMA range by these endpoints.
                return ((ins >= FIRST_FMA_INSTRUCTION) && (ins <= LAST_FMA_INSTRUCTION))
                    || IsAVXVNNIFamilyInstruction(ins)
                    || ((ins >= FIRST_AVX512BMM_INSTRUCTION) && (ins <= LAST_AVX512BMM_INSTRUCTION))
                    || ((ins >= FIRST_AVXIFMA_INSTRUCTION) && (ins <= LAST_AVXIFMA_INSTRUCTION))
                    || ((ins >= INS_vfmadd132ph) && (ins <= INS_vfnmsub231sh));
            }
        }
    }

    private static bool isAvx512Blendv(instruction ins)
    {
        return ins is INS_vblendmps or INS_vblendmpd or INS_vpblendmb or INS_vpblendmd or INS_vpblendmq or INS_vpblendmw;
    }

    private static bool isAvxBlendv(instruction ins)
    {
        return ins is INS_vblendvps or INS_vblendvpd or INS_vpblendvb;
    }

    private static bool isSse41Blendv(instruction ins)
    {
        return ins is INS_blendvps or INS_blendvpd or INS_pblendvb;
    }

    private static sbyte encodeRegAsIval(regNumber opReg)
    {
        assert(((opReg >= REG_XMM0) && (opReg <= REG_XMM15)) || isMaskReg(opReg));
        var ival = (nint)opReg;
        assert((ival >= 0) && (ival <= 0xFF));

        return unchecked((sbyte)ival);
    }
#endif
}
#endif
