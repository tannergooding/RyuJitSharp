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

        return (uint)BitOperations.Log2(value);
    }

    public static uint BitScanReverse(ulong value)
    {
        assert(value != 0);

        if (Configuration.HOST_WINDOWS && !Configuration.HOST_64BIT && (value == 0))
        {
            return BitScanReverse(0U);
        }

        return (uint)BitOperations.Log2(value);
    }
}
