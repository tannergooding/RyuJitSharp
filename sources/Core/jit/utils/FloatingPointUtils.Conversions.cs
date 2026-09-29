// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public static partial class FloatingPointUtils
{
    public static float convertToSingle(double value)
    {
        if ((RuntimeInformation.ProcessArchitecture == Architecture.RiscV64) && double.IsNaN(value))
        {
            // RISC-V casts canonicalize NaNs; native preserves their sign and payload.
            var bits = BitConverter.DoubleToUInt64Bits(value);
            var payload = unchecked((uint)(bits >> 29)) & ((1u << 23) - 1);
            var result = ((uint)(bits >> 63) << 31) | 0x7F800000u | payload;

            return BitConverter.UInt32BitsToSingle(result);
        }

        return (float)value;
    }
}
