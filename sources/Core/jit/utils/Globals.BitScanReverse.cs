// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;

namespace RyuJitSharp;

public static partial class Globals
{
    public static uint BitScanReverse(uint value)
    {
        assert(value != 0);

#if HOST_WINDOWS
        return (uint)BitOperations.Log2(value);
#else
        // LZCNT returns index starting from MSB, whereas BSR gives the index from LSB.
        // 31 ^ BSR here is equivalent to 31 - BSR since the BSR result is always between 0 and 31.
        // This saves an instruction, as subtraction from constant requires either MOV/SUB or NEG/ADD.

        var result = BitOperations.LeadingZeroCount(value);
        return (uint)(31 ^ result);
#endif
    }

    public static uint BitScanReverse(ulong value)
    {
        assert(value != 0);

#if HOST_WINDOWS
#if HOST_64BIT
        return (uint)BitOperations.Log2(value);
#else
        var upper = (uint)(value >> 32);

        if (upper == 0)
        {
            var lower = unchecked((uint)value);
            return BitScanReverse(lower);
        }

        return 32 + BitScanReverse(upper);
#endif
#else
        // LZCNT returns index starting from MSB, whereas BSR gives the index from LSB.
        // 63 ^ BSR here is equivalent to 63 - BSR since the BSR result is always between 0 and 63.
        // This saves an instruction, as subtraction from constant requires either MOV/SUB or NEG/ADD.

        var result = BitOperations.LeadingZeroCount(value);
        return (uint)(63 ^ result);
#endif
    }
}
