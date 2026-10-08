// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_XARCH
using NUnit.Framework;
using static RyuJitSharp.insCC;

namespace RyuJitSharp.UnitTests;

internal static class GenTreeConditionCodeTests
{
    [TestCase(INS_CC_O, 0u)]
    [TestCase(INS_CC_NO, 1u)]
    [TestCase(INS_CC_B, 2u)]
    [TestCase(INS_CC_NB, 3u)]
    [TestCase(INS_CC_E, 4u)]
    [TestCase(INS_CC_NE, 5u)]
    [TestCase(INS_CC_BE, 6u)]
    [TestCase(INS_CC_NBE, 7u)]
    [TestCase(INS_CC_S, 8u)]
    [TestCase(INS_CC_NS, 9u)]
    [TestCase(INS_CC_TRUE, 10u)]
    [TestCase(INS_CC_FALSE, 11u)]
    [TestCase(INS_CC_L, 12u)]
    [TestCase(INS_CC_NL, 13u)]
    [TestCase(INS_CC_LE, 14u)]
    [TestCase(INS_CC_NLE, 15u)]
    public static void ValuesMatchNativeConditionCodes(insCC conditionCode, uint expected)
    {
        Assert.That((uint)conditionCode, Is.EqualTo(expected));
    }

    [TestCase(INS_CC_B, INS_CC_C)]
    [TestCase(INS_CC_B, INS_CC_NAE)]
    [TestCase(INS_CC_NB, INS_CC_NC)]
    [TestCase(INS_CC_NB, INS_CC_AE)]
    [TestCase(INS_CC_E, INS_CC_Z)]
    [TestCase(INS_CC_NE, INS_CC_NZ)]
    [TestCase(INS_CC_BE, INS_CC_NA)]
    [TestCase(INS_CC_NBE, INS_CC_A)]
    [TestCase(INS_CC_L, INS_CC_NGE)]
    [TestCase(INS_CC_NL, INS_CC_GE)]
    [TestCase(INS_CC_LE, INS_CC_NG)]
    [TestCase(INS_CC_NLE, INS_CC_G)]
    public static void AliasesRetainNativeValues(insCC conditionCode, insCC alias)
    {
        Assert.That(alias, Is.EqualTo(conditionCode));
    }
}
#endif
