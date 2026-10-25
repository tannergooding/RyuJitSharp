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

        return (uint)BitOperations.TrailingZeroCount(value);
    }

    public static uint BitScanForward(ulong value)
    {
        assert(value != 0);

        if (Configuration.HOST_WINDOWS && !Configuration.HOST_64BIT && (value == 0))
        {
            return 32 + BitScanForward(0U);
        }

        return (uint)BitOperations.TrailingZeroCount(value);
    }

    public static uint LeadingZeroCount(uint value) => (uint)BitOperations.LeadingZeroCount(value);

    public static uint LeadingZeroCount(ulong value) => (uint)BitOperations.LeadingZeroCount(value);

    public static uint Log2(uint value) => (uint)BitOperations.Log2(value);

    public static uint Log2(ulong value) => (uint)BitOperations.Log2(value);

    public static uint TrailingZeroCount(uint value) => (uint)BitOperations.TrailingZeroCount(value);

    public static uint TrailingZeroCount(ulong value) => (uint)BitOperations.TrailingZeroCount(value);
}
