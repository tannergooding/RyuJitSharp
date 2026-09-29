// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_R_R_R_I_I(instruction ins, emitAttr attr, regNumber reg1,
        regNumber reg2, regNumber reg3, nint imm1, nint imm2, insOpts opt)
    {
        var size = EA_SIZE(attr);
        var dstsize = size;
        switch (ins)
        {
            case INS_ins:
            {
                emitIns_Mov(INS_mov, dstsize, reg1, reg2, canSkip: true);
                emitIns_R_R_I_I(ins, attr, reg1, reg3, imm1, imm2, opt);
                return;
            }

            default:
            {
                emitInsSve_R_R_R_I_I(ins, attr, reg1, reg2, reg3, imm1, imm2, opt);
                return;
            }
        }
    }

    public void emitIns_R_R_I_I(instruction ins, emitAttr attr, regNumber reg1,
        regNumber reg2, nint imm1, nint imm2, insOpts opt = INS_OPTS_NONE)
    {
        var size = EA_SIZE(attr);
        emitAttr elemsize;
        insFormat fmt;
        nuint immOut;
        switch (ins)
        {
            case INS_bfm:
            case INS_sbfm:
            case INS_ubfm:
            {
                assert(isGeneralRegister(reg1));
                assert((ins == INS_bfm) ? isGeneralRegisterOrZR(reg2) : isGeneralRegister(reg2));
                assert(isValidImmShift(imm1, size));
                assert(isValidImmShift(imm2, size));
                assert(insOptsNone(opt));

                bitMaskImm bmi = default;
                bmi.immN = size == EA_8BYTE ? 1u : 0u;
                bmi.immR = unchecked((uint)imm1);
                bmi.immS = unchecked((uint)imm2);
                immOut = bmi.immNRS;
                fmt = IF_DI_2D;
                break;
            }

            case INS_bfi:
            case INS_sbfiz:
            case INS_ubfiz:
            {
                assert(isGeneralRegister(reg1));
                assert(isGeneralRegister(reg2));
                var lsb = unchecked((nint)getBitWidth(size) - imm1);
                var width = unchecked(imm2 - 1);
                assert(isValidImmShift(lsb, size));
                assert(isValidImmShift(width, size));
                assert(insOptsNone(opt));

                bitMaskImm bmi = default;
                bmi.immN = size == EA_8BYTE ? 1u : 0u;
                bmi.immR = unchecked((uint)lsb);
                bmi.immS = unchecked((uint)width);
                immOut = bmi.immNRS;
                fmt = IF_DI_2D;
                break;
            }

            case INS_bfxil:
            case INS_sbfx:
            case INS_ubfx:
            {
                assert(isGeneralRegister(reg1));
                assert(isGeneralRegister(reg2));
                var lsb = imm1;
                var width = unchecked(imm2 + imm1 - 1);
                assert(isValidImmShift(lsb, size));
                assert(isValidImmShift(width, size));
                assert(insOptsNone(opt));

                bitMaskImm bmi = default;
                bmi.immN = size == EA_8BYTE ? 1u : 0u;
                bmi.immR = unchecked((uint)imm1);
                bmi.immS = unchecked((uint)(imm2 + imm1 - 1));
                immOut = bmi.immNRS;
                fmt = IF_DI_2D;
                break;
            }

            case INS_mov:
            case INS_ins:
            {
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                elemsize = size;
                assert(isValidVectorElemsize(elemsize));
                assert(isValidVectorIndex(EA_16BYTE, elemsize, imm1));
                assert(isValidVectorIndex(EA_16BYTE, elemsize, imm2));
                assert(insOptsNone(opt));
                immOut = unchecked((nuint)((imm1 << 4) + imm2));
                fmt = IF_DV_2F;
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
                elemsize = size;
                assert(isValidVectorElemsize(elemsize));
                assert(isValidVectorIndex(EA_16BYTE, elemsize, imm1));
                var registerListSize = insGetRegisterListSize(ins);
                assert(((uint)elemsize * registerListSize) == unchecked((uint)imm2));
                assert(insOptsPostIndex(opt));

                // Single-structure access post-indexed by an immediate.
                reg2 = encodingSPtoZR(reg2);
                immOut = unchecked((nuint)imm1);
                fmt = IF_LS_2G;
                break;
            }

            default:
            {
                unreached();
                return;
            }
        }
#pragma warning disable CA1508 // Preserve the native post-dispatch format invariant.
        assert(fmt != IF_NONE);
#pragma warning restore CA1508

        var id = emitNewInstrSC(attr, unchecked((nint)immOut));
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idInsOpt(opt);
        id.idReg1(reg1);
        id.idReg2(reg2);

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitIns_R_R_R_R(instruction ins, emitAttr attr, regNumber reg1,
        regNumber reg2, regNumber reg3, regNumber reg4, insOpts opt = INS_OPTS_NONE,
        insScalableOpts sopt = insScalableOpts.INS_SCALABLE_OPTS_NONE)
    {
        var size = EA_SIZE(attr);
        var dstsize = size;
        insFormat fmt;
        switch (ins)
        {
            case INS_sqdmlal:
            case INS_sqdmlsl:
            {
                dstsize = EA_16BYTE;
                goto case INS_sqrdmlah;
            }

            case INS_sqrdmlah:
            case INS_sqrdmlsh:
            {
                if (!insOptsAnyArrangement(opt))
                {
                    // Scalar variants return the whole Vector64, not just the scalar lane.
                    dstsize = EA_8BYTE;
                }
                emitIns_Mov(INS_mov, dstsize, reg1, reg2, canSkip: true);
                emitIns_R_R_R(ins, attr, reg1, reg3, reg4, opt);
                return;
            }

            case INS_addhn2:
            case INS_raddhn2:
            case INS_rsubhn2:
            case INS_sabal:
            case INS_sabal2:
            case INS_sha1c:
            case INS_sha1m:
            case INS_sha1p:
            case INS_sha1su0:
            case INS_sha256h:
            case INS_sha256h2:
            case INS_sha256su1:
            case INS_smlal:
            case INS_smlal2:
            case INS_smlsl:
            case INS_smlsl2:
            case INS_sqdmlal2:
            case INS_sqdmlsl2:
            case INS_subhn2:
            case INS_uabal:
            case INS_uabal2:
            case INS_umlal:
            case INS_umlal2:
            case INS_umlsl:
            case INS_umlsl2:
            {
                dstsize = EA_16BYTE;
                goto case INS_fmla;
            }

            case INS_fmla:
            case INS_fmls:
            case INS_mla:
            case INS_mls:
            case INS_saba:
            case INS_sdot:
            case INS_tbx:
            case INS_tbx_2regs:
            case INS_tbx_3regs:
            case INS_tbx_4regs:
            case INS_uaba:
            case INS_udot:
            {
                emitIns_Mov(INS_mov, dstsize, reg1, reg2, canSkip: true);
                emitIns_R_R_R(ins, attr, reg1, reg3, reg4, opt, sopt);
                return;
            }

            case INS_madd:
            case INS_msub:
            {
                assert(isValidGeneralDatasize(size));
                assert(isGeneralRegister(reg1));
                assert(isGeneralRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isGeneralRegister(reg4));
                assert(insScalableOptsNone(sopt));
                fmt = IF_DR_4A;
                break;
            }

            case INS_smaddl:
            case INS_smsubl:
            case INS_umaddl:
            case INS_umsubl:
            {
                assert(size == EA_8BYTE);
                assert(isGeneralRegister(reg1));
                assert(isGeneralRegister(reg2));
                assert(isGeneralRegister(reg3));
                assert(isGeneralRegister(reg4));
                assert(insScalableOptsNone(sopt));
                fmt = IF_DR_4A;
                break;
            }

            case INS_fmadd:
            case INS_fmsub:
            case INS_fnmadd:
            case INS_fnmsub:
            {
                assert(isValidScalarDatasize(size));
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isVectorRegister(reg4));
                assert(insScalableOptsNone(sopt));
                fmt = IF_DV_4A;
                break;
            }

            case INS_eor3:
            case INS_bcax:
            {
                assert(size == EA_16BYTE);
                assert(opt == INS_OPTS_16B);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isVectorRegister(reg4));
                assert(insScalableOptsNone(sopt));
                fmt = IF_DV_4B;
                break;
            }

            case INS_sm3ss1:
            {
                assert(size == EA_16BYTE);
                assert(opt == INS_OPTS_4S);
                assert(isVectorRegister(reg1));
                assert(isVectorRegister(reg2));
                assert(isVectorRegister(reg3));
                assert(isVectorRegister(reg4));
                assert(insScalableOptsNone(sopt));
                fmt = IF_DV_4B;
                break;
            }

            case INS_invalid:
            {
                fmt = IF_NONE;
                break;
            }

            default:
            {
                emitInsSve_R_R_R_R(ins, attr, reg1, reg2, reg3, reg4, opt, sopt);
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
        id.idReg4(reg4);

        dispIns(id);
        appendToCurIG(id);
    }

    public void emitIns_R_R_R_R_I(instruction ins, emitAttr attr, regNumber reg1,
        regNumber reg2, regNumber reg3, regNumber reg4, nint imm, insOpts opt = INS_OPTS_NONE)
    {
        var size = EA_SIZE(attr);
        var dstsize = size;
        switch (ins)
        {
            case INS_sqdmlal:
            case INS_sqdmlsl:
            {
                dstsize = EA_16BYTE;
                goto case INS_sqrdmlah;
            }

            case INS_sqrdmlah:
            case INS_sqrdmlsh:
            {
                if (!insOptsAnyArrangement(opt))
                {
                    // Scalar variants return the whole Vector64, not just the scalar lane.
                    dstsize = EA_8BYTE;
                }
                emitIns_Mov(INS_mov, dstsize, reg1, reg2, canSkip: true);
                emitIns_R_R_R_I(ins, attr, reg1, reg3, reg4, imm, opt);
                return;
            }

            case INS_smlal:
            case INS_smlal2:
            case INS_smlsl:
            case INS_smlsl2:
            case INS_sqdmlal2:
            case INS_sqdmlsl2:
            case INS_umlal:
            case INS_umlal2:
            case INS_umlsl:
            case INS_umlsl2:
            {
                dstsize = EA_16BYTE;
                goto case INS_fmla;
            }

            case INS_fmla:
            case INS_fmls:
            case INS_mla:
            case INS_mls:
            case INS_sdot:
            case INS_udot:
            {
                emitIns_Mov(INS_mov, dstsize, reg1, reg2, canSkip: true);
                emitIns_R_R_R_I(ins, attr, reg1, reg3, reg4, imm, opt);
                return;
            }

            default:
            {
                emitInsSve_R_R_R_R_I(ins, attr, reg1, reg2, reg3, reg4, imm, opt);
                return;
            }
        }
    }

    private static void emitInsSve_R_R_R_I_I(instruction ins, emitAttr attr, regNumber reg1,
        regNumber reg2, regNumber reg3, nint imm1, nint imm2, insOpts opt)
        => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 SVE three-register/two-immediate recording is not ported.");

    private static void emitInsSve_R_R_R_R(instruction ins, emitAttr attr, regNumber reg1,
        regNumber reg2, regNumber reg3, regNumber reg4, insOpts opt, insScalableOpts sopt)
        => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 SVE four-register recording is not ported.");

    private static void emitInsSve_R_R_R_R_I(instruction ins, emitAttr attr, regNumber reg1,
        regNumber reg2, regNumber reg3, regNumber reg4, nint imm, insOpts opt)
        => throw new FatalJitException(CORJIT_SKIPPED, "ARM64 SVE four-register/immediate recording is not ported.");
}
#endif
