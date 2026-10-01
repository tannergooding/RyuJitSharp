// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.GCInfo.GCtype;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_R_R(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2, insFlags flags)
    {
        emitIns_R_R(ins, attr, reg1, reg2);
    }

    public void emitIns_R_R_R(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2, regNumber reg3,
        insOpts opt = INS_OPTS_NONE, insScalableOpts sopt = insScalableOpts.INS_SCALABLE_OPTS_NONE)
    {
        var size = EA_SIZE(attr);
        var dstsize = size;
        emitAttr elemsize;
        insFormat fmt;
        switch (ins)
        {
            case INS_aesd:
            case INS_aese:
            case INS_fcvtn2:
            case INS_fcvtxn2:
            case INS_sha1su1:
            case INS_sha256su0:
            case INS_sqxtn2:
            case INS_sqxtun2:
            case INS_uqxtn2:
            case INS_xtn2:
            {
                dstsize = EA_16BYTE;
                goto case INS_sadalp;
            }

            case INS_sadalp:
            case INS_sm4e:
            case INS_suqadd:
            case INS_uadalp:
            case INS_usqadd:
            {
                emitIns_Mov(INS_mov, dstsize, reg1, reg2, canSkip: true);
                emitIns_R_R(ins, attr, reg1, reg3, opt, sopt);
                return;
            }

            case INS_mul:
            case INS_smull:
            case INS_umull:
            {
                if (insOptsAnyArrangement(opt))
                {
                    assert(isVectorRegister(reg1));
                    assert(isVectorRegister(reg2));
                    assert(isVectorRegister(reg3));
                    assert(isValidArrangement(size, opt));
                    assert((opt != INS_OPTS_1D) && (opt != INS_OPTS_2D));
                    fmt = IF_DV_3A;
                    break;
                }
                goto case INS_lsl;
            }

            case INS_lsl:
            case INS_lsr:
            case INS_asr:
            case INS_ror:
            case INS_adc:
            case INS_adcs:
            case INS_sbc:
            case INS_sbcs:
            case INS_udiv:
            case INS_sdiv:
            case INS_mneg:
            case INS_smnegl:
            case INS_smulh:
            case INS_umnegl:
            case INS_umulh:
            case INS_lslv:
            case INS_lsrv:
            case INS_asrv:
            case INS_rorv:
            case INS_crc32b:
            case INS_crc32h:
            case INS_crc32w:
            case INS_crc32x:
            case INS_crc32cb:
            case INS_crc32ch:
            case INS_crc32cw:
            case INS_crc32cx:
            {
                assert(insOptsNone(opt));
                assert(isValidGeneralDatasize(size));
                assert(isGeneralRegister(reg1));
                assert(isGeneralRegister(reg2));
                assert(isGeneralRegister(reg3));
                if (ins is INS_smull or INS_smnegl or INS_smulh or INS_umull or INS_umnegl or INS_umulh)
                {
                    assert(size == EA_8BYTE);
                }
                fmt = IF_DR_3A;
                break;
            }

            case INS_add:
            case INS_sub:
            {
                if (isVectorRegister(reg1))
                {
                    assert(isVectorRegister(reg2));
                    assert(isVectorRegister(reg3));
                    if (insOptsAnyArrangement(opt))
                    {
                        assert(opt != INS_OPTS_1D);
                        assert(isValidVectorDatasize(size));
                        assert(isValidArrangement(size, opt));
                        fmt = IF_DV_3A;
                    }
                    else
                    {
                        assert(insOptsNone(opt));
                        assert(size == EA_8BYTE);
                        fmt = IF_DV_3E;
                    }
                    break;
                }
                goto case INS_adds;
            }

            case INS_adds:
            case INS_subs:
            {
                emitIns_R_R_R_I(ins, attr, reg1, reg2, reg3, 0, opt);
                return;
            }

            case INS_cmeq:
            case INS_cmge:
            case INS_cmgt:
            case INS_cmhi:
            case INS_cmhs:
            case INS_cmtst:
            case INS_srshl:
            case INS_sshl:
            case INS_urshl:
            case INS_ushl:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                if (insOptsAnyArrangement(opt))
                {
                    assert(isValidArrangement(size, opt));
                    assert(opt != INS_OPTS_1D);
                    fmt = IF_DV_3A;
                }
                else
                {
                    assert(insOptsNone(opt));
                    assert(size == EA_8BYTE);
                    fmt = IF_DV_3E;
                }
                break;
            }

            case INS_sqadd:
            case INS_sqrshl:
            case INS_sqshl:
            case INS_sqsub:
            case INS_uqadd:
            case INS_uqrshl:
            case INS_uqshl:
            case INS_uqsub:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                if (insOptsAnyArrangement(opt))
                {
                    assert(isValidArrangement(size, opt));
                    assert(opt != INS_OPTS_1D);
                    fmt = IF_DV_3A;
                }
                else
                {
                    assert(insOptsNone(opt));
                    assert(isValidVectorElemsize(size));
                    fmt = IF_DV_3E;
                }
                break;
            }

            case INS_fcmeq:
            case INS_fcmge:
            case INS_fcmgt:
            case INS_frecps:
            case INS_frsqrts:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                if (insOptsAnyArrangement(opt))
                {
                    assert(isValidVectorDatasize(size));
                    assert(isValidArrangement(size, opt));
                    elemsize = optGetElemsize(opt);
                    assert((elemsize == EA_8BYTE) || (elemsize == EA_4BYTE));
                    assert(opt != INS_OPTS_1D);
                    fmt = IF_DV_3B;
                }
                else
                {
                    assert(insOptsNone(opt));
                    assert((size == EA_8BYTE) || (size == EA_4BYTE));
                    fmt = IF_DV_3D;
                }
                break;
            }

            case INS_mla:
            case INS_mls:
            case INS_saba:
            case INS_sabd:
            case INS_shadd:
            case INS_shsub:
            case INS_smax:
            case INS_smaxp:
            case INS_smin:
            case INS_sminp:
            case INS_srhadd:
            case INS_uaba:
            case INS_uabd:
            case INS_uhadd:
            case INS_uhsub:
            case INS_umax:
            case INS_umaxp:
            case INS_umin:
            case INS_uminp:
            case INS_urhadd:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidArrangement(size, opt));
                assert((opt != INS_OPTS_1D) && (opt != INS_OPTS_2D));
                fmt = IF_DV_3A;
                break;
            }

            case INS_addp:
            case INS_uzp1:
            case INS_uzp2:
            case INS_zip1:
            case INS_zip2:
            case INS_trn1:
            case INS_trn2:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidArrangement(size, opt));
                assert(opt != INS_OPTS_1D);
                fmt = IF_DV_3A;
                break;
            }

            case INS_mov:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(reg2 == reg3);
                assert(isValidVectorDatasize(size));
                // Vector MOV is an ORR alias.
                if (opt == INS_OPTS_NONE)
                {
                    elemsize = EA_1BYTE;
                    opt = optMakeArrangement(size, elemsize);
                }
                assert(isValidArrangement(size, opt));
                fmt = IF_DV_3C;
                break;
            }

            case INS_and:
            case INS_bic:
            case INS_eor:
            case INS_orr:
            case INS_orn:
            case INS_tbl:
            case INS_tbl_2regs:
            case INS_tbl_3regs:
            case INS_tbl_4regs:
            case INS_tbx:
            case INS_tbx_2regs:
            case INS_tbx_3regs:
            case INS_tbx_4regs:
            {
                if (isVectorRegister(reg1))
                {
                    assert(isValidVectorDatasize(size));
                    assert(isVectorRegister(reg2));
                    assert(isVectorRegister(reg3));
                    if (opt == INS_OPTS_NONE)
                    {
                        elemsize = EA_1BYTE;
                        opt = optMakeArrangement(size, elemsize);
                    }
                    assert(isValidArrangement(size, opt));
                    fmt = IF_DV_3C;
                    break;
                }
                goto case INS_ands;
            }

            case INS_ands:
            case INS_bics:
            case INS_eon:
            {
                emitIns_R_R_R_I(ins, attr, reg1, reg2, reg3, 0, INS_OPTS_NONE);
                return;
            }

            case INS_bsl:
            case INS_bit:
            case INS_bif:
            {
                assert(isValidVectorDatasize(size));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                if (opt == INS_OPTS_NONE)
                {
                    elemsize = EA_1BYTE;
                    opt = optMakeArrangement(size, elemsize);
                }
                assert(isValidArrangement(size, opt));
                fmt = IF_DV_3C;
                break;
            }

            case INS_fadd:
            case INS_fsub:
            case INS_fdiv:
            case INS_fmax:
            case INS_fmaxnm:
            case INS_fmin:
            case INS_fminnm:
            case INS_fabd:
            case INS_fmul:
            case INS_fmulx:
            case INS_facge:
            case INS_facgt:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                if (insOptsAnyArrangement(opt))
                {
                    assert(isValidVectorDatasize(size));
                    assert(isValidArrangement(size, opt));
                    elemsize = optGetElemsize(opt);
                    assert(isValidVectorElemsizeFloat(elemsize));
                    assert(opt != INS_OPTS_1D);
                    fmt = IF_DV_3B;
                }
                else
                {
                    assert(insOptsNone(opt));
                    assert(isValidScalarDatasize(size));
                    fmt = IF_DV_3D;
                }
                break;
            }

            case INS_fnmul:
            {
                assert(insOptsNone(opt));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidScalarDatasize(size));
                fmt = IF_DV_3D;
                break;
            }

            case INS_faddp:
            case INS_fmaxnmp:
            case INS_fmaxp:
            case INS_fminnmp:
            case INS_fminp:
            case INS_fmla:
            case INS_fmls:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsAnyArrangement(opt));
                assert(isValidVectorDatasize(size));
                assert(isValidArrangement(size, opt));
                elemsize = optGetElemsize(opt);
                assert(isValidVectorElemsizeFloat(elemsize));
                assert(opt != INS_OPTS_1D);
                fmt = IF_DV_3B;
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
            {
                emitIns_R_R_R_Ext(ins, attr, reg1, reg2, reg3, opt);
                return;
            }

            case INS_ldp:
            case INS_ldpsw:
            case INS_ldnp:
            case INS_stp:
            case INS_stnp:
            {
                emitIns_R_R_R_I(ins, attr, reg1, reg2, reg3, 0);
                return;
            }

            case INS_stxr:
            case INS_stxrb:
            case INS_stxrh:
            case INS_stlxr:
            case INS_stlxrb:
            case INS_stlxrh:
            {
                assert(isGeneralRegisterOrZR(reg1));
                assert(isGeneralRegisterOrZR(reg2));
                assert(isGeneralRegisterOrSP(reg3));
                fmt = IF_LS_3D;
                break;
            }

            case INS_casb:
            case INS_casab:
            case INS_casalb:
            case INS_caslb:
            case INS_cash:
            case INS_casah:
            case INS_casalh:
            case INS_caslh:
            case INS_cas:
            case INS_casa:
            case INS_casal:
            case INS_casl:
            case INS_ldaddb:
            case INS_ldaddab:
            case INS_ldaddalb:
            case INS_ldaddlb:
            case INS_ldaddh:
            case INS_ldaddah:
            case INS_ldaddalh:
            case INS_ldaddlh:
            case INS_ldadd:
            case INS_ldadda:
            case INS_ldaddal:
            case INS_ldaddl:
            case INS_ldclral:
            case INS_ldsetal:
            case INS_swpb:
            case INS_swpab:
            case INS_swpalb:
            case INS_swplb:
            case INS_swph:
            case INS_swpah:
            case INS_swpalh:
            case INS_swplh:
            case INS_swp:
            case INS_swpa:
            case INS_swpal:
            case INS_swpl:
            {
                assert(isGeneralRegisterOrZR(reg1));
                assert(isGeneralRegisterOrZR(reg2));
                assert(isGeneralRegisterOrSP(reg3));
                fmt = IF_LS_3E;
                break;
            }

            case INS_sha256h:
            case INS_sha256h2:
            case INS_sha256su1:
            case INS_sha1su0:
            case INS_sha1c:
            case INS_sha1p:
            case INS_sha1m:
            {
                assert(isValidVectorDatasize(size));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                if (opt == INS_OPTS_NONE)
                {
                    elemsize = EA_4BYTE;
                    opt = optMakeArrangement(size, elemsize);
                }
                assert(isValidArrangement(size, opt));
                fmt = IF_DV_3F;
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
                assert(isGeneralRegister(reg3));
                assert(isValidArrangement(size, opt));
                reg2 = encodingSPtoZR(reg2);
                fmt = IF_LS_3F;
                break;
            }

            case INS_addhn:
            case INS_raddhn:
            case INS_rsubhn:
            case INS_subhn:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(size == EA_8BYTE);
                assert(isValidArrangement(size, opt));
                assert(opt != INS_OPTS_1D);
                fmt = IF_DV_3A;
                break;
            }

            case INS_addhn2:
            case INS_raddhn2:
            case INS_rsubhn2:
            case INS_subhn2:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(size == EA_16BYTE);
                assert(isValidArrangement(size, opt));
                assert(opt != INS_OPTS_2D);
                fmt = IF_DV_3A;
                break;
            }

            case INS_sabal:
            case INS_sabdl:
            case INS_saddl:
            case INS_saddw:
            case INS_smlal:
            case INS_smlsl:
            case INS_ssubl:
            case INS_ssubw:
            case INS_uabal:
            case INS_uabdl:
            case INS_uaddl:
            case INS_uaddw:
            case INS_umlal:
            case INS_umlsl:
            case INS_usubl:
            case INS_usubw:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(size == EA_8BYTE);
                assert((opt == INS_OPTS_8B) || (opt == INS_OPTS_4H) || (opt == INS_OPTS_2S));
                fmt = IF_DV_3A;
                break;
            }

            case INS_sabal2:
            case INS_sabdl2:
            case INS_saddl2:
            case INS_saddw2:
            case INS_smlal2:
            case INS_smlsl2:
            case INS_ssubl2:
            case INS_ssubw2:
            case INS_umlal2:
            case INS_umlsl2:
            case INS_smull2:
            case INS_uabal2:
            case INS_uabdl2:
            case INS_uaddl2:
            case INS_uaddw2:
            case INS_usubl2:
            case INS_umull2:
            case INS_usubw2:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(size == EA_16BYTE);
                assert((opt == INS_OPTS_16B) || (opt == INS_OPTS_8H) || (opt == INS_OPTS_4S));
                fmt = IF_DV_3A;
                break;
            }

            case INS_sqdmlal:
            case INS_sqdmlsl:
            case INS_sqdmull:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                if (insOptsAnyArrangement(opt))
                {
                    assert(size == EA_8BYTE);
                    assert((opt == INS_OPTS_4H) || (opt == INS_OPTS_2S));
                    fmt = IF_DV_3A;
                }
                else
                {
                    assert(insOptsNone(opt));
                    assert((size == EA_2BYTE) || (size == EA_4BYTE));
                    fmt = IF_DV_3E;
                }
                break;
            }

            case INS_sqdmulh:
            case INS_sqrdmlah:
            case INS_sqrdmlsh:
            case INS_sqrdmulh:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                if (insOptsAnyArrangement(opt))
                {
                    assert(isValidVectorDatasize(size));
                    elemsize = optGetElemsize(opt);
                    assert((elemsize == EA_2BYTE) || (elemsize == EA_4BYTE));
                    fmt = IF_DV_3A;
                }
                else
                {
                    assert(insOptsNone(opt));
                    assert((size == EA_2BYTE) || (size == EA_4BYTE));
                    fmt = IF_DV_3E;
                }
                break;
            }

            case INS_sqdmlal2:
            case INS_sqdmlsl2:
            case INS_sqdmull2:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(size == EA_16BYTE);
                assert((opt == INS_OPTS_8H) || (opt == INS_OPTS_4S));
                fmt = IF_DV_3A;
                break;
            }

            case INS_pmul:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidArrangement(size, opt));
                assert((opt == INS_OPTS_8B) || (opt == INS_OPTS_16B));
                fmt = IF_DV_3A;
                break;
            }

            case INS_pmull:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(size == EA_8BYTE);
                assert((opt == INS_OPTS_8B) || (opt == INS_OPTS_1D));
                fmt = IF_DV_3A;
                break;
            }

            case INS_pmull2:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(size == EA_16BYTE);
                assert((opt == INS_OPTS_16B) || (opt == INS_OPTS_2D));
                fmt = IF_DV_3A;
                break;
            }

            case INS_sdot:
            case INS_udot:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(((size == EA_8BYTE) && (opt == INS_OPTS_2S)) ||
                    ((size == EA_16BYTE) && (opt == INS_OPTS_4S)));
                fmt = IF_DV_3A;
                break;
            }

            case INS_sha512h:
            case INS_sha512h2:
            case INS_sha512su1:
            case INS_rax1:
            {
                assert(size == EA_16BYTE);
                assert(opt == INS_OPTS_2D);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                fmt = IF_DV_3H;
                break;
            }

            case INS_sm3partw1:
            case INS_sm3partw2:
            case INS_sm4ekey:
            {
                assert(size == EA_16BYTE);
                assert(opt == INS_OPTS_4S);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                fmt = IF_DV_3H;
                break;
            }

            default:
            {
                emitInsSve_R_R_R(ins, attr, reg1, reg2, reg3, opt, sopt);
                return;
            }
        }
        assert(fmt != IF_NONE);

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(opt);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idReg3(reg3);

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitIns_R_R_R_I(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        regNumber reg3, nint imm, insOpts opt = INS_OPTS_NONE, emitAttr attrReg2 = EA_UNKNOWN,
        insScalableOpts sopt = insScalableOpts.INS_SCALABLE_OPTS_NONE)
    {
        var size = EA_SIZE(attr);
        emitAttr elemsize;
        var fmt = IF_NONE;
        var isLdSt = false;
        var isSIMD = false;
        var isAddSub = false;
        var setFlags = false;
        var scale = 0;

        switch (ins)
        {
            case INS_ins:
            case INS_rshrn2:
            case INS_shrn2:
            case INS_sli:
            case INS_sqrshrn2:
            case INS_sqrshrun2:
            case INS_sqshrn2:
            case INS_sqshrun2:
            case INS_sri:
            case INS_srsra:
            case INS_ssra:
            case INS_uqrshrn2:
            case INS_uqshrn2:
            case INS_ursra:
            case INS_usra:
            {
                // RMW instructions copy the destination before applying the operation.
                emitIns_Mov(INS_mov, attr, reg1, reg2, canSkip: true);
                emitIns_R_R_I(ins, attr, reg1, reg3, imm, opt, sopt);
                return;
            }

            case INS_extr:
            {
                assert(insOptsNone(opt));
                assert(isValidGeneralDatasize(size));
                assert(isGeneralRegister(reg1));
                assert(isGeneralRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isValidImmShift(imm, size));
                fmt = IF_DR_3E;
                break;
            }

            case INS_and:
            case INS_ands:
            case INS_eor:
            case INS_orr:
            case INS_bic:
            case INS_bics:
            case INS_eon:
            case INS_orn:
            {
                assert(isValidGeneralDatasize(size));
                assert(isGeneralRegister(reg1));
                assert(isGeneralRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isValidImmShift(imm, size));
                if (imm == 0)
                {
                    assert(insOptsNone(opt)); // Zero immediate means no shift kind.
                    fmt = IF_DR_3A;
                }
                else
                {
                    assert(insOptsAnyShift(opt));
                    fmt = IF_DR_3B;
                }
                break;
            }

            case INS_fmul:
            case INS_fmla:
            case INS_fmls:
            case INS_fmulx:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                if (insOptsAnyArrangement(opt))
                {
                    assert(isValidVectorDatasize(size));
                    assert(isValidArrangement(size, opt));
                    elemsize = optGetElemsize(opt);
                    assert(isValidVectorElemsizeFloat(elemsize));
                    assert(isValidVectorIndex(EA_16BYTE, elemsize, imm));
                    assert(opt != INS_OPTS_1D); // Reserved encoding.
                    fmt = IF_DV_3BI;
                }
                else
                {
                    assert(insOptsNone(opt));
                    assert(isValidScalarDatasize(size));
                    elemsize = size;
                    assert(isValidVectorIndex(EA_16BYTE, elemsize, imm));
                    fmt = IF_DV_3DI;
                }
                break;
            }

            case INS_mul:
            case INS_mla:
            case INS_mls:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(insOptsAnyArrangement(opt));
                assert(isValidVectorDatasize(size));
                assert(isValidArrangement(size, opt));
                elemsize = optGetElemsize(opt);
                assert(isValidVectorIndex(EA_16BYTE, elemsize, imm));
                assert((elemsize == EA_2BYTE) || (elemsize == EA_4BYTE));
                // Halfword indexed forms only encode V0-V15 for the element source.
                if ((elemsize == EA_2BYTE) && ((reg3.SingleTypeMask & SRBM_ASIMD_INDEXED_H_ELEMENT_ALLOWED_REGS) == 0))
                {
                    noway_assert(false, "Invalid reg3");
                }
                fmt = IF_DV_3AI;
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

            case INS_ldpsw:
            {
                scale = 2;
                isLdSt = true;
                break;
            }

            case INS_ldnp:
            case INS_stnp:
            {
                assert(insOptsNone(opt)); // Non-temporal pairs cannot use pre/post indexing.
                goto case INS_ldp;
            }

            case INS_ldp:
            case INS_stp:
            {
                if (isVectorRegister(reg1))
                {
                    scale = (int)NaturalScale_helper(size);
                    isSIMD = true;
                }
                else
                {
                    scale = (size == EA_8BYTE) ? 3 : 2;
                }
                isLdSt = true;
                fmt = IF_LS_3C;
                break;
            }

            case INS_ld1:
            case INS_ld2:
            case INS_ld3:
            case INS_ld4:
            case INS_st1:
            case INS_st2:
            case INS_st3:
            case INS_st4:
            {
                assert(isVectorRegister(reg1));
                assert(isGeneralRegisterOrSP(reg2));
                assert(isGeneralRegister(reg3));
                assert(insOptsPostIndex(opt));
                elemsize = size;
                assert(isValidVectorElemsize(elemsize));
                assert(isValidVectorIndex(EA_16BYTE, elemsize, imm));

                // Single-structure access post-indexed by a register.
                reg2 = encodingSPtoZR(reg2);
                fmt = IF_LS_3G;
                break;
            }

            case INS_ext:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidVectorDatasize(size));
                assert(isValidArrangement(size, opt));
                assert((opt == INS_OPTS_8B) || (opt == INS_OPTS_16B));
                assert(isValidVectorIndex(size, EA_1BYTE, imm));
                fmt = IF_DV_3G;
                break;
            }

            case INS_smlal:
            case INS_smlsl:
            case INS_smull:
            case INS_umlal:
            case INS_umlsl:
            case INS_umull:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(size == EA_8BYTE);
                assert((opt == INS_OPTS_4H) || (opt == INS_OPTS_2S));
                elemsize = optGetElemsize(opt);
                if ((elemsize == EA_2BYTE) && ((reg3.SingleTypeMask & SRBM_ASIMD_INDEXED_H_ELEMENT_ALLOWED_REGS) == 0))
                {
                    assert(false, "Invalid reg3");
                }
                assert(isValidVectorIndex(EA_16BYTE, elemsize, imm));
                fmt = IF_DV_3AI;
                break;
            }

            case INS_sqdmlal:
            case INS_sqdmlsl:
            case INS_sqdmull:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                if (insOptsAnyArrangement(opt))
                {
                    assert(size == EA_8BYTE);
                    assert((opt == INS_OPTS_4H) || (opt == INS_OPTS_2S));
                    elemsize = optGetElemsize(opt);
                    fmt = IF_DV_3AI;
                }
                else
                {
                    assert(insOptsNone(opt));
                    assert((size == EA_2BYTE) || (size == EA_4BYTE));
                    elemsize = size;
                    fmt = IF_DV_3EI;
                }
                if ((elemsize == EA_2BYTE) && ((reg3.SingleTypeMask & SRBM_ASIMD_INDEXED_H_ELEMENT_ALLOWED_REGS) == 0))
                {
                    assert(false, "Invalid reg3");
                }
                assert(isValidVectorIndex(EA_16BYTE, elemsize, imm));
                break;
            }

            case INS_sqdmulh:
            case INS_sqrdmlah:
            case INS_sqrdmlsh:
            case INS_sqrdmulh:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                if (insOptsAnyArrangement(opt))
                {
                    assert(isValidVectorDatasize(size));
                    elemsize = optGetElemsize(opt);
                    assert((elemsize == EA_2BYTE) || (elemsize == EA_4BYTE));
                    fmt = IF_DV_3AI;
                }
                else
                {
                    assert(insOptsNone(opt));
                    assert((size == EA_2BYTE) || (size == EA_4BYTE));
                    elemsize = size;
                    fmt = IF_DV_3EI;
                }
                if ((elemsize == EA_2BYTE) && ((reg3.SingleTypeMask & SRBM_ASIMD_INDEXED_H_ELEMENT_ALLOWED_REGS) == 0))
                {
                    assert(false, "Invalid reg3");
                }
                assert(isValidVectorIndex(EA_16BYTE, elemsize, imm));
                break;
            }

            case INS_smlal2:
            case INS_smlsl2:
            case INS_smull2:
            case INS_sqdmlal2:
            case INS_sqdmlsl2:
            case INS_sqdmull2:
            case INS_umlal2:
            case INS_umlsl2:
            case INS_umull2:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(size == EA_16BYTE);
                assert((opt == INS_OPTS_8H) || (opt == INS_OPTS_4S));
                elemsize = optGetElemsize(opt);
                assert(isValidVectorIndex(EA_16BYTE, elemsize, imm));
                if ((elemsize == EA_2BYTE) && ((reg3.SingleTypeMask & SRBM_ASIMD_INDEXED_H_ELEMENT_ALLOWED_REGS) == 0))
                {
                    assert(false, "Invalid reg3");
                }
                fmt = IF_DV_3AI;
                break;
            }

            case INS_sdot:
            case INS_udot:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(((size == EA_8BYTE) && (opt == INS_OPTS_2S)) || ((size == EA_16BYTE) && (opt == INS_OPTS_4S)));
                assert(isValidVectorIndex(EA_16BYTE, EA_4BYTE, imm));
                fmt = IF_DV_3AI;
                break;
            }

            case INS_xar:
            {
                assert(size == EA_16BYTE);
                assert(opt == INS_OPTS_2D);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isValidUimm(imm, 6));
                fmt = IF_DV_3I;
                break;
            }

            default:
            {
                emitInsSve_R_R_R_I(ins, attr, reg1, reg2, reg3, imm, opt, sopt);
                return;
            }
        }

        assert(insScalableOptsNone(sopt));
        if (isLdSt)
        {
            assert(!isAddSub);
            assert(isGeneralRegisterOrSP(reg3));
            assert(insOptsNone(opt) || insOptsIndexed(opt));
            if (isSIMD)
            {
                assert(isValidVectorLSPDatasize(size));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert((scale >= 2) && (scale <= 4));
            }
            else
            {
                assert(isValidGeneralDatasize(size));
                assert(isGeneralRegisterOrZR(reg1));
                assert(isGeneralRegisterOrZR(reg2));
                assert((scale == 2) || (scale == 3));
            }

            // Load destinations must differ; indexed pairs cannot overwrite their base.
            if (emitInsIsLoad(ins))
            {
                assert(reg1 != reg2);
            }
            if (insOptsIndexed(opt))
            {
                assert(reg1 != reg3);
                assert(reg2 != reg3);
            }
            reg3 = encodingSPtoZR(reg3);

            nint mask = (1 << scale) - 1;
            if (imm == 0)
            {
                assert(insOptsNone(opt));
                fmt = IF_LS_3B;
            }
            else
            {
                if ((imm & mask) == 0)
                {
                    imm >>= scale;
                    if ((imm >= -64) && (imm <= 63))
                    {
                        fmt = IF_LS_3C;
                    }
                }
#if DEBUG
                if (fmt != IF_LS_3C)
                {
                    assert(false, "Instruction cannot be encoded: IF_LS_3C");
                }
#endif
            }
        }
        else if (isAddSub)
        {
            var reg2IsSP = reg2 == REG_SP;
            assert(!isLdSt);
            assert(isValidGeneralDatasize(size));
            assert(isGeneralRegister(reg3));

            // Flag-setting and shifted forms cannot encode SP in the destination.
            if (setFlags || insOptsAluShift(opt))
            {
                assert(isGeneralRegisterOrZR(reg1));
            }
            else
            {
                assert(isGeneralRegisterOrSP(reg1));
                reg1 = encodingSPtoZR(reg1);
            }
            if (insOptsAluShift(opt))
            {
                assert(isGeneralRegister(reg2));
            }
            else
            {
                assert(isGeneralRegisterOrSP(reg2));
                reg2 = encodingSPtoZR(reg2);
            }

            if (insOptsAnyExtend(opt))
            {
                assert((imm >= 0) && (imm <= 4));
                fmt = IF_DR_3C;
            }
            else if (insOptsAluShift(opt))
            {
                assert(isValidImmShift(imm, size) && (imm != 0));
                fmt = IF_DR_3B;
            }
            else if (imm == 0)
            {
                assert(insOptsNone(opt));
                if (reg2IsSP)
                {
                    // SP as the first source requires the extended form with LSL zero.
                    opt = INS_OPTS_LSL;
                    fmt = IF_DR_3C;
                }
                else
                {
                    fmt = IF_DR_3A;
                }
            }
            else
            {
                assert(false, "Instruction cannot be encoded: Add/Sub IF_DR_3A");
            }
        }
        assert(fmt != IF_NONE);

        var id = emitNewInstrCns(attr, imm);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(opt);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idReg3(reg3);

        id.idGCrefReg2(GCT_NONE);
        if (attrReg2 != EA_UNKNOWN)
        {
            assert((fmt == IF_LS_3B) || (fmt == IF_LS_3C));
            if (EA_IS_GCREF(attrReg2))
            {
                id.idGCrefReg2(GCT_GCREF);
            }
            else if (EA_IS_BYREF(attrReg2))
            {
                id.idGCrefReg2(GCT_BYREF);
            }
        }

        dispIns(id);
        appendToCurIG(id);
    }

    private static bool insScalableOptsNone(insScalableOpts sopt)
    {
        return sopt == insScalableOpts.INS_SCALABLE_OPTS_NONE;
    }

    public void emitIns_R_R_R_Ext(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        regNumber reg3, insOpts opt = INS_OPTS_NONE, int shiftAmount = -1)
    {
        var size = EA_SIZE(attr);
        var isSIMD = false;
        int scale;
        switch (ins)
        {
            case INS_ldrb:
            case INS_ldrsb:
            case INS_strb:
            {
                scale = 0;
                break;
            }

            case INS_ldrh:
            case INS_ldrsh:
            case INS_strh:
            {
                scale = 1;
                break;
            }

            case INS_ldrsw:
            {
                scale = 2;
                break;
            }

            case INS_ldr:
            case INS_str:
            {
                if (isVectorRegister(reg1))
                {
                    assert(isValidVectorLSDatasize(size));
                    scale = (int)NaturalScale_helper(size);
                    isSIMD = true;
                }
                else
                {
                    assert(isValidGeneralDatasize(size));
                    scale = (size == EA_8BYTE) ? 3 : 2;
                }
                break;
            }

            default:
            {
                unreached();
                return;
            }
        }
        assert(scale != -1);
        assert(insOptsLSExtend(opt));
        if (isSIMD)
        {
            assert(isValidVectorLSDatasize(size));
            assert(isVectorRegister(reg1));
        }
        else
        {
            assert(isValidGeneralLSDatasize(size));
            assert(isGeneralRegisterOrZR(reg1));
        }
        assert(isGeneralRegisterOrSP(reg2));
        assert(isGeneralRegister(reg3));
        if (insOptsIndexed(opt))
        {
            assert(reg1 != reg2);
        }

        if (shiftAmount == -1)
        {
            shiftAmount = insOptsLSL(opt) ? scale : 0;
        }
        assert((shiftAmount == scale) || (shiftAmount == 0));
        reg2 = encodingSPtoZR(reg2);

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idInsFmt(IF_LS_3A);
        id.idInsOpt(opt);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idReg3(reg3);
        id.idReg3Scaled(shiftAmount == scale);

        dispIns(id);
        appendToCurIG(id);
    }
}
#endif
