// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public struct AllocMemArgs
{
    // Chunks to allocate. Supports one hot code chunk, one cold code chunk, and an arbitrary number of data chunks.
    public unsafe AllocMemChunk* chunks;

    public int chunksCount;

    public int xcptnsCount;
}
