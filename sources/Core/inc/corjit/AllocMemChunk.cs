// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct AllocMemChunk
{
    // Alignment of the chunk. Must be a power of two with the following restrictions:
    // - For the hot code chunk the max supported alignment == 32.
    // - For the cold code chunk the value must always be 1.
    // - For read-only data chunks the max supported alignment == 64.
    public int alignment;

    public int size;

    public CorJitAllocMemFlag flags;

    // out
    public unsafe byte* block;

    public unsafe byte* blockRW;
}
