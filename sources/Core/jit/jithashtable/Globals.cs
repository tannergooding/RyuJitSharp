// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Globals
{
    // Pinned jithashtable.cpp selects roughly doubling divisors with 32-bit magic
    // numbers; see Hacker's Delight, chapter 10.9. The first entry is deliberately 9.
    private static readonly JitPrimeInfo[] s_jitPrimeInfo = [
        new JitPrimeInfo(9, 0x38e38e39, 1),
        new JitPrimeInfo(23, 0xb21642c9, 4),
        new JitPrimeInfo(59, 0x22b63cbf, 3),
        new JitPrimeInfo(131, 0xfa232cf3, 7),
        new JitPrimeInfo(239, 0x891ac73b, 7),
        new JitPrimeInfo(433, 0x975a751, 4),
        new JitPrimeInfo(761, 0x561e46a5, 8),
        new JitPrimeInfo(1399, 0xbb612aa3, 10),
        new JitPrimeInfo(2473, 0x6a009f01, 10),
        new JitPrimeInfo(4327, 0xf2555049, 12),
        new JitPrimeInfo(7499, 0x45ea155f, 11),
        new JitPrimeInfo(12973, 0x1434f6d3, 10),
        new JitPrimeInfo(22433, 0x2ebe18db, 12),
        new JitPrimeInfo(46559, 0xb42bebd5, 15),
        new JitPrimeInfo(96581, 0xadb61b1b, 16),
        new JitPrimeInfo(200341, 0x29df2461, 15),
        new JitPrimeInfo(415517, 0xa181c46d, 18),
        new JitPrimeInfo(861719, 0x4de0bde5, 18),
        new JitPrimeInfo(1787021, 0x9636c46f, 20),
        new JitPrimeInfo(3705617, 0x4870adc1, 20),
        new JitPrimeInfo(7684087, 0x8bbc5b83, 22),
        new JitPrimeInfo(15933877, 0x86c65361, 23),
        new JitPrimeInfo(33040633, 0x40fec79b, 23),
        new JitPrimeInfo(68513161, 0x7d605cd1, 25),
        new JitPrimeInfo(142069021, 0xf1da390b, 27),
        new JitPrimeInfo(294594427, 0x74a2507d, 27),
        new JitPrimeInfo(733045421, 0x5dbec447, 28),
    ];

    public static ReadOnlySpan<JitPrimeInfo> jitPrimeInfo => s_jitPrimeInfo;
}
