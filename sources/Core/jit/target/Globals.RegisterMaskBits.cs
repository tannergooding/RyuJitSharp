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
        if (Configuration.REGMASK_BITS_32)
        {
            return (uint)BitOperations.PopCount(unchecked((uint)value));
        }

        return (uint)BitOperations.PopCount(unchecked((ulong)value));
    }

    internal static uint PopCount(regMaskTP value)
    {
        var result = PopCount(value.Lower);
        if (Configuration.HAS_MORE_THAN_64_REGISTERS)
        {
            result += PopCount(value.Upper);
        }

        return result;
    }

    internal static uint BitScanForward(SingleTypeRegSet value)
    {
        if (Configuration.REGMASK_BITS_32)
        {
            var bits = unchecked((uint)value);
            assert(bits != 0, conditionExpression: "value != 0");

            return (uint)BitOperations.TrailingZeroCount(bits);
        }

        var bits64 = unchecked((ulong)value);
        assert(bits64 != 0, conditionExpression: "value != 0");

        return (uint)BitOperations.TrailingZeroCount(bits64);
    }

    internal static uint BitScanForward(regMaskTP mask)
    {
        if (Configuration.HAS_MORE_THAN_64_REGISTERS)
        {
            if (mask.Lower != SRBM_NONE)
            {
                return BitScanForward(mask.Lower);
            }

            return 64 + BitScanForward(mask.Upper);
        }

        return BitScanForward(mask.Lower);
    }
}
