// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64 && DEBUG
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe void emitInsSveSanityCheck(instrDesc id)
    {
        nint imm;
        switch (id.idInsFmt())
        {
            case IF_SVE_CK_2A:
            {
                assert(isPredicateRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                break;
            }

            // Scalable.
            case IF_SVE_AA_3A:
            case IF_SVE_CM_3A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            // Scalable, .S or .D.
            case IF_SVE_AC_3A:
            case IF_SVE_CL_3A:
            {
                assert(insOptsScalableWords(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            // Scalable, merge or zero predicate.
            case IF_SVE_AH_3A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            // Scalable, with shift immediate.
            case IF_SVE_AM_2A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isValidVectorShiftAmount(emitGetInsSC(id), optGetSveElemsize(id.idInsOpt()), true));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            // Scalable wide.
            case IF_SVE_AO_3A:
            {
                assert(insOptsScalableWide(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            // Scalable to/from SIMD scalar.
            case IF_SVE_AF_3A:
            case IF_SVE_AK_3A:
            case IF_SVE_CN_3A:
            case IF_SVE_CP_3A:
            case IF_SVE_CR_3A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            // Scalable to FP SIMD scalar.
            case IF_SVE_HE_3A:
            case IF_SVE_HJ_3A:
            {
                assert(insOptsScalableFloat(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            // Scalable to general register.
            case IF_SVE_CO_3A:
            case IF_SVE_CS_3A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isGeneralRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isValidScalarDatasize(id.idOpSize()));
                break;
            }

            // Four scalable registers; the locations of reg3 and reg4 can switch.
            case IF_SVE_AR_4A:
            case IF_SVE_AS_4A:
            case IF_SVE_GI_4A:
            case IF_SVE_HU_4A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isVectorRegister(id.idReg4()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            // Scalable, unpredicated.
            case IF_SVE_AT_3A:
            case IF_SVE_BG_3A:
            case IF_SVE_BZ_3A:
            case IF_SVE_BZ_3A_A:
            case IF_SVE_EH_3A:
            case IF_SVE_EL_3A:
            case IF_SVE_EM_3A:
            case IF_SVE_EX_3A:
            case IF_SVE_FL_3A:
            case IF_SVE_FM_3A:
            case IF_SVE_FW_3A:
            case IF_SVE_GF_3A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            // Scalable, unpredicated, narrowing.
            case IF_SVE_GC_3A:
            {
                assert(insOptsScalableWide(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            // Scalable without predicates, with general-purpose source registers.
            case IF_SVE_BA_3A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isGeneralRegisterOrZR(id.idReg2()));
                assert(isGeneralRegisterOrZR(id.idReg3()));
                assert(isValidScalarDatasize(id.idOpSize()));
                break;
            }

            case IF_SVE_BH_3A:
            {
                assert((id.idInsOpt() == INS_OPTS_SCALABLE_S) || (id.idInsOpt() == INS_OPTS_SCALABLE_D));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isValidUimm(emitGetInsSC(id), 2));
                break;
            }

            case IF_SVE_BH_3B:
            case IF_SVE_BH_3B_A:
            {
                assert((id.idInsOpt() == INS_OPTS_SCALABLE_D_SXTW) || (id.idInsOpt() == INS_OPTS_SCALABLE_D_UXTW));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isValidUimm(emitGetInsSC(id), 2));
                break;
            }

            case IF_SVE_BL_1A:
            case IF_SVE_BM_1A:
            {
                assert(id.idInsOpt() == INS_OPTS_NONE);
                assert(isGeneralRegister(id.idReg1()));
                assert(id.idOpSize() == EA_8BYTE);
                assert(isValidUimmFrom1(emitGetInsSC(id), 4));
                break;
            }

            case IF_SVE_BN_1A:
            case IF_SVE_BP_1A:
            {
                assert(insOptsScalableAtLeastHalf(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isScalableVectorSize(id.idOpSize()));
                assert(isValidUimmFrom1(emitGetInsSC(id), 4));
                break;
            }

            case IF_SVE_BS_1A:
            case IF_SVE_BT_1A:
            {
                imm = emitGetInsSC(id);
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isValidImmNRS(unchecked((nuint)imm), optGetSveElemsize(id.idInsOpt())));
                break;
            }

            case IF_SVE_BO_1A:
            {
                assert(id.idInsOpt() == INS_OPTS_NONE);
                assert(isGeneralRegister(id.idReg1()));
                assert(isValidGeneralDatasize(id.idOpSize()));
                assert(isValidUimmFrom1(emitGetInsSC(id), 4));
                break;
            }

            case IF_SVE_BQ_2A:
            case IF_SVE_BQ_2B:
            {
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_B);
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isValidUimm(emitGetInsSC(id), 8));
                break;
            }

            case IF_SVE_BU_2A:
            {
                imm = emitGetInsSC(id);
                floatImm8 fpImm;
                fpImm.immFPIVal = unchecked((uint)imm);
                assert(insOptsScalableAtLeastHalf(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isValidSimm(unchecked((nint)emitDecodeFloatImm8(fpImm)), 8));
                assert(isPredicateRegister(id.idReg2()));
                assert(isValidVectorElemsize(optGetSveElemsize(id.idInsOpt())));
                break;
            }

            case IF_SVE_BV_2A:
            case IF_SVE_BV_2A_J:
            {
                imm = emitGetInsSC(id);
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isValidVectorElemsize(optGetSveElemsize(id.idInsOpt())));
                assert(isValidSimm(imm, 8));
                break;
            }

            case IF_SVE_BV_2B:
            {
                assert(insOptsScalableAtLeastHalf(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                break;
            }

            case IF_SVE_CE_2A:
            {
                assert(isPredicateRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                break;
            }

            case IF_SVE_CE_2B:
            {
                assert(isPredicateRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isValidUimm(emitGetInsSC(id), 3));
                break;
            }

            case IF_SVE_CE_2C:
            {
                assert(isPredicateRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isValidUimm(emitGetInsSC(id), 1));
                break;
            }

            case IF_SVE_CE_2D:
            {
                assert(isPredicateRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isValidUimm(emitGetInsSC(id), 3));
                break;
            }

            case IF_SVE_CF_2A:
            {
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                break;
            }

            case IF_SVE_CF_2B:
            {
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isValidUimm(emitGetInsSC(id), 3));
                break;
            }

            case IF_SVE_CF_2C:
            {
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isValidUimm(emitGetInsSC(id), 1));
                break;
            }

            case IF_SVE_CF_2D:
            {
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isValidUimm(emitGetInsSC(id), 2));
                break;
            }

            case IF_SVE_CC_2A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                break;
            }

            case IF_SVE_CD_2A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isGeneralRegisterOrZR(id.idReg2()));
                break;
            }

            case IF_SVE_CI_3A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isPredicateRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isPredicateRegister(id.idReg3()));
                break;
            }

            case IF_SVE_CJ_2A:
            {
                assert(isScalableVectorSize(id.idOpSize()));
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isPredicateRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                break;
            }

            case IF_SVE_CT_3A:
            {
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                break;
            }

            // Four scalable registers, with predicate destination.
            case IF_SVE_CX_4A:
            {
                assert(isScalableVectorSize(id.idOpSize()));
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isPredicateRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isVectorRegister(id.idReg4()));
                break;
            }

            case IF_SVE_CX_4A_A:
            {
                assert(isScalableVectorSize(id.idOpSize()));
                assert(insOptsScalableWide(id.idInsOpt()));
                assert(isPredicateRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isVectorRegister(id.idReg4()));
                break;
            }

            case IF_SVE_CY_3A:
            {
                assert(isScalableVectorSize(id.idOpSize()));
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isPredicateRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isValidSimm(emitGetInsSC(id), 5));
                break;
            }

            case IF_SVE_CY_3B:
            {
                assert(isScalableVectorSize(id.idOpSize()));
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isPredicateRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isValidUimm(emitGetInsSC(id), 7));
                break;
            }

            case IF_SVE_BR_3B:
            case IF_SVE_FN_3B:
            case IF_SVE_FO_3A:
            case IF_SVE_AT_3B:
            case IF_SVE_BD_3B:
            case IF_SVE_EF_3A:
            case IF_SVE_EI_3A:
            case IF_SVE_GJ_3A:
            case IF_SVE_GN_3A:
            case IF_SVE_GO_3A:
            case IF_SVE_GW_3B:
            case IF_SVE_HA_3A:
            case IF_SVE_HA_3A_E:
            case IF_SVE_HB_3A:
            case IF_SVE_HD_3A:
            case IF_SVE_HD_3A_A:
            case IF_SVE_HK_3B:
            case IF_SVE_AV_3A:
            {
                assert(insOptsScalable(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                break;
            }

            case IF_SVE_AU_3A:
            {
                assert(insOptsScalable(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert((id.idIns() == INS_sve_mov) || isVectorRegister(id.idReg3()));
                break;
            }

            case IF_SVE_HA_3A_F:
            case IF_SVE_EW_3A:
            case IF_SVE_EW_3B:
            {
                assert(insOptsNone(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                break;
            }

            case IF_SVE_EG_3A:
            case IF_SVE_EY_3A:
            case IF_SVE_EZ_3A:
            case IF_SVE_FD_3B:
            case IF_SVE_FF_3B:
            case IF_SVE_FI_3B:
            case IF_SVE_GU_3A:
            case IF_SVE_GX_3A:
            case IF_SVE_GY_3B:
            case IF_SVE_GY_3B_D:
            case IF_SVE_FK_3B:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert((REG_V0 <= id.idReg3()) && (id.idReg3() <= REG_V7));
                assert(isValidUimm(emitGetInsSC(id), 2));
                break;
            }

            case IF_SVE_FD_3A:
            case IF_SVE_FE_3A:
            case IF_SVE_FF_3A:
            case IF_SVE_FG_3A:
            case IF_SVE_FH_3A:
            case IF_SVE_FI_3A:
            case IF_SVE_FJ_3A:
            case IF_SVE_FK_3A:
            case IF_SVE_GU_3C:
            case IF_SVE_GX_3C:
            case IF_SVE_GY_3A:
            case IF_SVE_GZ_3A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert((REG_V0 <= id.idReg3()) && (id.idReg3() <= REG_V7));
                assert(isValidUimm(emitGetInsSC(id), 3));
                break;
            }

            case IF_SVE_FE_3B:
            case IF_SVE_FG_3B:
            case IF_SVE_FH_3B:
            case IF_SVE_FJ_3B:
            {
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_D);
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isLowVectorRegister(id.idReg3()));
                assert(isValidUimm(emitGetInsSC(id), 2));
                break;
            }

            case IF_SVE_EY_3B:
            case IF_SVE_FD_3C:
            case IF_SVE_FF_3C:
            case IF_SVE_FI_3C:
            case IF_SVE_GU_3B:
            case IF_SVE_GX_3B:
            case IF_SVE_FK_3C:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isLowVectorRegister(id.idReg3()));
                assert(isValidUimm(emitGetInsSC(id), 1));
                break;
            }

            case IF_SVE_CZ_4A:
            {
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_B);
                assert(isPredicateRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isPredicateRegister(id.idReg3()));

                switch (id.idIns())
                {
                    case INS_sve_and:
                    case INS_sve_ands:
                    case INS_sve_bic:
                    case INS_sve_bics:
                    case INS_sve_eor:
                    case INS_sve_eors:
                    case INS_sve_nand:
                    case INS_sve_nands:
                    case INS_sve_nor:
                    case INS_sve_nors:
                    case INS_sve_orn:
                    case INS_sve_orns:
                    case INS_sve_orr:
                    case INS_sve_orrs:
                    case INS_sve_sel:
                    {
                        assert(isPredicateRegister(id.idReg4()));
                        break;
                    }

                    case INS_sve_mov:
                    case INS_sve_movs:
                    case INS_sve_not:
                    case INS_sve_nots:
                    {
                        // These aliases have no fourth register.
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }
                break;
            }

            case IF_SVE_CZ_4A_A:
            case IF_SVE_CZ_4A_L:
            {
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_B);
                assert(isPredicateRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                break;
            }

            case IF_SVE_CZ_4A_K:
            case IF_SVE_DB_3A:
            case IF_SVE_DB_3B:
            case IF_SVE_DC_3A:
            {
                assert(isScalableVectorSize(id.idOpSize()));
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_B);
                assert(isPredicateRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isPredicateRegister(id.idReg3()));
                break;
            }

            case IF_SVE_DA_4A:
            {
                assert(isScalableVectorSize(id.idOpSize()));
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_B);
                assert(isPredicateRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isPredicateRegister(id.idReg3()));
                assert(isPredicateRegister(id.idReg4()));
                break;
            }

            case IF_SVE_DD_2A:
            case IF_SVE_DG_2A:
            {
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_B);
                assert(isPredicateRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                break;
            }

            case IF_SVE_DE_1A:
            {
                assert(isScalableVectorSize(id.idOpSize()));
                assert(isPredicateRegister(id.idReg1()));
                assert(insOptsScalableStandard(id.idInsOpt()));
                break;
            }

            case IF_SVE_DF_2A:
            case IF_SVE_DI_2A:
            {
                assert(isScalableVectorSize(id.idOpSize()));
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isPredicateRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                break;
            }

            case IF_SVE_DH_1A:
            case IF_SVE_DJ_1A:
            {
                assert(isScalableVectorSize(id.idOpSize()));
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_B);
                assert(isPredicateRegister(id.idReg1()));
                break;
            }

            case IF_SVE_DK_3A:
            {
                assert(isScalableVectorSize(id.idOpSize()));
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isGeneralRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isPredicateRegister(id.idReg3()));
                break;
            }

            case IF_SVE_GE_4A:
            {
                assert(isScalableVectorSize(id.idOpSize()));
                assert(insOptsScalableAtMaxHalf(id.idInsOpt()));
                assert(isPredicateRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isVectorRegister(id.idReg4()));
                break;
            }

            case IF_SVE_GQ_3A:
            {
                switch (id.idIns())
                {
                    case INS_sve_fcvtnt:
                    case INS_sve_fcvtlt:
                    {
                        assert(insOptsConvertFloatStepwise(id.idInsOpt()));
                        goto case INS_sve_fcvtxnt;
                    }

                    case INS_sve_fcvtxnt:
                    case INS_sve_bfcvtnt:
                    {
                        assert(isVectorRegister(id.idReg1()));
                        assert(isLowPredicateRegister(id.idReg2()));
                        assert(isVectorRegister(id.idReg3()));
                        break;
                    }

                    default:
                    {
                        assert(false, "!\"unreachable\"");
                        break;
                    }
                }
                break;
            }

            case IF_SVE_HO_3A:
            {
                assert(id.idInsOpt() == INS_OPTS_S_TO_H);
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                break;
            }

            case IF_SVE_HO_3B:
            {
                assert(insOptsConvertFloatToFloat(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                break;
            }

            case IF_SVE_HO_3C:
            {
                assert(id.idInsOpt() == INS_OPTS_D_TO_S);
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                break;
            }

            case IF_SVE_HP_3B:
            {
                assert(insOptsScalableFloat(id.idInsOpt()) || (id.idInsOpt() == INS_OPTS_H_TO_S)
                    || (id.idInsOpt() == INS_OPTS_H_TO_D) || (id.idInsOpt() == INS_OPTS_S_TO_D)
                    || (id.idInsOpt() == INS_OPTS_D_TO_S));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                break;
            }

            case IF_SVE_HS_3A:
            {
                assert(insOptsScalableAtLeastHalf(id.idInsOpt()) || (id.idInsOpt() == INS_OPTS_S_TO_H)
                    || (id.idInsOpt() == INS_OPTS_S_TO_D) || (id.idInsOpt() == INS_OPTS_D_TO_H)
                    || (id.idInsOpt() == INS_OPTS_D_TO_S));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                break;
            }

            case IF_SVE_HT_4A:
            {
                assert(isScalableVectorSize(id.idOpSize()));
                assert(insOptsScalableFloat(id.idInsOpt()));
                assert(isPredicateRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isVectorRegister(id.idReg4()));
                break;
            }

            // Scalable FP.
            case IF_SVE_GR_3A:
            case IF_SVE_HL_3A:
            case IF_SVE_HR_3A:
            {
                assert(insOptsScalableFloat(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_AB_3B:
            case IF_SVE_HL_3B:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            // Scalable to SIMD vector.
            case IF_SVE_AG_3A:
            case IF_SVE_AJ_3A:
            case IF_SVE_AL_3A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(id.idOpSize() == EA_8BYTE);
                break;
            }

            case IF_SVE_GS_3A:
            {
                assert(insOptsScalableFloat(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(id.idOpSize() == EA_8BYTE);
                break;
            }

            // Scalable, widening to scalar SIMD.
            case IF_SVE_AI_3A:
            {
                switch (id.idIns())
                {
                    case INS_sve_saddv:
                    {
                        assert(insOptsScalableWide(id.idInsOpt()));
                        break;
                    }

                    default:
                    {
                        assert(insOptsScalableStandard(id.idInsOpt()));
                        break;
                    }
                }
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            // Scalable, possibly FP.
            case IF_SVE_AP_3A:
            {
                switch (id.idIns())
                {
                    case INS_sve_fabs:
                    case INS_sve_fneg:
                    {
                        assert(insOptsScalableFloat(id.idInsOpt()));
                        break;
                    }

                    default:
                    {
                        assert(insOptsScalableStandard(id.idInsOpt()));
                        break;
                    }
                }
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            // Scalable, with instruction-specific element sizes.
            case IF_SVE_AQ_3A:
            case IF_SVE_CU_3A:
            {
                switch (id.idIns())
                {
                    case INS_sve_abs:
                    case INS_sve_neg:
                    case INS_sve_rbit:
                    {
                        assert(insOptsScalableStandard(id.idInsOpt()));
                        break;
                    }

                    case INS_sve_sxtb:
                    case INS_sve_uxtb:
                    case INS_sve_revb:
                    {
                        assert(insOptsScalableAtLeastHalf(id.idInsOpt()));
                        break;
                    }

                    case INS_sve_sxth:
                    case INS_sve_uxth:
                    case INS_sve_revh:
                    {
                        assert(insOptsScalableWords(id.idInsOpt()));
                        break;
                    }

                    default:
                    {
                        assert(id.idInsOpt() == INS_OPTS_SCALABLE_D);
                        break;
                    }
                }
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_CV_3A:
            case IF_SVE_CV_3B:
            {
                assert(isScalableVectorSize(id.idOpSize()));
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                break;
            }

            case IF_SVE_CW_4A:
            {
                assert(isScalableVectorSize(id.idOpSize()));
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                if (id.idIns() == INS_sve_sel)
                {
                    assert(isVectorRegister(id.idReg4()));
                }
                break;
            }

            // Scalable from general scalar, possibly SP.
            case IF_SVE_CQ_3A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isGeneralRegisterOrZR(id.idReg3()));
                assert(isValidScalarDatasize(id.idOpSize()));
                break;
            }

            case IF_SVE_EQ_3A:
            case IF_SVE_HQ_3A:
            {
                assert(insOptsScalableAtLeastHalf(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            // Scalable, possibly fixed to .S.
            case IF_SVE_ES_3A:
            {
                switch (id.idIns())
                {
                    case INS_sve_sqabs:
                    case INS_sve_sqneg:
                    {
                        assert(insOptsScalableStandard(id.idInsOpt()));
                        break;
                    }

                    default:
                    {
                        assert(id.idInsOpt() == INS_OPTS_SCALABLE_S);
                        break;
                    }
                }
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_GA_2A:
            {
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_H);
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isEvenRegister(id.idReg2()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_DL_2A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isValidVectorElemsize(optGetSveElemsize(id.idInsOpt())));
                assert(isGeneralRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_DO_2A:
            case IF_SVE_DM_2A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isValidVectorElemsize(optGetSveElemsize(id.idInsOpt())));
                assert(isGeneralRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isValidGeneralDatasize(id.idOpSize()));
                break;
            }

            case IF_SVE_DP_2A:
            case IF_SVE_DN_2A:
            {
                assert(insOptsScalableAtLeastHalf(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_DQ_0A:
            {
                break;
            }

            case IF_SVE_DR_1A:
            {
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_B);
                assert(isPredicateRegister(id.idReg1()));
                break;
            }

            case IF_SVE_DS_2A:
            {
                assert(insOptsNone(id.idInsOpt()));
                assert(isGeneralRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                assert(isValidGeneralDatasize(id.idOpSize()));
                break;
            }

            case IF_SVE_FZ_2A:
            {
                assert(insOptsNone(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isEvenRegister(id.idReg2()));
                break;
            }

            case IF_SVE_HG_2A:
            {
                assert(insOptsNone(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isEvenRegister(id.idReg2()));
                break;
            }

            case IF_SVE_GD_2A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(optGetSveElemsize(id.idInsOpt()) != EA_8BYTE);
                assert(isValidVectorElemsize(optGetSveElemsize(id.idInsOpt())));
                break;
            }

            case IF_SVE_BB_2A:
            {
                assert(insOptsNone(id.idInsOpt()));
                assert(id.idOpSize() == EA_8BYTE);
                assert(isGeneralRegisterOrZR(id.idReg1()));
                assert(isGeneralRegisterOrZR(id.idReg2()));
                assert(isValidSimm(emitGetInsSC(id), 6));
                break;
            }

            case IF_SVE_BC_1A:
            {
                assert(insOptsNone(id.idInsOpt()));
                assert(id.idOpSize() == EA_8BYTE);
                assert(isGeneralRegister(id.idReg1()));
                assert(isValidSimm(emitGetInsSC(id), 6));
                break;
            }

            case IF_SVE_AW_2A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isValidVectorElemsize(optGetSveElemsize(id.idInsOpt())));
                imm = emitGetInsSC(id);

                switch (id.idInsOpt())
                {
                    case INS_OPTS_SCALABLE_B:
                    {
                        assert(isValidUimmFrom1(imm, 3));
                        break;
                    }

                    case INS_OPTS_SCALABLE_H:
                    {
                        assert(isValidUimmFrom1(imm, 4));
                        break;
                    }

                    case INS_OPTS_SCALABLE_S:
                    {
                        assert(isValidUimmFrom1(imm, 5));
                        break;
                    }

                    case INS_OPTS_SCALABLE_D:
                    {
                        assert(isValidUimmFrom1(imm, 6));
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }
                break;
            }

            case IF_SVE_AX_1A:
            {
                nint imm1;
                nint imm2;
                insSveDecodeTwoSimm5(emitGetInsSC(id), &imm1, &imm2);
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isValidSimm(imm1, 5));
                assert(isValidSimm(imm2, 5));
                assert(isValidVectorElemsize(optGetSveElemsize(id.idInsOpt())));
                break;
            }

            case IF_SVE_AY_2A:
            case IF_SVE_AZ_2A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isValidSimm(emitGetInsSC(id), 5));
                assert(isIntegerRegister(id.idReg2()));
                assert(isValidVectorElemsize(optGetSveElemsize(id.idInsOpt())));
                break;
            }

            case IF_SVE_FR_2A:
            {
                assert(insOptsScalableAtLeastHalf(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isValidVectorElemsize(optGetSveElemsize(id.idInsOpt())));
                imm = emitGetInsSC(id);

                switch (id.idInsOpt())
                {
                    case INS_OPTS_SCALABLE_H:
                    {
                        assert(isValidUimm(imm, 3));
                        break;
                    }

                    case INS_OPTS_SCALABLE_S:
                    {
                        assert(isValidUimm(imm, 4));
                        break;
                    }

                    case INS_OPTS_SCALABLE_D:
                    {
                        assert(isValidUimm(imm, 5));
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }
                break;
            }

            case IF_SVE_GB_2A:
            {
                assert(insOptsScalableWide(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isValidVectorElemsize(optGetSveElemsize(id.idInsOpt())));
                imm = emitGetInsSC(id);

                switch (id.idInsOpt())
                {
                    case INS_OPTS_SCALABLE_B:
                    {
                        assert(isValidUimmFrom1(imm, 3));
                        break;
                    }

                    case INS_OPTS_SCALABLE_H:
                    {
                        assert(isValidUimmFrom1(imm, 4));
                        break;
                    }

                    case INS_OPTS_SCALABLE_S:
                    {
                        assert(isValidUimmFrom1(imm, 5));
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }
                break;
            }

            case IF_SVE_FV_2A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(emitIsValidEncodedRotationImm90_or_270(emitGetInsSC(id)));
                assert(isValidVectorElemsize(optGetSveElemsize(id.idInsOpt())));
                break;
            }

            case IF_SVE_FY_3A:
            {
                assert(insOptsScalableWords(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isValidVectorElemsize(optGetSveElemsize(id.idInsOpt())));
                break;
            }

            case IF_SVE_GK_2A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                if (id.idInsOpt() == INS_OPTS_SCALABLE_S)
                {
                    assert(id.idIns() == INS_sve_sm4e);
                }
                else
                {
                    assert(id.idInsOpt() == INS_OPTS_SCALABLE_B);
                }
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_GL_1A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_DU_3A:
            {
                assert(id.idOpSize() == EA_8BYTE);
                goto case IF_SVE_DT_3A;
            }

            case IF_SVE_DT_3A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isPredicateRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                assert(isValidGeneralDatasize(id.idOpSize()));
                assert(isGeneralRegister(id.idReg3()));
                assert(isValidVectorElemsize(optGetSveElemsize(id.idInsOpt())));
                break;
            }

            case IF_SVE_DV_4A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isPredicateRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isPredicateRegister(id.idReg3()));
                assert(isGeneralRegister(id.idReg4()));
                assert((REG_R12 <= id.idReg4()) && (id.idReg4() <= REG_R15));
                imm = emitGetInsSC(id);

                switch (id.idInsOpt())
                {
                    case INS_OPTS_SCALABLE_B:
                    {
                        assert(isValidUimm(imm, 4));
                        break;
                    }

                    case INS_OPTS_SCALABLE_H:
                    {
                        assert(isValidUimm(imm, 3));
                        break;
                    }

                    case INS_OPTS_SCALABLE_S:
                    {
                        assert(isValidUimm(imm, 2));
                        break;
                    }

                    case INS_OPTS_SCALABLE_D:
                    {
                        assert(isValidUimm(imm, 1));
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }
                break;
            }

            case IF_SVE_DW_2B:
            {
                assert(isValidUimm(emitGetInsSC(id), 1));
                goto case IF_SVE_DW_2A;
            }

            case IF_SVE_DW_2A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isPredicateRegister(id.idReg1()));
                assert(isHighPredicateRegister(id.idReg2()));
                assert(isValidUimm(emitGetInsSC(id), 2));
                assert(isValidVectorElemsize(optGetSveElemsize(id.idInsOpt())));
                break;
            }

            case IF_SVE_DX_3A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isLowPredicateRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                assert(isValidVectorElemsize(optGetSveElemsize(id.idInsOpt())));
                break;
            }

            case IF_SVE_DY_3A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isHighPredicateRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                assert(isValidVectorElemsize(optGetSveElemsize(id.idInsOpt())));
                break;
            }

            case IF_SVE_DZ_1A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isHighPredicateRegister(id.idReg1()));
                assert(isValidVectorElemsize(optGetSveElemsize(id.idInsOpt())));
                break;
            }

            case IF_SVE_EA_1A:
            {
                assert(insOptsScalableAtLeastHalf(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isValidUimm(emitGetInsSC(id), 8));
                assert(isValidVectorElemsize(optGetSveElemsize(id.idInsOpt())));
                break;
            }

            case IF_SVE_EB_1A:
            {
                imm = emitGetInsSC(id);
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isValidVectorElemsize(optGetSveElemsize(id.idInsOpt())));
                assert(isValidSimm(imm, 8));
                break;
            }

            case IF_SVE_EC_1A:
            {
                imm = emitGetInsSC(id);
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isValidVectorElemsize(optGetSveElemsize(id.idInsOpt())));
                assert(isValidUimm(imm, 8));
                break;
            }

            case IF_SVE_EB_1B:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isValidVectorElemsize(optGetSveElemsize(id.idInsOpt())));
                break;
            }

            case IF_SVE_ED_1A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isValidSimm(emitGetInsSC(id), 8) || isValidUimm(emitGetInsSC(id), 8));
                assert(isValidVectorElemsize(optGetSveElemsize(id.idInsOpt())));
                break;
            }

            case IF_SVE_EE_1A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isValidSimm(emitGetInsSC(id), 8));
                assert(isValidVectorElemsize(optGetSveElemsize(id.idInsOpt())));
                break;
            }

            case IF_SVE_EJ_3A:
            {
                assert(insOptsScalableWords(id.idInsOpt()));
                goto case IF_SVE_EK_3A;
            }

            case IF_SVE_EK_3A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(emitIsValidEncodedRotationImm0_to_270(emitGetInsSC(id)));
                assert(isVectorRegister(id.idReg3()));
                assert(isValidVectorElemsize(optGetSveElemsize(id.idInsOpt())));
                break;
            }

            case IF_SVE_FA_3A:
            case IF_SVE_FB_3A:
            case IF_SVE_FC_3A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert((REG_V0 <= id.idReg3()) && (id.idReg3() <= REG_V7));
                assert(isValidUimm(emitGetInsSC(id), 4));
                break;
            }

            case IF_SVE_FA_3B:
            case IF_SVE_FB_3B:
            case IF_SVE_FC_3B:
            case IF_SVE_GV_3A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isLowVectorRegister(id.idReg3()));
                assert(isValidUimm(emitGetInsSC(id), 3));
                break;
            }

            case IF_SVE_IH_3A:
            case IF_SVE_IH_3A_A:
            case IF_SVE_IH_3A_F:
            case IF_SVE_IJ_3A:
            case IF_SVE_IJ_3A_D:
            case IF_SVE_IJ_3A_E:
            case IF_SVE_IJ_3A_F:
            case IF_SVE_IJ_3A_G:
            case IF_SVE_IL_3A:
            case IF_SVE_IL_3A_A:
            case IF_SVE_IL_3A_B:
            case IF_SVE_IL_3A_C:
            case IF_SVE_IM_3A:
            case IF_SVE_IO_3A:
            case IF_SVE_IQ_3A:
            case IF_SVE_IS_3A:
            case IF_SVE_JE_3A:
            case IF_SVE_JM_3A:
            case IF_SVE_JN_3C:
            case IF_SVE_JN_3C_D:
            case IF_SVE_JO_3A:
            {
                assert(insOptsScalable(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                assert(isScalableVectorSize(id.idOpSize()));

                switch (id.idIns())
                {
                    case INS_sve_ld2b:
                    case INS_sve_ld2h:
                    case INS_sve_ld2w:
                    case INS_sve_ld2d:
                    case INS_sve_ld2q:
                    case INS_sve_st2b:
                    case INS_sve_st2h:
                    case INS_sve_st2w:
                    case INS_sve_st2d:
                    case INS_sve_st2q:
                    {
                        assert(isValidSimm(emitGetInsSC(id) / 2, 4) && ((emitGetInsSC(id) % 2) == 0));
                        break;
                    }

                    case INS_sve_ld3b:
                    case INS_sve_ld3h:
                    case INS_sve_ld3w:
                    case INS_sve_ld3d:
                    case INS_sve_ld3q:
                    case INS_sve_st3b:
                    case INS_sve_st3h:
                    case INS_sve_st3w:
                    case INS_sve_st3d:
                    case INS_sve_st3q:
                    {
                        assert(isValidSimm(emitGetInsSC(id) / 3, 4) && ((emitGetInsSC(id) % 3) == 0));
                        break;
                    }

                    case INS_sve_ld4b:
                    case INS_sve_ld4h:
                    case INS_sve_ld4w:
                    case INS_sve_ld4d:
                    case INS_sve_ld4q:
                    case INS_sve_st4b:
                    case INS_sve_st4h:
                    case INS_sve_st4w:
                    case INS_sve_st4d:
                    case INS_sve_st4q:
                    {
                        assert(isValidSimm(emitGetInsSC(id) / 4, 4) && ((emitGetInsSC(id) % 4) == 0));
                        break;
                    }

                    case INS_sve_ld1rqb:
                    case INS_sve_ld1rqd:
                    case INS_sve_ld1rqh:
                    case INS_sve_ld1rqw:
                    {
                        assert(isValidSimm(emitGetInsSC(id) / 16, 4) && ((emitGetInsSC(id) % 16) == 0));
                        break;
                    }

                    case INS_sve_ld1rob:
                    case INS_sve_ld1rod:
                    case INS_sve_ld1roh:
                    case INS_sve_ld1row:
                    {
                        assert(isValidSimm(emitGetInsSC(id) / 32, 4) && ((emitGetInsSC(id) % 32) == 0));
                        break;
                    }

                    default:
                    {
                        assert(isValidSimm(emitGetInsSC(id), 4));
                        break;
                    }
                }
                break;
            }

            case IF_SVE_JD_4A:
            {
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                assert(isGeneralRegister(id.idReg4()));
                assert(isScalableVectorSize(id.idOpSize()));

                // ST1H is reserved for scalable B.
                assert((id.idIns() == INS_sve_st1h) ? insOptsScalableAtLeastHalf(id.idInsOpt())
                    : insOptsScalableStandard(id.idInsOpt()));
                break;
            }

            case IF_SVE_JD_4B:
            {
                assert(insOptsScalableWords(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                assert(isGeneralRegister(id.idReg4()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_JJ_4A:
            case IF_SVE_JJ_4A_B:
            case IF_SVE_JJ_4A_C:
            case IF_SVE_JJ_4A_D:
            case IF_SVE_JK_4A:
            case IF_SVE_JK_4A_B:
            {
                assert(insOptsScalable32bitExtends(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_JN_3A:
            {
                imm = emitGetInsSC(id);
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                assert(isScalableVectorSize(id.idOpSize()));
                assert(isValidSimm(imm, 4));
                break;
            }

            case IF_SVE_JN_3B:
            {
                imm = emitGetInsSC(id);
                assert(insOptsScalableWords(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                assert(isScalableVectorSize(id.idOpSize()));
                assert(isValidSimm(imm, 4));
                break;
            }

            case IF_SVE_HW_4A:
            case IF_SVE_HW_4A_A:
            case IF_SVE_HW_4A_B:
            case IF_SVE_HW_4A_C:
            case IF_SVE_IU_4A:
            case IF_SVE_IU_4A_A:
            case IF_SVE_IU_4A_C:
            {
                assert(insOptsScalable32bitExtends(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                assert(isVectorRegister(id.idReg4()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_HW_4B:
            case IF_SVE_HW_4B_D:
            {
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_D);
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                assert(isVectorRegister(id.idReg4()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_IF_4A:
            case IF_SVE_IF_4A_A:
            {
                assert(insOptsScalableWords(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isGeneralRegisterOrZR(id.idReg4()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_IG_4A:
            case IF_SVE_IG_4A_D:
            case IF_SVE_IG_4A_E:
            case IF_SVE_IG_4A_F:
            case IF_SVE_IG_4A_G:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                assert(isGeneralRegisterOrZR(id.idReg4()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_II_4A:
            {
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_D);
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                assert(isGeneralRegister(id.idReg4()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_II_4A_B:
            {
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_Q);
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                assert(isGeneralRegister(id.idReg4()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_II_4A_H:
            {
                assert(insOptsScalableWordsOrQuadwords(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                assert(isGeneralRegister(id.idReg4()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_IK_4A:
            case IF_SVE_IK_4A_F:
            case IF_SVE_IK_4A_G:
            case IF_SVE_IK_4A_H:
            case IF_SVE_IK_4A_I:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                assert(isGeneralRegister(id.idReg4()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_IR_4A:
            {
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_Q);
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                assert(isGeneralRegister(id.idReg4()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_IU_4B:
            case IF_SVE_IU_4B_B:
            case IF_SVE_IU_4B_D:
            {
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_D);
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                assert(isVectorRegister(id.idReg4()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_IW_4A:
            case IF_SVE_IY_4A:
            {
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_Q);
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isGeneralRegisterOrZR(id.idReg4()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_IX_4A:
            {
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_D);
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isGeneralRegisterOrZR(id.idReg4()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_IZ_4A:
            case IF_SVE_IZ_4A_A:
            case IF_SVE_JA_4A:
            {
                assert(insOptsScalableWords(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isGeneralRegisterOrZR(id.idReg4()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_JD_4C:
            case IF_SVE_JD_4C_A:
            {
                assert(insOptsScalableDoubleWordsOrQuadword(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                assert(isGeneralRegister(id.idReg4()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_JF_4A:
            {
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_Q);
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                assert(isGeneralRegister(id.idReg4()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_IN_4A:
            case IF_SVE_IP_4A:
            case IF_SVE_IT_4A:
            case IF_SVE_JB_4A:
            case IF_SVE_JC_4A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                assert(isGeneralRegister(id.idReg4()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_JJ_4B:
            case IF_SVE_JJ_4B_C:
            case IF_SVE_JJ_4B_E:
            case IF_SVE_JK_4B:
            {
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_D);
                assert(isVectorRegister(id.idReg1()));
                assert(isPredicateRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                assert(isVectorRegister(id.idReg4()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_GP_3A:
            {
                imm = emitGetInsSC(id);
                assert(insOptsScalableAtLeastHalf(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(emitIsValidEncodedRotationImm90_or_270(imm));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_GT_4A:
            {
                imm = emitGetInsSC(id);
                assert(insOptsScalableAtLeastHalf(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isVectorRegister(id.idReg4()));
                assert(emitIsValidEncodedRotationImm0_to_270(imm));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_HI_3A:
            {
                assert(insOptsScalableAtLeastHalf(id.idInsOpt()));
                assert(isPredicateRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_HM_2A:
            {
                imm = emitGetInsSC(id);
                assert(insOptsScalableAtLeastHalf(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isScalableVectorSize(id.idOpSize()));
                assert(emitIsValidEncodedSmallFloatImm(unchecked((nuint)imm)));
                break;
            }

            case IF_SVE_HN_2A:
            {
                imm = emitGetInsSC(id);
                assert(insOptsScalableAtLeastHalf(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isValidUimm(imm, 3));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_HP_3A:
            {
                assert(insOptsScalableAtLeastHalf(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_HU_4B:
            {
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_H);
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isVectorRegister(id.idReg4()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_HV_4A:
            {
                assert(insOptsScalableAtLeastHalf(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isVectorRegister(id.idReg4()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_ID_2A:
            case IF_SVE_JG_2A:
            {
                assert(insOptsNone(id.idInsOpt()));
                assert(isScalableVectorSize(id.idOpSize()));
                assert(isPredicateRegister(id.idReg1()));
                assert(isGeneralRegisterOrZR(id.idReg2()));
                assert(isValidSimm(emitGetInsSC(id), 9));
                break;
            }

            case IF_SVE_IE_2A:
            case IF_SVE_JH_2A:
            {
                assert(insOptsNone(id.idInsOpt()));
                assert(isScalableVectorSize(id.idOpSize()));
                assert(isVectorRegister(id.idReg1()));
                assert(isGeneralRegisterOrZR(id.idReg2()));
                assert(isValidSimm(emitGetInsSC(id), 9));
                break;
            }

            case IF_SVE_GG_3A:
            {
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isValidUimm(emitGetInsSC(id), 2));
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_B);
                break;
            }

            case IF_SVE_GH_3B:
            case IF_SVE_GH_3B_B:
            {
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isValidUimm(emitGetInsSC(id), 2));
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_H);
                break;
            }

            case IF_SVE_GG_3B:
            {
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isValidUimm(emitGetInsSC(id), 3));
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_H);
                break;
            }

            case IF_SVE_GH_3A:
            {
                assert(insOptsScalable(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isValidUimm(emitGetInsSC(id), 1));
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_B);
                break;
            }

            case IF_SVE_HY_3A:
            case IF_SVE_HY_3A_A:
            {
                assert(insOptsScalable32bitExtends(id.idInsOpt()));
                assert(isLowPredicateRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_HY_3B:
            {
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_D);
                assert(isLowPredicateRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_IB_3A:
            {
                assert(insOptsNone(id.idInsOpt()));
                assert(isLowPredicateRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_HZ_2A_B:
            {
                assert(insOptsScalableWords(id.idInsOpt()));
                assert(isLowPredicateRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_IA_2A:
            {
                assert(insOptsNone(id.idInsOpt()));
                assert(isLowPredicateRegister(id.idReg1()));
                assert(isGeneralRegister(id.idReg2()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_HX_3A_B:
            {
                assert(insOptsScalableWords(id.idInsOpt()));
                assert(isScalableVectorSize(id.idOpSize()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isValidUimm(emitGetInsSC(id), 5));
                break;
            }

            case IF_SVE_HX_3A_E:
            {
                assert(insOptsScalableWords(id.idInsOpt()));
                assert(isScalableVectorSize(id.idOpSize()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                break;
            }

            case IF_SVE_IV_3A:
            {
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_D);
                assert(isScalableVectorSize(id.idOpSize()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                break;
            }

            case IF_SVE_JI_3A_A:
            {
                assert(insOptsScalableWords(id.idInsOpt()));
                assert(isScalableVectorSize(id.idOpSize()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                break;
            }

            case IF_SVE_JL_3A:
            {
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_D);
                assert(isScalableVectorSize(id.idOpSize()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isVectorRegister(id.idReg3()));
                assert(isValidUimm(emitGetInsSC(id) / 8, 5) && ((emitGetInsSC(id) % 8) == 0));
                break;
            }

            case IF_SVE_IC_3A:
            {
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_D);
                assert(isScalableVectorSize(id.idOpSize()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                break;
            }

            case IF_SVE_IC_3A_A:
            {
                assert(insOptsScalableWords(id.idInsOpt()));
                assert(isScalableVectorSize(id.idOpSize()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                break;
            }

            case IF_SVE_IC_3A_B:
            {
                assert(insOptsScalableAtLeastHalf(id.idInsOpt()));
                assert(isScalableVectorSize(id.idOpSize()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                break;
            }

            case IF_SVE_IC_3A_C:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isScalableVectorSize(id.idOpSize()));
                assert(isVectorRegister(id.idReg1()));
                assert(isLowPredicateRegister(id.idReg2()));
                assert(isGeneralRegister(id.idReg3()));
                break;
            }

            case IF_SVE_BI_2A:
            {
                assert(insOptsNone(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                break;
            }

            case IF_SVE_HH_2A:
            {
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_H);
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                break;
            }

            case IF_SVE_CB_2A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isGeneralRegisterOrZR(id.idReg2()));
                break;
            }

            case IF_SVE_CG_2A:
            {
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                break;
            }

            case IF_SVE_BJ_2A:
            case IF_SVE_HF_2A:
            {
                assert(insOptsScalableAtLeastHalf(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                break;
            }

            case IF_SVE_CH_2A:
            {
                assert(insOptsScalableWide(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                break;
            }

            case IF_SVE_BF_2A:
            case IF_SVE_FT_2A:
            case IF_SVE_FU_2A:
            {
                imm = emitGetInsSC(id);
                assert(isValidVectorShiftAmount(imm, optGetSveElemsize(id.idInsOpt()),
                    emitInsIsVectorRightShift(id.idIns())));
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isScalableVectorSize(id.idOpSize()));
                break;
            }

            case IF_SVE_BW_2A:
            {
                imm = emitGetInsSC(id);
                assert(insOptsScalable(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isValidBroadcastImm(imm, optGetSveElemsize(id.idInsOpt())));
                break;
            }

            case IF_SVE_BX_2A:
            {
                imm = emitGetInsSC(id);
                assert(insOptsScalableStandard(id.idInsOpt()));
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isScalableVectorSize(id.idOpSize()));

                switch (id.idInsOpt())
                {
                    case INS_OPTS_SCALABLE_B:
                    {
                        assert(isValidUimm(imm, 4));
                        break;
                    }

                    case INS_OPTS_SCALABLE_H:
                    {
                        assert(isValidUimm(imm, 3));
                        break;
                    }

                    case INS_OPTS_SCALABLE_S:
                    {
                        assert(isValidUimm(imm, 2));
                        break;
                    }

                    case INS_OPTS_SCALABLE_D:
                    {
                        assert(isValidUimm(imm, 1));
                        break;
                    }

                    default:
                    {
                        break;
                    }
                }
                break;
            }

            case IF_SVE_BY_2A:
            {
                imm = emitGetInsSC(id);
                assert(id.idInsOpt() == INS_OPTS_SCALABLE_B);
                assert(isVectorRegister(id.idReg1()));
                assert(isVectorRegister(id.idReg2()));
                assert(isScalableVectorSize(id.idOpSize()));
                assert(isValidUimm(imm, 4));
                break;
            }

            default:
            {
                jitprintf($"unexpected format {emitIfName(id.idInsFmt())}\n");
                assert(false, "!\"Unexpected format\"");
                break;
            }
        }
    }
}
#endif
