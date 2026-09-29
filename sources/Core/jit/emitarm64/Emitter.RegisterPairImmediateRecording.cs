// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public unsafe partial class Emitter
{
    public void emitIns_R_R_I(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        nint imm, insOpts opt = INS_OPTS_NONE, insScalableOpts sopt = insScalableOpts.INS_SCALABLE_OPTS_NONE)
    {
        assert(_compiler is not null);
        var size = EA_SIZE(attr);
        var elemsize = EA_UNKNOWN;
        var fmt = IF_NONE;
        var isLdSt = false;
        var isLdrStr = false;
        var isSIMD = false;
        var isAddSub = false;
        var setFlags = false;
        uint scale = 0;
        var unscaledOp = false;

        switch (ins)
        {
            case INS_mov:
            {
                // Vector MOV aliases select insertion, duplication or extraction.
                assert(insOptsNone(opt));
                assert(isValidVectorElemsize(size));
                elemsize = size;
                assert(isValidVectorIndex(EA_16BYTE, elemsize, imm));

                if (isVectorRegister(reg1))
                {
                    if (isGeneralRegisterOrZR(reg2))
                    {
                        fmt = IF_DV_2C;
                        break;
                    }
                    else if (isVectorRegister(reg2))
                    {
                        fmt = IF_DV_2E;
                        break;
                    }
                }
                else
                {
                    assert(isGeneralRegister(reg1));
                    if (isVectorRegister(reg2))
                    {
                        fmt = IF_DV_2B;
                        break;
                    }
                }
                assert(false, "invalid INS_mov operands");
                break;
            }

            case INS_lsl:
            case INS_lsr:
            case INS_asr:
            {
                assert(insOptsNone(opt));
                assert(isValidGeneralDatasize(size));
                assert(isGeneralRegister(reg1));
                assert(isGeneralRegister(reg2));
                assert(isValidImmShift(imm, size));
                fmt = IF_DI_2D;
                break;
            }

            case INS_ror:
            {
                assert(insOptsNone(opt));
                assert(isValidGeneralDatasize(size));
                assert(isGeneralRegister(reg1));
                assert(isGeneralRegister(reg2));
                assert(isValidImmShift(imm, size));
                fmt = IF_DI_2B;
                break;
            }

            case INS_shl:
            case INS_sli:
            case INS_sri:
            case INS_srshr:
            case INS_srsra:
            case INS_sshr:
            case INS_ssra:
            case INS_urshr:
            case INS_ursra:
            case INS_ushr:
            case INS_usra:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                var isRightShift = emitInsIsVectorRightShift(ins);
                assert(!isRightShift || (imm != 0),
                    "instructions for vector right-shift do not allow zero as an immediate value");

                if (insOptsAnyArrangement(opt))
                {
                    assert(isValidVectorDatasize(size));
                    assert(isValidArrangement(size, opt));
                    elemsize = optGetElemsize(opt);
                    assert(isValidVectorElemsize(elemsize));
                    assert(isValidVectorShiftAmount(imm, elemsize, isRightShift));
                    assert(opt != INS_OPTS_1D); // Reserved encoding.
                    fmt = IF_DV_2O;
                    break;
                }
                else
                {
                    assert(insOptsNone(opt));
                    assert(size == EA_8BYTE);
                    assert(isValidVectorShiftAmount(imm, size, isRightShift));
                    fmt = IF_DV_2N;
                }
                break;
            }

            case INS_sqshl:
            case INS_uqshl:
            case INS_sqshlu:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                var isRightShift = emitInsIsVectorRightShift(ins);
                if (insOptsAnyArrangement(opt))
                {
                    assert(isValidArrangement(size, opt));
                    assert(opt != INS_OPTS_1D); // immh = 1xxx, Q = 0 is reserved.
                    elemsize = optGetElemsize(opt);
                    assert(isValidVectorShiftAmount(imm, elemsize, isRightShift));
                    fmt = IF_DV_2O;
                }
                else
                {
                    assert(insOptsNone(opt));
                    assert(isValidVectorElemsize(size));
                    assert(isValidVectorShiftAmount(imm, size, isRightShift));
                    fmt = IF_DV_2N;
                }
                break;
            }

            case INS_sqrshrn:
            case INS_sqrshrun:
            case INS_sqshrn:
            case INS_sqshrun:
            case INS_uqrshrn:
            case INS_uqshrn:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                var isRightShift = emitInsIsVectorRightShift(ins);
                if (insOptsAnyArrangement(opt))
                {
                    assert(isValidArrangement(size, opt));
                    assert((opt != INS_OPTS_1D) && (opt != INS_OPTS_2D)); // immh = 1xxx is reserved.
                    elemsize = optGetElemsize(opt);
                    assert(isValidVectorShiftAmount(imm, elemsize, isRightShift));
                    fmt = IF_DV_2O;
                }
                else
                {
                    assert(insOptsNone(opt));
                    assert(isValidVectorElemsize(size));
                    assert(size != EA_8BYTE);
                    assert(isValidVectorShiftAmount(imm, size, isRightShift));
                    fmt = IF_DV_2N;
                }
                break;
            }

            case INS_sxtl:
            case INS_uxtl:
            {
                assert(imm == 0);
                goto case INS_rshrn;
            }

            case INS_rshrn:
            case INS_shrn:
            case INS_sshll:
            case INS_ushll:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                var isRightShift = emitInsIsVectorRightShift(ins);
                assert(size == EA_8BYTE);
                assert(isValidArrangement(size, opt));
                elemsize = optGetElemsize(opt);
                assert(elemsize != EA_8BYTE);
                assert(isValidVectorElemsize(elemsize));
                assert(isValidVectorShiftAmount(imm, elemsize, isRightShift));
                fmt = IF_DV_2O;
                break;
            }

            case INS_sxtl2:
            case INS_uxtl2:
            {
                assert(imm == 0);
                goto case INS_rshrn2;
            }

            case INS_rshrn2:
            case INS_shrn2:
            case INS_sqrshrn2:
            case INS_sqrshrun2:
            case INS_sqshrn2:
            case INS_sqshrun2:
            case INS_sshll2:
            case INS_uqrshrn2:
            case INS_uqshrn2:
            case INS_ushll2:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                var isRightShift = emitInsIsVectorRightShift(ins);
                assert(size == EA_16BYTE);
                assert(isValidArrangement(size, opt));
                elemsize = optGetElemsize(opt);
                assert(elemsize != EA_8BYTE); // immh = 1xxx is reserved.
                assert(isValidVectorElemsize(elemsize));
                assert(isValidVectorShiftAmount(imm, elemsize, isRightShift));
                fmt = IF_DV_2O;
                break;
            }

            case INS_mvn:
            case INS_neg:
            case INS_negs:
            {
                assert(isValidGeneralDatasize(size));
                assert(isGeneralRegister(reg1));
                assert(isGeneralRegisterOrZR(reg2));
                if (imm == 0)
                {
                    assert(insOptsNone(opt));
                    fmt = IF_DR_2E;
                }
                else
                {
                    if (ins == INS_mvn)
                    {
                        assert(insOptsAnyShift(opt));
                    }
                    else
                    {
                        assert(insOptsAluShift(opt)); // NEG/NEGS cannot use ROR.
                    }
                    assert(isValidImmShift(imm, size));
                    fmt = IF_DR_2F;
                }
                break;
            }

            case INS_tst:
            {
                assert(isValidGeneralDatasize(size));
                assert(isGeneralRegisterOrZR(reg1));
                assert(isGeneralRegister(reg2));
                if (insOptsAnyShift(opt))
                {
                    assert(isValidImmShift(imm, size) && (imm != 0));
                    fmt = IF_DR_2B;
                }
                else
                {
                    assert(insOptsNone(opt));
                    assert(imm == 0);
                    fmt = IF_DR_2A;
                }
                break;
            }

            case INS_cmp:
            case INS_cmn:
            {
                assert(isValidGeneralDatasize(size));
                assert(isGeneralRegisterOrSP(reg1));
                assert(isGeneralRegister(reg2));
                reg1 = encodingSPtoZR(reg1);
                if (insOptsAnyExtend(opt))
                {
                    assert((imm >= 0) && (imm <= 4));
                    fmt = IF_DR_2C;
                }
                else if (imm == 0)
                {
                    assert(insOptsNone(opt));
                    fmt = IF_DR_2A;
                }
                else
                {
                    assert(insOptsAnyShift(opt));
                    assert(isValidImmShift(imm, size));
                    fmt = IF_DR_2B;
                }
                break;
            }

            case INS_ands:
            case INS_and:
            case INS_eor:
            case INS_orr:
            {
                assert(insOptsNone(opt));
                assert(isGeneralRegister(reg2));
                if (ins == INS_ands)
                {
                    assert(isGeneralRegister(reg1));
                }
                else
                {
                    assert(isGeneralRegisterOrSP(reg1));
                    reg1 = encodingSPtoZR(reg1);
                }

                bitMaskImm bmi;
                bmi.immNRS = 0;
                var canEncode = canEncodeBitMaskImm(imm, size, &bmi);
                if (canEncode)
                {
                    imm = (nint)bmi.immNRS;
                    assert(isValidImmNRS(unchecked((nuint)imm), size));
                    fmt = IF_DI_2C;
                }
                break;
            }

            case INS_dup:
            {
                assert(isVectorRegister(reg1));
                if (isVectorRegister(reg2))
                {
                    if (insOptsAnyArrangement(opt))
                    {
                        // The immediate indexes the operand, which can be larger than
                        // the return arrangement. Codegen checks that operand's index.
                        assert(isValidVectorDatasize(size));
                        assert(isValidArrangement(size, opt));
                        elemsize = optGetElemsize(opt);
                        assert(isValidVectorElemsize(elemsize));
                        assert(opt != INS_OPTS_1D);
                        fmt = IF_DV_2D;
                        break;
                    }
                    else
                    {
                        assert(insOptsNone(opt));
                        elemsize = size;
                        assert(isValidVectorElemsize(elemsize));
                        assert(isValidVectorIndex(EA_16BYTE, elemsize, imm));
                        fmt = IF_DV_2E;
                        break;
                    }
                }
                goto case INS_ins;
            }

            case INS_ins:
            {
                assert(insOptsNone(opt));
                assert(isValidVectorElemsize(size));
                assert(isVectorRegister(reg1));
                assert(isGeneralRegisterOrZR(reg2));
                elemsize = size;
                assert(isValidVectorIndex(EA_16BYTE, elemsize, imm));
                fmt = IF_DV_2C;
                break;
            }

            case INS_umov:
            {
                assert(insOptsNone(opt));
                assert(isValidVectorElemsize(size));
                assert(isGeneralRegister(reg1));
                assert(isVectorRegister(reg2));
                elemsize = size;
                assert(isValidVectorIndex(EA_16BYTE, elemsize, imm));
                fmt = IF_DV_2B;
                break;
            }

            case INS_smov:
            {
                assert(insOptsNone(opt));
                assert(isValidVectorElemsize(size));
                assert(size != EA_8BYTE); // No encoding; use UMOV.
                assert(isGeneralRegister(reg1));
                assert(isVectorRegister(reg2));
                elemsize = size;
                assert(isValidVectorIndex(EA_16BYTE, elemsize, imm));
                fmt = IF_DV_2B;
                break;
            }

            case INS_add:
            case INS_sub:
            {
                setFlags = false;
                isAddSub = true;
                break;
            }

            case INS_adds:
            case INS_subs:
            {
                setFlags = true;
                isAddSub = true;
                break;
            }

            case INS_ldrsb:
            case INS_ldursb:
            {
                // Size specifies sign extension into the destination register.
                assert(isValidGeneralDatasize(size));
                unscaledOp = ins == INS_ldursb;
                scale = 0;
                isLdSt = true;
                break;
            }

            case INS_ldrsh:
            case INS_ldursh:
            {
                assert(isValidGeneralDatasize(size));
                unscaledOp = ins == INS_ldursh;
                scale = 1;
                isLdSt = true;
                break;
            }

            case INS_ldrsw:
            case INS_ldursw:
            {
                assert(size == EA_8BYTE);
                unscaledOp = ins == INS_ldursw;
                scale = 2;
                isLdSt = true;
                break;
            }

            case INS_ldrb:
            case INS_strb:
            {
                unscaledOp = false;
                scale = 0;
                isLdSt = true;
                break;
            }

            case INS_ldrh:
            case INS_strh:
            {
                unscaledOp = false;
                scale = 1;
                isLdSt = true;
                break;
            }

            case INS_ldr:
            case INS_str:
            {
                if (isVectorRegister(reg1))
                {
                    assert(isValidVectorLSDatasize(size));
                    assert(isGeneralRegisterOrSP(reg2));
                    isSIMD = true;
                }
                else
                {
                    assert(isValidGeneralDatasize(size));
                }
                unscaledOp = false;
                scale = NaturalScale_helper(size);
                isLdSt = true;
                isLdrStr = true;
                break;
            }

            case INS_ldurb:
            case INS_ldurh:
            case INS_sturb:
            case INS_sturh:
            case INS_stlurb:
            case INS_stlurh:
            case INS_ldapurb:
            case INS_ldapurh:
            {
                assert(isGeneralRegisterOrZR(reg1));
                reg2 = encodingSPtoZR(reg2);
                fmt = IF_LS_2C;
                break;
            }

            case INS_ldapur:
            case INS_ldur:
            case INS_stur:
            case INS_stlur:
            {
                reg2 = encodingSPtoZR(reg2);
                fmt = IF_LS_2C;
                break;
            }

            case INS_ld2:
            case INS_ld3:
            case INS_ld4:
            case INS_st2:
            case INS_st3:
            case INS_st4:
            {
                assert(opt != INS_OPTS_1D); // Only LD1/ST1 permit .1D.
                goto case INS_ld1;
            }

            case INS_ld1:
            case INS_ld1_2regs:
            case INS_ld1_3regs:
            case INS_ld1_4regs:
            case INS_st1:
            case INS_st1_2regs:
            case INS_st1_3regs:
            case INS_st1_4regs:
            {
                assert(isVectorRegister(reg1));
                assert(isGeneralRegisterOrSP(reg2));
                reg2 = encodingSPtoZR(reg2);
                if (insOptsAnyArrangement(opt))
                {
                    var registerListSize = insGetRegisterListSize(ins);
                    assert(isValidVectorDatasize(size));
                    assert(isValidArrangement(size, opt));
                    assert(((uint)size * registerListSize) == imm);
                    fmt = IF_LS_2E;
                }
                else
                {
                    assert(insOptsNone(opt));
                    assert((ins != INS_ld1_2regs) && (ins != INS_ld1_3regs) && (ins != INS_ld1_4regs) &&
                           (ins != INS_st1_2regs) && (ins != INS_st1_3regs) && (ins != INS_st1_4regs));
                    elemsize = size;
                    assert(isValidVectorElemsize(elemsize));
                    assert(isValidVectorIndex(EA_16BYTE, elemsize, imm));
                    fmt = IF_LS_2F;
                }
                break;
            }

            case INS_ld1r:
            case INS_ld2r:
            case INS_ld3r:
            case INS_ld4r:
            {
                assert(isVectorRegister(reg1));
                assert(isGeneralRegisterOrSP(reg2));
                assert(isValidVectorDatasize(size));
                assert(isValidArrangement(size, opt));
                elemsize = optGetElemsize(opt);
                var registerListSize = insGetRegisterListSize(ins);
                assert(((uint)elemsize * registerListSize) == imm);
                reg2 = encodingSPtoZR(reg2);
                fmt = IF_LS_2E;
                break;
            }

            default:
            {
                emitInsSve_R_R_I(ins, attr, reg1, reg2, imm, opt, sopt);
                return;
            }
        }

        if (isLdSt)
        {
            assert(!isAddSub);
            if (isSIMD)
            {
                assert(isValidVectorLSDatasize(size));
                assert(isVectorRegister(reg1));
                assert(scale <= 4);
            }
            else
            {
                assert(isValidGeneralLSDatasize(size));
                assert(isGeneralRegisterOrZR(reg1));
                assert(scale <= 3);
            }
            assert(isGeneralRegisterOrSP(reg2));
            if (insOptsIndexed(opt))
            {
                assert(reg1 != reg2);
            }
            reg2 = encodingSPtoZR(reg2);

            nint mask = (1 << (int)scale) - 1;
            if ((imm == 0) || EA_IS_CNS_TLSGD_RELOC(attr))
            {
                assert(insOptsNone(opt)); // Zero offset cannot use PRE/POST indexing.
                fmt = IF_LS_2A;
            }
            else if (insOptsIndexed(opt) || unscaledOp || (imm < 0) || ((imm & mask) != 0))
            {
                if (isValidSimm(imm, 9))
                {
                    fmt = IF_LS_2C;
                }
                else
                {
                    assert(false, "Instruction cannot be encoded: IF_LS_2C");
                }
            }
            else if (imm > 0)
            {
                assert(insOptsNone(opt));
                assert(!unscaledOp);
                if (((imm & mask) == 0) && ((imm >> (int)scale) < 0x1000))
                {
                    imm >>= (int)scale;
                    fmt = IF_LS_2B;
                }
                else
                {
                    assert(false, "Instruction cannot be encoded: IF_LS_2B");
                }
            }

            // Fold adrp/add/ldr into adrp/ldr with PAGEOFFSET_12L when possible.
            if ((fmt == IF_LS_2A) && _compiler.opts.compReloc && TryFoldPageOffsetIntoLdr(ins, attr, reg1, reg2))
            {
                return;
            }
            if (isLdrStr && _compiler.opts.OptimizationEnabled &&
                OptimizeLdrStr(ins, attr, reg1, reg2, imm, size, fmt, false, -1, -1
#if DEBUG
                    , false
#endif
                ))
            {
                return;
            }
        }
        else if (isAddSub)
        {
            assert(!isLdSt);
            assert(insOptsNone(opt));
            if (setFlags)
            {
                assert(isGeneralRegister(reg1));
                assert(isGeneralRegister(reg2));
            }
            else
            {
                assert(isGeneralRegisterOrSP(reg1));
                assert(isGeneralRegisterOrSP(reg2));
                if (imm == 0)
                {
                    emitIns_Mov(INS_mov, attr, reg1, reg2, canSkip: true);
                    return;
                }
                if ((reg1 == reg2) && (EA_SIZE(attr) == EA_PTRSIZE) && _compiler.opts.OptimizationEnabled &&
                    OptimizePostIndexed(ins, reg1, imm, attr))
                {
                    return;
                }
                reg1 = encodingSPtoZR(reg1);
                reg2 = encodingSPtoZR(reg2);
            }

            if (unsigned_abs(imm) <= 0x0fff)
            {
                if (imm < 0)
                {
                    ins = insReverse(ins);
                    imm = -imm;
                }
                assert(isValidUimm(imm, 12));
                fmt = IF_DI_2A;
            }
            else if (canEncodeWithShiftImmBy12(imm))
            {
                opt = INS_OPTS_LSL12;
                if (imm < 0)
                {
                    ins = insReverse(ins);
                    imm = -imm;
                }
                assert((imm & 0xfff) == 0);
                imm >>= 12;
                assert(isValidUimm(imm, 12));
                fmt = IF_DI_2A;
            }
            else
            {
                assert(false, "Instruction cannot be encoded: IF_DI_2A");
            }
        }
        assert(fmt != IF_NONE);

        var id = emitNewInstrSC(attr, imm);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(opt);
        id.idReg1(reg1);
        id.idReg2(reg2);
        if (EA_IS_CNS_TLSGD_RELOC(attr))
        {
            assert(imm != 0);
            id.idSetTlsGD();
        }
        dispIns(id);
        appendToCurIG(id);
    }
}
#endif
