// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    private static bool IsRiscVThreeRegisterInstruction(instruction ins) => ins is
        INS_add or INS_sub or INS_sll or INS_slt or INS_sltu or INS_xor or INS_srl or INS_sra or INS_or or INS_and or
        INS_addw or INS_subw or INS_sllw or INS_srlw or INS_sraw or INS_mul or INS_mulh or INS_mulhsu or INS_mulhu or
        INS_div or INS_divu or INS_rem or INS_remu or INS_mulw or INS_divw or INS_divuw or INS_remw or INS_remuw or
        INS_fadd_s or INS_fsub_s or INS_fmul_s or INS_fdiv_s or INS_fsgnj_s or INS_fsgnjn_s or INS_fsgnjx_s or
        INS_fmin_s or INS_fmax_s or INS_feq_s or INS_flt_s or INS_fle_s or
        INS_fadd_d or INS_fsub_d or INS_fmul_d or INS_fdiv_d or INS_fsgnj_d or INS_fsgnjn_d or INS_fsgnjx_d or
        INS_fmin_d or INS_fmax_d or INS_feq_d or INS_flt_d or INS_fle_d or
        INS_lr_w or INS_lr_d or INS_sc_w or INS_sc_d or INS_amoswap_w or INS_amoswap_d or INS_amoadd_w or
        INS_amoadd_d or INS_amoxor_w or INS_amoxor_d or INS_amoand_w or INS_amoand_d or INS_amoor_w or
        INS_amoor_d or INS_amomin_w or INS_amomin_d or INS_amomax_w or INS_amomax_d or INS_amominu_w or
        INS_amominu_d or INS_amomaxu_w or INS_amomaxu_d or
        INS_sh1add or INS_sh2add or INS_sh3add or INS_add_uw or INS_sh1add_uw or INS_sh2add_uw or INS_sh3add_uw or
        INS_rol or INS_rolw or INS_ror or INS_rorw or INS_xnor or INS_orn or INS_andn or INS_min or INS_minu or
        INS_max or INS_maxu or INS_bset or INS_bclr or INS_bext or INS_binv or INS_czero_eqz or INS_czero_nez;

    public unsafe void emitIns_R_R_C(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        CORINFO_FIELD_HANDLE fldHnd, int offs, insOpts instOptions = INS_OPTS_NONE)
    {
        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Two-register static-field instruction recording requires xarch.");
        }

        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }

        assert(IsSimdInstruction(ins));
        assert(IsThreeOperandAVXInstruction(ins));

        if (!jitStaticFldIsGlobAddr(fldHnd))
        {
            attr |= EA_DSP_RELOC_FLG;
        }

        var id = emitNewInstrDsp(attr, offs);
        id.idIns(ins);
        id.idInsFmt((ins == INS_mulx) ? IF_XARCH_RWR_RWR_MRD : emitInsModeFormat(ins, IF_XARCH_RRD_RRD_MRD));
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idAddr().iiaFieldHnd = fldHnd;

        SetEvexBroadcastIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        var sz = emitInsSizeCV(id, insCodeRM(ins));
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
    }

    public void emitIns_R_R_S(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        int varx, int offs, insOpts instOptions = INS_OPTS_NONE)
    {
        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Two-register stack instruction recording requires xarch.");
        }

        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }

        assert(IsSimdInstruction(ins) || IsApxExtendedEvexInstruction(ins));
        assert(IsThreeOperandAVXInstruction(ins) || IsApxExtendedEvexInstruction(ins));

        var id = emitNewInstr(attr);
        var useNDD = ((instOptions & INS_OPTS_EVEX_nd_MASK) != 0) && IsApxNddEncodableInstruction(ins);
        id.idIns(ins);
        id.idInsFmt((ins == INS_mulx) ? IF_XARCH_RWR_RWR_SRD : emitInsModeFormat(ins, IF_XARCH_RRD_RRD_SRD, useNDD));
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idAddr().iiaInitLclVarAddr(varx, unchecked((uint)offs));

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
    }

    public unsafe void emitIns_R_R_C_I(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        CORINFO_FIELD_HANDLE fldHnd, int offs, int ival, insOpts instOptions = INS_OPTS_NONE)
    {
        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Two-register field-immediate recording requires xarch.");
        }

        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }

        assert(IsSimdInstruction(ins));
        assert(IsThreeOperandAVXInstruction(ins));

        if (!jitStaticFldIsGlobAddr(fldHnd))
        {
            attr |= EA_DSP_RELOC_FLG;
        }

        var id = emitNewInstrCnsDsp(attr, ival, offs);
        id.idIns(ins);
        id.idInsFmt(IF_XARCH_RWR_RRD_MRD_CNS);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idAddr().iiaFieldHnd = fldHnd;

        SetEvexBroadcastIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        var sz = emitInsSizeCV(id, insCodeRM(ins), ival);
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
    }

    public void emitIns_R_R_S_I(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        int varx, int offs, int ival, insOpts instOptions = INS_OPTS_NONE)
    {
        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Two-register stack-immediate recording requires xarch.");
        }

        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }

        assert(IsSimdInstruction(ins));
        assert(IsThreeOperandAVXInstruction(ins));

        var id = emitNewInstrCns(attr, ival);
        id.idIns(ins);
        id.idInsFmt(IF_XARCH_RWR_RRD_SRD_CNS);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idAddr().iiaInitLclVarAddr(varx, unchecked((uint)offs));

        SetEvexBroadcastIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);
#if DEBUG
        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        debugInfo.idVarRefOffs = unchecked((uint)emitVarRefOffs);
#endif
        var sz = emitInsSizeSV(id, insCodeRM(ins), varx, offs, ival);
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
    }

    public unsafe void emitIns_R_R_C_R(instruction ins, emitAttr attr, regNumber targetReg,
        regNumber op1Reg, regNumber op3Reg, CORINFO_FIELD_HANDLE fldHnd, int offs, insOpts instOptions = INS_OPTS_NONE)
    {
        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Register-field-register instruction recording requires xarch.");
        }

        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }

        assert(isAvxBlendv(ins) || isAvx512Blendv(ins));
        assert(UseSimdEncoding());

        if (!jitStaticFldIsGlobAddr(fldHnd))
        {
            attr |= EA_DSP_RELOC_FLG;
        }

        var ival = encodeRegAsIval(op3Reg);
        var id = emitNewInstrCnsDsp(attr, ival, offs);
        id.idIns(ins);
        id.idReg1(targetReg);
        id.idReg2(op1Reg);
        id.idInsFmt(IF_XARCH_RWR_RRD_MRD_RRD);
        id.idAddr().iiaFieldHnd = fldHnd;

        SetEvexBroadcastIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        var sz = emitInsSizeCV(id, insCodeRM(ins), ival);
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
    }

    public void emitIns_R_R_S_R(instruction ins, emitAttr attr, regNumber targetReg,
        regNumber op1Reg, regNumber op3Reg, int varx, int offs, insOpts instOptions = INS_OPTS_NONE)
    {
        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Register-stack-register instruction recording requires xarch.");
        }

        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }

        assert(isAvxBlendv(ins) || isAvx512Blendv(ins));
        assert(UseSimdEncoding());

        var ival = encodeRegAsIval(op3Reg);
        var id = emitNewInstrCns(attr, ival);
        id.idIns(ins);
        id.idReg1(targetReg);
        id.idReg2(op1Reg);
        id.idInsFmt(IF_XARCH_RWR_RRD_SRD_RRD);
        id.idAddr().iiaInitLclVarAddr(varx, unchecked((uint)offs));

        SetEvexBroadcastIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        var sz = emitInsSizeSV(id, insCodeRM(ins), varx, offs, ival);
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
    }

    public void emitIns_R_R_I(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2, int ival,
        insOpts instOptions = INS_OPTS_NONE)
    {
        if (Configuration.TARGET_ARM64)
        {
            emitIns_R_R_IArm64(ins, attr, reg1, reg2, (nint)ival, instOptions);
            return;
        }

        if (Configuration.TARGET_ARM)
        {
            emitIns_R_R_I(ins, attr, reg1, reg2, ival, INS_FLAGS_DONT_CARE, instOptions);
            return;
        }

        if (Configuration.TARGET_RISCV64)
        {
            emitIns_R_R_I_RiscV(ins, attr, reg1, reg2, ival, instOptions);
            return;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            emitInsRRILoongArch64(ins, attr, reg1, reg2, ival, instOptions);
            return;
        }

        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Two-register-immediate instruction recording requires xarch.");
        }

        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
            // Only mov reg,imm64 takes a full eight-byte immediate. Other instructions
            // use a sign-extended dword, which cannot hold an eight-byte relocation.
            noway_assert((EA_SIZE(attr) < EA_8BYTE) || !EA_IS_CNS_RELOC(attr));
        }

        var id = emitNewInstrSC(attr, ival);
        var useNDD = ((instOptions & INS_OPTS_EVEX_nd_MASK) != 0) && IsApxNddEncodableInstruction(ins);
        id.idIns(ins);
        id.idInsFmt(emitInsModeFormat(ins, IF_XARCH_RRD_RRD_CNS, useNDD));
        id.idReg1(reg1);
        id.idReg2(reg2);

        ulong code;
        if (hasCodeMR(ins))
        {
            code = insCodeMR(ins);
        }
        else if (hasCodeMI(ins))
        {
            code = insCodeMI(ins);
        }
        else
        {
            code = insCodeRM(ins);
        }

        assert((instOptions & INS_OPTS_EVEX_b_MASK) == 0);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        SetEvexNdIfNeeded(id, instOptions);
        var sz = emitInsSizeRR(id, code, ival);
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
    }

    private void emitIns_R_R_I_RiscV(
        instruction ins,
        emitAttr attr,
        regNumber reg1,
        regNumber reg2,
        nint imm,
        insOpts opt)
    {
        RequireRiscV64Target();

        var code = emitInsCodeRiscV64(ins);
        var id = emitNewInstr(attr);

        switch (GetMajorOpcode(code))
        {
            case MajorOpcode.OpImm:
            case MajorOpcode.OpImm32:
            case MajorOpcode.Load:
            case MajorOpcode.LoadFp:
            case MajorOpcode.Jalr:
            {
                assert(!InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_clz, INS_rev8)); // Encoded under OP-IMM but do not take an immediate.
                assert(isGeneralRegisterOrR0(reg2));
                code |= (unchecked((uint)reg1) & 0x1Fu) << 7; // rd
                code |= unchecked((uint)reg2) << 15; // rs1
                code |= unchecked((uint)imm) << 20; // imm
                break;
            }
            case MajorOpcode.Store:
            case MajorOpcode.StoreFp:
            {
                assert(isGeneralRegister(reg2));
                code |= (unchecked((uint)reg1) & 0x1Fu) << 20; // rs2
                code |= unchecked((uint)reg2) << 15;
                // S-type immediates are split across the upper and lower immediate fields.
                code |= ((unchecked((uint)(imm >> 5)) & 0x7Fu) << 25) |
                        ((unchecked((uint)imm) & 0x1Fu) << 7);
                break;
            }
            case MajorOpcode.System:
            {
                assert(ins is INS_csrrs or INS_csrrw or INS_csrrc);
                assert(isGeneralRegisterOrR0(reg1));
                assert(isGeneralRegisterOrR0(reg2));
                assert(isValidUimm12(imm));
                code |= unchecked((uint)reg1) << 7;
                code |= unchecked((uint)reg2) << 15;
                code |= unchecked((uint)imm) << 20;
                break;
            }
            default:
            {
                NYI_RISCV64("illegal ins within emitIns_R_R_I!");
                throw new FatalJitException(
                    CORJIT_SKIPPED, "Illegal instruction within RISC-V two-register-immediate instruction recording.");
            }
        }

        id.idIns(ins);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idSmallCns(imm);
        id.idAddr().iiaInstrEncode = code;
        id.idCodeSize(4);

        dispIns(id);
        appendToCurIG(id);
    }

    private void emitIns_R_I_I_RiscV(instruction ins, emitAttr attr, regNumber reg, nint imm1, nint imm2)
    {
        RequireRiscV64Target();

        var code = emitInsCodeRiscV64(ins);
        if (InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_csrrwi, INS_csrrci))
        {
            assert(isGeneralRegisterOrR0(reg));
            assert(isValidUimm5(imm1));
            assert(isValidUimm12(imm2));
            code |= unchecked((uint)reg) << 7;
            code |= unchecked((uint)imm1) << 15;
            code |= unchecked((uint)imm2) << 20;
        }
        else
        {
            NYI_RISCV64("illegal ins within emitIns_R_I_I!");
            throw new FatalJitException(
                CORJIT_SKIPPED, "Illegal instruction within RISC-V register-two-immediate instruction recording.");
        }

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idReg1(reg);
        id.idAddr().iiaInstrEncode = code;
        id.idCodeSize(4);

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitIns_R_R_R(instruction ins, emitAttr attr, regNumber targetReg, regNumber reg1, regNumber reg2,
        insOpts instOptions = INS_OPTS_NONE)
    {
        if (Configuration.TARGET_ARM64)
        {
            emitIns_R_R_RArm64(ins, attr, targetReg, reg1, reg2, instOptions);
            return;
        }

        if (Configuration.TARGET_ARM)
        {
            if (!insOptsNone(instOptions))
            {
                throw new FatalJitException(CORJIT_SKIPPED,
                    "ARM32 three-register instruction options are not ported.");
            }

            recordArm32InsRRR(ins, attr, targetReg, reg1, reg2, INS_FLAGS_DONT_CARE);
            return;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            emitInsRRRLoongArch64(ins, attr, targetReg, reg1, reg2, instOptions);
            return;
        }

        if (Configuration.TARGET_RISCV64)
        {
            RequireRiscV64Target();
        if (tryEmitCompressedIns_R_R_R(ins, attr, targetReg, reg1, reg2, instOptions))
        {
            return;
        }

        var code = emitInsCodeRiscV64(ins);
        if (IsRiscVThreeRegisterInstruction(ins))
        {
            // The source/data register for load-reserved must be empty.
            assert((ins != INS_lr_w && ins != INS_lr_d) || reg2 == REG_RISCV64_R0);

            code |= (unchecked((uint)targetReg) & 0x1Fu) << 7;
            code |= (unchecked((uint)reg1) & 0x1Fu) << 15;
            code |= (unchecked((uint)reg2) & 0x1Fu) << 20;
            if (ins is INS_fadd_s or INS_fsub_s or INS_fmul_s or INS_fdiv_s or
                INS_fadd_d or INS_fsub_d or INS_fmul_d or INS_fdiv_d)
            {
                code |= 0x7u << 12;
            }
            else if (ins is INS_sc_w or INS_sc_d)
            {
                // Release ordering ends the load-reserved/store-conditional loop.
                code |= 0b10u << 25;
            }
            else if (ins is INS_lr_w or INS_lr_d or INS_amoswap_w or INS_amoswap_d or INS_amoadd_w or INS_amoadd_d or
                     INS_amoxor_w or INS_amoxor_d or INS_amoand_w or INS_amoand_d or INS_amoor_w or INS_amoor_d or
                     INS_amomin_w or INS_amomin_d or INS_amomax_w or INS_amomax_d or INS_amominu_w or INS_amominu_d or
                     INS_amomaxu_w or INS_amomaxu_d)
            {
                // Interlocked APIs do not expose acquire/release ordering.
                code |= 0b11u << 25;
            }
        }
        else
        {
            NYI_RISCV64("illegal ins within emitIns_R_R_R!");
            throw new FatalJitException(
                CORJIT_SKIPPED, "Illegal instruction within RISC-V three-register instruction recording.");
        }

        var riscVId = emitNewInstr(attr);
        riscVId.idIns(ins);
        riscVId.idReg1(targetReg);
        riscVId.idReg2(reg1);
        riscVId.idReg3(reg2);
        riscVId.idAddr().iiaInstrEncode = code;
        riscVId.idCodeSize(4);

        dispIns(riscVId);
        appendToCurIG(riscVId);
            return;
        }

        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Three-register instruction recording requires xarch.");
        }

        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }

        assert(IsSimdInstruction(ins) || IsApxExtendedEvexInstruction(ins));
        assert(IsThreeOperandAVXInstruction(ins) || IsKInstruction(ins) || IsApxExtendedEvexInstruction(ins));

        // The ND slot is shared with other features, so check instruction compatibility.
        var useNDD = ((instOptions & INS_OPTS_EVEX_nd_MASK) != 0) && IsApxNddEncodableInstruction(ins);
        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idInsFmt((ins == INS_mulx) ? IF_XARCH_RWR_RWR_RRD : emitInsModeFormat(ins, IF_XARCH_RRD_RRD_RRD, useNDD));
        id.idReg1(targetReg);
        id.idReg2(reg1);
        id.idReg3(reg2);

        SetEvexEmbRoundIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        SetEvexNdIfNeeded(id, instOptions);
        SetEvexNfIfNeeded(id, instOptions);
        var sz = emitInsSizeRR(id, insCodeRM(ins));
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
    }

    private bool tryEmitCompressedIns_R_R_R(
        instruction ins,
        emitAttr attr,
        regNumber rd,
        regNumber rs1,
        regNumber rs2,
        insOpts opt)
    {
        RequireRiscV64Target();

        // RVC instructions are not emitted in prologs or epilogs.
        if (emitGeneratingPrologOrFuncletProlog() || emitGeneratingEpilogOrFuncletEpilog())
        {
            return false;
        }

        var compressedIns = tryGetCompressedIns_R_R_R(ins, attr, rd, rs1, rs2, opt);
        if (compressedIns == INS_none)
        {
            return false;
        }

        uint code;
        switch (compressedIns)
        {
            case INS_c_mv:
            case INS_c_add:
            {
                code = insEncodeCRTypeInstr(compressedIns, unchecked((uint)rd), unchecked((uint)rs2));
                break;
            }
            case INS_c_and:
            case INS_c_or:
            case INS_c_xor:
            case INS_c_sub:
            case INS_c_addw:
            case INS_c_subw:
            {
                var rdRvc = tryGetRvcRegisterNumber(rd);
                var rs2Rvc = tryGetRvcRegisterNumber(rs2);
                assert((rdRvc != uint.MaxValue) && (rs2Rvc != uint.MaxValue));
                code = insEncodeCATypeInstr(compressedIns, rdRvc, rs2Rvc);
                break;
            }
            default:
            {
                return false;
            }
        }

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idReg1(rd);
        id.idReg2(rs1);
        id.idReg3(rs2);
        id.idAddr().iiaInstrEncode = code;
        id.idCodeSize(2);

        dispIns(id);
        appendToCurIG(id);

        return true;
    }

    public void emitIns_R_R_R_I(instruction ins, emitAttr attr, regNumber targetReg,
        regNumber reg1, regNumber reg2, int ival, insOpts instOptions = INS_OPTS_NONE)
    {
        if (Configuration.TARGET_ARM64)
        {
            emitIns_R_R_R_IArm64(ins, attr, targetReg, reg1, reg2, (nint)ival, instOptions);
            return;
        }

        if (Configuration.TARGET_ARM)
        {
            emitIns_R_R_R_I(ins, attr, targetReg, reg1, reg2, ival, INS_FLAGS_DONT_CARE, instOptions);
            return;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            emitInsRRRIloongArch64(ins, attr, targetReg, reg1, reg2, ival, instOptions);
            return;
        }

        if (!Configuration.TARGET_XARCH)
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Three-register-immediate instruction recording requires xarch.");
        }

        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }

        assert(IsSimdInstruction(ins));
        assert(IsThreeOperandAVXInstruction(ins));

        var id = emitNewInstrCns(attr, ival);
        id.idIns(ins);
        id.idInsFmt(IF_XARCH_RWR_RRD_RRD_CNS);
        id.idReg1(targetReg);
        id.idReg2(reg1);
        id.idReg3(reg2);

        assert((instOptions & INS_OPTS_EVEX_b_MASK) == 0);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        var sz = emitInsSizeRR(id, insCodeRM(ins), ival);
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
    }
}
