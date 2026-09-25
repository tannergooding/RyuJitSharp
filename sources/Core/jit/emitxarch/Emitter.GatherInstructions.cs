// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_R_AR_R(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        regNumber @base, regNumber index, int scale, int offs)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "AVX2 gather instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(IsAVX2GatherInstruction(ins));

        var id = emitNewInstrAmd(attr, offs);
        id.idIns(ins);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idInsFmt(emitInsModeFormat(ins, IF_RRD_ARD_RRD));
        id.idAddr().iiaAddrMode.amBaseReg = @base;
        id.idAddr().iiaAddrMode.amIndxReg = index;
        id.idAddr().iiaAddrMode.amScale = (uint)emitEncodeSize(unchecked((emitAttr)scale));

        var sz = emitInsSizeAM(id, insCodeRM(ins));
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }

#if TARGET_AMD64
    private static bool IsAVX2GatherInstruction(instruction ins)
    {
        return ins is INS_vpgatherdd or INS_vpgatherdq or INS_vpgatherqd or INS_vpgatherqq
            or INS_vgatherdps or INS_vgatherdpd or INS_vgatherqps or INS_vgatherqpd;
    }
#endif
}
