// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe void emitIns_R_R_C(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        CORINFO_FIELD_HANDLE fldHnd, int offs, insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Two-register static-field instruction recording requires xarch.");
#else
#if TARGET_AMD64
        RequireSupportedInstructionRecording();
#endif
        assert(IsSimdInstruction(ins));
        assert(IsThreeOperandAVXInstruction(ins));

        if (!jitStaticFldIsGlobAddr(fldHnd))
        {
            attr |= EA_DSP_RELOC_FLG;
        }

        var id = emitNewInstrDsp(attr, offs);
        id.idIns(ins);
        id.idInsFmt((ins == INS_mulx) ? IF_RWR_RWR_MRD : emitInsModeFormat(ins, IF_RRD_RRD_MRD));
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idAddr().iiaFieldHnd = fldHnd;

        SetEvexBroadcastIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        var sz = emitInsSizeCV(id, insCodeRM(ins));
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }

    public void emitIns_R_R_S(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        int varx, int offs, insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Two-register stack instruction recording requires xarch.");
#else
#if TARGET_AMD64
        RequireSupportedInstructionRecording();
#endif
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

    public unsafe void emitIns_R_R_C_I(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        CORINFO_FIELD_HANDLE fldHnd, int offs, int ival, insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Two-register field-immediate recording requires xarch.");
#else
#if TARGET_AMD64
        RequireSupportedInstructionRecording();
#endif
        assert(IsSimdInstruction(ins));
        assert(IsThreeOperandAVXInstruction(ins));

        if (!jitStaticFldIsGlobAddr(fldHnd))
        {
            attr |= EA_DSP_RELOC_FLG;
        }

        var id = emitNewInstrCnsDsp(attr, ival, offs);
        id.idIns(ins);
        id.idInsFmt(IF_RWR_RRD_MRD_CNS);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idAddr().iiaFieldHnd = fldHnd;

        SetEvexBroadcastIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        var sz = emitInsSizeCV(id, insCodeRM(ins), ival);
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }

    public void emitIns_R_R_S_I(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        int varx, int offs, int ival, insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Two-register stack-immediate recording requires xarch.");
#else
#if TARGET_AMD64
        RequireSupportedInstructionRecording();
#endif
        assert(IsSimdInstruction(ins));
        assert(IsThreeOperandAVXInstruction(ins));

        var id = emitNewInstrCns(attr, ival);
        id.idIns(ins);
        id.idInsFmt(IF_RWR_RRD_SRD_CNS);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idAddr().iiaLclVar.initLclVarAddr(varx, unchecked((uint)offs));

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
#endif
    }

    public unsafe void emitIns_R_R_C_R(instruction ins, emitAttr attr, regNumber targetReg,
        regNumber op1Reg, regNumber op3Reg, CORINFO_FIELD_HANDLE fldHnd, int offs, insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Register-field-register instruction recording requires xarch.");
#else
#if TARGET_AMD64
        RequireSupportedInstructionRecording();
#endif
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
        id.idInsFmt(IF_RWR_RRD_MRD_RRD);
        id.idAddr().iiaFieldHnd = fldHnd;

        SetEvexBroadcastIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        var sz = emitInsSizeCV(id, insCodeRM(ins), ival);
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }

    public void emitIns_R_R_S_R(instruction ins, emitAttr attr, regNumber targetReg,
        regNumber op1Reg, regNumber op3Reg, int varx, int offs, insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Register-stack-register instruction recording requires xarch.");
#else
#if TARGET_AMD64
        RequireSupportedInstructionRecording();
#endif
        assert(isAvxBlendv(ins) || isAvx512Blendv(ins));
        assert(UseSimdEncoding());

        var ival = encodeRegAsIval(op3Reg);
        var id = emitNewInstrCns(attr, ival);
        id.idIns(ins);
        id.idReg1(targetReg);
        id.idReg2(op1Reg);
        id.idInsFmt(IF_RWR_RRD_SRD_RRD);
        id.idAddr().iiaLclVar.initLclVarAddr(varx, unchecked((uint)offs));

        SetEvexBroadcastIfNeeded(id, instOptions);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        var sz = emitInsSizeSV(id, insCodeRM(ins), varx, offs, ival);
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }

#if !TARGET_ARM64
    public void emitIns_R_R_I(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2, int ival,
        insOpts instOptions = INS_OPTS_NONE)
    {
#if TARGET_RISCV64
        emitIns_R_R_I_RiscV(ins, attr, reg1, reg2, ival, instOptions);
#elif !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Two-register-immediate instruction recording requires xarch.");
#else
#if TARGET_AMD64
        RequireSupportedInstructionRecording();
        // Only mov reg,imm64 takes a full eight-byte immediate. Other instructions
        // use a sign-extended dword, which cannot hold an eight-byte relocation.
        noway_assert((EA_SIZE(attr) < EA_8BYTE) || !EA_IS_CNS_RELOC(attr));
#endif
        var id = emitNewInstrSC(attr, ival);
        var useNDD = ((instOptions & INS_OPTS_EVEX_nd_MASK) != 0) && IsApxNddEncodableInstruction(ins);
        id.idIns(ins);
        id.idInsFmt(emitInsModeFormat(ins, IF_RRD_RRD_CNS, useNDD));
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
#endif
#endif
    }

#if TARGET_RISCV64
    private void emitIns_R_R_I_RiscV(
        instruction ins,
        emitAttr attr,
        regNumber reg1,
        regNumber reg2,
        nint imm,
        insOpts opt)
    {
        var code = emitInsCode(ins);
        var id = emitNewInstr(attr);

        switch (GetMajorOpcode(code))
        {
            case MajorOpcode.OpImm:
            case MajorOpcode.OpImm32:
            case MajorOpcode.Load:
            case MajorOpcode.LoadFp:
            case MajorOpcode.Jalr:
            {
                assert(!(INS_clz <= ins && ins <= INS_rev8)); // Encoded under OP-IMM but do not take an immediate.
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
#endif

#if !TARGET_ARM64
    public void emitIns_R_R_R(instruction ins, emitAttr attr, regNumber targetReg, regNumber reg1, regNumber reg2,
        insOpts instOptions = INS_OPTS_NONE)
    {
#if TARGET_ARM
        if (!insOptsNone(instOptions))
        {
            throw new FatalJitException(CORJIT_SKIPPED, "ARM32 three-register instruction options are not ported.");
        }

        recordArm32InsRRR(ins, attr, targetReg, reg1, reg2, INS_FLAGS_DONT_CARE);
#elif TARGET_RISCV64
        if (tryEmitCompressedIns_R_R_R(ins, attr, targetReg, reg1, reg2, instOptions))
        {
            return;
        }

        var code = emitInsCode(ins);
        var instructionValue = (uint)ins;
        if (((uint)INS_add <= instructionValue && instructionValue <= (uint)INS_and) ||
            ((uint)INS_mul <= instructionValue && instructionValue <= (uint)INS_remuw) ||
            ((uint)INS_addw <= instructionValue && instructionValue <= (uint)INS_sraw) ||
            ((uint)INS_fadd_s <= instructionValue && instructionValue <= (uint)INS_fmax_s) ||
            ((uint)INS_fadd_d <= instructionValue && instructionValue <= (uint)INS_fmax_d) ||
            ((uint)INS_feq_s <= instructionValue && instructionValue <= (uint)INS_fle_s) ||
            ((uint)INS_feq_d <= instructionValue && instructionValue <= (uint)INS_fle_d) ||
            ((uint)INS_lr_w <= instructionValue && instructionValue <= (uint)INS_amomaxu_d) ||
            ((uint)INS_sh1add <= instructionValue && instructionValue <= (uint)INS_sh3add_uw) ||
            ((uint)INS_rol <= instructionValue && instructionValue <= (uint)INS_maxu) ||
            ((uint)INS_bset <= instructionValue && instructionValue <= (uint)INS_binv) ||
            ((uint)INS_czero_eqz <= instructionValue && instructionValue <= (uint)INS_czero_nez))
        {
#if DEBUG
            switch (ins)
            {
                case INS_add:
                case INS_sub:
                case INS_sll:
                case INS_slt:
                case INS_sltu:
                case INS_xor:
                case INS_srl:
                case INS_sra:
                case INS_or:
                case INS_and:
                case INS_addw:
                case INS_subw:
                case INS_sllw:
                case INS_srlw:
                case INS_sraw:
                case INS_mul:
                case INS_mulh:
                case INS_mulhsu:
                case INS_mulhu:
                case INS_div:
                case INS_divu:
                case INS_rem:
                case INS_remu:
                case INS_mulw:
                case INS_divw:
                case INS_divuw:
                case INS_remw:
                case INS_remuw:
                {
                    break;
                }
                case INS_fadd_s:
                case INS_fsub_s:
                case INS_fmul_s:
                case INS_fdiv_s:
                case INS_fsgnj_s:
                case INS_fsgnjn_s:
                case INS_fsgnjx_s:
                case INS_fmin_s:
                case INS_fmax_s:
                case INS_feq_s:
                case INS_flt_s:
                case INS_fle_s:
                case INS_fadd_d:
                case INS_fsub_d:
                case INS_fmul_d:
                case INS_fdiv_d:
                case INS_fsgnj_d:
                case INS_fsgnjn_d:
                case INS_fsgnjx_d:
                case INS_fmin_d:
                case INS_fmax_d:
                case INS_feq_d:
                case INS_flt_d:
                case INS_fle_d:
                {
                    break;
                }
                case INS_lr_w:
                case INS_lr_d:
                case INS_sc_w:
                case INS_sc_d:
                case INS_amoswap_w:
                case INS_amoswap_d:
                case INS_amoadd_w:
                case INS_amoadd_d:
                case INS_amoxor_w:
                case INS_amoxor_d:
                case INS_amoand_w:
                case INS_amoand_d:
                case INS_amoor_w:
                case INS_amoor_d:
                case INS_amomin_w:
                case INS_amomin_d:
                case INS_amomax_w:
                case INS_amomax_d:
                case INS_amominu_w:
                case INS_amominu_d:
                case INS_amomaxu_w:
                case INS_amomaxu_d:
                {
                    break;
                }
                case INS_sh1add:
                case INS_sh2add:
                case INS_sh3add:
                case INS_add_uw:
                case INS_sh1add_uw:
                case INS_sh2add_uw:
                case INS_sh3add_uw:
                case INS_rol:
                case INS_rolw:
                case INS_ror:
                case INS_rorw:
                case INS_xnor:
                case INS_orn:
                case INS_andn:
                case INS_min:
                case INS_minu:
                case INS_max:
                case INS_maxu:
                case INS_bset:
                case INS_bclr:
                case INS_bext:
                case INS_binv:
                case INS_czero_eqz:
                case INS_czero_nez:
                {
                    break;
                }
                default:
                {
                    NYI_RISCV64("illegal ins within emitIns_R_R_R!");
                    throw new FatalJitException(
                        CORJIT_SKIPPED, "Illegal instruction within RISC-V three-register instruction recording.");
                }
            }
#endif
            // The source/data register for load-reserved must be empty.
            assert((ins != INS_lr_w && ins != INS_lr_d) || reg2 == REG_R0);

            code |= (unchecked((uint)targetReg) & 0x1Fu) << 7;
            code |= (unchecked((uint)reg1) & 0x1Fu) << 15;
            code |= (unchecked((uint)reg2) & 0x1Fu) << 20;
            if (((uint)INS_fadd_s <= instructionValue && instructionValue <= (uint)INS_fdiv_s) ||
                ((uint)INS_fadd_d <= instructionValue && instructionValue <= (uint)INS_fdiv_d))
            {
                code |= 0x7u << 12;
            }
            else if (ins is INS_sc_w or INS_sc_d)
            {
                // Release ordering ends the load-reserved/store-conditional loop.
                code |= 0b10u << 25;
            }
            else if (ins is INS_lr_w or INS_lr_d ||
                     ((uint)INS_amoswap_w <= instructionValue && instructionValue <= (uint)INS_amomaxu_d))
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

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idReg1(targetReg);
        id.idReg2(reg1);
        id.idReg3(reg2);
        id.idAddr().iiaInstrEncode = code;
        id.idCodeSize(4);

        dispIns(id);
        appendToCurIG(id);
#elif !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Three-register instruction recording requires xarch.");
#else
#if TARGET_AMD64
        RequireSupportedInstructionRecording();
#endif
        assert(IsSimdInstruction(ins) || IsApxExtendedEvexInstruction(ins));
        assert(IsThreeOperandAVXInstruction(ins) || IsKInstruction(ins) || IsApxExtendedEvexInstruction(ins));

        // The ND slot is shared with other features, so check instruction compatibility.
        var useNDD = ((instOptions & INS_OPTS_EVEX_nd_MASK) != 0) && IsApxNddEncodableInstruction(ins);
        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idInsFmt((ins == INS_mulx) ? IF_RWR_RWR_RRD : emitInsModeFormat(ins, IF_RRD_RRD_RRD, useNDD));
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
#endif
    }

#if TARGET_RISCV64
    private bool tryEmitCompressedIns_R_R_R(
        instruction ins,
        emitAttr attr,
        regNumber rd,
        regNumber rs1,
        regNumber rs2,
        insOpts opt)
    {
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
#endif

    public void emitIns_R_R_R_I(instruction ins, emitAttr attr, regNumber targetReg,
        regNumber reg1, regNumber reg2, int ival, insOpts instOptions = INS_OPTS_NONE)
    {
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Three-register-immediate instruction recording requires xarch.");
#else
#if TARGET_AMD64
        RequireSupportedInstructionRecording();
#endif
        assert(IsSimdInstruction(ins));
        assert(IsThreeOperandAVXInstruction(ins));

        var id = emitNewInstrCns(attr, ival);
        id.idIns(ins);
        id.idInsFmt(IF_RWR_RRD_RRD_CNS);
        id.idReg1(targetReg);
        id.idReg2(reg1);
        id.idReg3(reg2);

        assert((instOptions & INS_OPTS_EVEX_b_MASK) == 0);
        SetEvexEmbMaskIfNeeded(id, instOptions);
        var sz = emitInsSizeRR(id, insCodeRM(ins), ival);
        id.idCodeSize(sz);

        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)sz);
#endif
    }
#endif
}
