// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
using System.Numerics;

namespace RyuJitSharp;

public partial class Emitter
{
    public static int SizeOfSLEB128(long value)
    {
        var signAdjustedValue = unchecked((ulong)(value ^ (value >> 63))) | 1UL;
        var significantBits = 1 + 6 + 64 - BitOperations.LeadingZeroCount(signAdjustedValue);
        return (significantBits * 37) >> 8;
    }
}
#endif
