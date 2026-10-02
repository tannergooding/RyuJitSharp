// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;

namespace RyuJitSharp;

public static partial class Globals
{
    internal static uint PopCount(SingleTypeRegSet value)
    {
#if REGMASK_BITS_32
        return (uint)BitOperations.PopCount(unchecked((uint)value));
#else
        return (uint)BitOperations.PopCount(unchecked((ulong)value));
#endif
    }

    internal static uint PopCount(regMaskTP value)
    {
        var result = PopCount(value.Lower);
#if HAS_MORE_THAN_64_REGISTERS
        result += PopCount(value.Upper);
#endif
        return result;
    }

    internal static uint BitScanForward(SingleTypeRegSet value)
    {
#if REGMASK_BITS_32
        var bits = unchecked((uint)value);
#else
        var bits = unchecked((ulong)value);
#endif
        assert(bits != 0, conditionExpression: "value != 0");

        return (uint)BitOperations.TrailingZeroCount(bits);
    }

    internal static uint BitScanForward(regMaskTP mask)
    {
#if HAS_MORE_THAN_64_REGISTERS
        if (mask.Lower != SRBM_NONE)
        {
            return BitScanForward(mask.Lower);
        }
        else
        {
            return 64 + BitScanForward(mask.Upper);
        }
#else
        return BitScanForward(mask.Lower);
#endif
    }
}
