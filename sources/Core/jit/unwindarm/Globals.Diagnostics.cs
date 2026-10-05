// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM && DEBUG
namespace RyuJitSharp;

public static partial class Globals
{
    // start is a zero-based bit index from the least significant bit.
    public static uint ExtractBits(uint dw, uint start, uint length)
    {
        return (dw >> (int)start) & unchecked((uint)((1 << (int)length) - 1));
    }
}
#endif
