// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public unsafe partial class Emitter
{
    private void emitInsSve_I(instruction ins, emitAttr attr, nint imm)
    {
        var fmt = IF_NONE;
        if (ins == INS_sve_setffr)
        {
            fmt = IF_SVE_DQ_0A;
            attr = EA_PTRSIZE;
            imm = 0;
        }
        else
        {
            unreached();
        }

        var id = emitNewInstrSC(attr, imm);
        id.idIns(ins);
        id.idInsFmt(fmt);

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitInsSve_R(instruction ins, emitAttr attr, regNumber reg, insOpts opt = INS_OPTS_NONE)
    {
        var fmt = IF_NONE;
        switch (ins)
        {
            case INS_sve_aesmc:
            case INS_sve_aesimc:
            {
                opt = INS_OPTS_SCALABLE_B;
                assert(isVectorRegister(reg));
                assert(isScalableVectorSize(attr));
                fmt = IF_SVE_GL_1A;
                break;
            }

            case INS_sve_rdffr:
            {
                opt = INS_OPTS_SCALABLE_B;
                assert(isPredicateRegister(reg));
                fmt = IF_SVE_DH_1A;
                break;
            }

            case INS_sve_pfalse:
            {
                opt = INS_OPTS_SCALABLE_B;
                assert(isPredicateRegister(reg));
                fmt = IF_SVE_DJ_1A;
                break;
            }

            case INS_sve_wrffr:
            {
                opt = INS_OPTS_SCALABLE_B;
                assert(isPredicateRegister(reg));
                fmt = IF_SVE_DR_1A;
                break;
            }

            case INS_sve_ptrue:
            {
                assert(insOptsScalableStandard(opt));
                assert(isHighPredicateRegister(reg));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_DZ_1A;
                break;
            }

            case INS_sve_fmov:
            {
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_EB_1B;

                // FMOV represents DUP here; MOV is its preferred alias.
                ins = INS_sve_mov;
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        var id = emitNewInstrSmall(attr);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(opt);
        id.idReg1(reg);

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitInsSve_R_I(instruction ins, emitAttr attr, regNumber reg, nint imm,
        insOpts opt = INS_OPTS_NONE, insScalableOpts sopt = insScalableOpts.INS_SCALABLE_OPTS_NONE)
    {
        var size = EA_SIZE(attr);
        var canEncode = false;
        var signedImm = false;
        var hasShift = false;
        var fmt = IF_NONE;
        bitMaskImm bmi;

        switch (ins)
        {
            case INS_sve_rdvl:
            {
                assert(insOptsNone(opt));
                assert(size == EA_8BYTE);
                assert(isGeneralRegister(reg));
                assert(isValidSimm(imm, 6));
                fmt = IF_SVE_BC_1A;
                canEncode = true;
                break;
            }

            case INS_sve_smax:
            case INS_sve_smin:
            {
                signedImm = true;
                goto case INS_sve_umax;
            }

            case INS_sve_umax:
            case INS_sve_umin:
            {
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                if (signedImm)
                {
                    assert(isValidSimm(imm, 8));
                }
                else
                {
                    assert(isValidUimm(imm, 8));
                }

                fmt = IF_SVE_ED_1A;
                canEncode = true;
                break;
            }

            case INS_sve_mul:
            {
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg));
                assert(isValidSimm(imm, 8));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                fmt = IF_SVE_EE_1A;
                canEncode = true;
                break;
            }

            case INS_sve_mov:
            {
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg));
                if (sopt == insScalableOpts.INS_SCALABLE_OPTS_IMM_BITMASK)
                {
                    bmi.immNRS = 0;
                    canEncode = canEncodeBitMaskImm(imm, optGetSveElemsize(opt), &bmi);
                    if (!useMovDisasmForBitMask(imm))
                    {
                        ins = INS_sve_dupm;
                    }

                    imm = (nint)bmi.immNRS;
                    assert(isValidImmNRS(unchecked((nuint)imm), optGetSveElemsize(opt)));
                    fmt = IF_SVE_BT_1A;
                }
                else
                {
                    assert(insScalableOptsNone(sopt));
                    assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                    if (!isValidSimm(imm, 8))
                    {
                        assert(isValidSimm(imm / 256, 8) && ((imm % 256) == 0));
                        assert(insOptsScalableAtLeastHalf(opt));
                        hasShift = true;
                        imm >>= 8;
                    }

                    fmt = IF_SVE_EB_1A;
                    canEncode = true;
                }
                break;
            }

            case INS_sve_dup:
            {
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                if (!isValidSimm(imm, 8))
                {
                    assert(isValidSimm(imm / 256, 8) && ((imm % 256) == 0));
                    assert(insOptsScalableAtLeastHalf(opt));
                    hasShift = true;
                    imm >>= 8;
                }

                fmt = IF_SVE_EB_1A;
                canEncode = true;

                // MOV is always the preferred alias of DUP.
                ins = INS_sve_mov;
                break;
            }

            case INS_sve_add:
            case INS_sve_sub:
            case INS_sve_sqadd:
            case INS_sve_sqsub:
            case INS_sve_uqadd:
            case INS_sve_uqsub:
            case INS_sve_subr:
            {
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                if (!isValidUimm(imm, 8))
                {
                    assert(isValidUimm(imm / 256, 8) && ((imm % 256) == 0));
                    assert(insOptsScalableAtLeastHalf(opt));
                    hasShift = true;
                    imm >>= 8;
                }

                fmt = IF_SVE_EC_1A;
                canEncode = true;
                break;
            }

            case INS_sve_and:
            case INS_sve_orr:
            case INS_sve_eor:
            {
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg));
                bmi.immNRS = 0;
                canEncode = canEncodeBitMaskImm(imm, optGetSveElemsize(opt), &bmi);
                imm = (nint)bmi.immNRS;
                assert(isValidImmNRS(unchecked((nuint)imm), optGetSveElemsize(opt)));
                fmt = IF_SVE_BS_1A;
                break;
            }

            case INS_sve_bic:
            {
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg));
                ins = INS_sve_and;
                imm = unchecked(-imm - 1);
                bmi.immNRS = 0;
                canEncode = canEncodeBitMaskImm(imm, optGetSveElemsize(opt), &bmi);
                imm = (nint)bmi.immNRS;
                assert(isValidImmNRS(unchecked((nuint)imm), optGetSveElemsize(opt)));
                fmt = IF_SVE_BS_1A;
                break;
            }

            case INS_sve_eon:
            {
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg));
                ins = INS_sve_eor;
                imm = unchecked(-imm - 1);
                bmi.immNRS = 0;
                canEncode = canEncodeBitMaskImm(imm, optGetSveElemsize(opt), &bmi);
                imm = (nint)bmi.immNRS;
                assert(isValidImmNRS(unchecked((nuint)imm), optGetSveElemsize(opt)));
                fmt = IF_SVE_BS_1A;
                break;
            }

            case INS_sve_orn:
            {
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg));
                ins = INS_sve_orr;
                imm = unchecked(-imm - 1);
                bmi.immNRS = 0;
                canEncode = canEncodeBitMaskImm(imm, optGetSveElemsize(opt), &bmi);
                imm = (nint)bmi.immNRS;
                assert(isValidImmNRS(unchecked((nuint)imm), optGetSveElemsize(opt)));
                fmt = IF_SVE_BS_1A;
                break;
            }

            case INS_sve_dupm:
            {
                assert(insOptsScalableStandard(opt));
                assert(isVectorRegister(reg));
                bmi.immNRS = 0;
                canEncode = canEncodeBitMaskImm(imm, optGetSveElemsize(opt), &bmi);
                fmt = IF_SVE_BT_1A;
                if (useMovDisasmForBitMask(imm))
                {
                    ins = INS_sve_mov;
                }

                imm = (nint)bmi.immNRS;
                assert(isValidImmNRS(unchecked((nuint)imm), optGetSveElemsize(opt)));
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        assert(canEncode);

        // Shifted immediates require the normal descriptor's dedicated shift bit.
        // Unshifted values retain native small-constant descriptor selection.
        var id = !hasShift ? emitNewInstrSC(attr, imm) : emitNewInstrCns(attr, imm);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(opt);
        id.idReg1(reg);
        id.idHasShift(hasShift);

        dispIns(id);
        appendToCurIG(id);
    }

    private void emitInsSve_R_F(instruction ins, emitAttr attr, regNumber reg, double immDbl, insOpts opt)
    {
        nint imm = 0;
        var canEncode = false;
        var fmt = IF_NONE;
        switch (ins)
        {
            case INS_sve_fmov:
            case INS_sve_fdup:
            {
                assert(insOptsScalableAtLeastHalf(opt));
                assert(isVectorRegister(reg));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                floatImm8 fpi;
                fpi.immFPIVal = 0;
                canEncode = canEncodeFloatImm8(immDbl, &fpi);
                imm = (nint)fpi.immFPIVal;
                fmt = IF_SVE_EA_1A;

                // FMOV is always the preferred alias of FDUP.
                ins = INS_sve_fmov;
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        assert(canEncode);

        var id = emitNewInstrSC(attr, imm);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(opt);
        id.idReg1(reg);

        dispIns(id);
        appendToCurIG(id);
    }

    private void emitInsSve_R_R_F(instruction ins, emitAttr attr, regNumber reg1, regNumber reg2,
        double immDbl, insOpts opt)
    {
        var size = EA_SIZE(attr);
        nint imm = 0;
        var fmt = IF_NONE;

        switch (ins)
        {
            case INS_sve_fmul:
            case INS_sve_fmaxnm:
            case INS_sve_fadd:
            case INS_sve_fmax:
            case INS_sve_fminnm:
            case INS_sve_fsub:
            case INS_sve_fmin:
            case INS_sve_fsubr:
            {
                assert(insOptsScalableAtLeastHalf(opt));
                assert(isVectorRegister(reg1));
                assert(isLowPredicateRegister(reg2));
                assert(isScalableVectorSize(size));
                imm = emitEncodeSmallFloatImm(immDbl, ins);
                fmt = IF_SVE_HM_2A;
                break;
            }

            case INS_sve_fmov:
            case INS_sve_fcpy:
            {
                assert(insOptsScalableAtLeastHalf(opt));
                assert(isVectorRegister(reg1));
                assert(isPredicateRegister(reg2));
                assert(isValidVectorElemsize(optGetSveElemsize(opt)));
                floatImm8 fpi;
                fpi.immFPIVal = 0;
                _ = canEncodeFloatImm8(immDbl, &fpi);
                imm = (nint)fpi.immFPIVal;
                fmt = IF_SVE_BU_2A;

                // FMOV is an alias for FCPY, and is always the preferred disassembly.
                ins = INS_sve_fmov;
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        assert(fmt != IF_NONE);
        var id = emitNewInstrSC(attr, imm);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(opt);
        id.idReg1(reg1);
        id.idReg2(reg2);
        dispIns(id);
        appendToCurIG(id);
    }

    private static nint emitEncodeSmallFloatImm(double immDbl, instruction ins)
    {
#if DEBUG
        switch (ins)
        {
            case INS_sve_fadd:
            case INS_sve_fsub:
            case INS_sve_fsubr:
            {
                assert((immDbl == 0.5) || (immDbl == 1.0));
                break;
            }

            case INS_sve_fmax:
            case INS_sve_fmaxnm:
            case INS_sve_fmin:
            case INS_sve_fminnm:
            {
                assert((immDbl == 0.0) || (immDbl == 1.0));
                break;
            }

            case INS_sve_fmul:
            {
                assert((immDbl == 0.5) || (immDbl == 2.0));
                break;
            }

            default:
            {
                assert(false, "!\"Invalid instruction\"");
                break;
            }
        }
#endif

        // Each supported instruction uses one immediate bit to distinguish its two legal constants.
        if (immDbl < 1.0)
        {
            return 0;
        }

        return 1;
    }
}
#endif
