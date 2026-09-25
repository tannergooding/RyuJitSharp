// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_Data16()
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "DATA16 recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        var id = emitNewInstrSmall(EA_1BYTE);
        id.idIns(INS_data16);
        id.idInsFmt(IF_NONE);
        id.idCodeSize(1);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + 1);
#endif
    }

    public void emitIns_R_AI(instruction ins, emitAttr attr, regNumber ireg, nint disp
#if DEBUG
        , nuint targetHandle = 0, GenTreeFlags gtFlags = GTF_EMPTY
#endif
        )
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Absolute-address instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(!CodeGen.instIsFP(ins) && (EA_SIZE(attr) <= EA_8BYTE) && (ireg != REG_NA));
        noway_assert(emitVerifyEncodable(ins, EA_SIZE(attr), ireg));

        var id = emitNewInstrAmd(attr, disp);
        var fmt = emitInsModeFormat(ins, IF_RRD_ARD);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idReg1(ireg);
        id.idAddr().iiaAddrMode.amBaseReg = REG_NA;
        id.idAddr().iiaAddrMode.amIndxReg = REG_NA;

        if (EA_IS_CNS_TLSGD_RELOC(attr))
        {
            id.idSetTlsGD();
        }

#if DEBUG
        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        debugInfo.idFlags = gtFlags;
        debugInfo.idMemCookie = unchecked((nint)targetHandle);
#endif
        assert(emitGetInsAmdAny(id) == disp);
        var sz = emitInsSizeAM(id, insCodeRM(ins));
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }
}
