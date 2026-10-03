// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static class XarchImulHelperTests
{
#if IMUL_TEST_AMD64
    [Test]
    public static void ImulOpcodesPreserveEveryIntegerRegisterNumber()
    {
        for (var reg = REG_RAX; reg <= REG_R31; reg++)
        {
            var ins = Emitter.inst3opImulForReg(reg);
            Assert.That((int)ins - (int)INS_imul_AX, Is.EqualTo((int)reg));
        }
    }
#elif IMUL_TEST_X86
    [TestCase(REG_EAX, INS_imul_AX)]
    [TestCase(REG_EBX, INS_imul_BX)]
    [TestCase(REG_ECX, INS_imul_CX)]
    [TestCase(REG_EDX, INS_imul_DX)]
    [TestCase(REG_EBP, INS_imul_BP)]
    [TestCase(REG_ESI, INS_imul_SI)]
    [TestCase(REG_EDI, INS_imul_DI)]
    public static void ImulOpcodesPreserveEachX86RegisterNumber(regNumber reg, instruction expected)
    {
        Assert.That(Emitter.inst3opImulForReg(reg), Is.EqualTo(expected));
    }
#endif
}
