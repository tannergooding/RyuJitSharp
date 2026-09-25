// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe void emitIns_R_C(instruction ins, emitAttr attr, regNumber reg, CORINFO_FIELD_HANDLE fldHnd,
        int offs, insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Static-field register instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        if (!jitStaticFldIsGlobAddr(fldHnd))
        {
            attr |= EA_DSP_RELOC_FLG;
        }

        var size = EA_SIZE(attr);
        assert(size <= EA_64BYTE);
        noway_assert(emitVerifyEncodable(ins, size, reg));

        uint sz;
        instrDesc id;

        if ((attr & EA_OFFSET_FLG) != 0)
        {
            id = emitNewInstrDsp(EA_1BYTE, offs);
            id.idIns(ins);
            id.idInsFmt(IF_RWR_MRD_OFF);
            id.idReg1(reg);
            assert((ins == INS_mov) && (reg == REG_EAX));

            // Special case: "mov eax, [addr]" is smaller.
            sz = 1 + TARGET_POINTER_SIZE;
        }
        else
        {
            var fmt = emitInsModeFormat(ins, IF_RRD_MRD);
            id = emitNewInstrDsp(attr, offs);
            id.idIns(ins);
            id.idInsFmt(fmt);
            id.idReg1(reg);

            SetEvexBroadcastIfNeeded(id, instOptions);
            SetEvexEmbMaskIfNeeded(id, instOptions);
            SetEvexDFVIfNeeded(id, instOptions);
            sz = emitInsSizeCV(id, insCodeRM(ins));

            if (fldHnd == FLD_GLOBAL_FS)
            {
                sz++;
            }
            else if (fldHnd == FLD_GLOBAL_GS)
            {
                sz += 2; // The GS override also requires a SIB byte.
            }
        }

        id.idCodeSize(sz);
        id.idAddr().iiaFieldHnd = fldHnd;

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }

    public unsafe void emitIns_R_R_C(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        CORINFO_FIELD_HANDLE fldHnd, int offs, insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Two-register static-field instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(IsSimdInstruction(ins));
        assert(IsThreeOperandAVXInstruction(ins));

        if (!jitStaticFldIsGlobAddr(fldHnd))
        {
            attr |= EA_DSP_RELOC_FLG;
        }

        var id = emitNewInstrDsp(attr, offs);
        id.idIns(ins);
        id.idInsFmt((ins == INS_mulx) ? IF_RWR_RWR_MRD : emitInsModeFormat(ins, IF_RRD_RRD_MRD));
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idAddr().iiaFieldHnd = fldHnd;

        SetEvexBroadcastIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        var sz = emitInsSizeCV(id, insCodeRM(ins));
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }

    public unsafe void emitIns_SIMD_R_R_C(instruction ins, emitAttr attr, regNumber targetReg, regNumber op1Reg,
        CORINFO_FIELD_HANDLE fldHnd, int offs, insOpts instOptions)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "SIMD static-field instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        if (UseSimdEncoding())
        {
            emitIns_R_R_C(ins, attr, targetReg, op1Reg, fldHnd, offs, instOptions);
        }
        else
        {
            assert(instOptions == INS_OPTS_NONE);
            _ = emitIns_Mov(INS_movaps, attr, targetReg, op1Reg, canSkip: true);
            emitIns_R_C(ins, attr, targetReg, fldHnd, offs);
        }
#endif
    }
}
