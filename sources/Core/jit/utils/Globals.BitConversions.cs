// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class Globals
{
    public static ulong DoubleToUInt64Bits(double value)
    {
        return BitConverter.DoubleToUInt64Bits(value);
    }

    public static uint SingleToUInt32Bits(float value)
    {
        return BitConverter.SingleToUInt32Bits(value);
    }

    public static float UInt32BitsToSingle(uint value)
    {
        return BitConverter.UInt32BitsToSingle(value);
    }

    public static double UInt64BitsToDouble(ulong value)
    {
        return BitConverter.UInt64BitsToDouble(value);
    }
}
