// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using NUnit.Framework;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.insFlags;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm32ThumbInstructionEncodingTests
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

    [TestCase(REG_R0, 0u, 0u, 0u, 0u)]
    [TestCase(REG_R7, 0x7000u, 0x0700u, 7u, 0x00070000u)]
    [TestCase(REG_PC, 0xF000u, 0x0F00u, 15u, 0x000F0000u)]
    public static void Thumb2GeneralRegisterEncodingsMatchField(regNumber reg, uint expectedT, uint expectedD, uint expectedM, uint expectedN)
    {
        Assert.That(Emitter.insEncodeRegT2_T(reg), Is.EqualTo(expectedT));
        Assert.That(Emitter.insEncodeRegT2_D(reg), Is.EqualTo(expectedD));
        Assert.That(Emitter.insEncodeRegT2_M(reg), Is.EqualTo(expectedM));
        Assert.That(Emitter.insEncodeRegT2_N(reg), Is.EqualTo(expectedN));
    }

    [TestCase(REG_F0, (int)EA_4BYTE, false, 0u, 0u)]
    [TestCase(REG_F31, (int)EA_4BYTE, false, 31u, 31u)]
    [TestCase(REG_F1, (int)EA_4BYTE, true, 1u, 16u)]
    [TestCase(REG_F17, (int)EA_4BYTE, true, 17u, 24u)]
    [TestCase(REG_F8, (int)EA_8BYTE, false, 4u, 4u)]
    [TestCase(REG_F30, (int)EA_8BYTE, true, 15u, 15u)]
    public static void Thumb2FloatRegisterIndexAndEncodingMatchInstruction(regNumber reg, int size, bool variant, uint expectedIndex, uint expectedEncoding)
    {
        var index = Emitter.floatRegIndex(reg, size);
        Assert.That(index, Is.EqualTo(expectedIndex));
        Assert.That(Emitter.floatRegEncoding(index, size, variant), Is.EqualTo(expectedEncoding));
    }

    [TestCase(REG_F0, (int)EA_4BYTE, false, 0u, 0u, 0u)]
    [TestCase(REG_F1, (int)EA_4BYTE, true, 0x20u, 0x80u, 0x00400000u)]
    [TestCase(REG_F17, (int)EA_4BYTE, true, 0x28u, 0x00080080u, 0x00408000u)]
    [TestCase(REG_F31, (int)EA_4BYTE, false, 0x2Fu, 0x000F0080u, 0x0040F000u)]
    [TestCase(REG_F8, (int)EA_8BYTE, true, 4u, 0x00040000u, 0x00004000u)]
    [TestCase(REG_F30, (int)EA_8BYTE, true, 15u, 0x000F0000u, 0x0000F000u)]
    public static void Thumb2VectorRegisterEncodingsMatchInstruction(regNumber reg, int size, bool variant, uint expectedM, uint expectedN, uint expectedD)
    {
        Assert.That(Emitter.insEncodeRegT2_VectorM(reg, size, variant), Is.EqualTo(expectedM));
        Assert.That(Emitter.insEncodeRegT2_VectorN(reg, size, variant), Is.EqualTo(expectedN));
        Assert.That(Emitter.insEncodeRegT2_VectorD(reg, size, variant), Is.EqualTo(expectedD));
    }

    [Test]
    public static void Thumb1InstructionOutputUsesWritableAlias()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var buffer = stackalloc byte[24];
            for (var index = 0; index < 24; index++)
            {
                buffer[index] = 0xA5;
            }

            emitter.writeableOffset = 8;
            var count = emitter.emitOutput_Thumb1Instr(buffer + 3, 0x1234);

            Assert.That(count, Is.EqualTo((uint)sizeof(short)));
            Assert.That(buffer[11], Is.EqualTo(0x34));
            Assert.That(buffer[12], Is.EqualTo(0x12));
            for (var index = 0; index < 24; index++)
            {
                if (index is < 11 or >= 13)
                {
                    Assert.That(buffer[index], Is.EqualTo(0xA5));
                }
            }
        });
    }

    [Test]
    public static void Thumb2InstructionOutputUsesWritableAliasAndHalfwordOrder()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var buffer = stackalloc byte[24];
            for (var index = 0; index < 24; index++)
            {
                buffer[index] = 0xA5;
            }

            emitter.writeableOffset = 8;
            var count = emitter.emitOutput_Thumb2Instr(buffer + 3, 0xE8001234);

            Assert.That(count, Is.EqualTo((uint)(sizeof(short) * 2)));
            Assert.That(buffer[11], Is.EqualTo(0x00));
            Assert.That(buffer[12], Is.EqualTo(0xE8));
            Assert.That(buffer[13], Is.EqualTo(0x34));
            Assert.That(buffer[14], Is.EqualTo(0x12));
            for (var index = 0; index < 24; index++)
            {
                if (index is < 11 or >= 15)
                {
                    Assert.That(buffer[index], Is.EqualTo(0xA5));
                }
            }
        });
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
