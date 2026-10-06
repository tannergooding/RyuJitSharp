// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using NUnit.Framework;
using static RyuJitSharp.insFlags;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;

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

    [TestCase(0, 0u)]
    [TestCase(1, 0x40u)]
    [TestCase(3, 0xC0u)]
    [TestCase(4, 0x1000u)]
    [TestCase(5, 0x1040u)]
    [TestCase(31, 0x70C0u)]
    public static void ShiftCountEncodingMatchesThumb2(int imm, uint expected)
    {
        Assert.That(Emitter.insEncodeShiftCount(imm), Is.EqualTo(expected));
    }

    [TestCase(0x000, 0x0000u)]
    [TestCase(0x01F, 0x001Fu)]
    [TestCase(0x020, 0x0040u)]
    [TestCase(0x040, 0x0080u)]
    [TestCase(0x060, 0x00C0u)]
    [TestCase(0x080, 0x1000u)]
    [TestCase(0x100, 0x2000u)]
    [TestCase(0x200, 0x4000u)]
    [TestCase(0x3FF, 0x70DFu)]
    public static void BitFieldImmediateEncodingMatchesThumb2(int imm, uint expected)
    {
        Assert.That(Emitter.insEncodeBitFieldImm(imm), Is.EqualTo(expected));
    }

    [TestCase(0x0000, 0x00000000u)]
    [TestCase(0x00FF, 0x000000FFu)]
    [TestCase(0x0100, 0x00001000u)]
    [TestCase(0x0700, 0x00007000u)]
    [TestCase(0x0800, 0x04000000u)]
    [TestCase(0x1000, 0x00010000u)]
    [TestCase(0xF000, 0x000F0000u)]
    [TestCase(0xFFFF, 0x040F70FFu)]
    public static void MovImmediateEncodingMatchesThumb2(int imm, uint expected)
    {
        Assert.That(Emitter.insEncodeImmT2_Mov(imm), Is.EqualTo(expected));
    }

    [TestCase(INS_ldr, 0, 0)]
    [TestCase(INS_ldr, 124, 31)]
    [TestCase(INS_ldr, -4, -1)]
    [TestCase(INS_str, 124, 31)]
    [TestCase(INS_str, -4, -1)]
    [TestCase(INS_ldrh, 0, 0)]
    [TestCase(INS_ldrh, 62, 31)]
    [TestCase(INS_ldrh, -2, -1)]
    [TestCase(INS_strh, 62, 31)]
    [TestCase(INS_strh, -2, -1)]
    [TestCase(INS_ldrb, 31, 31)]
    [TestCase(INS_strb, 31, 31)]
    [TestCase(INS_lsl, 31, 31)]
    [TestCase(INS_lsr, 31, 31)]
    [TestCase(INS_asr, 31, 31)]
    public static void Thumb1ImmediateUnscalingMatchesInstruction(instruction ins, int imm, int expected)
    {
        Assert.That(Emitter.insUnscaleImm(ins, imm), Is.EqualTo(expected));
    }

    [TestCase(INS_OPTS_NONE, -1, 0x01000000u)]
    [TestCase(INS_OPTS_NONE, 0, 0x01800000u)]
    [TestCase(INS_OPTS_LDST_PRE_DEC, -1, 0x01200000u)]
    [TestCase(INS_OPTS_LDST_PRE_DEC, 0, 0x01A00000u)]
    [TestCase(INS_OPTS_LDST_POST_INC, -1, 0x00200000u)]
    [TestCase(INS_OPTS_LDST_POST_INC, 0, 0x00A00000u)]
    public static void G0AddressModeEncodingMatchesThumb2(insOpts options, int imm, uint expected)
    {
        Assert.That(Emitter.insEncodePUW_G0(options, imm), Is.EqualTo(expected));
    }

    [TestCase(INS_OPTS_NONE, -1, 0x400u)]
    [TestCase(INS_OPTS_NONE, 0, 0x600u)]
    [TestCase(INS_OPTS_LDST_PRE_DEC, -1, 0x500u)]
    [TestCase(INS_OPTS_LDST_PRE_DEC, 0, 0x700u)]
    [TestCase(INS_OPTS_LDST_POST_INC, -1, 0x100u)]
    [TestCase(INS_OPTS_LDST_POST_INC, 0, 0x300u)]
    public static void H0AddressModeEncodingMatchesThumb2(insOpts options, int imm, uint expected)
    {
        Assert.That(Emitter.insEncodePUW_H0(options, imm), Is.EqualTo(expected));
    }
}
#endif
