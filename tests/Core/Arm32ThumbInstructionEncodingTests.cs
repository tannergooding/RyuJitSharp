// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using NUnit.Framework;
using static RyuJitSharp.insFlags;
using static RyuJitSharp.insOpts;

namespace RyuJitSharp.UnitTests;

internal static class Arm32ThumbInstructionEncodingTests
{
    [TestCase(INS_FLAGS_NOT_SET, 0u)]
    [TestCase(INS_FLAGS_SET, 0x00100000u)]
    [TestCase(INS_FLAGS_DONT_CARE, 0u)]
    public static void SetFlagsEncodingMatchesThumb2(insFlags flags, uint expected)
    {
        Assert.That(Emitter.insEncodeSetFlags(flags), Is.EqualTo(expected));
    }

    [TestCase(INS_OPTS_NONE, 0u)]
    [TestCase(INS_OPTS_LSL, 0u)]
    [TestCase(INS_OPTS_LSR, 0x10u)]
    [TestCase(INS_OPTS_ASR, 0x20u)]
    [TestCase(INS_OPTS_ROR, 0x30u)]
    [TestCase(INS_OPTS_RRX, 0x30u)]
    public static void ShiftTypeEncodingMatchesThumb2(insOpts options, uint expected)
    {
        Assert.That(Emitter.insEncodeShiftOpts(options), Is.EqualTo(expected));
    }
}
#endif
