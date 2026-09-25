// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.LsraGlobals;

namespace RyuJitSharp.UnitTests;

internal static class RegisterMaskTests
{
    [TestCase(regNumber.REG_NA, "NA")]
    [TestCase(regNumber.REG_STK, "STK")]
    [TestCase(regNumber.REG_RAX, "rax")]
    public static void RegisterNamesIncludeNativeSentinels(regNumber reg, string expected)
    {
        Assert.That(reg.Name, Is.EqualTo(expected));
    }

    [TestCase(regNumber.REG_RAX, regMask.SRBM_RAX, var_types.TYP_INT)]
    [TestCase(regNumber.REG_R31, regMask.SRBM_R31, var_types.TYP_INT)]
    [TestCase(regNumber.REG_XMM0, regMask.SRBM_XMM0, var_types.TYP_FLOAT)]
    [TestCase(regNumber.REG_XMM31, regMask.SRBM_XMM31, var_types.TYP_FLOAT)]
    [TestCase(regNumber.REG_K0, regMask.SRBM_K0, var_types.TYP_MASK)]
    [TestCase(regNumber.REG_K7, regMask.SRBM_K7, var_types.TYP_MASK)]
    public static void SingleTypeMasksPreserveNativeRegisterBits(regNumber reg, regMask expected, var_types type)
    {
        Assert.That(genSingleTypeRegMask(reg), Is.EqualTo(expected));
        Assert.That(genRegNumFromMask(expected, type), Is.EqualTo(reg));
        Assert.That(genExactlyOneBit(expected), Is.True);
    }

    [Test]
    public static void StackPseudoRegisterHasNoRegisterMask()
    {
        Assert.That(genSingleTypeRegMask(regNumber.REG_STK), Is.EqualTo(regMask.SRBM_NONE));
    }

    [TestCase(regMask.SRBM_NONE, true, false)]
    [TestCase(regMask.SRBM_RAX, true, true)]
    [TestCase(regMask.SRBM_XMM31, true, true)]
    [TestCase(regMask.SRBM_RAX | regMask.SRBM_XMM31, false, false)]
    public static void SingleBitQueriesIncludeZeroAndTheHighBit(regMask mask, bool maxOne, bool exactlyOne)
    {
        Assert.That(genMaxOneBit(mask), Is.EqualTo(maxOne));
        Assert.That(genExactlyOneBit(mask), Is.EqualTo(exactlyOne));
    }
}
