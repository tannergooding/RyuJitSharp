// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static class AbiShadowSpaceTests
{
    [TestCase(REG_RCX, 0)]
    [TestCase(REG_XMM0, 0)]
    [TestCase(REG_RDX, 8)]
    [TestCase(REG_XMM1, 8)]
    [TestCase(REG_R8, 16)]
    [TestCase(REG_XMM2, 16)]
    [TestCase(REG_R9, 24)]
    [TestCase(REG_XMM3, 24)]
    public static void RegisterPairsShareTheirCallerHome(regNumber reg, int expectedOffset)
    {
        Assert.That(AbiPassingInformation.GetShadowSpaceCallerOffsetForReg(reg, out var offset), Is.True);
        Assert.That(offset, Is.EqualTo(expectedOffset));
    }

    [TestCase(REG_RAX)]
    [TestCase(REG_R10)]
    [TestCase(REG_RSP)]
    [TestCase(REG_XMM4)]
    [TestCase(REG_NA)]
    public static void RegistersWithoutCallerHomesAreRejected(regNumber reg)
    {
        Assert.That(AbiPassingInformation.GetShadowSpaceCallerOffsetForReg(reg, out _), Is.False);
    }
}
