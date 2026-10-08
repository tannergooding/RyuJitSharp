// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.insSvePrfop;

namespace RyuJitSharp;

public partial class Emitter
{
    private enum PredicateType
    {
        PREDICATE_NONE,
        PREDICATE_MERGE,
        PREDICATE_ZERO,
        PREDICATE_SIZED,
        PREDICATE_N,
        PREDICATE_N_SIZED,
    }

    private static readonly string[] s_sveVectorRegisterNames =
    [
        "z0", "z1", "z2", "z3", "z4", "z5", "z6", "z7",
        "z8", "z9", "z10", "z11", "z12", "z13", "z14", "z15",
        "z16", "z17", "z18", "z19", "z20", "z21", "z22", "z23",
        "z24", "z25", "z26", "z27", "z28", "z29", "z30", "z31",
    ];

    private static readonly string[] s_svePredicateRegisterNames =
    [
        "p0", "p1", "p2", "p3", "p4", "p5", "p6", "p7",
        "p8", "p9", "p10", "p11", "p12", "p13", "p14", "p15",
    ];

    private static readonly string[] s_svePredicateCounterRegisterNames =
    [
        "pn0", "pn1", "pn2", "pn3", "pn4", "pn5", "pn6", "pn7",
        "pn8", "pn9", "pn10", "pn11", "pn12", "pn13", "pn14", "pn15",
    ];

    private static readonly string[] s_svePatternNames =
    [
        "pow2", "vl1", "vl2", "vl3", "vl4", "vl5", "vl6", "vl7",
        "vl8", "vl16", "vl32", "vl64", "vl128", "vl256",
        "invalid", "invalid", "invalid", "invalid", "invalid", "invalid",
        "invalid", "invalid", "invalid", "invalid", "invalid", "invalid",
        "invalid", "invalid", "invalid", "mul4", "mul3", "all",
    ];

    private static string emitSveRegName(regNumber reg)
    {
        assert((reg >= REG_V0) && (reg <= REG_V31));
        return s_sveVectorRegisterNames[(int)reg - (int)REG_V0];
    }

    private static string emitPredicateRegName(regNumber reg, PredicateType ptype)
    {
        assert((reg >= REG_P0) && (reg <= REG_P15));
        var index = (int)reg - (int)REG_P0;
        var usePredicateCounter = ptype is PredicateType.PREDICATE_N or PredicateType.PREDICATE_N_SIZED;
        return usePredicateCounter ? s_svePredicateCounterRegisterNames[index] : s_svePredicateRegisterNames[index];
    }

    private void emitDispSveReg(regNumber reg, bool addComma)
    {
        assert(isVectorRegister(reg));
        jitprintf(emitSveRegName(reg));
        if (addComma)
        {
            emitDispComma();
        }
    }

    private void emitDispSveReg(regNumber reg, insOpts opt, bool addComma)
    {
        assert(isVectorRegister(reg));
        jitprintf(emitSveRegName(reg));
        if (opt != INS_OPTS_NONE)
        {
            assert(insOptsScalable(opt) || insOptsScalable32bitExtends(opt));
            emitDispArrangement(opt);
        }
        if (addComma)
        {
            emitDispComma();
        }
    }

    private void emitDispSveRegIndex(regNumber reg, nint index, bool addComma)
    {
        assert(isVectorRegister(reg));
        jitprintf(emitSveRegName(reg));
        emitDispElementIndex(index, addComma);
    }

    private void emitDispSveConsecutiveRegList(regNumber firstReg, uint listSize, insOpts opt, bool addComma)
    {
        assert(isVectorRegister(firstReg));
        assert(listSize > 0);
        var currReg = firstReg;

        jitprintf("{ ");
        if ((listSize <= 2) || (((uint)currReg + listSize - 1) > (uint)REG_V31))
        {
            for (uint i = 0; i < listSize; i++)
            {
                var notLastRegister = i != listSize - 1;
                emitDispSveReg(currReg, opt, notLastRegister);
                currReg = currReg == REG_V31 ? REG_V0 : REG_NEXT(currReg);
            }
        }
        else
        {
            emitDispSveReg(currReg, opt, false);
            jitprintf(" - ");
            emitDispSveReg((regNumber)((uint)currReg + listSize - 1), opt, false);
        }
        jitprintf(" }");

        if (addComma)
        {
            emitDispComma();
        }
    }

    private void emitDispPredicateReg(regNumber reg, PredicateType ptype, insOpts opt, bool addComma)
    {
        assert(isPredicateRegister(reg));
        jitprintf(emitPredicateRegName(reg, ptype));
        if (ptype == PredicateType.PREDICATE_MERGE)
        {
            jitprintf("/m");
        }
        else if (ptype == PredicateType.PREDICATE_ZERO)
        {
            jitprintf("/z");
        }
        else if (ptype is PredicateType.PREDICATE_SIZED or PredicateType.PREDICATE_N_SIZED)
        {
            emitDispElemsize(optGetSveElemsize(opt));
        }
        if (addComma)
        {
            emitDispComma();
        }
    }

    private void emitDispPredicateRegPair(regNumber reg, insOpts opt)
    {
        jitprintf("{ ");
        emitDispPredicateReg(reg, PredicateType.PREDICATE_SIZED, opt, true);
        emitDispPredicateReg((regNumber)((uint)reg + 1), PredicateType.PREDICATE_SIZED, opt, false);
        jitprintf(" }, ");
    }

    private void emitDispLowPredicateReg(regNumber reg, PredicateType ptype, insOpts opt, bool addComma)
    {
        assert(isLowPredicateRegister(reg));
        reg = (regNumber)((((uint)reg - (uint)REG_PREDICATE_FIRST) & 0x7) + (uint)REG_PREDICATE_FIRST);
        emitDispPredicateReg(reg, ptype, opt, addComma);
    }

    private void emitDispLowPredicateRegPair(regNumber reg, insOpts opt)
    {
        assert(isLowPredicateRegister(reg));
        jitprintf("{ ");
        var baseRegNum = ((uint)reg - (uint)REG_PREDICATE_FIRST) & 0x7;
        var regNum = (baseRegNum * 2) + (uint)REG_PREDICATE_FIRST;
        emitDispPredicateReg((regNumber)regNum, PredicateType.PREDICATE_SIZED, opt, true);
        emitDispPredicateReg((regNumber)(regNum + 1), PredicateType.PREDICATE_SIZED, opt, false);
        jitprintf(" }, ");
    }

    private void emitDispVectorLengthSpecifier(instrDesc id)
    {
        assert(id is not null);
        assert(insOptsScalableStandard(id.idInsOpt()));
        jitprintf(id.idVectorLength4x() ? "vlx4" : "vlx2");
    }

    private void emitDispSvePattern(insSvePattern pattern, bool addComma)
    {
        jitprintf(s_svePatternNames[(int)pattern]);
        if (addComma)
        {
            emitDispComma();
        }
    }

    private void emitDispSvePrfop(insSvePrfop prfop, bool addComma)
    {
        switch (prfop)
        {
            case SVE_PRFOP_PLDL1KEEP: jitprintf("pldl1keep"); break;
            case SVE_PRFOP_PLDL1STRM: jitprintf("pldl1strm"); break;
            case SVE_PRFOP_PLDL2KEEP: jitprintf("pldl2keep"); break;
            case SVE_PRFOP_PLDL2STRM: jitprintf("pldl2strm"); break;
            case SVE_PRFOP_PLDL3KEEP: jitprintf("pldl3keep"); break;
            case SVE_PRFOP_PLDL3STRM: jitprintf("pldl3strm"); break;
            case SVE_PRFOP_PSTL1KEEP: jitprintf("pstl1keep"); break;
            case SVE_PRFOP_PSTL1STRM: jitprintf("pstl1strm"); break;
            case SVE_PRFOP_PSTL2KEEP: jitprintf("pstl2keep"); break;
            case SVE_PRFOP_PSTL2STRM: jitprintf("pstl2strm"); break;
            case SVE_PRFOP_PSTL3KEEP: jitprintf("pstl3keep"); break;
            case SVE_PRFOP_PSTL3STRM: jitprintf("pstl3strm"); break;
            case SVE_PRFOP_CONST6: jitprintf("#6"); break;
            case SVE_PRFOP_CONST7: jitprintf("#7"); break;
            case SVE_PRFOP_CONST14: jitprintf("#0xE"); break;
            case SVE_PRFOP_CONST15: jitprintf("#0xF"); break;
            default: assert(false, "!\"Invalid prfop\""); break;
        }
        if (addComma)
        {
            emitDispComma();
        }
    }

    private void emitDispSveExtendOpts(insOpts opt)
    {
        switch (opt)
        {
            case INS_OPTS_LSL:
            {
                jitprintf("lsl");
                break;
            }
            case INS_OPTS_UXTW:
            case INS_OPTS_SCALABLE_S_UXTW:
            case INS_OPTS_SCALABLE_D_UXTW:
            {
                jitprintf("uxtw");
                break;
            }
            case INS_OPTS_SXTW:
            case INS_OPTS_SCALABLE_S_SXTW:
            case INS_OPTS_SCALABLE_D_SXTW:
            {
                jitprintf("sxtw");
                break;
            }
            default:
            {
                assert(false, "!\"Bad value\"");
                break;
            }
        }
    }

    private void emitDispSveExtendOptsModN(insOpts opt, nint imm)
    {
        assert(imm >= 0 && imm <= 3);
        if (imm == 0 && opt != INS_OPTS_LSL)
        {
            emitDispSveExtendOpts(opt);
        }
        else if (imm > 0)
        {
            emitDispSveExtendOpts(opt);
            jitprintf($" #{(int)imm}");
        }
    }

    private void emitDispSveModAddr(instruction ins, regNumber reg1, regNumber reg2, insOpts opt,
        insFormat fmt)
    {
        jitprintf("[");
        if (isVectorRegister(reg1))
        {
            var regOpt = opt == INS_OPTS_SCALABLE_Q ? INS_OPTS_SCALABLE_D : opt;
            emitDispSveReg(reg1, regOpt, reg2 != REG_ZR);
        }
        else
        {
            emitDispReg(reg1, EA_8BYTE, reg2 != REG_ZR);
        }
        if (isVectorRegister(reg2))
        {
            emitDispSveReg(reg2, opt, false);
        }
        else if (reg2 != REG_ZR)
        {
            emitDispReg(reg2, EA_8BYTE, false);
        }
        if (insOptsScalable32bitExtends(opt))
        {
            emitDispComma();
            emitDispSveExtendOptsModN(opt, insSveGetLslOrModN(ins, fmt));
        }
        else if ((reg2 != REG_ZR) && insSveIsLslN(ins, fmt))
        {
            emitDispComma();
            switch (insSveGetLslOrModN(ins, fmt))
            {
                case 4: jitprintf("lsl #4"); break;
                case 3: jitprintf("lsl #3"); break;
                case 2: jitprintf("lsl #2"); break;
                case 1: jitprintf("lsl #1"); break;
                default: assert(false, "!\"Invalid instruction\""); break;
            }
        }
        jitprintf("]");
    }

    private void emitDispSveImm(regNumber reg1, nint imm, insOpts opt)
    {
        jitprintf("[");
        emitDispSveReg(reg1, opt, imm != 0);
        if (imm != 0)
        {
            emitDispImm(imm, false, alwaysHex: true);
        }
        jitprintf("]");
    }

    private void emitDispSveImmMulVl(regNumber reg1, nint imm)
    {
        jitprintf("[");
        emitDispReg(reg1, EA_8BYTE, imm != 0);
        if (imm != 0)
        {
            emitDispImm(imm, true);
            jitprintf("mul vl");
        }
        jitprintf("]");
    }

    private void emitDispSveImmIndex(regNumber reg1, insOpts opt, nint imm)
    {
        jitprintf("[");
        if (isVectorRegister(reg1))
        {
            emitDispSveReg(reg1, opt, imm != 0);
        }
        else
        {
            emitDispReg(reg1, EA_8BYTE, imm != 0);
        }
        if (imm != 0)
        {
            emitDispImm(imm, false, alwaysHex: imm > 31);
        }
        jitprintf("]");
    }

    private static double emitDecodeSmallFloatImm(nint imm, instruction ins)
    {
        assert(emitIsValidEncodedSmallFloatImm((nuint)imm));
        switch (ins)
        {
            case INS_sve_fadd:
            case INS_sve_fsub:
            case INS_sve_fsubr:
            {
                return imm == 0 ? 0.5 : 1.0;
            }
            case INS_sve_fmax:
            case INS_sve_fmaxnm:
            case INS_sve_fmin:
            case INS_sve_fminnm:
            {
                return imm == 0 ? 0.0 : 1.0;
            }
            case INS_sve_fmul:
            {
                return imm == 0 ? 0.5 : 2.0;
            }
            default:
            {
                assert(false, "!\"Invalid instruction\"");
                return 0;
            }
        }
    }

    private static nint emitEncodeRotationImm90_or_270(nint imm)
    {
        return imm switch
        {
            90 => 0,
            270 => 1,
            _ => InvalidRotation(),
        };
    }

    private static nint emitEncodeRotationImm0_to_270(nint imm)
    {
        return imm switch
        {
            0 => 0,
            90 => 1,
            180 => 2,
            270 => 3,
            _ => InvalidRotation(),
        };
    }

    private static nint InvalidRotation()
    {
        assert(false, "!\"Invalid rotation value\"");
        return 0;
    }

    private static insOpts optWidenSveElemsizeArrangement(insOpts arrangement)
    {
        return arrangement switch
        {
            INS_OPTS_SCALABLE_B => INS_OPTS_SCALABLE_H,
            INS_OPTS_SCALABLE_H => INS_OPTS_SCALABLE_S,
            INS_OPTS_SCALABLE_S => INS_OPTS_SCALABLE_D,
            _ => InvalidSveArrangement(),
        };
    }

    private static insOpts optSveToQuadwordElemsizeArrangement(insOpts arrangement)
    {
        return arrangement switch
        {
            INS_OPTS_SCALABLE_B => INS_OPTS_16B,
            INS_OPTS_SCALABLE_H => INS_OPTS_8H,
            INS_OPTS_SCALABLE_S => INS_OPTS_4S,
            INS_OPTS_SCALABLE_D => INS_OPTS_2D,
            _ => InvalidSveArrangement(),
        };
    }

    private static insOpts InvalidSveArrangement()
    {
        assert(false, "!\"invalid arrangement value\"");
        return INS_OPTS_NONE;
    }

    private static void optExpandConversionPair(insOpts opt, out insOpts dst, out insOpts src)
    {
        (dst, src) = opt switch
        {
            INS_OPTS_H_TO_S => (INS_OPTS_SCALABLE_S, INS_OPTS_SCALABLE_H),
            INS_OPTS_S_TO_H => (INS_OPTS_SCALABLE_H, INS_OPTS_SCALABLE_S),
            INS_OPTS_S_TO_D => (INS_OPTS_SCALABLE_D, INS_OPTS_SCALABLE_S),
            INS_OPTS_D_TO_S => (INS_OPTS_SCALABLE_S, INS_OPTS_SCALABLE_D),
            INS_OPTS_H_TO_D => (INS_OPTS_SCALABLE_D, INS_OPTS_SCALABLE_H),
            INS_OPTS_D_TO_H => (INS_OPTS_SCALABLE_H, INS_OPTS_SCALABLE_D),
            INS_OPTS_SCALABLE_H => (INS_OPTS_SCALABLE_H, INS_OPTS_SCALABLE_H),
            INS_OPTS_SCALABLE_S => (INS_OPTS_SCALABLE_S, INS_OPTS_SCALABLE_S),
            INS_OPTS_SCALABLE_D => (INS_OPTS_SCALABLE_D, INS_OPTS_SCALABLE_D),
            _ => InvalidConversionPair(),
        };
        assert(dst != INS_OPTS_NONE && src != INS_OPTS_NONE);
    }

    private static (insOpts, insOpts) InvalidConversionPair()
    {
        unreached();
        return (INS_OPTS_NONE, INS_OPTS_NONE);
    }
}
#endif
