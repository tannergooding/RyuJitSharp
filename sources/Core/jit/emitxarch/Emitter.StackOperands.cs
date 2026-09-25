// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_R_R_S(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        int varx, int offs, insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Two-register stack instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(IsSimdInstruction(ins) || IsApxExtendedEvexInstruction(ins));
        assert(IsThreeOperandAVXInstruction(ins) || IsApxExtendedEvexInstruction(ins));

        var id = emitNewInstr(attr);
        var useNDD = ((instOptions & INS_OPTS_EVEX_nd_MASK) != 0) && IsApxNddEncodableInstruction(ins);
        id.idIns(ins);
        id.idInsFmt((ins == INS_mulx) ? IF_RWR_RWR_SRD : emitInsModeFormat(ins, IF_RRD_RRD_SRD, useNDD));
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idAddr().iiaLclVar.initLclVarAddr(varx, unchecked((uint)offs));

        SetEvexBroadcastIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        SetEvexNdIfNeeded(id, instOptions);
#if DEBUG
        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        debugInfo.idVarRefOffs = unchecked((uint)emitVarRefOffs);
#endif
        var sz = emitInsSizeSV(id, insCodeRM(ins), varx, offs);
        id.idCodeSize(sz);
        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }

    public void emitIns_S(instruction ins, emitAttr attr, int varx, int offs)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Stack instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        var id = emitNewInstr(attr);
        var fmt = emitInsModeFormat(ins, IF_SRD);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idAddr().iiaLclVar.initLclVarAddr(varx, unchecked((uint)offs));

        var sz = emitInsSizeSV(id, insCodeMR(ins), varx, offs);
        id.idCodeSize(sz);
#if DEBUG
        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        debugInfo.idVarRefOffs = unchecked((uint)emitVarRefOffs);
#endif
        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);

        // Native emitAdjustStackDepthPushPop is empty with AMD64's FEATURE_FIXED_OUT_ARGS.
#endif
    }

    public void emitIns_S_I(instruction ins, emitAttr attr, int varx, int offs, int val)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Stack-immediate instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        noway_assert((EA_SIZE(attr) < EA_8BYTE) || !EA_IS_CNS_RELOC(attr));

        insFormat fmt;
        switch (ins)
        {
            case INS_rcl_N or INS_rcr_N or INS_rol_N or INS_ror_N or INS_shl_N or INS_shr_N or INS_sar_N:
            {
                assert(val != 1);
                fmt = IF_SRW_SHF;
                val &= 0x7F;
                break;
            }

            default:
            {
                fmt = emitInsModeFormat(ins, IF_SRD_CNS);
                break;
            }
        }

        var id = emitNewInstrCns(attr, val);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idAddr().iiaLclVar.initLclVarAddr(varx, unchecked((uint)offs));

        var sz = emitInsSizeSV(id, insCodeMI(ins), varx, offs, val);
        id.idCodeSize(sz);
#if DEBUG
        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        debugInfo.idVarRefOffs = unchecked((uint)emitVarRefOffs);
#endif
        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }
}
