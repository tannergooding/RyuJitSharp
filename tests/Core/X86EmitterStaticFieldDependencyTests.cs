// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_X86
using System;
using NUnit.Framework;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static class X86EmitterStaticFieldDependencyTests
{
    [TestCase(INS_add, EA_1BYTE, REG_EAX, REG_NA, true)]
    [TestCase(INS_add, EA_1BYTE, REG_EBX, REG_ECX, true)]
    [TestCase(INS_add, EA_1BYTE, REG_ESI, REG_NA, false)]
    [TestCase(INS_add, EA_1BYTE, REG_EAX, REG_EDI, false)]
    [TestCase(INS_add, EA_4BYTE, REG_ESI, REG_EDI, true)]
    [TestCase(INS_movsx, EA_1BYTE, REG_ESI, REG_NA, true)]
    [TestCase(INS_movzx, EA_1BYTE, REG_ESI, REG_EDI, false)]
    public static void ByteOperandsRequireEncodableRegisters(instruction ins, emitAttr size, regNumber reg1,
        regNumber reg2, bool expected)
    {
        Assert.That(Emitter.emitVerifyEncodable(ins, size, reg1, reg2), Is.EqualTo(expected));
    }

#if FEATURE_HW_INTRINSICS
    [Test]
    public static void Crc32AllowsNonByteDestination()
    {
        Assert.That(Emitter.emitVerifyEncodable(INS_crc32, EA_1BYTE, REG_ESI), Is.True);
    }
#endif

    [TestCase(REG_EAX, EA_1BYTE)]
    [TestCase(REG_ESP, EA_1BYTE)]
    [TestCase(REG_ESI, EA_4BYTE)]
    [TestCase(REG_XMM7, EA_16BYTE)]
    public static void X86HasNoExtendedRegisters(regNumber reg, emitAttr attr)
    {
        Assert.That(Emitter.IsExtendedReg(reg), Is.False);
        Assert.That(Emitter.IsExtendedReg(reg, attr), Is.False);
    }

    [TestCase(INS_add, -128L, true)]
    [TestCase(INS_add, 127L, true)]
    [TestCase(INS_add, -129L, false)]
    [TestCase(INS_add, 128L, false)]
    [TestCase(INS_mov, 1L, false)]
    [TestCase(INS_test, 1L, false)]
    public static void SignedByteImmediateRequiresTargetWidthAndAnEncodableInstruction(
        instruction ins, long value, bool expected)
    {
        Assert.That(Emitter.ImmCanUseSByteEncoding(ins, unchecked((nint)value)), Is.EqualTo(expected));
    }

    [Test]
    public static void CrossTargetImmediateMustFitX86Width()
    {
        if (IntPtr.Size == 8)
        {
            Assert.That(Emitter.ImmCanUseSByteEncoding(INS_add, unchecked((nint)0x1_0000_007FL)), Is.False);
        }
    }
}
#endif
