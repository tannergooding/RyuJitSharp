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
        OPSZ32,
        OPSZ64,
        OPSZ_SCALABLE = OPSZ32,
    }

    // The size tag at index 5 means 32 bytes on xarch and scalable on ARM64.
    private static ReadOnlySpan<emitAttr> emitSizeDecode =>
    [
        EA_1BYTE, EA_2BYTE, EA_4BYTE, EA_8BYTE, EA_16BYTE,
        EA_SCALABLE_ARM64, (emitAttr)64,
    ];

    private static opSize emitEncodeSize(emitAttr size)
    {
        assert((size != EA_UNKNOWN) && (EA_SIZE(size) == size));
        return (opSize)BitOperations.Log2((uint)size);
    }

    private static emitAttr emitDecodeSize(opSize size)
    {
        var count = Configuration.TARGET_XARCH ? 7u : Configuration.TARGET_ARM64 ? 6u : 5u;
        assert((uint)size < count);
        return emitSizeDecode[(int)size];
    }
}
