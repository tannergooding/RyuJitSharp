// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Numerics;

namespace RyuJitSharp;

public partial class Emitter
{
    private enum opSize : uint
    {
        OPSZ1,
        OPSZ2,
        OPSZ4,
        OPSZ8,
        OPSZ16,
#if TARGET_XARCH
        OPSZ32,
        OPSZ64,
#elif TARGET_ARM64
        OPSZ_SCALABLE,
#endif
        OPSZ_COUNT,
#if TARGET_AMD64
        OPSZP = OPSZ8,
#else
        OPSZP = OPSZ4,
#endif
    }

    private static ReadOnlySpan<emitAttr> emitSizeDecode =>
    [
        EA_1BYTE, EA_2BYTE, EA_4BYTE, EA_8BYTE, EA_16BYTE,
#if TARGET_XARCH
        EA_32BYTE, EA_64BYTE,
#elif TARGET_ARM64
        EA_SCALABLE,
#endif
    ];

    private static opSize emitEncodeSize(emitAttr size)
    {
        assert((size != EA_UNKNOWN) && ((size & EA_SIZE_MASK) == size));
        return (opSize)BitOperations.Log2((uint)size);
    }

    private static emitAttr emitDecodeSize(opSize size)
    {
        assert(size < opSize.OPSZ_COUNT);
        return emitSizeDecode[(int)size];
    }
}
