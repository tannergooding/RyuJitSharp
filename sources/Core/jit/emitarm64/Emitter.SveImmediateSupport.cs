// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
namespace RyuJitSharp;

public partial class Emitter
{
    public static emitAttr optGetSveElemsize(insOpts arrangement)
    {
        switch (arrangement)
        {
            case INS_OPTS_SCALABLE_B:
            {
                return EA_1BYTE;
            }

            case INS_OPTS_SCALABLE_H:
            {
                return EA_2BYTE;
            }

            case INS_OPTS_SCALABLE_S:
            case INS_OPTS_SCALABLE_S_UXTW:
            case INS_OPTS_SCALABLE_S_SXTW:
            {
                return EA_4BYTE;
            }

            case INS_OPTS_SCALABLE_D:
            case INS_OPTS_SCALABLE_D_UXTW:
            case INS_OPTS_SCALABLE_D_SXTW:
            {
                return EA_8BYTE;
            }

            case INS_OPTS_SCALABLE_Q:
            {
                return EA_16BYTE;
            }

            default:
            {
                assert(false, "Invalid insOpt for vector register");
                return EA_UNKNOWN;
            }
        }
    }

    private static bool useMovDisasmForBitMask(nint value)
    {
        var imm = value & 0xFF;
        uint minFieldSize;
        if (imm == 0)
        {
            imm = value & 0xFF00;
            minFieldSize = 16;
        }
        else
        {
            minFieldSize = 8;
        }

        assert(isValidUimm(imm, 16));

        // A byte or high-byte immediate repeated in zero- or one-filled fields prefers DUPM.
        for (var width = minFieldSize; width <= 64; width <<= 1)
        {
            if (value == getBitMaskZeroes(imm, width))
            {
                return false;
            }

            if (value == getBitMaskOnes(imm, width))
            {
                return false;
            }
        }

        return true;
    }

    private static nint getBitMaskOnes(nint imm, uint width)
    {
        assert(isValidUimm(imm, 16));
        assert((width % 8) == 0);
        assert(isValidGeneralLSDatasize((emitAttr)(width / 8)));

        var immWidth = isValidUimm(imm, 8) ? 8u : 16u;
        var numIterations = 64 / width;

        // The native expressions can shift by 64, which is undefined in C++.
        // Literal C# shifts mask the count; no width-specific repair is applied.
        var ones = unchecked((nint)(ulong.MaxValue >> (int)(64 - width + immWidth)));
        nint mask = 0;

        for (uint i = 0; i < numIterations; i++)
        {
            mask <<= (int)width;
            mask |= (ones << (int)immWidth) | imm;
        }

        return mask;
    }

    private static nint getBitMaskZeroes(nint imm, uint width)
    {
        assert(isValidUimm(imm, 16));
        assert((width % 8) == 0);
        assert(isValidGeneralLSDatasize((emitAttr)(width / 8)));

        var numIterations = 64 / width;
        nint mask = 0;

        for (uint i = 0; i < numIterations; i++)
        {
            mask <<= (int)width;
            mask |= imm;
        }

        return mask;
    }

    private static bool isScalableVectorSize(emitAttr size)
    {
        return size == EA_SCALABLE;
    }

    private static bool isHighPredicateRegister(regNumber reg)
    {
        return (reg >= REG_PREDICATE_HIGH_FIRST) && (reg <= REG_PREDICATE_HIGH_LAST);
    }

    private static bool insOptsScalableStandard(insOpts opt)
    {
        return (opt == INS_OPTS_SCALABLE_B) || (opt == INS_OPTS_SCALABLE_H)
            || (opt == INS_OPTS_SCALABLE_S) || (opt == INS_OPTS_SCALABLE_D);
    }

    private static bool insOptsScalableAtLeastHalf(insOpts opt)
    {
        return (opt == INS_OPTS_SCALABLE_H) || (opt == INS_OPTS_SCALABLE_S) || (opt == INS_OPTS_SCALABLE_D);
    }
}
#endif
