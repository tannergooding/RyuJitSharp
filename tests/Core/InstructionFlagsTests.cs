// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.insFlags;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static class InstructionFlagsTests
{
    [Test]
    public static void FlagsTableCoversEveryInstructionWithTheFullNativeBitWidth()
    {
        Assert.That(CodeGen.instInfo.Length, Is.EqualTo((int)INS_count));
        insFlags usedFlags = 0;
        for (instruction ins = 0; ins < INS_count; ins++)
        {
            usedFlags |= CodeGen.instInfo[(int)ins];
            _ = Emitter.DoesWriteZeroFlagForResult(ins);
        }

        Assert.That((ulong)usedFlags >> 53, Is.Zero);
        Assert.That(usedFlags & INS_FLAGS_HasPseudoName, Is.EqualTo(INS_FLAGS_HasPseudoName));
        Assert.That((ulong)INS_FLAGS_HasPseudoName, Is.EqualTo(1UL << 52));
        Assert.That((ulong)Writes_ZF, Is.EqualTo(1UL << 8));
    }

    [TestCase(INS_add, Writes_OF | Writes_SF | Writes_ZF | Writes_AF | Writes_PF | Writes_CF |
        INS_FLAGS_HasSBit | INS_FLAGS_HasWBit | Encoding_REX2 | INS_FLAGS_HasNDD | INS_FLAGS_HasNF)]
    [TestCase(INS_mov, INS_FLAGS_HasWBit | Encoding_REX2)]
    [TestCase(INS_addps, Input_32Bit | KMask_Base4 | REX_W0_EVEX | Encoding_VEX | Encoding_EVEX |
        INS_FLAGS_IsDstDstSrcAVXInstruction | INS_FLAGS_IsAvxCommutative)]
    [TestCase(INS_movdqa32, REX_W0_EVEX | Encoding_VEX | Encoding_EVEX | Encoding_REX2 | INS_FLAGS_HasPseudoName)]
    public static void TableEntriesPreserveAllNativeFlags(instruction ins, insFlags expected)
    {
        Assert.That(CodeGen.instInfo[(int)ins], Is.EqualTo(expected));
    }

    [TestCase(INS_add, true)]
    [TestCase(INS_xor, true)]
    [TestCase(INS_mov, false)]
    [TestCase(INS_addps, false)]
    [TestCase(INS_invalid, false)]
    [TestCase(INS_rol, false)]
    public static void ZeroFlagResultSupportComesFromInstructionMetadata(instruction ins, bool expected)
    {
        Assert.That(Emitter.DoesWriteZeroFlagForResult(ins), Is.EqualTo(expected));
    }

    [TestCase(INS_bsf)]
    [TestCase(INS_bsr)]
    public static void BitScansWriteZeroFlagForTheirSourceNotTheirResult(instruction ins)
    {
        Assert.That(CodeGen.instInfo[(int)ins] & Writes_ZF, Is.EqualTo(Writes_ZF));
        Assert.That(Emitter.DoesWriteZeroFlagForResult(ins), Is.False);
    }
}
