// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_AR_R(instruction ins, emitAttr attr, regNumber reg, regNumber @base, nint disp,
        insOpts instOptions = INS_OPTS_NONE)
    {
        emitIns_ARX_R(ins, attr, reg, @base, REG_NA, 1, disp, instOptions);
    }

    public void emitIns_ARX_R(instruction ins, emitAttr attr, regNumber reg, regNumber @base,
        regNumber index, uint scale, nint disp, insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Address/register instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        var id = emitNewInstrAmd(attr, disp);
        insFormat fmt;
        if (reg == REG_NA)
        {
            fmt = emitInsModeFormat(ins, IF_ARD);
        }
        else
        {
            fmt = ins == INS_xchg ? IF_ARW_RRW : emitInsModeFormat(ins, IF_ARD_RRD);
            noway_assert(emitVerifyEncodable(ins, EA_SIZE(attr), reg));
            assert(!CodeGen.instIsFP(ins) && (EA_SIZE(attr) <= EA_64BYTE));
            id.idReg1(reg);
        }

        id.idIns(ins);
        id.idInsFmt(fmt);
        SetEvexNfIfNeeded(id, instOptions);
        SetEvexDFVIfNeeded(id, instOptions);

        id.idAddr().iiaAddrMode.amBaseReg = @base;
        id.idAddr().iiaAddrMode.amIndxReg = index;
        id.idAddr().iiaAddrMode.amScale = (uint)emitEncodeScale(scale);
        if ((instOptions & INS_OPTS_EVEX_NoApxPromotion) != 0)
        {
            id.idSetNoApxEvexPromotion();
        }

        assert(emitGetInsAmdAny(id) == disp);
        var size = emitInsSizeAM(id, insCodeMR(ins));
        id.idCodeSize(size);
        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)size);

        // Native emitAdjustStackDepthPushPop is empty with AMD64's FEATURE_FIXED_OUT_ARGS.
#endif
    }
}
