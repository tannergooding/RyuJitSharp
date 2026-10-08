// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.insScalableOpts;
using static RyuJitSharp.insSveMovOpts;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitInsSve_R_R_R_R_I_I(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        regNumber reg3, regNumber reg4, nint imm1, nint imm2, insOpts opt = INS_OPTS_NONE,
        insScalableOpts sopt = INS_SCALABLE_OPTS_NONE, insSveMovOpts mopt = INS_SVE_MOV_OPTS_UNPRED)
    {
        switch (ins)
        {
            case INS_sve_cmla:
            case INS_sve_fcmla:
            case INS_sve_sqrdcmlah:
            case INS_sve_cdot:
            {
                assert(insSveMovOptsUnpredicated(mopt));
                assert(isValidMovprfxReg(mopt, reg1, reg2, reg3, reg4));
                emitInsSve_Mov(INS_sve_movprfx, EA_SCALABLE, reg1, reg2, true, INS_OPTS_NONE);
                emitInsSve_R_R_R_I_I(ins, attr, reg1, reg3, reg4, imm1, imm2, opt);
                return;
            }

            default:
            {
                unreached();
                break;
            }
        }
    }

    public void emitInsSve_R_R_R_R_R(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        regNumber reg3, regNumber reg4, regNumber reg5, insOpts opt = INS_OPTS_NONE,
        insScalableOpts sopt = INS_SCALABLE_OPTS_NONE, insSveMovOpts mopt = INS_SVE_MOV_OPTS_UNPRED)
    {
        switch (ins)
        {
            case INS_sve_and:
            case INS_sve_bic:
            case INS_sve_eor:
            case INS_sve_fmad:
            case INS_sve_fmla:
            case INS_sve_fmls:
            case INS_sve_fmsb:
            case INS_sve_fnmad:
            case INS_sve_fnmla:
            case INS_sve_fnmls:
            case INS_sve_fnmsb:
            case INS_sve_mad:
            case INS_sve_mla:
            case INS_sve_mls:
            case INS_sve_msb:
            case INS_sve_orr:
            {
                assert(isValidMovprfxReg(mopt, reg1, reg3, reg4, reg5));
                emitInsSve_Mov(INS_sve_movprfx, EA_SCALABLE, reg1, reg3, true, opt, mopt, reg2);
                emitInsSve_R_R_R_R(ins, attr, reg1, reg2, reg4, reg5, opt, sopt);
                return;
            }

            default:
            {
                unreached();
                break;
            }
        }
    }

    public void emitInsSve_R_R_R_R_R_I(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        regNumber reg3, regNumber reg4, regNumber reg5, nint imm, insOpts opt = INS_OPTS_NONE,
        insScalableOpts sopt = INS_SCALABLE_OPTS_NONE, insSveMovOpts mopt = INS_SVE_MOV_OPTS_UNPRED)
    {
        switch (ins)
        {
            case INS_sve_fcmla:
            {
                assert(isValidMovprfxReg(mopt, reg1, reg3, reg4, reg5));
                emitInsSve_Mov(INS_sve_movprfx, EA_SCALABLE, reg1, reg3, true, opt, mopt, reg2);
                emitInsSve_R_R_R_R_I(ins, attr, reg1, reg2, reg4, reg5, imm, opt, sopt);
                return;
            }

            default:
            {
                unreached();
                break;
            }
        }
    }

    public void emitIns_R_PATTERN_I(instruction ins, emitAttr attr, regNumber reg1, insSvePattern pattern,
        nint imm, insOpts opt = INS_OPTS_NONE)
    {
        var size = EA_SIZE(attr);
        var fmt = IF_NONE;

        switch (ins)
        {
            case INS_sve_cntb:
            case INS_sve_cntd:
            case INS_sve_cnth:
            case INS_sve_cntw:
            {
                assert(insOptsNone(opt));
                assert(isGeneralRegister(reg1));
                assert(isValidUimmFrom1(imm, 4));
                assert(size == EA_8BYTE);
                fmt = IF_SVE_BL_1A;
                break;
            }

            case INS_sve_incd:
            case INS_sve_inch:
            case INS_sve_incw:
            case INS_sve_decd:
            case INS_sve_dech:
            case INS_sve_decw:
            {
                assert(isValidUimmFrom1(imm, 4));
                if (insOptsNone(opt))
                {
                    assert(isGeneralRegister(reg1));
                    assert(size == EA_8BYTE);
                    fmt = IF_SVE_BM_1A;
                }
                else
                {
                    assert(insOptsScalableAtLeastHalf(opt));
                    assert(isVectorRegister(reg1));
                    fmt = IF_SVE_BN_1A;
                }
                break;
            }

            case INS_sve_incb:
            case INS_sve_decb:
            {
                assert(isGeneralRegister(reg1));
                assert(isValidUimmFrom1(imm, 4));
                assert(size == EA_8BYTE);
                fmt = IF_SVE_BM_1A;
                break;
            }

            case INS_sve_sqincb:
            case INS_sve_uqincb:
            case INS_sve_sqdecb:
            case INS_sve_uqdecb:
            {
                assert(insOptsNone(opt));
                assert(isGeneralRegister(reg1));
                assert(isValidUimmFrom1(imm, 4));
                assert(isValidGeneralDatasize(size));
                fmt = IF_SVE_BO_1A;
                break;
            }

            case INS_sve_sqinch:
            case INS_sve_uqinch:
            case INS_sve_sqdech:
            case INS_sve_uqdech:
            case INS_sve_sqincw:
            case INS_sve_uqincw:
            case INS_sve_sqdecw:
            case INS_sve_uqdecw:
            case INS_sve_sqincd:
            case INS_sve_uqincd:
            case INS_sve_sqdecd:
            case INS_sve_uqdecd:
            {
                assert(isValidUimmFrom1(imm, 4));
                if (insOptsNone(opt))
                {
                    assert(isGeneralRegister(reg1));
                    assert(isValidGeneralDatasize(size));
                    fmt = IF_SVE_BO_1A;
                }
                else
                {
                    assert(insOptsScalableAtLeastHalf(opt));
                    assert(isVectorRegister(reg1));
                    assert(isScalableVectorSize(size));
                    fmt = IF_SVE_BP_1A;
                }
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        assert(fmt != IF_NONE);
        var id = emitNewInstrCns(attr, imm);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(opt);
        id.idOpSize(size);
        id.idReg1(reg1);
        id.idSvePattern(pattern);
        dispIns(id);
        appendToCurIG(id);
    }

    public void emitIns_R_R_PATTERN_I(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        insSvePattern pattern, nint imm, insOpts opt = INS_OPTS_NONE,
        insScalableOpts sopt = INS_SCALABLE_OPTS_NONE, insSveMovOpts mopt = INS_SVE_MOV_OPTS_UNPRED)
    {
        switch (ins)
        {
            case INS_sve_sqinch:
            case INS_sve_uqinch:
            case INS_sve_sqdech:
            case INS_sve_uqdech:
            case INS_sve_sqincw:
            case INS_sve_uqincw:
            case INS_sve_sqdecw:
            case INS_sve_uqdecw:
            case INS_sve_sqincd:
            case INS_sve_uqincd:
            case INS_sve_sqdecd:
            case INS_sve_uqdecd:
            {
                if (!insOptsNone(opt))
                {
                    assert(insSveMovOptsUnpredicated(mopt));
                    emitInsSve_Mov(INS_sve_movprfx, EA_SCALABLE, reg1, reg2, true, INS_OPTS_NONE);
                    emitIns_R_PATTERN_I(ins, attr, reg1, pattern, imm, opt);
                    return;
                }
                goto case INS_sve_sqincb;
            }

            case INS_sve_sqincb:
            case INS_sve_uqincb:
            case INS_sve_sqdecb:
            case INS_sve_uqdecb:
            {
                emitIns_Mov(INS_mov, attr, reg1, reg2, true);
                emitIns_R_PATTERN_I(ins, attr, reg1, pattern, imm, opt);
                return;
            }

            default:
            {
                unreached();
                break;
            }
        }
    }

    public void emitIns_PRFOP_R_R_R(instruction ins, emitAttr attr, insSvePrfop prfop,
        regNumber reg1, regNumber reg2, regNumber reg3, insOpts opt = INS_OPTS_NONE,
        insScalableOpts sopt = INS_SCALABLE_OPTS_NONE)
    {
        var size = EA_SIZE(attr);
        var fmt = IF_NONE;

        switch (ins)
        {
            case INS_sve_prfb:
            {
                assert(insScalableOptsNone(sopt));
                assert(isLowPredicateRegister(reg1));
                assert(isGeneralRegister(reg2));
                assert(isScalableVectorSize(size));
                if (insOptsScalable32bitExtends(opt))
                {
                    assert(isVectorRegister(reg3));
                    fmt = insOptsScalableSingleWord32bitExtends(opt) ? IF_SVE_HY_3A : IF_SVE_HY_3A_A;
                    if (!insOptsScalableSingleWord32bitExtends(opt))
                    {
                        assert(insOptsScalableDoubleWord32bitExtends(opt));
                    }
                }
                else if (isVectorRegister(reg3))
                {
                    assert(opt == INS_OPTS_SCALABLE_D);
                    fmt = IF_SVE_HY_3B;
                }
                else
                {
                    assert(insOptsNone(opt));
                    assert(isGeneralRegister(reg3));
                    fmt = IF_SVE_IB_3A;
                }
                break;
            }

            case INS_sve_prfh:
            case INS_sve_prfw:
            case INS_sve_prfd:
            {
                assert(isLowPredicateRegister(reg1));
                assert(isGeneralRegister(reg2));
                assert(isScalableVectorSize(size));
                if (sopt == INS_SCALABLE_OPTS_MOD_N)
                {
                    if (insOptsScalableSingleWord32bitExtends(opt))
                    {
                        fmt = IF_SVE_HY_3A;
                    }
                    else
                    {
                        assert(insOptsScalableDoubleWord32bitExtends(opt));
                        fmt = IF_SVE_HY_3A_A;
                    }
                }
                else
                {
                    assert(sopt == INS_SCALABLE_OPTS_LSL_N);
                    if (isVectorRegister(reg3))
                    {
                        assert(opt == INS_OPTS_SCALABLE_D);
                        fmt = IF_SVE_HY_3B;
                    }
                    else
                    {
                        assert(insOptsNone(opt));
                        assert(isGeneralRegister(reg3));
                        fmt = IF_SVE_IB_3A;
                    }
                }
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        assert(fmt != IF_NONE);
        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idInsOpt(opt);
        id.idInsFmt(fmt);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idReg3(reg3);
        id.idSvePrfop(prfop);
        dispIns(id);
        appendToCurIG(id);
    }

    public void emitIns_PRFOP_R_R_I(instruction ins, emitAttr attr, insSvePrfop prfop,
        regNumber reg1, regNumber reg2, int imm, insOpts opt = INS_OPTS_NONE)
    {
        var size = EA_SIZE(attr);
        var fmt = IF_NONE;

        switch (ins)
        {
            case INS_sve_prfb:
            case INS_sve_prfh:
            case INS_sve_prfw:
            case INS_sve_prfd:
            {
                assert(isLowPredicateRegister(reg1));
                assert(isScalableVectorSize(size));
                if (isVectorRegister(reg2))
                {
                    assert(insOptsScalableWords(opt));
#if DEBUG
                    switch (ins)
                    {
                        case INS_sve_prfb:
                        {
                            assert(isValidUimm(imm, 5));
                            break;
                        }
                        case INS_sve_prfh:
                        {
                            assert(isValidUimm_MultipleOf(imm, 5, 2));
                            break;
                        }
                        case INS_sve_prfw:
                        {
                            assert(isValidUimm_MultipleOf(imm, 5, 4));
                            break;
                        }
                        case INS_sve_prfd:
                        {
                            assert(isValidUimm_MultipleOf(imm, 5, 8));
                            break;
                        }
                        default:
                        {
                            assert(false, "!\"Invalid instruction\"");
                            break;
                        }
                    }
#endif
                    fmt = IF_SVE_HZ_2A_B;
                }
                else
                {
                    assert(insOptsNone(opt));
                    assert(isGeneralRegister(reg2));
                    assert(isValidSimm(imm, 6));
                    fmt = IF_SVE_IA_2A;
                }
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        assert(fmt != IF_NONE);
        var id = emitNewInstrCns(attr, imm);
        id.idIns(ins);
        id.idInsOpt(opt);
        id.idInsFmt(fmt);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idSvePrfop(prfop);
        dispIns(id);
        appendToCurIG(id);
    }
}
#endif
