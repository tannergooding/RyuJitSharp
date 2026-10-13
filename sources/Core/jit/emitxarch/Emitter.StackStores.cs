// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_S_R(instruction ins, emitAttr attr, regNumber ireg, int varx, int offs,
        insOpts instOptions = INS_OPTS_NONE)
    {
        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }
        var fmt = (ins == INS_xchg) ? IF_XARCH_SRW_RRW : emitInsModeFormat(ins, IF_XARCH_SRD_RRD);

        if (IsMovInstruction(ins) && IsRedundantStackMov(ins, fmt, attr, ireg, varx, offs))
        {
            return;
        }

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idReg1(ireg);
        id.idAddr().iiaInitLclVarAddr(varx, unchecked((uint)offs));

        assert((instOptions & INS_OPTS_EVEX_b_MASK) == 0);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        var sz = emitInsSizeSV(id, insCodeMR(ins), varx, offs);
        if (Configuration.TARGET_X86 && (attr == EA_1BYTE))
        {
            assert(IsX86ByteRegister(ireg));
        }
        id.idCodeSize(sz);
#if DEBUG
        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        debugInfo.idVarRefOffs = unchecked((uint)emitVarRefOffs);
#endif
        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
    }

    public void emitIns_S_R_I(instruction ins, emitAttr attr, int varNum, int offs, regNumber reg, int ival)
    {
        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }
        assert(IsSimdInstruction(ins));
        assert(reg != REG_NA);

        var id = emitNewInstrAmdCns(attr, 0, ival);
        id.idIns(ins);
        id.idInsFmt(emitInsModeFormat(ins, IF_XARCH_SRD_RRD_CNS));
        id.idReg1(reg);
        id.idAddr().iiaInitLclVarAddr(varNum, unchecked((uint)offs));
#if DEBUG
        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        debugInfo.idVarRefOffs = unchecked((uint)emitVarRefOffs);
#endif
        var sz = emitInsSizeSV(id, insCodeMR(ins), varNum, offs, ival);
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
    }

    public void emitStoreSimd12ToLclOffset(uint varNum, uint offset, regNumber dataReg, GenTree? tmpRegProvider)
    {
        if (!Configuration.FEATURE_SIMD)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "SIMD stack stores are not enabled for this target.");
        }

        assert(varNum != unchecked((uint)BAD_VAR_NUM));
        assert(dataReg.IsFltReg);

        emitIns_S_R(INS_movsd_simd, EA_8BYTE, dataReg, unchecked((int)varNum), unchecked((int)offset));
        emitIns_S_R_I(INS_extractps, EA_16BYTE, unchecked((int)varNum), unchecked((int)(offset + 8)), dataReg, 2);
    }
}
