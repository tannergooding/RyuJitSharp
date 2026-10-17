// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static class XarchImulHelperTests
{
    [Test]
    public static void ImulOpcodesPreserveEveryIntegerRegisterNumber()
    {
        Assume.That(Configuration.TARGET_AMD64, "requires AMD64 target semantics");

        for (var reg = REG_X64_RAX; reg <= REG_X64_R31; reg++)
        {
            var ins = Emitter.inst3opImulForReg(reg);
            Assert.That((int)ins - (int)INS_imul_AX, Is.EqualTo((int)reg));
        }
    }

    [TestCase(REG_X86_EAX, INS_imul_AX)]
    [TestCase(REG_X86_EBX, INS_imul_BX)]
    [TestCase(REG_X86_ECX, INS_imul_CX)]
    [TestCase(REG_X86_EDX, INS_imul_DX)]
    [TestCase(REG_X86_EBP, INS_imul_BP)]
    [TestCase(REG_X86_ESI, INS_imul_SI)]
    [TestCase(REG_X86_EDI, INS_imul_DI)]
    public static void ImulOpcodesPreserveEachX86RegisterNumber(regNumber reg, instruction expected)
    {
        Assume.That(Configuration.TARGET_X86, "requires x86 target semantics");

        Assert.That(Emitter.inst3opImulForReg(reg), Is.EqualTo(expected));
    }
}
