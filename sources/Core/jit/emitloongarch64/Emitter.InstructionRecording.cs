// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.emitAttr;
using static RyuJitSharp.GCInfo.GCtype;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public partial class Emitter
{
    private void emitInsILoongArch64(instruction ins, emitAttr attr, nint imm)
    {
        var code = emitInsCodeLoongArch64(ins);

        switch (ins)
        {
            case INS_b:
            case INS_bl:
            {
                assert((imm & 0x3) == 0);
                code |= unchecked((uint)(imm >> 18)) & 0x3ff;
                code |= (unchecked((uint)(imm >> 2)) & 0xffff) << 10;
                break;
            }
            case INS_dbar:
            case INS_ibar:
            {
                assert((0 <= imm) && (imm <= 0x7fff));
                code |= unchecked((uint)imm) & 0x7fff;
                break;
            }
            default:
            {
                unreached();
                break;
            }
        }

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idAddr().iiaSetInstrEncode(code);
        id.idCodeSize(4);
        appendToCurIG(id);
    }

    private void emitInsIILoongArch64(instruction ins, emitAttr attr, nint cc, nint offs)
    {
        assert(ins is INS_bceqz or INS_bcnez);
        assert((offs & 0x3) == 0);
        assert((cc >> 3) == 0);

        var code = emitInsCodeLoongArch64(ins);
        code |= (unchecked((uint)cc) & 0x7) << 5;
        code |= (unchecked((uint)(offs >> 18)) & 0x1f);
        code |= (unchecked((uint)(offs >> 2)) & 0xffff) << 10;

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idAddr().iiaSetInstrEncode(code);
        id.idCodeSize(4);
        appendToCurIG(id);
    }

    private void emitInsJLoongArch64(instruction ins, BasicBlock? target, int instructionCount)
    {
        if (target is null)
        {
            assert(instructionCount != 0);
            assert(ins == INS_b);
            assert((-33554432 <= instructionCount) && (instructionCount < 33554432));
            emitIns_I(ins, EA_PTRSIZE, unchecked((nint)(instructionCount << 2)));
            return;
        }

        var compiler = _compiler;
        if (compiler is null)
        {
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.LoongArch64JumpRecordingRequiresAnActiveCompiler);
            return;
        }
        var currentBlock = compiler.compCurBB;
        if (currentBlock is null)
        {
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.LoongArch64JumpRecordingRequiresACurrentBasicBlock);
            return;
        }
        assert(target.HasFlag(BBF_HAS_LABEL));
        assert(InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_bceqz, INS_bl));

        var id = emitNewInstrJmp();
        id.idIns(ins);
        id.idReg1(unchecked((regNumber)(instructionCount & 0x1f)));
        id.idReg2(unchecked((regNumber)((instructionCount >> 5) & 0x1f)));
        id.idInsOpt(INS_OPTS_J);
        emitCounts_INS_OPTS_J = unchecked(emitCounts_INS_OPTS_J + 1);
        id.LoongArchBBlabel = target;
        ((instrDescJmp)id).idjTarget = target;
        id.idjShort = false;
        id.idjKeepLong = compiler.fgInDifferentRegions(currentBlock, target);
        if (Configuration.DEBUG && compiler.opts.compLongAddress)
        {
            id.idjKeepLong = true;
        }
        id.idjIG = emitCurIG;
        id.idjOffs = unchecked((uint)emitCurIGsize);
        id.idjNext = emitCurIGjmpList;
        emitCurIGjmpList = id;
        if (Configuration.EMITTER_STATS)
        {
            IncrementTotalIGjmps();
        }
        id.idCodeSize(4);
        appendToCurIG(id);
    }

    private void emitInsRLoongArchLabel(instruction ins, emitAttr attr, BasicBlock target, regNumber reg)
    {
        assert(target.HasFlag(BBF_HAS_LABEL));
        var compiler = _compiler;
        if (compiler is null)
        {
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.LoongArch64LabelRecordingRequiresAnActiveCompiler);
            return;
        }
        var id = emitNewInstr(attr);

        id.idIns(ins);
        id.idInsOpt(INS_OPTS_RL);
        id.LoongArchBBlabel = target;
        if (compiler.opts.compReloc)
        {
            id.idSetIsDspReloc();
            id.idCodeSize(8);
        }
        else
        {
            id.idCodeSize(12);
        }

        id.idReg1(reg);
        if (EA_IS_GCREF(attr))
        {
            id.idGCref(GCT_GCREF);
            id.idOpSize(EA_PTRSIZE);
        }
        else if (EA_IS_BYREF(attr))
        {
            id.idGCref(GCT_BYREF);
            id.idOpSize(EA_PTRSIZE);
        }

        if (Configuration.DEBUG)
        {
            var currentBlock = compiler.compCurBB;
            assert(currentBlock is not null);
            if (currentBlock.Kind == BBJ_EHCATCHRET)
            {
                var debugInfo = id.idDebugOnlyInfo();
                assert(debugInfo is not null);
                debugInfo.idCatchRet = true;
            }
        }
        appendToCurIG(id);
    }

    private void emitInsRLoongArchGroupLabel(instruction ins, emitAttr attr, insGroup target, regNumber reg)
    {
        assert(target is not null);
        var compiler = _compiler;
        if (compiler is null)
        {
            FatalJitException.ThrowFatalJitException(
        FatalJitErrorCode.LoongArch64InstructionGroupLabelRecordingRequiresAnActiveCompiler
    );
            return;
        }
        var id = emitNewInstr(attr);

        id.idIns(ins);
        id.idInsOpt(INS_OPTS_RL);
        id.LoongArchIGlabel = target;
        id.idSetIsBound();
        if (compiler.opts.compReloc)
        {
            id.idSetIsDspReloc();
            id.idCodeSize(8);
        }
        else
        {
            id.idCodeSize(12);
        }

        id.idReg1(reg);
        appendToCurIG(id);
    }

    private void emitInsSRLoongArch64(instruction ins, emitAttr attr, regNumber reg1, int varNum, int offset)
    {
        if (Configuration.DEBUG)
        {
            assert(ins is INS_st_d or INS_stx_d or INS_st_w or INS_stx_w or
                INS_fst_s or INS_fst_d or INS_fstx_s or INS_fstx_d or
                INS_st_b or INS_st_h or INS_stx_b or INS_stx_h);
        }

        var compiler = _compiler;
        if (compiler is null)
        {
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.LoongArch64StackStoresRequireAnActiveCompiler);
            return;
        }
        var baseOffset = compiler.lvaFrameAddress(varNum, out var framePointerBased);
        var imm = offset < 0 ? unchecked((nint)(-offset - 8)) : unchecked((nint)(baseOffset + offset));
        var frameReg = framePointerBased ? TargetRuntimeMetadata.FramePointerBaseRegister : TargetRuntimeMetadata.StackPointerBaseRegister;
        var reg2 = offset < 0 ? REG_LOONGARCH64_R21 : frameReg;
        offset = offset < 0 ? unchecked(-offset - 8) : offset;

        if (!isValidSimm12(imm))
        {
            var roundedImmediate = unchecked(imm + (imm & 0x800));
            assert(isValidSimm20(roundedImmediate >> 12));
            emitIns_R_I(INS_lu12i_w, EA_PTRSIZE, REG_LOONGARCH64_RA, roundedImmediate >> 12);
            emitIns_R_R_R(INS_add_d, EA_PTRSIZE, REG_LOONGARCH64_RA, REG_LOONGARCH64_RA, reg2);

            var lowImmediate = roundedImmediate & 0x7ff;
            imm = (imm & 0x800) != 0 ? lowImmediate - 0x800 : lowImmediate;
            reg2 = REG_LOONGARCH64_RA;
        }

        var id = emitNewInstr(attr);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idIns(ins);

        var code = emitInsCodeLoongArch64(ins);
        code |= unchecked((uint)reg1) & 0x1f;
        code |= unchecked((uint)reg2) << 5;
        if (ins is INS_stx_d or INS_stx_w or INS_stx_h or INS_stx_b or INS_fstx_s or INS_fstx_d)
        {
            code |= unchecked((uint)frameReg) << 10;
        }
        else
        {
            code |= (unchecked((uint)imm) & 0xfff) << 10;
        }

        id.idAddr().iiaSetInstrEncode(code);
        id.idAddr().iiaInitLclVarAddr(varNum, unchecked((uint)offset));
        id.idSetIsLclVar();
        id.idCodeSize(4);
        appendToCurIG(id);
    }

    private void emitInsRSLoongArch64(instruction ins, emitAttr attr, regNumber reg1, int varNum, int offset)
    {
        var size = EA_SIZE(attr);
        if (Configuration.DEBUG)
        {
            assert(ins is INS_ld_b or INS_ld_bu or INS_ld_h or INS_ld_hu or INS_ld_w or INS_ld_wu or
                INS_fld_s or INS_ld_d or INS_fld_d or INS_lea);
        }

        if (ins == INS_lea)
        {
            assert(size == EA_8BYTE);
        }

        var compiler = _compiler;
        if (compiler is null)
        {
            FatalJitException.ThrowFatalJitException(FatalJitErrorCode.LoongArch64StackLoadsRequireAnActiveCompiler);
            return;
        }
        var baseOffset = compiler.lvaFrameAddress(varNum, out var framePointerBased);
        var imm = offset < 0 ? unchecked((nint)(-offset - 8)) : unchecked((nint)(baseOffset + offset));
        var frameReg = framePointerBased ? TargetRuntimeMetadata.FramePointerBaseRegister : TargetRuntimeMetadata.StackPointerBaseRegister;
        var reg2 = offset < 0 ? REG_LOONGARCH64_R21 : frameReg;
        offset = offset < 0 ? unchecked(-offset - 8) : offset;

        reg1 = unchecked((regNumber)((uint)reg1 & 0x1f));
        uint code;
        if (isValidSimm12(imm))
        {
            if (ins == INS_lea)
            {
                ins = INS_addi_d;
            }

            code = emitInsCodeLoongArch64(ins);
            code |= unchecked((uint)reg1);
            code |= unchecked((uint)reg2) << 5;
            code |= (unchecked((uint)imm) & 0xfff) << 10;
        }
        else if (ins == INS_lea)
        {
            assert(isValidSimm20(imm >> 12));
            emitIns_R_I(INS_lu12i_w, EA_PTRSIZE, REG_LOONGARCH64_RA, imm >> 12);
            emitIns_R_R_I(INS_ori, EA_PTRSIZE, REG_LOONGARCH64_RA, REG_LOONGARCH64_RA, imm & 0xfff);

            ins = INS_add_d;
            code = emitInsCodeLoongArch64(ins);
            code |= unchecked((uint)reg1);
            code |= unchecked((uint)reg2) << 5;
            code |= unchecked((uint)REG_LOONGARCH64_RA) << 10;
        }
        else
        {
            var roundedImmediate = unchecked(imm + (imm & 0x800));
            assert(isValidSimm20(roundedImmediate >> 12));
            emitIns_R_I(INS_lu12i_w, EA_PTRSIZE, REG_LOONGARCH64_RA, roundedImmediate >> 12);
            emitIns_R_R_R(INS_add_d, EA_PTRSIZE, REG_LOONGARCH64_RA, REG_LOONGARCH64_RA, reg2);

            var lowImmediate = roundedImmediate & 0x7ff;
            var immediate = (imm & 0x800) != 0 ? lowImmediate - 0x800 : lowImmediate;
            code = emitInsCodeLoongArch64(ins);
            code |= unchecked((uint)reg1);
            code |= unchecked((uint)REG_LOONGARCH64_RA) << 5;
            code |= (unchecked((uint)immediate) & 0xfff) << 10;
        }

        var id = emitNewInstr(attr);
        id.idReg1(reg1);
        id.idIns(ins);
        id.idAddr().iiaSetInstrEncode(code);
        id.idAddr().iiaInitLclVarAddr(varNum, unchecked((uint)offset));
        id.idSetIsLclVar();
        id.idCodeSize(4);
        appendToCurIG(id);
    }

    private void emitInsRILoongArch64(
        instruction ins,
        emitAttr attr,
        regNumber reg,
        nint imm,
        insOpts instOptions)
    {
        assert(instOptions == INS_OPTS_NONE);
        var code = emitInsCodeLoongArch64(ins);

        switch (ins)
        {
            case INS_lu12i_w:
            case INS_lu32i_d:
            case INS_pcaddi:
            case INS_pcalau12i:
            case INS_pcaddu12i:
            case INS_pcaddu18i:
            {
                assert(isGeneralRegister(reg));
                assert((-524288 <= imm) && (imm < 524288));
                code |= unchecked((uint)reg);
                code |= (unchecked((uint)imm) & 0xfffff) << 5;
                break;
            }
            case INS_beqz:
            case INS_bnez:
            {
                assert(isGeneralRegisterOrR0(reg));
                assert((imm & 0x3) == 0);
                assert((-1048576 <= (imm >> 2)) && ((imm >> 2) <= 1048575));
                code |= (unchecked((uint)(imm >> 18)) & 0x1f);
                code |= unchecked((uint)reg) << 5;
                code |= (unchecked((uint)(imm >> 2)) & 0xffff) << 10;
                break;
            }
            case INS_movfr2cf:
            {
                assert(isFloatReg(reg));
                assert((0 <= imm) && (imm <= 7));
                code |= (unchecked((uint)reg) & 0x1f) << 5;
                code |= unchecked((uint)imm);
                break;
            }
            case INS_movcf2fr:
            {
                assert(isFloatReg(reg));
                assert((0 <= imm) && (imm <= 7));
                code |= unchecked((uint)reg) & 0x1f;
                code |= unchecked((uint)imm) << 5;
                break;
            }
            case INS_movgr2cf:
            {
                assert(isGeneralRegister(reg));
                assert((0 <= imm) && (imm <= 7));
                code |= unchecked((uint)reg) << 5;
                code |= unchecked((uint)imm);
                break;
            }
            case INS_movcf2gr:
            {
                assert(isGeneralRegister(reg));
                assert((0 <= imm) && (imm <= 7));
                code |= unchecked((uint)reg);
                code |= unchecked((uint)imm) << 5;
                break;
            }
            default:
            {
                unreached();
                break;
            }
        }

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idReg1(reg);
        id.idAddr().iiaSetInstrEncode(code);
        id.idCodeSize(4);
        appendToCurIG(id);
    }

    private void emitInsRRLLoongArch64(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2)
    {
        var code = emitInsCodeLoongArch64(ins);

        if (ins == INS_mov || (InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_ext_w_b, INS_cpucfg)))
        {
            assert(isGeneralRegisterOrR0(reg1));
            assert(isGeneralRegisterOrR0(reg2));
            code |= unchecked((uint)reg1);
            code |= unchecked((uint)reg2) << 5;
        }
        else if (InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_fabs_s, INS_fmov_d))
        {
            assert(isFloatReg(reg1));
            assert(isFloatReg(reg2));
            code |= unchecked((uint)reg1) & 0x1f;
            code |= (unchecked((uint)reg2) & 0x1f) << 5;
        }
        else if (InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_movgr2fr_w, INS_movgr2frh_w))
        {
            assert(isFloatReg(reg1));
            assert(isGeneralRegisterOrR0(reg2));
            code |= unchecked((uint)reg1) & 0x1f;
            code |= unchecked((uint)reg2) << 5;
        }
        else if (InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_movfr2gr_s, INS_movfrh2gr_s))
        {
            assert(isGeneralRegisterOrR0(reg1));
            assert(isFloatReg(reg2));
            code |= unchecked((uint)reg1);
            code |= (unchecked((uint)reg2) & 0x1f) << 5;
        }
        else if (ins is INS_dneg or INS_neg)
        {
            assert(isGeneralRegisterOrR0(reg1));
            assert(isGeneralRegisterOrR0(reg2));
            code |= unchecked((uint)reg1);
            code |= unchecked((uint)reg2) << 10;
        }
        else if (ins == INS_not)
        {
            assert(isGeneralRegisterOrR0(reg1));
            assert(isGeneralRegisterOrR0(reg2));
            code |= unchecked((uint)reg1);
            code |= unchecked((uint)reg2) << 5;
        }
        else if (((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_vreplgr2vr_b, INS_vreplgr2vr_d))) ||
                 ((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_xvreplgr2vr_b, INS_xvreplgr2vr_d))))
        {
            assert(isVectorRegister(reg1));
            assert(isGeneralRegisterOrR0(reg2));
            code |= unchecked((uint)reg1) & 0x1f;
            code |= unchecked((uint)reg2) << 5;
        }
        else if (((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_vclo_b, INS_vextl_qu_du))) ||
                 ((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_xvclo_b, INS_xvextl_qu_du))))
        {
            assert(isVectorRegister(reg1));
            assert(isVectorRegister(reg2));
            code |= unchecked((uint)reg1) & 0x1f;
            code |= (unchecked((uint)reg2) & 0x1f) << 5;
        }
        else
        {
            unreached();
        }

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idAddr().iiaSetInstrEncode(code);
        id.idCodeSize(4);
        appendToCurIG(id);
    }

    private void emitInsRRILoongArch64(
        instruction ins,
        emitAttr attr,
        regNumber reg1,
        regNumber reg2,
        nint imm,
        insOpts instOptions)
    {
        assert(instOptions == INS_OPTS_NONE);
        var code = emitInsCodeLoongArch64(ins);

        if (InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_slli_w, INS_rotri_w))
        {
            assert(ins is INS_slli_w or INS_srli_w or INS_srai_w or INS_rotri_w);
            assert(isGeneralRegister(reg1));
            assert(isGeneralRegisterOrR0(reg2));
            assert((0 <= imm) && (imm <= 0x1f));
            code |= unchecked((uint)reg1);
            code |= unchecked((uint)reg2) << 5;
            code |= (unchecked((uint)imm) & 0x1f) << 10;
        }
        else if (InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_slli_d, INS_rotri_d))
        {
            assert(ins is INS_slli_d or INS_srli_d or INS_srai_d or INS_rotri_d);
            assert(isGeneralRegister(reg1));
            assert(isGeneralRegisterOrR0(reg2));
            assert((0 <= imm) && (imm <= 0x3f));
            code |= unchecked((uint)reg1);
            code |= unchecked((uint)reg2) << 5;
            code |= (unchecked((uint)imm) & 0x3f) << 10;
        }
        else if (((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_addi_w, INS_xori))) ||
                 ((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_ld_b, INS_ld_wu))) ||
                 ((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_st_b, INS_st_d))))
        {
            assert(isGeneralRegister(reg1));
            assert(isGeneralRegisterOrR0(reg2));
            if (ins is INS_addi_w or INS_addi_d or INS_lu52i_d or INS_slti or
                INS_ld_b or INS_ld_h or INS_ld_w or INS_ld_d or INS_ld_bu or INS_ld_hu or INS_ld_wu or
                INS_st_b or INS_st_h or INS_st_w or INS_st_d)
            {
                assert((-2048 <= imm) && (imm <= 2047));
            }
            else if (ins == INS_sltui)
            {
                assert((0 <= imm) && (imm <= 0x7ff));
            }
            else
            {
                assert(ins is INS_andi or INS_ori or INS_xori);
                assert((0 <= imm) && (imm <= 0xfff));
            }
            code |= unchecked((uint)reg1);
            code |= unchecked((uint)reg2) << 5;
            code |= (unchecked((uint)imm) & 0xfff) << 10;
        }
        else if (InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_fld_s, INS_fst_d))
        {
            assert(ins is INS_fld_s or INS_fld_d or INS_fst_s or INS_fst_d);
            assert(isFloatReg(reg1));
            assert(isGeneralRegisterOrR0(reg2));
            assert((-2048 <= imm) && (imm <= 2047));
            code |= unchecked((uint)reg1) & 0x1f;
            code |= unchecked((uint)reg2) << 5;
            code |= (unchecked((uint)imm) & 0xfff) << 10;
        }
        else if ((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_ldptr_w, INS_ll_d)) ||
                 (InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_stptr_w, INS_sc_d)))
        {
            assert(ins is INS_ldptr_w or INS_ldptr_d or INS_ll_w or INS_ll_d or
                INS_stptr_w or INS_stptr_d or INS_sc_w or INS_sc_d);
            assert(isGeneralRegister(reg1));
            assert(isGeneralRegisterOrR0(reg2));
            assert((-8192 <= imm) && (imm <= 8191));
            code |= unchecked((uint)reg1);
            code |= unchecked((uint)reg2) << 5;
            code |= (unchecked((uint)imm) & 0x3fff) << 10;
        }
        else if (InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_beq, INS_bgeu))
        {
            assert(ins is INS_beq or INS_bne or INS_blt or INS_bltu or INS_bge or INS_bgeu);
            assert(isGeneralRegisterOrR0(reg1));
            assert(isGeneralRegisterOrR0(reg2));
            assert((imm & 0x3) == 0);
            assert((-32768 <= (imm >> 2)) && ((imm >> 2) <= 32767));
            code |= unchecked((uint)reg1) << 5;
            code |= unchecked((uint)reg2);
            code |= (unchecked((uint)(imm >> 2)) & 0xffff) << 10;
        }
        else if (InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_fcmp_caf_s, INS_fcmp_sune_s))
        {
            assert(isFloatReg(reg1));
            assert(isFloatReg(reg2));
            assert((0 <= imm) && (imm <= 7));
            code |= (unchecked((uint)reg1) & 0x1f) << 5;
            code |= (unchecked((uint)reg2) & 0x1f) << 10;
            code |= unchecked((uint)imm) & 0x7;
        }
        else if (ins == INS_addu16i_d || ins == INS_jirl)
        {
            assert(isGeneralRegister(reg1));
            assert(isGeneralRegisterOrR0(reg2));
            assert((-32768 <= imm) && (imm < 32768));
            code |= unchecked((uint)reg1);
            code |= unchecked((uint)reg2) << 5;
            code |= (unchecked((uint)imm) & 0xffff) << 10;
        }
        else if (TryEncodeLoongArchSimdRRI(ins, reg1, reg2, imm, ref code))
        {
        }
        else
        {
            unreached();
        }

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idAddr().iiaSetInstrEncode(code);
        id.idCodeSize(4);
        appendToCurIG(id);
    }

    private void emitInsRRRLoongArch64(
        instruction ins,
        emitAttr attr,
        regNumber reg1,
        regNumber reg2,
        regNumber reg3,
        insOpts instOptions)
    {
        assert(instOptions == INS_OPTS_NONE);
        var code = emitInsCodeLoongArch64(ins);

        if (((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_add_w, INS_crcc_w_d_w))) ||
            ((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_ldx_b, INS_ldle_d))) ||
            ((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_stx_b, INS_stle_d))))
        {
            assert(isGeneralRegister(reg1));
            assert(isGeneralRegisterOrR0(reg2));
            assert(isGeneralRegisterOrR0(reg3));
            code |= unchecked((uint)reg1);
            code |= unchecked((uint)reg2) << 5;
            code |= unchecked((uint)reg3) << 10;
        }
        else if (InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_amswap_w, INS_ammin_db_du))
        {
            assert(isGeneralRegister(reg1));
            assert(isGeneralRegisterOrR0(reg2));
            assert(isGeneralRegisterOrR0(reg3));
            code |= unchecked((uint)reg1);
            code |= unchecked((uint)reg2) << 10;
            code |= unchecked((uint)reg3) << 5;
        }
        else if (InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_fadd_s, INS_fcopysign_d))
        {
            assert(isFloatReg(reg1));
            assert(isFloatReg(reg2));
            assert(isFloatReg(reg3));
            code |= unchecked((uint)reg1) & 0x1f;
            code |= (unchecked((uint)reg2) & 0x1f) << 5;
            code |= (unchecked((uint)reg3) & 0x1f) << 10;
        }
        else if (InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_fldx_s, INS_fstle_d))
        {
            assert(isFloatReg(reg1));
            assert(isGeneralRegisterOrR0(reg2));
            assert(isGeneralRegisterOrR0(reg3));
            code |= unchecked((uint)reg1) & 0x1f;
            code |= unchecked((uint)reg2) << 5;
            code |= unchecked((uint)reg3) << 10;
        }
        else if (ins is INS_vldx or INS_vstx or INS_xvldx or INS_xvstx)
        {
            assert(isVectorRegister(reg1));
            assert(isGeneralRegisterOrR0(reg2));
            assert(isGeneralRegisterOrR0(reg3));
            code |= unchecked((uint)reg1) & 0x1f;
            code |= unchecked((uint)reg2) << 5;
            code |= unchecked((uint)reg3) << 10;
        }
        else if (((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_vreplve_b, INS_vreplve_d))) ||
                 ((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_xvreplve_b, INS_xvreplve_d))))
        {
            assert(isVectorRegister(reg1));
            assert(isVectorRegister(reg2));
            assert(isGeneralRegisterOrR0(reg3));
            code |= unchecked((uint)reg1) & 0x1f;
            code |= (unchecked((uint)reg2) & 0x1f) << 5;
            code |= unchecked((uint)reg3) << 10;
        }
        else if (((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_vfcmp_caf_s, INS_vshuf_d))) ||
                 ((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_xvfcmp_caf_s, INS_xvperm_w))))
        {
            assert(isVectorRegister(reg1));
            assert(isVectorRegister(reg2));
            assert(isVectorRegister(reg3));
            code |= unchecked((uint)reg1) & 0x1f;
            code |= (unchecked((uint)reg2) & 0x1f) << 5;
            code |= (unchecked((uint)reg3) & 0x1f) << 10;
        }
        else
        {
            unreached();
        }

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idReg3(reg3);
        id.idAddr().iiaSetInstrEncode(code);
        id.idCodeSize(4);
        appendToCurIG(id);
    }

    private void emitInsRRRIloongArch64(
        instruction ins,
        emitAttr attr,
        regNumber reg1,
        regNumber reg2,
        regNumber reg3,
        nint imm,
        insOpts instOptions)
    {
        assert(instOptions == INS_OPTS_NONE);
        var code = emitInsCodeLoongArch64(ins);

        if (InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_alsl_w, INS_bytepick_w))
        {
            assert(ins is INS_alsl_w or INS_alsl_wu or INS_alsl_d or INS_bytepick_w);
            assert(isGeneralRegister(reg1));
            assert(isGeneralRegisterOrR0(reg2));
            assert(isGeneralRegisterOrR0(reg3));
            assert((0 <= imm) && (imm <= 3));
            code |= unchecked((uint)reg1);
            code |= unchecked((uint)reg2) << 5;
            code |= unchecked((uint)reg3) << 10;
            code |= unchecked((uint)imm) << 15;
        }
        else if (ins == INS_bytepick_d)
        {
            assert(isGeneralRegister(reg1));
            assert(isGeneralRegisterOrR0(reg2));
            assert(isGeneralRegisterOrR0(reg3));
            assert((0 <= imm) && (imm <= 7));
            code |= unchecked((uint)reg1);
            code |= unchecked((uint)reg2) << 5;
            code |= unchecked((uint)reg3) << 10;
            code |= unchecked((uint)imm) << 15;
        }
        else if (ins == INS_fsel)
        {
            assert(isFloatReg(reg1));
            assert(isFloatReg(reg2));
            assert(isFloatReg(reg3));
            assert((0 <= imm) && (imm <= 7));
            code |= unchecked((uint)reg1) & 0x1f;
            code |= (unchecked((uint)reg2) & 0x1f) << 5;
            code |= (unchecked((uint)reg3) & 0x1f) << 10;
            code |= unchecked((uint)imm) << 15;
        }
        else
        {
            unreached();
        }

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idReg3(reg3);
        id.idAddr().iiaSetInstrEncode(code);
        id.idCodeSize(4);
        appendToCurIG(id);
    }

    private void emitInsRRIIloongArch64(
        instruction ins,
        emitAttr attr,
        regNumber reg1,
        regNumber reg2,
        nint imm1,
        nint imm2,
        insOpts instOptions)
    {
        assert(instOptions == INS_OPTS_NONE);
        var code = emitInsCodeLoongArch64(ins);

        assert(isGeneralRegisterOrR0(reg1));
        assert(isGeneralRegisterOrR0(reg2));

        switch (ins)
        {
            case INS_bstrins_w:
            case INS_bstrpick_w:
            {
                assert((0 <= imm2) && (imm2 <= imm1) && (imm1 < 32));
                code |= unchecked((uint)reg1);
                code |= unchecked((uint)reg2) << 5;
                code |= (unchecked((uint)imm1) & 0x1f) << 16;
                code |= (unchecked((uint)imm2) & 0x1f) << 10;
                break;
            }
            case INS_bstrins_d:
            case INS_bstrpick_d:
            {
                assert((0 <= imm2) && (imm2 <= imm1) && (imm1 < 64));
                code |= unchecked((uint)reg1);
                code |= unchecked((uint)reg2) << 5;
                code |= (unchecked((uint)imm1) & 0x3f) << 16;
                code |= (unchecked((uint)imm2) & 0x3f) << 10;
                break;
            }
            case INS_vstelm_d:
            case INS_vstelm_w:
            case INS_vstelm_h:
            case INS_vstelm_b:
            case INS_xvstelm_d:
            case INS_xvstelm_w:
            case INS_xvstelm_h:
            case INS_xvstelm_b:
            {
                assert(isVectorRegister(reg1));
                assert(isGeneralRegisterOrR0(reg2));
                assert((-128 <= imm1) && (imm1 <= 127));
                code |= unchecked((uint)reg1) & 0x1f;
                code |= unchecked((uint)reg2) << 5;
                code |= (unchecked((uint)imm1) & 0xff) << 10;
                var indexMask = ins switch {
                    INS_vstelm_d => 0x1,
                    INS_vstelm_w or INS_xvstelm_d => 0x3,
                    INS_vstelm_h or INS_xvstelm_w => 0x7,
                    INS_vstelm_b or INS_xvstelm_h => 0xf,
                    INS_xvstelm_b => 0x1f,
                    _ => 0,
                };
                assert((0 <= imm2) && (unchecked((uint)imm2) <= indexMask));
                code |= (unchecked((uint)imm2) & unchecked((uint)indexMask)) << 18;
                break;
            }
            default:
            {
                unreached();
                break;
            }
        }

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idAddr().iiaSetInstrEncode(code);
        id.idCodeSize(4);
        appendToCurIG(id);
    }

    private void emitInsRRRRIloongArch64(
        instruction ins,
        emitAttr attr,
        regNumber reg1,
        regNumber reg2,
        regNumber reg3,
        regNumber reg4)
    {
        var code = emitInsCodeLoongArch64(ins);

        switch (ins)
        {
            case INS_fmadd_s:
            case INS_fmadd_d:
            case INS_fmsub_s:
            case INS_fmsub_d:
            case INS_fnmadd_s:
            case INS_fnmadd_d:
            case INS_fnmsub_s:
            case INS_fnmsub_d:
            {
                assert(isFloatReg(reg1));
                assert(isFloatReg(reg2));
                assert(isFloatReg(reg3));
                assert(isFloatReg(reg4));
                code |= unchecked((uint)reg1) & 0x1f;
                code |= (unchecked((uint)reg2) & 0x1f) << 5;
                code |= (unchecked((uint)reg3) & 0x1f) << 10;
                code |= (unchecked((uint)reg4) & 0x1f) << 15;
                break;
            }
            case INS_vfmadd_s:
            case INS_vfmadd_d:
            case INS_vfmsub_s:
            case INS_vfmsub_d:
            case INS_vfnmadd_s:
            case INS_vfnmadd_d:
            case INS_vfnmsub_s:
            case INS_vfnmsub_d:
            case INS_vbitsel_v:
            case INS_vshuf_b:
            case INS_xvfmadd_s:
            case INS_xvfmadd_d:
            case INS_xvfmsub_s:
            case INS_xvfmsub_d:
            case INS_xvfnmadd_s:
            case INS_xvfnmadd_d:
            case INS_xvfnmsub_s:
            case INS_xvfnmsub_d:
            case INS_xvbitsel_v:
            case INS_xvshuf_b:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isVectorRegister(reg4));
                code |= unchecked((uint)reg1) & 0x1f;
                code |= (unchecked((uint)reg2) & 0x1f) << 5;
                code |= (unchecked((uint)reg3) & 0x1f) << 10;
                code |= (unchecked((uint)reg4) & 0x1f) << 15;
                break;
            }
            default:
            {
                unreached();
                break;
            }
        }

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idReg1(reg1);
        id.idAddr().iiaSetInstrEncode(code);
        id.idCodeSize(4);
        appendToCurIG(id);
    }

    private static bool TryEncodeLoongArchSimdRRI(
        instruction ins,
        regNumber reg1,
        regNumber reg2,
        nint imm,
        ref uint code)
    {
        uint immediateMask;
        bool isSigned;
        bool isVectorDestination;

        if (((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_vseqi_b, INS_vmini_d))) ||
            ((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_xvseqi_b, INS_xvmin_d))))
        {
            immediateMask = 0x1f;
            isSigned = true;
            isVectorDestination = true;
        }
        else if ((ins == INS_vldrepl_d) || (ins == INS_xvldrepl_d))
        {
            immediateMask = 0x1ff;
            isSigned = true;
            isVectorDestination = true;
        }
        else if ((ins == INS_vldrepl_w) || (ins == INS_xvldrepl_w))
        {
            immediateMask = 0x3ff;
            isSigned = true;
            isVectorDestination = true;
        }
        else if ((ins == INS_vldrepl_h) || (ins == INS_xvldrepl_h))
        {
            immediateMask = 0x7ff;
            isSigned = true;
            isVectorDestination = true;
        }
        else if (((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_vld, INS_vst))) ||
                 ((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_xvld, INS_xvst))))
        {
            immediateMask = 0xfff;
            isSigned = true;
            isVectorDestination = true;
        }
        else if (((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_vinsgr2vr_d, INS_vpickve2gr_du))) ||
                 (ins == INS_xvrepl128vei_d))
        {
            immediateMask = 0x1;
            isSigned = false;
            isVectorDestination = ins is INS_vinsgr2vr_d or INS_vreplvei_d or INS_xvrepl128vei_d;
        }
        else if (((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_vpickve2gr_w, INS_vreplvei_w))) ||
                 ((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_xvinsve0_d, INS_xvpickve2gr_du))))
        {
            immediateMask = 0x3;
            isSigned = false;
            isVectorDestination = ins is INS_vinsgr2vr_w or INS_vreplvei_w or
                INS_xvinsve0_d or INS_xvinsve0_w or INS_xvinsgr2vr_d;
        }
        else if (((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_vslli_b, INS_vpickve2gr_hu))) ||
                 ((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_xvpickve2gr_w, INS_xvsat_bu))))
        {
            immediateMask = 0x7;
            isSigned = false;
            isVectorDestination = ins is not (INS_vpickve2gr_h or INS_vpickve2gr_hu or
                INS_xvpickve2gr_w or INS_xvpickve2gr_wu);
        }
        else if (((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_vpickve2gr_b, INS_vreplvei_b))) ||
                 ((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_xvslli_h, INS_xvsat_hu))))
        {
            immediateMask = 0xf;
            isSigned = false;
            isVectorDestination = ins is not (INS_vpickve2gr_b or INS_vpickve2gr_bu);
        }
        else if (((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_vslei_bu, INS_vsat_wu))) ||
                 ((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_xvslei_bu, INS_xvsat_wu))))
        {
            immediateMask = 0x1f;
            isSigned = false;
            isVectorDestination = true;
        }
        else if (((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_vslli_d, INS_vsat_du))) ||
                 ((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_xvslli_d, INS_xvsat_du))))
        {
            immediateMask = 0x3f;
            isSigned = false;
            isVectorDestination = true;
        }
        else if (((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_vsrlni_d_q, INS_vssrarni_du_q))) ||
                 ((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_xvsrlni_d_q, INS_xvssrarni_du_q))))
        {
            immediateMask = 0x7f;
            isSigned = false;
            isVectorDestination = true;
        }
        else if (((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_vextrins_d, INS_vpermi_w))) ||
                 ((InstructionOrdinalMaps.IsNativeInstructionOrdinalInRange(ins, INS_xvextrins_d, INS_xvpermi_q))))
        {
            immediateMask = 0xff;
            isSigned = false;
            isVectorDestination = true;
        }
        else
        {
            return false;
        }

        assert(isVectorRegister(isVectorDestination ? reg1 : reg2));
        if (isSigned)
        {
            var signBit = unchecked((nint)(immediateMask + 1)) / 2;
            assert((-signBit <= imm) && (imm < signBit));
        }
        else
        {
            assert((0 <= imm) && (unchecked((uint)imm) <= immediateMask));
        }

        code |= unchecked((uint)reg1) & 0x1f;
        code |= (unchecked((uint)reg2) & 0x1f) << 5;
        code |= (unchecked((uint)imm) & immediateMask) << 10;
        return true;
    }
    public void emitIns_MovLoongArch64(instruction ins, emitAttr attr, regNumber dstReg, regNumber srcReg, bool canSkip)
    {
        RequireLoongArch64Target();

        assert(IsMovInstruction(ins));
        if (canSkip && dstReg == srcReg)
        {
            return;
        }

        if (Configuration.FEATURE_SIMD && isVectorRegister(dstReg))
        {
            assert(attr <= EA_32BYTE);
            if (isVectorRegister(srcReg))
            {
                emitIns_R_R_I(attr == EA_32BYTE ? INS_xvbsll_v : INS_vbsll_v, attr, dstReg, srcReg, 0);
            }
            else
            {
                emitIns_R_R(ins, attr, dstReg, srcReg);
            }
        }
        else
            if (attr == EA_4BYTE && ins == INS_mov)
            {
                emitIns_R_R_I(INS_slli_w, attr, dstReg, srcReg, 0);
            }
            else
            {
                emitIns_R_R(ins, attr, dstReg, srcReg);
            }
    }
}
