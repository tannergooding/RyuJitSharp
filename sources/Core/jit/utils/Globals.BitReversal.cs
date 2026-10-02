// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
    public static uint ReverseBits(uint value)
    {
        // Staged swaps from the Stanford Bit Twiddling Hacks:
        // http://graphics.stanford.edu/~seander/bithacks.html
        var result = value;

        result = ((result >> 1) & 0x55555555U) | ((result & 0x55555555U) << 1);
        result = ((result >> 2) & 0x33333333U) | ((result & 0x33333333U) << 2);
        result = ((result >> 4) & 0x0F0F0F0FU) | ((result & 0x0F0F0F0FU) << 4);
        result = ((result >> 8) & 0x00FF00FFU) | ((result & 0x00FF00FFU) << 8);
        result = (result >> 16) | (result << 16);

        return result;
    }

    public static ulong ReverseBits(ulong value)
    {
        // Exchange successively larger adjacent bit groups, then the two 32-bit halves.
        var result = value;

        result = ((result >> 1) & 0x5555555555555555UL) | ((result & 0x5555555555555555UL) << 1);
        result = ((result >> 2) & 0x3333333333333333UL) | ((result & 0x3333333333333333UL) << 2);
        result = ((result >> 4) & 0x0F0F0F0F0F0F0F0FUL) | ((result & 0x0F0F0F0F0F0F0F0FUL) << 4);
        result = ((result >> 8) & 0x00FF00FF00FF00FFUL) | ((result & 0x00FF00FF00FF00FFUL) << 8);
        result = ((result >> 16) & 0x0000FFFF0000FFFFUL) | ((result & 0x0000FFFF0000FFFFUL) << 16);
        result = (result >> 32) | (result << 32);

        return result;
    }
}
