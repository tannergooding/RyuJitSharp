// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;
#if REGMASK_BITS_32
using BitShapeRegBits = System.UInt32;
#else
using BitShapeRegBits = System.UInt64;
#endif

namespace RyuJitSharp;

public static partial class Globals
{
    // Return the lowest bit that is set.
    public static T genFindLowestBit<T>(T value)
        where T : IBinaryInteger<T>
    {
        return value & unchecked(T.Zero - value);
    }

    // Return true if the given value has exactly zero or one bits set.
    public static bool genMaxOneBit<T>(T value)
        where T : IBinaryInteger<T>
    {
        // C++ promotes signed 8/16-bit operands to int before subtracting and masking.
        // Keeping those operations narrow would incorrectly accept their minimum values.
        if ((typeof(T) == typeof(sbyte)) || (typeof(T) == typeof(short)))
        {
            var promoted = int.CreateChecked(value);

            return (promoted & (promoted - 1)) == 0;
        }

        return (value & unchecked(value - T.One)) == T.Zero;
    }

    // Return true if the given value has exactly one bit set.
    public static bool genExactlyOneBit<T>(T value)
        where T : IBinaryInteger<T>
    {
        return (value != T.Zero) && genMaxOneBit(value);
    }

    public static regMaskTP genFindLowestBit(regMaskTP value)
    {
#if HAS_MORE_THAN_64_REGISTERS
        if (value.Lower != SRBM_NONE)
        {
            var lower = unchecked((BitShapeRegBits)value.Lower);

            return new regMaskTP(unchecked((regMask)genFindLowestBit(lower)));
        }

        var upper = unchecked((BitShapeRegBits)value.Upper);

        return new regMaskTP(SRBM_NONE, unchecked((regMask)genFindLowestBit(upper)));
#else
        var lower = unchecked((BitShapeRegBits)value.Lower);

        return new regMaskTP(unchecked((regMask)genFindLowestBit(lower)));
#endif
    }

    public static bool genMaxOneBit(regMaskTP value)
    {
#if HAS_MORE_THAN_64_REGISTERS
        if (value.Lower == SRBM_NONE)
        {
            return genMaxOneBit(unchecked((BitShapeRegBits)value.Upper));
        }

        if (value.Upper == SRBM_NONE)
        {
            return genMaxOneBit(unchecked((BitShapeRegBits)value.Lower));
        }

        return false;
#else
        return genMaxOneBit(unchecked((BitShapeRegBits)value.Lower));
#endif
    }

    // Managed counterpart of the genExactlyOneBit<regMaskTP> instantiation.
    public static bool genExactlyOneBit(regMaskTP value)
    {
        return value.IsNonEmpty && genMaxOneBit(value);
    }

    // Given a value with exactly one bit set, return the position of that bit.
    public static uint genLog2(uint value)
    {
        assert(genExactlyOneBit(value));

        return BitScanForward(value);
    }

    // The Apple/OpenBSD size_t forwarding signature also maps to ulong.
    public static uint genLog2(ulong value)
    {
        assert(genExactlyOneBit(value));

        return BitScanForward(value);
    }
}
