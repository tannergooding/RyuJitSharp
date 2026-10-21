// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial struct GCInfo
{
}

internal static class GCInfoJit32HeaderTable
{
    [Flags]
    private enum InfoHdrFlags : ushort
    {
        None = 0,
        EpilogAtEnd = 1 << 0,
        EdiSaved = 1 << 1,
        EsiSaved = 1 << 2,
        EbxSaved = 1 << 3,
        EbpSaved = 1 << 4,
        EbpFrame = 1 << 5,
        Interruptible = 1 << 6,
        Security = 1 << 7,
        Handlers = 1 << 8,
    }

    private static ReadOnlySpan<byte> PrologSizes => [
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 2, 2, 2, 2, 2, 2,
        2, 2, 2, 2, 2, 2, 2, 2,
        2, 2, 2, 2, 2, 2, 3, 3,
        3, 3, 3, 3, 3, 3, 3, 3,
        3, 3, 3, 3, 4, 4, 4, 4,
        4, 4, 4, 4, 4, 4, 5, 5,
        5, 5, 5, 5, 5, 5, 5, 6,
        6, 6, 6, 6, 6, 6, 7, 7,
        7, 7, 7, 7, 7, 7, 7, 7,
        7, 7, 8, 8, 9, 9, 9, 10,
        10, 11, 12, 13, 13, 13, 14, 16,
        16, 16, 16, 16, 16, 16, 16, 16,
        16, 16, 19, 24, 45, 51, 51, 51,
    ];

    private static ReadOnlySpan<byte> EpilogSizes => [
        1, 1, 1, 1, 3, 3, 3, 3,
        3, 3, 3, 3, 0, 0, 2, 2,
        2, 2, 2, 2, 2, 4, 4, 4,
        4, 4, 3, 3, 3, 3, 3, 3,
        3, 3, 3, 3, 5, 5, 5, 5,
        5, 5, 5, 6, 7, 7, 4, 4,
        4, 4, 4, 4, 6, 6, 6, 6,
        6, 6, 6, 8, 5, 5, 5, 5,
        5, 7, 7, 7, 7, 7, 4, 6,
        6, 6, 6, 6, 6, 6, 8, 0,
        0, 4, 4, 6, 6, 7, 4, 5,
        5, 8, 8, 8, 8, 10, 10, 10,
        10, 10, 3, 6, 4, 7, 7, 5,
        5, 6, 5, 8, 8, 10, 7, 7,
        7, 7, 7, 7, 7, 7, 7, 7,
        9, 9, 7, 7, 9, 7, 7, 7,
    ];

    private static ReadOnlySpan<byte> EpilogCounts => [
        1, 1, 2, 3, 1, 1, 1, 1,
        1, 1, 1, 1, 0, 0, 1, 1,
        1, 1, 1, 2, 3, 1, 1, 1,
        1, 2, 1, 1, 1, 1, 1, 1,
        1, 1, 2, 3, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 2, 2, 1, 1, 1, 1,
        1, 1, 2, 1, 1, 1, 1, 1,
        2, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 0,
        0, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 2, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 2,
        1, 1, 1, 1, 1, 1, 1, 1,
    ];

    private static ReadOnlySpan<ushort> ShortcutFlags => [
        0, 1, 0, 0, 1, 1, 1, 1,
        1, 1, 1, 1, 0, 0, 0, 4,
        1, 9, 5, 4, 4, 5, 5, 5,
        5, 4, 4, 12, 6, 70, 5, 5,
        13, 7, 6, 6, 6, 6, 7, 7,
        7, 7, 7, 95, 318, 319, 6, 14,
        7, 15, 6, 14, 14, 14, 15, 15,
        15, 15, 14, 94, 14, 30, 15, 31,
        30, 30, 30, 31, 31, 31, 79, 30,
        7, 7, 31, 31, 31, 31, 31, 62,
        126, 48, 49, 54, 55, 15, 78, 52,
        52, 30, 31, 31, 30, 30, 31, 31,
        31, 31, 5, 55, 7, 63, 319, 5,
        15, 7, 181, 94, 31, 94, 127, 318,
        319, 319, 319, 319, 319, 319, 319, 318,
        319, 319, 383, 319, 63, 62, 63, 63,
    ];

    private static ReadOnlySpan<byte> ArgCounts => [
        0, 0, 0, 0, 1, 1, 2, 2,
        2, 3, 4, 4, 0, 1, 0, 0,
        0, 0, 0, 0, 0, 1, 1, 2,
        2, 1, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 1, 2, 1, 1,
        2, 2, 2, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 1, 2, 1, 1,
        2, 2, 1, 0, 0, 0, 0, 0,
        0, 1, 2, 1, 1, 2, 0, 0,
        0, 0, 0, 0, 1, 2, 1, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 2, 1, 1,
        2, 2, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 2, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        1, 2, 0, 0, 1, 0, 0, 0,
    ];

    private static ReadOnlySpan<byte> FrameSizes => [
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 1, 1, 1, 0,
        1, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 1, 0, 0, 0, 1, 1,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 1, 0, 1, 1, 0,
        1, 0, 1, 0, 0, 0, 0, 0,
        0, 0, 0, 9, 1, 0, 1, 0,
        0, 0, 0, 0, 0, 0, 0, 1,
        2, 4, 1, 1, 1, 1, 1, 0,
        0, 2, 2, 1, 1, 2, 0, 3,
        5, 3, 2, 2, 3, 6, 2, 2,
        2, 3, 1, 2, 1, 2, 0, 4,
        1, 4, 1, 9, 2, 6, 1, 4,
        0, 1, 3, 4, 4, 5, 6, 4,
        4, 4, 5, 5, 0, 13, 1, 11,
    ];

    private static ReadOnlySpan<byte> UntrackedCounts => [
        0, 0, 0, 0, 0, 1, 0, 1,
        2, 0, 0, 1, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 1, 0,
        2, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 1,
        0, 1, 2, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 1,
        0, 2, 0, 2, 0, 0, 0, 0,
        0, 0, 0, 0, 1, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 1, 0, 1,
        0, 0, 1, 0, 1, 0, 0, 1,
        1, 1, 0, 0, 1, 1, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        1, 1, 0, 0, 0, 0, 0, 0,
    ];

    private static ReadOnlySpan<sbyte> VarPtrTableSizes => [
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, -1, -1, -1, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, -1, 0, 0, 0, 0, -1,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, -1, -1, -1, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, -1, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, -1, -1, 0, 0, 0,
        0, -1, -1, 0, 0, 0, 0, -1,
        -1, -1, 0, -1, -1, -1, 0, -1,
        0, -1, 0, 0, 0, 0, -1, 0,
        0, 0, 0, -1, 0, -1, -1, -1,
        -1, -1, 0, 0, -1, -1, -1, -1,
        -1, -1, -1, -1, 0, -1, -1, 0,
    ];

    internal static int InfoHdrShortcutCount => PrologSizes.Length;

    private static byte GetFlag(ushort flags, InfoHdrFlags flag)
        => (flags & (ushort)flag) != 0 ? (byte)1 : (byte)0;

    internal static GCInfo.InfoHdrSmall GetInfoHdrShortcut(int index)
    {
        var flags = ShortcutFlags[index];
        return new(
            PrologSizes[index], EpilogSizes[index], EpilogCounts[index],
            GetFlag(flags, InfoHdrFlags.EpilogAtEnd),
            GetFlag(flags, InfoHdrFlags.EdiSaved), GetFlag(flags, InfoHdrFlags.EsiSaved),
            GetFlag(flags, InfoHdrFlags.EbxSaved), GetFlag(flags, InfoHdrFlags.EbpSaved),
            GetFlag(flags, InfoHdrFlags.EbpFrame), GetFlag(flags, InfoHdrFlags.Interruptible),
            0, GetFlag(flags, InfoHdrFlags.Security), GetFlag(flags, InfoHdrFlags.Handlers),
            0, 0, 0, 0, 0, 0, 0, 0,
            ArgCounts[index], FrameSizes[index], UntrackedCounts[index],
            unchecked((uint)VarPtrTableSizes[index]));
    }
}
