// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.GCInfo.GCtype;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_A(instruction ins, emitAttr attr, GenTreeIndir indir)
    {
        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Single indirect instruction recording requires xarch.");
        }

        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }

        var id = emitNewInstrAmd(attr, indir.Offset);
        var format = emitInsModeFormat(ins, IF_XARCH_ARD);
        id.idIns(ins);
        emitHandleMemOp(indir, id, format, ins);
        var size = emitInsSizeAM(id, insCodeMR(ins));
        id.idCodeSize(size);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)size);
        if (Configuration.TARGET_X86)
        {
            emitAdjustStackDepthPushPop(ins);
        }
    }

    public void emitIns_R_A(instruction ins, emitAttr attr, regNumber reg1, GenTreeIndir indir,
        insOpts instOptions = INS_OPTS_NONE)
    {
        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Register-memory instruction recording requires xarch.");
        }

        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }

        var offs = indir.Offset;
        var id = emitNewInstrAmd(attr, offs);
        id.idIns(ins);
        id.idReg1(reg1);
        emitHandleMemOp(indir, id, emitInsModeFormat(ins, IF_XARCH_RRD_ARD), ins);

        SetEvexBroadcastIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        var sz = emitInsSizeAM(id, insCodeRM(ins));
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
    }

    public void emitIns_R_A_I(instruction ins, emitAttr attr, regNumber reg1, GenTreeIndir indir, int ival,
        insOpts instOptions = INS_OPTS_NONE)
    {
        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Register-memory-immediate instruction recording requires xarch.");
        }

        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }

        noway_assert(emitVerifyEncodable(ins, EA_SIZE(attr), reg1));
        assert(IsSimdInstruction(ins));

        var offs = indir.Offset;
        var id = emitNewInstrAmdCns(attr, offs, ival);
        id.idIns(ins);
        id.idReg1(reg1);
        emitHandleMemOp(indir, id, emitInsModeFormat(ins, IF_XARCH_RRD_ARD_CNS), ins);

        ulong code;
        if (hasCodeMI(ins))
        {
            code = insCodeMI(ins);
        }
        else
        {
            code = insCodeRM(ins);
        }

        SetEvexBroadcastIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        var sz = emitInsSizeAM(id, code, ival);
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
    }

    public unsafe void emitIns_R_C_I(instruction ins, emitAttr attr, regNumber reg1,
        CORINFO_FIELD_HANDLE fldHnd, int offs, int ival, insOpts instOptions = INS_OPTS_NONE)
    {
        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Register-field-immediate instruction recording requires xarch.");
        }

        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }

        if (!jitStaticFldIsGlobAddr(fldHnd))
        {
            attr |= EA_DSP_RELOC_FLG;
        }

        noway_assert(emitVerifyEncodable(ins, EA_SIZE(attr), reg1));
        assert(IsSimdInstruction(ins));

        var id = emitNewInstrCnsDsp(attr, ival, offs);
        id.idIns(ins);
        id.idInsFmt(emitInsModeFormat(ins, IF_XARCH_RRD_MRD_CNS));
        id.idReg1(reg1);
        id.idAddr().iiaFieldHnd = fldHnd;

        ulong code;
        if (hasCodeMI(ins))
        {
            code = insCodeMI(ins);
        }
        else
        {
            code = insCodeRM(ins);
        }

        SetEvexBroadcastIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        var sz = emitInsSizeCV(id, code, ival);
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
    }

    public void emitIns_R_AR_R(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        regNumber @base, regNumber index, int scale, int offs)
    {
        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "AVX2 gather instruction recording requires xarch.");
        }

        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }

        assert(IsAVX2GatherInstruction(ins));

        var id = emitNewInstrAmd(attr, offs);
        id.idIns(ins);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idInsFmt(emitInsModeFormat(ins, IF_XARCH_RRD_ARD_RRD));
        id.idAddr().iiaAddrMode.amBaseReg = @base;
        id.idAddr().iiaAddrMode.amIndxReg = index;
        id.idAddr().iiaAddrMode.amScale = (uint)emitEncodeSize(unchecked((emitAttr)scale));

        var sz = emitInsSizeAM(id, insCodeRM(ins));
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
    }

    public void emitIns_I_AR(instruction ins, emitAttr attr, int val, regNumber reg, int disp,
        insOpts instOptions = INS_OPTS_NONE)
    {
        if (Configuration.TARGET_ARM)
        {
            NYI("emitIns_I_AR");
            throw new FatalJitException(CORJIT_SKIPPED,
                "ARM32 immediate memory instruction recording is not ported.");
        }

        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Immediate memory instruction recording requires xarch.");
        }

        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }

        assert(!CodeGen.instIsFP(ins) && (EA_SIZE(attr) <= EA_8BYTE));
        if (Configuration.TARGET_AMD64)
        {
            noway_assert((EA_SIZE(attr) < EA_8BYTE) || !EA_IS_CNS_RELOC(attr));
        }
        insFormat fmt;
        switch (ins)
        {
            case INS_rcl_N or INS_rcr_N or INS_rol_N or INS_ror_N or INS_shl_N or INS_shr_N or INS_sar_N:
            {
                assert(val != 1);
                fmt = IF_XARCH_ARW_SHF;
                val &= 0x7F;
                break;
            }

            default:
            {
                fmt = emitInsModeFormat(ins, IF_XARCH_ARD_CNS);
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
    }

    public unsafe void emitIns_R_AI(instruction ins, emitAttr attr, regNumber ireg, nint disp
#if DEBUG
        , nuint targetHandle = 0, GenTreeFlags gtFlags = GTF_EMPTY
#endif
        )
    {
        if (Configuration.TARGET_ARM64)
        {
            emitIns_R_AI_Arm64(ins, attr, ireg, disp
#if DEBUG
                , targetHandle, gtFlags
#endif
                );
            return;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            assert(EA_IS_RELOC(attr));
            assert(ins == INS_bl);
            assert(isGeneralRegister(ireg));

            var loongArchInstr = emitNewInstr(attr);
            loongArchInstr.idIns(ins);
            assert(ireg != TargetRuntimeMetadata.Register0);
            loongArchInstr.idReg1(ireg);
            loongArchInstr.idInsOpt(INS_OPTS_LOONGARCH64_RELOC);

            if (EA_IS_GCREF(attr))
            {
                loongArchInstr.idGCref(GCT_GCREF);
                loongArchInstr.idOpSize(EA_PTRSIZE);
            }
            else if (EA_IS_BYREF(attr))
            {
                loongArchInstr.idGCref(GCT_BYREF);
                loongArchInstr.idOpSize(EA_PTRSIZE);
            }

#if DEBUG
            var loongArchDebugInfo = loongArchInstr.idDebugOnlyInfo();
            assert(loongArchDebugInfo is not null);
            loongArchDebugInfo.idMemCookie = unchecked((nint)targetHandle);
            loongArchDebugInfo.idFlags = gtFlags;
#endif
            loongArchInstr.idAddr().iiaAddr = (byte*)disp;
            loongArchInstr.idCodeSize(8);
            appendToCurIG(loongArchInstr);
            return;
        }

        if (Configuration.TARGET_ARM)
        {
            assert(!CodeGen.instIsFP(ins) && (EA_SIZE(attr) <= EA_8BYTE) && (ireg != REG_NA));
            if (emitInsIsLoadArm32(ins))
            {
                var regTmp = ireg;
                if (isFloatReg(regTmp))
                {
                    assert(false, "emitIns_R_AI with a floating-point register.");
                    NYI("emitIns_R_AI with a floating-point register");
                    throw new FatalJitException(CORJIT_SKIPPED,
                        "ARM32 absolute-address instruction recording requires a general register.");
                }

                codeGen.instGen_Set_Reg_To_Imm(EA_IS_RELOC(attr) ? EA_HANDLE_CNS_RELOC : EA_PTRSIZE, regTmp, disp);
                emitIns_R_R_I(
                    ins,
                    attr & ~(EA_OFFSET_FLG | EA_DSP_RELOC_FLG | EA_CNS_RELOC_FLG),
                    ireg,
                    regTmp,
                    0,
                    INS_FLAGS_DONT_CARE,
                    INS_OPTS_NONE);
                return;
            }

            NYI("emitIns_R_AI");
            throw new FatalJitException(CORJIT_SKIPPED,
                "ARM32 relocated-address instruction recording is not ported.");
        }

        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Absolute-address instruction recording requires xarch.");
        }

        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }

        if (Configuration.TARGET_X86)
        {
            disp = unchecked((int)disp);
#if DEBUG
            targetHandle = unchecked((uint)targetHandle);
#endif
        }
        assert(!CodeGen.instIsFP(ins) && (EA_SIZE(attr) <= EA_8BYTE) && (ireg != REG_NA));
        noway_assert(emitVerifyEncodable(ins, EA_SIZE(attr), ireg));

        var id = emitNewInstrAmd(attr, disp);
        var fmt = emitInsModeFormat(ins, IF_XARCH_RRD_ARD);
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
    }

    public void emitIns_A_R_I(instruction ins, emitAttr attr, GenTreeIndir indir, regNumber reg, int imm)
    {
        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Memory-register-immediate recording requires xarch.");
        }

        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }

        assert(IsSimdInstruction(ins));
        assert(reg != REG_NA);

        var id = emitNewInstrAmdCns(attr, indir.Offset, imm);
        id.idIns(ins);
        id.idReg1(reg);
        emitHandleMemOp(indir, id, emitInsModeFormat(ins, IF_XARCH_ARD_RRD_CNS), ins);
        var size = emitInsSizeAM(id, insCodeMR(ins), imm);
        id.idCodeSize(size);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)size);
    }
}
