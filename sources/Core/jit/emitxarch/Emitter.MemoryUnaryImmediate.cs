// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe void emitIns_C(instruction ins, emitAttr attr, CORINFO_FIELD_HANDLE fldHnd, int offs)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Single static-field instruction recording requires Windows AMD64.");
#else
        RequireSupportedInstructionRecording();
        if (!jitStaticFldIsGlobAddr(fldHnd))
        {
            attr |= EA_DSP_RELOC_FLG;
        }

        uint size;
        instrDesc id;
        if ((attr & EA_OFFSET_FLG) != 0)
        {
            assert(ins == INS_push);
            size = 1 + TARGET_POINTER_SIZE;
            id = emitNewInstrDsp(EA_1BYTE, offs);
            id.idIns(ins);
            id.idInsFmt(IF_MRD_OFF);
        }
        else
        {
            var format = emitInsModeFormat(ins, IF_MRD);
            id = emitNewInstrDsp(attr, offs);
            id.idIns(ins);
            id.idInsFmt(format);
            size = emitInsSizeCV(id, insCodeMR(ins));
        }
        if (TakesRexWPrefix(id))
        {
            size += emitGetRexPrefixSize(id, ins);
        }

        id.idAddr().iiaFieldHnd = fldHnd;
        id.idCodeSize(size);
        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)size);
#endif
    }

    public void emitIns_A(instruction ins, emitAttr attr, GenTreeIndir indir)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Single indirect instruction recording requires Windows AMD64.");
#else
        RequireSupportedInstructionRecording();
        var id = emitNewInstrAmd(attr, indir.Offset);
        var format = emitInsModeFormat(ins, IF_ARD);
        id.idIns(ins);
        emitHandleMemOp(indir, id, format, ins);
        var size = emitInsSizeAM(id, insCodeMR(ins));
        id.idCodeSize(size);
        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)size);
#endif
    }

    public void emitIns_AR(instruction ins, emitAttr attr, regNumber baseReg, int offs, insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Unary memory instruction recording requires Windows AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(ins is INS_prefetcht0 or INS_prefetcht1 or INS_prefetcht2 or INS_prefetchnta or INS_inc or INS_dec);
        var id = emitNewInstrAmd(attr, offs);
        id.idIns(ins);
        id.idInsFmt(emitInsModeFormat(ins, IF_ARD));
        id.idAddr().iiaAddrMode.amBaseReg = baseReg;
        id.idAddr().iiaAddrMode.amIndxReg = REG_NA;
        if ((instOptions & INS_OPTS_EVEX_NoApxPromotion) != 0)
        {
            id.idSetNoApxEvexPromotion();
        }

        var size = emitInsSizeAM(id, insCodeMR(ins));
        id.idCodeSize(size);
        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)size);
#endif
    }

    public void emitIns_I_AR(
        instruction ins, emitAttr attr, int val, regNumber reg, int disp, insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Immediate memory instruction recording requires Windows AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(!CodeGen.instIsFP(ins) && (EA_SIZE(attr) <= EA_8BYTE));
        noway_assert((EA_SIZE(attr) < EA_8BYTE) || !EA_IS_CNS_RELOC(attr));
        insFormat fmt;
        switch (ins)
        {
            case INS_rcl_N or INS_rcr_N or INS_rol_N or INS_ror_N or INS_shl_N or INS_shr_N or INS_sar_N:
            {
                assert(val != 1);
                fmt = IF_ARW_SHF;
                val &= 0x7F;
                break;
            }

            default:
            {
                fmt = emitInsModeFormat(ins, IF_ARD_CNS);
                break;
            }
        }

        var id = emitNewInstrAmdCns(attr, disp, val);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idAddr().iiaAddrMode.amBaseReg = reg;
        id.idAddr().iiaAddrMode.amIndxReg = REG_NA;
        if ((instOptions & INS_OPTS_EVEX_NoApxPromotion) != 0)
        {
            id.idSetNoApxEvexPromotion();
        }
        assert(emitGetInsAmdAny(id) == disp);

        SetEvexNfIfNeeded(id, instOptions);
        SetEvexDFVIfNeeded(id, instOptions);
        var size = emitInsSizeAM(id, insCodeMI(ins), val);
        id.idCodeSize(size);
        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)size);
#endif
    }
}
