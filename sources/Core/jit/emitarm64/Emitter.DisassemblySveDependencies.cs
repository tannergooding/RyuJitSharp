// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.insSvePattern;

namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe void emitDispInsSveHelp(instrDesc id)
    {
        var ins = id.idIns();
        var fmt = id.idInsFmt();
        var size = id.idOpSize();

        nint imm;
        bitMaskImm bmi = new();

        switch (fmt)
        {
            //  <Zdn>.<T>, <Pg>/M, <Zdn>.<T>, <Zm>.<T>
            case IF_SVE_AA_3A: // ........xx...... ...gggmmmmmddddd
            case IF_SVE_AC_3A: // ........xx...... ...gggmmmmmddddd -- SVE integer divide vectors (predicated)
            case IF_SVE_GR_3A: // ........xx...... ...gggmmmmmddddd -- SVE2 floating-point pairwise operations
            case IF_SVE_HL_3A: // ........xx...... ...gggmmmmmddddd -- SVE floating-point arithmetic (predicated)
            // <Zdn>.D, <Pg>/M, <Zdn>.D, <Zm>.D
            case IF_SVE_AB_3B: // ................ ...gggmmmmmddddd -- SVE integer add/subtract vectors (predicated)
            // <Zdn>.H, <Pg>/M, <Zdn>.H, <Zm>.H
            case IF_SVE_HL_3B: // ................ ...gggmmmmmddddd -- SVE floating-point arithmetic (predicated)
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispLowPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true); // ggg
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveReg(id.idReg3(), id.idInsOpt(), false); // mmmmm
                break;
            }

            // <Zd>.<T>, <Pg>/<ZM>, <Zn>.<T>
            case IF_SVE_AH_3A: // ........xx.....M ...gggnnnnnddddd -- SVE constructive prefix (predicated)
            {
                var ptype = id.idPredicateReg2Merge()
                    ? PredicateType.PREDICATE_MERGE
                    : PredicateType.PREDICATE_ZERO;
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // nnnnn
                emitDispLowPredicateReg(id.idReg2(), ptype, id.idInsOpt(), true); // ggg
                emitDispSveReg(id.idReg3(), id.idInsOpt(), false); // ddddd
                break;
            }

            // <Zdn>.<T>, <Pg>/M, <Zdn>.<T>, #<const>
            case IF_SVE_AM_2A: // ........xx...... ...gggxxiiiddddd -- SVE bitwise shift by immediate (predicated)
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispLowPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true); // ggg
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispImm(emitGetInsSC(id), false); // iiii
                break;
            }

            // <Zdn>.<T>, <Pg>/M, <Zdn>.<T>, <Zm>.D
            case IF_SVE_AO_3A: // ........xx...... ...gggmmmmmddddd -- SVE bitwise shift by wide elements (predicated)
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispLowPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true); // ggg
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveReg(id.idReg3(), INS_OPTS_SCALABLE_D, false); // mmmmm
                break;
            }

            // <Zda>.<T>, <Pg>/M, <Zn>.<T>, <Zm>.<T>
            // <Zdn>.<T>, <Pg>/M, <Zm>.<T>, <Za>.<T>
            case IF_SVE_AR_4A: // ........xx.mmmmm ...gggnnnnnddddd -- SVE integer multiply-accumulate writing addend
            // (predicated)
            case IF_SVE_AS_4A: // ........xx.mmmmm ...gggaaaaaddddd -- SVE integer multiply-add writing multiplicand
            // (predicated)
            case IF_SVE_HU_4A: // ........xx.mmmmm ...gggnnnnnddddd -- SVE floating-point multiply-accumulate writing addend
            // <Zd>.<T>, <Pg>/Z, <Zn>.<T>, <Zm>.<T>
            case IF_SVE_GI_4A: // ........xx.mmmmm ...gggnnnnnddddd -- SVE2 histogram generation (vector)
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispLowPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true); // ggg
                emitDispSveReg(id.idReg3(), id.idInsOpt(), true);
                emitDispSveReg(id.idReg4(), id.idInsOpt(), false);
                break;
            }

            // <Zd>.<T>, <Zn>.<T>, <Zm>.<T>
            case IF_SVE_AT_3A: // ........xx.mmmmm ......nnnnnddddd
            // <Zda>.<T>, <Zn>.<T>, <Zm>.<T>
            case IF_SVE_EM_3A: // ........xx.mmmmm ......nnnnnddddd -- SVE2 saturating multiply-add high
            case IF_SVE_FW_3A: // ........xx.mmmmm ......nnnnnddddd -- SVE2 integer absolute difference and accumulate
            // <Zd>.Q, <Zn>.Q, <Zm>.Q
            case IF_SVE_BR_3B: // ...........mmmmm ......nnnnnddddd -- SVE permute vector segments
            // <Zda>.D, <Zn>.D, <Zm>.D
            case IF_SVE_HD_3A_A: // ...........mmmmm ......nnnnnddddd -- SVE floating point matrix multiply accumulate
            // <Zd>.D, <Zn>.D, <Zm>.D
            case IF_SVE_AT_3B: // ...........mmmmm ......nnnnnddddd -- SVE integer add/subtract vectors (unpredicated)
            // <Zd>.B, <Zn>.B, <Zm>.B
            case IF_SVE_GF_3A: // ........xx.mmmmm ......nnnnnddddd -- SVE2 histogram generation (segment)
            case IF_SVE_BD_3B: // ...........mmmmm ......nnnnnddddd -- SVE2 integer multiply vectors (unpredicated)
            // <Zd>.D, <Zn>.D, <Zm>.D
            // <Zd>.S, <Zn>.S, <Zm>.S
            case IF_SVE_GJ_3A: // ...........mmmmm ......nnnnnddddd -- SVE2 crypto constructive binary operations
            // <Zd>.H, <Zn>.H, <Zm>.H
            case IF_SVE_GW_3B: // ...........mmmmm ......nnnnnddddd -- SVE FP clamp
            case IF_SVE_HK_3B: // ...........mmmmm ......nnnnnddddd -- SVE floating-point arithmetic (unpredicated)
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveReg(id.idReg2(), id.idInsOpt(), true); // nnnnn/mmmmm
                emitDispSveReg(id.idReg3(), id.idInsOpt(), false); // mmmmm/aaaaa
                break;
            }

            // <Zd>.D, <Zn>.D, <Zm>.D
            case IF_SVE_AU_3A: // ...........mmmmm ......nnnnnddddd -- SVE bitwise logical operations (unpredicated)
            {
                emitDispSveReg(id.idReg1(), INS_OPTS_SCALABLE_D, true); // ddddd
                if (id.idIns() == INS_sve_mov)
                {
                    emitDispSveReg(id.idReg2(), INS_OPTS_SCALABLE_D, false); // nnnnn/mmmmm
                }
                else
                {
                    emitDispSveReg(id.idReg2(), INS_OPTS_SCALABLE_D, true); // nnnnn/mmmmm
                    emitDispSveReg(id.idReg3(), INS_OPTS_SCALABLE_D, false); // mmmmm/aaaaa
                }
                break;
            }

            // <Zda>.D, <Zn>.D, <Zm>.D
            case IF_SVE_EW_3A: // ...........mmmmm ......nnnnnddddd -- SVE2 multiply-add (checked pointer)
            // <Zdn>.D, <Zm>.D, <Za>.D
            case IF_SVE_EW_3B: // ...........mmmmm ......aaaaaddddd -- SVE2 multiply-add (checked pointer)
            {
                emitDispSveReg(id.idReg1(), INS_OPTS_SCALABLE_D, true); // ddddd
                emitDispSveReg(id.idReg2(), INS_OPTS_SCALABLE_D, true); // nnnnn
                emitDispSveReg(id.idReg3(), INS_OPTS_SCALABLE_D, false); // mmmmm
                break;
            }

            // <Zdn>.D, <Zdn>.D, <Zm>.D, <Zk>.D
            case IF_SVE_AV_3A: // ...........mmmmm ......kkkkkddddd -- SVE2 bitwise ternary operations
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveReg(id.idReg2(), id.idInsOpt(), true); // mmmmm
                emitDispSveReg(id.idReg3(), id.idInsOpt(), false); // kkkkk
                break;
            }

            // <Zd>.<T>, #<imm1>, #<imm2>
            case IF_SVE_AX_1A: // ........xx.iiiii ......iiiiiddddd -- SVE index generation (immediate start, immediate
            // increment)
            {
                nint imm1;
                nint imm2;
                insSveDecodeTwoSimm5(emitGetInsSC(id), &imm1, &imm2);
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispImm(imm1, true); // iiiii
                emitDispImm(imm2, false); // iiiii
                break;
            }

            // <Zd>.<T>, #<imm>, <R><m>
            case IF_SVE_AY_2A: // ........xx.mmmmm ......iiiiiddddd -- SVE index generation (immediate start, register
            // increment)
            {
                var intRegSize = (id.idInsOpt() == INS_OPTS_SCALABLE_D) ? EA_8BYTE : EA_4BYTE;
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispImm(emitGetInsSC(id), true); // iiiii
                emitDispReg(id.idReg2(), intRegSize, false); // mmmmm
                break;
            }

            // <Zd>.<T>, <R><n>, #<imm>
            case IF_SVE_AZ_2A: // ........xx.iiiii ......nnnnnddddd -- SVE index generation (register start, immediate
            // increment)
            {
                var intRegSize = (id.idInsOpt() == INS_OPTS_SCALABLE_D) ? EA_8BYTE : EA_4BYTE;
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispReg(id.idReg2(), intRegSize, true); // mmmmm
                emitDispImm(emitGetInsSC(id), false); // iiiii
                break;
            }

            // <Zda>.H, <Zn>.B, <Zm>.B
            case IF_SVE_GN_3A:   // ...........mmmmm ......nnnnnddddd -- SVE2 FP8 multiply-add long
            case IF_SVE_HA_3A_E: // ...........mmmmm ......nnnnnddddd -- SVE BFloat16 floating-point dot product
            {
                emitDispSveReg(id.idReg1(), INS_OPTS_SCALABLE_H, true); // ddddd
                emitDispSveReg(id.idReg2(), id.idInsOpt(), true); // nnnnn
                emitDispSveReg(id.idReg3(), id.idInsOpt(), false); // mmmmm
                break;
            }

            // <Zd>.<T>, <Zn>.<T>, <Zm>.<T>
            // <Zd>.<T>, {<Zn>.<T>}, <Zm>.<T>
            case IF_SVE_BZ_3A: // ........xx.mmmmm ......nnnnnddddd -- SVE table lookup (three sources)
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                if (id.idIns() == INS_sve_tbl)
                {
                    emitDispSveConsecutiveRegList(id.idReg2(), 1, id.idInsOpt(), true); // nnnnn
                }
                else
                {
                    assert(id.idIns() == INS_sve_tbx);
                    emitDispSveReg(id.idReg2(), id.idInsOpt(), true); // nnnnn
                }
                emitDispSveReg(id.idReg3(), id.idInsOpt(), false); // mmmmm
                break;
            }

            // <Zd>.<T>, <Zn>.<T>, <Zm>.<T>
            // <Zd>.<T>, {<Zn>.<T>}, <Zm>.<T>
            case IF_SVE_EX_3A: // ........xx.mmmmm ......nnnnnddddd -- SVE permute vector elements (quadwords)
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                if (id.idIns() == INS_sve_tblq)
                {
                    emitDispSveConsecutiveRegList(id.idReg2(), 1, id.idInsOpt(), true); // nnnnn
                }
                else
                {
                    emitDispSveReg(id.idReg2(), id.idInsOpt(), true); // nnnnn
                }
                emitDispSveReg(id.idReg3(), id.idInsOpt(), false); // mmmmm
                break;
            }

            // <Zd>.<T>, {<Zn1>.<T>, <Zn2>.<T>}, <Zm>.<T>
            case IF_SVE_BZ_3A_A: // ........xx.mmmmm ......nnnnnddddd -- SVE table lookup (three sources)
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveConsecutiveRegList(id.idReg2(), 2, id.idInsOpt(), true); // nnnnn
                emitDispSveReg(id.idReg3(), id.idInsOpt(), false); // mmmmm
                break;
            }

            // <Zd>.<T>, <R><n>, <R><m>
            case IF_SVE_BA_3A: // ........xx.mmmmm ......nnnnnddddd -- SVE index generation (register start, register
            {
                // increment)
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispReg(id.idReg2(), size, true); // nnnnn
                emitDispReg(id.idReg3(), size, false); // mmmmm
                break;
            }

            // <Xd>{, <pattern>{, MUL #<imm>}}
            case IF_SVE_BL_1A: // ............iiii ......pppppddddd -- SVE element count
            // <Xdn>{, <pattern>{, MUL #<imm>}}
            case IF_SVE_BM_1A: // ............iiii ......pppppddddd -- SVE inc/dec register by element count
            {
                imm = emitGetInsSC(id);
                emitDispReg(id.idReg1(), size, true); // ddddd
                emitDispSvePattern(id.idSvePattern(), (imm > 1)); // ppppp
                if (imm > 1)
                {
                    jitprintf("mul ");
                    emitDispImm(imm, false, false); // iiii
                }
                break;
            }

            // <Zdn>.D{, <pattern>{, MUL #<imm>}}
            // <Zdn>.H{, <pattern>{, MUL #<imm>}}
            // <Zdn>.S{, <pattern>{, MUL #<imm>}}
            case IF_SVE_BN_1A: // ............iiii ......pppppddddd -- SVE inc/dec vector by element count
            case IF_SVE_BP_1A: // ............iiii ......pppppddddd -- SVE saturating inc/dec vector by element count
            {
                imm = emitGetInsSC(id);
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSvePattern(id.idSvePattern(), (imm > 1)); // ppppp
                if (imm > 1)
                {
                    jitprintf("mul ");
                    emitDispImm(imm, false, false); // iiii
                }
                break;
            }

            // <Zdn>.<T>, <Zdn>.<T>, #<const>
            case IF_SVE_BS_1A: // ..............ii iiiiiiiiiiiddddd -- SVE bitwise logical with immediate (unpredicated)
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd

                goto case IF_SVE_BT_1A;
            }

            // <Zd>.<T>, #<const>
            case IF_SVE_BT_1A: // ..............ii iiiiiiiiiiiddddd -- SVE broadcast bitmask immediate
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                bmi.immNRS = (uint)emitGetInsSC(id);
                imm = unchecked((nint)emitDecodeBitMaskImm(bmi, optGetSveElemsize(id.idInsOpt())));
                emitDispImm(imm, false); // iiiiiiiiiiiii
                break;
            }

            // <Xdn>, <Wdn>{, <pattern>{, MUL #<imm>}}
            // <Xdn>{, <pattern>{, MUL #<imm>}}
            // <Wdn>{, <pattern>{, MUL #<imm>}}
            case IF_SVE_BO_1A: // ...........Xiiii ......pppppddddd -- SVE saturating inc/dec register by element count
            {
                switch (id.idIns())
                {
                    case INS_sve_sqincb:
                    case INS_sve_sqdecb:
                    case INS_sve_sqinch:
                    case INS_sve_sqdech:
                    case INS_sve_sqincw:
                    case INS_sve_sqdecw:
                    case INS_sve_sqincd:
                    case INS_sve_sqdecd:
                    {
                        emitDispReg(id.idReg1(), EA_8BYTE, true); // ddddd

                        if (size == EA_4BYTE)
                        {
                            emitDispReg(id.idReg1(), EA_4BYTE, true);
                        }
                        break;
                    }

                    default:
                    {
                        emitDispReg(id.idReg1(), size, true); // ddddd
                        break;
                    }
                }

                imm = emitGetInsSC(id);
                emitDispSvePattern(id.idSvePattern(), (imm > 1)); // ppppp
                if (imm > 1)
                {
                    jitprintf("mul ");
                    emitDispImm(imm, false, false); // iiii
                }
                break;
            }

            // <Zd>.B, {<Zn1>.B, <Zn2>.B }, #<imm>
            case IF_SVE_BQ_2A: // ...........iiiii ...iiinnnnnddddd -- SVE extract vector (immediate offset, destructive)
            {
                imm = emitGetInsSC(id);
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispVectorRegList(id.idReg2(), 2, id.idInsOpt(), true); // nnnnn
                emitDispImm(imm, false); // iiiii iii
                break;
            }

            // <Zdn>.B, <Zdn>.B, <Zm>.B, #<imm>
            case IF_SVE_BQ_2B: // ...........iiiii ...iiimmmmmddddd -- SVE extract vector (immediate offset, destructive)
            {
                imm = emitGetInsSC(id);
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true);
                emitDispSveReg(id.idReg2(), id.idInsOpt(), true); // mmmmm
                emitDispImm(imm, false); // iiiii iii
                break;
            }

            // <Zd>.<T>, <Pg>/M, #<const>
            case IF_SVE_BU_2A: // ........xx..gggg ...iiiiiiiiddddd -- SVE copy floating-point immediate (predicated)
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(id.idInsFmt()), INS_OPTS_NONE, true); // gggg
                emitDispFloatImm(emitGetInsSC(id)); // iiiiiiii
                break;
            }

            // <Zd>.<T>, <Zn>.<T>, <Zm>.D
            case IF_SVE_BG_3A: // ........xx.mmmmm ......nnnnnddddd -- SVE bitwise shift by wide elements (unpredicated)
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveReg(id.idReg2(), id.idInsOpt(), true); // nnnnn
                emitDispSveReg(id.idReg3(), INS_OPTS_SCALABLE_D, false); // mmmmm
                break;
            }

            // <Zd>.<T>, [<Zn>.<T>, <Zm>.<T>{, <mod> <amount>}]
            case IF_SVE_BH_3A: // .........x.mmmmm ....hhnnnnnddddd -- SVE address generation
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                jitprintf("[");
                emitDispSveReg(id.idReg2(), id.idInsOpt(), true);
                emitDispSveReg(id.idReg3(), id.idInsOpt(), emitGetInsSC(id) > 0);
                emitDispSveExtendOptsModN(INS_OPTS_LSL, emitGetInsSC(id));
                jitprintf("]");
                break;
            }

            // <Zd>.D, [<Zn>.D, <Zm>.D, SXTW{ <amount>}]
            case IF_SVE_BH_3B: // ...........mmmmm ....hhnnnnnddddd -- SVE address generation
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                jitprintf("[");
                emitDispSveReg(id.idReg2(), id.idInsOpt(), true);
                emitDispSveReg(id.idReg3(), id.idInsOpt(), true);
                emitDispSveExtendOptsModN(INS_OPTS_SXTW, emitGetInsSC(id));
                jitprintf("]");
                break;
            }

            // <Zd>.D, [<Zn>.D, <Zm>.D, UXTW{ <amount>}]
            case IF_SVE_BH_3B_A: // ...........mmmmm ....hhnnnnnddddd -- SVE address generation
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                jitprintf("[");
                emitDispSveReg(id.idReg2(), id.idInsOpt(), true);
                emitDispSveReg(id.idReg3(), id.idInsOpt(), true);
                emitDispSveExtendOptsModN(INS_OPTS_UXTW, emitGetInsSC(id));
                jitprintf("]");
                break;
            }

            // <Zdn>.<T>, <V><m>
            case IF_SVE_CC_2A: // ........xx...... ......mmmmmddddd -- SVE insert SIMD&FP scalar register
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispReg(id.idReg2(), optGetSveElemsize(id.idInsOpt()), false); // mmmmm
                break;
            }

            // <Zdn>.<T>, <R><m>
            case IF_SVE_CD_2A: // ........xx...... ......mmmmmddddd -- SVE insert general register
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispReg(id.idReg2(), id.idInsOpt() == INS_OPTS_SCALABLE_D ? EA_8BYTE : EA_4BYTE, false); // mmmmm
                break;
            }

            // <Pd>.H, <Pn>.B
            case IF_SVE_CK_2A: // ................ .......NNNN.DDDD -- SVE unpack predicate elements
            {
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt), INS_OPTS_SCALABLE_H, true); // DDDD
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), INS_OPTS_SCALABLE_B, false); // NNNN
                break;
            }

            // <Zdn>.<T>, <Pg>, <Zdn>.<T>, <Zm>.<T>
            case IF_SVE_CM_3A: // ........xx...... ...gggmmmmmddddd -- SVE conditionally broadcast element to vector
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispLowPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true); // ggg
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveReg(id.idReg3(), id.idInsOpt(), false); // mmmmm
                break;
            }

            // <R><dn>, <Pg>, <R><dn>, <Zm>.<T>
            case IF_SVE_CO_3A: // ........xx...... ...gggmmmmmddddd -- SVE conditionally extract element to general register
            {
                emitDispReg(id.idReg1(), size, true); // ddddd
                emitDispLowPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true); // ggg
                emitDispReg(id.idReg1(), size, true); // ddddd
                emitDispSveReg(id.idReg3(), id.idInsOpt(), false); // mmmmm
                break;
            }

            // <V><dn>, <Pg>, <V><dn>, <Zm>.<T>
            case IF_SVE_CN_3A: // ........xx...... ...gggmmmmmddddd -- SVE conditionally extract element to SIMD&FP scalar
            case IF_SVE_HJ_3A: // ........xx...... ...gggmmmmmddddd -- SVE floating-point serial reduction (predicated)
            {
                emitDispReg(id.idReg1(), optGetSveElemsize(id.idInsOpt()), true); // ddddd
                emitDispLowPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true); // ggg
                emitDispReg(id.idReg1(), optGetSveElemsize(id.idInsOpt()), true); // ddddd
                emitDispSveReg(id.idReg3(), id.idInsOpt(), false); // mmmmm
                break;
            }

            // <V><d>, <Pg>, <Zn>.<T>
            case IF_SVE_AF_3A: // ........xx...... ...gggnnnnnddddd -- SVE bitwise logical reduction (predicated)
            case IF_SVE_AK_3A: // ........xx...... ...gggnnnnnddddd -- SVE integer min/max reduction (predicated)
            case IF_SVE_CR_3A: // ........xx...... ...gggnnnnnddddd -- SVE extract element to SIMD&FP scalar register
            case IF_SVE_HE_3A: // ........xx...... ...gggnnnnnddddd -- SVE floating-point recursive reduction
            {
                emitDispReg(id.idReg1(), optGetSveElemsize(id.idInsOpt()), true); // ddddd
                emitDispLowPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true); // ggg
                emitDispSveReg(id.idReg3(), id.idInsOpt(), false); // mmmmm
                break;
            }

            // <R><d>, <Pg>, <Zn>.<T>
            case IF_SVE_CS_3A: // ........xx...... ...gggnnnnnddddd -- SVE extract element to general register
            {
                emitDispReg(id.idReg1(), size, true); // ddddd
                emitDispLowPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true); // ggg
                emitDispSveReg(id.idReg3(), id.idInsOpt(), false); // mmmmm
                break;
            }

            // <Vd>.<T>, <Pg>, <Zn>.<Tb>
            case IF_SVE_AG_3A: // ........xx...... ...gggnnnnnddddd -- SVE bitwise logical reduction (quadwords)
            case IF_SVE_AJ_3A: // ........xx...... ...gggnnnnnddddd -- SVE integer add reduction (quadwords)
            case IF_SVE_AL_3A: // ........xx...... ...gggnnnnnddddd -- SVE integer min/max reduction (quadwords)
            case IF_SVE_GS_3A: // ........xx...... ...gggnnnnnddddd -- SVE floating-point recursive reduction (quadwords)
            {
                emitDispVectorReg(id.idReg1(), optSveToQuadwordElemsizeArrangement(id.idInsOpt()), true); // ddddd
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true); // ggg
                emitDispSveReg(id.idReg3(), id.idInsOpt(), false); // mmmmm
                break;
            }

            // <Dd>, <Pg>, <Zn>.<T>
            case IF_SVE_AI_3A: // ........xx...... ...gggnnnnnddddd -- SVE integer add reduction (predicated)
            {
                emitDispReg(id.idReg1(), EA_8BYTE, true); // ddddd
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true); // ggg
                emitDispSveReg(id.idReg3(), id.idInsOpt(), false); // mmmmm
                break;
            }

            // <Zd>.<T>, <Pg>/M, <Zn>.<T>
            case IF_SVE_AP_3A: // ........xx...... ...gggnnnnnddddd -- SVE bitwise unary operations (predicated)
            case IF_SVE_AQ_3A: // ........xx...... ...gggnnnnnddddd -- SVE integer unary operations (predicated)
            case IF_SVE_CU_3A: // ........xx...... ...gggnnnnnddddd -- SVE reverse within elements
            case IF_SVE_ES_3A: // ........xx...... ...gggnnnnnddddd -- SVE2 integer unary operations (predicated)
            case IF_SVE_HQ_3A: // ........xx...... ...gggnnnnnddddd -- SVE floating-point round to integral value
            case IF_SVE_HR_3A: // ........xx...... ...gggnnnnnddddd -- SVE floating-point unary operations
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true); // ggg
                emitDispSveReg(id.idReg3(), id.idInsOpt(), false); // mmmmm
                break;
            }

            case IF_SVE_CE_2A: // ................ ......nnnnn.DDDD -- SVE move predicate from vector
            {
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt), INS_OPTS_SCALABLE_B, true); // DDDD
                emitDispSveReg(id.idReg2(), false); // nnnnn
                break;
            }

            case IF_SVE_CE_2B: // .........i...ii. ......nnnnn.DDDD -- SVE move predicate from vector
            {
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt), INS_OPTS_SCALABLE_D, true); // DDDD
                emitDispSveRegIndex(id.idReg2(), emitGetInsSC(id), false); // nnnnn
                break;
            }

            case IF_SVE_CE_2C: // ..............i. ......nnnnn.DDDD -- SVE move predicate from vector
            {
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt), INS_OPTS_SCALABLE_H, true); // DDDD
                emitDispSveRegIndex(id.idReg2(), emitGetInsSC(id), false); // nnnnn
                break;
            }

            case IF_SVE_CE_2D: // .............ii. ......nnnnn.DDDD -- SVE move predicate from vector
            {
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt), INS_OPTS_SCALABLE_S, true); // DDDD
                emitDispSveRegIndex(id.idReg2(), emitGetInsSC(id), false); // nnnnn
                break;
            }

            case IF_SVE_CF_2A:                      // ................ .......NNNNddddd -- SVE move predicate into vector
            {
                emitDispSveReg(id.idReg1(), true); // ddddd
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), INS_OPTS_SCALABLE_B, false); // NNNN
                break;
            }

            case IF_SVE_CF_2B: // .........i...ii. .......NNNNddddd -- SVE move predicate into vector
            {
                emitDispSveRegIndex(id.idReg1(), emitGetInsSC(id), true); // ddddd
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), INS_OPTS_SCALABLE_D, false); // NNNN
                break;
            }

            case IF_SVE_CF_2C: // ..............i. .......NNNNddddd -- SVE move predicate into vector
            {
                emitDispSveRegIndex(id.idReg1(), emitGetInsSC(id), true); // ddddd
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), INS_OPTS_SCALABLE_H, false); // NNNN
                break;
            }

            case IF_SVE_CF_2D: // .............ii. .......NNNNddddd -- SVE move predicate into vector
            {
                emitDispSveRegIndex(id.idReg1(), emitGetInsSC(id), true); // ddddd
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), INS_OPTS_SCALABLE_S, false); // NNNN
                break;
            }

            // <Pd>.<T>, <Pn>.<T>, <Pm>.<T>
            case IF_SVE_CI_3A: // ........xx..MMMM .......NNNN.DDDD -- SVE permute predicate elements
            {
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt, 1), id.idInsOpt(), true); // DDDD
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt, 2), id.idInsOpt(), true); // NNNN
                emitDispPredicateReg(id.idReg3(), insGetPredicateType(fmt, 3), id.idInsOpt(), false); // MMMM
                break;
            }

            // <Zd>.<T>, <Pg>, <Zn>.<T>
            case IF_SVE_CL_3A: // ........xx...... ...gggnnnnnddddd -- SVE compress active elements
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true); // ggg
                emitDispSveReg(id.idReg3(), id.idInsOpt(), false); // mmmmm
                break;
            }

            // <Zd>.<T>, <Pg>/M, <V><n>
            case IF_SVE_CP_3A: // ........xx...... ...gggnnnnnddddd -- SVE copy SIMD&FP scalar register to vector
            {
                // (predicated)
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true); // ggg
                emitDispReg(id.idReg3(), size, false); // mmmmm
                break;
            }

            // <Zd>.<T>, <Pg>/M, <R><n|SP>
            case IF_SVE_CQ_3A: // ........xx...... ...gggnnnnnddddd -- SVE copy general register to vector (predicated)
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true); // ggg
                emitDispReg(encodingZRtoSP(id.idReg3()), size, false); // mmmmm
                break;
            }

            // <Zd>.Q, <Pg>/M, <Zn>.Q
            case IF_SVE_CT_3A: // ................ ...gggnnnnnddddd -- SVE reverse doublewords
            {
                emitDispSveReg(id.idReg1(), INS_OPTS_SCALABLE_Q, true); // ddddd
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true); // ggg
                emitDispSveReg(id.idReg3(), INS_OPTS_SCALABLE_Q, false); // nnnnn
                break;
            }

            // <Zd>.<T>, <Pv>, {<Zn1>.<T>, <Zn2>.<T>}
            case IF_SVE_CV_3A: // ........xx...... ...VVVnnnnnddddd -- SVE vector splice (constructive)
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true); // VVV
                emitDispSveConsecutiveRegList(id.idReg3(), (uint)insGetSveReg1ListSize(ins), id.idInsOpt(), false); // nnnnn
                break;
            }

            // <Zdn>.<T>, <Pv>, <Zdn>.<T>, <Zm>.<T>
            case IF_SVE_CV_3B: // ........xx...... ...VVVmmmmmddddd -- SVE vector splice (destructive)
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true); // VVV
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveReg(id.idReg3(), id.idInsOpt(), false); // mmmmm
                break;
            }

            // MOV <Zd>.<T>, <Pv>/M, <Zn>.<T> or SEL <Zd>.<T>, <Pv>, <Zn>.<T>, <Zm>.<T>
            case IF_SVE_CW_4A: // ........xx.mmmmm ..VVVVnnnnnddddd -- SVE select vector elements (predicated)
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd

                if (id.idIns() == INS_sve_mov)
                {
                    emitDispPredicateReg(id.idReg2(), PredicateType.PREDICATE_MERGE, id.idInsOpt(), true); // VVVV
                    emitDispSveReg(id.idReg3(), id.idInsOpt(), false); // nnnnn
                }
                else
                {
                    emitDispPredicateReg(id.idReg2(), PredicateType.PREDICATE_NONE, id.idInsOpt(), true); // VVVV
                    emitDispSveReg(id.idReg3(), id.idInsOpt(), true); // nnnnn
                    emitDispSveReg(id.idReg4(), id.idInsOpt(), false); // mmmmm
                }
                break;
            }

            // <Pd>.<T>, <Pg>/Z, <Zn>.<T>, <Zm>.<T>
            case IF_SVE_CX_4A: // ........xx.mmmmm ...gggnnnnn.DDDD -- SVE integer compare vectors
            case IF_SVE_GE_4A: // ........xx.mmmmm ...gggnnnnn.DDDD -- SVE2 character match
            case IF_SVE_HT_4A: // ........xx.mmmmm ...gggnnnnn.DDDD -- SVE floating-point compare vectors
            {
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt, 1), id.idInsOpt(), true); // DDDD
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt, 2), id.idInsOpt(), true); // ggg
                emitDispSveReg(id.idReg3(), id.idInsOpt(), true); // nnnnn
                emitDispSveReg(id.idReg4(), id.idInsOpt(), false); // mmmmm
                break;
            }

            // <Pd>.<T>, <Pg>/Z, <Zn>.<T>, <Zm>.D
            case IF_SVE_CX_4A_A: // ........xx.mmmmm ...gggnnnnn.DDDD -- SVE integer compare vectors
            {
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt, 1), id.idInsOpt(), true); // DDDD
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt, 2), id.idInsOpt(), true); // ggg
                emitDispSveReg(id.idReg3(), id.idInsOpt(), true); // nnnnn
                emitDispSveReg(id.idReg4(), INS_OPTS_SCALABLE_D, false); // mmmmm
                break;
            }

            // <Pd>.<T>, <Pg>/Z, <Zn>.<T>, #<imm>
            case IF_SVE_CY_3A: // ........xx.iiiii ...gggnnnnn.DDDD -- SVE integer compare with signed immediate
            case IF_SVE_CY_3B: // ........xx.iiiii ii.gggnnnnn.DDDD -- SVE integer compare with uint immediate
            {
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt, 1), id.idInsOpt(), true); // DDDD
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt, 2), id.idInsOpt(), true); // ggg
                emitDispSveReg(id.idReg3(), id.idInsOpt(), true); // nnnnn
                emitDispImm(emitGetInsSC(id), false, (fmt == IF_SVE_CY_3B)); // iiiii
                break;
            }

            // <Zda>.S, <Zn>.H, <Zm>.H[<imm>]
            case IF_SVE_EG_3A: // ...........iimmm ......nnnnnddddd -- SVE two-way dot product (indexed)
            case IF_SVE_FG_3A: // ...........iimmm ....i.nnnnnddddd -- SVE2 integer multiply-add long (indexed)
            case IF_SVE_FJ_3A: // ...........iimmm ....i.nnnnnddddd -- SVE2 saturating multiply-add (indexed)
            case IF_SVE_GZ_3A: // ...........iimmm ....i.nnnnnddddd -- SVE floating-point multiply-add long (indexed)
            // <Zda>.S, <Zn>.B, <Zm>.B[<imm>]
            case IF_SVE_EY_3A:   // ...........iimmm ......nnnnnddddd -- SVE integer dot product (indexed)
            case IF_SVE_EZ_3A:   // ...........iimmm ......nnnnnddddd -- SVE mixed sign dot product (indexed)
            case IF_SVE_GY_3B_D: // ...........iimmm ......nnnnnddddd -- SVE BFloat16 floating-point dot product (indexed)
            // <Zd>.S, <Zn>.H, <Zm>.H[<imm>]
            case IF_SVE_FE_3A: // ...........iimmm ....i.nnnnnddddd -- SVE2 integer multiply long (indexed)
            case IF_SVE_FH_3A: // ...........iimmm ....i.nnnnnddddd -- SVE2 saturating multiply (indexed)
            case IF_SVE_GY_3B: // ...........iimmm ......nnnnnddddd -- SVE BFloat16 floating-point dot product (indexed)
            // <Zda>.S, <Zn>.S, <Zm>.S[<imm>]
            case IF_SVE_GU_3A: // ...........iimmm ......nnnnnddddd -- SVE floating-point multiply-add (indexed)
            case IF_SVE_GX_3A: // ...........iimmm ......nnnnnddddd -- SVE floating-point multiply (indexed)
            case IF_SVE_FF_3B: // ...........iimmm ......nnnnnddddd -- SVE2 integer multiply-add (indexed)
            case IF_SVE_FK_3B: // ...........iimmm ......nnnnnddddd -- SVE2 saturating multiply-add high (indexed)
            {
                emitDispSveReg(id.idReg1(), INS_OPTS_SCALABLE_S, true); // ddddd
                emitDispSveReg(id.idReg2(), id.idInsOpt(), true); // nnnnn
                emitDispSveReg(id.idReg3(), id.idInsOpt(), false); // mmm
                emitDispElementIndex(emitGetInsSC(id), false); // ii/iii
                break;
            }

            // <Zda>.S, <Zn>.H, <Zm>.H
            case IF_SVE_EF_3A: // ...........mmmmm ......nnnnnddddd -- SVE two-way dot product
            case IF_SVE_HA_3A: // ...........mmmmm ......nnnnnddddd -- SVE BFloat16 floating-point dot product
            case IF_SVE_HB_3A: // ...........mmmmm ......nnnnnddddd -- SVE floating-point multiply-add long
            case IF_SVE_HD_3A: // ...........mmmmm ......nnnnnddddd -- SVE floating point matrix multiply accumulate
            case IF_SVE_EI_3A: // ...........mmmmm ......nnnnnddddd -- SVE mixed sign dot product
            case IF_SVE_GO_3A: // ...........mmmmm ......nnnnnddddd -- SVE2 FP8 multiply-add long long
            {
                emitDispSveReg(id.idReg1(), INS_OPTS_SCALABLE_S, true); // ddddd
                emitDispSveReg(id.idReg2(), id.idInsOpt(), true); // nnnnn
                emitDispSveReg(id.idReg3(), id.idInsOpt(), false); // mmmmm
                break;
            }

            // <Zda>.S, <Zn>.B, <Zm>.B
            case IF_SVE_HA_3A_F: // ...........mmmmm ......nnnnnddddd -- SVE BFloat16 floating-point dot product
            {
                emitDispSveReg(id.idReg1(), INS_OPTS_SCALABLE_S, true); // ddddd
                emitDispSveReg(id.idReg2(), INS_OPTS_SCALABLE_B, true); // nnnnn
                emitDispSveReg(id.idReg3(), INS_OPTS_SCALABLE_B, false); // mmmmm
                break;
            }

            // <Zd>.D, <Zn>.S, <Zm>.S[<imm>]
            case IF_SVE_FE_3B: // ...........immmm ....i.nnnnnddddd -- SVE2 integer multiply long (indexed)
            case IF_SVE_FH_3B: // ...........immmm ....i.nnnnnddddd -- SVE2 saturating multiply (indexed)
            // <Zda>.D, <Zn>.S, <Zm>.S[<imm>]
            case IF_SVE_FG_3B: // ...........immmm ....i.nnnnnddddd -- SVE2 integer multiply-add long (indexed)
            case IF_SVE_FJ_3B: // ...........immmm ....i.nnnnnddddd -- SVE2 saturating multiply-add (indexed)
            {
                emitDispSveReg(id.idReg1(), INS_OPTS_SCALABLE_D, true); // ddddd
                emitDispSveReg(id.idReg2(), id.idInsOpt(), true); // nnnnn
                emitDispSveReg(id.idReg3(), id.idInsOpt(), false); // mmmm
                emitDispElementIndex(emitGetInsSC(id), false); // ii
                break;
            }

            // <Zda>.D, <Zn>.H, <Zm>.H[<imm>]
            case IF_SVE_EY_3B: // ...........immmm ......nnnnnddddd -- SVE integer dot product (indexed)
            {
                emitDispSveReg(id.idReg1(), INS_OPTS_SCALABLE_D, true); // ddddd
                emitDispSveReg(id.idReg2(), id.idInsOpt(), true); // nnnnn
                emitDispSveReg(id.idReg3(), id.idInsOpt(), false); // mmm
                emitDispElementIndex(emitGetInsSC(id), false); // ii
                break;
            }

            // <Zda>.H, <Zn>.B, <Zm>.B[<imm>]
            case IF_SVE_GY_3A: // ...........iimmm ....i.nnnnnddddd -- SVE BFloat16 floating-point dot product (indexed)
            {
                emitDispSveReg(id.idReg1(), INS_OPTS_SCALABLE_H, true); // ddddd
                emitDispSveReg(id.idReg2(), id.idInsOpt(), true); // nnnnn
                emitDispSveReg(id.idReg3(), id.idInsOpt(), false); // mmm
                emitDispElementIndex(emitGetInsSC(id), false); // iii
                break;
            }

            // <Zd>.H, <Zn>.H, <Zm>.H[<imm>]
            case IF_SVE_FD_3A: // .........i.iimmm ......nnnnnddddd -- SVE2 integer multiply (indexed)
            case IF_SVE_FI_3A: // .........i.iimmm ......nnnnnddddd -- SVE2 saturating multiply high (indexed)
            // <Zd>.S, <Zn>.S, <Zm>.S[<imm>]
            case IF_SVE_FD_3B: // ...........iimmm ......nnnnnddddd -- SVE2 integer multiply (indexed)
            case IF_SVE_FI_3B: // ...........iimmm ......nnnnnddddd -- SVE2 saturating multiply high (indexed)
            // <Zd>.D, <Zn>.D, <Zm>.D[<imm>]
            case IF_SVE_FD_3C: // ...........immmm ......nnnnnddddd -- SVE2 integer multiply (indexed)
            case IF_SVE_FI_3C: // ...........immmm ......nnnnnddddd -- SVE2 saturating multiply high (indexed)
            // <Zda>.D, <Zn>.D, <Zm>.D[<imm>]
            case IF_SVE_GU_3B: // ...........immmm ......nnnnnddddd -- SVE floating-point multiply-add (indexed)
            case IF_SVE_GX_3B: // ...........immmm ......nnnnnddddd -- SVE floating-point multiply (indexed)
            case IF_SVE_FF_3C: // ...........immmm ......nnnnnddddd -- SVE2 integer multiply-add (indexed)
            case IF_SVE_FK_3C: // ...........immmm ......nnnnnddddd -- SVE2 saturating multiply-add high (indexed)
            // <Zda>.H, <Zn>.H, <Zm>.H[<imm>]
            case IF_SVE_GU_3C: // .........i.iimmm ......nnnnnddddd -- SVE floating-point multiply-add (indexed)
            case IF_SVE_GX_3C: // .........i.iimmm ......nnnnnddddd -- SVE floating-point multiply (indexed)
            case IF_SVE_FF_3A: // .........i.iimmm ......nnnnnddddd -- SVE2 integer multiply-add (indexed)
            case IF_SVE_FK_3A: // .........i.iimmm ......nnnnnddddd -- SVE2 saturating multiply-add high (indexed)
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveReg(id.idReg2(), id.idInsOpt(), true); // nnnnn
                emitDispSveReg(id.idReg3(), id.idInsOpt(), false); // mmm
                emitDispElementIndex(emitGetInsSC(id), false); // i/ii/iii
                break;
            }

            // <Pd>.B, <Pg>/Z, <Pn>.B, <Pm>.B
            case IF_SVE_CZ_4A: // ............MMMM ..gggg.NNNN.DDDD -- SVE predicate logical operations
            {
                var isFourReg =
                !((ins == INS_sve_mov) || (ins == INS_sve_movs) || (ins == INS_sve_not) || (ins == INS_sve_nots));
                var ptype = (ins == INS_sve_sel) ? PredicateType.PREDICATE_NONE : insGetPredicateType(fmt, 2);
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt, 1), id.idInsOpt(), true); // DDDD
                emitDispPredicateReg(id.idReg2(), ptype, id.idInsOpt(), true); // gggg
                emitDispPredicateReg(id.idReg3(), insGetPredicateType(fmt, 3), id.idInsOpt(), isFourReg); // NNNN

                if (isFourReg)
                {
                    emitDispPredicateReg(id.idReg4(), insGetPredicateType(fmt, 4), id.idInsOpt(), false); // MMMM
                }

                break;
            }

            // <Pd>.B, <Pn>.B
            case IF_SVE_CZ_4A_A: // ............MMMM ..gggg.NNNN.DDDD -- SVE predicate logical operations
            case IF_SVE_CZ_4A_L: // ............MMMM ..gggg.NNNN.DDDD -- SVE predicate logical operations
            {
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt, 1), id.idInsOpt(), true); // DDDD
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt, 2), id.idInsOpt(), false); // NNNN
                break;
            }

            //  <Pd>.B, <Pg>/M, <Pn>.B
            case IF_SVE_CZ_4A_K: // ............MMMM ..gggg.NNNN.DDDD -- SVE predicate logical operations
            {
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt, 1), id.idInsOpt(), true); // DDDD
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt, 2), id.idInsOpt(), true); // gggg
                emitDispPredicateReg(id.idReg3(), insGetPredicateType(fmt, 3), id.idInsOpt(), false); // NNNN
                break;
            }

            //  <Pd>.B, <Pg>/Z, <Pn>.B, <Pm>.B
            case IF_SVE_DA_4A: // ............MMMM ..gggg.NNNN.DDDD -- SVE propagate break from previous partition
            {
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt, 1), id.idInsOpt(), true); // DDDD
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt, 2), id.idInsOpt(), true); // gggg
                emitDispPredicateReg(id.idReg3(), insGetPredicateType(fmt, 3), id.idInsOpt(), true); // NNNN
                emitDispPredicateReg(id.idReg4(), insGetPredicateType(fmt, 4), id.idInsOpt(), false); // MMMM
                break;
            }

            // <Pd>.B, <Pg>/<ZM>, <Pn>.B
            case IF_SVE_DB_3A: // ................ ..gggg.NNNNMDDDD -- SVE partition break condition
            case IF_SVE_DB_3B: // ................ ..gggg.NNNN.DDDD -- SVE partition break condition
            {
                var ptype = id.idPredicateReg2Merge()
                    ? PredicateType.PREDICATE_MERGE
                    : PredicateType.PREDICATE_ZERO;
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt, 1), id.idInsOpt(), true); // DDDD
                emitDispPredicateReg(id.idReg2(), ptype, id.idInsOpt(), true); // gggg
                emitDispPredicateReg(id.idReg3(), insGetPredicateType(fmt, 3), id.idInsOpt(), false); // NNNN
                break;
            }

            // <Pdm>.B, <Pg>/Z, <Pn>.B, <Pdm>.B
            case IF_SVE_DC_3A: // ................ ..gggg.NNNN.MMMM -- SVE propagate break to next partition
            {
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt, 1), id.idInsOpt(), true); // DDDD
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt, 2), id.idInsOpt(), true); // gggg
                emitDispPredicateReg(id.idReg3(), insGetPredicateType(fmt, 3), id.idInsOpt(), true); // NNNN
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt, 4), id.idInsOpt(), false); // MMMM
                break;
            }

            // <Pdn>.B, <Pg>, <Pdn>.B
            case IF_SVE_DD_2A: // ................ .......gggg.DDDD -- SVE predicate first active
            {
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt, 1), id.idInsOpt(), true); // DDDD
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt, 2), id.idInsOpt(), true); // gggg
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt, 3), id.idInsOpt(), false); // DDDD
                break;
            }

            // <Pd>.<T>{, <pattern>}
            case IF_SVE_DE_1A: // ........xx...... ......ppppp.DDDD -- SVE predicate initialize
            {
                var dispPattern = (id.idSvePattern() != SVE_PATTERN_ALL);
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt), id.idInsOpt(), dispPattern); // DDDD
                if (dispPattern)
                {
                    emitDispSvePattern(id.idSvePattern(), false); // ppppp
                }
                break;
            }

            // <Pd>.<T>, <Pn>.<T>
            case IF_SVE_CJ_2A: // ........xx...... .......NNNN.DDDD -- SVE reverse predicate elements
            {
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt, 1), id.idInsOpt(), true); // DDDD
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt, 2), id.idInsOpt(), false); // NNNN
                break;
            }

            // <Pdn>.<T>, <Pv>, <Pdn>.<T>
            case IF_SVE_DF_2A: // ........xx...... .......VVVV.DDDD -- SVE predicate next active
            {
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt, 1), id.idInsOpt(), true); // DDDD
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt, 2), id.idInsOpt(), true); // VVVV
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt, 3), id.idInsOpt(), false); // DDDD
                break;
            }

            // <Pd>.B, <Pg>/Z
            case IF_SVE_DG_2A: // ................ .......gggg.DDDD -- SVE predicate read from FFR (predicated)
            case IF_SVE_DI_2A: // ................ ..gggg.NNNN..... -- SVE predicate test
            {
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt, 1), id.idInsOpt(), true); // DDDD
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt, 2), id.idInsOpt(), false); // gggg
                break;
            }

            // <Pd>.B
            case IF_SVE_DH_1A: // ................ ............DDDD -- SVE predicate read from FFR (unpredicated)
            case IF_SVE_DJ_1A: // ................ ............DDDD -- SVE predicate zero
            {
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt), id.idInsOpt(), false); // DDDD
                break;
            }

            // <Xd>, <Pg>, <Pn>.<T>
            case IF_SVE_DK_3A:                             // ........xx...... ..gggg.NNNNddddd -- SVE predicate count
            {
                emitDispReg(id.idReg1(), EA_8BYTE, true); // ddddd
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt, 2), id.idInsOpt(), true); // gggg
                emitDispPredicateReg(id.idReg3(), insGetPredicateType(fmt, 3), id.idInsOpt(), false); // NNNN
                break;
            }

            // <Zda>.<T>, <Pg>/M, <Zn>.<Tb>
            case IF_SVE_EQ_3A: // ........xx...... ...gggnnnnnddddd -- SVE2 integer pairwise add and accumulate long
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispLowPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true); // ggg
                emitDispSveReg(id.idReg3(), (insOpts)((uint)id.idInsOpt() - 1), false); // mmmmm
                break;
            }

            // <Zd>.H, { <Zn1>.S-<Zn2>.S }, #<const>
            case IF_SVE_GA_2A: // ............iiii ......nnnn.ddddd -- SME2 multi-vec shift narrow
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveConsecutiveRegList(id.idReg2(), 2, INS_OPTS_SCALABLE_S, true); // nnnn
                emitDispImm(emitGetInsSC(id), false); // iiii
                break;
            }

            // <Xd>, <PNn>.<T>, <vl>
            case IF_SVE_DL_2A: // ........xx...... .....l.NNNNddddd -- SVE predicate count (predicate-as-counter)
            {
                emitDispReg(id.idReg1(), EA_8BYTE, true); // ddddd
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true); // NNNN
                emitDispVectorLengthSpecifier(id);
                break;
            }

            // <Xdn>, <Pm>.<T>
            case IF_SVE_DM_2A: // ........xx...... .......MMMMddddd -- SVE inc/dec register by predicate count
            {
                emitDispReg(id.idReg1(), id.idOpSize(), true); // ddddd
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), false); // MMMM
                break;
            }

            // <Zdn>.<T>, <Pm>.<T>
            case IF_SVE_DN_2A: // ........xx...... .......MMMMddddd -- SVE inc/dec vector by predicate count
            case IF_SVE_DP_2A: // ........xx...... .......MMMMddddd -- SVE saturating inc/dec vector by predicate count
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), false); // MMMM
                break;
            }

            // <Xdn>, <Pm>.<T>, <Wdn>
            // <Xdn>, <Pm>.<T>
            case IF_SVE_DO_2A: // ........xx...... .....X.MMMMddddd -- SVE saturating inc/dec register by predicate count
            {
                if ((ins == INS_sve_sqdecp) || (ins == INS_sve_sqincp))
                {
                    // 32-bit result: <Xdn>, <Pm>.<T>, <Wdn>
                    // 64-bit result: <Xdn>, <Pm>.<T>
                    var is32BitResult = (id.idOpSize() == EA_4BYTE); // X
                    emitDispReg(id.idReg1(), EA_8BYTE, true); // ddddd
                    emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), is32BitResult); // MMMM

                    if (is32BitResult)
                    {
                        emitDispReg(id.idReg1(), EA_4BYTE, false);
                    }
                }
                else
                {
                    assert((ins == INS_sve_uqdecp) || (ins == INS_sve_uqincp));
                    emitDispReg(id.idReg1(), id.idOpSize(), true); // ddddd
                    emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), false); // MMMM
                }
                break;
            }

            // none
            case IF_SVE_DQ_0A: // ................ ................ -- SVE FFR initialise
            {
                break;
            }

            // <Pn>.B
            case IF_SVE_DR_1A: // ................ .......NNNN..... -- SVE FFR write from predicate
            {
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt), id.idInsOpt(), false); // NNNN
                break;
            }

            // <R><n>, <R><m>
            case IF_SVE_DS_2A: // .........x.mmmmm ......nnnnn..... -- SVE conditionally terminate scalars
            {
                emitDispReg(id.idReg1(), id.idOpSize(), true); // nnnnn
                emitDispReg(id.idReg2(), id.idOpSize(), false); // mmmmm
                break;
            }

            // <Zd>.H, {<Zn1>.S-<Zn2>.S }
            case IF_SVE_FZ_2A: // ................ ......nnnn.ddddd -- SME2 multi-vec extract narrow
            {
                emitDispSveReg(id.idReg1(), INS_OPTS_SCALABLE_H, true);
                emitDispSveConsecutiveRegList(id.idReg2(), 2, INS_OPTS_SCALABLE_S, false);
                break;
            }

            // <Zd>.B, {<Zn1>.H-<Zn2>.H }
            case IF_SVE_HG_2A: // ................ ......nnnn.ddddd -- SVE2 FP8 downconverts
            {
                emitDispSveReg(id.idReg1(), INS_OPTS_SCALABLE_B, true);
                emitDispSveConsecutiveRegList(id.idReg2(), 2, INS_OPTS_SCALABLE_H, false);
                break;
            }

            // <Zd>.<T>, <Zn>.<Tb>
            case IF_SVE_GD_2A: // .........x.xx... ......nnnnnddddd -- SVE2 saturating extract narrow
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveReg(id.idReg2(), optWidenSveElemsizeArrangement(id.idInsOpt()), false); // nnnnn
                break;
            }

            // <Xd|SP>, <Xn|SP>, #<imm>
            case IF_SVE_BB_2A: // ...........nnnnn .....iiiiiiddddd -- SVE stack frame adjustment
            {
                var reg1 = (id.idReg1() == REG_ZR) ? REG_SP : id.idReg1();
                var reg2 = (id.idReg2() == REG_ZR) ? REG_SP : id.idReg2();
                emitDispReg(reg1, id.idOpSize(), true); // ddddd
                emitDispReg(reg2, id.idOpSize(), true); // nnnnn
                emitDispImm(emitGetInsSC(id), false); // iiiiii
                break;
            }

            // <Xd>, #<imm>
            case IF_SVE_BC_1A: // ................ .....iiiiiiddddd -- SVE stack frame size
            {
                emitDispReg(id.idReg1(), id.idOpSize(), true); // ddddd
                emitDispImm(emitGetInsSC(id), false); // iiiiii
                break;
            }

            // <Zd>.<T>, <Zn>.<Tb>, #<const>
            case IF_SVE_FR_2A: // .........x.xxiii ......nnnnnddddd -- SVE2 bitwise shift left long
            {
                insOpts narrowSizeSpecifier = (insOpts)(id.idInsOpt() - 1);
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveReg(id.idReg2(), narrowSizeSpecifier, true); // nnnnn
                emitDispImm(emitGetInsSC(id), false); // iii
                break;
            }

            // <Zd>.<T>, <Zn>.<Tb>, #<const>
            case IF_SVE_GB_2A: // .........x.xxiii ......nnnnnddddd -- SVE2 bitwise shift right narrow
            {
                insOpts largeSizeSpecifier = (insOpts)(id.idInsOpt() + 1);
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveReg(id.idReg2(), largeSizeSpecifier, true); // nnnnn
                emitDispImm(emitGetInsSC(id), false); // iii
                break;
            }

            // <Zdn>.<T>, <Zdn>.<T>, <Zm>.<T>, <const>
            case IF_SVE_FV_2A: // ........xx...... .....rmmmmmddddd -- SVE2 complex integer add
            {
                // Rotation bit implies rotation is 270 if set, else rotation is 90
                var rot = emitDecodeRotationImm90_or_270(emitGetInsSC(id));
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveReg(id.idReg2(), id.idInsOpt(), true); // mmmmm
                emitDispImm(rot, false); // r
                break;
            }

            // <Zda>.<T>, <Zn>.<T>, <Zm>.<T>
            case IF_SVE_FY_3A: // .........x.mmmmm ......nnnnnddddd -- SVE2 integer add/subtract long with carry
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveReg(id.idReg2(), id.idInsOpt(), true); // nnnnn
                emitDispSveReg(id.idReg3(), id.idInsOpt(), false); // mmmmm
                break;
            }

            // <Zdn>.B, <Zdn>.B, <Zm>.B
            // <Zdn>.S, <Zdn>.S, <Zm>.S
            case IF_SVE_GK_2A: // ................ ......mmmmmddddd -- SVE2 crypto destructive binary operations
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveReg(id.idReg2(), id.idInsOpt(), false); // mmmmm
                break;
            }

            // <Zdn>.B, <Zdn>.B
            case IF_SVE_GL_1A: // ................ ...........ddddd -- SVE2 crypto unary operations
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveReg(id.idReg1(), id.idInsOpt(), false); // ddddd
                break;
            }

            // <Pd>.<T>, <R><n>, <R><m>
            case IF_SVE_DT_3A: // ........xx.mmmmm ...X..nnnnn.DDDD -- SVE integer compare scalar count and limit
            // <Pd>.<T>, <Xn>, <Xm>
            case IF_SVE_DU_3A: // ........xx.mmmmm ......nnnnn.DDDD -- SVE pointer conflict compare
            {
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt), id.idInsOpt(), true); // DDDD
                emitDispReg(id.idReg2(), id.idOpSize(), true); // nnnnn
                emitDispReg(id.idReg3(), id.idOpSize(), false); // mmmmm
                break;
            }

            // <Pd>, <Pn>, <Pm>.<T>[<Wv>, <imm>]
            case IF_SVE_DV_4A: // ........ix.xxxvv ..NNNN.MMMM.DDDD -- SVE broadcast predicate element
            {
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt, 1), id.idInsOpt(), true); // DDDD
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt, 2), id.idInsOpt(), true); // NNNN
                emitDispPredicateReg(id.idReg3(), insGetPredicateType(fmt, 3), id.idInsOpt(), false); // MMMM
                jitprintf("[");
                emitDispReg(id.idReg4(), EA_4BYTE, true); // vv
                emitDispImm(emitGetInsSC(id), false); // ix xx
                jitprintf("]");
                break;
            }

            // <Pd>.<T>, <PNn>[<imm>]
            case IF_SVE_DW_2A: // ........xx...... ......iiNNN.DDDD -- SVE extract mask predicate from predicate-as-counter
            {
                emitDispPredicateReg(id.idReg1(), PredicateType.PREDICATE_SIZED, id.idInsOpt(), true); // DDDD
                emitDispPredicateReg(id.idReg2(), PredicateType.PREDICATE_N, id.idInsOpt(), false); // NNN
                emitDispElementIndex(emitGetInsSC(id), false); // ii
                break;
            }

            // {<Pd1>.<T>, <Pd2>.<T>}, <PNn>[<imm>]
            case IF_SVE_DW_2B: // ........xx...... .......iNNN.DDDD -- SVE extract mask predicate from predicate-as-counter
            {
                emitDispPredicateRegPair(id.idReg1(), id.idInsOpt()); // DDDD
                emitDispPredicateReg(id.idReg2(), PredicateType.PREDICATE_N, id.idInsOpt(), false); // NNN
                emitDispElementIndex(emitGetInsSC(id), false); // i
                break;
            }

            // {<Pd1>.<T>, <Pd2>.<T>}, <Xn>, <Xm>
            case IF_SVE_DX_3A: // ........xx.mmmmm ......nnnnn.DDD. -- SVE integer compare scalar count and limit (predicate
            {
                // pair)
                emitDispLowPredicateRegPair(id.idReg1(), id.idInsOpt());
                emitDispReg(id.idReg2(), id.idOpSize(), true); // nnnnn
                emitDispReg(id.idReg3(), id.idOpSize(), false); // mmmmm
                break;
            }

            // <PNd>.<T>, <Xn>, <Xm>, <vl>
            case IF_SVE_DY_3A: // ........xx.mmmmm ..l...nnnnn..DDD -- SVE integer compare scalar count and limit
            {
                // (predicate-as-counter)
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt), id.idInsOpt(), true); // DDD
                emitDispReg(id.idReg2(), id.idOpSize(), true); // nnnnn
                emitDispReg(id.idReg3(), id.idOpSize(), true); // mmmmm
                emitDispVectorLengthSpecifier(id);
                break;
            }

            // PTRUE <PNd>.<T>
            case IF_SVE_DZ_1A: // ........xx...... .............DDD -- sve_int_pn_ptrue
            {
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt), id.idInsOpt(), false); // DDD
                break;
            }

            // FDUP <Zd>.<T>, #<const>
            // FMOV <Zd>.<T>, #<const>
            case IF_SVE_EA_1A: // ........xx...... ...iiiiiiiiddddd -- SVE broadcast floating-point immediate (unpredicated)
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispFloatImm(emitGetInsSC(id)); // iiiiiiii
                break;
            }

            // DUP <Zd>.<T>, #<imm>{, <shift>}
            // MOV <Zd>.<T>, #<imm>{, <shift>}
            case IF_SVE_EB_1A: // ........xx...... ..hiiiiiiiiddddd -- SVE broadcast integer immediate (unpredicated)
            {
                imm = emitGetInsSC(id);
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispImmOptsLSL(imm, id.idHasShift(), 8); // h iiiiiiii
                break;
            }

            // ADD <Zdn>.<T>, <Zdn>.<T>, #<imm>{, <shift>}
            // SQADD <Zdn>.<T>, <Zdn>.<T>, #<imm>{, <shift>}
            // UQADD <Zdn>.<T>, <Zdn>.<T>, #<imm>{, <shift>}
            // SUB <Zdn>.<T>, <Zdn>.<T>, #<imm>{, <shift>}
            // SUBR <Zdn>.<T>, <Zdn>.<T>, #<imm>{, <shift>}
            // SQSUB <Zdn>.<T>, <Zdn>.<T>, #<imm>{, <shift>}
            // UQSUB <Zdn>.<T>, <Zdn>.<T>, #<imm>{, <shift>}
            case IF_SVE_EC_1A: // ........xx...... ..hiiiiiiiiddddd -- SVE integer add/subtract immediate (unpredicated)
            {
                imm = emitGetInsSC(id);
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispImmOptsLSL(imm, id.idHasShift(), 8); // h iiiiiiii
                break;
            }

            // FMOV <Zd>.<T>, #0.0
            // (Preferred disassembly: FMOV <Zd>.<T>, #0)
            case IF_SVE_EB_1B: // ........xx...... ...........ddddd -- SVE broadcast integer immediate (unpredicated)
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispImm(0, false);
                break;
            }

            // SMAX <Zdn>.<T>, <Zdn>.<T>, #<imm>
            // SMIN <Zdn>.<T>, <Zdn>.<T>, #<imm>
            // UMAX <Zdn>.<T>, <Zdn>.<T>, #<imm>
            // UMIN <Zdn>.<T>, <Zdn>.<T>, #<imm>
            case IF_SVE_ED_1A: // ........xx...... ...iiiiiiiiddddd -- SVE integer min/max immediate (unpredicated)
            // MUL <Zdn>.<T>, <Zdn>.<T>, #<imm>
            case IF_SVE_EE_1A: // ........xx...... ...iiiiiiiiddddd -- SVE integer multiply immediate (unpredicated)
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispImm(emitGetInsSC(id), false); // iiiiiiii
                break;
            }

            // <Zda>.<T>, <Zn>.<Tb>, <Zm>.<Tb>
            case IF_SVE_EH_3A: // ........xx.mmmmm ......nnnnnddddd -- SVE integer dot product (unpredicated)
            // <Zda>.S, <Zn>.B, <Zm>.B
            case IF_SVE_FO_3A: // ...........mmmmm ......nnnnnddddd -- SVE integer matrix multiply accumulate
            {
                insOpts smallSizeSpecifier = (insOpts)(id.idInsOpt() - 2);
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveReg(id.idReg2(), smallSizeSpecifier, true); // nnnnn
                emitDispSveReg(id.idReg3(), smallSizeSpecifier, false); // mmmmm
                break;
            }

            // <Zda>.<T>, <Zn>.<Tb>, <Zm>.<Tb>
            case IF_SVE_EL_3A: // ........xx.mmmmm ......nnnnnddddd
            // <Zd>.<T>, <Zn>.<Tb>, <Zm>.<Tb>
            case IF_SVE_FL_3A: // ........xx.mmmmm ......nnnnnddddd
            // <Zd>.Q, <Zn>.D, <Zm>.D
            case IF_SVE_FN_3B: // ...........mmmmm ......nnnnnddddd -- SVE2 integer multiply long
            {
                insOpts smallSizeSpecifier = (insOpts)(id.idInsOpt() - 1);
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveReg(id.idReg2(), smallSizeSpecifier, true); // nnnnn
                emitDispSveReg(id.idReg3(), smallSizeSpecifier, false); // mmmmm
                break;
            }

            // <Zd>.<T>, <Zn>.<Tb>, <Zm>.<Tb>
            case IF_SVE_GC_3A: // ........xx.mmmmm ......nnnnnddddd -- SVE2 integer add/subtract narrow high part
            {
                insOpts largeSizeSpecifier = (insOpts)(id.idInsOpt() + 1);
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveReg(id.idReg2(), largeSizeSpecifier, true); // nnnnn
                emitDispSveReg(id.idReg3(), largeSizeSpecifier, false); // mmmmm
                break;
            }

            // <Zd>.<T>, <Zn>.<T>, <Zm>.<Tb>
            case IF_SVE_FM_3A: // ........xx.mmmmm ......nnnnnddddd -- SVE2 integer add/subtract wide
            {
                insOpts smallSizeSpecifier = (insOpts)(id.idInsOpt() - 1);
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveReg(id.idReg2(), id.idInsOpt(), true); // nnnnn
                emitDispSveReg(id.idReg3(), smallSizeSpecifier, false); // mmmmm
                break;
            }

            // CDOT <Zda>.<T>, <Zn>.<Tb>, <Zm>.<Tb>, <const>
            case IF_SVE_EJ_3A: // ........xx.mmmmm ....rrnnnnnddddd -- SVE2 complex integer dot product
            {
                insOpts smallSizeSpecifier = (insOpts)(id.idInsOpt() - 2);
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveReg(id.idReg2(), smallSizeSpecifier, true); // nnnnn
                emitDispSveReg(id.idReg3(), smallSizeSpecifier, true); // mmmmm

                // rot specifies a multiple of 90-degree rotations
                emitDispImm(emitDecodeRotationImm0_to_270(emitGetInsSC(id)), false); // rr
                break;
            }

            // CMLA <Zda>.<T>, <Zn>.<T>, <Zm>.<T>, <const>
            // SQRDCMLAH <Zda>.<T>, <Zn>.<T>, <Zm>.<T>, <const>
            case IF_SVE_EK_3A: // ........xx.mmmmm ....rrnnnnnddddd -- SVE2 complex integer multiply-add
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveReg(id.idReg2(), id.idInsOpt(), true); // nnnnn
                emitDispSveReg(id.idReg3(), id.idInsOpt(), true); // mmmmm

                // rot specifies a multiple of 90-degree rotations
                emitDispImm(emitDecodeRotationImm0_to_270(emitGetInsSC(id)), false); // rr
                break;
            }

            // CDOT <Zda>.S, <Zn>.B, <Zm>.B[<imm>], <const>
            case IF_SVE_FA_3A: // ...........iimmm ....rrnnnnnddddd -- SVE2 complex integer dot product (indexed)
            {
                var packedImm = emitGetInsSC(id);
                var rot = (packedImm & 0b11);
                var index = (packedImm >> 2);
                emitDispSveReg(id.idReg1(), INS_OPTS_SCALABLE_S, true); // ddddd
                emitDispSveReg(id.idReg2(), id.idInsOpt(), true); // nnnnn
                emitDispSveReg(id.idReg3(), id.idInsOpt(), false); // mmm
                emitDispElementIndex(index, true); // ii

                // rot specifies a multiple of 90-degree rotations
                emitDispImm(emitDecodeRotationImm0_to_270(rot), false); // rr
                break;
            }

            // CDOT <Zda>.D, <Zn>.H, <Zm>.H[<imm>], <const>
            case IF_SVE_FA_3B: // ...........immmm ....rrnnnnnddddd -- SVE2 complex integer dot product (indexed)
            {
                var packedImm = emitGetInsSC(id);
                var rot = (packedImm & 0b11);
                var index = (packedImm >> 2);
                emitDispSveReg(id.idReg1(), INS_OPTS_SCALABLE_D, true); // ddddd
                emitDispSveReg(id.idReg2(), id.idInsOpt(), true); // nnnnn
                emitDispSveReg(id.idReg3(), id.idInsOpt(), false); // mmm
                emitDispElementIndex(index, true); // i

                // rot specifies a multiple of 90-degree rotations
                emitDispImm(emitDecodeRotationImm0_to_270(rot), false); // rr
                break;
            }

            // CMLA <Zda>.H, <Zn>.H, <Zm>.H[<imm>], <const>
            case IF_SVE_FB_3A: // ...........iimmm ....rrnnnnnddddd -- SVE2 complex integer multiply-add (indexed)
            // CMLA <Zda>.S, <Zn>.S, <Zm>.S[<imm>], <const>
            case IF_SVE_FB_3B: // ...........immmm ....rrnnnnnddddd -- SVE2 complex integer multiply-add (indexed)
            // SQRDCMLAH <Zda>.H, <Zn>.H, <Zm>.H[<imm>], <const>
            case IF_SVE_FC_3A: // ...........iimmm ....rrnnnnnddddd -- SVE2 complex saturating multiply-add (indexed)
            // SQRDCMLAH <Zda>.S, <Zn>.S, <Zm>.S[<imm>], <const>
            case IF_SVE_FC_3B: // ...........immmm ....rrnnnnnddddd -- SVE2 complex saturating multiply-add (indexed)
            // FCMLA <Zda>.S, <Zn>.S, <Zm>.S[<imm>], <const>
            case IF_SVE_GV_3A: // ...........immmm ....rrnnnnnddddd -- SVE floating-point complex multiply-add (indexed)
            {
                var packedImm = emitGetInsSC(id);
                var rot = (packedImm & 0b11);
                var index = (packedImm >> 2);
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispSveReg(id.idReg2(), id.idInsOpt(), true); // nnnnn
                emitDispSveReg(id.idReg3(), id.idInsOpt(), false); // mmm
                emitDispElementIndex(index, true); // i

                // rot specifies a multiple of 90-degree rotations
                emitDispImm(emitDecodeRotationImm0_to_270(rot), false); // rr
                break;
            }

            // <Zd>.H, <Pg>/M, <Zn>.S
            // <Zd>.S, <Pg>/M, <Zn>.D
            // <Zd>.D, <Pg>/M, <Zn>.S
            // <Zd>.S, <Pg>/M, <Zn>.H
            // <Zd>.D, <Pg>/M, <Zn>.D
            // <Zd>.S, <Pg>/M, <Zn>.S
            // <Zd>.D, <Pg>/M, <Zn>.H
            // <Zd>.H, <Pg>/M, <Zn>.H
            // <Zd>.H, <Pg>/M, <Zn>.D
            // <Zd>.H, <Pg>/M, <Zn>.S
            case IF_SVE_GQ_3A: // ................ ...gggnnnnnddddd -- SVE floating-point convert precision odd elements
            case IF_SVE_HO_3A: // ................ ...gggnnnnnddddd -- SVE floating-point convert precision
            case IF_SVE_HO_3B: // ................ ...gggnnnnnddddd -- SVE floating-point convert precision
            case IF_SVE_HO_3C: // ................ ...gggnnnnnddddd -- SVE floating-point convert precision
            case IF_SVE_HP_3B: // ................ ...gggnnnnnddddd -- SVE floating-point convert to integer
            case IF_SVE_HS_3A: // ................ ...gggnnnnnddddd -- SVE integer convert to floating-point
            {
                var opt = id.idInsOpt();

                switch (ins)
                {
                    // These cases have only one combination of operands so the option may be omitted.
                    case INS_sve_fcvtxnt:
                    {
                        opt = INS_OPTS_D_TO_S;
                        break;
                    }

                    case INS_sve_bfcvtnt:
                    {
                        opt = INS_OPTS_S_TO_H;
                        break;
                    }

                    case INS_sve_fcvtx:
                    {
                        opt = INS_OPTS_D_TO_S;
                        break;
                    }

                    case INS_sve_bfcvt:
                    {
                        opt = INS_OPTS_S_TO_H;
                        break;
                    }

                    default:
                    {
                        break;
                    }
                }

                optExpandConversionPair(opt, out var dst, out var src);

                emitDispSveReg(id.idReg1(), dst, true); // ddddd
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true); // ggg
                emitDispSveReg(id.idReg3(), src, false); // nnnnn
                break;
            }

            // { <Zt>.D }, <Pg>/Z, [<Xn|SP>{, #<imm>, MUL VL}]
            // Some of these formats may allow changing the element size instead of using 'D' for all instructions.
            case IF_SVE_IH_3A:   // ............iiii ...gggnnnnnttttt -- SVE contiguous load (quadwords, scalar plus
            // immediate)
            case IF_SVE_IH_3A_A: // ............iiii ...gggnnnnnttttt -- SVE contiguous load (quadwords, scalar plus
            // immediate)
            case IF_SVE_IH_3A_F: // ............iiii ...gggnnnnnttttt -- SVE contiguous load (quadwords, scalar plus
            // immediate)
            case IF_SVE_IJ_3A:   // ............iiii ...gggnnnnnttttt -- SVE contiguous load (scalar plus immediate)
            case IF_SVE_IJ_3A_D: // ............iiii ...gggnnnnnttttt -- SVE contiguous load (scalar plus immediate)
            case IF_SVE_IJ_3A_E: // ............iiii ...gggnnnnnttttt -- SVE contiguous load (scalar plus immediate)
            case IF_SVE_IJ_3A_F: // ............iiii ...gggnnnnnttttt -- SVE contiguous load (scalar plus immediate)
            case IF_SVE_IJ_3A_G: // ............iiii ...gggnnnnnttttt -- SVE contiguous load (scalar plus immediate)
            case IF_SVE_IL_3A: // ............iiii ...gggnnnnnttttt -- SVE contiguous non-fault load (scalar plus immediate)
            case IF_SVE_IL_3A_A: // ............iiii ...gggnnnnnttttt -- SVE contiguous non-fault load (scalar plus
            // immediate)
            case IF_SVE_IL_3A_B: // ............iiii ...gggnnnnnttttt -- SVE contiguous non-fault load (scalar plus
            // immediate)
            case IF_SVE_IL_3A_C: // ............iiii ...gggnnnnnttttt -- SVE contiguous non-fault load (scalar plus
            // immediate)
            case IF_SVE_IM_3A:   // ............iiii ...gggnnnnnttttt -- SVE contiguous non-temporal load (scalar plus
            // immediate)
            // { <Zt>.B }, <Pg>/Z, [<Xn|SP>{, #<imm>}]
            // { <Zt>.H }, <Pg>/Z, [<Xn|SP>{, #<imm>}]
            // { <Zt>.S }, <Pg>/Z, [<Xn|SP>{, #<imm>}]
            // { <Zt>.D }, <Pg>/Z, [<Xn|SP>{, #<imm>}]
            case IF_SVE_IO_3A: // ............iiii ...gggnnnnnttttt -- SVE load and broadcast quadword (scalar plus
            // immediate)
            // { <Zt1>.Q, <Zt2>.Q }, <Pg>/Z, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt1>.Q, <Zt2>.Q, <Zt3>.Q }, <Pg>/Z, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt1>.Q, <Zt2>.Q, <Zt3>.Q, <Zt4>.Q }, <Pg>/Z, [<Xn|SP>{, #<imm>, MUL VL}]
            case IF_SVE_IQ_3A: // ............iiii ...gggnnnnnttttt -- SVE load multiple structures (quadwords, scalar plus
            // immediate)
            // { <Zt1>.B, <Zt2>.B }, <Pg>/Z, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt1>.H, <Zt2>.H }, <Pg>/Z, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt1>.S, <Zt2>.S }, <Pg>/Z, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt1>.D, <Zt2>.D }, <Pg>/Z, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt1>.B, <Zt2>.B, <Zt3>.B }, <Pg>/Z, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt1>.H, <Zt2>.H, <Zt3>.H }, <Pg>/Z, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt1>.S, <Zt2>.S, <Zt3>.S }, <Pg>/Z, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt1>.D, <Zt2>.D, <Zt3>.D }, <Pg>/Z, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt1>.B, <Zt2>.B, <Zt3>.B, <Zt4>.B }, <Pg>/Z, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt1>.H, <Zt2>.H, <Zt3>.H, <Zt4>.H }, <Pg>/Z, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt1>.S, <Zt2>.S, <Zt3>.S, <Zt4>.S }, <Pg>/Z, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt1>.D, <Zt2>.D, <Zt3>.D, <Zt4>.D }, <Pg>/Z, [<Xn|SP>{, #<imm>, MUL VL}]
            case IF_SVE_IS_3A: // ............iiii ...gggnnnnnttttt -- SVE load multiple structures (scalar plus immediate)
            // { <Zt1>.Q, <Zt2>.Q }, <Pg>, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt1>.Q, <Zt2>.Q, <Zt3>.Q }, <Pg>, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt1>.Q, <Zt2>.Q, <Zt3>.Q, <Zt4>.Q }, <Pg>, [<Xn|SP>{, #<imm>, MUL VL}]
            case IF_SVE_JE_3A: // ............iiii ...gggnnnnnttttt -- SVE store multiple structures (quadwords, scalar plus
            // immediate)
            // { <Zt>.B }, <Pg>, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt>.H }, <Pg>, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt>.S }, <Pg>, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt>.D }, <Pg>, [<Xn|SP>{, #<imm>, MUL VL}]
            case IF_SVE_JM_3A: // ............iiii ...gggnnnnnttttt -- SVE contiguous non-temporal store (scalar plus
            // immediate)
            // { <Zt>.D }, <Pg>, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt>.Q }, <Pg>, [<Xn|SP>{, #<imm>, MUL VL}]
            case IF_SVE_JN_3C:   // ............iiii ...gggnnnnnttttt -- SVE contiguous store (scalar plus immediate)
            case IF_SVE_JN_3C_D: // ............iiii ...gggnnnnnttttt -- SVE contiguous store (scalar plus immediate)
            // { <Zt1>.B, <Zt2>.B }, <Pg>, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt1>.H, <Zt2>.H }, <Pg>, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt1>.S, <Zt2>.S }, <Pg>, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt1>.D, <Zt2>.D }, <Pg>, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt1>.B, <Zt2>.B, <Zt3>.B }, <Pg>, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt1>.H, <Zt2>.H, <Zt3>.H }, <Pg>, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt1>.S, <Zt2>.S, <Zt3>.S }, <Pg>, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt1>.D, <Zt2>.D, <Zt3>.D }, <Pg>, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt1>.B, <Zt2>.B, <Zt3>.B, <Zt4>.B }, <Pg>, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt1>.H, <Zt2>.H, <Zt3>.H, <Zt4>.H }, <Pg>, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt1>.S, <Zt2>.S, <Zt3>.S, <Zt4>.S }, <Pg>, [<Xn|SP>{, #<imm>, MUL VL}]
            // { <Zt1>.D, <Zt2>.D, <Zt3>.D, <Zt4>.D }, <Pg>, [<Xn|SP>{, #<imm>, MUL VL}]
            case IF_SVE_JO_3A: // ............iiii ...gggnnnnnttttt -- SVE store multiple structures (scalar plus immediate)
            {
                imm = emitGetInsSC(id);
                emitDispSveConsecutiveRegList(id.idReg1(), (uint)insGetSveReg1ListSize(ins), id.idInsOpt(), true); // ttttt
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true); // ggg
                jitprintf("[");
                emitDispReg(id.idReg3(), EA_8BYTE, imm != 0); // nnnnn
                if (imm != 0)
                {
                    switch (fmt)
                    {
                        case IF_SVE_IO_3A:
                        {
                            // This does not have to be printed as hex.
                            // We only do it because the capstone disassembly displays this immediate as hex.
                            // We could not modify capstone without affecting other cases.
                            emitDispImm(emitGetInsSC(id), false, /* alwaysHex */ true); // iiii
                            break;
                        }

                        case IF_SVE_IQ_3A:
                        case IF_SVE_IS_3A:
                        case IF_SVE_JE_3A:
                        case IF_SVE_JO_3A:
                        {
                            // This does not have to be printed as hex.
                            // We only do it because the capstone disassembly displays this immediate as hex.
                            // We could not modify capstone without affecting other cases.
                            emitDispImm(emitGetInsSC(id), true, /* alwaysHex */ true); // iiii
                            jitprintf("mul vl");
                            break;
                        }

                        default:
                        {
                            emitDispImm(emitGetInsSC(id), true); // iiii
                            jitprintf("mul vl");
                            break;
                        }
                    }
                }
                jitprintf("]");
                break;
            }

            // {<Zt>.<T>}, <Pg>, [<Xn|SP>, <Xm>]
            // {<Zt>.<T>}, <Pg>, [<Xn|SP>, <Xm>, LSL #1]
            case IF_SVE_JD_4A: // .........xxmmmmm ...gggnnnnnttttt -- SVE contiguous store (scalar plus scalar)
            // {<Zt>.<T>}, <Pg>, [<Xn|SP>, <Xm>, LSL #2]
            case IF_SVE_JD_4B: // ..........xmmmmm ...gggnnnnnttttt -- SVE contiguous store (scalar plus scalar)
            // {<Zt>.D }, <Pg>, [<Xn|SP>, <Zm>.D, <mod> #3]
            // {<Zt>.S }, <Pg>, [<Xn|SP>, <Zm>.S, <mod> #1]
            // {<Zt>.S }, <Pg>, [<Xn|SP>, <Zm>.S, <mod> #2]
            case IF_SVE_JJ_4A: // ...........mmmmm .h.gggnnnnnttttt -- SVE 64-bit scatter store (scalar plus 64-bit scaled
            // offsets)
            // {<Zt>.D }, <Pg>, [<Xn|SP>, <Zm>.D, <mod>]
            // {<Zt>.D }, <Pg>, [<Xn|SP>, <Zm>.D, <mod> #1]
            // {<Zt>.D }, <Pg>, [<Xn|SP>, <Zm>.D, <mod> #2]
            case IF_SVE_JJ_4A_B: // ...........mmmmm .h.gggnnnnnttttt -- SVE 64-bit scatter store (scalar plus 64-bit scaled
            // offsets)
            // {<Zt>.D }, <Pg>, [<Xn|SP>, <Zm>.D, <mod>]
            case IF_SVE_JJ_4A_C: // ...........mmmmm .h.gggnnnnnttttt -- SVE 64-bit scatter store (scalar plus 64-bit scaled
            // offsets)
            // {<Zt>.S }, <Pg>, [<Xn|SP>, <Zm>.S, <mod>]
            case IF_SVE_JJ_4A_D: // ...........mmmmm .h.gggnnnnnttttt -- SVE 64-bit scatter store (scalar plus 64-bit scaled
            // offsets)
            // {<Zt>.D }, <Pg>, [<Xn|SP>, <Zm>.D, <mod>]
            case IF_SVE_JK_4A: // ...........mmmmm .h.gggnnnnnttttt -- SVE 64-bit scatter store (scalar plus 64-bit unscaled
            // offsets)
            // {<Zt>.S }, <Pg>, [<Xn|SP>, <Zm>.S, <mod>]
            case IF_SVE_JK_4A_B: // ...........mmmmm .h.gggnnnnnttttt -- SVE 64-bit scatter store (scalar plus 64-bit
            // unscaled offsets)
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>, <Zm>.D, <mod>]
            // {<Zt>.S }, <Pg>/Z, [<Xn|SP>, <Zm>.S, <mod> #1]
            // {<Zt>.S }, <Pg>/Z, [<Xn|SP>, <Zm>.S, <mod> #2]
            case IF_SVE_HW_4A: // .........h.mmmmm ...gggnnnnnttttt -- SVE 32-bit gather load (scalar plus 32-bit unscaled
            // offsets)
            // {<Zt>.S }, <Pg>/Z, [<Xn|SP>, <Zm>.S, <mod>]
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>, <Zm>.D, <mod> #1]
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>, <Zm>.D, <mod> #2]
            case IF_SVE_HW_4A_A: // .........h.mmmmm ...gggnnnnnttttt -- SVE 32-bit gather load (scalar plus 32-bit unscaled
            // offsets)
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>, <Zm>.D, <mod>]
            case IF_SVE_HW_4A_B: // .........h.mmmmm ...gggnnnnnttttt -- SVE 32-bit gather load (scalar plus 32-bit unscaled
            // offsets)
            // {<Zt>.S }, <Pg>/Z, [<Xn|SP>, <Zm>.S, <mod>]
            case IF_SVE_HW_4A_C: // .........h.mmmmm ...gggnnnnnttttt -- SVE 32-bit gather load (scalar plus 32-bit unscaled
            // offsets)
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>, <Zm>.D, <mod> #2]
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>, <Zm>.D, <mod> #3]
            case IF_SVE_IU_4A: // .........h.mmmmm ...gggnnnnnttttt -- SVE 64-bit gather load (scalar plus 32-bit unpacked
            // scaled offsets)
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>, <Zm>.D, <mod>]
            case IF_SVE_IU_4A_A: // .........h.mmmmm ...gggnnnnnttttt -- SVE 64-bit gather load (scalar plus 32-bit unpacked
            // scaled offsets)
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>, <Zm>.D, <mod>]
            case IF_SVE_IU_4A_C: // .........h.mmmmm ...gggnnnnnttttt -- SVE 64-bit gather load (scalar plus 32-bit unpacked
            // scaled offsets)
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>, <Zm>.D]
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>, <Zm>.D, LSL #1]
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>, <Zm>.D, LSL #2]
            case IF_SVE_HW_4B: // ...........mmmmm ...gggnnnnnttttt -- SVE 32-bit gather load (scalar plus 32-bit unscaled
            // offsets)
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>, <Zm>.D]
            case IF_SVE_HW_4B_D: // ...........mmmmm ...gggnnnnnttttt -- SVE 32-bit gather load (scalar plus 32-bit unscaled
            // offsets)
            // {<Zt>.S }, <Pg>/Z, [<Zn>.S{, <Xm>}]
            case IF_SVE_IF_4A: // ...........mmmmm ...gggnnnnnttttt -- SVE2 32-bit gather non-temporal load (vector plus
            // scalar)
            // {<Zt>.D }, <Pg>/Z, [<Zn>.D{, <Xm>}]
            case IF_SVE_IF_4A_A: // ...........mmmmm ...gggnnnnnttttt -- SVE2 32-bit gather non-temporal load (vector plus
            // scalar)
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>{, <Xm>, LSL #3}]
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>{, <Xm>, LSL #2}]
            case IF_SVE_IG_4A: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous first-fault load (scalar plus scalar)
            // {<Zt>.H }, <Pg>/Z, [<Xn|SP>{, <Xm>}]
            // {<Zt>.S }, <Pg>/Z, [<Xn|SP>{, <Xm>}]
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>{, <Xm>}]
            case IF_SVE_IG_4A_D: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous first-fault load (scalar plus
            // scalar)
            // {<Zt>.B }, <Pg>/Z, [<Xn|SP>{, <Xm>}]
            // {<Zt>.H }, <Pg>/Z, [<Xn|SP>{, <Xm>}]
            // {<Zt>.S }, <Pg>/Z, [<Xn|SP>{, <Xm>}]
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>{, <Xm>}]
            case IF_SVE_IG_4A_E: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous first-fault load (scalar plus
            // scalar)
            // {<Zt>.S }, <Pg>/Z, [<Xn|SP>{, <Xm>, LSL #1}]
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>{, <Xm>, LSL #1}]
            // {<Zt>.S }, <Pg>/Z, [<Xn|SP>{, <Xm>, LSL #2}]
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>{, <Xm>, LSL #2}]
            case IF_SVE_IG_4A_F: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous first-fault load (scalar plus
            // scalar)
            // {<Zt>.H }, <Pg>/Z, [<Xn|SP>{, <Xm>, LSL #1}]
            // {<Zt>.S }, <Pg>/Z, [<Xn|SP>{, <Xm>, LSL #1}]
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>{, <Xm>, LSL #1}]
            case IF_SVE_IG_4A_G: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous first-fault load (scalar plus
            // scalar)
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>, <Xm>, LSL #3]
            case IF_SVE_II_4A: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous load (quadwords, scalar plus scalar)
            // {<Zt>.Q }, <Pg>/Z, [<Xn|SP>, <Xm>, LSL #3]
            case IF_SVE_II_4A_B: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous load (quadwords, scalar plus scalar)
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>, <Xm>, LSL #2]
            case IF_SVE_II_4A_H: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous load (quadwords, scalar plus scalar)
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>, <Xm>, LSL #2
            case IF_SVE_IK_4A: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous load (scalar plus scalar)
            // {<Zt>.H }, <Pg>/Z, [<Xn|SP>, <Xm>]
            // {<Zt>.S }, <Pg>/Z, [<Xn|SP>, <Xm>]
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>, <Xm>]
            case IF_SVE_IK_4A_F: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous load (scalar plus scalar)
            // {<Zt>.S }, <Pg>/Z, [<Xn|SP>, <Xm>, LSL #1]
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>, <Xm>, LSL #1]
            case IF_SVE_IK_4A_G: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous load (scalar plus scalar)
            // {<Zt>.B }, <Pg>/Z, [<Xn|SP>, <Xm>]
            // {<Zt>.H }, <Pg>/Z, [<Xn|SP>, <Xm>]
            // {<Zt>.S }, <Pg>/Z, [<Xn|SP>, <Xm>]
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>, <Xm>]
            case IF_SVE_IK_4A_H: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous load (scalar plus scalar)
            // {<Zt>.H }, <Pg>/Z, [<Xn|SP>, <Xm>, LSL #1]
            // {<Zt>.S }, <Pg>/Z, [<Xn|SP>, <Xm>, LSL #1]
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>, <Xm>, LSL #1]
            case IF_SVE_IK_4A_I: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous load (scalar plus scalar)
            // {<Zt>.B }, <Pg>/Z, [<Xn|SP>, <Xm>]
            // {<Zt>.H }, <Pg>/Z, [<Xn|SP>, <Xm>]
            // {<Zt>.S }, <Pg>/Z, [<Xn|SP>, <Xm>]
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>, <Xm>]
            case IF_SVE_IN_4A: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous non-temporal load (scalar plus scalar)
            // {<Zt>.B }, <Pg>/Z, [<Xn|SP>, <Xm>]
            // {<Zt>.H }, <Pg>/Z, [<Xn|SP>, <Xm>]
            // {<Zt>.S }, <Pg>/Z, [<Xn|SP>, <Xm>]
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>, <Xm>]
            case IF_SVE_IP_4A: // ...........mmmmm ...gggnnnnnttttt -- SVE load and broadcast quadword (scalar plus scalar)
            // {<Zt1>.Q, <Zt2>.Q }, <Pg>/Z, [<Xn|SP>, <Xm>, LSL #4]
            // {<Zt1>.Q, <Zt2>.Q, <Zt3>.Q }, <Pg>/Z, [<Xn|SP>, <Xm>, LSL #4]
            // {<Zt1>.Q, <Zt2>.Q, <Zt3>.Q, <Zt4>.Q }, <Pg>/Z, [<Xn|SP>, <Xm>, LSL #4]
            case IF_SVE_IR_4A: // ...........mmmmm ...gggnnnnnttttt -- SVE load multiple structures (quadwords, scalar plus
            // scalar)
            // {<Zt1>.B, <Zt2>.B }, <Pg>/Z, [<Xn|SP>, <Xm>]
            // {<Zt1>.H, <Zt2>.H }, <Pg>/Z, [<Xn|SP>, <Xm>, LSL #1]
            // {<Zt1>.S, <Zt2>.S }, <Pg>/Z, [<Xn|SP>, <Xm>, LSL #2]
            // {<Zt1>.D, <Zt2>.D }, <Pg>/Z, [<Xn|SP>, <Xm>, LSL #3]
            // {<Zt1>.B, <Zt2>.B, <Zt3>.B }, <Pg>/Z, [<Xn|SP>, <Xm>]
            // {<Zt1>.H, <Zt2>.H, <Zt3>.H }, <Pg>/Z, [<Xn|SP>, <Xm>, LSL #1]
            // {<Zt1>.S, <Zt2>.S, <Zt3>.S }, <Pg>/Z, [<Xn|SP>, <Xm>, LSL #2]
            // {<Zt1>.D, <Zt2>.D, <Zt3>.D }, <Pg>/Z, [<Xn|SP>, <Xm>, LSL #3]
            // {<Zt1>.B, <Zt2>.B, <Zt3>.B, <Zt4>.B }, <Pg>/Z, [<Xn|SP>, <Xm>]
            // {<Zt1>.H, <Zt2>.H, <Zt3>.H, <Zt4>.H }, <Pg>/Z, [<Xn|SP>, <Xm>, LSL #1]
            // {<Zt1>.S, <Zt2>.S, <Zt3>.S, <Zt4>.S }, <Pg>/Z, [<Xn|SP>, <Xm>, LSL #2]
            // {<Zt1>.D, <Zt2>.D, <Zt3>.D, <Zt4>.D }, <Pg>/Z, [<Xn|SP>, <Xm>, LSL #3]
            case IF_SVE_IT_4A: // ...........mmmmm ...gggnnnnnttttt -- SVE load multiple structures (scalar plus scalar)
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>, <Zm>.D, LSL #2]
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>, <Zm>.D, LSL #3]
            case IF_SVE_IU_4B: // ...........mmmmm ...gggnnnnnttttt -- SVE 64-bit gather load (scalar plus 32-bit unpacked
            // scaled offsets)
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>, <Zm>.D]
            case IF_SVE_IU_4B_B: // ...........mmmmm ...gggnnnnnttttt -- SVE 64-bit gather load (scalar plus 32-bit unpacked
            // scaled offsets)
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>, <Zm>.D]
            case IF_SVE_IU_4B_D: // ...........mmmmm ...gggnnnnnttttt -- SVE 64-bit gather load (scalar plus 32-bit unpacked
            // scaled offsets)
            // {<Zt>.Q }, <Pg>/Z, [<Zn>.D{, <Xm>}]
            case IF_SVE_IW_4A: // ...........mmmmm ...gggnnnnnttttt -- SVE2 128-bit gather load (vector plus scalar)
            // {<Zt>.D }, <Pg>/Z, [<Zn>.D{, <Xm>}]
            case IF_SVE_IX_4A: // ...........mmmmm ...gggnnnnnttttt -- SVE2 64-bit gather non-temporal load (vector plus
            // scalar)
            // {<Zt>.Q }, <Pg>, [<Zn>.D{, <Xm>}]
            case IF_SVE_IY_4A: // ...........mmmmm ...gggnnnnnttttt -- SVE2 128-bit scatter store (vector plus scalar)
            // {<Zt>.S }, <Pg>, [<Zn>.S{, <Xm>}]
            case IF_SVE_IZ_4A: // ...........mmmmm ...gggnnnnnttttt -- SVE2 32-bit scatter non-temporal store (vector plus
            // scalar)
            // {<Zt>.D }, <Pg>, [<Zn>.D{, <Xm>}]
            case IF_SVE_IZ_4A_A: // ...........mmmmm ...gggnnnnnttttt -- SVE2 32-bit scatter non-temporal store (vector plus
            // scalar)
            // {<Zt>.D }, <Pg>, [<Zn>.D{, <Xm>}]
            case IF_SVE_JA_4A: // ...........mmmmm ...gggnnnnnttttt -- SVE2 64-bit scatter non-temporal store (vector plus
            // scalar)
            // {<Zt>.B }, <Pg>, [<Xn|SP>, <Xm>]
            // {<Zt>.H }, <Pg>, [<Xn|SP>, <Xm>, LSL #1]
            // {<Zt>.S }, <Pg>, [<Xn|SP>, <Xm>, LSL #2]
            // {<Zt>.D }, <Pg>, [<Xn|SP>, <Xm>, LSL #3]
            case IF_SVE_JB_4A: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous non-temporal store (scalar plus
            // scalar)
            // {<Zt1>.B, <Zt2>.B }, <Pg>, [<Xn|SP>, <Xm>]
            // {<Zt1>.H, <Zt2>.H }, <Pg>, [<Xn|SP>, <Xm>, LSL #1]
            // {<Zt1>.S, <Zt2>.S }, <Pg>, [<Xn|SP>, <Xm>, LSL #2]
            // {<Zt1>.D, <Zt2>.D }, <Pg>, [<Xn|SP>, <Xm>, LSL #3]
            // {<Zt1>.B, <Zt2>.B, <Zt3>.B }, <Pg>, [<Xn|SP>, <Xm>]
            // {<Zt1>.H, <Zt2>.H, <Zt3>.H }, <Pg>, [<Xn|SP>, <Xm>, LSL #1]
            // {<Zt1>.S, <Zt2>.S, <Zt3>.S }, <Pg>, [<Xn|SP>, <Xm>, LSL #2]
            // {<Zt1>.D, <Zt2>.D, <Zt3>.D }, <Pg>, [<Xn|SP>, <Xm>, LSL #3]
            // {<Zt1>.B, <Zt2>.B, <Zt3>.B, <Zt4>.B }, <Pg>, [<Xn|SP>, <Xm>]
            // {<Zt1>.H, <Zt2>.H, <Zt3>.H, <Zt4>.H }, <Pg>, [<Xn|SP>, <Xm>, LSL #1]
            // {<Zt1>.S, <Zt2>.S, <Zt3>.S, <Zt4>.S }, <Pg>, [<Xn|SP>, <Xm>, LSL #2]
            // {<Zt1>.D, <Zt2>.D, <Zt3>.D, <Zt4>.D }, <Pg>, [<Xn|SP>, <Xm>, LSL #3]
            case IF_SVE_JC_4A: // ...........mmmmm ...gggnnnnnttttt -- SVE store multiple structures (scalar plus scalar)
            // {<Zt>.Q }, <Pg>, [<Xn|SP>, <Xm>, LSL #2]
            // {<Zt>.D }, <Pg>, [<Xn|SP>, <Xm>, LSL #3]
            case IF_SVE_JD_4C: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous store (scalar plus scalar)
            // {<Zt>.Q }, <Pg>, [<Xn|SP>, <Xm>, LSL #3]
            case IF_SVE_JD_4C_A: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous store (scalar plus scalar)
            // {<Zt1>.Q, <Zt2>.Q }, <Pg>, [<Xn|SP>, <Xm>, LSL #4]
            // {<Zt1>.Q, <Zt2>.Q, <Zt3>.Q }, <Pg>, [<Xn|SP>, <Xm>, LSL #4]
            // {<Zt1>.Q, <Zt2>.Q, <Zt3>.Q, <Zt4>.Q }, <Pg>, [<Xn|SP>, <Xm>, LSL #4]
            case IF_SVE_JF_4A: // ...........mmmmm ...gggnnnnnttttt -- SVE store multiple structures (quadwords, scalar plus
            // scalar)
            // {<Zt>.D }, <Pg>, [<Xn|SP>, <Zm>.D, LSL #1]
            // {<Zt>.D }, <Pg>, [<Xn|SP>, <Zm>.D, LSL #2]
            // {<Zt>.D }, <Pg>, [<Xn|SP>, <Zm>.D, LSL #3]
            case IF_SVE_JJ_4B: // ...........mmmmm ...gggnnnnnttttt -- SVE 64-bit scatter store (scalar plus 64-bit scaled
            // offsets)
            // {<Zt>.D }, <Pg>, [<Xn|SP>, <Zm>.D]
            case IF_SVE_JJ_4B_C: // ...........mmmmm ...gggnnnnnttttt -- SVE 64-bit scatter store (scalar plus 64-bit scaled
            // offsets)
            // {<Zt>.D }, <Pg>, [<Xn|SP>, <Zm>.D]
            case IF_SVE_JJ_4B_E: // ...........mmmmm ...gggnnnnnttttt -- SVE 64-bit scatter store (scalar plus 64-bit scaled
            // offsets)
            // {<Zt>.D }, <Pg>, [<Xn|SP>, <Zm>.D]
            case IF_SVE_JK_4B: // ...........mmmmm ...gggnnnnnttttt -- SVE 64-bit scatter store (scalar plus 64-bit unscaled
            {
                // offsets)
                emitDispSveConsecutiveRegList(id.idReg1(), (uint)insGetSveReg1ListSize(ins), id.idInsOpt(), true); // ttttt
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true); // ggg
                emitDispSveModAddr(ins, id.idReg3(), id.idReg4(), id.idInsOpt(), fmt); // nnnnn
                // mmmmm
                break;
            }

            // {<Zt>.<T>}, <Pg>, [<Xn|SP>{, #<imm>, MUL VL}]
            case IF_SVE_JN_3A: // .........xx.iiii ...gggnnnnnttttt -- SVE contiguous store (scalar plus immediate)
            {
                imm = emitGetInsSC(id);
                emitDispSveConsecutiveRegList(id.idReg1(), (uint)insGetSveReg1ListSize(ins), id.idInsOpt(), true); // ttttt
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true); // ggg
                emitDispSveImmMulVl(id.idReg3(), imm);
                break;
            }

            // {<Zt>.<T>}, <Pg>, [<Xn|SP>{, #<imm>, MUL VL}]
            case IF_SVE_JN_3B: // ..........x.iiii ...gggnnnnnttttt -- SVE contiguous store (scalar plus immediate)
            {
                imm = emitGetInsSC(id);
                emitDispSveConsecutiveRegList(id.idReg1(), (uint)insGetSveReg1ListSize(ins), id.idInsOpt(), true); // ttttt
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true); // ggg
                emitDispSveImmMulVl(id.idReg3(), imm);
                break;
            }

            // <Pt>, [<Xn|SP>{, #<imm>, MUL VL}]
            case IF_SVE_ID_2A: // ..........iiiiii ...iiinnnnn.TTTT -- SVE load predicate register
            // <Pt>, [<Xn|SP>{, #<imm>, MUL VL}]
            case IF_SVE_JG_2A: // ..........iiiiii ...iiinnnnn.TTTT -- SVE store predicate register
            {
                imm = emitGetInsSC(id);
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt), id.idInsOpt(), true); // TTTT
                emitDispSveImmMulVl(id.idReg2(), imm);
                break;
            }

            // <Zt>, [<Xn|SP>{, #<imm>, MUL VL}]
            case IF_SVE_IE_2A: // ..........iiiiii ...iiinnnnnttttt -- SVE load vector register
            // <Zt>, [<Xn|SP>{, #<imm>, MUL VL}]
            case IF_SVE_JH_2A: // ..........iiiiii ...iiinnnnnttttt -- SVE store vector register
            {
                imm = emitGetInsSC(id);
                emitDispReg(id.idReg1(), EA_SCALABLE, true); // ttttt
                emitDispSveImmMulVl(id.idReg2(), imm);
                break;
            }

            // <Zdn>.<T>, <Pg>/M, <Zdn>.<T>, <Zm>.<T>, <const>
            case IF_SVE_GP_3A: // ........xx.....r ...gggmmmmmddddd -- SVE floating-point complex add (predicated)
            {
                imm = emitGetInsSC(id);
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true);
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true);
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true);
                emitDispSveReg(id.idReg3(), id.idInsOpt(), true);
                emitDispImm(emitDecodeRotationImm90_or_270(imm), false);
                break;
            }

            // <Zda>.<T>, <Pg>/M, <Zn>.<T>, <Zm>.<T>, <const>
            case IF_SVE_GT_4A: // ........xx.mmmmm .rrgggnnnnnddddd -- SVE floating-point complex multiply-add (predicated)
            {
                imm = emitGetInsSC(id);
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true);
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true);
                emitDispSveReg(id.idReg3(), id.idInsOpt(), true);
                emitDispSveReg(id.idReg4(), id.idInsOpt(), true);
                emitDispImm(emitDecodeRotationImm0_to_270(imm), false);
                break;
            }

            // <Pd>.<T>, <Pg>/Z, <Zn>.<T>, #0.0
            case IF_SVE_HI_3A: // ........xx...... ...gggnnnnn.DDDD -- SVE floating-point compare with zero
            {
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt, 1), id.idInsOpt(), true);
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt, 2), id.idInsOpt(), true);
                emitDispSveReg(id.idReg3(), id.idInsOpt(), true);
                emitDispFloatZero();
                break;
            }

            // <Zdn>.<T>, <Pg>/M, <Zdn>.<T>, <const>
            case IF_SVE_HM_2A: // ........xx...... ...ggg....iddddd -- SVE floating-point arithmetic with immediate
            {
                // (predicated)
                imm = emitGetInsSC(id);
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true);
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true);
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true);
                emitDispSmallFloatImm(imm, id.idIns());
                break;
            }

            // <Zdn>.<T>, <Zdn>.<T>, <Zm>.<T>, #<imm>
            case IF_SVE_HN_2A: // ........xx...iii ......mmmmmddddd -- SVE floating-point trig multiply-add coefficient
            case IF_SVE_AW_2A: // ........xx.xxiii ......mmmmmddddd -- sve_int_rotate_imm
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true);
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true);
                emitDispSveReg(id.idReg2(), id.idInsOpt(), true);
                emitDispImm(emitGetInsSC(id), false);
                break;
            }

            // <Zd>.<T>, <Pg>/M, <Zn>.<T>
            case IF_SVE_HP_3A: // .............xx. ...gggnnnnnddddd -- SVE floating-point convert to integer
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true);
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true);
                emitDispSveReg(id.idReg3(), id.idInsOpt(), false);
                break;
            }

            // <Zda>.H, <Pg>/M, <Zn>.H, <Zm>.H
            case IF_SVE_HU_4B: // ...........mmmmm ...gggnnnnnddddd -- SVE floating-point multiply-accumulate writing addend
            // <Zdn>.<T>, <Pg>/M, <Zm>.<T>, <Za>.<T>
            case IF_SVE_HV_4A: // ........xx.aaaaa ...gggmmmmmddddd -- SVE floating-point multiply-accumulate writing
            {
                // multiplicand
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true);
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true);
                emitDispSveReg(id.idReg3(), id.idInsOpt(), true);
                emitDispSveReg(id.idReg4(), id.idInsOpt(), false);
                break;
            }

            // <Zd>.B, { <Zn>.B }, <Zm>[<index>]
            case IF_SVE_GG_3A: // ........ii.mmmmm ......nnnnnddddd -- SVE2 lookup table with 2-bit indices and 16-bit
            // element size
            // <Zd>.B, { <Zn>.B }, <Zm>[<index>]
            case IF_SVE_GH_3A: // ........i..mmmmm ......nnnnnddddd -- SVE2 lookup table with 4-bit indices and 16-bit
            // element size
            // <Zd>.H, { <Zn>.H }, <Zm>[<index>]
            case IF_SVE_GG_3B: // ........ii.mmmmm ...i..nnnnnddddd -- SVE2 lookup table with 2-bit indices and 16-bit
            // element size
            // <Zd>.H, { <Zn1>.H, <Zn2>.H }, <Zm>[<index>]
            case IF_SVE_GH_3B: // ........ii.mmmmm ......nnnnnddddd -- SVE2 lookup table with 4-bit indices and 16-bit
            // element size
            // <Zd>.H, {<Zn>.H }, <Zm>[<index>]
            case IF_SVE_GH_3B_B: // ........ii.mmmmm ......nnnnnddddd -- SVE2 lookup table with 4-bit indices and 16-bit
            {
                // element size
                imm = emitGetInsSC(id);
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true);
                emitDispSveConsecutiveRegList(id.idReg1(), 1, id.idInsOpt(), true);
                emitDispSveReg(id.idReg2(), id.idInsOpt(), false);
                emitDispElementIndex(imm, false);
                break;
            }

            // <prfop>, <Pg>, [<Xn|SP>, <Zm>.S, <mod>]
            // <prfop>, <Pg>, [<Xn|SP>, <Zm>.S, <mod> #1]
            // <prfop>, <Pg>, [<Xn|SP>, <Zm>.S, <mod> #2]
            // <prfop>, <Pg>, [<Xn|SP>, <Zm>.S, <mod> #3]
            case IF_SVE_HY_3A: // .........h.mmmmm ...gggnnnnn.oooo -- SVE 32-bit gather prefetch (scalar plus 32-bit scaled
            // offsets)
            // <prfop>, <Pg>, [<Xn|SP>, <Zm>.D, <mod>]
            // <prfop>, <Pg>, [<Xn|SP>, <Zm>.D, <mod> #1]
            // <prfop>, <Pg>, [<Xn|SP>, <Zm>.D, <mod> #2]
            // <prfop>, <Pg>, [<Xn|SP>, <Zm>.D, <mod> #3]
            case IF_SVE_HY_3A_A: // .........h.mmmmm ...gggnnnnn.oooo -- SVE 32-bit gather prefetch (scalar plus 32-bit
            // scaled offsets)
            // <prfop>, <Pg>, [<Xn|SP>, <Zm>.D]
            // <prfop>, <Pg>, [<Xn|SP>, <Zm>.D, LSL #1]
            // <prfop>, <Pg>, [<Xn|SP>, <Zm>.D, LSL #2]
            // <prfop>, <Pg>, [<Xn|SP>, <Zm>.D, LSL #3]
            case IF_SVE_HY_3B: // ...........mmmmm ...gggnnnnn.oooo -- SVE 32-bit gather prefetch (scalar plus 32-bit scaled
            // offsets)
            // <prfop>, <Pg>, [<Xn|SP>, <Xm>]
            // <prfop>, <Pg>, [<Xn|SP>, <Xm>, LSL #1]
            // <prfop>, <Pg>, [<Xn|SP>, <Xm>, LSL #2]
            // <prfop>, <Pg>, [<Xn|SP>, <Xm>, LSL #3]
            case IF_SVE_IB_3A: // ...........mmmmm ...gggnnnnn.oooo -- SVE contiguous prefetch (scalar plus scalar)
            {
                emitDispSvePrfop(id.idSvePrfop(), true);
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt), id.idInsOpt(), true);
                emitDispSveModAddr(ins, id.idReg2(), id.idReg3(), id.idInsOpt(), fmt);
                break;
            }

            // <prfop>, <Pg>, [<Zn>.S{, #<imm>}]
            // <prfop>, <Pg>, [<Zn>.D{, #<imm>}]
            case IF_SVE_HZ_2A_B: // ...........iiiii ...gggnnnnn.oooo -- SVE 32-bit gather prefetch (vector plus immediate)
            {
                imm = emitGetInsSC(id);
                emitDispSvePrfop(id.idSvePrfop(), true);
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt), id.idInsOpt(), true);
                emitDispSveImm(id.idReg2(), imm, id.idInsOpt());
                break;
            }

            // <prfop>, <Pg>, [<Xn|SP>{, #<imm>, MUL VL}]
            case IF_SVE_IA_2A: // ..........iiiiii ...gggnnnnn.oooo -- SVE contiguous prefetch (scalar plus immediate)
            {
                imm = emitGetInsSC(id);
                emitDispSvePrfop(id.idSvePrfop(), true);
                emitDispPredicateReg(id.idReg1(), insGetPredicateType(fmt), id.idInsOpt(), true);
                emitDispSveImmMulVl(id.idReg2(), imm);
                break;
            }

            // {<Zt>.S }, <Pg>/Z, [<Zn>.S{, #<imm>}]
            // {<Zt>.D }, <Pg>/Z, [<Zn>.D{, #<imm>}]
            case IF_SVE_HX_3A_B: // ...........iiiii ...gggnnnnnttttt -- SVE 32-bit gather load (vector plus immediate)
            // {<Zt>.S }, <Pg>/Z, [<Zn>.S{, #<imm>}]
            // {<Zt>.D }, <Pg>/Z, [<Zn>.D{, #<imm>}]
            case IF_SVE_HX_3A_E: // ...........iiiii ...gggnnnnnttttt -- SVE 32-bit gather load (vector plus immediate)
            // {<Zt>.D }, <Pg>/Z, [<Zn>.D{, #<imm>}]
            case IF_SVE_IV_3A: // ...........iiiii ...gggnnnnnttttt -- SVE 64-bit gather load (vector plus immediate)
            // {<Zt>.S }, <Pg>, [<Zn>.S{, #<imm>}]
            // {<Zt>.D }, <Pg>, [<Zn>.D{, #<imm>}]
            case IF_SVE_JI_3A_A: // ...........iiiii ...gggnnnnnttttt -- SVE 32-bit scatter store (vector plus immediate)
            // {<Zt>.D }, <Pg>, [<Zn>.D{, #<imm>}]
            case IF_SVE_JL_3A: // ...........iiiii ...gggnnnnnttttt -- SVE 64-bit scatter store (vector plus immediate)
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>{, #<imm>}]
            case IF_SVE_IC_3A: // ..........iiiiii ...gggnnnnnttttt -- SVE load and broadcast element
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>{, #<imm>}]
            case IF_SVE_IC_3A_A: // ..........iiiiii ...gggnnnnnttttt -- SVE load and broadcast element
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>{, #<imm>}]
            case IF_SVE_IC_3A_B: // ..........iiiiii ...gggnnnnnttttt -- SVE load and broadcast element
            // {<Zt>.D }, <Pg>/Z, [<Xn|SP>{, #<imm>}]
            case IF_SVE_IC_3A_C: // ..........iiiiii ...gggnnnnnttttt -- SVE load and broadcast element
            {
                imm = emitGetInsSC(id);
                emitDispSveConsecutiveRegList(id.idReg1(), (uint)insGetSveReg1ListSize(id.idIns()), id.idInsOpt(), true);
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true);
                emitDispSveImmIndex(id.idReg3(), id.idInsOpt(), imm);
                break;
            }

            // <Zd>, <Zn>
            case IF_SVE_BI_2A: // ................ ......nnnnnddddd -- SVE constructive prefix (unpredicated)
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true);
                emitDispSveReg(id.idReg2(), id.idInsOpt(), false);
                break;
            }

            // <Zd>.<T>, <R><n|SP>
            case IF_SVE_CB_2A: // ........xx...... ......nnnnnddddd -- SVE broadcast general register
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true);
                emitDispReg(encodingZRtoSP(id.idReg2()), size, false);
                break;
            }

            // <Zd>.H, <Zn>.B
            case IF_SVE_HH_2A: // ................ ......nnnnnddddd -- SVE2 FP8 upconverts
            // <Zd>.<T>, <Zn>.<Tb>
            case IF_SVE_CH_2A: // ........xx...... ......nnnnnddddd -- SVE unpack vector elements
            {
                emitDispSveReg(id.idReg1(), (insOpts)(id.idInsOpt() + 1), true);
                emitDispSveReg(id.idReg2(), id.idInsOpt(), false);
                break;
            }

            // <Zd>.<T>, <Zn>.<T>
            case IF_SVE_BJ_2A: // ........xx...... ......nnnnnddddd -- SVE floating-point exponential accelerator
            // <Zd>.<T>, <Zn>.<T>
            case IF_SVE_CG_2A: // ........xx...... ......nnnnnddddd -- SVE reverse vector elements
            // <Zd>.<T>, <Zn>.<T>
            case IF_SVE_HF_2A: // ........xx...... ......nnnnnddddd -- SVE floating-point reciprocal estimate (unpredicated)
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true);
                emitDispSveReg(id.idReg2(), id.idInsOpt(), false);
                break;
            }

            // <Zd>.<T>, <Zn>.<T>, #<const>
            case IF_SVE_BF_2A: // ........xx.xxiii ......nnnnnddddd -- SVE bitwise shift by immediate (unpredicated)
            // <Zd>.<T>, <Zn>.<T>, #<const>
            case IF_SVE_FT_2A: // ........xx.xxiii ......nnnnnddddd -- SVE2 bitwise shift and insert
            // <Zda>.<T>, <Zn>.<T>, #<const>
            case IF_SVE_FU_2A: // ........xx.xxiii ......nnnnnddddd -- SVE2 bitwise shift right and accumulate
            {
                imm = emitGetInsSC(id);
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true);
                emitDispSveReg(id.idReg2(), id.idInsOpt(), true);
                emitDispImm(imm, false);
                break;
            }

            // <Zd>.<T>, <Pg>/Z, #<imm>{, <shift>}
            // <Zd>.<T>, <Pg>/M, #<imm>{, <shift>}
            case IF_SVE_BV_2A:   // ........xx..gggg ..hiiiiiiiiddddd -- SVE copy integer immediate (predicated)
            case IF_SVE_BV_2A_J: // ........xx..gggg ..hiiiiiiiiddddd -- SVE copy integer immediate (predicated)
            {
                imm = emitGetInsSC(id);
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true); // gggg
                emitDispImmOptsLSL(imm, id.idHasShift(), 8); // iiiiiiii, h
                break;
            }

            // <Zd>.<T>, <Pg>/M, #<imm>
            case IF_SVE_BV_2B: // ........xx..gggg ...........ddddd -- SVE copy integer immediate (predicated)
            {
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                emitDispPredicateReg(id.idReg2(), insGetPredicateType(fmt), id.idInsOpt(), true); // gggg
                emitDispImm(0, false);
                break;
            }

            // <Zd>.<T>, <Zn>.<T>[<imm>]
            // <Zd>.<T>, <V><n>
            case IF_SVE_BW_2A: // ........ii.xxxxx ......nnnnnddddd -- SVE broadcast indexed element
            {
                imm = emitGetInsSC(id);
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true); // ddddd
                if (imm > 0)
                {
                    emitDispSveReg(id.idReg2(), id.idInsOpt(), false); // nnnnn
                    emitDispElementIndex(imm, false);
                }
                else
                {
                    assert(imm == 0);
                    emitDispReg(id.idReg2(), optGetSveElemsize(id.idInsOpt()), false);
                }
                break;
            }

            // <Zd>.<T>, <Zn>.<T>[<imm>]
            case IF_SVE_BX_2A: // ...........ixxxx ......nnnnnddddd -- sve_int_perm_dupq_i
            {
                imm = emitGetInsSC(id);
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true);
                emitDispSveReg(id.idReg2(), id.idInsOpt(), false);
                emitDispElementIndex(imm, false);
                break;
            }

            // <Zdn>.B, <Zdn>.B, <Zm>.B, #<imm>
            case IF_SVE_BY_2A: // ............iiii ......mmmmmddddd -- sve_int_perm_extq
            {
                imm = emitGetInsSC(id);
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true);
                emitDispSveReg(id.idReg1(), id.idInsOpt(), true);
                emitDispSveReg(id.idReg2(), id.idInsOpt(), true);
                emitDispImm(imm, false);
                break;
            }

            default:
            {
                jitprintf($"unexpected format {emitIfName(id.idInsFmt())}");
                assert(false, "!\"unexpectedFormat\"");
                break;
            }
        }
    }

#if DEBUG || LATE_DISASM
    private const float PERFSCORE_THROUGHPUT_9X = 1.0f / 9.0f;
    private const float PERFSCORE_THROUGHPUT_11C = 11.0f;
    private const float PERFSCORE_THROUGHPUT_14C = 14.0f;

    // SVE latencies follow the Arm Neoverse N2 Software Optimization Guide,
    // Issue 5.0, Revision r0p3. Preserve its undocumented/placeholder scores.
    private void getInsSveExecutionCharacteristics(instrDesc id, ref insExecutionCharacteristics result)
    {
        var ins = id.idIns();
        switch (id.idInsFmt())
        {
            case IF_SVE_AA_3A: // ........xx...... ...gggmmmmmddddd
            {
                switch (ins)
                {
                    case INS_sve_add:
                    case INS_sve_sub:
                    case INS_sve_subr:
                    case INS_sve_sabd:
                    case INS_sve_smax:
                    case INS_sve_smin:
                    case INS_sve_uabd:
                    case INS_sve_umax:
                    case INS_sve_umin:
                    case INS_sve_shadd:
                    case INS_sve_shsub:
                    case INS_sve_shsubr:
                    case INS_sve_srhadd:
                    case INS_sve_uhadd:
                    case INS_sve_uhsub:
                    case INS_sve_uhsubr:
                    case INS_sve_urhadd:
                    case INS_sve_addp:
                    case INS_sve_smaxp:
                    case INS_sve_sminp:
                    case INS_sve_umaxp:
                    case INS_sve_uminp:
                    case INS_sve_sqadd:
                    case INS_sve_sqsub:
                    case INS_sve_uqadd:
                    case INS_sve_uqsub:
                    case INS_sve_sqsubr:
                    case INS_sve_suqadd:
                    case INS_sve_uqsubr:
                    case INS_sve_usqadd:
                    {
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        break;
                    }

                    case INS_sve_mul:
                    case INS_sve_smulh:
                    case INS_sve_umulh:
                    case INS_sve_sqrshl:
                    case INS_sve_sqrshlr:
                    case INS_sve_sqshl:
                    case INS_sve_sqshlr:
                    case INS_sve_srshl:
                    case INS_sve_srshlr:
                    case INS_sve_uqrshl:
                    case INS_sve_uqrshlr:
                    case INS_sve_uqshl:
                    case INS_sve_uqshlr:
                    case INS_sve_urshl:
                    case INS_sve_urshlr:
                    {
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        break;
                    }

                    case INS_sve_asrr:
                    case INS_sve_lslr:
                    case INS_sve_lsrr:
                    case INS_sve_asr:
                    case INS_sve_lsl:
                    case INS_sve_lsr:
                    {
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        break;
                    }

                    default:
                    {
                        result.insLatency = PERFSCORE_LATENCY_1C;
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        break;
                    }
                }
                break;
            }

            // Divides, 32 bit (Note: worse for 64 bit)
            case IF_SVE_AC_3A: // ........xx...... ...gggmmmmmddddd -- SVE integer divide vectors (predicated)
            {
                result.insLatency = PERFSCORE_LATENCY_12C; // 7 to 12
                result.insThroughput = PERFSCORE_THROUGHPUT_11C; // 1/11 to 1/7
                break;
            }

            // Reduction, logical
            case IF_SVE_AF_3A: // ........xx...... ...gggnnnnnddddd -- SVE bitwise logical reduction (predicated)
            {
                result.insLatency = PERFSCORE_LATENCY_6C;
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                break;
            }

            case IF_SVE_AH_3A: // ........xx.....M ...gggnnnnnddddd -- SVE constructive prefix (predicated)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            // Reduction, arithmetic, D form (worse for B, S and H)
            case IF_SVE_AI_3A: // ........xx...... ...gggnnnnnddddd -- SVE integer add reduction (predicated)
            // Reduction, arithmetic, D form (worse for B, S and H)
            case IF_SVE_AK_3A: // ........xx...... ...gggnnnnnddddd -- SVE integer min/max reduction (predicated)
            {
                result.insLatency = PERFSCORE_LATENCY_4C;
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                break;
            }

            case IF_SVE_AM_2A: // ........xx...... ...gggxxiiiddddd -- SVE bitwise shift by immediate (predicated)
            {
                switch (ins)
                {
                    case INS_sve_asr:
                    case INS_sve_lsl:
                    case INS_sve_lsr:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        break;
                    }

                    case INS_sve_srshr:
                    case INS_sve_sqshl:
                    case INS_sve_urshr:
                    case INS_sve_sqshlu:
                    case INS_sve_uqshl:
                    case INS_sve_asrd:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            // Arithmetic, shift
            case IF_SVE_AO_3A: // ........xx...... ...gggmmmmmddddd -- SVE bitwise shift by wide elements (predicated)
            {
                result.insLatency = PERFSCORE_LATENCY_2C;
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                break;
            }

            // Count/reverse bits
            // Arithmetic, basic
            // Floating point absolute value/difference
            // Floating point arithmetic
            // Logical
            case IF_SVE_AP_3A: // ........xx...... ...gggnnnnnddddd -- SVE bitwise unary operations (predicated)
            {
                result.insLatency = PERFSCORE_LATENCY_2C;
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                break;
            }

            case IF_SVE_AQ_3A:
            {
                switch (ins)
                {
                    // Arithmetic, basic
                    case INS_sve_abs:
                    case INS_sve_neg:
                    {
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        break;
                    }

                    // Extend, sign or zero
                    case INS_sve_sxtb:
                    case INS_sve_sxth:
                    case INS_sve_sxtw:
                    case INS_sve_uxtb:
                    case INS_sve_uxth:
                    case INS_sve_uxtw:
                    {
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_AR_4A: // ........xx.mmmmm ...gggnnnnnddddd -- SVE integer multiply-accumulate writing addend
            // (predicated)
            case IF_SVE_AS_4A: // ........xx.mmmmm ...gggaaaaaddddd -- SVE integer multiply-add writing multiplicand
            // (predicated)
            case IF_SVE_FD_3A: // .........i.iimmm ......nnnnnddddd -- SVE2 integer multiply (indexed)
            case IF_SVE_FD_3B: // ...........iimmm ......nnnnnddddd -- SVE2 integer multiply (indexed)
            case IF_SVE_FD_3C: // ...........immmm ......nnnnnddddd -- SVE2 integer multiply (indexed)
            case IF_SVE_FF_3A: // .........i.iimmm ......nnnnnddddd -- SVE2 integer multiply-add (indexed)
            case IF_SVE_FF_3B: // ...........iimmm ......nnnnnddddd -- SVE2 integer multiply-add (indexed)
            case IF_SVE_FF_3C: // ...........immmm ......nnnnnddddd -- SVE2 integer multiply-add (indexed)
            case IF_SVE_FI_3A: // .........i.iimmm ......nnnnnddddd -- SVE2 saturating multiply high (indexed)
            case IF_SVE_FI_3B: // ...........iimmm ......nnnnnddddd -- SVE2 saturating multiply high (indexed)
            case IF_SVE_FI_3C: // ...........immmm ......nnnnnddddd -- SVE2 saturating multiply high (indexed)
            case IF_SVE_FK_3A: // .........i.iimmm ......nnnnnddddd -- SVE2 saturating multiply-add high (indexed)
            case IF_SVE_FK_3B: // ...........iimmm ......nnnnnddddd -- SVE2 saturating multiply-add high (indexed)
            case IF_SVE_FK_3C: // ...........immmm ......nnnnnddddd -- SVE2 saturating multiply-add high (indexed)
            case IF_SVE_EM_3A: // ........xx.mmmmm ......nnnnnddddd -- SVE2 saturating multiply-add high
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_5C;
                break;
            }

            case IF_SVE_GU_3A: // ...........iimmm ......nnnnnddddd -- SVE floating-point multiply-add (indexed)
            case IF_SVE_GU_3B: // ...........immmm ......nnnnnddddd -- SVE floating-point multiply-add (indexed)
            case IF_SVE_GN_3A: // ...........mmmmm ......nnnnnddddd -- SVE2 FP8 multiply-add long
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                result.insLatency = PERFSCORE_LATENCY_4C;
                break;
            }

            case IF_SVE_GX_3A: // ...........iimmm ......nnnnnddddd -- SVE floating-point multiply (indexed)
            case IF_SVE_GX_3B: // ...........immmm ......nnnnnddddd -- SVE floating-point multiply (indexed)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                result.insLatency = PERFSCORE_LATENCY_3C;
                break;
            }

            case IF_SVE_GY_3B: // ...........iimmm ......nnnnnddddd -- SVE BFloat16 floating-point dot product (indexed)
            {
                switch (ins)
                {
                    case INS_sve_fdot:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_bfdot:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_HA_3A: // ...........mmmmm ......nnnnnddddd -- SVE BFloat16 floating-point dot product
            {
                switch (ins)
                {
                    case INS_sve_fdot:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_bfdot:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_HB_3A: // ...........mmmmm ......nnnnnddddd -- SVE floating-point multiply-add long
            {
                switch (ins)
                {
                    case INS_sve_fmlalb:
                    case INS_sve_fmlalt:
                    case INS_sve_fmlslb:
                    case INS_sve_fmlslt:
                    case INS_sve_bfmlalb:
                    case INS_sve_bfmlalt:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_sve_bfmlslb:
                    case INS_sve_bfmlslt:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_AV_3A: // ...........mmmmm ......kkkkkddddd -- SVE2 bitwise ternary operations
            {
                switch (ins)
                {
                    case INS_sve_eor3:
                    case INS_sve_bcax:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        break;
                    }

                    case INS_sve_bsl:
                    case INS_sve_bsl1n:
                    case INS_sve_bsl2n:
                    case INS_sve_nbsl:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_GU_3C:   // .........i.iimmm ......nnnnnddddd -- SVE floating-point multiply-add (indexed)
            case IF_SVE_GX_3C:   // .........i.iimmm ......nnnnnddddd -- SVE floating-point multiply (indexed)
            case IF_SVE_EW_3A:   // ...........mmmmm ......nnnnnddddd -- SVE2 multiply-add (checked pointer)
            case IF_SVE_EW_3B:   // ...........mmmmm ......aaaaaddddd -- SVE2 multiply-add (checked pointer)
            case IF_SVE_EX_3A:   // ........xx.mmmmm ......nnnnnddddd -- SVE permute vector elements (quadwords)
            case IF_SVE_AT_3B:   // ...........mmmmm ......nnnnnddddd -- SVE integer add/subtract vectors (unpredicated)
            case IF_SVE_AB_3B:   // ................ ...gggmmmmmddddd -- SVE integer add/subtract vectors (predicated)
            case IF_SVE_HL_3B:   // ................ ...gggmmmmmddddd -- SVE floating-point arithmetic (predicated)
            case IF_SVE_GO_3A:   // ...........mmmmm ......nnnnnddddd -- SVE2 FP8 multiply-add long long
            case IF_SVE_GW_3B:   // ...........mmmmm ......nnnnnddddd -- SVE FP clamp
            case IF_SVE_HA_3A_E: // ...........mmmmm ......nnnnnddddd -- SVE BFloat16 floating-point dot product
            case IF_SVE_HA_3A_F: // ...........mmmmm ......nnnnnddddd -- SVE BFloat16 floating-point dot product
            case IF_SVE_HD_3A_A: // ...........mmmmm ......nnnnnddddd -- SVE floating point matrix multiply accumulate
            case IF_SVE_HK_3B:   // ...........mmmmm ......nnnnnddddd -- SVE floating-point arithmetic (unpredicated)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                break;
            }

            case IF_SVE_AT_3A: // ........xx.mmmmm ......nnnnnddddd
            {
                switch (ins)
                {
                    case INS_sve_tbxq:
                    case INS_sve_sclamp:
                    case INS_sve_uclamp:
                    case INS_sve_fclamp:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_bext:
                    case INS_sve_bdep:
                    case INS_sve_bgrp:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_6C;
                        break;
                    }

                    case INS_sve_ftssel:
                    case INS_sve_fmul:
                    case INS_sve_ftsmul:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                        result.insLatency = PERFSCORE_LATENCY_3C;
                        break;
                    }

                    case INS_sve_frecps:
                    case INS_sve_frsqrts:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_sve_mul:
                    case INS_sve_smulh:
                    case INS_sve_umulh:
                    case INS_sve_sqdmulh:
                    case INS_sve_sqrdmulh:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_5C;
                        break;
                    }

                    default:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        break;
                    }
                }
                break;
            }

            case IF_SVE_FL_3A: // ........xx.mmmmm ......nnnnnddddd
            {
                switch (ins)
                {
                    case INS_sve_smullb:
                    case INS_sve_smullt:
                    case INS_sve_umullb:
                    case INS_sve_umullt:
                    case INS_sve_sqdmullb:
                    case INS_sve_sqdmullt:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_sve_pmullb:
                    case INS_sve_pmullt:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        break;
                    }

                    default:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        break;
                    }
                }
                break;
            }

            case IF_SVE_BR_3B:   // ...........mmmmm ......nnnnnddddd -- SVE permute vector segments
            case IF_SVE_BZ_3A:   // ........xx.mmmmm ......nnnnnddddd -- SVE table lookup (three sources)
            case IF_SVE_BZ_3A_A: // ........xx.mmmmm ......nnnnnddddd -- SVE table lookup (three sources)
            case IF_SVE_FM_3A:   // ........xx.mmmmm ......nnnnnddddd -- SVE2 integer add/subtract wide
            case IF_SVE_GC_3A:   // ........xx.mmmmm ......nnnnnddddd -- SVE2 integer add/subtract narrow high part
            case IF_SVE_GF_3A:   // ........xx.mmmmm ......nnnnnddddd -- SVE2 histogram generation (segment)
            case IF_SVE_AU_3A:   // ...........mmmmm ......nnnnnddddd -- SVE bitwise logical operations (unpredicated)
            case IF_SVE_GI_4A:   // ........xx.mmmmm ...gggnnnnnddddd -- SVE2 histogram generation (vector)
            case IF_SVE_BB_2A:   // ...........nnnnn .....iiiiiiddddd -- SVE stack frame adjustment
            case IF_SVE_BC_1A:   // ................ .....iiiiiiddddd -- SVE stack frame size
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_SVE_BA_3A: // ........xx.mmmmm ......nnnnnddddd -- SVE index generation (register start, register
            // increment)
            case IF_SVE_AX_1A: // ........xx.iiiii ......iiiiiddddd -- SVE index generation (immediate start, immediate
            // increment)
            case IF_SVE_AY_2A: // ........xx.mmmmm ......iiiiiddddd -- SVE index generation (immediate start, register
            // increment)
            case IF_SVE_AZ_2A: // ........xx.iiiii ......nnnnnddddd -- SVE index generation (register start, immediate
            {
                // increment)
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_8C;
                break;
            }

            case IF_SVE_BH_3A:   // .........x.mmmmm ....hhnnnnnddddd -- SVE address generation
            case IF_SVE_BH_3B:   // ...........mmmmm ....hhnnnnnddddd -- SVE address generation
            case IF_SVE_BH_3B_A: // ...........mmmmm ....hhnnnnnddddd -- SVE address generation
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_SVE_BL_1A: // ............iiii ......pppppddddd -- SVE element count
            case IF_SVE_BM_1A: // ............iiii ......pppppddddd -- SVE inc/dec register by element count
            case IF_SVE_BN_1A: // ............iiii ......pppppddddd -- SVE inc/dec vector by element count
            case IF_SVE_BO_1A: // ...........Xiiii ......pppppddddd -- SVE saturating inc/dec register by element count
            case IF_SVE_BP_1A: // ............iiii ......pppppddddd -- SVE saturating inc/dec vector by element count
            case IF_SVE_BQ_2A: // ...........iiiii ...iiinnnnnddddd -- SVE extract vector (immediate offset, destructive)
            case IF_SVE_BQ_2B: // ...........iiiii ...iiimmmmmddddd -- SVE extract vector (immediate offset, destructive)
            case IF_SVE_BU_2A: // ........xx..gggg ...iiiiiiiiddddd -- SVE copy floating-point immediate (predicated)
            case IF_SVE_BS_1A: // ..............ii iiiiiiiiiiiddddd -- SVE bitwise logical with immediate (unpredicated)
            case IF_SVE_BT_1A: // ..............ii iiiiiiiiiiiddddd -- SVE broadcast bitmask immediate
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_SVE_FO_3A: // ...........mmmmm ......nnnnnddddd -- SVE integer matrix multiply accumulate
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                result.insLatency = PERFSCORE_LATENCY_3C;
                break;
            }

            case IF_SVE_BG_3A: // ........xx.mmmmm ......nnnnnddddd -- SVE bitwise shift by wide elements (unpredicated)
            case IF_SVE_FN_3B: // ...........mmmmm ......nnnnnddddd -- SVE2 integer multiply long
            case IF_SVE_BD_3B: // ...........mmmmm ......nnnnnddddd -- SVE2 integer multiply vectors (unpredicated)
            case IF_SVE_AW_2A: // ........xx.xxiii ......mmmmmddddd -- sve_int_rotate_imm
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_SVE_BV_2A:   // ........xx..gggg ..hiiiiiiiiddddd -- SVE copy integer immediate (predicated)
            case IF_SVE_BV_2A_J: // ........xx..gggg ..hiiiiiiiiddddd -- SVE copy integer immediate (predicated)
            case IF_SVE_BV_2B:   // ........xx..gggg ...........ddddd -- SVE copy integer immediate (predicated)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_SVE_BW_2A: // ........ii.xxxxx ......nnnnnddddd -- SVE broadcast indexed element
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_3C;
                break;
            }

            case IF_SVE_CE_2A: // ................ ......nnnnn.DDDD -- SVE move predicate from vector
            case IF_SVE_CE_2B: // .........i...ii. ......nnnnn.DDDD -- SVE move predicate from vector
            case IF_SVE_CE_2C: // ..............i. ......nnnnn.DDDD -- SVE move predicate from vector
            case IF_SVE_CE_2D: // .............ii. ......nnnnn.DDDD -- SVE move predicate from vector
            case IF_SVE_CF_2A: // ................ .......NNNNddddd -- SVE move predicate into vector
            case IF_SVE_CF_2B: // .........i...ii. .......NNNNddddd -- SVE move predicate into vector
            case IF_SVE_CF_2C: // ..............i. .......NNNNddddd -- SVE move predicate into vector
            case IF_SVE_CF_2D: // .............ii. .......NNNNddddd -- SVE move predicate into vector
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_140C; // @ToDo currently undocumented
                result.insLatency = PERFSCORE_LATENCY_140C;
                break;
            }

            case IF_SVE_CC_2A: // ........xx...... ......mmmmmddddd -- SVE insert SIMD&FP scalar register
            case IF_SVE_CD_2A: // ........xx...... ......mmmmmddddd -- SVE insert general register
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_5C;
                break;
            }

            case IF_SVE_CI_3A: // ........xx..MMMM .......NNNN.DDDD -- SVE permute predicate elements
            case IF_SVE_CJ_2A: // ........xx...... .......NNNN.DDDD -- SVE reverse predicate elements
            case IF_SVE_CK_2A: // ................ .......NNNN.DDDD -- SVE unpack predicate elements
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            // Conditional extract operations, SIMD&FP scalar and vector forms
            case IF_SVE_CL_3A: // ........xx...... ...gggnnnnnddddd -- SVE compress active elements
            case IF_SVE_CM_3A: // ........xx...... ...gggmmmmmddddd -- SVE conditionally broadcast element to vector
            case IF_SVE_CN_3A: // ........xx...... ...gggmmmmmddddd -- SVE conditionally extract element to SIMD&FP scalar
            {
                result.insLatency = PERFSCORE_LATENCY_3C;
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                break;
            }

            // Conditional extract operations, scalar form
            case IF_SVE_CO_3A: // ........xx...... ...gggmmmmmddddd -- SVE conditionally extract element to general register
            {
                result.insLatency = PERFSCORE_LATENCY_8C;
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                break;
            }

            // Copy, scalar SIMD&FP or imm
            case IF_SVE_CP_3A: // ........xx...... ...gggnnnnnddddd -- SVE copy SIMD&FP scalar register to vector
            {
                // (predicated)
                result.insLatency = PERFSCORE_LATENCY_2C;
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                break;
            }

            // Copy, scalar
            case IF_SVE_CQ_3A: // ........xx...... ...gggnnnnnddddd -- SVE copy general register to vector (predicated)
            {
                result.insLatency = PERFSCORE_LATENCY_5C;
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                break;
            }

            case IF_SVE_CT_3A: // ................ ...gggnnnnnddddd -- SVE reverse doublewords
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_140C; // @ToDo Currently undocumented.
                result.insLatency = PERFSCORE_LATENCY_140C;
                break;
            }

            case IF_SVE_CV_3A: // ........xx...... ...VVVnnnnnddddd -- SVE vector splice (constructive)
            case IF_SVE_CV_3B: // ........xx...... ...VVVmmmmmddddd -- SVE vector splice (destructive)
            {
                result.insLatency = PERFSCORE_LATENCY_3C;
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                break;
            }

            case IF_SVE_CW_4A: // ........xx.mmmmm ..VVVVnnnnnddddd -- SVE select vector elements (predicated)
            {
                result.insLatency = PERFSCORE_LATENCY_2C;
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                break;
            }

            case IF_SVE_CX_4A:   // ........xx.mmmmm ...gggnnnnn.DDDD -- SVE integer compare vectors
            case IF_SVE_CX_4A_A: // ........xx.mmmmm ...gggnnnnn.DDDD -- SVE integer compare vectors
            case IF_SVE_CY_3A:   // ........xx.iiiii ...gggnnnnn.DDDD -- SVE integer compare with signed immediate
            case IF_SVE_CY_3B:   // ........xx.iiiii ii.gggnnnnn.DDDD -- SVE integer compare with uint immediate
            case IF_SVE_EG_3A:   // ...........iimmm ......nnnnnddddd -- SVE two-way dot product (indexed)
            case IF_SVE_EY_3A:   // ...........iimmm ......nnnnnddddd -- SVE integer dot product (indexed)
            case IF_SVE_EY_3B:   // ...........immmm ......nnnnnddddd -- SVE integer dot product (indexed)
            case IF_SVE_FE_3A:   // ...........iimmm ....i.nnnnnddddd -- SVE2 integer multiply long (indexed)
            case IF_SVE_FE_3B:   // ...........immmm ....i.nnnnnddddd -- SVE2 integer multiply long (indexed)
            case IF_SVE_FG_3A:   // ...........iimmm ....i.nnnnnddddd -- SVE2 integer multiply-add long (indexed)
            case IF_SVE_FG_3B:   // ...........immmm ....i.nnnnnddddd -- SVE2 integer multiply-add long (indexed)
            case IF_SVE_FH_3A:   // ...........iimmm ....i.nnnnnddddd -- SVE2 saturating multiply (indexed)
            case IF_SVE_FH_3B:   // ...........immmm ....i.nnnnnddddd -- SVE2 saturating multiply (indexed)
            case IF_SVE_FJ_3A:   // ...........iimmm ....i.nnnnnddddd -- SVE2 saturating multiply-add (indexed)
            case IF_SVE_FJ_3B:   // ...........immmm ....i.nnnnnddddd -- SVE2 saturating multiply-add (indexed)
            case IF_SVE_EH_3A:   // ........xx.mmmmm ......nnnnnddddd -- SVE integer dot product (unpredicated)
            case IF_SVE_EL_3A:   // ........xx.mmmmm ......nnnnnddddd
            case IF_SVE_FW_3A:   // ........xx.mmmmm ......nnnnnddddd -- SVE2 integer absolute difference and accumulate
            {
                result.insLatency = PERFSCORE_LATENCY_4C;
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                break;
            }

            case IF_SVE_GJ_3A: // ...........mmmmm ......nnnnnddddd -- SVE2 crypto constructive binary operations
            {
                switch (ins)
                {
                    case INS_sve_rax1:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        break;
                    }

                    case INS_sve_sm4ekey:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_GZ_3A: // ...........iimmm ....i.nnnnnddddd -- SVE floating-point multiply-add long (indexed)
            {
                switch (ins)
                {
                    case INS_sve_fmlalb:
                    case INS_sve_fmlalt:
                    case INS_sve_fmlslb:
                    case INS_sve_fmlslt:
                    case INS_sve_bfmlalb:
                    case INS_sve_bfmlalt:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_sve_bfmlslb:
                    case INS_sve_bfmlslt:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_EZ_3A: // ...........iimmm ......nnnnnddddd -- SVE mixed sign dot product (indexed)
            {
                result.insLatency = PERFSCORE_LATENCY_3C;
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                break;
            }

            case IF_SVE_CZ_4A:   // ............MMMM ..gggg.NNNN.DDDD -- SVE predicate logical operations
            case IF_SVE_CZ_4A_A: // ............MMMM ..gggg.NNNN.DDDD -- SVE predicate logical operations
            case IF_SVE_CZ_4A_K: // ............MMMM ..gggg.NNNN.DDDD -- SVE predicate logical operations
            case IF_SVE_CZ_4A_L: // ............MMMM ..gggg.NNNN.DDDD -- SVE predicate logical operations
            {
                switch (ins)
                {
                    case INS_sve_mov:
                    case INS_sve_and:
                    case INS_sve_orr:
                    case INS_sve_eor:
                    case INS_sve_bic:
                    case INS_sve_orn:
                    case INS_sve_not:
                    case INS_sve_sel:
                    {
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                        break;
                    }

                    case INS_sve_bics:
                    case INS_sve_eors:
                    case INS_sve_nots:
                    case INS_sve_ands:
                    case INS_sve_orrs:
                    case INS_sve_orns:
                    case INS_sve_nors:
                    case INS_sve_nands:
                    {
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        break;
                    }

                    case INS_sve_nor:
                    case INS_sve_nand:
                    {
                        result.insLatency = PERFSCORE_LATENCY_1C;
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        break;
                    }

                    case INS_sve_movs:
                    {
                        result.insLatency = PERFSCORE_LATENCY_1C;
                        result.insThroughput = PERFSCORE_THROUGHPUT_3C;
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_DA_4A: // ............MMMM ..gggg.NNNN.DDDD -- SVE propagate break from previous partition
            case IF_SVE_DC_3A: // ................ ..gggg.NNNN.MMMM -- SVE propagate break to next partition
            {
                switch (ins)
                {
                    case INS_sve_brkpa:
                    case INS_sve_brkpb:
                    case INS_sve_brkn:
                    {
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        break;
                    }

                    case INS_sve_brkpas:
                    case INS_sve_brkpbs:
                    case INS_sve_brkns:
                    {
                        result.insLatency = PERFSCORE_LATENCY_3C;
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_DB_3A: // ................ ..gggg.NNNNMDDDD -- SVE partition break condition
            {
                result.insLatency = PERFSCORE_LATENCY_2C;
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                break;
            }

            case IF_SVE_DB_3B: // ................ ..gggg.NNNN.DDDD -- SVE partition break condition
            {
                result.insLatency = PERFSCORE_LATENCY_3C;
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                break;
            }

            case IF_SVE_DD_2A: // ................ .......gggg.DDDD -- SVE predicate first active
            {
                result.insLatency = PERFSCORE_LATENCY_3C;
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                break;
            }

            case IF_SVE_DE_1A: // ........xx...... ......ppppp.DDDD -- SVE predicate initialize
            {
                switch (ins)
                {
                    case INS_sve_ptrue:
                    {
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                        break;
                    }

                    case INS_sve_ptrues:
                    {
                        result.insLatency = PERFSCORE_LATENCY_3C;
                        result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_DF_2A: // ........xx...... .......VVVV.DDDD -- SVE predicate next active
            {
                result.insLatency = PERFSCORE_LATENCY_3C;
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                break;
            }

            case IF_SVE_DG_2A: // ................ .......gggg.DDDD -- SVE predicate read from FFR (predicated)
            {
                switch (ins)
                {
                    case INS_sve_rdffr:
                    {
                        result.insLatency = PERFSCORE_LATENCY_3C;
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        break;
                    }

                    case INS_sve_rdffrs:
                    {
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_DH_1A: // ................ ............DDDD -- SVE predicate read from FFR (unpredicated)
            {
                result.insLatency = PERFSCORE_LATENCY_3C;
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                break;
            }

            case IF_SVE_DJ_1A: // ................ ............DDDD -- SVE predicate zero
            {
                result.insLatency = PERFSCORE_LATENCY_2C;
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                break;
            }

            case IF_SVE_DI_2A: // ................ ..gggg.NNNN..... -- SVE predicate test
            {
                result.insLatency = PERFSCORE_LATENCY_1C;
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                break;
            }

            case IF_SVE_DK_3A: // ........xx...... ..gggg.NNNNddddd -- SVE predicate count
            {
                result.insLatency = PERFSCORE_LATENCY_2C;
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                break;
            }

            case IF_SVE_GE_4A: // ........xx.mmmmm ...gggnnnnn.DDDD -- SVE2 character match
            case IF_SVE_HT_4A: // ........xx.mmmmm ...gggnnnnn.DDDD -- SVE floating-point compare vectors
            {
                result.insLatency = PERFSCORE_LATENCY_2C;
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                break;
            }

            // Extract/insert operation, SIMD and FP scalar form
            case IF_SVE_CR_3A: // ........xx...... ...gggnnnnnddddd -- SVE extract element to SIMD&FP scalar register
            {
                result.insLatency = PERFSCORE_LATENCY_3C;
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                break;
            }

            // Extract/insert operation, scalar
            case IF_SVE_CS_3A: // ........xx...... ...gggnnnnnddddd -- SVE extract element to general register
            {
                result.insLatency = PERFSCORE_LATENCY_5C;
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                break;
            }

            // Count/reverse bits
            // Reverse, vector
            case IF_SVE_CU_3A: // ........xx...... ...gggnnnnnddddd -- SVE reverse within elements
            {
                result.insLatency = PERFSCORE_LATENCY_2C;
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                break;
            }

            case IF_SVE_ES_3A: // ........xx...... ...gggnnnnnddddd -- SVE2 integer unary operations (predicated)
            {
                switch (ins)
                {
                    // Arithmetic, complex
                    case INS_sve_sqabs:
                    case INS_sve_sqneg:
                    {
                        // Reciprocal estimate
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                        break;
                    }

                    // Reciprocal estimate
                    case INS_sve_urecpe:
                    case INS_sve_ursqrte:
                    {
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            // Arithmetic, pairwise add and accum long
            case IF_SVE_EQ_3A: // ........xx...... ...gggnnnnnddddd -- SVE2 integer pairwise add and accumulate long
            case IF_SVE_EF_3A: // ...........mmmmm ......nnnnnddddd -- SVE two-way dot product
            {
                result.insLatency = PERFSCORE_LATENCY_4C;
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                break;
            }

            case IF_SVE_GQ_3A: // ................ ...gggnnnnnddddd -- SVE floating-point convert precision odd elements
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_3C;
                break;
            }

            // Floating point arithmetic
            // Floating point min/max pairwise
            case IF_SVE_GR_3A: // ........xx...... ...gggmmmmmddddd -- SVE2 floating-point pairwise operations
            {
                result.insLatency = PERFSCORE_LATENCY_2C;
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                break;
            }

            // Floating point reduction, F64. (Note: Worse for F32 and F16)
            case IF_SVE_HE_3A: // ........xx...... ...gggnnnnnddddd -- SVE floating-point recursive reduction
            {
                result.insLatency = PERFSCORE_LATENCY_2C;
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                break;
            }

            // Floating point associative add, F64. (Note: Worse for F32 and F16)
            case IF_SVE_HJ_3A: // ........xx...... ...gggmmmmmddddd -- SVE floating-point serial reduction (predicated)
            {
                result.insLatency = PERFSCORE_LATENCY_4C;
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                break;
            }

            case IF_SVE_HL_3A: // ........xx...... ...gggmmmmmddddd -- SVE floating-point arithmetic (predicated)
            {
                switch (ins)
                {
                    // Floating point absolute value/difference
                    case INS_sve_fabd:
                    // Floating point min/max
                    case INS_sve_fmax:
                    case INS_sve_fmaxnm:
                    case INS_sve_fmin:
                    case INS_sve_fminnm:
                    // Floating point arithmetic
                    case INS_sve_fadd:
                    case INS_sve_fsub:
                    case INS_sve_fsubr:
                    {
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        break;
                    }

                    // Floating point divide, F64 (Note: Worse for F32, F16)
                    case INS_sve_fdiv:
                    case INS_sve_fdivr:
                    {
                        result.insLatency = PERFSCORE_LATENCY_15C; // 7 to 15
                        result.insThroughput = PERFSCORE_THROUGHPUT_14C; // 1/14 to 1/7
                        break;
                    }

                    // Floating point multiply
                    case INS_sve_fmul:
                    case INS_sve_fmulx:
                    case INS_sve_fscale:
                    {
                        result.insLatency = PERFSCORE_LATENCY_3C;
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        break;
                    }

                    case INS_sve_famax:
                    case INS_sve_famin:
                    {
                        result.insLatency = PERFSCORE_LATENCY_20C; // TODO-SVE: Placeholder
                        result.insThroughput = PERFSCORE_THROUGHPUT_25C; // TODO-SVE: Placeholder
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_HO_3A: // ................ ...gggnnnnnddddd -- SVE floating-point convert precision
            case IF_SVE_HO_3B:
            case IF_SVE_HO_3C:
            case IF_SVE_HP_3B: // ................ ...gggnnnnnddddd -- SVE floating-point convert to integer
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_3C;
                break;
            }

            // Floating point round to integral, F64. (Note: Worse for F32 and F16)
            case IF_SVE_HQ_3A: // ........xx...... ...gggnnnnnddddd -- SVE floating-point round to integral value
            {
                result.insLatency = PERFSCORE_LATENCY_3C;
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                break;
            }

            case IF_SVE_HR_3A: // ........xx...... ...gggnnnnnddddd -- SVE floating-point unary operations
            {
                switch (ins)
                {
                    // Floating point reciprocal estimate, F64. (Note: Worse for F32 and F16)
                    case INS_sve_frecpx:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_3C;
                        result.insLatency = PERFSCORE_LATENCY_1C;
                        break;
                    }

                    // Floating point square root F64. (Note: Worse for F32 and F16)
                    case INS_sve_fsqrt:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_16C;
                        result.insLatency = PERFSCORE_LATENCY_14C;
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_HS_3A: // ................ ...gggnnnnnddddd -- SVE integer convert to floating-point
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_4X;
                result.insLatency = PERFSCORE_LATENCY_6C;
                break;
            }

            case IF_SVE_DL_2A: // ........xx...... .....l.NNNNddddd -- SVE predicate count (predicate-as-counter)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_SVE_DM_2A: // ........xx...... .......MMMMddddd -- SVE inc/dec register by predicate count
            case IF_SVE_DN_2A: // ........xx...... .......MMMMddddd -- SVE inc/dec vector by predicate count
            case IF_SVE_DP_2A: // ........xx...... .......MMMMddddd -- SVE saturating inc/dec vector by predicate count
            case IF_SVE_DO_2A: // ........xx...... .....X.MMMMddddd -- SVE saturating inc/dec register by predicate count
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_7C;
                break;
            }

            case IF_SVE_DQ_0A: // ................ ................ -- SVE FFR initialise
            case IF_SVE_DR_1A: // ................ .......NNNN..... -- SVE FFR write from predicate
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_SVE_DW_2A: // ........xx...... ......iiNNN.DDDD -- SVE extract mask predicate from predicate-as-counter
            case IF_SVE_DW_2B: // ........xx...... .......iNNN.DDDD -- SVE extract mask predicate from predicate-as-counter
            case IF_SVE_DS_2A: // .........x.mmmmm ......nnnnn..... -- SVE conditionally terminate scalars
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_1C;
                break;
            }

            case IF_SVE_DV_4A:   // ........ix.xxxvv ..NNNN.MMMM.DDDD -- SVE broadcast predicate element
            case IF_SVE_FZ_2A:   // ................ ......nnnn.ddddd -- SME2 multi-vec extract narrow
            case IF_SVE_GY_3A:   // ...........iimmm ....i.nnnnnddddd -- SVE BFloat16 floating-point dot product (indexed)
            case IF_SVE_GY_3B_D: // ...........iimmm ......nnnnnddddd -- SVE BFloat16 floating-point dot product (indexed)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                break;
            }

            case IF_SVE_HG_2A: // ................ ......nnnn.ddddd -- SVE2 FP8 downconverts
            {
                switch (ins)
                {
                    case INS_sve_fcvtnt:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_3C;
                        break;
                    }

                    case INS_sve_fcvtn:
                    case INS_sve_bfcvtn:
                    case INS_sve_fcvtnb:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            // Not available in Arm Neoverse N2 Software Optimization Guide.
            case IF_SVE_AG_3A: // ........xx...... ...gggnnnnnddddd -- SVE bitwise logical reduction (quadwords)
            case IF_SVE_AJ_3A: // ........xx...... ...gggnnnnnddddd -- SVE integer add reduction (quadwords)
            case IF_SVE_AL_3A: // ........xx...... ...gggnnnnnddddd -- SVE integer min/max reduction (quadwords)
            case IF_SVE_GS_3A: // ........xx...... ...gggnnnnnddddd -- SVE floating-point recursive reduction (quadwords)
            {
                result.insLatency = PERFSCORE_LATENCY_20C; // TODO-SVE: Placeholder
                result.insThroughput = PERFSCORE_THROUGHPUT_25C; // TODO-SVE: Placeholder
                break;
            }

            // Not available in Arm Neoverse N2 Software Optimization Guide.
            case IF_SVE_GA_2A: // ............iiii ......nnnn.ddddd -- SME2 multi-vec shift narrow
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_25C; // TODO-SVE: Placeholder
                result.insLatency = PERFSCORE_LATENCY_20C; // TODO-SVE: Placeholder
                break;
            }

            case IF_SVE_GD_2A: // .........x.xx... ......nnnnnddddd -- SVE2 saturating extract narrow
            case IF_SVE_FA_3A: // ...........iimmm ....rrnnnnnddddd -- SVE2 complex integer dot product (indexed)
            case IF_SVE_FA_3B: // ...........immmm ....rrnnnnnddddd -- SVE2 complex integer dot product (indexed)
            case IF_SVE_EJ_3A: // ........xx.mmmmm ....rrnnnnnddddd -- SVE2 complex integer dot product
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_4C;
                break;
            }

            case IF_SVE_GK_2A: // ................ ......mmmmmddddd -- SVE2 crypto destructive binary operations
            case IF_SVE_GL_1A: // ................ ...........ddddd -- SVE2 crypto unary operations
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_SVE_DT_3A: // ........xx.mmmmm ...X..nnnnn.DDDD -- SVE integer compare scalar count and limit
            case IF_SVE_DX_3A: // ........xx.mmmmm ......nnnnn.DDD. -- SVE integer compare scalar count and limit (predicate
            // pair)
            case IF_SVE_DY_3A: // ........xx.mmmmm ..l...nnnnn..DDD -- SVE integer compare scalar count and limit
            // (predicate-as-counter)
            case IF_SVE_DU_3A: // ........xx.mmmmm ......nnnnn.DDDD -- SVE pointer conflict compare
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_3C;
                break;
            }

            case IF_SVE_DZ_1A: // ........xx...... .............DDD -- sve_int_pn_ptrue
            case IF_SVE_EA_1A: // ........xx...... ...iiiiiiiiddddd -- SVE broadcast floating-point immediate (unpredicated)
            case IF_SVE_EB_1A: // ........xx...... ..hiiiiiiiiddddd -- SVE broadcast integer immediate (unpredicated)
            case IF_SVE_EC_1A: // ........xx...... ..hiiiiiiiiddddd -- SVE integer add/subtract immediate (unpredicated)
            case IF_SVE_EB_1B: // ........xx...... ...........ddddd -- SVE broadcast integer immediate (unpredicated)
            case IF_SVE_FV_2A: // ........xx...... .....rmmmmmddddd -- SVE2 complex integer add
            case IF_SVE_FY_3A: // .........x.mmmmm ......nnnnnddddd -- SVE2 integer add/subtract long with carry
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_SVE_ED_1A: // ........xx...... ...iiiiiiiiddddd -- SVE integer min/max immediate (unpredicated)
            {
                switch (ins)
                {
                    case INS_sve_umin:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    default:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        break;
                    }
                }
                break;
            }

            case IF_SVE_EE_1A: // ........xx...... ...iiiiiiiiddddd -- SVE integer multiply immediate (unpredicated)
            case IF_SVE_FB_3A: // ...........iimmm ....rrnnnnnddddd -- SVE2 complex integer multiply-add (indexed)
            case IF_SVE_FB_3B: // ...........immmm ....rrnnnnnddddd -- SVE2 complex integer multiply-add (indexed)
            case IF_SVE_FC_3A: // ...........iimmm ....rrnnnnnddddd -- SVE2 complex saturating multiply-add (indexed)
            case IF_SVE_FC_3B: // ...........immmm ....rrnnnnnddddd -- SVE2 complex saturating multiply-add (indexed)
            case IF_SVE_EK_3A: // ........xx.mmmmm ....rrnnnnnddddd -- SVE2 complex integer multiply-add
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_5C;
                break;
            }

            case IF_SVE_IH_3A:   // ............iiii ...gggnnnnnttttt -- SVE contiguous load (quadwords, scalar plus
            // immediate)
            case IF_SVE_IH_3A_A: // ............iiii ...gggnnnnnttttt -- SVE contiguous load (quadwords, scalar plus
            // immediate)
            case IF_SVE_IH_3A_F: // ............iiii ...gggnnnnnttttt -- SVE contiguous load (quadwords, scalar plus
            // immediate)
            case IF_SVE_IJ_3A:   // ............iiii ...gggnnnnnttttt -- SVE contiguous load (scalar plus immediate)
            case IF_SVE_IJ_3A_D: // ............iiii ...gggnnnnnttttt -- SVE contiguous load (scalar plus immediate)
            case IF_SVE_IJ_3A_E: // ............iiii ...gggnnnnnttttt -- SVE contiguous load (scalar plus immediate)
            case IF_SVE_IJ_3A_F: // ............iiii ...gggnnnnnttttt -- SVE contiguous load (scalar plus immediate)
            case IF_SVE_IJ_3A_G: // ............iiii ...gggnnnnnttttt -- SVE contiguous load (scalar plus immediate)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_9C;
                break;
            }

            case IF_SVE_IL_3A: // ............iiii ...gggnnnnnttttt -- SVE contiguous non-fault load (scalar plus immediate)
            case IF_SVE_IL_3A_A: // ............iiii ...gggnnnnnttttt -- SVE contiguous non-fault load (scalar plus
            // immediate)
            case IF_SVE_IL_3A_B: // ............iiii ...gggnnnnnttttt -- SVE contiguous non-fault load (scalar plus
            // immediate)
            case IF_SVE_IL_3A_C: // ............iiii ...gggnnnnnttttt -- SVE contiguous non-fault load (scalar plus
            {
                // immediate)
                result.insThroughput = PERFSCORE_THROUGHPUT_3C;
                result.insLatency = PERFSCORE_LATENCY_6C;
                break;
            }

            case IF_SVE_IM_3A: // ............iiii ...gggnnnnnttttt -- SVE contiguous non-temporal load (scalar plus
            {
                // immediate)
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_10C;
                break;
            }

            case IF_SVE_IO_3A: // ............iiii ...gggnnnnnttttt -- SVE load and broadcast quadword (scalar plus
            {
                // immediate)
                switch (ins)
                {
                    case INS_sve_ld1rqb:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_3C;
                        result.insLatency = PERFSCORE_LATENCY_6C;
                        break;
                    }

                    case INS_sve_ld1rob:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_ld1rqh:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_3C;
                        result.insLatency = PERFSCORE_LATENCY_6C;
                        break;
                    }

                    case INS_sve_ld1roh:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_ld1rqw:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_3C;
                        result.insLatency = PERFSCORE_LATENCY_6C;
                        break;
                    }

                    case INS_sve_ld1row:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_ld1rqd:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_3C;
                        result.insLatency = PERFSCORE_LATENCY_6C;
                        break;
                    }

                    case INS_sve_ld1rod:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_IQ_3A: // ............iiii ...gggnnnnnttttt -- SVE load multiple structures (quadwords, scalar plus
            {
                // immediate)
                switch (ins)
                {
                    case INS_sve_ld2q:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_ld3q:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_ld4q:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_IS_3A: // ............iiii ...gggnnnnnttttt -- SVE load multiple structures (scalar plus immediate)
            {
                switch (ins)
                {
                    case INS_sve_ld2b:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_9C;
                        break;
                    }

                    case INS_sve_ld3b:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_3C;
                        result.insLatency = PERFSCORE_LATENCY_10C;
                        break;
                    }

                    case INS_sve_ld4b:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_10C;
                        break;
                    }

                    case INS_sve_ld2h:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_9C;
                        break;
                    }

                    case INS_sve_ld3h:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_3C;
                        result.insLatency = PERFSCORE_LATENCY_10C;
                        break;
                    }

                    case INS_sve_ld4h:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_10C;
                        break;
                    }

                    case INS_sve_ld2w:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_9C;
                        break;
                    }

                    case INS_sve_ld3w:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_3C;
                        result.insLatency = PERFSCORE_LATENCY_10C;
                        break;
                    }

                    case INS_sve_ld4w:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_10C;
                        break;
                    }

                    case INS_sve_ld2d:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_9C;
                        break;
                    }

                    case INS_sve_ld3d:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_3C;
                        result.insLatency = PERFSCORE_LATENCY_10C;
                        break;
                    }

                    case INS_sve_ld4d:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_10C;
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_JE_3A: // ............iiii ...gggnnnnnttttt -- SVE store multiple structures (quadwords, scalar plus
            {
                // immediate)
                switch (ins)
                {
                    case INS_sve_st2q:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_st3q:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_st4q:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_FR_2A:   // .........x.xxiii ......nnnnnddddd -- SVE2 bitwise shift left long
            case IF_SVE_JM_3A:   // ............iiii ...gggnnnnnttttt -- SVE contiguous non-temporal store (scalar plus
            // immediate)
            case IF_SVE_JN_3C:   // ............iiii ...gggnnnnnttttt -- SVE contiguous store (scalar plus immediate)
            case IF_SVE_JN_3C_D: // ............iiii ...gggnnnnnttttt -- SVE contiguous store (scalar plus immediate)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_SVE_GB_2A: // .........x.xxiii ......nnnnnddddd -- SVE2 bitwise shift right narrow
            {
                switch (ins)
                {
                    case INS_sve_sqshrunb:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_sve_sqshrunt:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_sve_sqrshrunb:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_sve_sqrshrunt:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_sve_shrnb:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        break;
                    }

                    case INS_sve_shrnt:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        break;
                    }

                    case INS_sve_rshrnb:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_sve_rshrnt:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_sve_sqshrnb:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_sve_sqshrnt:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_sve_sqrshrnb:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_sve_sqrshrnt:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_sve_uqshrnb:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_sve_uqshrnt:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_sve_uqrshrnb:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_sve_uqrshrnt:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_JO_3A: // ............iiii ...gggnnnnnttttt -- SVE store multiple structures (scalar plus immediate)
            {
                switch (ins)
                {
                    case INS_sve_st2b:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_sve_st3b:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_9C;
                        result.insLatency = PERFSCORE_LATENCY_7C;
                        break;
                    }

                    case INS_sve_st4b:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_9C;
                        result.insLatency = PERFSCORE_LATENCY_11C;
                        break;
                    }

                    case INS_sve_st2h:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_sve_st3h:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_9C;
                        result.insLatency = PERFSCORE_LATENCY_7C;
                        break;
                    }

                    case INS_sve_st4h:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_9C;
                        result.insLatency = PERFSCORE_LATENCY_11C;
                        break;
                    }

                    case INS_sve_st2w:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_sve_st3w:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_9C;
                        result.insLatency = PERFSCORE_LATENCY_7C;
                        break;
                    }

                    case INS_sve_st4w:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_9C;
                        result.insLatency = PERFSCORE_LATENCY_11C;
                        break;
                    }

                    case INS_sve_st2d:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_sve_st3d:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_9C;
                        result.insLatency = PERFSCORE_LATENCY_7C;
                        break;
                    }

                    case INS_sve_st4d:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_9C;
                        result.insLatency = PERFSCORE_LATENCY_11C;
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_JD_4A:   // .........xxmmmmm ...gggnnnnnttttt -- SVE contiguous store (scalar plus scalar)
            case IF_SVE_JD_4B:   // ..........xmmmmm ...gggnnnnnttttt -- SVE contiguous store (scalar plus scalar)
            case IF_SVE_JJ_4A:   // ...........mmmmm .h.gggnnnnnttttt -- SVE 64-bit scatter store (scalar plus 64-bit scaled
            // offsets)
            case IF_SVE_JJ_4A_B: // ...........mmmmm .h.gggnnnnnttttt -- SVE 64-bit scatter store (scalar plus 64-bit scaled
            // offsets)
            case IF_SVE_JJ_4A_C: // ...........mmmmm .h.gggnnnnnttttt -- SVE 64-bit scatter store (scalar plus 64-bit scaled
            // offsets)
            case IF_SVE_JJ_4A_D: // ...........mmmmm .h.gggnnnnnttttt -- SVE 64-bit scatter store (scalar plus 64-bit scaled
            // offsets)
            case IF_SVE_JK_4A: // ...........mmmmm .h.gggnnnnnttttt -- SVE 64-bit scatter store (scalar plus 64-bit unscaled
            // offsets)
            case IF_SVE_JK_4A_B: // ...........mmmmm .h.gggnnnnnttttt -- SVE 64-bit scatter store (scalar plus 64-bit
            // unscaled offsets)
            case IF_SVE_JN_3A:   // .........xx.iiii ...gggnnnnnttttt -- SVE contiguous store (scalar plus immediate)
            case IF_SVE_JN_3B:   // ..........x.iiii ...gggnnnnnttttt -- SVE contiguous store (scalar plus immediate)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_SVE_HW_4A:   // .........h.mmmmm ...gggnnnnnttttt -- SVE 32-bit gather load (scalar plus 32-bit unscaled
            // offsets)
            case IF_SVE_HW_4A_A: // .........h.mmmmm ...gggnnnnnttttt -- SVE 32-bit gather load (scalar plus 32-bit unscaled
            // offsets)
            case IF_SVE_HW_4A_B: // .........h.mmmmm ...gggnnnnnttttt -- SVE 32-bit gather load (scalar plus 32-bit unscaled
            // offsets)
            case IF_SVE_HW_4A_C: // .........h.mmmmm ...gggnnnnnttttt -- SVE 32-bit gather load (scalar plus 32-bit unscaled
            // offsets)
            case IF_SVE_IU_4A:   // .........h.mmmmm ...gggnnnnnttttt -- SVE 64-bit gather load (scalar plus 32-bit unpacked
            // scaled offsets)
            case IF_SVE_IU_4A_A: // .........h.mmmmm ...gggnnnnnttttt -- SVE 64-bit gather load (scalar plus 32-bit unpacked
            // scaled offsets)
            case IF_SVE_IU_4A_C: // .........h.mmmmm ...gggnnnnnttttt -- SVE 64-bit gather load (scalar plus 32-bit unpacked
            // scaled offsets)
            case IF_SVE_HW_4B:   // ...........mmmmm ...gggnnnnnttttt -- SVE 32-bit gather load (scalar plus 32-bit unscaled
            // offsets)
            case IF_SVE_HW_4B_D: // ...........mmmmm ...gggnnnnnttttt -- SVE 32-bit gather load (scalar plus 32-bit unscaled
            {
                // offsets)
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_9C;
                break;
            }

            case IF_SVE_IF_4A:   // ...........mmmmm ...gggnnnnnttttt -- SVE2 32-bit gather non-temporal load (vector plus
            // scalar)
            case IF_SVE_IF_4A_A: // ...........mmmmm ...gggnnnnnttttt -- SVE2 32-bit gather non-temporal load (vector plus
            {
                // scalar)
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_10C;
                break;
            }

            case IF_SVE_IG_4A: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous first-fault load (scalar plus scalar)
            case IF_SVE_IG_4A_D: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous first-fault load (scalar plus
            // scalar)
            case IF_SVE_IG_4A_E: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous first-fault load (scalar plus
            // scalar)
            case IF_SVE_IG_4A_F: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous first-fault load (scalar plus
            // scalar)
            case IF_SVE_IG_4A_G: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous first-fault load (scalar plus
            // scalar)
            case IF_SVE_II_4A:   // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous load (quadwords, scalar plus scalar)
            case IF_SVE_II_4A_B: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous load (quadwords, scalar plus scalar)
            case IF_SVE_II_4A_H: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous load (quadwords, scalar plus scalar)
            case IF_SVE_IK_4A:   // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous load (scalar plus scalar)
            case IF_SVE_IK_4A_F: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous load (scalar plus scalar)
            case IF_SVE_IK_4A_G: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous load (scalar plus scalar)
            case IF_SVE_IK_4A_H: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous load (scalar plus scalar)
            case IF_SVE_IK_4A_I: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous load (scalar plus scalar)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_9C;
                break;
            }

            case IF_SVE_IN_4A: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous non-temporal load (scalar plus scalar)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_10C;
                break;
            }

            case IF_SVE_IP_4A: // ...........mmmmm ...gggnnnnnttttt -- SVE load and broadcast quadword (scalar plus scalar)
            {
                switch (ins)
                {
                    case INS_sve_ld1rqb:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_3C;
                        result.insLatency = PERFSCORE_LATENCY_6C;
                        break;
                    }

                    case INS_sve_ld1rob:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_ld1rqh:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_3C;
                        result.insLatency = PERFSCORE_LATENCY_6C;
                        break;
                    }

                    case INS_sve_ld1roh:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_ld1rqw:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_3C;
                        result.insLatency = PERFSCORE_LATENCY_6C;
                        break;
                    }

                    case INS_sve_ld1row:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_ld1rqd:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_3C;
                        result.insLatency = PERFSCORE_LATENCY_6C;
                        break;
                    }

                    case INS_sve_ld1rod:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_IR_4A: // ...........mmmmm ...gggnnnnnttttt -- SVE load multiple structures (quadwords, scalar plus
            {
                // scalar)
                switch (ins)
                {
                    case INS_sve_ld2q:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_ld3q:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_ld4q:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_IT_4A: // ...........mmmmm ...gggnnnnnttttt -- SVE load multiple structures (scalar plus scalar)
            {
                switch (ins)
                {
                    case INS_sve_ld2b:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_9C;
                        break;
                    }

                    case INS_sve_ld3b:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_10C;
                        break;
                    }

                    case INS_sve_ld4b:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_10C;
                        break;
                    }

                    case INS_sve_ld2h:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_9C;
                        break;
                    }

                    case INS_sve_ld3h:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_10C;
                        break;
                    }

                    case INS_sve_ld4h:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_10C;
                        break;
                    }

                    case INS_sve_ld2w:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_9C;
                        break;
                    }

                    case INS_sve_ld3w:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_10C;
                        break;
                    }

                    case INS_sve_ld4w:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_10C;
                        break;
                    }

                    case INS_sve_ld2d:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_9C;
                        break;
                    }

                    case INS_sve_ld3d:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_10C;
                        break;
                    }

                    case INS_sve_ld4d:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_10C;
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_IU_4B:   // ...........mmmmm ...gggnnnnnttttt -- SVE 64-bit gather load (scalar plus 32-bit unpacked
            // scaled offsets)
            case IF_SVE_IU_4B_B: // ...........mmmmm ...gggnnnnnttttt -- SVE 64-bit gather load (scalar plus 32-bit unpacked
            // scaled offsets)
            case IF_SVE_IU_4B_D: // ...........mmmmm ...gggnnnnnttttt -- SVE 64-bit gather load (scalar plus 32-bit unpacked
            {
                // scaled offsets)
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_9C;
                break;
            }

            case IF_SVE_IW_4A: // ...........mmmmm ...gggnnnnnttttt -- SVE2 128-bit gather load (vector plus scalar)
            {
                switch (ins)
                {
                    case INS_sve_ld1q:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_IX_4A: // ...........mmmmm ...gggnnnnnttttt -- SVE2 64-bit gather non-temporal load (vector plus
            {
                // scalar)
                result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                result.insLatency = PERFSCORE_LATENCY_10C;
                break;
            }

            case IF_SVE_IY_4A: // ...........mmmmm ...gggnnnnnttttt -- SVE2 128-bit scatter store (vector plus scalar)
            {
                switch (ins)
                {
                    case INS_sve_st1q:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_IZ_4A:   // ...........mmmmm ...gggnnnnnttttt -- SVE2 32-bit scatter non-temporal store (vector plus
            // scalar)
            case IF_SVE_IZ_4A_A: // ...........mmmmm ...gggnnnnnttttt -- SVE2 32-bit scatter non-temporal store (vector plus
            // scalar)
            case IF_SVE_JA_4A:   // ...........mmmmm ...gggnnnnnttttt -- SVE2 64-bit scatter non-temporal store (vector plus
            // scalar)
            case IF_SVE_JB_4A:   // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous non-temporal store (scalar plus
            // scalar)
            case IF_SVE_JD_4C:   // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous store (scalar plus scalar)
            case IF_SVE_JD_4C_A: // ...........mmmmm ...gggnnnnnttttt -- SVE contiguous store (scalar plus scalar)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_SVE_JC_4A: // ...........mmmmm ...gggnnnnnttttt -- SVE store multiple structures (scalar plus scalar)
            {
                switch (ins)
                {
                    case INS_sve_st2b:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_sve_st3b:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_7C;
                        break;
                    }

                    case INS_sve_st4b:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_9X;
                        result.insLatency = PERFSCORE_LATENCY_11C;
                        break;
                    }

                    case INS_sve_st2h:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_sve_st3h:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_7C;
                        break;
                    }

                    case INS_sve_st4h:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_9X;
                        result.insLatency = PERFSCORE_LATENCY_11C;
                        break;
                    }

                    case INS_sve_st2w:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_sve_st3w:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_7C;
                        break;
                    }

                    case INS_sve_st4w:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_9X;
                        result.insLatency = PERFSCORE_LATENCY_11C;
                        break;
                    }

                    case INS_sve_st2d:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_4C;
                        break;
                    }

                    case INS_sve_st3d:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2X;
                        result.insLatency = PERFSCORE_LATENCY_7C;
                        break;
                    }

                    case INS_sve_st4d:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_9X;
                        result.insLatency = PERFSCORE_LATENCY_11C;
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_JF_4A: // ...........mmmmm ...gggnnnnnttttt -- SVE store multiple structures (quadwords, scalar plus
            {
                // scalar)
                switch (ins)
                {
                    case INS_sve_st2q:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_st3q:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_st4q:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_JJ_4B:   // ...........mmmmm ...gggnnnnnttttt -- SVE 64-bit scatter store (scalar plus 64-bit scaled
            // offsets)
            case IF_SVE_JJ_4B_C: // ...........mmmmm ...gggnnnnnttttt -- SVE 64-bit scatter store (scalar plus 64-bit scaled
            // offsets)
            case IF_SVE_JJ_4B_E: // ...........mmmmm ...gggnnnnnttttt -- SVE 64-bit scatter store (scalar plus 64-bit scaled
            // offsets)
            case IF_SVE_JK_4B: // ...........mmmmm ...gggnnnnnttttt -- SVE 64-bit scatter store (scalar plus 64-bit unscaled
            {
                // offsets)
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_SVE_GP_3A: // ........xx.....r ...gggmmmmmddddd -- SVE floating-point complex add (predicated)
            case IF_SVE_EI_3A: // ...........mmmmm ......nnnnnddddd -- SVE mixed sign dot product
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                result.insLatency = PERFSCORE_LATENCY_3C;
                break;
            }

            case IF_SVE_GV_3A: // ...........immmm ....rrnnnnnddddd -- SVE floating-point complex multiply-add (indexed)
            case IF_SVE_GT_4A: // ........xx.mmmmm .rrgggnnnnnddddd -- SVE floating-point complex multiply-add (predicated)
            case IF_SVE_HD_3A: // ...........mmmmm ......nnnnnddddd -- SVE floating point matrix multiply accumulate
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                result.insLatency = PERFSCORE_LATENCY_5C;
                break;
            }

            case IF_SVE_HI_3A: // ........xx...... ...gggnnnnn.DDDD -- SVE floating-point compare with zero
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_SVE_HM_2A: // ........xx...... ...ggg....iddddd -- SVE floating-point arithmetic with immediate
            {
                // (predicated)
                switch (ins)
                {
                    case INS_sve_fmul:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                        result.insLatency = PERFSCORE_LATENCY_3C;
                        break;
                    }

                    default:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        break;
                    }
                }
                break;
            }

            case IF_SVE_HN_2A: // ........xx...iii ......mmmmmddddd -- SVE floating-point trig multiply-add coefficient
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                result.insLatency = PERFSCORE_LATENCY_4C;
                break;
            }

            case IF_SVE_HP_3A: // .............xx. ...gggnnnnnddddd -- SVE floating-point convert to integer
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_3C;
                break;
            }

            case IF_SVE_HU_4B: // ...........mmmmm ...gggnnnnnddddd -- SVE floating-point multiply-accumulate writing addend
            {
                switch (ins)
                {
                    case INS_sve_bfmla:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_bfmls:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_HV_4A: // ........xx.aaaaa ...gggmmmmmddddd -- SVE floating-point multiply-accumulate writing
            // multiplicand
            case IF_SVE_HU_4A: // ........xx.mmmmm ...gggnnnnnddddd -- SVE floating-point multiply-accumulate writing addend
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                result.insLatency = PERFSCORE_LATENCY_4C;
                break;
            }

            case IF_SVE_ID_2A: // ..........iiiiii ...iiinnnnn.TTTT -- SVE load predicate register
            case IF_SVE_IE_2A: // ..........iiiiii ...iiinnnnnttttt -- SVE load vector register
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_3C;
                result.insLatency = PERFSCORE_LATENCY_6C;
                break;
            }

            case IF_SVE_JG_2A: // ..........iiiiii ...iiinnnnn.TTTT -- SVE store predicate register
            case IF_SVE_JH_2A: // ..........iiiiii ...iiinnnnnttttt -- SVE store vector register
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_SVE_GG_3A: // ........ii.mmmmm ......nnnnnddddd -- SVE2 lookup table with 2-bit indices and 16-bit
            {
                // element size
                result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                break;
            }

            case IF_SVE_GH_3B: // ........ii.mmmmm ......nnnnnddddd -- SVE2 lookup table with 4-bit indices and 16-bit
            {
                // element size
                result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                break;
            }

            case IF_SVE_GH_3B_B: // ........ii.mmmmm ......nnnnnddddd -- SVE2 lookup table with 4-bit indices and 16-bit
            {
                // element size
                result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                break;
            }

            case IF_SVE_GG_3B: // ........ii.mmmmm ...i..nnnnnddddd -- SVE2 lookup table with 2-bit indices and 16-bit
            {
                // element size
                result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                break;
            }

            case IF_SVE_GH_3A: // ........i..mmmmm ......nnnnnddddd -- SVE2 lookup table with 4-bit indices and 16-bit
            {
                // element size
                result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                break;
            }

            case IF_SVE_HY_3A: // .........h.mmmmm ...gggnnnnn.oooo -- SVE 32-bit gather prefetch (scalar plus 32-bit scaled
            {
                // offsets)
                switch (ins)
                {
                    case INS_sve_prfb:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_prfh:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_prfw:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_prfd:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_HY_3A_A: // .........h.mmmmm ...gggnnnnn.oooo -- SVE 32-bit gather prefetch (scalar plus 32-bit
            {
                // scaled offsets)
                switch (ins)
                {
                    case INS_sve_prfb:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_prfh:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_prfw:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_prfd:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_HY_3B: // ...........mmmmm ...gggnnnnn.oooo -- SVE 32-bit gather prefetch (scalar plus 32-bit scaled
            {
                // offsets)
                switch (ins)
                {
                    case INS_sve_prfb:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_prfh:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_prfw:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_prfd:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_IB_3A: // ...........mmmmm ...gggnnnnn.oooo -- SVE contiguous prefetch (scalar plus scalar)
            {
                switch (ins)
                {
                    case INS_sve_prfb:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_prfh:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_prfw:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_prfd:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_HZ_2A_B: // ...........iiiii ...gggnnnnn.oooo -- SVE 32-bit gather prefetch (vector plus immediate)
            {
                switch (ins)
                {
                    case INS_sve_prfb:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_prfh:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_prfw:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_prfd:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_IA_2A: // ..........iiiiii ...gggnnnnn.oooo -- SVE contiguous prefetch (scalar plus immediate)
            {
                switch (ins)
                {
                    case INS_sve_prfb:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_prfh:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_prfw:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_prfd:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_HX_3A_B: // ...........iiiii ...gggnnnnnttttt -- SVE 32-bit gather load (vector plus immediate)
            case IF_SVE_HX_3A_E: // ...........iiiii ...gggnnnnnttttt -- SVE 32-bit gather load (vector plus immediate)
            case IF_SVE_IV_3A:   // ...........iiiii ...gggnnnnnttttt -- SVE 64-bit gather load (vector plus immediate)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_9C;
                break;
            }

            case IF_SVE_JI_3A_A: // ...........iiiii ...gggnnnnnttttt -- SVE 32-bit scatter store (vector plus immediate)
            case IF_SVE_JL_3A:   // ...........iiiii ...gggnnnnnttttt -- SVE 64-bit scatter store (vector plus immediate)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_SVE_IC_3A:   // ..........iiiiii ...gggnnnnnttttt -- SVE load and broadcast element
            case IF_SVE_IC_3A_A: // ..........iiiiii ...gggnnnnnttttt -- SVE load and broadcast element
            case IF_SVE_IC_3A_B: // ..........iiiiii ...gggnnnnnttttt -- SVE load and broadcast element
            case IF_SVE_IC_3A_C: // ..........iiiiii ...gggnnnnnttttt -- SVE load and broadcast element
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_3C;
                result.insLatency = PERFSCORE_LATENCY_6C;
                break;
            }

            case IF_SVE_BI_2A: // ................ ......nnnnnddddd -- SVE constructive prefix (unpredicated)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_SVE_HH_2A: // ................ ......nnnnnddddd -- SVE2 FP8 upconverts
            {
                switch (ins)
                {
                    case INS_sve_f1cvt:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_f2cvt:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_bf1cvt:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_bf2cvt:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_f1cvtlt:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_f2cvtlt:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_bf1cvtlt:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    case INS_sve_bf2cvtlt:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                        result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_BJ_2A: // ........xx...... ......nnnnnddddd -- SVE floating-point exponential accelerator
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_3C;
                break;
            }

            case IF_SVE_CB_2A: // ........xx...... ......nnnnnddddd -- SVE broadcast general register
            {
                switch (ins)
                {
                    case INS_sve_mov:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        break;
                    }

                    case INS_sve_dup:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                        result.insLatency = PERFSCORE_LATENCY_3C;
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_CG_2A: // ........xx...... ......nnnnnddddd -- SVE reverse vector elements
            {
                switch (ins)
                {
                    case INS_sve_rev:
                    {
                        result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                        result.insLatency = PERFSCORE_LATENCY_2C;
                        break;
                    }

                    default:
                    {
                        // all other instructions
                        perfScoreUnhandledInstruction(id, ref result);
                        break;
                    }
                }
                break;
            }

            case IF_SVE_CH_2A: // ........xx...... ......nnnnnddddd -- SVE unpack vector elements
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_2C;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_SVE_HF_2A: // ........xx...... ......nnnnnddddd -- SVE floating-point reciprocal estimate (unpredicated)
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_3C;
                break;
            }

            case IF_SVE_BF_2A: // ........xx.xxiii ......nnnnnddddd -- SVE bitwise shift by immediate (unpredicated)
            case IF_SVE_FT_2A: // ........xx.xxiii ......nnnnnddddd -- SVE2 bitwise shift and insert
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_2C;
                break;
            }

            case IF_SVE_FU_2A: // ........xx.xxiii ......nnnnnddddd -- SVE2 bitwise shift right and accumulate
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C;
                result.insLatency = PERFSCORE_LATENCY_4C;
                break;
            }

            case IF_SVE_BX_2A:                                  // ...........ixxxx ......nnnnnddddd -- sve_int_perm_dupq_i
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                break;
            }

            case IF_SVE_BY_2A:                                  // ............iiii ......mmmmmddddd -- sve_int_perm_extq
            {
                result.insThroughput = PERFSCORE_THROUGHPUT_1C; // need to fix
                result.insLatency = PERFSCORE_LATENCY_1C; // need to fix
                break;
            }

            default:
            {
                // all other instructions
                perfScoreUnhandledInstruction(id, ref result);
                break;
            }
        }
    }
#endif
}
#endif
