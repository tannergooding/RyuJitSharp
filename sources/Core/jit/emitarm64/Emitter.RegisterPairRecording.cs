// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_R_R(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        insOpts opt = INS_OPTS_NONE, insScalableOpts sopt = insScalableOpts.INS_SCALABLE_OPTS_NONE)
    {
        assert(_compiler is not null);
        if (IsMovInstruction(ins))
        {
            assert(false, "Please use emitIns_Mov() to correctly handle move elision");
            emitIns_Mov(ins, attr, reg1, reg2, canSkip: false, opt);
        }

        var size = EA_SIZE(attr);
        emitAttr elemsize;
        insFormat fmt;
        switch (ins)
        {
            case INS_dup:
            {
                assert(insOptsAnyArrangement(opt));
                assert(isVectorRegister(reg1));
                assert(isGeneralRegisterOrZR(reg2));
                assert(isValidVectorDatasize(size));
                assert(isValidArrangement(size, opt));
                assert(opt != INS_OPTS_1D);
                fmt = IF_DV_2C;
                break;
            }

            case INS_abs:
            case INS_not:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                if (ins == INS_not)
                {
                    assert(isValidVectorDatasize(size));
                    // Bitwise behavior is independent of element size, but encodes as bytes.
                    opt = optMakeArrangement(size, EA_1BYTE);
                }
                if (insOptsNone(opt))
                {
                    assert(size == EA_8BYTE);
                    fmt = IF_DV_2L;
                }
                else
                {
                    assert(insOptsAnyArrangement(opt));
                    assert(isValidVectorDatasize(size));
                    assert(isValidArrangement(size, opt));
                    _ = optGetElemsize(opt);
                    fmt = IF_DV_2M;
                }
                break;
            }

            case INS_mvn:
            case INS_neg:
            {
                if (isVectorRegister(reg1))
                {
                    assert(isVectorRegister(reg2));
                    if (ins == INS_mvn)
                    {
                        assert(isValidVectorDatasize(size));
                        opt = optMakeArrangement(size, EA_1BYTE);
                    }
                    if (insOptsNone(opt))
                    {
                        assert(size == EA_8BYTE);
                        fmt = IF_DV_2L;
                    }
                    else
                    {
                        assert(isValidVectorDatasize(size));
                        assert(isValidArrangement(size, opt));
                        _ = optGetElemsize(opt);
                        fmt = IF_DV_2M;
                    }
                    break;
                }
                goto case INS_negs;
            }

            case INS_negs:
            {
                assert(insOptsNone(opt));
                assert(isGeneralRegister(reg1));
                assert(isGeneralRegisterOrZR(reg2));
                fmt = IF_DR_2E;
                break;
            }

            case INS_sxtl:
            case INS_sxtl2:
            case INS_uxtl:
            case INS_uxtl2:
            {
                emitIns_R_R_I(ins, size, reg1, reg2, 0, opt);
                return;
            }

            case INS_cls:
            case INS_clz:
            case INS_rbit:
            case INS_rev16:
            case INS_rev32:
            case INS_cnt:
            {
                if (isVectorRegister(reg1))
                {
                    assert(isVectorRegister(reg2));
                    assert(isValidVectorDatasize(size));
                    assert(isValidArrangement(size, opt));
                    elemsize = optGetElemsize(opt);
                    if ((ins == INS_cls) || (ins == INS_clz))
                    {
                        assert(elemsize != EA_8BYTE);
                    }
                    else if (ins == INS_rev32)
                    {
                        assert((elemsize == EA_2BYTE) || (elemsize == EA_1BYTE));
                    }
                    else
                    {
                        assert(elemsize == EA_1BYTE);
                    }
                    fmt = IF_DV_2M;
                    break;
                }
                assert((ins != INS_cnt) || _compiler.compIsaSupportedDebugOnly(InstructionSet_Cssc));
                goto case INS_ctz;
            }

            case INS_ctz:
            case INS_rev:
            {
                assert(insOptsNone(opt));
                assert(isGeneralRegister(reg1));
                assert(isGeneralRegister(reg2));
                assert((ins != INS_ctz) || _compiler.compIsaSupportedDebugOnly(InstructionSet_Cssc));
                if (ins == INS_rev32)
                {
                    assert(size == EA_8BYTE);
                }
                else
                {
                    assert(isValidGeneralDatasize(size));
                }
                fmt = IF_DR_2G;
                break;
            }

            case INS_addv:
            case INS_saddlv:
            case INS_smaxv:
            case INS_sminv:
            case INS_uaddlv:
            case INS_umaxv:
            case INS_uminv:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isValidVectorDatasize(size));
                assert(isValidArrangement(size, opt));
                assert((opt != INS_OPTS_2S) && (opt != INS_OPTS_1D) && (opt != INS_OPTS_2D));
                fmt = IF_DV_2T;
                break;
            }

            case INS_rev64:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isValidVectorDatasize(size));
                assert(isValidArrangement(size, opt));
                elemsize = optGetElemsize(opt);
                assert(elemsize != EA_8BYTE);
                fmt = IF_DV_2M;
                break;
            }

            case INS_sqxtn:
            case INS_sqxtun:
            case INS_uqxtn:
            {
                if (insOptsNone(opt))
                {
                    assert(isVectorRegister(reg1));
                    assert(isVectorRegister(reg2));
                    assert(isValidVectorElemsize(size));
                    assert(size != EA_8BYTE);
                    fmt = IF_DV_2L;
                    break;
                }
                goto case INS_xtn;
            }

            case INS_xtn:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(size == EA_8BYTE);
                assert(isValidArrangement(size, opt));
                assert(opt != INS_OPTS_1D);
                fmt = IF_DV_2M;
                break;
            }

            case INS_sqxtn2:
            case INS_sqxtun2:
            case INS_uqxtn2:
            case INS_xtn2:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(size == EA_16BYTE);
                assert(isValidArrangement(size, opt));
                assert(opt != INS_OPTS_2D);
                fmt = IF_DV_2M;
                break;
            }

            case INS_ldar:
            case INS_ldapr:
            case INS_ldaxr:
            case INS_ldxr:
            case INS_stlr:
            {
                assert(isValidGeneralDatasize(size));
                goto case INS_ldarb;
            }

            case INS_ldarb:
            case INS_ldaprb:
            case INS_ldaxrb:
            case INS_ldxrb:
            case INS_ldarh:
            case INS_ldaprh:
            case INS_ldaxrh:
            case INS_ldxrh:
            case INS_stlrb:
            case INS_stlrh:
            {
                assert(isValidGeneralLSDatasize(size));
                assert(isGeneralRegisterOrZR(reg1));
                assert(isGeneralRegisterOrSP(reg2));
                assert(insOptsNone(opt));
                reg2 = encodingSPtoZR(reg2);
                fmt = IF_LS_2A;
                break;
            }

            case INS_ldr:
            case INS_ldrb:
            case INS_ldrh:
            case INS_ldrsb:
            case INS_ldrsh:
            case INS_ldrsw:
            case INS_str:
            case INS_strb:
            case INS_strh:
            case INS_tst:
            {
                assert(insOptsNone(opt));
                emitIns_R_R_I(ins, attr, reg1, reg2, 0, INS_OPTS_NONE);
                return;
            }

            case INS_cmp:
            case INS_cmn:
            {
                emitIns_R_R_I(ins, attr, reg1, reg2, 0, opt);
                return;
            }

            case INS_staddb:
            {
                emitIns_R_R_R(INS_ldaddb, attr, reg1, REG_ZR, reg2);
                return;
            }

            case INS_staddlb:
            {
                emitIns_R_R_R(INS_ldaddlb, attr, reg1, REG_ZR, reg2);
                return;
            }

            case INS_staddh:
            {
                emitIns_R_R_R(INS_ldaddh, attr, reg1, REG_ZR, reg2);
                return;
            }

            case INS_staddlh:
            {
                emitIns_R_R_R(INS_ldaddlh, attr, reg1, REG_ZR, reg2);
                return;
            }

            case INS_stadd:
            {
                emitIns_R_R_R(INS_ldadd, attr, reg1, REG_ZR, reg2);
                return;
            }

            case INS_staddl:
            {
                emitIns_R_R_R(INS_ldaddl, attr, reg1, REG_ZR, reg2);
                return;
            }

            case INS_fcmp:
            case INS_fcmpe:
            {
                assert(insOptsNone(opt));
                assert(isValidVectorElemsizeFloat(size) || (size == EA_2BYTE));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                fmt = IF_DV_2K;
                break;
            }

            case INS_fcvtns:
            case INS_fcvtnu:
            case INS_fcvtas:
            case INS_fcvtau:
            case INS_fcvtps:
            case INS_fcvtpu:
            case INS_fcvtms:
            case INS_fcvtmu:
            case INS_fcvtzs:
            case INS_fcvtzu:
            {
                if (insOptsAnyArrangement(opt))
                {
                    assert(isVectorRegister(reg1));
                    assert(isVectorRegister(reg2));
                    assert(isValidVectorDatasize(size));
                    assert(isValidArrangement(size, opt));
                    elemsize = optGetElemsize(opt);
                    assert(isValidVectorElemsizeFloat(elemsize));
                    assert(opt != INS_OPTS_1D);
                    fmt = IF_DV_2A;
                }
                else
                {
                    assert(isVectorRegister(reg2));
                    if (isVectorRegister(reg1))
                    {
                        assert(insOptsNone(opt));
                        assert(isValidVectorElemsizeFloat(size));
                        fmt = IF_DV_2G;
                    }
                    else
                    {
                        assert(isGeneralRegister(reg1));
                        assert(insOptsConvertFloatToInt(opt));
                        assert(isValidVectorElemsizeFloat(size));
                        fmt = IF_DV_2H;
                    }
                }
                break;
            }

            case INS_fcvtl:
            case INS_fcvtn:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(size == EA_8BYTE);
                assert((opt == INS_OPTS_4H) || (opt == INS_OPTS_2S));
                fmt = IF_DV_2A;
                break;
            }

            case INS_fcvtl2:
            case INS_fcvtn2:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(size == EA_16BYTE);
                assert((opt == INS_OPTS_8H) || (opt == INS_OPTS_4S));
                fmt = IF_DV_2A;
                break;
            }

            case INS_fcvtxn:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                if (insOptsAnyArrangement(opt))
                {
                    assert(size == EA_8BYTE);
                    assert(opt == INS_OPTS_2S);
                    fmt = IF_DV_2A;
                }
                else
                {
                    assert(insOptsNone(opt));
                    assert(size == EA_4BYTE);
                    fmt = IF_DV_2G;
                }
                break;
            }

            case INS_fcvtxn2:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(size == EA_16BYTE);
                assert(opt == INS_OPTS_4S);
                fmt = IF_DV_2A;
                break;
            }

            case INS_scvtf:
            case INS_ucvtf:
            {
                if (insOptsAnyArrangement(opt))
                {
                    assert(isVectorRegister(reg1));
                    assert(isVectorRegister(reg2));
                    assert(isValidVectorDatasize(size));
                    assert(isValidArrangement(size, opt));
                    elemsize = optGetElemsize(opt);
                    assert(isValidVectorElemsizeFloat(elemsize));
                    assert(opt != INS_OPTS_1D);
                    fmt = IF_DV_2A;
                }
                else
                {
                    assert(isVectorRegister(reg1));
                    if (isVectorRegister(reg2))
                    {
                        assert(insOptsNone(opt));
                        assert(isValidVectorElemsizeFloat(size));
                        fmt = IF_DV_2G;
                    }
                    else
                    {
                        assert(isGeneralRegister(reg2));
                        assert(insOptsConvertIntToFloat(opt));
                        assert(isValidVectorElemsizeFloat(size) || (size == EA_2BYTE));
                        fmt = IF_DV_2I;
                    }
                }
                break;
            }

            case INS_fabs:
            case INS_fneg:
            case INS_fsqrt:
            case INS_frinta:
            case INS_frinti:
            case INS_frintm:
            case INS_frintn:
            case INS_frintp:
            case INS_frintx:
            case INS_frintz:
            {
                if (insOptsAnyArrangement(opt))
                {
                    assert(isVectorRegister(reg1));
                    assert(isVectorRegister(reg2));
                    assert(isValidVectorDatasize(size));
                    assert(isValidArrangement(size, opt));
                    elemsize = optGetElemsize(opt);
                    assert(isValidVectorElemsizeFloat(elemsize));
                    assert(opt != INS_OPTS_1D);
                    fmt = IF_DV_2A;
                }
                else
                {
                    assert(insOptsNone(opt));
                    assert(isValidVectorElemsizeFloat(size) || (size == EA_2BYTE));
                    assert(isVectorRegister(reg1));
                    assert(isVectorRegister(reg2));
                    fmt = IF_DV_2G;
                }
                break;
            }

            case INS_faddp:
            case INS_fmaxnmp:
            case INS_fmaxp:
            case INS_fminnmp:
            case INS_fminp:
            {
                assert(((size == EA_8BYTE) && (opt == INS_OPTS_2S)) ||
                    ((size == EA_16BYTE) && (opt == INS_OPTS_2D)));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                fmt = IF_DV_2Q;
                break;
            }

            case INS_fmaxnmv:
            case INS_fmaxv:
            case INS_fminnmv:
            case INS_fminv:
            {
                assert(size == EA_16BYTE);
                assert(opt == INS_OPTS_4S);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                fmt = IF_DV_2R;
                break;
            }

            case INS_addp:
            {
                assert(size == EA_16BYTE);
                assert(opt == INS_OPTS_2D);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                fmt = IF_DV_2S;
                break;
            }

            case INS_fcvt:
            {
                assert(insOptsConvertFloatToFloat(opt));
                assert(isValidVectorFcvtsize(size));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                fmt = IF_DV_2J;
                break;
            }

            case INS_cmeq:
            case INS_cmge:
            case INS_cmgt:
            case INS_cmle:
            case INS_cmlt:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                if (insOptsAnyArrangement(opt))
                {
                    assert(isValidVectorDatasize(size));
                    assert(isValidArrangement(size, opt));
                    _ = optGetElemsize(opt);
                    fmt = IF_DV_2M;
                }
                else
                {
                    assert(size == EA_8BYTE);
                    assert(insOptsNone(opt));
                    fmt = IF_DV_2L;
                }
                break;
            }

            case INS_fcmeq:
            case INS_fcmge:
            case INS_fcmgt:
            case INS_fcmle:
            case INS_fcmlt:
            case INS_frecpe:
            case INS_frsqrte:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                if (insOptsAnyArrangement(opt))
                {
                    assert(isValidVectorDatasize(size));
                    assert(isValidArrangement(size, opt));
                    elemsize = optGetElemsize(opt);
                    assert(isValidVectorElemsizeFloat(elemsize));
                    assert(opt != INS_OPTS_1D);
                    fmt = IF_DV_2A;
                }
                else
                {
                    assert(isValidScalarDatasize(size));
                    assert(insOptsNone(opt));
                    fmt = IF_DV_2G;
                }
                break;
            }

            case INS_aesd:
            case INS_aese:
            case INS_aesmc:
            case INS_aesimc:
            {
                assert(size == EA_16BYTE);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isValidVectorDatasize(size));
                elemsize = optGetElemsize(opt);
                assert(elemsize == EA_1BYTE);
                fmt = IF_DV_2P;
                break;
            }

            case INS_sha1h:
            {
                assert(size == EA_4BYTE);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(insOptsNone(opt));
                fmt = IF_DV_2U;
                break;
            }

            case INS_sha1su1:
            case INS_sha256su0:
            {
                assert(size == EA_16BYTE);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                elemsize = optGetElemsize(opt);
                assert(elemsize == EA_4BYTE);
                fmt = IF_DV_2U;
                break;
            }

            case INS_sha512su0:
            {
                assert(size == EA_16BYTE);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                elemsize = optGetElemsize(opt);
                assert(elemsize == EA_8BYTE);
                fmt = IF_DV_2V;
                break;
            }

            case INS_sm4e:
            {
                assert(size == EA_16BYTE);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                elemsize = optGetElemsize(opt);
                assert(elemsize == EA_4BYTE);
                fmt = IF_DV_2V;
                break;
            }

            case INS_ld2:
            case INS_ld3:
            case INS_ld4:
            case INS_st2:
            case INS_st3:
            case INS_st4:
            {
                assert(opt != INS_OPTS_1D);
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
            case INS_ld1r:
            case INS_ld2r:
            case INS_ld3r:
            case INS_ld4r:
            {
                assert(isVectorRegister(reg1));
                assert(isGeneralRegisterOrSP(reg2));
                assert(isValidVectorDatasize(size));
                assert(isValidArrangement(size, opt));
                reg2 = encodingSPtoZR(reg2);
                fmt = IF_LS_2D;
                break;
            }

            case INS_urecpe:
            case INS_ursqrte:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isValidVectorDatasize(size));
                assert(isValidArrangement(size, opt));
                elemsize = optGetElemsize(opt);
                assert(elemsize == EA_4BYTE);
                fmt = IF_DV_2A;
                break;
            }

            case INS_frecpx:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isValidScalarDatasize(size));
                assert(insOptsNone(opt));
                fmt = IF_DV_2G;
                break;
            }

            case INS_sadalp:
            case INS_saddlp:
            case INS_uadalp:
            case INS_uaddlp:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isValidArrangement(size, opt));
                assert((opt != INS_OPTS_1D) && (opt != INS_OPTS_2D));
                fmt = IF_DV_2T;
                break;
            }

            case INS_sqabs:
            case INS_sqneg:
            case INS_suqadd:
            case INS_usqadd:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                if (insOptsAnyArrangement(opt))
                {
                    assert(isValidArrangement(size, opt));
                    assert(opt != INS_OPTS_1D);
                    fmt = IF_DV_2M;
                }
                else
                {
                    assert(insOptsNone(opt));
                    assert(isValidVectorElemsize(size));
                    fmt = IF_DV_2L;
                }
                break;
            }

            case INS_autia:
            case INS_autib:
            case INS_pacia:
            case INS_pacib:
            {
                assert(insOptsNone(opt));
                assert(isValidGeneralDatasize(size));
                assert(isGeneralRegister(reg1));
                assert(isGeneralRegisterOrSP(reg2));
                reg2 = encodingSPtoZR(reg2);
                fmt = IF_PC_2A;
                break;
            }

            default:
            {
                emitInsSve_R_R(ins, attr, reg1, reg2, opt, sopt);
                return;
            }
        }
        assert(fmt != IF_NONE);

        var id = emitNewInstrSmall(attr);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(opt);
        id.idReg1(reg1);
        id.idReg2(reg2);

        dispIns(id);
        appendToCurIG(id);
    }

    private static insOpts optMakeArrangement(emitAttr datasize, emitAttr elemsize)
    {
        var result = INS_OPTS_NONE;
        if (datasize == EA_8BYTE)
        {
            switch (elemsize)
            {
                case EA_1BYTE:
                {
                    result = INS_OPTS_8B;
                    break;
                }

                case EA_2BYTE:
                {
                    result = INS_OPTS_4H;
                    break;
                }

                case EA_4BYTE:
                {
                    result = INS_OPTS_2S;
                    break;
                }

                case EA_8BYTE:
                {
                    result = INS_OPTS_1D;
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }
        }
        else if (datasize == EA_16BYTE)
        {
            switch (elemsize)
            {
                case EA_1BYTE:
                {
                    result = INS_OPTS_16B;
                    break;
                }

                case EA_2BYTE:
                {
                    result = INS_OPTS_8H;
                    break;
                }

                case EA_4BYTE:
                {
                    result = INS_OPTS_4S;
                    break;
                }

                case EA_8BYTE:
                {
                    result = INS_OPTS_2D;
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }
        }

        return result;
    }
}
#endif
