// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public partial class Emitter
{
    private static bool isValidUimmFrom1(nint value, int bits)
    {
        return isValidUimm(unchecked(value - 1), bits);
    }

    private static bool isValidBroadcastImm(nint imm, emitAttr laneSize)
    {
        // The index fits 7 - (log2(bytes in a lane) + 1) bits.
        nint max = 0;
        switch (laneSize)
        {
            case EA_16BYTE:
            {
                max = 4;
                break;
            }

            case EA_8BYTE:
            {
                max = 8;
                break;
            }

            case EA_4BYTE:
            {
                max = 16;
                break;
            }

            case EA_2BYTE:
            {
                max = 32;
                break;
            }

            case EA_1BYTE:
            {
                max = 64;
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        return (imm >= 0) && (imm < max);
    }

    private static bool isLowVectorRegister(regNumber reg)
    {
        return (reg >= FIRST_FP_ARGREG) && (reg <= LAST_FP_ARGREG);
    }

    private static bool isLowPredicateRegister(regNumber reg)
    {
        return (reg >= REG_PREDICATE_FIRST) && (reg <= REG_PREDICATE_LOW_LAST);
    }

    private static bool isEvenRegister(regNumber reg)
    {
        if (isGeneralRegister(reg))
        {
            return ((reg - REG_INT_FIRST) % 2) == 0;
        }
        else if (isVectorRegister(reg))
        {
            return ((reg - REG_FP_FIRST) % 2) == 0;
        }
        else
        {
            assert(isPredicateRegister(reg));
            return ((reg - REG_PREDICATE_FIRST) % 2) == 0;
        }
    }

    private static bool insOptsConvertFloatStepwise(insOpts opt)
    {
        return (opt == INS_OPTS_H_TO_S) || (opt == INS_OPTS_S_TO_H)
            || (opt == INS_OPTS_D_TO_S) || (opt == INS_OPTS_S_TO_D);
    }

    private static bool insOptsScalable(insOpts opt)
    {
        return (opt == INS_OPTS_SCALABLE_B) || (opt == INS_OPTS_SCALABLE_H) || (opt == INS_OPTS_SCALABLE_S)
            || (opt == INS_OPTS_SCALABLE_D) || (opt == INS_OPTS_SCALABLE_Q);
    }

    private static bool insOptsScalableWords(insOpts opt)
    {
        return (opt == INS_OPTS_SCALABLE_S) || (opt == INS_OPTS_SCALABLE_D);
    }

    private static bool insOptsScalableWordsOrQuadwords(insOpts opt)
    {
        return insOptsScalableWords(opt) || (opt == INS_OPTS_SCALABLE_Q);
    }

    private static bool insOptsScalableDoubleWordsOrQuadword(insOpts opt)
    {
        return (opt == INS_OPTS_SCALABLE_D) || (opt == INS_OPTS_SCALABLE_Q);
    }

    private static bool insOptsScalableAtMaxHalf(insOpts opt)
    {
        return (opt == INS_OPTS_SCALABLE_B) || (opt == INS_OPTS_SCALABLE_H);
    }

    private static bool insOptsScalableFloat(insOpts opt)
    {
        return (opt == INS_OPTS_SCALABLE_H) || (opt == INS_OPTS_SCALABLE_S) || (opt == INS_OPTS_SCALABLE_D);
    }

    private static bool insOptsScalableWide(insOpts opt)
    {
        return (opt == INS_OPTS_SCALABLE_B) || (opt == INS_OPTS_SCALABLE_H) || (opt == INS_OPTS_SCALABLE_S);
    }

    private static bool insOptsScalable32bitExtends(insOpts opt)
    {
        return insOptsScalableSingleWord32bitExtends(opt) || insOptsScalableDoubleWord32bitExtends(opt);
    }

    private static bool insOptsScalableSingleWord32bitExtends(insOpts opt)
    {
        return (opt == INS_OPTS_SCALABLE_S_UXTW) || (opt == INS_OPTS_SCALABLE_S_SXTW);
    }

    private static bool insOptsScalableDoubleWord32bitExtends(insOpts opt)
    {
        return (opt == INS_OPTS_SCALABLE_D_UXTW) || (opt == INS_OPTS_SCALABLE_D_SXTW);
    }

    private static unsafe void insSveDecodeTwoSimm5(nint imm, nint* imm1, nint* imm2)
    {
        assert(imm1 != null);
        assert(imm2 != null);

        *imm1 = imm & 0x1F;
        if ((imm & 0x20) != 0)
        {
            *imm1 *= -1;
        }

        imm >>= 6;
        *imm2 = imm & 0x1F;
        if ((imm & 0x20) != 0)
        {
            *imm2 *= -1;
        }

        assert(isValidSimm(*imm1, 5));
        assert(isValidSimm(*imm2, 5));
    }

    private static bool emitIsValidEncodedSmallFloatImm(nuint imm)
    {
        return (imm == 0) || (imm == 1);
    }

    private static bool emitIsValidEncodedRotationImm90_or_270(nint imm)
    {
        return isValidUimm(imm, 1);
    }

    private static bool emitIsValidEncodedRotationImm0_to_270(nint imm)
    {
        return isValidUimm(imm, 2);
    }
}
#endif
