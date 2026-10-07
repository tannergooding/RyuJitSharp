// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static RyuJitSharp.SveMaskPattern;

namespace RyuJitSharp;

public static partial class Globals
{
#if FEATURE_MASKED_HW_INTRINSICS
    public static bool EvaluateSimdPatternToVector<TSimd, TBase>(ref TSimd result, SveMaskPattern pattern)
        where TSimd : unmanaged
        where TBase : unmanaged
    {
        var elementSize = Unsafe.SizeOf<TBase>();
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
                finalOne = Math.Min((int)pattern - (int)SveMaskPatternVectorCount1 + 1, count);
                break;
            }

            case >= SveMaskPatternVectorCount16 and <= SveMaskPatternVectorCount256:
            {
                finalOne = Math.Min(16 << ((int)pattern - (int)SveMaskPatternVectorCount16), count);
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

        assert(finalOne <= count);

        var output = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref result, 1));
        var initializedBytes = finalOne * elementSize;
        output[..initializedBytes].Fill(byte.MaxValue);
        output[initializedBytes..].Clear();

        return true;
    }

    public static bool EvaluateSimdPatternToVector<TSimd>(var_types baseType, ref TSimd result, SveMaskPattern pattern)
        where TSimd : unmanaged
    {
        switch (baseType)
        {
            case TYP_FLOAT:
            case TYP_INT:
            case TYP_UINT:
            {
                return EvaluateSimdPatternToVector<TSimd, uint>(ref result, pattern);
            }

            case TYP_DOUBLE:
            case TYP_LONG:
            case TYP_ULONG:
            {
                return EvaluateSimdPatternToVector<TSimd, ulong>(ref result, pattern);
            }

            case TYP_BYTE:
            case TYP_UBYTE:
            {
                return EvaluateSimdPatternToVector<TSimd, byte>(ref result, pattern);
            }

            case TYP_SHORT:
            case TYP_USHORT:
            {
                return EvaluateSimdPatternToVector<TSimd, ushort>(ref result, pattern);
            }

            default:
            {
                unreached();
                throw new FatalJitException(CORJIT_IMPLLIMITATION, "Unsupported ARM64 SIMD element type.");
            }
        }
    }
#endif

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
