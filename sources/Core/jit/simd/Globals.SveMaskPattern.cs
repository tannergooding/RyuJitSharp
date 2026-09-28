// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System.Runtime.CompilerServices;
using static RyuJitSharp.SveMaskPattern;

namespace RyuJitSharp;

public static partial class Globals
{
    public static bool EvaluateSimdPatternToMask<TSimd>(var_types baseType, ref simdmask_t result, SveMaskPattern pattern)
        where TSimd : unmanaged
    {
        var elementSize = GetSimdElementSize(baseType);
        var count = Unsafe.SizeOf<TSimd>() / elementSize;
        int finalOne;

        switch (pattern)
        {
            case SveMaskPatternLargestPowerOf2:
            case SveMaskPatternAll:
            {
                finalOne = count;
                break;
            }

            case >= SveMaskPatternVectorCount1 and <= SveMaskPatternVectorCount8:
            {
                finalOne = pattern - SveMaskPatternVectorCount1 + 1;
                break;
            }

            case >= SveMaskPatternVectorCount16 and <= SveMaskPatternVectorCount256:
            {
                finalOne = 16 << (pattern - SveMaskPatternVectorCount16);
                break;
            }

            case SveMaskPatternLargestMultipleOf4:
            {
                finalOne = count - (count % 4);
                break;
            }

            case SveMaskPatternLargestMultipleOf3:
            {
                finalOne = count - (count % 3);
                break;
            }

            default:
            {
                return false;
            }
        }

        // PTRUE constraints exceeding the vector's lane count produce an all-false predicate.
        if (finalOne > count)
        {
            finalOne = 0;
        }

        ulong bits = 0;
        for (var index = 0; index < finalOne; index++)
        {
            bits |= 1UL << (index * elementSize);
        }

        result.u64[0] = bits;
        return true;
    }

    public static SveMaskPattern EvaluateSimdMaskToPattern<TSimd>(var_types baseType, simdmask_t mask)
        where TSimd : unmanaged
    {
        var laneSize = baseType switch
        {
            TYP_FLOAT or TYP_INT or TYP_UINT => 4,
            TYP_DOUBLE or TYP_LONG or TYP_ULONG => 8,
            TYP_BYTE or TYP_UBYTE => 1,
            TYP_SHORT or TYP_USHORT => 2,
            _ => throw new FatalJitException(CORJIT_IMPLLIMITATION, "Unsupported ARM64 mask element type."),
        };

        var count = Unsafe.SizeOf<TSimd>() / laneSize;
        var bits = unchecked((ulong)mask.RawBits);
        var firstZero = count;

        // Predicate bits are byte-granular; only the lowest bit of each element's group may be set.
        var laneMask = (1UL << laneSize) - 1;

        for (var i = 0; i < count; i++)
        {
            var lane = bits >> (i * laneSize);
            var elem = lane & laneMask;

            if (elem == 0)
            {
                firstZero = i;
                break;
            }
            else if (elem != 1)
            {
                return SveMaskPattern.SveMaskPatternNone;
            }
        }

        for (var i = firstZero; i < count; i++)
        {
            var lane = bits >> (i * laneSize);
            var elem = lane & laneMask;

            if (elem != 0)
            {
                return SveMaskPattern.SveMaskPatternNone;
            }
        }

        if (firstZero == count)
        {
            return SveMaskPattern.SveMaskPatternAll;
        }
        else if ((firstZero >= (int)SveMaskPattern.SveMaskPatternVectorCount1) &&
                 (firstZero <= (int)SveMaskPattern.SveMaskPatternVectorCount8))
        {
            return (SveMaskPattern)firstZero;
        }

        return SveMaskPattern.SveMaskPatternNone;
    }
}
#endif
