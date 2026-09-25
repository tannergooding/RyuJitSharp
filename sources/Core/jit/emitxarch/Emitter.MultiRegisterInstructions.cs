// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_R_R_I(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2, int ival,
        insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Two-register-immediate instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        // Only mov reg,imm64 takes a full eight-byte immediate. Other instructions
        // use a sign-extended dword, which cannot hold an eight-byte relocation.
        noway_assert((EA_SIZE(attr) < EA_8BYTE) || !EA_IS_CNS_RELOC(attr));
        var id = emitNewInstrSC(attr, ival);
        var useNDD = ((instOptions & INS_OPTS_EVEX_nd_MASK) != 0) && IsApxNddEncodableInstruction(ins);
        id.idIns(ins);
        id.idInsFmt(emitInsModeFormat(ins, IF_RRD_RRD_CNS, useNDD));
        id.idReg1(reg1);
        id.idReg2(reg2);

        ulong code;
        if (hasCodeMR(ins))
        {
            code = insCodeMR(ins);
        }
        else if (hasCodeMI(ins))
        {
            code = insCodeMI(ins);
        }
        else
        {
            code = insCodeRM(ins);
        }

        assert((instOptions & INS_OPTS_EVEX_b_MASK) == 0);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        SetEvexNdIfNeeded(id, instOptions);
        var sz = emitInsSizeRR(id, code, ival);
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }

    public void emitIns_R_R_R(instruction ins, emitAttr attr, regNumber targetReg, regNumber reg1, regNumber reg2,
        insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Three-register instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(IsSimdInstruction(ins) || IsApxExtendedEvexInstruction(ins));
        assert(IsThreeOperandAVXInstruction(ins) || IsKInstruction(ins) || IsApxExtendedEvexInstruction(ins));

        // The ND slot is shared with other features, so check instruction compatibility.
        var useNDD = ((instOptions & INS_OPTS_EVEX_nd_MASK) != 0) && IsApxNddEncodableInstruction(ins);
        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idInsFmt((ins == INS_mulx) ? IF_RWR_RWR_RRD : emitInsModeFormat(ins, IF_RRD_RRD_RRD, useNDD));
        id.idReg1(targetReg);
        id.idReg2(reg1);
        id.idReg3(reg2);

        SetEvexEmbRoundIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        SetEvexNdIfNeeded(id, instOptions);
        SetEvexNfIfNeeded(id, instOptions);
        var sz = emitInsSizeRR(id, insCodeRM(ins));
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }

    public void emitIns_R_R_R_I(instruction ins, emitAttr attr, regNumber targetReg,
        regNumber reg1, regNumber reg2, int ival, insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Three-register-immediate instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(IsSimdInstruction(ins));
        assert(IsThreeOperandAVXInstruction(ins));

        var id = emitNewInstrCns(attr, ival);
        id.idIns(ins);
        id.idInsFmt(IF_RWR_RRD_RRD_CNS);
        id.idReg1(targetReg);
        id.idReg2(reg1);
        id.idReg3(reg2);

        assert((instOptions & INS_OPTS_EVEX_b_MASK) == 0);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        var sz = emitInsSizeRR(id, insCodeRM(ins), ival);
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }
}
