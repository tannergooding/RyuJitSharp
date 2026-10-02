// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;

namespace RyuJitSharp;

public static partial class Globals
{
    public static uint BitScanForward(uint value)
    {
        assert(value != 0);

#if HOST_WINDOWS
        return (uint)BitOperations.TrailingZeroCount(value);
#else
        var result = BitOperations.TrailingZeroCount(value);
        return (uint)result;
#endif
    }

    public static uint BitScanForward(ulong value)
    {
        assert(value != 0);

#if HOST_WINDOWS
#if HOST_64BIT
        return (uint)BitOperations.TrailingZeroCount(value);
#else
        var lower = unchecked((uint)value);

        if (lower == 0)
        {
            var upper = (uint)(value >> 32);
            return 32 + BitScanForward(upper);
        }

        return BitScanForward(lower);
#endif
#else
        var result = BitOperations.TrailingZeroCount(value);
        return (uint)result;
#endif
    }

    public static uint LeadingZeroCount(uint value)
    {
        if (value == 0)
        {
            return 32;
        }

#if HOST_WINDOWS
        // LZCNT returns index starting from MSB, whereas BSR gives the index from LSB.
        // 31 ^ BSR here is equivalent to 31 - BSR since the BSR result is always between 0 and 31.
        // This saves an instruction, as subtraction from constant requires either MOV/SUB or NEG/ADD.

        var result = BitScanReverse(value);
        return 31 ^ result;
#else
        var result = BitOperations.LeadingZeroCount(value);
        return (uint)result;
#endif
    }

    public static uint LeadingZeroCount(ulong value)
    {
        if (value == 0)
        {
            return 64;
        }

#if HOST_WINDOWS
        // LZCNT returns index starting from MSB, whereas BSR gives the index from LSB.
        // 63 ^ BSR here is equivalent to 63 - BSR since the BSR result is always between 0 and 63.
        // This saves an instruction, as subtraction from constant requires either MOV/SUB or NEG/ADD.

        var result = BitScanReverse(value);
        return 63 ^ result;
#else
        var result = BitOperations.LeadingZeroCount(value);
        return (uint)result;
#endif
    }

    public static uint Log2(uint value)
    {
        // The 0->0 contract is fulfilled by setting the LSB to 1.
        // Log(1) is 0, and setting the LSB for values > 1 does not change the log2 result.
        return 31 ^ LeadingZeroCount(value | 1);
    }

    public static uint Log2(ulong value)
    {
        // The 0->0 contract is fulfilled by setting the LSB to 1.
        // Log(1) is 0, and setting the LSB for values > 1 does not change the log2 result.
        return 63 ^ LeadingZeroCount(value | 1);
    }

    public static uint TrailingZeroCount(uint value)
    {
        if (value == 0)
        {
            return 32;
        }

#if HOST_WINDOWS
        return BitScanForward(value);
#else
        var result = BitOperations.TrailingZeroCount(value);
        return (uint)result;
#endif
    }

    public static uint TrailingZeroCount(ulong value)
    {
        if (value == 0)
        {
            return 64;
        }

#if HOST_WINDOWS
        return BitScanForward(value);
#else
        var result = BitOperations.TrailingZeroCount(value);
        return (uint)result;
#endif
    }
}
