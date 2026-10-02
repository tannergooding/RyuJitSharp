// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if HAS_FIXED_REGISTER_SET
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static class ArgumentRegisterValidityTests
{
    [TestCase(CorInfoCallConvExtension.Managed)]
    [TestCase(CorInfoCallConvExtension.C)]
    [TestCase(CorInfoCallConvExtension.Stdcall)]
    [TestCase(CorInfoCallConvExtension.Thiscall)]
    [TestCase(CorInfoCallConvExtension.Fastcall)]
    [TestCase(CorInfoCallConvExtension.CMemberFunction)]
    [TestCase(CorInfoCallConvExtension.StdcallMemberFunction)]
    [TestCase(CorInfoCallConvExtension.FastcallMemberFunction)]
    [TestCase(CorInfoCallConvExtension.Swift)]
    public static void IntegerValidityUsesTheCompleteConventionMask(CorInfoCallConvExtension callConv)
    {
        var arguments = fullIntArgRegMask(callConv);
        for (var reg = REG_INT_FIRST; reg <= REG_INT_LAST; reg++)
        {
            Assert.That(isValidIntArgReg(reg, callConv), Is.EqualTo(arguments.IsSet(reg)));
        }

        Assert.That(isValidIntArgReg(REG_ARG_0, callConv), Is.True);
        Assert.That(isValidIntArgReg(REG_ARG_1, callConv), Is.True);
        Assert.That(isValidIntArgReg(REG_STK, callConv), Is.False);
    }

    [TestCase(FIRST_FP_ARGREG, true)]
    [TestCase(LAST_FP_ARGREG, true)]
    [TestCase((regNumber)((int)FIRST_FP_ARGREG - 1), false)]
    [TestCase((regNumber)((int)LAST_FP_ARGREG + 1), false)]
    [TestCase(REG_NA, false)]
    public static void FloatingValidityPreservesInclusiveEndpointsAndRejectsNoRegister(
        regNumber reg, bool expected)
    {
        Assert.That(isValidFloatArgReg(reg), Is.EqualTo(expected));
    }

#if HAS_MORE_THAN_64_REGISTERS && FEATURE_MASKED_HW_INTRINSICS
    [TestCase(CorInfoCallConvExtension.Managed)]
    [TestCase(CorInfoCallConvExtension.Swift)]
    public static void IntegerPredicatePreservesNativeBankLocalInput(CorInfoCallConvExtension callConv)
    {
        var lowerArguments = fullIntArgRegMask(callConv).Lower;
        var sawOverlap = false;
        for (var reg = REG_MASK_FIRST; reg <= REG_MASK_LAST; reg++)
        {
            var overlaps = (reg.SingleTypeMask & lowerArguments) != 0;
            Assert.That(isValidIntArgReg(reg, callConv), Is.EqualTo(overlaps));
            sawOverlap |= overlaps;
        }

        Assert.That(sawOverlap, Is.True);
    }
#endif
}
#endif
