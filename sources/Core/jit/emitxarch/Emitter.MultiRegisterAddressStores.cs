// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_AR_R_R(instruction ins, emitAttr attr, regNumber op2Reg, regNumber op3Reg,
        regNumber @base, int offs, insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Address/two-register instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(IsSimdInstruction(ins));
        assert(IsThreeOperandAVXInstruction(ins));

        var id = emitNewInstrAmd(attr, offs);
        id.idIns(ins);
        id.idReg1(op2Reg);
        id.idReg2(op3Reg);
        id.idInsFmt(IF_AWR_RRD_RRD);
        id.idAddr().iiaAddrMode.amBaseReg = @base;
        id.idAddr().iiaAddrMode.amIndxReg = REG_NA;

        assert((instOptions & INS_OPTS_EVEX_b_MASK) == 0);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        var sz = emitInsSizeAM(id, insCodeMR(ins));
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }
}
