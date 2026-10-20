// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_ARR_R(instruction ins, emitAttr attr, regNumber reg, regNumber @base, regNumber index, int disp)
    {
        if (Configuration.TARGET_ARM)
        {
            recordArm32InsARRR(ins, attr, reg, @base, index, disp);
            return;
        }

        if (Configuration.TARGET_ARM64)
        {
            NYI("emitIns_R_ARR");
            throw new FatalJitException(CORJIT_SKIPPED,
                "ARM64 indexed address/register instruction recording is not ported.");
        }

        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Indexed address/register instruction recording requires xarch.");
        }

        emitIns_ARX_R(ins, attr, reg, @base, index, 1, disp);
    }

    public void emitIns_AR_R(instruction ins, emitAttr attr, regNumber reg, regNumber @base, nint disp,
        insOpts instOptions = INS_OPTS_NONE)
    {
        if (instOptions != INS_OPTS_NONE)
        {
            if (Configuration.TARGET_ARM)
            {
                NYI("ARM32 address/register instruction options");
                throw new FatalJitException(CORJIT_SKIPPED,
                    "ARM32 address/register instruction options are not ported.");
            }
        }

        if (Configuration.TARGET_ARM)
        {
            assert(disp == (int)disp);
            recordArm32InsAR_R(ins, attr, reg, @base, unchecked((int)disp));
            return;
        }

        if (Configuration.TARGET_ARM64)
        {
            NYI("emitIns_AR_R");
            throw new FatalJitException(CORJIT_SKIPPED,
                "ARM64 address/register instruction recording is not ported.");
        }

        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Address/register instruction recording requires xarch.");
        }

        emitIns_ARX_R(ins, attr, reg, @base, REG_NA, 1, disp, instOptions);
    }

    public void emitIns_ARX_R(instruction ins, emitAttr attr, regNumber reg, regNumber @base,
        regNumber index, uint scale, nint disp, insOpts instOptions = INS_OPTS_NONE)
    {
        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Address/register instruction recording requires xarch.");
        }

        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }

        var id = emitNewInstrAmd(attr, disp);
        insFormat fmt;
        if (reg == REG_NA)
        {
            fmt = emitInsModeFormat(ins, IF_XARCH_ARD);
        }
        else
        {
            fmt = (ins == INS_xchg) || (ins == INS_xadd)
                ? IF_XARCH_ARW_RRW
                : emitInsModeFormat(ins, IF_XARCH_ARD_RRD);
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

        if (Configuration.TARGET_X86)
        {
            emitAdjustStackDepthPushPop(ins);
        }
    }
}
